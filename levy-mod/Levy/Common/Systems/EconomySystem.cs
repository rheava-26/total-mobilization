using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Levy.Common.Config;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>Prices in copper. Pure functions of tier + grade; treasury ops on a barracks. OWNED BY THE ROSTER AGENT.</summary>
	public static class EconomySystem
	{
		public static long LevyWage => (long)(RosterSystem.Instance.LevyWage * LevyConfig.Get.WageMultiplier);
		public static long WagePerDay(Grade grade, bool stoodDown) => (long)(LevyWage * LevyConstants.GradeWageMult[(int)grade] * (stoodDown ? LevyConstants.StoodDownWageMult : 1f));
		public static long MusterFee(Grade grade) => WagePerDay(grade, false) * LevyConstants.MusterFeeDays;
		public static long PromotionCost(Grade from, Grade to) => System.Math.Max(0, MusterFee(to) - MusterFee(from));
		public static long MilitiaMusterFeePerHead => LevyWage / LevyConstants.MilitiaMusterDivisor;
		public static long RecruitingDrivePerPoint => LevyWage * LevyConstants.RecruitingDriveWageMult;

		public static bool CanAfford(BarracksTE b, long cost) => b != null && b.Treasury >= cost;
		public static bool TryPay(BarracksTE b, long cost) { if (!CanAfford(b, cost)) return false; b.Treasury -= cost; return true; }
		public static void Deposit(BarracksTE b, long amount) { if (b != null && amount > 0) b.Treasury += amount; }
		/// <summary>Coin value of an item stack (coins only), 0 otherwise.</summary>
		public static long CoinValue(Item item)
		{
			if (item == null || item.IsAir) return 0;
			return item.type switch { Terraria.ID.ItemID.CopperCoin => 1L * item.stack, Terraria.ID.ItemID.SilverCoin => 100L * item.stack, Terraria.ID.ItemID.GoldCoin => 10000L * item.stack, Terraria.ID.ItemID.PlatinumCoin => 1000000L * item.stack, _ => 0 };
		}

		// ---- settlement-wide money -------------------------------------------------------------------------------------------------------
		// A barracks treasury is per-barracks, so paying from ONE barracks strands money held anywhere else in the settlement. The network paths below pool it: the anchor's
		// treasury first, then the other barracks of the settlement (SettlementSystem.NetworkedBarracks), then coin ITEMS lying in the settlement's armory storage
		// (the linked armory of each barracks and everything pylon-networked to it). A lone barracks is a network of one and behaves exactly like CanAfford / TryPay.

		/// <summary>Every armory whose storage the settlement of <paramref name="barracks"/> can reach for coins: each barracks's linked armory plus that armory's pylon network. No duplicates, no dead entries.</summary>
		private static List<ArmoryTE> SettlementArmories(List<BarracksTE> barracks)
		{
			var res = new List<ArmoryTE>(4);
			var seen = new HashSet<int>();
			var sys = SettlementSystem.Instance;
			foreach (var b in barracks)
			{
				ArmoryTE linked = sys.LinkedArmory(b);
				if (linked == null) continue;
				foreach (var a in sys.NetworkedArmories(linked))
					if (a != null && a.Storage != null && TileEntity.ByID.TryGetValue(a.ID, out var live) && ReferenceEquals(live, a) && seen.Add(a.ID)) res.Add(a);
			}
			return res;
		}

		private static long CoinUnit(int type) => type switch { ItemID.CopperCoin => 1L, ItemID.SilverCoin => 100L, ItemID.GoldCoin => 10000L, ItemID.PlatinumCoin => 1000000L, _ => 0L };

		/// <summary>Copper held as coin items in the storage of these armories.</summary>
		private static long ArmoryCoinTotal(List<ArmoryTE> armories)
		{
			long total = 0;
			foreach (var a in armories) foreach (var it in a.Storage) total += CoinValue(it);
			return total;
		}

		/// <summary>Copper the settlement of <paramref name="anchor"/> can spend: the treasury of every networked barracks plus the coin items in its armory storage.</summary>
		public static long NetworkFunds(BarracksTE anchor)
		{
			if (anchor == null) return 0;
			var bars = SettlementSystem.Instance.NetworkedBarracks(anchor);
			long total = 0;
			foreach (var b in bars) total += Math.Max(0L, b.Treasury);
			return total + ArmoryCoinTotal(SettlementArmories(bars));
		}

		/// <summary>True when the settlement of <paramref name="anchor"/> (its own treasury, the other networked barracks, armory coin items) can cover <paramref name="cost"/>. Cheap when the anchor alone can.</summary>
		public static bool CanAffordNetwork(BarracksTE anchor, long cost)
		{
			if (anchor == null) return false;
			if (cost <= 0 || anchor.Treasury >= cost) return true;
			return NetworkFunds(anchor) >= cost;
		}

		/// <summary>
		/// Pays <paramref name="cost"/> from the whole settlement, all or nothing: the anchor's treasury first, then the other networked barracks nearest-first, then coin items from armory storage
		/// (smallest coins first; any overshoot from a bigger coin goes back into the anchor's treasury as change). Syncs every barracks / armory it touched. Server and single player only.
		/// </summary>
		public static bool TryPayNetwork(BarracksTE anchor, long cost)
		{
			if (anchor == null) return false;
			if (cost <= 0) return true;
			if (anchor.Treasury >= cost) { anchor.Treasury -= cost; return true; }

			var bars = SettlementSystem.Instance.NetworkedBarracks(anchor);
			long barracksFunds = 0;
			foreach (var b in bars) barracksFunds += Math.Max(0L, b.Treasury);
			List<ArmoryTE> armories = null;
			if (barracksFunds < cost)
			{
				armories = SettlementArmories(bars);
				if (barracksFunds + ArmoryCoinTotal(armories) < cost) return false; // the whole settlement is short: touch nothing
			}

			long remaining = cost;
			foreach (var b in bars)
			{
				if (remaining <= 0) break;
				long take = Math.Min(Math.Max(0L, b.Treasury), remaining);
				if (take <= 0) continue;
				b.Treasury -= take; remaining -= take;
				RosterSystem.SyncBarracks(b);
			}
			if (remaining > 0)
			{
				long spent = ConsumeCoins(armories, remaining);
				if (spent > remaining) { Deposit(anchor, spent - remaining); RosterSystem.SyncBarracks(anchor); } // change from a coin bigger than the bill
			}
			return true;
		}

		/// <summary>Removes coin items from the armories' storage worth at least <paramref name="need"/> copper (smallest denomination first, so the overshoot stays small) and returns the copper actually taken.</summary>
		private static long ConsumeCoins(List<ArmoryTE> armories, long need)
		{
			var slots = new List<(ArmoryTE A, int Idx, long Unit)>();
			foreach (var a in armories)
				for (int i = 0; i < a.Storage.Length; i++)
				{
					long unit = CoinValue(a.Storage[i]) > 0 ? CoinUnit(a.Storage[i].type) : 0;
					if (unit > 0) slots.Add((a, i, unit));
				}
			slots.Sort((x, y) => x.Unit != y.Unit ? x.Unit.CompareTo(y.Unit) : (x.A.ID != y.A.ID ? x.A.ID.CompareTo(y.A.ID) : x.Idx.CompareTo(y.Idx)));

			long taken = 0;
			foreach (var (a, idx, unit) in slots)
			{
				if (taken >= need) break;
				Item it = a.Storage[idx];
				int n = (int)Math.Min(it.stack, (need - taken + unit - 1) / unit);
				if (n <= 0) continue;
				it.stack -= n;
				if (it.stack <= 0) a.Storage[idx] = new Item();
				taken += n * unit;
				a.MarkDirty();
			}
			return taken;
		}

		private static readonly int[] CoinTypes = { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };

		/// <summary>
		/// Gives copper to a player as coin stacks dropped at the player (server or single player). Stacks are split per
		/// denomination and per max stack; on the server the spawned items are synced to every client.
		/// </summary>
		public static void GiveCoins(Player player, long copper, IEntitySource source)
		{
			if (player == null || copper <= 0 || Main.netMode == NetmodeID.MultiplayerClient) return;
			int[] split = Utils.CoinsSplit(copper); // [copper, silver, gold, platinum]
			for (int i = 0; i < CoinTypes.Length && i < split.Length; i++)
			{
				int left = split[i];
				if (left <= 0) continue;
				int max = ContentSamples.ItemsByType.TryGetValue(CoinTypes[i], out var sample) ? Math.Max(1, sample.maxStack) : 100;
				while (left > 0)
				{
					int take = Math.Min(left, max);
					player.QuickSpawnItem(source, CoinTypes[i], take);
					left -= take;
				}
			}
		}
	}
}
