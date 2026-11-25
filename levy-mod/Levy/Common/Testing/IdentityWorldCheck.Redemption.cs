using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Redemption;
using Levy.Common.Soldiers;
using Levy.Common.Weapons;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- REDEMPTION WEAPONS (only when the mod is loaded; LEVY_REDE=0 skips, LEVY_REDE_ALONE=1 runs it alone)
		// On the army sweep's own arena and machinery (one levy against one stand-in player, the pinned enemy): a sample of the mod's weapons, one or more for each way its weapons are made, in a levy's hands against a player's.
		//   * the mod's own damage class is issuable (every weapon of it can be held by a levy);
		//   * for each route below: the levy's damage is at least the route's floor (0.35 to 0.6) of the player's (the player is the better of the button held, a short tap and a long tap), nothing comes out of the commander, nothing throws.
		// Routes: a channel sword (held swing), a tapped hammer, a blade-range dagger of the custom class, an ammo gun held through its holdout, an ammo bow whose shoot is the placeholder, a beam staff held through its holdout,
		// a tower that asks its owner for the staff's buff, a plain shooter. Only code identifiers are logged, never display text.
		// Full catalogue of the mod: LEVY_ARMYSWEEP_MODS=Redemption LEVY_ARMYSWEEP_ALONE=1 (the table "army row" lines).
		/// <summary>(item, route, floor). The floor is the least ratio of a player's damage the route holds today (parity is 0.6 and up; a held blade or dagger sits at 0.45 to 0.65 of a player who taps at the best cadence, and its floor is the regression guard, not the goal).</summary>
		private static readonly (string item, string route, double floor)[] RedeRows =
		{
			("Zweihander", "held channel sword", 0.4), ("PureIronSword", "held channel sword", 0.4), ("TinyCleaver", "held channel sword", 0.6), ("PoemOfIlmatar", "held beam staff", 0.6),
			("WornDagger", "custom-class dagger", 0.4), ("Incisor", "custom-class dagger", 0.35), ("DoubleRifle", "ammo gun, shoot is the placeholder", 0.6), ("ElderWoodBow", "ammo bow, shoot is the placeholder", 0.6),
			("DAN", "held ammo gun", 0.45), ("HoneycombStaff", "tower that asks its owner for the buff", 0.6), ("NestWand", "tower that asks its owner for the buff", 0.6), ("Dusksong", "plain staff", 0.6),
		};

		private static void RedemptionCheck()
		{
			if (!ModLoader.TryGetMod("Redemption", out Mod red) || Environment.GetEnvironmentVariable("LEVY_REDE") == "0") return;
			SetupOriginArena(); _spNet = false;
			On_AchievementsHelper.HandleSpecialEvent += NoAchievements;
			string savedReach = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_REACH"); Environment.SetEnvironmentVariable("LEVY_ARMYSWEEP_REACH", "1"); // a blade-range weapon meets its enemy at blade distance, as in the mods sweep
			try
			{
				KeepCommander(); Step(2);
				// the custom damage class: every item of it can be held by a levy
				var custom = new List<Item>();
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
					if (ContentSamples.ItemsByType.TryGetValue(t, out Item s) && s != null && !s.IsAir && s.ModItem?.Mod == red && s.damage > 0 && RedemptionCompat.IsCustomClass(s)) custom.Add(s);
				var probe = new SoldierGear(); int held = 0;
				foreach (Item c in custom) { Item it = new Item(); it.SetDefaults(c.type); if (probe.CanHold(SoldierGear.SlotWeapon, it)) held++; else Note($"redemption: {c.ModItem.Name} of the custom class cannot be held by a levy"); }
				Check($"Redemption: every weapon of the mod's own damage class can be held by a levy ({held} of {custom.Count})", custom.Count > 0 && held == custom.Count);
				var ammoByKind = AmmoByKind();
				foreach (var (name, route, floor) in RedeRows)
				{
					if (!red.TryFind(name, out ModItem mi)) { Check($"Redemption/{name} exists", false); continue; }
					try
					{
						Item sample = ContentSamples.ItemsByType[mi.Type];
						int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int at) ? at : 0;
						ArmyRes lv = ArmyOne(mi.Type, ammo, 600, false, false);
						ArmyRes pl = ArmyPlayerBest(mi.Type, ammo, 600, Math.Max(lv.DummyMinions, 1), Math.Max(lv.DummyTurrets, 1), sample);
						double ratio = lv.Dmg / (double)Math.Max(1, pl.Dmg);
						Note($"redemption {name} [{route}]: levy damage {lv.Dmg} ({lv.Hits} hits), player {pl.Dmg} ({pl.Hits} hits), ratio {ratio:0.00}, from the commander {lv.FromCommander}{(lv.Err != null ? ", threw " + lv.Err : "")}");
						Check($"Redemption/{name} [{route}]: a levy lands at least {floor:0.00} of a player's damage (levy {lv.Dmg}, player {pl.Dmg}, ratio {ratio:0.00}), none from the commander, nothing thrown", pl.Dmg > 0 && ratio >= floor && lv.Err == null && !lv.FromCommander);
					}
					catch (Exception e) { Check($"Redemption/{name} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
					finally { ClearSoldiers(); }
				}
			}
			finally { _sweepTarget = null; Environment.SetEnvironmentVariable("LEVY_ARMYSWEEP_REACH", savedReach); On_AchievementsHelper.HandleSpecialEvent -= NoAchievements; }
		}
	}
}
