using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>
	/// The rules of the off-screen catch-up hop of a map-point order (SoldierNPC.CatchUp.cs): who can see a point, which waypoint of a route to hop to, and which point on the way to a goal that a player can see.
	/// Pure functions where possible so the boot self-check can run them (OffscreenHopCheck).
	/// </summary>
	public static class OffscreenHop
	{
		/// <summary>True when the point lies inside the box (<see cref="LevyConstants.Movement.CatchUpBoxW"/> x <see cref="LevyConstants.Movement.CatchUpBoxH"/> px) around any of the viewers.</summary>
		public static bool SeenBy(Vector2 p, IReadOnlyList<Vector2> viewers)
		{
			for (int i = 0; i < viewers.Count; i++)
				if (Math.Abs(p.X - viewers[i].X) <= CatchUpBoxW * 0.5f && Math.Abs(p.Y - viewers[i].Y) <= CatchUpBoxH * 0.5f) return true;
			return false;
		}

		private static readonly List<Vector2> _viewers = new();
		/// <summary>Test seam for the travel lab: a headless server resets Player.active every tick for a slot nobody is connected on, so the lab lists its stand-in commander here.</summary>
		public static List<Vector2> LabViewers;

		/// <summary>True when any active player (every player in multiplayer, dead or alive: they watch the respawn screen) could see the point.</summary>
		public static bool Seen(Vector2 p)
		{
			_viewers.Clear();
			for (int i = 0; i < Main.maxPlayers; i++) { Player pl = Main.player[i]; if (pl != null && pl.active) _viewers.Add(pl.Center); }
			if (LabViewers != null) _viewers.AddRange(LabViewers);
			return SeenBy(p, _viewers);
		}

		/// <summary>The furthest waypoint index in [from, count) the test accepts (the test also records its landing spot), or -1.</summary>
		public static int PickWaypoint(int count, int from, Func<int, bool> accept)
		{
			for (int i = count - 1; i >= Math.Max(0, from); i--) if (accept(i)) return i;
			return -1;
		}

		/// <summary>
		/// The point of the straight way from <paramref name="me"/> to <paramref name="goal"/> that is nearest the goal and accepted (the test says: standable and out of every player's sight), at least <paramref name="minTiles"/> from me.
		/// Steps of 4 tiles from the goal back toward me. False when there is none.
		/// </summary>
		public static bool PickOnTheWay(Vector2 me, Vector2 goal, float minTiles, Func<Vector2, bool> accept, out Vector2 at)
		{
			at = me;
			float len = Vector2.Distance(me, goal) / 16f;
			for (float d = 0f; d <= len - minTiles; d += 4f)
			{
				Vector2 q = Vector2.Lerp(goal, me, len <= 0.01f ? 0f : d / len);
				if (accept(q)) { at = q; return true; }
			}
			return false;
		}

		/// <summary>A small open-room test: walking steps (sideways, one up, any drop) from the spot reach at least 240 distinct cells or 40 columns, so the spot is not a sealed pocket.</summary>
		public static bool RoomIsOpen(Point feet)
		{
			var seen = new HashSet<long>(); var q = new Queue<Point>();
			q.Enqueue(feet); seen.Add(Key(feet));
			int minX = feet.X, maxX = feet.X;
			while (q.Count > 0 && seen.Count < 240)
			{
				Point c = q.Dequeue();
				for (int side = -1; side <= 1; side += 2)
				{
					int nx = c.X + side;
					for (int dy = -1; dy <= 24; dy++)
					{
						int ny = c.Y + dy;
						if (!TileNav.InBounds(nx, ny)) break;
						if (dy < 0) { if (TileNav.IsStandable(nx, ny)) Add(new Point(nx, ny)); continue; }
						if (TileNav.IsSolid(nx, ny) || TileNav.HasLava(nx, ny)) break;
						if (TileNav.IsStandable(nx, ny)) { Add(new Point(nx, ny)); break; }
					}
				}
			}
			return seen.Count >= 240 || maxX - minX >= 40;
			void Add(Point p) { if (seen.Add(Key(p))) { q.Enqueue(p); if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X; } }
			static long Key(Point p) => ((long)p.X << 20) | (uint)p.Y;
		}
	}
}
