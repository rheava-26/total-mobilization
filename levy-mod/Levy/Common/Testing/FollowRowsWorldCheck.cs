using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// World self-check of the follow rows, logged as "[FollowRowsWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1. Builds a floor with three platform rows in the sky (one with a hole,
	/// one too short to stand a levy on), stands a stand-in commander on the middle row and asserts that the shared scan finds the commander's row, the other platforms and the floor, splits a holed row into two runs,
	/// ignores a three-tile ledge, is cached between rebuilds, costs well under a millisecond, hands out lanes at least two and a half tiles apart, and keeps a body's width over the row. The
	/// levy-level behaviour (rows used, share per row, spacing, nobody stranded) is the platspread harness scenario. Everything is removed again; nothing is saved.
	/// </summary>
	public class FollowRowsWorldCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			Mod.Logger.Info("[FollowRowsWorld] start");
			void Check(string name, bool ok) { if (ok) pass++; else fail++; Mod.Logger.Info($"[FollowRowsWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
			int x0 = Main.maxTilesX / 2 + 120, yF = Math.Max(150, (int)Main.worldSurface - 40);
			var touched = new List<Point>();
			void Set(int x, int y, ushort type, bool on) { Tile t = Main.tile[x, y]; touched.Add(new Point(x, y)); t.HasTile = on; t.TileType = type; t.TileFrameX = 0; t.TileFrameY = 0; t.WallType = 0; t.LiquidAmount = 0; t.BlockType = Terraria.ID.BlockType.Solid; }
			try
			{
				for (int x = x0 - 60; x <= x0 + 60; x++) for (int y = yF - 30; y <= yF + 2; y++) Set(x, y, TileID.Dirt, y > yF); // air above a two-thick dirt floor (top row yF + 1)
				int[] plat = { yF - 6, yF - 12, yF - 18 };                                  // platform tile rows; the feet rows stand one above
				for (int x = x0 - 40; x <= x0 + 40; x++) Set(x, plat[0], TileID.Platforms, true);
				for (int x = x0 - 40; x <= x0 + 40; x++) Set(x, plat[1], TileID.Platforms, !(x >= x0 + 14 && x <= x0 + 17)); // a four-tile hole at +14..+17
				for (int x = x0 + 5; x <= x0 + 7; x++) Set(x, plat[2], TileID.Platforms, true);  // a three-tile ledge: too short for a row
				FollowRows.Reset();
				var cmd = new Player(); cmd.active = true;
				int cmdFeet = plat[1] - 1;
				cmd.position = new Vector2(x0 * 16f - cmd.width / 2f, (cmdFeet + 1) * 16f - cmd.height); cmd.velocity = Vector2.Zero;
				int now = 1000;
				var set = FollowRows.Get(cmd, now);
				Check("the scan finds rows around a commander standing on a platform", set != null && set.Valid);
				if (set == null) return;
				var ys = new List<int>(); for (int i = 0; i < set.Count; i++) ys.Add(set.Rows[i].Y);
				Check($"it finds the floor and the two long platforms and not the 3-tile ledge (rows y={string.Join(",", ys)})", set.Count == 3 && ys.Contains(yF) && ys.Contains(plat[0] - 1) && ys.Contains(plat[1] - 1) && !ys.Contains(plat[2] - 1));
				Check("the commander's row is first and is marked", set.CmdRow == 0 && set.Rows[0].Y == cmdFeet);
				int hi = set.IndexOfY(plat[1] - 1), lo = set.IndexOfY(plat[0] - 1);
				Check("a platform with a hole is two runs", hi >= 0 && set.Rows[hi].Runs == 2 && !set.Rows[hi].Has(x0 + 15) && set.Rows[hi].Has(x0 + 13) && set.Rows[hi].Has(x0 + 18));
				Check("a clear platform is one run of 81 tiles", lo >= 0 && set.Rows[lo].Runs == 1 && set.Rows[lo].Width == 81);
				uint built = set.Built; var again = FollowRows.Get(cmd, now + 10); long rb = FollowRows.Rebuilds;
				Check("the scan is cached between rebuilds", again == set && again.Built == built && FollowRows.Rebuilds == rb);
				FollowRows.Get(cmd, now + FollowRows.RebuildTicks + 1);
				Check("the scan refreshes after the rebuild interval", FollowRows.Rebuilds == rb + 1);
				var sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 20; i++) FollowRows.Get(cmd, now + 1000 + i * FollowRows.RebuildTicks); sw.Stop();
				double ms = sw.Elapsed.TotalMilliseconds / 20.0;
				Check($"a row scan costs well under a millisecond ({ms:0.000} ms)", ms < 1.0);
				// lane geometry: normal and spread stances, either side
				bool spacing = true, start = true;
				foreach (bool spread in new[] { false, true })
				{
					float minGap = spread ? 5.9f : 2.9f;
					for (int k = 2; k < 14; k++)
					{
						float a = SoldierNPC.FrLaneOffset(k - 2, spread), b = SoldierNPC.FrLaneOffset(k, spread);
						if (Math.Abs(Math.Abs(b) - Math.Abs(a)) < minGap || Math.Sign(a) != Math.Sign(b)) spacing = false;
					}
					float l0 = SoldierNPC.FrLaneOffset(0, spread), l1 = SoldierNPC.FrLaneOffset(1, spread);
					if (Math.Abs(l0) < 2.5f || Math.Abs(l1) < 2.5f || l0 >= 0 || l1 <= 0) start = false;
				}
				Check("lanes on one side are at least 3 tiles apart (6 on Spread out) and alternate sides", spacing);
				Check("the first lanes are at least 2.5 tiles from the commander", start);
				float clamped = FollowRows.ClampToRun(set, (x0 + 15.5f) * 16f) / 16f;
				Check($"a plain slot over the hole in the commander's row is pulled onto the row (x={clamped - x0:0.0} from the commander)", clamped <= x0 + 13.3f && clamped >= x0 + 12.9f);
				// a commander in the air keeps the rows for a while and then has none
				cmd.velocity = new Vector2(0f, 5f); var air = FollowRows.Get(cmd, now + 1955);
				var gone = FollowRows.Get(cmd, now + 2400);
				Check("a commander falling keeps the last rows for a moment and then gives none", air != null && gone == null);
			}
			catch (Exception e) { fail++; Mod.Logger.Info("[FollowRowsWorld] FAIL threw " + e); }
			finally
			{
				foreach (var p in touched) { Tile t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileType = 0; }
				FollowRows.Reset();
			}
			Mod.Logger.Info($"[FollowRowsWorld] self-check pass={pass} fail={fail}");
		}
	}
}
