using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Globals;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS AI LENS for the Exo lab (lab "bossai", 2026-11-23). Measurement only. Per Exo fight it writes <c>out.tsv.bossai.tsv</c>: one row of key=value metrics about how the mechs fight the army:
	/// how often each mech's levy lock changes, whether the mechs share a target, how far each mech is from the levy it hunts, Thanatos under the floor, Ares enraged, what Ares's arms read in Main.player[Ares's target]
	/// (the empty slot, the commander or a levy, and how far that point is from the nearest levy), every hostile projectile the mechs make by type (made, and how many touched at least one levy), and what landed the
	/// last hit on each levy that died (a projectile within 20 ticks of the death, else the attacking NPC). The army-aware feature's own counters (ArmyAwareExo) are copied in.
	/// </summary>
	public partial class CombatHarness
	{
		internal sealed class BaState
		{
			public int[] Lock = { -2, -2, -2, -2 }; public long[] Switch = new long[4], LockSamples = new long[4]; // Ares, Thanatos, Artemis, Apollo
			public long Shared, SharedSamples;
			public double[] TgtDist = new double[4]; public long[] TgtDistN = new long[4]; public double[] Near40 = new double[4]; public long[] Near40N = new long[4];
			public long ThanSamples, ThanUnder; public long AresSamples, AresEnraged;
			public long ArmEmpty, ArmCmd, ArmLevy, ArmOther; public double ArmMissPx; public long ArmMissN;
			public Dictionary<string, long[]> Proj = new(); // name -> made, touched a levy, levy hits
			public Dictionary<int, (string name, bool hit)> Live = new();
			public Dictionary<int, (string name, int tick)> LastProjHit = new();
			public Dictionary<string, int> KilledBy = new(); public HashSet<int> Counted = new();
			public Dictionary<int, int> DeathsPer2s = new();
		}
		private static BaState _ba;

		private static int[] _baTypes;
		private static int BaSlot(int type)
		{
			ExTypes();
			_baTypes ??= new[] { _exAres, _exThan, _exTwinA, _exTwinB };
			for (int k = 0; k < 4; k++) if (_baTypes[k] == type) return k;
			return -1;
		}

		internal static void BaBegin() { _ba = new BaState(); ArmyAwareExo.ResetCounters(); ArmyAwareExo.ArmProbe = BaArmProbe; }
		internal static void BaEnd() { ArmyAwareExo.ArmProbe = null; _ba = null; }

		private static void BaArmProbe(NPC arm, Player seen)
		{
			var b = _ba; if (b == null || Main.GameUpdateCount % 15 != 0) return;
			if (seen == null || !seen.active) b.ArmEmpty++;
			else if (ReferenceEquals(seen, Main.player[0])) b.ArmCmd++;
			else if (IsLevyDummy(seen)) b.ArmLevy++;
			else b.ArmOther++;
			if (seen == null) return;
			float best = float.MaxValue; Vector2 at = seen.Center;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.IsAlive) best = Math.Min(best, Vector2.Distance(n.Center, at)); }
			if (best < float.MaxValue) { b.ArmMissPx += best; b.ArmMissN++; }
		}

		private static bool IsLevyDummy(Player p)
		{
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC s && ReferenceEquals(s.Dummy, p)) return true;
			return false;
		}

		/// <summary>Every tick of an Exo fight: lock changes of the four mech NPCs.</summary>
		private static void BaTick(CrState st)
		{
			var b = _ba; if (b == null) return;
			int[] lk = { -2, -2, -2, -2 }; bool[] open = new bool[4];
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active) continue;
				int k = BaSlot(n.type); if (k < 0) continue;
				lk[k] = LevyNpcData.Of(n).SoldierTarget; open[k] = !n.dontTakeDamage;
			}
			int sharedLock = int.MinValue; bool shared = true; int nOpen = 0;
			for (int k = 0; k < 4; k++)
			{
				if (lk[k] == -2) { b.Lock[k] = -2; continue; }
				b.LockSamples[k]++;
				if (b.Lock[k] >= 0 && lk[k] >= 0 && lk[k] != b.Lock[k]) b.Switch[k]++;
				b.Lock[k] = lk[k];
				if (!open[k]) continue;
				nOpen++;
				if (sharedLock == int.MinValue) sharedLock = lk[k]; else if (lk[k] != sharedLock) shared = false;
			}
			if (nOpen >= 2) { b.SharedSamples++; if (shared && sharedLock >= 0) b.Shared++; }
		}

		/// <summary>Every 15 ticks: distances, Thanatos's depth, Ares's enrage, and the levies that died since the last sample.</summary>
		private void BaSample(CrState st)
		{
			var b = _ba; if (b == null) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active) continue;
				int k = BaSlot(n.type); if (k < 0 || n.dontTakeDamage) continue;
				int t = LevyNpcData.Of(n).SoldierTarget;
				if (t >= 0 && t < Main.maxNPCs && Main.npc[t].active && Main.npc[t].ModNPC is SoldierNPC) { b.TgtDist[k] += Vector2.Distance(n.Center, Main.npc[t].Center) / 16f; b.TgtDistN[k]++; }
				int near = 0; foreach (var l in st.Levies) { if (l.Dead) continue; NPC ln = Main.npc[l.Idx]; if (ln.active && ln.ModNPC is SoldierNPC && Vector2.DistanceSquared(ln.Center, n.Center) <= 640f * 640f) near++; }
				b.Near40[k] += near; b.Near40N[k]++;
				if (k == 1) { b.ThanSamples++; if (n.Center.Y > (CrF + 3) * 16f) b.ThanUnder++; }
				if (k == 0) { b.AresSamples++; if (n.localAI[1] == 1f) b.AresEnraged++; }
			}
			foreach (var l in st.Levies)
			{
				if (!l.Dead || l.DeathTick < 0 || !b.Counted.Add(l.Id)) continue;
				string by;
				if (b.LastProjHit.TryGetValue(l.Idx, out var lp) && l.DeathTick - lp.tick <= 20) by = lp.name;
				else by = l.LastAttackerType > 0 ? CrNpcName(l.LastAttackerType) : "none";
				b.KilledBy.TryGetValue(by, out int v); b.KilledBy[by] = v + 1;
				int w = l.DeathTick / 120; b.DeathsPer2s.TryGetValue(w, out int dv); b.DeathsPer2s[w] = dv + 1;
			}
		}

		private static string BaProjName(Projectile p) => p.ModProjectile != null ? p.ModProjectile.Name : Lang.GetProjectileName(p.type).Value;

		public class BaProjWatch : GlobalProjectile
		{
			public override void OnSpawn(Projectile projectile, IEntitySource source)
			{
				var b = _ba; if (b == null || !projectile.hostile) return;
				bool exo = false;
				if (source is EntitySource_Parent { Entity: NPC n } && ExPoolOf(n.type) is >= 0 and < 3) exo = true;
				else if (source is EntitySource_Parent { Entity: Projectile pp } && b.Live.ContainsKey(pp.whoAmI)) exo = true;
				if (!exo) return;
				string name = BaProjName(projectile);
				if (!b.Proj.TryGetValue(name, out var a)) b.Proj[name] = a = new long[3];
				a[0]++;
				if (b.Live.TryGetValue(projectile.whoAmI, out var old)) BaFinal(b, old);
				b.Live[projectile.whoAmI] = (name, false);
			}
			public override void OnKill(Projectile projectile, int timeLeft)
			{
				var b = _ba; if (b == null) return;
				if (b.Live.TryGetValue(projectile.whoAmI, out var e)) { BaFinal(b, e); b.Live.Remove(projectile.whoAmI); }
			}
		}
		private static void BaFinal(BaState b, (string name, bool hit) e) { if (e.hit && b.Proj.TryGetValue(e.name, out var a)) a[1]++; }

		public class BaHitWatch : GlobalNPC
		{
			public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
			{
				var b = _ba; if (b == null || !projectile.hostile || npc.ModNPC is not SoldierNPC) return;
				string name = BaProjName(projectile);
				b.LastProjHit[npc.whoAmI] = (name, _cr != null ? _cr.Tick : 0);
				if (b.Live.TryGetValue(projectile.whoAmI, out var e)) { if (!e.hit) b.Live[projectile.whoAmI] = (e.name, true); if (b.Proj.TryGetValue(e.name, out var a)) a[2]++; }
			}
		}

		private void BaFinish(CrRun run, CrState st, int end)
		{
			var b = _ba; if (b == null) return;
			try
			{
				foreach (var e in b.Live.Values) BaFinal(b, e);
				b.Live.Clear();
				var kv = new List<string>();
				string[] nm = { "ares", "than", "artemis", "apollo" };
				Func<double, string> f = v => v.ToString("0.##", CultureInfo.InvariantCulture);
				for (int k = 0; k < 4; k++)
				{
					if (b.LockSamples[k] == 0) continue;
					kv.Add($"{nm[k]}.switchPerMin={f(b.Switch[k] / (b.LockSamples[k] / 3600.0))}");
					if (b.TgtDistN[k] > 0) kv.Add($"{nm[k]}.tgtDistTiles={f(b.TgtDist[k] / b.TgtDistN[k])}");
					if (b.Near40N[k] > 0) kv.Add($"{nm[k]}.levies40={f(b.Near40[k] / b.Near40N[k])}");
				}
				kv.Add($"sharedTarget%={(b.SharedSamples > 0 ? f(100.0 * b.Shared / b.SharedSamples) : "-")}");
				kv.Add($"thanUnder%={(b.ThanSamples > 0 ? f(100.0 * b.ThanUnder / b.ThanSamples) : "-")}");
				kv.Add($"aresEnraged%={(b.AresSamples > 0 ? f(100.0 * b.AresEnraged / b.AresSamples) : "-")}");
				long arm = b.ArmEmpty + b.ArmCmd + b.ArmLevy + b.ArmOther;
				kv.Add($"armReads(empty/cmd/levy/other)={b.ArmEmpty}/{b.ArmCmd}/{b.ArmLevy}/{b.ArmOther}");
				kv.Add($"armAimFromNearestLevyTiles={(b.ArmMissN > 0 ? f(b.ArmMissPx / b.ArmMissN / 16.0) : "-")}");
				kv.Add($"feature(armCalls/armLent/armEmpty/ventCalls/ventPlaced/homingCalls/homingPlaced)={ArmyAwareExo.ArmCalls}/{ArmyAwareExo.ArmLent}/{ArmyAwareExo.ArmReadEmpty}/{ArmyAwareExo.VentCalls}/{ArmyAwareExo.VentPlaced}/{ArmyAwareExo.HomingCalls}/{ArmyAwareExo.HomingPlaced}");
				foreach (var p in b.Proj.OrderByDescending(x => x.Value[0])) kv.Add($"proj.{p.Key}={p.Value[0]}/{p.Value[1]}/{p.Value[2]}");
				foreach (var d in b.KilledBy.OrderByDescending(x => x.Value)) kv.Add($"killedBy.{d.Key.Replace(' ', '_')}={d.Value}");
				kv.Add($"worst2s={(b.DeathsPer2s.Count > 0 ? b.DeathsPer2s.Values.Max() : 0)}");
				string path = _crOut + ".bossai.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tboss\tseed\tresult\tsec\tdeaths\tn\tmetrics\n");
				File.AppendAllText(path, $"{run.Key}\t{run.Boss}\t{run.Seed}\t{st.Result}\t{f(end / 60.0)}\t{st.Levies.Count(l => l.Dead)}\t{st.Levies.Count}\t{string.Join(" ", kv)}\n");
			}
			catch (Exception ex) { Mod.Logger.Warn("HARNESS-BOSSAI: finish failed: " + ex); }
		}
	}
}
