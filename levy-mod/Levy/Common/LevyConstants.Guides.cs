namespace Levy.Common
{
	/// <summary>
	/// Numbers for the four progression guides (Firepower, Healthcare, the Arcane Arts, Expertise), the armory buff slots Firepower adds,
	/// and the Logistical Center that grows an Armory's storage. The HP / mana ladder lives here: guides move the BASE a soldier starts
	/// from and crystals and fruit add on top of it, underneath the cap. A soldier is HARD-CAPPED at the vanilla player ceilings
	/// (<see cref="LevyConstants.MaxSoldierHp"/> HP, <see cref="LevyConstants.MaxMana"/> mana) whatever is studied: no guide cap may sit above them.
	/// </summary>
	public static partial class LevyConstants
	{
		// ---------- HP / mana ladder ----------
		/// <summary>Base HP: <see cref="BaseHp"/> normally, this once Firepower is studied (twice as much).</summary>
		public const int FirepowerBaseHp = 200;
		/// <summary>
		/// Max HP a soldier can reach with crystals / fruit: the vanilla player's 500. Healthcare's cap equals the default one, so studying it does not lift the ceiling;
		/// <see cref="LevyConstants.MaxSoldierHp"/> clamps both regardless. Firepower's higher base (<see cref="FirepowerBaseHp"/>) only gets a soldier to the cap with fewer crystals.
		/// </summary>
		public const int DefaultHpCap = 500, HealthcareHpCap = 500;
		/// <summary>Base mana: <see cref="BaseMana"/> normally, this once Firepower is studied (half the full pool).</summary>
		public const int FirepowerBaseMana = 100;
		/// <summary>
		/// Max mana a soldier can reach with mana crystals: the vanilla player's 200. The Arcane Arts' cap equals the default one, so studying it does not lift the ceiling;
		/// <see cref="LevyConstants.MaxMana"/> clamps both regardless. Firepower's higher base (<see cref="FirepowerBaseMana"/>) only gets a soldier to the cap with fewer crystals.
		/// </summary>
		public const int DefaultManaCap = 200, ArcaneManaCap = 200;
		/// <summary>Accessory slots every soldier gains from Expertise (on top of the grade's slots, up to the gear's slot count).</summary>
		public const int ExpertiseAccessorySlots = 1;

		// ---------- Armory buff slots (Firepower) ----------
		/// <summary>Buff slots Firepower adds to every Armory's issue potions row (slot 0 stays the healing / mana slot; the rest are army-wide buff slots).</summary>
		public const int FirepowerBuffSlots = 2;
		/// <summary>Sanity ceiling when reading an Armory's potion row length off the wire. Not a gameplay cap.</summary>
		public const int MaxPotionSlotsOnWire = 16;

		// ---------- Guide recipes (Crystal Ball + Bookshelf) ----------
		/// <summary>Healthcare: a Chlorophyte sink. Life Fruit and Chlorophyte Bars.</summary>
		public const int HealthcareRecipeLifeFruit = 8, HealthcareRecipeChlorophyte = 30;
		/// <summary>Arcane Arts: one stack's worth of each boss soul.</summary>
		public const int ArcaneRecipeSouls = 10;

		// ---------- Logistical Center ----------
		/// <summary>Storage slots one Logistical Center adds to the nearest Armory; centers stack.</summary>
		public const int LogisticsSlotsPerCenter = 250;
		/// <summary>A Logistical Center serves the nearest Armory within this many tiles.</summary>
		public const int LogisticsLinkRadiusTiles = 40;
		/// <summary>Server / single player: how often (ticks) every Armory's storage is reconciled with its linked centers. Clients recount for display at most this often.</summary>
		public const int LogisticsReconcileTicks = 60;
		/// <summary>Sanity ceiling when reading an Armory's storage length off the wire (400 centers). Not a gameplay cap.</summary>
		public const int LogisticsMaxStorageSlots = 100_000;
		public const int LogisticalCenterWidth = 3, LogisticalCenterHeight = 2;
		public const int LogisticalCenterRecipeChests = 4, LogisticalCenterRecipeIron = 12;
	}
}
