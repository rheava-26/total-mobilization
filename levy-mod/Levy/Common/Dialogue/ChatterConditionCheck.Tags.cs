using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Content.Buffs;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// The second half of the chatter self-check: the speaker tags ([female] [ranger] [melee]), the world-progress tags ([early] [postskeletron] [hardmode] [postplantera] [postmoonlord]),
	/// the multi-tag parser, and the Blood Moon override (a female levy or officer speaks ONLY [grumpy] lines while the moon is up, in every pool but the exempt ones). It also checks that every
	/// pool the override covers has an annoyed set big enough not to repeat, how a levy's kit ranks in the army (ARMORY_BEST / WORST / WANT), and the per-drink cheers. Log only.
	/// </summary>
	public partial class ChatterConditionCheck
	{
		private const LineCond NewTagMask = LineCond.Female | LineCond.Ranger | LineCond.Melee | LineCond.Early | LineCond.PostSkeletron | LineCond.Hardmode | LineCond.PostPlantera | LineCond.PostMoonlord | LineCond.Grumpy;
		private static readonly HashSet<string> GrumpyExemptKeys = new(StringComparer.OrdinalIgnoreCase) { "MUTINY", "MUTINY_BEGIN", "MUTINY_REFUSE", "MUTINY_CALM", "DEATH_MESSAGE", "DESERTED", "NO_ONE_LEFT" };
		private static readonly string[] FemaleOfficers = { "Nurse", "Dryad", "Zoologist", "Princess" };
		private static readonly HashSet<string> BigGrumpySets = new(StringComparer.OrdinalIgnoreCase)
			{ "ENGAGE", "KILL", "HURT", "LOW_HP", "OUT_OF_AMMO", "COMRADE_HIT", "COMRADE_DOWN", "SHAKEN", "ROUTED", "RALLIED", "VICTORY", "BOSS_SIGHTED" };

		/// <summary>The smallest annoyed set a covered pool must have: shouts that fire all the time need the most.</summary>
		private static int MinGrumpy(string key) => key == "IDLE" ? 20 : key == "BLOOD_MOON" ? 8 : BigGrumpySets.Contains(key) ? 10 : 4;

		private static void CheckTagsAndOverride(List<string> failures, ref int checkedLines)
		{
			var fem = Make("Fay", 10, false);
			var man = Make("Max", 10);
			var ranger = Make("Rae", 10); ranger.Gear.Weapon = new Item(); ranger.Gear.Weapon.SetDefaults(ItemID.Handgun);
			var melee = Make("Mel", 10); melee.Gear.Weapon = new Item(); melee.Gear.Weapon.SetDefaults(ItemID.CopperShortsword);
			var femRanger = Make("Fran", 10, false); femRanger.Gear.Weapon = new Item(); femRanger.Gear.Weapon.SetDefaults(ItemID.Handgun);
			var speakers = new (SoldierRecord rec, bool ranger, bool melee)[] { (fem, false, false), (man, false, false), (ranger, true, false), (melee, false, true), (femRanger, true, false) };
			var stages = new RosterFacts.WorldStage[]
			{
				default,
				new RosterFacts.WorldStage { Skeletron = true },
				new RosterFacts.WorldStage { Skeletron = true, Hardmode = true },
				new RosterFacts.WorldStage { Skeletron = true, Hardmode = true, Plantera = true },
				new RosterFacts.WorldStage { Skeletron = true, Hardmode = true, Plantera = true, MoonLord = true },
			};
			RosterFacts.TestRoster = new List<SoldierRecord> { fem, man, ranger, melee, femRanger };
			int tagged = 0, grumpyLines = 0, seenWorld = 0, seenClass = 0, seenFemale = 0;
			try
			{
				// ---- the parser: several tags join with a plus, the old two-word tag still means what it did, an unknown tag is rejected
				var into = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
				int bad = DialogueTable.Parse(null, "selfcheck", "IDLE | Any | [hardmode+ranger] a\nIDLE | Any | [hardmode+officer] b\nIDLE | Any | [female] c\nIDLE | Any | [grumpy+female] d\nIDLE | Any | [postmoonlord] e\nIDLE | Any | [bogus] f\nIDLE | Any | [hardmode+bogus] g", into);
				if (bad != 2) failures.Add($"the tag parser rejected {bad} of the 2 bad tags");
				if (!into.TryGetValue("IDLE", out var il) || il.Count != 5) failures.Add("the tag parser did not accept the 5 good tags");
				else
				{
					if (il[0].Cond != (LineCond.Hardmode | LineCond.Ranger)) failures.Add("[hardmode+ranger] did not parse to both tags: " + il[0].Cond);
					if (il[1].Cond != LineCond.HardmodeOfficer) failures.Add("[hardmode+officer] no longer parses to the hardmode-with-officer tag: " + il[1].Cond);
					if (il[2].Cond != LineCond.Female) failures.Add("[female] parsed as " + il[2].Cond);
					if (il[3].Cond != (LineCond.Grumpy | LineCond.Female)) failures.Add("[grumpy+female] parsed as " + il[3].Cond);
					if (il[4].Cond != LineCond.PostMoonlord) failures.Add("[postmoonlord] parsed as " + il[4].Cond);
				}

				// ---- every tagged line in the file resolves for exactly the speakers and worlds its tags describe (lines with other needs are left to their own checks)
				foreach (var kv in DialogueTable.Lines)
				{
					bool exempt = GrumpyExemptKeys.Contains(kv.Key);
					foreach (DialogueLine l in kv.Value)
					{
						if ((l.Cond & NewTagMask) == 0) continue;
						tagged++;
						if ((l.Cond & LineCond.Grumpy) != 0) grumpyLines++;
						if ((l.Cond & LineCond.Female) != 0) seenFemale++;
						if ((l.Cond & (LineCond.Ranger | LineCond.Melee)) != 0) seenClass++;
						if ((l.Cond & (LineCond.Early | LineCond.PostSkeletron | LineCond.Hardmode | LineCond.PostPlantera | LineCond.PostMoonlord)) != 0) seenWorld++;
						string who = kv.Key + ": " + (l.Text.Length > 30 ? l.Text.Substring(0, 30) : l.Text);
						if (exempt && (l.Cond & LineCond.Grumpy) != 0) failures.Add($"{who} is [grumpy] in a pool the Blood Moon override skips");
						bool plain = l.Needs == LineNeeds.None && l.NpcTypes.Length == 0 && l.Filter == LineFilter.Any && (l.Cond & ~NewTagMask) == LineCond.None;
						if (!plain) continue;
						foreach (bool moon in new[] { false, true })
						foreach (var w in stages)
						foreach (var sp in speakers)
						{
							RosterFacts.TestBloodMoon = moon; RosterFacts.TestStage = w;
							bool want = true;
							if ((l.Cond & LineCond.Grumpy) != 0 && !(moon && !sp.rec.Male)) want = false;
							if ((l.Cond & LineCond.Female) != 0 && sp.rec.Male) want = false;
							if ((l.Cond & LineCond.Ranger) != 0 && !sp.ranger) want = false;
							if ((l.Cond & LineCond.Melee) != 0 && !sp.melee) want = false;
							if ((l.Cond & LineCond.Early) != 0 && (w.Skeletron || w.Hardmode)) want = false;
							if ((l.Cond & LineCond.PostSkeletron) != 0 && !w.Skeletron) want = false;
							if ((l.Cond & LineCond.Hardmode) != 0 && !w.Hardmode) want = false;
							if ((l.Cond & LineCond.PostPlantera) != 0 && !w.Plantera) want = false;
							if ((l.Cond & LineCond.PostMoonlord) != 0 && !w.MoonLord) want = false;
							if (want && moon && !sp.rec.Male && (l.Cond & LineCond.Grumpy) == 0) want = false; // the override: a female speaker on a Blood Moon gets only the annoyed set
							bool got = DialogueSystem.RenderOne(l, sp.rec) != null;
							if (got != want) { failures.Add($"{who} [{l.Cond}] resolved={got}, expected {want} (moon {moon}, {(sp.rec.Male ? "male" : "female")}{(sp.ranger ? " ranger" : "")}{(sp.melee ? " melee" : "")}, stage {Array.IndexOf(stages, w)})"); goto doneTags; }
						}
					}
				}
				doneTags:
				RosterFacts.TestBloodMoon = null; RosterFacts.TestStage = null;
				if (seenWorld < 12) failures.Add($"expected at least 12 world-progress tagged lines, found {seenWorld}");
				if (seenClass < 8) failures.Add($"expected at least 8 [ranger] / [melee] lines, found {seenClass}");
				if (seenFemale < 1) failures.Add("expected at least 1 [female] line");

				// ---- the override, line by line: on a Blood Moon a female speaker resolves NO line that is not [grumpy], in any pool the override covers
				RosterFacts.TestBloodMoon = true;
				foreach (var kv in DialogueTable.Lines)
				{
					if (GrumpyExemptKeys.Contains(kv.Key)) continue;
					foreach (DialogueLine l in kv.Value)
					{
						if ((l.Cond & LineCond.Grumpy) != 0) continue;
						if (DialogueSystem.RenderOne(l, fem) != null) { failures.Add($"a female levy would say a normal line on a Blood Moon: {kv.Key}: {(l.Text.Length > 40 ? l.Text.Substring(0, 40) : l.Text)}"); break; }
					}
				}
				RosterFacts.TestBloodMoon = null;

				// ---- the annoyed set is big enough in every pool it covers
				var ctx = new DialogueContext { Player = "Cap", Comrade = "Bo", Boss = "Thing", BossKey = "EyeofCthulhu", Killer = "a Zombie", Town = "Town", Wage = "1 silver" };
				foreach (DialogueTrigger t in Enum.GetValues(typeof(DialogueTrigger)))
				{
					string key = t.ToString();
					if (t == DialogueTrigger.BOSS || t == DialogueTrigger.FLIGHT_SHOUT || key.StartsWith("OFFICER_CHAT_") || key.StartsWith("DRINK_") || GrumpyExemptKeys.Contains(key)) continue;
					var pool = DialogueTable.Get(t);
					int n = 0;
					if (pool != null) foreach (DialogueLine l in pool) if ((l.Cond & LineCond.Grumpy) != 0) n++;
					if (n < MinGrumpy(key)) failures.Add($"the annoyed set for {key} has {n} lines, needs {MinGrumpy(key)}");
					if (t == DialogueTrigger.NAMEDROP) continue; // no town NPCs in a boot with no world
					// the real picker: she gets annoyed lines only, he gets none, and off a Blood Moon nobody gets any
					int fHit = 0;
					for (int i = 0; i < 30; i++)
					{
						RosterFacts.TestBloodMoon = true;
						string a = DialogueSystem.Pick(t, fem, ctx, null, true, -1, out DialogueLine pf, false);
						string b = DialogueSystem.Pick(t, man, ctx, null, true, -1, out DialogueLine pm, false);
						RosterFacts.TestBloodMoon = false;
						string c = DialogueSystem.Pick(t, fem, ctx, null, true, -1, out DialogueLine pc, false);
						if (a != null) { fHit++; if ((pf.Cond & LineCond.Grumpy) == 0) { failures.Add($"{key}: a female levy on a Blood Moon was handed a normal line: {a}"); break; } }
						if (b != null && (pm.Cond & LineCond.Grumpy) != 0) { failures.Add($"{key}: a male levy was handed an annoyed line: {b}"); break; }
						if (c != null && (pc.Cond & LineCond.Grumpy) != 0) { failures.Add($"{key}: an annoyed line fired with no Blood Moon: {c}"); break; }
					}
					if (fHit == 0) failures.Add($"{key}: a female levy on a Blood Moon was handed nothing at all");
					checkedLines++;
				}
				RosterFacts.TestBloodMoon = null;

				// ---- a boss with no annoyed lines of its own falls back to the general annoyed boss set; the mutineers are left alone
				RosterFacts.TestBloodMoon = true;
				string bf = DialogueSystem.Pick(DialogueTrigger.BOSS, fem, ctx, null, true, -1, out DialogueLine pbf, false);
				if (bf == null || (pbf.Cond & LineCond.Grumpy) == 0) failures.Add("a female levy meeting a named boss on a Blood Moon was not handed an annoyed line: " + bf);
				RosterFacts.TestBloodMoon = false;
				string bm = DialogueSystem.Pick(DialogueTrigger.BOSS, man, ctx, null, true, -1, out DialogueLine pbm, false);
				if (bm == null || (pbm.Cond & LineCond.Grumpy) != 0) failures.Add("a male levy meeting a named boss was handed an annoyed line or nothing: " + bm);
				RosterFacts.TestBloodMoon = true;
				if (DialogueSystem.PickKey("MUTINY", fem, ctx, out DialogueLine pmu) == null || (pmu.Cond & LineCond.Grumpy) != 0) failures.Add("a female mutineer on a Blood Moon lost her mutiny cries");
				foreach (string officer in FemaleOfficers)
					foreach (string state in DialogueTable.OfficerStates)
					{
						string key = "OFFICER_" + officer + "_" + state;
						int n = 0; var pool = DialogueTable.Get(key);
						if (pool != null) foreach (DialogueLine l in pool) if ((l.Cond & LineCond.Grumpy) != 0) n++;
						if (n < 2) failures.Add($"{key} has {n} annoyed lines, needs 2");
						for (int i = 0; i < 20; i++)
						{
							RosterFacts.TestBloodMoon = true;
							string a = DialogueSystem.PickKey(key, fem, ctx, out DialogueLine pf);
							RosterFacts.TestBloodMoon = false;
							string b = DialogueSystem.PickKey(key, fem, ctx, out DialogueLine pc);
							if (a == null || (pf.Cond & LineCond.Grumpy) == 0) { failures.Add($"{key}: a female officer on a Blood Moon was handed {(a == null ? "nothing" : "a normal line")}"); break; }
							if (b == null || (pc.Cond & LineCond.Grumpy) != 0) { failures.Add($"{key}: a female officer off a Blood Moon was handed {(b == null ? "nothing" : "an annoyed line")}"); break; }
						}
					}
				RosterFacts.TestBloodMoon = null;
				foreach (string trig in new[] { "OFFICER_MARCH", "NURSE_MARCH" })
				{
					int n = 0; var pool = DialogueTable.Get(trig);
					if (pool != null) foreach (DialogueLine l in pool) if ((l.Cond & LineCond.Grumpy) != 0) n++;
					if (n < 6) failures.Add($"the annoyed {trig} set has {n} lines, needs 6");
				}

				// ---- where a kit ranks in the army, and the cheers of the five drinks
				var rich = Make("Rich", 10); rich.Gear.Weapon = new Item(); rich.Gear.Weapon.SetDefaults(ItemID.Zenith);
				var mid1 = Make("Mid1", 10); mid1.Gear.Weapon = new Item(); mid1.Gear.Weapon.SetDefaults(ItemID.IronBroadsword);
				var mid2 = Make("Mid2", 10); mid2.Gear.Weapon = new Item(); mid2.Gear.Weapon.SetDefaults(ItemID.GoldBroadsword);
				var poor = Make("Poor", 10); poor.Gear.Weapon = new Item(); poor.Gear.Weapon.SetDefaults(ItemID.WoodenSword);
				var simple = Make("Simple", 10); simple.Gear.Weapon = new Item(); simple.Gear.Weapon.SetDefaults(ItemID.CopperShortsword);
				foreach (var r in new[] { rich, mid1, mid2, poor, simple }) r.CommanderName = "Cap";
				RosterFacts.TestRoster = new List<SoldierRecord> { rich, mid1, mid2, poor, simple };
				if (RosterFacts.KitStanding(rich) != RosterFacts.KitRank.Best) failures.Add("the richest kit was not ranked best: " + RosterFacts.KitStanding(rich));
				if (RosterFacts.KitStanding(poor) != RosterFacts.KitRank.Worst) failures.Add("the poorest kit was not ranked worst: " + RosterFacts.KitStanding(poor));
				if (DialogueSystem.ArmoryTriggerFor(rich) != DialogueTrigger.ARMORY_BEST || DialogueSystem.ArmoryTriggerFor(poor) != DialogueTrigger.ARMORY_WORST) failures.Add("the armory chat trigger did not follow the ranking");
				RosterFacts.TestRoster = new List<SoldierRecord> { rich, poor };
				if (RosterFacts.KitStanding(rich) != RosterFacts.KitRank.None) failures.Add("a two-levy army still ranked a best kit");
				RosterFacts.TestRoster = new List<SoldierRecord> { fem, man, ranger, melee, femRanger };
				for (int d = 0; d < 5; d++)
				{
					DialogueTrigger t = DrinkCheers.TriggerFor(d);
					if (t == DialogueTrigger.DRINK || DialogueTable.Get(t) == null) failures.Add($"drink {d} has no pool of its own ({t})");
					if (DrinkCheers.CheerLine(d, man, ctx) == null) failures.Add($"drink {d} gave a levy no cheer");
					RosterFacts.TestBloodMoon = true;
					if (DrinkCheers.CheerLine(d, fem, ctx) == null) failures.Add($"drink {d} gave a female levy no cheer on a Blood Moon");
					RosterFacts.TestBloodMoon = null;
				}
				if (DrinkCheers.TriggerFor(-1) != DialogueTrigger.DRINK || DrinkCheers.TriggerFor(9) != DialogueTrigger.DRINK) failures.Add("an unknown drink does not fall back to DRINK");
				checkedLines += tagged;
			}
			catch (Exception e) { failures.Add("tag and override check threw: " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace); }
			finally { RosterFacts.TestRoster = null; RosterFacts.TestBloodMoon = null; RosterFacts.TestStage = null; }
		}
	}
}
