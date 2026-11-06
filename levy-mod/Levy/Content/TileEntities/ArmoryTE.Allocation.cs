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
	/// Army-wide weapon allocation (see <see cref="WeaponAllocator"/> for the rules). This half of the armory gathers the levies and the weapons they can be dealt, runs the plan, and carries it out:
	/// whenever a levy is bare-handed (<see cref="IssueGear"/>) or asks for an upgrade (<see cref="UpgradeWeapons"/>) the whole army's primaries are decided together and every change is made in one step,
	/// so nobody takes "the best item at his moment" at another class's expense. Server / single player only: a multiplayer client never issues, exactly as before.
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Set while a plan is being carried out, so the IssueGear calls it makes for the affected levies do not start another one.</summary>
		private bool _allocating;
		private bool _allocWarned;
		/// <summary>Memo for <see cref="ArmorableClasses"/> within one plan (the answer depends only on the three worn pieces, their locks and the view); null outside a plan.</summary>
		private Dictionary<(int, int, int, int, bool), HashSet<DamageClass>> _armorableMemo;

		private sealed class ShelfSrc { public ArmoryTE Owner; public Item[] Arr; }

		private sealed class AllocPlan
		{
			public readonly List<AllocLevy> Levies = new();
			public readonly List<AllocCand> Cands = new();
			public AllocLevy Requester;
		}

		/// <summary>True when the levy is mid-fight or mid-swing: the auto-upgrade never swaps his weapon then, so the allocation leaves it (and him) alone.</summary>
		private static bool IsBusy(SoldierRecord rec)
		{
			if (rec == null || !rec.HasBody) return false;
			if (Main.npc[rec.BodyIndex].ModNPC is not SoldierNPC s) return false;
			return s.State == SoldierState.Engage || s.State == SoldierState.Shaken || s.State == SoldierState.Routed || s.ItemAnimation > 0;
		}

		/// <summary>Gathers the levies this armory serves (plus <paramref name="requester"/>) and every weapon they could be dealt. Never changes any gear.</summary>
		private AllocPlan BuildAllocation(SoldierRecord requester)
		{
			var plan = new AllocPlan();
			var served = new List<SoldierRecord>(ServedSoldiers());
			if (requester != null) { bool present = false; foreach (var r in served) if (ReferenceEquals(r, requester)) { present = true; break; } if (!present) served.Add(requester); }
			var onHand = AmmoIdsOnHand();
			var stockOk = new Dictionary<int, bool>();
			bool classMatch = Common.Config.LevyConfig.Get.ClassMatchIssue;
			int seq = 0;
			_armorableMemo = new();
			try {
				foreach (var rec in served) {
					var g = rec?.Gear;
					if (g == null || rec.LoadoutLocked || g.IsLocked(SoldierGear.SlotWeapon)) continue;
					bool hasW = !IsAir(g.Weapon);
					if (hasW && (g.Weapon.maxStack > 1 || WeaponClassifier.GetProfile(g.Weapon).Family == WeaponFamily.Thrown)) continue; // thrown stacks are restocked, never swapped
					if (hasW && !ReferenceEquals(rec, requester) && IsBusy(rec)) continue;
					var lev = new AllocLevy { Tag = rec, Id = rec.Id, Cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs), Current = hasW ? g.Weapon : null };
					if (lev.Cls == null && !classMatch) lev.Flex = ArmorableClasses(rec, hasW);
					if (classMatch && hasW) lev.ClassLock = WeaponClassifier.ItemDamageClass(g.Weapon);
					var rule = PrimaryRule(rec);
					var pouch = g.Ammo;
					bool pouchFixed = g.IsLocked(SoldierGear.SlotAmmo) && !IsAir(pouch); // a hand-assigned pouch is never swapped: the weapon must fire it
					var self = rec;
					lev.Ok = it =>
					{
						if (!IsIssuable(it) || it.maxStack > 1) return false;
						if (rule != null && !rule(it)) return false;
						if (pouchFixed && it.useAmmo > 0 && pouch.ammo != it.useAmmo) return false;
						if (!hasW) return true;
						if (!HasAmmoFor(it, self, onHand) && !(it.useAmmo > 0 && g.Weapon.useAmmo == it.useAmmo)) return false; // a dry gun may trade up to a better gun of the same ammo
						if (WeaponClassifier.GetProfile(it).Family == WeaponFamily.Thrown) return false;
						// An upgrade onto ammo he does not carry needs a real stock of it, so a swap never leaves a gun with an empty pouch.
						if (it.useAmmo <= 0 || (!IsAir(pouch) && pouch.ammo == it.useAmmo)) return true;
						if (!stockOk.TryGetValue(it.useAmmo, out bool enough)) stockOk[it.useAmmo] = enough = AmmoStockAtLeast(it.useAmmo, LevyConstants.AutoUpgradeMinAmmoStock);
						return enough;
					};
					if (hasW && WeaponClassifier.GetProfile(g.Weapon).Family == WeaponFamily.Whip)
						lev.Yields = c => WeaponClassifier.GetProfile(c.It).Family == WeaponFamily.Summon; // a whip in hand yields to any minion staff regardless of score (the whip becomes the off-hand)
					lev.Fires = it => HasAmmoFor(it, self, onHand) || (hasW && it.useAmmo > 0 && g.Weapon.useAmmo == it.useAmmo);
					lev.AllowDry = !hasW; // an issue falls back to a gun with no bullets before it crosses class; an upgrade never trades onto one
					plan.Levies.Add(lev);
					if (ReferenceEquals(rec, requester)) plan.Requester = lev;
					if (hasW) lev.Own = WeaponAllocator.MakeCand(g.Weapon, 5, seq++, AllocSlot.Primary, lev, null);
					if (lev.Own != null) plan.Cands.Add(lev.Own);
					if (!g.IsLocked(SoldierGear.SlotSidearm) && IsIssuableMelee(g.Sidearm) && g.Sidearm.maxStack <= 1)
						plan.Cands.Add(WeaponAllocator.MakeCand(g.Sidearm, 9, seq++, AllocSlot.Sidearm, lev, null)); // a blade carried as a sidearm: any primary outranks it
				}
				void AddShelf(ArmoryTE owner, Item[] arr, int to, int pref)
				{
					var src = new ShelfSrc { Owner = owner, Arr = arr };
					for (int i = 0; i < to && i < arr.Length; i++) {
						var it = arr[i];
						if (IsAir(it) || it.maxStack > 1 || !IsIssuable(it) || owner.IsReservedRef(it)) continue;
						plan.Cands.Add(WeaponAllocator.MakeCand(it, pref, seq++, AllocSlot.Shelf, null, src));
					}
				}
				if (plan.Levies.Count > 0) {
					AddShelf(this, IssueWeapons, SidearmSlot, 0);
					AddShelf(this, Storage, Storage.Length, 1);
					int pref = 2;
					foreach (var o in OtherNetworked()) AddShelf(o, o.Storage, o.Storage.Length, pref++);
				}
			}
			finally { _armorableMemo = null; }
			return plan;
		}

		/// <summary>
		/// Decides and carries out the whole army's primary weapons. Called when <paramref name="requester"/> is bare-handed (an issue) or asks for an upgrade. A levy whose weapon changes is
		/// handed his new one, his old one goes back to the shelf (or straight to the levy who is dealt it), a levy who lost his weapon to a better-matched one is re-armed from what is left,
		/// and every changed levy gets a normal <see cref="IssueGear"/> pass (ammo, armor, sidearm). True if the requester's own primary changed.
		/// </summary>
		internal bool AllocateArmy(SoldierRecord requester, bool upgrade)
		{
			if (_allocating || requester?.Gear == null || Main.netMode == NetmodeID.MultiplayerClient) return false;
			_allocating = true;
			using var _rs = ArmoryRules.Enter(Rules);
			try {
				var plan = BuildAllocation(requester);
				WeaponAllocator.Plan(plan.Levies, plan.Cands, Rules.Alloc);
				return Carry(plan, requester, upgrade);
			}
			catch (Exception e) {
				if (!_allocWarned) { _allocWarned = true; Levy.Instance?.Logger.Warn("Levy: the army weapon allocation failed, falling back to per-soldier picks: " + e); }
				return false;
			}
			finally { _allocating = false; }
		}

		private bool Carry(AllocPlan plan, SoldierRecord requester, bool upgrade)
		{
			var changers = plan.Levies.FindAll(l => l.Target != null && !ReferenceEquals(l.Target, l.Own));
			if (changers.Count == 0) return false;
			var picked = new Dictionary<AllocLevy, Item>();
			var affected = new List<SoldierRecord>();
			void Touch(SoldierRecord r) { foreach (var a in affected) if (ReferenceEquals(a, r)) return; affected.Add(r); }
			// 1. Take every target out of where it is: a shelf slot, or the hand / belt of the levy who gives it up.
			foreach (var l in changers) {
				var c = l.Target; Item it = null;
				if (c.Slot == AllocSlot.Shelf) {
					var src = (ShelfSrc)c.Src;
					int idx = IndexOfRef(src.Arr, c.It);
					if (idx >= 0) it = src.Owner.TakeSlot(src.Arr, idx);
				}
				else {
					var holder = (SoldierRecord)c.Phys.Tag; var hg = holder.Gear;
					if (c.Slot == AllocSlot.Primary && ReferenceEquals(hg.Weapon, c.It)) { it = c.It; hg.Weapon = new Item(); hg.ThrownRestockType = 0; Touch(holder); }
					else if (c.Slot == AllocSlot.Sidearm && ReferenceEquals(hg.Sidearm, c.It)) { it = c.It; hg.Sidearm = new Item(); Touch(holder); }
				}
				picked[l] = it;
			}
			// 2. Hand each changer his new primary; what he put down goes back to storage (unless another changer was dealt it, in which case it is already gone from his hand).
			bool requesterChanged = false;
			foreach (var l in changers) {
				var neu = picked[l];
				if (IsAir(neu)) continue;
				var rec = (SoldierRecord)l.Tag; var g = rec.Gear;
				var old = g.Weapon;
				g.Weapon = neu; g.ThrownRestockType = 0;
				if (!IsAir(old)) { var rem = DepositOld(old); if (!IsAir(rem)) Spill(rem, protect: true); }
				ShelveSidearmForPrimary(g, neu, WeaponClassifier.ItemDamageClass(old) != WeaponClassifier.ItemDamageClass(neu));
				Touch(rec);
				if (ReferenceEquals(rec, requester)) requesterChanged = true;
			}
			MarkDirty();
			// 3. Everyone affected gets the rest of an issue (ammo for the new weapon, a sidearm, armor aimed at it); the requester is in the middle of his own.
			uint now = Main.GameUpdateCount;
			foreach (var rec in affected) {
				if (!ReferenceEquals(rec, requester)) {
					IssueGear(rec);
					if (upgrade) rec.Gear.UpgradeCheckTick = now + (uint)LevyConstants.AutoUpgradeSwapCooldownTicks;
				}
				if (Main.netMode != NetmodeID.MultiplayerClient && rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is SoldierNPC sn) sn.NotifyGearChanged();
			}
			return requesterChanged;
		}

		/// <summary>
		/// The weapons the army's plan has spoken for (shelf items some levy is to be dealt, now or as an upgrade), by reference, or null when no army is known (the caller then falls back to
		/// reserving the top of the shelf). A sidearm may only come from what is NOT in this set: blades no melee levy wants.
		/// </summary>
		private HashSet<object> ClaimedWeapons(SoldierRecord who)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return null;
			using var _rs = ArmoryRules.Enter(Rules);
			try {
				var plan = BuildAllocation(who);
				if (plan.Levies.Count == 0) return null;
				WeaponAllocator.Plan(plan.Levies, plan.Cands, Rules.Alloc);
				var set = new HashSet<object>(ReferenceEqualityComparer.Instance);
				foreach (var c in plan.Cands) if (c.Used) set.Add(c.It);
				return set;
			}
			catch (Exception e) {
				if (!_allocWarned) { _allocWarned = true; Levy.Instance?.Logger.Warn("Levy: could not read the army weapon plan for the sidearm pick: " + e); }
				return null;
			}
		}
	}
}
