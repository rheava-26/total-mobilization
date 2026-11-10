using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// The levy's sidearms (see <see cref="SidearmPolicy"/> for the rules): up to <see cref="LevyConstants.MaxSidearms"/> of them, the hand sidearm and two extra slots that hold the summon weapons.
	///  * A levy who is NOT a summoner carries one sentry staff and one minion staff, the best of each in stock, in the two extra slots; a ranged or magic levy keeps his lesser blade in hand.
	///  * A SUMMONER leads with a whip. The hand slot carries his minion staff, the extra slots his sentry staff and then as many one-minion staves (see <see cref="SummonCap"/>) as he has room for.
	///  * With no whip in stock a summoner leads with the staff and carries the sentry; with neither, with the sentry.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>The weapons he already has besides the one being considered: the primary and every filled sidearm slot.</summary>
		private static List<Item> CarriedWeapons(SoldierGear g)
		{
			var l = new List<Item>(1 + LevyConstants.MaxSidearms);
			if (g.HasWeapon) l.Add(g.Weapon);
			foreach (var s in g.Sidearms()) if (!IsAir(s)) l.Add(s);
			return l;
		}

		/// <summary>Gives back to the shelf every sidearm that no longer fits beside the primary (a second blade, a second whip, an off-class weapon). Locked slots stay. True if anything went back.</summary>
		private bool TidySidearms(SoldierGear g)
		{
			var bad = SidearmPolicy.Misfits(g);
			if (bad.Count == 0) return false;
			foreach (int slot in bad)
			{
				var rem = DepositOld(g.Get(slot));
				if (!IsAir(rem)) Spill(rem, protect: true);
				g.Set(slot, new Item());
			}
			MarkDirty();
			return true;
		}

		/// <summary>The best minion staff no levy's primary needs (the army plan's claims), for a hand slot: the dedicated sidearm row is read by the caller; here the racks and storage. The staff that fills a levy's minion slots comes first. A sentry is the fallback. Removes it.</summary>
		private Item TakeSummonSecondary(Func<Item, bool> free)
		{
			var staff = TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSummonStaff(it) && !SummonCap.IsCapped(it) && free(it), WeaponRank.Of)
				?? TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSummonStaff(it) && free(it), WeaponRank.Of);
			return staff ?? TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSentry(it) && free(it), WeaponRank.Of);
		}

		/// <summary>
		/// Fills every empty, unlocked sidearm slot. The hand slot: a summoner's minion staff (a whip too when his primary is not one), a staff / sentry from the armory's sidearm slot, else a ranged / magic levy's lesser blade; a melee
		/// levy who is not a summoner keeps it for what the player placed there. The extra slots: the summon weapons he is missing (see <see cref="NextDeployer"/>). True if anything was added.
		/// </summary>
		private bool FillSidearms(SoldierRecord soldier, SoldierGear g, bool summoner, WeaponFamily primaryFam, bool primaryMelee)
		{
			bool changed = TidySidearms(g);
			HashSet<object> claimed = null; bool claimedRead = false;
			bool Free(Item it) { if (!claimedRead) { claimed = ClaimedWeapons(soldier); claimedRead = true; } return claimed == null || !claimed.Contains(it); }
			bool meleeRole = g.HasWeapon && SidearmPolicy.IsMeleeRole(g.Weapon);

			if (!g.IsLocked(SoldierGear.SlotSidearm) && !g.HasSidearm)
			{
				Item s;
				if (summoner && !meleeRole) s = TakeSummonerComplement(soldier, primaryFam);
				else if (summoner) s = TakeDeployerFromSidearmSlot() ?? TakeSummonSecondary(Free); // a whip-leading summoner: a staff, never a blade
				else s = TakeDeployerFromSidearmSlot()
					?? (meleeRole || primaryMelee ? null // a melee levy's summon weapons ride in the extra slots, never a sword here
					: TakeSidearm(soldier)); // ranged / magic: a lesser blade
				if (s != null)
				{
					if (!g.HasWeapon || SidearmPolicy.Fits(g.Weapon, s, CarriedWeapons(g))) { g.Sidearm = s; changed = true; }
					else { var rem = DepositOld(s); if (!IsAir(rem)) Spill(rem, protect: true); } // the pick did not fit beside his primary: back it goes
				}
			}

			if (!g.HasWeapon) return changed;
			for (int x = 0; x < g.ExtraSidearms.Length; x++)
			{
				int slot = SoldierGear.SlotSide2 + x;
				if (g.IsLocked(slot) || !IsAir(g.ExtraSidearms[x])) continue;
				var pick = NextDeployer(g, summoner, CarriedWeapons(g), Free);
				if (pick == null) break;
				g.ExtraSidearms[x] = pick; changed = true;
			}
			if (changed) MarkDirty();
			return changed;
		}

		/// <summary>
		/// The next summon weapon an extra slot takes, or null (removed from the armory). A levy who is not a summoner takes one sentry staff, then one minion staff (the best of each in stock). A summoner takes the
		/// unlimited minion staff he does not carry yet, then his sentry staff, then one-minion staves with a limit of their own (<see cref="SummonCap"/>) that fit beside what he carries.
		/// </summary>
		private Item NextDeployer(SoldierGear g, bool summoner, List<Item> carried, Func<Item, bool> free)
		{
			bool hasSentry = false, hasStaff = false, hasUnlimited = false;
			foreach (var c in carried)
			{
				if (SidearmPolicy.IsSentry(c)) hasSentry = true;
				else if (SidearmPolicy.IsStaff(c)) { hasStaff = true; if (!SummonCap.IsCapped(c)) hasUnlimited = true; }
			}
			bool Fits(Item it) => free(it) && SidearmPolicy.Fits(g.Weapon, it, carried);
			Item TakeStaff(Func<Item, bool> extra) => TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSummonStaff(it) && extra(it) && Fits(it), WeaponRank.Of);
			Item TakeSentry() => TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSentry(it) && Fits(it), WeaponRank.Of);
			if (!summoner)
			{
				Item pick = null;
				if (!hasSentry) pick = TakeSentry();
				if (pick == null && !hasStaff) pick = TakeStaff(_ => true);
				return pick;
			}
			Item p = null;
			if (!hasUnlimited) p = TakeStaff(it => !SummonCap.IsCapped(it));
			if (p == null && !hasSentry) p = TakeSentry();
			p ??= TakeStaff(it => SummonCap.IsCapped(it));
			return p;
		}

		/// <summary>The periodic look at the sidearm slots (a levy enlisted before staves were stocked, or while the plan had claimed them all, stayed without).</summary>
		private bool FillEmptySidearms(SoldierRecord soldier)
		{
			var g = soldier.Gear;
			if (!g.HasWeapon) return false;
			var fam = WeaponClassifier.Classify(g.Weapon);
			return FillSidearms(soldier, g, IsSummonerSoldier(soldier), fam, IsMeleeTypeFamily(fam));
		}

		/// <summary>An extra slot's staff yields to a better staff of the same kind (limited for limited, unlimited for unlimited), and its sentry to a better sentry, that fits beside his other weapons and that no primary needs. True if one was swapped.</summary>
		private bool UpgradeExtraSidearms(SoldierRecord soldier)
		{
			var g = soldier.Gear;
			bool changed = false;
			HashSet<object> claimed = null; bool claimedRead = false;
			bool Free(Item it) { if (!claimedRead) { claimed = ClaimedWeapons(soldier); claimedRead = true; } return claimed == null || !claimed.Contains(it); }
			for (int x = 0; x < g.ExtraSidearms.Length; x++)
			{
				int slot = SoldierGear.SlotSide2 + x;
				var cur = g.ExtraSidearms[x];
				if (g.IsLocked(slot) || IsAir(cur) || !SidearmPolicy.IsDeployer(cur)) continue;
				bool sentry = SidearmPolicy.IsSentry(cur);
				bool capped = !sentry && SummonCap.IsCapped(cur);
				float need = WeaponRank.Of(cur) * LevyConstants.AutoUpgradeScoreMargin;
				var others = CarriedWeapons(g); others.Remove(cur);
				var neu = TakeBest(IssueWeapons, 0, SidearmSlot, 1,
					it => (sentry ? IsIssuableSentry(it) : IsIssuableSummonStaff(it) && SummonCap.IsCapped(it) == capped) && WeaponRank.Of(it) >= need && Free(it) && SidearmPolicy.Fits(g.Weapon, it, others), WeaponRank.Of);
				if (neu != null && SwapIn(g, slot, neu)) changed = true;
			}
			return changed;
		}

		/// <summary>Removes <paramref name="it"/> from whichever sidearm slot of <paramref name="g"/> holds that exact instance. True if it was found.</summary>
		private static bool TakeSidearmRef(SoldierGear g, Item it)
		{
			for (int i = 0; i < LevyConstants.MaxSidearms; i++)
			{
				int slot = SoldierGear.SidearmSlotAt(i);
				if (ReferenceEquals(g.Get(slot), it)) { g.Set(slot, new Item()); return true; }
			}
			return false;
		}
	}
}
