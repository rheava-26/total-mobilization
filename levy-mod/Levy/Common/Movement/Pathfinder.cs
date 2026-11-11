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
		public const byte KindWalk = 0, KindStep = 1, KindFall = 2, KindJump = 3, KindFly = 4, KindHook = 5, KindChain = 6, KindClimb = 7, KindArc = 8;

		private readonly int _maxW, _maxH, _cells;
		private readonly ushort[] _fl;   // consecutive flight steps on the best way to the cell (wings only run for WingTimeMax ticks)
		private readonly int[] _trap;    // row (+1) the body fell from on the best way to the cell when that drop was deeper than a jump and it has not climbed back to that row yet (0 = not in a pit); such a cell is never a best-effort end
		private bool _hook, _climb; private float _hookReachPx; private HashSet<long> _ban;
		public static long StatHookScans, StatHookEdges;
		private readonly float[] _g; private readonly int[] _from; private readonly int[] _stamp; private readonly bool[] _closed; private readonly byte[] _kind;
		private int _gen = 1;
		private float[] _heapF; private int[] _heapI; private int _heapN;
		private int _ox, _oy, _w, _h;
		private readonly List<int> _rev = new();
		private Point _goalTile; private int _startIdx, _goalIdx, _bestIdx; private float _bestH, _hWeight;
		private int _jumpTiles, _maxFall, _gapTiles, _maxFlight, _bestRow; private bool _wings;
		// flight arcs (route searches of winged bodies): takeoff cell -> cruise row -> descent line -> landing cell
		private bool _arcs, _gridFly; private float _arcFuelTicks, _arcCostPerTick, _arcGlideR; private int _arcAyT, _arcCxT;
		private readonly short[] _arcAy, _arcCx;      // KindArc cells: cruise row (offset from the window top) and cruise columns
		private int _arcStamp; private readonly int[] _afr, _afs, _ats; private readonly bool[] _afl; private readonly sbyte[] _atc;
		public static long StatArcScans, StatArcEdges;
		/// <summary>Lab switch: LEVY_ARCS=0 turns the 2.0.53 travel changes off (flight arcs, the cheaper flight grid, the shallow-drop nudge, the overhead-waypoint flight), for before / after runs in one build.</summary>
		public static readonly bool Travel6On = Environment.GetEnvironmentVariable("LEVY_ARCS") != "0";
		public static readonly bool ArcSearchOn = Environment.GetEnvironmentVariable("LEVY_ARCSEARCH") != "0", NudgeOn = Environment.GetEnvironmentVariable("LEVY_NUDGE") != "0", BestRowOn = Environment.GetEnvironmentVariable("LEVY_BESTROW") != "0";

		public State Status { get; private set; }
		public int Expanded { get; private set; }
		/// <summary>True when the last search ended without reaching the goal but has a best-effort node (the closest it expanded to the goal).</summary>
		public bool HasPartial => _bestIdx >= 0 && _bestIdx != _startIdx;

		public PathSearch(int maxW, int maxH, int maxCells = 0)
		{
			_maxW = maxW; _maxH = maxH; int cells = maxCells > 0 ? maxCells : maxW * maxH; _cells = cells;
			_arcAy = new short[cells]; _arcCx = new short[cells]; int arcSpan = ArcMaxCruiseTiles + ArcMaxGlideCols + 4; _afr = new int[arcSpan]; _afs = new int[arcSpan]; _ats = new int[arcSpan]; _afl = new bool[arcSpan]; _atc = new sbyte[arcSpan];
			_fl = new ushort[cells]; _trap = new int[cells]; _g = new float[cells]; _from = new int[cells]; _stamp = new int[cells]; _closed = new bool[cells]; _kind = new byte[cells];
			_heapF = new float[Math.Min(cells * 2, 1 << 16)]; _heapI = new int[_heapF.Length];
		}

		/// <summary>
		/// Starts a search. <paramref name="centred"/>: the window is centred on the start (the short search). Otherwise it is the bounding box of start and goal plus a margin, clipped to the buffer
		/// (a goal outside the window fails the search). <paramref name="hWeight"/> above 1 trades route optimality for far fewer expanded nodes.
		/// </summary>
		public bool Begin(Vector2 fromWorld, Vector2 toWorld, MovementAbilities abilities, bool centred, float hWeight, int padX = 60, int padUp = 90, int padDown = 150, HashSet<long> hookBan = null, bool allowHooks = true, bool gridFlight = false)
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
				// the window is the box of start and goal plus margins, within the buffer's area (any shape: a wide trip, or a tall one down a shaft). A box too big for it loses margin first; then the goal check below refuses (the caller hops).
				int x0, x1, y0, y1;
				for (int shrink = 0; ; shrink++)
				{
					x0 = Math.Min(s.X, t.X) - padX; x1 = Math.Max(s.X, t.X) + padX; y0 = Math.Min(s.Y, t.Y) - padUp; y1 = Math.Max(s.Y, t.Y) + padDown;
					if (!Travel6On || shrink >= 4 || ((x1 - x0 + 1) <= _maxW && (y1 - y0 + 1) <= _maxH && (long)(x1 - x0 + 1) * (y1 - y0 + 1) <= _cells)) break;
					padX = Math.Max(16, padX * 2 / 3); padUp = Math.Max(24, padUp * 2 / 3); padDown = Math.Max(30, padDown * 2 / 3);
				}
				_w = Math.Min(_maxW, x1 - x0 + 1); _h = Math.Min(_maxH, y1 - y0 + 1);
				if (Travel6On && (long)_w * _h > _cells) _h = Math.Max(1, _cells / _w);
				_ox = x0; _oy = y0;
				if (x1 - x0 + 1 > _maxW) _ox = (s.X + t.X) / 2 - _w / 2; // too wide: centre on the span, the goal check below refuses
			}
			if (!InWindow(t.X, t.Y) || !InWindow(s.X, s.Y)) return false;
			_wings = abilities.HasWings;
			_jumpTiles = Math.Max(1, abilities.JumpTiles);
			_maxFall = _wings ? FallScanTiles : (TravelLegacy.On || centred ? Math.Min(FallScanTiles, _jumpTiles + DropMarginTiles) : FallScanTiles); // the executor (ScanAhead) takes a fall of exactly FallScanTiles on a planned route // a route may go down into a pit (a safe fall) when the far side lets it climb out
			_gapTiles = abilities.GapTiles;
			_maxFlight = TravelLegacy.On ? ushort.MaxValue : Math.Max(6, (int)(abilities.WingTimeMax * FlySpeed / 16f * FlightRangeShare)); // tiles of flight one tank of wing time covers, with room for the climb
			_arcs = Travel6On && ArcSearchOn && _wings && !centred && !TravelLegacy.On && !gridFlight;
			_gridFly = _wings && !_arcs;   // a route search flies arcs between standing cells; the open-air flight grid only as the second pass (when no arc route reaches the goal within ArcPassMaxNodes)
			_arcFuelTicks = _wings ? Math.Max(0f, (abilities.WingTimeMax - WingBudgetReserveTicks) * ArcFuelShare - ArcOverheadTicks) : 0f;
			_arcCostPerTick = ArcPreference * Math.Max(3f, abilities.MaxRunSpeed) / 16f;           // search cost of a tick of flight: a tick of walking at the body's run speed costs 1/16 tile-cost per px
			_arcGlideR = MathHelper.Clamp(abilities.MaxRunSpeed / GlideFallSpeed * ArcGlideSpeedShare, ArcGlideRatioMin, ArcGlideRatioMax);
			_hWeight = Math.Max(1f, hWeight);
			_hook = allowHooks && !TravelLegacy.On && !centred && !_wings && abilities.HasHook && abilities.HookReachPx >= HookMinReachTiles * 16f;
			_climb = !TravelLegacy.On && !centred && abilities.CanWallClimb;
			_hookReachPx = _hook ? abilities.HookReachPx : 0f; _ban = hookBan;
			_goalTile = t; _bestRow = Math.Max(s.Y, t.Y) + 15;
			if (++_gen == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); _gen = 1; }
			_heapN = 0;
			_startIdx = Idx(s.X, s.Y); _goalIdx = Idx(t.X, t.Y);
			_bestIdx = _startIdx; _bestH = Heur(s.X, s.Y);
			Open(_startIdx, -1, 0f, Heur(s.X, s.Y), KindWalk); _fl[_startIdx] = 0; _trap[_startIdx] = 0;
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
				float hc = Heur(cx, cy) + (Travel6On && BestRowOn ? Math.Max(0, cy - _bestRow) * 0.6f : 0f);   // (the end of a best-effort route is not a hole far below the goal)
				if (hc < _bestH && _trap[cur] == 0) { _bestH = hc; _bestIdx = cur; }
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
		public bool BuildPath(List<Vector2> path) => BuildPath(path, null);

		/// <summary>As above; <paramref name="kinds"/> (when given) gets one byte per waypoint: 0 = reached by walking, jumping or falling, 1 = reached by one grappling hook cast from the previous waypoint, 2 = by a chain of casts up a wall, 3 = by wall climbing up a face from the previous waypoint.</summary>
		public bool BuildPath(List<Vector2> path, List<byte> kinds)
		{
			path.Clear(); kinds?.Clear();
			int end = Status == State.Found ? _goalIdx : (Status == State.Partial ? _bestIdx : -1);
			if (end < 0) return false;
			_rev.Clear();
			for (int n = end; n != -1 && n != _startIdx && _rev.Count <= _w * _h; n = _from[n]) _rev.Add(n);
			for (int i = _rev.Count - 1; i >= 0; i--)
			{
				int n = _rev[i];
				int x = _ox + n % _w, y = _oy + n / _w;
				if (_kind[n] == KindArc)
				{
					// the air points of the arc (straight up from the takeoff cell, level along the cruise row, down the descent line), then the landing; all kind 4
					int p = _from[n], sx = _ox + p % _w, ay = _oy + _arcAy[n], cx = _arcCx[n], side = x > sx ? 1 : -1;
					int sy = _oy + p / _w;
					for (int yy = sy - 6; yy > ay + 2; yy -= 6) { path.Add(TileNav.BottomCentre(sx, yy)); kinds?.Add(4); }
					path.Add(TileNav.BottomCentre(sx, ay)); kinds?.Add(4);
					for (int k = 6; k < cx; k += 6) { path.Add(TileNav.BottomCentre(sx + side * k, ay)); kinds?.Add(4); }
					if (cx > 0) { path.Add(TileNav.BottomCentre(sx + side * cx, ay)); kinds?.Add(4); }
					int j = Math.Abs(x - (sx + side * cx));
					for (int m = 5; m < j; m += 5) { path.Add(TileNav.BottomCentre(sx + side * (cx + m), ay + (y - ay) * m / j)); kinds?.Add(4); }
					path.Add(TileNav.BottomCentre(x, y)); kinds?.Add(4);
					continue;
				}
				if (i > 0)
				{
					// compress straight walking runs: a waypoint only where the row or the edge kind changes
					int m = _rev[i - 1];
					if (_oy + m / _w == y && _kind[m] == KindWalk && _kind[n] == KindWalk) continue;
				}
				path.Add(TileNav.BottomCentre(x, y));
				kinds?.Add(_kind[n] == KindHook ? (byte)1 : _kind[n] == KindChain ? (byte)2 : _kind[n] == KindClimb ? (byte)3 : (byte)0);
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
			if (standing && _arcs) ExpandArcs(x, y, cur, g);
			if (standing && _hook) ExpandHooks(x, y, cur, g);
			if (standing && _climb) ExpandClimb(x, y, cur, g);
			if (_gridFly)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dy == 0) continue;
						int nx = x + dx, ny = y + dy;
						if (!TileNav.IsBodyClear(nx, ny)) continue;
						float c = (dx != 0 && dy != 0) ? 1.414f : 1f;
						Relax(nx, ny, cur, g + c * (Travel6On ? FlightCostMult : 1.3f), KindFly);
					}
				}
			}
		}


		/// <summary>
		/// Wall climbing (Player.spikedBoots): from a standing cell with a wall beside it, up the face to the cell on top of it, however tall (a wall jump gains a jump of height each time, and the
		/// body hangs on the wall between). Needs the column the body climbs in to be open up to the lip. A face a plain jump clears gets no edge. A target the executor failed on is banned like a hook target.
		/// </summary>
		private void ExpandClimb(int x, int y, int cur, float g)
		{
			if (TileNav.HasLiquid(x, y) || TileNav.HasLiquid(x, y - 1)) return;
			for (int side = -1; side <= 1; side += 2)
			{
				int nx = x + side;
				if (!TileNav.IsSolid(nx, y - 1) || !TileNav.IsSolid(nx, y - 2)) continue;
				int top = y - 1, lim = Math.Max(_oy + 3, y - ClimbMaxFaceTiles);
				while (top > lim && TileNav.IsSolid(nx, top - 1)) top--;
				int hgt = y - top;
				if (hgt <= _jumpTiles || top <= lim) continue;       // a jump gets over it / taller than the window
				int ty = top - 1;
				if (!InWindow(nx, ty) || !TileNav.IsStandable(nx, ty) || HookBanned(nx, ty)) continue;
				bool open = true;                                    // the body climbs in column x: clear from its head up to a body-height over the lip, no lava
				for (int r = ty - 2; r <= y - 3 && open; r++) if (TileNav.IsSolid(x, r) || TileNav.HasLava(x, r)) open = false;
				if (!open) continue;
				Relax(nx, ty, cur, g + ClimbEdgeCost + hgt * ClimbTileCost, KindClimb);
			}
		}

		// ---- grappling hook edges ----
		private bool HookBanned(int tx, int ty) => _ban != null && _ban.Contains(((long)tx << 24) | (uint)ty);
		public static long HookKey(int tx, int ty) => ((long)tx << 24) | (uint)ty;

		// the anchors a cast from the current cell could latch onto (the HookPlanner fan: rays across the upper half-plane, the first latchable tile on each)
		private readonly int[] _anX = new int[HookFanRays], _anY = new int[HookFanRays]; private int _anN;
		private static bool Latchable(int x, int y)
		{
			if (!TileNav.InBounds(x, y)) return false;
			Terraria.Tile t = Terraria.Main.tile[x, y];
			return t.HasUnactuatedTile && Terraria.Main.tileSolid[t.TileType];
		}

		private void HookFan(int x, int y, float reachPx)
		{
			_anN = 0;
			float cx = x * 16f + 8f, cy = (y + 1) * 16f - 21f;
			for (int k = 0; k < HookFanRays; k++)
			{
				float th = -MathHelper.Pi + (k + 0.5f) * MathHelper.Pi / HookFanRays, dx = (float)Math.Cos(th), dy = (float)Math.Sin(th);
				for (float t = 10f; t <= reachPx; t += 8f)
				{
					float px = cx + dx * t, py = cy + dy * t;
					int bx0 = (int)Math.Floor((px - 5f) / 16f), bx1 = (int)Math.Floor((px + 5f) / 16f), by0 = (int)Math.Floor((py - 5f) / 16f), by1 = (int)Math.Floor((py + 5f) / 16f);
					bool hit = false; int hx = 0, hy = 0;
					for (int tx = bx0; tx <= bx1 && !hit; tx++) for (int ty = by0; ty <= by1; ty++) if (Latchable(tx, ty)) { hit = true; hx = tx; hy = ty; break; }
					if (!hit) continue;
					_anX[_anN] = hx; _anY[_anN] = hy; _anN++;
					break;
				}
			}
		}

		/// <summary>A fan anchor within three columns of the target and up to ten rows above it (or at its floor): a hook latched there carries the body to the target.</summary>
		private bool HookAnchorNear(int tx, int ty)
		{
			for (int i = 0; i < _anN; i++)
				if (Math.Abs(_anX[i] - tx) <= HookAnchorNearCols && _anY[i] >= ty - 10 && _anY[i] <= ty + 1) return true;
			return false;
		}

		/// <summary>
		/// Hook edges from a standing cell that has a wall or an open edge beside it: to the highest standable cell in each column within the hook's reach (a ledge above the wall, the far lip of a gap) that has
		/// a fan anchor near it; and a chain of casts up a tall wall face. The three nearest-the-goal targets per side are kept. The executor fires the real hook planner at the
		/// edge's start (a plan that fails bans the target and the route is searched again).
		/// </summary>
		private void ExpandHooks(int x, int y, int cur, float g)
		{
			if (TileNav.HasLiquid(x, y) || TileNav.HasLiquid(x, y - 1)) return;   // a soldier does not cast from the water
			StatHookScans++; bool fanned = false;
			float reachT = _hookReachPx / 16f;
			int dmax = (int)(reachT * HookDirectShare), riseMax = (int)(reachT * HookRiseShare);
			for (int side = -1; side <= 1; side += 2)
			{
				int nx = x + side;
				bool wall = TileNav.IsSolid(nx, y - 1) && TileNav.IsSolid(nx, y - 2);
				bool open = !TileNav.IsFloor(nx, y + 1) && !TileNav.IsSolid(nx, y - 1);
				if (!wall && !open) continue;
				int b0x = -1, b0y = 0, b1x = -1, b1y = 0, b2x = -1, b2y = 0; float h0 = float.MaxValue, h1 = float.MaxValue, h2 = float.MaxValue; int nb = 0;
				for (int dx = 1; dx <= dmax; dx++)
				{
					int tx = x + side * dx;
					if (!InWindow(tx, y)) break;
					// highest standable cell of the column within the rise, down to a little below the feet
					int ty = int.MinValue;
					for (int r = y - riseMax; r <= y + 1; r++) { if (TileNav.IsStandable(tx, r)) { ty = r; break; } }
					if (ty == int.MinValue || !InWindow(tx, ty)) continue;
					int rise = y - ty;
					if (rise < 2 && !open) continue;                                  // level or lower: only across an open edge
					if (rise <= _jumpTiles && dx <= 2) continue;                      // plain jumping gets there
					if (rise < 2 && dx <= 1) continue;
					float dist = (float)Math.Sqrt(dx * dx + rise * rise);
					if (dist > reachT * HookDirectShare || dist < 3f) continue;
					if (HookBanned(tx, ty)) continue;
					if (!fanned) { HookFan(x, y, _hookReachPx); fanned = true; }
					if (!HookAnchorNear(tx, ty)) continue;
					float h = Heur(tx, ty);
					nb++;
					if (h < h0) { b2x = b1x; b2y = b1y; h2 = h1; b1x = b0x; b1y = b0y; h1 = h0; b0x = tx; b0y = ty; h0 = h; }
					else if (h < h1) { b2x = b1x; b2y = b1y; h2 = h1; b1x = tx; b1y = ty; h1 = h; }
					else if (h < h2) { b2x = tx; b2y = ty; h2 = h; }
				}
				if (b0x >= 0) { HookRelax(x, y, b0x, b0y, cur, g, KindHook); }
				if (b1x >= 0) { HookRelax(x, y, b1x, b1y, cur, g, KindHook); }
				if (b2x >= 0) { HookRelax(x, y, b2x, b2y, cur, g, KindHook); }
				// a wall face beside us: reel up it (one cast when it is within the reach, a chain of casts when it is taller)
				if (wall)
				{
					int top = y - 1;
					int lim = Math.Max(_oy + 3, y - ClimbMaxFaceTiles);   // any height: the executor casts again from the anchor it hangs on, link after link
					while (top > lim && TileNav.IsSolid(nx, top - 1)) top--;
					int hgt = y - top;
					if (hgt > _jumpTiles && top > lim)
					{
						bool single = hgt <= riseMax;
						for (int dx = 0; dx <= 3; dx++)
						{
							int tx = nx + side * dx, found = int.MinValue;
							for (int ty = top - 2; ty <= top + 1 && found == int.MinValue; ty++) if (InWindow(tx, ty) && TileNav.IsStandable(tx, ty)) found = ty;
							if (found == int.MinValue || HookBanned(tx, found)) continue;
							HookRelax(x, y, tx, found, cur, single ? g : g + HookChainBaseCost + hgt * HookChainTileCost, single ? KindHook : KindChain); break;
						}
					}
				}
				if (open && !wall) GapChain(x, y, side, cur, g, reachT);
			}
		}

		/// <summary>
		/// Across a gap wider than one cast reaches: cast onto the far wall (below its lip), hang there, climb it. An edge to the far lip's standing cell when a face tile of the far wall is within the first cast's reach
		/// and the line to it is clear. The executor does the casts (chain climbing); the cost is a cast, the span and the short climb.
		/// </summary>
		private void GapChain(int x, int y, int side, int cur, float g, float reachT)
		{
			float cx0 = x * 16f + 8f, cy0 = (y + 1) * 16f - 21f;
			int maxDx = (int)(reachT * 1.0f) + 12;
			for (int dx = 3; dx <= maxDx; dx++)
			{
				int fx = x + side * dx;
				if (!InWindow(fx, y)) return;
				if (TileNav.IsSolid(fx - side, y - 1) || TileNav.IsSolid(fx - side, y - 2)) return;    // a wall in the span: not a gap
				int ty = int.MinValue;
				for (int r = y - 3; r <= y + 1; r++) if (TileNav.IsStandable(fx, r) && !TileNav.IsFloor(fx - side, r + 1)) { ty = r; break; }
				if (ty == int.MinValue || HookBanned(fx, ty)) continue;
				// a face tile of the far wall, below the lip, in reach of the first cast with a clear line
				for (int r = ty + 1; r <= ty + 7; r++)
				{
					if (!Latchable(fx, r) || Latchable(fx - side, r)) continue;
					float tcx = fx * 16f + 8f - side * 8f, tcy = r * 16f + 8f, ddx = tcx - cx0, ddy = tcy - cy0, dl = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
					if (dl > _hookReachPx * 0.93f + HookJumpCastPx) break;      // beyond the cast: a jump out over the gap first, then the cast from the air
					bool clear = true;
					for (float t = 12f; t < dl - 6f && clear; t += 8f) { int px = (int)Math.Floor((cx0 + ddx * t / dl) / 16f), py = (int)Math.Floor((cy0 + ddy * t / dl) / 16f); if (TileNav.IsSolid(px, py)) clear = false; }
					if (!clear) continue;
					HookRelax(x, y, fx, ty, cur, g + HookChainBaseCost + dx * 0.8f + (dl > _hookReachPx * 0.93f ? 6f : 0f), KindChain);
					return;
				}
			}
		}

		private void HookRelax(int x, int y, int tx, int ty, int cur, float g, byte kind)
		{
			StatHookEdges++;
			float dx = tx - x, dy = ty - y;
			float c = kind == KindChain ? 0f : HookEdgeCost + (float)Math.Sqrt(dx * dx + dy * dy) * HookEdgeDistCost;
			Relax(tx, ty, cur, g + c, kind);
		}

		// ---- flight arcs ----
		/// <summary>A standing cell at the rim of a pit or a gap, or with a wall taller than ArcLaunchWallRows within ArcLaunchLook tiles on this side: somewhere a body with wings takes off.</summary>
		private bool ArcLaunch(int x, int y, int side)
		{
			if (!TileNav.IsFloor(x + side, y + 1)) return true;                                  // the rim of a pit or a gap
			int h = Math.Min(_jumpTiles, ArcLaunchWallRows);
			for (int k = 1; k <= ArcLaunchLook; k++) if (TileNav.IsSolid(x + side * k, y - h)) return true;   // a wall taller than that stands within a few tiles
			return false;
		}

		private int ArcFloor(int x0, int side, int ay, int c, out bool lava)
		{
			if (_afs[c] != _arcStamp)
			{
				int col = x0 + side * c;
				int rMax = Math.Min(ay + ArcMaxDescentRows, _oy + _h - 2);
				int fr = TileNav.FirstFloorOrLava(col, ay + 1, rMax, out bool lv);
				_afr[c] = fr; _afl[c] = lv; _afs[c] = _arcStamp;
			}
			lava = _afl[c]; return _afr[c];
		}

		private bool ArcTopClear(int x0, int side, int ay, int c)
		{
			if (_ats[c] != _arcStamp) { _atc[c] = (sbyte)(TileNav.IsBodyClear(x0 + side * c, ay) ? 1 : 0); _ats[c] = _arcStamp; }
			return _atc[c] == 1;
		}

		/// <summary>
		/// Arcs from a launch spot: for each cruise row (ArcApex rows up) the body rises in the takeoff column, flies level along the row for as many columns as the tank allows, and comes down on a straight line (slope 1 / glide
		/// ratio) onto the first floor it meets. A landing counts when the line arrives exactly at a standable cell. The cruise keeps ArcClearAboveFloor rows over the ground, so a lip or a rim is cleared by body heights, not by a hair.
		/// </summary>
		private void ExpandArcs(int x, int y, int cur, float g)
		{
			const float tpt = 16f / ArcSpeedPx;
			StatArcScans++;
			for (int side = -1; side <= 1; side += 2)
			{
				if (!ArcLaunch(x, y, side)) continue;
				int lastClear = 0;
				for (int ui = 0; ui < ArcApex.Length; ui++)
				{
					int up = ArcApex[ui], ay = y - up;
					if ((up + 2) * tpt > _arcFuelTicks || !InWindow(x, ay - 2)) break;
					bool colOk = true;
					for (int r = lastClear + 1; r <= up && colOk; r++) if (TileNav.IsSolid(x, y - r - 2) || TileNav.HasLava(x, y - r - 2)) colOk = false;
					if (!colOk) break;
					lastClear = up;
					ArcCruise(x, y, side, up, cur, g);
				}
			}
		}

		private void ArcCruise(int x, int y, int side, int up, int cur, float g)
		{
			const float tpt = 16f / ArcSpeedPx;
			int ay = y - up;
			int maxC = Math.Min(ArcMaxCruiseTiles, (int)((_arcFuelTicks - up * tpt) / tpt));
			if (maxC < 0) return;
			if (++_arcStamp == int.MaxValue) { Array.Clear(_afs, 0, _afs.Length); Array.Clear(_ats, 0, _ats.Length); _arcStamp = 1; }
			int lastTx = int.MinValue, lastTy = int.MinValue;
			for (int cx = 0; cx <= maxC; cx += (cx >= 8 ? 2 : 1))
			{
				if (cx > 0)
				{
					int col = x + side * cx;
					if (!InWindow(col, ay)) break;
					if (cx >= 9 && !ArcTopClear(x, side, ay, cx - 1)) break;          // (the column skipped by the step of two)
					if (!ArcTopClear(x, side, ay, cx)) break;
					int fc = ArcFloor(x, side, ay, cx, out _);
					if (fc != int.MaxValue && fc < ay + 1 + ArcClearAboveFloor) break;       // the ground comes up into the cruise: this cruise ends
				}
				for (int j = 1; j <= ArcMaxGlideCols; j++)
				{
					int c = cx + j;
					if (c >= _afr.Length) break;
					int col = x + side * c;
					if (!InWindow(col, ay)) break;
					int r = ay + (int)(j / _arcGlideR);
					if (r - 2 <= ay && !ArcTopClear(x, side, ay, c)) break;                  // head room over the cruise row at this column
					int fr = ArcFloor(x, side, ay, c, out bool lava);
					if (fr == int.MaxValue) { if (r - ay >= ArcMaxDescentRows) break; continue; }
					if (r < fr - 1) continue;                                                // still above the ground here
					if (r == fr - 1 && !lava && InWindow(col, r) && !(col == lastTx && r == lastTy) && ArcLand(col, r))
					{
						lastTx = col; lastTy = r;
						float rows = r - ay, tUp = up * tpt, tCr = cx * tpt, fuelLeft = _arcFuelTicks - tUp - tCr;
						float len = (float)Math.Sqrt(j * j + rows * rows), tPow = len * tpt, tDesc;
						if (fuelLeft >= tPow) tDesc = tPow;
						else { float f = Math.Max(0f, fuelLeft); tDesc = f + (1f - f / tPow) * rows * (16f / GlideFallSpeed); }
						_arcAyT = ay; _arcCxT = cx; StatArcEdges++;
						Relax(col, r, cur, g + ArcTakeoffCost + (tUp + tCr + tDesc) * _arcCostPerTick, KindArc);
					}
					break;
				}
			}
		}

		/// <summary>A landing cell: standable, dry, not beside lava.</summary>
		private static bool ArcLand(int x, int y) => TileNav.IsStandable(x, y) && !TileNav.HasLiquid(x, y) && !TileNav.HasLiquid(x, y - 1) && !LavaBeside(x, y);

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

		/// <summary>Lava in the column beside the feet, or beside or under the floor: a body that lands here and is knocked, or steps wrong, is in it.</summary>
		private static bool LavaBeside(int x, int y) => TileNav.HasLava(x - 1, y) || TileNav.HasLava(x + 1, y) || TileNav.HasLava(x - 1, y - 1) || TileNav.HasLava(x + 1, y - 1) || TileNav.HasLava(x - 1, y + 1) || TileNav.HasLava(x + 1, y + 1);

		private void Relax(int x, int y, int from, float g, byte kind)
		{
			if (!InWindow(x, y)) return;
			if (kind >= KindFall && kind != KindFly && !_wings && !TravelLegacy.On && LavaBeside(x, y)) return;   // no planned landing (fall, jump, hook, climb) beside lava
			int idx = Idx(x, y);
			int fl = 0;
			if (kind == KindFly)
			{
				fl = (_kind[from] == KindFly ? _fl[from] : 0) + 1;
				if (fl > _maxFlight) return;
				if (Travel6On && TileNav.IsStandable(x, y)) fl = 0;   // touching down refills the tank
			}
			if (_stamp[idx] == _gen && (_closed[idx] || g >= _g[idx])) return;
			Open(idx, from, g, Heur(x, y), kind);
			if (kind == KindArc) { _arcAy[idx] = (short)(_arcAyT - _oy); _arcCx[idx] = (short)_arcCxT; }
			_fl[idx] = (ushort)fl;
			int tr = _trap[from];
			if (kind == KindFall) { if (tr == 0 && !_wings && y - (_oy + from / _w) > _jumpTiles) tr = _oy + from / _w + 1; }
			else if (tr != 0 && y <= tr - 1) tr = 0;   // climbed back to the row it fell from
			_trap[idx] = tr;
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
