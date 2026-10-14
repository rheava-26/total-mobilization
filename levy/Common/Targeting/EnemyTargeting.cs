using Terraria;

namespace Levy.Common.Targeting
{
	/// <summary>Enemy → soldier targeting (On_NPC.TargetClosest detour + slot-255 swap). OWNED BY THE TARGETING AGENT.</summary>
	public static class EnemyTargeting
	{
		public static bool IsAllowlisted(NPC npc) => false;
	}
}
