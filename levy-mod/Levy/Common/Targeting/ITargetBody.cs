using Terraria;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Anything enemies can be pointed at in place of a player: a soldier's body, or an officer (a conscripted town NPC). Enemy targeting only needs
	/// the NPC to chase, a free-standing <see cref="Player"/> to stand in Main.player[255] for the swap, the taunt weight and a liveness test.
	/// </summary>
	public interface ITargetBody
	{
		/// <summary>The body the enemy chases and the contact damage lands on.</summary>
		NPC NPC { get; }
		/// <summary>Free-standing Player the enemy AI reads while this body is swapped into slot 255. Never in Main.player[] outside the swap.</summary>
		Player Dummy { get; }
		/// <summary>Taunt weight (Player.aggro equivalent). Targeting adds the soldier standing advantage to it.</summary>
		int TargetAggro { get; }
		/// <summary>Active, alive and not mid-death.</summary>
		bool IsAlive { get; }
		/// <summary>Moves the dummy onto the body (position, velocity, life, slot 255) just before it is swapped in.</summary>
		void SyncDummyTransform();
	}
}
