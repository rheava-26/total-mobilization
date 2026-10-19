namespace Levy.Common
{
	/// <summary>Soldier-fired Flying Knife numbers: one knife per soldier hovers on the soldier's current target for as long as it keeps fighting, and returns to the soldier once it stops. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---- Soldier Flying Knife ----
		/// <summary>Attack phase: homing acceleration (px/tick per tick) and top speed (px/tick). There is no timeout while the soldier fights.</summary>
		public const float KnifeSeekAccel = 1.0f, KnifeSeekMaxSpeed = 17f;
		/// <summary>Return phase: homing acceleration, top speed (px/tick), and the distance to the soldier (px) at which the knife is caught and despawns.</summary>
		public const float KnifeReturnAccel = 1.0f, KnifeReturnMaxSpeed = 19f, KnifeReturnCatchPx = 20f;
		/// <summary>Blade spin per tick (radians).</summary>
		public const float KnifeSpinPerTick = 0.45f;
		/// <summary>Ticks the knife coasts after its soldier is gone before despawning.</summary>
		public const int KnifeOrphanGraceTicks = 20;
		/// <summary>Ticks the knife hovers with no target (soldier still fighting) before heading home, so a kill followed by a new acquisition never sends it back.</summary>
		public const int KnifeLoseTargetGraceTicks = 30;
		/// <summary>Defensive backstop (ticks, 3 minutes): cleans up a stuck knife only; not a normal lifetime.</summary>
		public const int KnifeSafetyMaxTicks = 10800;

		// ---- Homing authority (Flying Knife + guided missiles): turn rate grows as the projectile closes so it converges on the enemy instead of orbiting ----
		/// <summary>Max heading change per tick (radians) when far from / right on top of the destination; the turn authority blends between them over HomeNearRangePx.</summary>
		public const float HomeTurnFar = 0.16f, HomeTurnNear = 0.55f, HomeNearRangePx = 180f;
		/// <summary>The knife never drops below this speed (px/tick) while turning back onto a target it overshot.</summary>
		public const float KnifeMinSpeed = 7f;
		/// <summary>Guided missiles: speed ramps between these multiples of their launch speed; Accel is px/tick per tick.</summary>
		public const float GuidedMinSpeedMul = 0.6f, GuidedMaxSpeedMul = 1.6f, GuidedAccel = 0.45f, GuidedNearRangePx = 220f;

		// ---- Soul Drain bolt ----
		/// <summary>How far from the soldier (px) the drain bolt can be dropped on its target.</summary>
		public const float SoulDrainReachPx = 800f;
		/// <summary>HP the soldier recovers when a drain bolt connects.</summary>
		public const int SoulDrainHealPerHit = 1;

		// ---- Medusa Head ----
		/// <summary>Ticks a cast keeps the soldier's head alive (a fresh cast refreshes it).</summary>
		public const int MedusaHeadLingerTicks = 31;
		/// <summary>Pulse reach (px), ticks between pulse windows, and max enemies a pulse strikes.</summary>
		public const float MedusaRangePx = 320f;
		public const int MedusaPulseTicks = 60, MedusaMaxRays = 3;
		/// <summary>A driver-based magic weapon engages at this fraction of its real reach so the soldier is inside it when he fires.</summary>
		public const float DriverReachFraction = 0.9f;
	}
}
