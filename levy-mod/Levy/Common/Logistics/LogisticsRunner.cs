using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Net;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// Runs every order on the server (and in single player), a little each pass: standing buys, shortfall buys, then crafts. An order that cannot make progress waits and tries again on the next pass, so it finishes by itself
	/// when the missing material shows up: with 100 of 120 bars it makes what 100 bars allow, and the rest when the other 20 arrive.
	/// </summary>
	public class LogisticsRunner : ModSystem
	{
		public override void Load()
		{
			LevyPackets.Register(LevyPacket.LogisticsOrderEdit, LogisticsActions.OnOrderEdit);
			LevyPackets.Register(LevyPacket.LogisticsSell, LogisticsActions.OnSell);
			LevyPackets.Register(LevyPacket.LogisticsBuy, LogisticsActions.OnBuy);
			LevyPackets.Register(LevyPacket.LogisticsSnapshot, (r, who) => { if (Main.netMode == NetmodeID.MultiplayerClient) OrderBook.ReadBook(r); });
		}

		public override void PostSetupContent() => Crafting.ResetCache();
		public override void Unload() => Crafting.ResetCache();

		public override void PostUpdateWorld()
		{
			if (!LevyUtil.IsServerOrSingle || OrderBook.Orders.Count == 0) return;
			if (Main.GameUpdateCount % (uint)LogisticsConstants.RunnerTicks != 0) return;
			bool changed = false;
			foreach (var o in OrderBook.Orders.ToList())
			{
				try { changed |= Run(o); }
				catch (Exception e) { o.Status = "Error"; Mod.Logger.Warn("Levy/Logistics: order " + o.Id + " failed: " + e.GetType().Name + ": " + e.Message); }
			}
			if (changed) { OrderBook.Touch(); OrderBook.Broadcast(); }
		}

		/// <summary>One pass over one order. Returns true when anything the client shows changed.</summary>
		public static bool Run(LogisticsOrder o)
		{
			string before = o.Status; int madeBefore = o.Made; int rulesBefore = o.Rules.Sum(r => r.Bought);
			if (!TileEntity.ByID.TryGetValue(o.ArmoryId, out var te) || te is not ArmoryTE a) { o.Status = "NoArmory"; return before != o.Status; }
			var pool = new StockPool(a);
			var counts = new CountCache(pool.Count);

			if (!o.Paused && !o.Complete)
			{
				// 1. standing buys (X guns, X ammo)
				if (o.Rules.Any(r => !r.Finished))
				{
					var offers = Shopping.AllOffers(a);
					foreach (var r in o.Rules) Shopping.RunRule(a, r, offers);
				}
				// 2. shortfall buys: what a shopkeeper sells of the materials the whole order still needs
				if (o.BuyShortfalls && o.ShortfallSpent < o.ShortfallBudget)
				{
					var plan = PlanRemaining(o, a, new CountCache(pool.Count));
					if (plan.Missing.Count > 0)
					{
						var offers = Shopping.AllOffers(a);
						Shopping.BuyShortfalls(a, o, plan.Missing, offers);
					}
				}
				// 3. crafts
				CraftSome(o, a, pool, counts);
			}

			// checklist and status, always fresh for the screen
			var all = PlanRemaining(o, a, new CountCache(pool.Count));
			o.Checklist = ChecklistOf(all, pool);
			o.Status = StatusOf(o, all);
			return before != o.Status || o.Made != madeBefore || o.Rules.Sum(r => r.Bought) != rulesBefore ;
		}

		/// <summary>One plan over everything the order still owes, sharing one stock so shared ingredients are counted once.</summary>
		public static Crafting.Plan PlanRemaining(LogisticsOrder o, ArmoryTE a, CountCache counts)
		{
			var plan = new Crafting.Plan();
			foreach (var l in o.Lines) if (l.Left > 0) foreach (int t in l.Types) Crafting.PlanMake(t, l.Left, a, counts, plan);
			return plan;
		}

		/// <summary>The materials checklist: every raw material the remaining order needs, what stock holds, what is missing. Crafted intermediates do not appear; their ingredients do.</summary>
		public static List<ChecklistRow> ChecklistOf(Crafting.Plan plan, StockPool pool)
		{
			var rows = new List<ChecklistRow>();
			foreach (var kv in plan.Need.OrderBy(k => k.Key))
				rows.Add(new ChecklistRow { Type = kv.Key, Need = kv.Value, Have = pool.Count(kv.Key) });
			return rows;
		}

		private static string StatusOf(LogisticsOrder o, Crafting.Plan all)
		{
			if (o.Paused) return "Paused";
			if (o.Complete) return "Done";
			if (all.Blocked.Count > 0) return "Blocked:" + all.Blocked.First();
			if (all.NoRecipe.Count > 0) return "NoRecipe";
			if (all.Missing.Count > 0) return "Waiting";
			return "Working";
		}

		private static void CraftSome(LogisticsOrder o, ArmoryTE a, StockPool pool, CountCache counts)
		{
			int n = o.Lines.Count; var planned = new int[n]; var done = new int[n]; var craftable = new bool[n];
			var rng = new Random(o.Seed ^ (o.Made * 7919));
			for (int guard = 0; guard < LogisticsConstants.CraftsPerRun; guard++)
			{
				for (int i = 0; i < n; i++)
				{
					planned[i] = o.Lines[i].Planned; done[i] = o.Lines[i].Done;
					craftable[i] = o.Lines[i].Left > 0 && LineFeasible(o.Lines[i], a, counts);
				}
				int pick = OrderMath.PickLine(planned, done, craftable, o.Mode == OrderMode.Random, rng.NextDouble());
				if (pick < 0) break;
				var line = o.Lines[pick];
				const int units = 1; // a set is one unit of all its pieces
				var plan = new Crafting.Plan();
				foreach (int t in line.Types) Crafting.PlanMake(t, 1, a, counts, plan);
				if (!plan.Feasible) break;
				int madeTop = 0;
				foreach (var step in plan.Steps)
				{
					int made = Crafting.Execute(step, pool, counts);
					if (step.R.createItem.type == line.Types[0] || line.Types.Contains(step.R.createItem.type)) madeTop += made;
					if (made < step.Times * Math.Max(1, step.R.createItem.stack)) { counts = new CountCache(pool.Count); goto next; }
				}
				line.Done += line.Types.Length == 1 ? Math.Max(units, madeTop) : units;
			next:;
			}
		}

		private static bool LineFeasible(OrderLine l, ArmoryTE a, CountCache counts)
		{
			var fork = counts.Fork(); var plan = new Crafting.Plan();
			foreach (int t in l.Types) Crafting.PlanMake(t, 1, a, fork, plan);
			return plan.Feasible;
		}
	}
}
