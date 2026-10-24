using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// MORALE. The one buff that lifts an army's spirits: more damage, more defense and more critical-hit chance together, under a single icon. The
	/// Princess keeps it on the whole army, the Guide on the soldiers around him, the Merchant on the few close to his cart. It is a boon on a soldier
	/// (or an officer avatar), refreshed by whichever aura grants it and never stacked: a soldier standing in two morale auras carries exactly one.
	/// The buff itself is only the flag; its stat deltas are folded onto the soldier's dummy by <see cref="MoraleBuff.Apply"/> from
	/// SoldierNPC.ApplyBuffEffect, the same place the vanilla potion buffs land.
	/// </summary>
	public class MoraleBuff : ModBuff
	{
		/// <summary>Borrow the Banner buff's icon: a rallying standard reads exactly right for morale, and ships no new art.</summary>
		public override string Texture => "Terraria/Images/Buff_" + BuffID.MonsterBanner;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;        // a boon
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;     // never persisted: a soldier on reload starts without it, and the aura re-grants it within a second
		}

		/// <summary>Lays Morale's stat deltas on a soldier's dummy player: damage, defense and crit chance (<see cref="MoraleDamage"/>, <see cref="MoraleDefense"/>, <see cref="MoraleCrit"/>).</summary>
		public static void Apply(Player d)
		{
			d.GetDamage(DamageClass.Generic) += MoraleDamage;
			d.statDefense += MoraleDefense;
			d.GetCritChance(DamageClass.Generic) += MoraleCrit;
		}
	}
}
