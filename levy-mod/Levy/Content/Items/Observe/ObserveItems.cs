using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Observe
{
	/// <summary>
	/// A body camera. Put it in the Armory or hand it to a levy's accessory slot: a levy that carries one is watchable. Use it to watch the next GoPro levy through their eyes; right-click or Esc to stop.
	/// It is an accessory so the Armory issues it like any other; it adds no stats. Art: Content/Items/Observe/GoProItem.png (and GoProIndicator.png over a carrier).
	/// </summary>
	public class GoProItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 24; Item.useTime = 20; Item.useAnimation = 20; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.MenuTick; Item.value = Item.buyPrice(gold: 2); Item.rare = ItemRarityID.Green; Item.maxStack = 1; Item.noMelee = true;
			Item.accessory = true;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer || Main.dedServ) return true;
			if (player.altFunctionUse == 2) ObserveSystem.Exit(); else ObserveSystem.ConnectLevy(next: true);
			return true;
		}

		/// <summary>True when the soldier's gear has a GoPro in an accessory slot.</summary>
		public static bool Carries(SoldierGear g)
		{
			if (g?.Accessories == null) return false;
			int t = ModContent.ItemType<GoProItem>();
			foreach (var a in g.Accessories) if (a != null && !a.IsAir && a.type == t) return true;
			return false;
		}

		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Lens, 1).AddRecipeGroup(RecipeGroupID.IronBar, 3).AddIngredient(ItemID.Wire, 5).AddTile(TileID.WorkBenches).Register();
		}
	}

	/// <summary>Places a static camera.</summary>
	public class StaticCameraItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<StaticCameraTile>());
			Item.width = 32; Item.height = 32; Item.maxStack = Item.CommonMaxStack; Item.value = Item.buyPrice(gold: 5); Item.rare = ItemRarityID.Green;
		}

		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Lens, 2).AddRecipeGroup(RecipeGroupID.IronBar, 6).AddIngredient(ItemID.Wire, 10).AddRecipeGroup(RecipeGroupID.Wood, 4).AddTile(TileID.Anvils).Register();
		}
	}

	/// <summary>Direct control of one levy at any time (the same control a dead player gets until they respawn). Use to take the levy nearest the cursor or the next one; right-click to let go.</summary>
	public class MindLinkItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 28; Item.useTime = 20; Item.useAnimation = 20; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item4; Item.value = Item.buyPrice(gold: 8); Item.rare = ItemRarityID.Lime; Item.maxStack = 1; Item.noMelee = true;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer || Main.dedServ) return true;
			if (player.altFunctionUse == 2) MindControl.RequestEnd(); else MindControl.RequestFromItem(next: MindControl.LocalSoldierId >= 0);
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Lens, 2).AddIngredient(ItemID.Wire, 10).AddIngredient(ItemID.Silk, 5).AddRecipeGroup(RecipeGroupID.IronBar, 4).AddTile(TileID.Anvils).Register();
		}
	}
}
