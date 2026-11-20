using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Boot self-check of the rift mood ("[RiftMood]" lines). With NoxusBoss absent: it reports "not installed", every read is off, the mood is None and nothing in the dialogue changes.
	/// With NoxusBoss: every member is found by reflection, the real values read off at boot, and the real members are driven (the sun covered, the covering scene in progress, snow lying)
	/// and read back through the mood, then put back. Then, on overrides: the lines parse; every rift line fills its placeholders ({Mage} {Summoner} {Ranger} {Melee} name the right levy and
	/// never the speaker); silence while the sun is covered; the worried pool right after; the calm and joking pools answered only from rift pools and the hushed ones silent; combat pools
	/// untouched; shock lines only while tense, "what now" lines only from the later day; the absent and cold pools; the rift voice over the Blood Moon's; and the floating share per phase.
	/// Every global it touches (RNG, the roster, the overrides, the real NoxusBoss members, Main.raining) is put back.
	/// </summary>
	public partial class RiftMoodCheck : ModSystem
	{
		private int _pass, _fail;

		public override void PostAddRecipes() => global::Levy.Common.Util.LoadSafe.Run(Core);

		private void Check(string name, bool ok) { if (ok) _pass++; else _fail++; Mod.Logger.Info($"[RiftMood] {(ok ? "PASS" : "FAIL")} {name}"); }
		private void Note(string s) => Mod.Logger.Info("[RiftMood] OBSERVED " + s);

		private static SoldierRecord Make(string name, Temperament t, int item = 0, bool male = true)
		{
			var r = new SoldierRecord { Id = ("rift" + name).GetHashCode(), Name = name, Temperament = t, LifeCrystals = 10, Male = male, CommanderName = "Cap" };
			if (item > 0) { r.Gear.Weapon = new Item(); r.Gear.Weapon.SetDefaults(item); }
			return r;
		}

		private static readonly DialogueTrigger[] Rift = { DialogueTrigger.RIFT_PANIC, DialogueTrigger.RIFT_SHOCK, DialogueTrigger.RIFT_ABSENT, DialogueTrigger.RIFT_COLD, DialogueTrigger.RIFT_LATER, DialogueTrigger.RIFT_RESPECT, DialogueTrigger.RIFT_IDLE, DialogueTrigger.RIFT_CAMP, DialogueTrigger.RIFT_DARK, DialogueTrigger.RIFT_POWER, DialogueTrigger.RIFT_DUKE,
			DialogueTrigger.RIFT_BLOOD, DialogueTrigger.RIFT_GROW1, DialogueTrigger.RIFT_GROW2, DialogueTrigger.RIFT_GROW3, DialogueTrigger.RIFT_ICE, DialogueTrigger.RIFT_FROZEN, DialogueTrigger.RIFT_FOG, DialogueTrigger.RIFT_SCARE, DialogueTrigger.RIFT_GRIM, DialogueTrigger.RIFT_WORN,
			DialogueTrigger.RIFT_HUSH, DialogueTrigger.RIFT_ANGRY, DialogueTrigger.RIFT_BOSS, DialogueTrigger.RIFT_TOWN, DialogueTrigger.RIFT_CLASS, DialogueTrigger.RIFT_HABIT, DialogueTrigger.RIFT_FLIGHT };

		private void Core()
		{
			_pass = _fail = 0;
			var rand0 = Main.rand; bool rain0 = Main.raining;
			try
			{
				Main.rand = new Terraria.Utilities.UnifiedRandom(9091);
				Detection();
				Lines();
				Mood();
			}
			catch (Exception e) { Check("the check threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				RiftSky.ClearOverrides(); RiftMood.TestPhase = null; RiftMood.TestPanic = RiftMood.TestSurface = null; RiftMood.TestDays = null; RiftMood.Reset();
				RosterFacts.TestRoster = null; RosterFacts.TestBloodMoon = null;
				Main.rand = rand0; Main.raining = rain0;
				DialogueSystem.ResetRuntime();
			}
			Mod.Logger.Info($"[RiftMood] self-check pass={_pass} fail={_fail}");
		}

		// ---- 1. detection, by reflection, with and without the mod ----
		private void Detection()
		{
			bool has = ModLoader.HasMod(RiftSky.ModName);
			bool resolved = RiftSky.Resolve();
			Check($"NoxusBoss {(has ? "is loaded and was found" : "is not loaded and is reported so")} (installed={RiftSky.Installed}, problem: {(RiftSky.Problem.Length == 0 ? "none" : RiftSky.Problem)})", resolved == has && RiftSky.Installed == has && (has || RiftSky.Problem == "not installed"));
			if (!has)
			{
				bool quiet = !RiftSky.Transition && !RiftSky.Covered && !RiftSky.Defeated && !RiftSky.Snowing && RiftSky.SnowHeight == 0f && !RiftSky.Fog && !RiftSky.RiftOngoing;
				Check("without NoxusBoss every sky read is off and nothing throws", quiet);
				Check("without NoxusBoss the mood is None and every pool routes as normal", RiftMood.Phase == RiftPhase.None && RiftMood.Route(DialogueTrigger.IDLE) == RiftRoute.Normal && RiftMood.Route(DialogueTrigger.FIRESIDE) == RiftRoute.Normal && RiftMood.AllowFloating((byte)DialogueTrigger.IDLE));
				Check("without NoxusBoss setting a real member is refused", !RiftSky.SetReal(covered: true));
				RiftMood.Tick((int)Main.GameUpdateCount); RiftMood.Tick((int)Main.GameUpdateCount + 1);
				Check("without NoxusBoss the watcher records nothing", !RiftMood.Seen && RiftMood.Changes == 0);
				return;
			}
			Note("members found: " + RiftSky.Found);
			Check("every NoxusBoss member resolved (covered flag, scene, timer, armed flag, rift flag, snow, fog, the scene's instance)", RiftSky.Problem.Length == 0);
			var real = RiftSky.ReadReal();
			Note($"at boot: covered={real.covered} scene={real.ongoing} timer={real.timer} armed={real.waiting} rift={real.rift} snow={real.snow:F2} fog={real.fog} defeated={RiftSky.Defeated}");
			Check("at boot (no world) the sky reads untouched and the mood is None", !real.covered && !real.ongoing && RiftMood.Phase == RiftPhase.None);
			try
			{
				// the covering scene in progress: the real timer set as the mod's own start sets it
				Check("the real scene timer can be set", RiftSky.SetReal(eventTimer: 5));
				Check($"with the scene running the sky reads as being covered and the mood is Silent (transition={RiftSky.Transition}, phase={RiftMood.Phase})", RiftSky.Transition && !RiftSky.Covered && RiftMood.Phase == RiftPhase.Silent);
				// the scene over, the sun covered
				RiftSky.SetReal(eventTimer: 0, covered: true);
				Check($"with the sun covered the sky reads covered, NoxusBoss's own rift flag agrees, and the mood is Tense (covered={RiftSky.Covered}, rift={RiftSky.RiftOngoing}, phase={RiftMood.Phase})", RiftSky.Covered && !RiftSky.Transition && RiftSky.RiftOngoing && RiftMood.Phase == RiftPhase.Tense);
				Main.raining = true;
				Check("rain under the covered sky reads as snow falling", RiftSky.Snowing);
				Main.raining = false;
				RiftSky.SetReal(snowHeight: 4f);
				Check($"snow lying is read ({RiftSky.SnowHeight:F1})", Math.Abs(RiftSky.SnowHeight - 4f) < 0.01f);
				// the watcher sees the scene end
				RiftMood.Reset(); int c0 = RiftMood.Changes;
				RiftSky.SetReal(covered: false, eventTimer: 5); RiftMood.Tick(1000);
				RiftSky.SetReal(covered: true, eventTimer: 0); RiftMood.Tick(1001);
				bool dk0 = RiftSky.DukeKilled; RiftSky.SetReal(dukeKilled: true); bool dk1 = RiftSky.DukeKilled; RiftSky.SetReal(dukeKilled: false);
				Check($"the swallowed-boss flag is read and can be driven (off={!dk0}, on={dk1}); the Old Duke type is {(RiftSky.DukeType >= 0 ? "found" : "missing")}", !dk0 && dk1 && (RiftSky.DukeType >= 0 || !ModLoader.HasMod("CalamityMod")));
				Check($"the watcher sees the real scene end: one covering, the panic clock started, day 0 (changes {RiftMood.Changes - c0}, panic={RiftMood.PanicNow}, days={RiftMood.DaysSince})", RiftMood.Changes == c0 + 1 && RiftMood.Seen && RiftMood.DaysSince == 0);
			}
			finally
			{
				RiftSky.SetReal(covered: false, eventTimer: 0, waiting: false, snowHeight: 0f, dukeKilled: false);
				RiftMood.Reset();
			}
			var back = RiftSky.ReadReal();
			Check("the real members are put back", !back.covered && back.timer == 0 && !back.waiting && back.snow == 0f && RiftMood.Phase == RiftPhase.None);
		}

		// ---- 2. the lines: parse cleanly, sizes, placeholders ----
		private void Lines()
		{
			var into = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
			int bad = -1;
			try { bad = DialogueTable.Parse(null, "riftcheck", new System.Text.UTF8Encoding(false).GetString(Mod.GetFileBytes("Dialogue/en-US.txt")).TrimStart((char)0xFEFF), into); }
			catch (Exception e) { Note("could not read the packed file: " + e.Message); }
			Check($"the packed dialogue file parses with no bad line ({bad} rejected)", bad == 0);
			int total = 0;
			var mins = new Dictionary<DialogueTrigger, int> { [DialogueTrigger.RIFT_PANIC] = 18, [DialogueTrigger.RIFT_SHOCK] = 10, [DialogueTrigger.RIFT_ABSENT] = 6, [DialogueTrigger.RIFT_COLD] = 8, [DialogueTrigger.RIFT_LATER] = 6, [DialogueTrigger.RIFT_RESPECT] = 8, [DialogueTrigger.RIFT_IDLE] = 15, [DialogueTrigger.RIFT_CAMP] = 8, [DialogueTrigger.RIFT_DARK] = 10, [DialogueTrigger.RIFT_POWER] = 6, [DialogueTrigger.RIFT_DUKE] = 6,
				[DialogueTrigger.RIFT_BLOOD] = 12, [DialogueTrigger.RIFT_GROW1] = 8, [DialogueTrigger.RIFT_GROW2] = 8, [DialogueTrigger.RIFT_GROW3] = 8, [DialogueTrigger.RIFT_ICE] = 6, [DialogueTrigger.RIFT_FROZEN] = 6,
				[DialogueTrigger.RIFT_FOG] = 8, [DialogueTrigger.RIFT_SCARE] = 6, [DialogueTrigger.RIFT_GRIM] = 8, [DialogueTrigger.RIFT_WORN] = 8,
				[DialogueTrigger.RIFT_HUSH] = 4, [DialogueTrigger.RIFT_BOSS] = 12, [DialogueTrigger.RIFT_TOWN] = 12, [DialogueTrigger.RIFT_CLASS] = 12, [DialogueTrigger.RIFT_HABIT] = 10, [DialogueTrigger.RIFT_FLIGHT] = 6, [DialogueTrigger.RIFT_ANGRY] = 20 };
			foreach (DialogueTrigger t in Rift)
			{
				int n = DialogueTable.Get(t)?.Count ?? 0; total += n;
				Check($"{t} has {n} lines (needs {mins[t]})", n >= mins[t]);
			}
			Check($"{total} rift lines in all (300 to 450)", total >= 300 && total <= 450);

			// every line: ASCII, short, no annoyed tag, every placeholder filled for a speaker it suits
			var mia = Make("Mia", Temperament.Greenhorn, ItemID.WandofSparking);
			var sam = Make("Sam", Temperament.Sellsword, ItemID.SlimeStaff);
			var rae = Make("Rae", Temperament.Zealot, ItemID.Handgun);
			var mel = Make("Mel", Temperament.Cynic, ItemID.CopperShortsword);
			var pat = Make("Pat", Temperament.Cynic); pat.Veteran = true;
			var tess = Make("Tess", Temperament.Zealot, 0, false); tess.Deaths = 6;
			var gus = Make("Gus", Temperament.Sellsword); gus.Kills = 40;
			var army = new List<SoldierRecord> { mia, sam, rae, mel, pat, tess, gus };
			RosterFacts.TestRoster = army;
			var ctx = new DialogueContext { Player = "Cap", Comrade = "Bo" };
			int filled = 0, townLines = 0, rogueLines = 0;
			foreach (DialogueTrigger t in Rift)
			{
				var pool = DialogueTable.Get(t); if (pool == null) continue;
				foreach (DialogueLine l in pool)
				{
					string who = t + ": " + (l.Text.Length > 36 ? l.Text.Substring(0, 36) : l.Text);
					bool ascii = true; foreach (char ch in l.Text) if (ch < 32 || ch > 126) ascii = false;
					if (!ascii) Check(who + " is plain ASCII", false);
					int cap = t == DialogueTrigger.RIFT_PANIC ? LevyConstants.RiftPanicLineMaxChars : 60; // panic lines are shouted and may run longer
					if (l.Text.Length > cap) Check($"{who} is short ({l.Text.Length} chars, {cap} at most)", false);
					if ((l.Cond & LineCond.Grumpy) != 0) Check(who + " carries no [grumpy] tag", false);
					// a line naming a town NPC needs that NPC alive (none at boot): it must have parsed the NPC; a rogue line needs a rogue class (Calamity)
					if (l.NpcTypes.Length > 0) { townLines++; filled++; continue; }
					if (((l.Cond & LineCond.Rogue) != 0 || (l.Needs & LineNeeds.Rogue) != 0) ) { rogueLines++; filled++; continue; }
					string got = null;
					foreach (SoldierRecord sp in army)
					{
						if (l.Filter != LineFilter.Any && l.Filter != LineFilter.Veteran && (int)sp.Temperament + 1 != (int)l.Filter) continue;
						if (l.Filter == LineFilter.Veteran && !sp.Veteran) continue;
						got = PickOne(l, sp, ctx);
						if (got != null) break;
					}
					if (got == null || got.IndexOf('{') >= 0) Check($"{who} fills for some levy in a full army ({got ?? "nothing"})", false);
					else filled++;
				}
			}
			Check($"every rift line fills for a levy it suits ({filled} of {total}; {townLines} town lines parsed their NPC, {rogueLines} rogue lines need a rogue weapon, checked by tag below)", filled == total && townLines >= 12 && rogueLines >= 3);
			var into3 = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
			int bad3 = DialogueTable.Parse(null, "riftcheck3", "RIFT_CLASS | Any | [rogue] a\nRIFT_CLASS | Any | {Rogue} b", into3);
			Check("[rogue] and {Rogue} parse to the rogue tag and placeholder", bad3 == 0 && into3["RIFT_CLASS"][0].Cond == LineCond.Rogue && into3["RIFT_CLASS"][1].Needs == LineNeeds.Rogue);

			// the class names: the right levy, never the speaker, skipped with nobody of that class
			DialogueLine mageLine = Find(DialogueTrigger.RIFT_RESPECT, "{Mage} is hitting");
			DialogueLine sumLine = Find(DialogueTrigger.RIFT_RESPECT, "{Summoner}'s minions take");
			DialogueLine rangerLine = Find(DialogueTrigger.RIFT_RESPECT, "{Ranger}'s bullets");
			DialogueLine meleeLine = Find(DialogueTrigger.RIFT_RESPECT, "{Melee} went right");
			string byPat = mageLine != null ? PickOne(mageLine, pat, ctx) : null, byMia = mageLine != null ? PickOne(mageLine, mia, ctx) : null;
			Check($"{{Mage}} names the army's mage to another levy ({byPat})", byPat != null && byPat.StartsWith("Mia "));
			Check($"{{Mage}} is never the speaker herself: with no other mage the line is skipped ({byMia ?? "skipped"})", byMia == null);
			string s1 = sumLine != null ? PickOne(sumLine, pat, ctx) : null, r1 = rangerLine != null ? PickOne(rangerLine, pat, ctx) : null, m1 = meleeLine != null ? PickOne(meleeLine, pat, ctx) : null;
			Check($"{{Summoner}} {{Ranger}} {{Melee}} name Sam, Rae and Mel ({s1} / {r1} / {m1})", s1 != null && s1.StartsWith("Sam") && r1 != null && r1.Contains("Rae") && m1 != null && m1.StartsWith("Mel "));
			var mia2 = Make("Mia2", Temperament.Cynic, ItemID.WandofSparking); mia2.Reserve = true;
			var other = Make("Other", Temperament.Cynic, ItemID.WandofSparking); other.CommanderName = "SomeoneElse";
			RosterFacts.TestRoster = new List<SoldierRecord> { pat, mia2, other };
			Check("{Mage} skips a mage in reserve and one in another commander's army", mageLine == null || PickOne(mageLine, pat, ctx) == null);
			RosterFacts.TestRoster = army;
			var into2 = new Dictionary<string, List<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
			int bad2 = DialogueTable.Parse(null, "riftcheck2", "RIFT_IDLE | Any | {Mage} {Summoner} {Ranger} {Melee}\nRIFT_IDLE | Any | {Wizardly}", into2);
			Check("the four class placeholders parse and an unknown one is still rejected", bad2 == 1 && into2.TryGetValue("RIFT_IDLE", out var il) && il.Count == 1 && il[0].Needs == (LineNeeds.Mage | LineNeeds.Summoner | LineNeeds.Ranger | LineNeeds.Melee));
		}

		private static string PickOne(DialogueLine l, SoldierRecord sp, DialogueContext ctx)
		{
			return DialogueSystem.RenderOneCtx(l, sp, ctx);
		}

		private static DialogueLine Find(DialogueTrigger t, string start)
		{
			var pool = DialogueTable.Get(t);
			if (pool != null) foreach (DialogueLine l in pool) if (l.Text.StartsWith(start, StringComparison.Ordinal) || l.Text.Contains(start)) return l;
			return null;
		}

		private static bool InRift(DialogueLine l)
		{
			if (l == null) return false;
			foreach (DialogueTrigger t in Rift) { var pool = DialogueTable.Get(t); if (pool != null && pool.Contains(l)) return true; }
			return false;
		}

		/// <summary>
		/// The panic is shouted: every line has an exclamation mark, most have a word in capitals, every temperament and the Veteran have their own; filled with a long name each wraps on a chat bar
		/// in at most the preferred rows (the wrap estimate of TalkingCheck, capitals counted wide). The loud first minute: on right after the covering, off after a minute and while it is covered,
		/// and its gaps give ten levies a line every second or two, at least 20 a minute, where the usual squad gap allows a handful.
		/// </summary>
		private void LoudPanic()
		{
			var pool = DialogueTable.Get(DialogueTrigger.RIFT_PANIC);
			int shout = 0, caps = 0, maxRows = 0; var per = new int[6]; string worst = null;
			float sc = LevyConstants.ChatBarTextScale;
			Func<string, float> measure = s => { float w = 0f; foreach (char c in s) w += (c >= 'A' && c <= 'Z') || c == 'm' || c == 'w' ? 9f : 7f; return w * sc; };
			foreach (DialogueLine l in pool)
			{
				if (l.Text.IndexOf('!') >= 0) shout++;
				if (System.Text.RegularExpressions.Regex.IsMatch(l.Text, "\\b[A-Z]{2,}\\b")) caps++;
				per[Math.Clamp((int)l.Filter, 0, 5)]++;
				string filled = System.Text.RegularExpressions.Regex.Replace(l.Text, "\\{[^}]*\\}", "Bartholomew-Featherstonehaugh");
				int rows = global::Levy.Common.UI.ChatWrap.Fit(filled, measure, LevyConstants.ChatBarTextWidth, LevyConstants.ChatBarWideTextWidth, LevyConstants.ChatBarPreferredRows, out _).Count;
				if (rows > maxRows) { maxRows = rows; worst = l.Text; }
			}
			bool everyVoice = true; foreach (LineFilter f in new[] { LineFilter.Greenhorn, LineFilter.Sellsword, LineFilter.Zealot, LineFilter.Cynic, LineFilter.Veteran }) if (per[(int)f] < 3) everyVoice = false;
			Check($"the panic is shouted: {shout} of {pool.Count} lines with an exclamation mark, {caps} with a word in capitals, every voice at least 3 lines (any {per[(int)LineFilter.Any]})", shout == pool.Count && caps * 2 >= pool.Count && everyVoice);
			Check($"every panic line, a long name filled in, fits a chat bar in {LevyConstants.ChatBarPreferredRows} rows (at most {maxRows}: {worst})", maxRows <= LevyConstants.ChatBarPreferredRows);

			RiftMood.SetPanic(true);
			bool loud = RiftMood.PanicLoud, worried = RiftMood.PanicNow;
			RiftPhase? tp0 = RiftMood.TestPhase; RiftMood.TestPhase = RiftPhase.Silent; bool loudSilent = RiftMood.PanicLoud; RiftMood.TestPhase = tp0;
			RiftMood.SetPanic(false);
			bool window = RiftMood.LoudAt(0) && RiftMood.LoudAt(LevyConstants.RiftPanicLoudTicks - 1) && !RiftMood.LoudAt(LevyConstants.RiftPanicLoudTicks) && !RiftMood.LoudAt(-1) && LevyConstants.RiftPanicLoudTicks < LevyConstants.RiftPanicTicks;
			Check($"the loud minute: on right after the covering ({loud}, worried {worried}), off once cleared ({RiftMood.PanicLoud}) and while the sun is covered ({loudSilent}), {LevyConstants.RiftPanicLoudTicks / 60} s of the {LevyConstants.RiftPanicTicks / 60} s worried talk", loud && worried && !RiftMood.PanicLoud && !loudSilent && window);
			// the squad's pace: one line per gap plus the mean retry; ten levies, each ready every few seconds, keep the squad saturated
			float squadPerMin = 3600f / (LevyConstants.RiftPanicSquadGapTicks + (10 + LevyConstants.RiftPanicRetryMaxTicks) / 2f);
			float tenPerMin = 10 * 3600f / ((LevyConstants.RiftPanicChatterMinTicks + LevyConstants.RiftPanicChatterMaxTicks) / 2f);
			float usualPerMin = 3600f / LevyConstants.ChatterSquadGapTicks;
			Check($"the loud minute's pace: the squad gap allows {squadPerMin:F0} lines a minute, ten levies offer {tenPerMin:F0}, the usual gap {usualPerMin:F0}", Math.Min(squadPerMin, tenPerMin) >= 30f && squadPerMin >= 3f * usualPerMin);
		}

		private static bool In(DialogueTrigger t, DialogueLine l) => l != null && (DialogueTable.Get(t)?.Contains(l) ?? false);

		// ---- 3. the mood, on overrides ----
		private void Mood()
		{
			var pat = Make("Pat", Temperament.Cynic);
			var mia = Make("Mia", Temperament.Greenhorn, ItemID.WandofSparking);
			var sam = Make("Sam", Temperament.Sellsword, ItemID.SlimeStaff);
			var fay = Make("Fay", Temperament.Zealot, 0, false);
			RosterFacts.TestRoster = new List<SoldierRecord> { pat, mia, sam, fay };
			var ctx = new DialogueContext { Player = "Cap", Comrade = "Bo", Boss = "Thing", BossKey = "EyeofCthulhu", Killer = "a Zombie", Town = "Town", Wage = "1 silver" };
			var all = (DialogueTrigger[])Enum.GetValues(typeof(DialogueTrigger));

			// none: the mood off changes nothing
			RiftSky.TestInstalled = true; RiftSky.TestCovered = false; RiftSky.TestTransition = false; RiftSky.TestDefeated = false;
			Check($"installed but before the covering the mood is None ({RiftMood.Phase})", RiftMood.Phase == RiftPhase.None);
			string n0 = DialogueSystem.Pick(DialogueTrigger.IDLE, pat, ctx, null, true, -1, out DialogueLine pn0, false);
			Check($"before the covering a levy speaks his normal idle lines ({n0})", n0 != null && In(DialogueTrigger.IDLE, pn0));
			RiftSky.TestCovered = true; RiftSky.TestDefeated = true;
			Check("after the sky is freed the mood is None again", RiftMood.Phase == RiftPhase.None);
			RiftSky.TestDefeated = false;

			// silent: the sun is being covered
			RiftSky.TestCovered = false; RiftSky.TestTransition = true;
			Check($"while the sun is covered the mood is Silent ({RiftMood.Phase})", RiftMood.Phase == RiftPhase.Silent && RiftMood.Hushed);
			int spoke = 0, floats = 0; string spokeWhat = null;
			foreach (DialogueTrigger t in all)
			{
				if (t == DialogueTrigger.BOSS) continue;
				string a = DialogueSystem.Pick(t, pat, ctx, null, true, -1, out _, false);
				if (a != null && !RiftMood.IsNotice(t)) { spoke++; spokeWhat ??= t + ": " + a; }
				if (RiftMood.AllowFloating((byte)t)) floats++;
			}
			if (RiftMood.AllowFloating(byte.MaxValue)) floats++;
			Check($"while the sun is covered no pool gives a line and no floating line goes out ({spoke} lines{(spokeWhat != null ? ", e.g. " + spokeWhat : "")}, {floats} floats)", spoke == 0 && floats == 0);
			Check("while the sun is covered the chat-log notices still post (a death message)", DialogueSystem.Pick(DialogueTrigger.DEATH_MESSAGE, pat, ctx, null, true, -1, out _, false) != null);
			Check("while the sun is covered a mutineer's shout falls silent too (its string-keyed pool)", DialogueSystem.PickLine("MUTINY", pat, ctx, true) == null || !RiftMood.AllowFloating((byte)DialogueTrigger.MUTINY));

			// the scene ends: the panic
			RiftSky.TestTransition = false; RiftSky.TestCovered = true; RiftMood.TestDays = 0;
			RiftMood.Reset(); RiftMood.Tick(500); RiftSky.TestTransition = true; RiftSky.TestCovered = false; RiftMood.Tick(501); RiftSky.TestTransition = false; RiftSky.TestCovered = true;
			int ch0 = RiftMood.Changes; RiftMood.Tick(502);
			Check($"the watcher sees the covering end once and starts the worried talk (changes {RiftMood.Changes - ch0}, panic={RiftMood.PanicNow})", RiftMood.Changes == ch0 + 1);
			RiftMood.TestPanic = true;
			int panic = 0;
			for (int i = 0; i < 20; i++) { DialogueSystem.Pick(DialogueTrigger.IDLE, pat, ctx, null, true, -1, out DialogueLine pl, false); if (In(DialogueTrigger.RIFT_PANIC, pl)) panic++; }
			Check($"right after, chat is worried ({panic}/20 RIFT_PANIC)", panic == 20);
			string direct = DialogueSystem.Pick(DialogueTrigger.RIFT_PANIC, pat, ctx, null, true, -1, out _, false);
			Check($"the burst draws from RIFT_PANIC ({direct}) and every floating line goes out", direct != null && RiftMood.AllowFloating((byte)DialogueTrigger.RIFT_PANIC) && RiftMood.AllowFloating((byte)DialogueTrigger.IDLE));
			LoudPanic();
			RiftMood.TestPanic = false;

			// tense: the covered pools, the hushed ones, the untouched ones
			RiftMood.TestPhase = RiftPhase.Tense; RiftMood.TestDays = 0;
			int routed = 0, leaked = 0, hushedSpoke = 0, combatOk = 0, combatPools = 0; string leak = null;
			foreach (DialogueTrigger t in all)
			{
				if (t == DialogueTrigger.BOSS || RiftMood.IsRift(t)) continue;
				RiftRoute route = RiftMood.Covered(t);
				for (int i = 0; i < 12; i++)
				{
					string a = DialogueSystem.Pick(t, pat, ctx, null, true, -1, out DialogueLine pl, false);
					if (route == RiftRoute.Chat || route == RiftRoute.Camp)
					{
						if (a == null) continue;
						if (InRift(pl)) routed++; else { leaked++; leak ??= t + ": " + a; }
						if (route == RiftRoute.Camp && !(In(DialogueTrigger.RIFT_CAMP, pl) || In(DialogueTrigger.RIFT_IDLE, pl))) { leaked++; leak ??= "camp " + t + ": " + a; }
					}
					else if (route == RiftRoute.Hush) { if (a != null) { hushedSpoke++; leak ??= "hushed " + t + ": " + a; } }
				}
			}
			foreach (DialogueTrigger t in new[] { DialogueTrigger.ENGAGE, DialogueTrigger.KILL, DialogueTrigger.HURT, DialogueTrigger.COMRADE_DOWN, DialogueTrigger.PAYDAY, DialogueTrigger.UNPAID, DialogueTrigger.VICTORY, DialogueTrigger.DEATH_MESSAGE })
			{
				combatPools++;
				string a = DialogueSystem.Pick(t, pat, ctx, null, true, -1, out DialogueLine pl, false);
				if (a != null && In(t, pl)) combatOk++;
			}
			Check($"in the tense days every calm and joking pool answers only from the rift pools ({routed} rift lines, {leaked} leaks{(leak != null ? ": " + leak : "")})", routed > 100 && leaked == 0);
			Check($"the crystal, chest and gear lines and the flying whoops go quiet ({hushedSpoke} spoken)", hushedSpoke == 0);
			int flown = 0, flyOther = 0;
			foreach (DialogueTrigger ft in new[] { DialogueTrigger.TAKEOFF, DialogueTrigger.FLIGHT_FEAR, DialogueTrigger.LANDING })
				for (int i = 0; i < 20; i++) { DialogueSystem.Pick(ft, pat, ctx, null, true, -1, out DialogueLine pf, false); if (In(DialogueTrigger.RIFT_FLIGHT, pf)) flown++; else flyOther++; }
			string ecl = DialogueSystem.Pick(DialogueTrigger.ECLIPSE_END, pat, ctx, null, true, -1, out DialogueLine pe, false);
			Check($"flying under the rift is answered from the flight set ({flown} of 60, {flyOther} other); a solar eclipse ending keeps its own lines ({ecl})", flown == 60 && ecl != null && In(DialogueTrigger.ECLIPSE_END, pe));
			Check($"combat, pay, victory and death lines keep their own pools ({combatOk}/{combatPools})", combatOk == combatPools);
			int grimBoss = 0, plainBoss = 0;
			for (int i = 0; i < 200; i++) { DialogueSystem.Pick(DialogueTrigger.BOSS_SIGHTED, pat, ctx, null, true, -1, out DialogueLine pb, false); if (In(DialogueTrigger.RIFT_BOSS, pb)) grimBoss++; else if (In(DialogueTrigger.BOSS_SIGHTED, pb)) plainBoss++; }
			Check($"under the rift a boss is met grimly about {LevyConstants.RiftBossChance:P0} of the time ({grimBoss} grim, {plainBoss} usual in 200)", grimBoss > 120 && grimBoss < 180 && plainBoss > 20);
			string drink = DialogueSystem.PickLine("DRINK", pat, ctx, true);
			Check($"the drink fallback asked for by name answers from the camp pools ({drink})", drink != null && (Contains(DialogueTrigger.RIFT_CAMP, drink) || Contains(DialogueTrigger.RIFT_IDLE, drink)));
			string mut = DialogueSystem.PickLine("MUTINY", pat, ctx, true);
			Check($"the mutiny cries are untouched ({mut})", mut != null && Contains(DialogueTrigger.MUTINY, mut));

			// shock only while tense, "what now" only from the later day
			int shockT = CountPool(DialogueTrigger.RIFT_SHOCK, pat, ctx, 200), laterT = CountPool(DialogueTrigger.RIFT_LATER, pat, ctx, 200);
			Check($"tense, day 0: shock lines are spoken and no \"what now\" line yet ({shockT} shock, {laterT} later in 200)", shockT > 20 && laterT == 0);
			RiftMood.TestDays = LevyConstants.RiftLaterFromDay;
			int laterT2 = CountPool(DialogueTrigger.RIFT_LATER, pat, ctx, 200);
			Check($"tense, day {LevyConstants.RiftLaterFromDay}: \"what now\" lines start ({laterT2} in 200)", laterT2 > 15);
			// weary strength from the first dawn, dark humour only once settled
			RiftMood.TestDays = 0;
			int pow0 = CountPool(DialogueTrigger.RIFT_POWER, pat, ctx, 200), dark0 = CountPool(DialogueTrigger.RIFT_DARK, pat, ctx, 200);
			RiftMood.TestDays = LevyConstants.RiftPowerFromDay;
			int pow1 = CountPool(DialogueTrigger.RIFT_POWER, pat, ctx, 200), dark1 = CountPool(DialogueTrigger.RIFT_DARK, pat, ctx, 200);
			Check($"day 0 has no weary-strength and no dark lines; from day {LevyConstants.RiftPowerFromDay} the weary ones come, still no dark ones ({pow0}/{dark0} then {pow1}/{dark1} in 200)", pow0 == 0 && dark0 == 0 && pow1 > 4 && dark1 == 0);
			RiftMood.TestPhase = RiftPhase.Settled; RiftMood.TestDays = LevyConstants.RiftDarkFromDay;
			int darkS = CountPool(DialogueTrigger.RIFT_DARK, pat, ctx, 200);
			Check($"settled, day {LevyConstants.RiftDarkFromDay}: the dark jokes come back ({darkS} in 200)", darkS > 12);
			RiftMood.TestPhase = RiftPhase.Settled; RiftMood.TestDays = LevyConstants.RiftTenseDays;
			int shockS = CountPool(DialogueTrigger.RIFT_SHOCK, pat, ctx, 200), laterS = CountPool(DialogueTrigger.RIFT_LATER, pat, ctx, 200), idleS = CountPool(DialogueTrigger.RIFT_IDLE, pat, ctx, 200);
			Check($"settled: no shock lines, \"what now\" and quiet talk go on ({shockS} shock, {laterS} later, {idleS} idle in 200)", shockS == 0 && laterS > 5 && idleS > 10);
			RiftMood.TestPhase = null; RiftMood.TestDays = null;
			RiftSky.TestTransition = false; RiftSky.TestCovered = true;
			RiftMood.DaysSince = LevyConstants.RiftTenseDays - 1;
			bool tenseBefore = RiftMood.Phase == RiftPhase.Tense;
			RiftMood.DaysSince = LevyConstants.RiftTenseDays;
			Check($"the real phase follows the day count: tense before dawn {LevyConstants.RiftTenseDays}, settled from it", tenseBefore && RiftMood.Phase == RiftPhase.Settled);
			RiftMood.DaysSince = 0;
			RiftMood.TestPhase = RiftPhase.Tense;

			// who was away asks what happened, and stops after asking enough
			RiftMood.Away.Clear(); RiftMood.Away.Add(pat.Id);
			int absent = 0;
			for (int i = 0; i < 30 && RiftMood.Away.Contains(pat.Id); i++) { DialogueSystem.Pick(DialogueTrigger.IDLE, pat, ctx, null, true, -1, out DialogueLine pl, true); if (In(DialogueTrigger.RIFT_ABSENT, pl)) absent++; }
			Check($"a levy who was away asks what happened, {LevyConstants.RiftAwayLines} times, then has caught up ({absent} asked, still away={RiftMood.Away.Contains(pat.Id)})", absent == LevyConstants.RiftAwayLines && !RiftMood.Away.Contains(pat.Id));
			Check("a levy who was there never asks", CountPool(DialogueTrigger.RIFT_ABSENT, mia, ctx, 100) == 0);
			DialogueSystem.ResetRuntime();

			// the sky swallows the Old Duke: one ending only when it was in the scene a moment ago, and never on a world's first tick
			RiftSky.TestDukeKilled = false; RiftSky.TestDukeAlive = false;
			int de0 = RiftMood.DukeEndings;
			RiftMood.Reset(); RiftSky.TestDukeKilled = true; RiftMood.Tick(3000); RiftMood.Tick(3001);
			Check($"a world loaded with the flag already on is not an ending ({RiftMood.DukeEndings - de0})", RiftMood.DukeEndings == de0);
			RiftMood.Reset(); RiftSky.TestDukeKilled = false; RiftSky.TestDukeAlive = true; RiftMood.Tick(3100); RiftMood.Tick(3101);
			RiftSky.TestDukeAlive = false; RiftSky.TestDukeKilled = true; RiftMood.Tick(3102);
			Check($"the flag turning on just after the boss was in the scene is one ending ({RiftMood.DukeEndings - de0})", RiftMood.DukeEndings == de0 + 1);
			RiftMood.Reset(); RiftSky.TestDukeKilled = false; RiftSky.TestDukeAlive = true; RiftMood.Tick(3200);
			RiftSky.TestDukeAlive = false; RiftMood.Tick(3201); RiftSky.TestDukeKilled = true; RiftMood.Tick(3201 + LevyConstants.RiftDukeSeenTicks + 30);
			Check($"the flag turning on long after the boss was last seen is not ({RiftMood.DukeEndings - de0 - 1})", RiftMood.DukeEndings == de0 + 1);
			string duke = DialogueSystem.Pick(DialogueTrigger.RIFT_DUKE, pat, ctx, null, true, -1, out _, false);
			Check($"the shocked burst draws from RIFT_DUKE ({duke}) and is never thinned", duke != null && RiftMood.AllowFloating((byte)DialogueTrigger.RIFT_DUKE));
			RiftSky.TestDukeKilled = null; RiftSky.TestDukeAlive = null; RiftMood.Reset();

			SkyChecks(pat, ctx);

			// the cold: snow falling or lying, on the surface
			RiftSky.TestSnowing = true; RiftMood.TestSurface = true;
			int cold = CountPool(DialogueTrigger.RIFT_COLD, pat, ctx, 200);
			RiftMood.TestSurface = false;
			int coldDeep = CountPool(DialogueTrigger.RIFT_COLD, pat, ctx, 200);
			RiftSky.TestSnowing = false; RiftSky.TestSnowHeight = 0f; RiftMood.TestSurface = true;
			int coldNone = CountPool(DialogueTrigger.RIFT_COLD, pat, ctx, 200);
			RiftSky.TestSnowHeight = 3f;
			int coldLying = CountPool(DialogueTrigger.RIFT_COLD, pat, ctx, 200);
			Check($"snow falling on the surface brings cold talk, underground none, no snow none, snow lying some ({cold} / {coldDeep} / {coldNone} / {coldLying} in 200)", cold > 50 && coldDeep == 0 && coldNone == 0 && coldLying > 50);
			RiftSky.TestSnowing = null; RiftSky.TestSnowHeight = null; RiftMood.TestSurface = null;

			// the rift voice outranks the Blood Moon's annoyed set
			RosterFacts.TestBloodMoon = true;
			string fb = DialogueSystem.Pick(DialogueTrigger.IDLE, fay, ctx, null, true, -1, out DialogueLine pfb, false);
			string fe = DialogueSystem.Pick(DialogueTrigger.ENGAGE, fay, ctx, null, true, -1, out DialogueLine pfe, false);
			Check($"on a Blood Moon under the rift a woman speaks the rift lines in chat ({fb}) and her annoyed shouts in a fight ({fe})", fb != null && InRift(pfb) && fe != null && (pfe.Cond & LineCond.Grumpy) != 0);
			RosterFacts.TestBloodMoon = null;

			// how much is still said out loud
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			float iT = Rate(DialogueTrigger.IDLE, RiftPhase.Tense), iS = Rate(DialogueTrigger.IDLE, RiftPhase.Settled), eT = Rate(DialogueTrigger.ENGAGE, RiftPhase.Tense), eS = Rate(DialogueTrigger.ENGAGE, RiftPhase.Settled);
			float bT = Rate(DialogueTrigger.BOSS_SIGHTED, RiftPhase.Tense), fT = Rate(DialogueTrigger.FLIGHT_SHOUT, RiftPhase.Tense), pT = Rate(DialogueTrigger.RIFT_PANIC, RiftPhase.Tense), mT = Rate(DialogueTrigger.MUTINY, RiftPhase.Tense);
			float iN = Rate(DialogueTrigger.IDLE, RiftPhase.None), xT = Rate(byte.MaxValue, RiftPhase.Tense);
			Note($"floating share: ambient tense {iT:F2} settled {iS:F2}; combat tense {eT:F2} settled {eS:F2}; boss tense {bT:F2}; flight shout tense {fT:F2}; panic {pT:F2}; mutiny {mT:F2}; no mood {iN:F2}");
			Check("ambient talk is thinned hard while tense and less once settled", Near(iT, LevyConstants.RiftTenseChatterShare) && Near(iS, LevyConstants.RiftSettledChatterShare));
			Check("combat barks are thinned a little, boss lines barely", Near(eT, LevyConstants.RiftTenseBarkShare) && Near(eS, LevyConstants.RiftSettledBarkShare) && Near(bT, LevyConstants.RiftTenseBigShare));
			Check("no flying whoops while tense; panic, mutiny and untagged lines all go out; with no mood everything goes out", fT == 0f && pT == 1f && mT == 1f && xT == 1f && iN == 1f);
		}

		private static bool Near(float got, float want) => Math.Abs(got - want) < 0.05f;

		private static float Rate(byte trig, RiftPhase ph)
		{
			RiftMood.TestPhase = ph; RiftMood.TestPanic = false;
			int yes = 0; const int n = 4000;
			for (int i = 0; i < n; i++) if (RiftMood.AllowFloating(trig)) yes++;
			RiftMood.TestPhase = RiftPhase.Tense; RiftMood.TestPanic = null;
			return yes / (float)n;
		}
		private static float Rate(DialogueTrigger t, RiftPhase ph) => Rate((byte)t, ph);

		private static int CountPool(DialogueTrigger pool, SoldierRecord sp, DialogueContext ctx, int n)
		{
			int k = 0;
			for (int i = 0; i < n; i++) { DialogueSystem.Pick(DialogueTrigger.IDLE, sp, ctx, null, true, -1, out DialogueLine pl, false); if (In(pool, pl)) k++; }
			return k;
		}

		/// <summary>World-check hook: a filled line that could only have come from one of the rift pools.</summary>
		internal static bool IsRiftText(string text)
		{
			foreach (DialogueTrigger t in Rift) if (Contains(t, text)) return true;
			return false;
		}

		internal static bool Contains(DialogueTrigger t, string text)
		{
			var pool = DialogueTable.Get(t);
			if (pool == null || text == null) return false;
			foreach (DialogueLine l in pool) if (l.Text == text || Shape(l.Text, text)) return true;
			return false;
		}

		/// <summary>True when a filled line could have come from this template (the literal runs appear in order).</summary>
		private static bool Shape(string template, string text)
		{
			int at = 0;
			foreach (string part in template.Split('{'))
			{
				string lit = part.Contains('}') ? part.Substring(part.IndexOf('}') + 1) : part;
				if (lit.Length == 0) continue;
				int f = text.IndexOf(lit, at, StringComparison.Ordinal);
				if (f < 0) return false;
				at = f + lit.Length;
			}
			return true;
		}
	}
}
