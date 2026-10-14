using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common;
using Levy.Common.UI;
using Levy.Common.Util;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>3x3 guardhouse. Right-click opens the Barracks panel. OWNED BY THE TILES AGENT (TileObjectData, tile entity hook, right-click, kill).</summary>
	public class BarracksTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
			TileID.Sets.PreventsTileHammeringIfOnTopOfIt[Type] = true;
			TileID.Sets.AvoidedByMeteorLanding[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
			TileObjectData.newTile.Width = LevyConstants.BarracksWidth;
			TileObjectData.newTile.Height = LevyConstants.BarracksHeight;
			TileObjectData.newTile.Origin = new Point16(1, 2);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, LevyConstants.BarracksWidth, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.WoodFurniture;
			AddMapEntry(new Color(150, 105, 60), CreateMapEntryName());
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// The placing item drops automatically (ModTile.GetItemDrops default); only the entity needs cleaning up.
			ModContent.GetInstance<BarracksTE>().Kill(i, j);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<BarracksItem>();
		}

		public override bool RightClick(int i, int j)
		{
			if (!TileEntity.TryGet(i, j, out BarracksTE te)) return true;
			if (Main.netMode == NetmodeID.Server) return true;

			if (!te.TooltipShown) {
				te.TooltipShown = true;
				Main.NewText(LevyUtil.T("UI.FirstTimeTip"), new Color(255, 240, 20));
				// Single player persists the flag through SaveData; on a multiplayer client it stays local (it is only a tooltip).
				if (Main.netMode == NetmodeID.SinglePlayer) te.MarkDirty();
			}

			UISystem.Instance?.OpenBarracks(te);
			return true;
		}
	}
}
