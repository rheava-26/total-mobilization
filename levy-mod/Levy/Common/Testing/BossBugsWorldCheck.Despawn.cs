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
				if (levies) Levies(6, 3f, half);
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
				_cmdDead = true;
				int gone = -1, lockedAfter = 0, maxTl = 0, watched = 0; float minY = float.MaxValue, startY = Main.npc[who].Center.Y;
				_perTick = null;
				int watch = arm == "drawn" ? DespawnBudget : DespawnWatch;
				for (int t = 0; t < watch; t++)
				{
					Step(1);
					if (!Alive()) { gone = t; break; }
					NPC b = Main.npc[who]; watched++;
					if (Locked(b)) { if (EnemyTargeting.IsMachineBody(Main.npc[LevyNpcData.Of(b).SoldierTarget])) machineTicks++; else lockedAfter++; }
					maxTl = Math.Max(maxTl, b.timeLeft); minY = Math.Min(minY, b.Center.Y);
				}
				lockedFrac = watched > 0 ? lockedAfter / (float)watched : 0f;
				string seen = $"locked during the fight {lockedFight}/180 ticks, after the death {lockedAfter} ticks on levies and {machineTicks} on machines, max timeLeft after {maxTl}, rose {(startY - Math.Min(minY, startY)) / 16f:0} tiles (top y {minY / 16f:0})";
				return (gone, seen);
			}
			finally
			{
				_cmdDead = false; _perTick = null;
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
