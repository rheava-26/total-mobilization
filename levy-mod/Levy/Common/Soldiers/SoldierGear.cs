using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;
using Levy.Common.Weapons;

namespace Levy.Common.Soldiers
{
	/// <summary>Everything a soldier carries. Items are real Item instances (prefixes, stacks, mod data preserved).</summary>
	public class SoldierGear
	{
		/// <summary>Most accessories a soldier can carry (the Champion's 5 plus the Guide to Expertise's 1). Open slots are limited by grade; see <see cref="SoldierRecord.AccessorySlots"/>.</summary>
		public const int AccessorySlots = 6;
		/// <summary>Accessory slot count of worlds saved before the sixth was added (the ammo lock bit sat right after them).</summary>
		private const int LegacyAccessorySlots = 5;
		public const int DyeSlots = 3 + AccessorySlots; // 0..2 armor, then one per accessory

		public Item Weapon = new Item();
		public Item Sidearm = new Item();
		/// <summary>The sidearms beyond the first (<see cref="LevyConstants.ExtraSidearms"/> of them): minion staves and sentry staves, which run without being in hand.</summary>
		public Item[] ExtraSidearms = NewArray(LevyConstants.ExtraSidearms);
		public Item Head = new Item();
		public Item Body = new Item();
		public Item Legs = new Item();
		public Item[] Accessories = NewArray(AccessorySlots);
		public Item[] Dyes = NewArray(DyeSlots);
		/// <summary>Ammo pouch: one ammo type, up to <see cref="LevyConstants.AmmoPouchMax"/>.</summary>
		public Item Ammo = new Item();
		/// <summary>The grappling hook (the dummy's hook slot, miscEquips[4]). Issued from the armory like any other gear.</summary>
		public Item Hook = new Item();
		/// <summary>The camera slot: only a GoPro fits, and a levy that holds one can be watched. It takes no accessory slot.</summary>
		public Item Camera = new Item();

		// ---- hand-assigned ("locked") slots ----
		// Slot indices used by the loadout menu, the lock mask and the SoldierGearSet packet.
		public const int SlotWeapon = 0, SlotSidearm = 1, SlotHead = 2, SlotBody = 3, SlotLegs = 4, SlotAcc0 = 5;
		public const int SlotAmmo = SlotAcc0 + AccessorySlots;
		public const int SlotHook = SlotAmmo + 1;
		/// <summary>The extra sidearm slots follow the hook so every older lock bit keeps its place.</summary>
		public const int SlotSide2 = SlotHook + 1;
		/// <summary>The camera slot follows the extra sidearms so every older lock bit keeps its place.</summary>
		public const int SlotCamera = SlotSide2 + LevyConstants.ExtraSidearms;
		public const int SlotCount = SlotCamera + 1;

		/// <summary>One bit per slot (see Slot*): set while the player hand-placed the item there. Auto-issue never replaces a locked slot.</summary>
		public int LockMask;

		/// <summary>Item type of the thrown-weapon stack that ran dry (0 when none). The armory hands the same throwable back first; see ArmoryTE.RestockThrown.</summary>
		public int ThrownRestockType;
		/// <summary>Transient: game tick of the last thrown-restock look at an armory (throttle).</summary>
		public uint ThrownCheckTick;
		/// <summary>Transient: game tick of this soldier's next automatic weapon-upgrade look at his armory (0 = not yet scheduled). See WeaponUse.WeaponUpgradeTick.</summary>
		public uint UpgradeCheckTick;

		/// <summary>Transient (never saved): locked items a Re-arm return pulled out, so the same tick's re-issue can hand them back. See ArmoryTE.ReturnGear.</summary>
		public Item[] Reserved;
		public uint ReservedTick;

		public bool IsLocked(int slot) => slot >= 0 && slot < SlotCount && (LockMask & (1 << slot)) != 0;
		public void SetLocked(int slot, bool locked)
		{
			if (slot < 0 || slot >= SlotCount) return;
			if (locked) LockMask |= 1 << slot; else LockMask &= ~(1 << slot);
		}

		/// <summary>The item in a slot (never null), or null for an out-of-range slot.</summary>
		public Item Get(int slot)
		{
			if (slot == SlotWeapon) return Weapon ??= new Item();
			if (slot == SlotSidearm) return Sidearm ??= new Item();
			if (slot == SlotHead) return Head ??= new Item();
			if (slot == SlotBody) return Body ??= new Item();
			if (slot == SlotLegs) return Legs ??= new Item();
			if (slot == SlotAmmo) return Ammo ??= new Item();
			if (slot == SlotHook) return Hook ??= new Item();
			if (slot == SlotCamera) return Camera ??= new Item();
			int a = slot - SlotAcc0;
			if (a >= 0 && a < AccessorySlots) return Accessories[a] ??= new Item();
			int x = slot - SlotSide2;
			if (x >= 0 && x < ExtraSidearms.Length) return ExtraSidearms[x] ??= new Item();
			return null;
		}

