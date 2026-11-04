using Terraria;
using Terraria.ID;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Who counts as an ally for a Thorium healer or bard soldier: the COMMANDER (the player) and every soldier of that commander, uniformly. The player is just another ally for
	/// every heal and buff target search and every range check. Touches no ThoriumMod type, so it is not JIT-gated.
	/// </summary>
	public static class CompatAllies
	{
		/// <summary>The player this soldier serves, if active and alive. In single player an unbound soldier falls back to the one local player.</summary>
		public static Player CommanderOf(SoldierNPC s)
		{
			Player c = s?.Commander;
			if (c == null && Main.netMode == NetmodeID.SinglePlayer && Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) c = Main.player[Main.myPlayer];
			return c != null && c.active && !c.dead ? c : null;
		}

		/// <summary>True when <paramref name="other"/> serves the same commander as <paramref name="s"/> (a squadmate).</summary>
		public static bool SameCommander(SoldierNPC s, SoldierNPC other) =>
			other?.Record != null && s?.Record != null && string.Equals(other.Record.CommanderName ?? "", s.Record.CommanderName ?? "", System.StringComparison.Ordinal);
	}
}
