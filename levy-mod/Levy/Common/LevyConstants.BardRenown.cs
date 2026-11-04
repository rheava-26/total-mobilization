namespace Levy.Common
{
	/// <summary>Bard renown: the army-wide Inspiration growth earned by bard levies' damage. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Damage the army's bards must deal in total for the FIRST renown point. Point n costs <c>BardRenownBase * n^BardRenownExponent</c> in total (cumulative), so points come ever slower.</summary>
		public const double BardRenownBase = 4000.0;
		/// <summary>Curve steepness: 1.0 = a point every BardRenownBase damage; 1.5 = 4000, 11.3k, 20.8k, 32k, 44.7k ... 126k for the 10th, 657k for the 30th.</summary>
		public const double BardRenownExponent = 1.5;
		/// <summary>Most renown points the army can earn. A bard's Inspiration maximum is his own crystals plus the army's renown, never above Thorium's player cap (40), so 30 on top of the base 10 reaches it.</summary>
		public const int BardRenownMaxPoints = 30;
		/// <summary>Thorium's normal player Inspiration range: base maximum and the cap crystals can raise it to.</summary>
		public const int BardInspirationBase = 10, BardInspirationCap = 40;
	}
}
