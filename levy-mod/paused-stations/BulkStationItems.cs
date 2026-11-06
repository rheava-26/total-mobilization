using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Progression;

namespace Levy.Content.Tiles
{
	/// <summary>Places the Bulk Factory. Placeholder sprite: Content/Tiles/BulkFactoryItem.png.</summary>
	public class BulkFactoryItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<BulkFactoryTile>());
			Item.width = 32; Item.height = 32; Item.maxStack = Item.CommonMaxStack;
			Item.value = Item.buyPrice(gold: 5); Item.rare = ItemRarityID.Green;
		}

		public override void AddRecipes()
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.EnableBulkStations) return;
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 25)
				.AddIngredient(ItemID.Chain, 5)
				.AddIngredient(ItemID.Wire, 20)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	/// <summary>Places the Bulk Potion Station. Placeholder sprite: Content/Tiles/BulkPotionStationItem.png.</summary>
	public class BulkPotionStationItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<BulkPotionStationTile>());
			Item.width = 32; Item.height = 32; Item.maxStack = Item.CommonMaxStack;
			Item.value = Item.buyPrice(gold: 5); Item.rare = ItemRarityID.Green;
		}

		public override void AddRecipes()
		{
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.EnableBulkStations) return;
			CreateRecipe()
				.AddIngredient(ItemID.AlchemyTable, 1)
				.AddIngredient(ItemID.Bottle, 15)
				.AddIngredient(ItemID.Glass, 10)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
