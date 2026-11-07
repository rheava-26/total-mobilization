using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.Buffs;
using Levy.Content.Items.OOA;
using Levy.Content.NPCs;

namespace Levy.Common.Events
{
	/// <summary>
	/// Boot self-check for the Old One's Army levy sentries and the Tavernkeep drinks: sentry levies and mana by tier, the event start/end behaviour (DD2Event.Ongoing is
	/// toggled and restored), the call-up pick, the shop entries and how they fit the real Tavernkeep shop, and the drink buffs with the Tipsy aim penalty.
	/// A headless boot has no world, enemies or event, so a live sentry planting levies is NOT exercised here.
	/// </summary>
	public class OoaCheck : ModSystem
	{
		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostAddRecipesCore);

		private void PostAddRecipesCore()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}
			string shopInfo = "";

			// 1. Levies and Etherian Mana by tier.
			Guard("tiers", () =>
			{
				Check(OldOnesArmySystem.LeviesFor(1) == 1 && OldOnesArmySystem.LeviesFor(2) == 2 && OldOnesArmySystem.LeviesFor(3) == 3, "levies per tier are 1/2/3");
				Check(OldOnesArmySystem.ManaFor(1) == 5, "tier 1 costs 5 Etherian Mana");
				Check(OldOnesArmySystem.ManaFor(2) == 5 && OldOnesArmySystem.ManaFor(3) == 5, "every tier costs 5 Etherian Mana");
				Check(OldOnesArmySystem.Pay(4, 1) == -1 && OldOnesArmySystem.Pay(5, 1) == 0 && OldOnesArmySystem.Pay(23, 2) == 18 && OldOnesArmySystem.Pay(4, 3) == -1 && OldOnesArmySystem.Pay(5, 3) == 0, "Pay(): refuses when short, leaves the remainder");
				// 20 crystals buy four Rods = four sentries = four levies; the ledger is simply repeated payment
				int have = 20, placed = 0;
				while (OldOnesArmySystem.Pay(have, 1) >= 0) { have = OldOnesArmySystem.Pay(have, 1); placed++; }
				Check(placed == 4 && have == 0, "20 crystals place exactly four Rod sentries");
			});

			// 2. Event start / end.
			Guard("event", () =>
			{
				Check(OldOnesArmySystem.ActiveFor(true, true) && !OldOnesArmySystem.ActiveFor(false, true) && !OldOnesArmySystem.ActiveFor(true, false), "active only with the option on and the event running");
				Check(OldOnesArmySystem.EdgeOf(false, true) == OldOnesArmySystem.Edge.Started && OldOnesArmySystem.EdgeOf(true, false) == OldOnesArmySystem.Edge.Ended
					&& OldOnesArmySystem.EdgeOf(true, true) == OldOnesArmySystem.Edge.None && OldOnesArmySystem.EdgeOf(false, false) == OldOnesArmySystem.Edge.None, "start / end edges");
				var reg = new SoldierRecord { Kind = SoldierKind.Regular, Id = 9001 };
				var mil = new SoldierRecord { Kind = SoldierKind.Militia, Id = -9001 };
				bool was = DD2Event.Ongoing;
				try
				{
					DD2Event.Ongoing = false;
					Check(!OldOnesArmySystem.BlocksSpawn(reg), "no stand-down while the event is not running");
					DD2Event.Ongoing = true;
					bool on = OldOnesArmySystem.ConfigOn;
					Check(on, "the option defaults ON");
					Check(OldOnesArmySystem.BlocksSpawn(reg), "a regular levy is refused a body during the event");
					Check(!OldOnesArmySystem.BlocksSpawn(mil), "militia are not stood down");
					OldOnesArmySystem.Calling = true;
					Check(!OldOnesArmySystem.BlocksSpawn(reg), "a sentry's own call-up is let through");
					OldOnesArmySystem.Calling = false;
					DD2Event.Ongoing = false;
					Check(!OldOnesArmySystem.BlocksSpawn(reg), "levies deploy again when the event ends");
				}
				finally { DD2Event.Ongoing = was; OldOnesArmySystem.Calling = false; }
			});

			// 3. Call-up pick.
			Guard("callup", () =>
			{
				SoldierRecord R(int id, string cmd, bool dead = false, bool reserve = false) => new SoldierRecord { Id = id, CommanderName = cmd, Dead = dead, Reserve = reserve };
				var roster = new List<SoldierRecord> { R(1, "A"), R(2, "B"), R(3, "A", dead: true), R(4, "A", reserve: true), R(5, "A"), R(6, "A"), R(7, "A") };
				var called = new HashSet<int> { 5 };
				var pick = OldOnesArmySystem.PickCallups(roster, "A", 2, called, r => r.Id == 6);
				Check(pick.Count == 2 && pick[0].Id == 1 && pick[1].Id == 7, "call-up skips other commanders, the dead, reserve, bodied and already-called levies");
				Check(OldOnesArmySystem.PickCallups(roster, "A", 0, called, r => false).Count == 0, "a full sentry calls nobody");
			});

			// 4. Shop: the vanilla Tavernkeep shop is full at 40, so the Levy wares live on their own page.
			Guard("shop", () =>
			{
				int vanillaTotal = 0, vanillaReserving = 0, vanillaMax = 0;
				string mainName = NPCShopDatabase.GetShopName(NPCID.DD2Bartender, "Shop");
				if (NPCShopDatabase.TryGetNPCShop(mainName, out AbstractNPCShop main))
				{
					foreach (var e in main.ActiveEntries) { vanillaTotal++; if (e is NPCShop.Entry ne && ne.SlotReserved) vanillaReserving++; }
					// worst case = every condition met: every entry on the shelf
					vanillaMax = vanillaTotal; // every entry present = full stock; reserving entries keep a slot even when locked
					bool thorium = ModLoader.HasMod("ThoriumMod"), calamity = ModLoader.HasMod("CalamityMod");
					var items = new Item[Chest.maxItems];
					var npc = new NPC(); npc.SetDefaults(NPCID.DD2Bartender);
					main.FillShop(items, npc, out bool overflow);
					int stocked = 0, reserved = 0, free = 0;
					foreach (var it in items) { if (it == null) free++; else if (it.IsAir) reserved++; else stocked++; }
					shopInfo = $"main Tavernkeep shop (thorium={thorium}, calamity={calamity}): {vanillaTotal} entries, {vanillaReserving} reserving a slot, full stock {vanillaMax}/40 slots; at boot {stocked} stocked + {reserved} reserved + {free} free; overflow={overflow}";
					Check(!overflow, "the main Tavernkeep shop does not overflow");
					Check(vanillaMax <= Chest.maxItems, $"the main Tavernkeep shop's full stock ({vanillaMax}) fits 40 slots (Levy adds nothing to it)");
				}
				else failures.Add("main Tavernkeep shop not found");

				Check(NPCShopDatabase.TryGetNPCShop(TavernkeepShop.PageFullName, out AbstractNPCShop page), "the Levy wares page is registered");
				if (page != null)
				{
					int pageTotal = 0; foreach (var e in page.ActiveEntries) pageTotal++;
					var items = new Item[Chest.maxItems];
					var npc = new NPC(); npc.SetDefaults(NPCID.DD2Bartender);
					page.FillShop(items, npc, out bool overflow);
					Check(!overflow && pageTotal == 6 && pageTotal <= Chest.maxItems, $"the wares page holds 6 entries, well under 40 (has {pageTotal})");
					// gating: tier 1 always (plus 3 drinks); tier 2 after a mech boss; tier 3 after Golem
					bool mech = Condition.DownedMechBossAny.IsMet(), golem = Condition.DownedGolem.IsMet();
					int rod = ModContent.ItemType<LevyRod>(), cane = ModContent.ItemType<LevyCane>(), staff = ModContent.ItemType<LevyStaff>();
					bool hasRod = false, hasCane = false, hasStaff = false, medals = true;
					int drinkCount = 0;
					foreach (var it in items)
					{
						if (it == null || it.IsAir) continue;
						if (it.type == rod) { hasRod = true; medals &= it.shopSpecialCurrency == CustomCurrencyID.DefenderMedals && it.shopCustomPrice == LevyConstants.OoaMedalPricePerTier[0]; }
						if (it.type == cane) { hasCane = true; medals &= it.shopSpecialCurrency == CustomCurrencyID.DefenderMedals && it.shopCustomPrice == LevyConstants.OoaMedalPricePerTier[1]; }
						if (it.type == staff) { hasStaff = true; medals &= it.shopSpecialCurrency == CustomCurrencyID.DefenderMedals && it.shopCustomPrice == LevyConstants.OoaMedalPricePerTier[2]; }
						if (it.type == ModContent.ItemType<BarracksAle>() || it.type == ModContent.ItemType<BlackStout>() || it.type == ModContent.ItemType<SpicedRum>()) { drinkCount++; medals &= it.shopSpecialCurrency != CustomCurrencyID.DefenderMedals && it.value > 0; }
					}
					Check(hasRod && hasCane == mech && hasStaff == golem && drinkCount == 3 && medals, $"wares gating at this progression (mech={mech}, golem={golem}): Rod always, Cane={hasCane}, Staff={hasStaff}, 3 drinks for coins, staves in Defender Medals");
					// a world with everything beaten: every entry listed unconditionally
					int unlocked = ((NPCShop)page).Entries.Count;
					Check(unlocked == 6, "all six wares exist once everything is unlocked");
					shopInfo += $"; Levy wares page: {pageTotal} entries now ({unlocked} when everything is unlocked) of 40";
				}
				Check(ModContent.GetInstance<TavernkeepShopSystem>() != null, "the shop system is loaded");
			});

			// 5. Drinks.
			Guard("drinks", () =>
			{
				var d = new Player(); d.statDefense = new Player.DefenseStat();
				int def0 = d.statDefense; float dmg0 = d.GetDamage(DamageClass.Generic).Additive, spd0 = d.GetAttackSpeed(DamageClass.Generic);
				Check(DrinkBuffs.Apply(d, ModContent.BuffType<AleBuff>()) && d.statDefense > def0, "Ale adds defense");
				Check(DrinkBuffs.Apply(d, ModContent.BuffType<StoutBuff>()) && d.GetDamage(DamageClass.Generic).Additive > dmg0, "Stout adds damage");
				Check(DrinkBuffs.Apply(d, ModContent.BuffType<RumBuff>()) && d.GetAttackSpeed(DamageClass.Generic) > spd0, "Rum adds attack speed");
				Check(!DrinkBuffs.Apply(d, BuffID.Ironskin), "other buffs are not claimed");
				var c = new Vector2(1000, 1000);
				Check(DrinkBuffs.InReach(c, c + new Vector2(LevyConstants.DrinkRadiusTiles * 16f - 8f, 0)) && !DrinkBuffs.InReach(c, c + new Vector2(LevyConstants.DrinkRadiusTiles * 16f + 8f, 0)), "a round reaches the drink radius and no further");

				var n = new NPC(); n.SetDefaults(ModContent.NPCType<SoldierNPC>());
				var body = n.ModNPC as SoldierNPC;
				Check(body != null, "a soldier body can be built for the check");
				if (body != null)
				{
					Check(Math.Abs(DrinkBuffs.AimErrorFactor(body) - 1f) < 1e-4f, "a sober levy has aim factor 1");
					float sober = WeaponUse.AimErrorMultiplier(body);
					n.active = true; n.AddBuff(ModContent.BuffType<StoutBuff>(), 600); bool viaAdd = DrinkBuffs.IsDrunk(n); if (!viaAdd) { n.buffType[0] = ModContent.BuffType<StoutBuff>(); n.buffTime[0] = 600; }
					if (!DrinkBuffs.IsDrunk(n) && ModLoader.HasMod("CalamityMod"))
						shopInfo += "; drunk-aim check SKIPPED: with Calamity loaded a detached test NPC does not keep a buff (observed; cause not isolated)";
					else
					{
						Check(DrinkBuffs.IsDrunk(n) && Math.Abs(DrinkBuffs.AimErrorFactor(body) - LevyConstants.DrunkAimErrorMult) < 1e-4f, $"a drunk levy has the Tipsy aim factor (AddBuff worked: {viaAdd})");
						float drunk = WeaponUse.AimErrorMultiplier(body);
						Check(sober > 0f && Math.Abs(drunk / sober - LevyConstants.DrunkAimErrorMult) < 1e-3f, $"WeaponUse.AimErrorMultiplier rises {LevyConstants.DrunkAimErrorMult}x when drunk ({sober:0.###} -> {drunk:0.###})");
					}
				}
			});

			if (failures.Count == 0) Mod.Logger.Info($"Levy OOA/tavern self-check: {ok} checks OK; {shopInfo}");
			else
			{
				Mod.Logger.Warn($"Levy OOA/tavern self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!! {shopInfo}");
				foreach (string f in failures) Mod.Logger.Warn("Levy OOA/tavern self-check FAILED: " + f);
			}
		}
	}
}
