using System;
using Terraria.ModLoader;

namespace Levy.Common.Soldiers
{
	/// <summary>Fair-share arithmetic for scarce ammo and throwables. Pure, so the boot self-check can exercise it.</summary>
	public static class AmmoShare
	{
		/// <summary>
		/// The most one soldier may hold. Plenty (pool covers every user at the full <paramref name="max"/>) = max. Otherwise an equal split of the pool,
		/// raised to <paramref name="floor"/> so a soldier is never sent out with a token handful, and never above max. users below 1 count as 1.
		/// </summary>
		public static int Limit(long pool, int users, int max, int floor)
		{
			if (max <= 0) return 0;
			if (users < 1) users = 1;
			if (pool >= (long)users * max) return max;
			long share = Math.Max(0L, pool) / users;
			return (int)Math.Min(max, Math.Max(floor, share));
		}
	}

	/// <summary>Boot self-check of <see cref="AmmoShare.Limit"/> (WeaponWiringCheck style). Logs only; changes no state.</summary>
	public class AmmoShareCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			int pm = LevyConstants.AmmoPouchMax, fl = LevyConstants.AmmoFairShareFloor;
			var cases = new (string name, long pool, int users, int max, int floor, int expect)[]
			{
				("500 stock / 10 users", 500, 10, pm, fl, 50),
				("5000 stock / 10 users", 5000, 10, pm, fl, 500),
				("50000 stock / 10 users", 50000, 10, pm, fl, pm),
				("exactly enough (9990 / 10)", 9990, 10, pm, fl, pm),
				("just short (9989 / 10)", 9989, 10, pm, fl, 998),
				("tiny stock 30 / 10", 30, 10, pm, fl, fl),
				("no stock", 0, 5, pm, fl, fl),
				("no users counted", 400, 0, pm, fl, 400),
				("single user, huge stock", 1_000_000, 1, pm, fl, pm),
				("thrown 100 / 5 users (cap 60)", 100, 5, LevyConstants.ThrownCarryMax, LevyConstants.ThrownFairShareFloor, 20),
				("thrown 20 / 5 users", 20, 5, LevyConstants.ThrownCarryMax, LevyConstants.ThrownFairShareFloor, LevyConstants.ThrownFairShareFloor),
				("thrown 1000 / 5 users", 1000, 5, LevyConstants.ThrownCarryMax, LevyConstants.ThrownFairShareFloor, LevyConstants.ThrownCarryMax),
				("floor above max is clamped", 10, 2, 8, 50, 8),
			};
			int bad = 0;
			foreach (var c in cases)
			{
				int got = AmmoShare.Limit(c.pool, c.users, c.max, c.floor);
				if (got != c.expect) { bad++; Mod.Logger.Warn($"Levy ammo-share self-check FAILED: {c.name}: expected {c.expect}, got {got}"); }
			}
			if (bad == 0) Mod.Logger.Info($"Levy ammo-share self-check: {cases.Length} cases OK (ammo floor {fl}, pouch {pm}; thrown floor {LevyConstants.ThrownFairShareFloor}, cap {LevyConstants.ThrownCarryMax})");
		}
	}
}
