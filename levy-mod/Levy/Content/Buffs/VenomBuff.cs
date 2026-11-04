using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// The Witch Doctor's VENOM. The army's hits envenom an enemy while he is fielded (<see cref="Levy.Common.Venom.VenomSystem"/>); this buff is only the flag the
	/// damage-over-time reads — the burn itself lives on <see cref="Levy.Common.Venom.VenomGlobalNPC"/>. A debuff: it ticks down on its own and is gone the instant the
	/// enemy dies, so it never leaks state. Applied server-side and synced to clients.
	/// </summary>
	public class VenomBuff : ModBuff
	{
		/// <summary>Borrow vanilla Venom's icon: no new art.</summary>
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Venom;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;        // a debuff on the target, not a boon
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;    // never persisted: an envenomed enemy on reload starts clean
		}

		// On an enemy, presence is the whole effect; it expires on its own cadence. Nothing to do per tick.
		public override void Update(NPC npc, ref int buffIndex) { }

		// Defensive: venom is for enemies only. Should it ever land on a player, shed it at once.
		public override void Update(Player player, ref int buffIndex)
		{
			player.DelBuff(buffIndex);
			buffIndex--;
		}
	}
}
