namespace Levy.Common
{
	/// <summary>
	/// Event pressure (Events/EventPressure.cs): while a vanilla event runs enemies spawn faster and in greater number, and an invasion begins larger, so its reinforcement waves are many times the vanilla count.
	/// Vanilla is 1 for every multiplier. Difficulty comes from the enemy; nothing here touches the army.
	/// </summary>
	public static partial class LevyConstants
	{
		/// <summary>Enemies spawn this many times as often during an event, plus <see cref="EventSpawnRatePerCp"/> per command point of army in the field, up to <see cref="EventSpawnRateCap"/> (60 CP: 2.0 + 0.6 = 2.6x).</summary>
		public const float EventSpawnRateBase = 2.0f, EventSpawnRatePerCp = 0.01f, EventSpawnRateCap = 4.0f;
		/// <summary>The nearby-enemy cap is the vanilla cap times the spawn multiplier times this share (vanilla 5 becomes 13 at 2.6x), and never past <see cref="EventMaxSpawnsCap"/>.</summary>
		public const float EventMaxSpawnsShare = 1.0f;
		public const int EventMaxSpawnsCap = 30;
		/// <summary>An invasion starts this many times its vanilla size, plus <see cref="InvasionSizePerCp"/> per command point of army, up to <see cref="InvasionSizeCap"/> (60 CP: 3.0 + 1.2 = 4.2x, so the 80-enemy Goblin Army is 336).</summary>
		public const float InvasionSizeBase = 3.0f, InvasionSizePerCp = 0.02f, InvasionSizeCap = 6.0f;
		/// <summary>The army's command points are re-counted this often (ticks).</summary>
		public const int EventCpRefreshTicks = 60;
	}
}
