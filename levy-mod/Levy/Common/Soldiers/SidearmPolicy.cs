using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// What a levy may carry besides his primary. A levy holds up to <see cref="LevyConstants.MaxSidearms"/> sidearms: the hand sidearm (a fallback weapon, or a staff / sentry that is kept up without being in hand) and
	/// <see cref="LevyConstants.ExtraSidearms"/> more, which hold the summon weapons (a sentry staff and a minion staff). Two rules decide whether a weapon fits beside the others:
	///  * NO SECOND OF A ROLE. A melee weapon beside a melee primary, a second whip, a second sentry, a second unlimited minion staff or the same staff twice duplicate what he already has. A staff with a limit of its own
	///    (see <see cref="SummonCap"/>) only takes the slots its minions use, so it fits beside an unlimited staff and beside other limited ones.
	///  * NOTHING OFF-CLASS. A melee primary carries only a summon weapon beside it, never a gun, a bow or a second sword.
	/// Pure functions over items; the armory applies them when it issues, upgrades and tidies a kit.
	/// </summary>
	public static class SidearmPolicy
	{
		/// <summary>A weapon that fights as melee: a blade, spear, whip, yoyo, flail, boomerang or any melee-class weapon.</summary>
		public static bool IsMeleeRole(Item it)
		{
			if (it == null || it.IsAir) return false;
			var p = WeaponClassifier.GetProfile(it);
			if (p.Family == WeaponFamily.Summon || p.Family == WeaponFamily.Sentry) return false;
			switch (p.Family)
			{
				case WeaponFamily.Broadsword: case WeaponFamily.Shortsword: case WeaponFamily.Spear: case WeaponFamily.Whip:
				case WeaponFamily.Yoyo: case WeaponFamily.Flail: case WeaponFamily.Boomerang: case WeaponFamily.ThrownMelee:
					return true;
			}
			return p.IsMelee || WeaponClassifier.ItemDamageClass(it) == DamageClass.Melee;
		}

		public static bool IsStaff(Item it) => it != null && !it.IsAir && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon;
		public static bool IsSentry(Item it) => it != null && !it.IsAir && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Sentry;
		public static bool IsDeployer(Item it) => IsStaff(it) || IsSentry(it);

		/// <summary>True when <paramref name="side"/> repeats a role already filled by the primary or by one of <paramref name="others"/> (the sidearms already chosen).</summary>
		public static bool Duplicates(Item primary, Item side, IEnumerable<Item> others)
		{
			if (side == null || side.IsAir) return false;
			bool Same(Item a)
			{
				if (a == null || a.IsAir || ReferenceEquals(a, side)) return false;
				if (a.type == side.type) return true;
				if (IsStaff(side)) return IsStaff(a) && !SummonCap.IsCapped(side) && !SummonCap.IsCapped(a); // two unlimited staves share one set of slots
				if (IsSentry(side)) return IsSentry(a);
				if (IsMeleeRole(side)) return IsMeleeRole(a);
				return false;
			}
			if (Same(primary)) return true;
			if (others != null) foreach (var o in others) if (Same(o)) return true;
			return false;
		}

		/// <summary>True when <paramref name="side"/> is not of the primary's class of weapon: a melee primary carries only summon weapons beside it.</summary>
		public static bool OffClass(Item primary, Item side)
		{
			if (side == null || side.IsAir || primary == null || primary.IsAir) return false;
			return IsMeleeRole(primary) && !IsDeployer(side);
		}

		/// <summary>True when the weapon may sit beside this primary and these other sidearms.</summary>
		public static bool Fits(Item primary, Item side, IEnumerable<Item> others) => !OffClass(primary, side) && !Duplicates(primary, side, others);

		/// <summary>True when a levy who is not a summoner may carry this summon item: a minion staff needs a minion slot beyond the base one, a sentry staff a sentry slot beyond the base one. Anything else is allowed.</summary>
		public static bool AllowedForOthers(Item it, int extraMinions, int extraTurrets)
		{
			if (IsStaff(it)) return extraMinions >= 1;
			if (IsSentry(it)) return extraTurrets >= 1;
			return true;
		}

		/// <summary>The sidearms (hand first) that do not fit beside the primary and the ones kept before them: what a tidy-up gives back to the shelf. Locked slots are never listed.
		/// <paramref name="summoner"/> false adds the summon-slot rule: a levy who is not a summoner keeps a minion staff only while his gear gives a minion slot beyond the base one, and a sentry staff only while it gives a sentry slot beyond the base one.</summary>
		public static List<int> Misfits(SoldierGear g, bool? summoner = null)
		{
			var res = new List<int>();
			if (g == null) return res;
			var kept = new List<Item>();
			for (int i = 0; i < LevyConstants.MaxSidearms; i++)
			{
				int slot = SoldierGear.SidearmSlotAt(i);
				var it = g.Get(slot);
				if (it == null || it.IsAir) continue;
				if (!g.IsLocked(slot) && g.HasWeapon && !Fits(g.Weapon, it, kept)) { res.Add(slot); continue; }
				kept.Add(it);
			}
			if (summoner == false) AddUnearnedSummonItems(g, res);
			return res;
		}

		/// <summary>The summon-slot rule for a non-summoner: every unlocked staff / sentry his gear gives no extra slot for goes back.</summary>
		private static void AddUnearnedSummonItems(SoldierGear g, List<int> res)
		{
			int exM = 0, exT = 0; bool read = false;
			for (int i = 0; i < LevyConstants.MaxSidearms; i++)
			{
				int slot = SoldierGear.SidearmSlotAt(i);
				var it = g.Get(slot);
				if (it == null || it.IsAir || !IsDeployer(it) || g.IsLocked(slot) || res.Contains(slot)) continue;
				if (!read) { ArmorDoctrine.ExtraSummonSlots(g, out exM, out exT); read = true; } // read once, and only for a levy who carries a summon item at all
				if (!AllowedForOthers(it, exM, exT)) res.Add(slot);
			}
		}
	}
}
