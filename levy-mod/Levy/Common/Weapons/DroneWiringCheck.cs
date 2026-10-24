using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Content.Items.Books;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the combat-drone feature, in the style of <see cref="WeaponWiringCheck"/>. A headless boot cannot run a drone's AI (no world, no enemies), so this checks what can
	/// be checked without one, and logs one summary line (or the failures): which weapons a drone will and will not mount, the ammo and projectile each kind resolves to, the
	/// Armory's loan ledger on a detached Armory (lend, return, the soldier reserve, the save round trip, the recall for a soldier) and the Engineering guide's wiring and text.
	/// Inspect-and-log only: it touches no world state and never throws (a gap logs a warning and the mod still loads).
	/// </summary>
	public class DroneWiringCheck : ModSystem
	{
		/// <summary>Weapons a drone must mount (faithful or approximate) and weapons it must refuse; one stable vanilla probe each.</summary>
		private static readonly int[] MustMount =
		{
			ItemID.Musket, ItemID.WoodenBow, ItemID.AmethystStaff, ItemID.WaterBolt, ItemID.Boomstick, ItemID.Minishark, ItemID.Flamethrower, ItemID.RocketLauncher,
			ItemID.Phantasm, ItemID.LastPrism, ItemID.LaserMachinegun, ItemID.VortexBeater, ItemID.DaedalusStormbow, ItemID.Blowpipe,
		};

		private static readonly int[] MustRefuse =
		{
			ItemID.CopperBroadsword, ItemID.Spear, ItemID.CopperShortsword, ItemID.BlandWhip, ItemID.SlimeStaff, ItemID.EnchantedBoomerang, ItemID.WoodYoyo, ItemID.BallOHurt,
			ItemID.ThrowingKnife, ItemID.DayBreak, ItemID.Zenith, ItemID.SolarEruption, ItemID.MagicMissile, ItemID.Flamelash, ItemID.NimbusRod, ItemID.RainbowGun,
			ItemID.FlyingKnife, ItemID.CopperPickaxe, ItemID.WoodenArrow,
		};

		private static Item Make(int type, int stack = 1)
		{
			var it = new Item();
			it.SetDefaults(type);
			it.stack = stack;
			return it;
		}

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

			// 1. Eligibility.
			Guard("mountable probes", () =>
			{
				foreach (int id in MustMount) Check(DroneArsenal.IsMountable(Make(id)), $"{Make(id).Name} should be mountable");
				foreach (int id in MustRefuse) Check(!DroneArsenal.IsMountable(Make(id)), $"{Make(id).Name} should be refused");
				Check(!DroneArsenal.IsMountable(new Item()), "an empty item must not be mountable");
			});

			// 2. Ammo and projectile resolution, and the shot recipe.
			Guard("ammo defaults", () =>
			{
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Bullet) == ItemID.MusketBall, "default bullet is the Musket Ball");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Arrow) == ItemID.WoodenArrow, "default arrow is the Wooden Arrow");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Rocket) > 0, "rockets have a default round");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Gel) > 0, "gel has a default round");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Dart) > 0, "darts have a default round");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.None) == 0, "no ammo kind has no default round");
			});
			Guard("shot recipes", () =>
			{
				DroneShot musket = DroneArsenal.BuildShot(Make(ItemID.Musket), null);
				Check(musket.Valid && musket.Projectile == ProjectileID.Bullet && musket.BaseDamage > Make(ItemID.Musket).damage, "Musket + default round fires a Bullet for weapon + round damage");
				DroneShot bow = DroneArsenal.BuildShot(Make(ItemID.WoodenBow), null);
				Check(bow.Valid && bow.Projectile == ProjectileID.WoodenArrowFriendly && bow.Gravity, "Wooden Bow fires an arrow that drops");
				Item staff = Make(ItemID.AmethystStaff);
				DroneShot cast = DroneArsenal.BuildShot(staff, null);
				Check(cast.Valid && cast.Projectile == staff.shoot && cast.BaseDamage == staff.damage, "Amethyst Staff fires its own bolt for its own damage");
				Check(DroneArsenal.BuildShot(Make(ItemID.Boomstick), null).Count == 3, "Boomstick fires its three-pellet fan");
				DroneShot rocket = DroneArsenal.BuildShot(Make(ItemID.RocketLauncher), null);
				Check(rocket.Valid && rocket.Projectile == ProjectileID.RocketI, "Rocket Launcher + Rocket I fires a Rocket I");
				DroneShot flame = DroneArsenal.BuildShot(Make(ItemID.Flamethrower), null);
				Check(flame.Valid && flame.Projectile == ProjectileID.Flames, "Flamethrower fires flames");
				DroneShot prism = DroneArsenal.BuildShot(Make(ItemID.LastPrism), null);
				Check(prism.Valid && prism.StandIn && prism.Projectile == LevyConstants.Drones.HeldStandInProjectile && prism.IntervalTicks == LevyConstants.HeldRealCadence[ItemID.LastPrism],
					"Last Prism is an approximate stand-in bolt at its real cadence");
				DroneShot phantasm = DroneArsenal.BuildShot(Make(ItemID.Phantasm), null);
				Check(phantasm.Valid && phantasm.Count == 4 && phantasm.Gravity, "Phantasm fires its four-arrow volley as plain arrows");
				Check(!DroneArsenal.BuildShot(Make(ItemID.CopperBroadsword), null).Valid, "a blade has no drone shot");
				Check(DroneArsenal.MaxHpFor(ItemID.LastPrism) > DroneArsenal.MaxHpFor(ItemID.WoodenBow), "a better weapon makes a tougher drone");
				Check(DroneArsenal.MaxHpFor(ItemID.LastPrism) <= LevyConstants.Drones.HpMax, "drone life stays under its cap");
			});

			// 3. The Armory's loan ledger, on a detached Armory.
			Guard("loan ledger", () => CheckLedger(Check));

			// 4. The Engineering guide: wired through the study path, with its text.
			Guard("engineering guide", () =>
			{
				Check(GuideSystem.IsGuideKey("Engineering") && FlavorSystem.IsValidKey("Engineering"), "Engineering is a guide key the study path accepts");
				Check(ModContent.ItemType<GuideToEngineering>() > 0, "the Engineering book item is registered");
				Check(ModContent.NPCType<DroneNPC>() > 0, "the drone NPC is registered");
				foreach (string key in new[] { "Items.GuideToEngineering.DisplayName", "Items.GuideToEngineering.Tooltip", "NPCs.DroneNPC.DisplayName", "Flavor.Effect.Engineering" })
					Check(Language.Exists("Mods.Levy." + key), "localization key Mods.Levy." + key + " exists");
			});

			// 5. Coverage across every vanilla weapon: classification never throws, and a count of what a drone can carry.
			int mountable = 0, weapons = 0;
			Guard("vanilla sweep", () =>
			{
				foreach (KeyValuePair<int, Item> kv in ContentSamples.ItemsByType)
				{
					Item s = kv.Value;
					if (s == null || s.IsAir || kv.Key >= ItemID.Count || s.damage <= 0 || s.shoot <= ProjectileID.None) continue;
					weapons++;
					if (!DroneArsenal.IsMountable(s)) continue;
					mountable++;
					DroneShot shot = DroneArsenal.BuildShot(s, null);
					if (!shot.Valid) failures.Add($"{s.Name} is mountable but has no valid shot");
				}
			});

			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy drone self-check: {ok} checks OK; {mountable} of {weapons} vanilla projectile weapons are drone-mountable");
				else
				{
					Mod.Logger.Warn($"Levy drone self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
					foreach (string f in failures) Mod.Logger.Warn("Levy drone self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
		}

		private static void CheckLedger(Action<bool, string> check)
		{
			var armory = new ArmoryTE();
			armory.Storage[0] = Make(ItemID.Musket);
			armory.Storage[1] = Make(ItemID.WoodenBow);
			armory.Storage[2] = Make(ItemID.AmethystStaff);
			armory.Storage[3] = Make(ItemID.CopperBroadsword);
			armory.Storage[4] = Make(ItemID.MusketBall, 50);
			armory.Storage[5] = Make(ItemID.SilverBullet, 5); // under the keep line: never lent

			// Soldiers get first pick: a reserve as big as the shelf lends nothing; a reserve of one never lends the very best weapon.
			check(armory.LendDroneWeapon(4) == null, "a reserve covering every weapon on the racks lends nothing");
			Item best = null; float bestScore = float.NegativeInfinity;
			foreach (Item it in armory.Storage) if (it != null && !it.IsAir && WeaponClassifier.IsIssuableWeapon(it) && WeaponClassifier.Score(it) > bestScore) { best = it; bestScore = WeaponClassifier.Score(it); }
			Item first = armory.LendDroneWeapon(1);
			check(first != null && !ReferenceEquals(first, best) && armory.IsLent(first), "with a reserve of one, a drone never takes the army's best weapon");
			if (first != null) armory.ReturnLent(first);
			check(armory.Lent.Count == 0, "returning the loan empties the ledger");

			// Lend all three mountable weapons (the blade stays), and return them.
			var lent = new List<Item>();
			for (int i = 0; i < 5; i++) { Item w = armory.LendDroneWeapon(0); if (w == null) break; lent.Add(w); }
			check(lent.Count == 3, "three mountable weapons lent, never the blade (lent " + lent.Count + ")");
			foreach (Item w in lent) check(DroneArsenal.IsMountable(w) && armory.IsLent(w) && IndexIn(armory.Storage, w) < 0, "a lent weapon left the racks and is on the ledger");

			// The ledger survives a save: the weapons come back on the racks, once each, and the ledger is empty after the load.
			var tag = new TagCompound();
			armory.SaveData(tag);
			var reloaded = new ArmoryTE();
			reloaded.LoadData(tag);
			int found = 0;
			foreach (Item it in reloaded.Storage) if (it != null && !it.IsAir && DroneArsenal.IsMountable(it)) found++;
			check(found == 3 && reloaded.Lent.Count == 0, "a save with three weapons on loan reloads them on the racks, once each (found " + found + ")");

			// Ammo: one round of the best stack above the keep line, borrowed and returned whole.
			Item round = armory.LendDroneAmmo(AmmoID.Bullet, LevyConstants.Drones.AmmoKeepRounds);
			check(round != null && round.type == ItemID.MusketBall && round.stack == 1, "a drone borrows one round of the stack that can spare it");
			int ballsLeft = 0; foreach (Item it in armory.Storage) if (it != null && !it.IsAir && it.type == ItemID.MusketBall) ballsLeft += it.stack;
			check(ballsLeft == 49, "the borrowed round left the stack (" + ballsLeft + " left)");
			if (round != null) armory.ReturnLent(round);
			ballsLeft = 0; foreach (Item it in armory.Storage) if (it != null && !it.IsAir && it.type == ItemID.MusketBall) ballsLeft += it.stack;
			check(ballsLeft == 50 && armory.Lent.Count == 3, "the returned round rejoins its stack (" + ballsLeft + " balls, " + armory.Lent.Count + " on loan)");
			check(armory.ReturnLent(lent[0]) && !armory.ReturnLent(lent[0]), "a loan can be returned once, never twice");

			// Soldiers first, end to end: every usable weapon goes out on loan again, then a soldier with an empty hand is issued gear. He gets a weapon, and one drone is left short.
			int outAgain = 0;
			while (armory.LendDroneWeapon(0) != null && outAgain < 8) outAgain++;
			for (int i = 0; i < armory.Storage.Length; i++) // the blade is not mountable, so it stayed on the racks: take it away, so the racks hold nothing a soldier could draw
				if (armory.Storage[i] != null && !armory.Storage[i].IsAir && WeaponClassifier.IsIssuableWeapon(armory.Storage[i])) armory.Storage[i] = new Item();
			check(outAgain >= 1 && armory.Lent.Count == 3, "all three weapons are on loan before the soldier asks (" + armory.Lent.Count + ")");
			var recruit = new SoldierRecord { Kind = SoldierKind.Militia };
			check(!recruit.Gear.HasWeapon, "the test soldier starts bare-handed");
			armory.IssueGear(recruit);
			check(recruit.Gear.HasWeapon, "a soldier with an empty hand recalls a drone's weapon when the racks have none");
			check(armory.Lent.Count == 2, "exactly one loan was recalled for him (" + armory.Lent.Count + " left)");
		}

		private static int IndexIn(Item[] arr, Item it) { for (int i = 0; i < arr.Length; i++) if (ReferenceEquals(arr[i], it)) return i; return -1; }
	}
}
