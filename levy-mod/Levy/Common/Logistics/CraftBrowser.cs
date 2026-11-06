using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>The commander's own inventory as a stock: the main inventory slots, not coins or ammo. On a server every change is sent to that player slot by slot.</summary>
	public sealed class PlayerStock : IStock
	{
		public const int Slots = 50;
		public readonly Player P;
		public PlayerStock(Player p) { P = p; }
		public string Name => "inventory";
		public long Count(int type) { long n = 0; for (int i = 0; i < Slots; i++) { var it = P.inventory[i]; if (it != null && !it.IsAir && it.type == type) n += it.stack; } return n; }
		public long Take(int type, long n)
		{
			long taken = 0;
			for (int i = 0; i < Slots && taken < n; i++)
			{
				var it = P.inventory[i]; if (it == null || it.IsAir || it.type != type) continue;
				int k = (int)Math.Min(it.stack, n - taken); it.stack -= k; taken += k;
				if (it.stack <= 0) it.TurnToAir();
				Sync(i);
			}
			return taken;
		}
		public Item Put(Item item)
		{
			if (item == null || item.IsAir) return item;
			for (int i = 0; i < Slots && !item.IsAir; i++)
			{
				var s = P.inventory[i];
				if (s == null || s.IsAir || s.type != item.type || s.prefix != item.prefix || s.stack >= s.maxStack) continue;
				int k = Math.Min(item.stack, s.maxStack - s.stack); s.stack += k; item.stack -= k; Sync(i);
			}
			for (int i = 0; i < Slots && !item.IsAir; i++)
				if (P.inventory[i] == null || P.inventory[i].IsAir) { P.inventory[i] = item.Clone(); item.TurnToAir(); Sync(i); }
			return item.IsAir ? null : item;
		}
		private void Sync(int slot) { if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null, P.whoAmI, slot, P.inventory[slot].prefix); }
	}

	/// <summary>One row of the recipe browser.</summary>
	public sealed class RecipeEntry
	{
		public Recipe R; public string Name = ""; public long Crafts; public bool AnyMaterial; public string Blocker = "";
		public bool CanCraft => Crafts > 0 && Blocker == "";
	}

	public enum RecipeView : byte { CanCraft, HasMaterials, All }

	/// <summary>
	/// The Orders page's recipe browser and its instant craft. Sources, in the order a craft pulls from them: the commander's inventory, the armory, the armories pooled with it by pylon, chests within 25 tiles, then the Magic Storage network.
	/// The result goes to the commander's inventory first and overflows to the armory. Stations must stand within 30 tiles of the armory, as for orders; ingredient matching, recipe groups and consumption are the order runner's own (<see cref="Crafting"/>).
	/// </summary>
	public static class CraftBrowser
	{
		/// <summary>Bumped by every instant craft; the screen recounts when it changes.</summary>
		public static int Version;

		public static StockPool PoolFor(ArmoryTE a, Player commander) => new StockPool(a, true, true, commander);

		/// <summary>Whole crafts the stock allows from the recipe's own ingredients (a recipe group adds its members up). Intermediate crafting is not counted here; see <see cref="MaxCrafts"/>.</summary>
		public static long DirectCrafts(Recipe r, CountCache counts, out bool anyMaterial)
		{
			anyMaterial = false; long best = long.MaxValue; bool any = false;
			foreach (var ing in Crafting.Ingredients(r))
			{
				any = true; long have = 0; foreach (int t in ing.Types) have += Math.Max(0, counts.Get(t));
				if (have > 0) anyMaterial = true;
				best = Math.Min(best, have / Math.Max(1, ing.Count));
			}
			return any ? best : 0;
		}

		/// <summary>Every recipe with a result, for the browser: counts, whether any ingredient is in stock, and what blocks it (station or condition). Call when the page opens, after a craft, and on a slow timer, never per frame.</summary>
		public static List<RecipeEntry> Scan(ArmoryTE a, StockPool pool)
		{
			var counts = new CountCache(pool.Count); var res = new List<RecipeEntry>();
			foreach (var r in Main.recipe)
			{
				if (r == null || r.Disabled || r.createItem == null || r.createItem.IsAir) continue;
				long c = DirectCrafts(r, counts, out bool any);
				res.Add(new RecipeEntry { R = r, Name = Terraria.Lang.GetItemNameValue(r.createItem.type), Crafts = c, AnyMaterial = any, Blocker = Crafting.Blocker(r, a) });
			}
			return res;
		}

		public static bool InView(RecipeEntry e, RecipeView v) => v switch { RecipeView.CanCraft => e.CanCraft, RecipeView.HasMaterials => e.AnyMaterial, _ => true };

		/// <summary>The most crafts of <paramref name="r"/> the stock allows up to <paramref name="cap"/>, crafting missing intermediates (a bar from ore) the way an order does. 0 when a station or condition blocks it.</summary>
		public static int MaxCrafts(Recipe r, ArmoryTE a, StockPool pool, int cap)
		{
			bool Ok(int n) { var plan = new Crafting.Plan(); Crafting.PlanRecipe(r, n, a, new CountCache(pool.Count), plan); return plan.Feasible; }
			if (cap <= 0 || !Ok(1)) return 0;
			int lo = 1, hi = cap;
			while (lo < hi) { int mid = lo + (hi - lo + 1) / 2; if (Ok(mid)) lo = mid; else hi = mid - 1; }
			return lo;
		}

		/// <summary>Crafts up to <paramref name="crafts"/> of the recipe now from the pool. Returns the items of the recipe's result that were made. Server and single player.</summary>
		public static int Craft(Recipe r, int crafts, ArmoryTE a, StockPool pool)
		{
			int n = MaxCrafts(r, a, pool, Math.Clamp(crafts, 0, LogisticsConstants.MaxTotal)); if (n <= 0) return 0;
			var plan = new Crafting.Plan(); Crafting.PlanRecipe(r, n, a, new CountCache(pool.Count), plan);
			int made = 0;
			foreach (var step in plan.Steps)
			{
				int m = Crafting.Execute(step, pool, null);
				if (ReferenceEquals(step.R, r)) made += m;
				if (m < step.Times * Math.Max(1, step.R.createItem.stack)) break;
			}
			return made;
		}
	}
}
