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

		// ---------- Elf Melter (Player.ItemCheck_Shoot item 1910) ----------
		/// <summary>Vanilla spawns the Elf Melter's Flames with ai[0] = 1, which makes them fly the full 60 ticks as blue flames (a plain Flamethrower leaves ai[0] = 0 and they burn out at 50 as orange).</summary>
		public const float ElfMelterFlameAi0 = 1f;

		// ---------- Clinger Staff (Player.ItemCheck_Shoot item 3014) ----------
		/// <summary>Safety cap on the march from the muzzle toward the target (vanilla loops until the line is blocked or the cursor is reached).</summary>
		public const int ClingerMarchMaxSteps = 400;
		/// <summary>The wall's column search runs at most this many tiles up and down, and the wall is at most this many tiles tall.</summary>
		public const int ClingerScanTiles = 30, ClingerMaxWallTiles = 15;
		/// <summary>Stop marching once this close (plus the step size) to the target, in pixels.</summary>
		public const float ClingerArrivePx = 20f;

		// ---------- Spore Sac traps (ProjectileID.SporeTrap / SporeTrap2, aiStyle 105: Projectile.AI_105) ----------
		/// <summary>A trap lives until its age passes this many ticks (90 s); it then fades and stops hurting.</summary>
		public const float SporeTrapMaxAge = 5400f;
		/// <summary>Besides 1 a tick, a trap ages by its distance to the wearer divided by this (in pixels).</summary>
		public const float SporeAgeDistDivisor = 100f;
		/// <summary>The distance-age term is multiplied by each factor in turn once it exceeds its threshold, so far-off traps wither quickly.</summary>
		public static readonly (float over, float mult)[] SporeAgeFalloff =
			{ (4f, 1.1f), (5f, 1.2f), (6f, 1.3f), (7f, 1.4f), (8f, 1.5f), (9f, 1.6f), (10f, 1.7f) };
		/// <summary>Extra age per tick when no sac-wearing body backs the trap (vanilla adds this for an owner not wearing the sac): its soldier is gone.</summary>
		public const float SporeOrphanAgePerTick = 100f;
	}
}
