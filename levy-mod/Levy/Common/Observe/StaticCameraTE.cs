using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using Levy.Common.Util;
using Levy.Content.Tiles;

namespace Levy.Common.Observe
{
	/// <summary>A placed static camera. The tile entity gives every camera a stable id (so a player can connect to it from anywhere) and keeps its name. Position is the top-left tile of the 2x3 camera.</summary>
	public class StaticCameraTE : ModTileEntity
	{
		public const int TilesWide = 2, TilesHigh = 3;
		/// <summary>Player-given name; empty means the default "Camera N".</summary>
		public string Label = "";

		public string Display => string.IsNullOrEmpty(Label) ? LevyUtil.T("Observe.CameraName", ID) : Label;
		/// <summary>World point a connected player sees from: the lens, near the top of the tripod.</summary>
		public Vector2 ViewCenter => new Vector2(Position.X * 16f + 16f, Position.Y * 16f + 14f);

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.HasTile && t.TileType == ModContent.TileType<StaticCameraTile>() && t.TileFrameX == 0 && t.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			Point16 origin = TileObjectData.GetTileData(type, style, alternate).Origin;
			int left = i - origin.X, top = j - origin.Y;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				NetMessage.SendTileSquare(Main.myPlayer, left, top, TilesWide, TilesHigh);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: left, number2: top, number3: Type);
				return -1;
			}
			return Place(left, top);
		}

		public override void SaveData(TagCompound tag) { if (!string.IsNullOrEmpty(Label)) tag["label"] = Label; }
		public override void LoadData(TagCompound tag) { Label = tag.GetString("label"); }
		public override void NetSend(BinaryWriter writer) => writer.Write(Label ?? "");
		public override void NetReceive(BinaryReader reader) => Label = reader.ReadString();

		/// <summary>Every camera the local world knows (single player and the server: all of them; a client: only those in sections it has loaded).</summary>
		public static List<StaticCameraTE> All()
		{
			var list = new List<StaticCameraTE>();
			foreach (var te in ByID.Values) if (te is StaticCameraTE c) list.Add(c);
			list.Sort((a, b) => a.ID.CompareTo(b.ID));
			return list;
		}

		public static StaticCameraTE Get(int id) => ByID.TryGetValue(id, out var te) ? te as StaticCameraTE : null;
	}
}
