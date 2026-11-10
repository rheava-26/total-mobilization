using System;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Weapons;

namespace Warbands.Common
{
	/// <summary>
	/// The hostile-band stat rules (derived in warbands-mod/docs/TTK.md from the TtkLab numbers). A band member is an enemy that carries Levy gear, not a Levy: it is built from the stage, not from the gear's own numbers.
	///   LIFE     = stage reference life (about 1.5 times the toughest ordinary enemy of the stage) x the world mode's enemy life multiplier (Classic 1, Expert 2, Master 3), like vanilla's ScaleStats.
	///   DEFENSE  = the lower of the gear's real defense and Levy's own stage defense line (WeaponRank.StageDefense), so Turtle armor never makes a band member a wall for a mid-game weapon.
	///   DAMAGE   = the weapon's real hit x the mode's enemy damage multiplier, then scaled down until its sustained damage on the stage's typical player stays under the stage DPS cap.
	///   ENGAGE   = at most <see cref="EngageCap"/> members attack inside one window; the others close in, flank and juke.
	/// Everything is multiplied by the config LifeScale / DamageScale.
	/// </summary>
	public static class BandStats
	{
		/// <summary>Classic-mode life of a band member by stage 0..6 (R and M members take <see cref="RangedLifeShare"/> of it, E members <see cref="MeleeLifeShare"/>).</summary>
		public static readonly int[] StageLife = { 90, 180, 320, 555, 640, 750, 940 };
		public const float RangedLifeShare = 0.8f, MeleeLifeShare = 1.2f;
		/// <summary>Defense of the typical player at the stage (armor + accessories): the figure the DPS cap is computed against.</summary>
		public static readonly int[] TypicalPlayerDefense = { 13, 21, 30, 42, 52, 75, 80 };
		/// <summary>Sustained damage per second a ranged or magic member may deal to a player of the stage's typical defense, in Master (the median regular enemy x 0.6). Scaled by mode below.</summary>
		public static readonly float[] DpsCapMaster = { 37f, 33f, 55f, 82f, 70f, 34f, 40f };
		public const float MeleeDpsShare = 1.35f; // a melee member may out-damage a ranged one: it has to close in
		public const int EngageWindowTicks = 120;

		/// <summary>Players or levies a band member may attack at once: bands of up to 4 field two attackers, larger bands three.</summary>
		public static int EngageCap(int bandSize) => bandSize <= 4 ? 2 : 3;

		public static float LifeMult => Math.Max(1f, Main.GameModeInfo.EnemyMaxLifeMultiplier);
		public static float DamageMult => Math.Max(1f, Main.GameModeInfo.EnemyDamageMultiplier);

		public static int Life(int stage, char role)
		{
			stage = Math.Clamp(stage, 0, Stage.Count - 1);
			float share = role == 'E' ? MeleeLifeShare : RangedLifeShare;
			var cfg = WarbandsConfig.Get;
			return Math.Max(10, (int)Math.Round(StageLife[stage] * share * LifeMult * (cfg?.LifeScale ?? 1f)));
		}

		public static int Defense(int stage, int gearDefense)
		{
			stage = Math.Clamp(stage, 0, Stage.Count - 1);
			return Math.Min(gearDefense, WeaponRank.StageDefense[Math.Min(stage, WeaponRank.StageDefense.Length - 1)]);
		}

		/// <summary>Per-hit damage (before the target's defense) for a member whose weapon lands <paramref name="weaponHit"/> at <paramref name="hitsPerSecond"/>.</summary>
		public static int Hit(int stage, char role, float weaponHit, float hitsPerSecond)
		{
			stage = Math.Clamp(stage, 0, Stage.Count - 1);
			var cfg = WarbandsConfig.Get;
			float dmg = weaponHit * DamageMult;
			float refDef = TypicalPlayerDefense[stage];
			float eff = Main.masterMode ? 1f : Main.expertMode ? 0.75f : 0.5f;
			float cap = DpsCapMaster[stage] * (role == 'E' ? MeleeDpsShare : 1f) * (DamageMult / 3f); // the cap table is Master; other modes scale with the mode's damage multiplier
			float sustained = Math.Max(1f, dmg - refDef * eff) * Math.Max(0.1f, hitsPerSecond);
			if (sustained > cap) dmg = cap / Math.Max(0.1f, hitsPerSecond) + refDef * eff;
			return Math.Max(1, (int)Math.Round(dmg * (cfg?.DamageScale ?? 1f)));
		}
	}
}
