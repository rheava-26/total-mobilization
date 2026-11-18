using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// "[PerfBoss]" checks of the approximate gear frame ("Gear update interval when fighting", SoldierNPC.GearApprox.cs) and of the Calamity patches for levy dummies (CalamityLevyPatches.cs), on the standing army:
	/// <list type="bullet">
	/// <item>interval 1 serves nothing (the exact frame);</item>
	/// <item>interval 3: served ticks exist, every served tick's stats equal a full build's from the same state (nothing moves here, so any difference is an error, not lag), no cast is missed, and the tail runs on every frame;</item>
	/// <item>a change of gear and a buff on the body each rebuild on the very next tick, and the buff's stat is there at once;</item>
	/// <item>with Calamity: one gear frame run twice from the same state, patches bypassed and in force, leaves the same dummy (outside the lighting numbers the skipped call sets), and the patched projectile question gives the original's answer.</item>
	/// </list>
	/// </summary>
	public partial class PerfBossCheck
	{
		private static void GearApproxChecks()
		{
			var sample = Army.Where(s => s != null && s.NPC.active && s.UpkeepActive).ToList();
			if (sample.Count < 4) { Note($"approximate gear frame: only {sample.Count} levies run the per-tick gear frame here; not checked"); return; }
			bool dormWas = SoldierNPC.DormancyOn, probeWas = SoldierNPC.GearApprox.Probe; int hiWas = SoldierNPC.GearApprox.HarnessInterval;
			try
			{
				SoldierNPC.DormancyOn = false; // every levy runs the frame as a fighting one would
				void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); ClearHostiles(true); WorldStep.Update(); } }

				// ---- interval 1: the exact frame
				SoldierNPC.GearApprox.HarnessInterval = 1; SoldierNPC.GearApprox.ResetCounters();
				Step(30);
				Check($"interval 1 is the exact frame: no tick served ({SoldierNPC.GearApprox.Served} served)", SoldierNPC.GearApprox.Served == 0);

				// ---- interval 3, every served tick compared with a full build from the same state
				SoldierNPC.GearApprox.HarnessInterval = 3; SoldierNPC.GearApprox.ResetCounters(); SoldierNPC.GearApproxProbe.Reset(); SoldierNPC.GearApprox.Probe = true;
				long f0 = GearUpkeep.FrameCalls, t0 = GearUpkeep.TailRuns;
				Step(180);
				SoldierNPC.GearApprox.Probe = probeWas;
				long frames = GearUpkeep.FrameCalls - f0, tails = GearUpkeep.TailRuns - t0, served = SoldierNPC.GearApprox.Served;
				int statDiffs = 0; var which = new List<string>();
				for (int i = 0; i < SoldierNPC.GearApproxProbe.StatNames.Length; i++) if (SoldierNPC.GearApproxProbe.Diff[i] > 0) { statDiffs += (int)SoldierNPC.GearApproxProbe.Diff[i]; which.Add(SoldierNPC.GearApproxProbe.StatNames[i] + " " + SoldierNPC.GearApproxProbe.Diff[i]); }
				Note($"approximate gear frame, interval 3, {sample.Count} levies standing, 180 ticks: {served} frames served, {SoldierNPC.GearApprox.Builds} built ({string.Join(", ", SoldierNPC.GearApprox.Why.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value))}); leaves that differed from a full build: "
					+ (SoldierNPC.GearApproxProbe.Leaves.Count == 0 ? "none" : string.Join(", ", SoldierNPC.GearApproxProbe.Leaves.OrderByDescending(kv => kv.Value).Take(12).Select(kv => kv.Key + " " + kv.Value))));
				Check($"interval 3 serves ticks between builds ({served} of {frames} frames)", served > frames / 4);
				Check($"every served tick has the stats a full build gives from the same state ({SoldierNPC.GearApproxProbe.Checks} compared, {statDiffs} stat differences{(which.Count > 0 ? ": " + string.Join(", ", which) : "")})", SoldierNPC.GearApproxProbe.Checks == served && served > 0 && statDiffs == 0);
				Check($"no cast is missed on a served tick ({SoldierNPC.GearApproxProbe.MissedSpawns} missed)", SoldierNPC.GearApproxProbe.MissedSpawns == 0);
				Check($"the tail (Rage, Adrenaline, stealth, cooldowns) runs on every gear frame, served or built ({tails} tails, {frames} frames)", frames > 0 && tails == frames);

				// ---- a change of input rebuilds on the next tick
				var s = sample.FirstOrDefault(x => x.GearApproxReady) ?? sample[0];
				Step(3);
				// gear: an accessory taken off
				int slot = Array.FindIndex(s.Record.Gear.Accessories, it => it != null && !it.IsAir);
				if (slot >= 0)
				{
					var acc = s.Record.Gear.Accessories[slot];
					s.Record.Gear.Accessories[slot] = new Item(); s.NotifyGearChanged();
					Step(1);
					int kindGear = s.LastFrameKind;
					s.Record.Gear.Accessories[slot] = acc; s.NotifyGearChanged();
					Step(1);
					Check($"a change of gear rebuilds on the next tick (frame kind {kindGear}: 3 would be a served tick)", kindGear != 3);
				}
				// a buff on the body: Ironskin's defense is in the stats on the next tick
				Step(4);
				int busy = 0; while (busy++ < 6 && (int)((Main.GameUpdateCount + 1 + (uint)s.NPC.whoAmI) % 3) == 0) Step(1); // the next tick is not a due build
				int def0 = (int)s.Dummy.statDefense;
				s.NPC.AddBuff(BuffID.Ironskin, 600);
				Step(1);
				int kindBuff = s.LastFrameKind, def1 = (int)s.Dummy.statDefense;
				Check($"a buff on the body rebuilds on the next tick and its stat is there at once (frame kind {kindBuff}; defense {def0} -> {def1}, Ironskin +{LevyConstants.IronskinDefense})", kindBuff != 3 && def1 == def0 + LevyConstants.IronskinDefense);
				s.NPC.DelBuff(s.NPC.FindBuffIndex(BuffID.Ironskin));
				Step(2);
			}
			finally { SoldierNPC.DormancyOn = dormWas; SoldierNPC.GearApprox.Probe = probeWas; SoldierNPC.GearApprox.HarnessInterval = hiWas; }

			if (ModLoader.HasMod("CalamityMod")) CalamityPatchChecks(sample);
		}

		/// <summary>One frame run twice from the same state (the dummy, its ModPlayers, its items, the random generator): patches bypassed, then in force.</summary>
		private static void CalamityPatchChecks(List<SoldierNPC> sample)
		{
			Check($"the Calamity patches are installed ({string.Join(", ", CalamityLevyPatches.Installed)})", CalamityLevyPatches.Installed.Contains("AndroombaRightClick") && CalamityLevyPatches.Installed.Contains("AbyssEffects") && CalamityLevyPatches.Installed.Contains("ResetEffects.Any") && CalamityLevyPatches.Installed.Contains("UpdateDrawingParameters"));
			bool dormWas = SoldierNPC.DormancyOn; int hiWas = SoldierNPC.GearApprox.HarnessInterval;
			int same = 0, differ = 0; var leaves = new Dictionary<string, int>();
			long and0 = CalamityLevyPatches.AndroombaSkips, light0 = CalamityLevyPatches.LightSkips, any0 = CalamityLevyPatches.AnyServed;
			var lighting = new HashSet<string> { "CalamityPlayer.abyssDarkness", "CalamityPlayer.abyssPlayerGlowMultiplier", "CalamityPlayer.abyssFlashlightWidthMultiplier", "CalamityPlayer.darknessIntensity" };
			try
			{
				SoldierNPC.DormancyOn = false; SoldierNPC.GearApprox.HarnessInterval = 1; CalamityLevyPatches.Verify = true; CalamityLevyPatches.AnyMismatch = 0;
				foreach (var s in sample.Take(12))
				{
					Player d = s.Dummy; if (d == null) continue;
					var roots = new List<object> { d }; var names = new List<string> { "P" };
					foreach (var mp in d.ModPlayers) { roots.Add(mp); names.Add(mp.GetType().Name); }
					int ii = 0; foreach (var it in s.Record.Gear.AllItems()) if (it != null && !it.IsAir) { roots.Add(it); names.Add("I" + ii++); }
					roots.Add(Main.rand); names.Add("RNG");
					var img0 = new StateImage(); img0.Capture(roots, names);
					int life0 = s.NPC.life;
					CalamityLevyPatches.Bypass = true; s.TestGearFrame();
					var a = new StateImage(); a.Capture(roots, names);
					img0.Restore(); s.NPC.life = life0;
					CalamityLevyPatches.Bypass = false; s.TestGearFrame();
					var b = new StateImage(); b.Capture(roots, names);
					var diffs = new List<string>(); a.DiffInto(b, diffs, 64);
					diffs.RemoveAll(x => lighting.Contains(x));
					if (diffs.Count == 0) same++; else { differ++; foreach (var x in diffs) leaves[x] = leaves.TryGetValue(x, out int c) ? c + 1 : 1; }
				}
				Step2(60); // the army as it stands, the patches in force, every projectile question checked against the original
			}
			finally { CalamityLevyPatches.Bypass = false; CalamityLevyPatches.Verify = false; SoldierNPC.DormancyOn = dormWas; SoldierNPC.GearApprox.HarnessInterval = hiWas; }
			Check($"a gear frame leaves the same dummy with and without the Calamity patches ({same} levies the same, {differ} differ{(leaves.Count > 0 ? ": " + string.Join(", ", leaves.Take(10).Select(kv => kv.Key + " " + kv.Value)) : "")})", differ == 0 && same > 0);
			Check($"the skipped calls were reached for dummies (Androomba {CalamityLevyPatches.AndroombaSkips - and0}, abyss light levels {CalamityLevyPatches.LightSkips - light0})", CalamityLevyPatches.AndroombaSkips > and0 && CalamityLevyPatches.LightSkips > light0);
			Check($"the patched projectile question gives the original's answer every time ({CalamityLevyPatches.AnyServed - any0} answered from the blade list, {CalamityLevyPatches.AnyMismatch} differed)", CalamityLevyPatches.AnyServed > any0 && CalamityLevyPatches.AnyMismatch == 0);

			static void Step2(int n) { for (int i = 0; i < n; i++) { KeepCommander(); ClearHostiles(true); WorldStep.Update(); } }
		}
	}
}
