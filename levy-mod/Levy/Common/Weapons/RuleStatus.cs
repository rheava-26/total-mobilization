using System;
using System.Collections.Generic;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>What an armory could issue right now, counted by category and class (the rules' preview reads it). Built by <see cref="Levy.Content.TileEntities.ArmoryTE.CountStock"/>.</summary>
	public sealed class RuleStock
	{
		public readonly Dictionary<string, int> Cats = new(), Classes = new();
		public readonly Dictionary<string, Dictionary<string, int>> CatClass = new();
		public int Total;

		public int Cat(string c) => c != null && Cats.TryGetValue(c, out var n) ? n : 0;
		public int Class(string c) => c != null && Classes.TryGetValue(c, out var n) ? n : 0;

		public void Add(string[] cats, string cls)
		{
			Total++;
			if (cls != null) Classes[cls] = Class(cls) + 1;
			foreach (var c in cats)
			{
				Cats[c] = Cat(c) + 1;
				if (cls == null) continue;
				if (!CatClass.TryGetValue(c, out var d)) CatClass[c] = d = new Dictionary<string, int>();
				d[cls] = (d.TryGetValue(cls, out var k) ? k : 0) + 1;
			}
		}

		/// <summary>The class most of the stocked weapons of a category belong to (null when none are stocked).</summary>
		public string ClassOfCat(string cat)
		{
			if (!CatClass.TryGetValue(cat, out var d)) return null;
			string best = null; int n = 0;
			foreach (var kv in d) if (kv.Value > n) { n = kv.Value; best = kv.Key; }
			return best;
		}
	}

	/// <summary>One block's preview: a line of text, and whether the rule cannot be met as written.</summary>
	public readonly struct BlockStatus
	{
		public readonly string Text; public readonly bool Warn;
		public BlockStatus(string text, bool warn) { Text = text; Warn = warn; }
	}

	/// <summary>
	/// The live preview line under each rule block ("7 bows wanted, 4 in stock") and the checks behind it. Pure: it reads the rule list and a <see cref="RuleStock"/> only, so a multiplayer client can show it
	/// from the synced armory. A block is flagged when it cannot be met: the stock is short, or an EARLIER block already decides against it (the earlier block wins).
	/// </summary>
	public static class RuleStatus
	{
		private static string T(string key, params object[] args) => LevyUtil.T("Rules." + key, args);
		private static string Plural(string id) => WeaponTags.DisplayName(id, true);

		/// <summary>The preview of <c>rules.Blocks[index]</c>. <paramref name="levies"/> is the number of levies the armory serves, or -1 when unknown (a client).</summary>
		public static BlockStatus Describe(ArmoryRules rules, int index, RuleStock stock, int levies)
		{
			var b = rules.Blocks[index];
			if (!b.On) return new BlockStatus(T("StatusOff"), false);
			switch (b.Kind)
			{
				case RuleKind.Category: return Category(rules, index, b, stock, levies);
				case RuleKind.ItemList: return ItemList(b, stock);
				default: return ClassBalance(rules, index, b, stock, levies);
			}
		}

		private static BlockStatus Category(ArmoryRules rules, int index, RuleBlock b, RuleStock stock, int levies)
		{
			int have = stock.Cat(b.Cat);
			string cat = Plural(b.Cat);
			switch (b.Mode)
			{
				case CatMode.Weight:
					return new BlockStatus(T("StatusWeight", have, cat, b.Weight.ToString("0.##")), false);
				case CatMode.Over:
					if (string.IsNullOrEmpty(b.Other) || b.Other == b.Cat) return new BlockStatus(T("StatusOverSame"), true);
					if (have == 0) return new BlockStatus(T("StatusOverNone", cat, Plural(b.Other)), true);
					return new BlockStatus(WeaponTags.Cap(T("StatusOver", cat, Plural(b.Other), have, stock.Cat(b.Other))), false);
				case CatMode.Include:
					return have == 0 ? new BlockStatus(T("StatusIncludeNone", cat), true) : new BlockStatus(T("StatusInclude", have, cat), false);
				default:
				{
					string text = T("StatusMin", b.Count, cat, have);
					bool warn = have < b.Count;
					string why = Blocked(rules, index, b.Cat, b.Count, stock);
					if (why != null) return new BlockStatus(text + " " + why, true);
					if (levies >= 0 && b.Count > levies) return new BlockStatus(text + " " + T("StatusFewLevies", levies), true);
					return new BlockStatus(text, warn);
				}
			}
		}

		/// <summary>If an earlier enabled block keeps a minimum for <paramref name="cat"/> from being met, why (a sentence); otherwise null.</summary>
		public static string Blocked(ArmoryRules rules, int index, string cat, int want, RuleStock stock)
		{
			for (int i = 0; i < index; i++)
			{
				var e = rules.Blocks[i];
				if (!e.On) continue;
				if (e.Kind == RuleKind.Category && e.Mode == CatMode.Over && e.Other == cat && e.Cat != cat && stock.Cat(e.Cat) > 0)
					return T("BlockedOrder", i + 1, Plural(e.Cat), Plural(cat));
				if (e.Kind == RuleKind.ClassBalance)
				{
					string cls = stock.ClassOfCat(cat);
					var en = cls == null ? null : e.EntryFor(cls, false);
					if (en != null)
					{
						int cap = en.Fixed >= 0 ? en.Fixed : en.Max;
						if (cap >= 0 && cap < want) return T("BlockedClass", i + 1, WeaponTags.DisplayName(cls), cap);
					}
				}
			}
			return null;
		}

		private static BlockStatus ItemList(RuleBlock b, RuleStock stock)
		{
			int n = b.Items.Count;
			if (n == 0) return new BlockStatus(T("StatusListEmpty"), b.List == ListMode.Only);
			return new BlockStatus(b.List == ListMode.Only ? T("StatusOnly", n) : T("StatusNever", n), false);
		}

		/// <summary>How many levies of each class a class balance asks for, for an army of <paramref name="n"/> levies with no armor classes yet. Floors first, then the weights, each limited by the stock. Pure; mirrors the allocator's rules.</summary>
		public static Dictionary<string, int> ClassTargets(RuleBlock b, int n, RuleStock stock)
		{
			var res = new Dictionary<string, int>();
			int left = n;
			var entries = b.Classes.FindAll(e => !e.IsDefault);
			int Hi(ClassEntry e) => e.Fixed >= 0 ? e.Fixed : e.Max >= 0 ? Math.Max(e.Max, Math.Max(0, e.Min)) : n;
			foreach (var e in entries)
			{
				int lo = e.Fixed >= 0 ? e.Fixed : Math.Max(0, e.Min);
				int give = Math.Min(Math.Min(lo, stock.Class(e.Class)), left);
				if (give > 0) { res[e.Class] = give; left -= give; }
			}
			float sum = 0f;
			foreach (var e in entries) if (e.Weight > 0f) sum += e.Weight;
			if (sum > 0f)
			{
				for (int guard = 0; guard < n && left > 0; guard++)
				{
					ClassEntry best = null; float bestDef = 0f;
					foreach (var e in entries)
					{
						if (e.Weight <= 0f) continue;
						int cur = res.TryGetValue(e.Class, out var c) ? c : 0;
						if (cur >= Hi(e) || cur >= stock.Class(e.Class)) continue;
						float def = n * e.Weight / sum - cur;
						if (def > bestDef) { bestDef = def; best = e; }
					}
					if (best == null) break;
					res[best.Class] = (res.TryGetValue(best.Class, out var k) ? k : 0) + 1; left--;
				}
			}
			return res;
		}

		private static BlockStatus ClassBalance(ArmoryRules rules, int index, RuleBlock b, RuleStock stock, int levies)
		{
			bool any = false;
			string shortWhat = null;
			foreach (var e in b.Classes)
			{
				if (e.IsDefault) continue;
				any = true;
				int want = e.Fixed >= 0 ? e.Fixed : e.Min;
				if (want > stock.Class(e.Class) && shortWhat == null) { shortWhat = T("StatusClassShort", want, WeaponTags.DisplayName(e.Class, true), stock.Class(e.Class)); }
			}
			bool shadowed = false;
			for (int i = 0; i < index && !shadowed; i++) if (rules.Blocks[i].On && rules.Blocks[i].Kind == RuleKind.ClassBalance && rules.Blocks[i].Classes.Exists(e => !e.IsDefault)) shadowed = true; // the first block with numbers is the one in use
			if (shadowed) return new BlockStatus(T("StatusClassShadowed"), true);
			if (!any) return new BlockStatus(T("StatusClassFree"), false);
			if (shortWhat != null) return new BlockStatus(shortWhat, true);
			if (levies >= 0)
			{
				var t = ClassTargets(b, levies, stock);
				var parts = new List<string>();
				foreach (var cls in WeaponTags.ClassKeys) if (t.TryGetValue(cls, out var k) && k > 0) parts.Add(k + " " + WeaponTags.DisplayName(cls, true));
				if (parts.Count > 0) return new BlockStatus(T("StatusClassPlan", levies, string.Join(", ", parts)), false);
			}
			return new BlockStatus(T("StatusClassSet"), false);
		}
	}
}
