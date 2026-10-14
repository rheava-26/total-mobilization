using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>Cached numbers a soldier borrows from an item. Built once per item type by <see cref="WeaponClassifier"/>.</summary>
	public class WeaponProfile
	{
		public int ItemType;
		public WeaponFamily Family = WeaponFamily.Unsupported;
		public int UseTime = 30, UseAnimation = 30, ReuseDelay;
		public int Shoot; public float ShootSpeed;
		public int Damage; public float Knockback; public int Crit;
		public int UseAmmo; public int Mana;
		public int RangeTiles = LevyConstants.RangeMelee;
		public bool Gravity; public bool Homing;
		public int MultishotCount = 1; public float SpreadDeg;
		public DamageClass DamageType = DamageClass.Default;
		public bool IsRanged => Family == WeaponFamily.Gun || Family == WeaponFamily.Bow || Family == WeaponFamily.Magic;
		public bool IsMelee => Family == WeaponFamily.Broadsword || Family == WeaponFamily.Shortsword || Family == WeaponFamily.Spear;
		public bool Supported => Family != WeaponFamily.Unsupported && Family != WeaponFamily.None;
	}

	/// <summary>Sorts every item into a family from its fields at load. OWNED BY THE WEAPONS AGENT.</summary>
	public static class WeaponClassifier
	{
		public static WeaponFamily Classify(Item item) => item == null || item.IsAir ? WeaponFamily.None : WeaponFamily.Unsupported;
		public static WeaponProfile GetProfile(Item item) => new WeaponProfile { ItemType = item?.type ?? 0, Family = Classify(item) };
		/// <summary>True for weapons the armory may issue (supported family, not a tool/consumable).</summary>
		public static bool IsIssuableWeapon(Item item) => false;
		public static bool IsArmorPiece(Item item, int part) => item != null && !item.IsAir && (part == 0 ? item.headSlot >= 0 : part == 1 ? item.bodySlot >= 0 : item.legSlot >= 0);
		/// <summary>Accessories that do something on a soldier (plain stats + movement). Info/fishing/building accessories are refused.</summary>
		public static bool IsUsefulAccessory(Item item) => item != null && !item.IsAir && item.accessory;
		public static bool IsAmmo(Item item) => item != null && !item.IsAir && item.ammo > 0;
		public static bool IsHealingPotion(Item item) => item != null && !item.IsAir && item.healLife > 0 && item.consumable;
		/// <summary>Rank weapons for best-first issue: damage per second-ish. Higher is better.</summary>
		public static float Score(Item item) => item == null || item.IsAir ? 0f : item.damage * 60f / System.Math.Max(1, item.useTime);
	}
}
