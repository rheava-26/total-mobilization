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
		[Range(0, 10)] [DefaultValue(2)] public int BossHpScalingPercentPerCp;
		[Range(0, 200)] [DefaultValue(80)] public int BossHpScalingCapPercent;
		[Range(0f, 3f)] [DefaultValue(1f)] public float FearMultiplier;
		[DefaultValue(FearModelSetting.Events)] [DrawTicks] public FearModelSetting FearModel;
		[DefaultValue(true)] public bool EnemiesTargetSoldiers;
		public List<int> ExtraTargetingAiStyles = new();
		/// <summary>Enemies the militia will never attack, by exact display name (case-insensitive). For boss-summoning
		/// enemies like the Perforator Cyst, whose death spawns a boss. Add any others you run into.</summary>
		public List<string> DoNotAttackNpcNames = new() { "Perforator Cyst" };

		// ---- Levy & militia counts --------------------------------------------------------------------------
		[Header("LevyCounts")]
		[Range(4, 200)] [DefaultValue(10)] public int LeviesAtStart;
		[Range(4, 200)] [DefaultValue(20)] public int LeviesByEndOfPreHardmode;
		[Range(4, 200)] [DefaultValue(40)] public int LeviesByEndOfHardmode;
		[Range(4, 200)] [DefaultValue(60)] public int LeviesByEndOfPostMoonLord;
		[Range(4, 200)] [DefaultValue(60)] public int MaxLevies;
		[Range(1, 40)] [DefaultValue(10)] public int MilitiaFieldCap;
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
		[DefaultValue(true)] public bool AutoUpgradeWeapons;
		[DefaultValue(true)] public bool MagicStorageSupply;
		[DefaultValue(true)] public bool LogisticsReachesPylons;

		// ---- Hooks ------------------------------------------------------------------------------------------
		[Header("Hooks")]
		[DefaultValue(true)] public bool SoldiersUseHooks;
		[DefaultValue(false)] public bool SoldiersTakeFallDamage;

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
		[DefaultValue(false)] public bool WageMutiny;
		[DefaultValue(true)] public bool NurseHealsSoldiers;
		[DefaultValue(true)] public bool Spoils;

		[Header("Events")]
		[DefaultValue(true)] public bool OldOnesArmyUsesLevySentries;

		// ---- Interface --------------------------------------------------------------------------------------
		[Header("Interface")]
		[DefaultValue(true)] public bool Nameplates;
		[DefaultValue(MilitiaDeathMessageMode.Count)] [DrawTicks] public MilitiaDeathMessageMode MilitiaDeathMessages;
		[DefaultValue(true)] public bool RegularDeathMessages;
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
