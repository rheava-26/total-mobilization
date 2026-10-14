using Microsoft.Xna.Framework;

namespace Levy.Common
{
	/// <summary>Settlement and militia runtime numbers. Additive partial of <see cref="LevyConstants"/> (Militia agent).</summary>
	public static partial class LevyConstants
	{
		// ---------- Settlement runtime ----------
		/// <summary>Ticks a barracks' town-NPC membership list stays cached; UI and dawn code both ask for it.</summary>
		public const int TownNpcCacheTicks = 60;
		/// <summary>After StandDown, a militia body that has not reached home by then is dismissed where it stands.</summary>
		public const int StandDownSafetyTicks = 60 * 30;
		/// <summary>Auto-muster credits the nearest active player within this many tiles of the barracks as the mobilizer.</summary>
		public const int AutoMusterPlayerSearchTiles = 300;
		/// <summary>Interval of the settlement housekeeping pass: orphaned militia, stale runtime entries, armory sync.</summary>
		public const int SettlementHousekeepingTicks = 60;
		/// <summary>Militia hair styles are rolled from 0 to this count minus one.</summary>
		public const int MilitiaHairStyles = 51;

		// ---------- Chat colours ----------
		/// <summary>The vanilla event colour (invasion arrived, Blood Moon rising).</summary>
		public static readonly Color EventChatColor = new(175, 75, 255);
		/// <summary>Plain notices to one player.</summary>
		public static readonly Color NoticeChatColor = new(255, 240, 20);

		// ---------- Militia looks ----------
		public static readonly Color[] MilitiaHairColors =
		{
			new(215, 90, 55), new(60, 40, 30), new(120, 80, 50), new(230, 200, 120), new(35, 30, 30), new(170, 120, 80), new(200, 60, 40), new(110, 110, 110),
		};
		public static readonly Color[] MilitiaSkinColors =
		{
			new(255, 125, 90), new(240, 180, 140), new(210, 150, 110), new(170, 110, 75), new(120, 80, 55), new(255, 200, 170),
		};
		public static readonly Color[] MilitiaEyeColors =
		{
			new(105, 90, 75), new(60, 90, 140), new(70, 120, 70), new(40, 40, 40), new(130, 100, 60),
		};
		public static readonly Color[] MilitiaShirtColors =
		{
			new(175, 165, 140), new(120, 100, 80), new(90, 110, 130), new(140, 90, 70), new(100, 120, 90), new(160, 140, 100),
		};
		public static readonly Color[] MilitiaUnderShirtColors =
		{
			new(160, 180, 215), new(200, 200, 190), new(150, 130, 110), new(180, 160, 150),
		};
		public static readonly Color[] MilitiaPantsColors =
		{
			new(255, 230, 175), new(90, 80, 70), new(110, 95, 75), new(70, 80, 100), new(130, 110, 90),
		};
		public static readonly Color[] MilitiaShoeColors =
		{
			new(160, 105, 60), new(80, 60, 45), new(50, 45, 40), new(120, 90, 60),
		};
	}
}
