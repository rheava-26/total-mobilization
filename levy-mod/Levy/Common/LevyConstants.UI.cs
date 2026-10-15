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
			public const int ArmoryPanelWidth = 664, ArmoryPanelHeight = 404;
			public const int PanelDefaultLeft = 20, PanelDefaultTop = 300;
			public const int RosterRefreshTicks = 30;
			public const int RosterRowHeight = 100;
			public const float ButtonTextScale = 0.75f, SmallTextScale = 0.8f;
			public const long DepositSmall = 10000, DepositLarge = 100000; // 1 gold, 10 gold
			/// <summary>First and one-past-last main-inventory slot (hotbar, coins and ammo excluded).</summary>
			public const int InventoryMainStart = 10, InventoryMainEnd = 50;
			public const int InventoryMouseSlot = 58;

			// ---- armory grid ----
			public const int ArmoryColumns = 12, ArmoryRows = 10;
			public const float SlotScale = 0.6f;
			public const int SlotPitch = 34; // 52 * 0.6 + gap
			/// <summary>Server-side sanity range for armory slot edits (tiles from the armory).</summary>
			public const int ArmoryEditRangeTiles = 32;

			// ---- keybinds / radial ----
			public const int MobilizeKeyRadiusTiles = 120;
			public const int PatrolBannerSearchTiles = 60;
			public const int RadialRadius = 112;
			public const int RadialButtonWidth = 150, RadialButtonHeight = 34;
			/// <summary>Ticks after opening during which right-click / Escape do not close the radial (the opening click itself).</summary>
			public const int RadialInputGraceTicks = 6;

			// ---- nameplates ----
			public const int NameplateOffsetY = 28;
			public const int NameplateHpBarWidth = 30, NameplateHpBarHeight = 4;
			public const float NameplateTextScale = 0.8f;
			/// <summary>Weapon line scale relative to the name text scale.</summary>
			public const float NameplateWeaponScale = 0.8f;
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
