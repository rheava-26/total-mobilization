using System;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A/B switches for the combatfix changes (lab and self-check only; every change is ON in play). Env <c>LEVY_CF_OFF</c> is a bit mask that turns changes OFF:
	/// 1 = cursor puppet (a soldier's cursor-reading projectile never sees the commander's cursor), 2 = chain projectiles drawn from their soldier, 4 = shots that pass tiles need no line of sight,
	/// 8 = melee levies that have not hit anything close in on a boss, 16 = persistent-shot budget, 32 = rogue stealth patience.
	/// </summary>
	public static class CombatFix
	{
		public const int Cursor = 1, Draw = 2, Through = 4, Charge = 8, Budget = 16, Stealth = 32;
		private static readonly int _off = Read();
		private static int Read() { string v = Environment.GetEnvironmentVariable("LEVY_CF_OFF"); return !string.IsNullOrEmpty(v) && int.TryParse(v, out int m) ? m : 0; }
		public static bool On(int bit) => (_off & bit) == 0;
		/// <summary>Lab counters.</summary>
		public static int DrawSwaps, BudgetKills, BudgetKillsPerSoldier;
	}
}
