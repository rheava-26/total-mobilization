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
using Levy.Common.Flavor;
using Levy.Common.Integrations;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.Tiles;

namespace Levy.Content.TileEntities
{
	/// <summary>1000-slot storage plus issue rows. Soldiers take from issue slots first, storage second, best-first by damage.</summary>
	public class ArmoryTE : ModTileEntity
	{
		/// <summary>Base bulk storage slots (the panel scrolls). Sized so a full hoard of non-stacking gear (40+ armor sets, hundreds of weapons, ammo) never overflows. Each linked Logistical Center adds <see cref="LevyConstants.LogisticsSlotsPerCenter"/> more (see <see cref="FitStorage"/>).</summary>
		public const int StorageSize = 1000, WeaponSlots = 12, ArmorSets = 4, AmmoSlots = 6;
		/// <summary>Base potion row length: slot 0 healing / mana, slot 1 the army's one buff. The Guide to Firepower adds <see cref="LevyConstants.FirepowerBuffSlots"/> more buff slots (see <see cref="FitPotions"/>).</summary>
		public const int PotionSlots = 2;
		/// <summary>Armor pieces per set: head, body, legs.</summary>
		public const int ArmorParts = 3;
		public const int ArmorSlots = ArmorSets * ArmorParts;
		/// <summary>The sidearm slot is always the last weapon slot.</summary>
		public const int SidearmSlot = WeaponSlots - 1;
		/// <summary>Weapon slot count of worlds saved before the issue rows were enlarged (sidearm was the last of these).</summary>
		private const int LegacyWeaponSlots = 6;

		/// <summary>
		/// Bulk storage. Its length is <see cref="CapacityFor"/> the linked Logistical Centers (never below the base), so the array is REPLACED when it grows or shrinks: never cache it across a tick
		/// (the panel rebuilds its cells when the reference changes; saving, packets and every loop already read <c>.Length</c>).
		/// </summary>
		public Item[] Storage = NewItems(StorageSize);
		public Item[] IssueWeapons = NewItems(WeaponSlots);
		/// <summary>ArmorSets x (head, body, legs): index = set*ArmorParts + part.</summary>
		public Item[] IssueArmor = NewItems(ArmorSlots);
		public Item[] IssueAmmo = NewItems(AmmoSlots);
		/// <summary>Slot 0 = healing / mana, every later slot = a buff slot. Length is <see cref="TargetPotionSlots"/>; it only ever GROWS (Firepower never turns off), by replacing the array, so never cache it across a tick.</summary>
		public Item[] IssuePotions = NewItems(PotionSlots);

		private bool _dirty;

		public static Item[] NewItems(int n) { var a = new Item[n]; for (int i = 0; i < n; i++) a[i] = new Item(); return a; }

		// ------------------------------------------------------------------ expandable storage (Logistical Centers)

		/// <summary>Length the potion row should have in this world: the base plus Firepower's buff slots.</summary>
		public static int TargetPotionSlots => PotionSlots + GuideSystem.ArmoryBuffSlots;

		/// <summary>
		/// Server / single player: grows <see cref="IssuePotions"/> to <see cref="TargetPotionSlots"/>, moving the items by reference (a save written with two slots grows the same way). Never shrinks.
		/// Returns true (and queues a sync) when the length changed.
		/// </summary>
		public bool FitPotions()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;
			int want = TargetPotionSlots;
			if (IssuePotions.Length >= want) return false;
			var next = new Item[want];
			Array.Copy(IssuePotions, next, IssuePotions.Length);
			for (int i = IssuePotions.Length; i < want; i++) next[i] = new Item();
			IssuePotions = next;
			MarkDirty();
			return true;
		}

		/// <summary>Storage slots with <paramref name="centers"/> Logistical Centers linked.</summary>
		public static int CapacityFor(int centers) => StorageSize + Math.Max(0, centers) * LevyConstants.LogisticsSlotsPerCenter;

		/// <summary>Logistical Centers currently linked to this armory (each is linked to its nearest armory in reach). Cached; server, client and single player agree because every entity exists everywhere.</summary>
		public int LinkedCenters => LogisticsSystem.CentersFor(this);

		/// <summary>The last occupied storage index + 1 (0 when storage is empty): the shortest the array may be without losing an item.</summary>
		public int StorageUsedLength
		{
			get
			{
				for (int i = Storage.Length - 1; i >= 0; i--) if (!IsAir(Storage[i])) return i + 1;
				return 0;
			}
		}

