using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Movement;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Items;
using Levy.Content.NPCs;

namespace Levy.Common.Observe
{
	/// <summary>
	/// "Go to map point" order. The goal is a tile kept in <see cref="SoldierRecord.Ext"/> (so it saves with the record and needs no new field);
	/// <see cref="SoldierOrder.GoTo"/> is the order. The brain marches the soldier there in legs (<c>SoldierNPC.ThinkMapOrder</c>), still fighting what is in reach, and on arrival the order turns into Hold at that spot.
	/// Issued from the Command Radio (shift-click on the full map) and from the barracks panel (pick a point, then click the map).
	/// </summary>
	public static class MapOrder
	{
		public const string KeyX = "goX", KeyY = "goY";
		/// <summary>Soldiers fan out over this many tiles either side of the clicked point so a squad does not stack on one tile.</summary>
		public const int SpreadTiles = 3;
		public const int SpreadSlots = 9;

		/// <summary>Barracks "send to map point": the next left-click on the full map is the destination.</summary>
		public static bool PickMode;
		/// <summary>Server: a soldier changed order on its own (arrival); the roster snapshot is re-sent shortly.</summary>
		public static bool SnapshotDue;
		/// <summary>Last point the local player ordered (map marker), and when.</summary>
		public static Point LastPoint; public static uint LastPointTick;

		/// <summary>Barracks button: opens the full map; the next left-click is the destination for every Regular.</summary>
		public static void BeginPick()
		{
			if (Main.dedServ) return;
			if (!Potions.AutonomySystem.Studied) { Main.NewText(LevyUtil.T("Potions.NeedAutonomy"), 255, 180, 120); return; }
			var me = Main.LocalPlayer;
			ReopenBarracks = Common.UI.UISystem.CurrentBarracks;
			// Open the map the way vanilla's own map button does (Player.TryOpeningFullscreenMap): with the inventory or a chat still open, Main.Draw calls ToggleInv()
			// the very next frame, which closes the map again (one black frame, then the panel is back).
			Main.playerInventory = false; me.SetTalkNPC(-1); me.sign = -1; Main.editSign = false; Main.npcChatCornerItem = 0;
			me.releaseInventory = false;
			Main.mapFullscreenScale = 2.5f;
			PickMode = true; Main.mapFullscreen = true; Main.resetMapFull = true;
			PickGate.Reset();
			Main.NewText(LevyUtil.T("Observe.PickPrompt"), 190, 220, 255);
		}

		/// <summary>The Barracks that opened the map pick; it is reopened when the map closes.</summary>
		public static Content.TileEntities.BarracksTE ReopenBarracks;

		public static bool TryGoal(SoldierRecord r, out int tx, out int ty)
		{
			tx = ty = 0;
			if (r?.Ext == null || !r.Ext.TryGetValue(KeyX, out tx) || !r.Ext.TryGetValue(KeyY, out ty)) return false;
			return true;
		}

		public static void SetGoal(SoldierRecord r, int tx, int ty) { r.Ext[KeyX] = tx; r.Ext[KeyY] = ty; }
		public static void ClearGoal(SoldierRecord r) { r.Ext?.Remove(KeyX); r.Ext?.Remove(KeyY); }

		/// <summary>Clamps a map click to the playable world.</summary>
		public static bool ValidTile(int tx, int ty) => tx > 10 && ty > 10 && tx < Main.maxTilesX - 10 && ty < Main.maxTilesY - 10;

		/// <summary>The x offset (tiles) the n-th ordered soldier takes from the clicked point.</summary>
		public static int SpreadOffset(int n) => ((n % SpreadSlots) - SpreadSlots / 2) * SpreadTiles / 2;

		/// <summary>Server / single player: sends every Regular of <paramref name="commander"/> to the tile. Returns how many took the order.</summary>
		public static int Issue(Player commander, int tx, int ty)
		{
			if (LevyUtil.IsClient || commander == null || !ValidTile(tx, ty) || !Potions.AutonomySystem.Studied) return 0; // map orders need the Guide to Autonomy
			var roster = RosterSystem.Instance;
			int n = 0;
			foreach (var rec in roster.RegularsOf(commander))
			{
				if (rec.Reserve || MutinySystem.IsMutinous(rec)) continue;
				int gx = Math.Clamp(tx + SpreadOffset(n), 12, Main.maxTilesX - 12);
				ApplyTo(rec, gx, ty);
				n++;
			}
			if (n > 0) roster.SendRosterSnapshot();
			return n;
		}

