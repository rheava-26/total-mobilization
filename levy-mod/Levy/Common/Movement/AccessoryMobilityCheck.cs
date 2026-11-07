using System;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>Boot self-check (headless server): the mobility detector flags jump bottles, balloon bundles and wings and not a plain accessory, and the armory hands a mobility accessory out first. Logged as "[AccMobCheck]".</summary>
	public class AccessoryMobilityCheck : ModSystem
	{
		static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		public override void PostSetupRecipes()
		{
			if (!Main.dedServ) return;
			int pass = 0, fail = 0;
			void Check(string name, bool ok, string detail = "") { if (ok) pass++; else fail++; Mod.Logger.Info($"[AccMobCheck] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}"); }
			try
			{
				var cloud = Make(ItemID.CloudinaBottle);
				var bundle = Make(ItemID.BundleofBalloons);
				var wings = Make(ItemID.AngelWings);
				var regen = Make(ItemID.BandofRegeneration);
				var sand = Make(ItemID.SandstorminaBottle);
				var cloudBalloon = Make(ItemID.CloudinaBalloon);
				Check("Cloud in a Bottle is mobility", AccessoryMobility.Is(cloud), $"{AccessoryMobility.Read(cloud)}");
				Check("Sandstorm in a Bottle is mobility", AccessoryMobility.Is(sand), $"{AccessoryMobility.Read(sand)}");
				Check("Cloud in a Balloon is mobility", AccessoryMobility.Is(cloudBalloon), $"{AccessoryMobility.Read(cloudBalloon)}");
				Check("Bundle of Balloons has several jumps", AccessoryMobility.Read(bundle).jumps >= 2, $"{AccessoryMobility.Read(bundle)}");
				Check("Angel Wings are mobility", AccessoryMobility.Read(wings).wings && AccessoryMobility.Is(wings));
				Check("Band of Regeneration is not mobility", !AccessoryMobility.Is(regen), $"{AccessoryMobility.Read(regen)}");
				Check("wings outrank a bundle, a bundle outranks a bottle", AccessoryMobility.Value(wings) > AccessoryMobility.Value(bundle) && AccessoryMobility.Value(bundle) > AccessoryMobility.Value(cloud));

				var rocket = Make(ItemID.RocketBoots); var spectre = Make(ItemID.SpectreBoots); var frog = Make(ItemID.FrogLeg);
				var amph = Make(ItemID.AmphibianBoots); var hermes = Make(ItemID.HermesBoots);
				foreach (var x in new[] { cloud, bundle, wings, rocket, spectre, frog, amph, hermes, regen })
					Mod.Logger.Info($"[AccMobCheck] VALUE {x.Name} = {AccessoryMobility.Value(x):0.00} {AccessoryMobility.Read(x)} {AccessoryMobility.Dbg(x)}");
				Check("Rocket Boots are mobility", AccessoryMobility.Is(rocket));
				Check("Spectre Boots are mobility", AccessoryMobility.Is(spectre));
				Check("Frog Leg is mobility", AccessoryMobility.Is(frog));
				Check("Amphibian Boots are mobility", AccessoryMobility.Is(amph));
				Check("Hermes Boots are mobility", AccessoryMobility.Is(hermes));
				Check("Spectre Boots outrank Rocket Boots, below Angel Wings", AccessoryMobility.Value(spectre) > AccessoryMobility.Value(rocket) && AccessoryMobility.Value(wings) >= AccessoryMobility.Value(spectre));
				Check("Rocket Boots, Frog Leg, Amphibian are about a bottle", Math.Abs(AccessoryMobility.Value(rocket) - AccessoryMobility.Value(cloud)) <= 0.6f && Math.Abs(AccessoryMobility.Value(frog) - AccessoryMobility.Value(cloud)) <= 0.6f && Math.Abs(AccessoryMobility.Value(amph) - AccessoryMobility.Value(cloud)) <= 0.6f);
				Check("Hermes Boots (sprint only) rank below a bottle", AccessoryMobility.Value(hermes) < AccessoryMobility.Value(cloud));

				var te = new ArmoryTE();
				te.Deposit(Make(ItemID.BandofRegeneration)); te.Deposit(Make(ItemID.CloudinaBottle)); te.Deposit(Make(ItemID.ObsidianShield));
				var rec = new SoldierRecord { Id = 1, Grade = Grade.Levy };
				var got = te.TakeAccessory(rec);
				Check("armory hands a levy the bottle first", got != null && got.type == ItemID.CloudinaBottle, got?.Name ?? "null");
				rec.Gear.Accessories[0] = got ?? new Item();
				var second = te.TakeAccessory(rec);
				Check("second pick is not a second jump item when a stronger accessory is stocked", second != null && second.type != ItemID.CloudinaBottle, second?.Name ?? "null");

				// a levy in Spectre Boots + 3 weaker accessories, a bottle in stock: the boots stay
				var te2 = new ArmoryTE();
				te2.Deposit(Make(ItemID.CloudinaBottle));
				var r2 = new SoldierRecord { Id = 2, Grade = Grade.Levy };
				r2.Gear.Accessories[0] = Make(ItemID.BandofRegeneration); r2.Gear.Accessories[1] = Make(ItemID.SpectreBoots);
				r2.Gear.Accessories[2] = Make(ItemID.ShinyRedBalloon); r2.Gear.Accessories[3] = Make(ItemID.ObsidianShield);
				r2.Grade = Grade.Levy;
				bool upgraded = false; string upErr = "";
				try { upgraded = te2.UpgradeAccessories(r2); } catch (Exception ue) { upErr = ue.GetType().Name; }
				Mod.Logger.Info($"[AccMobCheck] INFO upgrade ran={upgraded} {upErr} slots={string.Join(",", Array.ConvertAll(r2.Gear.Accessories, x => x?.Name ?? "-"))}");
				bool bootsKept = false;
				for (int i = 0; i < r2.Gear.Accessories.Length; i++) if (r2.Gear.Accessories[i]?.type == ItemID.SpectreBoots) bootsKept = true;
				Check("levy keeps Spectre Boots with a bottle in stock", bootsKept);
			}
			catch (Exception e) { Check("self-check ran", false, e.GetType().Name + ": " + e.Message); }
			Mod.Logger.Info($"[AccMobCheck] DONE pass={pass} fail={fail}");
		}
	}
}
