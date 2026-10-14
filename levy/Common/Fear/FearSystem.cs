using Microsoft.Xna.Framework;
using Terraria;
using Levy.Content.NPCs;

namespace Levy.Common.Fear
{
	/// <summary>Squad-wide fear events. OWNED BY THE FEAR AGENT (per-soldier meter lives in SoldierNPC.Fear.cs).</summary>
	public static class FearSystem
	{
		public static void OnComradeHit(SoldierNPC victim, int damage) { }
		public static void OnComradeDied(SoldierNPC victim) { }
		public static void OnPlayerDied(Player player) { }
		public static void OnBossKilled(NPC boss) { }
		public static void OnBossPhase(NPC boss, int phase) { }
		/// <summary>Rally: fear = RallyValue for soldiers within radius tiles.</summary>
		public static void Rally(Vector2 center, float radiusTiles) { }
		public static float GainMultiplier(SoldierNPC s) => 1f;
	}
}