		/// <summary>
		/// Server / single player: sizes <see cref="Storage"/> to <paramref name="centers"/> centers. Growing appends empty slots. Shrinking removes only trailing EMPTY slots: if an item
		/// sits past the new capacity the array stays just long enough to hold it (Sort packs items to the front, and the next reconcile then trims). Items move by reference, so none is
		/// ever cloned, dropped or duplicated. Returns true (and queues a sync) when the length changed.
		/// </summary>
		public bool FitStorage(int centers)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;
			int want = Math.Max(CapacityFor(centers), StorageUsedLength);
			if (want == Storage.Length) return false;
			var next = new Item[want];
			int keep = Math.Min(want, Storage.Length);
			Array.Copy(Storage, next, keep);
			for (int i = keep; i < want; i++) next[i] = new Item();
			Storage = next;
			MarkDirty();
			return true;
		}
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

		private static bool IsMeleeFamily(WeaponFamily f) =>
			f == WeaponFamily.Broadsword || f == WeaponFamily.Shortsword || f == WeaponFamily.Spear;

		/// <summary>Every melee-class primary (blades, spears, the thrown boomerang, the whip): none of them needs a fallback blade in the off hand.</summary>
		private static bool IsMeleeTypeFamily(WeaponFamily f) => IsMeleeFamily(f) || f == WeaponFamily.Boomerang || f == WeaponFamily.Whip || f == WeaponFamily.Yoyo || f == WeaponFamily.Flail || f == WeaponFamily.ThrownMelee;

		/// <summary>A weapon the armory may hand out: the classifier accepts it (cached per item type, so scanning a 1000-slot hoard stays cheap).</summary>
		private static bool IsIssuable(Item it) => !IsAir(it) && WeaponClassifier.IsIssuableWeapon(it);

		private static bool IsIssuableMelee(Item it) => IsIssuable(it) && IsMeleeFamily(WeaponClassifier.GetProfile(it).Family);

		/// <summary>True when the weapon needs no ammo, the soldier's pouch already holds its ammo, or the armory has some (<paramref name="onHand"/>: ammo ids in the issue ammo row and storage).</summary>
		private static bool HasAmmoFor(Item weapon, SoldierRecord soldier, HashSet<int> onHand)
		{
			int ammoId = weapon.useAmmo;
			if (ammoId <= 0) return true;
			var pouch = soldier?.Gear?.Ammo;
			if (!IsAir(pouch) && pouch.ammo == ammoId) return true;
			return onHand.Contains(ammoId);
		}

		/// <summary>Every ammo id the armory currently holds (issue ammo row + storage), so per-weapon ammo checks are O(1) instead of rescanning 1000 slots.</summary>
		private HashSet<int> AmmoIdsOnHand()
		{
			var set = new HashSet<int>();
			foreach (var it in IssueAmmo) if (!IsAir(it) && it.ammo > 0) set.Add(it.ammo);
			foreach (var it in Storage) if (!IsAir(it) && it.ammo > 0) set.Add(it.ammo);
			foreach (var o in OtherNetworked()) foreach (var it in o.Storage) if (!IsAir(it) && it.ammo > 0) set.Add(it.ammo);
			var ms = MsHeart();
			if (ms != null) set.UnionWith(MagicStorageBridge.AmmoIds(ms));
			return set;
		}

		// ------------------------------------------------------------------ Magic Storage supply (issue-side, optional, reflection-only)

		/// <summary>The Magic Storage Heart this armory may draw from, or null (MS absent / toggle off / client / none in range). Zero cost when MS is not installed.</summary>
		private object MsHeart()
		{
			if (!MagicStorageBridge.Enabled) return null;
			return MagicStorageBridge.FindHeart(Center, LevyConstants.MagicStorageRadius);
		}

		/// <summary>Withdraws the best-scoring stored item passing <paramref name="pred"/> from the MS network; the returned item is exactly what MS gave up (the caller equips it). Null if nothing was withdrawn.</summary>
		private Item MsTakeBest(Func<Item, bool> pred, Func<Item, float> score)
		{
			var heart = MsHeart();
			if (heart == null) return null;
			return MagicStorageBridge.TryWithdrawMatch(heart, pred, 1, out var got, score) ? got : null;
		}

		/// <summary>Varied weapon pick over the MS network's items, then a single-item withdraw of the chosen one. Null if nothing was withdrawn.</summary>
		private Item MsTakeVaried(Func<Item, bool> pred, SoldierRecord who)
		{
			var heart = MsHeart();
			if (heart == null) return null;
			var arr = MagicStorageBridge.Snapshot(heart).ToArray();
			if (arr.Length == 0 || !TryPickVaried(arr, pred, who, 0, arr.Length, out int idx)) return null;
			return MagicStorageBridge.TryWithdrawExact(heart, arr[idx], 1, out var got) ? got : null;
		}

		/// <summary>Tops the pouch up from the MS network. Withdraws only what fits, then adds exactly the withdrawn count (any surplus or mismatch is dropped at the armory, never destroyed). Returns the amount added to the pouch.</summary>
		private int MsTakeAmmo(int ammoId, Item pouch, int max)
		{
			var heart = MsHeart();
			if (heart == null) return 0;
			int have = IsAir(pouch) ? 0 : pouch.stack;
			Item cand = null;
			foreach (var it in MagicStorageBridge.Snapshot(heart)) {
				if (IsAir(it) || it.ammo != ammoId || it.IsACoin) continue;
				if (!IsAir(pouch) && pouch.type != it.type) continue;
				if (cand == null || it.stack > cand.stack) cand = it;
			}
			if (cand == null) return 0;
			int room = Math.Min(max, IsAir(pouch) ? cand.maxStack : pouch.maxStack) - have;
			if (room <= 0) return 0;
			if (!MagicStorageBridge.TryWithdrawExact(heart, cand, room, out var got)) return 0;
			int added = 0;
			try {
				if (IsAir(pouch)) { pouch.SetDefaults(got.type); pouch.stack = 0; }
				if (pouch.type == got.type) {
					added = Math.Min(got.stack, Math.Max(0, pouch.maxStack - pouch.stack));
					pouch.stack += added;
				}
			}
			catch (Exception) { added = 0; }
			if (added < got.stack) { // never destroy withdrawn ammo: whatever did not land in the pouch is dropped at the armory
				var rest = got.Clone(); rest.stack = got.stack - added;
				Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("LevyMagicStorage"), Center, rest);
			}
			return added;
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

		private static readonly List<ArmoryTE> NoOthers = new();

		/// <summary>
		/// The other armories sharing this one's pooled gear (pylon-networked, see <see cref="SettlementSystem.NetworkedArmories"/>), nearest first.
		/// Empty when this armory is not on the network, on a multiplayer client (the server owns item movement), or when no other armory is live.
		/// Issue-side only: callers read their Storage and must remove through <see cref="TakeSlot"/> on THAT armory.
		/// </summary>
		private List<ArmoryTE> OtherNetworked()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return NoOthers;
			var sys = SettlementSystem.Instance;
			if (sys == null) return NoOthers;
			var all = sys.NetworkedArmories(this);
			if (all == null || all.Count <= 1) return NoOthers;
			var res = new List<ArmoryTE>(all.Count - 1);
			foreach (var a in all) {
				if (a == null || ReferenceEquals(a, this) || a.Storage == null) continue;
				if (!ByID.TryGetValue(a.ID, out var live) || !ReferenceEquals(live, a)) continue; // gone since the cache was built
				res.Add(a);
			}
			return res.Count == 0 ? NoOthers : res;
		}

		/// <summary>Removes the real Item in <paramref name="arr"/>[idx] of THIS armory (which must own <paramref name="arr"/>), marking this armory dirty. Null (nothing changed) if the slot is invalid or empty.</summary>
		private Item TakeSlot(Item[] arr, int idx)
		{
			if (arr == null || idx < 0 || idx >= arr.Length || IsAir(arr[idx])) return null;
			return Remove(arr, idx);
		}

		/// <summary>Issue slots first (indices issueFrom..issueTo step issueStep), bulk storage second; best-first within each.</summary>
		private Item TakeBest(Item[] issue, int issueFrom, int issueTo, int issueStep, Func<Item, bool> userPred, Func<Item, float> score)
		{
			// Items a Re-arm just pulled off a soldier with a hand-assigned slot are held for that soldier until the same tick ends.
			Func<Item, bool> pred = it => !IsReservedRef(it) && userPred(it);
			if (issue != null && TryBest(issue, pred, score, out int idx, issueFrom, issueTo, issueStep)) return Remove(issue, idx);
			if (TryBest(Storage, pred, score, out idx)) return Remove(Storage, idx);
			// Spill over into pylon-networked armories' storage; each honours its OWN re-arm reservations and loses the item from its OWN slot.
			foreach (var o in OtherNetworked()) {
				var src = o;
				Func<Item, bool> opred = it => !src.IsReservedRef(it) && userPred(it);
				if (TryBest(src.Storage, opred, score, out idx)) { var taken = src.TakeSlot(src.Storage, idx); if (taken != null) return taken; }
			}
			return MsTakeBest(userPred, score); // last resort: a connected Magic Storage network
		}

		/// <summary>Tier first, defense second: rarity tier and value outweigh a few points of flat defense, so progression gear (Crimtane) beats flat-defense metal (Platinum).</summary>
		private static float ArmorScore(Item it) =>
			WeaponClassifier.RarityTier(it.rare) * LevyConstants.ArmorRarityWeight
			+ it.defense * LevyConstants.ArmorDefenseWeight
			+ (float)Math.Log10(1.0 + Math.Max(0, it.value)) * LevyConstants.ArmorValueWeight;

		/// <summary>Strength of an accessory: rarity tier first, then defense, value (which carries the prefix's worth), a flat bonus for any prefix, and how much damage it adds to the soldier's class.</summary>
		private static float AccessoryScore(Item it, DamageClass cls) =>
			WeaponClassifier.RarityTier(it.rare) * LevyConstants.AccessoryRarityWeight
			+ it.defense * LevyConstants.AccessoryDefenseWeight
			+ (float)Math.Log10(1.0 + Math.Max(0, it.value)) * LevyConstants.AccessoryValueWeight
			+ (it.prefix > 0 ? LevyConstants.AccessoryPrefixBonus : 0f)
			+ ArmorDoctrine.AccessoryClassFit(it, cls) * LevyConstants.AccessoryClassFitWeight
			+ Math.Max(0, AccessoryAggro.Of(it)) * LevyConstants.Targeting.AccessoryAggroWeight; // taunt gear leans toward the soldiers who tank for the player

		/// <summary>Vanilla accessory rules: never two of one type, never two pairs of wings, and whatever the mods forbid pairing.</summary>
		private static bool CanWearWith(Item[] worn, Item it)
		{
			if (worn == null) return true;
			for (int i = 0; i < worn.Length; i++) {
				var w = worn[i];
				if (IsAir(w)) continue;
				if (w.type == it.type) return false;
				if (w.wingSlot > 0 && it.wingSlot > 0) return false;
				try { if (!ItemLoader.CanAccessoryBeEquippedWith(w, it)) return false; } catch (Exception) { }
			}
			return true;
		}

		// ------------------------------------------------------------------ matching armor sets

		/// <summary>The armor's family: the first word of its name ("Hallowed Mask" and "Hallowed Plate Mail" both read "Hallowed"); "Ancient X ..." keeps two words.</summary>
		private static string ArmorStem(Item it)
		{
			var t = (it.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (t.Length == 0) return "";
			return t.Length >= 3 && t[0] == "Ancient" ? t[0] + " " + t[1] : t[0];
		}

		private struct ArmorCand { public ArmoryTE Src; public Item[] Arr; public int Idx; public Item It; public float Score; public string Stem; public bool Fixed; public bool Ms; public bool Worn; }

		// ------------------------------------------------------------------ hand-assigned gear across a Re-arm

		// Re-arm = ReturnGear for the whole muster, then IssueGear for each soldier, all inside one tick. ReturnGear parks the
		// hand-assigned (locked) items in storage like everything else but remembers them on the soldier (SoldierGear.Reserved) and
		// here; IssueGear hands the same instances back first, and TakeBest skips them meanwhile so nobody else draws them.
		// Discharge / stand-down never issue in the same tick, so the reservation lapses and the items simply stay in the armory.
		private readonly List<Item> _reservedRefs = new();
		private uint _reservedTick;

		private bool IsReservedRef(Item it)
		{
			if (_reservedRefs.Count == 0) return false;
			if (_reservedTick != Main.GameUpdateCount) { _reservedRefs.Clear(); return false; }
			for (int i = 0; i < _reservedRefs.Count; i++) if (ReferenceEquals(_reservedRefs[i], it)) return true;
			return false;
		}

		private void Reserve(Item it)
		{
			if (_reservedTick != Main.GameUpdateCount) { _reservedRefs.Clear(); _reservedTick = Main.GameUpdateCount; }
			_reservedRefs.Add(it);
		}

		private static int IndexOfRef(Item[] arr, Item it) { for (int i = 0; i < arr.Length; i++) if (ReferenceEquals(arr[i], it)) return i; return -1; }

		/// <summary>Gives a soldier back what ReturnGear took from his locked slots earlier in the same tick (a Re-arm). Locks are restored.</summary>
		private bool ReclaimLocked(SoldierGear g, int accessorySlots)
		{
			var kept = g.Reserved; uint tick = g.ReservedTick;
			g.Reserved = null;
			if (kept == null || tick != Main.GameUpdateCount) return false;
			bool any = false;
			for (int s = 0; s < SoldierGear.SlotCount && s < kept.Length; s++) {
				var item = kept[s];
				if (IsAir(item)) continue;
				if (s >= SoldierGear.SlotAcc0 && s < SoldierGear.SlotAmmo && s - SoldierGear.SlotAcc0 >= accessorySlots) continue;
				if (!IsAir(g.Get(s))) continue;
				if (IsStackSlot(s, item)) {
					// Stackable (ammo pouch, a thrown-weapon stack): it may have merged into a bigger stack, so take the same type back by count.
					var pouch = new Item(); pouch.SetDefaults(item.type); pouch.stack = 0;
					int want = Math.Min(item.stack, s == SoldierGear.SlotAmmo ? LevyConstants.AmmoPouchMax : LevyConstants.ThrownCarryMax);
					int moved = DrainType(s == SoldierGear.SlotAmmo ? IssueAmmo : IssueWeapons, item.type, pouch, want);
					if (moved < want) moved += DrainType(Storage, item.type, pouch, want - moved);
					if (moved > 0) { g.Set(s, pouch); g.SetLocked(s, true); any = true; }
					continue;
				}
				int idx = IndexOfRef(Storage, item);
				if (idx >= 0) { g.Set(s, Remove(Storage, idx)); g.SetLocked(s, true); any = true; continue; }
				idx = IndexOfRef(IssueWeapons, item);
				if (idx >= 0) { g.Set(s, Remove(IssueWeapons, idx)); g.SetLocked(s, true); any = true; continue; }
				idx = IndexOfRef(IssueArmor, item);
				if (idx >= 0) { g.Set(s, Remove(IssueArmor, idx)); g.SetLocked(s, true); any = true; }
			}
			if (any) MarkDirty();
			return any;
		}

		/// <summary>Slots whose item is a stack that merges into storage on return, so a Re-arm must take it back by count: the ammo pouch and a thrown-weapon stack.</summary>
		private static bool IsStackSlot(int slot, Item it) => slot == SoldierGear.SlotAmmo || (slot == SoldierGear.SlotWeapon && it != null && it.maxStack > 1);

		private static int DrainType(Item[] arr, int type, Item pouch, int max)
		{
			int moved = 0;
			for (int i = 0; i < arr.Length && moved < max; i++) {
				var it = arr[i];
				if (IsAir(it) || it.type != type) continue;
				int room = Math.Min(max - moved, pouch.maxStack - pouch.stack);
				if (room <= 0) break;
				int n = Math.Min(room, it.stack);
				pouch.stack += n; it.stack -= n; moved += n;
				if (it.stack <= 0) arr[i] = new Item();
			}
			return moved;
		}

		// ------------------------------------------------------------------ issue API

		/// <summary>
		/// A strong issuable weapon for this soldier (family must be supported); removes it from the armory. Null if none.
		/// Draws from the top tier of viable weapon types (see <see cref="TryPickVaried"/>), so a varied armory yields a mixed squad.
		/// </summary>
		public virtual Item TakeBestWeapon(SoldierRecord forSoldier) => TakeBestWeaponWhere(forSoldier, null);

		/// <summary><see cref="TakeBestWeapon"/> restricted by <paramref name="extra"/> (null = anything issuable).</summary>
		private Item TakeBestWeaponWhere(SoldierRecord forSoldier, Func<Item, bool> extra)
		{
			// Pass 1: weapons that can actually fire (no ammo needed, or ammo in the pouch / armory). Pass 2: anything issuable.
			var onHand = AmmoIdsOnHand();
			var rule = PrimaryRule(forSoldier);
			return CapThrown(TakeVaried(forSoldier, it => IsIssuable(it) && (extra == null || extra(it)) && (rule == null || rule(it)) && HasAmmoFor(it, forSoldier, onHand))
				?? TakeVaried(forSoldier, it => IsIssuable(it) && (extra == null || extra(it)) && (rule == null || rule(it))));
		}

		/// <summary>Visits every unreserved item a primary pick could draw from, in <see cref="TakeVaried"/>'s reach: the weapon rows (not the dedicated sidearm slot), storage, the networked armories' storage and a connected Magic Storage network.</summary>
		private void ForEachShelfItem(Action<Item> visit)
		{
			void Consider(Item it) { if (!IsAir(it) && !IsReservedRef(it)) visit(it); }
			for (int i = 0; i < SidearmSlot; i++) Consider(IssueWeapons[i]);
			foreach (var it in Storage) Consider(it);
			foreach (var o in OtherNetworked()) foreach (var it in o.Storage) Consider(it);
			var ms = MsHeart();
			if (ms != null) foreach (var it in MagicStorageBridge.Snapshot(ms)) Consider(it);
		}

		/// <summary>True when a summon staff is stocked, or already in the soldier's hand: a whip is then only ever a sidearm, never a summoner's primary.</summary>
		private bool StaffAvailableFor(SoldierRecord who)
		{
			var held = who?.Gear?.Weapon;
			if (!IsAir(held) && IsIssuableSummonStaff(held)) return true;
			bool any = false;
			ForEachShelfItem(it => { if (!any && IsIssuableSummonStaff(it)) any = true; });
			return any;
		}

		/// <summary>
		/// True when the soldier is (or will be armed as) a summoner, so the summon-weapon reservation lets him draw whips, minion staves and sentries: he already
		/// carries a summon-class weapon, he wears summon armor, or — before any armor is issued — the only armor the armory can field him is a summon set. A soldier
		/// who already wears another class's armor is never a summoner, so a melee / ranged / magic trooper is locked out of every summon weapon.
		/// </summary>
		private bool IsSummonerSoldier(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			if (g.HasWeapon && WeaponClassifier.ItemDamageClass(g.Weapon) == DamageClass.Summon) return true; // a summon weapon in hand makes him one, whatever his armor
			var worn = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			if (worn != null) return worn == DamageClass.Summon; // armor on his back decides; another class is never a summoner
			if (Common.Config.LevyConfig.Get.ClassMatchIssue) return false; // fixed-class and no summon armor worn: not a summoner
			return ArmorableClasses(soldier, false).Contains(DamageClass.Summon); // fluid, no armor yet: a summon set he could field makes him one
		}

		/// <summary>
		/// Extra conditions every PRIMARY weapon pick shares (null = none): (1) summon-class weapons (whips, minion staves, sentries) are reserved for summoners, so a
		/// non-summoner is never handed one; (2) a summoner leads with his hard summon — while a minion staff is available his primary is a staff, never a whip or a
		/// sentry (both move to the off-hand / fallback), and with no staff he may lead with any summon weapon; (3) a ranged helmet's ammo subtype (<see cref="RangedSteer"/>).
		/// </summary>
		private Func<Item, bool> PrimaryRule(SoldierRecord who)
		{
			bool summoner = IsSummonerSoldier(who);
			bool staffFirst = summoner && StaffAvailableFor(who); // a summoner with a minion staff in reach takes the staff, not a whip or a sentry
			var steer = RangedSteer(who);
			if (summoner && !staffFirst && steer == null) return null; // a summoner with no staff may lead with any weapon; nothing else to restrict
			return it =>
			{
				var fam = WeaponClassifier.GetProfile(it).Family;
				bool summonWeapon = fam == WeaponFamily.Whip || fam == WeaponFamily.Summon || fam == WeaponFamily.Sentry;
				if (summonWeapon && !summoner) return false; // reservation: summon weapons for summoners only
				if (staffFirst && (fam == WeaponFamily.Whip || fam == WeaponFamily.Sentry)) return false; // hard summon first
				return steer == null || steer(it);
			};
		}

		/// <summary>
		/// Soft subtype steering for a ranged soldier whose worn helmet boosts one ammo subtype (<see cref="ArmorDoctrine.FavoredRangedSubtype"/>: Shroomite bows / guns / launchers).
		/// Null = no steering (no such helmet, or no weapon of the subtype worth it). Otherwise the returned test lets every non-ranged weapon through and ranged weapons only of
		/// the favoured subtype. It engages when the best favoured weapon (stocked or in hand) scores at least <see cref="LevyConstants.SubtypeSteerMinScoreFraction"/> of the best
		/// ranged weapon of any subtype, so a far stronger off-subtype weapon wins and a soldier with none of the subtype in stock keeps the best ranged weapon. The decision depends only
		/// on the shelves, his weapon in hand and his helmet, so the primary pick, the auto-upgrade and the armor reconcile all agree and never trade a weapon back and forth.
		/// </summary>
		private Func<Item, bool> RangedSteer(SoldierRecord who)
		{
			var g = who?.Gear;
			var sub = ArmorDoctrine.FavoredRangedSubtype(g?.Head);
			if (sub == RangedSubtype.None) return null;
			var onHand = AmmoIdsOnHand();
			float top = 0f, favTop = 0f;
			void Consider(Item it)
			{
				if (!IsIssuable(it) || WeaponClassifier.ItemDamageClass(it) != DamageClass.Ranged || !HasAmmoFor(it, who, onHand)) return;
				float sc = WeaponClassifier.Score(it);
				top = Math.Max(top, sc);
				if (ArmorDoctrine.FitsRangedSubtype(it, sub)) favTop = Math.Max(favTop, sc);
			}
			ForEachShelfItem(Consider);
			Consider(g.Weapon);
			if (favTop <= 0f || favTop < top * LevyConstants.SubtypeSteerMinScoreFraction) return null;
			return it => WeaponClassifier.ItemDamageClass(it) != DamageClass.Ranged || ArmorDoctrine.FitsRangedSubtype(it, sub);
		}

		/// <summary>A thrown stack leaves the shelf at most <see cref="LevyConstants.ThrownCarryMax"/> strong (what a soldier carries); the rest goes back to storage. Any other item is returned untouched.</summary>
		private Item CapThrown(Item w)
		{
			if (IsAir(w) || w.stack <= LevyConstants.ThrownCarryMax || WeaponClassifier.GetProfile(w).Family != WeaponFamily.Thrown) return w;
			var rest = w.Clone(); rest.stack = w.stack - LevyConstants.ThrownCarryMax; w.stack = LevyConstants.ThrownCarryMax;
			var left = Deposit(rest);
			if (!IsAir(left)) Spill(left, protect: true);
			return w;
		}

		/// <summary>
		/// Keeps a thrown-weapon stack supplied. A soldier holding a thrown stack below the carry limit is topped up from the shelves; one whose stack ran dry
		/// (<see cref="SoldierGear.ThrownRestockType"/>) is handed the same throwable again, as long as any is stocked. A hand-assigned (locked) empty slot keeps
		/// waiting for stock; an auto-issued one stops waiting once the shelves are bare, so a normal pick can arm him with something else. True if the gear changed.
		/// </summary>
		public virtual bool RestockThrown(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			int max = LevyConstants.ThrownCarryMax;
			if (g.HasWeapon)
			{
				g.ThrownRestockType = 0;
				if (WeaponClassifier.GetProfile(g.Weapon).Family != WeaponFamily.Thrown || g.Weapon.stack >= max) return false;
				return DrawThrown(g.Weapon, g.Weapon.type, max) > 0;
			}
			if (g.ThrownRestockType <= 0) return false;
			var fresh = new Item();
			if (DrawThrown(fresh, g.ThrownRestockType, max) > 0) { g.Weapon = fresh; g.ThrownRestockType = 0; return true; }
			if (!g.IsLocked(SoldierGear.SlotWeapon)) g.ThrownRestockType = 0;
			return false;
		}

		/// <summary>Moves up to (max - current) of <paramref name="type"/> from the issue weapon row, storage and the networked armories into <paramref name="stack"/> (air = a fresh stack). Returns the amount moved.</summary>
		private int DrawThrown(Item stack, int type, int max)
		{
			bool wasAir = IsAir(stack);
			if (wasAir) { stack.SetDefaults(type); stack.stack = 0; }
			else if (stack.type != type) return 0;
			int want = Math.Min(max, stack.maxStack) - stack.stack;
			if (want <= 0) { if (wasAir) stack.TurnToAir(); return 0; }
			int moved = DrainType(IssueWeapons, type, stack, want);
			moved += DrainType(Storage, type, stack, want - moved);
			if (moved > 0) MarkDirty();
			foreach (var o in OtherNetworked()) {
				if (moved >= want) break;
				int n = DrainType(o.Storage, type, stack, want - moved);
				if (n > 0) { o.MarkDirty(); moved += n; }
			}
			if (moved == 0 && wasAir) stack.TurnToAir();
			return moved;
		}

		/// <summary>A strong issuable weapon of the given damage class (guns/bows Ranged, staves Magic, blades/spears Melee) that can actually fire; removes it. Null if none (callers fall back to <see cref="TakeBestWeapon"/>).</summary>
		public virtual Item TakeBestWeaponOfClass(SoldierRecord forSoldier, DamageClass cls) => TakeBestWeaponOfClass(forSoldier, cls, null);

		/// <summary><see cref="TakeBestWeaponOfClass(SoldierRecord, DamageClass)"/> that also satisfies <paramref name="extra"/> (null = no extra condition).</summary>
		private Item TakeBestWeaponOfClass(SoldierRecord forSoldier, DamageClass cls, Func<Item, bool> extra)
		{
			if (cls == null) return null;
			var onHand = AmmoIdsOnHand();
			// Summoners want the summon weapon itself as primary; their whip comes through the sidearm slot.
			bool needSummon = cls == DamageClass.Summon;
			var rule = PrimaryRule(forSoldier); // a Shroomite helmet's ammo subtype (Ranged only); whips are already out for Summon (staves only)
			bool Pred(Item it) => IsIssuable(it) && WeaponClassifier.ItemDamageClass(it) == cls && HasAmmoFor(it, forSoldier, onHand) && (extra == null || extra(it)) && (rule == null || rule(it))
				&& (!needSummon || WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon);
			return CapThrown(TakeVaried(forSoldier, Pred));
		}

		/// <summary>Issue weapon slots first, bulk storage second; within each, a varied pick among the top-scoring weapon types.</summary>
		private Item TakeVaried(SoldierRecord who, Func<Item, bool> userPred)
		{
			Func<Item, bool> pred = it => !IsReservedRef(it) && userPred(it);
			if (TryPickVaried(IssueWeapons, pred, who, 0, SidearmSlot, out int idx)) return Remove(IssueWeapons, idx);
			if (TryPickVaried(Storage, pred, who, 0, Storage.Length, out idx)) return Remove(Storage, idx);
			foreach (var o in OtherNetworked()) {
				var src = o;
				Func<Item, bool> opred = it => !src.IsReservedRef(it) && userPred(it);
				if (TryPickVaried(src.Storage, opred, who, 0, src.Storage.Length, out idx)) { var taken = src.TakeSlot(src.Storage, idx); if (taken != null) return taken; }
			}
			return MsTakeVaried(userPred, who); // last resort: a connected Magic Storage network
		}

		/// <summary>
		/// Picks one weapon from <paramref name="arr"/>: the best-scoring copy of each weapon type, keep the types within
		/// <see cref="LevyConstants.WeaponVarietyTopFraction"/> of the best score (at most <see cref="LevyConstants.WeaponVarietyMaxTypes"/>), and
		/// let the soldier's stable id choose among them. Same soldier, same armory: same pick; consecutive soldiers rotate through the types.
		/// </summary>
		private static bool TryPickVaried(Item[] arr, Func<Item, bool> pred, SoldierRecord who, int from, int to, out int bestIdx)
		{
			bestIdx = -1;
			var byType = new Dictionary<int, (int idx, float score)>();
			for (int i = from; i < to && i < arr.Length; i++)
			{
				var it = arr[i];
				if (IsAir(it) || !pred(it)) continue;
				float sc = WeaponClassifier.Score(it);
				if (!byType.TryGetValue(it.type, out var cur) || sc > cur.score) byType[it.type] = (i, sc);
			}
			if (byType.Count == 0) return false;
			var list = new List<(int idx, float score)>(byType.Values);
			list.Sort((a, b) => { int c = b.score.CompareTo(a.score); return c != 0 ? c : a.idx.CompareTo(b.idx); });
			float cutoff = list[0].score * LevyConstants.WeaponVarietyTopFraction;
			int tier = 1;
			while (tier < list.Count && tier < LevyConstants.WeaponVarietyMaxTypes && list[tier].score >= cutoff) tier++;
			int key = who?.Id ?? 0;
			bestIdx = list[(int)((uint)key % (uint)tier)].idx;
			return true;
		}

		/// <summary>
		/// Best issuable sidearm: the melee item in the dedicated sidearm slot (the player's own choice), else the best LESSER blade on the shelves
		/// (<see cref="LesserBlade"/>, so the top blades stay in stock for melee primaries); removes it. Null if none.
		/// </summary>
		public virtual Item TakeSidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableMelee(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, LesserBlade(), WeaponClassifier.Score);
		}

		/// <summary>
		/// Best <see cref="WeaponClassifier.Score"/> among the issuable blades and spears that could be issued as a primary: the weapon rows (not the dedicated
		/// sidearm slot), storage, the networked armories' storage and a connected Magic Storage network. 0 when none is stocked.
		/// </summary>
		private float TopBladeScore()
		{
			float best = 0f;
			ForEachShelfItem(it => { if (IsIssuableMelee(it)) best = Math.Max(best, WeaponClassifier.Score(it)); });
			return best;
		}

		/// <summary>
		/// The fallback blade a sidearm may draw: an issuable melee weapon scoring at most <see cref="LevyConstants.SidearmScoreCapFraction"/> of the best blade in stock.
		/// Armory stock is finite, so a sidearm that took the best blade would leave the melee troopers who wield it as a primary with the leftovers; the top
		/// of the shelf is reserved for primaries. With only top-tier blades in stock the sidearm stays empty.
		/// </summary>
		private Func<Item, bool> LesserBlade()
		{
			float cap = TopBladeScore() * LevyConstants.SidearmScoreCapFraction;
			return it => IsIssuableMelee(it) && WeaponClassifier.Score(it) <= cap;
		}

		private static bool IsIssuableWhip(Item it) => IsIssuable(it) && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Whip;

		private static bool IsIssuableSummonStaff(Item it) => IsIssuable(it) && it.shoot > 0 && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon;

		private static bool IsIssuableSentry(Item it) => IsIssuable(it) && it.shoot > 0 && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Sentry;

		/// <summary>A whip in hand: the dedicated sidearm slot first, then the best whip in the racks. Removes it. Null if none in stock.</summary>
		private Item TakeWhipSidearm()
		{
			if (IsIssuableWhip(IssueWeapons[SidearmSlot]) && !IsReservedRef(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableWhip, WeaponClassifier.Score);
		}

		/// <summary>A minion-summoning staff in hand: the dedicated sidearm slot first, then the best in the racks. Removes it. Null if none in stock.</summary>
		private Item TakeSummonStaffSidearm()
		{
			if (IsIssuableSummonStaff(IssueWeapons[SidearmSlot]) && !IsReservedRef(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableSummonStaff, WeaponClassifier.Score);
		}

		/// <summary>
		/// A summoner's second weapon, given the family of his primary. A whip (his attack tool) is the ideal off-hand whenever he is not already swinging one, and a
		/// sentry-leaning kit puts the tower in hand first. Otherwise he fills a summon role he does not already field — never two weapons of the same role, never a
		/// second sentry while a staff is available — and a lesser blade is the last resort so he is never left one-handed. Removes it. Null if the armory has nothing.
		/// </summary>
		public virtual Item TakeSummonerComplement(SoldierRecord forSoldier, WeaponFamily primaryFam)
		{
			bool haveStaff = primaryFam == WeaponFamily.Summon, haveSentry = primaryFam == WeaponFamily.Sentry, haveWhip = primaryFam == WeaponFamily.Whip;
			if (ArmorDoctrine.HasSentryLean(forSoldier?.Gear?.Accessories) && !haveSentry) { var se = TakeSentrySidearm(forSoldier); if (se != null) return se; }
			if (!haveWhip) { var w = TakeWhipSidearm(); if (w != null) return w; } // the whip is the summoner's attack tool
			if (!haveStaff) { var st = TakeSummonStaffSidearm(); if (st != null) return st; } // then a summon role he does not cover yet
			if (!haveSentry) { var se = TakeSentrySidearm(forSoldier); if (se != null) return se; }
			var blade = TakeSidearm(forSoldier); // no summon weapon to spare: a lesser blade keeps an attack tool in hand
			if (blade != null) return blade;
			return haveSentry ? TakeSentrySidearm(forSoldier) : null; // only sentries in the whole armory: double up rather than go one-handed
		}

		/// <summary>A sentry-kitted soldier's sidearm: a sentry (the sidearm slot first, then the best in the armory). Removes it. Null if the armory has none.</summary>
		public virtual Item TakeSentrySidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableSentry(IssueWeapons[SidearmSlot]) && !IsReservedRef(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableSentry, WeaponClassifier.Score);
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
			var g = forSoldier?.Gear;
			var worn = g?.Accessories;
			// Best fit to the soldier's class: his weapon's, else what his armor favours.
			DamageClass cls = null;
			if (g != null && g.HasWeapon) cls = WeaponClassifier.ItemDamageClass(g.Weapon);
			if (cls == null && g != null) cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			bool Pred(Item it) => WeaponClassifier.IsUsefulAccessory(it) && CanWearWith(worn, it);
			return TakeBest(null, 0, 0, 1, Pred, it => AccessoryScore(it, cls));
		}

		/// <summary>
		/// Fills the soldier's empty, unlocked armor slots from ONE matching set (same family head/body/legs) when the armory can supply it, so the set bonus
		/// works. Sets are scored by piece tier/defense, a bonus for a real set bonus, and a bonus when the set's doctrine matches the soldier's weapon class;
		/// pieces he already wears must belong to the chosen set. Removes the taken pieces from the armory. Returns false (nothing taken) when no complete
		/// set is on hand or the set would be far weaker than the best loose pieces; the caller then falls back to <see cref="TakeArmor"/>.
		/// </summary>
		public virtual bool TakeArmorSet(SoldierRecord soldier)
		{
			// Own armory first; only when it cannot complete a set do the pylon-networked armories' storage join the pool.
			if (TakeArmorSetCore(soldier, false)) return true;
			return (OtherNetworked().Count > 0 || MsHeart() != null) && TakeArmorSetCore(soldier, true);
		}

		/// <summary>
		/// Re-arms the soldier's unlocked armor slots with the strongest set on hand when it beats the worn kit by <see cref="LevyConstants.AutoUpgradeScoreMargin"/>
		/// (the armor half of <see cref="UpgradeWeapons"/>). Same set scoring as <see cref="TakeArmorSet"/>, aimed at his CURRENT WEAPON's class, with the worn pieces
		/// competing as candidates so an equal set never swaps. The new pieces are real finite instances drawn from this armory (or the networked ones); the old ones go
		/// back into storage (protected spill if it is full). Locked slots stay put and count as fixed members of the set. True if anything changed.
		/// </summary>
		public virtual bool UpgradeArmor(SoldierRecord soldier) => TakeArmorSetCore(soldier, true, true);

		private bool TakeArmorSetCore(SoldierRecord soldier, bool usePool, bool upgrade = false)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			int[] slot = { SoldierGear.SlotHead, SoldierGear.SlotBody, SoldierGear.SlotLegs };
			Item[] cur = { g.Head, g.Body, g.Legs };
			// Issue fills empty unlocked slots; an upgrade re-evaluates every unlocked slot, and only a soldier with a weapon (the class the armor follows) is re-armed.
			var need = new bool[ArmorParts]; bool anyNeed = false;
			for (int p = 0; p < ArmorParts; p++) { need[p] = !g.IsLocked(slot[p]) && (upgrade || IsAir(cur[p])); anyNeed |= need[p]; }
			if (!anyNeed || (upgrade && !g.HasWeapon)) return false;

			// Pool: the best copy of each armor type, per part (issue row first, then storage; strictly-better wins so the issue row takes ties).
			var pool = new List<ArmorCand>[ArmorParts];
			var byType = new Dictionary<int, int>[ArmorParts];
			for (int p = 0; p < ArmorParts; p++) { pool[p] = new List<ArmorCand>(); byType[p] = new Dictionary<int, int>(); }
			object msHeart = usePool ? MsHeart() : null;
			void Scan(Item[] arr, ArmoryTE src, bool ms = false)
			{
				for (int i = 0; i < arr.Length; i++) {
					var it = arr[i];
					if (IsAir(it) || it.vanity || src.IsReservedRef(it)) continue;
					for (int p = 0; p < ArmorParts; p++) {
						if (!need[p] || !WeaponClassifier.IsArmorPiece(it, p)) continue;
						float sc = ArmorScore(it);
						var c = new ArmorCand { Src = src, Arr = arr, Idx = i, It = it, Score = sc, Stem = ArmorStem(it), Ms = ms };
						if (byType[p].TryGetValue(it.type, out int at)) { if (sc > pool[p][at].Score) pool[p][at] = c; }
						else { byType[p][it.type] = pool[p].Count; pool[p].Add(c); }
						break;
					}
				}
			}
			if (upgrade) { // the worn pieces compete first, so a tie keeps what he wears
				for (int p = 0; p < ArmorParts; p++) {
					if (!need[p] || IsAir(cur[p])) continue;
					byType[p][cur[p].type] = pool[p].Count;
					pool[p].Add(new ArmorCand { Worn = true, It = cur[p], Score = ArmorScore(cur[p]), Stem = ArmorStem(cur[p]) });
				}
			}
			Scan(IssueArmor, this); Scan(Storage, this);
			if (usePool) foreach (var o in OtherNetworked()) Scan(o.Storage, o);
			if (msHeart != null) Scan(MagicStorageBridge.Snapshot(msHeart).FindAll(x => !x.vanity && (x.headSlot >= 0 || x.bodySlot >= 0 || x.legSlot >= 0)).ToArray(), this, true); // Magic Storage last: ties keep the armories' stock
			for (int p = 0; p < ArmorParts; p++) if (need[p] && pool[p].Count == 0) return false; // a needed part has no stock at all: no complete set possible

			// Pieces he already wears are fixed members of the set.
			var fixedC = new ArmorCand[ArmorParts];
			string fixedStem = null; bool conflict = false;
			for (int p = 0; p < ArmorParts; p++) {
				if (need[p]) continue;
				if (IsAir(cur[p])) { fixedC[p] = new ArmorCand { Fixed = true }; continue; } // locked-empty slot: no piece
				string st = ArmorStem(cur[p]);
				if (fixedStem != null && fixedStem != st) conflict = true;
				fixedStem ??= st;
				fixedC[p] = new ArmorCand { Fixed = true, It = cur[p], Score = ArmorScore(cur[p]), Stem = st };
			}
			if (conflict) return false; // he already wears a mixed kit: leave the rest to the per-piece pick

			// The armor follows the weapon: the set bonus should land on the class he actually fires, whichever way he was armed.
			DamageClass wc = g.HasWeapon ? WeaponClassifier.ItemDamageClass(g.Weapon) : null;

			float bestScore = float.NegativeInfinity;
			ArmorCand[] best = null;
			float SetScore(ArmorCand h, ArmorCand b, ArmorCand l, bool requireBonus)
			{
				bool full = h.It != null && b.It != null && l.It != null;
				bool bonus = full && ArmorDoctrine.FormsSetBonus(h.It, b.It, l.It);
				if (requireBonus && !bonus) return float.NegativeInfinity;
				float sc = h.Score + b.Score + l.Score;
				if (bonus) sc += LevyConstants.ArmorSetBonusScore;
				if (wc != null && ArmorDoctrine.PreferredClass(h.It, b.It, l.It) == wc) sc += LevyConstants.ArmorSetClassMatchScore;
				return sc;
			}
			void EvalCombos(List<ArmorCand>[] opts, bool requireBonus)
			{
				foreach (var h in opts[0]) foreach (var b in opts[1]) foreach (var l in opts[2]) {
					float sc = SetScore(h, b, l, requireBonus);
					if (sc > bestScore) { bestScore = sc; best = new[] { h, b, l }; }
				}
			}

			// Pass 1: one family by name.
			var stems = new List<string>();
			for (int p = 0; p < ArmorParts; p++) if (need[p]) foreach (var c in pool[p]) if (!stems.Contains(c.Stem)) stems.Add(c.Stem);
			foreach (var stem in stems) {
				if (fixedStem != null && fixedStem != stem) continue;
				var opts = new List<ArmorCand>[ArmorParts];
				bool ok = true;
				for (int p = 0; p < ArmorParts && ok; p++) {
					opts[p] = need[p] ? pool[p].FindAll(c => c.Stem == stem) : new List<ArmorCand> { fixedC[p] };
					if (opts[p].Count == 0) ok = false;
				}
				if (ok) EvalCombos(opts, false);
			}
			// Pass 2 (irregular names): the strongest pieces whose combination the game itself calls a set (needs all three pieces to verify).
			if (best == null && !IsMissingFixed(fixedC)) {
				var opts = new List<ArmorCand>[ArmorParts];
				for (int p = 0; p < ArmorParts; p++) {
					if (!need[p]) { opts[p] = new List<ArmorCand> { fixedC[p] }; continue; }
					var top = new List<ArmorCand>(pool[p]);
					top.Sort((a, b) => b.Score.CompareTo(a.Score));
					if (top.Count > LevyConstants.ArmorSetCrossSearchTop) top.RemoveRange(LevyConstants.ArmorSetCrossSearchTop, top.Count - LevyConstants.ArmorSetCrossSearchTop);
					opts[p] = top;
				}
				EvalCombos(opts, true);
			}
			if (best == null) return false;

			// Never trade a lot of armor for a tidy set: compare the taken pieces to the best loose ones.
			float taken = 0f, loose = 0f;
			for (int p = 0; p < ArmorParts; p++) {
				if (!need[p]) continue;
				taken += best[p].Score;
				float top = float.NegativeInfinity;
				foreach (var c in pool[p]) top = Math.Max(top, c.Score);
				loose += top;
			}
			if (taken < loose * LevyConstants.ArmorSetMinFractionOfBest) return false;

			if (upgrade) return CommitArmorUpgrade(g, slot, cur, need, best, bestScore, SetScore, msHeart);

			// Commit: each pick is a distinct real slot; remove it and equip it.
			bool any = false;
			for (int p = 0; p < ArmorParts; p++) {
				if (!need[p]) continue;
				var c = best[p];
				if (c.Ms) { // Magic Storage piece: equip exactly what the withdraw returned, nothing if it returned nothing
					if (msHeart == null || !MagicStorageBridge.TryWithdrawExact(msHeart, c.It, 1, out var drawn)) continue;
					if (p == 0) g.Head = drawn; else if (p == 1) g.Body = drawn; else g.Legs = drawn;
					any = true;
					continue;
				}
				if (c.Src == null || c.Arr == null || c.Idx < 0 || c.Idx >= c.Arr.Length || !ReferenceEquals(c.Arr[c.Idx], c.It)) continue;
				var piece = c.Src.TakeSlot(c.Arr, c.Idx); // removes from the SOURCE armory (home or networked) and marks it dirty
				if (piece == null) continue;
				if (p == 0) g.Head = piece; else if (p == 1) g.Body = piece; else g.Legs = piece;
				any = true;
			}
			return any;
		}

		/// <summary>
		/// Second half of <see cref="UpgradeArmor"/>: swaps in the chosen set when it is a real change and beats the set he wears (same pieces, same bonuses) by the upgrade margin.
		/// Every new piece is drawn first; if any draw fails, the ones already drawn go back and nothing changes on the soldier.
		/// </summary>
		private bool CommitArmorUpgrade(SoldierGear g, int[] slot, Item[] cur, bool[] need, ArmorCand[] best, float bestScore,
			Func<ArmorCand, ArmorCand, ArmorCand, bool, float> setScore, object msHeart)
		{
			bool change = false;
			var worn = new ArmorCand[ArmorParts];
			for (int p = 0; p < ArmorParts; p++) {
				if (!need[p]) { worn[p] = new ArmorCand { Fixed = true, It = cur[p], Score = IsAir(cur[p]) ? 0f : ArmorScore(cur[p]) }; continue; }
				worn[p] = IsAir(cur[p]) ? new ArmorCand() : new ArmorCand { Worn = true, It = cur[p], Score = ArmorScore(cur[p]) };
				change |= !best[p].Worn;
			}
			if (!change || bestScore < setScore(worn[0], worn[1], worn[2], false) * LevyConstants.AutoUpgradeScoreMargin) return false;

			var drawn = new Item[ArmorParts];
			bool ok = true;
			for (int p = 0; p < ArmorParts && ok; p++) {
				if (!need[p] || best[p].Worn) continue;
				var c = best[p];
				if (c.Ms) { if (msHeart == null || !MagicStorageBridge.TryWithdrawExact(msHeart, c.It, 1, out var d)) ok = false; else drawn[p] = d; }
				else if (c.Src == null || c.Arr == null || c.Idx < 0 || c.Idx >= c.Arr.Length || !ReferenceEquals(c.Arr[c.Idx], c.It)) ok = false;
				else drawn[p] = c.Src.TakeSlot(c.Arr, c.Idx); // removes from the SOURCE armory and marks it dirty
				if (drawn[p] == null) ok = false;
			}
			if (!ok) {
				foreach (var d in drawn) if (!IsAir(d)) { var rem = Deposit(d); if (!IsAir(rem)) Spill(rem, protect: true); }
				return false;
			}
			for (int p = 0; p < ArmorParts; p++) {
				if (drawn[p] == null) continue;
				var old = cur[p];
				g.Set(slot[p], drawn[p]);
				if (IsAir(old)) continue;
				var rem = Deposit(old);
				if (!IsAir(rem)) Spill(rem, protect: true);
			}
			MarkDirty();
			return true;
		}

		/// <summary>True when any fixed (not-needed) slot holds no piece (a locked-empty slot), so a three-piece set cannot be verified.</summary>
		private static bool IsMissingFixed(ArmorCand[] fixedC)
		{
			foreach (var c in fixedC) if (c.Fixed && c.It == null) return true;
			return false;
		}

		/// <summary>Fills the pouch with ammo of <paramref name="ammoId"/> (AmmoID.Bullet/Arrow) from issue slots in priority order, then storage. Returns the amount taken.</summary>
		public virtual int TakeAmmo(int ammoId, Item pouch, int max)
		{
			if (pouch == null || ammoId <= 0 || max <= 0) return 0;
			if (!IsAir(pouch) && pouch.ammo != ammoId) return 0;
			int moved = Drain(IssueAmmo, ammoId, pouch, max);
			moved += Drain(Storage, ammoId, pouch, max);
			if (moved > 0) MarkDirty();
			// Networked armories top the pouch up; Drain only moves what it removes, and only a source that lost ammo is marked dirty.
			foreach (var o in OtherNetworked()) {
				int n = Drain(o.Storage, ammoId, pouch, max);
				if (n > 0) { o.MarkDirty(); moved += n; }
			}
			if (MagicStorageBridge.Enabled) moved += MsTakeAmmo(ammoId, pouch, max); // then a connected Magic Storage network
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

		/// <summary>Takes one potion of the given issue kind (0 healing, 1.. a buff slot). Null if none.</summary>
		public virtual Item TakePotion(int kind)
		{
			if (kind < 0 || kind >= IssuePotions.Length) return null;
			var it = IssuePotions[kind];
			if (IsAir(it)) return null;
			if (kind == 0 && it.healLife <= 0 && it.healMana <= 0) return null; // slot 0 dispenses both health and mana restoratives
			var one = it.Clone();
			one.stack = 1;
			it.stack--;
			if (it.stack <= 0) IssuePotions[kind] = new Item();
			MarkDirty();
			return one;
		}

		/// <summary>Takes one potion that satisfies <paramref name="serves"/>, from the issue slot <paramref name="preferSlot"/> first, then (when <paramref name="allowStorage"/>) bulk storage. With allowStorage false only the issue slot is checked. Null if none. Server/single player.</summary>
		public virtual Item TakeServingPotion(int preferSlot, Func<Item, bool> serves, bool allowStorage = true)
		{
			if (serves == null) return null;
			if (preferSlot >= 0 && preferSlot < IssuePotions.Length && !IsAir(IssuePotions[preferSlot]) && serves(IssuePotions[preferSlot]))
				return SplitOne(IssuePotions, preferSlot);
			if (!allowStorage) return null;
			for (int i = 0; i < Storage.Length; i++) {
				var it = Storage[i];
				if (IsAir(it) || !serves(it)) continue;
				return SplitOne(Storage, i);
			}
			return null;
		}

		/// <summary>Removes ONE item from the stack in arr[idx] (clearing the slot when it empties) and returns it as a 1-stack clone; marks dirty.</summary>
		private Item SplitOne(Item[] arr, int idx)
		{
			var it = arr[idx];
			var one = it.Clone();
			one.stack = 1;
			it.stack--;
			if (it.stack <= 0) arr[idx] = new Item();
			MarkDirty();
			return one;
		}

		// ------------------------------------------------------------------ sort

		private const int GroupWeapon = 0, GroupArmor = 1, GroupAccessory = 2, GroupAmmo = 3, GroupPotion = 4, GroupOther = 5;

		/// <summary>Sort bucket and in-bucket sub-order: weapons the soldiers can use first, then armor by part, accessories, ammo, potions, everything else.</summary>
		private static void SortBucket(Item it, out int group, out int sub)
		{
			sub = 0;
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0)
			{
				group = GroupArmor;
				sub = (it.headSlot >= 0 ? 0 : it.bodySlot >= 0 ? 1 : 2) + (it.vanity ? 3 : 0);
			}
			else if (it.accessory) { group = GroupAccessory; sub = it.vanity ? 1 : 0; }
			else if (it.ammo > 0 && !it.IsACoin) group = GroupAmmo;
			else if (it.damage > 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.IsACoin) { group = GroupWeapon; sub = WeaponClassifier.IsIssuableWeapon(it) ? 0 : 1; }
			else if (it.healLife > 0 || it.healMana > 0 || (it.buffType > 0 && it.consumable)) group = GroupPotion;
			else group = GroupOther;
		}

		private struct SortKey
		{
			public Item Item; public int Group, Sub, Tier, BaseValue;
		}

		/// <summary>
		/// Tidies bulk storage: merges partial stacks of the same type and prefix, orders the rest weapons → armor → accessories → ammo → potions →
		/// everything else (within a bucket by rarity tier then value, highest first; same item types stay together), and packs it to the front so
		/// every empty slot trails at the end. Server / single player only (a client asks the server). Returns true if anything moved.
		/// </summary>
		public bool SortStorage()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return false;
			var items = new List<Item>();
			foreach (var it in Storage) if (!IsAir(it)) items.Add(it);

			// 1. merge partial stacks (same type + prefix; the mod's CanStack hooks decide the rest)
			var open = new Dictionary<(int, int), Item>();
			var merged = new List<Item>(items.Count);
			foreach (var it in items)
			{
				if (it.maxStack > 1 && open.TryGetValue((it.type, it.prefix), out var dest) && dest.stack < dest.maxStack)
				{
					ItemLoader.TryStackItems(dest, it, out _);
					if (IsAir(it)) continue;
				}
				merged.Add(it);
				if (it.maxStack > 1) open[(it.type, it.prefix)] = it;
			}

			// 2. order
			var keys = new List<SortKey>(merged.Count);
			foreach (var it in merged)
			{
				SortBucket(it, out int g, out int sub);
				int baseValue = ContentSamples.ItemsByType.TryGetValue(it.type, out var sample) && sample != null ? sample.value : it.value;
				keys.Add(new SortKey { Item = it, Group = g, Sub = sub, Tier = WeaponClassifier.RarityTier(it.rare), BaseValue = baseValue });
			}
			keys.Sort((a, b) =>
			{
				int c = a.Group.CompareTo(b.Group); if (c != 0) return c;
				c = a.Sub.CompareTo(b.Sub); if (c != 0) return c;
				c = b.Tier.CompareTo(a.Tier); if (c != 0) return c;
				c = b.BaseValue.CompareTo(a.BaseValue); if (c != 0) return c;
				c = a.Item.type.CompareTo(b.Item.type); if (c != 0) return c;
				c = a.Item.prefix.CompareTo(b.Item.prefix); if (c != 0) return c;
				return b.Item.stack.CompareTo(a.Item.stack);
			});

			// 3. compact to the front
			bool changed = false;
			for (int i = 0; i < Storage.Length; i++)
			{
				Item next = i < keys.Count ? keys[i].Item : null;
				if (next == null) { if (!IsAir(Storage[i])) { Storage[i] = new Item(); changed = true; } continue; }
				if (!ReferenceEquals(Storage[i], next)) { Storage[i] = next; changed = true; }
			}
			if (changed || merged.Count != items.Count) MarkDirty();
			return changed;
		}

		// ------------------------------------------------------------------ bulk reforge

		/// <summary>Outcome of one <see cref="ReforgeToFloor"/> run.</summary>
		public class ReforgeResult
		{
			/// <summary>Weapons that were rolled at least once.</summary>
			public int Weapons;
			/// <summary>Paid rolls in total.</summary>
			public int Rolls;
			/// <summary>Weapons that reached the floor during this run.</summary>
			public int Reached;
			/// <summary>Weapons below the floor at the end (cap hit or treasury empty).</summary>
			public int Short;
			/// <summary>Of those, weapons that used up their roll cap.</summary>
			public int Capped;
			public long Spent;
			public bool RanDry;
			public bool NoPayer;
		}

		/// <summary>Per weapon type: the quality scores of every prefix that weapon can roll (one per prefix, best first). Learned once from throw-away rolls.</summary>
		private static readonly Dictionary<int, double[]> _prefixScores = new Dictionary<int, double[]>();

		/// <summary>
		/// Prefix quality for one weapon: the item's market value relative to the same weapon with no prefix. Terraria builds an item's value from
		/// every stat the prefix touches (damage, speed, crit, knockback, mana, velocity, size), so it is the one number that ranks any prefix
		/// of any weapon, vanilla or modded, on the same scale (1.0 = unprefixed; Legendary / Mythical / Unreal / Godly sit highest, Broken / Shoddy
		/// / Awful lowest). Weapons with no base value fall back to damage x speed x crit relative to the unprefixed weapon.
		/// </summary>
		public static double PrefixQuality(Item it, Item sample)
		{
			if (sample.value > 0) return (double)it.value / sample.value;
			double dmg = sample.damage > 0 ? (double)it.damage / sample.damage : 1.0;
			double spd = it.useAnimation > 0 && sample.useAnimation > 0 ? (double)sample.useAnimation / it.useAnimation : 1.0;
			return dmg * spd * (1.0 + (it.crit - sample.crit) / 100.0);
		}

		private static double[] LearnPrefixScores(Item sample)
		{
			if (_prefixScores.TryGetValue(sample.type, out var cached)) return cached;
			var byPrefix = new Dictionary<int, double>();
			for (int i = 0; i < LevyConstants.UI.ReforgeSampleRolls; i++)
			{
				var t = new Item();
				t.SetDefaults(sample.type);
				t.Prefix(-2);
				if (t.prefix > 0 && !byPrefix.ContainsKey(t.prefix)) byPrefix[t.prefix] = PrefixQuality(t, sample);
			}
			var arr = new double[byPrefix.Count];
			int n = 0; foreach (var v in byPrefix.Values) arr[n++] = v;
			Array.Sort(arr); Array.Reverse(arr);
			_prefixScores[sample.type] = arr;
			return arr;
		}

		/// <summary>
		/// The quality a weapon must reach: the score of the k-th best prefix it can roll, where k = ceil(topFraction x prefixes it can roll)
		/// (at least 1, so fraction 0 means "the single best"). Defined for every prefixable weapon, and a floor means the same thing for a
		/// sword and a staff alike: the top slice of what THAT weapon can get.
		/// </summary>
		private static double FloorThreshold(double[] sortedDesc, float topFraction)
		{
			int k = topFraction <= 0f ? 1 : Math.Max(1, (int)Math.Ceiling(topFraction * sortedDesc.Length));
			return sortedDesc[Math.Min(k, sortedDesc.Length) - 1];
		}

		/// <summary>Weapons only (armor, accessories, ammo, potions, tools, stacks and favourited items are never touched) that can take a prefix.</summary>
		public static bool IsReforgeable(Item it) =>
			!IsAir(it) && !it.favorited && it.maxStack == 1 && it.damage > 0 && !it.accessory && !it.consumable && !it.IsACoin
			&& it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && it.CanHavePrefixes();

		/// <summary>Vanilla reforge price of this item as it is now: its value (prefix included) / 3, through the mods' ReforgePrice hooks. No Goblin Tinkerer discount, no shop mood.</summary>
		public static long ReforgePrice(Item it)
		{
			int price = it.value; bool canDiscount = true;
			if (ItemLoader.ReforgePrice(it, ref price, ref canDiscount)) price /= 3;
			return Math.Max(1, price);
		}

		/// <summary>The barracks whose treasury pays for this armory (the nearest one that links to it), or null.</summary>
		public BarracksTE LinkedBarracks()
		{
			BarracksTE best = null; float bestD = float.MaxValue;
			float limit = LevyConstants.ArmoryLinkRadius * LevyConstants.TilePx; limit *= limit;
			foreach (var b in SettlementSystem.Instance.AllBarracks())
			{
				float d = Vector2.DistanceSquared(b.Center, Center);
				if (d > limit || d >= bestD || !ReferenceEquals(SettlementSystem.Instance.LinkedArmory(b), this)) continue;
				bestD = d; best = b;
			}
			return best;
		}

		/// <summary>
		/// Bulk reforge-to-threshold. For every weapon below the floor (issue rows first, then storage) it pays the barracks treasury the vanilla
		/// reforge price per roll and rerolls the prefix until it meets <paramref name="floorIndex"/> (an index into
		/// <see cref="LevyConstants.UI.ReforgeFloorTopFraction"/>), up to <see cref="LevyConstants.UI.ReforgeMaxRollsPerWeapon"/> rolls. A weapon that
		/// misses keeps the best prefix it rolled (never worse than before); when the treasury cannot afford the next roll the run stops.
		/// Server / single player only.
		/// </summary>
		public ReforgeResult ReforgeToFloor(BarracksTE payer, int floorIndex)
		{
			var res = new ReforgeResult();
			if (Main.netMode == NetmodeID.MultiplayerClient) return res;
			if (payer == null) { res.NoPayer = true; return res; }
			var fractions = LevyConstants.UI.ReforgeFloorTopFraction;
			float frac = fractions[Math.Max(0, Math.Min(fractions.Length - 1, floorIndex))];
			bool changed = false;
			foreach (var arr in new[] { IssueWeapons, Storage })
			{
				for (int i = 0; i < arr.Length; i++)
				{
					var item = arr[i];
					if (!IsReforgeable(item)) continue;
					if (!ContentSamples.ItemsByType.TryGetValue(item.type, out var sample) || sample == null) continue;
					var scores = LearnPrefixScores(sample);
					if (scores.Length == 0) continue;
					double threshold = FloorThreshold(scores, frac) - 1e-6;
					double bestScore = PrefixQuality(item, sample);
					if (bestScore >= threshold) continue;
					if (res.RanDry) { res.Short++; continue; }

					Item best = item, current = item;
					bool reached = false, rolled = false;
					int rolls = 0;
					while (rolls < LevyConstants.UI.ReforgeMaxRollsPerWeapon)
					{
						long price = ReforgePrice(current);
						if (!EconomySystem.TryPay(payer, price)) { res.RanDry = true; break; }
						res.Spent += price;
						res.Rolls++; rolls++; rolled = true;
						var trial = current.Clone();
						ItemLoader.PreReforge(trial);
						trial.ResetPrefix();
						trial.Prefix(-2);
						ItemLoader.PostReforge(trial);
						current = trial;
						double score = PrefixQuality(trial, sample);
						if (score > bestScore) { best = trial; bestScore = score; }
						if (score >= threshold) { reached = true; break; }
					}
					if (rolled) res.Weapons++;
					if (!ReferenceEquals(best, item)) { arr[i] = best; changed = true; }
					if (reached) res.Reached++;
					else { res.Short++; if (rolls >= LevyConstants.UI.ReforgeMaxRollsPerWeapon) res.Capped++; }
				}
			}
			if (changed) MarkDirty();
			if (res.Spent > 0) payer.MarkDirty();
			return res;
		}

		/// <summary>Chat lines telling the player how the bulk reforge went. Server / single player.</summary>
		public static void ReportReforge(Player to, ReforgeResult r)
		{
			if (to == null || r == null) return;
			if (r.NoPayer) { RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeNoBarracks")); return; }
			if (r.Rolls == 0 && r.Short == 0) { RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeNothing")); return; }
			if (r.Rolls > 0) RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeReport", r.Weapons, r.Rolls, LevyUtil.Coins(r.Spent), r.Reached));
			if (r.RanDry) RosterSystem.SendToPlayer(to, LevyUtil.T("UI.NotEnoughMoney") + " " + LevyUtil.T("UI2.ReforgeBroke", r.Short));
			else if (r.Capped > 0) RosterSystem.SendToPlayer(to, LevyUtil.T("UI2.ReforgeCapped", r.Capped, LevyConstants.UI.ReforgeMaxRollsPerWeapon));
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

		/// <summary>
		/// Auto-issue best-first. Fluid (default): weapon, then armor aimed at that weapon's class, sidearm, accessories (grade slots), ammo. With
		/// <see cref="Common.Config.LevyConfig.ClassMatchIssue"/> (fixed-class companies): armor first, then a weapon of the armor's damage class when possible.
		/// Slots the player hand-assigned (<see cref="SoldierGear.IsLocked"/>) are never replaced or emptied. Returns true if anything changed.
		/// </summary>
		public virtual bool IssueGear(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			bool changed = false;

			// A Re-arm in this very tick parked this soldier's hand-assigned items: take exactly those back first.
			if (g.Reserved != null && ReclaimLocked(g, soldier.AccessorySlots)) changed = true;

			// Fluid issue: the strongest weapon he can use comes first and the armor follows it. A thrown stack is restocked before a new pick, as below.
			bool fluid = !Common.Config.LevyConfig.Get.ClassMatchIssue;
			if (fluid) {
				if (RestockThrown(soldier)) changed = true;
				if (IssuePrimary(soldier, g)) changed = true;
			}

			// One matching set first (set bonus, aligned with his weapon); anything it could not supply is filled piece by piece below.
			if (TakeArmorSet(soldier)) changed = true;

			if (!g.IsLocked(SoldierGear.SlotHead) && IsAir(g.Head)) { var a = TakeArmor(soldier, 0); if (a != null) { g.Head = a; changed = true; } }
			if (!g.IsLocked(SoldierGear.SlotBody) && IsAir(g.Body)) { var a = TakeArmor(soldier, 1); if (a != null) { g.Body = a; changed = true; } }
			if (!g.IsLocked(SoldierGear.SlotLegs) && IsAir(g.Legs)) { var a = TakeArmor(soldier, 2); if (a != null) { g.Legs = a; changed = true; } }

			// The weapon and the armor come from separate pickers (network-wide availability vs local racks), so they can disagree: the armor now on him wins.
			if (fluid && ReconcileWeaponToArmor(soldier)) changed = true;

			if (!fluid) {
				// A thrown stack is kept supplied: topped up while he holds it, handed back first when it ran dry.
				if (RestockThrown(soldier)) changed = true;
				// Fixed-class companies: the armor decides doctrine, so prefer a weapon of its class, else the best of anything.
				if (IssuePrimary(soldier, g)) changed = true;
			}

			// A melee-type primary (blade, spear, boomerang) takes no fallback blade. A summoner seeks a whip + a staff — summon weapons are reserved for him; every
			// other class may carry a lesser blade. The sidearm is chosen after the accessories so the kit can steer it (accessory choice never reads the sidearm).
			var primaryFam = g.HasWeapon ? WeaponClassifier.Classify(g.Weapon) : WeaponFamily.None;
			bool primaryMelee = IsMeleeTypeFamily(primaryFam);
			bool summoner = IsSummonerSoldier(soldier);
			int slots = Math.Clamp(soldier.AccessorySlots, 0, Math.Min(SoldierGear.AccessorySlots, g.Accessories.Length));
			for (int i = 0; i < slots; i++) {
				if (g.IsLocked(SoldierGear.SlotAcc0 + i) || !IsAir(g.Accessories[i])) continue;
				var a = TakeAccessory(soldier);
				if (a == null) break;
				g.Accessories[i] = a; changed = true;
			}

			// Reservation: a non-summoner never keeps a summon-class off-hand (a leftover from an old save or a role change goes back to the racks).
			if (!summoner && !g.IsLocked(SoldierGear.SlotSidearm) && g.HasSidearm && WeaponClassifier.ItemDamageClass(g.Sidearm) == DamageClass.Summon)
			{
				var rem = Deposit(g.Sidearm);
				if (!IsAir(rem)) Spill(rem, protect: true);
				g.Sidearm = new Item();
				changed = true;
			}

			if (!g.IsLocked(SoldierGear.SlotSidearm) && !g.HasSidearm)
			{
				Item s = summoner ? TakeSummonerComplement(soldier, primaryFam)
					: primaryMelee ? null // a melee-type primary carries no fallback blade
					: TakeSidearm(soldier); // ranged / magic: a lesser blade
				if (s != null) { g.Sidearm = s; changed = true; }
			}

			g.Ammo ??= new Item();
			if (g.IsLocked(SoldierGear.SlotAmmo)) {
				// Hand-assigned pouch: never swapped or dumped; if it has run dry the lock lapses so the armory can refill it.
				if (IsAir(g.Ammo)) g.SetLocked(SoldierGear.SlotAmmo, false);
			}
			if (!g.IsLocked(SoldierGear.SlotAmmo)) {
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
			}

			if (changed) MarkDirty();
			return changed;
		}

		/// <summary>Arms an empty, unlocked primary slot: in fixed-class mode a weapon of the worn armor's class first; otherwise the strongest weapon of a class he can armor (<see cref="ArmorableClasses"/>; any class when none is constrained). The fallback is the strongest weapon he can use. True if he took one.</summary>
		private bool IssuePrimary(SoldierRecord soldier, SoldierGear g)
		{
			if (g.IsLocked(SoldierGear.SlotWeapon) || g.HasWeapon) return false;
			Item w = null;
			if (Common.Config.LevyConfig.Get.ClassMatchIssue)
			{
				var cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
				if (cls != null) w = TakeBestWeaponOfClass(soldier, cls);
			}
			else
			{
				var ok = ArmorableClasses(soldier, false);
				if (ok.Count > 0) w = TakeBestWeaponInClasses(soldier, ok);
			}
			w ??= TakeBestWeapon(soldier);
			if (w == null) return false;
			g.Weapon = w;
			return true;
		}

		// ------------------------------------------------------------------ coherent kit (a fluid soldier's weapon class follows the armor he can field)

		/// <summary>The strongest issuable weapon whose damage class is in <paramref name="classes"/>; a weapon with no class at all only as a last resort. Removes it. Null if none (callers fall back to <see cref="TakeBestWeapon"/>).</summary>
		private Item TakeBestWeaponInClasses(SoldierRecord forSoldier, HashSet<DamageClass> classes)
		{
			return TakeBestWeaponWhere(forSoldier, it => { var c = WeaponClassifier.ItemDamageClass(it); return c != null && classes.Contains(c); })
				?? TakeBestWeaponWhere(forSoldier, it => WeaponClassifier.ItemDamageClass(it) == null);
		}

		/// <summary>
		/// The damage classes this soldier can coherently wear armor for right now: the class of the armor he already wears, plus the class of every complete set
		/// (same-family head/body/legs, or a set the game itself calls one) the armory could hand him, scored like <see cref="TakeArmorSetCore"/> (open slots only,
		/// worn and locked pieces fixed, a set far weaker than the best loose pieces dropped). READ-ONLY: it scans, it never takes. His own armory's stock decides
		/// first, as in <see cref="TakeArmorSet"/>; the networked armories and Magic Storage count only when his own cannot complete a set. With no set to be had,
		/// the class of the best loose pieces counts. <paramref name="upgrade"/> opens every unlocked slot with the worn pieces competing (the <see cref="UpgradeArmor"/> view).
		/// Empty = nothing class-defining to match, so any weapon is fine.
		/// </summary>
		private HashSet<DamageClass> ArmorableClasses(SoldierRecord soldier, bool upgrade)
		{
			var res = new HashSet<DamageClass>();
			var g = soldier?.Gear;
			if (g == null) return res;
			var worn = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			if (worn != null) res.Add(worn); // already on his back: always armorable, so a coherent kit never thrashes
			int[] slot = { SoldierGear.SlotHead, SoldierGear.SlotBody, SoldierGear.SlotLegs };
			Item[] cur = { g.Head, g.Body, g.Legs };
			var open = new bool[ArmorParts]; bool anyOpen = false, missingFixed = false;
			string fixedStem = null; bool conflict = false;
			for (int p = 0; p < ArmorParts; p++) {
				open[p] = !g.IsLocked(slot[p]) && (upgrade || IsAir(cur[p]));
				anyOpen |= open[p];
				if (open[p]) continue;
				if (IsAir(cur[p])) { missingFixed = true; continue; }
				string st = ArmorStem(cur[p]);
				if (fixedStem != null && fixedStem != st) conflict = true;
				fixedStem ??= st;
			}
			if (!anyOpen) return res;

			// One pass over one pool: the best copy of each armor type per open part (the same candidate set TakeArmorSetCore builds). True when it holds a usable set.
			bool Gather(bool usePool, bool loose)
			{
				var pool = new Dictionary<int, Item>[ArmorParts];
				for (int p = 0; p < ArmorParts; p++) pool[p] = new Dictionary<int, Item>();
				void Add(int p, Item it) { if (!pool[p].TryGetValue(it.type, out var at) || ArmorScore(it) > ArmorScore(at)) pool[p][it.type] = it; }
				void Scan(Item[] arr, ArmoryTE src)
				{
					for (int i = 0; i < arr.Length; i++) {
						var it = arr[i];
						if (IsAir(it) || it.vanity || src.IsReservedRef(it)) continue;
						for (int p = 0; p < ArmorParts; p++) if (open[p] && WeaponClassifier.IsArmorPiece(it, p)) { Add(p, it); break; }
					}
				}
				if (upgrade) for (int p = 0; p < ArmorParts; p++) if (open[p] && !IsAir(cur[p])) Add(p, cur[p]);
				Scan(IssueArmor, this); Scan(Storage, this);
				if (usePool) {
					foreach (var o in OtherNetworked()) Scan(o.Storage, o);
					var heart = MsHeart();
					if (heart != null) Scan(MagicStorageBridge.Snapshot(heart).FindAll(x => !x.vanity && (x.headSlot >= 0 || x.bodySlot >= 0 || x.legSlot >= 0)).ToArray(), this);
				}
				float looseTop = 0f;
				for (int p = 0; p < ArmorParts; p++) {
					if (!open[p]) continue;
					if (pool[p].Count == 0) { looseTop = -1f; break; } // a needed part has no stock at all: no complete set
					float top = float.NegativeInfinity;
					foreach (var c in pool[p].Values) top = Math.Max(top, ArmorScore(c));
					looseTop += top;
				}
				bool found = false;
				if (looseTop >= 0f && !conflict) {
					// Real sets only count when they are not far weaker than the best loose pieces (the armor-follow's own gate).
					void Eval(List<Item>[] opts, bool requireBonus)
					{
						foreach (var h in opts[0]) foreach (var b in opts[1]) foreach (var l in opts[2]) {
							if (requireBonus && !ArmorDoctrine.FormsSetBonus(h, b, l)) continue;
							float taken = 0f; Item[] pick = { h, b, l };
							for (int p = 0; p < ArmorParts; p++) if (open[p]) taken += ArmorScore(pick[p]);
							if (taken < looseTop * LevyConstants.ArmorSetMinFractionOfBest) continue;
							found = true;
							var c = ArmorDoctrine.PreferredClass(h, b, l);
							if (c != null) res.Add(c);
						}
					}
					List<Item>[] Opts(Func<int, IEnumerable<Item>> open_)
					{
						var o = new List<Item>[ArmorParts];
						for (int p = 0; p < ArmorParts; p++) o[p] = open[p] ? new List<Item>(open_(p)) : new List<Item> { cur[p] };
						return o;
					}
					var stems = new HashSet<string>();
					for (int p = 0; p < ArmorParts; p++) if (open[p]) foreach (var c in pool[p].Values) stems.Add(ArmorStem(c));
					foreach (var stem in stems) {
						if (fixedStem != null && fixedStem != stem) continue;
						var opts = Opts(p => { var l = new List<Item>(); foreach (var c in pool[p].Values) if (ArmorStem(c) == stem) l.Add(c); return l; });
						bool ok = true;
						for (int p = 0; p < ArmorParts; p++) if (open[p] && opts[p].Count == 0) ok = false;
						if (ok) Eval(opts, false);
					}
					// Irregular names: the strongest pieces whose combination the game itself calls a set.
					if (!found && !missingFixed) {
						Eval(Opts(p => {
							var top = new List<Item>(pool[p].Values);
							top.Sort((a, b) => ArmorScore(b).CompareTo(ArmorScore(a)));
							if (top.Count > LevyConstants.ArmorSetCrossSearchTop) top.RemoveRange(LevyConstants.ArmorSetCrossSearchTop, top.Count - LevyConstants.ArmorSetCrossSearchTop);
							return top;
						}), true);
					}
				}
				if (!found && loose) {
					// No set to be had: the per-piece pick fills each open part with its best piece, and that kit's class is what he will wear.
					var kit = new Item[ArmorParts];
					bool any = false;
					for (int p = 0; p < ArmorParts; p++) {
						kit[p] = cur[p];
						if (!open[p]) continue;
						float top = float.NegativeInfinity;
						foreach (var c in pool[p].Values) if (ArmorScore(c) > top) { top = ArmorScore(c); kit[p] = c; any = true; }
					}
					if (any) { var c = ArmorDoctrine.PreferredClass(kit[0], kit[1], kit[2]); if (c != null) res.Add(c); }
				}
				return found;
			}

			if (!upgrade && Gather(false, false)) return res;
			Gather(true, !upgrade); // upgrade never falls back to loose pieces: UpgradeArmor only swaps in a whole set
			return res;
		}

		// ------------------------------------------------------------------ automatic weapon upgrade

		/// <summary>
		/// Automatic gear upgrade (server / single player): swaps the soldier's primary for a clearly better weapon he can use (ANY damage class, summon staves and
		/// sentries included, unless <see cref="Common.Config.LevyConfig.ClassMatchIssue"/> pins him to his own), his armor for a
		/// clearly better set aimed at that weapon's class (<see cref="UpgradeArmor"/>), and his sidearm for a better one of the SAME KIND
		/// (sentry / whip / summon staff / blade) when this armory stocks one. The candidate is drawn with
		/// the same pick as <see cref="IssueGear"/> (<see cref="TakeVaried"/> / <see cref="TakeBest"/>), so it is a real, finite instance: the soldier's
		/// old gear goes back into storage and a different soldier can never hold the same one. A candidate must score at least
		/// <see cref="LevyConstants.AutoUpgradeScoreMargin"/> times the gear in hand. Hand-assigned (locked) slots and
		/// <see cref="SoldierRecord.LoadoutLocked"/> soldiers are never touched; an empty slot is left to <see cref="IssueGear"/>. True if anything changed.
		/// </summary>
		public virtual bool UpgradeWeapons(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null || soldier.LoadoutLocked || Main.netMode == NetmodeID.MultiplayerClient) return false;
			bool changed = false;
			if (!g.IsLocked(SoldierGear.SlotWeapon) && UpgradePrimary(soldier)) changed = true;
			if (UpgradeArmor(soldier)) changed = true; // after the primary: the armor follows the weapon he now carries (locked slots are skipped inside)
			if (ReconcileWeaponToArmor(soldier)) changed = true; // the armor may not have followed (margin, locks, stock): a class mismatch is corrected, never upgraded into
			if (!g.IsLocked(SoldierGear.SlotSidearm) && UpgradeSidearm(soldier)) changed = true;
			// The new weapon may fire other ammo (or none): IssueGear returns the wrong pouch and draws the right one (it only fills what is empty otherwise).
			if (changed) IssueGear(soldier);
			return changed;
		}

		private bool UpgradePrimary(SoldierRecord soldier)
		{
			var g = soldier.Gear;
			var cur = g.Weapon;
			if (IsAir(cur) || cur.maxStack > 1) return false; // empty hand: IssueGear's job; thrown stacks are restocked, not swapped
			var curFam = WeaponClassifier.GetProfile(cur).Family;
			var cls = WeaponClassifier.ItemDamageClass(cur);
			if (curFam == WeaponFamily.Thrown || cls == null) return false;
			float need = WeaponClassifier.Score(cur) * LevyConstants.AutoUpgradeScoreMargin;
			var onHand = AmmoIdsOnHand();
			var pouch = g.Ammo;
			bool pouchFixed = g.IsLocked(SoldierGear.SlotAmmo) && !IsAir(pouch); // a hand-assigned pouch is never swapped, so the new weapon must fire it
			bool sameClassOnly = Common.Config.LevyConfig.Get.ClassMatchIssue; // fixed-class companies keep their damage class; fluid soldiers cross over
			var stockOk = new Dictionary<int, bool>();
			// The worn armor defines a fluid soldier's class: with one, the weapon only upgrades within it (no cross-class chase for ReconcileWeaponToArmor to undo).
			var armorCls = sameClassOnly ? null : ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			HashSet<DamageClass> armorable = null; // computed once, only when a candidate would otherwise qualify (it scans the armor shelves)
			var rule = PrimaryRule(soldier); // no whip over a staff; a ranged helmet's subtype (same rule as the first issue, so a swap is never undone by the reconcile)
			bool Pred(Item it)
			{
				if (!IsIssuable(it) || it.maxStack > 1 || !HasAmmoFor(it, soldier, onHand)) return false;
				if (rule != null && !rule(it)) return false;
				if (sameClassOnly && WeaponClassifier.ItemDamageClass(it) != cls) return false;
				if (pouchFixed && it.useAmmo > 0 && pouch.ammo != it.useAmmo) return false;
				var fam = WeaponClassifier.GetProfile(it).Family;
				// Fully fluid: a summon staff / sentry is an ordinary candidate, in or out. Only the score margin (and the swap cooldown) gate it; the armor follows the new weapon's class.
				// A whip in hand yields to any staff regardless of score (the whip moves to the sidearm slot); a staff in hand never trades for a whip (the rule above).
				if (fam == WeaponFamily.Thrown || WeaponClassifier.Score(it) < (curFam == WeaponFamily.Whip && fam == WeaponFamily.Summon ? 0f : need)) return false;
				// Coherent kit: a fluid soldier only crosses into a class he can also armor (worn, or a set on the shelves); nothing class-defining to match = any class.
				if (!sameClassOnly) {
					var ic = WeaponClassifier.ItemDamageClass(it);
					if (armorCls != null) { if (ic != armorCls) return false; }
					else if (ic != null) { armorable ??= ArmorableClasses(soldier, true); if (armorable.Count > 0 && !armorable.Contains(ic)) return false; }
				}
				// Moving onto ammo he does not carry needs a real stock of it, so a swap never leaves a gun with an empty pouch.
				if (it.useAmmo <= 0 || (!IsAir(pouch) && pouch.ammo == it.useAmmo)) return true;
				if (!stockOk.TryGetValue(it.useAmmo, out bool enough)) stockOk[it.useAmmo] = enough = AmmoStockAtLeast(it.useAmmo, LevyConstants.AutoUpgradeMinAmmoStock);
				return enough;
			}
			var neu = TakeVaried(soldier, Pred);
			if (neu == null || !SwapIn(g, SoldierGear.SlotWeapon, neu)) return false;
			ShelveSidearmForPrimary(g, neu, WeaponClassifier.ItemDamageClass(neu) != cls);
			return true;
		}

		/// <summary>After a primary swap: a melee-type primary of a new class carries no fallback blade, and a summon-staff primary keeps the only flock (a sentry sidearm stays). The sidearm goes back to the shelf; IssueGear then picks a fitting one.</summary>
		private void ShelveSidearmForPrimary(SoldierGear g, Item neu, bool classChanged)
		{
			if (g.IsLocked(SoldierGear.SlotSidearm) || IsAir(g.Sidearm)) return;
			var nf = WeaponClassifier.GetProfile(neu).Family;
			var sf = WeaponClassifier.GetProfile(g.Sidearm).Family;
			// A melee-type primary carries no fallback blade: a blade left from a ranged or magic kit goes back to the shelf.
			// A summon weapon is never carried twice in the same role: a second whip, a second staff or a second sentry goes back to the shelf, and IssueGear re-picks the complementary one.
			if (!((classChanged && IsMeleeTypeFamily(nf) && IsMeleeFamily(sf)) || (nf == WeaponFamily.Summon && sf == WeaponFamily.Summon) || (nf == WeaponFamily.Whip && sf == WeaponFamily.Whip) || (nf == WeaponFamily.Sentry && sf == WeaponFamily.Sentry))) return;
			var rem = Deposit(g.Sidearm);
			if (!IsAir(rem)) Spill(rem, protect: true);
			g.Sidearm = new Item();
		}

		/// <summary>
		/// Coherence post-condition, fluid mode only: when the armor he wears favours a damage class (<see cref="ArmorDoctrine.PreferredClass"/>) and his weapon is
		/// another, the weapon is swapped (old one filed in storage, protected spill if full) for the best issuable weapon of the armor's class. No such weapon
		/// in stock, no class on the armor, a locked or empty weapon slot, or fixed-class issue (which picks from the armor already) = untouched; never leaves the hand empty. True if swapped.
		/// </summary>
		private bool ReconcileWeaponToArmor(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null || Common.Config.LevyConfig.Get.ClassMatchIssue || g.IsLocked(SoldierGear.SlotWeapon) || !g.HasWeapon) return false;
			var armorCls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
			if (armorCls == null) return false;
			if (WeaponClassifier.ItemDamageClass(g.Weapon) == armorCls) return ReconcileWithinClass(soldier, armorCls);
			// Drawn while the old weapon is still in his hand, so the pick cannot be the one being returned; the swap files it afterwards.
			// A hand-assigned pouch is never swapped, so the replacement must fire it (or use no ammo); same guard as UpgradePrimary.
			var pouch = g.Ammo;
			bool pouchFixed = g.IsLocked(SoldierGear.SlotAmmo) && !IsAir(pouch);
			var neu = TakeBestWeaponOfClass(soldier, armorCls, pouchFixed ? (it => it.useAmmo <= 0 || pouch.ammo == it.useAmmo) : null);
			if (neu == null) return false;
			var old = g.Weapon;
			SwapIn(g, SoldierGear.SlotWeapon, neu);
			ShelveSidearmForPrimary(g, neu, WeaponClassifier.ItemDamageClass(old) != armorCls);
			return true;
		}

		/// <summary>
		/// The weapon already has the armor's class; refines WHICH weapon of it: a summoner's whip or sentry yields to a minion staff (it becomes the sidearm), and a Shroomite
		/// helmet's ranged subtype (<see cref="RangedSteer"/>) swaps an off-subtype gun / bow / launcher for the best of the favoured one. Nothing in stock = untouched. True if swapped.
		/// </summary>
		private bool ReconcileWithinClass(SoldierRecord soldier, DamageClass armorCls)
		{
			var g = soldier.Gear;
			var old = g.Weapon;
			var pouch = g.Ammo;
			bool pouchFixed = g.IsLocked(SoldierGear.SlotAmmo) && !IsAir(pouch); // same guard as ReconcileWeaponToArmor
			Func<Item, bool> pouchOk = pouchFixed ? (it => it.useAmmo <= 0 || pouch.ammo == it.useAmmo) : null;
			Item neu = null;
			var oldFam = WeaponClassifier.GetProfile(old).Family;
			if (armorCls == DamageClass.Summon && (oldFam == WeaponFamily.Whip || oldFam == WeaponFamily.Sentry))
				neu = TakeBestWeaponOfClass(soldier, DamageClass.Summon, pouchOk); // a minion staff supersedes a whip or a sentry in the primary slot; the old one is filed and re-picked as the off-hand
			else if (armorCls == DamageClass.Ranged)
			{
				var steer = RangedSteer(soldier);
				if (steer == null || steer(old)) return false;
				neu = TakeBestWeaponOfClass(soldier, DamageClass.Ranged, pouchOk == null ? steer : (it => steer(it) && pouchOk(it))); // PrimaryRule inside agrees with the same steer
			}
			if (neu == null) return false;
			SwapIn(g, SoldierGear.SlotWeapon, neu);
			ShelveSidearmForPrimary(g, neu, false);
			return true;
		}

		/// <summary>True when this armory (issue ammo row, storage, networked armories; a Magic Storage network counts as plenty) holds at least <paramref name="min"/> of the ammo type.</summary>
		private bool AmmoStockAtLeast(int ammoId, int min)
		{
			int n = 0;
			foreach (var it in IssueAmmo) if (!IsAir(it) && it.ammo == ammoId && (n += it.stack) >= min) return true;
			foreach (var it in Storage) if (!IsAir(it) && it.ammo == ammoId && (n += it.stack) >= min) return true;
			foreach (var o in OtherNetworked()) foreach (var it in o.Storage) if (!IsAir(it) && it.ammo == ammoId && (n += it.stack) >= min) return true;
			var ms = MsHeart();
			return ms != null && MagicStorageBridge.AmmoIds(ms).Contains(ammoId);
		}

		private bool UpgradeSidearm(SoldierRecord soldier)
		{
			var g = soldier.Gear;
			var cur = g.Sidearm;
			if (IsAir(cur)) return false; // empty hand: IssueGear's job
			var fam = WeaponClassifier.GetProfile(cur).Family;
			bool summoner = IsSummonerSoldier(soldier);
			float need = WeaponClassifier.Score(cur) * LevyConstants.AutoUpgradeScoreMargin;
			Func<Item, bool> pool, shelf = null; // pool = the role (and the dedicated sidearm slot); shelf = what the armory's racks may supply for it
			// Summon-class off-hands are a summoner's only (the reservation): a sentry kit wants a sentry in hand, else each summon role upgrades within itself.
			if (summoner && ArmorDoctrine.HasSentryLean(g.Accessories)) { pool = IsIssuableSentry; if (fam != WeaponFamily.Sentry) need = 0f; }
			else if (summoner && fam == WeaponFamily.Sentry) pool = IsIssuableSentry;
			else if (summoner && fam == WeaponFamily.Whip) pool = IsIssuableWhip;
			else if (summoner && fam == WeaponFamily.Summon) pool = IsIssuableSummonStaff;
			else if (IsMeleeFamily(fam)) { pool = IsIssuableMelee; shelf = LesserBlade(); } // a blade sidearm only upgrades to another lesser blade, never to the top of the shelf
			else return false;
			shelf ??= pool;
			bool Fits(Item it, Func<Item, bool> role) => !IsReservedRef(it) && role(it) && WeaponClassifier.Score(it) >= need;
			// Same order as the initial pick: the dedicated sidearm slot first, then the best in the weapon row / storage / networked armories.
			Item neu = Fits(IssueWeapons[SidearmSlot], pool) ? Remove(IssueWeapons, SidearmSlot) : null;
			neu ??= TakeBest(IssueWeapons, 0, SidearmSlot, 1, it => Fits(it, shelf), WeaponClassifier.Score);
			return neu != null && SwapIn(g, SoldierGear.SlotSidearm, neu);
		}

		/// <summary>Puts <paramref name="neu"/> (already removed from the armory) in the soldier's slot and files the old weapon in storage; if storage is full it spills protected, so nothing is ever lost.</summary>
		private bool SwapIn(SoldierGear g, int slot, Item neu)
		{
			var old = g.Get(slot);
			g.Set(slot, neu);
			if (slot == SoldierGear.SlotWeapon) g.ThrownRestockType = 0;
			var rem = Deposit(old);
			if (!IsAir(rem)) Spill(rem, protect: true);
			MarkDirty();
			return true;
		}

		/// <summary>
		/// Returns a soldier's whole kit to storage (stand down / discharge / softcore death / the first half of a Re-arm). Locks do not stop
		/// the return (they only guard against auto-issue) and are cleared with the gear. The one exception is bookkeeping for Re-arm: the
		/// hand-assigned items are remembered for the rest of this tick so an IssueGear in the same tick hands the same items back
		/// (see <see cref="ReclaimLocked"/>); any other caller never issues in the same tick, so for them the items just stay returned.
		/// </summary>
		public virtual void ReturnGear(SoldierGear gear)
		{
			if (gear == null) return;
			Item[] kept = null;
			if (gear.LockMask != 0) {
				kept = new Item[SoldierGear.SlotCount];
				for (int s = 0; s < SoldierGear.SlotCount; s++) {
					if (!gear.IsLocked(s) || IsAir(gear.Get(s))) continue;
					// Ammo is stackable and may merge into storage, so remember a copy; everything else keeps its instance.
					kept[s] = IsStackSlot(s, gear.Get(s)) ? gear.Get(s).Clone() : gear.Get(s);
				}
			}
			var items = new List<Item>(gear.AllItems());
			foreach (var it in items) {
				var rem = Deposit(it);
				if (!IsAir(rem)) Spill(rem, protect: true); // storage truly full: never lose gear, keep it on the ground, protected from slot recycling
			}
			gear.Clear();
			if (kept != null) {
				gear.Reserved = kept; gear.ReservedTick = Main.GameUpdateCount;
				for (int s = 0; s < kept.Length; s++) if (kept[s] != null && !IsStackSlot(s, kept[s])) Reserve(kept[s]);
			}
			MarkDirty();
		}

		/// <summary>
		/// Drops an item in front of the armory (server / single player only). With <paramref name="protect"/> the ground item is moved to the back of
		/// Terraria's item-slot recycling queue (see <see cref="LevyConstants.ArmorySpillProtectedAge"/>), so it stays until someone picks it up.
		/// </summary>
		private void Spill(Item item, bool protect = false)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || IsAir(item)) return;
			int idx = Item.NewItem(new EntitySource_TileEntity(this), new Vector2(Position.X * 16f, Position.Y * 16f),
				new Vector2(LevyConstants.ArmorySpillWidth, LevyConstants.ArmorySpillHeight), item);
			if (protect && idx >= 0 && idx < Main.maxItems && Main.item[idx].active)
				Main.item[idx].timeSinceItemSpawned = LevyConstants.ArmorySpillProtectedAge;
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

		/// <summary>True while any storage or issue slot holds an item — used to block breaking a stocked Armory (chest rule).</summary>
		public bool HasAnyContents
		{
			get
			{
				foreach (var it in Storage) if (!IsAir(it)) return true;
				foreach (var it in IssueWeapons) if (!IsAir(it)) return true;
				foreach (var it in IssueArmor) if (!IsAir(it)) return true;
				foreach (var it in IssueAmmo) if (!IsAir(it)) return true;
				foreach (var it in IssuePotions) if (!IsAir(it)) return true;
				return false;
			}
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
			// Storage is saved at its full (possibly enlarged) length; a save from before centers existed holds exactly StorageSize. The array is sized to what was saved so nothing is lost
			// before the Logistical Centers have loaded; the first reconcile after load trims any empty tail that no center backs any more.
			Storage = new Item[Math.Max(StorageSize, tag.GetList<TagCompound>("storage").Count)];
			IssuePotions = new Item[Math.Max(PotionSlots, tag.GetList<TagCompound>("potions").Count)]; // old saves hold exactly PotionSlots; FitPotions grows it if Firepower is on
			LoadList(tag, "storage", Storage); LoadList(tag, "weapons", IssueWeapons); LoadList(tag, "armor", IssueArmor); LoadList(tag, "ammo", IssueAmmo); LoadList(tag, "potions", IssuePotions);
			FitPotions(); // the world's guide flag may already be loaded; if not, the periodic reconcile in LogisticsSystem catches it
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
			writer.Write(Storage.Length); // storage grows with Logistical Centers and the potion row with Firepower, so their lengths lead
			writer.Write(IssuePotions.Length);
			foreach (var arr in new[] { Storage, IssueWeapons, IssueArmor, IssueAmmo, IssuePotions }) foreach (var it in arr) ItemIO.Send(it, writer, true);
		}
		public override void NetReceive(BinaryReader reader)
		{
			int storageLength = reader.ReadInt32();
			if (storageLength < StorageSize || storageLength > LevyConstants.LogisticsMaxStorageSlots) storageLength = StorageSize; // a corrupt length must not allocate gigabytes
			if (Storage.Length != storageLength) Storage = new Item[storageLength]; // replaced, not resized: the panel rebuilds its cells against the new reference
			int potionLength = reader.ReadInt32();
			if (potionLength < PotionSlots || potionLength > LevyConstants.MaxPotionSlotsOnWire) potionLength = PotionSlots;
			if (IssuePotions.Length != potionLength) IssuePotions = new Item[potionLength]; // replaced, not resized: the panel rebuilds its potion row against the new reference
			foreach (var arr in new[] { Storage, IssueWeapons, IssueArmor, IssueAmmo, IssuePotions }) for (int i = 0; i < arr.Length; i++) arr[i] = ItemIO.Receive(reader, true);
		}
	}
}
