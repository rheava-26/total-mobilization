using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Settlements: barracks enumeration, town-NPC membership, manpower cap/regen, militia muster/stand-down/reinforcements,
	/// auto-muster on events, event-book flags, settlement packets. OWNED BY THE MILITIA AGENT. Public surface is the contract.
	/// Logic runs where <c>Main.netMode != MultiplayerClient</c>; clients may only call the read members (enumeration,
	/// membership, caps, names). Militia records live only while mustered and are never saved.
	/// </summary>
	public class SettlementSystem : ModSystem
	{
		public static SettlementSystem Instance => ModContent.GetInstance<SettlementSystem>();

		/// <summary>Runtime state per barracks that <see cref="BarracksTE"/> has no field for. Keyed by tile-entity ID; never saved.</summary>
		private sealed class Runtime
		{
			public bool HasNpcs;
			public uint NpcStamp;
			public readonly List<NPC> Npcs = new();
			/// <summary>A record whose gear and fee are already paid but whose body hit the render cap. Retried every tick, never duplicated.</summary>
			public SoldierRecord PendingSpawn;
			/// <summary>Who blew the horn: loot from the militia credits this player.</summary>
			public string MobilizerName = "";
			/// <summary>Bodies walking home after StandDown and the tick at which each is dismissed where it stands.</summary>
			public readonly List<(SoldierRecord rec, uint deadline)> StandingDown = new();
		}

		private readonly Dictionary<int, Runtime> _rt = new();
		private readonly List<int> _scratchIds = new();
		private readonly HashSet<int> _dirtyArmories = new();
		private static readonly List<NPC> _none = new();
		/// <summary>Militia ids count down from -1 so they never collide with persistent regular ids.</summary>
		private int _nextMilitiaId = -1;
		private bool _eventActive, _lastEventActive;
		private uint _eventTick = uint.MaxValue;
		private int _housekeepingTimer;

		// ---- world flags set by event books (persisted here) ----
		public bool BookConscription, BookDrill, BookPlunder, BookFortification, BookDiscipline;

		// =====================================================================================================
		// Lifecycle
		// =====================================================================================================
		public override void Load() => SettlementNet.Register();
		public override void OnWorldLoad() => ResetRuntime();
		public override void OnWorldUnload() => ResetRuntime();

		public override void ClearWorld()
		{
			BookConscription = BookDrill = BookPlunder = BookFortification = BookDiscipline = false;
			ResetRuntime();
		}

		private void ResetRuntime()
		{
			_rt.Clear(); _dirtyArmories.Clear(); _scratchIds.Clear();
			_nextMilitiaId = -1;
			_lastEventActive = false; _eventTick = uint.MaxValue;
			_housekeepingTimer = 0;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["bookConscription"] = BookConscription; tag["bookDrill"] = BookDrill; tag["bookPlunder"] = BookPlunder;
			tag["bookFortification"] = BookFortification; tag["bookDiscipline"] = BookDiscipline;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			BookConscription = tag.GetBool("bookConscription"); BookDrill = tag.GetBool("bookDrill"); BookPlunder = tag.GetBool("bookPlunder");
			BookFortification = tag.GetBool("bookFortification"); BookDiscipline = tag.GetBool("bookDiscipline");
		}

		/// <summary>Book flags ride along with WorldData so joining clients can draw caps and tooltips.</summary>
		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(BookConscription); writer.Write(BookDrill); writer.Write(BookPlunder); writer.Write(BookFortification); writer.Write(BookDiscipline);
		}

		public override void NetReceive(BinaryReader reader)
		{
			BookConscription = reader.ReadBoolean(); BookDrill = reader.ReadBoolean(); BookPlunder = reader.ReadBoolean();
			BookFortification = reader.ReadBoolean(); BookDiscipline = reader.ReadBoolean();
		}

		/// <summary>Sets an event-book flag; false if it was already set or the key is unknown (books are once per world).</summary>
		public virtual bool TrySetBook(string key)
		{
			if (!LevyUtil.IsServerOrSingle || string.IsNullOrEmpty(key)) return false;
			switch (key)
			{
				case "Conscription": if (BookConscription) return false; BookConscription = true; break;
				case "Drill": if (BookDrill) return false; BookDrill = true; break;
				case "Plunder": if (BookPlunder) return false; BookPlunder = true; break;
				case "Fortification": if (BookFortification) return false; BookFortification = true; break;
				case "Discipline": if (BookDiscipline) return false; BookDiscipline = true; break;
				default: return false;
			}
			// WorldData carries NetSend, so one resend pushes the new flag to every client without a packet of our own.
			if (LevyUtil.IsServer) NetMessage.SendData(MessageID.WorldData);
			return true;
		}

		// =====================================================================================================
		// Enumeration and membership (safe on clients)
		// =====================================================================================================
		public virtual IEnumerable<BarracksTE> AllBarracks() { foreach (var te in TileEntity.ByID.Values) if (te is BarracksTE b) yield return b; }
		public virtual BarracksTE GetBarracks(int id) => id >= 0 && TileEntity.ByID.TryGetValue(id, out var te) && te is BarracksTE b ? b : null;
		public virtual IEnumerable<ArmoryTE> ArmoriesNear(Vector2 worldPos, float tiles) { foreach (var te in TileEntity.ByID.Values) if (te is ArmoryTE a && Vector2.Distance(a.Center, worldPos) <= tiles * 16f) yield return a; }

		public virtual BarracksTE NearestBarracks(Vector2 worldPos, float maxTiles = float.MaxValue)
		{
			float limit = maxTiles * LevyConstants.TilePx; limit *= limit;
			BarracksTE best = null; float bestD = float.MaxValue;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not BarracksTE b) continue;
				float d = Vector2.DistanceSquared(b.Center, worldPos);
				if (d <= limit && d < bestD) { bestD = d; best = b; }
			}
			return best;
		}

		public virtual ArmoryTE NearestArmory(Vector2 worldPos, float maxTiles)
		{
			float limit = maxTiles * LevyConstants.TilePx; limit *= limit;
			ArmoryTE best = null; float bestD = float.MaxValue;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				float d = Vector2.DistanceSquared(a.Center, worldPos);
				if (d <= limit && d < bestD) { bestD = d; best = a; }
			}
			return best;
		}

		public virtual ArmoryTE LinkedArmory(BarracksTE barracks) => barracks == null ? null : NearestArmory(barracks.Center, LevyConstants.ArmoryLinkRadius);

		public virtual string TownName(BarracksTE barracks) => string.IsNullOrWhiteSpace(barracks?.TownName) ? LevyUtil.T("Militia.DefaultTownName") : barracks.TownName;

		/// <summary>
		/// Town NPCs that belong to this settlement. Cached for <see cref="LevyConstants.TownNpcCacheTicks"/>; treat the list as
		/// read-only. Excluded: pets, the Old Man, the Travelling Merchant and anyone homeless (bound NPCs are not town NPCs yet).
		/// </summary>
		public virtual List<NPC> TownNpcsOf(BarracksTE barracks)
		{
			if (barracks == null) return _none;
			var rt = RuntimeOf(barracks);
			uint now = Main.GameUpdateCount;
			if (!rt.HasNpcs || now < rt.NpcStamp || now - rt.NpcStamp >= LevyConstants.TownNpcCacheTicks) RefreshTownNpcs(barracks, rt, now);
			return rt.Npcs;
		}

		private static void RefreshTownNpcs(BarracksTE b, Runtime rt, uint now)
		{
			rt.Npcs.Clear(); rt.HasNpcs = true; rt.NpcStamp = now;
			int bx = b.Position.X, by = b.Position.Y;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || !n.townNPC || n.homeless) continue;
				if (n.type == NPCID.OldMan || n.type == NPCID.TravellingMerchant) continue;
				if (n.type >= 0 && n.type < NPCID.Sets.IsTownPet.Length && NPCID.Sets.IsTownPet[n.type]) continue;
				if (Math.Abs(n.homeTileX - bx) > LevyConstants.SettlementRadiusX || Math.Abs(n.homeTileY - by) > LevyConstants.SettlementRadiusY) continue;
				if (!ReferenceEquals(NearestBarracksToTile(n.homeTileX, n.homeTileY), b)) continue;
				rt.Npcs.Add(n);
			}
		}

		/// <summary>An NPC belongs to exactly one settlement: the barracks nearest its home tile (ties go to the lower ID).</summary>
		private static BarracksTE NearestBarracksToTile(int tx, int ty)
		{
			BarracksTE best = null; long bestD = long.MaxValue;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not BarracksTE b) continue;
				long dx = tx - (b.Position.X + 1), dy = ty - (b.Position.Y + 1);
				long d = dx * dx + dy * dy;
				if (d < bestD || (d == bestD && best != null && b.ID < best.ID)) { bestD = d; best = b; }
			}
			return best;
		}

		/// <summary>Manpower ceiling from the town-NPC count. On the server also refreshes the cached cap and clamps the pool when the cap drops.</summary>
		public virtual int ManpowerCap(BarracksTE barracks)
		{
			if (barracks == null) return 0;
			int count = TownNpcsOf(barracks).Count;
			int cap = (BookConscription ? LevyConstants.ManpowerPerNpcConscription : LevyConstants.ManpowerPerNpc) * count;
			if (LevyUtil.IsServerOrSingle)
			{
				bool changed = barracks.ManpowerCapCached != cap;
				barracks.ManpowerCapCached = cap;
				if (barracks.Manpower > cap) { barracks.Manpower = cap; changed = true; }
				if (barracks.Manpower < 0) { barracks.Manpower = 0; changed = true; }
				if (changed) SyncTE(barracks);
			}
			return cap;
		}

		public virtual bool CanRaiseMilitia(BarracksTE barracks)
			=> barracks != null && !barracks.Mobilized && barracks.Manpower > 0 && TownNpcsOf(barracks).Count >= LevyConstants.MinTownNpcsForMilitia;

		/// <summary>True while any event the militia answers is active (computed once per tick).</summary>
		public virtual bool EventActive
		{
			get
			{
				uint now = Main.GameUpdateCount;
				if (_eventTick != now) { _eventTick = now; _eventActive = LevyUtil.IsEventActive(); }
				return _eventActive;
			}
		}

		// =====================================================================================================
		// Dawn
		// =====================================================================================================
		/// <summary>Called by RosterSystem once per dawn (after its own payday): manpower regen toward the cap.</summary>
		public virtual void OnDawn(int dayNumber)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			CollectBarracksIds();
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				var te = GetBarracks(_scratchIds[k]); if (te == null) continue;
				int count = TownNpcsOf(te).Count;
				int cap = ManpowerCap(te);
				int regen = (count + LevyConstants.NpcsPerManpowerRegen - 1) / LevyConstants.NpcsPerManpowerRegen + (BookDiscipline ? LevyConstants.DisciplineExtraRegen : 0);
				int before = te.Manpower;
				te.Manpower = Math.Clamp(te.Manpower + regen, 0, Math.Max(0, cap));
				if (te.Manpower != before) SyncTE(te);
			}
		}

		// =====================================================================================================
		// Muster / stand-down (server)
		// =====================================================================================================
		public virtual void Mobilize(BarracksTE barracks, Player by)
		{
			if (!LevyUtil.IsServerOrSingle || barracks == null || barracks.Mobilized) return;
			string town = TownName(barracks);
			if (TownNpcsOf(barracks).Count < LevyConstants.MinTownNpcsForMilitia) { ChatTo(by, LevyUtil.T("UI.TooFewNpcs")); return; }
			if (barracks.Manpower <= 0) { ChatTo(by, LevyUtil.T("Chat.NoOneLeft", town)); return; }

			var rt = RuntimeOf(barracks);
			barracks.Mobilized = true;
			barracks.MusterTimer = 0; barracks.ReinforcementsPending = 0; barracks.MilitiaLostThisEvent = 0; barracks.NoOneLeftAnnounced = false;
			rt.MobilizerName = by?.name ?? "";
			// Anyone still walking home from an earlier stand-down turns around and rejoins.
			for (int i = 0; i < rt.StandingDown.Count; i++)
			{
				var body = BodyOf(rt.StandingDown[i].rec);
				if (body != null && body.State == SoldierState.ReturnHome) { body.State = SoldierState.Idle; body.NPC.netUpdate = true; }
			}
			rt.StandingDown.Clear();
			LevyUtil.Chat(LevyUtil.T("Chat.MilitiaCalledUp", town), LevyConstants.EventChatColor);
			SyncTE(barracks);
		}

		public virtual void StandDown(BarracksTE barracks)
		{
			if (!LevyUtil.IsServerOrSingle || barracks == null || !barracks.Mobilized) return;
			var rt = RuntimeOf(barracks);
			string town = TownName(barracks);
			barracks.Mobilized = false;
			LevyUtil.Chat(LevyUtil.T("Chat.MilitiaStandsDown", town), LevyConstants.EventChatColor);
			if (LevyConfig.Get.MilitiaDeathMessages == MilitiaDeathMessageMode.Count && barracks.MilitiaLostThisEvent > 0)
				LevyUtil.Chat(LevyUtil.T("Chat.MilitiaLost", town, barracks.MilitiaLostThisEvent), LevyConstants.EventChatColor);

			// A record that never got a body hands its kit straight back.
			if (rt.PendingSpawn != null) { ReturnGearHome(barracks, rt.PendingSpawn); rt.PendingSpawn = null; }

			uint deadline = Main.GameUpdateCount + (uint)LevyConstants.StandDownSafetyTicks;
			rt.StandingDown.Clear();
			for (int i = barracks.Militia.Count - 1; i >= 0; i--)
			{
				var rec = barracks.Militia[i];
				var body = BodyOf(rec);
				if (body == null) { barracks.Militia.RemoveAt(i); continue; }
				body.State = SoldierState.ReturnHome;
				body.NPC.netUpdate = true;
				rt.StandingDown.Add((rec, deadline));
			}
			barracks.MusterTimer = 0; barracks.ReinforcementsPending = 0; barracks.MilitiaLostThisEvent = 0; barracks.NoOneLeftAnnounced = false;
			SyncTE(barracks);
		}

		/// <summary>Called by RosterSystem when a militia body dies (after manpower was taken).</summary>
		public virtual void OnMilitiaBodyDied(BarracksTE barracks, SoldierRecord rec, SoldierNPC body)
		{
			if (!LevyUtil.IsServerOrSingle || rec == null) return;
			rec.BodyIndex = -1; rec.Dead = true;
			var b = barracks ?? GetBarracks(rec.HomeBarracksId) ?? FindBarracksHolding(rec);
			if (b == null) return;
			var rt = RuntimeOf(b);
			RemoveStandingDown(rt, rec);
			if (!b.Militia.Remove(rec)) return; // already dismissed: never count a loss twice
			b.MilitiaLostThisEvent++;
			if (b.Mobilized && b.Manpower > 0)
			{
				// Someone is left in the pool and not yet on the field (render cap or still streaming): send the next after the delay.
				int fielded = b.Militia.Count + (rt.PendingSpawn != null ? 1 : 0);
				if (fielded < b.Manpower)
				{
					b.ReinforcementsPending++;
					if (b.MusterTimer < LevyConstants.ReinforcementDelayTicks) b.MusterTimer = LevyConstants.ReinforcementDelayTicks;
				}
			}
			else if (b.Manpower <= 0 && !b.NoOneLeftAnnounced)
			{
				b.NoOneLeftAnnounced = true;
				LevyUtil.Chat(LevyUtil.T("Chat.NoOneLeft", TownName(b)), LevyConstants.EventChatColor);
			}
			SyncTE(b);
		}

		public virtual void OnMilitiaDied(BarracksTE barracks, SoldierRecord rec) => OnMilitiaBodyDied(barracks, rec, null);

		/// <summary>A militia body reached home after StandDown (or must leave now): gear back to the armory, record dropped, NPC removed.</summary>
		public virtual void DismissMilitiaBody(SoldierNPC body)
		{
			if (!LevyUtil.IsServerOrSingle || body == null) return;
			var npc = body.NPC;
			var rec = body.Record;
			if (rec != null)
			{
				var home = GetBarracks(rec.HomeBarracksId) ?? FindBarracksHolding(rec);
				ReturnGearHome(home, rec);
				if (home != null) { home.Militia.Remove(rec); RemoveStandingDown(RuntimeOf(home), rec); }
				rec.BodyIndex = -1;
			}
			if (!npc.active) return;
			// Vanilla despawn idiom: checkDead() returns early for an inactive NPC, so this never counts as a death.
			npc.active = false; npc.netSkip = -1; npc.life = 0; npc.netUpdate = true;
			if (LevyUtil.IsServer) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
		}

		// =====================================================================================================
		// Per-tick work (server)
		// =====================================================================================================
		public override void PostUpdateNPCs()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			CollectBarracksIds();
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				var b = GetBarracks(_scratchIds[k]); if (b == null) continue;
				var rt = RuntimeOf(b);
				if (b.Mobilized) StreamTick(b, rt);
				if (rt.StandingDown.Count > 0) StandDownTick(b, rt);
			}
			if (++_housekeepingTimer >= LevyConstants.SettlementHousekeepingTicks) { _housekeepingTimer = 0; Housekeeping(); }
		}

		/// <summary>Auto-muster on the rising edge of an event, stand down on the falling edge.</summary>
		public override void PostUpdateInvasions()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			bool active = EventActive;
			if (active == _lastEventActive) return;
			_lastEventActive = active;
			CollectBarracksIds();
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				var b = GetBarracks(_scratchIds[k]); if (b == null) continue;
				if (active) { if (b.AutoMuster && !b.Mobilized && CanRaiseMilitia(b)) Mobilize(b, PickMobilizer(b)); }
				else if (b.Mobilized) StandDown(b);
			}
		}

		/// <summary>One body per tick at most: retry a capped record, else stream the next when the pool has room and the timer ran out.</summary>
		private void StreamTick(BarracksTE b, Runtime rt)
		{
			if (rt.PendingSpawn != null)
			{
				if (TrySpawn(b, rt.PendingSpawn)) rt.PendingSpawn = null;
				return;
			}
			if (b.Militia.Count >= b.Manpower) return;
			if (b.MusterTimer > 0) { b.MusterTimer--; return; }

			var rec = CreateMilitiaRecord(b, rt);
			IssueGear(b, rec);
			long fee = EconomySystem.MilitiaMusterFeePerHead;
			if (fee > 0) EconomySystem.TryPay(b, fee); // never block a defence on money
			b.MusterTimer = Main.rand.Next(LevyConstants.MusterStreamMinTicks, LevyConstants.MusterStreamMaxTicks + 1);
			if (!TrySpawn(b, rec)) rt.PendingSpawn = rec;
			SyncTE(b);
		}

		private bool TrySpawn(BarracksTE b, SoldierRecord rec)
		{
			int idx = RosterSystem.Instance.SpawnBody(rec, b.MusterPoint);
			if (idx < 0 || idx >= Main.maxNPCs || !Main.npc[idx].active) return false;
			b.Militia.Add(rec);
			if (b.ReinforcementsPending > 0) b.ReinforcementsPending--;
			if (Main.npc[idx].ModNPC is SoldierNPC body)
				DialogueSystem.Bark(body, DialogueTrigger.MUSTER, new DialogueContext { Town = TownName(b), Player = rec.CommanderName });
			return true;
		}

		private void StandDownTick(BarracksTE b, Runtime rt)
		{
			uint now = Main.GameUpdateCount;
			for (int i = rt.StandingDown.Count - 1; i >= 0; i--)
			{
				var (rec, deadline) = rt.StandingDown[i];
				var body = BodyOf(rec);
				if (body == null) { b.Militia.Remove(rec); rt.StandingDown.RemoveAt(i); continue; }
				if (now >= deadline) { rt.StandingDown.RemoveAt(i); DismissMilitiaBody(body); }
			}
		}

		/// <summary>Every second: drop runtime for vanished barracks, prune bodiless records, dismiss orphaned militia, flush armory syncs.</summary>
		private void Housekeeping()
		{
			_scratchIds.Clear();
			foreach (var id in _rt.Keys) if (GetBarracks(id) == null) _scratchIds.Add(id);
			for (int k = 0; k < _scratchIds.Count; k++) _rt.Remove(_scratchIds[k]);

			CollectBarracksIds();
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				var b = GetBarracks(_scratchIds[k]); if (b == null) continue;
				var rt = RuntimeOf(b);
				for (int i = b.Militia.Count - 1; i >= 0; i--)
					if (!b.Militia[i].HasBody) { RemoveStandingDown(rt, b.Militia[i]); b.Militia.RemoveAt(i); }
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsMilitia) continue;
				var home = GetBarracks(s.Record.HomeBarracksId);
				if (home == null) { DismissMilitiaBody(s); continue; }
				if (home.Militia.Contains(s.Record)) continue;
				if (home.Mobilized) home.Militia.Add(s.Record); else DismissMilitiaBody(s);
			}

			FlushArmorySync();
		}

		// =====================================================================================================
		// Economy actions (server)
		// =====================================================================================================
		public virtual bool RecruitingDrive(BarracksTE barracks, Player by)
		{
			if (!LevyUtil.IsServerOrSingle || barracks == null) return false;
			int day = RosterSystem.Instance.DayNumber;
			string town = TownName(barracks);
			if (barracks.RecruitingDriveDay == day) { ChatTo(by, LevyUtil.T("Militia.RecruitingDriveDone", town)); return false; }
			int missing = ManpowerCap(barracks) - barracks.Manpower;
			if (missing <= 0) { ChatTo(by, LevyUtil.T("Militia.ManpowerFull", town)); return false; }
			long price = EconomySystem.RecruitingDrivePerPoint;
			int bought = price <= 0 ? missing : (int)Math.Min(missing, barracks.Treasury / price);
			if (bought < 1) { ChatTo(by, LevyUtil.T("UI.NotEnoughMoney")); return false; }
			long cost = bought * Math.Max(0, price);
			if (cost > 0 && !EconomySystem.TryPay(barracks, cost)) { ChatTo(by, LevyUtil.T("UI.NotEnoughMoney")); return false; }
			barracks.Manpower += bought;
			barracks.RecruitingDriveDay = day;
			LevyUtil.Chat(LevyUtil.T("Chat.RecruitingDrive", town, LevyUtil.Coins(cost)));
			SyncTE(barracks);
			return true;
		}

		public virtual bool HazardPay(BarracksTE barracks, Player by)
		{
			if (!LevyUtil.IsServerOrSingle || barracks == null) return false;
			int day = RosterSystem.Instance.DayNumber;
			string town = TownName(barracks);
			if (barracks.HazardPayDay == day) { ChatTo(by, LevyUtil.T("Militia.HazardPayDone", town)); return false; }
			long cost = 0; int heads = 0;
			var regulars = RosterSystem.Instance.Regulars;
			for (int i = 0; i < regulars.Count; i++)
				if (regulars[i].HomeBarracksId == barracks.ID) { cost += EconomySystem.WagePerDay(regulars[i].Grade, false); heads++; }
			cost += barracks.Militia.Count * EconomySystem.LevyWage; heads += barracks.Militia.Count;
			cost *= LevyConstants.HazardPayDays;
			if (heads == 0) { ChatTo(by, LevyUtil.T("Militia.NoOneToPay", town)); return false; }
			if (!EconomySystem.TryPay(barracks, cost)) { ChatTo(by, LevyUtil.T("UI.NotEnoughMoney")); return false; }
			for (int i = 0; i < regulars.Count; i++) if (regulars[i].HomeBarracksId == barracks.ID) regulars[i].HazardPaid = true;
			barracks.HazardPayDay = day;
			LevyUtil.Chat(LevyUtil.T("Chat.HazardPay", town, LevyUtil.Coins(cost)));
			SyncTE(barracks);
			return true;
		}

		// =====================================================================================================
		// Helpers
		// =====================================================================================================
		/// <summary>Pushes a tile entity's state to every client (server only). Call after any change to a BarracksTE.</summary>
		public static void SyncTE(TileEntity te)
		{
			if (te == null || Main.netMode != NetmodeID.Server) return;
			NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, te.ID, te.Position.X, te.Position.Y);
		}

		private Runtime RuntimeOf(BarracksTE b)
		{
			if (!_rt.TryGetValue(b.ID, out var rt)) { rt = new Runtime(); _rt[b.ID] = rt; }
			return rt;
		}

		private void CollectBarracksIds()
		{
			_scratchIds.Clear();
			foreach (var kv in TileEntity.ByID) if (kv.Value is BarracksTE) _scratchIds.Add(kv.Key);
		}

		private static SoldierNPC BodyOf(SoldierRecord rec) => rec != null && rec.HasBody ? Main.npc[rec.BodyIndex].ModNPC as SoldierNPC : null;

		private static void RemoveStandingDown(Runtime rt, SoldierRecord rec)
		{
			for (int i = rt.StandingDown.Count - 1; i >= 0; i--) if (ReferenceEquals(rt.StandingDown[i].rec, rec)) rt.StandingDown.RemoveAt(i);
		}

		private BarracksTE FindBarracksHolding(SoldierRecord rec)
		{
			foreach (var te in TileEntity.ByID.Values)
				if (te is BarracksTE b && (b.Militia.Contains(rec) || (_rt.TryGetValue(b.ID, out var rt) && ReferenceEquals(rt.PendingSpawn, rec)))) return b;
			return null;
		}

		/// <summary>Nearest active player within <see cref="LevyConstants.AutoMusterPlayerSearchTiles"/>, else any active player, else null.</summary>
		private static Player PickMobilizer(BarracksTE b)
		{
			Player near = null, any = null; float bestD = float.MaxValue;
			float limit = LevyConstants.AutoMusterPlayerSearchTiles * LevyConstants.TilePx; limit *= limit;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var p = Main.player[i];
				if (!p.active) continue;
				any ??= p;
				float d = Vector2.DistanceSquared(p.Center, b.Center);
				if (d <= limit && d < bestD) { bestD = d; near = p; }
			}
			return near ?? any;
		}

		private SoldierRecord CreateMilitiaRecord(BarracksTE b, Runtime rt)
		{
			bool male = Main.rand.Next(2) == 0;
			var variants = male ? PlayerVariantID.Sets.VariantOrderMale : PlayerVariantID.Sets.VariantOrderFemale;
			int py = Math.Clamp(b.Position.Y + 2, 1, Main.maxTilesY - 2);
			var rec = new SoldierRecord
			{
				Id = _nextMilitiaId--,
				Kind = SoldierKind.Militia,
				Grade = Grade.Levy,
				Temperament = (Temperament)Main.rand.Next(4),
				Male = male,
				SkinVariant = variants[Main.rand.Next(variants.Length)],
				Hair = Main.rand.Next(LevyConstants.MilitiaHairStyles),
				HairColor = Pick(LevyConstants.MilitiaHairColors), SkinColor = Pick(LevyConstants.MilitiaSkinColors), EyeColor = Pick(LevyConstants.MilitiaEyeColors),
				ShirtColor = Pick(LevyConstants.MilitiaShirtColors), UnderShirtColor = Pick(LevyConstants.MilitiaUnderShirtColors),
				PantsColor = Pick(LevyConstants.MilitiaPantsColors), ShoeColor = Pick(LevyConstants.MilitiaShoeColors),
				HomeBarracksId = b.ID,
				HomePos = b.Position,
				CommanderName = rt.MobilizerName ?? "",
				Order = SoldierOrder.Patrol,
				PatrolA = new Point16(Math.Clamp(b.Position.X - LevyConstants.SettlementRadiusX / 2, 1, Main.maxTilesX - 2), py),
				PatrolB = new Point16(Math.Clamp(b.Position.X + LevyConstants.SettlementRadiusX / 2, 1, Main.maxTilesX - 2), py),
			};
			rec.Name = rec.RankPrefix + " " + DialogueSystem.RandomSoldierName(male);
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			return rec;
		}

		private static Color Pick(Color[] palette) => palette[Main.rand.Next(palette.Length)];

		/// <summary>Arms a record from the linked armory; other armories in link range fill in only while it still has no weapon.</summary>
		private void IssueGear(BarracksTE b, SoldierRecord rec)
		{
			var primary = LinkedArmory(b);
			if (primary == null) return;
			SafeIssue(primary, rec);
			if (rec.Gear.HasWeapon) return;
			foreach (var a in ArmoriesNear(b.Center, LevyConstants.ArmoryLinkRadius))
			{
				if (ReferenceEquals(a, primary)) continue;
				SafeIssue(a, rec);
				if (rec.Gear.HasWeapon) break;
			}
		}

		private void SafeIssue(ArmoryTE armory, SoldierRecord rec)
		{
			try { if (armory.IssueGear(rec)) _dirtyArmories.Add(armory.ID); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: armory refused to issue gear to a militia record: " + e); }
		}

		/// <summary>Returns a militia kit to the home armory (or the nearest one in link range of the old home position). No armory: the gear came from nowhere and goes nowhere.</summary>
		private void ReturnGearHome(BarracksTE home, SoldierRecord rec)
		{
			if (rec == null) return;
			ArmoryTE armory = home != null ? LinkedArmory(home) : null;
			if (armory == null && rec.HomePos.X >= 0)
				armory = NearestArmory(new Vector2((rec.HomePos.X + 1.5f) * LevyConstants.TilePx, (rec.HomePos.Y + 1.5f) * LevyConstants.TilePx), LevyConstants.ArmoryLinkRadius);
			if (armory != null)
			{
				try { armory.ReturnGear(rec.Gear); _dirtyArmories.Add(armory.ID); }
				catch (Exception e) { Levy.Instance.Logger.Warn("Levy: armory refused a returned militia kit: " + e); }
			}
			rec.Gear.Clear();
		}

		private void FlushArmorySync()
		{
			if (_dirtyArmories.Count == 0) return;
			if (LevyUtil.IsServer)
				foreach (var id in _dirtyArmories)
					if (TileEntity.ByID.TryGetValue(id, out var te) && te is ArmoryTE) SyncTE(te);
			_dirtyArmories.Clear();
		}

		/// <summary>A notice to one player (or everyone when there is no one to address).</summary>
		private static void ChatTo(Player to, string text)
		{
			if (to == null || !to.active) { LevyUtil.Chat(text, LevyConstants.NoticeChatColor); return; }
			if (Main.netMode == NetmodeID.Server) ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(text), LevyConstants.NoticeChatColor, to.whoAmI);
			else if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(text, LevyConstants.NoticeChatColor);
		}
	}
}
