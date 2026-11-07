using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
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
			Guard("beam melee boost", () => CheckBeamMelee(Check));
			Guard("armor skip", () => CheckArmorSkip(Check));
			Guard("balanced curves", () => Unpenalized(() => CheckBalancedCurves(Check)));
			Guard("balanced picks", () => Unpenalized(() => CheckBalancedRanks(Check)));
			Guard("found boost", () => Unpenalized(() => CheckFoundBoost(Check)));
			Guard("aim accuracy", () => CheckAimAccuracy(Check));
			Guard("star conversion", () => CheckStarMana(Check));
			foreach (var (mode, skip) in new[] { (WeaponRankMode.Balanced, false), (WeaponRankMode.Balanced, true), (WeaponRankMode.SellPrice, true), (WeaponRankMode.SellPrice, false), (WeaponRankMode.SellPricePlusMaterials, true), (WeaponRankMode.SellPricePlusMaterials, false), (WeaponRankMode.Damage, false) })
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

		// ------------------------------------------------------------------ a pure-contact blade ranks below every melee weapon that launches a projectile

		/// <summary>Runs a check that measures the stage / price / found factors on plain blades with the pure-contact factor out of the way (that factor has its own check).</summary>
		private static void Unpenalized(Action a) { WeaponRank.PureContactPenalty = false; try { a(); } finally { WeaponRank.PureContactPenalty = true; } }

		private static void CheckBeamMelee(Action<bool, string> check)
		{
			var capable = new[] { ItemID.EnchantedSword, ItemID.Starfury };
			var plain = new[] { ItemID.PlatinumBroadsword, ItemID.GoldBroadsword, ItemID.IronBroadsword, ItemID.BloodButcherer, ItemID.Muramasa, ItemID.BoneSword, ItemID.CopperShortsword };
			plain = plain.Where(t => WeaponClassifier.GetProfile(Make(t)).IsPureContactBlade).ToArray();
			check(plain.Length >= 4, $"{plain.Length} pure-contact blades found for the check");
			foreach (int c in capable) check(WeaponClassifier.GetProfile(Make(c)).FiresProjectileMelee && !WeaponClassifier.GetProfile(Make(c)).IsPureContactBlade, Lang.GetItemNameValue(c) + " counts as ranged-capable");
			check(!WeaponClassifier.GetProfile(Make(ItemID.WoodYoyo)).IsPureContactBlade && !WeaponClassifier.GetProfile(Make(ItemID.Spear)).IsPureContactBlade && !WeaponClassifier.GetProfile(Make(ItemID.WoodenBoomerang)).IsPureContactBlade, "yoyos, spears and boomerangs are not pure-contact blades");
			foreach (var (mode, skip) in new[] { (WeaponRankMode.Balanced, false), (WeaponRankMode.SellPrice, false), (WeaponRankMode.SellPricePlusMaterials, false), (WeaponRankMode.Damage, false) })
			{
				WeaponRank.Force(mode, skip);
				try
				{
					for (int stage = 0; stage <= 3; stage++)
					{
						WeaponRank.ForceStage = stage;
						foreach (int cap in capable)
						{
							float b = WeaponRank.Of(Make(cap));
							foreach (int pt in plain)
							{
								float r = WeaponRank.Of(Make(pt));
								if (mode != WeaponRankMode.Damage) check(b > r, $"{Lang.GetItemNameValue(cap)} ({b:0.0}) outranks {Lang.GetItemNameValue(pt)} ({r:0.0}) in {mode} at stage {stage}");
							}
						}
					}
					WeaponRank.ForceStage = -1;
					float ps = WeaponRank.Of(Make(ItemID.PlatinumBroadsword)); WeaponRank.PureContactPenalty = false; float off = WeaponRank.Of(Make(ItemID.PlatinumBroadsword)); float offE = WeaponRank.Of(Make(ItemID.EnchantedSword)); float offY = WeaponRank.Of(Make(ItemID.WoodYoyo)); WeaponRank.PureContactPenalty = true;
					check(ps <= off * WeaponRank.PureContactFactor + 0.01f || ps <= 1.01f, $"a pure-contact blade is x{WeaponRank.PureContactFactor} in {mode} ({off:0.0} -> {ps:0.0})");
					check(Math.Abs(WeaponRank.Of(Make(ItemID.EnchantedSword)) - offE) < 0.01f && Math.Abs(WeaponRank.Of(Make(ItemID.WoodYoyo)) - offY) < 0.01f, $"ranged-capable swords and yoyos are not changed in {mode}");
					check(ps > 0f, "a plain blade still ranks");
				}
				finally { WeaponRank.ForceStage = -1; WeaponRank.PureContactPenalty = true; WeaponRank.Release(); }
			}
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

		// ------------------------------------------------------------------ Balanced: the stage discount and the recency boost

		private static void CheckBalancedCurves(Action<bool, string> check)
		{
			check(WeaponRank.StageDiscount(-3f) == 1f && WeaponRank.StageDiscount(0f) == 1f, "a weapon at or ahead of the world is not discounted");
			check(WeaponRank.StageDiscount(1f) > 0.99f && WeaponRank.StageDiscount(2f) > 0.97f, "one or two stages behind is barely touched (" + WeaponRank.StageDiscount(1f).ToString("F3") + ", " + WeaponRank.StageDiscount(2f).ToString("F3") + ")");
			float mini = WeaponRank.StageDiscount(5.5f);
			check(mini > 0.55f && mini < 0.7f, "a Minishark (tier 1.5) in early Hardmode keeps about three fifths (" + mini.ToString("F3") + ")");
			check(Math.Abs(WeaponRank.StageDiscount(7f) - (float)Math.Exp(-1)) < 1e-4f, "seven stages behind is down to 1/e");
			check(WeaponRank.StageDiscount(10f) < 0.06f && WeaponRank.StageDiscount(14f) < 0.001f && WeaponRank.StageDiscount(14f) > 0f, "many stages behind is heavily discounted, never zero");
			bool mono = true; float prev = 1f;
			for (float g = 0f; g <= 25f; g += 0.25f) { float f = WeaponRank.StageDiscount(g); if (f > prev + 1e-7f || f <= 0f) mono = false; prev = f; }
			check(mono, "the discount only ever falls as the gap grows, and stays positive");

			float day = LevyClock.TicksPerDay;
			check(Math.Abs(WeaponRank.RecencyBoost(0) - 1.6f) < 1e-5f && WeaponRank.RecencyBoost(-500) == WeaponRank.RecencyBoost(0), "a weapon deposited now is boosted x1.6");
			check(WeaponRank.RecencyBoost((long)(day * 0.5)) > 1.4f && WeaponRank.RecencyBoost((long)(day * 0.5)) < 1.5f, "half a day on it is still x1.4 or more (" + WeaponRank.RecencyBoost((long)(day * 0.5)).ToString("F3") + ")");
			float one = WeaponRank.RecencyBoost((long)day), two = WeaponRank.RecencyBoost((long)(day * 2));
			check(one > LevyConstants.AutoUpgradeScoreMargin && one < 1.3f, "after one day the boost is still above the swap margin (" + one.ToString("F3") + ")");
			check(two < 1.02f && WeaponRank.RecencyBoost((long)(day * 5)) < 1.0001f, "after two days it is gone (" + two.ToString("F4") + ")");
			bool fall = true; prev = 2f;
			for (long t = 0; t <= (long)(day * 4); t += 3000) { float b = WeaponRank.RecencyBoost(t); if (b > prev + 1e-7f || b < 1f) fall = false; prev = b; }
			check(fall, "the boost only ever fades, and never below 1");

			// the stamp on an item
			try
			{
				LevyClock.Force(50 * LevyClock.TicksPerDay);
				var w = Make(ItemID.IronBroadsword);
				check(DepositStamp.Of(w) == 0 && DepositStamp.Boost(w) == 1f, "a weapon nobody has stamped has no boost");
				check(DepositStamp.Stamp(w) && DepositStamp.Of(w) == LevyClock.Now && !DepositStamp.Stamp(w), "a weapon is stamped once, with the clock");
				check(Math.Abs(DepositStamp.Boost(w) - 1.6f) < 1e-5f, "a freshly stamped weapon is boosted x1.6");
				LevyClock.Force(52 * LevyClock.TicksPerDay);
				check(DepositStamp.Boost(w) < 1.02f, "two days later the stamp has run out");
				var clone = w.Clone();
				check(DepositStamp.Of(clone) == DepositStamp.Of(w), "a clone of the item carries its stamp");
				var old = Make(ItemID.IronBroadsword); DepositStamp.Stamp(old, DepositStamp.Adopted);
				check(DepositStamp.Boost(old) == 1f, "a weapon adopted from an old save is not new");
			}
			finally { LevyClock.Force(null); }
		}

		/// <summary>Real items against a made-up tier table: a Copper Shortsword tier 0, a Minishark 1.5, Cobalt and Mythril swords 7.</summary>
		private static void CheckBalancedRanks(Action<bool, string> check)
		{
			var table = new float[ItemLoader.ItemCount]; for (int i = 0; i < table.Length; i++) table[i] = ProgressionTier.Inf;
			table[ItemID.CopperShortsword] = 0f; table[ItemID.Minishark] = 1.5f; table[ItemID.CobaltSword] = 7f; table[ItemID.MythrilSword] = 7f; table[ItemID.WoodenSword] = 0f;
			ProgressionTier.SetForTest(table);
			WeaponRank.Force(WeaponRankMode.Balanced, false);
			try
			{
				LevyClock.Force(100 * LevyClock.TicksPerDay);
				var copper = Make(ItemID.CopperShortsword); var mini = Make(ItemID.Minishark); var cobalt = Make(ItemID.CobaltSword); var mythril = Make(ItemID.MythrilSword);
				float price(Item it) => (float)Math.Pow(Math.Max(1, it.value), WeaponRank.PriceExponent);
				WeaponRank.ForceProgress = 7f; // early Hardmode
				check(WeaponRank.Of(copper) < 0.4f * price(copper), "a Copper Shortsword in Hardmode keeps under 40% of its price rank (" + WeaponRank.Of(copper).ToString("F1") + " of " + price(copper).ToString("F1") + ")");
				check(WeaponRank.Of(mini) > 0.55f * price(mini), "a Minishark in early Hardmode keeps more than half its price rank (" + (WeaponRank.Of(mini) / price(mini)).ToString("F2") + ")");
				check(WeaponRank.Of(cobalt) == price(cobalt) && WeaponRank.Of(mythril) == price(mythril), "on-tier weapons rank by price alone");
				WeaponRank.ForceProgress = 0f;
				check(WeaponRank.Of(mini) == price(mini) && WeaponRank.Of(copper) == price(copper), "before the first boss nothing is discounted");
				WeaponRank.ForceProgress = 2f;
				var slimeStaff = Make(ItemID.SlimeStaff); var impStaff = Make(ItemID.ImpStaff); var hornetStaff = Make(ItemID.HornetStaff); var frogStaff = Make(ItemID.VampireFrogStaff);
				check(WeaponRank.Of(slimeStaff) < WeaponRank.Of(impStaff) && WeaponRank.Of(slimeStaff) < WeaponRank.Of(hornetStaff) && WeaponRank.Of(slimeStaff) < WeaponRank.Of(frogStaff), "a minion staff ranks on its damage, so the Slime Staff (8 damage, 100000 copper) does not outrank the Imp, Hornet or Vampire Frog staff");
				WeaponRank.ForceProgress = 13f; // after Golem
				check(WeaponRank.Of(mini) < 0.1f * price(mini) && WeaponRank.Of(copper) >= 1f, "a Minishark after Golem is heavily discounted, and no rank is below 1");
				// recency
				WeaponRank.ForceProgress = 7f;
				float oldCobalt = WeaponRank.Of(cobalt), oldMythril = WeaponRank.Of(mythril);
				DepositStamp.Stamp(cobalt);
				check(WeaponRank.Of(cobalt) >= oldCobalt * LevyConstants.AutoUpgradeScoreMargin * 1.3f, "a fresh weapon clears the upgrade margin over an identical old one");
				check(WeaponRank.Of(cobalt) > oldMythril && oldMythril > oldCobalt, "a fresh Cobalt Sword is preferred to an old Mythril Sword of similar rank");
				var freshWood = Make(ItemID.WoodenSword); DepositStamp.Stamp(freshWood);
				check(WeaponRank.Of(freshWood) < oldCobalt * 0.5f, "a fresh Wooden Sword does not beat a much better old weapon");
				var freshCopper = Make(ItemID.CopperShortsword); DepositStamp.Stamp(freshCopper);
				check(WeaponRank.Of(freshCopper) < oldCobalt * 0.25f, "a fresh Copper Shortsword in Hardmode is nowhere near an old Cobalt Sword");
				check(WeaponRank.Of(mini) > 0 && WeaponRank.Of(Make(ItemID.IronBroadsword)) >= 1f, "ranks stay at least 1");
			}
			finally { WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); ProgressionTier.Reset(); }
		}

		/// <summary>Found weapons (no recipe, not sold) rank a little above crafted ones: Enchanted Sword over Gold and Platinum Broadswords before any boss, never over a clearly better crafted weapon.</summary>
		private static void CheckFoundBoost(Action<bool, string> check)
		{
			var table = new float[ItemLoader.ItemCount]; for (int i = 0; i < table.Length; i++) table[i] = ProgressionTier.Inf;
			var found = new bool[table.Length];
			table[ItemID.EnchantedSword] = 1.5f; found[ItemID.EnchantedSword] = true;
			table[ItemID.GoldBroadsword] = 0f; table[ItemID.PlatinumBroadsword] = 0f; table[ItemID.IronBroadsword] = 0f;
			ProgressionTier.SetForTest(table, found);
			WeaponRank.Force(WeaponRankMode.Balanced, false);
			try
			{
				LevyClock.Force(100 * LevyClock.TicksPerDay);
				WeaponRank.ForceProgress = 1f; // before the Eye of Cthulhu
				float price(Item it) => (float)Math.Pow(Math.Max(1, it.value), WeaponRank.PriceExponent);
				var ench = Make(ItemID.EnchantedSword); var gold = Make(ItemID.GoldBroadsword); var plat = Make(ItemID.PlatinumBroadsword); var iron = Make(ItemID.IronBroadsword);
				check(ProgressionTier.IsFound(ItemID.EnchantedSword) && !ProgressionTier.IsFound(ItemID.GoldBroadsword), "found flag: Enchanted Sword yes, Gold Broadsword no");
				check(Math.Abs(WeaponRank.Of(ench) / price(ench) - WeaponRank.FoundBoost) < 1e-3f, "a found weapon ranks " + WeaponRank.FoundBoost + " x its price rank (" + (WeaponRank.Of(ench) / price(ench)).ToString("F3") + ")");
				check(Math.Abs(WeaponRank.Of(gold) / price(gold) - 1f) < 0.01f, "a crafted weapon ranks by price (stage discount under 1%), no found boost (" + (WeaponRank.Of(gold) / price(gold)).ToString("F3") + ")");
				check(WeaponRank.FoundBoost > 1.02f && WeaponRank.FoundBoost < LevyConstants.AutoUpgradeScoreMargin, "the boost is small and under the swap margin (" + WeaponRank.FoundBoost + " < " + LevyConstants.AutoUpgradeScoreMargin + ")");
				DepositStamp.Stamp(gold); DepositStamp.Stamp(plat); // freshly crafted
				check(WeaponRank.Of(ench) > WeaponRank.Of(gold) * LevyConstants.AutoUpgradeScoreMargin && WeaponRank.Of(ench) > WeaponRank.Of(plat) * LevyConstants.AutoUpgradeScoreMargin, "an old Enchanted Sword beats fresh Gold and Platinum Broadswords by more than the swap margin (" + WeaponRank.Of(ench).ToString("F0") + " vs " + WeaponRank.Of(plat).ToString("F0") + ")");
				// never over a clearly better crafted weapon: a crafted weapon 15% or more above the raw price rank still wins
				var better = Make(ItemID.PlatinumBroadsword); better.value = (int)(ench.value * 1.25f);
				check(WeaponRank.Of(better) > WeaponRank.Of(ench), "a crafted weapon whose price rank is 15% higher outranks the found one");
			}
			finally { WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); ProgressionTier.Reset(); }
		}

		/// <summary>The "Levy aim accuracy" option divides aim error: 1 leaves it exactly alone.</summary>
		private static void CheckAimAccuracy(Action<bool, string> check)
		{
			float configured = Common.Config.LevyConfig.Get?.AimAccuracy ?? 1f;
			try
			{
				WeaponUse.SetAimAccuracy(1f);
				check(WeaponUse.AimAccuracyFactor == 1f, "accuracy 1 leaves aim error exactly unchanged");
				WeaponUse.SetAimAccuracy(2f);
				check(Math.Abs(WeaponUse.AimAccuracyFactor - 0.5f) < 1e-6f, "accuracy 2 halves aim error");
				WeaponUse.SetAimAccuracy(0.5f);
				check(Math.Abs(WeaponUse.AimAccuracyFactor - 2f) < 1e-6f, "accuracy 0.5 doubles aim error");
				WeaponUse.SetAimAccuracy(0f);
				check(Math.Abs(WeaponUse.AimAccuracyFactor - 10f) < 1e-4f, "accuracy is floored at 0.1, so error never goes infinite");
			}
			finally { WeaponUse.SetAimAccuracy(configured); }
		}

		// ------------------------------------------------------------------ Fallen Stars into mana

		private static void CheckStarMana(Action<bool, string> check)
		{
			check(StarMana.CrystalsFor(5, 5) == 1 && StarMana.CrystalsFor(4, 5) == 0 && StarMana.CrystalsFor(10, 5) == 2 && StarMana.CrystalsFor(999, 5) == 199, "at 5 stars per crystal: 5 make 1, 4 make none, 10 make 2");
			check(StarMana.CrystalsFor(9, 2) == 4 && StarMana.CrystalsFor(7, 3) == 2 && StarMana.CrystalsFor(1, 1) == 1 && StarMana.CrystalsFor(0, 3) == 0 && StarMana.CrystalsFor(-4, 3) == 0, "at 2 stars: 9 make 4; at 3 stars: 7 make 2; at 1 star: 1 makes 1; none make none");
			check(StarMana.CrystalsFor(10, 0) == 10 && StarMana.CrystalsFor(10, 9) == 2, "a ratio outside 1..5 is held to the range");
			var a = StarMana.Distribute(5, new[] { 0, 0, 0 }, new[] { 10, 10, 10 });
			check(a[0] + a[1] + a[2] == 5 && a.Max() - a.Min() <= 1 && a[0] >= a[2], "five crystals among three equal mages go 2, 2, 1");
			var b = StarMana.Distribute(4, new[] { 3, 0, 1 }, new[] { 5, 5, 5 });
			check(b[0] == 0 && b[1] == 3 && b[2] == 1, "worst-supplied first: 4 crystals among 3, 0 and 1 go 0, 3, 1 (got " + string.Join(",", b) + ")");
			var c = StarMana.Distribute(10, new[] { 9, 0, 2 }, new[] { 0, 1, 5 });
			check(c[0] == 0 && c[1] == 1 && c[2] == 5 && c.Sum() == 6, "no mage is given more than he has room for, and a mage at the cap gets none (got " + string.Join(",", c) + ")");
			check(StarMana.Distribute(3, new int[0], new int[0]).Length == 0 && StarMana.Distribute(0, new[] { 1 }, new[] { 4 })[0] == 0, "nothing to give or nobody to give it to is fine");
			var te = new ArmoryTE();
			te.Storage[0] = new Item(); te.Storage[0].SetDefaults(ItemID.FallenStar); te.Storage[0].stack = 12;
			te.Storage[3] = new Item(); te.Storage[3].SetDefaults(ItemID.FallenStar); te.Storage[3].stack = 4;
			check(te.CountInStorage(ItemID.FallenStar) == 16, "the armory counts its stars across stacks");
			check(te.TakeUpTo(ItemID.FallenStar, 15) == 15 && te.CountInStorage(ItemID.FallenStar) == 1, "taking stars empties stacks in order and leaves the remainder");
			check(te.TakeUpTo(ItemID.FallenStar, 15) == 1 && te.CountInStorage(ItemID.FallenStar) == 0, "taking more than there is takes what is there");
			check(!te.HoldsWeaponUsing(AmmoID.FallenStar), "an armory without a Star Cannon holds nothing that fires stars");
			te.Storage[5] = Make(ItemID.StarCannon);
			check(te.HoldsWeaponUsing(AmmoID.FallenStar) && StarMana.StarsAreAmmo(te, new List<SoldierRecord>()), "a Star Cannon in storage keeps the stars");
			// the whole conversion on throwaway levies: 23 stars at 5 per crystal = 4 crystals, worst-supplied mages first, melee levies and capped mages skipped
			SoldierRecord Mage(int id, int crystals) { var r = new SoldierRecord { Id = id, ManaCrystals = crystals }; r.Gear.Weapon = Make(ItemID.WaterBolt); return r; }
			var armory = new ArmoryTE(); armory.Storage[0] = Make(ItemID.FallenStar); armory.Storage[0].stack = 23;
			var m1 = Mage(1, 0); var m2 = Mage(2, 0); var m3 = Mage(3, 2); var capped = Mage(4, 9); var fighter = new SoldierRecord { Id = 5 }; fighter.Gear.Weapon = Make(ItemID.IronBroadsword);
			var squad = new List<SoldierRecord> { fighter, capped, m3, m2, m1 };
			int made = StarMana.Convert(armory, squad, 5, RosterSystem.CanAbsorbCrystal, r => { r.ManaCrystals++; return true; });
			check(made == 4 && m1.ManaCrystals == 2 && m2.ManaCrystals == 2 && m3.ManaCrystals == 2 && capped.ManaCrystals == 9 && fighter.ManaCrystals == 0, "23 stars at 5 per crystal give 4 crystals to the two worst-supplied mages (got " + made + ": " + m1.ManaCrystals + "," + m2.ManaCrystals + "," + m3.ManaCrystals + ")");
			check(armory.CountInStorage(ItemID.FallenStar) == 3, "exactly 20 stars were spent and the remainder of 3 stays (" + armory.CountInStorage(ItemID.FallenStar) + " left)");
			check(m1.MaxMana == 60 && m1.RawMana == 60, "two crystals make a levy's maximum 60 mana (20 + 2 x 20)");
			int one = StarMana.Convert(armory, squad, 1, RosterSystem.CanAbsorbCrystal, r => { r.ManaCrystals++; return true; });
			check(one == 3 && armory.CountInStorage(ItemID.FallenStar) == 0, "at 1 star per crystal the last 3 stars make 3 crystals (" + one + ")");
			var armory2 = new ArmoryTE(); armory2.Storage[0] = Make(ItemID.FallenStar); armory2.Storage[0].stack = 40; armory2.Storage[1] = Make(ItemID.StarCannon);
			check(StarMana.Convert(armory2, new List<SoldierRecord> { Mage(7, 0) }, 5, RosterSystem.CanAbsorbCrystal, r => { r.ManaCrystals++; return true; }) == 0 && armory2.CountInStorage(ItemID.FallenStar) == 40, "stars are kept while a Star Cannon is stocked");
			var armory3 = new ArmoryTE(); armory3.Storage[0] = Make(ItemID.FallenStar); armory3.Storage[0].stack = 40;
			check(StarMana.Convert(armory3, new List<SoldierRecord> { fighter, capped }, 5, RosterSystem.CanAbsorbCrystal, r => { r.ManaCrystals++; return true; }) == 0 && armory3.CountInStorage(ItemID.FallenStar) == 40, "with no mage who can take a crystal no star is spent");
			var armory4 = new ArmoryTE(); armory4.Storage[0] = Make(ItemID.FallenStar); armory4.Storage[0].stack = 10;
			check(StarMana.Convert(armory4, new List<SoldierRecord> { Mage(8, 0) }, 5, RosterSystem.CanAbsorbCrystal, r => false) == 0 && armory4.CountInStorage(ItemID.FallenStar) == 10, "a crystal that is refused gives its stars back");
			var te2 = new ArmoryTE(); var holder = new SoldierRecord { Id = 1 }; holder.Gear.Weapon = Make(ItemID.SuperStarCannon);
			check(!te2.HoldsWeaponUsing(AmmoID.FallenStar) && StarMana.StarsAreAmmo(te2, new List<SoldierRecord> { holder }), "a levy holding a Super Star Cannon keeps the stars");
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
