using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Boot-time self-check of the minion routing: every minion staff of every loaded mod must be driven as a REAL minion (the staff's own projectile, run under the soldier's dummy owner), and a vanilla
	/// staff must stay driven. Logs one summary line, or the staves that would fall back to the stand-in. Inspect-and-log only; never throws.
	/// </summary>
	public class ModdedMinionCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			try
			{
				int modded = 0, driven = 0;
				var undriven = new List<string>();
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = new Item();
					try { it.SetDefaults(t); } catch { continue; }
					if (it.IsAir || it.sentry || it.shoot <= 0 || it.damage <= 0 || !it.CountsAsClass(DamageClass.Summon)) continue;
					if (WeaponClassifier.GetProfile(it).Family != WeaponFamily.Summon) continue;
					modded++;
					if (SoldierMinions.CanDrive(it.shoot)) driven++; else undriven.Add(it.Name);
				}
				bool vanillaOk = SoldierMinions.CanDrive(ProjectileID.BabySlime) && SoldierMinions.CanDrive(ProjectileID.BabyBird);
				bool slotOk = Math.Abs(SoldierMinions.SlotOf(ContentSamples.ProjectilesByType[ProjectileID.BabySlime], 3f) - ContentSamples.ProjectilesByType[ProjectileID.BabySlime].minionSlots) < 0.001f;
				if (vanillaOk && slotOk && undriven.Count == 0)
					Mod.Logger.Info($"Levy: modded-minion self-check OK: {driven}/{modded} modded minion staves run their real minion (vanilla routing intact)");
				else
					Mod.Logger.Warn($"Levy: modded-minion self-check: vanillaOk={vanillaOk} slotOk={slotOk} {driven}/{modded} modded minion staves driven; stand-in for: {string.Join(", ", undriven)}");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: modded-minion self-check threw " + e.GetType().Name + ": " + e.Message); }
		}
	}
}
