using Terraria;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Which items belong in a levy's two dedicated mobility slots. Read from the item's own equip slots, never from a list of names, so every modded pair of wings and boots is found the way a vanilla one is:
	/// wings are accessories with a wing slot, boots are accessories with a shoe slot. A levy wears one of each besides his ordinary accessories.
	/// </summary>
	public static class MobilitySlots
	{
		/// <summary>Lab-only A/B switch: <c>LEVY_MOBSLOTS=0</c> makes no item count as wings or boots for a slot, so a levy wears them as ordinary accessories again (the pre-slots rule). Never set in play.</summary>
		internal static readonly bool On = System.Environment.GetEnvironmentVariable("LEVY_MOBSLOTS") != "0";

		/// <summary>An accessory with a wing slot: vanilla and modded wings (Item.wingSlot is set by the vanilla table and by the mod's equip texture).</summary>
		public static bool IsWings(Item it) => On && it != null && !it.IsAir && it.accessory && !it.vanity && it.wingSlot > 0;

		/// <summary>
		/// Movement boots: an accessory with a shoe slot that makes the wearer run faster or gives rocket flight (Hermes to Terraspark Boots, Angel Treads, Thorium's boots), read as the delta a dummy shows wearing it
		/// (<see cref="AccessoryMobility.IsRunner"/>). A shoe that only climbs, dashes, jumps, swims or walks on water (Shoe Spikes, Tabi, Master Ninja Gear, Frog Leg, Flipper, skates, water walkers) is an ordinary accessory.
		/// A shoe that does both counts as boots. Never a pair of wings.
		/// </summary>
		public static bool IsBoots(Item it) => On && it != null && !it.IsAir && it.accessory && !it.vanity && it.shoeSlot > 0 && it.wingSlot <= 0 && AccessoryMobility.IsRunner(it);

		/// <summary>Either one: such an item is never dealt into an ordinary accessory slot.</summary>
		public static bool Is(Item it) => IsWings(it) || IsBoots(it);
	}
}
