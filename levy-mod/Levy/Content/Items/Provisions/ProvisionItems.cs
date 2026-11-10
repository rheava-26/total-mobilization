using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Net;
using Levy.Common.Util;
using Levy.Content.Buffs;

namespace Levy.Content.Items.Provisions
{
	/// <summary>
	/// A round for the whole army from something big (a tank of fizz, a crucible of lava shots, a meal): using it reaches the levies within <see cref="LevyConstants.DrinkRadiusTiles"/> tiles of the player.
	/// Single player serves it directly; a client asks the server (the soldiers' stats live there). The round is refused, and not used up, when no levy is in reach.
	/// </summary>
	public abstract class ProvisionContainer : ModItem
	{
		public override string LocalizationCategory => "Provisions";
		/// <summary>Wire id of the round (see <see cref="ProvisionRules"/>).</summary>
		public abstract int RoundId { get; }
		protected abstract (int w, int h) Size { get; }
		protected virtual int PriceCopper => Item.sellPrice(silver: 20);
		protected virtual int Rarity => ItemRarityID.Blue;
		protected virtual int UseSoundId => 3;

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 1; }

		public override void SetDefaults()
		{
			Item.width = Size.w; Item.height = Size.h; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = UseSoundId == 2 ? SoundID.Item2 : SoundID.Item3;
			Item.consumable = true; Item.maxStack = 30; Item.rare = Rarity; Item.value = PriceCopper;
		}

