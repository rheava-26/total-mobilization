namespace Levy.Common
{
	/// <summary>Soldier-thrown yoyos and flails: Levy-owned anchored projectiles (SoldierYoyo / SoldierFlail) that never touch the commander's held-projectile state. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---------- Yoyos ----------
		/// <summary>Yoyos one soldier may have out at once.</summary>
		public const int YoyoMaxOut = 1;
		/// <summary>Reach (px from the soldier) when the yoyo gives no maximum range of its own, and the band every yoyo's reach is clamped into.</summary>
		public const float YoyoDefaultReachPx = 190f, YoyoReachMinPx = 110f, YoyoReachMaxPx = 420f;
		/// <summary>Engagement range band (tiles) derived from the yoyo's reach.</summary>
		public const int YoyoRangeMinTiles = 6, YoyoRangeMaxTiles = 26;
		/// <summary>Loiter time (ticks) for yoyos with no finite flight time, and the band every yoyo's loiter time is clamped into.</summary>
		public const int YoyoDefaultLoiterTicks = 300, YoyoLoiterMinTicks = 150, YoyoLoiterMaxTicks = 480;
		/// <summary>Top speed (px/tick) of the yoyo while chasing its target, and the steering blend per tick.</summary>
		public const float YoyoMaxSpeed = 15f, YoyoSteer = 0.22f;
		/// <summary>Orbit radius (px) around the target and the orbit angular speed (radians per tick).</summary>
		public const float YoyoOrbitRadiusPx = 26f, YoyoOrbitSpeed = 0.14f;
		/// <summary>How far (px) from the yoyo a new target may be picked while it loiters, and the ticks it hovers with no target before heading home.</summary>
		public const float YoyoSeekRadiusPx = 220f;
		public const int YoyoLoseTargetTicks = 25;
		/// <summary>Return phase: top speed (px/tick), distance to the soldier (px) at which it is caught, and a hard cap (ticks) on the whole flight.</summary>
		public const float YoyoReturnSpeed = 18f, YoyoCatchPx = 24f;
		public const int YoyoHardCapTicks = 900;
		public const int YoyoLocalHitCooldownTicks = 10, YoyoHitboxPx = 22;
		public const float YoyoSpinPerTick = 0.5f;
		/// <summary>Fraction of the reach at which a yoyo soldier holds off its target (like a lash), and the score scale against raw DPS.</summary>
		public const float YoyoHoldFraction = 0.75f, YoyoScoreFactor = 0.8f;

		// ---------- Flails ----------
		/// <summary>Flails one soldier may have out at once.</summary>
		public const int FlailMaxOut = 1;
		/// <summary>Reach (px) is shootSpeed x this, clamped into the band; engagement range band in tiles.</summary>
		public const float FlailReachPerShootSpeed = 11f, FlailReachMinPx = 90f, FlailReachMaxPx = 200f;
		public const int FlailRangeMinTiles = 4, FlailRangeMaxTiles = 13;
		/// <summary>Throw life in ticks is the item's use animation clamped into this band; the ball runs out over the first fraction, swings at the tip, then is hauled back.</summary>
		public const int FlailLifeMinTicks = 28, FlailLifeMaxTicks = 72;
		public const float FlailExtendFraction = 0.4f, FlailSwingFraction = 0.2f;
		/// <summary>Radius (px) of the swing the ball makes at the tip, spin per tick (radians), hit box and local hit cooldown.</summary>
		public const float FlailSwingRadiusPx = 18f, FlailSpinPerTick = 0.35f;
		public const int FlailHitboxPx = 30, FlailLocalHitCooldownTicks = 14;
		/// <summary>Fraction of the reach at which a flail soldier holds off its target, and the score scale against raw DPS.</summary>
		public const float FlailHoldFraction = 0.8f, FlailScoreFactor = 0.8f;
	}
}
