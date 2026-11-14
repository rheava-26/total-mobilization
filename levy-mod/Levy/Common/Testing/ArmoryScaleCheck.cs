using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Logistics;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The big-scene cost harness (env LEVY_PERFSCENE=1; LEVY_PERFSCENE_SLOTS, default 3000, and LEVY_PERFSCENE_LEVIES, default 40, size it). After recipes are final it fills a real, unplaced Armory with a
	/// mix of every issuable weapon, armor piece, accessory, ammo and potion the loaded mods have (Calamity and Thorium items included when they are loaded; about 25 copies of one weapon), serves a crowd of levies in
	/// every damage class from it, and times what the army asks of the armory: the first issue, the periodic upgrade pass per levy (the per-levy cost the 5-second upgrade tick pays), the army-wide primary plan, a drone
	/// loan, the 30-tick stamp pass, the sell-page selection and price, and the armory panel's total. One line per measure goes to the log as "[PerfScene]" with ms per call, so before and after a change can be compared
	/// on the same machine and mod list. Off by default: costs nothing and changes nothing.
	/// </summary>
	public class ArmoryScaleCheck : ModSystem
	{
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		public override void PostSetupRecipes()
		{
			if (Environment.GetEnvironmentVariable("LEVY_PERFSCENE") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private static int EnvInt(string name, int def) => int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : def;

		private struct Stat { public int N; public double Sum, Max; public List<double> All; }

		private static void Run()
		{
			int slots = EnvInt("LEVY_PERFSCENE_SLOTS", 3000), levies = EnvInt("LEVY_PERFSCENE_LEVIES", 40);
			var log = Levy.Instance?.Logger;
			var times = new Dictionary<string, Stat>();
			double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
			void Add(string k, double ms)
			{
				times.TryGetValue(k, out var s); s.All ??= new List<double>();
				s.N++; s.Sum += ms; if (ms > s.Max) s.Max = ms; s.All.Add(ms); times[k] = s;
			}
			var savedTestServed = ArmoryTE.TestServed; bool profWas = LevyProf.On; Action undoNpc = null, undoPlayer = null;
			try
			{
				try { ProgressionTier.EnsureBuilt(m => log?.Info("[PerfScene] " + m)); } catch (Exception) { }
				LevyProf.On = true; LevyProf.Reset();

				// ---- the stock: every kind of thing an armory holds, shuffled, from whatever mods are loaded
				var te = SellFreezeRepro.BuildStock(slots, out int filled, out string kinds);
				Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = Math.Min(stack, Math.Max(1, it.maxStack)); return it; }
				undoNpc = SellFreezeRepro.PlaceTown(EnvInt("LEVY_SELLREPRO_KEEPERS", 16), te.Center); undoPlayer = SellFreezeRepro.EnsurePayer(te.Center);
				log?.Info($"[PerfScene] stock: {filled}/{slots} slots ({kinds}); {Shopping.Keepers(te).Count} shopkeepers in reach");

				// ---- the crowd: levies of every grade in armor of every class, deterministic
				int[][] sets =
				{
					new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings }, new int[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings },
					new int[] { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings }, new int[] { ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings },
					new int[] { ItemID.SpiderMask, ItemID.SpiderBreastplate, ItemID.SpiderGreaves }, new int[] { 0, 0, 0 },
				};
				var crew = new List<SoldierRecord>();
				var grades = Enum.GetValues<Grade>();
				for (int i = 0; i < levies; i++)
				{
					var r = new SoldierRecord { Id = 1000 + i, Name = "Pvt. Scale " + i, Grade = grades[i % grades.Length] };
					var set = sets[i % sets.Length];
					if (set[0] != 0) { r.Gear.Head = Make(set[0]); r.Gear.Body = Make(set[1]); r.Gear.Legs = Make(set[2]); }
					crew.Add(r);
				}
				ArmoryTE.TestServed = crew;

				long t0 = Stopwatch.GetTimestamp();
				foreach (var r in crew) { long t = Stopwatch.GetTimestamp(); te.IssueGear(r); Add("IssueGear (first kit, per levy)", Ms(t)); }
				double issueAll = Ms(t0);
				int armed = crew.Count(r => r.Gear.HasWeapon);

				for (int round = 0; round < 3; round++)
					foreach (var r in crew) { long t = Stopwatch.GetTimestamp(); te.UpgradeWeapons(r); Add("UpgradeWeapons (one levy's 5 s pass)", Ms(t)); }

				for (int i = 0; i < 20; i++) { long t = Stopwatch.GetTimestamp(); te.StampNew(); Add("StampNew (every 30 ticks, per armory)", Ms(t)); }
				for (int i = 0; i < 10; i++) { long t = Stopwatch.GetTimestamp(); te.TotalSellValue(); Add("TotalSellValue (armory panel, per call)", Ms(t)); }
				for (int i = 0; i < 10; i++) { long t = Stopwatch.GetTimestamp(); te.LendDroneWeapon(crew.Count); Add("LendDroneWeapon (one drone launch)", Ms(t)); }
				var tiers = Selling.Tiers;
				for (int i = 0; i < 10; i++)
				{
					long t = Stopwatch.GetTimestamp();
					var pick = Selling.Select(te, new Selling.Filter { TierLo = tiers[i % tiers.Length].Lo, TierHi = tiers[i % tiers.Length].Hi });
					Add("Select matching (sell page, tier filter)", Ms(t));
					t = Stopwatch.GetTimestamp();
					Selling.BestBuyer(te, pick);
					Add("sell summary (BestBuyer over every shopkeeper)", Ms(t));
				}

				// ---- the table, slowest per call first, and the same through the profiler buckets
				var sb = new StringBuilder();
				sb.Append($"[PerfScene] {slots} slots, {filled} items, {levies} levies ({armed} armed after issue; issuing all took {issueAll:F1} ms)\n");
				sb.Append("[PerfScene]   measure                                       calls  steady ms  median ms     p95 ms     max ms   first ms\n");
				foreach (var kv in times.OrderByDescending(k => k.Value.All.Skip(1).DefaultIfEmpty(0).Average()))
				{
					var s = kv.Value; var sorted = s.All.OrderBy(x => x).ToList();
					double p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))], med = sorted[sorted.Count / 2], steady = s.All.Skip(1).DefaultIfEmpty(s.All[0]).Average();
					sb.Append($"[PerfScene]   {kv.Key,-46}{s.N,5} {steady,10:F3} {med,10:F3} {p95,10:F3} {s.Max,10:F3} {s.All[0],10:F3}\n");
				}
				double perTick = times.TryGetValue("UpgradeWeapons (one levy's 5 s pass)", out var up) ? up.All.Skip(levies).DefaultIfEmpty(up.Sum / up.N).Average() * levies / (LevyConstants.AutoUpgradeCheckIntervalTicks) : 0;
				sb.Append($"[PerfScene] model: {levies} levies each upgrading every {LevyConstants.AutoUpgradeCheckIntervalTicks} ticks costs {perTick:F4} ms per tick on average (a tick is 16.7 ms)\n");
				sb.Append("[PerfScene] profiler arm.* buckets, total ms (calls) over the whole run:");
				foreach (var (nm, k) in new[] { ("upgrade", LevyProf.ArmUp), ("alloc", LevyProf.ArmAlloc), ("build", LevyProf.ArmBuild), ("plan", LevyProf.ArmPlan), ("carry", LevyProf.ArmCarry), ("floor", LevyProf.ArmFloor), ("armor", LevyProf.ArmArmor), ("acc", LevyProf.ArmAcc), ("side", LevyProf.ArmSide), ("issue", LevyProf.ArmIssue), ("lend", LevyProf.ArmLend), ("tick", LevyProf.ArmTick) })
					sb.Append($" {nm}={LevyProf.Get(k) * 1000.0 / Stopwatch.Frequency:F1}({LevyProf.Cnt[k]})");
				sb.Append('\n');
				log?.Info(sb.ToString().TrimEnd());
			}
			catch (Exception e) { log?.Warn("[PerfScene] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim()))); }
			finally { undoPlayer?.Invoke(); undoNpc?.Invoke(); ArmoryTE.TestServed = savedTestServed; LevyProf.On = profWas; LevyProf.Reset(); ProgressionTier.Reset(); }
		}
	}
}
