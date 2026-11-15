using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// CHARGE LANES. A big boss that charges (Calamity's Leviathan: a 900 x 450 body at 20+ px/tick, aimed once) sweeps a band as tall as itself; a dodge along the line, or a hop of a few tiles, never leaves it. From the line-up on,
	/// a soldier inside the band goes ACROSS it: up on its wings (the way a player flies out of the line) or down through the platforms, to just past the band's edge, and holds there until the boss has left its charge state.
	/// Each soldier takes the side its own parity picks when both are open and a slot-dependent extra clearance, so one charge does not line the army up. Real movement only (wing thrust, platform drop); the boss is untouched.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _laneDir; private float _laneY; private uint _laneLast; private int _laneHold;
		internal int LaneEscapes, LaneUp, LaneDown, LaneTicks, LaneNoPlan;
		internal static bool LaneDebug = Environment.GetEnvironmentVariable("LEVY_LANEDBG") == "1"; private static int _laneDbgN;
		private const float LaneEscapeAccelPx = 1.6f, LaneHoverAccelPx = 1.0f, LaneThrustMult = 1.25f; private const int LaneFuelReserve = 6;   // a charge is the moment the whole tank is for

		/// <summary>True when the lane escape owns this tick's movement (a thrust across the band, or a hold beyond its edge).</summary>
		private bool TickChargeLane(bool grounded, bool air)
		{
			if (!ChargeLanes.On || Hostile || NPC.lavaWet) { _laneDir = 0; return false; }
			var lanes = ChargeLanes.Get();
			NPC npc = NPC; uint now = Main.GameUpdateCount;
			if (lanes.Count == 0)
			{
				if (_laneDir != 0 && now - _laneLast > 20u) _laneDir = 0;
				return false;
			}
			bool wings = Abilities != null && Abilities.HasWings && _wingTime > LaneFuelReserve;
			float halfH = npc.height * 0.5f;
			// the nearest lane this body is in (or already escaped from and is holding beside)
			int hit = -1;
			for (int i = 0; i < lanes.Count; i++) if (lanes[i].Kind != 0 && ChargeLanes.InLane(lanes[i], npc.Center, halfH)) { hit = i; break; }   // a hover (kind 0) only steers the rows (SoldierNPC.Rows.cs): a body cannot hold the air that long
			if (hit < 0)
			{
				if (_laneDir == 0) return false;
				_laneLast = now; LaneTicks++;
				// out of the band on the far side of a climb: hold the height beside it while the boss stays in its charge state (a fall simply carries on to its floor)
				if (_laneDir > 0 || !wings) { if (grounded) _laneDir = 0; return false; }
				ChargeLane near = lanes[0]; for (int i = 1; i < lanes.Count; i++) if (Math.Abs(lanes[i].Cy - npc.Center.Y) < Math.Abs(near.Cy - npc.Center.Y)) near = lanes[i];
				if (Math.Abs(npc.Center.Y - near.Cy) - near.Half - halfH > 12f * 16f) return false;
				Hover(); return true;
			}
			ChargeLane lane = lanes[hit];
			_laneLast = now;
			if (_laneDir < 0 && !wings) _laneDir = 0;   // the fuel ran out on the way up: pick again (a fall may still do)
			if (_laneDir == 0)
			{
				// choose the way across
				bool canDrop = grounded ? PlatformDropIsSafe() : !npc.wet;
				AirFloors(out _, out float solidFloor);
				if (LaneFloorIsLava(solidFloor)) solidFloor = npc.Center.Y;   // never a fall that ends in lava
				bool ok = ChargeLanes.Plan(lane, npc.Center.Y, halfH, wings, FlySpeed * LaneThrustMult * 0.95f, _wingTime - LaneFuelReserve, canDrop, solidFloor, npc.whoAmI & 1, SlotFrac(SlotSaltAltitude), out int dir, out float ty);
				if (!ok) { LaneNoPlan++; if (LaneDebug && _laneDbgN++ < 60) Levy.Instance?.Logger.Info($"LANEDBG noplan {SoldierName} grounded={grounded} wings={wings} wingTime={_wingTime} y={npc.Center.Y:0} laneCy={lane.Cy:0} half={lane.Half:0} floorDepth={(solidFloor - npc.Center.Y):0} canDrop={canDrop} kind={lane.Kind}"); return false; }
				_laneDir = dir; _laneY = ty; LaneEscapes++; if (dir < 0) LaneUp++; else LaneDown++;
				npc.netUpdate = true;
			}
			if (_laneDir < 0) { Thrust(-1); return true; }
			// down: let go. From a platform the drop-through is held; in the air the fall is helped to the speed a body falls at
			if (grounded) { _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; }
			npc.velocity.Y = Math.Max(npc.velocity.Y, Math.Min(ChargeLanes.FallPx, npc.velocity.Y + 1.2f));
			npc.velocity.X *= 0.96f; _flying = false; npc.netUpdate = true;
			return true;
		}

		/// <summary>The floor a fall would reach is lava (AirFloors counts lava as a floor).</summary>
		private bool LaneFloorIsLava(float floorCenterY)
		{
			int col = (int)(NPC.Center.X / 16f), row = (int)((floorCenterY + NPC.height * 0.5f) / 16f);
			return TileNav.HasLava(col, row) || TileNav.HasLava(col, row + 1);
		}

		/// <summary>Full thrust straight across the band: vertical speed toward the far side, sideways drift damped (the army stays where it is while it climbs).</summary>
		private void Thrust(int dir)
		{
			NPC npc = NPC;
			float want = dir * FlySpeed * LaneThrustMult;
			npc.velocity.Y += MathHelper.Clamp(want - npc.velocity.Y, -LaneEscapeAccelPx, LaneEscapeAccelPx);
			npc.velocity.Y -= Gravity();
			npc.velocity.X *= 0.96f;
			_wingTime--; _flying = true; _blocked = false; _aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = false; npc.netUpdate = true;
		}

		/// <summary>Hold the height beside the band: the vertical speed goes to zero (gravity cancelled) at the cost of flight time.</summary>
		private void Hover()
		{
			NPC npc = NPC;
			npc.velocity.Y += MathHelper.Clamp(-npc.velocity.Y, -LaneHoverAccelPx, LaneHoverAccelPx);
			npc.velocity.Y -= Gravity();
			npc.velocity.X *= 0.96f;
			_wingTime--; _flying = true; _blocked = false; _aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = false;
		}
	}
}
