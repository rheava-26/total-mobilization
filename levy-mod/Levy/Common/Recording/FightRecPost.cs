using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;

namespace Levy.Common.Recording
{
	/// <summary>
	/// After a boss fight: reads the finished recording back with the analyzer (the same code the command-line tool runs), writes the full report next to it (same base name, .txt), adds the fight to the folder's history
	/// (fightreport.index), and posts the short summary to chat when the setting is on. Runs on a pool thread once the writer has closed and renamed the file, so the game never waits for it; the chat lines come back through the
	/// same queue every other recorder message uses and are shown on the game thread. Nothing here touches the game's state.
	/// </summary>
	internal static class FightRecPost
	{
		/// <summary>Self-check: write the report for a recording made on a dedicated test server (normally only a client's own fights get one).</summary>
		public static bool TestForce;
		/// <summary>Self-check: stands in for the config's "Post-fight summary in chat".</summary>
		public static bool? TestSummary;
		public static readonly Color SummaryColor = new Color(255, 236, 178);

		private static readonly object Gate = new();
		private static int _pending;
		public static string LastReport, LastError; public static List<string> LastLines = new(); public static FightStats LastStats;

		public static bool SummaryOn()
		{
			if (TestSummary.HasValue) return TestSummary.Value;
			try { return FightRecConfig.Get?.PostFightSummary ?? true; } catch (Exception) { return true; }
		}

		/// <summary>Queues the report job for a finished recording.</summary>
		public static void Begin(string path, string dir, bool chat)
		{
			Interlocked.Increment(ref _pending);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try { Run(path, dir, chat); }
				catch (Exception e) { LastError = e.GetType().Name + ": " + e.Message; Levy.Instance?.Logger.Warn("[FightRec] report failed for " + path + ": " + e); }
				finally { Interlocked.Decrement(ref _pending); }
			});
		}

		/// <summary>Waits until no report job is queued or running (self-check).</summary>
		public static bool WaitIdle(int ms)
		{
			var t0 = DateTime.UtcNow;
			while (Volatile.Read(ref _pending) > 0 && (DateTime.UtcNow - t0).TotalMilliseconds < ms) Thread.Sleep(10);
			return Volatile.Read(ref _pending) == 0;
		}

		/// <summary>The job itself (also called directly by the self-check).</summary>
		internal static void Run(string path, string dir, bool chat)
		{
			lock (Gate)
			{
				var st = FightRecReader.Read(path, true);
				var prior = FightHistory.Load(dir, st.Boss, path, 8, m => Levy.Instance?.Logger.Info("[FightRec] history: " + m));
				string txt = FightReport.Full(st, prior);
				string rp = Path.ChangeExtension(path, ".txt");
				File.WriteAllText(rp, txt, new UTF8Encoding(false));
				FightHistory.Append(dir, FightHistory.FromStats(st, Path.GetFileName(path)));
				var lines = FightReport.Summary(st, prior);
				LastReport = rp; LastLines = lines; LastStats = st; LastError = null;
				Levy.Instance?.Logger.Info("[FightRec] report " + rp + " (" + (txt.Length / 1024) + " KB); " + (lines.Count > 0 ? string.Join(" | ", lines) : "no summary for a " + st.Result + " fight"));
				if (chat) foreach (string l in lines) FightRecAuto.PostLine(l, SummaryColor);
			}
		}
	}
}
