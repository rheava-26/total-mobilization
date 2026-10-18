namespace Levy.Common
{
	/// <summary>Soldier-fired Flying Knife numbers: one knife per soldier hovers on the soldier's current target for as long as it keeps fighting, and returns to the soldier once it stops. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---- Soldier Flying Knife ----
		/// <summary>Attack phase: homing acceleration (px/tick per tick) and top speed (px/tick). There is no timeout while the soldier fights.</summary>
		public const float KnifeSeekAccel = 0.7f, KnifeSeekMaxSpeed = 15f;
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
	}
}
