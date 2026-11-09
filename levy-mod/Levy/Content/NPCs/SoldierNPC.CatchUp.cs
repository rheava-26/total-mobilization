using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Systems;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// OFF-SCREEN CATCH-UP for a map-point order (Follow already teleports to catch up). When no active player could see the levy (outside the 2400x1400 px box around every one of them) and it has stayed
	/// within a few tiles of one spot for six seconds, it hops ahead and never into view: along its planned route to the furthest waypoint that is standable and out of every player's sight; with a route that ran out
	/// short (or none) to a standable spot at the goal, or, when a player can see the goal, to the nearest out-of-sight point on the way, and walks the rest. Spots have floor, three clear rows, no liquid.
	/// </summary>
	public partial class SoldierNPC
	{
		private Vector2 _cuAnchor; private int _cuTick, _cuLastThink = int.MinValue;
		/// <summary>Progress watch: the nearest the body has been to the goal (tiles) and when that last improved by CatchUpProgressTiles. A body looping casts in a pit moves a lot and gets nowhere.</summary>
		private float _cuBestDist = float.MaxValue; private int _cuBestTick; private Vector2 _cuGoalSeen;
		/// <summary>Hops made by this body; and over all bodies: hops, hops whose origin or landing a player could see (always 0), and the time they cost (ticks of Stopwatch).</summary>
		internal int CatchUpHopCount;
		internal static string CatchUpLast = ""; internal static int CatchUpTries;
		internal static int CatchUpHops, CatchUpHopsSeen; internal static long CatchUpTicks;

		/// <summary>Called by the map march every think. True when the body hopped this think (the caller starts over from the new spot).</summary>
		internal bool CatchUpTick(int now, Vector2 goalBottom)
		{
			if (TravelLegacy.On || !LevyUtil.IsServerOrSingle || OfficerMode) return false;
			bool interrupted = _cuLastThink == int.MinValue || now - _cuLastThink > 90;
			if (interrupted) { _cuBestDist = float.MaxValue; _cuBestTick = now; }
			if (interrupted || Vector2.DistanceSquared(NPC.Center, _cuAnchor) > CatchUpStuckTiles * 16f * CatchUpStuckTiles * 16f)
			{
				_cuAnchor = NPC.Center; _cuTick = now;
				if (interrupted) { _cuLastThink = now; return false; }
			}   // moving, or the march was interrupted (a fight): start counting again
			_cuLastThink = now;
			// progress toward the goal: a body that moves about (casts and falls inside a pit) stays 'unstuck' by the spot test but is going nowhere
			float distT = Vector2.Distance(NPC.Center, goalBottom) / 16f;
			if (Vector2.DistanceSquared(goalBottom, _cuGoalSeen) > 8f * 16f * 8f * 16f) { _cuGoalSeen = goalBottom; _cuBestDist = float.MaxValue; }
			if (distT < _cuBestDist - CatchUpProgressTiles) { _cuBestDist = distT; _cuBestTick = now; }
			bool noProgress = now - _cuBestTick >= CatchUpNoProgressTicks;
			if (now - _cuTick < CatchUpStuckTicks && !noProgress) return false;
			_cuTick = now - CatchUpStuckTicks + 120; if (noProgress) _cuBestTick = now - CatchUpNoProgressTicks + 120;   // nothing found this time: look again in two seconds
			if ((_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) || _hkStepUntil != 0 || ClimbActive || NPC.wet) return false;
			if (OffscreenHop.Seen(NPC.Center)) return false;
			long t0 = Stopwatch.GetTimestamp();
			if (!ChooseHop(goalBottom, out Vector2 spot, out int pick)) { CatchUpTicks += Stopwatch.GetTimestamp() - t0; return false; }
			bool seenBefore = OffscreenHop.Seen(NPC.Center), seenAfter = OffscreenHop.Seen(spot);
			FollowerTravel.HopTo(this, spot);
			CatchUpHopCount++; CatchUpHops++; if (seenBefore || seenAfter) CatchUpHopsSeen++;
			_cuAnchor = NPC.Center; _cuTick = now; _cuBestDist = float.MaxValue; _cuBestTick = now;
			if (pick >= 0 && _rt != null && _rt.Usable && pick < _rt.Pts.Count) { _rtIdx = pick; _rtLegSent = new Vector2(float.MinValue); _path.Clear(); _rtProgPos = NPC.Center; _rtProgTick = now; }
			else RouteReset();
			CatchUpTicks += Stopwatch.GetTimestamp() - t0;
			return true;
		}

		/// <summary>A standing spot near a point that no player can see: floor, three clear rows, no liquid, in sight of the point (FollowerTravel.FindStandingSpot), kept out of a boss's reach.</summary>
		private static bool HopLanding(Vector2 nearBottom, int radius, out Vector2 spot)
		{
			spot = nearBottom;
			Vector2 near = nearBottom - new Vector2(0f, BodyHeight * 0.5f);   // a body's centre: the line of sight is checked to it, and a floor's top edge is inside the floor
			if (!FollowerTravel.FindStandingSpot(near, radius, out Vector2 s)) return false;
			s = RosterSystem.SafeSpawnPosition(s);
			if (OffscreenHop.Seen(s)) return false;
			spot = s; return true;
		}

		private bool ChooseHop(Vector2 goalBottom, out Vector2 spot, out int pick)
		{
			spot = default; pick = -1; CatchUpTries++;
			Vector2 me = NPC.Bottom;
			var rt = _rt;
			bool routeOk = rt != null && rt.Usable && rt.Pts.Count > 0;
			bool routeRanOut = routeOk && rt.State == Route.S.Partial && (RouteEndedShort || RouteExhausted || Vector2.DistanceSquared(rt.Pts[rt.Pts.Count - 1], me) < 56f * 56f);
			if (routeOk && !routeRanOut && !RouteExhausted)
			{
				// along the route: the furthest waypoint that has a standing spot out of every player's sight
				var pts = rt.Pts; Vector2 found = default;
				int i = OffscreenHop.PickWaypoint(pts.Count, Math.Max(0, _rtIdx), k =>
				{
					if (Vector2.DistanceSquared(pts[k], me) < 32f * 32f) return false;
					if (!HopLanding(pts[k], 3, out Vector2 s)) return false;
					found = s; return true;
				});
				if (i < 0) { CatchUpLast = $"route pick none idx={_rtIdx}/{pts.Count} state={rt.State}"; return false; }
				spot = found; pick = i; return true;
			}
			// the route ran out short, is not found, or is not there: a spot at the goal; when a player can see the goal, the nearest unseen standable point on the way
			if (HopLanding(goalBottom, CatchUpGoalSpotTiles, out spot)) return true;
			Vector2 landing = default;
			bool ok = OffscreenHop.PickOnTheWay(me, goalBottom, CatchUpMinHopTiles, q =>
			{
				if (!HopLanding(q, CatchUpWaySpotTiles, out Vector2 s)) return false;
				if (Vector2.DistanceSquared(s, me) < CatchUpMinHopTiles * 16f * CatchUpMinHopTiles * 16f) return false;
				if (!OffscreenHop.RoomIsOpen(TileNav.FeetTile(s))) return false;
				landing = s; return true;
			}, out _);
			if (!ok) { CatchUpLast = $"no goal spot, no way point route={(rt == null ? "none" : rt.State.ToString())} exhausted={RouteExhausted}"; return false; }
			spot = landing; return true;
		}
	}
}
