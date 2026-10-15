using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Soldier → enemy choice, never-target list, flee list, attacker tracking. OWNED BY THE TARGETING AGENT.
	/// Priority 1: enemies that recently hurt a player or a soldier, or that are locked on a soldier (EnemyTargeting);
	/// priority 2: nearest. Everything needs line of sight.
	/// </summary>
	public static class SoldierTargeting
	{
		// Attacker table: tick (GameUpdateCount + 1, so 0 = never) and the NPC type seen at that time (slot reuse guard).
		private static readonly int[] _attackTick = new int[Main.maxNPCs];
		private static readonly int[] _attackType = new int[Main.maxNPCs];

		/// <summary>
		/// The commander's designated focus target when it is alive, a legal target and within weapon reach x <see cref="LevyConstants.FocusRangeMult"/>
		/// (line of sight is not required: the engage logic walks to a firing line). -1 when there is none or it is out of reach.
		/// </summary>
		public static int FocusTargetFor(SoldierNPC s, float rangeTiles)
		{
			if (s?.Record == null || s.NPC == null || !s.NPC.active || s.Record.Kind != SoldierKind.Regular) return -1;
			int idx = RosterSystem.FocusTargetOf(s.Record.CommanderName);
			if (idx < 0) return -1;
			return LevyUtil.WithinTiles(s.NPC.Center, Main.npc[idx].Center, rangeTiles * LevyConstants.FocusRangeMult) ? idx : -1;
		}

		/// <summary>
		/// Best enemy within <paramref name="rangeTiles"/> with line of sight, or -1. With <paramref name="useFocus"/> the commander's focus
		/// target (see <see cref="FocusTargetFor"/>) beats everything else.
		/// </summary>
		public static int PickTarget(SoldierNPC s, float rangeTiles, bool useFocus = true)
		{
			if (s == null || s.NPC == null || !s.NPC.active) return -1;
			if (useFocus) { int focus = FocusTargetFor(s, rangeTiles); if (focus >= 0) return focus; }
			Vector2 from = s.NPC.Center;
			float rangePx = rangeTiles * LevyConstants.TilePx;
			float rangeSq = rangePx * rangePx;
			int best = -1; float bestDistSq = float.MaxValue; bool bestAttacking = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || i == s.NPC.whoAmI || IsNeverTarget(npc)) continue;
				float distSq = Vector2.DistanceSquared(from, npc.Center);
				if (distSq > rangeSq) continue;
				bool attacking = IsAttackingUs(npc);
				if (bestAttacking && !attacking) continue;                 // a priority-1 target already beats every priority-2 one
				if (attacking == bestAttacking && distSq >= bestDistSq) continue;
				if (!Collision.CanHitLine(from, 1, 1, npc.Center, 1, 1)) continue; // LOS only for candidates that would win (it is the expensive check)
				best = i; bestDistSq = distSq; bestAttacking = attacking;
			}
			return best;
		}

		/// <summary>Things a soldier never shoots at: non-hostiles, invulnerable set pieces, disguised mimics, bound NPCs, boss hooks, projectile-NPCs, flee targets.</summary>
		public static bool IsNeverTarget(NPC npc)
		{
			if (npc == null || !npc.active) return true;
			if (!LevyUtil.IsHostileTarget(npc)) return true;          // friendly, townNPC, critter, dontTakeDamage, immortal, lifeMax <= 5
			if (npc.ModNPC is SoldierNPC) return true;
			int type = npc.type;
			if (type < 0) return true;
			if (type < NPCID.Sets.ProjectileNPC.Length && NPCID.Sets.ProjectileNPC[type]) return true;
			if (type < NPCID.Sets.IsTownPet.Length && NPCID.Sets.IsTownPet[type]) return true;
			if (IsFleeTarget(npc)) return true;
			switch (type)
			{
				case NPCID.DD2EterniaCrystal:
				case NPCID.TargetDummy:
				case NPCID.CultistTablet:
				case NPCID.CultistDevote:
				case NPCID.CultistArcherBlue:
				case NPCID.CultistArcherWhite:
				case NPCID.OldMan:
				case NPCID.BoundGoblin:
				case NPCID.BoundWizard:
				case NPCID.BoundMechanic:
				case NPCID.SleepingAngler:
				case NPCID.BartenderUnconscious:
				case NPCID.GolferRescue:
				case NPCID.WebbedStylist:
				case NPCID.PlanterasHook:
				case NPCID.PlanterasTentacle:
					return true;
				// Chest mimics wait disguised with ai[0] == 0 (NPC.cs aiStyle 25: ai[0] flips to 1 when a player comes within 100 px or it is hurt);
				// biome mimics idle the same way in aiStyle 87 before ai[0] becomes 1. Leave them asleep.
				case NPCID.Mimic:
				case NPCID.IceMimic:
				case NPCID.PresentMimic:
				case NPCID.BigMimicCorruption:
				case NPCID.BigMimicCrimson:
				case NPCID.BigMimicHallow:
				case NPCID.BigMimicJungle:
					return npc.ai[0] == 0f;
			}
			return false;
		}

		/// <summary>Enemies soldiers run from instead of fighting (never shot, kept at <see cref="LevyConstants.Targeting.FleeDistanceTiles"/>).</summary>
		public static bool IsFleeTarget(NPC npc) => npc != null && npc.active && npc.type == NPCID.DungeonGuardian;

		/// <summary>Nearest flee target within range tiles of a point, or null.</summary>
		public static NPC NearestFleeTarget(Vector2 worldPos, float rangeTiles)
		{
			NPC best = null; float bestSq = rangeTiles * rangeTiles * LevyConstants.TilePx * LevyConstants.TilePx;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!IsFleeTarget(npc)) continue;
				float d = Vector2.DistanceSquared(worldPos, npc.Center);
				if (d <= bestSq) { bestSq = d; best = npc; }
			}
			return best;
		}

		/// <summary>Record that an enemy hurt a player or a soldier (priority targeting). Call from damage code with the attacker's NPC index.</summary>
		public static void NoteAttacker(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
			NPC npc = Main.npc[npcIndex];
			if (!npc.active) { _attackTick[npcIndex] = 0; return; }
			_attackTick[npcIndex] = (int)Main.GameUpdateCount + 1;
			_attackType[npcIndex] = npc.type;
		}

		/// <summary>Drop the attacker memory of a slot (the NPC died or the slot was recycled).</summary>
		public static void ForgetAttacker(int npcIndex)
		{
			if (npcIndex >= 0 && npcIndex < Main.maxNPCs) _attackTick[npcIndex] = 0;
		}

		/// <summary>True when the enemy hurt a player or soldier within the memory window, or is currently locked on a soldier.</summary>
		public static bool IsAttackingUs(NPC npc)
		{
			if (npc == null || !npc.active) return false;
			int i = npc.whoAmI;
			if (i < 0 || i >= Main.maxNPCs) return false;
			int stamp = _attackTick[i];
			if (stamp != 0)
			{
				if (_attackType[i] != npc.type) _attackTick[i] = 0;          // the slot was reused by another enemy
				else if ((int)Main.GameUpdateCount + 1 - stamp <= LevyConstants.Targeting.AttackerMemoryTicks) return true;
				else _attackTick[i] = 0;
			}
			return LevyNpcData.Of(npc).SoldierTarget >= 0;
		}
	}

	/// <summary>Feeds the attacker table from player hurts. OnHurt runs inside Player.Hurt on every machine that applies the hit, the server included (packet 117 → Hurt(quiet: true)).</summary>
	public class AttackerTrackingPlayer : ModPlayer
	{
		public override void OnHurt(Player.HurtInfo info)
		{
			var src = info.DamageSource;
			if (src == null) return;
			int npcIndex = src.SourceNPCIndex;
			if (npcIndex >= 0) { SoldierTargeting.NoteAttacker(npcIndex); return; }
			int projIndex = src.SourceProjectileLocalIndex;
			if (projIndex < 0 || projIndex >= Main.maxProjectiles) return;
			Projectile proj = Main.projectile[projIndex];
			if (!proj.active || !proj.hostile) return;
			var tag = proj.GetGlobalProjectile<SoldierProjectileTag>();
			if (tag.SourceNpcIndex >= 0) SoldierTargeting.NoteAttacker(tag.SourceNpcIndex);
		}
	}
}
