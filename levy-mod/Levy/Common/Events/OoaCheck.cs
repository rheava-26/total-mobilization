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
		public override void PostAddRecipes()
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
				Check(OldOnesArmySystem.ManaFor(2) > OldOnesArmySystem.ManaFor(1) && OldOnesArmySystem.ManaFor(3) > OldOnesArmySystem.ManaFor(2), "mana rises with tier");
				Check(OldOnesArmySystem.Pay(4, 1) == -1 && OldOnesArmySystem.Pay(5, 1) == 0 && OldOnesArmySystem.Pay(23, 2) == 15 && OldOnesArmySystem.Pay(11, 3) == -1 && OldOnesArmySystem.Pay(12, 3) == 0, "Pay(): refuses when short, leaves the remainder");
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

			// 4. Shop.
			Guard("shop", () =>
			{
				var w0 = TavernkeepShop.Wanted(false, false, out int s0);
				var w2 = TavernkeepShop.Wanted(true, false, out int s2);
				var w3 = TavernkeepShop.Wanted(true, true, out int s3);
				Check(s0 == 1 && s2 == 2 && s3 == 3 && w0.Count == 4 && w2.Count == 5 && w3.Count == 6, "staves gated: tier 1 always, tier 2 after a mech boss, tier 3 after Golem (+3 drinks)");
				Check(w3[0].type == ModContent.ItemType<LevyRod>() && w3[1].type == ModContent.ItemType<LevyCane>() && w3[2].type == ModContent.ItemType<LevyStaff>(), "staves are Rod, Cane, Staff");
				Check(w3[2].shopSpecialCurrency == CustomCurrencyID.DefenderMedals && w3[2].shopCustomPrice == LevyConstants.OoaMedalPricePerTier[2], "staves are priced in Defender Medals");
				Check(w3[3].shopSpecialCurrency != CustomCurrencyID.DefenderMedals && w3[3].value > 0, "drinks are sold for coins");
				// layout: roomy shop, tight shop, no room
				var roomy = new Item[40]; roomy[39] = null;
				Check(TavernkeepShop.Lay(roomy, TavernkeepShop.Wanted(true, true, out _), 3, out bool c1) == 6 && !c1, "six entries fit an empty shop");
				var tight = new Item[40]; for (int i = 0; i < 36; i++) tight[i] = new Item();
				int p2 = TavernkeepShop.Lay(tight, TavernkeepShop.Wanted(true, true, out int st), st, out bool c2);
				Check(p2 == 4 && c2, "with 4 free slots the three staves collapse to the top tier and the drinks still fit");
				var full = new Item[40]; for (int i = 0; i < 40; i++) full[i] = new Item();
				Check(TavernkeepShop.Lay(full, TavernkeepShop.Wanted(true, true, out int st2), st2, out _) == 0, "a full shop is never overwritten");

				// the real shop
				if (NPCShopDatabase.TryGetNPCShop(NPCShopDatabase.GetShopName(NPCID.DD2Bartender, "Shop"), out AbstractNPCShop real))
				{
					var items = new Item[40];
					var npc = new NPC(); npc.SetDefaults(NPCID.DD2Bartender);
					real.FillShop(items, npc, out bool overflow);
					int filled = 0, reserved = 0, nulls = 0;
					foreach (var it in items) { if (it == null) nulls++; else if (it.IsAir) reserved++; else filled++; }
					int total = 0, reservedEntries = 0;
					foreach (var e in real.ActiveEntries) { total++; if (e is NPCShop.Entry ne && ne.SlotReserved) reservedEntries++; }
					shopInfo = $"vanilla Tavernkeep shop: {total} entries ({reservedEntries} reserve a slot), at boot {filled} stocked + {reserved} reserved + {nulls} free of 40; overflow={overflow}; our worst case needs 6 free (collapse needs 4)";
					Check(!overflow, "the vanilla Tavernkeep shop does not overflow by itself");
					Check(nulls >= 4, $"the Tavernkeep shop keeps at least 4 free slots at boot (has {nulls})");
				}
				else shopInfo = "vanilla Tavernkeep shop not found in NPCShopDatabase";
			});

			// 5. Drinks.
			Guard("drinks", () =>
			{
				var d = new Player();
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
					n.buffType[0] = ModContent.BuffType<StoutBuff>(); n.buffTime[0] = 600;
					Check(DrinkBuffs.IsDrunk(n) && Math.Abs(DrinkBuffs.AimErrorFactor(body) - LevyConstants.DrunkAimErrorMult) < 1e-4f, "a drunk levy has the Tipsy aim factor");
					float drunk = WeaponUse.AimErrorMultiplier(body);
					Check(sober > 0f && Math.Abs(drunk / sober - LevyConstants.DrunkAimErrorMult) < 1e-3f, $"WeaponUse.AimErrorMultiplier rises {LevyConstants.DrunkAimErrorMult}x when drunk ({sober:0.###} -> {drunk:0.###})");
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
