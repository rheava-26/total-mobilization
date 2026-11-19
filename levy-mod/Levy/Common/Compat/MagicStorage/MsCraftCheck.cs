using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MagicStorage;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// Boot self-check of crafting from armory racks through Magic Storage, on a real in-memory network (storage heart, a storage unit, an Armory Heart, an armory) and Magic Storage's OWN craft code
	/// (<c>CraftingGUI.AttemptLazyBatchCraft</c>, then <c>HandleCraftWithdrawAndDeposit</c>, reached by reflection: they are internal). Recipe: True Excalibur = Excalibur (in the armory) + Chlorophyte Bars (in the network).
	/// Proves: the craft takes exactly the one Excalibur off the armory and the bars off the network, the True Excalibur lands back in the armory, nothing is duplicated or lost (armory + network counts, and a
	/// save/load of the armory), a second craft on the spent ingredient fails, an Excalibur a levy picked up after the list was built is not consumed (the levy keeps it), reserved and favorited items are never
	/// offered, and a multiplayer client is offered nothing.
	/// </summary>
	[JITWhenModsEnabled("MagicStorage")]
	public static class MsCraftCheck
	{
		private const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, I = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		public static void Run(Action<bool, string> check, List<TileEntity> placed, Func<int, int, Type, TileEntity> place)
		{
			var armory = (ArmoryTE)place(140, 40, typeof(ArmoryTE));
			var ah = (TEArmoryHeart)place(143, 40, typeof(TEArmoryHeart));
			var heart = (TEStorageHeart)place(160, 60, typeof(TEStorageHeart));
			var unit = (TECheckUnit)place(162, 60, typeof(TECheckUnit));
			heart.ComponentManager.Link(unit);
			heart.ComponentManager.Link(ah);
			ArmoryCraftLink.ResetForTest();
			check(ReferenceEquals(ah.Armory, armory), "craft: the Armory Heart serves the armory beside it");
			check(heart.GetModules().Any(m => m is ArmoryRacksModule), "craft: a network with an Armory Heart lists the armory module (no Environment Simulator)");

			Recipe rec = null;
			for (int i = 0; i < Recipe.numRecipes; i++) if (Main.recipe[i].createItem.type == ItemID.TrueExcalibur && Main.recipe[i].requiredItem.Any(r => r.type == ItemID.Excalibur)) { rec = Main.recipe[i]; break; }
			check(rec != null, "craft: the True Excalibur recipe exists");
			if (rec == null) return;
			int barType = rec.requiredItem.First(r => r.type != ItemID.Excalibur).type, per = rec.requiredItem.First(r => r.type != ItemID.Excalibur).stack;

			// stock: one Excalibur and a favorited Night's Edge in the armory, bars for one craft and six over in the network
			var exc = Make(ItemID.Excalibur); armory.Deposit(exc);
			var fav = Make(ItemID.NightsEdge); fav.favorited = true; armory.Deposit(fav);
			var bars = Make(barType); bars.stack = per + 6; heart.TryDeposit(bars);
			long Total(int type) => Count(armory, type) + Net(unit, type);
			long excBefore = Total(ItemID.Excalibur), barsBefore = Total(barType), trueBefore = Total(ItemID.TrueExcalibur);

			ArmoryCraftLink.Sync(heart, false);
			var offered = ArmoryCraftLink.Offered(heart).ToList();
			check(offered.Count(o => o.type == ItemID.Excalibur && o.stack == 1) == 1, "craft: the armory's Excalibur is offered once (" + offered.Count + " offered)");
			check(!offered.Any(o => ReferenceEquals(o, exc)), "craft: Magic Storage is handed a proxy, never the armory's own item");
			check(!offered.Any(o => o.type == ItemID.NightsEdge), "craft: a favorited item is not offered");

			// 1. the real batch craft
			bool made = Craft(heart, rec, out var toWithdraw, out var results);
			check(made, "craft: Magic Storage's batch craft makes the True Excalibur");
			var leftover = Withdraw(heart, toWithdraw, results);
			ArmoryCraftLink.AfterUi(heart);
			check(Count(armory, ItemID.Excalibur) == 0 && armory.Storage.All(it => !ReferenceEquals(it, exc)), "craft: the Excalibur left the armory");
			check(Net(unit, barType) == 6, "craft: the network paid exactly " + per + " bars (holds " + Net(unit, barType) + ")");
			check(Count(armory, ItemID.TrueExcalibur) == 1 && Net(unit, ItemID.TrueExcalibur) == 0 && leftover == 0, $"craft: the True Excalibur went back to the armory (armory {Count(armory, ItemID.TrueExcalibur)}, network {Net(unit, ItemID.TrueExcalibur)}, on the player {leftover})");
			check(Total(ItemID.Excalibur) == excBefore - 1 && Total(barType) == barsBefore - per && Total(ItemID.TrueExcalibur) == trueBefore + 1, "craft: armory + network lost exactly the ingredients and gained exactly the result");
			check(ArmoryCraftLink.Taken == 1 && ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.Returned == 1, $"craft: one item taken off the racks, no shortfall, one result returned (taken {ArmoryCraftLink.Taken}, shortfall {ArmoryCraftLink.Shortfall}, returned {ArmoryCraftLink.Returned})");
			check(Count(armory, ItemID.NightsEdge) == 1, "craft: the favorited Night's Edge is untouched");

			// 2. the same (now spent) proxy again, with bars enough for another craft: nothing more is taken
			var more = Make(barType); more.stack = per; heart.TryDeposit(more);
			bool again = Craft(heart, rec, out _, out _, offered);
			check(!again && Count(armory, ItemID.TrueExcalibur) == 1 && ArmoryCraftLink.Taken == 1 && ArmoryCraftLink.Shortfall == 0 && Net(unit, barType) == per + 6, "craft: a second craft on the spent Excalibur fails and takes nothing");

			// 3. a levy takes the next Excalibur after the list was built: the craft fails, the levy keeps it
			var exc2 = Make(ItemID.Excalibur); armory.Deposit(exc2);
			ArmoryCraftLink.Sync(heart, false);
			var stale = ArmoryCraftLink.Offered(heart).ToList();
			check(stale.Any(o => o.type == ItemID.Excalibur && o.stack == 1), "craft: a new Excalibur is offered");
			int idx = Array.FindIndex(armory.Storage, it => ReferenceEquals(it, exc2));
			var levy = new SoldierRecord { Id = 9001, Name = "Crafter" };
			armory.Storage[idx] = new Item(); levy.Gear.Weapon = exc2; // a levy drew it (the list still holds its proxy)
			bool stolen = Craft(heart, rec, out _, out _, stale);
			check(!stolen, "craft: an Excalibur a levy now carries cannot be crafted with");
			check(levy.Gear.HasWeapon && ReferenceEquals(levy.Gear.Weapon, exc2) && exc2.type == ItemID.Excalibur && exc2.stack == 1, "craft: the levy still holds his Excalibur, whole");
			check(Net(unit, barType) == per + 6 && ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.Taken == 1, "craft: the failed craft took no bars, nothing off the racks and nothing short");
			ArmoryCraftLink.Sync(heart, false);
			check(!ArmoryCraftLink.Offered(heart).Any(o => o.type == ItemID.Excalibur && o.stack > 0), "craft: the next sync stops offering the carried Excalibur");

			// 4. a Re-arm's reserved item is never offered (the reservation lives for the tick it was made in)
			var exc3 = Make(ItemID.Excalibur); armory.Deposit(exc3);
			typeof(ArmoryTE).GetMethod("Reserve", I)?.Invoke(armory, new object[] { exc3 });
			ArmoryCraftLink.Sync(heart, false);
			check(!ArmoryCraftLink.Offered(heart).Any(o => o.type == ItemID.Excalibur && o.stack > 0), "craft: an item a Re-arm holds back for its levy is not offered");

			// 5. a multiplayer client is offered nothing (the server owns the racks)
			int nm = Main.netMode;
			try { Main.netMode = NetmodeID.MultiplayerClient; check(!ArmoryCraftLink.Offered(heart).Any(), "craft: a multiplayer client is offered no armory items"); }
			finally { Main.netMode = nm; }

			// 6. save and load the armory: exactly what is on the racks, no proxy, no duplicate
			var tag = new TagCompound(); armory.SaveData(tag);
			var back = new ArmoryTE(); back.LoadData(tag);
			foreach (int t in new[] { ItemID.Excalibur, ItemID.TrueExcalibur, ItemID.NightsEdge, barType })
				check(Count(back, t) == Count(armory, t), $"craft: save/load keeps {Lang.GetItemNameValue(t)} x{Count(armory, t)} (loaded {Count(back, t)})");
			check(Count(back, ItemID.Excalibur) == 1 && Count(back, ItemID.TrueExcalibur) == 1, "craft: after save/load the armory holds the True Excalibur and the one uncrafted Excalibur");
			Levy.Instance?.Logger.Info($"[MsCraft] OBSERVED Excalibur + {per} {Lang.GetItemNameValue(barType)} -> True Excalibur through Magic Storage's batch craft: taken off the racks {ArmoryCraftLink.Taken}, shortfall {ArmoryCraftLink.Shortfall}, returned to the armory {ArmoryCraftLink.Returned}; armory now Excalibur {Count(armory, ItemID.Excalibur)} True Excalibur {Count(armory, ItemID.TrueExcalibur)}, network bars {Net(unit, barType)}; the levy's Excalibur {(levy.Gear.HasWeapon ? "kept" : "LOST")}");
			ArmoryCraftLink.ResetForTest();
		}

		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		private static long Count(ArmoryTE a, int type) => a.Storage.Concat(a.IssueWeapons).Concat(a.IssueAmmo).Where(i => i != null && !i.IsAir && i.type == type).Sum(i => (long)i.stack);
		private static long Net(TECheckUnit u, int type) => u.Items.Where(i => i != null && !i.IsAir && i.type == type).Sum(i => (long)i.stack);

		/// <summary>One craft of <paramref name="rec"/> through Magic Storage's own batch craft, with the network's items (clones, withdrawn afterwards) and the armory module's proxies as the source.</summary>
		private static bool Craft(TEStorageHeart heart, Recipe rec, out List<Item> toWithdraw, out List<Item> results, List<Item> proxies = null)
		{
			var gui = typeof(CraftingGUI);
			var ctxType = gui.GetNestedType("CraftingContext", BindingFlags.NonPublic) ?? throw new InvalidOperationException("CraftingGUI.CraftingContext not found");
			var ctx = Activator.CreateInstance(ctxType, true);
			var modules = heart.GetModules().ToArray();
			object sandbox = Activator.CreateInstance(typeof(EnvironmentSandbox), I, null, new object[] { Main.LocalPlayer, heart }, null); // boxed: a lambda capturing the struct itself would give Levy a type with a Magic Storage value-type field, which fails to load without Magic Storage
			if (proxies == null)
			{
				proxies = new List<Item>();
				foreach (var m in modules) foreach (var i in m.GetAdditionalItems((EnvironmentSandbox)sandbox) ?? Array.Empty<Item>()) if (i.type > ItemID.None && i.stack > 0) proxies.Add(i);
			}
			var heartItems = heart.GetStoredItems().Select(i => i.Clone()).ToList();
			var source = heartItems.Concat(proxies).ToList();
			var fromModule = heartItems.Select(_ => false).Concat(proxies.Select(_ => true)).ToList();
			var counts = new Dictionary<int, int>(); foreach (var i in source) counts[i.type] = (counts.TryGetValue(i.type, out int c) ? c : 0) + i.stack;
			toWithdraw = new List<Item>(); results = new List<Item>();
			void Set(string f, object v) => (ctxType.GetField(f, I) ?? throw new InvalidOperationException("CraftingContext." + f + " not found")).SetValue(ctx, v);
			Set("sourceItems", source); Set("availableItems", source.Select(i => i.Clone()).ToList()); Set("toWithdraw", toWithdraw); Set("results", results);
			Set("itemCounts", counts); Set("fromModule", fromModule); Set("sandbox", sandbox); Set("consumedItemsFromModules", new List<Item>()); Set("modules", (IEnumerable<EnvironmentModule>)modules);
			Set("toCraft", 1); Set("simulation", false); Set("recipe", rec);
			var sel = gui.GetField("selectedRecipe", S) ?? throw new InvalidOperationException("CraftingGUI.selectedRecipe not found");
			var old = sel.GetValue(null);
			sel.SetValue(null, rec);
			try
			{
				var batch = gui.GetMethod("AttemptLazyBatchCraft", S) ?? throw new InvalidOperationException("CraftingGUI.AttemptLazyBatchCraft not found");
				return (bool)batch.Invoke(null, new[] { ctx });
			}
			finally { sel.SetValue(null, old); }
		}

		/// <summary>Magic Storage's single-player finish of a craft: withdraw the network ingredients, deposit the results. Returns how many result items it had to hand the player (network full).</summary>
		private static int Withdraw(TEStorageHeart heart, List<Item> toWithdraw, List<Item> results)
		{
			var m = typeof(CraftingGUI).GetMethod("HandleCraftWithdrawAndDeposit", S) ?? throw new InvalidOperationException("CraftingGUI.HandleCraftWithdrawAndDeposit not found");
			var left = (List<Item>)m.Invoke(null, new object[] { heart, toWithdraw, results });
			return left?.Sum(i => i.stack) ?? 0;
		}
	}
}
