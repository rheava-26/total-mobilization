using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>One recipe as the cost solver sees it: what it makes, and for each ingredient its stack and the other items the recipe accepts in its place (recipe groups).</summary>
	internal sealed class CostRecipe
	{
		public int Out, OutStack = 1;
		/// <summary>Added by a mod (<c>Recipe.Mod</c> is set), not by the base game.</summary>
		public bool Modded;
		public List<(int type, int stack, int[] alts)> Ing = new();
	}

	/// <summary>
	/// The total material cost of every craftable item, in copper: the cheapest recipe's ingredients, each expanded to what makes it, down to items nothing crafts (those count as their own sell value).
	/// A recipe group's ingredient counts as its cheapest member. The solver is a fixpoint over all recipes, so recipe loops (a block that makes walls and walls that make the block, Hellstone and Hellstone
	/// Wall) cannot poison anything: a cost only ever comes from a derivation that bottoms out in raw items, and a loop with no way in (no raw item reaches it) falls back to each member's own value.
	/// Two kinds of recipe are set aside first (<see cref="Prune"/>), so another mod's side route cannot drag a cost down to nothing: a mod's recipe for an item the base game already crafts (the base
	/// game's route prices it), and a worthless route (every ingredient is worth nothing, such as a mod's junk that recycles into Iron Bars) for an item that also has a route through something of value.
	/// Computed once, after all recipes are final (<see cref="PostSetupRecipes"/>), on a server or single-player world; a multiplayer client does not rank weapons.
	/// </summary>
	public class MaterialCost : ModSystem
	{
		private static Dictionary<int, double> _cost = new();
		/// <summary>Number of recipes read and the passes the fixpoint took (diagnostics).</summary>
		internal static int RecipesRead, Passes; internal static double SolveMs;
		/// <summary>Recipes the last <see cref="Prune"/> set aside: mod recipes for items the base game crafts, and worthless routes (diagnostics).</summary>
		internal static int PrunedModded, PrunedWorthless;
		internal static bool Ready => _cost.Count > 0;
		private const int MaxPasses = 64;

		/// <summary>The material cost of one item, in copper, or 0 when nothing crafts it (it is a raw item, a drop or a chest find: its sell price is all it is worth).</summary>
		public static double Of(int type) => _cost.TryGetValue(type, out double c) ? c : 0d;

		public override void PostSetupRecipes() => Ensure();

		private static bool _done;

		/// <summary>Computes the costs once (idempotent), so a boot check that runs before this system's own hook gets the same table.</summary>
		internal static void Ensure()
		{
			if (_done || Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
			_done = true;
			var log = ModContent.GetInstance<MaterialCost>()?.Mod.Logger;
			try
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				var recs = ReadRecipes();
				_cost = Solve(recs, ItemValue, out Passes);
				int pm = PrunedModded, pw = PrunedWorthless;
				RecipesRead = recs.Count; SolveMs = sw.Elapsed.TotalMilliseconds;
				log?.Info($"Levy material cost: {_cost.Count} craftable items from {RecipesRead} recipes in {Passes} passes, {SolveMs:F0} ms; set aside {pm} mod recipes for items the base game crafts and {pw} worthless routes");
				if (Environment.GetEnvironmentVariable("LEVY_MATCOST_DIFF") == "1") LogDiff(recs, log);
			}
			catch (Exception e) { _cost = new(); log?.Warn("Levy material cost failed, weapons rank by sell price alone: " + e.Message); }
		}

		public override void Unload() { _cost = new(); _done = false; }

		private static long ItemValue(int type)
		{
			return ContentSamples.ItemsByType.TryGetValue(type, out Item s) && s != null ? Math.Max(0, s.value) : 0;
		}

		private static List<CostRecipe> ReadRecipes()
		{
			var res = new List<CostRecipe>(Recipe.numRecipes);
			for (int i = 0; i < Recipe.numRecipes; i++)
			{
				Recipe r = Main.recipe[i];
				if (r == null || r.Disabled || r.createItem == null || r.createItem.IsAir) continue;
				var cr = new CostRecipe { Out = r.createItem.type, OutStack = Math.Max(1, r.createItem.stack), Modded = r.Mod != null };
				foreach (Item ing in r.requiredItem)
				{
					if (ing == null || ing.IsAir) continue;
					int[] alts = null;
					foreach (int gid in r.acceptedGroups)
					{
						if (!RecipeGroup.recipeGroups.TryGetValue(gid, out RecipeGroup grp) || !grp.ContainsItem(ing.type)) continue;
						var l = new List<int>(grp.ValidItems);
						alts = l.ToArray();
						break;
					}
					cr.Ing.Add((ing.type, Math.Max(1, ing.stack), alts));
				}
				if (cr.Ing.Count > 0) res.Add(cr);
			}
			return res;
		}

		/// <summary>
		/// The fixpoint. Costs start unknown; a recipe whose ingredients all have a cost (a craftable ingredient needs a derived one, a raw item its value) yields a candidate and the cheapest candidate wins.
		/// Passes repeat until nothing improves (at most <see cref="MaxPasses"/>, which also bounds a recipe loop that would multiply material). What is still unknown afterwards waits on a recipe loop that no raw
		/// item feeds (Hellstone makes Ember Wall and four Ember Walls make Hellstone; Stone Block and Stone Wall): the loop is anchored at its most valuable member, which takes its own sell value and is then
		/// fixed, and everything else in the loop and downstream of it derives from that anchor. Loops are anchored one layer at a time, upstream first. Anything left after that takes its own value. A recipe whose
		/// ingredients are all worthless is a real craft that costs nothing.
		/// </summary>
		internal static Dictionary<int, double> Solve(List<CostRecipe> recs, Func<int, long> value, out int passes)
		{
			var first = SolveCore(recs, value, out passes, out var anchors);
			var kept = Prune(recs, value, anchors, out PrunedModded, out PrunedWorthless);
			if (kept.Count == recs.Count) return first;
			var res = SolveCore(kept, value, out int p2, out _);
			passes += p2;
			return res;
		}

		/// <summary>
		/// The recipes the cost comes from, with two kinds set aside. (1) A mod's recipe for an item the base game also crafts: the base game's route prices it, whatever conversion another mod adds.
		/// (2) A worthless route: every ingredient slot can be filled by something worth nothing (raw items of value 0, or things made only of them), for an item that also has a route through something
		/// of value. An item with only worthless routes keeps them (a real craft that costs nothing). "Of value" is the least fixpoint seeded by the raw items with a sell value and the loop anchors of the
		/// first solve (<paramref name="anchors"/>), spread through every recipe that has one slot all of whose accepted items are of value.
		/// </summary>
		internal static List<CostRecipe> Prune(List<CostRecipe> recs, Func<int, long> value, HashSet<int> anchors, out int prunedModded, out int prunedWorthless)
		{
			prunedModded = prunedWorthless = 0;
			var baseCrafted = new HashSet<int>();
			foreach (var r in recs) if (!r.Modded) baseCrafted.Add(r.Out);
			var stage1 = new List<CostRecipe>(recs.Count);
			foreach (var r in recs) { if (r.Modded && baseCrafted.Contains(r.Out)) prunedModded++; else stage1.Add(r); }

			var crafted = new HashSet<int>();
			foreach (var r in stage1) crafted.Add(r.Out);
			var worth = new HashSet<int>();
			bool Worth(int t) => worth.Contains(t) || (!crafted.Contains(t) && value(t) > 0);
			if (anchors != null) foreach (int a in anchors) if (value(a) > 0) worth.Add(a);
			bool SlotWorth((int type, int stack, int[] alts) s)
			{
				if (!Worth(s.type)) return false;
				if (s.alts != null) foreach (int a in s.alts) if (!Worth(a)) return false;
				return true;
			}
			bool RecWorth(CostRecipe r) { foreach (var s in r.Ing) if (SlotWorth(s)) return true; return false; }
			for (int pass = 0; pass < 256; pass++)
			{
				bool grew = false;
				foreach (var r in stage1) if (!worth.Contains(r.Out) && RecWorth(r)) { worth.Add(r.Out); grew = true; }
				if (!grew) break;
			}
			var hasWorthRoute = new HashSet<int>();
			foreach (var r in stage1) if (RecWorth(r)) hasWorthRoute.Add(r.Out);
			var kept = new List<CostRecipe>(stage1.Count);
			foreach (var r in stage1) { if (hasWorthRoute.Contains(r.Out) && !RecWorth(r)) prunedWorthless++; else kept.Add(r); }
			return kept;
		}

		/// <summary>The same table with every recipe counted (the solver before <see cref="Prune"/>), for the LEVY_MATCOST_DIFF comparison.</summary>
		internal static Dictionary<int, double> SolveAll(List<CostRecipe> recs, Func<int, long> value) => SolveCore(recs, value, out _, out _);

		/// <summary>LEVY_MATCOST_DIFF=1: what setting recipes aside changed for weapons, in cost and in the "Sell price plus materials" order (the only ranking mode that reads the material cost).</summary>
		private static void LogDiff(List<CostRecipe> recs, log4net.ILog log)
		{
			try
			{
				var old = SolveAll(recs, ItemValue);
				var weapons = new List<int>();
				for (int t = 1; t < ItemLoader.ItemCount; t++)
					if (ContentSamples.ItemsByType.TryGetValue(t, out Item s) && s != null && s.damage > 0 && !s.accessory && s.ammo == 0 && s.useStyle != 0 && s.pick == 0 && s.axe == 0 && s.hammer == 0) weapons.Add(t);
				int changed = 0; var rows = new List<string>();
				foreach (int t in weapons)
				{
					double a = old.TryGetValue(t, out double x) ? x : 0d, b = Of(t);
					if (Math.Abs(a - b) <= 1e-6 * Math.Max(1d, a)) continue;
					changed++;
					if (rows.Count < 40) rows.Add($"{Lang.GetItemNameValue(t)} ({(t < ItemID.Count ? "vanilla" : ItemLoader.GetItem(t)?.Mod.Name)}) {a:F0} -> {b:F0}");
				}
				double Key(int t, Dictionary<int, double> c) => ItemValue(t) + (c == null ? Of(t) : (c.TryGetValue(t, out double v) ? v : 0d));
				var oldOrder = weapons.OrderByDescending(t => Key(t, old)).ThenBy(t => t).ToList();
				var newOrder = weapons.OrderByDescending(t => Key(t, null)).ThenBy(t => t).ToList();
				var oldPos = new Dictionary<int, int>(); for (int i = 0; i < oldOrder.Count; i++) oldPos[oldOrder[i]] = i;
				int moved = 0, maxMove = 0; string maxName = "";
				for (int i = 0; i < newOrder.Count; i++) { int d = Math.Abs(oldPos[newOrder[i]] - i); if (d > 0) moved++; if (d > maxMove) { maxMove = d; maxName = Lang.GetItemNameValue(newOrder[i]); } }
				int ironBefore = old.TryGetValue(ItemID.IronBar, out double ib) ? (int)ib : 0;
				log?.Info($"Levy material cost diff: {weapons.Count} weapons, {changed} change cost, {moved} change place in the sell-price-plus-materials order (largest move {maxMove}: {maxName}); Iron Bar {ironBefore} -> {Of(ItemID.IronBar):F0}");
				foreach (string r in rows) log?.Info("Levy material cost diff:   " + r);
			}
			catch (Exception e) { log?.Warn("Levy material cost diff threw " + e.GetType().Name + ": " + e.Message); }
		}

		private static Dictionary<int, double> SolveCore(List<CostRecipe> recs, Func<int, long> value, out int passes, out HashSet<int> anchors)
		{
			var cost = new Dictionary<int, double>();
			var crafted = new HashSet<int>();
			var fixedCost = new HashSet<int>(); // anchors: their cost is their own value and no recipe lowers it
			foreach (var r in recs) crafted.Add(r.Out);
			passes = 0;
			for (int layer = 0; layer < 32; layer++)
			{
				for (int pass = 0; pass < MaxPasses; pass++)
				{
					bool changed = false; passes++;
					foreach (var r in recs)
					{
						if (fixedCost.Contains(r.Out)) continue;
						double sum = 0d; bool ok = true;
						foreach (var (type, stack, alts) in r.Ing)
						{
							double best = IngredientCost(type, cost, crafted, value);
							if (alts != null) foreach (int a in alts) best = Math.Min(best, IngredientCost(a, cost, crafted, value));
							if (double.IsPositiveInfinity(best)) { ok = false; break; }
							sum += best * stack;
						}
						if (!ok) continue;
						double v = sum / r.OutStack;
						if (!cost.TryGetValue(r.Out, out double cur) || v < cur * (1d - 1e-4)) { cost[r.Out] = v; changed = true; }
					}
					if (!changed) break;
				}
				var waiting = new List<int>();
				foreach (int t in crafted) if (!cost.ContainsKey(t)) waiting.Add(t);
				if (waiting.Count == 0) break;
				var sources = LoopSources(waiting, recs);
				if (sources.Count == 0) { foreach (int t in waiting) { cost[t] = Math.Max(0L, value(t)); fixedCost.Add(t); } continue; } // nothing left to anchor but plain gaps: their own value
				foreach (var comp in sources)
				{
					int anchor = comp[0]; long av = value(anchor);
					foreach (int t in comp) { long v = value(t); if (v > av || (v == av && t < anchor)) { anchor = t; av = v; } }
					cost[anchor] = Math.Max(0L, av); fixedCost.Add(anchor);
				}
			}
			// an item that costs nothing to make is not worth listing: its material cost is the 0 a lookup gives
			foreach (int t in crafted) if (cost.TryGetValue(t, out double c) && c <= 0d) cost.Remove(t);
			anchors = fixedCost;
			return cost;
		}

		/// <summary>The recipe loops among the items still without a cost that wait on nothing outside themselves (strongly connected components, Tarjan), each as its member list.</summary>
		private static List<List<int>> LoopSources(List<int> waiting, List<CostRecipe> recs)
		{
			var set = new HashSet<int>(waiting);
			var edges = new Dictionary<int, List<int>>();
			foreach (var r in recs)
			{
				if (!set.Contains(r.Out)) continue;
				if (!edges.TryGetValue(r.Out, out var l)) edges[r.Out] = l = new List<int>();
				foreach (var (type, _, alts) in r.Ing)
				{
					if (set.Contains(type)) l.Add(type);
					if (alts != null) foreach (int a in alts) if (set.Contains(a)) l.Add(a);
				}
			}
			var index = new Dictionary<int, int>(); var low = new Dictionary<int, int>(); var onStack = new HashSet<int>(); var stack = new Stack<int>();
			var comps = new List<List<int>>(); int counter = 0;
			void Visit(int v)
			{
				index[v] = low[v] = counter++; stack.Push(v); onStack.Add(v);
				if (edges.TryGetValue(v, out var outs))
					foreach (int w in outs)
					{
						if (!index.ContainsKey(w)) { Visit(w); low[v] = Math.Min(low[v], low[w]); }
						else if (onStack.Contains(w)) low[v] = Math.Min(low[v], index[w]);
					}
				if (low[v] != index[v]) return;
				var comp = new List<int>(); int x;
				do { x = stack.Pop(); onStack.Remove(x); comp.Add(x); } while (x != v);
				comps.Add(comp);
			}
			foreach (int t in waiting) if (!index.ContainsKey(t)) Visit(t);
			var res = new List<List<int>>();
			foreach (var comp in comps)
			{
				var members = new HashSet<int>(comp);
				bool loop = comp.Count > 1, leaves = false;
				foreach (int u in comp)
					if (edges.TryGetValue(u, out var outs))
						foreach (int w in outs) { if (!members.Contains(w)) leaves = true; else if (w == u) loop = true; }
				if (loop && !leaves) res.Add(comp);
			}
			return res;
		}

		private static double IngredientCost(int type, Dictionary<int, double> cost, HashSet<int> crafted, Func<int, long> value)
		{
			if (cost.TryGetValue(type, out double c)) return c;
			return crafted.Contains(type) ? double.PositiveInfinity : value(type); // an unknown ingredient (nothing crafts it) is worth its own value
		}
	}
}
