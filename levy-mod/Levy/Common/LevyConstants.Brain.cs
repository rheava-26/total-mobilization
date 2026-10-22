namespace Levy.Common
{
	/// <summary>Numbers the soldier brain (SoldierNPC.Brain.cs) adds to the shared constants. Distances in tiles unless named px.</summary>
	public static partial class LevyConstants
	{
		// ---------- Engage ----------
		/// <summary>Melee soldiers look for work this far out; their weapon reach (RangeMelee) only decides when to swing.</summary>
		public const int MeleeAcquireRangeTiles = 20;
		/// <summary>Followers never chase farther than this from their commander.</summary>
		public const int FollowChaseLimit = 30;
		public const int EngageBarkCooldownTicks = 60 * 20;
		/// <summary>How far one step away from a hostile that got inside the standoff reaches.</summary>
		public const int KiteStepAwayTiles = 6;
		public const int StrafeTiles = 2;
		public const int MeleeStepBackWindowTicks = 12;
		/// <summary>A held target may drift to range x this before it is dropped.</summary>
		public const float TargetKeepRangeMult = 1.5f;

		// ---------- Ranged standoff (gun / bow / magic never walk into contact) ----------
		/// <summary>Floor of the standoff: ranged soldiers step away from any hostile closer than this (hitbox to body, tiles).</summary>
		public const int KiteMin = 14;
		/// <summary>Ceiling of the distance a ranged soldier walks out to when it has to close on a target.</summary>
		public const int KiteMax = 64;
		/// <summary>The standoff scales with weapon reach: reach x this (but never under KiteMin).</summary>
		public const float StandoffRangeFrac = 0.30f;
		/// <summary>The standoff never exceeds reach x this, so a short weapon still has room to shoot.</summary>
		public const float StandoffMaxRangeFrac = 0.60f;
		/// <summary>When closing on a target, stop at reach x this (capped by KiteMax).</summary>
		public const float KiteMaxRangeFrac = 0.90f;
		/// <summary>A target inside reach x this with a clear shot is simply shot at: no approach, no walking toward it.</summary>
		public const float ShootableRangeFrac = 0.98f;
		// ---------- Flamethrower family (short-reach extended guns whose shot is the Flamethrower AI) ----------
		/// <summary>A flamethrower soldier opens fire (and picks targets) at reach x this, well past the bare flame reach, so the stream is already running as it closes.</summary>
		public const float FlameOpenFireBuffer = 1.35f;
		/// <summary>A flamethrower soldier plans its position at reach x this (it walks in to about 0.9 of this and holds inside it), so the flames actually land.</summary>
		public const float FlameCloseInFraction = 0.70f;
		/// <summary>Floor of a flamethrower's standoff: it only steps away from a hostile inside this many tiles (a gunner's standoff would keep it out of its own reach).</summary>
		public const int FlameStandoffTiles = 2;
		/// <summary>Once retreating, a ranged soldier keeps backing off until the threat is this far past the standoff (tiles).</summary>
		public const float StandoffHysteresisTiles = 2f;
		/// <summary>The in-range lateral shuffle only happens with this much room past the standoff (tiles).</summary>
		public const float StrafeMarginTiles = 6f;

		// ---------- Safe spawn / spawn grace ----------
		/// <summary>A new body is never dropped within this many tiles (to the hostile's hitbox) of a hostile NPC when a safe spot exists.</summary>
		public const float SpawnDangerRadiusTiles = 14f;
		/// <summary>The safe-spot search covers columns this far either side of the intended spawn point (tiles).</summary>
		public const int SpawnSafeSearchTiles = 40;
		/// <summary>In each searched column, floors up to this many tiles above / below the intended spawn height are considered.</summary>
		public const int SpawnSafeVerticalTiles = 10;
		/// <summary>At most this many nearby hostiles are remembered for the safe-spot test.</summary>
		public const int SpawnHostileCap = 48;
		/// <summary>A candidate on the hostile's side of the intended point is penalised by this many tiles of extra travel.</summary>
		public const float SpawnHostileSideMalusTiles = 8f;
		/// <summary>A freshly bound body does not advance on anything for this long (ticks); it backs off from close hostiles and otherwise stands.</summary>
		public const int SpawnGraceTicks = 45;
		/// <summary>During the grace a hostile closer than this (tiles) is stepped away from.</summary>
		public const float SpawnGraceKeepAwayTiles = 16f;

		// ---------- Melee caution (no suicide charges) ----------
		/// <summary>A hostile whose contact damage (after defense) is at least this fraction of the soldier's current HP is not charged by melee soldiers. Bosses always count.</summary>
		public const float MeleeLethalContactFrac = 0.40f;
		/// <summary>Melee soldiers keep this far (tiles, hitbox distance) from a hostile they will not charge. Kept under MeleeAcquireRangeTiles.</summary>
		public const float MeleeCautionTiles = 14f;

		// ---------- Unarmed soldiers (no usable weapon) ----------
		/// <summary>An unarmed soldier reacts to hostiles inside this many tiles.</summary>
		public const float UnarmedAlertTiles = 45f;
		/// <summary>An unarmed soldier backs off from any hostile closer than this (tiles), and only returns to its anchor when it is at least this far from the hostile.</summary>
		public const float UnarmedKeepAwayTiles = 26f;
		/// <summary>One step away from a hostile that got inside the keep-away distance (tiles).</summary>
		public const int UnarmedStepAwayTiles = 10;
		/// <summary>Unarmed soldiers never walk closer than this to the world's left or right edge (px).</summary>
		public const int UnarmedWorldMarginPx = 160;

		// ---------- Reachability / firing spots ----------
		/// <summary>A cached firing spot or reachability answer is re-tested this often (ticks) or when the target changes.</summary>
		public const int EngageSpotRecheckTicks = 120;
		/// <summary>Tiles searched around a candidate point for somewhere to stand.</summary>
		public const int EngageSpotSearchRadius = 6;
		/// <summary>Candidate firing spots tried per search (each costs one path search).</summary>
		public const int EngageSpotCandidates = 4;
		/// <summary>A melee target the body cannot path to is left alone this long (ticks).</summary>
		public const int UnreachableIgnoreTicks = 60 * 4;
		/// <summary>Seconds a blocked melee chase waits before the next reachability probe (ticks).</summary>
		public const int MeleeProbeRecheckTicks = 90;

		// ---------- Aerial engagement (soldiers with wings / extra jumps / rocket boots) ----------
		/// <summary>A target this many tiles above the body centre is worth climbing to (the body can jump 3, so this is well past a hop).</summary>
		public const int VerticalEngageMinTiles = 7;
		/// <summary>Once aloft the soldier stays up until the separation falls below Min - this (stops it flapping up and down on the line).</summary>
		public const int VerticalEngageExitTiles = 3;
		/// <summary>A ranged soldier hovers with its centre this far below the target's centre, never level with it.</summary>
		public const int FiringAltitudeBelowTargetTiles = 3;
		/// <summary>Climbs shorter than this (tiles above the last solid ground) are not worth the budget.</summary>
		public const int MinClimbBenefitTiles = 4;
		/// <summary>Never climb higher than this above the last solid ground.</summary>
		public const int MaxAltitudeTiles = 40;
		/// <summary>The climb plan uses at most this fraction of the kit's reach.</summary>
		public const float MaxReachUseFrac = 0.9f;
		/// <summary>Never aim above this many tiles under the top of the world.</summary>
		public const int WorldTopMarginTiles = 80;
		/// <summary>A ranged target counts as in range for climbing at reach x this (extends the shoot-from-here check once aloft).</summary>
		public const float AerialRangeKeepMult = 1.1f;
		/// <summary>A melee soldier only climbs when its reach gets the body within this many tiles of the target's level.</summary>
		public const int MeleeAerialSlackTiles = 4;

		// ---------- Tactical positioning: high ground ----------
		// A ranged soldier already in a firing pocket scores its own footing against nearby standable tiles and relocates only
		// for a clear win. Merit = elevation over the target + ledge height + platform bonus - travel - crowding.
		/// <summary>Columns searched either side of the soldier for a better perch (tiles).</summary>
		public const int PerchSearchRadiusTiles = 18;
		/// <summary>Highest a perch may sit above the soldier's feet (tiles).</summary>
		public const int PerchMaxRiseTiles = 14;
		/// <summary>Lowest a perch may sit below the soldier's feet (tiles).</summary>
		public const int PerchMaxDropTiles = 3;
		/// <summary>Every Nth column is sampled (the start column is shifted per soldier so a squad does not sample the same ones).</summary>
		public const int PerchColumnStepTiles = 2;
		/// <summary>Best-scoring tiles that get the line-of-sight test (and then at most PerchPathChecks path searches).</summary>
		public const int PerchCandidates = 4;
		public const int PerchPathChecks = 2;
		/// <summary>Merit per tile of height over the target's centre, up to PerchElevationCapTiles.</summary>
		public const float PerchElevationWeight = 1f;
		public const int PerchElevationCapTiles = 12;
		/// <summary>Merit per empty tile under the spot's floor before the next floor (a ledge over open ground), up to PerchLedgeCapTiles.</summary>
		public const float PerchLedgeWeight = 0.5f;
		public const int PerchLedgeCapTiles = 8;
		/// <summary>Merit for standing on a platform.</summary>
		public const float PerchPlatformBonus = 1.5f;
		/// <summary>Merit lost per tile of walking to the spot.</summary>
		public const float PerchTravelCostPerTile = 0.06f;
		/// <summary>Per-soldier, per-tile deterministic merit noise (full width), so near-equal spots are split between soldiers.</summary>
		public const float PerchJitter = 1f;
		/// <summary>A new spot must beat the current footing by at least this much merit (about three tiles of extra height).</summary>
		public const float PerchMinImprovement = 3f;
		/// <summary>A soldier already standing this many tiles above its target is in a good spot: a relocation must beat it by PerchGoodSpotExtraImprovement more (no pointless climbing).</summary>
		public const float PerchGoodElevationTiles = 6f;
		public const float PerchGoodSpotExtraImprovement = 3f;
		/// <summary>No new relocation search this long after deciding to relocate (ticks).</summary>
		public const int PerchRelocateCooldownTicks = 60 * 8;
		/// <summary>After arriving the soldier stays put at least this long (ticks).</summary>
		public const int PerchSettleTicks = 60 * 4;
		/// <summary>After an abandoned relocation (blocked, timed out, retreat) the next search waits this long (ticks).</summary>
		public const int PerchFailCooldownTicks = 60 * 5;
		/// <summary>A relocation that has not arrived by now is abandoned (ticks).</summary>
		public const int PerchTravelTimeoutTicks = 60 * 8;
		/// <summary>Within this of the spot counts as arrived (tiles).</summary>
		public const float PerchArriveXTiles = 1f, PerchArriveYTiles = 2f;
		/// <summary>Candidate spots must sit inside reach x this (a little inside the shootable limit, so a drifting target stays in range).</summary>
		public const float PerchRangeFrac = 0.92f;
		/// <summary>A spot must be this far past the standoff (tiles).</summary>
		public const float PerchStandoffMarginTiles = 1f;
		/// <summary>Most friendly soldiers considered when scoring crowding.</summary>
		public const int PerchFriendCap = 32;
		/// <summary>A blocked-path verdict is ignored this long after a relocation starts (it may be left over from the previous goal).</summary>
		public const int PerchStallGraceTicks = 30;

		// ---------- Opportunistic verticality: gear-assisted perches and aerial pursuit ----------
		// A tendency, never a stampede: only some soldiers (by stable slot) ever climb on their gear, only when it clearly beats their footing,
		// only a few of a squad at a time, and never while much of the squad already has height.
		/// <summary>A gear-assisted perch (extra jumps, wings, rocket boots) must sit at least this many tiles above the feet.</summary>
		public const int AirPerchMinRiseTiles = 3;
		/// <summary>...and within this fraction of the kit's computed reach (a double-jump soldier gets low ledges, a winged one real height).</summary>
		public const float AirPerchReachUseFrac = 0.8f;
		/// <summary>...and no farther than this across (tiles).</summary>
		public const int AirPerchMaxDxTiles = 14;
		/// <summary>A gear-assisted perch must beat the footing by this much merit on top of the usual improvement (a bias for holding the line).</summary>
		public const float AirPerchExtraMerit = 2f;
		/// <summary>Fraction of soldiers (stable per body) that ever climb on their gear.</summary>
		public const float AirPerchAppetite = 0.55f;
		/// <summary>At most this fraction of the nearby squad may be mid-climb at once (at least one soldier is always allowed).</summary>
		public const float AirPerchMaxClimbingFrac = 0.25f;
		/// <summary>No new climb while this fraction of the nearby squad already stands clearly higher.</summary>
		public const float AirPerchMaxHighFrac = 0.35f;
		/// <summary>A gear climb that has not arrived by now is abandoned (ticks).</summary>
		public const int AirPerchTimeoutTicks = 60 * 5;
		/// <summary>Ticks after launch the body may still be on the ground before the climb is written off.</summary>
		public const int AirPerchLaunchGraceTicks = 30;
		/// <summary>No new perch search for this long after a gear climb ends (ticks).</summary>
		public const int AirPerchSettleTicks = 60 * 15;
		/// <summary>The climb aims this far above the perch surface (tiles) and lets go once the feet are over it.</summary>
		public const float AirPerchHoverAboveTiles = 1f;
		public const float AirPerchArriveBelowPx = 4f;
		/// <summary>Fraction of soldiers (stable per body) that take to the air after an airborne target no ground spot reaches.</summary>
		public const float FlyPursuitAppetite = 0.75f;
		/// <summary>A target counts as airborne when no floor lies within this many tiles beneath it.</summary>
		public const int FlyPursuitMinAirTiles = 6;
		/// <summary>An airborne target is pursued up to weapon reach x this.</summary>
		public const float FlyPursuitRangeMult = 1.6f;
		/// <summary>The pursuer closes to this fraction of weapon reach horizontally (never inside the standoff + margin).</summary>
		public const float FlyPursuitHoldFrac = 0.65f;
		public const float FlyPursuitStandoffMarginTiles = 2f;

		// ---------- Engaging from cover (a target in reach but with no firing line) ----------
		/// <summary>The line to an in-reach target must stay blocked this long (ticks) before the soldier looks for another angle.</summary>
		public const int CoverLosDebounceTicks = 36;
		/// <summary>Columns searched either side of the soldier for a spot with a firing line (tiles).</summary>
		public const int CoverSearchRadiusTiles = 22;
		/// <summary>Best-scoring firing-line spots kept per search, and how many of them get a path search.</summary>
		public const int CoverCandidates = 4;
		public const int CoverPathChecks = 3;
		/// <summary>No new angle search for this long after one finds nothing (ticks); the plain approach search carries on meanwhile.</summary>
		public const int CoverFailCooldownTicks = 90;
		/// <summary>No new angle search for this long after a relocation starts (ticks).</summary>
		public const int CoverRelocateCooldownTicks = 120;

		// ---------- Biome / chest mimics ----------
		/// <summary>Soldiers never step a spot closer than this (tiles, hitbox distance) to a mimic; kept over the mimic wake radius (MimicWakeSoldierRangePx = 200 px = 12.5 tiles).</summary>
		public const float MimicKeepAwayTiles = 15f;

		// ---------- Tactical positioning: dispersion ----------
		/// <summary>Friendly soldiers closer than this (tiles, same height band) are crowding and nudge apart.</summary>
		public const float DispersionMinGapTiles = 3f;
		/// <summary>A nudge aims for this much room to the nearest neighbour; crowding merit fades to zero at this spacing (tiles).</summary>
		public const float DispersionSpacingTiles = 4.5f;
		/// <summary>Neighbours more than this above or below are on another level and do not crowd (tiles).</summary>
		public const float DispersionVerticalTiles = 4f;
		/// <summary>Each soldier's stable lateral slot lies within this either side of the firing line (tiles).</summary>
		public const float DispersionSpreadTiles = 9f;
		/// <summary>One nudge steps at most this far (tiles).</summary>
		public const int DispersionNudgeMaxTiles = 5;
		public const int DispersionNudgeCooldownTicks = 40;
		/// <summary>Merit lost for a friendly soldier standing exactly on a candidate spot (fades with distance to DispersionSpacingTiles).</summary>
		public const float DispersionCrowdPenalty = 6f;
		/// <summary>Hovering ranged soldiers sit up to this many tiles further below the target than the minimum, per their slot.</summary>
		public const float DispersionAltitudeSpreadTiles = 2f;
		/// <summary>Melee soldiers aim this far to one side of the target (tiles, either side); kept under the melee reach.</summary>
		public const float DispersionMeleeSpreadTiles = 1f;
		/// <summary>Low-discrepancy step (golden ratio) that turns a body index into a well-spread slot fraction.</summary>
		public const float SlotGoldenStep = 0.6180339f;

		// ---------- Combat orders: Spread Out ----------
		/// <summary>Spread Out multiplies every dispersion spacing (crowd gap, nudge spacing, nudge length, lateral slot, altitude and melee spread) by this.</summary>
		public const float SpreadOutMult = 2.5f;
		/// <summary>Idle / holding soldiers on Spread Out may drift this far past their normal wander radius to find room (tiles).</summary>
		public const int SpreadOutIdleExtraTiles = 24;
		/// <summary>Idle / holding soldiers sharing an anchor stand evenly spaced on one line; this is its half-width either side of the anchor (tiles) with Spread Out off (Spread Out uses IdleWanderTiles + SpreadOutIdleExtraTiles).</summary>
		public const float IdleLineHalfTiles = 12f;
		/// <summary>Idle soldiers whose anchors lie within this of each other (tiles) share one line.</summary>
		public const float IdleLineGroupTiles = 8f;
		/// <summary>A soldier this close to its slot (tiles) counts as on it.</summary>
		public const float IdleSlotDeadzoneTiles = 1.5f;
		/// <summary>Idle life: a soldier on its slot shuffles at most this far either side (tiles).</summary>
		public const float IdleBobTiles = 1f;
		public const int IdleSlotRefreshTicks = 30, IdleSlotRetryTicks = 45;
		/// <summary>Rows searched above and below the anchor's level for a standable slot spot.</summary>
		public const int IdleSlotSearchTiles = 6;
		/// <summary>The crowd nudge may move a soldier at most this far from its slot (tiles).</summary>
		public const float IdleSlotLeewayTiles = 2f;
		/// <summary>Following soldiers on Spread Out take slots alternately behind and ahead of the commander, this many tiles apart per rank.</summary>
		public const float SpreadOutFollowSpacingTiles = 6f;
		/// <summary>... never farther than this from the commander (tiles); keeps it inside FollowChaseLimit.</summary>
		public const float SpreadOutFollowMaxTiles = 24f;

		// ---------- Combat orders: Search & Destroy ----------
		/// <summary>A searching soldier never strays farther than this from its search anchor (the settlement muster point, or where the order found it).</summary>
		public const int SearchLeashTiles = 120;
		/// <summary>Mustered militia hunt within this many tiles of their barracks muster point; two thirds of the settlement radius, so they cover the town and its approaches but never trek away from it.</summary>
		public const int MilitiaLeashTiles = SettlementRadiusX * 2 / 3;
		/// <summary>One sweep leg: the next waypoint lies this far outward along the current heading (tiles).</summary>
		public const int SearchStepTiles = 36;
		/// <summary>A sweep leg shorter than this means the leash edge (or a dead end) is reached: turn round.</summary>
		public const int SearchMinStepTiles = 8;
		/// <summary>Searching soldiers acquire targets at weapon reach x this, so they run enemies down instead of waiting for them.</summary>
		public const float SearchAcquireRangeMult = 1.5f;
		/// <summary>Standable-tile search radius around a roam waypoint (tiles).</summary>
		public const int SearchSpotRadiusTiles = 12;
		/// <summary>Pause at a waypoint / after turning round (ticks).</summary>
		public const int SearchPauseTicks = 90, SearchTurnPauseTicks = 150;
		public const float SearchArriveXTiles = 2f;
		/// <summary>A waypoint not reached in this long is dropped and a new one picked.</summary>
		public const int SearchGoalTimeoutTicks = 60 * 14;
		/// <summary>Keep this far (tiles) beyond the standoff before a roaming ranged soldier stops advancing toward a hostile it cannot engage.</summary>
		public const float SearchHaltMarginTiles = 10f;
		/// <summary>Per-soldier lateral offset of a waypoint, so a squad fans out while sweeping (tiles, either side).</summary>
		public const float SearchSlotSpreadTiles = 5f;
		/// <summary>Stay this far from the world's left and right edges (tiles).</summary>
		public const int SearchWorldMarginTiles = 60;

		// ---------- Combat orders: Free Rein (Search & Destroy, unleashed) ----------
		// Free Rein sweeps and hunts exactly like Search & Destroy, but is tied to no player and no short anchor. Its only bound is an outer
		// safety circle of FreeReinLeashTiles around a fixed base (the soldier's home muster point / nearest settlement, else where the order
		// found it), plus the world-edge margin and the underworld floor. Inside that circle a soldier roams where it likes: legs are measured
		// from its own position, not from the base, and the heading is pulled toward the nearest distant enemy so the squad follows the fight.
		/// <summary>The outer safety radius around the Free Rein base (tiles). Roaming, chasing and waypoints never leave it.</summary>
		public const int FreeReinLeashTiles = 600;
		/// <summary>One roam leg (tiles). Kept under half the pathfinder window (80 tiles) so every leg can be path-checked.</summary>
		public const int FreeReinStepTiles = 64;
		/// <summary>When a leg of full length cannot be pathed, retry at these fractions of it before turning round.</summary>
		public const float FreeReinStepRetryMult1 = 0.5f, FreeReinStepRetryMult2 = 0.25f;
		/// <summary>Free-roaming soldiers acquire targets at weapon reach x this.</summary>
		public const float FreeReinAcquireRangeMult = 1.75f;
		/// <summary>The heading is pulled toward the nearest hostile inside this radius (tiles) that is not yet in acquire range.</summary>
		public const int FreeReinScoutTiles = 220;
		/// <summary>After a blocked leg or a turn-round the scout pull is ignored this long (ticks), so an unreachable enemy cannot pin the soldier.</summary>
		public const int FreeReinScoutCooldownTicks = 60 * 10;
		/// <summary>Without a fight to follow, a soldier turns round after this many legs in one direction (+0..FreeReinLegsSpread per soldier).</summary>
		public const int FreeReinLegsBeforeTurn = 5, FreeReinLegsSpread = 4;
		/// <summary>Standable-tile search radius around a roam waypoint (tiles); wider than Search & Destroy because the legs are longer.</summary>
		public const int FreeReinSpotRadiusTiles = 24;
		/// <summary>A waypoint not reached in this long is dropped and a new one picked (ticks).</summary>
		public const int FreeReinGoalTimeoutTicks = 60 * 22;
		/// <summary>Stay this far from the world's left and right edges (tiles).</summary>
		public const int FreeReinWorldMarginTiles = 80;
		/// <summary>Never roam into the bottom of the world: waypoints stay this many tiles above the underworld layer.</summary>
		public const int FreeReinUnderworldMarginTiles = 10;

		// ---------- Combat orders: Focus Target ----------
		/// <summary>The focus target is accepted out to weapon reach x this, so the squad commits to it instead of nearer enemies.</summary>
		public const float FocusRangeMult = 1.25f;
		/// <summary>Click tolerance around an enemy's hitbox when designating (px).</summary>
		public const int FocusClickPadPx = 10;
		/// <summary>Designate mode ignores clicks for this many ticks after it starts (so the radial click itself is not consumed).</summary>
		public const int FocusDesignateGraceTicks = 4;

		// ---------- Flee ----------
		public const int FleeScanTiles = 60;
		public const int FleeRunTiles = 30;

		// ---------- Follow / catch-up teleport ----------
		public const float FollowSlotSpacingTiles = 1.2f;
		public const int TeleportCooldownTicks = 60 * 3;
		public const int TeleportOffscreenMarginPx = 200;
		public const int TeleportBehindExtraPx = 100;
		public const int TeleportSpotSearchTiles = 25;
		/// <summary>A dedicated server has no screen; assume at least this much is visible around every player.</summary>
		public const int ServerAssumedScreenW = 1920, ServerAssumedScreenH = 1080;

		// ---------- Resupply ----------
		public const float ArmoryArriveTiles = 2.5f;
		public const int ResupplyRetryTicks = 60 * 10;
		public const int ResupplySearchIntervalTicks = 60;
		public const int MagicRegenWaitTicks = 60 * 5;
		public const int ManaStarvedTicks = 60 * 3;
		public const float TopUpBelowFraction = 0.2f;
		public const int TopUpArmoryTiles = 3;
		public const int TopUpIntervalTicks = 120;

		// ---------- Haul ----------
		public const int HaulCarryMax = 5;
		public const float HaulArriveTiles = 2.5f;
		/// <summary>No hauling when the drop-off is farther than this; followers in the field keep following.</summary>
		public const int HaulMaxTripTiles = 120;
		/// <summary>An item type the armory refused is left alone this long.</summary>
		public const int HaulRefusedCooldownTicks = 60 * 60;
		public const int HaulRefusedMemory = 4;
		public const int HaulScanEveryThinks = 2;
		/// <summary>A single haul leg (walk to the item, or to the drop-off) that takes longer than this is abandoned.</summary>
		public const int HaulGiveUpTicks = 60 * 45;

		// ---------- Home / patrol / idle ----------
		public const float HomeArriveTiles = 2f;
		/// <summary>Inside this of the muster point a soldier counts as home (wider than the arrive radius so idling never re-triggers a return).</summary>
		public const float NearHomeTiles = 8f;
		public const float PatrolArriveXTiles = 1.5f, PatrolArriveYTiles = 6f;
		public const int IdleWanderTiles = 6, HoldIdleWanderTiles = 2;
		public const int IdleDecisionMinTicks = 60 * 3, IdleDecisionMaxTicks = 60 * 6;

		// ---------- Desert ----------
		// The 20 s force-despawn backstop is the roster's DesertDespawnTicks (LevyConstants.Roster.cs); the brain only despawns at the edge.
		public const int DesertEdgeMarginPx = 200;
		public const float DesertArriveTiles = 3f;

		// ---------- Goals ----------
		/// <summary>A goal this far below the feet asks the body to drop through platforms.</summary>
		public const int FallThroughBelowTiles = 2;
	}
}
