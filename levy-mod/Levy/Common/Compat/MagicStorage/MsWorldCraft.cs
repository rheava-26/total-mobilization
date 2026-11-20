using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MagicStorage;
using Microsoft.Xna.Framework;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Compat.MagicStorageLink
{
	/// <summary>
	/// In-world check of crafting with armory racks through the entry the Crafting Interface's Craft button uses (<c>CraftingGUI.Craft</c>), after Magic Storage's own refresh code
	/// (<c>LoadItemsAndSetDictionaryInfo</c> + <c>RefreshStorageItems</c>, the work its refresh thread does) built the ingredient lists from a real Storage Heart TILE the player is "viewing".
	/// Runs on a server as single player for its own length (one synchronous call). The designer's case with Calamity: Terra Blade in the armory, Uelibloom Bars in the network -> Terratomere
	/// (vanilla: Excalibur + Chlorophyte Bars -> True Excalibur). Cases: one craft, again with nothing left, a max-stack craft of several, the list refreshed while the racks change, a levy drawing the
	/// sword after the refresh, and a craft clicked the frame after the racks changed with no refresh in between.
	/// </summary>
	[JITWhenModsEnabled("MagicStorage")]
	public static class MsWorldCraft
	{
		private const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, I = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }

		public static void Run(Action<bool, string> check, Action<string> observe)
		{
			int nm = Main.netMode; bool menu = Main.gameMenu; var placed = new List<TileEntity>(); var tiles = new List<Point>();
			var sp = Main.LocalPlayer.GetModPlayer<StoragePlayer>();
			var accessField = typeof(StoragePlayer).GetField("storageAccess", I);
			object oldAccess = accessField.GetValue(sp);
			var selField = typeof(CraftingGUI).GetField("selectedRecipe", S); object oldSel = selField.GetValue(null);
			try
			{
				Main.gameMenu = false; // Magic Storage refuses access while the menu flag is up (it is during a server's world load)
				Main.netMode = NetmodeID.SinglePlayer; // the Crafting Interface only crafts on a client; this is the single-player branch
				bool cal = ModLoader.TryGetMod("CalamityMod", out Mod calMod);
				int sword = cal ? ItemID.TerraBlade : ItemID.Excalibur;
				int made = cal && calMod.TryFind("Terratomere", out ModItem tm) ? tm.Type : ItemID.TrueExcalibur;
				Recipe rec = null;
				for (int i = 0; i < Recipe.numRecipes; i++) if (Main.recipe[i].createItem.type == made && Main.recipe[i].requiredItem.Any(r => r.type == sword)) { rec = Main.recipe[i]; break; }
				check(rec != null, $"world craft: the {Lang.GetItemNameValue(made)} recipe exists");
				if (rec == null) return;
				var other = rec.requiredItem.First(r => r.type != sword); int per = other.stack;
				string tag = $"{Lang.GetItemNameValue(sword)} -> {Lang.GetItemNameValue(made)}";

				int x = Math.Clamp(Main.spawnTileX + 120, 50, Main.maxTilesX - 80), y = 60;
				var armory = (ArmoryTE)Place<ArmoryTE>(placed, x, y);
				var ah = (TEArmoryHeart)Place<TEArmoryHeart>(placed, x + 3, y);
				for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
				{
					var t = Main.tile[x + 10 + dx, y + dy]; t.HasTile = true; t.TileType = (ushort)ModContent.TileType<StorageHeart>(); t.TileFrameX = (short)(dx * 18); t.TileFrameY = (short)(dy * 18); tiles.Add(new Point(x + 10 + dx, y + dy));
				}
				var heart = (TEStorageHeart)Place<TEStorageHeart>(placed, x + 10, y);
				var unit = (TECheckUnit)Place<TECheckUnit>(placed, x + 13, y);
				heart.ComponentManager.Link(unit); heart.ComponentManager.Link(ah);
				ArmoryCraftLink.ResetForTest(); MsDriver.ResetCache(); ArmoryCraftLink.ForceOn = true;
				accessField.SetValue(sp, heart.Position);
				check(ReferenceEquals(StoragePlayer.LocalPlayer.GetStorageHeart(), heart), "world craft: the player views the real Storage Heart tile");
				selField.SetValue(null, rec);
				var adj = (bool[])typeof(CraftingGUI).GetField("adjTiles", S).GetValue(null); // the Crafting Interface's stations: a Mythril Anvil in its slot
				foreach (int t in rec.requiredTile) if (t >= 0 && t < adj.Length) adj[t] = true;

				long Armory(int t) => armory.Storage.Concat(armory.IssueWeapons).Where(i => !i.IsAir && i.type == t).Sum(i => (long)i.stack);
				long Net(int t) => unit.Items.Where(i => !i.IsAir && i.type == t).Sum(i => (long)i.stack);
				long All(int t) => Armory(t) + Net(t);
				void Frame() { ArmoryCraftLink.Sync(heart, false); } // PreUpdateUI
				int Craft(int n) { int before = (int)All(made); CraftingGUI.Craft(n); ArmoryCraftLink.AfterUi(heart); return (int)All(made) - before; } // the Craft button, then PostUpdateUI

				// stock: one sword in the armory, bars for three crafts in the network
				armory.Deposit(Make(sword));
				unit.DepositItem(Make(other.type, per * 3));

				// 1. one craft through the Craft button's entry
				observe($"[MsWorldCraft] OBSERVED stock: armory swords {Armory(sword)}, network {Lang.GetItemNameValue(other.type)} {Net(other.type)}, heart stored {heart.GetStoredItems().Count()}");
				Frame(); Refresh(heart);
				var fm = (List<bool>)typeof(CraftingGUI).GetField("storageItemsFromModules", S).GetValue(null);
				observe("[MsWorldCraft] OBSERVED stored ingredients: " + string.Join(", ", StorageItems().Select((i, n) => $"{i.Name} x{i.stack}{(n < fm.Count && fm[n] ? " (module)" : "")}")) + "; item counts " + string.Join(", ", ((Dictionary<int, int>)typeof(CraftingGUI).GetField("itemCounts", S).GetValue(null)).Select(kv => Lang.GetItemNameValue(kv.Key) + " " + kv.Value)));
				check(StorageItems().Any(i => i.type == sword && i.stack == 1), $"world craft [{tag}]: the armory's sword is a stored ingredient after Magic Storage's refresh");
				int amt = CraftingGUI.AmountCraftableForCurrentRecipe();
				check(amt == 1, "world craft: Magic Storage counts 1 craftable (" + amt + ")");
				int got = Craft(1);
				check(got == 1 && Armory(sword) == 0 && Net(other.type) == per * 2, $"world craft: one craft made 1, took the sword off the armory and {per} bars (made {got}, armory swords {Armory(sword)}, bars {Net(other.type)})");
				check(Armory(made) == 1, "world craft: the result went back to the armory (" + Armory(made) + ")");
				check(ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.Taken == 1, $"world craft: taken {ArmoryCraftLink.Taken}, shortfall {ArmoryCraftLink.Shortfall}");

				// 2. nothing left: the refreshed list, then a stale click without a refresh
				Frame(); Refresh(heart);
				check(CraftingGUI.AmountCraftableForCurrentRecipe() == 0 && Craft(1) == 0, "world craft: with the sword used up nothing more is made");
				check(Net(other.type) == per * 2, "world craft: and no bars are taken");

				// 3. max stack: two swords, ask for 9999
				armory.Deposit(Make(sword)); armory.Deposit(Make(sword));
				Frame(); Refresh(heart);
				got = Craft(9999);
				check(got == 2 && Armory(sword) == 0 && Net(other.type) == 0, $"world craft: a max-stack craft with 2 swords makes exactly 2 (made {got}, swords left {Armory(sword)}, bars {Net(other.type)})");
				check(ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.Taken == 3, $"world craft: taken {ArmoryCraftLink.Taken}, shortfall {ArmoryCraftLink.Shortfall}");

				// 4. a levy draws the sword after the refresh; the click comes before any refresh
				unit.DepositItem(Make(other.type, per * 3));
				var s4 = Make(sword); armory.Deposit(s4);
				Frame(); Refresh(heart);
				int idx = Array.FindIndex(armory.Storage, i => ReferenceEquals(i, s4));
				var levy = new SoldierRecord { Id = 9002, Name = "Smith" }; armory.Storage[idx] = new Item(); levy.Gear.Weapon = s4;
				got = Craft(1);
				check(got == 0 && levy.Gear.HasWeapon && s4.stack == 1 && s4.type == sword && Net(other.type) == per * 3, $"world craft: a sword a levy drew after the refresh is not crafted with (made {got}, levy holds {(levy.Gear.HasWeapon ? levy.Gear.Weapon.Name : "nothing")}, bars {Net(other.type)})");

				// 5. the same sword back on a different rack slot (sorted / re-deposited) with no refresh: one craft, paid once
				levy.Gear.Weapon = new Item(); armory.Deposit(s4); // same instance, maybe another slot
				got = Craft(1);
				long paid = ArmoryCraftLink.Taken;
				check(ArmoryCraftLink.Shortfall == 0 && (got == 0 || (got == 1 && Armory(sword) == 0)), $"world craft: the sword re-racked without a refresh is crafted at most once and paid (made {got}, swords {Armory(sword)}, shortfall {ArmoryCraftLink.Shortfall})");
				Frame(); Refresh(heart);
				int again = Craft(9999);
				check(ArmoryCraftLink.Shortfall == 0 && All(sword) == 0 && got + again == 1, $"world craft: after a refresh the total made from that sword is 1 (made {got}+{again})");

				// 6. the racks change every frame while the list is refreshed (levies topping up ammo): refresh, change, click
				var ammo = Make(ItemID.MusketBall, 500); armory.Deposit(ammo);
				var s6 = Make(sword); armory.Deposit(s6);
				Frame(); Refresh(heart);
				ammo.stack -= 37; Frame(); // a levy refilled his pouch: the proxies are rebuilt, the lists are not
				got = Craft(1);
				Frame(); Refresh(heart);
				int got6 = got + Craft(9999);
				check(got6 == 1 && Armory(sword) == 0 && ArmoryCraftLink.Shortfall == 0, $"world craft: racks changing between the refresh and the click still make exactly one per sword (made {got6}, swords {Armory(sword)}, shortfall {ArmoryCraftLink.Shortfall})");
				// 7. her video: a levy carries a sword too. Craft from the armory's sword; the result goes back to the armory; the levy trades up to it and hands his sword back,
				//    so the recipe is craftable again. Every craft still costs one real sword: swords (armory + levy) and results add up.
				unit.DepositItem(Make(other.type, per * 3));
				var carried = Make(sword); var smith = new SoldierRecord { Id = 9003, Name = "Smith" }; smith.Gear.Weapon = carried;
				armory.Deposit(Make(sword));
				long swords0 = All(sword) + 1, made0 = All(made) + (smith.Gear.HasWeapon && smith.Gear.Weapon.type == made ? 1 : 0);
				Frame(); Refresh(heart);
				int c1 = Craft(1);
				ArmoryTE.TestServed = new List<SoldierRecord> { smith };
				var guc = typeof(Main).GetField("_gameUpdateCount", BindingFlags.NonPublic | BindingFlags.Static);
				try { for (int r = 0; r < 4 && !(smith.Gear.HasWeapon && smith.Gear.Weapon.type == made); r++) { guc?.SetValue(null, Main.GameUpdateCount + 1); armory.UpgradeWeapons(smith); } } finally { ArmoryTE.TestServed = null; }
				if (!(smith.Gear.HasWeapon && smith.Gear.Weapon.type == made))
				{ // the swap a levy's upgrade makes (outside a barracks the test levy is not upgraded): he takes the crafted weapon off the rack and racks his sword
					int ri = Array.FindIndex(armory.Storage, i => !i.IsAir && i.type == made);
					if (ri >= 0) { var up = armory.Storage[ri]; armory.Storage[ri] = new Item(); var old = smith.Gear.Weapon; smith.Gear.Weapon = up; armory.Deposit(old); }
				}
				bool traded = smith.Gear.HasWeapon && smith.Gear.Weapon.type == made;
				Frame(); Refresh(heart);
				int craftableAgain = CraftingGUI.AmountCraftableForCurrentRecipe();
				int c2 = Craft(1);
				long swordsNow = All(sword) + (smith.Gear.HasWeapon && smith.Gear.Weapon.type == sword ? 1 : 0), madeNow = All(made) + (traded ? 1 : 0);
				observe($"[MsWorldCraft] OBSERVED levy trade-up: crafted {c1}, levy took the {Lang.GetItemNameValue(made)}: {traded}, craftable again: {craftableAgain}, crafted {c2}; swords {swords0} -> {swordsNow}, results {made0} -> {madeNow}");
				check(c1 == 1 && (!traded || craftableAgain == 1) && swords0 - swordsNow == c1 + c2 && madeNow - made0 == c1 + c2 && ArmoryCraftLink.Shortfall == 0,
					$"world craft: a levy trading his sword back makes the recipe craftable again, and every craft costs exactly one sword (crafts {c1}+{c2}, swords {swords0}->{swordsNow}, results {made0}->{madeNow})");

				// 9-10. the network does not list the armory module at the moment of the craft, so none of the module hooks run.
				//  9: the sword is still on the rack: the craft is made and the armory pays exactly one sword.
				//  10: a levy drew the sword after the list was built: nothing is made, nothing is taken, the levy keeps it.
				unit.DepositItem(Make(other.type, per * 2));
				var s9 = Make(sword); armory.Deposit(s9);
				Frame(); Refresh(heart);
				long sw9 = All(sword), bars9 = Net(other.type), made9 = All(made);
				ArmoryCraftLink.TestHideModule = true;
				int c9;
				try { c9 = Craft(1); } finally { ArmoryCraftLink.TestHideModule = false; }
				check(((c9 == 1 && All(sword) == sw9 - 1 && Net(other.type) == bars9 - per) || (c9 == 0 && All(sword) == sw9 && Net(other.type) == bars9)) && ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.ClawedBack == 0,
					$"world craft: with the module missing from the network at the click, the craft is either made and paid or not made at all (made {c9}, swords {sw9}->{All(sword)}, bars {bars9}->{Net(other.type)}, shortfall {ArmoryCraftLink.Shortfall})");
				var s10 = Make(sword); armory.Deposit(s10);
				Frame(); Refresh(heart);
				long bars10 = Net(other.type), made10 = All(made);
				int i10 = Array.FindIndex(armory.Storage, i => ReferenceEquals(i, s10));
				var levy10 = new SoldierRecord { Id = 9004, Name = "Quartermaster" }; armory.Storage[i10] = new Item(); levy10.Gear.Weapon = s10;
				ArmoryCraftLink.TestHideModule = true;
				int c10;
				try { c10 = Craft(1); } finally { ArmoryCraftLink.TestHideModule = false; }
				check(c10 == 0 && All(made) == made10 && Net(other.type) == bars10 && levy10.Gear.HasWeapon && s10.type == sword && s10.stack == 1 && ArmoryCraftLink.Shortfall == 0,
					$"world craft: with the module missing and the sword drawn by a levy after the list was built, nothing is made (made {c10}, results {made10}->{All(made)}, bars {bars10}->{Net(other.type)}, levy keeps it: {levy10.Gear.HasWeapon}, shortfall {ArmoryCraftLink.Shortfall})");

				// 11-12. Armory items are ingredients only: no Magic Storage withdrawal hands one out. Every entry the Crafting, Decrafting and Storage interfaces offer is tried, on the armory's crafted
				//  weapon (shown in the result slot because the network has none) and on an armory Terra Blade (the result slot of the Terra Blade recipe).
				unit.Items.RemoveAll(i => i.type == made || i.type == sword); // earlier cases left some in the network; hers had none
				armory.Deposit(Make(sword));
				var gui = typeof(CraftingGUI);
				var withdrawResult = gui.GetMethod("DoWithdrawResult", S);
				var slotFocusLogic = gui.GetMethod("SlotFocusLogic", S);
				var decraftWithdraw = typeof(DecraftingGUI).GetMethod("DoWithdraw", S, null, new[] { typeof(Item), typeof(bool) }, null);
				Recipe swordRecipe = null;
				for (int i = 0; i < Recipe.numRecipes; i++) if (Main.recipe[i].createItem.type == sword) { swordRecipe = Main.recipe[i]; break; }
				foreach (var (target, targetRecipe) in new[] { (made, rec), (sword, swordRecipe) })
				{
					string tn = Lang.GetItemNameValue(target);
					if (targetRecipe == null) { check(false, "world craft: a recipe makes " + tn); continue; }
					selField.SetValue(null, targetRecipe);
					Frame(); Refresh(heart);
					var resultItem = (Item)gui.GetField("result", S).GetValue(null);
					long inArmory = Armory(target), refused0 = ArmoryCraftLink.Refused;
					check(inArmory >= 1 && Net(target) == 0 && resultItem != null && resultItem.type == target && resultItem.stack > 0, $"world craft: the result slot shows the armory's {tn} (armory {inArmory}, network {Net(target)}, result {resultItem?.Name ?? "none"} x{resultItem?.stack ?? 0})");
					var paths = new List<(string name, Func<Item> take)>
					{
						("result slot click", () => (Item)withdrawResult.Invoke(null, new object[] { 1, false })),
						("result slot shift-click (to inventory)", () => (Item)withdrawResult.Invoke(null, new object[] { resultItem.maxStack, true })),
						("result slot right-click hold", () => { Main.mouseItem = new Item(); gui.GetField("slotFocus", S)?.SetValue(null, true); gui.GetField("rightClickTimer", S)?.SetValue(null, 0); slotFocusLogic.Invoke(null, null); var m = Main.mouseItem; Main.mouseItem = new Item(); gui.GetField("slotFocus", S)?.SetValue(null, false); return m; }),
						("decrafting interface withdraw", () => (Item)decraftWithdraw.Invoke(null, new object[] { Make(target), false })),
						("decrafting interface shift-withdraw", () => (Item)decraftWithdraw.Invoke(null, new object[] { Make(target), true })),
						("storage interface withdraw", () => StorageGUI.DoWithdraw(Make(target), false)),
						("storage interface shift-withdraw", () => StorageGUI.DoWithdraw(Make(target), true)),
					};
					foreach (var (name, take) in paths)
					{
						Item gotW; try { gotW = take(); } catch (Exception e) { check(false, $"world craft: {name} of the armory's {tn} threw {e.GetType().Name}: {e.Message}"); continue; }
						ArmoryCraftLink.AfterUi(heart);
						int n = gotW == null || gotW.IsAir ? 0 : gotW.stack;
						check(n == 0 && Armory(target) == inArmory && ArmoryCraftLink.Shortfall == 0 && ArmoryCraftLink.ClawedBack == 0, $"world craft: {name} hands out no armory {tn} (got {n}, armory {inArmory}->{Armory(target)}, shortfall {ArmoryCraftLink.Shortfall})");
						Frame(); Refresh(heart); resultItem = (Item)gui.GetField("result", S).GetValue(null) ?? resultItem;
					}
					check(ArmoryCraftLink.Refused > refused0, $"world craft: the refused withdrawals of the armory's {tn} were counted for the message ({ArmoryCraftLink.Refused - refused0})");
					// still an ingredient: the proxies are back after the refused withdrawals
					check(ArmoryCraftLink.Offered(heart).Any(o => o.type == target && o.stack > 0), $"world craft: the armory's {tn} is still offered as an ingredient");
				}
				selField.SetValue(null, rec);
				Frame(); Refresh(heart);
				unit.DepositItem(Make(other.type, per));
				long swBefore = Armory(sword);
				int cAfter = Craft(1);
				check(cAfter == 1 && Armory(sword) == swBefore - 1 && ArmoryCraftLink.Shortfall == 0, $"world craft: after the refused withdrawals the armory's Terra Blade still crafts, paid once (made {cAfter}, swords {swBefore}->{Armory(sword)})");

				// 13. the Armory Heart is unlinked after the list was built, and the click comes before any refresh: the sword is never used unpaid
				armory.Deposit(Make(sword));
				Frame(); Refresh(heart);
				long sw8 = All(sword), net8 = Net(other.type);
				heart.ComponentManager.Unlink(ah.Position); ArmoryCraftLink.ResetCache();
				check(heart.GetModules().Any(m => m is ArmoryRacksModule), "world craft: an unlinked network keeps the module listed while its proxies live");
				int c8 = Craft(1);
				check(ArmoryCraftLink.Shortfall == 0 && All(sword) == sw8 - c8 && Net(other.type) == net8 - per * c8, $"world craft: crafting right after the Armory Heart is unlinked never uses an unpaid sword (made {c8}, swords {sw8}->{All(sword)}, shortfall {ArmoryCraftLink.Shortfall})");
				Frame();
				check(!heart.GetModules().Any(m => m is ArmoryRacksModule) && !ArmoryCraftLink.Offered(heart).Any(), "world craft: one frame later the proxies are retired and the module leaves the network");

				observe($"[MsWorldCraft] OBSERVED {tag} through CraftingGUI.Craft: taken off the racks {ArmoryCraftLink.Taken}, shortfall {ArmoryCraftLink.Shortfall}, returned {ArmoryCraftLink.Returned}; armory {Lang.GetItemNameValue(made)} {Armory(made)}, network {Lang.GetItemNameValue(other.type)} {Net(other.type)}");
			}
			catch (Exception e) { check(false, "world craft threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))); }
			finally
			{
				Main.netMode = nm; Main.gameMenu = menu;
				try { accessField.SetValue(sp, oldAccess); selField.SetValue(null, oldSel); } catch (Exception) { }
				foreach (var te in placed) { TileEntity.ByID.Remove(te.ID); TileEntity.ByPosition.Remove(te.Position); }
				foreach (var p in tiles) { var t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileType = 0; }
				ArmoryCraftLink.ResetForTest(); ArmoryCraftLink.ForceOn = null;
				try { typeof(CraftingGUI).GetMethod("ClearAllCollections", S)?.Invoke(null, null); } catch (Exception) { }
			}
		}

		/// <summary>What Magic Storage's refresh thread does for the Crafting Interface, run synchronously: load the items (heart + module items, the same lazy query) and build the stored-ingredient list.</summary>
		private static void Refresh(TEStorageHeart heart)
		{
			var gui = typeof(CraftingGUI);
			// what MagicUI.RefreshItems / RefreshItems_Inner do first: clear every collection (keeping the selected recipe)
			var sel = gui.GetField("selectedRecipe", S); object keep = sel.GetValue(null);
			gui.GetMethod("ClearAllCollections", S)?.Invoke(null, null);
			sel.SetValue(null, keep);
			gui.GetField("numItemsWithoutSimulators", S)?.SetValue(null, 0);
			var stateType = gui.GetNestedType("ThreadState", BindingFlags.NonPublic) ?? throw new InvalidOperationException("ThreadState not found");
			var state = Activator.CreateInstance(stateType, true);
			object sandbox = Activator.CreateInstance(typeof(EnvironmentSandbox), I, null, new object[] { Main.LocalPlayer, heart }, null);
			void Set(string f, object v) => (stateType.GetField(f, I) ?? throw new InvalidOperationException("ThreadState." + f)).SetValue(state, v);
			Set("sandbox", sandbox);
			Set("heartItems", heart.GetStoredItems());
			Set("simulatorItems", Simulator(heart, sandbox));
			Set("globalHiddenTypes", new HashSet<int>());
			var spl = StoragePlayer.LocalPlayer;
			Set("hiddenTypes", spl.HiddenRecipes); Set("favoritedTypes", spl.FavoritedRecipes);
			Set("infiniteItems", new HashSet<int>());
			var thread = new StorageGUI.ThreadContext(new CancellationTokenSource(), _ => { }, null) { heart = heart, generalFilters = new HashSet<int>(), searchText = "", modSearch = ModSearchBox.ModIndexAll, state = state };
			gui.GetMethod("ResetRecentRecipeCache", S)?.Invoke(null, null);
			(gui.GetMethod("LoadItemsAndSetDictionaryInfo", S) ?? throw new InvalidOperationException("LoadItemsAndSetDictionaryInfo")).Invoke(null, new object[] { thread, state });
			(gui.GetMethod("RefreshStorageItems", S, null, new[] { typeof(StorageGUI.ThreadContext) }, null) ?? throw new InvalidOperationException("RefreshStorageItems")).Invoke(null, new object[] { thread });
			gui.GetMethod("ResetRecentRecipeCache", S)?.Invoke(null, null);
		}

		// Magic Storage's own query: every module's additional items, live, distinct by reference
		private static IEnumerable<Item> Simulator(TEStorageHeart heart, object sandbox)
			=> heart.GetModules().SelectMany(m => m.GetAdditionalItems((EnvironmentSandbox)sandbox) ?? Array.Empty<Item>()).Where(i => i.type > ItemID.None && i.stack > 0).Distinct(ReferenceEqualityComparer.Instance).Cast<Item>();

		private static List<Item> StorageItems() => (List<Item>)typeof(CraftingGUI).GetField("storageItems", S).GetValue(null);

		private static TileEntity Place<T>(List<TileEntity> placed, int x, int y) where T : ModTileEntity
		{
			int id = ModContent.GetInstance<T>().Place(x, y);
			var te = TileEntity.ByID[id]; placed.Add(te); return te;
		}
	}
}
