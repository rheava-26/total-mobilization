using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Coarse tile-grid A* with step/jump/fall/flight edges. A node is a tile that holds the feet of a standing body (three clear rows over a floor); with wings any clear cell is a node.
	/// <see cref="PathSearch"/> is the search itself (instance buffers, resumable in node-budget slices); <see cref="Pathfinder"/> keeps the short, immediate search every soldier uses
	/// (a window of PathWindowW x PathWindowH around the start) and <see cref="RoutePlanner"/> runs the same search over a whole route in slices. OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public static class Pathfinder
	{
		private static readonly PathSearch _local = new PathSearch(PathWindowW, PathWindowH);

		// counters (cheap; read by the travel lab and the perf log)
		public static long StatCalls, StatNodes, StatFails, StatTicks, StatMaxNodes;
		public static void StatReset() { StatCalls = StatNodes = StatFails = StatTicks = StatMaxNodes = 0; }
		public static string StatSummary() => $"calls={StatCalls} fails={StatFails} nodes={StatNodes} maxNodes={StatMaxNodes} ms={StatTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0}";

		/// <summary>Fills <paramref name="path"/> with world-space waypoints (first = next step). Returns false if unreachable within budget.</summary>
		public static bool FindPath(Vector2 fromWorld, Vector2 toWorld, MovementAbilities abilities, List<Vector2> path, int maxNodes = PathMaxNodes)
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			path.Clear();
			bool ok = false; int expanded = 0;
			if (_local.Begin(fromWorld, toWorld, abilities, centred: true, hWeight: 1f))
			{
				_local.Step(maxNodes);
				expanded = _local.Expanded;
				ok = _local.Status == PathSearch.State.Found && _local.BuildPath(path);
			}
			StatCalls++; StatTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; StatNodes += expanded; if (expanded > StatMaxNodes) StatMaxNodes = expanded; if (!ok) StatFails++;
			return ok;
		}
	}

	/// <summary>A* over the tile grid, resumable: <see cref="Begin"/>, then <see cref="Step"/> in node-budget slices until the status leaves Running. Buffers are reused between searches (single-threaded game update).</summary>
	public sealed class PathSearch
	{
		public enum State : byte { Idle, Running, Found, Partial, Failed }
		private const byte KindWalk = 0, KindStep = 1, KindFall = 2, KindJump = 3, KindFly = 4;

		private readonly int _maxW, _maxH;
		private readonly ushort[] _fl;   // consecutive flight steps on the best way to the cell (wings only run for WingTimeMax ticks)
		private readonly float[] _g; private readonly int[] _from; private readonly int[] _stamp; private readonly bool[] _closed; private readonly byte[] _kind;
		private int _gen = 1;
		private float[] _heapF; private int[] _heapI; private int _heapN;
		private int _ox, _oy, _w, _h;
		private readonly List<int> _rev = new();
		private Point _goalTile; private int _startIdx, _goalIdx, _bestIdx; private float _bestH, _hWeight;
		private int _jumpTiles, _maxFall, _gapTiles, _maxFlight; private bool _wings;

		public State Status { get; private set; }
		public int Expanded { get; private set; }
		/// <summary>True when the last search ended without reaching the goal but has a best-effort node (the closest it expanded to the goal).</summary>
		public bool HasPartial => _bestIdx >= 0 && _bestIdx != _startIdx;

		public PathSearch(int maxW, int maxH)
		{
			_maxW = maxW; _maxH = maxH; int cells = maxW * maxH;
			_fl = new ushort[cells]; _g = new float[cells]; _from = new int[cells]; _stamp = new int[cells]; _closed = new bool[cells]; _kind = new byte[cells];
			_heapF = new float[Math.Min(cells * 2, 1 << 16)]; _heapI = new int[_heapF.Length];
		}

		/// <summary>
		/// Starts a search. <paramref name="centred"/>: the window is centred on the start (the short search). Otherwise it is the bounding box of start and goal plus a margin, clipped to the buffer
		/// (a goal outside the window fails the search). <paramref name="hWeight"/> above 1 trades route optimality for far fewer expanded nodes.
		/// </summary>
		public bool Begin(Vector2 fromWorld, Vector2 toWorld, MovementAbilities abilities, bool centred, float hWeight, int padX = 60, int padUp = 90, int padDown = 150)
		{
			Status = State.Failed; Expanded = 0; _bestIdx = -1;
			abilities ??= new MovementAbilities();
			Point s = TileNav.FeetTile(fromWorld);
			Point t = TileNav.FeetTile(toWorld);
			if (!TileNav.InBounds(s.X, s.Y) || !TileNav.InBounds(t.X, t.Y)) return false;
			if (!TileNav.IsStandable(s.X, s.Y) && !TileNav.NearestStandable(s.X, s.Y, GoalSnapTiles, out s)) return false;
			if (!TileNav.IsStandable(t.X, t.Y) && !TileNav.NearestStandable(t.X, t.Y, GoalSnapTiles, out t)) return false;
			if (s == t) return false;
			if (centred) { _w = _maxW; _h = _maxH; _ox = s.X - _w / 2; _oy = s.Y - _h / 2; }
			else
			{
				int x0 = Math.Min(s.X, t.X) - padX, x1 = Math.Max(s.X, t.X) + padX;
				int y0 = Math.Min(s.Y, t.Y) - padUp, y1 = Math.Max(s.Y, t.Y) + padDown;
				_w = Math.Min(_maxW, x1 - x0 + 1); _h = Math.Min(_maxH, y1 - y0 + 1);
				_ox = x0; _oy = y0;
				if (x1 - x0 + 1 > _maxW) _ox = (s.X + t.X) / 2 - _w / 2; // too wide: centre on the span, the goal check below refuses
			}
			if (!InWindow(t.X, t.Y) || !InWindow(s.X, s.Y)) return false;
			_wings = abilities.HasWings;
			_jumpTiles = Math.Max(1, abilities.JumpTiles);
			_maxFall = _wings ? FallScanTiles : Math.Min(FallScanTiles, _jumpTiles + DropMarginTiles);
			_gapTiles = abilities.GapTiles;
			_maxFlight = TravelLegacy.On ? ushort.MaxValue : Math.Max(6, (int)(abilities.WingTimeMax * FlySpeed / 16f * FlightRangeShare)); // tiles of flight one tank of wing time covers, with room for the climb
			_hWeight = Math.Max(1f, hWeight);
			_goalTile = t;
			if (++_gen == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); _gen = 1; }
			_heapN = 0;
			_startIdx = Idx(s.X, s.Y); _goalIdx = Idx(t.X, t.Y);
			_bestIdx = _startIdx; _bestH = Heur(s.X, s.Y);
			Open(_startIdx, -1, 0f, Heur(s.X, s.Y), KindWalk); _fl[_startIdx] = 0;
			Status = State.Running;
			return true;
		}

		/// <summary>Expands up to <paramref name="budget"/> nodes. Returns the status afterwards (Running = call again).</summary>
		public State Step(int budget)
		{
			if (Status != State.Running) return Status;
			int n = 0;
			while (_heapN > 0)
			{
				int cur = Pop();
				if (_closed[cur]) continue;
				_closed[cur] = true;
				int cx = _ox + cur % _w, cy = _oy + cur / _w;
				if (cur == _goalIdx) { Status = State.Found; return Status; }
				float hc = Heur(cx, cy);
				if (hc < _bestH) { _bestH = hc; _bestIdx = cur; }
				Expand(cx, cy, cur, _g[cur]);
				Expanded++;
				if (++n >= budget) { return Status; } // Running: the budget slice is spent (the caller decides whether the search has its total allowance)
			}
			Status = HasPartial ? State.Partial : State.Failed;
			return Status;
		}

		/// <summary>Ends a running search that has used its total allowance: Partial when it expanded something closer to the goal than the start, else Failed.</summary>
		public void Abandon() { if (Status == State.Running) Status = HasPartial ? State.Partial : State.Failed; }

		/// <summary>World-space waypoints (bottom-centre) of the found path, or of the best-effort path to the closest node for a Partial search. First entry = next step.</summary>
		public bool BuildPath(List<Vector2> path)
		{
			path.Clear();
			int end = Status == State.Found ? _goalIdx : (Status == State.Partial ? _bestIdx : -1);
			if (end < 0) return false;
			_rev.Clear();
			for (int n = end; n != -1 && n != _startIdx && _rev.Count <= _w * _h; n = _from[n]) _rev.Add(n);
			for (int i = _rev.Count - 1; i >= 0; i--)
			{
				int n = _rev[i];
				int x = _ox + n % _w, y = _oy + n / _w;
				if (i > 0)
				{
					// compress straight walking runs: a waypoint only where the row or the edge kind changes
					int m = _rev[i - 1];
					if (_oy + m / _w == y && _kind[m] == KindWalk && _kind[n] == KindWalk) continue;
				}
				path.Add(TileNav.BottomCentre(x, y));
			}
			return path.Count > 0;
		}

		/// <summary>
		/// The movement code never walks a dry body into deep water (ScanAhead: water at head height ahead is a hard block unless the body is already wet), so the planner does not either:
		/// a tile whose head is in deep water is only enterable from a tile that already has water at the feet. Winged bodies fly over.
		/// </summary>
		private bool WaterOk(int fromX, int fromY, int x, int y) => TravelLegacy.On || _wings || !TileNav.IsDeepWater(x, y - 2) || TileNav.HasLiquid(fromX, fromY);

		private void Expand(int x, int y, int cur, float g)
		{
			bool standing = TileNav.IsStandable(x, y);
			if (standing)
			{
				for (int side = -1; side <= 1; side += 2)
				{
					int nx = x + side;
					// walk along the same floor
					if (TileNav.IsStandable(nx, y) && WaterOk(x, y, nx, y)) Relax(nx, y, cur, g + 1f, KindWalk);
					// one-tile step (Collision.StepUp handles it without a jump)
					if (!TileNav.IsSolid(x, y - 3) && TileNav.IsStandable(nx, y - 1) && WaterOk(x, y, nx, y - 1)) Relax(nx, y - 1, cur, g + 1.2f, KindStep);
					// walk off a ledge and fall to the first floor below
					if (!TileNav.IsSolid(nx, y) && !TileNav.IsSolid(nx, y - 1) && !TileNav.IsSolid(nx, y - 2) && !TileNav.HasLava(nx, y) && !TileNav.IsFloor(nx, y + 1))
						RelaxFall(nx, y, cur, g + 1f, TileNav.HasLiquid(x, y));
					// a running jump across a gap in the floor: level (or one tile up or down) landing within GapTiles, clear headroom over the whole span
					if (!TravelLegacy.On && !TileNav.IsFloor(nx, y + 1) && _gapTiles >= 2) RelaxGap(x, y, side, cur, g);
				}
				// drop through the platform underfoot
				if (TileNav.IsPlatform(x, y + 1) && !TileNav.IsSolid(x, y + 1)) RelaxFall(x, y, cur, g, TileNav.HasLiquid(x, y));

				// jumps: straight up or up and over, limited by the clear column above the head
				int clear = 0;
				for (int h = 1; h <= _jumpTiles; h++) { if (TileNav.IsSolid(x, y - 2 - h)) break; clear = h; }
				for (int h = 2; h <= clear; h++)
				{
					int ty = y - h;
					for (int dx = 0; dx <= PathJumpReachX; dx++)
					{
						for (int side = -1; side <= 1; side += 2)
						{
							int tx = x + dx * side;
							if (TileNav.IsStandable(tx, ty))
							{
								bool ok = true;
								for (int i = 1; i < dx && ok; i++) if (!TileNav.IsBodyClear(x + i * side, ty)) ok = false;
								if (ok) Relax(tx, ty, cur, g + h + dx + JumpEdgeCost, KindJump);
							}
							if (dx == 0) break;
						}
					}
				}
			}
			if (_wings)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dy == 0) continue;
						int nx = x + dx, ny = y + dy;
						if (!TileNav.IsBodyClear(nx, ny)) continue;
						float c = (dx != 0 && dy != 0) ? 1.414f : 1f;
						Relax(nx, ny, cur, g + c * FlightCostMult, KindFly);
					}
				}
			}
		}

		private void RelaxGap(int x, int y, int side, int cur, float g)
		{
			int rows = Math.Min(GapJumpClearRows, 3);
			for (int w = 1; w <= _gapTiles + 1; w++)
			{
				int cx = x + side * w;
				for (int r = 0; r <= rows; r++) if (TileNav.IsSolid(cx, y - r)) return;
				if (TileNav.HasLava(cx, y) || TileNav.HasLava(cx, y + 1)) return;
				if (w < 2 || !TileNav.IsFloor(cx, y + 1)) continue;
				if (TileNav.IsStandable(cx, y) && WaterOk(x, y, cx, y)) Relax(cx, y, cur, g + w + JumpEdgeCost, KindJump);
				return;
			}
		}

		private void RelaxFall(int x, int y, int cur, float g, bool originWet)
		{
			for (int d = 1; d <= _maxFall; d++)
			{
				int fy = y + d;
				if (TileNav.IsSolid(x, fy) || TileNav.HasLava(x, fy)) return;
				if (TileNav.IsFloor(x, fy + 1))
				{
					if (TileNav.IsStandable(x, fy) && (TravelLegacy.On || _wings || originWet || !TileNav.IsDeepWater(x, fy - 2))) Relax(x, fy, cur, g + d * FallEdgeCost, KindFall);
					return;
				}
			}
		}

		private void Relax(int x, int y, int from, float g, byte kind)
		{
			if (!InWindow(x, y)) return;
			int idx = Idx(x, y);
			int fl = 0;
			if (kind == KindFly)
			{
				fl = (_kind[from] == KindFly ? _fl[from] : 0) + 1;
				if (fl > _maxFlight) return;
			}
			if (_stamp[idx] == _gen && (_closed[idx] || g >= _g[idx])) return;
			Open(idx, from, g, Heur(x, y), kind);
			_fl[idx] = (ushort)fl;
		}

		private void Open(int idx, int from, float g, float h, byte kind)
		{
			_stamp[idx] = _gen; _closed[idx] = false; _g[idx] = g; _from[idx] = from; _kind[idx] = kind;
			Push(g + _hWeight * h, idx);
		}

		private float Heur(int x, int y)
		{
			float dx = x - _goalTile.X, dy = y - _goalTile.Y;
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		private bool InWindow(int x, int y) => x >= _ox && y >= _oy && x < _ox + _w && y < _oy + _h;
		private int Idx(int x, int y) => (x - _ox) + (y - _oy) * _w;

		// ---- binary min-heap on (f, idx) ----
		private void Push(float f, int idx)
		{
			if (_heapN >= _heapF.Length)
			{
				Array.Resize(ref _heapF, _heapF.Length * 2);
				Array.Resize(ref _heapI, _heapI.Length * 2);
			}
			int i = _heapN++;
			_heapF[i] = f; _heapI[i] = idx;
			while (i > 0)
			{
				int p = (i - 1) >> 1;
				if (_heapF[p] <= _heapF[i]) break;
				(_heapF[p], _heapF[i]) = (_heapF[i], _heapF[p]);
				(_heapI[p], _heapI[i]) = (_heapI[i], _heapI[p]);
				i = p;
			}
		}

		private int Pop()
		{
			int top = _heapI[0];
			_heapN--;
			if (_heapN > 0)
			{
				_heapF[0] = _heapF[_heapN]; _heapI[0] = _heapI[_heapN];
				int i = 0;
				while (true)
				{
					int l = 2 * i + 1, r = l + 1, m = i;
					if (l < _heapN && _heapF[l] < _heapF[m]) m = l;
					if (r < _heapN && _heapF[r] < _heapF[m]) m = r;
					if (m == i) break;
					(_heapF[m], _heapF[i]) = (_heapF[i], _heapF[m]);
					(_heapI[m], _heapI[i]) = (_heapI[i], _heapI[m]);
					i = m;
				}
			}
			return top;
		}
	}
}
