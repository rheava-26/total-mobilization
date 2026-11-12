namespace Levy.Common
{
	/// <summary>Numbers of the Holding Banner: the tile, the recipe, where levies stand around it and what the buff gives.</summary>
	public static partial class LevyConstants
	{
		public static class Banner
		{
			// ---------- tile + recipe (Work Bench) ----------
			public const int Width = 2, Height = 4;
			public const int RecipeWood = 8, RecipeSilk = 3;

			// ---------- where levies stand ----------
			/// <summary>Standing columns considered either side of the banner's centre; the nearest <see cref="MaxStands"/> that have ground at the banner's level are the banner's stands.</summary>
			public const int StandReachTiles = 4;
			public const int MaxStands = 10;
			/// <summary>A stand may sit this many rows above or below the foot of the pole (a sloped floor).</summary>
			public const int StandRowSlack = 1;
			/// <summary>A levy within this many tiles of its stand is standing at it and stops walking; once it has stopped it starts again only beyond <see cref="StandResumeTiles"/>.</summary>
			public const float StandArriveTiles = 1.0f, StandResumeTiles = 2.5f;
			/// <summary>A levy farther than this from its stand is still on the march: it keeps walking while it shoots (a levy on the stand fights from it, inside the Hold leash).</summary>
			public const float MarchFarTiles = 12f;
			/// <summary>A banner whose stand a levy could not get to (no progress along its planned route) is set aside for that levy for this long and the next nearest banner is tried.</summary>
			public const int UnreachableBanTicks = 60 * 90;
			/// <summary>Ticks between aura pulses and how long each pulse keeps the buff (refreshed, never stacked).</summary>
			public const int PulseTicks = 20, BuffTicks = 90;

			// ---------- the buff ----------
			/// <summary>Reach of the buff from the banner's centre: sideways, and up / down from the foot of the pole.</summary>
			public const int AuraTiles = 5, AuraRows = 4;
			public const int Defense = 12;
			public const float DamageReduction = 0.2f;
		}
	}
}
