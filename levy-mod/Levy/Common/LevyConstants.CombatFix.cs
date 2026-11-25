namespace Levy.Common
{
	/// <summary>Numbers of the combatfix pass: shots into a wall (SoldierNPC.ShotWaste.cs), the Supreme Calamitas bullet hell on the arena (ScalBulletHell.cs).</summary>
	public static partial class LevyConstants
	{
		// ---------- shots into a wall ----------
		/// <summary>A shot that dies on a tile this near its levy's target (tiles) counts as spent on the wall.</summary>
		public const float WasteNearTiles = 10f;
		/// <summary>This many such shots inside <see cref="WasteWindowTicks"/> open a strict window of <see cref="WasteStrictTicks"/>, in which line checks use a ray <see cref="WasteRayPx"/> wide.</summary>
		public const int WasteLimit = 4, WasteWindowTicks = 240, WasteStrictTicks = 600;
		public const float WasteRayPx = 6f;

		// ---------- Supreme Calamitas's bullet hell on the arena ----------
		/// <summary>Distance (px) beyond the arena's wall at which a bullet-hell shot is born, and the vanilla half-range the offsets are read against.</summary>
		public const float ScalBhMarginPx = 96f, ScalBhVanillaHalfPx = 1000f;
	}
}
