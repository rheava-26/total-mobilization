using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>2x4 war banner on a pole. Stands on the ground, a platform or a table; any number can be placed. Levies on the Hold the Banners order stand around the nearest one.</summary>
	public class HoldingBannerTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
			TileObjectData.newTile.Width = LevyConstants.Banner.Width;
			TileObjectData.newTile.Height = LevyConstants.Banner.Height;
			TileObjectData.newTile.Origin = new Point16(0, LevyConstants.Banner.Height - 1);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16 };
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, LevyConstants.Banner.Width, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<HoldingBannerTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.Silk;
			AddMapEntry(new Color(130, 13, 15), CreateMapEntryName());
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			ModContent.GetInstance<HoldingBannerTE>().Kill(i, j);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<HoldingBannerItem>();
		}
	}
}
