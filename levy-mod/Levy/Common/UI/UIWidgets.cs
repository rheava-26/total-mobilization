using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Levy.Common.UI
{
	/// <summary>A text button with a hover hint, an enabled/disabled look and a click callback.</summary>
	public class LevyButton : UITextPanel<string>
	{
		public static readonly Color Normal = new Color(63, 82, 151) * 0.8f, Hover = new Color(93, 118, 200) * 0.9f, Disabled = new Color(40, 44, 60) * 0.8f;
		public string Hint;
		public bool Enabled = true;
		/// <summary>Drawn in the lit colour while true (a filter that is currently narrowing the view).</summary>
		public bool Lit;
		public Action OnClick;
		/// <summary>Optional right-click action (the cycle buttons step backwards with it).</summary>
		public Action OnAltClick;

		public LevyButton(string text, float scale, float width, float height, Action onClick = null, string hint = null) : base(text, scale)
		{
			OnClick = onClick; Hint = hint;
			SetPadding(0); PaddingLeft = PaddingRight = 6f; PaddingTop = PaddingBottom = 2f;
			Width.Set(width, 0f); Height.Set(height, 0f);
			BackgroundColor = Normal;
			OnLeftClick += (evt, el) =>
			{
				if (!Enabled) return;
				SoundEngine.PlaySound(SoundID.MenuTick);
				OnClick?.Invoke();
			};
			OnRightClick += (evt, el) =>
			{
				if (!Enabled || OnAltClick == null) return;
				SoundEngine.PlaySound(SoundID.MenuTick);
				OnAltClick.Invoke();
			};
		}

		public void Set(string text, bool enabled, string hint = null)
		{
			if (Text != text) SetText(text);
			Enabled = enabled;
			if (hint != null) Hint = hint;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering) Main.LocalPlayer.mouseInterface = true;
			BackgroundColor = !Enabled ? Disabled : IsMouseHovering ? Hover : Lit ? LevyConstants.UI.ButtonLit : Normal;
			TextColor = Enabled ? Color.White : new Color(140, 140, 150);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (IsMouseHovering && !string.IsNullOrEmpty(Hint)) HintTooltip.Queue(Hint);
		}
	}

	/// <summary>
	/// Plain text tooltip for UI hints. Deliberately NOT UICommon.TooltipMouseText: with the opaque-box setting on, that fabricates a fake
	/// Item and runs it through the vanilla item-tooltip pipeline, which appends item lines such as "Material" (item.material is taken from
	/// ItemID.Sets.IsAMaterial for the fake item). This draws the same panel and text itself, so only the hint shows.
	/// The hint is queued during UI drawing and drawn from PostDrawInterface so no later widget can paint over it.
	/// </summary>
	public class HintTooltip : ModSystem
	{
		private const int MaxWidth = 340;
		private static string _pending;

		public static void Queue(string text) => _pending = text;

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			string text = _pending;
			_pending = null;
			if (Main.dedServ || string.IsNullOrEmpty(text)) return;
			Draw(spriteBatch, text);
		}

		private static List<string> Wrap(string text, ReLogic.Graphics.DynamicSpriteFont font)
		{
			var lines = new List<string>();
			foreach (string para in text.Split('\n'))
			{
				string cur = "";
				foreach (string word in para.Split(' '))
				{
					string trial = cur.Length == 0 ? word : cur + " " + word;
					if (cur.Length > 0 && font.MeasureString(trial).X > MaxWidth) { lines.Add(cur); cur = word; }
					else cur = trial;
				}
				lines.Add(cur);
			}
			return lines;
		}

		private static void Draw(SpriteBatch spriteBatch, string text)
		{
			var font = FontAssets.MouseText.Value;
			if (font == null) return;
			List<string> lines = Wrap(text, font);
			float width = 0f;
			foreach (string l in lines) width = Math.Max(width, font.MeasureString(l).X);
			int lineH = Math.Max(24, (int)font.LineSpacing);
			float height = lineH * lines.Count;

			bool box = Main.SettingsEnabled_OpaqueBoxBehindTooltips;
			float x = Main.mouseX + 16, y = Main.mouseY + 16;
			int margin = 4;
			if (box) { x += 8; y += 2; margin = 18; }
			if (x + width + margin > Main.screenWidth) x = Main.screenWidth - width - margin;
			if (y + height + margin > Main.screenHeight) y = Main.screenHeight - height - margin;
			if (x < 0) x = 0;
			if (y < 0) y = 0;

			byte c = Main.mouseTextColor;
			Color color = box ? Color.White : new Color(c, c, c, c);
			if (box)
				Utils.DrawInvBG(spriteBatch, new Rectangle((int)x - 10, (int)y - 5, (int)width + 20, (int)height + 7), new Color(23, 25, 81, 255) * 0.925f * 0.85f);
			for (int i = 0; i < lines.Count; i++)
				Utils.DrawBorderStringFourWay(spriteBatch, font, lines[i], x, y + i * lineH, color, Color.Black, Vector2.Zero);
		}
	}

	/// <summary>A UIPanel the player can drag by its background; stays on screen; blocks item use under it.</summary>
	public class DraggablePanel : UIPanel
	{
		private Vector2 _offset;
		private bool _dragging;

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			if (evt.Target == this) { _offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels); _dragging = true; }
		}

		public override void LeftMouseUp(UIMouseEvent evt)
		{
			base.LeftMouseUp(evt);
			if (_dragging) { _dragging = false; Left.Set(evt.MousePosition.X - _offset.X, 0f); Top.Set(evt.MousePosition.Y - _offset.Y, 0f); Recalculate(); }
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			if (_dragging)
			{
				if (!Main.mouseLeft) _dragging = false;
				Left.Set(Main.mouseX - _offset.X, 0f); Top.Set(Main.mouseY - _offset.Y, 0f);
				Recalculate();
			}
			if (Parent == null) return;
			var parentSpace = Parent.GetDimensions().ToRectangle();
			var mine = GetDimensions().ToRectangle();
			if (mine.Left < parentSpace.Left || mine.Top < parentSpace.Top || mine.Right > parentSpace.Right || mine.Bottom > parentSpace.Bottom)
			{
				Left.Pixels = Utils.Clamp(Left.Pixels, 0f, Math.Max(0f, parentSpace.Width - Width.Pixels));
				Top.Pixels = Utils.Clamp(Top.Pixels, 0f, Math.Max(0f, parentSpace.Height - Height.Pixels));
				Recalculate();
			}
		}
	}

	/// <summary>A filled bar (MagicPixel) with a centred label. Set <see cref="Fraction"/> and <see cref="Label"/> each refresh.</summary>
	public class BarElement : UIElement
	{
		public float Fraction;
		public string Label = "";
		public Color Fill = new(90, 170, 90), Back = new(20, 20, 30, 220);
		public float TextScale = 0.75f;

		public BarElement(float width, float height) { Width.Set(width, 0f); Height.Set(height, 0f); IgnoresMouseInteraction = true; }

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var d = GetDimensions();
			var pixel = TextureAssets.MagicPixel.Value;
			var r = d.ToRectangle();
			spriteBatch.Draw(pixel, r, Back);
			int w = (int)(r.Width * MathHelper.Clamp(Fraction, 0f, 1f));
			if (w > 0) spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, w, r.Height), Fill);
			if (!string.IsNullOrEmpty(Label))
				Utils.DrawBorderString(spriteBatch, Label, new Vector2(r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f + 2f), Color.White, TextScale, 0.5f, 0.5f);
		}
	}

	/// <summary>
	/// Terraria-HUD style stat row: a line of hearts (<c>star = false</c>, life) or mana stars (<c>star = true</c>, mana), drawn with
	/// the game's own Heart / Mana textures. A row holds as many icons as fit beside the small <see cref="Label"/> (the exact numbers); each icon stands for
	/// <see cref="PerIcon"/> units, picked from <see cref="LevyConstants.UI.IconValueSteps"/> so even a big pool stays a single short row (the icon count is
	/// also capped at what fits, so the row never spills). A part-filled icon is drawn smaller and fainter, like vanilla's partial hearts.
	/// <see cref="Dim"/> darkens the whole row (a fallen soldier). Hovering shows <see cref="Hint"/> plus what one icon is worth.
	/// </summary>
	public class IconRow : UIElement
	{
		public int Value, Max;
		public string Label = "", Hint = "", PerIconFormat = "";
		/// <summary>Icon draw size and icon-to-icon pitch (px), label scale and vertical nudge; defaults are the roomy header sizes, the card sets compact ones.</summary>
		public int IconSize = LevyConstants.UI.StatIconSize, Pitch = LevyConstants.UI.StatIconPitch, LabelDy = 2;
		public float LabelScale = LevyConstants.UI.IconLabelScale;
		/// <summary>Multiplier on every colour drawn (1 = normal).</summary>
		public float Dim = 1f;
		private readonly bool _star;
		private int _step = 1;

		public IconRow(float width, bool star, float height = LevyConstants.UI.IconRowHeight) { _star = star; Width.Set(width, 0f); Height.Set(height, 0f); }

		/// <summary>Units each icon stands for in the last draw.</summary>
		public int PerIcon => _step;

		/// <summary>The smallest nice step for which <paramref name="max"/> needs at most <paramref name="slots"/> icons.</summary>
		public static int PickStep(int max, int slots)
		{
			var steps = LevyConstants.UI.IconValueSteps;
			foreach (int s in steps) if ((max + s - 1) / s <= slots) return s;
			return steps[steps.Length - 1];
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			var font = FontAssets.MouseText.Value;
			float labelScale = LabelScale;
			float labelW = string.IsNullOrEmpty(Label) || font == null ? 0f : font.MeasureString(Label).X * labelScale;
			int pitch = Pitch;
			int room = r.Width - (int)labelW - (labelW > 0f ? LevyConstants.UI.IconLabelGap : 0);
			int slots = Math.Max(1, room / pitch);
			_step = PickStep(Math.Max(0, Max), slots);
			int n = Max <= 0 ? 0 : Math.Min(slots, (Max + _step - 1) / _step); // even the coarsest step is clamped to what fits

			var tex = (_star ? TextureAssets.Mana : TextureAssets.Heart).Value;
			if (tex != null)
			{
				float baseScale = IconSize / (float)Math.Max(tex.Width, tex.Height);
				var origin = new Vector2(tex.Width * 0.5f, tex.Height * 0.5f);
				for (int i = 0; i < n; i++)
				{
					var center = new Vector2(r.X + i * pitch + pitch * 0.5f, r.Y + r.Height * 0.5f);
					spriteBatch.Draw(tex, center, null, LevyConstants.UI.IconEmptyColor * Dim, 0f, origin, baseScale, SpriteEffects.None, 0f);
					float frac = MathHelper.Clamp((Value - i * _step) / (float)_step, 0f, 1f);
					if (frac <= 0f) continue;
					float k = LevyConstants.UI.IconPartialMinScale + (1f - LevyConstants.UI.IconPartialMinScale) * frac;
					spriteBatch.Draw(tex, center, null, Color.White * ((0.5f + 0.5f * frac) * Dim), 0f, origin, baseScale * k, SpriteEffects.None, 0f);
				}
			}
			if (labelW > 0f)
				Utils.DrawBorderString(spriteBatch, Label, new Vector2(r.Right - labelW, r.Y + r.Height * 0.5f + LabelDy), Color.White * Dim, labelScale, 0f, 0.5f);

			if (IsMouseHovering && !string.IsNullOrEmpty(Hint))
				HintTooltip.Queue(string.IsNullOrEmpty(PerIconFormat) ? Hint : Hint + "\n" + string.Format(PerIconFormat, _step));
		}
	}

	/// <summary>
	/// A bottom-right corner grab handle. Press and drag: <see cref="OnDrag"/> is called every frame with the mouse movement (UI pixels) since the
	/// press; <see cref="OnRelease"/> once when the button comes up. The owner turns that into a new panel size (and clamps it).
	/// </summary>
	public class ResizeGrip : UIElement
	{
		public Action<float, float> OnDrag;
		public Action OnPress, OnRelease;
		public string Hint;
		private bool _dragging;
		private Vector2 _start;

		public ResizeGrip(int size, string hint = null) { Width.Set(size, 0f); Height.Set(size, 0f); Hint = hint; }

		/// <summary>The mouse in UI-space pixels (screen pixels divided by the UI scale).</summary>
		private static Vector2 Mouse => Main.MouseScreen / Main.UIScale;

		public bool Dragging => _dragging;

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			_dragging = true; _start = Mouse;
			OnPress?.Invoke();
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering || _dragging) Main.LocalPlayer.mouseInterface = true;
			if (!_dragging) return;
			if (!Main.mouseLeft) { _dragging = false; OnRelease?.Invoke(); return; }
			var d = Mouse - _start;
			OnDrag?.Invoke(d.X, d.Y);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			var px = TextureAssets.MagicPixel.Value;
			var c = (_dragging || IsMouseHovering) ? Color.White : new Color(170, 180, 215);
			// three diagonal ridges toward the corner, like a storage-window resize handle
			for (int i = 0; i < 3; i++)
			{
				int off = 4 + i * 5; // distance of the ridge from the corner
				for (int k = 0; k < off; k++) spriteBatch.Draw(px, new Rectangle(r.Right - 3 - k, r.Bottom - 3 - (off - k), 2, 2), c);
			}
			if (IsMouseHovering && !_dragging && !string.IsNullOrEmpty(Hint)) HintTooltip.Queue(Hint);
		}
	}

	/// <summary>Plain label that never eats mouse events.</summary>
	public class Label : UIText
	{
		public Label(string text, float scale = 0.8f) : base(text, scale) { IgnoresMouseInteraction = true; }
	}
}
