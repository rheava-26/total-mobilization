using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// Raise the floor: when new armor or accessories arrive, the worst-equipped levies take them first, whoever happens to ask. The best set of a class still goes to a levy of that class (a set is never
	/// taken by another class while a levy of its class would gain from it), and every swap needs the usual upgrade margin, so a swap always strictly improves the slot and nobody churns.
	/// (Weapons are dealt by <see cref="WeaponAllocator"/>, which already deals each class's stock worst-equipped first.)
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Worst-equipped-first gear upgrades (default on). Test seam: the gear audit turns it off to measure the old first-come behaviour.</summary>
		internal static bool FloorFirst = true;

		internal static float ArmorScoreOf(Item it) => ArmorScore(it);
		internal static float AccessoryScoreOf(Item it, DamageClass cls) => AccessoryScore(it, cls);
		internal static string StemOf(Item it) => ArmorStem(it);

		/// <summary>Bumped whenever armor or an accessory enters this armory's storage; the sweep runs again only when it changed (or after <see cref="SweepRecheckTicks"/>).</summary>
		private int _gearStockRev;
		private int _sweepRev = -1, _sweepServed = -1;
		private uint _sweepTick;
		private bool _sweeping;
		private const uint SweepRecheckTicks = 60 * 20;

		private static float KitScore(SoldierGear g)
		{
			float s = 0f;
			if (!IsAir(g.Head)) s += ArmorScore(g.Head);
			if (!IsAir(g.Body)) s += ArmorScore(g.Body);
			if (!IsAir(g.Legs)) s += ArmorScore(g.Legs);
			return s;
		}

		/// <summary>The class an accessory is judged for: his weapon's, else what his armor favours (same as <see cref="TakeAccessory"/>).</summary>
		private static DamageClass AccClassOf(SoldierRecord r)
		{
			var g = r.Gear;
			DamageClass cls = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null;
			return cls ?? ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
		}

		private static int AccSlotsOf(SoldierRecord r) => Math.Clamp(r.AccessorySlots, 0, Math.Min(SoldierGear.AccessorySlots, r.Gear.Accessories.Length));

		/// <summary>Average accessory strength over his slots (an empty slot counts 0), so a levy with room and nothing in it is the worst-equipped.</summary>
		private static float AccAverage(SoldierRecord r)
		{
			int n = AccSlotsOf(r); if (n == 0) return float.MaxValue;
			var cls = AccClassOf(r); float s = 0f;
			for (int i = 0; i < n; i++) { var a = r.Gear.Accessories[i]; if (!IsAir(a)) s += AccessoryScore(a, cls); }
			return s / n;
		}

		private bool FloorEligible(SoldierRecord r) => r?.Gear != null && !r.LoadoutLocked && !IsBusy(r);

		/// <summary>The levies this armory serves that may be re-equipped now.</summary>
		private List<SoldierRecord> FloorLevies()
		{
			var res = new List<SoldierRecord>();
			foreach (var r in ServedSoldiers()) if (FloorEligible(r) && !res.Contains(r)) res.Add(r);
			return res;
		}

		/// <summary>A levy's gear changed outside his own upgrade tick: the armor may have changed his class, so the weapon follows, he is issued what is missing, and clients are told.</summary>
		private void FloorTouch(SoldierRecord w)
		{
			ReconcileWeaponToArmor(w);
			IssueGear(w);
			MarkDirty();
			if (Main.netMode != NetmodeID.MultiplayerClient && w.HasBody && Main.npc[w.BodyIndex].ModNPC is SoldierNPC sn) sn.NotifyGearChanged();
		}

		/// <summary>
		/// The army-wide armor and accessory deal: levies are visited from the worst-equipped up, and each takes the best he is entitled to (the normal upgrade pick, with its margin). A no-op when nothing new
		/// arrived since the last sweep. Server / single player only.
		/// </summary>
		internal void FloorSweep()
		{
			if (!FloorFirst || _sweeping || Main.netMode == NetmodeID.MultiplayerClient) return;
			var levies = FloorLevies();
			if (levies.Count < 2) return;
			uint now = Main.GameUpdateCount;
			if (_sweepRev == _gearStockRev && _sweepServed == levies.Count && now - _sweepTick < SweepRecheckTicks) return;
			_sweeping = true;
			using var _rs = ArmoryRules.Enter(Rules);
			try {
				// armor sets: worst kit first (bare first of all)
				for (int pass = 0; pass < 4; pass++) {
					levies.Sort((a, b) => { int c = KitScore(a.Gear).CompareTo(KitScore(b.Gear)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
					bool any = false;
					foreach (var w in levies) {
						if (!w.Gear.HasWeapon) continue; // an armor upgrade follows the weapon's class; a bare-handed levy is issued normally
						if (TakeArmorSetCore(w, true, true)) { any = true; FloorTouch(w); }
					}
					if (!any) break;
				}
				// accessories: one slot per levy per visit, lowest average first, until nobody improves
				for (int pass = 0; pass < 60; pass++) {
					levies.Sort((a, b) => { int c = AccAverage(a).CompareTo(AccAverage(b)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
					bool any = false;
					foreach (var w in levies) if (AccessoryStep(w, levies)) { any = true; MarkDirty(); NotifyOnly(w); }
					if (!any) break;
				}
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: the floor sweep failed: " + e); }
			finally {
				_sweeping = false;
				_sweepRev = _gearStockRev; _sweepServed = levies.Count; _sweepTick = now;
			}
		}

		private static void NotifyOnly(SoldierRecord w)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient && w.HasBody && Main.npc[w.BodyIndex].ModNPC is SoldierNPC sn) sn.NotifyGearChanged();
		}

		/// <summary>For an armor upgrade by <paramref name="who"/>: does a levy of the set's class (other than him) stand to gain from a set of this raw score? Per-class best-off-needing levy, computed once.</summary>
		private Func<DamageClass, float, bool> ClassWantsSet(SoldierRecord who)
		{
			var low = new Dictionary<DamageClass, float>();
			foreach (var z in FloorLevies()) {
				if (ReferenceEquals(z, who) || !z.Gear.HasWeapon) continue;
				var zc = WeaponClassifier.ItemDamageClass(z.Gear.Weapon);
				if (zc == null) continue;
				float k = KitScore(z.Gear);
				if (!low.TryGetValue(zc, out var cur) || k < cur) low[zc] = k;
			}
			return (cls, raw) => low.TryGetValue(cls, out var k) && k * LevyConstants.AutoUpgradeScoreMargin < raw;
		}

		/// <summary>The requester's own accessory pass (the sweep has already served the worse-equipped): fills and upgrades his slots. True if anything changed.</summary>
		public virtual bool UpgradeAccessories(SoldierRecord soldier)
		{
			if (!FloorFirst || soldier?.Gear == null || Main.netMode == NetmodeID.MultiplayerClient) return false;
			bool changed = false;
			var all = FloorLevies();
			for (int i = 0, n = AccSlotsOf(soldier); i < n; i++) if (AccessoryStep(soldier, all)) changed = true; else break;
			return changed;
		}

		/// <summary>
		/// One accessory improvement for one levy: an empty slot is filled with the best he may wear, else his weakest slot is swapped for something at least the upgrade margin better. An accessory whose
		/// bonus is for another class is left for a levy of that class who would use it. The old one goes back to storage. True if the slot changed.
		/// </summary>
		private bool AccessoryStep(SoldierRecord w, List<SoldierRecord> all)
		{
			var g = w.Gear;
			int n = AccSlotsOf(w);
			if (n == 0 || LevyIsClosed(w)) return false;
			var cls = AccClassOf(w);
			int target = -1; float tScore = float.MaxValue; bool empty = false;
			// Mobility: a levy with none takes one into his weakest unlocked slot; his only mobility accessory is never swapped for a non-mobility one, only for a better mobility one.
			float bestMob = MobilityFirst ? AccessoryMobility.Best(g.Accessories, -1) : 0f;
			int mobSlot = -1;
			if (bestMob > 0f) for (int i = 0; i < n; i++) if (!IsAir(g.Accessories[i]) && AccessoryMobility.Value(g.Accessories[i]) >= bestMob) { mobSlot = i; break; }
			var teamClaimed = Common.Team.TeamAccessory.Claimed(all, w); // team effects the rest of the army (or the commander) already wears
			bool soleMob = mobSlot >= 0 && AccessoryMobility.Best(g.Accessories, mobSlot) <= 0f;
			for (int i = 0; i < n; i++) {
				if (g.IsLocked(SoldierGear.SlotAcc0 + i)) continue;
				if (IsAir(g.Accessories[i])) { target = i; tScore = 0f; empty = true; break; }
				if (soleMob && i == mobSlot) continue;
				float sc = AccessoryScore(g.Accessories[i], cls) + Common.Team.TeamAccessory.Bonus(g.Accessories[i], teamClaimed);
				if (sc < tScore) { target = i; tScore = sc; }
			}
			Item a = null;
			// a better mobility accessory may replace his only one
			if (soleMob && !g.IsLocked(SoldierGear.SlotAcc0 + mobSlot) && !empty) {
				var wm = (Item[])g.Accessories.Clone(); wm[mobSlot] = new Item();
				bool UPred(Item it) => WeaponClassifier.IsUsefulAccessory(it) && CanWearWith(wm, it) && AccessoryMobility.Value(it) > bestMob && !ClassReserved(it, w, cls, all);
				a = TakeBest(null, 0, 0, 1, UPred, it => AccessoryMobility.Value(it) * 100000f + AccessoryScore(it, cls));
				if (a != null) target = mobSlot;
			}
			if (a == null) {
				if (target < 0) return false;
				var wornEx = (Item[])g.Accessories.Clone();
				wornEx[target] = new Item();
				float stack = MobilityFirst && AccessoryMobility.Best(wornEx) > 0f ? LevyConstants.Armory.MobilityStackBonus : 0f;
				float need = empty ? 0f : Math.Max(tScore * LevyConstants.AutoUpgradeScoreMargin, tScore + 1f);
				var claimed = Common.Team.TeamAccessory.Claimed(all, w);
				float Sc(Item it) => AccessoryScore(it, cls) + (stack > 0f && AccessoryMobility.Is(it) ? stack : 0f) + Common.Team.TeamAccessory.Bonus(it, claimed);
				// a levy with no mobility accessory takes the best one stocked into this slot before anything else
				if (MobilityFirst && bestMob <= 0f) {
					bool MPred(Item it) => WeaponClassifier.IsUsefulAccessory(it) && CanWearWith(wornEx, it) && AccessoryMobility.Is(it) && !ClassReserved(it, w, cls, all);
					a = TakeBest(null, 0, 0, 1, MPred, it => AccessoryMobility.Value(it) * 100000f + AccessoryScore(it, cls));
				}
				if (a == null) {
					bool Pred(Item it) => WeaponClassifier.IsUsefulAccessory(it) && CanWearWith(wornEx, it) && (empty || Sc(it) >= need) && !ClassReserved(it, w, cls, all);
					a = TakeBest(null, 0, 0, 1, Pred, Sc);
				}
			}
			if (a == null) return false;
			var old = g.Accessories[target];
			g.Accessories[target] = a;
			if (!IsAir(old)) { var rem = DepositOld(old); if (!IsAir(rem)) Spill(rem, protect: true); }
			MarkDirty();
			return true;
		}

		private static bool LevyIsClosed(SoldierRecord w) => w.LoadoutLocked;

		/// <summary>True when <paramref name="it"/> adds damage for some class other than the taker's and a levy of that class would put it on (a free slot or a clearly weaker one), so it is left for him.</summary>
		private bool ClassReserved(Item it, SoldierRecord taker, DamageClass takerCls, List<SoldierRecord> all)
		{
			if (takerCls != null && ArmorDoctrine.AccessoryClassFit(it, takerCls) > 0f) return false;
			foreach (var z in all) {
				if (ReferenceEquals(z, taker)) continue;
				var zc = AccClassOf(z);
				if (zc == null || zc == takerCls || ArmorDoctrine.AccessoryClassFit(it, zc) <= 0f) continue;
				int n = AccSlotsOf(z);
				float zs = AccessoryScore(it, zc);
				for (int i = 0; i < n; i++) {
					if (z.Gear.IsLocked(SoldierGear.SlotAcc0 + i)) continue;
					var cur = z.Gear.Accessories[i];
					if (!IsAir(cur) && cur.type == it.type) { i = n; break; }
					bool better = IsAir(cur) || zs >= AccessoryScore(cur, zc) * LevyConstants.AutoUpgradeScoreMargin;
					if (!better) continue;
					var ex = (Item[])z.Gear.Accessories.Clone(); ex[i] = new Item();
					if (CanWearWith(ex, it)) return true;
				}
			}
			return false;
		}
	}
}
