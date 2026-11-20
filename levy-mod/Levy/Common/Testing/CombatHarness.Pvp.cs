using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PVP LAB (measurement only; levy-mod/lab/pvp). A calrev line `fight pvp MODE SEED CAP lv=A.json lvb=B.json ta=NAME tb=NAME order=sd arena=a3` puts two lab-kit armies in the arena, army A on the left
	/// (team 1, spawned first) and army B on the right (team 2), and lets them fight until one is wiped or the cap runs out. Both armies are the stand-in commander's levies: the same brain, gear rules,
	/// stations, buffs, potions (the harness's drink rule), healing and damage intake. <see cref="PvpLab"/> makes each side the other's enemy. The commander stands at the arena centre, invulnerable
	/// under the Spectator buff. No boss. Every hit one team lands on the other is credited to the levy that fired it (projectiles, children, minions, swings). With LEVY_REC_DIR set the fight is
	/// recorded (both armies are levies, so names and gear are in the file; names carry the army label). Rows: LEVY_CR_OUT (one calrev row, result A / B / DRAW) and LEVY_CR_OUT.pvp.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class PvState
		{
			public CrRun Run; public CrState St; public List<CrLevy> A = new(), B = new();
			public Dictionary<int, CrLevy> ByIdx = new(); public Dictionary<CrLevy, int> TeamOfLevy = new();
			public Dictionary<CrLevy, long> Dealt = new(); public long[] Unattr = new long[3], Dot = new long[3]; public Dictionary<string, long> Kind = new();
			public int Tick, EndTick = -1; public string Result = ""; public bool Recording;
		}
		private static PvState _pvS;
		private static Projectile _pvProj;

		private void StepPvp(CrRun run)
		{
			if (!_crStarted)
			{
				ClearAll(); CrPurgeNpcs(); PvpLab.Reset();
				CrApplyMode(run.Mode); CrDrGlobal.Dr = run.Dr; _crRunCur = run;
				Main.rand = new Terraria.Utilities.UnifiedRandom(run.Seed * 7919 + 13);
				_crCmdPos = new Vector2(_crCx * 16f, CrF * 16f); // the commander stands at the arena centre, between the armies
				CrPinCommander();
				// the whole arena counts as watched (a player would be looking at her army): no levy takes the off-screen hop across it
				global::Levy.Common.Movement.OffscreenHop.LabViewers = new List<Vector2> { new Vector2((_crCx - 75) * 16f, (CrF - 10) * 16f), new Vector2(_crCx * 16f, (CrF - 10) * 16f), new Vector2((_crCx + 75) * 16f, (CrF - 10) * 16f) };
				CrSetHpScale(run); CrSetFear(run); CrSetOptions(run);
				if (run.L3) Lab3Stations(run);
				if (run.Dl != "") DlBegin(run);
				var specsB = KitSpecs(run.LvB);
				int total = run.Specs.Count + specsB.Count;
				if (total > global::Levy.Common.Config.LevyConfig.Get.RenderCap) global::Levy.Common.Config.LevyConfig.Get.RenderCap = total;
				var s = new PvState { Run = run, St = new CrState { Run = run, BossIdx = Main.maxNPCs, BossType = -1 } };
				PvpLab.On = true;
				PvSpawn(s, run, run.Specs, 1, run.TeamA, s.A);
				PvSpawn(s, run, specsB, 2, run.TeamB, s.B);
				s.St.Levies.AddRange(s.A); s.St.Levies.AddRange(s.B);
				if (run.L3) Lab3Begin(run, s.St);
				_pvS = s; _crStarted = true; _crTickInRun = 0;
				PvStartRecording(s, run);
				Mod.Logger.Info($"HARNESS-PVP: start {run.Key}: {run.TeamA} x{s.A.Count} (left, team 1) vs {run.TeamB} x{s.B.Count} (right, team 2), mode {run.Mode}, order {run.Order}, arena {run.Arena}, cap {run.Cap / 60}s, recording={s.Recording}");
				return;
			}
			var st = _pvS; st.Tick = ++_crTickInRun; st.St.Tick = st.Tick;
			CrPinCommander();
			// the commander's own facing still reaches the lashes outside the per-levy scopes (see PvFaceOwner): it turns every tick, so each army has it half the time. LEVY_PVP_CMDDIR=1|-1 pins it (lab probe).
			Main.player[0].direction = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PVP_CMDDIR"), out int cdir) && cdir != 0 ? cdir : (st.Tick & 1) == 0 ? 1 : -1;
			CrPinStrays(-1);
			int aliveA = PvTickArmy(st, run, st.A), aliveB = PvTickArmy(st, run, st.B);
			if (st.Result == "")
			{
				if (aliveA == 0 || aliveB == 0) { st.Result = aliveA > 0 ? "A" : aliveB > 0 ? "B" : "DRAW"; st.EndTick = st.Tick; }
				else if (st.Tick >= run.Cap)
				{
					// cap: the army with more levies standing wins; a tie goes to the larger share of life left
					double hpA = PvHpShare(st.A), hpB = PvHpShare(st.B);
					st.Result = aliveA != aliveB ? (aliveA > aliveB ? "A" : "B") : Math.Abs(hpA - hpB) < 1e-6 ? "DRAW" : hpA > hpB ? "A" : "B";
					st.Result += "-TIMEOUT"; st.EndTick = st.Tick;
				}
			}
			if (st.Result != "" && st.Tick >= st.EndTick + 30) PvFinish(run, st); // a second of recording past the last death
		}

		private void PvSpawn(PvState s, CrRun run, List<CrLevySpec> specs, int team, string label, List<CrLevy> into)
		{
			int side = (team == 1 ? -1 : 1) * (Environment.GetEnvironmentVariable("LEVY_PVP_FLIP") == "1" ? -1 : 1); // LEVY_PVP_FLIP=1: army A (team 1, spawned first) on the right
			for (int k = 0; k < specs.Count; k++)
			{
				var l = CrSpawnLevy(specs[k], run, k, specs.Count, new Vector2((_crCx + side * 70) * 16f, CrF * 16f));
				l.Rec.Name = label + " " + specs[k].Cls + (k + 1);
				NPC n = Main.npc[l.Idx];
				n.Bottom = new Vector2((_crCx + side * (35f + 25f * k / Math.Max(1, specs.Count - 1))) * 16f, CrF * 16f); n.velocity = Vector2.Zero; // 35-60 tiles out from the centre (inside Search & Destroy's 60-tile catch-up), the two fronts 70 tiles apart
				n.direction = -side;
				PvpLab.Add(l.Idx, team);
				s.ByIdx[l.Idx] = l; s.TeamOfLevy[l] = team; s.Dealt[l] = 0; into.Add(l);
			}
		}

		private static double PvHpShare(List<CrLevy> army)
		{
			double left = 0, max = 0;
			foreach (var l in army) { max += l.MaxHp; if (!l.Dead) { NPC n = Main.npc[l.Idx]; if (n.active) left += Math.Max(0, n.life); } }
			return max <= 0 ? 0 : left / max;
		}

		/// <summary>One tick of one army: the harness drink rule, the stations / buffs tick, life lost, deaths. Returns the levies standing.</summary>
		private int PvTickArmy(PvState st, CrRun run, List<CrLevy> army)
		{
			int alive = 0;
			foreach (var l in army)
			{
				if (l.Dead) continue;
				NPC n = Main.npc[l.Idx];
				bool body = l.Rec.HasBody && n.active && n.ModNPC is SoldierNPC sn0 && ReferenceEquals(sn0.Record, l.Rec) && n.life > 0;
				if (body)
				{
					var sn = (SoldierNPC)n.ModNPC;
					if (!run.L3) sn.Stations = StationFlags.Campfire | StationFlags.HeartLantern; else Lab3Tick(run, st.St, l, sn, n);
					if (l.PotionsLeft > 0 && sn.PotionSickness == 0 && n.life * 100 < n.lifeMax * LevyConstants.PotionDrinkBelowPercent)
					{
						Item pot = CrMake(l.Spec.Potion);
						n.life = Math.Min(n.lifeMax, n.life + pot.healLife); l.Rec.CurrentHp = n.life; sn.PotionSickness = LevyConstants.PotionSicknessTicks; l.PotionsLeft--; l.PotionsDrunk++;
						l.LastLife = n.life;
					}
					int drop = l.LastLife - n.life;
					if (drop > 0) { l.Hits++; l.TakenOther += drop; }
					l.BodyTicks++; l.DefSum += n.defense; if (sn.State == SoldierState.Shaken) l.ShakenTicks++; else if (sn.State == SoldierState.Routed) l.RoutedTicks++;
					l.LastLife = n.life; l.LifeMin = Math.Min(l.LifeMin, n.life);
					alive++;
				}
				else
				{
					l.Dead = true; l.DeathTick = st.Tick; l.TakenOther += Math.Max(0, l.LastLife);
					l.Rec.RespawnTimer = 1_000_000_000; // no respawn inside the lab
					PvpLab.Remove(l.Idx); st.ByIdx.Remove(l.Idx);
				}
			}
			return alive;
		}

		/// <summary>
		/// The owner's facing. Every levy shot is owned by the commander's player slot, and the commander's facing changes what a levy's shots and lashes do: with her facing right the army on the right
		/// (shooting left) out-damaged the army on the left 2-3 to 1 in identical mirrors and won 10 of 12; facing left, the left army won 6 of 6. So for the length of each levy's update and each of its
		/// shots' updates the commander faces the way that levy faces, as a player faces the way she shoots. LEVY_PVP_OWNERDIR=0 leaves her facing alone (the biased lab).
		/// </summary>
		internal static readonly bool PvOwnerDir = Environment.GetEnvironmentVariable("LEVY_PVP_OWNERDIR") != "0";
		private static int PvFaceOwner(NPC shooter)
		{
			if (!PvOwnerDir || shooter == null || !(shooter.ModNPC is SoldierNPC)) return 0;
			Player o = Main.player[SoldierMinions.TrueMyPlayer >= 0 && SoldierMinions.TrueMyPlayer < Main.maxPlayers ? SoldierMinions.TrueMyPlayer : 0];
			int prev = o.direction; o.direction = shooter.direction == 0 ? 1 : shooter.direction;
			return prev == 0 ? 2 : prev; // 0 = nothing to put back
		}
		private static void PvFaceRestore(int prev)
		{
			if (prev == 0) return;
			Player o = Main.player[SoldierMinions.TrueMyPlayer >= 0 && SoldierMinions.TrueMyPlayer < Main.maxPlayers ? SoldierMinions.TrueMyPlayer : 0];
			o.direction = prev == 2 ? 0 : prev;
		}

		/// <summary>LEVY_PVP_IFRAMES=0: the other army's hits land as on any NPC (vanilla per-shot immunity only). On (default): a levy hit by the other army gets the levy's own player-style hit window, as from an enemy's hit (SoldierNPC.AfterHit: immune ticks, fear, barks, hurt-driven gear).</summary>
		internal static readonly bool PvIFrames = Environment.GetEnvironmentVariable("LEVY_PVP_IFRAMES") != "0";
		private static System.Reflection.MethodInfo _pvAfterHit;

		/// <summary>The strike detour for a hit on a team body: the player-style hit window, the strike, the credit, the levy's own after-hit bookkeeping.</summary>
		private static int PvStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, SoldierNPC victim, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			int src = _crProjDepth > 0 ? PvpLab.ProjSoldier(_pvProj) : _crNpcCtx;
			int vt = PvpLab.TeamOf(self.whoAmI), at = PvpLab.TeamOf(src);
			bool enemy = at != 0 && at != vt;
			if (enemy && PvIFrames && victim.ImmuneTime > 0) return 0; // inside the window a hit from the other army does nothing (a projectile is stopped earlier, in PvpImmunity)
			int done = orig(self, hit, fromNet, noPlayerInteraction);
			PvCredit(self, done, src);
			if (enemy && PvIFrames && done > 0)
			{
				try
				{
					_pvAfterHit ??= typeof(SoldierNPC).GetMethod("AfterHit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
					_pvAfterHit.Invoke(victim, new object[] { done, src, -1 });
				}
				catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-PVP: AfterHit failed: " + e.Message); victim.ImmuneTime = victim.HitImmuneTicks(done); }
			}
			return done;
		}

		/// <summary>Strike detour: a hit on a team body. <paramref name="src"/> is the soldier slot that caused it (the projectile's soldier, or the soldier whose update swung), -1 unknown.</summary>
		private static void PvCredit(NPC victim, int done, int src)
		{
			var st = _pvS; if (st == null || done <= 0) return;
			int vt = PvpLab.TeamOf(victim.whoAmI); if (vt == 0) return;
			if (src >= 0 && st.ByIdx.TryGetValue(src, out var l) && st.TeamOfLevy[l] != vt)
			{
				st.Dealt[l] += done;
				var pj = _crProjDepth > 0 ? _pvProj : null; // what landed it: a lash, a minion (or its shot), another shot, or the levy's own swing
				string k = (st.TeamOfLevy[l] == 1 ? "A:" : "B:") + (pj == null ? "swing" : global::Levy.Common.Weapons.RealWhips.IsWhipProjectile(pj) ? "whip" : pj.minion || pj.sentry || (pj.type < Terraria.ID.ProjectileID.Sets.MinionShot.Length && Terraria.ID.ProjectileID.Sets.MinionShot[pj.type]) ? "minion" : "shot");
				st.Kind.TryGetValue(k, out long kv); st.Kind[k] = kv + done;
				return;
			}
			if (src >= 0 && PvpLab.TeamOf(src) == vt) st.Dot[vt] += done; // the body's own update (debuff damage, a hazard)
			else st.Unattr[vt] += done;
		}

		private void PvStartRecording(PvState s, CrRun run)
		{
			if (!global::Levy.Common.Recording.FightRecAuto.Enabled) return; // LEVY_REC_DIR unset: no recording
			try
			{
				if (global::Levy.Common.Recording.FightRecorder.On) global::Levy.Common.Recording.FightRecorder.Stop("next fight started");
				global::Levy.Common.Recording.FightRecorder.Headless = true;
				global::Levy.Common.Recording.FightRecorder.OutDirOverride = global::Levy.Common.Recording.FightRecAuto.Dir;
				global::Levy.Common.Recording.FightRecorder.IgnoreInactive = true;
				string ids(List<CrLevy> a) => string.Join(",", a.Select(l => l.Rec.Id));
				var meta = new Dictionary<string, string>
				{
					["harness"] = "calrev", ["pvp"] = "1", ["label"] = run.TeamA + " vs " + run.TeamB, ["teamA"] = run.TeamA, ["teamB"] = run.TeamB, ["sideA"] = "left", ["sideB"] = "right",
					["idsA"] = ids(s.A), ["idsB"] = ids(s.B), ["arena"] = run.Arena, ["seed"] = run.Seed.ToString(CultureInfo.InvariantCulture), ["mode"] = run.Mode, ["order"] = run.Order, ["cellkey"] = run.Key,
					["note"] = "Two levy armies of the same commander fight each other (PvP lab). Levy names start with the army label; the commander stands at the arena centre and takes no part.",
				};
				global::Levy.Common.Recording.FightRecorder.FightLabel = "PvP " + run.TeamA + " vs " + run.TeamB;
				string path = global::Levy.Common.Recording.FightRecorder.Start(Main.player[0], "PvP " + run.TeamA + " vs " + run.TeamB, out string err, meta, $"pvp_{run.TeamA}_vs_{run.TeamB}_s{run.Seed}");
				s.Recording = path != null;
				Mod.Logger.Info("HARNESS-PVP: recording " + (path ?? "failed: " + err));
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-PVP: recording failed: " + e.Message); }
		}

		private void PvFinish(CrRun run, PvState st)
		{
			int end = st.EndTick > 0 ? st.EndTick : st.Tick;
			string f1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
			string Army(List<CrLevy> a) => string.Join(";", a.Select(l => $"{l.Cls}:{l.Weapon.Replace(' ', '_')}:dmg{st.Dealt[l]}:{(l.Dead ? "dead@" + f1(l.DeathTick / 60.0) : "alive")}:hp{l.MaxHp}:pot{l.PotionsDrunk}"));
			string Cls(List<CrLevy> a) => string.Join(" ", a.GroupBy(l => l.Cls).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Sum(l => st.Dealt[l])));
			string Top(List<CrLevy> a) => string.Join(" ", a.GroupBy(l => l.Weapon).Select(g => (w: g.Key, d: g.Sum(l => st.Dealt[l]), n: g.Count())).OrderByDescending(x => x.d).Take(6).Select(x => x.w.Replace("CalamityMod/", "").Replace(' ', '_') + "x" + x.n + "=" + x.d));
			string Deaths(List<CrLevy> a) => string.Join(",", a.Where(l => l.Dead).Select(l => l.DeathTick / 60.0).OrderBy(x => x).Select(f1));
			int aliveA = st.A.Count(l => !l.Dead), aliveB = st.B.Count(l => !l.Dead);
			long dA = st.A.Sum(l => st.Dealt[l]), dB = st.B.Sum(l => st.Dealt[l]);
			string winner = st.Result.StartsWith("A") ? run.TeamA : st.Result.StartsWith("B") ? run.TeamB : "draw";
			try
			{
				string path = _crOut + ".pvp.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tseed\tteamA\tteamB\tresult\twinner\tsec\tnA\tnB\taliveA\taliveB\thpLeftA\thpLeftB\tdmgA\tdmgB\tclassA\tclassB\ttopA\ttopB\tdeathsA\tdeathsB\tpotionsA\tpotionsB\ttakenA\ttakenB\tunattrA\tunattrB\tdotA\tdotB\tperA\tperB\n");
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Seed, run.TeamA, run.TeamB, st.Result, winner, f1(end / 60.0), st.A.Count, st.B.Count, aliveA, aliveB,
					(100 * PvHpShare(st.A)).ToString("0", CultureInfo.InvariantCulture), (100 * PvHpShare(st.B)).ToString("0", CultureInfo.InvariantCulture), dA, dB, Cls(st.A), Cls(st.B), Top(st.A), Top(st.B), Deaths(st.A), Deaths(st.B),
					st.A.Sum(l => l.PotionsDrunk), st.B.Sum(l => l.PotionsDrunk), st.A.Sum(l => l.TakenOther), st.B.Sum(l => l.TakenOther), st.Unattr[1], st.Unattr[2], st.Dot[1], st.Dot[2], Army(st.A), Army(st.B) }) + "\n");
				// the ordinary calrev row, so drivers see the fight as done (result A / B / DRAW, deaths of both armies)
				var cols = CrHeader.Split('\t'); var v = new string[cols.Length]; for (int i = 0; i < v.Length; i++) v[i] = "0";
				void Set(string c, object o) { int i = Array.IndexOf(cols, c); if (i >= 0) v[i] = Convert.ToString(o, CultureInfo.InvariantCulture); }
				Set("key", run.Key); Set("kind", run.Kind); Set("boss", "pvp"); Set("mode", run.Mode); Set("order", run.Order); Set("seed", run.Seed); Set("result", st.Result); Set("ticks", end); Set("sec", f1(end / 60.0));
				Set("dmgBoss", dA); Set("dmgAdd", dB); Set("deaths", st.A.Count(l => l.Dead) + st.B.Count(l => l.Dead)); Set("n", st.A.Count + st.B.Count); Set("arena", run.Arena); Set("gear", run.TeamA + "_vs_" + run.TeamB);
				Set("potionsDrunk", st.A.Sum(l => l.PotionsDrunk) + st.B.Sum(l => l.PotionsDrunk)); Set("perLevy", "aliveA=" + aliveA + " aliveB=" + aliveB);
				File.AppendAllText(_crOut, string.Join("\t", v) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-PVP: row failed: " + e); }
			Mod.Logger.Info($"HARNESS-CR: RESULT {run.Key} -> {st.Result} t={end / 60.0:0.0}s winner={winner} alive {run.TeamA}={aliveA}/{st.A.Count} {run.TeamB}={aliveB}/{st.B.Count} dmg {run.TeamA}={dA} {run.TeamB}={dB} unattr={st.Unattr[1]}/{st.Unattr[2]} dot={st.Dot[1]}/{st.Dot[2]} by={string.Join(",", st.Kind.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value))}");
			PvStopRecording(st);
			PvpLab.Reset(); _pvS = null; _pvProj = null; global::Levy.Common.Movement.OffscreenHop.LabViewers = null;
			_crStarted = false; _crI++;
			if (run.Dl != "") DlEnd();
			CrSetHpScale(null); CrSetFear(null); CrSetOptions(null);
			ClearAll(); CrPurgeNpcs();
		}

		private static void PvStopRecording(PvState st)
		{
			if (st == null || !st.Recording) return;
			try { global::Levy.Common.Recording.FightRecorder.Stop("fight over"); } catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-PVP: recorder stop failed: " + e.Message); }
			st.Recording = false;
		}

		/// <summary>A run threw: drop the pvp state so the next line starts clean.</summary>
		private static void PvAbort()
		{
			PvStopRecording(_pvS);
			PvpLab.Reset(); _pvS = null; _pvProj = null; global::Levy.Common.Movement.OffscreenHop.LabViewers = null;
		}
	}

	/// <summary>PvP lab: a projectile of the other army does not hit a levy inside his player-style hit window (the levy's own rule for an enemy's projectile, SoldierProjectileImmunity).</summary>
	public sealed class PvpImmunity : GlobalNPC
	{
		public override bool IsLoadingEnabled(Mod mod) => Environment.GetEnvironmentVariable("LEVY_HARNESS") == "1"; // the harness only: no hook in a game
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<SoldierNPC>();
		public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
		{
			if (!PvpLab.On || !CombatHarness.PvIFrames || !(npc.ModNPC is SoldierNPC s)) return null;
			return s.ImmuneTime > 0 && PvpLab.ShotMayHit(projectile, npc) ? false : null;
		}
	}
}
