using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Soldiers;

namespace Levy.Content.TileEntities
{
	/// <summary>120-slot storage plus issue rows. Soldiers take from issue slots first, storage second, best-first by damage.</summary>
	public class ArmoryTE : ModTileEntity
	{
		public const int StorageSize = 120, WeaponSlots = 6, ArmorSlots = 9, AmmoSlots = 4, PotionSlots = 2;
		public const int SidearmSlot = 5;

		public Item[] Storage = NewItems(StorageSize);
		public Item[] IssueWeapons = NewItems(WeaponSlots);
		/// <summary>3 sets x (head, body, legs): index = set*3 + part.</summary>
		public Item[] IssueArmor = NewItems(ArmorSlots);
		public Item[] IssueAmmo = NewItems(AmmoSlots);
		/// <summary>0 = healing, 1 = buff.</summary>
		public Item[] IssuePotions = NewItems(PotionSlots);

		public static Item[] NewItems(int n) { var a = new Item[n]; for (int i = 0; i < n; i++) a[i] = new Item(); return a; }
		public Vector2 Center => new Vector2((Position.X + 1f) * 16f, (Position.Y + 1.5f) * 16f);

		public override bool IsTileValidForEntity(int x, int y) => false; // Tiles agent replaces
		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) => -1; // Tiles agent replaces

		// ---- issue API (implemented by the Tiles agent; called by Roster/Brain/Combat) ----
		/// <summary>Best issuable weapon for this soldier (family must be supported); removes it from the armory. Null if none.</summary>
		public virtual Item TakeBestWeapon(SoldierRecord forSoldier) => null;
		/// <summary>Best issuable sidearm (melee item in slot 5 or any supported melee); removes it. Null if none.</summary>
		public virtual Item TakeSidearm(SoldierRecord forSoldier) => null;
		/// <summary>Best armor piece for the given equip slot (0 head, 1 body, 2 legs); removes it. Null if none.</summary>
		public virtual Item TakeArmor(SoldierRecord forSoldier, int part) => null;
		/// <summary>Best accessory not already worn; removes it. Null if none.</summary>
		public virtual Item TakeAccessory(SoldierRecord forSoldier) => null;
		/// <summary>Fills the pouch with ammo of <paramref name="ammoId"/> (AmmoID.Bullet/Arrow) from issue slots in priority order, then storage. Returns the amount taken.</summary>
		public virtual int TakeAmmo(int ammoId, Item pouch, int max) => 0;
		/// <summary>Takes one potion of the given issue kind (0 healing, 1 buff). Null if none.</summary>
		public virtual Item TakePotion(int kind) => null;
		/// <summary>Puts an item into bulk storage (stacking); returns what did not fit (or null).</summary>
		public virtual Item Deposit(Item item) => item;
		/// <summary>Auto-issue best-first: weapon, sidearm, armor, accessories (grade slots), ammo. Returns true if anything changed.</summary>
		public virtual bool IssueGear(SoldierRecord soldier) => false;
		/// <summary>Returns a soldier's whole kit to storage (stand down / discharge / softcore death).</summary>
		public virtual void ReturnGear(SoldierGear gear) { }

		public override void SaveData(TagCompound tag)
		{
			tag["storage"] = SaveList(Storage); tag["weapons"] = SaveList(IssueWeapons); tag["armor"] = SaveList(IssueArmor); tag["ammo"] = SaveList(IssueAmmo); tag["potions"] = SaveList(IssuePotions);
		}
		public override void LoadData(TagCompound tag)
		{
			LoadList(tag, "storage", Storage); LoadList(tag, "weapons", IssueWeapons); LoadList(tag, "armor", IssueArmor); LoadList(tag, "ammo", IssueAmmo); LoadList(tag, "potions", IssuePotions);
		}
		private static List<TagCompound> SaveList(Item[] items) { var l = new List<TagCompound>(items.Length); foreach (var it in items) l.Add(ItemIO.Save(it)); return l; }
		private static void LoadList(TagCompound tag, string key, Item[] into) { var l = tag.GetList<TagCompound>(key); for (int i = 0; i < into.Length; i++) into[i] = i < l.Count ? ItemIO.Load(l[i]) : new Item(); }

		public override void NetSend(BinaryWriter writer)
		{
			foreach (var arr in new[] { Storage, IssueWeapons, IssueArmor, IssueAmmo, IssuePotions }) foreach (var it in arr) ItemIO.Send(it, writer, true);
		}
		public override void NetReceive(BinaryReader reader)
		{
			foreach (var arr in new[] { Storage, IssueWeapons, IssueArmor, IssueAmmo, IssuePotions }) for (int i = 0; i < arr.Length; i++) arr[i] = ItemIO.Receive(reader, true);
		}
	}
}
