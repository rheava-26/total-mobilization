using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PILLAR SCENE for the calrev harness (lab "lagsweep", 2026-11-17): a Celestial Pillar event the way Eve fought it, for measuring what the army costs a tick when the whole army is in the fight at once.
	/// Boss lines <c>solarp nebulap vortexp stardustp</c>: the pillar stands where a boss would (its shield is held up for the whole fight, as it is while its enemies are being cleared), the Lunar Events are on,
	/// the commander stands in the pillar's zone, and the pillar's own enemies keep arriving: every 6 ticks one more is set down at a random spot of the arena (left or right of the army, the flyers in the air, the walkers on
	/// the floor) while fewer than <c>pw=N</c> (default 30) are alive, the way the pillar's spawn rate keeps the zone full. The worm's body segments do not count. The enemies are the fight's adds (damage to them is the army's damage).
	/// Measurement only: nothing here changes product code.
	/// </summary>
	public partial class CombatHarness
	{
		internal static bool PwIsPillar(string b) => b is "solarp" or "nebulap" or "vortexp" or "stardustp";

		internal static int PwTower(string b) => b switch { "solarp" => NPCID.LunarTowerSolar, "nebulap" => NPCID.LunarTowerNebula, "vortexp" => NPCID.LunarTowerVortex, "stardustp" => NPCID.LunarTowerStardust, _ => -1 };

		/// <summary>The pillar's enemies (heads only for the worms) and whether each one flies.</summary>
		private static (int type, bool flies)[] PwPool(string b) => b switch
		{
			"solarp" => new (int, bool)[] { (NPCID.SolarCorite, true), (NPCID.SolarSolenian, false), (NPCID.SolarDrakomire, false), (NPCID.SolarSroller, false), (NPCID.SolarSpearman, false), (NPCID.SolarDrakomireRider, true), (NPCID.SolarCrawltipedeHead, true) },
			"nebulap" => new (int, bool)[] { (NPCID.NebulaBrain, true), (NPCID.NebulaHeadcrab, true), (NPCID.NebulaBeast, false), (NPCID.NebulaSoldier, false) },
			"vortexp" => new (int, bool)[] { (NPCID.VortexHornetQueen, true), (NPCID.VortexHornet, true), (NPCID.VortexRifleman, false), (NPCID.VortexSoldier, false) },
			"stardustp" => new (int, bool)[] { (NPCID.StardustCellBig, true), (NPCID.StardustJellyfishBig, true), (NPCID.StardustSpiderBig, false), (NPCID.StardustSoldier, false), (NPCID.StardustWormHead, true) },
			_ => Array.Empty<(int, bool)>()
		};

		/// <summary>Body parts and children that are part of an enemy, not one of the wave: never counted toward the wave size.</summary>
		private static readonly HashSet<int> PwParts = new() { NPCID.SolarCrawltipedeBody, NPCID.SolarCrawltipedeTail, NPCID.StardustWormBody, NPCID.StardustWormTail, NPCID.SolarGoop, NPCID.VortexLarva, NPCID.StardustCellSmall, NPCID.StardustJellyfishSmall, NPCID.StardustSpiderSmall };

		/// <summary>Part / add types of a pillar fight (from BbSetup).</summary>
		private static void PwSetup(CrState s, CrRun run)
		{
			s.PartTypes.Add(s.BossType);
			foreach (var (t, _) in PwPool(run.Boss)) s.AddTypes.Add(t);
			foreach (int t in PwParts) s.AddTypes.Add(t);
			PwSpawned = 0; PwMaxAlive = 0;
		}

		internal static int PwSpawned, PwMaxAlive;

		private static void PwEvent(string b, bool on)
		{
			NPC.LunarApocalypseIsUp = on;
			NPC.TowerActiveSolar = on && b == "solarp"; NPC.TowerActiveNebula = on && b == "nebulap"; NPC.TowerActiveVortex = on && b == "vortexp"; NPC.TowerActiveStardust = on && b == "stardustp";
			int sh = on ? NPC.ShieldStrengthTowerMax : 0;
			NPC.ShieldStrengthTowerSolar = NPC.TowerActiveSolar ? sh : 0; NPC.ShieldStrengthTowerNebula = NPC.TowerActiveNebula ? sh : 0; NPC.ShieldStrengthTowerVortex = NPC.TowerActiveVortex ? sh : 0; NPC.ShieldStrengthTowerStardust = NPC.TowerActiveStardust ? sh : 0;
		}

		/// <summary>The commander stands in the pillar's zone (the dummies of the levies mirror it).</summary>
		private static void PwZones(Player p, CrRun r)
		{
			p.ZoneTowerSolar = r.Boss == "solarp"; p.ZoneTowerNebula = r.Boss == "nebulap"; p.ZoneTowerVortex = r.Boss == "vortexp"; p.ZoneTowerStardust = r.Boss == "stardustp";
		}

		/// <summary>Every tick of a pillar fight: the event stays up, the shield stays up, the wave is topped up.</summary>
		private void PwTick(CrState st)
		{
			if (st?.Run == null || !PwIsPillar(st.Run.Boss)) return;
			PwEvent(st.Run.Boss, true);
			var pool = PwPool(st.Run.Boss); int alive = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i]; if (!n.active || !st.AddTypes.Contains(n.type)) continue;
				if (n.timeLeft < 600) n.timeLeft = 600; // the pillar's zone holds them: they never wander off and despawn
				if (!PwParts.Contains(n.type)) alive++;
			}
			PwMaxAlive = Math.Max(PwMaxAlive, alive);
			int want = st.Run.Pw >= 0 ? st.Run.Pw : 30;
			if (st.Tick % 6 != 0 || alive >= want) return;
			bool worm = false; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && (Main.npc[i].type == NPCID.SolarCrawltipedeHead || Main.npc[i].type == NPCID.StardustWormHead)) { worm = true; break; }
			var (type, flies) = pool[Main.rand.Next(pool.Length)];
			if ((type == NPCID.SolarCrawltipedeHead || type == NPCID.StardustWormHead) && worm) (type, flies) = pool[0]; // one worm at a time, as the zone has
			int side = Main.rand.NextBool() ? 1 : -1, dx = Main.rand.Next(24, 100);
			int x = (_crCx + side * dx) * 16, y = flies ? (CrF - Main.rand.Next(14, 60)) * 16 : (CrF - 3) * 16;
			int k = NPC.NewNPC(new EntitySource_WorldEvent("LevyPillarLab"), x, y, type);
			if (k >= 0 && k < Main.maxNPCs) { Main.npc[k].timeLeft = 99999; PwSpawned++; }
		}

		private static void PwEnd(CrRun run)
		{
			if (run == null || !PwIsPillar(run.Boss)) return;
			global::Levy.Levy.Instance.Logger.Info($"HARNESS-PW: {run.Key} pillar wave: {PwSpawned} enemies set down, at most {PwMaxAlive} alive at once (target {(run.Pw >= 0 ? run.Pw : 30)})");
			PwEvent(run.Boss, false);
		}
	}
}
