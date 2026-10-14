using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Tile queries shared by the movement reflexes, the pathfinder and the travel helpers. A body is 20×42 px, so it
	/// needs three clear tile rows above its floor. Platforms are floors but never walls; closed doors are passable
	/// (the body opens them on the way). OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public static class TileNav
	{
		public static bool InBounds(int x, int y) => x >= 5 && y >= 5 && x < Main.maxTilesX - 5 && y < Main.maxTilesY - 5;

		public static bool IsClosedDoor(Tile t) => t.HasUnactuatedTile && (TileLoader.IsClosedDoor(t) || t.TileType == TileID.TallGateClosed);

		/// <summary>A wall: a tile the body cannot pass through. Platforms and closed doors are not solid here.</summary>
		public static bool IsSolid(int x, int y)
		{
			if (!InBounds(x, y)) return true;
			Tile t = Main.tile[x, y];
			if (!t.HasUnactuatedTile) return false;
			ushort type = t.TileType;
			if (!Main.tileSolid[type] || Main.tileSolidTop[type]) return false;
			return !IsClosedDoor(t);
		}

		/// <summary>Solid ground or a platform: something feet can rest on.</summary>
		public static bool IsFloor(int x, int y)
		{
			if (!InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			if (!t.HasUnactuatedTile || IsClosedDoor(t)) return false;
			ushort type = t.TileType;
			return Main.tileSolid[type] || Main.tileSolidTop[type];
		}

		/// <summary>A platform-like tile (solid top only): the body can drop through it.</summary>
		public static bool IsPlatform(int x, int y)
		{
			if (!InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.HasUnactuatedTile && Main.tileSolidTop[t.TileType];
		}

		public static bool HasLava(int x, int y)
		{
			if (!InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.LiquidAmount > 0 && t.LiquidType == LiquidID.Lava;
		}

		public static bool HasLiquid(int x, int y)
		{
			if (!InBounds(x, y)) return false;
			return Main.tile[x, y].LiquidAmount > 0;
		}

		/// <summary>Water, honey or shimmer deep enough to swim in.</summary>
		public static bool IsDeepWater(int x, int y)
		{
			if (!InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.LiquidAmount > LevyConstants.Movement.DeepWaterLiquid && t.LiquidType != LiquidID.Lava;
		}

		/// <summary>The three body cells with their feet in tile (x, y) hold no wall and no lava.</summary>
		public static bool IsBodyClear(int x, int y)
			=> !IsSolid(x, y) && !IsSolid(x, y - 1) && !IsSolid(x, y - 2) && !HasLava(x, y) && !HasLava(x, y - 1) && !HasLava(x, y - 2);

		/// <summary>The body can stand with its feet in tile (x, y): clear body cells over a floor that is not lava.</summary>
		public static bool IsStandable(int x, int y) => IsBodyClear(x, y) && IsFloor(x, y + 1) && !HasLava(x, y + 1);

		/// <summary>World position of the bottom-centre of a body standing with its feet in tile (x, y).</summary>
		public static Vector2 BottomCentre(int x, int y) => new Vector2(x * 16 + 8, (y + 1) * 16);

		/// <summary>The tile that holds the feet of a body whose bottom edge is at <paramref name="bottom"/>.</summary>
		public static Point FeetTile(Vector2 bottom) => new Point((int)(bottom.X / 16f), (int)((bottom.Y - 1f) / 16f));

		/// <summary>Nearest standable tile within <paramref name="radius"/> tiles of (cx, cy), searched ring by ring. False if none.</summary>
		public static bool NearestStandable(int cx, int cy, int radius, out Point found)
		{
			for (int r = 0; r <= radius; r++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					for (int dx = -r; dx <= r; dx++)
					{
						if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != r) continue;
						int x = cx + dx, y = cy + dy;
						if (!InBounds(x, y) || !IsStandable(x, y)) continue;
						found = new Point(x, y);
						return true;
					}
				}
			}
			found = new Point(cx, cy);
			return false;
		}

		/// <summary>
		/// How far a body whose feet are in row <paramref name="feetY"/> would fall in column <paramref name="x"/>, in tiles.
		/// Returns -1 when no floor lies within <see cref="LevyConstants.Movement.FallScanTiles"/>, -2 when lava is in the way.
		/// </summary>
		public static int DropDepth(int x, int feetY)
		{
			for (int ty = feetY + 1; ty <= feetY + LevyConstants.Movement.FallScanTiles; ty++)
			{
				if (HasLava(x, ty)) return -2;
				if (IsFloor(x, ty)) return ty - (feetY + 1);
			}
			return -1;
		}
	}
}
