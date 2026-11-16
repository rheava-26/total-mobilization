using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Levy.Common.Recording;

// RecReport --selftest                      the analyzer's own checks (the ones the mod runs at boot)
// RecReport <recording | folder> [--report] [--tsv] [--breaks] [--no-history]
//   default   the chat summary of each recording (2 to 4 lines), in file-name order, with the history built from the earlier recordings of the same folder
//   --report  also print the full report
//   --tsv     one table row per recording (for spreadsheets and for the threshold work)
//   --breaks  when the line-breaking detector fires in each fight, and why
internal static class Program
{
	private static int Main(string[] args)
	{
		if (args.Length == 0) { Console.Error.WriteLine("usage: RecReport <recording | folder> [--report] [--tsv] [--breaks]"); return 64; }
		bool report = args.Contains("--report"), tsv = args.Contains("--tsv"), breaks = args.Contains("--breaks");
		if (args[0] == "--selftest")
		{
			var fails = new List<string>(); int pass = FightReportSelfTest.Run(fails);
			foreach (string f in fails) Console.WriteLine("FAIL " + f);
			Console.WriteLine("[FightReport] selftest pass=" + pass + " fail=" + fails.Count);
			return fails.Count == 0 ? 0 : 1;
		}
		if (args[0] == "--synth")
		{
			var sy = FightRecReader.ReadLines(FightReportSelfTest.Build(new FightReportSelfTest.Script { Ext = !args.Contains("--old") }));
			foreach (string l in FightReport.Summary(sy, null)) Console.WriteLine(l);
			Console.WriteLine(); Console.Write(FightReport.Full(sy, null));
			return 0;
		}
		string path = args[0];
		var files = new List<string>();
		if (Directory.Exists(path)) files.AddRange(Directory.EnumerateFiles(path, "*.levyrec").Where(f => !f.EndsWith(".part")).OrderBy(f => f, StringComparer.Ordinal));
		else files.Add(path);
		var history = new List<HistoryEntry>();
		if (args.Contains("--series"))
		{
			Console.WriteLine("file\tresult\tsec\tn\tbroken\tdeaths\tbossFrac");
			foreach (string f in files)
			{
				var s = FightRecReader.Read(f, true); int di = 0;
				foreach (var a in s.Army) { while (di < s.LevyDeathTicks.Count && s.LevyDeathTicks[di] <= a.Sec * 60) di++; Console.WriteLine(string.Join("\t", Path.GetFileName(f), s.Result, a.Sec, a.N, a.Broken, di, a.BossFrac.ToString("0.000"))); }
			}
			return 0;
		}
		if (tsv) Console.WriteLine("file\tboss\tresult\tdur\tdps\tfightDps\tstealthKnown\tstealthPct\tcritPct\tarmy0\tarmy1\tlost\tbrokenPct\tbreakSec\tbreakWhy\tleftPct\tdeaths\tkiller\tonYouPct\tarmyDmgPct");
		foreach (string f in files)
		{
			FightStats s;
			try { s = FightRecReader.Read(f, true); }
			catch (Exception e) { Console.Error.WriteLine(Path.GetFileName(f) + ": " + e.GetType().Name + " " + e.Message); continue; }
			var prior = history.Where(h => FightHistory.Norm(h.Boss) == FightHistory.Norm(s.Boss)).ToList();
			if (tsv)
			{
				double on = 0, tot = 0; for (int i = 0; i < s.Target.Count; i++) { int t1 = i + 1 < s.Target.Count ? s.Target[i + 1].tick : s.EndTick; double d = Math.Max(0, t1 - s.Target[i].tick); tot += d; if (s.Target[i].kind == 1) on += d; }
				long all = Math.Max(1, s.AllBossDmg);
				Console.WriteLine(string.Join("\t", Path.GetFileName(f), s.Boss, s.Result, s.DurationSec.ToString("0"), s.Dps.ToString("0"), s.FightDps.ToString("0"), s.StealthKnown ? "1" : "0", s.StealthKnown ? (s.StealthShare * 100).ToString("0") : "", s.CritKnownHitsOrNeg(),
					s.ArmyStart, s.ArmyEnd, s.LeviesLost, (s.BrokenShare * 100).ToString("0"), s.BreakSec >= 0 ? s.BreakSec.ToString("0") : "", s.BreakWhy ?? "", s.BossLeftFrac >= 0 ? (s.BossLeftFrac * 100).ToString("0") : "", s.CmdDeaths,
					s.Deaths.Count > 0 ? s.Deaths[s.Deaths.Count - 1].Killer : "", tot > 0 ? (on / tot * 100).ToString("0") : "", (s.LevyBossDmg + s.DroneBossDmg) * 100 / all));
			}
			else
			{
				Console.WriteLine("== " + Path.GetFileName(f));
				var lines = FightReport.Summary(s, prior);
				if (lines.Count == 0) Console.WriteLine("(no summary: " + s.Result + ")");
				foreach (string l in lines) Console.WriteLine(l);
				if (breaks) Console.WriteLine("  line: " + (s.BreakSec >= 0 ? "broke at " + FightStats.Clock(s.BreakSec) + " by " + s.BreakWhy + " (boss left " + (s.BossLeftFrac >= 0 ? (int)Math.Round(s.BossLeftFrac * 100) + "%" : "?") + ", army " + s.ArmyStart + " to " + s.ArmyEnd + ")" : "held (army " + s.ArmyStart + " to " + s.ArmyEnd + ", peak " + s.ArmyPeak + ")"));
				if (report) { Console.WriteLine(); Console.Write(FightReport.Full(s, prior)); Console.WriteLine(); }
			}
			history.Add(FightHistory.FromStats(s, Path.GetFileName(f)));
		}
		return 0;
	}

	private static string CritKnownHitsOrNeg(this FightStats s) => s.CritKnown && s.CmdCritKnownHits > 0 ? ((int)Math.Round(100.0 * s.CmdCrits / s.CmdCritKnownHits)).ToString() : "";
}
