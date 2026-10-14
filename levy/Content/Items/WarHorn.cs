using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Content.Items
{
	/// <summary>Left-click: mobilize / stand down the nearest settlement. Right-click: orders radial. OWNED BY THE ITEMS AGENT (UI agent owns the radial).</summary>
	public class WarHorn : ModItem
	{
		/// <summary>A long brass call: the vanilla roar pitched up and softened.</summary>
		public static readonly SoundStyle HornSound = SoundID.Roar with { Pitch = 0.55f, PitchVariance = 0.05f, Volume = 0.8f };

		private static uint _lastHintTick;

		public override void SetDefaults()
		{
			Item.width = 30; Item.height = 26; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = null; Item.value = Item.buyPrice(silver: 50); Item.rare = ItemRarityID.Blue; Item.maxStack = 1; Item.noMelee = true;
			Item.autoReuse = false;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			if (player.altFunctionUse == 2) return true;
			if (Nearest(player) != null) return true;
			// No settlement in reach: a short hint for the local player, at most once per 5 s.
			if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server && Main.GameUpdateCount - _lastHintTick > (uint)LevyConstants.HornHintCooldownTicks) {
				_lastHintTick = Main.GameUpdateCount;
				Main.NewText(LevyUtil.T("Tiles.HornNoBarracks"), new Color(255, 200, 100));
			}
			return false;
		}

		public override bool? UseItem(Player player)
		{
			// Right-click: the orders radial, local client only, no muster.
			if (player.altFunctionUse == 2) {
				if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server) UISystem.Instance?.ToggleRadial();
				return true;
			}

			if (Main.netMode != NetmodeID.Server) SoundEngine.PlaySound(HornSound, player.Center);
			if (player.whoAmI != Main.myPlayer) return true;

			BarracksTE te = Nearest(player);
			if (te == null) return false;

			if (Main.netMode == NetmodeID.MultiplayerClient) {
				var packet = LevyPackets.New(te.Mobilized ? LevyPacket.StandDown : LevyPacket.Mobilize);
				packet.Write(te.ID);
				packet.Send();
			}
			else {
				var settlements = SettlementSystem.Instance;
				if (te.Mobilized) settlements.StandDown(te);
				else settlements.Mobilize(te, player);
			}
			return true;
		}

		private static BarracksTE Nearest(Player player) => SettlementSystem.Instance?.NearestBarracks(player.Center, LevyConstants.HornRangeTiles);

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, LevyConstants.WarHornRecipeWood)
				.AddRecipeGroup(RecipeGroupID.IronBar, LevyConstants.WarHornRecipeIron)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
