using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Coarse tile-grid A* with step/jump/fall/flight edges inside a window around the start. A node is a tile that holds
	/// the feet of a standing body (three clear rows over a floor); with wings any clear cell is a node. Static buffers are
	/// reused between searches (single-threaded game update). OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public static class Pathfinder
	{
		private const int W = PathWindowW, H = PathWindowH, Cells = W * H;
		private const byte KindWalk = 0, KindStep = 1, KindFall = 2, KindJump = 3, KindFly = 4;

		private static readonly float[] _g = new float[Cells];
		private static readonly int[] _from = new int[Cells];
		private static readonly int[] _stamp = new int[Cells];   // == _gen when the cell was touched in this search
		private static readonly bool[] _closed = new bool[Cells];
		private static readonly byte[] _kind = new byte[Cells];
		private static int _gen = 1;
		private static float[] _heapF = new float[Cells * 2];
		private static int[] _heapI = new int[Cells * 2];
		private static int _heapN;
		private static int _ox, _oy;
		private static readonly List<int> _rev = new();

		/// <summary>Fills <paramref name="path"/> with world-space waypoints (first = next step). Returns false if unreachable within budget.</summary>
		public static bool FindPath(Vector2 fromWorld, Vector2 toWorld, MovementAbilities abilities, List<Vector2> path, int maxNodes = PathMaxNodes)
		{
			path.Clear();
			abilities ??= new MovementAbilities();
			Point s = TileNav.FeetTile(fromWorld);
			Point t = TileNav.FeetTile(toWorld);
			if (!TileNav.InBounds(s.X, s.Y) || !TileNav.InBounds(t.X, t.Y)) return false;
			if (!TileNav.IsStandable(s.X, s.Y) && !TileNav.NearestStandable(s.X, s.Y, GoalSnapTiles, out s)) return false;
			if (!TileNav.IsStandable(t.X, t.Y) && !TileNav.NearestStandable(t.X, t.Y, GoalSnapTiles, out t)) return false;
			if (s == t) return false;

			_ox = s.X - W / 2; _oy = s.Y - H / 2;
			if (!InWindow(t.X, t.Y)) return false;

			bool wings = abilities.HasWings;
			int jumpTiles = Math.Max(1, abilities.JumpTiles);
			int maxFall = wings ? FallScanTiles : Math.Min(FallScanTiles, jumpTiles + DropMarginTiles);

			if (++_gen == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); _gen = 1; }
			_heapN = 0;
			int start = Idx(s.X, s.Y), goal = Idx(t.X, t.Y);
			Open(start, -1, 0f, Heur(s.X, s.Y, t), KindWalk);

			int expanded = 0; bool found = false;
			while (_heapN > 0)
			{
				int cur = Pop();
				if (_closed[cur]) continue;
				_closed[cur] = true;
				if (cur == goal) { found = true; break; }
				if (++expanded > maxNodes) break;
				Expand(_ox + cur % W, _oy + cur / W, cur, _g[cur], t, jumpTiles, maxFall, wings);
			}
			if (!found) return false;

			_rev.Clear();
			for (int n = goal; n != -1 && n != start && _rev.Count <= Cells; n = _from[n]) _rev.Add(n);
			for (int i = _rev.Count - 1; i >= 0; i--)
			{
				int n = _rev[i];
				int x = _ox + n % W, y = _oy + n / W;
				if (i > 0)
				{
					// compress straight walking runs: a waypoint only where the row or the edge kind changes
					int m = _rev[i - 1];
					if (_oy + m / W == y && _kind[m] == KindWalk && _kind[n] == KindWalk) continue;
				}
				path.Add(TileNav.BottomCentre(x, y));
			}
			return path.Count > 0;
		}

		private static void Expand(int x, int y, int cur, float g, Point goal, int jumpTiles, int maxFall, bool wings)
		{
			bool standing = TileNav.IsStandable(x, y);
			if (standing)
			{
				for (int side = -1; side <= 1; side += 2)
				{
					int nx = x + side;
					// walk along the same floor
					if (TileNav.IsStandable(nx, y)) Relax(nx, y, cur, g + 1f, goal, KindWalk);
					// one-tile step (Collision.StepUp handles it without a jump)
					if (!TileNav.IsSolid(x, y - 3) && TileNav.IsStandable(nx, y - 1)) Relax(nx, y - 1, cur, g + 1.2f, goal, KindStep);
					// walk off a ledge and fall to the first floor below
					if (!TileNav.IsSolid(nx, y) && !TileNav.IsSolid(nx, y - 1) && !TileNav.IsSolid(nx, y - 2) && !TileNav.HasLava(nx, y) && !TileNav.IsFloor(nx, y + 1))
						RelaxFall(nx, y, cur, g + 1f, goal, maxFall);
				}
				// drop through the platform underfoot
				if (TileNav.IsPlatform(x, y + 1) && !TileNav.IsSolid(x, y + 1)) RelaxFall(x, y, cur, g, goal, maxFall);

				// jumps: straight up or up and over, limited by the clear column above the head
				int clear = 0;
				for (int h = 1; h <= jumpTiles; h++) { if (TileNav.IsSolid(x, y - 2 - h)) break; clear = h; }
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
								if (ok) Relax(tx, ty, cur, g + h + dx + JumpEdgeCost, goal, KindJump);
							}
							if (dx == 0) break;
						}
					}
				}
			}
			if (wings)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dy == 0) continue;
						int nx = x + dx, ny = y + dy;
						if (!TileNav.IsBodyClear(nx, ny)) continue;
						float c = (dx != 0 && dy != 0) ? 1.414f : 1f;
						Relax(nx, ny, cur, g + c * FlightCostMult, goal, KindFly);
					}
				}
			}
		}

		private static void RelaxFall(int x, int y, int cur, float g, Point goal, int maxFall)
		{
			for (int d = 1; d <= maxFall; d++)
			{
				int fy = y + d;
				if (TileNav.IsSolid(x, fy) || TileNav.HasLava(x, fy)) return;
				if (TileNav.IsFloor(x, fy + 1))
				{
					if (TileNav.IsStandable(x, fy)) Relax(x, fy, cur, g + d * FallEdgeCost, goal, KindFall);
					return;
				}
			}
		}

		private static void Relax(int x, int y, int from, float g, Point goal, byte kind)
		{
			if (!InWindow(x, y)) return;
			int idx = Idx(x, y);
			if (_stamp[idx] == _gen && (_closed[idx] || g >= _g[idx])) return;
			Open(idx, from, g, Heur(x, y, goal), kind);
		}

		private static void Open(int idx, int from, float g, float h, byte kind)
		{
			_stamp[idx] = _gen; _closed[idx] = false; _g[idx] = g; _from[idx] = from; _kind[idx] = kind;
			Push(g + h, idx);
		}

		private static float Heur(int x, int y, Point goal)
		{
			float dx = x - goal.X, dy = y - goal.Y;
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		private static bool InWindow(int x, int y) => x >= _ox && y >= _oy && x < _ox + W && y < _oy + H;
		private static int Idx(int x, int y) => (x - _ox) + (y - _oy) * W;

		// ---- binary min-heap on (f, idx) ----
		private static void Push(float f, int idx)
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

		private static int Pop()
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
