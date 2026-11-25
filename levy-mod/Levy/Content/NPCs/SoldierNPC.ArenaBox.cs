using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Compat.Calamity;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// ARENA BOX. A Calamity boss arena (Supreme Calamitas, the Calamitas Clone in Revengeance and above) is a rectangle of invisible walls that holds a body in (Common/Compat/Calamity/ArenaBoxes.cs); the tiles under and around it are
	/// the world's own and do not know about it. A levy reads the box as its whole world, three ways:
	///  * <b>Goals stay inside it.</b> A goal chosen outside the box (a follow slot behind the commander at a wall, a retreat from the boss, a perch on a row that crosses the wall, a firing altitude above the ceiling) is pulled to a few tiles
	///    inside, so a body is never walked into a wall it cannot pass and left there. The dodge reads the same walls: a manoeuvre's path stops at them (ThreatField.ClipOn).
	///  * <b>The bottom wall is a floor.</b> A body the box has stopped from falling (the world's ground is below the box, or a platform row crosses the bottom wall) rests on the wall; the wall's own push-back and gravity used to fight each tick
	///    and left it falling forever (a downward speed of a full tile a tick, never grounded), so it could neither jump, hook, take off nor dodge. Resting on the wall now counts as standing.
	///  * <b>Rows and perches are chosen inside it.</b>
	/// Nothing here is keyed to a boss name; with no box up every entry is one static check. <c>LEVY_ARENABOX=0</c> switches the whole box off (ArenaBoxes.On).
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Tiles a goal keeps from a side wall and from the ceiling.</summary>
		private const float ArenaWallMarginTiles = 3f, ArenaCeilingMarginTiles = 2f;
		/// <summary>Harness telemetry: goals and altitudes pulled inside a box, ticks a body was settled on the box floor.</summary>
		internal static long BoxClamps, BoxFloorTicks;
		private int _boxFloorTick = -1000;

		/// <summary>PostAI: the box has held this body; <paramref name="floor"/> is true when it rests on the bottom wall.</summary>
		internal void NoteBoxHeld(bool floor) { if (floor) _boxFloorTick = (int)Main.GameUpdateCount; }

		/// <summary>True while the box's bottom wall has been holding this body up (it rested on it last tick).</summary>
		internal bool OnBoxFloor => (int)Main.GameUpdateCount - _boxFloorTick <= 1;

		/// <summary>
		/// The start of the movement tick: a body that rested on the box's bottom wall last tick is standing, so the gravity the engine added after the wall held it is taken back (a body that is rising or already falling fast is left alone).
		/// Standing is what the rest of the movement reads (<c>velocity.Y == 0</c>): it walks, jumps, takes off, hooks and dodges from there as from the ground.
		/// </summary>
		private void SettleOnBoxFloor()
		{
			if (!OnBoxFloor) return;
			NPC npc = NPC;
			if (npc.velocity.Y < 0f) return;   // rising (a jump, a thrust): not resting
			npc.velocity.Y = 0f; BoxFloorTicks++;
		}

		/// <summary>The walls of the box that holds this body, taken in from each side by the goal margin; false with no box up or one too small to inset.</summary>
		private bool ArenaInner(out Vector2 lo, out Vector2 hi)
		{
			lo = hi = default;
			if (!ArenaBoxes.Active || !ArenaBoxes.TryBounds(NPC, out Vector2 tl, out Vector2 br)) return false;
			float mx = ArenaWallMarginTiles * LevyConstants.TilePx, my = ArenaCeilingMarginTiles * LevyConstants.TilePx;
			if (br.X - tl.X <= 2f * mx + 1f || br.Y - tl.Y <= my + BodyHeight + 1f) return false;
			lo = new Vector2(tl.X + mx, tl.Y + my + BodyHeight); hi = new Vector2(br.X - mx, br.Y);   // a goal is a point for the feet: the ceiling margin is kept above the head
			return true;
		}

		/// <summary>True when a point for the feet is inside the walls of the box that holds this body (or none holds it).</summary>
		private bool InArenaBox(Vector2 feet) => !ArenaInner(out Vector2 lo, out Vector2 hi) || (feet.X >= lo.X && feet.X <= hi.X && feet.Y >= lo.Y && feet.Y <= hi.Y);

		/// <summary>End of a think: a goal or a firing altitude outside the box is pulled inside it.</summary>
		private void ApplyArenaBox(int now)
		{
			if (!ArenaBoxes.On || (!HasMoveGoal && !WantsAltitude) || !ArenaInner(out Vector2 lo, out Vector2 hi)) return;
			if (HasMoveGoal)
			{
				Vector2 g = MoveGoal, c = new Vector2(MathHelper.Clamp(g.X, lo.X, hi.X), MathHelper.Clamp(g.Y, lo.Y, hi.Y));
				if (c != g) { SetGoal(c); _platActive = false; BoxClamps++; }
			}
			if (WantsAltitude)
			{
				float a = MathHelper.Clamp(AltitudeBottomY, lo.Y, hi.Y);
				if (a != AltitudeBottomY) { AltitudeBottomY = a; BoxClamps++; }
			}
		}
	}
}
