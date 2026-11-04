using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Mechanization.Common;
using Mechanization.Content.NPCs;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.Items
{
	/// <summary>
	/// Deploys a <see cref="TurretNPC"/> at the cursor. The turret is set down on the ground at the spot (a spot buried in terrain is lifted to open air; with no room the item
	/// is not used up) and begins gunning down hostiles in range. Craftable from cheap metal at a workbench.
	///
	/// Placement is server-authoritative: single player places it directly; a multiplayer client sends a request packet (<see cref="MechPackets.SendPlace"/>) and the server
	/// validates it, places the turret, and hands the item back if there turned out to be no room.
	/// </summary>
	public class TurretItem : ModItem
	{
		// A valid vanilla icon so no new art is needed.
		public override string Texture => "Terraria/Images/Item_" + ItemID.Cannon;

		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 24; Item.maxStack = 9999;
			Item.useStyle = ItemUseStyleID.Swing; Item.useTime = 25; Item.useAnimation = 25; Item.useTurn = true;
			Item.autoReuse = false; Item.consumable = true;
			Item.rare = ItemRarityID.Green; Item.value = Item.sellPrice(silver: 20);
			Item.UseSound = SoundID.Item44; // a heavy mechanical clunk
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;   // only the using client decides the spot

			Vector2 spot = Main.MouseWorld;
			if (Main.netMode == NetmodeID.MultiplayerClient) { MechPackets.SendPlace(spot); return true; } // the server places it (and refunds the item if there is no room)

			// TurretSystem settles the turret onto the ground AND registers it in the world save, so it survives a reload; a destroyed turret is dropped from that set.
			int idx = TurretSystem.Instance?.TryPlace(new EntitySource_ItemUse(player, Item), spot) ?? -1;
			if (idx < 0) Main.NewText(Language.GetTextValue("Mods.Mechanization.UI.TurretNoRoom"), 255, 190, 110);
			return idx >= 0;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddRecipeGroup(RecipeGroupID.Wood, 20)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
