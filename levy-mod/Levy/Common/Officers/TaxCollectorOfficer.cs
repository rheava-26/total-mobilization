using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Mark;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE TAX FARMER. The Tax Collector keeps to the rear and hangs the army's SHRED mark on every enemy in reach, so the army hits them harder — and when
	/// one of those marked enemies falls near him, he taxes its corpse: a coin bounty paid into the treasury. The kill-and-pay is detected server-side by
	/// <see cref="TaxBountyGlobalNPC"/>; this kit only lays the marks.
	/// </summary>
	public static class TaxCollectorOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.TaxCollector,
			Key = "TaxCollector",
			CostMult = TaxCostMult,
			Aggro = TaxAggro,
			Posture = OfficerPosture.Support,   // keeps to the rear; the army protects him
			Weapon = null,
			Kit = new TaxCollectorKit(),
		};
	}

	/// <summary>
	/// The Tax Collector's ability: every <see cref="LevyConstants.Officer.TaxMarkCadenceTicks"/> he hangs the shred on every hostile within
	/// <see cref="LevyConstants.Officer.TaxMarkRadiusTiles"/>. The bounty on a marked enemy's death is paid by <see cref="TaxBountyGlobalNPC"/>.
	/// </summary>
	public sealed class TaxCollectorKit : OfficerKit
	{
		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		// {0} mark reach (tiles), {1} the most one bounty can pay
		public override object[] DescriptionArgs => new object[]
		{
			TaxMarkRadiusTiles, LevyUtil.Coins(TaxBountyMaxCopper),
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < TaxMarkCadenceTicks) return;
			st.Timer = 0;
			Vector2 from = ctx.Npc.Center;
			float reach = TaxMarkRadiusTiles * LevyConstants.TilePx;
			float reachSq = reach * reach;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				if (Vector2.DistanceSquared(from, n.Center) > reachSq) continue;
				MarkSystem.Mark(n, TaxMarkDurationTicks);
			}
		}
	}
}
