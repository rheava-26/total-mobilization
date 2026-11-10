using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Weapons;
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
		/// <summary>0 = the old gates (no dodge / strafe / jump decisions while airborne), 1 = air mobility (dodges, double jump, wing thrust, dash, fast fall, hook while airborne).</summary>
		internal static int AirMode = 1;
		/// <summary>0 = a levy climbs one row at most and only from the ground, 1 = chain-hops rows and picks the lane away from the boss.</summary>
		internal static int PlatMode = 1;
		internal int DodgeAirManeuvers, DodgeAirScans;
		internal static bool DodgeDebug = Environment.GetEnvironmentVariable("LEVY_DODGEDBG") == "1";
		private const int ThreatHorizon = 40, ThreatHorizonMobile = 56, ThreatScanInterval = 4, ThreatCooldown = 10, ThreatBudgetPerTick = 14;
		private int _dodgeHold; private int _dodgeBusy; private int _dodgePend, _dodgePendTiles;
		private static uint _thrBudgetTick; private static int _thrBudget;
		private static readonly List<Threat> _thrNear = new(32);
		private static bool _thrNearChain;
		internal static bool ChainLongHold = Environment.GetEnvironmentVariable("LEVY_CHAINHOLD") != "0";
		internal static bool ChainLongSteps = Environment.GetEnvironmentVariable("LEVY_CHAINSTEPS") != "0";
		/// <summary>Step lengths (ticks) tried; a worm's body is a wide moving lane, so a segmented threat adds longer steps that clear it.</summary>
		private static readonly int[] StepDurs = { 8, 14, 22 }, ChainStepDurs = { 8, 14, 22, 34, 48 };
		internal int DodgeManeuvers, DodgeStays, DodgeSteps, DodgeJumps, DodgeDashes, DodgeDrops, DodgeScansDone;
		internal long DodgeTs;
		internal int DodgeAirKicks, DodgeAirFalls;
		internal int DodgeDoubleJumps, DodgeDashJumps, DodgeHooks, DodgeNearScans, DodgeBaseHitScans, DodgeNoBetter, DodgeSkippedBusy;
		private uint _air2Ready;
		// airborne dodging (AirMode >= 1): the stamp every candidate gets, a kick queued for later, and a wing / rocket thrust under way
		private float _dGlideCap; private bool _dAir; private float _dV0x, _dV0y, _dFloorY, _dSolidFloorY; private uint _airKickAt; private int _airThrustTicks; private float _dodgeAcc;
		private const float AirThrustVy = 4.5f, AirFastFallVy = 7f; private const int AirThrustTicks = 14, AirScanInterval = 2, AirCooldown = 4;
		/// <summary>Harness: the dodge layer's internal timers (cooldown, hold, queued hop, walking sidestep ticks left).</summary>
		internal (int cooldown, int hold, int pend, int stepTicks) DodgeTimers => (_dodgeCooldown, _dodgeHold, _dodgePend, _dodgeTicks);

		private bool TickDodge(bool grounded, bool air)
		{
			if (DodgeMode == 0) return false;
			if (DodgeDebug && (_dodgePend > 0 || !grounded)) Levy.Instance?.Logger.Info($"DODGEDBG-TRK t={Main.GameUpdateCount} y={NPC.Bottom.Y:0} vy={NPC.velocity.Y:0.00} pend={_dodgePend}");
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { return DodgeMode == 1 ? TickDodgeLegacy(grounded, air) : TickThreatDodge(grounded, air); }
			finally { long e = System.Diagnostics.Stopwatch.GetTimestamp() - t0; DodgeTs += e; if (global::Levy.Common.Util.LevyProf.On) global::Levy.Common.Util.LevyProf.Add(global::Levy.Common.Util.LevyProf.Dodge, e); }
		}

		private bool TickThreatDodge(bool grounded, bool air)
		{
			NPC npc = NPC;
			if (_air2Ready != 0 && Main.GameUpdateCount >= _air2Ready)
			{
				if (grounded || Main.GameUpdateCount > _air2Ready + 10u || _jumpsUsed >= Abilities.ExtraJumps) _air2Ready = 0;
				else if (npc.velocity.Y > -1f) { TryJump(BaseJumpTiles); _air2Ready = 0; }
			}
			if (AirMode >= 1 && !grounded && !air)
			{
				if (_airKickAt != 0 && Main.GameUpdateCount >= _airKickAt) { _airKickAt = 0; if (npc.velocity.Y > -AirJumpImpulsePx * 0.8f && AirKick(AirJumpImpulsePx)) DodgeAirKicks++; }
				if (_airThrustTicks > 0) TickAirThrust();
			}
			else { _airKickAt = 0; _airThrustTicks = 0; }
			if (_dodgeTicks > 0)
			{
				_dodgeTicks--;
				if (_dodgeAcc > 0f && !grounded) npc.velocity.X += MathHelper.Clamp(_dodgeVx - npc.velocity.X, -_dodgeAcc, _dodgeAcc); else npc.velocity.X = _dodgeVx;
				return true;
			}
			if (_dodgePend > 0)
			{
				if (--_dodgePend == 0 && grounded && !npc.wet) { DodgeJumps++; TryJump(_dodgePendTiles); npc.netUpdate = true; _dodgeCooldown = ThreatCooldown; }
				else if (!grounded && !air) _dodgePend = 0;
				else { npc.velocity.X *= StopDecel; if (Math.Abs(npc.velocity.X) < StopBelowPx) npc.velocity.X = 0f; }
				if (_dodgePend > 0) WhyM = Why.MDodgePend;
				return _dodgePend > 0;
			}
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) return false;
			bool airScan = AirMode >= 1 && !grounded && !air && !_flying && !npc.wet;
			if ((!grounded && !air && !airScan) || _flying || npc.wet) { _dodgeHold = 0; DodgeSkippedBusy++; return false; }
			bool holding = _dodgeHold > 0;
			if (_dodgeCooldown > 0) { _dodgeCooldown--; return HoldStep(holding); }
			if ((int)((Main.GameUpdateCount + (uint)npc.whoAmI) % (uint)(airScan || (DanceMode >= 1 && DOn(2) && ThreatField.BossBodies > 0) ? AirScanInterval : ThreatScanInterval)) != 0) return HoldStep(holding);
			uint now = Main.GameUpdateCount;
			if (_thrBudgetTick != now) { _thrBudgetTick = now; _thrBudget = ThreatBudgetPerTick; }
			if (_thrBudget <= 0) return HoldStep(holding);
			_thrBudget--; DodgeScansDone++;

			var all = ThreatField.Snapshot();
			if (all.Count == 0) return HoldStep(holding);
			Vector2 c0 = npc.Center;
			int horizon = MobilityMode >= 1 ? ThreatHorizonMobile : ThreatHorizon;
			{ long pn = global::Levy.Common.Util.LevyProf.Begin(); ThreatField.Near(all, c0, 128f, horizon, _thrNear); global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.Near, pn); }
			if (_thrNear.Count == 0) return HoldStep(holding);
			DodgeNearScans++;

			float g = Gravity();
			_dAir = airScan;
			if (airScan) { DodgeAirScans++; _dV0x = npc.velocity.X; _dV0y = npc.velocity.Y; AirFloors(out _dFloorY, out _dSolidFloorY); }
			_thrNearChain = false; for (int ci = 0; ci < _thrNear.Count; ci++) if (_thrNear[ci].TrajN > 0) { _thrNearChain = true; break; }
			var stay = new Maneuver { Kind = 0 };
			if (airScan) { stay.Air = true; stay.V0x = _dV0x; stay.V0y = _dV0y; stay.FloorY = _dFloorY; _dGlideCap = PredictedGlideCap; stay.VyCap = _dGlideCap; }
			_wormScan = WormDodgeOn && ThreatField.HasHead(_thrNear);
			float sp = Math.Max(DodgeSidestepSpeedPx, Math.Min(Abilities.MaxRunSpeed, MobilityMode >= 1 ? 9f : 5.5f));
			int hopTiles = DodgeHopTiles + (_kitAny ? FlightDodgeHopBonusTiles : 0);
			float jv = -JumpSpeedFor(Math.Min(hopTiles, Abilities.JumpTiles));
			int goalSide = HasMoveGoal ? (MoveGoal.X >= c0.X ? 1 : -1) : 0;
			int baseHits = 0, firstHit = 0, lastHit = 0, bestHits = 0, bestLast = 0; float baseScore = 0f, bestScore = 0f; Maneuver best = stay;
			bool jerked = false;
			if (_wormScan && WormPickOn)
			{
				// the worm rule is hard: it picks the manoeuvre itself (against the head's travel, late, dash + jump) and nothing else gets a vote
				int wj = WormPick(grounded, airScan, c0, g, horizon, sp, goalSide, out Maneuver jm);
				if (wj < 0) return HoldStep(holding);
				if (wj == 1) return WormHoldStill();
				if (wj == 2) { best = jm; bestHits = 0; bestLast = 0; baseHits = 1; jerked = true; WormJerks++; }
			}
			// a worm head is judged twice: first with a wide margin (a dodge that leaves room), then, if nothing can leave that much, by the field's own margin
			for (int wpass = 0; wpass < (jerked ? 0 : _wormScan ? 2 : 1); wpass++)
			{
			_wormMargin = _wormScan && wpass == 0 ? LevyConstants.WormDodge.MarginPx : 0f;
			baseHits = ThreatField.Hits(_thrNear, c0, npc.width, npc.height, stay, g, horizon, out firstHit, out lastHit, _wormMargin);
			if (baseHits == 0) return _wormScan && wpass == 1 ? WormHoldStill() : HoldStep(holding);
			if (wpass == 0 || !_wormScan) DodgeBaseHitScans++;
			// a threat already past the point of no return cannot be dodged by a long walk, but the same search still picks the best partial answer
			baseScore = baseHits * 100f;
			best = stay; bestHits = baseHits; bestScore = baseScore;
			bestLast = lastHit;
			for (int dir = -1; dir <= 1; dir += 2)
			{
				foreach (int dur in _thrNearChain && ChainLongSteps ? ChainStepDurs : StepDurs)
				{
					float dist = sp * dur;
					if (!(grounded ? StepLaneSafe(dir, dist) : SidestepClearAir(dir))) break; // a longer step in the same direction is no safer
					Consider(new Maneuver { Kind = 1, Vx = dir * sp, Dur = dur, AirAcc = RunAccel, Penalty = dur * 0.15f + (goalSide != 0 && dir != goalSide ? 3f : 0f) }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				}
				if (grounded && StepLaneSafe(dir, sp * 14f))
					Consider(new Maneuver { Kind = 3, Vx = dir * sp, Dur = 14, JumpVy = jv, Penalty = 6f + (goalSide != 0 && dir != goalSide ? 3f : 0f) }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				if (grounded && WormBodyHigh && _thrNearChain && StepLaneSafe(dir, sp * 14f))
				{
					// a worm's body is a long lane: a hop that keeps its sideways run for the whole flight lands far from where it left, so the landing is not back on the trail
					foreach (int wd in LevyConstants.WormDodge.CarryDurs)
					{
						var cj = new Maneuver { Kind = 3, Vx = dir * sp, Dur = wd, JumpVy = jv, Penalty = 7f + wd * 0.05f + (goalSide != 0 && dir != goalSide ? 3f : 0f) };
						if (CarryClearsHead(cj, c0, g)) Consider(cj, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
						if (Abilities.JumpTiles > hopTiles)
						{
							var cf = new Maneuver { Kind = 14, Vx = dir * sp, Dur = wd, JumpVy = -JumpSpeedFor(Abilities.JumpTiles), Penalty = 8f + wd * 0.05f + (goalSide != 0 && dir != goalSide ? 3f : 0f) };
							if (CarryClearsHead(cf, c0, g)) Consider(cf, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
						}
					}
				}
				if (Abilities != null && Abilities.HasDash && _dashCooldown <= 0 && (grounded ? DashLaneClear(dir) : airScan && DashSpaceClear(dir)))
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
			if (airScan && bestHits > 0) ConsiderAir(c0, g, sp, goalSide, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (grounded && PlatformDropIsSafe()) Consider(new Maneuver { Kind = 5, Drop = true, Penalty = 7f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (MobilityMode >= 1 && grounded && bestHits > 0) ConsiderMobility(c0, g, sp, goalSide, ref best, ref bestHits, ref bestScore, ref bestLast);

			if (!_wormScan || bestHits == 0) break;
			}
			if (!jerked && _wormScan && best.Kind != 0 && bestHits == 0 && WormCanWait(best, c0, g, horizon, airScan)) return WormHoldStill();
			if (_wormScan && DodgeDebug) Levy.Instance?.Logger.Info($"WORMDBG t={Main.GameUpdateCount} {SoldierName} margin={_wormMargin} base={baseHits}@{firstHit} best=k{best.Kind}/d{best.Delay} hits={bestHits} grounded={grounded}");
			_dodgeCooldown = airScan ? AirCooldown : ThreatCooldown;
			if (DodgeDebug) Levy.Instance?.Logger.Info($"DODGEDBG t={Main.GameUpdateCount} {SoldierName} threats={_thrNear.Count} base={baseHits}@{firstHit}..{lastHit} best=kind{best.Kind} delay={best.Delay} hits={bestHits} vx={NPC.velocity.X:0.0} grounded={grounded}");
			if (best.Kind == 0 || bestHits >= baseHits) { DodgeStays++; DodgeNoBetter++; if (!grounded && air && AirBlip()) npc.netUpdate = true; return HoldStep(holding); }
			DodgeManeuvers++; if (airScan) DodgeAirManeuvers++;
			switch (best.Kind)
			{
				case 1: DodgeSteps++; Begin(best.Vx, best.Dur, airScan ? RunAccel : 0f); break;
				case 11:
					if (best.J2At <= 0) { if (AirKick(AirJumpImpulsePx)) DodgeAirKicks++; } else _airKickAt = Main.GameUpdateCount + (uint)best.J2At;
					if (best.Dur > 0) Begin(best.Vx, best.Dur, RunAccel);
					break;
				case 12:
					_airThrustTicks = Math.Max(1, best.HookDur); DodgeAirKicks++;
					if (best.Dur > 0) Begin(best.Vx, best.Dur, RunAccel);
					break;
				case 13: DodgeAirFalls++; _fallThroughTicks = 12; WantsToFallThrough = true; if (npc.velocity.Y < AirFastFallVy) npc.velocity.Y = AirFastFallVy; WingDodgeBegin(false); break;
				case 15: DodgeAirKicks += 0; WingDodgeBegin(true); break;
				case 2: if (best.Delay > 0) { _dodgePend = best.Delay; _dodgePendTiles = hopTiles; } else { DodgeJumps++; TryJump(hopTiles); } break;
				case 6: if (best.Delay > 0) { _dodgePend = best.Delay; _dodgePendTiles = Abilities.JumpTiles; } else { DodgeJumps++; TryJump(Abilities.JumpTiles); } break;
				case 3: DodgeJumps++; TryJump(hopTiles); Begin(best.Vx, best.Dur); break;
				case 14: DodgeJumps++; TryJump(Abilities.JumpTiles); Begin(best.Vx, best.Dur); break;
				case 4: DodgeDashes++; DashIFrames(); _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); Begin(best.Vx, best.Dur); break;
				case 5: DodgeDrops++; _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; break;
				case 7:
					DodgeDoubleJumps++; TryJump(Abilities.JumpTiles);
					_air2Ready = Main.GameUpdateCount + (uint)Math.Max(1, best.J2At);
					if (best.Dur > 0) Begin(best.Vx, best.Dur);
					break;
				case 8: DodgeDashJumps++; DodgeDashes++; DashIFrames(); _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); TryJump(Abilities.JumpTiles); Begin(best.Vx, best.Dur); break;
				case 10: DodgeDashes++; DodgeDrops++; DashIFrames(); _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1); _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; Begin(best.Vx, best.Dur); break;
				case 9:
					DodgeHooks++; HookHoldUntil = 0; if (WormBodyHigh && _thrNearChain) Levy.Instance?.Logger.Info("WORMHOOK " + SoldierName); if (airScan) DodgeAirManeuvers += 0;
					if (SoldierHooks.TakePlanBudget()) { try { HookStart(HookReason.Rescue, best.HookTarget + new Vector2(0f, NPC.height * 0.5f)); } catch (Exception) { try { HookReset(true); } catch (Exception) { } } }
					// HOLD: hang on the anchor while the danger lasts, but only when the anchor itself is a spot the threat field says nothing reaches (a hang is not the landing the dodge was judged on)
					if (HmHold && _hkPhase == HookPhase.InFlight && _hkPlan != null && _hkPlan.Anchors.Count > 0)
					{
						Vector2 anc = _hkPlan.Anchors[0]; bool calm = true;
						for (int ox = -1; ox <= 1 && calm; ox++)
							if (ThreatField.Hits(_thrNear, anc + new Vector2(ox * 24f, 20f), npc.width, npc.height, new Maneuver { Kind = 0 }, g, horizon, out _, out _, 0f) > 0) calm = false;
						if (calm) HookHoldUntil = Main.GameUpdateCount + (uint)Math.Clamp(bestLast + 14, 40, 150);
					}
					break;
			}
			// hold the safe spot until the danger that sent us here has gone by
			_dodgeHold = best.Kind == 1 || best.Kind == 10 ? Math.Clamp(bestLast + 4 - best.Dur, 0, _thrNearChain && ChainLongHold ? 90 : 30) : 0;
			npc.netUpdate = true;
			return best.Kind == 1 || best.Kind == 3 || best.Kind == 4 || best.Kind == 8 || best.Kind == 10 || (best.Kind == 7 && best.Dur > 0) || ((best.Kind == 11 || best.Kind == 12) && best.Dur > 0);
		}

		/// <summary>Moves a player has beyond a step and a hop: a double jump from the apex of the first, a dash combined with a jump or a platform drop, a hook up to a ledge out of the line.</summary>
		private static readonly List<Threat> _thrHeads = new(4);
		private static uint _thrHeadsTick = uint.MaxValue; private static int _thrHeadsWho = -1;
		/// <summary>A carry hop is dropped when, over its whole flight, it crosses a worm head's predicted arc (head margin included).</summary>
		private bool CarryClearsHead(in Maneuver m, Vector2 c0, float g)
		{
			if (!ThreatField.FastOn || _thrHeadsTick != Main.GameUpdateCount || _thrHeadsWho != NPC.whoAmI)
			{
				_thrHeadsTick = Main.GameUpdateCount; _thrHeadsWho = NPC.whoAmI;
				_thrHeads.Clear();
				for (int i = 0; i < _thrNear.Count; i++) if (_thrNear[i].Head) _thrHeads.Add(_thrNear[i]);
			}
			if (_thrHeads.Count == 0) return true;
			return ThreatField.Hits(_thrHeads, c0, NPC.width, NPC.height, m, g, m.Dur + 14, out _, out _, LevyConstants.WormDodge.MarginPx) == 0;
		}

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
			if (HookUsable && (!Abilities.HasWings || HmDodge) && _hkPhase == HookPhase.Idle && Main.GameUpdateCount >= _hkNextAllowed)
			{
				HookInfo info = HookInfo.Of(Record.Gear.Hook);
				Point feet = TileNav.FeetTile(npc.Bottom);
				int reach = (int)(info.ReachPx / 16f) - 2;
				foreach (int dx in HookLedgeCols)
				{
					int upLo = 5, upHi = Math.Min(reach, 18); bool hiFirst = WormBodyHigh && _thrNearChain;
					for (int upi = upLo; upi <= upHi; upi++)
					{
						int up = hiFirst ? upHi - (upi - upLo) : upi;
						int x = feet.X + dx, y = feet.Y - up;
						if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y) || TileNav.HasLava(x, y + 1)) continue;
						if (up > 6 && TileNav.IsStandable(x, y + 1)) continue;
						Vector2 tgtBottom = TileNav.BottomCentre(x, y);
						Vector2 tgt = tgtBottom - new Vector2(0f, npc.height * 0.5f);
						float dist = Vector2.Distance(c0, tgt);
						Consider(new Maneuver { Kind = 9, Hook = true, HookTarget = tgt, HookDur = (int)Math.Max(6f, dist / Math.Max(4f, info.PullPx)), Penalty = (HmDodge ? 7f : 12f) + up * 0.2f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
						break;
					}
				}
			}
		}

		// =====================================================================================================
		// AIRBORNE DODGING. A player in the air still dodges: steers, second-jumps, flaps or thrusts, dashes, drops, hooks to a ledge. The body already has a velocity, so every candidate is a path from it.
		// =====================================================================================================

		/// <summary>Centre Y the body comes to rest at if it falls straight down: onto the first floor (platforms included), and onto the first solid floor (platforms ignored). Lava counts as a floor. Far below when there is none.</summary>
		private void AirFloors(out float floorY, out float solidFloorY)
		{
			NPC npc = NPC;
			int col = (int)(npc.Center.X / 16f); int r0 = TileNav.FeetTile(npc.Bottom).Y;
			floorY = solidFloorY = npc.Center.Y + 60f * 16f;
			bool gotFloor = false;
			for (int r = r0 + 1; r <= r0 + 60; r++)
			{
				bool lava = TileNav.HasLava(col, r), solid = TileNav.IsSolid(col, r), plat = TileNav.IsPlatform(col, r);
				float y = r * 16f - npc.height * 0.5f;
				if (!gotFloor && (lava || solid || plat)) { floorY = y; gotFloor = true; }
				if (lava || solid) { solidFloorY = y; return; }
			}
		}

		/// <summary>Where an air manoeuvre comes down: not into lava and not into a void.</summary>
		private bool AirLandingSafe(Vector2 c0, in Maneuver m, float g)
		{
			if (m.Hook) return true;
			if (m.FloorY >= c0.Y + 55f * 16f && m.Kind != 0) { /* no floor in sight: judged by the column below */ }
			for (int t = 4; t <= 110; t += 2)
			{
				Vector2 b = ThreatField_BodyAt(c0, m, g, t);
				if (b.Y >= m.FloorY - 0.5f)
				{
					int col = (int)(b.X / 16f); int r0 = TileNav.FeetTile(NPC.Bottom).Y - 6;
					for (int r = r0; r < r0 + 64; r++)
					{
						if (TileNav.HasLava(col, r)) return false;
						if (TileNav.IsFloor(col, r) || TileNav.IsSolid(col, r)) return true;
					}
					return false;
				}
			}
			return true;
		}

		private bool AirKickAvailable()
		{
			if (_airJumpCooldown > 0) return false;
			if (_jumpsUsed < Abilities.ExtraJumps) return true;
			if (Abilities.HasWings && _wingTime > WingBudgetReserveTicks + FlapAssistCostTicks) return true;
			return _rocketTime > FlapAssistCostTicks;
		}

		private bool AirThrustAvailable() => (Abilities.HasWings && _wingTime > WingBudgetReserveTicks + 2 * AirThrustTicks) || _rocketTime > 2 * AirThrustTicks;

		/// <summary>Spends flight budget for a short climb: wings first, then rocket boots.</summary>
		private void TickAirThrust()
		{
			bool wing = Abilities.HasWings && _wingTime > WingBudgetReserveTicks;
			bool rocket = !wing && _rocketTime > 0;
			if (!wing && !rocket) { _airThrustTicks = 0; return; }
			_airThrustTicks--;
			NPC.velocity.Y = -AirThrustVy - Gravity();
			SpendFlight(wing, AerialClimbDrain);
			_aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = rocket;
			if (((uint)Main.GameUpdateCount + (uint)NPC.whoAmI) % (uint)AerialNetSyncTicks == 0) NPC.netUpdate = true;
		}

		/// <summary>The moves a body in the air has: a second jump or flap (now or timed), a wing / rocket climb, a fast fall through the platforms, a hook to a ledge. Steps and dashes are already in the main list.</summary>
		private void ConsiderAir(Vector2 c0, float g, float sp, int goalSide, ref Maneuver best, ref int bestHits, ref float bestScore, ref int bestLast)
		{
			NPC npc = NPC;
			bool kick = AirKickAvailable() && npc.velocity.Y > -AirJumpImpulsePx * 0.8f;
			bool thrust = AirThrustAvailable();
			float up = -(AirJumpImpulsePx + JumpSpeedOffset);
			int airDur = WormBodyHigh && _thrNearChain ? LevyConstants.WormDodge.CarryDurs[1] : 24;
			for (int dir = -1; dir <= 1; dir++)
			{
				if (dir != 0 && !SidestepClearAir(dir)) continue;
				float pen = dir != 0 && goalSide != 0 && dir != goalSide ? 3f : 0f;
				if (kick) foreach (int dl in new[] { 0, 6, 12 })
					Consider(new Maneuver { Kind = 11, Vx = dir * sp, Dur = dir == 0 ? 0 : airDur, AirAcc = RunAccel, J2Vy = up, J2At = dl, Penalty = 6f + dl * 0.1f + (dir != 0 ? 1f : 0f) + pen }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
				if (thrust) Consider(new Maneuver { Kind = 12, Vx = dir * sp, Dur = dir == 0 ? 0 : airDur, AirAcc = RunAccel, J2Vy = -AirThrustVy, J2At = 0, HookDur = AirThrustTicks, Penalty = 8f + (dir != 0 ? 1f : 0f) + pen }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			}
			// a held wing: the fall slows to the glide while the danger goes by underneath (costs flight time)
			if (WingCtlOn && Abilities.HasWings && _wingTime > WingBudgetReserveTicks + WingHoldTicks && _wingRelTicks <= 0 && !_flying) Consider(new Maneuver { Kind = 15, Penalty = 4f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			// dropping out of the line: a fast fall (and through any platform on the way) when a safe floor lies below
			if (_dSolidFloorY < c0.Y + 40f * 16f) Consider(new Maneuver { Kind = 13, J2Vy = AirFastFallVy, J2At = 0, Penalty = 5f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
			if (HookUsable && (!Abilities.HasWings || HmDodge) && _hkPhase == HookPhase.Idle && Main.GameUpdateCount >= _hkNextAllowed)
			{
				HookInfo info = HookInfo.Of(Record.Gear.Hook);
				Point feet = TileNav.FeetTile(npc.Bottom);
				int reach = (int)(info.ReachPx / 16f) - 2;
				foreach (int dx in HookLedgeCols)
				{
					int uLo = 2, uHi = Math.Min(reach, 14); bool hiFirst = WormBodyHigh && _thrNearChain;
					for (int ui = uLo; ui <= uHi; ui++)
					{
						int upT = hiFirst ? uHi - (ui - uLo) : ui;
						int x = feet.X + dx, y = feet.Y - upT;
						if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y) || TileNav.HasLava(x, y + 1)) continue;
						if (upT > 6 && TileNav.IsStandable(x, y + 1)) continue;
						Vector2 tgt = TileNav.BottomCentre(x, y) - new Vector2(0f, npc.height * 0.5f);
						float dist = Vector2.Distance(c0, tgt);
						Consider(new Maneuver { Kind = 9, Hook = true, HookTarget = tgt, HookDur = (int)Math.Max(6f, dist / Math.Max(4f, info.PullPx)), Penalty = (HmDodge ? 7f : 12f) + upT * 0.2f }, c0, g, ref best, ref bestHits, ref bestScore, ref bestLast);
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

		/// <summary>The dash burst is a window the body cannot be struck in (the contact and projectile gates read ImmuneTime).</summary>
		private void DashIFrames() { if (DodgeMode >= 2 && DanceMode >= 1 && DOn(4)) ImmuneTime = Math.Max(ImmuneTime, DashIFrameTicks); }

		private void Begin(float vx, int dur, float acc = 0f) { _dodgeVx = vx; _dodgeTicks = dur; _dodgeAcc = acc; if (acc <= 0f) NPC.velocity.X = vx; }

		/// <summary>While the danger the last dodge answered is still passing, stand still (decelerating) instead of walking back into it.</summary>
		private bool HoldStep(bool holding)
		{
			if (!holding || _dodgeHold <= 0) return false;
			_dodgeHold--; WhyM = Why.MDodgeHold;
			NPC.velocity.X *= StopDecel;
			if (Math.Abs(NPC.velocity.X) < StopBelowPx) NPC.velocity.X = 0f;
			return true;
		}

		private void Consider(Maneuver m, Vector2 c0, float g, ref Maneuver best, ref int bestHits, ref float bestScore, ref int bestLast)
		{
			if (_dAir && !m.Hook) { m.Air = true; m.V0x = _dV0x; m.V0y = _dV0y; m.FloorY = m.Kind == 13 ? _dSolidFloorY : _dFloorY; m.VyCap = m.Kind == 13 ? 0f : m.Kind == 15 ? GlideFallSpeed : _dGlideCap; }
			int imm = DanceMode >= 1 && DOn(4) && DashIFrameTicks > 0 && (m.Kind == 4 || m.Kind == 8 || m.Kind == 10) ? DashIFrameTicks : 0;
			int h = ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, m, g, MobilityMode >= 1 ? ThreatHorizonMobile : ThreatHorizon, out _, out int last, _wormMargin, imm);
			if (MobilityMode >= 1 && h == 0 && m.Kind != 0 && !(m.Air ? AirLandingSafe(c0, m, g) : LandingSafe(c0, m, g))) return;
			float score = h * 100f + m.Penalty + (_wormScan && m.Kind != 0 ? WormAlongPenalty(m, c0, g) : 0f);
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

		// ---- platform rows in a boss fight ----
		private bool _platActive; private Vector2 _platGoal; private int _platNext, _platStart;
		internal int PlatformClimbs, PlatEvals, PlatNoCand, PlatSepOk;

		/// <summary>
		/// A player fights a boss from the platform rows: up out of the dash lane, down through them to dodge. Standing on plain ground with a platform in jump (or hook, or double-jump) reach, a levy in a boss fight walks / jumps / hooks up onto it,
		/// at most once every few seconds; once up it strafes there. True while the climb is under way (the goal is set).
		/// </summary>
		private bool TickPlatformPerch(int now) { long pt = global::Levy.Common.Util.LevyProf.Begin(); try { return TickPlatformPerchBody(now); } finally { global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.Perch, pt); } }
		private bool TickPlatformPerchBody(int now)
		{
			if (_platActive)
			{
				bool arrived = Math.Abs(NPC.Bottom.Y - _platGoal.Y) < 20f && Math.Abs(NPC.Bottom.X - _platGoal.X) < 40f;
				if (arrived || now - _platStart > 300)
				{
					_platActive = false; if (PlatMode >= 1) _platNext = Math.Max(_platNext, now + PlatformLaneDwellTicks);
					if (RowsMode >= 1)
					{
						if (arrived) { RowArriveS++; _rowHoldUntil = now + RowHoldTicks * 2; _rowNext = Math.Max(_rowNext, now + RowEvalTicks); }
						else { MarkBadPerch(TileNav.FeetTile(_platGoal), now); _rowHoldUntil = 0; _rowNext = now + 20; }
					}
					return false;
				}
				SetGoal(_platGoal);
				return true;
			}
			if (PlatMode >= 1 && RowsMode >= 1 && !PositionsAsMelee) { RowThreatPoke(now); return TickRowSpread(now); }
			if (PlatMode >= 1) return TickPlatformLane(now);
			if (now < _platNext || NPC.velocity.Y != 0f) return false;
			_platNext = now + 200 + (int)(SlotFrac(SlotSaltPhase) * 200f);
			Point feet = TileNav.FeetTile(NPC.Bottom);
			if (TileNav.IsPlatform(feet.X, feet.Y + 1)) return false;
			int maxUp = Abilities.JumpTiles + (Abilities.ExtraJumps > 0 ? 3 : 0);
			if (HookUsable) maxUp = Math.Max(maxUp, 12);
			float bestScore = float.MaxValue; Point best = default;
			for (int dx = -8; dx <= 8; dx++)
				for (int up = 3; up <= maxUp; up++)
				{
					int x = feet.X + dx, y = feet.Y - up;
					if (!TileNav.InBounds(x, y) || !TileNav.IsPlatform(x, y + 1) || !TileNav.IsBodyClear(x, y)) continue;
					float sc = Math.Abs(dx) + up;
					if (sc < bestScore) { bestScore = sc; best = new Point(x, y); }
				}
			if (bestScore == float.MaxValue) return false;
			_platGoal = TileNav.BottomCentre(best.X, best.Y); _platActive = true; _platStart = now; PlatformClimbs++;
			SetGoal(_platGoal);
			return true;
		}

		/// <summary>
		/// A player picks the lane the boss is not in: up on the rows when it dashes along the ground, down on the floor when it sweeps the rows. Judged by tiles of height between the body and the boss; a lane that gains
		/// <see cref="PlatformLaneNeedGain"/> tiles of separation is worth the walk. Rows are reached by what the gear allows (a jump, a second jump, wings, a hook), a lower lane by dropping through. Chains row to row on later looks.
		/// </summary>
		private float _platLow;
		/// <summary>Tiles from row y down to the first solid (not platform) tile in column x.</summary>
		private static int GroundAlt(int x, int y)
		{
			for (int r = y + 1; r <= y + 40; r++) if (TileNav.IsSolid(x, r)) return r - y - 1;
			return 40;
		}

		private bool TickPlatformLane(int now)
		{
			if (now < _platNext || NPC.velocity.Y != 0f) return false;
			PlatEvals++;
			_platNext = now + PlatformLaneEvalTicks + (int)(SlotFrac(SlotSaltPhase) * 60f);
			if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) return false;
			NPC bo = Main.npc[TargetIndex];
			if (!bo.active) return false;
			NPC seg = Aiming.NearestSegment(bo, NPC.Center) ?? bo;
			Point feet = TileNav.FeetTile(NPC.Bottom);
			float bossRow = seg.Center.Y / 16f;
			// is the boss working the floor? (smoothed: a hopper is on the ground half the time)
			int bossAlt = GroundAlt((int)(seg.Center.X / 16f), (int)(seg.Bottom.Y / 16f) - 1);
			_platLow = _platLow * 0.5f + (bossAlt <= PlatformLaneLowTiles ? 0.5f : 0f);
			bool groundLane = _platLow > 0.4f;
			int curAlt = GroundAlt(feet.X, feet.Y);
			float curSep = Math.Min(Math.Abs(bossRow - (feet.Y - 1f)), PlatformLaneSepCap);
			if (groundLane ? curAlt >= PlatformLaneRowTiles : curSep >= PlatformLaneSepCap - 1) { PlatSepOk++; return false; }
			int maxUp = Abilities.JumpTiles + (Abilities.ExtraJumps > 0 ? ExtraJumpReachTiles * Math.Min(2, Abilities.ExtraJumps) : 0);
			if (Abilities.HasWings) maxUp = Math.Max(maxUp, 16);
			if (HookUsable) maxUp = Math.Max(maxUp, 12);
			bool onPlatform = TileNav.IsPlatform(feet.X, feet.Y + 1);
			float bestScore = float.MinValue; Point best = default;
			for (int dx = -PlatformLaneColumns; dx <= PlatformLaneColumns; dx++)
			{
				int x = feet.X + dx;
				if (Math.Abs(seg.Center.X / 16f - x) > 34f) continue;
				for (int y = feet.Y - maxUp; y <= feet.Y + PlatformLaneDrop; y++)
				{
					int dy = y - feet.Y;
					if (Math.Abs(dy) < 3) continue;
					if (dy > 0 && !(onPlatform && Math.Abs(dx) <= 6)) continue;      // a lower lane is reached by dropping through where it stands
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					if (dy > 0 && TileNav.IsStandable(x, y - 1)) continue;
					float sc;
					if (groundLane)
					{
						int alt = GroundAlt(x, y);
						if (alt < PlatformLaneRowTiles || alt <= curAlt + PlatformLaneNeedGain - 1) continue;   // up onto a row at least a few tiles above where I stand
						sc = Math.Min(alt, 10) * 0.6f - Math.Abs(dx) * 0.25f - Math.Abs(dy) * 0.1f;
					}
					else
					{
						float sep = Math.Min(Math.Abs(bossRow - (y - 1f)), PlatformLaneSepCap);
						if (sep < curSep + PlatformLaneNeedGain) continue;
						sc = sep * 2f - Math.Abs(dx) * 0.25f - Math.Abs(dy) * 0.15f;
					}
					if (sc > bestScore) { bestScore = sc; best = new Point(x, y); }
				}
			}
			if (bestScore == float.MinValue) { PlatNoCand++; return false; }
			_platGoal = TileNav.BottomCentre(best.X, best.Y); _platActive = true; _platStart = now; PlatformClimbs++;
			SetGoal(_platGoal);
			return true;
		}
	}
}
