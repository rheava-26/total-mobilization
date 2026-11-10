using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Logistics;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// The Magic-Storage-touching half of the compat: finds the network an armory is joined to by an Armory Heart, wraps it as an <see cref="IStock"/>, and answers the station and item-list questions.
	/// JIT'd only when Magic Storage is enabled; only ever called behind <see cref="MagicStorageCompatSystem.Active"/> or through the hooks it registers.
	/// </summary>
	[JITWhenModsEnabled("MagicStorage")]
	public static class MsDriver
	{
		public static void Register()
		{
			NetworkStocks.Provider = ProviderFor;
			NetworkStocks.HasStation = HasStation;
			NetworkStocks.ItemTypes = ItemTypes;
			NetworkStocks.CanDecraft = CanDecraft;
		}

		public static void Unregister() { NetworkStocks.Provider = null; NetworkStocks.HasStation = null; NetworkStocks.ItemTypes = null; NetworkStocks.CanDecraft = null; }

		/// <summary>True when a Decrafting Interface (Magic Storage's shimmer module) is joined to the armory's network.</summary>
		private static bool CanDecraft(ArmoryTE a)
		{
			var heart = HeartFor(a); if (heart == null) return false;
			foreach (var _ in heart.ComponentManager.GetDecraftingAccessEntities()) return true;
			return false;
		}

		/// <summary>The storage heart of the network that an Armory Heart serving <paramref name="a"/> is joined to, or null.</summary>
		public static TEStorageHeart HeartFor(ArmoryTE a)
		{
			if (a == null) return null;
			foreach (var te in TileEntity.ByID.Values)
				if (te is TEArmoryHeart h && ReferenceEquals(h.Armory, a) && h.GetHeart() is TEStorageHeart heart && heart.IsAlive) return heart;
			return null;
		}

		private static IStock ProviderFor(ArmoryTE a) { var h = HeartFor(a); return h == null ? null : new MsNetworkStock(h); }

		private static bool HasStation(ArmoryTE a, int tile)
		{
			var heart = HeartFor(a); if (heart == null) return false;
			var set = new HashSet<int>();
			foreach (var access in heart.ComponentManager.GetCraftingAccessEntities())
				foreach (var st in access.stations)
					if (st != null && !st.IsAir && st.createTile >= TileID.Dirt) Crafting.AddWithEquivalents(set, st.createTile);
			return set.Contains(tile);
		}

		private static IEnumerable<int> ItemTypes(ArmoryTE a)
		{
			var heart = HeartFor(a); if (heart == null) return Array.Empty<int>();
			return heart.GetStoredItems().Where(i => i != null && !i.IsAir).Select(i => i.type).Distinct().ToList();
		}
	}

	/// <summary>A Magic Storage network as a stock: counts, withdraws and deposits through the storage heart, so Magic Storage does its own netcode, sorting and security.</summary>
	[JITWhenModsEnabled("MagicStorage")]
	public sealed class MsNetworkStock : IStockItems
	{
		private readonly TEStorageHeart _heart;
		public MsNetworkStock(TEStorageHeart heart) { _heart = heart; }
		public string Name => "network";

		public long Count(int type)
		{
			long n = 0;
			foreach (var it in _heart.GetStoredItems()) if (it != null && !it.IsAir && it.type == type) n += it.stack;
			return n;
		}

		public long Take(int type, long n)
		{
			long taken = 0;
			while (taken < n)
			{
				var want = new Item(); want.SetDefaults(type); want.stack = (int)Math.Min(n - taken, Math.Max(1, want.maxStack));
				var got = _heart.TryWithdraw(want, false);
				if (got == null || got.IsAir) break;
				taken += got.stack;
			}
			return taken;
		}

		public Item Put(Item item)
		{
			if (item == null || item.IsAir) return null;
			_heart.TryDeposit(item);
			return item.IsAir ? null : item;
		}

		public IEnumerable<Item> Items() => _heart.GetStoredItems().Where(i => i != null && !i.IsAir).ToList();

		/// <summary>Withdraws through the heart by exact type and prefix, so a prefixed weapon comes out whole and Magic Storage keeps its own sorting and security.</summary>
		public long TakeExact(Item like, long n)
		{
			long taken = 0;
			while (taken < n)
			{
				var want = like.Clone(); want.stack = (int)Math.Min(n - taken, Math.Max(1, want.maxStack));
				var got = _heart.TryWithdraw(want, true);
				if (got == null || got.IsAir || got.type != like.type) break;
				taken += got.stack;
			}
			return taken;
		}

		public bool Protected(Item it) => false;
	}
}
