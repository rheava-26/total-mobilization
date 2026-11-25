using System;
using System.Collections.Generic;
using Levy.Common.Dialogue;

namespace Levy.Common.Compat.Wotg
{
	/// <summary>
	/// WHAT THE ARMY SAYS WHILE THE AVATAR OF EMPTINESS IS UP (the Avatar's rift counts: it is the same fight). Selection only, no line is written or changed here:
	///  * No joke or banter pool speaks: every SCENE_ pool is held (the moments, the worried lines, the reactions) except SCENE_*_KILL, which is the dry line after the fight (and is only reached once it is over).
	///  * The plain combat callouts speak instead, through their own triggers and the usual budget: ENGAGE, KILL, HURT, LOW_HP, OUT_OF_AMMO and SHAKEN ("Open fire!", "Hold it together!"). Only a line anyone may say is picked from them:
	///    temperament Any, no [tag], no placeholder but {Player}. The personality lines of those pools (a Sellsword's pay jokes, a Cynic's sighs) and the lines that name a comrade or a count are left out.
	///    A line is not repeated within two minutes (LevyConstants.LineRepeatCooldownTicks): when the plain lines of a pool are used up the levy says nothing rather than a joke.
	/// Off with the "Levies react in Wrath of the Gods fights" setting (then the fight is silent, as before).
	/// </summary>
	public static class AoeVoice
	{
		/// <summary>Self-check: null = read the world, true/false = pretend the fight is on or off.</summary>
		internal static bool? TestActive;
		/// <summary>Self-check: every floating line delivered (trigger byte, text) is appended while this is not null.</summary>
		internal static List<(byte trigger, string text)> FloatLog;

		/// <summary>The Avatar of Emptiness (or its rift) is up and the fight talk setting is on.</summary>
		public static bool Active => TestActive ?? (WotgScenes.Enabled && AvatarFight.Active);

		/// <summary>The plain combat callout triggers.</summary>
		public static readonly DialogueTrigger[] PlainTriggers =
		{
			DialogueTrigger.ENGAGE, DialogueTrigger.KILL, DialogueTrigger.HURT, DialogueTrigger.LOW_HP, DialogueTrigger.OUT_OF_AMMO, DialogueTrigger.SHAKEN,
		};

		public static bool IsPlain(DialogueTrigger t) { for (int i = 0; i < PlainTriggers.Length; i++) if (PlainTriggers[i] == t) return true; return false; }

		/// <summary>A trigger the army may speak right now although the fight hush is on.</summary>
		public static bool Allows(DialogueTrigger t) => IsPlain(t) && Active;

		/// <summary>This pool is one of the plain callout pools (the table's own list, by identity).</summary>
		internal static bool IsPlainPool(List<DialogueLine> pool)
		{
			if (pool == null) return false;
			for (int i = 0; i < PlainTriggers.Length; i++) if (ReferenceEquals(DialogueTable.Get(PlainTriggers[i]), pool)) return true;
			return false;
		}

		/// <summary>A line anyone may say: temperament Any, no [tag], no placeholder but {Player}.</summary>
		internal static bool LineIsPlainPublic(DialogueLine l) => LineIsPlain(l);
		internal static bool LineIsPlain(DialogueLine l) => l.Filter == LineFilter.Any && l.Cond == LineCond.None && l.Needs == LineNeeds.None;

		/// <summary>A scene pool that stays silent while the Avatar is up: all of them but the kill line.</summary>
		internal static bool MutesScene(string pool) => Active && pool != null && !pool.EndsWith("_KILL", StringComparison.Ordinal);

		/// <summary>Self-check: the text is a line of a plain callout pool that anyone may say (placeholders matched loosely: the literal parts in order).</summary>
		internal static bool IsPlainText(string text)
		{
			if (string.IsNullOrEmpty(text)) return false;
			foreach (DialogueTrigger t in PlainTriggers)
			{
				var lines = DialogueTable.Get(t);
				if (lines == null) continue;
				foreach (DialogueLine l in lines)
				{
					if (!LineIsPlain(l) || l.Parts.Length == 0) continue;
					int at = 0; bool ok = true;
					foreach (LinePart p in l.Parts)
					{
						if (p.Kind != PlaceholderKind.Literal || string.IsNullOrEmpty(p.Literal)) continue;
						int i = text.IndexOf(p.Literal, at, StringComparison.Ordinal);
						if (i < 0) { ok = false; break; }
						at = i + p.Literal.Length;
					}
					if (ok) return true;
				}
			}
			return false;
		}
	}
}
