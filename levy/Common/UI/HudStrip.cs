using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader.UI;
using Terraria.UI;
using Levy.Common.Soldiers;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>Bottom-left strip: one head per Regular of the local player with an HP bar; dead ones grey out with a respawn countdown. Toggled with the ArmyStrip keybind.</summary>
	public class HudStrip : UIState
	{
		private StripElement _strip;

		public override void OnInitialize()
		{
			_strip = new StripElement();
			_strip.Left.Set(LevyConstants.UI.HudLeft, 0f);
			_strip.VAlign = 1f;
			_strip.Top.Set(-LevyConstants.UI.HudBottomMargin, 0f);
			Append(_strip);
		}

		private class StripElement : UIElement
		{
			private const int TitleHeight = 16, Gap = 4;
			private readonly List<SoldierRecord> _recs = new();
			private int _refresh;
			private static readonly Color Back = new(10, 12, 24, 170), DeadBox = new(40, 40, 48), IdleBox = new(70, 74, 90), Dark = new(20, 20, 30, 220);

			public StripElement() { Rebuild(); }

			private void Rebuild()
			{
				_recs.Clear();
				foreach (var r in UIActions.MyRegulars()) _recs.Add(r);
				int pitch = LevyConstants.UI.HudEntryPitch;
				Width.Set(Math.Max(1, _recs.Count) * pitch + 8, 0f);
				Height.Set(TitleHeight + LevyConstants.UI.HudHeadSize + Gap + LevyConstants.UI.HudBarHeight + 10, 0f);
				Recalculate();
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				if (++_refresh >= 10) { _refresh = 0; Rebuild(); }
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				var d = GetDimensions();
				var pixel = TextureAssets.MagicPixel.Value;
				var area = d.ToRectangle();
				spriteBatch.Draw(pixel, area, Back);
				Utils.DrawBorderString(spriteBatch, LevyUtil.T("UI2.HudTitle") + " " + _recs.Count, new Vector2(area.X + 4, area.Y + 1), new Color(220, 220, 230), 0.7f);
				if (_recs.Count == 0)
				{
					Utils.DrawBorderString(spriteBatch, LevyUtil.T("UI2.NoRegulars"), new Vector2(area.X + 4, area.Y + TitleHeight + 4), new Color(170, 170, 180), 0.65f);
					return;
				}

				int pitch = LevyConstants.UI.HudEntryPitch, head = LevyConstants.UI.HudHeadSize, barH = LevyConstants.UI.HudBarHeight;
				var mouse = Main.MouseScreen;
				for (int i = 0; i < _recs.Count; i++)
				{
					var rec = _recs[i];
					if (rec == null) continue;
					int x = area.X + 4 + i * pitch;
					int y = area.Y + TitleHeight;
					var box = new Rectangle(x + (pitch - head) / 2, y, head, head);
					var body = UIActions.BodyOf(rec);
					bool drewHead = false;
					if (body != null && body.Dummy != null && !rec.Dead)
					{
						try
						{
							Main.PlayerRenderer.DrawPlayerHead(Main.Camera, body.Dummy, box.Center.ToVector2(), 1f, LevyConstants.UI.HudHeadScale, Color.White);
							drewHead = true;
						}
						catch (Exception) { drewHead = false; }
					}
					if (!drewHead)
					{
						spriteBatch.Draw(pixel, box, rec.Dead ? DeadBox : IdleBox);
						string mark = rec.Dead ? (Math.Max(0, rec.RespawnTimer) / 60).ToString() : (string.IsNullOrEmpty(rec.Name) ? "?" : rec.Name.Substring(0, 1));
						Utils.DrawBorderString(spriteBatch, mark, box.Center.ToVector2() + new Vector2(0f, 2f), rec.Dead ? new Color(200, 120, 120) : Color.White, 0.8f, 0.5f, 0.5f);
					}

					// HP bar
					UIActions.GetHp(rec, out int cur, out int max);
					float frac = MathHelper.Clamp(cur / (float)Math.Max(1, max), 0f, 1f);
					var bar = new Rectangle(box.X, box.Bottom + Gap, head, barH);
					spriteBatch.Draw(pixel, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), Dark);
					if (frac > 0f) spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * frac), bar.Height), Color.Lerp(Color.Red, Color.LimeGreen, frac));
					if (rec.Veteran) Utils.DrawBorderString(spriteBatch, "^", new Vector2(box.Right - 2, box.Y - 2), new Color(255, 215, 80), 0.7f, 1f, 0f);

					var hit = new Rectangle(x, y, pitch, head + Gap + barH);
					if (hit.Contains(mouse.ToPoint()))
						UICommon.TooltipMouseText(LevyUtil.T("UI2.HudHover", rec.Name, UIActions.StateText(rec)) + "  " + LevyUtil.T("UI2.Hp", cur, max));
				}
			}
		}
	}
}
