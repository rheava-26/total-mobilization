using System;
using Terraria;
using ThoriumMod;
using ThoriumMod.Items;
using Levy.Content.NPCs;
using LevyThorium.Content;

namespace LevyThorium.Common
{
	/// <summary>
	/// Thorium Bard on a Levy soldier. Levy fires the instrument's real <c>Shoot</c> on the soldier's dummy Player, but never runs the item-use
	/// pipeline (<c>CanUseItem</c> / <c>UseAnimation</c>), so the Inspiration economy has to be supplied here:
	/// <list type="bullet">
	/// <item>the pool lives on the dummy's <see cref="ThoriumPlayer"/> (<c>bardResource</c>, <c>bardResourceMax</c>);</item>
	/// <item>its size follows the commander's permanent maximum (Inspiration Crystals);</item>
	/// <item>it refills on Thorium's own regeneration wheel, re-run here each tick because the dummy never gets <c>Player.Update</c>;</item>
	/// <item>each shot is gated on and charged against the weapon's real inspiration cost (<see cref="TryPlay"/>).</item>
	/// </list>
	/// Ally empowerments (the instrument's buff aura) are not applied: Thorium only buffs real players.
	/// </summary>
	public static class BardCompat
	{
		public static bool IsBardWeapon(Item item) => item != null && !item.IsAir && item.ModItem is BardItem;

		/// <summary>Inspiration cost of one play of this instrument for this soldier (Thorium's own formula, run on the dummy).</summary>
		public static int CostOf(Item item, Player dummy)
		{
			if (item.ModItem is not BardItem bi) return 0;
			try { return bi.GetInspirationCost(dummy); }
			catch (Exception) { return Math.Max(0, bi.InspirationCost); }
		}

		/// <summary>PreFireGate arm: true (and the cost is paid, with Thorium's post-spend regen penalty) when the pool covers one play; false = the soldier holds.</summary>
		public static bool TryPlay(SoldierNPC s, Item item)
		{
			Player d = s?.Dummy;
			if (d == null) return true;
			try
			{
				ThoriumPlayer tp = d.GetModPlayer<ThoriumPlayer>();
				int cost = CostOf(item, d);
				if (cost <= 0) return true;
				if (tp.bardResource < cost) return false;
				BardItem.ConsumeInspiration(d, cost);
				return true;
			}
			catch (Exception) { return true; } // never let a Thorium surprise stop a soldier from fighting
		}

		/// <summary>Per tick (server / single player), for a soldier carrying an instrument: pool size from the commander, then Thorium's regen wheel.</summary>
		public static void Tick(SoldierNPC s, SoldierThoriumGlobalNPC state)
		{
			Player d = s.Dummy;
			if (d == null) return;
			ThoriumPlayer tp = d.GetModPlayer<ThoriumPlayer>();

			// Pool size: the commander's permanent maximum (10 + Inspiration Crystals). The soldier's own bard gear may lift bardResourceMax2 above it.
			Player cmd = s.Commander;
			if (cmd == null && Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers && Main.player[Main.myPlayer].active) cmd = Main.player[Main.myPlayer];
			if (cmd != null)
			{
				int cmdMax = cmd.GetModPlayer<ThoriumPlayer>().bardResourceMax;
				tp.bardResourceMax = Math.Clamp(cmdMax, 10, 40);
			}
			tp.bardResourceMax2 = Math.Clamp(Math.Max(tp.bardResourceMax2, tp.bardResourceMax), tp.bardResourceMax, Math.Min(60, tp.bardResourceMax + 20));

			// A soldier is handed a full pool the first time this runs; after that only the regen wheel (and the spend) move it.
			if (!state.BardPoolInit) { tp.bardResource = tp.bardResourceMax2; state.BardPoolInit = true; }
			if (tp.bardResource > tp.bardResourceMax2) tp.bardResource = tp.bardResourceMax2;

			// Thorium's regen wheel, verbatim (ThoriumPlayer: runs once per tick while the pool is below its max).
			if (tp.bardResource < tp.bardResourceMax2)
			{
				if (tp.inspirationRegenTimer < 8f)
				{
					tp.inspirationRegenTimer += (int)((1f + tp.inspirationRegenBase) * tp.inspirationRegenBonus);
				}
				else
				{
					if (tp.inspirationRegenBase < 5f) tp.inspirationRegenBase += 0.15f;
					tp.inspirationRegenTimer = 0f;
					tp.bardResource++;
				}
				if (tp.bardResource >= tp.bardResourceMax2) tp.inspirationRegenBase = 0f;
			}
		}
	}
}
