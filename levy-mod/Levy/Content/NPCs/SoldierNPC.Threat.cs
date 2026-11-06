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
		/// <summary>0 = the dodge moves of the threat field alone, 1 = plus the mobility moves (double jump, hook to a ledge, diagonal dash). Harness flips it for before/after.</summary>
		internal static int MobilityMode = 1;
		internal static bool DodgeDebug = Environment.GetEnvironmentVariable("LEVY_DODGEDBG") == "1";
		private const int ThreatHorizon = 40, ThreatHorizonMobile = 56, ThreatScanInterval = 4, ThreatCooldown = 10, ThreatBudgetPerTick = 14;
		private int _dodgeHold; private int _dodgeBusy; private int _dodgePend, _dodgePendTiles;
		private static uint _thrBudgetTick; private static int _thrBudget;
		private static readonly List<Threat> _thrNear = new(32);
		internal int DodgeManeuvers, DodgeStays, DodgeSteps, DodgeJumps, DodgeDashes, DodgeDrops, DodgeScansDone;
		internal long DodgeTs;
		internal int DodgeDoubleJumps, DodgeDashJumps, DodgeHooks, DodgeNearScans, DodgeBaseHitScans, DodgeNoBetter, DodgeSkippedBusy;
		private uint _air2Ready;
		private bool _wideNow;
		private const float ClearanceWeight = 10f, ClearanceHysteresis = 4f, ClearanceMarginPx = 28f;
		/// <summary>Harness: the dodge layer's internal timers (cooldown, hold, queued hop, walking sidestep ticks left).</summary>
		internal (int cooldown, int hold, int pend, int stepTicks) DodgeTimers => (_dodgeCooldown, _dodgeHold, _dodgePend, _dodgeTicks);

		private bool TickDodge(bool grounded, bool air)
		{
			if (DodgeMode == 0) return false;
			if (DodgeDebug && (_dodgePend > 0 || !grounded)) Levy.Instance?.Logger.Info($"DODGEDBG-TRK t={Main.GameUpdateCount} y={NPC.Bottom.Y:0} vy={NPC.velocity.Y:0.00} pend={_dodgePend}");
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return DodgeMode == 1 ? TickDodgeLegacy(grounded, air) : TickThreatDodge(grounded, air); }
			finally { DodgeTs += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
		}

		private bool TickThreatDodge(bool grounded, bool air)
		{
			NPC npc = NPC;
			if (_air2Ready != 0 && Main.GameUpdateCount >= _air2Ready)
			{
				if (grounded || Main.GameUpdateCount > _air2Ready + 10u || _jumpsUsed >= Abilities.ExtraJumps) _air2Ready = 0;
				else if (npc.velocity.Y > -1f) { TryJump(BaseJumpTiles); _air2Ready = 0; }
			}
			if (_dodgeTicks > 0) { _dodgeTicks--; npc.velocity.X = _dodgeVx; return true; }
			if (_dodgePend > 0)
			{
				if (--_dodgePend == 0 && grounded && !npc.wet) { DodgeJumps++; TryJump(_dodgePendTiles); npc.netUpdate = true; _dodgeCooldown = ThreatCooldown; }
				else if (!grounded && !air) _dodgePend = 0;
				else { npc.velocity.X *= StopDecel; if (Math.Abs(npc.velocity.X) < StopBelowPx) npc.velocity.X = 0f; }
				return _dodgePend > 0;
			}
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) return false;
			if ((!grounded && !air) || _flying || npc.wet) { _dodgeHold = 0; DodgeSkippedBusy++; return false; }
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
			int horizon = MobilityMode >= 1 ? ThreatHorizonMobile : ThreatHorizon;
			ThreatField.Near(all, c0, 128f, horizon, _thrNear);
			if (_thrNear.Count == 0) return HoldStep(holding);
			DodgeNearScans++;

			float g = Gravity();
			var stay = new Maneuver { Kind = 0 };
			int baseHits = ThreatField.Hits(_thrNear, c0, npc.width, npc.height, stay, g, horizon, out int firstHit, out int lastHit);
			if (baseHits == 0) return HoldStep(holding);
			DodgeBaseHitScans++;
			// a threat already past the point of no return cannot be dodged by a long walk, but the same search still picks the best partial answer
			// when no move avoids every overlap, the move that keeps the widest berth from the threats still beats standing in their path (a player backs off a boss it cannot fully dodge)
			float baseScore = baseHits * 100f;
			if (MobilityMode >= 1)
			{
				_wideNow = true;
				baseScore += ClearanceWeight * ThreatField.Hits(_thrNear, c0, npc.width, npc.height, stay, g, horizon, out _, out _, ClearanceMarginPx);
			}
			else _wideNow = false;
			Maneuver best = stay; int bestHits = baseHits; float bestScore = baseScore;
			int bestLast = lastHit;
			float sp = Math.Max(DodgeSidestepSpeedPx, Math.Min(Abilities.MaxRunSpeed, MobilityMode >= 1 ? 9f : 5.5f));
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
			if (grounded)
			{
				// a hop timed to start later (a charge still building speed arrives after a hop made now would already be over)
				foreach (int dl in new[] { 0, 6, 12, 18, 24 })
					Consider(new Maneuver { Kind = 2, JumpVy = jv, Delay = dl, Penalty = 5f + dl * 0.1f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				if (Abilities.JumpTiles > hopTiles)
					foreach (int dl in new[] { 0, 6, 12, 18, 24 })
						Consider(new Maneuver { Kind = 6, JumpVy = -JumpSpeedFor(Abilities.JumpTiles), Delay = dl, Penalty = 7f + dl * 0.1f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			}
			if (grounded && PlatformDropIsSafe()) Consider(new Maneuver { Kind = 5, Drop = true, Penalty = 7f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (MobilityMode >= 1 && grounded && bestHits > 0) ConsiderMobility(c0, g, sp, goalSide, ref best, ref bestHits, ref bestScore, ref bestLast);

			_dodgeCooldown = ThreatCooldown;
			if (DodgeDebug) Levy.Instance?.Logger.Info($"DODGEDBG t={Main.GameUpdateCount} {SoldierName} threats={_thrNear.Count} base={baseHits}@{firstHit}..{lastHit} best=kind{best.Kind} delay={best.Delay} hits={bestHits} vx={NPC.velocity.X:0.0} grounded={grounded}");
			if (best.Kind == 0 || bestHits > baseHits || (bestHits == baseHits && !(_wideNow && bestScore + ClearanceHysteresis < baseScore))) { DodgeStays++; DodgeNoBetter++; if (!grounded && AirBlip()) npc.netUpdate = true; return HoldStep(holding); }
			DodgeManeuvers++;
			switch (best.Kind)
			{
				case 1: DodgeSteps++; Begin(best.Vx, best.Dur); break;
				case 2: if (best.Delay > 0) { _dodgePend = best.Delay; _dodgePendTiles = hopTiles; } else { DodgeJumps++; TryJump(hopTiles); } break;
				case 6: if (best.Delay > 0) { _dodgePend = best.Delay; _dodgePendTiles = Abilities.JumpTiles; } else { DodgeJumps++; TryJump(Abilities.JumpTiles); } break;
				case 3: DodgeJumps++; TryJump(hopTiles); Begin(best.Vx, best.Dur); break;
				case 4: DodgeDashes++; _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); Begin(best.Vx, best.Dur); break;
				case 5: DodgeDrops++; _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; break;
				case 7:
					DodgeDoubleJumps++; TryJump(Abilities.JumpTiles);
					_air2Ready = Main.GameUpdateCount + (uint)Math.Max(1, best.J2At);
					if (best.Dur > 0) Begin(best.Vx, best.Dur);
					break;
				case 8: DodgeDashJumps++; DodgeDashes++; _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); TryJump(Abilities.JumpTiles); Begin(best.Vx, best.Dur); break;
				case 10: DodgeDashes++; DodgeDrops++; _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; Begin(best.Vx, best.Dur); break;
				case 9:
					DodgeHooks++;
					if (SoldierHooks.TakePlanBudget()) { try { HookStart(HookReason.Rescue, best.HookTarget + new Vector2(0f, NPC.height * 0.5f)); } catch (Exception) { try { HookReset(true); } catch (Exception) { } } }
					break;
			}
			// hold the safe spot until the danger that sent us here has gone by
			_dodgeHold = best.Kind == 1 || best.Kind == 10 ? Math.Clamp(bestLast + 4 - best.Dur, 0, 30) : 0;
			npc.netUpdate = true;
			return best.Kind == 1 || best.Kind == 3 || best.Kind == 4 || best.Kind == 8 || best.Kind == 10 || (best.Kind == 7 && best.Dur > 0);
		}

		/// <summary>Moves a player has beyond a step and a hop: a double jump from the apex of the first, a dash combined with a jump or a platform drop, a hook up to a ledge out of the line.</summary>
		private void ConsiderMobility(Vector2 c0, float g, float sp, int goalSide, ref Maneuver best, ref int bestHits, ref float bestScore, ref int bestLast)
		{
			NPC npc = NPC;
			int fullTiles = Abilities.JumpTiles;
			float jvFull = -JumpSpeedFor(fullTiles);
			int ej = Abilities.ExtraJumps - _jumpsUsed;
			if (ej > 0)
			{
				float j2 = -JumpSpeedFor(BaseJumpTiles) * ExtraJumpSpeedMult;
				int apex = Math.Max(2, (int)(-jvFull / g * 0.9f));
				for (int dir = -1; dir <= 1; dir++)
				{
					if (dir != 0 && !StepLaneSafe(dir, sp * 24f)) continue;
					Consider(new Maneuver { Kind = 7, Vx = dir * sp, Dur = dir == 0 ? 0 : 36, JumpVy = jvFull, J2Vy = j2, J2At = apex, Penalty = 9f + (goalSide != 0 && dir != 0 && dir != goalSide ? 3f : 0f) }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				}
			}
			if (Abilities.HasDash && _dashCooldown <= 0)
			{
				bool drop = PlatformDropIsSafe();
				for (int dir = -1; dir <= 1; dir += 2)
				{
					if (!DashSpaceClear(dir)) continue;
					Consider(new Maneuver { Kind = 8, Vx = dir * DashSpeedPx, Dur = DashBurstTicks, JumpVy = jvFull, Penalty = 10f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
					if (drop) Consider(new Maneuver { Kind = 10, Vx = dir * DashSpeedPx, Dur = DashBurstTicks, Drop = true, Penalty = 10f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				}
			}
			if (HookUsable && !Abilities.HasWings && _hkPhase == HookPhase.Idle && Main.GameUpdateCount >= _hkNextAllowed)
			{
				HookInfo info = HookInfo.Of(Record.Gear.Hook);
				Point feet = TileNav.FeetTile(npc.Bottom);
				int reach = (int)(info.ReachPx / 16f) - 2;
				foreach (int dx in HookLedgeCols)
				{
					for (int up = 5; up <= Math.Min(reach, 18); up++)
					{
						int x = feet.X + dx, y = feet.Y - up;
						if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y) || TileNav.HasLava(x, y + 1)) continue;
						if (up > 6 && TileNav.IsStandable(x, y + 1)) continue;
						Vector2 tgtBottom = TileNav.BottomCentre(x, y);
						Vector2 tgt = tgtBottom - new Vector2(0f, npc.height * 0.5f);
						float dist = Vector2.Distance(c0, tgt);
						Consider(new Maneuver { Kind = 9, Hook = true, HookTarget = tgt, HookDur = (int)Math.Max(6f, dist / Math.Max(4f, info.PullPx)), Penalty = 12f + up * 0.2f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
						break;
					}
				}
			}
		}

		private static readonly int[] HookLedgeCols = { 0, -6, 6, -12, 12 };

		/// <summary>Open space for a dash and the jump on top of it: no wall in the next columns at head height and no lava underfoot ahead.</summary>
		private bool DashSpaceClear(int dir)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			for (int i = 1; i <= 5; i++)
			{
				int x = feet.X + dir * i;
				if (TileNav.IsSolid(x, feet.Y) || TileNav.IsSolid(x, feet.Y - 1) || TileNav.IsSolid(x, feet.Y - 2)) return false;
			}
			return true;
		}

		/// <summary>Where a jumping manoeuvre comes down: ground that is not lava, a void or a deep pit. Walking manoeuvres are covered by the lane check.</summary>
		private bool LandingSafe(Vector2 c0, in Maneuver m, float g)
		{
			if (m.Hook) return true;
			if (m.JumpVy >= 0f && !m.Drop) return true;
			if (m.JumpVy >= 0f) return true;
			float feetY = c0.Y;
			for (int t = 6; t <= 110; t += 2)
			{
				Vector2 b = ThreatField_BodyAt(c0, m, g, t);
				if (b.Y >= feetY - 1f)
				{
					int col = (int)(b.X / 16f); Point feet = TileNav.FeetTile(NPC.Bottom);
					if (TileNav.HasLava(col, feet.Y) || TileNav.HasLava(col, feet.Y + 1) || TileNav.HasLava(col, feet.Y - 1)) return false;
					int depth = TileNav.DropDepth(col, feet.Y);
					return depth >= 0 && depth <= Abilities.JumpTiles + DropMarginTiles;
				}
			}
			return true;
		}

		private static Vector2 ThreatField_BodyAt(Vector2 c0, in Maneuver m, float g, int t) => ThreatField.PathAt(c0, m, g, t);

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
			int h = ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, m, g, MobilityMode >= 1 ? ThreatHorizonMobile : ThreatHorizon, out _, out int last);
			if (MobilityMode >= 1 && h == 0 && m.Kind != 0 && !LandingSafe(c0, m, g)) return;
			float score = h * 100f + m.Penalty;
			if (_wideNow && score < bestScore) score += ClearanceWeight * ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, m, g, MobilityMode >= 1 ? ThreatHorizonMobile : ThreatHorizon, out _, out _, ClearanceMarginPx);
			if (score < bestScore) { best = m; bestHits = h; bestScore = score; bestLast = last; }
		}

		/// <summary>Room to run <paramref name="distPx"/> to one side: no wall, no lava, a floor under every column (and, on a platform, only the same level).</summary>
		private bool StepLaneSafe(int side, float distPx)
		{
			int cols = Math.Max(3, (int)Math.Ceiling(distPx / 16f) + 1);
			return SurfaceLane(side, cols, false);
		}

		/// <summary>
		/// A lane that follows the ground the way a walking body does: each column may rise one tile (a step or a slope) or fall up to three, and must have clear body cells over a floor that is not lava.
		/// A wall taller than a step, lava, or a drop deeper than three tiles (a platform edge, a pit) refuses the lane. Flat ground is the special case where every column keeps the same row.
		/// </summary>
		private bool SurfaceLane(int side, int cols, bool deepWaterRefuses)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int row = feet.Y;
			int ax = (int)((NPC.Center.X + side * (NPC.width / 2f + LookAheadPx)) / 16f);
			for (int i = 0; i < cols; i++)
			{
				int x = ax + side * i; int found = int.MinValue;
				for (int r = row - 1; r <= row + 3; r++)
				{
					if (!TileNav.IsBodyClear(x, r) || !TileNav.IsFloor(x, r + 1) || TileNav.HasLava(x, r + 1)) continue;
					if (r > row && (TileNav.IsSolid(x, row) || TileNav.IsSolid(x, row - 1) || TileNav.IsSolid(x, row - 2))) continue;
					if (deepWaterRefuses && TileNav.IsDeepWater(x, r - 1)) continue;
					found = r; break;
				}
				if (found == int.MinValue) return false;
				row = found;
			}
			return true;
		}
	}
}
