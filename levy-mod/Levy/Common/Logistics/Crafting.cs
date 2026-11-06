using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>One ingredient of a recipe: any of <see cref="Types"/> (a recipe group has several), <see cref="Count"/> per craft.</summary>
	public readonly struct Ingredient
	{
		public readonly int[] Types; public readonly int Count;
		/// <summary>The item the recipe itself names (for a group, the one it lists; the checklist and a shop purchase use it).</summary>
		public readonly int Primary;
		public Ingredient(int[] types, int count, int primary) { Types = types; Count = count; Primary = primary; }
	}

	/// <summary>A crafting step the planner found: make <see cref="Times"/> crafts of <see cref="R"/>.</summary>
	public readonly struct CraftStep
	{
		public readonly Recipe R; public readonly int Times;
		public CraftStep(Recipe r, int times) { R = r; Times = times; }
	}

	/// <summary>Counts as the planner sees them: read lazily from the stock pool, then adjusted as the plan "spends" them. One per order run.</summary>
	public sealed class CountCache
	{
		private readonly Func<int, long> _read;
		private readonly Dictionary<int, long> _c = new();
		public CountCache(Func<int, long> read) { _read = read; }
		public long Get(int type) { if (!_c.TryGetValue(type, out long n)) _c[type] = n = _read(type); return n; }
		public void Add(int type, long n) { _c[type] = Get(type) + n; }
		public CountCache Fork() { var f = new CountCache(_read); foreach (var kv in _c) f._c[kv.Key] = kv.Value; return f; }
	}

	/// <summary>
	/// What an order does to its materials. THE RULE (stated once, shown in the manual): an order makes an item only when (1) every ingredient is in stock, counting what can itself be crafted from stock
	/// (a bar from ore, down to <see cref="LogisticsConstants.MaxDepth"/> levels), (2) every crafting station the recipe names stands within <see cref="LogisticsConstants.StationRadiusTiles"/> tiles of the armory,
	/// or is supplied by the linked Magic Storage network, and (3) the recipe has no other condition (water, biome, shimmer) unless the commander in single player meets it. Stock means the armory, the armories pooled with it by pylon,
	/// chests near it, and the Magic Storage network when an Armory Heart links one. An order that cannot finish waits and makes more whenever the missing material arrives.
	/// </summary>
	public static class Crafting
	{
		private static Dictionary<int, List<Recipe>> _byOutput;

		public static void ResetCache() => _byOutput = null;

		public static List<Recipe> RecipesFor(int type)
		{
			if (_byOutput == null)
			{
				var d = new Dictionary<int, List<Recipe>>();
				foreach (var r in Main.recipe)
				{
					if (r == null || r.Disabled || r.createItem == null || r.createItem.IsAir) continue;
					if (!d.TryGetValue(r.createItem.type, out var l)) d[r.createItem.type] = l = new List<Recipe>();
					l.Add(r);
				}
				_byOutput = d;
			}
			return _byOutput.TryGetValue(type, out var res) ? res : _empty;
		}
		private static readonly List<Recipe> _empty = new();

		public static Ingredient[] Ingredients(Recipe r)
		{
			var res = new List<Ingredient>();
			foreach (var it in r.requiredItem)
			{
				if (it == null || it.IsAir) continue;
				int[] types = { it.type };
				foreach (int g in r.acceptedGroups)
					if (RecipeGroup.recipeGroups.TryGetValue(g, out var grp) && grp.ContainsItem(it.type)) { types = grp.ValidItems.ToArray(); break; }
				res.Add(new Ingredient(types, it.stack, it.type));
			}
			return res.ToArray();
		}

		// ---- stations -------------------------------------------------------------------------------------------------------------------

		private static readonly HashSet<int> _nearby = new();
		private static int _stationArmory = -1; private static uint _stationStamp;

		/// <summary>Tile types of the crafting stations standing within reach of the armory (cached for a second).</summary>
		/// <summary>Self-check hook: when set, these are the stations "near" every armory (an armory that is not placed in a world has nothing to scan).</summary>
		internal static HashSet<int> StationOverride;

		public static HashSet<int> StationsNear(ArmoryTE a)
		{
			if (StationOverride != null) return StationOverride;
			uint now = Main.GameUpdateCount;
			if (_stationArmory == a.ID && now - _stationStamp < 60) return _nearby;
			_stationArmory = a.ID; _stationStamp = now; _nearby.Clear();
			int r = LogisticsConstants.StationRadiusTiles, cx = a.Position.X, cy = a.Position.Y;
			for (int x = cx - r; x <= cx + r; x++)
				for (int y = cy - r; y <= cy + r; y++)
				{
					if (!WorldGen.InWorld(x, y, 2)) continue;
					var t = Main.tile[x, y]; if (!t.HasTile) continue;
					AddWithEquivalents(_nearby, t.TileType);
				}
			return _nearby;
		}

		/// <summary>Vanilla counts a better station as the lesser ones (a Mythril Anvil is an Anvil, an Adamantite Forge a Furnace and a Hellforge); a mod tile names its own.</summary>
		public static void AddWithEquivalents(HashSet<int> set, int tile)
		{
			if (!set.Add(tile)) { /* still add equivalents below */ }
			switch (tile)
			{
				case TileID.MythrilAnvil: set.Add(TileID.Anvils); break;
				case TileID.AdamantiteForge: case TileID.Hellforge: set.Add(TileID.Furnaces); if (tile == TileID.AdamantiteForge) set.Add(TileID.Hellforge); break;
				case TileID.Bottles: case TileID.AlchemyTable: set.Add(TileID.Bottles); break;
			}
			try { foreach (int t in TileLoader.GetTile(tile)?.AdjTiles ?? Array.Empty<int>()) set.Add(t); } catch { }
		}

		/// <summary>Why a recipe cannot be made at this armory right now ("" = it can): a missing station or an unmet condition.</summary>
		public static string Blocker(Recipe r, ArmoryTE a)
		{
			var near = a == null ? null : StationsNear(a);
			foreach (int t in r.requiredTile)
			{
				if (t < 0) continue;
				if (near != null && near.Contains(t)) continue;
				if (a != null && NetworkStocks.HasStation != null && NetworkStocks.HasStation(a, t)) continue;
				return "station:" + t;
			}
			if (r.Conditions != null && r.Conditions.Count > 0)
			{
				if (Main.netMode != NetmodeID.SinglePlayer) return "condition:" + r.Conditions[0].Description;
				foreach (var c in r.Conditions) if (!c.IsMet()) return "condition:" + c.Description;
			}
			return "";
		}

		// ---- planning -------------------------------------------------------------------------------------------------------------------

		/// <summary>The result of planning some crafts: steps in the order to run them, raw materials needed (and which of those are missing), and any station or condition blocking.</summary>
		public sealed class Plan
		{
			public readonly List<CraftStep> Steps = new();
			public readonly Dictionary<int, long> Need = new();
			public readonly Dictionary<int, long> Missing = new();
			public readonly HashSet<string> Blocked = new();
			public bool Feasible => Missing.Count == 0 && Blocked.Count == 0 && NoRecipe.Count == 0;
			public readonly HashSet<int> NoRecipe = new();
			internal void AddNeed(int type, long n) { Need.TryGetValue(type, out long c); Need[type] = c + n; }
			internal void AddMissing(int type, long n) { Missing.TryGetValue(type, out long c); Missing[type] = c + n; }
		}

		/// <summary>Plans <paramref name="items"/> units of <paramref name="type"/> made by crafting (never taken from stock). Spends from <paramref name="counts"/> as it plans.</summary>
		public static Plan PlanMake(int type, int items, ArmoryTE a, CountCache counts, Plan into = null)
		{
			var p = into ?? new Plan();
			var cands = RecipesFor(type);
			if (cands.Count == 0) { p.NoRecipe.Add(type); p.AddMissing(type, items); p.AddNeed(type, items); return p; }
			Plan best = null;
			// try each recipe on a scratch copy and keep the first that is feasible, else the first one (its shortfalls are what the checklist reports)
			for (int i = 0; i < cands.Count; i++)
			{
				var scratch = new Plan(); var cc = counts.Fork();
				MakeVia(cands[i], items, a, cc, scratch, 0);
				if (best == null) best = scratch;
				if (scratch.Feasible) { best = scratch; break; }
			}
			Merge(p, best);
			// charge the real counts for the winner by replaying it on them
			foreach (var s in best.Steps) { SpendIngredients(s.R, s.Times, counts); counts.Add(s.R.createItem.type, (long)s.R.createItem.stack * s.Times); }
			return p;
		}

		/// <summary>Plans <paramref name="crafts"/> crafts of one specific recipe (the browser's recipe, not "any recipe for this item"), crafting missing intermediates from stock. Spends from <paramref name="counts"/>.</summary>
		public static Plan PlanRecipe(Recipe r, int crafts, ArmoryTE a, CountCache counts, Plan into = null)
		{
			var p = into ?? new Plan();
			MakeVia(r, Math.Max(1, crafts) * Math.Max(1, r.createItem.stack), a, counts, p, 0);
			return p;
		}

		private static void Merge(Plan into, Plan from)
		{
			into.Steps.AddRange(from.Steps);
			foreach (var kv in from.Need) into.AddNeed(kv.Key, kv.Value);
			foreach (var kv in from.Missing) into.AddMissing(kv.Key, kv.Value);
			foreach (var b in from.Blocked) into.Blocked.Add(b);
			foreach (var n in from.NoRecipe) into.NoRecipe.Add(n);
		}

		private static void SpendIngredients(Recipe r, int times, CountCache c)
		{
			foreach (var ing in Ingredients(r))
			{
				long need = (long)ing.Count * times;
				foreach (int t in ing.Types.OrderByDescending(x => c.Get(x))) { long k = Math.Min(need, Math.Max(0, c.Get(t))); c.Add(t, -k); need -= k; if (need <= 0) break; }
			}
		}

		private static void MakeVia(Recipe r, int items, ArmoryTE a, CountCache c, Plan p, int depth)
		{
			int stack = Math.Max(1, r.createItem.stack);
			int times = (items + stack - 1) / stack;
			string block = Blocker(r, a);
			if (block != "") p.Blocked.Add(block);
			foreach (var ing in Ingredients(r))
			{
				long need = (long)ing.Count * times;
				// stock first, richest alternative first
				foreach (int t in ing.Types.OrderByDescending(x => c.Get(x)))
				{
					long have = Math.Max(0, c.Get(t)); long take = Math.Min(have, need);
					if (take > 0) { c.Add(t, -take); p.AddNeed(t, take); need -= take; }
					if (need <= 0) break;
				}
				if (need <= 0) continue;
				int primary = ing.Primary;
				// a shortfall: craft it from what is in stock when a recipe exists, the depth allows and the whole shortfall can be made; otherwise the item itself is what is missing
				bool crafted = false;
				if (depth + 1 < LogisticsConstants.MaxDepth && !IsCyclic(primary, r))
				{
					foreach (var cand in RecipesFor(primary))
					{
						var sub = new Plan(); var cc = c.Fork();
						MakeVia(cand, (int)Math.Min(int.MaxValue, need), a, cc, sub, depth + 1);
						if (!sub.Feasible) continue;
						Merge(p, sub);
						foreach (var s in sub.Steps) { SpendIngredients(s.R, s.Times, c); c.Add(s.R.createItem.type, (long)s.R.createItem.stack * s.Times); }
						c.Add(primary, -need);
						crafted = true; break;
					}
				}
				if (!crafted) { p.AddNeed(primary, need); p.AddMissing(primary, need); }
			}
			p.Steps.Add(new CraftStep(r, times));
		}

		private static bool IsCyclic(int ingredient, Recipe parent) => parent.createItem.type == ingredient;

		// ---- execution ------------------------------------------------------------------------------------------------------------------

		/// <summary>Runs one planned step against the real stock: consumes the ingredients, puts the product in the armory. Returns the items made (0 if the stock turned out short).</summary>
		public static int Execute(CraftStep s, StockPool pool, CountCache counts)
		{
			int made = 0;
			var ings = Ingredients(s.R);
			for (int k = 0; k < s.Times; k++)
			{
				foreach (var ing in ings) if (pool.Count(ing.Types) < ing.Count) return made;
				foreach (var ing in ings) { long took = ing.Types.Length == 1 ? pool.Take(ing.Types[0], ing.Count) : pool.TakeAny(ing.Types, ing.Count); if (took < ing.Count) return made; }
				var o = new Item(); o.SetDefaults(s.R.createItem.type); o.stack = s.R.createItem.stack;
				if (o.maxStack == 1 && (o.damage > 0 || o.accessory || o.defense > 0)) o.Prefix(-1);
				var rest = pool.PutOutput(o);
				if (rest != null && !rest.IsAir) { Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("Levy.Logistics"), pool.Armory.Center, rest); }
				made += s.R.createItem.stack;
			}
			return made;
		}
	}
}
