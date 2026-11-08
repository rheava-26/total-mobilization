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
		public Point Start, Goal; public int Variant; public uint Born;
		public int NodesUsed; public double Ms; public int Jump; public bool Wings;
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
		private static Route _active;
		private static readonly List<long> _evict = new();
		public static long Planned, PlannedNodes, PlannedFound, PlannedPartial, PlannedFailed, SliceTicks, SlicesRun; public static double PlannedMs, MaxSliceMs;

		public static readonly List<string> Log = new();
		public static void StatReset() { Log.Clear(); Planned = PlannedNodes = PlannedFound = PlannedPartial = PlannedFailed = SliceTicks = SlicesRun = 0; PlannedMs = 0; MaxSliceMs = 0; }
		public static string StatSummary() => $"routes={Planned} found={PlannedFound} partial={PlannedPartial} failed={PlannedFailed} nodes={PlannedNodes} ms={PlannedMs:0.0} maxSliceMs={MaxSliceMs:0.00} queued={_queue.Count}";

		private static long Key(Point goal, Point start, int jump, bool wings, int variant)
		{
			long k = goal.X; k = k * 4001 + goal.Y; k = k * 1009 + (start.X / RouteStartBucketTiles); k = k * 401 + (start.Y / RouteStartBucketTiles);
			k = k * 17 + Math.Clamp(jump, 0, 15); k = k * 2 + (wings ? 1 : 0); k = k * 8 + (variant & 7);
			return k;
		}

		/// <summary>The route for this trip: an existing one (any state) or a new Pending one queued for the planner. Null when the planner is full.</summary>
		public static Route Get(Vector2 fromBottom, Vector2 toBottom, MovementAbilities ab, int variant = 0)
		{
			if (LevyUtil.IsClient) return null;
			ab ??= new MovementAbilities();
			Point s = TileNav.FeetTile(fromBottom), t = TileNav.FeetTile(toBottom);
			long key = Key(t, s, ab.JumpTiles, ab.HasWings, variant);
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
			r = new Route { Start = s, Goal = t, Variant = variant, Born = now, Jump = ab.JumpTiles, Wings = ab.HasWings };
			_routes[key] = r;
			r.State = Route.S.Pending;
			_pendingAbilities[r] = ab; _pendingFrom[r] = fromBottom; _pendingTo[r] = toBottom;
			_queue.Enqueue(r);
			return r;
		}

		private static readonly Dictionary<Route, MovementAbilities> _pendingAbilities = new();
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
			_big ??= new PathSearch(RouteWindowW, RouteWindowH);
			if (_active == null)
			{
				while (_queue.Count > 0 && _active == null)
				{
					var r = _queue.Dequeue();
					if (!_pendingAbilities.TryGetValue(r, out var ab)) continue;
					Vector2 from = _pendingFrom[r], to = _pendingTo[r];
					_pendingAbilities.Remove(r); _pendingFrom.Remove(r); _pendingTo.Remove(r);
					if (_big.Begin(from, to, ab, centred: false, hWeight: RouteHWeight)) { _active = r; r.NodesUsed = 0; r.Ms = 0; Planned++; }
					else { r.State = Route.S.Failed; PlannedFailed++; Planned++; }
				}
			}
			if (_active != null)
			{
				var r = _active;
				var st = _big.Step(RouteNodesPerTick);
				r.NodesUsed = _big.Expanded;
				if (st == PathSearch.State.Running && _big.Expanded >= RouteMaxNodes) { _big.Abandon(); st = _big.Status; }
				if (st != PathSearch.State.Running)
				{
					if (st == PathSearch.State.Found || st == PathSearch.State.Partial)
					{
						if (_big.BuildPath(r.Pts)) { r.State = st == PathSearch.State.Found ? Route.S.Ready : Route.S.Partial; if (st == PathSearch.State.Found) PlannedFound++; else PlannedPartial++; }
						else { r.State = Route.S.Failed; PlannedFailed++; }
					}
					else { r.State = Route.S.Failed; PlannedFailed++; }
					PlannedNodes += r.NodesUsed;
					if (Log.Count < 400) { Point e = r.Pts.Count > 0 ? TileNav.FeetTile(r.Pts[r.Pts.Count - 1]) : r.Start; Log.Add($"route {r.State} jump={r.Jump} wings={r.Wings} v={r.Variant} from=({r.Start.X},{r.Start.Y}) to=({r.Goal.X},{r.Goal.Y}) nodes={r.NodesUsed} pts={r.Pts.Count} end=({e.X},{e.Y}) status={st}"); }
					r.Born = Main.GameUpdateCount;
					_active = null;
				}
			}
			double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
			PlannedMs += ms; SlicesRun++; if (ms > MaxSliceMs) MaxSliceMs = ms;
		}

		public override void OnWorldUnload() { _routes.Clear(); _queue.Clear(); _pendingAbilities.Clear(); _pendingFrom.Clear(); _pendingTo.Clear(); _active = null; }
	}
}
