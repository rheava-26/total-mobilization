using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Perf;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Boot self-check (log prefix [SmallFix], a dedicated server with LEVY_SELFCHECK_WORLD=1) of the small fixes:
	/// the active army size reaching the settings file and coming back from it (tModLoader's SaveChanges and the ConfigManager.Save reflection, a Load of the file) and from the character's file;
	/// the one-time move of a saved manpower-per-townsperson 9 to 15 (once only, a later choice never overridden, nothing recorded when the save failed);
	/// each letter of a shaking chat line trembling on its own, within bounds, allocating nothing;
	/// the in-game clock the Recruitment Office rests on (a day-to-night wrap, a time jump) and the Office's save and net round trips.
	/// The world, the Office price chain and the potion heal number are checked in [SettlementWorld] and [LevyRules].
	/// </summary>
	public delegate void CheckFn(string name, bool ok, Func<string> why = null);

	public class SmallFixWorldCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return; // test boots only
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok, Func<string> why = null) { if (ok) pass++; else fail++; Mod.Logger.Info($"[SmallFix] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }
			void Info(string text) => Mod.Logger.Info("[SmallFix] OBSERVED " + text);
			try { ArmyPersistence(Check, Info); } catch (Exception e) { Check("army persistence threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			try { Migration(Check); } catch (Exception e) { Check("migration threw " + e.GetType().Name + ": " + e.Message, false); }
			try { Tremble(Check, Info); } catch (Exception e) { Check("tremble threw " + e.GetType().Name + ": " + e.Message, false); }
			try { Clock(Check); } catch (Exception e) { Check("clock threw " + e.GetType().Name + ": " + e.Message, false); }
			Mod.Logger.Info($"[SmallFix] self-check pass={pass} fail={fail}");
		}

		// ---------------------------------------------------------------------------------------------- 1. active army size
		private static string ConfigFile(ModConfig c) => Path.Combine(Main.SavePath, "ModConfigs", c.Mod.Name + "_" + c.Name + ".json");

		private static bool FileHasArmy(ModConfig c, int size, out string text)
		{
			text = File.Exists(ConfigFile(c)) ? File.ReadAllText(ConfigFile(c)) : "";
			var m = System.Text.RegularExpressions.Regex.Match(text, "\"ActiveArmySize\"\\s*:\\s*(-?\\d+)");
			if (!m.Success) return size == 0 && text.Length > 0; // tModLoader leaves a value equal to its default out of the file: absent reads back as 0
			return int.Parse(m.Groups[1].Value) == size;
		}

		private static void ArmyPersistence(CheckFn Check, Action<string> Info)
		{
			var cfg = PerfConfig.Get;
			Check("the performance config is loaded", cfg != null);
			if (cfg == null) return;
			int was = cfg.ActiveArmySize;
			bool hadFile = File.Exists(ConfigFile(cfg)); string oldText = hadFile ? File.ReadAllText(ConfigFile(cfg)) : null;
			try
			{
				var save = typeof(ConfigManager).GetMethod("Save", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ModConfig) }, null);
				var load = typeof(ConfigManager).GetMethod("Load", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ModConfig) }, null);
				Info($"ConfigManager.Save(ModConfig) {(save != null ? "found" : "MISSING")}, Load(ModConfig) {(load != null ? "found" : "missing")}, file {ConfigFile(cfg)}");
				Check("the public way (ModConfig.SaveChanges) writes the army size to the settings file", Do(() => { cfg.ActiveArmySize = 37; return ConfigMigrations.SaveConfig(cfg); }) && FileHasArmy(cfg, 37, out string t1), () => "file: " + (File.Exists(ConfigFile(cfg)) ? File.ReadAllText(ConfigFile(cfg)) : "none"));
				Check("ConfigManager.Save by reflection exists and writes it too (the fallback)", save != null && Do(() => { cfg.ActiveArmySize = 12; return ConfigMigrations.SaveViaManager(cfg); }) && FileHasArmy(cfg, 12, out string t2), () => "file: " + (File.Exists(ConfigFile(cfg)) ? File.ReadAllText(ConfigFile(cfg)) : "none"));
				if (load != null)
				{
					cfg.ActiveArmySize = 0;
					load.Invoke(null, new object[] { cfg });
					Check("loading the settings file puts the saved army size back (12, after it was 0 in memory)", cfg.ActiveArmySize == 12, () => cfg.ActiveArmySize.ToString());
				}
				// the saved size is what the next start reads: SetActiveArmy -> SaveConfig on the client
				cfg.ActiveArmySize = 0;
				Check("size 0 (no limit) is written as 0, so 'no limit' also survives", Do(() => ConfigMigrations.SaveConfig(cfg)) && FileHasArmy(cfg, 0, out _));
			}
			finally
			{
				cfg.ActiveArmySize = was;
				try { if (hadFile) File.WriteAllText(ConfigFile(cfg), oldText); else if (File.Exists(ConfigFile(cfg))) File.Delete(ConfigFile(cfg)); } catch (Exception) { }
			}

			// the character's file: what comes back when the settings file lost it
			Check("a config with no limit takes the character's saved size", ActiveArmyPlayer.Resolve(0, true, 22) == 22);
			Check("a size set in the settings file wins over the character's", ActiveArmyPlayer.Resolve(30, true, 22) == 30);
			Check("a character with no saved size keeps the settings file's", ActiveArmyPlayer.Resolve(0, false, 0) == 0 && ActiveArmyPlayer.Resolve(15, false, 0) == 15);
			Check("a saved size above the maximum is held to it", ActiveArmyPlayer.Resolve(0, true, 9999) == LevyConstants.ActiveArmyMax);
			Player p = new Player();
			if (p.TryGetModPlayer(out ActiveArmyPlayer ap))
			{
				int s0 = ap.Saved; bool h0 = ap.HasSaved;
				try
				{
					ap.Saved = 22; ap.HasSaved = true;
					var tag = new TagCompound(); ap.SaveData(tag);
					ap.Saved = 0; ap.HasSaved = false; ap.LoadData(tag);
					Check("the character's file round trip keeps the size (22)", ap.HasSaved && ap.Saved == 22 && tag.GetInt("activeArmy") == 22, () => $"{ap.HasSaved}/{ap.Saved}");
					ap.LoadData(new TagCompound());
					Check("an older character file (no size) loads as 'none saved'", !ap.HasSaved && ap.Saved == 0);
				}
				finally { ap.Saved = s0; ap.HasSaved = h0; }
			}
			else Check("the player data of the army size exists", false);
		}

		private static bool Do(Func<bool> f) { try { return f(); } catch (Exception) { return false; } }

		// ---------------------------------------------------------------------------------------------- 3. manpower per townsperson: 9 -> 15, once
		private static void Migration(CheckFn Check)
		{
			var cfg = LevyConfig.Get;
			Check("the default manpower per townsperson is 15 (attribute and constant)", LevyConstants.ManpowerPerNpc == 15 && typeof(LevyConfig).GetField("ManpowerPerTownNpc").GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()?.Value is 15);
			if (cfg != null)
			{
				int per = cfg.ManpowerPerTownNpc;
				try
				{
					cfg.ManpowerPerTownNpc = LevyConstants.ManpowerPerNpc;
					Check("Conscription at the new default is round(15 x 15 / 9) = 25", SettlementSystem.Instance.ManpowerPerNpcNow(true) == 25 && LevyConstants.ManpowerPerNpcConscription == 25, () => SettlementSystem.Instance.ManpowerPerNpcNow(true).ToString());
				}
				finally { cfg.ManpowerPerTownNpc = per; }
			}
			string marker = Path.Combine(Main.SavePath, "ModConfigs", "Levy_ConfigMigrations_selfcheck.txt");
			try { File.Delete(marker); } catch (Exception) { }
			ConfigMigrations.TestForget();
			try
			{
				int v = 9; int saves = 0; bool saveOk = true;
				bool Run(string key) => ConfigMigrations.MoveOnce(key, () => v == LevyConstants.ManpowerPerNpcOldDefault, () => v = LevyConstants.ManpowerPerNpc, () => { saves++; return saveOk; }, marker);
				Check("a saved 9 (the old default) moves to 15 and is saved", Run("k1") && v == 15 && saves == 1 && ConfigMigrations.IsDone("k1", marker));
				v = 9; saves = 0;
				Check("run again after the player chose 9 on purpose: it stays 9, nothing saved", !Run("k1") && v == 9 && saves == 0);
				v = 20;
				Check("a value already changed (20) is left alone, and the move is recorded", !Run("k2") && v == 20 && saves == 0 && ConfigMigrations.IsDone("k2", marker));
				v = 15;
				Check("a fresh install (already 15) is left alone and recorded", !Run("k3") && v == 15 && ConfigMigrations.IsDone("k3", marker));
				v = 9; saveOk = false; saves = 0;
				bool moved = Run("k4");
				Check("the save failed: the live value is 15 for this session but nothing is recorded", moved && v == 15 && !ConfigMigrations.IsDone("k4", marker));
				saveOk = true;
				bool again = Run("k4");
				Check("the next run saves it and records it, without judging the live 15 as a player's choice", !again && ConfigMigrations.IsDone("k4", marker) && saves == 2, () => $"again {again}, saves {saves}");
				Check("the marker file lists exactly the recorded moves", File.ReadAllLines(marker).Length == 4);
			}
			finally { try { File.Delete(marker); } catch (Exception) { } ConfigMigrations.TestForget(); }
		}

		// ---------------------------------------------------------------------------------------------- 2. each letter trembles
		private static void Tremble(CheckFn Check, Action<string> Info)
		{
			const int Letters = 30, Steps = 400;
			bool bounded = true, noShake = true; int blockLike = 0, moving = 0, distinctSteps = 0;
			var seen = new System.Collections.Generic.HashSet<(float, float)>();
			for (int s = 0; s < Steps; s++)
			{
				int now = s * LevyConstants.ChatShakeStepTicks; seen.Clear(); int moved = 0;
				Vector2 first = default;
				bool allSame = true;
				for (int i = 0; i < Letters; i++)
				{
					Vector2 j = global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, i, now);
					if (Math.Abs(j.X) > LevyConstants.ChatShakeLetterPx + 1 || Math.Abs(j.Y) > LevyConstants.ChatShakeLetterPx + 1) bounded = false;
					if (global::Levy.Common.UI.ChatBars.LetterJitter(0f, 7, 0, i, now) != Vector2.Zero) noShake = false;
					if (j != Vector2.Zero) moved++;
					if (i == 0) first = j; else if (j != first) allSame = false;
					seen.Add((j.X, j.Y));
				}
				if (allSame) blockLike++;
				if (moved > 0) moving++;
				if (seen.Count >= 3) distinctSteps++;
			}
			Info($"{Steps} steps of a {Letters}-letter line at full shake: {moving} steps with a moving letter, {blockLike} steps where every letter had the same offset, {distinctSteps} steps with 3+ different offsets");
			Check("letter tremor stays within 2 pixels and never moves with no shake", bounded && noShake);
			Check("letters tremble independently: almost every step has several different offsets, the whole line almost never moves as one block", distinctSteps > Steps * 0.95 && blockLike < Steps * 0.02, () => $"distinct {distinctSteps}, block {blockLike}");
			// two letters of one line differ over time (their own offsets, not a shared one)
			int differ = 0;
			for (int s = 0; s < Steps; s++) if (global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, 3, s * 4) != global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, 4, s * 4)) differ++;
			Check("two neighbouring letters have different offsets on most steps", differ > Steps * 0.5, () => differ.ToString());
			// the offset changes with time, one step at a time
			int changes = 0; Vector2 last = global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, 5, 0);
			for (int s = 1; s < Steps; s++) { var j = global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, 5, s * LevyConstants.ChatShakeStepTicks); if (j != last) changes++; last = j; }
			Check("one letter's offset changes over time", changes > Steps * 0.4, () => changes.ToString());
			// a milder shake (the Blood Moon's despairing lines) moves fewer letters and by one pixel at most
			int mildMoved = 0; bool mildBounded = true;
			for (int s = 0; s < Steps; s++) for (int i = 0; i < Letters; i++) { var j = global::Levy.Common.UI.ChatBars.LetterJitter(0.6f, 7, 0, i, s * 4); if (j != Vector2.Zero) mildMoved++; if (Math.Abs(j.X) > 1f || Math.Abs(j.Y) > 1f) mildBounded = false; }
			int fullMoved = 0; for (int s = 0; s < Steps; s++) for (int i = 0; i < Letters; i++) if (global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 0, i, s * 4) != Vector2.Zero) fullMoved++;
			Check("a mild shake (0.6) moves fewer letters than full and by one pixel at most", mildBounded && mildMoved < fullMoved && mildMoved > 0, () => $"mild {mildMoved}, full {fullMoved}");
			long before = GC.GetAllocatedBytesForCurrentThread(); float sink = 0f;
			for (int s = 0; s < 20000; s++) sink += global::Levy.Common.UI.ChatBars.LetterJitter(1f, 7, 1, s % Letters, s).X;
			long alloc = GC.GetAllocatedBytesForCurrentThread() - before;
			Check("working out a letter's offset allocates nothing (20000 calls)", alloc < 512, () => alloc + " bytes " + sink);
		}

		// ---------------------------------------------------------------------------------------------- 4. the clock the Office rests on
		private static void Clock(CheckFn Check)
		{
			double n0 = WorldClock.Now;
			try
			{
				WorldClock.TestRestart();
				WorldClock.TestFeed(1000, true);
				double a = WorldClock.Now; WorldClock.TestFeed(1600, true);
				Check("the clock counts the ticks the time of day moved (600)", Math.Abs(WorldClock.Now - a - 600) < 0.01, () => (WorldClock.Now - a).ToString());
				WorldClock.TestFeed(53900, true); double b = WorldClock.Now; WorldClock.TestFeed(100, false);
				Check("across dusk it counts what was left of the day plus the night so far (100 + 100 = 200)", Math.Abs(WorldClock.Now - b - 200) < 0.01, () => (WorldClock.Now - b).ToString());
				double c = WorldClock.Now; WorldClock.TestFeed(100, false); WorldClock.TestFeed(40, false);
				Check("a time command backwards adds nothing", Math.Abs(WorldClock.Now - c) < 0.01, () => (WorldClock.Now - c).ToString());
				Check("a day and a night are 86400 ticks", Math.Abs(WorldClock.DayTicks - 86400) < 0.01 && LevyConstants.RecruitmentOfficeRestTicks == 86400);
				Check("the time left reads as in-game hours and minutes", WorldClock.Format(3600 * 13 + 1200) == "13h 20m" && WorldClock.Format(2400) == "40m" && WorldClock.Format(5) == "1m" && WorldClock.Format(0) == "0m", () => WorldClock.Format(3600 * 13 + 1200) + " / " + WorldClock.Format(2400));
			}
			finally { WorldClock.Now = n0; WorldClock.TestRestart(); }

			// the Office's own state
			var te = TestTileEntities.Make<RecruitmentOfficeTE>(new Point16(100, 100));
			Check("a new Office is ready", te.RestTicks == 0);
			te.StartRest();
			Check("a drive makes it rest one in-game day", Math.Abs(te.RestTicks - 86400) < 0.01, () => te.RestTicks.ToString());
			WorldClock.Now += 3600 * 5;
			Check("5 in-game hours later 19 remain", Math.Abs(te.RestTicks - 3600 * 19) < 0.01, () => te.RestTicks.ToString());
			var tag = new TagCompound(); te.SaveData(tag);
			var te2 = TestTileEntities.Make<RecruitmentOfficeTE>(new Point16(100, 100)); te2.LoadData(tag);
			Check("the world save keeps the time left", Math.Abs(te2.RestTicks - 3600 * 19) < 1.01, () => te2.RestTicks.ToString());
			WorldClock.Now += 3600 * 24;
			Check("after the day it is ready again", te.RestTicks == 0 && te2.RestTicks == 0);
			var ready = TestTileEntities.Make<RecruitmentOfficeTE>(new Point16(100, 100)); var rt = new TagCompound(); ready.SaveData(rt);
			Check("a ready Office writes nothing and loads ready", !rt.ContainsKey("rest") && Do(() => { var x = TestTileEntities.Make<RecruitmentOfficeTE>(new Point16(100, 100)); x.LoadData(rt); return x.RestTicks == 0; }));
			te.StartRest(); WorldClock.Now += 3600 * 2;
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) te.NetSend(w);
				ms.Position = 0;
				var te3 = TestTileEntities.Make<RecruitmentOfficeTE>(new Point16(100, 100));
				using (var r = new BinaryReader(ms)) te3.NetReceive(r);
				Check("the net message gives a client the time left (22 hours)", Math.Abs(te3.RestTicks - 3600 * 22) < 1.01, () => te3.RestTicks.ToString());
			}
			// the world data carries the count of rush drives since dawn
			var sys = SettlementSystem.Instance; int rd = sys.RushDrivesToday;
			try
			{
				sys.RushDrivesToday = 4;
				var wt = new TagCompound(); sys.SaveWorldData(wt);
				sys.RushDrivesToday = 0; sys.LoadWorldData(wt);
				Check("the world save keeps the rush drives run since dawn", sys.RushDrivesToday == 4, () => sys.RushDrivesToday.ToString());
				using (var ms = new MemoryStream())
				{
					using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) sys.NetSend(w);
					sys.RushDrivesToday = 0; ms.Position = 0;
					using (var r = new BinaryReader(ms)) sys.NetReceive(r);
					Check("the world data tells a client how many (4)", sys.RushDrivesToday == 4, () => sys.RushDrivesToday.ToString());
				}
				long b0 = SettlementSystem.RushPriceAfter(0);
				Check("the price doubles with every rush drive since dawn", SettlementSystem.RushPriceAfter(1) == 2 * b0 && SettlementSystem.RushPriceAfter(2) == 4 * b0 && SettlementSystem.RushPriceAfter(10) == 1024 * b0 && b0 == 4 * EconomySystem.LevyWage, () => $"{b0} {SettlementSystem.RushPriceAfter(1)}");
				Check("a very long chain stays a number (no overflow)", SettlementSystem.RushPriceAfter(500) > 0 && SettlementSystem.RushPriceAfter(500) == SettlementSystem.RushPriceAfter(LevyConstants.RushDriveMaxDoublings));
			}
			finally { sys.RushDrivesToday = rd; }
		}
	}
}
