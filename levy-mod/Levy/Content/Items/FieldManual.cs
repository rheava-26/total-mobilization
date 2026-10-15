using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.UI;

namespace Levy.Content.Items
{
	/// <summary>A readable field manual: how to raise a militia and an army. Using it opens the guide as a scrollable panel. Pure client-side; no state, no packets.</summary>
	public class FieldManual : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 22; Item.height = 26; Item.maxStack = 1; Item.rare = ItemRarityID.Blue;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 24; Item.useAnimation = 24; Item.useTurn = true;
			Item.autoReuse = false; Item.consumable = false; Item.value = Item.sellPrice(copper: 50);
			Item.UseSound = SoundID.Item10; // page turn
		}

		public override bool? UseItem(Player player)
		{
			// The manual is read on the local client only; the server runs UseItem for remote players but must stay silent.
			if (Main.dedServ || player.whoAmI != Main.myPlayer) return true;
			UISystem.Instance?.OpenManual();
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe().AddRecipeGroup(RecipeGroupID.Wood, 5).AddTile(TileID.WorkBenches).Register();
		}
	}
}
