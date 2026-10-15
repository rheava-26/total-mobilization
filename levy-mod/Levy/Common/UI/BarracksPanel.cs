using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
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
	/// the local player's Regulars with per-soldier actions. Everything goes through <see cref="UIActions"/>.
	/// </summary>
	public class BarracksPanel : UIState
	{
		public BarracksTE Te { get; private set; }

		private DraggablePanel _panel;
		private Label _title, _treasury, _rosterTitle, _allLabel, _empty;
		private LevyButton _close, _dep1, _dep10, _depAll, _wd1, _wd10, _mobilize, _autoMuster, _tax, _hazard, _recruit, _enlistLevy, _enlistMaa, _enlistChamp;
		private LevyButton _allFollow, _allHold, _allReturn, _allHoldFire;
		private BarElement _manpower, _cp;
		private UIList _list;
		private UIScrollbar _scroll;
		private readonly List<RosterRow> _rows = new();
		private int _refreshTimer;

		private const float BtnScale = LevyConstants.UI.ButtonTextScale;
		private const int W = LevyConstants.UI.BarracksPanelWidth, H = LevyConstants.UI.BarracksPanelHeight;
		private const int ListTop = 234;

		public override void OnInitialize()
		{
			_panel = new DraggablePanel();
			_panel.SetPadding(0);
			_panel.Left.Set(LevyConstants.UI.PanelDefaultLeft, 0f); _panel.Top.Set(LevyConstants.UI.PanelDefaultTop, 0f);
			_panel.Width.Set(W, 0f); _panel.Height.Set(H, 0f);
			_panel.BackgroundColor = new Color(33, 43, 79) * 0.9f;

			_title = Put(new Label("", 1f), 12, 8);
			_close = Put(new LevyButton("X", BtnScale, 26, 26, () => UISystem.Instance.CloseBarracks(), LevyUtil.T("UI2.Close")), W - 36, 6);

			// treasury
			_treasury = Put(new Label("", 0.85f), 12, 36);
			string depHint = LevyUtil.T("UI2.DepositHint"), wdHint = LevyUtil.T("UI2.WithdrawHint");
			_dep1 = Put(new LevyButton(LevyUtil.T("UI2.DepositOneGold"), 0.7f, 70, 24, () => UIActions.Deposit(Te, LevyConstants.UI.DepositSmall), depHint), 12, 58);
			_dep10 = Put(new LevyButton(LevyUtil.T("UI2.DepositTenGold"), 0.7f, 78, 24, () => UIActions.Deposit(Te, LevyConstants.UI.DepositLarge), depHint), 86, 58);
			_depAll = Put(new LevyButton(LevyUtil.T("UI2.DepositAll"), 0.7f, 92, 24, () => UIActions.Deposit(Te, UIActions.InventoryCoins()), depHint), 168, 58);
			_wd1 = Put(new LevyButton(LevyUtil.T("UI2.WithdrawOneGold"), 0.7f, 70, 24, () => UIActions.Withdraw(Te, LevyConstants.UI.DepositSmall), wdHint), 268, 58);
			_wd10 = Put(new LevyButton(LevyUtil.T("UI2.WithdrawTenGold"), 0.7f, 78, 24, () => UIActions.Withdraw(Te, LevyConstants.UI.DepositLarge), wdHint), 342, 58);
			_mobilize = Put(new LevyButton("", BtnScale, 118, 24, () => UIActions.ToggleMobilize(Te)), W - 12 - 118, 58);

			// bars
			_manpower = Put(new BarElement(260, 20) { Fill = new Color(90, 160, 90) }, 12, 88);
			_cp = Put(new BarElement(266, 20) { Fill = new Color(170, 140, 60) }, 282, 88);

			// settlement toggles + extras
			_autoMuster = Put(new LevyButton("", 0.7f, 150, 24, () => UIActions.BarracksToggle(Te, 0, !Te.AutoMuster)), 12, 116);
			_tax = Put(new LevyButton("", 0.7f, 170, 24, () => UIActions.BarracksToggle(Te, 1, !Te.TaxToTreasury), LevyUtil.T("UI.TaxToTreasury")), 168, 116);
			_hazard = Put(new LevyButton("", 0.7f, 204, 24, () => UIActions.HazardPay(Te)), W - 12 - 204, 116);
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

			// roster list
			_list = new UIList();
			_list.Left.Set(12f, 0f); _list.Top.Set(ListTop, 0f);
			_list.Width.Set(W - 12 - 12 - 24, 0f); _list.Height.Set(H - ListTop - 10, 0f);
			_list.ListPadding = 4f;
			_list.ManualSortMethod = _ => { };
			_panel.Append(_list);
			_scroll = new UIScrollbar();
			_scroll.Left.Set(W - 12 - 20, 0f); _scroll.Top.Set(ListTop + 4, 0f);
			_scroll.Height.Set(H - ListTop - 18, 0f);
			_panel.Append(_scroll);
			_list.SetScrollbar(_scroll);
			_empty = Put(new Label(LevyUtil.T("UI2.NoRegulars"), 0.8f), 16, ListTop + 8);

			Append(_panel);
		}

		private T Put<T>(T el, float left, float top) where T : UIElement
		{
			el.Left.Set(left, 0f); el.Top.Set(top, 0f);
			_panel.Append(el);
			return el;
		}

		public void Bind(BarracksTE te)
		{
			Te = te;
			if (_panel == null) return;
			float sh = Main.screenHeight / Main.UIScale;
			_panel.Top.Set(Math.Max(0f, Math.Min(LevyConstants.UI.PanelDefaultTop, sh - H - 10f)), 0f);
			_refreshTimer = 0;
			RebuildRows();
			Refresh();
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Te == null || !UIActions.IsLive(Te)) { UISystem.Instance.CloseBarracks(); return; }
			if (++_refreshTimer >= LevyConstants.UI.RosterRefreshTicks)
			{
				_refreshTimer = 0;
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

			bool any = _rows.Count > 0;
			if (any && _empty.Parent != null) _empty.Remove();
			else if (!any && _empty.Parent == null) _panel.Append(_empty);
			foreach (var row in _rows) row.Refresh();
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

		// ------------------------------------------------------------------ roster rows
		private bool RosterChanged()
		{
			int i = 0;
			foreach (var r in UIActions.MyRegulars())
			{
				if (i >= _rows.Count || !ReferenceEquals(_rows[i].Rec, r)) return true;
				i++;
			}
			return i != _rows.Count;
		}

		private void RebuildRows()
		{
			_list.Clear();
			_rows.Clear();
			foreach (var r in UIActions.MyRegulars())
			{
				var row = new RosterRow(this, r);
				_rows.Add(row);
				_list.Add(row);
			}
			_list.Recalculate();
		}

		private class RosterRow : UIPanel
		{
			public readonly SoldierRecord Rec;
			private readonly BarracksPanel _owner;
			private readonly Label _name, _grade, _hp, _state, _line2;
			private readonly LevyButton _promote, _discharge, _follow, _hold, _return, _kit, _patrol, _feedLc, _feedLf, _feedMc, _rebase;
			private readonly List<LevyButton> _line4 = new();
			private static readonly Color Gold = new(255, 215, 80);

			public RosterRow(BarracksPanel owner, SoldierRecord rec)
			{
				_owner = owner; Rec = rec;
				Width.Set(0f, 1f); Height.Set(LevyConstants.UI.RosterRowHeight, 0f);
				SetPadding(4);
				BackgroundColor = new Color(45, 58, 110) * 0.8f;

				_name = Add(new Label("", 0.85f), 2, 0);
				_grade = Add(new Label("", 0.75f), 180, 3); _grade.TextColor = Gold;
				_hp = Add(new Label("", 0.75f), 330, 3);
				_state = Add(new Label("", 0.75f), 410, 3);
				_line2 = Add(new Label("", 0.7f), 2, 20);

				_promote = Add(new LevyButton("", 0.7f, 150, 24, Promote), 0, 40);
				_discharge = Add(new LevyButton(LevyUtil.T("UI.Discharge"), 0.7f, 80, 24, () => UIActions.Discharge(Rec)), 154, 40);
				_follow = Add(new LevyButton(LevyUtil.T("UI2.FollowAll"), 0.7f, 66, 24, () => UIActions.SoldierOrder(Rec, SoldierOrder.Follow), LevyUtil.T("UI.Follow")), 238, 40);
				_hold = Add(new LevyButton(LevyUtil.T("UI2.HoldAll"), 0.7f, 60, 24, () => UIActions.SoldierOrder(Rec, SoldierOrder.Hold), LevyUtil.T("UI.Hold")), 308, 40);
				_return = Add(new LevyButton(LevyUtil.T("UI2.ReturnAll"), 0.7f, 70, 24, () => UIActions.SoldierOrder(Rec, SoldierOrder.ReturnToBarracks), LevyUtil.T("UI.Return")), 372, 40);
				_kit = Add(new LevyButton(LevyUtil.T("Loadout.Button"), 0.7f, 56, 24, OpenKit, LevyUtil.T("Loadout.ButtonHint")), 446, 40);

				_patrol = new LevyButton(LevyUtil.T("UI.Patrol"), 0.7f, 110, 24, Patrol, LevyUtil.T("UI2.PatrolHint"));
				_feedLc = new LevyButton(LevyUtil.T("UI2.FeedLifeCrystal"), 0.7f, 104, 24, () => UIActions.FeedCrystal(Rec, ItemID.LifeCrystal));
				_feedLf = new LevyButton(LevyUtil.T("UI2.FeedLifeFruit"), 0.7f, 92, 24, () => UIActions.FeedCrystal(Rec, ItemID.LifeFruit));
				_feedMc = new LevyButton(LevyUtil.T("UI2.FeedManaCrystal"), 0.7f, 110, 24, () => UIActions.FeedCrystal(Rec, ItemID.ManaCrystal));
				_rebase = new LevyButton("", 0.7f, 150, 24, Rebase);
				_line4.Add(_patrol); _line4.Add(_feedLc); _line4.Add(_feedLf); _line4.Add(_feedMc); _line4.Add(_rebase);
				Refresh();
			}

			private T Add<T>(T el, float left, float top) where T : UIElement { el.Left.Set(left, 0f); el.Top.Set(top, 0f); Append(el); return el; }

			public void Refresh()
			{
				var rec = Rec; var te = _owner.Te;
				if (rec == null || te == null) return;
				var roster = RosterSystem.Instance;
				string name = rec.Name ?? "";
				_name.SetText(name);
				float nameW = FontAssets.MouseText.Value.MeasureString(name).X * 0.85f;
				_grade.Left.Set(Math.Min(176f, 6f + nameW), 0f);
				_grade.SetText(UIActions.Chevrons(rec.Grade) + " " + UIActions.GradeName(rec.Grade) + (rec.Veteran ? "  " + LevyUtil.T("UI.Veteran") : ""));
				UIActions.GetHp(rec, out int cur, out int max);
				_hp.SetText(LevyUtil.T("UI2.Hp", cur, max));
				_hp.TextColor = cur <= 0 ? new Color(200, 100, 100) : Color.Lerp(new Color(230, 120, 120), Color.White, MathHelper.Clamp(cur / (float)max, 0f, 1f));
				_state.SetText(UIActions.StateText(rec));

				var home = SettlementSystem.Instance.GetBarracks(rec.HomeBarracksId);
				string line2 = LevyUtil.T("UI2.WagePerDay", LevyUtil.Coins(EconomySystem.WagePerDay(rec.Grade, rec.StoodDown)))
					+ "   " + LevyUtil.T("UI2.Home", home != null ? SettlementSystem.Instance.TownName(home) : LevyUtil.T("UI2.Homeless"))
					+ "   " + LevyUtil.T("UI2.Kills", rec.Kills);
				if (rec.DaysUnpaid > 0) line2 += "   " + LevyUtil.T("UI.Unpaid", rec.DaysUnpaid);
				if (rec.StoodDown) line2 += "   " + LevyUtil.T("UI.StoodDown");
				_line2.SetText(line2);
				_line2.TextColor = rec.DaysUnpaid >= 2 ? new Color(255, 150, 120) : rec.StoodDown ? new Color(170, 170, 180) : Color.White;

				// promote
				if (rec.Grade >= Grade.Champion) _promote.Set(LevyUtil.T("UI2.PromoteMax"), false, LevyUtil.T("UI2.PromoteMax"));
				else
				{
					var next = (Grade)((int)rec.Grade + 1);
					long cost = EconomySystem.PromotionCost(rec.Grade, next);
					bool unlocked = roster.GradeUnlocked(next);
					bool cpOk = UIActions.AvailableCp >= LevyConstants.GradeCpCost[(int)next] - LevyConstants.GradeCpCost[(int)rec.Grade];
					var payer = home ?? te;
					bool moneyOk = payer.Treasury >= cost;
					string hint = !unlocked ? LevyUtil.T("UI.GradeLocked") : !cpOk ? LevyUtil.T("UI.NotEnoughCp") : !moneyOk ? LevyUtil.T("UI.NotEnoughMoney") : UIActions.GradeName(next);
					_promote.Set(LevyUtil.T("UI.Promote", LevyUtil.Coins(cost)), unlocked && cpOk && moneyOk, hint);
				}

				// line 4: patrol, crystals held by the player, rebase
				bool banners = UIActions.FindPatrolPair(te.Center, LevyConstants.UI.PatrolBannerSearchTiles, out _, out _);
				_patrol.Set(LevyUtil.T("UI.Patrol"), banners, banners ? LevyUtil.T("UI2.PatrolHint") : LevyUtil.T("UI2.NeedTwoBanners"));
				var me = Main.LocalPlayer;
				bool showLc = me.HasItem(ItemID.LifeCrystal), showLf = me.HasItem(ItemID.LifeFruit), showMc = me.HasItem(ItemID.ManaCrystal);
				_feedLc.Enabled = UIActions.CanFeed(rec, ItemID.LifeCrystal);
				_feedLf.Enabled = UIActions.CanFeed(rec, ItemID.LifeFruit);
				_feedMc.Enabled = UIActions.CanFeed(rec, ItemID.ManaCrystal);
				bool showRebase;
				if (rec.HomeBarracksId != te.ID) { _rebase.Set(LevyUtil.T("UI2.RebaseHere"), true); showRebase = true; }
				else
				{
					var other = UIActions.NearestOtherBarracks(te.Center, te);
					showRebase = other != null;
					if (showRebase) _rebase.Set(LevyUtil.T("UI2.RebaseTo", SettlementSystem.Instance.TownName(other)), true);
				}
				float x = 0f;
				Place(_patrol, true, ref x);
				Place(_feedLc, showLc, ref x);
				Place(_feedLf, showLf, ref x);
				Place(_feedMc, showMc, ref x);
				Place(_rebase, showRebase, ref x);
			}

			private void Place(LevyButton b, bool show, ref float x)
			{
				if (!show) { if (b.Parent != null) b.Remove(); return; }
				if (b.Parent == null) Append(b);
				b.Left.Set(x, 0f); b.Top.Set(68f, 0f);
				x += b.Width.Pixels + 4f;
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
