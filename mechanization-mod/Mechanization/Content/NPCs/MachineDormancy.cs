using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Budget;
using Mechanization.Common;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// The two rules machines share about the players and the fight, read by <see cref="TurretSystem"/>, <see cref="FieldDroneSystem"/> and <see cref="DroneSystem"/>.
	///
	/// DORMANT. A machine with no player within <see cref="RangePx"/> is packed into its saved record: the NPC goes (its slot and its per-tick brain with it), the record stays (position, health, facing, mounted weapon,
	/// battery) and says it is dormant. When a player comes within range the record spawns the machine again, from that record, as it was. A Barracks garrison is not a record of its own: its drones stand down and file their weapons
	/// back into the Armory (a loan on the Armory's ledger, never lost) and the garrison launches again when a player is near, so the Barracks and the Armory are its record. Packing is delayed (<see cref="PackDelayTicks"/> far from everyone)
	/// so a player at the edge does not flap a machine in and out; unpacking is at once. Nothing is ever deleted: a dormant record is not "destroyed" to the reconcile pass and is saved with the world.
	/// Slots: a dormant machine is no NPC, so it holds none.
	///
	/// ROOM FOR A BOSS. The pack is lossless, so Levy's <see cref="BossSlots"/> may use it: a boss spawn that finds no slot packs player-built drones and turrets (farthest from the spawn first) after critters, stray hostiles,
	/// garrison drones and the army's summons, and before any levy is benched (<see cref="PackFarthestForBoss"/>, registered in <see cref="BossSlots.MachinePackers"/>). While a boss that needed room is alive the same pack keeps
	/// its <see cref="BossSlots.ExtraReserve"/> free (<see cref="MachineBossRoomSystem"/>). A machine packed for a boss does not wake while a boss lives; after, it wakes like any dormant record (a player near, a free slot).
	///
	/// STAND DOWN FOR BOSSES. With <see cref="MechConfig.StandDownDuringBosses"/> on, every machine leaves the field while a boss lives (Levy's <see cref="BossSlots.BossAlive"/>, the same notion of "a boss" the room-making uses):
	/// the garrison's drones file their weapons back into the Armory and go (nothing launches until the boss is gone, then the garrison launches again from the Barracks and the Armory), and every placed drone and turret
	/// packs into its record held for the boss (<see cref="MachineBossRoomSystem"/>), waking as it was once the boss is gone. Nothing wakes while it stands down.
	/// </summary>
	public static class MachineDormancy
	{
		/// <summary>Ticks a machine must be out of range of every player before it packs.</summary>
		public const int PackDelayTicks = 180;
		/// <summary>The sweep cadence: dormancy is judged every this many ticks.</summary>
		public const int SweepTicks = 30;

		/// <summary>Counters the world check reads: machines packed, machines unpacked, unpack attempts that found no free NPC slot.</summary>
		public static int Packed, Unpacked, NoSlot, GarrisonDismissed;
		/// <summary>Counters the world check reads: machines packed to make room for a boss, garrison drones removed by the boss stand-down, wakes held back while a boss lives.</summary>
		public static int PackedForBoss, GarrisonVanished, WakeHeld, StandDownPacked;
		/// <summary>Lab log of each boss pack: how far the packed machine was from the spot, and the farthest machine still standing after it (farthest first means the first is never below the second).</summary>
		internal static readonly List<(float packed, float farthestLeft)> BossPackLog = new();

		public static bool Enabled
		{
			get
			{
				try { return MechConfig.Get?.DormantWhenFar ?? true; } catch (Exception) { return true; }
			}
		}

		public static float RangePx
		{
			get
			{
				int tiles = 280;
				try { tiles = MechConfig.Get?.DormantRangeTiles ?? 280; } catch (Exception) { }
				return Math.Max(60, tiles) * Levy.Common.LevyConstants.TilePx;
			}
		}

		/// <summary>True when an active player is within <paramref name="rangePx"/> of <paramref name="p"/>. A dead player counts (she respawns); the slot-255 stand-in some enemy AI swaps in is not a player.</summary>
		public static bool NearPlayer(Vector2 p, float rangePx)
		{
			float r2 = rangePx * rangePx;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player pl = Main.player[i];
				if (pl != null && pl.active && Vector2.DistanceSquared(pl.Center, p) <= r2) return true;
			}
			return false;
		}

		/// <summary>True when a Barracks has no player near and dormancy is on: its garrison stands down and launches nothing.</summary>
		public static bool GarrisonAsleep(Vector2 barracksCenter) => Enabled && !NearPlayer(barracksCenter, RangePx);

		/// <summary>
		/// True when a packed machine may come back now. One packed for a boss stays packed while any boss lives (its flag is cleared once the boss is gone). While a boss that needed room is alive, any wake must leave
		/// that boss's <see cref="BossSlots.ExtraReserve"/> free slots untouched, the same line a levy body waits behind.
		/// </summary>
		public static bool MayWake(ref bool forBoss)
		{
			if (StandingDown) { WakeHeld++; return false; } // the machines stand down for the boss: none comes back while it lives (one placed mid-fight packs on the next pass)
			if (forBoss)
			{
				if (BossSlots.BossAlive) { WakeHeld++; return false; }
				forBoss = false;
			}
			int reserve = BossSlots.ExtraReserve;
			if (reserve > 0 && FreeNpcSlots() - 1 < reserve) { WakeHeld++; return false; }
			return true;
		}

		/// <summary>Free NPC slots, counted now (not the once-a-tick cache: wakes and packs change it inside a tick).</summary>
		public static int FreeNpcSlots() { int f = 0; for (int i = 0; i < Main.maxNPCs; i++) if (!Main.npc[i].active) f++; return f; }

		/// <summary>A live player-built drone or turret that could be packed losslessly.</summary>
		private static bool Packable(NPC n) => n.ModNPC is TurretNPC ? TurretSystem.Instance.CanPack(n) : n.ModNPC is DroneNPC d && d.IsField && FieldDroneSystem.Instance.CanPack(n);

		/// <summary>
		/// Levy's <see cref="BossSlots.MachinePackers"/> step: packs the ONE player-built drone or turret at slot <paramref name="minIndex"/> or above that is farthest from <paramref name="at"/> into its saved record (position, health,
		/// facing, weapon, battery: the dormancy pack) and holds it packed while the boss lives. Returns the slots freed: 1, or 0 when there is none to pack. Server / single player.
		/// </summary>
		public static int PackFarthestForBoss(Vector2 at, int minIndex)
		{
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return 0;
			int best = -1; float bd = -1f, second = -1f;
			for (int i = Math.Max(0, minIndex); i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !Packable(n)) continue;
				float d = Vector2.DistanceSquared(n.Center, at);
				if (d > bd) { second = bd; bd = d; best = i; }
				else if (d > second) second = d;
			}
			if (best < 0) return 0;
			NPC pick = Main.npc[best];
			bool ok = pick.ModNPC is TurretNPC ? TurretSystem.Instance.PackForBoss(pick) : FieldDroneSystem.Instance.PackForBoss(pick);
			if (!ok) return 0;
			if (BossPackLog.Count < 256) BossPackLog.Add(((float)Math.Sqrt(bd), second < 0f ? -1f : (float)Math.Sqrt(second)));
			return 1;
		}

		/// <summary>True while the "stand down during bosses" setting is on and a boss is alive: the garrison launches nothing.</summary>
		public static bool StandingDown
		{
			get
			{
				bool on = false;
				try { on = MechConfig.Get?.StandDownDuringBosses ?? false; } catch (Exception) { }
				return on && BossSlots.BossAlive;
			}
		}
	}

	/// <summary>
	/// Stands placed machines down for a boss and keeps a boss's room. With <see cref="MechConfig.StandDownDuringBosses"/> on and a boss alive, every placed drone and turret packs into its record, held for the boss, each sweep
	/// (a machine placed during the fight goes on the next one). Otherwise only the room: while a boss that needed room is alive (Levy's <see cref="BossSlots.ExtraReserve"/> above 0) and the free NPC slots fall under that reserve, the garrison's drones stand down first and then
	/// player-built drones and turrets pack into their records, farthest from the nearest boss first, until the reserve is whole again. Levies are never benched for the reserve: their bodies only wait behind it. Server / single player.
	/// Registers the lossless pack with Levy's room-making (<see cref="BossSlots.MachinePackers"/>).
	/// </summary>
	public class MachineBossRoomSystem : ModSystem
	{
		/// <summary>The most machines one pass packs (a pass runs every <see cref="MachineDormancy.SweepTicks"/> ticks).</summary>
		public const int MaxPerPass = 40;
		/// <summary>Lab counters: garrison drones and machines taken by the reserve keeper.</summary>
		public static int ReserveGarrison, ReservePacked;

		private static readonly Func<Vector2, int, int> Packer = MachineDormancy.PackFarthestForBoss;
		public override void Load() { BossSlots.MachinePackers.Add(Packer); }
		public override void Unload() { BossSlots.MachinePackers.Remove(Packer); }

		public override void PostUpdateNPCs()
		{
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
			if (Main.GameUpdateCount % (uint)MachineDormancy.SweepTicks != 0u) return;
			if (MachineDormancy.StandingDown)
			{
				NearestBoss(out Vector2 bossAt);
				for (int k = 0; k < Main.maxNPCs && MachineDormancy.PackFarthestForBoss(bossAt, 0) > 0; k++) MachineDormancy.StandDownPacked++;
				return;
			}
			int reserve = BossSlots.ExtraReserve;
			if (reserve <= 0) return;
			int short_ = reserve - MachineDormancy.FreeNpcSlots();
			if (short_ <= 0) return;
			if (!NearestBoss(out Vector2 at)) return;
			for (int k = 0; k < MaxPerPass && short_ > 0; k++)
			{
				int got = DroneSystem.ReleaseGarrisonSlots(1, 0);
				if (got > 0) { ReserveGarrison += got; short_ -= got; continue; }
				got = MachineDormancy.PackFarthestForBoss(at, 0);
				if (got <= 0) break;
				ReservePacked += got; short_ -= got;
			}
		}

		/// <summary>The centre of a live boss (the first found; a worm's head or any part of it): the spot machines are packed farthest from.</summary>
		private static bool NearestBoss(out Vector2 at)
		{
			at = default;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && !n.friendly && (n.boss || (n.type > 0 && n.type < Terraria.ID.NPCID.Sets.ShouldBeCountedAsBoss.Length && Terraria.ID.NPCID.Sets.ShouldBeCountedAsBoss[n.type]))) { at = n.Center; return true; }
			}
			return false;
		}
	}
}
