using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Net;
using Levy.Common.Fear;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Boss
{
	/// <summary>
	/// Boss-rule plumbing: rebuilds the per-tick caches, owns the two TargetClosest detours that let a boss "see" the dummy
	/// in Main.player[255] during a dash, and restores the slot if an AI threw mid-swap.
	/// </summary>
	public class BossRuleSystem : ModSystem
	{
		public override void Load()
		{
			LevyPackets.Register(LevyPacket.BossLifeMax, (reader, from) =>
			{
				int idx = reader.ReadInt16(); int lifeMax = reader.ReadInt32();
				if (Main.netMode != NetmodeID.MultiplayerClient) return;
				if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && lifeMax > 0) Main.npc[idx].lifeMax = lifeMax;
			});
			On_NPC.TargetClosest += Hook_TargetClosest;
			On_NPC.TargetClosestUpgraded += Hook_TargetClosestUpgraded;
		}

		public override void Unload() => BossRule.ClearAll();

		public override void PreUpdateNPCs()
		{
			BossRuleGlobalNPC.EnsureRestored();
			BossRule.RebuildCaches();
		}

		public override void PostUpdateNPCs() => BossRuleGlobalNPC.EnsureRestored();

		public override void ClearWorld()
		{
			BossRuleGlobalNPC.EnsureRestored();
			BossRule.ClearAll();
		}

		public override void OnWorldUnload()
		{
			BossRuleGlobalNPC.EnsureRestored();
			BossRule.ClearAll();
		}

		// Vanilla TargetClosest loops Main.player[0..254] (NPC.cs 70931-70944) so slot 255 can never win on its own. During a dash
		// swap the boss must keep targeting 255, so after vanilla has picked a player we put the dummy back. The targeting agent's
		// own detour never touches bosses, so the two never fight over an NPC.
		private static void Hook_TargetClosest(On_NPC.orig_TargetClosest orig, NPC self, bool faceTarget)
		{
			orig(self, faceTarget);
			BossRuleGlobalNPC.AfterVanillaTargeting(self, faceTarget);
		}

		private static void Hook_TargetClosestUpgraded(On_NPC.orig_TargetClosestUpgraded orig, NPC self, bool faceTarget, Vector2? checkPosition)
		{
			orig(self, faceTarget, checkPosition);
			BossRuleGlobalNPC.AfterVanillaTargeting(self, faceTarget);
		}
	}

	/// <summary>
	/// Main-boss behaviour: phase events, spite accounting, deliberate dashes at a spite target (slot-255 swap, allowlisted
	/// bosses only), kill events. State lives in LevyNpcData (shared) and BossRule's statics; this class holds no per-NPC data.
	/// </summary>
	public class BossRuleGlobalNPC : GlobalNPC
	{
		// ---- slot-255 swap (one at a time: NPC AI calls are sequential) ----
		private static bool _swapped;
		private static int _swapNpc = -1, _savedTarget = -1;
		private static Player _backup, _dummy;
		private static readonly List<int> _pruneBuffer = new();

		public override bool PreAI(NPC npc)
		{
			TryBeginDashSwap(npc);
			return true;
		}

		public override void PostAI(NPC npc)
		{
			if (_swapped && npc.whoAmI == _swapNpc) EndSwap(npc);
			if (!LevyUtil.IsServerOrSingle || !BossRule.MainBossSet.Contains(npc.type)) return;
			var data = LevyNpcData.Of(npc);
			UpdatePhase(npc, data);
			if (LevyConfig.Get.BossesPunishSoldiers) UpdateSpite(npc, data);
			else if (data.SpiteTarget != -1 || data.SpiteTimer != 0) { data.SpiteTarget = -1; data.SpiteTimer = 0; }
		}

		// =====================================================================================================
		// Phases
		// =====================================================================================================

		private static void UpdatePhase(NPC npc, LevyNpcData data)
		{
			// Eater of Worlds heads carry only a segment's life; a worm has no meaningful life fraction.
			if (npc.type == NPCID.EaterofWorldsHead || npc.lifeMax <= 0) return;
			float fraction = npc.life / (float)npc.lifeMax;
			var thresholds = LevyConstants.Fear.PhaseThresholds;
			int phase = 0;
			for (int i = 0; i < thresholds.Length; i++) if (fraction <= thresholds[i]) phase++;
			if (phase <= data.LastPhase) return;
			data.LastPhase = phase;
			try { FearSystem.OnBossPhase(npc, phase); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: FearSystem.OnBossPhase threw: " + e); }
		}

		// =====================================================================================================
		// Spite
		// =====================================================================================================

		private static void UpdateSpite(NPC npc, LevyNpcData data)
		{
			int tick = (int)Main.GameUpdateCount;
			var bodies = BossRule.LiveBodies;
			Rectangle reach = npc.Hitbox;
			reach.Inflate(LevyConstants.SpiteDistance * LevyConstants.TilePx, LevyConstants.SpiteDistance * LevyConstants.TilePx);
			float damageThreshold = LevyConstants.SpiteDamageFraction * npc.lifeMax;

			int best = -1; float bestScore = 0f; bool currentStillQualifies = false;
			for (int i = 0; i < bodies.Count; i++)
			{
				var body = bodies[i];
				if (!body.IsAlive) continue;
				int idx = body.NPC.whoAmI;
				int nearTicks = 0;
				if (reach.Intersects(body.NPC.Hitbox))
				{
					data.NearTicks.TryGetValue(idx, out nearTicks);
					nearTicks++;
					data.NearTicks[idx] = nearTicks;
				}
				else if (data.NearTicks.Count > 0) data.NearTicks.Remove(idx);

				int sum = 0;
				if (data.RecentDamage.TryGetValue(idx, out var q)) sum = BossRule.WindowSum(q, tick);
				bool qualifies = nearTicks >= LevyConstants.SpiteTicks || (sum > 0 && sum >= damageThreshold);
				if (!qualifies) continue;
				if (idx == data.SpiteTarget) currentStillQualifies = true;
				float score = sum + nearTicks;
				if (best < 0 || score > bestScore) { best = idx; bestScore = score; }
			}

			if (currentStillQualifies) data.SpiteTimer = LevyConstants.RedirectDurationTicks;
			else if (best >= 0)
			{
				data.SpiteTarget = best;
				data.SpiteTimer = LevyConstants.RedirectDurationTicks;
				data.RedirectCounter = 0;
			}
			else if (data.SpiteTimer > 0)
			{
				data.SpiteTimer--;
				if (data.SpiteTimer <= 0) { data.SpiteTimer = 0; data.SpiteTarget = -1; }
			}
			else if (data.SpiteTarget != -1) data.SpiteTarget = -1;

			if ((tick % LevyConstants.SpitePruneIntervalTicks) == 0) PruneDeparted(data, bodies);
		}

		/// <summary>Drops NearTicks / RecentDamage entries whose soldier body is gone (slot reuse would otherwise inherit them).</summary>
		private static void PruneDeparted(LevyNpcData data, List<SoldierNPC> bodies)
		{
			if (data.NearTicks.Count == 0 && data.RecentDamage.Count == 0) return;
			_pruneBuffer.Clear();
			foreach (var key in data.NearTicks.Keys) if (!IsIn(bodies, key)) _pruneBuffer.Add(key);
			for (int i = 0; i < _pruneBuffer.Count; i++) data.NearTicks.Remove(_pruneBuffer[i]);
			_pruneBuffer.Clear();
			foreach (var key in data.RecentDamage.Keys) if (!IsIn(bodies, key)) _pruneBuffer.Add(key);
			for (int i = 0; i < _pruneBuffer.Count; i++) data.RecentDamage.Remove(_pruneBuffer[i]);
		}

		private static bool IsIn(List<SoldierNPC> bodies, int npcIndex)
		{
			for (int i = 0; i < bodies.Count; i++) if (bodies[i].NPC.whoAmI == npcIndex) return true;
			return false;
		}

		// =====================================================================================================
		// Dashes (slot-255 swap, allowlist only)
		// =====================================================================================================

		private static void TryBeginDashSwap(NPC npc)
		{
			if (_swapped || !LevyUtil.IsServerOrSingle) return;
			if (!BossRule.DashSet.Contains(npc.type) || !LevyConfig.Get.BossesPunishSoldiers) return;
			var data = LevyNpcData.Of(npc);
			if (data.SpiteTimer <= 0 || !BossRule.IsLiveBody(data.SpiteTarget, out var body)) return;
			if (body.Dummy == null || !IsDashAimTick(npc, body)) return;

			body.SyncDummyTransform();
			_backup = Main.player[SoldierNPC.DummySlot];
			_dummy = body.Dummy;
			_swapNpc = npc.whoAmI;
			_savedTarget = npc.target;
			_swapped = true;
			Main.player[SoldierNPC.DummySlot] = _dummy;
			npc.target = SoldierNPC.DummySlot; // the AI's "target == 255" check re-targets through our detour, which hands 255 back
		}

		private static void EndSwap(NPC npc)
		{
			Main.player[SoldierNPC.DummySlot] = _backup;
			npc.target = _savedTarget; // outside the dash the boss keeps chasing the player it had
			npc.netUpdate = true;      // clients predicted the dash toward a player; ship the real heading now
			_swapped = false; _swapNpc = -1; _savedTarget = -1; _dummy = null; _backup = null;
		}

		/// <summary>Safety net: if an AI threw between PreAI and PostAI the dummy must never stay in Main.player[255].</summary>
		internal static void EnsureRestored()
		{
			if (!_swapped) return;
			if (_backup != null) Main.player[SoldierNPC.DummySlot] = _backup;
			if (_swapNpc >= 0 && _swapNpc < Main.maxNPCs) Main.npc[_swapNpc].target = _savedTarget;
			_swapped = false; _swapNpc = -1; _savedTarget = -1; _dummy = null; _backup = null;
		}

		/// <summary>Runs after vanilla TargetClosest: while this NPC is swapped, the dummy in slot 255 stays its target.</summary>
		internal static void AfterVanillaTargeting(NPC self, bool faceTarget)
		{
			if (!_swapped || self.whoAmI != _swapNpc || _dummy == null || !ReferenceEquals(Main.player[SoldierNPC.DummySlot], _dummy)) return;
			self.target = SoldierNPC.DummySlot;
			self.targetRect = _dummy.Hitbox;
			if (faceTarget) self.FaceTarget();
		}

		/// <summary>
		/// The tick on which the boss reads its target to compute a dash (decompiled NPC.cs, 1.4.4.9):
		/// Eye of Cthulhu aiStyle 4: ai[1]==1 computes the charge in one tick (15003-15028 phase 1, 15317-15341 phase 2);
		///   phase 2 (ai[0]==3) ai[1]==3 computes the fast charge (15424-15518). Vanilla sets target=255 right before (14946, 15069, 15305, 15385).
		/// Retinazer aiStyle 30: ai[0]==0 &amp;&amp; ai[1]==1 (21426-21446); its second phase hovers and fires, no dash.
		/// Spazmatism aiStyle 31: ai[1]==1 in ai[0]==0 (22030-22069) and ai[0]==3 (22356-22372).
		/// Skeletron head aiStyle 11: ai[1]==1 is the spin, which homes on Main.player[target] every tick (16723-16787).
		/// Duke Fishron AI_069: the hover states 0/5/10 pick an attack when ai[2] reaches the hover time and set the dash velocity
		///   from the target on that same tick (43403-43454, 43668-43731, 43902-43938); the hover time is replicated below.
		/// </summary>
		private static bool IsDashAimTick(NPC npc, SoldierNPC body)
		{
			switch (npc.type)
			{
				case NPCID.EyeofCthulhu:
					return (npc.ai[0] == 0f && npc.ai[1] == 1f) || (npc.ai[0] == 3f && (npc.ai[1] == 1f || npc.ai[1] == 3f));
				case NPCID.Retinazer:
					return npc.ai[0] == 0f && npc.ai[1] == 1f;
				case NPCID.Spazmatism:
					return (npc.ai[0] == 0f || npc.ai[0] == 3f) && npc.ai[1] == 1f;
				case NPCID.SkeletronHead:
					return npc.ai[1] == 1f;
				case NPCID.DukeFishron:
					return FishronPicksAttackThisTick(npc, body);
			}
			return false;
		}

		private static bool FishronPicksAttackThisTick(NPC npc, SoldierNPC body)
		{
			float state = npc.ai[0];
			if (state != 0f && state != 5f && state != 10f) return false;
			bool expert = Main.expertMode;
			bool phase2 = state > 4f, phase3 = state > 9f, fresh = npc.ai[3] < LevyConstants.FishronVanilla.FreshAttackCount;
			int hover = expert ? LevyConstants.FishronVanilla.HoverExpert : LevyConstants.FishronVanilla.HoverNormal;
			if (phase3) hover = LevyConstants.FishronVanilla.HoverPhase3;
			else if (phase2 && fresh) hover = expert ? LevyConstants.FishronVanilla.HoverPhase2FreshExpert : LevyConstants.FishronVanilla.HoverPhase2FreshNormal;
			else if (fresh && !phase2 && !phase3) hover = LevyConstants.FishronVanilla.HoverPhase1Fresh;
			// During the swap "player" is the dummy, so the enrage check (target out of the ocean) is evaluated on the soldier.
			Vector2 p = body.NPC.position;
			bool enraged = p.Y < LevyConstants.FishronVanilla.EnrageSkyY || p.Y > Main.worldSurface * LevyConstants.TilePx
				|| (p.X > LevyConstants.FishronVanilla.EnrageEdgePx && p.X < Main.maxTilesX * LevyConstants.TilePx - LevyConstants.FishronVanilla.EnrageEdgePx);
			if (enraged) hover = LevyConstants.FishronVanilla.HoverEnraged;
			return npc.ai[2] + 1f >= hover;
		}

		// =====================================================================================================
		// Kills
		// =====================================================================================================

		public override void OnKill(NPC npc)
		{
			// OnKill runs inside NPCLoot (server / single player only); the NPC is still active here (NPC.cs 72151 before 71531).
			if (!BossRule.TryGetRoot(npc, out var root, out int rootType)) return;
			BossRule.ForgetNpc(npc.whoAmI);
			if (_swapped && _swapNpc == npc.whoAmI) EnsureRestored();
			if (rootType == NPCID.EaterofWorldsHead)
			{
				if (BossRule.AnyEowSegmentExcept(npc.whoAmI)) return; // the worm lives on in other segments
				root = npc;
				BossRule.EndEowFight();
			}
			else if (npc.type != rootType) return; // a hand, limb, hook or eye died, not the boss

			try { RosterSystem.Instance.OnMainBossKilled(root); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: RosterSystem.OnMainBossKilled threw: " + e); }
			try { FearSystem.OnBossKilled(root); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy: FearSystem.OnBossKilled threw: " + e); }

			var data = LevyNpcData.Of(root);
			data.SpiteTarget = -1; data.SpiteTimer = 0; data.RedirectCounter = 0;
			data.NearTicks.Clear(); data.RecentDamage.Clear();
		}
	}

	/// <summary>
	/// Hostile projectiles spawned by a main boss (or one of its parts) while it holds a spite target: every
	/// RedirectEveryNth one is re-aimed at that soldier with lead, keeping its speed.
	/// </summary>
	public class BossRuleGlobalProjectile : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			// Bosses spawn their projectiles where the AI runs (server / single player); clients only mirror.
			if (!LevyUtil.IsServerOrSingle || !projectile.hostile || projectile.friendly) return;
			if (!LevyConfig.Get.BossesPunishSoldiers) return;

			// GlobalProjectile.OnSpawn ordering between classes is not guaranteed, so derive the source ourselves first.
			NPC src = null;
			if (source is EntitySource_Parent { Entity: NPC n }) src = n;
			else
			{
				int idx = projectile.GetGlobalProjectile<SoldierProjectileTag>().SourceNpcIndex;
				if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) src = Main.npc[idx];
			}
			if (src == null || !BossRule.TryGetRoot(src, out var root, out _)) return;

			var data = LevyNpcData.Of(root);
			if (data.SpiteTimer <= 0 || !BossRule.IsLiveBody(data.SpiteTarget, out var body)) return;
			data.RedirectCounter++;
			if (data.RedirectCounter % LevyConstants.RedirectEveryNth != 0) return;

			float speed = projectile.velocity.Length();
			if (speed <= LevyConstants.RedirectMinSpeed) return;
			projectile.velocity = Aiming.SolveIntercept(projectile.Center, speed, body.NPC.Center, body.NPC.velocity, false);
			projectile.GetGlobalProjectile<SoldierProjectileTag>().Redirected = true;
			projectile.netUpdate = true; // OnSpawn runs before the spawn packet (Projectile.cs 10451 → 10454), so clients get the new heading
		}
	}
}
