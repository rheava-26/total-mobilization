using Terraria;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// The targeting handle of one conscripted town NPC: the NPC itself plus a free-standing Player that enemy AI reads while the officer is swapped into
	/// Main.player[255] (exactly what a soldier's Dummy does). Created lazily per officer on every machine, because NPC AI (and so the swap) runs on clients too.
	/// </summary>
	public sealed class OfficerBody : ITargetBody
	{
		public NPC NPC { get; }
		public Player Dummy { get; }

		public OfficerBody(NPC npc)
		{
			NPC = npc;
			Dummy = new Player();
			Dummy.whoAmI = SoldierNPC.DummySlot;
			Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.difficulty = 0;
			Dummy.direction = 1;
			Dummy.name = npc.GivenName ?? "";
		}

		/// <summary>Taunt weight from the officer's registry entry.</summary>
		public int TargetAggro => OfficerRegistry.TryGet(NPC.type, out var def) ? def.Aggro : 0;

		public bool IsAlive => NPC.active && NPC.life > 0;

		public void SyncDummyTransform()
		{
			Dummy.width = NPC.width; Dummy.height = NPC.height;
			Dummy.position = NPC.position; Dummy.velocity = NPC.velocity;
			Dummy.direction = NPC.direction == 0 ? 1 : NPC.direction;
			Dummy.gravDir = 1f; Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.statLife = NPC.life; Dummy.statLifeMax = Dummy.statLifeMax2 = System.Math.Max(1, NPC.lifeMax);
			Dummy.whoAmI = SoldierNPC.DummySlot;
		}
	}
}
