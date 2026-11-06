using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Events;
using Levy.Content.Projectiles;

namespace Levy.Content.Items.OOA
{
	/// <summary>
	/// The Old One's Army levy sentry staves (Rod / Cane / Staff, tier 1-3). During the event each use plants a banner at the cursor for Etherian Mana; the banner calls
	/// up 1 / 2 / 3 levies from your roster. Place as many as you can pay for. Sold by the Tavernkeep for Defender Medals (see TavernkeepShop).
	/// </summary>
	public abstract class LevySentryStaff : ModItem
	{
		public abstract int Tier { get; }

		public override void SetDefaults()
		{
			Item.width = 40; Item.height = 40; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.Swing;
			Item.noMelee = true; Item.autoReuse = false; Item.maxStack = 1; Item.UseSound = SoundID.Item44;
			Item.rare = Tier == 1 ? ItemRarityID.Green : Tier == 2 ? ItemRarityID.LightPurple : ItemRarityID.Yellow;
			Item.value = Item.buyPrice(gold: 1); Item.shoot = ModContent.ProjectileType<LevySentryBanner>();
		}

		public override Terraria.Localization.LocalizedText Tooltip => base.Tooltip.WithFormatArgs(OldOnesArmySystem.LeviesFor(Tier), OldOnesArmySystem.ManaFor(Tier));

		public override bool CanUseItem(Player player)
		{
			if (!OldOnesArmySystem.Active) return false;
			if (player.CountItem(ItemID.DD2EnergyCrystal, OldOnesArmySystem.ManaFor(Tier)) < OldOnesArmySystem.ManaFor(Tier)) return false;
			var spot = Spot(player);
			return spot.HasValue;
		}

		private static Vector2? Spot(Player player)
		{
			var g = OldOnesArmySystem.GroundBelow(Main.MouseWorld);
			if (g == null) return null;
			if (Vector2.Distance(g.Value, player.Center) > LevyConstants.OoaPlaceRangeTiles * LevyConstants.TilePx) return null;
			return g;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			var spot = Spot(player);
			if (!spot.HasValue || OldOnesArmySystem.Pay(player.CountItem(ItemID.DD2EnergyCrystal), Tier) < 0) return false;
			for (int i = 0; i < OldOnesArmySystem.ManaFor(Tier); i++) player.ConsumeItem(ItemID.DD2EnergyCrystal);
			Projectile.NewProjectile(source, spot.Value + new Vector2(0, -20), Vector2.Zero, ModContent.ProjectileType<LevySentryBanner>(), 0, 0f, player.whoAmI, Tier);
			return false;
		}

	}

	public class LevyRod : LevySentryStaff { public override int Tier => 1; }
	public class LevyCane : LevySentryStaff { public override int Tier => 2; }
	public class LevyStaff : LevySentryStaff { public override int Tier => 3; }
}
