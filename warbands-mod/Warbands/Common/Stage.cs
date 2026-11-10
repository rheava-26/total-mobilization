using Terraria;
using Levy.Common.Weapons;

namespace Warbands.Common
{
	/// <summary>
	/// Where the world is, as a stage 0..6. Read from Levy's own world-progress number (<see cref="WeaponRank.Progress"/>, Boss Checklist scale: Eye 2, evil boss 3, Skeletron 5, Wall of Flesh 7, Twins 9,
	/// Plantera 12, Golem 13), so a Calamity or Thorium boss moves the stage the same way it moves Levy's weapon ranking. Falls back to vanilla flags if Levy's table is not up.
	/// </summary>
	public static class Stage
	{
		public const int Count = 7;
		public static readonly string[] Names = { "pre-boss", "post-EoW/BoC", "post-Skeletron", "early Hardmode", "post-Mechs", "post-Plantera", "post-Golem" };
		/// <summary>Lowest Boss Checklist progress value of each stage.</summary>
		public static readonly float[] Floor = { 0f, 3f, 5f, 7f, 9f, 12f, 13f };
		/// <summary>Highest item tier (same scale) a stage's roster may carry; the self-check holds every roster item to it.</summary>
		public static readonly float[] ItemCeiling = { 4f, 6f, 7f, 10f, 12f, 14f, 18f };

		public static int Current => FromProgress(Progress());

		public static float Progress()
		{
			float p = -1f;
			try { p = WeaponRank.Progress(); } catch { }
			if (p >= 0f) return p;
			if (NPC.downedGolemBoss) return 13f;
			if (NPC.downedPlantBoss) return 12f;
			if (NPC.downedMechBossAny) return 9f;
			if (Main.hardMode) return 7f;
			if (NPC.downedBoss3) return 5f;
			if (NPC.downedBoss2) return 3f;
			return 0f;
		}

		public static int FromProgress(float p)
		{
			int s = 0;
			for (int i = 0; i < Count; i++) if (p >= Floor[i]) s = i;
			return s;
		}
	}
}
