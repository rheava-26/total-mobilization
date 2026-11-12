using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TRAVEL LAB, spec-driven mode (round 8). Everything the no-code runner (lab/travel/travelab) needs on top of the world course: a start point and a goal given as tokens, world edits (a hellevator),
	/// several runs inside one boot, and gear kits by item name. Measurement only. Env (world course):
	/// LEVY_TRAVEL_START = spawn | X | X,Y (tile)      LEVY_TRAVEL_GOAL = far | east:N | west:N | surface:X | underworld | cave[:depth] | X,Y (tile of the floor)
	/// LEVY_TRAVEL_EDIT  = hellevator:DX[:W[:PLAT[:RING]]] (a shaft DX tiles east of the start, W wide, a platform landing every PLAT rows (0 = none), a platform ring RING tiles wide each side at the bottom); several edits split by ';'
	/// LEVY_TRAVEL_RUNS = N runs in this boot, LEVY_TRAVEL_SPREAD = tiles over which the start x of the runs is spread. Gear entries (LEVY_TRAVEL_MIX): kit:ItemA+ItemB+hk=HookItem (ItemID names).
	/// </summary>
	public partial class CombatHarness
	{
		private bool _tvSpec; private int _tvRuns = 1, _tvRun, _tvSpread; private string _tvOutBase = "";
		private string _tvStartTok = "", _tvGoalTok = "", _tvEditTok = "";
		private int _tvStartRowAbs = -1, _tvGoalRowAbs = int.MinValue, _tvBaseX = -1;
		private bool _tvWorldReady; private int _tvShaftX = -1, _tvShaftW = 4, _tvRingRow = -1, _tvRingHalf = 12;
		private int _tvStartX0, _tvStartD;

		private void TvSpecInit()
		{
			_tvStartTok = Environment.GetEnvironmentVariable("LEVY_TRAVEL_START") ?? "";
			_tvGoalTok = Environment.GetEnvironmentVariable("LEVY_TRAVEL_GOAL") ?? "";
			_tvEditTok = Environment.GetEnvironmentVariable("LEVY_TRAVEL_EDIT") ?? "";
			_tvSpec = TvWorld && (_tvStartTok.Length > 0 || _tvGoalTok.Length > 0 || _tvEditTok.Length > 0);
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_RUNS"), out int r) && r > 0) _tvRuns = r;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_SPREAD"), out int sp)) _tvSpread = sp;
			_tvRun = 0; _tvWorldReady = false; _tvOutBase = _tvOut;
		}

		/// <summary>Per-run output directory: the base for one run, base/rN for several.</summary>
		private void TvSpecOutDir()
		{
			_tvOut = _tvRuns > 1 ? Path.Combine(_tvOutBase, "r" + _tvRun) : _tvOutBase;
			Directory.CreateDirectory(_tvOut);
		}

		// ---------------- world edits ----------------
		private static int TvTopSolid(int x)
		{
			int y0 = (int)(Main.worldSurface * 0.6);
			for (int y = y0; y < Main.maxTilesY - 10; y++) { Tile t = Main.tile[x, y]; if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) return y; }
			return y0;
		}

		private void TvEditWorld(int baseX)
		{
			if (_tvEditTok.Length == 0) return;
			foreach (string ed in _tvEditTok.Split(';'))
			{
				var q = ed.Split(':'); if (q.Length == 0) continue;
				if (q[0] == "hellevator")
				{
					int dx = q.Length > 1 && int.TryParse(q[1], out int a) ? a : 120, w = q.Length > 2 && int.TryParse(q[2], out int b) ? b : 4, plat = q.Length > 3 && int.TryParse(q[3], out int c) ? c : 0, ring = q.Length > 4 && int.TryParse(q[4], out int d) ? d : 12;
					int x = Math.Clamp(baseX + dx, 30, Main.maxTilesX - 30 - w);
					int top = TvTopSolid(x), ringY = Main.UnderworldLayer + 30, bottom = ringY + 8;
					_tvShaftX = x; _tvShaftW = w; _tvRingRow = ringY; _tvRingHalf = ring;
					// the room at the bottom: ring tiles each side of the shaft, 14 clear rows over a platform floor, stone below it
					for (int xx = x - ring - 1; xx <= x + w + ring; xx++)
						for (int y = ringY - 14; y <= ringY + 6; y++)
						{
							bool wallX = xx == x - ring - 1 || xx == x + w + ring, wallY = y == ringY - 14 || y >= ringY + 1;
							if (wallX || wallY) { TvSet(xx, y, TileID.GrayBrick); Tile t = Main.tile[xx, y]; t.LiquidAmount = 0; }
							else TvAir(xx, y);
						}
					// the lining of the shaft: a stone skin on both sides (the world's own water, sand and lava stay out), then the shaft itself
					for (int y = top; y <= ringY - 14; y++)
					{
						foreach (int lx in new[] { x - 1, x + w })
						{
							Tile t = Main.tile[lx, y];
							if (!t.HasTile || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]) { TvSet(lx, y, TileID.GrayBrick); t = Main.tile[lx, y]; t.LiquidAmount = 0; }
						}
						for (int xx = x; xx < x + w; xx++) TvAir(xx, y);
					}
					for (int y = ringY - 14; y < ringY - 12; y++) for (int xx = x; xx < x + w; xx++) TvAir(xx, y);
					// platform landings down the shaft, then the ring (a platform floor across the room)
					if (plat > 0) for (int y = top + plat; y < ringY - 14; y += plat) for (int xx = x; xx < x + w; xx++) TvPlat(xx, y);
					for (int xx = x - ring; xx <= x + w + ring - 1; xx++) TvPlat(xx, ringY);
					_tvEvents.AppendLine($"EDIT hellevator x={x} w={w} top={top} ringRow={ringY} plat={plat} ring={ring} (shaft length {ringY - top} rows)");
				}
			}
		}

		private static void TvPlat(int x, int y)
		{
			Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid; t.LiquidAmount = 0;
		}

		// ---------------- start, goal ----------------
		private static bool TvParseXY(string s, out int x, out int y)
		{
			x = y = -1; var q = s.Split(',');
			if (q.Length == 0 || !int.TryParse(q[0], out x)) return false;
			if (q.Length > 1) int.TryParse(q[1], out y);
			return true;
		}

		/// <summary>Floor row (top solid row) of the first standing place at or below (tx, ty); the standing search starts ty - 3 and reaches 400 rows.</summary>
		private static int TvFloorBelow(int tx, int ty)
		{
			for (int y = Math.Max(10, ty - 3); y < Math.Min(Main.maxTilesY - 8, ty + 400); y++) if (TileNav.IsStandable(tx, y)) return y + 1;
			return -1;
		}

		/// <summary>The nearest standing place to (cx, cy) with room around it (a 7-wide clear stretch, no lava or water), columns searched outward to maxDx, rows cy-dy..cy+dy.</summary>
		private static bool TvFindRoom(int cx, int cy, int maxDx, int dy, out int fx, out int fy)
		{
			for (int d = 0; d <= maxDx; d++)
				for (int sgn = -1; sgn <= 1; sgn += 2)
				{
					int x = cx + sgn * d; if (x < 20 || x > Main.maxTilesX - 20) continue;
					for (int r = 0; r <= dy; r++)
						for (int s2 = -1; s2 <= 1; s2 += 2)
						{
							int y = cy + s2 * r;
							if (!TileNav.IsStandable(x, y)) continue;
							bool ok = true;
							for (int k = -3; k <= 3 && ok; k++) if (!TileNav.IsStandable(x + k, y) || TileNav.HasLiquid(x + k, y) || TileNav.HasLiquid(x + k, y - 1)) ok = false;
							if (ok) { fx = x; fy = y; return true; }
							if (r == 0) break;
						}
				}
			fx = cx; fy = cy; return false;
		}

		/// <summary>
		/// A deep cave a body could get to: flood the dry open cells (three clear rows, no liquid) 4-connected from the surface at the start, within 600 columns and down to depth+60 rows, and take the standing room
		/// (3 clear columns) whose depth is nearest the wanted one (at least 40 rows down): the deepest cave the surface connects to when the world has nothing at the wanted depth. A property of the world, not a route: whether a levy's gear gets there is the test. False when none is connected.
		/// </summary>
		private bool TvFindCave(int startX, int depth, out int fx, out int fy)
		{
			fx = startX; fy = 0;
			int srf = TvTopSolid(startX), x0 = Math.Max(20, startX - 600), x1 = Math.Min(Main.maxTilesX - 20, startX + 600), y0 = srf - 30, y1 = Math.Min(Main.maxTilesY - 30, srf + depth + 60);
			int w = x1 - x0 + 1, h = y1 - y0 + 1; var seen = new bool[w * h]; var q = new Queue<int>();
			int sx = -1, sy = -1;
			for (int d = 0; d < 30 && sx < 0; d++) for (int y = srf - 6; y < srf + 2 && sx < 0; y++) if (TileNav.IsStandable(startX + d, y)) { sx = startX + d; sy = y; }
			if (sx < 0) return false;
			seen[(sx - x0) + (sy - y0) * w] = true; q.Enqueue((sx - x0) + (sy - y0) * w);
			int best = -1, bestScore = int.MaxValue, reached = 0, deepest = 0;
			while (q.Count > 0)
			{
				int cur = q.Dequeue(); int cx = x0 + cur % w, cy = y0 + cur / w; reached++; if (cy - srf > deepest) deepest = cy - srf;
				if (cy - srf >= Math.Min(40, depth) && TileNav.IsStandable(cx, cy))
				{
					bool room = true; for (int k = -1; k <= 1 && room; k++) if (!TileNav.IsStandable(cx + k, cy) || TileNav.HasLiquid(cx + k, cy)) room = false;
					int score = Math.Abs(cy - srf - depth) * 4 + Math.Abs(cx - startX) / 4;
					if (room && score < bestScore) { bestScore = score; best = cur; }
				}
				for (int k = 0; k < 4; k++)
				{
					int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
					if (nx < x0 || nx > x1 || ny < y0 || ny > y1) continue;
					int ni = (nx - x0) + (ny - y0) * w; if (seen[ni]) continue;
					if (!TileNav.IsBodyClear(nx, ny) || TileNav.HasLiquid(nx, ny) || TileNav.HasLiquid(nx, ny - 1)) continue;
					seen[ni] = true; q.Enqueue(ni);
				}
			}
			if (best < 0) { Mod.Logger.Info($"HARNESS-TRAVEL: cave flood from ({sx},{sy}) reached {reached} cells, deepest {deepest} rows below the surface, none a room at depth {depth}"); return false; }
			fx = x0 + best % w; fy = y0 + best / w; Mod.Logger.Info($"HARNESS-TRAVEL: cave goal ({fx},{fy}) is {fy - srf} rows below the surface (wanted {depth}; the flood reached {deepest}); {reached} open cells"); return true;
		}

		/// <summary>Sets TvX0 (start column), _tvFrom, _tvTo and the absolute rows for run number <paramref name="run"/>.</summary>
		private void TvSpecPlace(int run)
		{
			if (_tvBaseX < 0)
			{
				string st = _tvStartTok.Length > 0 ? _tvStartTok : "spawn";
				_tvBaseX = st == "spawn" ? Main.spawnTileX : (TvParseXY(st, out int sx, out int sy) ? sx : Main.spawnTileX);
				_tvStartRowAbs = -1; if (st != "spawn" && TvParseXY(st, out _, out int sy2) && sy2 > 0) _tvStartRowAbs = sy2;
			}
			int off = _tvRuns > 1 ? (int)Math.Round((run - (_tvRuns - 1) / 2f) * _tvSpread / (float)Math.Max(1, _tvRuns - 1)) : 0;
			int startX = Math.Clamp(_tvBaseX + off, 60, Main.maxTilesX - 60);
			TvX0 = startX; _tvFrom = 0; _tvGoalRowAbs = int.MinValue;
			string g = _tvGoalTok.Length > 0 ? _tvGoalTok : "far";
			string key = g.Split(':')[0]; int arg = g.Contains(':') && int.TryParse(g.Substring(g.IndexOf(':') + 1), out int ga) ? ga : -1;
			int goalX;
			if (key == "east") goalX = startX + (arg > 0 ? arg : 800);
			else if (key == "west") goalX = startX - (arg > 0 ? arg : 800);
			else if (key == "far") goalX = _tvBaseX < Main.maxTilesX / 2 ? Main.maxTilesX - 350 : 350;   // (the side is fixed by the spec's start, not by each run's spread)
			else if (key == "surface") goalX = arg > 0 ? arg : startX + 800;
			else if (key == "underworld")
			{
				if (_tvShaftX >= 0) { goalX = _tvShaftX + _tvShaftW + Math.Min(_tvRingHalf - 2, 8); _tvGoalRowAbs = _tvRingRow; }
				else if (TvFindRoom(startX, Main.UnderworldLayer + 60, 600, 60, out int ux, out int uy)) { goalX = ux; _tvGoalRowAbs = uy + 1; }
				else { goalX = startX; _tvGoalRowAbs = Main.UnderworldLayer + 60; _tvEvents.AppendLine("GOAL underworld: no standing room found"); }
			}
			else if (key == "cave")
			{
				int depth = arg > 0 ? arg : 150;
				int srf = TvTopSolid(startX);
				if (TvFindCave(startX, depth, out int cx, out int cy)) { goalX = cx; _tvGoalRowAbs = cy + 1; }
				else if (TvFindRoom(startX, srf + depth, 500, 60, out cx, out cy)) { goalX = cx; _tvGoalRowAbs = cy + 1; _tvEvents.AppendLine("GOAL cave: no cave connected to the surface within reach; nearest standing room (may be sealed)"); }
				else { goalX = startX; _tvGoalRowAbs = srf + depth; _tvEvents.AppendLine("GOAL cave: no standing room found"); }
			}
			else if (TvParseXY(g, out int ax, out int ay)) { goalX = ax; if (ay > 0) _tvGoalRowAbs = ay; }
			else goalX = startX + 800;
			goalX = Math.Clamp(goalX, 20, Main.maxTilesX - 20);
			_tvTo = goalX - startX;
			_tvEvents.AppendLine($"SPEC run={run} start=({startX},{(_tvStartRowAbs > 0 ? _tvStartRowAbs.ToString() : "surface")}) goal={g} -> x={goalX} row={(_tvGoalRowAbs == int.MinValue ? "surface" : _tvGoalRowAbs.ToString())} shaft={_tvShaftX}");
		}

		/// <summary>The row spawn / commander use: the explicit start row's standing place, else the dry surface.</summary>
		private int TvSpawnRow(int off)
		{
			if (_tvSpec && _tvStartRowAbs > 0)
			{
				int tx = Math.Clamp(TvX0 + off, 10, Main.maxTilesX - 10), r = TvFloorBelow(tx, _tvStartRowAbs);
				if (r < 0) { for (int k = 1; k < 30 && r < 0; k++) { r = TvFloorBelow(tx + k, _tvStartRowAbs); if (r < 0) r = TvFloorBelow(tx - k, _tvStartRowAbs); } }
				if (r > 0) return r;
			}
			return TvRow(off);
		}

		private void TvSpecBuild()
		{
			if (!_tvWorldReady)
			{
				int bx = _tvStartTok.Length > 0 && _tvStartTok != "spawn" && TvParseXY(_tvStartTok, out int sx, out _) ? sx : Main.spawnTileX;
				TvEditWorld(bx);
				TvScanSurface();
				{ var sf = new StringBuilder("x,surfaceRow,dry\n"); for (int x = 0; x < Main.maxTilesX; x++) sf.AppendLine($"{x},{TvSurf[x]},{(TvSurf[x] > 0 && TvDry(x) ? 1 : 0)}"); File.WriteAllText(Path.Combine(_tvOut, "surface.csv"), sf.ToString()); }
				_tvWorldReady = true;
				_tvEvents.AppendLine($"tileSolid: cactus={Main.tileSolid[TileID.Cactus]} solidTop={Main.tileSolidTop[TileID.Cactus]} sand={Main.tileSolid[TileID.Sand]}");
			}
			TvSpecPlace(_tvRun);
			_tvEvents.AppendLine($"world mode: X0={TvX0} world={Main.maxTilesX}x{Main.maxTilesY} surface={Main.worldSurface:0} rock={Main.rockLayer:0} underworld={Main.UnderworldLayer}");
		}

		/// <summary>After a run's files are written: start the next one in the same boot. False when that was the last run.</summary>
		private bool TvNextRun()
		{
			if (!_tvSpec || _tvRun + 1 >= _tvRuns) return false;
			_tvRun++;
			OffscreenHop.LabViewers = null;
			ClearAll();
			_tv.Clear(); _tvTrace.Clear(); _tvEvents.Clear(); _tvAscii = 0; _tvKnockTick.Clear(); SoldierHooks.CastLog.Clear();
			RoutePlanner.LabReset();
			OffscreenHop.LabNoHop = Environment.GetEnvironmentVariable("LEVY_TRAVEL_NOHOP") == "1";
			_tvTick = 0; _tvStarted = false;
			TvSpecOutDir();
			return true;
		}

		// ---------------- gear kits ----------------
		/// <summary>kit:ItemA+ItemB+hk=HookItem -> accessories in order, hook in the hook slot (ItemID field names). An unknown name is logged and skipped.</summary>
		private bool TvKit(SoldierRecord rec, string g)
		{
			if (!g.StartsWith("kit:")) return false;
			int slot = 0;
			foreach (string part in g.Substring(4).Split('+'))
			{
				if (part.Length == 0) continue;
				bool hook = part.StartsWith("hk="); string name = hook ? part.Substring(3) : part;
				var f = typeof(ItemID).GetField(name);
				if (f == null) { _tvEvents.AppendLine("GEARERR unknown item " + name); Mod.Logger.Warn("HARNESS-TRAVEL: GEARERR unknown item " + name); continue; }
				var it = new Item(); it.SetDefaults((int)(short)f.GetValue(null));
				if (hook) { rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				else if (slot < SoldierGear.AccessorySlots) { rec.Gear.Accessories[slot] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0 + slot, true); slot++; }
			}
			return true;
		}
	}
}
