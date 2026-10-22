namespace Levy.Common
{
	/// <summary>
	/// Lunar Pillar (Solar / Vortex / Nebula / Stardust) army scaling. Additive partial; read by Globals/LunarPillarScaling.cs.
	/// An army parked on a pillar clears its enemies far faster than a lone player, so the pillar fights back: every command point (CP) of soldiers near it
	/// raises the kills needed to break its shield and the rate it spawns enemies.
	/// </summary>
	public static partial class LevyConstants
	{
		/// <summary>Extra kills needed to break a pillar's shield per CP of soldiers near it (35 CP = +140 kills on top of the vanilla count).</summary>
		public const int LunarShieldKillsPerCp = 4;
		/// <summary>Spawn-rate multiplier gained per CP of soldiers near the pillar: the pillar's natural spawn rate is x (1 + this x CP), so 35 CP = x1.7.</summary>
		public const float LunarSpawnRatePerCp = 0.02f;
		/// <summary>"Near the pillar": soldiers within this many pixels of it count. Vanilla's own arena (Player.ZoneTower*) is 4000 px.</summary>
		public const float LunarArenaRadiusPx = 4000f;
		/// <summary>CP counted per pillar is clamped here, so a huge army makes the pillar a long fight (at most +800 kills, x5 spawns) and never an impossible one.</summary>
		public const int LunarCpCap = 200;
		/// <summary>The scaled spawn rate multiplier never exceeds this, and the nearby-enemy cap (maxSpawns) scales with it but stops at <see cref="LunarMaxSpawnsCap"/>.</summary>
		public const float LunarSpawnRateMultCap = 5f;
		public const int LunarMaxSpawnsCap = 24;
		/// <summary>The army CP near each pillar is re-measured this often (ticks), not every tick; the shield and spawn hooks read the cached number.</summary>
		public const int LunarCpRefreshTicks = 30;
	}
}
