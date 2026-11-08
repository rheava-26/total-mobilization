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
	///  ArmoryRulesSet: int armoryId, int length, byte[] rules (ArmoryRules.ToBytes)  (UI) — server re-reads and clamps the list, replaces the armory's rules, then re-syncs the tile entity
	///  ArmoryReforgePlan: int armoryId, int length, byte[] plan (ReforgePlan.ToBytes)  (UI) — server re-reads and clamps the plan, runs the targeted reforge, charges the linked barracks treasury, re-syncs both tile entities
	///  ArmoryVanitySet: int armoryId, int length, byte[] vanity (ArmoryVanity.ToBytes)  (UI) — server re-reads and clamps the list, replaces the armory's vanity sets, then re-syncs the tile entity
	///  BarracksToggle: int barracksId, byte which(0 AutoMuster,1 TaxToTreasury), bool value   (Militia)
	///  HandAmmo: int npcIndex, int itemType, int count            (Roster) — client consumed the stack; server adds what fits, returns overflow
	///  AssignPatrol: int soldierId, short ax, short ay, short bx, short by   (Roster)
	///  FeedCrystal: int soldierId, int itemType                   (Roster) — client already consumed one item
	///  SetRebase: int soldierId, int barracksId                   (Roster)
	///  SoldierGearSet: int soldierId, byte slot(SoldierGear.Slot*), Item via ItemIO.Send(writeStack:true)   (Roster) — client moved the item out of its own hands (air = took the slot's item out); server validates, applies, locks/unlocks, re-syncs
	///  BookUsed: string bookKey                                   (Flavor) — client consumed the book; server sets the world flag
	///  OfficerCommand: byte action(0 commission,1 dismiss), int barracksId (-1 for dismiss), short npcIndex   (Officers) — commander = sender; server re-validates everything and charges the barracks treasury
	///  SoldierDyeSet: int soldierId, byte dyeSlot(0..SoldierGear.DyeSlots-1), Item via ItemIO.Send(writeStack:true)   (Roster) — client moved the dye out of its own hands (air = took the dye out); server validates, applies, re-syncs. Dyes are never locked: the Armory never touches them
	///  TalkCall: int soldierId                                    (Roster) — commander = sender; the soldier walks to the sender so a chat can open (a levy in a fight is out of vanilla's chat reach)
	///  CompanionRequest: byte kind, then a payload the companion defines                  (Compat) — sender = the commander; the server re-validates every target
	///  LogisticsOrderEdit / LogisticsSell / LogisticsBuy: layouts in the doc comment of LogisticsActions   (Logistics)
	///  MapOrder: int tileX, int tileY                              (Observe) — commander = sender; every Regular marches to that tile, then holds it
	///  ObserveRequest: byte kind(0 stop,1 camera,2 GoPro levy,3 list cameras), int id   (Observe) — the server then streams sections (RemoteClient.CheckSection) and the target position
	///  MindControlSet: bool on, int soldierId, bool viaDeath       (Observe) — server validates the levy is the sender's; without death it needs a Mind Link in the inventory
	///  MindInput: byte keys(1 left,2 right,4 jump,8 down,16 use,32 alt), float aimX, float aimY   (Observe) — sent when it changes or every 10 ticks
	/// Server -> client:
	///  LogisticsSnapshot: OrderBook.WriteBook   (Logistics) — the whole order book, with each order's status and materials checklist
	///  RosterSnapshot: int count, then per record: SoldierRecord.WriteSnapshot   (Roster; also sent via ModSystem.NetSend on join)
	///  SettlementSnapshot: (unused: tile entities sync themselves via MessageID.TileEntitySharing)
	///  ArmorySnapshot: (unused: same)
	///  SoldierVisual: (unused: NPC SendExtraAI)
	///  Bark: short npcIndex, byte channel (DialogueChannel), byte emoteId, string text   (Dialogue)
	///  BossLifeMax: short npcIndex, int lifeMax                  (Boss) — vanilla syncs life but not lifeMax
	///  MutinyRob: long copper, int damage, int hitDirection, string soldierName   (Mutiny) — to the robbed player only; the client takes up to that much coin from its own pockets and banks, then takes the hit
	///  CompanionEffect: byte kind, then a payload the companion defines                    (Compat) — to one client; Levy only routes it to the handler the companion registered
	///  ObserveList: int count, then per camera: int id, short tileX, short tileY, string label   (Observe)
	///  ObservePos: byte kind(1 camera,2 levy,3 mind), int id, float x, float y   (Observe) — the watched target's world position, every 6 ticks
	///  TeamEffect / TeamRequest: byte kind, then a payload   (Team, see TeamNet) — server -> one client: a team-item effect on the player; client -> server: a team-item reaction the player's own client owns
	/// </summary>
	public enum LevyPacket : byte
	{
		Enlist = 1, Discharge, Promote, Order, HoldFire, Mobilize, StandDown, Rally, Deposit, Withdraw, RecruitingDrive, HazardPay,
		ArmorySlotSet, BarracksToggle, HandAmmo, AssignPatrol, FeedCrystal, SetRebase, BookUsed, Rearm, SoldierGearSet, SpreadOut, FocusTarget, ArmorySort, FocusAttack, ArmoryReforge, ResetLoadouts, OfficerCommand,
		Demote, Reserve,
		CompanionRequest, // client -> server: a Thorium cast the local player just made, for the companion to lay on that player's soldiers; its own handler decodes the payload
		ArmoryRulesSet, // client -> server: the armory's whole rule list (see ArmoryRules.ToBytes); appended here so no existing value moves
		SoldierDyeSet, // client -> server: hand-place or take a soldier's vanity dye (see above); appended so no existing value moves
		TalkCall, // client -> server: call a levy over to talk (see above); appended so no existing value moves
		ArmoryReforgePlan, // client -> server: run a targeted reforge (see ReforgePlan.ToBytes)
		ArmoryVanitySet, // client -> server: the armory's whole vanity list (see ArmoryVanity.ToBytes)
		MapOrder, ObserveRequest, MindControlSet, MindInput, // client -> server: Command & Observe (Common/Observe)
		DrinkRound, // client -> server: byte drink(0 Ale,1 Stout,2 Rum) - the server serves the round to the sender's levies in range
		MeteorBeacon, // client -> server: no payload - the server drops a meteor if it is night and the sender holds a beacon use
		RosterSnapshot = 100, SettlementSnapshot, ArmorySnapshot, SoldierVisual, Bark, BossLifeMax, MutinyRob,
		CompanionEffect, // server -> one client: an effect a companion mod (Thorium compat) lays on a player; its own handler decodes the payload
		LogisticsOrderEdit, // client -> server: save / delete / pause / resume a craft order (see LogisticsActions)
		LogisticsSell, // client -> server: mass-sell armory storage slots or a filter
		LogisticsBuy, // client -> server: run one standing buy rule now
		LogisticsSnapshot, // server -> client: the whole order book (OrderBook.WriteBook)
		ObserveList, ObservePos, // server -> one client: Command & Observe (camera list, followed-levy position)
		LogisticsCraft, // client -> server: craft one recipe now from the sender's inventory, nearby chests and the armory stock (see LogisticsActions)
		TeamEffect, TeamRequest, // server -> one client / client -> server: team items (Common/Team, see TeamNet); appended so no existing value moves
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
		/// <summary>True when a handler is registered for this packet type (boot self-checks).</summary>
		public static bool IsRegistered(LevyPacket type) => _handlers.ContainsKey(type);
	}
}
