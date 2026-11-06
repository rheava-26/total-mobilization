using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.Items;

namespace Levy.Common.UI
{
	/// <summary>Every new character starts with a Field Manual, the starter guide that opens the in-game book.</summary>
	public class GuidePlayer : ModPlayer
	{
		public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
		{
			yield return new Item(ModContent.ItemType<FieldManual>());
		}
	}
}
