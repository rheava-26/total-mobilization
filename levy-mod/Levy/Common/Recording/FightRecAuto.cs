using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Headless auto-recording for harness fights. Inert unless the environment variable LEVY_REC_DIR is set (and this is a dedicated server). With it set, a boss that spawns while a levy body exists starts a recording
	/// of Main.player[0] (the stand-in commander every harness uses) into that folder; it stops when the boss is gone, a new trial starts, or the 10 minute limit passes. One file per fight, named
	/// harness_boss_arena_seed_mode_NN when those are known. Harness-agnostic: nothing here knows which harness runs. A harness may call <see cref="Tag"/> to add its own labels.
	/// </summary>
	internal static class FightRecAuto
	{
		public static readonly string Dir = Environment.GetEnvironmentVariable("LEVY_REC_DIR");
		public static bool Enabled => !string.IsNullOrEmpty(Dir) && Main.dedServ;

		private static readonly Dictionary<string, string> _tags = new();
		private static int _seq, _cooldownUntil;
		private static bool _announced;

		/// <summary>Optional: a harness labels the fight it is about to run (arena, seed, mode, ...). Cleared when a recording starts.</summary>
		public static void Tag(params (string key, object value)[] kv)
		{
			if (!Enabled) return;
			foreach (var (k, v) in kv) _tags[k] = Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
		}

		private static string Env(string k) { string v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? null : v.Trim(); }
		private static string Single(string k) { string v = Env(k); return v != null && !v.Contains(',') ? v : null; }

		/// <summary>Called once per game tick (FightRecSystem.PostUpdateEverything) before the recorder's own tick.</summary>
		public static void Update()
		{
			if (!Enabled) return;
			if (!_announced) { _announced = true; Levy.Instance?.Logger.Info("[FightRec] headless auto-recording on, folder " + Dir); }
			if (FightRecorder.On) return;
			if ((int)Main.GameUpdateCount < _cooldownUntil) return;
			NPC boss = null; bool levy = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				if (n.ModNPC is SoldierNPC) { levy = true; continue; }
				if (boss == null && n.boss && !n.friendly && !n.townNPC) boss = n;
			}
			if (boss == null || !levy) return;
			Player cmd = Main.player[0];
			FightRecorder.Headless = true;
			FightRecorder.OutDirOverride = Dir;
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
			meta["bossName"] = boss.FullName;
			string stem = harness + "_" + boss.FullName;
			foreach (string k in new[] { "arena", "seed", "mode", "target", "gear" })
				if (meta.TryGetValue(k, out string v) && !string.IsNullOrEmpty(v)) stem += "_" + (k == "seed" ? "s" : k == "mode" ? "m" : "") + v;
			stem += "_" + _seq.ToString("00");
			string path = FightRecorder.Start(cmd, boss.FullName, out string err, meta, stem);
			if (path == null) { Levy.Instance?.Logger.Warn("[FightRec] headless start failed: " + err); _cooldownUntil = (int)Main.GameUpdateCount + 300; }
			else Levy.Instance?.Logger.Info("[FightRec] headless recording started: " + path);
		}

		/// <summary>The recorder stopped in headless mode: a short pause so the next trial does not start a file on a dying boss.</summary>
		public static void Stopped() { _cooldownUntil = (int)Main.GameUpdateCount + 20; }
	}
}
