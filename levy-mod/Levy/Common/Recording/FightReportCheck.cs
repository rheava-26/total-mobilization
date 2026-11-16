using System;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader;

namespace Levy.Common.Recording
{
	/// <summary>Boot self-check of the fight analyzer ("[FightReport] self-check pass=N fail=M", every load): the pure checks of <see cref="FightReportSelfTest"/>, then the report job on a real gzip recording (the text file next to it, the chat queue, the setting).</summary>
	public class FightReportCheck : ModSystem
	{
		public override void PostSetupRecipes()
		{
			var fails = new List<string>();
			int pass = FightReportSelfTest.Run(fails);
			void Chk(bool c, string w) { if (c) pass++; else fails.Add(w); }
			string tmp = null;
			try
			{
				tmp = Path.Combine(Path.GetTempPath(), "levy_fightreport_job_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp);
				var s0 = FightRecReader.ReadLines(FightReportSelfTest.Build(new FightReportSelfTest.Script()));
				FightHistory.Append(tmp, FightHistory.FromStats(s0, "2026-11-16_11-18_Plantera_lost.levyrec"));
				// ---- the report job, on a real gzip recording
				string rec = Path.Combine(tmp, "2026-11-16_12-00_Plantera_lost.levyrec");
				var w = new RecWriter(rec, true, 0); foreach (string line in FightReportSelfTest.Build(new FightReportSelfTest.Script())) w.WriteLine(line); w.Close(null, null, true); // synchronous close
				FightRecAuto.ResetMessages();
				FightRecPost.Run(rec, tmp, true); FightRecAuto.Pump();
				string txt = Path.Combine(tmp, "2026-11-16_12-00_Plantera_lost.txt");
				Chk(File.Exists(txt) && File.ReadAllText(txt).Contains("Iron Francisca"), "the report file is written next to the recording with the same base name");
				Chk(FightRecAuto.Said.Count == 4 && FightRecAuto.Said[0].StartsWith("Plantera, lost at 2%"), "the summary reaches the chat queue (" + FightRecAuto.Said.Count + " lines)");
				Chk(FightRecAuto.Said.Count == 4 && FightRecAuto.Said[3].StartsWith("Your best against Plantera: 3,570"), "and compares with the earlier fight in the same folder");
				FightRecAuto.ResetMessages();
				FightRecPost.Run(rec, tmp, false); FightRecAuto.Pump();
				Chk(FightRecAuto.Said.Count == 0, "with the chat summary off nothing reaches the chat");
				var cf = typeof(FightRecConfig).GetField("PostFightSummary"); var cw = typeof(FightRecConfig).GetField("LineBreakingWarning");
				Chk(cf != null && (bool)((System.ComponentModel.DefaultValueAttribute)Attribute.GetCustomAttribute(cf, typeof(System.ComponentModel.DefaultValueAttribute))).Value, "the chat summary defaults to on");
				Chk(cw != null && (bool)((System.ComponentModel.DefaultValueAttribute)Attribute.GetCustomAttribute(cw, typeof(System.ComponentModel.DefaultValueAttribute))).Value, "the line-breaking warning defaults to on");
				Chk(FightRecFormat.Version >= 10, "the recording format is 10 or later");
			}
			catch (Exception e) { fails.Add("job threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim()); }
			finally { FightRecAuto.ResetMessages(); try { if (tmp != null) Directory.Delete(tmp, true); } catch { } }
			if (fails.Count == 0) Mod.Logger.Info($"[FightReport] self-check pass={pass} fail=0");
			else { Mod.Logger.Warn($"[FightReport] self-check pass={pass} fail={fails.Count}"); foreach (var f in fails) Mod.Logger.Warn("[FightReport] FAIL " + f); }
		}
	}
}
