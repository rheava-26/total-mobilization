using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>120-slot storage plus issue rows. Soldiers take from issue slots first, storage second, best-first by damage.</summary>
	public class ArmoryTE : ModTileEntity
	{
		public const int StorageSize = 120, WeaponSlots = 12, ArmorSets = 4, AmmoSlots = 6, PotionSlots = 2;
		/// <summary>Armor pieces per set: head, body, legs.</summary>
		public const int ArmorParts = 3;
		public const int ArmorSlots = ArmorSets * ArmorParts;
		/// <summary>The sidearm slot is always the last weapon slot.</summary>
		public const int SidearmSlot = WeaponSlots - 1;
		/// <summary>Weapon slot count of worlds saved before the issue rows were enlarged (sidearm was the last of these).</summary>
		private const int LegacyWeaponSlots = 6;

		public Item[] Storage = NewItems(StorageSize);
		public Item[] IssueWeapons = NewItems(WeaponSlots);
		/// <summary>ArmorSets x (head, body, legs): index = set*ArmorParts + part.</summary>
		public Item[] IssueArmor = NewItems(ArmorSlots);
		public Item[] IssueAmmo = NewItems(AmmoSlots);
		/// <summary>0 = healing, 1 = buff.</summary>
		public Item[] IssuePotions = NewItems(PotionSlots);

		private bool _dirty;

		public static Item[] NewItems(int n) { var a = new Item[n]; for (int i = 0; i < n; i++) a[i] = new Item(); return a; }
		public Vector2 Center => new Vector2((Position.X + 1f) * 16f, (Position.Y + 1.5f) * 16f);

		// ------------------------------------------------------------------ placement / validity / net

		public override bool IsTileValidForEntity(int x, int y)
		{
			if (!WorldGen.InWorld(x, y)) return false;
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<ArmoryTile>() && tile.TileFrameX == 0 && tile.TileFrameY == 0;
		}

		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				NetMessage.SendTileSquare(Main.myPlayer, i, j, LevyConstants.ArmoryWidth, LevyConstants.ArmoryHeight);
				NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: Type);
				return -1;
			}
			return Place(i, j);
		}

		/// <summary>Queue a sync to clients (sent once on the next server tick, so a 40-man muster costs one packet).</summary>
		public void MarkDirty() => _dirty = true;

		/// <summary>Sync now (server only; no-op elsewhere).</summary>
		public void Sync()
		{
			_dirty = false;
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
		}

		public override void Update()
		{
			if (_dirty) Sync();
		}

		// ------------------------------------------------------------------ helpers

		private static bool IsAir(Item it) => it == null || it.IsAir || it.stack <= 0;

		private static bool IsSupportedFamily(WeaponFamily f) =>
			f == WeaponFamily.Gun || f == WeaponFamily.Bow || f == WeaponFamily.Magic || IsMeleeFamily(f);

		private static bool IsMeleeFamily(WeaponFamily f) =>
			f == WeaponFamily.Broadsword || f == WeaponFamily.Shortsword || f == WeaponFamily.Spear;

		/// <summary>A weapon the armory may hand out: the classifier accepts it AND its family is one a soldier can use.</summary>
		private static bool IsIssuable(Item it) => !IsAir(it) && WeaponClassifier.IsIssuableWeapon(it) && IsSupportedFamily(WeaponClassifier.Classify(it));

		private static bool IsIssuableMelee(Item it) => IsIssuable(it) && IsMeleeFamily(WeaponClassifier.Classify(it));

		/// <summary>True when the weapon needs no ammo, the soldier's pouch already holds its ammo, or the armory has some.</summary>
		private bool HasAmmoFor(Item weapon, SoldierRecord soldier)
		{
			int ammoId = weapon.useAmmo;
			if (ammoId <= 0) return true;
			var pouch = soldier?.Gear?.Ammo;
			if (!IsAir(pouch) && pouch.ammo == ammoId) return true;
			return AnyAmmo(IssueAmmo, ammoId) || AnyAmmo(Storage, ammoId);
		}

		private static bool AnyAmmo(Item[] arr, int ammoId)
		{
			for (int i = 0; i < arr.Length; i++) { var it = arr[i]; if (!IsAir(it) && it.ammo == ammoId) return true; }
			return false;
		}

		/// <summary>Best slot in <paramref name="arr"/> over indices from, from+step, ... (&lt; to) that passes <paramref name="pred"/>; strictly-better wins so the leftmost takes ties.</summary>
		private static bool TryBest(Item[] arr, Func<Item, bool> pred, Func<Item, float> score, out int bestIdx, int from = 0, int to = -1, int step = 1)
		{
			bestIdx = -1; float best = float.NegativeInfinity;
			if (to < 0 || to > arr.Length) to = arr.Length;
			for (int i = from; i < to; i += step) {
				var it = arr[i];
				if (IsAir(it) || !pred(it)) continue;
				float s = score(it);
				if (s > best) { best = s; bestIdx = i; }
			}
			return bestIdx >= 0;
		}

		/// <summary>Removes and returns the real Item instance in a slot (the slot becomes air).</summary>
		private Item Remove(Item[] arr, int idx)
		{
			var it = arr[idx];
			arr[idx] = new Item();
			MarkDirty();
			return it;
		}

		/// <summary>Issue slots first (indices issueFrom..issueTo step issueStep), bulk storage second; best-first within each.</summary>
		private Item TakeBest(Item[] issue, int issueFrom, int issueTo, int issueStep, Func<Item, bool> pred, Func<Item, float> score)
		{
			if (issue != null && TryBest(issue, pred, score, out int idx, issueFrom, issueTo, issueStep)) return Remove(issue, idx);
			if (TryBest(Storage, pred, score, out idx)) return Remove(Storage, idx);
			return null;
		}

		private static float ArmorScore(Item it) => it.defense * 100f + it.rare;
		private static float AccessoryScore(Item it) => it.rare * 100f + it.defense;

		private static bool AlreadyWorn(Item[] worn, int type)
		{
			if (worn == null) return false;
			for (int i = 0; i < worn.Length; i++) if (!IsAir(worn[i]) && worn[i].type == type) return true;
			return false;
		}

		// ------------------------------------------------------------------ issue API

		/// <summary>Best issuable weapon for this soldier (family must be supported); removes it from the armory. Null if none.</summary>
		public virtual Item TakeBestWeapon(SoldierRecord forSoldier)
		{
			// Pass 1: weapons that can actually fire (no ammo needed, or ammo in the pouch / armory). Pass 2: anything issuable.
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => IsIssuable(it) && HasAmmoFor(it, forSoldier), WeaponClassifier.Score)
				?? TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuable, WeaponClassifier.Score);
		}

		/// <summary>Best issuable sidearm (melee item in the sidearm slot or any supported melee); removes it. Null if none.</summary>
		public virtual Item TakeSidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableMelee(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableMelee, WeaponClassifier.Score);
		}

		/// <summary>Best armor piece for the given equip slot (0 head, 1 body, 2 legs); removes it. Null if none.</summary>
		public virtual Item TakeArmor(SoldierRecord forSoldier, int part)
		{
			if (part < 0 || part > 2) return null;
			bool Pred(Item it) => WeaponClassifier.IsArmorPiece(it, part) && !it.vanity;
			// Sets are laid out head/body/legs, so the slots for one part are part, part+ArmorParts, part+2*ArmorParts, ... across every set.
			return TakeBest(IssueArmor, part, ArmorSlots, ArmorParts, Pred, ArmorScore);
		}

		/// <summary>Best accessory not already worn; removes it. Null if none.</summary>
		public virtual Item TakeAccessory(SoldierRecord forSoldier)
		{
			var worn = forSoldier?.Gear?.Accessories;
			bool Pred(Item it) => WeaponClassifier.IsUsefulAccessory(it) && !AlreadyWorn(worn, it.type);
			return TakeBest(null, 0, 0, 1, Pred, AccessoryScore);
		}

		/// <summary>Fills the pouch with ammo of <paramref name="ammoId"/> (AmmoID.Bullet/Arrow) from issue slots in priority order, then storage. Returns the amount taken.</summary>
		public virtual int TakeAmmo(int ammoId, Item pouch, int max)
		{
			if (pouch == null || ammoId <= 0 || max <= 0) return 0;
			if (!IsAir(pouch) && pouch.ammo != ammoId) return 0;
			int moved = Drain(IssueAmmo, ammoId, pouch, max);
			moved += Drain(Storage, ammoId, pouch, max);
			if (moved > 0) MarkDirty();
			return moved;
		}

		private static int Drain(Item[] arr, int ammoId, Item pouch, int max)
		{
			int moved = 0;
			for (int i = 0; i < arr.Length; i++) {
				var it = arr[i];
				if (IsAir(it) || it.ammo != ammoId) continue;
				if (!IsAir(pouch) && pouch.type != it.type) continue;
				if (IsAir(pouch)) { pouch.SetDefaults(it.type); pouch.stack = 0; }
				int room = Math.Min(max, pouch.maxStack) - pouch.stack;
				if (room <= 0) break;
				int n = Math.Min(room, it.stack);
				pouch.stack += n; it.stack -= n; moved += n;
				if (it.stack <= 0) arr[i] = new Item();
			}
			return moved;
		}

		/// <summary>Takes one potion of the given issue kind (0 healing, 1 buff). Null if none.</summary>
		public virtual Item TakePotion(int kind)
		{
			if (kind < 0 || kind >= PotionSlots) return null;
			var it = IssuePotions[kind];
			if (IsAir(it)) return null;
			if (kind == 0 && it.healLife <= 0) return null;
			var one = it.Clone();
			one.stack = 1;
			it.stack--;
			if (it.stack <= 0) IssuePotions[kind] = new Item();
			MarkDirty();
			return one;
		}

		/// <summary>Removes ONE item of this type from bulk storage (first matching stack). False if there is none.</summary>
		public virtual bool TryTakeOne(int itemType)
		{
			for (int i = 0; i < Storage.Length; i++) {
				var it = Storage[i];
				if (IsAir(it) || it.type != itemType) continue;
				it.stack--;
				if (it.stack <= 0) Storage[i] = new Item();
				MarkDirty();
				return true;
			}
			return false;
		}

		/// <summary>Puts an item into bulk storage (stacking); returns what did not fit (or null).</summary>
		public virtual Item Deposit(Item item)
		{
			if (IsAir(item)) return null;
			// 1. top up matching stacks (same type and prefix, below max stack)
			for (int i = 0; i < Storage.Length; i++) {
				var s = Storage[i];
				if (IsAir(s) || s.type != item.type || s.prefix != item.prefix || s.stack >= s.maxStack) continue;
				if (ItemLoader.TryStackItems(s, item, out int n) && n > 0) MarkDirty();
				if (IsAir(item)) return null;
			}
			// 2. first empty slot takes the instance itself
			for (int i = 0; i < Storage.Length; i++) {
				if (!IsAir(Storage[i])) continue;
				Storage[i] = item;
				MarkDirty();
				return null;
			}
			return item;
		}

		/// <summary>Auto-issue best-first: weapon, sidearm, armor, accessories (grade slots), ammo. Returns true if anything changed.</summary>
		public virtual bool IssueGear(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			bool changed = false;

			if (!g.HasWeapon) { var w = TakeBestWeapon(soldier); if (w != null) { g.Weapon = w; changed = true; } }

			// A melee primary needs no fallback blade; save the swords for the gunners.
			bool primaryMelee = g.HasWeapon && IsMeleeFamily(WeaponClassifier.Classify(g.Weapon));
			if (!g.HasSidearm && !primaryMelee) { var s = TakeSidearm(soldier); if (s != null) { g.Sidearm = s; changed = true; } }

			if (IsAir(g.Head)) { var a = TakeArmor(soldier, 0); if (a != null) { g.Head = a; changed = true; } }
			if (IsAir(g.Body)) { var a = TakeArmor(soldier, 1); if (a != null) { g.Body = a; changed = true; } }
			if (IsAir(g.Legs)) { var a = TakeArmor(soldier, 2); if (a != null) { g.Legs = a; changed = true; } }

			int slots = Math.Clamp(soldier.AccessorySlots, 0, Math.Min(SoldierGear.AccessorySlots, g.Accessories.Length));
			for (int i = 0; i < slots; i++) {
				if (!IsAir(g.Accessories[i])) continue;
				var a = TakeAccessory(soldier);
				if (a == null) break;
				g.Accessories[i] = a; changed = true;
			}

			g.Ammo ??= new Item();
			int ammoId = g.HasWeapon ? g.Weapon.useAmmo : 0;
			if (ammoId > 0) {
				if (!IsAir(g.Ammo) && g.Ammo.ammo != ammoId) {
					// Wrong ammo for the new weapon: back to storage.
					g.Ammo = Deposit(g.Ammo) ?? new Item();
					changed = true;
				}
				if (IsAir(g.Ammo) || g.Ammo.ammo == ammoId) {
					if (IsAir(g.Ammo)) g.Ammo = new Item();
					if (TakeAmmo(ammoId, g.Ammo, LevyConstants.AmmoPouchMax) > 0) changed = true;
				}
			}
			else if (g.HasWeapon && !IsAir(g.Ammo)) {
				// Blade or spellbook in hand: the pouch is dead weight.
				g.Ammo = Deposit(g.Ammo) ?? new Item();
				changed = true;
			}

			if (changed) MarkDirty();
			return changed;
		}

		/// <summary>Returns a soldier's whole kit to storage (stand down / discharge / softcore death).</summary>
		public virtual void ReturnGear(SoldierGear gear)
		{
			if (gear == null) return;
			var items = new List<Item>(gear.AllItems());
			foreach (var it in items) {
				var rem = Deposit(it);
				if (!IsAir(rem)) Spill(rem);
			}
			gear.Clear();
			MarkDirty();
		}

		/// <summary>Drops an item that found no room, in front of the armory (server / single player only).</summary>
		private void Spill(Item item)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || IsAir(item)) return;
			Item.NewItem(new EntitySource_TileEntity(this), new Vector2(Position.X * 16f, Position.Y * 16f),
				new Vector2(LevyConstants.ArmorySpillWidth, LevyConstants.ArmorySpillHeight), item);
		}

		/// <summary>
		/// Spills every stored item onto the ground (the tile was broken). Server / single player only; slots become air, so a second
		/// call spills nothing. Returns the number of item stacks spilled.
		/// </summary>
		public int DropAllContents()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return 0;
			int stacks = 0;
			foreach (var arr in new[] { Storage, IssueWeapons, IssueArmor, IssueAmmo, IssuePotions }) {
				for (int i = 0; i < arr.Length; i++) {
					if (IsAir(arr[i])) continue;
					Spill(arr[i]);
					arr[i] = new Item();
					stacks++;
				}
			}
			return stacks;
		}

		/// <summary>Whatever path removes this entity, its contents spill first (idempotent: slots are already air after a spill).</summary>
		public override void OnKill() => DropAllContents();

		// ------------------------------------------------------------------ persistence / net

		public override void SaveData(TagCompound tag)
		{
			tag["storage"] = SaveList(Storage); tag["weapons"] = SaveList(IssueWeapons); tag["armor"] = SaveList(IssueArmor); tag["ammo"] = SaveList(IssueAmmo); tag["potions"] = SaveList(IssuePotions);
		}
		public override void LoadData(TagCompound tag)
		{
			LoadList(tag, "storage", Storage); LoadList(tag, "weapons", IssueWeapons); LoadList(tag, "armor", IssueArmor); LoadList(tag, "ammo", IssueAmmo); LoadList(tag, "potions", IssuePotions);
			// Worlds saved with the smaller rows: the old sidearm (last of the old weapon slots) moves to the new last slot.
			if (tag.GetList<TagCompound>("weapons").Count == LegacyWeaponSlots && SidearmSlot != LegacyWeaponSlots - 1) {
				IssueWeapons[SidearmSlot] = IssueWeapons[LegacyWeaponSlots - 1];
				IssueWeapons[LegacyWeaponSlots - 1] = new Item();
			}
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
