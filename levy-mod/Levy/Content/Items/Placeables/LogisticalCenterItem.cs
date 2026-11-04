using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Placeables
{
	/// <summary>Placeable. Adds storage slots to the nearest Armory (see LogisticsSystem); crafted from chests and iron.</summary>
	public class LogisticalCenterItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<LogisticalCenterTile>());
			Item.width = 30; Item.height = 30; Item.maxStack = Item.CommonMaxStack; Item.useTurn = true; Item.autoReuse = true;
			Item.useAnimation = 15; Item.useTime = 10; Item.useStyle = ItemUseStyleID.Swing; Item.consumable = true; Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Green;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(LevyConstants.RecipeGroupAnyChest, LevyConstants.LogisticalCenterRecipeChests)
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.LogisticalCenterRecipeIron)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
