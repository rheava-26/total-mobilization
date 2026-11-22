using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Levy.Common.Perf
{
	// The performance analyzer's data and decisions. Pure data and System.* only (no game types), so the same code runs in the game (the always-on log, /levyperf, the post-fight popup),
	// in the boot self-check, and in the command-line tool (levy-mod/tools/perfanalyze) over the profile events inside old recordings.
	//
	// Every cost is carried as "ms in this window" plus a call count, and turned into ms per second of play (ms/s) to compare: an update that takes 5 ms at 60 updates a second is 300 ms/s, a frame
	// that draws in 6 ms at 60 frames a second is 360 ms/s. The sum of the two is what the game loop spent per second, and a share is an item's ms/s over that sum.

	/// <summary>Item categories of a window's "top" list.</summary>
	internal static class PerfCat
	{
		/// <summary>NPC updates, one row per owning mod ("Terraria" for vanilla), the time inside Global AI hooks taken out.</summary>
		public const string Npc = "npc";
		/// <summary>NPC updates of one NPC type, "Mod/Type" (the three biggest). A part of <see cref="Npc"/>, not an addition.</summary>
		public const string NpcType = "nt";
		/// <summary>Projectile updates, one row per owning mod, army shots excluded.</summary>
		public const string Proj = "proj";
		/// <summary>Projectile updates of one projectile type (the three biggest). A part of <see cref="Proj"/> and <see cref="Army"/>.</summary>
		public const string ProjType = "pt";
		/// <summary>Updates of projectiles fired by the levy army, whatever mod owns the projectile type.</summary>
		public const string Army = "army";
		/// <summary>Time in GlobalNPC and GlobalProjectile AI, PreAI and PostAI hooks, one row per mod.</summary>
		public const string Glob = "glob";
		/// <summary>NPC drawing, one row per owning mod.</summary>
		public const string DrawNpc = "dnpc";
		/// <summary>Projectile drawing, one row per owning mod, army shots excluded.</summary>
		public const string DrawProj = "dproj";
		/// <summary>Drawing of projectiles fired by the levy army.</summary>
		public const string DrawArmy = "darmy";
		/// <summary>One map layer ("Mod/Layer"): the layers mods add to the minimap and the fullscreen map.</summary>
		public const string Map = "map";
		/// <summary>One interface layer ("Mod: Layer").</summary>
		public const string Ui = "ui";

		/// <summary>True for the categories that are drill-downs of another row (they are never added to a total).</summary>
		public static bool IsDrill(string cat) => cat == NpcType || cat == ProjType;
	}

	/// <summary>One row of a window's list of who costs time: the total milliseconds in the window and the calls.</summary>
	internal sealed class PerfItem
	{
		public string Cat, Name; public double Ms; public double N;
		public PerfItem() { }
		public PerfItem(string cat, string name, double ms, double n) { Cat = cat; Name = name; Ms = ms; N = n; }
		/// <summary>The mod part of a name: "Mod/Type" gives Mod, "Mod: Layer" gives Mod, a bare name is its own mod.</summary>
		public string Mod
		{
			get
			{
				if (Name == null) return "?";
				int s = Name.IndexOf('/'); int c = Name.IndexOf(": ", StringComparison.Ordinal);
				int k = s < 0 ? c : c < 0 ? s : Math.Min(s, c);
				return k < 0 ? Name : Name.Substring(0, k);
			}
		}
	}

	/// <summary>One levy weapon's cost in a window (or a span): the levies holding it, ms per update of their bodies and of their shots, shots alive, and its costliest projectile type.</summary>
	internal sealed class WeaponRow
	{
		public string Name = ""; public double Levies, BodyMs, ShotMs, Shots; public string TopProj = ""; public double TopProjMs;
		public double Ms => BodyMs + ShotMs;
		public double MsPerLevy => Levies >= 0.05 ? Ms / Levies : 0;
	}

	/// <summary>One profiling window (a few seconds of play).</summary>
	internal sealed class PerfWindow
	{
		public const int MaxWeaponRows = 8;
		/// <summary>Levy weapons, ms per update, biggest first (<see cref="MaxWeaponRows"/> at most); empty when no levy was timed.</summary>
		public List<WeaponRow> Weapons = new();
		/// <summary>ms per update charged to all levy weapons (every one, not only the rows kept).</summary>
		public double WeaponAttribMs;
		/// <summary>What the attribution is checked against, ms per update: levy bodies and army shots with other mods' hooks taken out (the analyzer's own rows), other mods' hooks on them, and the owner swaps around the shots. Null when not measured.</summary>
		public double[] ArmyRefMs;
		public DateTime Utc; public long Tick; public double Secs = 8;
		public double Fps, Ups, UpdMs, DrawMs, WorstUpdMs, WorstFrameMs; public int Updates, Frames, SampledUpdates, SampledFrames;
		public bool Focus = true;
		/// <summary>Draw ms per frame: NPCs, projectiles, dust, gore, interface, players.</summary>
		public double[] DrawSplit = new double[6];
		/// <summary>Update ms per tick: the NPC phase, the projectile phase.</summary>
		public double[] UpdSplit = new double[2];
		public int Npcs, Projs, Dust, Gore, Levies, Minions;
		/// <summary>The local player's own Regulars with a body in the field (-1: not counted, as in a log line). Not written to the log.</summary>
		public int Mine = -1;
		/// <summary>The boss within reach of the player ("" none), a boss elsewhere in the world, 0 no map / 1 minimap or overlay / 2 fullscreen map, the held item.</summary>
		public string Boss = "", BossFar = ""; public int Map; public int Held; public string HeldName = ""; public int NetMode; public int Effects;
		/// <summary>The game's Frame Skip setting (0 Off, 1 On, 2 Subtle; -1 not known: a server, an old log).</summary>
		public int FrameSkip = -1;
		/// <summary>Levy's own buckets, ms per update (the biggest).</summary>
		public List<KeyValuePair<string, double>> Levy = new();
		public List<PerfItem> Top = new();
		/// <summary>True for a window made from an old recording's prof event: it has no mod attribution and no counts.</summary>
		public bool Legacy; public double LegacyLevyMs;

		public double TotalMsPerSec => UpdMs * Ups + DrawMs * Fps;

		// ---------------------------------------------------------------------------------------- json
		private static string N(double v, int d = 2)
		{
			if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
			return Math.Round(v, d).ToString(d == 0 ? "0" : "0." + new string('#', d), CultureInfo.InvariantCulture);
		}
		internal static string Esc(string s)
		{
			if (s == null) return "\"\"";
			var sb = new StringBuilder(s.Length + 2); sb.Append('"');
			foreach (char c in s) { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c < 32) sb.Append(' '); else sb.Append(c); }
			return sb.Append('"').ToString();
		}

		public string ToJson()
		{
			var sb = new StringBuilder(640);
			sb.Append("{\"k\":\"w\",\"t\":").Append(Esc(Utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)))
			  .Append(",\"tick\":").Append(Tick).Append(",\"win\":").Append(N(Secs, 1)).Append(",\"foc\":").Append(Focus ? 1 : 0)
			  .Append(",\"fps\":").Append(N(Fps, 1)).Append(",\"ups\":").Append(N(Ups, 1)).Append(",\"upd\":").Append(N(UpdMs)).Append(",\"draw\":").Append(N(DrawMs))
			  .Append(",\"wu\":").Append(N(WorstUpdMs, 1)).Append(",\"wf\":").Append(N(WorstFrameMs, 1)).Append(",\"nu\":").Append(Updates).Append(",\"nf\":").Append(Frames)
			  .Append(",\"su\":").Append(SampledUpdates).Append(",\"sf\":").Append(SampledFrames);
			sb.Append(",\"ds\":[").Append(string.Join(",", DrawSplit.Select(v => N(v)))).Append("],\"us\":[").Append(string.Join(",", UpdSplit.Select(v => N(v)))).Append(']');
			sb.Append(",\"cnt\":{\"npc\":").Append(Npcs).Append(",\"proj\":").Append(Projs).Append(",\"dust\":").Append(Dust).Append(",\"gore\":").Append(Gore).Append(",\"levy\":").Append(Levies).Append(",\"minion\":").Append(Minions).Append('}');
			sb.Append(",\"ctx\":{\"boss\":").Append(Esc(Boss)).Append(",\"far\":").Append(Esc(BossFar)).Append(",\"map\":").Append(Map).Append(",\"held\":").Append(Held).Append(",\"hn\":").Append(Esc(HeldName)).Append(",\"nm\":").Append(NetMode).Append(",\"fx\":").Append(Effects).Append(",\"fs\":").Append(FrameSkip).Append('}');
			sb.Append(",\"lv\":[");
			for (int i = 0; i < Levy.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(Esc(Levy[i].Key)).Append(',').Append(N(Levy[i].Value)).Append(']'); }
			sb.Append(']');
			if (Weapons.Count > 0 || WeaponAttribMs > 0)
			{
				sb.Append(",\"wpa\":").Append(N(WeaponAttribMs, 3));
				if (ArmyRefMs != null) sb.Append(",\"wpr\":[").Append(string.Join(",", ArmyRefMs.Select(v => N(v, 3)))).Append(']');
				sb.Append(",\"wp\":[");
				for (int i = 0; i < Weapons.Count; i++)
				{
					var r = Weapons[i]; if (i > 0) sb.Append(',');
					sb.Append('[').Append(Esc(r.Name)).Append(',').Append(N(r.Levies, 1)).Append(',').Append(N(r.BodyMs, 3)).Append(',').Append(N(r.ShotMs, 3)).Append(',').Append(N(r.Shots, 1)).Append(',').Append(Esc(r.TopProj)).Append(',').Append(N(r.TopProjMs, 3)).Append(']');
				}
				sb.Append(']');
			}
			sb.Append(",\"top\":[");
			for (int i = 0; i < Top.Count; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(Esc(Top[i].Cat)).Append(',').Append(Esc(Top[i].Name)).Append(',').Append(N(Top[i].Ms, 1)).Append(',').Append(N(Top[i].N, 0)).Append(']'); }
			return sb.Append("]}").ToString();
		}

		/// <summary>Reads one "w" line. Null when the line is not a window.</summary>
		public static PerfWindow FromJson(string line)
		{
			if (line == null || line.Length < 2 || line[0] != '{') return null; // not one of ours: no exception to log
			try
			{
				using var d = JsonDocument.Parse(line); var r = d.RootElement;
				if (!r.TryGetProperty("k", out var k) || k.GetString() != "w") return null;
				var w = new PerfWindow();
				if (r.TryGetProperty("t", out var t) && DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var u)) w.Utc = u;
				w.Tick = Lng(r, "tick"); w.Secs = Dbl(r, "win", 8); w.Focus = Dbl(r, "foc", 1) > 0;
				w.Fps = Dbl(r, "fps"); w.Ups = Dbl(r, "ups"); w.UpdMs = Dbl(r, "upd"); w.DrawMs = Dbl(r, "draw"); w.WorstUpdMs = Dbl(r, "wu"); w.WorstFrameMs = Dbl(r, "wf");
				w.Updates = (int)Dbl(r, "nu"); w.Frames = (int)Dbl(r, "nf"); w.SampledUpdates = (int)Dbl(r, "su"); w.SampledFrames = (int)Dbl(r, "sf");
				if (r.TryGetProperty("ds", out var ds)) { int i = 0; foreach (var e in ds.EnumerateArray()) if (i < w.DrawSplit.Length) w.DrawSplit[i++] = e.GetDouble(); }
				if (r.TryGetProperty("us", out var us)) { int i = 0; foreach (var e in us.EnumerateArray()) if (i < w.UpdSplit.Length) w.UpdSplit[i++] = e.GetDouble(); }
				if (r.TryGetProperty("cnt", out var c)) { w.Npcs = (int)Dbl(c, "npc"); w.Projs = (int)Dbl(c, "proj"); w.Dust = (int)Dbl(c, "dust"); w.Gore = (int)Dbl(c, "gore"); w.Levies = (int)Dbl(c, "levy"); w.Minions = (int)Dbl(c, "minion"); }
				if (r.TryGetProperty("ctx", out var x))
				{
					w.Boss = Str(x, "boss"); w.BossFar = Str(x, "far"); w.Map = (int)Dbl(x, "map"); w.Held = (int)Dbl(x, "held"); w.HeldName = Str(x, "hn"); w.NetMode = (int)Dbl(x, "nm"); w.Effects = (int)Dbl(x, "fx"); w.FrameSkip = (int)Dbl(x, "fs", -1);
				}
				if (r.TryGetProperty("lv", out var lv)) foreach (var e in lv.EnumerateArray()) w.Levy.Add(new KeyValuePair<string, double>(e[0].GetString(), e[1].GetDouble()));
				w.WeaponAttribMs = Dbl(r, "wpa");
				if (r.TryGetProperty("wpr", out var wpr)) w.ArmyRefMs = wpr.EnumerateArray().Select(e => e.GetDouble()).ToArray();
				if (r.TryGetProperty("wp", out var wp)) foreach (var e in wp.EnumerateArray()) w.Weapons.Add(new WeaponRow { Name = e[0].GetString(), Levies = e[1].GetDouble(), BodyMs = e[2].GetDouble(), ShotMs = e[3].GetDouble(), Shots = e[4].GetDouble(), TopProj = e[5].GetString(), TopProjMs = e[6].GetDouble() });
				if (r.TryGetProperty("top", out var top)) foreach (var e in top.EnumerateArray()) w.Top.Add(new PerfItem(e[0].GetString(), e[1].GetString(), e[2].GetDouble(), e[3].GetDouble()));
				return w;
			}
			catch (Exception) { return null; }
		}

		/// <summary>An old recording's prof event (fps, ups, upd, npc, proj, draw, ai, gear, top, win) as a window. It carries Levy's AI and gear ms per update and nothing else.</summary>
		public static PerfWindow FromRecProf(string line, string boss, DateTime utc)
		{
			try
			{
				using var d = JsonDocument.Parse(line); var r = d.RootElement;
				if (!r.TryGetProperty("e", out var e) || e.GetString() != "prof") return null;
				var w = new PerfWindow { Legacy = true, Utc = utc, Boss = boss ?? "", Tick = Lng(r, "t") };
				w.Secs = Math.Max(0.5, Dbl(r, "win", 2)); w.Fps = Dbl(r, "fps"); w.Ups = Dbl(r, "ups"); w.UpdMs = Dbl(r, "upd"); w.DrawMs = Dbl(r, "draw");
				w.UpdSplit[0] = Dbl(r, "npc"); w.UpdSplit[1] = Dbl(r, "proj");
				w.Updates = (int)Math.Round(w.Ups * w.Secs); w.Frames = (int)Math.Round(w.Fps * w.Secs);
				w.LegacyLevyMs = Dbl(r, "ai") + Dbl(r, "gear");
				string top = Str(r, "top");
				foreach (string part in top.Split(',', StringSplitOptions.RemoveEmptyEntries))
				{
					string p = part.Trim(); int sp = p.LastIndexOf(' ');
					if (sp > 0 && double.TryParse(p.Substring(sp + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) && w.Updates > 0) w.Levy.Add(new KeyValuePair<string, double>(p.Substring(0, sp), ms / w.Updates));
				}
				return w;
			}
			catch (Exception) { return null; }
		}

		private static double Dbl(JsonElement e, string k, double def = 0) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : def;
		private static long Lng(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? (long)v.GetDouble() : 0;
		private static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
	}

	/// <summary>The numbers of a stretch of windows (a minute, a fight), summed so that an average is weighted by the time it covers.</summary>
	internal sealed class PerfSpan
	{
		public double Secs; public long Updates, Frames; public int Windows;
		public double Fps, Ups, UpdMs, DrawMs, TotalMsPerSec, WorstUpdMs, WorstFrameMs;
		public double[] DrawSplit = new double[6]; public double[] UpdSplit = new double[2];
		public double Npcs, Projs, Dust, Gore, Levies, Minions;
		public bool Legacy;
		public double LegacyLevyMsPerSec;
		/// <summary>Per (category, name): total ms and calls over the span, and ms per second.</summary>
		public readonly Dictionary<(string cat, string name), PerfItem> Items = new();
		public readonly Dictionary<string, double> LevyBuckets = new(); // ms per update, weighted
		public string Boss = "";
		/// <summary>Levy weapons over the span, ms per update weighted by the updates of each window, biggest first; and all levy weapons together.</summary>
		public List<WeaponRow> Weapons = new(); public double WeaponAttribMs;

		public static PerfSpan Of(IEnumerable<PerfWindow> windows)
		{
			var s = new PerfSpan(); double wUpd = 0, wDraw = 0; double bucketWeight = 0; var bossSecs = new Dictionary<string, double>();
			var wacc = new Dictionary<string, WeaponRow>(); var pacc = new Dictionary<(string, string), double>(); double wa = 0;
			foreach (var w in windows)
			{
				if (w == null || w.Secs <= 0) continue;
				s.Windows++; s.Secs += w.Secs; s.Updates += w.Updates; s.Frames += w.Frames; if (w.Legacy) s.Legacy = true;
				wUpd += w.UpdMs * w.Updates; wDraw += w.DrawMs * w.Frames;
				s.WorstUpdMs = Math.Max(s.WorstUpdMs, w.WorstUpdMs); s.WorstFrameMs = Math.Max(s.WorstFrameMs, w.WorstFrameMs);
				for (int i = 0; i < 6; i++) s.DrawSplit[i] += w.DrawSplit[i] * w.Frames;
				for (int i = 0; i < 2; i++) s.UpdSplit[i] += w.UpdSplit[i] * w.Updates;
				s.Npcs += w.Npcs * w.Secs; s.Projs += w.Projs * w.Secs; s.Dust += w.Dust * w.Secs; s.Gore += w.Gore * w.Secs; s.Levies += w.Levies * w.Secs; s.Minions += w.Minions * w.Secs;
				s.LegacyLevyMsPerSec += w.LegacyLevyMs * w.Ups * w.Secs;
				foreach (var it in w.Top)
				{
					var key = (it.Cat, it.Name);
					if (!s.Items.TryGetValue(key, out var acc)) s.Items[key] = acc = new PerfItem(it.Cat, it.Name, 0, 0);
					acc.Ms += it.Ms; acc.N += it.N;
				}
				foreach (var kv in w.Levy) { s.LevyBuckets.TryGetValue(kv.Key, out double a); s.LevyBuckets[kv.Key] = a + kv.Value * w.Updates; }
				bucketWeight += w.Updates;
				wa += w.WeaponAttribMs * w.Updates;
				foreach (var r in w.Weapons)
				{
					if (!wacc.TryGetValue(r.Name, out var acc)) wacc[r.Name] = acc = new WeaponRow { Name = r.Name };
					acc.Levies += r.Levies * w.Updates; acc.BodyMs += r.BodyMs * w.Updates; acc.ShotMs += r.ShotMs * w.Updates; acc.Shots += r.Shots * w.Updates;
					if (!string.IsNullOrEmpty(r.TopProj)) { var key = (r.Name, r.TopProj); pacc.TryGetValue(key, out double pm); pacc[key] = pm + r.TopProjMs * w.Updates; }
				}
				if (!string.IsNullOrEmpty(w.Boss)) { bossSecs.TryGetValue(w.Boss, out double b); bossSecs[w.Boss] = b + w.Secs; }
			}
			if (s.Secs <= 0) return s;
			s.Fps = s.Frames / s.Secs; s.Ups = s.Updates / s.Secs;
			s.UpdMs = s.Updates > 0 ? wUpd / s.Updates : 0; s.DrawMs = s.Frames > 0 ? wDraw / s.Frames : 0;
			for (int i = 0; i < 6; i++) s.DrawSplit[i] = s.Frames > 0 ? s.DrawSplit[i] / s.Frames : 0;
			for (int i = 0; i < 2; i++) s.UpdSplit[i] = s.Updates > 0 ? s.UpdSplit[i] / s.Updates : 0;
			s.Npcs /= s.Secs; s.Projs /= s.Secs; s.Dust /= s.Secs; s.Gore /= s.Secs; s.Levies /= s.Secs; s.Minions /= s.Secs; s.LegacyLevyMsPerSec /= s.Secs;
			if (bucketWeight > 0) foreach (var k in s.LevyBuckets.Keys.ToList()) s.LevyBuckets[k] /= bucketWeight;
			s.TotalMsPerSec = s.UpdMs * s.Ups + s.DrawMs * s.Fps;
			if (bossSecs.Count > 0) s.Boss = bossSecs.OrderByDescending(kv => kv.Value).First().Key;
			if (s.Updates > 0)
			{
				double u = s.Updates;
				foreach (var r in wacc.Values)
				{
					r.Levies /= u; r.BodyMs /= u; r.ShotMs /= u; r.Shots /= u;
					var best = pacc.Where(kv => kv.Key.Item1 == r.Name).OrderByDescending(kv => kv.Value).FirstOrDefault();
					if (best.Key.Item2 != null) { r.TopProj = best.Key.Item2; r.TopProjMs = best.Value / u; }
				}
				s.Weapons = wacc.Values.OrderByDescending(r => r.Ms).ToList(); s.WeaponAttribMs = wa / u;
			}
			return s;
		}

		public double MsPerSec(PerfItem it) => Secs > 0 ? it.Ms / Secs : 0;
	}

	/// <summary>Settings of the analyzer's decisions. Static readonly numbers; the self-check reads them to build its synthetic fights.</summary>
	internal static class PerfThresholds
	{
		/// <summary>A window is laggy when its frames per second are under this or its updates per second are under <see cref="LagUps"/>.</summary>
		public const double LagFps = 40, LagUps = 55;
		/// <summary>A fight is laggy when this share of its time was in laggy windows.</summary>
		public const double LaggyShare = 0.20;
		/// <summary>The shortest fight, in seconds of windows with focus, that can be judged.</summary>
		public const double MinFightSecs = 20;
		/// <summary>Levy's share of the loop's time that counts as a big part of the cost.</summary>
		public const double LevyBigShare = 0.30;
		/// <summary>Another mod's share that counts as the biggest part of the cost, and the share that earns "most".</summary>
		public const double ModBigShare = 0.25, ModMostShare = 0.50;
		/// <summary>Levy is named the cause when its share is at least this fraction of the biggest other mod's.</summary>
		public const double LevyLeadsRatio = 0.8;
		/// <summary>Seconds between two popups, and after "It's fine".</summary>
		public const double PopupGapSecs = 600, FineGapSecs = 1800;
		/// <summary>The update time (ms per tick) a suggested active army size aims for: under the 16.7 ms frame with room left to draw.</summary>
		public const double ArmyTargetMs = 12.0;
		/// <summary>A suggestion never puts fewer soldiers in the field than this; the notice needs at least <see cref="ArmyMinFielded"/> of the player's own soldiers in the field.</summary>
		public const int ArmyMinSuggest = 5, ArmyMinFielded = 10;
		/// <summary>The live notice: a whole window (8 s) with updates this long, or with Frame Skip Subtle at under this share of full speed, while the army is at least <see cref="ArmyBigShare"/> of the update.</summary>
		public const double ArmySlowUpdMs = 14.0, ArmySlowSpeed = 0.8, ArmyBigShare = 0.30;
	}

	internal enum PopupOption { LowerLevyEffects, ActiveArmy, ItsFine, DontAskAgain, OpenSettings, FrameSkipSubtle }
	internal enum LagKind { Smooth, TooShort, Levy, OtherMod, BaseGame, Unknown, ArmyTooBig }

	/// <summary>What the analyzer concluded about one fight and what the popup should say.</summary>
	internal sealed class LagVerdict
	{
		public LagKind Kind = LagKind.Smooth; public bool Laggy;
		public double FightSecs, LaggySecs, LaggyShare, MedianFps, MedianUps, MinFps;
		/// <summary>Share (0 to 1) of the game loop's time of the laggy windows, Levy's and the biggest other mod's.</summary>
		public double LevyShare, TopShare; public string TopMod = "";
		public double TotalMsPerSec, LevyMsPerSec, TopMsPerSec;
		public string Boss = "";
		/// <summary>Per mod, ms per second over the laggy windows, biggest first (the base game as "Terraria").</summary>
		public List<KeyValuePair<string, double>> ByMod = new();
		public string Headline = "", Cause = ""; public string[] Detail = Array.Empty<string>();
		/// <summary>The options the popup offers, in order. Empty when no popup is to be shown.</summary>
		public List<PopupOption> Options = new();
		public bool ShowPopup => Options.Count > 0;
		/// <summary>The laggy windows show the Frame Skip On pattern (frames dropped, updates kept) with Frame Skip set to On.</summary>
		public bool FrameSkipOn;
		/// <summary>The levy weapons over the whole fight, ms per update, biggest first, and all of them together (empty when no levy was timed).</summary>
		public List<WeaponRow> Weapons = new(); public double WeaponAttribMs; public double Levies;
		/// <summary>The active army size suggested for this fight (-1: none), and the update time and army cost it was worked out from (ms per tick).</summary>
		public int SuggestedArmy = -1; public double ArmyUpdMs, ArmyCostMs;
	}

	internal static class PerfAnalysis
	{
		public static string Mod(string name) => new PerfItem("", name, 0, 0).Mod;

		/// <summary>Self-check: false hides the active army option, as if the roster could not take it.</summary>
		public static bool ActiveArmyAvailable = true;

		/// <summary>Why a popup may not be shown now, or null when it may. The rules in one place, in the order they are checked.</summary>
		public static string Refusal(bool enabled, bool dontAsk, bool bossNear, DateTime now, DateTime lastShown, DateTime snoozeUntil)
		{
			if (!enabled) return "turned off";
			if (dontAsk) return "don't ask again";
			if (bossNear) return "a fight is on";
			if (now < snoozeUntil) return "after It's fine";
			if ((now - lastShown).TotalSeconds < PerfThresholds.PopupGapSecs) return "rate limit";
			return null;
		}

		/// <summary>The label of an option. Nothing takes command points or soldiers from the army: the active army size only sets how many of them are in the field at once, and the player picks it.</summary>
		public static string Label(PopupOption o) => o switch
		{
			PopupOption.LowerLevyEffects => "Lower levy effects",
			PopupOption.ActiveArmy => "Set active army size",
			PopupOption.ItsFine => "It's fine",
			PopupOption.DontAskAgain => "Don't ask again",
			PopupOption.OpenSettings => "Open Levy settings",
			PopupOption.FrameSkipSubtle => "Set Frame Skip to Subtle",
			_ => o.ToString(),
		};

		// ----------------------------------------------------------------------------------- attribution
		/// <summary>Per mod, ms per second over the span. Levy-owned work and army shots are "Levy"; the base game's unattributed rest is added to "Terraria".</summary>
		public static List<KeyValuePair<string, double>> ByMod(PerfSpan s, out double levyMs, out double attributed)
		{
			var by = new Dictionary<string, double>(); levyMs = 0; attributed = 0;
			if (s.Secs <= 0) return new();
			foreach (var it in s.Items.Values)
			{
				if (PerfCat.IsDrill(it.Cat)) continue;
				double ms = s.MsPerSec(it);
				string mod = it.Cat == PerfCat.Army || it.Cat == PerfCat.DrawArmy ? "Levy" : it.Mod;
				if (it.Cat == PerfCat.Army || it.Cat == PerfCat.DrawArmy) { /* army shots count as Levy's */ }
				by.TryGetValue(mod, out double a); by[mod] = a + ms; attributed += ms;
			}
			// Global AI hooks sit inside NPC and projectile rows already taken out of the owner (see PerfAnalyzer), so they are additive here too.
			levyMs = by.TryGetValue("Levy", out double l) ? l : 0;
			double rest = Math.Max(0, s.TotalMsPerSec - attributed);
			by.TryGetValue("Terraria", out double t); by["Terraria"] = t + rest;
			return by.OrderByDescending(kv => kv.Value).ToList();
		}

		/// <summary>The decision about one fight: laggy or not, the cause, and the popup's words and options.</summary>
		public static LagVerdict Judge(IList<PerfWindow> fight)
		{
			var v = JudgeCause(fight);
			if (v.Laggy) FrameSkipAdvice(v, fight);
			WeaponAdvice(v, fight);
			if (v.Laggy) ArmyAdvice(v, fight);
			return v;
		}

		// ----------------------------------------------------------------------------------- active army size
		/// <summary>
		/// The active army size that brings an update of <paramref name="updMs"/> down to <paramref name="targetMs"/>. The army's own cost per update (<paramref name="armyMs"/>: its bodies and shots) shared out over the
		/// <paramref name="soldiersOnField"/> soldier bodies it was measured on is the cost of one soldier; enough of the player's <paramref name="fielded"/> soldiers come off the field to save the excess.
		/// Never below <see cref="PerfThresholds.ArmyMinSuggest"/>. -1 when nothing needs to change or a number is missing.
		/// </summary>
		public static int SuggestArmyCap(double updMs, double armyMs, double soldiersOnField, int fielded, double targetMs = PerfThresholds.ArmyTargetMs)
		{
			if (!(soldiersOnField > 0) || fielded <= 0 || !(armyMs > 0) || double.IsNaN(updMs) || double.IsInfinity(updMs)) return -1;
			double excess = updMs - targetMs;
			if (!(excess > 0)) return -1;
			double per = Math.Min(armyMs, updMs) / soldiersOnField;
			if (per <= 1e-6) return -1;
			int cut = (int)Math.Ceiling(excess / per - 1e-9);
			int cap = Math.Max(PerfThresholds.ArmyMinSuggest, fielded - cut);
			return cap >= fielded ? -1 : cap;
		}

		/// <summary>The army's cost per update in a window, ms: its bodies, its shots, other mods' hooks on them and the owner swaps (the weapon attribution's reference), else Levy's own NPC row plus the army's shots. 0 unknown.</summary>
		public static double ArmyMs(PerfWindow w)
		{
			if (w == null) return 0;
			if (w.ArmyRefMs != null && w.ArmyRefMs.Length > 0) { double t = 0; foreach (double d in w.ArmyRefMs) t += Math.Max(0, d); if (t > 0) return t; }
			if (w.Updates <= 0) return 0;
			double ms = 0;
			foreach (var it in w.Top) if (it.Cat == PerfCat.Army || (it.Cat == PerfCat.Npc && it.Mod == "Levy")) ms += it.Ms;
			return ms / w.Updates;
		}

		/// <summary>The update time a suggestion aims for: 12 ms, or with Frame Skip Subtle slowing the game, what leaves room for this window's drawing inside one frame (never under 4 ms).</summary>
		private static double ArmyTarget(PerfWindow w, bool speedCase) => speedCase ? Math.Max(4.0, Math.Min(PerfThresholds.ArmyTargetMs, 15.9 - w.DrawMs)) : PerfThresholds.ArmyTargetMs;

		/// <summary>
		/// The live notice (once a session, from the analyzer as each 8-second window closes): the whole window ran at <see cref="PerfThresholds.ArmySlowUpdMs"/> ms an update or more, or with Frame Skip Subtle the game ran under
		/// <see cref="PerfThresholds.ArmySlowSpeed"/> of full speed, the army was a big part of the update, and an active army size would bring it down. <paramref name="mine"/>: the player's own soldiers in the field.
		/// Null when there is nothing to say.
		/// </summary>
		public static LagVerdict ArmyNotice(PerfWindow w, int mine)
		{
			if (w == null || !w.Focus || w.Updates <= 0 || w.Secs <= 0 || mine < PerfThresholds.ArmyMinFielded || !ActiveArmyAvailable) return null;
			bool slowUpd = w.UpdMs >= PerfThresholds.ArmySlowUpdMs;
			bool slowSpeed = w.FrameSkip == 2 && w.Ups < PerfThresholds.ArmySlowSpeed * 60.0;
			if (!slowUpd && !slowSpeed) return null;
			double army = ArmyMs(w);
			if (!(army >= PerfThresholds.ArmyBigShare * w.UpdMs)) return null;
			int cap = SuggestArmyCap(w.UpdMs, army, Math.Max(w.Levies, mine), mine, ArmyTarget(w, !slowUpd));
			if (cap < 0) return null;
			var v = new LagVerdict { Kind = LagKind.ArmyTooBig, Laggy = true, SuggestedArmy = cap, ArmyUpdMs = w.UpdMs, ArmyCostMs = army, Levies = mine, MedianFps = w.Fps, MedianUps = w.Ups };
			v.Headline = "This fight is lagging.";
			v.Detail = new[] { ArmyNoticeLine(cap) };
			v.Options = new List<PopupOption> { PopupOption.ActiveArmy, PopupOption.OpenSettings, PopupOption.DontAskAgain };
			return v;
		}

		/// <summary>The notice's words.</summary>
		public static string ArmyNoticeLine(int cap) => string.Format(CultureInfo.InvariantCulture, "Your army is more than this computer can run smoothly (about {0} soldiers at once). Set an active army size?", cap);

		/// <summary>A laggy fight where the army was the cost: the active army size that would have brought its laggy windows to about 12 ms an update, offered in the popup with a line that says so.</summary>
		private static void ArmyAdvice(LagVerdict v, IList<PerfWindow> fight)
		{
			if (v.Kind != LagKind.Levy || !ActiveArmyAvailable) return;
			var laggy = fight.Where(w => w != null && w.Focus && w.Secs > 0 && w.Frames > 0 && w.Updates > 0 && (w.Fps < PerfThresholds.LagFps || w.Ups < PerfThresholds.LagUps)).ToList();
			double secs = laggy.Sum(w => w.Secs); if (secs <= 0) return;
			double upd = laggy.Sum(w => w.UpdMs * w.Secs) / secs, army = laggy.Sum(w => ArmyMs(w) * w.Secs) / secs, levies = laggy.Sum(w => w.Levies * w.Secs) / secs;
			var mineW = laggy.Where(w => w.Mine >= 0).ToList();
			double mine = mineW.Count > 0 ? mineW.Sum(w => w.Mine * w.Secs) / mineW.Sum(w => w.Secs) : levies;
			int fielded = (int)Math.Round(mine);
			int cap = SuggestArmyCap(upd, army, Math.Max(levies, mine), fielded);
			if (cap < 0) return;
			v.SuggestedArmy = cap; v.ArmyUpdMs = upd; v.ArmyCostMs = army;
			v.Detail = v.Detail.Concat(new[] { string.Format(CultureInfo.InvariantCulture, "About {0} soldiers at once would run smoothly here. Set an active army size?", cap) }).ToArray();
			int at = v.Options.IndexOf(PopupOption.LowerLevyEffects);
			v.Options.Insert(at >= 0 ? at + 1 : 0, PopupOption.ActiveArmy);
		}

		/// <summary>
		/// The game's Frame Skip set to On runs every update at full speed and drops the drawing when an update takes longer than a frame (16.7 ms): the frame rate then falls from 60 to a few frames a second while the updates
		/// stay near 60 (Eve's 11-17 pillar fights: 58 updates and 2 to 5 frames a second at 16 to 17 ms an update). Set to Subtle (the game's default) or Off, the game slows down instead and keeps drawing. When the laggy windows
		/// show that pattern with Frame Skip On, the verdict says so and offers to switch it to Subtle. The player's own setting, changed only by the button.
		/// </summary>
		private static void FrameSkipAdvice(LagVerdict v, IList<PerfWindow> fight)
		{
			var laggy = fight.Where(w => w != null && w.Focus && w.Secs > 0 && w.Frames > 0 && w.Updates > 0 && (w.Fps < PerfThresholds.LagFps || w.Ups < PerfThresholds.LagUps)).ToList();
			double secs = laggy.Sum(w => w.Secs); if (secs <= 0) return;
			double onSecs = laggy.Where(w => w.FrameSkip == 1).Sum(w => w.Secs);
			if (onSecs < 0.5 * secs) return;
			double fps = WeightedMedian(laggy, w => w.Fps), ups = WeightedMedian(laggy, w => w.Ups);
			if (!(fps < 20 && ups >= 50)) return; // the frames were dropped and the updates kept: the Frame Skip pattern
			v.FrameSkipOn = true;
			v.Detail = v.Detail.Concat(new[] { "Frame Skip is On: when an update runs long, the game drops frames to keep full speed.", "Set to Subtle, the game slows down instead and keeps drawing." }).ToArray();
			v.Options.Insert(0, PopupOption.FrameSkipSubtle);
			if (!v.Options.Contains(PopupOption.ItsFine)) v.Options.Add(PopupOption.ItsFine);
			if (string.IsNullOrEmpty(v.Headline)) v.Headline = "That fight got laggy.";
		}

		/// <summary>
		/// The levy weapons that cost the most over the fight (every focused window, not only the laggy ones): the verdict keeps them for the log and /levyperf fight, and a laggy fight's words name the top three with their share of
		/// the army's cost, plus a weapon that costs each levy holding it at least twice the army's average.
		/// </summary>
		private static void WeaponAdvice(LagVerdict v, IList<PerfWindow> fight)
		{
			var span = PerfSpan.Of(fight.Where(w => w != null && w.Focus && w.Secs > 0 && w.Updates > 0));
			v.Weapons = span.Weapons; v.WeaponAttribMs = span.WeaponAttribMs; v.Levies = span.Levies;
			if (!v.Laggy) return;
			var lines = WeaponLines(span, "this fight");
			if (lines.Count > 0) v.Detail = v.Detail.Concat(lines).ToArray();
		}

		private static string Ms(double v) => v.ToString(v < 0.995 ? "0.00" : "0.0", CultureInfo.InvariantCulture);

		/// <summary>"Heaviest levy weapons this fight: Terra Blade (5 levies) 1.2 ms/tick, 18% of the army; ..." and, for a weapon that costs each levy at least twice the army's average, "Dearest per levy: ...". Empty without data.</summary>
		public static List<string> WeaponLines(PerfSpan s, string when, int n = 3)
		{
			var o = new List<string>(); double tot = s.WeaponAttribMs;
			if (s.Weapons.Count == 0 || tot <= 0.005) return o;
			var top = s.Weapons.Where(r => r.Ms >= 0.005).Take(n).ToList(); if (top.Count == 0) return o;
			string L(double l) => l < 1.5 ? "1 levy" : l.ToString("0", CultureInfo.InvariantCulture) + " levies";
			o.Add("Heaviest levy weapons " + when + ": " + string.Join("; ", top.Select((r, i) => r.Name + " (" + L(r.Levies) + ") " + Ms(r.Ms) + " ms/tick, " + (r.Ms / tot * 100).ToString("0", CultureInfo.InvariantCulture) + (i == 0 ? "% of the army" : "%"))) + ".");
			double levies = Math.Max(1, s.Levies), avg = tot / levies;
			var dear = s.Weapons.Where(r => r.Levies >= 0.5 && r.MsPerLevy >= 0.1 && r.MsPerLevy >= 2 * avg).OrderByDescending(r => r.MsPerLevy).FirstOrDefault();
			if (dear != null) o.Add("Dearest per levy: " + dear.Name + ", " + Ms(dear.MsPerLevy) + " ms/tick for each levy holding it, " + (dear.MsPerLevy / avg).ToString("0.0", CultureInfo.InvariantCulture) + " times the army's average of " + Ms(avg) + ".");
			return o;
		}

		private static LagVerdict JudgeCause(IList<PerfWindow> fight)
		{
			var v = new LagVerdict();
			var ws = fight.Where(w => w != null && w.Focus && w.Secs > 0 && w.Frames > 0 && w.Updates > 0).ToList();
			v.FightSecs = ws.Sum(w => w.Secs);
			if (ws.Count > 0) v.Boss = ws.GroupBy(w => w.Boss ?? "").OrderByDescending(g => g.Sum(w => w.Secs)).First().Key;
			if (v.FightSecs < PerfThresholds.MinFightSecs) { v.Kind = LagKind.TooShort; return v; }
			v.MedianFps = WeightedMedian(ws, w => w.Fps); v.MedianUps = WeightedMedian(ws, w => w.Ups); v.MinFps = ws.Min(w => w.Fps);
			var laggy = ws.Where(w => w.Fps < PerfThresholds.LagFps || w.Ups < PerfThresholds.LagUps).ToList();
			v.LaggySecs = laggy.Sum(w => w.Secs); v.LaggyShare = v.LaggySecs / v.FightSecs;
			if (v.LaggyShare <= PerfThresholds.LaggyShare) return v;
			v.Laggy = true;
			var span = PerfSpan.Of(laggy);
			v.TotalMsPerSec = span.TotalMsPerSec;
			if (span.Legacy)
			{
				// an old recording: only Levy's own AI and gear upkeep per update are known (the draw side and every other mod are not)
				v.LevyMsPerSec = span.LegacyLevyMsPerSec; v.LevyShare = span.TotalMsPerSec > 0 ? v.LevyMsPerSec / span.TotalMsPerSec : 0;
				v.ByMod = new List<KeyValuePair<string, double>> { new("Levy", v.LevyMsPerSec), new("not measured by this recording", Math.Max(0, span.TotalMsPerSec - v.LevyMsPerSec)) };
				if (v.LevyShare >= PerfThresholds.LevyBigShare) SetLevy(v); else SetUnknown(v);
				return v;
			}
			v.ByMod = ByMod(span, out double levyMs, out double attributed);
			v.LevyMsPerSec = levyMs; v.LevyShare = span.TotalMsPerSec > 0 ? levyMs / span.TotalMsPerSec : 0;
			var other = v.ByMod.Where(kv => kv.Key != "Levy").ToList();
			var topNamed = other.FirstOrDefault(kv => kv.Key != "Terraria");
			var topAny = other.FirstOrDefault();
			double tot = span.TotalMsPerSec;
			v.TopMod = topAny.Key ?? ""; v.TopMsPerSec = topAny.Value; v.TopShare = tot > 0 ? topAny.Value / tot : 0;
			double namedShare = tot > 0 && topNamed.Key != null ? topNamed.Value / tot : 0;
			if (attributed < 0.05 * tot) { SetUnknown(v); return v; }
			// Levy is named when it is a big part of the loop's time and not clearly behind the biggest other mod (the base game's rest is not a mod and does not count against it)
			if (v.LevyShare >= PerfThresholds.LevyBigShare && v.LevyShare >= PerfThresholds.LevyLeadsRatio * namedShare) { SetLevy(v); return v; }
			if (topNamed.Key != null && namedShare >= PerfThresholds.ModBigShare)
			{
				v.Kind = LagKind.OtherMod; v.TopMod = topNamed.Key; v.TopMsPerSec = topNamed.Value; v.TopShare = namedShare;
				v.Cause = ModCause(span, topNamed.Key);
				v.Headline = "That fight got laggy.";
				var bigRow = span.Items.Values.Where(it => !PerfCat.IsDrill(it.Cat) && it.Cat != PerfCat.Army && it.Cat != PerfCat.DrawArmy).OrderByDescending(it => it.Ms).FirstOrDefault();
				string lead = namedShare >= PerfThresholds.ModMostShare ? "Most of the cost came from " : bigRow != null && bigRow.Mod == topNamed.Key ? "The biggest single cost came from " : "A big part of the cost came from ";
				v.Detail = new[] { lead + topNamed.Key + ".", v.Cause, Stats(v) }.Where(x => x.Length > 0).ToArray();
				v.Options = new List<PopupOption> { PopupOption.ItsFine, PopupOption.DontAskAgain, PopupOption.OpenSettings };
				return v;
			}
			if (v.TopShare >= PerfThresholds.ModBigShare && topAny.Key == "Terraria")
			{
				v.Kind = LagKind.BaseGame; v.Headline = "That fight got laggy.";
				v.Cause = BaseCause(span);
				v.Detail = new[] { (v.TopShare >= PerfThresholds.ModMostShare ? "Most" : "A big part") + " of the cost came from the base game.", v.Cause, Stats(v) }.Where(x => x.Length > 0).ToArray();
				v.Options = new List<PopupOption> { PopupOption.ItsFine, PopupOption.DontAskAgain, PopupOption.OpenSettings };
				return v;
			}
			SetUnknown(v); return v;
		}

		private static void SetLevy(LagVerdict v)
		{
			v.Kind = LagKind.Levy; v.Headline = "That fight got laggy.";
			v.Detail = new[] { "Your army was a big part of the cost.", string.Format(CultureInfo.InvariantCulture, "Levy took about {0:0}% of the game's time.", v.LevyShare * 100), Stats(v) }.Where(x => x.Length > 0).ToArray();
			v.Options = new List<PopupOption> { PopupOption.LowerLevyEffects }; // the active army size is added after it when a size would help (ArmyAdvice)
			v.Options.Add(PopupOption.ItsFine); v.Options.Add(PopupOption.DontAskAgain); v.Options.Add(PopupOption.OpenSettings);
		}

		private static void SetUnknown(LagVerdict v)
		{
			v.Kind = LagKind.Unknown; v.Headline = "That fight got laggy.";
			v.Detail = new[] { "The cost could not be traced to one source.", Stats(v) }.Where(x => x.Length > 0).ToArray();
			v.Options = new List<PopupOption>(); // nothing to offer: a popup that blames nobody helps nobody (the log and /levyperf still have the numbers)
		}

		private static string Stats(LagVerdict v) => string.Format(CultureInfo.InvariantCulture, "Median {0:0} fps and {1:0} updates per second; the lowest was {2:0} fps.", v.MedianFps, v.MedianUps, v.MinFps);

		/// <summary>What inside a mod cost the most, in words (the biggest rows of that mod).</summary>
		public static string ModCause(PerfSpan s, string mod)
		{
			var rows = s.Items.Values.Where(it => !PerfCat.IsDrill(it.Cat) && it.Cat != PerfCat.Army && it.Cat != PerfCat.DrawArmy && it.Mod == mod).OrderByDescending(it => it.Ms).Take(2).ToList();
			if (rows.Count == 0) return "";
			return string.Join(" ", rows.Select(it => Describe(it, s) + "."));
		}

		private static string BaseCause(PerfSpan s)
		{
			var parts = new List<string>();
			if (s.DrawSplit[2] * s.Fps >= 0.15 * s.TotalMsPerSec && s.Dust > 0) parts.Add(string.Format(CultureInfo.InvariantCulture, "Drawing {0:#,0} dust particles took {1:0.0} ms per frame", s.Dust, s.DrawSplit[2]));
			if (s.DrawSplit[3] * s.Fps >= 0.1 * s.TotalMsPerSec) parts.Add(string.Format(CultureInfo.InvariantCulture, "Drawing gore took {0:0.0} ms per frame", s.DrawSplit[3]));
			if (s.UpdSplit[0] * s.Ups >= 0.2 * s.TotalMsPerSec) parts.Add(string.Format(CultureInfo.InvariantCulture, "NPC updates took {0:0.0} ms per update", s.UpdSplit[0]));
			if (s.UpdSplit[1] * s.Ups >= 0.2 * s.TotalMsPerSec) parts.Add(string.Format(CultureInfo.InvariantCulture, "Projectile updates took {0:0.0} ms per update", s.UpdSplit[1]));
			return parts.Count == 0 ? "" : string.Join(". ", parts) + ".";
		}

		public static double WeightedMedian(IList<PerfWindow> ws, Func<PerfWindow, double> f)
		{
			var l = ws.Select(w => (v: f(w), w: w.Secs)).OrderBy(x => x.v).ToList();
			double total = l.Sum(x => x.w), half = total / 2, acc = 0;
			foreach (var x in l) { acc += x.w; if (acc >= half) return x.v; }
			return l.Count > 0 ? l[l.Count - 1].v : 0;
		}

		// ----------------------------------------------------------------------------------- words
		public static string Describe(PerfItem it, PerfSpan s)
		{
			double perTick = s.Updates > 0 ? it.Ms / s.Updates : 0, perFrame = s.Frames > 0 ? it.Ms / s.Frames : 0, perCall = it.N > 0 ? it.Ms / it.N : 0;
			string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
			switch (it.Cat)
			{
				case PerfCat.Npc: return "NPC updates of " + ModName(it.Name) + ": " + F(perTick) + " ms per update";
				case PerfCat.NpcType: return "NPC " + it.Name + ": " + F(perTick) + " ms per update";
				case PerfCat.Proj: return "projectile updates of " + ModName(it.Name) + ": " + F(perTick) + " ms per update";
				case PerfCat.ProjType: return "projectile " + it.Name + ": " + F(perTick) + " ms per update";
				case PerfCat.Army: return "projectiles fired by the army: " + F(perTick) + " ms per update";
				case PerfCat.Glob: return "GlobalNPC and GlobalProjectile AI hooks of " + ModName(it.Name) + ": " + F(perTick) + " ms per update";
				case PerfCat.DrawNpc: return "NPC drawing of " + ModName(it.Name) + ": " + F(perFrame) + " ms per frame";
				case PerfCat.DrawProj: return "projectile drawing of " + ModName(it.Name) + ": " + F(perFrame) + " ms per frame";
				case PerfCat.DrawArmy: return "drawing projectiles fired by the army: " + F(perFrame) + " ms per frame";
				case PerfCat.Map: return "map layer " + it.Name + ": " + F(perCall) + " ms per frame";
				case PerfCat.Ui: return "interface layer " + it.Name + ": " + F(perCall) + " ms per frame";
				default: return it.Cat + " " + it.Name + ": " + F(it.Ms / Math.Max(1, s.Secs)) + " ms per second";
			}
		}
		private static string ModName(string n) => n == "Terraria" ? "the base game" : n;

		/// <summary>The words of /levyperf: the last minute, plainly.</summary>
		public static List<string> Report(PerfSpan s, int rows = 5)
		{
			var o = new List<string>();
			if (s.Windows == 0 || s.Secs <= 0) { o.Add("No performance windows yet. The analyzer closes one every eight seconds."); return o; }
			string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
			o.Add($"Last {s.Secs:0} seconds: {F(s.Fps)} frames and {F(s.Ups)} updates per second. An update takes {F(s.UpdMs)} ms and a frame draws in {F(s.DrawMs)} ms (worst update {F(s.WorstUpdMs)} ms, worst frame {F(s.WorstFrameMs)} ms).");
			if (!s.Legacy) o.Add($"On screen: {s.Npcs:0} NPCs ({s.Levies:0} levies), {s.Projs:0} projectiles ({s.Minions:0} minions), {s.Dust:#,0} dust, {s.Gore:0} gore.");
			if (s.Legacy) { o.Add($"Levy's own work: {F(s.LegacyLevyMsPerSec / Math.Max(1, s.Ups))} ms per update. This recording did not measure other mods."); }
			else
			{
				o.Add($"Frame split, ms per frame: NPCs {F(s.DrawSplit[0])}, projectiles {F(s.DrawSplit[1])}, dust {F(s.DrawSplit[2])}, gore {F(s.DrawSplit[3])}, interface {F(s.DrawSplit[4])}, players {F(s.DrawSplit[5])}. Update split, ms per update: NPCs {F(s.UpdSplit[0])}, projectiles {F(s.UpdSplit[1])}.");
				var by = ByMod(s, out _, out _);
				double tot = Math.Max(1e-6, s.TotalMsPerSec);
				o.Add("Cost by mod: " + string.Join(", ", by.Take(rows).Select(kv => $"{(kv.Key == "Terraria" ? "base game" : kv.Key)} {kv.Value / tot * 100:0}% ({kv.Value:0} ms/s)")) + ".");
				var items = s.Items.Values.Where(it => !PerfCat.IsDrill(it.Cat) || true).OrderByDescending(it => it.Ms).Take(rows).ToList();
				if (items.Count > 0) o.Add("Biggest single costs: " + string.Join("; ", items.Select(it => Describe(it, s))) + ".");
			}
			if (!s.Legacy) o.AddRange(WeaponLines(s, "in the last minute", 5));
			var lv = s.LevyBuckets.OrderByDescending(kv => kv.Value).Where(kv => kv.Value >= 0.005).Take(6).ToList();
			if (lv.Count > 0) o.Add("Levy buckets, ms per update: " + string.Join(", ", lv.Select(kv => kv.Key + " " + kv.Value.ToString("0.00", CultureInfo.InvariantCulture))) + ".");
			return o;
		}

		// ----------------------------------------------------------------------------------- self-check
		/// <summary>Builds a window for the synthetic fights of the self-check and the tool. The loop is fully busy (1000 ms per second: 55 percent updates, 45 percent drawing);
		/// <paramref name="levyShare"/> and <paramref name="modShare"/> are the fractions of it given to Levy's army and to one other mod, the base game takes 8 percent and the rest is unattributed.</summary>
		public static PerfWindow Synth(double secs, double fps, double ups, double levyShare, string mod, double modShare, string boss = "Plantera", bool focus = true)
		{
			var w = new PerfWindow { Utc = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc), Secs = secs, Fps = fps, Ups = ups, Boss = boss, Focus = focus, Updates = (int)(ups * secs), Frames = (int)(fps * secs) };
			w.UpdMs = 550.0 / Math.Max(1, ups); w.DrawMs = 450.0 / Math.Max(1, fps); w.Npcs = 80; w.Projs = 200; w.Dust = 500; w.Gore = 20; w.Levies = 40; w.Minions = 5;
			double T = 1000 * secs;
			w.Top.Add(new PerfItem(PerfCat.Npc, "Levy", levyShare * T * 0.7, w.Updates * 40.0));
			w.Top.Add(new PerfItem(PerfCat.Army, "all", levyShare * T * 0.3, w.Updates * 30.0));
			if (mod != null) w.Top.Add(new PerfItem(PerfCat.Npc, mod, modShare * T, w.Updates * 3.0));
			w.Top.Add(new PerfItem(PerfCat.Npc, "Terraria", 0.08 * T, w.Updates * 20.0));
			return w;
		}
	}
}
