using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Officers
{
	/// <summary>
	/// Boot-time self-check of the officer kits and the raised-weapon pose (log only, changes nothing the game reads). A headless boot has no live avatar to look at, so this checks the
	/// pure parts: every kit description formats with its live numbers, every officer's town NPC maps to a stock attack pose (ai[0] 10 / 12 / 14 / 15) with the shooter's aim held inside
	/// vanilla's range, a lowered weapon is ai[0] = 0, and each rebuilt kit is what its spec says (the Painter has paint, the Guide a volley and no stat aura, the Merchant better-than-vanilla terms).
	/// </summary>
	public class OfficerKitCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			var fail = new List<string>();
			int poses = 0;
			try
			{
				foreach (OfficerDef def in OfficerRegistry.All)
				{
					string desc;
					try { desc = LevyUtil.T("Officers.Kit." + def.Key + ".Desc", def.Kit?.DescriptionArgs ?? Array.Empty<object>()); }
					catch (Exception e) { fail.Add(def.Key + " description threw " + e.Message); continue; }
					if (desc.Contains("Mods.Levy") || desc.Contains("{") || desc.Contains("}")) fail.Add(def.Key + " description unresolved: " + desc);
					// the player-facing text rule: 1 to 4 short declarative sentences, plain words
					int sentences = System.Text.RegularExpressions.Regex.Matches(desc.Trim(), @"\.(\s|$)").Count;
					if (sentences < 1 || sentences > 4) fail.Add(def.Key + " description has " + sentences + " sentences (1 to 4): " + desc);
					foreach (string w in new[] { "elevate", "seamless", "robust", "strays into reach", "lifts the whole", "unleash", "harness" })
						if (desc.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) fail.Add(def.Key + " description uses \"" + w + "\": " + desc);

					int at = NPCID.Sets.AttackType[def.NpcType];
					float a0 = SoldierNPC.OfficerPoseState(def.NpcType, 20, 40, 0.9f, out float a1, out float a2);
					float want = at == 0 ? 10f : at == 1 ? 12f : at == 2 ? 14f : at == 3 ? 15f : 0f;
					if (a0 != want) fail.Add(def.Key + " pose ai[0]=" + a0 + " expected " + want + " (AttackType " + at + ")");
					if (Math.Abs(a2) > 0.5f) fail.Add(def.Key + " pose aim " + a2 + " outside vanilla's range");
					if (want > 0f) { poses++; if (a1 <= 0f) fail.Add(def.Key + " pose countdown not positive"); }
					if (SoldierNPC.OfficerPoseState(def.NpcType, 0, 40, 0.3f, out _, out _) != 0f) fail.Add(def.Key + " weapon not lowered at 0");
				}
				if (OfficerRegistry.Get(NPCID.Painter)?.Kit is not PainterKit) fail.Add("Painter has no paint kit");
				if (OfficerRegistry.Get(NPCID.Guide)?.Kit is AuraKit) fail.Add("Guide is still a stat aura");
				if (OfficerRegistry.Get(NPCID.Guide)?.Kit is not GuideKit) fail.Add("Guide has no volley kit");
				if (!(LevyConstants.Officer.MerchantMarchPriceAdjust < 0.75)) fail.Add("Merchant march price is not better than vanilla's best");

				// the Officers panel's scrolling list: bounded by the screen at every common resolution and UI scale, whole content when it fits
				foreach (int sh in new[] { 600, 720, 768, 900, 1080, 1440, 2160 })
					foreach (float ui in new[] { 0.85f, 1f, 1.25f, 1.5f, 2f })
					{
						float screenH = sh / ui;
						if (screenH < 240f) continue; // below this the list floor (120 px) cannot fit
						foreach (float contentH in new[] { 200f, 1400f, 6000f })
						{
							float h = global::Levy.Common.UI.OfficerPanel.PanelHeight(contentH, screenH), list = global::Levy.Common.UI.OfficerPanel.ListHeight(contentH, screenH);
							if (h > screenH - 2 * global::Levy.Common.UI.OfficerPanel.EdgeMargin + 0.01f) fail.Add($"officer panel {h} tall does not fit a {screenH} px screen ({sh}p x{ui})");
							if (list > contentH + 0.01f || list <= 0f) fail.Add($"officer list height {list} for content {contentH} on {screenH}"); // list never exceeds content
						}
						// content that fits under the header and above the bottom edge shows whole; content too tall for this screen fills what the screen leaves (never more). 100 px fits every screen this loop reaches (floor 240).
						float room = global::Levy.Common.UI.OfficerPanel.ListHeight(1_000_000f, screenH); // what the screen leaves under the header for a list too tall to fit
						if (global::Levy.Common.UI.OfficerPanel.ListHeight(100f, screenH) != 100f) fail.Add("short content should show whole at " + screenH);
						foreach (float c in new[] { 200f, 1400f })
						{
							float want = Math.Min(c, room), got = global::Levy.Common.UI.OfficerPanel.ListHeight(c, screenH);
							if (Math.Abs(got - want) > 0.01f) fail.Add($"officer list for content {c} on {screenH} should be {want} (whole when it fits, else the room the screen leaves), was {got}");
						}
					}
				// the resizable panel at the resolutions players use (1366x768 to 2560x1440, UI scale 1 to 1.5): every width and height the grip allows fits the screen; a dragged height is kept
				foreach (var (sw, sh) in new[] { (1366, 768), (1600, 900), (1920, 1080), (2560, 1440) })
					foreach (float ui in new[] { 1f, 1.25f, 1.5f })
					{
						float w = sw / ui, hh = sh / ui;
						foreach (float want in new[] { 0f, 300f, 600f, 5000f })
						{
							float fw = global::Levy.Common.UI.OfficerPanel.FitWidth(want, w);
							if (fw > w - 2 * global::Levy.Common.UI.OfficerPanel.EdgeMargin + 0.01f && fw > global::Levy.Common.UI.OfficerPanel.MinW) fail.Add($"officer panel {fw} wide does not fit {sw}x{sh} x{ui}");
							foreach (float header in new[] { 96f, 150f })
							{
								float lst = global::Levy.Common.UI.OfficerPanel.ListHeight(9000f, hh, header, want);
								float total = header + lst + 24f;
								if (total > Math.Max(hh - 2 * global::Levy.Common.UI.OfficerPanel.EdgeMargin, header + 144f) + 0.01f) fail.Add($"officer panel {total} tall (header {header}, asked {want}) does not fit {sw}x{sh} x{ui}");
								if (want > 0f && want >= global::Levy.Common.UI.OfficerPanel.MinH && want <= hh - 2 * global::Levy.Common.UI.OfficerPanel.EdgeMargin && Math.Abs(total - want) > 0.01f) fail.Add($"officer panel dragged to {want} is {total} at {sw}x{sh} x{ui}");
							}
						}
					}
				// a card is as tall as its text: nine description lines are all shown (no line cap), and a card never cuts its cost line
				{
					float h9 = global::Levy.Common.UI.OfficerPanel.CandidateCardHeight(2, 24f, 9, 23f, 2, 22f), h1 = global::Levy.Common.UI.OfficerPanel.CandidateCardHeight(1, 24f, 1, 23f, 1, 22f);
					if (h9 < 10 + 48 + 4 + 9 * 23 + 4 + 2 * 22 + 10 - 0.01f) fail.Add("a candidate card with 9 description lines is " + h9 + " tall: lines are cut");
					if (h1 < 66f) fail.Add("a one-line candidate card is shorter than its portrait: " + h1);
					if (global::Levy.Common.UI.OfficerPanel.DescScale < 0.8f || global::Levy.Common.UI.OfficerPanel.SmallScale < 0.8f) fail.Add("officer panel text below 0.8 scale");
				}
				// the pylon is vanilla's: no Levy GlobalTile claims a right-click on any tile
				foreach (var gt in ModContent.GetContent<GlobalTile>())
					if (gt.Mod == Mod && gt.GetType().GetMethod("RightClick", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly) != null)
						fail.Add(gt.GetType().Name + " overrides RightClick (pylons must stay vanilla)");
			}
			catch (Exception e) { fail.Add("officer kit check threw " + e.GetType().Name + ": " + e.Message); }

			if (fail.Count == 0) Mod.Logger.Info($"OFFICERKIT CHECK: OK ({OfficerRegistry.All.Count} officers, {poses} with a stock attack pose)");
			else foreach (string f in fail) Mod.Logger.Warn("OFFICERKIT CHECK FAIL: " + f);
		}
	}
}
