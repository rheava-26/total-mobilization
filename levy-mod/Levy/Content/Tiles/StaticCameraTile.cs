using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common.Observe;
using Levy.Content.Items.Observe;

namespace Levy.Content.Tiles
{
	/// <summary>A static camera on a tripod (2x3). Right-click to connect; the view follows nothing and moves nowhere. Art: placeholder at Content/Tiles/StaticCameraTile.png.</summary>
	public class StaticCameraTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
			TileObjectData.newTile.Width = StaticCameraTE.TilesWide;
			TileObjectData.newTile.Height = StaticCameraTE.TilesHigh;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.CoordinatePadding = 0; // the placeholder art is a plain 32x48 sheet
			TileObjectData.newTile.Origin = new Point16(0, 2);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide | AnchorType.Platform, StaticCameraTE.TilesWide, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<StaticCameraTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.Iron;
			RegisterItemDrop(ModContent.ItemType<StaticCameraItem>());
			AddMapEntry(new Color(80, 110, 150), CreateMapEntryName());
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<StaticCameraTE>().Kill(i, j);

		public override bool RightClick(int i, int j)
		{
			var te = TeAt(i, j);
			if (te == null) return false;
			ObserveSystem.ConnectCamera(te.ID);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player p = Main.LocalPlayer;
			p.noThrow = 2; p.cursorItemIconEnabled = true; p.cursorItemIconID = ModContent.ItemType<StaticCameraItem>();
		}

		/// <summary>The camera tile entity of the 2x3 tile containing (i, j).</summary>
		public static StaticCameraTE TeAt(int i, int j)
		{
			Tile t = Main.tile[i, j];
			int left = i - (t.TileFrameX / 16) % StaticCameraTE.TilesWide;
			int top = j - (t.TileFrameY / 16) % StaticCameraTE.TilesHigh;
			return TileEntity.ByPosition.TryGetValue(new Point16(left, top), out var te) ? te as StaticCameraTE : null;
		}
	}
}
