using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the combatfix pass, logged as "[CombatFix]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server, run from the summon chain right after the SCal arena check; LEVY_COMBATFIX_ONLY=1 runs it alone, with
	/// LEVY_COMBATFIX_PARTS=scal,config,regroup,walls,immune,spray picking parts). The Supreme Calamitas bullet-hell part lives in ScalArenaWorldCheck.CombatFix.cs (it needs her arena scene). Parts:
	/// <list type="bullet">
	/// <item>config: the one-time default change (a saved old default moves once, a later choice stays) and the new Chalice bleed-out default.</item>
	/// <item>regroup: Regroup &amp; Engage in a boss fight, leashed to the commander (the base is the commander; a levy that is not on the boss hops back beside them), against the earlier Free Rein (LEVY_REGROUPLEASH=0 behaviour).</item>
	/// <item>walls: a levy whose shots die on a wall near its target stops trusting the thin line and the cover logic repositions it.</item>
	/// <item>immune: a hit by an attack that shortens a player's immunity window (Nameless Deity's beam) shortens a levy's window the same.</item>
	/// <item>spray: Wrath of the Gods' Emptiness Spray never erases a levy and never carries the delete-the-player flag from a levy's puff, and works as a weapon.</item>
	/// </list>
	/// Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed class CombatFixWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800; private const string Commander = "CombatFixTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool only = Environment.GetEnvironmentVariable("LEVY_COMBATFIX_ONLY") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_COMBATFIX") != "0";
			if (!only && !ship) return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[CombatFix] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[CombatFix] OBSERVED " + s);
		private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
		private static bool Part(string name) { string p = Environment.GetEnvironmentVariable("LEVY_COMBATFIX_PARTS"); return string.IsNullOrEmpty(p) || p.Split(',').Contains(name); }

		/// <summary>Runs now when this boot armed it (the summon world check's chain calls this, after the SCal arena check).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			_running = true;
			bool sp = LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			try
			{
				LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player();
				foreach (var (name, fn) in new (string, Action)[] { ("config", ConfigPart), ("regroup", RegroupPart), ("walls", WallsPart), ("immune", ImmunePart), ("spray", SprayPart) })
				{
					if (!Part(name)) continue;
					try { fn(); }
					catch (Exception e) { Check($"{name} part threw {e.GetType().Name}: {e.Message} @ {string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))}", false); }
					finally { Cleanup(); }
				}
			}
			finally { _running = false; LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			var (sp1, sf1) = ScalArenaWorldCheck.CFTotals;
			_mod.Logger.Info($"[CombatFix] self-check pass={_pass + sp1} fail={_fail + sf1}");
		}

		// ---------------------------------------------------------------------------------------------- scene helpers
		private static long _pinDamage;
		private static readonly List<int> _pinned = new(); private static readonly List<Vector2> _pinAt = new();

		private static void Arena()
		{
			for (int i = Cx - 260; i <= Cx + 260; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 12; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static Vector2 _cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
		private static void KeepCommander()
		{
			Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander;
			if (p.statLifeMax < 5000) p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
			Main.dayTime = false; Main.time = 18000;
		}

		private static void Step(int n, Action<int> each = null)
		{
			for (int i = 0; i < n; i++)
			{
				KeepCommander(); each?.Invoke(i);
				for (int k = 0; k < _pinned.Count; k++) { NPC p = Main.npc[_pinned[k]]; if (p.active) { p.Center = _pinAt[k]; p.velocity = Vector2.Zero; _pinDamage += p.lifeMax - p.life; p.life = p.lifeMax; p.damage = 1; } }
				WorldStep.Update();
				for (int k = 0; k < Main.maxNPCs; k++) { NPC n2 = Main.npc[k]; if (WorldStep.IsNaturalHostile(n2) && !_pinned.Contains(k) && n2.ModNPC?.Mod?.Name != "NoxusBoss" && n2.ModNPC?.Mod?.Name != "CalamityMod") n2.active = false; }
			}
		}

		private static NPC PinNpc(int type, Vector2 at)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("CombatFix"), (int)at.X, (int)at.Y, type);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.lifeMax = n.life = int.MaxValue / 8; n.damage = 1; n.noGravity = true; n.noTileCollide = true; n.knockBackResist = 0f; n.timeLeft = 99999; n.Center = at;
			_pinned.Add(i); _pinAt.Add(at); return n;
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
					else if (!n.friendly && !n.townNPC) n.active = false;
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				_pinned.Clear(); _pinAt.Clear();
				SoldierNPC.BossFightForTest = null; SoldierNPC.HitDbg = null;
				SpecialImmunity.WindowOverride = null;
			}
			catch (Exception) { }
		}

		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		private static SoldierNPC Levy(int k, Vector2 at, SoldierOrder order, int weapon = ItemID.Handgun, int ammo = ItemID.MusketBall)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Fix" + k,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.TrySet(SoldierGear.SlotWeapon, Make(weapon), rec.AccessorySlots, out _);
			if (ammo > 0) { var a = Make(ammo); a.stack = 999; rec.Gear.TrySet(SoldierGear.SlotAmmo, a, rec.AccessorySlots, out _); }
			rec.LoadoutLocked = true; rec.Order = order;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			NPC n = Main.npc[body]; n.Bottom = at; n.lifeMax = n.life = 60000; rec.CurrentHp = 60000;
			return n.ModNPC as SoldierNPC;
		}

		// ---------------------------------------------------------------------------------------------- config
		private static void ConfigPart()
		{
			var cfg = LevyConfig.Get;
			var fresh = (LevyConfig)Activator.CreateInstance(typeof(LevyConfig));
			bool attr = typeof(LevyConfig).GetField(nameof(LevyConfig.SoldiersBleedOut)).GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()?.Value is true;
			Check("a fresh config has Chalice bleed-out on, and its declared default says so", fresh.SoldiersBleedOut && attr);
			string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "levy-cfgmig-" + Environment.ProcessId + ".txt");
			bool saved = cfg.SoldiersBleedOut; int saves = 0;
			try
			{
				if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp);
				ConfigMigrations.TestForget();
				cfg.SoldiersBleedOut = false;
				bool ch1 = ConfigMigrations.MoveOnce("test-chalice", () => !cfg.SoldiersBleedOut, () => cfg.SoldiersBleedOut = true, () => { saves++; return true; }, tmp);
				Check("a saved old default (off) moves to the new one (on) once and is saved", ch1 && cfg.SoldiersBleedOut && saves == 1);
				cfg.SoldiersBleedOut = false; // the player turns it off after the change
				bool ch2 = ConfigMigrations.MoveOnce("test-chalice", () => !cfg.SoldiersBleedOut, () => cfg.SoldiersBleedOut = true, () => { saves++; return true; }, tmp);
				Check("a later choice of off stays off: the step does not run twice", !ch2 && !cfg.SoldiersBleedOut && saves == 1);
				cfg.SoldiersBleedOut = true;
				bool ch3 = ConfigMigrations.MoveOnce("test-chalice-2", () => !cfg.SoldiersBleedOut, () => cfg.SoldiersBleedOut = true, () => { saves++; return true; }, tmp);
				Check("a value that is not the old default is left alone (and the step is recorded)", !ch3 && cfg.SoldiersBleedOut && saves == 1 && ConfigMigrations.IsDone("test-chalice-2", tmp));
			}
			finally { cfg.SoldiersBleedOut = saved; try { System.IO.File.Delete(tmp); } catch (Exception) { } }
		}

		// ---------------------------------------------------------------------------------------------- regroup
		private sealed class RgStat { public double MaxFromCmd, SumFromCmd; public long N; public int FarTicks; public int Snaps; public int HopsWithin; }

		/// <summary>One Regroup &amp; Engage fight: <paramref name="n"/> levies near the commander, a pinned boss to the right and a pinned add (not a boss) far to the left.</summary>
		private static RgStat RegroupRun(string label, bool leash, int n, int ticks, bool far)
		{
			Cleanup(); Arena();
			Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			SoldierNPC.RegroupLeashOn = leash; SoldierNPC.RegroupSnaps = 0;
			var levies = new List<SoldierNPC>();
			for (int k = 0; k < n; k++) { var s = Levy(k, new Vector2((Cx + (k - n / 2) * 3) * 16f, FloorY * 16f), SoldierOrder.Regroup); if (s != null) levies.Add(s); }
			KeepCommander(); Step(30);
			NPC boss = PinNpc(NPCID.KingSlime, new Vector2((Cx + 85) * 16f, (FloorY - 6) * 16f));
			NPC add = PinNpc(NPCID.BlueSlime, new Vector2((Cx - 150) * 16f, (FloorY - 2) * 16f));
			if (far) foreach (var s in levies.Take(Math.Max(1, n / 2))) { s.NPC.Bottom = new Vector2((Cx - 190) * 16f, FloorY * 16f); s.NPC.velocity = Vector2.Zero; }
			var st = new RgStat();
			Step(ticks, k =>
			{
				foreach (var s in levies)
				{
					if (!s.NPC.active) continue;
					double d = Vector2.Distance(s.NPC.Center, _cmdAt) / 16.0;
					st.N++; st.SumFromCmd += d; st.MaxFromCmd = Math.Max(st.MaxFromCmd, d); if (d > 70) st.FarTicks++;
				}
			});
			st.Snaps = SoldierNPC.RegroupSnaps;
			Note($"regroup [{label}] {levies.Count} levies, {ticks} ticks: mean distance from the commander {F(st.SumFromCmd / Math.Max(1, st.N))} tiles, max {F(st.MaxFromCmd)}, ticks beyond 70 tiles {st.FarTicks} of {st.N}, hops back {st.Snaps}; boss at 85 tiles, an add at 150 tiles the other way; leashed now {levies.Count(l => l.RegroupLeashed)}");
			foreach (var s in levies) s.NPC.active = false;
			SoldierNPC.RegroupLeashOn = true;
			return st;
		}

		private static void RegroupPart()
		{
			// no boss: unchanged
			{
				Cleanup(); Arena(); _cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
				var l = Levy(1, new Vector2((Cx + 10) * 16f, FloorY * 16f), SoldierOrder.Regroup);
				KeepCommander(); SoldierNPC.RegroupSnaps = 0; Step(600);
				Check("no boss fight: Regroup is Search & destroy, not leashed, and never hops", l != null && l.EffOrder == SoldierOrder.SearchDestroy && !l.RegroupLeashed && SoldierNPC.RegroupSnaps == 0);
			}
			var before = RegroupRun("BEFORE: the earlier Free Rein", false, 6, 1800, true);
			var now = RegroupRun("NOW: leashed to the commander", true, 6, 1800, true);
			Check($"NOW levies in a boss fight stay with the commander: max distance {F(now.MaxFromCmd)} tiles against {F(before.MaxFromCmd)} before; ticks beyond 70 tiles {now.FarTicks} against {before.FarTicks}", now.FarTicks <= before.FarTicks && now.FarTicks <= now.N / 20);
			Check($"NOW a levy that is not on the boss hops back beside the commander ({now.Snaps} hops; BEFORE {before.Snaps})", now.Snaps >= 1 && before.Snaps == 0);
			Check($"NOW the mean distance from the commander is inside the fight zone ({F(now.SumFromCmd / Math.Max(1, now.N))} tiles against {F(before.SumFromCmd / Math.Max(1, before.N))} before)", now.SumFromCmd / Math.Max(1, now.N) <= LevyConstants.Movement.FightZoneTiles);
		}

		// ---------------------------------------------------------------------------------------------- walls
		private static void WallsPart()
		{
			Cleanup(); Arena(); _cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			var l = Levy(2, new Vector2((Cx - 10) * 16f, FloorY * 16f), SoldierOrder.Follow);
			KeepCommander(); Step(30);
			// the mechanism: a levy whose shots die on a tile near its target stops trusting the thin line
			NPC t = PinNpc(NPCID.BlueSlime, new Vector2((Cx + 12) * 16f, (FloorY - 2) * 16f));
			l.TargetIndex = t.whoAmI;
			var pr = new Projectile(); pr.SetDefaults(ProjectileID.Bullet); pr.Center = t.Center + new Vector2(-30f, 0f);
			bool strict0 = l.WasteStrict;
			for (int i = 0; i < LevyConstants.WasteLimit - 1; i++) l.NoteShotHitTile(pr);
			bool strictMid = l.WasteStrict;
			l.NoteShotHitTile(pr);
			bool strictNow = l.WasteStrict;
			Check($"{LevyConstants.WasteLimit} shots dying on a wall beside the target make the levy distrust the thin line ({(strict0 ? "already strict" : "not strict")} at the start, {(strictMid ? "strict" : "not strict")} one short, {(strictNow ? "strict" : "not strict")} at the limit)", !strict0 && !strictMid && strictNow);
			// a corner-grazing line: thin ray passes, a shot-wide ray does not (found on the real tiles)
			bool found = false; Vector2 from = default, to = default; float gap = 0f;
			for (int ty = FloorY - 3; ty >= FloorY - 3 && !found; ty--) { }
			// a 1-tile pillar; a line from above-left to its right side that skims the pillar's top-left corner
			for (int j = FloorY - 4; j < FloorY; j++) { Tile pt = Main.tile[Cx - 40, j]; pt.ClearEverything(); pt.HasTile = true; pt.TileType = TileID.GrayBrick; pt.BlockType = BlockType.Solid; }
			for (float dy = -40f; dy <= 40f && !found; dy += 0.5f)
			{
				Vector2 a = new Vector2((Cx - 60) * 16f, (FloorY - 5) * 16f + dy), b = new Vector2((Cx - 30) * 16f, (FloorY - 5) * 16f - 6f);
				bool thin = Collision.CanHitLine(a, 1, 1, b, 1, 1), thick = Collision.CanHit(a - new Vector2(3f), 6, 6, b - new Vector2(3f), 6, 6);
				if (thin && !thick) { found = true; from = a; to = b; gap = dy; }
			}
			if (found)
			{
				bool beforeLine = l.LineClear(from, to);   // strict now: reads blocked
				l.TestResetWaste();
				bool afterLine = l.LineClear(from, to);    // thin again: reads clear
				Check($"a line that grazes a pillar's corner reads clear to the thin ray and blocked to a shot-wide ray ({(beforeLine ? "clear" : "blocked")} while strict, {(afterLine ? "clear" : "blocked")} after the window)", !beforeLine && afterLine);
			}
			else Note("walls: no corner-grazing line found on the test pillar; the line-width check is skipped");
			Note($"walls: shots counted {SoldierNPC.WasteNoted}, strict windows opened {SoldierNPC.WasteStrictOpened}");
			// the behaviour: a pillar, and a target whose centre a levy sees over the pillar's corner on the thin ray while a shot-wide ray is blocked
			var before = WallRun(false); var now = WallRun(true);
			if (before.found)
			{
				Note($"walls [BEFORE: thin line only] 900 ticks: shots that died on a tile near the target {before.waste}, damage the target took {before.dmg}, levy moved {F(before.moved)} tiles, strict windows {before.strict}");
				Note($"walls [NOW: wall-wise line] 900 ticks: shots that died on a tile near the target {now.waste}, damage the target took {now.dmg}, levy moved {F(now.moved)} tiles, strict windows {now.strict}");
				Note("walls: with a corner-grazing line a levy walks to another angle by itself (it moved " + F(now.moved) + " tiles) and no shot died on the wall; the strict line is the backstop for the case where shots do");
			}
			else Note("walls: no corner-grazing placement found for the behaviour scene");
			var eb = EmbedRun(false); var en = EmbedRun(true);
			Note($"walls, a 120x120 target pinned into a wall, 3 levies, 900 ticks [BEFORE]: shots fired {eb.shots}, died on a tile near it {eb.waste}, damage it took {eb.dmg}, a levy moved {F(eb.moved)} tiles, strict windows {eb.strict}");
			Note($"walls, a 120x120 target pinned into a wall, 3 levies, 900 ticks [NOW]: shots fired {en.shots}, died on a tile near it {en.waste}, damage it took {en.dmg}, a levy moved {F(en.moved)} tiles, strict windows {en.strict}");
			Check($"BEFORE, a target pressed into a wall is not shot at and no levy moves for it (shots {eb.shots}, damage {eb.dmg}, moved {F(eb.moved)} tiles): the check has teeth", eb.shots == 0 && eb.dmg == 0);
			Check($"NOW the levies shoot the part of it that shows: {en.dmg} damage, {en.waste} shots died on the wall near it (before {eb.shots} and {eb.dmg})", en.dmg > 0);
		}

		/// <summary>A big target pinned into a wall (its centre inside the rock, part of its hitbox in the open), as Supreme Calamitas's brothers (120x120, no tile collision) end up in the arena's tile walls.</summary>
		private static (long shots, long waste, long dmg, double moved, int strict) EmbedRun(bool on)
		{
			Cleanup(); Arena(); _cmdAt = new Vector2((Cx - 20) * 16f, (FloorY - 3) * 16f);
			global::Levy.Common.Weapons.Aiming.ExposeOn = on;
			int wx = Cx + 2;
			for (int i = wx; i < wx + 8; i++) for (int j = FloorY - 14; j < FloorY; j++) { Tile pt = Main.tile[i, j]; pt.ClearEverything(); pt.HasTile = true; pt.TileType = TileID.GrayBrick; pt.BlockType = BlockType.Solid; }
			var ls = new List<SoldierNPC>();
			for (int k = 0; k < 3; k++) { var l = Levy(10 + k, new Vector2((Cx - 22 + k * 2) * 16f, FloorY * 16f), SoldierOrder.Follow); if (l != null) ls.Add(l); }
			KeepCommander(); Step(40);
			NPC t = PinNpc(NPCID.KingSlime, new Vector2((wx + 1.5f) * 16f, (FloorY - 6) * 16f));
			t.noTileCollide = true; t.width = 120; t.height = 120; t.Center = _pinAt[_pinned.Count - 1] = new Vector2((wx + 1.5f) * 16f, (FloorY - 6) * 16f);
			long w0 = SoldierNPC.WasteNoted, s0 = SoldierNPC.WasteStrictOpened; _pinDamage = 0; double moved = 0; var last = ls.Select(x => x.NPC.Center).ToArray(); var seen = new HashSet<int>(); long shots = 0;
			Step(900, k =>
			{
				for (int i = 0; i < ls.Count; i++) { moved += Vector2.Distance(ls[i].NPC.Center, last[i]) / 16.0; last[i] = ls[i].NPC.Center; }
				for (int pj = 0; pj < Main.maxProjectiles; pj++) { Projectile pr = Main.projectile[pj]; if (pr.active && pr.friendly && pr.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tg) && tg.SoldierIndex >= 0 && seen.Add(pr.identity * 4096 + pj)) shots++; }
			});
			global::Levy.Common.Weapons.Aiming.ExposeOn = true;
			var res = (shots, SoldierNPC.WasteNoted - w0, _pinDamage, moved / Math.Max(1, ls.Count), (int)(SoldierNPC.WasteStrictOpened - s0));
			Cleanup(); return res;
		}

		private static (bool found, long waste, long dmg, double moved, long strict) WallRun(bool on)
		{
			Cleanup(); Arena(); _cmdAt = new Vector2((Cx - 20) * 16f, (FloorY - 3) * 16f);
			SoldierNPC.ShotWasteOnLab = on;
			int px = Cx - 8;
			for (int j = FloorY - 6; j < FloorY; j++) { Tile pt = Main.tile[px, j]; pt.ClearEverything(); pt.HasTile = true; pt.TileType = TileID.GrayBrick; pt.BlockType = BlockType.Solid; }
			var l = Levy(9, new Vector2((Cx - 22) * 16f, FloorY * 16f), SoldierOrder.Follow);
			KeepCommander(); Step(40);
			Vector2 muzzle = global::Levy.Common.Weapons.WeaponUse.Muzzle(l), start = l.NPC.Center;
			bool found = false; Vector2 at = default;
			for (int ty = FloorY - 12; ty <= FloorY - 6 && !found; ty++)
				for (float tx = Cx - 4; tx <= Cx + 6 && !found; tx += 0.5f)
				{
					Vector2 c = new Vector2(tx * 16f, ty * 16f + 8f);
					if (Collision.SolidCollision(c - new Vector2(4f), 8, 8)) continue;
					bool thin = Collision.CanHitLine(muzzle, 1, 1, c, 1, 1), thick = Collision.CanHit(muzzle - new Vector2(3f), 6, 6, c - new Vector2(3f), 6, 6);
					if (thin && !thick) { found = true; at = c; }
				}
			if (!found) { SoldierNPC.ShotWasteOnLab = true; return (false, 0, 0, 0, 0); }
			NPC t = PinNpc(NPCID.BlueSlime, at);
			long w0 = SoldierNPC.WasteNoted, s0 = SoldierNPC.WasteStrictOpened; _pinDamage = 0; double moved = 0; Vector2 last = l.NPC.Center;
			Step(900, k => { moved += Vector2.Distance(l.NPC.Center, last) / 16.0; last = l.NPC.Center; });
			SoldierNPC.ShotWasteOnLab = true;
			var res = (true, SoldierNPC.WasteNoted - w0, _pinDamage, moved, SoldierNPC.WasteStrictOpened - s0);
			Cleanup(); return res;
		}

		// ---------------------------------------------------------------------------------------------- immune
		private static void ImmunePart()
		{
			Cleanup(); Arena(); Main.GameMode = GameModeID.Master; _cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			var levy = Levy(3, new Vector2(Cx * 16f, FloorY * 16f), SoldierOrder.Hold);
			KeepCommander(); Step(10);
			if (levy == null) { Check("a levy to hit spawns", false); return; }
			const int Ticks = 300, Dmg = 40;
			int Run(Func<Projectile, int> over)
			{
				SpecialImmunity.WindowOverride = over;
				NPC body = levy.NPC; levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune);
				SoldierNPC.HitDbg = new Dictionary<string, (long dmg, int n)>();
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), body.Center, Vector2.Zero, ProjectileID.DeathLaser, Dmg, 0f, 255);
				Projectile pr = Main.projectile[pi]; pr.hostile = true; pr.friendly = false; pr.npcProj = true; pr.penetrate = -1; pr.tileCollide = false; pr.timeLeft = 5000; pr.usesLocalNPCImmunity = false; pr.usesIDStaticNPCImmunity = false;
				Step(Ticks, k =>
				{
					body.life = body.lifeMax; if (levy.Record != null) levy.Record.CurrentHp = body.life;
					pr.active = true; pr.Center = body.Center; pr.velocity = Vector2.Zero; pr.timeLeft = 5000; pr.hostile = true;
					int my = Main.myPlayer; Main.myPlayer = pr.owner; try { pr.Damage(); } finally { Main.myPlayer = my; }
				});
				int hits = 0; foreach (var kv in SoldierNPC.HitDbg) if (kv.Key.StartsWith("proj:")) hits += kv.Value.n;
				SoldierNPC.HitDbg = null; pr.active = false; SpecialImmunity.WindowOverride = null; return hits;
			}
			int ordinary = Run(null);
			int shortened = Run(_ => 6);
			Note($"immunity window: a levy standing in a pinned shot for {Ticks} ticks takes {ordinary} hits on the ordinary window and {shortened} when the shot's own window is 6 ticks (a player: about {Ticks / 6} on 6, about {Ticks / 40} on 40)");
			Check($"a window an attack shortens shortens the levy's too: {shortened} hits against {ordinary}", shortened >= ordinary * 3 && shortened >= Ticks / 6 / 2);
			// the real beam, when Wrath of the Gods is loaded
			if (ModLoader.TryGetMod("NoxusBoss", out Mod nox) && nox.TryFind("SuperCosmicBeam", out ModProjectile beamT))
			{
				int expect = ReadNameless(nox, "SuperCosmicLaserbeam_LaserPlayerIframes"); if (expect <= 0) expect = 6;
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), levy.NPC.Center, Vector2.UnitX, beamT.Type, 100, 0f, 255);
				Projectile beam = pi >= 0 && pi < Main.maxProjectiles ? Main.projectile[pi] : null;
				if (beam == null) { Check("the real super cosmic beam spawns", false); return; }
				Note("immune: " + SpecialImmunity.Diag(beam));
				long p0 = SpecialImmunity.Probes;
				int w = SpecialImmunity.Window(beam, levy.NPC, 100, 40);
				beam.active = false;
				Check($"the real super cosmic beam leaves a hit window of {w} ticks (a player's, from the mod: {expect}) instead of 40; probed {SpecialImmunity.Probes - p0}", w == expect);
				int ordinaryW = SpecialImmunity.Window(Main.projectile[Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), levy.NPC.Center, Vector2.Zero, ProjectileID.DeathLaser, 10, 0f, 255)], levy.NPC, 10, 40);
				Check("an ordinary projectile keeps the ordinary window (40)", ordinaryW == 40);
			}
			else Note("immune: Wrath of the Gods is not loaded: the real beam part is skipped");
		}

		private static int ReadNameless(Mod nox, string prop)
		{
			try { var p = nox.Code.GetType("NoxusBoss.Content.NPCs.Bosses.NamelessDeity.NamelessDeityBoss")?.GetProperty(prop, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); return p == null ? -1 : (int)p.GetValue(null); }
			catch (Exception) { return -1; }
		}

		// ---------------------------------------------------------------------------------------------- spray
		private static void SprayPart()
		{
			if (!ModLoader.TryGetMod("NoxusBoss", out Mod nox) || !nox.TryFind("EmptinessSprayerGas", out ModProjectile gas)) { Note("spray: Wrath of the Gods is not loaded: skipped"); return; }
			Cleanup(); Arena(); Main.GameMode = GameModeID.Expert; _cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			int soldierType = ModContent.NPCType<SoldierNPC>();
			Check($"every levy NPC type is in the sprayer's protected set ({EmptinessSprayGuard.Protected} added)", EmptinessSprayGuard.Protects(soldierType));
			// BEFORE / NOW: a puff over a levy and over a hostile slime
			bool Survives(bool guard)
			{
				EmptinessSprayGuard.SetProtectedForLab(guard);
				Cleanup(); Arena(); KeepCommander();
				var l = Levy(4, new Vector2(Cx * 16f, FloorY * 16f), SoldierOrder.Hold); Step(5);
				NPC slime = PinNpc(NPCID.BlueSlime, new Vector2((Cx + 40) * 16f, (FloorY - 2) * 16f));
				Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), l.NPC.Center, Vector2.Zero, gas.Type, 0, 0f, 0);
				Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), slime.Center, Vector2.Zero, gas.Type, 0, 0f, 0);
				Step(6);
				bool lv = l.NPC.active && l.NPC.type == soldierType, sl = slime.active;
				Note($"spray [{(guard ? "NOW, guard on" : "BEFORE, guard off")}]: the levy {(lv ? "stands" : "was ERASED")}; the hostile slime {(sl ? "stands" : "was erased")} (the spray erases a hostile NPC in its gas with no death and no loot)");
				EmptinessSprayGuard.SetProtectedForLab(true);
				return lv && !sl;
			}
			bool beforeOk = Survives(false); bool nowOk = Survives(true);
			Check("BEFORE the guard a levy in the gas is erased (the check has teeth)", !beforeOk);
			Check("NOW a levy in the gas stays, and the gas still erases a hostile slime", nowOk);
			// a levy's own puff never carries the reflect flag that deletes the player
			{
				Cleanup(); Arena(); KeepCommander();
				var l = Levy(5, new Vector2(Cx * 16f, FloorY * 16f), SoldierOrder.Hold);
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), l.NPC.Center + new Vector2(300f, 0f), Vector2.Zero, gas.Type, 0, 0f, 0);
				int pj = Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), l.NPC.Center + new Vector2(-300f, 0f), Vector2.Zero, gas.Type, 0, 0f, 0);
				if (pi >= 0 && pi < Main.maxProjectiles && pj >= 0 && pj < Main.maxProjectiles)
				{
					Projectile mine = Main.projectile[pi], hers = Main.projectile[pj];
					if (mine.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag)) tag.SoldierIndex = l.NPC.whoAmI;
					mine.ai[0] = 1f; hers.ai[0] = 1f; long c0 = EmptinessSprayGuard.ReflectCleared;
					Step(2);
					Check($"a levy's puff that was reflected has the delete-the-player flag cleared ({mine.ai[0]}, cleared {EmptinessSprayGuard.ReflectCleared - c0}); the commander's own puff keeps it ({hers.ai[0]})", mine.ai[0] == 0f && hers.ai[0] == 1f);
				}
			}
			// a levy's puff spares everything friendly (town NPCs, critters, other mods' machines), and still erases a hostile slime; the commander's own puff spares only what a player's spares
			{
				Cleanup(); Arena(); KeepCommander();
				var l = Levy(8, new Vector2(Cx * 16f, FloorY * 16f), SoldierOrder.Hold);
				Vector2 spot = l.NPC.Center + new Vector2(120f, 0f);
				int gi = NPC.NewNPC(new EntitySource_WorldEvent("CombatFix"), (int)spot.X, (int)spot.Y, NPCID.Guide); NPC guide = gi >= 0 && gi < Main.maxNPCs ? Main.npc[gi] : null;
				int bi = NPC.NewNPC(new EntitySource_WorldEvent("CombatFix"), (int)spot.X, (int)spot.Y, NPCID.Bunny); NPC bunny = bi >= 0 && bi < Main.maxNPCs ? Main.npc[bi] : null;
				NPC machine = null; string machineName = "none";
				foreach (string mn in new[] { "Mechanization", "Warbands" })
					if (machine == null && ModLoader.TryGetMod(mn, out Mod mm))
						foreach (ModNPC mnpc in mm.GetContent<ModNPC>())
						{
							if (mnpc is SoldierNPC) continue;
							int ti = NPC.NewNPC(new EntitySource_WorldEvent("CombatFix"), (int)spot.X, (int)spot.Y, mnpc.Type);
							if (ti >= 0 && ti < Main.maxNPCs && Main.npc[ti].active && (Main.npc[ti].friendly || Main.npc[ti].townNPC)) { machine = Main.npc[ti]; machineName = mn + "/" + mnpc.Name; break; }
							if (ti >= 0 && ti < Main.maxNPCs) Main.npc[ti].active = false;
						}
				NPC slime = PinNpc(NPCID.BlueSlime, spot);
				foreach (NPC f in new[] { guide, bunny, machine }) if (f != null) { f.Center = spot; f.noGravity = true; f.velocity = Vector2.Zero; }
				for (int k = 0; k < 1; k++)
				{
					int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("CombatFix"), spot, Vector2.Zero, gas.Type, 0, 0f, 0);
					if (pi >= 0 && pi < Main.maxProjectiles && Main.projectile[pi].TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tg)) tg.SoldierIndex = k == 0 ? l.NPC.whoAmI : -1;
				}
				long sp0 = EmptinessSprayGuardProjectile.FriendlySpared;
				// one puff, a levy's (tagged): step once and read what stands
				Step(1, k => { foreach (NPC f in new[] { guide, bunny, machine }) if (f != null) { f.Center = spot; f.velocity = Vector2.Zero; } });
				bool townOk = guide == null || guide.active, critterOk = bunny == null || bunny.active, machineOk = machine == null || machine.active;
				Note($"spray, friendly fire: a town NPC {(guide == null ? "(not spawned)" : townOk ? "stands" : "ERASED")}, a critter {(bunny == null ? "(not spawned)" : critterOk ? "stands" : "ERASED")}, a machine [{machineName}] {(machine == null ? "(none loaded)" : machineOk ? "stands" : "ERASED")}, the hostile slime {(slime.active ? "stands" : "erased")}; friendly NPCs spared {EmptinessSprayGuardProjectile.FriendlySpared - sp0}");
				Check("a levy's puff erases the hostile slime and leaves the town NPC, the critter and the other mod's machine standing", !slime.active && townOk && critterOk && machineOk);
			}
			// a levy using the spray as a weapon, like a player
			if (nox.TryFind("EmptinessSprayer", out ModItem sprayer))
			{
				Cleanup(); Arena(); KeepCommander();
				var l = Levy(6, new Vector2(Cx * 16f, FloorY * 16f), SoldierOrder.Follow, sprayer.Type, 0);
				Step(10);
				NPC slime = PinNpc(NPCID.BlueSlime, new Vector2((Cx + 10) * 16f, (FloorY - 2) * 16f));
				NPC friend = Levy(7, new Vector2((Cx - 6) * 16f, FloorY * 16f), SoldierOrder.Hold, ItemID.Handgun)?.NPC;
				var wp = global::Levy.Common.Weapons.WeaponClassifier.GetProfile(sprayer.Item);
				Note($"spray weapon profile: family {wp?.Family} supported {wp?.Supported} range {wp?.RangeTiles} tiles ranged {wp?.IsRanged}; the levy holds {(l.Weapon == null ? "nothing" : l.Weapon.Family.ToString())}; state {l.State}");
				int firstGas = -1;
				Step(900, k => { if (firstGas < 0) for (int p = 0; p < Main.maxProjectiles; p++) if (Main.projectile[p].active && Main.projectile[p].type == gas.Type) { firstGas = k; break; } });
				bool deleted = !slime.active; bool levyOk = l.NPC.active && (friend == null || friend.active);
				var delType = nox.Code.GetType("NoxusBoss.Core.Graphics.SpecificEffectManagers.EmptinessSprayPlayerDeletionSystem");
				bool playerGone = delType != null && (bool)(delType.GetProperty("PlayerWasDeleted")?.GetValue(null) ?? false);
				Note($"spray as a weapon: a levy holding the Emptiness Spray, a hostile slime 10 tiles away: first gas at tick {firstGas}, slime {(deleted ? "erased" : "still there")}, levies {(levyOk ? "untouched" : "LOST")}, player-deletion flag {playerGone}");
				Note($"spray weapon after 900 ticks: state {l.State}, target {l.TargetIndex}, why {l.StillReason()}");
				Check("a levy using the Emptiness Spray makes gas and clears the hostile NPC in it like a player, and no levy is lost", firstGas >= 0 && deleted && levyOk && !playerGone);
			}
			else Note("spray: the item was not found");
		}
	}
}
