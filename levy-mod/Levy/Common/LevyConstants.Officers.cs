using Terraria.ID;

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
			/// <summary>Base command slots every commander starts with. The live cap is <c>OfficerSystem.MaxSlots()</c> = this plus one per crossed boss milestone (Wall of Flesh, any mechanical boss, Plantera, Golem, Moon Lord).</summary>
			public const int OfficerBaseSlots = 3;

			// ---- economy (copper; multiples of the tier's Levy wage so it scales with the world) ----
			/// <summary>An officer's daily wage in Levy wages (a Levy is 1x, a Man-at-Arms 3x). Paid at dawn from the barracks that commissioned him.</summary>
			public const float WageMult = 2f;
			/// <summary>The one-time commission fee, in days of the officer's wage (same idea as a soldier's muster fee).</summary>
			public const int FeeDays = LevyConstants.MusterFeeDays;
			/// <summary>Consecutive unpaid dawns after which an officer goes OFF DUTY: the commission and the slot are kept, but no avatar marches and no kit runs until a dawn payroll pays him again. A commission is never ended for want of pay.</summary>
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

			// ---- the parked town NPC (what vanilla draws for an officer; the avatar only feeds it a velocity and a facing) ----
			/// <summary>The most horizontal speed (px/tick) handed to the town NPC's stock walk animation: about the quickest a stock town NPC ever moves (fleeing). Above it the legs would cycle faster than any stock town NPC's; the avatar itself still moves at its real speed.</summary>
			public const float TownStrideMaxSpeedPx = 2f;

			// =====================================================================================================
			// THE SHRED MARK. The Dryad and the Tax Collector hang it on enemies in reach; while it stands, every army shot (soldiers, their
			// summons, an officer's bolt) lands harder there. The amp lives on the enemy side (Common/Mark/MarkAmpGlobalNPC).
			// =====================================================================================================
			// ---- the mark (read by MarkAmpGlobalNPC on the enemy side) ----
			/// <summary>Extra damage the whole army deals to a marked enemy, as a percent. A clean multiplier on final damage, so it stays relevant on bosses.</summary>
			public const float MarkDamagePct = 25f;
			/// <summary>The mark's amp as a fraction (MarkDamagePct / 100). What the amp hook multiplies final damage by, plus one.</summary>
			public static readonly float MarkDamageFrac = MarkDamagePct / 100f;

			// =====================================================================================================
			// MORALE — the ONE morale buff (Content/Buffs/MoraleBuff) that the Princess, the Guide and the Merchant keep on the army. A single icon
			// folding damage + defense + crit. It refreshes rather than stacks, so a soldier standing in two morale auras still carries exactly one.
			// Read by SoldierNPC.ApplyBuffEffect through MoraleBuff.Apply.
			// =====================================================================================================
			/// <summary>Damage Morale adds, as a fraction (0.12 = +12%).</summary>
			public const float MoraleDamage = 0.12f;
			/// <summary>Defense Morale adds.</summary>
			public const int MoraleDefense = 10;
			/// <summary>Critical-hit chance Morale adds, in percentage points.</summary>
			public const float MoraleCrit = 10f;

			// ---- the Guide (accuracy + a Morale aura; the guiding bolt is his own OfficerWeapon) ----
			/// <summary>Commission/wage multiplier for the Guide: a touch pricier than a plain fighter for an army-wide accuracy and morale aura.</summary>
			public const float GuideCostMult = 1.3f;
			/// <summary>Taunt weight. A frontline guide is not especially loud; enemies weigh it about like a soldier.</summary>
			public const int GuideAggro = 0;
			/// <summary>Soldiers within this many tiles of the Guide shoot straighter AND are kept under Morale: one reach for both.</summary>
			public const int GuideAuraRadiusTiles = 20;
			/// <summary>Ticks each Morale application lasts — refreshed every pulse, so it holds at this and never stacks. Longer than the cadence so it never lapses.</summary>
			public const int GuideAuraDurationTicks = 300;
			/// <summary>Ticks between Morale pulses (re-feeds once a second).</summary>
			public const int GuideAuraCadenceTicks = 60;
			/// <summary>Multiplier on the aim error (angle jitter + lead error) of every soldier in reach: 0.75 = 25% truer shots. Multiplies with Veteran, the Discipline book and the Arms Dealer's, so officers stack.</summary>
			public const float GuideAimErrorMult = 0.75f;

			// ---- the Guide's guiding bolt (his own OfficerWeapon: a poke at whatever strays into reach) ----
			public const float GuideBoltSpeed = 11f;
			/// <summary>Arc lift (px) a far shot is aimed above the target. Zero: a gem bolt flies straight, with no gravity to lead.</summary>
			public const float GuideBoltGravityCorrection = 0f;
			public const int GuideBoltDamage = 14;
			public const float GuideBoltKnockback = 2f;
			public const int GuideBoltCooldown = 24, GuideBoltRandomCooldown = 16;
			public const float GuideBoltRangeTiles = 30f;
			// Slot growth: see OfficerBaseSlots above; the milestone set lives in OfficerSystem.MaxSlots().
			// Aura infra: AuraKit holds no numbers of its own; each concrete aura (below) declares its own.
			// =====================================================================================================

			// ---- the Angler (the buff-aura proof officer) ----
			/// <summary>Wage/fee multiplier on top of the standard officer cost: the aura is worth a premium.</summary>
			public const float AnglerCostMult = 1.2f;
			/// <summary>Soldiers and officers within this many tiles of the Angler are kept Well Fed.</summary>
			public const int AnglerAuraRadiusTiles = 24;
			/// <summary>Ticks each Well Fed application lasts — refreshed every pulse, so it holds at this and never stacks. A little longer than the cadence so it never lapses between pulses.</summary>
			public const int AnglerAuraDurationTicks = 300;
			/// <summary>Ticks between aura pulses (re-feeds once a second).</summary>
			public const int AnglerAuraCadenceTicks = 60;
			/// <summary>Taunt weight: a support officer, not a target magnet (negative cancels the flat advantage soldiers have, so enemies do not single him out).</summary>
			public const int AnglerAggro = -200;
			/// <summary>Launch speed (px/tick) of a thrown frost daggerfish and the arc lift (px) a far throw is aimed above the target.</summary>
			public const float AnglerFishSpeed = 12f, AnglerFishGravityCorrection = 10f;
			/// <summary>Base damage and knockback of a thrown frost daggerfish (scaled with the world's progress like a town NPC's shot).</summary>
			public const int AnglerFishDamage = 16;
			public const float AnglerFishKnockback = 3f;
			/// <summary>Pause after a throw: fixed ticks plus a random extra.</summary>
			public const int AnglerFishCooldown = 30, AnglerFishRandomCooldown = 20;
			/// <summary>How far (tiles) an enemy may be and still be flung a fish at.</summary>
			public const float AnglerFishRangeTiles = 22f;

			// =====================================================================================================
			// BUFF / DEBUFF OFFICERS (rebuild) — Princess (army-wide Morale aura), Dryad (AoE shred + Dryad's Blessing),
			// Goblin Tinkerer (reforges weapons on the march + spiky balls), Tax Collector (AoE shred + coin bounty on a
			// marked kill). The aura and mark infrastructure is shared (AuraKit, MarkSystem); only these numbers are new.
			// =====================================================================================================

			// ---- the Princess (army-wide Morale aura; the capstone) ----
			/// <summary>Wage/fee multiplier: the strongest morale lift in the roster, priced to match.</summary>
			public const float PrincessCostMult = 2.5f;
			/// <summary>Taunt weight: a rear support, never singled out (negative cancels the flat advantage soldiers have in enemy targeting).</summary>
			public const int PrincessAggro = -250;
			/// <summary>Soldiers and officers within this many tiles are kept under Morale. The widest morale reach in the roster: she lifts the whole army.</summary>
			public const int PrincessAuraRadiusTiles = 26;
			/// <summary>Ticks each application lasts — refreshed every pulse, so it holds at this and never stacks. Longer than the cadence so it never lapses.</summary>
			public const int PrincessAuraDurationTicks = 300;
			/// <summary>Ticks between aura pulses (re-feeds once a second).</summary>
			public const int PrincessAuraCadenceTicks = 60;

			// ---- the Dryad (AoE shred + Dryad's Blessing nature aura) ----
			/// <summary>Wage/fee multiplier: two army-wide effects on one body.</summary>
			public const float DryadCostMult = 1.8f;
			/// <summary>Taunt weight: a support caster, not a target magnet.</summary>
			public const int DryadAggro = -200;
			/// <summary>Dryad's Bane hangs the shred on every hostile within this many tiles.</summary>
			public const int DryadMarkRadiusTiles = 26;
			/// <summary>Ticks a Dryad's Bane mark lasts if not refreshed — longer than the cadence so it reads as continuous.</summary>
			public const int DryadMarkDurationTicks = 180;
			/// <summary>Soldiers within this many tiles are kept under Dryad's Blessing.</summary>
			public const int DryadAuraRadiusTiles = 22;
			/// <summary>Ticks each Blessing application lasts (refreshed every pulse, so it holds steady and never stacks).</summary>
			public const int DryadAuraDurationTicks = 300;
			/// <summary>Ticks between the Dryad's pulses (one timer drives both the AoE mark and the Blessing).</summary>
			public const int DryadCadenceTicks = 60;
			/// <summary>Fraction of a contact hit a soldier under Dryad's Blessing reflects back at the attacker (capped by <see cref="LevyConstants.ThornsDamageCap"/>). Read in ApplyBuffEffect.</summary>
			public const float DryadThornsReflectFrac = 0.5f;
			/// <summary>Defense and life-regen units Dryad's Blessing adds. Vanilla's own Dryad's Blessing numbers (+8 defense, +6 regen); thorns is <see cref="DryadThornsReflectFrac"/>.</summary>
			public const int DryadBlessingDefense = 8, DryadBlessingLifeRegen = 6;

			// ---- the Goblin Tinkerer (reforges the army's weapons on the march + thrown spiky balls) ----
			/// <summary>Wage/fee multiplier.</summary>
			public const float GoblinCostMult = 1.4f;
			/// <summary>Taunt weight: a frontline fighter, weighed about like a soldier.</summary>
			public const int GoblinAggro = 0;
			/// <summary>He works on the weapon of the nearest soldier within this many tiles whose weapon sits below the floor.</summary>
			public const int GoblinReforgeRadiusTiles = 18;
			/// <summary>Ticks between his reforge pulses (one soldier per pulse): every 4 s.</summary>
			public const int GoblinReforgeCadenceTicks = 240;
			/// <summary>The quality floor he reforges toward: a weapon qualifies once its prefix is among the top this-fraction of the prefixes THAT weapon can roll (0.25 = the top quarter; the Armory's "Great" floor).</summary>
			public const float GoblinReforgeTopFraction = 0.25f;
			/// <summary>Paid rerolls he spends on one weapon per pulse before moving on; each costs the vanilla reforge price from the treasury.</summary>
			public const int GoblinReforgeRollsPerPulse = 4;
			/// <summary>He never takes the treasury below this many Levy wages, so a reforge can never be the reason dawn payroll fails.</summary>
			public const int GoblinReforgeReserveWages = 20;
			/// <summary>Launch speed (px/tick) of a thrown spiky ball and the arc lift (px) a far throw is aimed above the target.</summary>
			public const float GoblinBallSpeed = 11f, GoblinBallGravityCorrection = 9f;
			/// <summary>Base damage of a thrown spiky ball (scaled with the world's progress like a town NPC's shot).</summary>
			public const int GoblinBallDamage = 20;
			public const float GoblinBallKnockback = 2.5f;
			/// <summary>Pause after a throw: fixed ticks plus a random extra.</summary>
			public const int GoblinBallCooldown = 28, GoblinBallRandomCooldown = 18;
			/// <summary>How far (tiles) an enemy may be and still be flung a spiky ball.</summary>
			public const float GoblinBallRangeTiles = 20f;

			// ---- the Tax Collector (AoE shred + coin bounty on a marked kill; the bounty lives on TaxBountyGlobalNPC) ----
			/// <summary>Wage/fee multiplier.</summary>
			public const float TaxCostMult = 1.5f;
			/// <summary>Taunt weight: he keeps to the rear, never singled out.</summary>
			public const int TaxAggro = -250;
			/// <summary>He hangs the shred on every hostile within this many tiles.</summary>
			public const int TaxMarkRadiusTiles = 22;
			/// <summary>Ticks a tax mark lasts if not refreshed.</summary>
			public const int TaxMarkDurationTicks = 180;
			/// <summary>Ticks between his mark pulses.</summary>
			public const int TaxMarkCadenceTicks = 60;
			/// <summary>A marked enemy that dies within this many tiles of him is taxed.</summary>
			public const int TaxBountyRadiusTiles = 30;
			/// <summary>Copper paid per point of the slain enemy's max life, so tougher prey pays more and the bounty scales with the world.</summary>
			public const float TaxBountyCopperPerMaxHp = 2f;
			/// <summary>Floor and ceiling (copper) on one bounty: at least a little, at most a few gold.</summary>
			public const long TaxBountyMinCopper = 100, TaxBountyMaxCopper = 50000;
			// WEAPON OFFICERS (levy-weapon-rebuild). Four frontline fighters. Each: a weapon, a kit, and a cost.
			// =====================================================================================================

			// ---- the Demolitionist (lobbed grenades + a periodic heavy dynamite) ----
			/// <summary>Wage/fee multiplier of the Demolitionist (a heavy-AoE fighter costs a premium).</summary>
			public const float DemolitionistCostMult = 1.4f;
			/// <summary>Taunt weight: a frontline fighter, weighed about like a soldier.</summary>
			public const int DemolitionistAggro = 0;
			/// <summary>His lobbed grenade: launch speed (px/tick) and the arc lift (px) a far throw is aimed above the target.</summary>
			public const float DemoGrenadeSpeed = 10f, DemoGrenadeGravityCorrection = 16f;
			/// <summary>Base grenade damage and knockback (scaled with the world's progress like a town NPC's shot).</summary>
			public const int DemoGrenadeDamage = 24;
			public const float DemoGrenadeKnockback = 4f;
			/// <summary>Pause after a grenade: fixed ticks plus a random extra.</summary>
			public const int DemoGrenadeCooldown = 42, DemoGrenadeRandomCooldown = 24;
			/// <summary>How far (tiles) an enemy may be and still be lobbed a grenade.</summary>
			public const float DemoGrenadeRangeTiles = 24f;
			/// <summary>The kit's big dynamite: how often it is hurled (ticks) and how far it reaches (tiles).</summary>
			public const int DemoDynamiteIntervalTicks = 360;
			public const float DemoDynamiteRangeTiles = 26f;
			/// <summary>The dynamite's launch arc (speed px/tick, gravity lift px), base blast damage (scaled by boss progress) and knockback.</summary>
			public const float DemoDynamiteSpeed = 9f, DemoDynamiteGravityCorrection = 20f;
			public const int DemoDynamiteDamage = 70;
			public const float DemoDynamiteKnockback = 6f;

			// ---- the Arms Dealer (rapid rifle + truer shots and bottomless ammo for the soldiers near him) ----
			/// <summary>Wage/fee multiplier of the Arms Dealer.</summary>
			public const float ArmsDealerCostMult = 1.5f;
			public const int ArmsDealerAggro = 0;
			/// <summary>His rifle: fast, light bullets with a hair of gravity lead.</summary>
			public const float ArmsDealerBulletSpeed = 13f, ArmsDealerBulletGravityCorrection = 1f;
			/// <summary>Base bullet damage and knockback — light per shot, but fired in a rapid stream.</summary>
			public const int ArmsDealerBulletDamage = 13;
			public const float ArmsDealerBulletKnockback = 2f;
			/// <summary>Pause after a shot: short, so the fire reads as rapid.</summary>
			public const int ArmsDealerBulletCooldown = 11, ArmsDealerBulletRandomCooldown = 6;
			public const float ArmsDealerBulletRangeTiles = 32f;
			/// <summary>Soldiers within this many tiles of him shoot straighter and never spend ammo. Checked shot by shot (Common/Officers/OfficerSupport), so there is no buff and no timer.</summary>
			public const int ArmsDealerSupplyRadiusTiles = 22;
			/// <summary>Multiplier on the aim error (angle jitter + lead error) of every soldier in reach: 0.6 = 40% truer shots. Multiplies with Veteran, the Discipline book and the Guide's, so officers stack.</summary>
			public const float ArmsDealerAimErrorMult = 0.6f;

			// ---- Archery (the buff, whoever hands it out: today the armory's Archery potions) ----
			/// <summary>Ranged damage the Archery buff adds as a fraction. Extends vanilla Archery (arrow weapons only) to every ranged soldier — guns included. Read by SoldierNPC.ApplyBuffEffect.</summary>
			public const float ArcheryRangedDamage = 0.15f;

			// ---- the Merchant (throwing knives + a fence on nearby kills + a quartermaster's handouts + a small Morale lift) ----
			/// <summary>Wage/fee multiplier of the Merchant.</summary>
			public const float MerchantCostMult = 1.4f;
			public const int MerchantAggro = 0;
			/// <summary>His throwing knives: launch speed (px/tick) and the arc lift (px) a far throw is aimed above the target. The vanilla Merchant throws the same knife with a lift of 16.</summary>
			public const float MerchantKnifeSpeed = 12f, MerchantKnifeGravityCorrection = 14f;
			/// <summary>Base knife damage and knockback (scaled with the world's progress like a town NPC's shot).</summary>
			public const int MerchantKnifeDamage = 16;
			public const float MerchantKnifeKnockback = 2f;
			public const int MerchantKnifeCooldown = 22, MerchantKnifeRandomCooldown = 14;
			public const float MerchantKnifeRangeTiles = 24f;
			/// <summary>THE FENCE. An enemy that dies within this many tiles of him pays the treasury (the paying barracks) its own loot value times <see cref="MerchantFenceValueMult"/>, on top of what it drops.</summary>
			public const int MerchantFenceRadiusTiles = 30;
			/// <summary>Treasury credit per kill as a multiple of the corpse's coin value (NPC.value): 1 = the kill is worth double.</summary>
			public const float MerchantFenceValueMult = 1f;
			/// <summary>THE HANDOUTS. Every cadence he serves up to <see cref="MerchantServePerPulse"/> soldiers within this many tiles with the basic stock a soldier can use: ammo for the weapon in his hand, or a potion if he would drink one. Paid from the treasury at the item's price.</summary>
			public const int MerchantSupplyRadiusTiles = 16;
			public const int MerchantSupplyCadenceTicks = 180, MerchantServePerPulse = 3;
			/// <summary>A soldier whose ammo pouch holds fewer rounds than this is handed <see cref="MerchantAmmoHandout"/> more (basic ammo of the kind his weapon takes).</summary>
			public const int MerchantAmmoLowBelow = 150, MerchantAmmoHandout = 100;
			/// <summary>Basic ammo he stocks, by the ammo class a weapon takes: parallel arrays (AmmoID, the cheap ItemID for it). Torches and rope are on his shelf too, but a soldier has no use for them.</summary>
			public static readonly int[] MerchantAmmoClass = { AmmoID.Arrow, AmmoID.Bullet, AmmoID.Dart, AmmoID.Rocket, AmmoID.Gel, AmmoID.Flare };
			public static readonly int[] MerchantAmmoItem = { ItemID.WoodenArrow, ItemID.MusketBall, ItemID.Seed, ItemID.RocketI, ItemID.Gel, ItemID.Flare };
			/// <summary>THE MORALE LIFT. Soldiers within this many tiles stay under Morale: the same buff the Princess keeps on the whole army, but only for those close to his cart.</summary>
			public const int MerchantMoraleRadiusTiles = 12;
			public const int MerchantMoraleDurationTicks = 300, MerchantMoraleCadenceTicks = 60;

			// ---- the Painter (colorful paint barrage; no kit) ----
			/// <summary>Wage/fee multiplier of the Painter.</summary>
			public const float PainterCostMult = 1.3f;
			public const int PainterAggro = 0;
			/// <summary>His paint barrage: vivid paintballs, quick and hard-hitting.</summary>
			public const float PainterPaintballSpeed = 12f, PainterPaintballGravityCorrection = 2f;
			/// <summary>Base paintball damage and knockback (scaled with the world's progress).</summary>
			public const int PainterPaintballDamage = 28;
			public const float PainterPaintballKnockback = 3f;
			public const int PainterPaintballCooldown = 15, PainterPaintballRandomCooldown = 9;
			public const float PainterPaintballRangeTiles = 30f;
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
