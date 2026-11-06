using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;
using Levy.Common.Weapons;

namespace Levy.Common.Vanity
{
	/// <summary>Who a vanity set dresses.</summary>
	public enum VanityTargetKind : byte
	{
		/// <summary>Every levy this armory serves.</summary>
		All,
		/// <summary>Every levy whose weapon is of one class (melee, ranged, magic, summon, ...).</summary>
		Class,
		/// <summary>Named levies, by roster id.</summary>
		Levies,
	}

	/// <summary>
	/// One row of the vanity screen: head, body and legs vanity, a dye for each, and who wears it. The items live here once and dress every levy the row targets; nothing is taken from anyone per levy,
	/// and the set never changes a stat (it only fills the social armor slots the player renderer reads).
	/// </summary>
	public sealed class VanitySet
	{
		public const int Head = 0, Body = 1, Legs = 2, DyeHead = 3, DyeBody = 4, DyeLegs = 5, Slots = 6;
		public const int MaxLevies = 64;

		public bool On = true;
		public VanityTargetKind Target = VanityTargetKind.All;
		/// <summary>The class id when <see cref="Target"/> is <see cref="VanityTargetKind.Class"/>.</summary>
		public string ClassKey;
		/// <summary>Roster ids when <see cref="Target"/> is <see cref="VanityTargetKind.Levies"/>.</summary>
		public List<int> Levies = new();
		public Item[] Items = NewItems();

		public static Item[] NewItems() { var a = new Item[Slots]; for (int i = 0; i < Slots; i++) a[i] = new Item(); return a; }

		/// <summary>Whether this item may sit in the slot: head / body / legs pieces go in their own slot, a dye in a dye slot. One of each, never a stack.</summary>
		public static bool Fits(int slot, Item it)
		{
			if (it == null || it.IsAir) return true;
			switch (slot)
			{
				case Head: return it.headSlot >= 0 && !it.accessory;
				case Body: return it.bodySlot >= 0 && !it.accessory;
				case Legs: return it.legSlot >= 0 && !it.accessory;
				case DyeHead: case DyeBody: case DyeLegs: return it.dye > 0;
				default: return false;
			}
		}

		public bool IsEmpty { get { foreach (var it in Items) if (it != null && !it.IsAir) return false; return true; } }

		public bool Targets(int levyId, string classKey)
		{
			if (!On) return false;
			switch (Target)
			{
				case VanityTargetKind.All: return true;
				case VanityTargetKind.Class: return ClassKey != null && ClassKey == classKey;
				default: return Levies.Contains(levyId);
			}
		}

		public VanitySet Clone()
		{
			var c = new VanitySet { On = On, Target = Target, ClassKey = ClassKey, Levies = new List<int>(Levies) };
			for (int i = 0; i < Slots; i++) c.Items[i] = Items[i].Clone();
			return c;
		}

		/// <summary>Keeps only legal contents: every item fits its slot and is one piece, the class id is sane, the levy list is deduplicated and bounded.</summary>
		public void Sanitize()
		{
			for (int i = 0; i < Slots; i++)
			{
				var it = Items[i] ??= new Item();
				if (it.IsAir || !Fits(i, it)) { Items[i] = new Item(); continue; }
				it.stack = 1; it.favorited = false;
			}
			if (ClassKey != null && (ClassKey.Length == 0 || ClassKey.Length > 24)) ClassKey = null;
			if (Target == VanityTargetKind.Class && ClassKey == null) Target = VanityTargetKind.All;
			var seen = new HashSet<int>(); var kept = new List<int>();
			foreach (int id in Levies) if (kept.Count < MaxLevies && seen.Add(id)) kept.Add(id);
			Levies = kept;
		}
	}

	/// <summary>What one levy should wear, resolved from the rows. Entries are the stored items themselves (not copies); null = nothing from the vanity screen for that slot.</summary>
	public sealed class VanityLook
	{
		public readonly Item[] Items = new Item[VanitySet.Slots];
		public Item Head => Items[VanitySet.Head];
		public Item Body => Items[VanitySet.Body];
		public Item Legs => Items[VanitySet.Legs];
		public bool Any { get { foreach (var it in Items) if (it != null) return true; return false; } }

		/// <summary>A number that changes when the look does (which item types are worn and which dyes), for the draw's change check.</summary>
		public int Hash()
		{
			int h = 17;
			foreach (var it in Items) h = unchecked(h * 31 + (it == null ? 0 : it.type + 1));
			return h;
		}
	}

	/// <summary>
	/// An armory's vanity sets, top to bottom: each row dresses the levies it targets. Saved with the armory, and synced to and from clients as <see cref="ToBytes"/> (the same pattern as <see cref="ArmoryRules"/>).
	/// </summary>
	public sealed class ArmoryVanity
	{
		public const int MaxSets = 16, MaxWireBytes = 48 * 1024, FormatVersion = 1;

		public List<VanitySet> Sets = new();
		/// <summary>Bumped by the server on every accepted edit.</summary>
		public int Rev;

		public bool IsNeutral => Sets.Count == 0;

		public ArmoryVanity Clone() { var c = new ArmoryVanity { Rev = Rev }; foreach (var s in Sets) c.Sets.Add(s.Clone()); return c; }

		public void CopyFrom(ArmoryVanity o)
		{
			Sets = new List<VanitySet>();
			foreach (var s in o.Sets) Sets.Add(s.Clone());
			Rev = o.Rev;
		}

		public VanitySet AddSet()
		{
			if (Sets.Count >= MaxSets) return null;
			var s = new VanitySet();
			Sets.Add(s);
			return s;
		}

