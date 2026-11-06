using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THREAT-AWARE DODGING. A soldier reads the danger the way a player does: every hostile projectile, charging body and sweeping beam is a predicted path (<see cref="ThreatField"/>), every move it
	/// has (stay, step, jump, step and jump, dash, drop through a platform) is a path of its own, and it takes the move with the fewest predicted overlaps, then holds the safe spot until the danger has passed.
	/// Real movement only: no dodge chance, no damage reduction. The snapshot of threats is built once a tick for the whole army and each soldier only looks at what can reach it; scans are staggered by body index.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>0 = no dodging, 1 = the old closest-approach flinch, 2 = threat-field dodging (default). The harness flips it to compare.</summary>
		internal static int DodgeMode = 2;
		private const int ThreatHorizon = 40, ThreatScanInterval = 4, ThreatCooldown = 10, ThreatBudgetPerTick = 14;
		private int _dodgeHold; private int _dodgeBusy;
		private static uint _thrBudgetTick; private static int _thrBudget;
		private static readonly List<Threat> _thrNear = new(32);
		internal int DodgeManeuvers, DodgeStays, DodgeSteps, DodgeJumps, DodgeDashes, DodgeDrops, DodgeScansDone;
		internal long DodgeTs;

		private bool TickDodge(bool grounded, bool air)
		{
			if (DodgeMode == 0) return false;
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return DodgeMode == 1 ? TickDodgeLegacy(grounded, air) : TickThreatDodge(grounded, air); }
			finally { DodgeTs += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
		}

		private bool TickThreatDodge(bool grounded, bool air)
		{
			NPC npc = NPC;
			if (_dodgeTicks > 0) { _dodgeTicks--; npc.velocity.X = _dodgeVx; return true; }
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) return false;
			if ((!grounded && !air) || _flying || npc.wet) { _dodgeHold = 0; return false; }
			bool holding = _dodgeHold > 0;
			if (_dodgeCooldown > 0) { _dodgeCooldown--; return HoldStep(holding); }
			if ((int)((Main.GameUpdateCount + (uint)npc.whoAmI) % (uint)ThreatScanInterval) != 0) return HoldStep(holding);
			uint now = Main.GameUpdateCount;
			if (_thrBudgetTick != now) { _thrBudgetTick = now; _thrBudget = ThreatBudgetPerTick; }
			if (_thrBudget <= 0) return HoldStep(holding);
			_thrBudget--; DodgeScansDone++;

			var all = ThreatField.Snapshot();
			if (all.Count == 0) return HoldStep(holding);
			Vector2 c0 = npc.Center;
			ThreatField.Near(all, c0, 128f, ThreatHorizon, _thrNear);
			if (_thrNear.Count == 0) return HoldStep(holding);

			float g = Gravity();
			var stay = new Maneuver { Kind = 0 };
			int baseHits = ThreatField.Hits(_thrNear, c0, npc.width, npc.height, stay, g, ThreatHorizon, out int firstHit, out int lastHit);
			if (baseHits == 0) return HoldStep(holding);
			// a threat already past the point of no return cannot be dodged by a long walk, but the same search still picks the best partial answer
			Maneuver best = stay; int bestHits = baseHits; float bestScore = baseHits * 100f;
			int bestLast = lastHit;
			float sp = Math.Max(DodgeSidestepSpeedPx, Math.Min(Abilities.MaxRunSpeed, 5.5f));
			int hopTiles = DodgeHopTiles + (_kitAny ? FlightDodgeHopBonusTiles : 0);
			float jv = -JumpSpeedFor(Math.Min(hopTiles, Abilities.JumpTiles));
			int goalSide = HasMoveGoal ? (MoveGoal.X >= c0.X ? 1 : -1) : 0;
			for (int dir = -1; dir <= 1; dir += 2)
			{
				foreach (int dur in new[] { 8, 14, 22 })
				{
					float dist = sp * dur;
					if (!(grounded ? StepLaneSafe(dir, dist) : SidestepClearAir(dir))) break; // a longer step in the same direction is no safer
					Consider(new Maneuver { Kind = 1, Vx = dir * sp, Dur = dur, Penalty = dur * 0.15f + (goalSide != 0 && dir != goalSide ? 3f : 0f) }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				}
				if (grounded && StepLaneSafe(dir, sp * 14f))
					Consider(new Maneuver { Kind = 3, Vx = dir * sp, Dur = 14, JumpVy = jv, Penalty = 6f + (goalSide != 0 && dir != goalSide ? 3f : 0f) }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				if (grounded && Abilities != null && Abilities.HasDash && _dashCooldown <= 0 && DashLaneClear(dir))
					Consider(new Maneuver { Kind = 4, Vx = dir * DashSpeedPx, Dur = DashBurstTicks, Penalty = 8f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			}
			if (grounded) Consider(new Maneuver { Kind = 2, JumpVy = jv, Penalty = 5f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (grounded && Abilities.JumpTiles > hopTiles) Consider(new Maneuver { Kind = 6, JumpVy = -JumpSpeedFor(Abilities.JumpTiles), Penalty = 7f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (grounded && PlatformDropIsSafe()) Consider(new Maneuver { Kind = 5, Drop = true, Penalty = 7f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);

			_dodgeCooldown = ThreatCooldown;
			if (best.Kind == 0 || bestHits >= baseHits) { DodgeStays++; if (!grounded && AirBlip()) npc.netUpdate = true; return HoldStep(holding); }
			DodgeManeuvers++;
			switch (best.Kind)
			{
				case 1: DodgeSteps++; Begin(best.Vx, best.Dur); break;
				case 2: DodgeJumps++; TryJump(hopTiles); break;
				case 6: DodgeJumps++; TryJump(Abilities.JumpTiles); break;
				case 3: DodgeJumps++; TryJump(hopTiles); Begin(best.Vx, best.Dur); break;
				case 4: DodgeDashes++; _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); Begin(best.Vx, best.Dur); break;
				case 5: DodgeDrops++; _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; break;
			}
			// hold the safe spot until the danger that sent us here has gone by
			_dodgeHold = best.Kind == 1 ? Math.Clamp(bestLast + 4 - best.Dur, 0, 30) : 0;
			npc.netUpdate = true;
			return best.Kind == 1 || best.Kind == 3 || best.Kind == 4;
		}

		private void Begin(float vx, int dur) { _dodgeVx = vx; _dodgeTicks = dur; NPC.velocity.X = vx; }

		/// <summary>While the danger the last dodge answered is still passing, stand still (decelerating) instead of walking back into it.</summary>
		private bool HoldStep(bool holding)
		{
			if (!holding || _dodgeHold <= 0) return false;
			_dodgeHold--;
			NPC.velocity.X *= StopDecel;
			if (Math.Abs(NPC.velocity.X) < StopBelowPx) NPC.velocity.X = 0f;
			return true;
		}

		private void Consider(Maneuver m, Vector2 c0, float g, ref Maneuver best, ref int bestHits, ref float bestScore, ref int bestLast)
		{
			int h = ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, m, g, ThreatHorizon, out _, out int last);
			float score = h * 100f + m.Penalty;
			if (score < bestScore) { best = m; bestHits = h; bestScore = score; bestLast = last; }
		}

		/// <summary>Room to run <paramref name="distPx"/> to one side: no wall, no lava, a floor under every column (and, on a platform, only the same level).</summary>
		private bool StepLaneSafe(int side, float distPx)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int ax = (int)((NPC.Center.X + side * (NPC.width / 2f + LookAheadPx)) / 16f);
			int cols = Math.Max(3, (int)Math.Ceiling(distPx / 16f) + 1);
			for (int i = 0; i < cols; i++)
			{
				int x = ax + side * i;
				if (TileNav.IsSolid(x, feet.Y) || TileNav.IsSolid(x, feet.Y - 1) || TileNav.IsSolid(x, feet.Y - 2)) return false;
				if (!TileNav.IsFloor(x, feet.Y + 1) || TileNav.HasLava(x, feet.Y) || TileNav.HasLava(x, feet.Y + 1)) return false;
			}
			return true;
		}
	}
}
