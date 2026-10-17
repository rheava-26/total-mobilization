using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.UI;

namespace Levy.Content.Items
{
	/// <summary>
	/// Craftable remote command set. Carrying one anywhere in the inventory makes every order from the orders radial go out
	/// map-wide (atBarracks: true) instead of being limited to soldiers within <c>OrderRadius</c>. Using it (left or right click)
	/// opens the radial from anywhere; the War Horn is untouched.
	/// </summary>
	public class CommandRadio : ModItem
	{
		public const int RecipeIron = 8, RecipeWood = 10, RecipeWire = 5;

		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 26; Item.useTime = 20; Item.useAnimation = 20; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.MenuTick; Item.value = Item.buyPrice(gold: 1); Item.rare = ItemRarityID.Green; Item.maxStack = 1; Item.noMelee = true;
			Item.autoReuse = false;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool? UseItem(Player player)
		{
			// Local client only; the radial itself sends the (map-wide) order packets.
			if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server) UISystem.Instance?.ToggleRadial();
			return true;
		}

		/// <summary>True when the player carries a Command Radio anywhere in the inventory (the held item is always in it).</summary>
		public static bool Has(Player player)
		{
			if (player == null) return false;
			int type = ModContent.ItemType<CommandRadio>();
			for (int i = 0; i < 58; i++) { var it = player.inventory[i]; if (it != null && !it.IsAir && it.type == type) return true; }
			return false;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, RecipeIron)
				.AddRecipeGroup(RecipeGroupID.Wood, RecipeWood)
				.AddIngredient(ItemID.Wire, RecipeWire)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
