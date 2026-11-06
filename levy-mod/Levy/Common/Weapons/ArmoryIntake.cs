using Terraria;
using Terraria.ID;
using Levy.Common.Movement;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// What the Armory takes into storage by hand (shift-click, Deposit junk): everything its issue system can hand to a levy. Weapons (thrown stacks included), armor and vanity armor,
	/// accessories and vanity accessories, ammo, grappling hooks, dyes, healing / mana / buff potions and the growth crystals. Favourited items and coins are never taken.
	/// Levies use no mounts, so a mount stays with the player.
	/// </summary>
	public static class ArmoryIntake
	{
		public static bool Accepts(Item it)
		{
			if (it == null || it.IsAir || it.stack <= 0 || it.favorited || it.IsACoin) return false;
			if (WeaponClassifier.IsIssuableWeapon(it)) return true;                 // weapons, sentries, staves and thrown stacks (Molotov, knives, grenades)
			if (it.damage > 0 && !it.accessory && !it.consumable && it.pick == 0 && it.axe == 0 && it.hammer == 0 && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0) return true; // any other weapon-shaped item
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) return true; // armor and vanity armor
			if (it.accessory) return true;                                          // accessories and vanity accessories
			if (it.ammo > 0) return true;
			if (HookInfo.IsHookItem(it)) return true;
			if (it.dye > 0) return true;
			if (it.consumable && (it.healLife > 0 || it.healMana > 0 || it.buffType > 0)) return true;
			if (it.type == ItemID.LifeCrystal || it.type == ItemID.LifeFruit || it.type == ItemID.ManaCrystal) return true;
			return false;
		}
	}
}
