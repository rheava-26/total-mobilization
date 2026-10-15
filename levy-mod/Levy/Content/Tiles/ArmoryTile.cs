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
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>2x3 weapon racks. Right-click opens the Armory panel; breaking it spills every stored item. OWNED BY THE TILES AGENT.</summary>
	public class ArmoryTile : ModTile
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

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
			TileObjectData.newTile.Width = LevyConstants.ArmoryWidth;
			TileObjectData.newTile.Height = LevyConstants.ArmoryHeight;
			TileObjectData.newTile.Origin = new Point16(0, 2);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, LevyConstants.ArmoryWidth, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.Iron;
			AddMapEntry(new Color(120, 120, 135), CreateMapEntryName());
		}

		public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Spill the contents first (server / single player; clients hold only a mirror), then remove the entity.
			// The placing item itself drops automatically through ModTile.GetItemDrops.
			if (TileEntity.TryGet(i, j, out ArmoryTE te)) te.DropAllContents();
			ModContent.GetInstance<ArmoryTE>().Kill(i, j);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<ArmoryItem>();
		}

		public override bool RightClick(int i, int j)
		{
			if (!TileEntity.TryGet(i, j, out ArmoryTE te)) return true;
			if (Main.netMode == NetmodeID.Server) return true;
			UISystem.Instance?.OpenArmory(te);
			return true;
		}
	}
}
