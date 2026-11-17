using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Death;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class LevyRulesCheck
	{
		// =====================================================================================================
		// DEATH TEXT: a levy killed by something with its own death text posts that text (and always posts in a Nameless Deity / Avatar of Emptiness fight)
		// =====================================================================================================

		/// <summary>Spawns a levy with 1 life, puts <paramref name="buff"/> on it and steps until it dies. Returns the line posted by its death ("" when none) and whether it died.</summary>
		private static string KillByBuff(int buff, string name, out bool died)
		{
			var s = Levy(new Vector2((Cx + 20) * 16f, _floorY * 16f)); died = false;
			if (s == null) return null;
			string line = "";
			try
			{
				s.Record.Name = name;
				Step(3); // the body's first ticks clear the debuff immunities other mods' SetDefaults gave it (Calamity's), as in a real fight
				s.NPC.life = 1; s.Record.CurrentHp = 1; s.NPC.GivenName = name;
				int posted0 = LevyDeathPost.Posted; LevyDeathPost.LastPosted = null;
				long dt0 = SoldierNPC.DotTicks, dd0 = SoldierNPC.DotDamage;
				s.NPC.AddBuff(buff, 3600);
				bool had = s.NPC.HasBuff(buff);
				for (int t = 0; t < 40 && s.IsAlive; t++) { Step(30); if (s.NPC.life > 1) { s.NPC.life = 1; s.Record.CurrentHp = 1; } }
				died = !s.IsAlive;
				if (!died) Note($"death text: the levy did not die: buff added {had}, still on {s.NPC.HasBuff(buff)}, life {s.NPC.life}, dot ticks {SoldierNPC.DotTicks - dt0}, dot damage {SoldierNPC.DotDamage - dd0}, immune {s.NPC.buffImmune[buff]}, alive {s.IsAlive}, active {s.NPC.active}");
				if (LevyDeathPost.Posted > posted0) line = LevyDeathPost.LastPosted ?? "";
			}
			finally { Remove(s); }
			return line;
		}

		private static void DeathPart()
		{
			Arena(FloorY, Half); PlaceCommander(-150);
			var cfg = LevyConfig.Get; int pct0 = cfg.CustomDeathMessagePercent; bool reg0 = cfg.RegularDeathMessages; bool? nox0 = NoxusFights.Override;
			try
			{
				// the decision table
				bool ok = true;
				ok &= LevyDeathPost.Decide(true, false, 100, 0.5f, false) == LevyDeathPost.Verdict.Custom;   // custom text shows even with death messages off
				ok &= LevyDeathPost.Decide(true, false, 0, 0.0f, false) == LevyDeathPost.Verdict.None;      // the chance is a real dial: 0 never shows it
				ok &= LevyDeathPost.Decide(true, false, 30, 0.29f, true) == LevyDeathPost.Verdict.Custom;
				ok &= LevyDeathPost.Decide(true, false, 30, 0.31f, true) == LevyDeathPost.Verdict.Themed;
				ok &= LevyDeathPost.Decide(false, false, 100, 0.0f, false) == LevyDeathPost.Verdict.None;   // no custom text: the old setting rules
				ok &= LevyDeathPost.Decide(false, false, 100, 0.0f, true) == LevyDeathPost.Verdict.Themed;
				ok &= LevyDeathPost.Decide(false, true, 0, 0.99f, false) == LevyDeathPost.Verdict.Themed;   // a Noxus fight: always, whatever the settings
				ok &= LevyDeathPost.Decide(true, true, 0, 0.99f, false) == LevyDeathPost.Verdict.Custom;
				Check("death text: the decision table (custom text beats off, the percent is honoured, a Nameless Deity or Avatar fight always posts)", ok);
				Check("death text: the default chance is 30%", pct0 == 30);

				cfg.RegularDeathMessages = false; cfg.CustomDeathMessagePercent = 100; NoxusFights.Override = false;
				// a vanilla debuff has no custom text: nothing posts with death messages off
				string vline = KillByBuff(BuffID.OnFire, "Pvt. Ember", out bool vdied);
				Check($"death text: a levy burned to death by vanilla fire dies and posts no custom line with death messages off ({(vline.Length == 0 ? "no line" : vline)})", vdied && vline.Length == 0);

				// Calamity: the debuffs a player's death text is written for
				if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				{
					foreach (var (buffName, key) in new[] { ("BrimstoneFlames", "BrimstoneFlames"), ("Plague", "Plague"), ("Dragonfire", "Dragonfire") })
					{
						if (!cal.TryFind(buffName, out ModBuff mb)) { Check($"death text: Calamity buff {buffName} found", false); continue; }
						var want = new List<string>();
						for (int i = 1; i <= 4; i++) { string lk = "Mods.CalamityMod.Status.Death." + key + i; if (Language.Exists(lk)) want.Add(Language.GetTextValue(lk, "Pvt. Custom")); }
						string line = KillByBuff(mb.Type, "Pvt. Custom", out bool died);
						Note($"death text: {buffName} -> \"{line}\" (one of {want.Count} Calamity lines)");
						Check($"death text: a levy killed by {buffName} posts Calamity's own death line with its name, with death messages off", died && want.Contains(line));
					}
					// the same levy with the chance at 0 stays silent
					cfg.CustomDeathMessagePercent = 0;
					if (cal.TryFind("BrimstoneFlames", out ModBuff b2)) { string quiet = KillByBuff(b2.Type, "Pvt. Quiet", out bool d2); Check("death text: with the chance at 0 and death messages off the same death posts nothing", d2 && quiet.Length == 0); }
					// a Nameless Deity / Avatar fight: every death posts, custom text when there is one
					NoxusFights.Override = true;
					if (cal.TryFind("BrimstoneFlames", out ModBuff b3))
					{
						var want = new List<string>(); for (int i = 1; i <= 4; i++) { string lk = "Mods.CalamityMod.Status.Death.BrimstoneFlames" + i; if (Language.Exists(lk)) want.Add(Language.GetTextValue(lk, "Pvt. Noxus")); }
						string nline = KillByBuff(b3.Type, "Pvt. Noxus", out bool d3);
						Check($"death text: in a Noxus fight a levy dying to brimstone fire posts the custom line at 0% ({nline})", d3 && want.Contains(nline));
					}
				}
				else Note("death text: Calamity is not loaded; the debuff text cases are the Calamity half's");

				// a Noxus fight: a death with no custom text still posts the themed line, with death messages off and the chance at 0
				cfg.CustomDeathMessagePercent = 0; cfg.RegularDeathMessages = false; NoxusFights.Override = true;
				string tline = KillByBuff(BuffID.OnFire, "Pvt. Ember", out bool tdied);
				Check($"death text: in a Noxus fight every levy death posts, a themed line when it has no custom text ({tline})", tdied && !string.IsNullOrEmpty(tline));
				NoxusFights.Override = false;
				string qline = KillByBuff(BuffID.OnFire, "Pvt. Ember", out bool qdied);
				Check("death text: outside the fight the same death posts nothing again", qdied && qline.Length == 0);
				// no NoxusBoss, no fight
				NoxusFights.Override = null;
				Check($"death text: with NoxusBoss {(NoxusFights.Installed ? "loaded but no fight" : "not loaded")} the fight flag is off", !NoxusFights.Active);
			}
			finally { cfg.CustomDeathMessagePercent = pct0; cfg.RegularDeathMessages = reg0; NoxusFights.Override = nox0; }
		}
	}
}
