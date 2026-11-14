using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// The profiler for a real game (single player or a client): says where the time goes, update against draw. Switch it with the chat command /levyprof (<see cref="LevyProfCommand"/>) or LEVY_PROF=1 at start.
	/// Every few seconds it prints a plain-English summary to chat (frames and updates per second, ms per update split NPCs / projectiles / rest, ms per frame of draw, the biggest Levy costs) and writes the same lines
	/// and one detailed "[Prof] client" line to the log. A fight at 2 FPS shows at once whether the updates (ms per update over 16.7, updates per second under 60) or the draw (ms per frame) is the problem.
	/// Off, nothing is hooked and nothing is measured: the game hooks are attached when it is switched on and removed when it is switched off.
	/// </summary>
	public class ClientProf : ModSystem
	{
		private static bool _on, _chat, _manual, _auto, _dmgHooked;
		private const double ManualIntervalSecs = 4.0, AutoIntervalSecs = 2.0;
		/// <summary>Self-check: a shorter window, and permission to run on a dedicated server.</summary>
		internal static double? TestInterval; internal static bool TestServer; internal static bool? TestAutoProfile;
		/// <summary>Chat lines the profiler printed (with the text a player saw). The self-check reads it.</summary>
		internal static readonly System.Collections.Generic.List<string> Said = new();
		private static readonly long[] Draw = new long[8]; // DoDraw, NPCs, projectiles, dust, gore, interface, players, spare
		private static readonly string[] DrawNames = { "total", "npcs", "projectiles", "dust", "gore", "interface", "players", "-" };
		/// <summary>Levy bodies' own draw (PreDraw, which includes the player renderer and the armor effects) and their nameplates (PostDraw), ticks.</summary>
		public static long SoldierBody, SoldierPlate; public static int SoldiersDrawn;
		private static long _update, _hitLoop; private static int _frames, _updates;
		private static long _lastLog;
		private static long _npcStart, _npc, _projStart, _proj;
				private static readonly long[] _snap = new long[LevyProf.N], _now = new long[LevyProf.N];
		// one automatic (recorded) fight, summed over its windows
		private static int _fWins; private static double _fSecs, _fFps, _fWorstFps, _fWorstAt, _fWorstUpd, _fWorstDraw; private static long _fUpdates; private static readonly long[] _fB = new long[LevyProf.N];

		/// <summary>True when the player switched the profiler on (the command, the key, LEVY_PROF=1).</summary>
		public static bool IsOn => _on && _manual;
		/// <summary>True while anything is measuring, including the automatic in-fight profiling.</summary>
		public static bool IsRunning => _on;
		/// <summary>True while profiling because a recorded fight is running.</summary>
		public static bool AutoRunning => _on && _auto;
		internal static bool DamageHookOn => _dmgHooked;

		public override void Load()
		{
			if (LevyProf.On && !Main.dedServ) Enable(chat: false);
			else if (Main.dedServ && Environment.GetEnvironmentVariable("LEVY_PROFTEST") == "1") Enable(chat: true); // lab: exercises the chat summary on a headless server
		}

		public override void Unload() { _manual = _auto = false; Detach(); }

		/// <summary>Attach the game hooks and start measuring everything. <paramref name="chat"/>: also print the summary to chat. This is the player's switch: it stays on until <see cref="Disable"/>.</summary>
		public static void Enable(bool chat)
		{
			if (Main.dedServ && Environment.GetEnvironmentVariable("LEVY_PROFTEST") != "1" && !TestServer) return;
			_manual = true; _chat = chat;
			if (!Attach()) { _manual = false; Detach(); return; }
			WantDamageHook(true);
			ResetWindow();
		}

		/// <summary>The player's switch off. A recorded fight that is running keeps its own in-fight profiling.</summary>
		public static void Disable()
		{
			_manual = false; _chat = false;
			if (_auto && _on) { WantDamageHook(false); ResetWindow(); }
			else { _auto = false; Detach(); }
		}

		/// <summary>The keybind and anything else that flips the player's switch. Says what it did in chat, in the same words as /levyprof.</summary>
		public static void Toggle()
		{
			if (_manual) { Disable(); Say("Levy profiler is off.", Color.LightGray); return; }
			Enable(chat: true);
			Say(IsOn ? "Levy profiler is on. A summary appears every 4 seconds and goes to the log. Press the key again to stop." : "Levy profiler could not start. The log has the reason.", IsOn ? Color.LightGreen : Color.OrangeRed);
		}

		internal static Color LastSayColor;
		internal static void Say(string text, Color c) { LastSayColor = c; Said.Add(text); if (Said.Count > 200) Said.RemoveAt(0); if (!Main.dedServ) Main.NewText(text, c); }

		// ------------------------------------------------------------------------------------------ automatic, around a recorded fight
		/// <summary>A recording of an automatic fight just started: profile it unless the player turned the profiler on themselves or switched this off in the config. Nothing goes to chat until the fight is over.</summary>
		public static void AutoStart()
		{
			bool want = true;
			if (TestAutoProfile.HasValue) want = TestAutoProfile.Value;
			else { try { want = global::Levy.Common.Recording.FightRecConfig.Get?.ProfileRecordedFights ?? true; } catch (Exception) { } }
			if (!want || _auto) return;
			if (Main.dedServ && !TestServer) return;
			_auto = true;
			ClearFight();
			if (_on) { Levy.Instance?.Logger.Info("[Prof] fight profiling joins the profiler that is already on"); return; }
			if (!Attach()) { _auto = false; Detach(); return; }
			Levy.Instance?.Logger.Info("[Prof] fight profiling on (windows of " + (TestInterval ?? AutoIntervalSecs) + " s, nothing in chat until the fight ends)");
		}

		/// <summary>The recording is about to stop. Closes the last window (so it lands in the recording), switches the in-fight profiling off unless the player's own is on, and says one line in chat.</summary>
		public static void AutoEnding(bool quiet)
		{
			if (!_auto) return;
			try { if (_on) Report(true); } catch (Exception e) { Levy.Instance?.Logger.Warn("[Prof] last window failed: " + e.Message); }
			_auto = false;
			string line = FightSummary();
			if (_manual) ResetWindow(); else Detach();
			if (line == null) return;
			Levy.Instance?.Logger.Info("[Prof] fight summary: " + line);
			if (!quiet) Say(line, Color.LightGoldenrodYellow);
		}

		private static void ClearFight() { _fWins = 0; _fSecs = _fFps = _fWorstAt = _fWorstUpd = _fWorstDraw = 0; _fWorstFps = double.MaxValue; _fUpdates = 0; Array.Clear(_fB, 0, _fB.Length); }

		/// <summary>The Levy sections worth naming (leaf costs inside the army's update, not the totals that contain them).</summary>
		private static readonly int[] Leaf = { LevyProf.Dodge, LevyProf.Worm, LevyProf.Zone, LevyProf.Rows, LevyProf.Brain, LevyProf.Perch, LevyProf.Move, LevyProf.Combat, LevyProf.Dmg, LevyProf.Dummy, LevyProf.Stats, LevyProf.Rip, LevyProf.ArmTick, LevyProf.ArmUi };

		private static string FightSummary()
		{
			if (_fWins == 0 || _fSecs <= 0) return null;
			double f = 1000.0 / Stopwatch.Frequency, nUp = Math.Max(1, _fUpdates);
			int best = -1; double bestMs = 0;
			foreach (int k in Leaf) { double ms = _fB[k] * f / nUp; if (ms > bestMs) { bestMs = ms; best = k; } }
			double ai = _fB[LevyProf.AI] * f / nUp;
			string top = best >= 0 && bestMs >= 0.05 ? $" Biggest Levy cost: {LevyProf.NameOf(best)}, {Ms(bestMs)} ms per update (all Levy AI {Ms(ai)} ms)." : $" Levy AI costs {Ms(ai)} ms per update.";
			int at = (int)_fWorstAt;
			return $"Fight profile: {Ms(_fFps / _fSecs)} fps on average, worst {Ms(_fWorstFps)} fps at {at / 60}:{at % 60:00} (update {Ms(_fWorstUpd)} ms, draw {Ms(_fWorstDraw)} ms).{top}";
		}

		// ------------------------------------------------------------------------------------------ hooks
		private static bool Attach()
		{
			if (_on) return true;
			try
			{
				LevyProf.On = true;
				ResetWindow();
				On_Main.DoUpdate += DoUpdate;
				On_Main.DoDraw += DoDraw;
				On_Main.DrawNPCs += DrawNpcs;
				On_Main.DrawProjectiles += DrawProj;
				On_Main.DrawDust += DrawDust;
				On_Main.DrawGore += DrawGore;
				On_Main.DrawInterface += DrawInterface;
				On_Main.DrawPlayers_AfterProjectiles += DrawPlayers;
				_on = true;
				return true;
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[Prof] client profiler could not hook the game: " + e.Message); return false; }
		}

		/// <summary>The per-projectile hit-check timer is the one hook whose cost grows with the shots in the air, so only the player's own profiler carries it; the in-fight profiling leaves it off.</summary>
		private static void WantDamageHook(bool want)
		{
			if (want == _dmgHooked) return;
			try { if (want) On_Projectile.Damage += DamageTimer; else On_Projectile.Damage -= DamageTimer; _dmgHooked = want; } catch (Exception) { }
		}

		private static void Detach()
		{
			try
			{
				On_Main.DoUpdate -= DoUpdate; On_Main.DoDraw -= DoDraw; On_Main.DrawNPCs -= DrawNpcs; On_Main.DrawProjectiles -= DrawProj; On_Main.DrawDust -= DrawDust;
				On_Main.DrawGore -= DrawGore; On_Main.DrawInterface -= DrawInterface; On_Main.DrawPlayers_AfterProjectiles -= DrawPlayers; On_Projectile.Damage -= DamageTimer;
			}
			catch (Exception) { }
			_chat = false; _dmgHooked = false;
			LevyProf.On = LevyProf.OnFromStart;
			_on = false;
		}

		private static void ResetWindow()
		{
			_update = _npc = _proj = _hitLoop = 0; _npcStart = _projStart = 0; _updates = _frames = 0; Array.Clear(Draw, 0, Draw.Length); SoldierBody = SoldierPlate = 0; SoldiersDrawn = 0;
			LevyProf.CopyTo(_snap); _lastLog = Stopwatch.GetTimestamp();
		}

		public override void PreUpdateNPCs() { if (_on) _npcStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateNPCs() { if (_on && _npcStart != 0) { _npc += Stopwatch.GetTimestamp() - _npcStart; _npcStart = 0; } }
		public override void PreUpdateProjectiles() { if (_on) _projStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateProjectiles() { if (_on && _projStart != 0) { _proj += Stopwatch.GetTimestamp() - _projStart; _projStart = 0; } }

		private static void DoUpdate(On_Main.orig_DoUpdate orig, Main self, ref GameTime gameTime)
		{
			long t0 = Stopwatch.GetTimestamp();
			try { orig(self, ref gameTime); }
			finally { EndUpdate(t0); }
		}

		private static void EndUpdate(long t0)
		{
			_update += Stopwatch.GetTimestamp() - t0; _updates++;
			Report();
		}

		/// <summary>Self-check: one game update as the hook sees it (the caller runs the update between the two calls), and one drawn frame.</summary>
		internal static long TestBegin() => _on ? Stopwatch.GetTimestamp() : 0;
		internal static void TestEnd(long t0) { if (_on && t0 != 0) EndUpdate(t0); }
		internal static void TestFrame(long ticks) { if (_on) { Draw[0] += ticks; _frames++; } }

		private static void DoDraw(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
		{
			long t0 = Stopwatch.GetTimestamp();
			try { orig(self, gameTime); }
			finally { Draw[0] += Stopwatch.GetTimestamp() - t0; _frames++; }
		}

		private static void DrawNpcs(On_Main.orig_DrawNPCs orig, Main self, bool behindTiles) { long t0 = Stopwatch.GetTimestamp(); try { orig(self, behindTiles); } finally { Draw[1] += Stopwatch.GetTimestamp() - t0; } }
		private static void DrawProj(On_Main.orig_DrawProjectiles orig, Main self) { long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { Draw[2] += Stopwatch.GetTimestamp() - t0; } }
		private static void DrawDust(On_Main.orig_DrawDust orig, Main self) { long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { Draw[3] += Stopwatch.GetTimestamp() - t0; } }
		private static void DrawGore(On_Main.orig_DrawGore orig, Main self) { long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { Draw[4] += Stopwatch.GetTimestamp() - t0; } }
		private static void DrawInterface(On_Main.orig_DrawInterface orig, Main self, GameTime gameTime) { long t0 = Stopwatch.GetTimestamp(); try { orig(self, gameTime); } finally { Draw[5] += Stopwatch.GetTimestamp() - t0; } }
		private static void DrawPlayers(On_Main.orig_DrawPlayers_AfterProjectiles orig, Main self) { long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { Draw[6] += Stopwatch.GetTimestamp() - t0; } }
		private static void DamageTimer(On_Projectile.orig_Damage orig, Projectile self) { long t0 = Stopwatch.GetTimestamp(); try { orig(self); } finally { _hitLoop += Stopwatch.GetTimestamp() - t0; } }

		private static string Ms(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

		private static void Report(bool force = false)
		{
			long now = Stopwatch.GetTimestamp();
			double secs = (now - _lastLog) / (double)Stopwatch.Frequency;
			if (force ? secs < 0.5 : secs < (TestInterval ?? (_manual ? ManualIntervalSecs : AutoIntervalSecs))) return;
			double f = 1000.0 / Stopwatch.Frequency, ups = _updates / secs, fps = _frames / secs;
			double nUp = Math.Max(1, _updates), fr = Math.Max(1, _frames);
			double upd = _update * f / nUp, npc = _npc * f / nUp, proj = _proj * f / nUp, hit = _hitLoop * f / nUp, rest = Math.Max(0, upd - npc - proj), draw = Draw[0] * f / fr;
			LevyProf.CopyTo(_now);
			double B(int k) => (_now[k] - _snap[k]) * f / nUp;
			double ai = B(LevyProf.AI), gear = B(LevyProf.Stats);

			string l1 = $"Levy profiler: {Ms(fps)} frames and {Ms(ups)} updates per second.";
			string l2 = upd > 16.7 ? $"One update takes {Ms(upd)} ms. Holding 60 per second needs under 16.7 ms, so the updates are the slow part." : $"One update takes {Ms(upd)} ms. That is fast enough for 60 per second.";
			string l3 = $"Per update: NPCs {Ms(npc)} ms, projectiles {Ms(proj)} ms (shot hit checks {Ms(hit)} ms of that), everything else {Ms(rest)} ms. One frame draws in {Ms(draw)} ms.";
			string l4 = Main.netMode == 1 ? "The levy army runs on the host, so its costs show in the host's profiler."
				: $"Levy costs per update: AI {Ms(ai)} ms (dodging {Ms(B(LevyProf.Dodge))}, moving {Ms(B(LevyProf.Move))}, fighting {Ms(B(LevyProf.Combat))}, deciding {Ms(B(LevyProf.Brain))}), gear upkeep {Ms(gear)} ms.";

			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < 7; i++) sb.Append(i > 0 ? ", " : "").Append(DrawNames[i]).Append(' ').Append((Draw[i] * f / fr).ToString("0.00"));
			Levy.Instance?.Logger.Info($"[Prof] client{(_auto && !_manual ? " (fight, no hit loop)" : "")}: fps {fps:0.0} ups {ups:0.0} | update ms/tick {upd:0.00} (npcs {npc:0.00}, projectiles {proj:0.00}, hit loop {hit:0.00}) | draw ms/frame: {sb} | levy bodies {SoldierBody * f / fr:0.00}, nameplates {SoldierPlate * f / fr:0.00} ({SoldiersDrawn / fr:0.0}/frame) | npcs {CountActiveNpcs()}, projectiles {CountActiveProjectiles()} | levy buckets over this window, ms: {LevyProf.DescribeDelta(_snap, 14)}");
			if (global::Levy.Common.Recording.FightRecorder.On) global::Levy.Common.Recording.FightRecorder.NoteProf(fps, ups, upd, npc, proj, draw, ai, gear, LevyProf.DescribeDelta(_snap, 6), secs);
			if (_auto)
			{
				_fWins++; _fSecs += secs; _fFps += fps * secs; _fUpdates += _updates;
				for (int k = 0; k < LevyProf.N; k++) _fB[k] += _now[k] - _snap[k];
				if (fps < _fWorstFps) { _fWorstFps = fps; _fWorstAt = global::Levy.Common.Recording.FightRecorder.Elapsed / 60.0; _fWorstUpd = upd; _fWorstDraw = draw; }
			}
			if (_chat)
			{
				foreach (string l in new[] { l1, l2, l3, l4 }) { Levy.Instance?.Logger.Info("[Prof] " + l); Say(l, Color.LightGoldenrodYellow); }
			}
			ResetWindow();
		}

		private static int CountActiveNpcs() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) n++; return n; }
		private static int CountActiveProjectiles() { int n = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) n++; return n; }
	}

	/// <summary>The "Toggle Levy profiler" key: the same switch as /levyprof, for a fight where typing is not possible. Unbound until the player picks a key in the controls menu (Settings, Controls, Mod Controls). Read in the system's update, which runs every tick, so it works while dead and respawning.</summary>
	public class ClientProfKey : ModSystem
	{
		public static ModKeybind Key;
		private static bool _wasDown;
		public override void Load() { Key = KeybindLoader.RegisterKeybind(Mod, "ToggleProfiler", Microsoft.Xna.Framework.Input.Keys.None); }
		public override void Unload() { Key = null; }
		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gameMenu || Key == null) return;
			bool typing = Main.drawingPlayerChat || Terraria.GameInput.PlayerInput.WritingText;
			bool down = Key.Current && !typing;
			if (down && !_wasDown) ClientProf.Toggle();
			_wasDown = down;
		}
	}

	/// <summary>/levyprof [on|off]: the profiler for a player who cannot set environment variables. Prints a short summary to chat every few seconds and writes it to the log.</summary>
	public class LevyProfCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyprof";
		public override string Usage => "/levyprof [on|off]";
		public override string Description => "Measures where the game's time goes and prints a short summary every few seconds. Type it again to stop.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			bool want = args.Length > 0 ? (args[0].Equals("on", StringComparison.OrdinalIgnoreCase) || args[0] == "1") : !ClientProf.IsOn;
			if (want) { ClientProf.Enable(chat: true); caller.Reply(ClientProf.IsOn ? "Levy profiler is on. A summary appears every 4 seconds and goes to the log. Type /levyprof again to stop." : "Levy profiler could not start. The log has the reason.", ClientProf.IsOn ? Color.LightGreen : Color.OrangeRed); }
			else { ClientProf.Disable(); caller.Reply("Levy profiler is off.", Color.LightGray); }
		}
	}
}
