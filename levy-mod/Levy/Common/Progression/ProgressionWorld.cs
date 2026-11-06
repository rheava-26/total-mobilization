using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Progression
{
	/// <summary>
	/// World-side progression assistance: Chlorophyte growth rate and chunk size, and the Dark Shard / Light Shard shimmer route.
	/// </summary>
	public class ProgressionWorld : ModSystem
	{
		/// <summary>Shimmer ring that makes Dark Shards reachable in any world: Forbidden Fragment -> Dark Shard -> Light Shard -> Forbidden Fragment.</summary>
		public static readonly int[] ShardRing = { ItemID.AncientBattleArmorMaterial, ItemID.DarkShard, ItemID.LightShard };
		/// <summary>Ring entries that already had a vanilla shimmer result (left alone).</summary>
		public static int ShardRingSkipped;

		public override void Load() => Terraria.On_WorldGen.Chlorophyte += ChlorophyteDetour;
		public override void Unload() => Terraria.On_WorldGen.Chlorophyte -= ChlorophyteDetour;

		public override void PostSetupContent()
		{
			ShardRingSkipped = 0;
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.ShimmerDesertShards) return;
			for (int k = 0; k < ShardRing.Length; k++)
			{
				int from = ShardRing[k], to = ShardRing[(k + 1) % ShardRing.Length];
				if (ItemID.Sets.ShimmerTransformToItem[from] != -1) { ShardRingSkipped++; continue; }
				ItemID.Sets.ShimmerTransformToItem[from] = to;
			}
		}

		/// <summary>Vanilla density limits for Chlorophyte (WorldGen.Chlorophyte), scaled. At scale 1 this is identical to vanilla.</summary>
		static bool ChlorophyteDetour(Terraria.On_WorldGen.orig_Chlorophyte orig, int i, int j)
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || c.ChlorophyteChunkScale <= 1f) return orig(i, j);
			return Allowed(i, j, c.ChlorophyteChunkScale);
		}

		public static void Limits(int j, out int near, out int far, out int nearR, out int farR)
		{
			near = 40; far = 130; nearR = 35; farR = 85;
			if (Main.remixWorld) { near += 5; far *= 15; nearR -= 4; farR -= 8; }
			else if ((double)j < Main.rockLayer) { near /= 2; far /= 2; nearR = (int)(nearR * 1.5); farR = (int)(farR * 1.5); }
		}

		public static bool Allowed(int i, int j, float scale)
		{
			Limits(j, out int near, out int far, out int nearR, out int farR);
			var (nearCap, farCap) = ProgMath.ChlorophyteCaps(near, far, scale);
			int n = 0;
			for (int x = i - nearR; x < i + nearR; x++)
				for (int y = j - nearR; y < j + nearR; y++)
					if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Chlorophyte) n++;
			if (n > nearCap) return false;
			n = 0;
			for (int x = i - farR; x < i + farR; x++)
				for (int y = j - farR; y < j + farR; y++)
					if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Chlorophyte) n++;
			return n <= farCap;
		}
	}

	/// <summary>Extra Chlorophyte spawn and spread attempts, same rules and conditions as vanilla's hardmode tile update.</summary>
	public class ProgressionChlorophyteTiles : GlobalTile
	{
		public override void RandomUpdate(int i, int j, int type)
		{
			if (type != TileID.JungleGrass && type != TileID.Chlorophyte) return;
			var c = LevyProgressionConfig.Get;
			if (c == null || c.ChlorophyteGrowthMultiplier <= 1) return;
			if (!Main.hardMode || Main.tile[i, j].IsActuated) return;
			if (!((double)j > (Main.worldSurface + Main.rockLayer) / 2.0 || Main.remixWorld)) return;

			for (int n = 1; n < c.ChlorophyteGrowthMultiplier; n++)
			{
				if (type == TileID.JungleGrass && WorldGen.genRand.Next(300) == 0)
				{
					int x = i + WorldGen.genRand.Next(-10, 11), y = j + WorldGen.genRand.Next(-10, 11);
					if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Mud
						&& (!Main.tile[x, y - 1].HasTile || (Main.tile[x, y - 1].TileType != 5 && Main.tile[x, y - 1].TileType != 236 && Main.tile[x, y - 1].TileType != 238))
						&& WorldGen.Chlorophyte(x, y))
						Convert(x, y);
				}
				else if (type == TileID.Chlorophyte && WorldGen.genRand.Next(3) != 0)
				{
					int x = i, y = j;
					switch (WorldGen.genRand.Next(4)) { case 0: x++; break; case 1: x--; break; case 2: y++; break; default: y--; break; }
					if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile && (Main.tile[x, y].TileType == TileID.Mud || Main.tile[x, y].TileType == TileID.JungleGrass) && WorldGen.Chlorophyte(x, y))
						Convert(x, y);
				}
			}
		}

		static void Convert(int x, int y)
		{
			Main.tile[x, y].TileType = TileID.Chlorophyte;
			WorldGen.SquareTileFrame(x, y);
			if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, x, y);
		}
	}
}
