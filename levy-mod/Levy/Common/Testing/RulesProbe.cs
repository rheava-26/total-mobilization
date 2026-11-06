using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Rule-list directives for the pick probe's scenario files (TEST-ONLY, used by <see cref="WeaponChoiceProbe"/>):
	///   RULES clear                              start from an empty rule list (the default list is one untouched class balance block)
	///   RCLASS melee=w3,min1 ranged=max4 magic=fix2 ...   a class balance block (w weight, min, max, fix)
	///   RWEIGHT bow 2                            a category weight (soft)
	///   ROVER bow gun                            bows are always chosen over guns (hard order)
	///   RINCLUDE bow                             every bow is put to use
	///   RMIN bow 7                               at least 7 levies carry a bow
	///   RDENY Vanilla/Minishark   RONLY Vanilla/Musket    item lists (several items per line)
	///   OVERRIDE Vanilla/WoodenSword prefer|never|set 400|clear
	///   TAG Vanilla/Musket class melee|none|auto   TAG Vanilla/Minishark +bow -gun
	///   RPREVIEW                                 print each block's preview line
	/// </summary>
	internal static class RulesProbe
	{
		/// <summary>The number of soldiers the scenario will issue (the preview line needs it before they exist).</summary>
		public static int Levies = -1;

		public static bool Apply(ArmoryTE te, string[] t, StringBuilder sb)
		{
			var r = te.Rules;
			switch (t[0])
			{
				case "RULES": if (t.Length > 1 && t[1] == "clear") { r.Blocks.Clear(); r.Overrides.Clear(); } r.Changed(); return true;
				case "RCLASS":
				{
					var b = r.AddBlock(RuleKind.ClassBalance);
					foreach (var tok in t.Skip(1))
					{
						var kv = tok.Split('=');
						var e = b.EntryFor(kv[0], true);
						foreach (var f in kv[1].Split(','))
						{
							if (f.StartsWith("w")) e.Weight = float.Parse(f.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
							else if (f.StartsWith("min")) e.Min = int.Parse(f.Substring(3));
							else if (f.StartsWith("max")) e.Max = int.Parse(f.Substring(3));
							else if (f.StartsWith("fix")) e.Fixed = int.Parse(f.Substring(3));
						}
					}
					r.Changed(); return true;
				}
				case "RWEIGHT": { var b = r.AddBlock(RuleKind.Category); b.Mode = CatMode.Weight; b.Cat = t[1]; b.Weight = float.Parse(t[2], System.Globalization.CultureInfo.InvariantCulture); r.Changed(); return true; }
				case "ROVER": { var b = r.AddBlock(RuleKind.Category); b.Mode = CatMode.Over; b.Cat = t[1]; b.Other = t[2]; r.Changed(); return true; }
				case "RINCLUDE": { var b = r.AddBlock(RuleKind.Category); b.Mode = CatMode.Include; b.Cat = t[1]; r.Changed(); return true; }
				case "RMIN": { var b = r.AddBlock(RuleKind.Category); b.Mode = CatMode.Min; b.Cat = t[1]; b.Count = int.Parse(t[2]); r.Changed(); return true; }
				case "RDENY": case "RONLY":
				{
					var b = r.AddBlock(RuleKind.ItemList); b.List = t[0] == "RDENY" ? ListMode.Never : ListMode.Only;
					foreach (var spec in t.Skip(1)) { var it = WeaponChoiceProbe.MakeItem(spec); if (it == null) { sb.AppendLine("  !! cannot resolve " + spec); continue; } b.Items.Add(ArmoryRules.KeyOf(it.type)); }
					r.Changed(); return true;
				}
				case "OVERRIDE":
				{
					var it = WeaponChoiceProbe.MakeItem(t[1]); if (it == null) { sb.AppendLine("  !! cannot resolve " + t[1]); return true; }
					var o = r.OverrideFor(it.type, true);
					switch (t[2])
					{
						case "prefer": o.Rank = RankOverrideMode.Prefer; break;
						case "never": o.Rank = RankOverrideMode.Never; break;
						case "set": o.Rank = RankOverrideMode.Set; o.Value = float.Parse(t[3], System.Globalization.CultureInfo.InvariantCulture); break;
						default: o.Rank = RankOverrideMode.Auto; break;
					}
					r.Prune(); r.Changed(); return true;
				}
				case "TAG":
				{
					var it = WeaponChoiceProbe.MakeItem(t[1]); if (it == null) { sb.AppendLine("  !! cannot resolve " + t[1]); return true; }
					var o = r.OverrideFor(it.type, true);
					if (t[2] == "class") o.Class = t[3] == "auto" ? null : t[3];
					else foreach (var tok in t.Skip(2))
					{
						string id = tok.Substring(1);
						if (tok[0] == '+') { o.CatRemove.Remove(id); if (WeaponTags.DerivedCategory(it) != id) o.CatAdd.Add(id); }
						else if (tok[0] == '-') { if (!o.CatAdd.Remove(id)) o.CatRemove.Add(id); }
					}
					r.Prune(); r.Changed(); return true;
				}
				case "RPREVIEW": Preview(sb, te); return true;
			}
			return false;
		}

		/// <summary>Prints the rule list, the preview line of each block and the stock counts.</summary>
		public static void Preview(StringBuilder sb, ArmoryTE te)
		{
			var stock = te.CountStock();
			int n = Levies >= 0 ? Levies : te.ServedCount;
			sb.AppendLine($"  -- rules ({te.Rules.Blocks.Count} blocks, {te.Rules.Overrides.Count} item overrides, {n} levies served)");
			for (int i = 0; i < te.Rules.Blocks.Count; i++)
			{
				var b = te.Rules.Blocks[i];
				var s = RuleStatus.Describe(te.Rules, i, stock, n);
				string what = b.Kind == RuleKind.Category ? $"{b.Mode} {b.Cat}{(b.Mode == CatMode.Over ? " > " + b.Other : b.Mode == CatMode.Min ? " " + b.Count : b.Mode == CatMode.Weight ? " x" + b.Weight : "")}"
					: b.Kind == RuleKind.ItemList ? $"{b.List} {b.Items.Count} items"
					: "class " + string.Join(" ", b.Classes.Where(e => !e.IsDefault).Select(e => $"{e.Class}[w{e.Weight} min{e.Min} max{e.Max} fix{e.Fixed}]"));
				sb.AppendLine($"     {i + 1}. {(b.On ? "" : "(off) ")}{what}  =>  {s.Text}{(s.Warn ? "  [WARN]" : "")}");
			}
			sb.AppendLine("     stock by category: " + string.Join(", ", stock.Cats.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
			sb.AppendLine("     stock by class: " + string.Join(", ", stock.Classes.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)));
		}

		/// <summary>What the soldiers ended up with, tallied by category and class under the rules.</summary>
		public static void Summarize(StringBuilder sb, ArmoryTE te, List<SoldierRecord> souls)
		{
			if (te.Rules.IsNeutral && te.Rules.Blocks.Count == 1) return;
			using var _rs = ArmoryRules.Enter(te.Rules);
			var cats = new Dictionary<string, int>(); var cls = new Dictionary<string, int>();
			foreach (var s in souls)
			{
				var w = s.Gear.Weapon;
				if (w == null || w.IsAir) { cats["(bare)"] = (cats.TryGetValue("(bare)", out var z) ? z : 0) + 1; continue; }
				string c = string.Join("+", te.Rules.CatsOf(w)); if (c.Length == 0) c = "-";
				cats[c] = (cats.TryGetValue(c, out var a) ? a : 0) + 1;
				string k = te.Rules.ClassKeyOf(w) ?? "none";
				cls[k] = (cls.TryGetValue(k, out var d) ? d : 0) + 1;
			}
			sb.AppendLine("  by category: " + string.Join(", ", cats.OrderByDescending(x => x.Value).Select(x => x.Key + " x" + x.Value)));
			sb.AppendLine("  by class:    " + string.Join(", ", cls.OrderByDescending(x => x.Value).Select(x => x.Key + " x" + x.Value)));
		}
	}
}
