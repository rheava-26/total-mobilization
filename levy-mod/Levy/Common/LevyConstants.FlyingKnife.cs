namespace Levy.Common
{
	/// <summary>Soldier-fired Flying Knife numbers: the knife seeks its soldier's target, darts around it, then returns to the soldier. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---- Soldier Flying Knife ----
		/// <summary>Seek phase: homing acceleration (px/tick per tick), top speed (px/tick) and length (ticks) before the knife peels off for home.</summary>
		public const float KnifeSeekAccel = 0.7f, KnifeSeekMaxSpeed = 15f;
		public const int KnifeSeekTicks = 75;
		/// <summary>Return phase: homing acceleration, top speed (px/tick), and the distance to the soldier (px) at which the knife is caught and despawns.</summary>
		public const float KnifeReturnAccel = 1.0f, KnifeReturnMaxSpeed = 19f, KnifeReturnCatchPx = 20f;
		/// <summary>Blade spin per tick (radians).</summary>
		public const float KnifeSpinPerTick = 0.45f;
		/// <summary>With no soldier target, the knife looks for the nearest hostile within this many tiles of itself.</summary>
		public const float KnifeFallbackSearchTiles = 50f;
		/// <summary>Hard cap on the knife's lifetime (ticks), and the return-phase grace (ticks) it flies on after its soldier is gone.</summary>
		public const int KnifeMaxLifeTicks = 180, KnifeOrphanGraceTicks = 20;
	}
}
