using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.UI;

namespace Levy.Common.UI
{
	/// <summary>
	/// The one text box every Levy search field and number field is built from. It keeps what is typed in a <see cref="TextEditModel"/> and draws the placeholder as a dim hint over an empty, unfocused box: the hint is never
	/// part of the text, so no key can edit it and an empty focused box stays empty. (Vanilla's UISearchBar put the hint into its own text box and, in a narrow box, trimmed it back into the editable text.)
	/// The members mirror the vanilla search bar so a field swaps in without changing its callers.
	/// </summary>
	public class LevyTextInput : UIElement
	{
		public readonly TextEditModel Model;
		private readonly LocalizedText _hint;
		private readonly float _scale;
		/// <summary>Click-to-focus selects the whole text so the next key replaces it (number fields).</summary>
		public bool SelectAllOnFocus;
		/// <summary>When set, the widget hands the raw key buffer here instead of editing <see cref="Model"/> itself (the number field routes it through its number rules).</summary>
		public Action<string> RawEdit;
		/// <summary>What the box holds the moment it takes focus (null = what it already shows).</summary>
		public Func<string> FocusText;
		public event Action<string> OnContentsChanged;
		public event Action OnStartTakingInput;
		public event Action OnEndTakingInput;
		public event Action OnCanceledTakingInput;

		public LevyTextInput(LocalizedText hint, float scale, TextEditModel model = null)
		{
			_hint = hint; _scale = scale; Model = model ?? new TextEditModel();
		}

		public bool IsWritingText => Model.Focused;
		public bool HasContents => !string.IsNullOrWhiteSpace(Model.Text);
		public string Text => Model.Text;

		/// <summary>Set the text (empty is empty: the hint is drawn, not stored). Fires <see cref="OnContentsChanged"/> when it changes, or always with <paramref name="forced"/>.</summary>
		public void SetContents(string contents, bool forced = false)
		{
			contents ??= "";
			if (contents == Model.Text && !forced) return;
			Model.Set(contents);
			OnContentsChanged?.Invoke(Model.Text);
		}

		public void ToggleTakingText()
		{
			if (Model.Focused) { Model.Blur(); Main.clrInput(); OnEndTakingInput?.Invoke(); }
			else
			{
				Model.Focus(FocusText != null ? FocusText() : Model.Text, SelectAllOnFocus);
				Main.clrInput(); OnStartTakingInput?.Invoke();
			}
		}

		public override void Update(GameTime gameTime)
		{
			if (Model.Focused)
			{
				PlayerInput.WritingText = true;
				Main.CurrentInputTextTakerOverride = this;
			}
			base.Update(gameTime);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (Model.Focused)
			{
				PlayerInput.WritingText = true;
				Main.instance.HandleIME();
				string raw = Main.GetInputText(Model.Text);
				if (RawEdit != null) RawEdit(raw);
				else if (Model.Edit(raw)) OnContentsChanged?.Invoke(Model.Text);
				if (Main.inputTextEnter) ToggleTakingText();
				else if (Main.inputTextEscape) { ToggleTakingText(); OnCanceledTakingInput?.Invoke(); }
			}

			var d = GetDimensions();
			var font = FontAssets.MouseText.Value;
			float avail = d.Width - 4f;
			string text = Model.ShowPlaceholder ? _hint.Value : Model.Text;
			Color color = Model.ShowPlaceholder ? Color.Gray : Color.White;
			bool caret = Model.Focused && !Model.AllSelected && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0;
			// keep the end of the text in view while typing, the start otherwise
			string drawn = text;
			while (drawn.Length > 0 && font.MeasureString(drawn).X * _scale > avail) drawn = Model.Focused ? drawn.Substring(1) : drawn.Substring(0, drawn.Length - 1);
			var pos = new Vector2(d.X + 2f, d.Y + d.Height * 0.5f);
			float w = font.MeasureString(drawn).X * _scale;
			if (Model.Focused && Model.AllSelected && drawn.Length > 0)
			{
				var sel = new Rectangle((int)pos.X - 1, (int)(pos.Y - 9f * _scale - 2f), (int)w + 3, (int)(18f * _scale + 4f));
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, sel, new Color(70, 110, 190) * 0.8f);
			}
			if (drawn.Length > 0) Terraria.Utils.DrawBorderString(spriteBatch, drawn, pos, color, _scale, 0f, 0.5f);
			if (caret) Terraria.Utils.DrawBorderString(spriteBatch, "|", new Vector2(pos.X + w + 1f, pos.Y), Color.White, _scale, 0f, 0.5f);
		}
	}
}
