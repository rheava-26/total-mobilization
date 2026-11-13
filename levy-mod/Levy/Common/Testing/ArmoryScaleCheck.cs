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
			var savedTestServed = ArmoryTE.TestServed; bool profWas = LevyProf.On;
			try
			{
				try { ProgressionTier.EnsureBuilt(m => log?.Info("[PerfScene] " + m)); } catch (Exception) { }
				LevyProf.On = true; LevyProf.Reset();

				// ---- the stock: every kind of thing an armory holds, shuffled, from whatever mods are loaded
				var weapons = new List<int>(); var armor = new List<int>(); var acc = new List<int>(); var ammo = new List<int>(); var potions = new List<int>();
				foreach (var kv in ContentSamples.ItemsByType)
				{
					var it = kv.Value; if (it == null || it.IsAir || it.type <= 0) continue;
					if (it.damage > 0 && it.maxStack == 1 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && WeaponClassifier.IsIssuableWeapon(it)) weapons.Add(it.type);
					else if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) { if (!it.vanity) armor.Add(it.type); }
					else if (WeaponClassifier.IsUsefulAccessory(it)) acc.Add(it.type);
					else if (it.ammo > 0 && it.damage > 0) ammo.Add(it.type);
					else if (it.consumable && (it.healLife > 0 || it.healMana > 0 || it.buffType > 0)) potions.Add(it.type);
				}
				var rnd = new Random(20261113);
				void Shuffle(List<int> l) { for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); } }
				Shuffle(weapons); Shuffle(armor); Shuffle(acc); Shuffle(ammo); Shuffle(potions);
				var te = new ArmoryTE(); te.Storage = new Item[slots];
				for (int i = 0; i < slots; i++) te.Storage[i] = new Item();
				Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = Math.Min(stack, Math.Max(1, it.maxStack)); return it; }
				int w = 0, a = 0, c = 0, m = 0, p = 0, filled = 0;
				for (int i = 0; i < slots; i++)
				{
					int roll = i % 10; // 40% weapons, 20% armor, 20% accessories, 10% ammo and potions, 10% empty
					int type = 0, stack = 1;
					if (roll < 4 && weapons.Count > 0) type = i < 25 * 10 && i % 10 == 0 ? weapons[0] : weapons[w++ % weapons.Count]; // about 25 copies of the first weapon
					else if (roll < 6 && armor.Count > 0) type = armor[a++ % armor.Count];
					else if (roll < 8 && acc.Count > 0) type = acc[c++ % acc.Count];
					else if (roll == 8 && ammo.Count > 0) { type = ammo[m++ % ammo.Count]; stack = 999; }
					else if (roll == 8 && potions.Count > 0) { type = potions[p++ % potions.Count]; stack = 30; }
					if (type == 0) continue;
					te.Storage[i] = Make(type, stack); filled++;
				}
				log?.Info($"[PerfScene] stock: {filled}/{slots} slots ({weapons.Count} weapon kinds, {armor.Count} armor, {acc.Count} accessories, {ammo.Count} ammo, {potions.Count} potions in the loaded mods)");

				// ---- the crowd: levies of every grade in armor of every class, deterministic
				int[][] sets =
				{
					new[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings }, new[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings },
					new[] { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings }, new[] { ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings },
					new[] { ItemID.SpiderMask, ItemID.SpiderBreastplate, ItemID.SpiderGreaves }, new[] { 0, 0, 0 },
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
					var price = Selling.Pricer(Main.LocalPlayer, null); long total = 0; foreach (int s in pick) total += price(te.Storage[s]);
					Add("price the selection (one shopkeeper)", Ms(t));
				}

				// ---- the table, slowest per call first, and the same through the profiler buckets
				var sb = new StringBuilder();
				sb.Append($"[PerfScene] {slots} slots, {filled} items, {levies} levies ({armed} armed after issue; issuing all took {issueAll:F1} ms)\n");
				sb.Append("[PerfScene]   measure                                       calls   mean ms     p95 ms     max ms\n");
				foreach (var kv in times.OrderByDescending(k => k.Value.Sum / k.Value.N))
				{
					var s = kv.Value; var sorted = s.All.OrderBy(x => x).ToList();
					double p95 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
					sb.Append($"[PerfScene]   {kv.Key,-46}{s.N,5} {s.Sum / s.N,10:F3} {p95,10:F3} {s.Max,10:F3}\n");
				}
				double perTick = times.TryGetValue("UpgradeWeapons (one levy's 5 s pass)", out var up) ? up.Sum / up.N * levies / (LevyConstants.AutoUpgradeCheckIntervalTicks) : 0;
				sb.Append($"[PerfScene] model: {levies} levies each upgrading every {LevyConstants.AutoUpgradeCheckIntervalTicks} ticks costs {perTick:F4} ms per tick on average (a tick is 16.7 ms)\n");
				sb.Append("[PerfScene] profiler buckets (total ms over the run): alloc=" + (LevyProf.Get(LevyProf.ArmAlloc) * 1000.0 / Stopwatch.Frequency).ToString("F1", Inv) + " upgrade=" + (LevyProf.Get(LevyProf.ArmUp) * 1000.0 / Stopwatch.Frequency).ToString("F1", Inv)
					+ " lend=" + (LevyProf.Get(LevyProf.ArmLend) * 1000.0 / Stopwatch.Frequency).ToString("F1", Inv) + " armoryTick=" + (LevyProf.Get(LevyProf.ArmTick) * 1000.0 / Stopwatch.Frequency).ToString("F1", Inv));
				log?.Info(sb.ToString().TrimEnd());
			}
			catch (Exception e) { log?.Warn("[PerfScene] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim()))); }
			finally { ArmoryTE.TestServed = savedTestServed; LevyProf.On = profWas; LevyProf.Reset(); ProgressionTier.Reset(); }
		}
	}
}
