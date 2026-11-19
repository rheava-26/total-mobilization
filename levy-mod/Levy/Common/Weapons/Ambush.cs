using System;
using System.Collections.Generic;
using Terraria;

namespace Levy.Common.Weapons
{
	/// <summary>The four steps of a rogue levy's ambush cycle (SoldierNPC.Ambush.cs).</summary>
	internal enum AmbushPhase : byte { Off, FallBack, Charge, Strike }

	/// <summary>
	/// THE ROGUE AMBUSH (Eve, 11-19: "run back, charge their stealth, run in, throw a couple of hits, run back... time it with their adrenaline and rage... skirmishers rather than friendly soldiers").
	/// A Calamity rogue levy whose weapon wants its stealth strike works a cycle instead of strafing in the pocket: FALL BACK to a safe spot (out of the target's reach, away from hostile shots, inside its
	/// leash), CHARGE standing still so the bar fills at the standing rate, STRIKE (close to the weapon's reach, throw the strike and a couple of plain throws), and fall back again. Weapons whose plain throws
	/// beat the strike (<see cref="RogueMode.Spam"/>) keep throwing at their own rate. The cycle is skipped (the levy just throws) when the target is nearly dead or the commander is in danger.
	/// This class holds the switches, the per-weapon exceptions and the lab counters; the movement and the phase machine live on the levy (SoldierNPC.Ambush.cs), the fire gate in <see cref="StealthPolicy"/>.
	/// </summary>
	internal static class Ambush
	{
		/// <summary>Env LEVY_AMBUSH: 0 off (the levy strafes and the stealth policy alone decides), 1 the cycle for weapons that want the strike (default), 2 the cycle for every rogue weapon (lab).</summary>
		internal static int Mode = Read("LEVY_AMBUSH", 1);
		/// <summary>Env LEVY_AMBUSH_PLAIN: plain throws after the strike before falling back (default 2).</summary>
		internal static readonly int PlainAfter = Math.Max(0, Read("LEVY_AMBUSH_PLAIN", 2));

		/// <summary>Weapons measured to lose with the cycle (rogue army sweep, ambush on against off): they keep the old behaviour. Item full name (Mod/Item).</summary>
		internal static readonly HashSet<string> Off = new(StringComparer.Ordinal)
		{
		};

		/// <summary>A boss (or a hard hitter's) danger radius in tiles from its hitbox: the charge spot is at least this far. Bigger bodies push it out.</summary>
		internal const float SafeTilesBoss = 16f, SafeTilesMob = 9f, SafeTilesMax = 30f;
		/// <summary>The charge spot sits this far past the danger radius when the weapon reaches that far (a strike from where the bar was charged needs no run in).</summary>
		internal const float ChargeSlackTiles = 4f;
		/// <summary>Bounds (ticks): a fall back that has not arrived charges where it is; a charge that never fills strikes anyway; a strike window that never lands falls back.</summary>
		internal const int FallBackMaxTicks = 180, ChargeMaxTicks = 600, StrikeMaxTicks = 300, PlainWindowTicks = 150;
		/// <summary>A target this close (tiles from its hitbox, as a share of the danger radius) to a charging levy sends it back.</summary>
		internal const float CrowdedFrac = 0.7f;
		/// <summary>A levy hit this recently while charging finds another spot.</summary>
		internal const int HitRepickTicks = 20;
		/// <summary>The commander is in danger below this share of her life with a boss after her, or below <see cref="CmdCriticalLife"/> with any hostile near.</summary>
		internal const float CmdDangerLife = 0.5f, CmdCriticalLife = 0.3f;
		/// <summary>A target with no more life than this many plain throws is finished without the cycle.</summary>
		internal const float DyingThrows = 4f;

		/// <summary>Lab counters: phase entries, completed cycles (fall back -> charge -> strike -> fall back), strikes thrown in the window, fire holds while falling back or charging, cycles skipped and why, spots re-picked.</summary>
		internal static int FallBacks, Charges, Strikes, Cycles, StrikesThrown, FireHolds, SkipDying, SkipCommander, Repicks, ModeAligned;

		private static int Read(string n, int d) { string v = Environment.GetEnvironmentVariable(n); return !string.IsNullOrEmpty(v) && int.TryParse(v, out int x) ? x : d; }

		/// <summary>True when this weapon's plain throws keep the bar (the Doomsday Device's full-charge grenade): its own fire rule runs in every phase, the cycle only moves the body.</summary>
		internal static bool KeepsBar(Item item) => RogueGrenade.Is(item);

		/// <summary>The weapon is on the measured exception list.</summary>
		internal static bool Excepted(Item item) => item?.ModItem != null && Off.Contains(item.ModItem.Mod.Name + "/" + item.ModItem.Name);
	}
}
