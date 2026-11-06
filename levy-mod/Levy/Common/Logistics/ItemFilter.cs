using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Logistics
{
	/// <summary>The item kinds the logistics lists can be narrowed to. Weapons covers every weapon class; the class entries narrow it.</summary>
	public enum FilterCat : byte { All, Weapons, Melee, Ranged, Magic, Summon, Rogue, Ammo, Gear, Potions, Tools, Blocks }

	/// <summary>Search text plus kind, shared by the Buy, Sell and Find lists. Pure functions of an item; no UI.</summary>
	public static class ItemFilter
	{
		/// <summary>The kinds in the order the Type button steps through them. Rogue only exists with Calamity.</summary>
		public static FilterCat[] Cats()
		{
			var l = new List<FilterCat> { FilterCat.All, FilterCat.Weapons, FilterCat.Melee, FilterCat.Ranged, FilterCat.Magic, FilterCat.Summon };
			if (ModLoader.HasMod("CalamityMod")) l.Add(FilterCat.Rogue);
			l.AddRange(new[] { FilterCat.Ammo, FilterCat.Gear, FilterCat.Potions, FilterCat.Tools, FilterCat.Blocks });
			return l.ToArray();
		}

		private static bool IsTool(Item it) => it.pick > 0 || it.axe > 0 || it.hammer > 0 || it.fishingPole > 0;
		private static bool IsArmor(Item it) => it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0;
		private static bool IsPotion(Item it) => it.consumable && it.damage <= 0 && (it.buffType > 0 || it.healLife > 0 || it.healMana > 0);
		private static bool IsAmmo(Item it) => it.ammo > 0 && !it.notAmmo;
		private static bool IsWeapon(Item it) => it.damage > 0 && !IsAmmo(it) && !IsTool(it) && !IsArmor(it) && !it.accessory;

		/// <summary>The single kind of an item: ammo, then armor and accessories, potions, tools, weapons, and everything else (blocks, furniture, paint...).</summary>
		public static FilterCat KindOf(Item it)
		{
			if (it == null || it.IsAir) return FilterCat.Blocks;
			if (IsAmmo(it)) return FilterCat.Ammo;
			if (IsArmor(it) || it.accessory) return FilterCat.Gear;
			if (IsPotion(it)) return FilterCat.Potions;
			if (IsTool(it)) return FilterCat.Tools;
			if (IsWeapon(it)) return FilterCat.Weapons;
			return FilterCat.Blocks;
		}

		private static bool Is(DamageClass c, DamageClass of) => c == of || c.CountsAsClass(of);

		public static bool Matches(Item it, FilterCat cat)
		{
			if (cat == FilterCat.All) return true;
			var k = KindOf(it);
			switch (cat)
			{
				case FilterCat.Weapons: return k == FilterCat.Weapons;
				case FilterCat.Melee: case FilterCat.Ranged: case FilterCat.Magic: case FilterCat.Summon: case FilterCat.Rogue:
					{
						if (k != FilterCat.Weapons) return false;
						var c = WeaponClassifier.ItemDamageClass(it);
						return cat switch
						{
							FilterCat.Melee => Is(c, DamageClass.Melee) || c == DamageClass.MeleeNoSpeed,
							FilterCat.Ranged => Is(c, DamageClass.Ranged),
							FilterCat.Magic => Is(c, DamageClass.Magic),
							FilterCat.Summon => Is(c, DamageClass.Summon),
							_ => Is(c, DamageClass.Throwing) || c.Name == "RogueDamageClass",
						};
					}
				default: return k == cat;
			}
		}

		/// <summary>Every space-separated word of <paramref name="term"/> appears in the name, ignoring case. An empty term matches everything.</summary>
		public static bool NameMatches(string name, string term)
		{
			if (string.IsNullOrWhiteSpace(term)) return true;
			foreach (var w in term.Split(' ', StringSplitOptions.RemoveEmptyEntries)) if ((name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) < 0) return false;
			return true;
		}
	}
}
