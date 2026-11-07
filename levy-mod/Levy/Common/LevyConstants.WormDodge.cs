namespace Levy.Common
{
	/// <summary>Numbers of the worm dodge (SoldierNPC.WormDodge.cs): hold still while a worm head closes, then one late sidestep or jump across its path.</summary>
	public static partial class LevyConstants
	{
		public static class WormDodge
		{
			/// <summary>Extra clearance (px) a worm-head dodge must leave around the head's box, on top of the field's own boss margin.</summary>
			public const float MarginPx = 40f;
			/// <summary>The dodge starts only when starting this many ticks later would no longer clear the head (the last moment that still works).</summary>
			public const int SlackTicks = 6;
			/// <summary>Total heading change (rad) a head is assumed to keep making at its current turn rate before it flies straight.</summary>
			public const float TurnCapRad = 1.2f;
			/// <summary>Largest per-tick turn rate (rad) read from a head.</summary>
			public const float MaxTurnPerTick = 0.09f;
			/// <summary>Score added (a hit is 100) for a candidate that runs along the head's heading or toward the head, per unit of that share of its first-12-tick displacement.</summary>
			public const float AlongPenalty = 14f;
			/// <summary>How long (ticks) a deferred dodge holds still before the next look.</summary>
			public const int HoldTicks = 4;
		}
	}
}
