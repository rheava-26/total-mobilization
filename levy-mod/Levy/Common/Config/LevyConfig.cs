using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Config
{
	/// <summary>Server-side config. Everything has a sane default; the config exists for tuning, not for playing.</summary>
	public class LevyConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static LevyConfig Get => ModContent.GetInstance<LevyConfig>();

		[Header("Bosses")]
		[Range(0, 10)] [DefaultValue(2)] public int BossHpScalingPercentPerCp;
		[Range(0, 200)] [DefaultValue(80)] public int BossHpScalingCapPercent;
		[DefaultValue(true)] public bool BossesPunishSoldiers;

		[Header("Soldiers")]
		[Range(0f, 3f)] [DefaultValue(1f)] public float FearMultiplier;
		[Range(0f, 5f)] [DefaultValue(1f)] public float SoldierDamageMultiplier;
		[Range(5, 100)] [DefaultValue(40)] public int RenderCap;
		[DefaultValue(TombstoneMode.Vanilla)] [DrawTicks] public TombstoneMode Tombstones;
		[DefaultValue(true)] public bool EnemiesTargetSoldiers;
		[DefaultValue(true)] public bool UseVanillaStatPipeline;
		[DefaultValue(true)] public bool ClassMatchIssue;
		[DefaultValue(true)] public bool AutoUpgradeWeapons;
		[DefaultValue(true)] public bool RealWeaponShoot;
		[DefaultValue(true)] public bool AutoFeedCrystals;
		[DefaultValue(false)] public bool SoldiersTriggerPressurePlates;
		public List<int> ExtraTargetingAiStyles = new();

		[Header("Economy")]
		[Range(0f, 5f)] [DefaultValue(1f)] public float WageMultiplier;
		[DefaultValue(true)] public bool AutoMusterDefault;
		[Range(1, 40)] [DefaultValue(10)] public int MilitiaFieldCap;
		[DefaultValue(true)] public bool NurseHealsSoldiers;
		[DefaultValue(true)] public bool Spoils;
		[DefaultValue(true)] public bool MagicStorageSupply;

		[Header("Interface")]
		[DefaultValue(true)] public bool Nameplates;
		[DefaultValue(MilitiaDeathMessageMode.Count)] [DrawTicks] public MilitiaDeathMessageMode MilitiaDeathMessages;
		[DefaultValue(true)] public bool RegularDeathMessages;
	}
}
