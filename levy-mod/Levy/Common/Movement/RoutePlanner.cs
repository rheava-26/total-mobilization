using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>One planned trip: the waypoints (bottom-centre, first = next step) from where a soldier stood to the goal, or to the closest point the search could reach.</summary>
	public sealed class Route
	{
		public enum S : byte { Pending, Ready, Partial, Failed }
		public S State = S.Pending;
		public readonly List<Vector2> Pts = new();
		/// <summary>One per waypoint: 0 = reached on foot, 1 = by one grappling-hook cast from the previous waypoint, 2 = by a chain of casts up a wall, 3 = by wall climbing from the previous waypoint, 4 = in flight (the waypoints of an arc: up from the takeoff cell, along the cruise row, down to the landing; the last one is the landing cell).</summary>
		public readonly List<byte> Kinds = new();
		public Point Start, Goal; public int Variant; public uint Born;
		public int NodesUsed; public double Ms; public int Jump; public bool Climb; public bool Wings; public int HookTiles; public int HookEdges; public bool HookPass, GridPass;
		public bool Usable => State == S.Ready || State == S.Partial;
	}

	/// <summary>
	/// Long-distance routing. The old way to cross a long trip was to aim at a point 30 tiles on and let the reflexes and a 160x100 window search sort it out, which fails at the first cliff, pit or
	/// overhang that needs a detour. This plans the whole trip once with the same tile search (<see cref="PathSearch"/>) over the start..goal bounding box, in node-budget slices spread over game ticks
	/// (RouteNodesPerTick, about 1.4 ms), and shares the result between soldiers that start close together and have the same abilities. A goal the soldier cannot reach on foot or by wing comes back as a
	/// Partial route to the closest point reached, so the soldier goes as near as it can instead of standing at the first obstacle. Server / single player only. OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public class RoutePlanner : ModSystem
	{
		private static readonly Dictionary<long, Route> _routes = new();
		private static readonly Queue<Route> _queue = new();
		private static PathSearch _big;
		private static int _hookBase; private static Route _active; private static Vector2 _aFrom, _aTo; private static MovementAbilities _aAb; private static HashSet<long> _aBan;
		private static readonly List<long> _evict = new();
		public static long Planned, PlannedNodes, PlannedFound, PlannedPartial, PlannedFailed, SliceTicks, SlicesRun; public static double PlannedMs, MaxSliceMs;

		public static readonly List<string> Log = new();
		public static void StatReset() { PathSearch.StatHookScans = PathSearch.StatHookEdges = PathSearch.StatArcScans = PathSearch.StatArcEdges = 0; Log.Clear(); Planned = PlannedNodes = PlannedFound = PlannedPartial = PlannedFailed = SliceTicks = SlicesRun = 0; PlannedMs = 0; MaxSliceMs = 0; }
		public static string StatSummary() => $"routes={Planned} found={PlannedFound} partial={PlannedPartial} failed={PlannedFailed} nodes={PlannedNodes} ms={PlannedMs:0.0} maxSliceMs={MaxSliceMs:0.00} slices={SlicesRun} hookScans={PathSearch.StatHookScans} arcScans={PathSearch.StatArcScans} arcEdges={PathSearch.StatArcEdges} hookEdges={PathSearch.StatHookEdges} queued={_queue.Count}";

		private static long Key(Point goal, Point start, int jump, int wings, int variant, int hookTiles, int banHash, bool climb)
		{
			long k = goal.X; k = k * 4001 + goal.Y; k = k * 1009 + (start.X / RouteStartBucketTiles); k = k * 401 + (start.Y / RouteStartBucketTiles);
			k = k * 17 + Math.Clamp(jump, 0, 15); k = k * 17 + wings; k = k * 8 + (variant & 7); k = k * 33 + hookTiles; k = k * 1000003 + banHash; k = k * 2 + (climb ? 1 : 0);
			return k;
		}

		/// <summary>The route for this trip: an existing one (any state) or a new Pending one queued for the planner. Null when the planner is full.</summary>
		public static Route Get(Vector2 fromBottom, Vector2 toBottom, MovementAbilities ab, int variant = 0, List<long> hookBan = null)
		{
			if (LevyUtil.IsClient) return null;
			ab ??= new MovementAbilities();
			Point s = TileNav.FeetTile(fromBottom), t = TileNav.FeetTile(toBottom);
			int hookTiles = !TravelLegacy.On && !ab.HasWings && ab.HasHook && ab.HookReachPx >= HookMinReachTiles * 16f ? Math.Min(31, (int)(ab.HookReachPx / 16f)) : 0;
			bool climb = !TravelLegacy.On && ab.CanWallClimb;
			int banHash = 0; if ((hookTiles > 0 || climb) && hookBan != null) foreach (long b in hookBan) banHash = banHash * 31 + (int)(b ^ (b >> 29));
			int wingKey = ab.HasWings ? 1 + Math.Min(7, ab.WingTimeMax / 35) + (ab.MaxRunSpeed > 4.5f ? 8 : 0) : 0;   // the wing tank and the run speed set how far an arc reaches
			long key = Key(t, s, ab.JumpTiles, wingKey, variant, hookTiles, banHash & 0xFFFF, climb);
			uint now = Main.GameUpdateCount;
			if (_routes.TryGetValue(key, out var r))
			{
				bool stale = now - r.Born > (uint)RouteTtlTicks || ((r.State == Route.S.Failed || r.State == Route.S.Partial) && now - r.Born > (uint)RouteRetryTicks);
				if (!stale) return r;
				if (r == _active) return r;
				_routes.Remove(key);
			}
			if (_queue.Count >= RouteQueueMax) return null;
			if (_routes.Count >= RouteCacheMax) Evict(now);
			r = new Route { Start = s, Goal = t, Variant = variant, Born = now, Jump = ab.JumpTiles, Wings = ab.HasWings, HookTiles = hookTiles, Climb = climb };
			if ((hookTiles > 0 || climb) && hookBan != null && hookBan.Count > 0) _pendingBan[r] = new HashSet<long>(hookBan);
			_routes[key] = r;
			r.State = Route.S.Pending;
			_pendingAbilities[r] = ab; _pendingFrom[r] = fromBottom; _pendingTo[r] = toBottom;
			_queue.Enqueue(r);
			return r;
		}

		private static readonly Dictionary<Route, MovementAbilities> _pendingAbilities = new();
		private static readonly Dictionary<Route, HashSet<long>> _pendingBan = new();
		private static readonly Dictionary<Route, Vector2> _pendingFrom = new(), _pendingTo = new();

		private static void Evict(uint now)
		{
			_evict.Clear();
			foreach (var kv in _routes) if (kv.Value != _active && kv.Value.State != Route.S.Pending && now - kv.Value.Born > 60 * 20) _evict.Add(kv.Key);
			if (_evict.Count == 0) foreach (var kv in _routes) if (kv.Value != _active && kv.Value.State != Route.S.Pending) { _evict.Add(kv.Key); break; }
			foreach (long k in _evict) _routes.Remove(k);
		}

		public override void PostUpdateNPCs()
		{
			if (LevyUtil.IsClient) return;
			if (_active == null && _queue.Count == 0) return;
			long t0 = Stopwatch.GetTimestamp();
			_big ??= new PathSearch(RouteWindowW, RouteWindowH, RouteWindowCells);
			if (_active == null)
			{
				while (_queue.Count > 0 && _active == null)
				{
					var r = _queue.Dequeue();
					if (!_pendingAbilities.TryGetValue(r, out var ab)) continue;
					Vector2 from = _pendingFrom[r], to = _pendingTo[r];
					_pendingAbilities.Remove(r); _pendingFrom.Remove(r); _pendingTo.Remove(r);
					_pendingBan.TryGetValue(r, out var ban); _pendingBan.Remove(r);
					if (_big.Begin(from, to, ab, centred: false, hWeight: RouteHWeight, hookBan: ban, allowHooks: false)) { _active = r; _hookBase = 0; _aFrom = from; _aTo = to; _aAb = ab; _aBan = ban; r.NodesUsed = 0; r.Ms = 0; Planned++; }
					else { r.State = Route.S.Failed; PlannedFailed++; Planned++; }
				}
			}
			if (_active != null)
			{
				var r = _active;
				// a slice is RouteNodesPerTick nodes at most, and at most RouteSliceMs of time: a hook-carrying search spends far longer on a node at a wall or an edge (the anchor fan), so the node budget alone would spike
				var st = PathSearch.State.Running; int spent = 0;
				while (st == PathSearch.State.Running && spent < RouteNodesPerTick && (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency < RouteSliceMs) { int chunk = _aAb != null && _aAb.HasWings ? 2 : RouteChunkNodes; st = _big.Step(chunk); spent += chunk; }   // an arc node scans hundreds of tiles: check the clock more often
				r.NodesUsed = _hookBase + _big.Expanded;
				if (st == PathSearch.State.Running && _big.Expanded >= (_aAb != null && _aAb.HasWings && !r.GridPass && PathSearch.Travel6On ? ArcPassMaxNodes : RouteMaxNodes)) { _big.Abandon(); st = _big.Status; }
				// a route on foot comes first: only when none reaches the goal does the search run again with the grappling hook's edges (a levy walks where it can)
				if (st != PathSearch.State.Running && st != PathSearch.State.Found && r.HookTiles > 0 && !r.HookPass)
				{
					r.HookPass = true; int used = r.NodesUsed;
					if (_big.Begin(_aFrom, _aTo, _aAb, centred: false, hWeight: RouteHWeight, hookBan: _aBan, allowHooks: true)) { _hookBase = used; st = PathSearch.State.Running; r.NodesUsed = used; }
					else st = _big.Status;
				}
				// a winged body's arcs come first; only when none reaches the goal does the search run again with the open-air flight grid added (the old way)
				if (st != PathSearch.State.Running && st != PathSearch.State.Found && _aAb != null && _aAb.HasWings && !r.GridPass)
				{
					r.GridPass = true; int used = r.NodesUsed;
					if (_big.Begin(_aFrom, _aTo, _aAb, centred: false, hWeight: RouteHWeight, hookBan: _aBan, allowHooks: false, gridFlight: true)) { _hookBase = used; st = PathSearch.State.Running; r.NodesUsed = used; }
					else st = _big.Status;
				}
				if (st != PathSearch.State.Running) Finish(r, st);
			}
			double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
			PlannedMs += ms; SlicesRun++; if (ms > MaxSliceMs) MaxSliceMs = ms;
		}


		private static void Finish(Route r, PathSearch.State st)
		{
			if (st == PathSearch.State.Found || st == PathSearch.State.Partial)
			{
				if (_big.BuildPath(r.Pts, r.Kinds)) { r.State = st == PathSearch.State.Found ? Route.S.Ready : Route.S.Partial; if (st == PathSearch.State.Found) PlannedFound++; else PlannedPartial++; }
				else { r.State = Route.S.Failed; PlannedFailed++; }
			}
			else { r.State = Route.S.Failed; PlannedFailed++; }
			PlannedNodes += r.NodesUsed;
			if (Log.Count < 400) { Point e = r.Pts.Count > 0 ? TileNav.FeetTile(r.Pts[r.Pts.Count - 1]) : r.Start; Log.Add($"route {r.State} jump={r.Jump} wings={r.Wings} hook={r.HookTiles} climbs={r.Kinds.FindAll(k => k == 3).Count} hooks={r.Kinds.FindAll(k => k == 1 || k == 2).Count} air={r.Kinds.FindAll(k => k == 4).Count} grid={r.GridPass} v={r.Variant} from=({r.Start.X},{r.Start.Y}) to=({r.Goal.X},{r.Goal.Y}) nodes={r.NodesUsed} pts={r.Pts.Count} end=({e.X},{e.Y}) status={st}"); }
			r.Born = Main.GameUpdateCount;
			_active = null;
		}

		/// <summary>Lab: forget every cached and queued route (a new run in the same boot).</summary>
		public static void LabReset() { _routes.Clear(); _queue.Clear(); _pendingAbilities.Clear(); _pendingBan.Clear(); _pendingFrom.Clear(); _pendingTo.Clear(); _active = null; }

		public override void OnWorldUnload() { _routes.Clear(); _queue.Clear(); _pendingAbilities.Clear(); _pendingBan.Clear(); _pendingFrom.Clear(); _pendingTo.Clear(); _active = null; }
	}
}
