using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using Levy.Common.Globals;

namespace Levy.Common.Testing
{
	/// <summary>
	/// EXO MECHS for the calrev harness (lab "exolab", 2026-11-22). Measurement only: nothing here changes product code.
	/// Boss lines <c>exoares exothan exotwins</c>: Draedon is spawned beside the commander the way the Codebreaker does it, told that he has been talked to before (so he skips to the choice),
	/// and the given mech is chosen for him (CalamityWorld.DraedonMechToSummon, what the selection UI writes). From then on Calamity runs the whole fight: Draedon summons the mech, the other
	/// two arrive at 70%, the passive and invisible interludes, the berserk phases, his dialogue. The Exo Mechs count as beaten before (a rematch), so the Spectator commander is hidden from them.
	/// The fight is the three life pools (Ares with his arms, Thanatos with his segments, Artemis and Apollo who mirror one life); it is won when all three are dead.
	/// Arena <c>big</c>: an enormous open arena (a 1200-tile asphalt floor, three platform rows, 220 tiles of open sky). With cmd=far the commander stands 150 tiles left of the army.
	/// Per fight it writes <c>out.tsv.exo.tsv</c> (one row: kill order, phases, per-mech damage and DPS, who the mechs targeted, what the levies were doing, ms per tick) and
	/// <c>out.tsv.exo_events.tsv</c> (spawns, deaths, interludes, the phase timeline).
	/// </summary>
	public partial class CombatHarness
	{
		internal static bool ExIsExo(string b) => b is "exoares" or "exothan" or "exotwins";
		private static int ExChoice(string b) => b == "exothan" ? 1 : b == "exoares" ? 2 : 3; // CalamityMod.Enums.ExoMech: None, Destroyer (Thanatos), Prime (Ares), Twins
		private static readonly string[] ExPoolName = { "Ares", "Thanatos", "Twins" };
		private static readonly string[] ExPoolCode = { "A", "T", "W" };

		internal sealed class ExState
		{
			public int[] Main = { -1, -1, -1 };
			public bool[] Seen = new bool[3], Dead = new bool[3], Present = new bool[3], Immune = new bool[3];
			public long[] Life = new long[3], LifeMax = new long[3], LastLife = new long[3], Dmg = new long[3], DmgTicks = new long[3], PresentTicks = new long[3];
			public float[] Sec2 = { -99, -99, -99 };
			public int[] SpawnTick = { -1, -1, -1 }, DeathTick = { -1, -1, -1 };
			public long[,] MechTgt = new long[3, 4]; // pool x (0 none, 1 commander, 2 levy, 3 spite)
			public string Label = ""; public int LabelStart; public List<string> Phases = new();
			public Dictionary<string, long[]> PhaseStat = new(); // label -> ticks, dmg, levySamples, idleSamples, immuneTgtSamples, engageSamples, msTicks(x1000)
			public long LvSamples, LvNone, LvDmgTgt, LvImmTgt, LvDraedon, LvOther, LvIdle, LvEngage, LvFar, LvOff, LvNoneWhileOpen;
			public HashSet<int> OffArena = new(), Far = new();
			public long ClosedHits, OpenHits, DraedonHits, ImmuneHits;
			public double WallMs; public long WallTicks; public long LastTs; public double PhaseWall;
			public int DraedonIdx = -1; public bool ChoiceSet; public int FirstMechTick = -1;
			public long DraedonTgtLevy, DraedonTgtCmd, DraedonTgtNone; public float DraedonDistArmy, DraedonDistN;
			public List<string> Events = new();
		}
		private static ExState _ex;
		internal static ExState ExLive => _ex;

