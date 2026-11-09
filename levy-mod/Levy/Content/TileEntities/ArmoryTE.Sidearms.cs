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
	/// The levy's sidearms (see <see cref="SidearmPolicy"/> for the rules): up to <see cref="LevyConstants.MaxSidearms"/> of them, the hand sidearm and the extra slots that hold minion staves.
	///  * A MELEE levy's secondary is the best summon weapon in stock that no levy's primary needs; never a second blade, never a gun.
	///  * A RANGED or MAGIC levy keeps his lesser blade in hand and, when a spare staff is stocked, carries it in an extra slot.
	///  * A SUMMONER carries staves in every free slot: one unlimited staff for the rest of his minion slots and as many one-minion staves (see <see cref="SummonCap"/>) as he has room for.
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

		/// <summary>The best minion staff no levy's primary needs (the army plan's claims), for a melee levy's secondary: the dedicated sidearm row is read by the caller; here the racks and storage. A sentry is the fallback. Removes it.</summary>
		private Item TakeSummonSecondary(Func<Item, bool> free)
		{
			var staff = TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSummonStaff(it) && free(it), WeaponRank.Of);
			return staff ?? TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSentry(it) && free(it), WeaponRank.Of);
		}

		/// <summary>
		/// Fills every empty, unlocked sidearm slot. The hand sidearm: a summoner's complement (a melee-primary summoner takes a staff), a staff / sentry from the armory's sidearm slot, a melee levy's summon secondary, else a ranged /
		/// magic levy's lesser blade. The extra slots: minion staves that fit beside what he carries (see <see cref="SidearmPolicy.Fits"/>); a levy who is not a summoner carries one summon weapon in all. True if anything was added.
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
					?? (meleeRole || primaryMelee ? TakeSummonSecondary(Free) // a melee levy carries a summon weapon, never a sword
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
				var carried = CarriedWeapons(g);
				if (!summoner) { bool has = false; foreach (var c in carried) if (SidearmPolicy.IsDeployer(c)) has = true; if (has) break; } // one summon weapon for a levy who is not a summoner
				var pick = TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuableSummonStaff(it) && Free(it) && SidearmPolicy.Fits(g.Weapon, it, carried), WeaponRank.Of);
				if (pick == null) break;
				g.ExtraSidearms[x] = pick; changed = true;
			}
			if (changed) MarkDirty();
			return changed;
		}

		/// <summary>The periodic look at the sidearm slots (a levy enlisted before staves were stocked, or while the plan had claimed them all, stayed without).</summary>
		private bool FillEmptySidearms(SoldierRecord soldier)
		{
			var g = soldier.Gear;
			if (!g.HasWeapon) return false;
			var fam = WeaponClassifier.Classify(g.Weapon);
			return FillSidearms(soldier, g, IsSummonerSoldier(soldier), fam, IsMeleeTypeFamily(fam));
		}

		/// <summary>An extra slot's staff yields to a better staff of the same kind (limited for limited, unlimited for unlimited) that fits beside his other weapons and that no primary needs. True if one was swapped.</summary>
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
				if (g.IsLocked(slot) || IsAir(cur) || !SidearmPolicy.IsStaff(cur)) continue;
				bool capped = SummonCap.IsCapped(cur);
				float need = WeaponRank.Of(cur) * LevyConstants.AutoUpgradeScoreMargin;
				var others = CarriedWeapons(g); others.Remove(cur);
				var neu = TakeBest(IssueWeapons, 0, SidearmSlot, 1,
					it => IsIssuableSummonStaff(it) && SummonCap.IsCapped(it) == capped && WeaponRank.Of(it) >= need && Free(it) && SidearmPolicy.Fits(g.Weapon, it, others), WeaponRank.Of);
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
