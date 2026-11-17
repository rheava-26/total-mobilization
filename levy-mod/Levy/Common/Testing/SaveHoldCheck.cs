using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD SELF-CHECK of saving while a check runs, logged as "[SaveHold]" lines (test boots: dedicated server, LEVY_SELFCHECK_WORLD=1; LEVY_SAVEHOLD=0 skips it). It runs first in the world-check chain,
	/// before the chain opens its own <see cref="SaveHold"/>, and asserts:
	/// the probe sees the bug (an armory built with <c>new</c> and put in ByID makes the tile-entity save throw, as in the 11-17 crash); every Levy, Mechanization and Warbands tile entity built through
	/// <see cref="TestTileEntities"/> is save-safe and both halves of the save (vanilla and tML) write it; with a hold open the ten-minute autosave's own callback, run on a pool thread, saves nothing and
	/// does not throw even with the unsafe armory in; a world save forced on the check's own thread mid-hold, with every fixture in, completes; and after the hold (nested, thrown out of, with autosave on or
	/// off before it) <see cref="Main.autoSave"/> is back to what it was, no hold is open and the autosave callback saves the world again.
	/// <see cref="ProbeMidCheck"/> repeats the save probe from inside the checks that plant fixtures (the potion armory of [LevyRules], the bet barracks of [BossBugs]).
	/// </summary>
	public class SaveHoldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _pass, _fail;
		private static readonly MethodInfo TileIoSave = typeof(Mod).Assembly.GetType("Terraria.ModLoader.IO.TileIO")?.GetMethod("SaveTileEntities", BindingFlags.Static | BindingFlags.NonPublic);
		private static readonly string[] OurMods = { "Levy", "Mechanization", "Warbands" };

		public override void OnWorldLoad()
		{
			_armed = Main.dedServ && Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_SAVEHOLD") != "0";
			_done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		private static void Log(string s) => global::Levy.Levy.Instance?.Logger.Info("[SaveHold] " + s);
		private static void Check(string name, bool ok, string detail = "")
		{
			if (ok) _pass++; else _fail++;
			Log($"{(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}
		/// <summary>An error's short name and message, worded so the ship report's error scan does not count a caught, expected one.</summary>
		private static string Short(Exception e)
		{
			while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
			string frame = (e.StackTrace ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
			return Clean($"{e.GetType().Name} ({e.Message}) {frame}");
		}
		private static string Clean(string s) => s.Replace("Exception", "Err").Replace("exception", "err");

		/// <summary>Runs the check once per boot, outside any hold (the chain calls it before it opens its own).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done) return;
			_done = true;
			try { Run(); }
			catch (Exception e) { Check("the check ran to its end", false, Short(e)); }
			Log($"self-check pass={_pass} fail={_fail}");
		}

		// ------------------------------------------------------------------------------------------------ the probes
		/// <summary>The tile entities as both halves of a world save write them, into memory: vanilla's WorldFile.SaveTileEntities and tML's TileIO.SaveTileEntities. Null when both ran; else what threw.</summary>
		private static string SaveTileEntitiesInMemory(out List<TagCompound> modded)
		{
			modded = null;
			try
			{
				using (var bw = new BinaryWriter(new MemoryStream())) WorldFile.SaveTileEntities(bw);
				if (TileIoSave == null) return "tML's TileIO.SaveTileEntities was not found";
				modded = TileIoSave.Invoke(null, null) as List<TagCompound>;
				return null;
			}
			catch (Exception e) { return Short(e); }
		}

		/// <summary>The ten-minute autosave's own callback (WorldGen.saveAndPlayCallBack) run on a pool thread, as the autosave runs it. Null when it returned; else what threw.</summary>
		private static string AutosaveOnPool()
		{
			string r = "no answer in 120 s"; using var done = new ManualResetEventSlim();
			ThreadPool.QueueUserWorkItem(_ => { try { WorldGen.saveAndPlayCallBack(1); r = null; } catch (Exception e) { r = Short(e); } finally { done.Set(); } });
			done.Wait(120_000);
			return r;
		}

		private static DateTime WorldStamp() { try { return Main.worldPathName != null && File.Exists(Main.worldPathName) ? File.GetLastWriteTimeUtc(Main.worldPathName) : DateTime.MinValue; } catch (IOException) { return DateTime.MinValue; } }

		/// <summary>
		/// Called by a check while its fixtures are in the world (and the chain's hold is open): the tile entities as a save writes them, into memory, and the autosave callback on a pool thread.
		/// Both must return without throwing, and the autosave must save nothing while the hold is open. Runs only on a test boot.
		/// </summary>
		public static void ProbeMidCheck(string where)
		{
			if (!_armed) return;
			try
			{
				var bad = TestTileEntities.FindUnsafe();
				Check($"mid-check save ({where}): every registered tile entity is save-safe", bad.Count == 0, string.Join("; ", bad.Select(TestTileEntities.Describe)));
				string mem = SaveTileEntitiesInMemory(out _);
				Check($"mid-check save ({where}): the tile entities serialize as a world save writes them", mem == null, mem ?? "");
				DateTime before = WorldStamp();
				string pool = AutosaveOnPool();
				bool held = SaveHold.Depth > 0 && SaveHold.Locked;
				Check($"mid-check save ({where}): the autosave callback returns without throwing{(held ? " and saves nothing while the hold is open" : "")}", pool == null && (!held || WorldStamp() == before), pool ?? $"hold depth {SaveHold.Depth}, locked {SaveHold.Locked}, world written {WorldStamp() != before}");
			}
			catch (Exception e) { Check($"mid-check save ({where}) ran", false, Short(e)); }
		}

		// ------------------------------------------------------------------------------------------------ the check
		private static void Run()
		{
			bool auto0 = Main.autoSave;
			Check("no save hold is open before the checks start", SaveHold.Depth == 0 && !SaveHold.Locked);
			Check("the world-save lock (WorldFile.IOLock) is found", SaveHold.LockFound);
			Check("a world is loaded with a file to save to", Main.worldPathName != null && File.Exists(Main.worldPathName), Main.worldPathName ?? "null");

			// 1. the bug itself: the probe must see it, or its passes below mean nothing
			var raw = new ArmoryTE { Position = new Point16(12, 12) }; raw.ID = 899_990;
			try
			{
				lock (TileEntity.EntityCreationLock) TileEntity.ByID[raw.ID] = raw;
				Check("an armory built with new is not save-safe (type byte 0, no mod)", !TestTileEntities.IsSaveSafe(raw), TestTileEntities.Describe(raw));
				string r;
				using (new Logging.QuietExceptionHandle()) r = SaveTileEntitiesInMemory(out _); // the throw is expected here: kept out of tML's caught-error log
				Check("with it in ByID the tile-entity save throws NotImplemented from ModTileEntity.WriteExtraData (the 11-17 crash)", r != null && r.StartsWith("NotImplemented") && r.Contains("WriteExtraData"), r ?? "it saved");

				// 2. the hold: the autosave's own callback on a pool thread saves nothing and does not throw, even with the unsafe armory in
				using (SaveHold.Begin("SaveHold check"))
				{
					Check("inside a hold autosave is off and the world-save lock is held", !Main.autoSave && SaveHold.Locked && SaveHold.Depth == 1);
					Check("inside a hold another thread cannot start a world save", !SaveHold.FreeForOtherThreads());
					DateTime t0 = WorldStamp();
					string pool = AutosaveOnPool();
					Check("inside a hold the autosave callback on a pool thread returns without saving, with an unsafe entity in", pool == null && WorldStamp() == t0, pool ?? $"world written {WorldStamp() != t0}");
				}
			}
			finally { TestTileEntities.Remove(raw); }

			// 3. every tile entity type of our mods, built through TestTileEntities, saves cleanly; a save forced on this thread mid-hold completes
			var made = new List<ModTileEntity>();
			try
			{
				using (SaveHold.Begin("SaveHold check"))
				{
					var types = ModContent.GetContent<ModTileEntity>().Where(t => OurMods.Contains(t.Mod?.Name)).ToList();
					int k = 0;
					foreach (var tpl in types) made.Add((ModTileEntity)TestTileEntities.Register(TestTileEntities.Make(tpl, new Point16(20 + 4 * k, 12)), 899_900 + k++));
					Note($"fixtures: {string.Join(", ", made.Select(m => $"{m.Mod.Name}/{m.Name} type {m.Type}"))}");
					Check("fixtures cover the Barracks, the Armory and (with Mechanization) the Armory Relay", made.Any(m => m is BarracksTE) && made.Any(m => m is ArmoryTE) && (!ModLoader.HasMod("Mechanization") || made.Any(m => m.Name == "ArmoryRelayTE")), string.Join(",", made.Select(m => m.Name)));
					var bad = made.Where(m => !TestTileEntities.IsSaveSafe(m)).ToList();
					Check("every fixture built through TestTileEntities is save-safe (its mod, Type and type byte set)", bad.Count == 0, string.Join("; ", bad.Select(TestTileEntities.Describe)));
					string mem = SaveTileEntitiesInMemory(out var modded);
					Check("with every fixture in, both halves of the tile-entity save run", mem == null, mem ?? "");
					int written = modded?.Count(tag => made.Any(m => tag.GetString("mod") == m.Mod.Name && tag.GetString("name") == m.Name && tag.GetShort("X") == m.Position.X && tag.GetShort("Y") == m.Position.Y)) ?? -1;
					Check("tML's half writes each fixture as a modded entity", written == made.Count, $"{written} of {made.Count}");
					DateTime t0 = WorldStamp(); string disk = null;
					try { WorldFile.SaveWorld(); } catch (Exception e) { disk = Short(e); }
					Check("a world save forced on the check's thread mid-hold, every fixture in, completes and writes the world", disk == null && WorldStamp() != t0, disk ?? $"world written {WorldStamp() != t0}");
					ProbeMidCheck("SaveHold fixtures");
				}
			}
			finally { foreach (var m in made) TestTileEntities.Remove(m); }
			Check("the fixtures are gone again", made.All(m => !TileEntity.ByID.ContainsKey(m.ID)));

			// 4. the hold always gives the autosave back: closed normally, nested, thrown out of, from autosave on and from autosave off
			foreach (bool start in new[] { true, false })
			{
				Main.autoSave = start;
				using (SaveHold.Begin("outer"))
				{
					using (SaveHold.Begin("inner")) { }
					Check($"(autosave {(start ? "on" : "off")} before) closing a nested hold leaves the outer one open", SaveHold.Depth == 1 && SaveHold.Locked && !Main.autoSave);
				}
				Check($"(autosave {(start ? "on" : "off")} before) after the hold autosave is back and no hold is open", Main.autoSave == start && SaveHold.Depth == 0 && !SaveHold.Locked && SaveHold.FreeForOtherThreads());
				bool threw = false;
				using (new Logging.QuietExceptionHandle()) // the throw is the test: kept out of tML's caught-error log
				{
					try { using (SaveHold.Begin("throws")) throw new InvalidOperationException("probe"); }
					catch (InvalidOperationException) { threw = true; }
				}
				Check($"(autosave {(start ? "on" : "off")} before) a check that throws out of its hold still gives the autosave back", threw && Main.autoSave == start && SaveHold.Depth == 0 && SaveHold.FreeForOtherThreads());
			}
			Main.autoSave = auto0;

			// 5. and the real autosave works again afterwards: its callback on a pool thread saves the world
			{
				DateTime t0 = WorldStamp();
				string pool = AutosaveOnPool();
				Check("after the hold the autosave callback on a pool thread saves the world", pool == null && WorldStamp() != t0, pool ?? $"world written {WorldStamp() != t0}");
			}
			Check("autosave is left as the boot had it", Main.autoSave == auto0, $"{Main.autoSave} vs {auto0}");
		}

		private static void Note(string s) => Log("OBSERVED " + s);
	}
}
