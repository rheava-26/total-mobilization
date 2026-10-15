namespace Levy.Common
{
	/// <summary>Fear module numbers that are not part of the nested <c>LevyConstants.Fear</c> table. Tiles unless the name says ticks.</summary>
	public static partial class LevyConstants
	{
		/// <summary>COMRADE_DOWN is barked by the nearest living soldiers within this many tiles of the fallen one.</summary>
		public const int FearComradeBarkTiles = 30;
		/// <summary>How many soldiers bark COMRADE_DOWN for one death (1 or 2).</summary>
		public const int FearComradeBarkCount = 2;
		/// <summary>How many soldiers bark ROUTED when the commander dies mid-fight.</summary>
		public const int FearRoutBarkCount = 3;
		/// <summary>SHAKEN barks name the most recently fallen comrade if the death was this recent.</summary>
		public const int FearFallenRecallTicks = 60 * 30;
		/// <summary>Fear changes of this size trigger a network refresh of the body so clients see the meter.</summary>
		public const float FearNetStep = 10f;

		// ---------- Fighting retreat (Routed) ----------
		/// <summary>Hostiles within this many tiles (hitbox distance) count as threats: ranged standoff checks them, a routed soldier runs from them. With none about, a routed soldier walks to its anchor.</summary>
		public const int ThreatScanTiles = 60;
		/// <summary>Retreat speed multiplier on top of the run speed.</summary>
		public const float RoutedSpeedMult = 1.15f;
		/// <summary>How far ahead of itself a retreating soldier sets its goal (tiles).</summary>
		public const int RoutedRetreatTiles = 30;
		/// <summary>Within this of its anchor (home muster point / commander) a retreating soldier stops running.</summary>
		public const float RoutedAnchorArriveTiles = 3f;
	}
}
