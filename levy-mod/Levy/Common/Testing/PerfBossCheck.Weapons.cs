using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Levy.Common.Globals;
using Levy.Common.Perf;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;
using PA = Levy.Common.Perf.PerfAnalyzer;

namespace Levy.Common.Testing
{
	public partial class PerfBossCheck
	{
		/// <summary>
		/// PER-WEAPON COST in the always-on analyzer, on the engaged army (the four kits: Megashark, Crystal Storm, Night's Edge, Optic Staff) with Plantera at its edge: one window over the arm. PASS: every kit weapon has a row with about
		/// a quarter of the levies; the weapons together come to the army's own rows (levy bodies and army shots, from the analyzer's per-type and army rows) plus at most the other mods' hooks on them and the owner swaps; the window
		/// survives the log's round trip; a laggy fight made of it names the heaviest weapons in the popup's words; and the attribution's own code, timed call by call, costs under 0.1 ms per tick at 45 levies and 300 army shots.
		/// </summary>
		private static void WeaponAttribution(int ticks)
		{
			string tmp = Path.Combine(Path.GetTempPath(), "levy_perfwpn_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			PA.TestServer = true; PA.TestLogDir = tmp; PA.TestWindowSecs = 1e9;
			var order = _order;
			PerfWindow w = null; double buildMs = 0; double[] mb = null; int levies = 0, shots = 0;
			try
			{
				PA.TestLog = true; PA.ConfigChanged();
				_order = SoldierOrder.FreeRein; foreach (var s in Army) if (s.NPC.active && s.Record != null) s.Record.Order = _order;
				PA.TestReset();
				Arm a = RunArm("weapon attribution, army engaged with Plantera", ticks, 60, 0, 0, false, 2);
				levies = a.Levies;
				buildMs = double.MaxValue; for (int rep = 0; rep < 3; rep++) buildMs = Math.Min(buildMs, PA.TestBuildWeaponsMs());
				w = PA.TestClose();
				// the attribution's own code, call by call, on a real levy and one of its shots (cheapest of five runs: a preempted run only adds)
				SoldierNPC lv = Army.FirstOrDefault(s => s.NPC.active);
				Projectile shot = null;
				if (lv != null)
				{
					int k = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_WorldEvent("PerfWpn"), lv.NPC.Center, Microsoft.Xna.Framework.Vector2.Zero, ProjectileID.Bullet, 1, 0f, 0);
					if (k >= 0 && k < Main.maxProjectiles) { shot = Main.projectile[k]; shot.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex = lv.NPC.whoAmI; }
				}
				if (lv != null && shot != null)
				{
					for (int rep = 0; rep < 5; rep++) { var one = PA.MicroBenchWeapons(lv.NPC, shot); if (mb == null) mb = one; else for (int q = 0; q < mb.Length; q++) mb[q] = Math.Min(mb[q], one[q]); }
					shot.active = false;
				}
			}
			finally
			{
				_order = order; foreach (var s in Army) if (s.NPC.active && s.Record != null) s.Record.Order = _order;
				PA.TestLog = false; PA.ConfigChanged(); PA.TestServer = false; PA.TestWindowSecs = null; PA.TestLogDir = null;
				PA.TestResetLog(); PA.TestReset(); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception) { }
			}
			if (w == null) { Check("weapon attribution: a window closed", false); return; }
			Note("weapon attribution window: " + w.ToJson());
			string[] kit = { "Megashark", "Crystal Storm", "Night's Edge", "Optic Staff" };
			double sumLevies = w.Weapons.Sum(r => r.Levies);
			var missing = kit.Where(k => !w.Weapons.Any(r => r.Name == k && r.Levies >= levies / 4.0 * 0.7 && r.Levies <= levies / 4.0 * 1.3)).ToList();
			Check($"weapon attribution: every kit weapon has a row with about a quarter of the {levies} levies ({string.Join(", ", w.Weapons.Select(r => $"{r.Name} {r.Levies:F1} levies {r.BodyMs:F3}+{r.ShotMs:F3} ms/tick {r.Shots:F0} shots, top {r.TopProj}"))}){(missing.Count > 0 ? " MISSING: " + string.Join(", ", missing) : "")}", missing.Count == 0 && Math.Abs(sumLevies - levies) <= levies * 0.1);
			// the independent rows: the analyzer's per-type row of the levy NPC and its army-shot row (other mods' hooks taken out of both)
			double upd = Math.Max(1, w.Updates);
			double soldierRow = w.Top.Where(it => it.Cat == PerfCat.NpcType && it.Name.EndsWith("/SoldierNPC")).Sum(it => it.Ms) / upd;
			double armyRow = w.Top.Where(it => it.Cat == PerfCat.Army).Sum(it => it.Ms) / upd;
			double[] rf = w.ArmyRefMs ?? new double[4];
			double low = soldierRow + armyRow, high = low + rf[2] + rf[3];
			Check($"weapon attribution: the weapons together ({w.WeaponAttribMs:F3} ms/tick) come to the army's own rows (levy bodies {soldierRow:F3} + army shots {armyRow:F3} = {low:F3}) plus at most the other mods' hooks on them ({rf[2]:F3}) and the owner swaps ({rf[3]:F3}) = {high:F3}; NPC + projectile phases {w.UpdSplit[0] + w.UpdSplit[1]:F3}", low > 0 && w.WeaponAttribMs >= low * 0.97 && w.WeaponAttribMs <= high * 1.03);
			// the log's round trip and the popup's words
			var back = PerfWindow.FromJson(w.ToJson());
			Check($"weapon attribution: the window's weapon rows survive the log ({back?.Weapons.Count ?? -1} of {w.Weapons.Count} rows, {back?.WeaponAttribMs:F3} ms/tick)", back != null && back.Weapons.Count == w.Weapons.Count && back.Weapons.Select(r => r.Name).SequenceEqual(w.Weapons.Select(r => r.Name)) && Math.Abs(back.WeaponAttribMs - w.WeaponAttribMs) < 0.002);
			var fight = new List<PerfWindow>();
			for (int i = 0; i < 4; i++) { var f = PerfWindow.FromJson(w.ToJson()); f.Secs = 8; f.Updates = 464; f.Frames = 32; f.Fps = 4; f.Ups = 58; f.Boss = "Plantera"; f.Focus = true; f.FrameSkip = 1; fight.Add(f); }
			LagVerdict v = PerfAnalysis.Judge(fight);
			string line = v.Detail.FirstOrDefault(d => d.StartsWith("Heaviest levy weapons this fight: ")) ?? "";
			Note("weapon attribution, the popup's words for a laggy fight made of this window: " + string.Join(" | ", v.Detail));
			Check($"weapon attribution: a laggy fight's popup names the heaviest levy weapons with their share of the army (\"{line}\")", line.Length > 0 && kit.Any(k => line.Contains(k + " (")) && line.Contains("% of the army"));
			// the cost of the attribution itself
			if (mb == null) { Check("weapon attribution: the call-by-call bench ran", false); return; }
			for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) shots++;
			double npcNs = Math.Max(0, mb[2] - mb[0]), projNs = Math.Max(0, mb[3] - mb[1]), swapNs = mb[4] + 2 * mb[5];
			double Per(int nl, int ns) => ((nl * npcNs + ns * (projNs + swapNs)) / 1e6) / PA.SampleEvery + buildMs / 480.0;
			double at45 = Per(45, 300), at150 = Per(150, 1000);
			Note($"weapon attribution own code, ns per sampled call: levy body {mb[0]:F1} -> {mb[2]:F1}, army shot {mb[1]:F1} -> {mb[3]:F1}, swap timing {swapNs:F1} (clock read {mb[5]:F1}); window rows built in {buildMs * 1000:F1} us once a window; per tick averaged: {at45 * 1000:F2} us at 45 levies and 300 shots, {at150 * 1000:F2} us at 150 levies and 1,000 shots");
			Check($"weapon attribution costs under 0.1 ms per tick at 45 levies and 300 army shots ({at45 * 1000:F2} us)", at45 < 0.1);
		}
	}
}
