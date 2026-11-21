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
				Check(FightRecorder.LooksKilled(5000, true), "the death hook on any part is a kill");
				Check(FightRecorder.LooksKilled(0, false), "life 0 is a kill");
				Check(!FightRecorder.LooksKilled(45, false) && !FightRecorder.LooksKilled(1, false), "leaving at 45 or at 1 life with no death hook is a boss that left (Calamity's death animation sits at 1 until the hook runs)");
				Check(!FightRecorder.LooksKilled(14542, false), "leaving at 14542 of 1836000 right after a hit is a boss that left (the false Devourer of Gods win)");
				// the closing window of a fight: a kill that other bosses vanish with is still the fight's end (Moon Lord's core with his hands and head, the Cultist with the pillars his death spawns)
				Check(FightRecorder.FinalEndIndex(new[] { true, false, false, false }) == 0, "a killed core and three parts that vanish with it: the kill names the end");
				Check(FightRecorder.FinalEndIndex(new[] { true, false, false, false, false }) == 0, "a killed boss and four pillars purged after it: the kill names the end");
				Check(FightRecorder.FinalEndIndex(new[] { true, true, true }) == 2, "worm heads that all fell: the last of them names the end");
				Check(FightRecorder.FinalEndIndex(new[] { false, true, false }) == 1, "the last killed one wins over a later despawn");
				Check(FightRecorder.FinalEndIndex(new[] { false, false }) == 1 && FightRecorder.FinalEndIndex(new[] { false }) == 0, "nothing killed: the last one to go names the end, as before");
				// the result rule: a boss that leaves is lost when the commander's fall is why, fled when she was alive and well; a kill is won whatever happened to her
				Check(FightRecorder.DecideResult("killed", 3, false, 5000, 4000, 1) == "won", "a killed boss is won, even after deaths");
				Check(FightRecorder.DecideResult("despawn", 1, true, 1000, 940, 1) == "lost", "the boss left while the commander was dead: lost");
				Check(FightRecorder.DecideResult("despawn", 1, false, 1800, 1000, 1) == "lost", "the boss left 13 s after she fell and she had respawned: lost");
				Check(FightRecorder.DecideResult("despawn", 1, false, 1000, 1000, 1) == "lost", "the boss left on the very tick she fell: lost");
				Check(FightRecorder.DecideResult("despawn", 2, false, 20000, 1000, 1) == "fled", "the boss left long after her last fall with her alive: fled");
				Check(FightRecorder.DecideResult("despawn", 0, false, 5000, -1, 1) == "fled", "the boss left and she never fell: fled");
				Check(FightRecorder.DecideResult("despawn", 1, false, 1000, 5000, 1) == "fled", "she fell only after the boss had gone: fled");
				Check(FightRecorder.DecideResult("despawn", 1, false, 1000 + FightRecorder.LostWindowTicks, 1000, 1) == "lost" && FightRecorder.DecideResult("despawn", 1, false, 1001 + FightRecorder.LostWindowTicks, 1000, 1) == "fled", "the window is 60 s after the fall");
				Check(FightRecorder.DecideResult("none", 1, false, -1, 500, 1) == "lost" && FightRecorder.DecideResult("none", 0, false, -1, -1, 1) == "ended" && FightRecorder.DecideResult("none", 1, false, -1, 500, 0) == "ended", "no boss end: lost after a fall with a boss seen, else ended");
				// event NPCs are never a fight boss
				foreach (int t in new[] { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust, NPCID.MartianSaucerCore, NPCID.MartianSaucer, NPCID.PirateShip, NPCID.Pumpking, NPCID.IceQueen })
					Check(FightRecorder.IsEventNpc(t), "event NPC " + t + " is on the event list");
				Check(!FightRecorder.IsEventNpc(NPCID.CultistBoss) && !FightRecorder.IsEventNpc(NPCID.MoonLordCore) && !FightRecorder.IsEventNpc(NPCID.EyeofCthulhu) && !FightRecorder.IsEventNpc(NPCID.DukeFishron), "real bosses are not event NPCs");
				try
				{
					var pil = new NPC(); pil.SetDefaults(NPCID.LunarTowerSolar); pil.active = true;
					var cul = new NPC(); cul.SetDefaults(NPCID.CultistBoss); cul.active = true;
					Check(!FightRecorder.IsFightBoss(pil), "a Celestial Pillar is not a fight boss even though the game flags it a boss");
					Check(FightRecorder.IsFightBoss(cul), "the Lunatic Cultist is a fight boss");
				}
				catch (Exception e) { failures.Add("event npc: " + e.Message); }
				// the commander's own projectiles: his shot counts, the army's tagged shots (same owner slot) and hostile shots do not
				try
				{
					var cmd = new Player(); cmd.whoAmI = 0;
					Projectile Mk(int type) { var pr = new Projectile(); pr.SetDefaults(type); pr.active = true; pr.owner = 0; pr.friendly = true; pr.hostile = false; pr.damage = 40; return pr; }
					var own = Mk(ProjectileID.MagicMissile);
					Check(FightRecorder.IsCommanderShot(own, cmd), "the commander's own shot is recorded");
					var army = Mk(ProjectileID.MagicMissile); if (army.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag t1)) t1.SoldierIndex = 5;
					Check(!FightRecorder.IsCommanderShot(army, cmd), "a soldier's shot, owned by the commander's slot, is left out");
					var off = Mk(ProjectileID.MagicMissile); if (off.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag t2)) t2.CommanderIndex = 0;
					Check(!FightRecorder.IsCommanderShot(off, cmd), "an officer's or a summoned unit's shot is left out");
					var drone = Mk(ProjectileID.MagicMissile); if (drone.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag t3)) t3.DroneShot = true;
					Check(!FightRecorder.IsCommanderShot(drone, cmd), "a drone's shot is left out");
					var hostile = Mk(ProjectileID.MagicMissile); hostile.hostile = true; hostile.friendly = false;
					Check(!FightRecorder.IsCommanderShot(hostile, cmd), "a hostile shot is not the commander's");
					var other = Mk(ProjectileID.MagicMissile); other.owner = 1;
					Check(!FightRecorder.IsCommanderShot(other, cmd), "another player's shot is not the commander's");
					var hook = Mk(ProjectileID.Hook); hook.hostile = false;
					Check(!FightRecorder.IsCommanderShot(hook, cmd), "a grappling hook is not recorded");
				}
				catch (Exception e) { failures.Add("commander shots: " + e.Message); }
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
