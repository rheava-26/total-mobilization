using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// PRIORITY: a boss, its parts and its adds take players and levies strictly before Mechanization's drones and turrets
		// CALCLONE (Calamity half): her shots hurt a drone or turret like a levy, and machines outside her arena never make her invulnerable
		// =====================================================================================================

		/// <summary>Mechanization's field drones (bound, so they are live target bodies) and turrets around <paramref name="at"/>; none when Mechanization is not loaded.</summary>
		private static List<NPC> Machines(Vector2 at, int drones, int turrets, float spreadTiles = 4f)
		{
			var made = new List<NPC>();
			if (!ModLoader.TryGetMod("Mechanization", out Mod mech)) return made;
			if (mech.TryFind("DroneNPC", out ModNPC dn))
			{
				for (int k = 0; k < drones; k++)
				{
					Vector2 pad = at + new Vector2((k - drones / 2) * spreadTiles * 16f, -6 * 16f);
					NPC d = SpawnNpc(dn.Type, pad);
					if (d == null || d.type != dn.Type) continue;
					d.ModNPC.GetType().GetMethod("BindField", BindingFlags.Public | BindingFlags.Instance)?.Invoke(d.ModNPC, new object[] { -(9100 + k), pad });
					made.Add(d);
				}
			}
			if (mech.TryFind("TurretNPC", out ModNPC tn))
			{
				for (int k = 0; k < turrets; k++)
				{
					NPC t = SpawnNpc(tn.Type, new Vector2(at.X + (k - turrets / 2 + 0.5f) * spreadTiles * 16f, (_floorY - 2) * 16f));
					if (t != null && t.type == tn.Type) made.Add(t);
				}
			}
			return made;
		}

		private static void ClearMachines()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && EnemyTargeting.IsMachineBody(n)) { n.life = 0; n.active = false; } }
		}

		/// <summary>True when this NPC is locked on a drone or turret.</summary>
		private static bool OnMachine(NPC n) => Locked(n) && EnemyTargeting.IsMachineBody(Main.npc[LevyNpcData.Of(n).SoldierTarget]);

		private static int LiveMachines() { int c = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && EnemyTargeting.IsMachineBody(Main.npc[i]) && Main.npc[i].ModNPC is ITargetBody b && b.IsAlive) c++; return c; }

		private static void BossPriority()
		{
			Arena(FloorY, Half);
			bool cal = ModLoader.HasMod("CalamityMod");
			SetRevenge(cal, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			if (!ModLoader.HasMod("Mechanization")) { Note("(Priority) Mechanization is not loaded: no machines to rank"); return; }
			var bosses = new List<(string name, int type)> { ("Eye of Cthulhu", NPCID.EyeofCthulhu), ("Duke Fishron", NPCID.DukeFishron) };
			if (cal) bosses.Add(("Calamitas Clone", CalType("CalamitasClone")));
			foreach (var (name, type) in bosses)
			{
				if (type <= 0) { Check($"(Priority) {name}: the type resolves", false); continue; }
				foreach (string arm in new[] { "levies", "player", "spectator" })
				{
					ClearHostiles(); ClearMachines();
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false; global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
					global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
					_spectator = arm == "spectator";
					global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, true);
					SetBossesTarget(true); EnemyTargeting.RefreshConfig();
					try
					{
						PlaceCommander(0);
						if (arm == "levies") foreach (var l in Levies(6, 3f, 12f)) l.NPC.immortal = true; // the army 12 tiles off (kept alive for the watch); the machines right under the boss, far closer to it than anything else
						Vector2 spawnAt = _cmdAt + new Vector2(0, -30 * 16f);
						var machines = Machines(spawnAt + new Vector2(0, 6 * 16f), 3, 3);
						Step(2);
						// counted before the boss arrives: a boss can destroy every machine during the watch
						int liveAtStart = LiveMachines(); bool machineBodies = EnemyTargeting.Bodies.Exists(bd => EnemyTargeting.IsMachineBody(bd.NPC));
						NPC boss = SpawnNpc(type, spawnAt);
						if (boss == null || boss.type != type) { Check($"(Priority) {name} [{arm}]: spawns", false); continue; }
						int who = boss.whoAmI;
						int ticks = 0, bossOnMachine = 0, familyOnMachine = 0, onLevy = 0, onPlayer = 0, family = 0;
						_perTick = () =>
						{
							NPC b = Main.npc[who];
							if (b.active && b.type == type) { b.life = b.lifeMax; b.timeLeft = 99999; }
							for (int i = 0; i < Main.maxNPCs; i++)
							{
								NPC n = Main.npc[i];
								if (!n.active || n.whoAmI == who || !EnemyTargeting.IsBossClass(n)) continue;
								family++;
								if (OnMachine(n) && arm != "spectator") familyOnMachine++; // an add may fall to a machine only with no valid player and no levy in reach
							}
						};
						for (int t = 0; t < 420; t++)
						{
							Step(1);
							NPC b = Main.npc[who];
							if (!b.active || b.type != type) break;
							ticks++;
							if (OnMachine(b)) bossOnMachine++;
							else if (Locked(b)) onLevy++;
							else if (b.target == 0) onPlayer++;
						}
						int live = LiveMachines();
						string seen = $"{ticks} ticks: on a levy {onLevy}, on the commander {onPlayer}, on a machine {bossOnMachine}; adds on a machine {familyOnMachine} of {family} add-ticks; {machines.Count} machines spawned, {liveAtStart} live target bodies when the boss came, {live} live after";
						Note($"(Priority) {name} [{arm}]: {seen}");
						Check($"(Priority) {name} [{arm}]: the machines are live target bodies when the boss arrives", liveAtStart > 0 && machineBodies, () => seen);
						Check($"(Priority) {name} [{arm}]: the boss never locks a drone or turret", bossOnMachine == 0, () => seen);
						if (arm != "spectator") Check($"(Priority) {name} [{arm}]: its adds never lock a drone or turret while a player or levy is valid", familyOnMachine == 0, () => seen);
						if (arm == "levies") Check($"(Priority) {name} [{arm}]: it fights the levies or the commander", onLevy + onPlayer >= ticks / 2, () => seen);
						if (arm == "player") Check($"(Priority) {name} [{arm}]: with no levies it fights the commander, not the machines", onPlayer >= ticks / 2, () => seen);
					}
					finally
					{
						_perTick = null; _spectator = false;
						try { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); } catch (Exception) { }
						global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
						ClearHostiles(); ClearMachines();
					}
				}
			}
		}

		private static void CalClonePart()
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod calMod)) return;
			Arena(FloorY, Half);
			SetRevenge(true, false); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			int type = CalType("CalamitasClone");
			if (type <= 0 || !ModLoader.HasMod("Mechanization")) { Check("(CalClone) Calamitas Clone and Mechanization resolve", false); return; }
			CalCloneDamage(calMod);
			CalCloneArena(type, false);
			CalCloneArena(type, true);
		}

		/// <summary>One real CalamitousDart at the dart's own damage, struck on a target at full life through the game's own Projectile.Damage (as the Shots part does); returns the hit's damage (-1: no hit in 4 tries).</summary>
		private static int DartHit(NPC target, int projType, int dmg)
		{
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			target.life = target.lifeMax; target.immune[0] = 0; target.immune[Main.maxPlayers] = 0;
			if (target.ModNPC is SoldierNPC s0 && s0.Record != null) s0.Record.CurrentHp = target.lifeMax;
			int before = target.life;
			int myKeep = Main.myPlayer; Main.myPlayer = 0; // an enemy's shot carries the local player's index, as it does in the lab and in a single-player world
			int p = Projectile.NewProjectile(new EntitySource_WorldEvent("BossBugsCheck"), target.Center, Vector2.Zero, projType, dmg, 0f, 0);
			Main.myPlayer = myKeep;
			if (p < 0 || p >= Main.maxProjectiles) return -1;
			Projectile pr = Main.projectile[p];
			try
			{
				for (int k = 0; k < 4; k++)
				{
					pr.Center = target.Center; pr.velocity = Vector2.Zero;
					DamageNow(pr);
					if (!target.active) return before;
					if (target.life != before) return before - target.life;
				}
				return -1;
			}
			finally { pr.active = false; }
		}

		private static void CalCloneDamage(Mod calMod)
		{
			if (!calMod.TryFind("CalamitousDart", out ModProjectile dart)) { Check("(CalClone) CalamitousDart resolves", false); return; }
			int dmg = 22;
			if (calMod.TryFind("CalamitasClone", out ModNPC cc)) { var f = cc.GetType().GetField("DartDamage", BindingFlags.Public | BindingFlags.Static); if (f != null) dmg = (int)f.GetValue(null); }
			ClearHostiles(); ClearMachines();
			try
			{
				PlaceCommander(-150);
				var lv = Levy(new Vector2((Cx + 0) * 16f, _floorY * 16f));
				var ms = Machines(new Vector2((Cx + 40) * 16f, (_floorY - 10) * 16f), 1, 1, 20f);
				NPC drone = ms.Find(m => m.ModNPC != null && m.ModNPC.Name == "DroneNPC"), turret = ms.Find(m => m.ModNPC != null && m.ModNPC.Name == "TurretNPC");
				Step(2);
				if (lv == null || drone == null || turret == null) { Check("(CalClone) a levy, a drone and a turret stand", false); return; }
				// an armed drone's defense: the rarity tier of an endgame weapon (what a player mounts by the time she fights Calamitas Clone)
				int armedDefense = global::Levy.Common.Weapons.DroneArsenal.DefenseFor(ItemID.Zenith);
				drone.defense = drone.defDefense = armedDefense;
				int hl = DartHit(lv.NPC, dart.Type, dmg), hd = DartHit(drone, dart.Type, dmg), ht = DartHit(turret, dart.Type, dmg);
				string seen = $"dart damage {dmg} (Revengeance, Expert): a bare levy took {hl}, a drone (defense {drone.defense}) {hd}, a turret (defense {turret.defense}) {ht}";
				Note("(CalClone) " + seen);
				Check("(CalClone) her dart hurts a levy", hl > 1, () => seen);
				Check("(CalClone) her dart hurts an armed drone for real damage (at least 10, and at least 40% of a bare levy's hit)", hd >= 10 && hd * 10 >= hl * 4, () => seen);
				Check("(CalClone) her dart hurts a turret for real damage (at least 10, and at least 40% of a bare levy's hit)", ht >= 10 && ht * 10 >= hl * 4, () => seen);
			}
			finally { ClearHostiles(); ClearMachines(); for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false; global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander); }
		}

		/// <summary>Machines outside her arena: with the army beside the commander, or (<paramref name="spectator"/>) with a hidden commander and no levies, so a machine is the only thing left to target.</summary>
		private static void CalCloneArena(int type, bool spectator)
		{
			ClearHostiles(); ClearMachines();
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, true);
			SetBossesTarget(true); EnemyTargeting.RefreshConfig();
			try
			{
				_spectator = spectator;
				PlaceCommander(-60);
				if (!spectator) Levies(6, 3f, -60f); // the army around the commander, inside the arena her Revengeance AI raises on him
				// three turrets and three drones 75 tiles off, past the arena's side wall (1,000 px from its centre); the drones fly in to fight her
				Machines(new Vector2((Cx + 15) * 16f, (_floorY - 3) * 16f), 3, 3);
				Step(2);
				NPC boss = SpawnNpc(type, _cmdAt + new Vector2(0, -25 * 16f));
				if (boss == null || boss.type != type) { Check("(CalClone) Calamitas Clone spawns", false); return; }
				int who = boss.whoAmI;
				_perTick = () => { NPC b = Main.npc[who]; if (b.active && b.type == type) { b.life = b.lifeMax; b.timeLeft = 99999; } }; // full life: no brothers, no bullet hell
				Step(120); // her intro
				int ticks = 0, onMachine = 0, shielded = 0, onLevy = 0;
				for (int t = 0; t < 600; t++)
				{
					Step(1);
					NPC b = Main.npc[who];
					if (!b.active || b.type != type) break;
					ticks++;
					if (OnMachine(b)) onMachine++; else if (Locked(b)) onLevy++;
					if (b.dontTakeDamage) shielded++;
				}
				string tag = spectator ? "a hidden commander, no levies" : "the army beside the commander";
				string seen = $"{ticks} ticks: on a levy {onLevy}, on a machine {onMachine}, invulnerable {shielded}; {LiveMachines()} machines live";
				Note($"(CalClone) arena [{tag}]: " + seen);
				Check($"(CalClone) [{tag}] with machines outside her arena she never locks one", onMachine == 0, () => seen);
				Check($"(CalClone) [{tag}] with machines outside her arena she never turns invulnerable", shielded == 0, () => seen);
				if (!spectator) Check("(CalClone) she fights the levies", onLevy >= ticks / 2, () => seen);
			}
			finally
			{
				_perTick = null; _spectator = false;
				try { var c = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (c.HasBuff(bt)) c.ClearBuff(bt); } catch (Exception) { }
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles(); ClearMachines();
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false; global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
		}
	}
}
