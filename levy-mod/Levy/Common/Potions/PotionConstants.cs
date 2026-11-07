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
		/// <summary>A one-tick jump of at least this many tiles is a teleport; from <see cref="TeleportEndTiles"/> the Spectator buff ends, a shorter hop moves the tether anchor to the landing spot.</summary>
		public const int TeleportDetectTiles = 10, TeleportEndTiles = 100;
		/// <summary>Past the edge the spectator is pushed back by excess pixels times this, at most <see cref="TetherMaxPull"/> px per tick.</summary>
		public const float TetherPullPerPx = 0.04f, TetherMaxPull = 5f;

		// Autonomy: every tunable of the Autonomous levy behaviour lives here and in AutonomySystem.
		/// <summary>Autonomous is refreshed every <see cref="AutonomousPulseTicks"/> and lasts <see cref="AutonomousDurationTicks"/>.</summary>
		public const int AutonomousPulseTicks = 30, AutonomousDurationTicks = 90;
		/// <summary>An Autonomous levy falls back to heal below this fraction of its life and re-engages at or above <see cref="AutonomousReengageFrac"/>.</summary>
		public const float AutonomousFallbackFrac = 0.35f, AutonomousReengageFrac = 0.75f;
		/// <summary>While falling back, a levy heals this fraction of its max life per second.</summary>
		public const float AutonomousFallbackRegenPerSec = 0.03f;
		/// <summary>Targeting tiers for an Autonomous levy: bosses first, then enemies whose contact damage is at least this, then the rest.</summary>
		public const int AutonomousDangerDamage = 60;
		/// <summary>Slime Rain: an enemy drops the Guide to Autonomy at 1 in this many.</summary>
		public const int AutonomyDropDenominator = 50;

		// Seeker
		/// <summary>An armory not on a Magic Storage network must be within this many tiles for the Seeker to read its list.</summary>
		public const int SeekerArmoryReachTiles = 150;
		/// <summary>Tiles scanned around the player (half-width and half-height), rescan interval in ticks, and the most matches kept (a drawing/perf bound).</summary>
		public const int SeekerScanHalfW = 150, SeekerScanHalfH = 150, SeekerScanTicks = 15, SeekerMaxMatches = 2500;
		/// <summary>Chests within this many tiles are checked for the item.</summary>
		public const int SeekerChestTiles = 150;
	}
}
