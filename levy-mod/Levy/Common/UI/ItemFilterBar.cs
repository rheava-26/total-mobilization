using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;
using Levy.Common.Logistics;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// The Items tab's search row, as one reusable element: a search box (<see cref="UISearchBar"/> keeps typing away from game hotkeys), a "Type: X" button that cycles the kinds (right-click goes back) and a Clear button.
	/// The Buy, Sell and Find lists of the logistics screen share it; the owner calls <see cref="Matches"/> per item and refreshes on <see cref="Changed"/>.
	/// </summary>
	public class ItemFilterBar : UIElement
	{
		public const int TypeWidth = 190, ClearWidth = 60, Gap = 8, RowHeight = 26;
		private readonly UIPanel _box = new(); private readonly UISearchBar _search; private readonly LevyButton _typeBtn, _clearBtn;
		private readonly FilterCat[] _cats = ItemFilter.Cats();
		private int _idx; private string _term = "";
		public event Action Changed;

		public string Term => _term;
		public FilterCat Cat => _cats[_idx];
		public bool Active => _term.Length > 0 || _idx != 0;
		public bool Typing => _search.IsWritingText;

		public ItemFilterBar(float width, string searchKey)
		{
			Width.Set(width, 0f); Height.Set(RowHeight, 0f);
			_box.SetPadding(0); _box.Width.Set(width - TypeWidth - ClearWidth - 2 * Gap, 0f); _box.Height.Set(RowHeight, 0f); _box.BackgroundColor = new Color(20, 26, 52) * 0.9f;
			_search = new UISearchBar(Language.GetText(searchKey), LevyConstants.UI.ButtonTextScale + 0.05f);
			_search.Left.Set(LevyConstants.UI.ArmorySearchInset, 0f); _search.Width.Set(-2f * LevyConstants.UI.ArmorySearchInset, 1f); _search.Height.Set(0f, 1f);
			_search.OnContentsChanged += s => { _term = (s ?? "").Trim().ToLowerInvariant(); Changed?.Invoke(); Refresh(); };
			_search.OnCanceledTakingInput += () => _search.SetContents("", true);
			_box.OnLeftClick += (e, el) => { if (!_search.IsWritingText) _search.ToggleTakingText(); };
			_box.Append(_search); Append(_box);
			_typeBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, TypeWidth, LevyConstants.UI.ArmoryFilterHeight, () => Step(1), LevyUtil.T("UI2.ArmoryCategoryHint")) { OnAltClick = () => Step(-1) };
			_typeBtn.Left.Set(width - TypeWidth - ClearWidth - Gap, 0f); _typeBtn.Top.Set(1, 0f); Append(_typeBtn);
			_clearBtn = new LevyButton(LevyUtil.T("UI2.ArmoryClear"), LevyConstants.UI.ButtonTextScale, ClearWidth, LevyConstants.UI.ArmoryFilterHeight, Clear, LevyUtil.T("UI2.ArmoryClearHint"));
			_clearBtn.Left.Set(width - ClearWidth, 0f); _clearBtn.Top.Set(1, 0f); Append(_clearBtn);
			_search.SetContents("", true); // only after the buttons exist: SetContents fires OnContentsChanged -> Refresh
			Refresh();
		}

		private void Step(int d) { _idx = (_idx + d + _cats.Length) % _cats.Length; Changed?.Invoke(); Refresh(); }

		public void Clear() { _idx = 0; _search.SetContents("", true); _term = ""; Changed?.Invoke(); Refresh(); }

		public void StopTyping() { if (_search.IsWritingText) _search.ToggleTakingText(); }

		private void Refresh()
		{
			if (_typeBtn == null || _clearBtn == null) return; // UISearchBar can fire OnContentsChanged during construction
			_typeBtn.SetText(LevyUtil.T("UI2.ArmoryCategory", _idx == 0 ? LevyUtil.T("UI2.FilterAny") : LevyUtil.T("Logistics.Kind" + Cat)));
			_typeBtn.Lit = _idx != 0; _clearBtn.Enabled = Active;
		}

		public bool Matches(string name, Item it) => ItemFilter.NameMatches(name, _term) && ItemFilter.Matches(it, Cat);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_search.IsWritingText && Main.mouseLeft && Main.mouseLeftRelease && !_box.ContainsPoint(Main.MouseScreen)) _search.ToggleTakingText(); // a click anywhere else ends typing
		}
	}
}
