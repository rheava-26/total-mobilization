using System;
using System.Collections.Generic;
using System.Text;

namespace Levy.Common.UI
{
	/// <summary>
	/// Word wrap and reading time for speech over a speaker's head (Levy's chat bars and Warbands' bubbles). Pure logic: the caller passes a measure function (the real font on a client,
	/// an estimate in the headless checks), so the boot self-check runs the same code the screen does.
	/// Rules: no text is ever dropped. A line is wrapped at the preferred width; when that needs more than the preferred number of rows the bubble widens, step by step, up to the widest width;
	/// a line still longer than that simply takes more rows. A word wider than a row is split across rows.
	/// </summary>
	public static class ChatWrap
	{
		/// <summary>Width step when a bubble widens to fit its line in fewer rows.</summary>
		public const float WidenStep = 20f;

		/// <summary>Ticks a line needs to be read once shown: <see cref="LevyConstants.ChatReadBaseTicks"/> plus <see cref="LevyConstants.ChatReadTicksPerChar"/> a character, at most <see cref="LevyConstants.ChatReadMaxTicks"/>.</summary>
		public static int ReadTicks(int chars) => Math.Min(LevyConstants.ChatReadMaxTicks, LevyConstants.ChatReadBaseTicks + Math.Max(0, chars) * LevyConstants.ChatReadTicksPerChar);

		/// <summary>Wraps <paramref name="text"/> into rows no wider than <paramref name="maxWidth"/> (as <paramref name="measure"/> sees them). Every non-space character of the text is in the result, in order.</summary>
		public static List<string> Wrap(string text, Func<string, float> measure, float maxWidth)
		{
			var rows = new List<string>();
			if (string.IsNullOrEmpty(text)) return rows;
			maxWidth = Math.Max(8f, maxWidth);
			string[] paragraphs = text.Replace("\r", "").Split('\n');
			foreach (string para in paragraphs)
			{
				var cur = new StringBuilder();
				foreach (string word in para.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
				{
					string w = word;
					if (cur.Length > 0)
					{
						string joined = cur + " " + w;
						if (measure(joined) <= maxWidth) { cur.Append(' ').Append(w); continue; }
						rows.Add(cur.ToString()); cur.Clear();
					}
					// a word wider than a whole row is cut into row-wide pieces
					while (measure(w) > maxWidth && w.Length > 1)
					{
						int take = 1;
						while (take < w.Length - 1 && measure(w.Substring(0, take + 1)) <= maxWidth) take++;
						rows.Add(w.Substring(0, take));
						w = w.Substring(take);
					}
					cur.Append(w);
				}
				if (cur.Length > 0) rows.Add(cur.ToString());
			}
			return rows;
		}

		/// <summary>
		/// Wraps at the narrowest width from <paramref name="preferredWidth"/> up to <paramref name="maxWidth"/> (in <see cref="WidenStep"/> steps) that fits the text in <paramref name="preferredRows"/> rows,
		/// or at <paramref name="maxWidth"/> with as many rows as it takes. Returns the rows and the widest row's width.
		/// </summary>
		public static List<string> Fit(string text, Func<string, float> measure, float preferredWidth, float maxWidth, int preferredRows, out float width)
		{
			maxWidth = Math.Max(preferredWidth, maxWidth);
			List<string> rows = null;
			for (float w = preferredWidth; ; w = Math.Min(maxWidth, w + WidenStep))
			{
				rows = Wrap(text, measure, w);
				if (rows.Count <= Math.Max(1, preferredRows) || w >= maxWidth) break;
			}
			width = 0f;
			foreach (string r in rows) width = Math.Max(width, measure(r));
			return rows;
		}

		/// <summary>The text with its whitespace collapsed to single spaces: what the rows of <see cref="Wrap"/> join back to (with split words rejoined).</summary>
		public static string Squash(string text)
		{
			if (string.IsNullOrEmpty(text)) return "";
			return string.Join(" ", text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
		}

		/// <summary>Moves a box of width <paramref name="w"/> at <paramref name="x"/> inside [<paramref name="min"/>, <paramref name="max"/>]; a box wider than the span keeps its left edge on <paramref name="min"/>.</summary>
		public static float ClampInto(float x, float w, float min, float max) => Math.Max(min, Math.Min(x, max - w));
	}
}
