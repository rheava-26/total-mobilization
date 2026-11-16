using System;
using System.Collections.Generic;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Decides when the army's line is breaking. Fed the army about once a second while a boss lives: how many levies stand near the commander, how many of them are shaken, routed or deserting, and how many levies have fallen so far.
	/// Two signals, either one fires it, once per fight:
	///  morale: a third of the line is broken for three seconds running;
	///  attrition: four tenths of the army's best strength has fallen inside twenty seconds (at least five levies).
	/// An army whose best strength is under <see cref="MinPeak"/> never breaks (a handful of levies has no line). Pure data: the live watcher in the game and the recording analyzer feed the same detector, so the "broke at" time
	/// in the report is the moment the warning came. Deaths are counted, not the drop in headcount: a levy that only lags behind the commander out of view is not a casualty.
	/// The numbers come from the 46 recordings of the commander's own fights (tools/recreport): the fights she won with the army standing never reach either signal; her Plantera losses with an army reach morale 16 to 25 seconds in,
	/// the Destroyer and Skeletron Prime losses 14 and 15 seconds in, all well before the army is gone.
	/// </summary>
	internal sealed class LineBreakDetector
	{
		public const int MinPeak = 8;
		public const int MinMorale = 6;
		public const double BrokenShare = 0.35;
		public const int BrokenHoldTicks = 180;
		public const double LossShare = 0.40;
		public const int LossWindowTicks = 1200;
		public const int MinLoss = 5;

		private int _peak, _brokenSince = -1;
		private readonly List<(int tick, int deaths)> _win = new();
		public bool Fired { get; private set; }
		public int FiredTick { get; private set; } = -1;
		public string Why { get; private set; }
		public int Peak => _peak;

		public void Reset() { _peak = 0; _brokenSince = -1; _win.Clear(); Fired = false; FiredTick = -1; Why = null; }

		/// <summary>One reading. Returns true exactly once, on the reading that breaks the line.</summary>
		public bool Feed(int tick, int n, int broken, int deathsTotal)
		{
			if (Fired) return false;
			if (n > _peak) _peak = n;
			_win.Add((tick, deathsTotal));
			while (_win.Count > 1 && _win[1].tick <= tick - LossWindowTicks) _win.RemoveAt(0);
			if (_peak < MinPeak) return false;

			bool morale = n >= MinMorale && broken >= Math.Max(3, (int)Math.Ceiling(BrokenShare * n));
			if (morale) { if (_brokenSince < 0) _brokenSince = tick; } else _brokenSince = -1;
			if (morale && tick - _brokenSince >= BrokenHoldTicks) return Fire(tick, "morale");

			int lost = deathsTotal - _win[0].deaths;
			if (lost >= Math.Max(MinLoss, (int)Math.Ceiling(LossShare * _peak))) return Fire(tick, "attrition");
			return false;
		}

		private bool Fire(int tick, string why) { Fired = true; FiredTick = tick; Why = why; return true; }

		/// <summary>Replays a recorded army series (one point a second, boss present) and returns the tick the line broke, or -1.</summary>
		public static int Replay(IEnumerable<ArmyPoint> pts, out string why)
		{
			var d = new LineBreakDetector(); why = null;
			foreach (var p in pts)
			{
				if (p.BossFrac < 0) continue; // no boss in the sample: nothing to break against
				if (d.Feed(p.Sec * 60, p.N, p.Broken, p.Deaths)) { why = d.Why; return d.FiredTick; }
			}
			return -1;
		}
	}
}
