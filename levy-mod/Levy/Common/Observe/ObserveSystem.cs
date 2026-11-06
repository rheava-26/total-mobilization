using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Items.Observe;
using Levy.Content.NPCs;

namespace Levy.Common.Observe
{
	public enum ViewKind : byte { None, Camera, Levy, Mind }

	/// <summary>
	/// The viewing side of Command and Observe (client / single player): connect to a static camera or a GoPro levy, or follow the levy you are mind-controlling.
	/// The camera (<c>Main.screenPosition</c>) moves to the target while your character stays put; lighting, tiles and NPCs follow because the game derives every draw area from the screen position.
	/// While a view is open your character does not move or use items (except the GoPro / Mind Link); Esc or the item ends it; Left / Right switch target; the mouse wheel zooms a camera out (see ArenaView).
	/// </summary>
	public class ObserveSystem : ModSystem
	{
		public class CamInfo { public int Id; public Point16 Tile; public string Name; }

		public static ViewKind Kind { get; private set; }
		public static int TargetId { get; private set; } = -1;
		public static List<CamInfo> Cams = new();
		public static float Zoom = 2f;

		private static Vector2 _remotePos, _lastCenter; private static uint _remoteTick = uint.MaxValue; private static bool _haveLast;
		private static int _bodyCache = -1; private static bool _closeMenu; private static uint _nextAuto; private static bool _optOut;


		public override void Load()
		{
			Net_Register();
			ArenaView.Load();
		}

		public override void Unload() { ArenaView.Unload(); Kind = ViewKind.None; Cams = new(); }
		public override void OnWorldUnload() { Kind = ViewKind.None; TargetId = -1; ArenaView.Leave(); MindControl.Reset(); ObserveNet.Reset(); }

		private static void Net_Register() { MapOrder.Register(); ObserveNet.Register(); MindControl.Register(); }

		// =====================================================================================================
		// connecting
		// =====================================================================================================

		public static float MaxZoom => ArenaView.ClampFactor(ObserveConfig.Get?.ArenaZoomPerSide ?? 2f);

		public static void ConnectCamera(int id)
		{
			if (Main.dedServ) return;
			if (MindControl.LocalSoldierId >= 0) MindControl.RequestEnd();
			Kind = ViewKind.Camera; TargetId = id; Zoom = MaxZoom; _haveLast = false; _remoteTick = uint.MaxValue;
			ObserveNet.SendRequest(1, id);
			ObserveNet.SendRequest(3, 0);
			Main.NewText(LevyUtil.T("Observe.Connected", CameraName(id)), 160, 220, 255);
		}

		/// <summary>Watch a GoPro levy: the next one after the current target (or the first).</summary>
		public static void ConnectLevy(bool next)
		{
			if (Main.dedServ) return;
			var ids = GoProLevies(Main.LocalPlayer);
			if (ids.Count == 0) { Main.NewText(LevyUtil.T("Observe.NoGoPro"), 255, 200, 120); return; }
			int pick = ids[0];
			if (next && Kind == ViewKind.Levy) foreach (int id in ids) if (id > TargetId) { pick = id; goto found; }
			found:
			if (MindControl.LocalSoldierId >= 0) MindControl.RequestEnd();
			Kind = ViewKind.Levy; TargetId = pick; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1;
			ArenaView.Factor = 1f;
			ObserveNet.SendRequest(2, pick);
			Main.NewText(LevyUtil.T("Observe.Watching", NameOf(pick)), 160, 220, 255);
		}

		public static void Exit()
		{
			if (Kind == ViewKind.Mind) { _optOut = Main.LocalPlayer.dead; MindControl.RequestEnd(); return; } // released while dead: stay released until the respawn
			if (Kind == ViewKind.None) return;
			Kind = ViewKind.None; TargetId = -1;
			ArenaView.Leave();
			ObserveNet.SendRequest(0, 0);
		}

		public static void OnMindBegin(int soldierId)
		{
			if (Kind == ViewKind.Camera || Kind == ViewKind.Levy) { ObserveNet.SendRequest(0, 0); }
			Kind = ViewKind.Mind; TargetId = soldierId; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1;
			ArenaView.Leave();
		}

		public static void OnMindEnd()
		{
			MindControl.LocalSoldierId = -1;
			if (Kind != ViewKind.Mind) return;
			Kind = ViewKind.None; TargetId = -1; ArenaView.Leave();
		}