		public void Remove(int index) { if (index >= 0 && index < Sets.Count) Sets.RemoveAt(index); }

		public void Move(int index, int by)
		{
			int to = index + by;
			if (index < 0 || index >= Sets.Count || to < 0 || to >= Sets.Count) return;
			(Sets[index], Sets[to]) = (Sets[to], Sets[index]);
		}

		/// <summary>
		/// What a levy wears. Each of the six slots is settled on its own: the most specific row that fills it wins (named levies, then a class, then everyone), and among equals the upper row.
		/// A slot no row fills is null, so the levy shows his own gear there.
		/// </summary>
		public VanityLook Resolve(int levyId, string classKey)
		{
			var look = new VanityLook();
			if (Sets.Count == 0) return look;
			for (int pass = 0; pass < 3; pass++)
			{
				var kind = pass == 0 ? VanityTargetKind.Levies : pass == 1 ? VanityTargetKind.Class : VanityTargetKind.All;
				foreach (var s in Sets)
				{
					if (s.Target != kind || !s.Targets(levyId, classKey)) continue;
					for (int i = 0; i < VanitySet.Slots; i++)
						if (look.Items[i] == null && s.Items[i] != null && !s.Items[i].IsAir) look.Items[i] = s.Items[i];
				}
			}
			return look;
		}

		// ------------------------------------------------------------------ save (names, so a changed mod list cannot scramble the items)

		public TagCompound ToTag()
		{
			var sets = new List<TagCompound>();
			foreach (var s in Sets)
			{
				var items = new List<TagCompound>();
				foreach (var it in s.Items) items.Add(ItemIO.Save(it ?? new Item()));
				sets.Add(new TagCompound { ["on"] = s.On, ["kind"] = (int)s.Target, ["cls"] = s.ClassKey ?? "", ["levies"] = new List<int>(s.Levies), ["items"] = items });
			}
			return new TagCompound { ["v"] = FormatVersion, ["sets"] = sets };
		}

		public static ArmoryVanity FromTag(TagCompound tag)
		{
			var v = new ArmoryVanity();
			if (tag == null || !tag.ContainsKey("sets")) return v;
			try
			{
				foreach (var t in tag.GetList<TagCompound>("sets"))
				{
					if (v.Sets.Count >= MaxSets) break;
					var s = new VanitySet { On = t.GetBool("on") };
					int kind = t.GetInt("kind"); s.Target = kind >= 0 && kind <= (int)VanityTargetKind.Levies ? (VanityTargetKind)kind : VanityTargetKind.All;
					string cls = t.GetString("cls"); s.ClassKey = string.IsNullOrEmpty(cls) ? null : cls;
					s.Levies = new List<int>(t.GetList<int>("levies"));
					var items = t.GetList<TagCompound>("items");
					for (int i = 0; i < VanitySet.Slots && i < items.Count; i++) s.Items[i] = ItemIO.Load(items[i]);
					s.Sanitize();
					v.Sets.Add(s);
				}
			}
			catch (Exception) { return new ArmoryVanity(); }
			return v;
		}

		// ------------------------------------------------------------------ wire

		public byte[] ToBytes()
		{
			using var ms = new MemoryStream();
			using (var w = new BinaryWriter(ms))
			{
				w.Write((byte)FormatVersion);
				w.Write((byte)Math.Min(Sets.Count, MaxSets));
				for (int n = 0; n < Sets.Count && n < MaxSets; n++)
				{
					var s = Sets[n];
					w.Write(s.On); w.Write((byte)s.Target); w.Write(s.ClassKey ?? "");
					w.Write((byte)Math.Min(s.Levies.Count, VanitySet.MaxLevies));
					for (int i = 0; i < s.Levies.Count && i < VanitySet.MaxLevies; i++) w.Write(s.Levies[i]);
					for (int i = 0; i < VanitySet.Slots; i++)
					{
						var it = s.Items[i];
						bool has = it != null && !it.IsAir;
						w.Write(has);
						if (has) ItemIO.Send(it, w, false, false);
					}
				}
			}
			return ms.ToArray();
		}

		/// <summary>Reads a vanity list written by <see cref="ToBytes"/>. Never throws: bad or oversized data yields an empty list, and every field is clamped (items must fit their slot).</summary>
		public static ArmoryVanity FromBytes(byte[] data)
		{
			var v = new ArmoryVanity();
			if (data == null || data.Length == 0 || data.Length > MaxWireBytes) return v;
			try
			{
				using var r = new BinaryReader(new MemoryStream(data));
				if (r.ReadByte() != FormatVersion) return v;
				int n = Math.Min((int)r.ReadByte(), MaxSets);
				for (int k = 0; k < n; k++)
				{
					var s = new VanitySet { On = r.ReadBoolean() };
					byte kind = r.ReadByte(); s.Target = kind <= (byte)VanityTargetKind.Levies ? (VanityTargetKind)kind : VanityTargetKind.All;
					string cls = r.ReadString(); s.ClassKey = cls.Length == 0 ? null : cls;
					int lc = Math.Min((int)r.ReadByte(), VanitySet.MaxLevies);
					for (int i = 0; i < lc; i++) s.Levies.Add(r.ReadInt32());
					for (int i = 0; i < VanitySet.Slots; i++) if (r.ReadBoolean()) s.Items[i] = ItemIO.Receive(r, false, false);
					s.Sanitize();
					v.Sets.Add(s);
				}
			}
			catch (Exception) { return new ArmoryVanity(); }
			return v;
		}
	}
}
