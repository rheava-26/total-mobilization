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
	/// Boot self-check of the tier-aware Balanced ranking (<see cref="WeaponRank"/> bands and <see cref="LevyFit"/>), on the REAL progression table: in a post-Moon Lord world every working Moon Lord drop outranks every
	/// post-Golem weapon (Golem to the Lunatic Cultist, Calamity's Plaguebringer, Ravager and Astrum Deus when loaded), even a freshly deposited one; a weapon measured broken in levy hands ranks 0 and an armory
	/// never issues it while a working one is on the shelf; WEAK loses a band; the measured table resolves. Logs OBSERVED before/after picks per class at progress 18. Throwaway armories and levies only.
	/// </summary>
	public class TierRankCheck : ModSystem
	{
		private static Item Make(int type) { var it = new Item(); it.SetDefaults(type); return it; }
		private static string N(Item i) => i == null || i.IsAir ? "-" : (i.ModItem != null ? i.ModItem.Mod.Name + "/" + i.ModItem.Name : i.Name);

		/// <summary>Moon Lord's own weapon drops (vanilla).</summary>
		public static readonly int[] MoonLordDrops = { ItemID.Meowmere, ItemID.StarWrath, ItemID.Terrarian, ItemID.SDMG, ItemID.Celeb2, ItemID.LastPrism, ItemID.LunarFlareBook, ItemID.RainbowCrystalStaff, ItemID.MoonlordTurretStaff };

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(Core);

		private void Core()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) return;
			var failures = new List<string>(); int ok = 0;
			void Check(bool c, string what) { if (c) ok++; else failures.Add(what); }
			int pg = 0, ml = 0;
			try
			{
				MaterialCost.Ensure();
				ProgressionTier.EnsureBuilt(s => { });
				Check(ProgressionTier.Ready, "the progression table is built");
				// the measured table: every vanilla row resolves, and every row of a loaded mod
				Check(LevyFit.Count > 0 && LevyFit.Unresolved.Count == 0, $"the measured levy table resolves ({LevyFit.Count} weapons; unresolved: {string.Join(", ", LevyFit.Unresolved)})");
				Check(LevyFit.StatusOf(ItemID.Meowmere) == FitStatus.Ok && LevyFit.StatusOf(ItemID.Terrarian) == FitStatus.Weak, "Meowmere reads OK and the Terrarian WEAK from the table");

				WeaponRank.Force(WeaponRankMode.Balanced, false); WeaponRank.ForceProgress = 18f; LevyClock.Force(100 * LevyClock.TicksPerDay);
				long now = LevyClock.Now;
				// every issuable post-Golem weapon of the table, freshly deposited (its best case)
				var postGolem = new List<Item>();
				for (int t = 1; t < ItemLoader.ItemCount; t++)
				{
					float tier = ProgressionTier.Of(t);
					if (tier < 13f || tier >= 18f) continue;
					var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
					if (it.IsAir || it.damage <= 0 || it.maxStack > 1 || it.accessory || !WeaponClassifier.IsIssuableWeapon(it)) continue;
					if (LevyFit.StatusOf(t) == FitStatus.Broken) continue;
					DepositStamp.Set(it, now);
					postGolem.Add(it);
				}
				pg = postGolem.Count;
				Check(pg >= 20, "the table has post-Golem weapons to compare (" + pg + ")");
				var top = postGolem.OrderByDescending(WeaponRank.Of).FirstOrDefault();
				float topRank = top == null ? 0f : WeaponRank.Of(top);
				foreach (int type in MoonLordDrops)
				{
					var drop = Make(type); // never stamped: an old deposit, its worst case
					Check(ProgressionTier.Of(type) >= 18f, $"{N(drop)} is a Moon Lord tier weapon (tier {ProgressionTier.Of(type):0.##})");
					var st = LevyFit.StatusOf(type);
					if (st == FitStatus.Weak || st == FitStatus.Broken) continue;
					ml++;
					float r = WeaponRank.Of(drop);
					var beat = postGolem.Where(p => WeaponRank.Of(p) >= r).Select(p => $"{N(p)} {WeaponRank.Of(p):0}").Take(4).ToList();
					Check(beat.Count == 0, $"Moon Lord drop {N(drop)} (rank {r:0}, an old deposit) outranks every post-Golem weapon, even fresh ones (beaten by: {string.Join(", ", beat)})");
					Check(r > topRank / 0.75f, $"{N(drop)} puts the best post-Golem weapon ({N(top)} {topRank:0}) out of the 0.75 variety band");
				}
				// WEAK loses a band: the Terrarian ranks a quarter of its own working rank times its ratio
				var terr = Make(ItemID.Terrarian);
				var bdT = WeaponRank.Explain(terr);
				Check(bdT.FitStatus == FitStatus.Weak && Math.Abs(bdT.Fit - LevyFit.WeakFactor(LevyFit.Of(ItemID.Terrarian).Ratio) / WeaponRank.BandLift) < 1e-4f, "a WEAK weapon loses one band and is scaled by its ratio (Terrarian fit x" + bdT.Fit.ToString("0.###") + ")");
				// bands: one band behind is a quarter, the band edges are where they should be
				Check(WeaponRank.Band(17.5f) == WeaponRank.Band(13f) && WeaponRank.Band(18f) == WeaponRank.Band(13f) + 1 && WeaponRank.Band(8.9f) == 0 && WeaponRank.Band(23f) == WeaponRank.BandEdges.Length, "band edges: Golem to Astrum Deus one band, Moon Lord the next, nothing before the mechanical bosses");
				Check(Math.Abs(WeaponRank.BandFactor(13.5f) - 1f / WeaponRank.BandLift) < 1e-5f && WeaponRank.BandFactor(18.3f) == 1f, "at progress 18 a post-Golem weapon is lifted by 1/" + WeaponRank.BandLift + ", a Moon Lord weapon by 1");
				// the pick itself: an armory with an old Meowmere and the best fresh post-Golem melee weapon arms a levy with the Meowmere
				var bestMelee = postGolem.Where(p => WeaponClassifier.ItemDamageClass(p) == DamageClass.Melee || WeaponClassifier.ItemDamageClass(p) == DamageClass.MeleeNoSpeed).OrderByDescending(WeaponRank.Of).FirstOrDefault();
				if (bestMelee != null)
				{
					var got = Arm(new[] { Make(ItemID.Meowmere), bestMelee.Clone() });
					Check(got?.type == ItemID.Meowmere, $"a levy offered an old Meowmere and a fresh {N(bestMelee)} takes the Meowmere (took {N(got)})");
				}

				// BROKEN: a measured-broken weapon ranks 0 and is never issued while a working one is there (a made-up table marks Meowmere broken)
				var fake = new Dictionary<int, LevyFit.Entry> { [ItemID.Meowmere] = new LevyFit.Entry { Status = FitStatus.Broken }, [ItemID.TerraBlade] = new LevyFit.Entry { Status = FitStatus.Ok, Ratio = 0.9f, Dps = 286 } };
				LevyFit.SetForTest(fake);
				try
				{
					var mm = Make(ItemID.Meowmere);
					Check(WeaponRank.Of(mm) == 0f && WeaponRank.Explain(mm).Broken, "a broken weapon ranks 0 in Balanced");
					foreach (var mode in new[] { WeaponRankMode.SellPrice, WeaponRankMode.SellPricePlusMaterials, WeaponRankMode.Damage })
					{
						WeaponRank.Force(mode, false);
						Check(WeaponRank.Of(mm) == 0f, "a broken weapon ranks 0 in " + mode);
					}
					WeaponRank.Force(WeaponRankMode.Balanced, false);
					var got = Arm(new[] { Make(ItemID.Meowmere), Make(ItemID.TerraBlade) });
					Check(got?.type == ItemID.TerraBlade, "a broken Moon Lord drop is passed over for a working Terra Blade (took " + N(got) + ")");
					var only = Arm(new[] { Make(ItemID.Meowmere) });
					Check(only == null, "a broken weapon alone on the shelf is not issued (took " + N(only) + ")");
				}
				finally { LevyFit.SetForTest(null); }
				// the real table: every BROKEN row of a loaded mod ranks 0
				foreach (var kv in Enumerable.Range(1, ItemLoader.ItemCount - 1).Where(t => LevyFit.StatusOf(t) == FitStatus.Broken))
					Check(WeaponRank.Of(Make(kv)) == 0f, N(Make(kv)) + " (measured broken) ranks 0");

				Observe(postGolem);
			}
			catch (Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim()))); }
			finally { WeaponRank.ForceProgress = -1f; WeaponRank.TierAware = true; WeaponRank.Release(); LevyClock.Force(null); ArmoryTE.TestServed = null; ArmoryTE.TestOthers = null; LevyFit.SetForTest(null); }
			if (failures.Count == 0) Mod.Logger.Info($"Levy tier ranking self-check: {ok} checks OK ({ml} working Moon Lord drops against {pg} post-Golem weapons, {LevyFit.Count} measured weapons)");
			else { Mod.Logger.Warn($"Levy tier ranking self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("Levy tier ranking self-check FAILED: " + f); }
		}

		/// <summary>One bare levy armed from a throwaway armory holding <paramref name="stock"/>: what he ends up holding (null = nothing).</summary>
		private static Item Arm(IEnumerable<Item> stock)
		{
			var te = new ArmoryTE();
			ArmoryTE.TestOthers = null;
			foreach (var it in stock) te.Deposit(it);
			var lev = new SoldierRecord { Id = 1, Name = "T1" };
			ArmoryTE.TestServed = new List<SoldierRecord> { lev };
			try
			{
				Bump(1); te.IssueGear(lev);
				for (int round = 0; round < 3; round++) { Bump(1); te.UpgradeWeapons(lev); }
				return lev.Gear.HasWeapon ? lev.Gear.Weapon : null;
			}
			finally { ArmoryTE.TestServed = null; }
		}

		/// <summary>OBSERVED: the top pick per class among Moon Lord drops and post-Golem weapons (Calamity's too when loaded) at progress 18, before the tier bands and the measured table and after.</summary>
		private void Observe(List<Item> postGolem)
		{
			var pool = postGolem.Select(p => p.Clone()).Concat(MoonLordDrops.Select(Make)).ToList();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				float tier = ProgressionTier.Of(t); if (tier < 18f || tier >= 19f) continue; // Calamity's Moon Lord band (Profaned Guardians, Dragonfolly, Luminite crafts)
				var it = new Item(); try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || it.damage <= 0 || it.maxStack > 1 || it.accessory || !WeaponClassifier.IsIssuableWeapon(it)) continue;
				pool.Add(it);
			}
			foreach (var cls in new[] { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon })
			{
				var of = pool.Where(p => { var c = WeaponClassifier.ItemDamageClass(p); return c == cls || (cls == DamageClass.Melee && c == DamageClass.MeleeNoSpeed); }).ToList();
				if (of.Count == 0) continue;
				WeaponRank.TierAware = false;
				var before = of.OrderByDescending(WeaponRank.Of).Take(3).Select(p => $"{N(p)} (tier {ProgressionTier.Of(p.type):0.#}, {WeaponRank.Of(p):0})").ToList();
				WeaponRank.TierAware = true;
				var after = of.OrderByDescending(WeaponRank.Of).Take(3).Select(p => $"{N(p)} (tier {ProgressionTier.Of(p.type):0.#}, {WeaponRank.Of(p):0}, {LevyFit.StatusOf(p.type)})").ToList();
				Mod.Logger.Info($"[TierRank] OBSERVED {cls.Name} at progress 18, post-Golem stock deposited fresh: before {string.Join(" > ", before)} | after {string.Join(" > ", after)}");
			}
		}

		private static void Bump(uint n)
		{
			var f = typeof(Main).GetField("_gameUpdateCount", BindingFlags.NonPublic | BindingFlags.Static);
			f?.SetValue(null, Main.GameUpdateCount + n);
		}
	}
}
