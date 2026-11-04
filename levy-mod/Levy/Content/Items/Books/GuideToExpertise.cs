using Terraria;
using Terraria.ID;

namespace Levy.Content.Items.Books
{
	/// <summary>Progression guide: every soldier gains an accessory slot. Drops from the Lunatic Cultist. Consumed on use, once per world (see GuideSystem).</summary>
	public class GuideToExpertise : EventBook
	{
		public override string Key => "Expertise";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Cyan; Item.value = Item.sellPrice(gold: 10);
		}
	}
}
