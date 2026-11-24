using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>3x2 desk with a sundial and a ledger. Standing in a settlement, or anywhere on the pylon network, it lets that Barracks run a rush recruiting drive.</summary>
	public class RecruitmentOfficeTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Width = LevyConstants.RecruitmentOfficeWidth;
			TileObjectData.newTile.Height = LevyConstants.RecruitmentOfficeHeight;
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, LevyConstants.RecruitmentOfficeWidth, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<RecruitmentOfficeTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.WoodFurniture;
			AddMapEntry(new Color(150, 105, 60), CreateMapEntryName());
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override bool RightClick(int i, int j)
		{
			if (Main.netMode == NetmodeID.Server) return true;
			Tile t = Main.tile[i, j];
			int left = i - t.TileFrameX / 18, top = j - t.TileFrameY / 18;
			if (TileEntity.TryGet(left, top, out RecruitmentOfficeTE te)) global::Levy.Common.UI.UIActions.RushDrive(te);
			return true;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			ModContent.GetInstance<RecruitmentOfficeTE>().Kill(i, j);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<RecruitmentOfficeItem>();
		}
	}
}
