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
		public const int RecruitmentOfficeRecipeWood = 20, RecruitmentOfficeRecipeIron = 8, RecruitmentOfficeRecipeSilk = 6;
		public const int PatrolBannerRecipeWood = 2, PatrolBannerRecipeSilk = 1;
		public const int WarHornRecipeWood = 5, WarHornRecipeIron = 1;
		public const string RecipeGroupAnyBanner = "Levy:AnyBanner";
		public const string RecipeGroupAnyChest = "Levy:AnyChest";
		public const string RecipeGroupAnyCopperBar = "Levy:AnyCopperBar";

		// ---------- Tiles ----------
		public const int BarracksWidth = 3, BarracksHeight = 3;
		public const int ArmoryWidth = 2, ArmoryHeight = 3;
		public const int RecruitmentOfficeWidth = 3, RecruitmentOfficeHeight = 2;
		/// <summary>A Recruitment Office rests this many in-game ticks after a rush drive: one in-game day (54000 of day and 32400 of night).</summary>
		public const int RecruitmentOfficeRestTicks = 54000 + 32400;
		/// <summary>The most doublings of the rush drive's price counted (price x 2^n since dawn); a longer chain is far past any treasury and would only overflow.</summary>
		public const int RushDriveMaxDoublings = 30;
		public const int PatrolBannerWidth = 1, PatrolBannerHeight = 3;
		/// <summary>Spread (pixels) of items spilled when an armory is broken.</summary>
		public const int ArmorySpillWidth = 32, ArmorySpillHeight = 48;
		/// <summary>
		/// Starting Item.timeSinceItemSpawned for gear the Armory had no room to take back. Terraria has no despawn timer for ground items; a world item
		/// is only ever lost when all 400 item slots are full and the slot with the highest timeSinceItemSpawned is recycled. A hugely negative start
		/// keeps the item last in that queue (about 4 years of ticks before it could ever count as old).
		/// </summary>
		public const int ArmorySpillProtectedAge = -1_000_000_000;
	}
}
