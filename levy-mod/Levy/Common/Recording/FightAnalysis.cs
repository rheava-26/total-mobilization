using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Levy.Common.Recording
{
	// The fight analyzer: reads a finished recording (.levyrec, gzip, or plain .jsonl) into FightStats, the numbers the post-fight report and the chat summary are built from.
	// Pure data and System.* only (no game types), so the same code runs in the game after a fight, in the boot self-check, and in the command-line tool (levy-mod/tools/recreport) over a folder of old recordings.
	// A recording from before format 10 has no stealth, crit or weapon fields: those numbers come out as "not recorded" (Known flags), everything else is read the same way.

	/// <summary>One weapon of the commander: what it did to bosses.</summary>
	internal sealed class WeaponStat
	{
		public string Name; public int Item;
		public long Dmg, StealthDmg; public int Hits, StealthHits, Crits, CritKnownHits, Uses; public double ActiveSec;
		public bool StealthKnown, CritKnown;
		public long NormalDmg => Dmg - StealthDmg;
		public int NormalHits => Hits - StealthHits;
	}

	/// <summary>One kind of damage the commander took, grouped by its source label ("Spore Gas", "Plantera (contact)").</summary>
	internal sealed class TakenStat { public string Label; public int Hits; public long Dmg; public int Deaths; }

	internal struct ArmyPoint { public int Sec, N, Broken, Deaths; public double BossFrac; }

	internal sealed class DeathNote { public int Tick; public string Killer; public int ArmyAt; public int ArmyStart; }

	internal sealed class FightStats
	{
		public string File, Boss, Player, Mode, World, Date, Result = "ended", Outcome = "none";
		public int Format, CmdDeaths, Auto;
		public double DurationSec;
		public bool HasEnd;
		public string[] StateNames = { "Idle", "Follow", "Patrol", "Engage", "Haul", "Resupply", "Shaken", "Routed", "ReturnHome", "Dead", "Desert", "SearchDestroy", "FreeRein", "Mutiny" };
		public readonly Dictionary<int, string> Items = new(), Npcs = new(), Projs = new();

		// the commander's damage on bosses
		public readonly Dictionary<string, WeaponStat> Weapons = new();
		public long CmdBossDmg, CmdStealthDmg, CmdAddDmg, LevyBossDmg, DroneBossDmg, OtherBossDmg, AllBossDmg;
		public int CmdBossHits, CmdStealthHits, CmdCrits, CmdCritKnownHits;
		public double ActiveSec;
		public bool StealthKnown, CritKnown;

		// commander stealth bar, sampled once a second (percent of the bar): a Calamity rogue only
		public readonly List<int> StealthPct = new();
		public bool StealthBarSeen;

		// damage taken
		public readonly Dictionary<string, TakenStat> Taken = new();
		public long TakenTotal; public int TakenHits;
		public readonly List<DeathNote> Deaths = new();

		// the army, once a second: levies in the arena and how many of them are shaken, routed or deserting
		public readonly List<ArmyPoint> Army = new();
		public double LevyTime, BrokenTime;
		public int ArmyStart, ArmyEnd, ArmyPeak, LeviesLost;
		/// <summary>Second the line broke (the same detector the in-game warning uses), -1 when it held, and which signal fired.</summary>
		public double BreakSec = -1; public string BreakWhy;
		/// <summary>Second the in-game warning was shown, from the recording's lbw event (-1: none, or a recording made without the warning).</summary>
		public double WarnSec = -1;
		public readonly List<int> LevyDeathTicks = new();

		// who the boss is attacking: (tick, kind 0 none / 1 you / 2 levy / 3 spite, levy id)
		public readonly List<(int tick, int kind)> Target = new();

		// boss life
		public double BossLeftFrac = -1; public int FirstBossTick = -1;
		public readonly List<(int pct, int sec, int army)> Milestones = new();
		public readonly List<(int tick, string what)> Phases = new();
		public int EndTick;

		// internals while reading
		internal readonly List<int> FrameTicks = new(), FrameHeld = new();
		internal sealed class DmgRow { public string Owner, Cls, Src; public int Type, SrcItem, Ss, Crits; public bool Boss, Ext; public long Dmg; public int Hits; }
		internal sealed class DmgEvt { public int Tick; public List<DmgRow> Rows = new(); }
		internal readonly List<DmgEvt> DmgEvents = new();
		internal readonly List<(int tick, int dmg, string label)> HitEvents = new();
		internal readonly List<(int tick, string label)> KillNotes = new();
		internal readonly Dictionary<int, int> UseCount = new();
		internal bool Finalized;

		public static string Clock(double sec) { int s = (int)Math.Round(sec); return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture); }
		public static string N0(double v) => ((long)Math.Round(v)).ToString("N0", CultureInfo.InvariantCulture);

		/// <summary>Damage per second over the seconds the commander was hitting a boss (0 when never).</summary>
		public double Dps => ActiveSec > 0 ? CmdBossDmg / ActiveSec : 0;
		/// <summary>Damage per second over the whole fight.</summary>
		public double FightDps => DurationSec > 0 ? CmdBossDmg / DurationSec : 0;
		public double StealthShare => CmdBossDmg > 0 ? (double)CmdStealthDmg / CmdBossDmg : 0;
		public double BrokenShare => LevyTime > 0 ? BrokenTime / LevyTime : 0;
		public string ItemName(int t) => t <= 0 ? "Unknown" : Items.TryGetValue(t, out var n) ? n : "item " + t;
		public string NpcName(int t) => Npcs.TryGetValue(t, out var n) ? n : "enemy " + t;
		public string ProjName(int t) => Projs.TryGetValue(t, out var n) ? n : "projectile " + t;

		/// <summary>Name of a damage source for what hurt the commander: a projectile by name, an enemy's own body "(contact)", the world by kind.</summary>
		public string SourceLabel(int npc, int proj, int other)
		{
			if (proj > 0) return ProjName(proj);
			if (npc >= 0) return NpcName(npc) + " (contact)";
			return OtherName(other);
		}

		public static string OtherName(int other)
		{
			switch (other)
			{
				case 0: return "Falling";
				case 1: return "Drowning";
				case 2: return "Lava";
				case 3: return "Slain by something";
				case 6: return "Starving";
				case 7: return "Suffocating";
				case 8: return "Burning";
				case 9: return "Poison";
				case 10: return "Electricity";
				case 11: return "Wall of Flesh";
				case 12: return "Moon Lord";
				case 13: return "Wind";
				default: return other >= 0 ? "Another source (" + other + ")" : "Another source";
			}
		}

		/// <summary>Seconds into the fight, from a recording tick (60 per second).</summary>
		public static double Sec(int tick) => tick / 60.0;

		public bool IsBroken(int stateIndex)
		{
			if (stateIndex < 0 || stateIndex >= StateNames.Length) return false;
			string s = StateNames[stateIndex];
			return s == "Shaken" || s == "Routed" || s == "Desert";
		}
	}

	internal static class FightRecReader
	{
		/// <summary>Reads a recording. <paramref name="full"/> false skips the sample frames (damage, hits, deaths and the end record only): about ten times faster, enough for the history of earlier fights.</summary>
		public static FightStats Read(string path, bool full = true)
		{
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			Stream s = fs;
			bool gz = path.EndsWith(".levyrec", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".part", StringComparison.OrdinalIgnoreCase);
			if (gz) s = new GZipStream(fs, CompressionMode.Decompress);
			using var rd = new StreamReader(s);
			var st = ReadLines(Lines(rd), full);
			st.File = path;
			if (!st.HasEnd) st.Result = ResultFromName(path) ?? st.Result;
			return st;
		}

		private static IEnumerable<string> Lines(StreamReader r)
		{
			// a gzip stream cut short by a crash throws at the cut: everything before it is still read
			while (true)
			{
				string l;
				try { l = r.ReadLine(); } catch (Exception) { yield break; }
				if (l == null) yield break;
				yield return l;
			}
		}

		internal static string ResultFromName(string path)
		{
			string n = Path.GetFileNameWithoutExtension(path);
			foreach (string w in new[] { "won", "lost", "fled", "interrupted", "ended" }) if (n.EndsWith("_" + w) || n.Contains("_" + w + "_")) return w;
			return null;
		}

		/// <summary>The same reader over lines already in memory (the boot self-check and the synthetic fights).</summary>
		public static FightStats ReadLines(IEnumerable<string> lines, bool full = true)
		{
			var st = new FightStats();
			int prevFrame = -1, lastSec = -1, prevDmgTick = -1; int lastFrameTick = 0;
			double lastFrac = -1, lastBossLife = 0, lastBossMax = 0; int pendingEndTick = 0;
			foreach (string line in lines)
			{
				if (line.Length < 8) continue;
				bool isFrame = line.StartsWith("{\"k\":\"f\"", StringComparison.Ordinal);
				if (isFrame && !full) continue;
				JsonDocument doc;
				try { doc = JsonDocument.Parse(line); } catch (Exception) { continue; }
				using (doc)
				{
					var r = doc.RootElement;
					if (!r.TryGetProperty("k", out var kk)) continue;
					string k = kk.GetString();
					switch (k)
					{
						case "hdr": Header(st, r); break;
						case "f": Frame(st, r, ref prevFrame, ref lastSec, ref lastFrac); lastFrameTick = Int(r, "t"); break;
						case "e": Event(st, r, ref prevDmgTick); break;
						case "names": Names(st, r); break;
						case "end": End(st, r); pendingEndTick = (int)(st.DurationSec * 60); break;
					}
				}
			}
			if (!st.HasEnd) { st.DurationSec = Math.Max(st.DurationSec, lastFrameTick / 60.0); }
			st.EndTick = st.HasEnd ? (int)Math.Round(st.DurationSec * 60) : lastFrameTick; _ = pendingEndTick; _ = lastBossLife; _ = lastBossMax;
			Finish(st, lastFrac);
			return st;
		}

		private static int Int(JsonElement e, string name, int def = 0) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (int)Math.Round(v.GetDouble()) : def;
		private static double Dbl(JsonElement e, string name, double def = 0) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
		private static string Str(JsonElement e, string name, string def = null) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : def;
		private static double Num(JsonElement a, int i) => i < a.GetArrayLength() && a[i].ValueKind == JsonValueKind.Number ? a[i].GetDouble() : 0;

		private static void Header(FightStats st, JsonElement r)
		{
			st.Format = Int(r, "format"); st.Boss = Str(r, "boss"); st.Player = Str(r, "player"); st.Mode = Str(r, "mode"); st.World = Str(r, "world"); st.Date = Str(r, "date"); st.Auto = Int(r, "auto");
			string sn = Str(r, "stateNames"); if (!string.IsNullOrEmpty(sn)) st.StateNames = sn.Split(',');
			if (r.TryGetProperty("army", out var a) && a.ValueKind == JsonValueKind.Object) st.ArmyStart = Math.Max(st.ArmyStart, Int(a, "n"));
		}

		private static void Names(FightStats st, JsonElement r)
		{
			void Fill(string key, Dictionary<int, string> into)
			{
				if (!r.TryGetProperty(key, out var o) || o.ValueKind != JsonValueKind.Object) return;
				foreach (var p in o.EnumerateObject()) if (int.TryParse(p.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && p.Value.ValueKind == JsonValueKind.String) into[id] = p.Value.GetString();
			}
			Fill("items", st.Items); Fill("npcs", st.Npcs); Fill("projectiles", st.Projs);
		}

		private static void End(FightStats st, JsonElement r)
		{
			st.HasEnd = true;
			st.Result = Str(r, "result", st.Result); st.Outcome = Str(r, "outcome", "none"); st.DurationSec = Dbl(r, "durationSec", Int(r, "ticks") / 60.0); st.CmdDeaths = Int(r, "cmdDeaths");
			string bn = Str(r, "bossName"); if (!string.IsNullOrEmpty(bn)) st.Boss = bn;
			st.AllBossDmg = Math.Max(st.AllBossDmg, (long)Dbl(r, "dmgBoss"));
		}

		private static void Frame(FightStats st, JsonElement r, ref int prevFrame, ref int lastSec, ref double lastFrac)
		{
			int t = Int(r, "t"); int dt = prevFrame < 0 ? 2 : Math.Clamp(t - prevFrame, 1, 12); prevFrame = t;
			int held = 0;
			if (r.TryGetProperty("p", out var p) && p.ValueKind == JsonValueKind.Array) held = (int)Num(p, 11);
			st.FrameTicks.Add(t); st.FrameHeld.Add(held);
			int n = 0, broken = 0;
			if (r.TryGetProperty("l", out var l) && l.ValueKind == JsonValueKind.Array)
				foreach (var row in l.EnumerateArray()) { n++; if (st.IsBroken((int)Num(row, 7))) broken++; }
			st.LevyTime += n * dt; st.BrokenTime += broken * dt;
			double life = 0, max = 0; bool anyBoss = false;
			if (r.TryGetProperty("b", out var b) && b.ValueKind == JsonValueKind.Array)
				foreach (var row in b.EnumerateArray())
				{
					if (Num(row, 13) != 0) continue; // a part, not a boss
					life += Math.Max(0, Num(row, 6)); max += Math.Max(1, Num(row, 7)); anyBoss = true;
				}
			if (anyBoss) { lastFrac = max > 0 ? life / max : 0; if (st.FirstBossTick < 0) st.FirstBossTick = t; }
			int sec = t / 60;
			if (sec != lastSec)
			{
				lastSec = sec;
				st.Army.Add(new ArmyPoint { Sec = sec, N = n, Broken = broken, BossFrac = anyBoss ? lastFrac : -1 });
			}
			if (n > st.ArmyPeak) st.ArmyPeak = n;
		}

		private static void Event(FightStats st, JsonElement r, ref int prevDmgTick)
		{
			string e = Str(r, "e"); int t = Int(r, "t");
			switch (e)
			{
				case "dmg":
					{
						if (!r.TryGetProperty("d", out var d) || d.ValueKind != JsonValueKind.Array) break;
						var ev = new FightStats.DmgEvt { Tick = t };
						foreach (var row in d.EnumerateArray())
						{
							if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 7) continue;
							var x = new FightStats.DmgRow
							{
								Owner = row[0].GetString(), Cls = row[1].GetString(), Src = row[2].GetString(), Type = (int)Num(row, 3), Boss = Num(row, 4) != 0, Dmg = (long)Num(row, 5), Hits = (int)Num(row, 6)
							};
							if (row.GetArrayLength() >= 10) { x.Ext = true; x.SrcItem = (int)Num(row, 7); x.Ss = (int)Num(row, 8); x.Crits = (int)Num(row, 9); }
							ev.Rows.Add(x);
						}
						st.DmgEvents.Add(ev); prevDmgTick = t;
						break;
					}
				case "hit":
					{
						int dmg = Int(r, "dmg"), npc = Int(r, "npc", -1), proj = Int(r, "proj", -1), other = Int(r, "other", -1);
						st.HitEvents.Add((t, dmg, "\u0001" + npc + "," + proj + "," + other)); // labels are resolved at the end, when the names table is known
						break;
					}
				case "cdeath":
					{
						string note = null;
						if (r.TryGetProperty("kn", out _) || r.TryGetProperty("kp", out _) || r.TryGetProperty("ko", out _) || r.TryGetProperty("kc", out _))
						{
							string custom = Str(r, "kc");
							note = !string.IsNullOrEmpty(custom) ? "\u0002" + custom : "\u0001" + Int(r, "kn", -1) + "," + Int(r, "kp", -1) + "," + Int(r, "ko", -1);
						}
						st.KillNotes.Add((t, note));
						break;
					}
				case "tgt": st.Target.Add((t, Int(r, "kind"))); break;
				case "ldeath": st.LevyDeathTicks.Add(t); break;
				case "use": { int it = Int(r, "item"); st.UseCount[it] = st.UseCount.TryGetValue(it, out int c) ? c + 1 : 1; break; }
				case "stl": st.StealthPct.Add(Int(r, "s")); st.StealthBarSeen = true; break;
				case "lbw": if (st.WarnSec < 0) st.WarnSec = t / 60.0; break;
				case "bph": st.Phases.Add((t, Str(r, "what", Int(r, "pct") + "% life"))); break;
				case "bdeath":
				case "bdespawn":
					if (Int(r, "final") == 1)
					{
						double life = Dbl(r, "life"), max = Dbl(r, "max");
						st.BossLeftFrac = e == "bdeath" ? 0 : (max > 0 ? Math.Clamp(life / max, 0, 1) : -1);
					}
					break;
			}
		}

		private static string Resolve(FightStats st, string packed)
		{
			if (packed == null) return null;
			if (packed.Length > 0 && packed[0] == '\u0002') return packed.Substring(1);
			var p = packed.Substring(1).Split(',');
			return st.SourceLabel(int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture));
		}

		private static int HeldMode(FightStats st, int t0, int t1)
		{
			var ticks = st.FrameTicks; int n = ticks.Count; if (n == 0) return 0;
			int lo = 0, hi = n; while (lo < hi) { int mid = (lo + hi) >> 1; if (ticks[mid] <= t0) lo = mid + 1; else hi = mid; }
			Dictionary<int, int> c = null; int best = 0, bestN = 0;
			for (int i = lo; i < n && ticks[i] <= t1; i++)
			{
				int h = st.FrameHeld[i]; if (h <= 0) continue;
				c ??= new Dictionary<int, int>(); c.TryGetValue(h, out int v); v++; c[h] = v;
				if (v > bestN) { bestN = v; best = h; }
			}
			if (best > 0) return best;
			int j = Math.Max(0, Math.Min(n - 1, lo - 1)); return st.FrameHeld[j];
		}

		private static void Finish(FightStats st, double lastFrac)
		{
			// boss life left: the boss's own end record, else the last sample
			if (st.Outcome == "killed") st.BossLeftFrac = 0;
			else if (st.BossLeftFrac < 0 && lastFrac >= 0) st.BossLeftFrac = Math.Clamp(lastFrac, 0, 1);

			// damage rows: who dealt what, the commander's per weapon
			int prevT = -1; double lastEvtT = -1; _ = lastEvtT;
			foreach (var ev in st.DmgEvents)
			{
				double w = prevT < 0 ? 1.0 : Math.Clamp((ev.Tick - prevT) / 60.0, 0.05, 3.0); // an event covers the stretch since the one before it (the recorder flushes about once a second, a lagging game less often) prevT = ev.Tick;
				prevT = ev.Tick;
				bool anyCmdBoss = false; var seen = new HashSet<string>();
				foreach (var row in ev.Rows)
				{
					if (row.Owner == "cmd")
					{
						if (!row.Boss) { st.CmdAddDmg += row.Dmg; continue; }
						anyCmdBoss = true;
						int item = row.SrcItem > 0 ? row.SrcItem : (row.Src == "item" && row.Type > 0 ? row.Type : HeldMode(st, ev.Tick - 60, ev.Tick));
						string key = st.ItemName(item);
						if (!st.Weapons.TryGetValue(key, out var ws)) st.Weapons[key] = ws = new WeaponStat { Name = key, Item = item };
						ws.Dmg += row.Dmg; ws.Hits += row.Hits; st.CmdBossDmg += row.Dmg; st.CmdBossHits += row.Hits;
						if (row.Ext)
						{
							ws.StealthKnown = true; ws.CritKnown = true; st.StealthKnown = true; st.CritKnown = true;
							ws.Crits += row.Crits; ws.CritKnownHits += row.Hits; st.CmdCrits += row.Crits; st.CmdCritKnownHits += row.Hits;
							if (row.Ss > 0) { ws.StealthDmg += row.Dmg; ws.StealthHits += row.Hits; st.CmdStealthDmg += row.Dmg; st.CmdStealthHits += row.Hits; }
						}
						if (seen.Add(key)) ws.ActiveSec += w;
					}
					else if (row.Boss)
					{
						if (row.Owner == "levy") st.LevyBossDmg += row.Dmg; else if (row.Owner == "drone") st.DroneBossDmg += row.Dmg; else st.OtherBossDmg += row.Dmg;
					}
				}
				if (anyCmdBoss) st.ActiveSec += w;
			}
			if (st.AllBossDmg <= 0) st.AllBossDmg = st.CmdBossDmg + st.LevyBossDmg + st.DroneBossDmg + st.OtherBossDmg;
			foreach (var kv in st.UseCount) { string nm = st.ItemName(kv.Key); if (st.Weapons.TryGetValue(nm, out var ws)) ws.Uses += kv.Value; }

			// damage taken
			foreach (var h in st.HitEvents)
			{
				string label = Resolve(st, h.label);
				if (!st.Taken.TryGetValue(label, out var ts)) st.Taken[label] = ts = new TakenStat { Label = label };
				ts.Hits++; ts.Dmg += h.dmg; st.TakenTotal += h.dmg; st.TakenHits++;
			}

			// the army: start and end, the first ten seconds give the roster that came to the fight
			int startN = 0; foreach (var a in st.Army) { if (a.Sec > 10) break; if (a.N > startN) startN = a.N; }
			if (startN > 0) st.ArmyStart = startN;
			else if (st.Army.Count > 0) st.ArmyStart = 0;
			st.ArmyEnd = st.Army.Count > 0 ? st.Army[st.Army.Count - 1].N : 0;
			st.LeviesLost = st.LevyDeathTicks.Count;
			st.LevyDeathTicks.Sort();
			{ int di = 0; for (int i = 0; i < st.Army.Count; i++) { var a = st.Army[i]; while (di < st.LevyDeathTicks.Count && st.LevyDeathTicks[di] <= a.Sec * 60) di++; a.Deaths = di; st.Army[i] = a; } }
			int bt = LineBreakDetector.Replay(st.Army, out string bwhy);
			if (bt >= 0) { st.BreakSec = bt / 60.0; st.BreakWhy = bwhy; }

			// deaths: who killed the commander (the death's own source, else the last hit before it) and what the army looked like then
			foreach (var kn in st.KillNotes)
			{
				string killer = Resolve(st, kn.label);
				if (killer == null)
				{
					for (int i = st.HitEvents.Count - 1; i >= 0; i--) if (st.HitEvents[i].tick <= kn.tick + 2 && kn.tick - st.HitEvents[i].tick <= 180) { killer = Resolve(st, st.HitEvents[i].label); break; }
				}
				int armyAt = 0; foreach (var a in st.Army) { if (a.Sec * 60 > kn.tick) break; armyAt = a.N; }
				st.Deaths.Add(new DeathNote { Tick = kn.tick, Killer = killer, ArmyAt = armyAt, ArmyStart = st.ArmyStart });
				if (killer != null) { if (!st.Taken.TryGetValue(killer, out var ts)) st.Taken[killer] = ts = new TakenStat { Label = killer }; ts.Deaths++; }
			}
			if (st.CmdDeaths < st.Deaths.Count) st.CmdDeaths = st.Deaths.Count;

			// boss life milestones, with the army at that moment
			foreach (int pct in new[] { 75, 50, 25, 10 })
				foreach (var a in st.Army) if (a.BossFrac >= 0 && a.BossFrac <= pct / 100.0) { st.Milestones.Add((pct, a.Sec, a.N)); break; }
		}
	}
}
