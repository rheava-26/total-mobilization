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
			public const int JumpTowardGoalReachTiles = 6;      // a goal this far across and above (tiles) is jumped for, not just walked toward
			public const float JumpUpAbovePx = 20f;               // a one-tile rise is walked up (Collision.StepUp), not jumped; anything over a tile and a bit is

			// ---- eager jumping (soldiers should leave the ground whenever it helps) ----
			/// <summary>A fighting soldier jumps for a target up to this many tiles above the height its own jump reaches (its weapon covers the rest).</summary>
			public const int EngageJumpReachBonusTiles = 2;
			/// <summary>A pit this deep or deeper (tiles) with floor on the far side is jumped, not dropped into (was: the body's full jump height).</summary>
			public const int GapJumpMinDepthTiles = 2;
			/// <summary>Running hops: a soldier on the move toward a goal at least this far (tiles) hops now and then while it has open, safe ground ahead.</summary>
			public const int HopMinGoalDistTiles = 8;
			/// <summary>Ticks between running hops (random in this range).</summary>
			public const int HopIntervalMinTicks = 90, HopIntervalMaxTicks = 210;
			/// <summary>A running hop needs at least this horizontal speed (px/tick) and clears this many tiles.</summary>
			public const float HopMinSpeedPx = 1.6f;
			public const int HopTiles = 2;
			/// <summary>Columns ahead that must be open, floored and lava-free before a running hop; rows above the feet that must be clear.</summary>
			public const int HopLandingTiles = 4, HopClearRows = 4;
			/// <summary>Flight-kit soldiers engage targets this many tiles above them (the base rule asks for 7) and the climb pays off from this height.</summary>
			public const int AerialEngageMinTiles = 5;
			public const int AerialClimbBenefitTiles = 3;

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

			// ---- ordered home (ReturnToBarracks / stand-down) ----
			/// <summary>Wedged (not walking) this long while heading home: snap to the barracks muster point.</summary>
			public const int ReturnHomeStuckTeleportTicks = 60 * 8;
			/// <summary>No closer to home by <see cref="ReturnHomeProgressPx"/> for this long (a shuffle, a failed path, an unreachable muster point): snap home.</summary>
			public const int ReturnHomeNoProgressTeleportTicks = 60 * 15;
			public const float ReturnHomeProgressPx = 24f;

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

			// ---- aerial engagement (wings / extra jumps / rocket boots climbing to an airborne target) ----
			// Flight is hand-rolled on NPC.velocity: UpdateNPC adds gravity after AI, so thrust ticks subtract it first (as Fly does).
			/// <summary>Fastest climb under thrust (px/tick up). Vanilla wings cap at jumpSpeed x 1.5 = 7.5; kept lower so the climb reads as controlled.</summary>
			public const float AerialClimbSpeedPx = 5.5f;
			/// <summary>Upward acceleration of one thrust tick (px/tick per tick). Vanilla wings gain about 0.2 to 0.6.</summary>
			public const float AerialClimbAccel = 0.3f;
			/// <summary>Desired vertical speed per pixel of altitude error, so the climb eases off into the hover.</summary>
			public const float AerialClimbGain = 0.12f;
			/// <summary>Fastest controlled descent while aloft with flight budget left (px/tick down).</summary>
			public const float AerialDescendSpeedPx = 2.5f;
			/// <summary>Thrust only fires when the body is this much slower upward than wanted (px/tick); keeps a hover from flickering.</summary>
			public const float AerialThrustDeadbandPx = 0.15f;
			/// <summary>While thrusting, the vertical speed never lands on exactly 0 (that is the grounded test).</summary>
			public const float AerialMinVyPx = 0.02f;
			/// <summary>Within this of the target altitude (tiles) the body is "there": no climb, only the hover.</summary>
			public const float AltitudeToleranceTiles = 1.5f;
			/// <summary>Flight budget burned per tick of climbing thrust.</summary>
			public const float AerialClimbDrain = 1f;
			/// <summary>Flight budget burned per tick of hover feathering (vanilla wings burn 1 per tick held; tapping averages less).</summary>
			public const float AerialHoverDrain = 0.4f;
			/// <summary>Climb thrust below this vertical speed (px/tick, negative = up) counts as climbing; above it as hovering.</summary>
			public const float AerialClimbDrainBelowVy = -1f;
			/// <summary>Wing time kept in hand: below this the wings are not used to climb or hover, so a landing is always glided.</summary>
			public const int WingBudgetReserveTicks = 24;
			/// <summary>One rocket-boot burst lasts this long (vanilla rocketDelay); rocketTimeMax bursts make the whole budget.</summary>
			public const int RocketBurstTicks = 10;
			/// <summary>Fraction of a thrust source's theoretical climb that a plan can count on (acceleration, hover, drift).</summary>
			public const float AerialReachEfficiency = 0.6f;
			/// <summary>After a flight ran dry the soldier lands and waits this long before climbing again (no bouncing).</summary>
			public const int AerialRelaunchCooldownTicks = 180;
			/// <summary>Extra jump impulse (px/tick up). Peaks about 3.75 tiles higher at gravity 0.3.</summary>
			public const float AirJumpImpulsePx = 6f;
			/// <summary>An extra jump is spent only once the body is no longer rising faster than this (px/tick, negative = up): near the apex.</summary>
			public const float AirJumpApexVy = -1f;
			/// <summary>Climbs that still need less than this (tiles) do not spend an extra jump.</summary>
			public const int AirJumpMinNeedTiles = 3;
			public const int AirJumpCooldownTicks = 12;
			/// <summary>Position/velocity is re-sent this often while aloft (ticks); NPC netUpdate is otherwise event-driven.</summary>
			public const int AerialNetSyncTicks = 10;
			/// <summary>Height (px) of the strip probed above the head / under the feet to tell a ceiling bump from a landing.</summary>
			public const int CeilingProbePx = 2;
			/// <summary>Dummy flap animation holds this many ticks after the last thrust.</summary>
			public const int AerialVisualHoldTicks = 8;

			// ---- flight-assisted dodge and gaps ----
			/// <summary>Flighted soldiers hop this many tiles higher when dodging a flat shot.</summary>
			public const int FlightDodgeHopBonusTiles = 2;
			/// <summary>An airborne (aerial-mode) dodge by flap or extra jump: upward speed (px/tick).</summary>
			public const float AirDodgeBlipPx = 4.5f;
			/// <summary>Wing/rocket budget one flap-assist costs.</summary>
			public const int FlapAssistCostTicks = 8;
			/// <summary>Extra gap width (tiles) a body with extra jumps will attempt.</summary>
			public const int AirGapBonusTiles = 2;
			/// <summary>After a gap jump is requested, the in-flight assist is armed for this many ticks.</summary>
			public const int GapAssistTicks = 50;
			/// <summary>The assist only fires once the body is falling faster than this (px/tick).</summary>
			public const float GapAssistFallVy = 1f;
			/// <summary>Columns ahead searched for the far edge before the assist fires.</summary>
			public const int GapAssistLandingTiles = 3;
			/// <summary>Upward speed of a gap-assist flap that has no extra jump to spend (px/tick).</summary>
			public const float GapAssistFlapPx = 3.5f;

			// ---- gaps ----
			/// <summary>Widest gap (tiles with no floor) a body will jump across instead of falling into.</summary>
			public const int GapJumpMaxTiles = 3;
			/// <summary>Jump height (tiles) used to clear a gap: sets the airtime, so 3 carries about 40 ticks.</summary>
			public const int GapJumpTiles = 4;
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

			// ---- enemy-body dodging (worm segments, wyverns, chargers) ----
			/// <summary>A hostile NPC body is only dodged if its contact damage is at least this (filters harmless fliers).</summary>
			public const int DodgeNpcMinDamage = 20;
			/// <summary>...and only if it is moving at least this fast (px/tick). The key filter: walkers being meleed are far below it.</summary>
			public const float DodgeNpcMinSpeed = 4.5f;
			/// <summary>Hostile bodies farther than this are ignored (bigger and faster than a projectile, so a wider scan).</summary>
			public const float DodgeNpcRadiusTiles = 17f;
			/// <summary>Only bodies that would arrive within this many ticks matter.</summary>
			public const float DodgeNpcLookaheadTicks = 24f;
			/// <summary>Extra padding (px) around the two hitboxes when judging an overlap.</summary>
			public const float DodgeNpcMarginPx = 10f;
			/// <summary>Minimum horizontal component of the lane's normal for a sidestep to leave the lane; flatter charges are hopped instead.</summary>
			public const float DodgeNpcMinLaneX = 0.5f;

			// ---- dashes (Shield of Cthulhu, Tabi / Master Ninja Gear, Crystal Assassin: Player.dashType > 0) ----
			// A dash only ever speeds up a walk the soldier is already making along a flat, safe lane; it never picks a new destination.
			public const float DashSpeedPx = 8.5f;
			public const int DashBurstTicks = 9;
			public const int DashCooldownTicks = 60 * 6, DashCooldownJitterTicks = 60 * 3;
			/// <summary>The goal (or next waypoint) must be at least this far across to be worth a dash, and no farther than the max.</summary>
			public const int DashMinGoalTiles = 9, DashMaxGoalTiles = 45;
			/// <summary>The goal must be within this many tiles of level.</summary>
			public const int DashMaxRiseTiles = 1;
			/// <summary>Columns ahead that must be flat, open, floored and lava-free.</summary>
			public const int DashLaneTiles = 7;

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
