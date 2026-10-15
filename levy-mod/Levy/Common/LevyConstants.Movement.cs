namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Movement reflexes, pathfinding and travel. Pixels per tick unless the name says tiles.</summary>
		public static class Movement
		{
			// ---- speeds (a town NPC walks ~1 px/tick, a player runs 3, 6 with Hermes Boots) ----
			public const float WalkSpeedPx = 1.2f;
			public const float WalkAccel = 0.12f;
			public const float RunAccel = 0.2f;
			public const float StopDecel = 0.8f;
			public const float StopBelowPx = 0.1f;
			public const float BlockedDecel = 0.5f;
			public const int WalkWithinTiles = 6;
			public const float ArriveXPx = 4f;
			public const float ArriveYPx = 24f;
			public const float MinWalkSpeedMult = 0.5f, MaxWalkSpeedMult = 3f;
			public const float MinRunSpeed = 1f, MaxRunSpeed = 12f;

			// ---- jumping (NPC gravity 0.3/tick, terminal 10; verified in NPC.UpdateNPC_UpdateGravity) ----
			public const float DefaultGravity = 0.3f;
			public const int BaseJumpTiles = 3;
			public const float JumpBoostPerTile = 0.75f;       // Player.jumpSpeedBoost per extra tile of reach
			public const int BalloonJumpTiles = 2;              // Player.jumpBoost (Shiny Red Balloon family)
			public const float FrogLegBoost = 2.4f, EmpressBroochBoost = 1.8f, MoonLordLegsBoost = 1.8f; // added in Player.UpdateJumpHeight, not UpdateEquips
			public const int MaxJumpTiles = 12;
			public const int JumpClearancePx = 12;
			public const float MinJumpSpeed = 4.4f, MaxJumpSpeed = 12f;
			public const float JumpSpeedOffset = 0.013f;        // keeps the arc from passing through exactly 0 (the grounded test)
			public const float ExtraJumpSpeedMult = 0.9f;
			public const int JumpTowardGoalReachTiles = 3;
			public const float JumpUpAbovePx = 24f;               // a one-tile rise is walked up (Collision.StepUp), not jumped

			// ---- hazards ----
			public const int LookAheadPx = 8;
			public const int FallScanTiles = 20;
			public const int DropMarginTiles = 6;               // refuse drops deeper than JumpTiles + this (unless winged)
			public const int DeepWaterLiquid = 128;
			public const int PlatformDropBelowPx = 32;
			public const int PlatformDropTicks = 10;
			public const int DoorCloseDistanceTiles = 3;
			public const int DoorGiveUpDistanceTiles = 6;

			// ---- stuck handling ----
			public const int StuckSampleTicks = 10;
			public const float StuckMoveThresholdPx = 2f;
			public const int StuckReverseAfterExtraTicks = 30;
			public const int ReverseTicks = 20;
			public const float StuckProgressPx = 8f;            // movement per sample that counts as real walking
			public const int ReverseSettleTicks = 60;           // the reverse shuffle does not count as progress for this long

			// ---- pathfinding ----
			public const int PathRecalcTicks = 30;
			public const int PathGoalMovedTiles = 10;
			public const float WaypointReachPx = 12f;
			public const int PathWindowW = 160, PathWindowH = 100;
			public const int PathJumpReachX = 3;
			public const int PathMaxNodes = 2500;
			public const int GoalSnapTiles = 4;
			public const float JumpEdgeCost = 1.5f;
			public const float FallEdgeCost = 0.5f;
			public const float FlightCostMult = 1.3f;
			public const int PathFailRetryTicks = 45;

			// ---- wings ----
			public const float FlySpeed = 6f;
			public const float FlyAccel = 0.3f;
			public const float GlideFallSpeed = 1.5f;
			public const float FlyArriveYPx = 16f;

			// ---- gaps ----
			/// <summary>Widest gap (tiles with no floor) a body will jump across instead of falling into.</summary>
			public const int GapJumpMaxTiles = 2;
			/// <summary>Jump height (tiles) used to clear a gap: sets the airtime, so 3 carries about 40 ticks.</summary>
			public const int GapJumpTiles = 3;
			/// <summary>Rows above the feet that must be clear over the gap and the far edge.</summary>
			public const int GapJumpClearRows = 4;

			// ---- projectile dodging ----
			public const int DodgeScanIntervalTicks = 4;
			public const int DodgeCooldownTicks = 45;
			/// <summary>Hostile projectiles farther than this are ignored.</summary>
			public const float DodgeRadiusTiles = 16f;
			/// <summary>Only projectiles that would arrive within this many ticks matter.</summary>
			public const float DodgeLookaheadTicks = 30f;
			/// <summary>Closest approach (px) at which a projectile counts as a hit.</summary>
			public const float DodgeHitRadiusPx = 36f;
			public const float DodgeMinProjectileSpeed = 2f;
			public const float DodgeSidestepSpeedPx = 3.2f;
			public const int DodgeHopTiles = 2;
			public const int DodgeMinDamage = 1;
			/// <summary>A sidestep keeps its speed for this many ticks (about 1.5 tiles at 3.2 px/tick).</summary>
			public const int DodgeSidestepTicks = 8;

			// ---- bodies ----
			public const float PlayerNudgePx = 0.5f;
			public const float StepSpeed = 1f;

			// ---- travel ----
			public const int StandingSpotRadiusTiles = 12;
			public const int ScreenEdgeExtraPx = 100;
			public const int SpotColumnClearTiles = 3;
		}
	}
}
