using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// GEAR CACHE in the calrev harness: the fight token gc=0 / gc=1 switches the cache for that fight (a same-boot A/B), and every fight ends with a HARNESS-GCH line (and a row in &lt;out&gt;.gcache.tsv):
	/// gear frames served from the cache and run in full, microseconds per frame of each kind and of all frames, the reasons a frame ran in full, and with LEVY_GEARCACHE_PARITY=1 the parity checks, how many
	/// differed and which leaves.
	/// </summary>
	public partial class CombatHarness
	{
		private static bool? _gchDefault;

		private void GchBegin(CrRun run)
		{
			_gchDefault ??= SoldierNPC.GearCache.On;
			SoldierNPC.GearCache.On = run.Gc == "0" ? false : run.Gc == "1" ? true : _gchDefault.Value;
			SoldierNPC.GearCache.ResetCounters(); GearCacheParity.Reset();
			GearCacheReadScan.Run(m => Mod.Logger.Info(m));
			GearUpkeep.Ticks = 0; GearUpkeep.Frames = 0;
		}

		private void GchFinish(CrRun run)
		{
			double f = Stopwatch.Frequency / 1e6;
			long hits = SoldierNPC.GearCache.Hits, fulls = SoldierNPC.GearCache.Fulls;
			double usAll = GearUpkeep.Frames > 0 ? GearUpkeep.Ticks / f / GearUpkeep.Frames : 0, usHit = hits > 0 ? SoldierNPC.GearCache.HitTicks / f / hits : 0, usFull = fulls > 0 ? SoldierNPC.GearCache.FullTicks / f / fulls : 0;
			var why = SoldierNPC.GearCache.Why.OrderByDescending(kv => kv.Value).Take(30).Select(kv => kv.Key + " " + kv.Value);
			var inv = System.Globalization.CultureInfo.InvariantCulture;
			string line = $"gc={(SoldierNPC.GearCache.On ? 1 : 0)} frames={GearUpkeep.Frames} us/frame={usAll.ToString("0.0", inv)} cached={hits} us/cached={usHit.ToString("0.0", inv)} full={fulls} us/full={usFull.ToString("0.0", inv)} hit%={(hits + fulls > 0 ? 100.0 * hits / (hits + fulls) : 0).ToString("0.0", inv)}"
				+ $" build={(SoldierNPC.GearCache.BuildN > 0 ? SoldierNPC.GearCache.BuildTicks / f / SoldierNPC.GearCache.BuildN : 0).ToString("0.0", inv)}us"
				+ $" take={(SoldierNPC.GearCache.TakeN > 0 ? SoldierNPC.GearCache.TakeTicks / f / SoldierNPC.GearCache.TakeN : 0).ToString("0.0", inv)}us rebuilt={SoldierNPC.GearCache.TakeRebuilt} cmp={(SoldierNPC.GearCache.TakeN > 0 ? SoldierNPC.GearCache.CmpTicks / f / SoldierNPC.GearCache.TakeN : 0).ToString("0.0", inv)}us mid={(fulls > 0 ? SoldierNPC.GearCache.MidTicks / f / fulls : 0).ToString("0.0", inv)}us apply={(hits > 0 ? SoldierNPC.GearCache.ApplyTicks / f / hits : 0).ToString("0.0", inv)}us"
				+ (GearCacheParity.On ? $" parity checks={GearCacheParity.Checks} diverged={GearCacheParity.Diverged} levies={GearCacheParity.Levies.Count}" : "");
			Mod.Logger.Info($"HARNESS-GCH: {run.Key} {line}");
			Mod.Logger.Info("HARNESS-GCH: full-frame reasons: " + string.Join(", ", why));
			Mod.Logger.Info("HARNESS-GCH: recapture time by node (sampled, us total): " + string.Join(", ", SoldierNPC.GearCache.NodeTicks.OrderByDescending(kv => kv.Value).Take(25).Select(kv => kv.Key + " " + (kv.Value / f).ToString("0", inv))));
			if (SoldierNPC.GearCache.NodeDump != null) { Mod.Logger.Info("HARNESS-GCH: image nodes: " + SoldierNPC.GearCache.NodeDump); SoldierNPC.GearCache.NodeDump = null; }
			foreach (var g in SoldierNPC.GearCache.RngGear) Mod.Logger.Info("HARNESS-GCH: build draws random with: " + g);
			SoldierNPC.GearCache.RngGear.Clear();
			Mod.Logger.Info("HARNESS-GCH: image rebuilt at: " + string.Join(", ", SoldierNPC.GearCache.Rebuilds.OrderByDescending(kv => kv.Value).Take(20).Select(kv => kv.Key + " " + kv.Value)));
			if (GearCacheParity.On)
			{
				Mod.Logger.Info("HARNESS-GCH: parity leaves: " + string.Join(", ", GearCacheParity.Fields.OrderByDescending(kv => kv.Value).Take(40).Select(kv => kv.Key + " " + kv.Value)));
				foreach (var s in GearCacheParity.Samples) Mod.Logger.Info("HARNESS-GCH: parity sample " + s);
			}
			try { using var w = new StreamWriter(_crOut + ".gcache.tsv", true); w.WriteLine(run.Key + "\t" + line + "\t" + string.Join(", ", why)); } catch (Exception) { }
		}
	}
}
