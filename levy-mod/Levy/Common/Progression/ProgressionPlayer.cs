using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Util;

namespace Levy.Common.Progression
{
	/// <summary>
	/// Per-player progression-assistance state: how many rounds of each ammo type this player has crafted or picked up (Arms Dealer bulk ammo)
	/// and the batch size picked at each bulk station.
	/// </summary>
	public class ProgressionPlayer : ModPlayer
	{
		public readonly Dictionary<string, int> AmmoObtained = new();

		public static string KeyOf(Item i) => i.ModItem != null ? i.ModItem.FullName : ItemID.Search.GetName(i.type);

		public static int TypeOfKey(string key)
		{
			if (key.Contains('/')) return ModContent.TryFind<ModItem>(key, out var m) ? m.Type : 0;
			return ItemID.Search.TryGetId(key, out int id) ? id : 0;
		}

		public void CountAmmo(Item item, int amount)
		{
			if (amount <= 0 || !ProgressionRecipes.IsAmmo(item)) return;
			string k = KeyOf(item);
			AmmoObtained.TryGetValue(k, out int n);
			AmmoObtained[k] = (int)Math.Min((long)n + amount, 1_000_000);
		}

		public override bool OnPickup(Item item)
		{
			if (Player.whoAmI == Main.myPlayer) CountAmmo(item, item.stack);
			return true;
		}

		public override void SaveData(TagCompound tag)
		{
			if (AmmoObtained.Count > 0)
				tag["ammo"] = AmmoObtained.Select(kv => new TagCompound { ["k"] = kv.Key, ["n"] = kv.Value }).ToList();
		}

		public override void LoadData(TagCompound tag)
		{
			AmmoObtained.Clear();
			foreach (var t in tag.GetList<TagCompound>("ammo")) AmmoObtained[t.GetString("k")] = t.GetInt("n");
		}

		/// <summary>Ammo this player has obtained at least <paramref name="threshold"/> rounds of, most-obtained first.</summary>
		public List<int> UnlockedAmmoTypes(int threshold)
		{
			var list = new List<(int type, int n)>();
			foreach (var kv in AmmoObtained)
			{
				if (kv.Value < threshold) continue;
				int t = TypeOfKey(kv.Key);
				if (t > 0) list.Add((t, kv.Value));
			}
			return list.OrderByDescending(x => x.n).Select(x => x.type).ToList();
		}
	}

	public class ProgressionGlobalItem : GlobalItem
	{
		public override void OnCreated(Item item, ItemCreationContext context)
		{
			if (context is not RecipeItemCreationContext rc || Main.netMode == NetmodeID.Server) return;
			Player p = Main.LocalPlayer;
			if (p == null || !p.active) return;
			var pp = p.GetModPlayer<ProgressionPlayer>();
			pp.CountAmmo(item, item.stack);
		}
	}

	/// <summary>Arms Dealer bulk ammo: ammo types the player has crafted or picked up enough of are added to the Arms Dealer's shop.</summary>
	public class ProgressionShops : GlobalNPC
	{
		public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
		{
			if (npc.type != NPCID.ArmsDealer) return;
			var c = LevyProgressionConfig.Get;
			if (c == null || !c.ArmsDealerBulkAmmo) return;
			var pp = Main.LocalPlayer?.GetModPlayer<ProgressionPlayer>();
			if (pp == null) return;
			AddBulkAmmo(items, pp.UnlockedAmmoTypes(c.BulkAmmoUnlockAmount), c.BulkAmmoPricePercent);
		}

		public static int AddBulkAmmo(Item[] items, List<int> types, int pricePercent)
		{
			var have = new HashSet<int>();
			foreach (var it in items) if (it != null && !it.IsAir) have.Add(it.type);
			int added = 0, slot = 0;
			foreach (int type in types)
			{
				if (added >= ProgConst.BulkAmmoMaxSlots || have.Contains(type)) continue;
				while (slot < items.Length && items[slot] != null && !items[slot].IsAir) slot++;
				if (slot >= items.Length) break;
				var shopItem = new Item();
				shopItem.SetDefaults(type);
				shopItem.shopCustomPrice = ProgMath.BulkAmmoPrice(shopItem.value, pricePercent);
				items[slot++] = shopItem;
				have.Add(type);
				added++;
			}
			return added;
		}
	}
}
