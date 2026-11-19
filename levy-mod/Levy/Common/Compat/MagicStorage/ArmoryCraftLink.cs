using System;
using System.Collections.Generic;
using System.Linq;
using MagicStorage;
using MagicStorage.Common.Systems;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// Magic Storage's Crafting Interface crafts with the racks of every Armory joined to its network by an Armory Heart (an environment module Magic Storage asks for "additional items").
	/// A weapon crafted from an armory weapon goes back to that armory. Listed under the network's modules automatically while an Armory Heart is connected; no Environment Simulator needed.
	/// </summary>
	[ExtendsFromMod("MagicStorage")]
	[JITWhenModsEnabled("MagicStorage")]
	public sealed class ArmoryRacksModule : EnvironmentModule
	{
		public override IEnumerable<Item> GetAdditionalItems(EnvironmentSandbox sandbox) => ArmoryCraftLink.Offered(sandbox.heart);
		public override void ConsumeItemForRecipe(EnvironmentSandbox sandbox, Recipe recipe, int type, ref int stack) => ArmoryCraftLink.Validate();
#pragma warning disable CS0672, CS0618
		// Magic Storage calls this for every item a craft consumes, BEFORE it takes the stack off: the one hook that names the exact instance, so the armory pays at that moment.
		public override void OnConsumeItemForRecipe(EnvironmentSandbox sandbox, Item item, int stack) => ArmoryCraftLink.Consumed(item, stack);
#pragma warning restore CS0672, CS0618
		public override void OnConsumeItemsForRecipe(EnvironmentSandbox sandbox, Recipe recipe, List<Item> items) => ArmoryCraftLink.Crafted(recipe);
		public override void PreUpdateUI() => ArmoryCraftLink.Sync(LocalHeart(), true);
		public override void PostUpdateUI() => ArmoryCraftLink.AfterUi(LocalHeart());

		private static TEStorageHeart LocalHeart() { try { return StoragePlayer.LocalPlayer.GetStorageHeart(); } catch (Exception) { return null; } }
	}

	/// <summary>
	/// The bookkeeping behind <see cref="ArmoryRacksModule"/>. Magic Storage crafts by changing the stacks of the very items a module hands it, so the module never hands it an armory's own items: it hands
	/// PROXIES (clones), one per rack item, and pays the armory when a proxy is consumed:
	///  - <see cref="Sync"/> (every UI frame, main thread, before the UI handles clicks) rebuilds the proxies from the racks as they are now: an item a levy took, a Re-arm reserved or the player favorited is gone at once.
	///  - <see cref="Validate"/> (before each ingredient is consumed) lowers a proxy to what its rack item still holds, never raises it.
	///  - <see cref="Consumed"/> (Magic Storage's per-item consume hook) takes exactly that many off the rack item; <see cref="Reconcile"/> (after each craft and each frame) settles any proxy whose stack fell without it.
	/// Every path takes off the racks only what a proxy lost, and a proxy never holds more than its rack item: no duplicate, no loss. A shortfall (a proxy consumed with nothing behind it) is counted, never hidden.
	/// Multiplayer clients are offered nothing (the server owns the racks). All of it is main-thread except <see cref="Offered"/>, which returns a list published by <see cref="Sync"/>.
	/// </summary>
	[JITWhenModsEnabled("MagicStorage")]
	public static class ArmoryCraftLink
	{
		private sealed class Link { public ArmoryTE A; public Item Backing; public Item Proxy; public int Snap; }

		private static readonly object Gate = new();
		private static readonly Dictionary<Item, Link> ByBacking = new(ReferenceEqualityComparer.Instance), ByProxy = new(ReferenceEqualityComparer.Instance);
		private static readonly Dictionary<int, Item[]> Published = new();
		private static readonly List<(ArmoryTE a, int type)> Returns = new();
		private static ArmoryTE _weaponFrom;

		/// <summary>Items taken off armory racks by crafts, proxies consumed with nothing behind them (must stay 0), crafted weapons sent back to an armory.</summary>
		public static long Taken, Shortfall, Returned;

		// ------------------------------------------------------------------ the module in the network

		private static long _armStamp; private static int _armHeart = -1; private static List<ArmoryTE> _arm = new();

		/// <summary>The armories an Armory Heart joins to this storage heart (kept for half a second: Magic Storage asks for the modules many times a frame, from its worker threads too).</summary>
		public static List<ArmoryTE> ArmoriesOf(TEStorageHeart heart)
		{
			if (heart == null) return new();
			long now = Environment.TickCount64;
			lock (Gate)
			{
				if (_armHeart == heart.ID && now - _armStamp < 500) return _arm;
				var list = new List<ArmoryTE>();
				try
				{
					foreach (var c in heart.ComponentManager.GetMiscellaneousComponentEntities())
						if (c is TEArmoryHeart ah && ah.Armory is ArmoryTE a && !list.Contains(a)) list.Add(a);
				}
				catch (Exception) { }
				_arm = list; _armHeart = heart.ID; _armStamp = now;
				return list;
			}
		}

		/// <summary>Forgets the cached armory list (a network was linked a moment ago; the self-check).</summary>
		public static void ResetCache() { lock (Gate) { _armHeart = -1; _arm = new(); } }

		internal delegate IEnumerable<EnvironmentModule> OrigGetModules(TEStorageHeart self);
		/// <summary>Detour on <c>TEStorageHeart.GetModules</c>: a network with an Armory Heart has the armory module, with or without an Environment Simulator.</summary>
		internal static IEnumerable<EnvironmentModule> GetModulesDetour(OrigGetModules orig, TEStorageHeart self)
		{
			_hl_GetModulesDetour_1++;
			var mods = orig(self);
			if (Main.netMode == NetmodeID.MultiplayerClient || ArmoriesOf(self).Count == 0) return mods;
			var mine = ModContent.GetInstance<ArmoryRacksModule>();
			return mine == null ? mods : mods.Where(m => m.Type != mine.Type).Append(mine);
		}
		private static int _hl_GetModulesDetour_1;

		// ------------------------------------------------------------------ proxies

		/// <summary>The proxies offered for this heart (published by <see cref="Sync"/>; safe from any thread).</summary>
		public static IEnumerable<Item> Offered(TEStorageHeart heart)
		{
			if (heart == null || Main.netMode == NetmodeID.MultiplayerClient) return Array.Empty<Item>();
			lock (Gate) return Published.TryGetValue(heart.ID, out var arr) ? arr : Array.Empty<Item>();
		}

		private static bool Alive(ArmoryTE a) => a != null && TileEntity.ByID.TryGetValue(a.ID, out var te) && ReferenceEquals(te, a);

		private static bool Valid(Link l) => Alive(l.A) && !l.Backing.IsAir && l.Backing.type == l.Proxy.type && l.Backing.prefix == l.Proxy.prefix && l.A.OnRacks(l.Backing) && l.A.CraftOffers(l.Backing);

		private static int StackOf(Item proxy) => proxy.IsAir ? 0 : Math.Max(0, proxy.stack);

		private static void Drop(Link l) { ByBacking.Remove(l.Backing); ByProxy.Remove(l.Proxy); l.Proxy.TurnToAir(); }

		/// <summary>Pays the racks for every proxy whose stack fell since it was last settled. Main thread, under the gate.</summary>
		private static void ReconcileLocked()
		{
			foreach (var l in ByProxy.Values)
			{
				int cur = StackOf(l.Proxy), d = l.Snap - cur;
				if (d <= 0) { l.Snap = cur; continue; }
				int took = Valid(l) ? l.A.TakeForCraft(l.Backing, d) : 0;
				Taken += took;
				if (took < d) { Shortfall += d - took; Warn($"a craft used {d} {Lang.GetItemNameValue(l.Proxy.type)} from an armory, {took} were there"); }
				l.Snap = cur;
			}
		}

		public static void Reconcile() { if (Main.netMode == NetmodeID.MultiplayerClient) return; lock (Gate) ReconcileLocked(); }

		/// <summary>Rebuilds the proxies of <paramref name="heart"/>'s armories from their racks. Asks Magic Storage to refresh when anything changed (<paramref name="refreshUi"/>).</summary>
		public static void Sync(TEStorageHeart heart, bool refreshUi)
		{
			if (heart == null || Main.netMode == NetmodeID.MultiplayerClient) return;
			var changed = new HashSet<int>();
			lock (Gate)
			{
				ReconcileLocked();
				_weaponFrom = null;
				var seen = new HashSet<Link>();
				var offered = new List<Item>();
				foreach (var a in ArmoriesOf(heart))
				{
					if (!Alive(a)) continue;
					foreach (var arr in a.CraftRacks())
						for (int i = 0; i < arr.Length; i++)
						{
							Item it = arr[i];
							if (!a.CraftOffers(it)) continue;
							if (ByBacking.TryGetValue(it, out var l) && (!ReferenceEquals(l.A, a) || l.Proxy.IsAir || l.Proxy.type != it.type || l.Proxy.prefix != it.prefix)) { changed.Add(l.Proxy.type); Drop(l); l = null; }
							if (l == null)
							{
								l = new Link { A = a, Backing = it, Proxy = it.Clone(), Snap = it.stack };
								ByBacking[it] = l; ByProxy[l.Proxy] = l; changed.Add(it.type);
							}
							else if (l.Proxy.stack != it.stack || l.Snap != it.stack) { l.Proxy.stack = it.stack; l.Snap = it.stack; changed.Add(it.type); }
							seen.Add(l); offered.Add(l.Proxy);
						}
				}
				foreach (var l in ByProxy.Values.Where(x => !seen.Contains(x)).ToList()) { changed.Add(l.Proxy.type); Drop(l); }
				Published[heart.ID] = offered.ToArray();
			}
			if (refreshUi && changed.Count > 0)
			{
				try { if (MagicUI.uiInterface?.CurrentState is not null) { MagicUI.SetRefresh(); MagicUI.SetNextCollectionsToRefresh(changed); } } catch (Exception) { }
			}
		}

		/// <summary>Before an ingredient is consumed: every proxy is lowered to what its rack item still holds (to 0 when the item left the racks, was reserved or favorited). Never raised mid-craft.</summary>
		public static void Validate()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			lock (Gate)
			{
				ReconcileLocked();
				foreach (var l in ByProxy.Values)
				{
					int cap = Valid(l) ? l.Backing.stack : 0;
					if (StackOf(l.Proxy) > cap) { if (cap <= 0) l.Proxy.stack = 0; else l.Proxy.stack = cap; }
					l.Snap = StackOf(l.Proxy);
				}
			}
		}

		/// <summary>Magic Storage is about to take <paramref name="n"/> off <paramref name="item"/>: when it is one of the proxies, the rack item pays exactly that many now.</summary>
		public static void Consumed(Item item, int n)
		{
			if (item == null || n <= 0 || Main.netMode == NetmodeID.MultiplayerClient) return;
			lock (Gate)
			{
				if (!ByProxy.TryGetValue(item, out var l)) return;
				int took = Valid(l) ? l.A.TakeForCraft(l.Backing, n) : 0;
				Taken += took;
				if (took < n) { Shortfall += n - took; Warn($"a craft used {n} {Lang.GetItemNameValue(item.type)} from an armory, {took} were there"); }
				l.Snap = Math.Max(0, StackOf(item) - n); // the stack the proxy will have once Magic Storage has taken them
				if (item.damage > 0 && item.maxStack == 1) _weaponFrom = l.A;
			}
		}

		/// <summary>After each craft: settle, and when the craft used an armory weapon and made a weapon, send the result back to that armory once Magic Storage has deposited it.</summary>
		public static void Crafted(Recipe recipe)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			lock (Gate)
			{
				ReconcileLocked();
				var made = recipe?.createItem;
				if (_weaponFrom != null && made != null && made.type > ItemID.None && made.damage > 0 && made.maxStack == 1 && !made.accessory) Returns.Add((_weaponFrom, made.type));
				_weaponFrom = null;
			}
		}

		/// <summary>After the UI ran (the click that crafted is done): settle, and move each crafted weapon from the network back to the armory its ingredient came from. Single player (a client never crafts from racks).</summary>
		public static void AfterUi(TEStorageHeart heart)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			List<(ArmoryTE a, int type)> todo;
			lock (Gate) { ReconcileLocked(); todo = Returns.ToList(); Returns.Clear(); }
			if (heart == null || todo.Count == 0) return;
			foreach (var (a, type) in todo) ReturnOne(heart, a, type);
			try { MagicUI.SetRefresh(); } catch (Exception) { }
		}

		/// <summary>Takes one <paramref name="type"/> from the network into the armory; what the armory cannot hold goes back into the network, and what neither holds drops at the armory. Returns true when it moved.</summary>
		public static bool ReturnOne(TEStorageHeart heart, ArmoryTE a, int type)
		{
			if (heart == null || !Alive(a)) return false;
			var want = new Item(); want.SetDefaults(type); want.stack = 1;
			Item got;
			try { got = heart.TryWithdraw(want, false); } catch (Exception) { return false; }
			if (got == null || got.IsAir) return false;
			var rest = a.Deposit(got);
			if (rest != null && !rest.IsAir)
			{
				try { heart.TryDeposit(rest); } catch (Exception) { }
				if (!rest.IsAir) Item.NewItem(new Terraria.DataStructures.EntitySource_TileEntity(a), a.Center, rest);
				return false;
			}
			Returned++;
			return true;
		}

		/// <summary>Self-check seam: forgets every proxy (each is turned to air first) and the counters.</summary>
		internal static void ResetForTest()
		{
			lock (Gate) { foreach (var l in ByProxy.Values.ToList()) Drop(l); Published.Clear(); Returns.Clear(); _weaponFrom = null; Taken = Shortfall = Returned = 0; _armHeart = -1; _arm = new(); }
		}

		private static void Warn(string what) { try { Levy.Instance?.Logger.Warn("Levy/MagicStorage crafting: " + what); } catch (Exception) { } }
	}
}
