using Terraria;
using Terraria.ID;
using Levy.Common;

namespace Levy.Content.Items.Books
{
	/// <summary>
	/// Progression guide: each Barracks garrisons combat drones, one per spare ranged or magic weapon in its Armory. Crafted at an Anvil from iron or lead bars and lenses, so it is within reach
	/// early on purpose: a drone's strength is the weapon it carries, so the whole game is worth having them. Consumed on use, once per world (see GuideSystem; the garrison is DroneSystem).
	/// </summary>
	public class GuideToEngineering : EventBook
	{
		public override string Key => "Engineering";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 50);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.Drones.EngineeringRecipeIron)
				.AddIngredient(ItemID.Lens, LevyConstants.Drones.EngineeringRecipeLens)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
