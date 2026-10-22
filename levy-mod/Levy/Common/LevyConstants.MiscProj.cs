namespace Levy.Common
{
	/// <summary>Numbers for the per-item shots and projectile anchors outside the held-weapon chain (Zenith swords, Clinger Staff wall, Lunar sentries). Additive partial.</summary>
	public static partial class LevyConstants
	{
		// ---------- Zenith (Player.ItemCheck_Shoot item 4956) ----------
		/// <summary>The sword fan per swing is useAnimation / useTime shots (3 for the vanilla item), never more than this.</summary>
		public const int ZenithMaxShotsPerSwing = 4;
		/// <summary>Vanilla scatters the last sword's aim with Main.rand.NextVector2Circular(150, 150).</summary>
		public const float ZenithScatterPx = 150f;
		/// <summary>A sword's ai[0] (arc width) is rolled in [-ZenithArcRoll, +ZenithArcRoll].</summary>
		public const int ZenithArcRoll = 100;
		/// <summary>Longest body-to-target offset a sword's launch vector is built from (a player's cursor is limited to the screen; the launch velocity is half this offset).</summary>
		public const float ZenithMaxAimPx = 800f;

		// ---------- Elf Melter (Player.ItemCheck_Shoot item 1910) ----------
		/// <summary>Vanilla spawns the Elf Melter's Flames with ai[0] = 1, which makes them fly the full 60 ticks as blue flames (a plain Flamethrower leaves ai[0] = 0 and they burn out at 50 as orange).</summary>
		public const float ElfMelterFlameAi0 = 1f;

		// ---------- Clinger Staff (Player.ItemCheck_Shoot item 3014) ----------
		/// <summary>Safety cap on the march from the muzzle toward the target (vanilla loops until the line is blocked or the cursor is reached).</summary>
		public const int ClingerMarchMaxSteps = 400;
		/// <summary>The wall's column search runs at most this many tiles up and down, and the wall is at most this many tiles tall.</summary>
		public const int ClingerScanTiles = 30, ClingerMaxWallTiles = 15;
		/// <summary>Stop marching once this close (plus the step size) to the target, in pixels.</summary>
		public const float ClingerArrivePx = 20f;

		// ---------- Spore Sac traps (ProjectileID.SporeTrap / SporeTrap2, aiStyle 105: Projectile.AI_105) ----------
		/// <summary>A trap lives until its age passes this many ticks (90 s); it then fades and stops hurting.</summary>
		public const float SporeTrapMaxAge = 5400f;
		/// <summary>Besides 1 a tick, a trap ages by its distance to the wearer divided by this (in pixels).</summary>
		public const float SporeAgeDistDivisor = 100f;
		/// <summary>The distance-age term is multiplied by each factor in turn once it exceeds its threshold, so far-off traps wither quickly.</summary>
		public static readonly (float over, float mult)[] SporeAgeFalloff =
			{ (4f, 1.1f), (5f, 1.2f), (6f, 1.3f), (7f, 1.4f), (8f, 1.5f), (9f, 1.6f), (10f, 1.7f) };
		/// <summary>Extra age per tick when no sac-wearing body backs the trap (vanilla adds this for an owner not wearing the sac): its soldier is gone.</summary>
		public const float SporeOrphanAgePerTick = 100f;

		// ---------- Spectre Mask set bonus (Projectile.ghostHurt -> ProjectileID.SpectreWrath) ----------
		/// <summary>Vanilla keeps the wearer's recent bolt damage in Player.ghostDmg and drops 6.6666665 of it per tick (400 a second); no bolt spawns while it is above 1500 (the cap lives in Projectile.ghostHurt).</summary>
		public const float GhostDmgDecayPerTick = 6.6666665f;

		// ---------- Chlorophyte set bonus (Player.crystalLeaf -> ProjectileID.CrystalLeaf / CrystalLeafShot) ----------
		/// <summary>Vanilla aiStyle 42: the leaf hangs 60 px above the wearer's centre, scans 700 px (|dx|+|dy|) for a hostile in line of sight, and fires every 40 ticks (Player.crystalLeafCooldown).</summary>
		public const float LeafLiftPx = 60f, LeafRangePx = 700f;
		public const int LeafCooldownTicks = 40, LeafScanRetryTicks = 6;
		/// <summary>Shot launch speed per extra-update (the leaf shot runs 180 of them a tick) and the lead window, both vanilla's.</summary>
		public const float LeafShotSpeed = 12f;
		public const int LeafShotLeadUpdates = 180;
		/// <summary>How often SoldierNPC tops up / trims the soldier's leaf, and the pause after a spawn.</summary>
		public const int LeafMaintainIntervalTicks = 20, LeafSpawnCooldownTicks = 40;

		// ---------- Armor set bonuses that need a body of their own (Player.UpdateArmorSets / OnHit_Inner / Hurt; SoldierNPC.ArmorSets.cs) ----------
		/// <summary>Spectre Hood (Player.ghostHeal -> Projectile.ghostHeal): a magic hit heals 20% of its damage, less 5% per target it has already pierced, from a pool that refills 0.6 a tick (0.5 in Expert+) up to 80 (70).</summary>
		public const float HoodHealFraction = 0.2f, HoodHealPierceDecay = 0.05f, HoodPoolMax = 80f, HoodPoolMaxExpert = 70f, HoodPoolRefill = 0.6f, HoodPoolRefillExpert = 0.5f;
		/// <summary>The heal bolt (ProjectileID.SpiritHeal, aiStyle 52) cruises at 4 px per extra-update, steers by (v*15 + aim)/16 and lands inside 50 px.</summary>
		public const float HoodBoltSpeed = 4f, HoodBoltArriveRangePx = 50f;
		public const int HoodBoltTimeLeftTicks = 900;

		/// <summary>Beetle armor (Player.beetleCounter): defense grows an orb every 180 ticks (max 3), each worn orb takes 15% off a hit and the hit pops one. Might builds a counter from melee damage, loses 3 a tick (plus countdown/20) and shows 1/2/3 orbs above 400 / 1600 / 5200, each worth +10% melee damage and speed.</summary>
		public const int BeetleDefenseTicksPerOrb = 180, BeetleMaxOrbs = 3;
		public const float BeetleDefenseReductionPerOrb = 0.15f;
		public const float BeetleMightOrb1 = 400f, BeetleMightOrb2 = 1600f, BeetleMightOrb3 = 5200f, BeetleMightCap = 6400f, BeetleMightDecayPerTick = 3f, BeetleMightLevelUpBonus = 200f, BeetleMightBonusPerOrb = 0.1f;
		public const int BeetleMightCountdownDivisor = 20;

		/// <summary>Solar Flare armor: a shield every 180 ticks (max 3); while one is up hits are 20% lighter, and a hit pops one into a 160 px flare worth 150 melee damage.</summary>
		public const int SolarTicksPerShield = 180, SolarMaxShields = 3;
		public const float SolarHitScale = 0.8f, SolarFlareDamage = 150f, SolarFlareKnockback = 15f;
		public const int SolarFlareProjectile = 608;

		/// <summary>Hallowed armor (onHitDodge): a hit readies a 30 s dodge, the dodge grants 80 ticks of immunity and a 30 s cooldown.</summary>
		public const int HallowedReadyTicks = 1800, HallowedCooldownTicks = 1800, HallowedDodgeImmuneTicks = 80;
		/// <summary>Palladium armor (onHitRegen): +4 life-regen units a tick for 300 ticks after a hit. Nebula boosters: +6 units a tick per life level, +15% damage per damage level, 480 ticks a level.</summary>
		public const int PalladiumRegenTicks = 300, PalladiumRegenUnits = 4, NebulaBoosterTicks = 480, NebulaLifeUnitsPerLevel = 6, NebulaMaxLevel = 3, NebulaCooldownTicks = 30, NebulaChanceDenominator = 3;
		public const float NebulaDamagePerLevel = 0.15f;
		/// <summary>Orichalcum armor (onHitPetal): a 36-damage petal (ProjectileID.FlowerPetal) every 20 ticks at the struck enemy, launched at 24 px a tick from some way off.</summary>
		public const int OrichalcumPetalDamage = 36, OrichalcumPetalCooldownTicks = 20;
		public const float OrichalcumPetalSpeed = 24f, OrichalcumPetalMinStartPx = 360f, OrichalcumPetalMaxStartPx = 620f, OrichalcumPetalSpreadPx = 320f;
		/// <summary>Titanium armor (onHitTitaniumStorm): a hit (10 tick gap) refreshes a 600 tick storm and adds a 50-damage shard (15 knockback) to an orbit of at most 7.</summary>
		public const int TitaniumStormTicks = 600, TitaniumShardMax = 7, TitaniumShardDamage = 50, TitaniumHitGapTicks = 10;
		public const float TitaniumShardKnockback = 15f, TitaniumOrbitBasePx = 24f, TitaniumOrbitPerShardPx = 6f, TitaniumOrbitFlatten = 0.05f, TitaniumTurnsPer300Ticks = 6f, TitaniumFollowLerp = 0.3f;
		/// <summary>Crimson armor: natural regeneration runs half again as fast. Frost armor: melee and ranged shots set Frostburn for 5-15 s.</summary>
		public const float CrimsonRegenSpeedup = 1.5f;
		public const int FrostburnBuff = 324, FrostburnMinSeconds = 5, FrostburnMaxSeconds = 15;
		/// <summary>Shadow armor: +15% run speed. Cactus armor thorns: flat damage back (Classic / Expert / Master). Turtle armor reflects 200% of the contact hit (cap 1000).</summary>
		public const float ShadowArmorRunSpeed = 1.15f, TurtleThornsMult = 2f;
		public const int CactusThornsDamage = 15, CactusThornsDamageExpert = 30, CactusThornsDamageMaster = 45, ThornsDamageCap = 1000;
		public const float ThornsKnockback = 10f;
		/// <summary>Paladin's Shield: wearers within 800 px draw a third of the damage dealt to an ally (the ally takes 25% less); the nearest one above 25% health and out of immune time takes it.</summary>
		public const float PaladinRangePx = 800f, PaladinHealthFloor = 0.25f, PaladinAllyScale = 0.75f, PaladinShareFraction = 1f / 3f;
		public const int PaladinBeamDust = 12;
	}
}
