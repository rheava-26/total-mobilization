using Terraria;
using Terraria.ID;
using Levy.Common;

namespace Levy.Content.Items.Books
{
	/// <summary>Progression guide: militia are mustered with more mana. Crafted at a Crystal Ball and Bookshelf from the mechanical boss souls. Consumed on use, once per world (see GuideSystem).</summary>
	public class GuideToTheArcaneArts : EventBook
	{
		public override string Key => "TheArcaneArts";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Pink; Item.value = Item.sellPrice(gold: 8);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.SoulofMight, LevyConstants.ArcaneRecipeSouls)
				.AddIngredient(ItemID.SoulofSight, LevyConstants.ArcaneRecipeSouls)
				.AddIngredient(ItemID.SoulofFright, LevyConstants.ArcaneRecipeSouls)
				.AddTile(TileID.CrystalBall)
				.AddTile(TileID.Bookcases)
				.Register();
		}
	}
}
