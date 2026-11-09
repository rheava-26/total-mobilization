using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// WALL CLIMBING with spiked gear (Player.spikedBoots: Shoe Spikes, Climbing Claws, Tiger Climbing Gear, Master Ninja Gear, mods), on a planned route (a climb edge of <see cref="PathSearch"/>).
	/// The body does what a player does on a wall (Player.WallslideMovement): pressed against a wall while it falls it catches on it (strong spikes hold it still, weak ones slow the slide), and jumping from the wall
	/// kicks it off with 3 px/tick sideways and a full jump upward; holding toward the wall brings it back in time for the next catch. Each cycle gains one jump of height. At the lip it steps onto the top.
	/// </summary>
	public partial class SoldierNPC
	{
		private bool _clmOn, _clmFailed; private int _clmDir, _clmIdx, _clmCatch; private uint _clmStart, _clmBestTick; private float _clmBestY;
		private Vector2 _clmTarget; private Point _clmTile; private Route _clmRt;
		/// <summary>Climb edges finished, and given up (harness telemetry).</summary>
		internal int ClimbEdges, ClimbFails;
		internal bool ClimbActive => _clmOn;

		/// <summary>Starts the climb at the foot of a face toward <paramref name="target"/> (bottom-centre of the cell on top). False when this body cannot climb or the target is not beside the foot.</summary>
		private bool ClimbStart(Vector2 target, Vector2 foot)
		{
			if (!Abilities.CanWallClimb || NPC.wet) return false;
			int dir = Math.Sign(target.X - foot.X);
			if (dir == 0) return false;
			_clmOn = true; _clmFailed = false; _clmDir = dir; _clmTarget = target; _clmTile = TileNav.FeetTile(target); _clmCatch = 0;
			_clmStart = _clmBestTick = Main.GameUpdateCount; _clmBestY = NPC.Bottom.Y; _clmRt = _rt; _clmIdx = _rtIdx;
			return true;
		}

		/// <summary>Drops the climb without counting it (a teleport, a reset, an order change).</summary>
		private void ClimbCancel() { _clmOn = false; }

		private void ClimbEnd(bool ok)
		{
			_clmOn = false;
			if (ok)
			{
				ClimbEdges++;
				if (_rt != null && _rt == _clmRt && _rtIdx == _clmIdx && _rtIdx < _rt.Pts.Count - 1) _rtIdx++;   // the lip is reached: on to the next waypoint
				_rtLegSent = new Vector2(float.MinValue); _path.Clear();
			}
			else { ClimbFails++; _clmFailed = true; }
		}

		private bool ClimbWallContact()
		{
			NPC n = NPC;
			int col = (int)((_clmDir > 0 ? n.position.X + n.width + 1f : n.position.X - 1f) / 16f), feet = (int)((n.Bottom.Y - 1f) / 16f);
			return TileNav.IsSolid(col, feet) || TileNav.IsSolid(col, feet - 1) || TileNav.IsSolid(col, feet - 2);
		}

		/// <summary>One tick of the climb. True while it owns the tick.</summary>
		private bool TickClimb(bool grounded)
		{
			NPC npc = NPC; uint now = Main.GameUpdateCount;
			if (!Abilities.CanWallClimb || npc.wet || _hkPhase != HookPhase.Idle || now - _clmStart > (uint)ClimbMaxTicks) { ClimbEnd(false); return false; }
			if (npc.Bottom.Y < _clmBestY - 16f) { _clmBestY = npc.Bottom.Y; _clmBestTick = now; }
			else if (now - _clmBestTick > (uint)ClimbStallTicks) { ClimbEnd(false); return false; }
			float g = Gravity();
			npc.direction = _clmDir; npc.spriteDirection = _clmDir;

			// feet at the level of the lip: walk onto the top
			if (npc.Bottom.Y <= _clmTarget.Y + 6f)
			{
				npc.velocity.X = _clmDir * 2f;
				float over = (npc.Center.X - _clmTarget.X) * _clmDir;
				if (grounded && over >= -8f && Math.Abs(npc.Bottom.Y - _clmTarget.Y) < 10f) { npc.velocity.X = 0f; ClimbEnd(true); return false; }
				return true;
			}

			bool contact = ClimbWallContact();
			int jt = Math.Max(2, Abilities.JumpTiles);
			if (grounded)
			{
				npc.velocity.Y = -JumpSpeedFor(jt); npc.velocity.X = _clmDir * ClimbPressPx; npc.netUpdate = true; _clmCatch = 0;
				return true;
			}
			if (contact && npc.velocity.Y >= 0f)
			{
				// caught on the wall: strong spikes hold the body still, weak ones let it slide slowly; a few ticks later it jumps off the wall
				npc.velocity.X = _clmDir * ClimbPressPx;
				if (++_clmCatch > ClimbCatchTicks)
				{
					npc.velocity.Y = -JumpSpeedFor(jt); npc.velocity.X = -_clmDir * ClimbKickPx; npc.netUpdate = true; _clmCatch = 0;
				}
				else npc.velocity.Y = Abilities.SpikedBoots >= 2 ? -g + 0.02f : Math.Min(npc.velocity.Y, ClimbSlideSpeedPx - g);
				return true;
			}
			// in the air: hold toward the wall (the kick off it wears off at the player's run slowdown plus acceleration)
			float toward = npc.velocity.X * _clmDir;
			if (toward < -0.2f) toward += 0.2f;
			toward = Math.Min(ClimbPressPx, toward + ClimbReturnAccel);
			npc.velocity.X = toward * _clmDir;
			return true;
		}
	}
}
