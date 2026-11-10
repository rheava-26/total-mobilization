using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>How mass-sell picks items out of the armory's storage.</summary>
	public enum SellCat : byte { Melee = 1, Ranged, Magic, Summon, Armor, Accessory, Ammo, Potion, Other }

	/// <summary>
	/// Mass sell: picks storage slots (by drag, by category, by progression tier, or any mix), finds the shopkeeper on the pylon network who pays the most for the lot (the happiest one), and sells to him.
	/// The money goes to the armory's barracks treasury (coins into storage when there is none). Optionally items that can be shimmered apart come back as their ingredients instead of coin.
	/// </summary>
	public static class Selling
	{
		public static bool Sellable(Item it) => it != null && !it.IsAir && !it.favorited && it.value > 0 && it.type != ItemID.CopperCoin && it.type != ItemID.SilverCoin && it.type != ItemID.GoldCoin && it.type != ItemID.PlatinumCoin;

		public static SellCat CategoryOf(Item it)
		{
			if (it.damage > 0 && it.ammo > 0) return SellCat.Ammo;
			if (it.damage > 0 && !it.accessory && it.maxStack == 1 || it.damage > 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && it.maxStack == 1)
			{
				var c = WeaponClassifier.ItemDamageClass(it);
				if (c == DamageClass.Melee || c == DamageClass.MeleeNoSpeed || c.CountsAsClass(DamageClass.Melee)) return SellCat.Melee;
				if (c == DamageClass.Ranged || c.CountsAsClass(DamageClass.Ranged)) return SellCat.Ranged;
				if (c == DamageClass.Magic || c.CountsAsClass(DamageClass.Magic)) return SellCat.Magic;
				if (c == DamageClass.Summon || c.CountsAsClass(DamageClass.Summon)) return SellCat.Summon;
			}
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) return SellCat.Armor;
			if (it.accessory) return SellCat.Accessory;
			if (it.consumable && (it.buffType > 0 || it.healLife > 0 || it.healMana > 0)) return SellCat.Potion;
			return SellCat.Other;
		}

		/// <summary>What to select. Every set criterion must hold (a category AND a tier band); with nothing set it selects nothing.</summary>
		public sealed class Filter
		{
			public SellCat? Category;
			public float TierLo = -1, TierHi = -1;
			public bool Any => Category != null || TierHi >= 0;
			public bool Matches(Item it)
			{
				if (!Sellable(it) || !Any) return false;
				if (Category != null && CategoryOf(it) != Category.Value) return false;
				if (TierHi >= 0 && !OrderMath.InTierBand(ProgressionTier.Of(it.type), TierLo, TierHi)) return false;
				return true;
			}
		}

		/// <summary>The progression bands the screen offers, on Boss Checklist's scale. (name key, low, high)</summary>
		public static readonly (string Key, float Lo, float Hi)[] Tiers =
		{
			("PreHardmode", 0f, 7f), ("EarlyHardmode", 7.01f, 11f), ("LateHardmode", 11.01f, 15f), ("Endgame", 15.01f, 99f),
		};

		/// <summary>Storage indices matching the filter.</summary>
		public static List<int> Select(ArmoryTE a, Filter f)
		{
			var res = new List<int>();
			for (int i = 0; i < a.Storage.Length; i++) if (f.Matches(a.Storage[i])) res.Add(i);
			return res;
		}

		/// <summary>Storage indices in the grid rectangle spanned by two cells (drag select). The grid is <paramref name="columns"/> wide and indexes storage row by row.</summary>
		public static List<int> SelectRect(ArmoryTE a, int columns, int ca, int ra, int cb, int rb)
		{
			var (c0, r0, c1, r1) = OrderMath.DragCells(ca, ra, cb, rb);
			var res = new List<int>();
			for (int r = r0; r <= r1; r++)
				for (int c = c0; c <= c1; c++) { int i = r * columns + c; if (i >= 0 && i < a.Storage.Length && Sellable(a.Storage[i])) res.Add(i); }
			return res;
		}

		public static long SellValue(Player p, NPC buyer, Item it)
		{
			try { p.currentShoppingSettings = Main.ShopHelper.GetShoppingSettings(p, buyer); p.GetItemExpectedPrice(it, out long sell, out _); return Math.Max(0, sell); }
			catch (Exception) { return Math.Max(0, it.value / 5L * it.stack); }
		}

		/// <summary>The shopkeeper who pays the most for these slots (ties go to the nearer one), and what he pays. Null buyer when no shopkeeper is in reach.</summary>
		public static (NPC buyer, long total) BestBuyer(ArmoryTE a, IList<int> slots) => BestBuyer(a, slots.Select(i => a.Storage[i]).ToList());

		/// <summary>The same for a list of items from anywhere (a clear-out's chests and network, not only storage slots).</summary>
		public static (NPC buyer, long total) BestBuyer(ArmoryTE a, IList<Item> items)
		{
			var payer = Shopping.Payer(a); NPC best = null; long bestTotal = -1;
			if (payer == null) return (null, 0);
			foreach (var k in Shopping.Keepers(a))
			{
				long t = 0; foreach (var it in items) t += SellValue(payer, k, it);
				if (t > bestTotal || t == bestTotal && best != null && Math.Abs(k.Center.X - a.Center.X) < Math.Abs(best.Center.X - a.Center.X)) { best = k; bestTotal = t; }
			}
			return (best, Math.Max(0, bestTotal));
		}

		public sealed class Result { public int Items, Stacks, Shimmered; public long Coin; public string Buyer = ""; }

		/// <summary>Sells the slots to the best buyer. <paramref name="shimmerBack"/>: an item with a shimmer decrafting recipe is taken apart into its ingredients (deposited back) instead of sold. Server / single player.</summary>
		public static Result Run(ArmoryTE a, IList<int> slots, bool shimmerBack)
		{
			var res = new Result();
			var live = slots.Where(i => i >= 0 && i < a.Storage.Length && Sellable(a.Storage[i])).Distinct().ToList();
			if (live.Count == 0) return res;
			var toShimmer = new List<int>();
			if (shimmerBack) foreach (int i in live) if (DecraftOf(a.Storage[i].type) != null) toShimmer.Add(i);
			var toSell = live.Except(toShimmer).ToList();
			var (buyer, total) = BestBuyer(a, toSell);
			if (toSell.Count > 0 && buyer == null) { toSell.Clear(); total = 0; } // nobody to sell to: the shimmer-back part still goes through
			var payer = Shopping.Payer(a);
			foreach (int i in toSell) { res.Items += a.Storage[i].stack; res.Stacks++; a.Storage[i] = new Item(); }
			foreach (int i in toShimmer)
			{
				var it = a.Storage[i]; var rec = DecraftOf(it.type); if (rec == null) continue;
				int crafts = it.stack / Math.Max(1, rec.createItem.stack); if (crafts <= 0) continue;
				a.Storage[i] = new Item();
				foreach (var ing in rec.requiredItem) { if (ing == null || ing.IsAir) continue; var o = new Item(); o.SetDefaults(ing.type); o.stack = ing.stack * crafts; Spill(a, o); }
				res.Shimmered += it.stack; res.Stacks++;
			}
			if (total > 0) { Pay(a, total); res.Coin = total; res.Buyer = Shopping.KeeperLabel(a, buyer); }
			a.MarkDirty();
			return res;
		}

		/// <summary>The recipe shimmer takes <paramref name="type"/> apart by, or null.</summary>
		public static Recipe DecraftOf(int type)
		{
			try { int idx = ShimmerTransforms.GetDecraftingRecipeIndex(type); return idx >= 0 && idx < Recipe.maxRecipes ? Main.recipe[idx] : null; }
			catch (Exception) { return null; }
		}

		private static void Spill(ArmoryTE a, Item o)
		{
			while (!o.IsAir)
			{
				var part = o.Clone(); part.stack = Math.Min(o.stack, Math.Max(1, o.maxStack)); o.stack -= part.stack;
				var rest = a.Deposit(part);
				if (rest != null && !rest.IsAir) Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("Levy.Logistics"), a.Center, rest);
			}
		}

		internal static void Pay(ArmoryTE a, long copper)
		{
			var b = a.LinkedBarracks();
			if (b != null) { EconomySystem.Deposit(b, copper); RosterSystem.SyncBarracks(b); return; }
			int[] split = Utils.CoinsSplit(copper); int[] types = { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };
			for (int i = 0; i < types.Length; i++) if (split[i] > 0) { var c = new Item(); c.SetDefaults(types[i]); c.stack = split[i]; Spill(a, c); }
		}
	}
}