		public static void OnServerPos(byte kind, int id, Vector2 pos)
		{
			_remotePos = pos; _remoteTick = Main.GameUpdateCount;
			if (kind == 3 && Kind == ViewKind.Mind) { TargetId = id; MindControl.LocalSoldierId = id; } // the server moved the link to the next levy
		}

		// =====================================================================================================
		// targets
		// =====================================================================================================

		/// <summary>Soldier ids (of the local player's Regulars) that carry a GoPro and are alive, ascending.</summary>
		public static List<int> GoProLevies(Player p)
		{
			var list = new List<int>();
			var roster = RosterSystem.Instance; if (roster == null) return list;
			foreach (var r in roster.RegularsOf(p)) if (!r.Reserve && !r.Dead && GoProItem.Carries(r.Gear)) list.Add(r.Id);
			list.Sort();
			return list;
		}

		public static string NameOf(int soldierId) => RosterSystem.Instance?.FindById(soldierId)?.Name ?? "?";

		public static string CameraName(int id)
		{
			var te = StaticCameraTE.Get(id);
			if (te != null) return te.Display;
			foreach (var c in Cams) if (c.Id == id) return string.IsNullOrEmpty(c.Name) ? LevyUtil.T("Observe.CameraName", id) : c.Name;
			return LevyUtil.T("Observe.CameraName", id);
		}

