using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// The Field Manual as a paged book. A draggable panel: a scrollable contents list on the left, the open page on the right (an item icon, the title and the body), Previous / Next and a page counter
	/// along the bottom of the page, and a close button. Esc closes it; the Left and Right arrow keys turn pages. Client only, no state beyond the page last read. The text is in
	/// Localization/en-US_Mods.Levy.Guide.hjson and the page order in <see cref="GuidePages"/>.
	/// </summary>
	public class GuidePanel : UIState
	{
		private DraggablePanel _panel;
		private UIPanel _page;
		private UIList _contents, _body;
		private UIScrollbar _contentsScroll, _bodyScroll;
		private readonly List<LevyButton> _entries = new();
		private GuideIcon _icon;
		private Label _title, _counter;
		private LevyButton _prev, _next, _close;
		private uint _openedTick;
		private int _current;
		private bool _built;

		/// <summary>The page last read, so the book reopens where it was left.</summary>
		private static int _lastPage;

		// ---- layout (UI pixels, panel-local) ----
		private const int W = 720, H = 470, Margin = 14, TopBar = 44;
		private const int ContentsW = 196, ScrollW = 16, EntryH = 24, EntryPad = 2;
		private const int PageLeft = Margin + ContentsW + ScrollW + 8, PageW = W - PageLeft - Margin, PageH = H - TopBar - Margin;
		private const int PageInset = 12, IconSize = 44, BodyTop = 68, FooterH = 28, FooterGap = 12;
		private const float EntryScale = 0.75f, BodyScale = 0.9f;
		private static readonly Color Parchment = new(235, 220, 170), Gold = new(255, 240, 140);

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = new Label(LevyUtil.T("Guide.Title"), 1f) { TextColor = Gold };
			_title.Left.Set(Margin, 0f); _title.Top.Set(10, 0f);
			_panel.Append(_title);

			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance?.CloseManual(), LevyUtil.T("Guide.CloseHint"));
			_close.Left.Set(W - 36, 0f); _close.Top.Set(6, 0f);
			_panel.Append(_close);

			// ---- left: contents ----
			var head = new Label(LevyUtil.T("Guide.Contents"), 0.75f) { TextColor = Parchment };
			head.Left.Set(Margin, 0f); head.Top.Set(TopBar - 2, 0f);
			_panel.Append(head);
			int listTop = TopBar + 18, listH = H - listTop - Margin;
			_contents = new UIList { ListPadding = EntryPad };
			_contents.Left.Set(Margin, 0f); _contents.Top.Set(listTop, 0f);
			_contents.Width.Set(ContentsW, 0f); _contents.Height.Set(listH, 0f);
			_contents.ManualSortMethod = _ => { };
			_panel.Append(_contents);
			_contentsScroll = new UIScrollbar();
			_contentsScroll.Left.Set(Margin + ContentsW + 4, 0f); _contentsScroll.Top.Set(listTop + 4, 0f);
			_contentsScroll.Height.Set(listH - 8, 0f);
			_panel.Append(_contentsScroll);
			_contents.SetScrollbar(_contentsScroll);

			for (int i = 0; i < GuidePages.All.Length; i++)
			{
				int idx = i;
				string full = GuidePages.All[i].Title;
				var b = new LevyButton(Fit(full, ContentsW - 14, EntryScale), EntryScale, ContentsW, EntryH, () => Go(idx), full);
				_entries.Add(b);
				_contents.Add(b);
			}

			// ---- right: the open page ----
			_page = new UIPanel();
			_page.SetPadding(0);
			_page.Left.Set(PageLeft, 0f); _page.Top.Set(TopBar, 0f);
			_page.Width.Set(PageW, 0f); _page.Height.Set(PageH, 0f);
			_page.BackgroundColor = new Color(22, 30, 60) * 0.92f;
			_panel.Append(_page);

			_icon = new GuideIcon(IconSize);
			_icon.Left.Set(PageInset, 0f); _icon.Top.Set(PageInset, 0f);
			_page.Append(_icon);

			_pageTitleLeft = PageInset + IconSize + 10;
			_pageTitle = new Label("", 1.05f) { TextColor = Gold };
			_pageTitle.Left.Set(_pageTitleLeft, 0f); _pageTitle.Top.Set(PageInset + 12, 0f);
			_page.Append(_pageTitle);

			int bodyH = PageH - BodyTop - FooterH - FooterGap * 2;
			_body = new UIList { ListPadding = 8f };
			_body.Left.Set(PageInset, 0f); _body.Top.Set(BodyTop, 0f);
			_body.Width.Set(PageW - PageInset * 2 - ScrollW - 6, 0f); _body.Height.Set(bodyH, 0f);
			_body.ManualSortMethod = _ => { };
			_page.Append(_body);
			_bodyScroll = new UIScrollbar();
			_bodyScroll.Left.Set(PageW - PageInset - ScrollW, 0f); _bodyScroll.Top.Set(BodyTop + 4, 0f);
			_bodyScroll.Height.Set(bodyH - 8, 0f);
			_page.Append(_bodyScroll);
			_body.SetScrollbar(_bodyScroll);

			int footerTop = PageH - FooterH - FooterGap;
			_prev = new LevyButton(LevyUtil.T("Guide.Previous"), LevyConstants.UI.ButtonTextScale, 110, FooterH, () => Go(_current - 1), LevyUtil.T("Guide.PreviousHint"));
			_prev.Left.Set(PageInset, 0f); _prev.Top.Set(footerTop, 0f);
			_page.Append(_prev);
			_next = new LevyButton(LevyUtil.T("Guide.Next"), LevyConstants.UI.ButtonTextScale, 110, FooterH, () => Go(_current + 1), LevyUtil.T("Guide.NextHint"));
			_next.Left.Set(PageW - PageInset - 110, 0f); _next.Top.Set(footerTop, 0f);
			_page.Append(_next);
			_counter = new Label("", 0.8f) { TextColor = Parchment };
			_counter.Top.Set(footerTop + 6, 0f);
			_page.Append(_counter);

			Append(_panel);
			_built = true;
		}

		private Label _pageTitle;
		private int _pageTitleLeft;

		/// <summary>Centre the panel and show the page last read.</summary>
		public void Open()
		{
			if (!_built) return;
			_openedTick = Main.GameUpdateCount;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			_panel.Left.Set(Math.Max(0f, (sw - W) * 0.5f), 0f);
			_panel.Top.Set(Math.Max(0f, (sh - H) * 0.5f), 0f);
			Go(_lastPage);
		}

		/// <summary>Turn to a page (clamped), rebuild its text and keep the contents list scrolled so the entry shows.</summary>
		private void Go(int index)
		{
			int n = GuidePages.All.Length;
			if (!_built || n == 0) return;
			_current = Math.Clamp(index, 0, n - 1);
			_lastPage = _current;
			var page = GuidePages.All[_current];

			_pageTitle.SetText(page.Title);
			_icon.Type = page.IconType;
			_pageTitle.Left.Set(page.IconType > 0 ? _pageTitleLeft : PageInset, 0f);

			_body.Clear();
			float lineW = _body.Width.Pixels - 4f;
			foreach (string raw in page.Body.Split('\n'))
			{
				string line = raw.Trim();
				if (line.Length == 0) continue;
				var text = new UIText("", BodyScale) { IsWrapped = true, TextColor = Parchment, WrappedTextBottomPadding = 0f };
				text.Width.Set(lineW, 0f);
				text.Recalculate();
				text.SetText(line);
				_body.Add(text);
			}

			for (int i = 0; i < _entries.Count; i++) _entries[i].Lit = i == _current;
			_prev.Enabled = _current > 0;
			_next.Enabled = _current < n - 1;
			string counter = LevyUtil.T("Guide.PageOf", _current + 1, n);
			_counter.SetText(counter);
			float cw = FontAssets.MouseText.Value.MeasureString(counter).X * 0.8f;
			_counter.Left.Set((PageW - cw) * 0.5f, 0f);

			Recalculate();
			_bodyScroll.ViewPosition = 0f;
			float rowTop = _current * (EntryH + EntryPad), viewH = _contents.Height.Pixels;
			float view = _contentsScroll.ViewPosition;
			if (rowTop < view) view = rowTop;
			else if (rowTop + EntryH > view + viewH) view = rowTop + EntryH - viewH;
			_contentsScroll.ViewPosition = view;
		}

		/// <summary>The text cut to fit a pixel width at a scale, ending in "..." when it had to be cut.</summary>
		private static string Fit(string text, float maxWidth, float scale)
		{
			var font = FontAssets.MouseText.Value;
			if (font.MeasureString(text).X * scale <= maxWidth) return text;
			for (int len = text.Length - 1; len > 0; len--)
			{
				string cut = text.Substring(0, len).TrimEnd() + "...";
				if (font.MeasureString(cut).X * scale <= maxWidth) return cut;
			}
			return text;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_panel.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (Main.GameUpdateCount - _openedTick <= 6) return;
			if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape)) { UISystem.Instance?.CloseManual(); return; }
			if (Main.drawingPlayerChat) return;
			if (Main.keyState.IsKeyDown(Keys.Right) && !Main.oldKeyState.IsKeyDown(Keys.Right) && _next.Enabled) Go(_current + 1);
			else if (Main.keyState.IsKeyDown(Keys.Left) && !Main.oldKeyState.IsKeyDown(Keys.Left) && _prev.Enabled) Go(_current - 1);
		}

		/// <summary>A page's item icon: the item's sprite fitted into a dark square, nothing drawn when the type is 0.</summary>
		private sealed class GuideIcon : UIElement
		{
			public int Type;
			public GuideIcon(int size) { Width.Set(size, 0f); Height.Set(size, 0f); IgnoresMouseInteraction = true; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (Type <= 0) return;
				var box = GetDimensions().ToRectangle();
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, box, new Color(10, 14, 32) * 0.7f);
				Main.instance.LoadItem(Type);
				var tex = TextureAssets.Item[Type].Value;
				var frame = Main.itemAnimations[Type] != null ? Main.itemAnimations[Type].GetFrame(tex) : tex.Frame();
				float fit = Math.Min(1f, (box.Width - 8f) / Math.Max(frame.Width, frame.Height));
				spriteBatch.Draw(tex, box.Center.ToVector2(), frame, Color.White, 0f, frame.Size() * 0.5f, fit, SpriteEffects.None, 0f);
			}
		}
	}
}
