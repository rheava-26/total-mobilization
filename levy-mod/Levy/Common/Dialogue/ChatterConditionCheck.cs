using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Boot-time self-check of the roster-aware chatter ([summoner] / [non-summoner] / [fragile] tags and the {Tank} / {Glutton} placeholders).
	/// A fake roster (a summoner, a 5-death levy, a 30-kill levy, low HP) is fed through the real line filter and every such line must resolve with the right text,
	/// and must stay silent for a roster that does not meet its condition. Log only.
	/// </summary>
	public class ChatterConditionCheck : ModSystem
	{
		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(Core);

		private static SoldierRecord Make(string name, int crystals, bool male = true) => new SoldierRecord { Id = name.GetHashCode(), Name = name, LifeCrystals = crystals, Male = male };

		private void Core()
		{
			var failures = new List<string>();
			int checkedLines = 0;
			try
			{
				var pool = DialogueTable.Get(DialogueTrigger.IDLE);
				if (pool == null) { failures.Add("no IDLE pool"); }
				else
				{
					// fake roster: Sam is a summoner, Tess has died 5 times, Gus has 30 kills, everyone is under 200 HP
					var sam = Make("Sam", 0); sam.Gear.Weapon = new Item(); sam.Gear.Weapon.SetDefaults(ItemID.SlimeStaff);
					var tess = Make("Tess", 0, false); tess.Deaths = 5;
					var gus = Make("Gus", 0); gus.Kills = 30;
					var pat = Make("Pat", 0);
					RosterFacts.TestRoster = new List<SoldierRecord> { sam, tess, gus, pat };
					try
					{
						foreach (DialogueLine l in pool)
						{
							bool conditional = l.Cond != LineCond.None || (l.Needs & (LineNeeds.Tank | LineNeeds.Glutton)) != 0;
							if (!conditional) continue;
							checkedLines++;
							string who = l.Text.Length > 28 ? l.Text.Substring(0, 28) : l.Text;
							string bySam = DialogueSystem.RenderOne(l, sam), byPat = DialogueSystem.RenderOne(l, pat), byTess = DialogueSystem.RenderOne(l, tess), byGus = DialogueSystem.RenderOne(l, gus);
							if (l.Cond == LineCond.Summoner && (bySam == null || byPat != null)) failures.Add($"[summoner] line '{who}' did not resolve only for the summoner");
							if (l.Cond == LineCond.NonSummonerWithSummoner && (bySam != null || byPat == null)) failures.Add($"[non-summoner] line '{who}' did not resolve only for a non-summoner");
							if (l.Cond == LineCond.Fragile && byPat == null) failures.Add($"[fragile] line '{who}' did not resolve for a low-HP army");
							if ((l.Needs & LineNeeds.Tank) != 0)
							{
								if (byPat == null || !byPat.Contains("Tess") || (l.Text.Contains("{TankDeaths}") && !byPat.Contains("5"))) failures.Add($"{{Tank}} line '{who}' did not name Tess with 5 deaths: {byPat}");
								if (byTess != null) failures.Add($"{{Tank}} line '{who}' was spoken by the tank himself");
							}
							if ((l.Needs & LineNeeds.Glutton) != 0)
							{
								if (byPat == null || !byPat.Contains("Gus") || !byPat.Contains("30")) failures.Add($"{{Glutton}} line '{who}' did not name Gus with 30 kills: {byPat}");
								if (byGus != null) failures.Add($"{{Glutton}} line '{who}' was spoken by the glutton himself");
							}
						}

						// a roster that meets none of it: nobody special, healthy, no summoner
						var a = Make("Ann", 10); var b = Make("Bo", 10); b.Deaths = 2; b.Kills = 24;
						RosterFacts.TestRoster = new List<SoldierRecord> { a, b };
						foreach (DialogueLine l in pool)
						{
							if (l.Cond == LineCond.None && (l.Needs & (LineNeeds.Tank | LineNeeds.Glutton)) == 0) continue;
							if (DialogueSystem.RenderOne(l, a) != null) failures.Add($"line '{(l.Text.Length > 28 ? l.Text.Substring(0, 28) : l.Text)}' resolved for a roster that does not meet its condition");
						}

						// second batch: mage, hardmode+officer, goblin, lowhp, boss, {WorldName}, {Officer}
						var mia = Make("Mia", 10); mia.Gear.Weapon = new Item(); mia.Gear.Weapon.SetDefaults(ItemID.WandofSparking);
						var hurt = Make("Hal", 10); hurt.CurrentHp = 10;
						var fit = Make("Fay", 10);
						RosterFacts.TestRoster = new List<SoldierRecord> { mia, hurt, fit };
						RosterFacts.TestWorld = "Testland"; RosterFacts.TestOfficer = "Painter";
						RosterFacts.TestHardmode = true; RosterFacts.TestGoblin = true; RosterFacts.TestBoss = true;
						int seenOnA = 0;
						foreach (DialogueLine l in pool)
						{
							string t = l.Text.Length > 28 ? l.Text.Substring(0, 28) : l.Text;
							string byMia = DialogueSystem.RenderOne(l, mia), byFit = DialogueSystem.RenderOne(l, fit), byHurt = DialogueSystem.RenderOne(l, hurt);
							if ((l.Needs & LineNeeds.WorldName) != 0) { seenOnA++; if (byFit == null || !byFit.Contains("Testland")) failures.Add($"{{WorldName}} line '{t}' did not fill: {byFit}"); }
							if ((l.Needs & LineNeeds.Officer) != 0) { seenOnA++; if (l.Cond != LineCond.HardmodeOfficer || byFit == null || !byFit.Contains("Painter")) failures.Add($"{{Officer}} line '{t}' did not fill: {byFit}"); }
							if (l.Cond == LineCond.Mage) { seenOnA++; if (byMia == null || byFit != null) failures.Add($"[mage] line '{t}' did not resolve only for the mage"); }
							if (l.Cond == LineCond.NonMageWithMage) { seenOnA++; if (byMia != null || byFit == null) failures.Add($"[non-mage] line '{t}' did not resolve only for a non-mage"); }
							if (l.Cond == LineCond.LowHp) { seenOnA++; if (byHurt == null || byFit != null) failures.Add($"[lowhp] line '{t}' did not resolve only for the wounded levy"); }
							if (l.Cond == LineCond.Goblin || l.Cond == LineCond.BossFight || l.Cond == LineCond.HardmodeOfficer) { seenOnA++; if (byFit == null) failures.Add($"line '{t}' did not resolve with its condition met"); }
						}
						// the same lines must go quiet when the condition is off
						RosterFacts.TestHardmode = false; RosterFacts.TestGoblin = false; RosterFacts.TestBoss = false; RosterFacts.TestOfficer = null;
						foreach (DialogueLine l in pool)
						{
							if (l.Cond == LineCond.Goblin || l.Cond == LineCond.BossFight || l.Cond == LineCond.HardmodeOfficer || (l.Needs & LineNeeds.Officer) != 0)
								if (DialogueSystem.RenderOne(l, fit) != null) failures.Add($"line '{(l.Text.Length > 28 ? l.Text.Substring(0, 28) : l.Text)}' resolved with its condition off");
						}
						if (seenOnA < 9) failures.Add($"expected at least 9 second-batch tagged lines, found {seenOnA}");
					}
					finally { RosterFacts.TestRoster = null; RosterFacts.TestHardmode = RosterFacts.TestGoblin = RosterFacts.TestBoss = null; RosterFacts.TestOfficer = RosterFacts.TestWorld = null; }
					if (checkedLines < 21) failures.Add($"expected at least 21 roster-aware IDLE lines, found {checkedLines}");
				}
			}
			catch (Exception e) { failures.Add("chatter condition check threw: " + e.GetType().Name + ": " + e.Message); }

			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Levy chatter condition self-check: {checkedLines} roster-aware lines resolve correctly OK");
				else
				{
					Mod.Logger.Warn($"Levy chatter condition self-check: {failures.Count} issue(s)");
					foreach (string f in failures) Mod.Logger.Warn("Levy chatter condition self-check: " + f);
				}
			}
			catch (Exception) { }
		}
	}
}
