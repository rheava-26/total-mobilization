using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Content.Items;

namespace Levy.Common.Flavor
{
	/// <summary>Remembers, per character, that the player has ever held a War Horn (the Guide's hint condition).</summary>
	public class FlavorPlayer : ModPlayer
	{
		public bool EverHeldWarHorn;

		public override void PostUpdate()
		{
			if (EverHeldWarHorn || Player.whoAmI != Main.myPlayer) return;
			Item held = Player.HeldItem;
			if (held != null && held.type == ModContent.ItemType<WarHorn>()) EverHeldWarHorn = true;
		}

		public override void SaveData(TagCompound tag)
		{
			if (EverHeldWarHorn) tag["warHorn"] = true;
		}

		public override void LoadData(TagCompound tag)
		{
			EverHeldWarHorn = tag.GetBool("warHorn");
		}
	}
}
