namespace Levy.Common
{
	/// <summary>
	/// Reusable melee-class throwers (Daybreak, Shadowflame Knife, Vampire Knives): a no-melee, non-consumable weapon whose shot flies on a thrown AI
	/// (ThrownProjectile 2 / StickProjectile 113). They are thrown over and over, never used up. Additive partial; see LevyConstants.cs for the rest.
	/// </summary>
	public static partial class LevyConstants
	{
		// ---------- Reach ----------
		/// <summary>Engagement range (tiles) of a reusable melee thrower: closer than any caster, so the soldier presses in and throws from a short standoff.</summary>
		public const int RangeThrownMelee = 14;

		// ---------- Throw arc (per projectile AI update; see ThrownAim.Solve) ----------
		/// <summary>Shadowflame Knife (proj 497, aiStyle 2): straight for 30 ticks, then +0.5 gravity and 0.99 drag per tick.</summary>
		public static readonly ThrownFlight ThrownMeleeFlightShadowflameKnife = new(30, 0.5f, 0.99f);
		/// <summary>Daybreak (proj 636, aiStyle 113): straight for 45 updates, then +0.15 gravity and 0.995 drag per update. It runs two updates per tick, so the solver steps in half ticks.</summary>
		public static readonly ThrownFlight ThrownMeleeFlightDaybreak = new(45, 0.15f, 0.995f);
		/// <summary>A projectile that never drops (the Vampire Knife flies dead straight): no gravity ever starts.</summary>
		public static readonly ThrownFlight ThrownMeleeFlightStraight = new(100000, 0f, 1f);

		// ---------- Vampire Knives (Levy-owned SoldierVampireKnife) ----------
		/// <summary>Knives per throw: the first four always, then one more on each of these 1-in-N rolls (vanilla: 4, +1 at 1/2, +1 at 1/4, +1 at 1/8, +1 at 1/16).</summary>
		public const int VampKnifeBaseCount = 4;
		public static readonly int[] VampKnifeExtraRolls = { 2, 4, 8, 16 };
		/// <summary>Spread: knife k is knocked off the aim by up to (+-VampKnifeSpreadRange) * VampKnifeSpreadStep * k px/tick on each axis.</summary>
		public const int VampKnifeSpreadRange = 35;
		public const float VampKnifeSpreadStep = 0.05f;
		/// <summary>Ticks of full-strength flight before the knife fades out (and loses 10% damage and knockback per tick while it does).</summary>
		public const int VampKnifeFlightTicks = 30;
		/// <summary>Alpha gained per tick while fading; the knife is gone at 255.</summary>
		public const int VampKnifeFadePerTick = 10;
		/// <summary>Hit box used against tiles (px); the knife's own box is wider.</summary>
		public const int VampKnifeTileBoxPx = 4;
		/// <summary>HP the SOLDIER recovers per knife that connects: this fraction of the damage dealt, clamped to the min/max. The commander is never healed.</summary>
		public const float VampKnifeHealFraction = 0.075f;
		public const int VampKnifeHealMin = 1, VampKnifeHealMax = 5;
	}
}
