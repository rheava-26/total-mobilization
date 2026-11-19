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
		/// <summary>While watching, the whole interface is hidden (the camera viewer's toggle). Esc or the same toggle brings it back; it never outlives the view.</summary>
		public static bool HideHud { get; private set; }
		private static uint _hudHiddenAt;
		public static void SetHideHud(bool on) { HideHud = on && Kind != ViewKind.None; if (HideHud) _hudHiddenAt = Main.GameUpdateCount; }

		/// <summary>The world point the view is centred on right now (camera, GoPro levy or mind-linked levy), once known.</summary>
		public static bool TryViewCenter(out Vector2 c) { c = _lastCenter; return Kind != ViewKind.None && _haveLast; }

		private static Vector2 _remotePos, _lastCenter; private static uint _remoteTick = uint.MaxValue; private static bool _haveLast;
		/// <summary>How long a view may go without a position for its target before the signal counts as lost (a multiplayer client waits this long for the server's first position).</summary>
		public const uint LostGraceTicks = 120;
		private static uint _connectTick, _lastSeenTick;

		/// <summary>True when a view connected at <paramref name="connectTick"/> whose target was last seen at <paramref name="lastSeenTick"/> (0 = never) has had no position for longer than <see cref="LostGraceTicks"/>.</summary>
		internal static bool SignalLost(uint now, uint connectTick, uint lastSeenTick) => now - Math.Max(connectTick, lastSeenTick) > LostGraceTicks;

		/// <summary>A new target: the lost-signal clock starts again.</summary>
		private static void Connected() { _connectTick = Main.GameUpdateCount; _lastSeenTick = 0; }
		private static int _bodyCache = -1; private static bool _closeMenu; private static uint _nextAuto; private static bool _optOut, _deathPicked;


		public override void Load()
		{
			Net_Register();
			ArenaView.Load();
		}

		public override void Unload() { ArenaView.Unload(); Kind = ViewKind.None; Cams = new(); HideHud = false; CameraViewPanel.Unload(); }
		public override void OnWorldUnload() { Kind = ViewKind.None; TargetId = -1; HideHud = false; ArenaView.Leave(); MindControl.Reset(); ObserveNet.Reset(); if (!Main.dedServ) CameraViewPanel.Close(); }

		private static void Net_Register() { MapOrder.Register(); ObserveNet.Register(); MindControl.Register(); }

		// =====================================================================================================
		// connecting
		// =====================================================================================================

		public static float MaxZoom => ArenaView.ClampFactor(ObserveConfig.Get?.ArenaZoomPerSide ?? 2f);

		/// <summary>Set only by the world self-check ([CameraWorld]) so a dedicated server can run the connect path it would otherwise skip; never set in play.</summary>
		internal static bool CheckMode;
		internal static bool IsConnectedTo(int cameraId) => Kind == ViewKind.Camera && TargetId == cameraId;
		internal static void ResetForCheck() { Kind = ViewKind.None; TargetId = -1; }

		public static void ConnectCamera(int id)
		{
			if (Main.dedServ && !CheckMode) return;
			if (MindControl.LocalSoldierId >= 0) MindControl.RequestEnd();
			Kind = ViewKind.Camera; TargetId = id; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; Connected(); // opens at normal size; the wheel zooms out (ArenaView)
			ObserveNet.SendRequest(1, id);
			ObserveNet.SendRequest(3, 0);
			if (!Main.dedServ) Main.NewText(LevyUtil.T("Observe.Connected", CameraName(id)), 160, 220, 255);
		}

		/// <summary>Watch a GoPro levy: the next one after the current target (or the first).</summary>
		public static void ConnectLevy(bool next)
		{
			if (Main.dedServ) return;
			var ids = GoProLevies(Main.LocalPlayer);
			if (ids.Count == 0) { SayNoGoPro(); return; }
			int pick = ids[0];
			if (next && Kind == ViewKind.Levy) foreach (int id in ids) if (id > TargetId) { pick = id; goto found; }
			found:
			if (MindControl.LocalSoldierId >= 0) MindControl.RequestEnd();
			Kind = ViewKind.Levy; TargetId = pick; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1; Connected();
			ArenaView.Factor = 1f;
			ObserveNet.SendRequest(2, pick);
			Main.NewText(LevyUtil.T("Observe.Watching", NameOf(pick)), 160, 220, 255);
		}

		/// <summary>Watch one particular GoPro levy (the camera viewer's list).</summary>
		public static void WatchLevy(int id)
		{
			if (Main.dedServ) return;
			if (!GoProLevies(Main.LocalPlayer).Contains(id)) { SayNoGoPro(); return; }
			if (MindControl.LocalSoldierId >= 0) MindControl.RequestEnd();
			Kind = ViewKind.Levy; TargetId = id; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1; Connected();
			ArenaView.Factor = 1f;
			ObserveNet.SendRequest(2, id);
			Main.NewText(LevyUtil.T("Observe.Watching", NameOf(id)), 160, 220, 255);
		}

		public static void Exit()
		{
			if (Kind == ViewKind.Mind) { _optOut = Main.LocalPlayer.dead; MindControl.RequestEnd(); return; } // released while dead: stay released until the respawn
			if (Kind == ViewKind.None) return;
			Kind = ViewKind.None; TargetId = -1; HideHud = false;
			ArenaView.Leave();
			ObserveNet.SendRequest(0, 0);
		}

		public static void OnMindBegin(int soldierId)
		{
			if (Kind == ViewKind.Camera || Kind == ViewKind.Levy) { ObserveNet.SendRequest(0, 0); }
			Kind = ViewKind.Mind; TargetId = soldierId; Zoom = 1f; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1;
			ArenaView.Leave();
		}

		/// <summary>The view follows the levy the link is on now (after the controlled levy fell and the next was handed over).</summary>
		private static void FollowLink(int id)
		{
			if (id == TargetId && MindControl.LocalSoldierId == id) return;
			TargetId = id; MindControl.LocalSoldierId = id; _bodyCache = -1; _haveLast = false;
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
			if (kind == 4) { if (Kind == ViewKind.Mind) OnMindEnd(); return; } // the server ended the link: stop following and sending input now
			if (kind == 3 && Kind == ViewKind.Mind) { FollowLink(id); } // the server moved the link to the next levy
		}

		// =====================================================================================================
		// targets
		// =====================================================================================================

		/// <summary>
		/// Soldier ids (of the local player's Regulars) that can be watched now, ascending: alive, not in reserve, carrying a GoPro and, in single player, on the field with a body
		/// (a levy waiting to deploy past the army's body cap, or between death and respawn, has nothing to look through). A multiplayer client cannot see the server's bodies; the server checks there.
		/// </summary>
		public static List<int> GoProLevies(Player p)
		{
			var list = new List<int>();
			var roster = RosterSystem.Instance; if (roster == null) return list;
			bool client = Main.netMode == NetmodeID.MultiplayerClient;
			foreach (var r in roster.RegularsOf(p)) if (Watchable(r, client)) list.Add(r.Id);
			list.Sort();
			return list;
		}

		/// <summary>The rule <see cref="GoProLevies"/> applies to one levy.</summary>
		internal static bool Watchable(SoldierRecord r, bool client) => r != null && !r.Reserve && !r.Dead && GoProItem.Carries(r.Gear) && (client || r.HasBody);

		/// <summary>No GoPro levy to watch: says whether none carries one, or the ones that do are off the field.</summary>
		private static void SayNoGoPro()
		{
			bool any = false;
			var roster = RosterSystem.Instance;
			if (roster != null) foreach (var r in roster.RegularsOf(Main.LocalPlayer)) if (!r.Reserve && GoProItem.Carries(r.Gear)) { any = true; break; }
			Main.NewText(LevyUtil.T(any ? "Observe.NoGoProField" : "Observe.NoGoPro"), 255, 200, 120);
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
			bool ok = TryCenterCore(out c, out bool fresh);
			if (fresh) _lastSeenTick = Main.GameUpdateCount;
			return ok;
		}

		/// <summary>The view's centre; <paramref name="fresh"/> is true when it was found this frame (not the last known spot kept while the target is out of sight).</summary>
		private static bool TryCenterCore(out Vector2 c, out bool fresh)
		{
			c = _lastCenter; fresh = false;
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
			_lastCenter = c; _haveLast = true; fresh = true;
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
				int z = ids[i]; Zoom = 1f; TargetId = z; _haveLast = false; _remoteTick = uint.MaxValue; Connected();
				ObserveNet.SendRequest(1, z);
			}
			else if (Kind == ViewKind.Levy)
			{
				var ids = GoProLevies(Main.LocalPlayer); if (ids.Count == 0) return;
				int i = ids.IndexOf(TargetId); i = ((i < 0 ? 0 : i + dir) % ids.Count + ids.Count) % ids.Count;
				TargetId = ids[i]; _haveLast = false; _remoteTick = uint.MaxValue; _bodyCache = -1; Connected();
				ObserveNet.SendRequest(2, TargetId);
				Main.NewText(LevyUtil.T("Observe.Watching", NameOf(TargetId)), 160, 220, 255);
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
			bool have = TryCenter(out Vector2 c);
			if (Kind != ViewKind.Mind && SignalLost(Main.GameUpdateCount, _connectTick, _lastSeenTick))
			{
				// the target has been out of sight past the grace time (a watched levy fell, or a camera was removed): the next GoPro levy if there is one, else back to your own view
				if (Kind == ViewKind.Levy && GoProLevies(Main.LocalPlayer).Exists(x => x != TargetId)) { Main.NewText(LevyUtil.T("Observe.Lost"), 255, 200, 120); Cycle(1); return; }
				Main.NewText(LevyUtil.T("Observe.Lost"), 255, 200, 120); Exit();
				return;
			}
			if (!have) return;
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
			if (!me.dead) { _optOut = false; _deathPicked = false; }
			// dying hands you a levy, once per death: when that levy falls the server hands over the next one, and when there is none the view stays released (no key re-links it)
			if (me.dead && Kind != ViewKind.Mind && ObserveConfig.Get != null && ObserveConfig.Get.MindControlOnDeath && MindControl.LocalSoldierId < 0 && !_optOut && !_deathPicked && Main.GameUpdateCount >= _nextAuto)
			{
				_nextAuto = Main.GameUpdateCount + 60u; // a refusal is not retried every frame
				int id = MindControl.PickNext(me, -1, -1, me.Center);
				if (id >= 0) { _deathPicked = true; MindControl.RequestBegin(id, true); }
			}
			if (Kind == ViewKind.Mind && MindControl.LocalViaDeath && !me.dead) { MindControl.RequestEnd(); }
			if (Kind == ViewKind.Mind && Main.netMode == NetmodeID.SinglePlayer)
			{
				var link = MindControl.LinkOfPlayer(Main.myPlayer);
				if (link == null) OnMindEnd(); // the server side ended it
				else FollowLink(link.SoldierId); // the link moved to the next levy while the controlled one fell
			}
			if (Kind == ViewKind.None) { HideHud = false; return; }
			if (Kind != ViewKind.Mind && me.dead) { Exit(); return; }

			bool typing = Main.drawingPlayerChat || Main.editSign || Main.editChest;
			bool esc = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
			bool back = Main.keyState.IsKeyDown(Keys.Back) && !Main.oldKeyState.IsKeyDown(Keys.Back) && !typing;
			if ((esc || back) && HideHud && Main.GameUpdateCount - _hudHiddenAt > 6u) { HideHud = false; if (esc) _closeMenu = true; return; } // the first Esc only brings the HUD back; the next one stops watching
			if (esc || back) { if (esc) _closeMenu = true; Exit(); return; }
			if (!typing)
			{
				// while driving a levy the arrow keys are movement keys: switching levies is Page Up / Page Down there
				// a GoPro or camera view takes the arrow keys and Page Up / Page Down alike
				bool Pressed(Keys k) => Main.keyState.IsKeyDown(k) && !Main.oldKeyState.IsKeyDown(k);
				bool arrows = Kind != ViewKind.Mind;
				if (Pressed(Keys.PageDown) || (arrows && Pressed(Keys.Right))) Cycle(1);
				if (Pressed(Keys.PageUp) || (arrows && Pressed(Keys.Left))) Cycle(-1);
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

		public override void UpdateUI(GameTime gameTime) { if (!Main.dedServ) CameraViewPanel.Update(gameTime); }

		/// <summary>The layers that stay while the HUD is hidden: input and menu plumbing, the cursor and the viewer's own.</summary>
		private static bool KeepWhileHidden(string name) => name.StartsWith("Levy: Observe") || name.Contains("Cursor") || name.StartsWith("Vanilla: Interface Logic") || name == "Vanilla: Ingame Options" || name == "Vanilla: Death Text" || name == "Vanilla: Diagnose Net";

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (Main.dedServ) return;
			layers.Insert(0, new LegacyGameInterfaceLayer("Levy: Observe Restore", () => { ArenaView.RestoreSize(); return true; }, InterfaceScaleType.None));
			int i = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(i < 0 ? layers.Count : i, new LegacyGameInterfaceLayer("Levy: Observe HUD", DrawHud, InterfaceScaleType.UI));
			layers.Insert(i < 0 ? layers.Count : i, new LegacyGameInterfaceLayer("Levy: Observe Viewer", () => { CameraViewPanel.Draw(Main.spriteBatch, new GameTime()); return true; }, InterfaceScaleType.UI));
			if (HideHud && Kind != ViewKind.None) foreach (var l in layers) if (!KeepWhileHidden(l.Name)) l.Active = false;
		}

		private static bool DrawHud()
		{
			var sb = Main.spriteBatch;
			if (Kind != ViewKind.None && HideHud)
			{
				if (Main.GameUpdateCount - _hudHiddenAt < 240u) // a hint for four seconds, then nothing on screen at all
				{
					string hint = LevyUtil.T("Observe.HudRestore"); Vector2 hs = FontAssets.MouseText.Value.MeasureString(hint) * 0.8f;
					Utils.DrawBorderString(sb, hint, new Vector2((Main.screenWidth / Main.UIScale - hs.X) / 2f, 24f), Color.White * 0.8f, 0.8f);
				}
			}
			else if (Kind != ViewKind.None)
			{
				string title = Title(), keys = LevyUtil.T(Kind == ViewKind.Mind ? "Observe.KeysMind" : Kind == ViewKind.Camera ? "Observe.KeysCamera" : "Observe.KeysGoPro");
				float w = Main.screenWidth / Main.UIScale;
				Vector2 sz = FontAssets.MouseText.Value.MeasureString(title);
				Utils.DrawBorderString(sb, title, new Vector2((w - sz.X) / 2f, 24f), Kind == ViewKind.Mind ? new Color(190, 150, 255) : new Color(255, 120, 110), 1f);
				Vector2 sz2 = FontAssets.MouseText.Value.MeasureString(keys) * 0.8f;
				Utils.DrawBorderString(sb, keys, new Vector2((w - sz2.X) / 2f, 52f), Color.White, 0.8f);
			}
			if (!HideHud) DrawGoProMarks(sb);
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
