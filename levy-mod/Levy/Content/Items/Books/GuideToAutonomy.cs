using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Potions;

namespace Levy.Content.Items.Books
{
	/// <summary>
	/// Progression guide: while the Spectator buff is active your levies are Autonomous (+10% damage, +10% move speed), and you can give map orders without the Command Radio.
	/// Consumed on use, once per world (a <see cref="Common.Flavor.GuideSystem"/> flag). Drops from any enemy during Slime Rain at 1 in 50 (<see cref="AutonomyDrops"/>).
	/// </summary>
	public class GuideToAutonomy : EventBook
	{
		public override string LocalizationCategory => "Potions";
		public override string Key => "Autonomy";

		public override void SetDefaults()
		{
			base.SetDefaults();
			Item.rare = ItemRarityID.Green; Item.value = Item.sellPrice(silver: 50);
		}
	}
}
