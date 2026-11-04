using Terraria;
using Terraria.ID;
using Levy.Common;

namespace Levy.Content.Items.Books
{
	/// <summary>
	/// Progression guide: unlocks the combat-drone garrison, one drone per spare ranged or magic weapon in a Barracks' Armory. Crafted at an Anvil from iron or lead bars and lenses, so it is within reach
	/// early on purpose: a drone's strength is the weapon it carries, so the whole game is worth having them. Consumed on use, once per world (see GuideSystem). Levy only records the unlock; the drones
	/// themselves are the Mechanization mod's (its DroneSystem reads <see cref="Common.Flavor.GuideSystem.Engineering"/>), so without that mod the book unlocks nothing.
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
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.Arsenal.EngineeringRecipeIron)
				.AddIngredient(ItemID.Lens, LevyConstants.Arsenal.EngineeringRecipeLens)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
