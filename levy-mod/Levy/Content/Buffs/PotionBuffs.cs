using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Potions;

namespace Levy.Content.Buffs
{
	/// <summary>Spectator: nothing targets the holder; the holder's weapons and building are off and they are tethered to the spot they drank it. All logic lives in <see cref="SpectatorPlayer"/>.</summary>
	public class SpectatorBuff : ModBuff
	{
		public override string LocalizationCategory => "Potions";
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Invisibility;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true; // the tether anchor is not saved, so the buff is not either
		}
	}

	/// <summary>Seeker: highlights the one item chosen when the potion was drunk. Drawn by <see cref="SeekerSystem"/>.</summary>
	public class SeekerBuff : ModBuff
	{
		public override string LocalizationCategory => "Potions";
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Spelunker;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}
	}

	/// <summary>Autonomous: marks the levies of a Spectator who studied the Guide to Autonomy. The behaviour is in <see cref="AutonomySystem"/>; there is no stat change.</summary>
	public class AutonomousBuff : ModBuff
	{
		public override string LocalizationCategory => "Potions";
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Swiftness;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}

		// Only meant for levies; shed it at once on a real player.
		public override void Update(Player player, ref int buffIndex) { player.DelBuff(buffIndex); buffIndex--; }
	}
}
