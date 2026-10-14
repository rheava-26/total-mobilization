using Terraria;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>Soldier → enemy choice, never-target list, flee list. OWNED BY THE TARGETING AGENT.</summary>
	public static class SoldierTargeting
	{
		/// <summary>Best enemy within <paramref name="rangeTiles"/> with line of sight, or -1.</summary>
		public static int PickTarget(SoldierNPC s, float rangeTiles) => -1;
		public static bool IsNeverTarget(NPC npc) => true;
		public static bool IsFleeTarget(NPC npc) => false;
		/// <summary>Record that an enemy hurt a player or a soldier (priority targeting).</summary>
		public static void NoteAttacker(int npcIndex) { }
		public static bool IsAttackingUs(NPC npc) => false;
	}
}
