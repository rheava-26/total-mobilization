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
		public static void BeginPick() => BeginPick(true);

		/// <summary>Opens the map for a pick. <paramref name="fromBarracks"/> true reopens the open Barracks panel afterwards; the radial's button passes false.</summary>
		public static void BeginPick(bool fromBarracks)
		{
			if (Main.dedServ) return;
			if (!Potions.AutonomySystem.Studied) { Main.NewText(LevyUtil.T("Potions.NeedAutonomy"), 255, 180, 120); return; }
			var me = Main.LocalPlayer;
			ReopenBarracks = fromBarracks ? Common.UI.UISystem.CurrentBarracks : null;
			// Open the map the way vanilla's own map button does (Player.TryOpeningFullscreenMap): with the inventory or a chat still open, Main.Draw calls ToggleInv()
			// the very next frame, which closes the map again (one black frame, then the panel is back).
			Main.playerInventory = false; me.SetTalkNPC(-1); me.sign = -1; Main.editSign = false; Main.npcChatCornerItem = 0;
			me.releaseInventory = false;
			Main.mapFullscreenScale = 2.5f;
			PickMode = true; Main.mapFullscreen = true; Main.resetMapFull = true;
			PickGate.Reset(); MapClicks.Reset();
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
			int n = 0, refused = 0;
			foreach (var rec in roster.RegularsOf(commander))
			{
				if (rec.Reserve) continue;
				if (MutinySystem.IsMutinous(rec)) { refused++; if (rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is SoldierNPC mb) mb.MutinyRefuse(); continue; } // refusing is loud, never silent
				int gx = Math.Clamp(tx + SpreadOffset(n), 12, Main.maxTilesX - 12);
				ApplyTo(rec, gx, ty);
				n++;
			}
			if (refused > 0) RosterSystem.SendToPlayer(commander, LevyUtil.T("Mutiny.Refused", refused), LevyConstants.Mutiny.Alarm);
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

		private static uint _unreachableTick; private static int _unreachableCount;
		/// <summary>A soldier followed its route to the closest point it could reach and stopped: tell the commander (one line per few seconds, with the count).</summary>
		public static void NoteUnreachable(SoldierRecord rec, Vector2 at)
		{
			if (rec == null || LevyUtil.IsClient) return;
			_unreachableCount++;
			if (Main.GameUpdateCount - _unreachableTick < 60u * 6u) return;
			_unreachableTick = Main.GameUpdateCount;
			int ci = LevyUtil.FindPlayerByName(rec.CommanderName);
			var commander = ci >= 0 ? Main.player[ci] : null;
			try { if (commander != null) RosterSystem.SendToPlayer(commander, LevyUtil.T("Observe.MapUnreachable", _unreachableCount), new Color(255, 200, 120)); }
			catch (System.Exception) { } // a chat line is a nicety: no client socket (a headless test) must never cost a soldier its think
			_unreachableCount = 0;
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
			if (Main.dedServ || !Main.mapFullscreen) { _wasLeft = Main.mouseLeft; MapClicks.Reset(); return; }
			var me = Main.LocalPlayer;
			bool radio = CommandRadio.Has(me), pick = MapOrder.PickMode;
			bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
			bool armed = (radio && shift) || pick;
			_wasLeft = Main.mouseLeft;

			// the last ordered point stays on the map for a while so the player can watch the levies go
			if (Main.GameUpdateCount - MapOrder.LastPointTick < 60u * 30u)
				Mark(ref context, MapOrder.LastPoint.X, MapOrder.LastPoint.Y, new Color(120, 255, 160));

			if (pick && Main.keyState.IsKeyDown(Keys.Escape)) { MapOrder.PickMode = false; MapClicks.Reset(); return; }
			if (!armed) { MapClicks.Reset(); return; }
			Vector2 t = MapOrder.MouseTile(in context);
			int tx = (int)t.X, ty = (int)t.Y;
			bool overUi = MapOrder.OverMapCloseButton(Main.mouseX, Main.mouseY, Main.screenHeight);
			var step = MapClicks.Step(Main.mouseLeft, Main.MouseScreen, tx, ty, context.MapScale, Main.GameUpdateCount, overUi);
			if (MapClicks.Pending) Mark(ref context, MapClicks.PendingX, MapClicks.PendingY, new Color(255, 140, 60));
			if (!MapOrder.ValidTile(tx, ty)) return;
			Mark(ref context, tx, ty, new Color(255, 230, 90));
			text = LevyUtil.T(MapClicks.Pending ? "Observe.MapConfirmHint" : "Observe.MapSendHint");
			if (step == MapClicks.Result.Confirmed)
			{
				bool wasPick = MapOrder.PickMode;
				MapOrder.Send(MapClicks.SentX, MapClicks.SentY);
				MapOrder.PickMode = false;
				if (wasPick) { Main.mapFullscreen = false; Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuClose); } // the barracks pick is one click: back to the world (MapPickSystem reopens the panel)
			}
			else if (step == MapClicks.Result.Placed) Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private static void Mark(ref MapOverlayDrawContext c, int tx, int ty, Color col)
		{
			var tex = TextureAssets.MagicPixel.Value;
			float s = Math.Max(5f, 7f * c.DrawScale);
			c.Draw(tex, new Vector2(tx, ty), col, new SpriteFrame(1, 1), s, s, Alignment.Center);
		}
	}

	/// <summary>
	/// Map-order clicking. A click is a press and a release that stay within <see cref="DragPx"/> pixels, so dragging the map never counts. The first click puts a pending marker on the map;
	/// a second click on the same spot (within <see cref="SlopPx"/> pixels, inside <see cref="WindowTicks"/>) confirms and sends. A click anywhere else moves the marker. Pure state, driven once a frame.
	/// </summary>
	public static class MapClicks
	{
		public enum Result : byte { None, Placed, Confirmed }
		public const float DragPx = 6f, SlopPx = 16f;
		public const uint WindowTicks = 60u * 12u;
		public static bool Pending; public static int PendingX, PendingY; public static int SentX, SentY;
		private static uint _pendingTick; private static bool _wasLeft, _down, _gate; private static Vector2 _downAt;

		public static void Reset() { Pending = false; _down = false; _gate = false; _wasLeft = !Main.dedServ && Main.mouseLeft; }

		/// <summary>One frame. <paramref name="left"/> is the mouse button, <paramref name="mouse"/> its screen position, (tx, ty) the map tile under it, <paramref name="mapScale"/> the map's pixels per tile.</summary>
		public static Result Step(bool left, Vector2 mouse, int tx, int ty, float mapScale, uint now, bool overUi)
		{
			var res = Result.None;
			if (Pending && now - _pendingTick > WindowTicks) Pending = false;
			if (!left) _gate = true; // the press that opened the map is not a click
			if (left && !_wasLeft && _gate && !overUi) { _down = true; _downAt = mouse; }
			else if (left && _down && Vector2.Distance(mouse, _downAt) > DragPx) _down = false; // the map is being dragged
			else if (!left && _wasLeft && _down)
			{
				_down = false;
				if (Pending && Vector2.Distance(new Vector2(tx, ty) * mapScale, new Vector2(PendingX, PendingY) * mapScale) <= SlopPx)
				{
					SentX = PendingX; SentY = PendingY; Pending = false; res = Result.Confirmed;
				}
				else { Pending = true; PendingX = tx; PendingY = ty; _pendingTick = now; res = Result.Placed; }
			}
			_wasLeft = left;
			return res;
		}

		/// <summary>Boot check: the opening press never places, a drag never places, a click places, a second click on the spot confirms, a click elsewhere moves the marker, an old marker expires.</summary>
		public static bool SelfCheck(out string why)
		{
			why = "";
			bool was = _wasLeft, wp = Pending, wd = _down, wg = _gate; int px = PendingX, py = PendingY;
			try
			{
				Pending = false; _down = false; _gate = false; _wasLeft = true; // the opening press is still down
				uint t = 1000;
				Result Click(int x, int y, uint at) { Step(true, new Vector2(x * 2, y * 2), x, y, 2f, at, false); return Step(false, new Vector2(x * 2, y * 2), x, y, 2f, at + 2, false); }
				var r0 = Step(false, Vector2.Zero, 0, 0, 2f, t, false);
				if (r0 != Result.None || Pending) { why = "the press that opened the map placed a point"; return false; }
				Step(true, new Vector2(100, 100), 50, 50, 2f, t + 5, false); Step(true, new Vector2(160, 100), 80, 50, 2f, t + 6, false);
				var r1 = Step(false, new Vector2(160, 100), 80, 50, 2f, t + 7, false);
				if (r1 != Result.None || Pending) { why = "a drag placed a point"; return false; }
				var r2 = Click(300, 300, t + 20); if (r2 != Result.Placed || !Pending) { why = "a click did not place a point"; return false; }
				var r3 = Click(400, 400, t + 40); if (r3 != Result.Placed || PendingX != 400) { why = "a click elsewhere did not move the point"; return false; }
				var r4 = Click(401, 400, t + 60); if (r4 != Result.Confirmed || SentX != 400 || Pending) { why = "the second click did not confirm"; return false; }
				Click(10, 10, t + 80); var r5 = Click(10, 10, t + WindowTicks + 200); if (r5 != Result.Placed) { why = "an old point did not expire"; return false; }
				return true;
			}
			finally { _wasLeft = was; Pending = wp; _down = wd; _gate = wg; PendingX = px; PendingY = py; }
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
			string why = ""; if (PickGate.SelfCheck() && clickOk && MapClicks.SelfCheck(out why)) Mod.Logger.Info("[MapPick] self-check ok (a click places, a second click on it sends, a drag never does)"); else Mod.Logger.Error("[MapPick] self-check FAILED " + why);
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
