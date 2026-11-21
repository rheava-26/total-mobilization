using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Self-checks of the automatic fight recorder, two stages.
	/// BOOT ("[FightRecAuto] self-check pass=N fail=M", every load): the background writer (gzip round trip, plain mode, a crash-cut file still readable, size cap, an unwritable path), the folder rules (fallback to the default
	/// folder with a warning, creating a missing folder, oldest-first cleanup, file names, crash recovery), the config defaults and class names. Uses a throw-away folder in the system temp directory and removes it.
	/// WORLD ("[FightRecAuto] world self-check pass=N fail=M", a dedicated server with LEVY_SELFCHECK_WORLD=1 once a world loads): a scripted boss and a scripted skirmish drive the real start / sample / stop path with the
	/// game clock stepped by hand: Boss fights starts on the boss and not on the skirmish, All fights starts on both, each stops a few seconds after it ends, the file name, header, end record, damage events and compression
	/// round trip are read back, the size cap stops a recording, the folder cap removes the oldest file, and an unwritable chosen folder falls back to the default with a chat warning.
	/// Client-only parts (the config screen, chat output in a real game, the player's own commander) are not reached here.
	/// </summary>
	public class FightRecAutoCheck : ModSystem
	{
		private static string NewTemp() { string d = Path.Combine(Path.GetTempPath(), "levy_fightrec_check_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(d); return d; }
		private static void Rm(string d) { try { Directory.Delete(d, true); } catch { } }

		internal static List<string> ReadLines(string path, bool gz, out string err)
		{
			err = null; var l = new List<string>();
			try
			{
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				Stream s = gz ? new GZipStream(fs, CompressionMode.Decompress) : fs;
				using var r = new StreamReader(s);
				string line; while ((line = r.ReadLine()) != null) l.Add(line);
			}
			catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
			return l;
		}

		// ============================================================================================ boot
		public override void PostSetupRecipes()
		{
			int pass = 0; var fails = new List<string>();
			void Check(bool c, string w) { if (c) pass++; else fails.Add(w); }
			string tmp = null;
			try
			{
				tmp = NewTemp();
				// ---- writer: gzip round trip
				string a = Path.Combine(tmp, "a.levyrec"), b = Path.Combine(tmp, "b.levyrec");
				var w = new RecWriter(a, true, 0);
				const int N = 3000;
				for (int i = 0; i < N; i++) w.WriteLine("{\"k\":\"f\",\"t\":" + i + ",\"p\":[100.5,200,1,2,3,4,5],\"l\":[[1,2,3,4],[5,6,7,8]]}");
				w.WriteLine("{\"k\":\"end\"}", true);
				Check(File.Exists(a + ".part"), "gzip recording is written under a .part name while open");
				string doneAs = "unset"; w.Close(b, p => doneAs = p, true);
				Check(w.Done && doneAs == b && File.Exists(b) && !File.Exists(a + ".part") && !File.Exists(a), "close moves the .part file to the final name and reports it");
				var lines = ReadLines(b, true, out string e1);
				Check(e1 == null && lines.Count == N + 1 && lines[0].Contains("\"t\":0,") && lines[N - 1].Contains("\"t\":" + (N - 1) + ",") && lines[N] == "{\"k\":\"end\"}", "gzip round trip returns every line in order (" + (e1 ?? lines.Count.ToString()) + ")");
				long raw = 0; foreach (string x in lines) raw += x.Length + 1;
				Check(new FileInfo(b).Length * 4 < raw, "gzip file is under a quarter of the text (" + new FileInfo(b).Length + " of " + raw + ")");
				Check(w.Dropped == 0 && w.Error == null, "no lines dropped and no write error");
				// ---- writer: plain
				string pj = Path.Combine(tmp, "p.jsonl");
				var wp = new RecWriter(pj, false, 0); wp.WriteLine("one"); wp.WriteLine("two", true); wp.Close(null, null, true);
				var pl = ReadLines(pj, false, out _);
				Check(pl.Count == 2 && pl[1] == "two" && !File.Exists(pj + ".part"), "plain recording is written in place");
				// ---- writer: a file cut by a crash still reads up to the last flush
				int oldFlush = RecWriter.FlushMs; RecWriter.FlushMs = 20;
				try
				{
					string c = Path.Combine(tmp, "c.levyrec");
					var wc = new RecWriter(c, true, 0);
					for (int i = 0; i < 400; i++) wc.WriteLine("{\"k\":\"f\",\"t\":" + i + "}");
					Check(wc.Flush(), "a flush on demand returns once the file holds the queued lines");
					string cut = Path.Combine(tmp, "cut.levyrec"); using (var src = new FileStream(c + ".part", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var dst = File.Create(cut)) src.CopyTo(dst);
					var cl = ReadLines(cut, true, out string ce);
					Check(cl.Count >= 390 && cl[0] == "{\"k\":\"f\",\"t\":0}", "a recording copied while still open reads back its flushed lines (" + cl.Count + ")");
					wc.Close(null, null, true);
					// ---- writer: size cap
					string d = Path.Combine(tmp, "cap.levyrec");
					var wd = new RecWriter(d, true, 3000);
					var rnd = new Random(7); var buf = new byte[240]; int sent = 0;
					for (int ms = 0; ms < 3000 && !wd.Full; ms += 10)
					{
						rnd.NextBytes(buf); wd.WriteLine("{\"k\":\"f\",\"x\":\"" + Convert.ToBase64String(buf) + "\"}"); sent++;
						System.Threading.Thread.Sleep(10);
					}
					Check(wd.Full, "the size cap flags a full recording");
					long dropBefore = wd.Dropped; wd.WriteLine("late"); wd.WriteLine("{\"k\":\"end\",\"r\":1}", true);
					Check(wd.Dropped == dropBefore + 1, "a line after the cap is dropped, a forced line is not");
					wd.Close(null, null, true);
					var dl = ReadLines(d, true, out string de);
					Check(de == null && dl.Count > 0 && dl[dl.Count - 1] == "{\"k\":\"end\",\"r\":1}", "the closing line still lands after the cap (" + (de ?? dl.Count.ToString()) + ")");
					Check(new FileInfo(d).Length < 3000 + 6000, "the capped file stays near the cap (" + new FileInfo(d).Length + " bytes for 3000)");
				}
				finally { RecWriter.FlushMs = oldFlush; }
				// ---- writer: unwritable path
				bool opened = RecWriter.TryOpen(Path.Combine(tmp, "nope", "x.levyrec"), true, 0, out _, out string why0);
				Check(!opened && why0 != null, "a recording in a missing folder fails at start with a reason, not later");

				// ---- folder: fallback
				string def = Path.Combine(tmp, "default");
				string r1 = FightRecFolder.Resolve("", def, out string w1);
				Check(r1 == def && w1 == null && Directory.Exists(def), "empty setting uses the default folder and creates it");
				string nested = Path.Combine(tmp, "mine", "deeper");
				string r2 = FightRecFolder.Resolve(nested, def, out string w2);
				Check(r2 == Path.GetFullPath(nested) && w2 == null && Directory.Exists(nested), "a chosen folder that does not exist is created");
				string blocker = Path.Combine(tmp, "blocker"); File.WriteAllText(blocker, "x");
				string r3 = FightRecFolder.Resolve(Path.Combine(blocker, "sub"), def, out string w3);
				Check(r3 == def && w3 != null && w3.Contains("Can't write") && w3.Contains(def), "an unwritable chosen folder falls back to the default with a warning");
				string r4 = FightRecFolder.Resolve("relative/folder", def, out string w4);
				Check(r4 == def && w4 != null, "a path that is not a full path falls back with a warning");
				string r5 = FightRecFolder.Resolve(Path.Combine(blocker, "sub"), Path.Combine(blocker, "also"), out string w5);
				Check(r5 == null && w5 != null && w5.Contains("off"), "when the default is unwritable too, recording is off with a warning");
				Check(FightRecFolder.Expand("  \"" + tmp + "\"  ") == Path.GetFullPath(tmp), "quotes and spaces around a pasted path are ignored");

				// ---- folder: cleanup oldest first
				string cd = Path.Combine(tmp, "cleanup"); Directory.CreateDirectory(cd);
				for (int i = 1; i <= 5; i++) { string f = Path.Combine(cd, "f" + i + FightRecFolder.Ext); File.WriteAllBytes(f, new byte[1000]); File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-(6 - i))); }
				File.WriteAllBytes(Path.Combine(cd, "notes.txt"), new byte[5000]);
				int gone = FightRecFolder.Enforce(cd, 2500, null, out long tot);
				Check(gone == 3 && !File.Exists(Path.Combine(cd, "f1.levyrec")) && !File.Exists(Path.Combine(cd, "f3.levyrec")) && File.Exists(Path.Combine(cd, "f4.levyrec")) && File.Exists(Path.Combine(cd, "f5.levyrec")) && tot == 2000, "cleanup deletes the oldest recordings first until under the cap");
				Check(File.Exists(Path.Combine(cd, "notes.txt")), "cleanup leaves files that are not recordings alone");
				for (int i = 1; i <= 3; i++) { string f = Path.Combine(cd, "g" + i + FightRecFolder.Ext); File.WriteAllBytes(f, new byte[1000]); File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddDays(-30 + i)); }
				gone = FightRecFolder.Enforce(cd, 1500, Path.Combine(cd, "g1.levyrec"), out tot);
				Check(File.Exists(Path.Combine(cd, "g1.levyrec")) && gone >= 4, "the recording just saved is never deleted, even when it is the oldest");
				File.WriteAllBytes(Path.Combine(cd, "huge.levyrec"), new byte[4000]);
				gone = FightRecFolder.Enforce(cd, 500, Path.Combine(cd, "huge.levyrec"), out tot);
				Check(File.Exists(Path.Combine(cd, "huge.levyrec")) && tot == 4000, "a single recording over the folder cap is kept alone");

				// ---- folder: names and crash recovery
				string nm = FightRecFolder.FileName(tmp, new DateTime(2026, 11, 14, 20, 5, 9), "Eye of Cthulhu", "won");
				Check(Path.GetFileName(nm) == "2026-11-14_20-05_Eye_of_Cthulhu_won.levyrec", "file name is YYYY-MM-DD_HH-MM_Boss_outcome.levyrec (" + Path.GetFileName(nm) + ")");
				File.WriteAllBytes(nm, new byte[1]);
				string nm2 = FightRecFolder.FileName(tmp, new DateTime(2026, 11, 14, 20, 5, 59), "Eye of Cthulhu", "won");
				Check(Path.GetFileName(nm2) == "2026-11-14_20-05_Eye_of_Cthulhu_won_2.levyrec", "a second recording in the same minute gets _2");
				Check(Path.GetFileName(FightRecFolder.FileName(tmp, new DateTime(2026, 1, 2, 3, 4, 5), "Skeletron Prime / ??", "x/y")) == "2026-01-02_03-04_Skeletron_Prime_x_y.levyrec", "odd characters in a boss name become single underscores");
				Check(FightRecFolder.SafeName("", "Fight") == "Fight" && FightRecFolder.SafeName(new string('a', 90), "Fight").Length == 40, "empty names fall back, long names are cut");
				string part = Path.Combine(tmp, "x.levyrec.part"); File.WriteAllBytes(part, new byte[10]); File.SetLastWriteTimeUtc(part, DateTime.UtcNow.AddHours(-1));
				string fresh = Path.Combine(tmp, "y.levyrec.part"); File.WriteAllBytes(fresh, new byte[10]);
				int rec = FightRecFolder.RecoverParts(tmp);
				Check(rec == 1 && File.Exists(Path.Combine(tmp, "x_crashed.levyrec")) && File.Exists(fresh), "an old .part file from a crash gets a real name; one still being written is left alone");

				// ---- config and names
				var t = typeof(FightRecConfig);
				T Def<T>(string f) => (T)((System.ComponentModel.DefaultValueAttribute)Attribute.GetCustomAttribute(t.GetField(f), typeof(System.ComponentModel.DefaultValueAttribute))).Value;
				Check(Def<FightRecMode>("RecordFightsAutomatically") == FightRecMode.BossFights && Def<string>("RecordingFolder") == "" && Def<int>("MaxFolderSizeMB") == 2048 && Def<int>("MaxFileSizeMB") == 200, "config defaults: boss fights, default folder, 200 MB per file, 2048 MB folder");
				Check(new FightRecConfig().Mode == ConfigScope_Client(), "the recorder config is client side");
				Check(FightRecorder.ClassName(DamageClass.Melee) == "melee" && FightRecorder.ClassName(DamageClass.MeleeNoSpeed) == "melee" && FightRecorder.ClassName(DamageClass.Magic) == "magic" && FightRecorder.ClassName(DamageClass.Ranged) == "ranged"
					&& FightRecorder.ClassName(DamageClass.Summon) == "summon" && FightRecorder.ClassName(DamageClass.SummonMeleeSpeed) == "summon" && FightRecorder.ClassName(DamageClass.Default) == "other" && FightRecorder.ClassName(null) == "other", "damage classes get their usual words");
				string key = "Mods.Levy.Configs.FightRecConfig.RecordFightsAutomatically.Label";
				Check(Language.Exists(key) && Language.Exists("Mods.Levy.Configs.FightRecMode.BossFights.Label"), "config labels exist in the language files");
			}
			catch (Exception e) { fails.Add("threw " + e.GetType().Name + ": " + e.Message + " " + e.StackTrace?.Split('\n')[0]); }
			finally { if (tmp != null) Rm(tmp); }
			if (fails.Count == 0) Mod.Logger.Info($"[FightRecAuto] self-check pass={pass} fail=0");
			else { Mod.Logger.Warn($"[FightRecAuto] self-check pass={pass} fail={fails.Count}"); foreach (var f in fails) Mod.Logger.Warn("  fightrecauto: " + f); }
		}

		private static Terraria.ModLoader.Config.ConfigScope ConfigScope_Client() => Terraria.ModLoader.Config.ConfigScope.ClientSide;

		// ============================================================================================ world
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_FIGHTRECAUTO") == "0") return;
			global::Levy.Common.Util.LoadSafe.Run(RunWorld);
		}

		private int _pass, _fail;
		private void Chk(bool ok, string name) { if (ok) _pass++; else { _fail++; Mod.Logger.Warn("  fightrecauto world: FAIL " + name); } }
		internal const int Floor = 120;
		private static System.Reflection.FieldInfo _clock;

		private void RunWorld()
		{
			string keep = Environment.GetEnvironmentVariable("LEVY_FIGHTRECAUTO_OUT"); // lab: keep the recordings in this folder instead of deleting them
			string tmp = string.IsNullOrEmpty(keep) ? NewTemp() : Directory.CreateDirectory(keep).FullName, good = Path.Combine(tmp, "good"), def = Path.Combine(tmp, "default");
			Player p = Main.player[0]; bool wasActive = p.active; string wasName = p.name;
			var recs = new List<SoldierRecord>(); var spawned = new List<int>(); var oldInv0 = p.inventory[0];
			int oldFlush = RecWriter.FlushMs;
			try
			{
				_pass = _fail = 0;
				int cx = Main.maxTilesX / 2;
				// a floor to stand on
				for (int x = cx - 60; x <= cx + 60; x++)
				{
					for (int y = Floor - 40; y < Floor; y++) { Tile a = Main.tile[x, y]; a.HasTile = false; a.LiquidAmount = 0; }
					Tile top = Main.tile[x, Floor]; top.HasTile = true; top.TileType = TileID.GrayBrick; top.BlockType = BlockType.Solid;
				}
				p.active = true; p.dead = false; p.name = "FightRecAutoTester"; p.statLife = p.statLifeMax = p.statLifeMax2 = 500; p.statMana = 200;
				p.position = new Vector2(cx * 16f, Floor * 16f - p.height); p.velocity = Vector2.Zero;
				p.inventory[0] = new Item(ItemID.Zenith); p.armor[0] = new Item(ItemID.SolarFlareHelmet); p.armor[3] = new Item(ItemID.CloudinaBottle);
				for (int i = 0; i < 2; i++)
				{
					var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. RecAuto" + i,
						CommanderName = p.name, CommanderIndex = 0, LifeCrystals = 20, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
					rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
					rec.Gear.Weapon = new Item(i == 0 ? ItemID.Zenith : ItemID.SpaceGun);
					RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
					Vector2 at = new Vector2((cx - 6 + i * 3) * 16f, Floor * 16f);
					int idx = RosterSystem.Instance.SpawnBody(rec, at);
					if (idx >= 0) { Main.npc[idx].Bottom = at; Main.npc[idx].velocity = Vector2.Zero; spawned.Add(idx); }
				}
				Chk(spawned.Count == 2, "two levy bodies stand next to the commander");

				FightRecAuto.TestClient = true; FightRecAuto.TestFolder = good; FightRecAuto.TestDefaultDir = def; FightRecAuto.ResetMessages();
				EventRecorder.TestEnabled = false; // the boss checks run with no event recording (a world that loads during a Blood Moon must not change them); EventRecCheck turns it on
				FightRecorder.MaxFolderBytes = 0; FightRecorder.MaxFileBytes = 0; FightRecorder.ForceSync = true; RecWriter.FlushMs = 20;

				// ---------- 1. Boss fights: a skirmish does not start it, the boss does, a killed boss is "won"
				FightRecAuto.TestMode = FightRecMode.BossFights;
				var adds = SpawnAdds(cx, 4); Step(60, p, null);
				Chk(!FightRecorder.On, "Boss fights does not record a skirmish with no boss");
				KillAdds(adds);
				int boss = SpawnBoss(cx);
				Step(40, p, boss);
				Chk(FightRecorder.On && FightRecorder.Auto == 1, "Boss fights starts when a boss appears");
				ScriptHits(p, boss, 600);
				int endAt = (int)Main.GameUpdateCount;
				FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false;
				int ticksAfter = StepUntilStopped(p, -1, 1500);
				Chk(!FightRecorder.On && ticksAfter >= 250 && ticksAfter <= 420, "the recording stops about 5 seconds after the boss is gone (" + ticksAfter + " ticks)");
				string f1 = FightRecorder.LastResult;
				Chk(f1 != null && File.Exists(f1) && f1.StartsWith(good), "the file is saved in the chosen folder");
				Chk(f1 != null && Regex.IsMatch(Path.GetFileName(f1), @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}_Eye_of_Cthulhu_won\.levyrec$"), "the file is named date_time_Boss_outcome.levyrec (" + Path.GetFileName(f1 ?? "none") + ")");
				VerifyFile(f1, "boss", 1, "Eye of Cthulhu", "won");
				int boss2 = SpawnBoss(cx); Step(30, p, boss2);
				Chk(!FightRecorder.On, "no new recording starts in the first second after one ends");
				Step(70, p, boss2);
				Chk(FightRecorder.On, "a boss fight that follows within seconds (Duke Fishron coming back while the pillars stand) gets its own recording");
				if (FightRecorder.On) FightRecorder.Stop("self-check next");
				Main.npc[boss2].active = false; Step(3, p, null);

				// ---------- 1b. several main bosses are one fight with all of their names; spite is its own target kind (format 9)
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				TwinsAndSpite(p, cx, spawned);

				// ---------- 1c. a boss is killed and other bosses leave in the same moment (Moon Lord's hands and head go with the core, the Lunar Pillars his cousin's death spawns are purged a tick later): still "won"
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				int kA = SpawnBoss(cx), kB = SpawnBoss(cx, NPCID.KingSlime);
				Step(40, p, kA);
				Chk(FightRecorder.On, "Boss fights starts on two bosses at once");
				ScriptHits(p, kA, 200);
				FightRecorder.NoteBossKill(Main.npc[kA]); Main.npc[kA].life = 0; Main.npc[kA].active = false;   // the one that is killed
				Main.npc[kB].active = false;                                                                   // the one that vanishes with it (no OnKill, life untouched)
				int kP = SpawnBoss(cx, NPCID.LunarTowerSolar); One(p, -1, 0); Main.npc[kP].active = false;     // a boss-class NPC the kill spawned, purged a tick later
				StepUntilStopped(p, -1, 1500);
				string f1c = FightRecorder.LastResult;
				Chk(f1c != null && Regex.IsMatch(Path.GetFileName(f1c), @"_won\.levyrec$"), "a kill that other bosses vanish with is a won fight, not a despawn (" + Path.GetFileName(f1c ?? "none") + ")");
				if (f1c != null) { var l1c = ReadLines(f1c, true, out _); Chk(l1c.Exists(l => l.Contains("\"e\":\"bdeath\"") && l.Contains("\"final\":1")) && !l1c.Exists(l => l.Contains("\"e\":\"bdespawn\"") && l.Contains("\"final\":1")), "the final end event is a bdeath"); }
				for (int q = 0; q < Main.maxNPCs; q++) if (Main.npc[q].active && !(Main.npc[q].ModNPC is SoldierNPC) && !Main.npc[q].friendly && !Main.npc[q].townNPC) Main.npc[q].active = false;
				Step(3, p, null);

				// ---------- 1d. the Lunatic Cultist's win is not swallowed by the pillars: they are an event, not a boss. The kill ends the fight and the file is "won" although the pillars still stand
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				int cB = SpawnBoss(cx, NPCID.CultistBoss);
				Step(40, p, cB);
				Chk(FightRecorder.On && FightRecorder.Auto == 1, "Boss fights starts on the Lunatic Cultist");
				ScriptHits(p, cB, 200);
				FightRecorder.NoteBossKill(Main.npc[cB]); Main.npc[cB].life = 0; Main.npc[cB].active = false;
				int[] pil = { SpawnBoss(cx, NPCID.LunarTowerSolar), SpawnBoss(cx, NPCID.LunarTowerVortex), SpawnBoss(cx, NPCID.LunarTowerNebula), SpawnBoss(cx, NPCID.LunarTowerStardust) };
				int pillarTicks = StepUntilStopped(p, -1, 2400);
				string f1d = FightRecorder.LastResult;
				Chk(!FightRecorder.On && pillarTicks <= 420, "the recording ends about 5 seconds after the Cultist falls while four pillars stand (" + pillarTicks + " ticks)");
				Chk(f1d != null && Regex.IsMatch(Path.GetFileName(f1d), @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}_Lunatic_Cultist_won\.levyrec$"), "the Cultist's fight is named for her alone and won (" + Path.GetFileName(f1d ?? "none") + ")");
				if (f1d != null) { var l1d = ReadLines(f1d, true, out _); Chk(!l1d.Exists(l => l.Contains("Solar Pillar") && l.Contains("\"e\":\"boss\"")) && l1d.Exists(l => l.Contains("\"e\":\"bdeath\"") && l.Contains("\"final\":1")), "no pillar is a boss of the fight and the Cultist's death is its final end"); }
				foreach (int q in pil) if (q >= 0) Main.npc[q].active = false;
				Step(3, p, null);
				// a pillar that stands alone is no boss: it neither starts a boss-fight recording nor keeps one alive
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				int pillar = SpawnBoss(cx, NPCID.LunarTowerNebula); Step(120, p, null);
				Chk(!FightRecorder.On, "Boss fights does not record a Celestial Pillar standing alone");
				Main.npc[pillar].active = false; Step(3, p, null);

				// ---------- 1e. the commander falls and the boss leaves: lost. The commander alive and the boss leaves: fled
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				int dB = SpawnBoss(cx);
				Step(40, p, dB); ScriptHits(p, dB, 200);
				p.dead = true; Step(4, p, dB);
				Main.npc[dB].active = false; Step(3, p, null); p.dead = false;
				StepUntilStopped(p, -1, 1500);
				string f1e = FightRecorder.LastResult;
				Chk(f1e != null && Regex.IsMatch(Path.GetFileName(f1e), @"_Eye_of_Cthulhu_lost\.levyrec$"), "the commander fell and the boss left: lost (" + Path.GetFileName(f1e ?? "none") + ")");
				if (f1e != null) { var l1e = ReadLines(f1e, true, out _); Chk(l1e.Exists(l => l.Contains("\"e\":\"cdeath\"")) && l1e.Exists(l => l.Contains("\"e\":\"bdespawn\"") && l.Contains("\"final\":1")) && l1e.Exists(l => l.Contains("\"k\":\"end\"") && l.Contains("\"result\":\"lost\"") && l.Contains("\"outcome\":\"despawn\"")), "its end says despawn, result lost, and a cdeath is on record"); }
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				int eB = SpawnBoss(cx);
				Step(40, p, eB); ScriptHits(p, eB, 200);
				Main.npc[eB].active = false;
				StepUntilStopped(p, -1, 1500);
				string f1f = FightRecorder.LastResult;
				Chk(f1f != null && Regex.IsMatch(Path.GetFileName(f1f), @"_Eye_of_Cthulhu_fled\.levyrec$"), "the commander alive and the boss leaves: fled (" + Path.GetFileName(f1f ?? "none") + ")");

				// ---------- 1f2. THE KILL RULE (recwin): a boss that goes inactive without its death path is a boss that LEFT, whatever life it had. The false win this guards: the commander fell, her in-flight shots took the boss to 0.8%, the game removed it with no player alive
				// (Calamity DoG, 14542 of 1836000) and the file said won. A kill is the death hook (OnKill) of the boss or of its realLife pool, or life 0.
				// (a) the commander falls, the boss is cut down to 0.8% by a late hit, then removed with no kill: lost, bdespawn, kh 0
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				int rB = SpawnBoss(cx);
				Step(40, p, rB); ScriptHits(p, rB, 200);
				p.dead = true; Step(4, p, rB);
				FightRecorder.NoteBossHit(Main.npc[rB], 2792); Main.npc[rB].life = 22; Step(2, p, rB);
				Main.npc[rB].active = false; Step(3, p, null); p.dead = false;
				StepUntilStopped(p, -1, 1500);
				string f1h = FightRecorder.LastResult;
				Chk(f1h != null && Regex.IsMatch(Path.GetFileName(f1h), @"_Eye_of_Cthulhu_lost(_\d+)?\.levyrec$"), "the commander fell, a last hit left the boss at 0.8%, then it was removed with no death: lost, never won (" + Path.GetFileName(f1h ?? "none") + ")");
				if (f1h != null) { var l1h = ReadLines(f1h, true, out _); Chk(l1h.Exists(l => l.Contains("\"e\":\"bdespawn\"") && l.Contains("\"final\":1") && l.Contains("\"kh\":0")) && !l1h.Exists(l => l.Contains("\"e\":\"bdeath\"")) && l1h.Exists(l => l.Contains("\"k\":\"end\"") && l.Contains("\"outcome\":\"despawn\"") && l.Contains("\"result\":\"lost\"")), "the end record reads boss left (bdespawn, outcome despawn, result lost)"); }
				// (b) the same boss at 0.8% that really dies (the death hook runs): won
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				int rC = SpawnBoss(cx);
				Step(40, p, rC); ScriptHits(p, rC, 200);
				FightRecorder.NoteBossHit(Main.npc[rC], 2792); Main.npc[rC].life = 22; Step(2, p, rC);
				FightRecorder.NoteBossKill(Main.npc[rC]); Main.npc[rC].active = false;
				StepUntilStopped(p, -1, 1500);
				string f1i = FightRecorder.LastResult;
				Chk(f1i != null && Regex.IsMatch(Path.GetFileName(f1i), @"_Eye_of_Cthulhu_won(_\d+)?\.levyrec$"), "the same boss whose death hook ran is won (" + Path.GetFileName(f1i ?? "none") + ")");
				if (f1i != null) { var l1i = ReadLines(f1i, true, out _); Chk(l1i.Exists(l => l.Contains("\"e\":\"bdeath\"") && l.Contains("\"final\":1") && l.Contains("\"kh\":1")), "the bdeath says the death hook ran (kh 1)"); }
				// (c) a worm (realLife pool): the head stays at life 1 (a death animation), the loot runs on a BODY segment, the pool leaves: won; with no loot at all the same pool leaving is boss left
				foreach (bool dies in new[] { true, false })
				{
					FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
					int wH = SpawnBoss(cx, NPCID.TheDestroyer);
					int wB = SpawnBoss(cx, NPCID.TheDestroyerBody);
					if (wH >= 0 && wB >= 0) { Main.npc[wH].realLife = wH; Main.npc[wB].realLife = wH; }
					Step(40, p, wH); ScriptHits(p, wH, 200);
					p.dead = !dies; Step(4, p, wH);
					FightRecorder.NoteBossHit(Main.npc[wH], 10); Main.npc[wH].life = 1; Main.npc[wB].life = 1; Step(2, p, wH);
					if (dies) FightRecorder.NoteBossKill(Main.npc[wB]);
					Main.npc[wH].active = false; Main.npc[wB].active = false; Step(3, p, null); p.dead = false;
					StepUntilStopped(p, -1, 1500);
					string fw = FightRecorder.LastResult;
					Chk(fw != null && Regex.IsMatch(Path.GetFileName(fw), dies ? @"_won(_\d+)?\.levyrec$" : @"_lost(_\d+)?\.levyrec$"), "a worm at life 1 whose loot " + (dies ? "ran on a body segment is won" : "never ran and left with the commander down is lost, not won") + " (" + Path.GetFileName(fw ?? "none") + ")");
				}
				for (int q = 0; q < Main.maxNPCs; q++) if (Main.npc[q].active && !(Main.npc[q].ModNPC is SoldierNPC) && !Main.npc[q].friendly && !Main.npc[q].townNPC) Main.npc[q].active = false;
				Step(3, p, null);

				// ---------- 1g. format 11: the commander's own projectiles are in the frames, the army's shots (same owner slot) are not
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				int gB = SpawnBoss(cx);
				Step(40, p, gB);
				int mine = PlaceShot(p, new Vector2(60, -20), new Vector2(8, 0), false);
				int theirs = PlaceShot(p, new Vector2(-60, -20), new Vector2(-8, 0), true);
				Step(30, p, gB);
				if (mine >= 0) Main.projectile[mine].active = false; if (theirs >= 0) Main.projectile[theirs].active = false;
				ScriptHits(p, gB, 60);
				FightRecorder.NoteBossKill(Main.npc[gB]); Main.npc[gB].life = 0; Main.npc[gB].active = false;
				StepUntilStopped(p, -1, 1500);
				string f1g = FightRecorder.LastResult;
				if (f1g != null)
				{
					int withC = 0, maxRows = 0, wrongType = 0, noC = 0; string hdrFmt = null;
					foreach (string l in ReadLines(f1g, true, out _))
					{
						try
						{
							using var d = JsonDocument.Parse(l); var r = d.RootElement; string k = r.GetProperty("k").GetString();
							if (k == "hdr") hdrFmt = r.GetProperty("format").GetInt32().ToString();
							if (k != "f") continue;
							if (r.TryGetProperty("c", out var c)) { withC++; maxRows = Math.Max(maxRows, c.GetArrayLength()); foreach (var row in c.EnumerateArray()) { if (row.GetArrayLength() != 7 || row[0].GetInt32() != ProjectileID.MagicMissile) wrongType++; } }
							else noC++;
						}
						catch { }
					}
					Chk(hdrFmt == FightRecFormat.Version.ToString() && FightRecFormat.Version >= 11, "the header says format " + FightRecFormat.Version);
					Chk(mine >= 0 && theirs >= 0 && withC >= 5 && maxRows == 1 && wrongType == 0, "frames carry the commander's own shot and not the soldier-tagged one (frames with c " + withC + ", most rows " + maxRows + ", odd rows " + wrongType + ")");
					Chk(noC > 0, "a frame with no commander projectile has no c key (" + noC + " such frames)");
				}
				else Chk(false, "commander projectile recording was saved");

				// ---------- 2. All fights: the skirmish starts it, it ends 5 s after the adds are gone, commander death makes it "lost"
				FightRecorder.MaxFolderBytes = 0;
				FightRecAuto.TestMode = FightRecMode.AllFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				adds = SpawnAdds(cx, 4); Step(40, p, null);
				Chk(FightRecorder.On && FightRecorder.Auto == 2 && !FightRecorder.BossSeen, "All fights starts on a skirmish next to the army");
				Step(120, p, null); p.dead = true; Step(4, p, null); p.dead = false; Step(60, p, null);
				KillAdds(adds);
				int quietTicks = StepUntilStopped(p, -1, 1500);
				Chk(!FightRecorder.On && quietTicks >= 250 && quietTicks <= 420, "an all-fights recording stops about 5 seconds after the last hostile is gone (" + quietTicks + " ticks)");
				string f2 = FightRecorder.LastResult;
				Chk(f2 != null && Regex.IsMatch(Path.GetFileName(f2), @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}_Fight_ended\.levyrec$"), "a fight with no boss is named Fight_ended (" + Path.GetFileName(f2 ?? "none") + ")");
				VerifyFile(f2, "army", 2, null, "ended");

				// ---------- 3. Off records nothing
				FightRecAuto.TestMode = FightRecMode.Off; FightRecAuto.ClearCooldown(); Step(3, p, null);
				boss = SpawnBoss(cx); Step(60, p, boss);
				Chk(!FightRecorder.On, "Off records nothing, boss or not");
				Main.npc[boss].active = false; Step(5, p, null);

				// ---------- 4. size cap
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				int before = Directory.GetFiles(good, "*.levyrec").Length;
				FightRecorder.MaxFileBytes = 6000;
				boss = SpawnBoss(cx); Step(40, p, boss);
				bool started = FightRecorder.On;
				int guard = 0; while (FightRecorder.On && guard++ < 3000) { ScriptHits(p, boss, 1, sleep: 3); }
				Chk(started && !FightRecorder.On && guard < 3000, "a recording that reaches the size cap stops itself (" + guard + " ticks)");
				string f4 = FightRecorder.LastResult;
				var l4 = f4 != null ? ReadLines(f4, true, out string e4) : new List<string>();
				Chk(l4.Count > 0 && l4[l4.Count - 1].Contains("\"k\":\"end\"") && l4[l4.Count - 1].Contains("\"capped\":1") && l4[l4.Count - 1].Contains("size limit"), "a capped recording still ends with its end record, marked capped");
				Chk(f4 != null && new FileInfo(f4).Length < 6000 + 30000, "the capped file stays near the cap (" + (f4 != null ? new FileInfo(f4).Length : -1) + " bytes for 6000)");
				FightRecorder.MaxFileBytes = 0; Main.npc[boss].active = false; Step(5, p, null);

				// ---------- 5. folder cap removes the oldest
				int n0 = Directory.GetFiles(good, "*.levyrec").Length;
				foreach (string old in Directory.GetFiles(good, "*.levyrec")) File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-2));
				string oldest = Path.Combine(good, "2020-01-01_00-00_Old_Fight_ended.levyrec"); File.WriteAllBytes(oldest, new byte[50000]); File.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddDays(-400));
				FightRecAuto.TestMode = FightRecMode.BossFights; FightRecAuto.ClearCooldown(); Step(3, p, null);
				FightRecorder.MaxFolderBytes = 20000;
				boss = SpawnBoss(cx); Step(40, p, boss); ScriptHits(p, boss, 300);
				FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false;
				StepUntilStopped(p, -1, 1500);
				string f5 = FightRecorder.LastResult;
				Chk(!File.Exists(oldest) && f5 != null && File.Exists(f5), "the folder cap deletes the oldest recording and keeps the new one");
				Chk(Directory.GetFiles(good, "*.levyrec").Length < n0 + 2, "older recordings are removed down to the cap");
				FightRecorder.MaxFolderBytes = 0;

				// ---------- 6. unwritable chosen folder falls back, with a chat warning
				string blocker = Path.Combine(tmp, "blocker"); File.WriteAllText(blocker, "x");
				FightRecAuto.TestFolder = Path.Combine(blocker, "sub"); FightRecAuto.ResetMessages();
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				boss = SpawnBoss(cx); Step(40, p, boss); ScriptHits(p, boss, 200);
				bool onFallback = FightRecorder.On; FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false;
				StepUntilStopped(p, -1, 1500);
				string f6 = FightRecorder.LastResult; FightRecAuto.Pump();
				Chk(onFallback && f6 != null && f6.StartsWith(def) && File.Exists(f6), "an unwritable folder falls back to the default folder and still records");
				Chk(FightRecAuto.Said.Exists(m => m.Contains("Can't write") && m.Contains(def)), "the player is warned in chat about the fallback");
				// nothing writable at all: no recording, no exception
				FightRecAuto.TestFolder = Path.Combine(blocker, "sub"); FightRecAuto.TestDefaultDir = Path.Combine(blocker, "also"); FightRecAuto.ResetMessages();
				FightRecAuto.ClearCooldown(); Step(3, p, null);
				boss = SpawnBoss(cx); Step(60, p, boss); FightRecAuto.Pump();
				Chk(!FightRecorder.On && FightRecAuto.Said.Exists(m => m.Contains("could not start") || m.Contains("recording is off")), "with no writable folder at all it does not record and says why");
				Main.npc[boss].active = false;
				RunProf(p, cx, good, def, recs, spawned);
				if (Environment.GetEnvironmentVariable("LEVY_EVENTREC") != "0") EventRecCheck.Run(Mod, p, cx, good, recs, spawned);
				if (Environment.GetEnvironmentVariable("LEVY_FIGHTRECAUTO_SIZEPROBE") == "1") SizeProbe(p, cx, good, recs, spawned);
			}
			catch (Exception e) { _fail++; Mod.Logger.Warn("  fightrecauto world: threw " + e); }
			finally
			{
				try { if (FightRecorder.On) FightRecorder.Stop("self-check over"); } catch { }
				try { if (EventRecorder.On) EventRecorder.Stop("self-check over"); } catch { }
				EventRecorder.TestEnabled = null; EventRecorder.TestMaxTicks = 0; EventRecorder.TestArmyGoneTicks = 0;
				FightRecAuto.TestClient = false; FightRecAuto.TestMode = null; FightRecAuto.TestFolder = FightRecAuto.TestDefaultDir = null; FightRecAuto.ResetMessages();
				FightRecorder.MaxFolderBytes = 0; FightRecorder.MaxFileBytes = 0; FightRecorder.ForceSync = false; RecWriter.FlushMs = oldFlush;
				foreach (int i in spawned) { try { Main.npc[i].active = false; } catch { } }
				foreach (var r in recs) RosterSystem.Instance.Regulars.Remove(r);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].type != 0 && !Main.npc[i].townNPC && Main.npc[i].ai[3] == 4242f) Main.npc[i].active = false;
				p.active = wasActive; p.name = wasName; p.inventory[0] = oldInv0; p.armor[0] = new Item(); p.armor[3] = new Item();
				if (string.IsNullOrEmpty(keep)) Rm(tmp);
			}
			if (_fail == 0) Mod.Logger.Info($"[FightRecAuto] world self-check pass={_pass} fail=0");
			else Mod.Logger.Warn($"[FightRecAuto] world self-check pass={_pass} fail={_fail}");
		}

		// ============================================================================================ profiler (ClientProf) around automatic recordings
		private int _ppass, _pfail;
		private void PChk(bool ok, string name) { if (ok) _ppass++; else { _pfail++; Mod.Logger.Warn("  clientprof world: FAIL " + name); } }

		/// <summary>One scripted boss fight with the auto recorder, the clock stepped by hand. <paramref name="slowFrom"/>: from this tick on every tick sleeps (a lag stretch, so one profiler window reads lower fps than the rest).</summary>
		private static long _profClock;
		private string ProfFight(Player p, int cx, int ticks, int slowFrom, out int chatDuring, Action midFight = null)
		{
			FightRecAuto.ClearCooldown(); Step(3, p, null);
			int boss = SpawnBoss(cx); Step(40, p, boss);
			int said0 = global::Levy.Common.Util.ClientProf.Said.Count;
			for (int k = 0; k < ticks; k++)
			{
				if (k == ticks / 2) midFight?.Invoke();
				One(p, boss, k);
				if (FightRecorder.On && Main.npc[boss].active && k % 7 == 0) FightRecorder.NoteDamage(Main.npc[boss], p, null, new Item(ItemID.Zenith), 220);
				_profClock += (long)((k >= slowFrom ? 14.0 : (k % 3 == 0 ? 1.0 : 0.3)) / 1000.0 * System.Diagnostics.Stopwatch.Frequency); // the profiler's windows read this clock, so a lag stretch is a stretch of ticks, not of wall-clock seconds
			}
			chatDuring = global::Levy.Common.Util.ClientProf.Said.Count - said0;
			FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false;
			StepUntilStopped(p, -1, 1500);
			return FightRecorder.LastResult;
		}

		private static List<JsonElement> ProfEvents(string path, out JsonElement end)
		{
			var l = new List<JsonElement>(); end = default;
			foreach (string line in ReadLines(path, true, out _))
			{
				try { using var d = JsonDocument.Parse(line); var r = d.RootElement; string k = r.GetProperty("k").GetString(); if (k == "end") end = r.Clone(); else if (k == "e" && r.GetProperty("e").GetString() == "prof") l.Add(r.Clone()); } catch { }
			}
			return l;
		}

		private void RunProf(Player p, int cx, string good, string def, List<SoldierRecord> recs, List<int> spawned)
		{
			_ppass = _pfail = 0;
			try
			{
				_profClock = 0; global::Levy.Common.Util.ClientProf.TestClock = () => _profClock; global::Levy.Common.Util.ClientProf.TestServer = true; global::Levy.Common.Util.ClientProf.TestInterval = 0.05; global::Levy.Common.Util.ClientProf.TestAutoProfile = null; global::Levy.Common.Util.ClientProf.Said.Clear();
				FightRecAuto.TestFolder = good; FightRecAuto.TestDefaultDir = def; FightRecAuto.ResetMessages(); FightRecAuto.TestMode = FightRecMode.BossFights;

				// ---- the key and the config
				PChk(global::Levy.Common.Util.ClientProfKey.Key != null, "the Toggle Levy profiler keybind is registered");
				PChk(Language.Exists("Mods.Levy.Keybinds.ToggleProfiler.DisplayName") && Language.GetTextValue("Mods.Levy.Keybinds.ToggleProfiler.DisplayName").Contains("profiler"), "the keybind has a display name");
				PChk(Language.Exists("Mods.Levy.Configs.FightRecConfig.ProfileRecordedFights.Label") && Language.Exists("Mods.Levy.Configs.FightRecConfig.ProfileRecordedFights.Tooltip"), "the config option has a label and a tooltip");
				var fi = typeof(FightRecConfig).GetField("ProfileRecordedFights");
				PChk(fi != null && (bool)((System.ComponentModel.DefaultValueAttribute)Attribute.GetCustomAttribute(fi, typeof(System.ComponentModel.DefaultValueAttribute))).Value, "Profile recorded fights defaults to on");
				PChk(FightRecConfig.Get == null || FightRecConfig.Get.ProfileRecordedFights, "the loaded config has it on");

				// ---- the switch (same one as /levyprof): on, off, the same chat lines
				PChk(!global::Levy.Common.Util.ClientProf.IsRunning && !global::Levy.Common.Util.LevyProf.On || global::Levy.Common.Util.LevyProf.OnFromStart, "the profiler starts off");
				global::Levy.Common.Util.ClientProf.Toggle();
				PChk(global::Levy.Common.Util.ClientProf.IsOn && global::Levy.Common.Util.ClientProf.Said.Count == 1 && global::Levy.Common.Util.ClientProf.Said[0].StartsWith("Levy profiler is on."), "the key switches the profiler on and says so");
				global::Levy.Common.Util.ClientProf.Toggle();
				PChk(!global::Levy.Common.Util.ClientProf.IsRunning && global::Levy.Common.Util.ClientProf.Said.Count == 2 && global::Levy.Common.Util.ClientProf.Said[1] == "Levy profiler is off.", "the key switches it off and says so");
				PChk(global::Levy.Common.Util.ClientProf.LastSayColor == Color.LightGray, "the off line is grey");
				global::Levy.Common.Util.ClientProf.Toggle();
				PChk(global::Levy.Common.Util.ClientProf.LastSayColor == Color.LightGreen, "the on line is green");
				global::Levy.Common.Util.ClientProf.Toggle();

				// ---- 1. an automatic recording turns the profiler on, and off again, with nothing in chat mid-fight
				global::Levy.Common.Util.ClientProf.Said.Clear();
				bool onDuring = false, autoDuring = false, manualDuring = true, hookDuring = true;
				string f1 = ProfFight(p, cx, 900, 600, out int chat1, () => { onDuring = global::Levy.Common.Util.ClientProf.IsRunning; autoDuring = global::Levy.Common.Util.ClientProf.AutoRunning; manualDuring = global::Levy.Common.Util.ClientProf.IsOn; hookDuring = global::Levy.Common.Util.ClientProf.DamageHookOn; });
				PChk(f1 != null && File.Exists(f1), "the profiled recording was saved");
				PChk(!hookDuring, "the in-fight profiling leaves the per-projectile hit-check hook off");
				PChk(onDuring && autoDuring && !manualDuring, "profiling is on during an automatic recording, as the fight's own and not as the player's");
				PChk(chat1 == 0, "nothing was printed to chat during the fight (" + chat1 + " lines)");
				PChk(!global::Levy.Common.Util.ClientProf.IsRunning && !global::Levy.Common.Util.ClientProf.AutoRunning && (global::Levy.Common.Util.LevyProf.OnFromStart || !global::Levy.Common.Util.LevyProf.On), "profiling is off again when the recording ends");
				PChk(global::Levy.Common.Util.ClientProf.Said.Count == 1 && global::Levy.Common.Util.ClientProf.Said[0].StartsWith("Fight profile: ") && global::Levy.Common.Util.ClientProf.Said[0].Contains(" fps on average, worst ") && global::Levy.Common.Util.ClientProf.Said[0].Contains("Levy"), "one summary line in chat after the fight (" + (global::Levy.Common.Util.ClientProf.Said.Count > 0 ? global::Levy.Common.Util.ClientProf.Said[0] : "none") + ")");
				JsonElement e1 = default; var pe = f1 != null ? ProfEvents(f1, out e1) : new List<JsonElement>();
				PChk(pe.Count >= 5, "the recording has prof events through the fight (" + pe.Count + ")");
				bool fields = pe.Count > 0, ordered = true; double lo = double.MaxValue, hi = 0; double lastT = -1;
				foreach (var x in pe)
				{
					fields &= x.TryGetProperty("fps", out _) && x.TryGetProperty("ups", out _) && x.TryGetProperty("upd", out _) && x.TryGetProperty("draw", out _) && x.TryGetProperty("ai", out _) && x.TryGetProperty("top", out _) && x.TryGetProperty("win", out var wn) && wn.GetDouble() > 0;
					double t = x.GetProperty("t").GetDouble(); ordered &= t >= lastT; lastT = t;
					if (x.TryGetProperty("fps", out var fp)) { lo = Math.Min(lo, fp.GetDouble()); hi = Math.Max(hi, fp.GetDouble()); }
				}
				PChk(fields && ordered, "every prof event carries fps, ups, upd, draw, ai, top and its window length, in time order");
				PChk(pe.Count > 0 && hi > lo * 1.5, "the lag stretch shows as a lower-fps window in the file (" + lo.ToString("0") + " to " + hi.ToString("0") + " fps)");
				PChk(e1.ValueKind == JsonValueKind.Object && e1.TryGetProperty("profiled", out var pf) && pf.GetInt32() == 1, "the end record says the recording was profiled");

				// ---- 2. the player's own profiler is on when the fight starts: it stays on, full, chatting, and survives the fight's end
				global::Levy.Common.Util.ClientProf.Said.Clear();
				global::Levy.Common.Util.ClientProf.Toggle(); int said2 = global::Levy.Common.Util.ClientProf.Said.Count;
				string f2 = ProfFight(p, cx, 400, 1000000, out int chat2);
				PChk(global::Levy.Common.Util.ClientProf.IsOn && global::Levy.Common.Util.ClientProf.IsRunning && global::Levy.Common.Util.ClientProf.DamageHookOn, "the player's profiler is still on, hit-check hook included, after an automatic recording ends");
				PChk(chat2 >= 2, "with the player's own profiler on, its summaries still print (" + chat2 + " lines)");
				PChk(f2 != null && ProfEvents(f2, out _).Count >= 2, "the recording still gets prof events");
				global::Levy.Common.Util.ClientProf.Toggle();
				PChk(!global::Levy.Common.Util.ClientProf.IsRunning && global::Levy.Common.Util.ClientProf.Said[global::Levy.Common.Util.ClientProf.Said.Count - 1] == "Levy profiler is off.", "and the key still switches it off");

				// ---- 3. the player switches theirs on and off in the middle of a fight: the fight's own profiling carries on
				global::Levy.Common.Util.ClientProf.Said.Clear(); bool afterOff = false, midOn = false;
				string f3 = ProfFight(p, cx, 500, 1000000, out int chat3, () => { global::Levy.Common.Util.ClientProf.Toggle(); midOn = global::Levy.Common.Util.ClientProf.IsOn; global::Levy.Common.Util.ClientProf.Toggle(); afterOff = global::Levy.Common.Util.ClientProf.AutoRunning && !global::Levy.Common.Util.ClientProf.IsOn; });
				PChk(midOn && afterOff, "switching the profiler on and off mid-fight leaves the fight's own profiling running");
				PChk(f3 != null && ProfEvents(f3, out _).Count >= 3 && !global::Levy.Common.Util.ClientProf.IsRunning, "and it still ends cleanly with prof events");

				// ---- 4. the config option off: no profiling, no prof events, no line
				global::Levy.Common.Util.ClientProf.Said.Clear(); global::Levy.Common.Util.ClientProf.TestAutoProfile = false; bool onWhileOff = true;
				string f4 = ProfFight(p, cx, 300, 1000000, out int chat4, () => { onWhileOff = global::Levy.Common.Util.ClientProf.IsRunning; });
				PChk(!onWhileOff && chat4 == 0 && global::Levy.Common.Util.ClientProf.Said.Count == 0, "with Profile recorded fights off nothing is profiled and nothing is said");
				PChk(f4 != null && File.Exists(f4) && ProfEvents(f4, out JsonElement e4).Count == 0 && e4.GetProperty("profiled").GetInt32() == 0, "the recording is made without prof events and says it was not profiled");
				global::Levy.Common.Util.ClientProf.TestAutoProfile = null;

				// ---- 5. a manual recording (/levyrec start) is not profiled by this option
				FightRecAuto.ClearCooldown(); Step(3, p, null); FightRecorder.AutoKind = 0;
				string mp = FightRecorder.Start(p, "Probe", out string merr);
				PChk(mp != null && !global::Levy.Common.Util.ClientProf.IsRunning, "a recording the player starts by hand does not switch the profiler on");
				if (FightRecorder.On) FightRecorder.Stop("stopped by command");
			}
			catch (Exception e) { _pfail++; Mod.Logger.Warn("  clientprof world: threw " + e); }
			finally
			{
				try { if (FightRecorder.On) FightRecorder.Stop("self-check over"); } catch { }
				global::Levy.Common.Util.ClientProf.Disable(); global::Levy.Common.Util.ClientProf.TestAutoProfile = null;
			}
			if (Environment.GetEnvironmentVariable("LEVY_PROF_OVERHEAD") == "1") { try { ProfOverhead(p, cx, recs, spawned); } catch (Exception e) { Mod.Logger.Warn("[ClientProf] overhead probe threw " + e); } }
			global::Levy.Common.Util.ClientProf.TestServer = false; global::Levy.Common.Util.ClientProf.TestInterval = null; global::Levy.Common.Util.ClientProf.TestClock = null;
			if (_pfail == 0) Mod.Logger.Info($"[ClientProf] self-check pass={_ppass} fail=0");
			else Mod.Logger.Warn($"[ClientProf] self-check pass={_ppass} fail={_pfail}");
		}

		/// <summary>Lab only (LEVY_PROF_OVERHEAD=1, levies from LEVY_PROF_OVERHEAD_LEVIES, default 30): what the profiler costs. The levies fight 8 foes that never die; their real NPC updates run with the profiler off or on, tick by tick at random. The projectile hook and the clock are timed on their own.</summary>
		private void ProfOverhead(Player p, int cx, List<SoldierRecord> recs, List<int> spawned)
		{
			var rnd = new Random(5); int made = 0; var mine = new List<int>(); int levies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PROF_OVERHEAD_LEVIES"), out int lv) && lv > 0 ? lv : 30;
			foreach (int i in spawned) if (Main.npc[i].active) mine.Add(i);
			for (int i = 0; mine.Count < levies && i < levies * 2; i++)
			{
				var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Ovh" + i, CommanderName = p.name, CommanderIndex = 0, LifeCrystals = 20, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
				rec.Gear.Weapon = new Item(i % 3 == 0 ? ItemID.Zenith : i % 3 == 1 ? ItemID.SpaceGun : ItemID.Megashark);
				RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
				Vector2 at = new Vector2((cx - 55 + (mine.Count % 38) * 3) * 16f, Floor * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx >= 0) { Main.npc[idx].Bottom = at; Main.npc[idx].velocity = Vector2.Zero; spawned.Add(idx); mine.Add(idx); made++; }
			}
			var foes = new List<int>();
			for (int k = 0; k < 8; k++)
			{
				int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("FightRecAutoCheck"), cx * 16 + 300 + k * 60, (Floor - 3) * 16, NPCID.BlueSlime);
				if (i >= 0) { NPC b = Main.npc[i]; b.aiStyle = -1; b.noGravity = true; b.velocity = Vector2.Zero; b.damage = 20; b.life = b.lifeMax = 50_000_000; b.ai[3] = 4242f; foes.Add(i); }
			}
			_clock ??= typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			bool wasOn = global::Levy.Common.Util.LevyProf.On;
			void Tick(int mode, int k)
			{
				_clock.SetValue(null, Main.GameUpdateCount + 1u);
				global::Levy.Common.Util.LevyProf.On = mode == 1;
				foreach (int i in mine) { NPC n = Main.npc[i]; if (n.active) n.UpdateNPC(i); }
				foreach (int i in foes) { NPC n = Main.npc[i]; if (n.active) { n.life = n.lifeMax; n.Center = new Vector2(n.Center.X, Floor * 16f - 30); } }
				p.statLife = p.statLifeMax2; p.active = true;
			}
			for (int k = 0; k < 300; k++) Tick(0, k); // settle
			// every tick is timed on its own and given to the profiler-off or the profiler-on code path at random (a fixed alternation would line up with the army's own 10 and 60 tick schedules)
			var off = new List<double>(); var on = new List<double>(); var order = new Random(9);
			for (int k = 0; k < 8000; k++)
			{
				bool profOn = order.Next(2) == 1;
				long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
				Tick(profOn ? 1 : 0, k);
				double dt = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				(profOn ? on : off).Add(dt);
			}
			global::Levy.Common.Util.LevyProf.On = wasOn;
			double Mean(List<double> l) => l.Count == 0 ? 0 : System.Linq.Enumerable.Average(l);
			double Med(List<double> l) { var c = new List<double>(l); c.Sort(); return c.Count == 0 ? 0 : c[c.Count / 2]; }
			double Trim(List<double> l) { var c = new List<double>(l); c.Sort(); int k = c.Count / 20; return c.Count < 40 ? 0 : System.Linq.Enumerable.Average(c.GetRange(k, c.Count - 2 * k)); }
			double offM = Mean(off), fullM = Mean(on), offD = Med(off), fullD = Med(on), offT = Trim(off), fullT = Trim(on);
			// the projectile hook: friendly shots, so Damage walks the NPC list like a levy's shot does
			foreach (int i in foes) Main.npc[i].active = false;
			var shots = new List<int>();
			for (int k = 0; k < 300; k++) { int j = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_WorldEvent("probe"), p.Center + new Vector2(rnd.Next(-600, 600), rnd.Next(-300, 100)), Vector2.Zero, ProjectileID.Bullet, 10, 0f, 0); if (j >= 0) shots.Add(j); }
			double Damage(bool hooked)
			{
				if (hooked) global::Levy.Common.Util.ClientProf.Enable(false); else global::Levy.Common.Util.ClientProf.Disable();
				long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); int calls = 0;
				for (int rep = 0; rep < 40; rep++) foreach (int j in shots) { Projectile pr = Main.projectile[j]; if (!pr.active) continue; pr.velocity = Vector2.Zero; pr.Damage(); calls++; }
				double ns = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1e9 / System.Diagnostics.Stopwatch.Frequency / Math.Max(1, calls);
				global::Levy.Common.Util.ClientProf.Disable(); return ns;
			}
			Damage(true); Damage(false); double dOn = Math.Min(Damage(true), Damage(true)), dOff = Math.Min(Damage(false), Damage(false));
			foreach (int j in shots) Main.projectile[j].active = false;
			long c0 = System.Diagnostics.Stopwatch.GetTimestamp(); long sink = 0; for (int k = 0; k < 2_000_000; k++) sink += System.Diagnostics.Stopwatch.GetTimestamp();
			double clockNs = (System.Diagnostics.Stopwatch.GetTimestamp() - c0) * 1e9 / System.Diagnostics.Stopwatch.Frequency / 2_000_000.0;
			Mod.Logger.Info($"[ClientProf] overhead ({mine.Count} levies, 8 foes, {off.Count + on.Count} real NPC updates, each tick randomly with the profiler off or on): ms per tick mean off {offM:0.000} / full {fullM:0.000} (+{fullM - offM:0.000}, {100 * (fullM - offM) / Math.Max(1e-6, offM):0}%), trimmed mean (middle 90%) off {offT:0.000} / full {fullT:0.000} (+{fullT - offT:0.000}), median off {offD:0.000} / full {fullD:0.000} (+{fullD - offD:0.000}); projectile hook {dOff:0} ns per Damage call without, {dOn:0} with (+{dOn - dOff:0} ns, x300 shots = +{(dOn - dOff) * 300 / 1e6:0.000} ms per tick); Stopwatch.GetTimestamp {clockNs:0.0} ns (sink {sink & 1})");
		}

		/// <summary>Lab only (LEVY_FIGHTRECAUTO_SIZEPROBE=1): a big scripted fight of one minute (40 levies, a boss, 25 adds, 60 hostile projectiles, all moving) to measure bytes per minute.</summary>
		private void SizeProbe(Player p, int cx, string dir, List<SoldierRecord> recs, List<int> spawned)
		{
			var rnd = new Random(11); int more = 0;
			FightRecAuto.TestFolder = dir; FightRecAuto.TestDefaultDir = Path.Combine(dir, "..", "default"); FightRecAuto.ResetMessages();
			for (int i = 2; i < 40; i++)
			{
				var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Probe" + i, CommanderName = p.name, CommanderIndex = 0, LifeCrystals = 20, ManaCrystals = 9, HomeBarracksId = -1 };
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Gear.Weapon = new Item(i % 3 == 0 ? ItemID.Zenith : i % 3 == 1 ? ItemID.SpaceGun : ItemID.Megashark);
				RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
				Vector2 at = new Vector2((cx - 40 + i * 2) * 16f, Floor * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at); if (idx >= 0) { Main.npc[idx].Bottom = at; spawned.Add(idx); more++; }
			}
			var adds = SpawnAdds(cx, 25); var shots = new List<int>();
			for (int i = 0; i < 60; i++) { int j = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_WorldEvent("probe"), p.Center + new Vector2(rnd.Next(-900, 900), rnd.Next(-500, 200)), Vector2.Zero, ProjectileID.DeathLaser, 20, 0f); if (j >= 0) { Main.projectile[j].hostile = true; Main.projectile[j].timeLeft = 100000; shots.Add(j); } }
			FightRecAuto.ClearCooldown(); FightRecAuto.TestMode = FightRecMode.BossFights; FightRecorder.MaxFileBytes = 0; FightRecorder.MaxFolderBytes = 0;
			int boss = SpawnBoss(cx);
			var basePos = new Dictionary<int, Vector2>(); foreach (int i in spawned) basePos[i] = Main.npc[i].Center; foreach (int i in adds) basePos[i] = Main.npc[i].Center;
			for (int k = 0; k < 3600 + 60; k++)
			{
				if (k == 60) { }
				foreach (int i in spawned) { NPC n = Main.npc[i]; if (!n.active) continue; float ph = k / 40f + i; n.Center = basePos[i] + new Vector2((float)Math.Sin(ph) * 300f + rnd.Next(-2, 3), (float)Math.Cos(ph * 0.7) * 40f - 60 + rnd.Next(-2, 3)); n.velocity = new Vector2((float)Math.Cos(ph) * 3.1f, 0.4f * (float)Math.Sin(ph)); }
				foreach (int i in adds) { NPC n = Main.npc[i]; if (!n.active) continue; float ph = k / 25f + i; n.Center = basePos[i] + new Vector2((float)Math.Sin(ph) * 200f, (float)Math.Cos(ph) * 80f - 200); n.velocity = new Vector2((float)Math.Cos(ph) * 2f, 1f); }
				for (int s = 0; s < shots.Count; s++) { Projectile pr = Main.projectile[shots[s]]; pr.position += new Vector2((float)Math.Sin(k / 15f + s) * 6f, 5f); if (pr.position.Y > Floor * 16f) pr.position.Y -= 900f; pr.velocity = new Vector2(2f, 5f); pr.active = true; }
				p.position.X = cx * 16f + (float)Math.Sin(k / 60f) * 400f; p.velocity.X = (float)Math.Cos(k / 60f) * 3f;
				if (k % 5 == 0 && boss >= 0 && Main.npc[boss].active) { Main.npc[boss].life = Math.Max(1, Main.npc[boss].lifeMax - k * 30); FightRecorder.NoteDamage(Main.npc[boss], p, null, new Item(ItemID.Zenith), 120); }
				One(p, boss, k);
			}
			string running = FightRecorder.Path;
			if (boss >= 0) { FightRecorder.NoteBossKill(Main.npc[boss]); Main.npc[boss].life = 0; Main.npc[boss].active = false; }
			foreach (int j in shots) Main.projectile[j].active = false;
			KillAdds(adds);
			StepUntilStopped(p, -1, 1500);
			string f = FightRecorder.LastResult;
			if (f != null && File.Exists(f))
			{
				long raw = 0; foreach (string l in ReadLines(f, true, out _)) raw += l.Length + 1;
				Mod.Logger.Info($"[FightRecAuto] size probe: {more + 2} levies, 1 boss, 25 adds, 60 hostile projectiles, 60 s of fight: {raw / 1024} KB of text, {new FileInfo(f).Length / 1024} KB on disk ({raw / (double)new FileInfo(f).Length:0.0}x)");
			}
			else Mod.Logger.Warn("[FightRecAuto] size probe: no file");
		}

		/// <summary>
		/// Format 9: a fight with several main bosses is named for all of them (the Twins: "The Twins" in the file name, the header and the end record), and the boss-rule spite target is kind 3, apart from real targeting (kind 2).
		/// The naming rule is checked on its own too, so a boss the check cannot spawn is still covered.
		/// </summary>
		private void TwinsAndSpite(Player p, int cx, List<int> levies)
		{
			var twins = new List<(int, string)> { (NPCID.Retinazer, "Retinazer"), (NPCID.Spazmatism, "Spazmatism") };
			Chk(FightRecorder.FightName(twins) == "The Twins", "the two Twins are named The Twins (" + FightRecorder.FightName(twins) + ")");
			Chk(FightRecorder.FightName(new List<(int, string)> { (NPCID.Retinazer, "Retinazer") }) == "Retinazer", "a lone Retinazer keeps his name");
			Chk(FightRecorder.FightName(new List<(int, string)> { (1, "A"), (2, "B") }) == "A and B" && FightRecorder.FightName(new List<(int, string)> { (1, "A"), (2, "B"), (3, "C") }) == "A, B and C", "other bosses are joined: A and B, A, B and C");
			Chk(FightRecorder.FightName(new List<(int, string)> { (NPCID.EaterofWorldsHead, "Eater of Worlds"), (NPCID.EaterofWorldsBody, "Eater of Worlds") }) == "Eater of Worlds", "a boss with many segments is one name");
			Chk(FightRecorder.FightName(new List<(int, string)> { (NPCID.Retinazer, "Retinazer"), (1, "X"), (NPCID.Spazmatism, "Spazmatism") }) == "The Twins and X" && FightRecorder.FightName(new List<(int, string)>()) == null, "the Twins plus another boss, and no boss at all");
			Chk(FightRecFormat.Version >= 9 && FightRecFormat.TKinds.Contains("3 spite"), "the format documents target kind 3 (spite)");

			if (levies == null || levies.Count == 0 || !Main.npc[levies[0]].active || Main.npc[levies[0]].ModNPC is not SoldierNPC sn) { Chk(false, "a levy body stands for the spite check"); return; }
			int lv = levies[0];
			int ret = SpawnBoss(cx, NPCID.Retinazer), spaz = SpawnBoss(cx, NPCID.Spazmatism);
			Step(40, p, ret);
			Chk(FightRecorder.On && FightRecorder.Auto == 1, "Boss fights starts on the Twins");
			// the kinds, one cause at a time: the lock and an NPC target are kind 2, only the boss rule's grudge is kind 3
			NPC rb = Main.npc[ret]; var data = global::Levy.Common.Globals.LevyNpcData.Of(rb);
			int lockWas = data.SoldierTarget, tWas = rb.target, spT = data.SpiteTarget, spN = data.SpiteTimer;
			data.SoldierTarget = -1; rb.target = 0; data.SpiteTarget = -1; data.SpiteTimer = 0;
			FightRecorder.TargetOf(rb, out int kNone, out _); Chk(kNone == 1, "no levy cause: the boss is aimed at the commander (kind " + kNone + ")");
			data.SpiteTarget = lv; data.SpiteTimer = 300;
			FightRecorder.TargetOf(rb, out int kSpite, out int idSpite); Chk(kSpite == 3 && idSpite == FightRecorder.LevyId(sn, lv), "the boss rule's spite is kind 3 (kind " + kSpite + ")");
			data.SoldierTarget = lv;
			FightRecorder.TargetOf(rb, out int kLock, out _); Chk(kLock == 2, "the targeting lock stays kind 2 even with spite set (kind " + kLock + ")");
			data.SoldierTarget = -1; rb.target = 300 + lv;
			FightRecorder.TargetOf(rb, out int kNpc, out _); Chk(kNpc == 2, "an NPC target of 300 or more stays kind 2 (kind " + kNpc + ")");
			rb.target = 0; data.SpiteTarget = lv; data.SpiteTimer = 300;
			// and in the file: the grudge held for a few seconds
			for (int k = 0; k < 120; k++) { data.SoldierTarget = -1; rb.target = 0; data.SpiteTarget = lv; data.SpiteTimer = 300; One(p, ret, k); if (k % 7 == 0) FightRecorder.NoteDamage(rb, p, null, new Item(ItemID.Zenith), 220); if (k % 4 == 0) System.Threading.Thread.Sleep(1); }
			data.SoldierTarget = lockWas; rb.target = tWas; data.SpiteTarget = spT; data.SpiteTimer = spN;
			FightRecorder.NoteBossKill(Main.npc[ret]); FightRecorder.NoteBossKill(Main.npc[spaz]);
			Main.npc[ret].life = 0; Main.npc[ret].active = false; Main.npc[spaz].life = 0; Main.npc[spaz].active = false;
			StepUntilStopped(p, -1, 1500);
			string f = FightRecorder.LastResult;
			Chk(f != null && Regex.IsMatch(Path.GetFileName(f), @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}_The_Twins_won\.levyrec$"), "the Twins file is named The_Twins_won (" + Path.GetFileName(f ?? "none") + ")");
			var lines = f != null ? ReadLines(f, true, out _) : new List<string>();
			bool hdrOk = false, endOk = false, endSpite = false, tgt3 = false;
			int bossEv = 0;
			foreach (string l in lines)
			{
				try
				{
					using var d = JsonDocument.Parse(l); var r = d.RootElement; string kk = r.GetProperty("k").GetString();
					if (kk == "hdr") hdrOk = r.GetProperty("boss").GetString() == "The_Twins" && r.GetProperty("format").GetInt32() == FightRecFormat.Version;
					else if (kk == "end") { endOk = r.GetProperty("bossName").GetString() == "The Twins"; endSpite = r.GetProperty("target").TryGetProperty("spite", out var sp) && sp.GetInt32() > 0; }
					else if (kk == "e") { string e = r.GetProperty("e").GetString(); if (e == "tgt" && r.GetProperty("kind").GetInt32() == 3) tgt3 = true; if (e == "boss") bossEv++; }
				}
				catch { }
			}
			Chk(hdrOk, "the header names the fight The_Twins");
			Chk(endOk, "the end record's bossName is The Twins");
			Chk(bossEv >= 2, "both Twins are logged as bosses (" + bossEv + ")");
			Chk(tgt3, "a tgt event of kind 3 (spite) is written");
			Chk(endSpite, "end.target.spite counts the spite samples");
		}

		// ---------------------------------------------------------------- scripting helpers
		/// <summary>A friendly projectile owned by the commander's slot put straight into a free slot (no spawn hooks: other mods' OnSpawn hooks need a real player); soldier-tagged when asked.</summary>
		private static int PlaceShot(Player p, Vector2 off, Vector2 vel, bool soldier)
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				if (Main.projectile[i].active) continue;
				var pr = new Projectile(); pr.SetDefaults(ProjectileID.MagicMissile);
				pr.whoAmI = i; pr.owner = p.whoAmI; pr.friendly = true; pr.hostile = false; pr.damage = 40; pr.Center = p.Center + off; pr.velocity = vel; pr.timeLeft = 600; pr.active = true;
				if (soldier && pr.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag)) tag.SoldierIndex = 3;
				Main.projectile[i] = pr; return i;
			}
			return -1;
		}

		internal static int SpawnBoss(int cx, int type = NPCID.EyeofCthulhu)
		{
			int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("FightRecAutoCheck"), cx * 16 + 400, (Floor - 25) * 16, type);
			if (i >= 0) { NPC b = Main.npc[i]; b.aiStyle = -1; b.noGravity = true; b.noTileCollide = true; b.life = b.lifeMax = 2800; b.damage = 0; b.ai[3] = 4242f; }
			return i;
		}

		internal static List<int> SpawnAdds(int cx, int n)
		{
			var l = new List<int>();
			for (int k = 0; k < n; k++)
			{
				int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("FightRecAutoCheck"), cx * 16 + 200 + k * 40, (Floor - 3) * 16, NPCID.BlueSlime);
				if (i >= 0) { NPC b = Main.npc[i]; b.aiStyle = -1; b.noGravity = true; b.velocity = Vector2.Zero; b.damage = 20; b.life = b.lifeMax = 100; b.ai[3] = 4242f; l.Add(i); }
			}
			return l;
		}

		internal static void KillAdds(List<int> adds) { foreach (int i in adds) Main.npc[i].active = false; adds.Clear(); }

		/// <summary>Advances the game clock by hand and runs what the system runs every tick. Keeps the scripted boss and commander in place.</summary>
		internal static void Step(int ticks, Player p, int? boss)
		{
			for (int k = 0; k < ticks; k++) One(p, boss ?? -1, k);
		}

		internal static void One(Player p, int boss, int k)
		{
			_clock ??= typeof(Main).GetField("_gameUpdateCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			_clock.SetValue(null, Main.GameUpdateCount + 1u);
			p.active = true; p.statLife = Math.Max(1, p.statLife); p.itemAnimation = k % 40 < 10 ? 10 - k % 40 : 0; p.selectedItem = 0;
			if (boss >= 0 && Main.npc[boss].active)
			{
				NPC b = Main.npc[boss]; b.timeLeft = 3600; float ang = k / 70f;
				b.Center = p.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang) * 0.5f - 0.6f) * 400f; b.velocity = new Vector2(3, 0);
			}
			long q = global::Levy.Common.Util.ClientProf.TestBegin(); // the profiler (when a self-check has it on) sees this tick as one update and one drawn frame
			FightRecSystem.Step();
			if (q != 0) { global::Levy.Common.Util.ClientProf.TestFrame(System.Diagnostics.Stopwatch.Frequency / 1000); global::Levy.Common.Util.ClientProf.TestEnd(q); }
		}

		/// <summary>The commander and an unnamed shooter hit the boss every few ticks while the clock runs; one commander hit; real-time pauses so the writer thread can flush.</summary>
		internal static void ScriptHits(Player p, int boss, int ticks, int sleep = 1)
		{
			for (int k = 0; k < ticks; k++)
			{
				One(p, boss, k);
				if (FightRecorder.On && Main.npc[boss].active)
				{
					if (k % 7 == 0) FightRecorder.NoteDamage(Main.npc[boss], p, null, new Item(ItemID.Zenith), 220);
					if (k % 11 == 0) { var pr = new Projectile(); pr.SetDefaults(ProjectileID.MagicMissile); pr.owner = p.whoAmI; if (pr.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag)) tag.SoldierIndex = 3; FightRecorder.NoteDamage(Main.npc[boss], null, pr, null, 90); }
					if (k == 300) FightRecorder.NoteHit(p, 42, boss, 0, -1);
				}
				if (sleep > 0 && k % 4 == 0) System.Threading.Thread.Sleep(sleep);
			}
		}

		internal static int StepUntilStopped(Player p, int boss, int max)
		{
			int n = 0;
			while (FightRecorder.On && n < max) { One(p, boss, n); n++; }
			return n;
		}

		// ---------------------------------------------------------------- reading the file back
		private void VerifyFile(string path, string trigger, int auto, string boss, string result)
		{
			if (path == null || !File.Exists(path)) { Chk(false, "recording exists for " + trigger); return; }
			var lines = ReadLines(path, true, out string err);
			Chk(err == null && lines.Count > 20, "the file decompresses and parses (" + (err ?? lines.Count + " lines") + ")");
			bool allJson = true; int frames = 0, dmgEv = 0, cdeath = 0; JsonElement hdr = default, end = default; bool gotHdr = false, gotEnd = false; double dmgCmd = 0, dmgLevyOther = 0;
			foreach (string l in lines)
			{
				try
				{
					using var d = JsonDocument.Parse(l); var r = d.RootElement; string k = r.GetProperty("k").GetString();
					if (k == "hdr") { hdr = r.Clone(); gotHdr = true; }
					else if (k == "end") { end = r.Clone(); gotEnd = true; }
					else if (k == "f") frames++;
					else if (k == "e")
					{
						string e = r.GetProperty("e").GetString();
						if (e == "cdeath") cdeath++;
						if (e == "dmg") { dmgEv++; foreach (var row in r.GetProperty("d").EnumerateArray()) { if (row[0].GetString() == "cmd") dmgCmd += row[5].GetDouble(); else dmgLevyOther += row[5].GetDouble(); } }
					}
				}
				catch { allJson = false; }
			}
			Chk(allJson && gotHdr && gotEnd && lines[0].Contains("\"k\":\"hdr\"") && lines[lines.Count - 1].Contains("\"k\":\"end\""), "every line is JSON, the header comes first and the end record last");
			if (!gotHdr || !gotEnd) return;
			Chk(hdr.GetProperty("format").GetInt32() == FightRecFormat.Version && hdr.GetProperty("auto").GetInt32() == auto && hdr.GetProperty("gzip").GetInt32() == 1, "header: format " + FightRecFormat.Version + ", automatic kind " + auto + ", compressed");
			bool boss0 = boss == null || hdr.GetProperty("boss").GetString() == "Eye_of_Cthulhu";
			Chk(boss0 && hdr.GetProperty("meta").GetProperty("trigger").GetString() == trigger && hdr.GetProperty("meta").GetProperty("source").GetString() == "auto", "header: what started it (" + trigger + ") and the boss name");
			Chk(hdr.TryGetProperty("mode", out var mode) && mode.GetString().Length > 0, "header: difficulty / game mode (" + (hdr.TryGetProperty("mode", out var m2) ? m2.GetString() : "?") + ")");
			bool modsOk = false, levyVer = false;
			if (hdr.TryGetProperty("mods", out var mods)) foreach (var m in mods.EnumerateArray()) { if (m[0].GetString() == "Levy") { modsOk = true; levyVer = m[1].GetString() == hdr.GetProperty("levy").GetString() && m[1].GetString().Length > 0; } }
			Chk(modsOk && levyVer, "header: mod list with versions, and the Levy version");
			var army = hdr.GetProperty("army");
			Chk(army.GetProperty("n").GetInt32() >= 2 && army.GetProperty("classes").EnumerateObject().GetEnumerator().MoveNext() && army.GetProperty("weapons").GetArrayLength() >= 1, "header: army size, classes and weapons (" + army.GetRawText().Substring(0, Math.Min(90, army.GetRawText().Length)) + ")");
			var cmd = hdr.GetProperty("cmd");
			Chk(cmd.GetProperty("life").GetInt32() >= 100 && cmd.GetProperty("armor").GetArrayLength() >= 1 && cmd.GetProperty("accessories").GetArrayLength() >= 1 && cmd.GetProperty("hotbar").GetArrayLength() >= 1, "header: commander life, armor, accessories and hotbar");
			Chk(hdr.GetProperty("player").GetString() == "FightRecAutoTester" && hdr.GetProperty("world").GetString().Length > 0 && hdr.GetProperty("date").GetString().Length > 0, "header: player, world and date");
			Chk(end.GetProperty("result").GetString() == result && end.GetProperty("durationSec").GetDouble() > 5 && end.GetProperty("frames").GetInt32() == frames, "end: result " + result + ", duration, and the frame count matches the frames in the file (" + frames + ")");
			Chk(end.GetProperty("armyTotal").GetInt32() >= 2 && end.TryGetProperty("dmgBoss", out _) && end.TryGetProperty("cmdDeaths", out _) && end.TryGetProperty("profiled", out _), "end: army, damage totals, commander deaths and the profiled flag");
			if (trigger == "boss") Chk(dmgEv >= 3 && dmgCmd > 1000 && dmgLevyOther > 100 && end.GetProperty("dmgBoss").GetInt64() > 1000, "damage events split the commander's hits from the rest (" + dmgCmd + " / " + dmgLevyOther + ")");
			if (trigger == "army") Chk(cdeath == 1 && end.GetProperty("cmdDeaths").GetInt32() == 1, "the commander's fall is an event and is counted");
		}
	}
}
