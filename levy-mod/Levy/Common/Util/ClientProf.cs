using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// LEVY_PROF=1 on a CLIENT (single player): says where a real game's time goes, update against draw, in the log. Every two seconds one "[Prof] client" line gives frames and updates per second, the CPU time of one update
	/// (and what it was made of: NPCs, projectiles, the rest) and of one frame's draw split into NPCs, projectiles, dust, gore, players, the interface and the levy bodies / nameplates themselves.
	/// A fight that runs at 2 FPS shows at once whether the updates (ms per update over 16.7, ups under 60) or the draw (ms per frame) is the problem. Without the variable nothing is hooked and nothing is measured.
	/// </summary>
	public class ClientProf : ModSystem
	{
		private static bool _on;
		private static readonly long[] Draw = new long[8]; // DoDraw, NPCs, projectiles, dust, gore, interface, players, spare
		private static readonly string[] DrawNames = { "total", "npcs", "projectiles", "dust", "gore", "interface", "players", "-" };
		/// <summary>Levy bodies' own draw (PreDraw, which includes the player renderer and the armor effects) and their nameplates (PostDraw), ticks.</summary>
		public static long SoldierBody, SoldierPlate; public static int SoldiersDrawn;
		private static long _update, _updateStart; private static int _frames, _updates;
		private static long _lastLog;
		private static long _entStart, _ent, _npcStart, _npc, _projStart, _proj;

		public override void Load()
		{
			if (!LevyProf.On || Main.dedServ) return;
			try
			{
				On_Main.DoUpdate += DoUpdate;
				On_Main.DoDraw += DoDraw;
				On_Main.DrawNPCs += DrawNpcs;
				On_Main.DrawProjectiles += DrawProj;
				On_Main.DrawDust += DrawDust;
				On_Main.DrawGore += DrawGore;
				On_Main.DrawInterface += DrawInterface;
				On_Main.DrawPlayers_AfterProjectiles += DrawPlayers;
				_on = true; _lastLog = Stopwatch.GetTimestamp();
			}
			catch (Exception e) { Mod.Logger.Warn("[Prof] client profiler could not hook the game: " + e.Message); }
		}

		public override void Unload()
		{
			if (!_on) return;
			_on = false;
			try
			{
				On_Main.DoUpdate -= DoUpdate; On_Main.DoDraw -= DoDraw; On_Main.DrawNPCs -= DrawNpcs; On_Main.DrawProjectiles -= DrawProj; On_Main.DrawDust -= DrawDust;
				On_Main.DrawGore -= DrawGore; On_Main.DrawInterface -= DrawInterface; On_Main.DrawPlayers_AfterProjectiles -= DrawPlayers;
			}
			catch (Exception) { }
		}

		public override void PreUpdateEntities() { if (_on) _entStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateEverything() { if (_on) _ent += Stopwatch.GetTimestamp() - _entStart; }
		public override void PreUpdateNPCs() { if (_on) _npcStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateNPCs() { if (_on) _npc += Stopwatch.GetTimestamp() - _npcStart; }
		public override void PreUpdateProjectiles() { if (_on) _projStart = Stopwatch.GetTimestamp(); }
		public override void PostUpdateProjectiles() { if (_on) _proj += Stopwatch.GetTimestamp() - _projStart; }

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

		private static void Report()
		{
			long now = Stopwatch.GetTimestamp();
			double secs = (now - _lastLog) / (double)Stopwatch.Frequency;
			if (secs < 2.0) return;
			double f = 1000.0 / Stopwatch.Frequency, ups = _updates / secs, fps = _frames / secs;
			double upd = _updates > 0 ? _update * f / _updates : 0, fr = Math.Max(1, _frames);
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < 7; i++) sb.Append(i > 0 ? ", " : "").Append(DrawNames[i]).Append(' ').Append((Draw[i] * f / fr).ToString("0.00"));
			Levy.Instance?.Logger.Info($"[Prof] client: fps {fps:0.0} ups {ups:0.0} | update ms/tick {upd:0.00} (entities+world {_ent * f / Math.Max(1, _updates):0.00}: npcs {_npc * f / Math.Max(1, _updates):0.00}, projectiles {_proj * f / Math.Max(1, _updates):0.00}) | draw ms/frame: {sb} | levy bodies {SoldierBody * f / fr:0.00}, nameplates {SoldierPlate * f / fr:0.00} ({SoldiersDrawn / fr:0.0}/frame) | npcs {CountActiveNpcs()}, projectiles {CountActiveProjectiles()}");
			_update = _ent = _npc = _proj = 0; _updates = _frames = 0; Array.Clear(Draw, 0, Draw.Length); SoldierBody = SoldierPlate = 0; SoldiersDrawn = 0; _lastLog = now;
		}

		private static int CountActiveNpcs() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) n++; return n; }
		private static int CountActiveProjectiles() { int n = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) n++; return n; }
	}
}
