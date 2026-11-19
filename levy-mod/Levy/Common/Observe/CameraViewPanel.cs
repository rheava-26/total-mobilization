using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.UI;
using Levy.Common.Util;

namespace Levy.Common.Observe
{
	/// <summary>
	/// The camera viewer: right-click a GoPro (in the pack or in hand) to open it. It lists every levy that carries a GoPro and every static camera; click one to watch it (the same view the GoPro's left-click and the static camera's right-click open).
	/// A toggle hides the HUD while watching (Esc or the same button brings it back); another stops watching. Client only; the list rebuilds itself when the set of sources changes.
	/// </summary>
	public static class CameraViewPanel
	{
		private static UserInterface _ui;
		private static ViewerState _state;

		public static bool Open => _ui?.CurrentState != null;

		public static void Toggle() { if (Open) Close(); else Show(); }

		public static void Show()
		{
			if (Main.dedServ || Main.gameMenu) return;
			try
			{
				if (_ui == null) { _ui = new UserInterface(); _state = new ViewerState(); _state.Activate(); }
				ObserveSystem.SetHideHud(false);
				_state.Rebuild(true);
				_ui.SetState(_state);
				SoundEngine.PlaySound(SoundID.MenuOpen);
			}
			catch (Exception e) { ModContent.GetInstance<ObserveSystem>().Mod.Logger.Error("[CameraView] open failed: " + e); _ui = null; _state = null; }
		}

