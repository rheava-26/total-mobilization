using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the field fixes, logged as "[FieldFix]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the summon world check chains it; LEVY_FIELDFIX_ONLY=1 runs it alone).
	/// A long flat arena, slot 0 standing in as the commander, the real Main.Update stepping the world. Throwaway soldiers, town NPCs and tiles only; the arena is restored and nothing is saved.
	///  * THORNS: a levy standing in jungle, corruption and crimson thorns breaks them as a player does (the TouchDamageDestroyTile set, the same HurtTiles rule); it still takes the damage; spikes (hurt but not destroyed) and a thorn it never touched stay.
	///  * BOSS JOIN: a commander, a boss 25 / 60 tiles away, and one body of each kind in turn (a regular on Follow and on Search and Destroy, officers, a militiaman). Measures whether it gets in reach and hurts the boss.
	///  * BANNER: an officer or a levy away from home leaves the off-duty banner at home (data only here; the draw is client-side), with the hover text; it goes when they are back.
	/// </summary>
	public partial class FieldFixCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static NPC _target; private static Vector2 _targetAt, _cmdAt;
		private static bool _local; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 200;
		private const string Commander = "FieldFixTester";
		private static float _cmdTilesX; // where the commander stands, tiles from Cx

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_FIELDFIX") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; PinTarget(); if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the world checks chain it, one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; IDisposable quiet = WorldStep.Quiet();
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { quiet.Dispose(); _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); }
			_mod.Logger.Info($"[FieldFix] self-check pass={_pass} fail={_fail}");
		}

		/// <summary>True while the check steps the world: the stand-in commander counts as online (LevyUtil.FindPlayerByName), and natural enemy spawns are cleared.</summary>
		public static bool Running => _running;

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[FieldFix] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[FieldFix] OBSERVED " + s);

		private static void ClearStrangers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (WorldStep.IsNaturalHostile(n) && n != _target && !(n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) n.active = false;
			}
		}

		private static void Step(int n)
		{
			// the world spawns hostiles at random with the stand-in commander in it; clearing them once per call left a 240-tick wait open to a slime that knocks a Hold levy off its thorn or fights it, so they go every tick
			if (ClearOnce) ClearStrangers();
			for (int i = 0; i < n; i++) { if (!ClearOnce) ClearStrangers(); KeepCommander(); WorldStep.Update(); }
		}
		/// <summary>Lab only: LEVY_FIELDFIX_CLEAR_ONCE=1 puts the old once-per-call clearing back (to measure what it did to the thorns part).</summary>
		private static readonly bool ClearOnce = Environment.GetEnvironmentVariable("LEVY_FIELDFIX_CLEAR_ONCE") == "1";

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = true; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void PinTarget()
		{
			if (_target == null || !_target.active) return;
			_target.active = true; _target.timeLeft = 9999; _target.Bottom = _targetAt; _target.velocity = Vector2.Zero; _target.life = _target.lifeMax; _target.dontTakeDamage = false;
		}

		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 90; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void PlaceCommander(float tilesFromCentre) { _cmdTilesX = tilesFromCentre; _cmdAt = new Vector2((Cx + tilesFromCentre) * 16f, (FloorY - 3) * 16f); Main.player[0].position = _cmdAt; }

		/// <summary>A pinned boss dummy (a real boss type, flagged boss, no AI, a million life) <paramref name="tiles"/> east of the centre.</summary>
		private static void SpawnBoss(float tiles, int type = NPCID.EyeofCthulhu)
		{
			KillBoss();
			_targetAt = new Vector2((Cx + tiles) * 16f, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("FieldFixCheck"), (int)_targetAt.X, (int)_targetAt.Y, type);
			if (ti < 0 || ti >= Main.maxNPCs) { _target = null; return; }
			_target = Main.npc[ti];
			_target.lifeMax = 1_000_000; _target.life = 1_000_000; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 80; _target.height = 96;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.boss = true; _target.Bottom = _targetAt;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position;
		}

		private static void KillBoss()
		{
			if (_target != null) _target.active = false;
			_target = null;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) Main.npc[i].active = false;
		}

		/// <summary>Everything this check made, gone: soldiers, avatars, town NPCs, the boss, projectiles, placed tiles.</summary>
		private static void Cleanup()
		{
			try
			{
				SoldierNPC.TileHazardsOverride = -1;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					var g = OfficerSystem.Data(n);
					if (g != null && g.Enrolled && g.Commander == Commander) { g.Enrolled = false; g.Commander = ""; g.ResetRuntime(); n.active = false; }
					else if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				KillBoss();
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				OffDutyBanner.Clear();
			}
			catch (Exception) { }
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			PlaceCommander(-100);
			string only = Environment.GetEnvironmentVariable("LEVY_FIELDFIX_PART") ?? "all";
			int reps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_FIELDFIX_REPEAT"), out int rp) ? Math.Max(1, rp) : 1; // lab: the thorns part N times in one boot, to catch a flake
			if (only == "all" || only == "thorns") { for (int r = 0; r < reps; r++) { try { Thorns(); } finally { Cleanup(); } } }
			if (only == "all" || only == "join") { try { BossJoin(); } finally { Cleanup(); } }
			if (only == "all" || only == "banner") { try { Banner(); } finally { Cleanup(); } }
		}

		// =====================================================================================================
		// soldiers
		// =====================================================================================================
		private static SoldierNPC Levy(Vector2 at, SoldierOrder order, int weapon = 0, int ammo = 0, SoldierKind kind = SoldierKind.Regular, int barracksId = -1)
		{
			var rec = new SoldierRecord
			{
				Id = kind == SoldierKind.Regular ? RosterSystem.Instance.NextId++ : -7000 - Main.rand.Next(1000), Kind = kind, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = (kind == SoldierKind.Regular ? "Pvt. " : "Mil. ") + "FieldFix",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = barracksId, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (weapon > 0)
			{
				Item w = new Item(); w.SetDefaults(weapon);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				if (ammo > 0) { Item a = new Item(); a.SetDefaults(ammo); a.stack = 999; if (rec.Gear.CanHold(SoldierGear.SlotAmmo, a)) { rec.Gear.Ammo = a; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); } }
				rec.LoadoutLocked = true;
			}
			rec.Order = order;
			if (kind == SoldierKind.Regular) RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		/// <summary>Commissions a throwaway town NPC of <paramref name="townType"/> to the commander and steps until his avatar marches. Returns the town NPC, or null.</summary>
		private static NPC Commission(int townType, int slot, Vector2 at, out SoldierNPC avatar)
		{
			avatar = null;
			int i = NPC.NewNPC(new EntitySource_WorldEvent("FieldFixCheck"), (int)at.X, (int)at.Y - 40, townType);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC t = Main.npc[i];
			var g = OfficerSystem.Data(t);
			if (g == null) { t.active = false; return null; }
			g.Enrolled = true; g.Commander = Commander; g.Slot = slot; g.DaysUnpaid = 0; g.ResetRuntime();
			t.netAlways = true; t.netUpdate = true;
			for (int k = 0; k < 180 && avatar == null; k++) { Step(1); avatar = OfficerSystem.Instance.AvatarOf(t); }
			return t;
		}

		// =====================================================================================================
		// THORNS
		// =====================================================================================================
		private static void PutTile(int x, int y, int type)
		{
			Tile t = Main.tile[x, y]; t.ClearEverything(); t.HasTile = true; t.TileType = (ushort)type; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid;
		}
		private static bool Has(int x, int y, int type) { Tile t = Main.tile[x, y]; return t.HasTile && t.TileType == type; }

		private static void Thorns()
		{
			SoldierNPC.TileHazardsOverride = 1;
			var destroy = new List<int>(); for (int t = 0; t < TileID.Count; t++) if (TileID.Sets.TouchDamageDestroyTile[t]) destroy.Add(t);
			Note("tiles vanilla breaks on touch (TouchDamageDestroyTile): " + string.Join(", ", destroy.Select(t => t + " " + TileID.Search.GetName(t))));
			Check("the set is the vanilla thorns: jungle, corruption and crimson", destroy.Contains(TileID.JungleThorns) && destroy.Contains(TileID.CorruptThorns) && destroy.Contains(TileID.CrimsonThorns) && destroy.All(t => TileID.Sets.TouchDamageImmediate[t] > 0 || TileID.Sets.TouchDamageHot[t] || TileID.Sets.TouchDamageBleeding[t]));
			PlaceCommander(-100);
			var cases = new (string name, int tile, float x, bool breaks)[]
			{
				("jungle thorns", TileID.JungleThorns, -80, true), ("corruption thorns", TileID.CorruptThorns, -60, true), ("crimson thorns", TileID.CrimsonThorns, -40, true),
				("spikes (hurt, never broken)", TileID.Spikes, -20, false), ("wooden spikes (hurt, never broken)", TileID.WoodenSpikes, 0, false),
			};
			var bodies = new List<SoldierNPC>();
			foreach (var c in cases)
			{
				int tx = Cx + (int)c.x;
				PutTile(tx, FloorY - 1, c.tile); PutTile(tx, FloorY - 2, c.tile);
				var s = Levy(new Vector2(tx * 16f + 8f, FloorY * 16f), SoldierOrder.Hold);
				if (s == null) { Check(c.name + ": the levy spawns", false); bodies.Add(null); continue; }
				s.NPC.lifeMax = s.NPC.life = Math.Max(s.NPC.lifeMax, 4000); // the check is about the tile, not about surviving the spikes
				bodies.Add(s);
			}
			int farX = Cx + 40; PutTile(farX, FloorY - 1, TileID.JungleThorns); PutTile(farX, FloorY - 2, TileID.JungleThorns); // a thorn nobody touches
			// Wait for the contact rather than for a fixed 240 ticks: the check is about what a levy standing in a thorn does to it, and a body that anything shoves off its column (a knock, a neighbour) touches one tile of the
			// two and leaves the other, which read as "the touched tiles are broken" failing for crimson thorns once under load. A body that has left its column is put back (and counted below, so a Hold levy that
			// wanders is still seen); the verdicts are unchanged: every breaking tile must be gone and every case hurt, and spikes must still stand after at least 240 ticks.
			int displaced = 0, waited = 0;
			bool Settled()
			{
				for (int k = 0; k < cases.Length; k++)
				{
					var s0 = bodies[k]; if (s0 == null) continue;
					int tx0 = Cx + (int)cases[k].x;
					if (s0.HazardHits == 0) return false;
					if (cases[k].breaks && (Has(tx0, FloorY - 1, cases[k].tile) || Has(tx0, FloorY - 2, cases[k].tile))) return false;
				}
				return true;
			}
			for (waited = 0; waited < 1500; waited++)
			{
				Step(1);
				if (waited % 6 == 0)
					for (int k = 0; k < cases.Length; k++)
					{
						var s0 = bodies[k]; if (s0 == null || !s0.NPC.active) continue;
						float colX = (Cx + cases[k].x) * 16f + 8f;
						if (Math.Abs(s0.NPC.Center.X - colX) > 10f) { displaced++; s0.NPC.Bottom = new Vector2(colX, FloorY * 16f); s0.NPC.velocity = Vector2.Zero; }
					}
				if (waited >= 239 && Settled()) break;
			}
			Note($"thorns: settled after {waited + 1} ticks, bodies put back on their column {displaced} time(s)");
			for (int k = 0; k < cases.Length; k++)
			{
				var c = cases[k]; var s = bodies[k]; if (s == null) continue;
				int tx = Cx + (int)c.x; bool rem = !Has(tx, FloorY - 1, c.tile) && !Has(tx, FloorY - 2, c.tile), any = Has(tx, FloorY - 1, c.tile) || Has(tx, FloorY - 2, c.tile);
				Note($"{c.name}: hazard hits {s.HazardHits} (damage {s.HazardDamageTotal}), tiles broken {s.HazardBreaks}, thorn tiles left {(Has(tx, FloorY - 1, c.tile) ? 1 : 0) + (Has(tx, FloorY - 2, c.tile) ? 1 : 0)}");
				Check(c.name + ": the levy is hurt as before (" + s.HazardHits + " hits)", s.HazardHits > 0);
				if (c.breaks) Check(c.name + ": the touched tiles are broken", rem);
				else Check(c.name + ": the tile stays (vanilla breaks only the thorns)", any && s.HazardBreaks == 0);
			}
			Check("a thorn nobody touched stays", Has(farX, FloorY - 1, TileID.JungleThorns) && Has(farX, FloorY - 2, TileID.JungleThorns));
			// the setting off: no damage, no break
			SoldierNPC.TileHazardsOverride = 0;
			int ox = Cx + 60; PutTile(ox, FloorY - 1, TileID.JungleThorns); PutTile(ox, FloorY - 2, TileID.JungleThorns);
			var off = Levy(new Vector2(ox * 16f + 8f, FloorY * 16f), SoldierOrder.Hold);
			Step(120);
			Check("with the tile-hazards setting off a levy neither takes damage nor breaks thorns", off != null && off.HazardHits == 0 && Has(ox, FloorY - 1, TileID.JungleThorns) && Has(ox, FloorY - 2, TileID.JungleThorns));
			SoldierNPC.TileHazardsOverride = -1;
			Arena();
		}

		// =====================================================================================================
		// BOSS JOIN (measurement; the checks sit in FieldFixCheck.Join.cs)
		// =====================================================================================================
	}
}
