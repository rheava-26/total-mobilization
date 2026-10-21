namespace Levy.Common
{
	/// <summary>
	/// Numbers for the "rains from the sky onto the cursor" staves (Blizzard Staff, Meteor Staff, Lunar Flare). Read from the matching branches of Player.ItemCheck_Shoot:
	/// every stack-shot starts 600 px above the caster at a point halfway between a spread around the aim point and the caster, then flies down at the aim point.
	/// </summary>
	public static partial class LevyConstants
	{
		/// <summary>Spawn height above the caster, horizontal spread around the aim point (rolled 0..N toward the caster's back), and the extra +-N scatter after averaging with the caster's X.</summary>
		public const float SkyfallDropHeightPx = 600f;
		public const int SkyfallAimSpreadPx = 200, SkyfallScatterPx = 200;
		/// <summary>Each extra projectile of one cast starts this much higher than the last; a shot always flies downward and never flatter than this vertical component.</summary>
		public const float SkyfallStackStepPx = 100f, SkyfallMinFallPx = 20f;

		/// <summary>Blizzard Staff: two hail shards per cast (projectile 337), +-0.8 px/tick scatter on both axes, ai[1] = a random frame 0..4.</summary>
		public const int BlizzardShardCount = 2, BlizzardFrameCount = 5;
		public const float BlizzardVelScatter = 0.8f;

		/// <summary>Meteor Staff: one meteor per cast, a random one of three sizes of projectile 424, at 0.75x speed; scatter +-1.2 px on the raw aim, +-0.8 px/tick on the fall; ai[1] = scale 0.5..0.8.</summary>
		public const int MeteorVariantCount = 3;
		public const float MeteorAimScatterPx = 1.2f, MeteorFallScatter = 0.8f, MeteorSpeedFactor = 0.75f, MeteorScaleMin = 0.5f, MeteorScaleRange = 0.3f;

		/// <summary>Lunar Flare: three flares per cast (projectile 645) at half speed; ai[1] = the height they stop passing through terrain at (the aim point's).</summary>
		public const int LunarFlareCount = 3;
		public const float LunarFlareSpeedFactor = 0.5f;
	}
}
