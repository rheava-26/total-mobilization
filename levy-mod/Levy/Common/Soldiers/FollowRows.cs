using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Movement;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// FOLLOW ROWS. The standing rows around a commander that a following army can spread over: the commander's own row, every run of platform four or more tiles wide in a vertical band around them, and the ground
	/// under them. One scan per commander is shared by every follower and refreshed every <see cref="RebuildTicks"/> ticks (never a scan per levy). A row is a feet-tile y and its runs of standable columns.
	/// Each row hands out numbered lanes (offsets from the commander, alternating sides); a levy claims one lane of one row and keeps it, so two levies never take the same spot.
	/// </summary>
	public sealed class FollowRowSet
	{
		public const int MaxRows = 9, MaxRuns = 8, MaxLanes = 40;
		public sealed class Row
		{
			public int Y, X0, X1, Width; public bool InBand;
			public readonly int[] RunA = new int[MaxRuns], RunB = new int[MaxRuns]; public int Runs;
			public readonly int[] OwnerNpc = new int[MaxLanes], OwnerId = new int[MaxLanes];
			public bool Has(int x) { for (int i = 0; i < Runs; i++) if (x >= RunA[i] && x <= RunB[i]) return true; return false; }
		}
		public readonly Row[] Rows = new Row[MaxRows]; public int Count; public int CmdY, CmdX, CmdRow = -1; public uint Built; public int LastGroundTick = -100000; public bool Valid;
		public int IndexOfY(int y) { for (int i = 0; i < Count; i++) if (Rows[i].Y == y) return i; return -1; }
		public FollowRowSet() { for (int i = 0; i < MaxRows; i++) Rows[i] = new Row(); }
	}

	public static class FollowRows
	{
		public const int RebuildTicks = 45, ScanHalfWidth = 46, BandUp = 22, BandDown = 22, BandSlack = 4, MinRun = 4, SameRowTiles = 4;
		public static long Rebuilds, TilesRead; public static double RebuildMs; public static int MaxRowsSeen;
		private static readonly FollowRowSet[] _sets = new FollowRowSet[Main.maxPlayers];
		private static readonly List<int> _candY = new List<int>(64);
		private static readonly int[] _runA = new int[128], _runB = new int[128], _runY = new int[128]; private static readonly bool[] _runPlat = new bool[128];
		private static int _runN;

		public static void Reset() { for (int i = 0; i < _sets.Length; i++) _sets[i] = null; Rebuilds = TilesRead = 0; RebuildMs = 0; MaxRowsSeen = 0; }

		/// <summary>The row set around a commander, or null when they are not standing (flying, falling for a while) or no scan has ever found footing.</summary>
		public static FollowRowSet Get(Player cmd, int now)
		{
			int pi = cmd.whoAmI; if (pi < 0 || pi >= _sets.Length) return null;
			var set = _sets[pi] ??= new FollowRowSet();
			bool stands = cmd.velocity.Y == 0f && !cmd.dead && cmd.gravDir > 0f;
			Point feet = TileNav.FeetTile(cmd.Bottom);
			if (stands && TileNav.IsFloor(feet.X, feet.Y + 1)) { set.LastGroundTick = now; if (set.CmdY != feet.Y) set.Built = 0; set.CmdY = feet.Y; }
			set.CmdX = feet.X;
			if (now - set.LastGroundTick > 240) { set.Valid = false; return null; }
			if (!set.Valid || (uint)now - set.Built >= (uint)RebuildTicks) Rebuild(set, now);
			return set.Valid ? set : null;
		}

		/// <summary>A world x (px) pulled to the nearest place a body fits on the commander's row (the run nearest to it, a tile in from each end); unchanged when the row has no runs or x is already on one.</summary>
		public static float ClampToRun(FollowRowSet set, float xPx)
		{
			if (set == null || set.CmdRow < 0) return xPx;
			var row = set.Rows[set.CmdRow]; float xt = xPx / 16f, best = xt, bd = float.MaxValue;
			for (int i = 0; i < row.Runs; i++)
			{
				float lo = row.RunA[i] + 0.9f, hi = row.RunB[i] + 1f - 0.9f; if (hi < lo) { lo = hi = (row.RunA[i] + row.RunB[i] + 1) * 0.5f; }
				float c = Math.Clamp(xt, lo, hi), d = Math.Abs(c - xt);
				if (d < bd) { bd = d; best = c; }
			}
			return best * 16f;
		}

		private static void AddRun(int y, int a, int b, bool plat) { if (_runN < _runA.Length) { _runA[_runN] = a; _runB[_runN] = b; _runY[_runN] = y; _runPlat[_runN] = plat; _runN++; } }

		private static void Rebuild(FollowRowSet set, int now)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try { RebuildBody(set, now); }
			finally { RebuildMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
		}

		private static void RebuildBody(FollowRowSet set, int now)
		{
			Rebuilds++;
			set.Built = (uint)now;
			int cy = set.CmdY, cx = set.CmdX;
			// remember the claims by row y, then rebuild the row list around the commander
			var keep = new FollowRowSet.Row[FollowRowSet.MaxRows]; int keepN = 0;
			for (int i = 0; i < set.Count; i++) keep[keepN++] = set.Rows[i];
			_runN = 0;
			int x0 = cx - ScanHalfWidth, x1 = cx + ScanHalfWidth;
			for (int y = cy - BandUp - BandSlack; y <= cy + BandDown + BandSlack; y++)
			{
				int start = int.MinValue; int platCols = 0, cols = 0;
				for (int x = x0; x <= x1 + 1; x++)
				{
					bool st = x <= x1 && TileNav.InBounds(x, y) && TileNav.IsStandable(x, y);
					if (x <= x1) TilesRead++;
					if (st) { if (start == int.MinValue) { start = x; platCols = 0; cols = 0; } cols++; if (TileNav.IsPlatform(x, y + 1)) platCols++; }
					else if (start != int.MinValue)
					{
						if (cols >= MinRun || (y == cy && x - 1 >= cx && start <= cx)) AddRun(y, start, x - 1, platCols * 2 >= cols);
						start = int.MinValue;
					}
				}
			}
			// which rows are worth standing on: the commander's row, platform runs, and the first ground row at or below the commander
			bool underground = cy > Main.worldSurface;
			int groundY = int.MaxValue;
			for (int i = 0; i < _runN; i++) if (!_runPlat[i] && _runY[i] >= cy && _runY[i] < groundY && _runB[i] - _runA[i] + 1 >= MinRun) groundY = _runY[i];
			_candY.Clear();
			for (int i = 0; i < _runN; i++)
			{
				int y = _runY[i]; if (_candY.Contains(y)) continue;
				bool want = y == cy || _runPlat[i] || y == groundY;
				if (want) _candY.Add(y);
			}
			// order: commander row, rows kept from last time, then the widest
			int Width(int y) { int w = 0; for (int i = 0; i < _runN; i++) if (_runY[i] == y) w += _runB[i] - _runA[i] + 1; return w; }
			_candY.Sort((a, b) =>
			{
				int ka = a == cy ? 0 : (set.IndexOfY(a) >= 0 ? 1 : 2), kb = b == cy ? 0 : (set.IndexOfY(b) >= 0 ? 1 : 2);
				if (ka != kb) return ka.CompareTo(kb);
				return Width(b).CompareTo(Width(a));
			});
			var fresh = new FollowRowSet.Row[FollowRowSet.MaxRows]; int n = 0;
			foreach (int y in _candY)
			{
				if (n >= FollowRowSet.MaxRows) break;
				int hx0 = int.MaxValue, hx1 = int.MinValue; for (int i = 0; i < _runN; i++) if (_runY[i] == y) { hx0 = Math.Min(hx0, _runA[i]); hx1 = Math.Max(hx1, _runB[i]); }
				bool dup = false;
				for (int j = 0; j < n; j++) if (Math.Abs(fresh[j].Y - y) < SameRowTiles && hx0 <= fresh[j].X1 && hx1 >= fresh[j].X0) { dup = true; break; }
				if (dup) continue;
				// reuse the old row object of this y so its lane claims survive the rebuild
				FollowRowSet.Row row = null; for (int i = 0; i < keepN; i++) if (keep[i] != null && keep[i].Y == y) { row = keep[i]; keep[i] = null; break; }
				row ??= new FollowRowSet.Row();
				row.Y = y; row.Runs = 0; row.Width = 0; row.X0 = hx0; row.X1 = hx1;
				for (int i = 0; i < _runN && row.Runs < FollowRowSet.MaxRuns; i++) if (_runY[i] == y) { row.RunA[row.Runs] = _runA[i]; row.RunB[row.Runs] = _runB[i]; row.Runs++; row.Width += _runB[i] - _runA[i] + 1; }
				row.InBand = y >= cy - BandUp && y <= cy + BandDown;
				fresh[n++] = row;
			}
			// drop rows that are not worth a levy's time: outside the band and not the commander's, no standing run
			set.Count = 0;
			for (int i = 0; i < n; i++) set.Rows[set.Count++] = fresh[i];
			set.CmdRow = set.IndexOfY(cy);
			MaxRowsSeen = Math.Max(MaxRowsSeen, set.Count);
			set.Valid = set.Count > 0;
			if (underground && CountPlatformRows(set) < 2 && set.Count > 0)
			{
				// natural cave floors are not rows: a levy could be sent to a chamber it cannot walk to
				int c = 0; for (int i = 0; i < set.Count; i++) if (set.Rows[i].Y == cy) set.Rows[c++] = set.Rows[i]; set.Count = c; set.CmdRow = c > 0 ? 0 : -1;
			}
		}

		private static int CountPlatformRows(FollowRowSet set)
		{
			int c = 0;
			for (int i = 0; i < set.Count; i++) { var r = set.Rows[i]; if (r.Y == set.CmdY) continue; if (TileNav.IsPlatform(r.RunA[0], r.Y + 1)) c++; }
			return c;
		}
	}

	/// <summary>Drops the shared row scans when a world loads or unloads, so a new world never reads the old one's rows.</summary>
	public class FollowRowsSystem : ModSystem
	{
		public override void OnWorldLoad() => FollowRows.Reset();
		public override void OnWorldUnload() => FollowRows.Reset();
	}
}
