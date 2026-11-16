using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace Levy.Common.UI
{
	/// <summary>
	/// Boot self-check of the edit model behind every Levy text and number box (<see cref="TextEditModel"/>, <see cref="NumberEditModel"/>): keys go in as the game's key buffer, the displayed text and the number come out.
	/// It covers the reported failures: backspacing a number to empty must stay empty (the hint is never editable text and never shown while focused), a click selects the number so typing replaces it, digits only,
	/// clamping on commit, and an empty commit restoring the last valid value. Pure logic; never throws.
	/// </summary>
	public class InputModelCheck : ModSystem
	{
		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(RunChecks);

		private const string Hint = "Type a number";

		/// <summary>Type one key into a model the way the widget does: the game returns the buffer after the key (backspace removes the last char, a character is appended).</summary>
		private static float? Key(NumberEditModel m, char c) => m.Edit(c == '\b' ? (m.Box.Text.Length > 0 ? m.Box.Text.Substring(0, m.Box.Text.Length - 1) : "") : m.Box.Text + c);
		private static void Keys(NumberEditModel m, string s) { foreach (char c in s) Key(m, c); }

		private void RunChecks()
		{
			var fails = new List<string>(); int pass = 0;
			void Check(bool ok, string what) { if (ok) pass++; else fails.Add(what); }
			try
			{
				// ---- Count field: 1..9999, whole, currently 30 ----
				var m = new NumberEditModel(1, 9999, true, 30);
				Check(m.Display(Hint) == ("30", false), "unfocused shows the number");
				m.Focus();
				Check(m.Focused && m.Box.Text == "30" && m.Box.AllSelected, "click focuses and selects the current number");
				Key(m, '\b');
				Check(m.Box.Text == "" && m.Focused, "backspace over the selected number clears it");
				Check(m.Display(Hint) == ("", false), "an empty focused box shows nothing: not the hint");
				for (int i = 0; i < 20; i++) Key(m, '\b');
				Check(m.Box.Text == "" && m.Display(Hint) == ("", false), "backspacing an empty box keeps it empty (the hint is not text)");
				Keys(m, "77");
				Check(m.Box.Text == "77" && m.Value == 77, "typing digits after clearing works and applies live");
				Keys(m, "\b\b\b");
				Check(m.Box.Text == "" && m.Value == 7, "backspacing every digit leaves it empty and keeps the last valid value (7, applied live)");
				Check(m.Commit() == 7 && !m.Focused && m.Display(Hint) == ("7", false), "committing an empty box reverts to the last valid value");

				// typing replaces the selection
				m = new NumberEditModel(1, 9999, true, 30); m.Focus();
				m.Edit("309");
				Check(m.Box.Text == "9" && m.Value == 9, "a typed digit replaces the selected number");
				Keys(m, "2");
				Check(m.Box.Text == "92" && m.Value == 92, "digits then append");
				m = new NumberEditModel(1, 9999, true, 30); m.Focus();
				m.Edit("30" + "4500");
				Check(m.Box.Text == "4500", "pasted text replaces the selected number");

				// Budget field: 0..9999 gold, 51 -> 5 -> empty
				m = new NumberEditModel(0, 9999, true, 51); m.Focus();
				m.Edit("515");                                        // the game returns old text + the typed 5; the selection is replaced
				Check(m.Box.Text == "5" && m.Value == 5, "budget: typing 5 over 51");
				Key(m, '\b');
				Check(m.Box.Text == "" && m.Focused, "budget: backspacing 5 leaves it empty and still focused");
				Check(m.Display(Hint) == ("", false), "budget: no hint text while focused and empty");
				Keys(m, "9999");
				Check(m.Box.Text == "9999" && m.Value == 9999, "budget: 9999 accepted");
				m = new NumberEditModel(0, 9999, true, 51); m.Focus(); Key(m, '\b'); Keys(m, "87"); Check(m.Value == 87, "budget: 87 after clearing");

				// digits only
				m = new NumberEditModel(1, 9999, true, 30); m.Focus(); Key(m, '\b');
				Keys(m, "a1b-2.3 ");
				Check(m.Box.Text == "123", "letters, signs, dots and spaces are rejected on a whole-number box");
				var d = new NumberEditModel(1, 100000000, false, 10); d.Focus(); Key(d, '\b'); Keys(d, "1.2.5");
				Check(d.Box.Text == "1.25" && d.Value == 1.25f, "a decimal box takes one dot");

				// clamp on commit
				m = new NumberEditModel(1, 9999, true, 30); m.Focus(); Key(m, '\b'); Keys(m, "99999");
				Check(m.Box.Text == "99999", "an out-of-range number is left as typed while typing");
				Check(m.Commit() == 9999 && m.Value == 9999, "commit clamps to the maximum");
				m = new NumberEditModel(1, 9999, true, 30); m.Focus(); Key(m, '\b'); Keys(m, "0");
				Check(m.Value == 30, "an out-of-range number is not applied live");
				Check(m.Commit() == 1, "commit clamps to the minimum");

				// the hint: only empty AND not focused
				var off = new NumberEditModel(0, 100, true, 0) { OffAt = 0, OffText = "" };
				Check(off.Display(Hint) == (Hint, true), "an empty, unfocused box shows the dim hint");
				off.Focus();
				Check(off.Display(Hint) == ("", false), "focusing it hides the hint");
				var auto = new NumberEditModel(0, 100, true, 0) { OffAt = 0, OffText = "auto" };
				Check(auto.Display(Hint) == ("auto", false), "an off value shows its off text, not the hint");
				auto.Focus(); Keys(auto, "5"); Check(auto.Box.Text == "5" && auto.Value == 5, "an off box takes a typed number");
				auto = new NumberEditModel(0, 100, true, 0) { OffAt = 0, OffText = "auto" }; auto.Focus();
				Check(auto.Box.Text == "" && auto.Commit() == 0, "an off box that is left empty stays off");

				// Enter / click-away / Esc: commit ends focus
				m = new NumberEditModel(1, 9999, true, 30); m.Focus(); Key(m, '\b'); Keys(m, "45"); m.Commit();
				Check(!m.Focused && m.Display(Hint) == ("45", false) && !m.Box.AllSelected, "committing ends focus and shows the number");

				// ---- search box (TextEditModel) ----
				var t = new TextEditModel();
				Check(t.ShowPlaceholder, "an empty unfocused search box shows the hint");
				t.Focus("", false);
				Check(!t.ShowPlaceholder && t.IsEmpty, "a focused empty search box does not show the hint");
				Check(!t.Edit("") && t.Text == "", "backspace in an empty search box changes nothing");
				t.Edit("ab"); t.Edit("a"); t.Edit(""); t.Edit("");
				Check(t.Text == "" && t.Focused, "backspacing a search term to empty stays empty");
				t.Edit(new string('x', 80));
				Check(t.Text.Length == 50, "search text is capped at 50 characters");
				t.Blur();
				Check(!t.Focused && !t.ShowPlaceholder, "blur keeps the typed text");
			}
			catch (Exception e) { fails.Add($"threw {e.GetType().Name}: {e.Message}"); }

			if (fails.Count == 0) Mod.Logger.Info($"[InputModel] self-check pass={pass} fail=0");
			else
			{
				Mod.Logger.Warn($"[InputModel] self-check pass={pass} fail={fails.Count} !!!");
				foreach (var f in fails) Mod.Logger.Warn("[InputModel] FAIL " + f);
			}
		}
	}
}
