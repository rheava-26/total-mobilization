using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Boot-time self-check (WeaponWiringCheck style): logs every NPCID.Sets.ProjectileNPC type with its defaults, and asserts that the damageable "orb" projectile-NPCs
	/// (Chaos Ball, Water Sphere, ...) are not never-targets, so levies can shoot down the orbs casters throw. Inspect-and-log only.
	/// </summary>
	public class ProjectileNpcCheck : ModSystem
	{
		private static readonly int[] Orbs = { NPCID.ChaosBall, NPCID.WaterSphere, NPCID.BurningSphere, NPCID.ShadowFlameApparition };

		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostAddRecipesCore);

		private void PostAddRecipesCore()
		{
			var failures = new List<string>();
			int asserted = 0;
			try
			{
				var dump = new List<string>();
				for (int t = 1; t < NPCID.Sets.ProjectileNPC.Length; t++)
				{
					if (!NPCID.Sets.ProjectileNPC[t]) continue;
					var n = new NPC(); n.SetDefaults(t); n.active = true;
					dump.Add($"{t}:{n.TypeName}(life{n.lifeMax},noDmg{(n.dontTakeDamage ? 1 : 0)},imm{(n.immortal ? 1 : 0)},fr{(n.friendly ? 1 : 0)},hostile={(LevyUtil.IsHostileTarget(n) ? 1 : 0)},never={(SoldierTargeting.IsNeverTarget(n) ? 1 : 0)})");
				}
				Mod.Logger.Info("Levy projectile-NPC table: " + string.Join("; ", dump));
				foreach (int t in Orbs)
				{
					var n = new NPC(); n.SetDefaults(t);
					if (!NPCID.Sets.ProjectileNPC[t] || n.dontTakeDamage || n.immortal) continue; // only assert for damageable set members
					n.active = true; asserted++;
					if (SoldierTargeting.IsNeverTarget(n)) failures.Add($"orb {n.TypeName} ({t}) is still a never-target");
				}
			}
			catch (System.Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) Mod.Logger.Info($"Levy projectile-NPC self-check: {asserted} damageable orb types targetable OK");
			else foreach (var f in failures) Mod.Logger.Warn("Levy projectile-NPC self-check FAILED: " + f);
		}
	}
}
