using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;

namespace Levy.Common.Weapons
{
	/// <summary>Where a candidate weapon physically is right now.</summary>
	internal enum AllocSlot { Shelf, Primary, Sidearm }

	/// <summary>One levy in the allocation: what he wears (his class), what he holds, and what he is able to use.</summary>
	internal sealed class AllocLevy
	{
		/// <summary>The caller's handle (a <see cref="Levy.Common.Soldiers.SoldierRecord"/>).</summary>
		public object Tag;
		public int Id;
		/// <summary>Damage class of the armor he wears; null = no armor class (bare, generic or hybrid armor), served after every classed levy.</summary>
		public DamageClass Cls;
		/// <summary>Unclassed levies only: the classes he could field armor for (empty / null = any). A pick bias, with class-less and then anything as the fallbacks.</summary>
		public HashSet<DamageClass> Flex;
		/// <summary>The primary in hand (air / null = bare-handed).</summary>
		public Item Current;
		/// <summary>Whether he may use an item: summon reservation, ranged helmet steer, ammo reachable, a hand-assigned pouch. Null = anything.</summary>
		public Func<Item, bool> Ok;
		/// <summary>Whether an item can actually fire right now (needs no ammo, or the ammo is in his pouch / the armory). Null = everything does.</summary>
		public Func<Item, bool> Fires;
		/// <summary>A bare-handed levy takes a weapon that cannot fire yet (its class beats a gun with no bullets) when nothing that fires fits; a levy with a weapon never trades for one.</summary>
		public bool AllowDry;
		/// <summary>Fixed-class companies: while he holds a classed weapon he only ever trades within that class.</summary>
		public DamageClass ClassLock;

		// ---- plan output
		/// <summary>The weapon he should end up with (null = nothing to give him; he keeps what he has). Equal to his own <see cref="Cand"/> when unchanged.</summary>
		public AllocCand Target;
		/// <summary>His own held weapon as a candidate (null when bare-handed).</summary>
		public AllocCand Own;
	}

	/// <summary>A weapon that can be handed out: on a shelf, or held by a levy (a primary he may lose, or a blade sidearm).</summary>
	internal sealed class AllocCand
	{
		public Item It;
		public float Rank;
		public DamageClass Cls;
		public int Type;
		/// <summary>Tie preference among equal ranks: lower first (0 = the player's weapon rack, then storage, then networked armories).</summary>
		public int Pref;
		public int Seq;
		public AllocSlot Slot;
		/// <summary>Who physically holds it (null on a shelf). Never changes.</summary>
		public AllocLevy Phys;
		/// <summary>Who currently has a claim on it (starts as <see cref="Phys"/>; cleared when that levy has moved on to something else).</summary>
		public AllocLevy Owner;
		public bool Used;
		/// <summary>Opaque shelf origin for the executor.</summary>
		public object Src;
	}

	/// <summary>
	/// The army's weapon assignment, as pure logic over levies and candidate weapons (the armory builds both and carries out the result).
	/// One pass decides every levy's primary at once, instead of each levy taking the best item at the moment he asks:
	///  a) CLASS-MATCHED FIRST. Levies are grouped by the damage class of their armor; within a class the stock of that class is dealt out, bare-handed and worst-equipped levies first.
	///  b) UNCLASSED LEVIES LAST. Levies with no armor class (and any classed levy whose class has nothing left) are served after every classed levy, from what remains.
	///  c) VARIETY. Each pick is made as <c>TryPickVaried</c> always has: the best copy of each weapon type within <see cref="LevyConstants.WeaponVarietyTopFraction"/> of the best, chosen by the levy's stable id.
	///  d) SIDEARMS take only what the plan left over (the executor asks <see cref="Levy.Content.TileEntities.ArmoryTE"/> for the unclaimed blades).
	///  e) A levy keeps what he holds unless something at least <see cref="LevyConstants.AutoUpgradeScoreMargin"/> times better is on offer (or his weapon is not of his class and one that is, is). He can only
	///     take a weapon from a levy who has less claim to it (class-mismatched or unclassed, or a blade carried as a sidearm), never from a better-matched one.
	/// </summary>
	internal static class WeaponAllocator
	{
		/// <summary>The ranking key. Kept in one place so the ranking decision (the current score, or another) is a one-line change.</summary>
		internal static Func<Item, float> RankOf = WeaponClassifier.Score;

		/// <summary>How much claim a levy has on a weapon: 2 = it is his armor's class, 1 = he has no armor class, 0 = a classed levy holding another class's weapon.</summary>
		private static int Entitle(AllocLevy lev, AllocCand c) => lev.Cls != null ? (c.Cls == lev.Cls ? 2 : 0) : 1;
		private static int HolderEntitle(AllocCand c) => c.Slot == AllocSlot.Sidearm ? -1 : Entitle(c.Owner, c);

		private static bool Avail(AllocLevy lev, AllocCand c)
		{
			if (c.Used) return false;
			if (c.Owner == null) return true;
			if (c.Owner == lev) return c.Slot == AllocSlot.Primary;
			return Entitle(lev, c) > HolderEntitle(c);
		}

		/// <summary>Builds a candidate (rank, class) for a weapon; <paramref name="owner"/> is its physical holder (null on a shelf).</summary>
		internal static AllocCand MakeCand(Item it, int pref, int seq, AllocSlot slot, AllocLevy holder, object src)
			=> new AllocCand { It = it, Rank = RankOf(it), Cls = WeaponClassifier.ItemDamageClass(it), Type = it.type, Pref = pref, Seq = seq, Slot = slot, Phys = holder, Owner = holder, Src = src };

