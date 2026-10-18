using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Content.Items;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Eleven buttons on an ellipse around the cursor, clockwise from the top: Follow / Hold / Return / Search &amp; destroy (orders),
	/// Hold fire–Fire at will / Spread out–Close up / Focus target (combat), Mobilize–Stand down / Rally / Re-arm / Nameplates (muster).
	/// The ellipse is grown at start-up until no two buttons touch, so it stays readable for any button count.
	/// Opened by the War Horn right-click or the soldier chat button; closes after a click, on Escape or on right-click.
	/// </summary>
	public class OrdersRadial : UIState
	{
		private const int Count = 12;
		private const float GrowStep = 1.04f;
		private const int MaxGrowSteps = 60;
		private static float _radX, _radY;
		private static readonly Vector2[] _pos = new Vector2[Count];
		private UIElement _root;
		private readonly LevyButton[] _buttons = new LevyButton[Count];
		private Label _title;
		private uint _openedTick;
		private int _refreshTimer;
		private BarracksTE _nearest;
		private bool _radio;

		/// <summary>
		/// Button centres relative to the wheel centre. Starts from the base ellipse and grows both radii together until every pair of
		/// buttons is separated by <see cref="LevyConstants.UI.RadialGapPx"/> on at least one axis. Offset half a step so two buttons sit at the top.
		/// </summary>
		private static void ComputeLayout()
		{
			if (_radX > 0f) return;
			int bw = LevyConstants.UI.RadialButtonWidth, bh = LevyConstants.UI.RadialButtonHeight, gap = LevyConstants.UI.RadialGapPx;
			float a = LevyConstants.UI.RadialRadiusX, b = LevyConstants.UI.RadialRadius;
			for (int step = 0; step <= MaxGrowSteps; step++)
			{
				bool clear = true;
				for (int i = 0; i < Count; i++)
				{
					double ang = -Math.PI / 2 + (i - 0.5) * (2 * Math.PI / Count);
					_pos[i] = new Vector2((float)Math.Cos(ang) * a, (float)Math.Sin(ang) * b);
				}
				for (int i = 0; i < Count && clear; i++)
					for (int j = 0; j < i; j++)
						if (Math.Abs(_pos[i].X - _pos[j].X) < bw + gap && Math.Abs(_pos[i].Y - _pos[j].Y) < bh + gap) { clear = false; break; }
				if (clear) break;
				a *= GrowStep; b *= GrowStep;
			}
			_radX = a; _radY = b;
		}

		private static int RootW { get { ComputeLayout(); return (int)(2 * (_radX + LevyConstants.UI.RadialButtonWidth / 2) + 20); } }
		private static int RootH { get { ComputeLayout(); return (int)(2 * (_radY + LevyConstants.UI.RadialButtonHeight / 2) + 20); } }

		public override void OnInitialize()
		{
			ComputeLayout();
			_root = new UIElement();
			_root.Width.Set(RootW, 0f); _root.Height.Set(RootH, 0f);
			int bw = LevyConstants.UI.RadialButtonWidth, bh = LevyConstants.UI.RadialButtonHeight;
			float cx = RootW * 0.5f, cy = RootH * 0.5f;
			for (int i = 0; i < Count; i++)
			{
				int idx = i;
				var b = new LevyButton("", LevyConstants.UI.SmallTextScale, bw, bh, () => Click(idx));
				b.Left.Set(cx + _pos[i].X - bw * 0.5f, 0f);
				b.Top.Set(cy + _pos[i].Y - bh * 0.5f, 0f);
				_buttons[i] = b;
				_root.Append(b);
			}
			_title = new Label(LevyUtil.T("UI2.Orders"), 0.9f);
			_title.HAlign = 0.5f; _title.VAlign = 0.5f;
			_root.Append(_title);
			Append(_root);
		}

		/// <summary>Position the wheel on the cursor (kept inside the screen) and refresh the state-aware labels.</summary>
		public void Open()
		{
			_openedTick = Main.GameUpdateCount;
			float sw = Main.screenWidth / Main.UIScale, sh = Main.screenHeight / Main.UIScale;
			float halfW = RootW * 0.5f, halfH = RootH * 0.5f;
			var m = Main.MouseScreen;
			float x = MathHelper.Clamp(m.X, halfW, Math.Max(halfW, sw - halfW)) - halfW;
			float y = MathHelper.Clamp(m.Y, halfH, Math.Max(halfH, sh - halfH)) - halfH;
			_root.Left.Set(x, 0f); _root.Top.Set(y, 0f);
			Refresh();
			Recalculate();
		}

		private void Refresh()
		{
			// Command Radio in the pack: every order goes map-wide and the muster button reaches the nearest barracks anywhere.
			_radio = CommandRadio.Has(Main.LocalPlayer);
			_title.SetText(LevyUtil.T(_radio ? "UI.RadioOrders" : "UI2.Orders"));
			_nearest = UIActions.NearestBarracks(Main.LocalPlayer.Center, _radio ? float.MaxValue : LevyConstants.UI.MobilizeKeyRadiusTiles);
			bool holdFire = UIActions.MajorityHoldFire();
			bool spread = UIActions.MajoritySpreadOut();
			_buttons[0].Set(LevyUtil.T("UI.Follow"), true, LevyUtil.T(_radio ? "UI2.FollowHintRadio" : "UI2.FollowHint"));
			_buttons[1].Set(LevyUtil.T("UI.Hold"), true, LevyUtil.T(_radio ? "UI2.HoldHintRadio" : "UI2.HoldHint"));
			_buttons[2].Set(LevyUtil.T("UI.Return"), true, LevyUtil.T(_radio ? "UI2.ReturnHintRadio" : "UI2.ReturnHint"));
			_buttons[3].Set(LevyUtil.T("UI.SearchDestroy"), true, LevyUtil.T(_radio ? "UI2.SearchDestroyHintRadio" : "UI2.SearchDestroyHint"));
			_buttons[4].Set(LevyUtil.T("UI.FreeRein"), true, LevyUtil.T(_radio ? "UI2.FreeReinHintRadio" : "UI2.FreeReinHint"));
			_buttons[5].Set(LevyUtil.T(holdFire ? "UI.FireAtWill" : "UI.HoldFire"), true, LevyUtil.T(holdFire ? "UI2.FireAtWillHint" : "UI2.HoldFireHint"));
			_buttons[6].Set(LevyUtil.T(spread ? "UI.CloseUp" : "UI.SpreadOut"), true, LevyUtil.T(spread ? "UI2.CloseUpHint" : "UI2.SpreadOutHint"));
			_buttons[7].Set(LevyUtil.T("UI.FocusTarget"), true, LevyUtil.T("UI2.FocusTargetHint"));
			if (_nearest == null) _buttons[8].Set(LevyUtil.T("UI.Mobilize"), false, LevyUtil.T("UI2.NoBarracksNearby"));
			else _buttons[8].Set(LevyUtil.T(_nearest.Mobilized ? "UI.StandDown" : "UI.Mobilize"), true, LevyUtil.T(_nearest.Mobilized ? "UI2.StandDownHint" : "UI2.MobilizeHint") + " " + _nearest.TownName);
			_buttons[9].Set(LevyUtil.T("UI.Rally"), true, LevyUtil.T("UI2.RallyHint"));
			_buttons[10].Set(LevyUtil.T("UI.Rearm"), true, LevyUtil.T("UI.RearmHint"));
			_buttons[11].Set(LevyUtil.T(UISystem.NameplatesForced ? "UI.NamesOff" : "UI.NamesOn"), true, LevyUtil.T("UI.NamesHint"));
		}

		private void Click(int idx)
		{
			switch (idx)
			{
				case 0: UIActions.Order(SoldierOrder.Follow, _radio); break;
				case 1: UIActions.Order(SoldierOrder.Hold, _radio); break;
				case 2: UIActions.Order(SoldierOrder.ReturnToBarracks, _radio); break;
				case 3: UIActions.Order(SoldierOrder.SearchDestroy, _radio); break;
				case 4: UIActions.Order(SoldierOrder.FreeRein, _radio); break;
				case 5: UIActions.HoldFire(_radio); break;
				case 6: UIActions.SpreadOut(_radio); break;
				case 7: UISystem.Instance.BeginFocusDesignate(); break;
				case 8: if (_nearest != null && UIActions.IsLive(_nearest)) UIActions.ToggleMobilize(_nearest); break;
				case 9: UIActions.Rally(); break;
				case 10: UIActions.Rearm(); break;
				case 11: UISystem.Instance.ToggleNameplates(); break;
			}
			UISystem.Instance.CloseRadial();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_root.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (++_refreshTimer >= 10) { _refreshTimer = 0; Refresh(); }

			bool pastGrace = Main.GameUpdateCount - _openedTick > LevyConstants.UI.RadialInputGraceTicks;
			if (!pastGrace) return;
			bool escape = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
			bool rightClick = Main.mouseRight && Main.mouseRightRelease;
			if (escape || rightClick) UISystem.Instance.CloseRadial();
		}
	}
}
