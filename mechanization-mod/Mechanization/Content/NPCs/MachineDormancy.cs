using System;
using Microsoft.Xna.Framework;
using Terraria;
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
	/// Slots: a dormant machine is no NPC, so it holds none (Levy's <see cref="BossSlots"/> never has to ask for it).
	///
	/// STAND DOWN FOR BOSSES. With <see cref="MechConfig.StandDownDuringBosses"/> on, the garrison holds its launches and re-arms while a boss lives (Levy's <see cref="BossSlots.BossAlive"/>, the same notion of "a boss" the room-making uses).
	/// </summary>
	public static class MachineDormancy
	{
		/// <summary>Ticks a machine must be out of range of every player before it packs.</summary>
		public const int PackDelayTicks = 180;
		/// <summary>The sweep cadence: dormancy is judged every this many ticks.</summary>
		public const int SweepTicks = 30;

		/// <summary>Counters the world check reads: machines packed, machines unpacked, unpack attempts that found no free NPC slot.</summary>
		public static int Packed, Unpacked, NoSlot, GarrisonDismissed;

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
}
