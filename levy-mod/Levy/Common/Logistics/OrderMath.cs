using System;
using System.Collections.Generic;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// The pure arithmetic of craft orders, buying and selling. No Terraria state: everything here takes numbers and returns numbers, so the boot self-check (<see cref="LogisticsCheck"/>) can pin it.
	/// </summary>
	public static class OrderMath
	{
		/// <summary>
		/// Splits <paramref name="total"/> units over lines in proportion to <paramref name="weights"/> by largest remainder: the shares always sum to exactly <paramref name="total"/>, and
		/// ties go to the earlier line. A line with weight 0 gets nothing; all-zero weights split evenly.
		/// </summary>
		public static int[] Split(int total, int[] weights)
		{
			int n = weights.Length; var res = new int[n];
			if (n == 0 || total <= 0) return res;
			long sum = 0; foreach (int w in weights) sum += Math.Max(0, w);
			var w2 = new long[n];
			for (int i = 0; i < n; i++) w2[i] = sum == 0 ? 1 : Math.Max(0, weights[i]);
			long s2 = sum == 0 ? n : sum;
			var rem = new long[n]; int given = 0;
			for (int i = 0; i < n; i++) { long num = w2[i] * total; res[i] = (int)(num / s2); rem[i] = num % s2; given += res[i]; }
			var order = new int[n]; for (int i = 0; i < n; i++) order[i] = i;
			Array.Sort(order, (a, b) => rem[a] != rem[b] ? rem[b].CompareTo(rem[a]) : a.CompareTo(b));
			for (int k = 0; given < total && k < n; k++) { if (w2[order[k]] == 0) continue; res[order[k]]++; given++; }
			return res;
		}

		/// <summary>
		/// Which line to make next. Bucket mode: the craftable line furthest behind its quota (largest unmet fraction), so a partial run keeps the ratio. Random mode: a weighted draw over the craftable lines with
		/// units left, weighted by the units still owed. Returns -1 when nothing is craftable or owed. <paramref name="roll"/> is a value in [0,1).
		/// </summary>
		public static int PickLine(int[] planned, int[] done, bool[] craftable, bool random, double roll)
		{
			int n = planned.Length; int best = -1;
			if (!random)
			{
				double bestFrac = 0;
				for (int i = 0; i < n; i++)
				{
					int left = planned[i] - done[i];
					if (left <= 0 || !craftable[i]) continue;
					double f = (double)left / planned[i];
					if (best < 0 || f > bestFrac + 1e-9) { best = i; bestFrac = f; }
				}
				return best;
			}
			long total = 0;
			for (int i = 0; i < n; i++) if (craftable[i] && planned[i] - done[i] > 0) total += planned[i] - done[i];
			if (total <= 0) return -1;
			double target = roll * total; long acc = 0;
			for (int i = 0; i < n; i++)
			{
				if (!craftable[i] || planned[i] - done[i] <= 0) continue;
				acc += planned[i] - done[i];
				best = i;
				if (target < acc) return i;
			}
			return best;
		}

		/// <summary>
		/// How many crafts of a recipe the stock allows: for each ingredient (count per craft) the stock divided by the per-craft need, minimum over ingredients, capped at <paramref name="want"/>.
		/// <paramref name="have"/> is looked up per ingredient index. A recipe with no ingredients is capped only by <paramref name="want"/>.
		/// </summary>
		public static int MaxCrafts(int want, int[] perCraft, Func<int, long> have)
		{
			long m = want;
			for (int i = 0; i < perCraft.Length; i++) { if (perCraft[i] <= 0) continue; m = Math.Min(m, have(i) / perCraft[i]); }
			return (int)Math.Max(0, m);
		}

		/// <summary>The crafts a partial run can make now and what is left for later: with 100 of 120 bars at 6 bars per item and 20 wanted, 16 now and 4 later.</summary>
		public static (int now, int later) Partial(int want, int[] perCraft, Func<int, long> have) { int now = MaxCrafts(want, perCraft, have); return (now, want - now); }

		/// <summary>
		/// What an auto-buy may buy. Limited by the wanted count, by the stock the shop has (<paramref name="shopStock"/>, -1 = unlimited), and by the budget: never more units than <c>budget / unitPrice</c>.
		/// A unit price of 0 or less is free and only the count limits it.
		/// </summary>
		public static int BuyCount(int want, long unitPrice, long budget, int shopStock)
		{
			if (want <= 0 || (budget <= 0 && unitPrice > 0)) return 0;
			long n = want;
			if (unitPrice > 0) n = Math.Min(n, budget / unitPrice);
			if (shopStock >= 0) n = Math.Min(n, shopStock);
			return (int)Math.Max(0, n);
		}

		/// <summary>A tier band test: <paramref name="tier"/> is the item's progression tier (-1 = unknown, which no band selects).</summary>
		public static bool InTierBand(float tier, float lo, float hi) => tier >= 0f && tier >= lo && tier <= hi;

		/// <summary>The cells of a drag, normalised so the corners can be given in any order. Inclusive bounds.</summary>
		public static (int c0, int r0, int c1, int r1) DragCells(int ca, int ra, int cb, int rb) => (Math.Min(ca, cb), Math.Min(ra, rb), Math.Max(ca, cb), Math.Max(ra, rb));
	}
}
