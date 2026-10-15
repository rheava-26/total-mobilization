using System.Collections.Generic;

namespace Levy.Common
{
	/// <summary>Weapon-layer numbers (combat agent). Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
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

		// ---------- Cadence / animation ----------
		/// <summary>Melee weapons start a swing when the target is within RangeTiles + this many tiles.</summary>
		public const int MeleeEngageExtraTiles = 1;
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
		public const int ManaRegenIdleIntervalTicks = 10;
		public const int ManaRegenCombatIntervalTicks = 20;
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
	}
}