		private static int _exDraedon = -2, _exAres, _exThan, _exTwinA, _exTwinB; private static HashSet<int> _exAres4, _exThanSeg;
		private static void ExTypes()
		{
			if (_exDraedon != -2) return;
			_exDraedon = CrCalNpc("Draedon"); _exAres = CrCalNpc("AresBody"); _exThan = CrCalNpc("ThanatosHead"); _exTwinA = CrCalNpc("Artemis"); _exTwinB = CrCalNpc("Apollo");
			_exAres4 = new HashSet<int> { CrCalNpc("AresLaserCannon"), CrCalNpc("AresPlasmaFlamethrower"), CrCalNpc("AresTeslaCannon"), CrCalNpc("AresGaussNuke") };
			_exThanSeg = new HashSet<int> { CrCalNpc("ThanatosBody1"), CrCalNpc("ThanatosBody2"), CrCalNpc("ThanatosTail") };
		}
		/// <summary>0 Ares (body and arms), 1 Thanatos (head and segments), 2 the twins, 3 Draedon, -1 anything else.</summary>
		internal static int ExPoolOf(int type)
		{
			ExTypes();
			if (type <= 0) return -1;
			if (type == _exAres || _exAres4.Contains(type)) return 0;
			if (type == _exThan || _exThanSeg.Contains(type)) return 1;
			if (type == _exTwinA || type == _exTwinB) return 2;
			if (type == _exDraedon) return 3;
			return -1;
		}

		private static int ExBossType() { ExTypes(); return _exDraedon; }

		private static void ExSetup(CrState s)
		{
			ExTypes();
			foreach (int t in new[] { _exAres, _exThan, _exTwinA, _exTwinB }.Concat(_exAres4).Concat(_exThanSeg)) if (t > 0) s.PartTypes.Add(t);
			foreach (int t in new[] { _exAres, _exThan, _exTwinA, _exTwinB }) if (t > 0) global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(t, Environment.GetEnvironmentVariable("LEVY_CR_DOWNED") != "0"); // a rematch: the Spectator commander is hidden from beaten bosses only
		}

		private static Type _exWorldT;
		private static void ExSetDraedonChoice(int choice)
		{
			try
			{
				if (_exWorldT == null && ModLoader.TryGetMod("CalamityMod", out Mod cal)) _exWorldT = cal.Code.GetType("CalamityMod.World.CalamityWorld");
				var f = _exWorldT?.GetField("DraedonMechToSummon", BindingFlags.Public | BindingFlags.Static);
				f?.SetValue(null, Enum.ToObject(f.FieldType, choice));
				_exWorldT?.GetField("TalkedToDraedon", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, true);
			}
			catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("HARNESS-EXO: choice failed " + e.Message); }
		}

		private static void ExBegin(CrRun run)
		{
			NPC.downedMoonlord = NPC.downedAncientCultist = NPC.downedGolemBoss = NPC.downedPlantBoss = NPC.downedFishron = NPC.downedEmpressOfLight = true;
			NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = NPC.downedMechBossAny = true;
			try
			{
				if (_bbDownedType == null && ModLoader.TryGetMod("CalamityMod", out Mod cal)) _bbDownedType = cal.Code.GetType("CalamityMod.DownedBossSystem");
				foreach (string fn in new[] { "downedProvidence", "downedPolterghast", "downedDoG", "downedYharon", "downedRavager", "downedPlaguebringer", "downedLeviathan", "downedAstrumAureus", "downedSignus", "downedStormWeaver", "downedCeaselessVoid", "downedDragonfolly", "downedGuardians", "downedBoomerDuke" })
				{
					var p = _bbDownedType?.GetProperty(fn, BindingFlags.Public | BindingFlags.Static);
					if (p != null) p.SetValue(null, true); else _bbDownedType?.GetField(fn, BindingFlags.Public | BindingFlags.Static)?.SetValue(null, true);
				}
			}
			catch { }
			_ex = new ExState(); _exHitOn = true;
			ExSetDraedonChoice(0); // a choice left over from a fight before must not summon the next mech early
		}

		/// <summary>After the generic spawn put Draedon at the lab's spot: he hovers beside the commander, and the mech is chosen for him.</summary>
		private static void ExAfterSpawn(CrState s, CrRun run, int ei)
		{
			var x = _ex; if (x == null) return;
			x.DraedonIdx = ei;
			NPC d = Main.npc[ei];
			Player p = Main.player[0];
			d.Center = p.Center + new Vector2(0f, -160f); d.velocity = Vector2.Zero; d.target = 0;
			ExSetDraedonChoice(ExChoice(run.Boss)); x.ChoiceSet = true;
			ExEvent(x, 0, "draedon", $"spawned beside the commander; choice {ExPoolName[ExChoice(run.Boss) == 1 ? 1 : ExChoice(run.Boss) == 2 ? 0 : 2]}");
		}

