using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Util;
using Levy.Content.TileEntities;
using Levy.Common.Soldiers;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// ACCESSORY UPGRADE CHAINS. Accessories never go obsolete the way weapons do, so the armory ranks them by what they DO, with one hard rule on top: a direct upgrade always beats what it was made from.
	/// B is an upgrade of A when A is (transitively) an ingredient of B's recipe and both are real accessories (vanilla and every mod, read from the recipe list, recipe groups included). The graph is built
	/// once in PostAddRecipes on the main thread and swapped in whole, so nothing else ever sees it half-built. <see cref="Bonus"/> is a flat margin per level of the longest upgrade chain below an item, which
	/// is larger than every other term in the accessory score, so B outranks A however the rest compares. <see cref="Redundant"/> says a soldier already wears an item or its own upgrade.
	/// </summary>
	public static class AccessoryProgression
	{
		/// <summary>Score per level of upgrade chain below an item: larger than the spread of every other accessory score term between an item and its own upgrade.</summary>
		public const float LevelMargin = 5000f;
		private const int MaxLevels = 12;

		private sealed class Graph
		{
			public Dictionary<int, HashSet<int>> Below = new(); // upgrade -> every accessory it supersedes (transitive)
			public Dictionary<int, int> Level = new();
			public int Edges;
		}
		private static volatile Graph _g = new();

		public static bool Built => _g.Below.Count > 0;
		public static int EdgeCount => _g.Edges;

		/// <summary>True when <paramref name="upgrade"/> is a (transitive) upgrade of <paramref name="basic"/>.</summary>
		public static bool Supersedes(int upgrade, int basic) => _g.Below.TryGetValue(upgrade, out var s) && s.Contains(basic);

		/// <summary>Score added for the number of accessories this item improves on in a row (0 for an item that is nobody's upgrade).</summary>
		public static float Bonus(Item it) => it == null || it.IsAir ? 0f : LevelMargin * Math.Min(_g.Level.TryGetValue(it.type, out int l) ? l : 0, MaxLevels);

		/// <summary>True when something worn already is this item, one of its upgrades, or one of its ingredients: wearing both wastes a slot.</summary>
		public static bool Redundant(Item[] worn, Item cand)
		{
			if (worn == null || cand == null || cand.IsAir) return false;
			for (int i = 0; i < worn.Length; i++) {
				var w = worn[i];
				if (w == null || w.IsAir) continue;
				if (Supersedes(cand.type, w.type) || Supersedes(w.type, cand.type)) return true;
			}
			return false;
		}

		/// <summary>The worn slot a candidate would simply replace (it is an upgrade of what sits there); -1 if none.</summary>
		public static int ReplacesSlot(Item[] worn, Item cand, Func<int, bool> slotOk)
		{
			if (worn == null || cand == null || cand.IsAir) return -1;
			for (int i = 0; i < worn.Length; i++) {
				var w = worn[i];
				if (w == null || w.IsAir || !slotOk(i)) continue;
				if (Supersedes(cand.type, w.type)) return i;
			}
			return -1;
		}

		internal static void Build()
		{
			var direct = new Dictionary<int, HashSet<int>>(); // result -> accessory ingredients
			int edges = 0;
			bool Acc(int t) => t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out var s) && s.accessory && !s.vanity;
			for (int i = 0; i < Recipe.numRecipes; i++) {
				var r = Main.recipe[i];
				if (r == null || r.Disabled || r.createItem == null || !Acc(r.createItem.type)) continue;
				int res = r.createItem.type;
				void Add(int ing) { if (ing == res || !Acc(ing)) return; if (!direct.TryGetValue(res, out var set)) direct[res] = set = new HashSet<int>(); if (set.Add(ing)) edges++; }
				foreach (var ing in r.requiredItem) if (ing != null && !ing.IsAir) Add(ing.type);
				if (r.acceptedGroups != null)
					foreach (int gid in r.acceptedGroups)
						if (RecipeGroup.recipeGroups.TryGetValue(gid, out var grp)) foreach (int t in grp.ValidItems) Add(t);
			}
			// transitive closure (iterative DFS per result; accessory recipe graphs are shallow)
			var closure = new Dictionary<int, HashSet<int>>();
			foreach (int b in direct.Keys) {
				var seen = new HashSet<int>(); var stack = new Stack<int>(direct[b]);
				while (stack.Count > 0) { int a = stack.Pop(); if (!seen.Add(a)) continue; if (direct.TryGetValue(a, out var up)) foreach (int x in up) if (!seen.Contains(x)) stack.Push(x); }
				seen.Remove(b); closure[b] = seen;
			}
			// two items that each reach the other (a conversion recipe) are not an upgrade either way
			var below = new Dictionary<int, HashSet<int>>();
			foreach (var kv in closure) {
				var keep = new HashSet<int>();
				foreach (int a in kv.Value) if (!(closure.TryGetValue(a, out var back) && back.Contains(kv.Key))) keep.Add(a);
				if (keep.Count > 0) below[kv.Key] = keep;
			}
			// level = longest chain of strict upgrades below the item, so an upgrade is always at least one level above what it supersedes
			var level = new Dictionary<int, int>();
			int Lvl(int t) { if (level.TryGetValue(t, out int l)) return l; int best = 0; if (below.TryGetValue(t, out var bs)) foreach (int a in bs) best = Math.Max(best, 1 + (a == t ? 0 : Lvl(a))); level[t] = best; return best; }
			foreach (int t in below.Keys) Lvl(t);
			_g = new Graph { Below = below, Level = level, Edges = edges };
		}

		/// <summary>A sample (up to 600) of (upgrade, ingredient) pairs for the self-check.</summary>
		internal static IEnumerable<KeyValuePair<int, int>> EdgesForCheck()
		{
			int n = 0;
			foreach (var kv in _g.Below) foreach (int a in kv.Value) { if (n++ >= 600) yield break; yield return new KeyValuePair<int, int>(kv.Key, a); }
		}

		/// <summary>One-line summary of an item's place in the graph, for logs.</summary>
		internal static string Describe(int type) => $"level {(_g.Level.TryGetValue(type, out int l) ? l : 0)}, supersedes {(_g.Below.TryGetValue(type, out var s) ? s.Count : 0)}";
	}

	/// <summary>Builds the upgrade graph once recipes are final (main thread), then checks the ordering rules Eve asked for.</summary>
	public sealed class AccessoryProgressionSystem : ModSystem
	{
		public override void PostAddRecipes() => LoadSafe.Run(() => { AccessoryProgression.Build(); Levy.Instance?.Logger.Info($"[AccProg] graph: {AccessoryProgression.EdgeCount} accessory ingredient edges"); SelfCheck(Mod); });

		static void SelfCheck(Mod mod)
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; mod.Logger.Info($"[AccProgCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			var savedServed = ArmoryTE.TestServed;
			try {
				Item Make(int t) { var i = new Item(); i.SetDefaults(t); return i; }
				Check("graph built", AccessoryProgression.Built, AccessoryProgression.EdgeCount + " edges");
				Check("Lightning Boots supersede Hermes Boots", AccessoryProgression.Supersedes(ItemID.LightningBoots, ItemID.HermesBoots), AccessoryProgression.Describe(ItemID.LightningBoots));
				Check("Hermes Boots do not supersede Lightning Boots", !AccessoryProgression.Supersedes(ItemID.HermesBoots, ItemID.LightningBoots));
				// score margin: every upgrade edge in the graph outranks its ingredient for every damage class
				var classes = new[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon, DamageClass.Generic };
				int bad = 0, total = 0; string ex = "";
				foreach (var kv in AccessoryProgression.EdgesForCheck()) {
					var up = Make(kv.Key); var lo = Make(kv.Value);
					foreach (var c in classes) { total++; if (ArmoryTE.AccessoryScoreOf(up, c) <= ArmoryTE.AccessoryScoreOf(lo, c)) { bad++; if (ex.Length < 160) ex += $" {lo.Name}>={up.Name}({c.Name});"; } }
				}
				Check("every upgrade outscores its ingredient (sampled edges)", bad == 0, $"{bad}/{total} violations{ex}");
				var cls = DamageClass.Melee;
				Check("Lightning Boots outscore Hermes Boots", ArmoryTE.AccessoryScoreOf(Make(ItemID.LightningBoots), cls) > ArmoryTE.AccessoryScoreOf(Make(ItemID.HermesBoots), cls));
				// jump bottles: Fart > Sandstorm > Blizzard > Tsunami > Cloud
				int[] order = { ItemID.FartinaJar, ItemID.SandstorminaBottle, ItemID.BlizzardinaBottle, ItemID.TsunamiInABottle, ItemID.CloudinaBottle };
				var vals = new float[order.Length]; bool ok = true;
				for (int i = 0; i < order.Length; i++) { vals[i] = AccessoryMobility.Value(Make(order[i])); if (i > 0 && !(vals[i - 1] > vals[i])) ok = false; }
				Check("bottle order Fart > Sandstorm > Blizzard > Tsunami > Cloud", ok, string.Join(" > ", vals));

				// armory picks
				var r1 = new SoldierRecord { Id = 21, Grade = Grade.Levy };
				var r2 = new SoldierRecord { Id = 22, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { r1, r2 };
				var te = new ArmoryTE();
				te.Deposit(Make(ItemID.HermesBoots)); te.Deposit(Make(ItemID.LightningBoots));
				var first = te.TakeAccessory(r1);
				Check("armory issues Lightning Boots, not Hermes Boots", first != null && first.type == ItemID.LightningBoots, first?.Name ?? "null");
				if (first != null) r1.Gear.Accessories[0] = first;
				te.Deposit(Make(ItemID.ObsidianShield));
				var second = te.TakeAccessory(r1);
				Check("a levy in Lightning Boots is not also given Hermes Boots while other gear is stocked", second != null && second.type != ItemID.HermesBoots, second?.Name ?? "null");
				if (second != null) te.Deposit(second);
				te.Deposit(Make(ItemID.CloudinaBottle)); te.Deposit(Make(ItemID.SandstorminaBottle));
				var r3 = new SoldierRecord { Id = 23, Grade = Grade.Levy };
				var jump = te.TakeAccessory(r3);
				Check("a levy with no mobility takes the best stocked bottle over a plain cloud", jump != null && jump.type != ItemID.CloudinaBottle, jump?.Name ?? "null");
			}
			catch (Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message); }
			finally { ArmoryTE.TestServed = savedServed; }
			mod.Logger.Info($"[AccProgCheck] DONE pass={pass} fail={fail}");
		}
	}
}