		/// <summary>Raw replace of a slot's item instance; does not touch the lock.</summary>
		public void Set(int slot, Item item)
		{
			item ??= new Item();
			if (slot == SlotWeapon) Weapon = item;
			else if (slot == SlotSidearm) Sidearm = item;
			else if (slot == SlotHead) Head = item;
			else if (slot == SlotBody) Body = item;
			else if (slot == SlotLegs) Legs = item;
			else if (slot == SlotAmmo) Ammo = item;
			else if (slot == SlotHook) Hook = item;
			else if (slot == SlotCamera) Camera = item;
			else { int a = slot - SlotAcc0; if (a >= 0 && a < AccessorySlots) Accessories[a] = item; else { int x = slot - SlotSide2; if (x >= 0 && x < ExtraSidearms.Length) ExtraSidearms[x] = item; } }
		}

		private static bool Empty(Item it) => it == null || it.IsAir || it.stack <= 0;

		private static bool IsSupportedFamily(WeaponFamily f) => f == WeaponFamily.Gun || f == WeaponFamily.Bow || f == WeaponFamily.Magic || f == WeaponFamily.Summon || f == WeaponFamily.Whip || f == WeaponFamily.Boomerang || f == WeaponFamily.Yoyo || f == WeaponFamily.Flail || f == WeaponFamily.Sentry || f == WeaponFamily.HeldWeapon || f == WeaponFamily.Thrown || f == WeaponFamily.ThrownMelee || IsMeleeFamily(f);
		/// <summary>Sidearm candidates: blades and spears, the summoner's whip, the thrown boomerang/yoyo/flail, and a minion-summoning staff or a sentry for a melee soldier.</summary>
		private static bool IsSidearmFamily(WeaponFamily f) => IsMeleeFamily(f) || f == WeaponFamily.Whip || f == WeaponFamily.Boomerang || f == WeaponFamily.Yoyo || f == WeaponFamily.Flail || f == WeaponFamily.Summon || f == WeaponFamily.Sentry;
		private static bool IsMeleeFamily(WeaponFamily f) => f == WeaponFamily.Broadsword || f == WeaponFamily.Shortsword || f == WeaponFamily.Spear;

		/// <summary>True when <paramref name="item"/> may be hand-placed in <paramref name="slot"/> (air is always fine: it empties the slot). Accessory duplicates are rejected.</summary>
		public bool CanHold(int slot, Item item)
		{
			if (Empty(item)) return slot >= 0 && slot < SlotCount;
			if (slot == SlotAmmo) return item.ammo > 0 && !item.IsACoin;
			if (slot == SlotHook) return global::Levy.Common.Movement.HookInfo.IsHookItem(item) && item.stack <= 1;
			if (slot == SlotCamera) return item.ModItem is global::Levy.Content.Items.Observe.GoProItem && item.stack <= 1;
			// Only a thrown consumable may be a stack: it is carried (and used up) as one. Every other weapon is a single item.
			if (item.stack > 1 && !(slot == SlotWeapon && WeaponClassifier.Classify(item) == WeaponFamily.Thrown)) return false;
			if (slot == SlotWeapon) return WeaponClassifier.IsIssuableWeapon(item) && IsSupportedFamily(WeaponClassifier.Classify(item));
			if (slot == SlotSidearm) return WeaponClassifier.IsIssuableWeapon(item) && IsSidearmFamily(WeaponClassifier.Classify(item));
			if (slot >= SlotSide2) return slot < SlotCount && WeaponClassifier.IsIssuableWeapon(item) && WeaponClassifier.Classify(item) is WeaponFamily.Summon or WeaponFamily.Sentry; // an extra sidearm is a minion staff or a sentry staff: it runs without being in hand
			if (slot == SlotHead) return WeaponClassifier.IsArmorPiece(item, 0) && !item.vanity;
			if (slot == SlotBody) return WeaponClassifier.IsArmorPiece(item, 1) && !item.vanity;
			if (slot == SlotLegs) return WeaponClassifier.IsArmorPiece(item, 2) && !item.vanity;
			int a = slot - SlotAcc0;
			if (a < 0 || a >= AccessorySlots) return false;
			if (!WeaponClassifier.IsUsefulAccessory(item)) return false;
			for (int i = 0; i < AccessorySlots; i++) if (i != a && !Empty(Accessories[i]) && Accessories[i].type == item.type) return false;
			return true;
		}

