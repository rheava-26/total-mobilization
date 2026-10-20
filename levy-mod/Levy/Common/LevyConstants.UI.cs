using Microsoft.Xna.Framework;

namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Interface numbers (panels, radial, nameplates, HUD strip, minimap). Owned by the UI agent.</summary>
		public static class UI
		{
			// ---- panels ----
			public const int PanelCloseDistanceTiles = 10;
			public const int BarracksPanelWidth = 560, BarracksPanelHeight = 520;
			public const int ArmoryPanelWidth = 692, ArmoryPanelHeight = 424;
			public const int PanelDefaultLeft = 20, PanelDefaultTop = 300;
			public const int RosterRefreshTicks = 30;

			// ---- barracks roster (resizable panel + card grid) ----
			/// <summary>Where the roster grid starts below the panel top (the header of treasury / enlist / orders controls above it is fixed-height).</summary>
			public const int BarracksListTop = 234;
			/// <summary>Smallest the barracks panel can be dragged to: wide enough for the header controls, tall enough for one row of cards.</summary>
			public const int BarracksMinWidth = 560, BarracksMinHeight = 380;
			/// <summary>Margin kept to the screen edge when the resize grip is dragged out.</summary>
			public const int BarracksScreenMargin = 4;
			public const int ResizeGripSize = 18;
			/// <summary>Roster cards: the grid fits as many columns of at least CardMinWidth as the panel is wide (cards then stretch to fill the row).</summary>
			public const int RosterCardMinWidth = 220, RosterCardHeight = 132, RosterCardGap = 6;
			/// <summary>Gear icon strip on a card: icon box size and the gap between boxes.</summary>
			/// <summary>Width of a left column on each card reserved for the soldier's live sprite (0 until the portrait follow-up lands; the layout already honours it).</summary>
			public static readonly int CardPortraitWidth = 0;
			public const int CardGearIcon = 24, CardGearGap = 2;
			public const float CardGearInventoryScale = 0.46f; // 52 * 0.46 ~ CardGearIcon
			/// <summary>Card tint strength toward the grade colour (background), and the grade colours (border + tint). Better grade, warmer colour.</summary>
			public const float CardGradeTint = 0.22f;
			public static readonly Color GradeColorLevy = new(150, 165, 190), GradeColorManAtArms = new(90, 165, 240), GradeColorChampion = new(240, 190, 60);
			public const float ButtonTextScale = 0.75f, SmallTextScale = 0.8f;
			public const long DepositSmall = 10000, DepositLarge = 100000; // 1 gold, 10 gold
			/// <summary>First and one-past-last main-inventory slot (hotbar, coins and ammo excluded).</summary>
			public const int InventoryMainStart = 10, InventoryMainEnd = 50;
			public const int InventoryMouseSlot = 58;

			// ---- armory grid ----
			/// <summary>Storage grid: 12 columns, and ArmoryRows rows visible at once; the full ArmoryTE.StorageSize slots scroll inside it.</summary>
			public const int ArmoryColumns = 12, ArmoryRows = 10;
			/// <summary>Gap between the storage grid and its scrollbar, and the scrollbar's width.</summary>
			public const int ArmoryScrollGap = 6, ArmoryScrollWidth = 20;
			public const float SlotScale = 0.6f;
			public const int SlotPitch = 34; // 52 * 0.6 + gap
			/// <summary>Server-side sanity range for armory slot edits (tiles from the armory).</summary>
			public const int ArmoryEditRangeTiles = 32;

			// ---- bulk reforge (armory) ----
			/// <summary>Reforge floor presets, best-of-N: a weapon qualifies once its prefix is among the top this-fraction of the prefixes THAT weapon can roll (0 = only the single best). Index = the floor button's cycle position.</summary>
			public static readonly float[] ReforgeFloorTopFraction = { 0.50f, 0.25f, 0.10f, 0f };
			/// <summary>Localization keys (Mods.Levy.UI2.*) naming each floor preset.</summary>
			public static readonly string[] ReforgeFloorNameKey = { "ReforgeFloorGood", "ReforgeFloorGreat", "ReforgeFloorSuperb", "ReforgeFloorBest" };
			/// <summary>Hard cap of paid rolls per weapon per bulk reforge (a weapon that has not reached the floor by then keeps its best roll seen).</summary>
			public const int ReforgeMaxRollsPerWeapon = 40;
			/// <summary>Throw-away rolls used (once per weapon type, cached) to learn which prefixes that weapon can take and how good each is.</summary>
			public const int ReforgeSampleRolls = 600;
			/// <summary>Layout of the armory's bottom bar (reforge controls).</summary>
			public const int ArmoryBarTop = 388, ArmoryBarHeight = 26;

			// ---- keybinds / radial ----
			public const int MobilizeKeyRadiusTiles = 120;
			public const int PatrolBannerSearchTiles = 60;
			public const int RadialRadius = 112;
			/// <summary>Horizontal radius the wheel starts from before it is grown to fit its button count.</summary>
			public const int RadialRadiusX = 180;
			/// <summary>Minimum empty gap kept between any two radial buttons (px).</summary>
			public const int RadialGapPx = 4;
			public const int RadialButtonWidth = 150, RadialButtonHeight = 34;
			/// <summary>Ticks after opening during which right-click / Escape do not close the radial (the opening click itself).</summary>
			public const int RadialInputGraceTicks = 6;

			// ---- nameplates ----
			public const int NameplateOffsetY = 28;
			public const int NameplateHpBarWidth = 30, NameplateHpBarHeight = 4;
			public const float NameplateTextScale = 0.8f;
			/// <summary>Weapon line scale relative to the name text scale.</summary>
			public const float NameplateWeaponScale = 0.8f;
			/// <summary>Potion-sickness pip: size of the box, gap below the weapon line, and the text scale relative to the name text.</summary>
			public const int PotionPipWidth = 22, PotionPipHeight = 9, PotionPipGap = 3;
			public const float PotionPipTextScale = 0.7f;
			public const float FearIconMinScale = 0.6f, FearIconMaxScale = 1.3f;

			// ---- HUD strip ----
			public const int HudHeadSize = 36, HudEntryPitch = 44, HudBarHeight = 5;
			public const float HudHeadScale = 0.8f;
			public const int HudLeft = 20, HudBottomMargin = 46;

			// ---- minimap ----
			public const int MapDotSize = 6;
		}
	}
}