		/// <summary>The local body of a soldier (clients find it by the record id each NPC carries).</summary>
		private static SoldierNPC FindBody(int soldierId)
		{
			if (_bodyCache >= 0 && _bodyCache < Main.maxNPCs)
			{
				var n = Main.npc[_bodyCache];
				if (n.active && n.ModNPC is SoldierNPC c && c.Record != null && c.Record.Id == soldierId) return c;
			}
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC s && s.Record != null && s.Record.Id == soldierId) { _bodyCache = i; return s; }
			}
			return null;
		}

		private static bool TryCenter(out Vector2 c)
		{
			c = _lastCenter;
			bool remoteFresh = _remoteTick != uint.MaxValue && Main.GameUpdateCount - _remoteTick < 120u;
			switch (Kind)
			{
				case ViewKind.Camera:
				{
					var te = StaticCameraTE.Get(TargetId);
					if (te != null) { c = te.ViewCenter; break; }
					foreach (var ci in Cams) if (ci.Id == TargetId) { c = new Vector2(ci.Tile.X * 16f + 16f, ci.Tile.Y * 16f + 14f); goto have; }
					if (remoteFresh) { c = _remotePos; break; }
					return _haveLast;
				}
				case ViewKind.Levy:
				case ViewKind.Mind:
				{
					var body = FindBody(TargetId);
					if (body != null && body.IsAlive) { c = body.NPC.Center; break; }
					if (remoteFresh) { c = _remotePos; break; }
					return _haveLast;
				}
				default: return false;
			}
			have:
			_lastCenter = c; _haveLast = true;
			return true;
		}

		private static string Title()
		{
			switch (Kind)
			{
				case ViewKind.Camera: return LevyUtil.T("Observe.HudCamera", CameraName(TargetId), Zoom.ToString("0.##"));
				case ViewKind.Levy: return LevyUtil.T("Observe.HudGoPro", NameOf(TargetId));
				case ViewKind.Mind: return LevyUtil.T("Observe.HudMind", NameOf(TargetId));
			}
			return "";
		}

		private static void Cycle(int dir)
		{
			if (Kind == ViewKind.Camera)
			{
				var ids = new List<int>(); foreach (var te in StaticCameraTE.All()) ids.Add(te.ID);
				foreach (var c in Cams) if (!ids.Contains(c.Id)) ids.Add(c.Id);
				if (ids.Count == 0) return;
				ids.Sort(); int i = ids.IndexOf(TargetId); i = ((i < 0 ? 0 : i + dir) % ids.Count + ids.Count) % ids.Count;
				int z = ids[i]; Zoom = MaxZoom; TargetId = z; _haveLast = false; _remoteTick = uint.MaxValue;
				ObserveNet.SendRequest(1, z);
			}
			else if (Kind == ViewKind.Levy)
			{
				var ids = GoProLevies(Main.LocalPlayer); if (ids.Count == 0) return;
				int i = ids.IndexOf(TargetId); i = ((i < 0 ? 0 : i + dir) % ids.Count + ids.Count) % ids.Count;
				TargetId = ids[i]; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1;
				ObserveNet.SendRequest(2, TargetId);
			}
			else if (Kind == ViewKind.Mind)
			{
				int id = MindControl.PickNext(Main.LocalPlayer, dir > 0 ? TargetId : -1, TargetId);
				if (dir < 0) { var all = new List<int>(); foreach (var r in RosterSystem.Instance.RegularsOf(Main.LocalPlayer)) if (!r.Reserve && !r.Dead) all.Add(r.Id); all.Sort(); int k = all.IndexOf(TargetId); if (all.Count > 0) id = all[((k < 0 ? 0 : k - 1) % all.Count + all.Count) % all.Count]; }
				if (id >= 0 && id != TargetId) MindControl.RequestBegin(id, MindControl.LocalViaDeath);
			}
		}

		// =====================================================================================================
		// frame hooks
		// =====================================================================================================

		public override void ModifyScreenPosition()
		{
			if (Main.dedServ) return;
			if (Kind == ViewKind.None) { if (ArenaView.Active || ArenaView.Inflated) ArenaView.Leave(); return; }
			if (!TryCenter(out Vector2 c))
			{
				if (Main.GameUpdateCount > 0 && Kind != ViewKind.Mind) { Main.NewText(LevyUtil.T("Observe.Lost"), 255, 200, 120); Exit(); }
				return;
			}
			ArenaView.Factor = Kind == ViewKind.Camera ? ArenaView.ClampFactor(Math.Min(Zoom, MaxZoom)) : 1f;
			ArenaView.Frame(c, ArenaView.Factor);
		}

		public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
		{
			if (!Main.dedServ) ArenaView.Transform(Transform);
		}

		public override void PostUpdateInput()
		{
			if (Main.dedServ || Main.gameMenu) return;
			var me = Main.LocalPlayer;
			if (!me.dead) _optOut = false;
			// dying hands you a levy
			if (me.dead && Kind != ViewKind.Mind && ObserveConfig.Get != null && ObserveConfig.Get.MindControlOnDeath && MindControl.LocalSoldierId < 0 && !_optOut && Main.GameUpdateCount >= _nextAuto)
			{
				_nextAuto = Main.GameUpdateCount + 60u; // a refusal is not retried every frame
				int id = MindControl.PickNext(me, -1, -1, me.Center);
				if (id >= 0) MindControl.RequestBegin(id, true);
			}
			if (Kind == ViewKind.Mind && MindControl.LocalViaDeath && !me.dead) { MindControl.RequestEnd(); }
			if (Kind == ViewKind.Mind && Main.netMode == NetmodeID.SinglePlayer && MindControl.LinkOfPlayer(Main.myPlayer) == null) OnMindEnd(); // the server side ended it
			if (Kind == ViewKind.None) return;
			if (Kind != ViewKind.Mind && me.dead) { Exit(); return; }

			bool typing = Main.drawingPlayerChat || Main.editSign || Main.editChest;
			bool esc = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
			bool back = Main.keyState.IsKeyDown(Keys.Back) && !Main.oldKeyState.IsKeyDown(Keys.Back) && !typing;
			if (esc || back) { if (esc) _closeMenu = true; Exit(); return; }
			if (!typing)
			{
				if (Main.keyState.IsKeyDown(Keys.Right) && !Main.oldKeyState.IsKeyDown(Keys.Right)) Cycle(1);
				if (Main.keyState.IsKeyDown(Keys.Left) && !Main.oldKeyState.IsKeyDown(Keys.Left)) Cycle(-1);
			}
			if (Kind == ViewKind.Camera && PlayerInput.ScrollWheelDelta != 0)
			{
				Zoom = MathHelper.Clamp(Zoom + Math.Sign(PlayerInput.ScrollWheelDelta) * 0.25f, 1f, MaxZoom);
				PlayerInput.ScrollWheelDelta = 0;
			}
			if (Kind == ViewKind.Mind && !typing && !Main.mapFullscreen) MindControl.PushLocalInput(MindControl.Sample());
			if (Main.netMode == NetmodeID.MultiplayerClient && Kind == ViewKind.Camera && Main.GameUpdateCount % 300u == 0u) ObserveNet.SendRequest(3, 0);
			if (Kind == ViewKind.Mind && Main.netMode == NetmodeID.MultiplayerClient && _remoteTick != uint.MaxValue && Main.GameUpdateCount - _remoteTick > 180u) OnMindEnd(); // the server stopped streaming: the link is gone
		}

		public override void PostUpdateEverything()
		{
			if (_closeMenu) { _closeMenu = false; try { if (Main.ingameOptionsWindow) IngameOptions.Close(); } catch (Exception) { } }
			if (!LevyUtil.IsClient)
			{
				MindControl.ServerTick();
				ObserveNet.ServerTick();
				if (MapOrder.SnapshotDue && Main.GameUpdateCount % 30u == 0u) { MapOrder.SnapshotDue = false; RosterSystem.Instance?.SendRosterSnapshot(); }
			}
		}

		public override void PreUpdateWorld() => ArenaView.RestoreSize();

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (Main.dedServ) return;
			layers.Insert(0, new LegacyGameInterfaceLayer("Levy: Observe Restore", () => { ArenaView.RestoreSize(); return true; }, InterfaceScaleType.None));
			int i = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(i < 0 ? layers.Count : i, new LegacyGameInterfaceLayer("Levy: Observe HUD", DrawHud, InterfaceScaleType.UI));
		}

		private static bool DrawHud()
		{
			var sb = Main.spriteBatch;
			if (Kind != ViewKind.None)
			{
				string title = Title(), keys = LevyUtil.T(Kind == ViewKind.Mind ? "Observe.KeysMind" : Kind == ViewKind.Camera ? "Observe.KeysCamera" : "Observe.KeysGoPro");
				float w = Main.screenWidth / Main.UIScale;
				Vector2 sz = FontAssets.MouseText.Value.MeasureString(title);
				Utils.DrawBorderString(sb, title, new Vector2((w - sz.X) / 2f, 24f), Kind == ViewKind.Mind ? new Color(190, 150, 255) : new Color(255, 120, 110), 1f);
				Vector2 sz2 = FontAssets.MouseText.Value.MeasureString(keys) * 0.8f;
				Utils.DrawBorderString(sb, keys, new Vector2((w - sz2.X) / 2f, 52f), Color.White, 0.8f);
			}
			DrawGoProMarks(sb);
			return true;
		}

		/// <summary>The GoPro indicator over every levy that carries one (not while the arena zoom is on, where screen positions differ).</summary>
		private static void DrawGoProMarks(SpriteBatch sb)
		{
			if (ArenaView.Active || Main.mapFullscreen) return;
			var roster = RosterSystem.Instance; if (roster == null) return;
			bool any = false;
			for (int i = 0; i < Main.maxNPCs && !any; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.Record != null && GoProItem.Carries(s.Record.Gear)) any = true; }
			if (!any) return;
			var tex = ModContent.Request<Texture2D>("Levy/Content/Items/Observe/GoProIndicator").Value;
			var z = Main.GameViewMatrix.Zoom;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !GoProItem.Carries(s.Record.Gear)) continue;
				Vector2 p = (n.Top + new Vector2(0f, -22f) - Main.screenPosition) * z / Main.UIScale; // world to UI space (the scale layers use the UI matrix)
				if (p.X < -20 || p.Y < -20 || p.X > Main.screenWidth / Main.UIScale + 20 || p.Y > Main.screenHeight / Main.UIScale + 20) continue;
				bool watched = (Kind == ViewKind.Levy || Kind == ViewKind.Mind) && s.Record.Id == TargetId;
				sb.Draw(tex, p, null, watched ? Color.White : Color.White * 0.75f, 0f, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
			}
		}
	}

	/// <summary>While a view is open the character stays where it is: no movement, no item use except the GoPro and Mind Link.</summary>
	public class ObservePlayer : ModPlayer
	{
		public override void SetControls()
		{
			if (Player.whoAmI != Main.myPlayer || ObserveSystem.Kind == ViewKind.None) return;
			Player.controlLeft = Player.controlRight = Player.controlJump = Player.controlDown = Player.controlUp = false;
			Player.controlHook = Player.controlMount = Player.controlThrow = false;
			if (ObserveSystem.Kind == ViewKind.Mind) { Player.controlUseItem = Player.controlUseTile = false; }
		}

		public override bool CanUseItem(Item item)
		{
			if (Player.whoAmI != Main.myPlayer || ObserveSystem.Kind == ViewKind.None) return true;
			return item.ModItem is GoProItem || item.ModItem is MindLinkItem;
		}
	}
}
