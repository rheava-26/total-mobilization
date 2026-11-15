using Terraria;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Which items belong in a levy's two dedicated mobility slots. Read from the item's own equip slots, never from a list of names, so every modded pair of wings and boots is found the way a vanilla one is:
	/// wings are accessories with a wing slot, boots are accessories with a shoe slot. A levy wears one of each besides his ordinary accessories.
	/// </summary>
	public static class MobilitySlots
	{
		/// <summary>An accessory with a wing slot: vanilla and modded wings (Item.wingSlot is set by the vanilla table and by the mod's equip texture).</summary>
		public static bool IsWings(Item it) => it != null && !it.IsAir && it.accessory && !it.vanity && it.wingSlot > 0;

		/// <summary>An accessory with a shoe slot (Hermes to Terraspark Boots, Angel Treads, Thorium's boots, water walkers, skates). Never a pair of wings.</summary>
		public static bool IsBoots(Item it) => it != null && !it.IsAir && it.accessory && !it.vanity && it.shoeSlot > 0 && it.wingSlot <= 0;

		/// <summary>Either one: such an item is never dealt into an ordinary accessory slot.</summary>
		public static bool Is(Item it) => IsWings(it) || IsBoots(it);
	}
}
