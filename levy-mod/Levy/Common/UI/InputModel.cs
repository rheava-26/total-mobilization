using System;
using System.Globalization;
using System.Text;

namespace Levy.Common.UI
{
	/// <summary>
	/// The edit state of one text box, with no game types in it so it can be checked at boot. The widget feeds it the buffer the game's own key reader returns (<see cref="Edit"/>); it answers with the text to show.
	/// The placeholder is never part of <see cref="Text"/>: it is a hint the widget draws while the box is empty and not focused, and no key can edit it.
	/// While focused, an empty box stays empty. With <see cref="AllSelected"/> set (a click that selects the current text), the first key replaces everything: a backspace clears it, a typed character becomes the whole text.
	/// </summary>
	public class TextEditModel
	{
		public string Text = "";
		public bool Focused;
		public bool AllSelected;
		public int MaxLength = 50;
		/// <summary>Keeps only the characters the box accepts (null = anything).</summary>
		public Func<string, string> Sanitize;

		public bool IsEmpty => Text.Length == 0;
		/// <summary>True when the dim hint should be drawn in place of the text: empty and not focused.</summary>
		public bool ShowPlaceholder => !Focused && Text.Length == 0;

		/// <summary>Give the box focus with <paramref name="current"/> in it; <paramref name="select"/> marks it all selected so typing replaces it.</summary>
		public void Focus(string current, bool select)
		{
			Text = Clean(current ?? ""); Focused = true; AllSelected = select && Text.Length > 0;
		}

		public void Blur() { Focused = false; AllSelected = false; }

		/// <summary>Set the text from outside (a program write); never touches focus.</summary>
		public void Set(string text) { Text = Clean(text ?? ""); AllSelected = false; }

		/// <summary>The game returned <paramref name="raw"/> as the new buffer after this frame's keys. Returns true when <see cref="Text"/> changed.</summary>
		public bool Edit(string raw)
		{
			if (!Focused || raw == null) return false;
			string old = Text, next;
			if (AllSelected)
			{
				if (raw == old) return false;
				if (raw.Length < old.Length) next = "";                                                         // backspace / delete over a selection
				else next = Clean(raw.StartsWith(old, StringComparison.Ordinal) ? raw.Substring(old.Length) : raw); // typed or pasted text replaces it
				AllSelected = false;
			}
			else next = Clean(raw);
			if (next == old) return false;
			Text = next;
			return true;
		}

		private string Clean(string s)
		{
			if (Sanitize != null) s = Sanitize(s) ?? "";
			return s.Length > MaxLength ? s.Substring(0, MaxLength) : s;
		}
	}

	/// <summary>
	/// A number box: <see cref="TextEditModel"/> plus the number rules. The text you type is never rewritten while you type (it can be empty, or briefly out of range); the number is settled on <see cref="Commit"/>
	/// (Enter, a click elsewhere, Buy / Add). An empty box commits to the last valid value. <see cref="Value"/> is that last valid value.
	/// </summary>
	public class NumberEditModel
	{
		public readonly TextEditModel Box = new();
		public float Min, Max;
		public bool Whole = true;
		/// <summary>Values at or below this show <see cref="OffText"/> (float.NegativeInfinity = never).</summary>
		public float OffAt = float.NegativeInfinity;
		public string OffText = "";
		/// <summary>The last valid value: what the box shows when it is not focused, and what an empty commit restores.</summary>
		public float Value;

		public NumberEditModel(float min, float max, bool whole, float value)
		{
			Min = min; Max = max; Whole = whole; Value = value;
			Box.MaxLength = 12;
			Box.Sanitize = Digits;
		}

		public bool Focused => Box.Focused;
		/// <summary>The lowest value the box can hold: <see cref="OffAt"/> when there is an off state, else <see cref="Min"/>.</summary>
		public float Lowest => float.IsNegativeInfinity(OffAt) ? Min : Math.Min(Min, OffAt);

		public string Format(float v) => Whole ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

		private string Digits(string s)
		{
			var sb = new StringBuilder(s.Length); bool dot = false;
			foreach (char c in s)
			{
				if (c >= '0' && c <= '9') sb.Append(c);
				else if (c == '.' && !Whole && !dot) { sb.Append(c); dot = true; }
			}
			return sb.ToString();
		}

		/// <summary>What to draw: the text, and whether it is the dim hint. Focused shows exactly the text (empty stays empty); unfocused shows the value (or the off text), and the hint only if that is empty.</summary>
		public (string text, bool hint) Display(string placeholder)
		{
			if (Box.Focused) return (Box.Text, false);
			string shown = Value <= OffAt ? OffText : Format(Value);
			return shown.Length > 0 ? (shown, false) : (placeholder, true);
		}

		/// <summary>What a click puts in the box: the current number (nothing while it reads as off). The widget selects it so the next key replaces it.</summary>
		public string FocusText => Value <= OffAt ? "" : Format(Value);
		/// <summary>Click: focus and select the current number.</summary>
		public void Focus() => Box.Focus(FocusText, true);

		/// <summary>A key buffer from the game. Returns the number to apply LIVE when the text is a complete in-range number (it also becomes the last valid value), else null.</summary>
		public float? Edit(string raw)
		{
			if (!Box.Edit(raw)) return null;
			if (Box.Text.Length == 0 || !TryParse(Box.Text, out float v)) return null;
			v = Round(v);
			if (v < Lowest || v > Max) return null;
			Value = v;
			return v;
		}

		private float Round(float v) => Whole ? (float)Math.Round(v) : (float)Math.Round(v, 2);

		private static bool TryParse(string t, out float v) => float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

		/// <summary>Settle the box: an empty or unreadable text restores <see cref="Value"/>; a number is clamped to the range. Ends focus. Returns the committed value.</summary>
		public float Commit()
		{
			if (Box.Focused && Box.Text.Length > 0 && TryParse(Box.Text, out float v)) Value = Math.Clamp(Round(v), Lowest, Max);
			Box.Blur();
			return Value;
		}

		/// <summary>Keep the last valid value in step with the bound number (the widget calls this every frame while not focused).</summary>
		public void SyncValue(float v) { if (!Box.Focused) Value = v; }
	}
}
