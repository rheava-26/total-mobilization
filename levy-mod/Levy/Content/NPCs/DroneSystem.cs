using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using D = Levy.Common.LevyConstants.Drones;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The combat-drone garrison. Two jobs, both modeled on <see cref="SummonedUnitSystem"/> / <see cref="Common.Officers.SummonerKit"/>:
	///
	/// 1. A per-tick cache of the live drones as targeting bodies, so enemies chase them exactly as they chase soldiers (<see cref="EnemyTargetingSystem.PreUpdateNPCs"/> reads
	///    <see cref="ActiveBodies"/>). Rebuilt lazily on read on every netmode, so it never depends on ModSystem hook order.
	/// 2. The garrison itself (server / single player, and only once A Guide to Engineering is studied): each Barracks with a linked Armory keeps up to
	///    <see cref="D.PerBarracksCap"/> drones aloft, ONE per SURPLUS ranged or magic weapon in that Armory, launching one every <see cref="D.RespawnCadenceTicks"/> until it is full or the
	///    spare weapons run out. Drones are not in the roster and cost no command points, manpower or money; the weapon is the whole price.
	///
	/// SOLDIERS GET FIRST PICK, twice over. (1) The weapon lent is never one the army could draw: <see cref="ArmoryTE.LendDroneWeapon"/> ranks every weapon the soldiers could take and leaves the
	/// best <see cref="SoldierReserve"/> of them alone (one per Regular still without a weapon, one per open militia seat of every Barracks the Armory serves). (2) Should that estimate ever be
	/// short, a soldier with an empty hand recalls a drone's weapon on the spot (<see cref="ArmoryTE.ReclaimLentWeapon"/>, called from the armory's own issue path): the drone stands down.
	///
	/// THROTTLED: the sweep visits ONE Barracks every <see cref="D.SweepIntervalTicks"/> ticks, round robin; per visit it makes one pass over the NPC slots, a few dictionary lookups and, only
	/// when a launch is due, one scan of the Armory's racks. A Barracks that is full, or on cooldown, or found nothing to lend (it then waits <see cref="D.NoSurplusBackoffTicks"/>) costs a
	/// handful of comparisons. Nothing here runs per drone per tick: each drone's own brain is a throttled scan like a summoned unit's.
	/// </summary>
	public class DroneSystem : ModSystem
	{
		public static DroneSystem Instance => ModContent.GetInstance<DroneSystem>();

		// ---- the body cache ----
		private uint _stamp = uint.MaxValue;
		private readonly List<ITargetBody> _active = new();

		// ---- the garrison sweep (server / single player) ----
		private readonly List<int> _barracksIds = new();
		private uint _listStamp;
		private int _cursor, _sweepTimer;
		/// <summary>Barracks id -> the first tick it may launch again (after a launch, or after finding nothing to lend).</summary>
		private readonly Dictionary<int, uint> _nextLaunch = new();
		private readonly List<DroneNPC> _all = new(), _mine = new();
		private uint _lastWarnTick;
		private bool _announcedFirst;

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();

		private void Reset()
		{
			_stamp = uint.MaxValue; _active.Clear();
			_barracksIds.Clear(); _listStamp = 0; _cursor = 0; _sweepTimer = 0;
			_nextLaunch.Clear(); _all.Clear(); _mine.Clear();
			_lastWarnTick = 0; _announcedFirst = false;
		}

		// =====================================================================================================
		// Body cache
		// =====================================================================================================
		private void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp == now) return;
			_stamp = now;
			_active.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is DroneNPC d && d.IsAlive) _active.Add(d);
			}
		}

		/// <summary>The live drones this tick, as targeting bodies. Read-only; valid for the current tick.</summary>
		public IReadOnlyList<ITargetBody> ActiveBodies { get { Refresh(); return _active; } }

		// =====================================================================================================
		// The garrison
		// =====================================================================================================
		public override void PostUpdateNPCs()
		{
			if (!LevyUtil.IsServerOrSingle) return;
			GuideSystem guides = GuideSystem.Instance;
			if (guides == null || !guides.Engineering) return; // no guide, no garrison (and a single bool read is all this costs)
			if (--_sweepTimer > 0) return;
			_sweepTimer = D.SweepIntervalTicks;
			try { SweepOne(); }
			catch (Exception e)
			{
				// A fault in the garrison must never break the tick. Say so at most every ten seconds.
				uint now = Main.GameUpdateCount;
				if (_lastWarnTick == 0 || now - _lastWarnTick > 600u) { _lastWarnTick = now; Mod.Logger.Warn("Levy: the drone garrison sweep failed: " + e); }
			}
		}

		/// <summary>Visits the next Barracks in the round robin.</summary>
		private void SweepOne()
		{
			uint now = Main.GameUpdateCount;
			if (_barracksIds.Count == 0 || now < _listStamp || now - _listStamp >= (uint)D.BarracksListRefreshTicks) RebuildList(now);
			if (_barracksIds.Count == 0) return;
			if (_cursor >= _barracksIds.Count) _cursor = 0;
			int id = _barracksIds[_cursor++];
			if (!(TileEntity.ByID.TryGetValue(id, out TileEntity te) && te is BarracksTE barracks)) return; // gone since the list was built; the next rebuild drops it
			Garrison(barracks, now);
		}

		private void RebuildList(uint now)
		{
			_listStamp = now;
			_barracksIds.Clear();
			foreach (TileEntity te in TileEntity.ByID.Values) if (te is BarracksTE b) _barracksIds.Add(b.ID);
			if (_nextLaunch.Count > _barracksIds.Count)
			{
				var gone = new List<int>();
				foreach (int id in _nextLaunch.Keys) if (!_barracksIds.Contains(id)) gone.Add(id);
				foreach (int id in gone) _nextLaunch.Remove(id);
			}
		}

		private void Garrison(BarracksTE barracks, uint now)
		{
			int activeNpcs = CollectDrones();
			ArmoryTE armory = SettlementSystem.Instance.LinkedArmory(barracks);

			// This Barracks' drones. One tied to an Armory this Barracks no longer links to stands down, and its loan goes home.
			_mine.Clear();
			for (int i = 0; i < _all.Count; i++)
			{
				DroneNPC d = _all[i];
				if (d.BarracksId != barracks.ID) continue;
				if (armory == null || d.ArmoryId != armory.ID) { d.Dismantle(); continue; }
				_mine.Add(d);
			}
			if (armory == null) return;

			ReconcileLoans(armory);

			if (_mine.Count >= D.PerBarracksCap) return;
			if (_nextLaunch.TryGetValue(barracks.ID, out uint next) && now < next) return;
			if (Main.maxNPCs - activeNpcs < D.NpcSlotReserve) return; // never crowd soldiers or an invasion out of the NPC array

			Item weapon = armory.LendDroneWeapon(ArmoryReserve(armory));
			if (weapon == null) { _nextLaunch[barracks.ID] = now + (uint)D.NoSurplusBackoffTicks; return; }
			Item ammo = DroneArsenal.TakesAmmo(weapon) ? armory.LendDroneAmmo(weapon.useAmmo, D.AmmoKeepRounds) : null; // none to spare: the drone fires the default round
			if (Launch(barracks, armory, weapon, ammo, FreeSlot(_mine)) < 0)
			{
				if (ammo != null) armory.ReturnLent(ammo);
				armory.ReturnLent(weapon);
				_nextLaunch[barracks.ID] = now + (uint)D.NoSurplusBackoffTicks;
				return;
			}
			_nextLaunch[barracks.ID] = now + (uint)D.RespawnCadenceTicks;
		}

		/// <summary>One pass over the NPC slots: every live drone into <see cref="_all"/>, and the number of active NPCs (for the free-slot guard).</summary>
		private int CollectDrones()
		{
			_all.Clear();
			int active = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				active++;
				if (n.ModNPC is DroneNPC d && d.IsBound) _all.Add(d);
			}
			return active;
		}

		/// <summary>
		/// Returns every loan on this Armory's ledger that no live drone is carrying: a drone that vanished by some path that did not file its weapon back (a slot reused, another mod removing
		/// it) leaves the loan owed, and this is where it is paid. The weapon was never out of the Armory's keeping, so nothing is ever lost.
		/// </summary>
		private void ReconcileLoans(ArmoryTE armory)
		{
			for (int i = armory.Lent.Count - 1; i >= 0; i--)
			{
				Item item = armory.Lent[i];
				bool carried = false;
				for (int k = 0; k < _all.Count; k++)
				{
					DroneNPC d = _all[k];
					if (d.ArmoryId == armory.ID && (ReferenceEquals(d.Weapon, item) || ReferenceEquals(d.Ammo, item))) { carried = true; break; }
				}
				if (!carried) armory.ReturnLent(item);
			}
		}

		/// <summary>The lowest formation slot not taken by one of this Barracks' drones.</summary>
		private static int FreeSlot(List<DroneNPC> mine)
		{
			uint used = 0;
			for (int i = 0; i < mine.Count; i++) { int s = mine[i].Slot; if (s >= 0 && s < 32) used |= 1u << s; }
			for (int s = 0; s < D.PerBarracksCap && s < 32; s++) if ((used & (1u << s)) == 0) return s;
			return mine.Count % Math.Max(1, D.PerBarracksCap);
		}

		/// <summary>Spawns and binds one drone above its Barracks. Returns its whoAmI, or -1 when no slot or no usable shot (the caller then returns the loan).</summary>
		private int Launch(BarracksTE barracks, ArmoryTE armory, Item weapon, Item ammo, int slot)
		{
			int type = ModContent.NPCType<DroneNPC>();
			if (type <= 0) return -1;
			Vector2 spawn = DroneNPC.LaunchPoint(barracks);
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Levy:Drone"), (int)spawn.X, (int)(spawn.Y + D.BodyHeight * 0.5f), type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not DroneNPC drone) { n.active = false; return -1; }
			Vector2 anchor = DroneNPC.AnchorFor(barracks);
			drone.Bind(barracks.ID, armory.ID, weapon, ammo, slot, PickCommander(spawn), anchor);
			n.Center = spawn;
			n.velocity = new Vector2(0f, -1f);
			n.direction = slot % 2 == 0 ? 1 : -1; n.spriteDirection = n.direction;
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			if (!_announcedFirst) { _announcedFirst = true; Mod.Logger.Info($"Levy: first combat drone launched from a Barracks, mounting {weapon.Name}."); }
			return idx;
		}

		// =====================================================================================================
		// Soldiers first
		// =====================================================================================================
		/// <summary>
		/// How many of an Armory's best weapons a drone may not touch: the army's own need, summed over every Barracks that draws from it (two Barracks may share an Armory). Each counts one
		/// per Regular still without a weapon (a Regular who is armed holds his weapon already) and one per open militia seat; militia are raised only where the town can field them.
		/// A deliberate over-estimate: soldiers also draw from the weapon rows and may pass over a poor weapon, and the recall in <see cref="ArmoryTE.IssueGear"/> backs it up.
		/// </summary>
		private int ArmoryReserve(ArmoryTE armory)
		{
			int reserve = 0;
			SettlementSystem settlement = SettlementSystem.Instance;
			for (int i = 0; i < _barracksIds.Count; i++)
			{
				if (!(TileEntity.ByID.TryGetValue(_barracksIds[i], out TileEntity te) && te is BarracksTE b)) continue;
				if (!ReferenceEquals(settlement.LinkedArmory(b), armory)) continue;
				reserve += SoldierReserve(b);
			}
			return reserve;
		}

		/// <summary>Soldiers of one Barracks who may still need a weapon from its Armory: its Regulars without one, and the militia seats it could still fill.</summary>
		private static int SoldierReserve(BarracksTE barracks)
		{
			int need = 0;
			RosterSystem roster = RosterSystem.Instance;
			if (roster != null)
				foreach (SoldierRecord r in roster.Regulars)
					if (r != null && r.HomeBarracksId == barracks.ID && !r.Reserve && (r.Gear == null || !r.Gear.HasWeapon)) need++;

			int fieldCap = Math.Min(Math.Max(0, barracks.Manpower), Math.Max(1, LevyConfig.Get?.MilitiaFieldCap ?? 10));
			int seats = Math.Max(0, fieldCap - barracks.Militia.Count);
			if (seats > 0 && SettlementSystem.Instance.TownNpcsOf(barracks).Count < LevyConstants.MinTownNpcsForMilitia) seats = 0; // too few townsfolk to raise a militia
			return need + seats;
		}

		// =====================================================================================================
		// Credit
		// =====================================================================================================
		/// <summary>The player a drone's kills are credited to: the nearest active player to <paramref name="near"/>, a living one before a dead one. -1 when nobody is on.</summary>
		internal static int PickCommander(Vector2 near)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active) continue;
				float d = Vector2.DistanceSquared(p.Center, near);
				if (p.dead) d += 1e12f;
				if (d < bestD) { bestD = d; best = i; }
			}
			return best;
		}
	}
}
