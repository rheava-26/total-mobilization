using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Recording
{
	/// <summary>Boot self-check of the recorder's boss-end logic: a worm killed by a blow on a body segment, or that vanishes just after a near-lethal hit, is a kill; a boss that leaves healthy is a despawn. Pure data plus two throw-away NPC slots; never throws.</summary>
	public class FightRecChecks : ModSystem
	{
		public override void PostSetupRecipes()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool c, string w) { if (c) ok++; else failures.Add(w); }
			try
			{
				// the real recordings: Desert Scourge, 10200 life, head gone at 4..80 life within a tick of the last hit
				Check(FightRecorder.LooksKilled(45, 10200, 3744, 10, 3744, false), "left at 45/10200 on the hit tick is a kill");
				Check(FightRecorder.LooksKilled(80, 10200, 2286, 12, 2290, false), "left at 80/10200 4 ticks after a hit is a kill");
				Check(FightRecorder.LooksKilled(900, 10200, 100, 2000, 110, false), "a hit bigger than the remaining life is a kill");
				Check(FightRecorder.LooksKilled(5000, 10200, 100, 10, 110, true), "OnKill on any part is a kill");
				Check(FightRecorder.LooksKilled(0, 10200, -1, 0, 500, false), "life 0 is a kill");
				Check(!FightRecorder.LooksKilled(8000, 10200, 100, 40, 110, false), "leaving healthy right after a hit is a despawn");
				Check(!FightRecorder.LooksKilled(45, 10200, 100, 10, 400, false), "leaving at low life with no recent hit is a despawn");
				Check(!FightRecorder.LooksKilled(45, 10200, -1, 0, 400, false), "leaving at low life that was never hit is a despawn");
				// the closing window of a fight: a kill that other bosses vanish with is still the fight's end (Moon Lord's core with his hands and head, the Cultist with the pillars his death spawns)
				Check(FightRecorder.FinalEndIndex(new[] { true, false, false, false }) == 0, "a killed core and three parts that vanish with it: the kill names the end");
				Check(FightRecorder.FinalEndIndex(new[] { true, false, false, false, false }) == 0, "a killed boss and four pillars purged after it: the kill names the end");
				Check(FightRecorder.FinalEndIndex(new[] { true, true, true }) == 2, "worm heads that all fell: the last of them names the end");
				Check(FightRecorder.FinalEndIndex(new[] { false, true, false }) == 1, "the last killed one wins over a later despawn");
				Check(FightRecorder.FinalEndIndex(new[] { false, false }) == 1 && FightRecorder.FinalEndIndex(new[] { false }) == 0, "nothing killed: the last one to go names the end, as before");
				// format 7: the hitbox rows (full and short) of other hostile NPCs round-trip, and the format self-check passes
				Check(FightRecFormat.Version >= 7, "format version is 7 or later");
				string fmt = FightRecFormat.SelfCheck();
				Check(fmt == null, "format self-check incl. hitbox rows: " + fmt);
				// a body segment resolves to the head that owns its life
				int hi = Main.maxNPCs - 1, bi = Main.maxNPCs - 2;
				NPC saveH = Main.npc[hi], saveB = Main.npc[bi];
				try
				{
					NPC head = new NPC(), body = new NPC();
					head.SetDefaults(NPCID.EaterofWorldsHead); head.whoAmI = hi; head.active = true; head.realLife = -1;
					body.SetDefaults(NPCID.EaterofWorldsBody); body.whoAmI = bi; body.active = true; body.realLife = hi;
					Main.npc[hi] = head; Main.npc[bi] = body;
					Check(FightRecorder.BossRoot(head) == head, "a worm head is its own boss root");
					Check(FightRecorder.BossRoot(body) == head, "a worm body segment resolves to its head");
					Check(FightRecorder.BossRoot(null) == null, "null has no boss root");
				}
				finally { Main.npc[hi] = saveH; Main.npc[bi] = saveB; }
			}
			catch (Exception e) { failures.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) Mod.Logger.Info($"[FightRecCheck] DONE pass={ok} fail=0");
			else { Mod.Logger.Warn($"[FightRecCheck] DONE pass={ok} fail={failures.Count}"); foreach (var f in failures) Mod.Logger.Warn("  fightrec: " + f); }
		}
	}
}
