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
			public const int ArmoryPanelWidth = 692, ArmoryPanelHeight = 452;
			public const int PanelDefaultLeft = 20, PanelDefaultTop = 300;
			public const int RosterRefreshTicks = 30;

			// ---- barracks roster (resizable panel + card grid) ----
			/// <summary>Header layout: rows of controls start at BarracksHeaderTop and step down by BarracksRowPitch (button height + a 2 px gap); BarracksHeaderRows rows
			/// (deposit, manpower + command-point bars, toggles, recruit, enlist, filters, roster+orders). The roster grid starts right under the last row.</summary>
			public const int BarracksHeaderTop = 32, BarracksRowPitch = 26, BarracksButtonH = 24, BarracksHeaderRows = 7, BarracksListGap = 2;
			public const int BarracksListTop = BarracksHeaderTop + BarracksHeaderRows * BarracksRowPitch + BarracksListGap;
			/// <summary>Gap between the filter/sort buttons, and between the manpower bar and the command-point bar; and the height of those two header bars.</summary>
			public const int BarracksFilterGap = 4, BarracksBarGap = 10, BarracksBarH = 22;
			/// <summary>Width of the right-anchored "Reset loadouts" button on the recruit row.</summary>
			public const int ResetLoadoutsButtonW = 130;
			/// <summary>Mouse-wheel scroll of the roster: the raw wheel delta (120 per notch) times this, so a notch moves about half a card.</summary>
			public const float RosterScrollWheelScale = 0.45f;

			// ---- hearts / stars on the roster cards (a soldier's HP = hearts, mana = stars) ----
			/// <summary>Default row height, icon draw size and icon-to-icon pitch (px). The icon count is whatever fits the row; the value per icon is picked from IconValueSteps.</summary>
			public const int IconRowHeight = 22, StatIconSize = 18, StatIconPitch = 18;
			public const float IconLabelScale = 0.7f, IconPartialMinScale = 0.55f;
			public const int IconLabelGap = 6;
			/// <summary>The card's compact rows: height (two stacked rows share the strip above the gear), icon size and pitch, label scale and its vertical nudge, and the dim of an empty slot on a fallen soldier.</summary>
			public const int CardIconRowH = 15, CardIconSize = 13, CardIconPitch = 13, CardIconLabelDy = 1;
			public const float CardIconLabelScale = 0.58f, CardIconDeadDim = 0.55f;
			/// <summary>Tint of an empty heart / star slot.</summary>
			public static readonly Color IconEmptyColor = new Color(90, 90, 100) * 0.5f;
			/// <summary>Nice per-icon values; the smallest one whose icon count fits the row is used.</summary>
			public static readonly int[] IconValueSteps = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 75, 100, 150, 200, 250, 500, 1000 };

			// ---- roster filter / sort ----
			/// <summary>Weapon tier bands over WeaponClassifier.RarityTier (white 0 .. 12): the lowest tier of each band (Early, Mid, Late, Endgame). A band runs up to the next band's start.</summary>
			public static readonly int[] RosterTierBandMin = { 0, 3, 6, 9 };
			/// <summary>Highlight of a filter/sort button that is currently narrowing or reordering the roster.</summary>
			public static readonly Color ButtonLit = new Color(70, 140, 110) * 0.85f;

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
			/// <summary>Top of the storage grid and the issue rows (under the header and the filter row).</summary>
			public const int ArmoryGridTop = 68;
			public const float SlotScale = 0.6f;
			public const int SlotPitch = 34; // 52 * 0.6 + gap
			/// <summary>Server-side sanity range for armory slot edits (tiles from the armory).</summary>
			public const int ArmoryEditRangeTiles = 32;

			// ---- armory search + filter ----
			/// <summary>Search box in the header row: left edge, gap to the Sort button, height, and the left/right text inset inside it.</summary>
			public const int ArmorySearchLeft = 84, ArmorySearchGap = 8, ArmorySearchHeight = 26, ArmorySearchInset = 8;
			/// <summary>Filter row under the header: its top and height (rarity swatches, category button, clear button, shown count).</summary>
			public const int ArmoryFilterTop = 36, ArmoryFilterHeight = 24;
			/// <summary>Rarity swatch: square size and gap between swatches.</summary>
			public const int RaritySwatchSize = 18, RaritySwatchGap = 2;
			/// <summary>Gap between the swatch strip and the category button, and between the filter-row buttons.</summary>
			public const int ArmoryFilterGap = 8;
			public const int ArmoryCategoryButtonWidth = 130, ArmoryClearButtonWidth = 60;
			/// <summary>How often (ticks) a filtered storage view re-checks the items, so a taken or swapped item drops out of it.</summary>
			public const int ArmoryFilterRefreshTicks = 8;
			/// <summary>Rarity values shown as swatches, in order: gray, white, blue, green, orange, red, pink, light purple, lime, yellow, cyan, dark red, purple, then Quest, Expert, Master. Any other rarity (modded) shares one extra swatch after these.</summary>
			public static readonly int[] ArmoryRarityOrder = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, -11, -12, -13 };
			/// <summary>Localization keys (Mods.Levy.UI2.*) naming each swatch; the last one is the modded / other swatch.</summary>
			public static readonly string[] ArmoryRarityNameKey =
			{
				"RarityGray", "RarityWhite", "RarityBlue", "RarityGreen", "RarityOrange", "RarityLightRed", "RarityPink", "RarityLightPurple",
				"RarityLime", "RarityYellow", "RarityCyan", "RarityRed", "RarityPurple", "RarityQuest", "RarityExpert", "RarityMaster", "RarityOther",
			};
			/// <summary>Master-rarity swatch colour: green channel scales with Main.masterColor (0..1) by this much.</summary>
			public const float MasterRarityGreenScale = 200f;
			/// <summary>Colour of the extra swatch that stands for every modded / unlisted rarity.</summary>
			public static readonly Color ArmoryRarityOtherColor = new Color(110, 190, 180);

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
			public const int ArmoryBarTop = 416, ArmoryBarHeight = 26;

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
