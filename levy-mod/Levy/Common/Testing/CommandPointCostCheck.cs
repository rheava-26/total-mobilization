using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check of the command point costs: a Regular costs 1, a Man-at-Arms 2 and a Champion 3 in the one table (<see cref="LevyConstants.GradeCpCost"/>), on the record, in the roster's
	/// used-points count, and in the promotion delta. Reserve holds none. Pure data (records with no bodies), cleaned up in the same call. Never throws.
	/// </summary>
	public class CommandPointCostCheck : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(Run);

		private void Run()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			var roster = RosterSystem.Instance; var added = new List<SoldierRecord>();
			try
			{
				if (roster == null) throw new InvalidOperationException("no roster system");
				Check(LevyConstants.GradeCpCost.Length == 3 && LevyConstants.GradeCpCost[0] == 1 && LevyConstants.GradeCpCost[1] == 2 && LevyConstants.GradeCpCost[2] == 3, "the table is 1 / 2 / 3");
				int before = roster.CommandPointsUsed;
				SoldierRecord Mk(int id, Grade g) { var r = new SoldierRecord { Id = id, Name = "Pvt. " + id, Kind = SoldierKind.Regular, Grade = g, CommanderName = "Cp Check" }; roster.Regulars.Add(r); added.Add(r); return r; }
				var lv = Mk(990101, Grade.Levy); var ma = Mk(990102, Grade.ManAtArms); var ch = Mk(990103, Grade.Champion);
				Check(lv.CommandPointCost == 1 && ma.CommandPointCost == 2 && ch.CommandPointCost == 3, $"record cost is 1 / 2 / 3 ({lv.CommandPointCost} / {ma.CommandPointCost} / {ch.CommandPointCost})");
				Check(lv.CommandPointsHeld == 1 && ma.CommandPointsHeld == 2 && ch.CommandPointsHeld == 3, "points held on duty are 1 / 2 / 3");
				Check(roster.CommandPointsUsed - before == 6, $"the roster counts 6 points for one of each ({roster.CommandPointsUsed - before})");
				ch.Reserve = true;
				Check(ch.CommandPointsHeld == 0 && roster.CommandPointsUsed - before == 3, "a Champion in reserve holds none");
				ch.Reserve = false;
				Check(LevyConstants.GradeCpCost[(int)Grade.Champion] - LevyConstants.GradeCpCost[(int)Grade.Levy] == 2 && LevyConstants.GradeCpCost[(int)Grade.ManAtArms] - LevyConstants.GradeCpCost[(int)Grade.Levy] == 1, "promotion deltas are 1 (to Man-at-Arms) and 2 (to Champion)");
			}
			catch (Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			finally { foreach (var r in added) roster?.Regulars.Remove(r); }
			if (failures.Count == 0) Mod.Logger.Info($"[CpCost] self-check ok ({ok} checks: Regular 1, Man-at-Arms 2, Champion 3)");
			else { Mod.Logger.Warn($"[CpCost] self-check {failures.Count} FAILURE(S) ({ok} ok) !!!"); foreach (var f in failures) Mod.Logger.Warn("  cpcost: " + f); }
		}
	}
}
