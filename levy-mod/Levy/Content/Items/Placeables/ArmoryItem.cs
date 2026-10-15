using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Content.Tiles;

namespace Levy.Content.Items.Placeables
{
	/// <summary>Placeable. OWNED BY THE TILES/ITEMS AGENT (recipe, tile link, tooltip).</summary>
	public class ArmoryItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<ArmoryTile>());
			Item.width = 28; Item.height = 28; Item.maxStack = Item.CommonMaxStack; Item.useTurn = true; Item.autoReuse = true;
			Item.useAnimation = 15; Item.useTime = 10; Item.useStyle = ItemUseStyleID.Swing; Item.consumable = true; Item.value = Item.buyPrice(silver: 50);
			Item.rare = ItemRarityID.Blue;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.ArmoryRecipeIron)
				.AddRecipeGroup(LevyConstants.RecipeGroupAnyChest, LevyConstants.ArmoryRecipeChests)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
