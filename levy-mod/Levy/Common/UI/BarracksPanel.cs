using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Barracks management: treasury, manpower, command points, settlement toggles, militia, enlistment and the roster of
	/// the local player's Regulars as a grid of unit cards with per-soldier actions. Everything goes through <see cref="UIActions"/>.
	/// The panel is resizable: drag the grip in the bottom-right corner and the card grid reflows to the new size (more columns when
	/// wider, more rows when taller, a scrollbar only when the cards overflow). The size is saved per character in <see cref="UIPlayer"/>.
	/// </summary>
	public class BarracksPanel : UIState
	{
		public BarracksTE Te { get; private set; }

		/// <summary>Right edge of the panel in UI pixels (kept current), so the loadout panel can dock beside it.</summary>
		public static float DockRight { get; private set; }

		private DraggablePanel _panel;
		private Label _title, _treasury, _rosterTitle, _allLabel, _empty;
		private LevyButton _close, _dep1, _dep10, _depAll, _wd1, _wd10, _mobilize, _autoMuster, _tax, _hazard, _recruit, _enlistLevy, _enlistMaa, _enlistChamp;
		private LevyButton _allFollow, _allHold, _allReturn, _allHoldFire;
		private BarElement _manpower, _cp;
		private CardGrid _grid;
		private UIScrollbar _scroll;
		private ResizeGrip _grip;
		private readonly List<RosterCard> _cards = new();
		/// <summary>Soldiers whose card is showing the "more actions" page (by id, so it survives a roster rebuild).</summary>
		private readonly HashSet<int> _moreIds = new();
		private int _refreshTimer;

		// panel size in UI pixels (the live, clamped value) and the size at the moment the grip was pressed
		private float _w = LevyConstants.UI.BarracksPanelWidth, _h = LevyConstants.UI.BarracksPanelHeight;
		private float _gripW0, _gripH0;

		// per-refresh facts shared by every card, so a 100-soldier roster does these scans once, not once per card
		internal bool BannersNearby, HasLifeCrystal, HasLifeFruit, HasManaCrystal;
		internal BarracksTE OtherBarracks;

		private const float BtnScale = LevyConstants.UI.ButtonTextScale;
		private const int ListTop = LevyConstants.UI.BarracksListTop;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = Put(new Label("", 1f), 12, 8);
			_close = Put(new LevyButton("X", BtnScale, 26, 26, () => UISystem.Instance.CloseBarracks(), LevyUtil.T("UI2.Close")), 0, 6);

			// treasury
			_treasury = Put(new Label("", 0.85f), 12, 36);
			string depHint = LevyUtil.T("UI2.DepositHint"), wdHint = LevyUtil.T("UI2.WithdrawHint");
			_dep1 = Put(new LevyButton(LevyUtil.T("UI2.DepositOneGold"), 0.7f, 70, 24, () => UIActions.Deposit(Te, LevyConstants.UI.DepositSmall), depHint), 12, 58);
			_dep10 = Put(new LevyButton(LevyUtil.T("UI2.DepositTenGold"), 0.7f, 78, 24, () => UIActions.Deposit(Te, LevyConstants.UI.DepositLarge), depHint), 86, 58);
			_depAll = Put(new LevyButton(LevyUtil.T("UI2.DepositAll"), 0.7f, 92, 24, () => UIActions.Deposit(Te, UIActions.InventoryCoins()), depHint), 168, 58);
			_wd1 = Put(new LevyButton(LevyUtil.T("UI2.WithdrawOneGold"), 0.7f, 70, 24, () => UIActions.Withdraw(Te, LevyConstants.UI.DepositSmall), wdHint), 268, 58);
			_wd10 = Put(new LevyButton(LevyUtil.T("UI2.WithdrawTenGold"), 0.7f, 78, 24, () => UIActions.Withdraw(Te, LevyConstants.UI.DepositLarge), wdHint), 342, 58);
			_mobilize = Put(new LevyButton("", BtnScale, 118, 24, () => UIActions.ToggleMobilize(Te)), 0, 58);

			// bars (widths follow the panel width, see ApplyLayout)
			_manpower = Put(new BarElement(260, 20) { Fill = new Color(90, 160, 90) }, 12, 88);
			_cp = Put(new BarElement(266, 20) { Fill = new Color(170, 140, 60) }, 282, 88);

			// settlement toggles + extras
			_autoMuster = Put(new LevyButton("", 0.7f, 150, 24, () => UIActions.BarracksToggle(Te, 0, !Te.AutoMuster)), 12, 116);
			_tax = Put(new LevyButton("", 0.7f, 170, 24, () => UIActions.BarracksToggle(Te, 1, !Te.TaxToTreasury), LevyUtil.T("UI.TaxToTreasury")), 168, 116);
			_hazard = Put(new LevyButton("", 0.7f, 204, 24, () => UIActions.HazardPay(Te)), 0, 116);
			_recruit = Put(new LevyButton("", 0.7f, 250, 24, () => UIActions.RecruitingDrive(Te)), 12, 146);

			// enlist
			_enlistLevy = Put(new LevyButton("", 0.7f, 170, 24, () => UIActions.Enlist(Te, Grade.Levy)), 12, 176);
			_enlistMaa = Put(new LevyButton("", 0.7f, 182, 24, () => UIActions.Enlist(Te, Grade.ManAtArms)), 188, 176);
			_enlistChamp = Put(new LevyButton("", 0.7f, 170, 24, () => UIActions.Enlist(Te, Grade.Champion)), 376, 176);

			// roster header + squad-wide orders (atBarracks: every Regular of the commander)
			_rosterTitle = Put(new Label(LevyUtil.T("UI2.Roster"), 0.85f), 12, 208);
			_allLabel = Put(new Label(LevyUtil.T("UI2.AllOrders") + ":", 0.7f), 118, 210);
			_allFollow = Put(new LevyButton(LevyUtil.T("UI2.FollowAll"), 0.7f, 70, 24, () => UIActions.Order(SoldierOrder.Follow, true), LevyUtil.T("UI.Follow")), 212, 206);
			_allHold = Put(new LevyButton(LevyUtil.T("UI2.HoldAll"), 0.7f, 66, 24, () => UIActions.Order(SoldierOrder.Hold, true), LevyUtil.T("UI.Hold")), 286, 206);
			_allReturn = Put(new LevyButton(LevyUtil.T("UI2.ReturnAll"), 0.7f, 76, 24, () => UIActions.Order(SoldierOrder.ReturnToBarracks, true), LevyUtil.T("UI.Return")), 356, 206);
			_allHoldFire = Put(new LevyButton("", 0.7f, 110, 24, () => UIActions.HoldFire(true)), 436, 206);

			// roster card grid + its scrollbar
			_scroll = new UIScrollbar();
			_grid = new CardGrid(_scroll);
			_grid.Left.Set(12f, 0f); _grid.Top.Set(ListTop, 0f);
			_panel.Append(_grid);
			_scroll.Top.Set(ListTop + 4, 0f);
			_panel.Append(_scroll);
			_empty = Put(new Label(LevyUtil.T("UI2.NoRegulars"), 0.8f), 16, ListTop + 8);

			// resize grip: bottom-right corner, appended last so it sits on top of everything
			_grip = new ResizeGrip(LevyConstants.UI.ResizeGripSize, LevyUtil.T("UI2.ResizeHint"));
			_grip.OnPress = () => { _gripW0 = _w; _gripH0 = _h; };
			_grip.OnDrag = (dx, dy) => Resize(_gripW0 + dx, _gripH0 + dy, false, false);
			_grip.OnRelease = () => Resize(_w, _h, true, true);
			_panel.Append(_grip);

			ApplyLayout();
			Append(_panel);
		}

		private T Put<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el);
			return el;
		}

		// ------------------------------------------------------------------ size
		/// <summary>Positions everything that depends on the panel size: the right-anchored header controls, the two bars, the grid, scrollbar and grip.</summary>
		private void ApplyLayout()
		{
			_panel.Width.Set(_w, 0f); _panel.Height.Set(_h, 0f);
			_close.Left.Set(_w - 36, 0f);
			_mobilize.Left.Set(_w - 12 - 118, 0f);
			_hazard.Left.Set(_w - 12 - 204, 0f);
			float barW = (_w - 24f - 10f) * 0.5f;
			_manpower.Width.Set(barW, 0f);
			_cp.Left.Set(12f + barW + 10f, 0f); _cp.Width.Set(barW, 0f);

			float listH = _h - ListTop - 10f;
			_grid.Width.Set(_w - 12 - 12 - 24, 0f); _grid.Height.Set(listH, 0f);
			_scroll.Left.Set(_w - 12 - 20, 0f);
			_scroll.Height.Set(_h - LevyConstants.UI.ResizeGripSize - 4f - (ListTop + 4f), 0f);
			_grip.Left.Set(_w - LevyConstants.UI.ResizeGripSize - 2f, 0f); _grip.Top.Set(_h - LevyConstants.UI.ResizeGripSize - 2f, 0f);
			DockRight = _panel.Left.Pixels + _w;
		}

		/// <summary>Sets the panel size, clamped to [min, screen]; the cards reflow on the recalculate. <paramref name="persist"/> also saves it on the character.</summary>
		private void Resize(float w, float h, bool persist, bool force)
		{
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			int margin = LevyConstants.UI.BarracksScreenMargin;
			float maxW = Math.Max(LevyConstants.UI.BarracksMinWidth, sw - _panel.Left.Pixels - margin);
			float maxH = Math.Max(LevyConstants.UI.BarracksMinHeight, sh - _panel.Top.Pixels - margin);
			float nw = Math.Clamp(w, LevyConstants.UI.BarracksMinWidth, maxW), nh = Math.Clamp(h, LevyConstants.UI.BarracksMinHeight, maxH);
			bool changed = Math.Abs(nw - _w) >= 0.5f || Math.Abs(nh - _h) >= 0.5f;
			if (changed || force)
			{
				_w = nw; _h = nh;
				ApplyLayout();
				if (_panel.Parent != null) _panel.Recalculate();
			}
			if (persist && Main.LocalPlayer != null)
			{
				var ui = Main.LocalPlayer.GetModPlayer<UIPlayer>();
				ui.BarracksW = (int)_w; ui.BarracksH = (int)_h;
			}
		}

		public void Bind(BarracksTE te)
		{
			Te = te;
			if (_panel == null) return;
			// the character's saved size (or the default), kept on screen; a size trimmed to fit a small screen is not written back
			var ui = Main.LocalPlayer?.GetModPlayer<UIPlayer>();
			float wantW = ui != null && ui.BarracksW > 0 ? ui.BarracksW : LevyConstants.UI.BarracksPanelWidth;
			float wantH = ui != null && ui.BarracksH > 0 ? ui.BarracksH : LevyConstants.UI.BarracksPanelHeight;
			float sh = Main.screenHeight / Main.UIScale;
			wantH = Math.Min(wantH, Math.Max(LevyConstants.UI.BarracksMinHeight, sh - 2f * LevyConstants.UI.BarracksScreenMargin));
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, sh - wantH - 10f)), 0f);
			Resize(wantW, wantH, false, true);
			_refreshTimer = 0;
			RebuildRows();
			Refresh();
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Te == null || !UIActions.IsLive(Te)) { UISystem.Instance.CloseBarracks(); return; }
			DockRight = _panel.Left.Pixels + _w;
			if (++_refreshTimer >= LevyConstants.UI.RosterRefreshTicks)
			{
				_refreshTimer = 0;
				if (!_grip.Dragging) Resize(_w, _h, false, false); // keep the panel on screen if the window shrank
				if (RosterChanged()) RebuildRows();
				Refresh();
			}
		}

		// ------------------------------------------------------------------ refresh
		private void Refresh()
		{
			var te = Te; if (te == null) return;
			var roster = RosterSystem.Instance;
			_title.SetText(LevyUtil.T("UI.Barracks", SettlementSystem.Instance.TownName(te)));
			_treasury.SetText(LevyUtil.T("UI.Treasury", LevyUtil.Coins(te.Treasury)));
			_wd1.Enabled = te.Treasury >= LevyConstants.UI.DepositSmall;
			_wd10.Enabled = te.Treasury >= LevyConstants.UI.DepositLarge;
			long coins = UIActions.InventoryCoins();
			_dep1.Enabled = coins >= LevyConstants.UI.DepositSmall;
			_dep10.Enabled = coins >= LevyConstants.UI.DepositLarge;
			_depAll.Enabled = coins > 0;

			int cap = SettlementSystem.Instance.ManpowerCap(te);
			if (cap <= 0) cap = te.ManpowerCapCached;
			_manpower.Fraction = cap > 0 ? te.Manpower / (float)cap : 0f;
			_manpower.Label = LevyUtil.T("UI.Manpower", te.Manpower, Math.Max(0, cap));
			int cpTotal = roster.CommandPoints, cpUsed = roster.CommandPointsUsed;
			_cp.Fraction = cpTotal > 0 ? cpUsed / (float)cpTotal : 0f;
			_cp.Label = LevyUtil.T("UI.CommandPoints", cpUsed, cpTotal);

			_mobilize.Set(LevyUtil.T(te.Mobilized ? "UI.StandDown" : "UI.Mobilize"), true, LevyUtil.T(te.Mobilized ? "UI2.StandDownHint" : "UI2.MobilizeHint"));
			_autoMuster.Set(LevyUtil.T("UI2.Toggle", LevyUtil.T("UI.AutoMuster"), LevyUtil.T(te.AutoMuster ? "UI2.On" : "UI2.Off")), true);
			_tax.Set(LevyUtil.T("UI2.Toggle", LevyUtil.T("UI2.TaxShort"), LevyUtil.T(te.TaxToTreasury ? "UI2.On" : "UI2.Off")), true);
			int day = roster.DayNumber;
			string usedToday = " (" + LevyUtil.T("UI2.UsedToday") + ")";
			_hazard.Set(LevyUtil.T("UI.HazardPay", LevyUtil.Coins(HazardPayCost())) + (day > 0 && te.HazardPayDay == day ? usedToday : ""), true);
			_recruit.Set(LevyUtil.T("UI.RecruitingDrive", LevyUtil.Coins(EconomySystem.RecruitingDrivePerPoint)) + (day > 0 && te.RecruitingDriveDay == day ? usedToday : ""), true);

			RefreshEnlist(_enlistLevy, Grade.Levy, "UI.EnlistLevy");
			RefreshEnlist(_enlistMaa, Grade.ManAtArms, "UI.EnlistManAtArms");
			RefreshEnlist(_enlistChamp, Grade.Champion, "UI.EnlistChampion");

			bool holdFire = UIActions.MajorityHoldFire();
			_allHoldFire.Set(LevyUtil.T(holdFire ? "UI.FireAtWill" : "UI.HoldFire"), true, LevyUtil.T(holdFire ? "UI2.FireAtWillHint" : "UI2.HoldFireHint"));

			bool any = _cards.Count > 0;
			if (any && _empty.Parent != null) _empty.Remove();
			else if (!any && _empty.Parent == null) _panel.Append(_empty);
			string rosterText = LevyUtil.T("UI2.Roster") + (any ? " (" + _cards.Count + ")" : "");
			if (_rosterTitle.Text != rosterText) _rosterTitle.SetText(rosterText);

			// facts every card needs, scanned once here instead of once per card
			var me = Main.LocalPlayer;
			BannersNearby = UIActions.FindPatrolPair(te.Center, LevyConstants.UI.PatrolBannerSearchTiles, out _, out _);
			HasLifeCrystal = me.HasItem(ItemID.LifeCrystal); HasLifeFruit = me.HasItem(ItemID.LifeFruit); HasManaCrystal = me.HasItem(ItemID.ManaCrystal);
			OtherBarracks = UIActions.NearestOtherBarracks(te.Center, te);
			foreach (var card in _cards) if (card.Parent != null) card.Refresh(); // off-screen cards refresh when they scroll into view
		}

		private long HazardPayCost()
		{
			long sum = 0;
			foreach (var r in UIActions.MyRegulars()) if (!r.StoodDown) sum += EconomySystem.WagePerDay(r.Grade, false) * LevyConstants.HazardPayDays;
			return sum;
		}

		private void RefreshEnlist(LevyButton b, Grade g, string key)
		{
			long fee = EconomySystem.MusterFee(g);
			bool unlocked = RosterSystem.Instance.GradeUnlocked(g);
			bool cpOk = UIActions.AvailableCp >= LevyConstants.GradeCpCost[(int)g];
			bool moneyOk = Te != null && Te.Treasury >= fee;
			string hint = !unlocked ? LevyUtil.T("UI.GradeLocked") : !cpOk ? LevyUtil.T("UI.NotEnoughCp") : !moneyOk ? LevyUtil.T("UI.NotEnoughMoney") : LevyUtil.T("UI.Enlist");
			b.Set(LevyUtil.T(key, LevyUtil.Coins(fee)), unlocked && cpOk && moneyOk, hint);
		}

		// ------------------------------------------------------------------ roster cards
		private bool RosterChanged()
		{
			int i = 0;
			foreach (var r in UIActions.MyRegulars())
			{
				if (i >= _cards.Count || !ReferenceEquals(_cards[i].Rec, r)) return true;
				i++;
			}
			return i != _cards.Count;
		}

		private void RebuildRows()
		{
			_cards.Clear();
			foreach (var r in UIActions.MyRegulars()) _cards.Add(new RosterCard(this, r, _moreIds.Contains(r.Id)));
			_grid.SetCards(_cards);
		}

		private void SetMore(int id, bool more) { if (more) _moreIds.Add(id); else _moreIds.Remove(id); }

		private static float Measure(string s, float scale) => FontAssets.MouseText.Value.MeasureString(s ?? "").X * scale;

		/// <summary>Trims <paramref name="s"/> with an ellipsis so it is at most <paramref name="maxW"/> pixels wide at <paramref name="scale"/>.</summary>
		private static string Fit(string s, float scale, float maxW)
		{
			if (string.IsNullOrEmpty(s) || Measure(s, scale) <= maxW) return s ?? "";
			while (s.Length > 1 && Measure(s + "...", scale) > maxW) s = s.Substring(0, s.Length - 1);
			return s + "...";
		}

		/// <summary>"1g 20s" style, the two largest non-zero denominations (cards are too narrow for "1 gold 20 silver").</summary>
		private static string ShortCoins(long copper)
		{
			if (copper <= 0) return "0c";
			long p = copper / 1000000, g = copper / 10000 % 100, s = copper / 100 % 100, c = copper % 100;
			var parts = new List<string>();
			if (p > 0) parts.Add(p + "p");
			if (g > 0) parts.Add(g + "g");
			if (s > 0) parts.Add(s + "s");
			if (c > 0) parts.Add(c + "c");
			return string.Join(" ", parts.GetRange(0, Math.Min(2, parts.Count)));
		}

		/// <summary>
		/// The scrolling area of the roster: lays the cards out in as many columns as fit (each at least the minimum card width, stretched to fill the
		/// row), scrolls vertically only when the rows overflow, and keeps only the visible rows attached so a large roster stays cheap to update and draw.
		/// </summary>
		private class CardGrid : UIElement
		{
			private readonly UIScrollbar _bar;
			private IReadOnlyList<RosterCard> _cards = Array.Empty<RosterCard>();
			private float _lastView = -1f;

			public CardGrid(UIScrollbar bar) { _bar = bar; OverflowHidden = true; }

			public void SetCards(IReadOnlyList<RosterCard> cards)
			{
				RemoveAllChildren();
				_cards = cards;
				Recalculate();
			}

			public override void RecalculateChildren()
			{
				Layout();
				base.RecalculateChildren();
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				if (_bar.ViewPosition != _lastView) Recalculate(); // scrolled: slide the cards and swap which rows are attached
			}

			public override void ScrollWheel(UIScrollWheelEvent evt)
			{
				base.ScrollWheel(evt);
				_bar.ViewPosition -= evt.ScrollWheelValue;
			}

			private void Layout()
			{
				var inner = GetInnerDimensions();
				float w = inner.Width, h = inner.Height;
				float gap = LevyConstants.UI.RosterCardGap, cardH = LevyConstants.UI.RosterCardHeight;
				int cols = Math.Max(1, (int)((w + gap) / (LevyConstants.UI.RosterCardMinWidth + gap)));
				float cardW = (w - gap * (cols - 1)) / cols;
				int rows = (_cards.Count + cols - 1) / cols;
				float content = rows > 0 ? rows * (cardH + gap) - gap : 0f;
				_bar.SetView(h, Math.Max(content, h)); // clamps the scroll position when the content shrank
				float view = _bar.ViewPosition;
				_lastView = view;
				for (int i = 0; i < _cards.Count; i++)
				{
					var card = _cards[i];
					float x = i % cols * (cardW + gap), y = i / cols * (cardH + gap) - view;
					bool visible = y + cardH > 0f && y < h;
					if (!visible) { if (card.Parent != null) card.Remove(); continue; }
					card.Place(x, y, cardW, cardH);
					if (card.Parent == null) Append(card);
				}
			}
		}

		/// <summary>Draws a soldier's weapon, sidearm and armor as small item slots; hovering one shows the item tooltip (or the slot name when empty).</summary>
		private class GearStrip : UIElement
		{
			private static readonly int[] Slots = { SoldierGear.SlotWeapon, SoldierGear.SlotSidearm, SoldierGear.SlotHead, SoldierGear.SlotBody, SoldierGear.SlotLegs };
			private static readonly string[] SlotNameKeys = { "Loadout.Weapon", "Loadout.Sidearm", "Loadout.Head", "Loadout.Body", "Loadout.Legs" };
			private readonly Func<SoldierRecord> _rec;

			public static float StripWidth => Slots.Length * LevyConstants.UI.CardGearIcon + (Slots.Length - 1) * LevyConstants.UI.CardGearGap;

			public GearStrip(Func<SoldierRecord> rec)
			{
				_rec = rec;
				Width.Set(StripWidth, 0f); Height.Set(LevyConstants.UI.CardGearIcon, 0f);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				var gear = _rec()?.Gear;
				if (gear == null) return;
				int icon = LevyConstants.UI.CardGearIcon, pitch = icon + LevyConstants.UI.CardGearGap;
				var pos = GetDimensions().Position();
				float oldScale = Main.inventoryScale;
				Main.inventoryScale = LevyConstants.UI.CardGearInventoryScale;
				for (int i = 0; i < Slots.Length; i++)
				{
					Item it = gear.Get(Slots[i]) ?? new Item();
					ItemSlot.Draw(spriteBatch, ref it, ItemSlot.Context.ChestItem, pos + new Vector2(i * pitch, 0f));
				}
				Main.inventoryScale = oldScale;

				if (!IsMouseHovering) return;
				int hover = (int)((Main.MouseScreen.X / Main.UIScale - pos.X) / pitch);
				if (hover < 0 || hover >= Slots.Length) return;
				Main.LocalPlayer.mouseInterface = true;
				Item held = gear.Get(Slots[hover]);
				if (held != null && !held.IsAir) { Main.HoverItem = held.Clone(); Main.hoverItemName = held.Name; }
				else HintTooltip.Queue(LevyUtil.T(SlotNameKeys[hover]));
			}
		}

		/// <summary>Invisible strip that shows a (lazily built) tooltip while hovered. Used over a card's name row for the full detail text.</summary>
		private class HoverHint : UIElement
		{
			private readonly Func<string> _text;
			public HoverHint(Func<string> text) { _text = text; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (IsMouseHovering) HintTooltip.Queue(_text());
			}
		}

		/// <summary>
		/// One Regular. Name and grade (the border and tint take the grade colour), an HP bar and a mana bar, the weapon / sidearm / armor icons,
		/// the current state, and the per-soldier actions on two button rows. A "More" button swaps those rows for the less common actions
		/// (assign patrol, rebase, feed crystals). Hovering the name row shows the full details.
		/// Portrait hook: <see cref="LevyConstants.UI.CardPortraitWidth"/> reserves a left column for the soldier's live sprite; <see cref="DrawPortrait"/> is where it is drawn.
		/// </summary>
		private class RosterCard : UIPanel
		{
			public readonly SoldierRecord Rec;
			private readonly BarracksPanel _owner;
			private readonly Label _name, _grade, _state, _info;
			private readonly BarElement _hp, _mp;
			private readonly GearStrip _gear;
			private readonly LevyButton _follow, _hold, _return, _kit, _promote, _discharge, _moreBtn;
			private readonly LevyButton _patrol, _rebase, _back, _feedLc, _feedLf, _feedMc;
			private readonly List<LevyButton> _allButtons = new();
			private readonly List<(LevyButton b, float fixedW)> _rowItems = new();
			private bool _more, _inLayout;
			private float _w;
			private int _sig = -1;

			private static readonly Color CardBase = new(45, 58, 110), Vet = new(255, 240, 200), Unpaid = new(255, 150, 120), Muted = new(170, 170, 180);
			private const float NameScale = 0.8f, SmallScale = 0.62f, CardBtnScale = 0.65f;
			private const int Pad = 6, NameY = 3, HpY = 22, HpH = 13, MpY = 37, MpH = 11, GearY = 52, ActionY = 80, RowH = 22, RowGap = 3, BtnGap = 3;

			public RosterCard(BarracksPanel owner, SoldierRecord rec, bool more)
			{
				_owner = owner; Rec = rec; _more = more;
				SetPadding(0);

				var hoverRow = Add(new HoverHint(Details), 0, 0); // first, so the labels draw over it
				hoverRow.Width.Set(0f, 1f); hoverRow.Height.Set(HpY - 2, 0f);
				_name = Add(new Label("", NameScale), Pad, NameY);
				_grade = Add(new Label("", SmallScale), 0, NameY + 3);
				_hp = Add(new BarElement(10, HpH) { TextScale = SmallScale }, Pad, HpY);
				_mp = Add(new BarElement(10, MpH) { TextScale = SmallScale - 0.05f, Fill = new Color(70, 110, 215) }, Pad, MpY);
				_gear = Add(new GearStrip(() => Rec), Pad, GearY);
				_state = Add(new Label("", SmallScale), 0, GearY);
				_info = Add(new Label("", SmallScale), 0, GearY + 13);

				string follow = LevyUtil.T("UI2.FollowAll"), hold = LevyUtil.T("UI2.HoldAll"), ret = LevyUtil.T("UI2.ReturnAll");
				_follow = Btn(follow, () => UIActions.SoldierOrder(Rec, SoldierOrder.Follow), LevyUtil.T("UI.Follow"));
				_hold = Btn(hold, () => UIActions.SoldierOrder(Rec, SoldierOrder.Hold), LevyUtil.T("UI.Hold"));
				_return = Btn(ret, () => UIActions.SoldierOrder(Rec, SoldierOrder.ReturnToBarracks), LevyUtil.T("UI.Return"));
				_kit = Btn(LevyUtil.T("Loadout.Button"), OpenKit, LevyUtil.T("Loadout.ButtonHint"));
				_promote = Btn("", Promote);
				_discharge = Btn(LevyUtil.T("UI.Discharge"), () => UIActions.Discharge(Rec));
				_moreBtn = Btn(LevyUtil.T("UI2.MoreActions"), () => SetMore(true), LevyUtil.T("UI2.MoreActionsHint"));
				_patrol = Btn(LevyUtil.T("UI.Patrol"), Patrol, LevyUtil.T("UI2.PatrolHint"));
				_rebase = Btn("", Rebase);
				_back = Btn(LevyUtil.T("UI2.BackActions"), () => SetMore(false));
				_feedLc = Btn(LevyUtil.T("UI2.FeedLifeCrystalShort"), () => UIActions.FeedCrystal(Rec, ItemID.LifeCrystal), LevyUtil.T("UI2.FeedLifeCrystal"));
				_feedLf = Btn(LevyUtil.T("UI2.FeedLifeFruitShort"), () => UIActions.FeedCrystal(Rec, ItemID.LifeFruit), LevyUtil.T("UI2.FeedLifeFruit"));
				_feedMc = Btn(LevyUtil.T("UI2.FeedManaCrystalShort"), () => UIActions.FeedCrystal(Rec, ItemID.ManaCrystal), LevyUtil.T("UI2.FeedManaCrystal"));
			}

			private T Add<T>(T el, float left, float top) where T : UIElement { el.Left.Set(left, 0f); el.Top.Set(top, 0f); Append(el); return el; }

			private LevyButton Btn(string text, Action click, string hint = null)
			{
				var b = new LevyButton(text, CardBtnScale, 40, RowH, click, hint);
				_allButtons.Add(b); // not appended: Arrange attaches the ones the current page shows
				return b;
			}

			private void SetMore(bool more)
			{
				_more = more;
				_owner.SetMore(Rec.Id, more);
				Refresh();
			}

			/// <summary>Called by the grid: size and position inside it. Re-lays the contents out when the width changed or the card was off-screen.</summary>
			public void Place(float x, float y, float w, float h)
			{
				Left.Set(x, 0f); Top.Set(y, 0f); Width.Set(w, 0f); Height.Set(h, 0f);
				if (Math.Abs(w - _w) < 0.5f && Parent != null) return;
				_w = w;
				_inLayout = true; // the grid recalculates us right after this
				try { Refresh(); } finally { _inLayout = false; }
			}

			// ---- portrait hook (follow-up): the live sprite goes in this rectangle once CardPortraitWidth > 0
			private Rectangle PortraitArea
			{
				get { var d = GetDimensions(); return new Rectangle((int)d.X + Pad, (int)d.Y + NameY, LevyConstants.UI.CardPortraitWidth, (int)d.Height - 2 * NameY); }
			}

			private void DrawPortrait(SpriteBatch spriteBatch, Rectangle area) { }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);
				if (LevyConstants.UI.CardPortraitWidth > 0) DrawPortrait(spriteBatch, PortraitArea);
			}

			private static void SetText(Label l, string text) { if (l.Text != text) l.SetText(text); }

			public void Refresh()
			{
				var rec = Rec; var te = _owner.Te;
				if (rec == null || te == null || _w <= 0f) return;
				var roster = RosterSystem.Instance;
				float x0 = Pad + LevyConstants.UI.CardPortraitWidth, inner = _w - x0 - Pad;
				Color gc = UIActions.GradeColor(rec.Grade);
				UIActions.GetHp(rec, out int cur, out int max);
				bool dead = cur <= 0;
				BackgroundColor = Color.Lerp(CardBase, gc, LevyConstants.UI.CardGradeTint) * (dead ? 0.55f : 0.9f);
				BorderColor = gc * (dead ? 0.5f : 0.95f);

				// name + grade
				string gradeText = UIActions.Chevrons(rec.Grade) + " " + UIActions.GradeName(rec.Grade);
				SetText(_grade, gradeText); _grade.TextColor = gc;
				float gradeW = Measure(gradeText, SmallScale);
				_grade.Left.Set(_w - Pad - gradeW, 0f);
				_name.Left.Set(x0, 0f);
				SetText(_name, Fit(rec.Name, NameScale, inner - gradeW - 8f));
				_name.TextColor = rec.Veteran ? Vet : Color.White;

				// bars
				_hp.Left.Set(x0, 0f); _hp.Width.Set(inner, 0f);
				_hp.Fraction = max > 0 ? cur / (float)max : 0f;
				_hp.Fill = Color.Lerp(new Color(190, 55, 50), new Color(80, 170, 80), _hp.Fraction);
				_hp.Label = LevyUtil.T("UI2.Hp", cur, max);
				_mp.Left.Set(x0, 0f); _mp.Width.Set(inner, 0f);
				int maxMana = rec.MaxMana;
				_mp.Fraction = maxMana > 0 ? Math.Clamp(rec.Mana, 0, maxMana) / (float)maxMana : 0f;
				_mp.Label = LevyUtil.T("UI2.Mana", Math.Clamp(rec.Mana, 0, Math.Max(0, maxMana)), maxMana);

				// gear + state / warnings to its right
				_gear.Left.Set(x0, 0f);
				float infoX = x0 + GearStrip.StripWidth + 8f, infoW = Math.Max(10f, _w - infoX - Pad);
				_state.Left.Set(infoX, 0f); _info.Left.Set(infoX, 0f);
				SetText(_state, Fit(UIActions.StateText(rec), SmallScale, infoW));
				string info; Color infoColor = Color.White;
				if (rec.DaysUnpaid > 0) { info = LevyUtil.T("UI.Unpaid", rec.DaysUnpaid); infoColor = rec.DaysUnpaid >= 2 ? Unpaid : Color.White; }
				else if (rec.StoodDown) { info = LevyUtil.T("UI.StoodDown"); infoColor = Muted; }
				else info = LevyUtil.T("UI2.Kills", rec.Kills);
				SetText(_info, Fit(info, SmallScale, infoW)); _info.TextColor = infoColor;

				// promote
				if (rec.Grade >= Grade.Champion) _promote.Set(LevyUtil.T("UI2.PromoteMax"), false, LevyUtil.T("UI2.PromoteMax"));
				else
				{
					var next = (Grade)((int)rec.Grade + 1);
					long cost = EconomySystem.PromotionCost(rec.Grade, next);
					bool unlocked = roster.GradeUnlocked(next);
					bool cpOk = UIActions.AvailableCp >= LevyConstants.GradeCpCost[(int)next] - LevyConstants.GradeCpCost[(int)rec.Grade];
					var home = SettlementSystem.Instance.GetBarracks(rec.HomeBarracksId);
					var payer = home ?? te;
					bool moneyOk = payer.Treasury >= cost;
					string hint = !unlocked ? LevyUtil.T("UI.GradeLocked") : !cpOk ? LevyUtil.T("UI.NotEnoughCp") : !moneyOk ? LevyUtil.T("UI.NotEnoughMoney") : UIActions.GradeName(next) + " (" + LevyUtil.Coins(cost) + ")";
					_promote.Set(Fit(LevyUtil.T("UI.Promote", ShortCoins(cost)), CardBtnScale, ButtonTextRoom(inner - 2 * BtnGap - FixedW(_discharge) - FixedW(_moreBtn))), unlocked && cpOk && moneyOk, hint);
				}

				// "more" page: patrol, rebase, crystals the player is holding
				_patrol.Set(LevyUtil.T("UI.Patrol"), _owner.BannersNearby, _owner.BannersNearby ? LevyUtil.T("UI2.PatrolHint") : LevyUtil.T("UI2.NeedTwoBanners"));
				_feedLc.Enabled = UIActions.CanFeed(rec, ItemID.LifeCrystal);
				_feedLf.Enabled = UIActions.CanFeed(rec, ItemID.LifeFruit);
				_feedMc.Enabled = UIActions.CanFeed(rec, ItemID.ManaCrystal);
				bool showRebase = true; string rebaseText = null;
				if (rec.HomeBarracksId != te.ID) rebaseText = LevyUtil.T("UI2.RebaseHere");
				else if (_owner.OtherBarracks != null) rebaseText = LevyUtil.T("UI2.RebaseTo", SettlementSystem.Instance.TownName(_owner.OtherBarracks));
				else showRebase = false;
				if (showRebase) _rebase.Set(Fit(rebaseText, CardBtnScale, ButtonTextRoom(inner - BtnGap - FixedW(_patrol))), true, rebaseText);

				int sig = HashCode.Combine((int)_w, _more, showRebase, _owner.HasLifeCrystal, _owner.HasLifeFruit, _owner.HasManaCrystal, _promote.Text, _rebase.Text);
				if (sig != _sig)
				{
					_sig = sig;
					Arrange(showRebase);
				}
				if (Parent != null && !_inLayout) Recalculate();
			}

			private static float ButtonTextRoom(float buttonW) => Math.Max(20f, buttonW - 14f);

			private static float FixedW(LevyButton b) => Measure(b.Text, CardBtnScale) + 16f;

			/// <summary>Attaches and sizes the buttons of the current page (main actions or the "more" page).</summary>
			private void Arrange(bool showRebase)
			{
				foreach (var b in _allButtons) if (b.Parent != null) b.Remove();
				float inner = _w - 2f * Pad - LevyConstants.UI.CardPortraitWidth;
				float row2 = ActionY + RowH + RowGap;
				if (!_more)
				{
					_rowItems.Add((_follow, -1f)); _rowItems.Add((_hold, -1f)); _rowItems.Add((_return, -1f)); _rowItems.Add((_kit, -1f));
					LayoutRow(ActionY, inner);
					_rowItems.Add((_promote, -1f)); _rowItems.Add((_discharge, FixedW(_discharge))); _rowItems.Add((_moreBtn, FixedW(_moreBtn)));
					LayoutRow(row2, inner);
				}
				else
				{
					_rowItems.Add((_patrol, FixedW(_patrol)));
					if (showRebase) _rowItems.Add((_rebase, -1f));
					LayoutRow(ActionY, inner);
					_rowItems.Add((_back, FixedW(_back)));
					if (_owner.HasLifeCrystal) _rowItems.Add((_feedLc, -1f));
					if (_owner.HasLifeFruit) _rowItems.Add((_feedLf, -1f));
					if (_owner.HasManaCrystal) _rowItems.Add((_feedMc, -1f));
					LayoutRow(row2, inner);
				}
			}

			private void LayoutRow(float y, float inner)
			{
				float fixedSum = 0f; int flex = 0;
				foreach (var (_, fw) in _rowItems) { if (fw >= 0f) fixedSum += fw; else flex++; }
				float flexW = flex > 0 ? Math.Max(20f, (inner - BtnGap * (_rowItems.Count - 1) - fixedSum) / flex) : 0f;
				float x = Pad + LevyConstants.UI.CardPortraitWidth;
				foreach (var (b, fw) in _rowItems)
				{
					float bw = fw >= 0f ? fw : flexW;
					b.Left.Set(x, 0f); b.Top.Set(y, 0f); b.Width.Set(bw, 0f);
					if (b.Parent == null) Append(b);
					x += bw + BtnGap;
				}
				_rowItems.Clear();
			}

			/// <summary>Full detail text for the name-row tooltip.</summary>
			private string Details()
			{
				var rec = Rec; if (rec == null) return "";
				var home = SettlementSystem.Instance.GetBarracks(rec.HomeBarracksId);
				var lines = new List<string>
				{
					rec.Name + (rec.Veteran ? "  (" + LevyUtil.T("UI.Veteran") + ")" : ""),
					UIActions.Chevrons(rec.Grade) + " " + UIActions.GradeName(rec.Grade),
					LevyUtil.T("UI2.WagePerDay", LevyUtil.Coins(EconomySystem.WagePerDay(rec.Grade, rec.StoodDown))),
					LevyUtil.T("UI2.Home", home != null ? SettlementSystem.Instance.TownName(home) : LevyUtil.T("UI2.Homeless")),
					LevyUtil.T("UI2.Kills", rec.Kills),
					UIActions.StateText(rec),
				};
				if (rec.DaysUnpaid > 0) lines.Add(LevyUtil.T("UI.Unpaid", rec.DaysUnpaid));
				if (rec.StoodDown) lines.Add(LevyUtil.T("UI.StoodDown"));
				return string.Join("\n", lines);
			}

			private void OpenKit()
			{
				if (Rec == null) return;
				if (UISystem.CurrentLoadoutId == Rec.Id) UISystem.Instance.CloseSoldierLoadout();
				else UISystem.Instance.OpenSoldierLoadout(Rec);
			}

			private void Promote()
			{
				if (Rec == null || Rec.Grade >= Grade.Champion) return;
				UIActions.Promote(Rec, (Grade)((int)Rec.Grade + 1));
			}

			private void Patrol()
			{
				var te = _owner.Te; if (te == null) return;
				if (UIActions.FindPatrolPair(te.Center, LevyConstants.UI.PatrolBannerSearchTiles, out var a, out var b)) UIActions.AssignPatrol(Rec, a, b);
			}

			private void Rebase()
			{
				var te = _owner.Te; if (te == null || Rec == null) return;
				if (Rec.HomeBarracksId != te.ID) { UIActions.SetRebase(Rec, te); return; }
				var other = UIActions.NearestOtherBarracks(te.Center, te);
				if (other != null) UIActions.SetRebase(Rec, other);
			}
		}
	}
}
