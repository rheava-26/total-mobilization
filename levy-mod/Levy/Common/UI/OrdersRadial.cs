using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.UI;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>
	/// Seven buttons on an ellipse around the cursor: Follow / Hold / Return / Hold fire–Fire at will / Mobilize–Stand down / Rally / Re-arm.
	/// Opened by the War Horn right-click or the soldier chat button; closes after a click, on Escape or on right-click.
	/// </summary>
	public class OrdersRadial : UIState
	{
		private const int Count = 7;
		/// <summary>Horizontal radius of the button ring; wider than tall so the bottom pair of buttons clear each other.</summary>
		private const int RadiusX = 180;
		private UIElement _root;
		private readonly LevyButton[] _buttons = new LevyButton[Count];
		private Label _title;
		private uint _openedTick;
		private int _refreshTimer;
		private BarracksTE _nearest;

		private static int RootW => 2 * (RadiusX + LevyConstants.UI.RadialButtonWidth / 2) + 20;
		private static int RootH => 2 * (LevyConstants.UI.RadialRadius + LevyConstants.UI.RadialButtonHeight / 2) + 20;

		public override void OnInitialize()
		{
			_root = new UIElement();
			_root.Width.Set(RootW, 0f); _root.Height.Set(RootH, 0f);
			int bw = LevyConstants.UI.RadialButtonWidth, bh = LevyConstants.UI.RadialButtonHeight, r = LevyConstants.UI.RadialRadius;
			float cx = RootW * 0.5f, cy = RootH * 0.5f;
			for (int i = 0; i < Count; i++)
			{
				int idx = i;
				// Offset half a step so two buttons sit at the top and one at the bottom.
				double ang = -Math.PI / 2 + (i - 0.5) * (2 * Math.PI / Count);
				var b = new LevyButton("", LevyConstants.UI.SmallTextScale, bw, bh, () => Click(idx));
				b.Left.Set(cx + (float)Math.Cos(ang) * RadiusX - bw * 0.5f, 0f);
				b.Top.Set(cy + (float)Math.Sin(ang) * r - bh * 0.5f, 0f);
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
			_nearest = UIActions.NearestBarracks(Main.LocalPlayer.Center, LevyConstants.UI.MobilizeKeyRadiusTiles);
			bool holdFire = UIActions.MajorityHoldFire();
			_buttons[0].Set(LevyUtil.T("UI.Follow"), true, LevyUtil.T("UI2.FollowHint"));
			_buttons[1].Set(LevyUtil.T("UI.Hold"), true, LevyUtil.T("UI2.HoldHint"));
			_buttons[2].Set(LevyUtil.T("UI.Return"), true, LevyUtil.T("UI2.ReturnHint"));
			_buttons[3].Set(LevyUtil.T(holdFire ? "UI.FireAtWill" : "UI.HoldFire"), true, LevyUtil.T(holdFire ? "UI2.FireAtWillHint" : "UI2.HoldFireHint"));
			if (_nearest == null) _buttons[4].Set(LevyUtil.T("UI.Mobilize"), false, LevyUtil.T("UI2.NoBarracksNearby"));
			else _buttons[4].Set(LevyUtil.T(_nearest.Mobilized ? "UI.StandDown" : "UI.Mobilize"), true, LevyUtil.T(_nearest.Mobilized ? "UI2.StandDownHint" : "UI2.MobilizeHint") + " " + _nearest.TownName);
			_buttons[5].Set(LevyUtil.T("UI.Rally"), true, LevyUtil.T("UI2.RallyHint"));
			_buttons[6].Set(LevyUtil.T("UI.Rearm"), true, LevyUtil.T("UI.RearmHint"));
		}

		private void Click(int idx)
		{
			switch (idx)
			{
				case 0: UIActions.Order(SoldierOrder.Follow, false); break;
				case 1: UIActions.Order(SoldierOrder.Hold, false); break;
				case 2: UIActions.Order(SoldierOrder.ReturnToBarracks, false); break;
				case 3: UIActions.HoldFire(false); break;
				case 4: if (_nearest != null && UIActions.IsLive(_nearest)) UIActions.ToggleMobilize(_nearest); break;
				case 5: UIActions.Rally(); break;
				case 6: UIActions.Rearm(); break;
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
