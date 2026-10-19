namespace Levy.Common
{
	/// <summary>Right-click alternate attacks (Item.AltFunctionUse): how often soldiers mix them in. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Master switch: false and every soldier fires only the left-click attack, as before.</summary>
		public const bool AltFireEnabled = true;

		// ---------- Cadence ----------
		/// <summary>Chance, each time the alt is off cooldown and the soldier is about to attack, that this attack is the alt. High: the alt is the weapon's big attack.</summary>
		public const float AltUseChance = 0.8f;
		/// <summary>Ticks after an alt before the next one may be chosen; the primary fills the gap. Plus a random 0..AltGapJitterTicks so a squad does not alt in step.</summary>
		public const int AltMinGapTicks = 75;
		public const int AltGapJitterTicks = 30;

		// ---------- Defensive backoffs ----------
		/// <summary>A weapon with no alt is only re-probed this often (it behaves exactly as before in between).</summary>
		public const int AltNoAltRecheckTicks = 300;
		/// <summary>The alt could not start right now (its CanUseItem said no, or not enough mana): the primary is used and the alt is retried after this.</summary>
		public const int AltRetryTicks = 40;
		/// <summary>The alt ran but spawned nothing: back off this long, and after <see cref="AltGiveUpFailures"/> such failures in a row stop trying that weapon.</summary>
		public const int AltFailBackoffTicks = 240;
		public const int AltGiveUpFailures = 4;

		// ---------- Saving mana for the alt ----------
		/// <summary>A caster short of the alt's mana holds fire to save for it when the pool will cover it within this many ticks (0 turns saving off).</summary>
		public const int AltSaveManaTicks = 100;
		/// <summary>Longest a soldier holds fire waiting on alt mana before giving up and using the primary again.</summary>
		public const int AltHoldMaxTicks = 150;
	}
}
