using System;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Death
{
	/// <summary>
	/// Which death line a fallen levy posts. Three outcomes: its custom text (the death text the game, Calamity or another mod gives a player killed the same way: "X burned in brimstone fire"), the themed Levy line,
	/// or nothing. Custom text shows with the configured chance even when death messages are off. During a Nameless Deity or Avatar of Emptiness fight every levy death posts (100%), with its custom text when it has one.
	/// This only decides what a LEVY's death says; a real player's death text is never touched.
	/// </summary>
	public static class LevyDeathPost
	{
		public enum Verdict { None, Themed, Custom }

		/// <summary>The decision, pure. <paramref name="roll01"/> is a 0..1 roll; <paramref name="genericOn"/> is the existing setting for this levy's kind (Regular: RegularDeathMessages; Militia: Individual mode).</summary>
		public static Verdict Decide(bool hasCustom, bool noxusFight, int customPercent, float roll01, bool genericOn)
		{
			if (noxusFight) return hasCustom ? Verdict.Custom : Verdict.Themed;
			if (hasCustom && roll01 * 100f < customPercent) return Verdict.Custom;
			return genericOn ? Verdict.Themed : Verdict.None;
		}

		/// <summary>Self-check read-back: the last death line posted and how many have been.</summary>
		public static string LastPosted; public static int Posted;
		public static void Note(string line) { LastPosted = line; Posted++; }
	}

	/// <summary>
	/// Wrath of the Gods (NoxusBoss) is optional: found by name, no reference. Its Nameless Deity and Avatar of Emptiness fights have their own death scenes and death text for the player; Levy leaves those alone (no hook on a real
	/// player's death text or scene) and only turns levy death messages to always-on while either boss is alive.
	/// </summary>
	public static class NoxusFights
	{
		/// <summary>Self-check: null = read the world, true/false = pretend.</summary>
		public static bool? Override;
		private static bool? _has;
		public static bool Installed => _has ??= ModLoader.HasMod("NoxusBoss");

		public static bool IsFightNpc(NPC n)
		{
			if (n == null || !n.active || n.ModNPC == null || n.ModNPC.Mod == null || n.ModNPC.Mod.Name != "NoxusBoss") return false;
			string name = n.ModNPC.Name;
			return name.Contains("NamelessDeity", StringComparison.Ordinal) || name.Contains("AvatarOfEmptiness", StringComparison.Ordinal);
		}

		public static bool Active
		{
			get
			{
				if (Override.HasValue) return Override.Value;
				if (!Installed) return false;
				for (int i = 0; i < Main.maxNPCs; i++) if (IsFightNpc(Main.npc[i])) return true;
				return false;
			}
		}
	}
}
