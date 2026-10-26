using System;
using System.Collections.Generic;
using Terraria.ModLoader;
using Levy.Common.Officers;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Boot-time self-check of the officer voices, in the style of WeaponWiringCheck. A headless boot has no world and no avatar to right-click, so this checks what can be checked:
	/// every registered officer has lines of his own for every state, the pool key an officer speaks from resolves to his own pool when it exists and to the generic
	/// OFFICER_CHAT_* pool when it does not, and a mistyped state in the trigger column is rejected by the parser. Log only: it changes nothing the game reads.
	/// </summary>
	public class OfficerDialogueCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			var failures = new List<string>();
			int voiced = 0;
			try
			{
				foreach (OfficerDef def in OfficerRegistry.All)
				{
					bool complete = true;
					foreach (string state in DialogueTable.OfficerStates)
					{
						string own = OfficerChat.OwnPoolKey(def.Key, state);
						if (DialogueTable.Get(own) == null) { failures.Add($"officer {def.Key} has no {own} lines (he falls back to the generic voice for {state})"); complete = false; continue; }
						string resolved = OfficerChat.ResolvePoolKey(def.Key, state);
						if (!string.Equals(resolved, own, StringComparison.OrdinalIgnoreCase)) failures.Add($"{own} has lines but resolves to {resolved}");
					}
					if (complete) voiced++;
				}

				foreach (string state in DialogueTable.OfficerStates)
				{
					string generic = "OFFICER_CHAT_" + state;
					if (DialogueTable.Get(generic) == null) failures.Add($"the generic pool {generic} is empty, so an un-voiced officer would be silent for {state}");
					string fallback = OfficerChat.ResolvePoolKey("NoSuchOfficer", state);
					if (!string.Equals(fallback, generic, StringComparison.OrdinalIgnoreCase)) failures.Add($"an un-voiced officer resolves {state} to {fallback}, not {generic}");
				}

				// The parser must reject a mistyped state and accept a good one (null mod: silent, and nothing is added to the table on a rejection).
				int before = DialogueTable.LineCount;
				if (DialogueTable.Parse(null, "selfcheck", "OFFICER_Nurse_BOGUS | Any | x") != 1) failures.Add("the parser accepted OFFICER_Nurse_BOGUS");
				if (DialogueTable.LineCount != before) failures.Add("a rejected officer line still changed the table");
			}
			catch (Exception e) { failures.Add("officer dialogue check threw: " + e.GetType().Name + ": " + e.Message); }

			try
			{
				if (failures.Count == 0)
					Mod.Logger.Info($"Levy officer dialogue self-check: {voiced} officers fully voiced across {DialogueTable.OfficerStates.Length} states OK");
				else
				{
					Mod.Logger.Warn($"Levy officer dialogue self-check: {failures.Count} issue(s) ({voiced} officers fully voiced)");
					foreach (string f in failures) Mod.Logger.Warn("Levy officer dialogue self-check: " + f);
				}
			}
			catch (Exception) { }
		}
	}
}
