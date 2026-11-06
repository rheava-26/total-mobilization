using System;
using Terraria.ID;

namespace Levy.Common.Progression
{
	/// <summary>Numbers for the progression-assistance options.</summary>
	public static class ProgConst
	{
		// Arms Dealer bulk ammo.
		public const int BulkAmmoUnlockDefault = 500;   // rounds of one ammo type crafted or picked up before the Arms Dealer stocks it
		public const int BulkAmmoPriceDefault = 100;    // percent of the item's normal shop price
		public const int BulkAmmoMaxSlots = 12;         // most extra shop slots used

		// Recipe contents (vanilla item ids).
		public const int LifeCrystalPotions = 5;
		public const int LifeCrystalGems = 1;
		public const int LifeFruitCrystals = 1;
		public const int LifeFruitBars = 3;
		public const int LifeFruitSpores = 5;
		public const int FallenStarBars = 3;
		/// <summary>Fallen Stars one Mana Crystal costs in vanilla; the progression option can only lower it.</summary>
		public const int ManaCrystalStarsVanilla = 5;
		public const int ChlorophyteOreMud = 2;
		public const int ChlorophyteOreSpores = 2;

		// Plantera's Grenade Launcher drops with its Rocket I stack.
		public const int PlanteraRocketMin = 50;
		public const int PlanteraRocketMax = 149;

		// Crate ore (Cobalt/Palladium and Mythril/Orichalcum): the ores early Hardmode crates hand out.
		public static readonly int[] EarlyHardmodeCrateOres = { ItemID.CobaltOre, ItemID.PalladiumOre, ItemID.MythrilOre, ItemID.OrichalcumOre };

		// Chlorophyte Ore dropped by the jungle / event sources and the Extractinator: 1 to 3 per success.
		public const int ChlorophyteDropMin = 1;
		public const int ChlorophyteDropMax = 3;

		// Armor sets whose recipes never change (the sets that stay worth reaching).
		public static readonly int[] ProtectedArmor =
		{
			ItemID.SpectreMask, ItemID.SpectreHood, ItemID.SpectreRobe, ItemID.SpectrePants,
			ItemID.ShroomiteHeadgear, ItemID.ShroomiteMask, ItemID.ShroomiteHelmet, ItemID.ShroomiteBreastplate, ItemID.ShroomiteLeggings,
			ItemID.SpookyHelmet, ItemID.SpookyBreastplate, ItemID.SpookyLeggings,
		};
		public static readonly int[] ChlorophyteArmor = { ItemID.ChlorophyteMask, ItemID.ChlorophyteHelmet, ItemID.ChlorophyteHeadgear, ItemID.ChlorophytePlateMail, ItemID.ChlorophyteGreaves };
		public static readonly int[] HallowedArmor = { ItemID.HallowedMask, ItemID.HallowedHelmet, ItemID.HallowedHeadgear, ItemID.HallowedPlateMail, ItemID.HallowedGreaves };
		public static readonly int[] SpiderArmor = { ItemID.SpiderMask, ItemID.SpiderBreastplate, ItemID.SpiderGreaves };
	}

	/// <summary>Pure math, separate from any game state so the boot self-check can test it.</summary>
	public static class ProgMath
	{
		/// <summary>A drop rule's chance/amount after a drop multiplier. Expected items per kill rise by exactly <paramref name="factor"/>.</summary>
		public static (int num, int den, int min, int max) ScaleDrop(int num, int den, int min, int max, float factor)
		{
			if (factor <= 1f || den <= 0 || num <= 0) return (num, den, min, max);
			const int K = 100;
			double p = (double)num / den * factor;
			if (p <= 1.0)
				return ((int)Math.Round(num * (double)factor * K), den * K, min, max);
			int nmin = Math.Max(min, (int)Math.Round(min * p, MidpointRounding.AwayFromZero));
			int nmax = Math.Max(max, (int)Math.Round(max * p, MidpointRounding.AwayFromZero));
			return (1, 1, nmin, nmax);
		}

		/// <summary>The chance of a single-roll rule after a multiplier (for rules that carry no amount).</summary>
		public static (int num, int den) ScaleChance(int num, int den, float factor)
		{
			var r = ScaleDrop(num, den, 1, 1, factor);
			return (r.num, r.den);
		}

		/// <summary>Fallen Stars a Mana Crystal recipe costs under the option: the recipe's own cost, lowered to the option (never raised), at least 1.</summary>
		public static int ManaCrystalStars(int recipeStars, int option) => Math.Max(1, Math.Min(recipeStars, option));

