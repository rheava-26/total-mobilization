using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Perf
{
	/// <summary>/levyperf: the last minute of the performance analyzer in plain words. "/levyperf fight" says what it made of the last boss fight, "/levyperf file" where the log is.</summary>
	public class PerfCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "levyperf";
		public override string Usage => "/levyperf [fight|file]";
		public override string Description => "Shows where the last minute's time went: frames and updates per second, the costs by mod, and Levy's own parts.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			try
			{
				string a = args.Length > 0 ? args[0].ToLowerInvariant() : "";
				if (PerfAnalyzer.DeadWhy != null) { caller.Reply("The performance analyzer turned itself off after an error: " + PerfAnalyzer.DeadWhy, Color.OrangeRed); return; }
				if (!PerfAnalyzer.TimingOn) { caller.Reply("The performance analyzer is off. Turn on Record a performance log in the Levy: Performance settings.", Color.LightGray); return; }
				if (a == "file") { caller.Reply(PerfAnalyzer.LastLogFile != null ? "The log is " + PerfAnalyzer.LastLogFile : "Nothing has been written to the log yet. A window is written every eight seconds.", Color.LightGray); return; }
				if (a == "fight")
				{
					var v = PerfAnalyzer.LastVerdict;
					if (v == null) { caller.Reply("No boss fight has been judged yet.", Color.LightGray); return; }
					caller.Reply($"{v.Boss}: {v.FightSecs:0} seconds, {(v.Laggy ? "laggy" : "smooth")}. Median {v.MedianFps:0} fps and {v.MedianUps:0} updates per second; {v.LaggyShare * 100:0}% of the fight was under 40 fps or 55 updates per second.", Color.LightGoldenrodYellow);
					if (v.Laggy) foreach (string d in v.Detail) caller.Reply(d, Color.LightGoldenrodYellow);
					if (!v.Laggy && v.Weapons.Count > 0) foreach (string d in PerfAnalysis.WeaponLines(new PerfSpan { Weapons = v.Weapons, WeaponAttribMs = v.WeaponAttribMs, Levies = v.Levies }, "this fight")) caller.Reply(d, Color.LightGoldenrodYellow);
					if (v.Laggy && v.ByMod.Count > 0) caller.Reply("Cost by mod: " + string.Join(", ", v.ByMod.Take(5).Select(kv => $"{(kv.Key == "Terraria" ? "base game" : kv.Key)} {kv.Value / Math.Max(1e-6, v.TotalMsPerSec) * 100:0}%")) + ".", Color.LightGoldenrodYellow);
					return;
				}
				var span = PerfSpan.Of(PerfAnalyzer.LastSeconds(60));
				foreach (string l in PerfAnalysis.Report(span)) caller.Reply(l, Color.LightGoldenrodYellow);
			}
			catch (Exception e) { caller.Reply("The performance report failed: " + e.Message, Color.OrangeRed); }
		}
	}
}
