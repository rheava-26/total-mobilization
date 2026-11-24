using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Death;
using Levy.Common.Dialogue;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS SCENES ("[BossScene]" lines, run at the end of [ChatterWorld] with its six levies by the commander; LEVY_CHATTER_ONLY=1 LEVY_BOSSSCENE_ONLY=1 runs it alone).
	/// 1. Calamity boss lines that fit an army (Calamity half): every army line names a real Calamity line; Calamity's own BroadcastLocalizedText, called for real, sends the army version with an army deployed,
	///    the original with none, with the setting off, and for a line that has no army version; "show the original too" sends both; and a real Draedon, spawned and left to talk, is swapped too.
	/// 2. The Wrath of the Gods fights (every half, through the overrides; the WotG half also reads a real Avatar and Nameless Deity): moments speak their pool with the right number of speakers,
	///    nothing is said while the boss has the floor nor within the gap after, scripted lines keep their spacing, a moment speaks once, the worried line comes now and then and only from the fight's pool,
	///    the levies' ordinary chatter stays hushed, the hope, kill, fallen comrade, fallen commander and homecoming moments, and the setting off silences it all.
	/// Every override and global it touches is put back.
	/// </summary>
	public partial class ChatterWorldCheck
	{
		private static int _sPass, _sFail;
		private static void SCheck(string n, bool ok) { if (ok) _sPass++; else _sFail++; _mod.Logger.Info($"[BossScene] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void SNote(string s) => _mod.Logger.Info("[BossScene] OBSERVED " + s);
		private static bool _keepCal;

		private static void BossScene(List<SoldierNPC> army)
		{
			_sPass = _sFail = 0;
			int chat0 = SoldierNPC.ChatterOverride;
			try { BossLinesPart(army); }
			catch (Exception e) { SCheck("boss lines driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { BossLines.TestArmy = BossLines.TestOn = BossLines.TestShowOriginal = null; _keepCal = false; }
			try { SoldierNPC.ChatterOverride = 1; ScenesPart(army); }
			catch (Exception e) { SCheck("scenes driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				WotgScenes.TestFight = null; WotgScenes.TestState = null; WotgScenes.TestLife = null; WotgScenes.TestVoice = WotgScenes.TestEnabled = WotgScenes.TestAvatarDefeated = WotgScenes.TestNamelessDefeated = WotgScenes.TestGenesis = null;
				NoxusFights.Override = null; RiftMood.TestPhase = null; _keepDuke = false; SoldierNPC.ChatterOverride = chat0;
				WotgScenes.Reset(); DialogueSystem.ResetRuntime();
			}
			_mod.Logger.Info($"[BossScene] self-check pass={_sPass} fail={_sFail}");
		}

		// =====================================================================================================
		// 1. Calamity's boss lines
		// =====================================================================================================

		private static void BossLinesPart(List<SoldierNPC> army)
		{
			const string bh3 = BossLines.CalamityPrefix + "SCalBH3Text", desp1 = BossLines.CalamityPrefix + "SCalDesparationText1";
			LocalizedText[] mine = Language.FindAll(Lang.CreateDialogFilter(BossLines.LevyPrefix));
			SCheck($"Levy carries army versions of boss lines ({mine.Length} entries)", mine.Length >= 30);
			SCheck("the SCal line about keeping the power has an army version, her 'Just stop!' does not", BossLines.ArmyKeyFor(bh3) != null && BossLines.ArmyKeyFor(desp1) == null);
			BossLines.TestArmy = true; BossLines.TestOn = true;
			SCheck("with an army and the setting on, a line with an army version is swapped", BossLines.SwapFor(bh3) == BossLines.LevyPrefix + "SCalBH3Text");
			BossLines.TestArmy = false;
			SCheck("with no army it is not", BossLines.SwapFor(bh3) == null);
			BossLines.TestArmy = true; BossLines.TestOn = false;
			SCheck("with the setting off it is not", BossLines.SwapFor(bh3) == null);
			BossLines.TestArmy = null; BossLines.TestOn = null;
			SCheck($"six deployed levies count as an army on the field ({Standing(army)} standing)", BossLines.ArmyOnField());

			SCheck("the boss line note shows once, and only with Calamity loaded", BossLineNotice.ShouldShow(false, true) && !BossLineNotice.ShouldShow(true, true) && !BossLineNotice.ShouldShow(false, false));
			{
				string note = Language.GetTextValue("Mods.Levy.Dialogue.BossLineNotice"), fit = Language.GetTextValue("Mods.Levy.Configs.LevyConfig.BossLinesFitArmy.Label"), orig = Language.GetTextValue("Mods.Levy.Configs.LevyConfig.BossLinesShowOriginal.Label");
				SCheck($"the note names the real settings (\"{fit}\", \"{orig}\")", note.StartsWith("Levy adds and changes some boss dialogue.") && note.Contains(fit) && note.Contains(orig) && fit.Length > 0 && !fit.StartsWith("Mods."));
				BossLineNotice.TestSeen = false; BossLineNotice.TestCalamity = true; int said0 = BossLineNotice.Said;
				try { BossLineNotice.Announce(); BossLineNotice.Announce(); }
				finally { bool seen = BossLineNotice.TestSeen == true; BossLineNotice.TestSeen = null; BossLineNotice.TestCalamity = null; SCheck($"entering a world twice says it once and marks it seen ({BossLineNotice.Said - said0})", BossLineNotice.Said - said0 == 1 && seen); }
			}
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) { SCheck("without Calamity nothing is hooked", !BossLines.Installed); SNote("Calamity is not loaded: Calamity's own broadcast is checked in the Calamity half"); return; }
			SCheck("the detour on CalamityUtils.BroadcastLocalizedText is in place", BossLines.Installed);
			var stray = new List<string>();
			foreach (LocalizedText t in mine)
			{
				string shortKey = t.Key.Substring(BossLines.LevyPrefix.Length);
				if (shortKey == "OriginalLine") continue;
				if (!Language.Exists(BossLines.CalamityPrefix + shortKey)) stray.Add(shortKey);
				else if (t.Value == Language.GetTextValue(BossLines.CalamityPrefix + shortKey)) stray.Add(shortKey + " (same as Calamity's)");
			}
			SCheck($"every army line stands for a real Calamity boss line and changes it ({stray.Count} do not{(stray.Count > 0 ? ": " + string.Join(", ", stray.Take(6)) : "")})", stray.Count == 0);

			string[] scalRematch = { "SCalSummonTextRematch", "SCalStartTextRematch", "SCalBH2TextRematch", "SCalBH3TextRematch", "SCalBH4TextRematch", "SCalSeekerRingTextRematch", "SCalDesparationText1Rematch", "SCalDesparationText2Rematch", "SCalDesparationText4Rematch" };
			SCheck("Supreme Calamitas's rematch has nine army lines, each on a real Calamity key", scalRematch.All(k => Language.Exists(BossLines.CalamityPrefix + k) && BossLines.ArmyKeyFor(BossLines.CalamityPrefix + k) == BossLines.LevyPrefix + k));
			SCheck("the Brothers' rematch line and the third closing line stay Calamity's own", BossLines.ArmyKeyFor(BossLines.CalamityPrefix + "SCalBrothersTextRematch") == null && BossLines.ArmyKeyFor(BossLines.CalamityPrefix + "SCalDesparationText3Rematch") == null);
			SCheck("her lines read as written (Hm? You want me to fight all of you? / ... a greater threat than I.)", Language.GetTextValue(BossLines.LevyPrefix + "SCalSummonTextRematch") == "Hm? You want me to fight all of you? So be it." && Language.GetTextValue(BossLines.LevyPrefix + "SCalDesparationText2Rematch").EndsWith("a greater threat than I.") && Language.GetTextValue(BossLines.LevyPrefix + "SCalDesparationText4Rematch") == "I cannot imagine what comes next for someone of your caliber.");

			MethodInfo say = cal.Code.GetType("CalamityMod.CalamityUtils")?.GetMethod("BroadcastLocalizedText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(Color?) }, null);
			SCheck("Calamity's BroadcastLocalizedText is found", say != null);
			if (say == null) return;
			var heard = new List<string>();
			Terraria.Chat.On_ChatHelper.hook_BroadcastChatMessage spy = (orig, text, color, except) => { heard.Add(text.ToString()); };
			Terraria.Chat.On_ChatHelper.BroadcastChatMessage += spy;
			try
			{
				string army1 = Language.GetTextValue(BossLines.LevyPrefix + "SCalBH3Text"), orig1 = Language.GetTextValue(bh3), origD = Language.GetTextValue(desp1);
				heard.Clear(); say.Invoke(null, new object[] { bh3, (Color?)Color.Orange });
				SCheck($"Calamity's own call with the army deployed sends the army line (\"{string.Join(" | ", heard)}\")", heard.Count == 1 && heard[0] == army1);
				heard.Clear(); BossLines.TestArmy = false; say.Invoke(null, new object[] { bh3, (Color?)Color.Orange });
				SCheck($"with no army it sends Calamity's line (\"{string.Join(" | ", heard)}\")", heard.Count == 1 && heard[0] == orig1);
				heard.Clear(); BossLines.TestArmy = null; BossLines.TestOn = false; say.Invoke(null, new object[] { bh3, (Color?)Color.Orange });
				SCheck($"with the setting off it sends Calamity's line", heard.Count == 1 && heard[0] == orig1);
				heard.Clear(); BossLines.TestOn = null; say.Invoke(null, new object[] { desp1, (Color?)Color.Orange });
				SCheck($"a line with no army version goes out as Calamity wrote it (\"{string.Join(" | ", heard)}\")", heard.Count == 1 && heard[0] == origD);
				heard.Clear(); BossLines.TestShowOriginal = true; say.Invoke(null, new object[] { bh3, (Color?)Color.Orange });
				SCheck($"'show the original too' sends the army line, then the original marked as such (\"{string.Join(" | ", heard)}\")", heard.Count == 2 && heard[0] == army1 && heard[1].Contains(orig1) && heard[1] != orig1);
				BossLines.TestShowOriginal = null;

				// a real Draedon, left to talk: his second introduction line has an army version
				if (cal.TryFind("Draedon", out ModNPC dr))
				{
					_keepCal = true; heard.Clear();
					int seen0 = BossLines.Seen, swapped0 = BossLines.Swapped;
					int di = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), (int)_cmdAt.X + 200, (int)_cmdAt.Y - 200, dr.Type, 0, 0f, 0f, 0f, 0f, 0);
					int t = 0; string want = Language.GetTextValue(BossLines.LevyPrefix + "DraedonIntroductionText2");
					while (t < 420 && !heard.Contains(want)) { Step(1); t++; }
					SNote($"a real Draedon (slot {di}) after {t} ticks: heard [{string.Join(" | ", heard)}], lines seen {BossLines.Seen - seen0}, swapped {BossLines.Swapped - swapped0}, last key {BossLines.LastKey}");
					SCheck($"a real Draedon's own introduction goes through the hook and his second line comes out in its army version ({BossLines.Seen - seen0} lines seen, {BossLines.Swapped - swapped0} swapped)", heard.Contains(want) && heard.Contains(Language.GetTextValue(BossLines.CalamityPrefix + "DraedonIntroductionText1")));
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC?.Mod == cal) Main.npc[i].active = false;
					for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].ModProjectile?.Mod == cal) Main.projectile[i].active = false;
					_keepCal = false; Step(2);
				}
				else SCheck("Calamity's Draedon NPC is found", false);
			}
			finally { Terraria.Chat.On_ChatHelper.BroadcastChatMessage -= spy; }
		}

		// =====================================================================================================
		// 2. The Wrath of the Gods fights
		// =====================================================================================================

		private static int LinesOf(string pool, int from) { int n = 0; for (int i = from; i < WotgScenes.Log.Count; i++) if (WotgScenes.Log[i].Pool == pool) n++; return n; }
		private static int LinesSince(int from) => WotgScenes.Log.Count - from;

		private static void ScenesPart(List<SoldierNPC> army)
		{
			// every pool the code asks for has lines
			var thin = WotgScenes.Pools.Where(p => (DialogueTable.Get(p)?.Count ?? 0) < 3).ToList();
			SCheck($"every fight pool has at least three lines ({thin.Count} short{(thin.Count > 0 ? ": " + string.Join(", ", thin) : "")})", thin.Count == 0);
			RiftMood.TestPhase = RiftPhase.Tense;
			SCheck("under the rift sky a fight line is never thinned", RiftMood.AllowFloating((byte)DialogueTrigger.BOSS_SCENE) && RiftMood.AllowFloating((byte)DialogueTrigger.BOSS_SCENE));
			RiftMood.TestPhase = null;

			int stand = Standing(army);
			WotgScenes.Reset(); DialogueSystem.ResetRuntime();
			WotgScenes.TestEnabled = true; WotgScenes.TestVoice = false; WotgScenes.TestLife = 1f; WotgScenes.TestState = "Teleport"; WotgScenes.TestAvatarDefeated = false; WotgScenes.TestNamelessDefeated = false;
			NoxusFights.Override = true; // the hush as a real fight sets it
			WotgScenes.TestFight = WotgFight.Avatar;
			Step(5);
			SCheck($"the watch sees the Avatar fight ({WotgScenes.CurrentFight})", WotgScenes.CurrentFight == WotgFight.Avatar);

			// a moment: two speakers, one after another
			int l0 = WotgScenes.Log.Count, f0 = DialogueSystem.FloatingDelivered, h0 = NoxusFights.HushDropped;
			WotgScenes.TestState = "Awaken_LegEmergence"; Step(20 + 2 * LevyConstants.SceneStepTicks + 60);
			int emerge = LinesOf("SCENE_AVATAR_EMERGE", l0);
			SCheck($"the Avatar's legs come out: {emerge} levies speak an EMERGE line", emerge == Math.Min(2, stand));
			// the boss has the floor: its scream. Nothing while it screams, then three after the gap
			WotgScenes.TestState = "Awaken_Scream"; int sStart = (int)Main.GameUpdateCount, s0 = WotgScenes.Log.Count; Step(300);
			int inScream = LinesSince(s0); int sEnd = (int)Main.GameUpdateCount;
			WotgScenes.TestState = "Teleport"; Step(20 + 3 * LevyConstants.SceneStepTicks + 200);
			var after = WotgScenes.Log.Skip(s0).Where(x => x.Pool == "SCENE_AVATAR_SCREAM").ToList();
			SCheck($"nothing is said while it screams ({inScream} lines in 300 ticks)", inScream == 0);
			SCheck($"after the scream {after.Count} levies react, the first {(after.Count > 0 ? after[0].Tick - sEnd : -1)} ticks after it ends (at least {LevyConstants.SceneVoiceGapTicks})", after.Count == Math.Min(3, stand) && after.Count > 0 && after[0].Tick - sEnd >= LevyConstants.SceneVoiceGapTicks);
			// Solyn or the subtitles have the floor while a moment is waiting
			int v0 = WotgScenes.Log.Count;
			WotgScenes.TestVoice = true; WotgScenes.TestState = "GivePlayerHeadpats"; Step(240);
			int duringVoice = LinesSince(v0);
			WotgScenes.TestVoice = false; int vEnd = (int)Main.GameUpdateCount; Step(LevyConstants.SceneVoiceGapTicks + 2 * LevyConstants.SceneStepTicks + 120);
			var pats = WotgScenes.Log.Skip(v0).Where(x => x.Pool == "SCENE_AVATAR_HEADPATS").ToList();
			SCheck($"while someone else has the floor nothing is said ({duringVoice} lines), and the waiting moment speaks after the gap ({pats.Count} lines, first {(pats.Count > 0 ? pats[0].Tick - vEnd : -1)} ticks after)", duringVoice == 0 && pats.Count >= 1 && pats[0].Tick - vEnd >= LevyConstants.SceneVoiceGapTicks);
			// once a fight
			int o0 = WotgScenes.Log.Count;
			WotgScenes.TestState = "Teleport"; Step(10); WotgScenes.TestState = "Awaken_LegEmergence"; Step(400);
			SCheck($"a moment speaks once a fight ({LinesOf("SCENE_AVATAR_EMERGE", o0)} lines the second time)", LinesOf("SCENE_AVATAR_EMERGE", o0) == 0);

			// the worried line now and then; the levies' own chatter stays held
			WotgScenes.TestState = "Teleport";
			int c0 = WotgScenes.Log.Count, fl0 = WotgScenes.FightLines, fd0 = DialogueSystem.FloatingDelivered, amb0 = SoldierNPC.ChatterLines, hh0 = NoxusFights.HushDropped;
			Step(60 * 120);
			var chatter = WotgScenes.Log.Skip(c0).ToList();
			int fightLines = WotgScenes.FightLines - fl0, generic = (DialogueSystem.FloatingDelivered - fd0) - chatter.Count;
			SNote($"two minutes of the Avatar fight: {fightLines} worried lines ({string.Join(" / ", chatter.Select(x => x.Text).Take(4))}), {generic} ordinary floating lines, ambient chatter tried {SoldierNPC.ChatterLines - amb0}, hush held {NoxusFights.HushDropped - hh0}");
			SCheck($"in two minutes of fight the army says {fightLines} worried lines, all from the fight's pool", fightLines >= 3 && fightLines <= 7 && chatter.All(x => x.Pool == "SCENE_AVATAR_FIGHT"));
			SCheck($"and none of its ordinary chatter ({generic} ordinary lines out)", generic == 0);
			var chats = army.Where(a => a != null && a.NPC.active && a.IsAlive).Select(a => DialogueSystem.ChatLine(a)).ToList();
			SCheck($"right-click chat in the fight is about the fight ({chats.Count(WotgScenes.IsFightChat)} of {chats.Count}: {chats.FirstOrDefault()})", chats.Count > 0 && chats.All(WotgScenes.IsFightChat));

			// hope, the fallen, the kill
			int k0 = WotgScenes.Log.Count;
			WotgScenes.TestLife = 0.1f; Step(20 + 2 * LevyConstants.SceneStepTicks + 60);
			SCheck($"nearly beaten: {LinesOf("SCENE_AVATAR_HOPE", k0)} levies hope", LinesOf("SCENE_AVATAR_HOPE", k0) == Math.Min(2, stand));
			SoldierNPC victim = army.LastOrDefault(a => a != null && a.NPC.active && a.IsAlive);
			string victimName = victim?.Record?.Name;
			if (victim != null) { victim.NPC.StrikeInstantKill(); Step(5); }
			bool dead = victim?.Record != null && victim.Record.Dead;
			SNote($"{victimName} struck down in the fight: record dead={dead}");
			int stand2 = Standing(army);
			WotgScenes.TestState = "LeaveAfterBeingHit"; Step(20 + 4 * LevyConstants.SceneStepTicks + 200);
			var mourn = WotgScenes.Log.Skip(k0).Where(x => x.Pool == "SCENE_WOTG_MOURN").ToList();
			SCheck($"the Avatar leaves for good: {LinesOf("SCENE_AVATAR_KILL", k0)} levies cheer", LinesOf("SCENE_AVATAR_KILL", k0) == Math.Min(3, stand2));
			if (dead) SCheck($"and one remembers {victimName} ({(mourn.Count > 0 ? mourn[0].Text : "nothing")})", mourn.Count == 1 && mourn[0].Text.Contains(victimName));
			else SNote("the struck levy's record did not read dead: the fallen-comrade line is not checked");

			// spacing: no two scripted lines closer than the gap
			int tight = 0; for (int i = 1; i < WotgScenes.Log.Count; i++) if (WotgScenes.Log[i].Tick - WotgScenes.Log[i - 1].Tick < LevyConstants.SceneLineGapTicks) tight++;
			SCheck($"no two fight lines closer than {LevyConstants.SceneLineGapTicks} ticks ({tight} of {WotgScenes.Log.Count})", tight == 0);

			// the commander falls
			WotgScenes.Reset(); WotgScenes.TestState = "Teleport"; WotgScenes.TestLife = 1f; Step(5);
			int d0 = WotgScenes.Log.Count;
			Player cmd = Main.player[0]; cmd.dead = true; WotgScenes.Tick((int)Main.GameUpdateCount); cmd.dead = false;
			int dropped0 = WotgScenes.CuesDropped;
			Step(30 + 2 * LevyConstants.SceneStepTicks + 60);
			SNote($"the commander falls at tick {(int)Main.GameUpdateCount - 30 - 2 * LevyConstants.SceneStepTicks - 60}: lines [{string.Join(" | ", WotgScenes.Log.Skip(d0).Select(x => x.Tick + " " + x.Pool))}], cues dropped {WotgScenes.CuesDropped - dropped0}, {Standing(army)} standing");
			SCheck($"the commander falls: {LinesOf("SCENE_WOTG_COMMANDER_DOWN", d0)} levies near the spot call out", LinesOf("SCENE_WOTG_COMMANDER_DOWN", d0) == Math.Min(2, Standing(army)));

			// the rift and the Nameless Deity
			int r0 = WotgScenes.Log.Count;
			WotgScenes.TestFight = WotgFight.Rift; WotgScenes.TestState = "Awaken"; Step(20 + 3 * LevyConstants.SceneStepTicks + 60);
			SCheck($"the rift opens: {LinesOf("SCENE_RIFT_OPEN", r0)} levies speak", LinesOf("SCENE_RIFT_OPEN", r0) == Math.Min(3, Standing(army)));
			int n0 = WotgScenes.Log.Count;
			WotgScenes.TestFight = WotgFight.Nameless; WotgScenes.TestState = "IntroScreamAnimation"; Step(200);
			int ndVoice = LinesSince(n0);
			WotgScenes.TestState = "ClockConstellation"; Step(LevyConstants.SceneVoiceGapTicks + 2 * LevyConstants.SceneStepTicks + 3 * LevyConstants.SceneStepTicks + 120);
			SCheck($"the Nameless Deity: silent through its scream ({ndVoice}), then {LinesOf("SCENE_NAMELESS_SCREAM", n0)} react to it and {LinesOf("SCENE_NAMELESS_CLOCK", n0)} to the clock", ndVoice == 0 && LinesOf("SCENE_NAMELESS_SCREAM", n0) >= 1 && LinesOf("SCENE_NAMELESS_CLOCK", n0) == 1);
			int nk0 = WotgScenes.Log.Count;
			WotgScenes.TestNamelessDefeated = true; WotgScenes.TestFight = WotgFight.None; Step(40 + 3 * LevyConstants.SceneStepTicks + 120);
			SCheck($"it leaves beaten: {LinesOf("SCENE_NAMELESS_KILL", nk0)} levies speak, and no homecoming on top ({LinesOf("SCENE_NAMELESS_HOMECOMING", nk0)})", LinesOf("SCENE_NAMELESS_KILL", nk0) == Math.Min(3, Standing(army)) && LinesOf("SCENE_NAMELESS_HOMECOMING", nk0) == 0);

			// the homecoming: beaten in its garden while the army waited at home
			WotgScenes.Reset(); WotgScenes.TestNamelessDefeated = false; WotgScenes.TestFight = WotgFight.None; Step(40);
			int hc0 = WotgScenes.Log.Count, hom0 = WotgScenes.Homecomings;
			WotgScenes.TestNamelessDefeated = true; Step(40 + LevyConstants.SceneHomecomingDelayTicks + 3 * LevyConstants.SceneStepTicks + 120);
			SCheck($"the commander comes home from the garden with the Nameless Deity beaten: {WotgScenes.Homecomings - hom0} homecoming, {LinesOf("SCENE_NAMELESS_HOMECOMING", hc0)} levies greet them", WotgScenes.Homecomings - hom0 == 1 && LinesOf("SCENE_NAMELESS_HOMECOMING", hc0) == Math.Min(3, Standing(army)));

			// the setting off: the old silence
			WotgScenes.Reset(); WotgScenes.TestEnabled = false; WotgScenes.TestNamelessDefeated = false; WotgScenes.TestFight = WotgFight.Avatar; WotgScenes.TestState = "Teleport"; Step(5);
			int off0 = WotgScenes.Log.Count, offF = DialogueSystem.FloatingDelivered;
			WotgScenes.TestState = "Awaken_LegEmergence"; Step(60 * 50);
			string offChat = army.FirstOrDefault(a => a != null && a.NPC.active && a.IsAlive) is SoldierNPC oc ? DialogueSystem.ChatLine(oc) : "...";
			SCheck($"with the setting off the fight is silent ({LinesSince(off0)} fight lines, {DialogueSystem.FloatingDelivered - offF} floating lines in fifty seconds, right-click \"{offChat}\")", LinesSince(off0) == 0 && DialogueSystem.FloatingDelivered - offF == 0 && offChat == "...");
			WotgScenes.TestEnabled = true; WotgScenes.TestFight = WotgFight.None; NoxusFights.Override = null; Step(5);

			RealNoxus(army);
		}

		/// <summary>The WotG half: the watch reads a real Avatar of Emptiness and a real Nameless Deity spawned in the main world (their own AI left to run).</summary>
		private static void RealNoxus(List<SoldierNPC> army)
		{
			if (!WotgScenes.Installed) { SNote("NoxusBoss is not loaded: the real Avatar and Nameless Deity are read in the WotG half"); return; }
			SNote("NoxusBoss members: found " + WotgScenes.Found + "; missing: " + (WotgScenes.Problem.Length == 0 ? "none" : WotgScenes.Problem));
			SCheck("every NoxusBoss member the watch reads is found", WotgScenes.Problem.Length == 0);
			WotgScenes.TestFight = null; WotgScenes.TestState = null; WotgScenes.TestLife = null; WotgScenes.TestVoice = null; WotgScenes.TestAvatarDefeated = null; WotgScenes.TestNamelessDefeated = null;
			_keepDuke = true;
			try
			{
				foreach ((int type, WotgFight want) in new[] { (WotgScenes.AvatarType, WotgFight.Avatar), (WotgScenes.NamelessType, WotgFight.Nameless) })
				{
					if (type < 0) continue;
					WotgScenes.Reset(); int m0 = WotgScenes.MomentsStarted, l0 = WotgScenes.Log.Count;
					int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent(), (int)_cmdAt.X + 400, (int)_cmdAt.Y - 300, type);
					var states = new List<string>(); bool seen = false;
					for (int t = 0; t < 900; t++)
					{
						Step(1);
						NPC n = i >= 0 && i < Main.maxNPCs ? Main.npc[i] : null;
						if (n == null || !n.active || n.type != type) break;
						foreach (SoldierNPC s in army) if (s != null && s.NPC.active) { s.NPC.life = s.NPC.lifeMax; }
						string st = WotgScenes.StateOf(n);
						if (st != null && (states.Count == 0 || states[^1] != st)) states.Add(st);
						if (WotgScenes.CurrentFight == want) seen = true;
					}
					SNote($"real {want}: states [{string.Join(" > ", states.Take(10))}], moments {WotgScenes.MomentsStarted - m0}, lines [{string.Join(" | ", WotgScenes.Log.Skip(l0).Select(x => x.Pool + ": " + x.Text).Take(5))}]");
					SCheck($"a real {want} is seen as that fight and its state is read by name ({states.Count} states, first {(states.Count > 0 ? states[0] : "none")})", seen && states.Count > 0 && states[0].StartsWith("Awaken"));
					for (int k = 0; k < Main.maxNPCs; k++) if (Main.npc[k].active && Main.npc[k].ModNPC?.Mod.Name == "NoxusBoss") Main.npc[k].active = false;
					for (int k = 0; k < Main.maxProjectiles; k++) if (Main.projectile[k].active && Main.projectile[k].ModProjectile?.Mod.Name == "NoxusBoss") Main.projectile[k].active = false;
					Step(3);
				}
			}
			finally { _keepDuke = false; WotgScenes.Reset(); }
		}
	}
}
