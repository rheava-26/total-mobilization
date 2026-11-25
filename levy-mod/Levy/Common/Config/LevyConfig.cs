using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Config
{
	/// <summary>Server-side config. Everything has a sane default; the config exists for tuning, not for playing. Field names are saved in players' config files: never rename or remove one (a boot self-check enforces it).</summary>
	public class LevyConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static LevyConfig Get => ModContent.GetInstance<LevyConfig>();

		public override void OnLoaded() => Apply();
		public override void OnChanged() { Apply(); global::Levy.Common.Systems.LevySettingsNotes.OnConfigChanged(this); }
		private void Apply()
		{
			global::Levy.Common.Weapons.OwnerPuppet.SetConfig(SoldierEffectsAtSoldier);
			global::Levy.Common.Weapons.WeaponRank.SetConfig(WeaponRanking, SkipArmoredWeapons);
			global::Levy.Common.Weapons.WeaponUse.SetAimAccuracy(AimAccuracy);
			global::Levy.Common.Options.LevyOptions.SetConfig(FearModel == FearModelSetting.Events, FormationDefense);
		}

		// ---- Potions ----------------------------------------------------------------------------------------
		[Header("Potions")]
		[DefaultValue(true)] public bool AutoFeedCrystals;
		[DefaultValue(true)] public bool ArmoryTurnsStarsIntoMana;

		// ---- Aim --------------------------------------------------------------------------------------------
		[Header("Aim")]
		[Range(0.1f, 5f)] [Increment(0.1f)] [DefaultValue(1f)] public float AimAccuracy;
		[DefaultValue(true)] public bool RealWeaponShoot;

		// ---- Boss behaviour ---------------------------------------------------------------------------------
		[Header("BossBehaviour")]
		[DefaultValue(true)] public bool BossesPunishSoldiers;
		[DefaultValue(true)] public bool BossesTargetSoldiers;
		/// <summary>Calamity's Exo Mechs use their multi-target attacks on the army (Common/Boss/ArmyAwareExo.cs). Off by default; only acts while a beaten mech fights a fielded army.</summary>
		[DefaultValue(false)] public bool BossesAdaptToArmies;
		/// <summary>Retired (boss life now follows gear strength, BossHpArmyStrength); kept so saved config files still load. Hidden and ignored.</summary>
		[Newtonsoft.Json.JsonIgnore] [DefaultValue(2)] public int BossHpScalingPercentPerCp = 2;
		[Range(0f, 5f)] [Increment(0.1f)] [DefaultValue(1f)] public float BossHpArmyStrength;
		[Range(0, 1000)] [DefaultValue(0)] public int BossHpScalingCapPercent;
		[Range(0f, 3f)] [DefaultValue(1f)] public float FearMultiplier;
		[DefaultValue(FearModelSetting.Events)] [DrawTicks] public FearModelSetting FearModel;
		[DefaultValue(true)] public bool EnemiesTargetSoldiers;
		public List<int> ExtraTargetingAiStyles = new();
		/// <summary>Enemies the militia will never attack, by exact display name (case-insensitive). For boss-summoning
		/// enemies like the Perforator Cyst, whose death spawns a boss. Add any others you run into.</summary>
		public List<string> DoNotAttackNpcNames = new() { "Perforator Cyst" };

		// ---- Levy & militia counts --------------------------------------------------------------------------
		[Header("LevyCounts")]
		[Range(4, 200)] [DefaultValue(LevyConstants.DefaultLeviesAtStart)] public int LeviesAtStart;
		[Range(4, 200)] [DefaultValue(LevyConstants.DefaultLeviesPre)] public int LeviesByEndOfPreHardmode;
		[Range(4, 200)] [DefaultValue(LevyConstants.DefaultLeviesHm)] public int LeviesByEndOfHardmode;
		[Range(4, 200)] [DefaultValue(LevyConstants.DefaultLeviesPost)] public int LeviesByEndOfPostMoonLord;
		[Range(4, 200)] [DefaultValue(60)] public int MaxLevies;
		[DefaultValue(true)] public bool CapLeviesByStage;
		[Range(1, 40)] [DefaultValue(10)] public int MilitiaFieldCap;
		/// <summary>Manpower each housed town NPC adds to the pool. The Conscription book raises it in the same proportion (15 for 9).</summary>
		[Range(1, 100)] [DefaultValue(LevyConstants.ManpowerPerNpc)] public int ManpowerPerTownNpc;
		/// <summary>Ceiling of the manpower pool shared by every Barracks on the pylon network. 0 means no ceiling (the townsfolk count alone sets it).</summary>
		[Range(0, 2000)] [DefaultValue(LevyConstants.ManpowerNetworkCap)] public int ManpowerCap;
		[Range(5, 100)] [DefaultValue(40)] public int RenderCap;
		/// <summary>Boss milestones. The boss is a vanilla boss id (KingSlime, EyeOfCthulhu, EaterOrBrain, QueenBee, Skeletron, Deerclops, WallOfFlesh, QueenSlime, Destroyer, Twins,
		/// SkeletronPrime, Plantera, Golem, EmpressOfLight, DukeFishron, LunaticCultist, MoonLord) or a modded boss's exact NPC name.</summary>
		public List<MilestoneReward> MilestoneRewards = MilestoneReward.Defaults();

		// ---- Slots ------------------------------------------------------------------------------------------
		[Header("Slots")]
		[Range(0, 6)] [DefaultValue(1)] public int LevyAccessorySlots;
		[Range(0, 6)] [DefaultValue(3)] public int ManAtArmsAccessorySlots;
		[Range(0, 6)] [DefaultValue(5)] public int ChampionAccessorySlots;
		[Range(0, 6)] [DefaultValue(1)] public int ExpertiseGuideSlots;

		// ---- Armory & ranking -------------------------------------------------------------------------------
		[Header("ArmoryRanking")]
		[DefaultValue(global::Levy.Common.Weapons.WeaponRankMode.Balanced)] [DrawTicks] public global::Levy.Common.Weapons.WeaponRankMode WeaponRanking;
		[DefaultValue(false)] public bool SkipArmoredWeapons;
		[DefaultValue(false)] public bool ClassMatchIssue;
		[DefaultValue(true)] public bool ClassMatchSidearms;
		[DefaultValue(true)] public bool ClassMatchArmor;
		[DefaultValue(true)] public bool AutoUpgradeWeapons;
		[DefaultValue(true)] public bool MagicStorageSupply;
		[DefaultValue(true)] public bool MagicStorageArmoryCrafting;
		[DefaultValue(true)] public bool LogisticsReachesPylons;

		// ---- Hooks ------------------------------------------------------------------------------------------
		[Header("Hooks")]
		[DefaultValue(true)] public bool SoldiersUseHooks;
		/// <summary>Levies use the armor set abilities a key or a double-tap starts (God Slayer dash, Solar Flare dash, Forbidden storm, Vortex stealth, Calamity set keys). Policy: Common/Soldiers/SetAbilities.tsv.</summary>
		[DefaultValue(true)] public bool SoldiersUseSetAbilities;
		/// <summary>A levy in Calamity's Chalice of the Blood God bleeds out the part of a hit the Chalice deferred, as a player does (SoldierNPC.Bleedout.cs).</summary>
		[DefaultValue(true)] public bool SoldiersBleedOut = true;
		[DefaultValue(true)] public bool SoldiersTakeFallDamage;
		[DefaultValue(true)] public bool SoldiersTakeTileHazards;
		[DefaultValue(false)] public bool SoldiersCanDrown;
		[DefaultValue(true)] public bool LeviesSitWhenIdle;
		[DefaultValue(true)] public bool AmbientChatter;
		/// <summary>Levies speak lines written for Wrath of the Gods's fights (the Genesis, the Avatar, the Nameless Deity) in place of their ordinary chatter, between the boss's own moments (Common/Dialogue/WotgScenes.cs). Off: they stay silent through those fights.</summary>
		[DefaultValue(true)] public bool WotgFightChatter;
		[Range(60, 600)] [DefaultValue(200)] public int FreeReinRadius;
		[DefaultValue(true)] public bool FreeReinPastimes;

		// ---- Gear upkeep ------------------------------------------------------------------------------------
		[Header("GearUpkeep")]
		[DefaultValue(true)] public bool RealGearUpkeep;
		[DefaultValue(true)] public bool UseVanillaStatPipeline;
		[DefaultValue(true)] public bool SoldierEffectsAtSoldier;

		// ---- Soldiers ---------------------------------------------------------------------------------------
		[Header("Soldiers")]
		[Range(0f, 5f)] [DefaultValue(1f)] public float SoldierDamageMultiplier;
		[DefaultValue(TombstoneMode.Vanilla)] [DrawTicks] public TombstoneMode Tombstones;
		[DefaultValue(false)] public bool SoldiersTriggerPressurePlates;
		[DefaultValue(true)] public bool FormationDefense;

		// ---- Economy ----------------------------------------------------------------------------------------
		[Header("Economy")]
		[Range(0f, 5f)] [DefaultValue(1f)] public float WageMultiplier;
		[DefaultValue(true)] public bool AutoMusterDefault;
		[DefaultValue(true)] public bool AutoMusterBosses;
		[DefaultValue(false)] public bool WageMutiny;
		[DefaultValue(true)] public bool NurseHealsSoldiers;
		[DefaultValue(true)] public bool Spoils;

		[Header("Events")]
		[DefaultValue(true)] public bool OldOnesArmyUsesLevySentries;

		// ---- Interface --------------------------------------------------------------------------------------
		[Header("Interface")]
		[DefaultValue(true)] public bool Nameplates;
		/// <summary>A small name tag over every ally that is the army's: levies' minions and helpers, officers' creatures and units, Mechanization's machines. Shown only while nameplates are on.</summary>
		[DefaultValue(true)] public bool AllyNameTags;
		[DefaultValue(MilitiaDeathMessageMode.Count)] [DrawTicks] public MilitiaDeathMessageMode MilitiaDeathMessages;
		[DefaultValue(true)] public bool RegularDeathMessages;
		[Range(0, 100)] [DefaultValue(30)] public int CustomDeathMessagePercent;
		/// <summary>Calamity boss lines that assume a lone hero are swapped for versions that fit an army, while an army is on the field (Common/Compat/Calamity/BossLines.cs).</summary>
		[DefaultValue(true)] public bool BossLinesFitArmy;
		/// <summary>With the swap on, the boss's original line follows its army version in chat.</summary>
		[DefaultValue(false)] public bool BossLinesShowOriginal;

		// ---- Crowd ------------------------------------------------------------------------------------------
		[Header("Crowd")]
		[DefaultValue(true)] public bool ArmyEntityBudget;
		[Range(50, 400)] [DefaultValue(150)] public int ReservedProjectileSlots;
		[Range(10, 100)] [DefaultValue(40)] public int ReservedNpcSlots;
		[Range(0, 100)] [DefaultValue(30)] public int BossRoomSlots;
		[Range(0.1f, 1f)] [Increment(0.05f)] [DefaultValue(0.6f)] public float ArmySoundVolume;
	}

	/// <summary>What frightens levies during a boss fight.</summary>
	public enum FearModelSetting { Events, Proximity }

	/// <summary>One boss milestone: once <see cref="Boss"/> is beaten, every levy gains <see cref="AccessorySlots"/> accessory slots and the world gains <see cref="Levies"/> levies.</summary>
	public class MilestoneReward
	{
		[DefaultValue("")] public string Boss = "";
		[Range(0, 6)] [DefaultValue(0)] public int AccessorySlots;
		[Range(0, 100)] [DefaultValue(0)] public int Levies;
		[DefaultValue(true)] public bool Enabled = true;

		public MilestoneReward() { }
		public MilestoneReward(string boss, int slots, int levies, bool enabled) { Boss = boss; AccessorySlots = slots; Levies = levies; Enabled = enabled; }

		/// <summary>Vanilla-feeling milestones, all switched off so a default world plays exactly as before. Switching one on adds its reward on top of normal progression.</summary>
		public static List<MilestoneReward> Defaults() => new()
		{
			new("WallOfFlesh", 1, 0, false),
			new("Plantera", 0, 4, false),
			new("MoonLord", 1, 0, false),
		};

		public override bool Equals(object obj) => obj is MilestoneReward o && o.Boss == Boss && o.AccessorySlots == AccessorySlots && o.Levies == Levies && o.Enabled == Enabled;
		public override int GetHashCode() => System.HashCode.Combine(Boss, AccessorySlots, Levies, Enabled);
		public override string ToString() => global::Levy.Common.Systems.MilestoneMath.Describe(this);
	}
}
