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

		// ---------- Real (vanilla-driven) minions: see SoldierMinions ----------
		/// <summary>Slack (slots) when comparing a flock's slot total with the soldier's capacity.</summary>
		public const float MinionSlotEpsilon = 0.01f;
		/// <summary>Horizontal scatter (px) of a fresh real minion around its soldier.</summary>
		public const float MinionSpawnScatterPx = 36f;
		/// <summary>Size (px) of the box tested for wall overlap at a fresh real minion's scattered spawn point; a blocked point falls back to the soldier's centre.</summary>
		public static readonly Microsoft.Xna.Framework.Vector2 MinionSpawnProbe = new(16f, 16f);
		/// <summary>The dummy's maxMinions while a real minion runs its Update, so vanilla's own cap check (which would kill the overflow) never fires: the soldier's upkeep owns the count.</summary>
		public const int MinionDriverMaxMinions = 1000;
		/// <summary>Gem / counter controllers (Desert Tiger gems, Abigail's counters) are level markers, not a flock: they may stack past <see cref="MinionMaxPerSoldier"/>.</summary>
		public const int MinionControllerMaxPerSoldier = 12;
		/// <summary>Desert Tiger rank thresholds: more gems than this many promote the tiger to tier 2 / tier 3 (vanilla Player.GetDesiredStormTigerMinionRank).</summary>
		public const int TigerTier2GemsOver = 3, TigerTier3GemsOver = 6;
		/// <summary>Pygmy staff cast: the pygmy's ai[1] and its localAI[0] at spawn (vanilla SpawnMinionOnCursor / ItemCheck_Shoot).</summary>
		public const float PygmySpawnAi1 = 60f, PygmySpawnLocalAi0 = 30f;
		/// <summary>The Desert Tiger's localAI[0] at spawn (its first special-attack delay).</summary>
		public const float TigerSpawnLocalAi0 = 60f;
		/// <summary>The server re-sends every real minion this often (ticks, staggered by projectile index) so remote screens keep their walkers and flyers where they are.</summary>
		public const int MinionNetRefreshTicks = 20;
		/// <summary>The ratio of the dummy's summon damage that vanilla re-applies to a minion's originalDamage every tick is divided back out within these bounds, so the soldier's own gear sets what its minions deal.</summary>
		public const float MinionDamageRatioMin = 0.1f, MinionDamageRatioMax = 10f;

		// ---------- Sentries ----------
		/// <summary>Standoff reach (tiles) the brain assumes for a soldier holding only a sentry.</summary>
		public const int RangeSentry = 36;
		/// <summary>Sentries one soldier keeps standing at once.</summary>
		public const int SentryMaxPerSoldier = 1;
		/// <summary>How often a soldier recounts its sentry and decides whether to plant a new one (ticks). The standing sentry's lifetime is refreshed every tick.</summary>
		public const int SentryMaintainIntervalTicks = 20;
		/// <summary>Minimum wait after a sentry is planted before the next one may be (ticks), and the wait after a failed placement (no ground in reach, blocked spot).</summary>
		public const int SentryReplaceCooldownTicks = 300, SentryRetryTicks = 90;
		/// <summary>A soldier counts as in combat for sentry purposes while engaged and for this many ticks after its last engagement.</summary>
		public const int SentryCombatGraceTicks = 300;
		/// <summary>Fighting this many tiles from its sentry, the soldier plants a fresh one where it stands (the old one is removed).</summary>
		public const int SentryReplaceDistTiles = 50;
		/// <summary>How far below the soldier's feet (tiles) a sentry looks for ground to stand on.</summary>
		public const int SentryGroundSearchTiles = 30;
		/// <summary>In multiplayer a remote client counts a sentry down from its spawn (vanilla Projectile.SentryLifeTime); the soldier plants a fresh one before that runs out (ticks).</summary>
		public const int SentryMpRefreshTicks = 30000;
		/// <summary>Sentry-weapon score: item damage x this (the tower's own cadence is not the item's useTime).</summary>
		public const float SentryScoreDamageFactor = 1.5f;
		/// <summary>The summon-damage ratio the owner (commander) applies to a sentry is divided back out within these bounds, so the soldier's own gear sets the sentry's damage.</summary>
		public const float SentryOwnerRatioMin = 0.1f, SentryOwnerRatioMax = 10f;

		// ---------- Whips ----------
		/// <summary>Reach of a lash in pixels per point of the item's shootSpeed, with floor/ceiling; the brain stops short at this fraction of reach.</summary>
		public const float WhipReachPerShootSpeed = 40f, WhipReachMinPx = 90f, WhipReachMaxPx = 340f;
		public const float WhipHoldFraction = 0.8f;
		public const int WhipRangeMinTiles = 5, WhipRangeMaxTiles = 20;
		public const int WhipSegments = 14;
		/// <summary>Fraction of the lash life spent extending, then holding, before retracting.</summary>
		public const float WhipExtendFraction = 0.4f, WhipHoldFractionOfLife = 0.2f;
		public const int WhipLocalHitCooldownTicks = 60;
		/// <summary>Cadence: a lash repeats every (useTime x this) ticks, clamped; the lash lives for that whole period so a target in reach is lashed back-to-back.</summary>
		public const float WhipCadenceMult = 0.75f;
		public const int WhipCadenceMinTicks = 14, WhipCadenceMaxTicks = 40;
		/// <summary>The lash is the summoner's active damage: every lash deals this multiple of the weapon damage, and the outer fraction of the lash (the tip) cracks for a guaranteed critical.</summary>
		public const float WhipDamageMult = 1.35f, WhipTipFraction = 0.3f;
		/// <summary>Target hit-boxes are widened by this many pixels per side for the lash line test, so a lash that visibly meets an enemy lands.</summary>
		public const int WhipHitPadPx = 8;
		/// <summary>Segment sprite spacing (px) when drawing the lash sheet, and the min/max segment counts.</summary>
		public const int WhipDrawMinSegs = 3, WhipDrawMaxSegs = 40;
		/// <summary>The tag a lash leaves on an enemy: how long it lasts (ticks) and the extra fraction of damage soldier minions deal to it.</summary>
		public const int TagDurationTicks = 240;
		public const float TagMinionDamageBonus = 0.5f;

		// ---------- Boomerangs ----------
		/// <summary>Boomerangs one soldier may have out at once.</summary>
		public const int BoomerangMaxOut = 1;
		/// <summary>Per-weapon override of <see cref="BoomerangMaxOut"/>: multi-throw boomerangs. Bananarang is capped at 10 owned projectiles in vanilla (Player.ItemCheck_CheckCanUse, shoot 272).</summary>
		public const int BananarangMaxOut = 10;
		public static readonly System.Collections.Generic.Dictionary<int, int> BoomerangMaxOutByItem = new()
		{
			[Terraria.ID.ItemID.Bananarang] = BananarangMaxOut,
		};
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
		/// <summary>
		/// Summon-staff score = item damage x SummonScoreDamageFactor (sustained DPS of ONE minion per point of damage; a minion lands a hit every ~30 ticks minus travel and misses)
		/// x the minions the staff is assumed to field (see <see cref="SummonScoreBaseSlots"/>). Staves have no DPS of their own: useTime means nothing. Whips and boomerangs are scaled DPS.
		/// </summary>
		public const float SummonScoreDamageFactor = 1.2f, WhipScoreFactor = 0.8f, BoomerangScoreFactor = 0.8f;
		/// <summary>
		/// Minion slots a summoner kit is assumed to have when ranking a staff: SummonScoreBaseSlots + SummonScoreSlotsPerTier x the staff's rarity tier, capped at SummonScoreMaxSlots,
		/// divided by the staff's per-minion slot cost (at least one minion, at most <see cref="MinionMaxPerSoldier"/>). Slots grow with progression (armor sets and accessories add them),
		/// so an early staff stays modest while a late staff is valued as a real flock. The score depends only on the item, so a swap can never flip-flop.
		/// </summary>
		public const float SummonScoreBaseSlots = 1.5f, SummonScoreSlotsPerTier = 0.2f, SummonScoreMaxSlots = 4f;
		/// <summary>Armor doctrine: each extra minion slot (maxMinions above the bare 1) counts like this much damage bonus.</summary>
		public const float MinionDoctrineWeight = 0.06f;
		/// <summary>Standoff reach (tiles) the brain assumes for a soldier holding only a summon weapon.</summary>
		public const int RangeSummon = 36;
	}
}
