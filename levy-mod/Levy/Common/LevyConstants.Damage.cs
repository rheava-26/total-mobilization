namespace Levy.Common
{
	/// <summary>Numbers owned by the Damage module (SoldierNPC.Damage.cs). Ticks unless the name says otherwise.</summary>
	public static partial class LevyConstants
	{
		/// <summary>
		/// Projectile.Damage hurts a player for Main.DamageVar(damage) * 2 from any hostile projectile (every mode) but hits a
		/// friendly NPC at x1; soldiers apply this to SourceDamage so they die like players.
		/// </summary>
		public const float HostileProjectileDamageMult = 2f;

		// ---------- regen ----------
		public const int RegenHpPerInterval = 1;
		/// <summary>Regen per interval while stood down and within <see cref="RegenHomeBonusTiles"/> of the home barracks.</summary>
		public const int RegenHpPerIntervalAtHome = 2;
		public const int RegenHomeBonusTiles = 10;
		/// <summary>TicksSinceDamaged stops counting here so it never overflows.</summary>
		public const int TicksSinceDamagedCap = 1_000_000;

		// ---------- potions ----------
		/// <summary>A buff potion is drunk at most this often, and only while engaged.</summary>
		public const int BuffPotionIntervalTicks = 60 * 60 * 5;
		/// <summary>After an armory lookup finds nothing to drink, wait this long before looking again.</summary>
		public const int PotionRetryTicks = 60;

		// ---------- contact damage ----------
		/// <summary>
		/// Broad-phase margin (pixels) around the summed half-sizes before NPC.GetMeleeCollisionData is consulted; the widest
		/// special melee rectangle vanilla builds there is 100 px (Utils.CenteredRectangle(center, 100)).
		/// </summary>
		public const int ContactBroadPhasePx = 120;

		// ---------- lava ----------
		/// <summary>NPC.Collision_LavaCollision writes immune[255] = 30 immediately before its 50-damage strike; seen inside HitEffect it identifies a lava hit.</summary>
		public const int LavaStrikeImmuneTicks = 30;

		// ---------- hit effects ----------
		public const int HitDustCount = 4;
		public const int DeathDustCount = 20;
	}
}
