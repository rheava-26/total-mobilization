using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Mechanization.Common
{
	/// <summary>Boot self-check of the boss hold-fire rule: unbeaten bosses and their parts are held, minions and ordinary enemies are not, and the option defaults to off.</summary>
	public class BossHoldFireCheck : ModSystem
	{
		private static NPC Make(int type) { var n = new NPC(); n.SetDefaults(type); n.active = true; return n; }

		public override void PostSetupRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try
			{
				Check(!new MechConfig().FireOnUnbeatenBosses, "the option defaults to off");
				Check(BossHoldFire.Holds(Make(NPCID.EyeofCthulhu)), "an unbeaten Eye of Cthulhu is held");
				Check(BossHoldFire.Holds(Make(NPCID.SkeletronHand)), "an unbeaten Skeletron hand is held");
				Check(BossHoldFire.Holds(Make(NPCID.EaterofWorldsBody)), "an unbeaten Eater of Worlds segment is held");
				Check(BossHoldFire.Holds(Make(NPCID.TheDestroyerBody)), "an unbeaten Destroyer segment is held");
				Check(!BossHoldFire.Holds(Make(NPCID.ServantofCthulhu)), "a Servant of Cthulhu is not held");
				Check(!BossHoldFire.Holds(Make(NPCID.Creeper)), "a Creeper is not held");
				Check(!BossHoldFire.Holds(Make(NPCID.BlueSlime)), "an ordinary enemy is not held");
				Check(!BossHoldFire.Holds(null), "null is not held");
			}
			catch (Exception e) { failures.Add($"threw {e.GetType().Name}: {e.Message}"); }

			if (failures.Count == 0) Mod.Logger.Info($"Mechanization boss hold-fire self-check: {ok} checks OK");
			else
			{
				Mod.Logger.Warn($"Mechanization boss hold-fire self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
				foreach (var f in failures) Mod.Logger.Warn("  hold fire: " + f);
			}
		}
	}
}
