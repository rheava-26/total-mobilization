using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Movement;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Officers
{
	/// <summary>Why a commission cannot happen right now (the menu greys the button with the matching text; the server re-checks every action).</summary>
	public enum OfficerCheck : byte { Ok, Invalid, NoDef, AlreadyOfficer, NoSlot, NotHoused, TooFar, NoMoney }

	/// <summary>
	/// Officers: town NPCs conscripted into the army. THE DATA LIVES ON THE NPC (<see cref="OfficerGlobalNPC"/>: commander, slot, paying barracks, unpaid days; saved
	/// with the town NPC and synced through its extra-AI block), so there is no second list to drift: a dead NPC takes its commission with it, a respawned one is a
	/// fresh civilian, and "which NPC holds which slot" is a scan of Main.npc cached once per tick (<see cref="OfficersOf"/>, <see cref="InSlot"/>).
	/// This system owns the rules: who may be commissioned (<see cref="CanCommission"/>), the one-time fee, the dawn payroll, dismissal, lapse housekeeping and the
	/// per-tick caches enemy targeting reads. Mutating members run on the server / single player only; clients call the read members.
	/// </summary>
	public class OfficerSystem : ModSystem
	{
		public static OfficerSystem Instance => ModContent.GetInstance<OfficerSystem>();

		// ---- per-tick cache (rebuilt lazily, on every netmode: clients run NPC AI and so the enemy-targeting swap) ----
		private uint _stamp = uint.MaxValue;
		private readonly List<NPC> _enrolled = new();
		private readonly List<OfficerBody> _active = new();
		/// <summary>town NPC whoAmI -> its live avatar this tick (rebuilt in <see cref="Refresh"/> on every netmode; avatars are synced NPCs).</summary>
		private readonly Dictionary<int, SoldierNPC> _avatars = new();
		private int _lastDay = -1; private bool _dayInit;
		private int _housekeepingTimer;

		public override void Load() => OfficerNet.Register();

		/// <summary>Registers the officer definitions once every mod's content exists (NPCID.Sets and modded NPC types are final by now).</summary>
		public override void PostSetupContent() { OfficerRegistry.Clear(); OfficerRegistry.RegisterAll(); }

		public override void Unload() { OfficerRegistry.Clear(); _enrolled.Clear(); _active.Clear(); }

		public override void OnWorldLoad() { Reset(); }
		public override void OnWorldUnload() { Reset(); }
		private void Reset() { _stamp = uint.MaxValue; _enrolled.Clear(); _active.Clear(); _avatars.Clear(); _dayInit = false; _lastDay = -1; _housekeepingTimer = 0; }

		// =====================================================================================================
		// Queries (safe on clients)
		// =====================================================================================================
		public static OfficerGlobalNPC Data(NPC npc) => npc != null && npc.TryGetGlobalNPC(out OfficerGlobalNPC g) ? g : null;

		private void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp == now) return;
			_stamp = now;
			_enrolled.Clear(); _active.Clear(); _avatars.Clear();
			// 1. map every live officer avatar by the town NPC it serves (runs on clients too: the avatar is a synced NPC). First wins; duplicates are culled server-side.
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC s && s.OfficerMode && s.NPC.life > 0 && s.OfficerTownIndex >= 0 && !_avatars.ContainsKey(s.OfficerTownIndex))
					_avatars[s.OfficerTownIndex] = s;
			}
			// 2. enrolled town NPCs; bind each commission's targeting body to its avatar (only a commission with a live avatar is an active target)
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.townNPC) continue;
				OfficerGlobalNPC g = Data(n);
				if (g == null || !g.Enrolled) continue;
				_enrolled.Add(n);
				if (!OfficerRegistry.IsOfficerType(n.type) || LevyUtil.FindPlayerByName(g.Commander) < 0) continue;
				OfficerBody body = g.EnsureBody(n);
				_avatars.TryGetValue(n.whoAmI, out SoldierNPC avatar);
				body.SetAvatar(avatar);
				if (avatar != null && avatar.NPC.active && avatar.NPC.life > 0) _active.Add(body);
			}
		}

		/// <summary>True when this enrolled town NPC has a live avatar marching for it (used by the town NPC's draw to hide itself). Valid on clients.</summary>
		public bool HasLiveAvatar(NPC townNpc)
		{
			if (townNpc == null) return false;
			Refresh();
			return _avatars.TryGetValue(townNpc.whoAmI, out var a) && a != null && a.NPC.active && a.NPC.life > 0;
		}

		/// <summary>Every conscripted town NPC in the world, whoever commands it. Read-only; valid for the current tick.</summary>
		public IReadOnlyList<NPC> Enrolled { get { Refresh(); return _enrolled; } }

		/// <summary>Officers that are on duty this tick (alive, a registered type, commander online): the ones with a targeting body.</summary>
		public IReadOnlyList<OfficerBody> ActiveBodies { get { Refresh(); return _active; } }

		/// <summary>The targeting body of an officer that is on duty, else null. The returned body points enemies at the avatar (bound this tick by <see cref="Refresh"/>).</summary>
		public OfficerBody BodyOf(NPC npc)
		{
			if (npc == null || !npc.active || !npc.townNPC || npc.life <= 0) return null;
			OfficerGlobalNPC g = Data(npc);
			if (g == null || !g.Enrolled || !OfficerRegistry.IsOfficerType(npc.type) || LevyUtil.FindPlayerByName(g.Commander) < 0) return null;
			Refresh(); // binds the commission's body to its current avatar
			OfficerBody body = g.EnsureBody(npc);
			_avatars.TryGetValue(npc.whoAmI, out SoldierNPC avatar);
			body.SetAvatar(avatar);
			return body;
		}

		public bool IsEnrolled(NPC npc) => Data(npc)?.Enrolled == true;

		/// <summary>The officers one commander fields.</summary>
		public IEnumerable<NPC> OfficersOf(string commander)
		{
			Refresh();
			for (int i = 0; i < _enrolled.Count; i++)
				if (string.Equals(Data(_enrolled[i]).Commander, commander, StringComparison.Ordinal)) yield return _enrolled[i];
		}

		/// <summary>The officer in a command slot (0-based) of this commander, or null.</summary>
		public NPC InSlot(string commander, int slot)
		{
			foreach (NPC n in OfficersOf(commander)) if (Data(n).Slot == slot) return n;
			return null;
		}

		public int UsedSlots(string commander) { int c = 0; foreach (var _ in OfficersOf(commander)) c++; return c; }

		/// <summary>
		/// Command slots available to every commander right now: a base (<see cref="LevyConstants.Officer.OfficerBaseSlots"/>) plus one for each boss
		/// MILESTONE this world has crossed — Wall of Flesh, any mechanical boss, Plantera, Golem, Moon Lord. World-global (boss progress is world state,
		/// not per-player). The downed set and the hardmode flag both sync to clients and only ever grow, so the cap is the same on every machine and never
		/// drops mid-world. Static so the UI can read it without the instance.
		/// </summary>
		public static int MaxSlots()
		{
			int n = LevyConstants.Officer.OfficerBaseSlots;
			if (Main.hardMode || BossDownedSystem.IsTypeDowned(NPCID.WallofFlesh)) n++;          // Wall of Flesh (hardmode is the always-synced proof it fell)
			if (BossDownedSystem.IsTypeDowned(NPCID.TheDestroyer)
				|| BossDownedSystem.IsTypeDowned(NPCID.Retinazer)
				|| BossDownedSystem.IsTypeDowned(NPCID.Spazmatism)
				|| BossDownedSystem.IsTypeDowned(NPCID.SkeletronPrime)) n++;                     // any mechanical boss
			if (BossDownedSystem.IsTypeDowned(NPCID.Plantera)) n++;
			if (BossDownedSystem.IsTypeDowned(NPCID.Golem)) n++;
			if (BossDownedSystem.IsTypeDowned(NPCID.MoonLordCore)) n++;
			return n;
		}

		/// <summary>The slot cap floored at the officers this commander already fields, so a never-expected cap dip can never orphan a serving officer. Use for display and housekeeping; <see cref="MaxSlots"/> itself gates new commissions.</summary>
		public int SlotCap(string commander) => Math.Max(MaxSlots(), UsedSlots(commander));

		/// <summary>Lowest free command slot of this commander, or -1 when all <see cref="MaxSlots"/> are taken.</summary>
		public int FreeSlot(string commander)
		{
			int cap = MaxSlots();
			for (int s = 0; s < cap; s++) if (InSlot(commander, s) == null) return s;
			return -1;
		}

		// ---- economy ----
		/// <summary>Daily wage in copper: the tier's Levy wage (with the config multiplier) times <see cref="LevyConstants.Officer.WageMult"/> and the officer's own cost multiplier.</summary>
		public long Wage(OfficerDef def) => def == null ? 0 : (long)Math.Round(EconomySystem.LevyWage * LevyConstants.Officer.WageMult * def.CostMult);
		/// <summary>One-time commission fee in copper: <see cref="LevyConstants.Officer.FeeDays"/> days of wage.</summary>
		public long Fee(OfficerDef def) => Wage(def) * LevyConstants.Officer.FeeDays;

		/// <summary>
		/// May <paramref name="commander"/> commission <paramref name="npc"/> from <paramref name="barracks"/> right now? The menu uses it to enable the button; the
		/// server runs it again on every request, so a modified client gains nothing.
		/// </summary>
		public OfficerCheck CanCommission(Player commander, BarracksTE barracks, NPC npc)
		{
			if (commander == null || !commander.active || barracks == null || npc == null || !npc.active || !npc.townNPC || npc.life <= 0) return OfficerCheck.Invalid;
			if (!OfficerRegistry.TryGet(npc.type, out OfficerDef def)) return OfficerCheck.NoDef;
			OfficerGlobalNPC g = Data(npc);
			if (g == null) return OfficerCheck.Invalid;
			if (g.Enrolled) return OfficerCheck.AlreadyOfficer;
			if (FreeSlot(commander.name) < 0) return OfficerCheck.NoSlot;
			if (!SettlementSystem.Instance.TownNpcsOf(barracks).Contains(npc)) return OfficerCheck.NotHoused;
			if (!LevyUtil.WithinTiles(commander.Center, barracks.Center, LevyConstants.Officer.ActionRangeTiles)) return OfficerCheck.TooFar;
			if (!EconomySystem.CanAfford(barracks, Fee(def))) return OfficerCheck.NoMoney;
			return OfficerCheck.Ok;
		}

		// =====================================================================================================
		// Actions (server / single player)
		// =====================================================================================================
		/// <summary>Conscripts <paramref name="npc"/>: charges the fee to the barracks treasury and puts him in the commander's lowest free slot. Tells the commander why on a refusal.</summary>
		public bool Commission(Player commander, BarracksTE barracks, NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle) return false;
			OfficerCheck check = CanCommission(commander, barracks, npc);
			if (check != OfficerCheck.Ok)
			{
				if (commander != null) RosterSystem.SendToPlayer(commander, LevyUtil.T("Officers.Fail." + check));
				return false;
			}
			OfficerDef def = OfficerRegistry.Get(npc.type);
			OfficerGlobalNPC g = Data(npc);
			long fee = Fee(def);
			if (!EconomySystem.TryPay(barracks, fee)) return false; // CanCommission just said yes; never commission unpaid
			RosterSystem.SyncBarracks(barracks);

			g.Enrolled = true;
			g.Commander = commander.name;
			g.Slot = FreeSlot(commander.name);
			g.BarX = barracks.Position.X; g.BarY = barracks.Position.Y;
			g.DaysUnpaid = 0;
			g.ResetRuntime();
			_stamp = uint.MaxValue; // the slot is taken: the cache must see it at once (the avatar + its kit are created by MaintainAvatars)
			npc.netAlways = true; npc.netUpdate = true;
			RosterSystem.SendToPlayer(commander, LevyUtil.T("Officers.Msg.Commissioned", NameOf(npc), LevyUtil.T("Officers.Kit." + def.Key + ".Name"), LevyUtil.Coins(fee)));
			return true;
		}

		/// <summary>Sends an officer back to town life. Only his own commander may (no refund). False when he is not that player's officer.</summary>
		public bool Dismiss(Player commander, NPC npc)
		{
			if (!LevyUtil.IsServerOrSingle || commander == null || npc == null || !npc.active) return false;
			OfficerGlobalNPC g = Data(npc);
			if (g == null || !g.Enrolled || !string.Equals(g.Commander, commander.name, StringComparison.Ordinal)) return false;
			Release(npc, "Dismissed");
			return true;
		}

		/// <summary>
		/// Ends a commission for any reason: kit hook, data cleared, the NPC handed back to the town AI (and walked home if he is far). <paramref name="reason"/> is a
		/// key under Mods.Levy.Officers.Msg ("Dismissed", "Lapsed", "Invalid" for a silent clean-up), told to the commander if online.
		/// </summary>
		internal void Release(NPC npc, string reason)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			OfficerGlobalNPC g = Data(npc);
			if (g == null || !g.Enrolled) return;
			string commanderName = g.Commander;
			int ci = LevyUtil.FindPlayerByName(commanderName);
			Player commander = ci >= 0 ? Main.player[ci] : null;
			g.Enrolled = false; g.Commander = ""; g.Slot = 0; g.DaysUnpaid = 0;
			g.ResetRuntime();
			RemoveAvatarFor(npc);  // despawn the avatar (fires the kit's OnDismissed) and un-park the town NPC where it stood
			npc.netAlways = false; npc.netUpdate = true;
			_stamp = uint.MaxValue;
			if (commander != null && reason != "Invalid") RosterSystem.SendToPlayer(commander, LevyUtil.T("Officers.Msg." + reason, NameOf(npc)));
		}

		/// <summary>The town NPC itself was killed (e.g. while the commander was offline and it walked town as a civilian): tell the commander. The commission dies with it.</summary>
		internal void NotifyFallen(NPC npc, OfficerGlobalNPC g)
		{
			if (!LevyUtil.IsServerOrSingle || !g.Enrolled) return;
			int ci = LevyUtil.FindPlayerByName(g.Commander);
			if (ci >= 0) RosterSystem.SendToPlayer(Main.player[ci], LevyUtil.T("Officers.Msg.Fallen", NameOf(npc)));
			g.Enrolled = false;
			RemoveAvatarFor(npc);
			_stamp = uint.MaxValue;
		}

		/// <summary>
		/// The avatar fell in battle: the officer is lost. End the commission, tell the commander (the existing "fallen" line) and un-park the town NPC where it died, so it
		/// goes back to civilian life. Called from the avatar's death (server / single player). The avatar's kit OnDismissed has already run.
		/// </summary>
		internal void OnAvatarFell(NPC townNpc, SoldierNPC avatar)
		{
			if (!LevyUtil.IsServerOrSingle || townNpc == null) return;
			OfficerGlobalNPC g = Data(townNpc);
			if (g == null) { return; }
			if (g.Enrolled)
			{
				int ci = LevyUtil.FindPlayerByName(g.Commander);
				if (ci >= 0) RosterSystem.SendToPlayer(Main.player[ci], LevyUtil.T("Officers.Msg.Fallen", NameOf(townNpc)));
				g.Enrolled = false; g.Commander = ""; g.Slot = 0; g.DaysUnpaid = 0;
				g.ResetRuntime();
			}
			UnparkTownNpc(townNpc);
			townNpc.netAlways = false; townNpc.netUpdate = true;
			_stamp = uint.MaxValue;
		}

		internal static string NameOf(NPC npc) => string.IsNullOrEmpty(npc.GivenName) ? npc.TypeName : npc.GivenName;

		/// <summary>The barracks that pays this officer: the one that commissioned him if it still stands, else the nearest to him.</summary>
		public BarracksTE PayingBarracks(NPC npc, OfficerGlobalNPC g)
		{
			BarracksTE b = SettlementSystem.Instance.BarracksAt(new Point16(g.BarX, g.BarY));
			return b ?? SettlementSystem.Instance.NearestBarracks(npc.Center);
		}

		internal OfficerContext MakeContext(NPC npc, OfficerGlobalNPC g, OfficerDef def, Player commander) => new OfficerContext
		{
			Npc = npc, Def = def, Commander = commander, State = g.KitState, Barracks = PayingBarracks(npc, g), Slot = g.Slot,
		};

		// =====================================================================================================
		// Per-tick (server / single player): dawn payroll and housekeeping
		// =====================================================================================================
		public override void PostUpdateEverything()
		{
			if (!LevyUtil.IsServerOrSingle || Main.gameMenu) return;
			MaintainAvatars();
			// Payroll rides on the roster's day counter so officers are paid on the same dawn as the soldiers (after them: soldiers' pay is read first).
			int day = RosterSystem.Instance.DayNumber;
			if (!_dayInit) { _dayInit = true; _lastDay = day; }
			else if (day != _lastDay) { _lastDay = day; PayOfficers(); }

			if (++_housekeepingTimer >= 60) { _housekeepingTimer = 0; Housekeeping(); }
		}

		// =====================================================================================================
		// Avatar lifecycle (server / single player): spawn one per enrolled + online commission, cull the rest.
		// =====================================================================================================
		/// <summary>
		/// Keeps exactly one live avatar per enrolled commission whose commander is online: spawns the missing ones, and despawns any avatar whose
		/// commission ended, whose commander left, whose town NPC is gone, or that is a duplicate. The avatar itself parks and co-locates the town NPC.
		/// </summary>
		private void MaintainAvatars()
		{
			Refresh();
			// cull invalid / duplicate avatars (a duplicate is any officer-mode body that is not the one Refresh kept for its town NPC)
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!(n.active && n.ModNPC is SoldierNPC s && s.OfficerMode)) continue;
				NPC town = s.OfficerTownNpc;
				OfficerGlobalNPC g = town != null ? Data(town) : null;
				bool valid = g != null && g.Enrolled && OfficerRegistry.IsOfficerType(town.type)
							 && LevyUtil.FindPlayerByName(g.Commander) >= 0
							 && _avatars.TryGetValue(town.whoAmI, out var primary) && ReferenceEquals(primary, s);
				if (!valid) DespawnAvatar(s, town);
			}
			// spawn for enrolled + online commissions that have no avatar yet
			for (int k = 0; k < _enrolled.Count; k++)
			{
				NPC town = _enrolled[k];
				OfficerGlobalNPC g = Data(town);
				if (g == null || !g.Enrolled || !OfficerRegistry.TryGet(town.type, out OfficerDef def)) continue;
				int ci = LevyUtil.FindPlayerByName(g.Commander);
				if (ci < 0 || _avatars.ContainsKey(town.whoAmI)) continue;
				SpawnAvatar(town, def, Main.player[ci]);
			}
		}

		private void SpawnAvatar(NPC town, OfficerDef def, Player commander)
		{
			int type = ModContent.NPCType<SoldierNPC>();
			if (type <= 0) return;
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:OfficerAvatar"), (int)town.Center.X, (int)town.Center.Y, type);
			if (idx < 0 || idx >= Main.maxNPCs) return;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not SoldierNPC avatar) { n.active = false; return; }
			avatar.OfficerSetup(town, def, commander.name);
			n.Bottom = town.Bottom;
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			_avatars[town.whoAmI] = avatar;
			_stamp = uint.MaxValue; // the next Refresh binds the body to it
		}

		/// <summary>Removes the avatar of one town NPC, if any, and un-parks it. Used when a commission ends (dismiss / lapse / the town NPC itself dies).</summary>
		private void RemoveAvatarFor(NPC town)
		{
			if (town == null) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC s && s.OfficerMode && s.OfficerTownIndex == town.whoAmI) DespawnAvatar(s, town);
			}
			UnparkTownNpc(town);
		}

		private void DespawnAvatar(SoldierNPC avatar, NPC town)
		{
			if (avatar != null && avatar.NPC.active)
			{
				avatar.Officer_Dismissed();       // the kit's one-shot dismissal hook
				avatar.NPC.life = 0;
				avatar.NPC.active = false;
				avatar.NPC.netUpdate = true;
				if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, avatar.NPC.whoAmI);
			}
			if (town != null) UnparkTownNpc(town);
			_stamp = uint.MaxValue;
		}

		/// <summary>Hands a parked town NPC back to normal life: clears the inert flags and stops the per-tick stream.</summary>
		private static void UnparkTownNpc(NPC town)
		{
			if (town == null || !town.active) return;
			town.dontTakeDamage = false; town.dontTakeDamageFromHostiles = false; town.immortal = false;
			town.netAlways = false;
			town.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, town.whoAmI);
		}

		private void PayOfficers()
		{
			Refresh();
			var paid = new Dictionary<string, long>();
			var touched = new HashSet<BarracksTE>();
			var snapshot = new List<NPC>(_enrolled); // Release edits the cache's source flags
			foreach (NPC npc in snapshot)
			{
				OfficerGlobalNPC g = Data(npc);
				if (g == null || !g.Enrolled) continue;
				if (!OfficerRegistry.TryGet(npc.type, out OfficerDef def)) continue; // Housekeeping clears the orphan
				long wage = Wage(def);
				BarracksTE b = PayingBarracks(npc, g);
				if (b != null && EconomySystem.TryPay(b, wage))
				{
					g.DaysUnpaid = 0; touched.Add(b);
					paid[g.Commander] = (paid.TryGetValue(g.Commander, out long p) ? p : 0) + wage;
				}
				else
				{
					g.DaysUnpaid++;
					int ci = LevyUtil.FindPlayerByName(g.Commander);
					if (g.DaysUnpaid >= LevyConstants.Officer.MaxDaysUnpaid) Release(npc, "Lapsed");
					else if (ci >= 0) RosterSystem.SendToPlayer(Main.player[ci], LevyUtil.T("Officers.Msg.Unpaid", NameOf(npc)));
				}
				npc.netUpdate = true;
			}
			foreach (BarracksTE b in touched) RosterSystem.SyncBarracks(b);
			foreach (var kv in paid)
			{
				int ci = LevyUtil.FindPlayerByName(kv.Key);
				if (ci >= 0 && kv.Value > 0) RosterSystem.SendToPlayer(Main.player[ci], LevyUtil.T("Officers.Msg.Payday", LevyUtil.Coins(kv.Value)));
			}
		}

		/// <summary>Clears commissions that cannot work any more: a type that is no longer registered, a slot shared by two NPCs, or one truly out of range. The range is floored at the officers already serving (<see cref="SlotCap"/>), so a cap recompute never orphans a standing officer.</summary>
		private void Housekeeping()
		{
			Refresh();
			if (_enrolled.Count == 0) return;
			var seen = new HashSet<(string, int)>();
			var snapshot = new List<NPC>(_enrolled);
			foreach (NPC npc in snapshot)
			{
				OfficerGlobalNPC g = Data(npc);
				if (g == null || !g.Enrolled) continue;
				if (!OfficerRegistry.IsOfficerType(npc.type) || string.IsNullOrEmpty(g.Commander)) { Release(npc, "Invalid"); continue; }
				if (g.Slot < 0 || g.Slot >= SlotCap(g.Commander) || !seen.Add((g.Commander, g.Slot))) Release(npc, "Lapsed");
			}
		}
	}
}
