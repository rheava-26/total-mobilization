using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Net
{
	/// <summary>
	/// Packet types and their payloads (after the leading byte). Handlers live in the owning subsystem (Register from its
	/// ModSystem.Load); this file only routes. Senders and handlers MUST follow these layouts exactly.
	/// Client -> server:
	///  Enlist: int barracksId, byte grade                         (handler: Roster)
	///  Discharge: int soldierId                                   (Roster)
	///  Promote: int soldierId, byte grade                         (Roster)
	///  Demote: int soldierId, byte grade (the lower grade wanted)  (Roster) — a repeat of an applied demotion is a no-op, like Promote
	///  Reserve: int soldierId, bool toReserve                     (Roster) — true takes the soldier off active duty, false recalls him; the packet names the wanted state, so a repeat is a no-op
	///  Order: byte order (SoldierOrder, incl. SearchDestroy, FreeRein), bool atBarracks   (Roster) — commander = sender
	///  HoldFire: bool atBarracks                                  (Roster)
	///  SpreadOut: bool atBarracks                                 (Roster) — toggle, like HoldFire
	///  FocusTarget: int npcId (-1 clears)                         (Roster) — commander = sender; the squad prioritizes that hostile NPC
	///  FocusAttack: bool set, float worldX, float worldY          (Roster) — commander = sender; the squad attacks that position (and hostiles/critters near it); set=false clears
	///  Rally: (none)                                              (Roster)
	///  Rearm: (none)                                              (Roster) — commander = sender
	///  ResetLoadouts: (none)                                      (Roster) — commander = sender; every Regular returns its kit, loses all hand locks, and is re-issued from scratch
	///  Mobilize: int barracksId                                   (Militia)
	///  StandDown: int barracksId                                  (Militia)
	///  Deposit: int barracksId, long copper                       (Roster) — client already removed the coins
	///  Withdraw: int barracksId, long copper                      (Roster) — server deducts and spawns coins at the player
	///  RecruitingDrive: int barracksId                            (Militia)
	///  HazardPay: int barracksId                                  (Militia)
	///  ArmorySlotSet: int armoryId, byte region(0 storage,1 weapons,2 armor,3 ammo,4 potions), short slot, Item via ItemIO.Send(writeStack:true)  (UI)
	///  ArmorySort: int armoryId                                   (UI) — server merges and orders the storage, then re-syncs the tile entity
	///  ArmoryReforge: int armoryId, byte floorIndex               (UI) — server rerolls weapon modifiers to the floor, charging the linked barracks treasury per roll, then re-syncs both tile entities
	///  BarracksToggle: int barracksId, byte which(0 AutoMuster,1 TaxToTreasury), bool value   (Militia)
	///  HandAmmo: int npcIndex, int itemType, int count            (Roster) — client consumed the stack; server adds what fits, returns overflow
	///  AssignPatrol: int soldierId, short ax, short ay, short bx, short by   (Roster)
	///  FeedCrystal: int soldierId, int itemType                   (Roster) — client already consumed one item
	///  SetRebase: int soldierId, int barracksId                   (Roster)
	///  SoldierGearSet: int soldierId, byte slot(SoldierGear.Slot*), Item via ItemIO.Send(writeStack:true)   (Roster) — client moved the item out of its own hands (air = took the slot's item out); server validates, applies, locks/unlocks, re-syncs
	///  BookUsed: string bookKey                                   (Flavor) — client consumed the book; server sets the world flag
	///  OfficerCommand: byte action(0 commission,1 dismiss), int barracksId (-1 for dismiss), short npcIndex   (Officers) — commander = sender; server re-validates everything and charges the barracks treasury
	///  CompanionRequest: byte kind, then a payload the companion defines                  (Compat) — sender = the commander; the server re-validates every target
	/// Server -> client:
	///  RosterSnapshot: int count, then per record: SoldierRecord.WriteSnapshot   (Roster; also sent via ModSystem.NetSend on join)
	///  SettlementSnapshot: (unused: tile entities sync themselves via MessageID.TileEntitySharing)
	///  ArmorySnapshot: (unused: same)
	///  SoldierVisual: (unused: NPC SendExtraAI)
	///  Bark: short npcIndex, byte channel (DialogueChannel), byte emoteId, string text   (Dialogue)
	///  BossLifeMax: short npcIndex, int lifeMax                  (Boss) — vanilla syncs life but not lifeMax
	///  MutinyRob: long copper, int damage, int hitDirection, string soldierName   (Mutiny) — to the robbed player only; the client takes up to that much coin from its own pockets and banks, then takes the hit
	///  CompanionEffect: byte kind, then a payload the companion defines                    (Compat) — to one client; Levy only routes it to the handler the companion registered
	/// </summary>
	public enum LevyPacket : byte
	{
		Enlist = 1, Discharge, Promote, Order, HoldFire, Mobilize, StandDown, Rally, Deposit, Withdraw, RecruitingDrive, HazardPay,
		ArmorySlotSet, BarracksToggle, HandAmmo, AssignPatrol, FeedCrystal, SetRebase, BookUsed, Rearm, SoldierGearSet, SpreadOut, FocusTarget, ArmorySort, FocusAttack, ArmoryReforge, ResetLoadouts, OfficerCommand,
		Demote, Reserve,
		CompanionRequest, // client -> server: a Thorium cast the local player just made, for the companion to lay on that player's soldiers; its own handler decodes the payload
		RosterSnapshot = 100, SettlementSnapshot, ArmorySnapshot, SoldierVisual, Bark, BossLifeMax, MutinyRob,
		CompanionEffect, // server -> one client: an effect a companion mod (Thorium compat) lays on a player; its own handler decodes the payload
	}

	public static class LevyPackets
	{
		public static ModPacket New(LevyPacket type) { var p = Levy.Instance.GetPacket(); p.Write((byte)type); return p; }

		/// <summary>Routes to subsystem handlers. Subsystems register with <see cref="Register"/> from their ModSystem.Load.</summary>
		public static void Handle(Mod mod, BinaryReader reader, int whoAmI)
		{
			var type = (LevyPacket)reader.ReadByte();
			if (_handlers.TryGetValue(type, out var h)) h(reader, whoAmI);
			else mod.Logger.Warn($"Levy: unhandled packet {type}");
		}

		public delegate void Handler(BinaryReader reader, int fromWhoAmI);
		private static readonly System.Collections.Generic.Dictionary<LevyPacket, Handler> _handlers = new();
		public static void Register(LevyPacket type, Handler handler) => _handlers[type] = handler;
		public static void Clear() => _handlers.Clear();
	}
}
