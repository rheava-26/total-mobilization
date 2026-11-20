using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// DESPAWN: the commander dies. A boss beaten before keeps fighting levies within LostFightLeashTiles; otherwise (unbeaten, levies too far, only drones) it leaves and despawns
		// =====================================================================================================
		/// <summary>True while the stand-in commander is held dead (KeepCommander leaves him dead in place).</summary>
		private static bool _cmdDead;
		/// <summary>Despawn budget after the commander dies, in ticks (vanilla's own flee + CheckActive takes 1 to 4 s for these bosses).</summary>
		private const int DespawnBudget = 900, DespawnWatch = 1800;

		private static void HoldCommanderDead(Player p)
		{
			p.dead = true; p.statLife = 0; p.respawnTimer = 600; p.ghost = false;
		}

		/// <summary>Active levy NPCs (alive or not) and how many of them are alive, for the trace.</summary>
		private static string LevyNpcCount()
		{
			int a = 0, l = 0;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is global::Levy.Content.NPCs.SoldierNPC s) { a++; if (s.IsAlive) l++; }
			return $"{l}/{a} alive";
		}

		private static int CalType(string name) => ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind(name, out ModNPC mn) ? mn.Type : -1;

		/// <summary>One fight: the boss is fought for 3 s, then the commander dies; returns the ticks until the boss is gone (-1: still there after the watch) and what the boss did meanwhile.
		/// The "drawn" arm (the boss stays) is watched for <see cref="DespawnBudget"/> ticks only; <paramref name="lockedFrac"/> is the share of them it spent locked on a levy, <paramref name="machineTicks"/> the ticks it was locked on a drone or turret.</summary>
		private static (int gone, string seen) DespawnTrial(int type, string arm) => DespawnTrial(type, arm, out _, out _);
		private static (int gone, string seen) DespawnTrial(int type, string arm, out float lockedFrac, out int machineTicks)
		{
			ClearHostiles();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is global::Levy.Content.NPCs.SoldierNPC) n.active = false; }
			global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			_cmdDead = false;
			var cfg = global::Levy.Common.Config.LevyConfig.Get;
			bool savedEnemies = cfg?.EnemiesTargetSoldiers ?? true;
			lockedFrac = 0f; machineTicks = 0;
			try
			{
				bool far = arm == "far", leash = arm == "leash", drones = arm == "drones";
				// far: the army stands 370 tiles off, past the boss's own leash (Dragonfolly leaves beyond 350); leash: 240 tiles off, past LostFightLeashTiles (200) but inside every boss's own leash
				float half = far ? 185f : leash ? 120f : 0f;
				PlaceCommander(-half);
				bool levies = arm != "none" && !drones, drawn = arm == "drawn" || arm == "off" || far || leash || drones;
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, drawn);
				SetBossesTarget(drawn);
				if (cfg != null && arm == "off") cfg.EnemiesTargetSoldiers = false;
				EnemyTargeting.RefreshConfig();
				var army = levies ? Levies(6, 3f, half) : new List<global::Levy.Content.NPCs.SoldierNPC>();
				if (arm == "drawn") foreach (var l in army) l.NPC.immortal = true; // the beaten boss keeps fighting them: they must outlive the watch (a levy is pinned to 500 HP)
				if (drones) Machines(_cmdAt, 3, 3); // three field drones and three turrets beside the commander, and no levies: machines alone must not hold the boss
				Step(2);
				NPC boss = SpawnNpc(type, _cmdAt + new Vector2(0, -30 * 16f));
				if (boss == null || boss.type != type) return (-2, "did not spawn");
				boss.timeLeft = NPC.activeTime;
				int who = boss.whoAmI;
				bool Alive() => Main.npc[who].active && Main.npc[who].type == type;
				int lockedFight = 0;
				_perTick = () => { if (Alive() && Locked(Main.npc[who])) lockedFight++; };
				Step(180);
				if (!Alive()) return (-3, "left before the commander died");
				_cmdDead = true; EnemyTargeting.WhyTrack = new string[Main.maxNPCs + 1]; EnemyTargeting.WhyCount = new int[Main.maxNPCs + 1]; string trace = "";
				int gone = -1, lockedAfter = 0, maxTl = 0, watched = 0; float minY = float.MaxValue, startY = Main.npc[who].Center.Y;
				if (far || leash) { Main.npc[who].Center = _cmdAt + new Vector2(0, -30 * 16f); Main.npc[who].velocity = Vector2.Zero; } // the boss chased the commander toward the army; it starts the lost fight where it began, the full distance from the levies
				float nearLevy = float.MaxValue; foreach (var l in army) if (l.NPC.active) nearLevy = Math.Min(nearLevy, Vector2.Distance(l.NPC.Center, Main.npc[who].Center) / 16f);
				trace = $" nearest levy at the death {(nearLevy == float.MaxValue ? "none" : nearLevy.ToString("0") + " tiles")};" + trace;
				_perTick = arm == "drawn" ? () => { foreach (var l in army) if (l.NPC.active) { l.NPC.life = l.NPC.lifeMax; if (l.Record != null) l.Record.CurrentHp = l.NPC.lifeMax; } } : null;
				int watch = arm == "drawn" ? DespawnBudget : DespawnWatch;
				// who closes the gap: the boss's and each levy's travel since the death, and the first tick a levy stood within the leash of the boss
				Vector2 boss0 = Main.npc[who].Center; var levy0 = new List<Vector2>(); foreach (var l in army) levy0.Add(l.NPC.Center);
				float minNear = nearLevy; int inLeashAt = -1;
				for (int t = 0; t < watch; t++)
				{
					Step(1);
					if (!Alive()) { gone = t; break; }
					NPC b = Main.npc[who]; watched++;
					float near = float.MaxValue, levyMoved = 0f;
					for (int k = 0; k < army.Count; k++) { var l = army[k]; if (!l.NPC.active) continue; near = Math.Min(near, Vector2.Distance(l.NPC.Center, b.Center) / 16f); levyMoved = Math.Max(levyMoved, Vector2.Distance(l.NPC.Center, levy0[k]) / 16f); }
					minNear = Math.Min(minNear, near);
					if (inLeashAt < 0 && near <= LevyConstants.Targeting.LostFightLeashTiles)
					{
						inLeashAt = t;
						trace += $" [a levy first within {LevyConstants.Targeting.LostFightLeashTiles} tiles at t{t}: {near:0} tiles; the boss had moved {Vector2.Distance(b.Center, boss0) / 16f:0} tiles ({(b.Center.X - boss0.X) / 16f:+0;-0} x, {(b.Center.Y - boss0.Y) / 16f:+0;-0} y), the levies up to {levyMoved:0} tiles]";
					}
					if (Locked(b)) { if (EnemyTargeting.IsMachineBody(Main.npc[LevyNpcData.Of(b).SoldierTarget])) machineTicks++; else lockedAfter++; }
					if (t == 1 || t == 10 || t == 60 || t == 300) trace += $" [t{t}: target {b.target}, lock {LevyNpcData.Of(b).SoldierTarget}, ai0 {b.ai[0]:0}, searches {EnemyTargeting.WhyCount[who]}, why \"{EnemyTargeting.WhyTrack[who]}\", bodies {EnemyTargeting.Bodies.Count}, levy NPCs {LevyNpcCount()}]";
					maxTl = Math.Max(maxTl, b.timeLeft); minY = Math.Min(minY, b.Center.Y);
				}
				lockedFrac = watched > 0 ? lockedAfter / (float)watched : 0f;
				if (army.Count > 0) trace += $" [closest a levy came: {minNear:0} tiles]";
				string seen = $"locked during the fight {lockedFight}/180 ticks, after the death {lockedAfter} ticks on levies and {machineTicks} on machines, max timeLeft after {maxTl}, rose {(startY - Math.Min(minY, startY)) / 16f:0} tiles (top y {minY / 16f:0});{trace}";
				return (gone, seen);
			}
			finally
			{
				_cmdDead = false; _perTick = null; EnemyTargeting.WhyTrack = null; EnemyTargeting.WhyCount = null;
				if (cfg != null) cfg.EnemiesTargetSoldiers = savedEnemies;
				EnemyTargeting.RefreshConfig();
				SetBossesTarget(null);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles(); ClearMachines();
			}
		}

		private static void Despawn()
		{
			Arena(FloorY, Half);
			bool cal = ModLoader.HasMod("CalamityMod");
			SetRevenge(cal, cal); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			var bosses = new List<(string name, int type)> { ("Duke Fishron", NPCID.DukeFishron), ("Empress of Light", NPCID.HallowBoss) };
			if (cal) { bosses.Insert(0, ("Dragonfolly", CalType("Dragonfolly"))); bosses.Insert(1, ("Plaguebringer Goliath", CalType("PlaguebringerGoliath"))); }
			string[] arms = (Environment.GetEnvironmentVariable("LEVY_DESPAWN_ARMS") ?? "none,drawn,far,leash,drones,undrawn,off").Split(',');
			foreach (var (name, type) in bosses)
			{
				if (type <= 0) { Check($"{name}: the type resolves", false); continue; }
				foreach (string arm in arms)
				{
					var (gone, seen) = DespawnTrial(type, arm, out float lockedFrac, out int machineTicks);
					string label = arm switch { "none" => "no levies", "drawn" => "beaten before, 6 levies beside it", "far" => "beaten, 6 levies 370 tiles from the commander", "leash" => "beaten, 6 levies 240 tiles off (past the 200-tile leash)", "drones" => "beaten, no levies, 3 drones + 3 turrets beside it", "undrawn" => "6 levies, never beaten (a first fight)", "off" => "6 levies, enemy targeting off", _ => arm };
					Note($"{name} [{label}]: {(gone >= 0 ? $"gone {gone} ticks ({gone / 60f:0.0} s) after the commander died" : gone == -1 ? $"STILL ALIVE {(arm == "drawn" ? DespawnBudget : DespawnWatch) / 60} s after the commander died" : seen)}; {seen}");
					if (gone < -1) { Check($"{name} [{label}]: the trial runs ({seen})", false); continue; }
					if (arm == "drawn")
					{
						Check($"{name} [{label}]: keeps fighting the levies {DespawnBudget / 60} s after the commander's death", gone == -1 && lockedFrac >= 0.5f, () => $"gone {gone}, locked on levies {lockedFrac:P0}; " + seen);
						Check($"{name} [{label}]: never locks a drone or turret", machineTicks == 0, () => seen);
						continue;
					}
					Check($"{name} [{label}]: despawns within {DespawnBudget / 60} s of the commander's death", gone >= 0 && gone <= DespawnBudget, () => seen);
					if (arm == "drones") Check($"{name} [{label}]: never locks a drone or turret", machineTicks == 0, () => seen);
				}
			}
		}
	}
}
