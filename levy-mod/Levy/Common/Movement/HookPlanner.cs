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
		public List<Vector2> Path;
		public int ReelTicks, FlightTicks;
		public HookGoalKind Kind;
		public bool Beats(float arrivePx, float minGainPx) => (Landed || Hang) && (GoalDist <= arrivePx || Gain >= minGainPx);
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
		/// <summary>Harness: keep the bottom-centre path (reel, then flight) on each plan.</summary>
		public static bool Trace;
		/// <summary>Why candidates were dropped in the last plan (harness diagnostics): anchors, reel sims, releases tried, landings missed, lava, deep drops, unstandable.</summary>
		public static int DAnchors, DSims, DReleases, DNoLand, DLava, DDeep, DUnstand, DReelShort;
		public static string Describe() => $"anchors={DAnchors} sims={DSims} rel={DReleases} noLand={DNoLand} lava={DLava} deep={DDeep} unstand={DUnstand} reelShort={DReelShort}";

		public static HookPlan Plan(IHookWorld w, Vector2 bottom, in HookParams p, Vector2 goalBottom, HookGoalKind kind, float goalDistNow)
		{
			if (p.ReachPx < 64f || p.PullPx <= 0f || p.Kind == HookKind.StandOff || p.Kind == HookKind.Spring) return null;
			LastSteps = 0; DAnchors = DSims = DReleases = DNoLand = DLava = DDeep = DUnstand = DReelShort = 0;
			Vector2 c0 = bottom - new Vector2(0f, p.BodyH * 0.5f);
			var anchors = FanAnchors(w, c0, p, goalBottom, kind);
			DAnchors = anchors.Count;
			if (anchors.Count == 0) return null;
			HookPlan best = null;
			// singles
			for (int i = 0; i < anchors.Count; i++)
			{
				var one = new List<(Vector2, Vector2)>(1) { anchors[i] };
				var cand = Simulate(w, bottom, p, one, goalBottom, kind, goalDistNow);
				if (cand != null && (best == null || cand.Score < best.Score)) best = cand;
			}
			// pairs for a multi-hook item: the two nearest-to-best singles around the winner
			if (p.MaxHooks >= 2 && p.Count >= 2 && best != null)
			{
				var chosen = (best.Anchors[0], best.Aims[0]);
				for (int i = 0; i < anchors.Count; i++)
				{
					if ((anchors[i].a - chosen.Item1).LengthSquared() < 48f * 48f) continue;       // a second anchor at least three tiles off
					if ((anchors[i].a - chosen.Item1).LengthSquared() > 224f * 224f) continue;     // and not so far the pair pulls somewhere new
					var two = new List<(Vector2, Vector2)>(2) { chosen, anchors[i] };
					var cand = Simulate(w, bottom, p, two, goalBottom, kind, goalDistNow);
					if (cand != null && cand.Score < best.Score - 4f) best = cand;           // a pair has to be clearly better to be worth the second cast
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
			for (int k = 0; k < p.Rays; k++)
			{
				float th = -MathHelper.Pi + (k + 0.5f) * MathHelper.Pi / p.Rays; // -176 .. -4 degrees: left horizontal, over the top, right horizontal
				Vector2 dir = new(MathF.Cos(th), MathF.Sin(th));
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
						if (Vector2.Distance(a, c0) <= p.ReachPx + 8f) found.Add((a, pt, Heuristic(a, goal, c0, kind)));
					}
					break; // the first latchable tile on the ray is where the hook sticks
				}
			}
			found.Sort((x, y) => x.h.CompareTo(y.h));
			var res = new List<(Vector2 a, Vector2 aim)>(Math.Min(found.Count, p.MaxAnchors));
			for (int i = 0; i < found.Count && res.Count < p.MaxAnchors; i++) res.Add((found[i].a, found[i].aim));
			return res;
		}

		private static float Heuristic(Vector2 a, Vector2 goal, Vector2 c0, HookGoalKind kind)
		{
			// near the goal first; for a ground goal an anchor that leads the body across (toward the goal in x) and up beats one behind
			float dg = Vector2.Distance(a, goal);
			return dg + (kind == HookGoalKind.Ground ? Math.Abs(a.X - goal.X) * 0.25f : 0f);
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

		/// <summary>Moves the body (top-left x, y) by its velocity with tile collision; a blocked axis loses its speed. Returns true when it came to rest on something below it.</summary>
		private static bool Move(IHookWorld w, ref float x, ref float y, ref float vx, ref float vy, float bw, float bh)
		{
			LastSteps++;
			int nx = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(vx) / 4f));
			float sx = vx / nx;
			for (int i = 0; i < nx; i++)
			{
				if (Blocked(w, x + sx, y, bw, bh))
				{
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

		private static bool StandableAt(IHookWorld w, float x, float y, float bw, float bh)
		{
			int row = (int)MathF.Floor((y + bh + 0.5f) / 16f);
			int x0 = (int)MathF.Floor(x / 16f), x1 = (int)MathF.Floor((x + bw - 0.01f) / 16f);
			bool floor = false;
			for (int tx = x0; tx <= x1; tx++)
			{
				if (w.Lava(tx, row)) return false;
				if (w.Solid(tx, row) || w.Platform(tx, row)) floor = true;
			}
			return floor && !TouchesLava(w, x, y, bw, bh);
		}

		// ---------------------------------------------------------------------------------------------
		// simulation of one anchor set
		// ---------------------------------------------------------------------------------------------
		private struct ReelState { public float X, Y, Vx, Vy, Dist, DesVx, DesVy; }

		/// <summary>Re-plans from the anchors a cast REALLY latched onto (they can differ from the planned tiles when a ray grazes an edge): a single simulation. Null when those anchors cannot do the job.</summary>
		public static HookPlan Replan(IHookWorld w, Vector2 bottom, in HookParams p, List<Vector2> actualAnchors, Vector2 goalBottom, HookGoalKind kind, float goalDistNow)
		{
			var list = new List<(Vector2, Vector2)>(actualAnchors.Count);
			foreach (var a in actualAnchors) list.Add((a, a));
			return Simulate(w, bottom, p, list, goalBottom, kind, goalDistNow);
		}

		private static HookPlan Simulate(IHookWorld w, Vector2 bottom, in HookParams p, List<(Vector2 a, Vector2 aim)> cands, Vector2 goalBottom, HookGoalKind kind, float goalDistNow)
		{
			var anchors = new List<Vector2>(cands.Count); foreach (var c in cands) anchors.Add(c.a);
			DSims++;
			float bw = p.BodyW, bh = p.BodyH;
			Vector2 centroid = Vector2.Zero; for (int i = 0; i < anchors.Count; i++) centroid += anchors[i]; centroid /= anchors.Count;
			float x = bottom.X - bw / 2f, y = bottom.Y - bh;
			var states = new List<ReelState>(64); List<Vector2> reelPath = Trace ? new List<Vector2>() : null;
			float lastDist = float.MaxValue; int stall = 0;
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
				Move(w, ref x, ref y, ref vx, ref vy, bw, bh);
				c = new Vector2(x + bw / 2f, y + bh / 2f);
				float dist = Vector2.Distance(centroid, c);
				reelPath?.Add(new Vector2(x + bw / 2f, y + bh));
				states.Add(new ReelState { X = x, Y = y, Vx = vx, Vy = vy, Dist = dist, DesVx = desVx, DesVy = desVy });
				if (dist > lastDist - LevyConstants.Hooks.ReelProgressPx) { if (++stall >= 6) break; } else stall = 0;
				lastDist = Math.Min(lastDist, dist);
				if (dist < 6f) break;
			}
			if (states.Count < 3) { DReelShort++; return null; }
			// the realised velocities carry the body after a release; use a velocity that kept the body's own motion (post-collision) and the desired one for the kick test
			HookPlan best = null;
			int last = states.Count - 1;
			var tried = new HashSet<int>();
			for (int k = 0; k < p.ReleaseCandidates; k++)
			{
				int idx = Math.Clamp((int)MathF.Round(last * (0.15f + 0.85f * k / Math.Max(1, p.ReleaseCandidates - 1))), 1, last);
				if (!tried.Add(idx)) continue;
				DReleases++;
				ReelState s = states[idx];
				var plan = kind == HookGoalKind.Altitude ? null : Ballistic(w, p, s, idx, goalBottom, goalDistNow, bottom, anchors, reelPath, cands);
				if (plan != null && (best == null || plan.Score < best.Score)) best = plan;
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
			if (MathF.Abs(s.DesVy) < 1e-4f) vy = -p.JumpSpeed; // vanilla: a level reel gives the jump kick on release
			Vector2 relPos = new(x + bw / 2f, y + bh);
			bool landed = false; int ticks = 0;
			float startRow = start.Y; List<Vector2> fl = reelPath != null ? new List<Vector2>(reelPath.GetRange(0, Math.Min(idx + 1, reelPath.Count))) : null;
			for (int t = 0; t < LevyConstants.Hooks.PlanBallisticSimTicks; t++)
			{
				ticks = t + 1;
				vy = Math.Min(LevyConstants.Hooks.TerminalVelocityPx, vy + p.Gravity);
				if (MathF.Abs(vx) > LevyConstants.Hooks.AirDragPx) vx -= MathF.Sign(vx) * LevyConstants.Hooks.AirDragPx; else vx = 0f;
				bool rest = Move(w, ref x, ref y, ref vx, ref vy, bw, bh);
				fl?.Add(new Vector2(x + bw / 2f, y + bh));
				if (TouchesLava(w, x, y, bw, bh)) { DLava++; return null; }
				if (y + bh > startRow + LevyConstants.Hooks.MaxDropBelowStartTiles * 16f && y + bh > goalBottom.Y + 48f) { DDeep++; return null; } // a deep drop
				if (rest) { landed = true; break; }
			}
			if (!landed) { DNoLand++; return null; }
			if (!StandableAt(w, x, y, bw, bh)) { DUnstand++; return null; }
			Vector2 land = new(x + bw / 2f, y + bh);
			float gd = Vector2.Distance(land, goalBottom) + Math.Abs(land.Y - goalBottom.Y) * 0.5f;
			var plan = new HookPlan { Kind = HookGoalKind.Ground, ReleaseTick = idx, ReleaseDist = Math.Max(0f, s.Dist), ReleasePos = relPos, Landing = land, Landed = true, GoalDist = gd, Gain = goalDistNow - gd, ReelTicks = idx, FlightTicks = idx + ticks };
			plan.Anchors.AddRange(anchors); plan.Path = fl; foreach (var c in cands) plan.Aims.Add(c.aim);
			plan.Score = gd + 0.35f * (idx + ticks);
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
			}
			if (!landed || !StandableAt(w, x, y, bw, bh)) return null;
			Vector2 land = new(x + bw / 2f, y + bh);
			float gd = Vector2.Distance(hangBottom, goalBottom);
			var plan = new HookPlan { Kind = HookGoalKind.Altitude, ReleaseTick = idx, ReleaseDist = Math.Max(0f, e.Dist), ReleasePos = hangBottom, Landing = land, Landed = true, Hang = true, GoalDist = gd, Gain = goalDistNow - gd, ReelTicks = idx, FlightTicks = idx };
			plan.Anchors.AddRange(anchors); foreach (var c in cands) plan.Aims.Add(c.aim);
			plan.Score = gd + 0.2f * idx;
			return plan;
		}
	}
}
