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
		/// <summary>Once retreating, a ranged soldier keeps backing off until the threat is this far past the standoff (tiles).</summary>
		public const float StandoffHysteresisTiles = 2f;
		/// <summary>The in-range lateral shuffle only happens with this much room past the standoff (tiles).</summary>
		public const float StrafeMarginTiles = 6f;

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
