namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Grappling hooks: when a levy casts, how it reels and releases, and what the planner searches. Pixels per tick unless the name says tiles.</summary>
		public static class Hooks
		{
			// ---- triggers: ticks a blockage must hold (and the walking AI must have found no path) before the soldier plans a cast ----
			public const int TriggerGoalAboveTicks = 40, TriggerWallTicks = 40, TriggerGapTicks = 40, TriggerFleeTicks = 12, TriggerAltitudeTicks = 30, TriggerFollowTicks = 10;
			/// <summary>Ticks the trigger flag lives after the last tick a blocked branch set it (the branches run only while the soldier walks at the obstacle).</summary>
			public const int TriggerGraceTicks = 6;

			// ---- state machine ----
			/// <summary>Ticks to wait for the first hook to latch before the cast is abandoned.</summary>
			public const int LatchTimeoutTicks = 45;
			/// <summary>Spacing between the casts of a multi-hook plan (a default hook cannot be recast until the one in flight latched, so a cast is retried each tick).</summary>
			public const int CastSpacingTicks = 4;
			public const int ReelMaxTicks = 160;
			/// <summary>Reeling with no progress toward the anchor for this long ends the reel (a stand-off hook, a body wedged on a wall).</summary>
			public const int ReelStallTicks = 36;
			public const float ReelProgressPx = 0.5f;
			/// <summary>Longest a soldier hangs on the hook for an altitude plan before it lets go and drops.</summary>
			public const int HangMaxTicks = 300;
			public const int BallisticMaxTicks = 420;
			public const int CooldownTicks = 90, FailRetryTicks = 150, FailRetryMaxTicks = 1200;
			/// <summary>Air drag on horizontal speed after a release (a player with no input in the air loses runSlowdown * 0.5 per tick).</summary>
			public const float AirDragPx = 0.1f;
			public const float TerminalVelocityPx = 10f;
			/// <summary>The release is taken when the body is within this many px of the planned release distance to the anchors.</summary>
			public const float ReleaseDistSlackPx = 10f;
			/// <summary>A teleport by a hook (Hook of Dissonance) is any move of the dummy over this many px inside one hook update.</summary>
			public const float HookTeleportPx = 40f;

			// ---- planner ----
			public const int FanRays = 24, ReleaseCandidates = 7, MaxAnchorsSimulated = 8, MaxHooksPerPlan = 2;
			public const int PlanReelSimTicks = 130, PlanBallisticSimTicks = 170;
			/// <summary>A landing this many tiles below where the soldier started is a deep drop and is refused (unless the goal itself is lower).</summary>
			public const int MaxDropBelowStartTiles = 12;
			/// <summary>A plan has to land this much closer to the goal (tiles) than the soldier is now, or end within <see cref="ArriveTiles"/> of it.</summary>
			public const float MinGainTiles = 4f, ArriveTiles = 3f;
			/// <summary>Plans started in one tick across the whole army (the search is the costly part).</summary>
			public const int PlansPerTick = 1;
			/// <summary>Cheapest hook a soldier will bother to carry (reach tiles).</summary>
			public const float MinUsefulReachTiles = 8f;
			/// <summary>A soldier whose target hangs this many tiles above it climbs by hook (no flight kit).</summary>
			public const int AltitudeMinTiles = 6;
			/// <summary>A commander on a hook this far off (tiles, across or half that above) is followed by hook.</summary>
			public const float FollowMinTiles = 14f;
			/// <summary>Simulated reel ticks a plan may spend touching sloped tiles before it is judged a drag (a brush against an edge is fine).</summary>
			public const int MaxSlopeDragTicks = 4;
			/// <summary>Distances (tiles) a soldier backs off from a wall to look for a diagonal line over its lip, and how long it may take.</summary>
			public static readonly int[] StepBackTiles = { 3, 5, 8, 11 };
			public const int StepBackMaxTicks = 120;
		}
	}
}
