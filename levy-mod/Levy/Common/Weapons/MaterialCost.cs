using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>One recipe as the cost solver sees it: what it makes, and for each ingredient its stack and the other items the recipe accepts in its place (recipe groups).</summary>
	internal sealed class CostRecipe
	{
		public int Out, OutStack = 1;
		public List<(int type, int stack, int[] alts)> Ing = new();
	}

	/// <summary>
	/// The total material cost of every craftable item, in copper: the cheapest recipe's ingredients, each expanded to what makes it, down to items nothing crafts (those count as their own sell value).
	/// A recipe group's ingredient counts as its cheapest member. The solver is a fixpoint over all recipes, so recipe loops (a block that makes walls and walls that make the block, Hellstone and Hellstone
	/// Wall) cannot poison anything: a cost only ever comes from a derivation that bottoms out in raw items, and a loop with no way in (no raw item reaches it) falls back to each member's own value.
	/// Computed once, after all recipes are final (<see cref="PostSetupRecipes"/>), on a server or single-player world; a multiplayer client does not rank weapons.
	/// </summary>
	public class MaterialCost : ModSystem
	{
		private static Dictionary<int, double> _cost = new();
		/// <summary>Number of recipes read and the passes the fixpoint took (diagnostics).</summary>
		internal static int RecipesRead, Passes; internal static double SolveMs;
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
				RecipesRead = recs.Count; SolveMs = sw.Elapsed.TotalMilliseconds;
				log?.Info($"Levy material cost: {_cost.Count} craftable items from {RecipesRead} recipes in {Passes} passes, {SolveMs:F0} ms");
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
				var cr = new CostRecipe { Out = r.createItem.type, OutStack = Math.Max(1, r.createItem.stack) };
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
