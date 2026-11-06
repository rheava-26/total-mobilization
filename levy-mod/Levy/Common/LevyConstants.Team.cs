namespace Levy.Common
{
	/// <summary>Numbers for the team items (ally auras, shields and shared heals) that reach levies. Additive partial; every figure is the vanilla / mod number it mirrors.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Ticks between two team-item pulses (vanilla's team accessory code runs on miscCounter % 10).</summary>
		public const int TeamPulseTicks = 10;
		/// <summary>Ticks a worn-item team buff lasts after a pulse (vanilla Paladin's Shield, Trinket of Chi and Affliction all add their buff for 20 ticks).</summary>
		public const int TeamBuffTicks = 20;
		/// <summary>Reach of the Paladin's Shield aura (Player.cs: distance under 800).</summary>
		public const float TeamPaladinRangePx = 800f;
		/// <summary>Spectre Hood heal reach: the most wounded ally within this far of the hit (Projectile.ghostHeal: 3000).</summary>
		public const float TeamSpectreRangePx = 3000f;
		/// <summary>Calamity's rebalanced Spectre heal: the lifesteal pool is charged this fraction of the heal.</summary>
		public const float TeamSpectreCalamityCost = 0.66f;
		/// <summary>Nebula booster sharing: allies this close to the pickup get the same booster (MessageBuffer packet 102: 700).</summary>
		public const float TeamNebulaRangePx = 700f;
		/// <summary>Info accessories are shared between allies this close (Player.RefreshInfoAccsFromTeamPlayers: 800).</summary>
		public const float TeamInfoRangePx = 800f;
	}
}