		public static void Close()
		{
			if (_ui?.CurrentState == null) return;
			_ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public static void Unload() { _ui = null; _state = null; }

		public static void Update(GameTime gt)
		{
			if (_ui?.CurrentState == null) return;
			if (Main.gameMenu || Main.LocalPlayer.dead) { Close(); return; }
			if (ObserveSystem.Kind == ViewKind.None && Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) { Close(); return; }
			_ui.Update(gt);
		}

		public static void Draw(SpriteBatch sb, GameTime gt)
		{
			if (_ui?.CurrentState == null) return;
			if (ObserveSystem.HideHud && ObserveSystem.Kind != ViewKind.None) return; // hidden along with the rest of the HUD
			_ui.Draw(sb, gt);
		}

		private sealed class ViewerState : UIState
		{
			private const int W = 300, H = 360, Margin = 12;
			private DraggablePanel _panel;
			private UIList _list; private UIScrollbar _bar;
			private LevyButton _hide, _stop;
			private Label _title, _empty;
			private string _signature = "";
			private int _tick;

			public override void OnInitialize()
			{
				_panel = new DraggablePanel(); _panel.SetPadding(0);
				_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
				_panel.BackgroundColor = new Color(33, 43, 79) * 0.94f;
				float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
				_panel.Left.Set(Math.Max(0f, sw - W - 40f), 0f); _panel.Top.Set(Math.Max(0f, (sh - H) * 0.3f), 0f);

				_title = new Label(LevyUtil.T("Observe.CamTitle"), 1f) { TextColor = new Color(255, 240, 140) };
				_title.Left.Set(Margin, 0f); _title.Top.Set(10, 0f);
				_panel.Append(_title);
				var close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, Close, LevyUtil.T("UI2.Close"));
				close.Left.Set(W - 36, 0f); close.Top.Set(6, 0f);
				_panel.Append(close);

				_list = ChoicePopup.KeepOrder(new UIList { ListPadding = 4f });
				_list.Left.Set(Margin, 0f); _list.Top.Set(44, 0f); _list.Width.Set(W - Margin * 2 - 22, 0f); _list.Height.Set(H - 44 - 84, 0f);
				_bar = new UIScrollbar();
				_bar.Left.Set(W - Margin - 18, 0f); _bar.Top.Set(48, 0f); _bar.Height.Set(H - 44 - 92, 0f);
				_list.SetScrollbar(_bar);
				_panel.Append(_list); _panel.Append(_bar);

				_empty = new Label(LevyUtil.T("Observe.CamEmpty"), 0.8f);
				_empty.Left.Set(Margin, 0f); _empty.Top.Set(48, 0f);

				_hide = new LevyButton("", LevyConstants.UI.ButtonTextScale, W - Margin * 2, 28, () => { ObserveSystem.SetHideHud(!ObserveSystem.HideHud); }, LevyUtil.T("Observe.CamHideHint"));
				_hide.Left.Set(Margin, 0f); _hide.Top.Set(H - 76, 0f);
				_panel.Append(_hide);
				_stop = new LevyButton(LevyUtil.T("Observe.CamStop"), LevyConstants.UI.ButtonTextScale, W - Margin * 2, 28, ObserveSystem.Exit, LevyUtil.T("Observe.CamStopHint"));
				_stop.Left.Set(Margin, 0f); _stop.Top.Set(H - 40, 0f);
				_panel.Append(_stop);
				Append(_panel);
			}

			/// <summary>The sources the player can watch right now: (kind, id, name). Kind 1 = camera, 2 = GoPro levy, 3 = GoPro levy off the field (shown greyed).</summary>
			private static List<(int kind, int id, string name)> Sources()
			{
				var res = new List<(int, int, string)>();
				var watch = ObserveSystem.GoProLevies(Main.LocalPlayer);
				foreach (int id in watch) res.Add((2, id, ObserveSystem.NameOf(id)));
				// GoPro levies with nothing to look through right now (fallen, or waiting to deploy past the army's body cap) are listed greyed, so a camera never seems to vanish
				var roster = global::Levy.Common.Systems.RosterSystem.Instance;
				if (roster != null)
				{
					var off = new List<int>();
					foreach (var r in roster.RegularsOf(Main.LocalPlayer)) if (!r.Reserve && global::Levy.Content.Items.Observe.GoProItem.Carries(r.Gear) && !watch.Contains(r.Id)) off.Add(r.Id);
					off.Sort();
					foreach (int id in off) res.Add((3, id, ObserveSystem.NameOf(id)));
				}
				var cams = new List<int>(); foreach (var te in StaticCameraTE.All()) cams.Add(te.ID);
				foreach (var c in ObserveSystem.Cams) if (!cams.Contains(c.Id)) cams.Add(c.Id);
				cams.Sort();
				foreach (int id in cams) res.Add((1, id, ObserveSystem.CameraName(id)));
				return res;
			}

			private static bool Watching(int kind, int id) => (kind == 2 && ObserveSystem.Kind == ViewKind.Levy && ObserveSystem.TargetId == id) || (kind == 1 && ObserveSystem.Kind == ViewKind.Camera && ObserveSystem.TargetId == id);

			public void Rebuild(bool force)
			{
				var src = Sources();
				string sig = ObserveSystem.Kind + ":" + ObserveSystem.TargetId + "|";
				foreach (var s in src) sig += s.kind + "." + s.id + "." + s.name + ",";
				if (!force && sig == _signature) return;
				_signature = sig;
				_list.Clear();
				if (src.Count == 0) { _panel.Append(_empty); }
				else _panel.RemoveChild(_empty);
				int lastKind = 0;
				foreach (var (kind, id, name) in src)
				{
					if (kind != lastKind && !(kind == 3 && lastKind == 2))
					{
						lastKind = kind;
						var head = new UIElement(); head.Width.Set(0f, 1f); head.Height.Set(20, 0f);
						var l = new Label(LevyUtil.T(kind != 1 ? "Observe.CamLevies" : "Observe.CamCameras"), 0.8f) { TextColor = new Color(200, 210, 240) };
						head.Append(l); _list.Add(head);
					}
					lastKind = kind;
					int k = kind, i = id;
					if (kind == 3)
					{
						var off = new LevyButton(LevyUtil.T("Observe.CamOffField", name), LevyConstants.UI.ButtonTextScale, 0, 28, null, LevyUtil.T("Observe.NoGoProField")) { Enabled = false };
						off.Width.Set(0f, 1f);
						_list.Add(off);
						continue;
					}
					var b = new LevyButton(name, LevyConstants.UI.ButtonTextScale, 0, 28, () => { if (k == 2) ObserveSystem.WatchLevy(i); else ObserveSystem.ConnectCamera(i); });
					b.Width.Set(0f, 1f); b.Lit = Watching(kind, id);
					_list.Add(b);
				}
				Recalculate();
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				if (_panel.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
				if (++_tick >= 20) { _tick = 0; Rebuild(false); }
				bool watching = ObserveSystem.Kind != ViewKind.None;
				_hide.Set(LevyUtil.T(ObserveSystem.HideHud ? "Observe.CamShowHud" : "Observe.CamHideHud"), watching);
				_hide.Lit = ObserveSystem.HideHud;
				_stop.Set(LevyUtil.T("Observe.CamStop"), watching);
			}
		}
	}
}
