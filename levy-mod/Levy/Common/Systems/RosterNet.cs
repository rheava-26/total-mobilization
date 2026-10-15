using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Packet handlers for the roster (registered from RosterSystem.Load). Payload layouts follow LevyPackets exactly.
	/// Every client→server handler reads its whole payload first, then validates the sender: the sender must be the
	/// record's commander (any player may enlist). RosterSnapshot is the only server→client packet handled here.
	/// </summary>
	internal static class RosterNet
	{
		public static void Register()
		{
			LevyPackets.Register(LevyPacket.Enlist, OnEnlist);
			LevyPackets.Register(LevyPacket.Discharge, OnDischarge);
			LevyPackets.Register(LevyPacket.Promote, OnPromote);
			LevyPackets.Register(LevyPacket.Order, OnOrder);
			LevyPackets.Register(LevyPacket.HoldFire, OnHoldFire);
			LevyPackets.Register(LevyPacket.SpreadOut, OnSpreadOut);
			LevyPackets.Register(LevyPacket.FocusTarget, OnFocusTarget);
			LevyPackets.Register(LevyPacket.Rally, OnRally);
			LevyPackets.Register(LevyPacket.Rearm, OnRearm);
			LevyPackets.Register(LevyPacket.Deposit, OnDeposit);
			LevyPackets.Register(LevyPacket.Withdraw, OnWithdraw);
			LevyPackets.Register(LevyPacket.HandAmmo, OnHandAmmo);
			LevyPackets.Register(LevyPacket.AssignPatrol, OnAssignPatrol);
			LevyPackets.Register(LevyPacket.FeedCrystal, OnFeedCrystal);
			LevyPackets.Register(LevyPacket.SetRebase, OnSetRebase);
			LevyPackets.Register(LevyPacket.SoldierGearSet, OnSoldierGearSet);
			LevyPackets.Register(LevyPacket.RosterSnapshot, OnRosterSnapshot);
		}

		private static RosterSystem Roster => RosterSystem.Instance;

		private static Player Sender(int whoAmI) => whoAmI >= 0 && whoAmI < Main.maxPlayers && Main.player[whoAmI].active ? Main.player[whoAmI] : null;

		/// <summary>The record if the sender commands it (server), else null.</summary>
		private static SoldierRecord Owned(int soldierId, Player sender)
		{
			if (sender == null) return null;
			var rec = Roster.FindById(soldierId);
			return rec != null && rec.CommanderName == sender.name ? rec : null;
		}

		private static void OnEnlist(BinaryReader r, int whoAmI)
		{
			int barracksId = r.ReadInt32(); byte grade = r.ReadByte();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender == null || grade >= LevyConstants.GradeCpCost.Length) return;
			Roster.Enlist(SettlementSystem.Instance.GetBarracks(barracksId), sender, (Grade)grade);
		}

		private static void OnDischarge(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32();
			if (!LevyUtil.IsServer) return;
			var rec = Owned(soldierId, Sender(whoAmI));
			if (rec != null) Roster.Discharge(rec);
		}

		private static void OnPromote(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32(); byte grade = r.ReadByte();
			if (!LevyUtil.IsServer) return;
			var rec = Owned(soldierId, Sender(whoAmI));
			if (rec != null && grade < LevyConstants.GradeCpCost.Length) Roster.Promote(rec, (Grade)grade);
		}

		private static void OnOrder(BinaryReader r, int whoAmI)
		{
			byte order = r.ReadByte(); bool atBarracks = r.ReadBoolean();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender == null || order > (byte)SoldierOrder.FreeRein) return;
			Roster.IssueOrder(sender, (SoldierOrder)order, atBarracks);
		}

		private static void OnHoldFire(BinaryReader r, int whoAmI)
		{
			bool atBarracks = r.ReadBoolean();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender != null) Roster.ToggleHoldFire(sender, atBarracks);
		}

		private static void OnSpreadOut(BinaryReader r, int whoAmI)
		{
			bool atBarracks = r.ReadBoolean();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender != null) Roster.ToggleSpreadOut(sender, atBarracks);
		}

		private static void OnFocusTarget(BinaryReader r, int whoAmI)
		{
			int npcId = r.ReadInt32();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender != null) Roster.SetFocusTarget(sender, npcId);
		}

		private static void OnRally(BinaryReader r, int whoAmI)
		{
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender != null) Roster.Rally(sender);
		}

		private static void OnRearm(BinaryReader r, int whoAmI)
		{
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender != null) Roster.Rearm(sender);
		}

		/// <summary>The client already removed the coins from its own inventory; the server only credits the treasury.</summary>
		private static void OnDeposit(BinaryReader r, int whoAmI)
		{
			int barracksId = r.ReadInt32(); long copper = r.ReadInt64();
			if (!LevyUtil.IsServer || Sender(whoAmI) == null || copper <= 0) return;
			var te = SettlementSystem.Instance.GetBarracks(barracksId);
			if (te == null) return;
			te.Treasury += copper;
			RosterSystem.SyncBarracks(te);
		}

		private static void OnWithdraw(BinaryReader r, int whoAmI)
		{
			int barracksId = r.ReadInt32(); long copper = r.ReadInt64();
			if (!LevyUtil.IsServer || copper <= 0) return;
			var sender = Sender(whoAmI);
			if (sender == null) return;
			if (!Roster.Withdraw(sender, SettlementSystem.Instance.GetBarracks(barracksId), copper)) RosterSystem.SendToPlayer(sender, LevyUtil.T("UI.NotEnoughMoney"));
		}

		/// <summary>Moves the ammo stack from the sender's inventory slot into the soldier's pouch, then re-syncs that slot to everyone.</summary>
		private static void OnHandAmmo(BinaryReader r, int whoAmI)
		{
			int npcIndex = r.ReadInt32(); int type = r.ReadInt32(); int count = r.ReadInt32();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender == null || npcIndex < 0 || npcIndex >= Main.maxNPCs || type <= 0 || count <= 0) return;
			var npc = Main.npc[npcIndex];
			if (!npc.active || npc.ModNPC is not SoldierNPC body || body.Record == null) return;
			// the client already consumed the stack; add what fits and hand any overflow back to the sender
			int left = body.GiveAmmo(type, count);
			if (left > 0) sender.QuickSpawnItem(sender.GetSource_Misc("Levy:HandAmmo"), type, left);
		}

		private static void OnAssignPatrol(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32(); short ax = r.ReadInt16(), ay = r.ReadInt16(), bx = r.ReadInt16(), by = r.ReadInt16();
			if (!LevyUtil.IsServer) return;
			var rec = Owned(soldierId, Sender(whoAmI));
			if (rec != null) Roster.AssignPatrol(rec, new Point16(ax, ay), new Point16(bx, by));
		}

		/// <summary>The client already consumed one item; if the soldier cannot use it the server hands it back.</summary>
		private static void OnFeedCrystal(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32(); int itemType = r.ReadInt32();
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			var rec = Owned(soldierId, sender);
			if (rec == null || itemType <= 0) return;
			if (Roster.FeedCrystal(rec, itemType)) return;
			if (itemType == ItemID.LifeCrystal || itemType == ItemID.LifeFruit || itemType == ItemID.ManaCrystal)
			{
				sender.QuickSpawnItem(sender.GetSource_Misc("Levy:CrystalRefused"), itemType, 1);
				RosterSystem.SendToPlayer(sender, LevyUtil.T("Roster.CrystalRefused", rec.Name));
			}
		}

		private static void OnSetRebase(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32(); int barracksId = r.ReadInt32();
			if (!LevyUtil.IsServer) return;
			var rec = Owned(soldierId, Sender(whoAmI));
			if (rec != null) Roster.Rebase(rec, SettlementSystem.Instance.GetBarracks(barracksId));
		}

		/// <summary>
		/// Hand-equip from a client. The client already took the item out of its own hands, so anything the server refuses (not the
		/// commander, wrong slot, wrong kind of item) goes straight back to the sender rather than vanishing.
		/// </summary>
		private static void OnSoldierGearSet(BinaryReader r, int whoAmI)
		{
			int soldierId = r.ReadInt32(); byte slot = r.ReadByte();
			Item item = ItemIO.Receive(r, readStack: true);
			if (!LevyUtil.IsServer) return;
			var sender = Sender(whoAmI);
			if (sender == null) return;
			var rec = Owned(soldierId, sender);
			Item extra = null;
			bool ok = rec != null && ApplyGearSlot(rec, slot, item, out extra);
			if (!ok) Return(sender, item);
			else Return(sender, extra);
		}

		private static void Return(Player sender, Item it)
		{
			if (sender == null || it == null || it.IsAir || it.stack <= 0) return;
			sender.QuickSpawnItem(sender.GetSource_Misc("Levy:GearRefused"), it, it.stack);
		}

		/// <summary>
		/// Puts <paramref name="item"/> (or air, to empty the slot) into one of a soldier's gear slots and locks the slot against
		/// auto-issue while it holds something. A placement also lets the Armory fill what is still empty (e.g. ammo for a new bow).
		/// Used by the server handler and by single-player UIActions. <paramref name="overflow"/> is ammo above the pouch limit.
		/// </summary>
		internal static bool ApplyGearSlot(SoldierRecord rec, int slot, Item item, out Item overflow)
		{
			overflow = null;
			if (rec == null || rec.Gear == null) return false;
			if (!rec.Gear.TrySet(slot, item, rec.AccessorySlots, out overflow)) return false;
			if (!rec.Gear.Get(slot).IsAir)
			{
				var home = SettlementSystem.Instance.GetBarracks(rec.HomeBarracksId);
				var armory = home == null ? null : SettlementSystem.Instance.LinkedArmory(home);
				armory?.IssueGear(rec);
			}
			if (rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is SoldierNPC body) body.NotifyGearChanged();
			if (LevyUtil.IsServer) Roster.SendRosterSnapshot();
			return true;
		}

		private static void OnRosterSnapshot(BinaryReader r, int whoAmI)
		{
			if (!LevyUtil.IsClient) return;
			Roster.ReadRoster(r);
		}
	}
}
