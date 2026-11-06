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
			}
			catch (Exception e) { fail.Add("officer kit check threw " + e.GetType().Name + ": " + e.Message); }

			if (fail.Count == 0) Mod.Logger.Info($"OFFICERKIT CHECK: OK ({OfficerRegistry.All.Count} officers, {poses} with a stock attack pose)");
			else foreach (string f in fail) Mod.Logger.Warn("OFFICERKIT CHECK FAIL: " + f);
		}
	}
}
