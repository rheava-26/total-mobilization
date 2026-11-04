using Terraria;
using Terraria.ID;
using Levy.Common;

namespace Levy.Content.Items.Books
{
	/// <summary>Progression guide: militia are mustered with more HP. Crafted at a Crystal Ball and Bookshelf from Life Fruit and Chlorophyte. Consumed on use, once per world (see GuideSystem).</summary>
	public class GuideToHealthcare : EventBook
	{
		public override string Key => "Healthcare";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Lime; Item.value = Item.sellPrice(gold: 8);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.LifeFruit, LevyConstants.HealthcareRecipeLifeFruit)
				.AddIngredient(ItemID.ChlorophyteBar, LevyConstants.HealthcareRecipeChlorophyte)
				.AddTile(TileID.CrystalBall)
				.AddTile(TileID.Bookcases)
				.Register();
		}
	}
}
