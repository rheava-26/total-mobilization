using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PA = Levy.Common.Perf.PerfAnalyzer;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS-FIGHT COST: what an army costs the game tick while a boss floods the field with hostile projectiles, whether the levies are fighting or only standing there. Logged as "[PerfBoss]" lines when a world
	/// loads on a dedicated server, with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the shop lag check) or LEVY_PERFBOSS=1 (lab, standalone).
	/// <para/>A flat arena, a real army (LEVY_PERFBOSS_LEVIES bodies, default 150, in hallowed / titanium kits with a ranger, mage, melee and summoner mix and proc accessories, hit points high enough that nobody dies), slot 0 standing in
	/// as the commander in the middle of it. Each ARM steps the real Main.Update for a number of ticks and times every tick in thread CPU time (wall time is shared with every other process on the box): a CONTROL arm first with no
	/// army at all, then the same arm with the army, so the difference is what the levies add. The arms: a quiet field; ~500 hostile projectiles (Plantera's seeds, poison seeds and thorn balls) drifting over the army, far away from it,
	/// and through it; and a real Plantera at the army's edge. The army stands at Hold (idle) in every arm but the last, where it is let loose.
	/// <para/>With LEVY_PERFBOSS_PROF=1 every hook of the mod is instrumented (<see cref="PerfProbe"/>) and the per-hook cost per tick is logged for each arm, ranked; LEVY_PERFBOSS_DEEP=Type,Type instruments every method of the named types too.
	/// PASS rules (relative, so a loaded box cannot fail them): each of the quiet, flood-over and flood-through arms is timed with the fixes off and then on in the same boot, and the army's added cost with them on must be a clear fraction
	/// of the cost with them off (flood under 0.5; the quiet field must show dormant levies skipping gear frames). Absolute microseconds per levy are logged as information. Lab (measured, 150 levies, same boot, off -> on): quiet 75 -> 42 us per levy; a 500-projectile flood over, far from and
	/// through the army 520, 640, 520 -> 41, 11, 97 us. What it cannot show: draw cost (a headless server cannot draw).
	/// </summary>
	public class PerfBossCheck : ModSystem
	{
		private static bool _armed, _done, _running, _lab; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 200;
		private const string Commander = "PerfBossTester";
		private static Vector2 _cmdAt;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool lab = Environment.GetEnvironmentVariable("LEVY_PERFBOSS") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_PERFBOSSWORLD") != "0";
			if (!lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0; _lab = lab;
			if (lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 6) return;
			RunIfArmed();
		}

		/// <summary>Runs now when this boot armed it (the world checks chain it, one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time; int cap = LevyConfig.Get?.RenderCap ?? 40;
			try { LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally
			{
				_running = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time;
				try { if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = cap; } catch (Exception) { }
				PerfProbe.On = false;
			}
			_mod.Logger.Info($"[PerfBoss] self-check pass={_pass} fail={_fail}");
			_mod.Logger.Info("[PerfBoss] done");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[PerfBoss] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[PerfBoss] OBSERVED " + s);

		// ------------------------------------------------------------------------------------------------ scene
		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.ZoneJungle = _jungle;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}
		private static bool _jungle;

		private static readonly int[][] Kits =
		{
			// ranger: Megashark, hallowed
			new int[] { ItemID.Megashark, ItemID.HallowedHelmet, ItemID.HallowedPlateMail, ItemID.HallowedGreaves },
			// mage: Crystal Storm, hallowed
			new int[] { ItemID.CrystalStorm, ItemID.HallowedHeadgear, ItemID.HallowedPlateMail, ItemID.HallowedGreaves },
			// melee: Night's Edge, titanium
			new int[] { ItemID.NightsEdge, ItemID.TitaniumMask, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings },
			// summoner: Optic Staff, titanium
			new int[] { ItemID.OpticStaff, ItemID.TitaniumHeadgear, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings },
		};
		private static readonly int[] ProcAccessories = { ItemID.StarVeil, ItemID.BeeCloak, ItemID.ObsidianShield, ItemID.BrainOfConfusion, ItemID.FrozenTurtleShell, ItemID.EyeoftheGolem };

		private static List<int> ModdedAccessories()
		{
			var pool = new List<int>();
			foreach (string modName in new[] { "CalamityMod", "ThoriumMod" })
			{
				int got = 0;
				foreach (var kv in ContentSamples.ItemsByType) { var smp = kv.Value; if (smp?.ModItem != null && smp.ModItem.Mod.Name == modName && smp.accessory && !smp.vanity && got < 4) { pool.Add(kv.Key); got++; } }
			}
			return pool;
		}

		private static SoldierNPC Levy(int k, Vector2 at, List<int> modded)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Perf" + k,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			int[] kit = Kits[k % Kits.Length];
			rec.Gear.Weapon = new Item(); rec.Gear.Weapon.SetDefaults(kit[0]);
			rec.Gear.Head = new Item(); rec.Gear.Head.SetDefaults(kit[1]); rec.Gear.Body = new Item(); rec.Gear.Body.SetDefaults(kit[2]); rec.Gear.Legs = new Item(); rec.Gear.Legs.SetDefaults(kit[3]);
			if (k % Kits.Length == 0) { rec.Gear.Ammo = new Item(); rec.Gear.Ammo.SetDefaults(ItemID.HighVelocityBullet); rec.Gear.Ammo.stack = 999; }
			int a = 0;
			for (int i = 0; i < 4 && a < rec.Gear.Accessories.Length; i++) { var acc = new Item(); acc.SetDefaults(ProcAccessories[(k + i) % ProcAccessories.Length]); rec.Gear.Accessories[a++] = acc; }
			for (int i = 0; i < 2 && modded.Count > 0 && a < rec.Gear.Accessories.Length; i++) { var acc = new Item(); acc.SetDefaults(modded[(k + i) % modded.Count]); rec.Gear.Accessories[a++] = acc; }
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = _order;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			return Main.npc[body].ModNPC as SoldierNPC;
		}
		private static SoldierOrder _order = SoldierOrder.Hold;

		private static readonly List<SoldierNPC> Army = new();
		private static float _armyLo, _armyHi;

		private static int SpawnArmy(int n)
		{
			var modded = ModdedAccessories();
			if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = Math.Max(LevyConfig.Get.RenderCap, n + 20);
			float spacing = Spacing(n);
			for (int k = 0; k < n; k++)
			{
				var s = Levy(k, new Vector2((Cx + (k - n / 2) * spacing) * 16f, FloorY * 16f), modded);
				if (s != null) Army.Add(s);
			}
			SetSpan(n);
			return Army.Count;
		}

		private static float Spacing(int n) => Math.Min(1.5f, 2 * Half * 0.9f / Math.Max(1, n));
		private static void SetSpan(int n) { float spacing = Spacing(n); _armyLo = (Cx - n / 2 * spacing) * 16f; _armyHi = (Cx + n / 2 * spacing) * 16f; }

		private static void ClearArmy()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			Army.Clear();
		}

		private static void Cleanup()
		{
			try
			{
				ClearArmy(); ClearHostiles(true);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				Main.hardMode = false; _jungle = false;
			}
			catch (Exception) { }
		}

		private static void ClearHostiles(bool all)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.friendly || n.townNPC) continue;
				if (all || !_keepNpc.Contains(n.type)) n.active = false;
			}
		}
		private static readonly HashSet<int> _keepNpc = new();

		// ------------------------------------------------------------------------------------------------ the flood
		private static readonly int[] FloodTypes = { ProjectileID.PoisonSeedPlantera, ProjectileID.SeedPlantera, ProjectileID.ThornBall };
		private static int _floodTarget; private static float _floodDx; private static bool _floodThrough; private static readonly Random Rng = new(12345);
		private static readonly List<int> FloodIdx = new();

		private static void TopUp()
		{
			if (_floodTarget <= 0) return;
			int live = 0;
			for (int i = FloodIdx.Count - 1; i >= 0; i--) { Projectile p = Main.projectile[FloodIdx[i]]; if (!p.active || p.localAI[2] != 7771f) FloodIdx.RemoveAt(i); else live++; }
			int made = 0;
			while (live < _floodTarget && made < 60)
			{
				float x = _armyLo + (float)Rng.NextDouble() * (_armyHi - _armyLo) + _floodDx * 16f;
				float y = _floodThrough ? (FloorY - 1.2f - (float)Rng.NextDouble() * 2.2f) * 16f : (FloorY - 24f - (float)Rng.NextDouble() * 20f) * 16f;
				float ang = (float)(Rng.NextDouble() * Math.PI * 2); float sp = 1.5f + (float)Rng.NextDouble() * 3f;
				int keep = Main.myPlayer; Main.myPlayer = 0;
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("PerfBoss"), new Vector2(x, y), new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * sp, FloodTypes[Rng.Next(FloodTypes.Length)], 45, 0f, 0);
				Main.myPlayer = keep;
				if (pi < 0 || pi >= Main.maxProjectiles) break;
				Projectile pr = Main.projectile[pi];
				pr.hostile = true; pr.friendly = false; pr.tileCollide = false; pr.timeLeft = 40 + Rng.Next(120); pr.localAI[2] = 7771f; pr.penetrate = -1;
				FloodIdx.Add(pi); live++; made++;
			}
		}

		// ------------------------------------------------------------------------------------------------ arms
		private sealed class Arm { public List<double> SList = new(), UList = new(), LList = new(); public string Label; public double Mean, Median, P95, Max, WallMean; public int Levies, Proj, Ticks; public double Hits; public long Skips; public List<PerfProbe.Row> Rows; }
		private static readonly List<Arm> Arms = new();
		private static bool _prof;

		private static Arm RunArm(string label, int ticks, int warm, int flood, float dx, bool through, int plantera = 0)
		{
			_floodTarget = flood; _floodDx = dx; _floodThrough = through; FloodIdx.Clear();
			_keepNpc.Clear(); NPC boss = null;
			if (plantera > 0) { _keepNpc.Add(NPCID.Plantera); _keepNpc.Add(NPCID.PlanterasHook); _keepNpc.Add(NPCID.PlanterasTentacle); _jungle = true; Main.hardMode = true; }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			ClearHostiles(true);
			if (plantera > 0)
			{
				boss = NPC.NewNPCDirect(new EntitySource_WorldEvent("PerfBoss"), new Vector2(_armyHi + 30f * 16f, (FloorY - 12) * 16f), NPCID.Plantera);
				if (boss != null) { boss.timeLeft = 999999; if (plantera >= 2) boss.life = boss.lifeMax / 3; }
			}
			var cpu = new List<double>(ticks); var cpuS = new List<double>(); var cpuU = new List<double>(); var cpuL = new List<double>(); double wall = 0; double lost = 0; int projSum = 0;
			PerfProbe.Reset(); if (LevyProf.On) LevyProf.Reset(); long skips0 = SoldierNPC.DormantSkips;
			for (int t = 0; t < warm + ticks; t++)
			{
				KeepCommander(); ClearHostiles(false); TopUp();
				if (t >= warm) foreach (var s in Army) if (s.NPC.active && s.NPC.life < s.NPC.lifeMax) lost += s.NPC.lifeMax - s.NPC.life;
				foreach (var s in Army) if (s.NPC.active) { s.NPC.life = s.NPC.lifeMax; if (s.Record != null) s.Record.CurrentHp = s.NPC.life; }
				if (boss != null && boss.active) boss.timeLeft = 999999;
				bool measure = t >= warm;
				if (measure && _prof) PerfProbe.On = true;
				long c0 = LevyProf.ThreadCpuNs(), w0 = Stopwatch.GetTimestamp();
				WorldStep.Update();
				long c1 = LevyProf.ThreadCpuNs(), w1 = Stopwatch.GetTimestamp();
				PerfProbe.On = false;
				if (measure)
				{
					double wms = (w1 - w0) * 1000.0 / Stopwatch.Frequency; wall += wms;
					cpu.Add(c0 >= 0 && c1 >= 0 ? (c1 - c0) / 1e6 : wms);
					if (PA.TimingOn) (PA.LastTickLevy ? cpuL : PA.LastTickSampled ? cpuS : cpuU).Add(cpu[cpu.Count - 1]);
					int pj = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].hostile) pj++; projSum += pj;
				}
			}
			LastTicks = cpu;
			var sorted = cpu.OrderBy(x => x).ToList();
			var a = new Arm
			{
				Label = label, Ticks = ticks, Levies = Army.Count(s => s.NPC.active), Mean = cpu.Average(), Median = sorted[sorted.Count / 2], P95 = sorted[(int)(sorted.Count * 0.95)], Max = sorted[sorted.Count - 1],
				WallMean = wall / ticks, Proj = projSum / Math.Max(1, ticks), Hits = lost / ticks,
				Rows = _prof ? PerfProbe.Rows(ticks) : null,
			};
			a.Skips = SoldierNPC.DormantSkips - skips0; a.SList = cpuS; a.UList = cpuU; a.LList = cpuL;
			Arms.Add(a);
			_mod.Logger.Info($"[PerfBoss] ARM {label}: levies {a.Levies}, hostile projectiles/tick {a.Proj}, levy HP lost/tick {a.Hits:F1}; thread CPU ms/tick mean {a.Mean:F3} median {a.Median:F3} p95 {a.P95:F3} max {a.Max:F2} (wall mean {a.WallMean:F3})");
			if (a.Rows != null)
			{
				int shown = 0;
				foreach (var r in a.Rows) { if (shown++ >= (int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFBOSS_TOP"), out int tp) ? tp : 30)) break; _mod.Logger.Info($"[PerfBoss]   {label} | {r.Name,-52} excl {r.ExclMs,8:F3} ms/tick  incl {r.InclMs,8:F3}  calls/tick {r.CallsPerTick,9:F1}"); }
			}
			_mod.Logger.Info($"[PerfBoss]   {label}: dormancy sleeps {SoldierNPC.DormantSleeps} wakes {SoldierNPC.DormantWakes} gear frames skipped {SoldierNPC.DormantSkips}; not-calm reasons (levy-ticks, cumulative) {string.Join(",", SoldierNPC.DormantWhy)}");
			if (LevyProf.On) _mod.Logger.Info($"[PerfBoss]   {label} LevyProf (ms/tick): {LevyProf.Dump(ticks + warm)}");
			if (boss != null) { _mod.Logger.Info($"[PerfBoss]   {label}: Plantera alive={boss.active} life={boss.life}/{boss.lifeMax}"); boss.active = false; }
			_floodTarget = 0; _keepNpc.Clear();
			return a;
		}

		/// <summary>
		/// The cost of the always-on analyzer. The scene is the army of the arms above with the hostile projectile flood over it (about 650 entities a tick). Rounds alternate an arm with the analyzer attached and one without
		/// (the order flips every round so a box that speeds up or slows down cannot favour one side); each arm is timed in thread CPU time per tick. What is measured is the work the analyzer adds to a tick: its detours on the
		/// update, on every NPC and on every projectile, the sampled ticks (one in eleven: clock reads around every NPC and projectile, Levy's own bucket timers), and the army-shot tests. What it cannot measure: the draw hooks (nothing is drawn here,
		/// they cost two clock reads per frame piece, a few microseconds a frame), and the window close (timed on its own and spread over the 480 ticks of a window).
		/// </summary>
		private static void AnalyzerArms(int ticks, int flood)
		{
			int rounds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFAN_ROUNDS"), out int rr) && rr > 0 ? rr : 12;
			AnalyzerArmsOne(ticks, flood, rounds, 0);
			if (Environment.GetEnvironmentVariable("LEVY_PERFAN_AB") == "1") try { AnalyzerWrapAB(ticks, flood, int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFAN_ABROUNDS"), out int ar) && ar > 0 ? ar : 24); } catch (Exception e) { Note("wrap A/B did not run: " + e.Message); }
		}

		/// <summary>
		/// Same boot, the analyzer split into its pieces, one arm each, in an order rotated every round so a box that speeds up or slows down cannot favour one arm. Every arm is timed in thread CPU time per tick and compared with arm 0
		/// of the same round (paired differences, standard error over the rounds). Arms: (0) analyzer off, nothing attached; (1) the entity IL edits only (NPC.UpdateNPC, Projectile.Update); (2) the Main detours only, no sampling;
		/// (3) the Main detours with the sampling and bookkeeping, no entity IL; (4) everything the analyzer does, global hook wraps included.
		/// </summary>
		private static void AnalyzerWrapAB(int ticks, int flood, int rounds)
		{
			string tmp = Path.Combine(Path.GetTempPath(), "levy_perfan_ab_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			PA.TestServer = true; PA.TestLogDir = tmp; PA.TestWindowSecs = 1e9;
			const int N = 5;
			var per = new List<double>[N]; var d = new List<double>[N]; var dt = new List<double>[N];
			for (int i = 0; i < N; i++) { per[i] = new List<double>(); d[i] = new List<double>(); dt[i] = new List<double>(); }
			string[] names = { "off", "entity IL only", "Main detours only (no sampling)", "Main detours + sampling (no entity IL)", "everything on" };
			// LEVY_PERFAN_ABARMS="0,4" times only those arms (arm 0 always included): twice the rounds in the same time
			var sel = new List<int> { 0 };
			foreach (string tok in (Environment.GetEnvironmentVariable("LEVY_PERFAN_ABARMS") ?? "0,1,2,3,4").Split(',', StringSplitOptions.RemoveEmptyEntries)) if (int.TryParse(tok, out int ai) && ai >= 0 && ai < N && !sel.Contains(ai)) sel.Add(ai);
			int warm = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFAN_ABWARM"), out int aw) && aw > 0 ? aw : 20;
			try
			{
				for (int r = 0; r < rounds; r++)
				{
					var m = new double[N]; var mt = new double[N];
					for (int k = 0; k < sel.Count; k++)
					{
						int arm = sel[(k + r) % sel.Count];
						PA.TestLog = false; PA.ConfigChanged();
						PA.TestSetGlobalWraps(arm == 4);
						switch (arm)
						{
							case 0: PA.TestPieces(false, false, true); break;
							case 1: PA.TestPieces(true, false, true); break;
							case 2: PA.TestPieces(false, true, true); break;
							case 3: PA.TestPieces(false, true, false); break;
							default: PA.TestPieces(true, true, false); break;
						}
						Arm a = RunArm("A/B " + names[arm], ticks, warm, flood, 0, false);
						m[arm] = a.Mean; mt[arm] = Trimmed(a.Ticks > 0 ? LastTicks : null);
					}
					foreach (int i in sel) { per[i].Add(m[i]); d[i].Add(m[i] - m[0]); dt[i].Add(mt[i] - mt[0]); }
				}
			}
			finally
			{
				PA.TestSetGlobalWraps(true); PA.TestNoSample = false; PA.TestLog = false; PA.ConfigChanged(); PA.TestServer = false; PA.TestWindowSecs = null; PA.TestLogDir = null;
				PA.TestResetLog(); PA.TestReset(); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception) { }
			}
			string Fmt(List<double> l) { double mu = l.Average(), sd = l.Count > 1 ? Math.Sqrt(l.Sum(x => (x - mu) * (x - mu)) / (l.Count - 1)) : 0; return $"{mu:F3} (s.e. {sd / Math.Sqrt(l.Count):F3}, median {l.OrderBy(x => x).ElementAt(l.Count / 2):F3})"; }
			Note($"split A/B, {rounds} rounds of {ticks} ticks (+{warm} warm-up), arms rotated each round. ms/tick: {string.Join("; ", sel.Select(i => names[i] + " " + Fmt(per[i])))}");
			Note($"split A/B, difference from arm 0 per round (ms/tick, mean of the ticks): {string.Join("; ", sel.Where(i => i > 0).Select(i => names[i] + " " + Fmt(d[i])))}");
			Note($"split A/B, difference from arm 0 per round (ms/tick, mean of the middle 80% of the ticks: spikes from the box cut): {string.Join("; ", sel.Where(i => i > 0).Select(i => names[i] + " " + Fmt(dt[i])))}");
			if (sel.Contains(2) && sel.Contains(3)) Note($"split A/B, sampling and bookkeeping alone (arm 3 minus arm 2): {Fmt(d[3].Zip(d[2], (x, y) => x - y).ToList())}");
			if (!sel.Contains(4)) return;
			A_BEnd = (d[4].Average(), Math.Sqrt(d[4].Sum(x => (x - d[4].Average()) * (x - d[4].Average())) / Math.Max(1, d[4].Count - 1)) / Math.Sqrt(d[4].Count));
			Check($"the analyzer fully on costs {A_BEnd.Item1:F3} ms/tick over fully off in the same boot (s.e. {A_BEnd.Item2:F3}, {rounds} paired rounds); the gate is under 0.1 ms plus twice the standard error", A_BEnd.Item1 < 0.1 + 2 * A_BEnd.Item2);
		}
		private static (double, double) A_BEnd;
		private static double Trimmed(List<double> l) { if (l == null || l.Count == 0) return 0; var s = l.OrderBy(x => x).ToList(); int cut = s.Count / 10; return s.Skip(cut).Take(s.Count - 2 * cut).Average(); }
		private static List<double> LastTicks;

		private static void AnalyzerArmsOne(int ticks, int flood, int rounds, int variant)
		{
			string tmp = Path.Combine(Path.GetTempPath(), "levy_perfan_cost_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			PA.TestServer = true; PA.TestLogDir = tmp; PA.TestWindowSecs = 1e9;
			var offTicks = new List<double>(); var diffs = new List<double>(); var uOn = new List<double>(); var sOn = new List<double>(); var lOn = new List<double>(); double closeMs = 0; string sample = null; double offMean = 0, onMean = 0; int offN = 0, onN = 0;
			try
			{
				for (int r = 0; r < rounds; r++)
				{
					double a = 0, b = 0;
					for (int half = 0; half < 2; half++)
					{
						bool attached = (half == 0) == (r % 2 == 0);
						PA.TestLog = attached; PA.ConfigChanged();
						Arm arm = RunArm(attached ? "analyzer ON" : "analyzer OFF", ticks, 30, flood, 0, false);
						if (attached) { b = arm.Mean; onMean += arm.Mean; onN++; uOn.AddRange(arm.UList); sOn.AddRange(arm.SList); lOn.AddRange(arm.LList); }
						else { a = arm.Mean; offMean += arm.Mean; offN++; offTicks.Add(arm.Mean); }
						if (attached && r == rounds - 1)
						{
							long t0 = Stopwatch.GetTimestamp(); var w = PA.TestClose(); closeMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
							if (w != null) sample = w.ToJson();
						}
					}
					diffs.Add(b - a);
				}
			}
			finally
			{
				PA.TestLog = false; PA.ConfigChanged(); PA.TestServer = false; PA.TestWindowSecs = null; PA.TestLogDir = null;
				PA.TestResetLog(); PA.TestReset(); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch (Exception) { }
			}
			offMean /= Math.Max(1, offN); onMean /= Math.Max(1, onN);
			double mean = diffs.Average(), sd = diffs.Count > 1 ? Math.Sqrt(diffs.Sum(d => (d - mean) * (d - mean)) / (diffs.Count - 1)) : 0, se = sd / Math.Sqrt(diffs.Count);
			var sorted = diffs.OrderBy(d => d).ToList(); double median = sorted[sorted.Count / 2];
			double perTickClose = closeMs / 480.0; int levies = Army.Count(s => s.NPC.active); double analyticTotal = 1e9;
			// the same number from the ticks themselves: the analyzer costs a little on every tick (its detours), more on the one tick in eleven it samples, and more again on the one in 66 that also times Levy's own buckets
			double Mean(List<double> l) => l.Count > 0 ? l.Average() : 0;
			double Sem(List<double> l) { if (l.Count < 2) return 0; double m = l.Average(); return Math.Sqrt(l.Sum(x => (x - m) * (x - m)) / (l.Count - 1)) / Math.Sqrt(l.Count); }
			double u = Mean(uOn), s1 = Mean(sOn), l1 = Mean(lOn);
			double perTickDetour = u - offMean, extraS = s1 - u, extraL = l1 - u;
			double pS = (1.0 / PA.SampleEvery) - (1.0 / PA.LevySampleEvery), pL = 1.0 / PA.LevySampleEvery;
			double est = perTickDetour + extraS * pS + extraL * pL;
			double estSe = Math.Sqrt(Math.Pow(Sem(uOn), 2) + Math.Pow(pS * Math.Sqrt(Math.Pow(Sem(sOn), 2) + Math.Pow(Sem(uOn), 2)), 2) + Math.Pow(pL * Math.Sqrt(Math.Pow(Sem(lOn), 2) + Math.Pow(Sem(uOn), 2)), 2)) + 0.0;
			Note($"analyzer cost (variant {variant}) over {rounds} alternating rounds of {ticks} ticks with {levies} levies and {flood} hostile projectiles: off mean {offMean:F3} ms/tick, on mean {onMean:F3} ms/tick; on minus off per round [{string.Join(", ", diffs.Select(d => d.ToString("F3")))}] mean {mean:F3} (s.e. {se:F3}), median {median:F3} ms/tick");
			Note($"analyzer cost by kind of tick (thread CPU ms/tick): unsampled ticks {u:F3} (n {uOn.Count}), sampled ticks {s1:F3} (n {sOn.Count}), sampled ticks that also time Levy's buckets {l1:F3} (n {lOn.Count}); with the analyzer off {offMean:F3}. The detours add {perTickDetour:F3} to every tick, sampling adds {extraS:F3} to one tick in {PA.SampleEvery}, Levy's buckets add {extraL:F3} to one in {PA.LevySampleEvery}: {est:F3} ms/tick on average (s.e. about {estSe:F3})");
			// the analyzer's own code, call by call, on this scene's real NPC and projectile: deterministic where the end-to-end numbers above carry the box's noise
			double[] mb = null; NPC bn = null; Projectile bp = null;
			for (int i = 0; i < Main.maxNPCs && bn == null; i++) if (Main.npc[i].active) bn = Main.npc[i];
			for (int i = 0; i < Main.maxProjectiles && bp == null; i++) if (Main.projectile[i].active) bp = Main.projectile[i];
			if (bn == null) bn = Main.npc[0]; if (bp == null) bp = Main.projectile[0];
			bool wasT = PA.TestServer; PA.TestServer = true; PA.TestLog = true; PA.ConfigChanged(); bool wasActive = bn.active; bn.active = true;
			try { mb = PA.MicroBench(bn, bp); } finally { bn.active = wasActive; PA.TestLog = false; PA.ConfigChanged(); PA.TestServer = wasT; }
			if (mb != null)
			{
				int activeNpc = 0, activeProj = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) activeNpc++; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) activeProj++;
				double hookCalls = 1435; // wrapped global AI hook calls a tick in this scene (counted in the PerfAnalyzer world check: about 400000 over 280 ticks)
				double unsampledUs = (Main.maxNPCs * mb[0] + Main.maxProjectiles * mb[1] + hookCalls * mb[2]) / 1000.0;
				double sampledExtraUs = (activeNpc * Math.Max(0, mb[3] - mb[0]) + activeProj * Math.Max(0, mb[4] - mb[1]) + hookCalls * Math.Max(0, mb[5] - mb[2])) / 1000.0;
				double levyExtraMs = extraL; // measured: Levy's own bucket timers, on the one tick in 66
				double analytic = unsampledUs / 1000.0 + sampledExtraUs / 1000.0 / PA.SampleEvery + Math.Max(0, levyExtraMs) / PA.LevySampleEvery + 0.0002;
				Note($"analyzer own code, ns per enter+exit pair: NPC {mb[0]:F1} (sampled {mb[3]:F1}), projectile {mb[1]:F1} (sampled {mb[4]:F1}), wrapped global hook {mb[2]:F1} (sampled {mb[5]:F1}), one clock read {mb[6]:F1}. This scene: {Main.maxNPCs} NPC slots ({activeNpc} active), {Main.maxProjectiles} projectile slots ({activeProj} active), about {hookCalls:F0} global hook calls a tick: {unsampledUs:F1} us on every tick, {sampledExtraUs:F1} us more on one tick in {PA.SampleEvery}, Levy's bucket timers {levyExtraMs * 1000:F0} us more on one in {PA.LevySampleEvery}: {analytic * 1000:F1} us a tick on average, plus the window close {perTickClose * 1000:F1} us");
				analyticTotal = analytic + perTickClose;
			}
			Note($"analyzer window close: {closeMs:F3} ms once every eight seconds = {perTickClose * 1000:F1} us per tick; total {est + perTickClose:F3} ms/tick against the 0.1 ms target ({(est + perTickClose) / Math.Max(0.001, offMean) * 100:F1}% of the tick); hook calls timed so far: NPC {PA.NpcCalls}, projectile {PA.ProjCalls}");
			if (sample != null && variant == 0) Note("a window from the loaded scene: " + sample);
			if (variant != 0) return;
			// gate: the call-by-call cost under 0.1 ms. The end-to-end on/off difference is OBSERVED only (a short run on a shared box flips randomly, +-1 ms and more); it is a PASS/FAIL check only in the LEVY_PERFAN_AB=1 split A/B
			Note($"analyzer end-to-end on minus off: {mean * 1000:F0} +- {se * 1000:F0} us per tick from {rounds} rounds (information only; the pass/fail end-to-end check runs with LEVY_PERFAN_AB=1)");
			Check($"the analyzer's own code adds under 0.1 ms per tick with {levies} levies and {flood} projectiles on the field ({analyticTotal * 1000:F0} us from timing its calls one by one, window close {perTickClose * 1000:F0} us)", analyticTotal < 0.1);
		}

		private static void Run()
		{
			int levies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFBOSS_LEVIES"), out int lv) && lv > 0 ? lv : 150;
			int flood = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFBOSS_PROJ"), out int fl) && fl > 0 ? fl : 500;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFBOSS_TICKS"), out int tk) && tk > 0 ? tk : (_lab ? 240 : 150);
			string only = Environment.GetEnvironmentVariable("LEVY_PERFBOSS_ARMS") ?? "all";
			bool Want(string k) => only == "all" || Array.IndexOf(only.Split(','), k) >= 0;
			_prof = Environment.GetEnvironmentVariable("LEVY_PERFBOSS_PROF") == "1";
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f); Main.player[0].position = _cmdAt;
			Main.GameMode = GameModeID.Normal; Main.hardMode = true;
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.Plantera, true);
			if (_prof)
			{
				PerfProbe.InstallPhases();
				PerfProbe.Install(_mod, (Environment.GetEnvironmentVariable("LEVY_PERFBOSS_DEEP") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries));
				Note($"profiler: {PerfProbe.Wrapped} methods instrumented ({PerfProbe.WrapFailed} failed)");
			}

			// ---- controls: the same fields with no army
			SetSpan(levies);
			Arm c0 = null, c1 = null, c2 = null, c3 = null;
			if (Want("control"))
			{
				c0 = RunArm("control quiet", ticks, 40, 0, 0, false);
				c1 = RunArm("control flood over", ticks, 40, flood, 0, false);
				c2 = RunArm("control flood through", ticks, 40, flood, 0, true);
				if (Want("plantera")) c3 = RunArm("control plantera", ticks, 40, 0, 0, false, 1);
			}

			// ---- the army, idle
			int made = SpawnArmy(levies);
			Check($"{levies} levy bodies stand in the arena ({made})", made >= levies * 0.95);
			for (int i = 0; i < 420; i++) { KeepCommander(); ClearHostiles(true); WorldStep.Update(); } // standing around: long enough for a levy to settle
			bool ab = Environment.GetEnvironmentVariable("LEVY_PERFBOSS_AB") == "1"; // lab: every army arm is also timed with this fix set off, in the same boot, just before
			bool hostileWas = global::Levy.Common.Weapons.PairCull.HostileOn, dormWas = SoldierNPC.DormancyOn;
			void Fixes(bool on) { global::Levy.Common.Weapons.PairCull.HostileOn = on && hostileWas; SoldierNPC.DormancyOn = on && dormWas; }
			Arm lastBefore = null;
			Arm Pair(string label, int flood0, float dx, bool through, int plantera, bool vs = false)
			{
				lastBefore = null;
				if (ab || vs) { Fixes(false); lastBefore = RunArm(label + " [before]", ticks, 60, flood0, dx, through, plantera); }
				Fixes(true);
				return RunArm(label, ticks, 60, flood0, dx, through, plantera);
			}
			Arm b0 = null, b1 = null, b3 = null;
			Arm a0 = null, a1 = null, a2 = null, a3 = null, a4 = null, a5 = null;
			try
			{
				if (Want("quiet")) { a0 = Pair("army idle, quiet field", 0, 0, false, 0, true); b0 = lastBefore; }
				if (Want("over")) { a1 = Pair("army idle, flood over the army", flood, 0, false, 0, true); b1 = lastBefore; }
				if (Want("far")) a2 = Pair("army idle, flood 700 tiles away", flood, 700, false, 0);
				if (Want("through")) { a3 = Pair("army idle, flood through the army", flood, 0, true, 0, true); b3 = lastBefore; }
				if (Environment.GetEnvironmentVariable("LEVY_PERFBOSS_SWEEP") == "1")
					foreach (int d in new[] { 60, 150, 300 }) Pair($"army idle, flood {d} tiles off", flood, d, false, 0);
				if (Want("plantera"))
				{
					a4 = Pair("army idle, Plantera at the edge", 0, 0, false, 1);
					_order = SoldierOrder.FreeRein; foreach (var s in Army) if (s.NPC.active && s.Record != null) s.Record.Order = _order;
					a5 = Pair("army engaged, Plantera at the edge", 0, 0, false, 2);
				}
			}
			finally { global::Levy.Common.Weapons.PairCull.HostileOn = hostileWas; SoldierNPC.DormancyOn = dormWas; }

			// ---- the always-on performance analyzer's own cost: the same army and the same flood, timed with the analyzer attached and removed in turn
			if (Want("analyzer")) { try { AnalyzerArms(ticks, flood); } catch (Exception e) { Check("analyzer arms threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); } }

			// ---- verdicts: what the army adds, per levy
			string Add(string what, Arm a, Arm c)
			{
				if (a == null || c == null) return null;
				double add = a.Mean - c.Mean, us = add * 1000.0 / Math.Max(1, a.Levies);
				Note($"{what}: army {a.Mean:F3} ms/tick vs control {c.Mean:F3} -> the army adds {add:F3} ms/tick = {us:F1} us per levy per tick ({a.Levies} levies)");
				return what;
			}
			double PerLevy(Arm a, Arm c) => (a.Mean - c.Mean) * 1000.0 / Math.Max(1, a.Levies);
			Add("quiet field", a0, c0); Add("flood over the army", a1, c1); Add("flood far away", a2, c1); Add("flood through the army", a3, c2); Add("Plantera, army idle", a4, c3); Add("Plantera, army engaged", a5, c3);
			// The verdicts compare the army's added cost with the fixes ON against the same scene timed a moment earlier in this boot with them OFF, so a loaded box (which inflates both) cannot fail them. The absolute
			// microseconds per levy above are information only. A fix switched off by its env var (LEVY_PAIRCULL_HOSTILE=0, LEVY_GEARDORMANT=0) is not asked for.
			bool cullOn = hostileWas, dormOn = dormWas;
			void Versus(string what, Arm on, Arm off, Arm ctl, bool wanted, double maxRatio)
			{
				if (on == null || off == null || ctl == null || !wanted) return;
				double addOn = Math.Max(0.001, on.Mean - ctl.Mean), addOff = Math.Max(0.001, off.Mean - ctl.Mean);
				Check($"{what}: the army adds {addOn:F2} ms/tick with the fixes on against {addOff:F2} ms/tick with them off (ratio {addOn / addOff:F2}, must be under {maxRatio:F2}; {addOn * 1000.0 / Math.Max(1, on.Levies):F0} vs {addOff * 1000.0 / Math.Max(1, on.Levies):F0} us per levy)", addOn <= addOff * maxRatio);
			}
			// the quiet field is timed for information; the verdict on it is that calm levies really did go dormant (counted, so box load cannot move it)
			if (a0 != null && dormOn) Check($"a calm army goes dormant on a quiet field: {a0.Skips} gear frames skipped over {a0.Ticks + 60} ticks ({a0.Mean - (c0?.Mean ?? 0):F2} ms/tick added; {(b0 != null ? (b0.Mean - (c0?.Mean ?? 0)).ToString("F2") : "n/a")} with the fix off)", a0.Skips > levies * (a0.Ticks + 60) * 0.05);
			Versus($"an idle army under a flood of {flood} hostile projectiles", a1, b1, c1, cullOn, 0.5);
			Versus($"an army taking a flood of {flood} hostile projectiles", a3, b3, c2, cullOn, 0.5);
			if (Arms.Count > 0) Check("every arm ticked", Arms.All(a => a.Ticks > 0));
		}
	}
}
