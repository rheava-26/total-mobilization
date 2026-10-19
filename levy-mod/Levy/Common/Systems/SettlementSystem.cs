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
		/// <summary>Militia kits read back from the world tag (written by <see cref="SaveWorldData"/>); returned to their armories on the first server tick after load.</summary>
		private readonly List<(Point16 home, SoldierGear gear)> _pendingKits = new();

		public override void OnWorldLoad()
		{
			ResetRuntime();
			// Mobilized is saved on the barracks but the militia themselves never are: after a crash / dedicated-server exit / Host&Play shutdown
			// the flag would otherwise stay true and keep streaming militia from a depleted armory.
			if (LevyUtil.IsServerOrSingle) ClearStaleMobilization();
		}

		public override void OnWorldUnload() { ResetRuntime(); _pendingKits.Clear(); }

		public override void ClearWorld()
		{
			BookConscription = BookDrill = BookPlunder = BookFortification = BookDiscipline = false;
			ResetRuntime();
			_pendingKits.Clear();
		}

		private void ClearStaleMobilization()
		{
			foreach (var b in AllBarracks())
			{
				b.Mobilized = false; b.MusterTimer = 0; b.ReinforcementsPending = 0; b.MilitiaLostThisEvent = 0; b.NoOneLeftAnnounced = false;
				b.Militia.Clear();
			}
		}

		private void ResetRuntime()
		{
			_rt.Clear(); _dirtyArmories.Clear(); _scratchIds.Clear();
			_netArmories.Clear(); _netStamp = uint.MaxValue;
			_nextMilitiaId = -1;
			_lastEventActive = false; _eventTick = uint.MaxValue;
			_housekeepingTimer = 0;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			tag["bookConscription"] = BookConscription; tag["bookDrill"] = BookDrill; tag["bookPlunder"] = BookPlunder;
			tag["bookFortification"] = BookFortification; tag["bookDiscipline"] = BookDiscipline;

			// Mustered militia wear kits drawn from the armory and their records are never saved. PreSaveAndQuit only covers a single-player
			// Save&Quit, so persist every live militia kit here too (crash, dedicated exit, Host&Play shutdown, autosave). Read-only: tile
			// entities are never touched in here (they may already be serialized). The kits go back to an armory on the next load.
			var kits = new List<TagCompound>();
			var seen = new HashSet<SoldierRecord>();
			void AddKit(SoldierRecord rec)
			{
				if (rec == null || rec.Kind != SoldierKind.Militia || !seen.Add(rec)) return;
				bool any = false;
				foreach (var _ in rec.Gear.AllItems()) { any = true; break; }
				if (!any) return;
				kits.Add(new TagCompound { ["gear"] = rec.Gear.Save(), ["hx"] = (int)rec.HomePos.X, ["hy"] = (int)rec.HomePos.Y });
			}
			foreach (var b in AllBarracks())
			{
				if (_rt.TryGetValue(b.ID, out var rt))
				{
					AddKit(rt.PendingSpawn);
					foreach (var (rec, _) in rt.StandingDown) AddKit(rec);
				}
				foreach (var rec in b.Militia) AddKit(rec);
			}
			for (int i = 0; i < Main.maxNPCs; i++) // bodies not (yet) listed on a barracks
			{
				var n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn && sn.IsMilitia) AddKit(sn.Record);
			}
			// Kits restored from a previous load that have not been returned yet are still owed to the armory: keep them in the tag.
			foreach (var (home, gear) in _pendingKits)
				kits.Add(new TagCompound { ["gear"] = gear.Save(), ["hx"] = (int)home.X, ["hy"] = (int)home.Y });
			if (kits.Count > 0) tag["militiaKits"] = kits;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			BookConscription = tag.GetBool("bookConscription"); BookDrill = tag.GetBool("bookDrill"); BookPlunder = tag.GetBool("bookPlunder");
			BookFortification = tag.GetBool("bookFortification"); BookDiscipline = tag.GetBool("bookDiscipline");
			_pendingKits.Clear();
			foreach (var t in tag.GetList<TagCompound>("militiaKits"))
				_pendingKits.Add((new Point16(t.GetInt("hx"), t.GetInt("hy")), SoldierGear.Load(t.GetCompound("gear"))));
		}

		/// <summary>First server tick after load: every persisted militia kit goes back to its home armory (nearest in range, else spilled protected).</summary>
		private void RestorePendingKits()
		{
			if (_pendingKits.Count == 0) return;
			foreach (var (home, gear) in _pendingKits)
			{
				var rec = new SoldierRecord { Kind = SoldierKind.Militia, HomePos = home, Gear = gear, BodyIndex = -1 };
				var b = BarracksAt(home);
				ReturnGearHome(b, rec);
			}
			_pendingKits.Clear();
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
		/// <summary>The barracks whose top-left tile sits at <paramref name="pos"/>. Position is stable across saves; the tile-entity ID is not.</summary>
		public virtual BarracksTE BarracksAt(Point16 pos) { if (pos == Point16.NegativeOne) return null; foreach (var te in TileEntity.ByID.Values) if (te is BarracksTE b && b.Position == pos) return b; return null; }
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

		// ---- pylon-networked armories share issue-side gear ----
		private readonly List<ArmoryTE> _netArmories = new();
		private uint _netStamp = uint.MaxValue;

		/// <summary>
		/// The armories that pool their stored gear for issuing. An armory with a vanilla pylon (any type, any team) within
		/// <see cref="LevyConstants.PylonShareRadius"/> tiles is on the network, and every networked armory shares one pool (pylons interconnect).
		/// <paramref name="from"/> is always first, the rest nearest-first. An armory off the network gets just itself. Cached per tick; read-only use.
		/// </summary>
		public virtual List<ArmoryTE> NetworkedArmories(ArmoryTE from)
		{
			var res = new List<ArmoryTE>(4);
			if (from == null) return res;
			res.Add(from);
			var pylons = Main.PylonSystem?.Pylons;
			if (pylons == null || pylons.Count == 0) return res;

			uint now = Main.GameUpdateCount;
			if (_netStamp != now)
			{
				_netStamp = now;
				_netArmories.Clear();
				foreach (var te in TileEntity.ByID.Values)
					if (te is ArmoryTE a && NearPylon(a, pylons)) _netArmories.Add(a);
			}
			if (!_netArmories.Contains(from)) return res;

			int start = res.Count;
			foreach (var a in _netArmories) if (!ReferenceEquals(a, from)) res.Add(a);
			Vector2 c = from.Center;
			res.Sort(start, res.Count - start, Comparer<ArmoryTE>.Create((x, y) =>
			{
				int d = Vector2.DistanceSquared(x.Center, c).CompareTo(Vector2.DistanceSquared(y.Center, c));
				return d != 0 ? d : x.ID.CompareTo(y.ID);
			}));
			return res;
		}

		private static bool NearPylon(ArmoryTE a, IReadOnlyList<Terraria.GameContent.TeleportPylonInfo> pylons)
		{
			float limit = LevyConstants.PylonShareRadius * LevyConstants.TilePx; limit *= limit;
			Vector2 c = a.Center;
			for (int i = 0; i < pylons.Count; i++)
			{
				var p = pylons[i].PositionInTiles; // top-left of the 3x4 pylon; measure from its middle
				var pc = new Vector2((p.X + 1.5f) * LevyConstants.TilePx, (p.Y + 2f) * LevyConstants.TilePx);
				if (Vector2.DistanceSquared(c, pc) <= limit) return true;
			}
			return false;
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
				if (body == null) { ReturnGearHome(barracks, rec); barracks.Militia.RemoveAt(i); continue; }
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
			if (b == null) { ReturnGearHome(null, rec); return; } // no barracks found: still recover the kit via the record's home position
			var rt = RuntimeOf(b);
			RemoveStandingDown(rt, rec);
			if (!b.Militia.Remove(rec)) return; // already dismissed: never count a loss twice (and the kit was already handled)
			// A dead militiaman's kit was drawn from the armory — recover it (softcore keeps gear on death). In hardcore/mediumcore
			// OnBodyDied already dropped the gear on the ground and cleared it, so this is a harmless no-op there. Without this,
			// every militia death silently DELETED a full kit, draining the armory over a long fight.
			ReturnGearHome(b, rec);
			b.MilitiaLostThisEvent++;
			if (b.Mobilized && b.Manpower > 0)
			{
				// Someone is left in the pool and not yet on the field (render cap or still streaming): send the next after the delay.
				int fielded = b.Militia.Count + (rt.PendingSpawn != null ? 1 : 0);
				if (fielded < FieldCap(b))
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
			body.DropCarriedItems(); // anything the militiaman was hauling goes on the ground before the body vanishes
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
			if (_pendingKits.Count > 0) RestorePendingKits();
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
			bool active = EventActive || LevyUtil.IsBossActive();
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
		/// <summary>How many militia a barracks fields AT ONCE: the small field cap, never more than the manpower reserve it has left.</summary>
		private static int FieldCap(BarracksTE b) => Math.Min(b.Manpower, Math.Max(1, LevyConfig.Get.MilitiaFieldCap));

		private void StreamTick(BarracksTE b, Runtime rt)
		{
			if (rt.PendingSpawn != null)
			{
				if (TrySpawn(b, rt.PendingSpawn)) rt.PendingSpawn = null;
				return;
			}
			if (b.Militia.Count >= FieldCap(b)) return;
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
				if (body == null) { ReturnGearHome(b, rec); b.Militia.Remove(rec); rt.StandingDown.RemoveAt(i); continue; }
				if (now >= deadline) { rt.StandingDown.RemoveAt(i); DismissMilitiaBody(body); }
			}
		}

		/// <summary>Every second: drop runtime for vanished barracks, prune bodiless records, dismiss orphaned militia, flush armory syncs.</summary>
		private void Housekeeping()
		{
			_scratchIds.Clear();
			foreach (var id in _rt.Keys) if (GetBarracks(id) == null) _scratchIds.Add(id);
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				// The barracks is gone: a record still waiting on the render cap hands its kit to the nearest armory near the old home.
				var stale = _rt[_scratchIds[k]];
				if (stale.PendingSpawn != null) ReturnGearHome(null, stale.PendingSpawn);
				_rt.Remove(_scratchIds[k]);
			}

			CollectBarracksIds();
			for (int k = 0; k < _scratchIds.Count; k++)
			{
				var b = GetBarracks(_scratchIds[k]); if (b == null) continue;
				var rt = RuntimeOf(b);
				for (int i = b.Militia.Count - 1; i >= 0; i--)
					if (!b.Militia[i].HasBody) { ReturnGearHome(b, b.Militia[i]); RemoveStandingDown(rt, b.Militia[i]); b.Militia.RemoveAt(i); }
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
			for (int i = 0; i < barracks.Militia.Count; i++) barracks.Militia[i].HazardPaid = true; // charged above; grant them the fear reduction (militia records are transient, no dawn reset needed)
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
				Order = SoldierOrder.SearchDestroy,
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

		/// <summary>
		/// Returns a militia kit to the home armory (or the nearest one in link range of the old home position). No armory: the kit is
		/// DROPPED on the ground (at the live body, else the old home position) — a kit is never deleted.
		/// </summary>
		private void ReturnGearHome(BarracksTE home, SoldierRecord rec)
		{
			if (rec == null) return;
			bool any = false;
			foreach (var _ in rec.Gear.AllItems()) { any = true; break; }
			if (!any) { rec.Gear.Clear(); return; }
			ArmoryTE armory = home != null ? LinkedArmory(home) : null;
			if (armory == null && rec.HomePos.X >= 0)
				armory = NearestArmory(new Vector2((rec.HomePos.X + 1.5f) * LevyConstants.TilePx, (rec.HomePos.Y + 1.5f) * LevyConstants.TilePx), LevyConstants.ArmoryLinkRadius);
			if (armory != null)
			{
				try { armory.ReturnGear(rec.Gear); _dirtyArmories.Add(armory.ID); return; }
				catch (Exception e) { Levy.Instance.Logger.Warn("Levy: armory refused a returned militia kit, dropping it instead: " + e); }
			}
			if (LevyUtil.IsServerOrSingle)
			{
				// No armory anywhere (the player removed it): spill the kit instead of deleting it.
				var body = BodyOf(rec);
				Vector2? at = null;
				if (body != null && body.NPC.active) at = body.NPC.Center;
				else if (rec.HomePos.X >= 0) at = new Vector2((rec.HomePos.X + 1.5f) * LevyConstants.TilePx, (rec.HomePos.Y + 1.5f) * LevyConstants.TilePx);
				else
				{
					for (int i = 0; i < Main.maxPlayers; i++) if (Main.player[i].active) { at = Main.player[i].Center; break; }
				}
				if (at.HasValue)
				{
					var box = new Rectangle((int)at.Value.X - 8, (int)at.Value.Y - 8, 16, 16);
					var source = new EntitySource_Misc("Levy:MilitiaKitRecovered");
					foreach (var it in rec.Gear.AllItems()) RosterSystem.SpillProtected(source, box, it);
				}
				else { Levy.Instance.Logger.Warn("Levy: a militia kit had no armory and no position to drop at; keeping it on the record."); return; }
			}
			rec.Gear.Clear();
		}

		/// <summary>
		/// Runs before the quit-save: every mustered militiaman's kit goes back to its armory (or the ground) so it is written with the world.
		/// Militia records are never saved, so without this the gear drawn onto them would vanish on exit.
		/// </summary>
		public override void PreSaveAndQuit()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			foreach (var b in new List<BarracksTE>(AllBarracks()))
			{
				var rt = RuntimeOf(b);
				var done = new HashSet<SoldierRecord>();
				if (rt.PendingSpawn != null) { done.Add(rt.PendingSpawn); ReturnGearHome(b, rt.PendingSpawn); }
				foreach (var rec in b.Militia) if (done.Add(rec)) ReturnGearHome(b, rec);
				foreach (var (rec, _) in rt.StandingDown) if (done.Add(rec)) ReturnGearHome(b, rec);
				b.Militia.Clear(); rt.StandingDown.Clear(); rt.PendingSpawn = null;
				b.Mobilized = false;
			}
		}

		/// <summary>
		/// Re-arm for fielded militia (server / single player): each returns its kit to its barracks' Armory, then everyone draws again
		/// from the current issue slots. Militia with no linked Armory keep their kit. Returns how many were re-armed.
		/// </summary>
		public virtual int RearmMilitia()
		{
			if (!LevyUtil.IsServerOrSingle) return 0;
			var batch = new List<(BarracksTE home, SoldierRecord rec)>();
			foreach (var b in AllBarracks())
			{
				if (!b.Mobilized || LinkedArmory(b) == null) continue;
				foreach (var rec in b.Militia) if (rec.HasBody) batch.Add((b, rec));
			}
			foreach (var (home, rec) in batch) ReturnGearHome(home, rec);
			foreach (var (home, rec) in batch)
			{
				IssueGear(home, rec);
				BodyOf(rec)?.NotifyGearChanged();
			}
			return batch.Count;
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