		/// <summary>
		/// Hand-equip: puts <paramref name="item"/> (the item the player just placed, or air to take the slot's item out) into a slot,
		/// locking it against auto-issue while it holds something and unlocking it when emptied. An ammo stack above the pouch limit is
		/// trimmed and the excess returned in <paramref name="overflow"/>. False (nothing changed) when the slot is out of range for the
		/// soldier's grade or the item does not belong there.
		/// </summary>
		public bool TrySet(int slot, Item item, int accessorySlotsAllowed, out Item overflow)
		{
			overflow = null;
			if (slot < 0 || slot >= SlotCount) return false;
			if (slot >= SlotAcc0 && slot < SlotAmmo && slot - SlotAcc0 >= accessorySlotsAllowed) return false;
			if (!CanHold(slot, item)) return false;
			item = Empty(item) ? new Item() : item;
			if (slot == SlotAmmo && item.stack > LevyConstants.AmmoPouchMax)
			{
				overflow = item.Clone(); overflow.stack = item.stack - LevyConstants.AmmoPouchMax;
				item.stack = LevyConstants.AmmoPouchMax;
			}
			if (slot == SlotWeapon && item.stack > LevyConstants.ThrownCarryMax && WeaponClassifier.Classify(item) == WeaponFamily.Thrown)
			{
				overflow = item.Clone(); overflow.stack = item.stack - LevyConstants.ThrownCarryMax;
				item.stack = LevyConstants.ThrownCarryMax;
			}
			Set(slot, item);
			SetLocked(slot, !item.IsAir);
			if (slot == SlotWeapon) ThrownRestockType = 0;
			return true;
		}

		/// <summary>True when <paramref name="item"/> may sit in vanity dye slot <paramref name="dyeSlot"/> (air always may). Only a dye fits.</summary>
		public static bool CanHoldDye(int dyeSlot, Item item)
		{
			if (dyeSlot < 0 || dyeSlot >= DyeSlots) return false;
			return Empty(item) || item.dye > 0;
		}

		/// <summary>
		/// Hand-places a dye (or air, to take it out). One dye is kept; the rest of a stack comes back in <paramref name="overflow"/>.
		/// Dyes take no lock: the Armory never issues, replaces or removes them, so nothing can undo the choice.
		/// A dye over an accessory slot the soldier's grade does not open is refused.
		/// </summary>
		public bool TrySetDye(int dyeSlot, Item item, int accessorySlotsAllowed, out Item overflow)
		{
			overflow = null;
			if (!CanHoldDye(dyeSlot, item)) return false;
			if (dyeSlot >= 3 && dyeSlot - 3 >= accessorySlotsAllowed) return false;
			item = Empty(item) ? new Item() : item;
			if (item.stack > 1) { overflow = item.Clone(); overflow.stack = item.stack - 1; item.stack = 1; }
			Dyes[dyeSlot] = item;
			return true;
		}

		/// <summary>Writes the SoldierDyeSet payload after the packet type byte: int soldierId, byte dyeSlot, item with stack.</summary>
		public static void WriteDyePayload(BinaryWriter w, int soldierId, int dyeSlot, Item item)
		{
			w.Write(soldierId); w.Write((byte)dyeSlot);
			ItemIO.Send(item ?? new Item(), w, writeStack: true);
		}

		/// <summary>Reads what <see cref="WriteDyePayload"/> wrote.</summary>
		public static void ReadDyePayload(BinaryReader r, out int soldierId, out int dyeSlot, out Item item)
		{
			soldierId = r.ReadInt32(); dyeSlot = r.ReadByte();
			item = ItemIO.Receive(r, readStack: true);
		}

		private static Item[] NewArray(int n) { var a = new Item[n]; for (int i = 0; i < n; i++) a[i] = new Item(); return a; }

		public bool HasWeapon => Weapon != null && !Weapon.IsAir;
		public bool HasSidearm => Sidearm != null && !Sidearm.IsAir;

