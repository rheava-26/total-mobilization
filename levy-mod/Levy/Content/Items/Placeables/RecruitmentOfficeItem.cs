using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Placeables
{
	/// <summary>Placeable. Work Bench: an Enchanted Sundial, wood, iron and silk.</summary>
	public class RecruitmentOfficeItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<RecruitmentOfficeTile>());
			Item.width = 28; Item.height = 28; Item.maxStack = Item.CommonMaxStack; Item.useTurn = true; Item.autoReuse = true;
			Item.useAnimation = 15; Item.useTime = 10; Item.useStyle = ItemUseStyleID.Swing; Item.consumable = true; Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Green;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Sundial)
				.AddRecipeGroup(RecipeGroupID.Wood, LevyConstants.RecruitmentOfficeRecipeWood)
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.RecruitmentOfficeRecipeIron)
				.AddIngredient(ItemID.Silk, LevyConstants.RecruitmentOfficeRecipeSilk)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
