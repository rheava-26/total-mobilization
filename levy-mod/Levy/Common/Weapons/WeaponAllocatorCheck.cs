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
	/// Boot-time self-check of the army weapon allocator (<see cref="WeaponAllocator"/> and the armory's issue / upgrade / sidearm paths), in the style of <see cref="WeaponWiringCheck"/>.
	/// Logs one summary line (or the failures); inspect-and-log only: it builds throwaway armories and soldiers, touches no world state and never throws.
	/// </summary>
	public class WeaponAllocatorCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		public override void PostSetupRecipes()
		{
			MaterialCost.Ensure();
			var failures = new List<string>();
			int ok = 0;
			string tag = "";
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what + tag); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}{tag}"); }
			}
			// The same checks under every ranking mode (the price modes with the armored-enemy skip on, at the starting stage).
			foreach (var (mode, skip) in new[] { (WeaponRankMode.Damage, false), (WeaponRankMode.SellPrice, true), (WeaponRankMode.SellPricePlusMaterials, true) })
			{
				WeaponRank.Force(mode, skip); tag = " [" + mode + "]";
				try
				{
					Guard("pure allocator", () => CheckPure(Check));
					Guard("armory issue", () => CheckArmory(Check));
					Guard("extra classes", () => CheckExtraClasses(Check));
				}
				finally { WeaponRank.Release(); tag = ""; }
			}
			try {
				if (failures.Count == 0) Mod.Logger.Info($"Levy weapon allocator self-check: {ok} checks OK");
				else {
					Mod.Logger.Warn($"Levy weapon allocator self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (var f in failures) Mod.Logger.Warn("Levy weapon allocator self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		// ------------------------------------------------------------------ the pure allocator

		private static AllocLevy Lv(int id, DamageClass cls, Item cur = null)
		{
			var l = new AllocLevy { Id = id, Cls = cls, Current = cur };
			return l;
		}

		private static List<AllocCand> Cands(List<AllocLevy> levies, params Item[] shelf)
		{
			var res = new List<AllocCand>(); int seq = 0;
			foreach (var l in levies) if (l.Current != null) { l.Own = WeaponAllocator.MakeCand(l.Current, 5, seq++, AllocSlot.Primary, l, null); res.Add(l.Own); }
			foreach (var s in shelf) res.Add(WeaponAllocator.MakeCand(s, 1, seq++, AllocSlot.Shelf, null, null));
			return res;
		}

		private static void CheckPure(Action<bool, string> check)
		{
			var terra = Make(ItemID.TerraBlade); var mura = Make(ItemID.Muramasa); var wood = Make(ItemID.WoodenSword);
			var musket = Make(ItemID.Musket); var space = Make(ItemID.SpaceGun);

			// b) the unclassed are served after the classed: a melee levy takes the Terra Blade an unclassed levy was holding, and the unclassed levy is re-armed from what is left.
			{
				var flex = Lv(1, null, terra); var melee = Lv(2, DamageClass.Melee);
				var levies = new List<AllocLevy> { flex, melee };
				var cands = Cands(levies, wood, space);
				WeaponAllocator.Plan(levies, cands);
				check(melee.Target?.It == terra, "a melee levy takes the best blade from an unclassed levy");
				check(flex.Target != null && flex.Target.It != terra, "the unclassed levy is re-armed from what is left, not left bare");
			}
			// e) a better-matched levy is never stripped: two melee levies, one holds the Terra Blade, the other is bare; the bare one gets the next blade.
			{
				var a = Lv(1, DamageClass.Melee, terra); var b = Lv(2, DamageClass.Melee);
				var levies = new List<AllocLevy> { a, b };
				var cands = Cands(levies, mura, wood);
				WeaponAllocator.Plan(levies, cands);
				check(a.Target == a.Own, "a melee levy holding the best blade keeps it when another melee levy is bare");
				check(b.Target?.It == mura, "the bare melee levy gets the best blade left");
			}
			// a) class-matched first: the ranged levy gets the gun, not the blade-heavy leftovers; melee levies enlisted last are not left bare.
			{
				var r = Lv(1, DamageClass.Ranged, musket); var m1 = Lv(2, DamageClass.Melee); var m2 = Lv(3, DamageClass.Melee);
				var levies = new List<AllocLevy> { r, m1, m2 };
				var cands = Cands(levies, terra, mura);
				WeaponAllocator.Plan(levies, cands);
				check(r.Target == r.Own, "a ranged levy keeps his gun");
				check(m1.Target != null && m2.Target != null && new[] { m1.Target.It, m2.Target.It }.Contains(terra) && new[] { m1.Target.It, m2.Target.It }.Contains(mura), "two bare melee levies are dealt the two blades");
			}
			// e) an upgrade needs the margin: a sword at 90% of the best on offer is not traded.
			{
				var l = Lv(1, DamageClass.Melee, mura);
				var levies = new List<AllocLevy> { l };
				var cands = Cands(levies, wood);
				WeaponAllocator.Plan(levies, cands);
				check(l.Target == l.Own, "no swap for a weaker weapon");
				var terra2 = Make(ItemID.TerraBlade);
				cands = Cands(levies, terra2);
				WeaponAllocator.Plan(levies, cands);
				check(l.Target != null && l.Target.It == terra2, "a clearly better blade is taken");
			}
			// A class-less weapon (no damage class at all) upgrades: an unclassed levy goes to anything clearly better, and a classed levy is moved into his own class.
			{
				var fake = Make(ItemID.WoodenSword);
				var flex = Lv(1, null, fake); var armored = Lv(2, DamageClass.Melee, Make(ItemID.WoodenBow));
				var levies = new List<AllocLevy> { flex, armored };
				var cands = Cands(levies, Make(ItemID.MythrilSword), Make(ItemID.Muramasa));
				flex.Own.Cls = null; // a weapon with no class
				armored.Own.Cls = null;
				WeaponAllocator.Plan(levies, cands);
				check(flex.Target != null && flex.Target != flex.Own, "a levy holding a class-less weapon upgrades to anything better");
				check(armored.Target != null && armored.Target != armored.Own && armored.Target.Cls == DamageClass.Melee, "a classed levy holding a class-less weapon is moved into his class");
			}
			// A whip in hand yields to a minion staff whatever the staff scores.
			{
				var whip = Make(ItemID.ThornWhip); var staff = Make(ItemID.SlimeStaff);
				var l = Lv(1, null, whip);
				l.Yields = c => WeaponClassifier.GetProfile(c.It).Family == WeaponFamily.Summon;
				var levies = new List<AllocLevy> { l };
				WeaponAllocator.Plan(levies, Cands(levies, staff));
				check(l.Target != null && l.Target.It == staff, "a whip in hand yields to a minion staff");
			}
			// c) variety: three melee levies facing three close blades each get a different type.
			{
				var items = new[] { Make(ItemID.CobaltSword), Make(ItemID.PalladiumSword), Make(ItemID.MythrilSword) };
				var levies = new List<AllocLevy> { Lv(10, DamageClass.Melee), Lv(11, DamageClass.Melee), Lv(12, DamageClass.Melee) };
				WeaponAllocator.Plan(levies, Cands(levies, items));
				check(levies.All(l => l.Target != null) && levies.Select(l => l.Target.It.type).Distinct().Count() == 3, "close-scoring weapon types are spread across the levies");
			}
		}

		// ------------------------------------------------------------------ the armory

		private static ArmoryTE Armory(params Item[] weapons)
		{
			var te = new ArmoryTE();
			for (int i = 0; i < weapons.Length; i++) te.Storage[i] = weapons[i];
			return te;
		}

		private static void CheckArmory(Action<bool, string> check)
		{
			// Bug: the weapon rack was read before storage, so a Zombie Arm in the rack beat a Chlorophyte Saber in storage.
			{
				var te = Armory(Make(ItemID.ChlorophyteSaber));
				te.IssueWeapons[0] = Make(ItemID.ZombieArm);
				var s = new SoldierRecord { Id = 100 };
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				check(s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.ChlorophyteSaber, "the best weapon in storage beats a worse one on the rack (got " + (s.Gear.HasWeapon ? s.Gear.Weapon.Name : "nothing") + ")");
			}
			// ... and the rack still wins a tie.
			{
				var te = Armory(Make(ItemID.IronBroadsword));
				te.IssueWeapons[0] = Make(ItemID.IronBroadsword);
				var s = new SoldierRecord { Id = 100 };
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				check(s.Gear.HasWeapon && te.IssueWeapons[0].IsAir && !te.Storage[0].IsAir, "the rack is preferred on a tie");
			}
			// Soldiers enlisted one by one: an unclassed levy first, then a levy in melee armor; the armored one ends up with the better blade.
			var melee = Make(ItemID.SolarFlareHelmet); var body = Make(ItemID.SolarFlareBreastplate); var legs = Make(ItemID.SolarFlareLeggings);
			ArmorDoctrine.Reset();
			bool armorWorks = ArmorDoctrine.PreferredClass(melee, body, legs) == DamageClass.Melee;
			if (armorWorks) {
				var te = Armory(Make(ItemID.TerraBlade), Make(ItemID.Muramasa));
				var bare = new SoldierRecord { Id = 100 };
				var armored = new SoldierRecord { Id = 101 };
				armored.Gear.Head = Make(ItemID.SolarFlareHelmet); armored.Gear.Body = Make(ItemID.SolarFlareBreastplate); armored.Gear.Legs = Make(ItemID.SolarFlareLeggings);
				var served = new List<SoldierRecord> { bare };
				ArmoryTE.TestServed = served;
				try {
					te.IssueGear(bare);
					served.Add(armored);
					te.IssueGear(armored);
				}
				finally { ArmoryTE.TestServed = null; }
				check(armored.Gear.HasWeapon && armored.Gear.Weapon.type == ItemID.TerraBlade, "the levy in melee armor gets the best blade even though he enlisted second (got " + (armored.Gear.HasWeapon ? armored.Gear.Weapon.Name : "nothing") + ")");
				check(bare.Gear.HasWeapon && bare.Gear.Weapon.type == ItemID.Muramasa, "the unclassed levy was re-armed with the next blade, not left bare");

				// d) a sidearm never takes a blade a melee levy wants: the ranged levy gets no blade while the melee levy still lacks one.
				var te2 = Armory(Make(ItemID.Musket), Make(ItemID.MusketBall));
				te2.Storage[1].stack = 200;
				te2.Storage[2] = Make(ItemID.Muramasa);
				var gun = new SoldierRecord { Id = 200 };
				gun.Gear.Head = Make(ItemID.VortexHelmet); gun.Gear.Body = Make(ItemID.VortexBreastplate); gun.Gear.Legs = Make(ItemID.VortexLeggings);
				var swordsman = new SoldierRecord { Id = 201 };
				swordsman.Gear.Head = Make(ItemID.SolarFlareHelmet); swordsman.Gear.Body = Make(ItemID.SolarFlareBreastplate); swordsman.Gear.Legs = Make(ItemID.SolarFlareLeggings);
				ArmoryTE.TestServed = new List<SoldierRecord> { gun, swordsman };
				try { te2.IssueGear(gun); te2.IssueGear(swordsman); } finally { ArmoryTE.TestServed = null; }
				check(gun.Gear.HasWeapon && gun.Gear.Weapon.type == ItemID.Musket, "the ranged levy gets the gun");
				check(swordsman.Gear.HasWeapon && swordsman.Gear.Weapon.type == ItemID.Muramasa, "the melee levy gets the only blade; the gunner's sidearm did not take it (got " + (swordsman.Gear.HasWeapon ? swordsman.Gear.Weapon.Name : "nothing") + ")");
				// Loadout locks are respected: a locked soldier keeps his weapon and is not in the pool, however much better one is on offer.
				var te3 = Armory(Make(ItemID.TerraBlade));
				var locked = new SoldierRecord { Id = 300, LoadoutLocked = true };
				locked.Gear.Weapon = Make(ItemID.WoodenSword);
				var free = new SoldierRecord { Id = 301 };
				ArmoryTE.TestServed = new List<SoldierRecord> { locked, free };
				bool lockedChanged;
				try { te3.IssueGear(free); lockedChanged = te3.UpgradeWeapons(locked); } finally { ArmoryTE.TestServed = null; }
				check(!lockedChanged && locked.Gear.Weapon.type == ItemID.WoodenSword, "a loadout-locked soldier is never upgraded");
				check(free.Gear.HasWeapon && free.Gear.Weapon.type == ItemID.TerraBlade, "the free soldier gets the blade the locked one could have had");
			}
		}

		// ------------------------------------------------------------------ rogue / bard / healer

		private static void CheckExtraClasses(Action<bool, string> check)
		{
			ExtraDamageClasses.Reset();
			check(ExtraDamageClasses.All != null, "the extra damage class lookup runs without the optional mods");
			if (ExtraDamageClasses.All.Count == 0) return;
			// Every issuable weapon of a mod-added rogue / bard / healer type reports a real class, and a levy holding one is upgradeable.
			Item sample = null;
			foreach (var m in new[] { "CalamityMod", "ThoriumMod" }) {
				if (!ModLoader.TryGetMod(m, out var mod)) continue;
				foreach (var mi in mod.GetContent<ModItem>()) {
					var it = Make(mi.Type);
					if (it.damage <= 0 || it.accessory || it.ammo > 0 || !WeaponClassifier.IsIssuableWeapon(it)) continue;
					var ex = ExtraDamageClasses.Of(it);
					if (ex == null) continue;
					check(WeaponClassifier.ItemDamageClass(it) == ex, $"{m}/{mi.Name} reports its {ex.Name} class");
					if (sample == null || WeaponRank.Of(it) < WeaponRank.Of(sample)) sample = it; // the weakest, so Mythril Sword is a clear upgrade
				}
			}
			if (sample == null) return;
			var te = Armory(Make(ItemID.MythrilSword));
			var s = new SoldierRecord { Id = 100 };
			s.Gear.Weapon = sample;
			ArmoryTE.TestServed = new List<SoldierRecord> { s };
			try { te.UpgradeWeapons(s); } finally { ArmoryTE.TestServed = null; }
			check(s.Gear.Weapon.type != sample.type, $"a levy holding {sample.Name} (a {WeaponClassifier.ItemDamageClass(sample)?.Name} weapon) upgrades");
		}
	}
}
