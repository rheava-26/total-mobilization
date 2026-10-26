using System.IO;
using Microsoft.Xna.Framework;
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
		/// <summary>Client asks the server to set a turret down. Payload: float x, float y (the cursor spot).</summary>
		Place = 1,
		/// <summary>Client asks the server to recall or launch drones from a drone's chat panel. Payload: byte <see cref="DroneOrder"/>, int NPC slot.</summary>
		DroneCommand = 2,
		/// <summary>Client pressed the recall / launch key: the server acts on the Barracks nearest that player. No payload.</summary>
		DroneKey = 3,
	}

	/// <summary>What a drone order asks for.</summary>
	public enum DroneOrder : byte
	{
		/// <summary>This drone flies home, lands and files its weapon back.</summary>
		Recall = 0,
		/// <summary>This (parked) drone is armed with the best spare weapon and sent up.</summary>
		Launch = 1,
		/// <summary>Every drone of this drone's Barracks is recalled.</summary>
		RecallAll = 2,
		/// <summary>Every parked drone of this drone's Barracks is armed and sent up.</summary>
		LaunchAll = 3,
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

		/// <summary>Client: ask the server to set a turret down at world position <paramref name="spot"/> (it validates, places it, and refunds the item if there is no room).</summary>
		public static void SendPlace(Vector2 spot)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			ModPacket p = Mechanization.Instance.GetPacket();
			p.Write((byte)MechPacket.Place);
			p.Write(spot.X);
			p.Write(spot.Y);
			p.Send();
		}

		/// <summary>Client: ask the server to carry out a drone order on the drone in NPC slot <paramref name="npcIndex"/> (or on its whole Barracks).</summary>
		public static void SendDroneOrder(DroneOrder order, int npcIndex)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			ModPacket p = Mechanization.Instance.GetPacket();
			p.Write((byte)MechPacket.DroneCommand);
			p.Write((byte)order);
			p.Write(npcIndex);
			p.Send();
		}

		/// <summary>Client: the recall / launch key was pressed; the server acts on the Barracks nearest this player.</summary>
		public static void SendDroneKey()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			ModPacket p = Mechanization.Instance.GetPacket();
			p.Write((byte)MechPacket.DroneKey);
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
				case MechPacket.Place:
					float x = reader.ReadSingle(), y = reader.ReadSingle();
					if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers) return;
					TurretSystem.Instance?.PlaceFor(Main.player[whoAmI], new Vector2(x, y));
					break;
				case MechPacket.DroneCommand:
					byte order = reader.ReadByte();
					int slot = reader.ReadInt32();
					if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers || order > (byte)DroneOrder.LaunchAll) return;
					DroneSystem.Instance?.HandleOrder(Main.player[whoAmI], (DroneOrder)order, slot);
					break;
				case MechPacket.DroneKey:
					if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers) return;
					DroneSystem.Instance?.HandleKey(Main.player[whoAmI]);
					break;
			}
		}
	}
}
