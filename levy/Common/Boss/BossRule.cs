using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Main-boss detection, terror, spite targets, projectile redirect, HP scaling. OWNED BY THE BOSS AGENT.
	/// Hooks live in <see cref="BossRuleGlobalNPC"/>, <see cref="BossRuleGlobalProjectile"/> and <see cref="BossRuleSystem"/>;
	/// this class is the API other modules call plus the shared state (per-tick caches, HP-scaling bookkeeping).
	/// </summary>
	public static class BossRule
	{
		/// <summary>Main-boss root types (LevyConstants.MainBosses) for O(1) lookups.</summary>
		public static readonly HashSet<int> MainBossSet = new(LevyConstants.MainBosses);
		/// <summary>Bosses whose dashes may be aimed at a spite target (LevyConstants.DashAllowlist).</summary>
		public static readonly HashSet<int> DashSet = new(LevyConstants.DashAllowlist);

		// ---- per-tick caches, rebuilt by BossRuleSystem.PreUpdateNPCs ----
		/// <summary>Live main-boss ROOT NPCs this tick (every Eater of Worlds head counts as a root).</summary>
		public static readonly List<NPC> LiveBosses = new();
		/// <summary>Live soldier bodies this tick (spite accounting iterates this instead of Main.npc per boss).</summary>
		public static readonly List<SoldierNPC> LiveBodies = new();

		// ---- HP scaling bookkeeping (LevyNpcData is a shared contract; the base life lives here) ----
		private struct ScaleState { public int Type; public int BaseLifeMax; public int AppliedAdd; }
		private static readonly Dictionary<int, ScaleState> _scale = new();
		private static readonly List<int> _pruneBuffer = new();

		// ---- Eater of Worlds: segments have no realLife and heads split, so one fight record covers the whole worm ----
		private static readonly HashSet<int> _eowEngagedIds = new();
		private static int _eowEngagedCp;

		// =====================================================================================================
		// Detection
		// =====================================================================================================

		public static bool IsMainBossType(int type) => MainBossSet.Contains(type);

		/// <summary>True for a main boss or any of its parts (hands, limbs, segments, eyes, Moon Lord head/hands).</summary>
		public static bool IsMainBoss(NPC npc) => TryGetRoot(npc, out _, out _);

		/// <summary>
		/// Resolves a boss or boss part to its root NPC and root type. Order: realLife (Destroyer, Wall of Flesh eyes, other worms)
		/// → the NPC itself when it is a root type → LevyConstants.BossParts with the part's own link to its parent
		/// (Skeletron/Prime limbs ai[1], Golem parts NPC.golemBoss, Plantera NPC.plantBoss, Moon Lord parts ai[3], Eater of Worlds
		/// ai[1] chain to the head). When the parent cannot be found the nearest NPC of the root type is used, else the part itself.
		/// </summary>
		public static bool TryGetRoot(NPC npc, out NPC root, out int rootType)
		{
			root = null; rootType = -1;
			if (npc == null || !npc.active) return false;
			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && npc.realLife != npc.whoAmI)
			{
				var r = Main.npc[npc.realLife];
				if (r.active && MainBossSet.Contains(r.type)) { root = r; rootType = r.type; return true; }
			}
			if (MainBossSet.Contains(npc.type)) { root = npc; rootType = npc.type; return true; }
			if (!LevyConstants.BossParts.TryGetValue(npc.type, out rootType)) { rootType = -1; return false; }
			int idx = PartParentIndex(npc);
			if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].type == rootType) root = Main.npc[idx];
			else root = NearestOfType(rootType, npc.Center) ?? npc;
			return true;
		}

		/// <summary>The root type a boss or part belongs to, or -1.</summary>
		public static int RootType(NPC npc) => TryGetRoot(npc, out _, out int t) ? t : -1;

		private static int PartParentIndex(NPC part)
		{
			switch (part.type)
			{
				case NPCID.SkeletronHand:                                        // NPC.cs 16511: hand.ai[1] = head.whoAmI
				case NPCID.PrimeCannon: case NPCID.PrimeSaw: case NPCID.PrimeVice: case NPCID.PrimeLaser: // NPC.cs 22435: limb.ai[1] = head.whoAmI
					return (int)part.ai[1];
				case NPCID.GolemHead: case NPCID.GolemFistLeft: case NPCID.GolemFistRight: case NPCID.GolemHeadFree: // AI_047/45/46/48 read NPC.golemBoss
					return NPC.golemBoss;
				case NPCID.PlanterasHook: case NPCID.PlanterasTentacle:         // aiStyle 52/53 read NPC.plantBoss
					return NPC.plantBoss;
				case NPCID.MoonLordHead: case NPCID.MoonLordHand:                // NPC.cs 31527: part.ai[3] = core.whoAmI
				case NPCID.TheDestroyerBody: case NPCID.TheDestroyerTail:        // NPC.cs 44155: segment.ai[3] = head.whoAmI (realLife normally wins)
					return (int)part.ai[3];
				case NPCID.WallofFleshEye:                                        // NPC.cs 20832: realLife = Main.wofNPCIndex (fallback here)
					return Main.wofNPCIndex;
				case NPCID.EaterofWorldsBody: case NPCID.EaterofWorldsTail:      // NPC.cs 45707: segment.ai[1] = segment ahead; no realLife (45647)
					return WormHeadIndex(part);
			}
			return -1;
		}

		private static int WormHeadIndex(NPC segment)
		{
			int idx = segment.whoAmI;
			for (int step = 0; step < LevyConstants.WormChainMaxSteps; step++)
			{
				int next = (int)Main.npc[idx].ai[1];
				if (next < 0 || next >= Main.maxNPCs || next == idx || !Main.npc[next].active) return -1;
				int t = Main.npc[next].type;
				if (t == NPCID.EaterofWorldsHead) return next;
				if (t != NPCID.EaterofWorldsBody) return -1;
				idx = next;
			}
			return -1;
		}

		private static NPC NearestOfType(int type, Vector2 from)
		{
			NPC best = null; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.type != type) continue;
				float d = Vector2.DistanceSquared(n.Center, from);
				if (d < bestD) { bestD = d; best = n; }
			}
			return best;
		}

		/// <summary>True when any Eater of Worlds segment other than <paramref name="exceptWhoAmI"/> is active.</summary>
		public static bool AnyEowSegmentExcept(int exceptWhoAmI)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				if (i == exceptWhoAmI) continue;
				var n = Main.npc[i];
				if (n.active && (n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail)) return true;
			}
			return false;
		}

		/// <summary>A soldier body that is alive, by NPC index.</summary>
		public static bool IsLiveBody(int npcIndex, out SoldierNPC body)
		{
			body = null;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return false;
			var n = Main.npc[npcIndex];
			if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive) return false;
			body = s;
			return true;
		}

		// =====================================================================================================
		// Terror
		// =====================================================================================================

		/// <summary>Base terror of the boss's root type minus the trophy reduction when any barracks has its trophy nearby. Min 0.</summary>
		public static int TerrorOf(NPC boss)
		{
			if (!TryGetRoot(boss, out _, out int rootType)) return 0;
			return Math.Max(0, TerrorTable.BaseTerror(rootType) - TerrorTable.AnyBarracksTrophyReduction(rootType));
		}

		/// <summary>Terror as this soldier feels it: base minus the trophy reduction around the soldier's home barracks. Min 0.</summary>
		public static int TerrorFor(NPC boss, SoldierNPC soldier)
		{
			if (!TryGetRoot(boss, out _, out int rootType)) return 0;
			int terror = TerrorTable.BaseTerror(rootType);
			var home = soldier?.Home;
			if (home != null) terror -= TerrorTable.TrophyReduction(rootType, home);
			return Math.Max(0, terror);
		}

		// =====================================================================================================
		// Queries
		// =====================================================================================================

		/// <summary>Nearest living main boss within range tiles of a point, or null.</summary>
		public static NPC NearestMainBossInView(Vector2 worldPos, float rangeTiles)
		{
			NPC best = null; float bestD = rangeTiles * rangeTiles * LevyConstants.TilePx * LevyConstants.TilePx;
			for (int i = 0; i < LiveBosses.Count; i++)
			{
				var n = LiveBosses[i];
				if (!n.active) continue;
				float d = Vector2.DistanceSquared(n.Center, worldPos);
				if (d <= bestD) { bestD = d; best = n; }
			}
			return best;
		}

		public static bool AnyMainBossAlive => LiveBosses.Count > 0;

		/// <summary>The soldier body a boss currently holds a grudge against, or null.</summary>
		public static SoldierNPC SpiteTargetOf(NPC boss)
		{
			if (!TryGetRoot(boss, out var root, out _)) return null;
			var data = LevyNpcData.Of(root);
			return data.SpiteTimer > 0 && IsLiveBody(data.SpiteTarget, out var body) ? body : null;
		}

		/// <summary>Internal name key for BOSS_x dialogue (e.g. "EyeofCthulhu", "MoonLordCore"); parts resolve to their boss.</summary>
		public static string BossKey(NPC boss)
		{
			if (boss == null) return "";
			int type = TryGetRoot(boss, out _, out int rootType) ? rootType : boss.type;
			return type >= 0 && NPCID.Search.ContainsId(type) ? NPCID.Search.GetName(type) : "";
		}

		// =====================================================================================================
		// HP scaling + spite damage samples
		// =====================================================================================================

		/// <summary>
		/// Called whenever a soldier damages an enemy. On a main boss: the soldier's command points join the fight once
		/// (+BossHpScalingPercentPerCp % of base life per CP, capped at BossHpScalingCapPercent %), and the hit is recorded for
		/// spite. Eater of Worlds: engagement is per worm fight, the scaling lands on the segment that was hit.
		/// </summary>
		public static void OnSoldierDamagedNpc(NPC target, SoldierNPC soldier, int damage)
		{
			if (!LevyUtil.IsServerOrSingle || target == null || soldier == null || soldier.Record == null) return;
			if (!TryGetRoot(target, out var root, out int rootType)) return;
			var rec = soldier.Record;
			var data = LevyNpcData.Of(root);
			bool eow = rootType == NPCID.EaterofWorldsHead;
			int engagementKey = EngagementKey(rec);
			int engagedCp;
			if (eow)
			{
				if (_eowEngagedIds.Add(engagementKey)) _eowEngagedCp += rec.CommandPointCost;
				engagedCp = _eowEngagedCp;
			}
			else
			{
				if (data.EngagedSoldierIds.Add(engagementKey)) data.EngagedCp += rec.CommandPointCost;
				engagedCp = data.EngagedCp;
			}
			rec.EngagedBosses.Add(root.whoAmI);

			var cfg = LevyConfig.Get;
			float perCp = cfg.BossHpScalingPercentPerCp / 100f;
			float cap = Math.Max(0f, cfg.BossHpScalingCapPercent / 100f);
			float fraction = Math.Min(engagedCp * perCp, cap);
			ApplyScaling(eow ? target : root, fraction);

			if (damage > 0 && cfg.BossesPunishSoldiers && soldier.NPC != null) RecordDamage(data, soldier.NPC.whoAmI, damage);
		}

		/// <summary>Regulars dedupe by record Id; a record without an Id (transient militia) dedupes by object identity in the negative range.</summary>
		private static int EngagementKey(Soldiers.SoldierRecord rec)
			=> rec.Id > 0 ? rec.Id : -1 - (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rec) & 0x3FFFFFFF);

		private static bool IsEowSegment(int type) => type == NPCID.EaterofWorldsHead || type == NPCID.EaterofWorldsBody || type == NPCID.EaterofWorldsTail;

		/// <summary>Same NPC for scaling purposes. Eater of Worlds segments turn into heads/tails with a bare type change (NPC.cs 45852/45866) and keep their life.</summary>
		private static bool SameScalingKind(int a, int b) => a == b || (IsEowSegment(a) && IsEowSegment(b));

		private static void ApplyScaling(NPC npc, float fraction)
		{
			if (npc == null || !npc.active || fraction <= 0f) return;
			if (!_scale.TryGetValue(npc.whoAmI, out var st) || !SameScalingKind(st.Type, npc.type) || npc.lifeMax < st.BaseLifeMax + st.AppliedAdd)
				st = new ScaleState { Type = npc.type, BaseLifeMax = Math.Max(1, npc.lifeMax), AppliedAdd = 0 };
			int targetAdd = (int)(st.BaseLifeMax * fraction);
			int delta = targetAdd - st.AppliedAdd;
			if (delta > 0)
			{
				npc.lifeMax += delta;
				npc.life += delta;
				st.AppliedAdd = targetAdd;
				npc.netUpdate = true; // vanilla syncs life (not lifeMax); clients only need lifeMax for the health bar
			}
			_scale[npc.whoAmI] = st;
		}

		/// <summary>Life added to this NPC by soldier engagement so far (0 when untouched).</summary>
		public static int ScaledLifeAdded(NPC npc) => npc != null && _scale.TryGetValue(npc.whoAmI, out var st) && SameScalingKind(st.Type, npc.type) ? st.AppliedAdd : 0;

		private static void RecordDamage(LevyNpcData data, int soldierIndex, int damage)
		{
			int tick = (int)Main.GameUpdateCount;
			if (!data.RecentDamage.TryGetValue(soldierIndex, out var q))
			{
				q = new Queue<(int tick, int dmg)>();
				data.RecentDamage[soldierIndex] = q;
			}
			q.Enqueue((tick, damage));
			Trim(q, tick);
		}

		private static void Trim(Queue<(int tick, int dmg)> q, int tick)
		{
			while (q.Count > 0 && tick - q.Peek().tick > LevyConstants.SpiteDamageWindowTicks) q.Dequeue();
		}

		/// <summary>Damage this soldier dealt to the boss inside the spite window.</summary>
		public static int WindowSum(Queue<(int tick, int dmg)> q, int tick)
		{
			Trim(q, tick);
			int sum = 0;
			foreach (var e in q) sum += e.dmg;
			return sum;
		}

		// =====================================================================================================
		// Lifecycle (BossRuleSystem / BossRuleGlobalNPC)
		// =====================================================================================================

		/// <summary>Rebuilds the live boss and body lists and prunes bookkeeping for departed NPCs. Once per tick, before NPC updates.</summary>
		internal static void RebuildCaches()
		{
			LiveBosses.Clear(); LiveBodies.Clear();
			bool anyEow = false;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active) continue;
				if (MainBossSet.Contains(n.type)) LiveBosses.Add(n);
				else if (n.ModNPC is SoldierNPC s && s.Record != null && s.IsAlive) LiveBodies.Add(s);
				if (n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail) anyEow = true;
			}
			if (!anyEow && (_eowEngagedIds.Count > 0 || _eowEngagedCp != 0)) { _eowEngagedIds.Clear(); _eowEngagedCp = 0; }
			if (_scale.Count > 0)
			{
				_pruneBuffer.Clear();
				foreach (var kv in _scale)
				{
					if (kv.Key < 0 || kv.Key >= Main.maxNPCs || !Main.npc[kv.Key].active || !SameScalingKind(Main.npc[kv.Key].type, kv.Value.Type)) _pruneBuffer.Add(kv.Key);
				}
				for (int i = 0; i < _pruneBuffer.Count; i++) _scale.Remove(_pruneBuffer[i]);
			}
		}

		/// <summary>Drops scaling bookkeeping for one NPC slot (death).</summary>
		internal static void ForgetNpc(int whoAmI) => _scale.Remove(whoAmI);

		/// <summary>The worm fight is over: forget who engaged it.</summary>
		internal static void EndEowFight() { _eowEngagedIds.Clear(); _eowEngagedCp = 0; }

		/// <summary>World unload / clear: forget everything.</summary>
		internal static void ClearAll()
		{
			LiveBosses.Clear(); LiveBodies.Clear(); _scale.Clear(); _pruneBuffer.Clear();
			_eowEngagedIds.Clear(); _eowEngagedCp = 0;
			TerrorTable.ClearCaches();
		}
	}
}
