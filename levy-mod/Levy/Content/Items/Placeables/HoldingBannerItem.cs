using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Placeables
{
	/// <summary>Placeable war banner. Work Bench: wood and silk.</summary>
	public class HoldingBannerItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<HoldingBannerTile>());
			Item.width = 28; Item.height = 28; Item.maxStack = Item.CommonMaxStack; Item.useTurn = true; Item.autoReuse = true;
			Item.useAnimation = 15; Item.useTime = 10; Item.useStyle = ItemUseStyleID.Swing; Item.consumable = true; Item.value = Item.buyPrice(silver: 20);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, LevyConstants.Banner.RecipeWood)
				.AddIngredient(ItemID.Silk, LevyConstants.Banner.RecipeSilk)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
