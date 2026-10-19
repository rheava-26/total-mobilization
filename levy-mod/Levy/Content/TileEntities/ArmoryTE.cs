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
		/// <summary>Bulk storage slots (fixed array; the panel scrolls). Sized so a full hoard of non-stacking gear (40+ armor sets, hundreds of weapons, ammo) never overflows.</summary>
		public const int StorageSize = 1000, WeaponSlots = 12, ArmorSets = 4, AmmoSlots = 6, PotionSlots = 2;
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

		private struct ArmorCand { public ArmoryTE Src; public Item[] Arr; public int Idx; public Item It; public float Score; public string Stem; public bool Fixed; public bool Ms; }

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
		public virtual Item TakeBestWeapon(SoldierRecord forSoldier)
		{
			// Pass 1: weapons that can actually fire (no ammo needed, or ammo in the pouch / armory). Pass 2: anything issuable.
			var onHand = AmmoIdsOnHand();
			return CapThrown(TakeVaried(forSoldier, it => IsIssuable(it) && HasAmmoFor(it, forSoldier, onHand))
				?? TakeVaried(forSoldier, IsIssuable));
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
		public virtual Item TakeBestWeaponOfClass(SoldierRecord forSoldier, DamageClass cls)
		{
			if (cls == null) return null;
			var onHand = AmmoIdsOnHand();
			// Summoners want the summon weapon itself as primary; their whip comes through the sidearm slot.
			bool needSummon = cls == DamageClass.Summon;
			bool Pred(Item it) => IsIssuable(it) && WeaponClassifier.ItemDamageClass(it) == cls && HasAmmoFor(it, forSoldier, onHand)
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

		/// <summary>Best issuable sidearm (melee item in the sidearm slot or any supported melee); removes it. Null if none.</summary>
		public virtual Item TakeSidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableMelee(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableMelee, WeaponClassifier.Score);
		}

		private static bool IsIssuableWhip(Item it) => IsIssuable(it) && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Whip;

		/// <summary>A summoner's sidearm: a whip (the sidearm slot first, then the best in the armory), else the usual melee sidearm. Removes it. Null if none.</summary>
		public virtual Item TakeSummonerSidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableWhip(IssueWeapons[SidearmSlot]) && !IsReservedRef(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableWhip, WeaponClassifier.Score) ?? TakeSidearm(forSoldier);
		}

		private static bool IsIssuableSummonStaff(Item it) => IsIssuable(it) && it.shoot > 0 && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Summon;

		private static bool IsIssuableSentry(Item it) => IsIssuable(it) && it.shoot > 0 && WeaponClassifier.GetProfile(it).Family == WeaponFamily.Sentry;
		/// <summary>A deploy-and-forget utility weapon: a minion-summoning staff or a sentry.</summary>
		private static bool IsIssuableDeployer(Item it) => IsIssuableSummonStaff(it) || IsIssuableSentry(it);

		/// <summary>A melee-type soldier's sidearm: a minion-summoning staff or a sentry (the sidearm slot first, then the best in the armory). Never a blade. Removes it. Null if none.</summary>
		public virtual Item TakeSummonSidearm(SoldierRecord forSoldier)
		{
			if (IsIssuableDeployer(IssueWeapons[SidearmSlot]) && !IsReservedRef(IssueWeapons[SidearmSlot])) return Remove(IssueWeapons, SidearmSlot);
			return TakeBest(IssueWeapons, 0, SidearmSlot, 1, IsIssuableDeployer, WeaponClassifier.Score);
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

		private bool TakeArmorSetCore(SoldierRecord soldier, bool usePool)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			int[] slot = { SoldierGear.SlotHead, SoldierGear.SlotBody, SoldierGear.SlotLegs };
			Item[] cur = { g.Head, g.Body, g.Legs };
			var need = new bool[ArmorParts]; bool anyNeed = false;
			for (int p = 0; p < ArmorParts; p++) { need[p] = !g.IsLocked(slot[p]) && IsAir(cur[p]); anyNeed |= need[p]; }
			if (!anyNeed) return false;

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

			DamageClass wc = null;
			if (Common.Config.LevyConfig.Get.ClassMatchIssue && g.HasWeapon) wc = WeaponClassifier.ItemDamageClass(g.Weapon);

			float bestScore = float.NegativeInfinity;
			ArmorCand[] best = null;
			void EvalCombos(List<ArmorCand>[] opts, bool requireBonus)
			{
				foreach (var h in opts[0]) foreach (var b in opts[1]) foreach (var l in opts[2]) {
					bool full = h.It != null && b.It != null && l.It != null;
					bool bonus = full && ArmorDoctrine.FormsSetBonus(h.It, b.It, l.It);
					if (requireBonus && !bonus) continue;
					float sc = h.Score + b.Score + l.Score;
					if (bonus) sc += LevyConstants.ArmorSetBonusScore;
					if (wc != null && ArmorDoctrine.PreferredClass(h.It, b.It, l.It) == wc) sc += LevyConstants.ArmorSetClassMatchScore;
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

		/// <summary>Takes one potion of the given issue kind (0 healing, 1 buff). Null if none.</summary>
		public virtual Item TakePotion(int kind)
		{
			if (kind < 0 || kind >= PotionSlots) return null;
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
			if (preferSlot >= 0 && preferSlot < PotionSlots && !IsAir(IssuePotions[preferSlot]) && serves(IssuePotions[preferSlot]))
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
		/// Auto-issue best-first: armor, weapon (matching the armor's damage class when possible), sidearm, accessories (grade slots), ammo.
		/// Slots the player hand-assigned (<see cref="SoldierGear.IsLocked"/>) are never replaced or emptied. Returns true if anything changed.
		/// </summary>
		public virtual bool IssueGear(SoldierRecord soldier)
		{
			var g = soldier?.Gear;
			if (g == null) return false;
			bool changed = false;

			// A Re-arm in this very tick parked this soldier's hand-assigned items: take exactly those back first.
			if (g.Reserved != null && ReclaimLocked(g, soldier.AccessorySlots)) changed = true;

			// One matching set first (set bonus, aligned with his weapon); anything it could not supply is filled piece by piece below.
			if (TakeArmorSet(soldier)) changed = true;

			if (!g.IsLocked(SoldierGear.SlotHead) && IsAir(g.Head)) { var a = TakeArmor(soldier, 0); if (a != null) { g.Head = a; changed = true; } }
			if (!g.IsLocked(SoldierGear.SlotBody) && IsAir(g.Body)) { var a = TakeArmor(soldier, 1); if (a != null) { g.Body = a; changed = true; } }
			if (!g.IsLocked(SoldierGear.SlotLegs) && IsAir(g.Legs)) { var a = TakeArmor(soldier, 2); if (a != null) { g.Legs = a; changed = true; } }

			// A thrown stack is kept supplied: topped up while he holds it, handed back first when it ran dry.
			if (RestockThrown(soldier)) changed = true;

			if (!g.IsLocked(SoldierGear.SlotWeapon) && !g.HasWeapon)
			{
				// The armor decides doctrine: prefer a weapon of its class, else the best of anything.
				Item w = null;
				if (Common.Config.LevyConfig.Get.ClassMatchIssue)
				{
					var cls = ArmorDoctrine.PreferredClass(g.Head, g.Body, g.Legs);
					if (cls != null) w = TakeBestWeaponOfClass(soldier, cls);
				}
				w ??= TakeBestWeapon(soldier);
				if (w != null) { g.Weapon = w; changed = true; }
			}

			// A melee-type primary (blade, spear, boomerang, whip) never takes a fallback blade: it takes a summon staff for a support minion, or nothing.
			var primaryFam = g.HasWeapon ? WeaponClassifier.Classify(g.Weapon) : WeaponFamily.None;
			bool primaryMelee = IsMeleeTypeFamily(primaryFam);
			bool primarySummon = primaryFam == WeaponFamily.Summon; // the flock fights; the hand carries a whip
			int slots = Math.Clamp(soldier.AccessorySlots, 0, Math.Min(SoldierGear.AccessorySlots, g.Accessories.Length));
			for (int i = 0; i < slots; i++) {
				if (g.IsLocked(SoldierGear.SlotAcc0 + i) || !IsAir(g.Accessories[i])) continue;
				var a = TakeAccessory(soldier);
				if (a == null) break;
				g.Accessories[i] = a; changed = true;
			}

			// The sidearm is chosen after the accessories so the kit can steer it (accessory choice never reads the sidearm). Sentry-capacity / summoner
			// accessories put a sentry in the hand whatever the primary is; then the usual pick for the primary's type.
			if (!g.IsLocked(SoldierGear.SlotSidearm) && !g.HasSidearm)
			{
				bool sentryKit = ArmorDoctrine.HasSentryLean(g.Accessories);
				var s = sentryKit ? TakeSentrySidearm(soldier) : null;
				s ??= primaryMelee ? TakeSummonSidearm(soldier)
					: primarySummon ? TakeSummonerSidearm(soldier)
					: sentryKit ? (TakeSummonSidearm(soldier) ?? TakeSidearm(soldier)) // no sentry on hand: a summon staff before a blade
					: TakeSidearm(soldier);
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
