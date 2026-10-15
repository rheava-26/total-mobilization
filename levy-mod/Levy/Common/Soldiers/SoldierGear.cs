using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace Levy.Common.Soldiers
{
	/// <summary>Everything a soldier carries. Items are real Item instances (prefixes, stacks, mod data preserved).</summary>
	public class SoldierGear
	{
		public const int AccessorySlots = 5;
		public const int DyeSlots = 8; // 0..2 armor, 3..7 accessories

		public Item Weapon = new Item();
		public Item Sidearm = new Item();
		public Item Head = new Item();
		public Item Body = new Item();
		public Item Legs = new Item();
		public Item[] Accessories = NewArray(AccessorySlots);
		public Item[] Dyes = NewArray(DyeSlots);
		/// <summary>Ammo pouch: one ammo type, up to <see cref="LevyConstants.AmmoPouchMax"/>.</summary>
		public Item Ammo = new Item();

		private static Item[] NewArray(int n) { var a = new Item[n]; for (int i = 0; i < n; i++) a[i] = new Item(); return a; }

		public bool HasWeapon => Weapon != null && !Weapon.IsAir;
		public bool HasSidearm => Sidearm != null && !Sidearm.IsAir;
		public bool HasAmmo => Ammo != null && !Ammo.IsAir && Ammo.stack > 0;

		/// <summary>All non-air items, for dropping or returning to the armory.</summary>
		public IEnumerable<Item> AllItems()
		{
			if (!Weapon.IsAir) yield return Weapon;
			if (!Sidearm.IsAir) yield return Sidearm;
			if (!Head.IsAir) yield return Head;
			if (!Body.IsAir) yield return Body;
			if (!Legs.IsAir) yield return Legs;
			foreach (var a in Accessories) if (!a.IsAir) yield return a;
			foreach (var d in Dyes) if (!d.IsAir) yield return d;
			if (HasAmmo) yield return Ammo;
		}

		public void Clear()
		{
			Weapon = new Item(); Sidearm = new Item(); Head = new Item(); Body = new Item(); Legs = new Item(); Ammo = new Item();
			Accessories = NewArray(AccessorySlots); Dyes = NewArray(DyeSlots);
		}

		public SoldierGear Clone()
		{
			var g = new SoldierGear
			{
				Weapon = Weapon.Clone(), Sidearm = Sidearm.Clone(), Head = Head.Clone(), Body = Body.Clone(), Legs = Legs.Clone(), Ammo = Ammo.Clone(),
			};
			for (int i = 0; i < AccessorySlots; i++) g.Accessories[i] = Accessories[i].Clone();
			for (int i = 0; i < DyeSlots; i++) g.Dyes[i] = Dyes[i].Clone();
			return g;
		}

		public TagCompound Save()
		{
			var tag = new TagCompound
			{
				["weapon"] = ItemIO.Save(Weapon), ["sidearm"] = ItemIO.Save(Sidearm), ["head"] = ItemIO.Save(Head), ["body"] = ItemIO.Save(Body),
				["legs"] = ItemIO.Save(Legs), ["ammo"] = ItemIO.Save(Ammo),
			};
			var acc = new List<TagCompound>(); foreach (var a in Accessories) acc.Add(ItemIO.Save(a)); tag["acc"] = acc;
			var dyes = new List<TagCompound>(); foreach (var d in Dyes) dyes.Add(ItemIO.Save(d)); tag["dyes"] = dyes;
			return tag;
		}

		public static SoldierGear Load(TagCompound tag)
		{
			var g = new SoldierGear();
			if (tag == null) return g;
			g.Weapon = ItemIO.Load(tag.GetCompound("weapon")); g.Sidearm = ItemIO.Load(tag.GetCompound("sidearm"));
			g.Head = ItemIO.Load(tag.GetCompound("head")); g.Body = ItemIO.Load(tag.GetCompound("body")); g.Legs = ItemIO.Load(tag.GetCompound("legs"));
			g.Ammo = ItemIO.Load(tag.GetCompound("ammo"));
			var acc = tag.GetList<TagCompound>("acc"); for (int i = 0; i < AccessorySlots && i < acc.Count; i++) g.Accessories[i] = ItemIO.Load(acc[i]);
			var dyes = tag.GetList<TagCompound>("dyes"); for (int i = 0; i < DyeSlots && i < dyes.Count; i++) g.Dyes[i] = ItemIO.Load(dyes[i]);
			return g;
		}

		/// <summary>Visual-only sync (type + prefix) for clients that draw the body.</summary>
		public void WriteVisual(BinaryWriter w)
		{
			void W(Item it) { w.Write(it.type); w.Write((ushort)it.prefix); }
			W(Weapon); W(Sidearm); W(Head); W(Body); W(Legs);
			foreach (var a in Accessories) W(a);
			foreach (var d in Dyes) W(d);
			w.Write(Ammo.type); w.Write(Ammo.stack);
		}

		public void ReadVisual(BinaryReader r)
		{
			Item R() { var it = new Item(); int t = r.ReadInt32(); int p = r.ReadUInt16(); if (t > 0) { it.SetDefaults(t); if (p > 0) it.Prefix(p); } return it; }
			Weapon = R(); Sidearm = R(); Head = R(); Body = R(); Legs = R();
			for (int i = 0; i < AccessorySlots; i++) Accessories[i] = R();
			for (int i = 0; i < DyeSlots; i++) Dyes[i] = R();
			int at = r.ReadInt32(); int ast = r.ReadInt32(); Ammo = new Item(); if (at > 0) { Ammo.SetDefaults(at); Ammo.stack = ast; }
		}
	}
}