		public override bool CanUseItem(Player player)
		{
			if (ProvisionRules.AnyLevyInReach(player.Center)) return true;
			if (player.whoAmI == Main.myPlayer && !Main.dedServ) CombatText.NewText(player.getRect(), new Color(255, 200, 120), LevyUtil.T("Provisions.NobodyInReach", LevyConstants.DrinkRadiusTiles));
			return false;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				var p = LevyPackets.New(LevyPacket.DrinkRound);
				p.Write((byte)RoundId);
				p.Send();
			}
			else DrinkBuffs.Serve(player.Center, RoundId, player.name);
			return true;
		}
	}

	/// <summary>Case of Fizz (class keeps its old name): a bulk fizzy round, crafted from cans. Also sold by the Tavernkeep.</summary>
	public class FizzTank : ProvisionContainer
	{
		public override int RoundId => ProvisionRules.FizzId;
		protected override (int w, int h) Size => (30, 36);
		protected override int PriceCopper => Item.buyPrice(silver: 5);
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyConstants.DrinkRadiusTiles, LevyConstants.DrinkDurationTicks / 3600, ProvisionRules.FizzJumpTiles, LevyConstants.FizzDefense, (int)System.Math.Round(LevyConstants.FizzKnockbackResist * 100f));

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ModContent.ItemType<FizzPop>(), LevyConstants.FizzTankPops).AddTile(TileID.WorkBenches).Register();
	}

	/// <summary>Iceberg Tray (class keeps its old name): a bulk round of Iceberg Shots, crafted only.</summary>
	public class MagmaCrucible : ProvisionContainer
	{
		public override int RoundId => ProvisionRules.LavaId;
		protected override (int w, int h) Size => (38, 32);
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyConstants.DrinkRadiusTiles, LevyConstants.LavaShotDurationTicks / 3600, (int)System.Math.Round((LevyConstants.DrunkAimErrorMult - 1f) * 100f));

		public override void AddRecipes()
			=> CreateRecipe().AddIngredient(ModContent.ItemType<MagmaShot>(), LevyConstants.CrucibleShots).AddRecipeGroup(RecipeGroupID.IronBar, 4).AddTile(TileID.Anvils).Register();
	}

	/// <summary>A single drink: a buff potion the player can drink, which the Armory also issues to a levy from a buff slot.</summary>
	public abstract class SingleDrink : ModItem
	{
		public override string LocalizationCategory => "Provisions";
		protected abstract int BuffType { get; }
		protected abstract int BuffTicks { get; }
		protected abstract (int w, int h) Size { get; }

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 20; }

		public override void SetDefaults()
		{
			Item.width = Size.w; Item.height = Size.h; Item.maxStack = Item.CommonMaxStack; Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid; Item.useTime = 17; Item.useAnimation = 17; Item.UseSound = SoundID.Item3; Item.useTurn = true;
			Item.rare = ItemRarityID.Blue; Item.value = Item.sellPrice(silver: 1);
			Item.buffType = BuffType; Item.buffTime = BuffTicks;
		}
	}

	/// <summary>Fizz Can (class keeps its old name): a non-alcoholic bubbly drink in a soda can. Brewed in bulk (four per batch).</summary>
	public class FizzPop : SingleDrink
	{
		protected override int BuffType => ModContent.BuffType<FizzyBuff>();
		protected override int BuffTicks => LevyConstants.DrinkDurationTicks;
		protected override (int w, int h) Size => (20, 30);
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(ProvisionRules.FizzJumpTiles, LevyConstants.DrinkDurationTicks / 3600, LevyConstants.FizzDefense);

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			ItemID.Sets.DrinkParticleColors[Type] = new[] { new Color(111, 220, 114), new Color(201, 255, 156), new Color(47, 163, 86) };
		}

		public override void AddRecipes()
			=> CreateRecipe(LevyConstants.FizzPopsPerBrew).AddIngredient(ItemID.BottledWater, LevyConstants.FizzPopsPerBrew).AddIngredient(ItemID.Cloud).AddIngredient(ItemID.Daybloom).AddTile(TileID.Bottles).Register();
	}

	/// <summary>Iceberg Shot (class keeps its old name): ice-cold vodka, so numb that lava means nothing: lava immunity and lava swimming. Brewed in bulk (five per batch) from the Obsidian Skin Potion's own ingredients plus an Ice Block.</summary>
	public class MagmaShot : SingleDrink
	{
		protected override int BuffType => ModContent.BuffType<LavaShotBuff>();
		protected override int BuffTicks => LevyConstants.LavaShotDurationTicks;
		protected override (int w, int h) Size => (24, 22);
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyConstants.LavaShotDurationTicks / 3600);

		public override void SetStaticDefaults()
		{
			base.SetStaticDefaults();
			ItemID.Sets.DrinkParticleColors[Type] = new[] { new Color(217, 52, 26), new Color(255, 122, 28), new Color(255, 210, 74) };
		}
		// The recipe is registered in ProvisionsSystem.AddRecipes: it copies the vanilla Obsidian Skin Potion's ingredients.
	}

	/// <summary>A meal for the army: feeds the nearest levies in reach, Well Fed of its tier for thirty minutes and Morale for five.</summary>
	public abstract class MealItem : ProvisionContainer
	{
		public abstract int Meal { get; }
		public override int RoundId => ProvisionRules.MealBase + Meal;
		protected override int UseSoundId => 2;
		protected override int PriceCopper => Item.sellPrice(silver: 50);
		protected override int Rarity => ItemRarityID.Green;
		public override void SetDefaults() { base.SetDefaults(); Item.useStyle = ItemUseStyleID.EatFood; Item.maxStack = 20; }
		public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(LevyConstants.MealFeeds[Meal], LevyConstants.DrinkRadiusTiles, Lang.GetBuffName(ProvisionRules.WellFedBuff(LevyConstants.MealWellFedTier[Meal])), LevyConstants.MealDurationTicks / 3600, LevyConstants.MealMoraleTicks / 3600);
	}

	public class FishFryPlatter : MealItem { public override int Meal => 0; protected override (int w, int h) Size => (52, 22); }
	public class FruitBasket : MealItem { public override int Meal => 1; protected override (int w, int h) Size => (42, 32); }
	public class BurgerBox : MealItem { public override int Meal => 2; protected override (int w, int h) Size => (36, 34); }
	public class GrandFeast : MealItem { public override int Meal => 3; protected override (int w, int h) Size => (60, 30); protected override int Rarity => ItemRarityID.Orange; }
}
