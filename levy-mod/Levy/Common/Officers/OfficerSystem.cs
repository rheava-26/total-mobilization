using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.Util;
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
		private int _lastDay = -1; private bool _dayInit;
		private int _housekeepingTimer;

		public override void Load() => OfficerNet.Register();

		/// <summary>Registers the officer definitions once every mod's content exists (NPCID.Sets and modded NPC types are final by now).</summary>
		public override void PostSetupContent() { OfficerRegistry.Clear(); OfficerRegistry.RegisterAll(); }

		public override void Unload() { OfficerRegistry.Clear(); _enrolled.Clear(); _active.Clear(); }

		public override void OnWorldLoad() { Reset(); }
		public override void OnWorldUnload() { Reset(); }
		private void Reset() { _stamp = uint.MaxValue; _enrolled.Clear(); _active.Clear(); _dayInit = false; _lastDay = -1; _housekeepingTimer = 0; }

		// =====================================================================================================
		// Queries (safe on clients)
		// =====================================================================================================
		public static OfficerGlobalNPC Data(NPC npc) => npc != null && npc.TryGetGlobalNPC(out OfficerGlobalNPC g) ? g : null;

		private void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp == now) return;
			_stamp = now;
			_enrolled.Clear(); _active.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.townNPC) continue;
				OfficerGlobalNPC g = Data(n);
				if (g == null || !g.Enrolled) continue;
				_enrolled.Add(n);
				if (n.life > 0 && OfficerRegistry.IsOfficerType(n.type) && LevyUtil.FindPlayerByName(g.Commander) >= 0) _active.Add(g.EnsureBody(n));
			}
		}

		/// <summary>Every conscripted town NPC in the world, whoever commands it. Read-only; valid for the current tick.</summary>
		public IReadOnlyList<NPC> Enrolled { get { Refresh(); return _enrolled; } }

		/// <summary>Officers that are on duty this tick (alive, a registered type, commander online): the ones with a targeting body.</summary>
		public IReadOnlyList<OfficerBody> ActiveBodies { get { Refresh(); return _active; } }

		/// <summary>The targeting body of an officer that is on duty, else null.</summary>
		public OfficerBody BodyOf(NPC npc)
		{
			if (npc == null || !npc.active || !npc.townNPC || npc.life <= 0) return null;
			OfficerGlobalNPC g = Data(npc);
			if (g == null || !g.Enrolled || !OfficerRegistry.IsOfficerType(npc.type) || LevyUtil.FindPlayerByName(g.Commander) < 0) return null;
			return g.EnsureBody(npc);
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

		/// <summary>Lowest free command slot of this commander, or -1 when all <see cref="LevyConstants.Officer.MaxSlots"/> are taken.</summary>
		public int FreeSlot(string commander)
		{
			for (int s = 0; s < LevyConstants.Officer.MaxSlots; s++) if (InSlot(commander, s) == null) return s;
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
			g.Rt.KitState = def.Kit?.CreateState();
			_stamp = uint.MaxValue; // the slot is taken: the cache must see it at once
			def.Kit?.OnMobilized(MakeContext(npc, g, def, commander));
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
			if (OfficerRegistry.TryGet(npc.type, out OfficerDef def)) def.Kit?.OnDismissed(MakeContext(npc, g, def, commander));
			g.Enrolled = false; g.Commander = ""; g.Slot = 0; g.DaysUnpaid = 0;
			g.Disengage(npc);
			_stamp = uint.MaxValue;
			if (commander != null && reason != "Invalid") RosterSystem.SendToPlayer(commander, LevyUtil.T("Officers.Msg." + reason, NameOf(npc)));
		}

		/// <summary>The officer was killed: tell the commander. (The commission needs no clean-up; it died with the NPC.)</summary>
		internal void NotifyFallen(NPC npc, OfficerGlobalNPC g)
		{
			if (!LevyUtil.IsServerOrSingle || !g.Enrolled) return;
			int ci = LevyUtil.FindPlayerByName(g.Commander);
			if (ci >= 0) RosterSystem.SendToPlayer(Main.player[ci], LevyUtil.T("Officers.Msg.Fallen", NameOf(npc)));
			if (OfficerRegistry.TryGet(npc.type, out OfficerDef def)) def.Kit?.OnDismissed(MakeContext(npc, g, def, ci >= 0 ? Main.player[ci] : null));
			g.Enrolled = false;
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
			// Payroll rides on the roster's day counter so officers are paid on the same dawn as the soldiers (after them: soldiers' pay is read first).
			int day = RosterSystem.Instance.DayNumber;
			if (!_dayInit) { _dayInit = true; _lastDay = day; }
			else if (day != _lastDay) { _lastDay = day; PayOfficers(); }

			if (++_housekeepingTimer >= 60) { _housekeepingTimer = 0; Housekeeping(); }
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

		/// <summary>Clears commissions that cannot work any more: a type that is no longer registered, a slot out of range (MaxSlots lowered) or shared by two NPCs.</summary>
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
				if (g.Slot < 0 || g.Slot >= LevyConstants.Officer.MaxSlots || !seen.Add((g.Commander, g.Slot))) Release(npc, "Lapsed");
			}
		}
	}
}
