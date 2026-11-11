using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// The levy's sidearms (see <see cref="SidearmPolicy"/> for the rules): up to <see cref="LevyConstants.MaxSidearms"/> of them, the hand sidearm and two extra slots that hold the summon weapons.
	///  * A levy who is NOT a summoner carries ONE summon item, a sentry staff or a minion staff (the better one in stock; none when the config turns it off), in an extra slot; a ranged or magic levy keeps his lesser blade in hand.
	///  * A SUMMONER leads with a whip. The hand slot carries his minion staff, the extra slots his sentry staff and then as many one-minion staves (see <see cref="SummonCap"/>) as he has room for.
	///  * With no whip in stock a summoner leads with the staff and carries the sentry; with neither, with the sentry.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>The summon-weapon priority (summoners are dealt first, others get the leftovers). Harness-only switch for before/after runs (env LEVY_ARMORY_SUMMONERSFIRST=0 turns it off).</summary>
		internal static bool SummonersFirst = Environment.GetEnvironmentVariable("LEVY_ARMORY_SUMMONERSFIRST") != "0";

		/// <summary>What a summoner could still take into a summon slot: his kit as it stands, for <see cref="SummonerWants"/>.</summary>
		private sealed class SummonerNeed { public SoldierRecord Rec; public Item Primary; public List<Item> Carried; public bool OpenSlot; public List<Item> Swappable = new(); public float Mana; }

		/// <summary>The summoners this armory serves (other than <paramref name="except"/>) with a primary in hand, as <see cref="SummonerNeed"/>s. An open slot is an unlocked, empty hand-sidearm or extra slot; a swappable item is a carried staff or sentry in an unlocked slot.</summary>
		private List<SummonerNeed> SummonerWantList(SoldierRecord except)
		{
			var l = new List<SummonerNeed>();
			if (!SummonersFirst) return l;
			foreach (var z in ServedSoldiers())
			{
				var g = z?.Gear;
				if (g == null || ReferenceEquals(z, except) || z.LoadoutLocked || !g.HasWeapon || !IsSummonerSoldier(z)) continue;
				var n = new SummonerNeed { Rec = z, Primary = g.Weapon, Carried = CarriedWeapons(g), Mana = z.MaxMana };
				for (int i = 0; i < LevyConstants.MaxSidearms; i++)
				{
					int slot = SoldierGear.SidearmSlotAt(i);
					if (g.IsLocked(slot)) continue;
					var cur = g.Get(slot);
					if (IsAir(cur)) n.OpenSlot = true;
					else if (i > 0 && SidearmPolicy.IsDeployer(cur)) n.Swappable.Add(cur); // only the extra slots are upgraded in place (UpgradeExtraSidearms)
				}
				l.Add(n);
			}
			return l;
		}

		/// <summary>True when some summoner in <paramref name="wants"/> would take <paramref name="it"/>: it fits beside his weapons and he has an open slot for it, or it beats the staff / sentry of its kind he carries by the upgrade margin. Mirrors what <see cref="FillSidearms"/> and <see cref="UpgradeExtraSidearms"/> would do for him.</summary>
		private static bool SummonerWants(List<SummonerNeed> wants, Item it)
		{
			if (wants == null || wants.Count == 0) return false;
			bool sentry = SidearmPolicy.IsSentry(it), capped = !sentry && SummonCap.IsCapped(it);
			float rank = -1f;
			foreach (var n in wants)
			{
				if (!WeaponUse.CanEverPay(it, (int)n.Mana)) continue;
				if (n.OpenSlot && SidearmPolicy.Fits(n.Primary, it, n.Carried)) return true;
				foreach (var cur in n.Swappable)
				{
					if (SidearmPolicy.IsSentry(cur) != sentry || (!sentry && SummonCap.IsCapped(cur) != capped)) continue;
					if (rank < 0f) rank = WeaponRank.Of(it);
					if (rank < WeaponRank.Of(cur) * LevyConstants.AutoUpgradeScoreMargin) continue;
					var others = new List<Item>(n.Carried); others.Remove(cur);
					if (SidearmPolicy.Fits(n.Primary, it, others)) return true;
				}
			}
			return false;
		}

		/// <summary>The upgrade pass of the summon-weapon priority: a levy who is not a summoner gives back every staff or sentry a summoner could still use (he is dealt a leftover later), and the summoner it goes to is served at once.
		/// Locked slots stay. True if anything went back.</summary>
		private bool ReclaimSummonWeapons(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null || soldier.LoadoutLocked || IsSummonerSoldier(soldier)) return false;
			var wants = SummonerWantList(soldier);
			if (wants.Count == 0) return false;
			bool any = false;
			for (int x = 0; x < g.ExtraSidearms.Length; x++)
			{
				int slot = SoldierGear.SlotSide2 + x;
				var cur = g.ExtraSidearms[x];
				if (g.IsLocked(slot) || IsAir(cur) || !SidearmPolicy.IsDeployer(cur) || !SummonerWants(wants, cur)) continue;
				var rem = DepositOld(cur);
				if (!IsAir(rem)) Spill(rem, protect: true);
				g.ExtraSidearms[x] = new Item();
				any = true;
			}
			if (!any) return false;
			MarkDirty();
			foreach (var n in wants)
			{
				var z = n.Rec;
				bool ch = UpgradeExtraSidearms(z) | FillEmptySidearms(z);
				if (ch && z.HasBody && Main.netMode != NetmodeID.MultiplayerClient && Main.npc[z.BodyIndex].ModNPC is SoldierNPC sn) sn.NotifyGearChanged();
			}
			return true;
		}

		/// <summary>The weapons he already has besides the one being considered: the primary and every filled sidearm slot.</summary>
		private static List<Item> CarriedWeapons(SoldierGear g)
		{
			var l = new List<Item>(1 + LevyConstants.MaxSidearms);
			if (g.HasWeapon) l.Add(g.Weapon);
			foreach (var s in g.Sidearms()) if (!IsAir(s)) l.Add(s);
			return l;
		}

		/// <summary>Gives back to the shelf every sidearm that no longer fits beside the primary (a second blade, a second whip, an off-class weapon). Locked slots stay. True if anything went back.</summary>
		private bool TidySidearms(SoldierGear g, SoldierRecord soldier = null)
		{
			var bad = SidearmPolicy.Misfits(g, soldier == null ? (bool?)null : IsSummonerSoldier(soldier));
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
			bool changed = TidySidearms(g, soldier);
			if (EnforceSidearmRules(g, summoner)) changed = true;
			HashSet<object> claimed = null; bool claimedRead = false;
			bool Free(Item it) { if (!claimedRead) { claimed = ClaimedWeapons(soldier); claimedRead = true; } return claimed == null || !claimed.Contains(it); }
			bool meleeRole = g.HasWeapon && SidearmPolicy.IsMeleeRole(g.Weapon);
			bool noSummons = !summoner && !Rules.SummonsForOthers;

			if (!g.IsLocked(SoldierGear.SlotSidearm) && !g.HasSidearm && SidearmRoom(g, summoner))
			{
				Item s;
				if (summoner && !meleeRole) s = TakeSummonerComplement(soldier, primaryFam);
				else if (summoner) s = TakeDeployerFromSidearmSlot() ?? TakeSummonSecondary(Free); // a whip-leading summoner: a staff, never a blade
				else s = (noSummons ? null : TakeDeployerFromSidearmSlot())
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
				if (!SidearmRoom(g, summoner)) break;
				var pick = NextDeployer(g, summoner, CarriedWeapons(g), Free, soldier);
				if (pick == null) break;
				g.ExtraSidearms[x] = pick; changed = true;
			}
			if (changed) MarkDirty();
			return changed;
		}

		/// <summary>
		/// The next summon weapon an extra slot takes, or null (removed from the armory). A levy who is not a summoner takes at most one: the better of a sentry staff and a minion staff in stock. A summoner takes the
		/// unlimited minion staff he does not carry yet, then his sentry staff, then one-minion staves with a limit of their own (<see cref="SummonCap"/>) that fit beside what he carries.
		/// </summary>
		private Item NextDeployer(SoldierGear g, bool summoner, List<Item> carried, Func<Item, bool> free, SoldierRecord who = null)
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
				// ONE SUMMON ITEM: a levy who is not a summoner carries a single minion staff OR a single sentry (the better one in stock); none when the armory's rules keep summon weapons for summoners.
				if (!Rules.SummonsForOthers || hasSentry || hasStaff) return null;
				// SUMMONERS FIRST: a levy who is not a summoner is dealt only the staves no summoner could still use (see SummonerWants), the leftovers.
				var wants = SummonerWantList(who);
				return TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => (IsIssuableSentry(it) || IsIssuableSummonStaff(it)) && Fits(it) && !SummonerWants(wants, it), WeaponRank.Of);
			}
			Item p = null;
			if (!hasUnlimited) p = TakeStaff(it => !SummonCap.IsCapped(it));
			if (p == null && !hasSentry) p = TakeSentry();
			p ??= TakeStaff(it => SummonCap.IsCapped(it));
			return p;
		}

		/// <summary>True when this armory's sidearm limit still lets the levy carry one more sidearm. Summoners are not limited.</summary>
		private bool SidearmRoom(SoldierGear g, bool summoner)
		{
			if (summoner) return true;
			int n = 0; foreach (var s in g.Sidearms()) if (!IsAir(s)) n++;
			return n < Rules.SidearmLimit;
		}

		/// <summary>Gives back to the shelf the sidearms a levy who is not a summoner carries beyond this armory's limit (extra slots first), and the summon weapons the rules keep for summoners. Locked slots stay. True if anything went back.</summary>
		private bool EnforceSidearmRules(SoldierGear g, bool summoner)
		{
			if (summoner || !Rules.LimitsLoadouts) return false;
			bool any = false;
			int count = 0; foreach (var s in g.Sidearms()) if (!IsAir(s)) count++;
			for (int i = LevyConstants.MaxSidearms - 1; i >= 0; i--)
			{
				int slot = SoldierGear.SidearmSlotAt(i); var cur = g.Get(slot);
				if (g.IsLocked(slot) || IsAir(cur)) continue;
				bool denied = !Rules.SummonsForOthers && SidearmPolicy.IsDeployer(cur);
				if (!denied && count <= Rules.SidearmLimit) continue;
				var rem = DepositOld(cur);
				if (!IsAir(rem)) Spill(rem, protect: true);
				g.Set(slot, new Item()); count--; any = true;
			}
			if (any) MarkDirty();
			return any;
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
			var wants = IsSummonerSoldier(soldier) ? null : SummonerWantList(soldier); // summoners first: a levy who is not one only upgrades onto staves no summoner could still use
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
					it => (sentry ? IsIssuableSentry(it) : IsIssuableSummonStaff(it) && SummonCap.IsCapped(it) == capped) && WeaponRank.Of(it) >= need && Free(it) && SidearmPolicy.Fits(g.Weapon, it, others) && !SummonerWants(wants, it), WeaponRank.Of);
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
