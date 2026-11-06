using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Util;

namespace Levy.Common.UI
{
	/// <summary>
	/// A number the player edits with minus and plus buttons or by typing into the box. <see cref="Get"/> / <see cref="Set"/> bind it to a value; a value at or below <see cref="OffAt"/> reads as <see cref="OffText"/>
	/// ("auto", "off"). Left-click the box to type, Enter or a click elsewhere to finish. <see cref="Typing"/> is true while the box has focus.
	/// </summary>
	public class NumberField : UIElement
	{
		public const int Height_ = 24;
		private readonly LevyButton _minus, _plus;
		private readonly UIPanel _box;
		private readonly UISearchBar _input;
		private readonly Label _shown;
		public Func<float> Get;
		public Action<float> Set;
		public float Min, Max, Step;
		public bool Whole = true;
		/// <summary>Values at or below this show <see cref="OffText"/> instead of a number (float.NegativeInfinity = never).</summary>
		public float OffAt = float.NegativeInfinity;
		public string OffText = "";
		public bool Typing => _input.IsWritingText;
		public void StopTypingIfAny() { if (_input.IsWritingText) _input.ToggleTakingText(); }
		private float _lastShown = float.NaN;
		private bool _lastTyping;

		public NumberField(float width, float min, float max, float step, bool whole, Func<float> get, Action<float> set)
		{
			Min = min; Max = max; Step = step; Whole = whole; Get = get; Set = set;
			Width.Set(width, 0f); Height.Set(Height_, 0f);
			_minus = new LevyButton("-", LevyConstants.UI.ButtonTextScale, 24, Height_, () => Nudge(-1));
			_plus = new LevyButton("+", LevyConstants.UI.ButtonTextScale, 24, Height_, () => Nudge(1));
			_plus.Left.Set(width - 24, 0f);
			_box = new UIPanel();
			_box.SetPadding(0);
			_box.Left.Set(26, 0f); _box.Width.Set(width - 52, 0f); _box.Height.Set(Height_, 0f);
			_box.BackgroundColor = new Color(20, 26, 52) * 0.9f;
			_shown = new Label("", 0.8f);
			_shown.HAlign = 0.5f; _shown.VAlign = 0.5f;
			_box.Append(_shown);
			_input = new UISearchBar(Language.GetText("Mods.Levy.Rules.TypeNumber"), 0.8f);
			_input.Left.Set(4, 0f); _input.Width.Set(-8, 1f); _input.Height.Set(0, 1f);
			_input.OnContentsChanged += OnTyped;
			_input.OnCanceledTakingInput += () => { if (_input.IsWritingText) _input.ToggleTakingText(); };
			_box.OnLeftClick += (e, el) => { if (!_input.IsWritingText) { _input.SetContents(Format(Get()), true); _input.ToggleTakingText(); } };
			Append(_minus); Append(_box); Append(_plus);
		}

		private string Format(float v) => Whole ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

		private void Nudge(int dir)
		{
			float v = Get();
			if (v <= OffAt) v = dir > 0 ? Math.Max(Min, OffAt + Step) : OffAt; // from "off" a plus turns it on at the first step
			else v += dir * Step;
			if (!float.IsNegativeInfinity(OffAt) && v < Min) v = OffAt;
			Apply(v);
		}

		private void Apply(float v)
		{
			if (!Whole) v = (float)Math.Round(v, 2);
			float lo = float.IsNegativeInfinity(OffAt) ? Min : Math.Min(Min, OffAt);
			v = Math.Clamp(v, lo, Max);
			if (Math.Abs(v - Get()) > 1e-6f) Set(v);
		}

