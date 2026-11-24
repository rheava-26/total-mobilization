using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>Marks a Recruitment Office so a Barracks can find it (at the tile's top-left). It holds no state: its presence in a settlement, or on the pylon network, is what unlocks the rush recruiting drive.</summary>
	public class RecruitmentOfficeTE : ModTileEntity
	{
		public Vector2 Center => new Vector2((Position.X + LevyConstants.RecruitmentOfficeWidth * 0.5f) * 16f, (Position.Y + LevyConstants.RecruitmentOfficeHeight * 0.5f) * 16f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<RecruitmentOfficeTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.RecruitmentOfficeWidth, LevyConstants.RecruitmentOfficeHeight);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			return Place(i, j);
		}
	}
}
