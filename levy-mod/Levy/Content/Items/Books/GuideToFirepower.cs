using Terraria;
using Terraria.ID;

namespace Levy.Content.Items.Books
{
	/// <summary>Progression guide: militia are mustered with more HP and mana, and every Armory gains buff slots. Drops from the Wall of Flesh. Consumed on use, once per world (see GuideSystem).</summary>
	public class GuideToFirepower : EventBook
	{
		public override string Key => "Firepower";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Pink; Item.value = Item.sellPrice(gold: 5);
		}
	}
}
