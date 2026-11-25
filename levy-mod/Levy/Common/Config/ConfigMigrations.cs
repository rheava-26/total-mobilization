using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Levy.Common.Config
{
	/// <summary>
	/// One-time moves of a saved config value to a new default. tModLoader saves every field to the player's config file, so a player who never touched a setting still holds the OLD default there and a changed default never reaches them.
	/// A migration moves the saved value only while it still equals the old default, then records its key in a small marker file next to the config files (not inside any ModConfig: tModLoader's settings screen saves a clone
	/// of the config, which would drop a marker kept in it). Once recorded it never runs again, so a value chosen later is never overridden. The marker is written only after the new value was saved to disk.
	/// </summary>
	public static class ConfigMigrations
	{
		/// <summary>Marker file: one migration key per line.</summary>
		public static string MarkerPath => Path.Combine(Main.SavePath, "ModConfigs", "Levy_ConfigMigrations.txt");

		public static bool IsDone(string key, string markerPath = null)
		{
			try { string p = markerPath ?? MarkerPath; if (!File.Exists(p)) return false; foreach (string l in File.ReadAllLines(p)) if (l.Trim() == key) return true; }
			catch (Exception) { }
			return false;
		}

		public static void MarkDone(string key, string markerPath = null)
		{
			try { string p = markerPath ?? MarkerPath; Directory.CreateDirectory(Path.GetDirectoryName(p)); File.AppendAllText(p, key + "\n"); }
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: could not record config migration " + key + ": " + e.Message); }
		}

		/// <summary>Migrations whose new value is live but not yet on disk (the save failed): the next run retries the save instead of judging the value.</summary>
		private static readonly System.Collections.Generic.HashSet<string> _unsaved = new();
		internal static void TestForget() => _unsaved.Clear();

		/// <summary>
		/// Runs the migration <paramref name="key"/> once. <paramref name="isOldDefault"/>: the saved value is still the old default. <paramref name="setNew"/>: puts the new default in the live config.
		/// <paramref name="save"/>: writes the config to disk, true when it worked. A value already changed by the player (or a fresh install, which holds the new default) is left alone and the migration is recorded.
		/// Returns true when the value was moved. When the save fails nothing is recorded, so the next run (or start) tries again.
		/// </summary>
		public static bool MoveOnce(string key, Func<bool> isOldDefault, Action setNew, Func<bool> save, string markerPath = null)
		{
			if (IsDone(key, markerPath)) return false;
			bool retry = _unsaved.Contains(key);
			if (!retry)
			{
				if (!isOldDefault()) { MarkDone(key, markerPath); return false; }
				setNew();
			}
			bool saved = false;
			try { saved = save(); } catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: config migration " + key + " could not save: " + e.Message); }
			if (!saved) { _unsaved.Add(key); return !retry; } // the live value is new for this session; the file still holds the old one, so no marker yet
			_unsaved.Remove(key);
			MarkDone(key, markerPath);
			return !retry;
		}

		/// <summary>Every migration Levy has. Safe to call again: each one runs once.</summary>
		public static void RunAll()
		{
			try
			{
				var c = LevyConfig.Get; if (c == null) return;
				// Chalice of the Blood God bleed-out: the default went from off to on (combatfix)
				MoveOnce("LevyConfig.SoldiersBleedOut:false>true", () => !c.SoldiersBleedOut, () => c.SoldiersBleedOut = true, () => SaveConfig(c));
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: config migrations failed: " + e.Message); }
		}

		/// <summary>Saves a config to its file the way tModLoader does: its public SaveChanges on the active instance, and if that does not report success, the internal ConfigManager.Save. True when either wrote the file.</summary>
		public static bool SaveConfig(ModConfig c)
		{
			if (c == null) return false;
			try
			{
				// a ServerSide config from a multiplayer client is the server's to save
				if (c.Mode == ConfigScope.ServerSide && Main.netMode == NetmodeID.MultiplayerClient) return false;
				var r = c.SaveChanges(null, null, true, false);
				if (r == ConfigSaveResult.Success) return true;
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: SaveChanges failed for " + c.Name + ": " + e.Message); }
			return SaveViaManager(c);
		}

		/// <summary>The internal ConfigManager.Save(ModConfig), by reflection. False when it is missing or throws.</summary>
		public static bool SaveViaManager(ModConfig c)
		{
			try
			{
				var m = typeof(ConfigManager).GetMethod("Save", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(ModConfig) }, null);
				if (m == null) { Levy.Instance?.Logger.Warn("Levy: ConfigManager.Save(ModConfig) not found; the config of " + c.Name + " is not written."); return false; }
				m.Invoke(null, new object[] { c });
				return true;
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("Levy: config save failed for " + c.Name + ": " + (e.InnerException?.Message ?? e.Message)); return false; }
		}
	}

	/// <summary>Runs the config migrations once the game is set up, and again when a world loads (a start where the first try could not save).</summary>
	public class ConfigMigrationSystem : ModSystem
	{
		public override void PostSetupContent() => ConfigMigrations.RunAll();
		public override void OnWorldLoad() { if (Main.netMode != NetmodeID.MultiplayerClient) ConfigMigrations.RunAll(); }
	}
}
