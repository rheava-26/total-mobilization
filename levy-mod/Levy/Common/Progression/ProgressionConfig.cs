using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Progression
{
	/// <summary>
	/// PROGRESSION ASSISTANCE. Every option defaults to the vanilla behaviour (toggle off, multiplier 1, percent 100).
	/// The mod is designed around vanilla progression; these only ease hard-to-get items so a levy army can be equipped in bulk.
	/// Options that change recipes, loot tables or item sets are [ReloadRequired] (they are applied while the mod loads).
	/// </summary>
	public class LevyProgressionConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;
		public static LevyProgressionConfig Get => ModContent.GetInstance<LevyProgressionConfig>();

		// ---- Recipes -------------------------------------------------------------------------------------------
		[Header("Note")]
		[ReloadRequired] [DefaultValue(false)] public bool LifeCrystalRecipe;
		[ReloadRequired] [DefaultValue(false)] public bool LifeFruitRecipe;
		[ReloadRequired] [DefaultValue(false)] public bool FallenStarRecipe;
		[ReloadRequired] [DefaultValue(false)] public bool GemsToManaCrystalRecipe;
		[ReloadRequired] [Range(1, 5)] [DefaultValue(5)] public int ChlorophyteOrePerBar;
		[ReloadRequired] [DefaultValue(false)] public bool ChlorophyteOreRecipe;
		[ReloadRequired] [Range(1, 10)] [DefaultValue(1)] public int AmmoRecipeYieldMultiplier;

		// ---- Drop rates ----------------------------------------------------------------------------------------
		[Header("DropRates")]
		[ReloadRequired] [Range(1f, 20f)] [Increment(0.5f)] [DefaultValue(1f)] public float EctoplasmDropMultiplier;
		[ReloadRequired] [Range(1f, 20f)] [Increment(0.5f)] [DefaultValue(1f)] public float SoulDropMultiplier;
		[ReloadRequired] [Range(1f, 20f)] [Increment(0.5f)] [DefaultValue(1f)] public float MobDropMultiplier;

		// ---- Chlorophyte ---------------------------------------------------------------------------------------
		[Header("Chlorophyte")]
		[Range(1, 10)] [DefaultValue(1)] public int ChlorophyteGrowthMultiplier;
		[Range(1f, 10f)] [Increment(0.5f)] [DefaultValue(1f)] public float ChlorophyteChunkScale;

		// ---- Boss weapons / shimmer / crates -------------------------------------------------------------------
		[Header("Items")]
		[ReloadRequired] [Range(0, 100)] [DefaultValue(0)] public int OnceOnlyBossWeaponRepeatPercent;
		[ReloadRequired] [DefaultValue(false)] public bool ShimmerDesertShards;
		[ReloadRequired] [Range(1, 10)] [DefaultValue(1)] public int EarlyHardmodeCrateOreMultiplier;

		// ---- Armor costs ---------------------------------------------------------------------------------------
		[Header("ArmorCosts")]
		[ReloadRequired] [Range(10, 100)] [DefaultValue(100)] public int ChlorophyteArmorCostPercent;
		[ReloadRequired] [Range(10, 100)] [DefaultValue(100)] public int HallowedArmorCostPercent;
		[ReloadRequired] [Range(10, 100)] [DefaultValue(100)] public int SpiderArmorCostPercent;
		[ReloadRequired] [Range(10, 100)] [DefaultValue(100)] public int LevyArmorCostPercent;

		// ---- Arms Dealer ---------------------------------------------------------------------------------------
		[Header("ArmsDealer")]
		[DefaultValue(false)] public bool ArmsDealerBulkAmmo;
		[Range(50, 9999)] [DefaultValue(ProgConst.BulkAmmoUnlockDefault)] public int BulkAmmoUnlockAmount;
		[Range(50, 400)] [DefaultValue(ProgConst.BulkAmmoPriceDefault)] public int BulkAmmoPricePercent;
	}
}
