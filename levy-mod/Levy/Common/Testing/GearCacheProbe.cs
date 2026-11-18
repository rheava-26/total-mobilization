using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Terraria;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// GEAR CACHE PROBE (lab, LEVY_GCPROBE=1): what the per-tick gear frame changes, phase by phase, for every levy that runs it. At six points of each frame (start; after the timers and ResetEffects; after the buffs;
	/// after the equips, sets, stations and potions; after PostUpdateEquips; after PostUpdateMiscEffects) the whole dummy is imaged (StateImage: the Player, every ModPlayer, the worn items with their ModItem and globals,
	/// and the game's random generator) and compared with the previous point and with the same point of the levy's previous frame. For each split k (skip the phases up to k, keep the rest) it then answers, frame by frame,
	/// the question the cache asks: had the phases up to k been skipped and every field they touched last frame been put back to last frame's value, would the dummy be exactly what the full phases made?
	/// Output at each fight end: &lt;out&gt;.gcprobe.tsv (per field) and HARNESS-GCP summary lines. Measurement only.
	/// </summary>
	internal static class GearCacheProbe
	{
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_GCPROBE") == "1";
		internal const int NB = 6; // boundaries 0..5
		internal static readonly string[] Phase = { "start", "reset", "buffs", "equips", "postequips", "misc" };

		private sealed class Lv
		{
			public StateImage Img = new();
			public List<(string p, long v)>[] Cur = new List<(string, long)>[NB], Prev = new List<(string, long)>[NB];
			public uint PrevTick = uint.MaxValue; public bool Open;
			public HashSet<string>[] TouchedPrev = new HashSet<string>[NB], TouchedCur = new HashSet<string>[NB];
		}

		private sealed class Stat { public long[] Chg = new long[NB], Var = new long[NB], Miss = new long[NB]; public long Ext; public HashSet<int> Levies = new(); }

		private static readonly Dictionary<int, Lv> Levies = new();
		private static readonly Dictionary<string, Stat> Stats = new();
		private static readonly long[] Frames = new long[NB], Exact = new long[NB];
		private static long _framesAll, _consec, _captureTicks, _leaves, _nodes, _trunc;
		private static readonly List<object> _roots = new(); private static readonly List<string> _names = new();

		private static readonly int MaxLevies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GCPROBE_N"), out int n) ? n : 6;
		private static readonly int Mod = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GCPROBE_MOD"), out int m) ? m : 0;
		private static bool _dumped;

		internal static void Mark(SoldierNPC s, Player d, int k)
		{
			if (!On || s?.Record == null) return;
			long t0 = Stopwatch.GetTimestamp();
			try
			{
				int id = s.NPC.whoAmI;
				if (!Levies.TryGetValue(id, out var lv)) { if (Levies.Count >= MaxLevies || (Mod > 1 && id % Mod != 1)) return; Levies[id] = lv = new Lv(); }
				if (k == 0) lv.Open = true; else if (!lv.Open) return;
				_roots.Clear(); _names.Clear();
				_roots.Add(d); _names.Add("P");
				foreach (var mp in d.ModPlayers) { _roots.Add(mp); _names.Add(mp.GetType().Name); }
				int ii = 0;
				foreach (var it in s.Record.Gear.AllItems()) if (it != null && !it.IsAir) { _roots.Add(it); _names.Add("I" + ii++ + ":" + it.Name); }
				Item held = d.inventory[d.selectedItem]; if (held != null && !held.IsAir) { _roots.Add(held); _names.Add("Held:" + held.Name); }
				_roots.Add(Main.rand); _names.Add("RNG");
				lv.Img.Capture(_roots, _names);
				if (!_dumped) { _dumped = true; Dump(lv.Img, t0); }
				var list = lv.Cur[k] ??= new List<(string, long)>(8192); list.Clear();
				lv.Img.Flatten((p, v) => list.Add((p, v)));
				if (k == 0) { _leaves += list.Count; _nodes += lv.Img.NodeCount; if (lv.Img.Truncated) _trunc++; }
				if (k == NB - 1) { Analyze(id, lv); lv.Open = false; }
			}
			catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-GCP: probe failed: " + e); }
			finally { _captureTicks += Stopwatch.GetTimestamp() - t0; }
		}

		private static void Dump(StateImage img, long t0)
		{
			long t1 = Stopwatch.GetTimestamp(); int c = 0; img.Flatten((p, v) => c++); long t2 = Stopwatch.GetTimestamp();
			var log = global::Levy.Levy.Instance.Logger;
			log.Info($"HARNESS-GCP: image nodes {img.NodeCount} leaves {img.LeafCount} flat {c} truncated {img.Truncated} capture ms {(t1 - t0) * 1000.0 / Stopwatch.Frequency:0.00} flatten ms {(t2 - t1) * 1000.0 / Stopwatch.Frequency:0.00}");
			log.Info("HARNESS-GCP: biggest nodes: " + string.Join(", ", img.NodeList().OrderByDescending(x => x.leaves).Take(30).Select(x => x.path + " " + x.leaves)));
			var byRoot = new Dictionary<string, (int n, long l)>();
			foreach (var x in img.NodeList()) { string r = x.path.Split('.')[0]; var v = byRoot.TryGetValue(r, out var q) ? q : (n: 0, l: 0L); byRoot[r] = (v.n + 1, v.l + x.leaves); }
			log.Info("HARNESS-GCP: by root: " + string.Join(", ", byRoot.OrderByDescending(x => x.Value.l).Take(40).Select(x => x.Key + " " + x.Value.n + "/" + x.Value.l)));
		}

		private static Dictionary<string, long> Map(List<(string p, long v)> l) { var m = new Dictionary<string, long>(l.Count); foreach (var (p, v) in l) m[p] = v; return m; }

		private static void Analyze(int id, Lv lv)
		{
			_framesAll++;
			uint now = Main.GameUpdateCount;
			bool consec = lv.PrevTick != uint.MaxValue && now - lv.PrevTick == 1;
			var m = new Dictionary<string, long>[NB];
			for (int k = 0; k < NB; k++) m[k] = Map(lv.Cur[k]);
			// touched up to k this frame: fields whose value at k differs from the start
			for (int k = 1; k < NB; k++)
			{
				var t = lv.TouchedCur[k] ??= new HashSet<string>(); t.Clear();
				foreach (var kv in m[k]) if (!m[0].TryGetValue(kv.Key, out long v0) || v0 != kv.Value) t.Add(kv.Key);
			}
			// per phase changes
			for (int k = 1; k < NB; k++)
				foreach (var kv in m[k]) if (!m[k - 1].TryGetValue(kv.Key, out long w) || w != kv.Value) S(kv.Key, id).Chg[k]++;
			if (consec)
			{
				_consec++;
				var pm = new Dictionary<string, long>[NB];
				for (int k = 0; k < NB; k++) pm[k] = Map(lv.Prev[k]);
				foreach (var kv in m[0]) if (!pm[NB - 1].TryGetValue(kv.Key, out long w) || w != kv.Value) S(kv.Key, id).Ext++;
				foreach (var kv in m[0]) if (!pm[0].TryGetValue(kv.Key, out long w) || w != kv.Value) S(kv.Key, id).Var[0]++;
				for (int k = 1; k < NB; k++)
				{
					foreach (var kv in m[k]) if (!pm[k].TryGetValue(kv.Key, out long w) || w != kv.Value) S(kv.Key, id).Var[k]++;
					// the cached prediction at split k: touched last frame -> last frame's value at k; otherwise this frame's start value
					Frames[k]++; int miss = 0;
					var tp = lv.TouchedPrev[k];
					foreach (var kv in m[k])
					{
						long pred;
						if (tp != null && tp.Contains(kv.Key)) { if (!pm[k].TryGetValue(kv.Key, out pred)) pred = ~kv.Value; }
						else if (!m[0].TryGetValue(kv.Key, out pred)) pred = ~kv.Value;
						if (pred != kv.Value) { miss++; S(kv.Key, id).Miss[k]++; }
					}
					if (miss == 0) Exact[k]++;
				}
			}
			for (int k = 0; k < NB; k++) { var tmp = lv.Prev[k]; lv.Prev[k] = lv.Cur[k]; lv.Cur[k] = tmp; }
			for (int k = 1; k < NB; k++) { var tmp = lv.TouchedPrev[k]; lv.TouchedPrev[k] = lv.TouchedCur[k]; lv.TouchedCur[k] = tmp; }
			lv.PrevTick = now;
		}

		private static Stat S(string p, int id) { if (!Stats.TryGetValue(p, out var s)) Stats[p] = s = new Stat(); s.Levies.Add(id); return s; }

		internal static void Finish(string key, string outPath)
		{
			if (!On) return;
			var log = global::Levy.Levy.Instance.Logger;
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			double ms = _captureTicks * 1000.0 / Stopwatch.Frequency;
			log.Info($"HARNESS-GCP: {key} frames {_framesAll} consecutive {_consec} leaves/frame {(_framesAll > 0 ? _leaves / (double)_framesAll : 0):0} nodes/frame {(_framesAll > 0 ? _nodes / (double)_framesAll : 0):0} truncated {_trunc} probe ms {ms:0}");
			for (int k = 1; k < NB; k++)
			{
				log.Info($"HARNESS-GCP: split after {Phase[k]}: {Exact[k]}/{Frames[k]} frames exact ({(Frames[k] > 0 ? 100.0 * Exact[k] / Frames[k] : 0):0.0}%); top misses: " +
					string.Join(", ", Stats.Where(kv => kv.Value.Miss[k] > 0).OrderByDescending(kv => kv.Value.Miss[k]).Take(25).Select(kv => kv.Key + " " + kv.Value.Miss[k])));
			}
			log.Info("HARNESS-GCP: start-to-start changes, top: " + string.Join(", ", Stats.Where(kv => kv.Value.Var[0] > 0).OrderByDescending(kv => kv.Value.Var[0]).Take(60).Select(kv => kv.Key + " " + kv.Value.Var[0])));
			log.Info("HARNESS-GCP: external changes (between frames), top: " + string.Join(", ", Stats.Where(kv => kv.Value.Ext > 0).OrderByDescending(kv => kv.Value.Ext).Take(40).Select(kv => kv.Key + " " + kv.Value.Ext)));
			try
			{
				using var w = new StreamWriter(outPath + ".gcprobe.tsv", true);
				w.WriteLine("key\tfield\tlevies\text\t" + string.Join("\t", Enumerable.Range(1, NB - 1).Select(k => "chg_" + Phase[k])) + "\t" + string.Join("\t", Enumerable.Range(0, NB).Select(k => "var_" + Phase[k])) + "\t" + string.Join("\t", Enumerable.Range(1, NB - 1).Select(k => "miss_" + Phase[k])));
				foreach (var kv in Stats.OrderBy(x => x.Key, StringComparer.Ordinal))
				{
					var s = kv.Value;
					w.WriteLine(key + "\t" + kv.Key + "\t" + s.Levies.Count + "\t" + s.Ext + "\t" + string.Join("\t", Enumerable.Range(1, NB - 1).Select(k => s.Chg[k].ToString(inv))) + "\t" + string.Join("\t", Enumerable.Range(0, NB).Select(k => s.Var[k].ToString(inv))) + "\t" + string.Join("\t", Enumerable.Range(1, NB - 1).Select(k => s.Miss[k].ToString(inv))));
				}
				w.WriteLine(key + "\t#frames\t" + _framesAll + "\t" + _consec + "\t" + string.Join("\t", Enumerable.Range(1, NB - 1).Select(k => Exact[k] + "/" + Frames[k])));
			}
			catch (Exception e) { log.Warn("HARNESS-GCP: write failed " + e.Message); }
			Levies.Clear(); Stats.Clear(); Array.Clear(Frames); Array.Clear(Exact); _framesAll = _consec = _captureTicks = _leaves = _nodes = _trunc = 0;
		}
	}
}
