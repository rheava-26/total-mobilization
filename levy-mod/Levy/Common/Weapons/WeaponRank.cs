using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;

namespace Levy.Common.Weapons
{
	/// <summary>How levies rank the weapons in the armory (server config <see cref="LevyConfig.WeaponRanking"/>).</summary>
	public enum WeaponRankMode { Balanced, SellPrice, SellPricePlusMaterials, Damage }

	/// <summary>
	/// The one ranking key every weapon pick uses: the allocator (<see cref="WeaponAllocator.RankOf"/>), the armory's varied pick, sidearms, drone loans, upgrade margins and the variety band all call <see cref="Of"/>,
	/// so a mode is consistent everywhere. Class matching, healer scoring, whips yielding to minion staves, dry-gun avoidance and loadout locks are decided elsewhere and do not change with the mode.
	///   Balanced (default):      the sell price, discounted for weapons the world has outgrown (<see cref="StageDiscount"/>) and raised for weapons that just entered the armory (<see cref="RecencyBoost"/>).
	///   SellPrice:               the item's sell value (<c>Item.value</c>).
	///   SellPricePlusMaterials:  sell value plus the recursive cost of the cheapest recipe's ingredients (<see cref="MaterialCost"/>).
	///   Damage:                  <see cref="WeaponClassifier.Score"/> (damage per second scaled by rarity and value).
	/// SCALE. Every caller compares ranks by RATIO (a swap needs 1.15x, the variety band is 0.75x of the best, a sidearm may take at most 0.5x of the best blade). Score spans a few hundred; value spans copper to
	/// platinum, a spread about 1.45x wider in log terms for weapons at the same point in progression (the standard deviation of ln(value) over the 1,640 vanilla, Calamity and Thorium weapons is 1.99 against
	/// 1.38 for ln(Score); per mod 0.54, 0.83 and 0.69). The price modes therefore rank by value^<see cref="PriceExponent"/> (0.7), which leaves the order alone and puts the same margins and band at
	/// the same distance in progression: a 15% better weapon is worth a swap, and the band covers weapons within about a third of the best's price.
	/// BALANCED. rank = max(1, value^0.7 x StageDiscount(world progress - weapon tier) x RecencyBoost(age in the armory)).
	///   Tier is <see cref="ProgressionTier"/> (Boss Checklist scale); world progress is <see cref="Progress"/> (the furthest boss downed, same scale). Damage never enters, so a fast weak weapon is not punished.
	///   StageDiscount(g) = exp(-(g/7)^3) for g > 0: 1 at the current stage, 0.997 one stage behind, 0.977 two, 0.92 three, 0.61 at 5.5 (a Minishark in early Hardmode), 0.37 at 7 (a Copper Shortsword in Hardmode), 0.05 at 10.
	///   RecencyBoost(a) = 1 + 0.6 x exp(-a^2), a in game days since deposit: x1.6 at once, x1.47 after half a day, x1.22 after one, x1.06 after a day and a half, x1.01 after two. 1.6 clears the 1.15 swap margin with room,
	///   and on the value^0.7 scale it is worth a 1.96x sell price: a fresh weapon beats an older one down to about half its price, while a fresh weapon a quarter of the price (rank 0.38 x 1.6 = 0.6) loses to it.
	/// ARMOR. With <see cref="SkipArmored"/> (price modes only) a weapon whose hit mostly bounces off the defense of enemies at the world's stage ranks below every weapon that does real damage; see <see cref="Negligible"/>.
	/// </summary>
	public static class WeaponRank
	{
		/// <summary>Rank by value to this power so ratios behave on the same footing as Score's (derivation in the class summary).</summary>
		public const float PriceExponent = 0.7f;
		/// <summary>Balanced: a weapon this many stages behind the world is down to 1/e (0.37) of its price rank; the exponent sets how sharply the discount sets in.</summary>
		public const float StageScale = 7f, StageSharpness = 3f;
		/// <summary>Balanced: a weapon deposited just now ranks this much higher (x1.6), fading as a Gaussian over <see cref="RecentDays"/> game days.</summary>
		public const float RecentBoostMax = 0.6f, RecentDays = 1f;
		/// <summary>A weapon whose hit lands less than this share of its damage on an enemy at the world's stage defense is negligible.</summary>
		public const float NegligibleShare = 0.25f;
		/// <summary>Defense of the tougher ordinary enemies by progression stage: before Eye of Cthulhu, after it, after Skeletron, Hardmode, after the mechanical bosses, after Plantera, after Golem.</summary>
		public static readonly int[] StageDefense = { 8, 14, 24, 34, 40, 46, 55 };

