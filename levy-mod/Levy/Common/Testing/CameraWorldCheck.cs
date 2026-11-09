using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common.Observe;
using Levy.Content.Items.Observe;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// World self-check of the static camera, logged as "[CameraWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1. Places a camera through the same tile-object
	/// and post-placement-hook path the item uses (with the hook's real "processed coordinates" convention), then asserts the tile entity exists at the camera's top-left, every one of its six
	/// tiles finds it, it would survive a reload (IsTileValidForEntity), the tile advertises smart-interact (the interaction highlight), the hover sets the cursor item icon, and a real
	/// right-click dispatch through the player's tile-interaction code connects the view. Everything it makes is removed again in the same call; nothing is saved.
	/// </summary>
	public class CameraWorldCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private void Run()
		{
			int pass = 0, fail = 0;
			void Check(string name, bool ok) { if (ok) pass++; else fail++; Mod.Logger.Info($"[CameraWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
			var touched = new List<Point>();
			int type = ModContent.TileType<StaticCameraTile>();
			StaticCameraTE te = null;
			int x0 = Main.maxTilesX / 2 + 60, y0 = Math.Max(100, (int)Main.worldSurface - 40);
			try
			{
				// a clean pad: air above, a six-wide dirt floor under it
				for (int x = x0 - 3; x <= x0 + 6; x++)
					for (int y = y0 - 8; y <= y0 + 1; y++)
					{
						Tile t = Main.tile[x, y]; touched.Add(new Point(x, y));
						t.HasTile = y == y0 + 1 && x >= x0 - 1 && x <= x0 + 4; t.TileType = TileID.Dirt; t.TileFrameX = 0; t.TileFrameY = 0; t.WallType = 0;
					}
				TileObjectData data = TileObjectData.GetTileData(type, 0);
				Check("camera tile object data exists with its post-placement hook", data != null && data.HookPostPlaceMyPlayer.hook != null);
				int i = x0, j = y0; // the placement coordinate is the origin tile (bottom-left of the 2x3)
				bool can = TileObject.CanPlace(i, j, type, 0, 1, out TileObject obj, false);
				Check("camera can be placed on the pad", can);
				if (!can) return;
				Check("TileObject.Place succeeds", TileObject.Place(obj));
				WorldGen.SquareTileFrame(i, j);
				// the player's path then runs the post-placement hook with the top-left (processed) coordinates when the hook asks for them
				PlacementHook hook = data.HookPostPlaceMyPlayer;
				int hx = hook.processedCoordinates ? obj.xCoord : i, hy = hook.processedCoordinates ? obj.yCoord : j;
				hook.hook(hx, hy, type, obj.style, 1, obj.alternate);

				int left = obj.xCoord, top = obj.yCoord;
				Check($"camera tile stands at top-left ({left},{top}) with frame 0,0", Main.tile[left, top].HasTile && Main.tile[left, top].TileType == type && Main.tile[left, top].TileFrameX == 0 && Main.tile[left, top].TileFrameY == 0);
				bool at = TileEntity.ByPosition.TryGetValue(new Point16(left, top), out TileEntity found);
				te = found as StaticCameraTE;
				Check("tile entity exists at the camera's top-left after placement", at && te != null);
				if (te == null)
				{
					var stray = new List<string>();
					foreach (TileEntity e in TileEntity.ByID.Values) if (e is StaticCameraTE c) stray.Add(c.Position.ToString());
					Mod.Logger.Info($"[CameraWorld] camera TEs anywhere in the world: [{string.Join(", ", stray)}]");
					return;
				}
				Check("tile entity is valid for its tile (survives a reload)", te.IsTileValidForEntity(te.Position.X, te.Position.Y));

				bool allFound = true, allRight = true;
				var tile = ModContent.GetInstance<StaticCameraTile>();
				ObserveSystem.CheckMode = true;
				for (int dx = 0; dx < StaticCameraTE.TilesWide; dx++)
					for (int dy = 0; dy < StaticCameraTE.TilesHigh; dy++)
					{
						if (StaticCameraTile.TeAt(left + dx, top + dy) != te) allFound = false;
						ObserveSystem.ResetForCheck();
						if (!tile.RightClick(left + dx, top + dy) || !ObserveSystem.IsConnectedTo(te.ID)) allRight = false;
					}
				Check("all six tiles find the tile entity", allFound);
				Check("RightClick on every tile returns true and connects the view", allRight);
				Check("tile advertises smart-interact (interaction highlight)", tile.HasSmartInteract(left, top, default));

				// the real dispatch: the player's own tile-interaction code (hover, then right-click use)
				ObserveSystem.ResetForCheck();
				var player = new Player { name = "CameraCheck", active = true };
				player.position = new Vector2(left * 16f, (top + 1) * 16f);
				player.releaseUseTile = true; Player.tileTargetX = left; Player.tileTargetY = top + 1; // the cursor is on the camera
				Main.mouseRightRelease = true; Main.mouseRight = true;
				try
				{
					// hover only (no click): the cursor shows the camera item
					player.tileInteractAttempted = false;
					Player me = Main.LocalPlayer; // the tile's MouseOver writes the local player's cursor icon (the player interacting, in a real client)
					me.cursorItemIconEnabled = false; me.cursorItemIconID = 0;
					player.TileInteractionsCheck(left, top + 1);
					Check("hover shows the camera item as the cursor icon", me.cursorItemIconEnabled && me.cursorItemIconID == ModContent.ItemType<StaticCameraItem>());
					me.cursorItemIconEnabled = false; me.cursorItemIconID = 0;
					// then the right-click
					player.tileInteractAttempted = true;
					player.TileInteractionsCheck(left, top + 1);
					Check("Player.TileInteractionsCheck right-click connects the view", ObserveSystem.IsConnectedTo(te.ID));
				}
				catch (Exception e) { Check("Player.TileInteractionsCheck threw " + e.GetType().Name + ": " + e.Message, false); }
				finally { Main.mouseRight = false; }
			}
			catch (Exception e) { Check("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				ObserveSystem.CheckMode = false; ObserveSystem.ResetForCheck();
				try
				{
					var gone = new List<int>();
					foreach (TileEntity e in TileEntity.ByID.Values) if (e is StaticCameraTE c && Math.Abs(c.Position.X - x0) < 8 && Math.Abs(c.Position.Y - y0) < 12) gone.Add(c.ID);
					foreach (int id in gone) { TileEntity.ByPosition.Remove(TileEntity.ByID[id].Position); TileEntity.ByID.Remove(id); }
					foreach (Point p in touched) { Tile t = Main.tile[p.X, p.Y]; t.HasTile = false; t.TileFrameX = 0; t.TileFrameY = 0; }
				}
				catch (Exception) { }
			}
			Mod.Logger.Info($"[CameraWorld] self-check pass={pass} fail={fail}");
		}
	}
}
