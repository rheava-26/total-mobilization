using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Mechanization.Content.NPCs;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.Items
{
	/// <summary>
	/// Deploys a <see cref="TurretNPC"/> at the cursor. The turret drops onto the terrain below the spot and begins
	/// gunning down hostiles in range. Craftable from cheap metal at a workbench for testing.
	///
	/// v0 placement is single-player / server-authoritative: a dedicated-multiplayer CLIENT cannot spawn an authoritative
	/// NPC without a spawn packet, which is a deferred next-phase item, so the item is simply inert on an MP client.
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

		/// <summary>v0: only single player / server may place (an MP client has no authoritative spawn yet). Blocking use here also keeps the item from being consumed with no effect.</summary>
		public override bool CanUseItem(Player player) => Main.netMode != NetmodeID.MultiplayerClient;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;   // only the using client decides the spot
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;

			Vector2 spot = Main.MouseWorld;
			// TurretSystem spawns the turret AND registers it in the world save, so it survives a reload; a destroyed turret is dropped from that set.
			int idx = TurretSystem.Instance?.PlaceNew(new EntitySource_ItemUse(player, Item), spot) ?? -1;
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
