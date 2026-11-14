using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Integrations;
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

		/// <summary>Where a shelf candidate physically is: a slot array of an armory (<see cref="Owner"/> + <see cref="Arr"/>), or a Magic Storage network (<see cref="Heart"/>, withdrawn through <see cref="MagicStorageBridge"/>).</summary>
		private sealed class ShelfSrc { public ArmoryTE Owner; public Item[] Arr; public object Heart; }

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
					if (g == null || rec.LoadoutLocked || rec.Disarmed || g.IsLocked(SoldierGear.SlotWeapon)) continue;
					bool hasW = !IsAir(g.Weapon);
					if (hasW && (g.Weapon.maxStack > 1 || WeaponClassifier.GetProfile(g.Weapon).Family == WeaponFamily.Thrown)) continue; // thrown stacks are restocked, never swapped
					if (hasW && !ReferenceEquals(rec, requester) && IsBusy(rec)) continue;
					var lev = new AllocLevy { Tag = rec, Id = rec.Id, Cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs), Current = hasW ? g.Weapon : null, Mana = rec.MaxMana }; // crystals plus what his armor and open accessory slots add
					if (lev.Cls == null && !classMatch) lev.Flex = ArmorableClasses(rec, hasW);
					if (classMatch && hasW) lev.ClassLock = WeaponClassifier.ItemDamageClass(g.Weapon);
					var rule = PrimaryRule(rec);
					Func<Item, bool> summonRule = null; // the rule once the class balance has made him a summoner (an armorless levy drafted onto the summon class may be dealt a staff)
					var pouch = g.Ammo;
					bool pouchFixed = g.IsLocked(SoldierGear.SlotAmmo) && !IsAir(pouch); // a hand-assigned pouch is never swapped: the weapon must fire it
					var self = rec;
					lev.Ok = it =>
					{
						if (!IsIssuable(it)) return false;
						if (ModWeaponGates.Locked(it)) return false; // its own use gate is shut in this world
						if (!WeaponUse.CanEverPay(it, (int)lev.Mana)) return false; // a cast dearer than his whole mana pool (crystals + what his gear adds) would never fire
						if (it.maxStack > 1 && (hasW || WeaponClassifier.GetProfile(it).Family != WeaponFamily.Thrown)) return false; // a bare-handed levy may be dealt a thrown stack; a stack is never a swap
						var useRule = rule;
						if (lev.Virtual && lev.Cls == DamageClass.Summon) useRule = summonRule ??= PrimaryRule(rec, true);
						if (useRule != null && !useRule(it)) return false;
						if (pouchFixed && it.useAmmo > 0 && pouch.ammo != it.useAmmo) return false;
						if (!hasW) return true;
						if (!HasAmmoFor(it, self, onHand) && !(it.useAmmo > 0 && g.Weapon.useAmmo == it.useAmmo)) return false; // a dry gun may trade up to a better gun of the same ammo
						if (WeaponClassifier.GetProfile(it).Family == WeaponFamily.Thrown) return false;
						// An upgrade onto ammo he does not carry needs a real stock of it, so a swap never leaves a gun with an empty pouch.
						if (it.useAmmo <= 0 || (!IsAir(pouch) && pouch.ammo == it.useAmmo)) return true;
						if (!stockOk.TryGetValue(it.useAmmo, out bool enough)) stockOk[it.useAmmo] = enough = AmmoStockAtLeast(it.useAmmo, LevyConstants.AutoUpgradeMinAmmoStock);
						return enough;
					};
					if (hasW && IsSummonerSoldier(rec) && WeaponClassifier.GetProfile(g.Weapon).Family is WeaponFamily.Summon or WeaponFamily.Sentry)
						lev.Yields = c => WeaponClassifier.GetProfile(c.It).Family == WeaponFamily.Whip; // a staff or sentry in a summoner's hand yields to any whip regardless of score (it becomes a sidearm)
					lev.Fires = it => HasAmmoFor(it, self, onHand) || (hasW && it.useAmmo > 0 && g.Weapon.useAmmo == it.useAmmo);
					lev.AllowDry = !hasW; // an issue falls back to a gun with no bullets before it crosses class; an upgrade never trades onto one
					plan.Levies.Add(lev);
					if (ReferenceEquals(rec, requester)) plan.Requester = lev;
					if (hasW) lev.Own = WeaponAllocator.MakeCand(g.Weapon, 5, seq++, AllocSlot.Primary, lev, null);
					if (lev.Own != null) plan.Cands.Add(lev.Own);
					if (!g.IsLocked(SoldierGear.SlotSidearm) && IsIssuableMelee(g.Sidearm) && g.Sidearm.maxStack <= 1)
						plan.Cands.Add(WeaponAllocator.MakeCand(g.Sidearm, 9, seq++, AllocSlot.Sidearm, lev, null)); // a blade carried as a sidearm: any primary outranks it
					// A minion staff or sentry a levy who is not a summoner carries as a sidearm is spare stock too: a summoner who needs a primary staff may have it, and the levy is dealt another later.
					for (int si = 0; si < LevyConstants.MaxSidearms && !IsSummonerSoldier(rec); si++) {
						int sslot = SoldierGear.SidearmSlotAt(si); var sit = g.Get(sslot);
						if (g.IsLocked(sslot) || IsAir(sit) || sit.maxStack > 1 || !(IsIssuableSummonStaff(sit) || IsIssuableSentry(sit))) continue;
						plan.Cands.Add(WeaponAllocator.MakeCand(sit, 9, seq++, AllocSlot.Sidearm, lev, null));
					}
				}
				void AddShelf(ArmoryTE owner, Item[] arr, int to, int pref)
				{
					var src = new ShelfSrc { Owner = owner, Arr = arr };
					for (int i = 0; i < to && i < arr.Length; i++) {
						var it = arr[i];
						if (IsAir(it) || !IsIssuable(it) || owner.IsReservedRef(it)) continue;
						if (it.maxStack > 1) {
							// A thrown stack serves several levies: one candidate per levy that could carry some; Carry draws each levy's share from the shelf (fair-share limited).
							if (WeaponClassifier.GetProfile(it).Family != WeaponFamily.Thrown) continue;
							for (int k = 0; k < plan.Levies.Count && k < LevyConstants.ThrownStackCandidates; k++) plan.Cands.Add(WeaponAllocator.MakeCand(it, pref, seq++, AllocSlot.Shelf, null, src));
							continue;
						}
						plan.Cands.Add(WeaponAllocator.MakeCand(it, pref, seq++, AllocSlot.Shelf, null, src));
					}
				}
				if (plan.Levies.Count > 0) {
					AddShelf(this, IssueWeapons, SidearmSlot, 0);
					AddShelf(this, Storage, Storage.Length, 1);
					int pref = 2;
					foreach (var o in OtherNetworked()) AddShelf(o, o.Storage, o.Storage.Length, pref++);
					// A connected Magic Storage network is part of the same pool: its weapons are dealt on rank like the armory's own, never only after the armory runs dry.
					if (!LegacySourceOrder) AddMsShelf(plan, pref, ref seq);
				}
			}
			finally { _armorableMemo = null; }
			return plan;
		}

		/// <summary>Adds the issuable weapons of the connected Magic Storage network as shelf candidates (at most one per levy of each type and prefix: a stack of identical staves is not offered a thousand times).</summary>
		private void AddMsShelf(AllocPlan plan, int pref, ref int seq)
		{
			var heart = MsHeart();
			if (heart == null) return;
			var src = new ShelfSrc { Heart = heart };
			var perKind = new Dictionary<(int, int), int>();
			int cap = Math.Max(1, plan.Levies.Count);
			foreach (var it in MagicStorageBridge.Snapshot(heart)) {
				if (IsAir(it) || it.maxStack > 1 || !IsIssuable(it)) continue;
				var key = (it.type, (int)it.prefix);
				perKind.TryGetValue(key, out int n);
				if (n >= cap) continue;
				perKind[key] = n + 1;
				plan.Cands.Add(WeaponAllocator.MakeCand(it, pref, seq++, AllocSlot.Shelf, null, src));
			}
		}

		// ---- plan memo: the army plan is a pure function of the shelves, the levies' gear and the rules, so when none of them changed since a plan that found nothing to do, the answer is the same
		/// <summary>A memoised plan is trusted for this many ticks at most, so anything the fingerprint does not read (a world flag that opens a weapon's gate) still gets looked at every couple of seconds.</summary>
		private const uint PlanMemoTicks = 120;
		private long _planFp = long.MinValue; private uint _planFpTick; private int _lastChangers;
		private long _claimFp = long.MinValue; private uint _claimFpTick; private HashSet<object> _claimSet;
		/// <summary>Self-check seam: plans skipped because the fingerprint matched, and plans actually built.</summary>
		internal static int PlanMemoHits, PlanBuilds;

		/// <summary>
		/// A hash of everything the army plan reads: the weapon rack, ammo row and storage of this armory and of the armories networked with it (each item by type, prefix, stack and identity), every served levy's gear,
		/// lock mask, mana, slots and whether he is busy, the armory rules and the issue mode. <see cref="long.MinValue"/> when it cannot be taken (a Magic Storage network is attached: its contents are not hashed).
		/// A levy other than the requester who is busy is left out of the plan, so the requester's own id joins the hash only when it matters (he is busy or not on the served list).
		/// </summary>
		private long PlanFingerprint(SoldierRecord requester)
		{
			if (MsHeart() != null) return long.MinValue;
			unchecked
			{
				ulong h = 14695981039346656037UL;
				void M(long v) { h = (h ^ (ulong)v) * 1099511628211UL; }
				void MI(Item it)
				{
					if (it == null || it.IsAir) { M(0); return; }
					M(it.type); M(it.prefix); M(it.stack); M(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(it));
				}
				void MA(Item[] a, int to) { if (a == null) { M(-7); return; } for (int i = 0; i < a.Length && i < to; i++) MI(a[i]); }
				M(Rules.Rev); M(Common.Config.LevyConfig.Get.ClassMatchIssue ? 1 : 0); M(_reservedRefs.Count);
				MA(IssueWeapons, SidearmSlot); MA(IssueAmmo, int.MaxValue); MA(Storage, int.MaxValue);
				foreach (var o in OtherNetworked()) { M(o.ID); M(o._reservedRefs.Count); MA(o.Storage, int.MaxValue); }
				bool present = false;
				foreach (var rec in ServedSoldiers())
				{
					var g = rec?.Gear; if (g == null) { M(-3); continue; }
					if (ReferenceEquals(rec, requester)) present = true;
					M(rec.Id); M(g.LockMask); M(rec.LoadoutLocked ? 1 : 0); M(rec.Disarmed ? 1 : 0); M((long)rec.MaxMana); M(rec.AccessorySlots); M((int)rec.Grade); M(IsBusy(rec) ? 1 : 0);
					MI(g.Weapon); MI(g.Sidearm); MI(g.Head); MI(g.Body); MI(g.Legs); MI(g.Ammo); MA(g.ExtraSidearms, int.MaxValue); MA(g.Accessories, int.MaxValue);
				}
				if (requester != null && (!present || IsBusy(requester))) M(requester.Id + 100000L);
				long r = (long)h; return r == long.MinValue ? r + 1 : r;
			}
		}

		/// <summary>
		/// Decides and carries out the whole army's primary weapons. Called when <paramref name="requester"/> is bare-handed (an issue) or asks for an upgrade. A levy whose weapon changes is
		/// handed his new one, his old one goes back to the shelf (or straight to the levy who is dealt it), a levy who lost his weapon to a better-matched one is re-armed from what is left,
		/// and every changed levy gets a normal <see cref="IssueGear"/> pass (ammo, armor, sidearm). True if the requester's own primary changed.
		/// </summary>
		internal bool AllocateArmy(SoldierRecord requester, bool upgrade)
		{
			if (_allocating || requester?.Gear == null || Main.netMode == NetmodeID.MultiplayerClient) return false;
			long fp = PlanFingerprint(requester); uint now0 = Main.GameUpdateCount;
			if (fp != long.MinValue && fp == _planFp && _lastChangers == 0 && now0 - _planFpTick < PlanMemoTicks) { PlanMemoHits++; return false; } // nothing changed since a plan that found nothing to deal
			_allocating = true;
			using var _rs = ArmoryRules.Enter(Rules);
			long pa = global::Levy.Common.Util.LevyProf.Begin();
			try {
				PlanBuilds++; _lastChangers = -1;
				long pb = global::Levy.Common.Util.LevyProf.Begin();
				var plan = BuildAllocation(requester);
				global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.ArmBuild, pb); pb = global::Levy.Common.Util.LevyProf.Begin();
				WeaponAllocator.Plan(plan.Levies, plan.Cands, Rules.Alloc);
				global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.ArmPlan, pb); pb = global::Levy.Common.Util.LevyProf.Begin();
				try { return Carry(plan, requester, upgrade); }
				finally { global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.ArmCarry, pb); if (_lastChangers == 0) { _planFp = fp; _planFpTick = now0; } else _planFp = long.MinValue; }
			}
			catch (Exception e) {
				if (!_allocWarned) { _allocWarned = true; Levy.Instance?.Logger.Warn("Levy: the army weapon allocation failed, falling back to per-soldier picks: " + e); }
				return false;
			}
			finally { _allocating = false; global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.ArmAlloc, pa); }
		}

		private bool Carry(AllocPlan plan, SoldierRecord requester, bool upgrade)
		{
			var changers = plan.Levies.FindAll(l => l.Target != null && !ReferenceEquals(l.Target, l.Own));
			_lastChangers = changers.Count;
			if (changers.Count == 0) return false;
			var picked = new Dictionary<AllocLevy, Item>();
			var affected = new List<SoldierRecord>();
			void Touch(SoldierRecord r) { foreach (var a in affected) if (ReferenceEquals(a, r)) return; affected.Add(r); }
			// 1. Take every target out of where it is: a shelf slot, or the hand / belt of the levy who gives it up.
			foreach (var l in changers) {
				var c = l.Target; Item it = null;
				if (c.Slot == AllocSlot.Shelf && c.It.maxStack > 1) {
					var stack = new Item();
					if (DrawThrown(stack, c.It.type, LevyConstants.ThrownCarryMax) > 0) it = stack;
				}
				else if (c.Slot == AllocSlot.Shelf) {
					var src = (ShelfSrc)c.Src;
					if (src.Heart != null) {
						// Magic Storage: ask the network for exactly this weapon (type + prefix); what it hands back is what the levy holds. Nothing handed back = he keeps what he has.
						if (MagicStorageBridge.TryWithdrawExact(src.Heart, c.It, 1, out var got) && !IsAir(got)) it = got;
					}
					else {
						int idx = IndexOfRef(src.Arr, c.It);
						if (idx >= 0) it = src.Owner.TakeSlot(src.Arr, idx);
					}
				}
				else {
					var holder = (SoldierRecord)c.Phys.Tag; var hg = holder.Gear;
					if (c.Slot == AllocSlot.Primary && ReferenceEquals(hg.Weapon, c.It)) { it = c.It; hg.Weapon = new Item(); hg.ThrownRestockType = 0; Touch(holder); }
					else if (c.Slot == AllocSlot.Sidearm && TakeSidearmRef(hg, c.It)) { it = c.It; Touch(holder); }
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
			long fp = PlanFingerprint(who); uint now0 = Main.GameUpdateCount;
			if (fp != long.MinValue && fp == _claimFp && now0 - _claimFpTick < PlanMemoTicks) { PlanMemoHits++; return _claimSet; } // same shelves, gear and rules: the same plan
			try {
				PlanBuilds++;
				var plan = BuildAllocation(who);
				HashSet<object> set = null;
				if (plan.Levies.Count > 0)
				{
					WeaponAllocator.Plan(plan.Levies, plan.Cands, Rules.Alloc);
					set = new HashSet<object>(ReferenceEqualityComparer.Instance);
					foreach (var c in plan.Cands) if (c.Used) set.Add(c.It);
				}
				if (fp != long.MinValue) { _claimFp = fp; _claimFpTick = now0; _claimSet = set; }
				return set;
			}
			catch (Exception e) {
				if (!_allocWarned) { _allocWarned = true; Levy.Instance?.Logger.Warn("Levy: could not read the army weapon plan for the sidearm pick: " + e); }
				return null;
			}
		}
	}
}
