using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Movement;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// Boot-time self-check of the loadout rules (dedicated server only; throwaway armories and records, no world state): every levy has two accessory slots, one for mobility and one for the rest; a levy carries up to
	/// four weapons; a staff with a limit of its own uses only its real slot; a melee levy's secondary is a summon weapon and never a second sword; the armor sets and accessories the player prefers are dealt first.
	/// Logs one line per scenario with the numbers ("[Loadout] ...") and a final "[Loadout] self-check DONE pass=N fail=M".
	/// </summary>
	public sealed class LoadoutCheck : ModSystem
	{
		private delegate void CheckFn(string name, bool ok, string detail = "");
		private static Item Make(int type, int stack = 1) { var i = new Item(); i.SetDefaults(type); i.stack = stack; return i; }
		private static string N(Item it) => it == null || it.IsAir ? "-" : it.Name;

		public override void PostSetupRecipes()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[Loadout] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			void Info(string line) => Mod.Logger.Info("[Loadout] " + line);
			var savedServed = ArmoryTE.TestServed; bool savedReserve = ArmoryTE.ReserveSlots;
			try
			{
				WeaponRank.Force(WeaponRankMode.Damage, false);
				ArmorDoctrine.Reset();
				SlotsPerRank(Check, Info);
				ReservedSlots(Check, Info);
				Sidearms(Check, Info);
				CappedSummons(Check, Info);
				MeleeSecondary(Check, Info);
				SummonSlots(Check, Info);
				PreferredGear(Check, Info);
				RoundTrips(Check);
			}
			catch (Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
			finally { ArmoryTE.TestServed = savedServed; ArmoryTE.ReserveSlots = savedReserve; WeaponRank.Release(); SummonCap.Reset(); }
			Mod.Logger.Info($"[Loadout] self-check DONE pass={pass} fail={fail}");
		}

		private static bool Mob(Item it) => !it.IsAir && AccessoryMobility.Is(it);

		// ------------------------------------------------------------------ A. slots per rank
		private static void SlotsPerRank(CheckFn check, Action<string> info)
		{
			var names = new[] { "Levy", "ManAtArms", "Champion" };
			var before = LevyConstants.GradeSlots;
			var after = new int[3];
			for (int g = 0; g < 3; g++) after[g] = SoldierRecord.AccessorySlotsFor((Grade)g);
			info($"accessory slots per rank: before {string.Join("/", before)} after {string.Join("/", after)} ({string.Join("/", names)}); every rank at least {LevyConstants.MinAccessorySlots}");
			check("every grade has at least two accessory slots", after.All(s => s >= 2), string.Join("/", after));
			check("higher ranks keep their extra slots (3 and 5 stay)", after[1] >= 3 && after[2] >= 5, string.Join("/", after));
		}

		// ------------------------------------------------------------------ A. one mobility slot, one non-mobility slot
		private static void ReservedSlots(CheckFn check, Action<string> info)
		{
			Item[] Stock() => new[] { Make(ItemID.FartinaJar), Make(ItemID.SandstorminaBottle), Make(ItemID.CloudinaBottle), Make(ItemID.ObsidianShield), Make(ItemID.BandofRegeneration) };
			string Worn(SoldierRecord r) => string.Join(", ", r.Gear.Accessories.Take(r.AccessorySlots).Select(N));
			int MobN(SoldierRecord r) => r.Gear.Accessories.Take(r.AccessorySlots).Count(Mob);
			int OtherN(SoldierRecord r) => r.Gear.Accessories.Take(r.AccessorySlots).Count(a => !a.IsAir && !Mob(a));

			foreach (bool reserve in new[] { false, true })
			{
				ArmoryTE.ReserveSlots = reserve;
				var te = new ArmoryTE(); foreach (var it in new[] { Make(ItemID.FartinaJar), Make(ItemID.SandstorminaBottle), Make(ItemID.CloudinaBottle), Make(ItemID.BandofRegeneration) }) te.Deposit(it);
				var levy = new SoldierRecord { Id = 41, Grade = Grade.Levy };
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.IssueGear(levy);
				info($"levy accessories, stock = 3 mobility bottles + Band of Regeneration, slot reservation {(reserve ? "on" : "off")}: [{Worn(levy)}] mobility={MobN(levy)} other={OtherN(levy)}");
				if (reserve)
				{
					check("a two-slot levy wears one mobility and one non-mobility accessory", MobN(levy) == 1 && OtherN(levy) == 1, Worn(levy));
					check("the mobility slot holds the best bottle", levy.Gear.Accessories.Any(a => a.type == ItemID.FartinaJar), Worn(levy));
				}
			}
			ArmoryTE.ReserveSlots = true;
			{ // a kit of two mobility accessories gives one up when a non-mobility accessory is stocked
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.ObsidianShield));
				var levy = new SoldierRecord { Id = 42, Grade = Grade.Levy };
				levy.Gear.Accessories[0] = Make(ItemID.CloudinaBottle); levy.Gear.Accessories[1] = Make(ItemID.SandstorminaBottle);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				info($"levy in two bottles, Obsidian Shield stocked: [{Worn(levy)}]");
				check("two mobility accessories make room for a non-mobility one, keeping the better bottle", MobN(levy) == 1 && OtherN(levy) == 1 && levy.Gear.Accessories.Any(a => a.type == ItemID.SandstorminaBottle), Worn(levy));
			}
			{ // a kit with no mobility takes one when it arrives and keeps a non-mobility accessory
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.SandstorminaBottle));
				var levy = new SoldierRecord { Id = 43, Grade = Grade.Levy };
				levy.Gear.Accessories[0] = Make(ItemID.ObsidianShield); levy.Gear.Accessories[1] = Make(ItemID.BandofRegeneration);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				info($"levy in Obsidian Shield + Band of Regeneration, bottle stocked: [{Worn(levy)}]");
				check("a levy with no mobility is given one and keeps a non-mobility accessory", MobN(levy) == 1 && OtherN(levy) == 1, Worn(levy));
			}
			{ // a second mobility accessory does not take the non-mobility slot of a two-slot levy
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.FartinaJar));
				var levy = new SoldierRecord { Id = 44, Grade = Grade.Levy };
				levy.Gear.Accessories[0] = Make(ItemID.CloudinaBottle); levy.Gear.Accessories[1] = Make(ItemID.ObsidianShield);
				ArmoryTE.TestServed = new List<SoldierRecord> { levy };
				te.UpgradeAccessories(levy);
				info($"levy in Cloud + Obsidian Shield, better bottle stocked: [{Worn(levy)}]");
				check("a better bottle replaces the bottle, never the non-mobility slot", MobN(levy) == 1 && OtherN(levy) == 1 && levy.Gear.Accessories.Any(a => a.type == ItemID.FartinaJar), Worn(levy));
			}
			{ // a champion keeps all five slots
				var te = new ArmoryTE(); foreach (var it in Stock()) te.Deposit(it); te.Deposit(Make(ItemID.HermesBoots));
				var champ = new SoldierRecord { Id = 45, Grade = Grade.Champion };
				ArmoryTE.TestServed = new List<SoldierRecord> { champ };
				te.IssueGear(champ);
				int filled = champ.Gear.Accessories.Take(champ.AccessorySlots).Count(a => !a.IsAir);
				info($"champion ({champ.AccessorySlots} slots): [{Worn(champ)}]");
				check("a champion fills every slot and keeps a non-mobility accessory", filled == Math.Min(champ.AccessorySlots, 6) && OtherN(champ) >= 1 && MobN(champ) >= 1, Worn(champ));
			}
		}

		// ------------------------------------------------------------------ B. how many weapons
		private static ArmoryTE Armory(params Item[] stock)
		{
			var te = new ArmoryTE();
			for (int i = 0; i < stock.Length; i++) te.Storage[i] = stock[i];
			return te;
		}

		private static void Armor(SoldierRecord r, int head, int body, int legs) { r.Gear.Head = Make(head); r.Gear.Body = Make(body); r.Gear.Legs = Make(legs); }
		private static string Kit(SoldierRecord r) => $"primary {N(r.Gear.Weapon)}; sidearms {string.Join(" | ", r.Gear.Sidearms().Select(N))}";
		private static int WeaponsCarried(SoldierRecord r) => (r.Gear.HasWeapon ? 1 : 0) + r.Gear.SidearmCount;

		private static void Sidearms(CheckFn check, Action<string> info)
		{
			info($"weapons a levy carries: before 2 (primary + 1 sidearm), after {1 + LevyConstants.MaxSidearms} (primary + {LevyConstants.MaxSidearms} sidearms)");
			var te = Armory(Make(ItemID.Musket), Make(ItemID.MusketBall, 300), Make(ItemID.IronBroadsword), Make(ItemID.SlimeStaff));
			var gun = new SoldierRecord { Id = 51 };
			Armor(gun, ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings);
			ArmoryTE.TestServed = new List<SoldierRecord> { gun };
			te.IssueGear(gun);
			info("ranged levy: " + Kit(gun));
			check("a ranged levy carries a gun, a lesser blade and a spare minion staff (3 weapons)", gun.Gear.Weapon.type == ItemID.Musket && gun.Gear.Sidearm.type == ItemID.IronBroadsword && gun.Gear.ExtraSidearms[0].type == ItemID.SlimeStaff && WeaponsCarried(gun) == 3, Kit(gun));
			check("the extra sidearm slots only take minion staves", !gun.Gear.CanHold(SoldierGear.SlotSide2, Make(ItemID.IronBroadsword)) && gun.Gear.CanHold(SoldierGear.SlotSide2, Make(ItemID.SlimeStaff)));
		}

		// ------------------------------------------------------------------ C. staves with a limit of their own
		private static void CappedSummons(CheckFn check, Action<string> info)
		{
			// detection from the item's real behaviour (Calamity's Deathstare Rod refuses to be used while its minion exists)
			if (ModLoader.TryGetMod("CalamityMod", out var cal) && cal.TryFind<ModItem>("DeathstareRod", out var rod))
			{
				SummonCap.Reset();
				var it = Make(rod.Type);
				info($"Deathstare Rod: slots per minion {Content.NPCs.SoldierNPC.SlotsPerMinion(it)}, most minions {SummonCap.MaxCasts(it)}");
				check("Calamity's Deathstare Rod is read as one minion at a time", SummonCap.MaxCasts(it) == 1, SummonCap.MaxCasts(it).ToString());
				var capped = new List<string>(); int total = 0;
				foreach (var mi in cal.GetContent<ModItem>())
				{
					var s = Make(mi.Type);
					if (s.damage <= 0 || s.shoot <= 0 || WeaponClassifier.GetProfile(s).Family != WeaponFamily.Summon) continue;
					total++;
					if (SummonCap.IsCapped(s)) capped.Add(mi.Name + "=" + SummonCap.MaxCasts(s));
				}
				info($"Calamity minion staves with a limit of their own: {capped.Count} of {total}: {string.Join(", ", capped.Take(14))}{(capped.Count > 14 ? ", ..." : "")}");
				check("a vanilla staff is unlimited", !SummonCap.IsCapped(Make(ItemID.HornetStaff)));
				check("Calamity has staves read as limited and staves read as unlimited", capped.Count > 0 && capped.Count < total, $"{capped.Count}/{total}");
			}
			else
			{
				SummonCap.Reset();
				check("vanilla staves are unlimited", !SummonCap.IsCapped(Make(ItemID.HornetStaff)) && !SummonCap.IsCapped(Make(ItemID.SlimeStaff)));
			}

			// a summoner holding several limited staves (limits forced on vanilla staves so the check runs everywhere)
			SummonCap.Force(ItemID.PygmyStaff, 1); SummonCap.Force(ItemID.SpiderStaff, 1); SummonCap.Force(ItemID.OpticStaff, 1);
			var te = Armory(Make(ItemID.ImpStaff), Make(ItemID.PygmyStaff), Make(ItemID.SpiderStaff), Make(ItemID.OpticStaff), Make(ItemID.ThornWhip));
			var summoner = new SoldierRecord { Id = 61, Grade = Grade.ManAtArms };
			summoner.Gear.Weapon = Make(ItemID.HornetStaff);
			ArmoryTE.TestServed = new List<SoldierRecord> { summoner };
			te.IssueGear(summoner);
			info("summoner: " + Kit(summoner));
			var staves = new List<Item> { summoner.Gear.Weapon };
			foreach (var s in summoner.Gear.Sidearms()) if (SidearmPolicy.IsStaff(s)) staves.Add(s);
			var cappedHeld = staves.Count(SummonCap.IsCapped);
			check("a summoner carries his unlimited staff, a whip and two one-minion staves (4 weapons)", WeaponsCarried(summoner) == 4 && summoner.Gear.Sidearm.type == ItemID.ThornWhip && cappedHeld == 2 && staves.Count == 3, Kit(summoner));
			check("he is not given a second unlimited staff (the Imp Staff stays on the shelf)", !summoner.Gear.Sidearms().Any(s => s.type == ItemID.ImpStaff));
			foreach (float budget in new[] { 3f, 4f, 5f })
			{
				var n = SummonCap.Allot(staves, budget, Content.NPCs.SoldierNPC.SlotsPerMinion, w => LevyConstants.MinionMaxPerSoldier);
				int used = 0; for (int i = 0; i < n.Length; i++) used += (int)Math.Round(n[i] * Content.NPCs.SoldierNPC.SlotsPerMinion(staves[i]));
				info($"minion slots {budget}: casts per staff [{string.Join(", ", staves.Select((s, i) => s.Name + " x" + n[i]))}] = {used} slots");
				check($"with {budget} minion slots every staff has its minion out and none overflows", n.Length == 3 && n[1] == 1 && n[2] == 1 && n[0] == Math.Max(1, (int)budget - 2) && used == (int)budget, string.Join(",", n));
			}
			var one = SummonCap.Allot(new List<Item> { Make(ItemID.PygmyStaff) }, 5f, Content.NPCs.SoldierNPC.SlotsPerMinion, w => 5);
			check("a lone one-minion staff fields exactly one minion whatever the minion slots", one[0] == 1, one[0].ToString());
		}

		// ------------------------------------------------------------------ D. a melee levy's secondary
		private static void MeleeSecondary(CheckFn check, Action<string> info)
		{
			SummonCap.Reset();
			var blade = Make(ItemID.TerraBlade); var sword2 = Make(ItemID.Muramasa); var gun = Make(ItemID.Musket); var staff = Make(ItemID.HornetStaff); var staff2 = Make(ItemID.ImpStaff);
			// the policy itself
			check("a melee primary takes no second sword", !SidearmPolicy.Fits(blade, sword2, null));
			check("a melee primary takes no gun", !SidearmPolicy.Fits(blade, gun, null));
			check("a melee primary takes a minion staff", SidearmPolicy.Fits(blade, staff, null));
			check("a gunner takes a lesser blade", SidearmPolicy.Fits(gun, sword2, null));
			check("a second unlimited staff is a duplicate role", !SidearmPolicy.Fits(staff, staff2, null));
			{
				var te = Armory(Make(ItemID.TerraBlade), Make(ItemID.Muramasa), Make(ItemID.HornetStaff), Make(ItemID.ImpStaff), Make(ItemID.Musket), Make(ItemID.MusketBall, 100));
				var melee = new SoldierRecord { Id = 71 };
				Armor(melee, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { melee };
				te.IssueGear(melee);
				info("melee levy, staves in stock: " + Kit(melee));
				var sides = melee.Gear.Sidearms().Where(s => !s.IsAir).ToList();
				check("a melee levy with no extra summon slot takes no secondary, never a second sword", sides.Count == 0, Kit(melee));
				check("a melee levy carries no gun and no extra blade", !sides.Any(s => !SidearmPolicy.IsDeployer(s)), Kit(melee));
			}
			{
				var te = Armory(Make(ItemID.TerraBlade), Make(ItemID.Muramasa), Make(ItemID.Musket), Make(ItemID.MusketBall, 100));
				var melee = new SoldierRecord { Id = 72 };
				Armor(melee, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { melee };
				te.IssueGear(melee);
				info("melee levy, no staff in stock: " + Kit(melee));
				check("with no summon weapon in stock a melee levy carries no secondary", melee.Gear.SidearmCount == 0, Kit(melee));
			}
			{ // an older kit: a sword secondary is given back and a staff taken
				var te = Armory(Make(ItemID.HornetStaff));
				var melee = new SoldierRecord { Id = 73 };
				Armor(melee, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				melee.Gear.Weapon = Make(ItemID.TerraBlade); melee.Gear.Sidearm = Make(ItemID.Muramasa);
				string before = Kit(melee);
				ArmoryTE.TestServed = new List<SoldierRecord> { melee };
				te.UpgradeWeapons(melee);
				info($"older melee kit with a sword secondary: before [{before}] after [{Kit(melee)}]");
				check("a second sword is put back on the shelf and, with no extra minion slot, no staff is taken in its place", !melee.Gear.Sidearms().Any(s => s.type == ItemID.Muramasa) && !melee.Gear.Sidearms().Any(SidearmPolicy.IsStaff), Kit(melee));
				bool onShelf = false; foreach (var it in te.Storage) if (it.type == ItemID.Muramasa) onShelf = true; foreach (var it in te.IssueWeapons) if (it.type == ItemID.Muramasa) onShelf = true;
				check("the sword went back to the armory, not lost", onShelf);
			}
		}

		// ------------------------------------------------------------------ D2. the two summon slots: a sentry and a minion staff; a summoner leads with a whip
		private static void SummonSlots(CheckFn check, Action<string> info)
		{
			SummonCap.Reset();
			SummonCap.Force(ItemID.PygmyStaff, 1);
			int Count(SoldierRecord r, Func<Item, bool> f) => r.Gear.Sidearms().Count(s => !s.IsAir && f(s));
			Item[] Staves() => new[] { Make(ItemID.HornetStaff), Make(ItemID.ImpStaff), Make(ItemID.StaffoftheFrostHydra), Make(ItemID.QueenSpiderStaff) };
			{ // 1. a melee levy: no summon item without an extra slot; with an extra minion slot, one minion staff; no second blade
				var te = Armory(Make(ItemID.TerraBlade), Make(ItemID.Muramasa)); foreach (var it in Staves()) te.Deposit(it);
				var melee = new SoldierRecord { Id = 91 };
				Armor(melee, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { melee };
				te.IssueGear(melee);
				info("case 1, melee levy, 2 staves + 2 sentries in stock: " + Kit(melee));
				check("a melee levy with no extra summon slot carries no summon item", melee.Gear.SidearmCount == 0, Kit(melee));
				var slotted = new SoldierRecord { Id = 192 };
				Armor(slotted, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				slotted.Gear.Accessories[0] = Make(ItemID.PapyrusScarab); slotted.Gear.SetLocked(SoldierGear.SlotAcc0, true);
				ArmoryTE.TestServed = new List<SoldierRecord> { slotted };
				te.IssueGear(slotted);
				check("with an extra minion slot a melee levy carries one minion staff and no sentry", Count(slotted, SidearmPolicy.IsStaff) == 1 && Count(slotted, SidearmPolicy.IsSentry) == 0 && slotted.Gear.SidearmCount == 1, Kit(slotted));
				// an older kit with both and no extra slot: both go back to the shelf
				var old = new SoldierRecord { Id = 191 };
				Armor(old, ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings);
				old.Gear.Weapon = Make(ItemID.TerraBlade); old.Gear.ExtraSidearms[0] = Make(ItemID.StaffoftheFrostHydra); old.Gear.ExtraSidearms[1] = Make(ItemID.ImpStaff);
				ArmoryTE.TestServed = new List<SoldierRecord> { old };
				te.UpgradeWeapons(old);
				check("an older melee kit with a sentry and a staff and no extra slot gives both back", Count(old, SidearmPolicy.IsDeployer) == 0, Kit(old));
			}
			{ // 2. a gunner: his lesser blade in hand, no summon item without an extra slot
				var te = Armory(Make(ItemID.Musket), Make(ItemID.MusketBall, 300), Make(ItemID.IronBroadsword)); foreach (var it in Staves()) te.Deposit(it);
				var gun = new SoldierRecord { Id = 92 };
				Armor(gun, ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { gun };
				te.IssueGear(gun);
				info("case 2, ranged levy: " + Kit(gun));
				check("a ranged levy carries a gun and a lesser blade and no summon item", gun.Gear.Weapon.type == ItemID.Musket && gun.Gear.Sidearm.type == ItemID.IronBroadsword && Count(gun, SidearmPolicy.IsDeployer) == 0 && WeaponsCarried(gun) == 2, Kit(gun));
			}
			{ // 3. a summoner with a whip in stock: whip primary, minion staff + sentry + a one-minion staff
				var te = Armory(Make(ItemID.ThornWhip), Make(ItemID.PygmyStaff)); foreach (var it in Staves()) te.Deposit(it);
				var sum = new SoldierRecord { Id = 93, Grade = Grade.ManAtArms };
				Armor(sum, ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { sum };
				te.IssueGear(sum);
				info("case 3, summoner with a whip in stock: " + Kit(sum));
				var held = sum.Gear.Sidearms().Where(s => !s.IsAir).ToList();
				check("a summoner leads with the whip", sum.Gear.Weapon.type == ItemID.ThornWhip, Kit(sum));
				check("he carries an unlimited minion staff, a sentry staff and a one-minion staff", held.Count(SidearmPolicy.IsSentry) == 1 && held.Count(s => SidearmPolicy.IsStaff(s) && !SummonCap.IsCapped(s)) == 1 && held.Count(s => SidearmPolicy.IsStaff(s) && SummonCap.IsCapped(s)) == 1, Kit(sum));
			}
			{ // 4. a summoner with no whip: the staff leads, the sentry rides as a sidearm
				var te = Armory(Make(ItemID.PygmyStaff)); foreach (var it in Staves()) te.Deposit(it);
				var sum = new SoldierRecord { Id = 94, Grade = Grade.ManAtArms };
				Armor(sum, ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { sum };
				te.IssueGear(sum);
				info("case 4, summoner, no whip in stock: " + Kit(sum));
				check("with no whip a summoner leads with a minion staff and carries a sentry staff", SidearmPolicy.IsStaff(sum.Gear.Weapon) && Count(sum, SidearmPolicy.IsSentry) == 1, Kit(sum));
			}
			{ // a staff-led summoner swaps to the whip when one arrives, and keeps the staff as a sidearm
				var te = Armory(Make(ItemID.ThornWhip)); te.Deposit(Make(ItemID.StaffoftheFrostHydra));
				var sum = new SoldierRecord { Id = 95, Grade = Grade.ManAtArms };
				Armor(sum, ItemID.StardustHelmet, ItemID.StardustBreastplate, ItemID.StardustLeggings);
				sum.Gear.Weapon = Make(ItemID.HornetStaff);
				ArmoryTE.TestServed = new List<SoldierRecord> { sum };
				te.UpgradeWeapons(sum);
				info("case 4b, a whip arrives for a staff-led summoner: " + Kit(sum));
				check("the whip becomes his primary and the staff a sidearm", sum.Gear.Weapon.type == ItemID.ThornWhip && Count(sum, SidearmPolicy.IsStaff) == 1, Kit(sum));
			}
			{ // extra slots take a sentry from the hand, and the wire format keeps it
				var g = new SoldierGear();
				check("an extra slot holds a sentry staff by hand", g.CanHold(SoldierGear.SlotSide2, Make(ItemID.QueenSpiderStaff)) && !g.CanHold(SoldierGear.SlotSide2, Make(ItemID.IronBroadsword)));
			}
		}

		// ------------------------------------------------------------------ E. preferred armor and accessories
		private static void PreferredGear(CheckFn check, Action<string> info)
		{
			string Armor3(SoldierRecord r) => $"{N(r.Gear.Head)} / {N(r.Gear.Body)} / {N(r.Gear.Legs)}";
			SoldierRecord Wearer(int id) { var r = new SoldierRecord { Id = id }; r.Gear.Weapon = Make(ItemID.IronBroadsword); return r; }
			ArmoryTE Sets()
			{
				var te = new ArmoryTE();
				foreach (var t in new[] { ItemID.HallowedMask, ItemID.HallowedPlateMail, ItemID.HallowedGreaves, ItemID.AdamantiteHelmet, ItemID.AdamantiteBreastplate, ItemID.AdamantiteLeggings }) te.Deposit(Make(t));
				return te;
			}
			{
				var te = Sets(); var r = Wearer(81);
				ArmoryTE.TestServed = new List<SoldierRecord> { r };
				te.IssueGear(r);
				info("armor sets in stock, nothing preferred: " + Armor3(r));
				check("without a preference the stronger set is worn", r.Gear.Head.type == ItemID.HallowedMask && r.Gear.Body.type == ItemID.HallowedPlateMail && r.Gear.Legs.type == ItemID.HallowedGreaves, Armor3(r));
			}
			{
				var te = Sets(); var r = Wearer(82);
				foreach (var t in new[] { ItemID.AdamantiteHelmet, ItemID.AdamantiteBreastplate, ItemID.AdamantiteLeggings }) te.Rules.OverrideFor(t, true).Rank = RankOverrideMode.Prefer;
				te.Rules.Changed();
				// the preference survives the wire format (saved with the armory, synced to clients)
				var back = ArmoryRules.FromBytes(te.Rules.ToBytes());
				check("the preferred pieces survive the save / sync format", back.OverrideOf(ItemID.AdamantiteBreastplate)?.Rank == RankOverrideMode.Prefer);
				ArmoryTE.TestServed = new List<SoldierRecord> { r };
				te.IssueGear(r);
				info("Adamantite set preferred: " + Armor3(r));
				check("the preferred armor set is dealt first", r.Gear.Head.type == ItemID.AdamantiteHelmet && r.Gear.Body.type == ItemID.AdamantiteBreastplate && r.Gear.Legs.type == ItemID.AdamantiteLeggings, Armor3(r));
			}
			{
				var te = new ArmoryTE(); te.Deposit(Make(ItemID.ObsidianShield)); te.Deposit(Make(ItemID.BandofRegeneration)); te.Deposit(Make(ItemID.HermesBoots));
				var r = new SoldierRecord { Id = 83 };
				te.Rules.OverrideFor(ItemID.BandofRegeneration, true).Rank = RankOverrideMode.Prefer; te.Rules.Changed();
				ArmoryTE.TestServed = new List<SoldierRecord> { r };
				te.IssueGear(r);
				string worn = string.Join(", ", r.Gear.Accessories.Take(r.AccessorySlots).Select(N));
				info("Band of Regeneration preferred, two slots: " + worn);
				check("a preferred accessory is worn", r.Gear.Accessories.Take(r.AccessorySlots).Any(a => a.type == ItemID.BandofRegeneration), worn);
				te.Deposit(Make(ItemID.CharmofMyths)); // a stronger accessory arrives
				ArmoryTE.TestServed = new List<SoldierRecord> { r };
				te.UpgradeAccessories(r);
				check("a preferred accessory is not swapped for an ordinary one", r.Gear.Accessories.Take(r.AccessorySlots).Any(a => a.type == ItemID.BandofRegeneration), string.Join(", ", r.Gear.Accessories.Take(r.AccessorySlots).Select(N)));
			}
		}

		// ------------------------------------------------------------------ persistence
		private static void RoundTrips(CheckFn check)
		{
			var g = new SoldierGear();
			g.Weapon = Make(ItemID.HornetStaff); g.Sidearm = Make(ItemID.ThornWhip); g.ExtraSidearms[0] = Make(ItemID.PygmyStaff); g.ExtraSidearms[1] = Make(ItemID.SpiderStaff);
			g.SetLocked(SoldierGear.SlotSide2 + 1, true);
			var back = SoldierGear.Load(g.Save());
			check("extra sidearms and their lock survive a world save", back.ExtraSidearms[0].type == ItemID.PygmyStaff && back.ExtraSidearms[1].type == ItemID.SpiderStaff && back.IsLocked(SoldierGear.SlotSide2 + 1) && back.SidearmCount == 3);
			var old = new SoldierGear(); old.Weapon = Make(ItemID.IronBroadsword);
			var oldTag = old.Save();
			check("a save without extra sidearms loads empty", SoldierGear.Load(oldTag).SidearmCount == 0);
			var clone = g.Clone();
			check("a copy keeps the extra sidearms", clone.ExtraSidearms[1].type == ItemID.SpiderStaff && clone.AllItems().Count(i => i.type == ItemID.PygmyStaff) == 1);
			using var ms = new System.IO.MemoryStream();
			using (var w = new System.IO.BinaryWriter(ms)) g.WriteVisual(w);
			var vis = new SoldierGear();
			using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(ms.ToArray()))) vis.ReadVisual(r);
			check("extra sidearms reach clients", vis.ExtraSidearms[0].type == ItemID.PygmyStaff && vis.ExtraSidearms[1].type == ItemID.SpiderStaff && vis.Sidearm.type == ItemID.ThornWhip);
			check("slot count fits the lock mask", SoldierGear.SlotCount <= 16, SoldierGear.SlotCount.ToString());
		}
	}
}
