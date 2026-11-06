using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Progression
{
	/// <summary>
	/// Recipe side of progression assistance: new recipes (Life Crystal, Life Fruit, Fallen Star, Chlorophyte Ore),
	/// edits to vanilla recipes (Chlorophyte Bar cost, Mana Crystal star cost, ammo yield, armor cost). With every option at its default nothing is added or edited.
	/// </summary>
	public class ProgressionRecipes : ModSystem
	{
		/// <summary>Edits applied to vanilla recipes (for the log / self-check).</summary>
		public static int EditedBarRecipes, EditedAmmoRecipes, EditedArmorRecipes, EditedManaCrystalRecipes;

		/// <summary>Every non-Levy recipe as it was when this mod's PostAddRecipes ran, before Levy's edits: the recipe object (its index changes when other mods reorder recipes later) to (result stack, ingredient stacks).</summary>
		public static readonly Dictionary<Recipe, (int result, int[] ing)> Snapshot = new(ReferenceEqualityComparer.Instance);

		public override void AddRecipes()
		{
			var c = LevyProgressionConfig.Get;
			if (c == null) return;

			if (c.LifeCrystalRecipe)
				Recipe.Create(ItemID.LifeCrystal)
					.AddIngredient(ItemID.LesserHealingPotion, ProgConst.LifeCrystalPotions)
					.AddIngredient(ItemID.Ruby, ProgConst.LifeCrystalGems)
					.AddTile(TileID.AlchemyTable).Register();

			if (c.LifeFruitRecipe)
				Recipe.Create(ItemID.LifeFruit)
					.AddIngredient(ItemID.LifeCrystal, ProgConst.LifeFruitCrystals)
					.AddIngredient(ItemID.ChlorophyteBar, ProgConst.LifeFruitBars)
					.AddIngredient(ItemID.JungleSpores, ProgConst.LifeFruitSpores)
					.AddTile(TileID.MythrilAnvil)
					.AddCondition(Condition.DownedMechBossAny).Register();

			if (c.FallenStarRecipe)
				Recipe.Create(ItemID.FallenStar)
					.AddIngredient(ItemID.MeteoriteBar, ProgConst.FallenStarBars)
					.AddTile(TileID.Anvils)
					.AddCondition(Condition.DownedEowOrBoc).Register();

			if (c.ChlorophyteOreRecipe)
				Recipe.Create(ItemID.ChlorophyteOre)
					.AddIngredient(ItemID.MudBlock, ProgConst.ChlorophyteOreMud)
					.AddIngredient(ItemID.JungleSpores, ProgConst.ChlorophyteOreSpores)
					.AddTile(TileID.AdamantiteForge)
					.AddCondition(Condition.Hardmode).Register();
		}

		public override void PostAddRecipes()
		{
			var c = LevyProgressionConfig.Get;
			EditedBarRecipes = EditedAmmoRecipes = EditedArmorRecipes = EditedManaCrystalRecipes = 0;
			Snapshot.Clear();
			for (int r = 0; r < Recipe.numRecipes; r++)
			{
				Recipe rec = Main.recipe[r];
				if (rec == null || rec.Mod == Mod) continue;
				Snapshot[rec] = (rec.createItem.stack, rec.requiredItem.Select(i => i.stack).ToArray());
			}
			if (c == null) return;

			for (int r = 0; r < Recipe.numRecipes; r++)
			{
				Recipe recipe = Main.recipe[r];
				if (recipe == null || recipe.Disabled || recipe.createItem == null || recipe.createItem.IsAir) continue;
				if (recipe.Mod == Mod) continue;   // our own new recipes keep their numbers
				Item result = recipe.createItem;

				// Chlorophyte Bar: ore per bar.
				if (result.type == ItemID.ChlorophyteBar && c.ChlorophyteOrePerBar < 5 && recipe.TryGetIngredient(ItemID.ChlorophyteOre, out Item ore))
				{
					ore.stack = Math.Min(ore.stack, c.ChlorophyteOrePerBar);
					EditedBarRecipes++;
				}

				// Mana Crystal: Fallen Stars per crystal (the option can only lower the vanilla cost).
				if (result.type == ItemID.ManaCrystal && c.FallenStarsPerManaCrystal < ProgConst.ManaCrystalStarsVanilla && recipe.TryGetIngredient(ItemID.FallenStar, out Item stars))
				{
					stars.stack = ProgMath.ManaCrystalStars(stars.stack, c.FallenStarsPerManaCrystal);
					EditedManaCrystalRecipes++;
				}

				// Ammo yield.
				if (c.AmmoRecipeYieldMultiplier > 1 && IsAmmo(result))
				{
					result.stack = ProgMath_AmmoStack(result, c.AmmoRecipeYieldMultiplier);
					EditedAmmoRecipes++;
				}

				// Armor cost.
				if ((c.ChlorophyteArmorCostPercent < 100 || c.HallowedArmorCostPercent < 100 || c.SpiderArmorCostPercent < 100 || c.LevyArmorCostPercent < 100) && IsArmorPiece(result))
				{
					int pct = ProgMath.ArmorPercent(result.type, c.ChlorophyteArmorCostPercent, c.HallowedArmorCostPercent, c.SpiderArmorCostPercent, c.LevyArmorCostPercent);
					if (pct < 100)
					{
						foreach (Item ing in recipe.requiredItem) ing.stack = ProgMath.ScaleCost(ing.stack, pct);
						EditedArmorRecipes++;
					}
				}
			}
		}

		public static bool IsAmmo(Item i)
			=> i.ammo > AmmoID.None && i.damage > 0 && i.consumable && !i.notAmmo
			   && (i.ammo == AmmoID.Arrow || i.ammo == AmmoID.Bullet || i.ammo == AmmoID.Dart || i.ammo == AmmoID.Rocket);

		public static bool IsArmorPiece(Item i)
			=> !i.vanity && (i.headSlot >= 0 || i.bodySlot >= 0 || i.legSlot >= 0);

		public static int ProgMath_AmmoStack(Item result, int mult)
		{
			long s = (long)result.stack * mult;
			return (int)Math.Min(s, Math.Max(1, result.maxStack));
		}
	}
}
