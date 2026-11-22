using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of Armory autosell and the 1 platinum treasury buttons, logged as "[AutoSell]" lines (LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_AUTOSELL_ONLY=1 runs it alone through
	/// the summon check's chain). A real placed Barracks and Armory: a marked item deposited is sold and the treasury rises by exactly its sell price; the kept number stays; the best prefixes are kept;
	/// favourited items, items a rule names, issue rows, a levy's own gear and loaned weapons are never sold; unmarked and worthless items stay; the sweep waits after an edit and then runs from the
	/// armory's own tick; the marks survive a save and load and the multiplayer wire; an armory no Barracks pays for gets the coins as coins. Then the treasury's 1 platinum withdraw and deposit.
	/// Everything it places is removed again; every global it touches (the update counter, player 0) is put back.
	/// </summary>
	public class AutoSellWorldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Bx = 760;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_AUTOSELL") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[AutoSell] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[AutoSell] OBSERVED " + s);

		private static readonly System.Reflection.FieldInfo TickField = typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		private static void SetTick(uint t) => TickField?.SetValue(null, t);

		private static Item Make(int type, int stack = 1, int prefix = 0) { var it = new Item(); it.SetDefaults(type); it.stack = stack; if (prefix > 0) it.Prefix(prefix); return it; }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			uint tick0 = Main.GameUpdateCount;
			Player p0 = Main.player[0] ??= new Player();
			bool act0 = p0.active; Vector2 pos0 = p0.position; var inv0 = p0.inventory.Select(i => i.Clone()).ToArray();
			var placed = new List<(int i, int j)>(); BarracksTE bar = null; ArmoryTE arm = null;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try
			{
				global::Levy.Common.Util.LevyUtil.SpEmulation = true;
				p0.active = true; p0.dead = false; p0.position = new Vector2((Bx + 2) * 16f, (FloorY - 4) * 16f);
				(bar, arm) = Build(placed);
				Check("scene: a placed Barracks and Armory, and the Barracks pays for the Armory", bar != null && arm != null && ReferenceEquals(arm.LinkedBarracks(), bar));
				if (bar != null && arm != null) { Sell(bar, arm); Treasury(bar, p0); }
			}
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server;
				SetTick(tick0);
				foreach (var (i, j) in placed) for (int x = i; x < i + 3; x++) for (int y = j; y < j + 3; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
				foreach (var te in new TileEntity[] { bar, arm }) if (te != null) { TileEntity.ByID.Remove(te.ID); TileEntity.ByPosition.Remove(te.Position); }
				for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && (Main.item[i].type == ItemID.PlatinumCoin || Main.item[i].type == ItemID.GoldCoin)) { Main.item[i].active = false; Main.item[i].TurnToAir(); }
				for (int i = 0; i < p0.inventory.Length && i < inv0.Length; i++) p0.inventory[i] = inv0[i];
				p0.active = act0; p0.position = pos0;
			}
			_mod.Logger.Info($"[AutoSell] self-check pass={_pass} fail={_fail}");
		}

		private static void Flatten(int cx, int floorY, int half = 8)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private static (BarracksTE, ArmoryTE) Build(List<(int, int)> placed)
		{
			int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
			Flatten(Bx + 3, FloorY);
			BarracksTE b = null; ArmoryTE a = null;
			WorldGen.PlaceObject(Bx + 1, FloorY - 1, barType, true);
			ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(Bx, FloorY - 3, barType, 0, 1, 0); placed.Add((Bx, FloorY - 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx, FloorY - 3), out var e) && e is BarracksTE bb) { bb.TownName = "AutoSell"; bb.Treasury = 0; b = bb; }
			WorldGen.PlaceObject(Bx + 4, FloorY - 1, armType, true);
			ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(Bx + 4, FloorY - 3, armType, 0, 1, 0); placed.Add((Bx + 4, FloorY - 3));
			if (TileEntity.ByPosition.TryGetValue(new Point16(Bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE aa) a = aa;
			return (b, a);
		}

		private static int Held(ArmoryTE a, int type) { int n = 0; foreach (var it in a.Storage) if (it != null && !it.IsAir && it.type == type) n += it.stack; return n; }
		private static void Clear(ArmoryTE a) { for (int i = 0; i < a.Storage.Length; i++) a.Storage[i] = new Item(); }
		private static void Put(ArmoryTE a, int type, int count) { while (count > 0) { var it = Make(type); int k = Math.Min(count, Math.Max(1, it.maxStack)); it.stack = k; a.Deposit(it); count -= k; } }
		private static long Price(int type, int n) => (long)Make(type).value * n / 5;
		private static int FreeSlot(ArmoryTE a) { for (int i = 0; i < a.Storage.Length; i++) if (a.Storage[i] == null || a.Storage[i].IsAir) return i; return -1; }

		// ---- autosell --------------------------------------------------------------------------------------------------
		private static void Sell(BarracksTE bar, ArmoryTE arm)
		{
			const int Banana = ItemID.Bananarang;
			int bmax = Make(Banana).maxStack;
			Note($"Bananarang: value {Make(Banana).value} copper, stacks to {bmax}");
			Clear(arm); arm.SellMarks.Clear(); bar.Treasury = 0;

			// unmarked: nothing is sold
			Put(arm, Banana, 25);
			Check("unmarked: a sweep sells nothing", arm.RunAutoSell() == 0 && Held(arm, Banana) == 25 && bar.Treasury == 0);

			// marked, keep 0: every copy sold, the treasury rises by exactly the sell price
			Check("a Bananarang can be marked, a coin cannot", ArmoryTE.CanMarkForSale(Banana) && !ArmoryTE.CanMarkForSale(ItemID.GoldCoin) && !arm.SetSellMark(ItemID.PlatinumCoin, true, 0));
			arm.SetSellMark(Banana, true, 0);
			long paid = arm.RunAutoSell();
			Check($"keep 0: all 25 Bananarangs sold for {Price(Banana, 25)} copper into the Barracks treasury (paid {paid}, treasury {bar.Treasury})", Held(arm, Banana) == 0 && paid == Price(Banana, 25) && bar.Treasury == Price(Banana, 25));

			// a deposit after the mark: the next sweep sells it
			long t0 = bar.Treasury; Put(arm, Banana, 13); arm.RunAutoSell();
			Check($"a later deposit of 13 is sold too (+{bar.Treasury - t0})", Held(arm, Banana) == 0 && bar.Treasury - t0 == Price(Banana, 13));

			// keep N
			arm.SetSellMark(Banana, true, 7);
			Check("the mark keeps 7", arm.IsSellMarked(Banana, out int k7) && k7 == 7);
			t0 = bar.Treasury; Put(arm, Banana, 30); arm.RunAutoSell();
			Check($"keep 7: 30 deposited, 7 stay and 23 are sold (+{bar.Treasury - t0})", Held(arm, Banana) == 7 && bar.Treasury - t0 == Price(Banana, 23));
			t0 = bar.Treasury; arm.RunAutoSell();
			Check("keep 7: a second sweep sells nothing more", Held(arm, Banana) == 7 && bar.Treasury == t0);

			// the armory's own tick: held off just after an edit, then it runs by itself
			Clear(arm); Put(arm, Banana, 20); arm.SetSellMark(Banana, true, 0); t0 = bar.Treasury;
			for (int i = 0; i < 90; i++) arm.Update();
			Check("tick: right after an edit the sweep waits (nothing sold in 90 ticks)", Held(arm, Banana) == 20 && bar.Treasury == t0);
			SetTick(Main.GameUpdateCount + ArmoryTE.SellGraceTicks + 10);
			for (int i = 0; i < ArmoryTE.SellSweepTicks + 1; i++) arm.Update();
			Check($"tick: once the edit settles, the armory's own update sells them (+{bar.Treasury - t0})", Held(arm, Banana) == 0 && bar.Treasury - t0 == Price(Banana, 20));

			// favourited copies are never sold, and they count toward the kept number
			Clear(arm); arm.SetSellMark(Banana, true, 0);
			var fav = Make(Banana, Math.Min(5, bmax)); fav.favorited = true; arm.Storage[FreeSlot(arm)] = fav;
			Put(arm, Banana, 10); t0 = bar.Treasury; arm.RunAutoSell();
			Check($"favourited: the {fav.stack} favourited stay, the 10 others sell", Held(arm, Banana) == fav.stack && fav.stack > 0 && !fav.IsAir && bar.Treasury - t0 == Price(Banana, 10));

			// a rule that names the item: an always-prefer override, a set rank, an only-these list
			Clear(arm); Put(arm, Banana, 10);
			var o = arm.Rules.OverrideFor(Banana, true); o.Rank = RankOverrideMode.Prefer; arm.Rules.Changed();
			t0 = bar.Treasury; arm.RunAutoSell();
			Check("rule: an Always prefer override keeps every copy", Held(arm, Banana) == 10 && bar.Treasury == t0);
			o.Rank = RankOverrideMode.Set; o.Value = 50; arm.Rules.Changed(); arm.RunAutoSell();
			Check("rule: a Set rank override keeps every copy", Held(arm, Banana) == 10 && bar.Treasury == t0);
			arm.Rules.Overrides.Clear(); var blk = arm.Rules.AddBlock(RuleKind.ItemList); blk.List = ListMode.Only; blk.Items.Add(ArmoryRules.KeyOf(Banana)); arm.Rules.Changed(); arm.RunAutoSell();
			Check("rule: an only-these list that holds it keeps every copy", Held(arm, Banana) == 10 && bar.Treasury == t0);
			blk.List = ListMode.Never; arm.Rules.Changed(); arm.RunAutoSell();
			Check("rule: a never-use list does not protect it (sold)", Held(arm, Banana) == 0 && bar.Treasury - t0 == Price(Banana, 10));
			arm.Rules = ArmoryRules.Default();

			// out of storage: an issue row, a levy's own gear, a weapon on loan to a drone
			Clear(arm);
			var racked = Make(Banana, Math.Min(3, bmax)); arm.IssueWeapons[0] = racked;
			var lent = Make(Banana); arm.Lent.Add(lent);
			var rec = new SoldierRecord { Id = 94100, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Seller", HomeBarracksId = bar.ID };
			var carried = Make(Banana); rec.Gear.Weapon = carried;
			RosterSystem.Instance.Regulars.Add(rec);
			try
			{
				t0 = bar.Treasury; arm.RunAutoSell();
				Check("in use: the issue row, the levy's weapon and the drone's loan are untouched", ReferenceEquals(arm.IssueWeapons[0], racked) && racked.stack > 0 && ReferenceEquals(rec.Gear.Weapon, carried) && !carried.IsAir && arm.Lent.Contains(lent) && !lent.IsAir && bar.Treasury == t0);
			}
			finally { RosterSystem.Instance.Regulars.Remove(rec); arm.IssueWeapons[0] = new Item(); arm.Lent.Remove(lent); }

			// unmarked neighbours and worthless items stay
			Clear(arm); Put(arm, ItemID.WoodenSword, 3); Put(arm, Banana, 4);
			var free = Make(ItemID.DirtBlock, 50); free.value = 0; arm.Storage[FreeSlot(arm)] = free; arm.SetSellMark(ItemID.DirtBlock, true, 0);
			t0 = bar.Treasury; arm.RunAutoSell();
			Check("an unmarked Wooden Sword stays while its neighbour sells; a worthless marked stack stays", Held(arm, ItemID.WoodenSword) == 3 && Held(arm, Banana) == 0 && Held(arm, ItemID.DirtBlock) == 50 && bar.Treasury - t0 == Price(Banana, 4));
			arm.SetSellMark(ItemID.DirtBlock, false, 0);

			// the kept copies are the best: the cheapest prefixes go first
			Clear(arm); int sword = ItemID.GoldBroadsword; arm.SetSellMark(sword, true, 1);
			var best = Make(sword, 1, PrefixID.Legendary); var worst = Make(sword, 1, PrefixID.Broken); var plain = Make(sword);
			arm.Storage[0] = worst; arm.Storage[1] = best; arm.Storage[2] = plain;
			t0 = bar.Treasury; arm.RunAutoSell();
			Check($"keep 1 of three Gold Broadswords: the Legendary one stays, the Broken and plain ones sell (+{bar.Treasury - t0} = {(worst.value + plain.value) / 5})", Held(arm, sword) == 1 && ReferenceEquals(arm.Storage[1], best) && bar.Treasury - t0 == (worst.value + (long)plain.value) / 5);
			arm.SetSellMark(sword, false, 0);
			Check("clearing a mark", !arm.IsSellMarked(sword, out _));

			// save and load, the wire
			arm.SetSellMark(Banana, true, 7); arm.SellMarks["NoSuchMod/NoSuchItem"] = 3;
			var tag = new TagCompound(); arm.SaveData(tag);
			var loaded = new ArmoryTE(); loaded.LoadData(tag);
			Check("save and load keeps the marks and their kept numbers (a mark of a mod that is not loaded too)", loaded.IsSellMarked(Banana, out int lk) && lk == 7 && loaded.SellMarks.TryGetValue("NoSuchMod/NoSuchItem", out int nk) && nk == 3 && loaded.SellMarks.Count == arm.SellMarks.Count);
			byte[] wire; using (var ms = new MemoryStream()) { using (var w = new BinaryWriter(ms)) arm.NetSend(w); wire = ms.ToArray(); }
			var client = new ArmoryTE(); using (var ms = new MemoryStream(wire)) using (var r = new BinaryReader(ms)) client.NetReceive(r);
			Check("the multiplayer wire carries the marks", client.IsSellMarked(Banana, out int ck) && ck == 7 && client.SellMarks.Count == arm.SellMarks.Count);
			var empty = new TagCompound(); new ArmoryTE().SaveData(empty);
			Check("an armory with no marks writes no autosell data", !empty.ContainsKey("sellV1"));
			Clear(arm); Put(arm, Banana, 9); t0 = bar.Treasury; arm.RunAutoSell();
			Check("an unloaded mod's mark is harmless (the Bananarang mark still works)", Held(arm, Banana) == 7 && bar.Treasury - t0 == Price(Banana, 2));
			arm.SellMarks.Remove("NoSuchMod/NoSuchItem");

			// the edit entry point the item panel uses (server and single player: applied at once)
			ArmoryEditNet.SetSellMark(arm, Banana, false, 0);
			Check("the item panel's entry point clears the mark", !arm.IsSellMarked(Banana, out _));

			// an armory no Barracks pays for: the coins land in its storage
			var lone = new ArmoryTE(); lone.SetSellMark(Banana, true, 0); Put(lone, Banana, 20);
			long lp = lone.RunAutoSell();
			long coins = 0; foreach (var it in lone.Storage) if (it != null && !it.IsAir) coins += global::Levy.Common.Systems.EconomySystem.CoinValue(it);
			Check($"no Barracks: 20 Bananarangs become {Price(Banana, 20)} copper of coins in the Armory ({coins})", lone.LinkedBarracks() == null && lp == Price(Banana, 20) && coins == lp && Held(lone, Banana) == 0);
			Clear(arm); arm.SellMarks.Clear();
		}

		// ---- the 1 platinum treasury buttons ----------------------------------------------------------------------------------
		private static int Coins(int type) { int n = 0; for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == type) n += Main.item[i].stack; return n; }

		private static void Treasury(BarracksTE bar, Player p)
		{
			long plat = LevyConstants.UI.DepositPlatinum;
			Check("the platinum button moves 1 platinum (1,000,000 copper)", plat == 1_000_000);
			for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == ItemID.PlatinumCoin) { Main.item[i].active = false; Main.item[i].TurnToAir(); }
			bar.Treasury = 2_500_000;
			bool ok = RosterSystem.Instance.Withdraw(p, bar, plat);
			Check($"withdraw 1 platinum: the treasury drops by 1 platinum and one Platinum Coin is handed over (treasury {bar.Treasury}, coins {Coins(ItemID.PlatinumCoin)})", ok && bar.Treasury == 1_500_000 && Coins(ItemID.PlatinumCoin) == 1);
			bar.Treasury = 500_000;
			Check("withdraw 1 platinum from 50 gold is refused and changes nothing", !RosterSystem.Instance.Withdraw(p, bar, plat) && bar.Treasury == 500_000);
			for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
			p.inventory[10] = Make(ItemID.PlatinumCoin, 1);
			ok = RosterSystem.Instance.Deposit(p, bar, plat);
			Check($"deposit 1 platinum: the coin leaves the pack and the treasury rises by 1 platinum (treasury {bar.Treasury})", ok && bar.Treasury == 1_500_000 && p.inventory[10].IsAir);
			Check("deposit 1 platinum with an empty pack is refused", !RosterSystem.Instance.Deposit(p, bar, plat) && bar.Treasury == 1_500_000);
		}
	}
}
