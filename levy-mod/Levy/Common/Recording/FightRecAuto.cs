using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Starts and stops recordings by itself, in two settings.
	/// HEADLESS (a harness): inert unless the environment variable LEVY_REC_DIR is set on a dedicated server. A boss that spawns while a levy body exists starts a recording of Main.player[0] (the stand-in commander every harness uses)
	/// into that folder; it stops when the boss is gone, a new trial starts, or the 10 minute limit passes. One file per fight, named harness_boss_arena_seed_mode_NN when those are known. Harness-agnostic: nothing here knows
	/// which harness runs. A harness may call <see cref="Tag"/> to add its own labels.
	/// CLIENT (a player, single player or the host's own game): the "Record fights automatically" setting. Boss fights: a fight boss within 250 tiles of the commander starts it. All fights: also a fight where at least one levy
	/// stands next to the commander and three or more hostile NPCs (or one big one) are within 60 tiles. It stops 5 seconds after the boss is gone, or after 5 seconds with no hostile NPC near the commander when there was no boss.
	/// </summary>
	internal static class FightRecAuto
	{
		public static readonly string Dir = Environment.GetEnvironmentVariable("LEVY_REC_DIR");
		/// <summary>Self-check: drive the headless (harness) path in a world check without LEVY_REC_DIR; recordings go to this folder.</summary>
		public static string TestHeadlessDir;
		private static string HeadlessDir => TestHeadlessDir ?? Dir;
		public static bool Enabled => !string.IsNullOrEmpty(HeadlessDir) && Main.dedServ;

		/// <summary>Self-check: act as a client on a dedicated server (the commander is Main.player[0]) and use this mode instead of the config.</summary>
		public static bool TestClient;
		public static FightRecMode? TestMode;
		/// <summary>Self-check: stand-ins for the config's recording folder and for the default folder.</summary>
		public static string TestFolder, TestDefaultDir;

		public const int ScanEvery = 10, QuietTicks = 300, CooldownTicks = 600, BossGoneCooldownTicks = 60, BossRangeTiles = 250, ArmyRangeTiles = 60, MinThreats = 3, BigThreatLife = 1500;

		private static readonly Dictionary<string, string> _tags = new();
		private static int _seq, _cooldownUntil, _quiet;
		private static bool _announced, _recovered;
		private static readonly ConcurrentQueue<(string text, bool warn, bool once, Color? color)> _posts = new();
		private static readonly HashSet<string> _said = new();
		/// <summary>Chat lines the self-check saw (warnings are queued from any thread and shown on the game thread).</summary>
		public static readonly List<string> Said = new();

		/// <summary>Optional: a harness labels the fight it is about to run (arena, seed, mode, ...). Cleared when a recording starts.</summary>
		public static void Tag(params (string key, object value)[] kv)
		{
			if (!Enabled) return;
			foreach (var (k, v) in kv) _tags[k] = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
		}

		private static string Env(string k) { string v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? null : v.Trim(); }
		private static string Single(string k) { string v = Env(k); return v != null && !v.Contains(',') ? v : null; }

		/// <summary>A line for the player's chat, from any thread. The same line is shown once per session.</summary>
		public static void Post(string text, bool warn)
		{
			if (string.IsNullOrEmpty(text)) return;
			Levy.Instance?.Logger.Info("[FightRec] " + text);
			_posts.Enqueue((text, warn, true, null));
		}

		/// <summary>A line for the player's chat that is shown every time it is posted (the post-fight summary repeats from fight to fight), from any thread.</summary>
		public static void PostLine(string text, Color color)
		{
			if (string.IsNullOrEmpty(text)) return;
			_posts.Enqueue((text, false, false, color));
		}

		/// <summary>Game thread, every tick: shows queued lines.</summary>
		public static void Pump()
		{
			while (_posts.TryDequeue(out var m))
			{
				if (m.once && !_said.Add(m.text)) continue;
				Said.Add(m.text);
				if (!Main.dedServ) Main.NewText(m.text, m.color ?? (m.warn ? Color.OrangeRed : Color.LightGray));
			}
		}

		/// <summary>Forget what was said (self-check, and a new world).</summary>
		public static void ResetMessages() { _said.Clear(); Said.Clear(); while (_posts.TryDequeue(out _)) { } }

		public static FightRecMode CurrentMode()
		{
			if (TestMode.HasValue) return TestMode.Value;
			try { return FightRecConfig.Get?.RecordFightsAutomatically ?? FightRecMode.BossFights; } catch { return FightRecMode.BossFights; }
		}

		private static bool ClientActive => (!Main.dedServ || TestClient) && Main.netMode != NetmodeID.MultiplayerClient;
		private static Player Commander => Main.dedServ ? Main.player[0] : Main.LocalPlayer;

		/// <summary>Called once per game tick (FightRecSystem.PostUpdateEverything) while no recording runs.</summary>
		public static void Update()
		{
			if (Enabled) { HeadlessUpdate(); return; }
			if (!ClientActive) return;
			ClientUpdate();
		}

		/// <summary>The recorder stopped: a pause before the next automatic start, so a leftover add or the next wave of the same fight does not open a file at once.</summary>
		public static void RecorderStopped(string reason)
		{
			int pause = reason == "stopped by command" ? 1800 : reason == "write error" || reason == "size limit" ? 3600 : reason == "boss gone" && FightRecorder.Auto == 1 ? BossGoneCooldownTicks : CooldownTicks;
			if (!Headless) _cooldownUntil = (int)Main.GameUpdateCount + pause;
			_quiet = 0;
		}
		private static bool Headless => FightRecorder.Headless;
		/// <summary>Self-check: forget the pause after a recording.</summary>
		public static void ClearCooldown() { _cooldownUntil = 0; }

		// ------------------------------------------------------------------------------------------ client
		private static void ClientUpdate()
		{
			if (Main.GameUpdateCount % ScanEvery != 0 || (int)Main.GameUpdateCount < _cooldownUntil) return;
			FightRecMode mode = CurrentMode();
			if (mode == FightRecMode.Off) return;
			Player cmd = Commander;
			if (cmd == null || !cmd.active || cmd.dead) return;
			if (!_recovered) { _recovered = true; RecoverOldParts(); }
			NPC boss = null; int levyNear = 0, threats = 0; bool big = false;
			float bossR = BossRangeTiles * 16f, nearR = ArmyRangeTiles * 16f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.townNPC) continue;
				float dx = Math.Abs(n.Center.X - cmd.Center.X), dy = Math.Abs(n.Center.Y - cmd.Center.Y);
				if (n.ModNPC is SoldierNPC) { if (dx < nearR && dy < nearR) levyNear++; continue; }
				if (n.friendly) continue;
				if (boss == null && dx < bossR && dy < bossR && FightRecorder.IsFightBoss(n)) boss = n;
				if (mode == FightRecMode.AllFights && IsThreat(n) && dx < nearR && dy < nearR) { threats++; if (n.lifeMax >= BigThreatLife) big = true; }
			}
			bool army = mode == FightRecMode.AllFights && levyNear >= 1 && (threats >= MinThreats || big);
			if (boss == null && !army) return;
			var meta = new Dictionary<string, string> { ["source"] = "auto", ["trigger"] = boss != null ? "boss" : "army", ["setting"] = mode.ToString(), ["levyNear"] = levyNear.ToString(), ["threats"] = threats.ToString() };
			FightRecorder.AutoKind = mode == FightRecMode.BossFights ? 1 : 2;
			FightRecorder.FightLabel = boss == null ? FightLabel() : null;
			if (boss == null) meta["label"] = FightRecorder.FightLabel;
			string path = FightRecorder.Start(cmd, (boss == null ? null : FightRecorder.NpcName(boss)), out string err, meta);
			FightRecorder.AutoKind = 0;
			if (path == null)
			{
				Post("Fight recording could not start: " + err, true);
				_cooldownUntil = (int)Main.GameUpdateCount + 3600;
			}
			else Levy.Instance?.Logger.Info("[FightRec] automatic recording started (" + meta["trigger"] + ", " + mode + "): " + path);
			_quiet = 0;
		}

		/// <summary>A hostile NPC that can hurt: touching damage, real life, not a critter or a decoy.</summary>
		private static bool IsThreat(NPC n) => !n.friendly && !n.townNPC && n.damage > 0 && n.lifeMax > 5 && !n.dontCountMe && n.CanBeChasedBy();

		private static string FightLabel()
		{
			if (Main.pumpkinMoon) return "Pumpkin_Moon";
			if (Main.snowMoon) return "Frost_Moon";
			if (Main.eclipse) return "Solar_Eclipse";
			switch (Main.invasionType)
			{
				case InvasionID.GoblinArmy: return "Goblin_Army";
				case InvasionID.SnowLegion: return "Frost_Legion";
				case InvasionID.PirateInvasion: return "Pirate_Invasion";
				case InvasionID.MartianMadness: return "Martian_Madness";
			}
			if (Main.bloodMoon) return "Blood_Moon";
			return "Fight";
		}

		/// <summary>Called every tick while a recording runs, after its own tick: stops an all-fights recording that never met a boss once nothing hostile has been near the commander for 5 seconds, and honours the setting being switched off.</summary>
		public static void Watch()
		{
			if (FightRecorder.Auto == 0 || !FightRecorder.On) return;
			if (CurrentMode() == FightRecMode.Off) { FightRecorder.Stop("turned off"); return; }
			if (FightRecorder.Auto != 2 || FightRecorder.BossSeen || Main.GameUpdateCount % ScanEvery != 0) return;
			Player cmd = Commander; if (cmd == null) return;
			float nearR = ArmyRangeTiles * 16f; int threats = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !IsThreat(n) || n.ModNPC is SoldierNPC) continue;
				if (Math.Abs(n.Center.X - cmd.Center.X) < nearR && Math.Abs(n.Center.Y - cmd.Center.Y) < nearR) threats++;
			}
			if (threats > 0) _quiet = 0;
			else if ((_quiet += ScanEvery) >= QuietTicks) FightRecorder.Stop("fight over");
		}

		private static void RecoverOldParts()
		{
			string cfg = TestFolder; if (cfg == null) { try { cfg = FightRecConfig.Get?.RecordingFolder; } catch { } }
			string dir = FightRecorder.OutDirOverride ?? FightRecFolder.Resolve(cfg, TestDefaultDir ?? FightRecFolder.DefaultDir, out string w);
			if (dir == null) return;
			System.Threading.ThreadPool.QueueUserWorkItem(_ =>
			{
				int n = FightRecFolder.RecoverParts(dir);
				if (n > 0) Levy.Instance?.Logger.Info("[FightRec] renamed " + n + " unfinished recording(s) from an earlier crash in " + dir);
			});
		}

		// ------------------------------------------------------------------------------------------ headless (harness)
		private static void HeadlessUpdate()
		{
			if (!_announced && TestHeadlessDir == null) { _announced = true; Levy.Instance?.Logger.Info("[FightRec] headless auto-recording on, folder " + Dir); }
			if (FightRecorder.On) return;
			if ((int)Main.GameUpdateCount < _cooldownUntil) return;
			NPC boss = null; bool levy = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				if (n.ModNPC is SoldierNPC) { levy = true; continue; }
				if (boss == null && FightRecorder.IsFightBoss(n) && !n.friendly && !n.townNPC) boss = n;
			}
			if (boss == null || !levy) return;
			Player cmd = Main.player[0];
			FightRecorder.Headless = true;
			FightRecorder.OutDirOverride = HeadlessDir;
			FightRecorder.IgnoreInactive = true;
			var meta = new Dictionary<string, string>();
			string harness = Env("LEVY_HARNESS_SCENARIO") ?? (Env("LEVY_FIELD_SPEC") != null ? "field" : "harness");
			meta["harness"] = harness;
			string fs = Env("LEVY_FIELD_SPEC"); if (fs != null) meta["spec"] = fs;
			string arena = Single("LEVY_REAL_ARENA"), seed = Single("LEVY_REAL_SEEDS") == "1" ? "1" : null, mode = Env("LEVY_REAL_MODE"), target = Env("LEVY_REAL_TARGET"), order = Env("LEVY_REAL_ORDER"), gear = Single("LEVY_REAL_GEAR");
			if (arena != null) meta["arena"] = arena;
			if (seed != null) meta["seed"] = seed;
			if (mode != null) meta["mode"] = mode;
			if (target != null) meta["target"] = target;
			if (order != null) meta["order"] = order;
			if (gear != null) meta["gear"] = gear;
			foreach (var kv in _tags) meta[kv.Key] = kv.Value; // the harness's own labels win
			_tags.Clear();
			_seq++;
			meta["trial"] = _seq.ToString();
			meta["bossName"] = FightRecorder.NpcName(boss);
			string stem = harness + "_" + FightRecorder.NpcName(boss);
			foreach (string k in new[] { "arena", "seed", "mode", "target", "gear" })
				if (meta.TryGetValue(k, out string v) && !string.IsNullOrEmpty(v)) stem += "_" + (k == "seed" ? "s" : k == "mode" ? "m" : "") + v;
			stem += "_" + _seq.ToString("00");
			string path = FightRecorder.Start(cmd, FightRecorder.NpcName(boss), out string err, meta, stem);
			if (path == null) { Levy.Instance?.Logger.Warn("[FightRec] headless start failed: " + err); _cooldownUntil = (int)Main.GameUpdateCount + 300; }
			else Levy.Instance?.Logger.Info("[FightRec] headless recording started: " + path);
		}

		/// <summary>The recorder stopped in headless mode: a short pause so the next trial does not start a file on a dying boss.</summary>
		public static void Stopped() { _cooldownUntil = (int)Main.GameUpdateCount + 20; }
		/// <summary>Self-check: the trial counter, so the headless check can restore it.</summary>
		public static int Seq { get => _seq; set => _seq = value; }
	}
}
