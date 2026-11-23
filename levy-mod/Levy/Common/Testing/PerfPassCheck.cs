using System;
using System.Collections.Generic;
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
	/// THE PERF-PASS GUARD ("[PerfPass]" lines; perf pass 11-22). A fighting army costs what it cost after the perf pass, and fights the same.
	/// <para/>Scene: a flat arena, the commander in slot 0, <see cref="ArmySize"/> levies (a ranger, a mage, a swordsman and a summoner kit in turn, proc accessories, and with Calamity or Thorium loaded two modded accessories
	/// each, so the per-tick gear frame runs), a pinned boss-flagged target in front of them that never dies, and a stream of hostile projectiles over the army (the threat field, the dodge solver and the hostile hit tests run).
	/// A CONTROL arm runs the same field with no army; the ARMY arm lets the army loose for <see cref="Warm"/> ticks and then measures two halves of <see cref="Half"/> ticks each. Every tick is timed in thread CPU time.
	/// <para/>PASS rules: the army's added cost (army arm minus control, ms per tick) stays under <see cref="CeilMs"/> (generous: about ten times what it measures, so a loaded box cannot fail it, an order-of-magnitude regression does);
	/// every levy acquires the target; shots fired and hits landed per levy-second are inside the reference band measured on the build before the perf pass (<see cref="RefShots"/>, <see cref="RefHits"/>: 0.7x to 1.43x; the scene repeats to within 1% on one build, the band leaves room for the wall-clock search budget on a loaded box);
	/// and the two halves agree with each other within noise (0.6x to 1.67x), so a change that made the army stop fighting halfway is caught. The numbers are logged as OBSERVED.
	/// Env: with LEVY_SELFCHECK_WORLD=1 it runs in the world-check chain (LEVY_PERFPASSWORLD=0 skips it); LEVY_PERFPASS=1 runs it alone, a few ticks after a world loads.
	/// </summary>
	public sealed class PerfPassCheck : ModSystem
	{
		private const int ArmySize = 24, Warm = 180, Half = 360, ControlTicks = 240, FloorY = 300, Cx = 1800, HalfWidth = 120, FloodN = 250;
		/// <summary>Ceiling of the army's added cost, thread CPU ms per tick, for 24 levies (measured 2-5 ms on the lab box).</summary>
		private const double CeilMs = 40.0;
		/// <summary>Reference rates from the build before the perf pass (same scene; see <c>lab/perfpass/README.md</c>): shots made and target hits per levy per second, vanilla and Calamity halves.</summary>
		private static double RefShots => ModLoader.HasMod("CalamityMod") ? RefShotsCal : RefShotsVan;
		private static double RefHits => ModLoader.HasMod("CalamityMod") ? RefHitsCal : RefHitsVan;
		internal static double RefShotsVan = 5.14, RefShotsCal = 5.26, RefHitsVan = 2.00, RefHitsCal = 2.00; // measured 11-22 on the pre-pass build (244c881, product code of 57c5a09), two runs per half: shots 5.156/5.132 and 5.271/5.257, hits 2.003/1.993 and 2.007/1.993
		private const string Commander = "PerfPassTester";

		private static bool _armed, _done, _running, _lab; private static int _calls, _pass, _fail; private static Mod _mod; private static int _savedMy;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			_lab = Environment.GetEnvironmentVariable("LEVY_PERFPASS") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_PERFPASSWORLD") != "0";
			if (!_lab && !ship) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (_lab) Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time; int cap = LevyConfig.Get?.RenderCap ?? 40;
			var rand = Main.rand;
			try { LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally
			{
				_running = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.hardMode = hm; Main.dayTime = day; Main.time = time; Main.rand = rand;
				try { if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = cap; } catch (Exception) { }
			}
			_mod.Logger.Info($"[PerfPass] self-check pass={_pass} fail={_fail}");
			_mod.Logger.Info("[PerfPass] done");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[PerfPass] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[PerfPass] OBSERVED " + s);

		// ------------------------------------------------------------------------------------------------ scene
		private static NPC _target; private static Vector2 _cmdAt;
		private static readonly List<SoldierNPC> Army = new();
		private static readonly List<int> Flood = new();
		private static readonly Random Rng = new(4242);

		private static readonly int[][] Kits =
		{
			new int[] { ItemID.Megashark, ItemID.HallowedHelmet, ItemID.HallowedPlateMail, ItemID.HallowedGreaves },
			new int[] { ItemID.CrystalStorm, ItemID.HallowedHeadgear, ItemID.HallowedPlateMail, ItemID.HallowedGreaves },
			new int[] { ItemID.NightsEdge, ItemID.TitaniumMask, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings },
			new int[] { ItemID.OpticStaff, ItemID.TitaniumHeadgear, ItemID.TitaniumBreastplate, ItemID.TitaniumLeggings },
		};
		private static readonly int[] Procs = { ItemID.StarVeil, ItemID.BeeCloak, ItemID.ObsidianShield, ItemID.BrainOfConfusion, ItemID.FrozenTurtleShell, ItemID.EyeoftheGolem };

		private static void Arena()
		{
			for (int i = Cx - HalfWidth - 10; i <= Cx + HalfWidth + 10; i++)
			{
				for (int j = FloorY - 90; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = FloorY; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander; Main.dayTime = true; Main.time = 27000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2; p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static List<int> Modded()
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
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Pass" + k,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			int[] kit = Kits[k % Kits.Length];
			rec.Gear.Weapon = new Item(); rec.Gear.Weapon.SetDefaults(kit[0]);
			rec.Gear.Head = new Item(); rec.Gear.Head.SetDefaults(kit[1]); rec.Gear.Body = new Item(); rec.Gear.Body.SetDefaults(kit[2]); rec.Gear.Legs = new Item(); rec.Gear.Legs.SetDefaults(kit[3]);
			if (k % Kits.Length == 0) { rec.Gear.Ammo = new Item(); rec.Gear.Ammo.SetDefaults(ItemID.HighVelocityBullet); rec.Gear.Ammo.stack = 999; }
			int a = 0;
			for (int i = 0; i < 3 && a < rec.Gear.Accessories.Length; i++) { var acc = new Item(); acc.SetDefaults(Procs[(k + i) % Procs.Length]); rec.Gear.Accessories[a++] = acc; }
			for (int i = 0; i < 2 && modded.Count > 0 && a < rec.Gear.Accessories.Length; i++) { var acc = new Item(); acc.SetDefaults(modded[(k + i) % modded.Count]); rec.Gear.Accessories[a++] = acc; }
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Hold; rec.HoldPos = at;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private static void SpawnTarget()
		{
			Vector2 at = new Vector2((Cx + 16) * 16f, (FloorY - 8) * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("PerfPassCheck"), (int)at.X, (int)at.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) throw new Exception("target did not spawn");
			_target = Main.npc[ti];
			_target.lifeMax = 5_000_000; _target.life = _target.lifeMax; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 64; _target.height = 96; _target.boss = true;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.Center = at;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position;
		}

		/// <summary>Hostile projectiles drifting over the army (Plantera's seeds), topped up every tick.</summary>
		private static void TopUp(bool on)
		{
			for (int i = Flood.Count - 1; i >= 0; i--) { Projectile p = Main.projectile[Flood[i]]; if (!p.active || p.localAI[2] != 7781f) Flood.RemoveAt(i); }
			if (!on) return;
			int made = 0;
			while (Flood.Count < FloodN && made < 40)
			{
				float x = (Cx - HalfWidth + (float)Rng.NextDouble() * 2 * HalfWidth) * 16f, y = (FloorY - 14f - (float)Rng.NextDouble() * 30f) * 16f;
				float ang = (float)(Rng.NextDouble() * Math.PI * 2); float spd = 1.5f + (float)Rng.NextDouble() * 3f;
				int keep = Main.myPlayer; Main.myPlayer = 0;
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("PerfPass"), new Vector2(x, y), new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * spd, ProjectileID.SeedPlantera, 30, 0f, 0);
				Main.myPlayer = keep;
				if (pi < 0 || pi >= Main.maxProjectiles) break;
				Projectile pr = Main.projectile[pi];
				pr.hostile = true; pr.friendly = false; pr.tileCollide = false; pr.timeLeft = 60 + Rng.Next(120); pr.localAI[2] = 7781f; pr.penetrate = -1;
				Flood.Add(pi); made++;
			}
		}

		private static void ClearHostiles()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (WorldStep.IsNaturalHostile(n) && !ReferenceEquals(n, _target)) n.active = false; }
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				Army.Clear(); Flood.Clear();
				if (_target != null) _target.active = false; _target = null;
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			}
			catch (Exception) { }
		}

		// ------------------------------------------------------------------------------------------------ the measured window
		private sealed class Tally { public double Cpu; public int Ticks; public long Shots, Hits, Dmg, TargetTicks, LevyTicks; public HashSet<int> Acquired = new(); }
		private static readonly int[] _seen = new int[Main.maxProjectiles];

		private static void StepOnce(Tally t, bool army, bool measure)
		{
			KeepCommander(); ClearHostiles(); TopUp(true);
			foreach (var s in Army) if (s.NPC.active) { s.NPC.life = s.NPC.lifeMax; if (s.Record != null) s.Record.CurrentHp = s.NPC.life; }
			int life0 = _target != null && _target.active ? _target.life : 0;
			long c0 = LevyProf.ThreadCpuNs(), w0 = System.Diagnostics.Stopwatch.GetTimestamp();
			WorldStep.Update();
			long c1 = LevyProf.ThreadCpuNs(), w1 = System.Diagnostics.Stopwatch.GetTimestamp();
			if (_target != null && _target.active)
			{
				int d = life0 - _target.life; if (d > 0 && measure) { t.Hits++; t.Dmg += d; }
				_target.life = _target.lifeMax;
			}
			if (!measure) return;
			t.Ticks++; t.Cpu += c0 >= 0 && c1 >= 0 ? (c1 - c0) / 1e6 : (w1 - w0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
			if (!army) return;
			foreach (var s in Army) { if (!s.NPC.active) continue; t.LevyTicks++; if (_target != null && s.TargetIndex == _target.whoAmI) { t.TargetTicks++; t.Acquired.Add(s.NPC.whoAmI); } }
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.hostile) { _seen[i] = 0; continue; }
				if (_seen[i] == p.identity + 1) continue;
				_seen[i] = p.identity + 1;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) t.Shots++;
			}
		}

		private static void Run()
		{
			Arena(); Cleanup();
			_cmdAt = new Vector2((Cx - HalfWidth + 6) * 16f, (FloorY - 3) * 16f);
			Main.hardMode = true; Main.rand = new Terraria.Utilities.UnifiedRandom(2211);
			KeepCommander(); SpawnTarget();
			// control: the same field and the same flood, no army
			var ctl = new Tally();
			for (int k = 0; k < 60 + ControlTicks; k++) StepOnce(ctl, false, k >= 60);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false; Flood.Clear();
			var modded = Modded();
			if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = Math.Max(LevyConfig.Get.RenderCap, ArmySize + 20);
			for (int k = 0; k < ArmySize; k++)
			{
				var s = Levy(k, new Vector2((Cx - 8 + (k % 12) * 1.5f) * 16f, FloorY * 16f), modded);
				if (s != null) Army.Add(s);
			}
			Check($"the army spawned ({Army.Count}/{ArmySize})", Army.Count == ArmySize);
			if (Army.Count == 0) return;
			Array.Clear(_seen, 0, _seen.Length);
			var warm = new Tally(); for (int k = 0; k < Warm; k++) StepOnce(warm, true, true);
			var a = new Tally(); for (int k = 0; k < Half; k++) StepOnce(a, true, true);
			var b = new Tally(); for (int k = 0; k < Half; k++) StepOnce(b, true, true);
			double ctlMs = ctl.Cpu / Math.Max(1, ctl.Ticks), armyMs = (a.Cpu + b.Cpu) / Math.Max(1, a.Ticks + b.Ticks), added = armyMs - ctlMs;
			double secs = (a.Ticks + b.Ticks) / 60.0, lv = Math.Max(1, Army.Count);
			double shots = (a.Shots + b.Shots) / lv / secs, hits = (a.Hits + b.Hits) / lv / secs;
			double ra = a.Shots / Math.Max(1.0, b.Shots), rh = a.Hits / Math.Max(1.0, b.Hits);
			double tgt = (a.TargetTicks + b.TargetTicks) / (double)Math.Max(1, a.LevyTicks + b.LevyTicks);
			var acquired = new HashSet<int>(a.Acquired); acquired.UnionWith(b.Acquired); acquired.UnionWith(warm.Acquired);
			Note($"{Army.Count} levies vs a pinned boss-flagged target, {FloodN} hostile seeds over them: thread CPU ms/tick control {ctlMs:F3}, army {armyMs:F3}, the army's added cost {added:F3} ({added / lv * 1000:F0} us per levy); "
				+ $"shots made per levy-second {shots:F3} (halves {a.Shots}/{b.Shots}), target hits per levy-second {hits:F3} (halves {a.Hits}/{b.Hits}), damage {a.Dmg + b.Dmg}, levy-ticks on the target {tgt:P0}, levies that acquired it {acquired.Count}/{Army.Count}; "
				+ $"reference shots {RefShots:F3}, hits {RefHits:F3} ({(ModLoader.HasMod("CalamityMod") ? "Calamity" : "vanilla")} half, 0 = not measured); box load {LoadAvg()}");
			Check($"the army's added cost stays under {CeilMs:F0} ms/tick of thread CPU ({added:F2} ms for {Army.Count} levies)", added < CeilMs);
			Check($"every levy acquires the target ({acquired.Count}/{Army.Count}) and the army spends most of the window on it ({tgt:P0} of levy-ticks, floor 50%)", acquired.Count == Army.Count && tgt >= 0.5);
			Check($"the army fires and hits (shots {a.Shots + b.Shots}, hits {a.Hits + b.Hits})", a.Shots + b.Shots > 0 && a.Hits + b.Hits > 0);
			Check($"the two halves fight alike (shots {a.Shots}/{b.Shots} = {ra:F2}, hits {a.Hits}/{b.Hits} = {rh:F2}; band 0.6 to 1.67)", ra >= 0.6 && ra <= 1.67 && rh >= 0.6 && rh <= 1.67);
			if (RefShots > 0) Check($"shots per levy-second match the pre-pass reference within noise ({shots:F3} against {RefShots:F3}; band 0.7x to 1.43x)", shots >= 0.7 * RefShots && shots <= 1.43 * RefShots);
			if (RefHits > 0) Check($"hits per levy-second match the pre-pass reference within noise ({hits:F3} against {RefHits:F3}; band 0.7x to 1.43x)", hits >= 0.7 * RefHits && hits <= 1.43 * RefHits);
		}

		private static string LoadAvg() { try { return System.IO.File.ReadAllText("/proc/loadavg").Split(' ').Take(3).Aggregate((x, y) => x + " " + y); } catch (Exception) { return "?"; } }
	}
}