		/// <summary>Gives one record the order (and its live body, if any).</summary>
		public static void ApplyTo(SoldierRecord rec, int tx, int ty)
		{
			SetGoal(rec, tx, ty);
			rec.Order = SoldierOrder.GoTo; rec.StoodDown = false;
			if (rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is SoldierNPC body) body.ReceiveOrder(SoldierOrder.GoTo);
		}

		/// <summary>Client / single player entry: the local player orders their levies to a tile.</summary>
		public static void Send(int tx, int ty)
		{
			if (!ValidTile(tx, ty)) return;
			if (!Potions.AutonomySystem.Studied) { Main.NewText(LevyUtil.T("Potions.NeedAutonomy"), 255, 180, 120); MapOrder.PickMode = false; return; }
			LastPoint = new Point(tx, ty); LastPointTick = Main.GameUpdateCount;
			if (Main.netMode == NetmodeID.SinglePlayer)
			{
				int n = Issue(Main.LocalPlayer, tx, ty);
				Main.NewText(n > 0 ? LevyUtil.T("Observe.MapSent", n, LevyUtil.T("Observe.Place" + PlaceOf(ty))) : LevyUtil.T("Observe.MapNone"), 190, 220, 255);
			}
			else
			{
				var p = LevyPackets.New(LevyPacket.MapOrder); p.Write(tx); p.Write(ty); p.Send();
			}
		}

		/// <summary>Which depth band a map tile is in (the suffix of the Observe.Place* strings), for the confirmation line.</summary>
		public static string PlaceOf(int ty)
		{
			if (ty < Main.worldSurface * 0.35) return "Space";
			if (ty < Main.worldSurface * 0.8) return "Sky";
			if (ty < Main.worldSurface) return "Surface";
			if (ty < Main.rockLayer) return "Underground";
			if (ty < Main.maxTilesY - 200) return "Caverns";
			return "Underworld";
		}

		/// <summary>
		/// Whether this frame's mouse state is a pick click on the full map. Vanilla's DrawMap sets <c>Player.mouseInterface = true</c> for the whole of the full-screen map
		/// BEFORE the map layers draw, so <c>mouseInterface</c> can never be the "a UI element is under the cursor" test here: with it, no click ever picked (Eve's bug).
		/// The only vanilla UI on the full map is the close/style button at the bottom-left, which is excluded by rectangle.
		/// </summary>
		public static bool IsPickClick(bool click, int mouseX, int mouseY, int screenHeight)
			=> click && !OverMapCloseButton(mouseX, mouseY, screenHeight);

		/// <summary>The vanilla "close map" button (32x30 at (10, screenHeight - 40), a little more with the FPS counter showing).</summary>
		public static bool OverMapCloseButton(int mouseX, int mouseY, int screenHeight)
			=> mouseX >= 10 && mouseX <= 42 && mouseY >= screenHeight - 60 && mouseY <= screenHeight - 8;

		public static void Register() => LevyPackets.Register(LevyPacket.MapOrder, OnPacket);

		private static void OnPacket(BinaryReader r, int whoAmI)
		{
			int tx = r.ReadInt32(), ty = r.ReadInt32();
			if (whoAmI < 0 || whoAmI >= Main.maxPlayers || !Main.player[whoAmI].active) return;
			Issue(Main.player[whoAmI], tx, ty);
		}

		/// <summary>The standable tile the soldiers actually aim for: the nearest floor to the clicked tile (a click in the air or inside rock still works).</summary>
		public static Point ResolveStand(int tx, int ty)
		{
			if (TileNav.NearestStandable(tx, ty, 25, out Point p)) return p;
			return new Point(tx, ty);
		}

		public const int LegTiles = 30;

		/// <summary>
		/// The goal a marching soldier is given this think: the final stand when it is within one leg, else a standable tile about <see cref="LegTiles"/> tiles on toward it (the nearest floor in that column within 30 rows of his feet;
		/// no floor found = a point at his own height, and the movement code's gap, wall and hook handling does the rest).
		/// </summary>
		public static Vector2 LegGoal(Vector2 center, Vector2 bottom, Vector2 finalGoal)
		{
			float dx = finalGoal.X - center.X, legPx = LegTiles * 16f;
			if (Math.Abs(dx) <= legPx) return finalGoal;
			float lx = center.X + Math.Sign(dx) * legPx;
			int cx = (int)(lx / 16f), fy = TileNav.FeetTile(bottom).Y;
			for (int d = 0; d <= 30; d++)
				for (int sgn = -1; sgn <= 1; sgn += 2)
				{
					int y = fy + sgn * d;
					if (TileNav.InBounds(cx, y) && TileNav.IsStandable(cx, y)) return TileNav.BottomCentre(cx, y);
				}
			return new Vector2(lx, bottom.Y);
		}

