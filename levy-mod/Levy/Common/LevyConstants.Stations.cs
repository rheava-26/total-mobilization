namespace Levy.Common
{
	/// <summary>Numbers for armory-side buff stations (campfire, heart lantern, ammo box, ...). See StationBuffSystem.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Stations are looked for in a box this many tiles either side of an armory (X) / above and below it (Y).</summary>
		public const int StationScanRadiusX = 40, StationScanRadiusY = 25;
		/// <summary>A soldier gets an armory's stations while within this many tiles of that armory.</summary>
		public const int StationBenefitRadiusTiles = 40;
		/// <summary>Per-armory station cache lifetime in ticks.</summary>
		public const int StationScanIntervalTicks = 90;

		// ---- effects (vanilla values) ----
		/// <summary>Player.lifeRegen units: Campfire +1, Heart Lantern +2. Vanilla adds lifeRegen to a counter each tick and heals 1 HP per 120.</summary>
		public const int StationCampfireLifeRegen = 1, StationHeartLanternLifeRegen = 2, StationLifeRegenPerHp = 120;
		/// <summary>Honey buff: +2 life regen units, as vanilla.</summary>
		public const int StationHoneyLifeRegen = 2;
		/// <summary>Duration of the Happy! / Honey marker buffs on a soldier; refreshed with its stats every second, so it outlasts a refresh.</summary>
		public const int StationMarkerBuffTicks = 150;
		/// <summary>Scene stations are recorded this many tiles beyond vanilla's scan box around every armory and player, so a soldier that far from its source still sees exactly what a player there would.</summary>
		public const int StationSceneMarginTiles = 60;
		/// <summary>Star in a Bottle (manaRegenBonus +10): one extra mana this often while below max.</summary>
		public const int StationStarManaIntervalTicks = 30;
		/// <summary>Ammo Box: 1 in N shots is free (vanilla 20%).</summary>
		public const int StationAmmoBoxDenominator = 5;
		public const int CatBastDefense = 5;
		/// <summary>Sharpened: +12 melee armor penetration in vanilla. The soldier strike path has no armor-penetration input, so melee weapons also get the Classic-mode equivalent as flat damage.</summary>
		public const float SharpenedArmorPen = 12f;
		public const int SharpenedMeleeFlatDamage = 6;
	}
}
