using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Content.Items.Books
{
	/// <summary>Event book: consumed on use, once per world. OWNED BY THE FLAVOR AGENT (drop + effect flag).</summary>
	public class GuideToConscription : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24; Item.height = 28; Item.useTime = 30; Item.useAnimation = 30; Item.useStyle = ItemUseStyleID.HoldUp; Item.consumable = true; Item.maxStack = 1;
			Item.rare = ItemRarityID.Orange; Item.value = Item.sellPrice(gold: 1); Item.UseSound = SoundID.Item4;
		}
	}
}
