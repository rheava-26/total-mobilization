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
	/// Armor and accessory rules of a levy's own gear check (see <see cref="UpgradeWeapons"/>): each levy looks at the shelf on his own timer, takes the best free piece for a slot when it beats what he wears by the usual
	/// upgrade margin, and gives back what it replaced. A set is never taken by another class while a levy of its class would gain from it, and a class-specific accessory is left for a levy of that class,
	/// so a swap always strictly improves the slot and nobody churns. There is no army-wide pass: worse-equipped levies pick up what the better-equipped ones put back on their own next look.
	/// (Weapons are dealt by <see cref="WeaponAllocator"/>, which already deals each class's stock worst-equipped first.)
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Worst-equipped-first gear upgrades (default on). Test seam: the gear audit turns it off to measure the old first-come behaviour.</summary>
		internal static bool FloorFirst = true;

		internal static float ArmorScoreOf(Item it) => ArmorScore(it);
		internal static float AccessoryScoreOf(Item it, DamageClass cls) => AccessoryScore(it, cls);
		internal static string StemOf(Item it) => ArmorStem(it);

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

		/// <summary>Mobility accessories among the first <paramref name="n"/> slots.</summary>
		private static int MobilityWorn(Item[] worn, int n)
		{
			int c = 0;
			if (worn != null) for (int i = 0; i < n && i < worn.Length; i++) if (!IsAir(worn[i]) && AccessoryMobility.Is(worn[i])) c++;
			return c;
		}

		/// <summary>True when one more mobility accessory can join <paramref name="worn"/> and still leave a slot for a non-mobility one (every levy keeps one of each; a single slot is not split).</summary>
		private static bool MobilityRoom(Item[] worn, int n) => !ReserveSlots || n < 2 || MobilityWorn(worn, n) + 1 <= n - 1;

		/// <summary>Every levy keeps one slot for mobility and one for the rest (default on). Test seam: the loadout check turns it off to show the old fill.</summary>
		internal static bool ReserveSlots = true;

		private bool FloorEligible(SoldierRecord r) => r?.Gear != null && !r.LoadoutLocked && !r.Disarmed && !IsBusy(r);

		/// <summary>The levies this armory serves that may be re-equipped now.</summary>
		private List<SoldierRecord> FloorLevies()
		{
			var res = new List<SoldierRecord>();
			foreach (var r in ServedSoldiers()) if (FloorEligible(r) && !res.Contains(r)) res.Add(r);
			return res;
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

		/// <summary>A levy's own accessory pass: fills and upgrades his slots. True if anything changed.</summary>
		public virtual bool UpgradeAccessories(SoldierRecord soldier)
		{
			if (!FloorFirst || soldier?.Gear == null || Main.netMode == NetmodeID.MultiplayerClient) return false;
			using var _rs = ArmoryRules.Enter(Rules); // the player's preferred pieces count in every score below
			bool changed = false;
			if (MobilitySlotsStep(soldier)) changed = true; // wings and boots first: they have their own slots and never take an accessory slot
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
			bool mobFirst = MobilityFirst && !g.HasWings; // wings in his wings slot cover his height: a jump accessory is then an ordinary one
			var cls = AccClassOf(w);
			int target = -1; float tScore = float.MaxValue; bool empty = false;
			// Mobility: a levy with none takes one into his weakest unlocked slot; his only mobility accessory is never swapped for a non-mobility one, only for a better mobility one.
			float bestMob = mobFirst ? AccessoryMobility.Best(g.Accessories, -1) : 0f;
			int mobSlot = -1;
			if (bestMob > 0f) for (int i = 0; i < n; i++) if (!IsAir(g.Accessories[i]) && AccessoryMobility.Value(g.Accessories[i]) >= bestMob) { mobSlot = i; break; }
			long pt = global::Levy.Common.Util.LevyProf.Begin(); var teamClaimed = Common.Team.TeamAccessory.Claimed(all, w); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsTeam, pt); // team effects the rest of the army (or the commander) already wears
			bool soleMob = mobSlot >= 0 && AccessoryMobility.Best(g.Accessories, mobSlot) <= 0f;
			// every slot holds mobility (an older kit, or nothing else was stocked): the weakest of the extra ones is given up as soon as a non-mobility accessory is stocked
			bool allMob = mobFirst && ReserveSlots && n >= 2 && !AnyEmpty(g, n) && MobilityWorn(g.Accessories, n) >= n;
			for (int i = 0; i < n; i++) {
				if (g.IsLocked(SoldierGear.SlotAcc0 + i)) continue;
				if (IsAir(g.Accessories[i])) { target = i; tScore = 0f; empty = true; break; }
				if (soleMob && i == mobSlot) continue;
				if (i == mobSlot && (allMob || (mobFirst && ReserveSlots && n >= 2))) continue; // his best mobility accessory is only ever swapped for a better mobility one (below): giving it up for a non-mobility pick, then taking it back as "better mobility", turned two mobility accessories and one other into a three-step rotation every check
				float sc = AccessoryScore(g.Accessories[i], cls) + Common.Team.TeamAccessory.Bonus(g.Accessories[i], teamClaimed);
				if (sc < tScore) { target = i; tScore = sc; }
			}
			Item a = null;
			// a better mobility accessory may replace his only one
			if (soleMob && !g.IsLocked(SoldierGear.SlotAcc0 + mobSlot) && !empty) {
				var wm = (Item[])g.Accessories.Clone(); wm[mobSlot] = new Item();
				bool UPred(Item it) => Ordinary(it) && AccessoryMobility.Value(it) > bestMob && (!mobFirst || MobilityRoom(wm, n)) && !Preferred(g.Accessories[mobSlot], it) && CanWearWith(wm, it, g) && !ClassReserved(it, w, cls, all); // cheapest tests first: the item must beat his mobility before anything is asked of the other levies
				pt = global::Levy.Common.Util.LevyProf.Begin(); a = TakeBest(null, 0, 0, 1, UPred, it => AccessoryMobility.Value(it) * 100000f + AccessoryScore(it, cls)); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsScan, pt);
				if (a != null) target = mobSlot;
			}
			// a direct upgrade of something he wears takes that slot (Lightning Boots over Hermes), never a second slot beside it
			if (a == null && !empty) {
				bool UpPred(Item it) {
					if (!Ordinary(it)) return false;
					int slot = AccessoryProgression.ReplacesSlot(g.Accessories, it, i => !g.IsLocked(SoldierGear.SlotAcc0 + i));
					if (slot < 0 || Preferred(g.Accessories[slot], it)) return false; // most of the shelf replaces nothing he wears: it ends here, before the other levies are asked
					var ex = (Item[])g.Accessories.Clone(); ex[slot] = new Item();
					return CanWearWith(ex, it, g) && !AccessoryProgression.Redundant(ex, it) && !ClassReserved(it, w, cls, all);
				}
				pt = global::Levy.Common.Util.LevyProf.Begin(); a = TakeBest(null, 0, 0, 1, UpPred, it => AccessoryScore(it, cls)); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsScan, pt);
				if (a != null) target = AccessoryProgression.ReplacesSlot(g.Accessories, a, i => !g.IsLocked(SoldierGear.SlotAcc0 + i));
			}
			if (a == null) {
				if (target < 0) return false;
				var wornEx = (Item[])g.Accessories.Clone();
				wornEx[target] = new Item();
				float stack = mobFirst && AccessoryMobility.Best(wornEx) > 0f ? LevyConstants.Armory.MobilityStackBonus : 0f;
				float need = empty || (allMob && target >= 0) ? 0f : Math.Max(tScore * LevyConstants.AutoUpgradeScoreMargin, tScore + 1f);
				bool mobRoom = !mobFirst || MobilityRoom(wornEx, n);
				pt = global::Levy.Common.Util.LevyProf.Begin(); var claimed = Common.Team.TeamAccessory.Claimed(all, w); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsTeam, pt);
				float Sc(Item it) => AccessoryScore(it, cls) + (stack > 0f && AccessoryMobility.Is(it) ? stack : 0f) + Common.Team.TeamAccessory.Bonus(it, claimed);
				// a levy with no mobility accessory takes the best one stocked into this slot before anything else
				if (mobFirst && bestMob <= 0f) {
					bool MPred(Item it) => Ordinary(it) && mobRoom && AccessoryMobility.Is(it) && CanWearWith(wornEx, it, g) && !AccessoryProgression.Redundant(wornEx, it) && !ClassReserved(it, w, cls, all);
					pt = global::Levy.Common.Util.LevyProf.Begin(); a = TakeBest(null, 0, 0, 1, MPred, it => AccessoryMobility.Value(it) * 100000f + AccessoryScore(it, cls)); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsScan, pt);
				}
				if (a == null) {
					bool Pred(Item it) => Ordinary(it) && (empty || Sc(it) >= need) && (mobRoom || !AccessoryMobility.Is(it)) && (!allMob || !AccessoryMobility.Is(it))
						&& CanWearWith(wornEx, it, g) && !AccessoryProgression.Redundant(wornEx, it) && !ClassReserved(it, w, cls, all); // the score margin first: most of the shelf is no better than what he wears
					pt = global::Levy.Common.Util.LevyProf.Begin(); a = TakeBest(null, 0, 0, 1, Pred, Sc); global::Levy.Common.Util.LevyProf.EndCount(global::Levy.Common.Util.LevyProf.AsScan, pt);
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

		private static bool AnyEmpty(SoldierGear g, int n) { for (int i = 0; i < n; i++) if (IsAir(g.Accessories[i])) return true; return false; }

		/// <summary>True when the player marked <paramref name="worn"/> as preferred and <paramref name="cand"/> is not: a preferred piece is never swapped out for an ordinary one.</summary>
		private static bool Preferred(Item worn, Item cand) => !IsAir(worn) && GearPreferred(worn) && !GearPreferred(cand);

		/// <summary>True when the current armory's rules mark this armor piece or accessory as preferred.</summary>
		internal static bool GearPreferred(Item it) => it != null && !it.IsAir && ArmoryRules.Current?.OverrideOf(it.type) is { Rank: RankOverrideMode.Prefer };

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
