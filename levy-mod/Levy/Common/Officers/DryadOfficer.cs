using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Mark;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE AOE MARKER. The Dryad is a SUPPORT officer who works two effects on one cadence. DRYAD'S BANE hangs the army's SHRED mark on EVERY enemy in
	/// reach (not just one, as the Guide does), so the army deals extra damage to all of them; and a nature aura keeps the soldiers around her under Thorns
	/// (contact damage reflected) and Regeneration. The mark and its amp live in <see cref="Levy.Common.Mark.MarkSystem"/> / MarkAmpGlobalNPC.
	/// </summary>
	public static class DryadOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Dryad,
			Key = "Dryad",
			CostMult = DryadCostMult,
			Aggro = DryadAggro,
			Posture = OfficerPosture.Support,   // casts from behind the line, out of melee
			Weapon = null,
			Kit = new DryadKit(),
		};
	}

	/// <summary>
	/// The Dryad's ability: every <see cref="LevyConstants.Officer.DryadCadenceTicks"/> she hangs the shred on every hostile within
	/// <see cref="LevyConstants.Officer.DryadMarkRadiusTiles"/> (AoE) and refreshes Thorns + Regeneration on every soldier within
	/// <see cref="LevyConstants.Officer.DryadAuraRadiusTiles"/>. The marks are real debuffs that tick down on their own and vanish with the enemy, so there
	/// is no state to leak; the buffs are refreshed each pulse, so they hold steady and never stack.
	/// </summary>
	public sealed class DryadKit : OfficerKit
	{
		private static readonly int[] _auraBuffs = { BuffID.Thorns, BuffID.Regeneration };
		private sealed class State { public int Timer; }

		public override object CreateState() => new State();

		// {0} mark reach (tiles), {1} army damage added to each marked enemy (percent), {2} aura reach (tiles)
		public override object[] DescriptionArgs => new object[]
		{
			DryadMarkRadiusTiles, (int)MarkDamagePct, DryadAuraRadiusTiles,
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			if (++st.Timer < DryadCadenceTicks) return;
			st.Timer = 0;
			Vector2 from = ctx.Npc.Center;

			// DRYAD'S BANE — hang the shred on every hostile in reach (refreshes those already marked, so it reads as continuous).
			float markReach = DryadMarkRadiusTiles * LevyConstants.TilePx;
			float markReachSq = markReach * markReach;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				if (Vector2.DistanceSquared(from, n.Center) > markReachSq) continue;
				MarkSystem.Mark(n, DryadMarkDurationTicks);
			}

			// Nature aura — keep nearby soldiers under Thorns + Regeneration, then flag their stats for a recompute so the effect lands.
			float auraReach = DryadAuraRadiusTiles * LevyConstants.TilePx;
			float auraReachSq = auraReach * auraReach;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive) continue;
				if (Vector2.DistanceSquared(from, s.NPC.Center) > auraReachSq) continue;
				for (int b = 0; b < _auraBuffs.Length; b++) s.NPC.AddBuff(_auraBuffs[b], DryadAuraDurationTicks);
				s.StatsDirty = true;
			}
		}
	}
}
