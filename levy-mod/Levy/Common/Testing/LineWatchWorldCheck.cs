using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Recording;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE LINE-BREAKING WARNING, WITH A REAL ARMY ("[LineWatch]" lines; LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_LINEWATCH_ONLY=1 runs it alone). Twelve levies stand near the commander (slot 0 as the single-player commander) and an enemy
	/// flagged a boss stands forty tiles away, and the real game ticks run the real <see cref="LineWatch"/>.
	/// A steady fight (two minutes, nobody shaken, nobody falls) must not warn. A collapse by morale (eight of the twelve shaken for a few seconds) must warn exactly once and keep quiet through the rest of it. A collapse by casualties (six levies fall inside twenty
	/// seconds) must warn once in a fight of its own, but not while the one-a-minute pause from the last warning still stands, and not at all with the setting off. The warning is a message: the levies' states and orders are read before and after and must not differ.
	/// </summary>
	public class LineWatchWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800; private const string Commander = "LineTester";
		private static NPC _boss; private static Vector2 _bossAt, _cmdAt;
		private static readonly HashSet<int> Shake = new();

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_LINEWATCHWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0" && Environment.GetEnvironmentVariable("LEVY_LINEWATCH_ONLY") == "1") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		/// <summary>Holds the chosen levies in the shaken state after the NPCs have thought and before the watcher looks (the fear system would otherwise rally them).</summary>
		public override void PostUpdateNPCs()
		{
			if (!_running || Shake.Count == 0) return;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && Shake.Contains(n.whoAmI)) s.State = SoldierState.Shaken; }
		}
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int my0 = Main.myPlayer;
			try { LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; LevyUtil.SpEmulation = sp; Main.myPlayer = my0; LineWatch.TestClient = false; LineWatch.TestEnabled = null; LineWatch.Reset(true); Shake.Clear(); Cleanup(); }
			_mod.Logger.Info($"[LineWatch] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[LineWatch] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[LineWatch] OBSERVED " + s);

		private static void Arena()
		{
			for (int i = Cx - 150; i <= Cx + 150; i++)
			{
				for (int j = FloorY - 70; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } }
				if (_boss != null) { _boss.boss = false; _boss.active = false; } _boss = null;
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				if (RosterSystem.Instance != null) RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
			catch (Exception) { }
		}

		private static void SpawnBoss()
		{
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("LineWatchWorldCheck"), (int)_bossAt.X, (int)_bossAt.Y, NPCID.GreenSlime);
			_boss = Main.npc[ti];
			_boss.lifeMax = 100_000_000; _boss.life = _boss.lifeMax; _boss.defense = 0; _boss.damage = 0; _boss.knockBackResist = 0f; _boss.width = 64; _boss.height = 96;
			_boss.noGravity = true; _boss.noTileCollide = true; _boss.aiStyle = -1; _boss.friendly = false; _boss.value = 0f; _boss.boss = true; _boss.Bottom = _bossAt;
			var ht = _boss.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _boss.position;
		}

		private static SoldierNPC MakeLevy(Vector2 at, int n)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Line" + n, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int bi = RosterSystem.Instance.SpawnBody(rec, at);
			if (bi < 0 || !rec.HasBody) return null;
			Main.npc[bi].Bottom = at; Main.npc[bi].lifeMax = Main.npc[bi].life = 4_000_000; rec.CurrentHp = 4_000_000;
			return Main.npc[bi].ModNPC as SoldierNPC;
		}

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander;
				if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2;
				p.Bottom = _cmdAt; p.velocity = Vector2.Zero; p.fallStart = p.fallStart2 = (int)(_cmdAt.Y / 16f); p.noFallDmg = true; Main.dayTime = false; Main.time = 18000;
				if (_boss != null && _boss.active) { _boss.active = true; _boss.timeLeft = 9999; _boss.Bottom = _bossAt; _boss.velocity = Vector2.Zero; _boss.life = _boss.lifeMax; var ht = _boss.GetGlobalNPC<HarnessTarget>(); ht.Pin = _boss.position; }
				for (int k = 0; k < Main.maxNPCs; k++) { NPC e = Main.npc[k]; if (e.active && e != _boss && WorldStep.IsNaturalHostile(e)) e.active = false; }
				WorldStep.Update();
			}
		}

		private static string Snapshot(List<SoldierNPC> army)
		{
			var sb = new System.Text.StringBuilder();
			foreach (var s in army) sb.Append(s.NPC.active ? ((int)s.Record.Order).ToString() : "x").Append(',');
			return sb.ToString();
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena(); _cmdAt = new Vector2(Cx * 16f, FloorY * 16f); _bossAt = _cmdAt + new Vector2(40 * 16f, 0f);
			Main.rand = new Terraria.Utilities.UnifiedRandom(7100);
			LineWatch.TestClient = true; LineWatch.TestEnabled = null; LineWatch.Reset(true); LineWatch.Warnings = 0; LineWatch.Shown.Clear();
			var army = new List<SoldierNPC>();
			for (int i = 0; i < 12; i++) { var l = MakeLevy(_cmdAt + new Vector2((-4 + i) * 16f, 0f), i); if (l != null) army.Add(l); }
			Check($"the army spawns ({army.Count} levies)", army.Count == 12);
			SpawnBoss(); Step(120);

			// ---- 1. a steady fight
			string orders0 = Snapshot(army);
			Step(120 * 60);
			Check($"a steady two-minute fight does not warn ({LineWatch.Warnings} warnings)", LineWatch.Warnings == 0);

			// ---- 2. morale: eight of twelve shaken
			int w0 = LineWatch.Warnings;
			for (int i = 0; i < 8; i++) Shake.Add(army[i].NPC.whoAmI);
			Step(2 * 60);
			Check("two seconds of it is not yet a warning", LineWatch.Warnings == w0);
			Step(6 * 60);
			Check($"eight of twelve shaken for a few seconds warns ({LineWatch.Warnings - w0} warnings, {LineWatch.LastWhy})", LineWatch.Warnings == w0 + 1 && LineWatch.LastWhy == "morale");
			Check("the warning's words are the line breaking", LineWatch.Shown.Count == 1 && LineWatch.Shown[0] == "The line is breaking!");
			Step(30 * 60);
			Check($"it says nothing more through the rest of that fight ({LineWatch.Warnings - w0} in all)", LineWatch.Warnings == w0 + 1);
			Shake.Clear();
			string orders1 = Snapshot(army);
			Check("the warning changed nothing about the levies' orders (" + orders1 + ")", orders1 == orders0);

			// ---- 3. a second collapse inside the minute is not announced again; the fight is over when the boss has been gone ten seconds
			_boss.active = false; Step(12 * 60);
			SpawnBoss(); Step(60);
			for (int i = 0; i < 8; i++) Shake.Add(army[i].NPC.whoAmI);
			Step(8 * 60);
			Check($"a second collapse inside the minute does not warn again ({LineWatch.Warnings} in all)", LineWatch.Warnings == w0 + 1);
			Shake.Clear(); _boss.active = false; Step(12 * 60);

			// ---- 4. casualties: six fall inside twenty seconds, a minute after the last warning
			LineWatch.Reset(true); w0 = LineWatch.Warnings; SpawnBoss(); Step(5 * 60);
			for (int i = 0; i < 6; i++) { NPC n = army[i].NPC; n.life = 0; n.checkDead(); Step(3 * 60); }
			Step(2 * 60);
			Check($"six levies fallen inside twenty seconds warns ({LineWatch.Warnings - w0} warnings, {LineWatch.LastWhy})", LineWatch.Warnings == w0 + 1 && LineWatch.LastWhy == "attrition");
			_boss.active = false; Step(12 * 60);

			// ---- 5. the setting
			LineWatch.Reset(true); LineWatch.TestEnabled = false; w0 = LineWatch.Warnings; SpawnBoss(); Step(60);
			var rest = new List<SoldierNPC>(); foreach (var s in army) if (s.NPC.active) rest.Add(s);
			for (int i = 0; i < rest.Count && i < 8; i++) Shake.Add(rest[i].NPC.whoAmI);
			Step(10 * 60);
			Check($"with the setting off a collapse does not warn ({LineWatch.Warnings - w0})", LineWatch.Warnings == w0);
			Note($"{rest.Count} levies were still up for the setting run");
		}
	}
}
