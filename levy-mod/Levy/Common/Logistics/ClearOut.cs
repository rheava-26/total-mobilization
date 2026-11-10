using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Logistics
{
	/// <summary>A stock whose items can be listed and taken one at a time: what a clear-out sweeps. Server and single player only.</summary>
	public interface IStockItems : IStock
	{
		/// <summary>The stored items, live instances: read them, never change them, and copy the list before taking anything out.</summary>
		IEnumerable<Item> Items();
		/// <summary>Removes up to <paramref name="n"/> of the item's exact type and prefix (that very instance first); returns how many came out.</summary>
		long TakeExact(Item like, long n);
		/// <summary>True when a clear-out must leave this item alone (reserved for a levy, or a kind the player prefers).</summary>
		bool Protected(Item it);
	}

	/// <summary>
	/// CLEAR OUT BELOW A TIER. Sweeps every weapon, armor piece and accessory that sits below a progression tier out of the armory, the armories pooled with it, the chests beside it and the linked Magic Storage network,
	/// and either sells the lot (to the best-paying shopkeeper in reach, into the barracks treasury) or, when the network has a Decrafting Interface, takes it apart into materials that go back into storage.
	/// It never touches a favourited item, a vanity piece, an item with no known tier, a kind the Rules tab prefers or sets by hand, or anything reserved for a levy. Gear a levy carries is not in storage, so it is never in the sweep.
	/// Two steps: <see cref="Build"/> lists what would go (the preview), <see cref="Execute"/> does it, and refuses when the stock changed since the list the player confirmed. Server-authoritative: on a multiplayer client
	/// the actions in <see cref="LogisticsActions"/> become packets and the server runs these.
	/// </summary>
	public static class ClearOut
	{
		/// <summary>The tiers the picker steps through (Boss Checklist's scale: King Slime 1 ... Moon Lord 18). "Below N" means a tier under N.</summary>
		public static readonly float[] Milestones = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f, 17f, 18f };

		/// <summary>Index into <see cref="Milestones"/> the picker starts on (Skeletron).</summary>
		public const int DefaultMilestone = 4;
		/// <summary>Rows a preview carries to a client (the totals still count every item).</summary>
		public const int MaxPreviewRows = 300;

		public static bool IsWeapon(Item it) => it.damage > 0 && it.maxStack == 1 && it.ammo == 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && !IsArmor(it);
		public static bool IsArmor(Item it) => (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0) && !it.vanity;
		public static bool IsAccessory(Item it) => it.accessory && !it.vanity && !IsArmor(it);

		/// <summary>True when the item belongs to Levy's own companion mods, whose gear is never swept.</summary>
		private static bool IsOwnMod(Item it) => it.ModItem != null && (it.ModItem.Mod.Name == "Levy" || it.ModItem.Mod.Name == "Mechanization");

		/// <summary>
		/// True when a clear-out below <paramref name="tier"/> may take this item: a weapon, armor piece or accessory worth coin, not favourited, with a known progression tier under <paramref name="tier"/>. With
		/// <paramref name="decraft"/> the item must also come apart by shimmer. (The protections that depend on where the item sits are <see cref="IStockItems.Protected"/>.)
		/// </summary>
		public static bool Eligible(Item it, float tier, bool decraft)
		{
			if (it == null || it.IsAir || it.favorited || it.value <= 0 || it.questItem || IsOwnMod(it)) return false;
			if (!(IsWeapon(it) || IsArmor(it) || IsAccessory(it))) return false;
			float t = ProgressionTier.Of(it.type);
			if (t < 0f || t >= tier) return false;
			return !decraft || Selling.DecraftOf(it.type) != null;
		}

		/// <summary>Every place a clear-out sweeps, in order: the armory, the armories pooled with it, the chests beside it, the Magic Storage network.</summary>
		public static List<IStockItems> Sources(ArmoryTE a)
		{
			var res = new List<IStockItems> { new ArmoryStock(a) };
			var sys = SettlementSystem.Instance;
			if (sys != null) foreach (var o in sys.NetworkedArmories(a)) if (!ReferenceEquals(o, a) && o.Storage != null) res.Add(new ArmoryStock(o));
			foreach (int c in StockPool.NearbyChests(a)) res.Add(new ChestStock(c));
			if (NetworkStocks.For(a) is IStockItems net) res.Add(net);
			return res;
		}

		/// <summary>True when the armory's Magic Storage network has a Decrafting Interface, the shimmer module that takes an item apart.</summary>
		public static bool CanDecraft(ArmoryTE a)
		{
			try { return NetworkStocks.CanDecraft?.Invoke(a) ?? false; }
			catch (Exception) { return false; }
		}

		public sealed class Entry { public IStockItems Src; public Item Item; }

		/// <summary>What a clear-out would take, right now.</summary>
		public sealed class Plan
		{
			public float Tier; public bool Decraft, CanDecraft;
			public readonly List<Entry> Entries = new();
			public long Value; public NPC Buyer; public string BuyerName = "";
			public int FromArmory, FromChests, FromNetwork;
			public bool TestBuyer;
			public bool NoBuyer => !Decraft && Entries.Count > 0 && Buyer == null && !TestBuyer;
			public int Hash => HashOf(Entries.Select(e => e.Item));
		}

		/// <summary>An order-free fingerprint of a list of items (type and prefix of each), so a confirm can tell the stock changed since the list was shown.</summary>
		public static int HashOf(IEnumerable<Item> items)
		{
			int n = 0; long sum = 0;
			foreach (var it in items) { n++; sum += it.type * 31L + it.prefix * 7L + 1; }
			return unchecked((int)(sum * 2654435761L) ^ n);
		}

		/// <summary>Self-check seam: a fixed price per item, standing in for the shopkeeper (a dedicated server has no player to price for).</summary>
		internal static Func<Item, long> TestPrice;

		public static Plan Build(ArmoryTE a, float tier, bool decraft)
		{
			var plan = new Plan { Tier = tier, Decraft = decraft && CanDecraft(a), CanDecraft = CanDecraft(a) };
			if (!ProgressionTier.Ready) return plan;
			foreach (var src in Sources(a))
			{
				var list = src.Items().ToList();
				foreach (var it in list)
				{
					if (!Eligible(it, tier, plan.Decraft) || src.Protected(it)) continue;
					plan.Entries.Add(new Entry { Src = src, Item = it });
					if (src is ArmoryStock) plan.FromArmory++; else if (src is ChestStock) plan.FromChests++; else plan.FromNetwork++;
				}
			}
			if (!plan.Decraft && plan.Entries.Count > 0 && TestPrice != null)
			{
				plan.TestBuyer = true; plan.BuyerName = "test"; foreach (var e in plan.Entries) plan.Value += TestPrice(e.Item);
			}
			else if (!plan.Decraft && plan.Entries.Count > 0)
			{
				var (buyer, total) = Selling.BestBuyer(a, plan.Entries.Select(e => e.Item).ToList());
				plan.Buyer = buyer; plan.Value = total;
				if (buyer != null) plan.BuyerName = Shopping.KeeperLabel(a, buyer);
			}
			return plan;
		}

		public sealed class Result { public int Items, Stacks, Decrafted; public long Coin; public string Buyer = ""; public bool Stale, NoBuyer; }

		/// <summary>
		/// Does the clear-out. <paramref name="expectHash"/> is the fingerprint of the list the player confirmed; when the stock no longer matches it nothing is taken (<see cref="Result.Stale"/>).
		/// Selling pays the barracks treasury; decrafting puts the materials back where each item came from, else into the armory.
		/// </summary>
		public static Result Execute(ArmoryTE a, float tier, bool decraft, int expectHash)
		{
			var res = new Result();
			var plan = Build(a, tier, decraft);
			if (plan.Entries.Count == 0) return res;
			if (plan.Hash != expectHash) { res.Stale = true; return res; }
			var touched = new HashSet<ArmoryTE>();
			if (plan.Decraft)
			{
				foreach (var e in plan.Entries)
				{
					var rec = Selling.DecraftOf(e.Item.type); if (rec == null) continue;
					int crafts = Math.Max(1, e.Item.stack) / Math.Max(1, rec.createItem.stack); if (crafts <= 0) continue;
					var like = e.Item.Clone();
					if (e.Src.TakeExact(e.Item, e.Item.stack) <= 0) continue;
					if (e.Src is ArmoryStock ast) touched.Add(ast.Te);
					foreach (var ing in rec.requiredItem)
					{
						if (ing == null || ing.IsAir) continue;
						var o = new Item(); o.SetDefaults(ing.type); o.stack = ing.stack * crafts;
						GiveBack(a, e.Src, o);
					}
					res.Decrafted += like.stack; res.Stacks++;
				}
			}
			else
			{
				if (plan.Buyer == null && !plan.TestBuyer) { res.NoBuyer = true; return res; }
				var payer = Shopping.Payer(a); long total = 0;
				foreach (var e in plan.Entries)
				{
					var it = e.Item; long worth = TestPrice != null ? TestPrice(it) : payer != null ? Selling.SellValue(payer, plan.Buyer, it) : it.value / 5L * it.stack; int stack = it.stack;
					if (e.Src.TakeExact(it, stack) <= 0) continue;
					if (e.Src is ArmoryStock ast) touched.Add(ast.Te);
					total += worth; res.Items += stack; res.Stacks++;
				}
				if (total > 0) { Selling.Pay(a, total); res.Coin = total; res.Buyer = plan.BuyerName; }
			}
			foreach (var t in touched) { t.MarkDirty(); }
			return res;
		}

		/// <summary>Puts materials back: into the stock they came from, then the armory, then (nothing fits) onto the ground beside it.</summary>
		private static void GiveBack(ArmoryTE a, IStockItems from, Item o)
		{
			while (!o.IsAir)
			{
				var part = o.Clone(); part.stack = Math.Min(o.stack, Math.Max(1, o.maxStack)); o.stack -= part.stack;
				Item rest = from.Put(part);
				if (rest != null && !rest.IsAir) rest = a.Deposit(rest);
				if (rest != null && !rest.IsAir) Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("Levy.ClearOut"), a.Center, rest);
			}
		}

		// ------------------------------------------------------------------ the preview a client shows

		/// <summary>One line of a preview: a kind of item (type and prefix) and how many there are.</summary>
		public struct Row { public int Type; public byte Prefix; public int Count; public long Value; }

		/// <summary>What the player is shown before confirming. Built from a <see cref="Plan"/> on the server and sent to the client.</summary>
		public sealed class Preview
		{
			public int ArmoryId; public float Tier; public bool Decraft, CanDecraft, NoBuyer, Stale; public int Hash, Items, FromArmory, FromChests, FromNetwork; public long Value; public string Buyer = "";
			public List<Row> Rows = new();
			/// <summary>The rows of the full list (Rows is cut at <see cref="MaxPreviewRows"/>).</summary>
			public int RowTotal;
			public bool Empty => Items == 0;

			public static Preview Of(ArmoryTE a, Plan plan)
			{
				var p = new Preview { ArmoryId = a.ID, Tier = plan.Tier, Decraft = plan.Decraft, CanDecraft = plan.CanDecraft, NoBuyer = plan.NoBuyer, Hash = plan.Hash, Items = plan.Entries.Count, Value = plan.Value, Buyer = plan.BuyerName ?? "",
					FromArmory = plan.FromArmory, FromChests = plan.FromChests, FromNetwork = plan.FromNetwork };
				var payer = Shopping.Payer(a);
				var rows = new Dictionary<(int, int), Row>();
				foreach (var e in plan.Entries)
				{
					var k = (e.Item.type, e.Item.prefix); rows.TryGetValue(k, out var r);
					long worth = plan.Decraft ? 0 : TestPrice != null ? TestPrice(e.Item) : payer == null || plan.Buyer == null ? 0 : Selling.SellValue(payer, plan.Buyer, e.Item);
					rows[k] = new Row { Type = k.Item1, Prefix = (byte)k.Item2, Count = r.Count + Math.Max(1, e.Item.stack), Value = r.Value + worth };
				}
				var all = rows.Values.OrderBy(r => ProgressionTier.Of(r.Type)).ThenByDescending(r => r.Value).ToList();
				p.RowTotal = all.Count; p.Rows = all.Take(MaxPreviewRows).ToList();
				return p;
			}

			public void Write(BinaryWriter w)
			{
				w.Write(ArmoryId); w.Write(Tier); w.Write(Decraft); w.Write(CanDecraft); w.Write(NoBuyer); w.Write(Stale); w.Write(Hash); w.Write(Items); w.Write(FromArmory); w.Write(FromChests); w.Write(FromNetwork); w.Write(Value); w.Write(Buyer ?? "");
				w.Write(RowTotal); w.Write((ushort)Rows.Count);
				foreach (var r in Rows) { w.Write(r.Type); w.Write(r.Prefix); w.Write(r.Count); w.Write(r.Value); }
			}

			public static Preview Read(BinaryReader r)
			{
				var p = new Preview { ArmoryId = r.ReadInt32(), Tier = r.ReadSingle(), Decraft = r.ReadBoolean(), CanDecraft = r.ReadBoolean(), NoBuyer = r.ReadBoolean(), Stale = r.ReadBoolean(), Hash = r.ReadInt32(), Items = r.ReadInt32(),
					FromArmory = r.ReadInt32(), FromChests = r.ReadInt32(), FromNetwork = r.ReadInt32(), Value = r.ReadInt64(), Buyer = r.ReadString(), RowTotal = r.ReadInt32() };
				int n = r.ReadUInt16();
				for (int i = 0; i < n; i++) p.Rows.Add(new Row { Type = r.ReadInt32(), Prefix = r.ReadByte(), Count = r.ReadInt32(), Value = r.ReadInt64() });
				return p;
			}
		}

		/// <summary>The latest preview this client holds (single player: computed here; multiplayer: sent by the server). The Clear out page reads it.</summary>
		public static Preview Latest;
		/// <summary>Bumped whenever <see cref="Latest"/> changes, so the page knows to rebuild its list.</summary>
		public static int Version;
		public static void SetLatest(Preview p) { Latest = p; Version++; }
	}

	/// <summary>The stocks of a clear-out: an armory's storage and a vanilla chest list their slots.</summary>
	public static class StockItemsEx
	{
		public static long TakeFromArray(Item[] arr, Item like, long n, Action<int> changed)
		{
			if (arr == null) return 0;
			for (int i = 0; i < arr.Length; i++)
				if (ReferenceEquals(arr[i], like)) return Remove(arr, i, n, changed);
			long taken = 0;
			for (int i = 0; i < arr.Length && taken < n; i++)
			{
				var s = arr[i]; if (s == null || s.IsAir || s.type != like.type || s.prefix != like.prefix) continue;
				taken += Remove(arr, i, n - taken, changed);
			}
			return taken;
		}

		private static long Remove(Item[] arr, int i, long n, Action<int> changed)
		{
			var s = arr[i]; if (s == null || s.IsAir) return 0;
			int k = (int)Math.Min(s.stack, n);
			s.stack -= k;
			if (s.stack <= 0) arr[i] = new Item();
			changed?.Invoke(i);
			return k;
		}
	}
}
