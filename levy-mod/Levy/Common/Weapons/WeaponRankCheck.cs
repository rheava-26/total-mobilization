using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the weapon ranking (<see cref="WeaponRank"/>, <see cref="MaterialCost"/>, the sword-shot credit), in the style of <see cref="WeaponAllocatorCheck"/>: throwaway armories and soldiers,
	/// no world state, never throws. Logs one summary line, or every failure.
	/// </summary>
	public class WeaponRankCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		public override void PostSetupRecipes()
		{
			MaterialCost.Ensure();
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}
			Guard("cost solver", () => CheckSolver(Check));
			Guard("real recipes", () => CheckRealRecipes(Check));
			Guard("sword shots", () => CheckSwordShots(Check));
			Guard("armor skip", () => CheckArmorSkip(Check));
			foreach (var (mode, skip) in new[] { (WeaponRankMode.SellPrice, true), (WeaponRankMode.SellPrice, false), (WeaponRankMode.SellPricePlusMaterials, true), (WeaponRankMode.SellPricePlusMaterials, false), (WeaponRankMode.Damage, false) })
			{
				WeaponRank.Force(mode, skip);
				try { Guard($"mode consistency {mode}/skip={skip}", () => CheckConsistency(Check, mode)); }
				finally { WeaponRank.Release(); }
			}
			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy weapon ranking self-check: {ok} checks OK (material cost: {MaterialCost.RecipesRead} recipes, {MaterialCost.Passes} passes, {MaterialCost.SolveMs:F0} ms)");
				else
				{
					Mod.Logger.Warn($"Levy weapon ranking self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (var f in failures) Mod.Logger.Warn("Levy weapon ranking self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		// ------------------------------------------------------------------ the cost solver, on made-up recipes

		private static CostRecipe R(int outType, int outStack, params (int type, int stack)[] ing)
		{
			var r = new CostRecipe { Out = outType, OutStack = outStack };
			foreach (var (t, s) in ing) r.Ing.Add((t, s, null));
			return r;
		}

		private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6 * Math.Max(1d, Math.Abs(b));

		private static void CheckSolver(Action<bool, string> check)
		{
			// items: 1 raw ore (100), 2 raw gel (40); 10 bar = 1 ore; 11 brick = 1 bar; 12 wall = 1 brick -> 4 walls; 13 = 4 walls -> 1 brick (the Hellstone <-> Hellstone Wall loop, a second recipe for the brick)
			// 20 and 21 make each other with no raw way in (value 70 / 90); 22 = 2 x item 20. 30 = 1 unknown item 99 (value 40, nothing crafts it). 40 = 5 bars (500) or 1 gel (40). 50 = any of {10, 2}.
			// 60 and 61 turn one into two of the other (a loop that multiplies material), fed by 1 ore.
			var values = new Dictionary<int, long> { [1] = 100, [2] = 40, [10] = 3000, [11] = 5000, [12] = 1000, [20] = 70, [21] = 90, [22] = 1, [30] = 1, [99] = 40, [40] = 1, [50] = 1, [60] = 500, [61] = 500, [70] = 999 };
			var recs = new List<CostRecipe>
			{
				R(10, 1, (1, 1)), R(11, 1, (10, 1)), R(12, 4, (11, 1)), R(11, 1, (12, 4)),
				R(20, 1, (21, 1)), R(21, 1, (20, 1)), R(22, 1, (20, 2)),
				R(30, 1, (99, 1)),
				R(40, 1, (10, 5)), R(40, 1, (2, 1)),
				R(60, 2, (61, 1)), R(61, 2, (60, 1)), R(60, 1, (1, 1)),
				R(70, 1, (888, 1)), // an ingredient whose value is unknown (0) and that nothing crafts: no cost at all
			};
			// 80 (value 1250) makes 4 x item 81 (value 0), and 4 x item 81 make 80: the Hellstone / Ember Wall loop. 82 = 2 x item 80 + 1 x item 83 (83 = 5 x item 84, a Stone-like loop at value 0): needs both anchors.
			recs.Add(R(81, 4, (80, 1))); recs.Add(R(80, 1, (81, 4))); recs.Add(R(84, 1, (85, 1))); recs.Add(R(85, 1, (84, 1))); recs.Add(R(83, 5, (84, 1))); recs.Add(R(82, 1, (80, 2), (83, 1)));
			values[80] = 1250; values[81] = 0; values[84] = 0; values[85] = 0; values[83] = 0;
			var alt = new CostRecipe { Out = 50, OutStack = 1 }; alt.Ing.Add((10, 1, new[] { 2 })); recs.Add(alt);
			var cost = MaterialCost.Solve(recs, t => values.TryGetValue(t, out long v) ? v : 0L, out int passes);
			check(Near(cost.GetValueOrDefault(10), 100), "a bar made of one ore costs the ore");
			check(Near(cost.GetValueOrDefault(11), 100), "a brick made of a bar costs the bar, not the loop through its walls");
			check(Near(cost.GetValueOrDefault(12), 25), "a wall made four to a brick costs a quarter of the brick");
			check(cost.ContainsKey(11) && cost[11] <= 4 * cost[12] + 1e-6, "the brick <-> wall loop keeps both costs (brick " + cost.GetValueOrDefault(11) + ", wall " + cost.GetValueOrDefault(12) + ")");
			check(Near(cost.GetValueOrDefault(21), 90) && Near(cost.GetValueOrDefault(20), 90), "a loop with no raw way in is anchored at its most valuable member and the others derive from it");
			check(Near(cost.GetValueOrDefault(22), 180), "an item waiting on that loop resolves from the anchor");
			check(Near(cost.GetValueOrDefault(30), 40), "an ingredient nothing crafts counts as its own value");
			check(Near(cost.GetValueOrDefault(40), 40), "the cheapest of two recipes wins");
			check(Near(cost.GetValueOrDefault(50), 40), "a recipe-group ingredient counts as its cheapest member");
			check(Near(cost.GetValueOrDefault(80), 1250) && Near(cost.GetValueOrDefault(81), 312.5), "the Hellstone / Ember Wall loop: the ore keeps its value and the wall is a quarter of it (" + cost.GetValueOrDefault(80) + ", " + cost.GetValueOrDefault(81) + ")");
			check(Near(cost.GetValueOrDefault(82), 2500), "an item made from the loop and from a worthless loop costs only the valuable part (" + cost.GetValueOrDefault(82) + ")");
			check(!cost.ContainsKey(70) && cost.Count > 0, "an item made only of worthless things costs nothing to make (no entry, a lookup gives 0)");
			check(cost.TryGetValue(60, out double c60) && cost.TryGetValue(61, out double c61) && c60 > 0 && c61 > 0 && !double.IsNaN(c60) && !double.IsInfinity(c60) && passes <= 130, "a loop that multiplies material stays finite and positive and stops (" + passes + " passes)");
			check(Near(cost.GetValueOrDefault(10), 100), "the multiplying loop does not touch the raw chain");
		}

		// ------------------------------------------------------------------ the real recipes

		private static void CheckRealRecipes(Action<bool, string> check)
		{
			check(MaterialCost.Ready, "material costs were computed from the loaded recipes");
			if (!MaterialCost.Ready) return;
			check(MaterialCost.Of(ItemID.Wood) == 0 && MaterialCost.Of(ItemID.IronOre) == 0, "raw items have no material cost");
			double ironBar = MaterialCost.Of(ItemID.IronBar), ironBroadsword = MaterialCost.Of(ItemID.IronBroadsword);
			check(ironBar > 0 && ironBroadsword > 0 && ironBroadsword > ironBar, "an Iron Broadsword costs more than one Iron Bar (" + ironBroadsword + " vs " + ironBar + ")");
			double brick = MaterialCost.Of(ItemID.HellstoneBrick), wall = MaterialCost.Of(ItemID.HellstoneBrickWall), bar = MaterialCost.Of(ItemID.HellstoneBar);
			check(bar > 0 && brick > 0 && wall > 0, "Hellstone Bar, Brick and Brick Wall all have a cost (" + bar + ", " + brick + ", " + wall + ") " + Explain(ItemID.Hellstone) + Explain(ItemID.Obsidian));
			check(wall <= brick / 4d * 1.0001 + 1e-6 || wall <= brick, "a Hellstone Brick Wall costs no more than the brick it is made from (" + wall + " vs " + brick + ")");
			check(MaterialCost.Of(ItemID.TerraBlade) > MaterialCost.Of(ItemID.Muramasa), "a Terra Blade costs more in materials than a Muramasa");
			int bad = 0; for (int t = 1; t < ItemLoader.ItemCount; t++) { double c = MaterialCost.Of(t); if (double.IsNaN(c) || double.IsInfinity(c) || c < 0) bad++; }
			check(bad == 0, "every cost is a finite, non-negative number (" + bad + " are not)");
		}

		private static string Explain(int type)
		{
			var sb = new System.Text.StringBuilder(" {" + Lang.GetItemNameValue(type) + ":");
			for (int i = 0; i < Recipe.numRecipes; i++)
			{
				var r = Main.recipe[i];
				if (r.createItem.type != type) continue;
				sb.Append(" [x" + r.createItem.stack + " <- ");
				foreach (var ing in r.requiredItem) if (!ing.IsAir) sb.Append(ing.stack + "x" + ing.Name + "(v" + ing.value + ",c" + MaterialCost.Of(ing.type).ToString("F0") + (Main.recipe.Take(Recipe.numRecipes).Any(q => q.createItem.type == ing.type) ? ",crafted" : ",raw") + ") ");
				sb.Append("]");
			}
			return sb.Append("}").ToString();
		}

		// ------------------------------------------------------------------ swords that also launch a shot

		private static void CheckSwordShots(Action<bool, string> check)
		{
			var ench = Make(ItemID.EnchantedSword); var star = Make(ItemID.Starfury); var terra = Make(ItemID.TerraBlade); var iron = Make(ItemID.IronBroadsword);
			float e = WeaponClassifier.SwordShotDamage(ench, WeaponClassifier.GetProfile(ench), out _);
			float s = WeaponClassifier.SwordShotDamage(star, WeaponClassifier.GetProfile(star), out float starShot);
			float t = WeaponClassifier.SwordShotDamage(terra, WeaponClassifier.GetProfile(terra), out _);
			float n = WeaponClassifier.SwordShotDamage(iron, WeaponClassifier.GetProfile(iron), out _);
			check(Math.Abs(e - ench.damage) < 0.01f, "Enchanted Sword's beam adds its damage to each swing (" + e + " vs " + ench.damage + ")");
			check(Math.Abs(s - star.damage * 1.5f) < 0.01f && Math.Abs(starShot - star.damage * 1.5f) < 0.01f, "Starfury's falling star adds one and a half times its damage (" + s + " vs " + star.damage * 1.5f + ")");
			check(Math.Abs(t - terra.damage) < 0.01f, "Terra Blade's beam adds its damage to each swing (" + t + " vs " + terra.damage + ")");
			check(n == 0f, "a sword that launches nothing adds nothing");
			check(WeaponClassifier.Score(star) > 0f && WeaponClassifier.PerHitDamage(star) >= star.damage * 1.5f - 0.01f, "Starfury scores and hits above its swing alone");
			check(WeaponClassifier.Score(ench) > WeaponClassifier.Score(Make(ItemID.IronBroadsword)), "Enchanted Sword outscores an Iron Broadsword");
		}

		// ------------------------------------------------------------------ the armored-enemy skip

		private static void CheckArmorSkip(Action<bool, string> check)
		{
			WeaponRank.Force(WeaponRankMode.SellPrice, true);
			int savedStage = WeaponRank.ForceStage;
			try
			{
				var mini = Make(ItemID.Minishark); var terra = Make(ItemID.TerraBlade); var wood = Make(ItemID.WoodenSword);
				WeaponRank.ForceStage = 0;
				check(!WeaponRank.Negligible(mini) && !WeaponRank.Negligible(wood), "at the start of the game neither a Minishark nor a Wooden Sword is negligible");
				var tiny = Make(ItemID.WoodenSword); tiny.damage = 2;
				check(WeaponRank.Negligible(tiny), "a 2-damage weapon is negligible even at the start (the armor swallows the hit)");
				WeaponRank.ForceStage = 3;
				check(WeaponRank.Negligible(mini) && WeaponRank.Negligible(wood), "at Hardmode a Minishark and a Wooden Sword barely hurt armored enemies");
				check(!WeaponRank.Negligible(terra), "a Terra Blade never does");
				check(WeaponRank.Of(mini) < 0.5f && WeaponRank.Of(terra) >= 1f, "a negligible weapon ranks below every real one");
				var zero = Make(ItemID.TerraBlade); zero.damage = 0;
				check(WeaponRank.Negligible(zero) && WeaponRank.Of(zero) < 0.5f, "a weapon that deals no damage is negligible however much it costs");
				float demoted = WeaponRank.Of(wood), higher = WeaponRank.Of(mini);
				check(demoted > 0f && higher > 0f, "negligible weapons keep a positive rank, so a levy with nothing better still gets one");
				WeaponRank.Force(WeaponRankMode.SellPrice, false);
				check(WeaponRank.Of(mini) >= 1f, "with the skip off the Minishark ranks by its price");
				WeaponRank.Force(WeaponRankMode.Damage, true);
				check(WeaponRank.Of(mini) == WeaponClassifier.Score(mini), "Damage mode is the damage score whatever the skip says");
				// a heal weapon ranks by what it heals in Damage mode, by its price in the price modes, and the armor skip never demotes it
				Item healer = null;
				if (ModLoader.TryGetMod("ThoriumMod", out var th))
					foreach (var mi in th.GetContent<ModItem>()) { var h = Make(mi.Type); if (h.value > 0 && WeaponClassifier.IsIssuableWeapon(h) && WeaponClassifier.HealScore(h) > 0f) { healer = h; break; } }
				if (healer != null)
				{
					WeaponRank.ForceStage = 6;
					check(!WeaponRank.Negligible(healer), $"{healer.Name} (a heal weapon) is never negligible");
					WeaponRank.Force(WeaponRankMode.Damage, true);
					check(WeaponRank.Of(healer) == WeaponClassifier.HealScore(healer), $"{healer.Name} ranks by its healing in Damage mode");
					WeaponRank.Force(WeaponRankMode.SellPrice, true);
					check(Math.Abs(WeaponRank.Of(healer) - (float)Math.Pow(Math.Max(1, healer.value), WeaponRank.PriceExponent)) < 0.01f, $"{healer.Name} ranks by its price in the price modes");
				}
			}
			finally { WeaponRank.ForceStage = savedStage; WeaponRank.Release(); }
		}

		// ------------------------------------------------------------------ every pick path uses the same key

		private static ArmoryTE Armory(params Item[] weapons)
		{
			var te = new ArmoryTE();
			for (int i = 0; i < weapons.Length; i++) te.Storage[i] = weapons[i];
			return te;
		}

		private static void CheckConsistency(Action<bool, string> check, WeaponRankMode mode)
		{
			check(ReferenceEquals(WeaponAllocator.RankOf.Method, ((Func<Item, float>)WeaponRank.Of).Method), "the allocator ranks with the shared key");
			// ordered by Score, price and material cost differ: the best blade by the current key is whichever it says
			int[] stockTypes = { ItemID.WoodenSword, ItemID.IronBroadsword, ItemID.Muramasa, ItemID.TerraBlade, ItemID.NightsEdge, ItemID.BladeofGrass, ItemID.FieryGreatsword };
			var stock = stockTypes.Select(Make).ToArray();
			Item best = stock.OrderByDescending(WeaponRank.Of).First();
			string label = $"[{mode}] best by the key = {best.Name} ({WeaponRank.Of(best):F1})";

			// the pure allocator: a lone unclassed levy takes the best
			var levy = new AllocLevy { Id = 0 };
			var levies = new List<AllocLevy> { levy };
			var cands = new List<AllocCand>(); int seq = 0;
			foreach (var it in stock) cands.Add(WeaponAllocator.MakeCand(it, 1, seq++, AllocSlot.Shelf, null, null));
			check(cands.All(c => c.Rank == WeaponRank.Of(c.It)), "every candidate carries the shared rank " + label);
			WeaponAllocator.Plan(levies, cands);
			check(levy.Target?.It.type == best.type, "the allocator hands out the best weapon by the key " + label + ", got " + levy.Target?.It.Name);

			// the armory's varied pick (IssueGear)
			var te = Armory(stockTypes.Select(Make).ToArray());
			var s = new SoldierRecord { Id = 0 };
			ArmoryTE.TestServed = new List<SoldierRecord> { s };
			try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
			check(s.Gear.HasWeapon && s.Gear.Weapon.type == best.type, "the armory issues the best weapon by the key " + label + ", got " + (s.Gear.HasWeapon ? s.Gear.Weapon.Name : "nothing"));

			// the upgrade path: a levy holding the worst blade is moved to the best
			var te2 = Armory(Make(best.type));
			var low = new SoldierRecord { Id = 0 };
			low.Gear.Weapon = Make(ItemID.WoodenSword);
			if (WeaponRank.Of(best) >= WeaponRank.Of(low.Gear.Weapon) * LevyConstants.AutoUpgradeScoreMargin)
			{
				ArmoryTE.TestServed = new List<SoldierRecord> { low };
				try { te2.UpgradeWeapons(low); } finally { ArmoryTE.TestServed = null; }
				check(low.Gear.Weapon.type == best.type, "a levy upgrades to the best weapon by the key " + label + ", got " + low.Gear.Weapon.Name);
			}
			// ... and the margin is on the ranks: a weapon under 1.15x the one in hand is not worth a swap
			Item near = stock.Where(i => i.type != best.type).OrderByDescending(WeaponRank.Of).First();
			if (WeaponRank.Of(best) < WeaponRank.Of(near) * LevyConstants.AutoUpgradeScoreMargin)
			{
				var te3 = Armory(Make(best.type));
				var holder = new SoldierRecord { Id = 0 };
				holder.Gear.Weapon = Make(near.type);
				ArmoryTE.TestServed = new List<SoldierRecord> { holder };
				try { te3.UpgradeWeapons(holder); } finally { ArmoryTE.TestServed = null; }
				check(holder.Gear.Weapon.type == near.type, "a weapon under the swap margin is not taken " + label);
			}

			// sidearms: the best blade at most half the best's rank (the shelf reserve), by the same key
			var te4 = Armory(stockTypes.Select(Make).ToArray());
			var sidearm = new SoldierRecord { Id = 0 };
			Item side = te4.TakeSidearm(sidearm);
			float cap = WeaponRank.Of(best) * LevyConstants.SidearmScoreCapFraction;
			Item expectSide = stock.Where(i => WeaponRank.Of(i) <= cap).OrderByDescending(WeaponRank.Of).FirstOrDefault();
			check((side == null && expectSide == null) || (side != null && expectSide != null && WeaponRank.Of(side) == WeaponRank.Of(expectSide)), "the sidearm is the best blade under half the top rank " + label + ", got " + (side?.Name ?? "nothing") + " expected " + (expectSide?.Name ?? "nothing"));
		}
	}
}
