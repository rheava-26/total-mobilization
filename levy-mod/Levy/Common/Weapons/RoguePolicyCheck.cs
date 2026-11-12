using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>Boot self-check of the rogue policy table: every row names a real rogue weapon (when Calamity is loaded), the three modes are all in use, and the default for an unknown weapon is the hedge.</summary>
	public class RoguePolicyCheck : ModSystem
	{
		public override void PostAddRecipes() => LoadSafe.Run(Run);

		private void Run()
		{
			var fails = new List<string>(); int ok = 0;
			void Check(bool c, string w) { if (c) ok++; else fails.Add(w); }
			var count = new int[4];
			foreach (var kv in RoguePolicy.Table) count[(int)kv.Value]++;
			Check(RoguePolicy.Table.Count >= 55, $"the table has {RoguePolicy.Table.Count} rows");
			Check(count[(int)RogueMode.Wait] > 10 && count[(int)RogueMode.Spam] > 10 && count[(int)RogueMode.Situational] > 5 && count[(int)RogueMode.SmallTargets] > 3, $"all four policies are used (wait {count[0]}, spam {count[1]}, situational {count[2]}, small targets {count[3]})");
			Check(RoguePolicy.Estimate(4) == RogueMode.Situational && RoguePolicy.Estimate(90) == RogueMode.Situational && RoguePolicy.For(new Item()) == RogueMode.Situational, "an unlisted weapon gets the hedge (strike for a boss or a crowd, plain throws otherwise)");
			Check(RoguePolicy.Table["CalamityMod/Mycoroot"] == RogueMode.Wait && RoguePolicy.Table["CalamityMod/EclipsesFall"] == RogueMode.Spam && RoguePolicy.Table["CalamityMod/Penumbra"] == RogueMode.Spam && RoguePolicy.Table["CalamityMod/ProfanedPartisan"] == RogueMode.Situational && RoguePolicy.Table["CalamityMod/EpidemicShredder"] == RogueMode.SmallTargets, "spot rows: Mycoroot waits (plain throws do nothing), Eclipse's Fall spams, Penumbra spams, Profaned Partisan is situational, Epidemic Shredder strikes small targets only");
			string info = "";
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
			{
				int missing = 0, notRogue = 0;
				foreach (string key in RoguePolicy.Table.Keys)
				{
					string name = key.Substring(key.IndexOf('/') + 1);
					if (!cal.TryFind(name, out ModItem mi)) { missing++; continue; }
					if (!ExtraDamageClasses.IsRogue(mi.Item.DamageType)) notRogue++;
				}
				Check(missing == 0 && notRogue == 0, $"every table row is a real Calamity rogue weapon (missing {missing}, not rogue {notRogue})");
				if (cal.TryFind("WulfrumKnife", out ModItem wk)) Check(RoguePolicy.For(wk.Item) == RogueMode.Wait, "For(Wulfrum Knife) reads the table");
				info = " (Calamity rows resolved)";
			}
			if (fails.Count == 0) Mod.Logger.Info($"[RoguePolicy] self-check {ok} checks ok fail=0 (wait {count[0]}, spam {count[1]}, situational {count[2]}, small targets {count[3]}){info}");
			else Mod.Logger.Error("[RoguePolicy] self-check FAILED: " + string.Join("; ", fails));
		}
	}
}
