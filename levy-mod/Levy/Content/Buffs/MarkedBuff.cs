using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// The army's SHRED mark. The Dryad and the Tax Collector hang it on every enemy in reach; while it stands, every shot from the player's army
	/// (soldiers, their summoned units, and an officer's own bolt) lands harder on that enemy. This buff is only the flag the amp reads —
	/// the amp itself lives on <see cref="Levy.Common.Mark.MarkAmpGlobalNPC"/>. A debuff: it ticks down on its own and is gone the instant the
	/// enemy dies, so it never leaks state. Applied and cleared server-side (<see cref="Levy.Common.Mark.MarkSystem"/>) and synced to clients.
	/// </summary>
	public class MarkedBuff : ModBuff
	{
		/// <summary>Borrow Ichor's icon: a yellow "takes more damage" debuff reads exactly right for the shred, and ships no new art.</summary>
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Ichor;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;        // a debuff on the target, not a boon
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;    // never persisted: a marked enemy on reload starts clean
		}

		// On an enemy, presence is the whole effect; it expires on its own cadence. Nothing to do per tick.
		public override void Update(NPC npc, ref int buffIndex) { }

		// Defensive: the mark is for enemies only. Should it ever land on a player, shed it at once.
		public override void Update(Player player, ref int buffIndex)
		{
			player.DelBuff(buffIndex);
			buffIndex--;
		}
	}
}
