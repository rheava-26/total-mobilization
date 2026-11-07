using System;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using Levy.Common.Config;

namespace Levy.Common.Movement
{
	/// <summary>Boot self-check for the mobility layer: the predicted paths of the new dodge moves and the config default.</summary>
	public class MobilityCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			var fails = new System.Collections.Generic.List<string>();
			try
			{
				const float g = 0.3f;
				var c0 = new Vector2(1000f, 1000f);
				var single = new Maneuver { Kind = 6, JumpVy = -6.2f };
				var dbl = new Maneuver { Kind = 7, JumpVy = -6.2f, J2Vy = -5.6f, J2At = 18 };
				float apex1 = 9999f, apex2 = 9999f;
				for (int t = 1; t < 120; t++) { apex1 = Math.Min(apex1, ThreatField.PathAt(c0, single, g, t).Y); apex2 = Math.Min(apex2, ThreatField.PathAt(c0, dbl, g, t).Y); }
				if (!(apex2 < apex1 - 20f)) fails.Add($"a double jump should peak clearly higher than one jump ({apex2:0} vs {apex1:0})");
				if (Math.Abs(ThreatField.PathAt(c0, dbl, g, 3).Y - ThreatField.PathAt(c0, single, g, 3).Y) > 0.01f) fails.Add("a double jump follows the first jump until the second impulse");
				var hook = new Maneuver { Kind = 9, Hook = true, HookTarget = new Vector2(1100f, 800f), HookDur = 20 };
				if (Vector2.Distance(ThreatField.PathAt(c0, hook, g, 40), hook.HookTarget) > 0.5f) fails.Add("a hook path ends on its target");
				if (Vector2.Distance(ThreatField.PathAt(c0, hook, g, 4), c0) > 0.5f) fails.Add("a hook path waits out the cast");
				var f = typeof(LevyConfig).GetField("SoldiersTakeFallDamage");
				var dv = f?.GetCustomAttribute<DefaultValueAttribute>();
				if (dv == null || !(dv.Value is bool b) || b) fails.Add("SoldiersTakeFallDamage must default to false");
			}
			catch (Exception e) { fails.Add("exception " + e.Message); }
			if (fails.Count == 0) Levy.Instance?.Logger.Info("Levy mobility self-check: OK (double jump, hook path, fall damage default off)");
			else Levy.Instance?.Logger.Error("Levy mobility self-check FAILED: " + string.Join("; ", fails));
		}
	}
}
