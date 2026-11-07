using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// WORM DODGE. A worm head homes on its target at about twice a levy's running speed, and a path predicted more than ~15 ticks ahead is wrong by a body width. So against a head the levy does what a player does:
	/// it holds still and lets the head commit to the line (and to its turn), then makes ONE late, decisive move across the head's path, at the last moment that still clears (WormCanWait), never along the head's
	/// heading and never in the head's horizontal direction of travel (WormAlongPenalty: against the turn, the body ends behind the head and the head needs a long turn to come back).
	/// Generic: the trigger is "a chain head the threat field tracks is in the near list", never a name. LEVY_WORMDODGE=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static bool WormDodgeOn = Environment.GetEnvironmentVariable("LEVY_WORMDODGE") != "0";
		/// <summary>LEVY_WORMPICK=0: the late-deferred search instead of the hard pick. LEVY_WORMHOLD=0: keep walking while the head closes (no hold-still).</summary>
		internal static bool WormPickOn = Environment.GetEnvironmentVariable("LEVY_WORMPICK") != "0", WormHoldOn = Environment.GetEnvironmentVariable("LEVY_WORMHOLD") != "0";
		private bool _wormScan; private float _wormMargin;
		internal int WormHolds, WormCommits, WormJerks;
		private static readonly System.Collections.Generic.List<Threat> _thrAged = new(8);

		/// <summary>Stand still (decelerating) until the next look: the head has not reached the point where a move is still just enough.</summary>
		private bool WormHoldStill()
		{
			WormHolds++; WhyM = Why.MDodgeHold;
			_dodgeHold = LevyConstants.WormDodge.HoldTicks; _dodgeCooldown = 0;
			NPC.velocity.X *= StopDecel;
			if (Math.Abs(NPC.velocity.X) < StopBelowPx) NPC.velocity.X = 0f;
			return true;
		}

		/// <summary>Would this manoeuvre still clear the head if it started SlackTicks later (the body holds still meanwhile)? Then wait.</summary>
		private bool WormCanWait(in Maneuver best, Vector2 c0, float g, int horizon, bool airScan)
		{
			int slack = LevyConstants.WormDodge.SlackTicks;
			ThreatField.Aged(_thrNear, slack, _thrAged);
			int imm = DanceMode >= 1 && DOn(4) && DashIFrameTicks > 0 && (best.Kind == 4 || best.Kind == 8 || best.Kind == 10) ? DashIFrameTicks : 0;
			int h = ThreatField.Hits(_thrAged, c0, NPC.width, NPC.height, best, g, Math.Max(8, horizon - slack), out _, out _, _wormMargin, imm);
			if (h == 0) return true;
			WormCommits++;
			return false;
		}

		/// <summary>Soft preference (a hit is 100): a candidate that moves along the head's heading, toward the head, or the way the head is travelling horizontally scores worse than one that goes across / against.</summary>
		private float WormAlongPenalty(in Maneuver m, Vector2 c0, float g)
		{
			Vector2 d = ThreatField.PathAt(c0, m, g, 12) - c0;
			float len = d.Length(); if (len < 20f) return 0f;
			Vector2 u = d / len;
			float pen = 0f; int best = -1; float bd = float.MaxValue;
			for (int i = 0; i < _thrNear.Count; i++)
			{
				if (!_thrNear[i].Head) continue;
				float dd = Vector2.DistanceSquared(_thrNear[i].P, c0);
				if (dd < bd) { bd = dd; best = i; }
			}
			if (best < 0) return 0f;
			Threat h = _thrNear[best];
			float hs = h.V.Length(); if (hs < 1f) return 0f;
			Vector2 hv = h.V / hs;
			pen += Math.Max(0f, Vector2.Dot(u, hv));                                                      // along its heading
			Vector2 toHead = h.P - c0; float tl = toHead.Length(); if (tl > 1f) pen += Math.Max(0f, Vector2.Dot(u, toHead / tl));  // toward the head
			float hx = Math.Abs(h.V.X) > 1f ? Math.Sign(h.V.X) : 0f;
			pen += Math.Max(0f, u.X * hx);                                                                // with its horizontal direction of travel
			return pen * LevyConstants.WormDodge.AlongPenalty;
		}

		/// <summary>
		/// The hard worm rule. Returns -1 when no head is predicted to reach this body (nothing to do), 1 to hold still (the head is not close enough yet), 2 with <paramref name="m"/> set to the move to make now,
		/// 0 when no move in the allowed direction exists (the ordinary search takes over). The side is AGAINST the head's horizontal travel (for a head rising or diving, away from its turn, else the open side);
		/// the move is the strongest the gear allows: dash + jump, double jump, step + jump, air kick.
		/// </summary>
		private int WormPick(bool grounded, bool airScan, Vector2 c0, float g, int horizon, float sp, int goalSide, out Maneuver m)
		{
			m = default;
			int hi = -1; float bd = float.MaxValue;
			for (int i = 0; i < _thrNear.Count; i++) { if (!_thrNear[i].Head) continue; float dd = Vector2.DistanceSquared(_thrNear[i].P, c0); if (dd < bd) { bd = dd; hi = i; } }
			if (hi < 0) return -1;
			Threat h = _thrNear[hi];
			var stay = new Maneuver { Kind = 0 };
			if (airScan) { stay.Air = true; stay.V0x = _dV0x; stay.V0y = _dV0y; stay.FloorY = _dFloorY; }
			int sh = ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, stay, g, horizon, out int first, out _, 20f);
			if (sh == 0) return -1;
			// the side: against where the head is going
			float dx20 = ThreatField.PosAt(h, 20).X - h.P.X, dx40 = ThreatField.PosAt(h, 40).X - h.P.X;
			int dir;
			if (Math.Abs(dx20) > LevyConstants.WormDodge.VerticalDriftPx) dir = dx20 > 0 ? -1 : 1;
			else if (Math.Abs(dx40) > 12f) dir = dx40 > 0 ? -1 : 1;             // rising / diving: it will drift this way as it turns, so leave the other way
			else
			{
				bool l = grounded ? StepLaneSafe(-1, sp * 14f) : SidestepClearAir(-1), r = grounded ? StepLaneSafe(1, sp * 14f) : SidestepClearAir(1);
				dir = l && !r ? -1 : r && !l ? 1 : (c0.X >= h.P.X ? 1 : -1);        // the open side, else the side already on
			}
			float hsp = h.V.Length(); bool horizontalHead = hsp > 1f && Math.Abs(h.V.X) > 0.8f * hsp;
			float g0 = g; int hopTiles = DodgeHopTiles + (_kitAny ? FlightDodgeHopBonusTiles : 0);
			for (int attempt = 0; attempt < 2; attempt++, dir = -dir)
			{
				Maneuver bestM = default; int bestH = int.MaxValue; bool have = false;
				void Try(Maneuver c)
				{
					if (airScan) { c.Air = true; c.V0x = _dV0x; c.V0y = _dV0y; c.FloorY = _dFloorY; }
					int hh = ThreatField.Hits(_thrNear, c0, NPC.width, NPC.height, c, g0, horizon, out _, out _, 0f);
					if (!have || hh < bestH) { bestM = c; bestH = hh; have = true; }
				}
				float jvFull = -JumpSpeedFor(Abilities.JumpTiles);
				if (horizontalHead)
				{
					// a head flying along the row: across its path is up (or down through a platform), never along the floor into or with it
					if (attempt == 1) break;
					if (grounded && Abilities.ExtraJumps - _jumpsUsed > 0)
					{
						float j2 = -JumpSpeedFor(BaseJumpTiles) * ExtraJumpSpeedMult; int apex = Math.Max(2, (int)(-jvFull / g0 * 0.9f));
						Try(new Maneuver { Kind = 7, JumpVy = jvFull, J2Vy = j2, J2At = apex, Penalty = 0f });
					}
					if (grounded) Try(new Maneuver { Kind = 6, JumpVy = jvFull, Penalty = 0f });
					if (grounded && PlatformDropIsSafe()) Try(new Maneuver { Kind = 5, Drop = true, Penalty = 0f });
					if (airScan)
					{
						if (AirKickAvailable() && _dV0y > -AirJumpImpulsePx * 0.8f) Try(new Maneuver { Kind = 11, J2Vy = -(AirJumpImpulsePx + JumpSpeedOffset), J2At = 0, Penalty = 0f });
						if (AirThrustAvailable()) Try(new Maneuver { Kind = 12, J2Vy = -AirThrustVy, J2At = 0, HookDur = AirThrustTicks, Penalty = 0f });
						if (_dSolidFloorY < c0.Y + 40f * 16f) Try(new Maneuver { Kind = 13, J2Vy = AirFastFallVy, J2At = 0, Penalty = 0f });
					}
					if (!have) return 0;
					bool goV;
					if (first <= 2) goV = true;
					else if (bestH == 0) goV = !WormCanWait(bestM, c0, g0, horizon, airScan) || first <= LevyConstants.WormDodge.CommitTicks - 6;
					else goV = first <= LevyConstants.WormDodge.CommitTicks;
					if (!goV) return WormHoldOn ? 1 : -1;
					m = bestM; return 2;
				}
				bool laneOk = grounded ? StepLaneSafe(dir, sp * 14f) : SidestepClearAir(dir);
				if (!laneOk) continue;
				if (Abilities != null && Abilities.HasDash && _dashCooldown <= 0 && DashSpaceClear(dir) && (grounded || airScan))
				{
					if (grounded) Try(new Maneuver { Kind = 8, Vx = dir * DashSpeedPx, Dur = DashBurstTicks, JumpVy = jvFull, Penalty = 0f });
					else Try(new Maneuver { Kind = 4, Vx = dir * DashSpeedPx, Dur = DashBurstTicks, Penalty = 0f });
				}
				if (grounded && Abilities.ExtraJumps - _jumpsUsed > 0)
				{
					float j2 = -JumpSpeedFor(BaseJumpTiles) * ExtraJumpSpeedMult; int apex = Math.Max(2, (int)(-jvFull / g0 * 0.9f));
					Try(new Maneuver { Kind = 7, Vx = dir * sp, Dur = 36, JumpVy = jvFull, J2Vy = j2, J2At = apex, Penalty = 0f });
				}
				if (grounded) Try(new Maneuver { Kind = 3, Vx = dir * sp, Dur = 14, JumpVy = jvFull, Penalty = 0f });
				if (airScan && AirKickAvailable() && _dV0y > -AirJumpImpulsePx * 0.8f)
					Try(new Maneuver { Kind = 11, Vx = dir * sp, Dur = 24, AirAcc = RunAccel, J2Vy = -(AirJumpImpulsePx + JumpSpeedOffset), J2At = 0, Penalty = 0f });
				if (!have) { if (airScan || grounded) Try(new Maneuver { Kind = 1, Vx = dir * sp, Dur = 22, AirAcc = airScan ? RunAccel : 0f, Penalty = 0f }); }
				if (!have) continue;
				// timing: as late as still works. A move that clears waits until starting a few ticks later would not; one that cannot fully clear goes when the head is CommitTicks away.
				bool go;
				if (first <= 2) go = true;
				else if (bestH == 0) go = !WormCanWait(bestM, c0, g0, horizon, airScan) || first <= LevyConstants.WormDodge.CommitTicks - 6;
				else go = first <= LevyConstants.WormDodge.CommitTicks;
				if (!go) return WormHoldOn ? 1 : -1;
				m = bestM; return 2;
			}
			return 0;
		}
	}
}