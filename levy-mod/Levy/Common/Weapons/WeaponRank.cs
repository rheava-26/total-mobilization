using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;

namespace Levy.Common.Weapons
{
	/// <summary>How levies rank the weapons in the armory (server config <see cref="LevyConfig.WeaponRanking"/>).</summary>
	public enum WeaponRankMode { SellPrice, SellPricePlusMaterials, Damage }

	/// <summary>
	/// The one ranking key every weapon pick uses: the allocator (<see cref="WeaponAllocator.RankOf"/>), the armory's varied pick, sidearms, drone loans, upgrade margins and the variety band all call <see cref="Of"/>,
	/// so a mode is consistent everywhere. Class matching, healer scoring, whips yielding to minion staves, dry-gun avoidance and loadout locks are decided elsewhere and do not change with the mode.
	///   SellPrice:               the item's sell value (<c>Item.value</c>).
	///   SellPricePlusMaterials:  sell value plus the recursive cost of the cheapest recipe's ingredients (<see cref="MaterialCost"/>).
	///   Damage:                  <see cref="WeaponClassifier.Score"/> (damage per second scaled by rarity and value).
	/// SCALE. Every caller compares ranks by RATIO (a swap needs 1.15x, the variety band is 0.75x of the best, a sidearm may take at most 0.5x of the best blade). Score spans a few hundred; value spans copper to
	/// platinum, a spread about 1.45x wider in log terms for weapons at the same point in progression (the standard deviation of ln(value) over the 1,640 vanilla, Calamity and Thorium weapons is 1.99 against
	/// 1.38 for ln(Score); per mod 0.54, 0.83 and 0.69). The price modes therefore rank by value^<see cref="PriceExponent"/> (0.7), which leaves the order alone and puts the same margins and band at
	/// the same distance in progression: a 15% better weapon is worth a swap, and the band covers weapons within about a third of the best's price.
	/// ARMOR. With <see cref="SkipArmored"/> (price modes only) a weapon whose hit mostly bounces off the defense of enemies at the world's stage ranks below every weapon that does real damage; see <see cref="Negligible"/>.
	/// </summary>
	public static class WeaponRank
	{
		/// <summary>Rank by value to this power so ratios behave on the same footing as Score's (derivation in the class summary).</summary>
		public const float PriceExponent = 0.7f;
		/// <summary>A weapon whose hit lands less than this share of its damage on an enemy at the world's stage defense is negligible.</summary>
		public const float NegligibleShare = 0.25f;
		/// <summary>Defense of the tougher ordinary enemies by progression stage: before Eye of Cthulhu, after it, after Skeletron, Hardmode, after the mechanical bosses, after Plantera, after Golem.</summary>
		public static readonly int[] StageDefense = { 8, 14, 24, 34, 40, 46, 55 };

		private static WeaponRankMode _mode = WeaponRankMode.SellPrice; private static bool _skip = true;
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
			if (SkipArmored && Negligible(it)) return 0.5f * r / (r + 1f); // below every real weapon (those rank 1 and up), still ordered among themselves
			return r;
		}

		/// <summary>
		/// True when the weapon's hit barely hurts an enemy at the world's stage defense: after defense (half of it in Normal, three quarters in Expert and Master, at least 1 damage left) less than
		/// <see cref="NegligibleShare"/> of the hit remains. A weapon that deals no damage at all is negligible too, except a healer (ranked by what it heals) and a summon staff or sentry (the minions hit for the staff's damage).
		/// </summary>
		public static bool Negligible(Item it)
		{
			if (WeaponClassifier.HealScore(it) > 0f) return false;
			float hit = WeaponClassifier.PerHitDamage(it);
			if (hit <= 0f) return true;
			float c = Main.expertMode ? 0.75f : 0.5f;
			float net = Math.Max(1f, hit - StageDefenseNow() * c);
			return net / hit < NegligibleShare;
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
