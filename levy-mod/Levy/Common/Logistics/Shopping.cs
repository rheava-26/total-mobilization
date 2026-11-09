using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>
	/// Shopkeepers in reach of an armory and what they sell, at the price the shopkeeper's happiness gives. A shopkeeper is in reach when he stands inside the settlement box around the armory, or (config, default on) when the armory is near a working pylon and he stands near any working pylon. Money comes from the settlement treasury (<see cref="EconomySystem.TryPayNetwork"/>) of the armory's barracks; goods go into the armory. Server and single player only.
	/// </summary>
	public static class Shopping
	{
		public struct Offer
		{
			public NPC Keeper; public string Pylon; public Item Item; public long UnitPrice; public int StackPerBuy; public bool Once;
			public override string ToString() => $"{Item?.Name} x{StackPerBuy} @{UnitPrice} from {Keeper?.GivenName}";
		}

		/// <summary>The player whose shopping settings price the goods: the nearest living player to the armory (single player: the player).</summary>
		public static Player Payer(ArmoryTE a)
		{
			if (Main.netMode == NetmodeID.SinglePlayer) return Main.LocalPlayer;
			Player best = null; float bd = float.MaxValue;
			foreach (var p in Main.player) { if (p == null || !p.active) continue; float d = Vector2.DistanceSquared(p.Center, a.Center); if (d < bd) { bd = d; best = p; } }
			return best;
		}

		/// <summary>A shopkeeper in reach; <see cref="Pylon"/> is the name of the pylon he is reached through, null when he stands in the settlement.</summary>
		public struct Reach { public NPC Keeper; public string Pylon; }

		/// <summary>True when the config lets logistics reach shopkeepers through pylons (default on).</summary>
		public static bool PylonReachOn => ModContent.GetInstance<global::Levy.Common.Config.LevyConfig>()?.LogisticsReachesPylons ?? true;

		/// <summary>
		/// The reach rule, pure. A shopkeeper standing in the settlement box is always in reach. With <paramref name="viaPylons"/> and the armory within range of a working pylon, a shopkeeper near ANY working pylon is in reach too
		/// (the pylon's name comes back). A pylon without enough town NPCs does not count at either end.
		/// </summary>
		public static (bool inReach, string pylon) ReachOf(Vector2 armory, Vector2 npc, bool inBox, IList<SettlementSystem.PylonPoint> pylons, bool viaPylons)
		{
			if (inBox) return (true, null);
			if (!viaPylons || pylons == null || SettlementSystem.NearestValidPylon(armory, pylons) < 0) return (false, null);
			int i = SettlementSystem.NearestValidPylon(npc, pylons);
			return i < 0 ? (false, null) : (true, pylons[i].Name);
		}

		/// <summary>The reach rule with the config off: the settlement box, plus any shopkeeper near any pylon when the armory itself is near one (pylon validity is not checked).</summary>
		public static bool LegacyReach(Vector2 armory, Vector2 npc, bool inBox, IList<SettlementSystem.PylonPoint> pylons)
		{
			if (inBox) return true;
			if (pylons == null) return false;
			bool Near(Vector2 c) { float lim = LevyConstants.PylonShareRadius * LevyConstants.TilePx; lim *= lim; foreach (var p in pylons) if (Vector2.DistanceSquared(c, p.Center) <= lim) return true; return false; }
			return Near(armory) && Near(npc);
		}

		/// <summary>Town NPCs with a shop that the armory's orders can trade with, and how each is reached.</summary>
		public static List<Reach> KeepersReach(ArmoryTE a, bool? viaPylons = null, IList<SettlementSystem.PylonPoint> pylons = null)
		{
			var res = new List<Reach>();
			bool via = viaPylons ?? PylonReachOn;
			pylons ??= SettlementSystem.PylonPoints();
			Vector2 anchor = a.Center;
			if (via && SettlementSystem.NearestValidPylon(anchor, pylons) < 0) // armory off to the side of the pylon: its settlement's linking pylon stands in for it
			{
				var home = a.LinkedBarracks(); var link = home == null ? default : SettlementSystem.Instance.LinkOf(home);
				if (link.Linked) anchor = new Vector2((link.Pylon.X + 1.5f) * LevyConstants.TilePx, (link.Pylon.Y + 2f) * LevyConstants.TilePx);
			}
			foreach (var n in Main.npc)
			{
				if (n == null || !n.active || !n.townNPC || n.ModNPC is SoldierNPC) continue;
				bool inBox = Math.Abs(n.Center.X / 16f - a.Position.X) <= LevyConstants.SettlementRadiusX && Math.Abs(n.Center.Y / 16f - a.Position.Y) <= LevyConstants.SettlementRadiusY;
				var (ok, py) = via ? ReachOf(anchor, n.Center, inBox, pylons, true) : (LegacyReach(a.Center, n.Center, inBox, pylons), (string)null);
				if (ok) res.Add(new Reach { Keeper = n, Pylon = py });
			}
			return res;
		}

		public static List<NPC> Keepers(ArmoryTE a) => KeepersReach(a).Select(r => r.Keeper).ToList();

		/// <summary>Copper per unit for <paramref name="item"/> from <paramref name="keeper"/> for this payer, happiness included. Items on a special currency return -1 (not buyable with coin).</summary>
		public static long UnitPrice(Player payer, NPC keeper, Item item)
		{
			if (payer == null || item == null || item.shopSpecialCurrency != -1) return -1;
			try
			{
				payer.currentShoppingSettings = Main.ShopHelper.GetShoppingSettings(payer, keeper);
				payer.GetItemExpectedPrice(item, out _, out long buy);
				return Math.Max(1, buy / Math.Max(1, item.stack));
			}
			catch (Exception) { return -1; }
		}

		public static List<Offer> OffersFrom(NPC keeper, Player payer, string pylon = null)
		{
			var res = new List<Offer>();
			if (!NPCShopDatabase.TryGetNPCShop(NPCShopDatabase.GetShopName(keeper.type), out var shop)) return res;
			var items = new List<Item>();
			try { shop.FillShop(items, keeper); } catch (Exception) { return res; }
			foreach (var it in items)
			{
				if (it == null || it.IsAir) continue;
				long unit = UnitPrice(payer, keeper, it); if (unit < 0) continue;
				res.Add(new Offer { Keeper = keeper, Pylon = pylon, Item = it, UnitPrice = unit, StackPerBuy = Math.Max(1, it.stack), Once = it.buyOnce });
			}
			return res;
		}

		public static List<Offer> AllOffers(ArmoryTE a)
		{
			var res = new List<Offer>(); var payer = Payer(a); if (payer == null) return res;
			foreach (var k in KeepersReach(a)) res.AddRange(OffersFrom(k.Keeper, payer, k.Pylon));
			return res;
		}

		/// <summary>All offers plus how many shopkeepers are in reach here (settlement) and via pylons.</summary>
		public static List<Offer> AllOffers(ArmoryTE a, out int here, out int viaPylons)
		{
			here = 0; viaPylons = 0;
			var res = new List<Offer>(); var payer = Payer(a); if (payer == null) return res;
			foreach (var k in KeepersReach(a)) { if (k.Pylon == null) here++; else viaPylons++; res.AddRange(OffersFrom(k.Keeper, payer, k.Pylon)); }
			return res;
		}

		/// <summary>The shopkeeper's name, with his pylon when he is reached through one.</summary>
		public static string KeeperLabel(ArmoryTE a, NPC keeper)
		{
			if (keeper == null) return "";
			foreach (var r in KeepersReach(a)) if (ReferenceEquals(r.Keeper, keeper)) return r.Pylon == null ? keeper.GivenName : $"{keeper.GivenName} ({r.Pylon})";
			return keeper.GivenName;
		}

		public static bool IsGun(Item it) => it.damage > 0 && it.useAmmo == AmmoID.Bullet && it.maxStack == 1;
		public static bool IsAmmo(Item it, int ammoId) => it.damage > 0 && it.ammo > 0 && (ammoId == 0 || it.ammo == ammoId);

		public static bool Matches(BuyRule r, Item it) => r.Kind switch { BuyRule.KindItem => it.type == r.Type, BuyRule.KindAnyGun => IsGun(it), BuyRule.KindAnyAmmo => IsAmmo(it, r.Type), _ => false };

		// ---- money ----------------------------------------------------------------------------------------------------------------------

		public static long Funds(ArmoryTE a) { var b = a.LinkedBarracks(); return b == null ? 0 : EconomySystem.NetworkFunds(b); }

		private static bool Pay(ArmoryTE a, long cost)
		{
			var b = a.LinkedBarracks(); if (b == null) return false;
			if (!EconomySystem.TryPayNetwork(b, cost)) return false;
			RosterSystem.SyncBarracks(b); return true;
		}

		/// <summary>Buys up to <paramref name="want"/> units of an offer into the armory, never spending more than <paramref name="budget"/> copper. Returns the units bought and the copper spent.</summary>
		public static (int units, long spent) Buy(ArmoryTE a, Offer o, int want, long budget)
		{
			if (o.Item == null || want <= 0) return (0, 0);
			if (o.Once) want = Math.Min(want, 1);
			long funds = Funds(a);
			int n = OrderMath.BuyCount(want, o.UnitPrice, Math.Min(budget, funds), -1);
			if (n <= 0) return (0, 0);
			long cost = n * o.UnitPrice;
			if (!Pay(a, cost)) return (0, 0);
			int left = n;
			while (left > 0)
			{
				var it = new Item(); it.SetDefaults(o.Item.type);
				int k = Math.Min(left, Math.Max(1, it.maxStack)); it.stack = k;
				var rest = a.Deposit(it);
				if (rest != null && !rest.IsAir) Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("Levy.Logistics"), a.Center, rest);
				left -= k;
			}
			a.MarkDirty();
			return (n, cost);
		}

		/// <summary>
		/// Runs one standing buy rule: picks the best match the shops offer (dearest first: guns and ammo are bought as good as the budget allows) and buys until the count or the budget is met.
		/// Returns the copper spent this pass.
		/// </summary>
		public static long RunRule(ArmoryTE a, BuyRule r, List<Offer> offers)
		{
			if (r.Finished) return 0;
			long spentNow = 0;
			var cands = offers.Where(o => Matches(r, o.Item)).OrderByDescending(o => o.UnitPrice).ToList();
			foreach (var o in cands)
			{
				if (r.Bought >= r.Count || r.Spent >= r.Budget) break;
				var (u, s) = Buy(a, o, r.Count - r.Bought, r.Budget - r.Spent);
				r.Bought += u; r.Spent += s; spentNow += s;
			}
			return spentNow;
		}

		/// <summary>Buys what a shop sells of the missing materials of an order, within the order's shortfall budget. Returns the copper spent.</summary>
		public static long BuyShortfalls(ArmoryTE a, LogisticsOrder o, IEnumerable<KeyValuePair<int, long>> missing, List<Offer> offers)
		{
			long total = 0;
			foreach (var kv in missing)
			{
				long room = o.ShortfallBudget - o.ShortfallSpent; if (room <= 0) break;
				var offer = offers.Where(x => x.Item.type == kv.Key).OrderBy(x => x.UnitPrice).Cast<Offer?>().FirstOrDefault();
				if (offer == null) continue;
				var (u, s) = Buy(a, offer.Value, (int)Math.Min(kv.Value, 9999), room);
				o.ShortfallSpent += s; total += s;
			}
			return total;
		}
	}
}
