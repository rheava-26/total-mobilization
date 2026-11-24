using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// BOSS LINES THAT KNOW ABOUT THE ARMY. Calamity's Supreme Witch Calamitas and Draedon speak to a lone hero ("you wield it for no one but yourself"). With an army on the field
	/// (<see cref="LevyConstants.BossLineArmyMin"/> or more standing regular levies) a line that does not fit is swapped for Levy's version of it, keeping its point and tone.
	/// How: one detour on <c>CalamityUtils.BroadcastLocalizedText(string key, Color?)</c>, the one call those bosses speak through (Main.NewText in single player, a localized broadcast on a server, so
	/// clients read the line in their own language). A key "Mods.CalamityMod.Status.Boss.X" with a "Mods.Levy.BossLines.X" entry is sent as that entry instead; every other line goes through untouched.
	/// Nothing of Calamity's is patched: no file, no text, only which key is sent. Settings: "Boss lines acknowledge your army" (on) and "Show the original boss line too" (off: when on, the original
	/// follows its army version in a dimmer colour). Wrath of the Gods' bosses are never touched.
	/// </summary>
	public class BossLines : ModSystem
	{
		public const string CalamityPrefix = "Mods.CalamityMod.Status.Boss.", LevyPrefix = "Mods.Levy.BossLines.";
		private delegate void OrigBroadcast(string key, Color? textColor);

		/// <summary>The detour is in place (Calamity loaded and the method found).</summary>
		internal static bool Installed;
		/// <summary>Self-check overrides (null in play): pretend an army is or is not on the field, and the two settings.</summary>
		internal static bool? TestArmy, TestOn, TestShowOriginal;
		/// <summary>Telemetry: lines seen, lines swapped, and the last key seen and sent.</summary>
		internal static int Seen, Swapped;
		internal static string LastKey, LastSent;

		public override void PostSetupContent()
		{
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type util = cal.Code.GetType("CalamityMod.CalamityUtils");
				MethodInfo m = util?.GetMethod("BroadcastLocalizedText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(Color?) }, null);
				if (m != null) { MonoModHooks.Add(m, (Action<OrigBroadcast, string, Color?>)BroadcastDetour); Installed = true; }
				Mod.Logger.Info("Levy boss lines: CalamityUtils.BroadcastLocalizedText " + (Installed ? "hooked" : "NOT found (boss lines stay as Calamity wrote them)"));
			}
			catch (Exception e) { Mod.Logger.Warn("Levy boss lines failed: " + e.Message); }
		}

		private static void BroadcastDetour(OrigBroadcast orig, string key, Color? textColor)
		{ _hl_BroadcastDetour_1++;
			Seen++; LastKey = key;
			string swap = null;
			try { swap = SwapFor(key); } catch (Exception) { swap = null; }
			if (swap == null) { LastSent = key; orig(key, textColor); return; }
			Swapped++; LastSent = swap;
			orig(swap, textColor);
			if (ShowOriginal) SayOriginal(key, Dim(textColor ?? Color.White));
		} private static int _hl_BroadcastDetour_1;

		/// <summary>The Levy key to send in place of <paramref name="key"/>, or null to send it as it is.</summary>
		internal static string SwapFor(string key)
		{
			string levy = ArmyKeyFor(key);
			if (levy == null || !On || !ArmyOnField()) return null;
			return levy;
		}

		/// <summary>Levy's army version of a Calamity boss line key, or null when it has none.</summary>
		internal static string ArmyKeyFor(string key)
		{
			if (key == null || !key.StartsWith(CalamityPrefix, StringComparison.Ordinal)) return null;
			string levy = LevyPrefix + key.Substring(CalamityPrefix.Length);
			return Language.Exists(levy) ? levy : null;
		}

		private static bool On { get { if (TestOn.HasValue) return TestOn.Value; try { return LevyConfig.Get?.BossLinesFitArmy ?? true; } catch (Exception) { return true; } } }
		private static bool ShowOriginal { get { if (TestShowOriginal.HasValue) return TestShowOriginal.Value; try { return LevyConfig.Get?.BossLinesShowOriginal ?? false; } catch (Exception) { return false; } } }

		/// <summary>An army is on the field: at least <see cref="LevyConstants.BossLineArmyMin"/> standing regular levies deployed (not in reserve), of any commander.</summary>
		internal static bool ArmyOnField()
		{
			if (TestArmy.HasValue) return TestArmy.Value;
			if (RosterSystem.Instance == null) return false;
			int n = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
				if (s.IsAlive && s.IsRegular && !s.Record.Reserve && !s.Hostile && ++n >= LevyConstants.BossLineArmyMin) return true;
			return false;
		}

		/// <summary>The boss's original line, marked as such, after its army version: in single player straight to chat, on a server as a localized broadcast.</summary>
		private static void SayOriginal(string key, Color c)
		{
			if (Main.netMode == Terraria.ID.NetmodeID.SinglePlayer) Main.NewText(Language.GetTextValue(LevyPrefix + "OriginalLine", Language.GetTextValue(key)), c);
			else if (Main.dedServ) Terraria.Chat.ChatHelper.BroadcastChatMessage(NetworkText.FromKey(LevyPrefix + "OriginalLine", NetworkText.FromKey(key)), c);
			Originals++;
		}
		internal static int Originals;

		private static Color Dim(Color c) => new Color((int)(c.R * 0.6f + 40), (int)(c.G * 0.6f + 40), (int)(c.B * 0.6f + 40));
	}

	/// <summary>
	/// The one-time note about boss dialogue: the first time this install enters a world with Calamity loaded (the mod whose lines it changes), a line in chat says Levy adds and changes some boss dialogue and where to turn it off.
	/// Once per install: the seen flag is saved in the client-side Observe settings ("Boss line note shown" there is a record, not a switch).
	/// </summary>
	public class BossLineNotice : ModPlayer
	{
		/// <summary>Self-check: stand-ins for the saved flag and for Calamity being loaded (null in play).</summary>
		internal static bool? TestSeen, TestCalamity;
		/// <summary>Times the note was said this session.</summary>
		internal static int Said;

		/// <summary>The note shows when Calamity is loaded and it has not been shown before.</summary>
		internal static bool ShouldShow(bool seen, bool calamity) => calamity && !seen;

		private static bool Seen { get { if (TestSeen.HasValue) return TestSeen.Value; try { return global::Levy.Common.Observe.ObserveConfig.Get?.BossLineNoteShown ?? true; } catch (Exception) { return true; } } }

		public override void OnEnterWorld()
		{
			if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;
			Announce();
		}

		/// <summary>Says the note if it is due, and records that it was said.</summary>
		internal static void Announce()
		{
			if (!ShouldShow(Seen, TestCalamity ?? BossLines.Installed)) return;
			Said++;
			Main.NewText(global::Levy.Common.Util.LevyUtil.T("Dialogue.BossLineNotice"), 230, 200, 120);
			if (TestSeen.HasValue) { TestSeen = true; return; }
			try
			{
				var c = global::Levy.Common.Observe.ObserveConfig.Get;
				if (c != null) { c.BossLineNoteShown = true; global::Levy.Common.Perf.PerfPopup.SaveConfig(c); }
			}
			catch (Exception e) { global::Levy.Levy.Instance.Logger.Warn("Levy boss line note: could not save the seen flag: " + e.Message); }
		}
	}
}
