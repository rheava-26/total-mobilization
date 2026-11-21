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
	///  2. the boss rule: with the setting on, the garrison's drones leave the field within two seconds of a boss (weapons back in the Armory, slots free), nothing launches while it lives, and the garrison launches again after it;
	///     with the setting off a boss changes nothing;
	///  2b. room for a boss: a worm boss summoned into a near-full NPC array packs placed drones and turrets (farthest first) before any levy is benched, keeps them packed while it lives, and they wake as they were after it;
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
				BossRoomPacksMachines();
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
			int Fill()
			{
				int added = 0;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					int idx = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (cx + 40) * 16, (FloorY - 3) * 16, NPCID.Bunny);
					if (idx < 0 || idx >= Main.maxNPCs) break;
					NPC n = Main.npc[idx]; n.aiStyle = -1; n.noGravity = true; n.noTileCollide = true; n.lifeMax = n.life = 100000; n.friendly = true; n.timeLeft = 99999; filler.Add(n); added++;
				}
				return added;
			}
			Fill();
			_cmdAt = near;
			int topUps = 0; // a slot the world frees on its own (a natural NPC leaving) is filled again before the next tick, so the array really stays full
			for (int k = 0; k < 150; k++) { Step(1); topUps += Fill(); }
			DNote($"full NPC array: slots the world freed on its own over 150 ticks, filled again: {topUps}");
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

				// on: while the boss lives the garrison leaves the field at once, every weapon filed back, its NPC slots free; nothing launches
				cfg.StandDownDuringBosses = true;
				int before = GarrisonDrones(), beforeArmed = GarrisonDrones(true), lentBefore = armory.Lent.Count, freeBefore = Main.maxNPCs - HeldNpcs();
				MachineDormancy.GarrisonVanished = 0;
				boss = Target(cx, 100); boss.boss = true; boss.friendly = false; boss.lifeMax = boss.life = 100000; bossAt = boss.position;
				int gone = -1;
				for (int k = 0; k < 180; k++) { Pin(boss, bossAt); Step(1); if (gone < 0 && GarrisonDrones() == 0) gone = k + 1; }
				int freeAfter = Main.maxNPCs - HeldNpcs();
				DChk($"boss rule on: every garrison drone is gone within 2 seconds of the boss (gone after {gone} ticks; {before} drones, {beforeArmed} armed before; removed {MachineDormancy.GarrisonVanished})", before >= 1 && gone > 0 && gone <= 120 && GarrisonDrones() == 0 && MachineDormancy.GarrisonVanished >= before);
				DChk($"boss rule on: every weapon is back in the Armory (lent {lentBefore} -> {armory.Lent.Count}, racks hold {CountStored(armory)})", armory.Lent.Count == 0 && CountStored(armory) >= 6);
				DChk($"boss rule on: their NPC slots are free (slots not held by a critter {freeBefore} -> {freeAfter}, the boss took 1)", freeAfter >= freeBefore - 1 + before);
				for (int k = 0; k < 1500; k++) { Pin(boss, bossAt); Step(1); }
				DChk($"boss rule on, 25 seconds more: nothing launches (garrison drones {GarrisonDrones()}, lent {armory.Lent.Count})", GarrisonDrones() == 0 && armory.Lent.Count == 0 && BossSlots.BossAlive);
				// a player-built drone and a turret are not touched while the boss lives
				UseItem(Main.player[0], new Item(ModContent.ItemType<DroneItem>(), 2), new Vector2((cx + 20) * 16f, (FloorY - 5) * 16f), out _);
				UseItem(Main.player[0], new Item(ModContent.ItemType<TurretItem>(), 2), new Vector2((cx + 24) * 16f, (FloorY - 4) * 16f), out _);
				for (int k = 0; k < 300; k++) { Pin(boss, bossAt); Step(1); }
				DChk("boss rule on: a player-built drone and a turret still work and are standing", FieldDrones() == 1 && CountNpcs<TurretNPC>() == 1);
				KillAll<TurretNPC>(); KillAll<DroneNPC>(); // the fixtures; the garrison and its records are rebuilt below
				TurretSystem.Instance.OnWorldUnload(); FieldDroneSystem.Instance.OnWorldUnload();

				// the boss dies: the garrison launches again from the Barracks and the Armory
				boss.active = false;
				Step(1800);
				DChk($"boss rule on, the boss gone: the garrison launches again ({GarrisonDrones(true)} armed, lent {armory.Lent.Count})", !BossSlots.BossAlive && GarrisonDrones(true) >= 1 && armory.Lent.Count >= 1);
				DChk("boss rule: no weapon was lost over the whole fight (the Armory ledger and its racks account for all six)", armory.Lent.Count + CountStored(armory) >= 6);
			}
			finally { cfg.StandDownDuringBosses = false; g.Engineering = savedEng; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].boss && Main.npc[i].type == NPCID.GreenSlime) Main.npc[i].active = false; ResetMachines(); ClearPlaced(); }
		}

		/// <summary>Active NPCs that are not critters (a bunny the world drops in beside the check does not move a free-slot count).</summary>
		private static int HeldNpcs() { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && !(x.friendly && !x.townNPC && x.ModNPC == null && (NPCID.Sets.CountsAsCritter[x.type] || x.catchItem > 0))) n++; } return n; }

		/// <summary>
		/// Room for a boss, without losing a machine. A worm boss (the Devourer of Gods with Calamity, else the Destroyer) is summoned the way its item does into an NPC array with 3 slots fewer than it needs; the world holds
		/// four levies, two placed turrets and two placed drones, and friendly fillers nothing may take. Levy's room-making must pack the three machines farthest from the spawn and bench no levy; the reserve it keeps while the boss
		/// lives then packs the fourth (never a levy); none wakes while the boss lives; when it is gone all four stand again with the position, health, facing, weapon and battery they left with. A control summon with room to spare
		/// measures how many slots the boss takes first, and must take nothing.
		/// </summary>
		private static void BossRoomPacksMachines()
		{
			int cx = 1200; PrepareArena(cx); ResetMachines();
			bool cal = ModLoader.TryGetMod("CalamityMod", out Mod calMod);
			int bossType = NPCID.TheDestroyer; string bossName = "the Destroyer";
			if (cal && calMod.TryFind("DevourerofGodsHead", out ModNPC dog)) { bossType = dog.Type; bossName = "the Devourer of Gods"; }
			bool day = Main.dayTime; double time = Main.time; Main.dayTime = false; Main.time = 3000; // a night: the Destroyer leaves by day
			var fillers = new List<NPC>(); var levies = new List<NPC>(); NPC standIn = null;
			Player p = Main.player[0];
			Vector2 standAt = new Vector2((cx - 150) * 16f, (FloorY - 40) * 16f);
			_keepBossParts = true;
			var lastBat = new Dictionary<int, float>(); // each placed drone's battery the tick before it packed (it charges on its pad, so the record holds what it had then)
			void StepSafe(int n) { for (int k = 0; k < n; k++) { p.immune = true; p.immuneTime = 30; if (standIn != null) Pin(standIn, standAt); foreach (DroneNPC d in Fielders()) lastBat[d.RecordId] = d.Battery; Step(1); } }
			int FreeSlots() => MachineDormancy.FreeNpcSlots();
			int Levies() { int n = 0; foreach (NPC l in levies) if (l.active && l.ModNPC is global::Levy.Content.NPCs.SoldierNPC) n++; return n; }
			void KillBoss(int head)
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (i == head || (head >= 0 && n.realLife == head) || (!n.friendly && (n.boss || global::Levy.Common.Util.LevyUtil.IsBossNpc(n) || global::Levy.Common.Testing.WorldStep.IsNaturalHostile(n)))) n.active = false;
				}
			}
			void ResetCounters()
			{
				BossSlots.Failed = BossSlots.Freed = BossSlots.FreedCritters = BossSlots.FreedHostiles = BossSlots.FreedReleased = BossSlots.FreedSummons = BossSlots.FreedPacked = BossSlots.FreedLevies = BossSlots.Unanswered = 0;
				MachineDormancy.PackedForBoss = MachineDormancy.WakeHeld = 0; MachineDormancy.BossPackLog.Clear(); MachineBossRoomSystem.ReserveGarrison = MachineBossRoomSystem.ReservePacked = 0;
			}
			int Summon(out int head) => Summon2(out head, out _);
			int Summon2(out int head, out int brought)
			{
				brought = 0;
				var was = new bool[Main.maxNPCs]; for (int i = 0; i < Main.maxNPCs; i++) was[i] = Main.npc[i].active;
				head = NPC.NewNPC(new EntitySource_BossSpawn(p), (int)_cmdAt.X, (int)_cmdAt.Y - 1600, bossType, 1);
				if (head < 0 || head >= Main.maxNPCs || Main.npc[head].type != bossType) { head = -1; return -1; }
				Main.npc[head].timeLeft *= 20;
				StepSafe(3);
				int segs = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].realLife == head && i != head) segs++;
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && !Main.npc[i].friendly && (!was[i] || i == head)) brought++;
				return segs;
			}
			try
			{
				ArmoryWith(cx, -10, ItemID.Handgun, ItemID.Minishark);
				UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 4), new Vector2((cx + 2) * 16f, (FloorY - 4) * 16f), out _);
				UseItem(p, new Item(ModContent.ItemType<TurretItem>(), 4), new Vector2((cx + 14) * 16f, (FloorY - 4) * 16f), out _);
				UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 4), new Vector2((cx + 26) * 16f, (FloorY - 5) * 16f), out _);
				UseItem(p, new Item(ModContent.ItemType<DroneItem>(), 4), new Vector2((cx + 38) * 16f, (FloorY - 5) * 16f), out _);
				for (int k = 0; k < 4; k++) { NPC l = MakeLevy(new Vector2((cx - 6 - 3 * k) * 16f, FloorY * 16f)); if (l != null) levies.Add(l); }
				StepSafe(150);
				DChk($"room for a boss: two turrets, two placed drones and four levies stand (turrets {CountNpcs<TurretNPC>()}, drones {FieldDrones()}, levies {Levies()})", CountNpcs<TurretNPC>() == 2 && FieldDrones() == 2 && Levies() == 4);
				if (Machines() != 4 || Levies() != 4) return;
				foreach (TurretNPC t in Turrets()) { t.NPC.life -= 11; t.NPC.direction = -1; t.NPC.spriteDirection = -1; }
				foreach (DroneNPC d in Fielders()) { d.NPC.life -= 9; d.RestoreFromPack(37f); }

				// control: the same boss with room to spare takes nothing and says how many slots it needs
				ResetCounters();
				int segs0 = Summon2(out int head0, out int need);
				DNote($"room for a boss: {bossName} with room to spare spawned {segs0} segments and took {need} NPC slots");
				DChk($"room for a boss, control: {bossName} with room to spare spawns whole and packs nothing (segments {segs0}, machines {Machines()}, freed {BossSlots.Freed})", head0 >= 0 && segs0 >= 10 && Machines() == 4 && BossSlots.Freed == 0);
				KillBoss(head0); StepSafe(90);
				if (head0 < 0 || segs0 < 10) return;

				// the trial: a stand-in boss keeps the fight on whatever the worm does; friendly fillers nothing may take leave 3 slots fewer than the worm needs
				standIn = Target(cx - 150, 0);
				if (standIn != null) { standIn.boss = true; standIn.friendly = false; standIn.dontTakeDamage = true; }
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.friendly && !n.townNPC && n.ModNPC == null && n.lifeMax < 100000) n.active = false; } // the world's critters would be taken first
				for (int guard = 0; guard < Main.maxNPCs && FreeSlots() > need - 3; guard++)
				{
					int idx = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (cx - 40) * 16, (FloorY - 3) * 16, NPCID.BlueSlime);
					if (idx < 0 || idx >= Main.maxNPCs) break;
					NPC n = Main.npc[idx]; n.aiStyle = -1; n.noGravity = true; n.noTileCollide = true; n.lifeMax = n.life = 100000; n.friendly = true; n.dontTakeDamage = true; n.damage = 0; fillers.Add(n);
				}
				ResetCounters();
				var tRec = new Dictionary<int, (Vector2 pos, int weapon)>(); var tDir = new Dictionary<int, int>();
				foreach (TurretNPC t in Turrets()) tRec[t.RecordId] = (t.NPC.position, t.MountedWeaponType);
				int free0 = FreeSlots(), leviesBefore = Levies();
				int segs = Summon(out int head);
				int others = BossSlots.FreedCritters + BossSlots.FreedHostiles + BossSlots.FreedReleased + BossSlots.FreedSummons;
				string tally = $"free before {free0} for {need}; freed {BossSlots.Freed}: critters {BossSlots.FreedCritters}, hostiles {BossSlots.FreedHostiles}, garrison {BossSlots.FreedReleased}, summoned {BossSlots.FreedSummons}, machines packed {BossSlots.FreedPacked}, levies {BossSlots.FreedLevies}";
				DNote("room for a boss, trial: " + tally + $"; segments {segs}; pack distances " + string.Join(", ", MachineDormancy.BossPackLog.ConvertAll(e => $"{e.packed / 16f:0}t (farthest left {e.farthestLeft / 16f:0}t)")));
				DChk($"room for a boss: {bossName} spawns whole into a world 3 slots short (segments {segs} of {segs0})", head >= 0 && segs == segs0);
				DChk($"room for a boss: the room comes from packing placed machines and not one levy is benched ({tally}; levies {leviesBefore} -> {Levies()}, squeeze {BossSlots.Squeeze})", BossSlots.FreedPacked >= 2 && BossSlots.FreedPacked + others == 3 && BossSlots.FreedLevies == 0 && BossSlots.Squeeze == 0 && Levies() == leviesBefore);
				bool farthestFirst = MachineDormancy.BossPackLog.Count >= 2; foreach (var e in MachineDormancy.BossPackLog) farthestFirst &= e.packed >= e.farthestLeft;
				DChk($"room for a boss: the machines farthest from the spawn pack first ({MachineDormancy.BossPackLog.Count} packs)", farthestFirst);
				DChk($"room for a boss: what was packed is a record, not a loss (records {TurretSystem.Instance.RecordCount}+{FieldDroneSystem.Instance.RecordCount}, dormant {TurretSystem.Instance.DormantCount}+{FieldDroneSystem.Instance.DormantCount}, standing {Machines()})", TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2 && TurretSystem.Instance.DormantCount + FieldDroneSystem.Instance.DormantCount == BossSlots.FreedPacked + MachineBossRoomSystem.ReservePacked && Machines() == 4 - BossSlots.FreedPacked - MachineBossRoomSystem.ReservePacked);

				// the reserve the boss keeps: free slots under it, so the last machine packs too; a levy is never benched for it
				StepSafe(90);
				DChk($"room for a boss: the boss's reserve ({BossSlots.ExtraReserve} slots) packs the machine left standing and benches no levy (standing {Machines()}, reserve packs {MachineBossRoomSystem.ReservePacked}, levies {Levies()}, squeeze {BossSlots.Squeeze})", BossSlots.ExtraReserve > 0 && Machines() == 0 && MachineBossRoomSystem.ReservePacked >= 1 && Levies() == leviesBefore && BossSlots.Squeeze == 0);
				var dRec = new Dictionary<int, (int life, float bat, int weapon)>(); var tLife = new Dictionary<int, int>();
				bool recOk = true; string recWhy = "";
				foreach (var r in TurretSystem.Instance.Records)
				{
					tLife[r.Id] = r.Life; tDir[r.Id] = r.Dir;
					if (!tRec.TryGetValue(r.Id, out var w) || Vector2.Distance(r.Position, w.pos) > 0.01f || (r.Dir != -1 && r.Dir != 1) || r.Weapon != w.weapon || !r.Dormant || !r.ForBoss) { recOk = false; recWhy += $" turret {r.Id} dormant {r.Dormant} forBoss {r.ForBoss} dir {r.Dir} weapon {r.Weapon};"; }
				}
				foreach (var r in FieldDroneSystem.Instance.Records)
				{
					dRec[r.Id] = (r.Life, r.Battery, r.Weapon);
					if (!r.Dormant || !r.ForBoss || r.Life <= 0 || !lastBat.TryGetValue(r.Id, out float lb) || Math.Abs(r.Battery - lb) > 1f) { recOk = false; recWhy += $" drone {r.Id} dormant {r.Dormant} forBoss {r.ForBoss} life {r.Life} battery {r.Battery:0.0} (had {(lastBat.TryGetValue(r.Id, out float hb) ? hb.ToString("0.0") : "?")});"; }
				}
				DChk("room for a boss: each packed record holds its position, health, facing, weapon and battery" + (recWhy.Length > 0 ? " (" + recWhy + ")" : ""), recOk);

				// the boss lives on, with free slots and a player beside them: they stay packed
				foreach (NPC n in fillers) n.active = false;
				fillers.Clear();
				StepSafe(600);
				DChk($"room for a boss: with the boss alive and a player beside them none wakes, even with free slots (standing {Machines()}, free {FreeSlots()}, wakes held {MachineDormancy.WakeHeld}, boss alive {BossSlots.BossAlive})", BossSlots.BossAlive && Machines() == 0 && MachineDormancy.WakeHeld > 0);

				// the boss is gone: they wake as they were
				KillBoss(head);
				if (standIn != null) { standIn.active = false; standIn = null; }
				int woke = -1; for (int k = 0; k < 240 && woke < 0; k++) { StepSafe(1); if (Machines() == 4) woke = k + 1; }
				DChk($"room for a boss: the boss gone, all four stand again (after {woke} ticks; reserve {BossSlots.ExtraReserve}, boss alive {BossSlots.BossAlive})", woke > 0 && !BossSlots.BossAlive && BossSlots.ExtraReserve == 0);
				StepSafe(3);
				bool same = true; string why = "";
				foreach (TurretNPC t in Turrets())
				{
					if (!tRec.TryGetValue(t.RecordId, out var w)) { same = false; why += " unknown turret;"; continue; }
					if (Vector2.Distance(t.NPC.position, w.pos) > 0.5f) { same = false; why += $" turret {t.RecordId} moved;"; }
					if (t.NPC.life != tLife[t.RecordId]) { same = false; why += $" turret {t.RecordId} life {t.NPC.life} vs {tLife[t.RecordId]};"; }
					if (t.NPC.direction != tDir[t.RecordId]) { same = false; why += $" turret {t.RecordId} faces {t.NPC.direction} vs {tDir[t.RecordId]};"; }
					if (t.MountedWeaponType != w.weapon) { same = false; why += $" turret {t.RecordId} weapon {t.MountedWeaponType} vs {w.weapon};"; }
				}
				foreach (DroneNPC d in Fielders())
				{
					if (!dRec.TryGetValue(d.RecordId, out var w)) { same = false; why += " unknown drone;"; continue; }
					if (d.NPC.life != w.life) { same = false; why += $" drone {d.RecordId} life {d.NPC.life} vs {w.life};"; }
					if (Math.Abs(d.Battery - w.bat) > 1f) { same = false; why += $" drone {d.RecordId} battery {d.Battery:0.0} vs {w.bat:0.0};"; }
					if (d.FieldWeaponType != w.weapon) { same = false; why += $" drone {d.RecordId} weapon {d.FieldWeaponType} vs {w.weapon};"; }
				}
				DChk("room for a boss: every machine woke exactly as it was packed" + (why.Length > 0 ? " (" + why + ")" : ""), same);
				bool flags = true;
				foreach (var r in TurretSystem.Instance.Records) flags &= !r.Dormant && !r.ForBoss;
				foreach (var r in FieldDroneSystem.Instance.Records) flags &= !r.Dormant && !r.ForBoss;
				DChk($"room for a boss: nothing lost and nothing left marked (records {TurretSystem.Instance.RecordCount}+{FieldDroneSystem.Instance.RecordCount}, levies {Levies()})", flags && TurretSystem.Instance.RecordCount == 2 && FieldDroneSystem.Instance.RecordCount == 2 && Levies() == leviesBefore);
			}
			finally
			{
				_keepBossParts = false;
				if (standIn != null) standIn.active = false;
				var left = new List<string>();
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.townNPC) continue;
					bool fight = n.boss || n.realLife >= 0 || n.type == bossType || global::Levy.Common.Util.LevyUtil.IsBossNpc(n) || global::Levy.Common.Testing.WorldStep.IsNaturalHostile(n);
					if (!fight) continue;
					if (n != standIn && left.Count < 12) left.Add(n.TypeName + "#" + n.type);
					n.active = false; // the fight leaves nothing behind for the checks after this one
				}
				if (left.Count > 0) DNote("room for a boss: cleared after the fight: " + string.Join(", ", left));
				foreach (NPC n in fillers) n.active = false;
				foreach (NPC l in levies) l.active = false;
				global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.Name == "Pvt. MachineDorm");
				Main.dayTime = day; Main.time = time;
				ResetMachines(); ClearPlaced();
			}
		}

		/// <summary>A Regular levy body for player 0 (immortal for the check), through the roster's own spawn.</summary>
		private static NPC MakeLevy(Vector2 at)
		{
			var roster = global::Levy.Common.Systems.RosterSystem.Instance;
			var rec = new global::Levy.Common.Soldiers.SoldierRecord
			{
				Id = roster.NextId++, Kind = global::Levy.Common.SoldierKind.Regular, Grade = global::Levy.Common.Grade.Levy, Temperament = global::Levy.Common.Temperament.Sellsword, Male = true, Name = "Pvt. MachineDorm",
				CommanderName = Main.player[0].name, CommanderIndex = 0, LifeCrystals = global::Levy.Common.LevyConstants.MaxLifeCrystals, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Order = global::Levy.Common.SoldierOrder.Hold;
			roster.Regulars.Add(rec);
			int body = roster.SpawnBody(rec, at);
			if (body < 0 || body >= Main.maxNPCs || !rec.HasBody) return null;
			NPC n = Main.npc[body]; n.Bottom = at; n.immortal = true;
			return n;
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
