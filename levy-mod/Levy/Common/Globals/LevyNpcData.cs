using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;

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

		// ---- soldier whip tag (see SoldierWhip / SoldierMinion) ----
		/// <summary>Game tick the tag runs out (0 = never tagged) and the soldier whoAmI that lashed it.</summary>
		public int TagExpireTick;
		public int TagSoldier = -1;
		public bool IsTagged => TagExpireTick > (int)Main.GameUpdateCount;
		/// <summary>Marks an enemy as tagged by a soldier's whip for <see cref="LevyConstants.TagDurationTicks"/>; a fresh lash refreshes it.</summary>
		public static void ApplyTag(NPC npc, int soldierIndex)
		{
			if (npc == null || !npc.active) return;
			var d = Of(npc);
			d.TagExpireTick = (int)Main.GameUpdateCount + LevyConstants.TagDurationTicks;
			d.TagSoldier = soldierIndex;
		}
		public static bool IsNpcTagged(NPC npc) => npc != null && npc.active && Of(npc).IsTagged;
	}
}
