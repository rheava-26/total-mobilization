using Terraria;
using Terraria.ID;

namespace Levy.Content.Items.Books
{
	/// <summary>Progression guide: soldiers start with more HP and mana and share the commander's buffs. Drops from the Wall of Flesh. Consumed on use, once per world (see GuideSystem).</summary>
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
