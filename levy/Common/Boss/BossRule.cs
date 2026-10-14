using Microsoft.Xna.Framework;
using Terraria;
using Levy.Content.NPCs;

namespace Levy.Common.Boss
{
	/// <summary>Main-boss detection, terror, spite targets, projectile redirect, HP scaling. OWNED BY THE BOSS AGENT.</summary>
	public static class BossRule
	{
		public static bool IsMainBoss(NPC npc) => false;
		public static int TerrorOf(NPC boss) => 0;
		/// <summary>Nearest living main boss within range tiles of a point, or null.</summary>
		public static NPC NearestMainBossInView(Vector2 worldPos, float rangeTiles) => null;
		public static bool AnyMainBossAlive => false;
		/// <summary>Called whenever a soldier damages an enemy (HP scaling + spite accounting).</summary>
		public static void OnSoldierDamagedNpc(NPC target, SoldierNPC soldier, int damage) { }
		/// <summary>Internal name key for BOSS_x dialogue (e.g. "EyeOfCthulhu").</summary>
		public static string BossKey(NPC boss) => "";
	}
}
