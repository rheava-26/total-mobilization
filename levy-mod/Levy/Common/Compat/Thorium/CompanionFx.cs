using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Effects a soldier lays on the commander PLAYER. A player's life and buffs are owned by that player's own client, so in multiplayer the server sends a
	/// <see cref="LevyPacket.CompanionEffect"/> to that one client and the client applies it; in single player it is applied directly. Touches no ThoriumMod type
	/// (the bard-empowerment kind is decoded by the gated <see cref="EmpowerCompat"/>), so it is not JIT-gated.
	/// </summary>
	public static class CompanionFx
	{
		public const byte KindHeal = 1, KindEmpower = 2;

		/// <summary>Server / single player: heal <paramref name="p"/> by <paramref name="amount"/> (on the player's own client in multiplayer).</summary>
		public static void HealPlayer(Player p, int amount)
		{
			if (p == null || !p.active || p.dead || amount <= 0) return;
			if (Main.netMode == NetmodeID.Server)
			{
				ModPacket pkt = LevyPackets.New(LevyPacket.CompanionEffect);
				pkt.Write(KindHeal); pkt.Write((byte)p.whoAmI); pkt.Write(amount);
				SendTo(pkt, p.whoAmI);
				return;
			}
			ApplyHeal(p, amount);
		}

		/// <summary>Sends to one client if that slot is connected (a stale player slot must never throw out of a soldier's tick).</summary>
		public static void SendTo(ModPacket pkt, int client)
		{
			if (client < 0 || client >= Main.maxPlayers || !Netplay.Clients[client].IsConnected()) return;
			try { pkt.Send(client); } catch (Exception) { }
		}

		/// <summary>Runs on the machine that owns <paramref name="p"/>: raise life, show the number, tell the server.</summary>
		private static void ApplyHeal(Player p, int amount)
		{
			int gained = Math.Min(amount, Math.Max(0, p.statLifeMax2 - p.statLife));
			if (gained <= 0) return;
			p.statLife += gained;
			p.HealEffect(gained, true);
			if (Main.netMode == NetmodeID.MultiplayerClient && p.whoAmI == Main.myPlayer) NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, p.whoAmI);
		}

		/// <summary>Client side of the heal kind (payload after the kind byte: byte player, int amount).</summary>
		public static void OnHealPacket(BinaryReader r)
		{
			int who = r.ReadByte(); int amount = r.ReadInt32();
			if (who == Main.myPlayer && who < Main.maxPlayers) ApplyHeal(Main.player[who], amount);
		}
	}
}
