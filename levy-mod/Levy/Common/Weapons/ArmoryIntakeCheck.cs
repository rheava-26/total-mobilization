using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot self-check of the armory's intake (what Deposit junk / shift-click take), thrown stacks (Molotov, knives) in the army allocation, the class-less spread, the mana pull toward magic and the
	/// periodic sidearm fill. Logs one summary line (or the failures); builds throwaway armories and soldiers only. LEVY_ARMORYPROBE=1 also logs the stock tables it saw.
	/// </summary>
	public class ArmoryIntakeCheck : ModSystem
	{
		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : i.Name + (i.stack > 1 ? "x" + i.stack : "");

		public override void PostSetupRecipes()
		{
			MaterialCost.Ensure();
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			bool verbose = Environment.GetEnvironmentVariable("LEVY_ARMORYPROBE") == "1";
			void Log(string s) { if (verbose) Mod.Logger.Info("[ArmoryCheck] " + s); }
			foreach (var (mode, skip) in new[] { (WeaponRankMode.Damage, false), (WeaponRankMode.SellPrice, true) })
			{
				WeaponRank.Force(mode, skip);
				string tag = " [" + mode + "]";
				void C(bool cond, string what) => Check(cond, what + tag);
				try
				{
					Guarded("intake", () => Intake(C), failures, tag);
					Guarded("thrown", () => Thrown(C, Log), failures, tag);
					Guarded("spread", () => Spread(C, Log, verbose), failures, tag);
					Guarded("mana", () => Mana(C), failures, tag);
					Guarded("sidearm", () => Sidearm(C, Log), failures, tag);
				}
				finally { WeaponRank.Release(); }
			}
			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy armory intake/spread self-check: {ok} checks OK");
				else { Mod.Logger.Warn($"Levy armory intake/spread self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy armory intake/spread self-check FAILED: " + f); }
			}
			catch (Exception) { }
		}

		private static void Guarded(string name, Action body, List<string> failures, string tag)
		{
			try { body(); } catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}{tag}"); }
		}

		// ------------------------------------------------------------------ intake

		private static void Intake(Action<bool, string> check)
		{
			foreach (var (id, what) in new[] {
				(ItemID.GrapplingHook, "a grappling hook"), (ItemID.AmethystHook, "a gem hook"), (ItemID.CobaltShield, "an accessory"), (ItemID.IronHelmet, "a helmet"), (ItemID.FamiliarWig, "vanity armor"),
				(ItemID.RedDye, "a dye"), (ItemID.MusketBall, "ammo"), (ItemID.LesserHealingPotion, "a healing potion"), (ItemID.LesserManaPotion, "a mana potion"), (ItemID.IronskinPotion, "a buff potion"),
				(ItemID.Musket, "a gun"), (ItemID.MolotovCocktail, "Molotov Cocktails"), (ItemID.ThrowingKnife, "throwing knives"), (ItemID.Grenade, "grenades"), (ItemID.Shuriken, "shuriken"), (ItemID.BoneJavelin, "bone javelins"),
				(ItemID.LifeCrystal, "a life crystal"), (ItemID.ManaCrystal, "a mana crystal") })
			{
				var it = Make(id, 20);
				if (id == ItemID.RedDye) it.dye = 1; // the dye shader id is assigned after recipes are set up
				check(ArmoryIntake.Accepts(it), "the armory takes " + what);
			}
			var fav = Make(ItemID.GrapplingHook); fav.favorited = true;
			check(!ArmoryIntake.Accepts(fav), "a favourited item is never taken");
			check(!ArmoryIntake.Accepts(Make(ItemID.GoldCoin, 5)), "coins are never taken");
			check(!ArmoryIntake.Accepts(Make(ItemID.IronPickaxe)), "a pickaxe is not taken");
			check(!ArmoryIntake.Accepts(Make(ItemID.SlimySaddle)), "a mount is not taken (levies use none)");
			check(WeaponClassifier.Classify(Make(ItemID.MolotovCocktail)) == WeaponFamily.Thrown, "Molotov Cocktail is a thrown weapon");
			check(WeaponClassifier.IsIssuableWeapon(Make(ItemID.MolotovCocktail)), "Molotov Cocktail is issuable");
			foreach (int t in new[] { ItemID.ThrowingKnife, ItemID.Grenade, ItemID.Shuriken, ItemID.Javelin, ItemID.BoneJavelin, ItemID.SpikyBall, ItemID.PoisonedKnife, ItemID.MolotovCocktail })
				check(UI.ArmoryPanel.IsWeaponShaped(Make(t, 40)) && UI.ArmoryPanel.IsJunk(Make(t, 40)), Make(t).Name + " fits a weapon row and Deposit junk");
		}

		// ------------------------------------------------------------------ thrown stacks through the allocation

		private static SoldierRecord Soldier(int id, int[] armor = null)
		{
			var s = new SoldierRecord { Id = id };
			if (armor != null) { s.Gear.Head = Make(armor[0]); s.Gear.Body = Make(armor[1]); s.Gear.Legs = Make(armor[2]); }
			return s;
		}

		private static void Thrown(Action<bool, string> check, Action<string> log)
		{
			// Only Molotovs: a bare levy is armed with a stack (at most the carry limit), and the shelf loses exactly that.
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.MolotovCocktail, 200);
				var s = Soldier(100); ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				check(s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.MolotovCocktail && s.Gear.Weapon.stack >= 1 && s.Gear.Weapon.stack <= LevyConstants.ThrownCarryMax, "a levy is armed with a Molotov stack from the armory (got " + N(s.Gear.Weapon) + ")");
				check(s.Gear.Weapon.stack + te.CountInStorage(ItemID.MolotovCocktail) == 200, "no Molotov is lost or duplicated by the issue");
			}
			// A mixed stock and several class-less levies: some of them throw, the rest take guns / blades / staves.
			{
				var te = new ArmoryTE(); int k = 0;
				te.Storage[k++] = Make(ItemID.ThrowingKnife, 400); te.Storage[k++] = Make(ItemID.Musket); te.Storage[k++] = Make(ItemID.Minishark); te.Storage[k++] = Make(ItemID.MusketBall, 999);
				te.Storage[k++] = Make(ItemID.IronBroadsword); te.Storage[k++] = Make(ItemID.WandofSparking);
				var list = Enumerable.Range(100, 8).Select(i => Soldier(i)).ToList(); ArmoryTE.TestServed = list;
				try { foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				int knives = list.Count(s => s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.ThrowingKnife);
				log("thrown mix: " + string.Join(" | ", list.Select(s => N(s.Gear.Weapon) + "/" + N(s.Gear.Sidearm))));
				check(knives >= 1, "class-less levies are dealt throwing knives when a stack is stocked (got " + knives + ")");
				check(list.All(s => s.Gear.HasWeapon), "no levy is left bare-handed");
				check(list.Where(s => s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.ThrowingKnife).Sum(s => s.Gear.Weapon.stack) + te.CountInStorage(ItemID.ThrowingKnife) == 400, "no throwing knife is lost or duplicated");
			}
			// The restock loop still tops a thrown stack up from the armory.
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.Grenade, 300);
				var s = Soldier(100); s.Gear.Weapon = Make(ItemID.Grenade, 5);
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.RestockThrown(s); } finally { ArmoryTE.TestServed = null; }
				check(s.Gear.Weapon.stack > 5 && s.Gear.Weapon.stack <= LevyConstants.ThrownCarryMax, "a thrown stack is restocked from the armory (now " + s.Gear.Weapon.stack + ")");
			}
		}

		// ------------------------------------------------------------------ class-less spread

		private static ArmoryTE StockedArmory()
		{
			var te = new ArmoryTE(); int k = 0;
			foreach (int t in new[] { ItemID.IronBroadsword, ItemID.CobaltSword, ItemID.MythrilSword, ItemID.TitaniumSword, ItemID.Excalibur, ItemID.TrueExcalibur }) te.Storage[k++] = Make(t);
			foreach (int t in new[] { ItemID.Musket, ItemID.Boomstick, ItemID.Minishark, ItemID.ClockworkAssaultRifle, ItemID.Megashark, ItemID.ChainGun }) te.Storage[k++] = Make(t);
			foreach (int t in new[] { ItemID.WandofSparking, ItemID.AmethystStaff, ItemID.SapphireStaff, ItemID.EmeraldStaff, ItemID.RubyStaff, ItemID.DiamondStaff }) te.Storage[k++] = Make(t);
			te.Storage[k++] = Make(ItemID.MusketBall, 999);
			return te;
		}

		private static void Spread(Action<bool, string> check, Action<string> log, bool verbose)
		{
			var te = StockedArmory();
			if (verbose) WeaponAllocator.Trace = m => { if (m.StartsWith("spread")) log(m); };
			var list = Enumerable.Range(100, 12).Select(i => Soldier(i)).ToList(); ArmoryTE.TestServed = list;
			try { foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
			WeaponAllocator.Trace = null;
			int Count(DamageClass c) => list.Count(s => WeaponClassifier.ItemDamageClass(s.Gear.Weapon) == c);
			log($"spread 12 class-less: melee {Count(DamageClass.Melee)} ranged {Count(DamageClass.Ranged)} magic {Count(DamageClass.Magic)} :: " + string.Join(" | ", list.Select(s => N(s.Gear.Weapon))));
			check(Count(DamageClass.Melee) >= 2 && Count(DamageClass.Ranged) >= 2 && Count(DamageClass.Magic) >= 2 && Count(DamageClass.Melee) <= 7 && Count(DamageClass.Ranged) <= 7 && Count(DamageClass.Magic) <= 7, $"12 class-less levies spread over melee / ranged / magic (got {Count(DamageClass.Melee)}/{Count(DamageClass.Ranged)}/{Count(DamageClass.Magic)})");
			check(list.All(s => s.Gear.HasWeapon && WeaponClassifier.ItemDamageClass(s.Gear.Weapon) != DamageClass.Summon), "no class-less levy is given a summon weapon");
			// the pure allocator: stock 1 sword, 6 guns, 0 staves: the weights follow the stock
			var levies = Enumerable.Range(1, 6).Select(i => new AllocLevy { Id = i, Cls = null }).ToList();
			var cands = new List<AllocCand>(); int seq = 0;
			cands.Add(WeaponAllocator.MakeCand(Make(ItemID.MythrilSword), 1, seq++, AllocSlot.Shelf, null, null));
			foreach (int t in new[] { ItemID.Musket, ItemID.Boomstick, ItemID.Minishark, ItemID.ClockworkAssaultRifle, ItemID.Megashark, ItemID.ChainGun }) cands.Add(WeaponAllocator.MakeCand(Make(t), 1, seq++, AllocSlot.Shelf, null, null));
			WeaponAllocator.Plan(levies, cands);
			check(levies.All(l => l.Target != null), "every class-less levy is armed from a mixed stock");
			check(levies.Count(l => l.Target != null && l.Target.Cls == DamageClass.Ranged) >= 4, "the class-less spread follows the stock (mostly guns when mostly guns)");
		}

		// ------------------------------------------------------------------ mana

		private static void Mana(Action<bool, string> check)
		{
			check(Math.Abs(WeaponAllocator.MagicManaBias(0f) - 1f) < 1e-4 && Math.Abs(WeaponAllocator.MagicManaBias(LevyConstants.MagicManaBiasFloor) - 1f) < 1e-4, "no mana pull at or below the floor");
			check(Math.Abs(WeaponAllocator.MagicManaBias(LevyConstants.MagicManaBiasFull) - (1f + LevyConstants.MagicManaBiasGain)) < 1e-4, "full pull at the full mana mark");
			check(WeaponAllocator.MagicManaBias(100f) > WeaponAllocator.MagicManaBias(60f), "the pull rises with mana");
			int MagesAmong(float mana)
			{
				var levies = Enumerable.Range(1, 8).Select(i => new AllocLevy { Id = i, Cls = null, Mana = mana }).ToList();
				var cands = new List<AllocCand>(); int seq = 0;
				foreach (int t in new[] { ItemID.IronBroadsword, ItemID.CobaltSword, ItemID.MythrilSword, ItemID.TitaniumSword, ItemID.Excalibur, ItemID.TrueExcalibur, ItemID.NightsEdge, ItemID.Muramasa }) cands.Add(WeaponAllocator.MakeCand(Make(t), 1, seq++, AllocSlot.Shelf, null, null));
				foreach (int t in new[] { ItemID.WandofSparking, ItemID.AmethystStaff, ItemID.SapphireStaff, ItemID.EmeraldStaff, ItemID.RubyStaff, ItemID.DiamondStaff, ItemID.TopazStaff, ItemID.AmberStaff }) cands.Add(WeaponAllocator.MakeCand(Make(t), 1, seq++, AllocSlot.Shelf, null, null));
				WeaponAllocator.Plan(levies, cands);
				return levies.Count(l => l.Target != null && l.Target.Cls == DamageClass.Magic);
			}
			int low = MagesAmong(20f), high = MagesAmong(200f);
			check(high > low, $"high-mana class-less levies take more magic weapons (mana 20: {low} mages of 8, mana 200: {high})");
			// mixed army: the high-mana levy gets the staff
			var mixed = new List<AllocLevy> { new AllocLevy { Id = 1, Mana = 20f }, new AllocLevy { Id = 2, Mana = 20f }, new AllocLevy { Id = 3, Mana = 200f } };
			var mc = new List<AllocCand>(); int sq = 0;
			mc.Add(WeaponAllocator.MakeCand(Make(ItemID.MythrilSword), 1, sq++, AllocSlot.Shelf, null, null));
			mc.Add(WeaponAllocator.MakeCand(Make(ItemID.Minishark), 1, sq++, AllocSlot.Shelf, null, null));
			mc.Add(WeaponAllocator.MakeCand(Make(ItemID.RubyStaff), 1, sq++, AllocSlot.Shelf, null, null));
			WeaponAllocator.Plan(mixed, mc);
			check(mixed[2].Target?.Cls == DamageClass.Magic, "of three class-less levies the one with the most mana takes the only staff");
		}

		// ------------------------------------------------------------------ sidearms

		private static void Sidearm(Action<bool, string> check, Action<string> log)
		{
			var ranged = new int[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings };
			// A gunner enlisted before any blade is stocked has no sidearm; once one is stocked the periodic upgrade fills the slot.
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.Musket); te.Storage[1] = Make(ItemID.MusketBall, 999);
				var s = Soldier(100, ranged); ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try
				{
					te.IssueGear(s);
					bool before = s.Gear.HasSidearm;
					te.Storage[2] = Make(ItemID.IronBroadsword);
					te.UpgradeWeapons(s);
					log($"sidearm fill: before={before} after={N(s.Gear.Sidearm)}");
					check(!before, "a gunner enlisted with no blade in stock has no sidearm");
					check(s.Gear.HasSidearm && s.Gear.Sidearm.type == ItemID.IronBroadsword, "the periodic upgrade fills an empty sidearm once a blade is stocked (got " + N(s.Gear.Sidearm) + ")");
				}
				finally { ArmoryTE.TestServed = null; }
			}
			// A melee-type primary still carries no fallback blade.
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.TerraBlade); te.Storage[1] = Make(ItemID.IronBroadsword);
				var s = Soldier(100, new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings }); ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); te.UpgradeWeapons(s); } finally { ArmoryTE.TestServed = null; }
				check(s.Gear.HasWeapon && !s.Gear.HasSidearm, "a melee levy takes no fallback blade");
			}
		}
	}
}
