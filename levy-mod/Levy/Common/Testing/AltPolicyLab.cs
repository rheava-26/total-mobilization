using System;
using System.Collections.Generic;
using System.Text;
using Terraria;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ALT-FIRE HARNESS SWITCHES (harness only; inert unless LEVY_HARNESS and LEVY_ALT_MODE are both set). The decision itself lives in <see cref="Weapons.AltPolicies"/>; this class only chooses which of
	/// three behaviours the comparison runs and counts what happened:
	///   LEVY_ALT_MODE=left    every alt is refused: the soldier fires only the left-click attack (and no held right button)
	///   LEVY_ALT_MODE=random  the old behaviour: each attack is the alt with probability AltUseChance (0.8), whatever the weapon or the situation
	///   LEVY_ALT_MODE=policy  the production registry decides (AltPolicies.Want)
	/// LEVY_HARNESS_CROWD=N pins N slimes 2.5 tiles apart around the target; LEVY_ALT_TOUGH=0/1 forces the "tough target" reading (the pinned target has a million life, so it would always read tough).
	/// Per-item counters (alt asked / let through / refused, primary shots, held-right-button sets) are appended to the trial's notes column.
	/// </summary>
	public static class AltPolicyLab
	{
		internal static readonly string Mode = (Environment.GetEnvironmentVariable("LEVY_ALT_MODE") ?? "").Trim().ToLowerInvariant();
		internal static readonly int CrowdN = Math.Max(1, Math.Min(12, int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_CROWD"), out int cn) ? cn : 1));
		internal static readonly int ToughEnv = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ALT_TOUGH"), out int te) ? te : -1;

		private static readonly Dictionary<string, int[]> Count = new(); // name -> { asked, passed, refused, primaryShots, holdSet }
		private static int[] C(Item item) { string n = item.ModItem?.Name ?? item.Name; if (!Count.TryGetValue(n, out int[] a)) Count[n] = a = new int[5]; return a; }

		internal static void Asked(Item item, bool ok) { int[] c = C(item); c[0]++; if (ok) c[1]++; else c[2]++; }
		internal static void Primary(Item item) => C(item)[3]++;
		internal static void Hold(Item item) => C(item)[4]++;

		internal static string TakeNotes()
		{
			if (!Testing.CombatHarness.Enabled || Mode == "" || Count.Count == 0) { Count.Clear(); return ""; }
			var sb = new StringBuilder();
			sb.Append(" ALT[").Append(Mode).Append(CrowdN > 1 ? ",crowd" + CrowdN : "").Append("]:");
			foreach (var kv in Count) sb.Append(' ').Append(kv.Key).Append(" asked=").Append(kv.Value[0]).Append(" pass=").Append(kv.Value[1]).Append(" veto=").Append(kv.Value[2]).Append(" prim=").Append(kv.Value[3]).Append(kv.Value[4] > 0 ? " hold=" + kv.Value[4] : "");
			Count.Clear();
			return sb.ToString();
		}
	}
}
