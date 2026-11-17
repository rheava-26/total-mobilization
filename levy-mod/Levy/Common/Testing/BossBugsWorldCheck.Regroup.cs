using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Potions;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// SPECTATOR FORFEIT: taking the buff off during a boss fight kills the commander ("<name> forfeited.")
		// SPECTATOR DRONES: drones, turrets and minions do not hold the bet open
		// REGROUP & ENGAGE: Search & destroy, Free rein for the levies in a boss fight, respawns beside the commander
		// =====================================================================================================
		private static BarracksTE NewBetBarracks()
		{
			var bar = TestTileEntities.Add<BarracksTE>(BetBarracksId, new Point16(Cx - 30, FloorY - 3)); bar.TownName = "BetCheckTown";
			return bar;
		}

		private static void BetSceneSetup()
		{
			Arena(FloorY, Half);
			SetRevenge(false, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.KingSlime, true);
			SetBossesTarget(true);
			On_Player.KillMe += BetKillMeDetour; _betHooked = true;
		}

		private static void BetSceneEnd()
		{
			if (_betHooked) { On_Player.KillMe -= BetKillMeDetour; _betHooked = false; }
			if (TileEntity.ByID.TryGetValue(BetBarracksId, out var betBar)) TestTileEntities.Remove(betBar);
			global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
		}

		private static void Forfeit()
		{
			BetSceneSetup();
			var bar = NewBetBarracks();
			Player me = Main.player[0];
			int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>();
			try
			{
				// (name, boss alive, how the buff comes off, expected death)
				var cases = new (string name, bool boss, string how, bool dies)[]
				{
					("cancelled in a boss fight", true, "cancel", true),
					("cancelled with no boss about", false, "cancel", false),
					("ran out in a boss fight", true, "expire", false),
				};
				foreach (var c in cases)
				{
					NewBetScene(bar, 12);
					_spectator = true; PlaceCommander(-20);
					var levies = Levies(3, 3f, 10f); foreach (var l in levies) l.Record.HomeBarracksId = BetBarracksId;
					Step(4);
					NPC boss = c.boss ? SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f)) : null;
					if (boss != null) _perTick = () => { if (boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } };
					BetStep(30);
					Check($"(Forfeit: {c.name}) the Spectator is on and the army stands", me.HasBuff(bt) && levies.All(l => l.NPC.active && l.IsAlive));
					int kills0 = _betKills; _betText = null;
					_spectator = false;
					if (c.how == "expire") { int ix = me.FindBuffIndex(bt); if (ix >= 0) me.buffTime[ix] = 1; BetStep(1); }
					me.ClearBuff(bt);
					BetStep(3);
					bool died = _betKills > kills0;
					Check($"(Forfeit: {c.name}) {(c.dies ? "the commander dies" : "no death")}", died == c.dies);
					if (c.dies) Check($"(Forfeit: {c.name}) the death message is \"{Commander} forfeited.\"", _betText == Commander + " forfeited.", () => "got \"" + _betText + "\"");
					_perTick = null; if (boss != null) boss.active = false;
				}
			}
			finally { BetSceneEnd(); }
		}

		private static void DronesDoNotHoldTheBet()
		{
			BetSceneSetup();
			var bar = NewBetBarracks();
			try
			{
				ModNPC drone = null; if (ModLoader.TryGetMod("Mechanization", out Mod mech)) mech.TryFind("DroneNPC", out drone);
				// levies dead, manpower 0, a drone in the fight: the bet is lost, with its existing lines
				for (int rep = 0; rep < 2; rep++)
				{
					NewBetScene(bar, 0);
					_spectator = true; PlaceCommander(-20);
					var levies = Levies(4, 3f, 10f); foreach (var l in levies) l.Record.HomeBarracksId = BetBarracksId;
					Step(4);
					NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f));
					NPC d = drone != null ? SpawnNpc(drone.Type, new Vector2((Cx + 20) * 16f, (_floorY - 12) * 16f)) : SpawnNpc(NPCID.Bunny, new Vector2((Cx + 20) * 16f, (_floorY - 3) * 16f)); // no Mechanization loaded: any non-levy body stands in
					_perTick = () => { if (boss != null && boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } if (d != null && d.active) d.timeLeft = 99999; };
					BetStep(30);
					int kills0 = _betKills; _betText = null;
					WipeArmy(levies);
					Check($"(Drones: {(drone != null ? "a drone" : "a stand-in")} flies) the army is down and the pool is empty (pool {bar.Manpower})", levies.All(l => !l.NPC.active || !l.IsAlive) && bar.Manpower == 0);
					BetStep(3);
					bool droneUp = d != null && d.active;
					Check("(Drones) the drone is still alive in the battle when the bet is judged", droneUp, () => d == null ? "no drone spawned" : "drone NPC went inactive");
					Check("(Drones) levies gone, manpower 0, a drone alive: the bet is lost", _betKills > kills0);
					Check("(Drones) the death message is one of the five lost-bet lines", Enumerable.Range(0, SpectatorBet.MessageCount).Any(k => SpectatorBet.Message(k, Commander) == _betText), () => "got \"" + _betText + "\"");
					Check("(Drones) the verdict ignores everything but levies and manpower", SpectatorBet.Lost(Main.player[0], true));
					_perTick = null; if (boss != null) boss.active = false; if (d != null) d.active = false;
					if (drone == null) break;
				}
			}
			finally { BetSceneEnd(); }
		}

		private static void RegroupOrder()
		{
			Arena(FloorY, Half);
			SetRevenge(false, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true; SetBossesTarget(true);
			Player me = Main.player[0];
			try
			{
				ClearHostiles(); RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
				PlaceCommander(-180);
				Step(4);
				Check("(Regroup) the order is its own byte and the Order packet accepts it", (byte)SoldierOrder.Regroup == 8 && SoldierNPC.StateAfterOrder(SoldierOrder.Regroup) == SoldierState.SearchDestroy);

				// ---- a levy raised under the order appears beside the commander, even when asked for 100 tiles away
				var far = new Vector2((Cx + 60) * 16f, _floorY * 16f);
				var a = Levy(far, SoldierOrder.Regroup);
				Check("(Regroup) a levy that spawns under the order appears within a few tiles of the commander", a != null && Vector2.Distance(a.NPC.Center, me.Center) / 16f <= 8f, () => a == null ? "no body" : (Vector2.Distance(a.NPC.Center, me.Center) / 16f).ToString("0.0") + " tiles");
				var control = Levy(far, SoldierOrder.SearchDestroy);
				Check("(Regroup) control: a Search & destroy levy spawns where it was asked", control != null && Vector2.Distance(control.NPC.Center, far) / 16f <= 8f);
				if (a == null) return;

				// ---- no boss: Search & destroy
				Step(60);
				Check("(Regroup) no boss: the effective order is Search & destroy", a.EffOrder == SoldierOrder.SearchDestroy && a.Record.Order == SoldierOrder.Regroup);
				Check("(Regroup) no boss: the levy acts in the Search & destroy state", a.State == SoldierState.SearchDestroy || a.State == SoldierState.Engage, () => "state " + a.State);
				Check("(Regroup) the order audit accepts the body", a.ObeysOrder(SoldierOrder.Regroup, out string why), () => why);

				// ---- a boss far from the levy, which is not engaged: still Search & destroy
				NPC boss = SpawnNpc(NPCID.KingSlime, new Vector2((Cx + 190) * 16f, (_floorY - 30) * 16f));
				_perTick = () => { if (boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; } };
				Step(30);
				Check("(Regroup) a boss is alive but far from a levy that is not engaged: Search & destroy", a.EffOrder == SoldierOrder.SearchDestroy || a.State == SoldierState.Engage || a.TargetIndex >= 0, () => "eff " + a.EffOrder + " state " + a.State + " dist " + (Vector2.Distance(boss.Center, a.NPC.Center) / 16f).ToString("0"));

				// ---- the fight reaches the levy: Free rein
				Note($"regroup: levy at {(a.NPC.Center / 16f).ToPoint()} commander at {(me.Center / 16f).ToPoint()} boss at {(boss.Center / 16f).ToPoint()}");
				_perTick = () => { if (boss.active) { boss.life = boss.lifeMax; boss.timeLeft = 99999; boss.Center = a.NPC.Center + new Vector2(25 * 16f, -10 * 16f); boss.velocity = Vector2.Zero; } };
				for (int k = 0; k < 20 && !(a.State == SoldierState.FreeRein || a.State == SoldierState.Engage); k++) Step(10); // the brain re-reads the order on its next think
				Check("(Regroup) a boss fight with the levy in it: Free rein", a.EffOrder == SoldierOrder.FreeRein && (a.State == SoldierState.FreeRein || a.State == SoldierState.Engage), () => "eff " + a.EffOrder + " state " + a.State + " bossActive " + boss.active + " fight " + SoldierNPC.BossFightNow + " dist " + (Vector2.Distance(boss.Center, a.NPC.Center) / 16f).ToString("0") + " isBoss " + boss.boss + " type " + boss.type + " :: " + a.OrderDiagnosis());
				Check("(Regroup) the levy in the fight does not teleport to the commander (Free rein never hops)", !SoldierNPC.CatchUpOrder(a.EffOrder));
				Check("(Regroup) the order audit still accepts the body in the fight", a.ObeysOrder(SoldierOrder.Regroup, out why), () => why);

				// ---- the boss dies: back to Search & destroy
				_perTick = null; boss.active = false;
				Step(60);
				Check("(Regroup) the boss is gone: back to Search & destroy", a.EffOrder == SoldierOrder.SearchDestroy && (a.State == SoldierState.SearchDestroy || a.State == SoldierState.Engage), () => "eff " + a.EffOrder + " state " + a.State);

				// ---- issuing it, save and load, the client snapshot
				RosterSystem.Instance.IssueOrder(me, SoldierOrder.Regroup, true);
				Check("(Regroup) the army-wide order reaches every regular", control.Record.Order == SoldierOrder.Regroup && a.Record.Order == SoldierOrder.Regroup);
				Step(10);
				Check("(Regroup) the levy that took the order acts in the Search & destroy state", control.EffOrder == SoldierOrder.SearchDestroy && (control.State == SoldierState.SearchDestroy || control.State == SoldierState.Engage), () => "state " + control.State);
				var back = SoldierRecord.Load(a.Record.Save());
				Check("(Regroup) the order survives Save and Load", back.Order == SoldierOrder.Regroup);
				using (var ms = new MemoryStream())
				{
					using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) a.Record.WriteSnapshot(w);
					ms.Position = 0;
					using var r = new BinaryReader(ms);
					Check("(Regroup) the order survives the roster snapshot to clients", SoldierRecord.ReadSnapshot(r).Order == SoldierOrder.Regroup);
				}
				Check("(Regroup) the orders wheel has its text", LevyUtilT("UI.Regroup") == "Regroup & engage" && !LevyUtilT("UI2.RegroupHint").StartsWith("Mods."));
			}
			finally { _perTick = null; SoldierNPC.BossFightForTest = null; }
		}

		private static string LevyUtilT(string key) => global::Levy.Common.Util.LevyUtil.T(key);
	}
}
