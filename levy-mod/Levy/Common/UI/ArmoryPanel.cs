using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Armory: scrolling storage grid (12 columns, 10 rows in view, every storage slot reachable: the base ArmoryTE.StorageSize plus any Logistical Center's) plus the issue rows (12 weapons ending in the sidearm slot, 4 armor sets, 6 ammo, 2 potions),
	/// "Sort", "Deposit junk" and "Loot all". Single player edits the live tile-entity arrays; a multiplayer client sends
	/// ArmorySlotSet for each changed slot.
	/// A search box, a rarity-swatch strip and a type button narrow the STORAGE grid to the matching items, packed together with no gaps. The filters are view-only:
	/// every shown cell still points at its real <see cref="ArmoryTE.Storage"/> index, and filtering itself never edits an item or sends a packet.
	/// </summary>
	public class ArmoryPanel : UIState
	{
		public ArmoryTE Te { get; private set; }

		private DraggablePanel _panel;
		private UIElement _issue;
		private UIList _grid;
		private UIScrollbar _gridScroll;
		private LevyButton _depositJunk, _lootAll, _sort, _close, _floor, _reforge, _catBtn, _clearBtn;
		private Label _title, _treasury, _count, _noMatch, _capacity;
		/// <summary>The storage array the grid cells are bound to. The array is replaced when Logistical Centers change its size (or a client receives a new length), and the cells must follow.</summary>
		private Item[] _builtStorage, _builtPotions;
		/// <summary>The potion row's elements and top (issue-area coordinates), so the row can be rebuilt alone when its array is replaced.</summary>
		private readonly List<UIElement> _potionEls = new();
		private float _potionTop;
		private int _shownCapacity = -1, _shownCenters = -1;
		private UIPanel _searchBox;
		private UISearchBar _search;
		private RaritySwatch[] _swatches;
		/// <summary>Selected bulk-reforge floor (index into LevyConstants.UI.ReforgeFloorTopFraction); kept while the panel lives.</summary>
		private int _floorIndex;
		private int _payerRefresh;
		private BarracksTE _payer;

		// ---- storage view filters (view-only) ----
		/// <summary>Search terms (the lower-cased query split on spaces; every term must appear in the item name), the rarity toggles (one bit per swatch) and the type button's state (0 = any).</summary>
		private string[] _terms = Array.Empty<string>();
		private uint _rarityMask;
		private int _cat;
		/// <summary>Set by any filter change; the next Update re-collects the matches and resets the scroll.</summary>
		private bool _filterDirty;
		private int _filterTick;
		/// <summary>Real Storage indices the grid currently shows, in order; null while the full, unfiltered grid (every slot, empties included) is showing.</summary>
		private List<int> _shown;
		/// <summary>Item type -> type bucket (0 = not computed yet), so the type filter does not re-classify the same item every refresh.</summary>
		private sbyte[] _catCache;

		private const int CatMelee = 1, CatRanged = 2, CatMagic = 3, CatSummon = 4, CatArmor = 5, CatAccessory = 6, CatAmmo = 7, CatPotion = 8, CatOther = 9, CatCount = 10;
		private static readonly string[] CatKeys = { "UI2.ClassMelee", "UI2.ClassRanged", "UI2.ClassMagic", "UI2.ClassSummon", "UI2.CatArmor", "UI2.CatAccessory", "UI2.CatAmmo", "UI2.CatPotion", "UI2.CatOther" };

		private const int GridLeft = 10, GridTop = LevyConstants.UI.ArmoryGridTop, IssueLeft = 460, IssueWidth = 220, IssueColumns = 6;
		private static int Pitch => LevyConstants.UI.SlotPitch;
		private static float Scale => LevyConstants.UI.SlotScale;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(LevyConstants.UI.ArmoryPanelWidth, 0f); _panel.Height.Set(LevyConstants.UI.ArmoryPanelHeight, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = new Label(LevyUtil.T("UI2.ArmoryTitle"), 1f);
			_title.Left.Set(12f, 0f); _title.Top.Set(10f, 0f);
			_panel.Append(_title);

			_capacity = new Label("", 0.7f);
			_capacity.Left.Set(96f, 0f); _capacity.Top.Set(15f, 0f);
			_panel.Append(_capacity);

			int w = LevyConstants.UI.ArmoryPanelWidth;
			_close = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 26, 26, () => UISystem.Instance.CloseArmory(), LevyUtil.T("UI2.Close"));
			_close.Left.Set(w - 36, 0f); _close.Top.Set(6f, 0f);
			_panel.Append(_close);
			_lootAll = new LevyButton(LevyUtil.T("UI.LootAll"), LevyConstants.UI.ButtonTextScale, 104, 26, LootAll);
			_lootAll.Left.Set(w - 36 - 112, 0f); _lootAll.Top.Set(6f, 0f);
			_panel.Append(_lootAll);
			_depositJunk = new LevyButton(LevyUtil.T("UI.DepositJunk"), LevyConstants.UI.ButtonTextScale, 150, 26, DepositJunk);
			_depositJunk.Left.Set(w - 36 - 112 - 158, 0f); _depositJunk.Top.Set(6f, 0f);
			_panel.Append(_depositJunk);
			_sort = new LevyButton(LevyUtil.T("UI.Sort"), LevyConstants.UI.ButtonTextScale, 62, 26, SortStorage, LevyUtil.T("UI.SortHint"));
			_sort.Left.Set(w - 36 - 112 - 158 - 70, 0f); _sort.Top.Set(6f, 0f);
			_panel.Append(_sort);

			// Bottom bar: bulk reforge. The floor button cycles the preset; the reforge button spends treasury coin rerolling weapon modifiers up to it.
			_floor = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, LevyConstants.UI.ArmoryBarHeight, CycleFloor, LevyUtil.T("UI2.ReforgeFloorHint"));
			_floor.Left.Set(GridLeft, 0f); _floor.Top.Set(LevyConstants.UI.ArmoryBarTop, 0f);
			_panel.Append(_floor);
			_reforge = new LevyButton(LevyUtil.T("UI2.ReforgeAll"), LevyConstants.UI.ButtonTextScale, 150, LevyConstants.UI.ArmoryBarHeight, ReforgeAll, LevyUtil.T("UI2.ReforgeAllHint"));
			_reforge.Left.Set(GridLeft + 158, 0f); _reforge.Top.Set(LevyConstants.UI.ArmoryBarTop, 0f);
			_panel.Append(_reforge);
			_treasury = new Label("", 0.8f);
			_treasury.Left.Set(GridLeft + 158 + 160, 0f); _treasury.Top.Set(LevyConstants.UI.ArmoryBarTop + 4, 0f);
			_panel.Append(_treasury);
			RefreshReforgeBar();
			BuildFilterBar();

			// Storage: one UIList row per grid row (only rows in view are drawn), scrolled by the wheel or the bar beside it.
			_grid = new UIList();
			_grid.Left.Set(GridLeft, 0f); _grid.Top.Set(GridTop, 0f);
			_grid.Width.Set(LevyConstants.UI.ArmoryColumns * Pitch, 0f); _grid.Height.Set(LevyConstants.UI.ArmoryRows * Pitch, 0f);
			_grid.ListPadding = 0f;
			_grid.ManualSortMethod = _ => { };
			_panel.Append(_grid);
			_gridScroll = new UIScrollbar();
			_gridScroll.Left.Set(GridLeft + LevyConstants.UI.ArmoryColumns * Pitch + LevyConstants.UI.ArmoryScrollGap, 0f);
			_gridScroll.Top.Set(GridTop, 0f);
			_gridScroll.Height.Set(LevyConstants.UI.ArmoryRows * Pitch, 0f);
			_panel.Append(_gridScroll);
			_grid.SetScrollbar(_gridScroll);

			_issue = new UIElement();
			_issue.Left.Set(IssueLeft, 0f); _issue.Top.Set(GridTop, 0f);
			_issue.Width.Set(IssueWidth, 0f); _issue.Height.Set(LevyConstants.UI.ArmoryRows * Pitch, 0f);
			_panel.Append(_issue);

			Append(_panel);
		}

		/// <summary>Point the panel at a tile entity and rebuild every slot element against its arrays.</summary>
		public void Bind(ArmoryTE te)
		{
			Te = te;
			if (_grid == null) return;
			float sh = Main.screenHeight / Main.UIScale;
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, sh - LevyConstants.UI.ArmoryPanelHeight - 10f)), 0f);
			_grid.Clear();
			_issue.RemoveAllChildren();
			_potionEls.Clear();
			_shown = null;
			ResetFilters();
			_shownCapacity = _shownCenters = -1;
			if (te == null) return;

			// A fresh open shows every storage slot (no search, no filters).
			BuildGrid();

			float y = 0f;
			AddLabel(LevyUtil.T("UI.IssueWeapons"), y); y += 18f;
			int weaponRows = (te.IssueWeapons.Length + IssueColumns - 1) / IssueColumns;
			for (int i = 0; i < te.IssueWeapons.Length; i++)
			{
				bool sidearm = i == ArmoryTE.SidearmSlot;
				var slot = MakeSlot(te.IssueWeapons, i, 1, IsWeaponShaped, sidearm ? LevyUtil.T("UI2.Sidearm") : null);
				slot.Left.Set((i % IssueColumns) * Pitch, 0f); slot.Top.Set(y + (i / IssueColumns) * Pitch, 0f);
				_issue.Append(slot);
				if (sidearm)
				{
					var side = new Label(LevyUtil.T("UI2.Sidearm"), 0.6f);
					side.Left.Set((i % IssueColumns) * Pitch, 0f); side.Top.Set(y + (i / IssueColumns) * Pitch + Pitch - 2f, 0f);
					_issue.Append(side);
				}
			}
			y += weaponRows * Pitch + 16f;

			// Armor sets sit side by side, two sets (head, body, legs each) per row of the issue grid.
			AddLabel(LevyUtil.T("UI.IssueArmor"), y); y += 18f;
			int setsPerRow = Math.Max(1, IssueColumns / ArmoryTE.ArmorParts);
			int setCount = te.IssueArmor.Length / ArmoryTE.ArmorParts;
			for (int set = 0; set < setCount; set++)
			{
				for (int part = 0; part < ArmoryTE.ArmorParts; part++)
				{
					int idx = set * ArmoryTE.ArmorParts + part, p = part;
					var slot = MakeSlot(te.IssueArmor, idx, 2, it => IsArmorPart(it, p), null);
					slot.Left.Set(((set % setsPerRow) * ArmoryTE.ArmorParts + part) * Pitch, 0f);
					slot.Top.Set(y + (set / setsPerRow) * Pitch, 0f);
					_issue.Append(slot);
				}
			}
			y += ((setCount + setsPerRow - 1) / setsPerRow) * Pitch + 6f;

			AddLabel(LevyUtil.T("UI.IssueAmmo"), y); y += 18f;
			int ammoRows = (te.IssueAmmo.Length + IssueColumns - 1) / IssueColumns;
			for (int i = 0; i < te.IssueAmmo.Length; i++)
			{
				var slot = MakeSlot(te.IssueAmmo, i, 3, it => it.ammo > 0 && !it.IsACoin, null);
				slot.Left.Set((i % IssueColumns) * Pitch, 0f); slot.Top.Set(y + (i / IssueColumns) * Pitch, 0f);
				_issue.Append(slot);
			}
			y += ammoRows * Pitch + 6f;

			AddLabel(LevyUtil.T("UI.IssuePotions"), y); y += 18f;
			_potionTop = y;
			BuildPotionRow();
			Recalculate();
			_gridScroll.ViewPosition = 0f;
		}

		/// <summary>
		/// (Re)builds the potion row: slot 0 healing / mana, every later slot a buff slot. The Guide to Firepower adds buff slots, which replaces the array, so this runs again then.
		/// The row wraps after IssueColumns slots (it is never longer than that today).
		/// </summary>
		private void BuildPotionRow()
		{
			foreach (var el in _potionEls) el.Remove();
			_potionEls.Clear();
			_builtPotions = Te.IssuePotions;
			for (int i = 0; i < _builtPotions.Length; i++)
			{
				bool healing = i == 0;
				float x = (i % IssueColumns) * Pitch, y = _potionTop + (i / IssueColumns) * (Pitch + 12f);
				var slot = MakeSlot(_builtPotions, i, 4, healing ? (Func<Item, bool>)(it => it.healLife > 0 || it.healMana > 0) : (it => it.buffType > 0 && it.consumable), LevyUtil.T(healing ? "UI2.PotionHealing" : "UI2.PotionBuff"));
				slot.Left.Set(x, 0f); slot.Top.Set(y, 0f);
				_issue.Append(slot); _potionEls.Add(slot);
				var tag = new Label(LevyUtil.T(healing ? "UI2.PotionHealing" : "UI2.PotionBuff"), 0.6f);
				tag.Left.Set(x, 0f); tag.Top.Set(y + Pitch - 2f, 0f);
				_issue.Append(tag); _potionEls.Add(tag);
			}
			_issue.Recalculate();
		}

		/// <summary>
		/// (Re)builds the storage grid from <see cref="_shown"/>: null = every slot of <see cref="ArmoryTE.Storage"/> in order (empties included); otherwise only those real indices, packed in order.
		/// Rows of ArmoryColumns; the list culls the rows that are scrolled out of view. Each cell is bound to its REAL index, so a drag, click or packet always acts on the item the cell shows.
		/// </summary>
		private void BuildGrid()
		{
			_grid.Clear();
			if (Te == null) return;
			var arr = Te.Storage;
			_builtStorage = arr;
			int cols = LevyConstants.UI.ArmoryColumns;
			int count = _shown?.Count ?? arr.Length;
			var rows = new List<UIElement>((count + cols - 1) / cols);
			for (int start = 0; start < count; start += cols)
			{
				var row = new UIElement();
				row.Width.Set(cols * Pitch, 0f); row.Height.Set(Pitch, 0f);
				for (int c = 0; c < cols && start + c < count; c++)
				{
					int real = _shown == null ? start + c : _shown[start + c];
					var slot = MakeSlot(arr, real, 0, null, null);
					slot.Left.Set(c * Pitch, 0f);
					row.Append(slot);
				}
				rows.Add(row);
			}
			_grid.AddRange(rows);
		}

		private void AddLabel(string text, float y)
		{
			var l = new Label(text, 0.8f);
			l.Left.Set(0f, 0f); l.Top.Set(y, 0f);
			_issue.Append(l);
		}

		private ItemSlotElement MakeSlot(Item[] arr, int index, byte region, Func<Item, bool> accepts, string emptyHint)
		{
			var slot = new ArmorySlot(arr, index, Scale) { Accepts = accepts, EmptyHint = emptyHint };
			slot.OnChanged = (a, i) => { if (Te != null) UIActions.SendArmorySlot(Te, region, i); };
			return slot;
		}

		/// <summary>
		/// An armory cell. The UI's hover flag only moves when the UI is updated, but the grid re-lays itself out (scroll, rebuild) when it is drawn, so for a frame the flag can still name a cell
		/// the pointer has already left. Such a cell is drawn plain (no tooltip, no click handling), so the tooltip and the click always belong to the cell under the pointer.
		/// </summary>
		private sealed class ArmorySlot : ItemSlotElement
		{
			private readonly Item[] _items;
			private readonly int _at;
			private readonly float _scale;

			public ArmorySlot(Item[] arr, int index, float scale) : base(arr, index, scale) { _items = arr; _at = index; _scale = scale; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (!IsMouseHovering || ContainsPoint(Main.MouseScreen)) { base.DrawSelf(spriteBatch); return; }
				if (_items == null || _at < 0 || _at >= _items.Length) return;
				_items[_at] ??= new Item();
				float oldScale = Main.inventoryScale;
				Main.inventoryScale = _scale;
				Item inv = _items[_at];
				ItemSlot.Draw(spriteBatch, ref inv, Context, GetDimensions().Position());
				_items[_at] = inv;
				Main.inventoryScale = oldScale;
			}
		}

		// ------------------------------------------------------------------ search + filters (view-only)
		private bool Filtering => _terms.Length > 0 || _rarityMask != 0 || _cat != 0;

		/// <summary>The search box (header row) and the filter row: rarity swatches, the type cycle button, Clear and the shown count.</summary>
		private void BuildFilterBar()
		{
			int sortLeft = (int)_sort.Left.Pixels;
			_searchBox = new UIPanel();
			_searchBox.SetPadding(0);
			_searchBox.Left.Set(LevyConstants.UI.ArmorySearchLeft, 0f); _searchBox.Top.Set(6f, 0f);
			_searchBox.Width.Set(sortLeft - LevyConstants.UI.ArmorySearchGap - LevyConstants.UI.ArmorySearchLeft, 0f);
			_searchBox.Height.Set(LevyConstants.UI.ArmorySearchHeight, 0f);
			_searchBox.BackgroundColor = new Color(20, 26, 52) * 0.9f;
			_search = new UISearchBar(Language.GetText("Mods.Levy.UI2.ArmorySearch"), LevyConstants.UI.ButtonTextScale + 0.05f);
			_search.Left.Set(LevyConstants.UI.ArmorySearchInset, 0f);
			_search.Width.Set(-2f * LevyConstants.UI.ArmorySearchInset, 1f); _search.Height.Set(0f, 1f);
			_search.OnContentsChanged += OnSearchChanged;
			_search.OnCanceledTakingInput += () => _search.SetContents("", true); // Escape drops the query
			_search.SetContents("", true);
			_searchBox.OnLeftClick += (evt, el) => { if (!_search.IsWritingText) _search.ToggleTakingText(); };
			_searchBox.Append(_search);
			_panel.Append(_searchBox);

			// Rarity swatches: one per listed rarity, plus one for every other (modded) rarity. Each toggles its bit in _rarityMask.
			var order = LevyConstants.UI.ArmoryRarityOrder;
			int size = LevyConstants.UI.RaritySwatchSize, step = size + LevyConstants.UI.RaritySwatchGap;
			float swY = LevyConstants.UI.ArmoryFilterTop + (LevyConstants.UI.ArmoryFilterHeight - size) / 2f;
			_swatches = new RaritySwatch[order.Length + 1];
			for (int i = 0; i < _swatches.Length; i++)
			{
				int bit = i;
				bool other = i == order.Length;
				int rare = other ? 0 : order[i];
				var sw = new RaritySwatch(size)
				{
					Hint = LevyUtil.T("UI2.RarityHint", LevyUtil.T("UI2." + LevyConstants.UI.ArmoryRarityNameKey[i]).ToLowerInvariant()),
					Glyph = other ? "+" : "",
					ColorOf = other ? () => LevyConstants.UI.ArmoryRarityOtherColor : () => RarityColor(rare),
					OnToggle = () => { _rarityMask ^= 1u << bit; FilterChanged(); },
				};
				sw.Left.Set(GridLeft + i * step, 0f); sw.Top.Set(swY, 0f);
				_swatches[i] = sw;
				_panel.Append(sw);
			}
			float x = GridLeft + _swatches.Length * step - LevyConstants.UI.RaritySwatchGap + LevyConstants.UI.ArmoryFilterGap;

			_catBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, LevyConstants.UI.ArmoryCategoryButtonWidth, LevyConstants.UI.ArmoryFilterHeight, null, LevyUtil.T("UI2.ArmoryCategoryHint"));
			_catBtn.OnClick = () => StepCategory(1);
			_catBtn.OnAltClick = () => StepCategory(-1);
			_catBtn.Left.Set(x, 0f); _catBtn.Top.Set(LevyConstants.UI.ArmoryFilterTop, 0f);
			_panel.Append(_catBtn);
			x += LevyConstants.UI.ArmoryCategoryButtonWidth + LevyConstants.UI.ArmoryFilterGap;

			_clearBtn = new LevyButton(LevyUtil.T("UI2.ArmoryClear"), LevyConstants.UI.ButtonTextScale, LevyConstants.UI.ArmoryClearButtonWidth, LevyConstants.UI.ArmoryFilterHeight, ClearFilters, LevyUtil.T("UI2.ArmoryClearHint"));
			_clearBtn.Left.Set(x, 0f); _clearBtn.Top.Set(LevyConstants.UI.ArmoryFilterTop, 0f);
			_panel.Append(_clearBtn);
			x += LevyConstants.UI.ArmoryClearButtonWidth + LevyConstants.UI.ArmoryFilterGap;

			_count = new Label("", 0.75f);
			_count.Left.Set(x, 0f); _count.Top.Set(LevyConstants.UI.ArmoryFilterTop + 5, 0f);
			_panel.Append(_count);

			_noMatch = new Label("", 0.85f);
			_noMatch.Left.Set(GridLeft + 8, 0f); _noMatch.Top.Set(GridTop + 8, 0f);
			_panel.Append(_noMatch);
			RefreshFilterWidgets();
		}

		private void OnSearchChanged(string text)
		{
			_terms = string.IsNullOrWhiteSpace(text) ? Array.Empty<string>() : text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			FilterChanged();
		}

		private void StepCategory(int dir)
		{
			_cat = (_cat + dir + CatCount) % CatCount;
			FilterChanged();
		}

		/// <summary>Search and every filter off. The grid goes back to the full view on the next Update.</summary>
		private void ClearFilters()
		{
			_rarityMask = 0; _cat = 0;
			if (_search != null) _search.SetContents("", true);
			FilterChanged();
		}

		/// <summary>Panel (re)opened: start from the full grid, with the search box idle and empty.</summary>
		private void ResetFilters()
		{
			_rarityMask = 0; _cat = 0; _filterTick = 0;
			if (_search != null)
			{
				if (_search.IsWritingText) _search.ToggleTakingText();
				_search.SetContents("", true);
			}
			_terms = Array.Empty<string>();
			_filterDirty = false;
			RefreshFilterWidgets();
			_count?.SetText(""); _noMatch?.SetText("");
		}

		private void FilterChanged()
		{
			_filterDirty = true;
			RefreshFilterWidgets();
		}

		/// <summary>Swatch lit / dimmed state, the type button caption and the Clear button.</summary>
		private void RefreshFilterWidgets()
		{
			if (_swatches == null) return;
			for (int i = 0; i < _swatches.Length; i++)
			{
				bool on = (_rarityMask & (1u << i)) != 0;
				_swatches[i].On = on;
				_swatches[i].Dim = _rarityMask != 0 && !on;
			}
			_catBtn.Set(LevyUtil.T("UI2.ArmoryCategory", _cat == 0 ? LevyUtil.T("UI2.FilterAny") : LevyUtil.T(CatKeys[_cat - 1])), true);
			_catBtn.Lit = _cat != 0;
			_clearBtn.Enabled = Filtering;
		}

		/// <summary>Called every Update. A filter change re-collects at once and resets the scroll; while a filter is on it re-checks every few ticks so a taken, swapped or server-changed item drops out (or in).</summary>
		private void UpdateFilterView()
		{
			if (_search != null && _search.IsWritingText && Main.mouseLeft && Main.mouseLeftRelease && !_searchBox.ContainsPoint(Main.MouseScreen))
				_search.ToggleTakingText(); // a click anywhere else ends typing
			if (Te == null || _grid == null) return;
			if (_filterDirty) { _filterDirty = false; _filterTick = LevyConstants.UI.ArmoryFilterRefreshTicks; RefreshGrid(true); }
			else if (Filtering && --_filterTick <= 0) { _filterTick = LevyConstants.UI.ArmoryFilterRefreshTicks; RefreshGrid(false); }
		}

		/// <summary>
		/// Called by the UI system just BEFORE this panel's input is processed each frame. Logistical Centers replace the storage array when they change its size (and a client replaces it when the server
		/// sends a new length); a cell still bound to the old array would show stale items and swallow whatever the player dropped on it, so the grid is rebuilt against the new one first. The potion row is replaced the same way when Firepower adds buff slots.
		/// </summary>
		public void RefreshIfStorageReplaced()
		{
			if (Te == null || _grid == null) return;
			if (!ReferenceEquals(_builtPotions, Te.IssuePotions)) BuildPotionRow(); // Firepower grew the buff slots
			if (ReferenceEquals(_builtStorage, Te.Storage)) return;
			_shown = Filtering ? Collect() : null;
			float pos = _gridScroll.ViewPosition;
			BuildGrid();
			_grid.Recalculate();
			_gridScroll.ViewPosition = pos;
		}

		/// <summary>Re-collects the matching indices and rebuilds the grid only when they differ from what is shown.</summary>
		private void RefreshGrid(bool resetScroll)
		{
			List<int> want = Filtering ? Collect() : null;
			if (want == null) { _count.SetText(""); _noMatch.SetText(""); }
			else
			{
				int stored = 0;
				foreach (var it in Te.Storage) if (it != null && !it.IsAir) stored++;
				_count.SetText(LevyUtil.T("UI2.ArmoryShown", want.Count, stored));
				_noMatch.SetText(want.Count == 0 ? LevyUtil.T("UI2.ArmoryNoMatch") : "");
			}
			bool same = want == null ? _shown == null : _shown != null && want.SequenceEqual(_shown);
			float pos = resetScroll ? 0f : _gridScroll.ViewPosition;
			if (!same)
			{
				_shown = want;
				BuildGrid();
				_grid.Recalculate(); // refreshes the scrollbar's view size to the new row count; its position then clamps into range
			}
			_gridScroll.ViewPosition = pos;
		}

		/// <summary>Real Storage indices of every item that passes the search, the rarity toggles and the type filter, in storage order.</summary>
		private List<int> Collect()
		{
			var list = new List<int>();
			var arr = Te.Storage;
			for (int i = 0; i < arr.Length; i++) if (Matches(arr[i])) list.Add(i);
			return list;
		}

		private bool Matches(Item it)
		{
			if (it == null || it.IsAir || it.stack <= 0) return false;
			if (_cat != 0 && CategoryCached(it) != _cat) return false;
			if (_rarityMask != 0 && (_rarityMask & (1u << RarityBucket(it))) == 0) return false;
			if (_terms.Length > 0)
			{
				string name = it.AffixName();
				foreach (string t in _terms) if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) return false;
			}
			return true;
		}

		/// <summary>Swatch index of an item: its position in <see cref="LevyConstants.UI.ArmoryRarityOrder"/> (Master / Expert flags win over the number), or the last swatch for any other rarity.</summary>
		private static int RarityBucket(Item it)
		{
			int rare = it.master ? ItemRarityID.Master : it.expert ? ItemRarityID.Expert : it.rare;
			int i = Array.IndexOf(LevyConstants.UI.ArmoryRarityOrder, rare);
			return i >= 0 ? i : LevyConstants.UI.ArmoryRarityOrder.Length;
		}

		/// <summary>The swatch colour for a rarity value: vanilla's rarity colour table (so modded rarities match too), white for 0, and the animated Expert (rainbow) and Master colours the table lacks.</summary>
		private static Color RarityColor(int rare)
		{
			if (rare == ItemRarityID.Expert) return Main.DiscoColor;
			if (rare == ItemRarityID.Master) return new Color(255, (int)(Main.masterColor * LevyConstants.UI.MasterRarityGreenScale), 0);
			if (rare == ItemRarityID.White) return Color.White;
			return ItemRarity.GetColor(rare);
		}

		private int CategoryCached(Item it)
		{
			int n = ItemLoader.ItemCount;
			if (it.type <= 0 || it.type >= n) return CategoryOf(it);
			if (_catCache == null || _catCache.Length < n) _catCache = new sbyte[n];
			int c = _catCache[it.type];
			if (c == 0) _catCache[it.type] = (sbyte)(c = CategoryOf(it));
			return c;
		}

		/// <summary>Type bucket (CatMelee .. CatOther): armor, then accessory, ammo, a weapon by its damage class, a potion, anything else.</summary>
		private static int CategoryOf(Item it)
		{
			if (IsArmor(it)) return CatArmor;
			if (it.accessory) return CatAccessory;
			if (it.ammo > 0 && !it.IsACoin) return CatAmmo;
			if (it.damage > 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.IsACoin)
			{
				var dc = WeaponClassifier.ItemDamageClass(it);
				if (dc == DamageClass.Melee) return CatMelee;
				if (dc == DamageClass.Ranged) return CatRanged;
				if (dc == DamageClass.Magic) return CatMagic;
				if (dc == DamageClass.Summon) return CatSummon;
				return CatOther;
			}
			if (it.consumable && (it.buffType > 0 || it.healLife > 0 || it.healMana > 0)) return CatPotion;
			return CatOther;
		}

		// ------------------------------------------------------------------ classification
		/// <summary>Weapon-shaped: deals damage, is not a tool, armor, accessory, consumable or coin. Soldiers decide what they can use.</summary>
		public static bool IsWeaponShaped(Item it) => it != null && !it.IsAir && it.damage > 0 && !it.accessory && !it.consumable && it.headSlot < 0 && it.bodySlot < 0 && it.legSlot < 0
			&& it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.IsACoin;
		public static bool IsArmorPart(Item it, int part) => it != null && !it.IsAir && !it.vanity && (part == 0 ? it.headSlot >= 0 : part == 1 ? it.bodySlot >= 0 : it.legSlot >= 0);
		public static bool IsArmor(Item it) => it != null && !it.IsAir && !it.vanity && (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0);
		public static bool IsJunk(Item it)
		{
			if (it == null || it.IsAir || it.stack <= 0 || it.favorited) return false;
			if (IsWeaponShaped(it)) return true;
			if (IsArmor(it)) return true;
			if (it.accessory && !it.vanity) return true;
			if (it.ammo > 0 && !it.IsACoin) return true;
			return false;
		}

		// ------------------------------------------------------------------ bulk buttons
		/// <summary>Moves every non-favourited weapon, armor piece, accessory and ammo stack from the main inventory (not the hotbar) into storage.</summary>
		private void DepositJunk()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			var inv = Main.LocalPlayer.inventory;
			var before = Snapshot(Te.Storage);
			bool any = false;
			for (int i = LevyConstants.UI.InventoryMainStart; i < LevyConstants.UI.InventoryMainEnd && i < inv.Length; i++)
			{
				var it = inv[i];
				if (!IsJunk(it)) continue;
				var left = Te.Deposit(it);
				if (left == null || left.IsAir || left.stack <= 0) { inv[i] = new Item(); any = true; }
				else { if (!ReferenceEquals(left, it) || left.stack != it.stack) any = true; inv[i] = left; }
			}
			SendChanged(before, Te.Storage);
			if (any) SoundEngine.PlaySound(SoundID.Grab);
		}

		/// <summary>Takes everything in storage into the inventory (issue rows stay); with a search or filter on, only the shown items.</summary>
		private void LootAll()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			var player = Main.LocalPlayer;
			var before = Snapshot(Te.Storage);
			bool any = false;
			var visible = Filtering ? Collect() : null; // with a search / filter on, only what the grid shows
			int count = visible?.Count ?? Te.Storage.Length;
			for (int k = 0; k < count; k++)
			{
				int i = visible == null ? k : visible[k];
				var it = Te.Storage[i];
				if (it == null || it.IsAir) continue;
				var rest = player.GetItem(Main.myPlayer, it, GetItemSettings.LootAllSettings);
				Te.Storage[i] = rest ?? new Item();
				if (SlotSnap.Differs(before, Te.Storage, i)) any = true;
			}
			SendChanged(before, Te.Storage);
			if (any) SoundEngine.PlaySound(SoundID.Grab);
		}

		/// <summary>Groups and packs the storage (weapons, armor, accessories, ammo, potions, the rest; partial stacks merged). Single player edits live; a multiplayer client asks the server.</summary>
		private void SortStorage()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			UIActions.SortArmory(Te);
			SoundEngine.PlaySound(SoundID.Grab);
		}

		private void CycleFloor()
		{
			_floorIndex = (_floorIndex + 1) % LevyConstants.UI.ReforgeFloorTopFraction.Length;
			RefreshReforgeBar();
		}

		/// <summary>Bulk reforge: every weapon below the floor is rerolled (a paid roll each, from the linked barracks treasury) until it meets it. Single player runs it; a multiplayer client asks the server.</summary>
		private void ReforgeAll()
		{
			if (Te == null || !UIActions.IsLive(Te)) return;
			UIActions.ReforgeArmory(Te, _floorIndex);
			SoundEngine.PlaySound(SoundID.Item37);
		}

		private void RefreshReforgeBar()
		{
			if (_floor == null) return;
			_floor.Set(LevyUtil.T("UI2.ReforgeFloor", LevyUtil.T("UI2." + LevyConstants.UI.ReforgeFloorNameKey[_floorIndex])), true);
			_treasury.SetText(_payer == null ? "" : LevyUtil.T("UI.Treasury", LevyUtil.Coins(_payer.Treasury)));
			_reforge.Enabled = _payer != null && _payer.Treasury > 0;
		}

		private static SlotSnap[] Snapshot(Item[] arr) { var s = new SlotSnap[arr.Length]; for (int i = 0; i < arr.Length; i++) s[i] = new SlotSnap(arr[i]); return s; }

		private void SendChanged(SlotSnap[] before, Item[] arr)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || Te == null) return;
			for (int i = 0; i < arr.Length; i++) if (SlotSnap.Differs(before, arr, i)) UIActions.SendArmorySlot(Te, 0, i);
		}

		/// <summary>
		/// Runs before the panel's cells are drawn. The vanilla inventory and crafting list sit under this panel and have already put whatever they were hovering into the shared tooltip slot;
		/// with the pointer on the panel only the panel's own cells may set it.
		/// </summary>
		/// <summary>Shows the storage size and how much of it the linked Logistical Centers provide. Rebuilds the text only when a number changes.</summary>
		private void UpdateCapacityLabel()
		{
			if (Te == null || _capacity == null) return;
			int slots = Te.Storage.Length, centers = Te.LinkedCenters;
			if (slots == _shownCapacity && centers == _shownCenters) return;
			_shownCapacity = slots; _shownCenters = centers;
			int extra = Math.Max(0, slots - ArmoryTE.StorageSize);
			_capacity.SetText(centers <= 0 ? LevyUtil.T("UI2.ArmoryCapacity", slots)
				: centers == 1 ? LevyUtil.T("UI2.ArmoryCapacityCenter", slots, extra)
				: LevyUtil.T("UI2.ArmoryCapacityCenters", slots, extra, centers));
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (_panel != null && _panel.ContainsPoint(Main.MouseScreen)) { Main.HoverItem = new Item(); Main.hoverItemName = ""; }
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Te != null && !UIActions.IsLive(Te)) { UISystem.Instance.CloseArmory(); return; }
			bool anyStorage = false;
			if (Te != null) foreach (var it in Te.Storage) if (it != null && !it.IsAir) { anyStorage = true; break; }
			_lootAll.Enabled = anyStorage;
			_sort.Enabled = anyStorage;
			UpdateFilterView();
			UpdateCapacityLabel();
			if (Te != null && --_payerRefresh <= 0) { _payerRefresh = 20; _payer = Te.LinkedBarracks(); }
			else if (Te == null) _payer = null;
			RefreshReforgeBar();
		}
	}
}
