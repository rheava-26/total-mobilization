using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Movement
{
	/// <summary>The tile queries the planner needs, so it can be driven by a synthetic map in a test.</summary>
	public interface IHookWorld
	{
		/// <summary>A tile that blocks the body (walls, floors). One-way platforms are <see cref="Platform"/>, not this.</summary>
		bool Solid(int x, int y);
		/// <summary>A one-way platform: blocks a body only from above.</summary>
		bool Platform(int x, int y);
		/// <summary>A tile a hook latches onto (the hook AI's own test: solid tile, platforms included).</summary>
		bool Latchable(int x, int y);
		bool Lava(int x, int y);
		/// <summary>A sloped or half-height tile: the body slides along it instead of stopping, and the planner's block model does not know how far.</summary>
		bool Sloped(int x, int y);
	}

	/// <summary>The live world (<see cref="TileNav"/> plus the hook AI's latch test).</summary>
	public sealed class TileHookWorld : IHookWorld
	{
		public static readonly TileHookWorld Instance = new();
		public bool Solid(int x, int y) => TileNav.IsSolid(x, y);
		public bool Platform(int x, int y) => TileNav.IsPlatform(x, y) && !TileNav.IsSolid(x, y);
		public bool Latchable(int x, int y)
		{
			if (!TileNav.InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.HasUnactuatedTile && Main.tileSolid[t.TileType];
		}
		public bool Lava(int x, int y) => TileNav.HasLava(x, y);
		public bool Sloped(int x, int y)
		{
			if (!TileNav.InBounds(x, y)) return false;
			Tile t = Main.tile[x, y];
			return t.HasUnactuatedTile && (t.Slope != Terraria.ID.SlopeType.Solid || t.IsHalfBlock);
		}
	}

	public enum HookGoalKind : byte
	{
		/// <summary>Land on standable ground as near the goal as possible (ledge, gap, ceiling climb, flight).</summary>
		Ground,
		/// <summary>Reach a height: hang on the anchor as near the goal point as possible, with a safe drop afterwards.</summary>
		Altitude,
	}

	public struct HookParams
	{
		public float ReachPx, PullPx; public int Count; public HookKind Kind;
		public float BodyW, BodyH, JumpSpeed, Gravity;
		public int Rays, ReleaseCandidates, MaxAnchors, MaxHooks;
		/// <summary>After the release the body holds the direction key toward the goal (a route edge: the climber who lets go at a lip steps onto it) instead of drifting with air drag.</summary>
		public bool AirSteer;
		/// <summary>A climbing plan (route edge, wall, goal above): the fan is joined by rays aimed at the exposed tiles around the body (so the lip of a wall is found, not only what a coarse fan happens to hit), a hang is not checked for a safe drop, and a release at the anchor is a hook-jump (a full jump kick).</summary>
		public bool Climb;
		/// <summary>The upward speed of a hook-jump (px/tick) for a climbing plan; 0 = JumpSpeed.</summary>
		public float KickSpeed;
		public static HookParams For(HookInfo info, float bodyW, float bodyH, float jumpSpeed, float gravity) => new()
		{
			ReachPx = info.ReachPx, PullPx = info.PullPx, Count = info.Count, Kind = info.Kind, BodyW = bodyW, BodyH = bodyH, JumpSpeed = jumpSpeed, Gravity = gravity,
			Rays = LevyConstants.Hooks.FanRays, ReleaseCandidates = LevyConstants.Hooks.ReleaseCandidates, MaxAnchors = LevyConstants.Hooks.MaxAnchorsSimulated, MaxHooks = LevyConstants.Hooks.MaxHooksPerPlan,
		};
	}

	/// <summary>One planned cast: where to aim, when to let go, where the body should come down.</summary>
	public sealed class HookPlan
	{
		public readonly List<Vector2> Anchors = new();
		/// <summary>Where to aim each cast: a point on the fan ray that found the anchor (the hook flies the same straight line, so it sticks where the planner said).</summary>
		public readonly List<Vector2> Aims = new();
		/// <summary>Reel ticks until the release (counted from the tick every planned hook is latched).</summary>
		public int ReleaseTick;
		/// <summary>Distance (px, body centre to the anchors' centroid) at which to let go.</summary>
		public float ReleaseDist;
		/// <summary>Body bottom-centre at the release and where it comes down (ground plans).</summary>
		public Vector2 ReleasePos, Landing;
		public bool Landed;
		/// <summary>An altitude plan: hang at the anchor until told to let go (the drop was checked safe).</summary>
		public bool Hang;
		public float GoalDist, Gain, Score;
		/// <summary>Ticks of the simulated reel that the body spent touching a sloped tile (it drags along it instead of travelling clear). Each costs score.</summary>
		public int SlopeTicks;
		public List<Vector2> Path;
		public int ReelTicks, FlightTicks;
		public HookGoalKind Kind;
		/// <summary>A plan is worth casting when it lands (or hangs) nearer the goal AND its simulated reel does not spend more than a brush of ticks dragging along a slope (the body would not make the progress the simulation shows).</summary>
		public bool Beats(float arrivePx, float minGainPx) => (Landed || Hang) && SlopeTicks <= LevyConstants.Hooks.MaxSlopeDragTicks && (GoalDist <= arrivePx || Gain >= minGainPx);
	}

	/// <summary>
	/// Plans a grapple. Pure: it reads the world through <see cref="IHookWorld"/> and has no state.
	/// <para/>Fans <see cref="HookParams.Rays"/> rays across the upper half-plane, each to the first latchable tile within reach (the hook AI latches the first solid tile it overlaps), keeps the
	/// <see cref="HookParams.MaxAnchors"/> most promising, and for each (also pairs, for a multi-hook item) simulates the vanilla reel: every tick the body is given velocity toward the anchors' centroid
	/// at min(distance, pull) (Player.GetGrapplingForces) and moved with tile collision. At <see cref="HookParams.ReleaseCandidates"/> points along the reel it simulates the release (the reel velocity is kept, with the vanilla
	/// jump kick when the reel was level) and the ballistic flight (gravity, terminal speed, air drag on x) to a landing. The plan that lands on standable ground nearest the goal wins; flights through lava and
	/// landings far below the start are refused.
	/// </summary>
	public static class HookPlanner
	{
		public static long LastSteps;
		/// <summary>A pair of hooks has to beat the best single by this much (px of score) to be worth the second cast. The harness lowers it to force pairs.</summary>
		public static float PairMargin = 4f;
		private static readonly float[] DirectedHeightsTiles = { 4f, 8f, 12f, 16f, 20f, 25f };
		private const int DirectedRays = 18;
		/// <summary>Harness: keep the bottom-centre path (reel, then flight) on each plan.</summary>
		public static bool Trace; public static readonly List<string> DiagLog = new();
		/// <summary>Why candidates were dropped in the last plan (harness diagnostics): anchors, reel sims, releases tried, landings missed, lava, deep drops, unstandable.</summary>
		public static int DAnchors, DSims, DReleases, DNoLand, DLava, DDeep, DUnstand, DReelShort;
		public static string Describe() => $"anchors={DAnchors} sims={DSims} rel={DReleases} noLand={DNoLand} lava={DLava} deep={DDeep} unstand={DUnstand} reelShort={DReelShort}";

		public static HookPlan Plan(IHookWorld w, Vector2 bottom, in HookParams p, Vector2 goalBottom, HookGoalKind kind, float goalDistNow)
		{
			if (p.ReachPx < 64f || p.PullPx <= 0f || ((p.Kind == HookKind.StandOff || p.Kind == HookKind.Spring) && !HookInfo.LabAllowAll)) return null;
			DiagLog.Clear(); LastSteps = 0; DAnchors = DSims = DReleases = DNoLand = DLava = DDeep = DUnstand = DReelShort = 0;
			Vector2 c0 = bottom - new Vector2(0f, p.BodyH * 0.5f);
			var anchors = FanAnchors(w, c0, p, goalBottom, kind);
			DAnchors = anchors.Count;
			if (anchors.Count == 0) return null;
			HookPlan best = null;
			// singles
			var singles = new List<(HookPlan plan, int i)>(anchors.Count);
			for (int i = 0; i < anchors.Count; i++)
			{
				var one = new List<(Vector2, Vector2)>(1) { anchors[i] };
				var cand = Simulate(w, bottom, p, one, goalBottom, kind, goalDistNow);
				if (cand != null) { singles.Add((cand, i)); if (best == null || cand.Score < best.Score) best = cand; }
			}
			// the two best anchors get every release tick tried (a marginal crossing hangs on one tick)
			singles.Sort((x, y) => x.plan.Score.CompareTo(y.plan.Score));
			for (int k = 0; k < Math.Min(2, singles.Count); k++)
			{
				var one = new List<(Vector2, Vector2)>(1) { anchors[singles[k].i] };
				var cand = Simulate(w, bottom, p, one, goalBottom, kind, goalDistNow, true);
				if (cand != null && cand.Score < best.Score) best = cand;
			}
			// pairs for a multi-hook item: the two nearest-to-best singles around the winner
			if (p.MaxHooks >= 2 && p.Count >= 2 && best != null && !p.Climb)   // a climb casts one hook at a time (a pair that does not both latch strands the body)
			{
				var chosen = (best.Anchors[0], best.Aims[0]);
				for (int i = 0; i < anchors.Count; i++)
				{
					if ((anchors[i].a - chosen.Item1).LengthSquared() < 48f * 48f) continue;       // a second anchor at least three tiles off
					if ((anchors[i].a - chosen.Item1).LengthSquared() > 224f * 224f) continue;     // and not so far the pair pulls somewhere new
					var two = new List<(Vector2, Vector2)>(2) { chosen, anchors[i] };
					var cand = Simulate(w, bottom, p, two, goalBottom, kind, goalDistNow);
					if (cand != null && cand.Score < best.Score - PairMargin) best = cand;           // a pair has to be clearly better to be worth the second cast
				}
			}
			return best;
		}

		// ---------------------------------------------------------------------------------------------
		// the fan
		// ---------------------------------------------------------------------------------------------
		private static List<(Vector2 a, Vector2 aim)> FanAnchors(IHookWorld w, Vector2 c0, in HookParams p, Vector2 goal, HookGoalKind kind)
		{
			var found = new List<(Vector2 a, Vector2 aim, float h)>(p.Rays);
			var seen = new HashSet<long>();
			int extra = kind == HookGoalKind.Ground ? DirectedRays : 0;
			for (int k = 0; k < p.Rays + extra; k++)
			{
				Vector2 dir;
				if (k < p.Rays)
				{
					float th = -MathHelper.Pi + (k + 0.5f) * MathHelper.Pi / p.Rays; // -176 .. -4 degrees: left horizontal, over the top, right horizontal
					dir = new(MathF.Cos(th), MathF.Sin(th));
				}
				else
				{
					// rays aimed at points above the goal (a handful of heights, three offsets): the ones that clear a ledge's lip, which a coarse fan misses
					int j = k - p.Rays; float hdy = DirectedHeightsTiles[j / 3] * 16f, ox = (j % 3 - 1) * 24f;
					Vector2 tgt = new(goal.X + ox, goal.Y - hdy);
					dir = tgt - c0; float len = dir.Length();
					if (len < 32f || dir.Y > -0.15f * len) continue;
					dir /= len;
				}
				for (float t = 10f; t <= p.ReachPx; t += 6f)
				{
					Vector2 pt = c0 + dir * t;
					// the hook AI latches onto a tile its 10 px box overlaps (the box is centre -5 .. +5, a tile is 16 wide), so a tile within 5 px of the point sticks it
					int bx0 = (int)MathF.Floor((pt.X - 5f) / 16f), bx1 = (int)MathF.Floor((pt.X + 5f) / 16f), by0 = (int)MathF.Floor((pt.Y - 5f) / 16f), by1 = (int)MathF.Floor((pt.Y + 5f) / 16f);
					int tx = 0, ty = 0; float bd = float.MaxValue; bool hit = false;
					for (int x = bx0; x <= bx1; x++) for (int y = by0; y <= by1; y++)
					{
						if (!w.Latchable(x, y)) continue;
						float d2 = Vector2.DistanceSquared(pt, new Vector2(x * 16f + 8f, y * 16f + 8f));
						if (d2 < bd) { bd = d2; tx = x; ty = y; hit = true; }
					}
					if (!hit) continue;
					long key = ((long)tx << 32) ^ (uint)ty;
					if (seen.Add(key))
					{
						Vector2 a = new(tx * 16f + 8f, ty * 16f + 8f);
						if (Vector2.Distance(a, c0) <= p.ReachPx + 8f) found.Add((a, pt, Heuristic(a, goal, c0, kind, p.Climb)));
					}
					break; // the first latchable tile on the ray is where the hook sticks
				}
			}
			if (p.Climb) DirectedAnchors(w, c0, p, goal, kind, found, seen);
			if (p.Climb && kind == HookGoalKind.Altitude && Math.Abs(goal.X - c0.X) > 64f) { float sg = Math.Sign(goal.X - c0.X); found.RemoveAll(f => (f.a.X - c0.X) * sg < -12f); }   // a climb toward a goal across the way never casts at the wall behind it
			found.Sort((x, y) => x.h.CompareTo(y.h));
			var res = new List<(Vector2 a, Vector2 aim)>(Math.Min(found.Count, p.MaxAnchors));
			for (int i = 0; i < found.Count && res.Count < p.MaxAnchors; i++) res.Add((found[i].a, found[i].aim));
			return res;
		}

		/// <summary>The first latchable tile along a ray from <paramref name="c0"/> (the hook flies the straight line and sticks where its 10 px box first overlaps a tile). False when nothing is hit within the reach.</summary>
		private static bool FirstLatch(IHookWorld w, Vector2 c0, Vector2 dir, float reach, out int tx, out int ty, out Vector2 pt)
		{
			tx = ty = 0; pt = c0;
			for (float t = 10f; t <= reach; t += 6f)
			{
				pt = c0 + dir * t;
				int bx0 = (int)MathF.Floor((pt.X - 5f) / 16f), bx1 = (int)MathF.Floor((pt.X + 5f) / 16f), by0 = (int)MathF.Floor((pt.Y - 5f) / 16f), by1 = (int)MathF.Floor((pt.Y + 5f) / 16f);
				float bd = float.MaxValue; bool hit = false;
				for (int x = bx0; x <= bx1; x++) for (int y = by0; y <= by1; y++)
				{
					if (!w.Latchable(x, y)) continue;
					float d2 = Vector2.DistanceSquared(pt, new Vector2(x * 16f + 8f, y * 16f + 8f));
					if (d2 < bd) { bd = d2; tx = x; ty = y; hit = true; }
				}
				if (hit) return true;
			}
			return false;
		}

		/// <summary>Max rays aimed at exposed tiles per plan (a wall face in reach has a few dozen).</summary>
		private const int DirectedMax = 48;
		/// <summary>
		/// Rays aimed at the exposed latchable tiles around the body that lie nearest the goal (by the plan's own heuristic): what a player does, aiming the cursor at the lip or the next ledge.
		/// A coarse fan misses a tile 5 degrees off its rays, and against a wall the body hangs so close to the face that the fan's steepest rays latch only a couple of tiles up.
		/// </summary>
		private static void DirectedAnchors(IHookWorld w, Vector2 c0, in HookParams p, Vector2 goal, HookGoalKind kind, List<(Vector2 a, Vector2 aim, float h)> found, HashSet<long> seen)
		{
			int R = (int)(p.ReachPx / 16f) + 1, cx = (int)MathF.Floor(c0.X / 16f), cy = (int)MathF.Floor(c0.Y / 16f);
			var cand = new List<(float h, int x, int y)>(128);
			float r2 = (p.ReachPx + 8f) * (p.ReachPx + 8f);
			for (int tx = cx - R; tx <= cx + R; tx++) for (int ty = cy - R; ty <= cy + R; ty++)
			{
				if (!w.Latchable(tx, ty)) continue;
				Vector2 a = new(tx * 16f + 8f, ty * 16f + 8f);
				if (Vector2.DistanceSquared(a, c0) > r2) continue;
				if (w.Latchable(tx - 1, ty) && w.Latchable(tx + 1, ty) && w.Latchable(tx, ty - 1) && w.Latchable(tx, ty + 1)) continue;   // buried
				cand.Add((Heuristic(a, goal, c0, kind, p.Climb), tx, ty));
			}
			cand.Sort((x, y) => x.h.CompareTo(y.h));
			for (int i = 0; i < cand.Count && i < DirectedMax; i++)
			{
				// aim at the exposed side of the tile that faces the body (the corner of a lip is reached from below and beside)
				Vector2 tgt = new(cand[i].x * 16f + 8f, cand[i].y * 16f + 8f);
				Vector2 dir = tgt - c0; float len = dir.Length(); if (len < 24f) continue;
				dir /= len;
				if (!FirstLatch(w, c0, dir, p.ReachPx, out int hx, out int hy, out Vector2 pt)) continue;
				long key = ((long)hx << 32) ^ (uint)hy;
				if (!seen.Add(key)) continue;
				Vector2 an = new(hx * 16f + 8f, hy * 16f + 8f);
				if (Vector2.Distance(an, c0) <= p.ReachPx + 8f) found.Add((an, pt, Heuristic(an, goal, c0, kind, p.Climb)));
			}
		}

		/// <summary>
		/// One link of a climb: the cast that carries the body nearest the goal while it hangs on the anchor (no landing needed: a body halfway up a 100-tile face has no safe drop and does not need one).
		/// Null when no anchor in reach gets the body at least <paramref name="minGainPx"/> closer.
		/// </summary>
		public static HookPlan PlanClimb(IHookWorld w, Vector2 bottom, HookParams p, Vector2 goalBottom, float goalDistNow, float minGainPx)
		{
			if (p.ReachPx < 64f || p.PullPx <= 0f || p.Kind == HookKind.StandOff || p.Kind == HookKind.Spring) return null;
			p.Climb = true;
			DiagLog.Clear(); LastSteps = 0; DAnchors = DSims = DReleases = DNoLand = DLava = DDeep = DUnstand = DReelShort = 0;
			Vector2 c0 = bottom - new Vector2(0f, p.BodyH * 0.5f);
			var anchors = FanAnchors(w, c0, p, goalBottom, HookGoalKind.Altitude);
			DAnchors = anchors.Count;
			HookPlan best = null;
			for (int i = 0; i < anchors.Count; i++)
			{
				var one = new List<(Vector2, Vector2)>(1) { anchors[i] };
				var cand = Simulate(w, bottom, p, one, goalBottom, HookGoalKind.Altitude, goalDistNow);
				if (cand != null && (best == null || cand.Score < best.Score)) best = cand;
			}
			return best != null && best.Gain >= minGainPx ? best : null;
		}

		/// <summary>How far a climber at <paramref name="p"/> is from the goal: the height still to make up (or overshoot) plus a share of the way across, capped. A goal far down the road must not make a climb that gains height look like no progress.</summary>
		public static float ClimbDist(Vector2 p, Vector2 goal) => (p.Y >= goal.Y ? p.Y - goal.Y : 0.25f * (goal.Y - p.Y)) + 0.3f * Math.Min(Math.Abs(p.X - goal.X), 640f);

		private static float Heuristic(Vector2 a, Vector2 goal, Vector2 c0, HookGoalKind kind, bool climb = false)
		{
			if (kind == HookGoalKind.Altitude) return climb ? ClimbDist(a, goal) : Vector2.Distance(a, goal);
			// An anchor that carries the body to the goal hangs over it: near the goal in x, and no lower than just above the goal's level (a ledge face or a wall below the goal only drags the body along it).
			float above = goal.Y - 40f - a.Y;                    // > 0: the anchor is higher than the goal's standing height
			return MathF.Abs(a.X - goal.X) + (above < 0f ? -above : above * 0.1f);
		}

		// ---------------------------------------------------------------------------------------------
		// body physics
		// ---------------------------------------------------------------------------------------------
		private static bool Blocked(IHookWorld w, float x, float y, float bw, float bh)
		{
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f), y0 = (int)MathF.Floor(y / 16f), y1 = (int)MathF.Floor((y + bh - 0.01f) / 16f);
			for (int tx = x0; tx <= x1; tx++) for (int ty = y0; ty <= y1; ty++) if (w.Solid(tx, ty)) return true;
			return false;
		}

		private static bool TouchesLava(IHookWorld w, float x, float y, float bw, float bh)
		{
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f), y0 = (int)MathF.Floor(y / 16f), y1 = (int)MathF.Floor((y + bh - 0.01f) / 16f);
			for (int tx = x0; tx <= x1; tx++) for (int ty = y0; ty <= y1; ty++) if (w.Lava(tx, ty)) return true;
			return false;
		}

		private static bool BlockedBySlope(IHookWorld w, float x, float y, float bw, float bh)
		{
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f), y0 = (int)MathF.Floor(y / 16f), y1 = (int)MathF.Floor((y + bh - 0.01f) / 16f);
			for (int tx = x0; tx <= x1; tx++) for (int ty = y0; ty <= y1; ty++) if (w.Solid(tx, ty) && w.Sloped(tx, ty)) return true;
			return false;
		}

		/// <summary>Moves the body (top-left x, y) by its velocity with tile collision; a blocked axis loses its speed. Returns true when it came to rest on something below it.
		/// With <paramref name="slopeSticks"/> a body pulled into a sloped tile stops dead instead of sliding along it (a hook pulls straight; the body does not glide up a slope).</summary>
		private static bool Move(IHookWorld w, ref float x, ref float y, ref float vx, ref float vy, float bw, float bh, bool slopeSticks = false)
		{
			LastSteps++;
			int nx = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(vx) / 4f));
			float sx = vx / nx;
			for (int i = 0; i < nx; i++)
			{
				if (Blocked(w, x + sx, y, bw, bh))
				{
					if (slopeSticks && BlockedBySlope(w, x + sx, y, bw, bh)) { vx = 0f; vy = 0f; return false; }
					if (!Blocked(w, x + sx, y - 16f, bw, bh) && !Blocked(w, x, y - 16f, bw, bh) && vy >= 0f && Blocked(w, x, y + 1f, bw, bh)) { y -= 16f; x += sx; continue; } // a one-tile step up, as Collision.StepUp does for a walker
					float cs = MathF.Sign(sx); while (!Blocked(w, x + cs, y, bw, bh)) x += cs; // creep up to the wall
					vx = 0f; break;
				}
				x += sx;
			}
			bool landed = false;
			int ny = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(vy) / 4f));
			float sy = vy / ny;
			for (int i = 0; i < ny; i++)
			{
				if (Blocked(w, x, y + sy, bw, bh))
				{
					if (slopeSticks && sy < 0f && BlockedBySlope(w, x, y + sy, bw, bh)) { vx = 0f; vy = 0f; return false; }
					float cs = MathF.Sign(sy); while (!Blocked(w, x, y + cs, bw, bh)) y += cs; // creep up to the surface
					if (vy > 0f) landed = true; vy = 0f; break;
				}
				if (sy > 0f)
				{
					int rowOld = (int)MathF.Floor((y + bh) / 16f), rowNew = (int)MathF.Floor((y + sy + bh) / 16f);
					if (rowNew > rowOld && OnPlatformRow(w, x, bw, rowNew)) { y = rowNew * 16f - bh; vy = 0f; landed = true; break; }
				}
				y += sy;
			}
			return landed;
		}

		private static bool OnPlatformRow(IHookWorld w, float x, float bw, int row)
		{
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f);
			for (int tx = x0; tx <= x1; tx++) if (w.Platform(tx, row)) return true;
			return false;
		}

		/// <summary>Standing: floor under the centre column of the feet (the body comes to rest up to a pixel above it), nothing hot under or in the body.</summary>
		private static bool StandableAt(IHookWorld w, float x, float y, float bw, float bh)
		{
			int row = (int)MathF.Floor((y + bh + 2f) / 16f);
			int cx = (int)MathF.Floor((x + bw * 0.5f) / 16f);
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f);
			for (int tx = x0; tx <= x1; tx++) if (w.Lava(tx, row)) return false;
			return (w.Solid(cx, row) || w.Platform(cx, row)) && !TouchesLava(w, x, y, bw, bh);
		}

		// ---------------------------------------------------------------------------------------------
		// simulation of one anchor set
		// ---------------------------------------------------------------------------------------------
		private struct ReelState { public float X, Y, Vx, Vy, Dist, DesVx, DesVy; public int Slope; }
		/// <summary>Score (px) charged per reel tick spent dragging along a slope; also the harness switch (0 = the planner is blind to slopes).</summary>
		public static float SlopeDragCost = 14f;
		private static bool TouchesSlope(IHookWorld w, float x, float y, float bw, float bh)
		{
			int x0 = (int)MathF.Floor((x - 2f) / 16f), x1 = (int)MathF.Floor((x + bw + 2f) / 16f), y0 = (int)MathF.Floor((y - 2f) / 16f), y1 = (int)MathF.Floor((y + bh + 2f) / 16f);
			for (int tx = x0; tx <= x1; tx++) for (int ty = y0; ty <= y1; ty++) if (w.Sloped(tx, ty) && w.Solid(tx, ty)) return true;
			return false;
		}

		/// <summary>Re-plans from the anchors a cast REALLY latched onto (they can differ from the planned tiles when a ray grazes an edge): a single simulation. Null when those anchors cannot do the job.</summary>
		public static HookPlan Replan(IHookWorld w, Vector2 bottom, in HookParams p, List<Vector2> actualAnchors, Vector2 goalBottom, HookGoalKind kind, float goalDistNow)
		{
			var list = new List<(Vector2, Vector2)>(actualAnchors.Count);
			foreach (var a in actualAnchors) list.Add((a, a));
			return Simulate(w, bottom, p, list, goalBottom, kind, goalDistNow);
		}

		private static HookPlan Simulate(IHookWorld w, Vector2 bottom, in HookParams p, List<(Vector2 a, Vector2 aim)> cands, Vector2 goalBottom, HookGoalKind kind, float goalDistNow, bool scanAll = false)
		{
			var anchors = new List<Vector2>(cands.Count); foreach (var c in cands) anchors.Add(c.a);
			DSims++;
			float bw = p.BodyW, bh = p.BodyH;
			Vector2 centroid = Vector2.Zero; for (int i = 0; i < anchors.Count; i++) centroid += anchors[i]; centroid /= anchors.Count;
			float x = bottom.X - bw / 2f, y = bottom.Y - bh;
			var states = new List<ReelState>(64); List<Vector2> reelPath = Trace ? new List<Vector2>() : null;
			float lastDist = float.MaxValue; int stall = 0, slopeAcc = 0;
			for (int t = 0; t < LevyConstants.Hooks.PlanReelSimTicks; t++)
			{
				Vector2 c = new(x + bw / 2f, y + bh / 2f);
				Vector2 d = centroid - c; float len = d.Length();
				float desVx, desVy;
				if (p.Kind == HookKind.Steered) { Vector2 u = len > 0.01f ? d / len : Vector2.Zero; desVx = u.X * p.PullPx * 0.92f; desVy = u.Y * p.PullPx * 0.92f; }
				else if (len > p.PullPx) { desVx = d.X * (p.PullPx / len); desVy = d.Y * (p.PullPx / len); }
				else { desVx = d.X; desVy = d.Y; }
				float vx = desVx, vy = desVy;
				if (TouchesLava(w, x, y, bw, bh)) return null;
				Move(w, ref x, ref y, ref vx, ref vy, bw, bh, SlopeDragCost > 0f);
				c = new Vector2(x + bw / 2f, y + bh / 2f);
				float dist = Vector2.Distance(centroid, c);
				reelPath?.Add(new Vector2(x + bw / 2f, y + bh));
				if (SlopeDragCost > 0f && TouchesSlope(w, x, y, bw, bh)) slopeAcc++;
				states.Add(new ReelState { X = x, Y = y, Vx = vx, Vy = vy, Dist = dist, DesVx = desVx, DesVy = desVy, Slope = slopeAcc });
				if (dist > lastDist - LevyConstants.Hooks.ReelProgressPx) { if (++stall >= 6) break; } else stall = 0;
				lastDist = Math.Min(lastDist, dist);
				if (dist < 6f) break;
			}
			if (states.Count < 3) { DReelShort++; return null; }
			// the realised velocities carry the body after a release; use a velocity that kept the body's own motion (post-collision) and the desired one for the kick test
			HookPlan best = null;
			int last = states.Count - 1;
			var tried = new HashSet<int>();
			int nCand = scanAll ? last : p.ReleaseCandidates;
			for (int k = 0; k < nCand; k++)
			{
				int idx = scanAll ? k + 1 : Math.Clamp((int)MathF.Round(last * (0.15f + 0.85f * k / Math.Max(1, p.ReleaseCandidates - 1))), 1, last);
				if (!tried.Add(idx)) continue;
				DReleases++;
				ReelState s = states[idx];
				var plan = kind == HookGoalKind.Altitude ? null : Ballistic(w, p, s, idx, goalBottom, goalDistNow, bottom, anchors, reelPath, cands);
				if (plan != null && (best == null || plan.Score < best.Score)) best = plan;
			}
			if (best != null && kind != HookGoalKind.Altitude)
			{
				int b0 = best.ReleaseTick;
				for (int d = -2; d <= 2; d++)
				{
					int idx = b0 + d; if (d == 0 || idx < 1 || idx > last || !tried.Add(idx)) continue;
					DReleases++;
					var plan = Ballistic(w, p, states[idx], idx, goalBottom, goalDistNow, bottom, anchors, reelPath, cands);
					if (plan != null && plan.Score < best.Score) best = plan;
				}
			}
			if (kind == HookGoalKind.Altitude)
			{
				ReelState e = states[last];
				best = HangPlan(w, p, e, last, goalBottom, goalDistNow, bottom, anchors, cands);
			}
			return best;
		}

		private static HookPlan Ballistic(IHookWorld w, in HookParams p, ReelState s, int idx, Vector2 goalBottom, float goalDistNow, Vector2 start, List<Vector2> anchors, List<Vector2> reelPath, List<(Vector2 a, Vector2 aim)> cands)
		{
			float bw = p.BodyW, bh = p.BodyH;
			float x = s.X, y = s.Y, vx = s.DesVx, vy = s.DesVy; // the release hands the body the reel's full velocity (Player.GrappleMovement sets it before it lets go)
			if (MathF.Abs(s.DesVy) < 1e-4f || (p.Climb && s.Dist < 24f)) vy = Math.Min(vy, -(p.Climb && p.KickSpeed > 0f ? p.KickSpeed : p.JumpSpeed)); // vanilla: a level reel gives the jump kick on release; a climber who has arrived hangs still and jumps off the hook
			Vector2 relPos = new(x + bw / 2f, y + bh);
			bool landed = false; int ticks = 0;
			float startRow = start.Y; List<Vector2> fl = reelPath != null ? new List<Vector2>(reelPath.GetRange(0, Math.Min(idx + 1, reelPath.Count))) : null;
			for (int t = 0; t < LevyConstants.Hooks.PlanBallisticSimTicks; t++)
			{
				ticks = t + 1;
				vy = Math.Min(LevyConstants.Hooks.TerminalVelocityPx, vy + p.Gravity);
				float steerDx = goalBottom.X - (x + bw / 2f);
				if (p.AirSteer && MathF.Abs(steerDx) > 20f && (y + bh) - goalBottom.Y < 12f * 16f) vx = MathF.Sign(steerDx) * MathF.Min(3.4f, MathF.Max(1.5f, MathF.Abs(steerDx) / 8f));   // same rule as SoldierNPC.TickBallistic
				else if (MathF.Abs(vx) > LevyConstants.Hooks.AirDragPx) vx -= MathF.Sign(vx) * LevyConstants.Hooks.AirDragPx; else vx = 0f;
				bool rest = Move(w, ref x, ref y, ref vx, ref vy, bw, bh);
				fl?.Add(new Vector2(x + bw / 2f, y + bh));
				if (TouchesLava(w, x, y, bw, bh)) { DLava++; if (Trace) DiagLog.Add($"a={anchors[0].X / 16f:0.0},{anchors[0].Y / 16f:0.0} rel={idx} v={s.DesVx:0.0},{s.DesVy:0.0} lava"); return null; }
				if (y + bh > startRow + LevyConstants.Hooks.MaxDropBelowStartTiles * 16f && y + bh > goalBottom.Y + 48f) { DDeep++; if (Trace) DiagLog.Add($"a={anchors[0].X / 16f:0.0},{anchors[0].Y / 16f:0.0} rel={idx} relpos={relPos.X / 16f:0.0},{relPos.Y / 16f:0.0} v={s.DesVx:0.0},{s.DesVy:0.0} deep at x={x / 16f:0.0} t={t}"); return null; } // a deep drop
				if (rest) { landed = true; break; }
			}
			if (!landed) { DNoLand++; return null; }
			if (!StandableAt(w, x, y, bw, bh)) { DUnstand++; if (Trace) DiagLog.Add($"a={anchors[0].X / 16f:0.0},{anchors[0].Y / 16f:0.0} rel={idx} unstandable at {x / 16f:0.0},{(y + bh) / 16f:0.0}"); return null; }
			if (Trace) DiagLog.Add($"a={anchors[0].X / 16f:0.0},{anchors[0].Y / 16f:0.0} rel={idx} relpos={relPos.X / 16f:0.0},{relPos.Y / 16f:0.0} v={s.DesVx:0.0},{s.DesVy:0.0} lands {x / 16f + bw / 32f:0.0},{(y + bh) / 16f:0.0} ticks={ticks}");
			Vector2 land = new(x + bw / 2f, y + bh);
			float gd = Vector2.Distance(land, goalBottom) + Math.Abs(land.Y - goalBottom.Y) * 0.5f;
			var plan = new HookPlan { Kind = HookGoalKind.Ground, ReleaseTick = idx, ReleaseDist = Math.Max(0f, s.Dist), ReleasePos = relPos, Landing = land, Landed = true, GoalDist = gd, Gain = goalDistNow - gd, ReelTicks = idx, FlightTicks = idx + ticks };
			plan.Anchors.AddRange(anchors); plan.Path = fl; foreach (var c in cands) plan.Aims.Add(c.aim);
			plan.SlopeTicks = s.Slope; plan.Score = gd + 0.35f * (idx + ticks) + SlopeDragCost * s.Slope;
			return plan;
		}

		private static HookPlan HangPlan(IHookWorld w, in HookParams p, ReelState e, int idx, Vector2 goalBottom, float goalDistNow, Vector2 start, List<Vector2> anchors, List<(Vector2 a, Vector2 aim)> cands)
		{
			float bw = p.BodyW, bh = p.BodyH;
			Vector2 hangBottom = new(e.X + bw / 2f, e.Y + bh);
			// a safe drop from the hang point: straight down to standable ground, no lava, not a deep fall
			float x = e.X, y = e.Y, vx = 0f, vy = 0f; bool landed = false;
			for (int t = 0; t < LevyConstants.Hooks.PlanBallisticSimTicks; t++)
			{
				vy = Math.Min(LevyConstants.Hooks.TerminalVelocityPx, vy + p.Gravity);
				if (Move(w, ref x, ref y, ref vx, ref vy, bw, bh)) { landed = true; break; }
				if (TouchesLava(w, x, y, bw, bh)) return null;
				if (p.Climb) break;   // a climb link hangs where it arrives: no drop is simulated
			}
			if (!p.Climb && (!landed || !StandableAt(w, x, y, bw, bh))) return null;
			Vector2 land = new(x + bw / 2f, y + bh);
			float gd = p.Climb ? ClimbDist(hangBottom, goalBottom) : Vector2.Distance(hangBottom, goalBottom);
			var plan = new HookPlan { Kind = HookGoalKind.Altitude, ReleaseTick = idx, ReleaseDist = Math.Max(0f, e.Dist), ReleasePos = hangBottom, Landing = land, Landed = true, Hang = true, GoalDist = gd, Gain = goalDistNow - gd, ReelTicks = idx, FlightTicks = idx };
			plan.Anchors.AddRange(anchors); foreach (var c in cands) plan.Aims.Add(c.aim);
			plan.SlopeTicks = e.Slope; plan.Score = gd + 0.2f * idx + SlopeDragCost * e.Slope;
			return plan;
		}
	}
}
