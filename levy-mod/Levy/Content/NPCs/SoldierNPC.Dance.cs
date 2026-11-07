using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// BOSS DANCE. A player fighting a boss never stands: he runs long legs across the lane, hops, hovers and strafes in the air when he has wings, and keeps firing the whole time.
	/// Against a boss (or a hard hitter) a levy in reach runs a leg of <see cref="DanceLegMinTicks"/>..<see cref="DanceLegMaxTicks"/> ticks at run speed, flips on a ledge, a wall or when the boss closes
	/// (away from it first), hops on its own clock (a double jump on top when the gear has one), and with a sustained flight kit lifts to a hover a few tiles above the floor and strafes there.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>0 = the old shuffle (short steps, suppressed inside the standoff margin), 1 = boss dance.</summary>
		internal static int DanceMode = 1;
		private int _danceDir, _danceLegEnd, _danceHopNext;
		internal int DanceLegs, DanceHops, DanceFlights, DanceFlips;
		private const int DanceLegMinTicks = 40, DanceLegMaxTicks = 95, DanceRunTiles = 10, DanceHopMinTicks = 40, DanceHopMaxTicks = 95, DanceHoverMinTiles = 5, DanceHoverSpreadTiles = 4;
		private const int DanceFlightRelaunchTicks = 20;

		/// <summary>True when the dance owns this think's movement (a goal was set or the body is deliberately holding).</summary>
		private bool BossDance(NPC seg, float range, float standoff, int now)
		{
			NPC npc = NPC;
			DanceLift(seg);
			bool aloft = WantsAltitude || _aerial || npc.velocity.Y != 0f;
			float dxBoss = seg.Center.X - npc.Center.X;
			float distTiles = Math.Abs(dxBoss) / LevyConstants.TilePx;
			float awayDir = dxBoss > 1f ? -1f : dxBoss < -1f ? 1f : (_danceDir != 0 ? _danceDir : 1f);
			bool close = distTiles < (standoff + LevyConstants.StrafeMarginTiles) * 1.4f;

			if (_danceDir == 0 || now >= _danceLegEnd || (close && _danceDir != awayDir && Main.rand.NextBool(4)))
			{
				int pick;
				if (close) pick = (int)awayDir;
				else pick = SlotFrac(SlotSaltPhase) * 2f > 1f ? (_danceDir == 0 ? 1 : -_danceDir) : (_danceDir == 0 ? -1 : -_danceDir);
				if (_danceDir != 0 && pick != _danceDir) DanceFlips++;
				_danceDir = pick;
				_danceLegEnd = now + DanceLegMinTicks + Main.rand.Next(DanceLegMaxTicks - DanceLegMinTicks + 1);
				DanceLegs++;
			}

			Vector2 dest = default; bool have = false;
			for (int attempt = 0; attempt < 2 && !have; attempt++)
			{
				int dir = attempt == 0 ? _danceDir : -_danceDir;
				if (aloft)
				{
					float x = npc.Center.X + dir * DanceRunTiles * LevyConstants.TilePx;
					if (AirLaneSafe(dir, DanceRunTiles)) { dest = new Vector2(x, npc.Center.Y); have = true; }
				}
				else
				{
					int tiles = DanceRunTiles;
					while (tiles >= 3 && !have)
					{
						if (FindSafeStep(dir, tiles, seg.Center, range, standoff, false, out dest) && Math.Abs(dest.X - npc.Center.X) >= Math.Min(tiles, 6) * 16f * 0.7f) have = true;
						else tiles -= 3;
					}
				}
				if (have && attempt == 1) { _danceDir = dir; DanceFlips++; _danceLegEnd = now + DanceLegMinTicks; }
			}
			if (!have) { WhyB = Why.BLedgeNoStep; return true; }

			// hops on their own clock: a bunny hop under a ground charge, a double jump on top when the gear has one
			if (!aloft && npc.velocity.Y == 0f && now >= _danceHopNext && !LavaAhead(_danceDir) && !npc.wet)
			{
				_danceHopNext = now + DanceHopMinTicks + Main.rand.Next(DanceHopMaxTicks - DanceHopMinTicks + 1);
				int tiles = Math.Min(Abilities.JumpTiles, PlayerBaseJumpTiles);
				if (TryJump(tiles)) { DanceHops++; if (Abilities.ExtraJumps > 0 && Main.rand.NextBool()) _air2Ready = (uint)now + 20u; }
			}
			WhyB = aloft ? Why.BAirStrafe : Why.BStrafe;
			ApplyChaseClamp(ref dest);
			SetGoal(dest);
			return true;
		}

		/// <summary>Flight first: a body that can hover and strafe does, at a height that takes it out of the contact lane. Called every think of a boss fight.</summary>
		private void DanceLift(NPC seg)
		{
			NPC npc = NPC;
			if (seg != null && (seg.noGravity || seg.aiStyle == NPCAIStyleID.Worm)) return;   // a flyer or a worm owns the air: hovering at its height puts the body in its lane, so on wings the body keeps its floor and strafes
			if (!(AerialSustained && HasFlightKit && !WantsAltitude && !_aerialSpent && AerialMaxReachTiles >= LevyConstants.Movement.AerialClimbBenefitTiles && !RoofOverhead())) return;
			float groundY = AerialGroundY == float.MinValue ? npc.Bottom.Y : AerialGroundY;
			float hoverPx = (DanceHoverMinTiles + SlotFrac(SlotSaltAltitude) * DanceHoverSpreadTiles) * LevyConstants.TilePx;
			hoverPx = Math.Min(hoverPx, AerialMaxReachTiles * LevyConstants.MaxReachUseFrac * LevyConstants.TilePx);
			if (hoverPx < LevyConstants.Movement.AerialClimbBenefitTiles * LevyConstants.TilePx) return;
			if (_aerialCooldown > DanceFlightRelaunchTicks) _aerialCooldown = DanceFlightRelaunchTicks;
			WantsAltitude = true; AltitudeBottomY = groundY - hoverPx; _altHeld = true; DanceFlights++;
		}

		private int _cornerUntil, _cornerDir; internal int DanceCorners, DanceOvers;

		/// <summary>
		/// The direction to back away from a threat. Backing into a wall or off a platform edge is a dead end a player never takes: cornered, the body goes OVER and THROUGH instead, back across the boss
		/// (a dash with its i-frames when it has one, otherwise a run with the dodge hops and double jumps), until it is on the far side.
		/// </summary>
		private float EscapeDir(float away, NPC threat, int now)
		{
			if (DanceMode < 1) return away;
			if (now < _cornerUntil)
			{
				if (Math.Sign(NPC.Center.X - threat.Center.X) == _cornerDir && Math.Abs(NPC.Center.X - threat.Center.X) > 6f * LevyConstants.TilePx) _cornerUntil = 0;
				else return _cornerDir;
			}
			if (NPC.velocity.Y != 0f && (WantsAltitude || _aerial)) return away;   // aloft the lane has no wall to be pinned on
			if (SurfaceLane((int)away, 5, false)) return away;
			_cornerDir = -(int)away; _cornerUntil = now + 100; DanceCorners++;
			bool grounded = NPC.velocity.Y == 0f;
			if (grounded && Abilities != null && Abilities.HasDash && _dashCooldown <= 0 && DashSpaceClear(_cornerDir))
			{
				DanceOvers++; DodgeDashes++; DashIFrames(); _dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1);
				Begin(_cornerDir * DashSpeedPx, DashBurstTicks); NPC.netUpdate = true;
			}
			else if (grounded && NPC.velocity.Y == 0f && !LavaAhead(_cornerDir) && TryJump(Math.Min(Abilities.JumpTiles, PlayerBaseJumpTiles))) { DanceOvers++; if (Abilities.ExtraJumps > 0) _air2Ready = (uint)now + 20u; }
			return _cornerDir;
		}

		/// <summary>Room to strafe in the air: no wall on the body's row for the run, and no lava or void under the landing column.</summary>
		private bool AirLaneSafe(int dir, int tiles)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int row = (int)(NPC.Center.Y / 16f);
			for (int i = 1; i <= tiles; i++)
			{
				int x = feet.X + dir * i;
				if (TileNav.IsSolid(x, row) || TileNav.IsSolid(x, row - 1)) return false;
			}
			int cx = feet.X + dir * tiles;
			for (int r = feet.Y; r < feet.Y + 60; r++)
			{
				if (TileNav.HasLava(cx, r)) return false;
				if (TileNav.IsFloor(cx, r) || TileNav.IsSolid(cx, r)) return true;
			}
			return false;
		}
	}
}
