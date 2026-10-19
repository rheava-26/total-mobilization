using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time wiring self-check. Catches a "defined but never dispatched" weapon family at load instead of in play: every <see cref="WeaponFamily"/>
	/// must be declared fired or not-fired in <see cref="WeaponUse"/>, and one representative vanilla item per fired family must classify to that family
	/// with a built (non-default) range. Inspect-and-log only: it changes no state the game reads (it only warms the per-type profile cache with the
	/// same value GetProfile would compute on first use). Runs in PostAddRecipes so every mod's PostSetupContent registrations are done first.
	/// </summary>
	public class WeaponWiringCheck : ModSystem
	{
		/// <summary>Set to true to throw on any wiring gap (loud failure in a dev build). Default off: a gap logs a warning and the mod still loads.</summary>
		private const bool ThrowOnFailure = false;

		/// <summary>One stable vanilla probe per fired family. rangedReach = the profile must have a built reach, not the 3-tile melee default.</summary>
		private static readonly (WeaponFamily family, int itemId, bool rangedReach)[] Probes =
		{
			(WeaponFamily.Gun, ItemID.Musket, true),
			(WeaponFamily.Bow, ItemID.WoodenBow, true),
			(WeaponFamily.Magic, ItemID.WaterBolt, true),
			(WeaponFamily.Broadsword, ItemID.CopperBroadsword, false),
			(WeaponFamily.Spear, ItemID.Spear, false),
			(WeaponFamily.Shortsword, ItemID.CopperShortsword, false),
			(WeaponFamily.Whip, ItemID.BlandWhip, false),
			(WeaponFamily.Boomerang, ItemID.EnchantedBoomerang, true),
			(WeaponFamily.Yoyo, ItemID.WoodYoyo, false),
			(WeaponFamily.Flail, ItemID.BallOHurt, false),
			(WeaponFamily.HeldWeapon, ItemID.Phantasm, true),
			(WeaponFamily.Thrown, ItemID.ThrowingKnife, true),
			(WeaponFamily.ThrownMelee, ItemID.DayBreak, true),
		};

		public override void PostAddRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			try
			{
				// 1. Every family is declared exactly once: fired (has a Dispatch case) or deliberately not fired.
				foreach (WeaponFamily f in Enum.GetValues<WeaponFamily>())
				{
					bool fired = WeaponUse.FiredFamilies.Contains(f), notFired = WeaponUse.NotFiredFamilies.Contains(f);
					if (!fired && !notFired) failures.Add($"family {f} is in neither FiredFamilies nor NotFiredFamilies (defined but never dispatched?)");
					else if (fired && notFired) failures.Add($"family {f} is in BOTH FiredFamilies and NotFiredFamilies");
					else ok++;
				}
				foreach (var f in WeaponUse.FiredFamilies)
					if (!Probes.Any(p => p.family == f)) failures.Add($"fired family {f} has no probe item in WeaponWiringCheck.Probes");
			}
			catch (Exception e) { failures.Add("declaration check threw: " + e.Message); }

			// 2. Each fired family's representative item classifies to it and has a built range.
			foreach (var (family, itemId, rangedReach) in Probes)
			{
				try
				{
					var item = new Item();
					item.SetDefaults(itemId);
					if (item.IsAir) { failures.Add($"probe {family}: item id {itemId} did not load"); continue; }
					var profile = WeaponClassifier.GetProfile(item);
					if (profile.Family != family) { failures.Add($"probe {family}: {item.Name} classified as {profile.Family}"); continue; }
					if (profile.RangeTiles <= 0) { failures.Add($"probe {family}: {item.Name} has RangeTiles {profile.RangeTiles}"); continue; }
					if (rangedReach && profile.RangeTiles <= LevyConstants.RangeMelee) { failures.Add($"probe {family}: {item.Name} RangeTiles {profile.RangeTiles} is the melee default (family not built)"); continue; }
				}
				catch (Exception e) { failures.Add($"probe {family}: threw {e.GetType().Name}: {e.Message}"); }
			}

			// 3. One summary line.
			try
			{
				if (failures.Count == 0)
					Mod.Logger.Info($"Levy wiring self-check: {ok} families OK");
				else
				{
					Mod.Logger.Warn($"Levy wiring self-check: {failures.Count} FAILURE(S) ({ok} families OK) !!!");
					foreach (var f in failures) Mod.Logger.Warn("Levy wiring self-check FAILED: " + f);
				}
			}
			catch (Exception) { }
			if (ThrowOnFailure && failures.Count > 0) throw new InvalidOperationException("Levy wiring self-check failed: " + string.Join("; ", failures));
		}
	}
}