		/// <summary>Decides every levy's <see cref="AllocLevy.Target"/>. <paramref name="cands"/> may be in any order and is sorted in place.</summary>
		internal static void Plan(List<AllocLevy> levies, List<AllocCand> cands)
		{
			cands.Sort((a, b) => { int c = b.Rank.CompareTo(a.Rank); if (c != 0) return c; c = a.Pref.CompareTo(b.Pref); return c != 0 ? c : a.Seq.CompareTo(b.Seq); });
			foreach (var l in levies) { l.Target = null; l.Own = null; }
			foreach (var c in cands) { c.Used = false; c.Owner = c.Phys; if (c.Phys != null && c.Slot == AllocSlot.Primary) c.Phys.Own = c; }

			// a) class-matched: each class's levies are dealt that class's stock.
			var order = new List<DamageClass>();
			foreach (var l in levies) if (l.Cls != null && !order.Contains(l.Cls)) order.Add(l.Cls);
			foreach (var cls in order) {
				var group = levies.FindAll(l => l.Cls == cls);
				Serve(group, cands, (l, c) => c.Cls == l.Cls);
			}
			// b) everyone still without a weapon: classed levies whose class ran out first, then the unclassed, from what remains.
			Serve(levies.FindAll(l => l.Cls != null && l.Target == null), cands, (l, c) => true);
			var unclassed = levies.FindAll(l => l.Cls == null);
			Serve(unclassed, cands, (l, c) => l.Flex == null || l.Flex.Count == 0 || (c.Cls != null && l.Flex.Contains(c.Cls)));
			Serve(unclassed.FindAll(l => l.Target == null), cands, (l, c) => c.Cls == null);
			Serve(unclassed.FindAll(l => l.Target == null), cands, (l, c) => true);
		}

		/// <summary>Serves a group in two passes: levies whose weapon cannot be improved keep it (strongest first), then the rest are dealt picks worst-equipped first.</summary>
		private static void Serve(List<AllocLevy> group, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept)
		{
			var open = group.FindAll(l => l.Target == null);
			if (open.Count == 0) return;
			// Pass 1 - sticky: a levy with a usable weapon and nothing clearly better on offer keeps it. Strongest holders settle first so their weapons are not in play for anyone else.
			var holders = open.FindAll(l => Usable(l, accept));
			holders.Sort((a, b) => { int c = b.Own.Rank.CompareTo(a.Own.Rank); return c != 0 ? c : a.Id.CompareTo(b.Id); });
			foreach (var l in holders)
				if (Pick(l, cands, accept) == null) Keep(l);
			// Pass 2 - worst-equipped first (bare-handed ahead of everyone), by the stable id.
			var rest = open.FindAll(l => l.Target == null);
			rest.Sort((a, b) => { float ra = Usable(a, accept) ? a.Own.Rank : -1f, rb = Usable(b, accept) ? b.Own.Rank : -1f; int c = ra.CompareTo(rb); return c != 0 ? c : a.Id.CompareTo(b.Id); });
			foreach (var l in rest) {
				var pick = Pick(l, cands, accept);
				if (pick != null) Take(l, pick);
				else if (Usable(l, accept)) Keep(l);
			}
		}

		/// <summary>His own weapon counts as a basis for comparison while it is still his; a classed levy's must also be of the class this stage deals (a mismatched one is corrected, whatever it scores), an unclassed levy's always counts.</summary>
		private static bool Usable(AllocLevy l, Func<AllocLevy, AllocCand, bool> accept)
			=> l.Own != null && !l.Own.Used && l.Own.Owner == l && (l.Cls == null || accept(l, l.Own)); // what he already holds stays his whatever its ammo or the rules say; those only gate what he is dealt

		private static void Keep(AllocLevy l) { l.Target = l.Own; l.Own.Used = true; }

		private static void Take(AllocLevy l, AllocCand c)
		{
			l.Target = c; c.Used = true;
			if (l.Own != null && l.Own != c) l.Own.Owner = null; // his old weapon is released to the pool
		}

		/// <summary>The varied pick for a levy: the best copy of each weapon type within the variety band of the best candidate he can take, chosen by his id. Null when nothing qualifies (or nothing beats what he has by the upgrade margin).</summary>
		private static AllocCand Pick(AllocLevy l, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept)
		{
			var pick = Pick(l, cands, accept, l.Fires != null);
			return pick ?? (l.AllowDry && l.Fires != null ? Pick(l, cands, accept, false) : null);
		}

		private static AllocCand Pick(AllocLevy l, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept, bool requireFire)
		{
			float floor = 0f;
			bool usable = Usable(l, accept);
			if (usable) floor = l.Own.Rank * LevyConstants.AutoUpgradeScoreMargin;
			var band = new List<AllocCand>(LevyConstants.WeaponVarietyMaxTypes);
			float cutoff = float.NegativeInfinity;
			for (int i = 0; i < cands.Count; i++) {
				var c = cands[i];
				if (c.Rank < floor || c.Rank < cutoff) break;
				if (!Avail(l, c) || !accept(l, c)) continue;
				if (l.Ok != null && !l.Ok(c.It)) continue;
				if (requireFire && !l.Fires(c.It)) continue;
				if (l.ClassLock != null && c.Cls != l.ClassLock) continue;
				if (ReferenceEquals(c, l.Own)) continue;
				if (c.Slot == AllocSlot.Sidearm && c.Phys == l) continue;
				if (band.Count == 0) cutoff = Math.Max(floor, c.Rank * LevyConstants.WeaponVarietyTopFraction);
				bool seen = false;
				for (int k = 0; k < band.Count; k++) if (band[k].Type == c.Type) { seen = true; break; }
				if (seen) continue;
				band.Add(c);
				if (band.Count >= LevyConstants.WeaponVarietyMaxTypes) break;
			}
			if (band.Count == 0) return null;
			return band[(int)((uint)l.Id % (uint)band.Count)];
		}
	}
}
