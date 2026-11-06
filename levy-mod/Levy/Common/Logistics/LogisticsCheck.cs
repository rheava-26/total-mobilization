using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// Boot self-check of the logistics arithmetic and its orchestration, run once after recipes are final. It uses REAL recipes and a REAL (unplaced) armory, with the station scan replaced by a fixed set:
	/// ratios and rounding, bucket vs random picking, partial runs that resume when stock arrives, the materials checklist (sets, groups, intermediates), station blocking, sell selection and tier bands,
	/// auto-buy budget limits, order save/wire round trips and the server-side clamps. Pure data; never throws.
	/// </summary>
	public class LogisticsCheck : ModSystem
	{
		private static int[] T(params int[] t) => t;
		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static void Stock(ArmoryTE a, int type, long n) { while (n > 0) { var it = Make(type); int k = (int)Math.Min(n, Math.Max(1, it.maxStack)); it.stack = k; a.Deposit(it); n -= k; } }
		private static long Held(ArmoryTE a, int type) => a.CountInStorage(type);
		private static int PerCraft(int outType, int ingIndex = 0) { var r = Crafting.RecipesFor(outType)[0]; return Crafting.Ingredients(r)[ingIndex].Count; }

		public override void PostSetupRecipes()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			var savedStations = Crafting.StationOverride;
			try
			{
				Crafting.ResetCache();
				Crafting.StationOverride = new HashSet<int> { TileID.WorkBenches, TileID.Anvils, TileID.Furnaces, TileID.Bottles, TileID.Tables, TileID.Chairs };
				Math_(Check);
				Orders_(Check);
				Sell_(Check);
				Buy_(Check);
				Search_(Check);
				CraftBrowser_(Check);
				Wire_(Check);
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message} {string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim()))}"); }
			finally { Crafting.StationOverride = savedStations; OrderBook.Clear(); ProgressionTier.Reset(); Crafting.ResetCache(); }

			if (failures.Count == 0) Mod.Logger.Info($"Levy logistics self-check: {ok} checks OK");
			else
			{
				Mod.Logger.Warn($"Levy logistics self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
				foreach (var f in failures) Mod.Logger.Warn("  logistics: " + f);
			}
		}

		// ------------------------------------------------------------------------------------------------------------------ search filter and pylon reach
		private static void Search_(Action<bool, string> Check)
		{
			Check(ItemFilter.Matches(Make(ItemID.Handgun), FilterCat.Weapons) && ItemFilter.Matches(Make(ItemID.Handgun), FilterCat.Ranged) && !ItemFilter.Matches(Make(ItemID.Handgun), FilterCat.Melee), "filter: a Handgun is a ranged weapon");
			Check(ItemFilter.Matches(Make(ItemID.IronBroadsword), FilterCat.Melee) && !ItemFilter.Matches(Make(ItemID.IronBroadsword), FilterCat.Ammo), "filter: a sword is melee, not ammo");
			Check(ItemFilter.Matches(Make(ItemID.MusketBall, 10), FilterCat.Ammo) && !ItemFilter.Matches(Make(ItemID.MusketBall, 10), FilterCat.Weapons), "filter: a Musket Ball is ammo, not a weapon");
			Check(ItemFilter.Matches(Make(ItemID.IronHelmet), FilterCat.Gear) && ItemFilter.Matches(Make(ItemID.HermesBoots), FilterCat.Gear), "filter: armor and accessories are gear");
			Check(ItemFilter.Matches(Make(ItemID.LesserHealingPotion, 5), FilterCat.Potions) && !ItemFilter.Matches(Make(ItemID.LesserHealingPotion, 5), FilterCat.Tools), "filter: a healing potion is a potion");
			Check(ItemFilter.Matches(Make(ItemID.IronPickaxe), FilterCat.Tools) && !ItemFilter.Matches(Make(ItemID.IronPickaxe), FilterCat.Weapons), "filter: a pickaxe is a tool, not a weapon");
			Check(ItemFilter.Matches(Make(ItemID.Torch, 5), FilterCat.Blocks) && ItemFilter.Matches(Make(ItemID.DirtBlock, 5), FilterCat.Blocks) && !ItemFilter.Matches(Make(ItemID.Torch, 5), FilterCat.Weapons), "filter: torches and blocks are blocks & other");
			Check(ItemFilter.Matches(Make(ItemID.Handgun), FilterCat.All) && ItemFilter.Cats()[0] == FilterCat.All, "filter: All matches everything");
			Check(ItemFilter.NameMatches("Iron Broadsword", "BROAD") && ItemFilter.NameMatches("Iron Broadsword", "iron sword".Replace("sword", "broad")) && !ItemFilter.NameMatches("Iron Broadsword", "gold") && ItemFilter.NameMatches("Torch", ""), "search: case-insensitive name match, every word, empty matches all");

			float px = LevyConstants.TilePx, far = LevyConstants.PylonShareRadius * px * 5f;
			var armory = new Microsoft.Xna.Framework.Vector2(1000f * px, 500f * px); var farNpc = new Microsoft.Xna.Framework.Vector2(1000f * px + far, 500f * px);
			SettlementSystem.PylonPoint P(Microsoft.Xna.Framework.Vector2 c, bool ok, string n) => new SettlementSystem.PylonPoint { Center = c, Valid = ok, Name = n };
			var both = new[] { P(armory, true, "Forest Pylon"), P(farNpc, true, "Snow Pylon") };
			var (r1, n1) = Shopping.ReachOf(armory, farNpc, false, both, true);
			Check(r1 && n1 == "Snow Pylon", "pylon reach: a shopkeeper by a second working pylon is in reach and names it");
			var remoteBad = new[] { P(armory, true, "Forest Pylon"), P(farNpc, false, "Snow Pylon") };
			Check(!Shopping.ReachOf(armory, farNpc, false, remoteBad, true).inReach, "pylon reach: his pylon has too few town NPCs, so he is out of reach");
			var homeBad = new[] { P(armory, false, "Forest Pylon"), P(farNpc, true, "Snow Pylon") };
			Check(!Shopping.ReachOf(armory, farNpc, false, homeBad, true).inReach, "pylon reach: the armory's own pylon is not working, so nothing is reached through pylons");
			Check(Shopping.ReachOf(armory, farNpc, true, homeBad, true) == (true, null), "pylon reach: a shopkeeper in the settlement is always in reach, with no pylon named");
			Check(!Shopping.ReachOf(armory, farNpc, false, both, false).inReach, "pylon reach: toggle off, the new rule reaches nobody outside the settlement");
			Check(Shopping.LegacyReach(armory, farNpc, false, remoteBad) && !Shopping.LegacyReach(armory, farNpc, false, new[] { P(armory, true, "Forest Pylon") }), "pylon reach: toggle off keeps the earlier rule (any pylon at both ends, working or not)");
			Check(!Shopping.LegacyReach(armory, armory, false, Array.Empty<SettlementSystem.PylonPoint>()) && Shopping.LegacyReach(armory, farNpc, true, null), "pylon reach: no pylons means the settlement only");
		}

		// ------------------------------------------------------------------------------------------------------------------ recipe browser and instant craft
		private static RecipeEntry Entry(List<RecipeEntry> l, Recipe r) => l.FirstOrDefault(e => ReferenceEquals(e.R, r));

		private static void CraftBrowser_(Action<bool, string> Check)
		{
			var sword = Crafting.RecipesFor(ItemID.IronBroadsword)[0]; // 8 Iron Bars at an Anvil
			var a = new ArmoryTE(); var p = new Player(); var inv = new PlayerStock(p);
			StockPool Pool() => new StockPool(a, false, false, p);

			inv.Put(Make(ItemID.IronBar, 8));
			var e = Entry(CraftBrowser.Scan(a, Pool()), sword);
			Check(e != null && e.CanCraft && e.Crafts == 1 && CraftBrowser.InView(e, RecipeView.CanCraft), "browser: Can craft lists a recipe whose ingredients are only in the inventory");
			inv.Take(ItemID.IronBar, 100);

			inv.Put(Make(ItemID.IronBar, 3));
			e = Entry(CraftBrowser.Scan(a, Pool()), sword);
			Check(e != null && !e.CanCraft && e.AnyMaterial && CraftBrowser.InView(e, RecipeView.HasMaterials) && !CraftBrowser.InView(e, RecipeView.CanCraft) && CraftBrowser.InView(e, RecipeView.All), "browser: Has materials shows a partly stocked recipe, Can craft does not");
			inv.Take(ItemID.IronBar, 100);
			e = Entry(CraftBrowser.Scan(a, Pool()), sword);
			Check(e != null && !e.AnyMaterial && !CraftBrowser.InView(e, RecipeView.HasMaterials) && CraftBrowser.InView(e, RecipeView.All), "browser: with nothing in stock only All lists it");

			var wood = Crafting.RecipesFor(ItemID.WoodenSword);
			var wr = wood.Count > 0 ? wood[0] : null; var wi = wr == null ? Array.Empty<Ingredient>() : Crafting.Ingredients(wr);
			if (wr != null && wi.Length == 1 && wi[0].Types.Length > 1)
			{
				int other = wi[0].Types.First(t => t != wi[0].Primary);
				inv.Put(Make(other, wi[0].Count));
				e = Entry(CraftBrowser.Scan(a, Pool()), wr);
				Check(e != null && e.CanCraft, "browser: a recipe group (any wood) counts a different wood from the inventory");
				inv.Take(other, 1000);
			}
			else Check(true, "browser: wooden sword has no wood group in this build (skipped)");

			// a craft pulls from the inventory, then the armory, then the chest, and the sword lands in the inventory
			int ci = Main.maxChests - 1; var savedChest = Main.chest[ci]; Main.chest[ci] = new Chest(); Main.chest[ci].item[0] = Make(ItemID.IronBar, 3);
			try
			{
				inv.Put(Make(ItemID.IronBar, 3)); Stock(a, ItemID.IronBar, 2);
				var pool = Pool(); pool.Sources.Add(new ChestStock(ci));
				int made = CraftBrowser.Craft(sword, 1, a, pool);
				Check(made == 1, "craft: made the sword (" + made + ")");
				Check(inv.Count(ItemID.IronBar) == 0 && Held(a, ItemID.IronBar) == 0 && new ChestStock(ci).Count(ItemID.IronBar) == 0, "craft: ingredients came from inventory, armory and chest together");
				Check(inv.Count(ItemID.IronBroadsword) == 1 && Held(a, ItemID.IronBroadsword) == 0, "craft: the result went to the inventory");

				// a full inventory overflows to the armory
				var full = new Player(); for (int i = 0; i < PlayerStock.Slots; i++) full.inventory[i] = Make(ItemID.DirtBlock, 999);
				var a2 = new ArmoryTE(); Stock(a2, ItemID.IronBar, 8);
				int made2 = CraftBrowser.Craft(sword, 1, a2, new StockPool(a2, false, false, full));
				Check(made2 == 1 && Held(a2, ItemID.IronBroadsword) == 1 && new PlayerStock(full).Count(ItemID.IronBroadsword) == 0, "craft: a full inventory overflows the result to the armory");
				Check(CraftBrowser.Craft(sword, 1, a2, new StockPool(a2, false, false, full)) == 0, "craft: nothing in stock crafts nothing");
			}
			finally { Main.chest[ci] = savedChest; }
		}

		// ------------------------------------------------------------------------------------------------------------------ ratios, picking, partial
		private static void Math_(Action<bool, string> Check)
		{
			Check(OrderMath.Split(99, new[] { 33, 33, 33 }).SequenceEqual(new[] { 33, 33, 33 }), "99 over 33/33/33 is 33 each");
			Check(OrderMath.Split(100, new[] { 33, 33, 34 }).SequenceEqual(new[] { 33, 33, 34 }), "100 over 33/33/34 is 33/33/34");
			Check(OrderMath.Split(10, new[] { 1, 1, 1 }).SequenceEqual(new[] { 4, 3, 3 }), "10 over three equal lines is 4/3/3 (remainder to the earliest)");
			Check(OrderMath.Split(7, new[] { 0, 0 }).Sum() == 7, "all-zero weights still split the whole total");
			Check(OrderMath.Split(5, new[] { 0, 1 }).SequenceEqual(new[] { 0, 5 }), "a zero-weight line gets nothing");
			bool sumsOk = true; var rnd = new Random(1);
			for (int k = 0; k < 500; k++) { int n = rnd.Next(1, 8); var w = Enumerable.Range(0, n).Select(_ => rnd.Next(0, 100)).ToArray(); int t = rnd.Next(0, 3000); if (OrderMath.Split(t, w).Sum() != (w.Sum() == 0 && n > 0 ? t : (w.Sum() == 0 ? 0 : t))) sumsOk = false; }
			Check(sumsOk, "500 random splits always add up to the total");

			// bucket: with everything craftable, 12 units at 50/25/25 land exactly 6/3/3 and the ratio holds at every step
			var planned = OrderMath.Split(12, new[] { 50, 25, 25 }); var done = new int[3]; var yes = new[] { true, true, true };
			bool balanced = true;
			for (int i = 0; i < 12; i++)
			{
				int p = OrderMath.PickLine(planned, done, yes, false, 0); if (p < 0) { balanced = false; break; }
				done[p]++;
				// never more than one unit of a bucket ahead of its quota share
				for (int j = 0; j < 3; j++) if (done[j] > Math.Ceiling(planned[j] * (i + 1) / 12.0) + 0) balanced = false;
			}
			Check(done.SequenceEqual(planned) && balanced, "bucket mode fills 6/3/3 and keeps the ratio all the way");
			Check(OrderMath.PickLine(planned, done, yes, false, 0) == -1, "bucket: nothing owed picks nothing");
			// bucket with one line unmakeable: the others carry on and the stuck line keeps its quota
			var d2 = new int[3]; var some = new[] { false, true, true };
			for (int i = 0; i < 20; i++) { int p = OrderMath.PickLine(planned, d2, some, false, 0); if (p < 0) break; d2[p]++; }
			Check(d2[0] == 0 && d2[1] == 3 && d2[2] == 3, "bucket: a line that cannot be made is skipped, the rest still finish");

			// random: each line still ends exactly on its quota, and the order of picks differs from bucket
			var pl = OrderMath.Split(3000, new[] { 33, 33, 34 }); var dr = new int[3]; var seq = new List<int>(); var rng = new Random(42);
			for (int i = 0; i < 3000; i++) { int p = OrderMath.PickLine(pl, dr, yes, true, rng.NextDouble()); if (p < 0) break; dr[p]++; seq.Add(p); }
			Check(dr.SequenceEqual(pl) && seq.Count == 3000, "random mode ends exactly on every quota (33/33/34 of 3000)");
			var bucketSeq = new List<int>(); var db = new int[3];
			for (int i = 0; i < 3000; i++) { int p = OrderMath.PickLine(pl, db, yes, false, 0); db[p]++; bucketSeq.Add(p); }
			Check(!seq.Take(60).SequenceEqual(bucketSeq.Take(60)), "random picks are not the bucket order");
			// random first 300 picks stay near the ratio (it is random, not lopsided)
			var first = new int[3]; foreach (int p in seq.Take(300)) first[p]++;
			Check(first.All(c => c > 70 && c < 130), "random mode's first 300 picks are roughly a third each");

			// partial run: 100 of 120 bars at 6 per item and 20 wanted, then the missing 20 arrive
			var bars = 100L; var (now, later) = OrderMath.Partial(20, new[] { 6 }, _ => bars);
			Check(now == 16 && later == 4, "100 of 120 bars at 6 per item: 16 now, 4 later");
			bars = 120; (now, later) = OrderMath.Partial(20, new[] { 6 }, _ => bars);
			Check(now == 20 && later == 0, "with the other 20 bars: all 20");
			Check(OrderMath.MaxCrafts(5, new[] { 2, 3 }, i => i == 0 ? 100 : 7) == 2, "the scarcest ingredient limits the crafts");
			Check(OrderMath.MaxCrafts(5, new int[0], _ => 0) == 5, "no ingredients: only the wanted count limits");
		}

		// ------------------------------------------------------------------------------------------------------------------ real recipes, real armory
		private static void Orders_(Action<bool, string> Check)
		{
			OrderBook.Clear();
			// --- partial then resume, on a real recipe (Iron Broadsword from an Iron/Lead Bar group)
			int per = PerCraft(ItemID.IronBroadsword);
			var a = new ArmoryTE();
			Stock(a, ItemID.IronBar, per * 16L);
			var o = new LogisticsOrder { ArmoryId = -1, Total = 20, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword), Weight = 1 } } };
			o.Replan();
			LogisticsRunner.RunOn(o, a);
			Check(o.Lines[0].Done == 16 && Held(a, ItemID.IronBroadsword) == 16, $"partial run made 16 of 20 swords from {per * 16} bars (made {o.Lines[0].Done}, in armory {Held(a, ItemID.IronBroadsword)})");
			Check(Held(a, ItemID.IronBar) == 0, "the bars were all used");
			Check(o.Status == "Waiting", "status is Waiting while materials are short (" + o.Status + ")");
			var row = o.Checklist.FirstOrDefault(r => r.Type == ItemID.IronBar);
			Check(row.Need == per * 4L && row.Have == 0 && row.Missing == per * 4L, $"checklist shows the {per * 4} bars still missing (need {row.Need}, have {row.Have})");
			Stock(a, ItemID.IronBar, per * 4L);
			LogisticsRunner.RunOn(o, a);
			Check(o.Lines[0].Done == 20 && Held(a, ItemID.IronBroadsword) == 20 && o.Complete && o.Status == "Done", "when the other bars arrive the order finishes by itself (20 swords, status " + o.Status + ")");
			LogisticsRunner.RunOn(o, a);
			Check(Held(a, ItemID.IronBroadsword) == 20, "a finished order makes no more");

			// --- three buckets of wood weapons, 33% each; limited wood stops it part way, the ratio stays level, more wood finishes
			var wood = new List<int>();
			foreach (int cand in T(ItemID.WoodenSword, ItemID.WoodenBow, ItemID.WoodenBoomerang, ItemID.WoodenHammer, ItemID.WoodYoyo))
			{
				var rs = Crafting.RecipesFor(cand); if (rs.Count == 0) continue;
				var ing = Crafting.Ingredients(rs[0]);
				if (ing.Length == 1 && ing[0].Primary == ItemID.Wood && rs[0].createItem.stack == 1 && rs[0].requiredTile.Count > 0 && wood.Count < 3) wood.Add(cand);
			}
			Check(wood.Count == 3, "three plain wood-only weapon recipes exist for the ratio test (found " + wood.Count + ")");
			if (wood.Count < 3) return;
			int sw = wood[0], bw = wood[1], bm = wood[2];
			int w1 = PerCraft(sw), w2 = PerCraft(bw), w3 = PerCraft(bm);
			var b = new ArmoryTE(); Stock(b, ItemID.Wood, (w1 + w2 + w3) * 10L);
			var ob = new LogisticsOrder { Total = 30, Mode = OrderMode.Bucket, Lines = { new OrderLine { Types = T(sw), Weight = 33 }, new OrderLine { Types = T(bw), Weight = 33 }, new OrderLine { Types = T(bm), Weight = 34 } } };
			ob.Replan();
			Check(ob.Lines.Select(l => l.Planned).SequenceEqual(new[] { 10, 10, 10 }), "30 at 33/33/34 plans 10/10/10 (" + string.Join("/", ob.Lines.Select(l => l.Planned)) + ")");
			LogisticsRunner.RunOn(ob, b);
			Check(ob.Complete && Held(b, sw) == 10 && Held(b, bw) == 10 && Held(b, bm) == 10, "bucket order with enough wood makes 10 swords, 10 bows, 10 boomerangs");
			var c = new ArmoryTE(); Stock(c, ItemID.Wood, (w1 + w2 + w3) * 5L);
			var oc = new LogisticsOrder { Total = 30, Mode = OrderMode.Bucket, Lines = { new OrderLine { Types = T(sw) }, new OrderLine { Types = T(bw) }, new OrderLine { Types = T(bm) } } };
			oc.Replan(); LogisticsRunner.RunOn(oc, c);
			var dn = oc.Lines.Select(l => l.Done).ToArray();
			Check(dn.Max() - dn.Min() <= 1 && dn.Sum() >= 9, "a bucket run that runs out of wood stays level across the lines (" + string.Join("/", dn) + ")");
			Stock(c, ItemID.Wood, (w1 + w2 + w3) * 6L); LogisticsRunner.RunOn(oc, c);
			Check(oc.Complete, "and finishes when more wood arrives");
			var d = new ArmoryTE(); Stock(d, ItemID.Wood, (w1 + w2 + w3) * 10L);
			var od = new LogisticsOrder { Total = 30, Mode = OrderMode.Random, Seed = 7, Lines = { new OrderLine { Types = T(sw) }, new OrderLine { Types = T(bw) }, new OrderLine { Types = T(bm) } } };
			od.Replan(); LogisticsRunner.RunOn(od, d);
			Check(od.Complete && od.Lines.All(l => l.Done == 10), "random mode also lands on 10/10/10 (" + string.Join("/", od.Lines.Select(l => l.Done)) + ")");

			// --- materials checklist for a set: 2 Iron armor sets from an empty armory = the sum of the three recipes, twice
			int bars = 0; foreach (int t in T(ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves)) bars += Crafting.Ingredients(Crafting.RecipesFor(t)[0]).Where(i => i.Primary == ItemID.IronBar).Sum(i => i.Count);
			var e = new ArmoryTE();
			var oe = new LogisticsOrder { Total = 2, Lines = { new OrderLine { Types = T(ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves) } } };
			oe.Replan(); LogisticsRunner.RunOn(oe, e);
			var need = oe.Checklist.Where(r => r.Type == ItemID.IronBar).Select(r => r.Need).FirstOrDefault();
			Check(oe.Lines[0].Planned == 2 && need == bars * 2L, $"2 iron sets need {bars * 2} bars in the checklist (listed {need})");
			Check(oe.Checklist.All(r => r.Have == 0 && r.Missing == r.Need), "an empty armory has none of it");

			// --- intermediates: only ore in stock; the order smelts the bars (Furnace in range) and then forges the sword
			int oreEach = Crafting.RecipesFor(ItemID.IronBar).SelectMany(r => Crafting.Ingredients(r)).First(i => i.Primary == ItemID.IronOre).Count;
			var f = new ArmoryTE(); Stock(f, ItemID.IronOre, oreEach * per + 5L);
			var of = new LogisticsOrder { Total = 1, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword) } } };
			of.Replan(); LogisticsRunner.RunOn(of, f);
			string barRecipes = string.Join(" ; ", Crafting.RecipesFor(ItemID.IronBar).Select(r => string.Join("+", Crafting.Ingredients(r).Select(i => i.Count + "x" + i.Primary)) + "=>" + r.createItem.stack));
			Check(of.Complete && Held(f, ItemID.IronBroadsword) == 1 && Held(f, ItemID.IronOre) == 5, $"ore is smelted to bars on the way (sword {Held(f, ItemID.IronBroadsword)}, ore left {Held(f, ItemID.IronOre)}; iron bar recipes: {barRecipes}; oreEach {oreEach}, bars per sword {per})");
			var g = new ArmoryTE();
			var og = new LogisticsOrder { Total = 1, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword) } } };
			og.Replan(); LogisticsRunner.RunOn(og, g);
			Check(og.Checklist.Any(r => r.Type == ItemID.IronBar && r.Need == per && r.Missing == per), "with nothing stocked the checklist names the bars (the thing to bring), not the ore");
			var f2 = new ArmoryTE(); Stock(f2, ItemID.IronOre, oreEach * per - 1L);
			var of2 = new LogisticsOrder { Total = 1, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword) } } };
			of2.Replan(); LogisticsRunner.RunOn(of2, f2);
			Check(of2.Lines[0].Done == 0 && Held(f2, ItemID.IronOre) == oreEach * per - 1L, "one ore short of the bars: nothing is smelted or forged, and nothing is wasted");

			// --- stations: without an anvil nothing is made, and the status says why
			var saved = Crafting.StationOverride; Crafting.StationOverride = new HashSet<int> { TileID.Furnaces };
			var h = new ArmoryTE(); Stock(h, ItemID.IronBar, per * 2L);
			var oh = new LogisticsOrder { Total = 2, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword) } } };
			oh.Replan(); LogisticsRunner.RunOn(oh, h);
			Check(oh.Lines[0].Done == 0 && oh.Status.StartsWith("Blocked:station:"), "no anvil in range: nothing is made and the order says it is blocked (" + oh.Status + ")");
			Crafting.StationOverride = saved;
			LogisticsRunner.RunOn(oh, h);
			Check(oh.Complete, "put an anvil in range and the same order completes");

			// --- paused and no-recipe
			var p = new ArmoryTE(); Stock(p, ItemID.IronBar, per * 2L);
			var op = new LogisticsOrder { Total = 2, Paused = true, Lines = { new OrderLine { Types = T(ItemID.IronBroadsword) } } };
			op.Replan(); LogisticsRunner.RunOn(op, p);
			Check(op.Lines[0].Done == 0 && op.Status == "Paused", "a paused order makes nothing");
			int noRecipe = 0; for (int t = 1; t < ItemLoader.ItemCount && noRecipe == 0; t++) if (Crafting.RecipesFor(t).Count == 0 && ContentSamples.ItemsByType[t].value > 0 && !ContentSamples.ItemsByType[t].IsAir) noRecipe = t;
			var oq = new LogisticsOrder { Total = 1, Lines = { new OrderLine { Types = T(noRecipe) } } };
			oq.Replan(); LogisticsRunner.RunOn(oq, new ArmoryTE());
			Check(oq.Status == "NoRecipe", "an item nothing crafts reports NoRecipe (" + oq.Status + ")");

			// --- the order book keeps progress across an edit, caps the count, and removes
			var q = new LogisticsOrder { ArmoryId = 5, Total = 10, Lines = { new OrderLine { Types = T(ItemID.WoodenSword), Weight = 1 } } };
			var stored = OrderBook.Upsert(q);
			stored.Lines[0].Done = 4;
			var edit = new LogisticsOrder { Id = stored.Id, ArmoryId = 5, Total = 25, Lines = { new OrderLine { Types = T(ItemID.WoodenSword), Weight = 1 } } };
			var after = OrderBook.Upsert(edit);
			Check(after.Lines[0].Done == 4 && after.Lines[0].Planned == 25, "editing the total keeps what is already made and re-plans the rest");
			var smaller = new LogisticsOrder { Id = stored.Id, ArmoryId = 5, Total = 2, Lines = { new OrderLine { Types = T(ItemID.WoodenSword), Weight = 1 } } };
			Check(OrderBook.Upsert(smaller).Lines[0].Planned == 4, "a total below what is made never un-makes it (planned stays at the 4 done)");
			OrderBook.Clear(); bool refused = false;
			for (int i = 0; i < LogisticsConstants.MaxOrders + 1; i++) if (OrderBook.Upsert(new LogisticsOrder { ArmoryId = 1, Total = 1, Lines = { new OrderLine { Types = T(ItemID.WoodenSword) } } }) == null) refused = true;
			Check(refused && OrderBook.Orders.Count == LogisticsConstants.MaxOrders, "the order book caps at " + LogisticsConstants.MaxOrders);
			Check(OrderBook.Remove(OrderBook.Orders[0].Id) && OrderBook.Orders.Count == LogisticsConstants.MaxOrders - 1, "an order can be removed");
		}

		// ------------------------------------------------------------------------------------------------------------------ sell selection
		private static void Sell_(Action<bool, string> Check)
		{
			var table = new float[ItemLoader.ItemCount]; for (int i = 0; i < table.Length; i++) table[i] = ProgressionTier.Inf;
			table[ItemID.IronBroadsword] = 3f; table[ItemID.WoodenBow] = 0f; table[ItemID.Minishark] = 2f; table[ItemID.Excalibur] = 8f; table[ItemID.IronHelmet] = 3f; table[ItemID.TerraBlade] = 13f;
			ProgressionTier.SetForTest(table);
			var a = new ArmoryTE();
			a.Storage[0] = Make(ItemID.IronBroadsword); a.Storage[1] = Make(ItemID.WoodenBow); a.Storage[2] = Make(ItemID.Minishark); a.Storage[3] = Make(ItemID.Excalibur);
			a.Storage[4] = Make(ItemID.IronHelmet); a.Storage[5] = Make(ItemID.TerraBlade); a.Storage[6] = Make(ItemID.CopperCoin, 50);
			a.Storage[7] = Make(ItemID.IronBroadsword); a.Storage[7].favorited = true;
			var melee = Selling.Select(a, new Selling.Filter { Category = SellCat.Melee });
			Check(melee.SequenceEqual(new[] { 0, 3, 5 }), "category Melee picks the swords, not the favorited one (" + string.Join(",", melee) + ")");
			var ranged = Selling.Select(a, new Selling.Filter { Category = SellCat.Ranged });
			Check(ranged.SequenceEqual(new[] { 1, 2 }), "category Ranged picks the bow and the Minishark (" + string.Join(",", ranged) + ")");
			Check(Selling.Select(a, new Selling.Filter { Category = SellCat.Armor }).SequenceEqual(new[] { 4 }), "category Armor picks the helmet");
			var pre = Selling.Select(a, new Selling.Filter { TierLo = Selling.Tiers[0].Lo, TierHi = Selling.Tiers[0].Hi });
			Check(pre.SequenceEqual(new[] { 0, 1, 2, 4 }), "tier Pre-Hardmode picks everything at or below tier 7 (" + string.Join(",", pre) + ")");
			var preSwords = Selling.Select(a, new Selling.Filter { Category = SellCat.Melee, TierLo = Selling.Tiers[0].Lo, TierHi = Selling.Tiers[0].Hi });
			Check(preSwords.SequenceEqual(new[] { 0 }), "all pre-Hardmode swords = just the Iron Broadsword");
			var hm = Selling.Select(a, new Selling.Filter { TierLo = Selling.Tiers[1].Lo, TierHi = Selling.Tiers[1].Hi });
			Check(hm.SequenceEqual(new[] { 3 }), "tier Early Hardmode picks Excalibur only");
			Check(Selling.Select(a, new Selling.Filter { Category = SellCat.Melee, TierLo = Selling.Tiers[2].Lo, TierHi = Selling.Tiers[2].Hi }).SequenceEqual(new[] { 5 }), "tier Late Hardmode melee picks the Terra Blade");
			Check(Selling.Select(a, new Selling.Filter()).Count == 0, "an empty filter selects nothing");
			Check(!OrderMath.InTierBand(-1f, 0f, 99f), "an item with no tier is in no band");
			// drag select: a 4-wide grid, corners given backwards
			var cells = Selling.SelectRect(a, 4, 3, 1, 0, 0);
			Check(cells.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5, 7 }) || cells.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }), "dragging corner to corner selects the rectangle, skipping coins (" + string.Join(",", cells) + ")");
			Check(OrderMath.DragCells(3, 1, 0, 0) == (0, 0, 3, 1), "drag corners are normalised");
			Check(Selling.SelectRect(a, 4, 0, 0, 0, 0).SequenceEqual(new[] { 0 }), "a one-cell drag selects one slot");
			Check(Selling.CategoryOf(Make(ItemID.HealingPotion)) == SellCat.Potion, "a healing potion is a Potion");
			ProgressionTier.Reset();
		}

		// ------------------------------------------------------------------------------------------------------------------ auto-buy
		private static void Buy_(Action<bool, string> Check)
		{
			Check(OrderMath.BuyCount(100, 10, 450, -1) == 45, "budget 450 at 10 each buys 45, not 100");
			Check(OrderMath.BuyCount(10, 10, 450, -1) == 10, "the wanted count caps a big budget");
			Check(OrderMath.BuyCount(100, 10, 450, 7) == 7, "the shop's stock caps it");
			Check(OrderMath.BuyCount(100, 10, 9, -1) == 0, "a budget below one unit buys nothing");
			Check(OrderMath.BuyCount(5, 0, 0, -1) == 5, "a free item is limited only by the count");
			Check(OrderMath.BuyCount(0, 10, 100, -1) == 0 && OrderMath.BuyCount(5, 10, 0, -1) == 0, "nothing wanted or no budget buys nothing");
			var gun = new BuyRule { Kind = BuyRule.KindAnyGun, Count = 2, Budget = 1000 };
			Check(Shopping.Matches(gun, Make(ItemID.Handgun)) && !Shopping.Matches(gun, Make(ItemID.MusketBall, 1)) && !Shopping.Matches(gun, Make(ItemID.IronBroadsword)), "'any gun' matches a Handgun, not a Musket Ball or a sword");
			var ammo = new BuyRule { Kind = BuyRule.KindAnyAmmo, Type = AmmoID.Bullet, Count = 10, Budget = 1000 };
			Check(Shopping.Matches(ammo, Make(ItemID.MusketBall)) && !Shopping.Matches(ammo, Make(ItemID.WoodenArrow)), "'any bullet' matches a Musket Ball, not a Wooden Arrow");
			Check(Shopping.Matches(new BuyRule { Kind = BuyRule.KindAnyAmmo, Type = 0 }, Make(ItemID.WoodenArrow)), "'any ammo' matches an arrow");
			var item = new BuyRule { Kind = BuyRule.KindItem, Type = ItemID.ChlorophyteBar, Count = 5, Budget = 10 };
			Check(Shopping.Matches(item, Make(ItemID.ChlorophyteBar)) && !Shopping.Matches(item, Make(ItemID.IronBar)), "an item rule matches only that item");
			var spent = new BuyRule { Count = 5, Bought = 2, Budget = 100, Spent = 100 };
			Check(spent.Finished && !new BuyRule { Count = 5, Bought = 2, Budget = 100, Spent = 50 }.Finished && new BuyRule { Count = 2, Bought = 2, Budget = 100 }.Finished, "a rule is finished at its count or its budget");
			// chained: a Chlorophyte Bullet order's checklist names Musket Balls and Chlorophyte Bars, i.e. what shortfall-buying would purchase
			var a = new ArmoryTE();
			var o = new LogisticsOrder { Total = 100, BuyShortfalls = true, ShortfallBudget = 5000, Lines = { new OrderLine { Types = T(ItemID.ChlorophyteBullet) } } };
			o.Replan(); var plan = LogisticsRunner.PlanRemaining(o, a, new CountCache(_ => 0));
			Check(plan.Missing.ContainsKey(ItemID.MusketBall) && plan.Missing.ContainsKey(ItemID.ChlorophyteBar), "a Chlorophyte Bullet order is short of Musket Balls and Chlorophyte Bars (the things auto-buy would purchase)");
		}

		// ------------------------------------------------------------------------------------------------------------------ wire, save, clamps, seeker
		private static void Wire_(Action<bool, string> Check)
		{
			var o = new LogisticsOrder { Id = 3, Name = "Front line", ArmoryId = 9, Mode = OrderMode.Random, Total = 120, Seed = 99, BuyShortfalls = true, ShortfallBudget = 777, Status = "Waiting",
				Lines = { new OrderLine { Types = T(ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves), Weight = 50, Planned = 60, Done = 7 }, new OrderLine { Types = T(ItemID.WoodenBow), Weight = 50, Planned = 60, Done = 3 } },
				Rules = { new BuyRule { Kind = 1, Count = 4, Budget = 5000, Bought = 1, Spent = 200 } },
				Checklist = { new ChecklistRow { Type = ItemID.IronBar, Need = 300, Have = 120 } } };
			foreach (bool rt in new[] { false, true })
			{
				var ms = new MemoryStream(); using (var w = new BinaryWriter(ms)) o.Write(w, rt);
				var r = LogisticsOrder.Read(new BinaryReader(new MemoryStream(ms.ToArray())));
				bool same = r.Id == 3 && r.Name == "Front line" && r.ArmoryId == 9 && r.Mode == OrderMode.Random && r.Total == 120 && r.Seed == 99 && r.BuyShortfalls && r.ShortfallBudget == 777
					&& r.Lines.Count == 2 && r.Lines[0].Types.Length == 3 && r.Lines[0].Done == 7 && r.Lines[1].Planned == 60 && r.Rules.Count == 1 && r.Rules[0].Budget == 5000 && r.Rules[0].Spent == 200;
				Check(same, "an order survives its own " + (rt ? "wire" : "save") + " format");
				if (rt) Check(r.Status == "Waiting" && r.Checklist.Count == 1 && r.Checklist[0].Need == 300 && r.Checklist[0].Have == 120, "the wire format carries status and checklist");
				else Check(r.Status == "" && r.Checklist.Count == 0, "the save format leaves out the runtime state");
			}
			var bad = new LogisticsOrder { Total = 5, Lines = { new OrderLine { Types = T(0) } } };
			Check(!bad.Sanitize(), "a line naming item 0 is refused");
			bad = new LogisticsOrder { Total = 5, Lines = { new OrderLine { Types = T(ItemLoader.ItemCount + 50) } } };
			Check(!bad.Sanitize(), "a line naming a type beyond the item table is refused");
			bad = new LogisticsOrder { Total = 5 }; Check(!bad.Sanitize(), "an order with no lines is refused");
			bad = new LogisticsOrder { Total = int.MaxValue, ShortfallBudget = long.MaxValue, Lines = { new OrderLine { Types = T(ItemID.WoodenSword), Weight = -5, Planned = -3 } } };
			Check(bad.Sanitize() && bad.Total == LogisticsConstants.MaxTotal && bad.ShortfallBudget == 100_000_000L && bad.Lines[0].Weight == 0 && bad.Lines[0].Planned == 0, "huge or negative numbers are clamped");
			// find-this-item picker candidates: the armory's own storage
			var a = new ArmoryTE(); a.Storage[3] = Make(ItemID.ChlorophyteOre, 10); a.Storage[9] = Make(ItemID.IronBar, 4);
			var cand = FindTarget.Candidates(a);
			Check(cand.Contains(ItemID.ChlorophyteOre) && cand.Contains(ItemID.IronBar) && cand.Count == 2, "the find-this-item chooser lists the armory's items when no network is linked");
		}
	}
}
