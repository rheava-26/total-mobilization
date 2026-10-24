using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// DRYAD'S BLESSING. The Dryad's gift to the army: more defense, a steady life regeneration and thorns that turn a contact hit back on the attacker,
	/// all under a single icon. A boon on a soldier, refreshed by the Dryad's pulse and never stacked. The buff itself is only the flag; its stat deltas
	/// are folded onto the soldier's dummy by <see cref="DryadsBlessingBuff.Apply"/> from SoldierNPC.ApplyBuffEffect, the same place the vanilla potion buffs land.
	/// </summary>
	public class DryadsBlessingBuff : ModBuff
	{
		/// <summary>Borrow vanilla's own Dryad's Blessing icon: it is the same blessing, and ships no new art.</summary>
		public override string Texture => "Terraria/Images/Buff_" + BuffID.DryadsWard;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;        // a boon
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;     // never persisted: the Dryad re-grants it within a second of the army reloading
		}

		/// <summary>
		/// Lays the Blessing's stat deltas on a soldier's dummy player — defense, thorns — and returns the life-regen units it adds, which the caller sums into the soldier's regeneration
		/// (<see cref="DryadBlessingDefense"/>, <see cref="DryadThornsReflectFrac"/>, <see cref="DryadBlessingLifeRegen"/>).
		/// </summary>
		public static int Apply(Player d)
		{
			d.statDefense += DryadBlessingDefense;
			d.thorns += DryadThornsReflectFrac;
			return DryadBlessingLifeRegen;
		}
	}
}