		/// <summary>The map tile under the mouse for a map draw context (full-screen / overlay map).</summary>
		public static Vector2 MouseTile(in MapOverlayDrawContext c) => (Main.MouseScreen - c.MapOffset) / Math.Max(0.0001f, c.MapScale) + c.MapPosition;
	}

	/// <summary>The map side of the radio and the barracks command: previews the destination and takes the click.</summary>
	public class RadioMapLayer : ModMapLayer
	{
		private bool _wasLeft;
		public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (Main.dedServ || !Main.mapFullscreen) { _wasLeft = Main.mouseLeft; return; }
			var me = Main.LocalPlayer;
			bool radio = CommandRadio.Has(me), pick = MapOrder.PickMode;
			bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
			bool armed = (radio && shift) || pick;
			bool click = PickGate.Click(Main.mouseLeft, _wasLeft);
			_wasLeft = Main.mouseLeft;

			// the last ordered point stays on the map for a while so the player can watch the levies go
			if (Main.GameUpdateCount - MapOrder.LastPointTick < 60u * 30u)
				Mark(ref context, MapOrder.LastPoint.X, MapOrder.LastPoint.Y, new Color(120, 255, 160));

			if (pick && Main.keyState.IsKeyDown(Keys.Escape)) { MapOrder.PickMode = false; return; }
			if (!armed) return;
			Vector2 t = MapOrder.MouseTile(in context);
			int tx = (int)t.X, ty = (int)t.Y;
			if (!MapOrder.ValidTile(tx, ty)) return;
			Mark(ref context, tx, ty, new Color(255, 230, 90));
			text = LevyUtil.T("Observe.MapSendHint");
			if (MapOrder.IsPickClick(click, Main.mouseX, Main.mouseY, Main.screenHeight))
			{
				bool wasPick = MapOrder.PickMode;
				MapOrder.Send(tx, ty);
				MapOrder.PickMode = false;
				if (wasPick) { Main.mapFullscreen = false; Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuClose); } // the barracks pick is one click: back to the world (MapPickSystem reopens the panel)
			}
		}

		private static void Mark(ref MapOverlayDrawContext c, int tx, int ty, Color col)
		{
			var tex = TextureAssets.MagicPixel.Value;
			float s = Math.Max(5f, 7f * c.DrawScale);
			c.Draw(tex, new Vector2(tx, ty), col, new SpriteFrame(1, 1), s, s, Alignment.Center);
		}
	}

	/// <summary>A pick click counts only after the button that opened the map has been released (the opening click is still held on the first map frame).</summary>
	public static class PickGate
	{
		private static bool _released;
		public static void Reset() => _released = false;
		/// <summary>True on a fresh press, once the mouse has been seen up since <see cref="Reset"/>.</summary>
		public static bool Click(bool left, bool wasLeft)
		{
			if (!left) _released = true;
			return left && !wasLeft && _released;
		}
		/// <summary>Boot check: the opening press never picks; the first press after a release does.</summary>
		public static bool SelfCheck()
		{
			Reset();
			bool a = Click(true, false);   // opening click still down
			bool b = Click(true, true);    // held
			Click(false, true);            // released
			bool c = Click(true, false);   // new press
			Reset();
			return !a && !b && c;
		}
	}

	/// <summary>Reopens the Barracks panel after a map pick, and drops a stale pick when the map was closed some other way.</summary>
	public class MapPickSystem : ModSystem
	{
		private uint _since;
		public override void PostSetupContent()
		{
			bool clickOk = MapOrder.IsPickClick(true, 600, 400, 1080) && !MapOrder.IsPickClick(false, 600, 400, 1080) && !MapOrder.IsPickClick(true, 20, 1050, 1080);
			if (PickGate.SelfCheck() && clickOk) Mod.Logger.Info("[MapPick] self-check ok"); else Mod.Logger.Error("[MapPick] self-check FAILED (the opening click would pick)");
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ) return;
			if (Main.mapFullscreen) { _since = Main.GameUpdateCount; return; }
			if (!MapOrder.PickMode && MapOrder.ReopenBarracks == null) return;
			if (Main.GameUpdateCount - _since < 2u) return; // the map opens on the same tick the button is pressed
			MapOrder.PickMode = false;
			var te = MapOrder.ReopenBarracks; MapOrder.ReopenBarracks = null;
			if (te != null && Common.UI.UISystem.Instance != null && Common.UI.UIActions.IsLive(te)) Common.UI.UISystem.Instance.OpenBarracks(te);
		}
	}
}
