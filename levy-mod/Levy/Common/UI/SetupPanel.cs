using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Levy.Common.Config;
using Levy.Common.Systems;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// The new-world setup popup. Page 1: three presets (Default, Relaxed, Hardcore), Customize and Skip. Page 2 (Customize): the three levy counts, whether bosses target and punish levies,
	/// the live "too few / too many" notes, and a pointer to the full config. Single player only; answering is saved per world. Client only.
	/// </summary>
	public class SetupPanel : UIState
	{
		private DraggablePanel _panel;
		private UIElement _page0, _page1;
		private Label _title, _footer;
		private readonly LevyButton[] _presetBtn = new LevyButton[3];
		private readonly UIText[] _presetBlurb = new UIText[3];
		private LevyButton _customize, _skip, _apply, _back, _close;
		private readonly LevyButton[] _minus = new LevyButton[3], _plus = new LevyButton[3];
		private readonly Label[] _countLabel = new Label[3];
		private LevyButton _targetBtn, _punishBtn;
		private UIText _anchors, _warn;
		private uint _openedTick;
		private bool _onCustom;

		// working values for the customize page
		private int _pre, _hm, _post, _max;
		private bool _target, _punish;

		private const int W = 540, H = 440, Margin = 16;
		private static readonly Color Parchment = new(235, 220, 170);
		private static readonly Color WarnColor = new(240, 200, 110);

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.94f;

			_title = new Label(LevyUtil.T("Setup.Title"), 1f) { TextColor = new Color(255, 240, 140) };
			_title.Left.Set(Margin, 0f); _title.Top.Set(10, 0f);
			_panel.Append(_title);
			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => Finish(SetupState.Skipped, SetupPreset.Default, false), LevyUtil.T("Setup.SkipHint"));
			_close.Left.Set(W - 36, 0f); _close.Top.Set(6, 0f);
			_panel.Append(_close);

			// ---- page 0: presets
			_page0 = new UIElement(); _page0.Width.Set(W, 0f); _page0.Height.Set(H, 0f);
			var intro = new UIText(LevyUtil.T("Setup.Intro"), 0.82f) { IsWrapped = true, TextColor = Parchment, WrappedTextBottomPadding = 0f };
			intro.Left.Set(Margin, 0f); intro.Top.Set(46, 0f); intro.Width.Set(W - Margin * 2, 0f); intro.Height.Set(44, 0f);
			_page0.Append(intro);
			for (int i = 0; i < 3; i++)
			{
				var p = (SetupPreset)i;
				int idx = i;
				float y = 100 + i * 78;
				_presetBtn[i] = new LevyButton(SetupPresets.Name(p), 0.9f, 170, 34, () => Finish(SetupState.Answered, (SetupPreset)idx, false), SetupPresets.Blurb(p));
				_presetBtn[i].Left.Set(Margin, 0f); _presetBtn[i].Top.Set(y, 0f);
				_page0.Append(_presetBtn[i]);
				_presetBlurb[i] = new UIText("", 0.78f) { IsWrapped = true, TextColor = Parchment, WrappedTextBottomPadding = 0f };
				_presetBlurb[i].Left.Set(Margin + 182, 0f); _presetBlurb[i].Top.Set(y - 2, 0f); _presetBlurb[i].Width.Set(W - Margin * 2 - 182, 0f); _presetBlurb[i].Height.Set(70, 0f);
				_page0.Append(_presetBlurb[i]);
			}
			_customize = new LevyButton(LevyUtil.T("Setup.Customize"), 0.85f, 170, 34, ShowCustom, LevyUtil.T("Setup.CustomizeHint"));
			_customize.Left.Set(Margin, 0f); _customize.Top.Set(H - 56, 0f);
			_page0.Append(_customize);
			_skip = new LevyButton(LevyUtil.T("Setup.Skip"), 0.85f, 170, 34, () => Finish(SetupState.Skipped, SetupPreset.Default, false), LevyUtil.T("Setup.SkipHint"));
			_skip.Left.Set(W - Margin - 170, 0f); _skip.Top.Set(H - 56, 0f);
			_page0.Append(_skip);

			// ---- page 1: customize
			_page1 = new UIElement(); _page1.Width.Set(W, 0f); _page1.Height.Set(H, 0f);
			_anchors = new UIText("", 0.8f) { IsWrapped = true, TextColor = Parchment, WrappedTextBottomPadding = 0f };
			_anchors.Left.Set(Margin, 0f); _anchors.Top.Set(44, 0f); _anchors.Width.Set(W - Margin * 2, 0f); _anchors.Height.Set(60, 0f);
			_page1.Append(_anchors);
			string[] keys = { "Setup.CountPre", "Setup.CountHardmode", "Setup.CountPost" };
			for (int i = 0; i < 3; i++)
			{
				int t = i;
				float y = 112 + i * 36;
				_countLabel[i] = new Label("", 0.85f) { TextColor = Color.White };
				_countLabel[i].Left.Set(Margin, 0f); _countLabel[i].Top.Set(y + 6, 0f);
				_page1.Append(_countLabel[i]);
				_minus[i] = new LevyButton("-", 0.9f, 34, 30, () => Step(t, -Stride()), LevyUtil.T("Setup.StepHint"));
				_minus[i].Left.Set(W - Margin - 74, 0f); _minus[i].Top.Set(y, 0f);
				_plus[i] = new LevyButton("+", 0.9f, 34, 30, () => Step(t, Stride()), LevyUtil.T("Setup.StepHint"));
				_plus[i].Left.Set(W - Margin - 34, 0f); _plus[i].Top.Set(y, 0f);
				_page1.Append(_minus[i]); _page1.Append(_plus[i]);
			}
			_targetBtn = new LevyButton("", 0.8f, W - Margin * 2, 30, () => { _target = !_target; Refresh(); }, LevyUtil.T("Setup.TargetHint"));
			_targetBtn.Left.Set(Margin, 0f); _targetBtn.Top.Set(228, 0f);
			_punishBtn = new LevyButton("", 0.8f, W - Margin * 2, 30, () => { _punish = !_punish; Refresh(); }, LevyUtil.T("Setup.PunishHint"));
			_punishBtn.Left.Set(Margin, 0f); _punishBtn.Top.Set(264, 0f);
			_page1.Append(_targetBtn); _page1.Append(_punishBtn);
			_warn = new UIText("", 0.78f) { IsWrapped = true, TextColor = WarnColor, WrappedTextBottomPadding = 0f };
			_warn.Left.Set(Margin, 0f); _warn.Top.Set(304, 0f); _warn.Width.Set(W - Margin * 2, 0f); _warn.Height.Set(60, 0f);
			_page1.Append(_warn);
			_footer = new Label(LevyUtil.T("Setup.MoreOptions"), 0.72f) { TextColor = new Color(170, 185, 220) };
			_footer.Left.Set(Margin, 0f); _footer.Top.Set(H - 82, 0f);
			_page1.Append(_footer);
			_apply = new LevyButton(LevyUtil.T("Setup.Apply"), 0.85f, 170, 34, ApplyCustom, LevyUtil.T("Setup.ApplyHint"));
			_apply.Left.Set(Margin, 0f); _apply.Top.Set(H - 56, 0f);
			_back = new LevyButton(LevyUtil.T("Setup.Back"), 0.85f, 170, 34, ShowPresets);
			_back.Left.Set(W - Margin - 170, 0f); _back.Top.Set(H - 56, 0f);
			_page1.Append(_apply); _page1.Append(_back);

			Append(_panel);
		}

		/// <summary>Centre the panel on the presets page.</summary>
		public void Open()
		{
			if (_panel == null) return;
			_openedTick = Main.GameUpdateCount;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			_panel.Left.Set(Math.Max(0f, (sw - W) * 0.5f), 0f);
			_panel.Top.Set(Math.Max(0f, (sh - H) * 0.5f), 0f);
			var c = LevyConfig.Get;
			_pre = c.LeviesByEndOfPreHardmode; _hm = c.LeviesByEndOfHardmode; _post = c.LeviesByEndOfPostMoonLord; _max = c.MaxLevies;
			_target = c.BossesTargetSoldiers; _punish = c.BossesPunishSoldiers;
			for (int i = 0; i < 3; i++)
			{
				var v = SetupPresets.Of((SetupPreset)i);
				_presetBlurb[i].SetText(SetupPresets.Blurb((SetupPreset)i) + "\n" + LevyUtil.T("Setup.PresetNumbers", v.Pre, v.Hm, v.Post));
			}
			ShowPresets();
		}

		private void ShowPresets()
		{
			_onCustom = false;
			_panel.RemoveChild(_page1); _panel.RemoveChild(_page0);
			_panel.Append(_page0);
			Recalculate();
		}

		private void ShowCustom()
		{
			_onCustom = true;
			_panel.RemoveChild(_page0); _panel.RemoveChild(_page1);
			_panel.Append(_page1);
			Refresh();
			Recalculate();
		}

		private static int Stride() => Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift) ? 5 : 1;

		private void Step(int tier, int delta)
		{
			if (tier == 0) _pre = Math.Clamp(_pre + delta, 4, 200);
			else if (tier == 1) _hm = Math.Clamp(_hm + delta, 4, 200);
			else _post = Math.Clamp(_post + delta, 4, 200);
			if (_hm < _pre) { if (tier == 0) _hm = _pre; else _pre = _hm; }
			if (_post < _hm) { if (tier == 2) _hm = Math.Min(_hm, _post); else _post = _hm; }
			if (_hm < _pre) _pre = _hm;
			Refresh();
		}

		private void Refresh()
		{
			_anchors.SetText(LevyAnchors.Explain(_pre, _hm, _post));
			int[] v = { _pre, _hm, _post };
			for (int i = 0; i < 3; i++)
				_countLabel[i].SetText(LevyUtil.T("Setup.Count" + LevyAnchors.TierKeys[i], v[i], LevyAnchors.RecommendedLow[i], LevyAnchors.RecommendedHigh[i]));
			_targetBtn.SetText(LevyUtil.T(_target ? "Setup.TargetOn" : "Setup.TargetOff"));
			_targetBtn.Lit = _target;
			_punishBtn.SetText(LevyUtil.T(_punish ? "Setup.PunishOn" : "Setup.PunishOff"));
			_punishBtn.Lit = _punish;
			var notes = LevyAnchors.Warnings(_pre, _hm, _post, Math.Max(_max, _post));
			var sb = new System.Text.StringBuilder();
			foreach (var k in notes) { if (sb.Length > 0) sb.Append('\n'); sb.Append(LevyUtil.T(k)); }
			_warn.SetText(sb.ToString());
			Recalculate();
		}

		private void ApplyCustom()
		{
			var v = new SetupPresets.Values(_pre, _hm, _post, Math.Max(_max, _post), _target, _punish);
			Finish(SetupState.Answered, SetupPreset.Default, true, v);
		}

		private void Finish(byte state, SetupPreset preset, bool customized, SetupPresets.Values? custom = null)
		{
			if (state == SetupState.Answered && !SetupPresets.ApplyAndSave(custom ?? SetupPresets.Of(preset)))
				Main.NewText(LevyUtil.T("Setup.HostOnly"), 230, 200, 120);
			if (state != SetupState.Skipped || WorldSetupSystem.State == SetupState.Unanswered) WorldSetupSystem.Answer(state, preset, customized); // skipping a re-opened popup keeps the earlier answer
			UISystem.Instance?.CloseSetup();
			if (state == SetupState.Answered) Main.NewText(LevyUtil.T(customized ? "Setup.DoneCustom" : "Setup.Done", SetupPresets.Name(preset)), 160, 220, 160);
			else Main.NewText(LevyUtil.T("Setup.Skipped"), 200, 200, 160);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_panel.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (Main.GameUpdateCount - _openedTick > 6 && Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
			{
				if (_onCustom) ShowPresets(); else Finish(SetupState.Skipped, SetupPreset.Default, false);
			}
		}
	}
}
