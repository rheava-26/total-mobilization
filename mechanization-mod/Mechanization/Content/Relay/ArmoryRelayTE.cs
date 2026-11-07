using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.TileEntities;

namespace Mechanization.Content.Relay
{
	/// <summary>Marks a placed Armory Relay so it can be enumerated. Holds no data; the link is worked out from the live Armories.</summary>
	public class ArmoryRelayTE : ModTileEntity
	{
		/// <summary>World position of the relay's middle (the 2x2 tile's centre).</summary>
		public Vector2 Center => new Vector2((Position.X + 1f) * 16f, (Position.Y + 1f) * 16f);

		private ArmoryTE _linked; private uint _stamp = uint.MaxValue;

		/// <summary>The Armory this relay is linked to, re-resolved twice a second (a client-side read; tile entities sync). Null when none.</summary>
		public ArmoryTE Linked
		{
			get
			{
				uint now = Main.GameUpdateCount;
				if (_stamp == uint.MaxValue || now - _stamp >= 30 || now < _stamp) { _stamp = now; _linked = ArmoryRelays.LinkOf(this); }
				return _linked;
			}
		}

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.HasTile && t.TileType == ModContent.TileType<ArmoryRelayTile>() && t.TileFrameX == 0 && t.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				NetMessage.SendTileSquare(Main.myPlayer, i, j, 2, 2);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			return Place(i, j);
		}
	}
}
