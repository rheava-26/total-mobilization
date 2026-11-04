using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>
	/// 3x2 stack of supply crates. It adds storage slots to the nearest Armory (see <see cref="LogisticsSystem"/>). Mining it is refused while the armory holds items in the slots it provides
	/// (use Sort to pack them forward first), so a break can never strand gear.
	/// </summary>
	public class LogisticalCenterTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileID.Sets.AvoidedByMeteorLanding[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Width = LevyConstants.LogisticalCenterWidth;
			TileObjectData.newTile.Height = LevyConstants.LogisticalCenterHeight;
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, LevyConstants.LogisticalCenterWidth, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<LogisticalCenterTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.WoodFurniture;
			AddMapEntry(new Color(150, 105, 60), CreateMapEntryName());
		}

		public override bool CanKillTile(int i, int j, ref bool blockDamaged)
		{
			Tile t = Main.tile[i, j];
			int left = i - t.TileFrameX / 16, top = j - t.TileFrameY / 16;
			if (!TileEntity.TryGet(left, top, out LogisticalCenterTE te)) return true;
			ArmoryTE armory = LogisticsSystem.ArmoryFor(te.Center);
			if (armory == null) return true;
			// With this center gone the armory has one fewer center's worth of slots: refuse while an item sits beyond them.
			if (armory.StorageUsedLength > ArmoryTE.CapacityFor(armory.LinkedCenters - 1))
			{
				if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 40 == 0)
					Main.NewText(LevyUtil.T("Chat.LogisticalCenterInUse", LevyConstants.LogisticsSlotsPerCenter), LevyConstants.EventChatColor);
				blockDamaged = false;
				return false;
			}
			return true;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			ModContent.GetInstance<LogisticalCenterTE>().Kill(i, j);
			LogisticsSystem.Invalidate();
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<LogisticalCenterItem>();
		}
	}
}
