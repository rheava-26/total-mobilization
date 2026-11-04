using Terraria;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// The targeting handle of an officer. Since the rework the officer fights as a reskinned soldier (the AVATAR), so this points enemies at the
	/// avatar, not the parked town NPC: <see cref="NPC"/> is the avatar's body and <see cref="Dummy"/> is a free-standing Player swapped into
	/// Main.player[255] for one AI call (exactly as a soldier's dummy is). <see cref="TownNpc"/> stays the identity the commission lives on.
	/// The avatar link is refreshed each tick by <see cref="OfficerSystem"/>; with no live avatar the body reads as not-alive and drops out of the target list.
	/// </summary>
	public sealed class OfficerBody : ITargetBody
	{
		/// <summary>The conscripted town NPC this commission belongs to (where the data lives; what the player shops with).</summary>
		public NPC TownNpc { get; }
		/// <summary>The transient avatar that actually marches and fights, or null between spawns.</summary>
		public SoldierNPC Avatar { get; private set; }
		public Player Dummy { get; }

		public OfficerBody(NPC townNpc)
		{
			TownNpc = townNpc;
			Dummy = new Player();
			Dummy.whoAmI = SoldierNPC.DummySlot;
			Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.difficulty = 0;
			Dummy.direction = 1;
			Dummy.name = townNpc.GivenName ?? "";
		}

		/// <summary>OfficerSystem sets the current avatar for this commission each tick (null when none is live).</summary>
		public void SetAvatar(SoldierNPC avatar) => Avatar = avatar;

		/// <summary>The body enemies chase: the avatar's NPC while one lives, else the town NPC (so callers never get null).</summary>
		public NPC NPC => Avatar != null && Avatar.NPC.active ? Avatar.NPC : TownNpc;

		/// <summary>Taunt weight from the officer's registry entry.</summary>
		public int TargetAggro => OfficerRegistry.TryGet(TownNpc.type, out var def) ? def.Aggro : 0;

		/// <summary>Alive only while a live avatar is marching; a commission with no avatar is not a target.</summary>
		public bool IsAlive => Avatar != null && Avatar.NPC.active && Avatar.NPC.life > 0;

		public void SyncDummyTransform()
		{
			NPC body = NPC;
			Dummy.width = body.width; Dummy.height = body.height;
			Dummy.position = body.position; Dummy.velocity = body.velocity;
			Dummy.direction = body.direction == 0 ? 1 : body.direction;
			Dummy.gravDir = 1f; Dummy.active = true; Dummy.dead = false; Dummy.ghost = false;
			Dummy.statLife = body.life; Dummy.statLifeMax = Dummy.statLifeMax2 = System.Math.Max(1, body.lifeMax);
			Dummy.whoAmI = SoldierNPC.DummySlot;
		}
	}
}