		/// <summary>The slot number (for <see cref="Get"/>) of sidearm <paramref name="i"/>: 0 is the hand sidearm, then the extras.</summary>
		public static int SidearmSlotAt(int i) => i == 0 ? SlotSidearm : SlotSide2 + i - 1;
		/// <summary>Every sidearm slot's item, hand sidearm first, never null (air when empty).</summary>
		public IEnumerable<Item> Sidearms()
		{
			yield return Sidearm ??= new Item();
			for (int i = 0; i < ExtraSidearms.Length; i++) yield return ExtraSidearms[i] ??= new Item();
		}
		/// <summary>How many weapons he carries besides his primary.</summary>
		public int SidearmCount { get { int n = 0; foreach (var s in Sidearms()) if (!Empty(s)) n++; return n; } }
		public bool HasAmmo => Ammo != null && !Ammo.IsAir && Ammo.stack > 0;
		public bool HasHook => Hook != null && !Hook.IsAir;
		public bool HasCamera => Camera != null && !Camera.IsAir;

		/// <summary>All non-air items, for dropping or returning to the armory.</summary>
		public IEnumerable<Item> AllItems()
		{
			if (!Weapon.IsAir) yield return Weapon;
			if (!Sidearm.IsAir) yield return Sidearm;
			foreach (var x in ExtraSidearms) if (x != null && !x.IsAir) yield return x;
			if (!Head.IsAir) yield return Head;
			if (!Body.IsAir) yield return Body;
			if (!Legs.IsAir) yield return Legs;
			foreach (var a in Accessories) if (!a.IsAir) yield return a;
			foreach (var d in Dyes) if (!d.IsAir) yield return d;
			if (HasAmmo) yield return Ammo;
			if (HasHook) yield return Hook;
			if (HasCamera) yield return Camera;
		}

		public void Clear()
		{
			LockMask = 0; Reserved = null; ThrownRestockType = 0;
			Weapon = new Item(); Sidearm = new Item(); Head = new Item(); Body = new Item(); Legs = new Item(); Ammo = new Item(); Hook = new Item(); Camera = new Item();
			Accessories = NewArray(AccessorySlots); Dyes = NewArray(DyeSlots); ExtraSidearms = NewArray(LevyConstants.ExtraSidearms);
		}

		public SoldierGear Clone()
		{
			var g = new SoldierGear
			{
				LockMask = LockMask, ThrownRestockType = ThrownRestockType,
				Weapon = Weapon.Clone(), Sidearm = Sidearm.Clone(), Head = Head.Clone(), Body = Body.Clone(), Legs = Legs.Clone(), Ammo = Ammo.Clone(), Hook = (Hook ?? new Item()).Clone(), Camera = (Camera ?? new Item()).Clone(),
			};
			for (int i = 0; i < AccessorySlots; i++) g.Accessories[i] = Accessories[i].Clone();
			for (int i = 0; i < DyeSlots; i++) g.Dyes[i] = Dyes[i].Clone();
			for (int i = 0; i < ExtraSidearms.Length; i++) g.ExtraSidearms[i] = (ExtraSidearms[i] ?? new Item()).Clone();
			return g;
		}

		public TagCompound Save()
		{
			var tag = new TagCompound
			{
				["weapon"] = ItemIO.Save(Weapon), ["sidearm"] = ItemIO.Save(Sidearm), ["head"] = ItemIO.Save(Head), ["body"] = ItemIO.Save(Body),
				["legs"] = ItemIO.Save(Legs), ["ammo"] = ItemIO.Save(Ammo),
			};
			if (HasHook) tag["hook"] = ItemIO.Save(Hook);
			if (HasCamera) tag["cam"] = ItemIO.Save(Camera); // absent in older saves: loads empty (a GoPro in an accessory slot moves here on load)
			bool anyExtra = false; foreach (var x in ExtraSidearms) if (!Empty(x)) anyExtra = true;
			if (anyExtra) { var ex = new List<TagCompound>(); foreach (var x in ExtraSidearms) ex.Add(ItemIO.Save(x ?? new Item())); tag["sides"] = ex; } // absent in older saves: loads empty
			var acc = new List<TagCompound>(); foreach (var a in Accessories) acc.Add(ItemIO.Save(a)); tag["acc"] = acc;
			var dyes = new List<TagCompound>(); foreach (var d in Dyes) dyes.Add(ItemIO.Save(d)); tag["dyes"] = dyes;
			if (ThrownRestockType > 0) tag["thrownType"] = ThrownRestockType;
			if (LockMask != 0) tag["locks"] = LockMask; // absent in old saves: loads as 0 (nothing locked)
			tag["slotCount"] = SlotCount; // lock bits shifted when accessory slots were added; this tells the loader which layout the mask is in
			return tag;
		}