		private void OnTyped(string text)
		{
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) Apply(v);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (_input.IsWritingText && Main.mouseLeft && Main.mouseLeftRelease && !_box.ContainsPoint(Main.MouseScreen)) _input.ToggleTakingText();
			float v = Get();
			bool typing = _input.IsWritingText;
			if (typing)
			{
				if (_box.HasChild(_shown)) { _box.RemoveChild(_shown); _box.Append(_input); }
			}
			else
			{
				if (_box.HasChild(_input)) { _box.RemoveChild(_input); _box.Append(_shown); _lastShown = float.NaN; }
				if (v != _lastShown || _lastTyping) { _lastShown = v; _shown.SetText(v <= OffAt ? OffText : Format(v)); }
			}
			_lastTyping = typing;
			_minus.Enabled = v > (float.IsNegativeInfinity(OffAt) ? Min : OffAt);
			_plus.Enabled = v < Max;
		}
	}

	/// <summary>A button that shows its current choice; clicking asks the owner to open the choice list (<see cref="Choose"/>).</summary>
	public class DropButton : LevyButton
	{
		public Func<string> Current;
		public Action<DropButton> Open;
		private string _shownText = "";

		public DropButton(float width, Func<string> current, Action<DropButton> open, string hint = null) : base("", LevyConstants.UI.ButtonTextScale, width, NumberField.Height_, null, hint)
		{
			Current = current; Open = open;
			OnClick = () => Open?.Invoke(this);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			string t = Current() + " v";
			if (t != _shownText) { _shownText = t; SetText(t); }
		}
	}

	/// <summary>A pop-up list of choices placed under a button. One click picks; a click anywhere else closes it. Long lists scroll with the wheel.</summary>
	public class ChoicePopup : UIPanel
	{
		private readonly UIList _list = new();
		private readonly UIScrollbar _bar = new();
		public bool Open => Parent != null;

		public ChoicePopup()
		{
			SetPadding(4);
			BackgroundColor = new Color(18, 24, 50) * 0.97f;
			_list.Width.Set(-24, 1f); _list.Height.Set(0, 1f); _list.ListPadding = 2f;
			_bar.Left.Set(-18, 1f); _bar.Height.Set(0, 1f);
			_list.SetScrollbar(_bar);
			Append(_list); Append(_bar);
		}

		/// <summary>Shows <paramref name="options"/> (label, action) inside <paramref name="host"/> with its top-left at (x, y), <paramref name="width"/> wide.</summary>
		public void Show(UIElement host, float x, float y, float width, List<(string label, Action pick, bool on)> options)
		{
			if (Parent != null) Remove();
			_list.Clear();
			int rows = Math.Min(options.Count, 9);
			Width.Set(width + 28, 0f); Height.Set(rows * 26 + 10, 0f);
			var hostBox = host.GetDimensions();
			float maxY = hostBox.Height - Height.Pixels - 2f;
			Left.Set(Math.Min(x, hostBox.Width - width - 30f), 0f); Top.Set(Math.Max(0f, Math.Min(y, maxY)), 0f);
			foreach (var (label, pick, on) in options)
			{
				var act = pick;
				var b = new LevyButton(label, LevyConstants.UI.ButtonTextScale, width, 24, () => { Remove(); act?.Invoke(); }) { Lit = on };
				_list.Add(b);
			}
			host.Append(this);
			Recalculate();
			_bar.ViewPosition = 0f;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			else if (Main.mouseLeft && Main.mouseLeftRelease) Remove();
		}
	}

	/// <summary>
	/// One item type in a list, drawn as its icon. Hover shows the item; click removes it from the list.
	/// </summary>
	public class TypeCell : UIElement
	{
		private readonly int _type;
		private readonly Action _remove;
		private Item _item;

		public TypeCell(int type, Action remove)
		{
			_type = type; _remove = remove;
			Width.Set(36, 0f); Height.Set(36, 0f);
			OnLeftClick += (e, el) => { SoundEngine.PlaySound(SoundID.MenuTick); _remove?.Invoke(); };
			OnRightClick += (e, el) => { SoundEngine.PlaySound(SoundID.MenuTick); _remove?.Invoke(); };
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			var pixel = TextureAssets.MagicPixel.Value;
			bool hot = IsMouseHovering;
			spriteBatch.Draw(pixel, r, hot ? new Color(93, 118, 200) * 0.9f : new Color(20, 26, 52) * 0.9f);
			if (_item == null) { _item = new Item(); _item.SetDefaults(_type); }
			if (_type > 0 && _type < TextureAssets.Item.Length)
			{
				Main.instance.LoadItem(_type);
				var tex = TextureAssets.Item[_type].Value;
				float scale = Math.Min(1f, 28f / Math.Max(tex.Width, tex.Height));
				spriteBatch.Draw(tex, r.Center.ToVector2(), null, Color.White, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
			if (hot)
			{
				Main.LocalPlayer.mouseInterface = true;
				Main.HoverItem = _item.Clone();
				Main.hoverItemName = _item.Name;
			}
		}
	}

	/// <summary>
	/// The "+" cell of an item list: with an item on the cursor, a click adds that item's type to the list (the item stays in the hand). Empty-handed it only shows a hint.
	/// </summary>
	public class AddTypeCell : UIElement
	{
		private readonly Action<int> _add;

		public AddTypeCell(Action<int> add)
		{
			_add = add;
			Width.Set(36, 0f); Height.Set(36, 0f);
			OnLeftClick += (e, el) =>
			{
				var held = Main.mouseItem;
				if (held == null || held.IsAir) return;
				SoundEngine.PlaySound(SoundID.MenuTick);
				_add?.Invoke(held.type);
			};
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			var r = GetDimensions().ToRectangle();
			var pixel = TextureAssets.MagicPixel.Value;
			bool hot = IsMouseHovering;
			bool holding = Main.mouseItem != null && !Main.mouseItem.IsAir;
			spriteBatch.Draw(pixel, r, hot && holding ? LevyConstants.UI.ButtonLit : hot ? new Color(93, 118, 200) * 0.9f : new Color(40, 54, 100) * 0.8f);
			Utils.DrawBorderString(spriteBatch, "+", r.Center.ToVector2() + new Vector2(0, 2), Color.White, 1.1f, 0.5f, 0.5f);
			if (hot) { Main.LocalPlayer.mouseInterface = true; HintTooltip.Queue(LevyUtil.T("Rules.AddItemHint")); }
		}
	}
}
