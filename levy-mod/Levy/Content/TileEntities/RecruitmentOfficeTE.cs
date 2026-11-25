using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// A Recruitment Office (at the tile's top-left). Standing in a settlement, or on the pylon network, it runs a rush recruiting drive for what it serves, then rests for one in-game day (<see cref="LevyConstants.RecruitmentOfficeRestTicks"/>).
	/// The rest is held as a point on the <see cref="WorldClock"/>; the world save and the net message carry the time left.
	/// </summary>
	public class RecruitmentOfficeTE : ModTileEntity
	{
		/// <summary>The <see cref="WorldClock.Now"/> at which this Office can run a drive again. At or before now: ready.</summary>
		public double ReadyAt;

		/// <summary>In-game ticks until this Office is ready again; 0 when it is.</summary>
		public double RestTicks => Math.Max(0.0, ReadyAt - WorldClock.Now);

		/// <summary>Starts the rest: ready again one in-game day from now.</summary>
		public void StartRest() => ReadyAt = WorldClock.Now + LevyConstants.RecruitmentOfficeRestTicks;

		public override void SaveData(TagCompound tag) { if (RestTicks > 0) tag["rest"] = (int)Math.Ceiling(RestTicks); }
		public override void LoadData(TagCompound tag) { ReadyAt = WorldClock.Now + (tag.ContainsKey("rest") ? tag.GetInt("rest") : 0); }
		public override void NetSend(BinaryWriter writer) => writer.Write((int)Math.Ceiling(RestTicks));
		public override void NetReceive(BinaryReader reader) { ReadyAt = WorldClock.Now + reader.ReadInt32(); }

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
