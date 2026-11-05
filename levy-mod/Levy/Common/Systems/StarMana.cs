using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Fallen Stars into levy mana. An Armory with <see cref="LevyConfig.ArmoryTurnsStarsIntoMana"/> on turns the Fallen Stars in its storage into Mana Crystals for its magic levies, at
	/// <see cref="LevyConfig.FallenStarsPerManaCrystal"/> stars per crystal (vanilla: 5; each crystal is worth <see cref="LevyConstants.ManaPerCrystal"/> maximum mana, up to <see cref="LevyConstants.MaxMana"/>).
	/// Each crystal goes to the magic levy with the fewest crystals that can still absorb one, ties to the lowest id. Whole crystals only: a remainder of stars stays in storage until it makes one.
	/// Stars are kept while a weapon on the racks or in storage, or one a levy holds, fires them (a Star Cannon). Mana regeneration and the mana cap are not touched.
	/// </summary>
	public static class StarMana
	{
		public const int MinRatio = 1, MaxRatio = 5;

		/// <summary>The configured stars per crystal, held to 1..5.</summary>
		public static int Ratio
		{
			get { var c = LevyConfig.Get; return c == null ? MaxRatio : Math.Clamp(c.FallenStarsPerManaCrystal, MinRatio, MaxRatio); }
		}

		/// <summary>Whole crystals <paramref name="stars"/> make at <paramref name="ratio"/> stars each.</summary>
		public static int CrystalsFor(int stars, int ratio) => stars <= 0 ? 0 : stars / Math.Clamp(ratio, MinRatio, MaxRatio);

		/// <summary>
		/// Deals <paramref name="crystals"/> out one at a time to the entry with the fewest crystals (ties: the earliest) among those with room (<c>room[i]</c> &gt; 0). Returns the crystals given to each entry;
		/// their sum is at most <paramref name="crystals"/> and at most the total room.
		/// </summary>
		public static int[] Distribute(int crystals, int[] have, int[] room)
		{
			int n = have.Length; var given = new int[n]; var left = (int[])room.Clone();
			for (int c = 0; c < crystals; c++)
			{
				int best = -1;
				for (int i = 0; i < n; i++)
					if (left[i] > 0 && (best < 0 || have[i] + given[i] < have[best] + given[best])) best = i;
				if (best < 0) break;
				given[best]++; left[best]--;
			}
			return given;
		}

		/// <summary>True for a levy who fights as a mage: his armor's class is magic, or, with no armor class, so is his weapon.</summary>
		public static bool IsMage(SoldierRecord r)
		{
			var g = r?.Gear; if (g == null) return false;
			var cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			if (cls == null && g.HasWeapon) cls = WeaponClassifier.ItemDamageClass(g.Weapon);
			return cls != null && (cls == DamageClass.Magic || cls.CountsAsClass(DamageClass.Magic));
		}

		/// <summary>True when something in this armory's reach fires Fallen Stars: a weapon on its racks or in storage, or one of these levies' weapons or sidearms.</summary>
		public static bool StarsAreAmmo(ArmoryTE armory, IEnumerable<SoldierRecord> levies)
		{
			if (armory.HoldsWeaponUsing(AmmoID.FallenStar)) return true;
			foreach (var r in levies)
			{
				var g = r?.Gear; if (g == null) continue;
				if (g.HasWeapon && g.Weapon.useAmmo == AmmoID.FallenStar) return true;
				if (g.Sidearm != null && !g.Sidearm.IsAir && g.Sidearm.useAmmo == AmmoID.FallenStar) return true;
			}
			return false;
		}
	}
}
