using System;
using Terraria;
using Terraria.ID;

namespace Levy.Common.Movement
{
	/// <summary>
	/// What the floor under a runner does to its run, the way Player.Update applies it after the gear (the dummy a levy wears never runs that part, which is why asphalt did nothing for a levy):
	/// asphalt (<c>Player.powerrun</c>) multiplies the ground run cap by 3.5 and doubles the braking, honey blocks (<c>sticky</c>) quarter the cap and the acceleration, ice (<c>slippy</c>, <see cref="TileID.Sets.IceSkateSlippery"/>)
	/// and frozen slime (<c>slippy2</c>) cut the acceleration and the braking (Ice Skates give acceleration x3.5 and a 25% higher cap), and Desert Boots on sand make everything x1.75.
	/// The sprint (accRunSpeed, the boots' own number) is NOT scaled by asphalt, so a player tops out at the larger of the two, as in <c>HorizontalMovement</c>.
	/// The floor is found the way <c>Player.FloorVisuals</c> finds it: the solid tile under the centre of the feet, else the one to its left, else its right (a platform is not a floor tile).
	/// <c>LEVY_TILERUN=0</c> switches it off (A/B for the lab and the world check).
	/// </summary>
	public static class TileRun
	{
		public static bool On = Environment.GetEnvironmentVariable("LEVY_TILERUN") != "0";

		/// <summary>The most a levy runs on any floor (px/tick): asphalt under Lightning Boots and up would otherwise leave the 12 of <see cref="LevyConstants.Movement.MaxRunSpeed"/> behind.</summary>
		public const float CapPx = 16f;
		/// <summary>The least a slippery floor brakes (a multiple of the normal braking): a player's 0.1 would have a levy slide far past the spot it was walking to.</summary>
		public const float MinBrake = 0.3f;

		public enum Floor : byte { Plain, Asphalt, Honey, Ice, FrozenSlime, Sand, Conveyor }

		/// <summary>The floor tile type under a body's feet (-1 for none), by Player.FloorVisuals' rule.</summary>
		public static int FloorType(NPC n)
		{
			int x = (int)(n.Center.X / 16f), y = (int)((n.position.Y + n.height + 0.01f) / 16f);
			if (x < 2 || y < 2 || x >= Main.maxTilesX - 2 || y >= Main.maxTilesY - 2) return -1;
			return Player.GetFloorTileType(x, y);
		}

		public static Floor Kind(int tileType, MovementAbilities ab)
		{
			if (tileType < 0) return Floor.Plain;
			if (tileType == TileID.Asphalt) return Floor.Asphalt;
			if (tileType == TileID.HoneyBlock) return Floor.Honey;
			if (tileType == TileID.FrozenSlimeBlock) return Floor.FrozenSlime;
			if (tileType < TileID.Sets.IceSkateSlippery.Length && TileID.Sets.IceSkateSlippery[tileType]) return Floor.Ice;
			if (ab != null && ab.DesertBoots && (TileID.Sets.Conversion.Sand[tileType] || TileID.Sets.Conversion.Sandstone[tileType] || TileID.Sets.Conversion.HardenedSand[tileType])) return Floor.Sand;
			return Floor.Plain;
		}

		/// <summary>
		/// The run on this floor: the top speed (px/tick), the factor on the acceleration and the factor on the braking.
		/// Plain ground gives <see cref="MovementAbilities.MaxRunSpeed"/>, 1 and 1.
		/// </summary>
		public static void Resolve(MovementAbilities ab, Floor kind, out float top, out float accelMul, out float brakeMul)
		{
			float ground = ab.GroundRun, sprint = ab.SprintRun;
			accelMul = 1f; brakeMul = 1f;
			switch (kind)
			{
				case Floor.Honey: ground *= 0.25f; accelMul = 0.25f; brakeMul = 2f; top = ground; break; // the speed is clamped to the quartered cap, the sprint cannot carry past it
				case Floor.Asphalt: ground *= 3.5f; brakeMul = 2f; top = Math.Max(ground, sprint); break;
				case Floor.Sand: ground *= 1.75f; sprint *= 1.75f; accelMul = 1.75f; brakeMul = 1.75f; top = Math.Max(ground, sprint); break;
				case Floor.FrozenSlime:
					accelMul = 0.6f; brakeMul = 0f;
					if (ab.IceSkate) { accelMul *= 3.5f; ground *= 1.25f; }
					top = Math.Max(ground, sprint); break;
				case Floor.Ice:
					accelMul = 0.7f;
					if (ab.IceSkate) { accelMul *= 3.5f; ground *= 1.25f; } else brakeMul = 0.1f;
					top = Math.Max(ground, sprint); break;
				default: top = Math.Max(ground, sprint); break;
			}
			top = Math.Clamp(top, LevyConstants.Movement.MinRunSpeed * 0.25f, CapPx);
			if (brakeMul < MinBrake) brakeMul = MinBrake;
		}
	}
}
