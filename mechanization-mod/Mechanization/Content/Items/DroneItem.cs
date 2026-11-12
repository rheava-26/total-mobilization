using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Mechanization.Common;
using Mechanization.Content.NPCs;
using F = Mechanization.Common.MechConstants.FieldDrone;

namespace Mechanization.Content.Items
{
	/// <summary>
	/// Deploys a combat drone at the cursor. The drone unfolds on its landing pad (the ground under the spot), takes off, patrols the box around it and shoots hostiles in reach of what it has mounted.
	/// It arms itself from the Armories in reach exactly as <see cref="TurretItem"/>'s turret does, and fires a plain bullet when it finds none. Craftable from wire, so the Mechanic gates it.
	///
	/// Placement is server-authoritative, as the turret's: single player places directly; a multiplayer client sends <see cref="MechPackets.SendPlaceDrone"/> and the server validates, places
	/// the drone and hands the item back if there turned out to be no room. <see cref="FieldDroneSystem"/> saves it with the world and packs it back up into this item.
	/// </summary>
	public class DroneItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 24; Item.maxStack = 9999;
			Item.useStyle = ItemUseStyleID.Swing; Item.useTime = 25; Item.useAnimation = 25; Item.useTurn = true;
			Item.autoReuse = false; Item.consumable = true;
			Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(gold: 1);
			Item.UseSound = SoundID.Item23; // rotors unfolding
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;   // only the using client decides the spot

			Vector2 spot = Main.MouseWorld;
			if (Main.netMode == NetmodeID.MultiplayerClient) { MechPackets.SendPlaceDrone(spot); return true; } // the server places it (and refunds the item if there is no room)

			string refusal = "";
			int idx = FieldDroneSystem.Instance?.TryPlace(new EntitySource_ItemUse(player, Item), spot, out refusal) ?? -1;
			if (idx < 0) Main.NewText(FieldDroneSystem.Instance?.RefusalText(string.IsNullOrEmpty(refusal) ? "Mods.Mechanization.UI.DroneNoRoom" : refusal) ?? Language.GetTextValue("Mods.Mechanization.UI.DroneNoRoom"), 255, 190, 110);
			return idx >= 0;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wire, F.RecipeWire)
				.AddRecipeGroup(RecipeGroupID.IronBar, F.RecipeBars)
				.AddIngredient(ItemID.Lens, F.RecipeLenses)
				.AddTile(TileID.TinkerersWorkbench)
				.Register();
		}
	}
}
