namespace Levy.Common
{
	/// <summary>
	/// Numbers for the four progression guides (Firepower, Healthcare, the Arcane Arts, Expertise), the armory buff slots Firepower adds,
	/// and the Logistical Center that grows an Armory's storage. The guides buff the MILITIA, never the regulars: a regular mirrors the vanilla player
	/// (see LevyConstants.cs: crystals and fruit, hard-capped at <see cref="LevyConstants.MaxSoldierHp"/> HP and <see cref="LevyConstants.MaxMana"/> mana) and no guide changes him,
	/// while a militiaman takes no crystals, so the flat bonuses below are his whole HP / mana ladder.
	/// </summary>
	public static partial class LevyConstants
	{
		// ---------- Militia HP / mana (the guides are their whole ladder) ----------
		// A militiaman is mustered with BaseHp / BaseMana plus the bonus of every guide studied in the world, then held under MaxSoldierHp / MaxMana (a hard cap, never exceeded).
		// At these defaults, with all three studied, a militiaman has 350 HP / 160 mana: clearly tougher than a bare conscript (100 / 20) and a notch under a maxed regular (500 / 200).
		// TUNING: these four numbers are the only knobs. Bonuses add up independently of each other and of the order the guides are studied in.
		/// <summary>Guide to Firepower: militia are mustered with this much more HP and mana (200 HP / 100 mana on their own).</summary>
		public const int FirepowerMilitiaHp = 100, FirepowerMilitiaMana = 80;
		/// <summary>Guide to Healthcare: militia are mustered with this much more HP.</summary>
		public const int HealthcareMilitiaHp = 150;
		/// <summary>Guide to the Arcane Arts: militia are mustered with this much more mana.</summary>
		public const int ArcaneMilitiaMana = 60;
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
