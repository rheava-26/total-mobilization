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
		/// <summary>Weapons that replace his current one whatever they score (a whip in hand yields to any minion staff). Null = none.</summary>
		public Func<AllocCand, bool> Yields;
		/// <summary>Fixed-class companies: while he holds a classed weapon he only ever trades within that class.</summary>
		public DamageClass ClassLock;
		/// <summary>True when <see cref="Cls"/> was assigned by the army's class balance rather than worn (the levy has no armor class of his own).</summary>
		public bool Virtual;
		/// <summary>True once the class balance has counted this free levy against the class of the weapon he is to have.</summary>
		public bool Counted;
		/// <summary>His maximum mana after gear (crystals plus what his armor and accessories add). Pulls a class-less levy toward magic; 0 = unknown.</summary>
		public float Mana;
		/// <summary>True when the class spread gave this class-less levy his class: his fallback weapons are melee / ranged / magic / thrown before anything else (never summon or rogue first).</summary>
		public bool Spread;

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
		/// <summary>The weapon's category ids under the armory's rules (never null).</summary>
		public string[] Cats = Array.Empty<string>();
		public int Type;
		/// <summary>A thrown consumable stack (Molotov, knives, grenades): carried and used up as a stack.</summary>
		public bool Thrown;
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
	///  r) RULES FIRST. The armory's hard rules (<see cref="ArmoryRules"/>) run in block order before anything else: a minimum or include drafts levies onto a category, the class balance spreads the free levies over the
	///     classes, and a hard order (bows over guns) filters every pick. Where two rules disagree the EARLIER block wins. Soft weights and hand-set ranks are already in the rank.
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
		internal static Func<Item, float> RankOf = WeaponRank.Of;

		/// <summary>How much claim a levy has on a weapon: 2 = it is his armor's class, 1 = he has no armor class, 0 = a classed levy holding another class's weapon.</summary>
		private static int Entitle(AllocLevy lev, AllocCand c) => lev.Cls != null && !lev.Spread ? (c.Cls == lev.Cls ? 2 : 0) : 1; // a levy the spread classed has no more claim than a class-less one
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
			=> new AllocCand { It = it, Rank = RankOf(it), Cls = WeaponClassifier.ItemDamageClass(it), Cats = ArmoryRules.CatsFor(it), Type = it.type, Thrown = WeaponClassifier.GetProfile(it).Family == WeaponFamily.Thrown, Pref = pref, Seq = seq, Slot = slot, Phys = holder, Owner = holder, Src = src };

		/// <summary>The hard rules of the plan being made (null = none). Set for the length of <see cref="Plan"/>.</summary>
		private static AllocRules _rules;
		/// <summary>The live head count per class while a class balance or a minimum is in force (null = none); a class at its maximum is closed to free levies.</summary>
		private static ClassBook _book;

		/// <summary>Decides every levy's <see cref="AllocLevy.Target"/>. <paramref name="cands"/> may be in any order and is sorted in place. <paramref name="rules"/> are the armory's hard rules (null = none).</summary>
		internal static void Plan(List<AllocLevy> levies, List<AllocCand> cands, AllocRules rules = null)
		{
			cands.Sort((a, b) => { int c = b.Rank.CompareTo(a.Rank); if (c != 0) return c; c = a.Pref.CompareTo(b.Pref); return c != 0 ? c : a.Seq.CompareTo(b.Seq); });
			foreach (var l in levies) { l.Target = null; l.Own = null; }
			foreach (var c in cands) { c.Used = false; c.Owner = c.Phys; if (c.Phys != null && c.Slot == AllocSlot.Primary) c.Phys.Own = c; }
			_rules = rules != null && rules.Active ? rules : null;
			try {
				if (_rules != null) RuleStage(levies, cands, _rules);

				// r2) class-less levies are spread over melee, ranged, magic and thrown by what is stocked (the rules above have already placed whoever they were to).
				var thrownLevies = LevyConstants.ClasslessSpread ? SpreadClassless(levies, cands) : null;
				if (thrownLevies != null && thrownLevies.Count > 0) Serve(thrownLevies, cands, (l, c) => c.Thrown);

				// a) class-matched: each class's levies are dealt that class's stock.
				var order = new List<DamageClass>();
				foreach (var l in levies) if (l.Cls != null && !order.Contains(l.Cls)) order.Add(l.Cls);
				foreach (var cls in order) {
					var group = levies.FindAll(l => l.Cls == cls);
					Serve(group, cands, (l, c) => c.Cls == l.Cls);
				}
				// b) everyone still without a weapon: classed levies whose class ran out first, then the unclassed, from what remains.
				Serve(levies.FindAll(l => l.Spread && l.Target == null), cands, (l, c) => IsSpreadClass(c.Cls) || c.Thrown); // a spread levy falls back to the martial classes before summon / rogue
				Serve(levies.FindAll(l => l.Cls != null && l.Target == null), cands, (l, c) => true);
				var unclassed = levies.FindAll(l => l.Cls == null);
				Serve(unclassed, cands, (l, c) => l.Flex == null || l.Flex.Count == 0 || (c.Cls != null && l.Flex.Contains(c.Cls)));
				Serve(unclassed.FindAll(l => l.Target == null), cands, (l, c) => c.Cls == null);
				Serve(unclassed.FindAll(l => l.Target == null), cands, (l, c) => true);
				// c) a class maximum gives way before a levy is left bare-handed: whoever still has nothing takes what is left, whatever its class.
				if (_book != null) {
					_book = null;
					Serve(levies.FindAll(l => l.Target == null), cands, (l, c) => true);
				}
			}
			finally { _rules = null; _book = null; }
		}

		// ------------------------------------------------------------------ class-less spread

		private static bool IsSpreadClass(DamageClass c) => c == DamageClass.Melee || c == DamageClass.Ranged || c == DamageClass.Magic;

		/// <summary>The weight of the magic bucket for a levy with this much maximum mana: 1 below <see cref="LevyConstants.MagicManaBiasFloor"/>, rising linearly to 1 + <see cref="LevyConstants.MagicManaBiasGain"/> at <see cref="LevyConstants.MagicManaBiasFull"/>.</summary>
		internal static float MagicManaBias(float mana)
		{
			float span = Math.Max(1f, LevyConstants.MagicManaBiasFull - LevyConstants.MagicManaBiasFloor);
			return 1f + LevyConstants.MagicManaBiasGain * Math.Clamp((mana - LevyConstants.MagicManaBiasFloor) / span, 0f, 1f);
		}

		/// <summary>
		/// Spreads the levies that have no class (no worn armor class, no class from the rules) over melee, ranged, magic and thrown, weighted by how many good weapons each has in stock
		/// (a weapon counts when it ranks within <see cref="LevyConstants.ClasslessQualityFraction"/> of the best of all four). Summon and rogue are never chosen. A levy already holding a weapon keeps his class;
		/// the bare ones are dealt a bucket by highest-averages (D'Hondt) over the stock weights, the highest-mana levies first, with the magic weight multiplied by <see cref="MagicManaBias"/> for each. Returns the levies given the thrown bucket.
		/// </summary>
		private static List<AllocLevy> SpreadClassless(List<AllocLevy> levies, List<AllocCand> cands)
		{
			var free = levies.FindAll(l => l.Cls == null && l.ClassLock == null && l.Target == null);
			if (free.Count == 0) return null;
			var rep = free[0];
			var freeSet = new HashSet<AllocLevy>(free);
			var ranks = new List<float>[4]; for (int i = 0; i < 4; i++) ranks[i] = new List<float>();
			int Bucket(AllocCand c) => c.Thrown ? (c.Cls == DamageClass.Ranged || c.Cls == DamageClass.Melee ? 3 : -1) : c.Cls == DamageClass.Melee ? 0 : c.Cls == DamageClass.Ranged ? 1 : c.Cls == DamageClass.Magic ? 2 : -1;
			float top = 0f;
			var bucketTop = new float[4];
			foreach (var c in cands) {
				if (c.Used || c.Rank <= 0f || c.Slot == AllocSlot.Sidearm) continue;
				if (c.Slot == AllocSlot.Primary && (c.Phys == null || !freeSet.Contains(c.Phys))) continue; // a classed levy's weapon is not stock
				int b = Bucket(c); if (b < 0) continue;
				if (rep.Ok != null && !rep.Ok(c.It)) continue;
				if (rep.Fires != null && !rep.Fires(c.It)) continue;
				ranks[b].Add(c.Rank); if (c.Rank > top) top = c.Rank; if (c.Rank > bucketTop[b]) bucketTop[b] = c.Rank;
			}
			if (top <= 0f) return null;
			var supply = new int[4];
			for (int b = 0; b < 4; b++) {
				if (bucketTop[b] < top * LevyConstants.ClasslessBucketMinFraction) continue; // nothing in this class is worth arming a levy with next to what the others have
				foreach (float r in ranks[b]) if (r >= bucketTop[b] * LevyConstants.ClasslessQualityFraction) supply[b]++;
			}
			var weight = new float[4]; for (int b = 0; b < 3; b++) weight[b] = supply[b];
			var stackTypes = new HashSet<int>(); // a thrown stack is offered as several copies, but it is one kind of stock: it weighs once per type
			foreach (var c in cands) if (c.Thrown && !c.Used && c.Rank >= bucketTop[3] * LevyConstants.ClasslessQualityFraction && bucketTop[3] >= top * LevyConstants.ClasslessBucketMinFraction && Bucket(c) == 3) stackTypes.Add(c.Type);
			weight[3] = stackTypes.Count;
			Trace?.Invoke($"spread free={free.Count} top={top:F0} bucketTop=[{string.Join(",", Array.ConvertAll(bucketTop, x => x.ToString("F0")))}] supply=[{string.Join(",", supply)}]");
			var cls = new DamageClass[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, null };
			var given = new int[4]; var held = new int[4];
			var thrown = new List<AllocLevy>();
			// levies already holding a weapon of a bucket stay in it (counted first, so the bare ones fill the gaps)
			foreach (var l in free.FindAll(x => x.Own != null)) {
				int b = Bucket(l.Own); if (b < 0 || b == 3) continue;
				given[b]++; held[b]++; // he keeps his weapon and stays class-less (no claim of his own); the bare ones fill the other buckets
			}
			var bare = free.FindAll(l => l.Cls == null);
			bare.Sort((a, c) => { int k = c.Mana.CompareTo(a.Mana); return k != 0 ? k : a.Id.CompareTo(c.Id); });
			foreach (var l in bare) {
				int pick = -1; float best = 0f;
				for (int b = 0; b < 4; b++) {
					if (supply[b] <= 0 || given[b] >= supply[b]) continue;
					var bc = b == 3 ? DamageClass.Ranged : cls[b];
					if (_book != null && _book.Hi.TryGetValue(bc, out var hi) && _book.Of(bc) + held[b] >= hi) continue; // the class maximum of a rule
					if (l.Flex != null && l.Flex.Count > 0 && !l.Flex.Contains(b == 3 ? DamageClass.Ranged : cls[b])) continue; // he can only field armor for some classes
					float score = weight[b] * (b == 2 ? MagicManaBias(l.Mana) : 1f) / (given[b] + 1);
					if (score > best) { best = score; pick = b; }
				}
				if (pick < 0) continue;
				given[pick]++;
				if (pick == 3) { l.Spread = true; thrown.Add(l); }
				else Assign(l, cls[pick]);
			}
			return thrown;
		}

		private static void Assign(AllocLevy l, DamageClass c)
		{
			l.Cls = c; l.Virtual = true; l.Spread = true;
			_book?.Add(c, 1);
		}

		// ------------------------------------------------------------------ the rule stage: minimums, includes and the class balance, in block order (the earlier block wins)

		/// <summary>Per-class head count of the levies the plan has settled on a class (worn, locked or assigned), and the most the class balance allows.</summary>
		private sealed class ClassBook
		{
			public readonly Dictionary<DamageClass, int> Count = new();
			public readonly Dictionary<DamageClass, int> Hi = new();
			public int Of(DamageClass c) => c != null && Count.TryGetValue(c, out var n) ? n : 0;
			public void Add(DamageClass c, int d) { if (c != null) Count[c] = Of(c) + d; }
		}

		private static void RuleStage(List<AllocLevy> levies, List<AllocCand> cands, AllocRules rules)
		{
			var book = _book = new ClassBook();
			foreach (var l in levies) book.Add(l.Cls ?? l.ClassLock, 1);
			var steps = new List<(int block, MinRule min, ClassPlanRule cls)>();
			foreach (var m in rules.Mins) steps.Add((m.Block, m, null));
			if (rules.Class != null) steps.Add((rules.Class.Block, null, rules.Class));
			steps.Sort((a, b) => a.block.CompareTo(b.block));
			foreach (var st in steps) {
				if (st.min != null) ClaimMin(levies, cands, st.min, book, rules.Class != null && rules.Class.Block < st.min.Block);
				else AssignClasses(levies, cands, st.cls, book);
			}
		}

		/// <summary>Gives the levy <paramref name="c"/>'s class when he has none of his own (a free levy), keeping the head counts right.</summary>
		private static void Reclass(AllocLevy l, DamageClass c, ClassBook book)
		{
			if (c == null || l.Cls == c) return;
			if (l.Cls != null && !l.Virtual) return;
			if (l.Cls != null) book.Add(l.Cls, -1);
			l.Cls = c; l.Virtual = true; book.Add(c, 1);
		}

		/// <summary>True when the levy may be dealt <paramref name="c"/> by a minimum rule: his armor class matches, or he is free and the class has room under a class maximum that came earlier.</summary>
		private static bool MinCanTake(AllocLevy l, AllocCand c, ClassBook book, bool classFirst)
		{
			if (l.ClassLock != null && c.Cls != l.ClassLock) return false;
			if (l.Cls != null && !l.Virtual) return c.Cls == l.Cls;
			if (c.Cls == null) return l.Cls == null;
			if (l.Cls == c.Cls) return true;
			if (classFirst && book.Hi.TryGetValue(c.Cls, out var hi) && book.Of(c.Cls) >= hi) return false;
			return true;
		}

		/// <summary>At least <see cref="MinRule.Count"/> levies carry a weapon of the category (every stocked one for an include). Holders keep theirs first; the rest are drafted, bare-handed and weakest first.</summary>
		private static void ClaimMin(List<AllocLevy> levies, List<AllocCand> cands, MinRule m, ClassBook book, bool classFirst)
		{
			int have = 0;
			foreach (var l in levies) if (l.Target != null && ArmoryRules.Has(l.Target.Cats, m.Cat)) have++;
			var holders = levies.FindAll(l => l.Target == null && l.Own != null && !l.Own.Used && l.Own.Owner == l && ArmoryRules.Has(l.Own.Cats, m.Cat) && MinCanTake(l, l.Own, book, classFirst));
			holders.Sort((a, b) => { int c = b.Own.Rank.CompareTo(a.Own.Rank); return c != 0 ? c : a.Id.CompareTo(b.Id); });
			foreach (var l in holders) {
				if (have >= m.Count) break;
				Reclass(l, l.Own.Cls, book); Keep(l); have++;
			}
			if (have >= m.Count) return;
			var open = levies.FindAll(l => l.Target == null);
			open.Sort((a, b) => { float ra = a.Own == null ? -1f : a.Own.Rank, rb = b.Own == null ? -1f : b.Own.Rank; int c = ra.CompareTo(rb); return c != 0 ? c : a.Id.CompareTo(b.Id); });
			foreach (var l in open) {
				if (have >= m.Count) break;
				var pick = Pick(l, cands, (lv, c) => MinCanTake(lv, c, book, classFirst), true, m.Block, c => ArmoryRules.Has(c.Cats, m.Cat));
				if (pick == null) continue;
				Take(l, pick); Reclass(l, pick.Cls, book); have++;
			}
		}

		/// <summary>Spreads the free levies (no armor class) over the classes: minimums and fixed counts first, then the weights, then whatever is left is held under the maximums.</summary>
		private static void AssignClasses(List<AllocLevy> levies, List<AllocCand> cands, ClassPlanRule plan, ClassBook book)
		{
			int total = levies.Count;
			var free = levies.FindAll(l => l.Cls == null && l.ClassLock == null && l.Target == null);
			var entries = new List<(DamageClass cls, int lo, int hi, float w)>();
			foreach (var e in plan.Entries) {
				var cls = WeaponTags.ClassFromKey(e.Class);
				if (cls == null) continue;
				int lo = e.Fixed >= 0 ? e.Fixed : Math.Max(0, e.Min);
				int hi = e.Fixed >= 0 ? e.Fixed : e.Max >= 0 ? e.Max : total;
				hi = Math.Max(lo, hi);
				entries.Add((cls, lo, hi, e.Weight));
				book.Hi[cls] = hi;
			}
			// weapons each class could still be dealt: the shelf stock, the blades carried as sidearms, and what free levies hold (they give it up when they are re-classed).
			var supply = new Dictionary<DamageClass, int>();
			var freeSet = new HashSet<AllocLevy>(free);
			foreach (var c in cands) {
				if (c.Used || c.Cls == null) continue;
				if (c.Slot == AllocSlot.Shelf || c.Slot == AllocSlot.Sidearm || (c.Slot == AllocSlot.Primary && c.Phys != null && freeSet.Contains(c.Phys))) supply[c.Cls] = (supply.TryGetValue(c.Cls, out var n) ? n : 0) + 1;
			}
			var given = new Dictionary<DamageClass, int>();
			int Left(DamageClass c) => (supply.TryGetValue(c, out var n) ? n : 0) - (given.TryGetValue(c, out var g) ? g : 0);
			void Give(DamageClass cls, int k)
			{
				// holders of that class first, then the bare, then the weakest weapon, then the id
				free.Sort((a, b) => {
					int ka = a.Own != null && a.Own.Cls == cls ? 0 : a.Own == null ? 1 : 2, kb = b.Own != null && b.Own.Cls == cls ? 0 : b.Own == null ? 1 : 2;
					int c = ka.CompareTo(kb); if (c != 0) return c;
					float ra = a.Own == null ? 0f : a.Own.Rank, rb = b.Own == null ? 0f : b.Own.Rank;
					c = ra.CompareTo(rb); return c != 0 ? c : a.Id.CompareTo(b.Id);
				});
				for (int i = 0; i < k && free.Count > 0; i++) {
					var l = free[0]; free.RemoveAt(0);
					l.Cls = cls; l.Virtual = true; book.Add(cls, 1);
					given[cls] = (given.TryGetValue(cls, out var g) ? g : 0) + 1;
				}
			}
			// 1. floors (minimums and fixed counts), in the order the classes are listed: the earlier entry is served first.
			foreach (var (cls, lo, hi, w) in entries) {
				int need = lo - book.Of(cls);
				if (need > 0 && Left(cls) > 0) Give(cls, Math.Min(Math.Min(need, Left(cls)), free.Count));
			}
			// 2. weights: the whole army is spread by the shares; the largest shortfall is served first.
			float sum = 0f;
			foreach (var e in entries) if (e.w > 0f) sum += e.w;
			if (sum > 0f && free.Count > 0) {
				var want = new List<(DamageClass cls, float deficit)>();
				foreach (var (cls, lo, hi, w) in entries) if (w > 0f) want.Add((cls, total * w / sum - book.Of(cls)));
				for (int guard = 0; guard < total && free.Count > 0; guard++) {
					want.Sort((a, b) => b.deficit.CompareTo(a.deficit));
					int pick = -1;
					for (int i = 0; i < want.Count; i++) {
						var cls = want[i].cls;
						if (want[i].deficit > 0f && book.Of(cls) < book.Hi[cls] && Left(cls) > 0) { pick = i; break; }
					}
					if (pick < 0) break;
					Give(want[pick].cls, 1);
					want[pick] = (want[pick].cls, want[pick].deficit - 1f);
				}
			}
		}

		// ------------------------------------------------------------------ the standard stages

		/// <summary>Serves a group in two passes: levies whose weapon cannot be improved keep it (strongest first), then the rest are dealt picks worst-equipped first.</summary>
		private static void Serve(List<AllocLevy> group, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept)
		{
			var open = group.FindAll(l => l.Target == null);
			if (open.Count == 0) return;
			// Pass 1 - sticky: a levy with a usable weapon and nothing clearly better on offer keeps it. Strongest holders settle first so their weapons are not in play for anyone else.
			var holders = open.FindAll(l => Usable(l, accept));
			holders.Sort((a, b) => { int c = b.Own.Rank.CompareTo(a.Own.Rank); return c != 0 ? c : a.Id.CompareTo(b.Id); });
			foreach (var l in holders)
				if (Pick(l, cands, accept) == null && !CapBlocks(l, l.Own)) Keep(l);
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

		private static void Keep(AllocLevy l) { l.Target = l.Own; l.Own.Used = true; Commit(l, l.Own); }

		private static void Take(AllocLevy l, AllocCand c)
		{
			l.Target = c; c.Used = true;
			if (l.Own != null && l.Own != c) l.Own.Owner = null; // his old weapon is released to the pool
			Commit(l, c);
		}

		/// <summary>A free levy (no class of his own) is counted against the class of the weapon he is to have, so a class maximum holds for the levies served after him.</summary>
		private static void Commit(AllocLevy l, AllocCand c)
		{
			if (_book == null || l.Cls != null || l.Counted || c.Cls == null) return;
			_book.Add(c.Cls, 1); l.Counted = true;
		}

		/// <summary>True when a class maximum closes <paramref name="c"/>'s class to this levy: he has no class of his own (or was assigned one) and the class is full.</summary>
		private static bool CapBlocks(AllocLevy l, AllocCand c)
			=> _book != null && c != null && c.Cls != null && (l.Cls == null || l.Virtual) && l.Cls != c.Cls && _book.Hi.TryGetValue(c.Cls, out var hi) && _book.Of(c.Cls) >= hi;

		/// <summary>The varied pick for a levy: the best copy of each weapon type within the variety band of the best candidate he can take, chosen by his id. Null when nothing qualifies (or nothing beats what he has by the upgrade margin).
		/// <paramref name="ignoreFloor"/> drops the upgrade margin (a minimum rule drafting him); <paramref name="limit"/> is the last block whose hard order still applies.</summary>
		private static AllocCand Pick(AllocLevy l, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept, bool ignoreFloor = false, int limit = int.MaxValue, Func<AllocCand, bool> require = null)
		{
			var pick = Pick(l, cands, accept, l.Fires != null, ignoreFloor, limit, require);
			return pick ?? (l.AllowDry && l.Fires != null ? Pick(l, cands, accept, false, ignoreFloor, limit, require) : null);
		}

		private static AllocCand Pick(AllocLevy l, List<AllocCand> cands, Func<AllocLevy, AllocCand, bool> accept, bool requireFire, bool ignoreFloor, int limit, Func<AllocCand, bool> require)
		{
			var rules = _rules;
			bool ordered = rules != null && rules.HasOrders; // hard orders need every candidate in view, so the scan cannot stop at the edge of the band
			float floor = 0f;
			bool usable = Usable(l, accept);
			if (usable && !ignoreFloor) floor = l.Own.Rank * LevyConstants.AutoUpgradeScoreMargin;
			var own = usable ? l.Own : null;
			var band = new List<AllocCand>(LevyConstants.WeaponVarietyMaxTypes);
			List<(AllocCand c, bool yields)> seen = ordered ? new List<(AllocCand, bool)>() : null;
			HashSet<int> types = ordered ? new HashSet<int>() : null;
			float cutoff = float.NegativeInfinity;
			for (int i = 0; i < cands.Count; i++) {
				var c = cands[i];
				bool yields = (l.Yields != null && usable && l.Yields(c)) || (ordered && own != null && rules.Beats(c.Cats, own.Cats, limit));
				if (!ordered && (c.Rank < cutoff || (c.Rank < floor && l.Yields == null))) break;
				if (c.Rank < floor && !yields) continue;
				if (!Avail(l, c) || !accept(l, c)) continue;
				if (l.Ok != null && !l.Ok(c.It)) continue;
				if (requireFire && !l.Fires(c.It)) continue;
				if (l.ClassLock != null && c.Cls != l.ClassLock) continue;
				if (CapBlocks(l, c)) continue;
				if (c.Rank <= 0f || ReferenceEquals(c, l.Own)) continue; // rank 0 = barred by the armory's rules
				if (c.Slot == AllocSlot.Sidearm && c.Phys == l) continue;
				if (ordered) { if (types.Add(c.Type)) seen.Add((c, yields)); continue; }
				if (require != null && !require(c)) continue;
				if (band.Count == 0) cutoff = Math.Max(yields ? 0f : floor, c.Rank * LevyConstants.WeaponVarietyTopFraction);
				bool dup = false;
				for (int k = 0; k < band.Count; k++) if (band[k].Type == c.Type) { dup = true; break; }
				if (dup) continue;
				band.Add(c);
				if (band.Count >= LevyConstants.WeaponVarietyMaxTypes) break;
			}
			if (ordered) {
				// Hard orders, earliest block first: where an "over" rule's first category is on offer (or already in his hand), the second category is not.
				foreach (var o in rules.Orders) {
					if (o.Block > limit) continue;
					bool anyA = own != null && ArmoryRules.Has(own.Cats, o.A);
					if (!anyA) foreach (var (c, _) in seen) if (ArmoryRules.Has(c.Cats, o.A)) { anyA = true; break; }
					if (anyA) seen.RemoveAll(x => ArmoryRules.Has(x.c.Cats, o.B) && !ArmoryRules.Has(x.c.Cats, o.A));
				}
				if (require != null) seen.RemoveAll(x => !require(x.c)); // a minimum's own category is asked for only after the orders have had their say
				foreach (var (c, yields) in seen) {
					if (band.Count == 0) cutoff = Math.Max(yields ? 0f : floor, c.Rank * LevyConstants.WeaponVarietyTopFraction);
					else if (c.Rank < cutoff) break;
					band.Add(c);
					if (band.Count >= LevyConstants.WeaponVarietyMaxTypes) break;
				}
			}
			var chosen = band.Count == 0 ? null : band[(int)((uint)l.Id % (uint)band.Count)];
			Trace?.Invoke($"pick levy {l.Id} cls={(l.Cls == null ? "-" : l.Cls.Name)} fire={requireFire} floor={floor:F0} ordered={ordered} limit={limit} -> {(chosen == null ? "none" : chosen.It.Name + " (" + chosen.Rank.ToString("F0") + ")")} of band [{string.Join(", ", band.ConvertAll(x => x.It.Name))}]");
			return chosen;
		}

		/// <summary>Probe seam: when set, every pick reports what it chose (TEST-only).</summary>
		internal static Action<string> Trace;
	}
}
