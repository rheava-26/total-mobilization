using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Config;
using Levy.Common.Movement;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FREE REIN: where a free-roaming levy may go (the home leash) and what it does when nothing threatens (SoldierNPC.Leisure.cs).
	/// The base is the levy's barracks (else the nearest one, else the spot the commander gave the order from). The leash is a BOX around it: FreeReinRadius tiles each way along the ground,
	/// <see cref="LevyConstants.FreeReinUpTiles"/> rows of sky and <see cref="LevyConstants.FreeReinDownTiles"/> rows under the base floor, so a hole, a shaft or a cave under the base is outside it however near it lies.
	/// A wander spot is a standing tile inside the box that is dry, has little rock over it (not a cave) and is not a pit floor; the route to it may not dip into a hole, and a spot lower than the levy
	/// needs a way back. A levy that finds itself outside the box (knocked, carried, an old save) walks home before it does anything else.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Lab switch (LEVY_FREEREIN_LEGACY=1): the Free Rein of 2.0.55, with its 600-tile circle and no spot checks. Never set outside the lab.</summary>
		internal static bool FreeReinLegacy = Environment.GetEnvironmentVariable("LEVY_FREEREIN_LEGACY") == "1";

		/// <summary>Lab ablation (LEVY_FREEREIN_ABL, bit flags): 1 old nearest-standable spot pick, 2 no route check, 4 no walk-home, 8 the old 600-tile circle, 16 no vertical box. Never set outside the lab.</summary>
		internal static int FreeReinAblate = int.TryParse(Environment.GetEnvironmentVariable("LEVY_FREEREIN_ABL"), out int abl) ? abl : 0;
		internal static bool FrOn(int bit) => !FreeReinLegacy && (FreeReinAblate & bit) == 0;

		/// <summary>Tiles from home along the ground (the Free Rein range option).</summary>
		internal static int FreeReinRadius
		{
			get
			{
				if (FreeReinRadiusOverride > 0) return FreeReinRadiusOverride;
				try { int r = LevyConfig.Get.FreeReinRadius; return r > 0 ? r : LevyConstants.FreeReinRadiusDefault; } catch (Exception) { return LevyConstants.FreeReinRadiusDefault; }
			}
		}
		/// <summary>Self-check / harness override of the range (0 = the config).</summary>
		internal static int FreeReinRadiusOverride;
		private int FreeReinLeash => FreeReinLegacy || (FreeReinAblate & 8) != 0 ? LevyConstants.FreeReinLeashTiles : FreeReinRadius;

		private int _frAnchorOver, _frAnchorRow;
		private bool _frRecall; private int _frRecallAt; private Vector2 _frRecallGoal;
		internal static int FreeReinRecalls, FreeReinStranded;
		private readonly List<Vector2> _probePath2 = new();

		// ---------------- the base ----------------
		/// <summary>No barracks near: the commander's spot when he is close (the whole squad then shares one base), else where the levy stands.</summary>
		private Vector2 FreeReinCommanderSpot(Vector2 fallback)
		{
			Player cmd = Commander;
			if (cmd != null && !cmd.dead && LevyUtil.WithinTiles(cmd.Center, NPC.Center, FreeReinLeash * 2)) return cmd.Bottom + new Vector2(0f, -8f);
			return fallback;
		}

		private void FreeReinAnchorSet(Vector2 a)
		{
			Point t = TileNav.FeetTile(a);
			_frAnchorOver = Overburden(t.X, t.Y); _frAnchorRow = t.Y;
			_frRecall = false; _frRecallAt = 0;
		}

		// ---------------- the box ----------------
		/// <summary>Tiles by which a point lies outside the box (0 inside): sideways beyond the radius, below the base floor's reach, above the sky's.</summary>
		private float FreeReinBoxOutside(Vector2 p)
		{
			return BoxOutside(p, FreeReinBase(), FreeReinLeash, (FreeReinAblate & 16) == 0);
		}

		/// <summary>Pure rule of the leash: tiles by which <paramref name="p"/> lies outside the box around <paramref name="a"/> (0 inside).</summary>
		internal static float BoxOutside(Vector2 p, Vector2 a, int radiusTiles, bool vertical = true)
		{
			float dx = Math.Max(0f, Math.Abs(p.X - a.X) / 16f - radiusTiles);
			if (!vertical) return dx;
			float down = Math.Max(0f, (p.Y - a.Y) / 16f - LevyConstants.FreeReinDownTiles);
			float up = Math.Max(0f, (a.Y - p.Y) / 16f - LevyConstants.FreeReinUpTiles);
			return dx + down + up;
		}

		/// <summary>Inside the box, or no more than <paramref name="margin"/> tiles outside it (a target's reach).</summary>
		private bool FreeReinInBox(Vector2 p, float margin = 0f)
		{
			if (FreeReinLegacy) return LevyUtil.WithinTiles(FreeReinBase(), p, LevyConstants.FreeReinLeashTiles + margin);
			return FreeReinBoxOutside(p) <= margin;
		}

		private void FreeReinClampBox(ref Vector2 goal)
		{
			if (FreeReinLegacy) return;
			Vector2 a = FreeReinBase();
			float r = FreeReinLeash * 16f;
			goal.X = MathHelper.Clamp(goal.X, a.X - r, a.X + r);
			if ((FreeReinAblate & 16) == 0) goal.Y = MathHelper.Clamp(goal.Y, a.Y - LevyConstants.FreeReinUpTiles * 16f, a.Y + LevyConstants.FreeReinDownTiles * 16f);
		}

		// ---------------- the spots ----------------
		/// <summary>Solid tiles (not platforms) in the column above row y within the scan: a house roof is a few, a cave or a hill is a dozen or more.</summary>
		internal static int Overburden(int x, int y)
		{
			int n = 0;
			for (int r = 1; r <= LevyConstants.FreeReinOverburdenScanRows; r++)
			{
				int yy = y - r; if (yy < 5) break;
				Tile t = Main.tile[x, yy];
				if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) n++;
			}
			return n;
		}

		/// <summary>Pure rule of a wander spot's roof: a spot may have this much rock over it given the base's own.</summary>
		internal static bool OverburdenOk(int over, int anchorOver) => over <= anchorOver + LevyConstants.FreeReinOverburdenMax;

		/// <summary>The tile (feet row ty) is a place to wander to: inside the box and the world, dry, no lava near, not under much rock.</summary>
		private bool FreeReinSpotOk(int tx, int ty, bool underworldBase)
		{
			int m = LevyConstants.FreeReinWorldMarginTiles;
			if (tx < m || tx > Main.maxTilesX - m) return false;
			if (FreeReinBoxOutside(TileNav.BottomCentre(tx, ty)) > 0f) return false;
			if (!underworldBase && ty >= Main.UnderworldLayer - LevyConstants.FreeReinUnderworldMarginTiles) return false;
			if (TileNav.HasLiquid(tx, ty) || TileNav.HasLiquid(tx, ty - 1)) return false;
			for (int k = -2; k <= 2; k++) if (TileNav.HasLava(tx + k, ty) || TileNav.HasLava(tx + k, ty + 1)) return false;
			int over = Overburden(tx, ty);
			if (!OverburdenOk(over, _frAnchorOver)) return false;
			if (PitFloor(tx, ty, over, _frAnchorRow)) return false;
			return !NearDrop(tx, ty, LevyConstants.FreeReinRimTiles);
		}

		/// <summary>A hole, a chasm or a cliff edge within <paramref name="reach"/> columns of the spot: a deep drop (or no floor in reach) under the feet row beside it.</summary>
		internal static bool NearDrop(int tx, int ty, int reach)
		{
			for (int k = -reach; k <= reach; k++)
			{
				int d = TileNav.DropDepth(tx + k, ty);
				if (d < 0 || d >= LevyConstants.FreeReinHoleTiles) return true;
			}
			return false;
		}

		/// <summary>
		/// A goal across a hole is pulled back to this side: scanning from the levy toward the goal at the levy's own feet row, the first column with no floor in reach (a hole or a chasm) stops the goal four tiles short.
		/// A fight across a gap is shot from the rim, not walked or flown into. Free Rein only.
		/// </summary>
		private void FreeReinStopAtGap(ref Vector2 goal)
		{
			if (!FrOn(64)) return;
			Point me = TileNav.FeetTile(NPC.Bottom);
			int gx = (int)(goal.X / 16f), dir = Math.Sign(gx - me.X);
			if (dir == 0) return;
			int span = Math.Min(Math.Abs(gx - me.X), 90);
			for (int k = 1; k <= span; k++)
			{
				int x = me.X + dir * k;
				if (!TileNav.InBounds(x, me.Y)) return;
				int d = TileNav.DropDepth(x, me.Y);
				if (d < 0 || d >= LevyConstants.FreeReinGapTiles)
				{
					int stop = Math.Max(0, k - 1 - LevyConstants.FreeReinRimTiles - 1);
					int sx = me.X + dir * stop;
					goal = new Vector2(sx * 16f + 8f, NPC.Bottom.Y);
					return;
				}
			}
		}

		/// <summary>A goal that a fight would set beside a drop is moved a few tiles back from the edge (a knock or a bad landing at a rim is how levies end up in holes). Free Rein only.</summary>
		private void FreeReinKeepOffRim(ref Vector2 goal)
		{
			if (!FrOn(32)) return;
			int tx = (int)(goal.X / 16f), ty = (int)(goal.Y / 16f) - 1;
			if (!TileNav.InBounds(tx, ty) || !NearDrop(tx, ty, LevyConstants.FreeReinRimTiles)) return;
			for (int s = 1; s <= 10; s++)
				for (int sgn = -1; sgn <= 1; sgn += 2)
				{
					int x = tx + sgn * s;
					if (!TileNav.InBounds(x, ty)) continue;
					int row = ty; if (!TileNav.IsStandable(x, row)) { row = -1; for (int dy = -2; dy <= 2 && row < 0; dy++) if (TileNav.IsStandable(x, ty + dy)) row = ty + dy; if (row < 0) continue; }
					if (NearDrop(x, row, LevyConstants.FreeReinRimTiles) || TileNav.HasLiquid(x, row)) continue;
					goal = TileNav.BottomCentre(x, row); return;
				}
		}

		/// <summary>
		/// The floor of a pit, a hole or a ravine: lower than the base floor, open to the sky, and walled in on both sides (a solid tile
		/// <see cref="LevyConstants.FreeReinPitWallRows"/> rows up within <see cref="LevyConstants.FreeReinPitWallTiles"/> columns on each side). A courtyard at the base's own level, a house (it has a roof) and a hillside are not.
		/// </summary>
		internal static bool PitFloor(int tx, int ty, int over, int anchorRow)
		{
			if (ty <= anchorRow + LevyConstants.FreeReinPitBelowTiles || over > 1) return false;
			return WalledSide(tx, ty, -1) && WalledSide(tx, ty, 1);
		}

		private static bool WalledSide(int tx, int ty, int dir)
		{
			for (int k = 1; k <= LevyConstants.FreeReinPitWallTiles; k++) if (TileNav.IsSolid(tx + dir * k, ty - LevyConstants.FreeReinPitWallRows)) return true;
			return false;
		}

		/// <summary>
		/// The standing spot for a leg that ends near world x <paramref name="gx"/>: the nearest column (out to the spot radius) that has a valid spot within a few rows of where the levy stands, and in it
		/// the spot nearest the levy's own row. Never a pit floor, a cave or a place outside the box.
		/// </summary>
		private bool FreeReinPickSpot(float gx, bool underworldBase, out Point tile)
		{
			tile = default;
			int cx = (int)(gx / 16f), feet = TileNav.FeetTile(NPC.Bottom).Y, R = LevyConstants.FreeReinSpotRadiusTiles;
			for (int off = 0; off <= R; off++)
				for (int sgn = off == 0 ? 0 : -1; sgn <= 1; sgn += 2)
				{
					int x = cx + sgn * off;
					if (!TileNav.InBounds(x, feet)) continue;
					int bestY = int.MinValue, bestD = int.MaxValue;
					for (int y = feet - 28; y <= feet + 14; y++)
					{
						if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
						int d = Math.Abs(y - feet); if (d >= bestD) continue;
						if (!FreeReinSpotOk(x, y, underworldBase)) continue;
						bestD = d; bestY = y;
					}
					if (bestY != int.MinValue) { tile = new Point(x, bestY); return true; }
					if (sgn == 0) break;
				}
			return false;
		}

		/// <summary>The planned route (in <c>_probePath</c>) does not dip into a hole between its ends, and a spot well below the levy has a route back.</summary>
		private bool FreeReinRouteOk(Vector2 cand)
		{
			int startRow = TileNav.FeetTile(NPC.Bottom).Y, candRow = TileNav.FeetTile(cand).Y;
			int lim = Math.Max(startRow, candRow) + LevyConstants.FreeReinDipTiles;
			for (int i = 0; i < _probePath.Count; i++) if (TileNav.FeetTile(_probePath[i]).Y > lim) return false;
			// and no stretch of it crosses a hole: a gap in the ground that a jump clears on paper and a knock or a bad landing drops a levy into
			Vector2 prev = NPC.Bottom;
			for (int i = 0; i < _probePath.Count; i++)
			{
				Vector2 q = _probePath[i];
				int x0 = (int)(prev.X / 16f), x1 = (int)(q.X / 16f), row = Math.Max(TileNav.FeetTile(prev).Y, TileNav.FeetTile(q).Y), st = Math.Sign(x1 - x0);
				for (int x = x0 + st; st != 0 && x != x1; x += st) { int d = TileNav.DropDepth(x, row); if (d < 0 || d >= LevyConstants.FreeReinHoleTiles) return false; }
				Point wq = TileNav.FeetTile(q); { int d = TileNav.DropDepth(wq.X, wq.Y); if (d < 0 || d >= LevyConstants.FreeReinHoleTiles) return false; } // a waypoint in the air over a gap: a flight over a hole
				prev = q;
			}
			if (candRow > startRow + LevyConstants.FreeReinReturnCheckTiles && !Pathfinder.FindPath(cand, NPC.Bottom, Abilities, _probePath2)) return false;
			return true;
		}

		// ---------------- walking home ----------------
		/// <summary>
		/// A levy outside its box walks back. Each re-plan picks the standing spot (in the sky or the ground, out to the spot radius and 40 rows up) that lies closest to the box and that a route reaches;
		/// when none gets nearer it waits and tries again (the levy is stranded: counted for the lab). True while the levy is out and this think was spent on it.
		/// </summary>
		private bool FreeReinRecall(int now)
		{
			Vector2 here = NPC.Bottom;
			float outside = FreeReinBoxOutside(NPC.Center);
			if (outside <= 2f) { _frRecall = false; return false; }
			if (!_frRecall) { _frRecall = true; _frRecallAt = 0; FreeReinRecalls++; }
			if (now < _frRecallAt) return true;
			_frRecallAt = now + LevyConstants.FreeReinRecallRetryTicks;
			if (HasMoveGoal && MoveGoal == _frRecallGoal && now - _searchGoalTick < LevyConstants.FreeReinGoalTimeoutTicks && Math.Abs(NPC.Bottom.X - _frRecallGoal.X) > 16f * LevyConstants.SearchArriveXTiles) return true;
			if (!SearchMayStart()) { _frRecallAt = now + 20; return true; }
			Vector2 a = FreeReinBase();
			int feet = TileNav.FeetTile(here).Y, px = (int)(here.X / 16f), ax = (int)(a.X / 16f);
			int dirX = Math.Abs(here.X - a.X) > 16f * 3f ? Math.Sign(a.X - here.X) : 0;
			int gx = px + dirX * LevyConstants.FreeReinStepTiles;
			if (dirX != 0 && Math.Sign(ax - gx) != dirX) gx = ax;
			Point best = default; float bestScore = outside - 3f; bool have = false;
			int R = LevyConstants.FreeReinSpotRadiusTiles;
			for (int x = gx - R; x <= gx + R; x++)
				for (int y = feet - 40; y <= feet + 8; y++)
				{
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					if (TileNav.HasLiquid(x, y) || TileNav.HasLiquid(x, y - 1) || TileNav.HasLava(x, y + 1)) continue;
					float o = FreeReinBoxOutside(TileNav.BottomCentre(x, y)) + Math.Abs(x - gx) * 0.02f;
					if (o < bestScore) { bestScore = o; best = new Point(x, y); have = true; }
				}
			if (have)
			{
				Vector2 goal = TileNav.BottomCentre(best.X, best.Y);
				_frRecallGoal = goal; _searchGoal = goal; _searchGoalTick = now;
				SetGoal(goal);   // far or high goals go to the route planner (wings, hooks, climbs) by themselves
				return true;
			}
			FreeReinStranded++;
			ClearGoal();
			return true;
		}

		// ---------------- lab ----------------
		/// <summary>Lab: the local route from where this levy stands to a spot, its waypoints, and what the route check says.</summary>
		internal string LabRouteProbe(Vector2 cand)
		{
			bool found = Pathfinder.FindPath(NPC.Bottom, cand, Abilities, _probePath);
			var sb = new System.Text.StringBuilder($"from=({(int)(NPC.Bottom.X / 16f)},{(int)(NPC.Bottom.Y / 16f)}) to=({(int)(cand.X / 16f)},{(int)(cand.Y / 16f)}) found={found} wings={Abilities.HasWings} pts={_probePath.Count}:");
			foreach (var p in _probePath) sb.Append($" ({(int)(p.X / 16f)},{(int)(p.Y / 16f)})");
			sb.Append($" routeOk={(found ? FreeReinRouteOk(cand) : false)}");
			return sb.ToString();
		}

		/// <summary>One line for the lab: what this free-roaming levy is doing and where it is aiming.</summary>
		internal string FreeReinProbe()
		{
			Vector2 a = FreeReining ? FreeReinBase() : default;
			return $"state={State} target={TargetIndex} why={WhyB} goal={(HasMoveGoal ? ((int)(MoveGoal.X / 16f - a.X / 16f)) + "," + ((int)(MoveGoal.Y / 16f - a.Y / 16f)) : "-")} searchGoal={((int)(_searchGoal.X / 16f - a.X / 16f))},{((int)(_searchGoal.Y / 16f - a.Y / 16f))} dir={_searchDir} legs={_freeLegs} recall={_frRecall} anchor=({(int)(a.X / 16f)},{(int)(a.Y / 16f)}) pos=({(int)(NPC.Center.X / 16f)},{(int)(NPC.Bottom.Y / 16f)}) vel=({NPC.velocity.X:0.0},{NPC.velocity.Y:0.0})";
		}
	}
}
