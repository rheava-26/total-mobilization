using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.UI;
using Levy.Common.Util;
using Levy.Common.Vanity;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// The two armory edits that carry a whole structure: a targeted reforge run (<see cref="ReforgePlan"/>) and the vanity list (<see cref="ArmoryVanity"/>). Single player calls the armory directly;
	/// a multiplayer client sends the bytes and the server re-reads, clamps and applies them, after checking the sender stands near the armory (the same rule as every other armory edit).
	/// </summary>
	public class ArmoryEditNet : ModSystem
	{
		public override void Load()
		{
			LevyPackets.Register(LevyPacket.ArmoryReforgePlan, OnReforgePlan);
			LevyPackets.Register(LevyPacket.ArmoryVanitySet, OnVanitySet);
		}

		public override void OnWorldUnload() => VanityLookup.Reset();

		// ------------------------------------------------------------------ client side

		/// <summary>Runs a targeted reforge: single player directly (chat report), a multiplayer client asks the server.</summary>
		public static void RunReforge(ArmoryTE te, ReforgePlan plan)
		{
			if (te == null || plan == null || !UIActions.IsLive(te)) return;
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				ArmoryTE.ReportTargeted(Main.LocalPlayer, te.ReforgeTargeted(te.LinkedBarracks(), plan), plan);
				return;
			}
			var bytes = plan.ToBytes();
			var p = LevyPackets.New(LevyPacket.ArmoryReforgePlan); p.Write(te.ID); p.Write(bytes.Length); p.Write(bytes); p.Send();
		}

		/// <summary>The vanity list was edited in place: single player only queues a sync; a multiplayer client sends the whole list to the server.</summary>
		public static void SendVanity(ArmoryTE te)
		{
			if (te == null || !UIActions.IsLive(te)) return;
			if (Main.netMode == NetmodeID.SinglePlayer) { te.MarkDirty(); return; }
			var bytes = te.Vanity.ToBytes();
			var p = LevyPackets.New(LevyPacket.ArmoryVanitySet); p.Write(te.ID); p.Write(bytes.Length); p.Write(bytes); p.Send();
		}

		// ------------------------------------------------------------------ server side

		private static ArmoryTE NearbyArmory(int armoryId, int fromWhoAmI, out Player sender)
		{
			sender = null;
			if (Main.netMode != NetmodeID.Server) return null;
			if (!TileEntity.ByID.TryGetValue(armoryId, out var te) || te is not ArmoryTE armory) return null;
			if (fromWhoAmI < 0 || fromWhoAmI >= Main.maxPlayers || !Main.player[fromWhoAmI].active) return null;
			sender = Main.player[fromWhoAmI];
			return LevyUtil.WithinTiles(sender.Center, armory.Center, LevyConstants.UI.ArmoryEditRangeTiles) ? armory : null;
		}

		/// <summary>Server: a client asked for a targeted reforge. Payload: int armoryId, int length, plan bytes. The linked barracks treasury pays per roll.</summary>
		private static void OnReforgePlan(BinaryReader r, int whoAmI)
		{
			int armoryId = r.ReadInt32(); int len = r.ReadInt32();
			byte[] bytes = len > 0 && len <= 8 * 1024 ? r.ReadBytes(len) : null;
			var armory = NearbyArmory(armoryId, whoAmI, out var sender);
			if (armory == null || bytes == null) return;
			var plan = ReforgePlan.FromBytes(bytes);
			var payer = armory.LinkedBarracks();
			var result = armory.ReforgeTargeted(payer, plan);
			if (result.Rolls > 0)
			{
				armory.Sync();
				RosterSystem.SyncBarracks(payer);
			}
			ArmoryTE.ReportTargeted(sender, result, plan);
		}

		/// <summary>Server: a client sent its edited vanity list. Payload: int armoryId, int length, bytes.</summary>
		private static void OnVanitySet(BinaryReader r, int whoAmI)
		{
			int armoryId = r.ReadInt32(); int len = r.ReadInt32();
			byte[] bytes = len > 0 && len <= ArmoryVanity.MaxWireBytes ? r.ReadBytes(len) : null;
			var armory = NearbyArmory(armoryId, whoAmI, out _);
			if (armory == null || bytes == null) return;
			armory.SetVanity(ArmoryVanity.FromBytes(bytes));
			armory.Sync();
		}
	}
}