		private static void ExEvent(ExState x, int tick, string kind, string detail) => x.Events.Add($"{tick / 60.0:0.0}\t{kind}\t{detail}");

		private static GlobalNPC _exCalG; private static MethodInfo _exInst; private static FieldInfo _exNewAI;
		private static float ExNewAI1(NPC n)
		{
			try
			{
				if (_exCalG == null) { if (!ModContent.TryFind("CalamityMod/CalamityGlobalNPC", out GlobalNPC g)) return -1; _exCalG = g; _exInst = g.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "Instance" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(NPC)); _exNewAI = g.GetType().GetField("newAI", BindingFlags.Public | BindingFlags.Instance); }
				object inst = _exInst?.Invoke(_exCalG, new object[] { n }); return inst != null && _exNewAI?.GetValue(inst) is float[] a && a.Length > 1 ? a[1] : -1;
			}
			catch { return -1; }
		}

		/// <summary>One tick of an Exo fight (in place of the worm tick): the three pools, the phase label, the samples. True while the fight goes on.</summary>
		private bool ExTick(CrState st, ref NPC e)
		{
			var x = _ex; if (x == null) return false;
			long now = System.Diagnostics.Stopwatch.GetTimestamp();
			if (x.LastTs != 0) { double ms = (now - x.LastTs) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; if (ms < 500) { x.WallMs += ms; x.WallTicks++; x.PhaseWall += ms; } }
			x.LastTs = now;
			if (!x.ChoiceSet || !x.Seen.Any(b => b)) ExSetDraedonChoice(ExChoice(st.Run.Boss)); // Calamity clears the choice while Draedon's index is still unset
			int[] main = { -1, -1, -1 }; long[] life = new long[3], lmax = new long[3]; bool[] imm = { true, true, true }; int twins = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active) continue;
				int pool = ExPoolOf(n.type); if (pool < 0) continue;
				n.timeLeft = Math.Max(n.timeLeft, 3600);
				if (pool == 3) { x.DraedonIdx = i; continue; }
				bool isMain = n.type == _exAres || n.type == _exThan || n.type == _exTwinA || n.type == _exTwinB;
				if (!isMain) continue;
				if (pool == 2) { twins++; life[2] = main[2] < 0 ? n.life : Math.Min(life[2], n.life); lmax[2] = Math.Max(lmax[2], n.lifeMax); if (main[2] < 0 || n.type == _exTwinB) main[2] = i; if (!n.dontTakeDamage) imm[2] = false; }
				else { main[pool] = i; life[pool] = n.life; lmax[pool] = n.lifeMax; imm[pool] = n.dontTakeDamage; }
			}
			for (int k = 0; k < 3; k++)
			{
				bool pres = main[k] >= 0;
				if (pres && !x.Seen[k]) { x.Seen[k] = true; x.SpawnTick[k] = st.Tick; x.LastLife[k] = life[k]; if (x.FirstMechTick < 0) x.FirstMechTick = st.Tick; ExEvent(x, st.Tick, "spawn", $"{ExPoolName[k]} life {life[k]}"); }
				if (pres)
				{
					long drop = x.LastLife[k] - life[k]; if (drop > 0) { x.Dmg[k] += drop; ExPhase(x, x.Label)[1] += drop; }
					x.LastLife[k] = life[k]; x.LifeMax[k] = Math.Max(x.LifeMax[k], lmax[k]); x.Life[k] = life[k]; x.PresentTicks[k]++;
					if (!imm[k]) x.DmgTicks[k]++;
					if (imm[k] != x.Immune[k] || !x.Present[k]) ExEvent(x, st.Tick, imm[k] ? "immune" : "open", $"{ExPoolName[k]} at {100.0 * life[k] / Math.Max(1, lmax[k]):0}%");
					float s2 = ExNewAI1(Main.npc[main[k]]); if (s2 != x.Sec2[k]) { if (x.Sec2[k] > -99) ExEvent(x, st.Tick, "state", $"{ExPoolName[k]} secondary {x.Sec2[k]:0}->{s2:0} at {100.0 * life[k] / Math.Max(1, lmax[k]):0}%"); x.Sec2[k] = s2; }
					x.Immune[k] = imm[k];
				}
				else if (x.Present[k])
				{
					bool died = x.LastLife[k] <= Math.Max(1, x.LifeMax[k] / 100);
					if (!x.Dead[k]) { x.Dead[k] = true; x.DeathTick[k] = st.Tick; x.Dmg[k] += x.LastLife[k]; ExPhase(x, x.Label)[1] += x.LastLife[k]; ExEvent(x, st.Tick, died ? "killed" : "gone", $"{ExPoolName[k]} (last life {x.LastLife[k]})"); x.LastLife[k] = 0; x.Life[k] = 0; }
				}
				x.Present[k] = pres; x.Main[k] = main[k];
			}
			// phase label: an upper-case letter for a mech that can be hurt, lower case for one that is there but passive and immune
			string label = string.Concat(Enumerable.Range(0, 3).Where(k => x.Present[k]).Select(k => x.Immune[k] ? ExPoolCode[k].ToLowerInvariant() : ExPoolCode[k]));
			if (label == "") label = x.Seen.Any(b => b) ? "-" : "pre";
			if (label != x.Label)
			{
				if (x.Label != "") x.Phases.Add($"{x.Label}@{x.LabelStart / 60.0:0}");
				ExEvent(x, st.Tick, "phase", label); x.Label = label; x.LabelStart = st.Tick;
			}
			var ps = ExPhase(x, label); ps[0]++; ps[6] += (long)(x.PhaseWall * 1000); x.PhaseWall = 0;
			// the fight's life: the three pools (Draedon's own life never counts)
			long sum = 0, smax = 0; for (int k = 0; k < 3; k++) { if (x.Present[k]) sum += x.Life[k]; if (x.Seen[k]) smax += x.LifeMax[k]; }
			st.BossLife = (int)Math.Min(sum, int.MaxValue); st.BossLifeMaxSum = Math.Max(st.BossLifeMaxSum, smax); st.BossLifeMax = (int)Math.Min(st.BossLifeMaxSum, int.MaxValue);
			if (st.HalfTick < 0 && x.Seen.All(b => b) && sum * 2 <= st.BossLifeMaxSum) st.HalfTick = st.Tick;
			int lead = -1; for (int k = 0; k < 3 && lead < 0; k++) if (x.Present[k] && !x.Immune[k]) lead = k; for (int k = 0; k < 3 && lead < 0; k++) if (x.Present[k]) lead = k;
			if (lead >= 0) e = Main.npc[x.Main[lead]]; else if (x.DraedonIdx >= 0 && Main.npc[x.DraedonIdx].active) e = Main.npc[x.DraedonIdx];
			if (st.Tick % 15 == 0) ExSample(st, x);
			bool any = x.Present.Any(b => b);
			bool alive = any || (!x.Seen.Any(b => b) && st.Tick < 1800);
			if (!alive && !st.BossGone)
			{
				st.BossGone = true; st.EndTick = st.Tick; st.BossLife = 0;
				bool flag = false; try { var p = _bbDownedType?.GetProperty("downedExoMechs", BindingFlags.Public | BindingFlags.Static); flag = p != null && (bool)p.GetValue(null); } catch { }
				bool all = x.Dead.All(b => b) && x.Seen.All(b => b);
				st.Result = all ? "WIN" : "DESPAWN";
				ExEvent(x, st.Tick, "end", $"{st.Result} downedExoMechs={flag}");
			}
			return alive;
		}

		private static long[] ExPhase(ExState x, string label) { if (!x.PhaseStat.TryGetValue(label, out var a)) x.PhaseStat[label] = a = new long[7]; return a; }

		/// <summary>Every 15 ticks: who each mech and Draedon go for, and what every living levy is doing.</summary>
		private void ExSample(CrState st, ExState x)
		{
			for (int k = 0; k < 3; k++)
			{
				if (!x.Present[k] || x.Immune[k]) continue;
				global::Levy.Common.Recording.FightRecorder.TargetOf(Main.npc[x.Main[k]], out int kind, out _);
				x.MechTgt[k, Math.Clamp(kind, 0, 3)]++;
			}
			// the parts a levy could hurt right now, and their centres
			var open = new List<Vector2>(); var partIdx = new HashSet<int>();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (!n.active) continue; int pool = ExPoolOf(n.type); if (pool < 0 || pool == 3) continue; partIdx.Add(i); if (!n.dontTakeDamage) open.Add(n.Center); }
			Vector2 armyC = Vector2.Zero; int na = 0;
			var ps = ExPhase(x, x.Label);
			foreach (var l in st.Levies)
			{
				if (l.Dead) continue;
				NPC n = Main.npc[l.Idx]; if (!n.active || !(n.ModNPC is SoldierNPC sn) || !ReferenceEquals(sn.Record, l.Rec) || n.life <= 0) continue;
				armyC += n.Center; na++;
				x.LvSamples++; ps[2]++;
				int t = sn.TargetIndex; bool engage = sn.State == SoldierState.Engage; if (engage) { x.LvEngage++; ps[5]++; }
				if (t < 0 || t >= Main.maxNPCs || !Main.npc[t].active) { x.LvNone++; if (open.Count > 0) { x.LvNoneWhileOpen++; ps[3]++; } }
				else if (partIdx.Contains(t)) { if (Main.npc[t].dontTakeDamage) { x.LvImmTgt++; ps[4]++; } else x.LvDmgTgt++; }
				else if (ExPoolOf(Main.npc[t].type) == 3) x.LvDraedon++;
				else x.LvOther++;
				if (open.Count > 0 && !engage && !(t >= 0 && t < Main.maxNPCs && partIdx.Contains(t))) x.LvIdle++;
				if (open.Count > 0) { float best = float.MaxValue; foreach (var c in open) best = Math.Min(best, Vector2.DistanceSquared(c, n.Center)); if (best > 100f * 16f * 100f * 16f) { x.LvFar++; x.Far.Add(l.Id); } }
				bool off = n.Center.Y > (CrF + 6) * 16f || Math.Abs(n.Center.X / 16f - _crCx) > _exHalf + 4;
				if (off) { x.LvOff++; if (x.OffArena.Add(l.Id)) ExEvent(x, st.Tick, "offarena", $"levy {l.Id} ({l.Cls} {l.Weapon}) at {n.Center.X / 16f - _crCx:0},{n.Center.Y / 16f - CrF:0} tiles from the arena centre floor"); }
			}
			if (x.DraedonIdx >= 0 && Main.npc[x.DraedonIdx].active)
			{
				NPC d = Main.npc[x.DraedonIdx];
				global::Levy.Common.Recording.FightRecorder.TargetOf(d, out int dk, out _);
				if (dk == 2 || dk == 3) x.DraedonTgtLevy++; else if (dk == 1) x.DraedonTgtCmd++; else x.DraedonTgtNone++;
				if (na > 0) { x.DraedonDistArmy += Vector2.Distance(d.Center, armyC / na) / 16f; x.DraedonDistN++; }
			}
		}

		private static string ExPct(long a, long b) => b <= 0 ? "-" : (100.0 * a / b).ToString("0", CultureInfo.InvariantCulture);

		private void ExFinish(CrRun run, CrState st, int end)
		{
			var x = _ex; if (x == null) return;
			try
			{
				if (x.Label != "") x.Phases.Add($"{x.Label}@{x.LabelStart / 60.0:0}");
				string path = _crOut + ".exo.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tboss\tseed\tresult\tsec\tfirstMechSec\tkillOrder\tphases\tA_max\tA_left\tA_dmg\tA_openSec\tA_dps\tT_max\tT_left\tT_dmg\tT_openSec\tT_dps\tW_max\tW_left\tW_dmg\tW_openSec\tW_dps\tdeaths\tn\tdeathBy\t"
					+ "A_tgt(none/cmd/levy/spite)\tT_tgt\tW_tgt\tlvTgtOpen%\tlvTgtImmune%\tlvTgtDraedon%\tlvTgtOther%\tlvNoTgtWhileOpen%\tlvIdle%\tlvEngage%\tlvFar100%\tfarLevies\toffArenaLevies\tthanClosedHits\tthanOpenHits\tdraedonHits\tdraedonTgt(levy/cmd/none)\tdraedonDistArmyTiles\tmsPerTick\tphaseStats\n");
				Func<double, string> f1 = v => v.ToString("0.0", CultureInfo.InvariantCulture);
				string ko = string.Join(";", Enumerable.Range(0, 3).Where(k => x.DeathTick[k] >= 0).OrderBy(k => x.DeathTick[k]).Select(k => $"{ExPoolName[k]}@{f1(x.DeathTick[k] / 60.0)}"));
				var row = new List<object> { run.Key, run.Boss, run.Seed, st.Result, f1(end / 60.0), x.FirstMechTick < 0 ? "-" : f1(x.FirstMechTick / 60.0), ko == "" ? "-" : ko, string.Join(" ", x.Phases) };
				for (int k = 0; k < 3; k++) { row.Add(x.LifeMax[k]); row.Add(x.Seen[k] ? (x.Dead[k] ? 0 : x.Life[k]) : -1); row.Add(x.Dmg[k]); row.Add(f1(x.DmgTicks[k] / 60.0)); row.Add(x.DmgTicks[k] > 0 ? f1(x.Dmg[k] / (x.DmgTicks[k] / 60.0)) : "-"); }
				row.Add(st.Levies.Count(l => l.Dead)); row.Add(st.Levies.Count);
				row.Add(string.Join(" ", st.DeathBy.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key.Replace(' ', '_') + "=" + kv.Value)));
				for (int k = 0; k < 3; k++) { long tt = x.MechTgt[k, 0] + x.MechTgt[k, 1] + x.MechTgt[k, 2] + x.MechTgt[k, 3]; row.Add(tt == 0 ? "-" : $"{ExPct(x.MechTgt[k, 0], tt)}/{ExPct(x.MechTgt[k, 1], tt)}/{ExPct(x.MechTgt[k, 2], tt)}/{ExPct(x.MechTgt[k, 3], tt)}"); }
				long s = x.LvSamples;
				row.Add(ExPct(x.LvDmgTgt, s)); row.Add(ExPct(x.LvImmTgt, s)); row.Add(ExPct(x.LvDraedon, s)); row.Add(ExPct(x.LvOther, s)); row.Add(ExPct(x.LvNoneWhileOpen, s)); row.Add(ExPct(x.LvIdle, s)); row.Add(ExPct(x.LvEngage, s)); row.Add(ExPct(x.LvFar, s)); row.Add(x.Far.Count); row.Add(x.OffArena.Count);
				row.Add(x.ClosedHits); row.Add(x.OpenHits); row.Add(x.DraedonHits);
				long dt = x.DraedonTgtLevy + x.DraedonTgtCmd + x.DraedonTgtNone; row.Add(dt == 0 ? "-" : $"{ExPct(x.DraedonTgtLevy, dt)}/{ExPct(x.DraedonTgtCmd, dt)}/{ExPct(x.DraedonTgtNone, dt)}");
				row.Add(x.DraedonDistN > 0 ? f1(x.DraedonDistArmy / x.DraedonDistN) : "-");
				row.Add(x.WallTicks > 0 ? (x.WallMs / x.WallTicks).ToString("0.00", CultureInfo.InvariantCulture) : "-");
				row.Add(string.Join(" ", x.PhaseStat.Where(kv => kv.Value[0] >= 30).Select(kv => $"{kv.Key}:{f1(kv.Value[0] / 60.0)}s,dps{(kv.Value[1] / Math.Max(1.0, kv.Value[0] / 60.0)):0},idle{ExPct(kv.Value[3], kv.Value[2])}%,immT{ExPct(kv.Value[4], kv.Value[2])}%,eng{ExPct(kv.Value[5], kv.Value[2])}%,ms{(kv.Value[6] / 1000.0 / Math.Max(1, kv.Value[0])):0.0}")));
				File.AppendAllText(path, string.Join("\t", row) + "\n");
				string ep = _crOut + ".exo_events.tsv";
				if (!File.Exists(ep)) File.WriteAllText(ep, "key\tsec\tevent\tdetail\n");
				File.AppendAllText(ep, string.Concat(x.Events.Select(ev => run.Key + "\t" + ev + "\n")));
				Mod.Logger.Info($"HARNESS-EXO: {run.Key} {st.Result} t={end / 60.0:0.0}s kill={ko} phases={string.Join(" ", x.Phases)} msPerTick={(x.WallTicks > 0 ? x.WallMs / x.WallTicks : 0):0.00}");
			}
			catch (Exception ex) { Mod.Logger.Warn("HARNESS-EXO: finish failed: " + ex); }
		}

		private static void ExEnd() { _ex = null; _exHitOn = false; ExSetDraedonChoice(0); }

		// ------------------------------------------------------------------------------------------------------------------ the big arena
		private static int _exHalf = 110;
		/// <summary>Arena "big": a 1200-tile asphalt floor (4 tiles thick), three platform rows 12, 24 and 36 tiles above it with a 10-tile gap every 40 tiles, and 220 tiles of cleared open sky.</summary>
		private void ExBuildBig(string kind)
		{
			float nn = Main.maxTilesX / 4200f; nn *= nn;
			int band = (int)(60f + 10f * nn + Main.worldSurface / 6.0) + 2;
			_crCx = _cx; int cx = _cx;
			int half = Math.Min(600, Math.Min(cx - 60, Main.maxTilesX - cx - 60)); _exHalf = half;
			int fy = Math.Clamp(Math.Max(band + 75, 240), 100, Main.maxTilesY - 120), top = Math.Max(10, fy - 220);
			for (int i = cx - half - 20; i <= cx + half + 20; i++)
			{
				for (int j = top; j < fy; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				if (i < cx - half || i > cx + half) continue;
				for (int j = fy; j < fy + 4; j++) { Tile f = Main.tile[i, j]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Asphalt; f.BlockType = Terraria.ID.BlockType.Solid; }
				foreach (int row in new[] { 12, 24, 36 })
				{
					if (i < cx - half + 6 || i > cx + half - 6 || (i - cx + half + row) % 40 >= 30) continue;
					Tile pf = Main.tile[i, fy - row]; pf.HasTile = true; pf.TileType = TileID.Platforms; pf.TileFrameX = 0; pf.TileFrameY = 0; pf.BlockType = Terraria.ID.BlockType.Solid;
				}
			}
			CrF = fy; _crArenaBuilt = kind;
			Mod.Logger.Info($"HARNESS-EXO: arena '{kind}' built: asphalt floor row {fy}, half-width {half}, rows at 12/24/36, sky cleared to row {top}; worldSurface {Main.worldSurface:0} => {(fy < Main.worldSurface ? "ABOVE" : "BELOW")} worldSurface; world {Main.maxTilesX}x{Main.maxTilesY}");
		}

		// ------------------------------------------------------------------------------------------------------------------ hits on Thanatos and Draedon
		private static bool _exHitOn;
		public class ExHitWatch : GlobalNPC
		{
			public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
			{
				if (!_exHitOn) return; var x = _ex; if (x == null) return;
				int pool = ExPoolOf(npc.type); if (pool < 0) return;
				if (pool == 3) { x.DraedonHits++; return; }
				if (npc.dontTakeDamage) x.ImmuneHits++;
				if (pool == 1) { if (damageDone * 20 < Math.Max(1, hit.SourceDamage)) x.ClosedHits++; else x.OpenHits++; }
			}
			public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
			{
				if (!_exHitOn) return; var x = _ex; if (x == null) return;
				int pool = ExPoolOf(npc.type); if (pool < 0) return;
				if (pool == 3) { x.DraedonHits++; return; }
				if (pool == 1) { if (damageDone * 20 < Math.Max(1, hit.SourceDamage)) x.ClosedHits++; else x.OpenHits++; }
			}
		}
	}
}
