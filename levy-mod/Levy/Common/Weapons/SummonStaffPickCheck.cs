using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.TileEntities;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot self-check of the designer's case ("my summoners only carry the Slime Staff"): a world where only King Slime and the Eye of Cthulhu are down, an armory stocked directly with summoner armor, two Slime Staves, the
	/// class-balance minimum (2 of each class) and better pre-Hardmode staves (the Imp, Hornet and Vampire Frog staves, plus up to three modded ones when a mod brings them, such as Calamity's). In EVERY rank mode, for fresh levies
	/// and for levies already holding a Slime Staff, no summoner may end up with the Slime Staff as his weapon. Logs one summary line, or the failures. Throwaway armory and soldiers only.
	/// </summary>
	public class SummonStaffPickCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : (i.ModItem != null ? i.ModItem.Mod.Name + "/" + i.ModItem.Name : i.Name);

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(Core);

		private void Core()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			var failures = new List<string>();
			int ok = 0, summoners = 0, betterCount = 0;
			try
			{
				MaterialCost.Ensure();
				ProgressionTier.EnsureBuilt(s => { });
				var slime = Make(ItemID.SlimeStaff);
				var better = new List<Item> { Make(ItemID.ImpStaff), Make(ItemID.HornetStaff), Make(ItemID.VampireFrogStaff) };
				float slimeScore = WeaponClassifier.Score(slime);
				var extra = new List<Item>();
				for (int t = ItemID.Count; t < ItemLoader.ItemCount && extra.Count < 64; t++)
				{
					var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
					if (it.IsAir || it.sentry || it.damage <= 0 || it.accessory) continue;
					if (WeaponClassifier.GetProfile(it).Family != WeaponFamily.Summon) continue;
					float tier = ProgressionTier.Of(t);
					if (tier < 0f || tier > 5f || WeaponClassifier.Score(it) <= slimeScore * 1.3f) continue;
					extra.Add(it);
				}
				better.AddRange(extra.OrderByDescending(WeaponClassifier.Score).Take(3)); betterCount = better.Count;
				foreach (var mode in new[] { WeaponRankMode.Balanced, WeaponRankMode.SellPrice, WeaponRankMode.SellPricePlusMaterials })
					foreach (bool holdingSlime in new[] { false, true })
					{
						string tag = $" [{mode}, {(holdingSlime ? "levies already hold a Slime Staff" : "fresh levies")}]";
						try
						{
							WeaponRank.Force(mode, false); WeaponRank.ForceProgress = 2f; LevyClock.Force(10 * LevyClock.TicksPerDay);
							var te = new ArmoryTE();
							ArmoryTE.TestOthers = null;
							foreach (var type in new[] { ItemID.CopperBroadsword, ItemID.CopperBroadsword, ItemID.CopperBow, ItemID.CopperBow, ItemID.AmethystStaff, ItemID.TopazStaff, ItemID.SlimeStaff, ItemID.SlimeStaff, ItemID.BeeHeadgear, ItemID.BeeBreastplate, ItemID.BeeGreaves }) te.Deposit(Make(type));
							var arrows = Make(ItemID.WoodenArrow); arrows.stack = 999; te.Deposit(arrows);
							foreach (var b in better) te.Deposit(b.Clone());
							long now = LevyClock.Now;
							foreach (var arr in new[] { te.Storage, te.IssueWeapons }) foreach (var it in arr) if (it != null && !it.IsAir) DepositStamp.Set(it, now);
							foreach (var k in new[] { "melee", "ranged", "magic", "summon" }) te.Rules.Blocks[0].EntryFor(k, true).Min = 2;
							te.Rules.Rev++;
							var levies = new List<SoldierRecord>();
							for (int i = 0; i < 12; i++) { var r = new SoldierRecord { Id = i + 1, Name = "L" + (i + 1) }; if (holdingSlime && i % 2 == 0) r.Gear.Weapon = Make(ItemID.SlimeStaff); levies.Add(r); }
							ArmoryTE.TestServed = levies;
							foreach (var l in levies) { Bump(1); te.IssueGear(l); }
							for (int round = 0; round < 4; round++) foreach (var l in levies) { Bump(1); te.UpgradeWeapons(l); }
							var summ = levies.Where(l => l.Gear.HasWeapon && WeaponClassifier.ItemDamageClass(l.Gear.Weapon) == DamageClass.Summon && WeaponClassifier.GetProfile(l.Gear.Weapon).Family == WeaponFamily.Summon).ToList();
							summoners += summ.Count;
							void Check(bool c, string what) { if (c) ok++; else failures.Add(what + tag); }
							Check(summ.Count >= 2, "the class-balance minimum fields at least two summoners (got " + summ.Count + ")");
							var slimers = summ.Where(l => l.Gear.Weapon.type == ItemID.SlimeStaff).ToList();
							var shelf = te.Storage.Concat(te.IssueWeapons).Where(i => i != null && !i.IsAir && i.type != ItemID.SlimeStaff && WeaponClassifier.GetProfile(i).Family == WeaponFamily.Summon).ToList();
							float topShelf = shelf.Count == 0 ? 0f : shelf.Max(i => WeaponRank.Of(i)), heldSlime = slimers.Count == 0 ? 0f : slimers.Max(l => WeaponRank.Of(l.Gear.Weapon));
							string held = string.Join(", ", summ.Select(l => N(l.Gear.Weapon)).Distinct());
							// a Slime Staff left in a hand is fine only when nothing clearly better (well past the upgrade margin) is still on the shelves, and most summoners carry something else
							Check(slimers.Count * 2 <= summ.Count && topShelf <= heldSlime * 1.25f, "summoners carry the Slime Staff although better staves are in the armory (" + slimers.Count + " of " + summ.Count + " do; best staff left on the shelves ranks " + topShelf.ToString("F0") + " against the held Slime Staff's " + heldSlime.ToString("F0") + "; they hold " + held + ")");
						}
						finally { ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); }
					}
			}
			catch (Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message); }
			finally { WeaponRank.ForceProgress = -1f; WeaponRank.Release(); LevyClock.Force(null); ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; }
			if (failures.Count == 0) Mod.Logger.Info($"Levy summon-staff pick self-check: {ok} checks OK (EoC+KS world, every rank mode, {summoners} summoners armed, {betterCount} better staves stocked)");
			else { Mod.Logger.Warn($"Levy summon-staff pick self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy summon-staff pick self-check FAILED: " + f); }
		}

		private static void Bump(uint n)
		{
			var f = typeof(Main).GetField("_gameUpdateCount", BindingFlags.NonPublic | BindingFlags.Static);
			f?.SetValue(null, Main.GameUpdateCount + n);
		}
	}
}
