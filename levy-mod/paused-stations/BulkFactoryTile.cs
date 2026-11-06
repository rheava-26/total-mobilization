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
	/// 3x2 production line. Stand near it and craft with a batch size picked by right-clicking: one click makes the whole batch,
	/// and batches of the configured size or more yield extra (see <see cref="BulkStations"/>). Placeholder sprite: Content/Tiles/BulkFactoryTile.png.
	/// </summary>
	public class BulkFactoryTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.addTile(Type);

			DustType = DustID.Iron;
			AddMapEntry(new Color(120, 120, 130), CreateMapEntryName());
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<BulkFactoryItem>();
		}

		public override bool RightClick(int i, int j)
		{
			if (Main.netMode == NetmodeID.Server) return true;
			var pp = Main.LocalPlayer.GetModPlayer<ProgressionPlayer>();
			pp.FactoryBatchIndex = (pp.FactoryBatchIndex + 1) % ProgConst.FactoryBatches.Length;
			int batch = ProgConst.FactoryBatches[pp.FactoryBatchIndex];
			var c = LevyProgressionConfig.Get;
			Main.NewText(batch <= 1
				? LevyUtil.T("Progression.FactoryOff")
				: LevyUtil.T("Progression.FactoryBatch", batch, c.FactoryMinCrafts, c.FactoryBonusPercent), ProgressionColors.Note);
			return true;
		}
	}
}
