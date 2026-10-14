namespace Levy.Common
{
	/// <summary>Dialogue numbers. The core budget numbers (floating interval, emote budget, repeat cooldown, idle chat) live in LevyConstants.cs.</summary>
	public static partial class LevyConstants
	{
		/// <summary>How long an emote bubble stays above a soldier.</summary>
		public const int DialogueEmoteBubbleTicks = 120;
		/// <summary>A PAYDAY/UNPAID/HOLD_FIRE line stays the answer to a right-click chat for this long.</summary>
		public const int DialogueRecentLineTicks = 60 * 120;
		/// <summary>Chance that a right-click chat is a NAMEDROP line (when no event or recent line applies).</summary>
		public const float DialogueNamedropChance = 0.3f;
		/// <summary>Chance that an applicable biome or situation line is used instead of falling through to IDLE.</summary>
		public const float DialogueSituationChance = 0.65f;
		/// <summary>Selection weight of a personality line (temperament or Veteran) relative to an Any line.</summary>
		public const int DialoguePersonalityWeight = 3;
		/// <summary>Soldiers also show the text of emote triggers (ENGAGE, KILL, HURT...) as a small floating line, within the squad budget.</summary>
		public const bool DialogueChatterText = true;
		/// <summary>Floating lines longer than this are split over two or three rows.</summary>
		public const int DialogueFloatingRowChars = 34;
		/// <summary>Floating text lives this long (vanilla combat text lives 60 ticks, too short to read a sentence).</summary>
		public const int DialogueFloatingLifeTicks = 60 * 3;
		/// <summary>Only clients within this many pixels of the speaker receive a floating line.</summary>
		public const float DialogueFloatingRangePx = 3200f;
		/// <summary>A comrade placeholder with no named comrade is filled with a squad-mate inside this many tiles.</summary>
		public const int DialogueComradeSearchTiles = 40;
	}
}
