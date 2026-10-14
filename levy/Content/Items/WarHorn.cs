using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Items
{
	/// <summary>Left-click: mobilize / stand down the nearest settlement. Right-click: orders radial. OWNED BY THE ITEMS AGENT (UI agent owns the radial).</summary>
	public class WarHorn : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 30; Item.height = 26; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.HoldUp;
			Item.UseSound = SoundID.Item1; Item.value = Item.buyPrice(silver: 50); Item.rare = ItemRarityID.Blue; Item.maxStack = 1; Item.noMelee = true;
		}
	}
}
