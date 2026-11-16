using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Levy.Common.Perf
{
	/// <summary>
	/// The rolling performance log on disk: plain JSON lines in a "perf" folder inside the recordings folder (so it syncs with it), one file a day (levyperf-2026-11-16.jsonl, then _2, _3 when a file passes its size),
	/// old files removed by age and by total size. Plain file work, no game types: the self-check runs it on a temp folder.
	/// </summary>
	internal static class PerfLogFile
	{
		public const string Prefix = "levyperf-", Ext = ".jsonl";

		public static string Dir(string recordingsDir) => Path.Combine(recordingsDir, "perf");

		/// <summary>The file lines go to on <paramref name="day"/>: the day's file, or its next numbered one once the current file has reached <paramref name="maxFileBytes"/>.</summary>
		public static string Current(string dir, DateTime day, long maxFileBytes)
		{
			string stem = Path.Combine(dir, Prefix + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
			string p = stem + Ext; int n = 2;
			while (File.Exists(p) && new FileInfo(p).Length >= maxFileBytes) p = stem + "_" + n++ + Ext;
			return p;
		}

		/// <summary>Appends lines (each ends with a newline) to the current file. Returns the file written. Throws on an IO error: the caller decides what that means.</summary>
		public static string Append(string dir, DateTime day, IEnumerable<string> lines, long maxFileBytes)
		{
			Directory.CreateDirectory(dir);
			string path = Current(dir, day, maxFileBytes);
			var sb = new StringBuilder();
			foreach (string l in lines) sb.Append(l).Append('\n');
			if (sb.Length == 0) return path;
			using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
			{
				byte[] b = new UTF8Encoding(false).GetBytes(sb.ToString());
				fs.Write(b, 0, b.Length); fs.Flush(true);
			}
			return path;
		}

		/// <summary>Removes log files older than <paramref name="keepDays"/> (by the date in the name), then the oldest ones until the folder is at most <paramref name="maxTotalBytes"/>. The file named by <paramref name="keep"/> is never removed. Returns how many went.</summary>
		public static int Enforce(string dir, int keepDays, long maxTotalBytes, DateTime today, string keep, out long totalAfter)
		{
			totalAfter = 0; int removed = 0;
			if (!Directory.Exists(dir)) return 0;
			var files = new List<(string path, DateTime day, long len)>();
			foreach (string f in Directory.EnumerateFiles(dir, Prefix + "*" + Ext))
			{
				var fi = new FileInfo(f); DateTime day = DayOf(fi.Name) ?? fi.LastWriteTime.Date;
				files.Add((f, day, fi.Length));
			}
			files.Sort((a, b) => { int c = a.day.CompareTo(b.day); return c != 0 ? c : string.CompareOrdinal(a.path, b.path); });
			long total = 0; foreach (var f in files) total += f.len;
			DateTime cutoff = today.Date.AddDays(-(Math.Max(1, keepDays) - 1));
			foreach (var f in files.ToArray())
			{
				bool isKeep = keep != null && string.Equals(Path.GetFullPath(f.path), Path.GetFullPath(keep), StringComparison.Ordinal);
				if (isKeep) continue;
				if (f.day < cutoff || total > maxTotalBytes)
				{
					try { File.Delete(f.path); total -= f.len; removed++; files.Remove(f); } catch (Exception) { }
				}
			}
			totalAfter = total;
			return removed;
		}

		/// <summary>The date in "levyperf-2026-11-16_2.jsonl" (null when the name has none).</summary>
		public static DateTime? DayOf(string name)
		{
			if (!name.StartsWith(Prefix, StringComparison.Ordinal) || name.Length < Prefix.Length + 10) return null;
			return DateTime.TryParseExact(name.Substring(Prefix.Length, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
		}

		/// <summary>Reads every window out of the log files of a folder, oldest first (the tool and the self-check use it).</summary>
		public static List<PerfWindow> ReadAll(string dir)
		{
			var o = new List<PerfWindow>();
			if (!Directory.Exists(dir)) return o;
			var files = new List<string>(Directory.EnumerateFiles(dir, Prefix + "*" + Ext)); files.Sort(StringComparer.Ordinal);
			foreach (string f in files)
				using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var rd = new StreamReader(fs))
				{
					string l;
					while ((l = rd.ReadLine()) != null) { var w = PerfWindow.FromJson(l); if (w != null) o.Add(w); }
				}
			return o;
		}
	}
}
