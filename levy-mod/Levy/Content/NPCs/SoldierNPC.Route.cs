using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Following a planned long route (see <see cref="RoutePlanner"/>). The brain asks <see cref="RouteLeg"/> once per think for the point to aim at: the route waypoint about
	/// RouteLookaheadTiles on from where the soldier is, with the waypoints in between handed to the movement code as its path. While the plan is still being searched, or when none can be had,
	/// the caller falls back to the old hop toward the goal. Cost per think: a dictionary lookup and a scan of at most 60 waypoints.
	/// </summary>
	public partial class SoldierNPC
	{
		private Route _rt; private int _rtIdx; private Point _rtGoal; private uint _rtAsked; private int _rtVariant, _rtReplans;
		private Vector2 _rtProgPos; private int _rtProgTick; private Vector2 _rtLegSent = new Vector2(float.MinValue);
		private readonly List<Vector2> _rtLeg = new();
		private bool _pathFromRoute;
		/// <summary>The route's last waypoint is where this soldier stands and the goal is farther: nothing it can do gets it closer.</summary>
		internal bool RouteEndedShort { get; private set; }
		/// <summary>The best-effort route ran out: from here the soldier aims by the old rule (the planner's picture of the ground can be stricter than what the body manages), and gives up sooner if that gets nowhere.</summary>
		internal bool RouteExhausted { get; private set; }
		internal bool RouteActive => _rt != null && _rt.Usable;

		// ---- hook edges: stand at the cast point, fire the hook planner at the edge's target, ban the target when it comes to nothing ----
		private readonly List<long> _rtBan = new();
		private bool _rtHookWant; private Point _rtHookT; private Vector2 _rtHookGoal; private uint _rtHookSince, _rtHookLastTry; private int _rtHookTries;
		internal int RouteHookCasts, RouteHookBans;
		/// <summary>Tick the route last asked this body to fly an arc (thrust while the tank lasts); 0 = never. Movement reads it (<see cref="RouteFlying"/>).</summary>
		private uint _rtFlyTick;
		internal bool RouteFlying => _rtFlyTick != 0 && Main.GameUpdateCount - _rtFlyTick <= (uint)RouteFlyHoldTicks;
		internal static long StatArcStarts, StatArcFuelOut;

		internal void RouteReset() { _rtFlyTick = 0; ClimbCancel(); _rtBan.Clear(); _rtHookWant = false; RouteExhausted = false; _rt = null; _rtIdx = 0; _rtVariant = 0; _rtReplans = 0; _rtLegSent = new Vector2(float.MinValue); RouteEndedShort = false; _pathFromRoute = false; }

		/// <summary>Hands the movement code the waypoints of the current leg as its path (no search): the same list a local A* would have produced.</summary>
		private void FeedPath(List<Vector2> pts, Vector2 goal)
		{
			_path.Clear(); _path.AddRange(pts); _pathGoal = goal; _pathTimer = 0; _pathFailTimer = 0; _pathFromRoute = true;
		}

		/// <summary>
		/// The point to aim at on the way to <paramref name="goalBottom"/>. False = no usable route yet (still being planned, none found, or the trip is short enough to walk directly): the caller aims by its own rule.
		/// Sets <see cref="RouteEndedShort"/> when the soldier has followed a best-effort route to its end.
		/// </summary>
		internal bool RouteLeg(int now, Vector2 goalBottom, out Vector2 leg)
		{
			leg = goalBottom; RouteEndedShort = false;
			if (TravelLegacy.On || !LevyUtilIsServerOrSingle() || RouteExhausted) return false;
			// up a wall face: the climb owns the body; a climb that came to nothing bans its lip and the route is asked for again
			if (_clmFailed) { _clmFailed = false; RouteHookGiveUp(_clmTile); return false; }
			if (_clmOn) { _rtProgPos = NPC.Center; _rtProgTick = now; leg = _clmTarget; return true; }
			// hanging on a chain of casts up a face: the climb owns the body, the route waits for it to land
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown && _rt != null && _rtHookWant) { _rtProgPos = NPC.Center; _rtProgTick = now; leg = _rtHookGoal; return true; }
			Vector2 me = NPC.Bottom;
			float dxT = Math.Abs(goalBottom.X - me.X) / 16f, dyT = (goalBottom.Y - me.Y) / 16f;
			if (dxT <= RouteMinTripTiles && Math.Abs(dyT) <= RouteMinTripRise && !(_rt != null && RouteFlying && NPC.velocity.Y != 0f)) { if (_rt != null) RouteReset(); return false; }   // (an arc in the air is flown to its landing)
			// a trip whose box (wide, or tall: a shaft down to the Underworld) does not fit the planner's window is routed in hops: the standable cell nearest the point of the straight line to the goal that does
			{
				int ddx = (int)dxT, ddy = (int)Math.Abs(dyT);
				bool fits(double f) { double wx = ddx * f + 40, wy = ddy * f + 70; return ddx * f <= RouteMaxHopTiles && (!PathSearch.Travel8On || (wx <= RouteWindowW && wy <= RouteWindowH && wx * wy <= RouteWindowCells)); }
				if (!fits(1.0))
				{
					double f = 1.0; while (f > 0.05 && !fits(f)) f *= 0.9;
					Point mt = TileNav.FeetTile(me); int hx = mt.X + (int)Math.Round((goalBottom.X - me.X) / 16f * f), hy = mt.Y + (int)Math.Round(dyT * f), found = -1;
					for (int d = 0; d <= 80 && found < 0; d++)
						for (int sgn = -1; sgn <= 1 && found < 0; sgn += 2)
							if (TileNav.InBounds(hx, hy + sgn * d) && TileNav.IsStandable(hx, hy + sgn * d)) found = hy + sgn * d;
					if (found < 0) { if (!TileNav.NearestStandable(hx, hy, 60, out Point np)) return false; hx = np.X; found = np.Y; }
					goalBottom = TileNav.BottomCentre(hx, found);
				}
			}
			Point g = TileNav.FeetTile(goalBottom);

			// progress watch: a soldier that stops getting anywhere along the route asks for a fresh one from where it stands
			if (now - _rtProgTick >= RouteStallTicks)
			{
				bool stalled = Vector2.DistanceSquared(NPC.Center, _rtProgPos) < 40f * 40f;
				_rtProgPos = NPC.Center; _rtProgTick = now;
				if (stalled && !_rtHookWant && _rt != null && _rt.Usable && _rtReplans < RouteReplanMax) { _rtReplans++; _rtVariant = (_rtVariant + 1) & 7; _rt = null; }
			}

			if (_rt == null || _rtGoal != g)
			{
				if (_rt != null && _rtGoal != g) RouteReset();
				if ((uint)now - _rtAsked < 20u && _rt == null) return false;
				_rtAsked = (uint)now;
				_rt = RoutePlanner.Get(me, goalBottom, Abilities, _rtVariant, _rtBan);
				_rtGoal = g; _rtIdx = 0; _rtLegSent = new Vector2(float.MinValue);
				if (_rt == null) return false;
			}
			if (_rt.State == Route.S.Pending) return false;
			if (_rt.State == Route.S.Failed)
			{
				if (Main.GameUpdateCount - _rt.Born > (uint)RouteRetryTicks) _rt = null;
				return false;
			}
			var pts = _rt.Pts; var kinds = _rt.Kinds;
			int n = pts.Count;
			if (n == 0) return false;
			bool hasKinds = kinds.Count == n;

			// nearest waypoint in the window ahead of the last one reached
			int lo = Math.Max(0, _rtIdx - 3), hi = Math.Min(n - 1, _rtIdx + 70);
			int best = -1; float bestD = float.MaxValue;
			for (int i = lo; i <= hi; i++)
			{
				float d = Vector2.DistanceSquared(pts[i], me) - i * 0.5f;
				if (d < bestD) { bestD = d; best = i; }
			}
			if (best < 0) return false;
			float offTiles = Vector2.Distance(pts[best], me) / 16f;
			if (offTiles > RouteOffRouteTiles)
			{
				// off the route (knocked away, a fight, a fall): plan again from here, at most a few times
				if (_rtReplans < RouteReplanMax + 3) { _rtReplans++; _rtVariant = (_rtVariant + 1) & 7; _rt = null; _rtLegSent = new Vector2(float.MinValue); }
				return false;
			}
			_rtIdx = Math.Max(_rtIdx, best);
			while (_rtIdx < n - 1 && Vector2.DistanceSquared(pts[_rtIdx], me) < 36f * 36f) _rtIdx++;

			// the next step is a wall climb from the previous waypoint (the foot of the face): walk to the foot, then climb
			if (hasKinds && kinds[_rtIdx] == 3 && _rtIdx > 0)
			{
				Vector2 foot = pts[_rtIdx - 1];
				_rtHookWant = false;
				bool atFoot = Math.Abs(foot.X - me.X) < 40f && Math.Abs(foot.Y - me.Y) < 56f;
				if (atFoot)
				{
					if (ClimbStart(pts[_rtIdx], foot)) { leg = pts[_rtIdx]; return true; }
					RouteHookGiveUp(TileNav.FeetTile(pts[_rtIdx])); return false;
				}
				if (_rtLegSent != foot || _path.Count == 0) { _rtLeg.Clear(); _rtLeg.Add(foot); FeedPath(_rtLeg, foot); _rtLegSent = foot; }
				leg = foot; return true;
			}

			// the next step is a hook cast from the previous waypoint: walk to it, then cast at the edge's target
			if (hasKinds && (kinds[_rtIdx] == 1 || kinds[_rtIdx] == 2) && _rtIdx > 0)
			{
				Vector2 cast = pts[_rtIdx - 1];
				Point tile = TileNav.FeetTile(pts[_rtIdx]);
				if (!_rtHookWant || _rtHookT != tile) { _rtHookWant = false; _rtHookT = tile; _rtHookGoal = pts[_rtIdx]; _rtHookSince = Main.GameUpdateCount; _rtHookTries = 0; }
				bool atCast = Math.Abs(cast.X - me.X) < 40f && Math.Abs(cast.Y - me.Y) < 56f;
				if (atCast) { if (!_rtHookWant) _rtHookSince = Main.GameUpdateCount; _rtHookWant = true; }
				if (_rtHookWant && (_rtHookTries >= 3 || Main.GameUpdateCount - _rtHookSince > (uint)RouteHookWaitTicks)) { RouteHookGiveUp(tile); return false; }
				if (_rtLegSent != cast || _path.Count == 0) { _rtLeg.Clear(); _rtLeg.Add(cast); FeedPath(_rtLeg, cast); _rtLegSent = cast; }
				leg = cast; return true;
			}
			_rtHookWant = false;

			if (Vector2.DistanceSquared(pts[n - 1], me) < 56f * 56f)
			{
				if (_rt.State == Route.S.Partial) { RouteEndedShort = true; RouteExhausted = true; _path.Clear(); return false; }
				leg = goalBottom; return true; // the last stretch: the goal itself
			}

			// the next waypoints are the air points of an arc: walk to the takeoff cell, then fly them to the landing (thrust while the tank lasts, a glide after)
			if (hasKinds && kinds[_rtIdx] == 4)
			{
				_rtHookWant = false;
				Vector2 takeoff = _rtIdx > 0 ? pts[_rtIdx - 1] : me;
				bool midArc = _rtIdx > 0 && kinds[_rtIdx - 1] == 4;
				bool atTakeoff = Math.Abs(takeoff.X - me.X) < 40f && Math.Abs(takeoff.Y - me.Y) < 56f;
				if (midArc || NPC.velocity.Y != 0f || _flying || atTakeoff)
				{
					int e = _rtIdx; while (e + 1 < n && kinds[e + 1] == 4) e++;
					if (!RouteFlying) { StatArcStarts++; TakeoffTalk(); }
					_rtFlyTick = Main.GameUpdateCount;
					leg = pts[e];
					if (leg != _rtLegSent || _path.Count == 0)
					{
						_rtLeg.Clear();
						for (int i = _rtIdx; i <= e; i++) _rtLeg.Add(pts[i]);
						FeedPath(_rtLeg, leg);
						_rtLegSent = leg;
					}
					return true;
				}
				if (_rtLegSent != takeoff || _path.Count == 0) { _rtLeg.Clear(); _rtLeg.Add(takeoff); FeedPath(_rtLeg, takeoff); _rtLegSent = takeoff; }
				leg = takeoff; return true;
			}
			// the leg: waypoints up to RouteLookaheadTiles of polyline ahead
			float acc = 0f; int j = _rtIdx; Vector2 prev = me;
			for (; j < n; j++)
			{
				acc += Vector2.Distance(prev, pts[j]) / 16f; prev = pts[j];
				if (acc >= RouteLookaheadTiles) break;
				if (hasKinds && j + 1 < n && kinds[j + 1] != 0) break;   // the leg ends at a hook cast point, a climb foot or a takeoff cell
			}
			if (j >= n) j = n - 1;
			leg = pts[j];
			if (j == n - 1 && _rt.State == Route.S.Ready) leg = goalBottom;
			if (Vector2.DistanceSquared(leg, _rtLegSent) > 16f * 16f || _path.Count == 0)
			{
				_rtLeg.Clear();
				for (int i = _rtIdx; i <= j; i++) _rtLeg.Add(pts[i]);
				FeedPath(_rtLeg, leg);
				_rtLegSent = leg;
			}
			return true;
		}

		/// <summary>Movement tick: a route edge asked for a hook and the soldier stands at the cast point. Starts the real hook planner at the edge's target (SoldierNPC.Hook.cs).</summary>
		private void RouteHookTick(bool grounded)
		{
			if (!_rtHookWant || _hkPhase != HookPhase.Idle || _hkStepUntil != 0) return;
			if (!HookUsable) { RouteHookGiveUp(_rtHookT); return; }
			if (Main.GameUpdateCount < _rtHookLastTry + (uint)RouteHookRetryTicks || !grounded || NPC.velocity.Y > 2.2f || NPC.wet || _flying) return;   // the edge's own retry spacing replaces the general back-off of the local triggers
			if (!SoldierHooks.TakePlanBudget()) return;
			_rtHookTries++; _rtHookLastTry = Main.GameUpdateCount; HookLog($"route try {_rtHookTries} target={_rtHookT.X},{_rtHookT.Y}");
			try { HookStart(HookReason.Route, _rtHookGoal); }
			catch (Exception e)
			{
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: route hook failed on soldier '" + SoldierName + "': " + e);
				try { HookReset(true); } catch (Exception) { }
				HookBackoff();
			}
			if (_hkPhase != HookPhase.Idle) RouteHookCasts++;
		}

		/// <summary>The hook edge came to nothing: the target is banned from the next search and the route is asked for again from here.</summary>
		private void RouteHookGiveUp(Point target)
		{
			HookLog($"route ban target={target.X},{target.Y} tries={_rtHookTries}"); _rtHookWant = false; RouteHookBans++;
			if (_rtBan.Count < RouteHookBanMax) _rtBan.Add(PathSearch.HookKey(target.X, target.Y));
			_rt = null; _rtLegSent = new Vector2(float.MinValue); _path.Clear();
			if (_rtBan.Count >= RouteHookBanMax) { RouteExhausted = true; RouteEndedShort = true; }
		}

		/// <summary>Harness telemetry: the route state and where the soldier is on it.</summary>
		internal string DebugRoute()
		{
			if (_rt == null) return "route=none";
			string nx = "";
			for (int i = _rtIdx; i < Math.Min(_rt.Pts.Count, _rtIdx + 6); i++) { var q = TileNav.FeetTile(_rt.Pts[i]); nx += $"({q.X - TileNav.FeetTile(NPC.Bottom).X},{q.Y - TileNav.FeetTile(NPC.Bottom).Y})"; }
			string p0 = _path.Count > 0 ? $"({(_path[0].X - NPC.Center.X) / 16f:0.0},{(_path[0].Y - NPC.Bottom.Y) / 16f:0.0})" : "none";
			string kd = ""; if (_rt.Kinds.Count == _rt.Pts.Count) for (int i = _rtIdx; i < Math.Min(_rt.Pts.Count, _rtIdx + 6); i++) kd += _rt.Kinds[i];
			return $"dodge={DodgeTimers} threats={ThreatField.Snapshot().Count} hkPhase={_hkPhase} stage[{DbgStage}] lavaL={LavaAhead(-1)} lavaR={LavaAhead(1)} jump={DbgJump} blkWhy={BlockWhy} dropWhy={DropWhy} flyWhy={FlyWhy} kinds={kd} routeFlying={RouteFlying} p0={p0} whyM={WhyM} route={_rt.State} hookWant={_rtHookWant}/{_rtHookTries} bans={_rtBan.Count} idx={_rtIdx}/{_rt.Pts.Count} replans={_rtReplans} fromRoute={_pathFromRoute} pathN={_path.Count} goal=({MoveGoal.X / 16f:0},{MoveGoal.Y / 16f:0}) next={nx}";
		}

		private static bool LevyUtilIsServerOrSingle() => global::Levy.Common.Util.LevyUtil.IsServerOrSingle;
	}
}
