namespace Levy.Common
{
	/// <summary>Summoner-soldier numbers: per-soldier minions, soldier-anchored whips, soldier-returning boomerangs. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		// ---------- Minions ----------
		/// <summary>Most minions one soldier keeps out, whatever its armor says.</summary>
		public const int MinionMaxPerSoldier = 6;
		/// <summary>How often a soldier recounts its flock and tops it up (ticks), and the wait after each new summon.</summary>
		public const int MinionMaintainIntervalTicks = 20;
		public const int MinionSpawnCooldownTicks = 40;
		/// <summary>Scatter radius (px) of a fresh minion's spawn point around its soldier.</summary>
		public const float MinionOrbitRadiusPx = 52f;
		/// <summary>Idle trailing formation: distance behind the soldier of the first minion, extra distance per further minion, base lift above the soldier's centre, and the extra lift of every second minion (all px).</summary>
		public const float MinionTrailBackPx = 34f, MinionTrailSpacingPx = 26f, MinionTrailLiftPx = 30f, MinionTrailStaggerPx = 16f;
		/// <summary>Idle bob: vertical amplitude (px) and angular speed (radians per tick).</summary>
		public const float MinionBobAmpPx = 8f, MinionBobSpeed = 0.06f;
		/// <summary>Ranged minions back away when closer than this fraction of their standoff range; each shot nudges them back by the kickback (px/tick).</summary>
		public const float MinionRangedBackoffFraction = 0.5f, MinionRangedKickback = 1.2f;
		/// <summary>A minion this far from its soldier (px) snaps back beside it.</summary>
		public const float MinionTeleportDistPx = 900f;
		/// <summary>Targets must be within this many px of the minion to be picked, and within the leash of the soldier.</summary>
		public const float MinionSeekRangePx = 520f, MinionLeashPx = 760f;
		/// <summary>How often a minion looks for a new target (ticks).</summary>
		public const int MinionRetargetTicks = 12;
		public const float MinionIdleSpeed = 9f, MinionDashSpeed = 13f, MinionTurnLerp = 0.14f;
		/// <summary>After a hit the minion backs off for this many ticks before diving again.</summary>
		public const int MinionRecoilTicks = 14;
		public const int MinionLocalHitCooldownTicks = 18;
		public const int MinionHitboxPx = 22;
		public const int MinionLifeTicks = 20000;
		/// <summary>Frames of the real minion sprite advance every this many ticks.</summary>
		public const int MinionFrameTicks = 6;
		/// <summary>Targets already tagged by a whip are weighted by this when picking (smaller = preferred harder).</summary>
		public const float MinionTaggedPreference = 0.35f;
		/// <summary>Fallback minion slot cost when the summon weapon's projectile reports none.</summary>
		public const float MinionDefaultSlots = 1f;

		// ---------- Whips ----------
		/// <summary>Reach of a lash in pixels per point of the item's shootSpeed, with floor/ceiling; the brain stops short at this fraction of reach.</summary>
		public const float WhipReachPerShootSpeed = 40f, WhipReachMinPx = 90f, WhipReachMaxPx = 340f;
		public const float WhipHoldFraction = 0.8f;
		public const int WhipRangeMinTiles = 5, WhipRangeMaxTiles = 20;
		public const int WhipSegments = 14;
		/// <summary>Fraction of the lash life spent extending, then holding, before retracting.</summary>
		public const float WhipExtendFraction = 0.4f, WhipHoldFractionOfLife = 0.2f;
		public const int WhipLocalHitCooldownTicks = 60;
		/// <summary>The tag a lash leaves on an enemy: how long it lasts (ticks) and the extra fraction of damage soldier minions deal to it.</summary>
		public const int TagDurationTicks = 240;
		public const float TagMinionDamageBonus = 0.5f;

		// ---------- Boomerangs ----------
		/// <summary>Boomerangs one soldier may have out at once.</summary>
		public const int BoomerangMaxOut = 1;
		/// <summary>Engagement range (tiles) and the farthest a boomerang flies out from the soldier (px).</summary>
		public const int RangeBoomerang = 22;
		public const float BoomerangMaxOutPx = 22f * 16f;
		public const float BoomerangMinSpeed = 7f, BoomerangMaxSpeed = 16f, BoomerangReturnAccel = 0.9f;
		public const int BoomerangMaxFlightTicks = 240;
		public const float BoomerangCatchDistPx = 26f;
		public const int BoomerangLocalHitCooldownTicks = 12;
		public const int BoomerangHitboxPx = 20;
		public const float BoomerangSpinPerTick = 0.45f;

		// ---------- Doctrine / ranking ----------
		/// <summary>Summon-weapon score: minion damage x this (summon staves have no meaningful DPS of their own). Whips and boomerangs are scaled DPS.</summary>
		public const float SummonScoreDamageFactor = 2.2f, WhipScoreFactor = 0.6f, BoomerangScoreFactor = 0.8f;
		/// <summary>Armor doctrine: each extra minion slot (maxMinions above the bare 1) counts like this much damage bonus.</summary>
		public const float MinionDoctrineWeight = 0.06f;
		/// <summary>Standoff reach (tiles) the brain assumes for a soldier holding only a summon weapon.</summary>
		public const int RangeSummon = 36;
	}
}
