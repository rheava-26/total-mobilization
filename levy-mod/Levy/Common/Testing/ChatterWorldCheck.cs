using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TWO CHAT FEATURES, IN A REAL WORLD ("[ChatterWorld]" lines; LEVY_SELFCHECK_WORLD=1 on a dedicated server, LEVY_CHATTER_ONLY=1 runs it alone). Six levies stand by the commander (slot 0) and real game ticks run.
	/// The eclipse: ending it by day speaks exactly one round of ECLIPSE_END lines and nothing more over the next ten seconds; vanilla's own nightfall ending (the clock run past dusk) speaks one round too;
	/// an ending with no levies of that commander near says nothing; and an eclipse that was on in a world just left is not an ending in the next one.
	/// The upgrades (Calamity half): each of the six shared Rage and Adrenaline items, used through its own UseItem, sends exactly one line naming it; a second use sends none; Calamity's other permanent items,
	/// a life crystal and a life fruit send none; a character loaded with the upgrades already on sends none; an upgrade used with no army sends none. The vanilla half checks that nothing is ever sent.
	/// Every global it touches (the clock, the eclipse, the Blood Moon, the RNG, the commander's Calamity state and life) is put back.
	/// </summary>
	public partial class ChatterWorldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800; private const string Commander = "ChatTester";
		private static Vector2 _cmdAt; private static string _name = Commander;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_CHATTERWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0" && Environment.GetEnvironmentVariable("LEVY_CHATTER_ONLY") == "1") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		// lab tracer (LEVY_RIFTWATCH=1): logs every change of the real sky flags and of Moon Lord's downed flag while the world checks run, so the check that moves them can be named
		private static string _watchLast;
		public override void PostUpdateEverything()
		{
			if (!_armed || _done || !Main.dedServ || Environment.GetEnvironmentVariable("LEVY_RIFTWATCH") != "1") return;
			try
			{
				var r = RiftSky.ReadReal();
				string now = "covered=" + r.covered + " scene=" + r.ongoing + " timer=" + r.timer + " waiting=" + r.waiting + " rift=" + r.rift + " downedMoonlord=" + NPC.downedMoonlord + " hardMode=" + Main.hardMode + " day=" + Main.dayTime;
				if (now != _watchLast) { _watchLast = now; _mod.Logger.Info("[ChatterWorld] WATCH tick " + Main.GameUpdateCount + ": " + now); }
			}
			catch (Exception) { }
		}
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			bool sp = LevyUtil.SpEmulation; int my0 = Main.myPlayer;
			bool day0 = Main.dayTime, ecl0 = Main.eclipse, blood0 = Main.bloodMoon; double time0 = Main.time; var rand0 = Main.rand;
			Player p0 = Main.player[0] ??= new Player();
			string name0 = p0.name; int lifeMax0 = p0.statLifeMax, manaMax0 = p0.statManaMax, crystals0 = p0.ConsumedLifeCrystals, fruit0 = p0.ConsumedLifeFruit, mana0 = p0.ConsumedManaCrystals; bool extra0 = p0.extraAccessory;
			object cp = LevyRippers.Active ? LevyRippers.Of(p0) : null;
			var calState = Snapshot(cp);
			try { LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				Restore(cp, calState);
				p0.name = name0; p0.statLifeMax = lifeMax0; p0.statManaMax = manaMax0; p0.ConsumedLifeCrystals = crystals0; p0.ConsumedLifeFruit = fruit0; p0.ConsumedManaCrystals = mana0; p0.extraAccessory = extra0; p0.itemAnimation = p0.itemTime = 0;
				Main.dayTime = day0; Main.time = time0; Main.eclipse = ecl0; Main.bloodMoon = blood0; Main.rand = rand0;
				LevyUtil.SpEmulation = sp; Main.myPlayer = my0; _name = Commander;
				Cleanup(); CommanderReactions.Reset(); SharedUpgradeNotice.Reset();
			}
			_mod.Logger.Info($"[ChatterWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ChatterWorld] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[ChatterWorld] OBSERVED " + s);

		/// <summary>Every value-type field of the commander's CalamityPlayer (the upgrade flags, the life and mana fruit flags, the extra slot), so the check can put them all back.</summary>
		private static List<(FieldInfo f, object v)> Snapshot(object cp)
		{
			var list = new List<(FieldInfo, object)>();
			if (cp == null) return list;
			foreach (FieldInfo f in cp.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				if (f.FieldType.IsValueType && !f.IsInitOnly) { try { list.Add((f, f.GetValue(cp))); } catch (Exception) { } }
			return list;
		}
		private static void Restore(object cp, List<(FieldInfo f, object v)> state) { if (cp == null) return; foreach (var (f, v) in state) { try { f.SetValue(cp, v); } catch (Exception) { } } }

		private static void Arena()
		{
			for (int i = Cx - 60; i <= Cx + 60; i++)
			{
				for (int j = FloorY - 40; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = FloorY; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = Terraria.ID.BlockType.Solid; }
			}
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn && sn.Record?.CommanderName == Commander) { sn.Record.Gear = new SoldierGear(); n.active = false; } }
				if (RosterSystem.Instance != null) RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
			catch (Exception) { }
		}

		private static SoldierNPC MakeLevy(Vector2 at, int n)
		{
			var temper = (Temperament)(n % 4);
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = temper, Male = true, Name = "Pvt. Chat" + n, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = 5, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int bi = RosterSystem.Instance.SpawnBody(rec, at);
			if (bi < 0 || !rec.HasBody) return null;
			Main.npc[bi].Bottom = at;
			return Main.npc[bi].ModNPC as SoldierNPC;
		}

		/// <summary>Real world ticks. The clock is left to run (the dusk case needs vanilla's own nightfall).</summary>
		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0]; p.active = true; p.dead = false; p.name = _name;
				// The check's levies have no barracks, so every dawn counts them unpaid; the check's own day flips (and a world left at night by an earlier check) make two dawns, and on the second
				// each rolls 25% to desert (seen in a ship-world run: 3 of 6 walked off before the rift). Wages are not what this check is about: they are kept paid.
				foreach (SoldierRecord r in RosterSystem.Instance.Regulars) if (r.CommanderName == Commander) r.DaysUnpaid = 0;
				if (p.statLifeMax < 400) p.statLifeMax = p.statLifeMax2 = 400; p.statLife = p.statLifeMax2;
				p.Bottom = _cmdAt; p.velocity = Vector2.Zero; p.fallStart = p.fallStart2 = (int)(_cmdAt.Y / 16f); p.noFallDmg = true;
				for (int k = 0; k < Main.maxNPCs; k++) { NPC e = Main.npc[k]; if (e.active && WorldStep.IsNaturalHostile(e) && !(_keepDuke && (e.type == RiftSky.DukeType || e.ModNPC?.Mod.Name == RiftSky.ModName)) && !(_keepCal && e.ModNPC?.Mod.Name == "CalamityMod")) e.active = false; }
				WorldStep.Update();
			}
		}

		private static void Run()
		{
			Arena(); _cmdAt = new Vector2(Cx * 16f, FloorY * 16f);
			Main.rand = new Terraria.Utilities.UnifiedRandom(5150);
			Main.dayTime = true; Main.time = 15000; Main.eclipse = false; Main.bloodMoon = false;
			var army = new List<SoldierNPC>();
			for (int i = 0; i < 6; i++) { var l = MakeLevy(_cmdAt + new Vector2((-3 + i) * 24f, 0f), i); if (l != null) army.Add(l); }
			Check($"the army spawns ({army.Count} levies)", army.Count == 6);
			CommanderReactions.Reset();
			Step(60);
			bool sceneOnly = Environment.GetEnvironmentVariable("LEVY_BOSSSCENE_ONLY") == "1";
			if (!sceneOnly) { Eclipse(); Rift(army); Upgrades(army); }
			BossScene(army);
		}

		private static void Eclipse()
		{
			// ---- 1. an eclipse that ends by day: one round of lines, then nothing
			Main.dayTime = true; Main.time = 15000; Main.eclipse = true; Step(120);
			int e0 = CommanderReactions.EclipseEndings, r0 = CommanderReactions.EclipseEndReactions, l0 = CommanderReactions.EclipseEndLines;
			Step(120);
			Check($"two seconds into an eclipse nothing has ended ({CommanderReactions.EclipseEndings - e0} endings)", CommanderReactions.EclipseEndings == e0 && CommanderReactions.EclipseEndLines == l0);
			Main.eclipse = false; Step(150);
			int lines = CommanderReactions.EclipseEndLines - l0;
			Check($"the eclipse ending by day is one ending with one round of reactions ({CommanderReactions.EclipseEndings - e0} endings, {CommanderReactions.EclipseEndReactions - r0} rounds)", CommanderReactions.EclipseEndings == e0 + 1 && CommanderReactions.EclipseEndReactions == r0 + 1);
			Check($"{LevyConstants.EclipseEndSpeakers} levies speak an ECLIPSE_END line ({lines} spoke)", lines == LevyConstants.EclipseEndSpeakers);
			Step(600);
			Check($"ten more seconds of daylight add nothing ({CommanderReactions.EclipseEndings - e0} endings, {CommanderReactions.EclipseEndLines - l0} lines)", CommanderReactions.EclipseEndings == e0 + 1 && CommanderReactions.EclipseEndLines - l0 == lines);

			// ---- 2. vanilla's own ending: the clock runs past dusk and Main.UpdateTime clears the eclipse at nightfall
			Main.dayTime = true; Main.time = Main.dayLength - 40; Main.eclipse = true; Main.bloodMoon = false;
			e0 = CommanderReactions.EclipseEndings; l0 = CommanderReactions.EclipseEndLines; r0 = CommanderReactions.EclipseEndReactions;
			Step(90);
			bool vanillaEnded = !Main.dayTime && !Main.eclipse;
			Note($"after the clock ran past dusk: dayTime={Main.dayTime} eclipse={Main.eclipse} bloodMoon={Main.bloodMoon}");
			Check("vanilla ends the eclipse itself at nightfall", vanillaEnded);
			Step(120);
			int duskLines = CommanderReactions.EclipseEndLines - l0;
			Check($"that nightfall ending is one ending and one round ({CommanderReactions.EclipseEndings - e0} endings, {CommanderReactions.EclipseEndReactions - r0} rounds, {duskLines} lines)", CommanderReactions.EclipseEndings == e0 + 1 && CommanderReactions.EclipseEndReactions == r0 + 1 && duskLines >= 1 && duskLines <= LevyConstants.EclipseEndSpeakers);
			Main.bloodMoon = false; Main.dayTime = true; Main.time = 15000;

			// ---- 3. the commander's levies are not near him (he is someone else to them): the ending is seen, nobody speaks
			Main.eclipse = true; Step(60);
			e0 = CommanderReactions.EclipseEndings; l0 = CommanderReactions.EclipseEndLines; r0 = CommanderReactions.EclipseEndReactions;
			_name = "NotTheirCommander"; Main.eclipse = false; Step(150); _name = Commander; Step(30);
			Check($"with no levies of his near, an ending speaks nothing ({CommanderReactions.EclipseEndings - e0} endings, {CommanderReactions.EclipseEndReactions - r0} rounds, {CommanderReactions.EclipseEndLines - l0} lines)", CommanderReactions.EclipseEndings == e0 + 1 && CommanderReactions.EclipseEndReactions == r0 && CommanderReactions.EclipseEndLines == l0);

			// ---- 4. an eclipse on in the world just left is not an ending in the next (the reset a world load does)
			Main.eclipse = true; Step(60);
			e0 = CommanderReactions.EclipseEndings;
			CommanderReactions.Reset(); Main.eclipse = false; Step(150);
			Check($"a world loaded after an eclipse is not an ending ({CommanderReactions.EclipseEndings - e0} endings)", CommanderReactions.EclipseEndings == e0);
		}

		/// <summary>
		/// The rift sky (Wrath of the Gods), with real levies and real ticks. With NoxusBoss loaded the real covering scene is started (its timer set as the mod's own start sets it) and run to its end;
		/// without it the same moments are driven through the overrides. Ambient chatter is forced on and two minutes of it are counted with no mood (the control), then while the sun is covered
		/// (nothing may be said: no line, no float, right-click "..."), then the burst after (the levies near the commander, one after another), then two tense minutes (fewer lines, every chat line a rift line).
		/// </summary>
		/// <summary>What keeps an army quiet, read as it stands now: the rift mood and the real sky flags, every Wrath of the Gods NPC alive, the fight hush and the fight voice (the report line a leak from an earlier check shows up in).</summary>
		private static string WorldVoiceState()
		{
			var nox = new List<string>();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC?.Mod?.Name == "NoxusBoss") nox.Add(n.ModNPC.Name); }
			var real = RiftSky.ReadReal();
			string V(object o) => o == null ? "-" : o.ToString();
			return "mood " + RiftMood.Phase + ", covered=" + real.covered + " scene=" + real.ongoing + " timer=" + real.timer + " waiting=" + real.waiting + " rift=" + real.rift + " snow=" + real.snow.ToString("F1") + ", defeated=" + RiftSky.Defeated
				+ ", fight hush=" + global::Levy.Common.Death.NoxusFights.Hush + ", Avatar voice=" + global::Levy.Common.Compat.Wotg.AoeVoice.Active + ", genesis=" + WotgScenes.GenesisOn
				+ ", Wrath of the Gods NPCs alive [" + string.Join(", ", nox) + "], overrides: hush=" + V(global::Levy.Common.Death.NoxusFights.Override) + " voice=" + V(WotgScenes.TestVoice) + " fight=" + V(WotgScenes.TestFight)
				+ " avatar-on=" + V(global::Levy.Common.Compat.Wotg.AvatarFight.TestOn) + " avatar-void=" + V(global::Levy.Common.Compat.Wotg.AvatarFight.TestVoid) + " avatar-voice=" + V(global::Levy.Common.Compat.Wotg.AoeVoice.TestActive);
		}

		private static void Rift(List<SoldierNPC> army)
		{
			bool real = RiftSky.Installed && RiftSky.Problem.Length == 0;
			Note(real ? "NoxusBoss is loaded: the real covering scene is run" : "NoxusBoss is not loaded: the covering is driven through the overrides (" + RiftSky.Problem + ")");
			int chat0 = SoldierNPC.ChatterOverride; bool rain0 = Main.raining;
			SoldierNPC.ChatterOverride = 1; Main.raining = false;
			bool ml0 = NPC.downedMoonlord;
			try
			{
				Main.dayTime = true; Main.time = 9000; Note("the world as the rift check begins: " + WorldVoiceState());
				// this check owns the sky it tests: an earlier check that killed Moon Lord leaves Wrath of the Gods armed or the sun covered, and Moon Lord's flag is what arms it again by itself
				if (real) { NPC.downedMoonlord = false; RiftSky.SetReal(covered: false, eventTimer: 0, waiting: false, snowHeight: 0f, dukeKilled: false); }
				RiftMood.Reset(); DialogueSystem.ResetRuntime();
				int d0 = DialogueSystem.FloatingDelivered, a0 = SoldierNPC.ChatterLines;
				RiftStep(RiftWindow);
				int control = DialogueSystem.FloatingDelivered - d0, controlSaid = SoldierNPC.ChatterLines - a0;
				Note($"control, no mood, three minutes: {controlSaid} ambient lines picked, {control} floating lines out = {control / 3f:F1} lines a minute from {Standing(army)} standing levies ({_bandsCleared} band members cleared from the arena)");
				Check($"with no rift the army chats as usual ({control} floating lines in three minutes)", control >= 5 && RiftMood.Phase == RiftPhase.None);

				// the covering
				Main.dayTime = true; Main.time = 9000;
				if (real) Check("the real covering scene starts", RiftSky.SetReal(eventTimer: 1));
				else { RiftSky.TestInstalled = true; RiftSky.TestDefeated = false; RiftSky.TestCovered = false; RiftSky.TestTransition = true; }
				RiftStep(2);
				Check($"while the sun is covered the mood is Silent ({RiftMood.Phase})", RiftMood.Phase == RiftPhase.Silent);
				d0 = DialogueSystem.FloatingDelivered; a0 = SoldierNPC.ChatterLines; int ch0 = RiftMood.Changes, pr0 = RiftMood.PanicRounds, pl0 = RiftMood.PanicLines;
				string chat = army.Count > 0 ? DialogueSystem.ChatLine(army[0]) : null;
				int scene = 0;
				if (real) { while (RiftSky.Transition && scene < 2400) { RiftStep(1); scene++; } }
				else { RiftStep(1500); scene = 1500; }
				int silentOut = DialogueSystem.FloatingDelivered - d0, silentSaid = SoldierNPC.ChatterLines - a0;
				Check($"while the sun was covered ({scene} ticks) nobody said anything ({silentSaid} lines picked, {silentOut} floats out, right-click \"{chat}\")", silentOut == 0 && silentSaid == 0 && chat == "..." && scene >= 1000);

				// the end: the burst
				int atEnd = Standing(army);
				if (!real) { RiftSky.TestTransition = false; RiftSky.TestCovered = true; }
				int burstTicks = 20 + LevyConstants.RiftPanicSpeakers * LevyConstants.RiftPanicStepTicks + 60, ll0 = RiftMood.LoudLines;
				RiftStep(burstTicks);
				int burst = RiftMood.PanicLines - pl0;
				Note($"at the end of the covering: {atEnd} of {army.Count} levies standing near the commander ({Standing(army)} after the burst)");
				Check($"the covering ends once and the levies near the commander react ({RiftMood.Changes - ch0} endings, {RiftMood.PanicRounds - pr0} rounds, {burst} worried lines from {atEnd} standing)", RiftMood.Changes == ch0 + 1 && RiftMood.PanicRounds == pr0 + 1 && burst == Math.Min(LevyConstants.RiftPanicSpeakers, atEnd) && atEnd >= LevyConstants.RiftPanicSpeakers);
				Check($"after the covering the sky reads covered and the mood is Tense ({RiftMood.Phase}, covered={RiftSky.Covered})", RiftSky.Covered && RiftMood.Phase == RiftPhase.Tense && RiftMood.PanicNow);
				if (real) Note($"NoxusBoss's own rift flag after the scene: {RiftSky.RiftOngoing}");
				LoudMinute(army, burstTicks, pl0, ll0);

				// the tense days
				RiftMood.SetPanic(false); DialogueSystem.ResetRuntime();
				int riftChat = 0, otherChat = 0; string other = null;
				foreach (SoldierNPC s in army)
				{
					if (s == null || !s.NPC.active) continue;
					for (int i = 0; i < 8; i++) { string t = DialogueSystem.ChatLine(s); if (RiftMoodCheck.IsRiftText(t)) riftChat++; else { otherChat++; other ??= t; } }
				}
				Check($"in the tense days every right-click chat is a rift line ({riftChat} rift, {otherChat} other{(other != null ? ": " + other : "")})", riftChat > 0 && otherChat == 0);
				DialogueSystem.ResetRuntime(); // the chat sampling above used up the rift lines' repeat cooldowns; the ambient minutes start fresh, as the control did
				d0 = DialogueSystem.FloatingDelivered; a0 = SoldierNPC.ChatterLines;
				RiftStep(RiftWindow);
				int tense = DialogueSystem.FloatingDelivered - d0, tenseSaid = SoldierNPC.ChatterLines - a0;
				Note($"tense, three minutes: {tenseSaid} ambient lines picked, {tense} floating lines out = {tense / 3f:F1} a minute (control {control / 3f:F1}), {Standing(army)} standing");
				Check($"the tense army is much quieter, but not mute ({tense} floating lines against {control}; {tenseSaid} picked)", tense <= control * 0.6f && tenseSaid > 0);

				// the sky's later turns: the rift grows, the water freezes, a Blood Moon rises
				int gl0 = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_GROW1), il0 = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_ICE), bl0 = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_BLOOD);
				int burstWait = 20 + 3 * LevyConstants.RiftBurstStepTicks + 60;
				RiftSky.TestIntensity = 0.3f; RiftStep(burstWait);
				int grew = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_GROW1) - gl0;
				RiftStep(300);
				Check($"the rift grows: the levies near the commander say so, once ({grew} lines, {RiftMood.BurstLinesOf(DialogueTrigger.RIFT_GROW1) - gl0} after five more seconds)", grew == Math.Min(LevyConstants.RiftGrowSpeakers, Standing(army)) && RiftMood.BurstLinesOf(DialogueTrigger.RIFT_GROW1) - gl0 == grew);
				RiftSky.TestIceCover = true; RiftStep(burstWait);
				int froze = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_ICE) - il0;
				Check($"the water freezes: {froze} levies notice", froze == Math.Min(LevyConstants.RiftIceSpeakers, Standing(army)));
				Main.dayTime = false; Main.time = 600; Main.bloodMoon = true; RiftStep(burstWait);
				int blood = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_BLOOD) - bl0;
				Check($"a Blood Moon rises under the rift: {blood} levies speak", blood == Math.Min(LevyConstants.RiftBloodSpeakers, Standing(army)));
				DialogueSystem.ResetRuntime();
				int bloodChat = 0, chats = 0;
				for (int round = 0; round < 6; round++) // one chat per levy a round, fresh repeat cooldowns each round: the pool is not drained by the sampling itself
				{
					DialogueSystem.ResetRuntime();
					foreach (SoldierNPC s in army) { if (s == null || !s.NPC.active) continue; string t = DialogueSystem.ChatLine(s); chats++; if (RiftMoodCheck.Contains(DialogueTrigger.RIFT_BLOOD, t) || RiftMoodCheck.Contains(DialogueTrigger.RIFT_HUSH, t)) bloodChat++; }
				}
				Check($"and most right-click chat is about it or silence ({bloodChat} of {chats})", chats > 0 && bloodChat * 2 > chats);
				Main.bloodMoon = false; Main.dayTime = true; Main.time = 9000; RiftSky.TestIntensity = null; RiftSky.TestIceCover = null; DialogueSystem.ResetRuntime();
				RiftStep(5);

				// the fog pulls the army apart: mean distance from the commander, clear, then deep in the fog
				RiftStep(300);
				float clear = MeanSpread(army, 60);
				RiftSky.TestFog = true; RiftStep(LevyConstants.RiftFogDriftTicks + 600);
				float foggy = MeanSpread(army, 60);
				{
					var sb = new System.Text.StringBuilder();
					foreach (SoldierNPC s in army) if (s != null && s.NPC.active) sb.Append($"{s.State}/{(s.InFollowRow ? "row" : "-")}/drift {RiftMood.FogDriftTiles(s.Record, s.TargetIndex >= 0, (int)Main.GameUpdateCount):F1}/dx {(s.NPC.Center.X - _cmdAt.X) / 16f:F0}; ");
					Note("in the fog: " + sb);
				}
				RiftSky.TestFog = null; RiftStep(1200);
				float after = MeanSpread(army, 60);
				Check($"in the fog the idle army drifts out from the commander ({clear:F1} tiles clear, {foggy:F1} in the fog, {after:F1} once it lifts)", foggy >= clear + 1f && after <= foggy - 0.25f); // after the fog they settle on their own slots, tighter than the slack left them before it

				// a scare in the fog: with NoxusBoss, a real fog tree set down by the commander
				if (real && RiftSky.FogwoodType >= 0)
				{
					_keepDuke = true;
					int sl0 = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_SCARE), ss0 = RiftMood.ScaresSeen;
					int fi = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), (int)_cmdAt.X + 120, (int)_cmdAt.Y - 8, RiftSky.FogwoodType);
					int ft = 0;
					while (RiftMood.ScaresSeen == ss0 && ft < 600) { RiftStep(1); ft++; }
					NPC f = fi >= 0 && fi < Main.maxNPCs ? Main.npc[fi] : null;
					Note($"fog tree set down (slot {fi}): after {ft} ticks active={f?.active} state={f?.ai[0]}");
					RiftStep(burstWait);
					int scared = RiftMood.BurstLinesOf(DialogueTrigger.RIFT_SCARE) - sl0;
					Check($"a real fog tree screams by the army and the levies near it are shocked ({RiftMood.ScaresSeen - ss0} scares, {scared} lines)", RiftMood.ScaresSeen > ss0 && scared == Math.Min(LevyConstants.RiftScareSpeakers, Standing(army)));
					if (f != null && f.type == RiftSky.FogwoodType) f.active = false;
					_keepDuke = false; RiftStep(5);
				}
				else Note("no fog trees without NoxusBoss: the scare rule is checked at boot");

				// the sky swallows the Old Duke in front of the army
				int de0 = RiftMood.DukeEndings, dr0 = RiftMood.DukeRounds, dl0 = RiftMood.DukeLines;
				bool realDuke = real && RiftSky.DukeType >= 0 && !RiftSky.DukeKilled;
				int dukeTicks = 0;
				if (realDuke)
				{
					_keepDuke = true; // the arena clearing in Step would take the Old Duke and NoxusBoss's own NPCs for natural spawns
					int di = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), (int)_cmdAt.X + 400, (int)_cmdAt.Y - 300, RiftSky.DukeType);
					Note($"a real Old Duke is spawned (slot {di}); NoxusBoss is left to run its scene");
					while (!RiftSky.DukeKilled && dukeTicks < 900)
					{
						RiftStep(1); dukeTicks++;
						if (dukeTicks == 5 || dukeTicks == 120) { NPC d = di >= 0 && di < Main.maxNPCs ? Main.npc[di] : null; Note($"Old Duke after {dukeTicks} ticks: active={d?.active} type ok={d?.type == RiftSky.DukeType} ai0={d?.ai[0]} ai2={d?.ai[2]} scale={d?.scale:F2} immune={d?.dontTakeDamage}"); }
					}
					Check($"NoxusBoss's own scene takes the Old Duke ({dukeTicks} ticks, flag={RiftSky.DukeKilled})", RiftSky.DukeKilled);
				}
				else
				{
					Note("the Old Duke is driven through the overrides" + (real ? " (Calamity's Old Duke is not loaded)" : ""));
					RiftSky.TestDukeAt = _cmdAt + new Vector2(400f, -300f);
					RiftSky.TestDukeAlive = true; RiftStep(60); RiftSky.TestDukeAlive = false; RiftSky.TestDukeKilled = true;
				}
				RiftStep(20 + LevyConstants.RiftDukeSpeakers * LevyConstants.RiftDukeStepTicks + 60);
				int dukeLines = RiftMood.DukeLines - dl0;
				Check($"when the sky swallows the boss the levies who saw it are shocked ({RiftMood.DukeEndings - de0} endings, {RiftMood.DukeRounds - dr0} rounds, {dukeLines} lines)", RiftMood.DukeEndings == de0 + 1 && RiftMood.DukeRounds == dr0 + 1 && dukeLines == Math.Min(LevyConstants.RiftDukeSpeakers, Standing(army)));
				RiftStep(600);
				Check($"and only once ({RiftMood.DukeEndings - de0} endings ten seconds later)", RiftMood.DukeEndings == de0 + 1);
				// the same far from the army: an ending, but nobody saw it
				RiftSky.TestDukeKilled = false; RiftStep(5);
				RiftSky.TestDukeAt = _cmdAt + new Vector2(LevyConstants.RiftDukeReachTiles * 16f * 2.5f, 0f);
				RiftSky.TestDukeAlive = true; RiftStep(30); RiftSky.TestDukeAlive = false; RiftSky.TestDukeKilled = true;
				int far0 = RiftMood.DukeLines, farR = RiftMood.DukeRounds;
				RiftStep(20 + LevyConstants.RiftDukeSpeakers * LevyConstants.RiftDukeStepTicks + 60);
				Check($"swallowed {LevyConstants.RiftDukeReachTiles * 2.5f:F0} tiles from the army it is an ending nobody saw ({RiftMood.DukeEndings - de0} endings, {RiftMood.DukeRounds - farR} rounds, {RiftMood.DukeLines - far0} lines)", RiftMood.DukeEndings == de0 + 2 && RiftMood.DukeRounds == farR && RiftMood.DukeLines == far0);
			}
			finally
			{
				_keepDuke = false;
				Main.bloodMoon = false;
				if (real) RiftSky.SetReal(covered: false, eventTimer: 0, waiting: false, snowHeight: 0f, dukeKilled: false);
				NPC.downedMoonlord = ml0;
				try { for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && (n.type == RiftSky.DukeType || n.ModNPC?.Mod.Name == RiftSky.ModName)) n.active = false; } } catch (Exception) { }
				RiftSky.ClearOverrides(); RiftMood.Reset(); DialogueSystem.ResetRuntime();
				SoldierNPC.ChatterOverride = chat0; Main.raining = rain0;
				Main.dayTime = true; Main.time = 15000;
			}
		}

		/// <summary>
		/// The loud first minute after the covering: four more levies join the six by the commander (ten near), the rest of the minute runs, and the panic lines (the burst plus the shouting
		/// over each other) are counted: at least 20. The next half minute is still worried talk but at the usual pace (at most one line a squad gap). The four leave again.
		/// </summary>
		private static void LoudMinute(List<SoldierNPC> army, int burstTicks, int pl0, int ll0)
		{
			var extra = new List<SoldierNPC>();
			try
			{
				for (int i = 0; i < 4; i++) { var l = MakeLevy(_cmdAt + new Vector2((i < 2 ? -6 + i : 3 + i) * 24f, 0f), 6 + i); if (l != null) extra.Add(l); }
				int ticks = burstTicks, guard = LevyConstants.RiftPanicLoudTicks + 600;
				bool loudAtStart = RiftMood.PanicLoud;
				while (RiftMood.PanicLoud && ticks < guard) { RiftStep(1); ticks++; }
				int burst = RiftMood.PanicLines - pl0, shouted = RiftMood.LoudLines - ll0, near = Standing(army) + Standing(extra);
				Note($"the loud minute: {ticks} ticks after the covering ended, {burst} burst lines + {shouted} shouted = {burst + shouted} panic lines from {near} levies near ({(burst + shouted) * 3600f / Math.Max(1, ticks):F1} a minute)");
				Check($"the loud first minute: ten levies near shout at least 20 panic lines ({burst + shouted}: {burst} burst, {shouted} over each other, {near} near, loud for {ticks} ticks)", loudAtStart && near >= 8 && burst + shouted >= 20 && Math.Abs(ticks - LevyConstants.RiftPanicLoudTicks) <= 30);
				int a0 = SoldierNPC.ChatterLines, l1 = RiftMood.LoudLines, half = Math.Min(60 * 30, LevyConstants.RiftPanicTicks - LevyConstants.RiftPanicLoudTicks - 30);
				RiftStep(half);
				int after = SoldierNPC.ChatterLines - a0, cap = half / LevyConstants.ChatterSquadGapTicks + 1;
				Note($"after the loud minute, {half / 60} s of worried talk at the usual pace: {after} ambient lines ({after * 3600f / Math.Max(1, half):F1} a minute), still worried: {RiftMood.PanicNow}");
				Check($"after the loud minute the usual pace is back ({after} ambient lines in {half / 60} s, at most {cap}; none shouted: {RiftMood.LoudLines - l1})", after <= cap && RiftMood.LoudLines == l1 && RiftMood.PanicNow && !RiftMood.PanicLoud);
			}
			finally
			{
				foreach (SoldierNPC s in extra)
				{
					if (s?.Record == null) continue;
					int id = s.Record.Id; s.Record.Gear = new SoldierGear(); if (s.NPC.active) s.NPC.active = false;
					RosterSystem.Instance?.Regulars.RemoveAll(r => r.Id == id);
				}
			}
		}

		private const int RiftWindow = 60 * 180;
		private static int _bandsCleared;
		private static bool _keepDuke;

		/// <summary>Real ticks for the rift section, with the arena kept to the army: besides the world's own spawns (Step clears those) a Warbands band can spawn on the arena and fight the levies,
		/// which stops their chatter and kills them (seen in a ship-world run). Band members within 250 tiles are removed each tick. NoxusBoss's own NPCs are left alone.</summary>
		private static void RiftStep(int n)
		{
			for (int t = 0; t < n; t++)
			{
				for (int k = 0; k < Main.maxNPCs; k++)
				{
					NPC e = Main.npc[k];
					if (!e.active || e.ModNPC == null || e.ModNPC.Mod.Name != "Warbands") continue;
					if (Math.Abs(e.Center.X - _cmdAt.X) < 250 * 16f) { e.active = false; _bandsCleared++; }
				}
				Step(1);
			}
		}

		/// <summary>Mean horizontal distance (tiles) of the standing army from the commander over <paramref name="ticks"/> real ticks.</summary>
		private static float MeanSpread(List<SoldierNPC> army, int ticks)
		{
			double sum = 0; int n = 0;
			for (int t = 0; t < ticks; t++)
			{
				RiftStep(1);
				foreach (SoldierNPC s in army) if (s != null && s.NPC.active && s.IsAlive) { sum += Math.Abs(s.NPC.Center.X - _cmdAt.X) / 16.0; n++; }
			}
			return n == 0 ? 0f : (float)(sum / n);
		}

		/// <summary>Levies of the check's army standing (alive, not in reserve) within reaction reach of the commander.</summary>
		private static int Standing(List<SoldierNPC> army)
		{
			int n = 0; float reach = LevyConstants.ReactReachTiles * LevyConstants.TilePx;
			foreach (SoldierNPC s in army) if (s != null && s.NPC.active && s.IsAlive && s.Record != null && !s.Record.Reserve && Vector2.Distance(s.NPC.Center, _cmdAt) <= reach) n++;
			return n;
		}

		private static int SentSince(int from) => SharedUpgradeNotice.Sent.Count - from;

		/// <summary>Uses a Calamity item through its own CanUseItem / UseItem, as the item-use step does. Returns what CanUseItem said.</summary>
		private static bool UseCalamityItem(Player p, string name, out bool found)
		{
			found = ModContent.TryFind("CalamityMod", name, out ModItem mi);
			if (!found) return false;
			var item = new Item(mi.Type);
			bool can = item.ModItem.CanUseItem(p);
			p.itemAnimation = 10; p.itemTime = 0;
			if (can) item.ModItem.UseItem(p);
			p.itemAnimation = 0; p.itemTime = 0;
			return can;
		}

		private static void Upgrades(List<SoldierNPC> army)
		{
			Player p = Main.player[0];
			int s0 = SharedUpgradeNotice.Sent.Count;
			if (!LevyRippers.Active)
			{
				p.ConsumedLifeCrystals = Math.Min(15, p.ConsumedLifeCrystals + 1); p.ConsumedLifeFruit = Math.Min(20, p.ConsumedLifeFruit + 1); p.statLifeMax += 25;
				Step(150);
				Check($"without Calamity nothing is shared, and a life crystal and life fruit send no line ({SentSince(s0)} sent)", SentSince(s0) == 0);
				Note("Calamity is not loaded: the six shared upgrades are checked in the Calamity half");
				return;
			}
			object cp = LevyRippers.Of(p);
			Check("the commander has a CalamityPlayer", cp != null);
			if (cp == null) return;

			// ---- 1. a character that already has the upgrades when first seen sends nothing
			for (int b = 0; b < LevyRippers.BoosterSlots; b++) LevyRippers.SetBooster(cp, b, true);
			SharedUpgradeNotice.Reset(); Step(180);
			Check($"a character loaded with all six upgrades sends no line ({SentSince(s0)} sent)", SentSince(s0) == 0);
			for (int b = 0; b < LevyRippers.BoosterSlots; b++) LevyRippers.SetBooster(cp, b, false);
			Step(30);
			Check($"losing them sends nothing either ({SentSince(s0)} sent)", SentSince(s0) == 0);

			// ---- 2. each shared upgrade, used for real: one line naming it; used again: refused, no line
			for (int b = 0; b < LevyRippers.BoosterSlots; b++)
			{
				string item = LevyRippers.BoosterItems[b];
				int before = SharedUpgradeNotice.Sent.Count;
				bool can = UseCalamityItem(p, item, out bool found);
				Step(30);
				int sent = SentSince(before);
				string text = sent > 0 ? SharedUpgradeNotice.Sent[before].Text : "(none)";
				string display = found && ModContent.TryFind("CalamityMod", item, out ModItem mi) ? mi.DisplayName.Value : item;
				Check($"{item}: used once sends exactly one line naming it ({sent} sent: \"{text}\")", found && can && sent == 1 && SharedUpgradeNotice.Sent[before].Booster == b && text.Contains(display));
				before = SharedUpgradeNotice.Sent.Count;
				bool again = UseCalamityItem(p, item, out _);
				Step(30);
				Check($"{item}: a second use is refused and sends nothing (can use again={again}, {SentSince(before)} sent)", !again && SentSince(before) == 0);
			}
			Check($"the commander now carries all six ({LevyRippers.BoosterCount(cp)})", LevyRippers.BoosterCount(cp) == 6);
			// the line says the levies have it: the copy the army runs puts all six on a levy
			SoldierNPC lv = army.Find(a => a != null && a.NPC.active && a.Dummy != null);
			object lcp = lv != null ? LevyRippers.Of(lv.Dummy) : null;
			if (lcp != null) { LevyRippers.CopyBoosters(p, lcp); Check($"a levy given the commander's upgrades carries all six ({LevyRippers.BoosterCount(lcp)})", LevyRippers.BoosterCount(lcp) == 6); }
			else Note("no levy dummy with a CalamityPlayer to copy onto");
			foreach (var e in SharedUpgradeNotice.Sent) Note($"line {LevyRippers.BoosterItems[e.Booster]}: {e.Text}");

			// ---- 3. permanent items Levy does not share: no line (life fruit and mana crystals at their vanilla maximum, as Calamity's own items require)
			p.ConsumedLifeFruit = Player.LifeFruitMax; p.ConsumedManaCrystals = Player.ManaCrystalMax;
			string[] unshared = { "SanguineTangerine", "TaintedCloudberry", "SacredStrawberry", "MiracleFruit", "CometShard", "EtherealCore", "PhantomHeart", "CelestialOnion" };
			foreach (string u in unshared)
			{
				int before = SharedUpgradeNotice.Sent.Count;
				bool can = UseCalamityItem(p, u, out bool found);
				Step(30);
				if (!found) { Note(u + " is not in this Calamity"); continue; }
				Check($"{u} (not shared) is used and sends no line (used={can}, {SentSince(before)} sent)", can && SentSince(before) == 0);
			}
			int b0 = SharedUpgradeNotice.Sent.Count;
			p.ConsumedLifeCrystals = Math.Min(15, p.ConsumedLifeCrystals + 1); p.ConsumedLifeFruit = Math.Min(20, p.ConsumedLifeFruit + 1); p.statLifeMax += 25;
			Step(30);
			Check($"a life crystal and a life fruit send no line ({SentSince(b0)} sent)", SentSince(b0) == 0);

			// ---- 4. an upgrade used with no army sends nothing, then or later
			LevyRippers.SetBooster(cp, 0, false); Step(30);
			b0 = SharedUpgradeNotice.Sent.Count;
			_name = "NoArmyCommander";
			UseCalamityItem(p, LevyRippers.BoosterItems[0], out _);
			Step(30); _name = Commander; Step(30);
			Check($"used with no levies it sends nothing, then or once his army is back ({SentSince(b0)} sent)", SentSince(b0) == 0);
		}
	}
}
