using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Boss;
using Levy.Common.Mark;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Officer;

namespace Levy.Common.Officers
{
	/// <summary>
	/// THE SPOTTER, and the proof of the army-wide mark. The Guide is a FRONTLINE officer that marches with the line and calls the army's fire
	/// onto one enemy at a time: every cadence it finds the single strongest hostile in reach — a boss, or whatever has the most max life — and
	/// hangs the SHRED mark on it, so the whole army deals extra damage there until a tougher target appears or the mark lapses. It also pokes with
	/// a guiding bolt at anything that strays into reach. The mark and its amp live in <see cref="Levy.Common.Mark.MarkSystem"/> and
	/// <see cref="Levy.Common.Mark.MarkAmpGlobalNPC"/>; this officer is just the hand that calls the shot.
	/// </summary>
	public static class GuideOfficer
	{
		public static OfficerDef Create() => new OfficerDef
		{
			NpcType = NPCID.Guide,
			Key = "Guide",
			CostMult = GuideCostMult,
			Aggro = GuideAggro,
			Posture = OfficerPosture.Frontline,          // marches with the army and fights what comes in reach
			// The guiding bolt: a bright, straight shard thrown at whatever strays into reach. The call for fire (the mark) is the kit's job, not this.
			Weapon = new OfficerWeapon
			{
				Projectile = ProjectileID.SapphireBolt,
				Speed = GuideBoltSpeed, GravityCorrection = GuideBoltGravityCorrection, Damage = GuideBoltDamage, Knockback = GuideBoltKnockback,
				Delay = 1, Cooldown = GuideBoltCooldown, RandomCooldown = GuideBoltRandomCooldown,
				WindUp = Math.Max(1, NPCID.Sets.AttackTime[NPCID.Guide]),
				RangeTiles = GuideBoltRangeTiles,
			},
			Kit = new GuideKit(),
		};
	}

	/// <summary>
	/// The Guide's ability: every <see cref="GuideMarkCadenceTicks"/> it picks the single strongest hostile within <see cref="GuideMarkRangeTiles"/>
	/// (a boss always outranks a mob; otherwise the highest max life) and hangs the SHRED mark on it for <see cref="GuideMarkDurationTicks"/>. Only one
	/// enemy carries the mark at a time: taking a new target pulls the mark off the last one. The mark is a real debuff, so it ticks down on its own and
	/// vanishes with the enemy — there is no state to leak.
	/// </summary>
	public sealed class GuideKit : OfficerKit
	{
		private sealed class State
		{
			public int Timer;
			/// <summary>NPC slot of the enemy currently marked, or -1. Only used to pull the old mark when switching; the live enemy is re-checked first.</summary>
			public int MarkedIndex = -1;
		}

		public override object CreateState() => new State();

		// {0} reach in tiles, {1} damage the army adds (percent), {2} seconds the mark lasts
		public override object[] DescriptionArgs => new object[]
		{
			(int)GuideMarkRangeTiles, (int)MarkDamagePct, GuideMarkDurationTicks / 60,
		};

		public override void Tick(OfficerContext ctx)
		{
			if (ctx.State is not State st) return;
			NPC guide = ctx.Npc;
			if (++st.Timer < GuideMarkCadenceTicks) return;
			st.Timer = 0;

			NPC prey = StrongestInReach(guide.Center, GuideMarkRangeTiles * LevyConstants.TilePx);
			if (prey == null) return;                          // nothing to call fire on: the old mark simply lapses

			// one target at a time: when the strongest has changed, pull the mark off the previous enemy first
			if (st.MarkedIndex >= 0 && st.MarkedIndex < Main.maxNPCs && st.MarkedIndex != prey.whoAmI)
			{
				NPC old = Main.npc[st.MarkedIndex];
				if (old.active) MarkSystem.Unmark(old);
			}

			MarkSystem.Mark(prey, GuideMarkDurationTicks);     // (re)hang the mark on the strongest — refreshes it when the target is unchanged
			st.MarkedIndex = prey.whoAmI;
			guide.direction = prey.Center.X < guide.Center.X ? -1 : 1; guide.spriteDirection = guide.direction;
		}

		/// <summary>The single strongest hostile within <paramref name="reach"/> of <paramref name="from"/>: a boss (or boss part) always wins, else the highest max life. Null when none is in reach.</summary>
		private static NPC StrongestInReach(Vector2 from, float reach)
		{
			NPC best = null; long bestScore = long.MinValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				if (Vector2.Distance(from, n.Center) > reach) continue;
				long score = n.lifeMax + ((n.boss || BossRule.IsMainBoss(n)) ? GuideBossPriorityScore : 0L);
				if (score > bestScore) { bestScore = score; best = n; }
			}
			return best;
		}
	}
}
