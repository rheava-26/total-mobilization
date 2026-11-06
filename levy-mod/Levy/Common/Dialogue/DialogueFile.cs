using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// The editable dialogue file. Every spoken levy and officer line lives in ONE text file, Dialogue/en-US.txt inside the mod (format documented at its top).
	/// To rewrite lines without rebuilding the mod, export that file to the save folder with the /levydialogue command, edit the copy, and reload:
	/// <c>Terraria/tModLoader/Levy/Dialogue/&lt;culture&gt;.txt</c> (under <see cref="Main.SavePath"/>) is read in preference to the packed file whenever it exists.
	/// Same format, same parser, same placeholders; a malformed line is logged with its line number and skipped, never fatal.
	/// </summary>
	public static class DialogueFile
	{
		/// <summary>Folder the editable files live in.</summary>
		public static string EditableDir => Path.Combine(Main.SavePath, "Levy", "Dialogue");

		public static string EditablePath(string culture) => Path.Combine(EditableDir, culture + ".txt");

		/// <summary>The editable file that would be used for this culture (its own, else en-US), or null when none exists.</summary>
		public static string FindEditable(string culture)
		{
			try
			{
				if (!string.IsNullOrEmpty(culture) && File.Exists(EditablePath(culture))) return EditablePath(culture);
				if (File.Exists(EditablePath("en-US"))) return EditablePath("en-US");
			}
			catch (Exception) { /* no save folder yet: there is no editable file */ }
			return null;
		}

		/// <summary>Reads a dialogue text file (UTF-8, with or without a byte-order mark).</summary>
		public static string ReadTextFile(string path) => new UTF8Encoding(false).GetString(File.ReadAllBytes(path)).TrimStart((char)0xFEFF);

		/// <summary>Loads the editable file into <see cref="DialogueTable"/> when one exists. False (table untouched) when there is none or it cannot be read.</summary>
		public static bool TryLoadEditable(Mod mod, string culture)
		{
			string path = FindEditable(culture);
			if (path == null) return false;
			string text;
			try { text = ReadTextFile(path); }
			catch (Exception e) { mod.Logger.Warn("Levy: could not read the editable dialogue file " + path + ": " + e.Message); return false; }
			if (text.Trim().Length == 0) { mod.Logger.Warn("Levy: the editable dialogue file " + path + " is empty; using the mod's own."); return false; }
			DialogueTable.LoadedCulture = Path.GetFileNameWithoutExtension(path);
			DialogueTable.LoadedFrom = "editable file " + path;
			int bad = DialogueTable.Parse(mod, path, text);
			mod.Logger.Info($"Levy: dialogue table (EDITABLE FILE {path}): {DialogueTable.LineCount} lines in {DialogueTable.Lines.Count} pools" + (bad > 0 ? $", {bad} bad lines skipped." : "."));
			return true;
		}

		/// <summary>Writes the mod's own dialogue file to the editable location. Refuses to overwrite an existing copy unless <paramref name="force"/> (the old one is kept as .bak).</summary>
		public static string Export(Mod mod, string culture, bool force, out string error)
		{
			error = null;
			string packed = mod.FileExists("Dialogue/" + culture + ".txt") ? "Dialogue/" + culture + ".txt" : "Dialogue/en-US.txt";
			string target = EditablePath(culture);
			try
			{
				if (File.Exists(target))
				{
					if (!force) { error = "An edited file already exists at " + target + ". Use '/levydialogue export force' to replace it (the old copy is kept as .bak)."; return null; }
					File.Copy(target, target + ".bak", true);
				}
				Directory.CreateDirectory(EditableDir);
				File.WriteAllBytes(target, mod.GetFileBytes(packed));
				return target;
			}
			catch (Exception e) { error = "Could not export: " + e.Message; return null; }
		}

		/// <summary>Serializes a table back to the file format, grouped by key in table order. Placeholders and text are written exactly as parsed.</summary>
		public static string Serialize(Dictionary<string, List<DialogueLine>> table)
		{
			var sb = new StringBuilder(64 * 1024);
			foreach (var kv in table)
			{
				foreach (DialogueLine l in kv.Value) sb.Append(kv.Key).Append(" | ").Append(l.Filter).Append(" | ").Append(l.Text).Append('\n');
			}
			return sb.ToString();
		}

		/// <summary>Flat "key|filter|text" list of a table, for comparing two tables line for line.</summary>
		public static List<string> Flatten(Dictionary<string, List<DialogueLine>> table)
		{
			var list = new List<string>();
			foreach (var kv in table) foreach (DialogueLine l in kv.Value) list.Add(kv.Key.ToUpperInvariant() + "|" + l.Filter + "|" + l.Text);
			list.Sort(StringComparer.Ordinal);
			return list;
		}
	}

	/// <summary>/levydialogue export|reload|path - manage the editable dialogue file.</summary>
	public class DialogueCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat | CommandType.Console;
		public override string Command => "levydialogue";
		public override string Usage => "/levydialogue export [force] | reload | path";
		public override string Description => "Export the levy and officer dialogue file to the save folder, reload an edited copy, or show where it is.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "path";
			string culture = DialogueTable.LoadedCulture.Length > 0 ? DialogueTable.LoadedCulture : "en-US";
			if (sub == "export")
			{
				string path = DialogueFile.Export(Mod, culture, args.Length > 1 && args[1].ToLowerInvariant() == "force", out string err);
				caller.Reply(path != null ? "Exported the dialogue file to " + path + ". Edit it, then use /levydialogue reload." : err);
			}
			else if (sub == "reload")
			{
				DialogueTable.Load(Mod, culture);
				DialogueSystem.ResetRuntime();
				caller.Reply($"Dialogue reloaded from the {DialogueTable.LoadedFrom}: {DialogueTable.LineCount} lines in {DialogueTable.Lines.Count} groups.");
			}
			else
			{
				string ed = DialogueFile.FindEditable(culture);
				caller.Reply(ed != null ? "Using the editable file " + ed : "No edited file. Using the mod's own. '/levydialogue export' copies it to " + DialogueFile.EditablePath(culture));
			}
		}
	}
}
