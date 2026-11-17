using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Levy.Common.Perf
{
	/// <summary>
	/// The analyzer's pure checks (System.* only): the window file format, the lag decision on synthetic fights (laggy and Levy's, laggy and another mod's, smooth, too short, the Magic Storage map layer case), the popup's option lists and rate rules,
	/// the words, and the rolling log (rotation by size and by day, caps). The mod runs them at every boot ("[PerfAnalyzer] self-check"), and the command-line tool runs the same ones.
	/// </summary>
	internal static class PerfSelfTest
	{
		private static List<PerfWindow> Fight(int n, double fps, double ups, double levy, string mod, double modShare, double secs = 8) { var l = new List<PerfWindow>(); for (int i = 0; i < n; i++) l.Add(PerfAnalysis.Synth(secs, fps, ups, levy, mod, modShare)); return l; }
		private static string Opts(LagVerdict v) => string.Join(",", v.Options);

		public static int Run(List<string> fails)
		{
			int pass = 0;
			void Chk(bool c, string w) { if (c) pass++; else fails.Add(w); }

			// ---- the window file format
			var w0 = PerfAnalysis.Synth(8, 33.3, 59.5, 0.4, "CalamityMod", 0.2);
			w0.Held = 757; w0.HeldName = "Sword \"X\""; w0.Map = 1; w0.Levy.Add(new KeyValuePair<string, double>("ai", 1.25)); w0.Top.Add(new PerfItem(PerfCat.Map, "MagicStorage/PortableAccessRangeDisplay", 3360, 168));
			string json = w0.ToJson(); var back = PerfWindow.FromJson(json);
			Chk(back != null && Math.Abs(back.Fps - 33.3) < 0.05 && back.Held == 757 && back.HeldName == "Sword \"X\"" && back.Map == 1 && back.Boss == "Plantera", "a window survives the file format (" + (back == null ? "unreadable" : "fps " + back.Fps) + ")");
			Chk(back != null && back.Levy.Count == 1 && back.Levy[0].Key == "ai" && back.Top.Count == w0.Top.Count && back.Top.Any(t => t.Cat == PerfCat.Map && t.Name == "MagicStorage/PortableAccessRangeDisplay" && Math.Abs(t.Ms - 3360) < 1), "its buckets and its list of who costs time survive too");
			Chk(!json.Contains('\n') && PerfWindow.FromJson("{\"k\":\"hdr\"}") == null && PerfWindow.FromJson("not json") == null, "a window is one line; other lines and garbage read as nothing");
			Chk(json.Length < 1800, "a window is small (" + json.Length + " bytes)");

			// ---- the decision
			var smooth = PerfAnalysis.Judge(Fight(20, 60, 60, 0.2, "CalamityMod", 0.1));
			Chk(!smooth.Laggy && !smooth.ShowPopup && smooth.Kind == LagKind.Smooth, "a smooth fight shows no popup");
			var levy = PerfAnalysis.Judge(Fight(20, 30, 60, 0.45, "CalamityMod", 0.1));
			Chk(levy.Laggy && levy.Kind == LagKind.Levy && levy.ShowPopup, "laggy and mostly Levy: the army is named (" + levy.Kind + ", Levy " + (int)(levy.LevyShare * 100) + "%)");
			Chk(Opts(levy) == "LowerLevyEffects,ItsFine,DontAskAgain,OpenSettings", "Levy's popup offers: lower effects, it's fine, don't ask again, open settings (" + Opts(levy) + ")");
			Chk(levy.Detail.Length > 0 && levy.Detail[0] == "Your army was a big part of the cost." && levy.Headline == "That fight got laggy.", "and says so in the words asked for");
			var other = PerfAnalysis.Judge(Fight(20, 30, 60, 0.1, "CalamityMod", 0.55));
			Chk(other.Laggy && other.Kind == LagKind.OtherMod && other.TopMod == "CalamityMod", "laggy and mostly another mod: that mod is named (" + other.Kind + " " + other.TopMod + ")");
			Chk(other.Detail.Length > 0 && other.Detail[0] == "Most of the cost came from CalamityMod.", "in plain words (" + (other.Detail.Length > 0 ? other.Detail[0] : "none") + ")");
			Chk(Opts(other) == "ItsFine,DontAskAgain,OpenSettings", "another mod's popup offers only it's fine, don't ask again and open settings (" + Opts(other) + ")");
			var both = PerfAnalysis.Judge(Fight(20, 30, 60, 0.35, "CalamityMod", 0.6));
			Chk(both.Kind == LagKind.OtherMod && both.TopMod == "CalamityMod", "Levy at 35% against a mod at 60%: the mod is named");
			var close = PerfAnalysis.Judge(Fight(20, 30, 60, 0.35, "CalamityMod", 0.3));
			Chk(close.Kind == LagKind.Levy, "Levy at 35% against a mod at 30%: the army is named");
			var baseG = PerfAnalysis.Judge(Fight(20, 30, 60, 0.0, null, 0));
			Chk(baseG.Kind == LagKind.BaseGame && Opts(baseG) == "ItsFine,DontAskAgain,OpenSettings" && baseG.Detail[0].Contains("base game"), "laggy with nothing but the base game: the base game is named (" + baseG.Kind + ")");
			// ---- Frame Skip On: the frames are dropped and the updates kept (Eve's 11-17 pillar fights: 58 updates, 2 to 5 frames a second)
			List<PerfWindow> Fs(int mode, double fps, double ups) { var l = Fight(20, fps, ups, 0.45, "CalamityMod", 0.1); foreach (var fw in l) fw.FrameSkip = mode; return l; }
			var fsOn = PerfAnalysis.Judge(Fs(1, 3, 58));
			Chk(fsOn.FrameSkipOn && Opts(fsOn) == "FrameSkipSubtle,LowerLevyEffects,ItsFine,DontAskAgain,OpenSettings" && fsOn.Detail.Any(d => d.StartsWith("Frame Skip is On")), "Frame Skip On with frames dropped and updates kept: Subtle is offered first (" + Opts(fsOn) + ")");
			Chk(!PerfAnalysis.Judge(Fs(2, 3, 58)).FrameSkipOn && !PerfAnalysis.Judge(Fs(0, 3, 58)).FrameSkipOn && !PerfAnalysis.Judge(Fs(-1, 3, 58)).FrameSkipOn, "not with Frame Skip Subtle, Off or not known");
			Chk(!PerfAnalysis.Judge(Fs(1, 30, 40)).FrameSkipOn, "not when the updates were slow too (the game was slowing down, not dropping frames)");
			Chk(fsOn.Detail.All(d => d.Length <= 140), "and the lines stay short");
			var wfs = Fs(1, 3, 58)[0]; var wfs2 = PerfWindow.FromJson(wfs.ToJson());
			Chk(wfs2 != null && wfs2.FrameSkip == 1 && PerfWindow.FromJson(wfs.ToJson().Replace(",\"fs\":1", "")).FrameSkip == -1, "the log keeps the Frame Skip setting, and an older line reads as not known");
			var none = new List<PerfWindow>(); for (int i = 0; i < 20; i++) { var wn = PerfAnalysis.Synth(8, 30, 60, 0, null, 0); wn.Top.Clear(); none.Add(wn); }
			var unk = PerfAnalysis.Judge(none);
			Chk(unk.Laggy && unk.Kind == LagKind.Unknown && !unk.ShowPopup, "laggy with nothing attributed: no popup (a popup that blames nobody helps nobody)");
			// how much of the fight was laggy
			var part = Fight(20, 60, 60, 0.45, "CalamityMod", 0.1); for (int i = 0; i < 5; i++) { part[i] = PerfAnalysis.Synth(8, 30, 60, 0.45, "CalamityMod", 0.1); }
			Chk(PerfAnalysis.Judge(part).Laggy, "a quarter of the fight under 40 fps is laggy");
			var part2 = Fight(20, 60, 60, 0.45, "CalamityMod", 0.1); for (int i = 0; i < 3; i++) { part2[i] = PerfAnalysis.Synth(8, 30, 60, 0.45, "CalamityMod", 0.1); }
			Chk(!PerfAnalysis.Judge(part2).Laggy, "fifteen percent of the fight under 40 fps is not");
			var upsLag = PerfAnalysis.Judge(Fight(20, 60, 50, 0.45, "CalamityMod", 0.1));
			Chk(upsLag.Laggy, "updates under 55 a second are laggy even at 60 frames");
			Chk(PerfAnalysis.Judge(Fight(2, 20, 60, 0.45, "CalamityMod", 0.1)).Kind == LagKind.TooShort, "a fight of sixteen seconds is too short to judge");
			var unfocused = Fight(20, 12, 20, 0.45, "CalamityMod", 0.1); foreach (var w in unfocused) w.Focus = false;
			Chk(!PerfAnalysis.Judge(unfocused).Laggy, "windows with the game in the background are not counted");
			Chk(PerfAnalysis.Judge(new List<PerfWindow>()).Kind == LagKind.TooShort, "no windows at all is nothing to judge");

			// ---- the case the analyzer exists for: a map layer redrawing something every frame while a remote is held
			var ms = new List<PerfWindow>();
			for (int i = 0; i < 20; i++)
			{
				var w = new PerfWindow { Utc = DateTime.UtcNow, Secs = 8, Fps = 21, Ups = 60, UpdMs = 5, DrawMs = 28, Updates = 480, Frames = 168, Boss = "King Slime", Npcs = 8, Projs = 12 };
				w.Top.Add(new PerfItem(PerfCat.Map, "MagicStorage/PortableAccessRangeDisplay", 20.0 * 168, 168));
				w.Top.Add(new PerfItem(PerfCat.Npc, "Terraria", 0.2 * 480, 480 * 8));
				w.Top.Add(new PerfItem(PerfCat.Ui, "Terraria: Inventory", 1.5 * 168, 168));
				ms.Add(w);
			}
			var msv = PerfAnalysis.Judge(ms);
			Chk(msv.Laggy && msv.Kind == LagKind.OtherMod && msv.TopMod == "MagicStorage", "a map layer at 20 ms a frame names its mod (" + msv.Kind + " " + msv.TopMod + ")");
			Chk(string.Join(" ", msv.Detail).Contains("map layer MagicStorage/PortableAccessRangeDisplay: 20.0 ms per frame"), "and the layer, in ms per frame (" + string.Join(" | ", msv.Detail) + ")");
			var rep = PerfAnalysis.Report(PerfSpan.Of(ms));
			Chk(rep.Any(l => l.Contains("map layer MagicStorage/PortableAccessRangeDisplay: 20.0 ms per frame")) && rep[0].Contains("21.0 frames"), "/levyperf shows it as the biggest single cost");

			// ---- the options: never fewer command points, the reserve bank not offered until it exists
			bool reserve = PerfAnalysis.ReserveBankAvailable;
			try
			{
				PerfAnalysis.ReserveBankAvailable = false;
				Chk(!PerfAnalysis.Judge(Fight(20, 30, 60, 0.45, "CalamityMod", 0.1)).Options.Contains(PopupOption.ReserveBank), "the reserve bank is not offered while it is not built");
				PerfAnalysis.ReserveBankAvailable = true;
				var rb = PerfAnalysis.Judge(Fight(20, 30, 60, 0.45, "CalamityMod", 0.1));
				Chk(rb.Options.Contains(PopupOption.ReserveBank) && rb.Options[1] == PopupOption.ReserveBank, "the hook offers it second once it is switched on");
				Chk(!PerfAnalysis.Judge(Fight(20, 30, 60, 0.1, "CalamityMod", 0.55)).Options.Contains(PopupOption.ReserveBank), "and never in the other-mod popup");
			}
			finally { PerfAnalysis.ReserveBankAvailable = reserve; }
			var all = Enum.GetValues(typeof(PopupOption)).Cast<PopupOption>().Select(o => o.ToString()).ToList();
			Chk(all.Count == 6 && !all.Any(n => n.IndexOf("command", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("point", StringComparison.OrdinalIgnoreCase) >= 0), "no option about command points exists (" + string.Join(",", all) + ")");

			// ---- the popup's rules
			DateTime now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc), never = DateTime.MinValue;
			Chk(PerfAnalysis.Refusal(true, false, false, now, never, never) == null, "a popup may show on a fresh session");
			Chk(PerfAnalysis.Refusal(false, false, false, now, never, never) != null, "not with the setting off");
			Chk(PerfAnalysis.Refusal(true, true, false, now, never, never) != null, "not after don't ask again");
			Chk(PerfAnalysis.Refusal(true, false, true, now, never, never) != null, "not while a fight is on");
			Chk(PerfAnalysis.Refusal(true, false, false, now, now.AddSeconds(-300), never) != null && PerfAnalysis.Refusal(true, false, false, now, now.AddSeconds(-601), never) == null, "not twice within ten minutes");
			Chk(PerfAnalysis.Refusal(true, false, false, now, never, now.AddSeconds(1500)) != null && PerfAnalysis.Refusal(true, false, false, now, never, now.AddSeconds(-1)) == null, "not for half an hour after it's fine");

			// ---- the words: short declarative sentences, no capitals shouting
			var words = new List<string>(); words.AddRange(levy.Detail); words.AddRange(other.Detail); words.AddRange(baseG.Detail); words.AddRange(msv.Detail); words.Add(levy.Headline);
			words.AddRange(PerfAnalysis.Report(PerfSpan.Of(Fight(5, 30, 60, 0.4, "CalamityMod", 0.2)))); words.AddRange(PerfAnalysis.Report(PerfSpan.Of(new List<PerfWindow>())));
			foreach (PopupOption o in Enum.GetValues(typeof(PopupOption))) words.Add(PerfAnalysis.Label(o));
			Chk(!words.Any(s => Regex.IsMatch(s, "[A-Z]{4,}")) && !words.Any(s => s.Contains('!')), "no shouting in any of the words");
			Chk(levy.Detail.All(s => s.Length <= 140) && other.Detail.All(s => s.Length <= 200), "the popup's lines are short");

			// ---- the old recordings' profile events
			string prof = "{\"k\":\"e\",\"t\":242,\"e\":\"prof\",\"fps\":30,\"ups\":60,\"upd\":12,\"npc\":2.4,\"proj\":0.6,\"draw\":6.2,\"ai\":6.5,\"gear\":0.5,\"top\":\"ai 99.3, stats 56.1, dmg 20.6\",\"win\":2}";
			var lw = PerfWindow.FromRecProf(prof, "Plantera", DateTime.UtcNow);
			Chk(lw != null && lw.Legacy && lw.Fps == 30 && Math.Abs(lw.LegacyLevyMs - 7.0) < 1e-9 && lw.Levy.Count == 3 && PerfWindow.FromRecProf("{\"k\":\"e\",\"e\":\"hit\"}", "x", DateTime.UtcNow) == null, "a profile event of an old recording reads as a window");
			var lf = new List<PerfWindow>(); for (int i = 0; i < 20; i++) lf.Add(PerfWindow.FromRecProf(prof, "Plantera", DateTime.UtcNow));
			var lv = PerfAnalysis.Judge(lf);
			Chk(lv.Laggy && lv.Kind == LagKind.Levy && lv.Options.Contains(PopupOption.LowerLevyEffects), "an old recording where Levy's own work is over 30% of the time still names the army (" + lv.Kind + " " + (int)(lv.LevyShare * 100) + "%)");
			string prof2 = prof.Replace("\"ai\":6.5", "\"ai\":1.0").Replace("\"gear\":0.5", "\"gear\":0.1");
			var lf2 = new List<PerfWindow>(); for (int i = 0; i < 20; i++) lf2.Add(PerfWindow.FromRecProf(prof2, "Plantera", DateTime.UtcNow));
			var lv2 = PerfAnalysis.Judge(lf2);
			Chk(lv2.Laggy && lv2.Kind == LagKind.Unknown && !lv2.ShowPopup, "an old recording where Levy's own work is small has no popup: it cannot name the cause");

			// ---- the rolling log
			string tmp = null;
			try
			{
				tmp = Path.Combine(Path.GetTempPath(), "levy_perflog_" + Guid.NewGuid().ToString("N").Substring(0, 8)); string dir = PerfLogFile.Dir(tmp);
				DateTime d0 = new DateTime(2026, 11, 16, 9, 0, 0);
				string line = w0.ToJson();
				string f1 = PerfLogFile.Append(dir, d0, new[] { line, line }, 1_000_000);
				Chk(Path.GetFileName(f1) == "levyperf-2026-11-16.jsonl" && File.ReadAllLines(f1).Length == 2, "a day's lines go to that day's file");
				string f2 = PerfLogFile.Append(dir, d0, new[] { line }, 1_000_000);
				Chk(f2 == f1 && File.ReadAllLines(f1).Length == 3, "later lines are appended");
				string f3 = PerfLogFile.Append(dir, d0, new[] { line }, 500); // the file is over 500 bytes
				Chk(Path.GetFileName(f3) == "levyperf-2026-11-16_2.jsonl", "a file past its size rolls to the next numbered one (" + Path.GetFileName(f3) + ")");
				string f4 = PerfLogFile.Append(dir, d0, new[] { line }, 500);
				Chk(Path.GetFileName(f4) == "levyperf-2026-11-16_3.jsonl" || Path.GetFileName(f4) == "levyperf-2026-11-16_2.jsonl", "and on to a third when that one is full too");
				Chk(PerfLogFile.ReadAll(dir).Count == 5, "every window written reads back (" + PerfLogFile.ReadAll(dir).Count + ")");
				// age
				for (int d = 1; d <= 9; d++) File.WriteAllText(Path.Combine(dir, "levyperf-" + d0.AddDays(-d).ToString("yyyy-MM-dd") + ".jsonl"), new string('x', 1000));
				int gone = PerfLogFile.Enforce(dir, 7, long.MaxValue, d0, f1, out long left);
				var names = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
				Chk(gone == 3 && !names.Contains("levyperf-2026-11-07.jsonl") && names.Contains("levyperf-2026-11-10.jsonl"), "files older than seven days go and the rest stay (removed " + gone + ")");
				// size
				long total = Directory.GetFiles(dir).Sum(f => new FileInfo(f).Length);
				int gone2 = PerfLogFile.Enforce(dir, 7, 4000, d0, f1, out long left2);
				var names2 = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
				Chk(gone2 > 0 && left2 <= 4000 + new FileInfo(f1).Length && names2.Contains(Path.GetFileName(f1)), "over the size cap the oldest go first and today's file is kept (removed " + gone2 + ", " + total + " to " + left2 + " bytes)");
				Chk(!names2.Contains("levyperf-2026-11-09.jsonl") || names2.Contains("levyperf-2026-11-15.jsonl"), "the newest of the old files outlast the oldest");
				Chk(PerfLogFile.DayOf("levyperf-2026-11-16_2.jsonl") == new DateTime(2026, 11, 16) && PerfLogFile.DayOf("other.txt") == null, "the date in a file name reads back");
			}
			catch (Exception e) { fails.Add("log check threw " + e.GetType().Name + ": " + e.Message); }
			finally { try { if (tmp != null) Directory.Delete(tmp, true); } catch (Exception) { } }
			return pass;
		}
	}
}
