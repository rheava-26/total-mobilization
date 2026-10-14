using System.IO;
using Terraria;
using Terraria.ID;
using Levy.Common.Net;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Client -> server packets owned by the settlement module (payload layouts in <see cref="LevyPackets"/>). Every handler
	/// reads its whole payload first, then validates that it runs on the server, that the sender is an active player and
	/// that the barracks still exists. Clients ignore these packets.
	/// </summary>
	internal static class SettlementNet
	{
		public static void Register()
		{
			LevyPackets.Register(LevyPacket.Mobilize, HandleMobilize);
			LevyPackets.Register(LevyPacket.StandDown, HandleStandDown);
			LevyPackets.Register(LevyPacket.RecruitingDrive, HandleRecruitingDrive);
			LevyPackets.Register(LevyPacket.HazardPay, HandleHazardPay);
			LevyPackets.Register(LevyPacket.BarracksToggle, HandleBarracksToggle);
		}

		private static void HandleMobilize(BinaryReader reader, int from)
		{
			int id = reader.ReadInt32();
			if (!Validate(from, id, out var player, out var barracks)) return;
			SettlementSystem.Instance.Mobilize(barracks, player);
		}

		private static void HandleStandDown(BinaryReader reader, int from)
		{
			int id = reader.ReadInt32();
			if (!Validate(from, id, out _, out var barracks)) return;
			SettlementSystem.Instance.StandDown(barracks);
		}

		private static void HandleRecruitingDrive(BinaryReader reader, int from)
		{
			int id = reader.ReadInt32();
			if (!Validate(from, id, out var player, out var barracks)) return;
			SettlementSystem.Instance.RecruitingDrive(barracks, player);
		}

		private static void HandleHazardPay(BinaryReader reader, int from)
		{
			int id = reader.ReadInt32();
			if (!Validate(from, id, out var player, out var barracks)) return;
			SettlementSystem.Instance.HazardPay(barracks, player);
		}

		private static void HandleBarracksToggle(BinaryReader reader, int from)
		{
			int id = reader.ReadInt32();
			byte which = reader.ReadByte();
			bool value = reader.ReadBoolean();
			if (!Validate(from, id, out _, out var barracks)) return;
			switch (which)
			{
				case 0: barracks.AutoMuster = value; break;
				case 1: barracks.TaxToTreasury = value; break;
				default: return;
			}
			SettlementSystem.SyncTE(barracks);
		}

		/// <summary>Server only; the sender must be an active player and the barracks must still stand.</summary>
		private static bool Validate(int from, int barracksId, out Player player, out BarracksTE barracks)
		{
			player = null; barracks = null;
			if (Main.netMode != NetmodeID.Server) return false;
			if (from < 0 || from >= Main.maxPlayers || !Main.player[from].active) return false;
			barracks = SettlementSystem.Instance.GetBarracks(barracksId);
			if (barracks == null) return false;
			player = Main.player[from];
			return true;
		}
	}
}
