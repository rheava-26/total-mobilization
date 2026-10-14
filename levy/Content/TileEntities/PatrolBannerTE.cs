using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>Marks a patrol banner so routes can be enumerated. Two banners define a patrol line.</summary>
	public class PatrolBannerTE : ModTileEntity
	{
		/// <summary>World position of the banner's bottom tile centre (where a patrolling soldier stands).</summary>
		public Vector2 Center => new Vector2((Position.X + 0.5f) * 16f, (Position.Y + LevyConstants.PatrolBannerHeight) * 16f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<PatrolBannerTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.PatrolBannerWidth, LevyConstants.PatrolBannerHeight);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			return Place(i, j);
		}
	}
}
