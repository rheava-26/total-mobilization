using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using Mechanization.Common;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// The combat-drone garrison and its orders. Three jobs:
	///
	/// 1. Feeds the live drones into Levy's <see cref="EnemyTargeting.Bodies"/> every tick (the seam <see cref="TurretSystem"/> uses), so enemies chase them exactly as they chase soldiers.
	/// 2. The garrison itself (server / single player, and only once Levy's A Guide to Engineering is studied): each Barracks with a linked Armory keeps up to <see cref="D.PerBarracksCap"/>
	///    drones, ONE per SURPLUS ranged or magic weapon in that Armory, launching one every <see cref="D.RespawnCadenceTicks"/> until it is full or the spare weapons run out. A drone that
	///    stood down on its own (its weapon was taken, or its Armory changed) is re-armed first. Drones are not in the roster and cost no command points, manpower or money.
	/// 3. The orders: RECALL sends a drone home to land and file its weapon back into the Armory; RECALL ALL does that for the whole Barracks. A recalled drone HOLDS its Barracks' garrison (no
	///    new launches, no re-arming) so the player can swap what the Armory holds; LAUNCH / LAUNCH ALL release the hold and re-arm the parked drones with the best spare weapon. The orders come from the drone's chat panel or the
	///    keybind (<see cref="DroneControls"/>); on a multiplayer client they travel as a <see cref="MechPacket"/> and are validated here.
	///
	/// SOLDIERS GET FIRST PICK, twice over. (1) The weapon lent is never one the army could draw: <see cref="ArmoryTE.LendDroneWeapon"/> ranks every weapon the soldiers could take and leaves the
	/// best <see cref="ArmoryReserve"/> of them alone (one per Regular still without a weapon, one per open militia seat of every Barracks the Armory serves). (2) Should that estimate ever be
	/// short, a soldier with an empty hand recalls a drone's weapon on the spot (<see cref="ArmoryTE.ReclaimLentWeapon"/>, called from the armory's own issue path): the drone lands, parked.
	///
	/// THROTTLED: the sweep visits ONE Barracks every <see cref="D.SweepIntervalTicks"/> ticks, round robin; per visit it makes one pass over the NPC slots, a few dictionary lookups and, only
	/// when a launch is due, one scan of the Armory's racks. Nothing here runs per drone per tick.
	/// </summary>
	public class DroneSystem : ModSystem
	{
		public static DroneSystem Instance => ModContent.GetInstance<DroneSystem>();

		// ---- the garrison sweep (server / single player) ----
		private readonly List<int> _barracksIds = new();
		private uint _listStamp;
		private int _cursor, _sweepTimer;
		/// <summary>Barracks id -> the first tick it may launch again (after a launch, or after finding nothing to lend).</summary>
		private readonly Dictionary<int, uint> _nextLaunch = new();
		private readonly List<DroneNPC> _all = new(), _mine = new();
		private static readonly uint[] _lastOrder = new uint[Main.maxPlayers + 1];
		private uint _lastWarnTick;
		private bool _announcedFirst;

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();

		private void Reset()
		{
			_barracksIds.Clear(); _listStamp = 0; _cursor = 0; _sweepTimer = 0;
			_nextLaunch.Clear(); _all.Clear(); _mine.Clear();
			Array.Clear(_lastOrder, 0, _lastOrder.Length);
			_lastWarnTick = 0; _announcedFirst = false;
		}

		// =====================================================================================================
		// Enemy aggro: feed live drones into Levy's targeting body list every tick (Levy rebuilds it earlier; Mechanization sorts after Levy).
		// =====================================================================================================
		public override void PreUpdateNPCs()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is DroneNPC d && d.IsAlive)
					EnemyTargeting.Bodies.Add(d);
			}
		}

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
				if (_lastWarnTick == 0 || now - _lastWarnTick > 600u) { _lastWarnTick = now; Mod.Logger.Warn("Mechanization: the drone garrison sweep failed: " + e); }
			}
		}

		/// <summary>Visits the next Barracks in the round robin.</summary>
		private void SweepOne()
		{
			uint now = Main.GameUpdateCount;
			EnsureList(now);
			if (_barracksIds.Count == 0) return;
			if (_cursor >= _barracksIds.Count) _cursor = 0;
			int id = _barracksIds[_cursor++];
			if (!(TileEntity.ByID.TryGetValue(id, out TileEntity te) && te is BarracksTE barracks)) return; // gone since the list was built; the next rebuild drops it
			Garrison(barracks, now);
		}

		private void EnsureList(uint now)
		{
			if (_barracksIds.Count == 0 || now < _listStamp || now - _listStamp >= (uint)D.BarracksListRefreshTicks) RebuildList(now);
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
			ArmoryTE armory = ArmoryLink.OfBarracks(barracks);

			// This Barracks' drones. One armed from an Armory this Barracks no longer links to stands down, and its loan goes home.
			_mine.Clear();
			for (int i = 0; i < _all.Count; i++)
			{
				DroneNPC d = _all[i];
				if (d.BarracksId != barracks.ID) continue;
				if (armory == null || (d.Weapon != null && d.ArmoryId != armory.ID)) { d.Dismantle(); continue; }
				d.ArmoryId = armory.ID; // an unarmed drone simply adopts the Armory the Barracks links to now
				_mine.Add(d);
			}
			if (armory == null) return;

			ReconcileLoans(armory);

			// A drone the player recalled (flying home or parked) holds the whole garrison: no new launch and no re-arming, so the weapon that came back is not lent straight out again while the
			// player is still working the Armory. A launch order (or the drone being destroyed) lifts the hold.
			for (int i = 0; i < _mine.Count; i++) if (_mine[i].Held) return;
			if (_nextLaunch.TryGetValue(barracks.ID, out uint next) && now < next) return;

			// A drone that stood down on its own (its weapon went to a soldier) is re-armed before any new one is launched.
			for (int i = 0; i < _mine.Count; i++)
			{
				DroneNPC d = _mine[i];
				if (!d.IsParked || d.Held) continue;
				if (TryArm(d, armory)) _nextLaunch[barracks.ID] = now + (uint)D.RespawnCadenceTicks;
				else _nextLaunch[barracks.ID] = now + (uint)D.NoSurplusBackoffTicks;
				return;
			}

			if (_mine.Count >= D.PerBarracksCap) return;
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

		/// <summary>One pass over the NPC slots: every bound drone into <see cref="_all"/>, and the number of active NPCs (for the free-slot guard).</summary>
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

		/// <summary>Spawns and binds one drone on its pad beside its Barracks; it takes off from there. Returns its whoAmI, or -1 when no slot or no usable shot (the caller then returns the loan).</summary>
		private int Launch(BarracksTE barracks, ArmoryTE armory, Item weapon, Item ammo, int slot)
		{
			int type = ModContent.NPCType<DroneNPC>();
			if (type <= 0) return -1;
			Vector2 pad = DroneNPC.PadFor(barracks, slot);
			int idx = NPC.NewNPC(new EntitySource_WorldEvent("Mechanization:Drone"), (int)pad.X, (int)(pad.Y + D.BodyHeight * 0.5f), type);
			if (idx < 0 || idx >= Main.maxNPCs) return -1;
			NPC n = Main.npc[idx];
			if (n.ModNPC is not DroneNPC drone) { n.active = false; return -1; }
			if (!drone.Bind(barracks, armory.ID, weapon, ammo, slot, ShotKit.PickCommander(pad))) { n.active = false; return -1; }
			n.netUpdate = true;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
			if (!_announcedFirst) { _announcedFirst = true; Mod.Logger.Info($"Mechanization: first combat drone launched from a Barracks, mounting {weapon.Name}."); }
			return idx;
		}

		/// <summary>Lends the best spare weapon (and a round) to a parked drone and sends it up. False, with nothing left lent, when the Armory has nothing spare or the weapon yields no shot.</summary>
		private bool TryArm(DroneNPC drone, ArmoryTE armory)
		{
			EnsureList(Main.GameUpdateCount);
			Item weapon = armory.LendDroneWeapon(ArmoryReserve(armory));
			if (weapon == null) return false;
			Item ammo = DroneArsenal.TakesAmmo(weapon) ? armory.LendDroneAmmo(weapon.useAmmo, D.AmmoKeepRounds) : null;
			if (!drone.Arm(armory.ID, weapon, ammo))
			{
				if (ammo != null) armory.ReturnLent(ammo);
				armory.ReturnLent(weapon);
				return false;
			}
			return true;
		}

		// =====================================================================================================
		// Soldiers first
		// =====================================================================================================
		/// <summary>
		/// How many of an Armory's best weapons a drone may not touch: the army's own need, summed over every Barracks that draws from it (two Barracks may share an Armory). Each counts one
		/// per Regular still without a weapon (a Regular who is armed holds his weapon already) and one per open militia seat; militia are raised only where the town can field them.
		/// A deliberate over-estimate: soldiers also draw from the weapon rows and may pass over a poor weapon, and the recall in <see cref="ArmoryTE"/>'s issue path backs it up.
		/// </summary>
		private int ArmoryReserve(ArmoryTE armory)
		{
			int reserve = 0;
			SettlementSystem settlement = SettlementSystem.Instance;
			for (int i = 0; i < _barracksIds.Count; i++)
			{
				if (!(TileEntity.ByID.TryGetValue(_barracksIds[i], out TileEntity te) && te is BarracksTE b)) continue;
				if (!ReferenceEquals(ArmoryLink.OfBarracks(b), armory)) continue;
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
			if (seats > 0 && SettlementSystem.Instance.TownNpcsOf(barracks).Count < Levy.Common.LevyConstants.MinTownNpcsForMilitia) seats = 0; // too few townsfolk to raise a militia
			return need + seats;
		}

		// =====================================================================================================
		// Orders
		// =====================================================================================================
		/// <summary>
		/// Server / single player: one drone order from a player who clicked a button in a drone's chat panel. Validates the player (alive, near the drone, not spamming) and the drone, then acts on that
		/// drone or on every drone of its Barracks. Tells the player what happened.
		/// </summary>
		public bool HandleOrder(Player player, DroneOrder order, int npcIndex)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active || player.dead) return false;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return false;
			NPC npc = Main.npc[npcIndex];
			if (!npc.active || npc.ModNPC is not DroneNPC drone || !drone.IsBound) return false;
			if (Vector2.DistanceSquared(player.Center, npc.Center) > D.OrderReachPx * D.OrderReachPx) return false;
			if (!OrderGap(player)) return false;

			switch (order)
			{
				case DroneOrder.Recall: Recall(drone); Say(player, "Mods.Mechanization.UI.DroneRecalling", 1); return true;
				case DroneOrder.Launch: Say(player, LaunchOne(drone) ? "Mods.Mechanization.UI.DroneLaunching" : "Mods.Mechanization.UI.DroneNoSpare", 1); return true;
				case DroneOrder.RecallAll: Say(player, "Mods.Mechanization.UI.DroneRecalling", RecallAll(drone.BarracksId)); return true;
				case DroneOrder.LaunchAll:
					int n = LaunchAll(drone.BarracksId);
					Say(player, n > 0 ? "Mods.Mechanization.UI.DroneLaunching" : "Mods.Mechanization.UI.DroneNoSpare", n);
					return true;
			}
			return false;
		}

		/// <summary>
		/// Server / single player: the keybind. Acts on the Barracks nearest the player: if any of its drones is armed it recalls them all, otherwise it launches the parked ones.
		/// </summary>
		public bool HandleKey(Player player)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || player == null || !player.active || player.dead) return false;
			if (!OrderGap(player)) return false;
			BarracksTE barracks = SettlementSystem.Instance?.NearestBarracks(player.Center, D.KeyReachTiles);
			if (barracks == null) { Say(player, "Mods.Mechanization.UI.DroneKeyNoBarracks", 0); return false; }
			CollectDrones();
			int armed = 0, total = 0;
			foreach (DroneNPC d in _all) if (d.BarracksId == barracks.ID) { total++; if (d.Armed) armed++; }
			if (total == 0) { Say(player, "Mods.Mechanization.UI.DroneNone", 0); return false; }
			if (armed > 0) Say(player, "Mods.Mechanization.UI.DroneRecalling", RecallAll(barracks.ID));
			else
			{
				int n = LaunchAll(barracks.ID);
				Say(player, n > 0 ? "Mods.Mechanization.UI.DroneLaunching" : "Mods.Mechanization.UI.DroneNoSpare", n);
			}
			return true;
		}

		/// <summary>Spam guard: false if this player's last accepted order was under <see cref="D.OrderMinGapTicks"/> ago.</summary>
		private static bool OrderGap(Player player)
		{
			uint now = Main.GameUpdateCount;
			ref uint last = ref _lastOrder[Math.Clamp(player.whoAmI, 0, Main.maxPlayers)];
			if (now - last < (uint)D.OrderMinGapTicks && now >= last) return false;
			last = now;
			return true;
		}

		private static void Recall(DroneNPC drone) => drone.OrderRecall();

		/// <summary>Recalls every drone of one Barracks and holds its garrison. Returns how many were sent (parked ones are held but not counted).</summary>
		public int RecallAll(int barracksId)
		{
			CollectDrones();
			int sent = 0;
			foreach (DroneNPC d in _all)
			{
				if (d.BarracksId != barracksId) continue;
				bool wasFlying = !d.IsParked;
				if (d.OrderRecall() && wasFlying) sent++;
			}
			return sent;
		}

		/// <summary>
		/// Releases the hold on every drone of one Barracks and arms and sends up each parked one. Returns how many launched. A drone with no spare weapon to take stays parked but no longer held,
		/// so the garrison arms it as soon as one turns up.
		/// </summary>
		public int LaunchAll(int barracksId)
		{
			CollectDrones();
			ArmoryTE armory = null;
			if (TileEntity.ByID.TryGetValue(barracksId, out TileEntity te) && te is BarracksTE barracks) armory = ArmoryLink.OfBarracks(barracks);
			if (armory == null) return 0;
			int launched = 0;
			foreach (DroneNPC d in _all)
			{
				if (d.BarracksId != barracksId) continue;
				d.Held = false; // one still flying home on a recall will park unheld, and the garrison arms it
				if (!d.IsParked) continue;
				d.ArmoryId = armory.ID;
				if (TryArm(d, armory)) launched++;
			}
			return launched;
		}

		/// <summary>Releases one drone's hold and, if it is parked, arms it and sends it up. False when there is no spare weapon (it then waits unheld for one) or it is not parked yet.</summary>
		private bool LaunchOne(DroneNPC drone)
		{
			drone.Held = false;
			if (!drone.IsParked) return false;
			if (!(TileEntity.ByID.TryGetValue(drone.BarracksId, out TileEntity te) && te is BarracksTE barracks)) return false;
			ArmoryTE armory = ArmoryLink.OfBarracks(barracks);
			if (armory == null) return false;
			drone.ArmoryId = armory.ID;
			CollectDrones();
			return TryArm(drone, armory);
		}

		/// <summary>Tells the ordering player what happened: straight to chat in single player, to that client in multiplayer.</summary>
		private static bool Say(Player player, string key, int count)
		{
			if (Main.dedServ) ChatHelper.SendChatMessageToClient(NetworkText.FromKey(key, count), new Color(150, 220, 255), player.whoAmI);
			else if (player.whoAmI == Main.myPlayer) Main.NewText(Language.GetTextValue(key, count), 150, 220, 255);
			return true;
		}

		/// <summary>Numbers for the boot self-check and for debugging: drones in the world, by phase.</summary>
		public int CountDrones(DronePhase? phase = null)
		{
			int n = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
				if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsBound && (phase == null || d.Phase == phase)) n++;
			return n;
		}
	}
}
