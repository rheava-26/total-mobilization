using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common.Progression;
using Levy.Common.Util;

namespace Levy.Content.Tiles
{
	/// <summary>
	/// 2x2 brewing vat. Counts as an Alchemy Table and Bottles. Right-click picks a batch size; one click brews the whole batch of potions,
	/// and batches of the configured size or more yield extra. Placeholder sprite: Content/Tiles/BulkPotionStationTile.png.
	/// </summary>
	public class BulkPotionStationTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileID.Sets.DisableSmartCursor[Type] = true;
			AdjTiles = new[] { (int)TileID.AlchemyTable, (int)TileID.Bottles };

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Origin = new Point16(0, 1);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.addTile(Type);

			DustType = DustID.Glass;
			AddMapEntry(new Color(110, 60, 150), CreateMapEntryName());
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<BulkPotionStationItem>();
		}

		public override bool RightClick(int i, int j)
		{
			if (Main.netMode == NetmodeID.Server) return true;
			var pp = Main.LocalPlayer.GetModPlayer<ProgressionPlayer>();
			pp.PotionBatchIndex = (pp.PotionBatchIndex + 1) % ProgConst.PotionBatches.Length;
			int batch = ProgConst.PotionBatches[pp.PotionBatchIndex];
			var c = LevyProgressionConfig.Get;
			Main.NewText(batch <= 1
				? LevyUtil.T("Progression.PotionOff")
				: LevyUtil.T("Progression.PotionBatch", batch, c.PotionMinCrafts, c.PotionBonusPercent), ProgressionColors.Note);
			return true;
		}
	}
}
