using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Banners;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>Marks a Holding Banner so the army can find it (the TE is at the tile's top-left; the pole stands on the row under the last one).</summary>
	public class HoldingBannerTE : ModTileEntity
	{
		/// <summary>World position of the centre of the foot of the pole: where the banner's own row of ground is.</summary>
		public Vector2 Foot => new Vector2((Position.X + LevyConstants.Banner.Width * 0.5f) * 16f, (Position.Y + LevyConstants.Banner.Height) * 16f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<HoldingBannerTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.Banner.Width, LevyConstants.Banner.Height);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			int id = Place(i, j);
			BannerSystem.Changed();
			return id;
		}

		public override void OnKill() => BannerSystem.Changed();
	}
}
