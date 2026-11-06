using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using MagicStorage.Components;
using Levy.Common.Logistics;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// A storage unit with no tile behind it: it holds items in a list, so a REAL <see cref="TEStorageHeart"/> can be given a network to withdraw from and deposit into at boot, before any world exists.
	/// Inert in play: <see cref="ValidTile"/> is always false, so no tile ever claims it and the network scanner never finds one.
	/// </summary>
	[ExtendsFromMod("MagicStorage")]
	[JITWhenModsEnabled("MagicStorage")]
	public class TECheckUnit : TEAbstractStorageUnit
	{
		public readonly List<Item> Items = new();
		public override bool IsFull => Items.Count >= 200;
		public override bool ValidTile(in Tile tile) => false;
		public override bool HasSpaceInStackFor(Item check) => Items.Any(i => i.type == check.type && i.prefix == check.prefix && i.stack < i.maxStack);
		public override bool HasItem(Item check, bool ignorePrefix = false) => Items.Any(i => i.type == check.type && (ignorePrefix || i.prefix == check.prefix));
		public override IEnumerable<Item> GetItems() => Items;
		public override void DepositItem(Item toDeposit)
		{
			foreach (var i in Items)
				if (i.type == toDeposit.type && i.prefix == toDeposit.prefix && i.stack < i.maxStack)
				{
					int k = Math.Min(toDeposit.stack, i.maxStack - i.stack); i.stack += k; toDeposit.stack -= k;
					if (toDeposit.stack <= 0) { toDeposit.TurnToAir(); return; }
				}
			if (Items.Count < 200) { Items.Add(toDeposit.Clone()); toDeposit.TurnToAir(); }
		}
		public override Item TryWithdraw(Item lookFor, bool locked = false, bool keepOneIfFavorite = false)
		{
			var res = new Item();
			for (int n = Items.Count - 1; n >= 0 && lookFor.stack > 0; n--)
			{
				var i = Items[n]; if (i.type != lookFor.type) continue;
				int k = Math.Min(i.stack, lookFor.stack);
				if (res.IsAir) { res = i.Clone(); res.stack = k; } else res.stack += k;
				i.stack -= k; lookFor.stack -= k;
				if (i.stack <= 0) Items.RemoveAt(n);
			}
			return res;
		}
	}

	/// <summary>The Magic-Storage-touching part of the boot self-check: a real storage heart (placed in memory, no tiles) linked to a <see cref="TECheckUnit"/> and a real <see cref="TEArmoryHeart"/>, then Levy's hooks and an order run against it.</summary>
	[JITWhenModsEnabled("MagicStorage")]
	public static class MsCheckDriver
	{
		public static void Run(Mod mod)
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			var placed = new List<TileEntity>();
			var savedStations = Crafting.StationOverride;
			bool savedMenu = Main.gameMenu;
			try
			{
				// static facts: recipe and art
				bool recipe = false;
				for (int i = 0; i < Recipe.numRecipes; i++) if (Main.recipe[i].createItem.type == ModContent.ItemType<ArmoryHeartItem>()) { recipe = true; break; }
				Check(recipe, "the Armory Heart has a recipe");
				var files = mod.GetFileNames().Select(f => f.Replace('\\', '/')).ToList();
				foreach (var t in new[] { "ArmoryHeartItem", "ArmoryHeartTile", "ArmoryHeartTile_Glow", "ArmoryHeartTile_Highlight" })
					Check(files.Any(f => f.StartsWith("Common/Compat/MagicStorage/" + t + ".")), "texture packed in the mod: " + t);
				Check(ModContent.TileType<ArmoryHeartTile>() > 0 && TileLoader.GetTile(ModContent.TileType<ArmoryHeartTile>()) is StorageComponent, "the Armory Heart tile is a Magic Storage StorageComponent");
				Check(NetworkStocks.Provider != null && NetworkStocks.HasStation != null && NetworkStocks.ItemTypes != null, "the compat registered Provider, HasStation and ItemTypes");

				// Magic Storage refuses every access while Main.gameMenu is true (it is, during load); the check pretends a world is up
				Main.gameMenu = false;
				// a real network: armory at (40,40), armory heart at (43,40), storage heart at (60,60), a unit at (62,60)
				var armory = (ArmoryTE)Place<ArmoryTE>(placed, 40, 40);
				var ah = (TEArmoryHeart)Place<TEArmoryHeart>(placed, 43, 40);
				var heart = (TEStorageHeart)Place<TEStorageHeart>(placed, 60, 60);
				var unit = (TECheckUnit)Place<TECheckUnit>(placed, 62, 60);
				Check(ReferenceEquals(ah.Armory, armory), "the Armory Heart finds the armory beside it");
				Check(NetworkStocks.For(armory) == null, "an Armory Heart that reaches no storage heart gives no network");
				heart.ComponentManager.Link(unit);
				heart.ComponentManager.Link(ah);
				Check(ReferenceEquals(ah.GetHeart(), heart), "the connected Armory Heart resolves to the storage heart");
				Check(ReferenceEquals(MsDriver.HeartFor(armory), heart), "MsDriver.HeartFor(armory) is the real storage heart");
				var net = NetworkStocks.For(armory);
				Check(net is MsNetworkStock, "NetworkStocks.For returns the Magic Storage stock");
				if (net == null) throw new InvalidOperationException("no network stock");

				// stock: counts, item list, deposit, withdraw
				Check(net.Count(ItemID.IronBar) == 0, "an empty network counts 0");
				var bars = new Item(); bars.SetDefaults(ItemID.IronBar); bars.stack = 700;
				var left = net.Put(bars);
				Check(left == null && net.Count(ItemID.IronBar) == 700, "700 iron bars deposited through the real heart (network holds " + net.Count(ItemID.IronBar) + ")");
				Check(unit.Items.Sum(i => i.type == ItemID.IronBar ? i.stack : 0) == 700, "they landed in the storage unit");
				var types = NetworkStocks.ItemTypes(armory).ToList();
				Check(types.Contains(ItemID.IronBar) && types.Count == types.Distinct().Count(), "NetworkStocks.ItemTypes lists iron bar once");
				Check(net.Take(ItemID.IronBar, 250) == 250 && net.Count(ItemID.IronBar) == 450, "withdraw of 250 leaves 450 (network holds " + net.Count(ItemID.IronBar) + ")");
				Check(net.Take(ItemID.IronBar, 9999) == 450 && net.Count(ItemID.IronBar) == 0, "an over-large withdraw returns what exists");
				Check(net.Take(ItemID.GoldBar, 5) == 0, "withdrawing an absent item returns 0");

				// a real craft order drawing its bars from the network, nothing in the armory
				Crafting.ResetCache();
				Crafting.StationOverride = new HashSet<int> { TileID.WorkBenches, TileID.Anvils, TileID.Furnaces, TileID.Bottles, TileID.Tables, TileID.Chairs };
				int per = Crafting.Ingredients(Crafting.RecipesFor(ItemID.IronBroadsword)[0])[0].Count;
				var more = new Item(); more.SetDefaults(ItemID.IronBar); more.stack = per * 6; net.Put(more);
				var o = new LogisticsOrder { ArmoryId = -1, Total = 5, Lines = { new OrderLine { Types = new int[] { ItemID.IronBroadsword }, Weight = 1 } } };
				o.Replan();
				LogisticsRunner.RunOn(o, armory);
				Check(o.Complete && armory.CountInStorage(ItemID.IronBroadsword) == 5, "the order made 5 swords into the armory from network bars (done " + o.Lines[0].Done + ", armory holds " + armory.CountInStorage(ItemID.IronBroadsword) + ", status " + o.Status + ")");
				Check(net.Count(ItemID.IronBar) == per * 6 - per * 5, "the network paid exactly " + per * 5 + " bars (left " + net.Count(ItemID.IronBar) + ")");
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message} {string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))}"); }
			finally
			{
				Main.gameMenu = savedMenu; Crafting.StationOverride = savedStations; Crafting.ResetCache(); OrderBook.Clear();
				foreach (var te in placed) { TileEntity.ByID.Remove(te.ID); TileEntity.ByPosition.Remove(te.Position); }
			}

			if (failures.Count == 0) mod.Logger.Info($"Levy Magic Storage self-check: {ok} checks OK");
			else
			{
				mod.Logger.Warn($"Levy Magic Storage self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
				foreach (var f in failures) mod.Logger.Warn("  magicstorage: " + f);
			}
		}

		private static TileEntity Place<T>(List<TileEntity> placed, int x, int y) where T : ModTileEntity
		{
			int id = ModContent.GetInstance<T>().Place(x, y);
			var te = TileEntity.ByID[id]; placed.Add(te); return te;
		}
	}
}
