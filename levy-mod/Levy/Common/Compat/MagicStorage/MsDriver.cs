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
			// the Crafting Interface draws on armory racks: a network with an Armory Heart lists the armory module (ArmoryCraftLink)
			var getModules = typeof(TEStorageHeart).GetMethod(nameof(TEStorageHeart.GetModules), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
			if (getModules == null) throw new InvalidOperationException("TEStorageHeart.GetModules not found");
			MonoModHooks.Add(getModules, (Func<ArmoryCraftLink.OrigGetModules, TEStorageHeart, IEnumerable<global::MagicStorage.EnvironmentModule>>)ArmoryCraftLink.GetModulesDetour);
		}

		public static void Unregister() { ArmoryCraftLink.ResetForTest(); ResetCache(); NetworkStocks.Provider = null; NetworkStocks.HasStation = null; NetworkStocks.ItemTypes = null; NetworkStocks.CanDecraft = null; }

		/// <summary>True when a Decrafting Interface (Magic Storage's shimmer module) is joined to the armory's network.</summary>
		private static bool CanDecraft(ArmoryTE a)
		{
			var heart = HeartFor(a); if (heart == null) return false;
			foreach (var _ in heart.ComponentManager.GetDecraftingAccessEntities()) return true;
			return false;
		}

		// The heart, the stations and the stock of a network are read many times a frame by the logistics screen (every recipe asks for a station, every ingredient for a count),
		// so each is looked up once and kept for a few ticks.
		private static int _heartArmory = -1; private static TEStorageHeart _heartHit; private static uint _heartStamp = uint.MaxValue;
		private static readonly Dictionary<int, CachedStock> _stocks = new();
		private static int _stationHeart = -1; private static uint _stationStamp = uint.MaxValue; private static HashSet<int> _stations;

		/// <summary>The storage heart of the network that an Armory Heart serving <paramref name="a"/> is joined to, or null. The answer is kept for half a second: finding it walks every tile entity of the world.</summary>
		public static TEStorageHeart HeartFor(ArmoryTE a)
		{
			if (a == null) return null;
			uint now = Main.GameUpdateCount;
			if (_heartArmory == a.ID && _heartStamp != uint.MaxValue && now >= _heartStamp && now - _heartStamp < 30)
			{
				if (_heartHit == null) return null;
				if (TileEntity.ByID.TryGetValue(_heartHit.ID, out var live) && ReferenceEquals(live, _heartHit) && _heartHit.IsAlive) return _heartHit;
			}
			TEStorageHeart found = null;
			foreach (var te in TileEntity.ByID.Values)
				if (te is TEArmoryHeart h && ReferenceEquals(h.Armory, a) && h.GetHeart() is TEStorageHeart heart && heart.IsAlive) { found = heart; break; }
			_heartArmory = a.ID; _heartHit = found; _heartStamp = now;
			return found;
		}

		/// <summary>Forgets the kept heart, stations and snapshots (a network was linked or unlinked just now; the self-check, whose game tick never moves).</summary>
		public static void ResetCache() { _heartArmory = -1; _heartHit = null; _heartStamp = uint.MaxValue; _stationHeart = -1; _stations = null; _stocks.Clear(); }

		private static IStock ProviderFor(ArmoryTE a)
		{
			var h = HeartFor(a); if (h == null) return null;
			if (!_stocks.TryGetValue(h.ID, out var cached) || !ReferenceEquals(((MsNetworkStock)cached.Inner).Heart, h)) _stocks[h.ID] = cached = new CachedStock(new MsNetworkStock(h));
			return cached;
		}

		private static bool HasStation(ArmoryTE a, int tile)
		{
			var heart = HeartFor(a); if (heart == null) return false;
			uint now = Main.GameUpdateCount;
			if (_stations == null || _stationHeart != heart.ID || now < _stationStamp || now - _stationStamp >= 60)
			{
				var set = new HashSet<int>();
				foreach (var access in heart.ComponentManager.GetCraftingAccessEntities())
					foreach (var st in access.stations)
						if (st != null && !st.IsAir && st.createTile >= TileID.Dirt) Crafting.AddWithEquivalents(set, st.createTile);
				_stations = set; _stationHeart = heart.ID; _stationStamp = now;
			}
			return _stations.Contains(tile);
		}

		private static IEnumerable<int> ItemTypes(ArmoryTE a) => ProviderFor(a) is CachedStock c ? c.Types().ToList() : Array.Empty<int>();
	}

	/// <summary>A Magic Storage network as a stock: counts, withdraws and deposits through the storage heart, so Magic Storage does its own netcode, sorting and security.</summary>
	[JITWhenModsEnabled("MagicStorage")]
	public sealed class MsNetworkStock : IStockItems
	{
		private readonly TEStorageHeart _heart;
		public MsNetworkStock(TEStorageHeart heart) { _heart = heart; }
		public TEStorageHeart Heart => _heart;
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
