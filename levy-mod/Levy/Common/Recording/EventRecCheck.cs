using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// World self-check of the event recorder ("[EventRec] world self-check pass=N fail=M"), run at the end of the fight recorder's world check (FightRecAutoCheck, LEVY_SELFCHECK_WORLD=1; LEVY_EVENTREC=0 skips it).
	/// Scripted events through the vanilla start methods, the game clock stepped by hand: a Goblin Army (Main.StartInvasion) won by running its size out, with a levy standing 1500 tiles away and a Goblin Summoner;
	/// a Pumpkin Moon (Main.startPumpkinMoon) won at wave 15 with Mourning Wood (hand list) and a fallback miniboss; a Pumpkin Moon that meets dawn at wave 6 (survived); the Celestial Pillars all down (won);
	/// a boss during an event (two recordings at once, the boss one with the far levy); the setting switched on during an event; the hour cap stand-in (survived); the army wiped (lost, not restarted);
	/// and the recorder's own cost with 60 levies, 100 enemies and 100 hostile projectiles (OBSERVED line, size per hour).
	/// </summary>
	internal static class EventRecCheck
	{
		private static int _pass, _fail;
		private static Mod _mod;
		private static void Chk(bool ok, string name) { if (ok) { _pass++; _mod.Logger.Info("[EventRec] PASS " + name); } else { _fail++; _mod.Logger.Warn("[EventRec] FAIL " + name); } }
		private const int Floor = FightRecAutoCheck.Floor;

		private sealed class Rec
		{
			public JsonElement Hdr, End; public bool HasHdr, HasEnd;
			public readonly List<JsonElement> Frames = new(), Events = new(), Arenas = new(), Nt = new();
			public string Err; public int Lines;
		}

		private static Rec Read(string path)
		{
			var r = new Rec();
			if (path == null || !File.Exists(path)) { r.Err = "no file " + path; return r; }
			var lines = FightRecAutoCheck.ReadLines(path, path.EndsWith(".levyrec"), out string err);
			r.Err = err; r.Lines = lines.Count;
			foreach (string l in lines)
			{
				try
				{
					using var d = JsonDocument.Parse(l); var e = d.RootElement.Clone(); string k = e.GetProperty("k").GetString();
					switch (k)
					{
						case "hdr": r.Hdr = e; r.HasHdr = true; break;
						case "end": r.End = e; r.HasEnd = true; break;
						case "f": r.Frames.Add(e); break;
						case "e": r.Events.Add(e); break;
						case "arena": r.Arenas.Add(e); break;
						case "nt": r.Nt.Add(e); break;
					}
				}
				catch (Exception ex) { r.Err ??= "bad line: " + ex.Message; }
			}
			return r;
		}

		private static int NtImp(Rec r, int type) { int v = -1; foreach (var e in r.Nt) if (e.GetProperty("type").GetInt32() == type) v = e.GetProperty("imp").GetInt32(); return v; }
		private static bool HasEvent(Rec r, string kind, Func<JsonElement, bool> cond = null) { foreach (var e in r.Events) if (e.GetProperty("e").GetString() == kind && (cond == null || cond(e))) return true; return false; }
		private static bool ArenaCovers(Rec r, int tx, int ty, bool far = false)
		{
			foreach (var a in r.Arenas)
			{
				int x0 = a.GetProperty("x0").GetInt32(), y0 = a.GetProperty("y0").GetInt32(), w = a.GetProperty("w").GetInt32(), h = a.GetProperty("h").GetInt32();
				if (far && !(a.TryGetProperty("far", out var f) && f.GetInt32() == 1)) continue;
				if (tx >= x0 && tx < x0 + w && ty >= y0 && ty < y0 + h) return true;
			}
			return false;
		}
		/// <summary>True when some frame's l rows hold levy id at x beyond minX.</summary>
		private static bool LevyFar(Rec r, int id, float minX)
		{
			foreach (var f in r.Frames)
				if (f.TryGetProperty("l", out var l)) foreach (var row in l.EnumerateArray()) if (row[0].GetInt32() == id && row[1].GetDouble() > minX) return true;
			return false;
		}

		private static System.Reflection.FieldInfo AcidField(string name)
		{
			try { return ModLoader.TryGetMod("CalamityMod", out Mod cal) ? cal.Code?.GetType("CalamityMod.Events.AcidRainEvent")?.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public) : null; }
			catch { return null; }
		}

		private static int SpawnNpc(int x, int y, int type, int life = 0)
		{
			int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("EventRecCheck"), x, y, type);
			if (i >= 0 && i < Main.maxNPCs)
			{
				NPC b = Main.npc[i]; b.aiStyle = -1; b.noGravity = true; b.noTileCollide = true; b.velocity = Vector2.Zero; b.ai[3] = 4242f; b.timeLeft = 100000;
				if (life > 0) b.life = b.lifeMax = life;
			}
			return i;
		}

		private static void Kill(int i) { if (i < 0) return; NPC n = Main.npc[i]; EventRecorder.NoteKill(n); n.life = 0; n.active = false; }

		private static int StepUntilStopped(Player p, int max) { int n = 0; while (EventRecorder.On && n < max) { FightRecAutoCheck.One(p, -1, n); n++; } return n; }

		private static int MakeLevy(Player p, List<SoldierRecord> recs, List<int> spawned, Vector2 at, string name, int weapon)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = name,
				CommanderName = p.name, CommanderIndex = 0, LifeCrystals = 20, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
			rec.Gear.Weapon = new Item(weapon);
			RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx >= 0) { Main.npc[idx].Bottom = at; Main.npc[idx].velocity = Vector2.Zero; spawned.Add(idx); return rec.Id; }
			return int.MinValue;
		}

		private static void ResetWorldEvents()
		{
			Main.invasionType = 0; Main.invasionSize = 0; Main.invasionSizeStart = 0; Main.pumpkinMoon = false; Main.snowMoon = false; Main.bloodMoon = false; Main.eclipse = false;
			NPC.waveNumber = 0; NPC.waveKills = 0; NPC.LunarApocalypseIsUp = false;
			try { AcidField("AcidRainEventIsOngoing")?.SetValue(null, false); } catch { }
		}

		public static void Run(Mod mod, Player p, int cx, string dir, List<SoldierRecord> recs, List<int> spawned)
		{
			_mod = mod; _pass = _fail = 0;
			bool day = Main.dayTime; double time = Main.time; int oldCap = LevyConfig.Get.RenderCap; int oldCrystals = p.ConsumedLifeCrystals;
			var mine = new List<int>();
			try
			{
				ResetWorldEvents();
				FightRecAuto.TestMode = FightRecMode.Off; FightRecAuto.ClearCooldown(); FightRecorder.MaxFileBytes = 0; FightRecorder.MaxFolderBytes = 0;
				EventRecorder.TestEnabled = true; EventRecorder.ClearCooldown(); EventRecorder.TestMaxTicks = 0; EventRecorder.TestArmyGoneTicks = 0;
				p.ConsumedLifeCrystals = 15;
				LevyConfig.Get.RenderCap = 100;
				// a levy far away (1500 tiles to the right of the commander)
				int farX = Math.Min(Main.maxTilesX - 60, cx + 1500), farY = Floor;
				int farId = MakeLevy(p, recs, spawned, new Vector2(farX * 16f, farY * 16f), "Pvt. FarAway", ItemID.SpaceGun);
				Chk(farId != int.MinValue, "a levy stands " + (farX - cx) + " tiles away from the commander");
				FightRecAutoCheck.Step(40, p, null);
				Chk(!EventRecorder.On, "no event, no event recording");

				// ---------- 1. Goblin Army, started the vanilla way, won by running its size out
				Main.StartInvasion(InvasionID.GoblinArmy);
				Chk(Main.invasionType == InvasionID.GoblinArmy && Main.invasionSize > 0, "Main.StartInvasion starts a Goblin Army (size " + Main.invasionSize + ")");
				FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On && EventRecorder.MainEvent == "goblinarmy", "the Goblin Army starts one event recording (" + EventRecorder.MainEvent + ")");
				string run = EventRecorder.Path;
				Chk(run != null && Path.GetFileName(run).StartsWith("event_goblinarmy_"), "the file is named for the event while it records (" + Path.GetFileName(run ?? "none") + ")");
				int summoner = SpawnNpc(cx * 16 + 500, (Floor - 4) * 16, NPCID.GoblinSummoner);
				var peons = new List<int>(); for (int k = 0; k < 4; k++) peons.Add(SpawnNpc(cx * 16 + 300 + k * 40, (Floor - 3) * 16, NPCID.GoblinPeon));
				FightRecAutoCheck.Step(420, p, null);
				Kill(summoner); foreach (int i in peons) Kill(i);
				FightRecAutoCheck.Step(30, p, null);
				Main.invasionSize = 0; FightRecAutoCheck.Step(2, p, null); Main.invasionType = 0; // what Main.UpdateInvasion does when the size runs out
				int after = StepUntilStopped(p, 200);
				Chk(!EventRecorder.On && EventRecorder.LastResult == "won", "the recording ends with the invasion, result won (" + EventRecorder.LastResult + ", " + after + " ticks)");
				string f1 = EventRecorder.LastPath;
				Chk(f1 != null && Regex.IsMatch(Path.GetFileName(f1), @"^event_goblinarmy_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}_won(_\d+)?\.levyrec$"), "saved as event_goblinarmy_date_won.levyrec (" + Path.GetFileName(f1 ?? "none") + ")");
				var r1 = Read(f1);
				Chk(r1.Err == null && r1.HasHdr && r1.HasEnd, "the file decompresses and every line parses (" + (r1.Err ?? r1.Lines + " lines") + ")");
				if (r1.HasHdr && r1.HasEnd)
				{
					Chk(r1.Hdr.GetProperty("format").GetInt32() == 12 && r1.Hdr.GetProperty("kind").GetString() == "event" && r1.Hdr.GetProperty("event").GetString() == "goblinarmy", "header: format 12, kind event, event goblinarmy");
					Chk(r1.Hdr.GetProperty("roster").TryGetProperty(NPCID.GoblinSummoner.ToString(), out var ro) && ro.GetProperty("imp").GetInt32() == 1, "header: the roster names the Goblin Summoner a miniboss");
					Chk(LevyFar(r1, farId, (cx + 1000) * 16f), "the levy 1500 tiles away is in the frames");
					Chk(ArenaCovers(r1, farX, farY - 2) && ArenaCovers(r1, cx, Floor - 2), "terrain is recorded around the far levy and around the commander");
					Chk(NtImp(r1, NPCID.GoblinSummoner) == 1 && NtImp(r1, NPCID.GoblinPeon) == 0, "the Goblin Summoner is labelled a miniboss, the Goblin Peon a normal enemy (" + NtImp(r1, NPCID.GoblinSummoner) + ", " + NtImp(r1, NPCID.GoblinPeon) + ")");
					Chk(HasEvent(r1, "ekill", e => e.GetProperty("npc").GetInt32() == NPCID.GoblinSummoner && e.GetProperty("imp").GetInt32() == 1), "the Summoner's death is an ekill event");
					bool aType = false; foreach (var f in r1.Frames) if (f.TryGetProperty("a", out var a)) foreach (var row in a.EnumerateArray()) if (row[0].GetInt32() == NPCID.GoblinSummoner) aType = true;
					Chk(aType, "enemy rows carry the type");
					var e1 = r1.End;
					Chk(e1.GetProperty("result").GetString() == "won" && e1.GetProperty("kind").GetString() == "event" && e1.GetProperty("kills").GetInt32() >= 5 && e1.GetProperty("minibossKills").GetInt32() == 1 && e1.GetProperty("results").GetProperty("goblinarmy").GetString() == "won",
						"end: won, 5 kills, 1 miniboss kill, results.goblinarmy won (" + e1.GetRawText().Substring(0, Math.Min(220, e1.GetRawText().Length)) + ")");
					Chk(e1.GetProperty("frames").GetInt32() == r1.Frames.Count && r1.Frames.Count >= 25 && r1.Hdr.GetProperty("hz").GetInt32() == 5, "end: frame count matches, 5 Hz (" + r1.Frames.Count + " frames)");
				}

				// ---------- 2. Pumpkin Moon to wave 15: won; Mourning Wood by the hand list, a big ordinary type by the fallback
				EventRecorder.ClearCooldown(); Main.dayTime = false; Main.time = 0;
				Main.startPumpkinMoon();
				FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On && EventRecorder.MainEvent == "pumpkinmoon", "Main.startPumpkinMoon starts an event recording");
				var foes = new List<int> { SpawnNpc(cx * 16 + 200, (Floor - 3) * 16, NPCID.Splinterling, 200), SpawnNpc(cx * 16 + 260, (Floor - 3) * 16, NPCID.Hellhound, 210), SpawnNpc(cx * 16 + 320, (Floor - 3) * 16, NPCID.Poltergeist, 190) };
				FightRecAutoCheck.Step(12, p, null);
				int wood = SpawnNpc(cx * 16 + 600, (Floor - 6) * 16, NPCID.MourningWood);
				int crow = SpawnNpc(cx * 16 + 700, (Floor - 3) * 16, NPCID.Scarecrow1, 5000);
				FightRecAutoCheck.Step(420, p, null);
				NPC.waveNumber = 8; FightRecAutoCheck.Step(30, p, null);
				NPC.waveNumber = 15; FightRecAutoCheck.Step(30, p, null);
				Kill(wood); Kill(crow); foreach (int i in foes) Kill(i);
				Main.stopMoonEvent(); // dawn
				StepUntilStopped(p, 200);
				Chk(EventRecorder.LastResult == "won", "a Pumpkin Moon that reached wave 15 is won (" + EventRecorder.LastResult + ")");
				var r2 = Read(EventRecorder.LastPath);
				Chk(r2.HasEnd && Path.GetFileName(EventRecorder.LastPath ?? "").StartsWith("event_pumpkinmoon_"), "saved as event_pumpkinmoon_... (" + Path.GetFileName(EventRecorder.LastPath ?? "none") + ")");
				Chk(NtImp(r2, NPCID.MourningWood) == 1, "Mourning Wood is labelled a miniboss (hand list)");
				Chk(NtImp(r2, NPCID.Scarecrow1) == 1 && NtImp(r2, NPCID.Splinterling) == 0, "a type with 25 times the median life of the ordinary ones is a miniboss by the fallback (" + NtImp(r2, NPCID.Scarecrow1) + ")");
				Chk(HasEvent(r2, "wave", e => e.GetProperty("n").GetInt32() == 15) && r2.HasEnd && r2.End.GetProperty("waveMax").GetInt32() == 15, "the wave number is recorded (wave events, end.waveMax 15)");

				// ---------- 3. Pumpkin Moon that meets dawn at wave 6: survived
				EventRecorder.ClearCooldown(); Main.startPumpkinMoon(); FightRecAutoCheck.Step(40, p, null);
				NPC.waveNumber = 6; FightRecAutoCheck.Step(320, p, null);
				Main.stopMoonEvent(); StepUntilStopped(p, 200);
				Chk(EventRecorder.LastResult == "survived" && (EventRecorder.LastPath ?? "").Contains("_survived"), "a Pumpkin Moon that meets dawn at wave 6 is survived (" + EventRecorder.LastResult + ")");

				// ---------- 4. the Celestial Pillars: highlighted, won when all four are down
				EventRecorder.ClearCooldown(); FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown();
				var towers = new List<int>(); int k2 = 0;
				foreach (int t in new[] { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust }) towers.Add(SpawnNpc(cx * 16 - 800 + 400 * k2++, (Floor - 20) * 16, t));
				NPC.LunarApocalypseIsUp = true;
				FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On && EventRecorder.MainEvent == "pillars", "standing Celestial Pillars start an event recording");
				Chk(!FightRecorder.On, "the pillars do not start a boss recording");
				FightRecAutoCheck.Step(420, p, null);
				foreach (int i in towers) { Kill(i); FightRecAutoCheck.Step(20, p, null); }
				NPC.LunarApocalypseIsUp = false; FightRecAuto.TestMode = FightRecMode.Off;
				StepUntilStopped(p, 200);
				var r4 = Read(EventRecorder.LastPath);
				Chk(EventRecorder.LastResult == "won" && r4.HasEnd && r4.End.GetProperty("bossKills").GetInt32() == 4, "the pillars all down is won, four boss-level kills (" + EventRecorder.LastResult + ")");
				Chk(NtImp(r4, NPCID.LunarTowerSolar) == 2 && NtImp(r4, NPCID.LunarTowerStardust) == 2, "every pillar is labelled at boss importance");

				// ---------- 5. a boss during an event: its own boss recording, the event recording carries on; the boss recording has the far levy
				EventRecorder.ClearCooldown(); FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown();
				Main.StartInvasion(InvasionID.GoblinArmy); FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On, "a second Goblin Army records again after the first ended");
				int boss = FightRecAutoCheck.SpawnBoss(cx);
				FightRecAutoCheck.Step(40, p, boss);
				Chk(EventRecorder.On && FightRecorder.On, "a boss during the event gets its own recording and the event recording keeps running");
				FightRecAutoCheck.ScriptHits(p, boss, 300);
				// the fight travels: the commander (and the boss with her) goes 1400 tiles away mid-recording, then comes back
				Vector2 home = p.position; int travelX = Math.Max(60, cx - 1400);
				p.position = new Vector2(travelX * 16f, (Floor - 3) * 16f); FightRecAutoCheck.ScriptHits(p, boss, 240);
				p.position = home; FightRecAutoCheck.ScriptHits(p, boss, 60);
				FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false;
				FightRecAutoCheck.StepUntilStopped(p, -1, 1500);
				string bossFile = FightRecorder.LastResult;
				Chk(!FightRecorder.On && EventRecorder.On, "the boss recording ends with the boss; the event recording carries on");
				var rb = Read(bossFile);
				Chk(rb.HasHdr && rb.Hdr.GetProperty("kind").GetString() == "fight" && LevyFar(rb, farId, (cx + 1000) * 16f), "the boss recording (kind fight) carries the levy 1500 tiles away");
				Chk(ArenaCovers(rb, farX, farY - 2, far: true), "the boss recording has terrain around the far levy (far cells)");
				Chk(ArenaCovers(rb, travelX, Floor - 3) && ArenaCovers(rb, travelX + 80, Floor - 3) && ArenaCovers(rb, travelX, Floor - 40), "the boss recording has terrain where the commander travelled mid-fight, 1400 tiles from the start (" + rb.Arenas.Count + " arena records)");
				FightRecAuto.TestMode = FightRecMode.Off;
				Main.invasionSize = 0; Main.invasionType = 0; StepUntilStopped(p, 200);
				Chk(EventRecorder.LastResult == "won", "that Goblin Army is won too (" + EventRecorder.LastResult + ")");
				var r5 = Read(EventRecorder.LastPath);
				Chk(ArenaCovers(r5, travelX, Floor - 3) && ArenaCovers(r5, travelX + 80, Floor - 3), "the event recording has terrain where the commander travelled mid-event");

				// ---------- 6. the setting turned on during an event starts a recording; turning it off ends it as ended
				EventRecorder.ClearCooldown(); EventRecorder.TestEnabled = false;
				Main.eclipse = true; Main.dayTime = true; FightRecAutoCheck.Step(60, p, null);
				Chk(!EventRecorder.On, "with the setting off an eclipse is not recorded");
				EventRecorder.TestEnabled = true; FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On && EventRecorder.MainEvent == "solareclipse", "turning the setting on during the eclipse starts its recording");
				FightRecAutoCheck.Step(320, p, null);
				EventRecorder.TestEnabled = false; FightRecAutoCheck.Step(2, p, null);
				Chk(!EventRecorder.On && EventRecorder.LastResult == "ended", "turning the setting off ends it, result ended (" + EventRecorder.LastResult + ")");

				// ---------- 7. the hour cap (a short stand-in): survived, and the same eclipse is not recorded again
				EventRecorder.TestEnabled = true; EventRecorder.ClearCooldown(); EventRecorder.TestMaxTicks = 400; FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On, "the eclipse records again");
				StepUntilStopped(p, 600);
				Chk(EventRecorder.LastResult == "survived", "the time cap ends it as survived (" + EventRecorder.LastResult + ")");
				FightRecAutoCheck.Step(200, p, null);
				Chk(!EventRecorder.On, "a capped event is not recorded again until it ends");
				EventRecorder.TestMaxTicks = 0; Main.eclipse = false; FightRecAutoCheck.Step(40, p, null);

				// ---------- 8. cost and size: 60 levies, 100 enemies, 100 hostile projectiles, a minute of Blood Moon
				Cost(p, cx, recs, spawned, mine);

				// ---------- 9. the army wiped: lost, and not started again while the Blood Moon lasts
				EventRecorder.ClearCooldown(); EventRecorder.TestArmyGoneTicks = 300;
				Main.bloodMoon = true; Main.dayTime = false; FightRecAutoCheck.Step(40, p, null);
				Chk(EventRecorder.On && EventRecorder.MainEvent == "bloodmoon", "a Blood Moon starts an event recording");
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) { EventRecorder.NoteLevyDeath(Main.npc[i]); Main.npc[i].active = false; }
				int n9 = StepUntilStopped(p, 800);
				Chk(!EventRecorder.On && EventRecorder.LastResult == "lost", "no levy standing for the grace ends it as lost (" + EventRecorder.LastResult + ", " + n9 + " ticks)");
				Chk((EventRecorder.LastPath ?? "").Contains("event_bloodmoon_") && (EventRecorder.LastPath ?? "").EndsWith("_lost.levyrec"), "saved as event_bloodmoon_..._lost (" + Path.GetFileName(EventRecorder.LastPath ?? "none") + ")");
				var r9 = Read(EventRecorder.LastPath);
				Chk(r9.HasEnd && r9.End.GetProperty("levyDeaths").GetInt32() >= 50 && r9.End.GetProperty("results").GetProperty("bloodmoon").GetString() == "lost", "end: every levy death counted, the Blood Moon lost");
				FightRecAutoCheck.Step(200, p, null);
				Chk(!EventRecorder.On, "the same Blood Moon is not recorded again after the army was lost");
				Main.bloodMoon = false; FightRecAutoCheck.Step(40, p, null);

				// ---------- 9b. the Old One's Army sentry mode stands the regulars down on purpose: that is not the army gone
				bool oldSentry = LevyConfig.Get.OldOnesArmyUsesLevySentries;
				try
				{
					LevyConfig.Get.OldOnesArmyUsesLevySentries = true;
					int ooaLevy = MakeLevy(p, recs, spawned, new Vector2((cx - 4) * 16f, Floor * 16f), "Pvt. Sentry", ItemID.SpaceGun);
					EventRecorder.ClearCooldown(); Terraria.GameContent.Events.DD2Event.Ongoing = true; FightRecAutoCheck.Step(40, p, null);
					Chk(EventRecorder.On && EventRecorder.MainEvent == "oldonesarmy", "the Old One's Army starts an event recording");
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false; // the stand-down takes the bodies off the field
					FightRecAutoCheck.Step(700, p, null);
					Chk(EventRecorder.On, "with the regulars stood down by the sentry mode the recording does not end as the army gone");
					Terraria.GameContent.Events.DD2Event.Ongoing = false; StepUntilStopped(p, 200);
					Chk(EventRecorder.LastResult == "ended", "an Old One's Army that stopped with no win or loss reads ended (" + EventRecorder.LastResult + ")");
				}
				finally { LevyConfig.Get.OldOnesArmyUsesLevySentries = oldSentry; Terraria.GameContent.Events.DD2Event.Ongoing = false; }

				// ---------- 10. Calamity's Acid Rain (reflection, only when Calamity is loaded): won when its kill points run out
				var acidOn = AcidField("AcidRainEventIsOngoing"); var acidPts = AcidField("AccumulatedKillPoints");
				if (acidOn != null && acidPts != null)
				{
					MakeLevy(p, recs, spawned, new Vector2((cx - 3) * 16f, Floor * 16f), "Pvt. Acid", ItemID.SpaceGun); // the army-wiped check removed every body
					EventRecorder.ClearCooldown(); acidOn.SetValue(null, true); acidPts.SetValue(null, 40); FightRecAutoCheck.Step(40, p, null);
					Chk(EventRecorder.On && EventRecorder.MainEvent == "acidrain", "Calamity's Acid Rain starts an event recording");
					acidPts.SetValue(null, 12); FightRecAutoCheck.Step(400, p, null); acidPts.SetValue(null, 1); FightRecAutoCheck.Step(20, p, null);
					acidPts.SetValue(null, 0); acidOn.SetValue(null, false); StepUntilStopped(p, 200);
					Chk(EventRecorder.LastResult == "won" && (EventRecorder.LastPath ?? "").Contains("event_acidrain_"), "Acid Rain whose points ran out is won (" + EventRecorder.LastResult + ")");
				}
				else _mod.Logger.Info("[EventRec] OBSERVED Acid Rain not checked: Calamity is not loaded");
			}
			catch (Exception e) { _fail++; _mod.Logger.Warn("[EventRec] FAIL threw " + e); }
			finally
			{
				try { if (EventRecorder.On) EventRecorder.Stop("self-check over"); } catch { }
				ResetWorldEvents(); Main.dayTime = day; Main.time = time; LevyConfig.Get.RenderCap = oldCap; p.ConsumedLifeCrystals = oldCrystals;
				EventRecorder.TestEnabled = false; EventRecorder.TestMaxTicks = 0; EventRecorder.TestArmyGoneTicks = 0; FightRecAuto.TestMode = FightRecMode.Off;
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ai[3] == 4242f && !(Main.npc[i].ModNPC is SoldierNPC)) Main.npc[i].active = false;
				for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].ai[2] == 4242f) Main.projectile[i].active = false;
			}
			if (_fail == 0) mod.Logger.Info($"[EventRec] world self-check pass={_pass} fail=0");
			else mod.Logger.Warn($"[EventRec] world self-check pass={_pass} fail={_fail}");
		}

		/// <summary>The recorder's own cost and the file size with a big army: 60 levies spread over 120 tiles, 100 enemies and 100 hostile projectiles all moving, one minute of a Blood Moon at 10 Hz.</summary>
		private static void Cost(Player p, int cx, List<SoldierRecord> recs, List<int> spawned, List<int> mine)
		{
			var rnd = new Random(13);
			int have = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) have++;
			for (int i = 0; have < 60 && i < 80; i++) { int id = MakeLevy(p, recs, spawned, new Vector2((cx - 60 + i * 2) * 16f, Floor * 16f), "Pvt. Cost" + i, i % 3 == 0 ? ItemID.Zenith : i % 3 == 1 ? ItemID.SpaceGun : ItemID.Megashark); if (id != int.MinValue) { have++; mine.Add(spawned[spawned.Count - 1]); } }
			var foes = new List<int>();
			int[] types = { NPCID.Zombie, NPCID.BloodZombie, NPCID.Drippler, NPCID.DemonEye, NPCID.BlueSlime };
			for (int k = 0; k < 100; k++) { int i = SpawnNpc(cx * 16 - 1600 + k * 32, (Floor - 3 - k % 5) * 16, types[k % types.Length], 200); if (i >= 0) foes.Add(i); }
			var shots = new List<int>();
			for (int k = 0; k < 100; k++)
			{
				int j = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_WorldEvent("EventRecCheck"), p.Center + new Vector2(rnd.Next(-900, 900), rnd.Next(-500, 200)), Vector2.Zero, ProjectileID.DeathLaser, 20, 0f);
				if (j >= 0) { Projectile pr = Main.projectile[j]; pr.hostile = true; pr.friendly = false; pr.timeLeft = 100000; pr.ai[2] = 4242f; shots.Add(j); }
			}
			var basePos = new Dictionary<int, Vector2>(); foreach (int i in spawned) if (Main.npc[i].active) basePos[i] = Main.npc[i].Center; foreach (int i in foes) basePos[i] = Main.npc[i].Center;
			EventRecorder.ClearCooldown(); Main.bloodMoon = true; Main.dayTime = false;
			int levies = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) levies++;
			const int Ticks = 3600 + 40; EventRecorder.ProfileParts = true;
			for (int k = 0; k < Ticks; k++)
			{
				foreach (int i in spawned) { NPC n = Main.npc[i]; if (!n.active || !basePos.ContainsKey(i)) continue; float ph = k / 40f + i; n.Center = basePos[i] + new Vector2((float)Math.Sin(ph) * 300f + rnd.Next(-2, 3), (float)Math.Cos(ph * 0.7) * 40f - 60); n.velocity = new Vector2((float)Math.Cos(ph) * 7.5f, -(float)Math.Sin(ph * 0.7) * 0.7f); if (k % 97 == i % 97) n.life = Math.Max(1, n.life - 7); }
				foreach (int i in foes) { NPC n = Main.npc[i]; if (!n.active) continue; float ph = k / 25f + i; n.Center = basePos[i] + new Vector2((float)Math.Sin(ph) * 200f, (float)Math.Cos(ph) * 80f - 200); n.velocity = new Vector2((float)Math.Cos(ph) * 2f, 1f); }
				for (int s = 0; s < shots.Count; s++) { Projectile pr = Main.projectile[shots[s]]; pr.position += new Vector2((float)Math.Sin(k / 15f + s) * 6f, 5f); if (pr.position.Y > Floor * 16f) pr.position.Y -= 900f; pr.velocity = new Vector2(2f, 5f); pr.active = true; }
				p.position.X = cx * 16f + (float)Math.Sin(k / 60f) * 400f; p.velocity.X = (float)Math.Cos(k / 60f) * 3f;
				FightRecAutoCheck.One(p, -1, k);
			}
			string path = EventRecorder.Path; bool was = EventRecorder.On; EventRecorder.ProfileParts = false;
			double fq = 1e6 / System.Diagnostics.Stopwatch.Frequency, ns = Math.Max(1, (Ticks - 40) / EventRecorder.SampleEvery);
			string parts = $"per sample: levies {EventRecorder.PartL * fq / ns:0} us, enemies {EventRecorder.PartA * fq / ns:0} us, projectiles {EventRecorder.PartJ * fq / ns:0} us, queueing the line {EventRecorder.PartW * fq / ns:0} us; per tick: event state {EventRecorder.PartPoll * fq / Ticks:0.0} us, levy hit poll {EventRecorder.PartLevyPoll * fq / Ticks:0.0} us";
			Main.bloodMoon = false; StepUntilStopped(p, 200);
			foreach (int j in shots) Main.projectile[j].active = false;
			foreach (int i in foes) Main.npc[i].active = false;
			string f = EventRecorder.LastPath;
			Chk(was && f != null && File.Exists(f), "the cost run made a recording (" + levies + " levies)");
			if (f == null || !File.Exists(f)) return;
			long raw = 0; foreach (string l in FightRecAutoCheck.ReadLines(f, true, out _)) raw += l.Length + 1;
			long disk = new FileInfo(f).Length; double min = Ticks / 3600.0;
			double mbHour = disk / min * 60.0 / 1048576.0;
			// a load-robust yardstick from the same boot: what the game spends updating these 60 levies (NPC.UpdateNPC), per tick
			var armySlots = new List<int>(); for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) armySlots.Add(i);
			long a0 = System.Diagnostics.Stopwatch.GetTimestamp();
			for (int k = 0; k < 300; k++) { foreach (int i in armySlots) { NPC n = Main.npc[i]; if (n.active) n.UpdateNPC(i); } p.active = true; }
			double armyUs = (System.Diagnostics.Stopwatch.GetTimestamp() - a0) * 1e6 / System.Diagnostics.Stopwatch.Frequency / 300.0;
			parts += $"; the same {armySlots.Count} levies' own update (NPC.UpdateNPC) costs {armyUs:0} us per tick on this boot, so the recorder adds {100.0 * EventRecorder.LastMeanUs / Math.Max(1, armyUs):0.0}% to it";
			var h = EventRecorder.LastHist; int tot = 0; foreach (int x in h) tot += x;
			string hist = ""; for (int b = 0; b < h.Length; b++) hist += (b < EventRecorder.CostEdgesUs.Length ? "<" + EventRecorder.CostEdgesUs[b] : ">=" + EventRecorder.CostEdgesUs[b - 1]) + "us:" + h[b] + " ";
			int slow = 0; for (int b = 7; b < h.Length; b++) slow += h[b];
			_mod.Logger.Info($"[EventRec] OBSERVED cost with {levies} levies, 100 enemies, 100 hostile projectiles, one minute at 5 Hz: recorder mean {EventRecorder.LastMeanUs:0.0} us per tick, worst tick {EventRecorder.LastMaxMs:0.00} ms, {EventRecorder.LastSampleUs:0} us per sample; "
				+ $"file {disk / 1024} KB on disk ({raw / 1024} KB of text) = {mbHour:0.0} MB per hour of event. Ticks by cost: {hist}({tot} ticks, {slow} at 2 ms or more). {parts}");
			Chk(levies >= 50, "the cost run had at least 50 levies (" + levies + ")");
			Chk(EventRecorder.LastMeanUs < 2000, "the recorder costs under 2 ms per tick on average (" + EventRecorder.LastMeanUs.ToString("0.0") + " us)");
			Chk(mbHour < 120, "an hour of this event stays under 120 MB (" + mbHour.ToString("0.0") + " MB)");
		}
	}
}
