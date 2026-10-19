namespace Levy.Common
{
	/// <summary>Numbers for the per-item shots and projectile anchors outside the held-weapon chain (Zenith swords, Clinger Staff wall, Lunar sentries). Additive partial.</summary>
	public static partial class LevyConstants
	{
		// ---------- Zenith (Player.ItemCheck_Shoot item 4956) ----------
		/// <summary>The sword fan per swing is useAnimation / useTime shots (3 for the vanilla item), never more than this.</summary>
		public const int ZenithMaxShotsPerSwing = 4;
		/// <summary>Vanilla scatters the last sword's aim with Main.rand.NextVector2Circular(150, 150).</summary>
		public const float ZenithScatterPx = 150f;
		/// <summary>A sword's ai[0] (arc width) is rolled in [-ZenithArcRoll, +ZenithArcRoll].</summary>
		public const int ZenithArcRoll = 100;
		/// <summary>Longest body-to-target offset a sword's launch vector is built from (a player's cursor is limited to the screen; the launch velocity is half this offset).</summary>
		public const float ZenithMaxAimPx = 800f;

		// ---------- Clinger Staff (Player.ItemCheck_Shoot item 3014) ----------
		/// <summary>Safety cap on the march from the muzzle toward the target (vanilla loops until the line is blocked or the cursor is reached).</summary>
		public const int ClingerMarchMaxSteps = 400;
		/// <summary>The wall's column search runs at most this many tiles up and down, and the wall is at most this many tiles tall.</summary>
		public const int ClingerScanTiles = 30, ClingerMaxWallTiles = 15;
		/// <summary>Stop marching once this close (plus the step size) to the target, in pixels.</summary>
		public const float ClingerArrivePx = 20f;
	}
}
