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

		/// <summary>
		/// One armory's conversion: the stars in its storage make whole crystals at <paramref name="ratio"/>, dealt to its magic levies (<paramref name="canAbsorb"/> says whether a levy can still take one;
		/// <paramref name="apply"/> gives it to him and returns false if refused). Stars are taken only for crystals actually given. Does nothing while something fires the stars. Returns the crystals given.
		/// </summary>
		public static int Convert(ArmoryTE armory, List<SoldierRecord> levies, int ratio, Func<SoldierRecord, int, bool> canAbsorb, Func<SoldierRecord, bool> apply)
		{
			ratio = Math.Clamp(ratio, MinRatio, MaxRatio);
			int crystals = CrystalsFor(armory.CountInStorage(ItemID.FallenStar), ratio);
			if (crystals <= 0 || StarsAreAmmo(armory, levies)) return 0;
			var mages = levies.FindAll(r => r != null && !r.Dead && !r.Reserve && IsMage(r) && canAbsorb(r, ItemID.ManaCrystal));
			if (mages.Count == 0) return 0;
			mages.Sort((a, b) => a.Id.CompareTo(b.Id));
			var have = new int[mages.Count]; var room = new int[mages.Count];
			for (int k = 0; k < mages.Count; k++)
			{
				have[k] = mages[k].ManaCrystals;
				room[k] = Math.Max(0, (int)Math.Ceiling((LevyConstants.MaxMana - mages[k].RawMana) / (double)LevyConstants.ManaPerCrystal));
			}
			var plan = Distribute(crystals, have, room);
			int want = 0; foreach (int n in plan) want += n;
			if (want <= 0) return 0;
			int cost = want * ratio, taken = armory.TakeUpTo(ItemID.FallenStar, cost);
			if (taken < cost)
			{
				Refund(armory, taken);
				return 0; // the stars were not all there after all: nothing is given and nothing is lost
			}
			int given = 0;
			for (int k = 0; k < mages.Count; k++)
				for (int c = 0; c < plan[k]; c++)
				{
					if (!apply(mages[k])) { Refund(armory, ratio * (plan[k] - c)); break; } // refused: that crystal's stars go back
					given++;
				}
			return given;
		}

		private static void Refund(ArmoryTE armory, int stars)
		{
			if (stars <= 0) return;
			var back = new Item(); back.SetDefaults(ItemID.FallenStar); back.stack = stars;
			var rem = armory.Deposit(back);
			if (rem != null && !rem.IsAir) Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("LevyStarMana"), armory.Center, rem);
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
