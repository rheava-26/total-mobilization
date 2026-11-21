using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Budget;
using Levy.Common.Flavor;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;
using Mechanization.Content.Items;
using Mechanization.Content.NPCs;

namespace Mechanization.Common
{
	/// <summary>
	/// The world checks for the two machine rules (see <see cref="MachineDormancy"/>), logged as "[MachineDorm]" lines and a "[MachineDorm] self-check pass=N fail=N" summary:
	///  1. dormancy: turrets and player-built drones far from every player pack (their NPC slots come back), restore as they were when she is near, survive a save and load (packed, live and near, live and far as a server boots), and are never deleted;
	///     a Barracks garrison far from every player files its weapons back and launches again when she returns;
	///  2. the boss rule: with the setting on, no garrison drone launches or is re-armed while a boss lives and the airborne ones park, and they fly again after it; with the setting off a boss changes nothing;
	///  3. what a hostile does with nobody near (the farm question), recorded as an OBSERVED line.
	/// </summary>
	public partial class MechWorldCheck
	{
		private static int _dPass, _dFail;
		private static void DChk(string name, bool ok) { if (ok) _dPass++; else _dFail++; _mod.Logger.Info($"[MachineDorm] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void DNote(string s) => _mod.Logger.Info("[MachineDorm] OBSERVED " + s);

		private static int ActiveNpcs() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) n++; return n; }
		private static int Machines() => CountNpcs<TurretNPC>() + CountNpcs<DroneNPC>();
		private static int FieldDrones() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsField) n++; return n; }
		private static int GarrisonDrones(bool armedOnly = false) { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsBound && !d.IsField && (!armedOnly || d.Armed)) n++; return n; }
		private static TurretNPC[] Turrets() { var l = new List<TurretNPC>(); for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is TurretNPC t) l.Add(t); return l.ToArray(); }
		private static DroneNPC[] Fielders() { var l = new List<DroneNPC>(); for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsField) l.Add(d); return l.ToArray(); }

		private static void DormancyWorld()
		{
			MechConfig cfg = MechConfig.Get;
			bool oldEnabled = cfg.DormantWhenFar, oldStand = cfg.StandDownDuringBosses; int oldRange = cfg.DormantRangeTiles;
			Vector2 oldCmd = _cmdAt;
			_dPass = _dFail = 0;
			try
			{
				cfg.DormantWhenFar = true; cfg.DormantRangeTiles = 280; cfg.StandDownDuringBosses = false;
				DormantMachines();
				DormantSaveLoad();
				DormantGarrisonAndBoss();
				FarmObservation();
			}
			finally { cfg.DormantWhenFar = oldEnabled; cfg.StandDownDuringBosses = oldStand; cfg.DormantRangeTiles = oldRange; _cmdAt = oldCmd; }
			_mod.Logger.Info($"[MachineDorm] self-check pass={_dPass} fail={_dFail}");
		}

		private static void ResetMachines()
		{
			KillAll<TurretNPC>(); KillAll<DroneNPC>();
			TurretSystem.Instance.OnWorldUnload(); FieldDroneSystem.Instance.OnWorldUnload();
			MachineDormancy.Packed = MachineDormancy.Unpacked = MachineDormancy.NoSlot = MachineDormancy.GarrisonDismissed = 0;
		}

		/// <summary>Two turrets and two player-built drones placed through the real items, hurt, turned and left alone for a while with no player near.</summary>
		private static void DormantMachines()
		{
			int cx = 1200; PrepareArena(cx); ResetMachines();
			int farX = cx + 600; // 600 tiles out: past the 280 of the range, inside the 4200-tile world
			Vector2 near = _cmdAt, far = new Vector2(farX * 16f, (FloorY - 3) * 16f);
			ArmoryWith(cx, -10, ItemID.Handgun, ItemID.Minishark);
			Player p = Main.player[0];
			UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 4), new Vector2((cx + 2) * 16f, (FloorY - 4) * 16f), out _);
			UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 4), new Vector2((cx + 8) * 16f, (FloorY - 4) * 16f), out _);
			UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 4), new Vector2((cx + 14) * 16f, (FloorY - 5) * 16f), out _);
			UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 4), new Vector2((cx + 20) * 16f, (FloorY - 5) * 16f), out _);
			Step(150);
			TurretNPC[] ts = Turrets(); DroneNPC[] ds = Fielders();
			DChk($"two turrets and two player-built drones stand with a player near (turrets {ts.Length}, drones {ds.Length}, records {TurretSystem.Instance.RecordCount}+{FieldDroneSystem.Instance.RecordCount})", ts.Length == 2 && ds.Length == 2 && TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2);
			if (ts.Length != 2 || ds.Length != 2) { ResetMachines(); ClearPlaced(); return; }
			Step(300);
			DChk("with a player near, nothing packs in 5 seconds", Machines() == 4 && TurretSystem.Instance.DormantCount == 0 && FieldDroneSystem.Instance.DormantCount == 0);

			// give each something to remember: damage and a way to face
			foreach (TurretNPC t in ts) { t.NPC.life -= 11; t.NPC.direction = -1; t.NPC.spriteDirection = -1; }
			foreach (DroneNPC d in ds) { d.NPC.life -= 9; d.RestoreFromPack(41f); }
			var tId = new int[2]; var tPos = new Vector2[2]; var tLife = new int[2]; var tWeapon = new int[2]; var tMax = new int[2];
			for (int i = 0; i < 2; i++) { tId[i] = ts[i].RecordId; tPos[i] = ts[i].NPC.position; tLife[i] = ts[i].NPC.life; tWeapon[i] = ts[i].MountedWeaponType; tMax[i] = ts[i].NPC.lifeMax; }
			var dId = new int[2]; for (int i = 0; i < 2; i++) dId[i] = ds[i].RecordId;
			int slotsBefore = Main.maxNPCs - ActiveNpcs();
			DNote($"before packing: turret weapons {tWeapon[0]}/{tWeapon[1]}, drone weapons {ds[0].FieldWeaponType}/{ds[1].FieldWeaponType}, free NPC slots {slotsBefore}");

			// a player just inside the range does not pack them
			_cmdAt = new Vector2((cx + 270) * 16f, (FloorY - 3) * 16f); Step(300);
			DChk("a player 270 tiles away (inside the 280) leaves all four standing", Machines() == 4);

			// nobody near: they pack after the delay
			_cmdAt = far;
			Step(100);
			DChk("far from every player for 100 ticks: still standing (the pack waits 180 ticks)", Machines() == 4);
			Step(260);
			int slotsAfter = Main.maxNPCs - ActiveNpcs();
			DChk($"nobody within 280 tiles for 6 seconds: all four are packed (machines {Machines()}), their NPC slots free (free slots {slotsBefore} -> {slotsAfter})", Machines() == 0 && slotsAfter > slotsBefore);
			DChk($"the records stay and say dormant: turrets {TurretSystem.Instance.DormantCount}/{TurretSystem.Instance.RecordCount}, drones {FieldDroneSystem.Instance.DormantCount}/{FieldDroneSystem.Instance.RecordCount}", TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2 && TurretSystem.Instance.DormantCount == 2 && FieldDroneSystem.Instance.DormantCount == 2);
			Step(400);
			DChk("400 more ticks packed: no record dropped as destroyed, none respawned", TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2 && Machines() == 0);
			var dRecLife = new int[2]; var dRecDir = new int[2]; var dRecBat = new float[2]; var dRecWeapon = new int[2];
			for (int i = 0; i < 2; i++) { var r = FieldDroneSystem.Instance.RecordOf(dId[i]); if (r != null) { dRecLife[i] = r.Life; dRecDir[i] = r.Dir; dRecBat[i] = r.Battery; dRecWeapon[i] = r.Weapon; } }
			bool recsOk = true; var tRecLife = new int[2];
			for (int i = 0; i < 2; i++) { var r = TurretSystem.Instance.RecordOf(tId[i]); recsOk &= r != null && r.Dormant && r.Life >= tLife[i] && r.Life <= tMax[i] && r.Dir == -1 && r.Weapon == tWeapon[i] && Vector2.Distance(r.Position, tPos[i]) < 0.01f; if (r != null) tRecLife[i] = r.Life; } // a turret mends a little between the hurt and the pack: the record holds the health it left with
			DChk("a packed turret's record holds its position, health, facing and weapon", recsOk);

			// she comes back
			_cmdAt = near;
			int woke = -1; for (int k = 0; k < 120 && woke < 0; k++) { Step(1); if (Machines() == 4) woke = k; }
			DChk($"a player back within range: all four stand again (after {woke + 1} ticks)", woke >= 0 && woke <= 60);
			Step(3);
			bool same = true; string why = "";
			foreach (TurretNPC t in Turrets())
			{
				int i = t.RecordId == tId[0] ? 0 : t.RecordId == tId[1] ? 1 : -1;
				if (i < 0) { same = false; why += " unknown turret id;"; continue; }
				if (Vector2.Distance(t.NPC.position, tPos[i]) > 0.5f) { same = false; why += $" turret {i} moved {t.NPC.position} vs {tPos[i]};"; }
				if (t.NPC.life != tRecLife[i]) { same = false; why += $" turret {i} life {t.NPC.life} vs {tRecLife[i]};"; }
				if (t.NPC.direction != -1) { same = false; why += $" turret {i} faces {t.NPC.direction};"; }
				if (t.MountedWeaponType != tWeapon[i]) { same = false; why += $" turret {i} weapon {t.MountedWeaponType} vs {tWeapon[i]};"; }
			}
			foreach (DroneNPC d in Fielders())
			{
				int i = d.RecordId == dId[0] ? 0 : d.RecordId == dId[1] ? 1 : -1;
				if (i < 0) { same = false; why += " unknown drone id;"; continue; }
				if (d.NPC.life != dRecLife[i]) { same = false; why += $" drone {i} life {d.NPC.life} vs {dRecLife[i]};"; }
				if (Math.Abs(d.Battery - dRecBat[i]) > 1f) { same = false; why += $" drone {i} battery {d.Battery:0.0} vs {dRecBat[i]:0.0};"; }
				if (d.FieldWeaponType != dRecWeapon[i]) { same = false; why += $" drone {i} weapon {d.FieldWeaponType} vs {dRecWeapon[i]};"; }
				if (Vector2.Distance(d.HomeSpot, FieldDroneSystem.Instance.RecordOf(d.RecordId).Pad) > 0.01f) { same = false; why += $" drone {i} home moved;"; }
			}
			DChk("every machine is back exactly as it was: same position, health, facing, weapon and battery" + (why.Length > 0 ? " (" + why + ")" : ""), same);
			DChk("woken: no record is dormant, none lost, none duplicated (turrets and drones still two each)", TurretSystem.Instance.DormantCount == 0 && FieldDroneSystem.Instance.DormantCount == 0 && CountNpcs<TurretNPC>() == 2 && FieldDrones() == 2 && TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2);
			DNote($"counters: packed {MachineDormancy.Packed}, unpacked {MachineDormancy.Unpacked}, woke without a free slot {MachineDormancy.NoSlot}");
			DChk($"the counters agree: packed {MachineDormancy.Packed} = unpacked {MachineDormancy.Unpacked} = 4", MachineDormancy.Packed == 4 && MachineDormancy.Unpacked == 4);

			// the setting off: far machines keep standing
			cfgOff(false);
			_cmdAt = far; Step(500);
			DChk("with the setting off, nothing packs however far she is", Machines() == 4 && MachineDormancy.Packed == 4);
			cfgOff(true);
			Step(300);
			DChk("switching it on packs them (a config change needs no restart)", Machines() == 0 && MachineDormancy.Packed == 8);
			cfgOff(false); Step(40);
			DChk("switching it off wakes them all with nobody near", Machines() == 4 && MachineDormancy.Unpacked == 8);
			cfgOff(true);

			// a destroyed live machine still drops its record; packed ones are never confused with destroyed
			_cmdAt = near; Step(60);
			TurretNPC victim = Turrets()[0]; int victimId = victim.RecordId; victim.NPC.active = false; Step(5);
			DChk("a turret destroyed while a player is near still loses its record (the reconcile pass is unchanged)", TurretSystem.Instance.RecordOf(victimId) == null && TurretSystem.Instance.RecordCount == 1);

			// no free NPC slot: it stays packed and wakes when a slot is free
			_cmdAt = far; Step(400);
			DChk("all machines packed again before the crowded wake", Machines() == 0 && TurretSystem.Instance.DormantCount == 1 && FieldDroneSystem.Instance.DormantCount == 2);
			var filler = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				int idx = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (cx + 40) * 16, (FloorY - 3) * 16, NPCID.Bunny);
				if (idx < 0 || idx >= Main.maxNPCs) break;
				NPC n = Main.npc[idx]; n.aiStyle = -1; n.noGravity = true; n.noTileCollide = true; n.lifeMax = n.life = 100000; n.friendly = true; filler.Add(n);
			}
			_cmdAt = near; Step(150);
			DChk($"a full NPC array: the packed machines stay packed, not deleted (machines {Machines()}, records {TurretSystem.Instance.RecordCount}+{FieldDroneSystem.Instance.RecordCount}, woke-without-slot {MachineDormancy.NoSlot})", Machines() == 0 && TurretSystem.Instance.RecordCount == 1 && FieldDroneSystem.Instance.RecordCount == 2 && MachineDormancy.NoSlot > 0);
			foreach (NPC n in filler) n.active = false;
			Step(90);
			DChk("with the slots free again they wake", Machines() == 3);

			ResetMachines(); ClearPlaced();

			void cfgOff(bool on) { MechConfig.Get.DormantWhenFar = on; }
		}

		/// <summary>The world's own save path: a packed record, a live machine and a live machine on a server nobody has joined yet each come back through TagIO.</summary>
		private static void DormantSaveLoad()
		{
			int cx = 1200; PrepareArena(cx); ResetMachines();
			Vector2 near = _cmdAt, far = new Vector2((cx + 600) * 16f, (FloorY - 3) * 16f);
			ArmoryWith(cx, -10, ItemID.Handgun, ItemID.Minishark);
			Player p = Main.player[0];
			UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 4), new Vector2((cx + 3) * 16f, (FloorY - 4) * 16f), out _);
			UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 4), new Vector2((cx + 12) * 16f, (FloorY - 5) * 16f), out _);
			Step(150);
			if (Machines() != 2) { DChk("save and load: a turret and a drone placed", false); ResetMachines(); ClearPlaced(); return; }
			TurretNPC t0 = Turrets()[0]; DroneNPC d0 = Fielders()[0];
			t0.NPC.life -= 13; t0.NPC.direction = -1; d0.NPC.life -= 7;
			int tId = t0.RecordId, dId = d0.RecordId; Vector2 tPos = t0.NPC.position; int tLife = t0.NPC.life, tWeapon = t0.MountedWeaponType, dWeapon = d0.FieldWeaponType; Vector2 dPad = d0.HomeSpot;

			TagCompound RoundTrip()
			{
				var a = new TagCompound(); var b = new TagCompound();
				TurretSystem.Instance.SaveWorldData(a); FieldDroneSystem.Instance.SaveWorldData(b);
				var tag = new TagCompound { ["turret"] = a, ["drone"] = b };
				using var ms = new MemoryStream(); TagIO.ToStream(tag, ms); ms.Position = 0; return TagIO.FromStream(ms);
			}
			void Reload(TagCompound tag, bool killBodies = true)
			{
				if (killBodies) { KillAll<TurretNPC>(); KillAll<DroneNPC>(); }
				TurretSystem.Instance.OnWorldUnload(); FieldDroneSystem.Instance.OnWorldUnload();
				TurretSystem.Instance.LoadWorldData(tag.GetCompound("turret")); FieldDroneSystem.Instance.LoadWorldData(tag.GetCompound("drone"));
			}
			bool Intact(string label)
			{
				var tr = TurretSystem.Instance.RecordOf(tId); var dr = FieldDroneSystem.Instance.RecordOf(dId);
				bool ok = tr != null && dr != null && tr.Life == tLife && Vector2.Distance(tr.Position, tPos) < 0.01f && tr.Dir == -1 && tr.Weapon == tWeapon && Vector2.Distance(dr.Pad, dPad) < 0.01f && dr.Weapon == dWeapon;
				if (!ok) DNote($"{label}: turret record {(tr == null ? "missing" : $"life {tr.Life} dir {tr.Dir} weapon {tr.Weapon} pos {tr.Position}")} (want life {tLife} dir -1 weapon {tWeapon} pos {tPos}); drone record {(dr == null ? "missing" : $"weapon {dr.Weapon} pad {dr.Pad}")}");
				return ok;
			}

			// (a) saved while packed, loaded with nobody near, then she arrives
			_cmdAt = far; Step(400);
			DChk("save and load, packed: both are packed before the save", Machines() == 0 && TurretSystem.Instance.DormantCount == 1 && FieldDroneSystem.Instance.DormantCount == 1);
			TagCompound saved = RoundTrip();
			Reload(saved); Step(60);
			DChk("save and load, packed: after the load both records are back and still dormant, no NPC holds a slot", TurretSystem.Instance.RecordCount == 1 && FieldDroneSystem.Instance.RecordCount == 1 && TurretSystem.Instance.DormantCount == 1 && FieldDroneSystem.Instance.DormantCount == 1 && Machines() == 0);
			_cmdAt = near; Step(120);
			DChk("save and load, packed: a player arrives and they stand again, as they were", Machines() == 2 && Intact("packed"));
			TurretNPC t1 = Turrets().Length > 0 ? Turrets()[0] : null; DroneNPC d1 = Fielders().Length > 0 ? Fielders()[0] : null;
			DChk("save and load, packed: the woken turret keeps its position, health, facing and weapon", t1 != null && t1.RecordId == tId && Vector2.Distance(t1.NPC.position, tPos) < 0.5f && t1.NPC.life == tLife && t1.NPC.direction == -1 && t1.MountedWeaponType == tWeapon);
			DChk("save and load, packed: the woken drone keeps its home and weapon", d1 != null && d1.RecordId == dId && Vector2.Distance(d1.HomeSpot, dPad) < 0.01f && d1.FieldWeaponType == dWeapon);

			// (b) saved live with a player near, loaded with her near
			Step(30);
			saved = RoundTrip();
			Reload(saved); Step(60);
			DChk("save and load, live with a player near: both are standing again, none packed", Machines() == 2 && TurretSystem.Instance.DormantCount == 0 && FieldDroneSystem.Instance.DormantCount == 0 && Turrets()[0].RecordId == tId && Fielders()[0].RecordId == dId);

			// (c) saved live, loaded with nobody near (a dedicated server before the first player joins): they load packed and hold no slot
			saved = RoundTrip();
			_cmdAt = far; Step(1);
			Reload(saved); Step(60);
			DChk("save and load, live but nobody near at load: they load packed, no NPC slot used, nothing lost", Machines() == 0 && TurretSystem.Instance.RecordCount == 1 && FieldDroneSystem.Instance.RecordCount == 1 && TurretSystem.Instance.DormantCount == 1 && FieldDroneSystem.Instance.DormantCount == 1 && Intact("live-then-far"));
			_cmdAt = near; Step(120);
			DChk("save and load, live but nobody near at load: a player joins and they stand", Machines() == 2);

			// an older save with no dormancy keys loads as plain live records
			var old = new TagCompound { ["turrets"] = new List<TagCompound> { new TagCompound { ["id"] = 5, ["x"] = (cx + 3) * 16f, ["y"] = (FloorY - 3) * 16f, ["life"] = 100 } }, ["nextId"] = 6 };
			KillAll<TurretNPC>(); KillAll<DroneNPC>(); TurretSystem.Instance.OnWorldUnload(); FieldDroneSystem.Instance.OnWorldUnload();
			TurretSystem.Instance.LoadWorldData(old); FieldDroneSystem.Instance.LoadWorldData(new TagCompound()); Step(60);
			DChk("a world saved before dormancy existed loads its turret live, with a player near", CountNpcs<TurretNPC>() == 1 && TurretSystem.Instance.DormantCount == 0);

			ResetMachines(); ClearPlaced();
		}

		/// <summary>Barracks garrison: asleep far from every player (loans go home), awake when she is near; and the boss rule with the setting on and off.</summary>
		private static void DormantGarrisonAndBoss()
		{
			int cx = 1200; PrepareArena(cx); ResetMachines();
			Vector2 near = _cmdAt, far = new Vector2((cx + 600) * 16f, (FloorY - 3) * 16f);
			GuideSystem g = GuideSystem.Instance; bool savedEng = g.Engineering; g.Engineering = true;
			MechConfig cfg = MechConfig.Get;
			try
			{
				Point16 bar = RealPlace(ModContent.TileType<BarracksTile>(), 0, cx + 2, FloorY - 1);
				int[] guns = { ItemID.Handgun, ItemID.Minishark, ItemID.Musket, ItemID.TheUndertaker, ItemID.FlintlockPistol, ItemID.Boomstick };
				ArmoryTE armory = ArmoryWith(cx, 8, guns);
				DChk("garrison: a Barracks and an Armory with six guns placed", bar.X >= 0 && armory != null);
				if (bar.X < 0 || armory == null) return;
				Step(1500);
				int up = GarrisonDrones(true);
				DChk($"garrison, a player near: drones launch ({up} armed)", up >= 1);
				int lentUp = armory.Lent.Count;

				// nobody near: the garrison is asleep, every loan goes back to the Armory
				_cmdAt = far; Step(600);
				DChk($"garrison, nobody within 280 tiles: no garrison drone is left in the world ({GarrisonDrones()}), the Armory holds every weapon again (lent {lentUp} -> {armory.Lent.Count})", GarrisonDrones() == 0 && armory.Lent.Count == 0);
				int stored = 0; for (int i = 0; i < armory.Storage.Length; i++) if (armory.Storage[i] != null && !armory.Storage[i].IsAir) stored++;
				DChk($"garrison, asleep: the six guns are still in the Armory (found {stored})", stored >= 6);
				Step(900);
				DChk("garrison, asleep for 25 seconds: still nothing launched", GarrisonDrones() == 0);
				_cmdAt = near; Step(1500);
				DChk($"garrison, a player back: it launches again ({GarrisonDrones(true)} armed)", GarrisonDrones(true) >= 1);

				// the boss rule. Control first: setting off, a boss changes nothing.
				NPC boss = Target(cx, 100); boss.boss = true; boss.friendly = false; boss.lifeMax = boss.life = 100000;
				Vector2 bossAt = boss.position;
				cfg.StandDownDuringBosses = false;
				for (int k = 0; k < 700; k++) { Pin(boss, bossAt); Step(1); }
				int armedOff = GarrisonDrones(true);
				DChk($"boss rule off, a boss alive (BossSlots sees it: {BossSlots.BossAlive}): the garrison keeps flying ({armedOff} armed)", BossSlots.BossAlive && armedOff >= 1);
				boss.active = false; Step(60);

				// on: while the boss lives, the ones in the air fly home and park and nothing launches
				cfg.StandDownDuringBosses = true;
				int before = GarrisonDrones(true);
				boss = Target(cx, 100); boss.boss = true; boss.friendly = false; boss.lifeMax = boss.life = 100000; bossAt = boss.position;
				for (int k = 0; k < 1500; k++) { Pin(boss, bossAt); Step(1); }
				int armedOn = GarrisonDrones(true), total = GarrisonDrones();
				DChk($"boss rule on, a boss alive for 25 seconds: no garrison drone is armed ({armedOn}, was {before}) and every weapon is back in the Armory (lent {armory.Lent.Count})", BossSlots.BossAlive && armedOn == 0 && armory.Lent.Count == 0);
				int parked = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC d && d.IsBound && !d.IsField && d.IsParked) parked++;
				DChk($"boss rule on: the drones parked on their pads, none deleted (parked {parked} of {total}, was {before} flying)", parked == total && total >= before);
				for (int k = 0; k < 1500; k++) { Pin(boss, bossAt); Step(1); }
				DChk($"boss rule on, 25 seconds more: still no new launch or re-arm (armed {GarrisonDrones(true)}, drones {GarrisonDrones()} vs {total})", GarrisonDrones(true) == 0 && GarrisonDrones() == total);
				// a player-built drone and a turret are not touched while the boss lives
				UseItem(Main.player[0], new Item(ModContent.ItemType<DroneItem>(), 2), new Vector2((cx + 20) * 16f, (FloorY - 5) * 16f), out _);
				UseItem(Main.player[0], new Item(ModContent.ItemType<TurretItem>(), 2), new Vector2((cx + 24) * 16f, (FloorY - 4) * 16f), out _);
				for (int k = 0; k < 300; k++) { Pin(boss, bossAt); Step(1); }
				DChk("boss rule on: a player-built drone and a turret still work and are standing", FieldDrones() == 1 && CountNpcs<TurretNPC>() == 1);
				KillAll<TurretNPC>(); KillAll<DroneNPC>(); // the fixtures; the garrison and its records are rebuilt below
				TurretSystem.Instance.OnWorldUnload(); FieldDroneSystem.Instance.OnWorldUnload();

				// the boss dies: the garrison arms again
				boss.active = false;
				Step(1800);
				DChk($"boss rule on, the boss gone: the garrison launches again ({GarrisonDrones(true)} armed)", !BossSlots.BossAlive && GarrisonDrones(true) >= 1);
				DChk("boss rule: no weapon was lost over the whole fight (the Armory ledger and its racks account for all six)", armory.Lent.Count + CountStored(armory) >= 6);
			}
			finally { cfg.StandDownDuringBosses = false; g.Engineering = savedEng; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].boss && Main.npc[i].type == NPCID.GreenSlime) Main.npc[i].active = false; ResetMachines(); ClearPlaced(); }
		}

		private static int CountStored(ArmoryTE a) { int n = 0; for (int i = 0; i < a.Storage.Length; i++) if (a.Storage[i] != null && !a.Storage[i].IsAir) n++; return n; }

		/// <summary>
		/// The farm question: with nobody within range, does a hostile that a statue or an event drops beside a machine last long enough for the machine to matter? Only recorded: skeletons spawned at several distances (sideways, then straight down)
		/// from the only player, each followed for up to 300 ticks (damage 0, so none harms her), and the tick each one was removed by the game, if it was.
		/// </summary>
		private static void FarmObservation()
		{
			Player p = Main.player[0];
			Vector2 at = new Vector2(1200 * 16f, (FloorY - 3) * 16f); p.active = true; p.dead = false;
			bool day = Main.dayTime; double time = Main.time; Main.dayTime = false; Main.time = 3000; // a night, so no daylight despawn rule is what ends it
			var rows = new List<string>();
			try
			{
				foreach (var (dx, dy) in new[] { (40, 0), (100, 0), (150, 0), (200, 0), (250, 0), (300, 0), (400, 0), (0, 80), (0, 120), (0, 160), (0, 200) })
				{
					int i = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (int)(at.X + dx * 16f), (int)(at.Y + dy * 16f), NPCID.Skeleton);
					if (i < 0 || i >= Main.maxNPCs) { rows.Add($"{dx}/{dy}:nospawn"); continue; }
					NPC z = Main.npc[i]; z.damage = 0; int gone = -1;
					for (int k = 0; k < 300; k++)
					{
						p.active = true; p.dead = false; p.position = at - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
						global::Levy.Common.Testing.WorldStep.Update();
						if (!z.active || z.type != NPCID.Skeleton) { gone = k + 1; break; }
					}
					rows.Add($"{(dx > 0 ? dx + " tiles across" : dy + " tiles down")}: {(gone > 0 ? "removed after " + gone + " ticks" : "alive after 300")}");
					if (z.active && z.type == NPCID.Skeleton) z.active = false;
				}
			}
			finally { Main.dayTime = day; Main.time = time; }
			DNote("farm: a skeleton spawned at a distance from the only player, followed 300 ticks: " + string.Join("; ", rows));
		}
	}
}
