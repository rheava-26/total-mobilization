using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	/// PASS rules: the army's added cost in the busy arms stays inside <see cref="BudgetPerLevyUs"/> microseconds per levy per tick. What it cannot show: draw cost (a headless server cannot draw).
	/// </summary>
	public class PerfBossCheck : ModSystem
	{
		/// <summary>Levy-added thread-CPU microseconds per levy per tick allowed in the flood arms (set from the measurement, see the check's PASS line).</summary>
		public const double BudgetPerLevyUs = 60.0;
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
		private sealed class Arm { public string Label; public double Mean, Median, P95, Max, WallMean; public int Levies, Proj, Ticks; public double Hits; public List<PerfProbe.Row> Rows; }
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
			var cpu = new List<double>(ticks); double wall = 0; double lost = 0; int projSum = 0;
			PerfProbe.Reset();
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
					int pj = 0; for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].hostile) pj++; projSum += pj;
				}
			}
			var sorted = cpu.OrderBy(x => x).ToList();
			var a = new Arm
			{
				Label = label, Ticks = ticks, Levies = Army.Count(s => s.NPC.active), Mean = cpu.Average(), Median = sorted[sorted.Count / 2], P95 = sorted[(int)(sorted.Count * 0.95)], Max = sorted[sorted.Count - 1],
				WallMean = wall / ticks, Proj = projSum / Math.Max(1, ticks), Hits = lost / ticks,
				Rows = _prof ? PerfProbe.Rows(ticks) : null,
			};
			Arms.Add(a);
			_mod.Logger.Info($"[PerfBoss] ARM {label}: levies {a.Levies}, hostile projectiles/tick {a.Proj}, levy HP lost/tick {a.Hits:F1}; thread CPU ms/tick mean {a.Mean:F3} median {a.Median:F3} p95 {a.P95:F3} max {a.Max:F2} (wall mean {a.WallMean:F3})");
			if (a.Rows != null)
			{
				int shown = 0;
				foreach (var r in a.Rows) { if (shown++ >= (int.TryParse(Environment.GetEnvironmentVariable("LEVY_PERFBOSS_TOP"), out int tp) ? tp : 30)) break; _mod.Logger.Info($"[PerfBoss]   {label} | {r.Name,-52} excl {r.ExclMs,8:F3} ms/tick  incl {r.InclMs,8:F3}  calls/tick {r.CallsPerTick,9:F1}"); }
			}
			if (boss != null) { _mod.Logger.Info($"[PerfBoss]   {label}: Plantera alive={boss.active} life={boss.life}/{boss.lifeMax}"); boss.active = false; }
			_floodTarget = 0; _keepNpc.Clear();
			return a;
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
			for (int i = 0; i < 90; i++) { KeepCommander(); ClearHostiles(true); WorldStep.Update(); }
			Arm a0 = Want("quiet") ? RunArm("army idle, quiet field", ticks, 60, 0, 0, false) : null;
			Arm a1 = Want("over") ? RunArm("army idle, flood over the army", ticks, 60, flood, 0, false) : null;
			Arm a2 = Want("far") ? RunArm("army idle, flood 700 tiles away", ticks, 60, flood, 700, false) : null;
			Arm a3 = Want("through") ? RunArm("army idle, flood through the army", ticks, 60, flood, 0, true) : null;
			if (Environment.GetEnvironmentVariable("LEVY_PERFBOSS_SWEEP") == "1")
				foreach (int d in new[] { 60, 150, 300 }) RunArm($"army idle, flood {d} tiles off", ticks, 60, flood, d, false);
			Arm a4 = null, a5 = null;
			if (Want("plantera"))
			{
				a4 = RunArm("army idle, Plantera at the edge", ticks, 60, 0, 0, false, 1);
				_order = SoldierOrder.FreeRein; foreach (var s in Army) if (s.NPC.active && s.Record != null) s.Record.Order = _order;
				a5 = RunArm("army engaged, Plantera at the edge", ticks, 60, 0, 0, false, 2);
			}

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
			if (a0 != null && c0 != null) Check($"an idle army on a quiet field adds under {BudgetPerLevyUs:F0} us per levy per tick ({PerLevy(a0, c0):F1})", PerLevy(a0, c0) <= BudgetPerLevyUs);
			if (a1 != null && c1 != null) Check($"an idle army under a flood of {flood} hostile projectiles adds under {BudgetPerLevyUs:F0} us per levy per tick ({PerLevy(a1, c1):F1})", PerLevy(a1, c1) <= BudgetPerLevyUs);
			if (a2 != null && c1 != null) Check($"a far-away flood costs the army no more than a near one ({PerLevy(a2, c1):F1} vs {(a1 != null ? PerLevy(a1, c1) : 0):F1} us per levy per tick)", a1 == null || PerLevy(a2, c1) <= PerLevy(a1, c1) + 10.0);
			if (a3 != null && c2 != null) Check($"an army taking a flood of {flood} hostile projectiles adds under {BudgetPerLevyUs * 2:F0} us per levy per tick ({PerLevy(a3, c2):F1})", PerLevy(a3, c2) <= BudgetPerLevyUs * 2);
			if (a4 != null && c3 != null) Check($"an idle army beside Plantera adds under {BudgetPerLevyUs * 2:F0} us per levy per tick ({PerLevy(a4, c3):F1})", PerLevy(a4, c3) <= BudgetPerLevyUs * 2);
			if (a5 != null && c3 != null) Check($"an engaged army beside Plantera adds under {BudgetPerLevyUs * 3:F0} us per levy per tick ({PerLevy(a5, c3):F1})", PerLevy(a5, c3) <= BudgetPerLevyUs * 3);
			if (Arms.Count > 0) Check("every arm ticked", Arms.All(a => a.Ticks > 0));
		}
	}
}
