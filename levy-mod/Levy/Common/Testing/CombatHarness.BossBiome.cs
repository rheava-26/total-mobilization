using System;
using System.Globalization;
using System.Text;
using Terraria;
using Terraria.ID;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BOSS BIOME probe (measurement only; lab 2026-11 "bossbiome"). Once a second, from inside the boss's own AI call (EnemyTargetingGlobalNPC.PostAI, before the dummy is taken out of its slot) logs what
	/// the boss's AI saw: who its target is (a levy dummy or the commander), the target's tile position, the zone flags Calamity's enrage checks read, the boss's CurrentlyEnraged, and each enrage condition
	/// of the biome-gated bosses (Plantera, Duke Fishron, Brimstone Elemental, Queen Bee) evaluated on that target with Calamity's own formulas.
	/// </summary>
	public partial class CombatHarness
	{
		internal static void BbBiomeProbe(NPC npc)
		{
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
