using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;

namespace Levy.Common.Recording
{
	/// <summary>Where recordings go and how the folder is kept: picking and validating the folder, naming files, deleting the oldest when the folder is over its cap, recovering files a crash left half-written. Plain file work; none of it touches the game.</summary>
	internal static class FightRecFolder
	{
		public const string Ext = ".levyrec";
		/// <summary>Format 13 part files (see <see cref="RecParts"/>).</summary>
		public static string PartBase(string path, out int part) => RecParts.PartBase(path, out part);
		public static bool IsLaterPart(string path) => RecParts.IsLaterPart(path);
		public static List<string> Parts(string path) => RecParts.Parts(path);

		public static string DefaultDir => Path.Combine(Main.SavePath, "Levy", "Recordings");

		/// <summary>The folder to record into. <paramref name="configured"/> empty means the default. When the chosen folder can't be created or written, the default is tried and <paramref name="warning"/> says so; null when nothing works.</summary>
		public static string Resolve(string configured, string defaultDir, out string warning)
		{
			warning = null; string why = null;
			bool custom = !string.IsNullOrWhiteSpace(configured);
			string want = custom ? Expand(configured) : defaultDir;
			if (want != null && Probe(want, out why)) return want;
			if (custom && !(want != null && SamePath(want, defaultDir)))
			{
				warning = "Can't write to " + configured.Trim() + " (" + (why ?? "not a full folder path") + "). Recording to " + defaultDir + " instead.";
				if (Probe(defaultDir, out _)) return defaultDir;
				warning = "Can't write to " + configured.Trim() + " or " + defaultDir + ". Fight recording is off.";
				return null;
			}
			warning = "Can't write to " + defaultDir + " (" + (why ?? "not a full folder path") + "). Fight recording is off.";
			return null;
		}

		private static bool SamePath(string a, string b)
		{
			try { return string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase); }
			catch { return false; }
		}

		/// <summary>Quotes, %VARS% and a leading ~ are expanded; a path that is not absolute is rejected (null).</summary>
		public static string Expand(string s)
		{
			try
			{
				s = s.Trim().Trim('"');
				s = Environment.ExpandEnvironmentVariables(s);
				if (s == "~" || s.StartsWith("~/") || s.StartsWith("~\\")) s = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), s.Length > 2 ? s.Substring(2) : "");
				if (!Path.IsPathRooted(s) || s.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return null;
				return Path.GetFullPath(s);
			}
			catch { return null; }
		}

		/// <summary>Creates the folder when missing and proves it can be written by making and deleting a one-byte file.</summary>
		public static bool Probe(string dir, out string why)
		{
			why = null;
			try
			{
				// a plain file where a folder should be is the commonest mistake: say so without raising an exception
				for (string a = dir; !string.IsNullOrEmpty(a); a = Path.GetDirectoryName(a))
					if (File.Exists(a)) { why = "a file named " + Path.GetFileName(a) + " is in the way"; return false; }
				Directory.CreateDirectory(dir);
				string t = Path.Combine(dir, ".levy_write_test_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				File.WriteAllBytes(t, new byte[] { 1 });
				File.Delete(t);
				return true;
			}
			catch (Exception e) { why = e.Message; return false; }
		}

		/// <summary>A name that is safe in any file system and still readable: letters and digits kept, anything else a single underscore.</summary>
		public static string SafeName(string s, string fallback)
		{
			var sb = new StringBuilder();
			foreach (char c in s ?? "")
			{
				if (char.IsLetterOrDigit(c)) sb.Append(c);
				else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
			}
			string r = sb.ToString().Trim('_');
			if (r.Length > 40) r = r.Substring(0, 40).TrimEnd('_');
			return r.Length == 0 ? fallback : r;
		}

		/// <summary>YYYY-MM-DD_HH-MM_Boss_outcome.levyrec, with _2, _3 ... when that minute already has one.</summary>
		public static string FileName(string dir, DateTime when, string boss, string outcome)
		{
			string stem = when.ToString("yyyy-MM-dd_HH-mm") + "_" + SafeName(boss, "Fight") + "_" + SafeName(outcome, "ended");
			string p = Path.Combine(dir, stem + Ext); int n = 2;
			while (File.Exists(p) || File.Exists(p + ".part")) p = Path.Combine(dir, stem + "_" + n++ + Ext);
			return p;
		}

		/// <summary>Deletes the oldest .levyrec files until the folder's recordings total at most <paramref name="maxBytes"/>. <paramref name="keep"/> (the file just written) is never deleted. Returns how many went.</summary>
		public static int Enforce(string dir, long maxBytes, string keep, out long totalAfter)
		{
			totalAfter = 0; int deleted = 0;
			try
			{
				var files = new List<FileInfo>();
				foreach (string f in Directory.EnumerateFiles(dir, "*" + Ext)) files.Add(new FileInfo(f));
				long total = 0; foreach (var f in files) total += f.Length;
				files.Sort((a, b) => { int c = a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc); return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name); });
				foreach (var f in files)
				{
					if (total <= maxBytes) break;
					if (keep != null && (SamePath(f.FullName, keep) || SamePath(PartBase(f.FullName, out _), keep))) continue; // the recording just saved, every part of it
					try { long len = f.Length; f.Delete(); total -= len; deleted++; } catch { }
				}
				totalAfter = total;
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRec] folder cleanup failed: " + e.Message); }
			return deleted;
		}

		/// <summary>A crash leaves NAME.levyrec.part (readable up to its last flush). Gives each one a real name so the reader finds it. Returns how many.</summary>
		public static int RecoverParts(string dir)
		{
			int n = 0;
			try
			{
				foreach (string f in Directory.EnumerateFiles(dir, "*" + Ext + ".part"))
				{
					try
					{
						if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) < TimeSpan.FromMinutes(2)) continue; // still being written by a live recorder
						string stem = f.Substring(0, f.Length - (Ext + ".part").Length);
						// a later part keeps its _partN at the end, so it still joins the first part's crashed name
						var pm = System.Text.RegularExpressions.Regex.Match(stem, @"^(.*)_part(\d+)$");
						string to = pm.Success ? pm.Groups[1].Value + "_crashed_part" + pm.Groups[2].Value + Ext : stem + "_crashed" + Ext; int k = 2;
						while (File.Exists(to)) to = pm.Success ? pm.Groups[1].Value + "_crashed_" + k++ + "_part" + pm.Groups[2].Value + Ext : stem + "_crashed_" + k++ + Ext;
						File.Move(f, to); n++;
					}
					catch { }
				}
			}
			catch { }
			return n;
		}
	}
}
