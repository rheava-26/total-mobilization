using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// SHOTS INTO A WALL. A levy fires when a thin ray (Collision.CanHitLine, one pixel wide) from its muzzle to the target's centre is clear. A target that hugs a wall (Supreme Calamitas's brothers pushed against the arena's walls, a boss
	/// pressed into a tile edge) can have a clear ray and still be unreachable: the shot is wider than the ray, grazes the corner and dies on the tile, and the levy, whose gate said "clear", fires the next one into the same corner and stands
	/// where it is. This watches what the gate cannot: every shot of the levy that dies on a tile within <see cref="LevyConstants.WasteNearTiles"/> of its target counts, and <see cref="LevyConstants.WasteLimit"/> of them inside
	/// <see cref="LevyConstants.WasteWindowTicks"/> make the levy distrust the thin ray for <see cref="LevyConstants.WasteStrictTicks"/>: its line checks (the fire gate, the engage spot search, the cover search) use a ray as wide as a shot
	/// (<see cref="LevyConstants.WasteRayPx"/>). A corner-grazing line then reads as blocked, so the existing cover logic runs: wait out the debounce, look for another angle (a perch with a clear thick line), else write the target off for a while
	/// and take another or return to its order. General: any target in or behind tiles, no boss named. <c>LEVY_SHOTWASTE=0</c> turns it off (lab A/B).
	/// </summary>
	public partial class SoldierNPC
	{
		private static readonly bool ShotWasteEnv = Environment.GetEnvironmentVariable("LEVY_SHOTWASTE") != "0";
		/// <summary>Lab: the world check switches the rule off for its BEFORE run.</summary>
		internal static bool ShotWasteOnLab = true;
		internal static bool ShotWasteOn => ShotWasteEnv && ShotWasteOnLab;
		/// <summary>Harness telemetry: shots counted as spent on a wall near the target, strict windows opened.</summary>
		internal static long WasteNoted, WasteStrictOpened;
		private int _wasteTarget = -1, _wasteCount, _wasteFirst, _strictUntil = -1;

		/// <summary>One of this levy's shots has just struck a tile. Called by SoldierProjectileTag.OnTileCollide.</summary>
		internal void NoteShotHitTile(Projectile p)
		{
			if (!ShotWasteEnv || LevyUtil.IsClient || !IsAlive) return;
			int t = TargetIndex;
			if (t < 0 || t >= Main.maxNPCs) return;
			NPC n = Main.npc[t];
			if (!n.active) return;
			float near = LevyConstants.WasteNearTiles * LevyConstants.TilePx + Math.Max(n.width, n.height) * 0.5f;
			if (Vector2.DistanceSquared(p.Center, n.Center) > near * near) return;
			int now = (int)Main.GameUpdateCount;
			if (_wasteTarget != t || now - _wasteFirst > LevyConstants.WasteWindowTicks) { _wasteTarget = t; _wasteCount = 0; _wasteFirst = now; }
			WasteNoted++;
			if (++_wasteCount >= LevyConstants.WasteLimit) { _wasteCount = 0; if (ShotWasteOn) { _strictUntil = now + LevyConstants.WasteStrictTicks; WasteStrictOpened++; } }
		}

		/// <summary>Lab: forget the shots counted and end the strict window.</summary>
		internal void TestResetWaste() { _wasteTarget = -1; _wasteCount = 0; _strictUntil = -1; }

		/// <summary>True while the levy distrusts the thin ray (it has been shooting a wall).</summary>
		internal bool WasteStrict => ShotWasteOn && _strictUntil > (int)Main.GameUpdateCount;

		/// <summary>A clear line between two points for a shot of this levy: the thin ray, or while it distrusts it a ray as wide as a shot.</summary>
		internal bool LineClear(Vector2 from, Vector2 to)
		{
			if (!WasteStrict) return Aiming.HasLineOfSight(from, to);
			float h = LevyConstants.WasteRayPx * 0.5f;
			int w = (int)LevyConstants.WasteRayPx;
			return Collision.CanHit(from - new Vector2(h, h), w, w, to - new Vector2(h, h), w, w);
		}
	}
}
