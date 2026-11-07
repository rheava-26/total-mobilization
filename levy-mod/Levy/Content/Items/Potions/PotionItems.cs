using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Potions;
using Levy.Common.Util;
using Levy.Content.Buffs;

namespace Levy.Content.Items.Potions
{
	/// <summary>Spectator Potion: 20 minutes of watching. Rules in <see cref="SpectatorPlayer"/>. Cannot be drunk within 5 seconds of taking damage.</summary>
	public class SpectatorPotion : ModItem
	{
		public override string LocalizationCategory => "Potions";

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 20; }

		public override void SetDefaults()
		{
			Item.width = 24; Item.height = 30; Item.maxStack = Item.CommonMaxStack; Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid; Item.useTime = 17; Item.useAnimation = 17; Item.UseSound = SoundID.Item3; Item.useTurn = true;
			Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(silver: 2);
			Item.buffType = ModContent.BuffType<SpectatorBuff>(); Item.buffTime = PotionConstants.SpectatorTicks;
		}

		public override bool CanUseItem(Player player)
		{
			if (player.dead) return false;
			if (global::Levy.Common.Targeting.EnemyTargeting.FindUndownedBoss() != null)
			{
				if (player.whoAmI == Main.myPlayer && !Main.dedServ) CombatText.NewText(player.getRect(), new Color(255, 120, 100), LevyUtil.T("Potions.SpectatorBossRefused"));
				return false;
			}
			if (player.TryGetModPlayer(out SpectatorPlayer sp) && sp.RecentlyHurt)
			{
				if (player.whoAmI == Main.myPlayer && !Main.dedServ) CombatText.NewText(player.getRect(), new Color(255, 120, 100), LevyUtil.T("Potions.SpectatorRefused"));
				return false;
			}
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Shiverthorn).AddIngredient(ItemID.Blinkroot).AddIngredient(ItemID.Lens)
				.AddIngredient(ItemID.InvisibilityPotion).AddIngredient(ItemID.BottledWater)
				.AddTile(TileID.Bottles).Register();
		}
	}

	/// <summary>Seeker Potion: 12 minutes of finding one chosen item. Drinking it opens the chooser (see <see cref="SeekerSystem"/>).</summary>
	public class SeekerPotion : ModItem
	{
		public override string LocalizationCategory => "Potions";

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 20; }

		public override void SetDefaults()
		{
			Item.width = 24; Item.height = 30; Item.maxStack = Item.CommonMaxStack; Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid; Item.useTime = 17; Item.useAnimation = 17; Item.UseSound = SoundID.Item3; Item.useTurn = true;
			Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(silver: 2);
			Item.buffType = ModContent.BuffType<SeekerBuff>(); Item.buffTime = PotionConstants.SeekerTicks;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer && !Main.dedServ) SeekerSystem.BeginChoose(player);
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Moonglow).AddIngredient(ItemID.Blinkroot).AddIngredient(ItemID.SpelunkerPotion).AddIngredient(ItemID.BottledWater)
				.AddTile(TileID.Bottles).Register();
			foreach (int ore in new[] { ItemID.GoldOre, ItemID.PlatinumOre })
				CreateRecipe()
					.AddIngredient(ItemID.Moonglow).AddIngredient(ItemID.Blinkroot).AddIngredient(ore).AddIngredient(ItemID.BottledWater)
					.AddTile(TileID.Bottles).Register();
		}
	}
}
