using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Levy.Common.Perf;

// PerfAnalyze --selftest                          the analyzer's own checks (the ones the mod runs at boot)
// PerfAnalyze popup <recording | folder> [--tsv] [--all]
//                                                 what the post-fight popup would have said about each recording, from the profile events inside it
//                                                 (an old recording has Levy's own work and the frame times only, so it can name the army but no other mod)
// PerfAnalyze log <perf folder> [--minutes N]     the last N minutes (default 1) of an always-on perf log, as /levyperf shows them, and the fights the log judged
internal static class Program
{
	private static int Main(string[] args)
	{
		if (args.Length == 0) { Console.Error.WriteLine("usage: PerfAnalyze --selftest | popup <recording|folder> [--tsv] [--all] | log <perf folder> [--minutes N]"); return 64; }
		if (args[0] == "--selftest")
		{
			var fails = new List<string>(); int pass = PerfSelfTest.Run(fails);
			foreach (string f in fails) Console.WriteLine("FAIL " + f);
			Console.WriteLine("[PerfAnalyzer] selftest pass=" + pass + " fail=" + fails.Count);
			return fails.Count == 0 ? 0 : 1;
		}
		if (args[0] == "popup" && args.Length > 1) return Popup(args[1], args.Contains("--tsv"), args.Contains("--all"));
		if (args[0] == "log" && args.Length > 1)
		{
			int i = Array.IndexOf(args, "--minutes"); double min = i >= 0 && i + 1 < args.Length ? double.Parse(args[i + 1], CultureInfo.InvariantCulture) : 1;
			var all = PerfLogFile.ReadAll(args[1]);
			if (all.Count == 0) { Console.WriteLine("no windows in " + args[1]); return 1; }
			var last = new List<PerfWindow>(); double acc = 0;
			for (int k = all.Count - 1; k >= 0 && acc < min * 60; k--) { last.Insert(0, all[k]); acc += all[k].Secs; }
			foreach (string l in PerfAnalysis.Report(PerfSpan.Of(last))) Console.WriteLine(l);
			return 0;
		}
		Console.Error.WriteLine("unknown command"); return 64;
	}

	private static IEnumerable<string> Lines(string path)
	{
		using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		Stream s = fs; if (path.EndsWith(".levyrec", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".part")) s = new GZipStream(fs, CompressionMode.Decompress);
		using var rd = new StreamReader(s);
		while (true)
		{
			string l; try { l = rd.ReadLine(); } catch (Exception) { yield break; }
			if (l == null) yield break;
			yield return l;
		}
	}

	private static int Popup(string path, bool tsv, bool all)
	{
		var files = new List<string>();
		if (Directory.Exists(path)) files.AddRange(Directory.EnumerateFiles(path, "*.levyrec").Where(f => !f.EndsWith(".part")).OrderBy(f => f, StringComparer.Ordinal));
		else files.Add(path);
		if (tsv) Console.WriteLine("file\tboss\twindowSec\tmedianFps\tmedianUps\tlaggyPct\tlevyPct\tkind\tpopup\toptions");
		int laggy = 0, withPopup = 0, total = 0;
		foreach (string f in files)
		{
			string boss = ""; var ws = new List<PerfWindow>(); string result = "";
			foreach (string l in Lines(f))
			{
				if (l.StartsWith("{\"k\":\"hdr\"")) { try { using var d = JsonDocument.Parse(l); if (d.RootElement.TryGetProperty("boss", out var b)) boss = b.GetString() ?? ""; } catch (Exception) { } }
				else if (l.Contains("\"e\":\"prof\"") && l.StartsWith("{\"k\":\"e\"")) { var w = PerfWindow.FromRecProf(l, boss, DateTime.UtcNow); if (w != null) ws.Add(w); }
			}
			if (ws.Count == 0) continue;
			foreach (var w in ws) w.Boss = boss;
			total++;
			var v = PerfAnalysis.Judge(ws);
			if (v.Laggy) laggy++; if (v.ShowPopup) withPopup++;
			string name = Path.GetFileNameWithoutExtension(f);
			if (tsv) { Console.WriteLine($"{name}\t{boss}\t{v.FightSecs:0}\t{v.MedianFps:0.0}\t{v.MedianUps:0.0}\t{v.LaggyShare * 100:0}\t{v.LevyShare * 100:0}\t{v.Kind}\t{(v.ShowPopup ? 1 : 0)}\t{string.Join("+", v.Options)}"); continue; }
			if (!all && !v.Laggy) continue;
			Console.WriteLine($"{name}: {boss}, {v.FightSecs:0} s of profile windows, median {v.MedianFps:0} fps and {v.MedianUps:0} updates per second, {v.LaggyShare * 100:0}% of it laggy.");
			if (!v.Laggy) { Console.WriteLine("  verdict: smooth, no popup"); continue; }
			Console.WriteLine($"  verdict: {v.Kind} (Levy's own work {v.LevyShare * 100:0}% of the loop's time)");
			if (v.ShowPopup)
			{
				Console.WriteLine("  popup:   " + v.Headline + " " + string.Join(" ", v.Detail));
				Console.WriteLine("  buttons: " + string.Join(" | ", v.Options.Select(PerfAnalysis.Label)));
			}
			else Console.WriteLine("  popup:   none (" + (v.Kind == LagKind.Unknown ? "an old recording cannot name another cause" : v.Kind.ToString()) + ")");
		}
		if (!tsv) Console.WriteLine($"\n{total} recordings with profile events: {laggy} laggy, {withPopup} would show a popup.");
		return 0;
	}
}
