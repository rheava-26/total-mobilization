using FSP = global::Levy.Common.Targeting.FlyerSlotProjectile;
using System;
using System.Globalization;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS BIOME probe (measurement only; lab 2026-11 "bossbiome"). Once a second, from inside the boss's own AI call (EnemyTargetingGlobalNPC.PostAI, before the dummy is taken out of its slot) logs what
	/// the boss's AI saw: who its target is (a levy dummy or the commander), the target's tile position, the zone flags Calamity's enrage checks read, the boss's CurrentlyEnraged, and each enrage condition
	/// of the biome-gated bosses (Plantera, Duke Fishron, Brimstone Elemental, Queen Bee) evaluated on that target with Calamity's own formulas.
	/// </summary>
	public partial class CombatHarness
	{
		/// <summary>Empress of Light probe (lab): her AI state every 15 ticks and a census of her hostile projectiles (target slot read, whether they close on a levy / the commander).</summary>
		private static void BbEolProbe(NPC npc)
		{
			var st = _cr; if (st == null || st.Run == null || st.Run.Boss != "eol" || npc.whoAmI != st.BossIdx || st.Tick > 2400) return;
			if (st.Tick % 15 != 0) return;
			NPCAimedTarget td = npc.GetTargetData(); Player tp = npc.target >= 0 && npc.target <= Main.maxPlayers ? Main.player[npc.target] : null;
			global::Levy.Levy.Instance.Logger.Info(string.Format(CultureInfo.InvariantCulture, "HARNESS-EOL: t={0} ai=[{1:0},{2:0},{3:0},{4:0}] tgt={5} invalid={6} dist={7:0} tgtActive={8} vel={9:0.0} hp={10:0.00} day={11}",
				st.Tick, npc.ai[0], npc.ai[1], npc.ai[2], npc.ai[3], npc.target, td.Invalid ? 1 : 0, td.Invalid ? -1f : npc.Distance(td.Center), tp != null && tp.active ? 1 : 0, npc.velocity.Length(), npc.life / (float)npc.lifeMax, Main.dayTime ? 1 : 0));
		}

		/// <summary>Called from the harness step (after the NPC and projectile updates of the tick, so the slots are back to normal): what the Empress's hostile projectiles are doing now.</summary>
		internal static void BbEolProjCensus(CrState st)
		{
			if (st == null || st.Run == null || st.Run.Boss != "eol" || st.Tick > 2400 || st.Tick % 20 != 0) return;
			global::Levy.Levy.Instance.Logger.Info($"HARNESS-EOLPROJ: t={st.Tick} hook seen={FSP.DbgSeen} swapped={FSP.DbgSwapped} noBody={FSP.DbgNoBody} blocked={FSP.DbgBlocked}");
			var seen = new System.Collections.Generic.Dictionary<string, int>();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile pr = Main.projectile[i]; if (!pr.active || !pr.hostile || pr.velocity.LengthSquared() < 0.25f) continue;
				string k = $"{pr.type}:{pr.Name}"; seen.TryGetValue(k, out int c); if (c >= 2) continue; seen[k] = c + 1;
				int a0 = (int)pr.ai[0]; Player sl = a0 >= 0 && a0 <= Main.maxPlayers ? Main.player[a0] : null;
				NPC nl = null; float dl = float.MaxValue;
				for (int q = 0; q < Main.maxNPCs; q++) { NPC n = Main.npc[q]; if (n.active && n.ModNPC is SoldierNPC) { float d = Vector2.Distance(n.Center, pr.Center); if (d < dl) { dl = d; nl = n; } } }
				Vector2 v = Vector2.Normalize(pr.velocity);
				float cl = nl != null ? Vector2.Dot(v, Vector2.Normalize(nl.Center - pr.Center)) : 0f, cc = Vector2.Dot(v, Vector2.Normalize(Main.player[0].Center - pr.Center));
				string cs = sl != null ? Vector2.Dot(v, Vector2.Normalize(sl.Center - pr.Center)).ToString("0.00", CultureInfo.InvariantCulture) : "-";
				global::Levy.Levy.Instance.Logger.Info(string.Format(CultureInfo.InvariantCulture, "HARNESS-EOLPROJ: t={0} {1} ai0={2} slotActive={3} speed={4:0.0} cosToSlot={5} cosToNearestLevy={6:0.00} cosToCmd={7:0.00} levyTiles={8:0} cmdTiles={9:0}",
					st.Tick, k, a0, sl != null && sl.active ? 1 : 0, pr.velocity.Length(), cs, cl, cc, dl / 16f, Vector2.Distance(Main.player[0].Center, pr.Center) / 16f));
			}
		}

		internal static void BbBiomeProbe(NPC npc)
		{
			BbEolProbe(npc);
			var st = _cr; if (st == null || st.Run == null || npc.whoAmI != st.BossIdx) return;
			string b = st.Run.Boss; if (!(BbIsBoss(b) || b == "qb")) return;
			if (st.Tick % 60 != 30) return;
			int tg = npc.target; string kind; Player pl = null;
			if (tg >= 0 && tg < Main.maxPlayers + 1) { pl = Main.player[tg]; kind = ReferenceEquals(pl, Main.player[0]) ? "cmd" : "levy"; if (pl == null || !pl.active) kind = "inactive:" + kind; }
			else if (tg >= 300) kind = "npc"; else kind = "none";
			var sb = new StringBuilder();
			sb.Append($"HARNESS-ENR: key={st.Run.Key} t={st.Tick} boss={b} tgt={tg} kind={kind} enr={(CrEnraged(npc) ? 1 : 0)}");
			double ws = Main.worldSurface, ul = Main.UnderworldLayer; int w = Main.maxTilesX;
			sb.Append(string.Format(CultureInfo.InvariantCulture, " world={0}x{1} surf={2:0} underworld={3:0} bossTile={4:0},{5:0}", w, Main.maxTilesY, ws, ul, npc.Center.X / 16f, npc.Center.Y / 16f));
			if (pl != null)
			{
				float px = pl.position.X, py = pl.position.Y;
				sb.Append(string.Format(CultureInfo.InvariantCulture, " tgtTile={0:0},{1:0} zones[jungle={2} beach={3} uwh={4} crag={5} sulph={6} hallow={7} snow={8}]", pl.Center.X / 16f, pl.Center.Y / 16f,
					pl.ZoneJungle ? 1 : 0, pl.ZoneBeach ? 1 : 0, pl.ZoneUnderworldHeight ? 1 : 0, BbInBiome(pl, "CalamityMod/BrimstoneCragsBiome") ? 1 : 0, BbInBiome(pl, "CalamityMod/SulphurousSeaBiome") ? 1 : 0, pl.ZoneHallow ? 1 : 0, pl.ZoneSnow ? 1 : 0));
				switch (b)
				{
					case "plant": sb.Append($" cond[aboveSurface={(py < ws * 16.0 ? 1 : 0)} belowUnderworld={(py > ul * 16.0 ? 1 : 0)}]"); break;
					case "fish": sb.Append($" cond[tooHigh={(py < 800f ? 1 : 0)} underground={(py > ws * 16.0 ? 1 : 0)} notOceanX={(px > 6400f && px < (w * 16 - 6400) ? 1 : 0)}]"); break;
					case "brim": sb.Append($" cond[noUnderworldHeight={(pl.ZoneUnderworldHeight ? 0 : 1)} noCrag={(BbInBiome(pl, "CalamityMod/BrimstoneCragsBiome") ? 0 : 1)}]"); break;
					case "qb": sb.Append($" cond[bossAboveSurface={(npc.position.Y / 16f < ws ? 1 : 0)} noJungle={(pl.ZoneJungle ? 0 : 1)}]"); break;
				}
			}
			global::Levy.Levy.Instance.Logger.Info(sb.ToString());
		}
	}
}
