namespace Levy.Common
{
	/// <summary>Army weapon allocation numbers: the class-less spread and the mana pull toward magic. Additive partial; see LevyConstants.cs for the rest.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Levies with no class of their own are spread over melee, ranged, magic and thrown by stock instead of all taking the top-ranked weapon.</summary>
		public const bool ClasslessSpread = true;
		/// <summary>A weapon counts as stock for the spread when it ranks at least this fraction of the best melee / ranged / magic / thrown weapon on offer.</summary>
		public const float ClasslessQualityFraction = 0.25f;
		/// <summary>A class takes part in the spread only when its best weapon ranks at least this fraction of the best weapon of any class (a stick is not worth a levy next to a Zenith).</summary>
		public const float ClasslessBucketMinFraction = 0.05f;
		/// <summary>Most levies one thrown stack on a shelf is offered to in a single allocation.</summary>
		public const int ThrownStackCandidates = 12;
		/// <summary>Magic weight curve of a class-less levy by maximum mana (after gear): 1 at or below the floor, 1 + gain at the full mark.</summary>
		public const float MagicManaBiasFloor = 40f, MagicManaBiasFull = 200f, MagicManaBiasGain = 3f;
	}
}
