using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Boot self-check (headless server, throwaway armories and records) of the wings and boots slots, logged as "[MobSlots]": the classifier read off every loaded item (counts and the classified list, so it can be reviewed),
	/// what each slot accepts, the armory filling both slots first from the shelf and never dealing wings or boots into an ordinary accessory slot, the preferred setting, the old-save migration, and the save, copy and
	/// network round trips. The measured top speed and flight time of a levy wearing them are the world check's (<see cref="Levy.Common.Testing.MobilityWorldCheck"/>).
	/// </summary>
	public sealed class MobilitySlotsCheck : ModSystem
	{
		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }
		private static string N(Item it) => it == null || it.IsAir ? "-" : it.Name;

		public override void PostSetupRecipes()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[MobSlots] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			void Info(string line) => Mod.Logger.Info("[MobSlots] " + line);
			var savedServed = ArmoryTE.TestServed;
			try
			{
				WeaponRank.Force(WeaponRankMode.Damage, false);
				ArmorDoctrine.Reset();
				Classifier(Check, Info);
				SlotRules(Check);
				Filling(Check, Info);
				Preferred(Check, Info);
				Upgrades(Check, Info);
				Migration(Check, Info);
				Dashes(Check, Info);
				RoundTrips(Check);
			}
			catch (Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
			finally { ArmoryTE.TestServed = savedServed; WeaponRank.Release(); }
			Mod.Logger.Info($"[MobSlots] self-check DONE pass={pass} fail={fail}");
		}

		private delegate void CheckFn(string name, bool ok, string detail = "");

		// ------------------------------------------------------------------ the classifier, read off every loaded item
		private static void Classifier(CheckFn check, Action<string> info)
		{
			var wings = new List<Item>(); var boots = new List<Item>(); int both = 0;
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item s) || s == null || s.IsAir) continue;
				bool w = MobilitySlots.IsWings(s), b = MobilitySlots.IsBoots(s);
				if (w && b) both++;
				if (w) wings.Add(s); else if (b) boots.Add(s);
			}
			string Mod_(Item it) => it.ModItem?.Mod.Name ?? "Terraria";
			int wv = wings.Count(i => i.ModItem == null), bv = boots.Count(i => i.ModItem == null);
			info($"classified from item properties (accessory, not vanity, wingSlot > 0 / shoeSlot > 0): {wings.Count} wings ({wv} vanilla, {wings.Count - wv} modded), {boots.Count} boots ({bv} vanilla, {boots.Count - bv} modded)");
			info("BOOTS vanilla: " + string.Join(", ", boots.Where(i => i.ModItem == null).Select(i => i.Name)));
			info("WINGS vanilla: " + string.Join(", ", wings.Where(i => i.ModItem == null).Select(i => i.Name)));
			foreach (var g in boots.Where(i => i.ModItem != null).GroupBy(Mod_)) info($"BOOTS {g.Key} ({g.Count()}): " + string.Join(", ", g.Select(i => i.Name)));
			foreach (var g in wings.Where(i => i.ModItem != null).GroupBy(Mod_)) info($"WINGS {g.Key} ({g.Count()}): " + string.Join(", ", g.Select(i => i.Name)));
			check("no item is both wings and boots", both == 0, both.ToString());
			check("Angel Wings, Fledgling Wings and the Jetpack are wings", new[] { ItemID.AngelWings, ItemID.CreativeWings, ItemID.Jetpack, ItemID.LongRainbowTrailWings }.All(t => MobilitySlots.IsWings(Make(t))));
			check("Hermes, Rocket, Spectre, Lightning and Terraspark Boots are boots", new[] { ItemID.HermesBoots, ItemID.RocketBoots, ItemID.SpectreBoots, ItemID.LightningBoots, ItemID.TerrasparkBoots, ItemID.FlurryBoots }.All(t => MobilitySlots.IsBoots(Make(t))));
			check("a shield, a bottle, a band and the utility shoes are neither", new[] { ItemID.ObsidianShield, ItemID.CloudinaBottle, ItemID.BandofRegeneration, ItemID.ShinyRedBalloon, ItemID.ShoeSpikes, ItemID.TigerClimbingGear, ItemID.Tabi, ItemID.MasterNinjaGear, ItemID.FrogLeg, ItemID.Flipper, ItemID.IceSkates, ItemID.WaterWalkingBoots, ItemID.FlowerBoots }.All(t => !MobilitySlots.Is(Make(t))));
			var utility = new List<Item>(); for (int t = 1; t < ItemLoader.ItemCount; t++) if (ContentSamples.ItemsByType.TryGetValue(t, out Item us) && us != null && !us.IsAir && us.accessory && !us.vanity && us.shoeSlot > 0 && us.wingSlot <= 0 && !MobilitySlots.IsBoots(us)) utility.Add(us);
			info($"UTILITY SHOES (shoeSlot > 0, no run speed or rocket flight; ordinary accessories) ({utility.Count}): " + string.Join(", ", utility.Select(i => (i.ModItem != null ? i.ModItem.Mod.Name + ":" : "") + i.Name)));
			check("the Spectre, Lightning and Terraspark Boots stay boots although they also walk on water", MobilitySlots.IsBoots(Make(ItemID.TerrasparkBoots)) && MobilitySlots.IsBoots(Make(ItemID.WaterWalkingBoots)) == false);
			var treads = FindModded("CalamityMod", "AngelTreads");
			if (treads != null) check("Calamity's Angel Treads are boots", MobilitySlots.IsBoots(treads) && !MobilitySlots.IsWings(treads), $"shoeSlot={treads.shoeSlot}");
			if (ModLoader.HasMod("ThoriumMod")) check("Thorium has boots classified", boots.Any(i => i.ModItem?.Mod.Name == "ThoriumMod"), boots.Count(i => i.ModItem?.Mod.Name == "ThoriumMod").ToString());
		}

		private static Item FindModded(string mod, string name) => ModLoader.TryGetMod(mod, out Mod m) && m.TryFind<ModItem>(name, out var mi) ? Make(mi.Type) : null;

		// ------------------------------------------------------------------ what each slot takes
		private static void SlotRules(CheckFn check)
		{
			var g = new SoldierGear();
			var aw = Make(ItemID.AngelWings); var hb = Make(ItemID.HermesBoots); var band = Make(ItemID.BandofRegeneration);
			check("the wings slot takes wings only", g.CanHold(SoldierGear.SlotWings, aw) && !g.CanHold(SoldierGear.SlotWings, hb) && !g.CanHold(SoldierGear.SlotWings, band));
			check("the boots slot takes boots only", g.CanHold(SoldierGear.SlotBoots, hb) && !g.CanHold(SoldierGear.SlotBoots, aw) && !g.CanHold(SoldierGear.SlotBoots, band));
			check("an ordinary accessory slot refuses wings and boots and takes the rest", !g.CanHold(SoldierGear.SlotAcc0, aw) && !g.CanHold(SoldierGear.SlotAcc0, hb) && g.CanHold(SoldierGear.SlotAcc0, band));
			check("both slots are open at the lowest grade", g.TrySet(SoldierGear.SlotWings, aw, 0, out _) && g.TrySet(SoldierGear.SlotBoots, hb, 0, out _) && g.HasWings && g.HasBoots);
			check("a hand-placed pair is locked and counts in AllItems", g.IsLocked(SoldierGear.SlotWings) && g.IsLocked(SoldierGear.SlotBoots) && g.AllItems().Count(i => i.type == ItemID.AngelWings || i.type == ItemID.HermesBoots) == 2);
			g.TrySet(SoldierGear.SlotWings, new Item(), 0, out _);
			check("taking wings out clears the lock", !g.HasWings && !g.IsLocked(SoldierGear.SlotWings));
			check("the slot count still fits the lock mask", SoldierGear.SlotCount <= 31, SoldierGear.SlotCount.ToString());
		}

		// ------------------------------------------------------------------ the armory fills the slots first and keeps them out of the accessory slots
		private static string Worn(SoldierRecord r) => string.Join(", ", r.Gear.Accessories.Take(r.AccessorySlots).Select(N));
		private static bool NoMobilityWorn(SoldierRecord r) => r.Gear.Accessories.All(a => a.IsAir || !MobilitySlots.Is(a));

		private static void Filling(CheckFn check, Action<string> info)
		{
			{ // the case from the recordings: wings and boots both stocked, one levy
				var te = new ArmoryTE();
				foreach (var t in new[] { ItemID.CreativeWings, ItemID.AngelWings, ItemID.HermesBoots, ItemID.SpectreBoots, ItemID.ObsidianShield, ItemID.BandofRegeneration, ItemID.CloudinaBottle }) te.Deposit(Make(t));
				var levy = new SoldierRecord { Id = 61, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"levy, stock = 2 wings, 2 boots, 2 shields and bands, a bottle: wings [{N(levy.Gear.Wings)}] boots [{N(levy.Gear.Boots)}] accessories [{Worn(levy)}] ({levy.AccessorySlots} slots)");
				check("a levy is given both a pair of wings and a pair of boots", levy.Gear.HasWings && levy.Gear.HasBoots, $"{N(levy.Gear.Wings)} / {N(levy.Gear.Boots)}");
				check("the best pair of each is chosen", levy.Gear.Boots.type == ItemID.SpectreBoots && levy.Gear.Wings.type == ItemID.AngelWings, $"{N(levy.Gear.Wings)} / {N(levy.Gear.Boots)}");
				check("no wings or boots in an accessory slot", NoMobilityWorn(levy), Worn(levy));
				check("the accessory slots are all filled with ordinary accessories", levy.Gear.Accessories.Take(levy.AccessorySlots).Count(a => !a.IsAir) == Math.Min(levy.AccessorySlots, 3), Worn(levy));
				check("the pairs not worn stay on the shelf", te.Storage.Any(i => !i.IsAir && i.type == ItemID.CreativeWings) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.HermesBoots));
			}
			{ // 39 levies, 40 pairs of wings, 5 pairs of boots: the boots go out, the wings never take an accessory slot
				var te = new ArmoryTE(); var army = new List<SoldierRecord>();
				for (int i = 0; i < 40; i++) te.Deposit(Make(ItemID.AngelWings));
				for (int i = 0; i < 5; i++) te.Deposit(Make(ItemID.SpectreBoots));
				for (int i = 0; i < 10; i++) { te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.BandofRegeneration)); }
				for (int i = 0; i < 12; i++) army.Add(new SoldierRecord { Id = 100 + i, Grade = Grade.Levy });
				ArmoryTE.TestServed = army;
				foreach (var r in army) te.IssueGear(r);
				int w = army.Count(r => r.Gear.HasWings), b = army.Count(r => r.Gear.HasBoots), stray = army.Count(r => !NoMobilityWorn(r));
				info($"12 levies, 40 wings, 5 boots in stock: {w} wear wings, {b} wear boots, {stray} wear any in an accessory slot");
				check("every levy gets wings, and the boots there are go out too", w == 12 && b == 5, $"wings {w} boots {b}");
				check("no levy wears wings or boots in an accessory slot", stray == 0);
			}
			{ // nothing but boots stocked: the boots slot takes them, the free accessory slots do not
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.HermesBoots)); te.Deposit(Make(ItemID.LightningBoots));
				var levy = new SoldierRecord { Id = 62, Grade = Grade.Champion };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				check("a free accessory slot is left empty rather than given boots", levy.Gear.Boots.type == ItemID.LightningBoots && levy.Gear.Accessories.All(a => a.IsAir), $"{N(levy.Gear.Boots)} / [{Worn(levy)}]");
			}
			{ // Terraspark Boots in the boots slot and Master Ninja Gear in an accessory slot, both stocked
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.TerrasparkBoots)); te.Deposit(Make(ItemID.MasterNinjaGear)); te.Deposit(Make(ItemID.ShoeSpikes)); te.Deposit(Make(ItemID.BandofRegeneration));
				var levy = new SoldierRecord { Id = 73, Grade = Grade.Champion };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"Terraspark Boots + Master Ninja Gear stocked: boots [{N(levy.Gear.Boots)}] accessories [{Worn(levy)}]");
				check("a levy with Terraspark Boots in his boots slot also wears Master Ninja Gear as an accessory", levy.Gear.Boots.type == ItemID.TerrasparkBoots && levy.Gear.Accessories.Any(a => a.type == ItemID.MasterNinjaGear), $"{N(levy.Gear.Boots)} / {Worn(levy)}");
				var g = new SoldierGear();
				check("utility shoes may be hand-placed in an accessory slot; the boots slot refuses the ones that neither run nor dash and takes a dash", g.CanHold(SoldierGear.SlotAcc0, Make(ItemID.MasterNinjaGear)) && g.CanHold(SoldierGear.SlotAcc0, Make(ItemID.ShoeSpikes)) && !g.CanHold(SoldierGear.SlotBoots, Make(ItemID.ShoeSpikes)) && g.CanHold(SoldierGear.SlotBoots, Make(ItemID.MasterNinjaGear)));
			}
			{ // wings already worn: extra wings take no accessory slot
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.CreativeWings)); te.Deposit(Make(ItemID.BandofRegeneration));
				var levy = new SoldierRecord { Id = 63, Grade = Grade.Levy };
				levy.Gear.Wings = Make(ItemID.AngelWings); levy.Gear.SetLocked(SoldierGear.SlotWings, true);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				check("wings in the wings slot: a second pair never takes an accessory slot", levy.Gear.Wings.type == ItemID.AngelWings && NoMobilityWorn(levy) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.CreativeWings), $"{N(levy.Gear.Wings)} / [{Worn(levy)}]");
			}
			{ // a levy in wings is not handed a jump accessory first: his accessory slots are for everything else
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.AngelWings)); te.Deposit(Make(ItemID.CloudinaBottle)); te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.BandofRegeneration));
				var levy = new SoldierRecord { Id = 64, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"levy with wings, a bottle stocked: [{Worn(levy)}]");
				check("a levy in wings spends his accessory slots on non-mobility accessories", levy.Gear.HasWings && levy.Gear.Accessories.Take(levy.AccessorySlots).All(a => a.IsAir || !AccessoryMobility.Is(a)), Worn(levy));
			}
			{ // a levy without wings still gets a jump accessory first (the older rule, for levies who have no wings)
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.CloudinaBottle)); te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.BandofRegeneration));
				var levy = new SoldierRecord { Id = 65, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				check("with no wings in stock a levy still takes a bottle for height", levy.Gear.Accessories.Take(levy.AccessorySlots).Any(a => a.type == ItemID.CloudinaBottle), Worn(levy));
			}
		}

		// ------------------------------------------------------------------ preferred / non-preferred
		private static void Preferred(CheckFn check, Action<string> info)
		{
			var te = new ArmoryTE(); te.Deposit(Make(ItemID.AngelWings)); te.Deposit(Make(ItemID.CreativeWings)); te.Deposit(Make(ItemID.SpectreBoots)); te.Deposit(Make(ItemID.HermesBoots));
			te.Rules.OverrideFor(ItemID.CreativeWings, true).Rank = RankOverrideMode.Prefer;
			te.Rules.OverrideFor(ItemID.HermesBoots, true).Rank = RankOverrideMode.Prefer; te.Rules.Changed();
			var levy = new SoldierRecord { Id = 66, Grade = Grade.Levy };
			ArmoryTE.TestServed = new List<SoldierRecord> { levy };
			te.IssueGear(levy);
			info($"Fledgling Wings and Hermes Boots preferred: wings [{N(levy.Gear.Wings)}] boots [{N(levy.Gear.Boots)}]");
			check("a preferred pair of wings and of boots are dealt before the better ones", levy.Gear.Wings.type == ItemID.CreativeWings && levy.Gear.Boots.type == ItemID.HermesBoots);
			te.Deposit(Make(ItemID.LightningBoots)); te.Deposit(Make(ItemID.WingsSolar));
			ArmoryTE.TestServed = new List<SoldierRecord> { levy };
			te.UpgradeAccessories(levy);
			check("a preferred pair is not swapped for an ordinary one on the gear check", levy.Gear.Wings.type == ItemID.CreativeWings && levy.Gear.Boots.type == ItemID.HermesBoots, $"{N(levy.Gear.Wings)} / {N(levy.Gear.Boots)}");
			te.Rules.OverrideFor(ItemID.CreativeWings, true).Rank = RankOverrideMode.Auto; te.Rules.OverrideFor(ItemID.HermesBoots, true).Rank = RankOverrideMode.Auto; te.Rules.Changed();
			ArmoryTE.TestServed = new List<SoldierRecord> { levy };
			te.UpgradeAccessories(levy);
			check("without the preference the better pair wins on the next gear check", levy.Gear.Wings.type != ItemID.CreativeWings && levy.Gear.Boots.type != ItemID.HermesBoots, $"{N(levy.Gear.Wings)} / {N(levy.Gear.Boots)}");
			check("the pair taken out goes back to the shelf", te.Storage.Any(i => !i.IsAir && i.type == ItemID.CreativeWings) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.HermesBoots));
		}

		// ------------------------------------------------------------------ the gear check upgrades the slots and clears strays
		private static void Upgrades(CheckFn check, Action<string> info)
		{
			{
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.AngelWings)); te.Deposit(Make(ItemID.LightningBoots)); te.Deposit(Make(ItemID.ObsidianShield));
				var levy = new SoldierRecord { Id = 67, Grade = Grade.Levy };
				levy.Gear.Wings = Make(ItemID.CreativeWings); levy.Gear.Boots = Make(ItemID.HermesBoots);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				info($"gear check, Fledgling Wings + Hermes Boots worn, Angel Wings + Lightning Boots stocked: wings [{N(levy.Gear.Wings)}] boots [{N(levy.Gear.Boots)}]");
				check("a better pair of wings and of boots replace the worn ones on the gear check", levy.Gear.Wings.type == ItemID.AngelWings && levy.Gear.Boots.type == ItemID.LightningBoots);
				check("the old pairs are back on the shelf", te.Storage.Any(i => !i.IsAir && i.type == ItemID.CreativeWings) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.HermesBoots));
			}
			{ // a kit from before the slots: wings and boots in accessory slots (unlocked) move out and free the slots
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.CharmofMyths));
				var levy = new SoldierRecord { Id = 68, Grade = Grade.Champion };
				levy.Gear.Accessories[0] = Make(ItemID.AngelWings); levy.Gear.Accessories[1] = Make(ItemID.SpectreBoots); levy.Gear.Accessories[2] = Make(ItemID.BandofRegeneration);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				info($"gear check on a kit with wings and boots in accessory slots: wings [{N(levy.Gear.Wings)}] boots [{N(levy.Gear.Boots)}] accessories [{Worn(levy)}]");
				check("wings and boots in accessory slots move to their own slots", levy.Gear.Wings.type == ItemID.AngelWings && levy.Gear.Boots.type == ItemID.SpectreBoots && NoMobilityWorn(levy));
				check("the freed accessory slot is filled with an ordinary accessory", levy.Gear.Accessories.Take(levy.AccessorySlots).Any(a => a.type == ItemID.ObsidianShield), Worn(levy));
			}
			{ // a hand-locked slot is never touched
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.AngelWings)); te.Deposit(Make(ItemID.SpectreBoots));
				var levy = new SoldierRecord { Id = 69, Grade = Grade.Levy };
				levy.Gear.Wings = Make(ItemID.CreativeWings); levy.Gear.SetLocked(SoldierGear.SlotWings, true);
				levy.Gear.Boots = Make(ItemID.HermesBoots); levy.Gear.SetLocked(SoldierGear.SlotBoots, true);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy); te.IssueGear(levy);
				check("hand-assigned wings and boots are never swapped", levy.Gear.Wings.type == ItemID.CreativeWings && levy.Gear.Boots.type == ItemID.HermesBoots);
			}
			{ // returning a kit gives them back, and Re-arm keeps a hand-assigned pair
				var te = new ArmoryTE();
				var g = new SoldierGear(); g.Wings = Make(ItemID.AngelWings); g.Boots = Make(ItemID.SpectreBoots); g.SetLocked(SoldierGear.SlotWings, true);
				te.ReturnGear(g);
				check("returning a kit sends wings and boots back to the Armory", !g.HasWings && !g.HasBoots && te.Storage.Any(i => !i.IsAir && i.type == ItemID.AngelWings) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.SpectreBoots));
				var rec = new SoldierRecord { Id = 70, Grade = Grade.Levy, Gear = g };
				ArmoryTE.TestServed = new List<SoldierRecord> { rec };
				te.IssueGear(rec);
				check("a Re-arm hands the hand-assigned wings back", rec.Gear.Wings.type == ItemID.AngelWings && rec.Gear.IsLocked(SoldierGear.SlotWings) && rec.Gear.Boots.type == ItemID.SpectreBoots, $"{N(rec.Gear.Wings)} / {N(rec.Gear.Boots)}");
			}
			{ // the armory's in-use list shows them
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.AngelWings)); te.Deposit(Make(ItemID.SpectreBoots));
				var levy = new SoldierRecord { Id = 71, Grade = Grade.Levy, Name = "Pvt. Test" };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				var use = te.InUse(out _);
				check("the in-use cells show the wings and boots a levy carries", use.Any(e => e.Item.type == ItemID.AngelWings) && use.Any(e => e.Item.type == ItemID.SpectreBoots), use.Count.ToString());
			}
		}

		// ------------------------------------------------------------------ an old save
		private static void Migration(CheckFn check, Action<string> info)
		{
			// a kit written before the slots: wings and boots among the accessories, no wings / boots keys, the older slot count
			var old = new SoldierGear();
			old.Weapon = Make(ItemID.Musket);
			old.Accessories[0] = Make(ItemID.ObsidianShield); old.Accessories[1] = Make(ItemID.AngelWings); old.Accessories[2] = Make(ItemID.SpectreBoots); old.Accessories[3] = Make(ItemID.BandofRegeneration); old.Accessories[4] = Make(ItemID.HermesBoots); old.Accessories[5] = Make(ItemID.MasterNinjaGear);
			old.SetLocked(SoldierGear.SlotAcc0 + 1, true); old.SetLocked(SoldierGear.SlotAcc0 + 2, true);
			var tag = old.Save(); tag.Remove("wings"); tag.Remove("boots"); tag["slotCount"] = 16;
			var ld = SoldierGear.Load(tag);
			info($"old save: wings [{N(ld.Wings)}] boots [{N(ld.Boots)}] accessories [{string.Join(", ", ld.Accessories.Select(N))}] locks 0x{ld.LockMask:X}");
			check("an old save's wings move to the wings slot", ld.Wings.type == ItemID.AngelWings && ld.Accessories[1].IsAir);
			check("its best boots move to the boots slot", ld.Boots.type == ItemID.SpectreBoots && ld.Accessories[2].IsAir);
			check("their hand-assigned locks move with them", ld.IsLocked(SoldierGear.SlotWings) && ld.IsLocked(SoldierGear.SlotBoots) && !ld.IsLocked(SoldierGear.SlotAcc0 + 1) && !ld.IsLocked(SoldierGear.SlotAcc0 + 2));
			check("the other accessories stay where they were", ld.Accessories[0].type == ItemID.ObsidianShield && ld.Accessories[3].type == ItemID.BandofRegeneration && ld.Weapon.type == ItemID.Musket);
			check("an old save's utility shoes (Master Ninja Gear) stay in their accessory slot", ld.Accessories[5].type == ItemID.MasterNinjaGear && ld.Boots.type != ItemID.MasterNinjaGear);
			check("a second pair of boots stays put (the armory returns it on the gear check)", ld.Accessories[4].type == ItemID.HermesBoots);
			var te = new ArmoryTE(); var rec = new SoldierRecord { Id = 72, Grade = Grade.Champion, Gear = ld };
			ArmoryTE.TestServed = new List<SoldierRecord> { rec };
			te.UpgradeAccessories(rec);
			check("the gear check files the spare pair of boots on the shelf, losing nothing", NoMobilityWorn(rec) && te.Storage.Any(i => !i.IsAir && i.type == ItemID.HermesBoots), Worn(rec));
			// a save with no wings or boots at all loads as before
			var plain = new SoldierGear(); plain.Accessories[0] = Make(ItemID.BandofRegeneration);
			var pl = SoldierGear.Load(plain.Save());
			check("an old save with no wings or boots loads unchanged", !pl.HasWings && !pl.HasBoots && pl.Accessories[0].type == ItemID.BandofRegeneration);
		}

		// ------------------------------------------------------------------ the horizontal slot takes a dash
		private static void Dashes(CheckFn check, Action<string> info)
		{
			var dashItems = new List<Item>();
			for (int t = 1; t < ItemLoader.ItemCount; t++) if (ContentSamples.ItemsByType.TryGetValue(t, out Item s) && s != null && !s.IsAir && MobilitySlots.IsDash(s)) dashItems.Add(s);
			info($"DASH items classified ({dashItems.Count}): " + string.Join(", ", dashItems.Select(i => (i.ModItem != null ? i.ModItem.Mod.Name + ":" : "") + i.Name)));
			check("Tabi, Master Ninja Gear and the Shield of Cthulhu are dash accessories", new[] { ItemID.Tabi, ItemID.MasterNinjaGear, ItemID.EoCShield }.All(t => MobilitySlots.IsDash(Make(t))), string.Join(",", new[] { ItemID.Tabi, ItemID.MasterNinjaGear, ItemID.EoCShield }.Select(t => MobilitySlots.IsDash(Make(t)))));
			check("boots, wings, balloons, bottles, a shield and the climbing shoes are not dash accessories", new[] { ItemID.HermesBoots, ItemID.AngelWings, ItemID.ShinyRedBalloon, ItemID.CloudinaBottle, ItemID.ObsidianShield, ItemID.ShoeSpikes, ItemID.FrogLeg, ItemID.BandofRegeneration }.All(t => !MobilitySlots.IsDash(Make(t))));
			if (ModLoader.HasMod("CalamityMod"))
			{
				var names = new[] { "AsgardianAegis", "ElysianAegis", "StatisNinjaBelt", "StatisVoidSash", "EvasionScarf", "CounterScarf", "OrnateShield", "DeepDiver", "V8Engine", "V8000Engine", "AsgardsValor" };
				var found = names.Select(n => (n, it: FindModded("CalamityMod", n))).Where(x => x.it != null).ToList();
				var notDash = found.Where(x => !MobilitySlots.IsDash(x.it)).Select(x => x.n).ToList();
				info($"Calamity dash accessories: {found.Count} of {names.Length} found, not read as a dash: [{string.Join(", ", notDash)}]");
				check("every Calamity dash accessory is read as a dash (its CalamityPlayer.DashID, not dashType)", found.Count >= 8 && notDash.Count == 0, string.Join(",", notDash));
				var treads = FindModded("CalamityMod", "AngelTreads");
				if (treads != null) check("Angel Treads are boots and not a dash", MobilitySlots.IsBoots(treads) && !MobilitySlots.IsDash(treads));
			}

			var g = new SoldierGear();
			check("the horizontal slot takes a dash and runner boots; the wings slot refuses a dash; an ordinary slot still takes one", g.CanHold(SoldierGear.SlotBoots, Make(ItemID.Tabi)) && g.CanHold(SoldierGear.SlotBoots, Make(ItemID.HermesBoots)) && !g.CanHold(SoldierGear.SlotWings, Make(ItemID.Tabi)) && g.CanHold(SoldierGear.SlotAcc0, Make(ItemID.Tabi)) && !g.CanHold(SoldierGear.SlotBoots, Make(ItemID.ShinyRedBalloon)) && !g.CanHold(SoldierGear.SlotBoots, Make(ItemID.CloudinaBottle)));
			float H(int t, Item wings = null) => AccessoryMobility.HorizontalValue(Make(t), wings);
			info($"horizontal values (no wings): Hermes {H(ItemID.HermesBoots):0.00}, Rocket {H(ItemID.RocketBoots):0.00}, Spectre {H(ItemID.SpectreBoots):0.00}, Lightning {H(ItemID.LightningBoots):0.00}, Terraspark {H(ItemID.TerrasparkBoots):0.00}, Tabi {H(ItemID.Tabi):0.00}, Master Ninja {H(ItemID.MasterNinjaGear):0.00}, Shield of Cthulhu {H(ItemID.EoCShield):0.00}, Shoe Spikes {H(ItemID.ShoeSpikes):0.00}, Frog Leg {H(ItemID.FrogLeg):0.00}");
			check("a dash adds more horizontal mobility than Hermes Boots; Spectre, Lightning and Terraspark Boots add more than a dash; Shoe Spikes and a Frog Leg add none", H(ItemID.Tabi) > H(ItemID.HermesBoots) && H(ItemID.SpectreBoots) > H(ItemID.Tabi) && H(ItemID.LightningBoots) > H(ItemID.Tabi) && H(ItemID.TerrasparkBoots) > H(ItemID.Tabi) && H(ItemID.ShoeSpikes) == 0f && H(ItemID.FrogLeg) == 0f);

			{ // Hermes Boots and a Tabi stocked: the dash gets the slot
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.HermesBoots)); te.Deposit(Make(ItemID.Tabi));
				var levy = new SoldierRecord { Id = 81, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"Hermes Boots + Tabi stocked: horizontal [{N(levy.Gear.Boots)}] accessories [{Worn(levy)}]");
				check("with Hermes Boots and a Tabi stocked the dash takes the horizontal slot", levy.Gear.Boots.type == ItemID.Tabi, N(levy.Gear.Boots));
			}
			{ // Lightning Boots outrun a dash
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.LightningBoots)); te.Deposit(Make(ItemID.Tabi));
				var levy = new SoldierRecord { Id = 82, Grade = Grade.Champion };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				check("Lightning Boots keep the horizontal slot against a Tabi, which is worn as an ordinary accessory", levy.Gear.Boots.type == ItemID.LightningBoots && levy.Gear.Accessories.Any(a => a.type == ItemID.Tabi), $"{N(levy.Gear.Boots)} / {Worn(levy)}");
			}
			{ // a dash in the slot is replaced by boots that outrun it on the gear check, and the dash goes back to the shelf
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.TerrasparkBoots));
				var levy = new SoldierRecord { Id = 83, Grade = Grade.Levy };
				levy.Gear.Boots = Make(ItemID.Tabi);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				check("Terraspark Boots replace a Tabi in the horizontal slot on the gear check", levy.Gear.Boots.type == ItemID.TerrasparkBoots, N(levy.Gear.Boots));
				check("the Tabi taken out is on the shelf or in an ordinary slot", te.Storage.Any(i => !i.IsAir && i.type == ItemID.Tabi) || levy.Gear.Accessories.Any(a => a.type == ItemID.Tabi));
			}
			{ // a hand-placed dash is never swapped
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.TerrasparkBoots));
				var levy = new SoldierRecord { Id = 84, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				levy.Gear.TrySet(SoldierGear.SlotBoots, Make(ItemID.Tabi), levy.AccessorySlots, out _);
				te.UpgradeAccessories(levy); te.IssueGear(levy);
				check("a hand-placed Tabi stays in the horizontal slot", levy.Gear.Boots.type == ItemID.Tabi && levy.Gear.IsLocked(SoldierGear.SlotBoots), N(levy.Gear.Boots));
			}
			{ // balloons and bottles still go to the ordinary slots and still count as a double jump
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.ShinyRedBalloon)); te.Deposit(Make(ItemID.CloudinaBottle)); te.Deposit(Make(ItemID.Tabi)); te.Deposit(Make(ItemID.HermesBoots)); te.Deposit(Make(ItemID.ObsidianShield));
				var levy = new SoldierRecord { Id = 85, Grade = Grade.Champion };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"balloon, bottle, Tabi, Hermes, shield stocked (Champion): horizontal [{N(levy.Gear.Boots)}] accessories [{Worn(levy)}]");
				check("a balloon and a bottle are worn in ordinary accessory slots beside a dash in the horizontal slot", levy.Gear.Boots.type == ItemID.Tabi && levy.Gear.Accessories.Any(a => a.type == ItemID.ShinyRedBalloon) && levy.Gear.Accessories.Any(a => a.type == ItemID.CloudinaBottle) && NoMobilityWorn(levy), $"{N(levy.Gear.Boots)} / {Worn(levy)}");
				check("neither the horizontal slot nor the wings slot ever takes a balloon or a bottle", !MobilitySlots.IsHorizontal(Make(ItemID.ShinyRedBalloon)) && !MobilitySlots.IsHorizontal(Make(ItemID.CloudinaBottle)) && !MobilitySlots.IsWings(Make(ItemID.ShinyRedBalloon)) && AccessoryMobility.Is(Make(ItemID.ShinyRedBalloon)) && AccessoryMobility.Is(Make(ItemID.CloudinaBottle)));
				var d = new Player(); d.ResetEffects(); for (int i = 0; i < d.armor.Length; i++) d.armor[i] = new Item(); d.armor[3] = Make(ItemID.CloudinaBottle); d.armor[4] = Make(ItemID.ShinyRedBalloon);
				global::Levy.Common.Util.DummyEquip.SafeUpdateEquips(d, 250);
				var ab = MovementAbilities.FromDummy(d);
				check($"a dummy in a bottle and a balloon has the double jump and the jump height ({ab.ExtraJumps} extra jumps, {ab.JumpTiles} tiles)", ab.ExtraJumps >= 1 && ab.JumpTiles > LevyConstants.Movement.BaseJumpTiles);
			}
			{ // old save: a dash worn as an ordinary accessory, no runner boots
				var old = new SoldierGear(); old.Weapon = Make(ItemID.Musket);
				old.Accessories[0] = Make(ItemID.ObsidianShield); old.Accessories[1] = Make(ItemID.Tabi); old.Accessories[2] = Make(ItemID.ShinyRedBalloon);
				var tag = old.Save(); tag.Remove("wings"); tag.Remove("boots"); tag["slotCount"] = 16;
				var ld = SoldierGear.Load(tag);
				check("an old save's Tabi (an ordinary accessory) moves to the empty horizontal slot, freeing the slot", ld.Boots.type == ItemID.Tabi && ld.Accessories[1].IsAir && ld.Accessories[0].type == ItemID.ObsidianShield && ld.Accessories[2].type == ItemID.ShinyRedBalloon, $"{N(ld.Boots)} / {string.Join(", ", ld.Accessories.Take(4).Select(N))}");
				var old2 = new SoldierGear(); old2.Accessories[0] = Make(ItemID.SpectreBoots); old2.Accessories[1] = Make(ItemID.Tabi);
				var tag2 = old2.Save(); tag2.Remove("wings"); tag2.Remove("boots"); tag2["slotCount"] = 16;
				var ld2 = SoldierGear.Load(tag2);
				check("an old save with Spectre Boots and a Tabi keeps the boots in the slot and the Tabi in its accessory slot", ld2.Boots.type == ItemID.SpectreBoots && ld2.Accessories[1].type == ItemID.Tabi, $"{N(ld2.Boots)} / {N(ld2.Accessories[1])}");
				var te = new ArmoryTE(); var rec = new SoldierRecord { Id = 86, Grade = Grade.Champion, Gear = ld };
				ArmoryTE.TestServed = new List<SoldierRecord> { rec };
				te.UpgradeAccessories(rec);
				check("the gear check on that kit changes nothing it should not", rec.Gear.Boots.type == ItemID.Tabi && rec.Gear.Accessories.Any(a => a.type == ItemID.ShinyRedBalloon), $"{N(rec.Gear.Boots)} / {Worn(rec)}");
			}
			{ // wings that already run (Seraph Tracers): the horizontal slot prefers a dash to boots that add nothing
				var seraph = FindModded("CalamityMod", "SeraphTracers");
				if (seraph == null) info("Seraph Tracers: Calamity not loaded, skipped");
				else
				{
					info($"Seraph Tracers: wings={MobilitySlots.IsWings(seraph)}, run value {AccessoryMobility.RunValue(seraph):0.00}; horizontal value of Hermes beside them {AccessoryMobility.HorizontalValue(Make(ItemID.HermesBoots), seraph):0.00}, of Tabi {AccessoryMobility.HorizontalValue(Make(ItemID.Tabi), seraph):0.00}");
					check("Seraph Tracers are wings, they run, and Hermes Boots beside them add no horizontal mobility", MobilitySlots.IsWings(seraph) && AccessoryMobility.RunValue(seraph) > 0.5f && AccessoryMobility.HorizontalValue(Make(ItemID.HermesBoots), seraph) <= 0.01f);
					var te = new ArmoryTE(); te.Deposit(Make(ItemID.HermesBoots)); te.Deposit(Make(ItemID.Tabi));
					var levy = new SoldierRecord { Id = 87, Grade = Grade.Levy };
					levy.Gear.Wings = Make(seraph.type);
					ArmoryTE.TestServed = new List<SoldierRecord> { levy };
					te.IssueGear(levy);
					check("a levy in Seraph Tracers is given the Tabi, not the Hermes Boots", levy.Gear.Boots.type == ItemID.Tabi, N(levy.Gear.Boots));
					var te2 = new ArmoryTE(); te2.Deposit(Make(ItemID.Tabi));
					var levy2 = new SoldierRecord { Id = 88, Grade = Grade.Levy };
					levy2.Gear.Wings = Make(seraph.type); levy2.Gear.Boots = Make(ItemID.HermesBoots);
					ArmoryTE.TestServed = new List<SoldierRecord> { levy2 };
					te2.UpgradeAccessories(levy2);
					check("a levy in Seraph Tracers and Hermes Boots swaps the boots for a stocked Tabi on the gear check", levy2.Gear.Boots.type == ItemID.Tabi, N(levy2.Gear.Boots));
				}
			}
		}

		// ------------------------------------------------------------------ save, copy, network
		private static void RoundTrips(CheckFn check)
		{
			var g = new SoldierGear();
			g.Wings = Make(ItemID.AngelWings); g.Boots = Make(ItemID.SpectreBoots); g.Boots.Prefix(PrefixID.Warding); g.SetLocked(SoldierGear.SlotBoots, true);
			var back = SoldierGear.Load(g.Save());
			check("wings, boots and the boots' lock survive a world save", back.Wings.type == ItemID.AngelWings && back.Boots.type == ItemID.SpectreBoots && back.IsLocked(SoldierGear.SlotBoots) && !back.IsLocked(SoldierGear.SlotWings));
			var clone = g.Clone();
			check("a copy keeps them", clone.Wings.type == ItemID.AngelWings && clone.Boots.type == ItemID.SpectreBoots && clone.IsLocked(SoldierGear.SlotBoots));
			using var ms = new System.IO.MemoryStream();
			using (var w = new System.IO.BinaryWriter(ms)) g.WriteVisual(w);
			var vis = new SoldierGear();
			using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(ms.ToArray()))) vis.ReadVisual(r);
			check("wings, boots and every lock bit reach clients", vis.Wings.type == ItemID.AngelWings && vis.Boots.type == ItemID.SpectreBoots && vis.Boots.prefix == PrefixID.Warding && vis.IsLocked(SoldierGear.SlotBoots));
			var ids = new HashSet<int> { SoldierGear.SlotWings, SoldierGear.SlotBoots, SoldierGear.SlotCamera, SoldierGear.SlotSide2, SoldierGear.SlotHook, SoldierGear.SlotAmmo };
			check("the new slot numbers are distinct from every other slot", ids.Count == 6 && SoldierGear.SlotWings == 16 && SoldierGear.SlotBoots == 17 && SoldierGear.SlotCount == 18, $"{SoldierGear.SlotWings}/{SoldierGear.SlotBoots}/{SoldierGear.SlotCount}");
			check("Get and Set reach the slots by number", g.Get(SoldierGear.SlotWings).type == ItemID.AngelWings && g.Get(SoldierGear.SlotBoots).type == ItemID.SpectreBoots);
		}
	}
}
