namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Movement reflexes, pathfinding and travel. Pixels per tick unless the name says tiles.</summary>
		public static class Movement
		{
			// ---- speeds (a town NPC walks ~1 px/tick, a player runs 3, 6 with Hermes Boots) ----
			public const float WalkSpeedPx = 1.2f;
			// ---- fight zone: while the commander is in a boss fight, a levy with no explicit order stays inside this radius of the commander ----
			public const float FightZoneTiles = 46f, FightZoneBossTiles = 90f, FightZoneEdgeFrac = 0.8f;
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
			/// <summary>A player with no gear clears about this many tiles in one jump.</summary>
			public const int PlayerBaseJumpTiles = 5;
			/// <summary>The same, measured from Player.Update (15 frames at 5.01 px, then the coast under 0.4 gravity: about 6.6 tiles of rise): a gearless player clears a 6-tile row.</summary>
			public const int RowBaseJumpTiles = 6;
			/// <summary>Tiles a second jump (a bottle, a jar) adds to the reach of a goal jump.</summary>
			public const int ExtraJumpReachTiles = 3;
			/// <summary>A lane is judged by how many tiles it sits above or below the boss; past this many it is as safe as it gets.</summary>
			public const int PlatformLaneSepCap = 8, PlatformLaneNeedGain = 3, PlatformLaneColumns = 10, PlatformLaneDrop = 12;
			public const int PlatformLaneLowTiles = 5, PlatformLaneRowTiles = 3;
			public const int PlatformLaneEvalTicks = 80, PlatformLaneDwellTicks = 120;
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

			// ---- readier flight (an equipped soldier takes to the air for a target above it) ----
			// AerialClimbBenefitTiles stays at the height a plain jump reaches: below it a "climb" is only a hop, so a lower value would bunny-hop soldiers, not fly them.
			// The gates that decide how soon a soldier commits are below: the reach test, the squad share, the wait between flights and how much of the standoff the hover point holds.
			/// <summary>
			/// An equipped soldier starts its climb once the target is within weapon reach x this (it climbs while it closes), and keeps the climb up to reach x <see cref="AerialHoldRangeMult"/> (never less than the start, so the latch cannot flap).
			/// Kept modest: a ground path search cannot start from mid-air, so a soldier that is up while its target is still out of reach has no new firing spot to look for. A target hanging in the air is pursued much further out (FlyPursuitRangeMult).
			/// </summary>
			public const float AerialCommitRangeMult = 1.2f, AerialHoldRangeMult = 1.3f;
			/// <summary>Fraction of the soldiers with sustained flight (wings or rocket boots; stable per body) that take to the air after a target hanging above them. The rest fight from the ground.</summary>
			public const float AerialPursuitAppetite = 0.9f;
			/// <summary>
			/// A soldier that can hover holds only this fraction of its standoff by altitude (the hover point sits that far below a target it stands under); the rest of the standoff
			/// is kept sideways by the ordinary step-away, so a flier ends level with a boss at the standoff. 1 = the whole standoff is held by staying low.
			/// </summary>
			public const float AerialStandoffAltitudeFrac = 0.6f;
			/// <summary>A grounded soldier with a solid tile this close above its head (tiles) does not ask for altitude: it is in a room or under an overhang, and a climb would only spend the flight on the ceiling.</summary>
			public const int RoofProbeTiles = 10;

			// ---- seeking height (a soldier on foot climbs terrain toward a target far overhead) ----
			// All of it runs inside the brain's budgeted position searches (SearchMayStart / ChargeSearch) at their existing rechecks; nothing here is a per-tick scan.
			/// <summary>A target whose centre is this many tiles above the soldier's feet is "overhead": the firing-spot search looks for footing under the sky instead of on the line to it, and height keeps paying.</summary>
			public const int OverheadTargetMinTiles = 10;
			/// <summary>Merit per tile of extra height once a spot is already further below its target than PerchElevationCapTiles (no upper bound: what matters is the difference between two spots). Under 1, so tiles near the target stay the most valuable.</summary>
			public const float OverheadElevationWeight = 0.75f;
			/// <summary>Footing is looked for this many tiles below a mid-air sample point when no standable tile lies within EngageSpotSearchRadius of it (stops at the first solid ground).</summary>
			public const int OverheadDropScanTiles = 70;
			/// <summary>Columns searched either side of the soldier for a perch against an overhead target (the usual reach is PerchSearchRadiusTiles).</summary>
			public const int OverheadPerchRadiusTiles = 28;
			/// <summary>Best-scoring perches kept against an overhead target (usual PerchCandidates), and how many of those perches, and of the approach spots, get a path search (usual PerchPathChecks / EngageSpotCandidates).</summary>
			public const int OverheadPerchCandidates = 6, OverheadPerchPathChecks = 2, OverheadApproachPathChecks = 2;
			/// <summary>
			/// Node cap of those path searches (usual PathMaxNodes): a climb that needs a long detour is not worth a failed search. About 2 microseconds a node, so a failure costs at most
			/// 1.2 ms and one whole search at most OverheadPerchPathChecks x that; the usual caps allow 5 ms a check.
			/// </summary>
			public const int OverheadPathMaxNodes = 600;
			/// <summary>After an open perch search against an overhead target vetted spots and reached none of them, the next one waits this long (ticks; the usual recheck is EngageSpotRecheckTicks).</summary>
			public const int OverheadFailRecheckTicks = 60 * 4;
			/// <summary>A soldier on foot climbs about this many tiles per column of slope on top of its jump; a spot steeper than that loses <see cref="SteepClimbPenalty"/> merit per excess tile (overhead searches only).</summary>
			public const float WalkClimbTilesPerColumn = 0.5f;
			public const float SteepClimbPenalty = 0.6f;
			/// <summary>Perches a path search could not reach are remembered (this many, each for this long) and not tried again, so the next search tries the next best instead of repeating the failure. A spot within PerchBadRadiusTiles of one counts as it.</summary>
			public const int PerchBadMemory = 8, PerchBadMemoryTicks = 60 * 40, PerchBadRadiusTiles = 3;
			/// <summary>The memory belongs to one patch of ground: a soldier that has moved this many columns from where it was recorded forgets it (a cliff top out of reach from the valley may be easy from the hill).</summary>
			public const int PerchBadForgetMoveTiles = 12;

			// ---- goal-jump debounce (only a jump that keeps landing without gaining height; a clean ledge jump never meets these) ----
			/// <summary>After a goal-jump that did not gain height, the next one waits until the goal is this close (tiles across) instead of the full reach.</summary>
			public const int GoalJumpRetryReachTiles = 2;
			/// <summary>This many goal-jumps in a row without gaining <see cref="GoalJumpGainPx"/> of height starts a hold.</summary>
			public const int GoalJumpStreakMax = 3;
			public const float GoalJumpGainPx = 12f;
			/// <summary>No goal-jumps at all for this long after a futile streak (walls, gaps and running hops are unaffected).</summary>
			public const int GoalJumpHoldTicks = 180;
			/// <summary>A streak is forgotten this long after its last jump (longer than the hold, so the retry rule survives it).</summary>
			public const int GoalJumpStreakResetTicks = 300;
			/// <summary>A goal-jump is skipped while a ceiling is this close above the head (px): the body would only bump it.</summary>
			public const int GoalJumpHeadroomPx = 32;

			// ---- hazards ----
			public const int LookAheadPx = 8;
			public const int FallScanTiles = 20;
			/// <summary>Route searches of bodies that a fall cannot hurt (no fall damage in the config, or wings or a horseshoe) take a fall down a shaft of up to this many tiles, the part beyond FallScanTiles at LongFallEdgeCost a tile (vertical movement is the cheapest kind).</summary>
			public const int LongFallTiles = 1600; public const float LongFallEdgeCost = 0.2f;
			public const int DropMarginTiles = 6;               // refuse drops deeper than JumpTiles + this (unless winged)
			public const int DeepWaterLiquid = 128;
			/// <summary>Swimming up (see SoldierNPC.SwimAssist): upward acceleration and top speed in px per tick, and how far above the feet the goal must be (px).</summary>
			public const float SwimAccel = 0.35f, SwimMaxUp = 3f, SwimUpDyPx = 16f;
			public const int PlatformDropBelowPx = 32;
			public const int PlatformDropTicks = 10;
			/// <summary>The door custodian keeps trying to shut a door a levy or officer opened for this long (30 s), checks every DoorCustodianIntervalTicks, and in the last DoorCustodianStrictTicks closes it even with someone beside it (only a body in the doorway holds it then).</summary>
			public const int DoorCustodianTicks = 1800, DoorCustodianIntervalTicks = 10, DoorCustodianStrictTicks = 300;
			/// <summary>Tiles around the doorway column that keep a registered door open while a player, officer, town NPC or levy stands in them.</summary>
			public const int DoorCustodianMarginTiles = 1;
			/// <summary>A levy that just opened a door does not dash or hop for this long (a dash would carry it back into the leaf).</summary>
			public const int DoorBusyTicks = 90;

			// ---- stuck handling ----
			public const int StuckSampleTicks = 10;
			public const float StuckMoveThresholdPx = 2f;
			public const int StuckReverseAfterExtraTicks = 30;
			public const int ReverseTicks = 20;
			public const float StuckProgressPx = 8f;            // movement per sample that counts as real walking
			public const int ReverseSettleTicks = 60;           // the reverse shuffle does not count as progress for this long

			// ---- wedge detector (long-window net displacement; catches bounce-in-place that the 10-tick sampler reads as movement) ----
			/// <summary>Net displacement is judged over this window while the body is trying to walk (ticks).</summary>
			public const int WedgeWindowTicks = 90;
			/// <summary>A window that ends less than this far (px) from where it began counts as wedged (a slow walk covers 50+).</summary>
			public const float WedgeNetPx = 20f;
			/// <summary>Wedged windows in a row before a fresh path is forced (invisible), and before a following soldier is leashed to its commander.</summary>
			public const int WedgeRepathWindows = 2, WedgeTeleportWindows = 4;

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
			public const float FlightCostMult = 0.9f;
			/// <summary>Share of the straight-line range of a full wing tank (WingTimeMax ticks at FlySpeed) a planned flight may use before it must touch down (the climb and the wind-up take the rest).</summary>
			public const float FlightRangeShare = 0.8f;
			public const int PathFailRetryTicks = 45;

			// ---- long routes (RoutePlanner / SoldierNPC.Route.cs) ----
			// One shared, sliced A* over the whole trip (start..goal bounding box) per (goal, ability class, start bucket); soldiers follow its waypoints with the usual local reflexes.
			/// <summary>Search buffer: the widest and tallest trip window (tiles). A trip wider than this is walked in hops toward the goal (the far end is out of the window).</summary>
			public const int RouteWindowW = 1500, RouteWindowH = 1900, RouteWindowCells = 1000000;
			/// <summary>The most margin (tiles) a long trip's window gets beyond the box of start and goal: a third of the trip's longer side, up to this much sideways and up (down gets 60 more).</summary>
			public const int RouteMaxPadX = 240, RouteMaxPadY = 240;
			/// <summary>Nodes expanded per game tick by the planner (about 2 microseconds a node: 1.4 ms) and the most one route may use before it settles for the closest point it reached.</summary>
			public const int RouteNodesPerTick = 700, RouteMaxNodes = 220000;
			/// <summary>A planner slice also stops after this many ms (checked every RouteChunkNodes nodes).</summary>
			public const float RouteSliceMs = 1.6f; public const int RouteChunkNodes = 25;
			/// <summary>Weight on the heuristic: above 1 the search commits toward the goal and expands far fewer tiles (routes are a little longer than optimal).</summary>
			public const float RouteHWeight = 1.7f;
			/// <summary>Soldiers within the same bucket (tiles) of one another share a route; routes are kept this long (ticks) and at most this many are cached.</summary>
			public const int RouteStartBucketTiles = 12, RouteTtlTicks = 60 * 240, RouteCacheMax = 48, RouteQueueMax = 24;
			/// <summary>A failed or partial route is searched again after this long (ticks) when somebody asks again.</summary>
			public const int RouteRetryTicks = 60 * 6;
			/// <summary>How far ahead along the route (tiles of waypoint length) a soldier aims, and how far off the route (tiles) it may be before it asks for a fresh route from where it stands.</summary>
			public const int RouteLookaheadTiles = 26, RouteOffRouteTiles = 12;
			/// <summary>No progress for this many ticks while following a route: ask for a fresh route from here (at most RouteReplanMax times per order), then fall back to the old hop-toward-the-goal walk.</summary>
			public const int RouteStallTicks = 60 * 8, RouteReplanMax = 3;
			/// <summary>A planned route climbs by wing when the next waypoint is this far above (px).</summary>
			public const float RouteFlyRisePx = 40f;
			/// <summary>A trip is routed when the goal is farther than this (tiles) in x, or this much above or below the feet; a trip wider than RouteMaxHopTiles is routed in hops.</summary>
			public const int RouteMinTripTiles = 24, RouteMinTripRise = 10, RouteMaxHopTiles = 1100;
			// ---- hooks in routes ----
			/// <summary>A route uses a hook only when the item reaches at least this many tiles. A hook edge reaches up to HookDirectShare of the reach (HookRiseShare of it straight up); a chain of casts climbs a wall face up to HookChainShare of the reach tall.</summary>
			public const float HookMinReachTiles = 8f, HookDirectShare = 0.88f, HookRiseShare = 0.75f, HookChainShare = 3.2f;
			/// <summary>Fan rays the route search casts from a cell to find anchors, how many columns from a target an anchor may be, and the search cost of a cast (plus per tile of reach).</summary>
			public const int HookFanRays = 24, HookAnchorNearCols = 3;
			public const float HookEdgeCost = 7f, HookEdgeDistCost = 0.5f;
			/// <summary>A chain of casts up a face or across a gap: the base cost and the cost per tile of face (a link gains about half the reach in about 40 ticks, a little slower than a run).</summary>
			public const float HookChainBaseCost = 8f, HookChainTileCost = 1.3f;
			/// <summary>How much farther than the cast reaches (px) a far wall may be when the body jumps out over the gap first (the arc carries it about this far before the cast from the air).</summary>
			public const float HookJumpCastPx = 64f;
			/// <summary>Failed hook targets remembered per soldier (each is banned from the route search), ticks the executor waits at the cast point for the hook planner before it gives the edge up, and the spacing of its tries (three per edge).</summary>
			public const int RouteHookBanMax = 6, RouteHookWaitTicks = 420, RouteHookRetryTicks = 45;

			// ---- wall climb gear (Shoe Spikes, Climbing Claws, Tiger Climbing Gear, Master Ninja Gear: Player.spikedBoots) ----
			/// <summary>A climb edge in the route search: the base cost plus this per tile of face (a wall-jump cycle gains about a jump of height every 25 ticks, about the pace of a run).</summary>
			public const float ClimbEdgeCost = 3f, ClimbTileCost = 1.1f;
			/// <summary>Tallest face a climb edge looks up (tiles).</summary>
			public const int ClimbMaxFaceTiles = 380;
			/// <summary>The kick off the wall when a wall jump starts (px/tick, Player: 3), the acceleration back toward the wall, and the speed pressed into it.</summary>
			public const float ClimbKickPx = 3f, ClimbReturnAccel = 0.28f, ClimbPressPx = 1.6f;
			/// <summary>A climb that has not gained a tile of height in this many ticks, or takes longer than ClimbMaxTicks in all, is given up (the edge is banned and the route searched again).</summary>
			public const int ClimbStallTicks = 150, ClimbMaxTicks = 60 * 90;
			/// <summary>Slide speed on a wall with weak spikes (spikedBoots 1); stronger spikes (2: Tiger Climbing Gear, Master Ninja Gear) hold the body still.</summary>
			public const float ClimbSlideSpeedPx = 0.5f;
			/// <summary>Ticks the body hangs on the wall after a catch before it jumps off (a player releases and presses jump).</summary>
			public const int ClimbCatchTicks = 3;

			// ---- off-screen catch-up for map-point orders ----
			/// <summary>A map-order levy that no player can see and that has stayed within CatchUpStuckTiles of one spot for this many ticks hops ahead along its route.</summary>
			public const int CatchUpStuckTicks = 60 * 6; public const float CatchUpStuckTiles = 5f;
			/// <summary>Or has not got this many tiles nearer the goal (best straight distance) in this many ticks: a body looping casts in a pit moves, but is stuck all the same.</summary>
			/// <summary>A body falling toward lava steers in the air by this much per tick, and spends an extra jump or a flap once the lava is this many ticks away.</summary>
			public const float LavaSwimAccel = 0.7f, LavaSwimMaxUp = 5f;
			public const float AirSteerAccelPx = 0.3f; public const int AirLavaKickTicks = 40;
			public const int CatchUpNoProgressTicks = 60 * 15; public const float CatchUpProgressTiles = 8f;
			/// <summary>Nobody may see the levy, or the place it lands: the box around each active player (px, full width and height).</summary>
			public const float CatchUpBoxW = 2400f, CatchUpBoxH = 1400f;
			/// <summary>A hop lands at least this far ahead (tiles of straight line) or is not made; a spot at the goal is searched this many tiles around it.</summary>
			public const int CatchUpMinHopTiles = 10, CatchUpGoalSpotTiles = 5, CatchUpWaySpotTiles = 6;

			// ---- flight arcs (a winged body on a planned route flies from one standing cell to another) ----
			/// <summary>An arc is: straight up from the takeoff cell to a cruise row, level along that row, then a straight descent line to the landing cell. It is planned with the powered speed ArcSpeedPx and a share of the wing tank (the climb and the cruise must fit; the descent line is flown on thrust while the tank lasts and glided after).</summary>
			public const float ArcSpeedPx = 6f, ArcFuelShare = 0.8f;
			/// <summary>Ticks of wing time every arc spends on top of its path (the takeoff and the turn at the cruise row are not at full speed).</summary>
			public const int ArcOverheadTicks = 8;
			/// <summary>Longest cruise (tiles), deepest landing below the cruise row, and widest descent (columns) an arc looks at.</summary>
			public const int ArcMaxCruiseTiles = 64, ArcMaxDescentRows = 40, ArcMaxGlideCols = 90;
			/// <summary>Rows above the takeoff cell the cruise row may be, and the rows kept clear between the cruise row and the ground under it.</summary>
			public static readonly int[] ArcApex = { 3, 6, 10, 16, 24, 34 };
			public const int ArcClearAboveFloor = 3;
			/// <summary>A standing cell with a wall or no floor this many tiles ahead is a launch spot.</summary>
			public const int ArcLaunchLook = 4, ArcLaunchWallRows = 3;
			/// <summary>Nodes the arc search may spend before the route search starts again with the open-air flight grid (arc routes need a few hundred).</summary>
			public const int ArcPassMaxNodes = 30000;
			/// <summary>Search cost of an arc: a takeoff, and the share of the walking cost its flight time is charged at (below 1: flight is faster than walking, so it is preferred).</summary>
			public const float ArcTakeoffCost = 2f, ArcPreference = 0.85f;
			/// <summary>Horizontal tiles a glide covers per tile it falls: run speed over the glide fall speed, kept at this share, and clamped.</summary>
			public const float ArcGlideSpeedShare = 0.8f, ArcGlideRatioMin = 1.6f, ArcGlideRatioMax = 5f;
			/// <summary>The route keeps the body flying (thrust while the tank lasts) for this many ticks after the last time the route asked for it.</summary>
			public const int RouteFlyHoldTicks = 24;
			/// <summary>A winged body that has been flying this long (ticks) shouts once; at most ShoutMaxAtOnce shouts show at a time, each blocks its slot for ShoutSlotTicks.</summary>
			public const int ShoutAfterFlyTicks = 90, ShoutMaxAtOnce = 3, ShoutSlotTicks = 240;

			// ---- wings ----
			public const float FlySpeed = 6f;
			public const float FlyAccel = 0.3f;
			/// <summary>Thrust acceleration (px/tick per tick) while a body flies a planned arc.</summary>
			public const float RouteFlyAccel = 0.75f;
			public const float GlideFallSpeed = 1.5f;
			public const float FlyArriveYPx = 16f;
			// Hold-to-accelerate: the longer a soldier keeps flying, the faster it flies, up to a peak set by its wings
			// (the wing's own horizontal speed, Player.GetWingStats(wingsLogic).AccRunSpeedOverride). FlySpeed is the floor, never a nerf.
			/// <summary>Ticks of continuous flight to reach the peak speed (linear ramp). Ground contact or any end of flight restarts it.</summary>
			public const int FlyRampTicks = 75;
			/// <summary>Peak speed as a multiple of FlySpeed for a reference wing (FlyWingRefRunSpeed), before the per-wing scaling below.</summary>
			public const float FlyRampPeakMult = 1.3f;
			/// <summary>Wing horizontal speed (px/tick) that earns exactly FlyRampPeakMult: vanilla starter wings (Angel / Demon, 6.25).</summary>
			public const float FlyWingRefRunSpeed = 6.25f;
			/// <summary>Extra peak multiple per px/tick of wing horizontal speed above the reference (Solar / Nebula wings, 9, reach about 1.63).</summary>
			public const float FlyRampPeakPerRunSpeed = 0.12f;
			/// <summary>Peak multiple clamp: never below 1 (weak wings fly at the flat FlySpeed), never above the cap (modded wings).</summary>
			public const float FlyRampPeakMinMult = 1f, FlyRampPeakMaxMult = 1.8f;
			/// <summary>Within this distance of the goal the ramp bonus fades linearly to nothing, so the body is back at plain FlySpeed on arrival.</summary>
			public const float FlyRampArriveDampPx = 200f;
			/// <summary>Share of the ramp bonus applied to the vertical-climb branch (flyUp), which stays controlled.</summary>
			public const float FlyUpRampShare = 0.35f;

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
			/// <summary>After a flight ran dry the soldier lands and waits this long before climbing again (no bouncing). Wings refill on touching down, so the wait is only to stop a landing-and-launching flutter.</summary>
			public const int AerialRelaunchCooldownTicks = 90;
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
			/// <summary>Gap tiles a running jump gains per px/tick of run speed (Hermes Boots 6.75 px/tick: 8 tiles; bare feet 3 px/tick: 3).</summary>
			public const float GapTilesPerRunSpeed = 1.2f;
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
			/// <summary>A body that hits for less than this is not worth a dodge. A boss at 15 contact damage is still 6% of a young levy's life, per tick of overlap.</summary>
			public const int DodgeNpcMinDamageMobile = 6;
			/// <summary>...and only if it is moving at least this fast (px/tick). The key filter: walkers being meleed are far below it.</summary>
			public const float DodgeNpcMinSpeed = 4.5f;
			/// <summary>A boss, or a body with at least <see cref="DodgeHeavyDamage"/> contact damage, counts as a threat from this speed (px/tick) up.</summary>
			public const float DodgeHeavyMinSpeed = 1.4f;
			public const int DodgeHeavyDamage = 40;
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
			/// <summary>A dodge dash carries the body through a hit like a player's dash does: invulnerable for the burst and a moment after (0 turns it off).</summary>
			public const int DashIFrameTicks = 12;
			public const int DashCooldownTicks = 60 * 6, DashCooldownJitterTicks = 60 * 3;
			/// <summary>The goal (or next waypoint) must be at least this far across to be worth a dash, and no farther than the max.</summary>
			public const int DashMinGoalTiles = 9, DashMaxGoalTiles = 45;
			/// <summary>The goal must be within this many tiles of level.</summary>
			public const int DashMaxRiseTiles = 1;
			/// <summary>Columns ahead that must be flat, open, floored and lava-free.</summary>
			public const int DashLaneTiles = 7;

			// ---- movement visuals (cosmetic only: dust, gore and sound that mirror the movement state; never read back by movement) ----
			/// <summary>Boot flame / wing dust cadence (ticks). Vanilla emits every tick of a thrust; half the rate reads the same and halves the dust.</summary>
			public const int FxRocketEveryTicks = 2, FxWingEveryTicks = 2;
			/// <summary>Flame / flap visuals hold this many ticks after the last thrust tick (a rocket burst lasts 10 in vanilla).</summary>
			public const int FxRocketHoldTicks = 6, FxWingHoldTicks = 4;
			/// <summary>Boots sound spacing while thrusting (ticks): vanilla uses 30 for Rocket Boots, 15 for the rest.</summary>
			public const int FxBootSoundSlowTicks = 30, FxBootSoundFastTicks = 15;
			/// <summary>Run dust (Hermes / Lightning Boots style) cadence, the speed it needs (px/tick; a bootless walk tops out near 3.3) and the minimum spacing of the step sound.</summary>
			public const int FxRunEveryTicks = 3, FxRunSoundTicks = 14;
			public const float FxRunMinSpeedPx = 3.6f;
			/// <summary>Extra-jump trail (Blizzard / Sandstorm / Tsunami / Cloud streams): base length in ticks (vanilla jump height), cap, and cadence.</summary>
			public const int FxTrailBaseTicks = 15, FxTrailMaxTicks = 60, FxTrailEveryTicks = 2;
			/// <summary>Dust in the burst at the start of a dash (vanilla 20) and ticks the afterimages linger after the burst.</summary>
			public const int FxDashBurstDust = 12, FxDashShadowTail = 6;
			/// <summary>Multiplayer clients cannot see the server's flight state: rising below this vertical speed (px/tick) with wings or boots reads as thrust, a ground speed this high as a dash, and a mid-air upward kick this big as an extra jump.</summary>
			public const float FxClientThrustMaxVy = 0.5f, FxClientDashSpeedPx = DashSpeedPx * 0.92f, FxClientJumpKickPx = 3f;
			/// <summary>Most visual emits (all soldiers together) allowed per game tick; the rest wait for the next cadence slot.</summary>
			public const int FxBudgetPerTick = 24;

			// ---- bodies ----
			public const float PlayerNudgePx = 0.5f;
			public const float StepSpeed = 1f;

			// ---- travel ----
			public const int StandingSpotRadiusTiles = 12;
			public const int ScreenEdgeExtraPx = 100;
			public const int SpotColumnClearTiles = 3;
			/// <summary>A catch-up teleport that finds no standing spot in the ordinary radius widens to this many columns either side and this many rows down / up (ground under a commander on a ceiling, over a chasm, in a sky house).</summary>
			public const int SafeGroundColumns = 30, SafeGroundRowsDown = 110, SafeGroundRowsUp = 40;
			/// <summary>After any teleport a body takes no fall damage and starts no fall count for this long (ticks) or until it lands.</summary>
			public const int TeleportFallImmunityTicks = 240;
		}
	}
}
