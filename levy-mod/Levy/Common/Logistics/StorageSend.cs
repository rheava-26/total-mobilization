using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Levy.Common.Movement;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// SEND TO STORAGE. Moves items out of the armory into the Magic Storage network it is joined to, or, with no network, into the chests beside it (nearest first). Two ways in: the slots the player selected on the Sell page
	/// (<see cref="SendSlots"/>), and "everything the army has no use for" (<see cref="Build"/> shows the list first, <see cref="Execute"/> moves it). The army has a use for the weapons, armor, accessories, ammo, potions, hooks,
	/// dyes, crystals and cameras a levy can carry; coins, favorited items and anything reserved for a levy are never moved. One pass over the slots, each kind of item classified once.
	/// </summary>
	public static class StorageSend
	{
		/// <summary>Where the items go: the network, or the chests beside the armory. Null Stocks = nowhere to send.</summary>
		public sealed class Target
		{
			public byte Kind; // 0 none, 1 Magic Storage network, 2 chests
			public readonly List<IStockItems> Stocks = new();
			public bool Any => Stocks.Count > 0;

			/// <summary>Puts the item into the first stock that takes it; what fits nowhere comes back.</summary>
			public Item Put(Item item)
			{
				foreach (var s in Stocks)
				{
					if (item == null || item.IsAir) return null;
					item = s.Put(item);
				}
				return item == null || item.IsAir ? null : item;
			}
		}

		/// <summary>Self-check seam: when set, every armory sends to this target (an armory that is not placed in a world has no network and no chests beside it).</summary>
		internal static Func<ArmoryTE, Target> TargetOverride;

		public static Target TargetFor(ArmoryTE a)
		{
			if (TargetOverride != null) return TargetOverride(a);
			var t = new Target();
			if (a == null) return t;
			if (NetworkStocks.For(a) is IStockItems net) { t.Kind = 1; t.Stocks.Add(net); return t; }
			var chests = StockPool.NearbyChests(a);
			if (chests.Count == 0) return t;
			chests.Sort((x, y) => Dist(a, x).CompareTo(Dist(a, y)));
			t.Kind = 2;
			foreach (int c in chests) t.Stocks.Add(new ChestStock(c));
			return t;
		}

		private static float Dist(ArmoryTE a, int chest) { var c = Main.chest[chest]; return c == null ? float.MaxValue : Microsoft.Xna.Framework.Vector2.DistanceSquared(new Microsoft.Xna.Framework.Vector2(c.x * 16f + 16f, c.y * 16f + 16f), a.Center); }

		/// <summary>True when a levy can make use of the item: a weapon, armor piece, useful accessory, ammo, potion, hook, dye, crystal or camera. Decided by the item's kind alone.</summary>
		public static bool ArmyUses(Item it)
		{
			if (it == null || it.IsAir || it.IsACoin) return false; // a coin counts as ammo for the coin gun, and is the player's money
			if (WeaponClassifier.IsIssuableWeapon(it)) return true;
			if (it.damage > 0 && !it.accessory && !it.consumable && it.pick == 0 && it.axe == 0 && it.hammer == 0 && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0) return true; // any other weapon-shaped item
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) return !it.vanity;
			if (it.accessory) return WeaponClassifier.IsUsefulAccessory(it);
			if (it.ammo > 0) return true;
			if (HookInfo.IsHookItem(it)) return true;
			if (it.dye > 0) return true;
			if (it.consumable && (it.healLife > 0 || it.healMana > 0 || it.buffType > 0)) return true;
			if (it.type == ItemID.LifeCrystal || it.type == ItemID.LifeFruit || it.type == ItemID.ManaCrystal) return true;
			if (it.ModItem is global::Levy.Content.Items.Observe.GoProItem) return true;
			return false;
		}

		/// <summary>Never moved, whatever the player picked: coins, favorited items and anything reserved for a levy or a kind the player prefers.</summary>
		public static bool Movable(ArmoryTE a, Item it) => it != null && !it.IsAir && it.stack > 0 && !it.IsACoin && !it.favorited && !a.ClearOutProtected(it);

		/// <summary>One kind (type and prefix) of the list shown before a send.</summary>
		public sealed class Preview
		{
			public int ArmoryId, Hash, Items, Stacks, RowTotal; public byte TargetKind; public bool Stale;
			public List<ClearOut.Row> Rows = new();
			public bool Empty => Items == 0;

			public void Write(BinaryWriter w)
			{
				w.Write(ArmoryId); w.Write(Hash); w.Write(Items); w.Write(Stacks); w.Write(RowTotal); w.Write(TargetKind); w.Write(Stale); w.Write((ushort)Rows.Count);
				foreach (var r in Rows) { w.Write(r.Type); w.Write(r.Prefix); w.Write(r.Count); }
			}

			public static Preview Read(BinaryReader r)
			{
				var p = new Preview { ArmoryId = r.ReadInt32(), Hash = r.ReadInt32(), Items = r.ReadInt32(), Stacks = r.ReadInt32(), RowTotal = r.ReadInt32(), TargetKind = r.ReadByte(), Stale = r.ReadBoolean() };
				int n = r.ReadUInt16();
				for (int i = 0; i < n; i++) p.Rows.Add(new ClearOut.Row { Type = r.ReadInt32(), Prefix = r.ReadByte(), Count = r.ReadInt32() });
				return p;
			}
		}

		/// <summary>The latest list this client holds (single player: computed here; multiplayer: sent by the server).</summary>
		public static Preview Latest;
		public static int Version;
		public static void SetLatest(Preview p) { Latest = p; Version++; }

		/// <summary>The storage slots holding something the army has no use for, and the movable ones only. One pass; each item type is classified once.</summary>
		public static List<int> NonArmySlots(ArmoryTE a)
		{
			var res = new List<int>(); var memo = new Dictionary<int, bool>();
			var st = a.Storage;
			for (int i = 0; i < st.Length; i++)
			{
				var it = st[i];
				if (it == null || it.IsAir || it.stack <= 0 || it.IsACoin || it.favorited) continue;
				if (!memo.TryGetValue(it.type, out bool uses)) memo[it.type] = uses = ArmyUses(it);
				if (uses || a.ClearOutProtected(it)) continue;
				res.Add(i);
			}
			return res;
		}

		/// <summary>The list a "send the rest to storage" would move right now, grouped by kind, with a fingerprint the confirm checks.</summary>
		public static Preview Build(ArmoryTE a)
		{
			var target = TargetFor(a);
			var slots = NonArmySlots(a);
			var p = new Preview { ArmoryId = a.ID, TargetKind = target.Kind, Stacks = slots.Count };
			var rows = new Dictionary<(int, int), ClearOut.Row>(); long sum = 0;
			foreach (int i in slots)
			{
				var it = a.Storage[i]; var k = (it.type, (int)it.prefix); rows.TryGetValue(k, out var r);
				rows[k] = new ClearOut.Row { Type = it.type, Prefix = (byte)it.prefix, Count = r.Count + it.stack };
				p.Items += it.stack; sum += it.type * 31L + it.prefix * 7L + it.stack;
			}
			p.Hash = unchecked((int)(sum * 2654435761L) ^ slots.Count);
			var all = rows.Values.OrderByDescending(r => r.Count).ToList();
			p.RowTotal = all.Count; p.Rows = all.Take(ClearOut.MaxPreviewRows).ToList();
			return p;
		}

		public sealed class Result { public int Items, Stacks, Left; public bool Stale, NoTarget; public byte TargetKind; }

		/// <summary>Moves the non-army list the player confirmed. Nothing moves when the stock changed since the list was shown (<paramref name="expectHash"/>) or there is nowhere to send it.</summary>
		public static Result Execute(ArmoryTE a, int expectHash)
		{
			var res = new Result(); var target = TargetFor(a); res.TargetKind = target.Kind;
			if (!target.Any) { res.NoTarget = true; return res; }
			if (Build(a).Hash != expectHash) { res.Stale = true; return res; }
			return Move(a, NonArmySlots(a), target, res);
		}

		/// <summary>Moves the slots the player selected (any item that can be moved, army gear included: the player chose it).</summary>
		public static Result SendSlots(ArmoryTE a, IEnumerable<int> slots)
		{
			var res = new Result(); var target = TargetFor(a); res.TargetKind = target.Kind;
			if (!target.Any) { res.NoTarget = true; return res; }
			var list = new List<int>(); var seen = new HashSet<int>();
			foreach (int i in slots) if (i >= 0 && i < a.Storage.Length && seen.Add(i) && Movable(a, a.Storage[i])) list.Add(i);
			return Move(a, list, target, res);
		}

		private static Result Move(ArmoryTE a, List<int> slots, Target target, Result res)
		{
			foreach (int i in slots)
			{
				var it = a.Storage[i]; if (it == null || it.IsAir) continue;
				int before = it.stack;
				var rest = target.Put(it);
				bool gone = rest == null || rest.IsAir;
				int left = gone ? 0 : rest.stack;
				res.Items += before - left;
				if (gone) { a.Storage[i] = new Item(); res.Stacks++; }
				else { a.Storage[i] = rest; res.Left += left; }
			}
			if (res.Items > 0) a.MarkDirty();
			return res;
		}
	}
}
