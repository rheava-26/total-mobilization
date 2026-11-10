using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	public enum RuleKind : byte { ClassBalance = 0, Category = 1, ItemList = 2 }

	/// <summary>
	/// What a category rule does.
	/// Weight: a soft factor on the rank of every weapon of the category.
	/// Over:   a hard order, the first category is always chosen over the second when a levy can take either.
	/// Include: a hard include, every weapon of the category is put in service (the other weapons are still dealt by rank to the remaining levies).
	/// Min:    at least N levies carry a weapon of the category (as far as the stock goes).
	/// </summary>
	public enum CatMode : byte { Weight = 0, Over = 1, Include = 2, Min = 3 }

	/// <summary>Only: just the listed items are used. Never: the listed items are not used.</summary>
	public enum ListMode : byte { Only = 0, Never = 1 }

	/// <summary>How a hand-set rank is read: Auto = the computed rank, Set = a number, Never = the item is not used, Prefer = the item is used before any other.</summary>
	public enum RankOverrideMode : byte { Auto = 0, Set = 1, Never = 2, Prefer = 3 }

	/// <summary>One class's line in a class balance block. Weight 0 = no share set. Min / Max / Fixed are levy counts; -1 = off. Fixed means Min = Max = N.</summary>
	public sealed class ClassEntry
	{
		public string Class = "melee";
		public float Weight;
		public int Min;
		public int Max = -1;
		public int Fixed = -1;
		public bool IsDefault => Weight <= 0f && Min <= 0 && Max < 0 && Fixed < 0;
		public ClassEntry Clone() => (ClassEntry)MemberwiseClone();
	}

	/// <summary>One rule block. Which fields matter depends on <see cref="Kind"/>.</summary>
	public sealed class RuleBlock
	{
		public RuleKind Kind;
		public bool On = true;
		// class balance
		public List<ClassEntry> Classes = new();
		// category rule
		public CatMode Mode = CatMode.Weight;
		public string Cat = "bow", Other = "gun";
		public float Weight = 2f;
		public int Count = 1;
		// item list
		public ListMode List = ListMode.Never;
		public List<string> Items = new();

		public ClassEntry EntryFor(string cls, bool create)
		{
			foreach (var e in Classes) if (e.Class == cls) return e;
			if (!create) return null;
			var n = new ClassEntry { Class = cls };
			Classes.Add(n);
			return n;
		}

		public RuleBlock Clone()
		{
			var b = (RuleBlock)MemberwiseClone();
			b.Classes = Classes.ConvertAll(e => e.Clone());
			b.Items = new List<string>(Items);
			return b;
		}
	}

	/// <summary>Per item type, per armory: a hand-set rank and hand-edited tags.</summary>
	public sealed class ItemOverride
	{
		public RankOverrideMode Rank;
		public float Value;
		/// <summary>null = the item's own class; "none" = no class; otherwise a class id.</summary>
		public string Class;
		public HashSet<string> CatAdd = new(), CatRemove = new();
		public bool IsEmpty => Rank == RankOverrideMode.Auto && Class == null && CatAdd.Count == 0 && CatRemove.Count == 0;
		public ItemOverride Clone() => new ItemOverride { Rank = Rank, Value = Value, Class = Class, CatAdd = new HashSet<string>(CatAdd), CatRemove = new HashSet<string>(CatRemove) };
	}

	/// <summary>
	/// An armory's rule list, saved with its tile entity and synced to clients. Ordered blocks, each with numbers the player edits, plus per-item overrides:
	///  - CLASS BALANCE: how the army's levies spread over the classes (weights, minimums, maximums, a fixed count). With no numbers set the stock and the armor decide, as they always have.
	///  - CATEGORY: bows, guns, staves... Weight (soft), Over (hard order), Include (hard include), Min (at least N).
	///  - ITEM LIST: only these items / never these items.
	/// When blocks conflict the EARLIER block wins. The allocator reads the compiled form (<see cref="Alloc"/>); the ranking reads <see cref="RuleFactor"/> and the overrides.
	/// A rule set is made current for the length of an armory call with <see cref="Enter"/>, so the static ranking helpers need no armory in their signatures.
	/// </summary>
	public sealed class ArmoryRules
	{
		public const int MaxBlocks = 32, MaxItemsPerList = 64, MaxOverrides = 512, MaxWireBytes = 96 * 1024, FormatVersion = 1;
		/// <summary>Rank given to an "always prefer" item, on top of its own rank: above every real rank, so a preferred item is used first.</summary>
		public const float PreferBase = 1e7f;
		/// <summary>Score added to an armor piece or accessory the player marked "prefer": above every ordinary score term (including an upgrade chain), so it is dealt first.</summary>
		public const float GearPreferBonus = 4e6f;

		public List<RuleBlock> Blocks = new();
		public readonly Dictionary<string, ItemOverride> Overrides = new();
		/// <summary>Most sidearms a levy who is not a summoner carries (0 to <see cref="LevyConstants.MaxSidearms"/>). The top value is no limit.</summary>
		public int SidearmLimit = LevyConstants.MaxSidearms;
		/// <summary>Whether a levy who is not a summoner may carry sentry and minion staves as sidearms.</summary>
		public bool SummonsForOthers = true;
		/// <summary>True when the sidearm rules limit anything.</summary>
		public bool LimitsLoadouts => SidearmLimit < LevyConstants.MaxSidearms || !SummonsForOthers;
		/// <summary>Bumped on every edit; the UI and the compiled cache compare it.</summary>
		public int Rev;

		/// <summary>The rule set the current armory call runs under (null = none: the plain ranking).</summary>
		public static ArmoryRules Current;

		public readonly struct Scope : IDisposable
		{
			private readonly ArmoryRules _prev;
			public Scope(ArmoryRules prev) { _prev = prev; }
			public void Dispose() { Current = _prev; }
		}

		/// <summary>Makes <paramref name="r"/> the current rule set until the returned scope is disposed.</summary>
		public static Scope Enter(ArmoryRules r) { var s = new Scope(Current); Current = r; return s; }

		// ------------------------------------------------------------------ construction

		/// <summary>The rule list every armory starts with: one class balance block with nothing set, which behaves exactly as an armory with no rules.</summary>
		public static ArmoryRules Default()
		{
			var r = new ArmoryRules();
			r.Blocks.Add(new RuleBlock { Kind = RuleKind.ClassBalance });
			return r;
		}

		public ArmoryRules Clone()
		{
			var r = new ArmoryRules { Blocks = Blocks.ConvertAll(b => b.Clone()), Rev = Rev, SidearmLimit = SidearmLimit, SummonsForOthers = SummonsForOthers };
			foreach (var kv in Overrides) r.Overrides[kv.Key] = kv.Value.Clone();
			return r;
		}

		/// <summary>Replaces this rule set's contents with <paramref name="o"/>'s (same object, so the tile entity and the panel keep their reference).</summary>
		public void CopyFrom(ArmoryRules o)
		{
			Blocks = o.Blocks.ConvertAll(b => b.Clone());
			Overrides.Clear();
			foreach (var kv in o.Overrides) Overrides[kv.Key] = kv.Value.Clone();
			SidearmLimit = o.SidearmLimit; SummonsForOthers = o.SummonsForOthers;
			Rev = o.Rev;
			Changed(false);
		}

		/// <summary>Call after any edit: drops the compiled cache and (by default) bumps <see cref="Rev"/>.</summary>
		public void Changed(bool bump = true)
		{
			if (bump) Rev++;
			_compiled = null;
		}

		/// <summary>True when the list does nothing: no enabled block with any effect and no overrides.</summary>
		public bool IsNeutral
		{
			get
			{
				if (Overrides.Count > 0 || LimitsLoadouts) return false;
				foreach (var b in Blocks)
				{
					if (!b.On) continue;
					if (b.Kind == RuleKind.ClassBalance) { foreach (var e in b.Classes) if (!e.IsDefault) return false; }
					else if (b.Kind == RuleKind.Category) return false;
					else if (b.Items.Count > 0) return false;
				}
				return true;
			}
		}

		// ------------------------------------------------------------------ item keys (stable across mod load orders)

		private static readonly Dictionary<int, string> _keyOfType = new();
		private static readonly Dictionary<string, int> _typeOfKey = new();

		public static void ResetKeys() { _keyOfType.Clear(); _typeOfKey.Clear(); }

		/// <summary>"Terraria/CopperShortsword" for a vanilla item, "Mod/ItemName" for a modded one.</summary>
		public static string KeyOf(int type)
		{
			if (_keyOfType.TryGetValue(type, out var k)) return k;
			if (type > 0 && type < ItemID.Count) k = "Terraria/" + (ItemID.Search.TryGetName(type, out var n) ? n : type.ToString());
			else { var mi = ItemLoader.GetItem(type); k = mi != null ? mi.Mod.Name + "/" + mi.Name : null; }
			if (k != null) { _keyOfType[type] = k; _typeOfKey[k] = type; }
			return k;
		}

		/// <summary>The item type a key names, or 0 (an item of a mod that is not loaded).</summary>
		public static int TypeOf(string key)
		{
			if (string.IsNullOrEmpty(key)) return 0;
			if (_typeOfKey.TryGetValue(key, out var t)) return t;
			int slash = key.IndexOf('/');
			if (slash <= 0) return 0;
			string mod = key.Substring(0, slash), name = key.Substring(slash + 1);
			t = 0;
			if (mod == "Terraria") { if (ItemID.Search.TryGetId(name, out var id)) t = id; else if (int.TryParse(name, out var raw) && raw > 0 && raw < ItemID.Count) t = raw; }
			else if (ModContent.TryFind<ModItem>(mod + "/" + name, out var mi)) t = mi.Type;
			if (t > 0) { _typeOfKey[key] = t; _keyOfType[t] = key; }
			return t;
		}

		// ------------------------------------------------------------------ the per-item queries the ranking and the allocator use

		private Compiled _compiled;

		private sealed class Compiled
		{
			public readonly Dictionary<int, ItemOverride> ByType = new();
			public readonly HashSet<int> Never = new();
			public HashSet<int> Only;
			public readonly List<(string cat, float w)> Weights = new();
			public readonly Dictionary<int, string[]> Cats = new();
			public AllocRules Alloc;
		}

		private Compiled C => _compiled ??= Compile();

		private Compiled Compile()
		{
			var c = new Compiled();
			foreach (var kv in Overrides) { int t = TypeOf(kv.Key); if (t > 0) c.ByType[t] = kv.Value; }
			var alloc = new AllocRules();
			for (int i = 0; i < Blocks.Count; i++)
			{
				var b = Blocks[i];
				if (!b.On) continue;
				switch (b.Kind)
				{
					case RuleKind.ItemList:
						if (b.List == ListMode.Never) { foreach (var k in b.Items) { int t = TypeOf(k); if (t > 0) c.Never.Add(t); } }
						else if (b.Items.Count > 0) { c.Only ??= new HashSet<int>(); foreach (var k in b.Items) { int t = TypeOf(k); if (t > 0) c.Only.Add(t); } }
						break;
					case RuleKind.Category:
						if (string.IsNullOrEmpty(b.Cat)) break;
						if (b.Mode == CatMode.Weight) c.Weights.Add((b.Cat, Math.Max(0.01f, b.Weight)));
						else if (b.Mode == CatMode.Over) { if (!string.IsNullOrEmpty(b.Other) && b.Other != b.Cat) alloc.Orders.Add(new OrderRule { A = b.Cat, B = b.Other, Block = i }); }
						else if (b.Mode == CatMode.Include) alloc.Mins.Add(new MinRule { Cat = b.Cat, Count = int.MaxValue, Block = i });
						else alloc.Mins.Add(new MinRule { Cat = b.Cat, Count = Math.Max(0, b.Count), Block = i });
						break;
					case RuleKind.ClassBalance:
						if (alloc.Class == null)
						{
							bool any = false;
							foreach (var e in b.Classes) if (!e.IsDefault) { any = true; break; }
							if (any) alloc.Class = new ClassPlanRule { Block = i, Entries = b.Classes.ConvertAll(e => e.Clone()) };
						}
						break;
				}
			}
			c.Alloc = alloc;
			return c;
		}

		/// <summary>The compiled hard rules the allocator applies (never null).</summary>
		internal AllocRules Alloc => C.Alloc;

		/// <summary>The override stored for this item type, or null.</summary>
		public ItemOverride OverrideOf(int type) => C.ByType.TryGetValue(type, out var o) ? o : null;

		/// <summary>The override stored under this item's key (creating one when asked); used by the editor.</summary>
		public ItemOverride OverrideFor(int type, bool create)
		{
			string k = KeyOf(type);
			if (k == null) return null;
			if (Overrides.TryGetValue(k, out var o)) return o;
			if (!create || Overrides.Count >= MaxOverrides) return null;
			return Overrides[k] = new ItemOverride();
		}

		/// <summary>Drops empty overrides after an edit.</summary>
		public void Prune()
		{
			List<string> dead = null;
			foreach (var kv in Overrides) if (kv.Value.IsEmpty) (dead ??= new List<string>()).Add(kv.Key);
			if (dead != null) foreach (var k in dead) Overrides.Remove(k);
		}

		/// <summary>True when the armory must not issue this item: an "always never" override, a never list, or an only list that does not hold it.</summary>
		public bool Excluded(Item it)
		{
			var c = C;
			if (c.Never.Contains(it.type)) return true;
			if (c.Only != null && !c.Only.Contains(it.type)) return true;
			return c.ByType.TryGetValue(it.type, out var o) && o.Rank == RankOverrideMode.Never;
		}

		/// <summary>If the player replaced this item type's class: true, and the class (null = no class). Otherwise false.</summary>
		public bool TryClassEdit(Item it, out DamageClass dc)
		{
			dc = null;
			if (!C.ByType.TryGetValue(it.type, out var o) || o.Class == null) return false;
			if (o.Class == "none") return true;
			dc = WeaponTags.ClassFromKey(o.Class);
			return dc != null; // an unknown class id (its mod is not loaded) is ignored
		}

		/// <summary>The class id the item has under these rules (hand edit, else its own), or null.</summary>
		public string ClassKeyOf(Item it)
		{
			if (C.ByType.TryGetValue(it.type, out var o) && o.Class != null) return o.Class == "none" ? null : o.Class;
			return WeaponTags.DerivedClass(it);
		}

		/// <summary>The category ids of an item under these rules: its own, plus the added ones, minus the removed ones. Never null.</summary>
		public string[] CatsOf(Item it)
		{
			var c = C;
			if (c.Cats.TryGetValue(it.type, out var hit)) return hit;
			var set = new List<string>(2);
			string own = WeaponTags.DerivedCategory(it);
			c.ByType.TryGetValue(it.type, out var o);
			if (own != null && (o == null || !o.CatRemove.Contains(own))) set.Add(own);
			if (o != null) foreach (var a in o.CatAdd) if (!set.Contains(a)) set.Add(a);
			return c.Cats[it.type] = set.ToArray();
		}

		/// <summary>Categories of an item under the current rules, or its own category when no rules are current.</summary>
		public static string[] CatsFor(Item it)
		{
			var r = Current;
			if (r != null) return r.CatsOf(it);
			string own = WeaponTags.DerivedCategory(it);
			return own == null ? Array.Empty<string>() : new[] { own };
		}

		public static bool Has(string[] cats, string cat)
		{
			if (cats == null) return false;
			for (int i = 0; i < cats.Length; i++) if (cats[i] == cat) return true;
			return false;
		}

		/// <summary>The product of every enabled weight rule that matches the item (1 = none), with a short note of which ones.</summary>
		public float RuleFactor(Item it, out string note)
		{
			note = null;
			var c = C;
			if (c.Weights.Count == 0) return 1f;
			var cats = CatsOf(it);
			float f = 1f;
			foreach (var (cat, w) in c.Weights)
				if (Has(cats, cat)) { f *= w; note = note == null ? WeaponTags.DisplayName(cat, true) + " x" + w.ToString("0.##") : note + ", " + WeaponTags.DisplayName(cat, true) + " x" + w.ToString("0.##"); }
			return f;
		}

		// ------------------------------------------------------------------ editing helpers (used by the panel, the probe and the checks)

		public RuleBlock AddBlock(RuleKind kind)
		{
			if (Blocks.Count >= MaxBlocks) return null;
			var b = new RuleBlock { Kind = kind };
			if (kind == RuleKind.ItemList) b.List = ListMode.Never;
			Blocks.Add(b);
			Changed();
			return b;
		}

		public bool Move(int index, int dir)
		{
			int to = index + dir;
			if (index < 0 || index >= Blocks.Count || to < 0 || to >= Blocks.Count) return false;
			(Blocks[index], Blocks[to]) = (Blocks[to], Blocks[index]);
			Changed();
			return true;
		}

		public bool Remove(int index)
		{
			if (index < 0 || index >= Blocks.Count) return false;
			Blocks.RemoveAt(index);
			Changed();
			return true;
		}

		// ------------------------------------------------------------------ persistence and wire format (one serializer for both, bounds-checked on read)

		public byte[] ToBytes()
		{
			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms))
			{
				w.Write((byte)FormatVersion);
				w.Write((ushort)Math.Min(Blocks.Count, MaxBlocks));
				for (int i = 0; i < Blocks.Count && i < MaxBlocks; i++)
				{
					var b = Blocks[i];
					w.Write((byte)b.Kind); w.Write(b.On);
					switch (b.Kind)
					{
						case RuleKind.ClassBalance:
							w.Write((byte)Math.Min(b.Classes.Count, 16));
							for (int k = 0; k < b.Classes.Count && k < 16; k++) { var e = b.Classes[k]; w.Write(e.Class ?? ""); w.Write(e.Weight); w.Write((short)e.Min); w.Write((short)e.Max); w.Write((short)e.Fixed); }
							break;
						case RuleKind.Category:
							w.Write((byte)b.Mode); w.Write(b.Cat ?? ""); w.Write(b.Other ?? ""); w.Write(b.Weight); w.Write((short)Math.Min(b.Count, short.MaxValue));
							break;
						case RuleKind.ItemList:
							w.Write((byte)b.List); w.Write((byte)Math.Min(b.Items.Count, MaxItemsPerList));
							for (int k = 0; k < b.Items.Count && k < MaxItemsPerList; k++) w.Write(b.Items[k] ?? "");
							break;
					}
				}
				int n = Math.Min(Overrides.Count, MaxOverrides);
				w.Write((ushort)n);
				int written = 0;
				foreach (var kv in Overrides)
				{
					if (written++ >= n) break;
					var o = kv.Value;
					w.Write(kv.Key); w.Write((byte)o.Rank); w.Write(o.Value); w.Write(o.Class ?? "");
					w.Write((byte)Math.Min(o.CatAdd.Count, 16)); int a = 0; foreach (var s in o.CatAdd) { if (a++ >= 16) break; w.Write(s); }
					w.Write((byte)Math.Min(o.CatRemove.Count, 16)); a = 0; foreach (var s in o.CatRemove) { if (a++ >= 16) break; w.Write(s); }
				}
				w.Write((byte)Math.Clamp(SidearmLimit, 0, LevyConstants.MaxSidearms)); w.Write(SummonsForOthers); // appended last: data written before these existed ends above and reads as no limit
			}
			return ms.ToArray();
		}

		/// <summary>Reads a rule set written by <see cref="ToBytes"/>. Never throws: bad or oversized data yields the default list. Every field is clamped.</summary>
		public static ArmoryRules FromBytes(byte[] data)
		{
			if (data == null || data.Length == 0 || data.Length > MaxWireBytes) return Default();
			try
			{
				using var ms = new MemoryStream(data);
				using var r = new BinaryReader(ms);
				if (r.ReadByte() != FormatVersion) return Default();
				var res = new ArmoryRules();
				int nb = Math.Min((int)r.ReadUInt16(), MaxBlocks);
				for (int i = 0; i < nb; i++)
				{
					var kind = (RuleKind)r.ReadByte();
					var b = new RuleBlock { Kind = kind, On = r.ReadBoolean() };
					if (kind > RuleKind.ItemList) return Default();
					switch (kind)
					{
						case RuleKind.ClassBalance:
							int nc = Math.Min((int)r.ReadByte(), 16);
							for (int k = 0; k < nc; k++)
							{
								var e = new ClassEntry { Class = Id(r.ReadString()), Weight = Clamp(r.ReadSingle(), 0f, 100f), Min = Math.Clamp((int)r.ReadInt16(), 0, 9999), Max = Math.Clamp((int)r.ReadInt16(), -1, 9999), Fixed = Math.Clamp((int)r.ReadInt16(), -1, 9999) };
								if (e.Class.Length > 0) b.Classes.Add(e);
							}
							break;
						case RuleKind.Category:
							byte mode = r.ReadByte(); if (mode > (byte)CatMode.Min) mode = 0;
							b.Mode = (CatMode)mode; b.Cat = Id(r.ReadString()); b.Other = Id(r.ReadString()); b.Weight = Clamp(r.ReadSingle(), 0.01f, 100f); b.Count = Math.Clamp((int)r.ReadInt16(), 0, 9999);
							break;
						case RuleKind.ItemList:
							byte lm = r.ReadByte(); b.List = lm == 0 ? ListMode.Only : ListMode.Never;
							int ni = Math.Min((int)r.ReadByte(), MaxItemsPerList);
							for (int k = 0; k < ni; k++) { string key = r.ReadString(); if (key.Length > 0 && key.Length <= 96) b.Items.Add(key); }
							break;
					}
					res.Blocks.Add(b);
				}
				int no = Math.Min((int)r.ReadUInt16(), MaxOverrides);
				for (int i = 0; i < no; i++)
				{
					string key = r.ReadString();
					var o = new ItemOverride { Rank = (RankOverrideMode)Math.Min((int)r.ReadByte(), 3), Value = Clamp(r.ReadSingle(), 0f, 1e9f) };
					string cls = Id(r.ReadString()); o.Class = cls.Length == 0 ? null : cls;
					int na = Math.Min((int)r.ReadByte(), 16); for (int k = 0; k < na; k++) { string s = Id(r.ReadString()); if (s.Length > 0) o.CatAdd.Add(s); }
					int nr = Math.Min((int)r.ReadByte(), 16); for (int k = 0; k < nr; k++) { string s = Id(r.ReadString()); if (s.Length > 0) o.CatRemove.Add(s); }
					if (key.Length > 0 && key.Length <= 96 && !o.IsEmpty) res.Overrides[key] = o;
				}
				if (ms.Position + 2 <= ms.Length) { res.SidearmLimit = Math.Clamp((int)r.ReadByte(), 0, LevyConstants.MaxSidearms); res.SummonsForOthers = r.ReadBoolean(); }
				return res;
			}
			catch (Exception) { return Default(); }
		}

		private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Clamp(v, lo, hi);

		/// <summary>An id read from outside: lower-case letters, digits and underscore, at most 24 characters; anything else is dropped.</summary>
		private static string Id(string s)
		{
			if (string.IsNullOrEmpty(s)) return "";
			if (s.Length > 24) return "";
			foreach (char ch in s) if (!(ch >= 'a' && ch <= 'z') && !(ch >= '0' && ch <= '9') && ch != '_') return "";
			return s;
		}
	}

	// ---------------------------------------------------------------------- compiled hard rules (read by WeaponAllocator)

	internal sealed class OrderRule { public string A, B; public int Block; }
	internal sealed class MinRule { public string Cat; public int Count; public int Block; }
	internal sealed class ClassPlanRule { public int Block; public List<ClassEntry> Entries; }

	internal sealed class AllocRules
	{
		public readonly List<OrderRule> Orders = new();
		public readonly List<MinRule> Mins = new();
		public ClassPlanRule Class;
		public bool HasOrders => Orders.Count > 0;
		public bool Active => Orders.Count > 0 || Mins.Count > 0 || Class != null;

		/// <summary>True when a rule at or before <paramref name="limit"/> (block index) puts a weapon with categories <paramref name="a"/> ahead of one with <paramref name="b"/>: a has the first category, b has the second and not the first.</summary>
		public bool Beats(string[] a, string[] b, int limit = int.MaxValue)
		{
			if (b == null) return false;
			foreach (var o in Orders)
				if (o.Block <= limit && ArmoryRules.Has(a, o.A) && ArmoryRules.Has(b, o.B) && !ArmoryRules.Has(b, o.A)) return true;
			return false;
		}
	}
}
