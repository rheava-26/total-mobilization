using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.UI;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot self-check of THE ARMY NEVER ALTERS THE WORLD (no world needed; the measured half is TileSweepCheck). Logs "Levy world-alter self-check: pass=N fail=M". It checks that the classifier flags the canonical
	/// breakers (explosives, tile-breaking rounds, pick / hammer / drill) and nothing ordinary, that no pick path can select a flagged item (weapon classifier, hand assignment, allocation, sidearms, ammo, drone and turret
	/// arsenal, lent rounds), that a kit made before the rule is emptied of them, that the deposit warning fires for exactly the flagged items and honours "don't show again", and that the identity guard's talk mirror
	/// writes the field and not SetTalkNPC.
	/// </summary>
	public class AlterGuardCheck : ModSystem
	{
		private static Item Make(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : i.Name + (i.stack > 1 ? "x" + i.stack : "");

		public override void PostSetupRecipes()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guarded(string name, Action body) { try { body(); } catch (Exception e) { failures.Add(name + " threw " + e.GetType().Name + ": " + e.Message); } }
			bool verbose = Environment.GetEnvironmentVariable("LEVY_ARMORYPROBE") == "1";
			void Log(string s) { if (verbose) Mod.Logger.Info("[AlterGuard] " + s); }
			if (WorldAlter.Disabled) { Mod.Logger.Info("Levy world-alter self-check: skipped (LEVY_WORLDALTER=0)"); return; }
			WorldAlter.EnsureLoaded();
			Check(WorldAlter.TableLines.TryGetValue('S', out int safeLines) && safeLines > 100, "the shipped table is present (" + (WorldAlter.TableLines.TryGetValue('S', out int sl) ? sl : 0) + " safe-item lines)");

			int rocketII = ItemID.RocketII, rocketIV = ItemID.RocketIV, miniNukeII = ItemID.MiniNukeII;
			Guarded("classification", () =>
			{
				foreach (int t in new[] { ItemID.Dynamite, ItemID.Bomb, ItemID.StickyBomb, ItemID.IronPickaxe, ItemID.Pwnhammer, ItemID.Drax, ItemID.CobaltChainsaw })
					Check(WorldAlter.Alters(Make(t, 5)), Make(t).Name + " is flagged");
				foreach (int t in new[] { rocketII, rocketIV, miniNukeII })
					Check(WorldAlter.Alters(Make(t, 50)), Make(t).Name + " is flagged as a round");
				foreach (int t in new[] { ItemID.Musket, ItemID.MusketBall, ItemID.WoodenArrow, ItemID.Minishark, ItemID.RocketLauncher, ItemID.RocketI, ItemID.ThrowingKnife, ItemID.Grenade, ItemID.IronBroadsword, ItemID.WandofSparking, ItemID.CobaltShield })
					Check(!WorldAlter.Alters(Make(t, 5)), Make(t).Name + " is not flagged");
				Check(WorldAlter.AltersWith(Make(ItemID.RocketLauncher), Make(rocketII)) && !WorldAlter.AltersWith(Make(ItemID.RocketLauncher), Make(ItemID.RocketI)), "a Rocket Launcher is fine with Rocket I and not with Rocket II");
				Check(WorldAlter.KindOf(Make(ItemID.Dynamite)) != WorldAlter.Kind.None && WorldAlter.ReasonText(Make(ItemID.Dynamite)) != null && WorldAlter.ReasonText(Make(ItemID.Musket)) == null, "a flagged item has a reason line, an ordinary one has none");
				Check(WorldAlter.IsTool(Make(ItemID.CopperPickaxe)) && WorldAlter.IsTool(Make(ItemID.SolarFlareDrill)) && !WorldAlter.IsTool(Make(ItemID.Musket)), "pick, axe, hammer and drill flags are honoured");
			});
			Guarded("classifier", () =>
			{
				foreach (int t in new[] { ItemID.Dynamite, ItemID.Bomb, ItemID.IronPickaxe, ItemID.Pwnhammer, ItemID.Drax })
					Check(!WeaponClassifier.IsIssuableWeapon(Make(t, 20)), Make(t).Name + " is not an issuable weapon");
				Check(WeaponClassifier.IsIssuableWeapon(Make(ItemID.Musket)) && WeaponClassifier.IsIssuableWeapon(Make(ItemID.ThrowingKnife, 20)), "ordinary weapons are still issuable");
				var g = new SoldierGear();
				Check(!g.CanHold(SoldierGear.SlotWeapon, Make(ItemID.Dynamite, 5)) && !g.CanHold(SoldierGear.SlotSidearm, Make(ItemID.Pwnhammer)) && !g.CanHold(SoldierGear.SlotAmmo, Make(rocketII, 50)), "hand assignment refuses a breaker in a weapon, sidearm or ammo slot");
				Check(g.CanHold(SoldierGear.SlotAmmo, Make(ItemID.RocketI, 50)) && g.CanHold(SoldierGear.SlotWeapon, Make(ItemID.Musket)), "hand assignment still takes ordinary items");
				Check(!ArmoryIntake.Accepts(Make(ItemID.IronPickaxe)) && !ArmoryIntake.Accepts(Make(rocketII, 50)) && ArmoryIntake.TakesByHand(Make(rocketII, 50)), "Deposit junk leaves a breaker alone, a shift-click still takes it (with the warning)");
			});
			Guarded("allocation", () =>
			{
				var te = new ArmoryTE(); int k = 0;
				foreach (var it in new[] { Make(ItemID.RocketLauncher), Make(rocketII, 999), Make(ItemID.RocketI, 300), Make(ItemID.Dynamite, 50), Make(ItemID.Bomb, 50), Make(ItemID.IronPickaxe), Make(ItemID.Pwnhammer), Make(ItemID.Drax),
					Make(ItemID.Musket), Make(ItemID.MusketBall, 999), Make(ItemID.Minishark), Make(ItemID.IronBroadsword), Make(ItemID.WandofSparking), Make(ItemID.ThrowingKnife, 100) })
					te.Storage[k++] = it;
				var list = Enumerable.Range(300, 5).Select(i => new SoldierRecord { Id = i }).ToList(); ArmoryTE.TestServed = list;
				try { foreach (var s in list) te.IssueGear(s); foreach (var s in list) te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				var bad = new List<string>();
				foreach (var s in list)
				{
					foreach (var it in new[] { s.Gear.Weapon, s.Gear.Sidearm }.Concat(s.Gear.ExtraSidearms)) if (it != null && !it.IsAir && WorldAlter.Alters(it)) bad.Add(N(it));
					Item w = s.Gear.HasWeapon ? s.Gear.Weapon : null;
					if (s.Gear.Ammo != null && !s.Gear.Ammo.IsAir && WorldAlter.AltersWith(w, s.Gear.Ammo)) bad.Add(N(s.Gear.Ammo));
				}
				Log("issued: " + string.Join(" | ", list.Select(s => N(s.Gear.Weapon) + "/" + N(s.Gear.Sidearm) + "/" + N(s.Gear.Ammo))));
				Check(bad.Count == 0, "no levy is issued a breaker (found: " + string.Join(", ", bad) + ")");
				Check(list.All(s => s.Gear.HasWeapon), "every levy is still armed from the safe stock");
				var launcher = list.FirstOrDefault(s => s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.RocketLauncher);
				Check(launcher == null || launcher.Gear.Ammo == null || launcher.Gear.Ammo.IsAir || launcher.Gear.Ammo.type == ItemID.RocketI, "a Rocket Launcher levy, if any, carries Rocket I and never Rocket II (" + N(launcher?.Gear.Ammo) + ")");
				Check(te.CountInStorage(ItemID.Dynamite) == 50 && te.CountInStorage(ItemID.Bomb) == 50 && te.CountInStorage(rocketII) == 999, "the breakers stay on the shelf, none lost");
				// lent rounds
				var lend = new ArmoryTE(); lend.Storage[0] = Make(rocketII, 100);
				Check(lend.LendDroneAmmo(AmmoID.Rocket, 0) == null, "a drone is lent no Rocket II when it is all there is");
				lend.Storage[1] = Make(ItemID.RocketI, 100);
				var lent = lend.LendDroneAmmo(AmmoID.Rocket, 0);
				Check(lent != null && lent.type == ItemID.RocketI, "a drone is lent Rocket I past the Rocket II stack (" + N(lent) + ")");
				// drone and turret arsenal
				Check(!DroneArsenal.IsMountable(Make(ItemID.Dynamite)) && !DroneArsenal.IsMountable(Make(ItemID.Pwnhammer)), "a breaker is never mounted on a drone or turret");
				var shot = DroneArsenal.BuildShot(Make(ItemID.RocketLauncher), Make(rocketII));
				Check(!shot.Valid || !WorldAlter.AltersProjectile(shot.Projectile), "a drone with a Rocket Launcher and a lent Rocket II fires a safe shot (valid " + shot.Valid + ", projectile " + shot.Projectile + ")");
				Check(DroneArsenal.DefaultAmmoType(AmmoID.Rocket) == 0 || !WorldAlter.Alters(Make(DroneArsenal.DefaultAmmoType(AmmoID.Rocket))), "the default round of a rocket drone is not a breaker (" + Lang.GetItemNameValue(DroneArsenal.DefaultAmmoType(AmmoID.Rocket)) + ")");
			});
			Guarded("purge", () =>
			{
				var te = new ArmoryTE(); te.Storage[0] = Make(ItemID.Musket); te.Storage[1] = Make(ItemID.MusketBall, 500);
				var s = new SoldierRecord { Id = 400 };
				s.Gear.Weapon = Make(ItemID.Dynamite, 5); s.Gear.Sidearm = Make(ItemID.Pwnhammer); s.Gear.Ammo = Make(rocketII, 50);
				ArmoryTE.TestServed = new List<SoldierRecord> { s };
				try { te.IssueGear(s); } finally { ArmoryTE.TestServed = null; }
				Check(!WorldAlter.Alters(s.Gear.Weapon) && !WorldAlter.Alters(s.Gear.Sidearm) && !WorldAlter.Alters(s.Gear.Ammo), "a kit made before the rule is emptied of breakers (now " + N(s.Gear.Weapon) + " / " + N(s.Gear.Sidearm) + " / " + N(s.Gear.Ammo) + ")");
				Check(te.CountInStorage(ItemID.Dynamite) == 5 && te.CountInStorage(ItemID.Pwnhammer) == 1 && te.CountInStorage(rocketII) == 50, "what was taken back is filed in storage, not lost");
				Check(s.Gear.HasWeapon && s.Gear.Weapon.type == ItemID.Musket, "he is armed with an ordinary weapon after it");
			});
			Guarded("warning", () =>
			{
				var items = new[] { Make(ItemID.Musket), Make(ItemID.Dynamite, 3), Make(rocketII, 100), Make(ItemID.Dynamite, 1) };
				var all = AlterWarning.Collect(items, null);
				Check(all.Count == 2 && all.Any(e => e.Type == ItemID.Dynamite) && all.Any(e => e.Type == rocketII) && all.All(e => !string.IsNullOrEmpty(e.Reason)), "the deposit warning lists exactly the breakers, once each, with a reason (" + string.Join(", ", all.Select(e => e.Name)) + ")");
				var off = new HashSet<string> { WorldAlter.KeyOf(ItemID.Dynamite) };
				var rest = AlterWarning.Collect(items, off);
				Check(rest.Count == 1 && rest[0].Type == rocketII, "an item marked \"don't show again\" is left out of the warning");
				Check(AlterWarning.Collect(new[] { Make(ItemID.Musket), Make(ItemID.IronBroadsword), Make(ItemID.MusketBall, 50) }, null).Count == 0, "ordinary items raise no warning");
				AlterWarning.Pending.Clear(); int opened = AlterWarning.Opened;
				AlterWarning.NoteFor(items, null);
				Check(AlterWarning.Active && AlterWarning.Pending.Count == 2 && AlterWarning.Opened == opened + 1, "a deposit of breakers opens the warning (pending " + AlterWarning.Pending.Count + ")");
				AlterWarning.Dismiss(false);
				Check(!AlterWarning.Active, "clicking OK closes it");
				AlterWarning.NoteFor(new[] { Make(ItemID.Musket) }, null);
				Check(!AlterWarning.Active, "a deposit of ordinary items opens nothing");
			});
			Guarded("mirror", () =>
			{
				var p = new Player(); p.whoAmI = 255;
				long cheap = TalkMirror.Cheap;
				TalkMirror.Set(p, 7); bool a = p.talkNPC == 7; TalkMirror.Set(p, -1); bool b = p.talkNPC == -1;
				Check(TalkMirror.Direct && a && b && TalkMirror.Cheap == cheap + 2, "the identity guard's talk mirror writes the field (direct " + TalkMirror.Direct + ")");
			});
			Mod.Logger.Info($"Levy world-alter table: {string.Join(", ", WorldAlter.TableLines.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value))}; items and projectiles classified from compiled code so far: {WorldAlter.Scanned} ({WorldAlter.ScannedAltering} altering) in {WorldAlter.ScanTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F1} ms");
			if (failures.Count == 0) Mod.Logger.Info($"Levy world-alter self-check: pass={ok} fail=0");
			else { Mod.Logger.Warn($"Levy world-alter self-check: pass={ok} fail={failures.Count} FAILURE(S) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy world-alter self-check FAILURE: " + f); }
		}
	}
}
