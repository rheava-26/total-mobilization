using System;
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
			return LevyUtil.WithinTiles(s.NPC.Center, Main.npc[idx].Center, rangeTiles * s.BossRangeMult(Main.npc[idx]) * LevyConstants.FocusRangeMult) ? idx : -1;
		}

		/// <summary>
		/// Best enemy within <paramref name="rangeTiles"/> with line of sight, or -1. With <paramref name="useFocus"/> the commander's focus
		/// target (see <see cref="FocusTargetFor"/>) beats everything else.
		/// <paramref name="excludeIdx"/> (an NPC index, or -1) and every index in <paramref name="alsoExclude"/> are never returned and never beat a reachable enemy: they are skipped before the
		/// line-of-sight test, so targets the soldier found unreachable do not blank the pick while others stand in reach.
		/// With <paramref name="preferNearest"/> the attacker priority is ignored and the closest legal enemy in sight wins (every other filter still applies).
		/// COST: one pass over the NPC slots. The range cut runs FIRST (a few float ops) so the never-target test and the attacker lookup are only paid for the handful of slots that are
		/// actually in reach; the line-of-sight test (the expensive one) only for a candidate that would beat the best so far. Callers pace how often they scan at all (see the brain's acquisition interval).
		/// </summary>
		public static int PickTarget(SoldierNPC s, float rangeTiles, bool useFocus = true, int excludeIdx = -1, bool bossesOnly = false, bool preferNearest = false, ReadOnlySpan<int> alsoExclude = default)
		{
			if (s == null || s.NPC == null || !s.NPC.active) return -1;
			if (useFocus) { int focus = FocusTargetFor(s, rangeTiles); if (focus >= 0) return focus; }
			Vector2 from = s.NPC.Center;
			float rangePx = rangeTiles * LevyConstants.TilePx;
			float rangeSq = rangePx * rangePx;
			int self = s.NPC.whoAmI;
			int best = -1; float bestDistSq = float.MaxValue; bool bestAttacking = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || i == self || i == excludeIdx) continue;
				float distSq = Vector2.DistanceSquared(from, npc.Center);
				if (distSq > rangeSq) continue;
				if (IsIn(alsoExclude, i) || IsNeverTarget(npc)) continue;
				if (bossesOnly && !LevyUtil.IsBossNpc(npc)) continue;
				bool attacking = !preferNearest && IsAttackingUs(npc); // nearest-only: every candidate ranks as priority 2, so distance alone decides
				if (bestAttacking && !attacking) continue;                 // a priority-1 target already beats every priority-2 one
				if (attacking == bestAttacking && distSq >= bestDistSq) continue;
				if (!Collision.CanHitLine(from, 1, 1, npc.Center, 1, 1)) continue; // LOS only for candidates that would win (it is the expensive check)
				best = i; bestDistSq = distSq; bestAttacking = attacking;
			}
			return best;
		}

		private static bool IsIn(ReadOnlySpan<int> set, int value)
		{
			for (int k = 0; k < set.Length; k++) if (set[k] == value) return true;
			return false;
		}

		// A boss is the only reason for a ranged soldier's second, long-reach, boss-only scan. The answer is the same for every soldier in a tick, so it is computed once per tick.
		private static uint _bossScanStamp;
		private static bool _bossScanValue;

		/// <summary>
		/// True when any active NPC is a boss or counts as one (boss flag, ShouldBeCountedAsBoss); false means a boss-only pick can only return -1 and need not be run. A superset of
		/// <see cref="LevyUtil.IsBossNpc"/> (boss parts always have a boss root that is itself flagged). The Pumpkin / Frost Moon minibosses are not boss-flagged, so during those events this is false.
		/// One pass over the NPC slots per game tick, shared by every soldier.
		/// </summary>
		public static bool AnyBossNpcActive()
		{
			uint stamp = Main.GameUpdateCount + 1u; // 0 = never computed
			if (_bossScanStamp == stamp) return _bossScanValue;
			bool any = false;
			var counted = NPCID.Sets.ShouldBeCountedAsBoss;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				if (n.boss || (n.type >= 0 && n.type < counted.Length && counted[n.type])) { any = true; break; }
			}
			_bossScanStamp = stamp; _bossScanValue = any;
			return any;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Focus Attack: the commander designated a world position. Everything here is gated on that position being set; with none, nothing changes.
		// ------------------------------------------------------------------------------------------------------------

		/// <summary>The commander's Focus Attack position for this soldier (regulars only), or false when none is set.</summary>
		public static bool FocusPositionFor(SoldierNPC s, out Vector2 pos)
		{
			pos = default;
			if (s?.Record == null || s.NPC == null || !s.NPC.active || s.Record.Kind != SoldierKind.Regular) return false;
			return RosterSystem.FocusPositionOf(s.Record.CommanderName, out pos);
		}

		/// <summary>A critter that Focus Attack may shoot: alive, a vanilla critter, not a pet, not a town NPC, damageable.</summary>
		public static bool IsFocusCritter(NPC npc)
		{
			if (npc == null || !npc.active || npc.life <= 0 || npc.townNPC || npc.dontTakeDamage || npc.immortal || npc.ModNPC is SoldierNPC) return false;
			int type = npc.type;
			if (type <= 0 || type >= NPCID.Sets.CountsAsCritter.Length || !NPCID.Sets.CountsAsCritter[type]) return false;
			return !(type < NPCID.Sets.IsTownPet.Length && NPCID.Sets.IsTownPet[type]);
		}

		/// <summary>True when the NPC is within the focus radius of the position.</summary>
		public static bool InFocusRadius(Vector2 pos, NPC npc) => LevyUtil.WithinTiles(pos, npc.Center, LevyConstants.Targeting.FocusAttackRadiusTiles);

		/// <summary>The one exception to "critters are never targets": a critter inside the focus radius while this soldier's commander has a Focus Attack position.</summary>
		public static bool FocusAllowsCritter(SoldierNPC s, NPC npc)
			=> IsFocusCritter(npc) && FocusPositionFor(s, out Vector2 pos) && InFocusRadius(pos, npc);

		/// <summary>Same exception for a soldier's projectile hitting the critter (the shot's owner is looked up by body index).</summary>
		public static bool FocusAllowsCritterShot(int soldierIndex, NPC npc)
		{
			if (!IsFocusCritter(npc)) return false;
			SoldierNPC body = EnemyTargeting.LiveBody(soldierIndex);
			return body != null && FocusAllowsCritter(body, npc);
		}

		/// <summary>
		/// The nearest (to the soldier) legal hostile (<paramref name="critters"/> false) or critter (true) inside the focus radius of <paramref name="pos"/>, or -1.
		/// <paramref name="excludeIdx"/> and every index in <paramref name="alsoExclude"/> are skipped (targets recently found unreachable).
		/// </summary>
		public static int PickFocusAttackNpc(SoldierNPC s, Vector2 pos, bool critters, int excludeIdx, ReadOnlySpan<int> alsoExclude = default)
		{
			float radiusPx = LevyConstants.Targeting.FocusAttackRadiusTiles * LevyConstants.TilePx;
			float radiusSq = radiusPx * radiusPx;
			Vector2 from = s.NPC.Center;
			int best = -1; float bestSq = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || i == excludeIdx || i == s.NPC.whoAmI || npc.life <= 0) continue;
				if (Vector2.DistanceSquared(pos, npc.Center) > radiusSq) continue;
				if (IsIn(alsoExclude, i)) continue;
				if (critters ? !IsFocusCritter(npc) : IsNeverTarget(npc)) continue;
				float d = Vector2.DistanceSquared(from, npc.Center);
				if (d < bestSq) { bestSq = d; best = i; }
			}
			return best;
		}

		/// <summary>
		/// The point to aim at for a focus position. A point inside solid ground is walked out toward the shooter to the first open spot, so a shot at a
		/// buried block strikes its exposed face and the firing-line checks have an open point to test.
		/// </summary>
		public static Vector2 FocusAimPoint(Vector2 focus, Vector2 from)
		{
			if (!SolidAt(focus)) return focus;
			Vector2 d = from - focus; float len = d.Length();
			if (len < 1f) return focus;
			d /= len;
			float step = LevyConstants.Targeting.FocusAttackExposeStepPx, max = Math.Min(len, LevyConstants.Targeting.FocusAttackRadiusTiles * LevyConstants.TilePx);
			for (float t = step; t <= max; t += step)
			{
				Vector2 p = focus + d * t;
				if (!SolidAt(p)) return p;
			}
			return focus;
		}

		private static bool SolidAt(Vector2 world)
		{
			int x = (int)(world.X / LevyConstants.TilePx), y = (int)(world.Y / LevyConstants.TilePx);
			return x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY && WorldGen.SolidTile(x, y);
		}

		private static NPC _phantom;

		/// <summary>
		/// A stand-in target at the focus position (exposed toward <paramref name="from"/>): never in <c>Main.npc</c>, a negative whoAmI unique to the spot so the per-target caches
		/// reset when the spot changes. One shared instance, re-aimed on every call, so use it immediately and do not hold it across soldiers.
		/// </summary>
		public static NPC FocusPhantom(Vector2 focus, Vector2 from)
		{
			_phantom ??= new NPC();
			int size = LevyConstants.Targeting.FocusAttackPhantomPx;
			_phantom.active = true; _phantom.width = size; _phantom.height = size; _phantom.velocity = Vector2.Zero;
			_phantom.Center = FocusAimPoint(focus, from);
			int tx = (int)(focus.X / LevyConstants.TilePx), ty = (int)(focus.Y / LevyConstants.TilePx);
			_phantom.whoAmI = -2 - (int)(unchecked((uint)tx * 73856093u ^ (uint)ty * 19349663u) & 0x3FFFFFFF);
			return _phantom;
		}

		// IsNeverTarget is asked for every (soldier shot, NPC) pair by vanilla's projectile hit loop (the CanHitNPC hooks run before the hitbox test), by every soldier's world scan and by the
		// minions: with a sky full of shots that is hundreds of thousands of calls a tick for a few hundred distinct answers. Each NPC slot's answer is therefore computed once per game tick.
		// The answer depends on slow-moving NPC state (flags, type, a mimic's wake flag), so being up to a tick old changes nothing the player can see.
		private static readonly uint[] _neverStamp = new uint[Main.maxNPCs];
		private static readonly int[] _neverType = new int[Main.maxNPCs];
		private static readonly bool[] _neverValue = new bool[Main.maxNPCs];

		/// <summary>Things a soldier never shoots at: non-hostiles, invulnerable set pieces, disguised mimics, bound NPCs, boss hooks, projectile-NPCs, summoned blades, flee targets.</summary>
		public static bool IsNeverTarget(NPC npc)
		{
			if (npc == null || !npc.active) return true;
			int i = npc.whoAmI;
			// only a real slot of Main.npc is cached (the focus stand-in has a negative index; a stray NPC instance never matches the array entry)
			if ((uint)i < (uint)Main.maxNPCs && ReferenceEquals(Main.npc[i], npc))
			{
				uint stamp = Main.GameUpdateCount + 1u; // 0 = never computed
				int type = npc.type;
				if (_neverStamp[i] == stamp && _neverType[i] == type) return _neverValue[i];
				bool value = ComputeNeverTarget(npc);
				_neverStamp[i] = stamp; _neverType[i] = type; _neverValue[i] = value;
				return value;
			}
			return ComputeNeverTarget(npc);
		}

		private static bool ComputeNeverTarget(NPC npc)
		{
			if (!LevyUtil.IsHostileTarget(npc)) return true;          // friendly, townNPC, critter, dontTakeDamage, immortal, lifeMax <= 5
			if (npc.ModNPC is SoldierNPC) return true;
			int type = npc.type;
			if (type < 0) return true;
			if (type < NPCID.Sets.ProjectileNPC.Length && NPCID.Sets.ProjectileNPC[type]) return true;
			if (type < NPCID.Sets.IsTownPet.Length && NPCID.Sets.IsTownPet[type]) return true;
			if (IsFleeTarget(npc)) return true;
			switch (type)
			{
				// The Pumpking's two scythes are weapons on a leash, not enemies: they cannot be hurt and exist only to be dodged. (Already excluded above through dontTakeDamage; named here so
				// the rule survives even if a script or another mod makes them damageable, and so the list says what it means.)
				case NPCID.PumpkingBlade:
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
