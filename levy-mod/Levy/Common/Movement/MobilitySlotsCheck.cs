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
			check("a shield, a bottle and a band are neither", new[] { ItemID.ObsidianShield, ItemID.CloudinaBottle, ItemID.BandofRegeneration, ItemID.ShinyRedBalloon }.All(t => !MobilitySlots.Is(Make(t))));
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
			old.Accessories[0] = Make(ItemID.ObsidianShield); old.Accessories[1] = Make(ItemID.AngelWings); old.Accessories[2] = Make(ItemID.SpectreBoots); old.Accessories[3] = Make(ItemID.BandofRegeneration); old.Accessories[4] = Make(ItemID.HermesBoots);
			old.SetLocked(SoldierGear.SlotAcc0 + 1, true); old.SetLocked(SoldierGear.SlotAcc0 + 2, true);
			var tag = old.Save(); tag.Remove("wings"); tag.Remove("boots"); tag["slotCount"] = 16;
			var ld = SoldierGear.Load(tag);
			info($"old save: wings [{N(ld.Wings)}] boots [{N(ld.Boots)}] accessories [{string.Join(", ", ld.Accessories.Select(N))}] locks 0x{ld.LockMask:X}");
			check("an old save's wings move to the wings slot", ld.Wings.type == ItemID.AngelWings && ld.Accessories[1].IsAir);
			check("its best boots move to the boots slot", ld.Boots.type == ItemID.SpectreBoots && ld.Accessories[2].IsAir);
			check("their hand-assigned locks move with them", ld.IsLocked(SoldierGear.SlotWings) && ld.IsLocked(SoldierGear.SlotBoots) && !ld.IsLocked(SoldierGear.SlotAcc0 + 1) && !ld.IsLocked(SoldierGear.SlotAcc0 + 2));
			check("the other accessories stay where they were", ld.Accessories[0].type == ItemID.ObsidianShield && ld.Accessories[3].type == ItemID.BandofRegeneration && ld.Weapon.type == ItemID.Musket);
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
