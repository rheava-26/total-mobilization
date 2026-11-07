using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mechanization.Content.Relay
{
	/// <summary>Places an Armory Relay. Iron or lead bars, copper wire and a chest at a Work Bench.</summary>
	public class ArmoryRelayItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<ArmoryRelayTile>());
			Item.width = 28; Item.height = 30; Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.Green; Item.value = Item.buyPrice(gold: 1);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddIngredient(ItemID.Wire, 20)
				.AddIngredient(ItemID.Chest, 1)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
