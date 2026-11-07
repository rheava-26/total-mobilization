namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Formation defense (config FormationDefense): each other living, un-routed levy within <see cref="Tiles"/> tiles cuts the damage a levy takes by <see cref="DrPerAlly"/>, up to <see cref="MaxDr"/>.</summary>
		public static class Formation
		{
			public const int Tiles = 15;
			public const float DrPerAlly = 0.02f;
			public const float MaxDr = 0.20f;
		}

		/// <summary>Sunflowers (the Happy! buff, or a placed Sunflower within the vanilla scene radius) slow fear gain to this fraction.</summary>
		public const float SunflowerFearMult = 0.5f;

		/// <summary>Numbers of the design options (LEVY_FEARMODEL / LEVY_SURV). Nothing here is read unless an option is switched on.</summary>
		public static class Lab
		{
			// ---- fear: events ----
			/// <summary>An ally dying or taking a big hit frightens witnesses only within this many tiles (the shipped model uses the 80-tile boss view range).</summary>
			public const int EventsRadiusTiles = 40;
			/// <summary>A hit counts as an event only when it takes at least this fraction of the victim's max HP (own hits and allies' hits alike).</summary>
			public const float EventsBigHitFrac = 0.30f;
			/// <summary>Ticks without being hit before the meter starts to drain (events model).</summary>
			public const int EventsDrainDelayTicks = 60 * 3;
			public const float EventsDrainPerSecond = 4f;

			// ---- fear: proximity_slow ----
			/// <summary>Multiplier on the per-tick boss-in-view gain.</summary>
			public const float SlowGainScale = 0.20f;
			/// <summary>Healthy = above this fraction of max HP: the meter drains while it holds.</summary>
			public const float SlowHealthyFrac = 0.70f;
			public const float SlowDrainPerSecond = 2.5f;

			// ---- fear: rally ----
			/// <summary>A living commander within this many tiles rallies a levy (fear down to RallyValue, Shaken/Routed ends).</summary>
			public const int RallyCommanderTiles = 40;
			/// <summary>The rally check runs every this many ticks per body.</summary>
			public const int RallyPulseTicks = 20;

			// ---- survivability: veteran ----
			public const int VetMaxFights = 5;
			public const float VetHpPerFight = 0.06f;
			public const int VetFightsPerDefense = 2;
			public const float VetManAtArmsHp = 0.10f, VetChampionHp = 0.20f;
		}
	}
}