		/// <summary>An ingredient count after a percent cost: never below 1.</summary>
		public static int ScaleCost(int count, int percent)
			=> percent >= 100 ? count : Math.Max(1, (int)Math.Round(count * percent / 100.0, MidpointRounding.AwayFromZero));

		/// <summary>Per-armor cost percent. Protected sets are untouched; the set sliders and the levy slider multiply.</summary>
		public static int ArmorPercent(int itemType, int chloro, int hallowed, int spider, int levy)
		{
			if (Array.IndexOf(ProgConst.ProtectedArmor, itemType) >= 0) return 100;
			double pct = levy / 100.0;
			if (Array.IndexOf(ProgConst.ChlorophyteArmor, itemType) >= 0) pct *= chloro / 100.0;
			else if (Array.IndexOf(ProgConst.HallowedArmor, itemType) >= 0) pct *= hallowed / 100.0;
			else if (Array.IndexOf(ProgConst.SpiderArmor, itemType) >= 0) pct *= spider / 100.0;
			return (int)Math.Round(pct * 100.0, MidpointRounding.AwayFromZero);
		}

		/// <summary>Extra-yield fraction from the army: command points x percent per point (10 CP at 1% = 0.10).</summary>
		public static double ArmyBonus(int cp, float percentPerCp) => Math.Max(0, cp) * Math.Max(0f, percentPerCp) / 100.0;

		/// <summary>Chance of the second roll after a failed first roll so that the total chance is p x (1 + bonus), capped at certain.</summary>
		public static double RareRerollChance(int num, int den, double bonus)
		{
			if (den <= 0 || num <= 0 || bonus <= 0) return 0;
			double p = (double)num / den;
			if (p >= 1) return 0;
			return Math.Min(1.0, p * bonus / (1 - p));
		}

		/// <summary>A fractional item count rounded down or up at random: <paramref name="u"/> is a uniform [0,1) sample.</summary>
		public static int RoundStochastic(double x, double u) { if (x <= 0) return 0; int f = (int)Math.Floor(x); return f + (u < x - f ? 1 : 0); }

		/// <summary>Dungeon Spirit spawn rolls per dying monster. Vanilla rolls once; the multiplier (x the army bonus) scales the expected spirits, so extra rolls are whole + a fractional one. If vanilla did not roll (the monster fought levies, not a player) the first roll is added here.</summary>
		public static (int whole, double frac) SpiritTrials(float mult, float percentPerCp, int cp, bool vanillaRolled)
		{
			double eff = Math.Max(1f, mult) * (1.0 + ArmyBonus(cp, percentPerCp));
			double extra = Math.Max(0.0, eff - 1.0) + (vanillaRolled ? 0.0 : 1.0);
			extra = Math.Min(extra, 60.0);
			int whole = (int)Math.Floor(extra);
			return (whole, extra - whole);
		}

		/// <summary>The Chlorophyte density limits scaled by <paramref name="scale"/> (1 = vanilla): ore allowed in the near and far area.</summary>
		public static (int nearCap, int farCap) ChlorophyteCaps(int near, int far, float scale) => ((int)(near * scale), (int)(far * scale));

		/// <summary>One Mud Block in the Extractinator: Chlorophyte Ore on a successful roll after any Mechanical Boss, nothing on a failed roll, the Mud Block back before then.</summary>
		public static (int item, int stack) ExtractMud(bool mechDown, float percent, double roll, int stack)
			=> !mechDown ? (ItemID.MudBlock, 1) : roll * 100.0 < percent ? (ItemID.ChlorophyteOre, stack) : (-1, 1);

		/// <summary>Output of a batch: crafts x recipe yield, plus the bonus when the batch is big enough.</summary>
		public static int BatchYield(int recipeStack, int crafts, int bonusPercent, int minCrafts)
		{
			long total = (long)recipeStack * crafts;
			if (crafts >= minCrafts && bonusPercent > 0) total += total * bonusPercent / 100;
			return (int)Math.Min(total, int.MaxValue);
		}

		/// <summary>How many whole crafts (1..<paramref name="wanted"/>) the stock <paramref name="have"/> covers for ingredient stacks <paramref name="need"/>.</summary>
		public static int AffordableCrafts(int[] need, int[] have, int wanted)
		{
			int crafts = wanted;
			for (int i = 0; i < need.Length; i++)
				if (need[i] > 0) crafts = Math.Min(crafts, have[i] / need[i]);
			return Math.Max(0, crafts);
		}

		/// <summary>The extra price the Arms Dealer asks, in copper.</summary>
		public static int BulkAmmoPrice(int itemValue, int percent) => Math.Max(1, (int)Math.Ceiling(itemValue * percent / 100.0));
	}
}
