using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Movement;
using Levy.Common.Soldiers;

namespace Levy.Common.Observe
{
	/// <summary>
	/// Boot self-checks of Command and Observe (pure data and a throw-away strip of synthetic terrain; never throws), and, with env LEVY_OBSERVE_BENCH=1, the lighting benchmark behind PERFORMANCE.md.
	/// Not covered here (needs a live world and a GPU): the soldiers' real movement along the march, the screen draw, multiplayer delivery.
	/// </summary>
	public class ObserveChecks : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try { MapOrderChecks(Check); } catch (Exception e) { failures.Add("map order threw " + e.GetType().Name + ": " + e.Message); }
			try { MindChecks(Check); } catch (Exception e) { failures.Add("mind control threw " + e.GetType().Name + ": " + e.Message); }
			try { CameraChecks(Check); } catch (Exception e) { failures.Add("camera tile threw " + e.GetType().Name + ": " + e.Message); }
			try { ZoomChecks(Check); } catch (Exception e) { failures.Add("zoom threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) Mod.Logger.Info($"Levy observe self-check: {ok} checks OK");
			else
			{
				Mod.Logger.Warn($"Levy observe self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!");
				foreach (var f in failures) Mod.Logger.Warn("  observe: " + f);
			}
			if (Environment.GetEnvironmentVariable("LEVY_OBSERVE_BENCH") == "1") { try { Bench(); } catch (Exception e) { Mod.Logger.Warn("Levy observe bench threw: " + e); } }
		}

		// ---- map order ----
		private static void MapOrderChecks(Action<bool, string> Check)
		{
			// the goal lives in the record's Ext, survives save and load, and the order survives the client snapshot
			var rec = new SoldierRecord { Id = 7, Name = "Pvt. Check" };
			MapOrder.SetGoal(rec, 321, 654); rec.Order = SoldierOrder.GoTo;
			var back = SoldierRecord.Load(rec.Save());
			Check(back.Order == SoldierOrder.GoTo && MapOrder.TryGoal(back, out int gx, out int gy) && gx == 321 && gy == 654, "GoTo order and its goal survive Save/Load");
			using (var ms = new MemoryStream())
			{
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) rec.WriteSnapshot(w);
				ms.Position = 0;
				using var r = new BinaryReader(ms);
				Check(SoldierRecord.ReadSnapshot(r).Order == SoldierOrder.GoTo, "GoTo order survives the roster snapshot");
			}
			MapOrder.ClearGoal(rec);
			Check(!MapOrder.TryGoal(rec, out _, out _), "clearing the goal removes it");

			// the squad fans out: nine distinct offsets, none further than the spread allows
			var seen = new HashSet<int>(); for (int i = 0; i < MapOrder.SpreadSlots; i++) seen.Add(MapOrder.SpreadOffset(i));
			Check(seen.Count == MapOrder.SpreadSlots, "nine soldiers get nine different spread offsets");

			// the march over synthetic terrain: a flat floor with a hill and a gap, 400 tiles wide
			const int X0 = 3000, X1 = 3400, Floor = 500;
			var touched = new List<Point>();
			void Solid(int x, int y) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Dirt; touched.Add(new Point(x, y)); }
			try
			{
				for (int x = X0; x <= X1; x++) { if (x >= 3250 && x <= 3253) continue; for (int y = Floor; y < Floor + 4; y++) Solid(x, y); }
				for (int x = 3150; x <= 3170; x++) for (int y = Floor - 5; y < Floor; y++) Solid(x, y);

				// a click in the air, and one inside rock, both resolve to a standable tile
				Point air = MapOrder.ResolveStand(3380, Floor - 20);
				Check(TileNav.IsStandable(air.X, air.Y) && Math.Abs(air.X - 3380) <= 25, "a click in the air resolves to a standable tile");
				Point rock = MapOrder.ResolveStand(3100, Floor + 2);
				Check(TileNav.IsStandable(rock.X, rock.Y), "a click inside rock resolves to a standable tile");

				// march: every think the leg goal is a standable tile (or the final stand) and the soldier arrives in a handful of thinks
				var stand = MapOrder.ResolveStand(3380, Floor - 20);
				Vector2 fin = TileNav.BottomCentre(stand.X, stand.Y);
				Vector2 pos = TileNav.BottomCentre(X0 + 10, Floor - 1); // bottom centre; the body centre is 24 px up
				int thinks = 0; bool allStandable = true, arrived = false;
				for (; thinks < 40; thinks++)
				{
					Vector2 center = pos - new Vector2(0f, 24f);
					if (Math.Abs(fin.X - center.X) <= 3 * 16f) { arrived = true; break; }
					Vector2 leg = MapOrder.LegGoal(center, pos, fin);
					int lx = (int)(leg.X / 16f), ly = (int)((leg.Y - 1f) / 16f);
					if (leg != fin && !TileNav.IsStandable(lx, ly) && Math.Abs(leg.Y - pos.Y) > 1f) allStandable = false; // a leg off the floor is only allowed when no floor was found (it then keeps his own height)
					pos = leg;
				}
				Check(arrived && thinks <= 14, $"the march from 370 tiles away arrives in {thinks} leg steps (30-tile legs: 13 expected, got {thinks})");
				Check(allStandable, "every leg of the march targets a standable tile");
			}
			finally { foreach (var p in touched) { Tile t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileType = 0; } }
		}

		// ---- mind control ----
		private static void MindChecks(Action<bool, string> Check)
		{
			// pack / unpack of every key combination and the aim point
			bool all = true;
			for (int m = 0; m < 64; m++)
			{
				var s = MindInputState.Unpack((byte)m, new Vector2(12.5f, -3f));
				if (s.Pack() != m) all = false;
			}
			Check(all, "all 64 key combinations survive pack/unpack");

			// the MindInput packet payload, written and read back in the documented order (byte flags, float x, float y)
			using (var ms = new MemoryStream())
			{
				var s = new MindInputState { Left = true, Jump = true, Use = true, Aim = new Vector2(1234.5f, 678.25f) };
				using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { w.Write(s.Pack()); w.Write(s.Aim.X); w.Write(s.Aim.Y); }
				ms.Position = 0; using var r = new BinaryReader(ms);
				var back = MindInputState.Unpack(r.ReadByte(), new Vector2(r.ReadSingle(), r.ReadSingle()));
				Check(back.Left && back.Jump && back.Use && !back.Right && !back.Down && !back.Alt && back.Aim == s.Aim, "the MindInput payload round-trips");
			}

			var c = new Vector2(1000f, 800f);
			MindControl.Map(new MindInputState { Left = true }, c, 816f, true, false, out bool g, out Vector2 goal, out bool j, out bool d);
			Check(g && goal.X < c.X && goal.Y == 816f && !j && !d, "left walks left");
			MindControl.Map(new MindInputState { Right = true }, c, 816f, true, false, out g, out goal, out j, out d);
			Check(g && goal.X > c.X && !j, "right walks right");
			MindControl.Map(new MindInputState { Left = true, Right = true }, c, 816f, true, false, out g, out goal, out j, out d);
			Check(!g, "left and right together cancel");
			MindControl.Map(default, c, 816f, true, false, out g, out goal, out j, out d);
			Check(!g && !j && !d, "no keys: no goal, no jump");
			MindControl.Map(new MindInputState { Jump = true }, c, 816f, true, false, out g, out goal, out j, out d);
			Check(j && !g, "jump on the ground jumps");
			MindControl.Map(new MindInputState { Jump = true, Right = true }, c, 816f, false, true, out g, out goal, out j, out d);
			Check(!j && g && goal.Y < 816f - 100f, "jump in the air with wings lifts the goal");
			MindControl.Map(new MindInputState { Jump = true }, c, 816f, false, false, out g, out goal, out j, out d);
			Check(!j && !g, "jump in the air without wings does nothing");
			MindControl.Map(new MindInputState { Down = true }, c, 816f, true, false, out g, out goal, out j, out d);
			Check(d, "down arms a platform drop");
		}

		// ---- camera tile ----
		private static void CameraChecks(Action<bool, string> Check)
		{
			var te = ModContent.GetInstance<StaticCameraTE>();
			string keep = te.Label;
			try
			{
				te.Label = "Arena North";
				var tag = new TagCompound(); te.SaveData(tag);
				using (var ms = new MemoryStream()) { TagIO.ToStream(tag, ms); ms.Position = 0; tag = TagIO.FromStream(ms); } // through the actual NBT bytes a world save writes
				te.Label = "";
				te.LoadData(tag);
				Check(te.Label == "Arena North", "the camera label survives SaveData / LoadData through the tag bytes");
				using (var ms = new MemoryStream())
				{
					using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) te.NetSend(w);
					te.Label = ""; ms.Position = 0; using var r = new BinaryReader(ms); te.NetReceive(r);
					Check(te.Label == "Arena North", "the camera label survives NetSend / NetReceive");
				}
				te.Label = ""; var empty = new TagCompound(); te.SaveData(empty); te.LoadData(empty);
				Check(te.Label == "", "an unnamed camera saves nothing and loads unnamed");
			}
			finally { te.Label = keep; }
			Check(ModContent.TileType<Content.Tiles.StaticCameraTile>() > 0 && ModContent.ItemType<Content.Items.Observe.StaticCameraItem>() > 0, "the camera tile and item are registered");
		}

		// ---- zoom limits ----
		private static void ZoomChecks(Action<bool, string> Check)
		{
			Check(ArenaView.ClampFactor(0f) == 1f && ArenaView.ClampFactor(9f) == 3f && ArenaView.ClampFactor(float.NaN) == 1f && ArenaView.ClampFactor(2f) == 2f, "the arena zoom is held between 1 and 3 per side");
			Check(!ArenaView.Active && !ArenaView.Inflated, "no view is engaged at boot");
		}

		// =====================================================================================================
		// benchmark (LEVY_OBSERVE_BENCH=1): CPU time of the lighting engines at the screen sizes a zoomed-out camera needs.
		// =====================================================================================================
		private void Bench()
		{
			try { TimeLogger.Initialize(); } catch (Exception) { } // the server has no draw-time log; the lighting engines write to it
			int sw0 = Main.screenWidth, sh0 = Main.screenHeight; var mode0 = Lighting.Mode; bool map0 = Main.mapEnabled;
			const int X0 = 1000, Y0 = 300, W = 700, H = 260;
			var touched = new List<Point>();
			try
			{
				// synthetic cave terrain: rolling surface with a dirt wall behind everything below it, pockets of air, a torch here and there
				for (int x = X0; x < X0 + W; x++)
				{
					int surf = Y0 + 60 + (int)(14 * Math.Sin(x / 37.0) + 6 * Math.Sin(x / 11.0));
					for (int y = Y0; y < Y0 + H; y++)
					{
						Tile t = Main.tile[x, y]; touched.Add(new Point(x, y));
						if (y < surf) continue;
						t.WallType = WallID.Dirt;
						uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
						bool pocket = ((h >> 3) % 100) < 14 && y > surf + 6;
						if (!pocket) { t.HasTile = true; t.TileType = TileID.Dirt; }
						else if ((h >> 11) % 40 == 0) { t.HasTile = true; t.TileType = TileID.Torches; }
					}
				}
				Main.mapEnabled = false;
				Mod.Logger.Info($"LEVY-OBSERVE-BENCH base screen 1920x1080, CPUs={Environment.ProcessorCount}; columns: factor, tiles in view, ms per light pass (median of 15)");
				foreach (float f in new[] { 1f, 1.5f, 2f, 2.5f, 3f })
				{
					Main.screenWidth = (int)(1920 * f); Main.screenHeight = (int)(1080 * f);
					int fx = X0 + 120, fy = Y0 + 20, lx = fx + Main.screenWidth / 16 + 2, ly = fy + Main.screenHeight / 16 + 2;
					foreach (var m in new[] { LightMode.Retro, LightMode.Color })
					{
						Lighting.Mode = m; Lighting.Initialize();
						var times = new List<double>();
						for (int i = 0; i < 20; i++)
						{
							Main.renderCount = 0; Main.renderNow = false;
							var sw = Stopwatch.StartNew();
							// the Retro (legacy) engine spreads one update over three consecutive calls (renderCount 1, 2, 3); the Color engine does all of it each call
							int calls = m == LightMode.Retro ? 3 : 1;
							for (int c = 0; c < calls; c++) Lighting.LightTiles(fx, lx, fy, ly);
							sw.Stop(); if (i >= 5) times.Add(sw.Elapsed.TotalMilliseconds);
						}
						times.Sort();
						Mod.Logger.Info($"LEVY-OBSERVE-BENCH lighting {m} factor={f:0.0} tiles={(lx - fx) * (ly - fy)} ms={times[times.Count / 2]:0.00} (per full update{(m == LightMode.Retro ? ", 3 calls" : "")})");
					}
					// the least the tile pass can cost: visit every visible tile, read its flags and its light (what TileDrawing does before it draws anything)
					Lighting.Mode = LightMode.Retro; Lighting.Initialize(); Main.renderCount = 0; Lighting.LightTiles(fx, lx, fy, ly);
					var tl = new List<double>(); long solid = 0;
					for (int i = 0; i < 15; i++)
					{
						var sw = Stopwatch.StartNew();
						for (int x = fx; x < lx; x++) for (int y = fy; y < ly; y++) { Tile t = Main.tile[x, y]; if (t.HasTile) { solid++; solid += (long)(Lighting.GetColor(x, y).R); } else if (t.WallType > 0) solid += (long)(Lighting.GetColor(x, y).G); }
						sw.Stop(); if (i >= 3) tl.Add(sw.Elapsed.TotalMilliseconds);
					}
					tl.Sort();
					Mod.Logger.Info($"LEVY-OBSERVE-BENCH tilescan factor={f:0.0} tiles={(lx - fx) * (ly - fy)} ms={tl[tl.Count / 2]:0.00} (sink={solid})");
				}
			}
			finally
			{
				foreach (var p in touched) { Tile t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileType = 0; t.WallType = 0; }
				Main.screenWidth = sw0; Main.screenHeight = sh0; Lighting.Mode = mode0; Main.mapEnabled = map0;
				try { Lighting.Initialize(); } catch (Exception) { }
			}
		}
	}
}
