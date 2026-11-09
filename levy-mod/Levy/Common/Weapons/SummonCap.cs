using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// HOW MANY MINIONS A SUMMON WEAPON CAN EVER HAVE OUT. Most staves stack until the owner's minion slots run out, but a good number of modded ones keep their own limit: Calamity's Deathstare Rod refuses
	/// to be used while its minion exists (<c>CanUseItem => ownedProjectileCounts[shoot] &lt;= 0</c>) and replaces it on every cast, so it can never field more than one whatever the owner's slots are.
	/// A levy has no use for the item's code (it casts the minion directly), so the limit is read from the item's real behaviour instead of a name list:
	///  * the item's own <c>CanUseItem</c> is asked with a scratch player that owns 0, 1, 2... of its minion (and plenty of free minion slots); the first count it refuses is the cap;
	///  * a cast that first kills the minion it made before (Calamity's <c>KillShootProjectiles</c>, found in the body of <c>Shoot</c>) is a replace-on-cast weapon: a cap of one.
	/// A capped staff uses only its real slot cost, so a summoner can carry several of them and keep every minion out; an uncapped staff takes the rest of the slots. Cached per item type.
	/// </summary>
	public static class SummonCap
	{
		/// <summary>The most casts the probe tries before deciding a staff is not capped.</summary>
		private const int ProbeMax = 12;
		/// <summary>Cap value meaning "no limit of its own".</summary>
		public const int None = int.MaxValue;

		private static readonly Dictionary<int, int> Cache = new();
		private static Player _probe;

		/// <summary>The most minions of this staff that can exist at once: its own limit, or <see cref="None"/>.</summary>
		public static int MaxCasts(Item it)
		{
			if (it == null || it.IsAir || it.shoot <= 0) return None;
			if (Cache.TryGetValue(it.type, out int hit)) return hit;
			int cap = None;
			try { cap = Read(it); } catch (Exception) { cap = None; }
			Cache[it.type] = cap;
			return cap;
		}

		/// <summary>True when the staff can only ever have a limited number of minions out (<see cref="MaxCasts"/> below <see cref="None"/>).</summary>
		public static bool IsCapped(Item it) => MaxCasts(it) < None;

		/// <summary>Drops the cache (world change / tests).</summary>
		internal static void Reset() { Cache.Clear(); _probe = null; }

		/// <summary>Test seam: pins an item type's cap (None for unlimited).</summary>
		internal static void Force(int itemType, int cap) => Cache[itemType] = cap;

		/// <summary>
		/// How many casts of each staff a levy keeps out. A lone staff gets what the levy's minion capacity gives it (<paramref name="unlimitedFit"/>), never more than its own limit. With several, a staff with a limit
		/// of its own takes only the slots its minions really use (<paramref name="slotsPer"/> each, so a summoner carrying three one-minion staves has all three out), and the first unlimited staff takes the rest of
		/// <paramref name="budget"/> slots; a second unlimited staff gets none. The first staff always gets at least one cast.
		/// </summary>
		public static int[] Allot(IList<Item> weapons, float budget, Func<Item, float> slotsPer, Func<Item, int> unlimitedFit)
		{
			var n = new int[weapons.Count];
			if (n.Length == 0) return n;
			if (n.Length == 1) { n[0] = Math.Min(unlimitedFit(weapons[0]), MaxCasts(weapons[0])); return n; }
			float remaining = Math.Max(1f, budget);
			bool unlimitedTaken = false, first = true;
			for (int pass = 0; pass < 2; pass++)
				for (int i = 0; i < weapons.Count; i++)
				{
					var w = weapons[i];
					int cap = MaxCasts(w);
					if ((pass == 0) != (cap < None)) continue; // limited staves first, then the unlimited ones
					float per = Math.Max(0.01f, slotsPer(w));
					int fit = (int)Math.Floor(remaining / per + LevyConstants.MinionSlotEpsilon);
					int want = cap < None ? Math.Min(cap, fit) : (unlimitedTaken ? 0 : Math.Min(fit, unlimitedFit(w)));
					if (cap >= None) unlimitedTaken = true;
					if (want < 1 && first) want = 1;
					n[i] = Math.Max(0, want);
					remaining -= n[i] * per;
					first = false;
				}
			return n;
		}

		private static int Read(Item it)
		{
			if (it.ModItem == null) return None; // a vanilla staff stacks up to the owner's slots
			var sample = new Item();
			sample.SetDefaults(it.type);
			var mi = sample.ModItem;
			if (mi == null) return None;
			int shoot = sample.shoot;
			if (shoot <= 0 || shoot >= ProjectileLoader.ProjectileCount) return None;
			int cap = None;

			// 1. the item refuses to be used once enough of its minion exist
			var p = _probe ??= new Player();
			p.ResetEffects();
			p.maxMinions = 100;
			p.slotsMinions = 0f;
			p.statMana = p.statManaMax2 = 1000;
			for (int c = 0; c <= ProbeMax; c++)
			{
				Array.Clear(p.ownedProjectileCounts, 0, p.ownedProjectileCounts.Length);
				p.ownedProjectileCounts[shoot] = c;
				bool usable;
				try { usable = mi.CanUseItem(p); } catch (Exception) { return None; }
				if (c == 0 && !usable) { cap = None; break; } // unusable even with none out: its gate is something else (a boss, a biome), not a minion count
				if (!usable) { cap = c; break; }
			}
			Array.Clear(p.ownedProjectileCounts, 0, p.ownedProjectileCounts.Length);

			// 2. the cast kills the minion it made before and makes a fresh one: one at a time
			if (cap == None && KillsPreviousCast(mi)) cap = 1;
			return cap;
		}

		private static readonly string[] ReplaceCalls = { "KillShootProjectiles" };

		/// <summary>True when the body of the item's Shoot calls a replace-the-old-minion helper (Calamity's KillShootProjectiles). Reads the IL; any failure reads as "no".</summary>
		private static bool KillsPreviousCast(ModItem mi)
		{
			try
			{
				MethodInfo m = mi.GetType().GetMethod("Shoot", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
				var body = m?.GetMethodBody();
				var il = body?.GetILAsByteArray();
				if (il == null) return false;
				var module = m.Module;
				for (int i = 0; i + 4 < il.Length; i++)
				{
					if (il[i] != 0x28 && il[i] != 0x6F) continue; // call, callvirt
					int token = BitConverter.ToInt32(il, i + 1);
					int table = (int)((uint)token >> 24);
					if (table != 0x06 && table != 0x0A && table != 0x2B) continue; // MethodDef, MemberRef, MethodSpec
					MethodBase target;
					try { target = module.ResolveMethod(token, mi.GetType().GenericTypeArguments, null); } catch (Exception) { continue; }
					if (target == null) continue;
					foreach (var name in ReplaceCalls) if (target.Name == name) return true;
				}
			}
			catch (Exception) { }
			return false;
		}
	}
}
