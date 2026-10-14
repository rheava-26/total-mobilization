using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace Levy.Common.UI
{
	/// <summary>A text button with a hover hint, an enabled/disabled look and a click callback.</summary>
	public class LevyButton : UITextPanel<string>
	{
		public static readonly Color Normal = new Color(63, 82, 151) * 0.8f, Hover = new Color(93, 118, 200) * 0.9f, Disabled = new Color(40, 44, 60) * 0.8f;
		public string Hint;
		public bool Enabled = true;
		public Action OnClick;

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
			BackgroundColor = !Enabled ? Disabled : IsMouseHovering ? Hover : Normal;
			TextColor = Enabled ? Color.White : new Color(140, 140, 150);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (IsMouseHovering && !string.IsNullOrEmpty(Hint)) UICommon.TooltipMouseText(Hint);
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

	/// <summary>Plain label that never eats mouse events.</summary>
	public class Label : UIText
	{
		public Label(string text, float scale = 0.8f) : base(text, scale) { IgnoresMouseInteraction = true; }
	}
}
