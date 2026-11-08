using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Levy.Common.Team;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PALADIN LAB (measurement only; spec token pl=shield|none|tankA|tankB on a calrev fight line with l3=1). Same army (weapons rolled by Lab3Roll from the seed, so identical across arms), no armor, max HP (15 crystals = 400),
	/// no potions, no accessories except the arm's:
	///   shield = every levy wears a Paladin's Shield;  none = nobody does;
	///   tankA  = one tank at the front (slot n-2) with the tank kit (Solar Flare armor, Worm Scarf, Frozen Turtle Shell, Ankh Shield, Obsidian Shield, Cobalt Shield, Endurance + Ironskin) flanked by two shield holders; the rest plain;
	///   tankB  = the same three slots without the tank kit (the two flankers keep their shields).
	/// Extra row per fight in out.pl.tsv.
	/// </summary>
	public partial class CombatHarness
	{
		private static int _plSavedSlots = -1;
		private static readonly string[] PlTankArmor = { "Solar Flare Helmet", "Solar Flare Breastplate", "Solar Flare Leggings" };
		private static readonly string[] PlTankAcc = { "Worm Scarf", "Frozen Turtle Shell", "Ankh Shield", "Obsidian Shield", "Cobalt Shield" };

		internal static void PlApply(CrRun r)
		{
			int n = r.Specs.Count, tank = n - 2;
			for (int i = 0; i < n; i++)
			{
				var sp = r.Specs[i];
				bool holder = r.Pl == "shield" || ((r.Pl == "tankA" || r.Pl == "tankB") && (i == tank - 1 || i == tank + 1));
				sp.Armor = Array.Empty<string>(); sp.Acc = holder ? new[] { "Paladin's Shield" } : Array.Empty<string>();
				sp.LifeCrystals = 15; sp.Potions = 0; sp.ManAtArms = false;
				if (r.Pl == "tankA" && i == tank)
				{
					sp.Tank = true;
					sp.Armor = PlTankArmor.All(a => CrTryItemId(a, out _)) ? PlTankArmor : Array.Empty<string>();
					sp.Acc = PlTankAcc.Where(a => CrTryItemId(a, out _)).ToArray();
				}
			}
			r.Desc = string.Join(" ; ", r.Specs.Select(sp => sp.Describe()));
		}

		private void PlBegin(CrRun run)
		{
			var c = global::Levy.Common.Config.LevyConfig.Get;
			if (_plSavedSlots < 0) _plSavedSlots = c.LevyAccessorySlots;
			c.LevyAccessorySlots = 5;
			Mod.Logger.Info($"HARNESS-PL: begin {run.Key} pl={run.Pl} n={run.N}");
		}
		private static void PlEnd() { if (_plSavedSlots >= 0) global::Levy.Common.Config.LevyConfig.Get.LevyAccessorySlots = _plSavedSlots; _plSavedSlots = -1; }
		private static void PlReset() { TeamPlayer.PaladinCuts = TeamPlayer.PaladinShares = TeamPlayer.PaladinRawShared = TeamPlayer.PaladinSoaks = TeamPlayer.PaladinHits = TeamPlayer.PaladinNested = 0; TeamPlayer.PaladinSoakDone = TeamPlayer.PaladinHitDmg = 0; _plTankDef = _plTankEnd = -1f; }

		private static float _plTankDef = -1f, _plTankEnd = -1f;
		private static void PlBuffs(CrLevy l, NPC n)
		{
			if (!l.Spec.Tank) return;
			n.AddBuff(BuffID.Endurance, 60 * 60 * 30); n.AddBuff(BuffID.Ironskin, 60 * 60 * 30);
		}

		private void PlSample(CrState st)
		{
			var t = st.Levies.FirstOrDefault(x => x.Spec.Tank); if (t == null || t.Dead) return;
			var n = Main.npc[t.Idx]; if (n.ModNPC is global::Levy.Content.NPCs.SoldierNPC sn) { _plTankDef = n.defense; _plTankEnd = sn.Endurance; }
		}

		private void PlFinish(CrRun run, CrState st, int end)
		{
			try
			{
				PlSample(st);
				string path = _crOut + ".pl.tsv";
				if (!File.Exists(path)) File.WriteAllText(path, "key\tpl\tseed\tresult\tsec\tn\tdeaths\tfirstDeathSec\ttakenTotal\ttakenBoss\trawShared\tsoakLifeLost\tsoaks\tshieldedHits\tshieldedHitDmg\tnested\ttankDead\ttankDeathSec\ttankTaken\ttankHits\ttankDef\ttankEndurance\tholdersDead\n");
				string f1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
				var dead = st.Levies.Where(x => x.Dead).ToList();
				var tank = st.Levies.FirstOrDefault(x => x.Spec.Tank);
				var holders = st.Levies.Where(x => x.Spec.Acc.Contains("Paladin's Shield")).ToList();
				long tb = st.Levies.Sum(x => x.TakenBoss), ta = st.Levies.Sum(x => x.TakenAdd), to = st.Levies.Sum(x => x.TakenOther);
				File.AppendAllText(path, string.Join("\t", new object[] { run.Key, run.Pl, run.Seed, st.Result, f1(end / 60.0), st.Levies.Count, dead.Count, dead.Count == 0 ? "-" : f1(dead.Min(x => x.DeathTick) / 60.0),
					tb + ta + to, tb, TeamPlayer.PaladinRawShared, TeamPlayer.PaladinSoakDone, TeamPlayer.PaladinSoaks, TeamPlayer.PaladinHits, TeamPlayer.PaladinHitDmg, TeamPlayer.PaladinNested,
					tank == null ? "-" : tank.Dead ? 1 : 0, tank == null || !tank.Dead ? "-" : f1(tank.DeathTick / 60.0), tank == null ? "-" : (tank.TakenBoss + tank.TakenAdd + tank.TakenOther).ToString(), tank == null ? "-" : tank.Hits.ToString(),
					f1(_plTankDef), f1(_plTankEnd), holders.Count(x => x.Dead) }) + "\n");
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS-PL: finish row failed: " + e.Message); }
		}
	}
}
