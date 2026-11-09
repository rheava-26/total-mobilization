using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Vanity;

namespace Levy.Content.Items
{
	/// <summary>
	/// Vanity accessory. While it is worn (accessory slot or vanity slot) this client draws every levy, its held weapon, its projectiles, its summoned units and its officers at 15% opacity
	/// (<see cref="LevyConstants.LensOpacity"/>), so a boss fight is easy to read through the army. Draw-only: see <see cref="LensDraw"/>. Art: SpectralLens.png.
	/// </summary>
	public class SpectralLens : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 20; Item.maxStack = 1;
			Item.accessory = true; Item.vanity = true; Item.hasVanityEffects = true;
			Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 50);
		}

		public override void UpdateAccessory(Player player, bool hideVisual) => player.GetModPlayer<SpectralLensPlayer>().Worn = true;

		public override void UpdateVanity(Player player) => player.GetModPlayer<SpectralLensPlayer>().Worn = true;

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(LevyConstants.RecipeGroupAnyGrave, 1)
				.AddIngredient(ItemID.InvisibilityPotion, LevyConstants.LensRecipePotions)
				.AddIngredient(ItemID.Goggles, 1)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
