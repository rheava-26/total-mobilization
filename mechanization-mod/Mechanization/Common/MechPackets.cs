using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Mechanization.Content.NPCs;

namespace Mechanization.Common
{
	/// <summary>Mechanization's few client-to-server requests. A packet is a type byte then its payload; the server validates everything.</summary>
	public enum MechPacket : byte
	{
		/// <summary>Client asks the server to dismantle a turret. Payload: int NPC slot.</summary>
		Dismantle = 0,
	}

	public static class MechPackets
	{
		/// <summary>Client: ask the server to pick up the turret in NPC slot <paramref name="npcIndex"/> and hand it to this player.</summary>
		public static void SendDismantle(int npcIndex)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			ModPacket p = Mechanization.Instance.GetPacket();
			p.Write((byte)MechPacket.Dismantle);
			p.Write(npcIndex);
			p.Send();
		}

		/// <summary>Server: handles one request. <paramref name="whoAmI"/> is the sending player's slot.</summary>
		public static void Handle(BinaryReader reader, int whoAmI)
		{
			var type = (MechPacket)reader.ReadByte();
			switch (type)
			{
				case MechPacket.Dismantle:
					int idx = reader.ReadInt32();
					if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers) return;
					if (idx < 0 || idx >= Main.maxNPCs) return;
					TurretSystem.Instance?.Dismantle(Main.npc[idx], Main.player[whoAmI]);
					break;
			}
		}
	}
}
