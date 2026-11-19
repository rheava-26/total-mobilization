using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Levy.Common.Recording
{
	/// <summary>One earlier fight, as the history keeps it: enough to say "last time" and "best".</summary>
	internal sealed class HistoryEntry
	{
		public string File, Boss, Date, Result;
		public double Dur, Dps, HitDps, Stealth = -1, Left = -1; // Dps: damage per second over the whole fight (the headline); HitDps: over the seconds the commander was hitting
		public int Army0 = -1, Army1 = -1, Deaths;
		public bool Won => Result == "won";
	}

	/// <summary>The per-folder history of fights: one line per recording in fightreport.index next to the recordings. A recording that is not in it yet (an older one) is read once and added.</summary>
	internal static class FightHistory
	{
		public const string IndexName = "fightreport.index";

		public static string Norm(string s)
		{
			var sb = new StringBuilder(); foreach (char c in s ?? "") if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
			return sb.ToString();
		}

		public static HistoryEntry FromStats(FightStats s, string fileName)
		{
			return new HistoryEntry
			{
				File = fileName, Boss = s.Boss, Date = s.Date, Result = s.Result, Dur = Math.Round(s.DurationSec, 1), Dps = Math.Round(s.FightDps, 1), HitDps = Math.Round(s.Dps, 1),
				Stealth = s.StealthKnown && s.StealthBarSeen ? Math.Round(s.StealthShare, 3) : -1, Left = s.BossLeftFrac < 0 ? -1 : Math.Round(s.BossLeftFrac, 3),
				Army0 = s.Army.Count > 0 ? s.ArmyStart : -1, Army1 = s.Army.Count > 0 ? s.ArmyEnd : -1, Deaths = s.CmdDeaths
			};
		}

		public static string ToJson(HistoryEntry e)
		{
			string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
			return "{\"f\":" + JsonSerializer.Serialize(e.File ?? "") + ",\"boss\":" + JsonSerializer.Serialize(e.Boss ?? "") + ",\"date\":" + JsonSerializer.Serialize(e.Date ?? "") + ",\"result\":" + JsonSerializer.Serialize(e.Result ?? "")
				+ ",\"dur\":" + F(e.Dur) + ",\"dps\":" + F(e.Dps) + ",\"hdps\":" + F(e.HitDps) + ",\"ss\":" + F(e.Stealth) + ",\"left\":" + F(e.Left) + ",\"a0\":" + e.Army0 + ",\"a1\":" + e.Army1 + ",\"deaths\":" + e.Deaths + "}";
		}

		public static HistoryEntry Parse(string line)
		{
			try
			{
				using var d = JsonDocument.Parse(line); var r = d.RootElement;
				double D(string n, double def = 0) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
				string S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
				return new HistoryEntry { File = S("f"), Boss = S("boss"), Date = S("date"), Result = S("result"), Dur = D("dur"), Dps = D("dps"), HitDps = D("hdps"), Stealth = D("ss", -1), Left = D("left", -1), Army0 = (int)D("a0", -1), Army1 = (int)D("a1", -1), Deaths = (int)D("deaths") };
			}
			catch (Exception) { return null; }
		}

		public static List<HistoryEntry> ReadIndex(string dir)
		{
			var l = new List<HistoryEntry>();
			try
			{
				string p = Path.Combine(dir, IndexName); if (!File.Exists(p)) return l;
				foreach (string line in File.ReadAllLines(p)) { var e = Parse(line); if (e != null && !string.IsNullOrEmpty(e.File)) l.Add(e); }
			}
			catch (Exception) { }
			return l;
		}

		public static void Append(string dir, HistoryEntry e)
		{
			try { File.AppendAllText(Path.Combine(dir, IndexName), ToJson(e) + "\n"); } catch (Exception) { }
		}

		/// <summary>Earlier fights against <paramref name="boss"/> in the folder, oldest first, not counting <paramref name="exclude"/> (the fight being reported). Recordings that the index does not know yet are read (at most
		/// <paramref name="budget"/> of them per call, newest first, damage and end record only) and added to the index.</summary>
		public static List<HistoryEntry> Load(string dir, string boss, string exclude, int budget, Action<string> log = null)
		{
			var res = new List<HistoryEntry>(); string want = Norm(boss);
			try
			{
				var idx = ReadIndex(dir); var known = new HashSet<string>(idx.Select(e => e.File), StringComparer.OrdinalIgnoreCase);
				string ex = exclude == null ? null : Path.GetFileName(exclude);
				var files = Directory.EnumerateFiles(dir, "*.levyrec").Where(f => !f.EndsWith(".part")).Select(f => Path.GetFileName(f)).OrderByDescending(f => f, StringComparer.Ordinal).ToList();
				foreach (string f in files)
				{
					if (budget <= 0) break;
					if (known.Contains(f) || string.Equals(f, ex, StringComparison.OrdinalIgnoreCase)) continue;
					string stem = Norm(Path.GetFileNameWithoutExtension(f));
					if (!stem.Contains(want)) continue; // the file name carries the boss's name: other bosses' recordings are not read at all
					try
					{
						var st = FightRecReader.Read(Path.Combine(dir, f), false);
						if (Norm(st.Boss) != want) continue; // a recording with a boss in the name that is not the fight's boss
						var he = FromStats(st, f); he.Army0 = he.Army1 = -1; Append(dir, he); idx.Add(he); known.Add(f); budget--;
						log?.Invoke("indexed " + f);
					}
					catch (Exception e) { log?.Invoke("could not read " + f + ": " + e.Message); budget--; }
				}
				foreach (var e in idx) if (Norm(e.Boss) == want && !string.Equals(e.File, ex, StringComparison.OrdinalIgnoreCase)) res.Add(e);
				res.Sort((a, b) => string.CompareOrdinal(a.File, b.File));
			}
			catch (Exception) { }
			return res;
		}
	}

	internal static class FightReport
	{
		private static string N0(double v) => FightStats.N0(v);
		private static string Pct(double f) => ((int)Math.Round(f * 100)).ToString(CultureInfo.InvariantCulture) + "%";

		/// <summary>How the fight ended, in words: "won in 2:41", "lost at 2%", "left at 63%". Null when the recording is not a finished boss fight.</summary>
		public static string Outcome(FightStats s)
		{
			string at = s.BossLeftFrac >= 0 ? " at " + Pct(s.BossLeftFrac) : "";
			switch (s.Result)
			{
				case "won": return "won in " + FightStats.Clock(s.DurationSec);
				case "lost": return "lost" + at;
				case "fled": return "left" + at;
				default: return null;
			}
		}

		private static string Plural(int n, string one, string many) => n == 1 ? one : many;

		/// <summary>The chat summary: two to four short lines.</summary>
		public static List<string> Summary(FightStats s, List<HistoryEntry> prior)
		{
			var lines = new List<string>();
			string oc = Outcome(s); if (oc == null) return lines;
			var l1 = new StringBuilder(s.Boss + ", " + oc + ". ");
			if (s.CmdBossDmg <= 0) l1.Append("You did no damage to it.");
			else
			{
				l1.Append("Your damage: " + N0(s.FightDps) + " per second.");
				if (s.StealthKnown && s.StealthBarSeen) l1.Append(" Stealth strikes " + Pct(s.StealthShare) + " of it.");
			}
			lines.Add(l1.ToString());
			if (s.CmdDeaths > 0)
			{
				var last = s.Deaths.Count > 0 ? s.Deaths[s.Deaths.Count - 1] : null;
				string killer = last?.Killer;
				bool fell = last != null && last.ArmyStart > 0 && last.ArmyAt <= Math.Max(2, last.ArmyStart / 10);
				string tail = fell ? " after the army fell." : ".";
				if (s.CmdDeaths == 1) lines.Add("Killed by " + (killer ?? "something that left no trace in the recording") + tail);
				else lines.Add("Killed " + s.CmdDeaths + " times, last by " + (killer ?? "something unrecorded") + tail);
			}
			if (s.Army.Count > 0 && s.ArmyStart > 0)
			{
				string br = s.BreakSec >= 0 ? "broke at " + FightStats.Clock(s.BreakSec) : "held";
				lines.Add("Army: " + s.ArmyStart + " to " + s.ArmyEnd + ", " + br + "." + (s.HpScalePct > 0 ? " " + ScaleLine(s) : ""));
			}
			else if (s.HpScalePct > 0) lines.Add(ScaleLine(s));
			if (prior != null && prior.Count > 0 && lines.Count < 4)
			{
				var best = prior.Where(p => p.Dps > 0).OrderByDescending(p => p.Dps).FirstOrDefault();
				if (best != null)
				{
					var lastP = prior[prior.Count - 1];
					string cmp = "Your best against " + s.Boss + ": " + N0(best.Dps) + " per second.";
					if (lastP != null && lastP.Dps > 0 && lastP != best) cmp += " Last time: " + N0(lastP.Dps) + ".";
					lines.Add(cmp);
				}
			}
			return lines;
		}

		/// <summary>"Boss life scaled +80% for 40 command points."</summary>
		internal static string ScaleLine(FightStats s) => "Boss life scaled +" + s.HpScalePct + "% for " + s.HpScaleCp + " command " + Plural(s.HpScaleCp, "point", "points") + ".";

		private static string Row(params string[] cols) => string.Join("  ", cols);

		/// <summary>The full report file.</summary>
		public static string Full(FightStats s, List<HistoryEntry> prior)
		{
			var o = new StringBuilder();
			void L(string t = "") => o.Append(t).Append('\n');
			string oc = Outcome(s) ?? s.Result;
			L(s.Boss + ", " + oc);
			L("Recorded " + (s.Date ?? "unknown date").Replace('T', ' ') + (string.IsNullOrEmpty(s.Player) ? "" : ", " + s.Player) + (string.IsNullOrEmpty(s.Mode) ? "" : ", " + s.Mode) + ", " + FightStats.Clock(s.DurationSec) + " long.");
			L("Recording format " + s.Format + (s.Format < 10 ? ": made before stealth, crit and weapon fields were recorded, so those numbers are marked not recorded." : "."));
			if (s.HpScalePct > 0) L(ScaleLine(s));
			L();

			L("What you did to the boss");
			if (s.CmdBossDmg <= 0) L("  No damage from you reached the boss.");
			else
			{
				L("  " + N0(s.FightDps) + " damage per second over the whole fight (" + N0(s.CmdBossDmg) + " damage in " + FightStats.Clock(s.DurationSec) + "). " + N0(s.Dps) + " per second over the " + N0(s.ActiveSec) + " seconds you were hitting it.");
				if (s.StealthKnown) L("  Stealth strikes: " + Pct(s.StealthShare) + " of your damage, " + N0(s.CmdStealthHits) + " of " + N0(s.CmdBossHits) + " hits.");
				else L("  Stealth strikes: not recorded in this file.");
				if (s.CritKnown && s.CmdCritKnownHits > 0) L("  Crits: " + Pct((double)s.CmdCrits / s.CmdCritKnownHits) + " of hits.");
				else L("  Crits: not recorded in this file.");
				if (s.StealthBarSeen && s.StealthPct.Count > 0)
				{
					int full = s.StealthPct.Count(p => p >= 99); double avg = s.StealthPct.Average();
					L("  Stealth bar: full " + Pct((double)full / s.StealthPct.Count) + " of the time, " + N0(avg) + "% on average.");
				}
				L();
				L("By weapon (hits per second and damage per hit count only the seconds that weapon was landing hits)");
				var ws = s.Weapons.Values.OrderByDescending(w => w.Dmg).ToList();
				int nameW = Math.Max(8, ws.Count == 0 ? 8 : Math.Min(30, ws.Max(w => w.Name.Length)));
				L("  " + Row("weapon".PadRight(nameW), "seconds".PadLeft(7), "dmg/s".PadLeft(7), "hits/s".PadLeft(6), "dmg/hit".PadLeft(8), "stealth".PadLeft(8), "st dmg/hit".PadLeft(10), "norm dmg/hit".PadLeft(12), "crit".PadLeft(5), "share".PadLeft(6)));
				foreach (var w in ws)
				{
					string nm = w.Name.Length > nameW ? w.Name.Substring(0, nameW) : w.Name.PadRight(nameW);
					double sec = Math.Max(0.001, w.ActiveSec);
					string st = w.StealthKnown ? Pct(w.Dmg > 0 ? (double)w.StealthDmg / w.Dmg : 0) : "n/a";
					string sh = w.StealthKnown && w.StealthHits > 0 ? N0((double)w.StealthDmg / w.StealthHits) : (w.StealthKnown ? "-" : "n/a");
					string nh = w.StealthKnown && w.NormalHits > 0 ? N0((double)w.NormalDmg / w.NormalHits) : (w.StealthKnown ? "-" : "n/a");
					string cr = w.CritKnown && w.CritKnownHits > 0 ? Pct((double)w.Crits / w.CritKnownHits) : "n/a";
					L("  " + Row(nm, w.ActiveSec.ToString("0", CultureInfo.InvariantCulture).PadLeft(7), N0(w.Dmg / sec).PadLeft(7), (w.Hits / sec).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(6), N0(w.Hits > 0 ? (double)w.Dmg / w.Hits : 0).PadLeft(8),
						st.PadLeft(8), sh.PadLeft(10), nh.PadLeft(12), cr.PadLeft(5), Pct(s.CmdBossDmg > 0 ? (double)w.Dmg / s.CmdBossDmg : 0).PadLeft(6)));
				}
				var uses = ws.Where(w => w.Uses > 0).ToList();
				if (uses.Count > 0) L("  Uses: " + string.Join(", ", uses.Select(w => w.Name + " " + w.Uses + " (" + (w.Hits / (double)Math.Max(1, w.Uses)).ToString("0.0", CultureInfo.InvariantCulture) + " hits per use)")) + ".");
			}
			L();

			L("What hurt you");
			if (s.TakenHits == 0) L("  Nothing hit you.");
			else
			{
				L("  " + N0(s.TakenTotal) + " damage in " + s.TakenHits + " hits. By source:");
				foreach (var t in s.Taken.Values.OrderByDescending(t => t.Dmg).Take(12))
					L("    " + t.Label.PadRight(34) + " " + N0(t.Dmg).PadLeft(8) + " damage, " + t.Hits.ToString().PadLeft(3) + " hits" + (t.Deaths > 0 ? ", killed you " + t.Deaths + (t.Deaths == 1 ? " time" : " times") : ""));
			}
			foreach (var d in s.Deaths)
				L("  You fell at " + FightStats.Clock(d.Tick / 60.0) + " to " + (d.Killer ?? "something unrecorded") + ", with " + d.ArmyAt + " levies in the arena" + (d.ArmyStart > 0 ? " of " + d.ArmyStart : "") + ".");
			if (s.Deaths.Count == 0 && s.CmdDeaths > 0) L("  You fell " + s.CmdDeaths + (s.CmdDeaths == 1 ? " time." : " times."));
			L();

			L("The army");
			if (s.Army.Count == 0 || s.ArmyStart <= 0) L("  No levies were in the arena.");
			else
			{
				L("  " + s.ArmyStart + " at the start, " + s.ArmyEnd + " at the end. " + s.LeviesLost + " levies fell.");
				L("  " + (s.BreakSec >= 0 ? "The line broke at " + FightStats.Clock(s.BreakSec) + " (" + (s.BreakWhy == "morale" ? "a third of the army shaken or routed" : "four tenths of the army lost within twenty seconds") + ")." : "The line held.") + (s.WarnSec >= 0 ? " The warning came at " + FightStats.Clock(s.WarnSec) + "." : ""));
				L("  Shaken, routed or deserting: " + Pct(s.BrokenShare) + " of all levy time.");
				foreach (var line in StrengthLines(s)) L("  " + line);
				var br = MoraleBreaks(s);
				if (br.Count == 0) L("  No stretch with a quarter of the army broken.");
				else foreach (var b in br) L("  Morale break " + FightStats.Clock(b.start) + " to " + FightStats.Clock(b.end) + ": up to " + b.peak + " of " + b.of + " levies shaken or routed.");
				long all = Math.Max(1, s.AllBossDmg);
				long army = s.LevyBossDmg + s.DroneBossDmg;
				L("  Damage to the boss: you " + Pct((double)s.CmdBossDmg / all) + ", levies " + Pct((double)s.LevyBossDmg / all) + (s.DroneBossDmg > 0 ? ", drones " + Pct((double)s.DroneBossDmg / all) : "") + (s.OtherBossDmg > 0 ? ", other " + Pct((double)s.OtherBossDmg / all) : "") + ". The army did " + N0(army) + " of " + N0(all) + ".");
			}
			L();

			L("Who the boss went for");
			foreach (var line in AggroLines(s)) L("  " + line);
			L();

			if (s.Milestones.Count > 0 || s.Phases.Count > 0)
			{
				L("The boss's life");
				foreach (var m in s.Milestones) L("  Down to " + m.pct + "% at " + FightStats.Clock(m.sec) + ", " + m.army + " levies in the arena.");
				foreach (var p in s.Phases.Where(p => !(s.Milestones.Count > 0 && System.Text.RegularExpressions.Regex.IsMatch(p.what, @"^\d+% life$")))) L("  " + FightStats.Clock(p.tick / 60.0) + ": " + p.what + ".");
				L();
			}

			L("Against earlier fights with " + s.Boss);
			if (prior == null || prior.Count == 0) L("  This is the first recorded fight against " + s.Boss + ".");
			else
			{
				var last = prior[prior.Count - 1];
				L("  Last: " + Describe(last) + ".");
				var best = prior.Where(p => p.Dps > 0).OrderByDescending(p => p.Dps).FirstOrDefault();
				if (best != null) L("  Best: " + Describe(best) + ".");
				if (s.FightDps > 0 && last.Dps > 0) L("  This fight: " + N0(s.FightDps) + " per second, " + Delta(s.FightDps, last.Dps) + " against the last and " + (best != null ? Delta(s.FightDps, best.Dps) + " against the best" : "") + ".");
				var all = prior.Where(p => p.Dps > 0).ToList();
				if (all.Count > 1) L("  Across " + all.Count + " earlier fights: between " + N0(all.Min(p => p.Dps)) + " and " + N0(all.Max(p => p.Dps)) + " per second, " + N0(all.Average(p => p.Dps)) + " on average. " + prior.Count(p => p.Won) + " won.");
			}
			return o.ToString();
		}

		private static string Delta(double now, double then)
		{
			if (then <= 0) return "no comparison";
			double d = (now - then) / then * 100;
			return (d >= 0 ? "up " : "down ") + Math.Abs((int)Math.Round(d)).ToString(CultureInfo.InvariantCulture) + "%";
		}

		private static string Describe(HistoryEntry e)
		{
			string date = (e.Date ?? "").Replace('T', ' '); if (date.Length > 16) date = date.Substring(0, 16);
			string res = e.Result == "won" ? "won" : e.Result == "lost" ? "lost" + (e.Left >= 0 ? " at " + Pct(e.Left) : "") : e.Result == "fled" ? "left" + (e.Left >= 0 ? " at " + Pct(e.Left) : "") : e.Result;
			string ss = e.Stealth >= 0 ? ", stealth strikes " + Pct(e.Stealth) + " of it" : "";
			return N0(e.Dps) + " per second (" + res + ", " + date + ss + ")";
		}

		/// <summary>The army's strength over the fight as short rows: a sample every few seconds, the number of levies, and a bar scaled to the best strength.</summary>
		internal static List<string> StrengthLines(FightStats s)
		{
			var lines = new List<string>();
			if (s.Army.Count == 0) return lines;
			double secs = Math.Max(1, s.Army[s.Army.Count - 1].Sec - s.Army[0].Sec);
			int step = secs <= 90 ? 5 : secs <= 240 ? 10 : secs <= 600 ? 30 : 60;
			var tl = new StringBuilder("Army by time:"); var bar = new StringBuilder();
			int peak = Math.Max(1, s.ArmyPeak); const string ramp = " .:-=+*#%@";
			int t0 = s.Army[0].Sec;
			for (int sec = t0; sec <= s.Army[s.Army.Count - 1].Sec; sec += step)
			{
				var p = s.Army.LastOrDefault(a => a.Sec <= sec); if (p.Sec == 0 && p.N == 0 && s.Army[0].Sec > sec) continue;
				tl.Append(' ').Append(FightStats.Clock(sec - t0)).Append('=').Append(p.N);
				bar.Append(ramp[Math.Clamp((int)Math.Round((double)p.N / peak * (ramp.Length - 1)), 0, ramp.Length - 1)]);
			}
			lines.Add(tl.ToString());
			lines.Add("Strength line, one mark per " + step + " seconds, scaled to " + peak + " levies: [" + bar + "]");
			return lines;
		}

		/// <summary>Stretches where a quarter or more of the army was shaken, routed or deserting (army of at least four).</summary>
		internal static List<(double start, double end, int peak, int of)> MoraleBreaks(FightStats s)
		{
			var res = new List<(double, double, int, int)>(); bool on = false; double st = 0; int pk = 0, of = 0; double lastSec = 0;
			foreach (var a in s.Army)
			{
				bool b = a.N >= 4 && a.Broken * 4 >= a.N;
				if (b && !on) { on = true; st = a.Sec; pk = 0; of = 0; }
				if (b) { if (a.Broken > pk) { pk = a.Broken; of = a.N; } lastSec = a.Sec; }
				if (!b && on) { on = false; res.Add((st, lastSec, pk, of)); }
			}
			if (on) res.Add((st, lastSec, pk, of));
			return res;
		}

		/// <summary>Where the boss's attention was: time on you, on levies (real targeting) and on a levy by spite, split by how much boss life was left, and the moment it last turned on you.</summary>
		internal static List<string> AggroLines(FightStats s)
		{
			var lines = new List<string>();
			if (s.Target.Count == 0) { lines.Add("No target changes were recorded."); return lines; }
			int endTick = Math.Max(s.EndTick, s.Target[s.Target.Count - 1].tick);
			double[] by = new double[4]; double[,] band = new double[3, 4];
			for (int i = 0; i < s.Target.Count; i++)
			{
				int t0 = s.Target[i].tick, t1 = i + 1 < s.Target.Count ? s.Target[i + 1].tick : endTick; int k = Math.Clamp(s.Target[i].kind, 0, 3);
				if (t1 <= t0) continue;
				by[k] += t1 - t0;
				// split by boss life at the moment: sample each second inside the span
				for (int t = t0; t < t1; t += 60) { double f = FracAt(s, t); int bi = f > 0.5 ? 0 : f > 0.25 ? 1 : 2; band[bi, k] += Math.Min(60, t1 - t); }
			}
			double tot = Math.Max(1, by[0] + by[1] + by[2] + by[3]);
			lines.Add("On you " + Pct(by[1] / tot) + " of the fight, on levies " + Pct(by[2] / tot) + (by[3] > 0 ? ", on a levy by grudge " + Pct(by[3] / tot) : "") + (by[0] > 0 ? ", on nobody " + Pct(by[0] / tot) : "") + ".");
			string[] names = { "boss life above 50%", "boss life 25% to 50%", "boss life 25% or less" };
			for (int bi = 0; bi < 3; bi++)
			{
				double bt = band[bi, 0] + band[bi, 1] + band[bi, 2] + band[bi, 3]; if (bt < 60) continue;
				lines.Add("  With " + names[bi] + ": on you " + Pct(band[bi, 1] / bt) + ", on levies " + Pct((band[bi, 2] + band[bi, 3]) / bt) + ".");
			}
			int lastSwitch = -1; for (int i = 1; i < s.Target.Count; i++) if (s.Target[i].kind == 1 && s.Target[i - 1].kind != 1) lastSwitch = s.Target[i].tick;
			if (lastSwitch >= 0) lines.Add("It last turned on you at " + FightStats.Clock(lastSwitch / 60.0) + (s.Target[s.Target.Count - 1].kind == 1 ? " and stayed there." : "."));
			else if (s.Target[0].kind == 1) lines.Add("It was on you from the start.");
			return lines;
		}

		private static double FracAt(FightStats s, int tick)
		{
			double f = 1; int sec = tick / 60;
			foreach (var a in s.Army) { if (a.Sec > sec) break; if (a.BossFrac >= 0) f = a.BossFrac; }
			return f;
		}
	}
}
