using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>A place items can be drawn from and put into: an armory's storage, a chest, a Magic Storage network. Server and single player only.</summary>
	public interface IStock
	{
		string Name { get; }
		/// <summary>How many items of this type it holds.</summary>
		long Count(int type);
		/// <summary>Removes up to <paramref name="n"/> items of this type; returns how many were removed.</summary>
		long Take(int type, long n);
		/// <summary>Puts the item in; returns what did not fit (null or air when it all fit).</summary>
		Item Put(Item item);
	}

	public sealed class ArmoryStock : IStock
	{
		public readonly ArmoryTE Te;
		public ArmoryStock(ArmoryTE te) { Te = te; }
		public string Name => "armory";
		public long Count(int type) => Te.CountInStorage(type);
		public long Take(int type, long n) => Te.TakeUpTo(type, (int)Math.Min(n, int.MaxValue));
		public Item Put(Item item) => Te.Deposit(item);
	}

	/// <summary>A vanilla chest (by <c>Main.chest</c> index). Every change is sent to clients slot by slot.</summary>
	public sealed class ChestStock : IStock
	{
		public readonly int Index;
		public ChestStock(int index) { Index = index; }
		private Chest C => Index >= 0 && Index < Main.maxChests ? Main.chest[Index] : null;
		public string Name => "chest";
		public long Count(int type)
		{
			var c = C; if (c == null) return 0; long n = 0;
			for (int i = 0; i < c.item.Length; i++) { var it = c.item[i]; if (it != null && !it.IsAir && it.type == type) n += it.stack; }
			return n;
		}
		public long Take(int type, long n)
		{
			var c = C; if (c == null) return 0; long taken = 0;
			for (int i = 0; i < c.item.Length && taken < n; i++)
			{
				var it = c.item[i]; if (it == null || it.IsAir || it.type != type) continue;
				int k = (int)Math.Min(it.stack, n - taken);
				it.stack -= k; taken += k;
				if (it.stack <= 0) it.TurnToAir();
				Sync(i);
			}
			return taken;
		}
		public Item Put(Item item)
		{
			var c = C; if (c == null || item == null || item.IsAir) return item;
			for (int i = 0; i < c.item.Length && !item.IsAir; i++)
			{
				var s = c.item[i];
				if (s == null || s.IsAir || s.type != item.type || s.prefix != item.prefix || s.stack >= s.maxStack) continue;
				int k = Math.Min(item.stack, s.maxStack - s.stack); s.stack += k; item.stack -= k; Sync(i);
			}
			for (int i = 0; i < c.item.Length && !item.IsAir; i++)
				if (c.item[i] == null || c.item[i].IsAir) { c.item[i] = item.Clone(); item.TurnToAir(); Sync(i); }
			return item.IsAir ? null : item;
		}
		private void Sync(int slot) { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, Index, slot); }
	}

	/// <summary>
	/// The seam Magic Storage plugs into. Levy core never names a Magic Storage type: the compat registers these hooks when the mod is present, and every caller treats a null hook as "no network".
	/// </summary>
	public static class NetworkStocks
	{
		/// <summary>The Magic Storage network the armory is joined to by an Armory Heart, or null (not linked, or the mod is absent).</summary>
		public static Func<ArmoryTE, IStock> Provider;
		/// <summary>True when the armory's network supplies this crafting station (its Crafting Access's combined stations); null = Magic Storage absent.</summary>
		public static Func<ArmoryTE, int, bool> HasStation;
		/// <summary>The network's item types, for the "find this item" picker (the Magic Storage item list). Null when absent.</summary>
		public static Func<ArmoryTE, IEnumerable<int>> ItemTypes;
		public static bool Present => Provider != null;
		public static IStock For(ArmoryTE a) { try { return Provider?.Invoke(a); } catch (Exception e) { Levy.Instance?.Logger.Warn("Levy/Logistics: network provider failed: " + e.Message); return null; } }
	}

	/// <summary>The stocks one order draws from, in priority order: the armory's own storage, the armories pooled with it by pylon, nearby chests, then the Magic Storage network. Output always goes to the armory.</summary>
	public sealed class StockPool
	{
		public readonly ArmoryTE Armory;
		public readonly List<IStock> Sources = new();
		public IStock Network;

		public StockPool(ArmoryTE armory, bool includeChests = true, bool includeNetwork = true)
		{
			Armory = armory;
			Sources.Add(new ArmoryStock(armory));
			var sys = SettlementSystem.Instance;
			if (sys != null) foreach (var a in sys.NetworkedArmories(armory)) if (!ReferenceEquals(a, armory) && a.Storage != null) Sources.Add(new ArmoryStock(a));
			if (includeChests) foreach (int c in NearbyChests(armory)) Sources.Add(new ChestStock(c));
			if (includeNetwork) { Network = NetworkStocks.For(armory); if (Network != null) Sources.Add(Network); }
		}

		/// <summary>Chests within <see cref="LogisticsConstants.ChestRadiusTiles"/> tiles of the armory that nobody has open.</summary>
		public static List<int> NearbyChests(ArmoryTE armory)
		{
			var res = new List<int>();
			float r = LogisticsConstants.ChestRadiusTiles * 16f; r *= r;
			for (int i = 0; i < Main.maxChests; i++)
			{
				var c = Main.chest[i]; if (c == null) continue;
				if (Vector2.DistanceSquared(new Vector2(c.x * 16f + 16f, c.y * 16f + 16f), armory.Center) > r) continue;
				if (Chest.UsingChest(i) != -1) continue;
				res.Add(i);
			}
			return res;
		}

		public long Count(int type) { long n = 0; foreach (var s in Sources) n += s.Count(type); return n; }
		public long Count(int[] types) { long n = 0; foreach (int t in types) n += Count(t); return n; }

		public long Take(int type, long n)
		{
			long taken = 0;
			foreach (var s in Sources) { if (taken >= n) break; taken += s.Take(type, n - taken); }
			return taken;
		}

		/// <summary>Takes <paramref name="n"/> from any of <paramref name="types"/> (a recipe group), richest stock first.</summary>
		public long TakeAny(int[] types, long n)
		{
			var order = new List<int>(types);
			order.Sort((a, b) => Count(b).CompareTo(Count(a)));
			long taken = 0;
			foreach (int t in order) { if (taken >= n) break; taken += Take(t, n - taken); }
			return taken;
		}

		/// <summary>Puts an item into the armory's storage (the output side). Returns what did not fit.</summary>
		public Item PutOutput(Item item) => Armory.Deposit(item);
	}

	/// <summary>The few numbers of the logistics screen. Kept local so this area adds no merge surface to <c>LevyConstants</c>.</summary>
	public static class LogisticsConstants
	{
		/// <summary>Chests this near an armory count as its stock (tiles).</summary>
		public const int ChestRadiusTiles = 25;
		/// <summary>A crafting station this near an armory counts as available to its orders (tiles).</summary>
		public const int StationRadiusTiles = 30;
		/// <summary>The order runner works once per this many ticks.</summary>
		public const int RunnerTicks = 30;
		/// <summary>Crafts one order may make per run, so a huge order spreads over time instead of stalling a tick.</summary>
		public const int CraftsPerRun = 40;
		/// <summary>Intermediate crafting depth an order will go to (a bar from ore, a part from bars).</summary>
		public const int MaxDepth = 4;
		/// <summary>Most lines in one order.</summary>
		public const int MaxLines = 16;
		/// <summary>Most orders per world.</summary>
		public const int MaxOrders = 24;
		/// <summary>Most units one order can ask for.</summary>
		public const int MaxTotal = 9999;
	}
}
