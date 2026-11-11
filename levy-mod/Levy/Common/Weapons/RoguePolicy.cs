using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>How a Calamity rogue levy uses one weapon's stealth.</summary>
	internal enum RogueMode
	{
		/// <summary>Hold the throw until the stealth bar is full and throw the strike (its damage per second beats the rate of plain throws, which each empty the bar).</summary>
		Wait,
		/// <summary>Throw at the weapon's own rate; the strike is not worth the two seconds of waiting.</summary>
		Spam,
		/// <summary>Strike only when it pays: a boss in reach, or a crowd (the strike pierces, explodes or splits); plain throws otherwise.</summary>
		Situational,
	}

	/// <summary>
	/// PER-WEAPON ROGUE POLICY (data). Calamity empties the stealth bar on every throw that is not a stealth strike, so a rogue either throws plain at the weapon's use rate (no strikes) or waits out the bar
	/// (standing still about two seconds) and throws one strike. Which is better depends on the weapon: its use time, and what its strike is worth. Measured on a target dummy with a Statigel rogue levy
	/// (docs/fizzrogue/ROGUE.md): the strike cycle gives <c>strikeDamage / (fillTime + useTime)</c> against <c>damage / useTime</c> for spam. A weapon not in <see cref="Table"/> is classified by <see cref="Estimate"/>
	/// from its use time (the measured break-even), so a modded rogue weapon gets a sensible default.
	/// </summary>
	internal static class RoguePolicy
	{
		/// <summary>Weapons measured or decided by hand, by item full name (Mod/Item). The reason is the measured spam / strike damage per second on a dummy, or the strike's utility.</summary>
		internal static readonly Dictionary<string, RogueMode> Table = new(StringComparer.Ordinal)
		{
			// @@TABLE@@
		};

		/// <summary>Use time (ticks) from which an unlisted rogue weapon waits for its strike; below it the plain rate wins on a single target. Set from the measured weapons.</summary>
		internal const int WaitFromUseTime = 40;
		/// <summary>Use time under which an unlisted weapon only strikes for a boss or a crowd rather than ever waiting.</summary>
		internal const int SpamBelowUseTime = 16;

		internal static RogueMode For(Item item)
		{
			if (item == null || item.IsAir) return RogueMode.Wait;
			string key = item.ModItem != null ? item.ModItem.Mod.Name + "/" + item.ModItem.Name : null;
			if (key != null && Table.TryGetValue(key, out RogueMode m)) return m;
			return Estimate(item.useTime > 0 ? item.useTime : item.useAnimation);
		}

		/// <summary>The default for a weapon with no table row: very fast throws spam; slow throws wait; the middle strikes only when it pays.</summary>
		internal static RogueMode Estimate(int useTime)
		{
			if (useTime >= WaitFromUseTime) return RogueMode.Wait;
			if (useTime < SpamBelowUseTime) return RogueMode.Spam;
			return RogueMode.Situational;
		}
	}
}