		public static SoldierGear Load(TagCompound tag)
		{
			var g = new SoldierGear();
			if (tag == null) return g;
			g.Weapon = ItemIO.Load(tag.GetCompound("weapon")); g.Sidearm = ItemIO.Load(tag.GetCompound("sidearm"));
			g.Head = ItemIO.Load(tag.GetCompound("head")); g.Body = ItemIO.Load(tag.GetCompound("body")); g.Legs = ItemIO.Load(tag.GetCompound("legs"));
			g.Ammo = ItemIO.Load(tag.GetCompound("ammo"));
			if (tag.ContainsKey("hook")) g.Hook = ItemIO.Load(tag.GetCompound("hook"));
			if (tag.ContainsKey("cam")) g.Camera = ItemIO.Load(tag.GetCompound("cam"));
			if (tag.ContainsKey("sides")) { var ex = tag.GetList<TagCompound>("sides"); for (int i = 0; i < g.ExtraSidearms.Length && i < ex.Count; i++) g.ExtraSidearms[i] = ItemIO.Load(ex[i]); }
			var acc = tag.GetList<TagCompound>("acc"); for (int i = 0; i < AccessorySlots && i < acc.Count; i++) g.Accessories[i] = ItemIO.Load(acc[i]);
			var dyes = tag.GetList<TagCompound>("dyes"); for (int i = 0; i < DyeSlots && i < dyes.Count; i++) g.Dyes[i] = ItemIO.Load(dyes[i]);
			g.ThrownRestockType = tag.GetInt("thrownType");
			int locks = tag.GetInt("locks");
			if (!tag.ContainsKey("slotCount"))
			{
				// Saved with five accessory slots: the ammo lock bit sat at SlotAcc0 + 5 and now belongs to the sixth accessory.
				int oldAmmoBit = 1 << (SlotAcc0 + LegacyAccessorySlots);
				if ((locks & oldAmmoBit) != 0) locks = (locks & ~oldAmmoBit) | (1 << SlotAmmo);
			}
			g.LockMask = locks & ((1 << SlotCount) - 1);
			g.MigrateCamera();
			for (int s = 0; s < SlotCount; s++) if (g.IsLocked(s) && g.Get(s).IsAir) g.SetLocked(s, false); // a lock on an empty slot means nothing
			return g;
		}

		/// <summary>A GoPro sitting in an accessory slot moves to the empty camera slot (with its hand-assigned lock), freeing the accessory slot. A second GoPro stays where it is.</summary>
		public void MigrateCamera()
		{
			if (HasCamera) return;
			for (int i = 0; i < Accessories.Length; i++)
			{
				var a = Accessories[i];
				if (Empty(a) || !(a.ModItem is global::Levy.Content.Items.Observe.GoProItem)) continue;
				Camera = a; Accessories[i] = new Item();
				if (IsLocked(SlotAcc0 + i)) { SetLocked(SlotAcc0 + i, false); SetLocked(SlotCamera, true); }
				return;
			}
		}

		/// <summary>Visual-only sync (type + prefix) for clients that draw the body.</summary>
		public void WriteVisual(BinaryWriter w)
		{
			void W(Item it) { w.Write(it.type); w.Write((ushort)it.prefix); }
			W(Weapon); W(Sidearm); W(Head); W(Body); W(Legs);
			foreach (var a in Accessories) W(a);
			foreach (var d in Dyes) W(d);
			w.Write(Ammo.type); w.Write(Ammo.stack);
			w.Write((ushort)LockMask);
			foreach (var x in ExtraSidearms) W(x ?? new Item()); // appended last so the earlier layout is unchanged
			W(Camera ?? new Item());
		}

		public void ReadVisual(BinaryReader r)
		{
			Item R() { var it = new Item(); int t = r.ReadInt32(); int p = r.ReadUInt16(); if (t > 0) { it.SetDefaults(t); if (p > 0) it.Prefix(p); } return it; }
			Weapon = R(); Sidearm = R(); Head = R(); Body = R(); Legs = R();
			for (int i = 0; i < AccessorySlots; i++) Accessories[i] = R();
			for (int i = 0; i < DyeSlots; i++) Dyes[i] = R();
			int at = r.ReadInt32(); int ast = r.ReadInt32(); Ammo = new Item(); if (at > 0) { Ammo.SetDefaults(at); Ammo.stack = ast; }
			LockMask = r.ReadUInt16();
			for (int i = 0; i < ExtraSidearms.Length; i++) ExtraSidearms[i] = R();
			Camera = R();
		}
	}
}
