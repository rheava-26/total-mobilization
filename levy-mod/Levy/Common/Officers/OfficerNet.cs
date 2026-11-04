using System.IO;
using Terraria;
using Terraria.ID;
using Levy.Common.Net;
using Levy.Common.Systems;

namespace Levy.Common.Officers
{
	/// <summary>
	/// Client -> server packet of the officer module (layout in <see cref="LevyPackets"/>): <c>OfficerCommand</c>. The whole payload is read first, then the sender
	/// must be an active player and the officer system re-validates the request from scratch (type, slot, house, range, treasury): the client's idea of any of
	/// that is never trusted, and the fee is taken from the barracks treasury on the server, so the menu cannot be used to create money.
	/// </summary>
	internal static class OfficerNet
	{
		public const byte ActionCommission = 0, ActionDismiss = 1;

		public static void Register() => LevyPackets.Register(LevyPacket.OfficerCommand, Handle);

		private static void Handle(BinaryReader reader, int from)
		{
			byte action = reader.ReadByte();
			int barracksId = reader.ReadInt32();
			short npcIndex = reader.ReadInt16();
			if (Main.netMode != NetmodeID.Server) return;
			if (from < 0 || from >= Main.maxPlayers || !Main.player[from].active) return;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
			Player player = Main.player[from];
			NPC npc = Main.npc[npcIndex];
			switch (action)
			{
				case ActionCommission:
					OfficerSystem.Instance.Commission(player, SettlementSystem.Instance.GetBarracks(barracksId), npc);
					break;
				case ActionDismiss:
					OfficerSystem.Instance.Dismiss(player, npc);
					break;
			}
		}
	}
}
