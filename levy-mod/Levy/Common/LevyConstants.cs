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
		/// <summary>The old fixed start. Kept only so the boot self-check can prove the boss-by-boss math still reproduces the original numbers.</summary>
		public const int CommandPointsBase = 4;
		public const int DefaultLeviesAtStart = 14;
		/// <summary>Default levy caps by stage: Wall of Flesh, Moon Lord, end of post-Moon Lord. A flat curve: most of the growth comes before hardmode.</summary>
		public const int DefaultLeviesPre = 28, DefaultLeviesHm = 34, DefaultLeviesPost = 40;
		public const int CpKingSlime = 1, CpEyeOfCthulhu = 2, CpEaterOrBrain = 2, CpQueenBee = 1, CpSkeletron = 2, CpDeerclops = 1,
			CpWallOfFlesh = 7, CpQueenSlime = 1, CpEachMech = 2, CpPlantera = 3, CpGolem = 3, CpEmpress = 1, CpFishron = 1, CpCultist = 2, CpMoonLord = 3;

		// ---------- Grades ----------
		public static readonly int[] GradeSlots = { 1, 3, 5 };
		public static readonly int[] GradeCpCost = { 1, 2, 3 };
		public static readonly float[] GradeWageMult = { 1f, 3f, 7f };
		public static readonly float[] GradeAimJitterDeg = { 6f, 3f, 1f };
		public static readonly float[] GradeLeadError = { 0.25f, 0.10f, 0.03f };
		public const float VeteranAimMult = 0.6f;
		public const float ShakenAimMult = 2f;
		public const float DrillMilitiaAimMult = 0.75f;
		/// <summary>A Guide to Discipline: every soldier of the settlement shoots with this fraction of its normal aim error (jitter + lead error).</summary>
		public const float DisciplineAimErrorMult = 0.65f;
		public const float ShakenDamageMult = 0.6f;
		public const int VeteranBossFights = 3;
		public const float VeteranMaxFraction = 0.25f;

		// ---------- Armor sets / accessory ranking ----------
		/// <summary>Set pick score = sum of piece scores + bonuses: a set whose pieces trigger a vanilla set bonus, and a set whose doctrine matches the soldier's weapon class.</summary>
		public const float ArmorSetBonusScore = 300f, ArmorSetClassMatchScore = 1400f;
		/// <summary>A complete matching set is only preferred while its piece scores total at least this fraction of the best loose pieces (never trade Hallowed for Wood just to match).</summary>
		public const float ArmorSetMinFractionOfBest = 0.5f;
		/// <summary>
		/// Class-matched armor (<see cref="Config.LevyConfig.ClassMatchArmor"/>): a set or piece of the levy's weapon class is worn over any other whenever its progression tier (Boss Checklist scale, see
		/// <see cref="Weapons.ProgressionTier"/>) is at most this far behind the best other armor in reach. Three tiers keeps Spectre over Beetle, Stardust over Solar Flare and Silva or Tarragon over God Slayer, and refuses
		/// pre-Hardmode class armor once the army is in Hardmode gear well past it.
		/// </summary>
		public const float ClassArmorTierTolerance = 3f;
		/// <summary>Per-piece pick: an on-class piece within the tolerance outranks every other piece by this much (the rest of the score still orders the on-class pieces).</summary>
		public const float ClassArmorPieceBonus = 1e6f;
		/// <summary>Cross-family (set-bonus verified) set search considers only the top N pieces of each slot.</summary>
		public const int ArmorSetCrossSearchTop = 6;
		/// <summary>Accessory score = rarity tier x Rarity + defense x Defense + log10(1 + value) x Value + prefix + class fit (damage fraction for the soldier's class) x ClassFit.</summary>
		public const float AccessoryRarityWeight = 100f, AccessoryDefenseWeight = 50f, AccessoryValueWeight = 30f, AccessoryPrefixBonus = 10f, AccessoryClassFitWeight = 12000f;
		/// <summary>Score taken off an accessory made for other classes only, for a levy it does nothing for: larger than any upgrade chain (5000 per level), so a fitting or generic accessory always wins, yet a slot is never left empty when nothing else is stocked.</summary>
		public const float AccessoryMismatchPenalty = 100000f;

		/// <summary>Survival effects read off a dummy wearing the accessory (see AccessoryEffect): points per regen unit, per 100% damage reduction, per extra life point, and the total cap. Rarity is only a small tie-break; what the item does decides.</summary>
		public const float AccessoryRegenWeight = 60f, AccessoryEnduranceWeight = 3000f, AccessoryLifeWeight = 3f, AccessoryEffectCap = 1200f;

		/// <summary>Mobility accessories (extra jumps, wings): value per kind, how many jumps count, and the small score bonus a second mobility accessory earns. Every levy takes one first.</summary>
		public static class Armory
		{
			public const float MobilityJumpValue = 1f, MobilityWingsValue = 4f;
			/// <summary>Wall climb gear (Player.spikedBoots): this much, plus the bonus for the kind that holds the body on the wall (spikedBoots 2).</summary>
			public const float MobilityClimbValue = 1f, MobilityClimbHoldBonus = 0.3f;
			public const int MobilityMaxJumpsCounted = 3;
			public const float MobilityStackBonus = 150f;
			/// <summary>Score points per unit of measured movement value when two pairs of wings or boots are ranked for a mobility slot (the accessory score decides first; this breaks a level ranking toward the faster pair).</summary>
			public const float MobilitySlotValueWeight = 150f;
			/// <summary>Score added to boots that raise the run speed or give rocket flight: above an upgrade chain (5000 per level), below a Preferred pick, so the boots slot never goes to a shoe that does not make a levy faster while real boots are stocked.</summary>
			public const float MobilityRunnerBoots = 100000f;
			/// <summary>A dash accessory in the horizontal slot: its base movement value and the extra per px/tick of start speed (a Tabi dash, 16.9, reads about 1.35; Hermes Boots read 0.6, Lightning Boots and up read 2.7 and more). In an ordinary accessory slot a dash counts this much.</summary>
			public const float MobilityDashValue = 1f, MobilityDashSpeedWeight = 0.02f, MobilityDashOrdinary = 0.3f;
		}

		// ---------- HP / mana ----------
		// A REGULAR mirrors the vanilla player exactly and no guide touches him: base HP + 20 per Life Crystal (15) + 5 per Life Fruit (20) = 500, and
		// base mana + 20 per Mana Crystal (9) = 200. MILITIA take no crystals; their whole ladder is the three progression guides (numbers in LevyConstants.Guides.cs).
		public const int BaseHp = 100;
		public const int HpPerLifeCrystal = 20;
		public const int HpPerLifeFruit = 5;
		/// <summary>
		/// Growth items a regular can absorb (15 Life Crystals, 20 Life Fruit) and the absolute HP ceiling of EVERY soldier, regular or militia: the vanilla player's 500 life.
		/// <see cref="MaxSoldierHp"/> is a hard cap: <c>SoldierRecord.MaxHp</c> never exceeds it, the body's <c>NPC.lifeMax</c> is pinned to that value every tick, and nothing raises it.
		/// </summary>
		public const int MaxLifeCrystals = 15, MaxLifeFruit = 20, MaxSoldierHp = 500;
		/// <summary>
		/// Mana per soldier: the base, what a Mana Crystal adds (9 of them fill a regular's pool) and the absolute ceiling of EVERY soldier, regular or militia
		/// (<see cref="MaxMana"/>, the vanilla player's 200). <c>SoldierRecord.MaxMana</c> never exceeds it.
		/// </summary>
		public const int BaseMana = 20, ManaPerCrystal = 20, MaxMana = 200;
		public const int PotionDrinkBelowPercent = 50;
		public const int ManaPotionDrinkBelowPercent = 30; // magic/summon soldiers sip a mana potion from the heal slot below this fraction of their pool
		/// <summary>Potion sickness after a healing potion: vanilla's Item.potionDelay (60 s). A levy's own value comes from its dummy (SoldierNPC.PotionDelayFor); this is the base for a body with no dummy.</summary>
		public const int PotionSicknessTicks = 60 * 60;
		public const int RegenDelayTicks = 60 * 5;
		public const int RegenIntervalTicks = 60;
		public const int ImmuneTicksOnHit = 20;
		/// <summary>Every post-hit immunity window a levy gets (the player-parity 40 / 20 / 80 ticks and the real Player.Hurt window of its gear) is scaled by this: 0.4 s to 0.3 s in Eve's words, so 40 ticks becomes 30.</summary>
		public const float HitImmuneScale = 0.75f;
		/// <summary>A post-hit immunity window after <see cref="HitImmuneScale"/> (never below 1 tick).</summary>
		public static int ScaledImmune(int ticks) => ticks <= 0 ? 0 : System.Math.Max(1, (int)System.Math.Round(ticks * HitImmuneScale));
		/// <summary>Player parity: a body hit grants the post-hit window Player.Hurt would (40 ticks, 20 for a 1-damage hit, doubled by Cross Necklace / Star Veil). LEVY_IFRAMES=0 restores the flat <see cref="ImmuneTicksOnHit"/> (A/B switch, harness only).</summary>
		public static readonly bool PlayerParityIFrames = System.Environment.GetEnvironmentVariable("LEVY_IFRAMES") != "0";

		// ---------- Economy ----------
		/// <summary>
		/// Levy daily wage by tier T1..T6 in copper. Every price in the mod is a multiple of this one number (soldier wages by grade, hazard pay, militia muster,
		/// recruiting drive, officer wages and fees), so it is kept deliberately flat: boss progress lifts per-head costs only gently and growing the army never
		/// prices the player out of it. Difficulty comes from the enemy, not from the bill. Scale the whole economy at once with <c>LevyConfig.WageMultiplier</c>.
		/// </summary>
		public static readonly long[] TierLevyWage = { 1000, 1200, 1400, 1700, 2000, 2400 };
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
		/// <summary>An armory with a vanilla pylon within this many tiles is on the pylon network and pools its stored gear with every other networked armory when issuing.</summary>
		public const float PylonShareRadius = 60f;
		/// <summary>An armory with a Magic Storage Heart / Storage Access / Remote Access (resolving to a heart) within this many tiles may draw issue gear from that network.</summary>
		public const float MagicStorageRadius = 50f;
		public const int MinTownNpcsForMilitia = 2;
		// A boss must be within this many tiles of a player to call out the garrison — keeps ambient/modded bosses from perma-mustering.
		public const int BossMusterTiles = 250;
		// Manpower is the militia RESERVE (drains 1 per death). Simultaneous bodies on the field are capped separately by
		// LevyConfig.RenderCap, so a deep reserve just lets a town absorb a long invasion's attrition, not flood the screen.
		public const int ManpowerPerNpc = 9, ManpowerPerNpcConscription = 15;
		public const int NpcsPerManpowerRegen = 2;       // +1 per 2 town NPCs at dawn, rounded up
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
		/// <summary>When ammo is scarce each soldier's pouch is capped at an equal share of the reachable stock, but never below this (a short fight) and never above AmmoPouchMax.</summary>
		public const int AmmoFairShareFloor = 25;
		public const int OrderRadius = 150; // order hotkeys reach every levy within about a screen and a half; the Command Radio and the barracks panel reach all of them
		public const int RallyRadius = 30;
		/// <summary>Rally with a Command Radio in the pack: wider than any world (tiles), so every Regular is steadied.</summary>
		public const int RadioRallyRadius = 100000;
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
		/// <summary>Boss life one command point adds at gear strength 1 (gear of the boss's own stage).</summary>
		public const float HpScalePerCp = 0.02f;
		/// <summary>Gear strength doubles every this many progression tiers the gear is ahead of the boss, and halves every this many behind; clamped to [Min, Max].</summary>
		public const float HpStrengthTiersPerDouble = 3f, HpStrengthMin = 0.05f, HpStrengthMax = 4f;
		/// <summary>Boss life scaling cap by progression (Boss Checklist scale, see ProgressionTier): from this progress on, a boss gains at most this fraction of its base life.
		/// Pre-Hardmode, Hardmode, Golem, Moon Lord, Providence, Devourer of Gods, Yharon and beyond.</summary>
		public static readonly (float from, float cap)[] HpScaleCapBands = { (0f, 0.8f), (7f, 1.2f), (13f, 1.6f), (18f, 2.2f), (19f, 2.8f), (21f, 3.4f), (22f, 4.0f) };
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
		public const int FloatingLineIntervalTicks = 300;
		public const int EmotePerSoldierTicks = 60 * 5;
		public const int LineRepeatCooldownTicks = 60 * 120;
		public const int NameplateHideAbove = 20;
		public const int IdleChatCooldownTicks = 60 * 20;

		// ---------- Weapons ----------
		/// <summary>Multishot: item type → (count, total spread degrees). Everything else fires one projectile.</summary>
		public static readonly Dictionary<int, (int count, float spreadDeg)> Multishot = new()
		{
			{ ItemID.Boomstick, (3, 20f) }, { ItemID.Shotgun, (4, 25f) }, { ItemID.TacticalShotgun, (6, 25f) }, { ItemID.QuadBarrelShotgun, (4, 30f) },
			{ ItemID.OnyxBlaster, (5, 20f) }, { ItemID.Xenopopper, (4, 15f) }, { ItemID.Tsunami, (5, 10f) }, { ItemID.Phantasm, (4, 6f) },
			{ ItemID.DD2BetsyBow, (3, 12f) }, { ItemID.BubbleGun, (3, 30f) },
			{ ItemID.VampireKnives, (5, 0f) }, // thrown as a fan of ~5 (see FireVampireKnives); only the ranking reads this entry
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
