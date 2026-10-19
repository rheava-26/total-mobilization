using System.Collections.Generic;

namespace Levy.Common
{
	/// <summary>Weapon-layer numbers (combat agent). Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---------- Weapon reach (tiles) ----------
		/// <summary>Effective engagement range by family: how far a soldier acquires targets and opens fire. Ranged soldiers hold and kite at this reach.</summary>
		public const int RangeGun = 72, RangeBow = 54, RangeMagic = 40;

		// ---------- Muzzle / aim ----------
		/// <summary>Muzzle offset from the body centre, in pixels (X is multiplied by the facing direction).</summary>
		public const float MuzzleOffsetX = 10f, MuzzleOffsetY = -4f;
		/// <summary>Intercept solve: how many prediction passes and the longest flight time (ticks) a shot is led for.</summary>
		public const int AimRefineIterations = 2;
		public const float AimMaxLeadTicks = 90f;
		/// <summary>Distance used to turn a lead error into an angle when the caller gives no target position (see Aiming.ApplyError).</summary>
		public const float AimErrorReferenceDistancePx = 320f;
		/// <summary>Projectile types with aiStyle 1 that vanilla exempts from the gravity timer (Projectile.AI_001, read from the source): bullets, lasers, Jester's arrows…</summary>
		public static readonly HashSet<int> AiStyle1NoGravity = new()
		{
			5, 14, 20, 36, 38, 55, 83, 84, 88, 89, 98, 100, 104, 110, 158, 159, 160, 161, 180, 184, 242, 248, 257, 259, 265, 270, 279, 283, 284, 285, 286, 287,
			299, 302, 323, 325, 348, 349, 350, 355, 374, 376, 389, 435, 436, 438, 440, 442, 449, 459, 462, 467, 468, 469, 472, 483, 484, 485, 498, 576, 577,
			585, 592, 593, 601, 606, 616, 634, 635, 638, 639, 660, 661, 682, 684, 706, 709, 710, 712, 876,
		};

		/// <summary>
		/// Long-range aim help: shots farther than AccuracyFalloffStartTiles scale their jitter and lead error down, reaching AccuracyLongRangeErrorMult
		/// of the normal error at AccuracyFalloffEndTiles and beyond. Shots inside the start distance are untouched.
		/// </summary>
		public const int AccuracyFalloffStartTiles = RangeGun / 2, AccuracyFalloffEndTiles = RangeGun;
		public const float AccuracyLongRangeErrorMult = 0.6f;

		/// <summary>Routed soldiers keep shooting: the aim error is this multiple of normal in total (Shaken's own x2 is part of it, not on top).</summary>
		public const float RoutedAimMult = 3f;

		// ---------- Cadence / animation ----------
		/// <summary>Melee weapons start a swing when the target is within RangeTiles + this many tiles.</summary>
		public const int MeleeEngageExtraTiles = 1;
		/// <summary>Pure melee soldiers (swords, spears, shortswords) start the swing/thrust when a hostile is within this multiple of their melee reach, so the blow is already landing as the enemy arrives. Never less than reach + MeleeEngageExtraTiles.</summary>
		public const float MeleePreSwingReachMult = 1.4f;
		/// <summary>Projectile-melee weapons (swords that fire a beam) engage out to the beam's reach, capped here.</summary>
		public const int ProjectileMeleeReachCapTiles = RangeMagic;
		/// <summary>Size of the swing hitbox when no texture is loaded (dedicated server): vanilla's fallback item rectangle.</summary>
		public const int MeleeHitboxFallbackPx = 32;
		/// <summary>Titan Glove melee size bonus (Player.ApplyMeleeScale).</summary>
		public const float MeleeGloveScale = 1.1f;
		/// <summary>Base crit chance a player always has (Player.GetTotalCritChance base), used when the dummy pipeline is unavailable.</summary>
		public const int BaseCritChance = 4;
		/// <summary>Sword beams leave the body at this speed when the item has no shootSpeed.</summary>
		public const float SwordBeamSpeedFallback = 8f;
		/// <summary>How often the held weapon is re-evaluated for ammo/mana (ticks).</summary>
		public const int SidearmCheckIntervalTicks = 30;

