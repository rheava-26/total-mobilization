using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>The Field Manual reading panel: a draggable, scrollable page of wrapped text with a close button. Client only.</summary>
	public class ManualPanel : UIState
	{
		private DraggablePanel _panel;
		private UIList _list;
		private UIScrollbar _scroll;
		private Label _title;
		private LevyButton _close;
		private uint _openedTick;

		private const int W = 580, H = 470;
		private const int BodyTop = 44, Margin = 14, ScrollW = 20;
		private const float TextScale = 0.85f;
		private static readonly Color Parchment = new(235, 220, 170);

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = new Label("", 1f) { TextColor = new Color(255, 240, 140) };
			_title.Left.Set(Margin, 0f); _title.Top.Set(10, 0f);
			_panel.Append(_title);

			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance?.CloseManual(), LevyUtil.T("UI2.Close"));
			_close.Left.Set(W - 36, 0f); _close.Top.Set(6, 0f);
			_panel.Append(_close);

			_list = new UIList();
			_list.Left.Set(Margin, 0f); _list.Top.Set(BodyTop, 0f);
			_list.Width.Set(W - Margin * 2 - ScrollW - 6, 0f); _list.Height.Set(H - BodyTop - 12, 0f);
			_list.ListPadding = 4f;
			_list.ManualSortMethod = _ => { };
			_panel.Append(_list);

			_scroll = new UIScrollbar();
			_scroll.Left.Set(W - Margin - ScrollW, 0f); _scroll.Top.Set(BodyTop + 4, 0f);
			_scroll.Height.Set(H - BodyTop - 20, 0f);
			_panel.Append(_scroll);
			_list.SetScrollbar(_scroll);

			Append(_panel);
		}

		/// <summary>Centre the panel, rebuild the text from localization and scroll to the top.</summary>
		public void Open()
		{
			if (_panel == null) return;
			_openedTick = Main.GameUpdateCount;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			_panel.Left.Set(Math.Max(0f, (sw - W) * 0.5f), 0f);
			_panel.Top.Set(Math.Max(0f, (sh - H) * 0.5f), 0f);
			_title.SetText(LevyUtil.T("Manual.Title"));

			_list.Clear();
			float lineW = _list.Width.Pixels - 4f;
			foreach (string raw in LevyUtil.T("Manual.Body").Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Trim().Length == 0)
				{
					var gap = new UIElement();
					gap.Width.Set(lineW, 0f); gap.Height.Set(6f, 0f);
					_list.Add(gap);
					continue;
				}
				var text = new UIText("", TextScale) { IsWrapped = true, TextColor = Parchment, WrappedTextBottomPadding = 0f };
				text.Width.Set(lineW, 0f);
				text.Recalculate();
				text.SetText(line);
				_list.Add(text);
			}
			Recalculate();
			_scroll.ViewPosition = 0f;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_panel.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (Main.GameUpdateCount - _openedTick > 6 && Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
				UISystem.Instance?.CloseManual();
		}
	}
}
