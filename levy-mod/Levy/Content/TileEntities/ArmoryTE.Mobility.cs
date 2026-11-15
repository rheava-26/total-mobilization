using System;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// The two mobility slots of every levy: Wings and Boots. They are open from the start and take no accessory slot. The armory deals the best pair of wings and the best pair of boots it holds (the accessory ranking, with
	/// what the item does for movement on top, and the player's Preferred items first) into them, and never deals wings or boots into an ordinary accessory slot. They are looked at on the levy's own gear timer,
	/// with the rest of his gear (<see cref="UpgradeAccessories"/>), and once when he is issued his kit (<see cref="IssueGear"/>).
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Score of a pair of wings or boots for the mobility slots: the accessory score, plus a measured movement value (run speed, rocket flight, jump height) so a plainly better pair wins when the ranking is level.</summary>
		private static float MobilityScore(Item it, DamageClass cls) => AccessoryScore(it, cls) + MobilityBonus(it);

		/// <summary>The movement part of <see cref="MobilityScore"/>: the measured movement value, and a large step for boots that make a levy run faster, so a shoe that only climbs, dashes or jumps (Frog Leg, Shoe Spikes, Tabi) never takes the slot from real boots.</summary>
		private static float MobilityBonus(Item it) => AccessoryMobility.Value(it) * LevyConstants.Armory.MobilitySlotValueWeight + (MobilitySlots.IsBoots(it) && AccessoryMobility.IsRunner(it) ? LevyConstants.Armory.MobilityRunnerBoots : 0f);

		/// <summary>An accessory that goes in an ordinary slot: useful, and neither wings nor boots (those have slots of their own).</summary>
		private static bool Ordinary(Item it) => WeaponClassifier.IsUsefulAccessory(it) && !MobilitySlots.Is(it);

		/// <summary>True when <paramref name="cand"/> may be worn beside <paramref name="w"/> (never two of one type, whatever the mods forbid pairing). An empty slot allows anything.</summary>
		private static bool PairOk(Item w, Item cand)
		{
			if (IsAir(w)) return true;
			if (w.type == cand.type) return false;
			try { if (!ItemLoader.CanAccessoryBeEquippedWith(w, cand)) return false; } catch (Exception) { }
			return true;
		}

		/// <summary>The accessory rules of <see cref="CanWearWith(Item[], Item)"/> applied to what the levy wears in his mobility slots too (a mod may forbid two items together wherever they sit).</summary>
		private static bool CanWearWith(Item[] worn, Item it, SoldierGear g) => CanWearWith(worn, it) && (g == null || (PairOk(g.Wings, it) && PairOk(g.Boots, it)));

		/// <summary>True for a pair of wings (<paramref name="wings"/>) or of boots that does something for a levy (the useless-accessory list, info and fishing gear, still applies).</summary>
		private static bool MobilityKind(bool wings, Item it) => (wings ? MobilitySlots.IsWings(it) : MobilitySlots.IsBoots(it)) && WeaponClassifier.IsUsefulAccessory(it);

		/// <summary>Best wings (<paramref name="wings"/> true) or boots on the shelf for this levy that beat what he wears by the usual margin; removes it. Null if none.</summary>
		public virtual Item TakeMobility(SoldierRecord soldier, bool wings)
		{
			var g = soldier?.Gear; if (g == null) return null;
			var cur = wings ? g.Wings : g.Boots;
			Item other = wings ? g.Boots : g.Wings;
			var cls = AccClassOf(soldier);
			bool empty = IsAir(cur);
			float curBase = empty ? 0f : AccessoryScore(cur, cls);
			float curScore = empty ? 0f : curBase + MobilityBonus(cur);
			float need = empty ? float.NegativeInfinity : curScore + Math.Max(Math.Abs(curBase) * (LevyConstants.AutoUpgradeScoreMargin - 1f), 1f); // the usual margin, taken on the accessory score (the movement step is not a score to be beaten by 15%)
			bool Pred(Item it) => MobilityKind(wings, it) && PairOk(other, it) && (empty || (!Preferred(cur, it) && MobilityScore(it, cls) >= need)) && CanWearWith(g.Accessories, it);
			return TakeBest(null, 0, 0, 1, Pred, it => MobilityScore(it, cls));
		}

		/// <summary>
		/// One look at a levy's mobility slots. Wings or boots found in an unlocked ordinary accessory slot (a kit from before the slots, or a hand-placed one the lock no longer covers) move into the empty mobility slot or go back
		/// to the shelf; then each mobility slot is filled, or upgraded when a pair on the shelf beats it by the upgrade margin. The old pair goes back to storage. True if anything changed.
		/// </summary>
		private bool MobilitySlotsStep(SoldierRecord w)
		{
			var g = w?.Gear;
			if (g == null || LevyIsClosed(w)) return false;
			bool changed = false;
			for (int i = 0; i < g.Accessories.Length; i++)
			{
				var a = g.Accessories[i];
				if (IsAir(a) || g.IsLocked(SoldierGear.SlotAcc0 + i) || !MobilitySlots.Is(a)) continue;
				bool wings = MobilitySlots.IsWings(a);
				int slot = wings ? SoldierGear.SlotWings : SoldierGear.SlotBoots;
				if (IsAir(g.Get(slot)) && !g.IsLocked(slot)) { g.Set(slot, a); g.Accessories[i] = new Item(); changed = true; continue; }
				g.Accessories[i] = new Item();
				var rem = DepositOld(a); if (!IsAir(rem)) Spill(rem, protect: true);
				changed = true;
			}
			for (int pass = 0; pass < 2; pass++)
			{
				bool wings = pass == 0;
				int slot = wings ? SoldierGear.SlotWings : SoldierGear.SlotBoots;
				if (g.IsLocked(slot)) continue;
				var got = TakeMobility(w, wings);
				if (got == null) continue;
				var old = g.Get(slot);
				g.Set(slot, got);
				if (!IsAir(old)) { var rem = DepositOld(old); if (!IsAir(rem)) Spill(rem, protect: true); }
				changed = true;
			}
			if (changed) MarkDirty();
			return changed;
		}
	}
}
