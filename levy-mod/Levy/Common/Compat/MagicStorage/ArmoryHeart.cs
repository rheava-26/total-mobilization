#if MAGICSTORAGE
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;
using MagicStorage.Components;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	// Art: recoloured from Magic Storage's Storage Heart (MIT licence, see MAGICSTORAGE_LICENSE.txt in this folder). Sprites there by AdipemDragon.

	/// <summary>
	/// The Armory Heart: a Magic Storage COMPONENT (a 2x2 tile on Magic Storage's own <see cref="StorageComponent"/> base, with its own tile entity), so the network scanner, connectors, the security system and
	/// the network-recalculation netcode treat it like any other component. Placed beside an Armory (within the Logistical Center's reach) it joins that armory to whichever storage network its
	/// connectors reach: orders and buying draw from the network, and output that does not fit the armory goes into it.
	/// </summary>
	[ExtendsFromMod("MagicStorage")]
	[JITWhenModsEnabled("MagicStorage")]
	public class ArmoryHeartTile : StorageComponent
	{
		public override string Texture => "Levy/Common/Compat/MagicStorage/ArmoryHeartTile";
		public override string HighlightTexture => "Levy/Common/Compat/MagicStorage/ArmoryHeartTile_Highlight";

		public override ModTileEntity GetTileEntity() => ModContent.GetInstance<TEArmoryHeart>();
		public override int ItemType(int frameX, int frameY) => ModContent.ItemType<ArmoryHeartItem>();

		public override bool HasSmartInteract(int i, int j, Terraria.GameContent.ObjectInteractions.SmartInteractScanSettings settings) => true;

		private static TEArmoryHeart At(int i, int j)
		{
			if (Main.tile[i, j].TileFrameX % 36 == 18) i--;
			if (Main.tile[i, j].TileFrameY % 36 == 18) j--;
			return TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity te) ? te as TEArmoryHeart : null;
		}

		public override void MouseOver(int i, int j)
		{
			var player = Main.LocalPlayer; var tile = Main.tile[i, j];
			player.cursorItemIconEnabled = true; player.cursorItemIconID = ItemType(tile.TileFrameX, tile.TileFrameY); player.noThrow = 2;
			base.MouseOver(i, j);
		}

		/// <summary>Right-click: says whether the heart reaches a storage network and an armory, and opens that armory.</summary>
		public override bool RightClick(int i, int j)
		{
			var te = At(i, j); if (te == null) return false;
			var armory = te.Armory; var heart = te.GetHeart();
			Main.NewText(Terraria.Localization.Language.GetTextValue(heart == null ? "Mods.Levy.Logistics.HeartNoNetwork" : "Mods.Levy.Logistics.HeartNetworked", heart?.storageName ?? ""), 200, 220, 255);
			if (armory == null) Main.NewText(Terraria.Localization.Language.GetTextValue("Mods.Levy.Logistics.HeartNoArmory"), 220, 200, 120);
			else UISystem.Instance.OpenArmory(armory);
			return true;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			if (!TileDrawing.IsVisible(tile)) return;
			Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			Vector2 pos = zero + 16f * new Vector2(i, j) - Main.screenPosition;
			var frame = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);
			Color light = Lighting.GetColor(i, j, Color.White);
			spriteBatch.Draw(ModContent.Request<Texture2D>(Texture + "_Glow").Value, pos, frame, Color.Lerp(light, Color.White, Main.essScale));
		}
	}

	[ExtendsFromMod("MagicStorage")]
	[JITWhenModsEnabled("MagicStorage")]
	public class TEArmoryHeart : TEStorageComponent
	{
		public override bool ValidTile(in Tile tile) => TileLoader.GetTile(tile.TileType) is ArmoryHeartTile && tile.TileFrameX == 0 && tile.TileFrameY == 0;

		public Vector2 Center => new((Position.X + 1f) * 16f, (Position.Y + 1f) * 16f);

		/// <summary>The armory this heart serves: the nearest within a Logistical Center's reach.</summary>
		public ArmoryTE Armory => LogisticsSystem.ArmoryFor(Center);
	}

	[ExtendsFromMod("MagicStorage")]
	[JITWhenModsEnabled("MagicStorage")]
	public class ArmoryHeartItem : ModItem
	{
		public override string Texture => "Levy/Common/Compat/MagicStorage/ArmoryHeartItem";

		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<ArmoryHeartTile>());
			Item.width = 32; Item.height = 32; Item.maxStack = Item.CommonMaxStack; Item.value = Item.buyPrice(gold: 5); Item.rare = ItemRarityID.Pink;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<global::MagicStorage.Items.StorageComponent>(1)
				.AddIngredient<ArmoryItem>(1)
				.AddIngredient(ItemID.Ruby, 5)
				.AddIngredient(ItemID.Diamond, 1)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
#endif
