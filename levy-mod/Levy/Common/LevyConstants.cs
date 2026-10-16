using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>
	/// Every tunable number in the mod. Spec v0.1 placeholders. Agents may ADD members here, never rename or remove.
	/// Distances are in tiles unless the name says pixels; money is in copper (1 silver = 100, 1 gold = 10000).
	/// </summary>
	public static partial class LevyConstants
	{
		public const int TilePx = 16;

		// ---------- Command points ----------
		public const int CommandPointsBase = 4;
		public const int CpKingSlime = 1, CpEyeOfCthulhu = 2, CpEaterOrBrain = 2, CpQueenBee = 1, CpSkeletron = 2, CpDeerclops = 1,
			CpWallOfFlesh = 7, CpQueenSlime = 1, CpEachMech = 2, CpPlantera = 3, CpGolem = 3, CpEmpress = 1, CpFishron = 1, CpCultist = 2, CpMoonLord = 3;

		// ---------- Grades ----------
		public static readonly int[] GradeSlots = { 1, 3, 5 };
		public static readonly int[] GradeCpCost = { 1, 3, 7 };
		public static readonly int[] GradeHpCap = { 200, 400, 500 };
		public static readonly float[] GradeWageMult = { 1f, 3f, 7f };
		public static readonly float[] GradeDamageMult = { 0.8f, 0.9f, 1.0f };
		public static readonly float[] GradeAimJitterDeg = { 6f, 3f, 1f };
		public static readonly float[] GradeLeadError = { 0.25f, 0.10f, 0.03f };
		public const float VeteranAimMult = 0.6f;
		public const float ShakenAimMult = 2f;
		public const float DrillMilitiaAimMult = 0.75f;
		public const float ShakenDamageMult = 0.6f;
		public const int VeteranBossFights = 3;
		public const float VeteranMaxFraction = 0.25f;

		// ---------- HP / mana ----------
		public const int BaseHp = 100;
		public const int HpPerLifeCrystal = 20;
		public const int HpPerLifeFruit = 5;
		public const int MaxLifeCrystalsLevy = 5, MaxLifeCrystalsManAtArms = 15, MaxLifeCrystalsChampion = 20, MaxLifeFruit = 20;
		public const int BaseMana = 20, ManaPerCrystal = 20, MaxMana = 200;
		public const int PotionDrinkBelowPercent = 50;
		public const int ManaPotionDrinkBelowPercent = 30; // magic/summon soldiers sip a mana potion from the heal slot below this fraction of their pool
		public const int PotionSicknessTicks = 60 * 30;
		public const int RegenDelayTicks = 60 * 5;
		public const int RegenIntervalTicks = 60;
		public const int ImmuneTicksOnHit = 20;

		// ---------- Economy ----------
		/// <summary>Levy daily wage by tier T1..T6 in copper.</summary>
		public static readonly long[] TierLevyWage = { 1000, 2500, 4000, 6000, 8000, 10000 };
		public const float StoodDownWageMult = 0.5f;
		public const int MusterFeeDays = 5;
		public const int MilitiaMusterDivisor = 5;        // L / 5 per head per event
		public const int RecruitingDriveWageMult = 2;     // 2 x L per manpower point
		public const int HazardPayDays = 1;
		public const float HazardPayFearMult = 0.75f;
		public static readonly float[] DesertionChanceByDaysUnpaid = { 0f, 0f, 0.25f, 0.5f }; // index = days unpaid (clamped)

		// ---------- Settlement / manpower ----------
		public const int SettlementRadiusX = 120, SettlementRadiusY = 70;
		// Armories within this many tiles of a Barracks arm its militia. Matches the settlement footprint so an Armory anywhere
		// in the town links — it no longer has to sit right next to the Barracks.
		public const int ArmoryLinkRadius = 120;
		public const int MinTownNpcsForMilitia = 2;
		// A boss must be within this many tiles of a player to call out the garrison — keeps ambient/modded bosses from perma-mustering.
		public const int BossMusterTiles = 250;
		// Manpower is the militia RESERVE (drains 1 per death). Simultaneous bodies on the field are capped separately by
		// LevyConfig.RenderCap, so a deep reserve just lets a town absorb a long invasion's attrition, not flood the screen.
		public const int ManpowerPerNpc = 9, ManpowerPerNpcConscription = 15;
		public const int NpcsPerManpowerRegen = 2;       // +1 per 2 town NPCs at dawn, rounded up
		public const int DisciplineExtraRegen = 1;
		public const int FortificationDefense = 4, FortificationRadius = 60;
		public const float PlunderCoinMult = 1.5f;
		public const int EventBookDropDenominator = 50;
		public const int TrophyRadius = 40;
		public const int TrophyTerrorReduction = 2;

		// ---------- Muster / bodies ----------
		public const int RenderCapDefault = 40;
		public const int MusterStreamMinTicks = 15, MusterStreamMaxTicks = 30;
		public const int ReinforcementDelayTicks = 120;
		public const int RegularRespawnTicks = 60 * 60;
		public const int ThinkIntervalTicks = 6;

		// ---------- Brain / ranges ----------
		public const int RangeMelee = 3;
		// RangeGun/RangeBow/RangeMagic live in LevyConstants.Weapons.cs; KiteMin/KiteMax and the standoff numbers in LevyConstants.Brain.cs.
		public const int HoldChaseLimit = 15, PostChaseLimit = 20;
		public const int FollowDistance = 12;
		public const int TeleportDistance = 60;
		public const int HaulNoEnemyRadius = 25, HaulPickupRadius = 6, HaulPlayerExclusion = 8;
		public const int ResupplyArmoryRadius = 40;
		public const int AmmoPouchMax = 999;
		public const int OrderRadius = 40;
		public const int RallyRadius = 30;
		public const int RallyCooldownTicks = 60 * 60;
		public const int RoutRallyOutOfSightTicks = 60 * 20;
		public const int PatrolPauseTicks = 120;
		public const int StuckTicks = 120, StuckTeleportTicks = 360;
		public const int FollowHomeIfPlayerDead = 1;
		public const int ChatBubbleCooldown = 60 * 8;

		// ---------- Boss rule ----------
		public const int SpiteDistance = 6;
		public const int SpiteTicks = 60;
		public const int SpiteDamageWindowTicks = 180;
		public const float SpiteDamageFraction = 0.01f;
		public const int RedirectEveryNth = 3;
		public const int RedirectDurationTicks = 180;
		public const float HpScalePerCp = 0.02f;
		public const float HpScaleCap = 0.80f;
		public static readonly int[] DashAllowlist = { NPCID.EyeofCthulhu, NPCID.Retinazer, NPCID.Spazmatism, NPCID.DukeFishron, NPCID.SkeletronHead };

		// ---------- Fear ----------
		public static class Fear
		{
			public const int BossViewRange = 80;
			public const float BossInViewPerTick = 0.02f;   // x terror
			public const float ComradeHitMult = 40f;        // x (dmg / comrade max HP)
			public const float ComradeDeath = 25f;
			public const float SelfHitMult = 60f;           // x (dmg / own max HP)
			public const float PhaseChange = 15f;
			public const int PlayerNearTiles = 10;
			public const float PlayerNearMult = 0.5f;
			public const float NoBossDecayPerSecond = 2f;
			public const float ShakenThreshold = 50f;
			public const float RallyValue = 25f;
			public const float GreenhornMult = 1.10f, ZealotMult = 0.90f, VeteranMult = 0.70f;
			public static readonly float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };
		}

		// ---------- Terror table (0-10) keyed by NPC type ----------
		public static readonly Dictionary<int, int> Terror = new()
		{
			{ NPCID.KingSlime, 2 }, { NPCID.EyeofCthulhu, 3 }, { NPCID.EaterofWorldsHead, 4 }, { NPCID.BrainofCthulhu, 4 },
			{ NPCID.QueenBee, 4 }, { NPCID.Deerclops, 5 }, { NPCID.SkeletronHead, 5 }, { NPCID.WallofFlesh, 7 }, { NPCID.QueenSlimeBoss, 4 },
			{ NPCID.Retinazer, 6 }, { NPCID.Spazmatism, 6 }, { NPCID.TheDestroyer, 7 }, { NPCID.SkeletronPrime, 6 }, { NPCID.Plantera, 8 },
			{ NPCID.Golem, 5 }, { NPCID.HallowBoss, 9 }, { NPCID.DukeFishron, 9 }, { NPCID.CultistBoss, 6 }, { NPCID.MoonLordCore, 10 },
		};

		/// <summary>Main-boss head/core types (the vanilla boss list). Parts resolve through realLife.</summary>
		public static readonly int[] MainBosses =
		{
			NPCID.KingSlime, NPCID.EyeofCthulhu, NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu, NPCID.QueenBee, NPCID.Deerclops, NPCID.SkeletronHead,
			NPCID.WallofFlesh, NPCID.QueenSlimeBoss, NPCID.Retinazer, NPCID.Spazmatism, NPCID.TheDestroyer, NPCID.SkeletronPrime, NPCID.Plantera,
			NPCID.Golem, NPCID.HallowBoss, NPCID.DukeFishron, NPCID.CultistBoss, NPCID.MoonLordCore,
		};

		// ---------- Dialogue budget ----------
		public const int FloatingLineIntervalTicks = 60 * 4;
		public const int EmotePerSoldierTicks = 60 * 8;
		public const int LineRepeatCooldownTicks = 60 * 120;
		public const int NameplateHideAbove = 20;
		public const int IdleChatCooldownTicks = 60 * 20;

		// ---------- Weapons ----------
		/// <summary>Multishot: item type → (count, total spread degrees). Everything else fires one projectile.</summary>
		public static readonly Dictionary<int, (int count, float spreadDeg)> Multishot = new()
		{
			{ ItemID.Boomstick, (3, 20f) }, { ItemID.Shotgun, (4, 25f) }, { ItemID.TacticalShotgun, (6, 25f) }, { ItemID.QuadBarrelShotgun, (4, 30f) },
			{ ItemID.OnyxBlaster, (4, 20f) }, { ItemID.Xenopopper, (4, 15f) }, { ItemID.Tsunami, (5, 10f) }, { ItemID.Phantasm, (4, 6f) },
			{ ItemID.DD2BetsyBow, (3, 12f) }, { ItemID.Marrow, (2, 8f) },
		};
		/// <summary>Ammo whose projectile homes; the soldier skips the lead.</summary>
		public static readonly HashSet<int> HomingAmmo = new() { ItemID.ChlorophyteBullet, ItemID.ChlorophyteArrow, ItemID.CursedDart, ItemID.IchorDart };
		public const float ArrowGravity = 0.1f;   // per tick after the arrow's grace period (see Projectile AI 1)
		public const int ArrowGravityDelayTicks = 15;
		public const float BulletNoGravity = 0f;
		public const int MeleeStepBackPixels = 8;
		public const int MinThinkRangeTiles = 50;
	}
}
