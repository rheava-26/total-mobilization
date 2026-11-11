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
		private static bool _on, _chat;
		private const double IntervalSecs = 4.0;
		private static readonly long[] Draw = new long[8]; // DoDraw, NPCs, projectiles, dust, gore, interface, players, spare
		private static readonly string[] DrawNames = { "total", "npcs", "projectiles", "dust", "gore", "interface", "players", "-" };
		/// <summary>Levy bodies' own draw (PreDraw, which includes the player renderer and the armor effects) and their nameplates (PostDraw), ticks.</summary>
		public static long SoldierBody, SoldierPlate; public static int SoldiersDrawn;
		private static long _update, _updateStart, _hitLoop; private static int _frames, _updates;
		private static long _lastLog;
		private static long _npcStart, _npc, _projStart, _proj;
		private static readonly long[] _snap = new long[LevyProf.N], _now = new long[LevyProf.N];

		public static bool IsOn => _on;

		public override void Load()
		{
			if (LevyProf.On && !Main.dedServ) Enable(chat: false);
		}

		public override void Unload() { Disable(); }

		/// <summary>Attach the game hooks and start measuring. <paramref name="chat"/>: also print the summary to chat.</summary>
		public static void Enable(bool chat)
		{
			_chat = chat;
			if (_on || Main.dedServ) return;
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
				On_Projectile.Damage += DamageTimer;
				_on = true;
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[Prof] client profiler could not hook the game: " + e.Message); Disable(); }
		}

		public static void Disable()
		{
			try
			{
				On_Main.DoUpdate -= DoUpdate; On_Main.DoDraw -= DoDraw; On_Main.DrawNPCs -= DrawNpcs; On_Main.DrawProjectiles -= DrawProj; On_Main.DrawDust -= DrawDust;
				On_Main.DrawGore -= DrawGore; On_Main.DrawInterface -= DrawInterface; On_Main.DrawPlayers_AfterProjectiles -= DrawPlayers; On_Projectile.Damage -= DamageTimer;
			}
			catch (Exception) { }
			if (!LevyProf.OnFromStart) LevyProf.On = false;
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
			finally { _update += Stopwatch.GetTimestamp() - t0; _updates++; Report(); }
		}

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

		private static void Report()
		{
			long now = Stopwatch.GetTimestamp();
			double secs = (now - _lastLog) / (double)Stopwatch.Frequency;
			if (secs < IntervalSecs) return;
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
			Levy.Instance?.Logger.Info($"[Prof] client: fps {fps:0.0} ups {ups:0.0} | update ms/tick {upd:0.00} (npcs {npc:0.00}, projectiles {proj:0.00}, hit loop {hit:0.00}) | draw ms/frame: {sb} | levy bodies {SoldierBody * f / fr:0.00}, nameplates {SoldierPlate * f / fr:0.00} ({SoldiersDrawn / fr:0.0}/frame) | npcs {CountActiveNpcs()}, projectiles {CountActiveProjectiles()} | levy buckets over this window, ms: {LevyProf.DescribeDelta(_snap, 14)}");
			if (_chat)
			{
				foreach (string l in new[] { l1, l2, l3, l4 }) { Levy.Instance?.Logger.Info("[Prof] " + l); Main.NewText(l, Color.LightGoldenrodYellow); }
			}
			ResetWindow();
		}

		private static int CountActiveNpcs() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) n++; return n; }
		private static int CountActiveProjectiles() { int n = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) n++; return n; }
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
