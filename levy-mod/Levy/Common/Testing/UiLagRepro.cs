using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Logistics;
using Levy.Common.Potions;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Times what the Armory and Logistics screens do on a big armory, without a client: opening the Logistics tab (the Orders page re-plans its materials every frame), the Find page and the
	/// Seeker chooser (candidate list, search filter), the craft browser scan, the Sell and Clear pages, and the Items tab search. Env LEVY_UILAG=1 (LEVY_PERFSCENE_SLOTS, default 3000) runs it after
	/// recipes are final and logs "[UiLag]" lines with ms per call. Only calls that exist before and after the fixes are used, so the same file times the old and the new code.
	/// </summary>
	public partial class UiLagRepro : ModSystem
	{
		public override void PostSetupRecipes()
		{
			if (Environment.GetEnvironmentVariable("LEVY_UILAG") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private static double Ms(long t0) => (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

		private static void Time(string name, int reps, Func<string> act)
		{
			var log = Levy.Instance?.Logger; string note = "";
			var times = new List<double>();
			for (int i = 0; i < reps; i++) { long t = Stopwatch.GetTimestamp(); note = act(); times.Add(Ms(t)); }
			log?.Info($"[UiLag] {name,-58} first {times[0],9:F2} ms  then avg {(times.Count > 1 ? times.Skip(1).Average() : times[0]),9:F2} ms  {note}");
		}

		private static void Run()
		{
			int slots = SellFreezeRepro.EnvInt("LEVY_PERFSCENE_SLOTS", 3000);
			var log = Levy.Instance?.Logger; Action undoPlayer = null;
			try
			{
				try { ProgressionTier.EnsureBuilt(m => log?.Info("[UiLag] " + m)); } catch (Exception) { }
				var te = SellFreezeRepro.BuildStock(slots, out int filled, out string kinds);
				undoPlayer = SellFreezeRepro.EnsurePayer(te.Center);
				log?.Info($"[UiLag] stock: {filled}/{slots} slots ({kinds}); {ItemLoader.ItemCount} item types, {Main.recipe.Length} recipes");

				// ---- Logistics, Orders page: what Tick() does every frame for an unsaved draft (LocalPreview)
				var draft = new LogisticsOrder { ArmoryId = te.ID, Total = 30, Mode = OrderMode.Bucket };
				foreach (var set in new[] { new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings }, new int[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings }, new int[] { ItemID.Zenith } })
					draft.Lines.Add(new OrderLine { Types = set, Weight = 33 });
				draft.Replan();
				Time("Orders page frame: plan 3 lines + checklist counts", 6, () =>
				{
					var cc = new CountCache(t => te.CountInStorage(t));
					var plan = LogisticsRunner.PlanRemaining(draft, te, cc);
					var rows = plan.Need.OrderBy(k => k.Key).Select(k => new ChecklistRow { Type = k.Key, Need = k.Value, Have = te.CountInStorage(k.Key) }).ToList();
					return rows.Count + " materials";
				});
				Time("Armory panel frame: status line (TotalSellValue)", 6, () => te.TotalSellValue().ToString());
				Time("Armory panel frame: InUse stamp", 6, () => { te.InUse(out int s); return "stamp " + s; });

				// ---- Find page / Seeker chooser
				Time("Find: FindTarget.Candidates (first use)", 1, () => FindTarget.Candidates(te).Count + " candidates");
				Time("Find: FindTarget.Candidates (again, per keystroke)", 4, () => FindTarget.Candidates(te).Count + " candidates");
				var cands = FindTarget.Candidates(te);
				Time("Find: empty-search list (stocked or treasure)", 4, () =>
				{
					var stocked = new HashSet<int>(); foreach (var it in te.Storage) if (it != null && !it.IsAir) stocked.Add(it.type);
					int shown = 0;
					foreach (int t in cands) { if (!stocked.Contains(t) && !SeekerMatcher.IsTreasure(t)) continue; if (++shown > 400) break; }
					return shown + " cells";
				});
				Time("Find: typed search 'sword' over every candidate", 4, () =>
				{
					int shown = 0;
					foreach (int t in cands) { if (!ItemFilter.NameMatches(Lang_Name(t), "sword") || !ItemFilter.Matches(ContentSamples.ItemsByType[t], FilterCat.All)) continue; if (++shown > 400) break; }
					return shown + " cells";
				});
				Time("Find: typed search 's' + Weapons kind over every candidate", 4, () =>
				{
					int shown = 0;
					foreach (int t in cands) { if (!ItemFilter.NameMatches(Lang_Name(t), "s") || !ItemFilter.Matches(ContentSamples.ItemsByType[t], FilterCat.Weapons)) continue; if (++shown > 400) break; }
					return shown + " cells";
				});

				// ---- Craft browser
				var payer = Main.player[0];
				Time("Craft browser: PoolFor + Scan (every recipe)", 3, () => { var pool = CraftBrowser.PoolFor(te, payer); return CraftBrowser.Scan(te, pool).Count + " recipes"; });

				// ---- the same two big readers with a Magic Storage network in the pool (modelled: a network of LEVY_UILAG_NET stacks whose Count walks every stack, as GetStoredItems does)
				int netStacks = SellFreezeRepro.EnvInt("LEVY_UILAG_NET", 40000);
				var fake = new FakeNet(netStacks);
				Time($"Craft browser with a {netStacks}-stack network (raw)", 2, () => { var pool = CraftBrowser.PoolFor(te, payer); pool.Sources.Add(fake); return CraftBrowser.Scan(te, pool).Count + " recipes, " + fake.Walks + " network walks"; });
				Time($"Orders page frame with the network (raw)", 3, () =>
				{
					var pool = CraftBrowser.PoolFor(te, payer); pool.Sources.Add(fake); fake.Walks = 0;
					var cc = new CountCache(pool.Count);
					var plan = LogisticsRunner.PlanRemaining(draft, te, cc);
					return plan.Need.Count + " materials, " + fake.Walks + " network walks";
				});
				AfterNetwork(te, payer, draft, netStacks);

				// ---- Items tab search (the armory grid)
				Time("Armory search: AffixName match over storage", 4, () =>
				{
					int n = 0; foreach (var it in te.Storage) { if (it == null || it.IsAir) continue; if (it.AffixName().IndexOf("sword", StringComparison.OrdinalIgnoreCase) >= 0) n++; }
					return n + " matches";
				});

				// ---- Sell / Clear
				Time("Sell: Select 'every tier'", 3, () => Selling.Select(te, new Selling.Filter { TierLo = 0f, TierHi = 99f }).Count + " slots");
				Time("Clear: ClearOut.Build tier 7", 3, () => { var p = ClearOut.Build(te, 7f, false); return p.Entries.Count + " entries"; });
			}
			catch (Exception e) { log?.Warn("[UiLag] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))); }
			finally { undoPlayer?.Invoke(); ProgressionTier.Reset(); }
		}

		/// <summary>Models Magic Storage's network stock: Count walks every stored stack (MsNetworkStock.Count enumerates GetStoredItems each call).</summary>
		internal sealed class FakeNet : IStockItems
		{
			private readonly List<Item> _items = new(); public int Walks;
			public FakeNet(int stacks)
			{
				var rnd = new Random(7); int n = ItemLoader.ItemCount;
				for (int i = 0; i < stacks; i++) { var it = new Item(); it.SetDefaults(1 + rnd.Next(n - 1)); it.stack = 1 + rnd.Next(Math.Max(1, it.maxStack)); _items.Add(it); }
			}
			public string Name => "network";
			public long Count(int type) { Walks++; long c = 0; foreach (var it in _items) if (it != null && !it.IsAir && it.type == type) c += it.stack; return c; }
			public long Take(int type, long n) => 0;
			public Item Put(Item item) => item;
			public IEnumerable<Item> Items() => _items;
			public long TakeExact(Item like, long n) => 0;
			public bool Protected(Item it) => false;
		}

		static partial void AfterNetwork(ArmoryTE te, Player payer, LogisticsOrder draft, int netStacks);

		private static string Lang_Name(int t) => Terraria.Lang.GetItemNameValue(t);
	}
}
