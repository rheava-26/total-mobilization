using System;
using System.Diagnostics;

namespace Levy.Common.Util
{
	/// <summary>Stopwatch self-measure for the lab (LEVY_PROF=1): milliseconds per game tick spent in the levy AI and its worm / dodge / zone / row subsystems. Off = one bool test per call site.</summary>
	public static class LevyProf
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_PROF") == "1";
		public const int AI = 0, Dodge = 1, Near = 2, Hits = 3, Snap = 4, Worm = 5, Zone = 6, Rows = 7, Brain = 8, Perch = 9, Hold = 10, Mom = 11, Move = 12, Combat = 13, Fear = 14, Dmg = 15, Dummy = 16, Stats = 17, Rip = 18, N = 19;
		private static readonly string[] Names = { "ai", "dodge", "near", "hits", "snap", "wormfn", "zone", "rows", "brain", "perch", "hold", "mom", "move", "combat", "fear", "dmg", "dummy", "stats", "rip" };
		private static readonly long[] T = new long[N];
		public static long Begin() => On ? Stopwatch.GetTimestamp() : 0L;
		public static void End(int k, long t0) { if (On) T[k] += Stopwatch.GetTimestamp() - t0; }
		public static void Add(int k, long d) { T[k] += d; }
		public static void Reset() { Array.Clear(T, 0, N); }
		public static string Dump(int ticks)
		{
			double f = 1000.0 / Stopwatch.Frequency / Math.Max(1, ticks);
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < N; i++) sb.Append(Names[i]).Append('=').Append((T[i] * f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
			return sb.ToString().Trim();
		}
	}
}
