using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.UI;
using Levy.Common.Util;

namespace Levy.Common.Potions
{
	/// <summary>
	/// A minimal item-grid chooser (search box, scrolling grid, Close) for the Seeker potion. Used when the logistics screen's chooser is not on screen: the Seeker is usually drunk away from any armory panel.
	/// <see cref="Open"/> takes the candidate item types and a callback that receives the picked type.
	/// </summary>
	public class SeekerChooser : ModSystem
	{
		private static UserInterface _ui;
		private static ChooserState _state;
		public static bool IsOpen => _ui?.CurrentState != null;

		public override void Load()
		{
			if (Main.dedServ) return;
			_ui = new UserInterface(); _state = new ChooserState();
		}

		public override void Unload() { _ui = null; _state = null; }

		public static void Open(IReadOnlyList<int> candidates, Action<int> picked)
		{
			if (Main.dedServ || _ui == null) return;
			_state.Setup(candidates, t => { Close(); picked?.Invoke(t); });
			_ui.SetState(_state);
		}

		public static void Close() { _ui?.SetState(null); }

		public override void UpdateUI(GameTime gameTime)
		{
			if (_ui?.CurrentState == null) return;
			if (Main.gameMenu || Main.LocalPlayer.dead || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) { Close(); return; }
			_ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int i = layers.FindIndex(l => l.Name.Equals("Vanilla: Inventory"));
			if (i < 0) return;
			layers.Insert(i + 1, new LegacyGameInterfaceLayer("Levy: Seeker Chooser", () =>
			{
				if (_ui?.CurrentState != null) _ui.Draw(Main.spriteBatch, new GameTime());
				return true;
			}, InterfaceScaleType.UI));
		}

		private class ChooserState : UIState
		{
			private UIPanel _panel;
			private readonly UIGrid _grid = new();
			private readonly UIScrollbar _bar = new();
			private UISearchBar _search;
			private Action<int> _pick;
			private List<int> _all = new();
			private string _term = "";

			public void Setup(IReadOnlyList<int> cands, Action<int> pick)
			{
				RemoveAllChildren();
				_pick = pick; _all = new List<int>(cands); _term = "";
				const float w = 420, h = 360;
				_panel = new UIPanel(); _panel.SetPadding(8);
				_panel.Width.Set(w, 0f); _panel.Height.Set(h, 0f); _panel.HAlign = 0.5f; _panel.VAlign = 0.4f;
				_panel.BackgroundColor = new Color(20, 26, 52) * 0.95f;
				Append(_panel);
				var title = new UIText(LevyUtil.T("Potions.SeekerPrompt"), 0.9f); _panel.Append(title);
				var box = new UIPanel(); box.SetPadding(0); box.Top.Set(26, 0f); box.Width.Set(w - 120, 0f); box.Height.Set(28, 0f); box.BackgroundColor = new Color(10, 14, 34) * 0.9f;
				_search = new UISearchBar(Language.GetText("Mods.Levy.Logistics.FindSearch"), 0.8f);
				_search.Left.Set(6, 0f); _search.Width.Set(-12, 1f); _search.Height.Set(0, 1f);
				_search.OnContentsChanged += s => { _term = (s ?? "").Trim().ToLowerInvariant(); Fill(); };
				_search.OnCanceledTakingInput += () => { if (_search.IsWritingText) _search.ToggleTakingText(); };
				box.OnLeftClick += (e, el) => { if (!_search.IsWritingText) _search.ToggleTakingText(); };
				box.Append(_search); _panel.Append(box);
				var close = new LevyButton(LevyUtil.T("Potions.SeekerClose"), LevyConstants.UI.ButtonTextScale, 90, 28, Close);
				close.Left.Set(w - 118, 0f); close.Top.Set(26, 0f); _panel.Append(close);
				_grid.Clear(); _grid.Top.Set(62, 0f); _grid.Width.Set(w - 44, 0f); _grid.Height.Set(h - 86, 0f); _grid.ListPadding = 2f;
				_bar.Left.Set(w - 36, 0f); _bar.Top.Set(62, 0f); _bar.Height.Set(h - 86, 0f); _grid.SetScrollbar(_bar);
				_panel.Append(_grid); _panel.Append(_bar);
				Fill();
				Recalculate();
			}

			private void Fill()
			{
				_grid.Clear();
				foreach (int t in _all)
				{
					if (_term.Length > 0 && !Lang.GetItemNameValue(t).ToLowerInvariant().Contains(_term)) continue;
					int tt = t;
					_grid.Add(new TypeCell(t, () => _pick?.Invoke(tt)));
				}
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				if (_panel != null && _panel.IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
			}
		}
	}
}
