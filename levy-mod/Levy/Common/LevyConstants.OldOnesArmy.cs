namespace Levy.Common
{
	/// <summary>Old One's Army levy sentries and the Tavernkeep's drinks. Additive to LevyConstants.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Levies a sentry of tier 1 / 2 / 3 (Rod / Cane / Staff) calls up.</summary>
		public static readonly int[] OoaLeviesPerTier = { 1, 2, 3 };
		/// <summary>Etherian Mana (the Old One's Army crystal item) one sentry of each tier costs: 5 per levy for the Rod, a little less per levy above it.</summary>
		public static readonly int[] OoaEtherianManaPerTier = { 5, 8, 12 };
		/// <summary>Defender Medals the Tavernkeep asks for each staff.</summary>
		public static readonly int[] OoaMedalPricePerTier = { 10, 25, 50 };
		/// <summary>How far from the cursor (tiles) a sentry may be planted from the player, and how far below the cursor it looks for ground.</summary>
		public const int OoaPlaceRangeTiles = 40, OoaGroundScanTiles = 30;
		/// <summary>Ticks between a sentry topping its levies back up.</summary>
		public const int OoaMaintainTicks = 30;

		// ---- Tavernkeep drinks ----
		public const int DrinkRadiusTiles = 60;
		/// <summary>Five minutes.</summary>
		public const int DrinkDurationTicks = 60 * 60 * 5;
		public const int AleDefense = 8;
		public const float StoutDamage = 0.15f;
		public const float RumAttackSpeed = 0.2f;
		/// <summary>Tipsy: a drunk levy's aim error is multiplied by this (all three drinks).</summary>
		public const float DrunkAimErrorMult = 1.6f;
	}
}