		private static WeaponRankMode _mode = WeaponRankMode.Balanced; private static bool _skip = false;
		private static bool _forced; private static WeaponRankMode _forcedMode; private static bool _forcedSkip;

		public static WeaponRankMode Mode => _forced ? _forcedMode : _mode;
		public static bool SkipArmored => _forced ? _forcedSkip : _skip;

		/// <summary>Called by the config on load / change.</summary>
		internal static void SetConfig(WeaponRankMode mode, bool skipArmored) { _mode = mode; _skip = skipArmored; }

		/// <summary>Self-check / probe seam: pins the mode for the length of a test. <see cref="Release"/> returns to the config.</summary>
		internal static void Force(WeaponRankMode mode, bool skipArmored) { _forced = true; _forcedMode = mode; _forcedSkip = skipArmored; }
		internal static void Release() { _forced = false; }

		/// <summary>The rank of a weapon under the current mode: higher is better, 0 for an item that is not a supported weapon.</summary>
		public static float Of(Item it)
		{
			if (it == null || it.IsAir) return 0f;
			var p = WeaponClassifier.GetProfile(it);
			if (!p.Supported) return 0f;
			WeaponRankMode mode = Mode;
			if (mode == WeaponRankMode.Damage) return WeaponClassifier.Score(it);
			double key = Math.Max(0, it.value);
			if (mode == WeaponRankMode.SellPricePlusMaterials) key += MaterialCost.Of(it.type);
			float r = (float)Math.Pow(Math.Max(1d, key), PriceExponent); // at least 1: a worthless item is the cheapest of the real weapons
			if (mode == WeaponRankMode.Balanced)
			{
				float tier = ProgressionTier.Of(it.type);
				if (tier >= 0f) r *= StageDiscount(Progress() - tier);
				r = Math.Max(1f, r * DepositStamp.Boost(it));
			}
			if (SkipArmored && Negligible(it)) return 0.5f * r / (r + 1f); // below every real weapon (those rank 1 and up), still ordered among themselves
			return r;
		}

		/// <summary>The Balanced discount for a weapon <paramref name="gap"/> stages behind the world: 1 at or ahead of the world, exp(-(gap/7)^3) behind it.</summary>
		public static float StageDiscount(float gap) => gap <= 0f ? 1f : (float)Math.Exp(-Math.Pow(gap / StageScale, StageSharpness));

		/// <summary>The Balanced boost for a weapon deposited <paramref name="ageTicks"/> world ticks ago: 1 + 0.6 x exp(-(days)^2).</summary>
		public static float RecencyBoost(long ageTicks)
		{
			double days = Math.Max(0L, ageTicks) / (double)LevyClock.TicksPerDay / RecentDays;
			return 1f + RecentBoostMax * (float)Math.Exp(-days * days);
		}

		/// <summary>Probe / self-check seam: pins the world's progress on the Boss Checklist scale. Negative = the world's.</summary>
		internal static float ForceProgress = -1f;
		private static uint _progTick = uint.MaxValue; private static float _prog;

		/// <summary>The world's progress on the Boss Checklist scale (<see cref="ProgressionTier"/>): the furthest boss milestone reached, 0 before King Slime, 18 after Moon Lord.</summary>
		public static float Progress()
		{
			if (ForceProgress >= 0f) return ForceProgress;
			if (ForceStage >= 0) return RepresentativeProgress[ForceStage];
			uint now = Main.GameUpdateCount;
			if (_progTick == now) return _prog;
			_progTick = now;
			float p = 0f;
			if (NPC.downedSlimeKing) p = Math.Max(p, 1f);
			if (NPC.downedBoss1) p = Math.Max(p, 2f);
			if (NPC.downedBoss2) p = Math.Max(p, 3f);
			if (NPC.downedQueenBee) p = Math.Max(p, 4f);
			if (NPC.downedBoss3) p = Math.Max(p, 5f);
			if (NPC.downedDeerclops) p = Math.Max(p, 6f);
			if (Main.hardMode) p = Math.Max(p, 7f);
			if (NPC.downedQueenSlime) p = Math.Max(p, 8f);
			if (NPC.downedMechBoss2) p = Math.Max(p, 9f);
			if (NPC.downedMechBoss1) p = Math.Max(p, 10f);
			if (NPC.downedMechBoss3) p = Math.Max(p, 11f);
			if (NPC.downedPlantBoss) p = Math.Max(p, 12f);
			if (NPC.downedGolemBoss) p = Math.Max(p, 13f);
			if (NPC.downedFishron) p = Math.Max(p, 14f);
			if (NPC.downedEmpressOfLight) p = Math.Max(p, 15f);
			if (NPC.downedAncientCultist) p = Math.Max(p, 17f);
			if (NPC.downedMoonlord) p = Math.Max(p, 18f);
			_prog = p;
			return p;
		}

