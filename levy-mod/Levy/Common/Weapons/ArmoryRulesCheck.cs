using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the armory rule list: the save and wire round trips, rule evaluation in the pure allocator (minimum, include, hard order, class balance, lists, overrides, tag edits, conflicts),
	/// and the armory's issue path under a deny list. Throwaway objects only, never throws; logs one summary line, or every failure. In the style of <see cref="WeaponAllocatorCheck"/>.
	/// </summary>
	public class ArmoryRulesCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		private static readonly int[] Bows = { ItemID.WoodenBow, ItemID.CopperBow, ItemID.TinBow, ItemID.IronBow, ItemID.LeadBow, ItemID.SilverBow, ItemID.TungstenBow, ItemID.GoldBow, ItemID.PlatinumBow, ItemID.DemonBow };
		private static readonly int[] Guns = { ItemID.Musket, ItemID.Handgun, ItemID.FlintlockPistol, ItemID.Minishark, ItemID.TheUndertaker, ItemID.Boomstick };
		private static readonly int[] Swords = { ItemID.CopperBroadsword, ItemID.IronBroadsword, ItemID.TinBroadsword, ItemID.LeadBroadsword, ItemID.SilverBroadsword, ItemID.TungstenBroadsword, ItemID.GoldBroadsword, ItemID.PlatinumBroadsword, ItemID.BladeofGrass, ItemID.Muramasa };
		private static readonly int[] Staves = { ItemID.WandofSparking, ItemID.AmethystStaff, ItemID.TopazStaff, ItemID.SapphireStaff, ItemID.EmeraldStaff, ItemID.RubyStaff, ItemID.DiamondStaff, ItemID.WaterBolt, ItemID.DemonScythe, ItemID.BookofSkulls };

		public override void PostSetupRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}
			WeaponTags.Reset(); ArmoryRules.ResetKeys();
			WeaponRank.Force(WeaponRankMode.SellPrice, false);
			try
			{
				Guard("tags", () => CheckTags(Check));
				Guard("round trips", () => CheckRoundTrips(Check));
				Guard("minimum and include", () => CheckMin(Check));
				Guard("hard order", () => CheckOrder(Check));
				Guard("class balance", () => CheckClasses(Check));
				Guard("lists and overrides", () => CheckLists(Check));
				Guard("tag edits", () => CheckTagEdits(Check));
				Guard("conflicts", () => CheckConflicts(Check));
				Guard("neutral rules", () => CheckNeutral(Check));
				Guard("armory issue", () => CheckArmory(Check));
			}
			finally { WeaponRank.Release(); }
			string census;
			try { census = Census(); } catch (Exception e) { census = "census failed: " + e.GetType().Name; }
			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy armory rules self-check: {ok} checks OK. {census}");
				else
				{
					Mod.Logger.Warn($"Levy armory rules self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (var f in failures) Mod.Logger.Warn("Levy armory rules self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		/// <summary>How the generic tag derivation sorts every issuable weapon of the loaded content: counts by category and by class (a modded weapon needs no table).</summary>
		private static string Census()
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var cats = new SortedDictionary<string, int>(); var cls = new SortedDictionary<string, int>();
			int total = 0, noCat = 0;
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var it = new Item();
				try { it.SetDefaults(t); } catch (Exception) { continue; }
				if (it.accessory || it.ammo > 0 || it.createTile >= 0 || it.pick > 0 || !WeaponClassifier.IsIssuableWeapon(it)) continue;
				total++;
				string c = WeaponTags.DerivedCategory(it);
				if (c == null) noCat++; else cats[c] = (cats.TryGetValue(c, out var a) ? a : 0) + 1;
				string k = WeaponTags.DerivedClass(it) ?? "none";
				cls[k] = (cls.TryGetValue(k, out var b) ? b : 0) + 1;
			}
			return $"Tags over {total} issuable weapons ({sw.ElapsedMilliseconds} ms): categories [{string.Join(", ", cats.Select(x => x.Key + " " + x.Value))}], none {noCat}; classes [{string.Join(", ", cls.Select(x => x.Key + " " + x.Value))}]; class ids [{string.Join(", ", WeaponTags.ClassKeys)}].";
		}

		// ------------------------------------------------------------------ helpers

		/// <summary>Deals <paramref name="levies"/> unarmored levies the given stock under <paramref name="rules"/> and returns the weapon each was given.</summary>
		private static List<Item> Deal(ArmoryRules rules, int levies, params int[][] stock)
		{
			using var _rs = ArmoryRules.Enter(rules);
			var ls = new List<AllocLevy>();
			for (int i = 0; i < levies; i++) ls.Add(new AllocLevy { Id = 100 + i });
			var cands = new List<AllocCand>(); int seq = 0;
			foreach (var arr in stock) foreach (int t in arr) cands.Add(WeaponAllocator.MakeCand(Make(t), 1, seq++, AllocSlot.Shelf, null, null));
			WeaponAllocator.Plan(ls, cands, rules.Alloc);
			return ls.Select(l => l.Target?.It).ToList();
		}

		private static int CountCat(ArmoryRules rules, List<Item> items, string cat) { using var _rs = ArmoryRules.Enter(rules); return items.Count(i => i != null && ArmoryRules.Has(rules.CatsOf(i), cat)); }
		private static int CountCls(ArmoryRules rules, List<Item> items, string cls) { using var _rs = ArmoryRules.Enter(rules); return items.Count(i => i != null && rules.ClassKeyOf(i) == cls); }

		private static ArmoryRules Empty() => new ArmoryRules();

		private static RuleBlock Cat(ArmoryRules r, CatMode mode, string cat, string other = null, int count = 1, float weight = 2f)
		{
			var b = r.AddBlock(RuleKind.Category); b.Mode = mode; b.Cat = cat; if (other != null) b.Other = other; b.Count = count; b.Weight = weight; r.Changed(); return b;
		}

		// ------------------------------------------------------------------ the checks

		private static void CheckTags(Action<bool, string> check)
		{
			check(WeaponTags.DerivedCategory(Make(ItemID.WoodenBow)) == "bow", "a wooden bow is a bow");
			check(WeaponTags.DerivedCategory(Make(ItemID.Musket)) == "gun", "a musket is a gun");
			check(WeaponTags.DerivedCategory(Make(ItemID.CopperBroadsword)) == "sword", "a broadsword is a sword");
			check(WeaponTags.DerivedCategory(Make(ItemID.AmethystStaff)) == "staff", "an amethyst staff is a staff");
			check(WeaponTags.DerivedCategory(Make(ItemID.WaterBolt)) == "tome", "water bolt is a tome");
			check(WeaponTags.DerivedCategory(Make(ItemID.SlimeStaff)) == "minion", "the slime staff is a minion staff");
			check(WeaponTags.DerivedCategory(Make(ItemID.Spear)) == "spear", "a spear is a spear");
			check(WeaponTags.DerivedCategory(Make(ItemID.RocketI)) == null, "ammo has no weapon category");
			check(WeaponTags.ClassKeys.Contains("melee") && WeaponTags.ClassKeys.Contains("summon"), "the vanilla classes are listed");
			check(WeaponTags.ClassFromKey("ranged") == DamageClass.Ranged && WeaponTags.ClassKey(DamageClass.Magic) == "magic", "class ids round trip");
		}

		private static ArmoryRules Sample()
		{
			var r = ArmoryRules.Default();
			var c = r.AddBlock(RuleKind.ClassBalance); var e = c.EntryFor("magic", true); e.Weight = 2.5f; e.Min = 1; e.Max = 6; e.Fixed = -1;
			Cat(r, CatMode.Min, "bow", null, 7); Cat(r, CatMode.Over, "bow", "gun"); Cat(r, CatMode.Weight, "staff", null, 1, 1.5f); Cat(r, CatMode.Include, "spear");
			var l = r.AddBlock(RuleKind.ItemList); l.List = ListMode.Never; l.Items.Add(ArmoryRules.KeyOf(ItemID.Minishark)); l.Items.Add("SomeMod/Missing");
			var o = r.OverrideFor(ItemID.Musket, true); o.Rank = RankOverrideMode.Set; o.Value = 321f; o.Class = "melee"; o.CatAdd.Add("bow"); o.CatRemove.Add("gun");
			var p = r.OverrideFor(ItemID.WoodenSword, true); p.Rank = RankOverrideMode.Prefer;
			r.Blocks[2].On = false;
			r.Changed();
			return r;
		}

		private static void CheckRoundTrips(Action<bool, string> check)
		{
			var r = Sample();
			var bytes = r.ToBytes();
			var back = ArmoryRules.FromBytes(bytes);
			check(back.Blocks.Count == r.Blocks.Count && back.Overrides.Count == r.Overrides.Count, "the byte round trip keeps the blocks and overrides");
			check(back.ToBytes().SequenceEqual(bytes), "the byte round trip is exact");
			check(!back.Blocks[2].On && back.Blocks[1].Classes.Count == 1 && back.Blocks[1].Classes[0].Weight == 2.5f && back.Blocks[1].Classes[0].Max == 6, "block fields survive (on/off, class entry numbers)");
			check(back.Blocks[2].Mode == CatMode.Min && back.Blocks[2].Count == 7 && back.Blocks[3].Other == "gun" && back.Blocks[4].Weight == 1.5f, "category modes and numbers survive");
			check(back.Blocks[6].Items.Contains("SomeMod/Missing"), "an item key of an absent mod is kept");
			var ov = back.Overrides.TryGetValue(ArmoryRules.KeyOf(ItemID.Musket), out var m) ? m : null;
			check(ov != null && ov.Rank == RankOverrideMode.Set && ov.Value == 321f && ov.Class == "melee" && ov.CatAdd.Contains("bow") && ov.CatRemove.Contains("gun"), "item overrides survive");

			// the tile entity: save data, wire data
			var te = new ArmoryTE(); te.Rules = Sample();
			var tag = new TagCompound(); te.SaveData(tag);
			var te2 = new ArmoryTE(); te2.LoadData(tag);
			check(te2.Rules.ToBytes().SequenceEqual(te.Rules.ToBytes()), "the rules survive SaveData / LoadData");
			var plain = new ArmoryTE(); var tag0 = new TagCompound(); plain.SaveData(tag0);
			check(!tag0.ContainsKey("rulesV1"), "an untouched armory writes no rules (an old build reading the save loses nothing)");
			var te3 = new ArmoryTE(); te3.Rules = Sample(); te3.LoadData(tag0);
			check(te3.Rules.Blocks.Count == 1 && te3.Rules.Blocks[0].Kind == RuleKind.ClassBalance && te3.Rules.IsNeutral, "a save with no rules loads the default list");
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { te.Rules.Rev = 41; te.NetSend(w); }
				ms.Position = 0;
				var te4 = new ArmoryTE();
				using (var rd = new BinaryReader(ms)) te4.NetReceive(rd);
				check(te4.Rules.ToBytes().SequenceEqual(te.Rules.ToBytes()) && te4.Rules.Rev == 41, "the rules survive NetSend / NetReceive (and carry their revision)");
			}
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) te.NetSend(w);
				ms.Position = 0;
				var te5 = new ArmoryTE { RulesHeldBack = true }; te5.Rules = ArmoryRules.Default();
				using (var rd = new BinaryReader(ms)) te5.NetReceive(rd);
				check(te5.Rules.Blocks.Count == 1, "a held-back client keeps its own edit when a sync arrives");
			}
			// hostile input
			check(ArmoryRules.FromBytes(new byte[] { 1, 255, 255, 9, 9, 9 }).Blocks.Count == 1, "garbage bytes load as the default list");
			check(ArmoryRules.FromBytes(new byte[ArmoryRules.MaxWireBytes + 1]).Blocks.Count == 1, "an oversized blob loads as the default list");
			var evil = new ArmoryRules(); for (int i = 0; i < 100; i++) evil.Blocks.Add(new RuleBlock { Kind = RuleKind.Category, Weight = float.NaN, Count = 100000, Cat = "BAD CAT" });
			var clean = ArmoryRules.FromBytes(evil.ToBytes());
			check(clean.Blocks.Count <= ArmoryRules.MaxBlocks && clean.Blocks.All(b => b.Count <= 9999 && !float.IsNaN(b.Weight) && b.Cat == ""), "blocks and numbers are clamped when read");
		}

		private static void CheckMin(Action<bool, string> check)
		{
			var r = Empty(); Cat(r, CatMode.Min, "bow", null, 7);
			var plan = Deal(r, 12, Bows, Swords, Guns);
			check(CountCat(r, plan, "bow") == 7, "at least 7 bows: 7 of 12 levies carry a bow when 10 are stocked (got " + CountCat(r, plan, "bow") + ")");
			var base0 = Deal(Empty(), 12, Bows, Swords, Guns);
			check(CountCat(Empty(), base0, "bow") < 7, "without the rule the bows are not forced onto 7 levies");
			var few = Deal(r, 12, Bows.Take(3).ToArray(), Swords, Guns);
			check(CountCat(r, few, "bow") == 3 && few.All(i => i != null), "at least 7 bows with only 3 in stock: all 3 are used and nobody is left bare");
			var inc = Empty(); Cat(inc, CatMode.Include, "bow");
			var all = Deal(inc, 14, Bows.Take(4).ToArray(), Swords, Guns);
			check(CountCat(inc, all, "bow") == 4, "include: every one of 4 stocked bows is in use");
			// a levy already holding a bow keeps it toward the minimum
			using (var _rs = ArmoryRules.Enter(r))
			{
				var held = Make(ItemID.WoodenBow);
				var l1 = new AllocLevy { Id = 1, Current = held }; var l2 = new AllocLevy { Id = 2 };
				var cands = new List<AllocCand>(); l1.Own = WeaponAllocator.MakeCand(held, 5, 0, AllocSlot.Primary, l1, null); cands.Add(l1.Own);
				cands.Add(WeaponAllocator.MakeCand(Make(ItemID.CopperBroadsword), 1, 1, AllocSlot.Shelf, null, null));
				var one = Empty(); Cat(one, CatMode.Min, "bow", null, 1);
				WeaponAllocator.Plan(new List<AllocLevy> { l1, l2 }, cands, one.Alloc);
				check(l1.Target == l1.Own, "a levy already holding a bow keeps it toward a minimum of 1");
			}
		}

		private static void CheckOrder(Action<bool, string> check)
		{
			var r = Empty(); Cat(r, CatMode.Over, "bow", "gun");
			var none = Deal(Empty(), 6, Bows.Take(4).ToArray(), Guns);
			check(CountCat(Empty(), none, "gun") > 0, "baseline: with no rule the better guns are dealt before weak bows");
			var plan = Deal(r, 6, Bows.Take(4).ToArray(), Guns);
			check(CountCat(r, plan, "bow") == 4 && CountCat(r, plan, "gun") == 2, "bows over guns: 4 bows then 2 guns for 6 levies (bows " + CountCat(r, plan, "bow") + ", guns " + CountCat(r, plan, "gun") + ")");
			var plenty = Deal(r, 5, Bows, Guns);
			check(CountCat(r, plenty, "gun") == 0 && CountCat(r, plenty, "bow") == 5, "bows over guns: no gun while a bow is free");
			// a levy holding a gun trades it for a bow whatever the rank margin
			using (var _rs = ArmoryRules.Enter(r))
			{
				var gun = Make(ItemID.TheUndertaker);
				var l = new AllocLevy { Id = 1, Current = gun };
				var cands = new List<AllocCand>(); l.Own = WeaponAllocator.MakeCand(gun, 5, 0, AllocSlot.Primary, l, null); cands.Add(l.Own);
				cands.Add(WeaponAllocator.MakeCand(Make(ItemID.WoodenBow), 1, 1, AllocSlot.Shelf, null, null));
				WeaponAllocator.Plan(new List<AllocLevy> { l }, cands, r.Alloc);
				check(l.Target != null && l.Target.It.type == ItemID.WoodenBow, "a levy holding a gun is given a free bow even though the bow ranks lower");
			}
			// soft weight
			var w = Empty(); Cat(w, CatMode.Weight, "bow", null, 1, 1000f);
			using (var _rs = ArmoryRules.Enter(w))
			{
				var bow = Make(ItemID.WoodenBow);
				float plainRank = WeaponRank.Of(bow);
				using (var _x = ArmoryRules.Enter(Empty())) check(Math.Abs(WeaponRank.Of(bow) - plainRank / 1000f) / plainRank < 0.01f, "a weight multiplies the rank of its category only");
				check(WeaponRank.Of(Make(ItemID.Musket)) > 0f, "a weight leaves other categories alone");
			}
		}

		private static void CheckClasses(Action<bool, string> check)
		{
			var r = Empty(); var b = r.AddBlock(RuleKind.ClassBalance);
			b.EntryFor("melee", true).Weight = 3; b.EntryFor("ranged", true).Weight = 2; b.EntryFor("magic", true).Weight = 1; r.Changed();
			var plan = Deal(r, 12, Swords, Bows, Staves);
			check(CountCls(r, plan, "melee") == 6 && CountCls(r, plan, "ranged") == 4 && CountCls(r, plan, "magic") == 2, "weights 3:2:1 spread 12 levies 6 / 4 / 2 (got " + CountCls(r, plan, "melee") + "/" + CountCls(r, plan, "ranged") + "/" + CountCls(r, plan, "magic") + ")");
			var f = Empty(); var fb = f.AddBlock(RuleKind.ClassBalance);
			fb.EntryFor("magic", true).Fixed = 3; fb.EntryFor("ranged", true).Weight = 1; f.Changed();
			var fixedPlan = Deal(f, 10, Swords, Bows, Staves);
			check(CountCls(f, fixedPlan, "magic") == 3 && CountCls(f, fixedPlan, "ranged") == 7, "fixed 3 magic, the rest by weight: 3 magic, 7 ranged");
			var mm = Empty(); var mb = mm.AddBlock(RuleKind.ClassBalance);
			mb.EntryFor("melee", true).Min = 2; mb.EntryFor("melee", true).Max = 2; mb.EntryFor("ranged", true).Weight = 1; mm.Changed();
			var capped = Deal(mm, 8, Swords, Bows, Staves);
			check(CountCls(mm, capped, "melee") == 2, "min 2 and max 2 melee: exactly 2 melee (got " + CountCls(mm, capped, "melee") + ")");
			var mx = Empty(); var xb = mx.AddBlock(RuleKind.ClassBalance); xb.EntryFor("melee", true).Max = 1; mx.Changed();
			var maxed = Deal(mx, 8, Swords, Bows, Staves);
			check(CountCls(mx, maxed, "melee") <= 1 && maxed.All(i => i != null), "melee max 1 with swords on top of the ranking: at most 1 melee, nobody bare (got " + CountCls(mx, maxed, "melee") + ")");
			var scarce = Empty(); var sb2 = scarce.AddBlock(RuleKind.ClassBalance); sb2.EntryFor("magic", true).Fixed = 5; scarce.Changed();
			var short1 = Deal(scarce, 6, Swords, Bows, Staves.Take(2).ToArray());
			check(CountCls(scarce, short1, "magic") == 2 && short1.All(i => i != null), "fixed 5 magic with 2 magic weapons: 2 are used and the rest are armed from other classes");
			var stock = new RuleStock(); stock.Add(new[] { "staff" }, "magic"); stock.Add(new[] { "staff" }, "magic");
			scarce.AddBlock(RuleKind.Category); // an extra block so the status index logic is exercised
			var st = RuleStatus.Describe(scarce, 0, stock, 6);
			check(st.Warn, "the preview flags a class count the stock cannot meet");
		}

		private static void CheckLists(Action<bool, string> check)
		{
			var d = Empty(); var b = d.AddBlock(RuleKind.ItemList); b.List = ListMode.Never; b.Items.Add(ArmoryRules.KeyOf(ItemID.Minishark)); d.Changed();
			var plan = Deal(d, 6, Guns);
			check(plan.Count(i => i != null && i.type == ItemID.Minishark) == 0 && plan.Count(i => i != null) == 5, "a deny list: the denied item is never dealt (5 of 6 guns, 6 levies)");
			using (var _rs = ArmoryRules.Enter(d)) check(WeaponRank.Of(Make(ItemID.Minishark)) == 0f && WeaponRank.Of(Make(ItemID.Musket)) > 0f, "a denied item ranks 0, others keep their rank");
			var only = Empty(); var ob = only.AddBlock(RuleKind.ItemList); ob.List = ListMode.Only; ob.Items.Add(ArmoryRules.KeyOf(ItemID.Musket)); ob.Items.Add(ArmoryRules.KeyOf(ItemID.Handgun)); only.Changed();
			var onlyPlan = Deal(only, 6, Guns, Bows);
			check(onlyPlan.Count(i => i != null) == 2 && onlyPlan.All(i => i == null || i.type == ItemID.Musket || i.type == ItemID.Handgun), "an only list: just the listed items are dealt");

			var o = Empty(); var ov = o.OverrideFor(ItemID.WoodenSword, true); ov.Rank = RankOverrideMode.Prefer; o.Changed();
			var pref = Deal(o, 1, new int[] { ItemID.WoodenSword }, Swords);
			check(pref[0] != null && pref[0].type == ItemID.WoodenSword, "always prefer: the cheapest sword is dealt first");
			var noPref = Deal(Empty(), 1, new int[] { ItemID.WoodenSword }, Swords);
			check(noPref[0] != null && noPref[0].type != ItemID.WoodenSword, "baseline: without the override the wooden sword is not dealt first");
			var never = Empty(); never.OverrideFor(ItemID.Muramasa, true).Rank = RankOverrideMode.Never; never.Changed();
			var np = Deal(never, 3, Swords);
			check(np.All(i => i != null && i.type != ItemID.Muramasa), "never use: the item is not dealt");
			var set = Empty(); var sv = set.OverrideFor(ItemID.CopperBroadsword, true); sv.Rank = RankOverrideMode.Set; sv.Value = 100000f; set.Changed();
			var sp = Deal(set, 1, Swords);
			check(sp[0] != null && sp[0].type == ItemID.CopperBroadsword, "a set rank of 100000 puts a copper sword first");
			using (var _rs = ArmoryRules.Enter(set))
			{
				var bd = WeaponRank.Explain(Make(ItemID.CopperBroadsword));
				check(bd.Override == RankOverrideMode.Set && Math.Abs(bd.Final - 100000f) < 1f, "the rank breakdown reports the override and the final rank");
			}
			using (var _rs = ArmoryRules.Enter(Sample()))
			{
				var bd = WeaponRank.Explain(Make(ItemID.WoodenBow));
				check(Math.Abs(bd.Final - WeaponRank.Of(Make(ItemID.WoodenBow))) < 1e-3f && bd.Base > 0f, "Explain agrees with Of");
			}
		}

		private static void CheckTagEdits(Action<bool, string> check)
		{
			var r = Empty();
			var gunAsBow = r.OverrideFor(ItemID.Musket, true); gunAsBow.CatRemove.Add("gun"); gunAsBow.CatAdd.Add("bow"); r.Changed();
			using (var _rs = ArmoryRules.Enter(r))
			{
				var musket = Make(ItemID.Musket);
				check(ArmoryRules.Has(r.CatsOf(musket), "bow") && !ArmoryRules.Has(r.CatsOf(musket), "gun"), "a category edit retags an item");
				check(ArmoryRules.Has(ArmoryRules.CatsFor(musket), "bow"), "the current rules are what the allocator reads");
			}
			check(ArmoryRules.Has(ArmoryRules.CatsFor(Make(ItemID.Musket)), "gun"), "outside the armory the item keeps its own tags");
			var m = Empty(); Cat(m, CatMode.Min, "bow", null, 1);
			m.OverrideFor(ItemID.Musket, true).CatAdd.Add("bow"); m.Changed();
			var plan = Deal(m, 3, new int[] { ItemID.Musket }, Swords);
			check(plan.Any(i => i != null && i.type == ItemID.Musket), "a gun tagged as a bow satisfies a bows minimum");
			var c = Empty(); c.OverrideFor(ItemID.Musket, true).Class = "melee"; c.Changed();
			using (var _rs = ArmoryRules.Enter(c))
			{
				check(WeaponClassifier.ItemDamageClass(Make(ItemID.Musket)) == DamageClass.Melee, "a class edit changes the class the allocator sees");
				check(c.ClassKeyOf(Make(ItemID.Musket)) == "melee", "and the class id");
			}
			check(WeaponClassifier.ItemDamageClass(Make(ItemID.Musket)) == DamageClass.Ranged, "outside the armory the class is unchanged");
			var none = Empty(); none.OverrideFor(ItemID.Musket, true).Class = "none"; none.Changed();
			using (var _rs = ArmoryRules.Enter(none)) check(WeaponClassifier.ItemDamageClass(Make(ItemID.Musket)) == null, "a class edit to none gives the item no class");
		}

		private static void CheckConflicts(Action<bool, string> check)
		{
			// bows over guns (block 1) against at least 3 guns (block 2): the earlier block wins, the later one is reported
			var r = Empty(); Cat(r, CatMode.Over, "bow", "gun"); Cat(r, CatMode.Min, "gun", null, 3);
			var plan = Deal(r, 6, Bows.Take(5).ToArray(), Guns);
			check(CountCat(r, plan, "gun") == 1 && CountCat(r, plan, "bow") == 5, "an earlier hard order beats a later minimum: 5 bows, 1 gun for 6 levies (guns " + CountCat(r, plan, "gun") + ")");
			var stock = new RuleStock(); for (int i = 0; i < 5; i++) stock.Add(new[] { "bow" }, "ranged"); for (int i = 0; i < 6; i++) stock.Add(new[] { "gun" }, "ranged");
			var st = RuleStatus.Describe(r, 1, stock, 6);
			check(st.Warn && st.Text.Contains("1"), "the preview names the earlier block that stops a minimum");
			// the same two rules the other way round: the minimum comes first and wins
			var r2 = Empty(); Cat(r2, CatMode.Min, "gun", null, 3); Cat(r2, CatMode.Over, "bow", "gun");
			var plan2 = Deal(r2, 6, Bows.Take(5).ToArray(), Guns);
			check(CountCat(r2, plan2, "gun") >= 3, "a minimum placed before the order wins: at least 3 guns (got " + CountCat(r2, plan2, "gun") + ")");
			// a minimum against an earlier class maximum
			var r3 = Empty(); var cb = r3.AddBlock(RuleKind.ClassBalance); cb.EntryFor("ranged", true).Max = 2; r3.Changed(); Cat(r3, CatMode.Min, "bow", null, 5);
			var plan3 = Deal(r3, 8, Bows, Swords);
			check(CountCat(r3, plan3, "bow") <= 2, "an earlier class maximum caps a later minimum (bows " + CountCat(r3, plan3, "bow") + ")");
			var st3 = RuleStatus.Describe(r3, 1, stock, 8);
			check(st3.Warn, "the preview flags a minimum blocked by a class maximum");
			var short0 = new RuleStock();
			var st4 = RuleStatus.Describe(r3, 1, short0, 8);
			check(st4.Warn && st4.Text.Length > 0, "the preview reports 'wanted, in stock' when the stock is short");
		}

		private static void CheckNeutral(Action<bool, string> check)
		{
			var d = ArmoryRules.Default();
			check(d.IsNeutral && !d.Alloc.Active, "the default list does nothing");
			var withRules = Deal(d, 8, Swords, Bows, Guns, Staves);
			var without = Deal(Empty(), 8, Swords, Bows, Guns, Staves);
			bool same = withRules.Count == without.Count;
			for (int i = 0; same && i < withRules.Count; i++) same = withRules[i]?.type == without[i]?.type;
			check(same, "the default list deals exactly what an armory with no rules deals");
		}

		private static void CheckArmory(Action<bool, string> check)
		{
			var te = new ArmoryTE();
			var b = te.Rules.AddBlock(RuleKind.ItemList); b.List = ListMode.Never; b.Items.Add(ArmoryRules.KeyOf(ItemID.Minishark)); te.Rules.Changed();
			te.Storage[0] = Make(ItemID.Minishark); te.Storage[1] = Make(ItemID.Handgun);
			var rec = new SoldierRecord { Id = 900 };
			var taken = te.TakeBestWeapon(rec);
			check(taken != null && taken.type == ItemID.Handgun, "the armory's own pick skips a denied item (took " + (taken?.Name ?? "nothing") + ")");
			var te2 = new ArmoryTE();
			te2.Storage[0] = Make(ItemID.Minishark); te2.Storage[1] = Make(ItemID.Handgun);
			var any = te2.TakeBestWeapon(new SoldierRecord { Id = 901 });
			check(any != null, "an armory with no rules still issues");
			// the server's one entry point for an edited list (a client's packet ends here): replaces the contents, keeps the object, bumps the revision, queues a sync
			var te6 = new ArmoryTE();
			var keep = te6.Rules; int rev0 = te6.Rules.Rev;
			var incoming = ArmoryRules.FromBytes(Sample().ToBytes());
			te6.SetRules(incoming);
			check(ReferenceEquals(te6.Rules, keep) && te6.Rules.Blocks.Count == Sample().Blocks.Count && te6.Rules.Rev == rev0 + 1, "SetRules replaces the contents in place and bumps the revision");
			check(te6.Rules.ToBytes().SequenceEqual(Sample().ToBytes()), "SetRules keeps the list it was given");
			var sold = new SoldierRecord { Id = 902 };
			var te3 = new ArmoryTE(); te3.Rules = Sample(); te3.Storage[0] = Make(ItemID.Minishark);
			check(te3.CountStock().Total == 0, "the stock count leaves out a denied item");
			te3.IssueGear(sold);
			check(sold.Gear.Weapon == null || sold.Gear.Weapon.IsAir || sold.Gear.Weapon.type != ItemID.Minishark, "IssueGear never hands out a denied weapon");
		}
	}
}
