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
		}
	}
}