		/// <summary>Progress a pinned coarse stage (<see cref="ForceStage"/>) stands for: before Eye, after Eye, after Skeletron, early Hardmode, after the Twins, after Plantera, after Golem.</summary>
		private static readonly float[] RepresentativeProgress = { 1f, 3f, 5f, 7f, 9f, 12f, 13f };

		/// <summary>
		/// True when the weapon's hit barely hurts an enemy at the world's stage defense: after defense (half of it in Normal, three quarters in Expert and Master) less than
		/// <see cref="NegligibleShare"/> of the hit remains. A weapon that deals no damage at all is negligible too, except a healer (ranked by what it heals) and a summon staff or sentry (the minions hit for the staff's damage).
		/// </summary>
		public static bool Negligible(Item it)
		{
			if (WeaponClassifier.HealScore(it) > 0f) return false;
			float hit = WeaponClassifier.PerHitDamage(it);
			if (hit <= 0f) return true;
			float c = Main.expertMode ? 0.75f : 0.5f;
			float share = Math.Max(0f, hit - StageDefenseNow() * c) / hit; // the share of the hit that gets through (vanilla's 1-damage floor is not counted: a hit the armor swallows is a hit that does nothing)
			return share < NegligibleShare;
		}

		/// <summary>The defense of enemies at the world's progression stage (see <see cref="StageDefense"/>).</summary>
		public static int StageDefenseNow() => StageDefense[Stage()];

		/// <summary>Probe seam: pins the stage (0..6) instead of reading the world. -1 = the world's.</summary>
		internal static int ForceStage = -1;
		private static uint _stageTick = uint.MaxValue; private static int _stage;

		/// <summary>0..6 from the world's progress: the furthest boss milestone reached, on Boss Checklist's scale (King Slime 1, Eye 2, Evil boss 3, Bee 4, Skeletron 5, Deerclops 6, Wall of Flesh 7, Queen Slime 8, Twins 9, Destroyer 10, Prime 11, Plantera 12, Golem 13).</summary>
		public static int Stage()
		{
			if (ForceStage >= 0) return ForceStage;
			uint now = Main.GameUpdateCount;
			if (_stageTick == now) return _stage;
			_stageTick = now;
			int p = 0;
			if (NPC.downedSlimeKing) p = Math.Max(p, 1);
			if (NPC.downedBoss1) p = Math.Max(p, 2);
			if (NPC.downedBoss2) p = Math.Max(p, 3);
			if (NPC.downedQueenBee) p = Math.Max(p, 4);
			if (NPC.downedBoss3) p = Math.Max(p, 5);
			if (NPC.downedDeerclops) p = Math.Max(p, 6);
			if (Main.hardMode) p = Math.Max(p, 7);
			if (NPC.downedQueenSlime) p = Math.Max(p, 8);
			if (NPC.downedMechBoss2) p = Math.Max(p, 9);
			if (NPC.downedMechBoss1) p = Math.Max(p, 10);
			if (NPC.downedMechBoss3) p = Math.Max(p, 11);
			if (NPC.downedPlantBoss) p = Math.Max(p, 12);
			if (NPC.downedGolemBoss || NPC.downedFishron || NPC.downedAncientCultist || NPC.downedMoonlord || NPC.downedEmpressOfLight) p = Math.Max(p, 13);
			_stage = p < 2 ? 0 : p < 5 ? 1 : p < 7 ? 2 : p < 9 ? 3 : p < 11 ? 4 : p < 13 ? 5 : 6;
			return _stage;
		}
	}
}
