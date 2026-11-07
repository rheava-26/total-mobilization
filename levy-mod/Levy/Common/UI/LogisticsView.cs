using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;
using Levy.Common.Logistics;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// The Logistics tab of the armory panel (its own file; <see cref="ArmoryPanel"/> only hosts it). Four pages: ORDERS (craft-into-armory orders: sets and weapons split by share, bucket or random, auto-buy of short materials),
	/// BUY (standing buys from the shopkeepers in reach), SELL (drag-select, category and tier selection, sold to the happiest shopkeeper), FIND (pick an item for the Seeker and Spectator potions, from the Magic Storage list or the armory).
	/// The materials checklist hangs off the side of the panel (<see cref="ChecklistPanel"/>) while the Orders page is showing. Every action goes through <see cref="LogisticsActions"/>, so on a multiplayer client the server does the work.
	/// </summary>
	public class LogisticsView : UIElement
	{
		private static string L(string key, params object[] a) => LevyUtil.T("Logistics." + key, a);
		private const int PageTop = 32, ColL = 232, ColR = 244;

		private readonly ArmoryPanel _owner;
		private ArmoryTE Te => _owner.Te;
		private readonly LevyButton[] _pageBtn = new LevyButton[4];
		private readonly UIElement[] _page = new UIElement[4];
		private int _cur;
		private readonly ChoicePopup _popup = new();
		public readonly ChecklistPanel Checklist = new();
		public bool ChecklistVisible => _cur == 0 && !_craftMode;

		// ---- orders page
		private readonly UIList _orderList = new(); private readonly UIScrollbar _orderBar = new();
		private readonly UIList _lineList = new(); private readonly UIScrollbar _lineBar = new();
		private LogisticsOrder _draft = new(); private int _selectedId; private bool _selectLatest;
		private int _seenVersion = -1, _lineHash;
		private LevyButton _modeBtn, _buyBtn, _pauseBtn, _deleteBtn, _saveBtn;
		private NumberField _totalField, _budgetField;
		private Label _statusLbl, _rulesLbl, _note;

		// ---- craft browser (the second pane of the orders page)
		private bool _craftMode; private UIElement _ordersPane, _craftPane, _detailHost; private LevyButton _modeOrdersBtn, _modeCraftBtn, _viewCan, _viewHas, _viewAll, _craftBtn, _makeOrderBtn;
		private ItemFilterBar _craftFilter; private RecipeView _view = RecipeView.CanCraft;
		private readonly UIList _recipeList = new(); private readonly UIScrollbar _recipeBar = new();
		private List<RecipeEntry> _recipes = new(); private int _recipeTick = -1000, _recipeVersion = -1; private bool _recipeRescan = true, _recipeRebuild = true; private string _recipeSig = "";
		private Recipe _selRecipe; private int _craftCount = 1, _selMax; private NumberField _craftField; private Label _craftCan, _recipeNote;
		private readonly List<UIElement> _detailEls = new(); private string[] _selLines = Array.Empty<string>(); private string _selBlocker = "";

		// ---- buy page
		private readonly BuyRule _rule = new() { Kind = BuyRule.KindAnyGun, Count = 1, Budget = 50000 };
		private LevyButton _kindBtn, _buyNowBtn, _addRuleBtn;
		private Label _buyInfo, _offersHdr; private UIElement _itemCellHost; private ItemFilterBar _buyFilter;
		private readonly UIList _offerList = new(); private readonly UIScrollbar _offerBar = new();
		private int _offerTick = -1000;

		// ---- sell page
		private ItemFilterBar _sellFilter; private SellGrid _grid; private LevyButton _catBtn, _tierBtn, _pickBtn, _clearBtn, _shimmerBtn, _sellBtn; private Label _sellInfo;
		private int _catIdx, _tierIdx = -1; private bool _shimmer; private int _sellTick; private bool _sellDirty = true;

		// ---- find page
		private readonly UIGrid _findGrid = new(); private readonly UIScrollbar _findBar = new(); private Label _findLbl; private ItemFilterBar _findFilter;
		private List<int> _findAll = new(); private bool _findDirty = true;

		public LogisticsView(ArmoryPanel owner)
		{
			_owner = owner;
			Width.Set(LevyConstants.UI.ArmoryPanelWidth - 20, 0f); Height.Set(LevyConstants.UI.ArmoryBarTop - 36 - 4, 0f);
			string[] keys = { "PageOrders", "PageBuy", "PageSell", "PageFind" };
			for (int i = 0; i < 4; i++)
			{
				int p = i;
				_pageBtn[i] = new LevyButton(L(keys[i]), LevyConstants.UI.ButtonTextScale, 110, 24, () => ShowPage(p), L(keys[i] + "Hint"));
				_pageBtn[i].Left.Set(i * 116, 0f); Append(_pageBtn[i]);
				_page[i] = new UIElement(); _page[i].Top.Set(PageTop, 0f); _page[i].Width.Set(Width.Pixels, 0f); _page[i].Height.Set(Height.Pixels - PageTop, 0f);
			}
			_note = new Label(L("Rule"), 0.7f); _note.Left.Set(4 * 116 + 6, 0f); _note.Top.Set(4, 0f); Append(_note);
			BuildOrders(); BuildBuy(); BuildSell(); BuildFind();
			FindTarget.Chooser = (cb, cands) => { ShowPage(3); _findPick = cb; };
			ShowPage(0);
		}

		private Action<int> _findPick;

		private void ShowPage(int p)
		{
			if (_popup.Open) _popup.Remove();
			StopTyping();
			for (int i = 0; i < 4; i++) { if (HasChild(_page[i])) RemoveChild(_page[i]); _pageBtn[i].Lit = i == p; }
			_cur = p; Append(_page[p]);
			if (p == 1) _offerTick = -1000;
			if (p == 2) _sellDirty = true;
			if (p == 3) _findDirty = true;
			Recalculate();
		}

		/// <summary>Ends typing in whichever search box has focus.</summary>
		public void StopTyping() { _craftFilter?.StopTyping(); _craftField?.StopTypingIfAny(); _buyFilter?.StopTyping(); _sellFilter?.StopTyping(); _findFilter?.StopTyping(); }

		/// <summary>Called when the panel points at an armory.</summary>
		public void Bind()
		{
			StopTyping();
			_recipeRescan = true; _recipeSig = "";
			_selectedId = 0; _seenVersion = -1; _draft = NewDraft(); _grid?.Clear(); _sellDirty = true; _findDirty = true; _offerTick = -1000;
			RebuildOrderList(); RebuildLines();
		}

		private LogisticsOrder NewDraft()
		{
			var o = new LogisticsOrder { ArmoryId = Te?.ID ?? -1, Total = 30, Mode = OrderMode.Bucket, ShortfallBudget = 10000 * 5L };
			return o;
		}

		private static LogisticsOrder Clone(LogisticsOrder o) { var ms = new MemoryStream(); using (var w = new BinaryWriter(ms)) o.Write(w, true); return LogisticsOrder.Read(new BinaryReader(new MemoryStream(ms.ToArray()))); }

		private LogisticsOrder Stored => _selectedId == 0 ? null : OrderBook.Find(_selectedId);

		// ====================================================================================== ORDERS
		private void BuildOrders()
		{
			_ordersPane = new UIElement(); _craftPane = new UIElement();
			foreach (var pane in new[] { _ordersPane, _craftPane }) { pane.Top.Set(30, 0f); pane.Width.Set(Width.Pixels, 0f); pane.Height.Set(Height.Pixels - PageTop - 30, 0f); }
			_modeOrdersBtn = new LevyButton(L("ModeOrders"), LevyConstants.UI.ButtonTextScale, 150, 24, () => SetCraftMode(false), L("ModeOrdersHint"));
			_modeCraftBtn = new LevyButton(L("ModeCraft"), LevyConstants.UI.ButtonTextScale, 150, 24, () => SetCraftMode(true), L("ModeCraftHint")); _modeCraftBtn.Left.Set(156, 0f);
			_page[0].Append(_modeOrdersBtn); _page[0].Append(_modeCraftBtn); _page[0].Append(_ordersPane);
			BuildCraft();
			var pg = _ordersPane;
			var hdr = new Label(L("OrdersHeader"), 0.8f); hdr.Top.Set(0, 0f); pg.Append(hdr);
			_orderList.Top.Set(18, 0f); _orderList.Width.Set(ColL - 16, 0f); _orderList.Height.Set(236, 0f); _orderList.ListPadding = 3f;
			_orderBar.Left.Set(ColL - 12, 0f); _orderBar.Top.Set(18, 0f); _orderBar.Height.Set(236, 0f); _orderList.SetScrollbar(_orderBar);
			pg.Append(_orderList); pg.Append(_orderBar);
			var newBtn = new LevyButton(L("New"), LevyConstants.UI.ButtonTextScale, 70, 24, () => { _selectedId = 0; _draft = NewDraft(); RebuildOrderList(); RebuildLines(); }, L("NewHint")); newBtn.Top.Set(262, 0f); pg.Append(newBtn);
			_pauseBtn = new LevyButton(L("Pause"), LevyConstants.UI.ButtonTextScale, 70, 24, () => { var s = Stored; if (s != null) LogisticsActions.SetPaused(s.Id, !s.Paused); }, L("PauseHint")); _pauseBtn.Left.Set(76, 0f); _pauseBtn.Top.Set(262, 0f); pg.Append(_pauseBtn);
			_deleteBtn = new LevyButton(L("Delete"), LevyConstants.UI.ButtonTextScale, 70, 24, () => { var s = Stored; if (s != null) { LogisticsActions.DeleteOrder(s.Id); _selectedId = 0; _draft = NewDraft(); } }, L("DeleteHint")); _deleteBtn.Left.Set(152, 0f); _deleteBtn.Top.Set(262, 0f); pg.Append(_deleteBtn);
			_statusLbl = new Label("", 0.75f); _statusLbl.Top.Set(292, 0f); pg.Append(_statusLbl);

			float x = ColR;
			var tl = new Label(L("Total"), 0.8f); tl.Left.Set(x, 0f); tl.Top.Set(4, 0f); pg.Append(tl);
			_totalField = new NumberField(130, 1, LogisticsConstants.MaxTotal, 5, true, () => _draft.Total, v => { _draft.Total = (int)v; _draft.Replan(); });
			_totalField.Left.Set(x + 52, 0f); pg.Append(_totalField);
			_modeBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 110, 24, () => { _draft.Mode = _draft.Mode == OrderMode.Bucket ? OrderMode.Random : OrderMode.Bucket; }, L("ModeHint")); _modeBtn.Left.Set(x + 196, 0f); pg.Append(_modeBtn);
			_buyBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, 24, () => { _draft.BuyShortfalls = !_draft.BuyShortfalls; }, L("BuyShortHint")); _buyBtn.Left.Set(x, 0f); _buyBtn.Top.Set(32, 0f); pg.Append(_buyBtn);
			var bl = new Label(L("Budget"), 0.8f); bl.Left.Set(x + 158, 0f); bl.Top.Set(36, 0f); pg.Append(bl);
			_budgetField = new NumberField(120, 0, 9999, 1, true, () => _draft.ShortfallBudget / 10000f, v => _draft.ShortfallBudget = (long)v * 10000L);
			_budgetField.Left.Set(x + 212, 0f); _budgetField.Top.Set(32, 0f); pg.Append(_budgetField);
			var gl = new Label(L("Gold"), 0.75f); gl.Left.Set(x + 336, 0f); gl.Top.Set(36, 0f); pg.Append(gl);

			var ll = new Label(L("Lines"), 0.8f); ll.Left.Set(x, 0f); ll.Top.Set(66, 0f); pg.Append(ll);
			_lineList.Left.Set(x, 0f); _lineList.Top.Set(84, 0f); _lineList.Width.Set(400, 0f); _lineList.Height.Set(166, 0f); _lineList.ListPadding = 3f;
			_lineBar.Left.Set(x + 404, 0f); _lineBar.Top.Set(84, 0f); _lineBar.Height.Set(166, 0f); _lineList.SetScrollbar(_lineBar);
			pg.Append(_lineList); pg.Append(_lineBar);

			var add = new AddTypeCell(t => AddLine(new[] { t })); add.Left.Set(x, 0f); add.Top.Set(256, 0f); pg.Append(add);
			var addLbl = new Label(L("AddItem"), 0.7f); addLbl.Left.Set(x + 40, 0f); addLbl.Top.Set(262, 0f); pg.Append(addLbl);
			var addSet = new LevyButton(L("AddSet"), LevyConstants.UI.ButtonTextScale, 100, 24, AddSetFromCursor, L("AddSetHint")); addSet.Left.Set(x + 150, 0f); addSet.Top.Set(262, 0f); pg.Append(addSet);
			_saveBtn = new LevyButton(L("Save"), LevyConstants.UI.ButtonTextScale, 130, 28, SaveDraft, L("SaveHint")); _saveBtn.Left.Set(x + 270, 0f); _saveBtn.Top.Set(260, 0f); pg.Append(_saveBtn);
			_rulesLbl = new Label("", 0.72f); _rulesLbl.Left.Set(x, 0f); _rulesLbl.Top.Set(296, 0f); pg.Append(_rulesLbl);
		}

		// ====================================================================================== CRAFT BROWSER
		private void SetCraftMode(bool on)
		{
			StopTyping(); _craftMode = on;
			if (on) { if (HasChild(_page[0]) && _page[0].HasChild(_ordersPane)) _page[0].RemoveChild(_ordersPane); if (!_page[0].HasChild(_craftPane)) _page[0].Append(_craftPane); _recipeRescan = true; }
			else { if (_page[0].HasChild(_craftPane)) _page[0].RemoveChild(_craftPane); if (!_page[0].HasChild(_ordersPane)) _page[0].Append(_ordersPane); }
			Recalculate();
		}

		private void BuildCraft()
		{
			var pg = _craftPane;
			_craftFilter = new ItemFilterBar(430, "Mods.Levy.Logistics.RecipeSearch"); _craftFilter.Changed += () => _recipeRebuild = true; pg.Append(_craftFilter);
			_viewCan = new LevyButton(L("ViewCan"), LevyConstants.UI.ButtonTextScale, 72, 24, () => SetView(RecipeView.CanCraft), L("ViewCanHint")); _viewCan.Left.Set(436, 0f); _viewCan.Top.Set(1, 0f); pg.Append(_viewCan);
			_viewHas = new LevyButton(L("ViewHas"), LevyConstants.UI.ButtonTextScale, 104, 24, () => SetView(RecipeView.HasMaterials), L("ViewHasHint")); _viewHas.Left.Set(512, 0f); _viewHas.Top.Set(1, 0f); pg.Append(_viewHas);
			_viewAll = new LevyButton(L("ViewAll"), LevyConstants.UI.ButtonTextScale, 44, 24, () => SetView(RecipeView.All), L("ViewAllHint")); _viewAll.Left.Set(620, 0f); _viewAll.Top.Set(1, 0f); pg.Append(_viewAll);
			_recipeList.Top.Set(34, 0f); _recipeList.Width.Set(430, 0f); _recipeList.Height.Set(300, 0f); _recipeList.ListPadding = 2f;
			_recipeBar.Left.Set(436, 0f); _recipeBar.Top.Set(34, 0f); _recipeBar.Height.Set(300, 0f); _recipeList.SetScrollbar(_recipeBar);
			pg.Append(_recipeList); pg.Append(_recipeBar);
			_detailHost = new UIElement(); _detailHost.Left.Set(470, 0f); _detailHost.Top.Set(34, 0f); _detailHost.Width.Set(200, 0f); _detailHost.Height.Set(300, 0f); pg.Append(_detailHost);
			_craftCan = new Label("", 0.75f); _craftCan.Top.Set(176, 0f); _detailHost.Append(_craftCan);
			var cl = new Label(L("Count"), 0.8f); cl.Top.Set(206, 0f); _detailHost.Append(cl);
			_craftField = new NumberField(130, 1, 9999, 1, true, () => _craftCount, v => _craftCount = (int)v); _craftField.Left.Set(60, 0f); _craftField.Top.Set(202, 0f); _detailHost.Append(_craftField);
			_craftBtn = new LevyButton(L("CraftNow"), LevyConstants.UI.ButtonTextScale, 92, 26, CraftSelected, L("CraftNowHint")); _craftBtn.Top.Set(236, 0f); _detailHost.Append(_craftBtn);
			_makeOrderBtn = new LevyButton(L("MakeOrder"), LevyConstants.UI.ButtonTextScale, 102, 26, MakeOrderFromSelected, L("MakeOrderHint")); _makeOrderBtn.Left.Set(98, 0f); _makeOrderBtn.Top.Set(236, 0f); _detailHost.Append(_makeOrderBtn);
			_recipeNote = new Label("", 0.66f); _recipeNote.Top.Set(270, 0f); _detailHost.Append(_recipeNote);
			SetView(RecipeView.CanCraft);
		}

		private void SetView(RecipeView v) { _view = v; _viewCan.Lit = v == RecipeView.CanCraft; _viewHas.Lit = v == RecipeView.HasMaterials; _viewAll.Lit = v == RecipeView.All; _recipeRebuild = true; }

		private void CraftSelected()
		{
			if (Te == null || _selRecipe == null) return;
			LogisticsActions.CraftNow(Te, _selRecipe, _craftCount);
			CraftBrowser.Version++; SoundEngine.PlaySound(SoundID.Item37);
		}

		private void MakeOrderFromSelected()
		{
			if (Te == null || _selRecipe == null) return;
			int t = _selRecipe.createItem.type;
			var o = NewDraft(); o.Lines.Add(new OrderLine { Types = new[] { t }, Weight = 1 });
			o.Total = Math.Clamp(_craftCount * Math.Max(1, _selRecipe.createItem.stack), 1, LogisticsConstants.MaxTotal); o.Replan();
			o.Name = LevyUtil.T("Logistics.OrderName", ItemName(t));
			_selectedId = 0; _selectLatest = true;
			LogisticsActions.SaveOrder(o);
			SoundEngine.PlaySound(SoundID.MenuTick);
			SetCraftMode(false);
		}

		private static string BlockerText(string b)
		{
			if (b.StartsWith("station:") && int.TryParse(b.Substring(8), out int tile)) return L("StatusStation", StationName(tile));
			if (b.StartsWith("condition:")) return L("StatusCondition", b.Substring(10));
			return "";
		}

		/// <summary>Recounts every recipe against the stock (page open, after a craft, then at most every 30 ticks) and the selected recipe's ingredient lines.</summary>
		private void RescanRecipes()
		{
			_recipeRescan = false; _recipeTick = (int)Main.GameUpdateCount; _recipeVersion = CraftBrowser.Version;
			try
			{
				var pool = CraftBrowser.PoolFor(Te, Main.LocalPlayer);
				_recipes = CraftBrowser.Scan(Te, pool);
				_selLines = Array.Empty<string>(); _selMax = 0; _selBlocker = "";
				if (_selRecipe != null)
				{
					var lines = new List<string>();
					foreach (var ing in Crafting.Ingredients(_selRecipe))
					{
						long have = pool.Count(ing.Types);
						lines.Add(ing.Types.Length > 1 ? L("IngGroup", ItemName(ing.Primary), have, ing.Count) : L("Ing", ItemName(ing.Primary), have, ing.Count));
					}
					_selLines = lines.ToArray(); _selBlocker = Crafting.Blocker(_selRecipe, Te);
					_selMax = CraftBrowser.MaxCrafts(_selRecipe, Te, pool, LogisticsConstants.MaxTotal);
				}
			}
			catch (Exception) { _recipes = new(); }
			_recipeRebuild = true;
		}

		private void RebuildRecipeList()
		{
			_recipeRebuild = false;
			var shown = _recipes.Where(e => CraftBrowser.InView(e, _view) && _craftFilter.Matches(e.Name, e.R.createItem)).OrderByDescending(e => e.CanCraft).ThenBy(e => e.Name).ThenBy(e => e.R.RecipeIndex).ToList();
			int total = shown.Count; shown = shown.Take(150).ToList();
			string sig = _view + "|" + total + "|" + string.Join(",", shown.Select(e => e.R.RecipeIndex + ":" + e.Crafts + (e.CanCraft ? "y" : "n"))) + "|" + (_selRecipe?.RecipeIndex ?? -1) + "|" + _selMax + "|" + string.Join(";", _selLines) + _selBlocker;
			if (sig == _recipeSig) return;
			_recipeSig = sig;
			_recipeList.Clear();
			foreach (var e in shown) { var ee = e; _recipeList.Add(new RecipeRowEl(ee, () => _selRecipe != null && ReferenceEquals(_selRecipe, ee.R), () => { _selRecipe = ee.R; _craftCount = 1; _recipeRescan = true; })); }
			if (shown.Count == 0) _recipeList.Add(new Label(L("NoRecipes"), 0.8f));
			else if (total > shown.Count) _recipeList.Add(new Label(L("MoreRecipes", total - shown.Count), 0.7f));
			RebuildDetail();
		}

		private void RebuildDetail()
		{
			foreach (var c in _detailEls) _detailHost.RemoveChild(c);
			_detailEls.Clear();
			bool has = _selRecipe != null;
			_craftBtn.Enabled = has && _selMax > 0; _makeOrderBtn.Enabled = has; 
			if (!has) { _craftCan.SetText(""); _recipeNote.SetText(L("PickRecipe")); return; }
			int t = _selRecipe.createItem.type;
			var cell = new TypeCell(t, () => { }); _detailHost.Append(cell); _detailEls.Add(cell);
			var nm = new Label(ItemName(t) + (_selRecipe.createItem.stack > 1 ? " x" + _selRecipe.createItem.stack : ""), 0.8f); nm.Left.Set(42, 0f); nm.Top.Set(10, 0f); _detailHost.Append(nm); _detailEls.Add(nm);
			float y = 44; foreach (var ln in _selLines.Take(8)) { var l = new Label(ln, 0.68f); l.Top.Set(y, 0f); _detailHost.Append(l); _detailEls.Add(l); y += 15; }
			_craftCan.SetText(_selBlocker != "" ? BlockerText(_selBlocker) : L("CanMake", _selMax));
			_recipeNote.SetText("");
			_detailHost.Recalculate();
		}

		private void AddLine(int[] types)
		{
			if (_draft.Lines.Count >= LogisticsConstants.MaxLines) return;
			if (_draft.Lines.Any(l => l.Types.SequenceEqual(types))) return;
			_draft.Lines.Add(new OrderLine { Types = types, Weight = 1 }); _draft.Replan(); RebuildLines();
		}

		private static string StemOf(Item it) { var t = (it.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries); return t.Length == 0 ? "" : t.Length >= 3 && t[0] == "Ancient" ? t[0] + " " + t[1] : t[0]; }

		/// <summary>The cursor holds one armor piece; the set is the head, body and legs pieces sharing its name stem and an armor-set bonus.</summary>
		private void AddSetFromCursor()
		{
			var held = Main.mouseItem; if (held == null || held.IsAir) { Main.NewText(L("SetNeedsItem"), 220, 200, 120); return; }
			string stem = StemOf(held);
			int head = -1, body = -1, legs = -1; float hs = float.MaxValue, bs = float.MaxValue, ls = float.MaxValue;
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				var smp = ContentSamples.ItemsByType[t];
				if (smp.vanity || StemOf(smp) != stem) continue;
				// the closest in rarity and value to the held piece wins each slot, so a Hallowed Mask beats a Hallowed Hood only when that is the piece you hold
				float score = t == held.type ? -1f : Math.Abs(smp.rare - held.rare) * 100000f + Math.Abs(smp.value - held.value);
				if (smp.headSlot >= 0 && score < hs) { head = t; hs = score; }
				else if (smp.bodySlot >= 0 && score < bs) { body = t; bs = score; }
				else if (smp.legSlot >= 0 && score < ls) { legs = t; ls = score; }
			}
			var parts = new List<int>(); foreach (int p in new[] { head, body, legs }) if (p > 0) parts.Add(p);
			if (parts.Count == 0 || !parts.Contains(held.type)) { Main.NewText(L("SetNeedsArmor"), 220, 200, 120); return; }
			AddLine(parts.ToArray());
		}

		private void SaveDraft()
		{
			if (Te == null || _draft.Lines.Count == 0) { Main.NewText(L("NeedLines"), 220, 200, 120); return; }
			_draft.ArmoryId = Te.ID; _draft.Replan();
			if (_draft.Name == "") _draft.Name = LevyUtil.T("Logistics.OrderName", ItemName(_draft.Lines[0].Types[0]));
			_selectLatest = _selectedId == 0;
			LogisticsActions.SaveOrder(_draft);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public static string ItemName(int type) => type > 0 && type < ItemLoader.ItemCount ? Lang.GetItemNameValue(type) : "?";

		public static string StatusText(LogisticsOrder o)
		{
			string s = o.Status ?? "";
			if (s.StartsWith("Blocked:station:") && int.TryParse(s.Substring(16), out int tile)) return L("StatusStation", StationName(tile));
			if (s.StartsWith("Blocked:condition:")) return L("StatusCondition", s.Substring(18));
			return s switch { "Done" => L("StatusDone"), "Paused" => L("StatusPaused"), "Waiting" => L("StatusWaiting"), "Working" => L("StatusWorking"), "NoRecipe" => L("StatusNoRecipe"), "NoArmory" => L("StatusNoArmory"), "Error" => L("StatusError"), _ => L("StatusNew") };
		}

		private static string StationName(int tile)
		{
			try { string n = Lang.GetMapObjectName(Terraria.Map.MapHelper.TileToLookup(tile, 0)); if (!string.IsNullOrWhiteSpace(n)) return n; } catch (Exception) { }
			return "#" + tile;
		}

		private void RebuildOrderList()
		{
			_orderList.Clear();
			if (Te == null) return;
			foreach (var o in OrderBook.For(Te.ID))
			{
				var oo = o;
				string text = $"{(string.IsNullOrEmpty(o.Name) ? "#" + o.Id : o.Name)}  {o.Made}/{o.Lines.Sum(l => l.Planned)}";
				var b = new LevyButton(text, 0.8f, ColL - 26, 40, () => { _selectedId = oo.Id; _draft = Clone(oo); RebuildOrderList(); RebuildLines(); }, StatusText(o)) { Lit = o.Id == _selectedId };
				var st = new Label(StatusText(o), 0.62f); st.Left.Set(6, 0f); st.Top.Set(22, 0f); b.Append(st);
				_orderList.Add(b);
			}
		}

		private void RebuildLines()
		{
			_lineList.Clear();
			for (int i = 0; i < _draft.Lines.Count; i++)
			{
				int idx = i; var line = _draft.Lines[i];
				var row = new UIElement(); row.Width.Set(396, 0f); row.Height.Set(38, 0f);
				for (int k = 0; k < line.Types.Length; k++) { var c = new TypeCell(line.Types[k], () => { }); c.Left.Set(k * 38, 0f); row.Append(c); }
				string name = line.Types.Length == 1 ? ItemName(line.Types[0]) : L("SetOf", StemOf(ContentSamples.ItemsByType[line.Types[0]]));
				var nm = new Label(name, 0.75f); nm.Left.Set(line.Types.Length * 38 + 6, 0f); nm.Top.Set(10, 0f); row.Append(nm);
				var share = new NumberField(112, 0, 100, 5, true, () => line.Weight, v => { line.Weight = (int)v; _draft.Replan(); }); share.Left.Set(240, 0f); share.Top.Set(7, 0f); row.Append(share);
				var pct = new Label("%", 0.8f); pct.Left.Set(356, 0f); pct.Top.Set(10, 0f); row.Append(pct);
				var x = new LevyButton("X", LevyConstants.UI.ButtonTextScale, 22, 24, () => { _draft.Lines.RemoveAt(idx); _draft.Replan(); RebuildLines(); }); x.Left.Set(372, 0f); x.Top.Set(7, 0f); row.Append(x);
				_lineList.Add(row);
			}
			_lineHash = _draft.Lines.Count;
		}

		// ====================================================================================== BUY
		private void BuildBuy()
		{
			var pg = _page[1];
			var h = new Label(L("BuyHeader"), 0.8f); pg.Append(h);
			_kindBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, 24, () => { _rule.Kind = (byte)((_rule.Kind + 1) % 3); _rule.Type = 0; RebuildItemCell(); }, L("KindHint")); _kindBtn.Top.Set(20, 0f); pg.Append(_kindBtn);
			_itemCellHost = new UIElement(); _itemCellHost.Left.Set(160, 0f); _itemCellHost.Top.Set(14, 0f); _itemCellHost.Width.Set(200, 0f); _itemCellHost.Height.Set(38, 0f); pg.Append(_itemCellHost);
			var cl = new Label(L("Count"), 0.8f); cl.Top.Set(62, 0f); pg.Append(cl);
			var count = new NumberField(150, 1, 9999, 5, true, () => _rule.Count, v => _rule.Count = (int)v); count.Left.Set(60, 0f); count.Top.Set(58, 0f); pg.Append(count);
			var bl = new Label(L("Budget"), 0.8f); bl.Left.Set(224, 0f); bl.Top.Set(62, 0f); pg.Append(bl);
			var budget = new NumberField(130, 0, 9999, 1, true, () => _rule.Budget / 10000f, v => _rule.Budget = (long)v * 10000L); budget.Left.Set(284, 0f); budget.Top.Set(58, 0f); pg.Append(budget);
			var gl = new Label(L("Gold"), 0.75f); gl.Left.Set(420, 0f); gl.Top.Set(62, 0f); pg.Append(gl);
			_buyNowBtn = new LevyButton(L("BuyNow"), LevyConstants.UI.ButtonTextScale, 120, 26, () => { if (Te != null) { LogisticsActions.BuyNow(Te, new BuyRule { Kind = _rule.Kind, Type = _rule.Type, Count = _rule.Count, Budget = _rule.Budget }); _offerTick = -1000; } }, L("BuyNowHint")); _buyNowBtn.Top.Set(92, 0f); pg.Append(_buyNowBtn);
			_addRuleBtn = new LevyButton(L("AddToOrder"), LevyConstants.UI.ButtonTextScale, 170, 26, () => { if (_draft.Rules.Count < 8) { _draft.Rules.Add(new BuyRule { Kind = _rule.Kind, Type = _rule.Type, Count = _rule.Count, Budget = _rule.Budget }); Main.NewText(L("RuleAdded", _draft.Rules.Count), 160, 220, 160); } }, L("AddToOrderHint")); _addRuleBtn.Left.Set(128, 0f); _addRuleBtn.Top.Set(92, 0f); pg.Append(_addRuleBtn);
			_buyInfo = new Label("", 0.75f); _buyInfo.Left.Set(304, 0f); _buyInfo.Top.Set(98, 0f); pg.Append(_buyInfo);
			_offersHdr = new Label(L("Offers"), 0.8f); _offersHdr.Top.Set(124, 0f); pg.Append(_offersHdr);
			_buyFilter = new ItemFilterBar(620, "Mods.Levy.Logistics.OfferSearch"); _buyFilter.Top.Set(142, 0f); _buyFilter.Changed += () => _offerTick = -1000; pg.Append(_buyFilter);
			_offerList.Top.Set(174, 0f); _offerList.Width.Set(620, 0f); _offerList.Height.Set(190, 0f); _offerList.ListPadding = 2f;
			_offerBar.Left.Set(626, 0f); _offerBar.Top.Set(174, 0f); _offerBar.Height.Set(190, 0f); _offerList.SetScrollbar(_offerBar);
			pg.Append(_offerList); pg.Append(_offerBar);
			RebuildItemCell();
		}

		private void RebuildItemCell()
		{
			_itemCellHost.RemoveAllChildren();
			if (_rule.Kind == BuyRule.KindItem)
			{
				if (_rule.Type > 0) { var c = new TypeCell(_rule.Type, () => { _rule.Type = 0; RebuildItemCell(); }); _itemCellHost.Append(c); var n = new Label(ItemName(_rule.Type), 0.75f); n.Left.Set(42, 0f); n.Top.Set(10, 0f); _itemCellHost.Append(n); }
				else { var add = new AddTypeCell(t => { _rule.Type = t; RebuildItemCell(); }); _itemCellHost.Append(add); var n = new Label(L("AddItem"), 0.7f); n.Left.Set(42, 0f); n.Top.Set(12, 0f); _itemCellHost.Append(n); }
			}
			else if (_rule.Kind == BuyRule.KindAnyAmmo)
			{
				var n = new Label(L("AnyAmmoHint"), 0.72f); n.Top.Set(12, 0f); _itemCellHost.Append(n);
			}
			else { var n = new Label(L("AnyGunHint"), 0.72f); n.Top.Set(12, 0f); _itemCellHost.Append(n); }
		}

		private void RefreshOffers()
		{
			_offerList.Clear();
			if (Te == null) return;
			List<Shopping.Offer> offers; int here = 0, via = 0;
			try { offers = Shopping.AllOffers(Te, out here, out via).OrderBy(o => o.Keeper.GivenName).ThenBy(o => o.UnitPrice).ToList(); } catch (Exception) { offers = new(); }
			_offersHdr.SetText(via > 0 ? L("ReachHeaderPylons", Shopkeepers(here), via) : L("ReachHeader", Shopkeepers(here)));
			if (offers.Count == 0) { var none = new Label(L("NoShops"), 0.8f); _offerList.Add(none); return; }
			int shown = 0;
			foreach (var o in offers)
			{
				if (!_buyFilter.Matches(ItemName(o.Item.type), o.Item)) continue;
				if (++shown > 120) break;
				var oo = o;
				string who = o.Pylon == null ? o.Keeper.GivenName : L("OfferVia", o.Keeper.GivenName, o.Pylon);
				var row = new LevyButton($"{ItemName(o.Item.type)}  -  {LevyUtil.Coins(o.UnitPrice)}  -  {who}", 0.75f, 600, 24, () => { _rule.Kind = BuyRule.KindItem; _rule.Type = oo.Item.type; RebuildItemCell(); }, L("OfferHint"));
				_offerList.Add(row);
			}
			if (shown == 0) _offerList.Add(new Label(L("NoMatch"), 0.8f));
		}

		private static string Shopkeepers(int n) => n == 1 ? L("Shopkeeper1") : L("ShopkeeperN", n);

		// ====================================================================================== SELL
		private void BuildSell()
		{
			var pg = _page[2];
			_grid = new SellGrid(() => Te, () => _sellDirty = true); _grid.Top.Set(0, 0f); pg.Append(_grid);
			float x = SellGrid.Cols * SellGrid.Cell + 14;
			var hl = new Label(L("SellHeader"), 0.8f); hl.Left.Set(x, 0f); pg.Append(hl);
			_catBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, 24, () => { _catIdx = (_catIdx + 1) % (Enum.GetValues<SellCat>().Length + 1); }, L("CatHint")); _catBtn.Left.Set(x, 0f); _catBtn.Top.Set(20, 0f); pg.Append(_catBtn);
			_tierBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, 24, () => { _tierIdx++; if (_tierIdx >= Selling.Tiers.Length) _tierIdx = -1; }, L("TierHint")); _tierBtn.Left.Set(x, 0f); _tierBtn.Top.Set(48, 0f); pg.Append(_tierBtn);
			_pickBtn = new LevyButton(L("SelectMatching"), LevyConstants.UI.ButtonTextScale, 150, 24, SelectMatching, L("SelectMatchingHint")); _pickBtn.Left.Set(x, 0f); _pickBtn.Top.Set(76, 0f); pg.Append(_pickBtn);
			_clearBtn = new LevyButton(L("ClearSel"), LevyConstants.UI.ButtonTextScale, 150, 24, () => { _grid.Clear(); _sellDirty = true; }); _clearBtn.Left.Set(x, 0f); _clearBtn.Top.Set(104, 0f); pg.Append(_clearBtn);
			_shimmerBtn = new LevyButton("", LevyConstants.UI.ButtonTextScale, 150, 24, () => { _shimmer = !_shimmer; _sellDirty = true; }, L("ShimmerHint")); _shimmerBtn.Left.Set(x, 0f); _shimmerBtn.Top.Set(132, 0f); pg.Append(_shimmerBtn);
			_sellBtn = new LevyButton(L("SellNow"), LevyConstants.UI.ButtonTextScale, 150, 28, SellNow, L("SellNowHint")); _sellBtn.Left.Set(x, 0f); _sellBtn.Top.Set(164, 0f); pg.Append(_sellBtn);
			_sellInfo = new Label("", 0.72f); _sellInfo.Left.Set(x, 0f); _sellInfo.Top.Set(200, 0f); pg.Append(_sellInfo);
			var tip = new Label(L("DragTip"), 0.66f); tip.Left.Set(x, 0f); tip.Top.Set(236, 0f); pg.Append(tip);
			_sellFilter = new ItemFilterBar(SellGrid.Cols * SellGrid.Cell + 14 + 230, "Mods.Levy.Logistics.OfferSearch"); _sellFilter.Top.Set(SellGrid.VisRows * SellGrid.Cell + 8, 0f); pg.Append(_sellFilter);
			_grid.Visible = it => _sellFilter.Matches(Lang.GetItemNameValue(it.type), it);
		}

		private Selling.Filter CurrentFilter() => LogisticsActions.FilterOf((byte)_catIdx, _tierIdx);

		private void SelectMatching()
		{
			if (Te == null) return; var f = CurrentFilter();
			if (!f.Any) { Main.NewText(L("PickAFilter"), 220, 200, 120); return; }
			_grid.Add(Selling.Select(Te, f)); _sellDirty = true;
		}

		private void SellNow()
		{
			if (Te == null || _grid.Selected.Count == 0) return;
			LogisticsActions.Sell(Te, _grid.Selected.ToList(), _shimmer);
			_grid.Clear(); _sellDirty = true; SoundEngine.PlaySound(SoundID.Coins);
		}

		// ====================================================================================== FIND
		private void BuildFind()
		{
			var pg = _page[3];
			_findLbl = new Label("", 0.8f); pg.Append(_findLbl);
			_findFilter = new ItemFilterBar(500, "Mods.Levy.Logistics.FindSearch"); _findFilter.Top.Set(22, 0f); _findFilter.Changed += () => _findDirty = true; pg.Append(_findFilter);
			var clear = new LevyButton(L("FindClear"), LevyConstants.UI.ButtonTextScale, 110, 26, () => { FindTarget.Set(0); }, L("FindClearHint")); clear.Left.Set(510, 0f); clear.Top.Set(22, 0f); pg.Append(clear);
			_findGrid.Top.Set(56, 0f); _findGrid.Width.Set(620, 0f); _findGrid.Height.Set(280, 0f); _findGrid.ListPadding = 2f;
			_findBar.Left.Set(626, 0f); _findBar.Top.Set(56, 0f); _findBar.Height.Set(280, 0f); _findGrid.SetScrollbar(_findBar);
			pg.Append(_findGrid); pg.Append(_findBar);
		}

		private void RefreshFind()
		{
			_findGrid.Clear();
			if (Te == null) return;
			if (_findAll.Count == 0 || _findDirty) _findAll = FindTarget.Candidates(Te);
			int shown = 0;
			var stocked = new HashSet<int>(); if (Te.Storage != null) foreach (var it in Te.Storage) if (it != null && !it.IsAir) stocked.Add(it.type);
			foreach (int t in _findAll)
			{
				if (!_findFilter.Matches(ItemName(t), ContentSamples.ItemsByType[t])) continue;
				if (!_findFilter.Active && !stocked.Contains(t) && !global::Levy.Common.Potions.SeekerMatcher.IsTreasure(t)) continue; // no search: stocked items plus ores, gems and treasure; a search covers every item
				if (++shown > 400) break; // ~9000 items in all: cap the grid, the search narrows it
				int tt = t;
				_findGrid.Add(new TypeCell(t, () => { if (_findPick != null) { var cb = _findPick; _findPick = null; FindTarget.Set(tt); cb(tt); } else FindTarget.Set(tt); }));
			}
			_findDirty = false;
		}

		// ====================================================================================== tick
		public void Tick()
		{
			if (Te == null) return;
			// orders: re-read when the book changes; a freshly saved new order gets selected
			if (_seenVersion != OrderBook.Version)
			{
				_seenVersion = OrderBook.Version;
				if (_selectLatest) { var last = OrderBook.For(Te.ID).LastOrDefault(); if (last != null) { _selectedId = last.Id; _draft = Clone(last); RebuildLines(); _selectLatest = false; } }
				else if (Stored != null) { _draft.Id = Stored.Id; }
				else if (_selectedId != 0) { _selectedId = 0; _draft = NewDraft(); RebuildLines(); }
				RebuildOrderList();
			}
			if (_draft.Lines.Count != _lineHash) RebuildLines();
			var s = Stored;
			_modeBtn.SetText(_draft.Mode == OrderMode.Bucket ? L("Bucket") : L("Random"));
			_buyBtn.SetText(_draft.BuyShortfalls ? L("BuyShortOn") : L("BuyShortOff")); _buyBtn.Lit = _draft.BuyShortfalls;
			_pauseBtn.SetText(s != null && s.Paused ? L("Resume") : L("Pause")); _pauseBtn.Enabled = s != null; _deleteBtn.Enabled = s != null;
			_statusLbl.SetText(s == null ? L("StatusNew") : StatusText(s));
			_rulesLbl.SetText(_draft.Rules.Count == 0 ? "" : L("RulesCount", _draft.Rules.Count));
			_kindBtn.SetText(_rule.Kind switch { BuyRule.KindItem => L("KindItem"), BuyRule.KindAnyGun => L("KindGun"), _ => L("KindAmmo") });
			_addRuleBtn.Enabled = _rule.Kind != BuyRule.KindItem || _rule.Type > 0; _buyNowBtn.Enabled = _addRuleBtn.Enabled;
			_buyInfo.SetText(L("Funds", LevyUtil.Coins(Shopping.Funds(Te))));
			_catBtn.SetText(_catIdx == 0 ? L("CatAny") : L("Cat" + (SellCat)_catIdx));
			_tierBtn.SetText(_tierIdx < 0 ? L("TierAny") : L("Tier" + Selling.Tiers[_tierIdx].Key));
			_shimmerBtn.SetText(_shimmer ? L("ShimmerOn") : L("ShimmerOff")); _shimmerBtn.Lit = _shimmer;
			_sellBtn.Enabled = _grid.Selected.Count > 0;

			_modeOrdersBtn.Lit = !_craftMode; _modeCraftBtn.Lit = _craftMode;
			if (_cur == 0 && !_craftMode) Checklist.Show(s != null ? Clone(s) : LocalPreview(), s != null);
			if (_cur == 0 && _craftMode)
			{
				if (_recipeRescan || _recipeVersion != CraftBrowser.Version || (Main.GameUpdateCount - (uint)_recipeTick) > 30) RescanRecipes();
				if (_recipeRebuild) RebuildRecipeList();
			}
			if (_cur == 1 && (Main.GameUpdateCount - (uint)_offerTick) > 120) { _offerTick = (int)Main.GameUpdateCount; RefreshOffers(); }
			if (_cur == 2 && (_sellDirty || ++_sellTick > 60)) { _sellTick = 0; _sellDirty = false; UpdateSellInfo(); }
			if (_cur == 3) { _findLbl.SetText(FindTarget.Target > 0 ? L("FindCurrent", ItemName(FindTarget.Target)) : L("FindNone")); if (_findDirty) RefreshFind(); }
		}

		/// <summary>Unsaved draft: the client's own estimate of the materials from the armory it can see (the server's figure, with chests and the network, replaces it once saved).</summary>
		private LogisticsOrder LocalPreview()
		{
			var d = Clone(_draft); d.Replan();
			try
			{
				var cc = new CountCache(t => Te.CountInStorage(t));
				var plan = LogisticsRunner.PlanRemaining(d, Te, cc);
				d.Checklist = plan.Need.OrderBy(k => k.Key).Select(k => new ChecklistRow { Type = k.Key, Need = k.Value, Have = Te.CountInStorage(k.Key) }).ToList();
				d.Status = "";
			}
			catch (Exception) { }
			return d;
		}

		private void UpdateSellInfo()
		{
			int n = _grid.Selected.Count;
			if (n == 0) { _sellInfo.SetText(L("SellNothing")); return; }
			_grid.Prune();
			var (buyer, total) = Selling.BestBuyer(Te, _grid.Selected.ToList());
			_sellInfo.SetText(buyer == null ? L("SellNoBuyer", n) : L("SellInfo", n, LevyUtil.Coins(total), Shopping.KeeperLabel(Te, buyer)));
		}

		public void PlaceChecklist(float panelLeft, float panelTop, float screenW)
		{
			float x = panelLeft + LevyConstants.UI.ArmoryPanelWidth + 6f;
			if (x + ChecklistPanel.PanelWidth > screenW) x = Math.Max(0f, panelLeft - ChecklistPanel.PanelWidth - 6f);
			Checklist.Left.Set(x, 0f); Checklist.Top.Set(panelTop, 0f);
		}

		public bool Typing => (_craftFilter?.Typing ?? false) || (_buyFilter?.Typing ?? false) || (_sellFilter?.Typing ?? false) || (_findFilter?.Typing ?? false);
	}

	/// <summary>One recipe of the browser: result icon, name, and how many can be made now. Green when it can be crafted now, dim when a station or condition blocks it.</summary>
	internal class RecipeRowEl : UIElement
	{
		private readonly RecipeEntry _e; private readonly Func<bool> _selected; private readonly Action _pick; private Item _item;
		public RecipeRowEl(RecipeEntry e, Func<bool> selected, Action pick)
		{
			_e = e; _selected = selected; _pick = pick; Width.Set(430, 0f); Height.Set(26, 0f);
			OnLeftClick += (ev, el) => { SoundEngine.PlaySound(SoundID.MenuTick); _pick(); };
		}
		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions().ToRectangle();
			bool sel = _selected();
			var bg = sel ? new Color(210, 150, 40) * 0.7f : _e.CanCraft ? new Color(30, 70, 45) * 0.7f : new Color(40, 54, 100) * 0.7f;
			if (IsMouseHovering) { bg = Color.Lerp(bg, Color.White, 0.15f); Main.LocalPlayer.mouseInterface = true; }
			sb.Draw(TextureAssets.MagicPixel.Value, d, bg);
			int type = _e.R.createItem.type;
			if (type > 0 && type < TextureAssets.Item.Length)
			{
				Main.instance.LoadItem(type); var tex = TextureAssets.Item[type].Value;
				float sc = Math.Min(1f, 20f / Math.Max(tex.Width, tex.Height));
				sb.Draw(tex, new Vector2(d.X + 14, d.Y + 13), null, Color.White, 0f, tex.Size() * 0.5f, sc, SpriteEffects.None, 0f);
			}
			string name = _e.Name + (_e.R.createItem.stack > 1 ? " x" + _e.R.createItem.stack : "");
			if (name.Length > 34) name = name.Substring(0, 33) + ".";
			Utils.DrawBorderString(sb, name, new Vector2(d.X + 30, d.Y + 4), _e.CanCraft || _e.Blocker == "" ? Color.White : new Color(200, 200, 210), 0.78f);
			string n = _e.Crafts.ToString(); var sz = FontAssets.MouseText.Value.MeasureString(n) * 0.78f;
			Utils.DrawBorderString(sb, n, new Vector2(d.Right - 8 - sz.X, d.Y + 4), _e.Crafts > 0 ? new Color(150, 240, 160) : new Color(170, 170, 180), 0.78f);
			if (IsMouseHovering) { if (_item == null) { _item = new Item(); _item.SetDefaults(type); } Main.HoverItem = _item.Clone(); Main.hoverItemName = _item.Name; }
		}
	}

	/// <summary>The materials checklist that hangs beside the armory: one row per material the order still needs, with what the stock holds. Green when covered, red when short.</summary>
	public class ChecklistPanel : UIPanel
	{
		public const int PanelWidth = 250, PanelHeight = 462;
		private readonly UIList _list = new(); private readonly UIScrollbar _bar = new();
		private readonly Label _title, _status, _foot;
		private string _sig = "";

		public ChecklistPanel()
		{
			SetPadding(0); Width.Set(PanelWidth, 0f); Height.Set(PanelHeight, 0f); BackgroundColor = new Color(33, 43, 79) * 0.94f;
			_title = new Label(LevyUtil.T("Logistics.ChecklistTitle"), 0.9f); _title.Left.Set(10, 0f); _title.Top.Set(8, 0f); Append(_title);
			_status = new Label("", 0.7f); _status.Left.Set(10, 0f); _status.Top.Set(30, 0f); Append(_status);
			_list.Left.Set(8, 0f); _list.Top.Set(52, 0f); _list.Width.Set(PanelWidth - 34, 0f); _list.Height.Set(PanelHeight - 90, 0f); _list.ListPadding = 2f;
			_bar.Left.Set(PanelWidth - 22, 0f); _bar.Top.Set(52, 0f); _bar.Height.Set(PanelHeight - 90, 0f); _list.SetScrollbar(_bar);
			Append(_list); Append(_bar);
			_foot = new Label("", 0.66f); _foot.Left.Set(10, 0f); _foot.Top.Set(PanelHeight - 32, 0f); Append(_foot);
		}

		public void Show(LogisticsOrder o, bool saved)
		{
			string sig = (saved ? "S" : "D") + o.Status + "|" + string.Join(";", o.Checklist.Select(r => r.Type + ":" + r.Need + ":" + r.Have)) + "|" + o.Made + "/" + o.Total;
			if (sig == _sig) return;
			_sig = sig; _list.Clear();
			_status.SetText(saved ? LogisticsView.StatusText(o) : LevyUtil.T("Logistics.ChecklistDraft"));
			long missing = 0;
			foreach (var r in o.Checklist)
			{
				var row = new ChecklistRowEl(r); _list.Add(row); if (r.Missing > 0) missing++;
			}
			if (o.Checklist.Count == 0) _list.Add(new Label(LevyUtil.T("Logistics.ChecklistEmpty"), 0.75f));
			_foot.SetText(o.Checklist.Count == 0 ? "" : missing == 0 ? LevyUtil.T("Logistics.AllCovered") : LevyUtil.T("Logistics.SomeShort", missing));
			Recalculate();
		}
	}

	internal class ChecklistRowEl : UIElement
	{
		private readonly ChecklistRow _r; private Item _item;
		public ChecklistRowEl(ChecklistRow r) { _r = r; Width.Set(ChecklistPanel.PanelWidth - 40, 0f); Height.Set(30, 0f); }
		protected override void DrawSelf(SpriteBatch sb)
		{
			var d = GetDimensions().ToRectangle();
			bool ok = _r.Missing == 0;
			sb.Draw(TextureAssets.MagicPixel.Value, d, (ok ? new Color(30, 70, 45) : new Color(80, 35, 40)) * 0.55f);
			if (_item == null) { _item = new Item(); _item.SetDefaults(_r.Type); }
			if (_r.Type > 0 && _r.Type < TextureAssets.Item.Length)
			{
				Main.instance.LoadItem(_r.Type); var tex = TextureAssets.Item[_r.Type].Value;
				float sc = Math.Min(1f, 22f / Math.Max(tex.Width, tex.Height));
				sb.Draw(tex, new Vector2(d.X + 15, d.Y + 15), null, Color.White, 0f, tex.Size() * 0.5f, sc, SpriteEffects.None, 0f);
			}
			string name = _item.Name; if (name.Length > 16) name = name.Substring(0, 15) + ".";
			Utils.DrawBorderString(sb, name, new Vector2(d.X + 32, d.Y + 2), Color.White, 0.7f);
			Utils.DrawBorderString(sb, $"{Fmt(_r.Have)} / {Fmt(_r.Need)}", new Vector2(d.X + 32, d.Y + 15), ok ? new Color(150, 240, 160) : new Color(255, 150, 150), 0.7f);
			if (IsMouseHovering) { Main.LocalPlayer.mouseInterface = true; Main.HoverItem = _item.Clone(); Main.hoverItemName = _item.Name; }
		}
		private static string Fmt(long n) => n >= 10000 ? (n / 1000) + "k" : n.ToString();
	}

	/// <summary>
	/// The sell page's grid: the armory's storage as small cells. Press and drag a rectangle to select it (dragging over a fully selected rectangle deselects it), wheel to scroll. Hover shows the item.
	/// </summary>
	public class SellGrid : UIElement
	{
		public const int Cols = 16, Cell = 26, VisRows = 11;
		private readonly Func<ArmoryTE> _te; private readonly Action _changed;
		public readonly HashSet<int> Selected = new();
		/// <summary>Cells whose item this returns false for are dimmed and ignored by drag-select (the search and type filter). Null shows everything.</summary>
		public Func<Item, bool> Visible;
		private int _scroll; private bool _drag; private int _ac, _ar;

		public SellGrid(Func<ArmoryTE> te, Action changed)
		{
			_te = te; _changed = changed;
			Width.Set(Cols * Cell, 0f); Height.Set(VisRows * Cell, 0f);
			OnScrollWheel += (e, el) => { _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - Math.Sign(e.ScrollWheelValue))); };
		}

		private int TotalRows => (_te()?.Storage.Length ?? 0 + Cols - 1) / Cols;
		private int MaxScroll => Math.Max(0, ((_te()?.Storage.Length ?? 0) + Cols - 1) / Cols - VisRows);

		public void Clear() { Selected.Clear(); _scroll = 0; _drag = false; }
		public void Add(IEnumerable<int> slots) { foreach (int i in slots) Selected.Add(i); }
		public void Prune() { var a = _te(); if (a == null) { Selected.Clear(); return; } Selected.RemoveWhere(i => i < 0 || i >= a.Storage.Length || !Selling.Sellable(a.Storage[i])); }

		private (int c, int r) CellAt(Vector2 mouse)
		{
			var d = GetDimensions();
			int c = (int)Math.Clamp((mouse.X - d.X) / Cell, 0, Cols - 1), r = (int)Math.Clamp((mouse.Y - d.Y) / Cell, 0, VisRows - 1);
			return (c, r + _scroll);
		}

		public override void LeftMouseDown(UIMouseEvent evt) { base.LeftMouseDown(evt); var (c, r) = CellAt(evt.MousePosition); _drag = true; _ac = c; _ar = r; }

		private void Finish()
		{
			_drag = false; var a = _te(); if (a == null) return;
			var (c, r) = CellAt(Main.MouseScreen);
			var cells = Selling.SelectRect(a, Cols, _ac, _ar, c, r); if (Visible != null) cells.RemoveAll(i => !Visible(a.Storage[i]));
			if (cells.Count > 0) { if (cells.All(Selected.Contains)) foreach (int i in cells) Selected.Remove(i); else foreach (int i in cells) Selected.Add(i); }
			_changed?.Invoke();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_drag && !Main.mouseLeft) Finish();
			if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var a = _te(); if (a == null) return;
			var d = GetDimensions(); var px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, new Rectangle((int)d.X, (int)d.Y, Cols * Cell, VisRows * Cell), new Color(15, 20, 40) * 0.9f);
			_scroll = Math.Min(_scroll, MaxScroll);
			(int c0, int r0, int c1, int r1) drag = default;
			if (_drag) { var (mc, mr) = CellAt(Main.MouseScreen); drag = OrderMath.DragCells(_ac, _ar, mc, mr); }
			Item hover = null;
			for (int r = 0; r < VisRows; r++)
				for (int c = 0; c < Cols; c++)
				{
					int idx = (r + _scroll) * Cols + c; if (idx >= a.Storage.Length) continue;
					var rect = new Rectangle((int)d.X + c * Cell, (int)d.Y + r * Cell, Cell - 1, Cell - 1);
					var it = a.Storage[idx]; bool sel = Selected.Contains(idx);
					bool inDrag = _drag && c >= drag.c0 && c <= drag.c1 && r + _scroll >= drag.r0 && r + _scroll <= drag.r1;
					sb.Draw(px, rect, sel ? new Color(210, 150, 40) * 0.8f : inDrag ? new Color(120, 160, 255) * 0.6f : new Color(40, 54, 100) * 0.7f);
					if (it == null || it.IsAir) continue;
					Main.instance.LoadItem(it.type); var tex = TextureAssets.Item[it.type].Value;
					float sc = Math.Min(1f, (Cell - 6f) / Math.Max(tex.Width, tex.Height));
					sb.Draw(tex, rect.Center.ToVector2(), null, Selling.Sellable(it) && (Visible == null || Visible(it)) ? Color.White : Color.White * 0.25f, 0f, tex.Size() * 0.5f, sc, SpriteEffects.None, 0f);
					if (rect.Contains(Main.MouseScreen.ToPoint())) hover = it;
				}
			if (hover != null) { Main.HoverItem = hover.Clone(); Main.hoverItemName = hover.Name; }
		}
	}
}
