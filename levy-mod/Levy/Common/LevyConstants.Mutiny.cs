using Microsoft.Xna.Framework;

namespace Levy.Common
{
	public static partial class LevyConstants
	{
		/// <summary>Wage Mutiny numbers (LevyConfig.WageMutiny). Copper unless the name says otherwise.</summary>
		public static class Mutiny
		{
			// ---- robbing town NPCs ----
			/// <summary>A mutineer looks for a town NPC to rob within this many tiles.</summary>
			public const float NpcSearchTiles = 90f;
			/// <summary>A mutineer robs the NPC once it stands within this many tiles.</summary>
			public const float RobReachTiles = 2.5f;
			/// <summary>What one town NPC hands over, in Levy daily wages (it is not robbed again until its mood recovers).</summary>
			public const long NpcPurseWages = 2;
			/// <summary>A robbed NPC is sour for this long: its prices rise and its mood report says why. One in-game day.</summary>
			public const int RobbedMoodTicks = 60 * 60 * 24;
			/// <summary>Price multiplier on a robbed town NPC's shop.</summary>
			public const double RobbedPriceMult = 1.25;

			// ---- attacking the commander ----
			/// <summary>A mutineer strikes the commander once within this many tiles.</summary>
			public const float StrikeReachTiles = 2.2f;
			/// <summary>Ticks between a mutineer's strikes at the commander.</summary>
			public const int StrikeCooldownTicks = 90;
			/// <summary>A strike does at least this much damage, or this fraction of the commander's maximum life, whichever is more (defense still applies).</summary>
			public const int StrikeMinDamage = 12;
			public const float StrikeLifeFraction = 0.04f;

			// ---- presentation ----
			/// <summary>Tint behind a mutineer's body (premultiplied: the multiplier is the opacity).</summary>
			public static readonly Color Tint = Color.Red * 0.3f;
			/// <summary>Colour of the mutiny chat lines and the mutineer's name.</summary>
			public static readonly Color Alarm = new(255, 50, 40);
			/// <summary>A mutineer shouts a floating line this often while he still holds his claim.</summary>
			public const int ShoutIntervalTicks = 60 * 25;
			/// <summary>Ticks between settlement passes over the mutineers' back pay while any exist.</summary>
			public const int SettleIntervalTicks = 60;
		}
	}
}
