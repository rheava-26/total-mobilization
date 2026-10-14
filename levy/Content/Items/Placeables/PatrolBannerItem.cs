using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Placeables
{
	/// <summary>Placeable. OWNED BY THE TILES/ITEMS AGENT (recipe, tile link, tooltip).</summary>
	public class PatrolBannerItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<PatrolBannerTile>());
			Item.width = 28; Item.height = 28; Item.maxStack = Item.CommonMaxStack; Item.useTurn = true; Item.autoReuse = true;
			Item.useAnimation = 15; Item.useTime = 10; Item.useStyle = ItemUseStyleID.Swing; Item.consumable = true; Item.value = Item.buyPrice(silver: 10);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, LevyConstants.PatrolBannerRecipeWood)
				.AddIngredient(ItemID.Silk, LevyConstants.PatrolBannerRecipeSilk)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
