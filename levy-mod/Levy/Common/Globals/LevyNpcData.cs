using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Globals
{
	/// <summary>
	/// Per-enemy scratch data shared by Enemy targeting (SoldierTarget), the boss rule (EngagedCp, spite) and loot credit.
	/// Pure data: hooks live in each owner's own GlobalNPC class (multiple GlobalNPC classes are fine).
	/// </summary>
	public class LevyNpcData : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public static LevyNpcData Of(NPC npc) => npc.GetGlobalNPC<LevyNpcData>();

		// ---- enemy -> soldier targeting ----
		public int SoldierTarget = -1;
		public int RetargetTimer;

		// ---- boss rule ----
		public int EngagedCp;
		public readonly HashSet<int> EngagedSoldierIds = new();
		public int SpiteTarget = -1;
		public int SpiteTimer;
		public int RedirectCounter;
		public int LastPhase;
		/// <summary>Soldier whoAmI -> ticks spent within the spite distance.</summary>
		public readonly Dictionary<int, int> NearTicks = new();
		/// <summary>Soldier whoAmI -> recent damage samples (tick, damage).</summary>
		public readonly Dictionary<int, Queue<(int tick, int dmg)>> RecentDamage = new();

		// ---- misc ----
		public int LastHitBySoldier = -1;
	}
}
