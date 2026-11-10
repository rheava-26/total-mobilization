using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Budget;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// Boot self-check of the drone launch gate against the army entity budget, logged as "[Crowd] drone gate ... fail=N". A launch needs <see cref="DroneSystem.ReserveSlots"/> free NPC slots. The old gate wanted 60 free
	/// (a launch refused once 141 of the 200 slots were taken); the budget's reserve is 40 (refused only past 160), and drones stand down (oldest first) when the free slots fall under it.
	/// </summary>
	public class DroneBudgetCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			var fails = new System.Collections.Generic.List<string>();
			void Check(bool ok, string what) { if (!ok) fails.Add(what); }
			try
			{
				int reserve = DroneSystem.ReserveSlots;
				Check(CrowdBudget.On ? reserve == CrowdBudget.ReservedNpc : reserve == D.NpcSlotReserve, "the launch gate reads the budget's NPC reserve");
				bool Launches(int active, int res) => Main.maxNPCs - active >= res;
				Check(Launches(140, reserve) && !Launches(165, reserve), "launch gate: allowed with 140 NPCs up, refused with 165");
				if (CrowdBudget.On) Check(Launches(150, reserve) && !Launches(150, D.NpcSlotReserve), "a 150-NPC world (36 levies, 12 militia, an invasion) launches under the budget, was refused under the old 60");
				Check(D.NpcSlotReserve >= reserve || !CrowdBudget.On, "the budget never asks for more free slots than the old gate");
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fails.Count == 0) Mod.Logger.Info($"[Crowd] drone gate self-check ok fail=0 :: reserve {DroneSystem.ReserveSlots} free NPC slots (old {D.NpcSlotReserve}), field drones {Levy.Common.Budget.CrowdBudget.ReservedNpc}");
			else Mod.Logger.Error($"[Crowd] drone gate self-check FAILED fail={fails.Count}: " + string.Join(" | ", fails));
		}
	}
}
