using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Levy.Common.Recording
{
	/// <summary>
	/// The analyzer's self-check, pure data (no game types) so the mod runs it at boot and the command-line tool runs the very same assertions (RecReport --selftest).
	/// A scripted fight with known numbers is written as a recording in the current format, read back with the real reader and checked number for number (damage per second, stealth share, crit rate per weapon, what killed the
	/// commander, the army's start, end and break, who the boss went for, the chat lines word for word); the same fight in the format of older files (no stealth, crit or weapon fields) must read without those numbers and
	/// without an error; a stable fight must not break; the line-breaking detector is fed a collapse, a morale-only collapse and a steady fight; the history of earlier fights is written, read back and compared.
	/// </summary>
	internal static class FightReportSelfTest
	{
		private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

		// the scripted fight: 60 seconds, 20 levies, a lost Plantera fight
		internal sealed class Script
		{
			public bool Ext = true;          // format 10 rows (stealth, crit, weapon) and stealth samples, or the older rows
			public int Levies = 20;          // levies at the start
			public bool Collapse = true;     // eight shaken from 20 s, fourteen fall from 24 s on, the commander dies at 58 s
			public int Seconds = 60;
			public bool Acc;                 // accessory-effect hits (an accessory source, one with no source) and "N% life" phase lines beside the milestones
		}

		internal static List<string> Build(Script sc)
		{
			var L = new List<string>(); int fmt = sc.Ext ? 10 : 9;
			L.Add("{\"k\":\"hdr\",\"format\":" + fmt + ",\"boss\":\"Plantera\",\"player\":\"Tester\",\"mode\":\"Master+Death\",\"world\":\"W\",\"date\":\"2026-11-16T11:18:52\",\"stateNames\":\"Idle,Follow,Patrol,Engage,Haul,Resupply,Shaken,Routed,ReturnHome,Dead,Desert,SearchDestroy,FreeRein,Mutiny\",\"auto\":1,\"army\":{\"n\":" + sc.Levies + "}}");
			int ticks = sc.Seconds * 60; int alive = sc.Levies; var deathAt = new List<int>();
			if (sc.Collapse) for (int i = 0; i < 14; i++) deathAt.Add(24 * 60 + i * 80);
			else for (int i = 0; i < 3; i++) deathAt.Add(10 * 60 + i * 1000);
			var events = new SortedDictionary<int, List<string>>();
			void Ev(int t, string json) { if (!events.TryGetValue(t, out var l)) events[t] = l = new List<string>(); l.Add(json); }
			Ev(0, "{\"k\":\"e\",\"t\":0,\"e\":\"tgt\",\"kind\":2,\"levy\":5,\"boss\":1}");
			if (sc.Collapse) Ev(40 * 60, "{\"k\":\"e\",\"t\":2400,\"e\":\"tgt\",\"kind\":1,\"levy\":-1,\"boss\":1}");
			foreach (int d in deathAt) Ev(d, "{\"k\":\"e\",\"t\":" + d + ",\"e\":\"ldeath\",\"id\":" + (d % 1000) + ",\"x\":0,\"y\":0,\"npc\":262,\"by\":\"boss\"}");
			// one damage event a second from the tenth to the sixtieth second
			for (int sec = 10; sec <= sc.Seconds; sec++)
			{
				int t = sec * 60;
				string rows = sc.Ext
					? "[\"cmd\",\"rogue\",\"proj\",9001,1,3000,3,1000,1,1],[\"cmd\",\"rogue\",\"proj\",9001,1,500,5,1000,0,2],[\"cmd\",\"melee\",\"item\",2000,1,700,7,2000,0,1],[\"levy\",\"ranged\",\"proj\",14,1,2000,20,0,0,0],[\"cmd\",\"rogue\",\"proj\",9001,0,90,1,1000,0,0]"
					: "[\"cmd\",\"rogue\",\"proj\",9001,1,3500,8],[\"cmd\",\"melee\",\"item\",2000,1,700,7],[\"levy\",\"ranged\",\"proj\",14,1,2000,20],[\"cmd\",\"rogue\",\"proj\",9001,0,90,1]";
				if (sc.Acc && sc.Ext) rows += ",[\"cmd\",\"rogue\",\"proj\",9100,1,400,1,5000,0,0],[\"cmd\",\"melee\",\"proj\",9101,1,343,1,0,0,0]";
				Ev(t, "{\"k\":\"e\",\"t\":" + t + ",\"e\":\"dmg\",\"d\":[" + rows + "]}");
				if (sc.Ext) Ev(t, "{\"k\":\"e\",\"t\":" + t + ",\"e\":\"stl\",\"s\":" + (sec % 20 < 10 ? 100 : 40) + ",\"m\":80}");
			}
			if (sc.Collapse)
			{
				Ev(50 * 60 + 5, "{\"k\":\"e\",\"t\":3005,\"e\":\"hit\",\"dmg\":100,\"npc\":-1,\"proj\":777,\"other\":-1,\"life\":400}");
				Ev(52 * 60, "{\"k\":\"e\",\"t\":3120,\"e\":\"hit\",\"dmg\":100,\"npc\":-1,\"proj\":777,\"other\":-1,\"life\":300}");
				Ev(54 * 60, "{\"k\":\"e\",\"t\":3240,\"e\":\"hit\",\"dmg\":200,\"npc\":262,\"proj\":-1,\"other\":-1,\"life\":100}");
				Ev(57 * 60 + 50, "{\"k\":\"e\",\"t\":3470,\"e\":\"hit\",\"dmg\":200,\"npc\":262,\"proj\":-1,\"other\":-1,\"life\":0}");
				Ev(58 * 60, sc.Ext ? "{\"k\":\"e\",\"t\":3480,\"e\":\"cdeath\",\"x\":0,\"y\":0,\"n\":1,\"kn\":262,\"kp\":-1,\"ko\":-1,\"kc\":\"\",\"kd\":200}" : "{\"k\":\"e\",\"t\":3480,\"e\":\"cdeath\",\"x\":0,\"y\":0,\"n\":1}");
				Ev(18 * 60, "{\"k\":\"e\",\"t\":1080,\"e\":\"lbw\",\"why\":\"morale\",\"n\":20,\"broken\":8,\"deaths\":0}");
			}
			int ei = 0; var evKeys = events.Keys.ToList();
			for (int t = 0; t <= ticks; t += 2)
			{
				while (ei < evKeys.Count && evKeys[ei] <= t) { foreach (string e in events[evKeys[ei]]) L.Add(e); ei++; }
				int sec = t / 60; int dead = deathAt.Count(d => d <= t); int n = Math.Max(0, sc.Levies - dead - (sc.Collapse && sec >= 50 ? 0 : 0));
				if (sc.Collapse && dead >= 14) n = Math.Max(0, 6 - (sec - 40) / 3); // the last six fall away after the fourteenth death
				if (sc.Collapse && sec >= 55) n = 0;
				int shaken = sc.Collapse && sec >= 20 ? Math.Min(8, n) : 0;
				var sb = new StringBuilder("{\"k\":\"f\",\"t\":" + t + ",\"p\":[0,0,0,0,1,1,500,200,0,0,0," + (sec < 30 ? 1000 : 2000) + ",-1,0,0,0],\"b\":[[262,1,0,0,0,0," + Math.Max(200, 10000 - 9800 * t / ticks) + ",10000,0,0,0,0,0,0,1,-1,86,86]],\"l\":[");
				for (int i = 0; i < n; i++) { if (i > 0) sb.Append(','); sb.Append("[" + (100 + i) + ",0,0,0,0,0,400," + (i < shaken ? 6 : 1) + ",0,0,500,20,42]"); }
				sb.Append("]}"); L.Add(sb.ToString());
			}
			while (ei < evKeys.Count) { foreach (string e in events[evKeys[ei]]) L.Add(e); ei++; }
			L.Add("{\"k\":\"e\",\"t\":" + ticks + ",\"e\":\"bdespawn\",\"npc\":262,\"name\":\"Plantera\",\"id\":1,\"x\":0,\"y\":0,\"life\":200,\"max\":10000,\"final\":1}");
			if (sc.Acc) { L.Add("{\"k\":\"e\",\"t\":" + (12 * 60) + ",\"e\":\"bph\",\"pct\":75}"); L.Add("{\"k\":\"e\",\"t\":" + (30 * 60) + ",\"e\":\"bph\",\"pct\":50,\"what\":\"Plantera enters phase two\"}"); }
			L.Add("{\"k\":\"names\",\"items\":{\"1000\":\"Iron Francisca\",\"2000\":\"Spear of Destiny\",\"5000\":\"Abyssal Mirror\",\"6000\":\"Advanced Portable Remote Storage Access\"},\"nonweapons\":[5000,6000],\"npcs\":{\"262\":\"Plantera\"},\"projectiles\":{\"777\":\"Spore Gas\",\"9001\":\"Francisca\"},\"buffs\":{},\"soldiers\":{},\"prefixes\":{}}");
			L.Add("{\"k\":\"end\",\"reason\":\"boss gone\",\"ticks\":" + ticks + ",\"frames\":" + (ticks / 2) + ",\"events\":0,\"outcome\":\"despawn\",\"result\":\"" + (sc.Collapse ? "lost" : "won") + "\",\"durationSec\":" + sc.Seconds + ",\"bossName\":\"Plantera\",\"bosses\":1,\"cmdDeaths\":" + (sc.Collapse ? 1 : 0) + ",\"armyAlive\":0,\"armyTotal\":" + sc.Levies + ",\"dmgBoss\":0,\"dmgAll\":0}");
			return L;
		}


		/// <summary>Runs the checks. Returns how many passed; every failure is added to <paramref name="fails"/>.</summary>
		public static int Run(List<string> fails)
		{
			int pass = 0;
			void Chk(bool c, string w) { if (c) pass++; else fails.Add(w); }
			string tmp = null;
			try
			{
				// ---- the current format: every number
				var s = FightRecReader.ReadLines(Build(new Script()));
				Chk(s.Format == 10 && s.Boss == "Plantera" && s.Result == "lost" && s.CmdDeaths == 1, "header and end: " + s.Format + " " + s.Boss + " " + s.Result + " " + s.CmdDeaths);
				Chk(Math.Abs(s.Dps - 4200) < 0.5 && Math.Abs(s.ActiveSec - 51) < 0.01 && Math.Abs(s.FightDps - 4200 * 51 / 60.0) < 0.5, "damage per second while hitting is 4200 over 51 s, got " + F(s.Dps) + " over " + F(s.ActiveSec) + " s, " + F(s.FightDps) + " over the fight");
				Chk(s.StealthKnown && s.CritKnown && Math.Abs(s.StealthShare - 3000.0 / 4200.0) < 1e-6, "stealth share is 3000 of 4200, got " + F(s.StealthShare));
				Chk(s.Weapons.TryGetValue("Iron Francisca", out var fw) && fw.Hits == 8 * 51 && fw.StealthHits == 3 * 51 && fw.Crits == 3 * 51 && fw.CritKnownHits == 8 * 51, "Iron Francisca: hits, stealth hits and crits");
				Chk(fw != null && fw.StealthDmg == 3000 * 51 && fw.Dmg == 3500 * 51, "Iron Francisca: stealth damage " + (fw?.StealthDmg ?? -1) + " of " + (fw?.Dmg ?? -1));
				Chk(s.Weapons.TryGetValue("Spear of Destiny", out var sw) && sw.Hits == 7 * 51 && sw.StealthHits == 0 && sw.Crits == 51, "Spear of Destiny: hits and crits");
				Chk(s.CmdAddDmg == 90 * 51 && s.LevyBossDmg == 2000 * 51, "adds and the army's damage kept apart");
				Chk(s.StealthBarSeen && s.StealthPct.Count == 51 && s.StealthPct.Count(p => p >= 99) == 21, "stealth bar samples read back (" + s.StealthPct.Count + ", full " + s.StealthPct.Count(p => p >= 99) + ")");
				Chk(s.TakenHits == 4 && s.TakenTotal == 600 && s.Taken.ContainsKey("Spore Gas") && s.Taken["Spore Gas"].Dmg == 200 && s.Taken.ContainsKey("Plantera (contact)") && s.Taken["Plantera (contact)"].Deaths == 1, "damage taken by source");
				Chk(s.Deaths.Count == 1 && s.Deaths[0].Killer == "Plantera (contact)" && s.Deaths[0].ArmyAt == 0, "killed by Plantera (contact) with no army left: " + (s.Deaths.Count > 0 ? s.Deaths[0].Killer + " " + s.Deaths[0].ArmyAt : "none"));
				Chk(s.ArmyStart == 20 && s.ArmyEnd == 0 && s.LeviesLost == 14, "army 20 to 0, 14 fell: " + s.ArmyStart + " " + s.ArmyEnd + " " + s.LeviesLost);
				Chk(Math.Abs(s.BreakSec - 23) < 0.01 && s.BreakWhy == "morale", "the line broke at 23 s by morale, got " + F(s.BreakSec) + " " + s.BreakWhy);
				Chk(Math.Abs(s.WarnSec - 18) < 0.01, "the recorded warning is read (" + F(s.WarnSec) + ")");
				Chk(Math.Abs(s.BossLeftFrac - 0.02) < 1e-9, "the boss had 2% left, got " + F(s.BossLeftFrac));
				var prior = new List<HistoryEntry>();
				var lines = FightReport.Summary(s, prior);
				Chk(lines.Count == 3, "three summary lines, got " + lines.Count);
				Chk(lines.Count > 0 && lines[0] == "Plantera, lost at 2%. Your damage: 3,570 per second. Stealth strikes 71% of it.", "line 1: " + (lines.Count > 0 ? lines[0] : ""));
				Chk(lines.Count > 1 && lines[1] == "Killed by Plantera (contact) after the army fell.", "line 2: " + (lines.Count > 1 ? lines[1] : ""));
				Chk(lines.Count > 2 && lines[2] == "Army: 20 to 0, broke at 0:23.", "line 3: " + (lines.Count > 2 ? lines[2] : ""));
				Chk(lines.All(l => l.Length <= 90 && l == l.TrimEnd() && !l.Any(c => c > 126) && l.ToUpperInvariant() != l), "lines are short, plain and not shouting");
				prior.Add(new HistoryEntry { File = "a.levyrec", Boss = "Plantera", Date = "2026-11-15T10:00:00", Result = "won", Dps = 5000, Left = 0 });
				prior.Add(new HistoryEntry { File = "b.levyrec", Boss = "Plantera", Date = "2026-11-16T09:00:00", Result = "lost", Dps = 3900, Left = 0.2 });
				var l2 = FightReport.Summary(s, prior);
				Chk(l2.Count == 4 && l2[3] == "Your best against Plantera: 5,000 per second. Last time: 3,900.", "line 4 compares with the best and the last: " + (l2.Count > 3 ? l2[3] : "none"));
				string full = FightReport.Full(s, prior);
				foreach (string want in new[] { "Plantera, lost at 2%", "3,570 damage per second over the whole fight", "Stealth strikes: 71% of your damage", "Iron Francisca", "Spear of Destiny", "Spore Gas", "Plantera (contact)", "The line broke at 0:23", "The warning came at 0:18", "On you 33%", "Morale break", "Strength line", "Best: 5,000 per second", "Last: 3,900 per second" })
					Chk(full.Contains(want), "the full report says '" + want + "'");
				Chk(!full.Contains("not recorded"), "a current recording has nothing marked not recorded");

				// ---- the format of older files: the same fight with no stealth, crit or weapon fields
				var o = FightRecReader.ReadLines(Build(new Script { Ext = false }));
				Chk(!o.StealthKnown && !o.CritKnown && !o.StealthBarSeen && o.Format == 9, "an older file has no stealth or crit numbers");
				Chk(Math.Abs(o.Dps - 4200) < 0.5 && Math.Abs(o.FightDps - 3570) < 0.5 && o.Weapons.Count == 2, "an older file still gives damage per second and weapons from the held item (" + o.Weapons.Count + ")");
				var ol = FightReport.Summary(o, new List<HistoryEntry>());
				Chk(ol.Count == 3 && ol[0] == "Plantera, lost at 2%. Your damage: 3,570 per second.", "an older file's first line has no stealth sentence: " + (ol.Count > 0 ? ol[0] : ""));
				string ofull = FightReport.Full(o, null);
				Chk(ofull.Contains("not recorded in this file") && ofull.Contains("first recorded fight"), "an older file's report says what was not recorded");

				// ---- accessory effects are not weapons, and a life milestone is listed once
				var ac = FightRecReader.ReadLines(Build(new Script { Acc = true }));
				Chk(ac.Weapons.TryGetValue("Accessory effects (Abyssal Mirror)", out var am) && am.Hits == 51 && am.Dmg == 400 * 51, "a hit sourced to an accessory is grouped as its accessory effect");
				Chk(ac.Weapons.TryGetValue("Accessory effects", out var ae) && ae.Dmg == 343 * 51 && !ac.Weapons.ContainsKey("Advanced Portable Remote Storage Access") && !ac.Weapons.ContainsKey("Abyssal Mirror"), "a hit with no recorded source is not charged to the held item");
				Chk(ac.Weapons.TryGetValue("Iron Francisca", out var af) && af.Dmg == 3500 * 51 && ac.Weapons.TryGetValue("Spear of Destiny", out var asp) && asp.Dmg == 700 * 51, "real weapons keep their own damage");
				string afull = FightReport.Full(ac, null);
				Chk(!afull.Contains("0:12: 75% life") && afull.Contains("Plantera enters phase two") && afull.Contains("Down to"), "a life milestone appears once, with the levy count, and a named phase stays");

				// ---- a steady fight never breaks
				var st = FightRecReader.ReadLines(Build(new Script { Collapse = false }));
				Chk(st.BreakSec < 0 && st.Result == "won", "a steady fight does not break (" + F(st.BreakSec) + ")");
				var sl = FightReport.Summary(st, new List<HistoryEntry>());
				Chk(sl.Count >= 2 && sl[0].StartsWith("Plantera, won in 1:00.") && sl.Any(x => x == "Army: 20 to 17, held."), "a won fight reads 'won in' and 'held': " + string.Join(" / ", sl));

				// ---- the kill rule on old files (recwin): the commander fell at 9334, her in-flight shots took the Devourer of Gods to 14542 of 1836000, Calamity removed it, and the file said killed / won. A file with no
				// "kh" field has no proof of a death: the reader says the boss left and the fight was lost. A real win (Calamity's death animation leaves life 1) and any file with kh keep their result.
				List<string> Dog(string cdeath, string bend, string outcome, string result) => new List<string> {
					"{\"k\":\"hdr\",\"format\":12,\"boss\":\"The_Devourer_of_Gods\",\"player\":\"Tester\",\"mode\":\"Master\",\"world\":\"W\",\"date\":\"2026-11-20T15:19:00\",\"auto\":1}",
					cdeath, bend,
					"{\"k\":\"names\",\"items\":{},\"npcs\":{\"796\":\"The Devourer of Gods\"},\"projectiles\":{},\"buffs\":{},\"soldiers\":{},\"prefixes\":{}}",
					"{\"k\":\"end\",\"reason\":\"boss gone\",\"ticks\":9684,\"frames\":4842,\"events\":3,\"outcome\":\"" + outcome + "\",\"result\":\"" + result + "\",\"durationSec\":161.4,\"bossName\":\"The Devourer of Gods\",\"bosses\":102,\"cmdDeaths\":" + (cdeath.Contains("cdeath") ? 1 : 0) + "}" };
				const string cd = "{\"k\":\"e\",\"t\":9334,\"e\":\"cdeath\",\"x\":22368.5,\"y\":4812.6,\"n\":1}";
				var falseWin = FightRecReader.ReadLines(Dog(cd, "{\"k\":\"e\",\"t\":9384,\"e\":\"bdeath\",\"npc\":796,\"name\":\"The Devourer of Gods\",\"id\":190,\"x\":0,\"y\":0,\"life\":14542,\"max\":1836000,\"final\":1,\"ht\":9351,\"hd\":2792,\"hs\":\"other\"}", "killed", "won"));
				Chk(falseWin.Result == "lost" && falseWin.Outcome == "despawn" && falseWin.Rederived, "the old Devourer false win reads boss left, lost: " + falseWin.Result + " " + falseWin.Outcome);
				Chk(FightReport.Outcome(falseWin) != null && !FightReport.Outcome(falseWin).StartsWith("won"), "its report line does not say won: " + FightReport.Outcome(falseWin));
				var realWin = FightRecReader.ReadLines(Dog("{\"k\":\"e\",\"t\":5,\"e\":\"tgt\",\"kind\":1,\"levy\":0,\"boss\":1}", "{\"k\":\"e\",\"t\":10153,\"e\":\"bdeath\",\"npc\":795,\"name\":\"The Devourer of Gods\",\"id\":69,\"x\":0,\"y\":0,\"life\":1,\"max\":1836000,\"final\":1,\"ht\":10153,\"hd\":10,\"hs\":\"other\"}", "killed", "won"));
				Chk(realWin.Result == "won" && realWin.Outcome == "killed" && !realWin.Rederived, "the real Devourer win (life 1, no commander death) stays won: " + realWin.Result);
				var hooked = FightRecReader.ReadLines(Dog(cd, "{\"k\":\"e\",\"t\":9384,\"e\":\"bdeath\",\"npc\":796,\"name\":\"The Devourer of Gods\",\"id\":190,\"x\":0,\"y\":0,\"life\":14542,\"max\":1836000,\"final\":1,\"kh\":1,\"ht\":9351,\"hd\":2792,\"hs\":\"other\"}", "killed", "won"));
				Chk(hooked.Result == "won" && !hooked.Rederived, "a file that says the death hook ran is believed whatever the life: " + hooked.Result);
				var newLeft = FightRecReader.ReadLines(Dog(cd, "{\"k\":\"e\",\"t\":9384,\"e\":\"bdespawn\",\"npc\":796,\"name\":\"The Devourer of Gods\",\"id\":190,\"x\":0,\"y\":0,\"life\":14542,\"max\":1836000,\"final\":1,\"kh\":0,\"ht\":9351,\"hd\":2792,\"hs\":\"other\"}", "despawn", "lost"));
				Chk(newLeft.Result == "lost" && newLeft.Outcome == "despawn" && !newLeft.Rederived && Math.Abs(newLeft.BossLeftFrac - 14542.0 / 1836000) < 1e-9, "a new file's bdespawn is read as it is written: " + newLeft.Result);

				// ---- the detector alone
				var d = new LineBreakDetector(); int fired = 0, firedAt = -1;
				for (int sec = 0; sec < 120; sec++) { if (d.Feed(sec * 60, 30, sec % 17 == 0 ? 3 : 0, sec / 40)) { fired++; firedAt = sec; } }
				Chk(fired == 0, "30 levies, a few shaken now and then, three casualties in two minutes: no warning");
				d.Reset(); fired = 0;
				for (int sec = 0; sec < 120; sec++) { int dead = sec < 20 ? 0 : Math.Min(30, (sec - 20) * 2); if (d.Feed(sec * 60, 30 - dead, 0, dead)) { fired++; firedAt = sec; } }
				Chk(fired == 1 && firedAt >= 25 && firedAt <= 28 && d.Why == "attrition", "a collapse by deaths fires once, when twelve have fallen (at " + firedAt + " by " + d.Why + ")");
				d.Reset(); fired = 0;
				for (int sec = 0; sec < 120; sec++) { if (d.Feed(sec * 60, 20, sec < 30 ? 0 : 9, 0)) { fired++; firedAt = sec; } }
				Chk(fired == 1 && firedAt == 33 && d.Why == "morale", "nine of twenty shaken for three seconds fires once (at " + firedAt + ")");
				d.Reset(); fired = 0;
				for (int sec = 0; sec < 60; sec++) { if (d.Feed(sec * 60, 20, (sec / 2) % 2 == 0 ? 9 : 0, 0)) fired++; }
				Chk(fired == 0, "morale that flickers on and off for less than three seconds does not fire");
				d.Reset(); fired = 0;
				for (int sec = 0; sec < 60; sec++) { if (d.Feed(sec * 60, 4, 4, sec)) fired++; }
				Chk(fired == 0, "four levies are not a line");

				// ---- history: write, read back, find earlier fights by boss
				tmp = Path.Combine(Path.GetTempPath(), "levy_fightreport_check_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp);
				var he = FightHistory.FromStats(s, "2026-11-16_11-18_Plantera_lost.levyrec");
				FightHistory.Append(tmp, he);
				FightHistory.Append(tmp, new HistoryEntry { File = "2026-11-15_10-00_Golem_won.levyrec", Boss = "Golem", Result = "won", Dps = 1 });
				var back = FightHistory.ReadIndex(tmp);
				Chk(back.Count == 2 && back[0].Boss == "Plantera" && Math.Abs(back[0].Dps - 3570) < 0.5 && Math.Abs(back[0].Stealth - 0.714) < 0.001 && back[0].Army0 == 20, "history entries round-trip");
				var only = FightHistory.Load(tmp, "Plantera", "other.levyrec", 0);
				Chk(only.Count == 1 && only[0].File.Contains("Plantera"), "history is per boss");
				var ex = FightHistory.Load(tmp, "Plantera", "2026-11-16_11-18_Plantera_lost.levyrec", 0);
				Chk(ex.Count == 0, "the fight being reported is not its own history");

			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			finally { try { if (tmp != null) Directory.Delete(tmp, true); } catch (Exception) { } }
			return pass;
		}
	}
}
