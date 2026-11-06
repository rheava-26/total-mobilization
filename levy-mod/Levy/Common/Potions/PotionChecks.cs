using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Items.Potions;

namespace Levy.Common.Potions
{
	/// <summary>Boot self-checks of the potions (pure math and item flags; never throws). Not covered: boss aggro, the chooser UI, the Seeker draw, levy fall-back in a live fight.</summary>
	public class PotionChecks : ModSystem
	{
		private static void SpectatorTargetingChecks(Action<bool, string> Check)
		{
			Player saved = Main.player[0];
			SpectatorPlayer.InvalidateCache();
			var boss = new NPC(); boss.SetDefaults(NPCID.EyeofCthulhu);
			bool before = global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss);
			try
			{
				var p = new Player { active = true };
				p.AddBuff(ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(), 600);
				Main.player[0] = p;
				SpectatorPlayer.InvalidateCache();
				Check(SpectatorPlayer.IsSpectator(p) && SpectatorPlayer.AnyActive, "spectator: detected as a spectator");
				Check(global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss), "spectator: boss aggro on soldiers unlocked for a not-downed boss while a commander spectates");
				var z = new NPC(); z.SetDefaults(NPCID.Zombie); z.Center = p.Center; z.target = 0;
				SpectatorPlayer.HideAll();
				Check(p.dead && p.ghost, "spectator: hidden from the candidate search (dead+ghost, which vanilla's search skips)");
				SpectatorPlayer.HideAll(); SpectatorPlayer.ReleaseAll();
				Check(p.dead, "spectator: hide is re-entrant (inner release keeps it hidden)");
				SpectatorPlayer.ReleaseAll();
				Check(!p.dead && !p.ghost, "spectator: released after the search");
				z.target = 0;
				Check(global::Levy.Common.Targeting.EnemyTargeting.TargetsSpectator(z), "spectator: an enemy whose target is a spectator is flagged for an immediate re-search");
			}
			finally { Main.player[0] = saved; SpectatorPlayer.InvalidateCache(); }
			Check(global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss) == before, "spectator: boss aggro override ends with the buff");
		}

		public override void PostSetupRecipes()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try
			{
				float edge = PotionConstants.TetherTiles * 16f;
				Vector2 v = new Vector2(5f, 0f), c = new Vector2(edge + 200f, 0f);
				SpectatorPlayer.Tether(Vector2.Zero, ref c, ref v, out Vector2 pull);
				Check(v.X <= 0.001f && pull.X < 0f, "tether: outward speed removed past the edge and a pull back toward the anchor");
				v = new Vector2(5f, 0f); c = new Vector2(100f, 0f);
				SpectatorPlayer.Tether(Vector2.Zero, ref c, ref v, out pull);
				Check(v.X == 5f && pull == Vector2.Zero, "tether: free movement near the anchor");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.CopperShortsword)), "spectator: weapons blocked");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.CopperPickaxe)), "spectator: pickaxes blocked");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.StoneBlock)), "spectator: blocks blocked");
				Check(!SpectatorPlayer.Blocked(new Item(ItemID.LesserHealingPotion)), "spectator: potions allowed");
				Check(PotionConstants.AutonomousFallbackFrac < PotionConstants.AutonomousReengageFrac, "autonomy: re-engage threshold above fall-back threshold");
				Check(Array.IndexOf(Flavor.GuideSystem.Keys, "Autonomy") >= 0, "autonomy: guide key registered");
				Check(ModContent.ItemType<SeekerPotion>() > 0 && ModContent.ItemType<SpectatorPotion>() > 0, "potion items registered");
				try { SpectatorTargetingChecks(Check); } catch (Exception e) { failures.Add("spectator targeting checks threw " + e.GetType().Name + ": " + e.Message); }
			}
			catch (Exception e) { failures.Add("potion checks threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) Mod.Logger.Info($"Levy potion self-check: {ok} checks OK");
			else { Mod.Logger.Warn($"Levy potion self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("  potion: " + f); }
		}
	}
}
