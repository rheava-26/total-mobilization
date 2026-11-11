using System;
using System.Diagnostics;

namespace Levy.Common.Util
{
	/// <summary>Stopwatch self-measure for the lab (LEVY_PROF=1): milliseconds per game tick spent in the levy AI and its worm / dodge / zone / row subsystems. Off = one bool test per call site.</summary>
	public static class LevyProf
	{
		/// <summary>LEVY_OPT=0 puts the army tick back to its previous code path (the perf-destroyer fixes off) so a lab can A/B them. On by default.</summary>
		public static readonly bool Opt = Environment.GetEnvironmentVariable("LEVY_OPT") != "0";
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_PROF") == "1";
		public const int AI = 0, Dodge = 1, Near = 2, Hits = 3, Snap = 4, Worm = 5, Zone = 6, Rows = 7, Brain = 8, Perch = 9, Hold = 10, Mom = 11, Move = 12, Combat = 13, Fear = 14, Dmg = 15, Dummy = 16, Stats = 17, Rip = 18, GfSlot = 19, GfCopy = 20, GfReset = 21, GfBuffs = 22, GfEquips = 23, GfSets = 24, GfPost = 25, GfMisc = 26, GfExit = 27, NpcLevy = 28, NpcBoss = 29, NpcAdd = 30, NpcMech = 31, NpcOther = 32, ProjArmy = 33, ProjEnemy = 34, ProjMech = 35, ProjOther = 36, N = 37;
		private static readonly string[] Names = { "ai", "dodge", "near", "hits", "snap", "wormfn", "zone", "rows", "brain", "perch", "hold", "mom", "move", "combat", "fear", "dmg", "dummy", "stats", "rip", "g.slot", "g.copy", "g.reset", "g.buffs", "g.equips", "g.sets", "g.post", "g.misc", "g.exit", "npc.levy", "npc.boss", "npc.add", "npc.mech", "npc.other", "proj.army", "proj.enemy", "proj.mech", "proj.other" };
		/// <summary>Entities updated since the last Reset (per category, same order as Names from NpcLevy), for the per-tick counts in the dump.</summary>
		public static readonly long[] Cnt = new long[N];
		/// <summary>Lab counters: projectile/NPC pairs a soldier shot was asked about (CanHitNPC, once per pair the vanilla loop gets past its own flags) and hits it landed.</summary>
		public static long Pairs, ShotHits, DamageTicks, PairsCulled;
		private static readonly long[] T = new long[N];
		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct Timespec { public long Sec, Nsec; }
		[System.Runtime.InteropServices.DllImport("libc", EntryPoint = "clock_gettime")] private static extern int ClockGetTime(int clk, out Timespec ts);
		private static bool _cpuBroken;
		/// <summary>CPU time of the calling thread in nanoseconds (Linux CLOCK_THREAD_CPUTIME_ID), or -1 where it is unavailable. Lab only: unlike wall time it does not grow when other processes share the cores.</summary>
		public static long ThreadCpuNs()
		{
			if (_cpuBroken) return -1;
			try { return ClockGetTime(3, out Timespec ts) == 0 ? ts.Sec * 1_000_000_000L + ts.Nsec : -1; }
			catch (Exception) { _cpuBroken = true; return -1; }
		}
		public static long Begin() => On ? Stopwatch.GetTimestamp() : 0L;
		public static void End(int k, long t0) { if (On) T[k] += Stopwatch.GetTimestamp() - t0; }
		public static void Add(int k, long d) { T[k] += d; }
		/// <summary>Update time and calls per projectile / NPC type (lab only), to find the one weapon or enemy whose AI is out of proportion.</summary>
		private static readonly System.Collections.Generic.Dictionary<int, (long ticks, int n)> ByType = new();
		/// <summary>key: a projectile type, or 100000 + an NPC type.</summary>
		public static void AddType(int key, long ticks) { ByType.TryGetValue(key, out var v); ByType[key] = (v.ticks + ticks, v.n + 1); }
		public static string TopTypes(int ticks, int top)
		{
			var l = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, (long ticks, int n)>>(ByType);
			l.Sort((a, b) => b.Value.ticks.CompareTo(a.Value.ticks));
			var sb = new System.Text.StringBuilder();
			double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, ticks);
			for (int i = 0; i < top && i < l.Count; i++) sb.Append(i > 0 ? ", " : "").Append(TypeName(l[i].Key)).Append(' ').Append((l[i].Value.ticks * f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("ms (").Append((l[i].Value.n / (double)Math.Max(1, ticks)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append("/tick)");
			return sb.ToString();
		}
		private static string TypeName(int key)
		{
			try
			{
				if (key >= 100000) { int t = key - 100000; var m = Terraria.ModLoader.NPCLoader.GetNPC(t); return "N:" + (m != null ? m.FullName : "vanilla" + t); }
				var p = Terraria.ModLoader.ProjectileLoader.GetProjectile(key); return "P:" + (p != null ? p.FullName : "vanilla" + key);
			}
			catch (Exception) { return key.ToString(); }
		}
		public static void CopyTo(long[] dst) { Array.Copy(T, dst, N); }
		/// <summary>The buckets that grew the most since <paramref name="before"/> (a CopyTo snapshot), as "name ms" pairs: what a spike tick was spent in. Lab only.</summary>
		public static string DescribeDelta(long[] before, int top)
		{
			var d = new (double ms, int i)[N];
			for (int i = 0; i < N; i++) d[i] = ((T[i] - before[i]) * 1000.0 / Stopwatch.Frequency, i);
			Array.Sort(d, (a, b) => b.ms.CompareTo(a.ms));
			var sb = new System.Text.StringBuilder();
			for (int k = 0; k < top && d[k].ms > 0.05; k++) sb.Append(k > 0 ? ", " : "").Append(Names[d[k].i]).Append(' ').Append(d[k].ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
			return sb.ToString();
		}
		public static void Reset() { Array.Clear(T, 0, N); Array.Clear(Cnt, 0, N); ByType.Clear(); Pairs = ShotHits = DamageTicks = PairsCulled = 0; }
		public static string Dump(int ticks)
		{
			double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, ticks);
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < N; i++) sb.Append(Names[i]).Append('=').Append((T[i] * f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
			sb.Append("| pairs/tick=").Append((Pairs / (double)Math.Max(1, ticks)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" culled/tick=").Append((PairsCulled / (double)Math.Max(1, ticks)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" hits/tick=").Append((ShotHits / (double)Math.Max(1, ticks)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" damage ms/tick=").Append((DamageTicks * f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
			sb.Append("| per tick counts:");
			for (int i = NpcLevy; i < N; i++) sb.Append(' ').Append(Names[i]).Append('=').Append((Cnt[i] / (double)Math.Max(1, ticks)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
			return sb.ToString().Trim();
		}
	}
}
