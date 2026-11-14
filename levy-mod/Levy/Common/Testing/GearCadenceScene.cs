using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The gear-cadence half of the big-scene harness (runs at the end of <see cref="ArmoryScaleCheck"/>, env LEVY_PERFSCENE=1; LEVY_PERFSCENE_SEEDS, default 3, is how many random timer seeds it tries). It plays a FRESH army
	/// (the scene's crowd, 3000-slot stock) through the real levy timers on a virtual clock: every levy is issued at once, then each one re-checks its own gear when his own timer is due
	/// (<see cref="ArmoryTE.GearCheckFirstDelay"/>, <see cref="ArmoryTE.GearCheckDelay"/>, the swap cooldown), exactly as WeaponUse.WeaponUpgradeTick does, until nobody has changed anything for three minutes of game time.
	/// It reports the real cost (ms per tick, worst single call, worst tick), how long game time took to reach the last change, how many items changed hands (and whether any went back to an earlier holder: a ping-pong),
	/// and the final kits as score sums (weapons, sidearms, armor, accessories) with class-fit and empty-slot counts, so a change to the gear cadence can be compared with the old plan on the same stock.
	/// </summary>
	internal static class GearCadenceScene
	{
		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
		private const uint Start = 100000;
		private static readonly System.Reflection.FieldInfo TickField = typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		internal static void SetTick(uint v) => TickField.SetValue(null, v);

		private static DamageClass AccClass(SoldierRecord r)
		{
			var g = r.Gear;
			DamageClass cls = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null;
			return cls ?? ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
		}

		private static long Hash(List<SoldierRecord> army)
		{
			unchecked
			{
				long h = 17;
				void M(Item it) { h = h * 31 + (it == null || it.IsAir ? 0 : it.type * 7919L + it.prefix * 131L + RuntimeHelpers.GetHashCode(it)); }
				foreach (var r in army)
				{
					var g = r.Gear; M(g.Weapon); M(g.Sidearm); M(g.Head); M(g.Body); M(g.Legs); M(g.Ammo);
					foreach (var x in g.ExtraSidearms) M(x);
					foreach (var x in g.Accessories) M(x);
				}
				return h;
			}
		}

		internal static readonly List<string> TraceLog = new(), FinalDump = new();
		private static readonly int[] BucketIds = { LevyProf.ArmAlloc, LevyProf.ArmBuild, LevyProf.ArmPlan, LevyProf.ArmCarry, LevyProf.ArmArmor, LevyProf.ArmAcc, LevyProf.ArmSide, LevyProf.ArmIssue, LevyProf.IssPrimary, LevyProf.IssArmorSet, LevyProf.IssAcc, LevyProf.IssSide, LevyProf.AsScan, LevyProf.AsTeam };
		private static readonly string[] BucketNames = { "alloc", "build", "plan", "carry", "armor", "acc", "side", "issue", "iss.primary", "iss.armorset", "iss.acc", "iss.side", "as.scan", "as.team" };
		private static readonly int TraceLevy = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFSCENE_TRACE_LEVY"), out int tl) ? tl : 0;
		private static readonly bool Trace = Environment.GetEnvironmentVariable("LEVY_PERFSCENE_TRACE") == "1";
		private static string N(Item it) => it == null || it.IsAir ? "-" : it.Name + (it.prefix > 0 ? "[" + it.prefix + "]" : "") + "#" + (RuntimeHelpers.GetHashCode(it) & 0xFFF).ToString("x");
		private static string[] Slots(SoldierRecord r)
		{
			var g = r.Gear; var l = new List<string> { "W:" + N(g.Weapon), "S:" + N(g.Sidearm), "H:" + N(g.Head), "B:" + N(g.Body), "L:" + N(g.Legs), "Am:" + N(g.Ammo) };
			for (int i = 0; i < g.ExtraSidearms.Length; i++) l.Add("S" + (i + 2) + ":" + N(g.ExtraSidearms[i]));
			for (int i = 0; i < g.Accessories.Length; i++) l.Add("A" + i + ":" + N(g.Accessories[i]));
			return l.ToArray();
		}

		private struct Kit { public double Weapons, Sidearms, Armor, Acc; public int NoWeapon, ClassMiss, EmptyAcc, AccSlots; public double Total => Weapons + Sidearms + Armor + Acc; }

		private static Kit Score(List<SoldierRecord> army)
		{
			var k = new Kit();
			foreach (var r in army)
			{
				var g = r.Gear;
				if (g.HasWeapon) k.Weapons += WeaponClassifier.Score(g.Weapon); else k.NoWeapon++;
				if (!g.Sidearm.IsAir) k.Sidearms += WeaponClassifier.Score(g.Sidearm);
				foreach (var x in g.ExtraSidearms) if (x != null && !x.IsAir) k.Sidearms += WeaponClassifier.Score(x);
				if (!g.Head.IsAir) k.Armor += ArmoryTE.ArmorScoreOf(g.Head);
				if (!g.Body.IsAir) k.Armor += ArmoryTE.ArmorScoreOf(g.Body);
				if (!g.Legs.IsAir) k.Armor += ArmoryTE.ArmorScoreOf(g.Legs);
				var cls = AccClass(r); int n = Math.Clamp(r.AccessorySlots, 0, g.Accessories.Length); k.AccSlots += n;
				for (int i = 0; i < n; i++) { var a = g.Accessories[i]; if (a == null || a.IsAir) k.EmptyAcc++; else k.Acc += ArmoryTE.AccessoryScoreOf(a, cls); }
				var wc = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null; var pc = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
				if (wc != null && pc != null && pc != wc) k.ClassMiss++;
			}
			return k;
		}

		private sealed class Result
		{
			public string Name; public double RealMs, WorstCallMs, WorstTickMs, WorstSweepMs; public int Calls, Ticks, Swaps, Handoffs, PingPong, MultiHolder, Dropped; public double LastChangeSec, DropSettleSec, DropAtSec; public Kit AtDrop; public long AllocBytes; public double WorstNoGcAt; public string WorstNoGcBuckets; public double WorstCallAt, WorstNoGcMs; public int GcCalls; public string WorstCallBuckets, WorstGc; public Kit Final; public Kit Initial;
			public List<(int sec, double total)> Series = new();
			public List<double> TickMs = new();
		}

		/// <summary>Lifts the best armor, accessories and weapons out of the stock (they arrive later as a drop, as a boss kill would add them).</summary>
		private static List<Item> TakeDrop(ArmoryTE te)
		{
			var drop = new List<Item>();
			void Top(Func<Item, bool> is_, Func<Item, float> score, int n)
			{
				var idx = new List<(int i, float s)>();
				for (int i = 0; i < te.Storage.Length; i++) { var it = te.Storage[i]; if (it != null && !it.IsAir && is_(it)) idx.Add((i, score(it))); }
				idx.Sort((a, b) => b.s.CompareTo(a.s));
				for (int k = 0; k < n && k < idx.Count; k++) { drop.Add(te.Storage[idx[k].i]); te.Storage[idx[k].i] = new Item(); }
			}
			Top(it => (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) && !it.vanity, ArmoryTE.ArmorScoreOf, 45);
			Top(it => WeaponClassifier.IsUsefulAccessory(it), it => ArmoryTE.AccessoryScoreOf(it, null), 40);
			Top(it => it.damage > 0 && it.maxStack == 1 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && WeaponClassifier.IsIssuableWeapon(it), WeaponClassifier.Score, 30);
			return drop;
		}

		private static Result Play(string name, Func<ArmoryTE> stock, Func<List<SoldierRecord>> crew, int seed, int maxSeconds)
		{
			var res = new Result { Name = name };
			var te = stock(); var army = crew();
			var drop = TakeDrop(te); res.Dropped = drop.Count; const int dropAtSec = 240;
			var savedRand = Main.rand; var savedTick = Main.GameUpdateCount; var savedServed = ArmoryTE.TestServed;
			var sweep = typeof(ArmoryTE).GetMethod("FloorSweep", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			Action runSweep = sweep == null ? null : (Action)Delegate.CreateDelegate(typeof(Action), te, sweep);
			var swpActive = typeof(ArmoryTE).GetField("_swActive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			try
			{
				Main.rand = new UnifiedRandom(seed); SetTick(Start); ArmoryTE.TestServed = army;
				foreach (var r in army) { r.Gear.UpgradeCheckTick = 0; te.IssueGear(r); }
				res.Initial = Score(army);
				long lastHash = Hash(army); int lastChange = 0;
				// who holds each item (by reference), the holders it has had, in order
				var hist = new Dictionary<Item, List<int>>(ReferenceEqualityComparer.Instance as IEqualityComparer<Item>);
				void Track(int sec)
				{
					var holder = new Dictionary<Item, int>(ReferenceEqualityComparer.Instance as IEqualityComparer<Item>);
					foreach (var r in army)
					{
						var g = r.Gear; void H(Item it) { if (it != null && !it.IsAir) holder[it] = r.Id; }
						H(g.Weapon); H(g.Sidearm); H(g.Head); H(g.Body); H(g.Legs); foreach (var x in g.ExtraSidearms) H(x); foreach (var x in g.Accessories) H(x);
					}
					foreach (var kv in holder)
					{
						if (!hist.TryGetValue(kv.Key, out var l)) hist[kv.Key] = new List<int> { kv.Value };
						else if (l[^1] != kv.Value) { l.Add(kv.Value); res.Handoffs++; }
					}
				}
				Track(0);
				var snap = new Dictionary<int, string[]>(); if (Trace) foreach (var r in army) snap[r.Id] = Slots(r);
				int swapCool = LevyConstants.AutoUpgradeSwapCooldownTicks;
				int ticks = maxSeconds * 60; int quietTicks = 3 * 60 * 60; int dropTick = dropAtSec * 60; int phase1Last = 0; res.DropAtSec = dropAtSec;
				double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
				for (int t = 1; t <= ticks; t++)
				{
					uint now = Start + (uint)t; SetTick(now);
					double tickMs = 0; bool worked = false;
					if (t == dropTick) { phase1Last = lastChange; res.AtDrop = Score(army); foreach (var d in drop) te.Deposit(d); worked = true; }
					// the armory's own tick (an old sweep that did not fit in one slice carries on)
					if (runSweep != null && swpActive != null && (bool)swpActive.GetValue(te)) { long s0 = Stopwatch.GetTimestamp(); runSweep(); double d = Ms(s0); tickMs += d; res.WorstSweepMs = Math.Max(res.WorstSweepMs, d); res.WorstCallMs = Math.Max(res.WorstCallMs, d); worked = true; }
					foreach (var r in army)
					{
						var g = r.Gear;
						if (g.UpgradeCheckTick == 0) { g.UpgradeCheckTick = now + ArmoryTE.GearCheckFirstDelay(r); continue; }
						if (now < g.UpgradeCheckTick) continue;
						g.UpgradeCheckTick = now + ArmoryTE.GearCheckDelay();
						long[] b0 = null; if (BucketIds != null) { b0 = new long[BucketIds.Length]; for (int q = 0; q < b0.Length; q++) b0[q] = LevyProf.Get(BucketIds[q]); }
						long a0 = GC.GetAllocatedBytesForCurrentThread(); int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2); long c0 = Stopwatch.GetTimestamp(); bool up = te.UpgradeWeapons(r); double cm = Ms(c0);
						res.Calls++; tickMs += cm; worked = true; res.AllocBytes += GC.GetAllocatedBytesForCurrentThread() - a0;
						bool hadGc = GC.CollectionCount(0) != g0; if (hadGc) res.GcCalls++;
						else if (cm > res.WorstNoGcMs)
						{
							res.WorstNoGcMs = cm; res.WorstNoGcAt = t / 60.0;
							if (b0 != null) { var sbk = new StringBuilder(); for (int q = 0; q < b0.Length; q++) { double d = (LevyProf.Get(BucketIds[q]) - b0[q]) * 1000.0 / Stopwatch.Frequency; if (d >= 1) sbk.Append(' ').Append(BucketNames[q]).Append('=').Append(d.ToString("F0", Inv)); } res.WorstNoGcBuckets = sbk.ToString(); }
						}
						if (cm > res.WorstCallMs)
						{
							res.WorstCallMs = cm; res.WorstCallAt = t / 60.0; res.WorstGc = (GC.CollectionCount(0) - g0) + "/" + (GC.CollectionCount(1) - g1) + "/" + (GC.CollectionCount(2) - g2);
							if (b0 != null) { var sbk = new StringBuilder(); for (int q = 0; q < b0.Length; q++) { double d = (LevyProf.Get(BucketIds[q]) - b0[q]) * 1000.0 / Stopwatch.Frequency; if (d >= 1) sbk.Append(' ').Append(BucketNames[q]).Append('=').Append(d.ToString("F0", Inv)); } res.WorstCallBuckets = sbk.ToString(); }
						}
						if (up) g.UpgradeCheckTick = now + (uint)swapCool;
					}
					res.RealMs += tickMs; res.WorstTickMs = Math.Max(res.WorstTickMs, tickMs);
					if (worked || t % 60 == 0) res.TickMs.Add(tickMs);
					if (worked)
					{
						long h = Hash(army);
						if (h != lastHash)
						{
							lastHash = h; lastChange = t; res.Swaps++; Track(t / 60);
							if (Trace)
								foreach (var r in army)
								{
									var cur = Slots(r); if (!snap.TryGetValue(r.Id, out var old)) old = new string[cur.Length];
									for (int i = 0; i < cur.Length; i++) if (old[i] != cur[i] && (TraceLevy == 0 || TraceLevy == r.Id)) TraceLog.Add("seed " + seed + " t=" + (t / 60.0).ToString("F1", Inv) + "s levy " + r.Id + " " + cur[i].Split(':')[0] + ": " + old[i].Substring(old[i].IndexOf(':') + 1) + " -> " + cur[i].Substring(cur[i].IndexOf(':') + 1));
									snap[r.Id] = cur;
								}
						}
					}
					if (t % (30 * 60) == 0) res.Series.Add((t / 60, Score(army).Total));
					if (t > dropTick && t - Math.Max(lastChange, dropTick) > quietTicks) { res.Ticks = t; break; }
					res.Ticks = t;
				}
				res.LastChangeSec = phase1Last / 60.0; res.DropSettleSec = Math.Max(0, lastChange - dropTick) / 60.0;
				res.Final = Score(army);
				if (Trace) foreach (var r in army) FinalDump.Add("seed " + seed + " levy " + r.Id + " " + string.Join(" | ", Slots(r)));
				foreach (var kv in hist)
				{
					var l = kv.Value;
					if (Trace && l.Count >= 3 && l.Distinct().Count() < l.Count) FinalDump.Add("seed " + seed + " PINGPONG " + N(kv.Key) + " holders " + string.Join(">", l));
					var distinct = l.Distinct().Count();
					if (l.Count >= 3 && distinct < l.Count) res.PingPong++; // went back to a holder it had before
					if (distinct >= 2) res.MultiHolder++;
				}
				return res;
			}
			finally { Main.rand = savedRand; SetTick(savedTick); ArmoryTE.TestServed = savedServed; }
		}

		internal static void Run(Func<ArmoryTE> stock, Func<List<SoldierRecord>> crew, Action<string> log, int levies)
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFSCENE_SEEDS"), out int s) && s > 0 ? s : 3;
			int maxSec = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFSCENE_CADENCE_SECONDS"), out int ms) && ms > 0 ? ms : 900;
			var results = new List<Result>();
			for (int seed = 1; seed <= seeds; seed++)
				results.Add(Play("seed " + seed, stock, crew, seed * 7717, maxSec));
			var sb = new StringBuilder();
			sb.Append($"[PerfScene] gear cadence: a fresh {levies}-levy army issued at once, then each levy re-checks his own gear on his own timer (first look in 0-{ArmoryTE.GearCheckDelayMax() / 60.0:F0} s, then every {ArmoryTE.GearCheckDelayMin() / 60.0:F0}-{ArmoryTE.GearCheckDelayMax() / 60.0:F0} s, {LevyConstants.AutoUpgradeSwapCooldownTicks / 60} s after a swap); ends after the drop and 3 quiet minutes, or {maxSec} s\n");
			sb.Append($"[PerfScene]   a drop of the {results[0].Dropped} best armor pieces, accessories and weapons is deposited at game second {results[0].DropAtSec:F0}\n");
			sb.Append("[PerfScene]   run      real ms  ms/tick  worst call  worst tick  worst sweep slice  calls  fresh army settled at s  drop settled after s  changes  handoffs  items passed on  ping-pong\n");
			foreach (var r in results)
				sb.Append($"[PerfScene]   {r.Name,-8}{r.RealMs,8:F0}{r.RealMs / Math.Max(1, r.Ticks),9:F4}{r.WorstCallMs,12:F1}{r.WorstTickMs,12:F1}{r.WorstSweepMs,19:F1}{r.Calls,7}{r.LastChangeSec,24:F0}{r.DropSettleSec,22:F0}{r.Swaps,9}{r.Handoffs,10}{r.MultiHolder,17}{r.PingPong,11}\n");
			foreach (var r in results) sb.Append($"[PerfScene]   {r.Name} worst call {r.WorstCallMs:F1} ms (worst with no GC inside it: {r.WorstNoGcMs:F1} ms; {r.GcCalls} of {r.Calls} calls had a GC inside; {r.AllocBytes / 1048576.0 / Math.Max(1, r.Calls) * 1024:F0} KB allocated per call) at game second {r.WorstCallAt:F0} (the worst no-GC call was at second {r.WorstNoGcAt:F0}:{r.WorstNoGcBuckets}), GCs during it (gen0/1/2) {r.WorstGc}, ms by bucket (not exclusive):{r.WorstCallBuckets}\n");
			sb.Append("[PerfScene]   kit score sums (levy count " + levies + "): run, total, weapons, sidearms, armor, accessories, levies without weapon, armor class != weapon class, empty accessory slots of slots\n");
			var f0 = results[0].Initial;
			sb.Append($"[PerfScene]   {"issued",-8}{f0.Total,10:F0}{f0.Weapons,10:F0}{f0.Sidearms,10:F0}{f0.Armor,10:F0}{f0.Acc,10:F0}{f0.NoWeapon,6}{f0.ClassMiss,6}{f0.EmptyAcc,6}/{f0.AccSlots}\n");
			var ad = results[0].AtDrop; sb.Append($"[PerfScene]   {"pre-drop",-8}{ad.Total,10:F0}{ad.Weapons,10:F0}{ad.Sidearms,10:F0}{ad.Armor,10:F0}{ad.Acc,10:F0}{ad.NoWeapon,6}{ad.ClassMiss,6}{ad.EmptyAcc,6}/{ad.AccSlots}  (seed 1, just before the drop)\n");
			foreach (var r in results) { var f = r.Final; sb.Append($"[PerfScene]   {r.Name,-8}{f.Total,10:F0}{f.Weapons,10:F0}{f.Sidearms,10:F0}{f.Armor,10:F0}{f.Acc,10:F0}{f.NoWeapon,6}{f.ClassMiss,6}{f.EmptyAcc,6}/{f.AccSlots}\n"); }
			sb.Append("[PerfScene]   kit total by game second (seed 1):");
			foreach (var (sec, tot) in results[0].Series.Take(16)) sb.Append($" {sec}s={tot:F0}");
			if (Trace) { sb.Append("\n[PerfScene] trace:"); foreach (var l in TraceLog.Skip(Math.Max(0, TraceLog.Count - 260))) sb.Append("\n[PerfScene]   " + l); }
			if (Trace) { sb.Append("\n[PerfScene] final kits:"); foreach (var l in FinalDump) sb.Append("\n[PerfScene]   " + l); }
			sb.Append("\n[PerfScene] gear cadence done");
			log(sb.ToString());
		}
	}
}
