namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Worm momentum (SoldierNPC.WormMomentum.cs): a head that has just passed is committed to its heading and cannot come back at once, so the levies behind it get a firing window.</summary>
		public static class WormMomentum
		{
			/// <summary>The turn rate (rad per tick) a head is assumed to be able to make (the measured rate raises it).</summary>
			public const float TurnRate = 0.05f;
			/// <summary>Speed (px per tick) a head is assumed to have at the least when estimating its return.</summary>
			public const float MinSpeed = 6f;
			/// <summary>Extra ticks a head already turning one way needs to start turning the other.</summary>
			public const float ReverseTicks = 18f;
			/// <summary>The window is open while the head cannot threaten the levy for at least this many ticks.</summary>
			public const float WindowMinTicks = 50f;
			/// <summary>The head must be this far (px) past the levy along its heading for it to count as having passed.</summary>
			public const float BehindMarginPx = 48f;
			/// <summary>Heads farther than this (tiles) are ignored.</summary>
			public const float RangeTiles = 70f;
			/// <summary>Largest shift (tiles) toward the side the head must turn the long way round to reach, and the gain (ticks) it must buy.</summary>
			public const int ShiftTiles = 8;
			public const float ShiftGainTicks = 10f;
			/// <summary>Ticks a started shift is left alone before the next look.</summary>
			public const int ShiftHoldTicks = 24;
			/// <summary>A piercing weapon aims at the body segment whose line runs most along the shot, within this many tiles of the nearest segment, and only among segments behind the head.</summary>
			public const float AimSlackTiles = 6f;
			public const float AlignWeight = 0.8f;
		}
	}
}
