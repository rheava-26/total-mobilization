using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST-ONLY headless probe of targeted reforging (changes no game behaviour; does nothing unless LEVY_REFORGEPROBE is set). On a fixed hoard (see <see cref="ReforgeFixture"/>) it runs every target mode,
	/// a named-prefix goal and a spending cap, and for each prints the preview, which weapons were touched (and how their modifier changed), which were left alone, and the money spent.
	/// Output goes to LEVY_REFORGEPROBE_OUT (default reforgeprobe.txt). Runs during content setup: no world needed.
	/// </summary>
	public class ReforgeProbe : ModSystem
	{
		private const long Treasury = 2_000_000;

		public override void PostSetupRecipes()
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_REFORGEPROBE"))) return;
			var sb = new StringBuilder();
			var rand = Main.rand;
			try { Run(sb); }
			catch (Exception e) { sb.AppendLine("PROBE EXCEPTION: " + e); }
			finally { Main.rand = rand; ArmoryTE.TestServed = null; LevyClock.Force(null); }
			string outp = Environment.GetEnvironmentVariable("LEVY_REFORGEPROBE_OUT") ?? "reforgeprobe.txt";
			File.WriteAllText(outp, sb.ToString());
			Mod.Logger.Info("REFORGEPROBE: done -> " + outp);
		}

		private static Dictionary<string, string> Snapshot(ArmoryTE a)
		{
			var d = new Dictionary<string, string>();
			foreach (var c in a.ReforgeCandidates()) d[ReforgeFixture.Label(c)] = c.Item.prefix == 0 ? "-" : ArmoryTE.PrefixName(c.Item.prefix);
			return d;
		}

		private static void Scenario(StringBuilder sb, string title, ReforgePlan plan, int seed, long treasury = Treasury, bool legacy = false)
		{
			var f = ReforgeFixture.Build(treasury);
			try
			{
				sb.AppendLine($"== {title}");
				var before = Snapshot(f.Armory);
				var labelOrder = new List<string>(); foreach (var c in f.Armory.ReforgeCandidates()) labelOrder.Add(ReforgeFixture.Label(c));
				if (!legacy)
				{
					var pv = f.Armory.PreviewReforge(plan, f.Payer);
					sb.AppendLine($"  preview: {pv.Weapons} weapons, about {LevyUtil.Coins(pv.EstCost)}{(pv.CapApplied ? " (cut to the cap)" : "")}  [selected {pv.Selected}, already there {pv.AlreadyMet}, cannot take it {pv.NotApplicable}]");
				}
				ReforgeFixture.Seed(seed);
				ArmoryTE.ReforgeResult res;
				if (legacy) res = f.Armory.ReforgeToFloor(f.Payer, plan.FloorIndex);
				else res = f.Armory.ReforgeTargeted(f.Payer, plan);
				var after = Snapshot(f.Armory);
				var touched = new HashSet<string>();
				int changed = 0;
				foreach (var lbl in labelOrder) if (before[lbl] != after[lbl]) { changed++; touched.Add(lbl); }
				sb.AppendLine($"  run: {res.Weapons} weapons rolled, {res.Rolls} rolls, {res.Reached} reached the goal, {res.Short} left short{(res.CapHit ? ", STOPPED AT THE CAP" : "")}{(res.RanDry ? ", TREASURY RAN DRY" : "")}");
				sb.AppendLine($"  money: spent {res.Spent} copper ({LevyUtil.Coins(res.Spent)}); treasury {LevyUtil.Coins(treasury)} -> {LevyUtil.Coins(f.Payer.Treasury)}");
				sb.AppendLine($"  modifier changed on {changed} of {labelOrder.Count} weapons:");
				foreach (var lbl in labelOrder) if (touched.Contains(lbl)) sb.AppendLine($"    + {lbl,-34} {before[lbl]} -> {after[lbl]}");
				sb.Append("  left alone: ");
				var rest = new List<string>(); foreach (var lbl in labelOrder) if (!touched.Contains(lbl)) rest.Add(lbl);
				sb.AppendLine(rest.Count == 0 ? "(none)" : string.Join("; ", rest));
				if (res.Log != null && res.Log.Count > 0) { sb.AppendLine("  roll log (name|region|rolls|outcome|copper):"); foreach (var l in res.Log) sb.AppendLine("    " + l); }
				sb.AppendLine();
			}
			finally { f.Release(); }
		}

		private void Run(StringBuilder sb)
		{
			sb.AppendLine("# Levy targeted-reforge probe. Levy " + Mod.Version);
			sb.AppendLine($"# Fixture: 10 storage slots (8 weapons at ages 1h-20d, one at the best modifier, a Torch, a favourited sword), 2 rack weapons, 4 levies (4 primaries, 1 sidearm). Treasury {LevyUtil.Coins(Treasury)}. Floor = Great unless stated.");
			sb.AppendLine("# Labels: storeN / rackN = slot, hand#id / side#id = a levy's primary / sidearm.");
			sb.AppendLine();
			Scenario(sb, "In hand (levies' weapons only), floor Great", new ReforgePlan { Mode = ReforgeMode.Issued, FloorIndex = 1 }, 11);
			Scenario(sb, "Recent: added within 1 day, floor Great", new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 1, FloorIndex = 1 }, 12);
			Scenario(sb, "Recent: added within 7 days, floor Great", new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 7, FloorIndex = 1 }, 13);
			var picked = new ReforgePlan { Mode = ReforgeMode.Picked, FloorIndex = 1 }; picked.TogglePick(0, 2); picked.TogglePick(0, 6); picked.TogglePick(1, 1);
			Scenario(sb, "Picked by hand: store2, store6, rack1, floor Great", picked, 14);
			Scenario(sb, "By group: class ranged, floor Great", new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "ranged", FloorIndex = 1 }, 15);
			Scenario(sb, "By group: type bow, named modifier Rapid", new ReforgePlan { Mode = ReforgeMode.Group, Category = "bow", Goal = ReforgeGoal.Prefix, PrefixId = PrefixID.Rapid }, 16);
			Scenario(sb, "Recent 7 days narrowed to class ranged, floor Superb", new ReforgePlan { Mode = ReforgeMode.Recent, RecentDays = 7, ClassKey = "ranged", FloorIndex = 2 }, 17);
			Scenario(sb, "By group: class melee, named modifier Legendary, spending cap 5 gold", new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "melee", Goal = ReforgeGoal.Prefix, PrefixId = PrefixID.Legendary, CapCopper = 50_000 }, 18);
			Scenario(sb, "By group: everything (no class, no type), floor Great", new ReforgePlan { Mode = ReforgeMode.Group, FloorIndex = 1 }, 19);
			Scenario(sb, "By group: melee, named modifier Unreal (no sword can take it)", new ReforgePlan { Mode = ReforgeMode.Group, ClassKey = "melee", Goal = ReforgeGoal.Prefix, PrefixId = PrefixID.Unreal }, 20);
			Scenario(sb, "OLD reforge-to-floor (Great), for comparison: storage and rack only", new ReforgePlan { FloorIndex = 1 }, 21, Treasury, legacy: true);
		}
	}
}
