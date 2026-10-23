namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>
		/// Officers: town NPCs conscripted into the army. Slots, economy, the follow/fight reflexes and the Nurse's kit.
		/// Pixels per tick unless the name says tiles or ticks. Tune here; nothing else in the module holds a number.
		/// </summary>
		public static class Officer
		{
			// ---- command slots ----
			/// <summary>Officers one commander can field at once. Progression (more slots per boss) is a later layer: raise this or turn it into a function.</summary>
			public const int MaxSlots = 3;

			// ---- economy (copper; multiples of the tier's Levy wage so it scales with the world) ----
			/// <summary>An officer's daily wage in Levy wages (a Levy is 1x, a Man-at-Arms 3x). Paid at dawn from the barracks that commissioned him.</summary>
			public const float WageMult = 2f;
			/// <summary>The one-time commission fee, in days of the officer's wage (same idea as a soldier's muster fee).</summary>
			public const int FeeDays = LevyConstants.MusterFeeDays;
			/// <summary>Consecutive unpaid dawns after which the commission lapses and the officer goes back to town life.</summary>
			public const int MaxDaysUnpaid = 2;
			/// <summary>The commander must be this close (tiles) to the barracks to commission or dismiss (server check; the panel itself closes at <see cref="LevyConstants.UI.PanelCloseDistanceTiles"/>).</summary>
			public const int ActionRangeTiles = 32;

			// ---- follow ----
			/// <summary>Distance behind the commander an officer idles at (tiles); each slot stands a little further out so they do not stack.</summary>
			public const int FollowDistTiles = 5, FollowSlotStepTiles = 2;
			/// <summary>Inside this many tiles of the follow spot the officer stands still; beyond <see cref="RunDistTiles"/> he runs.</summary>
			public const int StopDistTiles = 2, RunDistTiles = 14;
			/// <summary>Walk and run speed cap (px/tick) and acceleration. A town NPC walks ~1.2, a player runs 3.</summary>
			public const float WalkSpeedPx = 1.3f, RunSpeedPx = 3.0f, Accel = 0.14f;
			/// <summary>Farther than this from the commander (tiles) for <see cref="TeleportAfterTicks"/> and the officer is brought to the commander (pylons, mirrors, long falls).</summary>
			public const int TeleportDistTiles = 70;
			public const int TeleportAfterTicks = 180;
			/// <summary>Stuck (no progress toward a far goal) this long and the officer is brought to the commander.</summary>
			public const int StuckTeleportTicks = 360;
			/// <summary>Stuck sampling: every this many ticks, progress under <see cref="StuckProgressPx"/> counts as stuck.</summary>
			public const int StuckSampleTicks = 30;
			public const float StuckProgressPx = 6f;
			/// <summary>Jump: speed for an obstacle of N tiles is sqrt(2 * gravity * (N * 16 + <see cref="JumpClearPx"/>)); anything taller than <see cref="MaxJumpTiles"/> is not attempted.</summary>
			public const int MaxJumpTiles = 4;
			public const float JumpClearPx = 10f, Gravity = 0.3f;
			/// <summary>Spot-search radius (tiles) when an officer is brought to the commander or sent home.</summary>
			public const int SpotRadiusTiles = 8;
			/// <summary>A dismissed officer farther than this from his home tile (tiles) is walked home instantly instead of strolling back across the map.</summary>
			public const int HomeSnapDistTiles = 40;

			// ---- fight ----
			/// <summary>Hostiles are looked for this often (ticks).</summary>
			public const int ScanIntervalTicks = 10;
			/// <summary>Retreat (stay with the commander, stop shooting at anything out of arm's reach) below this fraction of max life; resume above <see cref="ResumeLifeFrac"/>.</summary>
			public const float RetreatLifeFrac = 0.4f, ResumeLifeFrac = 0.6f;
			/// <summary>Fraction of the vanilla town damage the officer's weapon deals (support officers fight weakly).</summary>
			public const float AttackDamageMult = 1f;

			// ---- the Nurse ----
			/// <summary>The Nurse looks for a wounded soldier this often (ticks) and may heal at most one per look.</summary>
			public const int NurseCastIntervalTicks = 60;
			/// <summary>The same soldier is not healed again for this long (ticks): 5 s.</summary>
			public const int NursePerSoldierCooldownTicks = 300;
			/// <summary>Soldiers within this many tiles of her can be healed.</summary>
			public const int NurseHealRangeTiles = 20;
			/// <summary>A soldier is worth a heal once at least this fraction of max life (and <see cref="NurseMinMissingHp"/>) is gone.</summary>
			public const float NurseMinMissingFrac = 0.15f;
			public const int NurseMinMissingHp = 15;
			/// <summary>One heal restores this fraction of max life (never more than is missing).</summary>
			public const float NurseHealFrac = 0.5f;
			/// <summary>One heal costs the vanilla Nurse price for the life restored, but never more than this (copper): a few gold at most, paid from the commissioning barracks.</summary>
			public const long NurseMaxPriceCopper = 30000;
			/// <summary>Taunt weight of the Nurse. -250 cancels the flat advantage soldiers have in enemy targeting, so enemies weigh her like the commander: she is not the first thing they chase.</summary>
			public const int NurseAggro = -250;

			// =====================================================================================================
			// THE AVATAR — an officer fights as a reskinned soldier (reuses the soldier movement AI), its real town NPC
			// parked and co-located so the player can still shop. These tune the transient body, not the town NPC.
			// =====================================================================================================
			/// <summary>Flat HP added on top of the town NPC's own max life: officers are expensive and named, so the avatar is a little sturdier than the civilian.</summary>
			public const int AvatarBonusHp = 150;
			/// <summary>Baseline defense of an avatar before the combat-book bonus — already more than a fresh town NPC. Tune the whole sturdiness here.</summary>
			public const int AvatarBaseDefense = 18;
			/// <summary>Knockback resistance of the avatar (0 = none, 1 = immovable). A touch of resist so a hit does not fling the officer off the line.</summary>
			public const float AvatarKnockbackResist = 0.5f;

			// ---- Advanced Combat Techniques (the two town-NPC combat books; vanilla values, kept tunable) ----
			/// <summary>Defense each read combat book grants a town NPC (NPC.AI_007_TownEntities adds 6 per book). The avatar gets the same when the flags are set.</summary>
			public const int CombatBookDefensePerBook = 6;
			/// <summary>Damage scale each read combat book grants a town NPC (NPC.AI_007_TownEntities adds 0.2 to the damage multiple per book).</summary>
			public const float CombatBookDamagePerBook = 0.2f;

			// =====================================================================================================
			// COMBAT POSTURE — how a kit wants the shared soldier movement to position the avatar.
			// FRONTLINE marches at the follow distance and shoots what comes in reach. SUPPORT hangs back.
			// =====================================================================================================
			/// <summary>SUPPORT: tiles behind the army's leading edge (or the commander) the officer tries to stand.</summary>
			public const int SupportRearStandoffTiles = 9;
			/// <summary>SUPPORT: an enemy within this many tiles pushes the officer back toward the commander; she never seeks melee.</summary>
			public const int SupportEnemyAvoidRadiusTiles = 15;
			/// <summary>SUPPORT: she stays within this many tiles of a wounded soldier she is tending, so the heal can reach.</summary>
			public const int SupportTendRadiusTiles = 12;

			// ---- the Nurse's healing syringe (ranged heal) ----
			/// <summary>Launch speed (px/tick) of a thrown syringe — the attack and the heal one both. A real bump over the stock 8, so the tiny-range syringe actually reaches.</summary>
			public const float NurseSyringeSpeed = 12f;
			/// <summary>A soldier this close (tiles) is topped off directly (proximity heal); farther wounded in reach get a thrown healing syringe.</summary>
			public const int NurseProximityTiles = 4;
			/// <summary>Ticks a thrown healing syringe lives before giving up (it is lightly homing, so this is a safety net).</summary>
			public const int NurseHealSyringeLifeTicks = 180;

			// ---- on-the-move chatter ----
			/// <summary>While marching, an officer rolls a chatter line every this-to-that many ticks (so two officers do not speak in lockstep).</summary>
			public const int MarchChatterMinTicks = 360, MarchChatterMaxTicks = 900;
			/// <summary>Chance a march chatter roll actually speaks (the squad dialogue budget throttles it further).</summary>
			public const float MarchChatterChance = 0.5f;

			// ---- drawing the town sprite on the avatar ----
			/// <summary>First frame of a town NPC sheet that belongs to the walk cycle (frame 0 is standing, the low frames are the attack pose).</summary>
			public const int TownWalkStartFrame = 7;
			/// <summary>Ticks of accumulated stride per walk frame (lower = faster leg cycle).</summary>
			public const int TownWalkFrameTicks = 7;
			/// <summary>Vertical nudge (px) of the drawn town sprite so its feet sit on the ground like a civilian's.</summary>
			public const float TownDrawFootOffsetPx = 4f;
		}

		/// <summary>
		/// SUMMON INFRASTRUCTURE — summoned combat units (real NPCs with healthbars) fielded by officer kits, and the Pirate Captain
		/// that proves it. Pixels per tick unless the name says tiles or ticks. Appended block; owned by the summon module.
		/// </summary>
		public static class Summon
		{
			// ---- Summon infra (shared by every summon kind) ----
			/// <summary>A unit looks for a hostile this often (ticks).</summary>
			public const int ScanIntervalTicks = 12;
			/// <summary>A unit idles this far (tiles) from its officer (a per-unit jitter fans the squad out).</summary>
			public const float FollowStandoffTiles = 4f;
			/// <summary>Inside this many tiles of its follow spot the unit stands still.</summary>
			public const float StopDistTiles = 2f;
			/// <summary>Farther than this from the officer (tiles) for <see cref="TeleportAfterTicks"/> and the unit is brought to him (pits, long falls).</summary>
			public const float LeashTiles = 40f;
			public const int TeleportAfterTicks = 180;
			/// <summary>Walk acceleration (px/tick^2) toward the follow spot.</summary>
			public const float AccelPx = 0.2f;
			/// <summary>Jump: speed for an N-tile wall is sqrt(2 * gravity * (N*16 + clear)); anything taller than <see cref="MaxJumpTiles"/> is not attempted.</summary>
			public const float Gravity = 0.3f, JumpClearPx = 10f;
			public const int MaxJumpTiles = 4;
			/// <summary>Healthbar drawn over each unit's head.</summary>
			public const int HpBarWidthPx = 36, HpBarHeightPx = 5, HpBarOffsetY = 10;

			// ---- Pirate (the proof officer and its boarding crew) ----
			/// <summary>Wage/fee multiplier of the Pirate Captain (a summon officer costs more than a plain fighter).</summary>
			public const float PirateCostMult = 1.9f;
			/// <summary>Taunt weight of the Pirate Captain avatar (0 = weighed like a plain soldier body).</summary>
			public const int PirateOfficerAggro = 0;
			/// <summary>Flintlock gunners the captain keeps fielded at once.</summary>
			public const int PirateCrewSize = 4;
			/// <summary>Health of one crewman (flat; damage, not HP, scales with progress).</summary>
			public const int PirateUnitHp = 200;
			/// <summary>Base flintlock damage per crewman before the world-progress multiply.</summary>
			public const int PirateUnitBaseDamage = 22;
			public const float PirateFlintlockSpeed = 11f, PirateFlintlockKnockback = 2f, PirateFlintlockGravityCorrection = 5f;
			public const int PirateFlintlockCooldownTicks = 55, PirateFlintlockRandomCooldownTicks = 35;
			/// <summary>Flintlock reach (tiles).</summary>
			public const float PirateRangeTiles = 28f;
			/// <summary>Crewman walk speed cap (px/tick).</summary>
			public const float PirateUnitMoveSpeedPx = 1.7f;
			public const int PirateUnitAggro = 0;
			/// <summary>A lost crewman is replaced this often (ticks) while below the crew size.</summary>
			public const int PirateRespawnCadenceTicks = 300;
		}
	}
}
