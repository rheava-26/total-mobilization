namespace Levy.Common
{
	/// <summary>Numbers for Free Rein's home leash (SoldierNPC.FreeRein.cs) and the off-duty life (SoldierNPC.Leisure.cs). Distances in tiles unless named px or ticks.</summary>
	public static partial class LevyConstants
	{
		// ---------- Free Rein: the home leash ----------
		// Free Rein roams a box around its base (the soldier's barracks, else the commander's spot when the order was given): FreeReinRadius tiles each way along the ground, FreeReinUpTiles of
		// sky and FreeReinDownTiles of ground under the base floor. Wander spots, chases and the scout pull all stay inside it.
		/// <summary>Default of the Free Rein range option (tiles from home).</summary>
		public const int FreeReinRadiusDefault = 200;
		/// <summary>Rows above / below the base floor that Free Rein reaches.</summary>
		public const int FreeReinUpTiles = 70, FreeReinDownTiles = 16;
		/// <summary>A wander spot may have this many solid tiles over it in the 80 rows above (a house roof is 1 to 3, a cave or a hill is a dozen or more), plus the base's own.</summary>
		public const int FreeReinOverburdenMax = 10, FreeReinOverburdenScanRows = 80;
		/// <summary>A route to a wander spot is refused when it dips this many rows under both its ends (a hole in the way).</summary>
		public const int FreeReinDipTiles = 6;
		/// <summary>A spot lower than the levy by more than this must have a way back (a path from the spot to where the levy stands).</summary>
		public const int FreeReinReturnCheckTiles = 4;
		/// <summary>A gap in the ground under a route stretch this many rows deep (or with no floor in reach) is a hole: the route is refused.</summary>
		public const int FreeReinHoleTiles = 6;
		/// <summary>A fight goal stops short of a column whose floor is this far below the levy's feet (or out of reach): a chasm, not a slope.</summary>
		public const int FreeReinGapTiles = 10;
		/// <summary>A wander spot keeps this many columns clear of a drop on each side.</summary>
		public const int FreeReinRimTiles = 3;
		/// <summary>A pit floor: lower than the base floor by more than this many rows, open above, with a wall this many rows up within this many columns on both sides.</summary>
		public const int FreeReinPitBelowTiles = 3, FreeReinPitWallRows = 4, FreeReinPitWallTiles = 12;
		/// <summary>A levy outside its box walks home: how often it re-plans (ticks).</summary>
		public const int FreeReinRecallRetryTicks = 90;

		// ---------- Free Rein: off-duty life ----------
		/// <summary>A free-roaming levy that has been calm this long may take up a pastime.</summary>
		public const int LeisureCalmTicks = 60 * 8;
		/// <summary>After a pastime (or a failed pick) the next one comes this long after (ticks).</summary>
		public const int LeisureGapMinTicks = 60 * 10, LeisureGapMaxTicks = 60 * 40;
		/// <summary>How long one pastime lasts (ticks).</summary>
		public const int LeisureKiteMinTicks = 60 * 25, LeisureKiteMaxTicks = 60 * 70;
		public const int LeisureRoastMinTicks = 60 * 15, LeisureRoastMaxTicks = 60 * 40;
		public const int LeisureKegMinTicks = 60 * 12, LeisureKegMaxTicks = 60 * 30;
		public const int LeisureStargazeMinTicks = 60 * 20, LeisureStargazeMaxTicks = 60 * 50;
		public const int LeisureCheerMinTicks = 60 * 6, LeisureCheerMaxTicks = 60 * 14;
		public const int LeisureFireSitMinTicks = 60 * 25, LeisureFireSitMaxTicks = 60 * 70;
		/// <summary>A levy abandons a pastime it cannot reach in this long (ticks).</summary>
		public const int LeisureWalkTimeoutTicks = 60 * 25;
		/// <summary>Props (campfire, bed, keg, chair) are looked for this far from the base (tiles), and the scan is shared for this long (ticks).</summary>
		public const int LeisurePropRadiusTiles = 70, LeisurePropScanTicks = 60 * 8;
		/// <summary>A pastime at a prop stands this far from it (tiles).</summary>
		public const int LeisureStandOffMinTiles = 2, LeisureStandOffMaxTiles = 4;
		/// <summary>Open sky for a kite: no solid tile in the column this many rows up, and a flat stretch of this many tiles around.</summary>
		public const int LeisureKiteSkyRows = 26, LeisureKiteRoomTiles = 3;
		/// <summary>No more than this share of the army takes up a pastime at once (1 in N, rounded up): the rest keep patrolling the base.</summary>
		public const float LeisureMaxShare = 0.7f;
		/// <summary>A pair chat lasts this long (ticks) and they speak every few seconds.</summary>
		public const int LeisureChatMinTicks = 60 * 7, LeisureChatMaxTicks = 60 * 16;
	}
}
