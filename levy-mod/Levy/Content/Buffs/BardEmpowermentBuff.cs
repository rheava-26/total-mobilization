using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// EMPOWERED. The flag a bard's music hangs on a soldier. The buff itself carries nothing: the empowerments it stands for (which stats, how strong, for how long) live with the
	/// Thorium compat, which lays them on the soldier's dummy through <c>SoldierNPC.ExtraBuffStats</c>. The buff exists so the icon shows, the timer is real and clients can see who is
	/// being played to. Granted only by the Thorium compat; without Thorium nothing ever applies it.
	/// </summary>
	public class BardEmpowermentBuff : ModBuff
	{
		/// <summary>Borrow the Happy! icon: a cheerful lift reads right for a tune, and ships no new art.</summary>
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Sunflower;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;        // a boon
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;     // never persisted: the next verse re-grants it
		}
	}
}
