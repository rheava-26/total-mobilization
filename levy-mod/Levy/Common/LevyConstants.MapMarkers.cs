using Microsoft.Xna.Framework;

namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>
		/// Map markers (client draw only): army, officer and boss markers on the minimap, overlay map and fullscreen map.
		/// Every size is in UI pixels at 100% UI scale, outer diameter including the 1 px dark outline.
		/// </summary>
		public static class MapMarkers
		{
			// ---- army pips ----
			/// <summary>One soldier (or summoned unit) drawn on its own.</summary>
			public const int SoldierPip = 6;
			/// <summary>Aggregated army marker by head-count: below ArmyMediumCount, up to ArmyLargeCount, and above.</summary>
			public const int ArmyPipSmall = 9, ArmyPipMedium = 11, ArmyPipLarge = 13;
			public const int ArmyMediumCount = 8, ArmyLargeCount = 20;

			// ---- aggregation (minimap and overlay map only; the fullscreen map shows every soldier) ----
			/// <summary>Soldiers this close together (tiles) share one marker.</summary>
			public const float ArmyMergeTiles = 40f;
			/// <summary>Markers never sit closer than this on screen, so a far zoomed-out map still merges what would overlap.</summary>
			public const float ArmyMergeMinPx = 14f;
			/// <summary>At most this many aggregated markers per frame; any further group joins the nearest one.</summary>
			public const int MaxArmyMarkers = 6;
			/// <summary>The fullscreen map draws each soldier unless there are more than this many; past it, tight groups merge.</summary>
			public const int FullscreenIndividualMax = 300;
			/// <summary>Merge radius (screen px) used on the fullscreen map past FullscreenIndividualMax.</summary>
			public const float FullscreenMergePx = 8f;

			// ---- officer and boss pips: the stand-in drawn only where there is no vanilla head icon to put a ring around ----
			/// <summary>Officer diamond: a dark-outlined white diamond with a flat gold one inside, leaving a white rim about 1.5 px wide.</summary>
			public const int OfficerPipOuter = 16, OfficerPipInner = 8;
			/// <summary>Boss disc: a dark-outlined white disc with a flat red one inside, leaving a 1 px white rim. Always larger than the officer diamond.</summary>
			public const int BossPipOuter = 17, BossPipInner = 13;

			// ---- halo ring drawn around the vanilla head icon that sits on top of an officer or boss ----
			public const float RingThickness = 2.5f;
			/// <summary>Clear space between the vanilla icon outline and the ring (UI px).</summary>
			public const float RingGap = 1.5f;
			/// <summary>Vanilla draws its head icon outline up to this many icon-scale pixels beyond the sprite.</summary>
			public const float VanillaIconOutline = 4f;
			/// <summary>A player's head icon reaches about this far from its centre at icon scale 1 (a roughly 40 px head plus its 2 px outline, measured against a
			/// real minimap screenshot). A grouped army marker keeps this far, plus its own size and HeadClearGap, from every player so it is never hidden under a head.</summary>
			public const float PlayerHeadHalfPx = 22f;
			public const float HeadClearGap = 1.5f;
			/// <summary>Vanilla paints a player's head up and left of the player's true spot: this far left everywhere, and this far up on the minimap or on the fullscreen
			/// and overlay maps (each plus 0.4 px per map scale). Copied from Main.DrawMap so the army markers clear the head where it really is.</summary>
			public const float PlayerHeadShiftX = -6f, PlayerHeadShiftYMinimap = -8f, PlayerHeadShiftYMap = -4f;
			/// <summary>Head icon size (px) assumed when the texture is not loaded yet.</summary>
			public const int FallbackIconPx = 24;
			/// <summary>Largest ring texture ever baked (px), whatever the icon reports.</summary>
			public const int MaxRingPx = 200;

			// ---- text and feedback ----
			public const float LabelScale = 0.75f;
			/// <summary>Half the height of a head-count label (UI px): the room a grouped marker needs above and below itself to keep its number on the map.</summary>
			public const float LabelHalfHeight = 8f;
			public const float HoverGrow = 1.35f;
			/// <summary>Boss ring pulse: speed (radians per second) and the dimmest the ring gets (0..1).</summary>
			public const float PulseSpeed = 4f, PulseMin = 0.6f;
			/// <summary>Opacity of the dark outline baked into every marker texture.</summary>
			public const float OutlineOpacity = 0.9f;
			/// <summary>Cached marker textures kept before the cache is flushed and rebuilt (sizes change as the map zooms).</summary>
			public const int TextureCacheMax = 96;

			// ---- colours ----
			public static readonly Color RegularCyan = new(64, 220, 255);
			public static readonly Color MilitiaBlue = new(72, 120, 255);
			public static readonly Color SummonAqua = new(120, 255, 225);
			public static readonly Color ArmyAzure = new(40, 190, 255);
			public static readonly Color OfficerGold = new(255, 196, 36);
			public static readonly Color OfficerWhite = new(255, 246, 214);
			public static readonly Color BossRed = new(236, 38, 38);
			public static readonly Color BossWhite = new(255, 236, 236);
			public static readonly Color BossRing = new(255, 70, 70);
			public static readonly Color LabelColor = new(235, 250, 255);
		}
	}
}
