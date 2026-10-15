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
		public const int KiteStepAwayTiles = 4;
		public const int StrafeTiles = 2;
		public const int MeleeStepBackWindowTicks = 12;
		/// <summary>A held target may drift to range x this before it is dropped.</summary>
		public const float TargetKeepRangeMult = 1.5f;

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
