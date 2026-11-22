using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Progression
{
	/// <summary>
	/// Boot self-check for progression assistance, run after recipes are set up and logged as "[ProgCheck]" lines.
	/// Whatever the config says, it asserts the matching state: option off means vanilla, option on means the effect is present.
	/// The loot comparison builds a second, unmodified drop database and runs only when LEVY_PROGCHECK_DIFF=1.
	/// </summary>
	public class ProgressionSelfCheck : ModSystem
	{
		int pass, fail;

		void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[ProgCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		static int RecipesMaking(int type, Mod mod) { int n = 0; for (int r = 0; r < Recipe.numRecipes; r++) { var x = Main.recipe[r]; if (x != null && !x.Disabled && x.Mod == mod && x.createItem.type == type) n++; } return n; }

		public override void PostSetupRecipes()
		{
			string env = Environment.GetEnvironmentVariable("LEVY_PROGCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;   // headless server boots and explicit opt-in only
			pass = fail = 0;
			var c = LevyProgressionConfig.Get;
			Mod.Logger.Info("[ProgCheck] START config: " + Newtonsoft.Json.JsonConvert.SerializeObject(c));
			try
			{
				ProgressionDropsSystem.Ensure(); Math_(); Recipes(c); Drops(c); Shimmer(c); Localization_(c); Shop(c); CalamityOptions(c);
				if (Environment.GetEnvironmentVariable("LEVY_PROGCHECK_DIFF") == "1") Loot(c);
				LogVanilla();
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			Mod.Logger.Info($"[ProgCheck] SUMMARY pass={pass} fail={fail}");
		}

		// ---- pure math ---------------------------------------------------------------------------------------------
		void Math_()
		{
			Check("drop x1 is identity", ProgMath.ScaleDrop(1, 5, 1, 2, 1f) == (1, 5, 1, 2));
			Check("guaranteed 1-2 x2 -> 2-4", ProgMath.ScaleDrop(1, 1, 1, 2, 2f) == (1, 1, 2, 4));
			var s = ProgMath.ScaleDrop(1, 5, 1, 1, 2f);
			Check("1/5 x2 -> 2/5 chance", Math.Abs((double)s.num / s.den - 0.4) < 1e-9 && s.min == 1 && s.max == 1, $"{s}");
			s = ProgMath.ScaleDrop(1, 3, 1, 1, 5f);
			Check("1/3 x5 -> certain, 1-2 items (expected 1.67)", s.num == s.den && s.min >= 1, $"{s}");
			s = ProgMath.ScaleDrop(1, 2, 3, 4, 1.5f);
			Check("1/2 x1.5 -> 3/4 chance", Math.Abs((double)s.num / s.den - 0.75) < 1e-9, $"{s}");
			Check("cost 100% unchanged", ProgMath.ScaleCost(12, 100) == 12);
			Check("cost 50% of 12 = 6", ProgMath.ScaleCost(12, 50) == 6);
			Check("cost never below 1", ProgMath.ScaleCost(1, 10) == 1);
			Check("protected armor ignores every slider", new[] { ItemID.SpectreHood, ItemID.ShroomiteHeadgear, ItemID.SpookyHelmet }.All(t => ProgMath.ArmorPercent(t, 50, 50, 50, 50) == 100));
			Check("chlorophyte 80% x levy 50% = 40%", ProgMath.ArmorPercent(ItemID.ChlorophyteGreaves, 80, 100, 100, 50) == 40);
			Check("hallowed 70% alone = 70%", ProgMath.ArmorPercent(ItemID.HallowedPlateMail, 100, 70, 100, 100) == 70);
			Check("spider 60% alone = 60%", ProgMath.ArmorPercent(ItemID.SpiderMask, 100, 100, 60, 100) == 60);
			Check("other armor takes only the levy slider", ProgMath.ArmorPercent(ItemID.IronHelmet, 10, 10, 10, 75) == 75);
			Check("defaults leave all armor at 100%", ProgMath.ArmorPercent(ItemID.HallowedGreaves, 100, 100, 100, 100) == 100);
			Check("factory 10 unicorn horns +25% = 12", ProgMath.BatchYield(1, 10, 25, 10) == 12);
			Check("factory below min batch has no bonus", ProgMath.BatchYield(1, 9, 25, 10) == 9);
			Check("factory 10 crafts x 150 arrows +25%", ProgMath.BatchYield(150, 10, 25, 10) == 1875);
			Check("potion 40 brews +30% = 52", ProgMath.BatchYield(1, 40, 30, 5) == 52);
			Check("bonus 0 is exactly crafts x yield", ProgMath.BatchYield(3, 25, 0, 2) == 75);
			Check("affordable crafts limited by scarcest ingredient", ProgMath.AffordableCrafts(new[] { 2, 1 }, new[] { 9, 50 }, 10) == 4);
			Check("affordable crafts capped by wanted", ProgMath.AffordableCrafts(new[] { 1 }, new[] { 500 }, 10) == 10);
			Check("mana crystal cost: option 2 on a 5-star recipe = 2", ProgMath.ManaCrystalStars(5, 2) == 2);
			Check("mana crystal cost: option 5 leaves vanilla 5", ProgMath.ManaCrystalStars(5, 5) == 5);
			Check("mana crystal cost: never raised above another mod's cheaper recipe", ProgMath.ManaCrystalStars(3, 5) == 3 && ProgMath.ManaCrystalStars(3, 4) == 3);
			Check("10 stars at option 2 craft 5 mana crystals", ProgMath.AffordableCrafts(new[] { ProgMath.ManaCrystalStars(5, 2) }, new[] { 10 }, 99) == 5);
			Check("10 stars at vanilla craft 2 mana crystals", ProgMath.AffordableCrafts(new[] { ProgMath.ManaCrystalStars(5, 5) }, new[] { 10 }, 99) == 2);
			Check("bulk ammo price 100% = item value", ProgMath.BulkAmmoPrice(50, 100) == 50);
			// army loot
			Check("army bonus: 10 CP at 1% = 0.10", Math.Abs(ProgMath.ArmyBonus(10, 1f) - 0.10) < 1e-9);
			Check("army bonus: 0 CP or 0% = 0", ProgMath.ArmyBonus(0, 1f) == 0 && ProgMath.ArmyBonus(40, 0f) == 0);
			Check("rare reroll: p=1/10 bonus 0.1 gives total p x 1.1", Math.Abs((0.1 + 0.9 * ProgMath.RareRerollChance(1, 10, 0.1)) - 0.11) < 1e-9);
			Check("rare reroll: never above certain, none at bonus 0 or p=1", ProgMath.RareRerollChance(1, 2, 50) == 1.0 && ProgMath.RareRerollChance(1, 10, 0) == 0 && ProgMath.RareRerollChance(1, 1, 1) == 0);
			Check("stochastic round: 2.3 -> 3 below .3, 2 above", ProgMath.RoundStochastic(2.3, 0.29) == 3 && ProgMath.RoundStochastic(2.3, 0.31) == 2 && ProgMath.RoundStochastic(0, 0.1) == 0);
			double mean = 0; int N = 100000; var rr = new Random(7); for (int i = 0; i < N; i++) mean += ProgMath.RoundStochastic(8.0, rr.NextDouble()) + ProgMath.RoundStochastic(0.37, rr.NextDouble());
			Check("stochastic round keeps the mean (8 + 0.37)", Math.Abs(mean / N - 8.37) < 0.02, $"{mean / N:F3}");
			// dungeon spirits
			var sp = ProgMath.SpiritTrials(3f, 0f, 0, true);
			Check("spirit x3, vanilla rolled: 2 extra rolls", sp.whole == 2 && sp.frac < 1e-9, sp.ToString());
			sp = ProgMath.SpiritTrials(3f, 1f, 10, true);
			Check("spirit x3 with 10 CP at 1%: x3.3 = 2.3 extra rolls", sp.whole == 2 && Math.Abs(sp.frac - 0.3) < 1e-9, sp.ToString());
			sp = ProgMath.SpiritTrials(3f, 0f, 0, false);
			Check("spirit, monster fought levies: vanilla roll added (3 rolls)", sp.whole == 3, sp.ToString());
			sp = ProgMath.SpiritTrials(1f, 0f, 0, true);
			Check("spirit x1: no extra rolls (vanilla)", sp.whole == 0 && sp.frac == 0);
			// chlorophyte
			Check("chlorophyte caps: scale 1 is vanilla 40/130, scale 4 is 160/520", ProgMath.ChlorophyteCaps(40, 130, 1f) == (40, 130) && ProgMath.ChlorophyteCaps(40, 130, 4f) == (160, 520));
			Check("extractinator mud: before mechs the mud comes back", ProgMath.ExtractMud(false, 100f, 0.0, 2) == (ItemID.MudBlock, 1));
			Check("extractinator mud: after mechs a roll under the percent gives ore", ProgMath.ExtractMud(true, 25f, 0.24, 2) == (ItemID.ChlorophyteOre, 2));
			Check("extractinator mud: a roll over the percent gives nothing", ProgMath.ExtractMud(true, 25f, 0.26, 2).item == -1);
			Check("bulk ammo price 150% of 50 = 75", ProgMath.BulkAmmoPrice(50, 150) == 75);
		}

		// ---- Calamity options: off means nothing is added, on means the recipe / hook / rule is there -----------------------------
		void CalamityOptions(LevyProgressionConfig c)
		{
			bool cal = CalamityLink.Loaded;
			int magnet = ModContent.ItemType<global::Levy.Content.Items.SuperpoweredMagnet>(), agit = ModContent.ItemType<global::Levy.Content.Items.SlimeAgitator>(), scoria = CalamityLink.ItemType("ScoriaOre");
			Check("calamity: magnet recipe iff option and Calamity", (RecipesMaking(magnet, Mod) > 0) == (c.SuperpoweredMagnetRecipe && cal));
			Check("calamity: agitator recipe iff option and Calamity", (RecipesMaking(agit, Mod) > 0) == (c.SlimeAgitatorRecipe && cal));
			Check("calamity: scoria recipe iff option and Calamity", scoria <= 0 ? !cal || !c.ScoriaOreRecipe : (RecipesMaking(scoria, Mod) > 0) == (c.ScoriaOreRecipe && cal));
			Check("calamity: the ore hook is installed iff Calamity is loaded", CalamityOreGen.Installed == cal);
			Check("calamity: post-Providence material set is built iff Calamity is loaded", (ProgressionDropsSystem.PostMaterials.Count == 3) == cal);
			Check("calamity: no Calamity means every Calamity option is inert (no material set, no rules)", cal || (ProgressionDropsSystem.ProvidenceRules == 0 && ProgressionDropsSystem.PostMaterialRules == 0));
		}

		// ---- recipes -----------------------------------------------------------------------------------------------
		void Recipes(LevyProgressionConfig c)
		{
			Check("life crystal recipe iff option", (RecipesMaking(ItemID.LifeCrystal, Mod) > 0) == c.LifeCrystalRecipe);
			Check("life fruit recipe iff option", (RecipesMaking(ItemID.LifeFruit, Mod) > 0) == c.LifeFruitRecipe);
			Check("fallen star recipe iff option", (RecipesMaking(ItemID.FallenStar, Mod) > 0) == c.FallenStarRecipe);
			Check("no Levy-added mana crystal recipe (the option edits the vanilla one)", RecipesMaking(ItemID.ManaCrystal, Mod) == 0);
			Check("chlorophyte ore recipe iff option", (RecipesMaking(ItemID.ChlorophyteOre, Mod) > 0) == c.ChlorophyteOreRecipe);

			// Mana Crystal: Fallen Stars per crystal. Expected is the pre-Levy cost (another mod may already have changed it) capped by the option.
			var crystals = Enumerable.Range(0, Recipe.numRecipes).Select(r => Main.recipe[r]).Where(x => x != null && x.Mod != Mod && !x.Disabled && x.createItem.type == ItemID.ManaCrystal && x.requiredItem.Any(i => i.type == ItemID.FallenStar)).ToList();
			Check("mana crystal star recipe found", crystals.Count > 0, crystals.Count.ToString());
			bool CrystalStarsOk(Recipe m)
			{
				int idx = m.requiredItem.FindIndex(i => i.type == ItemID.FallenStar);
				int baseStars = ProgressionRecipes.Snapshot.TryGetValue(m, out var sn) ? sn.ing[idx] : m.requiredItem[idx].stack;
				return m.requiredItem[idx].stack == ProgMath.ManaCrystalStars(baseStars, c.FallenStarsPerManaCrystal);
			}
			Check("mana crystal costs the option's Fallen Stars (default 5 = vanilla)", crystals.All(CrystalStarsOk), $"option {c.FallenStarsPerManaCrystal}");
			Check("edited mana crystal recipe count matches", ProgressionRecipes.EditedManaCrystalRecipes == (c.FallenStarsPerManaCrystal < ProgConst.ManaCrystalStarsVanilla ? crystals.Count : 0), ProgressionRecipes.EditedManaCrystalRecipes.ToString());

			// Chlorophyte bar cost: every bar recipe matches the option (vanilla = 5).
			var bars = Enumerable.Range(0, Recipe.numRecipes).Select(r => Main.recipe[r]).Where(x => x != null && x.Mod != Mod && x.createItem.type == ItemID.ChlorophyteBar).ToList();
			Check("chlorophyte bar recipes found", bars.Count > 0, bars.Count.ToString());
			int expectOre = c.ChlorophyteOrePerBar;
			// Expected is the pre-Levy cost (another mod may already have changed it) capped by the option.
			bool BarOreOk(Recipe b)
			{
				if (!b.TryGetIngredient(ItemID.ChlorophyteOre, out var o)) return false;
				int baseOre = ProgressionRecipes.Snapshot.TryGetValue(b, out var sn) ? sn.ing[b.requiredItem.IndexOf(o)] : o.stack;
				return o.stack == (expectOre < 5 ? Math.Min(baseOre, expectOre) : baseOre);
			}
			Check("chlorophyte ore per bar = option", bars.All(BarOreOk), $"option {expectOre}");
			Check("edited bar recipes count matches", ProgressionRecipes.EditedBarRecipes == (c.ChlorophyteOrePerBar < 5 ? bars.Count : 0), ProgressionRecipes.EditedBarRecipes.ToString());

			// Ammo and armor: compare every vanilla/other-mod recipe against its pre-edit snapshot.
			int ammoOk = 0, ammoBad = 0, armorOk = 0, armorBad = 0, other = 0, otherBad = 0, protectedBad = 0, foreign = 0;
			foreach (var kv in ProgressionRecipes.Snapshot)
			{
				Recipe rec = kv.Key;
				if (rec == null || rec.createItem == null || rec.Disabled) continue;   // disabled recipes (e.g. replaced by another mod) are never edited
				// Only what Levy itself changes is checked: a recipe another mod edited after Levy's edits were made (its own PostSetupRecipes, a runtime edit) no longer matches what Levy left, whatever Levy did to it.
				if (ProgressionRecipes.PostEdit.TryGetValue(rec, out var left) && (rec.createItem.stack != left.result || !rec.requiredItem.Select(i => i.stack).SequenceEqual(left.ing))) { foreign++; continue; }
				Item res = rec.createItem;
				bool isAmmo = ProgressionRecipes.IsAmmo(res);
				bool isArmor = ProgressionRecipes.IsArmorPiece(res);
				bool isBar = res.type == ItemID.ChlorophyteBar;
				int expectStack = kv.Value.result;
				if (isAmmo && c.AmmoRecipeYieldMultiplier > 1) expectStack = (int)Math.Min((long)expectStack * c.AmmoRecipeYieldMultiplier, Math.Max(1, res.maxStack));
				bool stackOk = res.stack == expectStack;
				int[] expectIng = kv.Value.ing.ToArray();
				if (isArmor)
				{
					int pct = ProgMath.ArmorPercent(res.type, c.ChlorophyteArmorCostPercent, c.HallowedArmorCostPercent, c.SpiderArmorCostPercent, c.LevyArmorCostPercent);
					for (int i = 0; i < expectIng.Length; i++) expectIng[i] = ProgMath.ScaleCost(expectIng[i], pct);
					if (Array.IndexOf(ProgConst.ProtectedArmor, res.type) >= 0 && !rec.requiredItem.Select(i => i.stack).SequenceEqual(kv.Value.ing)) protectedBad++;
				}
				if (res.type == ItemID.ManaCrystal)
					for (int i = 0; i < expectIng.Length; i++) if (rec.requiredItem[i].type == ItemID.FallenStar) expectIng[i] = ProgMath.ManaCrystalStars(expectIng[i], c.FallenStarsPerManaCrystal);
				if (isBar && c.ChlorophyteOrePerBar < 5)
					for (int i = 0; i < expectIng.Length; i++) if (rec.requiredItem[i].type == ItemID.ChlorophyteOre) expectIng[i] = Math.Min(expectIng[i], c.ChlorophyteOrePerBar);
				bool ingOk = rec.requiredItem.Select(i => i.stack).SequenceEqual(expectIng);
				bool ok = stackOk && ingOk;
				if (isAmmo) { if (ok) ammoOk++; else ammoBad++; }
				else if (isArmor) { if (ok) armorOk++; else armorBad++; }
				else { other++; if (!ok) otherBad++; }
			}
			Check("ammo recipes match option", ammoBad == 0 && ammoOk > 0, $"ok={ammoOk} bad={ammoBad} mult={c.AmmoRecipeYieldMultiplier}");
			Check("armor recipes match options", armorBad == 0 && armorOk > 0, $"ok={armorOk} bad={armorBad}");
			Check("Spectre, Shroomite and Spooky recipes never change", protectedBad == 0);
			Check("untouched recipes identical to vanilla", otherBad == 0, $"{other} recipes" + (foreign > 0 ? $", {foreign} left out: another mod edited them after Levy's edits" : ""));
			bool allDefault = c.FallenStarsPerManaCrystal == ProgConst.ManaCrystalStarsVanilla && c.AmmoRecipeYieldMultiplier == 1 && c.ChlorophyteOrePerBar == 5 && c.ChlorophyteArmorCostPercent == 100 && c.HallowedArmorCostPercent == 100 && c.SpiderArmorCostPercent == 100 && c.LevyArmorCostPercent == 100;
			if (allDefault) Check("defaults: zero vanilla recipes edited", ProgressionRecipes.EditedAmmoRecipes + ProgressionRecipes.EditedArmorRecipes + ProgressionRecipes.EditedBarRecipes + ProgressionRecipes.EditedManaCrystalRecipes == 0);
			else Mod.Logger.Info($"[ProgCheck] INFO edited recipes: bar={ProgressionRecipes.EditedBarRecipes} mana={ProgressionRecipes.EditedManaCrystalRecipes} ammo={ProgressionRecipes.EditedAmmoRecipes} armor={ProgressionRecipes.EditedArmorRecipes}");
		}

		// ---- drops: army loot, lunar items, chlorophyte sources, dungeon spirits, extractinator -----------------------
		static int Chains<T>(IEnumerable<IItemDropRule> roots) where T : IItemDropRule
		{
			var all = new List<IItemDropRule>(); ProgressionLootNpc.Collect(roots, all);
			int n = 0;
			foreach (var r in all) if (r.ChainedRules != null) foreach (var ch in r.ChainedRules) if (ch.RuleToChain is T) n++;
			return n;
		}

		void Drops(LevyProgressionConfig c)
		{
			var db = Main.ItemDropsDB;
			Check("drop database exists", db != null);
			if (db == null) return;
			Check("army loot hook registered on bosses and bags", ProgressionDropsSystem.ArmyRules > 0, $"{ProgressionDropsSystem.ArmyRules} material rules");
			Check("rare-chance reroll registered on boss drops", ProgressionDropsSystem.RareRules > 0, $"{ProgressionDropsSystem.RareRules} rules");
			Check("Moon Lord Luminite carries the bonus rule (drop + bag)", Chains<ExtraAmountRule>(db.GetRulesForNPCID(NPCID.MoonLordCore, false)) >= 1 && Chains<ExtraAmountRule>(db.GetRulesForItemID(ItemID.MoonLordBossBag)) >= 1, $"{ProgressionDropsSystem.LuminiteRules} luminite rules");
			bool frag = true;
			foreach (int pillar in new[] { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust }) frag &= Chains<ExtraAmountRule>(db.GetRulesForNPCID(pillar, false)) >= 1;
			Check("each lunar pillar carries the fragment bonus rule", frag, $"{ProgressionDropsSystem.FragmentRules} fragment rules");
			Check("a vanilla boss (Skeletron Prime) has its rare drops rerolled", Chains<RareRerollRule>(db.GetRulesForNPCID(NPCID.SkeletronPrime, false)) >= 1);
			{
				var zl = new List<IItemDropRule>(); ProgressionLootNpc.Collect(db.GetRulesForNPCID(NPCID.Zombie, false), zl);
				int armyOnZombie = 0; var zMat = new List<string>();
				foreach (var r in zl) if (r.ChainedRules != null) foreach (var ch in r.ChainedRules) if (ch.RuleToChain is ExtraAmountRule e) { if (e.Army || e.BossRule) armyOnZombie++; else if (e.Material) zMat.Add(Lang.GetItemNameValue(e.ItemId)); }
				Check("ordinary monsters get no army bonus or reroll (only the material drop rates, on materials)", armyOnZombie + Chains<RareRerollRule>(db.GetRulesForNPCID(NPCID.Zombie, false)) == 0, zMat.Count > 0 ? "material rule on: " + string.Join(", ", zMat) : "");
			}
			Check("Chlorophyte global drop rules registered (jungle + event)", ProgressionGlobalNpc.GlobalRules >= 2 && db.GetRulesForNPCID(NPCID.Zombie, true).OfType<ChlorophyteSourceRule>().Count() == 2, ProgressionGlobalNpc.GlobalRules.ToString());
			Check("extractinator: mud mode iff option", ProgressionExtractinatorSystem.Registered == c.ExtractinatorChlorophyte && (!c.ExtractinatorChlorophyte || ItemID.Sets.ExtractinatorMode[ItemID.MudBlock] == ItemID.MudBlock), $"mode {ItemID.Sets.ExtractinatorMode[ItemID.MudBlock]}");
			Check("hooks present: dungeon spirit OnKill, extractinator", ModContent.GetInstance<ProgressionGlobalNpc>() != null && ModContent.GetInstance<ProgressionExtractinator>() != null);
			// The drop conditions against a fake kill (no world needed).
			var zombie = new NPC(); zombie.SetDefaults(NPCID.Zombie);
			var p = new Player();
			var info = new DropAttemptInfo { npc = zombie, player = p };
			bool saveP = NPC.downedPlantBoss, saveH = Main.hardMode, savePm = Main.pumpkinMoon;
			try
			{
				NPC.downedPlantBoss = true; Main.hardMode = true; p.ZoneJungle = true;
				Check("jungle Chlorophyte condition follows the option", new JungleChlorophyteCondition().CanDrop(info) == c.JungleChlorophyteDrop);
				p.ZoneJungle = false;
				Check("jungle Chlorophyte condition needs the jungle", !new JungleChlorophyteCondition().CanDrop(info));
				p.ZoneJungle = true; NPC.downedPlantBoss = false;
				Check("jungle Chlorophyte condition needs Plantera down", !new JungleChlorophyteCondition().CanDrop(info));
				Main.pumpkinMoon = true;
				Check("event Chlorophyte condition follows the option (Pumpkin Moon)", new EventChlorophyteCondition().CanDrop(info) == c.EventChlorophyteDrop);
				Main.pumpkinMoon = false;
				var corite = new NPC(); corite.SetDefaults(NPCID.SolarCorite);
				Check("event Chlorophyte condition: lunar event enemy yes", new EventChlorophyteCondition().CanDrop(new DropAttemptInfo { npc = corite, player = p }) == c.EventChlorophyteDrop, $"value {corite.value} life {corite.lifeMax} friendly {corite.friendly} town {corite.townNPC} statue {corite.SpawnedFromStatue} boss {corite.boss}");
				Check("event Chlorophyte condition: ordinary zombie no (no event running)", !new EventChlorophyteCondition().CanDrop(info), $"pumpkin {Main.pumpkinMoon} snow {Main.snowMoon}");
				var bossNpc = new NPC(); bossNpc.SetDefaults(NPCID.EyeofCthulhu); Main.pumpkinMoon = true;
				Check("event Chlorophyte condition excludes bosses", !new EventChlorophyteCondition().CanDrop(new DropAttemptInfo { npc = bossNpc, player = p }));
			}
			finally { NPC.downedPlantBoss = saveP; Main.hardMode = saveH; Main.pumpkinMoon = savePm; }
			Check("army CP of a player with no soldiers is 0", ArmyCp.Of(p) == 0);
			var ex = new ExtraAmountRule { ItemId = ItemID.LunarOre, Luminite = true, Average = () => 80 };
			Check("luminite bonus = multiplier - 1 (no army)", Math.Abs(ex.Bonus(p) - Math.Max(0f, c.LuminiteDropMultiplier - 1f)) < 1e-9, $"x{c.LuminiteDropMultiplier}");
			var ex2 = new ExtraAmountRule { ItemId = ItemID.FragmentSolar, Fragment = true, Average = () => 40 };
			Check("fragment bonus = multiplier - 1 (no army)", Math.Abs(ex2.Bonus(p) - Math.Max(0f, c.LunarFragmentDropMultiplier - 1f)) < 1e-9);
			Check("ore, bar and fragment are boss materials; boss mask and coin are not", ProgressionDropsSystem.IsMaterial(ItemID.ChlorophyteOre) && ProgressionDropsSystem.IsMaterial(ItemID.HallowedBar) && ProgressionDropsSystem.IsMaterial(ItemID.FragmentNebula) && !ProgressionDropsSystem.IsMaterial(ItemID.SkeletronPrimeMask) && !ProgressionDropsSystem.IsMaterial(ItemID.GoldCoin), $"ore {ProgressionDropsSystem.IsMaterial(ItemID.ChlorophyteOre)} bar {ProgressionDropsSystem.IsMaterial(ItemID.HallowedBar)} frag {ProgressionDropsSystem.IsMaterial(ItemID.FragmentNebula)} mask {ProgressionDropsSystem.IsMaterial(ItemID.SkeletronPrimeMask)} coin {ProgressionDropsSystem.IsMaterial(ItemID.GoldCoin)}; mask used by [{ProgressionDropsSystem.UsedBy(ItemID.SkeletronPrimeMask)}], coin used by [{ProgressionDropsSystem.UsedBy(ItemID.GoldCoin)}]");
			{
				// a base-game item another mod's recipe uses (Fargo's takes boss masks and trophies) stays what the base game says it is; the masks and trophies of every vanilla boss are never materials
				var only = ProgressionDropsSystem.ThirdPartyOnlyMaterials();
				var leaked = only.Where(ProgressionDropsSystem.IsMaterial).ToList();
				Check("base-game items only other mods' recipes use are not boss materials", leaked.Count == 0, $"{only.Count} such items ({string.Join(", ", only.Take(8).Select(t => Lang.GetItemNameValue(t)))}{(only.Count > 8 ? ", ..." : "")}), {leaked.Count} still counted");
				int[] vanity = { ItemID.SkeletronPrimeMask, ItemID.EyeMask, ItemID.SkeletronMask, ItemID.PlanteraMask, ItemID.GolemMask, ItemID.BossMaskMoonlord, ItemID.EyeofCthulhuTrophy, ItemID.SkeletronPrimeTrophy, ItemID.MoonLordTrophy };
				var bad = vanity.Where(ProgressionDropsSystem.IsMaterial).Select(t => Lang.GetItemNameValue(t)).ToList();
				Check("base-game boss masks and trophies are not boss materials, whatever other mods craft from them", bad.Count == 0, bad.Count == 0 ? "" : string.Join(", ", bad));
				var bars = new[] { ItemID.IronBar, ItemID.HallowedBar, ItemID.SoulofMight, ItemID.ShadowScale, ItemID.TissueSample, ItemID.BeetleHusk, ItemID.Ectoplasm, ItemID.TurtleShell, ItemID.LunarOre }.Where(t => !ProgressionDropsSystem.IsMaterial(t)).Select(t => Lang.GetItemNameValue(t)).ToList();
				Check("base-game boss materials stay materials (bars, souls, scales, husks, ectoplasm, luminite)", bars.Count == 0, bars.Count == 0 ? "" : string.Join(", ", bars));
				Mod.Logger.Info($"[ProgCheck] OBSERVED {ProgressionDropsSystem.NotArmyAnyMore.Count} base-game boss drops another mod's recipe makes a material get no army bonus: {string.Join(", ", ProgressionDropsSystem.NotArmyAnyMore.Take(30).Select(t => Lang.GetItemNameValue(t)))}");
			}
			MaterialRates(c);
			Check("chlorophyte density cap follows config", ProgMath.ChlorophyteCaps(40, 130, c.ChlorophyteChunkScale).nearCap == (int)(40 * c.ChlorophyteChunkScale), $"scale {c.ChlorophyteChunkScale}, growth x{c.ChlorophyteGrowthMultiplier}");
		}

		// ---- the three material drop rates (universal, boss, event and enemy) ---------------------------------------------
		static List<ExtraAmountRule> ExtrasOn(IEnumerable<IItemDropRule> roots, int item)
		{
			var all = new List<IItemDropRule>(); ProgressionLootNpc.Collect(roots, all);
			var l = new List<ExtraAmountRule>();
			foreach (var r in all) if (r.ChainedRules != null) foreach (var ch in r.ChainedRules) if (ch.RuleToChain is ExtraAmountRule e && e.ItemId == item) l.Add(e);
			return l;
		}

		void MaterialRates(LevyProgressionConfig c)
		{
			var db = Main.ItemDropsDB;
			Check("material rates: the material rule is on boss, enemy and global drops", ProgressionDropsSystem.MaterialRules > ProgressionDropsSystem.ArmyRules && ProgressionDropsSystem.GlobalMaterialRules > 0, $"{ProgressionDropsSystem.MaterialRules} material rules, {ProgressionDropsSystem.GlobalMaterialRules} global, {ProgressionDropsSystem.ArmyRules} army");
			var gel = ExtrasOn(db.GetRulesForNPCID(NPCID.BlueSlime, false), ItemID.Gel);
			Check("material rates: a Blue Slime's Gel carries the material rule (an enemy rule)", gel.Count >= 1 && gel.All(e => e.Material && !e.BossRule), $"{gel.Count} rules");
			var bars = ExtrasOn(db.GetRulesForNPCID(NPCID.SkeletronPrime, false), ItemID.HallowedBar).Concat(ExtrasOn(db.GetRulesForItemID(ItemID.SkeletronPrimeBossBag), ItemID.HallowedBar)).ToList();
			Check("material rates: Skeletron Prime's and his bag's Hallowed Bars carry it as a boss rule", bars.Count >= 2 && bars.All(e => e.Material && e.BossRule), $"{bars.Count} rules");
			var souls = ExtrasOn(db.GetRulesForNPCID(NPCID.BlueSlime, true), ItemID.SoulofLight);
			Check("material rates: the global Soul of Light drop carries it, its source decided at the kill", souls.Count >= 1 && souls.All(e => e.Material && !e.BossRule), $"{souls.Count} rules");
			// non-materials never carry it
			int bad = 0; string first = "";
			var seen = new HashSet<ExtraAmountRule>(ReferenceEqualityComparer.Instance);
			foreach (var kv in ContentSamples.NpcsByNetId)
			{
				var all = new List<IItemDropRule>(); ProgressionLootNpc.Collect(db.GetRulesForNPCID(kv.Key, true), all);
				foreach (var r in all) if (r.ChainedRules != null) foreach (var ch in r.ChainedRules)
					if (ch.RuleToChain is ExtraAmountRule e && seen.Add(e) && e.Material && !ProgressionDropsSystem.RateMaterial(e.ItemId)) { bad++; if (first == "") first = Lang.GetItemNameValue(e.ItemId); }
			}
			Check("material rates: no weapon, accessory, bag or other non-material carries the material rule", bad == 0, bad == 0 ? $"{seen.Count} extra-amount rules looked at" : $"{bad}, first {first}");
			Check("material rates: the Pwnhammer and the Wall of Flesh's emblems carry no extra-amount rule", ExtrasOn(db.GetRulesForNPCID(NPCID.WallofFlesh, false), ItemID.Pwnhammer).Count == 0 && ExtrasOn(db.GetRulesForNPCID(NPCID.WallofFlesh, false), ItemID.WarriorEmblem).Count == 0);
			// source of a drop
			var eye = new NPC(); eye.SetDefaults(NPCID.EyeofCthulhu); var zom = new NPC(); zom.SetDefaults(NPCID.Zombie);
			Check("material rates: a boss, a boss bag or a boss's rule is a boss source; a zombie and a crate are not",
				ExtraAmountRule.BossSource(new DropAttemptInfo { npc = eye }, false) && ExtraAmountRule.BossSource(new DropAttemptInfo { item = ItemID.EyeOfCthulhuBossBag }, false) && ExtraAmountRule.BossSource(new DropAttemptInfo { npc = zom }, true)
				&& !ExtraAmountRule.BossSource(new DropAttemptInfo { npc = zom }, false) && !ExtraAmountRule.BossSource(new DropAttemptInfo { item = ItemID.WoodenCrate }, false));
			// the combination rule (a standalone config object: the live one is never written)
			var t = new LevyProgressionConfig { UniversalMaterialDropMultiplier = 2f, BossMaterialDropMultiplier = 2f, EnemyMaterialDropMultiplier = 1f };
			Check("material rates: 2x universal and 2x boss give a boss material 3x (bonus 2) and an enemy material 2x (bonus 1)", Math.Abs(ExtraAmountRule.MaterialBonus(true, t) - 2) < 1e-9 && Math.Abs(ExtraAmountRule.MaterialBonus(false, t) - 1) < 1e-9);
			t.UniversalMaterialDropMultiplier = 1f; t.BossMaterialDropMultiplier = 1f; t.EnemyMaterialDropMultiplier = 3.5f;
			Check("material rates: 3.5x event and enemy alone gives enemies 3.5x and bosses nothing extra", Math.Abs(ExtraAmountRule.MaterialBonus(false, t) - 2.5) < 1e-9 && ExtraAmountRule.MaterialBonus(true, t) == 0);
			t.EnemyMaterialDropMultiplier = 1f;
			Check("material rates: all three at 1 add nothing", ExtraAmountRule.MaterialBonus(true, t) == 0 && ExtraAmountRule.MaterialBonus(false, t) == 0);
			bool off = c.UniversalMaterialDropMultiplier <= 1f && c.BossMaterialDropMultiplier <= 1f && c.EnemyMaterialDropMultiplier <= 1f;
			if (off) Check("material rates: off as booted, a material rule adds nothing to a boss or an enemy drop", ExtraAmountRule.MaterialBonus(true, c) == 0 && ExtraAmountRule.MaterialBonus(false, c) == 0);
			else Mod.Logger.Info($"[ProgCheck] INFO material rates as booted: universal x{c.UniversalMaterialDropMultiplier}, boss x{c.BossMaterialDropMultiplier}, event and enemy x{c.EnemyMaterialDropMultiplier}");
		}

		// ---- shimmer -----------------------------------------------------------------------------------------------
		void Shimmer(LevyProgressionConfig c)
		{
			var ring = ProgressionWorld.ShardRing;
			var map = ItemID.Sets.ShimmerTransformToItem;
			if (c.ShimmerDesertShards)
			{
				bool good = true;
				for (int k = 0; k < ring.Length; k++) good &= map[ring[k]] == ring[(k + 1) % ring.Length];
				Check("shimmer ring set (Fragment -> Dark -> Light -> Fragment)", good && ProgressionWorld.ShardRingSkipped == 0, $"skipped={ProgressionWorld.ShardRingSkipped}");
				// Following the ring from the desert fragment reaches Dark Shards.
				Check("desert fragment reaches Dark Shard in one shimmer", map[ItemID.AncientBattleArmorMaterial] == ItemID.DarkShard);
			}
			else Check("shimmer off: shards and fragment have no shimmer result (vanilla)", ring.All(t => map[t] == -1));
		}

		static Item Fresh(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		// ---- Arms Dealer shop--------------------------------------------------------------------------------------
		void Shop(LevyProgressionConfig c)
		{
			var items = new Item[40];
			for (int i = 0; i < items.Length; i++) items[i] = new Item();
			var shop = (Item[])items.Clone();
			shop[0] = Fresh(ItemID.MusketBall);
			int added = ProgressionShops.AddBulkAmmo(shop, new List<int> { ItemID.ChlorophyteBullet, ItemID.MusketBall, ItemID.HellfireArrow }, 150);
			Check("bulk ammo fills free slots, skips stocked ammo", added == 2 && shop[1].type == ItemID.ChlorophyteBullet && shop[2].type == ItemID.HellfireArrow);
			Check("bulk ammo price uses percent of item value", shop[1].shopCustomPrice == ProgMath.BulkAmmoPrice(Fresh(ItemID.ChlorophyteBullet).value, 150));
			var full = Enumerable.Range(0, 40).Select(_ => Fresh(ItemID.Wood)).ToArray();
			Check("bulk ammo does nothing in a full shop", ProgressionShops.AddBulkAmmo(full, new List<int> { ItemID.ChlorophyteBullet }, 100) == 0);
			Check("bulk ammo is capped", ProgressionShops.AddBulkAmmo(items, Enumerable.Range(1000, 30).ToList(), 100) <= ProgConst.BulkAmmoMaxSlots);

			var pp = new Player().GetModPlayer<ProgressionPlayer>();
			var bullet = Fresh(ItemID.ChlorophyteBullet);
			int th = c.BulkAmmoUnlockAmount;
			pp.CountAmmo(bullet, th - 1);
			Check("below threshold: not unlocked", !pp.UnlockedAmmoTypes(th).Contains(bullet.type));
			pp.CountAmmo(bullet, 1);
			Check("at threshold: unlocked", pp.UnlockedAmmoTypes(th).Contains(bullet.type));
			pp.CountAmmo(Fresh(ItemID.IronBar), 5000);
			Check("non-ammo is never counted", !pp.AmmoObtained.ContainsKey(ProgressionPlayer.KeyOf(Fresh(ItemID.IronBar))));
			Check("ammo key round-trips", ProgressionPlayer.TypeOfKey(ProgressionPlayer.KeyOf(bullet)) == bullet.type);
		}

		// ---- localization ------------------------------------------------------------------------------------------
		void Localization_(LevyProgressionConfig c)
		{
			var missing = new List<string>();
			foreach (var f in typeof(LevyProgressionConfig).GetFields().Where(f => !f.IsStatic))
			{
				foreach (var part in new[] { "Label", "Tooltip" })
				{
					string key = $"Mods.Levy.Configs.LevyProgressionConfig.{f.Name}.{part}";
					if (!Language.Exists(key)) missing.Add(key);
				}
			}
			foreach (var h in new[] { "Note", "DropRates", "Chlorophyte", "Items", "ArmorCosts", "ArmsDealer", "ArmyLoot", "DungeonSpirits", "ChlorophyteSources", "Lunar" })
				if (!Language.Exists("Mods.Levy.Configs.LevyProgressionConfig.Headers." + h)) missing.Add("Headers." + h);
			foreach (var k in new[] { "Mods.Levy.Progression.PlanteraRepeat", "Mods.Levy.Progression.JungleChlorophyte", "Mods.Levy.Progression.EventChlorophyte" })
				if (!Language.Exists(k)) missing.Add(k);
			Check("every progression string has text", missing.Count == 0, string.Join(", ", missing));
		}

		// ---- loot (needs LEVY_PROGCHECK_DIFF=1) ---------------------------------------------------------------------
		void Loot(LevyProgressionConfig c)
		{
			ProgressionLootNpc.Bypass = true;
			var vanilla = new ItemDropDatabase();
			vanilla.Populate();
			ProgressionLootNpc.Bypass = false;
			var ours = Main.ItemDropsDB;

			int[] mobs = { NPCID.DungeonSpirit, NPCID.WyvernHead, NPCID.BlueSlime, NPCID.Zombie, NPCID.DemonEye, NPCID.Skeleton, NPCID.Pixie, NPCID.Mimic };
			foreach (int id in mobs)
			{
				var a = new List<IItemDropRule>(); var b = new List<IItemDropRule>();
				ProgressionLootNpc.Collect(vanilla.GetRulesForNPCID(id, true), a);
				ProgressionLootNpc.Collect(ours.GetRulesForNPCID(id, true), b); b.RemoveAll(r => r is ChlorophyteSourceRule || r is ExtraAmountRule || r is UelibloomSourceRule);
				int compared = 0, bad = 0; string firstBad = "";
				if (a.Count != b.Count) { Check($"loot rule structure NPC {id}", false, $"{a.Count} vs {b.Count}"); continue; }
				for (int k = 0; k < a.Count; k++)
				{
					if (a[k] is CommonDrop va && b[k] is CommonDrop ob)
					{
						float f = ProgressionLootNpc.Factor(va.itemId, false, c);
						var e = ProgMath.ScaleDrop(va.chanceNumerator, va.chanceDenominator, va.amountDroppedMinimum, va.amountDroppedMaximum, f);
						compared++;
						if ((ob.chanceNumerator, ob.chanceDenominator, ob.amountDroppedMinimum, ob.amountDroppedMaximum) != e) { bad++; if (firstBad == "") firstBad = $"item {va.itemId} vanilla ({va.chanceNumerator}/{va.chanceDenominator},{va.amountDroppedMinimum}-{va.amountDroppedMaximum}) ours ({ob.chanceNumerator}/{ob.chanceDenominator},{ob.amountDroppedMinimum}-{ob.amountDroppedMaximum}) expected {e} factor {f}"; }
					}
				}
				Check($"loot NPC {id} ({Lang.GetNPCNameValue(id)}) drops match options", bad == 0, $"{compared} drops compared; {firstBad}");
			}

			// Expected effective Ectoplasm change on the Dungeon Spirit.
			var ecto = ours.GetRulesForNPCID(NPCID.DungeonSpirit, false).OfType<CommonDrop>().FirstOrDefault(d => d.itemId == ItemID.Ectoplasm);
			var ectoV = vanilla.GetRulesForNPCID(NPCID.DungeonSpirit, false).OfType<CommonDrop>().FirstOrDefault(d => d.itemId == ItemID.Ectoplasm);
			if (ecto != null && ectoV != null)
				Mod.Logger.Info($"[ProgCheck] INFO Dungeon Spirit Ectoplasm vanilla {ectoV.chanceNumerator}/{ectoV.chanceDenominator} x{ectoV.amountDroppedMinimum}-{ectoV.amountDroppedMaximum}; now {ecto.chanceNumerator}/{ecto.chanceDenominator} x{ecto.amountDroppedMinimum}-{ecto.amountDroppedMaximum}");

			// Plantera: extra Grenade Launcher rule iff option.
			int pv = vanilla.GetRulesForNPCID(NPCID.Plantera, false).Count, po = ours.GetRulesForNPCID(NPCID.Plantera, false).Count;
			Check("Plantera repeat Grenade Launcher rule iff option", po == pv + (c.OnceOnlyBossWeaponRepeatPercent > 0 ? 1 : 0), $"vanilla {pv} rules, ours {po}");
			var bagV = vanilla.GetRulesForItemID(ItemID.PlanteraBossBag).Count; var bagO = ours.GetRulesForItemID(ItemID.PlanteraBossBag).Count;
			Check("Plantera bag repeat rule iff option", bagO == bagV + (c.OnceOnlyBossWeaponRepeatPercent > 0 ? 1 : 0), $"vanilla {bagV} rules, ours {bagO}");

			// Crate ore.
			int crates = 0, crateBad = 0;
			foreach (int crate in new[] { ItemID.IronCrateHard, ItemID.GoldenCrateHard, ItemID.JungleFishingCrateHard, ItemID.FrozenCrateHard })
			{
				var a = new List<IItemDropRule>(); var b = new List<IItemDropRule>();
				ProgressionLootNpc.Collect(vanilla.GetRulesForItemID(crate), a);
				ProgressionLootNpc.Collect(ours.GetRulesForItemID(crate), b);
				if (a.Count != b.Count) { crateBad++; continue; }
				for (int k = 0; k < a.Count; k++)
					if (a[k] is CommonDrop va && b[k] is CommonDrop ob && Array.IndexOf(ProgConst.EarlyHardmodeCrateOres, va.itemId) >= 0)
					{
						crates++;
						int m = c.EarlyHardmodeCrateOreMultiplier;
						if (ob.amountDroppedMinimum != va.amountDroppedMinimum * m || ob.amountDroppedMaximum != va.amountDroppedMaximum * m) crateBad++;
						if (crates == 1) Mod.Logger.Info($"[ProgCheck] INFO crate {crate} ore {va.itemId}: vanilla {va.amountDroppedMinimum}-{va.amountDroppedMaximum}, now {ob.amountDroppedMinimum}-{ob.amountDroppedMaximum}");
					}
			}
			Check("early Hardmode crate ore amounts match option", crateBad == 0 && crates > 0, $"{crates} ore rules compared");
		}

		// ---- vanilla facts for the report --------------------------------------------------------------------------
		void LogVanilla()
		{
			string Describe(Recipe rec) { var snap = ProgressionRecipes.Snapshot[rec]; return $"{rec.createItem.Name} x{snap.result} <= " + string.Join(" + ", rec.requiredItem.Select((i, k) => $"{(k < snap.ing.Length ? snap.ing[k] : i.stack)} {i.Name}")); }
			var want = new HashSet<int>(ProgConst.ChlorophyteArmor.Concat(ProgConst.HallowedArmor).Concat(ProgConst.SpiderArmor).Concat(new int[] { ItemID.ChlorophyteBar, ItemID.WoodenArrow, ItemID.MusketBall, ItemID.LifeCrystal, ItemID.ManaCrystal }));
			foreach (var kv in ProgressionRecipes.Snapshot)
				if (kv.Key.createItem != null && want.Contains(kv.Key.createItem.type))
					Mod.Logger.Info("[ProgCheck] VANILLA " + Describe(kv.Key));
			var bullet = Fresh(ItemID.ChlorophyteBullet);
			Mod.Logger.Info($"[ProgCheck] VANILLA Chlorophyte Bullet value={bullet.value} copper");
		}
	}
}
