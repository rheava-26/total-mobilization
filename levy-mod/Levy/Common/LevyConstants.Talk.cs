namespace Levy.Common
{
	/// <summary>Talking to levies: the combat chat bar, the talk-hold, Close up and the levy inventory window.</summary>
	public static partial class LevyConstants
	{
		// ---------- combat chat bar ----------
		/// <summary>Most bars on screen at once. Nearby speakers and important lines win the slots; the rest wait.</summary>
		public const int ChatBarMaxVisible = 5;
		/// <summary>Most lines waiting for a free slot. Past this the lowest-scored waiting line is dropped.</summary>
		public const int ChatBarMaxWaiting = 16;
		/// <summary>A waiting line older than this (ticks) is stale and dropped.</summary>
		public const int ChatBarWaitExpireTicks = 4 * 60;
		/// <summary>An important line (higher priority) may bump a bar that has been up at least this long (ticks).</summary>
		public const int ChatBarMinShowTicks = 45;
		/// <summary>Speakers farther than this (tiles) from the local player never get a bar.</summary>
		public const float ChatBarRangeTiles = 70f;
		/// <summary>Typewriter speed: characters revealed per tick (0.75 = 45 per second).</summary>
		public const float ChatBarCharsPerTick = 0.75f;
		/// <summary>After the last character shows, the bar stays this many ticks plus ChatBarHoldPerCharTicks per character (capped).</summary>
		public const int ChatBarHoldBaseTicks = 90, ChatBarHoldPerCharTicks = 2, ChatBarHoldMaxTicks = 60 * 5;
		/// <summary>Fade-out length (ticks).</summary>
		public const int ChatBarFadeTicks = 18;
		/// <summary>Priorities: chatter, announcements, big moments.</summary>
		public const int ChatBarPriorityChatter = 1, ChatBarPriorityNotice = 2, ChatBarPriorityBig = 3;
		/// <summary>Bar layout: text scale, widest text row (px), most rows, padding, gap above the speaker's head, gap between stacked bars.</summary>
		public const float ChatBarTextScale = 0.82f;
		public const int ChatBarMaxTextWidth = 230, ChatBarMaxRows = 3, ChatBarPad = 6, ChatBarHeadGap = 46, ChatBarStackGap = 3;

		// ---------- talking ----------
		/// <summary>A levy being talked to walks to within this many tiles of the player and stops.</summary>
		public const float TalkStandTiles = 2.5f;
		/// <summary>A right-click "call over" on a levy this far or nearer (tiles) brings it to the player; farther is ignored.</summary>
		public const float TalkCallMaxTiles = 70f;
		/// <summary>A call stays live this long (ticks) while the levy walks over.</summary>
		public const int TalkCallTicks = 60 * 10;

		/// <summary>The levy inventory window closes when the levy is farther than this from the player (tiles).</summary>
		public const float InventoryCloseTiles = 14f;

		// ---------- Close up ----------
		/// <summary>Close up multiplies every dispersion spacing by this (Spread Out uses SpreadOutMult).</summary>
		public const float CloseUpMult = 0.35f;
		/// <summary>Following soldiers on Close up pack into columns this far apart, starting this far from the commander and never past the max (tiles).</summary>
		public const float CloseUpFollowStartTiles = 0.9f, CloseUpFollowStepTiles = 0.45f, CloseUpFollowMaxTiles = 4.5f;
		/// <summary>On Close up a following soldier stops within CloseUpFollowStopTiles of its slot and starts walking again past CloseUpFollowStartMoveTiles (normal: FollowDistance / 3 and / 2).</summary>
		public const float CloseUpFollowStopTiles = 0.6f, CloseUpFollowStartMoveTiles = 1.6f;
		/// <summary>Close up keeps Follow soldiers from chasing a target farther than this from the commander (tiles).</summary>
		public const int CloseUpChaseLimit = 14;
		/// <summary>Idle / holding soldiers on Close up share a line this wide each side of the anchor (tiles).</summary>
		public const float CloseUpIdleLineHalfTiles = 4f;
	}
}