		// ---------- Mana ----------
		/// <summary>Mana per second: the larger of the floor and the pool fraction (a bigger pool refills faster, as in vanilla). In combat / out of combat.</summary>
		public const float ManaRegenCombatMinPerSec = 6f, ManaRegenCombatPoolFrac = 0.20f;
		public const float ManaRegenIdleMinPerSec = 12f, ManaRegenIdlePoolFrac = 0.40f;
		/// <summary>A mana-fed primary that is short of one cast stays in hand (holding fire) when the pool will cover the cast within this many ticks; only a longer wait falls back to the sidearm. Once on the sidearm it returns to the primary when a full cast is available.</summary>
		public const int ManaWaitForPrimaryTicks = 90;
		/// <summary>Pure-melee soldiers (blade / spear / shortsword) swing whenever a hostile is within reach * this + extra tiles, so the blow is already landing as the enemy arrives and the swing never idles while one is in reach.</summary>
		public const float MeleeRelentlessReachMult = 1.8f;
		public const int MeleeRelentlessExtraTiles = 2;
		/// <summary>Pure-melee soldiers close until the hostile's hitbox is this near (tiles) or its centre is within MeleeCloseCentreTiles, then hold and swing.</summary>
		public const float MeleeContactTiles = 0.5f, MeleeCloseCentreTiles = 2f;
		/// <summary>Ticks after leaving Engage before mana regenerates at the idle rate.</summary>
		public const int ManaCombatGraceTicks = 120;

		// ---------- Ammo conservation (Player.IsAmmoFreeThisShot odds) ----------
		public const int AmmoConserve80Denominator = 5;   // ammoCost80 / chloroAmmoCost80: 1 in 5 shots free
		public const int AmmoConserve75Denominator = 4;   // ammoCost75: 1 in 4
		public const int AmmoConserve90Denominator = 10;  // huntressAmmoCost90: 1 in 10
		public const int MagicQuiverDenominator = 5;      // magicQuiver, arrows only: 1 in 5

		// ---------- Thrust (spears / shortswords) ----------
		/// <summary>Reach of a thrust in pixels per point of the item's shootSpeed (Spear 3.75 → 90 px, shortswords 2.1 → 50 px).</summary>
		public const float ThrustReachPerShootSpeed = 24f;
		public const float ThrustReachMinPx = 24f;
		public const int ThrustDefaultLifeTicks = 30;
		public const int ThrustLocalHitCooldownTicks = 20;
		public const int ThrustHitboxPx = 18;
		public const int ThrustRangeMinTiles = 2, ThrustRangeMaxTiles = 6;

		// ---------- Wider weapon compatibility ----------
		/// <summary>Ammo weapons whose projectile dies young (flamethrowers, short-lived shots) engage only at their real reach: speed x lifetime x this fraction, clamped to [min, RangeGun].</summary>
		public const float ShortShotReachFraction = 0.8f;
		public const int ShortShotRangeMinTiles = 8;
		/// <summary>A projectile whose default lifetime exceeds this many ticks is treated as long-range (no reach clamp).</summary>
		public const int LongShotLifetimeTicks = 240;
		/// <summary>A weapon that spawned nothing (hooks fired no projectile / threw) waits this many ticks before the soldier tries again, so a dud never spams hooks every tick.</summary>
		public const int DudWeaponRetryTicks = 30;
		/// <summary>Rarity tier given to Expert / Master items (negative item.rare values) when ranking.</summary>
		public const int SpecialRarityTier = 6;
		/// <summary>Highest rarity tier counted when ranking (modded rarities above vanilla Purple still count up to here).</summary>
		public const int MaxRarityTier = 12;

		// ---------- Tier-aware ranking ----------
		/// <summary>Weapon score = DPS x (1 + WeaponRarityWeight x tier + WeaponValueWeight x log10(1 + value copper)). DPS stays primary; a tier is worth ~6%.</summary>
		public const float WeaponRarityWeight = 0.06f, WeaponValueWeight = 0.04f;
		/// <summary>Armor score = rarity tier x ArmorRarityWeight + defense x ArmorDefenseWeight + log10(1 + value) x ArmorValueWeight. One rarity tier outweighs ~3.5 defense, so progression beats flat defense.</summary>
		public const float ArmorRarityWeight = 350f, ArmorDefenseWeight = 100f, ArmorValueWeight = 40f;

		// ---------- Squad weapon variety ----------
		/// <summary>When issuing a primary weapon a soldier draws from the weapon types scoring within this fraction of the best viable score (at most WeaponVarietyMaxTypes types).</summary>
		public const float WeaponVarietyTopFraction = 0.75f;
		public const int WeaponVarietyMaxTypes = 6;
	}
}
