using Terraria;
using Terraria.ModLoader;
using Levy.Common;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// HOLDING THE BANNER. A levy standing near a Holding Banner carries this one buff: more defense, less damage taken and no knockback, under a single icon.
	/// Granted by <see cref="Levy.Common.Banners.BannerSystem"/> (server / single player, refreshed, never stacked; NPC.AddBuff syncs it). The buff is only the flag: its stat deltas are folded onto
	/// the soldier's dummy by <see cref="Apply"/> from SoldierNPC.ApplyBuffEffect, the same place Morale and the potion buffs land.
	/// </summary>
	public class HoldingBannerBuff : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true; // never persisted: the banner grants it again within a second
		}

		/// <summary>Lays the buff's stat deltas on a soldier's dummy: defense (<see cref="LevyConstants.Banner.Defense"/>), damage reduction (<see cref="LevyConstants.Banner.DamageReduction"/>) and knockback immunity.</summary>
		public static void Apply(Player d)
		{
			d.statDefense += LevyConstants.Banner.Defense;
			d.endurance += LevyConstants.Banner.DamageReduction;
			d.noKnockback = true;
		}
	}
}
