using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Levy.Content.NPCs;
using Terraria;

namespace Levy.Common.Testing
{
	/// <summary>
	/// GEAR CACHE in the calrev harness: the fight token gc=0 / gc=1 switches the cache for that fight (a same-boot A/B), and every fight ends with a HARNESS-GCH line (and a row in &lt;out&gt;.gcache.tsv):
	/// gear frames served from the cache and run in full, microseconds per frame of each kind and of all frames, the reasons a frame ran in full, and with LEVY_GEARCACHE_PARITY=1 the parity checks, how many
	/// differed and which leaves.
	/// </summary>
	public partial class CombatHarness
	{
		private static bool? _gchDefault, _nsDefault; private static uint _gchTick0;

		private void GchBegin(CrRun run)
		{
			_gchDefault ??= SoldierNPC.GearCache.On;
			SoldierNPC.GearCache.On = run.Gc == "0" ? false : run.Gc == "1" ? true : _gchDefault.Value;
			SoldierNPC.GearCache.ResetCounters(); GearCacheParity.Reset();
			Weapons.SwapProf.Reset(); System.Array.Clear(Weapons.OwnerPuppet.NestWhy); _gchTick0 = Main.GameUpdateCount; Compat.Calamity.CalamityLevyPatches.Bypass = run.Cp == "0"; _nsDefault ??= Weapons.OwnerPuppet.NestSkipOn; Weapons.OwnerPuppet.NestSkipOn = run.Ns == "0" ? false : run.Ns == "1" ? true : _nsDefault.Value;
			SoldierNPC.GearApprox.HarnessInterval = run.Gi > 0 ? run.Gi : 0; SoldierNPC.GearApprox.LearnEvery = run.Gl > 0 ? run.Gl : SoldierNPC.GearApprox.LearnEveryDefault; SoldierNPC.GearApprox.ResetCounters(); SoldierNPC.GearApproxProbe.Reset();
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
			{
				long served = SoldierNPC.GearApprox.Served, builds = SoldierNPC.GearApprox.Builds;
				string ga = $"gi={SoldierNPC.GearApprox.Interval} served={served} builds={builds} served%={(served + builds > 0 ? 100.0 * served / (served + builds) : 0).ToString("0.0", inv)} forced={SoldierNPC.GearApprox.Forced}"
					+ $" us/served={(served > 0 ? SoldierNPC.GearApprox.ServedFrameTicks / f / served : 0).ToString("0.0", inv)} us/fullframe={(builds > 0 ? SoldierNPC.GearApprox.FullFrameTicks / f / builds : 0).ToString("0.0", inv)}"
					+ $" apply={(served > 0 ? SoldierNPC.GearApprox.ApplyTicks / f / served : 0).ToString("0.0", inv)}us post={(builds > 0 ? SoldierNPC.GearApprox.PostTicks / f / builds : 0).ToString("0.0", inv)}us learn={(builds > 0 ? SoldierNPC.GearApprox.LearnTicks / f / builds : 0).ToString("0.0", inv)}us"
					+ $" shapeResets={SoldierNPC.GearApprox.ShapeResets} exemptSpawn={SoldierNPC.GearApprox.ExemptSpawn} exemptRng={SoldierNPC.GearApprox.ExemptRng} exemptCounter={SoldierNPC.GearApprox.ExemptCounter} masks={SoldierNPC.GearApprox.Masks} eligibleBuilds={SoldierNPC.GearApprox.EligibleBuilds} before={(SoldierNPC.GearApprox.EligibleBuilds > 0 ? SoldierNPC.GearApprox.BeforeTicks / f / SoldierNPC.GearApprox.EligibleBuilds : 0).ToString("0.0", inv)}us after={(SoldierNPC.GearApprox.EligibleBuilds > 0 ? SoldierNPC.GearApprox.AfterTicks / f / SoldierNPC.GearApprox.EligibleBuilds : 0).ToString("0.0", inv)}us maskTotal={(SoldierNPC.GearApprox.MaskTicks / f / 1000).ToString("0.0", inv)}ms build={(SoldierNPC.GearCache.BuildN > 0 ? SoldierNPC.GearCache.BuildTicks / f / SoldierNPC.GearCache.BuildN : 0).ToString("0.0", inv)}us"
					+ $" frames={GearUpkeep.Frames} us/frame={usAll.ToString("0.0", inv)}";
				Mod.Logger.Info($"HARNESS-GA: {run.Key} {ga}");
				if (Weapons.SwapProf.On)
				{
					double tk = Math.Max(1, Main.GameUpdateCount - _gchTick0), ms = 1000.0 / Stopwatch.Frequency;
					Mod.Logger.Info($"HARNESS-SWAP: {run.Key} ticks={tk} shotUpdates/tick={(Weapons.SwapProf.UpdN / tk).ToString("0.0", inv)} shotUpdate ms/tick={(Weapons.SwapProf.UpdTotal * ms / tk).ToString("0.000", inv)} ownerSwap(update) ms/tick={(Weapons.SwapProf.UpdSwap * ms / tk).ToString("0.000", inv)} ownerSwap(hit) ms/tick={(Weapons.SwapProf.DamSwap * ms / tk).ToString("0.000", inv)} hits/tick={(Weapons.SwapProf.DamN / tk).ToString("0.0", inv)} useGuard ms/tick={(Weapons.SwapProf.GuardSwap * ms / tk).ToString("0.000", inv)} heldView ms/tick={(Weapons.SwapProf.ViewSwap * ms / tk).ToString("0.000", inv)} views/tick={(Weapons.SwapProf.ViewN / tk).ToString("0.0", inv)} notPuppetable ms/tick={(Weapons.SwapProf.EnterMiss * ms / tk).ToString("0.000", inv)} nestSkip(test+skip) ms/tick={(Weapons.SwapProf.SkipSwap * ms / tk).ToString("0.000", inv)} nestSkips/tick={(Weapons.SwapProf.SkipN / tk).ToString("0.0", inv)} nestWhy/tick=[{string.Join(",", Weapons.OwnerPuppet.NestWhy.Select(x => (x / tk).ToString("0.0", inv)))}]");
				}
				Mod.Logger.Info($"HARNESS-CALPATCH: {run.Key} bypass={Compat.Calamity.CalamityLevyPatches.Bypass} androombaSkips={Compat.Calamity.CalamityLevyPatches.AndroombaSkips} lightSkips={Compat.Calamity.CalamityLevyPatches.LightSkips} drawSkips={Compat.Calamity.CalamityLevyPatches.DrawSkips} anyServed={Compat.Calamity.CalamityLevyPatches.AnyServed} anyFull={Compat.Calamity.CalamityLevyPatches.AnyFull} listBuilds={Compat.Calamity.CalamityLevyPatches.ListBuilds}");
				Mod.Logger.Info("HARNESS-GA: build reasons: " + string.Join(", ", SoldierNPC.GearApprox.Why.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)));
				foreach (var g in SoldierNPC.GearApprox.SpawnGear) Mod.Logger.Info("HARNESS-GA: exact (build spawns): " + g);
				foreach (var g in SoldierNPC.GearApprox.RngGear) Mod.Logger.Info("HARNESS-GA: exact (build draws random): " + g);
				foreach (var g in SoldierNPC.GearApprox.UpGear) Mod.Logger.Info("HARNESS-GA: exact (build counts up): " + g);
				if (SoldierNPC.GearApproxProbe.Checks > 0)
				{
					Mod.Logger.Info($"HARNESS-GA: probe checks={SoldierNPC.GearApproxProbe.Checks} diverged={SoldierNPC.GearApproxProbe.Diverged} missedSpawns={SoldierNPC.GearApproxProbe.MissedSpawns}");
					for (int i = 0; i < SoldierNPC.GearApproxProbe.StatNames.Length; i++)
					{
						long dn = SoldierNPC.GearApproxProbe.Diff[i]; if (dn == 0) continue;
						Mod.Logger.Info($"HARNESS-GA: probe stat {SoldierNPC.GearApproxProbe.StatNames[i]} differs={dn} ({(100.0 * dn / SoldierNPC.GearApproxProbe.Checks).ToString("0.00", inv)}%) higher={SoldierNPC.GearApproxProbe.Higher[i]} lower={SoldierNPC.GearApproxProbe.Lower[i]} meanAbs={(SoldierNPC.GearApproxProbe.SumAbs[i] / dn).ToString("0.####", inv)} maxAbs={SoldierNPC.GearApproxProbe.MaxAbs[i].ToString("0.####", inv)} meanRel={(100 * SoldierNPC.GearApproxProbe.SumRel[i] / dn).ToString("0.##", inv)}%");
					}
					foreach (var smp in SoldierNPC.GearApproxProbe.Samples) Mod.Logger.Info("HARNESS-GA: probe sample " + smp);
					Mod.Logger.Info("HARNESS-GA: probe leaves: " + string.Join(", ", SoldierNPC.GearApproxProbe.Leaves.OrderByDescending(kv => kv.Value).Take(60).Select(kv => kv.Key + " " + kv.Value)));
				}
			}
			try { using var w = new StreamWriter(_crOut + ".gcache.tsv", true); w.WriteLine(run.Key + "\t" + line + "\t" + string.Join(", ", why)); } catch (Exception) { }
		}
	}
}
