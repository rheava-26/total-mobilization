using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// DESPAWN: the commander dies; a flying boss leaves and despawns, levies or not
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

		/// <summary>One fight: the boss is fought for 3 s, then the commander dies; returns the ticks until the boss is gone (-1: still there after the watch) and what the boss did meanwhile.</summary>
		private static (int gone, string seen) DespawnTrial(int type, string arm)
		{
			ClearHostiles();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is global::Levy.Content.NPCs.SoldierNPC) n.active = false; }
			global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			_cmdDead = false;
			var cfg = global::Levy.Common.Config.LevyConfig.Get;
			bool savedEnemies = cfg?.EnemiesTargetSoldiers ?? true;
			try
			{
				bool far = arm == "far";
				PlaceCommander(far ? -185 : 0); // far: the army stands 370 tiles off, past the boss's own leash (Dragonfolly leaves beyond 350)
				bool levies = arm != "none", drawn = arm == "drawn" || arm == "off" || far;
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, drawn);
				SetBossesTarget(drawn);
				if (cfg != null && arm == "off") cfg.EnemiesTargetSoldiers = false;
				EnemyTargeting.RefreshConfig();
				if (levies) Levies(6, 3f, far ? 185 : 0);
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
				int gone = -1, lockedAfter = 0, maxTl = 0; float minY = float.MaxValue, startY = Main.npc[who].Center.Y;
				_perTick = null;
				for (int t = 0; t < DespawnWatch; t++)
				{
					Step(1);
					if (!Alive()) { gone = t; break; }
					NPC b = Main.npc[who];
					if (Locked(b)) lockedAfter++;
					maxTl = Math.Max(maxTl, b.timeLeft); minY = Math.Min(minY, b.Center.Y);
				}
				string seen = $"locked during the fight {lockedFight}/180 ticks, after the death {lockedAfter} ticks, max timeLeft after {maxTl}, rose {(startY - Math.Min(minY, startY)) / 16f:0} tiles (top y {minY / 16f:0})";
				return (gone, seen);
			}
			finally
			{
				_cmdDead = false; _perTick = null;
				if (cfg != null) cfg.EnemiesTargetSoldiers = savedEnemies;
				EnemyTargeting.RefreshConfig();
				SetBossesTarget(null);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles();
			}
		}

		private static void Despawn()
		{
			Arena(FloorY, Half);
			bool cal = ModLoader.HasMod("CalamityMod");
			SetRevenge(cal, cal); Main.GameMode = GameModeID.Expert; Main.hardMode = true;
			var bosses = new List<(string name, int type)> { ("Duke Fishron", NPCID.DukeFishron), ("Empress of Light", NPCID.HallowBoss) };
			if (cal) { bosses.Insert(0, ("Dragonfolly", CalType("Dragonfolly"))); bosses.Insert(1, ("Plaguebringer Goliath", CalType("PlaguebringerGoliath"))); }
			string[] arms = (Environment.GetEnvironmentVariable("LEVY_DESPAWN_ARMS") ?? "none,drawn,far,undrawn,off").Split(',');
			foreach (var (name, type) in bosses)
			{
				if (type <= 0) { Check($"{name}: the type resolves", false); continue; }
				foreach (string arm in arms)
				{
					var (gone, seen) = DespawnTrial(type, arm);
					string label = arm switch { "none" => "no levies", "drawn" => "6 levies, the army draws it", "far" => "6 levies drawn, 370 tiles from the commander", "undrawn" => "6 levies, a first fight (not drawn)", "off" => "6 levies, enemy targeting off", _ => arm };
					Note($"{name} [{label}]: {(gone >= 0 ? $"gone {gone} ticks ({gone / 60f:0.0} s) after the commander died" : gone == -1 ? $"STILL ALIVE {DespawnWatch / 60} s after the commander died" : seen)}; {seen}");
					if (gone < -1) { Check($"{name} [{label}]: the trial runs ({seen})", false); continue; }
					Check($"{name} [{label}]: despawns within {DespawnBudget / 60} s of the commander's death", gone >= 0 && gone <= DespawnBudget, () => seen);
				}
			}
		}
	}
}
