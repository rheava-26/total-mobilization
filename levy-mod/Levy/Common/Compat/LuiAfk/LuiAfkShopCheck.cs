using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Compat.LuiAfk
{
	/// <summary>
	/// Boot self-check for the LuiAFK Reborn repairs. Runs only when LuiAFK is loaded. It opens every shop LuiAFK adds or edits the way the game does (Chest.SetupShop, so the hooks run), plus the one step a click
	/// on the "Shop" button performs before that (ModNPC.OnChatButtonClicked must name a registered shop), under several world states, and prints one PASS/FAIL line per shop and a final pass=N fail=M.
	/// With env LEVY_LUIAFK_REPAIR=0 the repairs are off and the same check reports the unrepaired defects. A headless boot has no client: the drawn shop window and the mouse click itself are NOT exercised.
	/// </summary>
	public class LuiAfkShopCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			if (!LuiAfkShops.TryGet(out _)) return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private int pass, fail;
		private readonly List<string> lines = new List<string>();

		private void Check(string shop, bool ok, string detail)
		{
			if (ok) pass++; else fail++;
			lines.Add($"  [{(ok ? "PASS" : "FAIL")}] {shop}: {detail}");
		}

		private static List<Item> Open(int npcType, string shopName, NPC npc, out string error)
		{
			error = null;
			var chest = new Chest();
			try { chest.SetupShop(NPCShopDatabase.GetShopName(npcType, shopName), npc); }
			catch (Exception e) { error = e.GetType().Name + ": " + e.Message.Split('\n')[0]; }
			return chest.item.Where(i => i != null && !i.IsAir).ToList();
		}

		private static string Dups(List<Item> items)
		{
			var d = items.GroupBy(i => i.type).Where(g => g.Count() > 1).Select(g => $"{g.First().Name} x{g.Count()}").ToList();
			return d.Count == 0 ? "" : string.Join(", ", d);
		}

		private void Run()
		{
			pass = fail = 0; lines.Clear();
			bool bm = Main.bloodMoon, ec = Main.eclipse, hm = Main.hardMode, day = Main.dayTime, cr = WorldGen.crimson, sm = NPC.savedMech, sw = NPC.savedWizard;
			int slot = Main.maxNPCs - 1; var spare = Main.npc[slot]; bool spareWasActive = spare.active; int spareType = spare.type;
			try
			{
				int fairy = LuiAfkShops.NpcTypeOf("MobileMerchant"), skel = LuiAfkShops.NpcTypeOf("SkeletonMerchant"), trav = LuiAfkShops.NpcTypeOf("TravellingMerchant");
				NPC Dummy(int type) { var n = new NPC(); n.SetDefaults(type); n.active = true; return n; }

				// 0. The click: each LuiAFK shop NPC must hand the game the name of a shop that is registered. (This is the whole of the click's effect before the shop is filled.)
				foreach (var (label, type) in new[] { ("Fairy Merchant", fairy), ("Skeleton Merchant", skel), ("Travelling Merchant", trav) })
				{
					if (type < 0) { Check(label + " click", false, "NPC not found in LuiAFK"); continue; }
					string name = null;
					try { ModContent.GetModNPC(type).OnChatButtonClicked(true, ref name); } catch (Exception e) { Check(label + " click", false, "OnChatButtonClicked threw " + e.GetType().Name); continue; }
					bool reg = name != null && NPCShopDatabase.TryGetNPCShop(NPCShopDatabase.GetShopName(type, name), out _);
					Check(label + " click", reg, $"first button names shop '{name}' -> registered={reg}");
				}

				// 1. Fairy Merchant: no duplicates in any state; the four load-frozen items follow the world.
				if (fairy >= 0)
				{
					var dupStates = new List<string>();
					foreach (bool d in new[] { true, false }) foreach (bool h in new[] { false, true })
					{
						Main.dayTime = d; Main.hardMode = h;
						var items = Open(fairy, LuiAfkShops.FairyShopName, Dummy(fairy), out string err);
						string dd = Dups(items);
						if (err != null) dupStates.Add($"threw {err}");
						else if (dd != "") dupStates.Add($"day={d} hard={h}: {dd}");
					}
					Check("Fairy Merchant duplicates", dupStates.Count == 0, dupStates.Count == 0 ? "no item listed twice (day/night x pre/hardmode)" : string.Join(" | ", dupStates));

					Main.dayTime = true; Main.hardMode = false;
					NPC.savedMech = false; NPC.savedWizard = false;
					bool Has(List<Item> l, int t) => l.Any(i => i.type == t);
					var none = Open(fairy, LuiAfkShops.FairyShopName, Dummy(fairy), out _);
					bool noneOk = !LuiAfkShops.FrozenItems.Any(t => Has(none, t));
					NPC.savedMech = true; NPC.savedWizard = true;
					var arm = Main.npc[slot]; arm.SetDefaults(NPCID.ArmsDealer); arm.active = true;
					var all = Open(fairy, LuiAfkShops.FairyShopName, Dummy(fairy), out _);
					arm.active = false;
					var missing = LuiAfkShops.FrozenItems.Where(t => !Has(all, t)).Select(t => Lang.GetItemNameValue(t)).ToList();
					Check("Fairy Merchant world-gated items", noneOk && missing.Count == 0,
						$"nothing extra before the rescues ({noneOk}); with Arms Dealer present + Mechanic + Wizard rescued, missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing))}");
				}

				// 2. Steampunker: Crimson world on a Blood Moon.
				{
					Main.dayTime = false; WorldGen.crimson = true; Main.bloodMoon = true; Main.hardMode = true;
					var items = Open(NPCID.Steampunker, "Shop", Dummy(NPCID.Steampunker), out string err);
					string dd = Dups(items);
					Check("Steampunker", err == null && dd == "" && items.Count > 0, err != null ? "threw " + err : $"{items.Count} items on a Crimson Blood Moon night, duplicates: {(dd == "" ? "none" : dd)}");
				}

				// 3. Shops that must simply open with items (no exception, not empty) in a plain state.
				Main.dayTime = true; Main.bloodMoon = false; Main.eclipse = false; WorldGen.crimson = cr; Main.hardMode = false;
				foreach (var (label, type, name) in new[] { ("Skeleton Merchant shop", skel, "LuiAFK Skeleton Merchant Shop"), ("Fairy Merchant shop", fairy, LuiAfkShops.FairyShopName), ("Merchant (LuiAFK adds)", NPCID.Merchant, "Shop"), ("Dryad (LuiAFK adds)", NPCID.Dryad, "Shop"), ("Arms Dealer (LuiAFK adds)", NPCID.ArmsDealer, "Shop") })
				{
					if (type < 0) { Check(label, false, "NPC not found"); continue; }
					var items = Open(type, name, Dummy(type), out string err);
					Check(label, err == null && items.Count > 0 && items.All(i => i.type > 0), err != null ? "threw " + err : $"opens with {items.Count} items");
				}
				if (trav >= 0)
				{
					NPCShopDatabase.TryGetNPCShop(NPCShopDatabase.GetShopName(trav, "Shop"), out var tShop);
					var items = Open(trav, "Shop", Dummy(trav), out string err);
					Check("Travelling Merchant shop", err == null && tShop != null, err != null ? "threw " + err : $"registered, fills from Main.travelShop ({items.Count} items now; the roster is filled each dawn)");
				}

				// 4. Dedicated server: ResetEffects on a detached Player (whoAmI = Main.myPlayer = 255, like Levy's dummies) must not throw out of LuiAFK's UI reset. A client builds those UIs, so this only applies to a server.
				if (Main.dedServ)
				{
					string thrown = null;
					try { var dummy = new Player(); dummy.whoAmI = Main.myPlayer; dummy.ResetEffects(); } catch (Exception e) { thrown = e.GetType().Name + " in " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim(); }
					Check("Dedicated server detached-Player ResetEffects", thrown == null, thrown == null ? $"Player.ResetEffects completes (LuiafkUI.ResetUIs patched={LuiAfkServerRepair.Hooked})" : "threw " + thrown);
				}
			}
			catch (Exception e) { Check("self-check", false, "threw " + e.GetType().Name + ": " + e.Message); }
			finally
			{
				Main.bloodMoon = bm; Main.eclipse = ec; Main.hardMode = hm; Main.dayTime = day; WorldGen.crimson = cr; NPC.savedMech = sm; NPC.savedWizard = sw;
				var s = Main.npc[slot]; s.active = spareWasActive; if (!spareWasActive) s.type = spareType;
			}

			var sb = new StringBuilder();
			sb.Append($"LuiAFK shop repair self-check (repair {(LuiAfkShops.Disabled ? "OFF" : "on")}): pass={pass} fail={fail}");
			foreach (string l in lines) sb.Append('\n').Append(l);
			if (fail == 0) Mod.Logger.Info(sb.ToString()); else Mod.Logger.Warn(sb.ToString());
		}
	}
}
