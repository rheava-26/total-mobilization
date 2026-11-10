namespace Levy.Common
{
	/// <summary>
	/// Idle sitting and chatting (Content/NPCs/SoldierNPC.Sit.cs). A Regular with nothing to do sits on a chair, bench or throne nearby, chats with the levies seated close to it, and stands the moment a fight, an order
	/// or a threat arrives. Tiles unless the name says ticks.
	/// </summary>
	public static partial class LevyConstants
	{
		/// <summary>A levy must have been calm (no target, nothing hostile near, not hurt, no walking to do) this long before it looks for a seat, plus a per-levy share of <see cref="SitIdleSpreadTicks"/> so the army does not sit down together.</summary>
		public const int SitIdleTicks = 60 * 4, SitIdleSpreadTicks = 60 * 8;
		/// <summary>Out of combat this long (no target, not hit) before sitting.</summary>
		public const int SitQuietTicks = 60 * 6;
		/// <summary>A hostile closer than this wakes a seated levy (and counts as combat before it sits).</summary>
		public const float SitWakeEnemyTiles = 32f;
		/// <summary>How far from its post a levy looks for a seat: holding a post, standing by with no orders, and following a commander who is standing still.</summary>
		public const int SitHoldRadiusTiles = 14, SitIdleRadiusTiles = 28, SitFollowRadiusTiles = 20;
		/// <summary>A following levy sits only while the commander is within this many tiles and standing still; a seated one stands up when the commander walks more than <see cref="SitFollowLeaveTiles"/> away.</summary>
		public const float SitFollowMaxTiles = 28f, SitFollowLeaveTiles = 34f;
		/// <summary>The commander counts as standing still while their speed stays under this many pixels per tick (standing at a workbench, mining, building).</summary>
		public const float SitCommanderStillSpeed = 1f;
		/// <summary>Seats are looked for within this many tile rows above and below the levy's feet.</summary>
		public const int SitSeatRows = 6;
		/// <summary>A seat search that found nothing is not repeated for this long; a levy that could not reach its seat gives up after <see cref="SitWalkTimeoutTicks"/> and waits <see cref="SitFailCooldownTicks"/>.</summary>
		public const int SitScanRetryTicks = 60 * 3, SitWalkTimeoutTicks = 60 * 20, SitFailCooldownTicks = 60 * 25;
		/// <summary>A levy sits down when its feet are within this many pixels of the seat across, and this many up and down.</summary>
		public const float SitArriveXPx = 14f, SitArriveYPx = 20f;
		/// <summary>Seated levies within this many tiles of each other chat: one speaks to the nearest every random interval between the two tick values, and only while a player is within <see cref="SitChatPlayerTiles"/>.</summary>
		public const float SitChatTiles = 12f, SitChatPlayerTiles = 100f;
		public const int SitChatMinTicks = 60 * 8, SitChatMaxTicks = 60 * 24;
		/// <summary>How long the speaker's emote bubble stays up (ticks).</summary>
		public const int SitChatEmoteTicks = 120;
	}
}
