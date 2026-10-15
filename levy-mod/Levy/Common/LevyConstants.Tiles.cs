namespace Levy.Common
{
	/// <summary>Numbers owned by the tiles / items module (recipes, War Horn reach, sync).</summary>
	public static partial class LevyConstants
	{
		// ---------- War Horn ----------
		/// <summary>The horn reaches the nearest barracks within this many tiles.</summary>
		public const int HornRangeTiles = 120;
		/// <summary>"No barracks nearby" chat hint at most once per this many ticks.</summary>
		public const int HornHintCooldownTicks = 60 * 5;

		// ---------- Recipes (Work Bench) ----------
		public const int BarracksRecipeWood = 30, BarracksRecipeIron = 10, BarracksRecipeBanners = 1;
		public const int ArmoryRecipeIron = 20, ArmoryRecipeChests = 1;
		public const int PatrolBannerRecipeWood = 2, PatrolBannerRecipeSilk = 1;
		public const int WarHornRecipeWood = 5, WarHornRecipeIron = 1;
		public const string RecipeGroupAnyBanner = "Levy:AnyBanner";
		public const string RecipeGroupAnyChest = "Levy:AnyChest";

		// ---------- Tiles ----------
		public const int BarracksWidth = 3, BarracksHeight = 3;
		public const int ArmoryWidth = 2, ArmoryHeight = 3;
		public const int PatrolBannerWidth = 1, PatrolBannerHeight = 3;
		/// <summary>Spread (pixels) of items spilled when an armory is broken.</summary>
		public const int ArmorySpillWidth = 32, ArmorySpillHeight = 48;
	}
}
