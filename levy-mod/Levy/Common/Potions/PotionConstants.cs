namespace Levy.Common.Potions
{
	/// <summary>Numbers for the Spectator and Seeker potions and the Guide to Autonomy.</summary>
	public static class PotionConstants
	{
		public const int SpectatorMinutes = 20, SeekerMinutes = 12;
		public const int SpectatorTicks = SpectatorMinutes * 3600, SeekerTicks = SeekerMinutes * 3600;

		/// <summary>A Spectator potion cannot be drunk this many ticks after the player last took damage (5 s).</summary>
		public const int RecentDamageTicks = 5 * 60;
		/// <summary>The Spectator is held within this many tiles of where the potion was drunk.</summary>
		public const int TetherTiles = 60;
		/// <summary>Outward speed is damped over the last this-many tiles before the edge.</summary>
		public const int TetherSoftTiles = 8;
		/// <summary>Past the edge the spectator is pushed back by excess pixels times this, at most <see cref="TetherMaxPull"/> px per tick.</summary>
		public const float TetherPullPerPx = 0.04f, TetherMaxPull = 5f;

		// Autonomy
		public const float AutonomousDamage = 0.10f, AutonomousMoveSpeed = 0.10f;
		/// <summary>Autonomous is refreshed every <see cref="AutonomousPulseTicks"/> and lasts <see cref="AutonomousDurationTicks"/>.</summary>
		public const int AutonomousPulseTicks = 30, AutonomousDurationTicks = 90;
		/// <summary>Slime Rain: an enemy drops the Guide to Autonomy at 1 in this many.</summary>
		public const int AutonomyDropDenominator = 50;
		/// <summary>True: map orders without the Command Radio only work while the Spectator buff is active. False: any time once the guide is studied.</summary>
		public const bool MapOrdersNeedSpectator = false;

		// Seeker
		/// <summary>An armory not on a Magic Storage network must be within this many tiles for the Seeker to read its list.</summary>
		public const int SeekerArmoryReachTiles = 150;
		/// <summary>Tiles scanned around the player (half-width and half-height), rescan interval in ticks, and the most matches kept (a drawing/perf bound).</summary>
		public const int SeekerScanHalfW = 70, SeekerScanHalfH = 45, SeekerScanTicks = 15, SeekerMaxMatches = 2500;
		/// <summary>Chests within this many tiles are checked for the item.</summary>
		public const int SeekerChestTiles = 90;
	}
}
