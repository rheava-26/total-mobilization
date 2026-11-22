using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

	/// <summary>One kind of damage the commander took, grouped by its source label ("Spore Gas", "Plantera (contact)"). Dmg is the life it took; Raw the game's own damage numbers (a dodge, a heal or an immunity can undo a hit).</summary>
	internal sealed class TakenStat { public string Label; public int Hits; public long Dmg, Raw; public int Deaths; }

	/// <summary>
	/// One life pool of the fight's bosses as the frames show it: a boss with its own life, a worm and its segments, Ares and his arms, the Exo Twins (who share one pool).
	/// Format 13 groups rows by realLife (the slot that owns the life); older files by NPC type, rows of one type giving the lowest life (a worm's segments freeze at full life while the head loses it).
	/// Pools with the same life and maximum in a sample are one pool there (Ares and his arms, Artemis and Apollo).
	/// </summary>
	internal sealed class BossTrack
	{
		public string Key; public readonly List<int> Types = new();
		public long Max, FirstLife = -1, MinLife = long.MaxValue, LastLife; public int FirstTick = -1, LastTick, RowsMax; public bool Killed;
		/// <summary>The keys of the other tracks that shared this one's life in its last sample.</summary>
		public List<string> LastGroup = new();
		/// <summary>(tick, kind) whenever its target kind changed: 0 none, 1 the commander, 2 a levy, 3 a levy by spite.</summary>
		public readonly List<(int tick, int kind)> Kinds = new();
		/// <summary>It lost life at some point (Draedon's own NPC, a part that only mirrors, never do).</summary>
		public bool Damaged => MinLife < Max;
		public double Frac => Max > 0 ? Math.Clamp((double)LastLife / Max, 0, 1) : 0;
	}

	internal struct ArmyPoint { public int Sec, N, Broken, Deaths; public double BossFrac; public int Snap; }

	internal sealed class DeathNote { public int Tick; public string Killer; public int ArmyAt; public int ArmyStart; public int FallenBefore; }

	internal sealed class FightStats
	{
		public string File, Boss, Player, Mode, World, Date, Result = "ended", Outcome = "none";
		public int Format, CmdDeaths, Auto;
		public double DurationSec;
		/// <summary>How far the army's engagement raised the boss's life (percent of base life, 0 when it did not or the file predates the field), the soldiers engaged and their mean gear strength.</summary>
		public int HpScalePct, HpScaleSoldiers; public double HpScaleStrength;
		public bool HasEnd;
		public string[] StateNames = { "Idle", "Follow", "Patrol", "Engage", "Haul", "Resupply", "Shaken", "Routed", "ReturnHome", "Dead", "Desert", "SearchDestroy", "FreeRein", "Mutiny" };
		public readonly Dictionary<int, string> Items = new(), Npcs = new(), Projs = new();
		/// <summary>Items recorded as hit sources that are not usable weapons (accessories and effects); their hits are grouped as accessory effects.</summary>
		public readonly HashSet<int> NonWeapons = new();

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
		/// <summary>The fight's final boss end event as written (bdeath | bdespawn, tick, life, max, last hit damage, kh = the death hook ran, absent in files made before it) and whether this reader overrode the file's own result because of it.</summary>
		public string EndEvent; public int EndEvTick, EndHd; public double EndLife, EndMax; public bool EndHasKh; public bool Rederived;
		public readonly List<(int pct, int sec, int army)> Milestones = new();
		/// <summary>The bosses' life pools in the order they appeared (frames only).</summary>
		public readonly List<BossTrack> Tracks = new();
		internal readonly Dictionary<string, BossTrack> TrackBy = new();
		/// <summary>Per second: the pools standing then, as groups (the tracks sharing one life, life, max).</summary>
		internal readonly List<List<(string[] keys, long life, long max)>> Snaps = new();
		internal readonly Dictionary<int, string> SlotTrack = new();
		/// <summary>The pools the report names at the end: groups of tracks that lost life, with their life left (0 when killed).</summary>
		public readonly List<(string name, double frac, bool killed, string key)> EndPools = new();
		/// <summary>The pool the "who the boss went for" lines follow (the largest that lost life), and whether its target was readable at all.</summary>
		public string AimKey; public bool TargetUnread, TargetFromFrames;
		internal readonly HashSet<int> TgtBosses = new();
		/// <summary>Per pool target shares, when the fight had several pools that lost life: (name, on you, on levies, nobody).</summary>
		public readonly List<(string name, double you, double levies, double none)> PoolAggro = new();
		/// <summary>The game's own damage numbers of the hits on the commander (TakenTotal is the life they took).</summary>
		public long TakenRaw;
		/// <summary>Every levy, officer and militiaman the frames carry, wherever they stood.</summary>
		public readonly HashSet<long> LevyIds = new();
		public readonly List<(int tick, string what)> Phases = new();
		public int EndTick;

		// internals while reading
		internal readonly List<int> FrameTicks = new(), FrameHeld = new(), FrameLife = new();
		internal readonly RowDecoder Rows = new();
		internal sealed class DmgRow { public string Owner, Cls, Src; public int Type, SrcItem, Ss, Crits; public bool Boss, Ext; public long Dmg; public int Hits; }
		internal sealed class DmgEvt { public int Tick; public List<DmgRow> Rows = new(); }
		internal readonly List<DmgEvt> DmgEvents = new();
		internal readonly List<(int tick, int dmg, string label, int life, int taken)> HitEvents = new();
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
			// format 13: a recording past the part size goes on in NAME_part2.levyrec ...; every part is read, in order, as one fight
			var parts = RecParts.Parts(path);
			var st = ReadLines(PartLines(parts), full);
			st.File = parts[0];
			if (!st.HasEnd) st.Result = ResultFromName(parts[0]) ?? st.Result;
			return st;
		}

		private static IEnumerable<string> PartLines(List<string> parts)
		{
			foreach (string path in parts)
			{
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				Stream s = fs;
				// gzip by its magic bytes, whatever the name (.levyrec, .part, a .jsonl.gz copy)
				int b0 = fs.ReadByte(), b1 = fs.ReadByte(); fs.Seek(0, SeekOrigin.Begin);
				if (b0 == 0x1f && b1 == 0x8b) s = new GZipStream(fs, CompressionMode.Decompress);
				using var rd = new StreamReader(s);
				foreach (string l in Lines(rd)) yield return l;
			}
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
			int prevFrame = -1, lastSec = -1, prevDmgTick = -1; int lastFrameTick = 0; bool hdrSeen = false;
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
						case "hdr": if (!hdrSeen) Header(st, r); hdrSeen = true; st.Rows.Header(r); break; // a later part's header only restarts the row decoder
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

		/// <summary>A boss row's target kind: field 14 (format 2+), else whether field 12 names a player.</summary>
		internal static int TKind(double[] b) => b.Length > 14 ? (int)b[14] : b.Length > 12 && b[12] >= 0 && b[12] < 255 ? 1 : 0;

		/// <summary>The life pools of one sample (see <see cref="BossTrack"/>), updating the tracks; returns the groups (tracks that share one life).</summary>
		private static List<(string[] keys, long life, long max)> Pools(FightStats st, List<double[]> rows, int t)
		{
			var units = new Dictionary<string, (List<int> types, long life, long max, bool owner, int kind, int rows)>(); var order = new List<string>();
			foreach (var b in rows)
			{
				if (b.Length < 14 || b[13] != 0) continue; // a part, not a boss
				int slot = (int)b[1]; long life = Math.Max(0, (long)b[6]), max = Math.Max(1, (long)b[7]);
				string key; bool own = false;
				if (b.Length > 18) { int o = b[18] >= 0 ? (int)b[18] : slot; key = "o" + o; own = o == slot; }
				else key = "t" + (int)b[0];
				st.SlotTrack[slot] = key;
				// the row that stands for the pool gives its life and its target: the owner (format 13), else the row with the lowest life (a worm's live head, not a segment frozen at full life)
				if (!units.TryGetValue(key, out var u)) { u = (new List<int>(), life, max, own, TKind(b), 1); order.Add(key); }
				else
				{
					u.rows++;
					if (own && !u.owner) { u.life = life; u.max = max; u.owner = true; u.kind = TKind(b); }
					else if (!u.owner && life < u.life) { u.life = life; u.max = max; u.kind = TKind(b); }
				}
				if (!u.types.Contains((int)b[0])) u.types.Add((int)b[0]);
				units[key] = u;
			}
			var groups = new List<(string[] keys, long life, long max)>();
			var gk = new List<List<string>>();
			foreach (string k in order)
			{
				var u = units[k];
				if (!st.TrackBy.TryGetValue(k, out var tr)) { tr = new BossTrack { Key = k, FirstTick = t, FirstLife = u.life }; st.TrackBy[k] = tr; st.Tracks.Add(tr); }
				foreach (int ty in u.types) if (!tr.Types.Contains(ty)) tr.Types.Add(ty);
				tr.Max = u.max; tr.LastLife = u.life; tr.LastTick = t; if (u.life < tr.MinLife) tr.MinLife = u.life; if (u.rows > tr.RowsMax) tr.RowsMax = u.rows;
				if (tr.Kinds.Count == 0 || tr.Kinds[tr.Kinds.Count - 1].kind != u.kind) tr.Kinds.Add((t, u.kind));
				int gi = groups.FindIndex(g => g.life == u.life && g.max == u.max);
				if (gi < 0) { groups.Add((new[] { k }, u.life, u.max)); gk.Add(new List<string> { k }); }
				else { gk[gi].Add(k); groups[gi] = (gk[gi].ToArray(), u.life, u.max); }
			}
			foreach (var g in gk) foreach (string k in g) st.TrackBy[k].LastGroup = g;
			return groups;
		}

		/// <summary>Life left over the groups of a sample whose tracks lost life at some point in the fight (all of them when none did). -1 with no boss.</summary>
		private static double Combined(FightStats st, List<(string[] keys, long life, long max)> groups, bool anyDamaged)
		{
			double life = 0, max = 0;
			foreach (var g in groups)
			{
				bool dmg = false; foreach (string k in g.keys) if (st.TrackBy[k].Damaged) dmg = true;
				if (anyDamaged && !dmg) continue;
				life += Math.Min(g.life, g.max); max += g.max;
			}
			return max > 0 ? life / max : -1;
		}

		/// <summary>A pool's name: its NPC names, without the ones that only extend another ("XF-09 Ares Laser Cannon" goes with "XF-09 Ares"), joined with and.</summary>
		internal static string PoolName(FightStats st, IEnumerable<int> types)
		{
			var names = new List<string>();
			foreach (int t in types) { string n = st.NpcName(t); if (!names.Contains(n)) names.Add(n); }
			var keep = names.Where(n => !names.Any(o => o != n && n.StartsWith(o + " ", StringComparison.Ordinal))).ToList();
			if (keep.Count <= 1) return keep.Count == 1 ? keep[0] : "the boss";
			return string.Join(", ", keep.Take(keep.Count - 1)) + " and " + keep[keep.Count - 1];
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
			if (r.TryGetProperty("nonweapons", out var nw) && nw.ValueKind == JsonValueKind.Array) foreach (var e in nw.EnumerateArray()) if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int id)) st.NonWeapons.Add(id);
		}

		private static void End(FightStats st, JsonElement r)
		{
			st.HasEnd = true;
			st.Result = Str(r, "result", st.Result); st.Outcome = Str(r, "outcome", "none"); st.DurationSec = Dbl(r, "durationSec", Int(r, "ticks") / 60.0); st.CmdDeaths = Int(r, "cmdDeaths");
			string bn = Str(r, "bossName"); if (!string.IsNullOrEmpty(bn)) st.Boss = bn;
			st.AllBossDmg = Math.Max(st.AllBossDmg, (long)Dbl(r, "dmgBoss"));
			st.HpScalePct = Int(r, "hpScalePct"); st.HpScaleSoldiers = Int(r, "hpScaleSoldiers"); st.HpScaleStrength = Dbl(r, "hpScaleStrength", 0);
		}

		private static void Frame(FightStats st, JsonElement r, ref int prevFrame, ref int lastSec, ref double lastFrac)
		{
			int t = Int(r, "t"); int dt = prevFrame < 0 ? 2 : Math.Clamp(t - prevFrame, 1, 12); prevFrame = t;
			int held = 0; double px = 0, py = 0; int plife = 0; bool pdead = false;
			if (r.TryGetProperty("p", out var p) && p.ValueKind == JsonValueKind.Array) { held = (int)Num(p, 11); px = Num(p, 0); py = Num(p, 1); plife = (int)Num(p, 6); pdead = ((int)Num(p, 5) & 8192) != 0; }
			st.FrameTicks.Add(t); st.FrameHeld.Add(held); st.FrameLife.Add(pdead ? 0 : plife);
			var bossRows = st.Rows.Rows(r, 0); var levyRows = st.Rows.Rows(r, 2);
			st.Rows.Rows(r, 1); st.Rows.Rows(r, 3); st.Rows.Rows(r, 4); // keep the decoder's other lists in step
			// levies in the arena: within FightRecorder.ArenaRangeTiles of the commander or of a boss (a militiaman at his outpost 230 tiles away is not in the fight)
			double rr = FightRecFormat.ArenaRangeTiles * 16.0; var anchors = new List<(double x, double y)> { (px, py) };
			foreach (var b in bossRows) if (b.Length > 13 && b[13] == 0 && (b.Length <= 18 || b[18] < 0 || b[18] == b[1])) anchors.Add((b[2], b[3]));
			int n = 0, broken = 0;
			foreach (var row in levyRows)
			{
				if (row.Length < 3) continue;
				bool near = false; foreach (var a in anchors) if (Math.Abs(row[1] - a.x) <= rr && Math.Abs(row[2] - a.y) <= rr) { near = true; break; }
				if (!near) continue;
				n++; if (row.Length > 7 && st.IsBroken((int)row[7])) broken++;
			}
			st.LevyTime += n * dt; st.BrokenTime += broken * dt;
			foreach (var row in levyRows) if (row.Length > 0) st.LevyIds.Add((long)row[0]);
			var groups = Pools(st, bossRows, t); bool anyBoss = groups.Count > 0;
			if (anyBoss) { lastFrac = Combined(st, groups, false); if (st.FirstBossTick < 0) st.FirstBossTick = t; }
			int sec = t / 60;
			if (sec != lastSec)
			{
				lastSec = sec;
				st.Snaps.Add(groups);
				st.Army.Add(new ArmyPoint { Sec = sec, N = n, Broken = broken, BossFrac = anyBoss ? lastFrac : -1, Snap = st.Snaps.Count - 1 });
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
						st.HitEvents.Add((t, dmg, "\u0001" + npc + "," + proj + "," + other, Int(r, "life", -1), Int(r, "taken", -1))); // labels are resolved at the end, when the names table is known
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
				case "tgt": st.Target.Add((t, Int(r, "kind"))); st.TgtBosses.Add(Int(r, "boss", -1)); break;
				case "ldeath": st.LevyDeathTicks.Add(t); break;
				case "use": { int it = Int(r, "item"); st.UseCount[it] = st.UseCount.TryGetValue(it, out int c) ? c + 1 : 1; break; }
				case "stl": st.StealthPct.Add(Int(r, "s")); st.StealthBarSeen = true; break;
				case "lbw": if (st.WarnSec < 0) st.WarnSec = t / 60.0; break;
				case "bph": st.Phases.Add((t, Str(r, "what", Int(r, "pct") + "% life"))); break;
				case "bdeath":
				case "bdespawn":
					if (e == "bdeath" && st.SlotTrack.TryGetValue(Int(r, "id", -1), out string tk) && st.TrackBy.TryGetValue(tk, out var ktr)) ktr.Killed = true;
					if (Int(r, "final") == 1)
					{
						double life = Dbl(r, "life"), max = Dbl(r, "max");
						st.EndEvent = e; st.EndEvTick = Int(r, "t"); st.EndLife = life; st.EndMax = max; st.EndHd = Int(r, "hd"); st.EndHasKh = r.TryGetProperty("kh", out _);
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

		/// <summary>
		/// Files written before the kill rule (recwin) called a boss "killed" when it vanished within a second of a hit that left it under 1% life, even though the game had only removed it: a commander who fell, in-flight shots taking
		/// the Devourer of Gods to 14542 of 1836000, then Calamity despawning it with no player alive, read as won. Such a file has a bdeath with no "kh" (death hook) field and a life that no lethal blow explains
		/// (above 1 and above the last hit's damage: Calamity's death animation sits at 1) within 10 s of the commander's fall. Read it as the boss leaving, and the result by the same rule the recorder uses. Files with "kh" are never touched.
		/// </summary>
		/// <summary>An old bdeath with no hook and no lethal blow counts as a boss that left only this soon after the commander's fall (10 s: no living player, so the game removed it). Python: recrules.REDERIVE_WINDOW.</summary>
		internal const int RederiveWindowTicks = 600;

		internal static void RederiveKill(FightStats st)
		{
			if (st.Outcome != "killed" || st.EndEvent != "bdeath" || st.EndHasKh || st.EndLife <= Math.Max(1, st.EndHd)) return;
			int last = -1, n = 0;
			foreach (var kn in st.KillNotes) if (kn.tick <= st.EndEvTick + 10) { n++; last = kn.tick; }
			if (n == 0 || st.EndEvTick - last > RederiveWindowTicks) return;
			st.Outcome = "despawn"; st.Rederived = true;
			st.Result = FightRecFormat.DecideResult("despawn", n, false, st.EndEvTick, last, 1);
			if (st.EndMax > 0) st.BossLeftFrac = Math.Clamp(st.EndLife / st.EndMax, 0, 1);
		}

		/// <summary>After the last line: which pools lost life, the boss life per second from them (Draedon's own NPC and other pools that never lost life drop out), the pools at the end, and the target from the main pool.</summary>
		private static void BossPools(FightStats st)
		{
			if (st.Tracks.Count == 0) return;
			bool anyDamaged = st.Tracks.Any(t => t.Damaged);
			for (int i = 0; i < st.Army.Count; i++)
			{
				var a = st.Army[i]; if (a.BossFrac < 0 || a.Snap < 0 || a.Snap >= st.Snaps.Count) continue;
				a.BossFrac = Combined(st, st.Snaps[a.Snap], anyDamaged); st.Army[i] = a;
			}
			// the pools at the end: each group of tracks that lost life, as its last sample left it (0 when killed)
			var done = new HashSet<string>();
			foreach (var tr in st.Tracks.OrderBy(t => t.FirstTick))
			{
				if (done.Contains(tr.Key) || (anyDamaged && !tr.Damaged)) continue;
				var grp = tr.LastGroup.Count > 0 ? tr.LastGroup.Where(k => st.TrackBy.ContainsKey(k) && st.TrackBy[k].LastTick == tr.LastTick).ToList() : new List<string> { tr.Key };
				if (!grp.Contains(tr.Key)) grp.Add(tr.Key);
				foreach (string k in grp) done.Add(k);
				var types = new List<int>(); bool killed = false;
				foreach (string k in grp) { var t2 = st.TrackBy[k]; types.AddRange(t2.Types); killed |= t2.Killed || t2.LastLife <= 0; }
				st.EndPools.Add((PoolName(st, types), killed || st.Outcome == "killed" ? 0 : tr.Frac, killed || st.Outcome == "killed", tr.Key));
			}
			// one name once: a worm's head and body types that ended a little apart are one boss (the lower life stands); an arm whose life drifted a little from its body's ("XF-09 Ares Plasma Cannon") goes with it ("XF-09 Ares")
			for (int i = st.EndPools.Count - 1; i >= 0; i--)
			{
				string ni = st.EndPools[i].name;
				int pj = st.EndPools.FindIndex(x => x.name != ni && ni.StartsWith(x.name + " ", StringComparison.Ordinal));
				if (pj >= 0) st.EndPools.RemoveAt(i);
			}
			for (int i = st.EndPools.Count - 1; i > 0; i--)
			{
				int j = st.EndPools.FindIndex(x => x.name == st.EndPools[i].name);
				if (j >= 0 && j < i) { var a = st.EndPools[j]; var b = st.EndPools[i]; st.EndPools[j] = (a.name, Math.Min(a.frac, b.frac), a.killed || b.killed, st.TrackBy[a.key].Max >= st.TrackBy[b.key].Max ? a.key : b.key); st.EndPools.RemoveAt(i); }
			}
			// the target: the main pool (the largest that lost life) over its samples
			// (on a tie, the pool seen in the fewest rows: an old file's worm head is one row, its body type ninety)
			var main = st.Tracks.Where(t => !anyDamaged || t.Damaged).OrderByDescending(t => t.Max).ThenBy(t => t.RowsMax).ThenBy(t => t.Frac).FirstOrDefault();
			if (main == null) return;
			st.AimKey = main.Key;
			// the tgt events stand when they follow the main pool (format 13 always does); a file whose tgt events follow another boss (the first one seen: Draedon, not his mechs) reads the main pool's own rows instead
			bool eventsFollowMain = st.Target.Count > 0 && st.TgtBosses.All(b => st.SlotTrack.TryGetValue(b, out string k) && k == main.Key);
			if (!eventsFollowMain)
			{
				st.Target.Clear(); foreach (var k in main.Kinds) st.Target.Add(k); st.TargetFromFrames = true;
				st.TargetUnread = st.Format < 13 && Share(st, main, 0) >= 0.9;
			}
			if (st.EndPools.Count >= 2)
				foreach (var ep in st.EndPools) { var tr = st.TrackBy[ep.key]; st.PoolAggro.Add((ep.name, Share(st, tr, 1), Share(st, tr, 2) + Share(st, tr, 3), Share(st, tr, 0))); }
		}

		/// <summary>The share of a track's samples (first to last tick) its target kind was <paramref name="kind"/>.</summary>
		private static double Share(FightStats st, BossTrack tr, int kind)
		{
			double on = 0, all = 0;
			for (int i = 0; i < tr.Kinds.Count; i++)
			{
				int t0 = tr.Kinds[i].tick, t1 = i + 1 < tr.Kinds.Count ? tr.Kinds[i + 1].tick : tr.LastTick + 2;
				if (t1 <= t0) continue; all += t1 - t0; if (tr.Kinds[i].kind == kind) on += t1 - t0;
			}
			return all > 0 ? on / all : 0;
		}

		private static void Finish(FightStats st, double lastFrac)
		{
			RederiveKill(st);
			BossPools(st);
			// boss life left: the pools at the end (a worm's real pool, the mechs together, never Draedon's own life), else the boss's own end record, else the last sample
			if (st.Outcome == "killed") st.BossLeftFrac = 0;
			else if (st.EndPools.Count > 0) { double l = 0, m = 0; foreach (var ep in st.EndPools) { var tr = st.TrackBy[ep.key]; l += ep.frac * tr.Max; m += tr.Max; } st.BossLeftFrac = m > 0 ? Math.Clamp(l / m, 0, 1) : st.BossLeftFrac; }
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
						// the recorded firing item decides; the held item stands in only for a recording that predates the field, never for a hit whose source was recorded as none
						int item = row.SrcItem > 0 ? row.SrcItem : (row.Src == "item" && row.Type > 0 ? row.Type : row.Ext ? 0 : HeldMode(st, ev.Tick - 60, ev.Tick));
						string key = item <= 0 && row.Ext ? "Accessory effects" : st.NonWeapons.Contains(item) ? "Accessory effects (" + st.ItemName(item) + ")" : st.ItemName(item);
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

			// damage taken: the life each hit took (format 13 writes it; older files read it from the life before the hit and the commander's life in the next sample), never more than the game's number
			for (int hi = 0; hi < st.HitEvents.Count; hi++)
			{
				var h = st.HitEvents[hi];
				long taken = h.taken >= 0 ? h.taken : h.dmg;
				if (h.taken < 0 && h.life >= 0 && st.FrameTicks.Count > 0)
				{
					int fi = st.FrameTicks.BinarySearch(h.tick + 1); if (fi < 0) fi = ~fi;
					if (fi < st.FrameTicks.Count)
					{
						int after = st.FrameLife[fi];
						if (hi + 1 < st.HitEvents.Count && st.HitEvents[hi + 1].life >= 0 && st.HitEvents[hi + 1].tick < st.FrameTicks[fi]) after = st.HitEvents[hi + 1].life;
						taken = Math.Clamp(h.life - after, 0, Math.Max(0, h.dmg));
					}
				}
				string label = Resolve(st, h.label);
				if (!st.Taken.TryGetValue(label, out var ts)) st.Taken[label] = ts = new TakenStat { Label = label };
				ts.Hits++; ts.Dmg += taken; ts.Raw += h.dmg; st.TakenTotal += taken; st.TakenRaw += h.dmg; st.TakenHits++;
			}

			// the army: start and end, the first ten seconds with levies in the arena give the roster that came to the fight (an army that marched in from 700 tiles away arrives later)
			int firstSec = -1; foreach (var a in st.Army) if (a.N > 0) { firstSec = a.Sec; break; }
			int startN = 0; if (firstSec >= 0) foreach (var a in st.Army) { if (a.Sec > firstSec + 10) break; if (a.Sec >= firstSec && a.N > startN) startN = a.N; }
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
				st.Deaths.Add(new DeathNote { Tick = kn.tick, Killer = killer, ArmyAt = armyAt, ArmyStart = st.ArmyStart, FallenBefore = st.LevyDeathTicks.Count(x => x <= kn.tick) });
				if (killer != null) { if (!st.Taken.TryGetValue(killer, out var ts)) st.Taken[killer] = ts = new TakenStat { Label = killer }; ts.Deaths++; }
			}
			if (st.CmdDeaths < st.Deaths.Count) st.CmdDeaths = st.Deaths.Count;

			// boss life milestones, with the army at that moment
			foreach (int pct in new[] { 75, 50, 25, 10 })
				foreach (var a in st.Army) if (a.BossFrac >= 0 && a.BossFrac <= pct / 100.0) { st.Milestones.Add((pct, a.Sec, a.N)); break; }
		}
	}
}
