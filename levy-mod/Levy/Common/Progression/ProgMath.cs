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
		public const int ManaCrystalGems = 8;
		public const int ChlorophyteOreMud = 2;
		public const int ChlorophyteOreSpores = 2;

		// Plantera's Grenade Launcher drops with its Rocket I stack.
		public const int PlanteraRocketMin = 50;
		public const int PlanteraRocketMax = 149;

		// Crate ore (Cobalt/Palladium and Mythril/Orichalcum): the ores early Hardmode crates hand out.
		public static readonly int[] EarlyHardmodeCrateOres = { ItemID.CobaltOre, ItemID.PalladiumOre, ItemID.MythrilOre, ItemID.OrichalcumOre };

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
