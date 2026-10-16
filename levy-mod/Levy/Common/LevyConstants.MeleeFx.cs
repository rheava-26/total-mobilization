namespace Levy.Common
{
	/// <summary>Numbers for the vanilla melee on-hit specials and sword-shot ports (Common/Weapons/MeleeEffects.cs, WeaponUse.DefaultShoot). Values read from Player.ItemCheck / ApplyNPCOnHitEffects.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Volcano blast, Muramasa cut and Bladetongue ichor use this fraction of the hit's source damage.</summary>
		public const float VolcanoBlastDamageFactor = 0.75f, MuramasaCutDamageFactor = 0.5f, BladetongueIchorDamageFactor = 0.5f, BladetongueIchorKnockFactor = 0.7f;
		/// <summary>Bee Keeper bees deal a third of the hit's source damage.</summary>
		public const int BeeKeeperDamageDivisor = 3;
		/// <summary>Tentacle Spike / Blood Butcherer barbs allowed on one enemy at a time.</summary>
		public const int JavelinBarbsPerTarget = 5;
		/// <summary>Breaker Blade bonus against enemies above 90% health; Keybrand bonus scales up to +100% against enemies near death.</summary>
		public const float BreakerBladeBonus = 2.5f;
		public const float BreakerBladeHealthFraction = 0.9f;

		/// <summary>Starfury: stars fall from this far above the target and hit this much harder.</summary>
		public const float StarfuryDropHeightPx = 600f;
		public const float StarfuryDamageFactor = 1.5f;
		public const int StarfuryDropSpreadPx = 200;
		/// <summary>Light's Bane slash: damage fraction (doubled first on a crit) and the pixel range in which it picks the target.</summary>
		public const float LightsBaneDamageFactor = 0.5f;
		/// <summary>Blade of Grass vine: damage fraction, and the radius in which a target is homed on.</summary>
		public const float BladeOfGrassDamageFactor = 0.25f;
		public const float BladeOfGrassTargetRadiusPx = 150f;
		/// <summary>Hellwing Bow bats curve toward the aim vector; this scatters it like vanilla (fractions of the shot speed).</summary>
		public const float HellwingAimScatter = 0.15f;
	}
}
