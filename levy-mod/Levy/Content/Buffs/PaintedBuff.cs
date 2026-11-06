using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// PAINT-SOAKED, the Painter's mark. An enemy hit by his paintball is covered in paint: it loses defense (flat, like Ichor) and deals less damage, and it is tinted a paint color while it lasts.
	/// This buff is only the flag; the effects live on <see cref="Levy.Common.Officers.PaintGlobalNPC"/>. Debuff, never saved, applied server-side and synced.
	/// </summary>
	public class PaintedBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Ichor;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}

		public override void Update(NPC npc, ref int buffIndex) { }

		public override void Update(Player player, ref int buffIndex) { player.DelBuff(buffIndex); buffIndex--; }
	}
}
