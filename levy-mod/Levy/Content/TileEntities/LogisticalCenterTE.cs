using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// Marks a Logistical Center so the armories can find it. It holds no data: which armory it serves and how many slots that adds are derived from where it stands
	/// (<see cref="LogisticsSystem"/>), so there is nothing to save or sync beyond the entity itself.
	/// </summary>
	public class LogisticalCenterTE : ModTileEntity
	{
		/// <summary>World position of the center's middle.</summary>
		public Vector2 Center => new Vector2((Position.X + LevyConstants.LogisticalCenterWidth * 0.5f) * 16f, (Position.Y + LevyConstants.LogisticalCenterHeight * 0.5f) * 16f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<LogisticalCenterTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.LogisticalCenterWidth, LevyConstants.LogisticalCenterHeight);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			LogisticsSystem.Invalidate();
			return Place(i, j);
		}

		public override void OnKill() => LogisticsSystem.Invalidate();
	}
}
