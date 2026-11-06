using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>DIAGNOSTIC (log only, LEVY_ARMORYPROBE=1): what the armory does with thrown stacks, sidearms and class-less levies.</summary>
	public class ArmoryProbe : ModSystem
	{
		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : i.Name + (i.stack > 1 ? "x" + i.stack : "");
		private void L(string s) => Mod.Logger.Info("[ArmoryProbe] " + s);

		public override void PostSetupRecipes()
		{
			if (Environment.GetEnvironmentVariable("LEVY_ARMORYPROBE") != "1") return;
			try { Run(); } catch (Exception e) { L("THREW " + e); }
		}

		private static SoldierRecord Soldier(int id, int[] armor = null)
		{
			var s = new SoldierRecord { Id = id };
			if (armor != null) { s.Gear.Head = Make(armor[0]); s.Gear.Body = Make(armor[1]); s.Gear.Legs = Make(armor[2]); }
			return s;
		}

		private void Run()
		{
			MaterialCost.Ensure();
			ExtraDamageClasses.Reset();
			int[] thrown = { ItemID.MolotovCocktail, ItemID.ThrowingKnife, ItemID.PoisonedKnife, ItemID.Grenade, ItemID.Shuriken, ItemID.SpikyBall, ItemID.BoneJavelin, ItemID.BoneDagger, ItemID.Javelin, ItemID.Dynamite, ItemID.Bomb, ItemID.Beenade, ItemID.StickyGrenade };
			foreach (int t in thrown)
			{
				var it = Make(t);
				var p = WeaponClassifier.GetProfile(it);
				var proj = it.shoot > 0 ? ContentSamples.ProjectilesByType[it.shoot] : null;
				var g = new SoldierGear();
				L($"item {it.Name}: family={p.Family} supported={p.Supported} issuable={WeaponClassifier.IsIssuableWeapon(it)} class={WeaponClassifier.ItemDamageClass(it)?.Name ?? "null"} dmgType={it.DamageType?.Name} maxStack={it.maxStack} dmg={it.damage} consumable={it.consumable} ai={proj?.aiStyle} rank={WeaponRank.Of(it):F1} slotCanHold={g.CanHold(SoldierGear.SlotWeapon, Make(t, 30))} IsWeaponShaped={UI.ArmoryPanel.IsWeaponShaped(it)} IsJunk={UI.ArmoryPanel.IsJunk(it)}");
			}
			// B: only molotovs
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.MolotovCocktail, 200);
				var s = Soldier(100); ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				L($"B only molotov(200): classless gets {N(s.Gear.Weapon)} sidearm={N(s.Gear.Sidearm)} left={N(te.Storage[0])}");
			}
			// C: molotov + gun
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.MolotovCocktail, 200); te.Storage[1] = Make(ItemID.Musket); te.Storage[2] = Make(ItemID.MusketBall, 500);
				var list = Enumerable.Range(100, 5).Select(i => Soldier(i)).ToList(); ArmoryTE.TestServed = list;
				try { foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				L("C molotov+musket, 5 classless: " + string.Join(" | ", list.Select(s => N(s.Gear.Weapon) + "/" + N(s.Gear.Sidearm))));
			}
			// C2: knives + blade, melee-armor levy
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.ThrowingKnife, 200); te.Storage[1] = Make(ItemID.IronBroadsword);
				var s = Soldier(100, new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings });
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				L($"C2 knives+broadsword, melee-armor levy: {N(s.Gear.Weapon)}/{N(s.Gear.Sidearm)}");
			}
			SidearmScenario("E1 storage stock", false);
			SidearmScenario("E2 rack stock", true);
			// F: class-less spread
			{
				var te = new ArmoryTE(); int k = 0;
				foreach (int t in new int[] { ItemID.IronBroadsword, ItemID.CobaltSword, ItemID.MythrilSword, ItemID.TitaniumSword, ItemID.Excalibur, ItemID.TrueExcalibur }) te.Storage[k++] = Make(t);
				foreach (int t in new int[] { ItemID.Musket, ItemID.Boomstick, ItemID.Minishark, ItemID.ClockworkAssaultRifle, ItemID.Megashark, ItemID.ChainGun }) te.Storage[k++] = Make(t);
				foreach (int t in new int[] { ItemID.WandofSparking, ItemID.AmethystStaff, ItemID.SapphireStaff, ItemID.EmeraldStaff, ItemID.RubyStaff, ItemID.DiamondStaff }) te.Storage[k++] = Make(t);
				te.Storage[k++] = Make(ItemID.MusketBall, 999);
				var list = Enumerable.Range(100, 12).Select(i => Soldier(i)).ToList(); ArmoryTE.TestServed = list;
				try { foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				var cnt = list.GroupBy(s => WeaponClassifier.ItemDamageClass(s.Gear.Weapon)?.Name ?? "none").Select(g => g.Key + "=" + g.Count());
				L("F 12 classless, 6 blades/6 guns/6 staves: " + string.Join(",", cnt) + " :: " + string.Join(" | ", list.Select(s => N(s.Gear.Weapon) + "/" + N(s.Gear.Sidearm))));
			}
		}

		private void SidearmScenario(string tag, bool rack)
		{
			var te = new ArmoryTE(); int rk = 0, sk = 0;
			void Put(Item it) { if (rack && rk < ArmoryTE.SidearmSlot) te.IssueWeapons[rk++] = it; else te.Storage[sk++] = it; }
			foreach (int t in new int[] { ItemID.IronBroadsword, ItemID.CobaltSword, ItemID.MythrilSword, ItemID.TitaniumSword, ItemID.Excalibur, ItemID.TrueExcalibur, ItemID.NightsEdge, ItemID.Muramasa }) Put(Make(t));
			foreach (int t in new int[] { ItemID.Musket, ItemID.Minishark, ItemID.Megashark, ItemID.ChainGun }) Put(Make(t));
			foreach (int t in new int[] { ItemID.WandofSparking, ItemID.AmethystStaff, ItemID.RubyStaff }) Put(Make(t));
			te.Storage[sk++] = Make(ItemID.MusketBall, 999);
			var ranged = new int[] { ItemID.VortexHelmet, ItemID.VortexBreastplate, ItemID.VortexLeggings };
			var magic = new int[] { ItemID.NebulaHelmet, ItemID.NebulaBreastplate, ItemID.NebulaLeggings };
			var melee = new int[] { ItemID.SolarFlareHelmet, ItemID.SolarFlareBreastplate, ItemID.SolarFlareLeggings };
			var list = new List<SoldierRecord> { Soldier(100, ranged), Soldier(101, ranged), Soldier(102, ranged), Soldier(103, magic), Soldier(104, magic), Soldier(105, melee), Soldier(106), Soldier(107) };
			ArmoryTE.TestServed = list;
			try { foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
			L($"{tag}: " + string.Join(" | ", list.Select(s => (ArmorDoctrine.PreferredClass(s.Gear.Head, s.Gear.Body, s.Gear.Legs)?.Name ?? "free") + ":" + N(s.Gear.Weapon) + "/" + N(s.Gear.Sidearm))));
		}
	}
}
