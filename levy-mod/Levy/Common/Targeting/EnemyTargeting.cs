using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Enemy → soldier targeting. OWNED BY THE TARGETING AGENT.
	///
	/// Vanilla enemies only ever target players (NPC.TargetClosest / TargetClosestUpgraded scan Main.player[0..254]; the 300+ NPC-target
	/// scheme is honoured only by AIs that call GetTargetData()). Two mechanisms make them chase soldier bodies:
	///  * SWAP (default, styles in LevyConstants.Targeting.SwapAiStyles): after the vanilla search we set npc.target = 255 (the vanilla
	///    null-player slot and "no target" sentinel) and, for the duration of that NPC's AI call, put the soldier's free-standing Dummy
	///    Player into Main.player[255] (PreAI/PostAI). Every Main.player[target] read inside the AI then sees the soldier. Verified in
	///    NPC.cs: Fighters (3) re-run TargetClosest each tick via "target == 255 || !active"; Slimes (1) via their jump cadence; Demon eyes (2),
	///    Casters (8), Bats (14), Vultures (17), Antlions (19) call TargetClosest every tick; Worms (6), Piranhas (16), Jellyfish (18),
	///    Hovering fighters (22), Mimics (25), Spiders (40), Flying fish (44) re-check on "target == 255 || dead"; none of them uses GetTargetData.
	///  * NATIVE (types with NPCID.Sets.UsesNewTargetting: Old One's Army fighters/flyers/dark mages, Goblin Shark, Queen Bee's bees):
	///    npc.target = soldier.WhoAmIToTargettingIndex (whoAmI + 300); their AI reads GetTargetData(), which resolves NPC targets. Those AIs
	///    search through NPCUtils.TargetClosestOldOnesInvasion / TargetClosestNonBees / TargetClosestCommon, so those are detoured too.
	/// Flying (5, Eater of Souls, Hornets…) is GetTargetData-based but reads Main.player[target] unguarded, so neither mechanism is safe; it is excluded
		/// (details beside LevyConstants.Targeting.SwapAiStyles; only Queen Bee's bees, which carry UsesNewTargetting, take the native route).
		/// STICKY TARGETS: many swap-style AIs (Worms incl. the Wyvern, Piranhas, Jellyfish, Mimics, Spiders, Flying fish) only call TargetClosest when
		/// target == 255 or the target is dead. An enemy whose first search was won by the player (every soldier was farther than player distance + |aggro|,
		/// or none was alive yet) therefore kept that player forever. EnemyTargetingGlobalNPC.PreAI re-runs the search for every allowlisted enemy about once
		/// a second (staggered by whoAmI) so a de-aggroed player sheds such enemies onto the army as soon as a soldier wins the comparison.
	/// Bosses, ShouldBeCountedAsBoss types, realLife parts of bosses and known boss segments are never allowlisted (the boss rule owns them).
	/// </summary>
	public static class EnemyTargeting
	{
		/// <summary>Live soldier bodies this tick (rebuilt in EnemyTargetingSystem.PreUpdateNPCs on every netmode; NPC AI runs on clients too).</summary>
		internal static readonly List<SoldierNPC> Bodies = new(64);
		private static readonly HashSet<int> _swapStyles = new(LevyConstants.Targeting.SwapAiStyles);
		private static readonly HashSet<int> _nativeStyles = new(LevyConstants.Targeting.NativeAiStyles);
		private static readonly HashSet<int> _swapIncompatible = new(LevyConstants.Targeting.SwapIncompatibleAiStyles);
		private static readonly HashSet<int> _bossParts = new(LevyConstants.Targeting.BossPartTypes);
		private static readonly HashSet<int> _swapExcludedTypes = new(LevyConstants.Targeting.SwapExcludedTypes);

		// ---- the slot-255 swap ----
		private static Player _backup;
		private static bool _swapped;
		/// <summary>The Player instance that legitimately lives in Main.player[255] (vanilla null player). Captured whenever the slot is in its normal state.</summary>
		private static Player _nullPlayer;
		private static LevyConfig _cfg;

		internal static bool Enabled => (_cfg ??= LevyConfig.Get) != null && _cfg.EnemiesTargetSoldiers;
		internal static void RefreshConfig() => _cfg = LevyConfig.Get;

		/// <summary>Soldier body (live) that this enemy is locked on, or null.</summary>
		public static SoldierNPC SoldierTargetOf(NPC npc) => npc == null || !npc.active ? null : LiveBody(LevyNpcData.Of(npc).SoldierTarget);

		/// <summary>Live soldier body at an NPC index, or null (guards reuse of the slot by another NPC).</summary>
		public static SoldierNPC LiveBody(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return null;
			NPC npc = Main.npc[npcIndex];
			if (!npc.active || npc.ModNPC is not SoldierNPC s || s.Record == null || !s.IsAlive) return null;
			return s;
		}

		/// <summary>True for types whose AI resolves 300+ NPC targets through GetTargetData() (vanilla marks them in UsesNewTargetting).</summary>
		public static bool UsesNativeTargeting(NPC npc)
		{
			int type = npc.type;
			return type >= 0 && type < NPCID.Sets.UsesNewTargetting.Length && NPCID.Sets.UsesNewTargetting[type];
		}

		/// <summary>Config on, not a boss or boss part, not friendly, and an AI style we verified against the chosen mechanism.</summary>
		public static bool IsAllowlisted(NPC npc)
		{
			if (npc == null || !npc.active || !Enabled) return false;
			if (npc.friendly || npc.townNPC || npc.boss || npc.ModNPC is SoldierNPC) return false;
			int type = npc.type;
			if (type < 0 || type >= NPCID.Sets.ShouldBeCountedAsBoss.Length) return false;
			if (NPCID.Sets.ShouldBeCountedAsBoss[type] || _bossParts.Contains(type)) return false;
			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].boss) return false;
			if (BossRule.IsMainBoss(LevyUtil.RootNpc(npc))) return false;
			int style = npc.aiStyle;
			if (UsesNativeTargeting(npc)) return _nativeStyles.Contains(style) || ConfigAllows(style);
			if (_swapIncompatible.Contains(style) || _swapExcludedTypes.Contains(type)) return false;
			return _swapStyles.Contains(style) || ConfigAllows(style);
		}

		/// <summary>Chest mimic (aiStyle 25) or biome Big Mimic (87) still waiting disguised: ai[0] == 0 until a target comes close or it is hurt.</summary>
		public static bool IsDisguisedMimic(NPC npc) => npc != null && (npc.aiStyle == NPCAIStyleID.Mimic || npc.aiStyle == NPCAIStyleID.BiomeMimic) && npc.ai[0] == 0f;

		private static bool ConfigAllows(int style)
		{
			var extra = _cfg?.ExtraTargetingAiStyles;
			return extra != null && extra.Contains(style);
		}

		// =====================================================================================================
		// Detours. Each calls the vanilla search first, then lets the nearest live soldier compete with vanilla's pick.
		// =====================================================================================================
		internal static void Hook()
		{
			On_NPC.TargetClosest += TargetClosest_Detour;
			On_NPC.TargetClosestUpgraded += TargetClosestUpgraded_Detour;
			On_NPCUtils.TargetClosestOldOnesInvasion += TargetClosestOldOnes_Detour;
			On_NPCUtils.TargetClosestNonBees += TargetClosestNonBees_Detour;
		}

		internal static void Unhook()
		{
			On_NPC.TargetClosest -= TargetClosest_Detour;
			On_NPC.TargetClosestUpgraded -= TargetClosestUpgraded_Detour;
			On_NPCUtils.TargetClosestOldOnesInvasion -= TargetClosestOldOnes_Detour;
			On_NPCUtils.TargetClosestNonBees -= TargetClosestNonBees_Detour;
		}

		private static void TargetClosest_Detour(On_NPC.orig_TargetClosest orig, NPC self, bool faceTarget)
		{
			bool netBefore = self.netUpdate;
			orig(self, faceTarget);
			// TryTrackingTarget measures |dx| + |dy| (with vanilla's own position/width quirk); mirror it so the comparison is fair.
			if (Compete(self, faceTarget, self.Center, manhattan: true, float.PositiveInfinity)
				&& self.target == self.oldTarget && self.direction == self.oldDirection && self.directionY == self.oldDirectionY)
				self.netUpdate = netBefore; // vanilla flagged a sync only because its interim player pick differed from the soldier we kept
		}

		private static void TargetClosestUpgraded_Detour(On_NPC.orig_TargetClosestUpgraded orig, NPC self, bool faceTarget, Vector2? checkPosition)
		{
			orig(self, faceTarget, checkPosition);
			Compete(self, faceTarget, checkPosition ?? self.Center, manhattan: false, float.PositiveInfinity);
		}

		private static void TargetClosestOldOnes_Detour(On_NPCUtils.orig_TargetClosestOldOnesInvasion orig, NPC searcher, bool faceTarget, Vector2? checkPosition)
		{
			orig(searcher, faceTarget, checkPosition);
			// Vanilla only lets players within 200 px distract an Old One's Army enemy from the crystal; soldiers get the same rule.
			Compete(searcher, faceTarget, searcher.Center, manhattan: false, LevyConstants.Targeting.OldOnesSoldierRangePx);
		}

		private static void TargetClosestNonBees_Detour(On_NPCUtils.orig_TargetClosestNonBees orig, NPC searcher, bool faceTarget, Vector2? checkPosition)
		{
			orig(searcher, faceTarget, checkPosition);
			Compete(searcher, faceTarget, searcher.Center, manhattan: false, float.PositiveInfinity);
		}

		/// <summary>After a vanilla search: if the nearest live soldier beats vanilla's pick, point the enemy at it. Returns true when a soldier was chosen.</summary>
		private static bool Compete(NPC npc, bool faceTarget, Vector2 center, bool manhattan, float maxSoldierDist)
		{
			if (!npc.active) return false;
			LevyNpcData data = LevyNpcData.Of(npc);
			if (Bodies.Count == 0 || !IsAllowlisted(npc)) { data.SoldierTarget = -1; return false; }

			float vanillaDist = float.PositiveInfinity;
			if (npc.target >= 0 && npc.target < Main.maxPlayers)
			{
				Player p = Main.player[npc.target];
				if (p.active && !p.dead && !p.ghost)
				{
					// targetRect is the player's hitbox, or the tank pet's when vanilla chose that
					Vector2 point = npc.targetRect.Width > 0 ? npc.targetRect.Center.ToVector2() : p.Center;
					vanillaDist = Metric(npc, center, point, manhattan) - p.aggro;
					if (p.npcTypeNoAggro[npc.type] && npc.direction != 0) vanillaDist += 1000f;
				}
			}
			else if (npc.HasNPCTarget)
			{
				int idx = npc.target - 300;
				if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) vanillaDist = Metric(npc, center, Main.npc[idx].Center, manhattan);
			}

			// A sleeping mimic only reacts to something inside its wake radius, so soldiers compete for it only that close.
			if (IsDisguisedMimic(npc)) maxSoldierDist = Math.Min(maxSoldierDist, LevyConstants.Targeting.MimicWakeSoldierRangePx);

			// Same weighting vanilla applies to players (NPC.TryTrackingTarget / TargetClosestUpgraded: distance -= player.aggro): the soldier's aggro,
			// read from its dummy's equip pass, shortens its effective distance. The range limit uses the real distance.
			SoldierNPC best = null; float bestDist = float.PositiveInfinity;
			for (int i = 0; i < Bodies.Count; i++)
			{
				SoldierNPC body = Bodies[i];
				if (!body.IsAlive) continue;
				float real = Metric(npc, center, body.NPC.Center, manhattan);
				if (real > maxSoldierDist) continue;
				float d = real - body.Aggro;
				if (d >= bestDist) continue;
				bestDist = d; best = body;
			}
			if (best == null || bestDist >= vanillaDist) { data.SoldierTarget = -1; return false; }

			npc.target = UsesNativeTargeting(npc) ? best.NPC.WhoAmIToTargettingIndex : SoldierNPC.DummySlot;
			npc.targetRect = best.NPC.Hitbox;
			if (faceTarget)
			{
				// what SetTargetTrackingValues does for a player it decided to face
				npc.direction = best.NPC.Center.X < npc.Center.X ? -1 : 1;
				npc.directionY = best.NPC.Center.Y < npc.Center.Y ? -1 : 1;
				if (npc.confused) npc.direction *= -1;
			}
			data.SoldierTarget = best.NPC.whoAmI;
			data.RetargetTimer = 0;
			return true;
		}

		private static float Metric(NPC npc, Vector2 center, Vector2 point, bool manhattan)
		{
			if (!manhattan) return Vector2.Distance(center, point);
			// NPC.TryTrackingTarget: Abs(p.Center.X - position.X + width/2) + Abs(p.Center.Y - position.Y + height/2)
			return Math.Abs(point.X - npc.position.X + npc.width / 2) + Math.Abs(point.Y - npc.position.Y + npc.height / 2);
		}

		/// <summary>True on this enemy's staggered once-a-second slot when it is allowlisted, steers itself (worm segments follow their head) and a soldier exists.</summary>
		internal static bool ShouldPollForSoldier(NPC npc)
		{
			if (Bodies.Count == 0) return false;
			if ((Main.GameUpdateCount + (uint)npc.whoAmI) % (uint)LevyConstants.Targeting.RetargetIntervalTicks != 0) return false;
			if (npc.realLife >= 0 && npc.realLife != npc.whoAmI) return false;
			return IsAllowlisted(npc);
		}

		/// <summary>Re-run the vanilla search this enemy uses (the detours decide soldier vs player again). faceTarget false: never turns the sprite.</summary>
		internal static void Retarget(NPC npc)
		{
			int type = npc.type;
			if (type >= 0 && type < NPCID.Sets.BelongsToInvasionOldOnesArmy.Length && NPCID.Sets.BelongsToInvasionOldOnesArmy[type])
				NPCUtils.TargetClosestOldOnesInvasion(npc, faceTarget: false);
			else if (type == NPCID.Bee || type == NPCID.BeeSmall)
				NPCUtils.TargetClosestNonBees(npc, faceTarget: false);
			else
				npc.TargetClosest(faceTarget: false);
		}

		// =====================================================================================================
		// Swap bookkeeping. AI calls are sequential, so one backup/flag pair is enough; everything else is a safety net.
		// =====================================================================================================
		internal static void Swap(SoldierNPC body)
		{
			if (_swapped) Unswap();
			body.SyncDummyTransform(); // active, !dead, !ghost, position/velocity of the body, whoAmI 255
			_backup = Main.player[SoldierNPC.DummySlot];
			Main.player[SoldierNPC.DummySlot] = body.Dummy;
			_swapped = true;
		}

		internal static void Unswap()
		{
			if (!_swapped) return;
			Main.player[SoldierNPC.DummySlot] = _backup ?? _nullPlayer ?? new Player();
			_backup = null;
			_swapped = false;
		}

		/// <summary>Never leave a soldier's dummy in the array: restore the real null player if anything (an exception mid-AI) skipped PostAI.</summary>
		internal static void EnsureSlotRestored()
		{
			if (_swapped) Unswap();
			Player cur = Main.player[SoldierNPC.DummySlot];
			if (cur == null) { Main.player[SoldierNPC.DummySlot] = _nullPlayer ?? new Player(); return; }
			if (_nullPlayer == null || ReferenceEquals(cur, _nullPlayer)) { _nullPlayer = cur; return; }
			if (IsAnyDummy(cur)) Main.player[SoldierNPC.DummySlot] = _nullPlayer;
			else _nullPlayer = cur; // tModLoader re-creates the slot on load; adopt whatever legitimately lives there
		}

		private static bool IsAnyDummy(Player p)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
				if (Main.npc[i].ModNPC is SoldierNPC s && ReferenceEquals(s.Dummy, p)) return true;
			return false;
		}

		internal static void ResetState()
		{
			_swapped = false; _backup = null; Bodies.Clear();
			_nullPlayer = Main.player[SoldierNPC.DummySlot];
		}
	}

	/// <summary>Per-tick body cache, detour registration and the slot-255 safety net. Runs on server and clients (both run NPC AI).</summary>
	public class EnemyTargetingSystem : ModSystem
	{
		public override void Load() => EnemyTargeting.Hook();
		public override void Unload() { EnemyTargeting.Unhook(); EnemyTargeting.ResetState(); }
		public override void OnWorldLoad() { EnemyTargeting.ResetState(); EnemyTargeting.RefreshConfig(); }
		public override void OnWorldUnload() => EnemyTargeting.EnsureSlotRestored();

		public override void PreUpdateNPCs()
		{
			EnemyTargeting.RefreshConfig();
			EnemyTargeting.EnsureSlotRestored();
			var bodies = EnemyTargeting.Bodies;
			bodies.Clear();
			if (!EnemyTargeting.Enabled) return;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
				if (s != null && s.IsAlive && s.Dummy != null) bodies.Add(s);
		}

		public override void PostUpdateNPCs() => EnemyTargeting.EnsureSlotRestored();
	}

	/// <summary>Puts the targeted soldier's dummy into Main.player[255] for exactly one AI call of the enemy that targets it.</summary>
	public class EnemyTargetingGlobalNPC : GlobalNPC
	{
		public override bool PreAI(NPC npc)
		{
			if (!EnemyTargeting.Enabled) return true;
			LevyNpcData data = LevyNpcData.Of(npc);
			if (data.SoldierTarget < 0)
			{
				// Not locked on a soldier: give the army a chance once a second. Without this a Wyvern/worm/piranha/jellyfish whose first search
				// went to the player never searches again (their AIs only re-search on target == 255 or a dead target).
				if (!EnemyTargeting.ShouldPollForSoldier(npc)) return true;
				EnemyTargeting.Retarget(npc);
				if (data.SoldierTarget < 0) return true;
			}

			bool native = EnemyTargeting.UsesNativeTargeting(npc);
			SoldierNPC body = EnemyTargeting.LiveBody(data.SoldierTarget);
			if (body == null || !EnemyTargeting.IsAllowlisted(npc))
			{
				// The soldier died, despawned or the slot now holds another NPC: pick again right now so the enemy never
				// chases the null player at (0,0) or a stranger that inherited the index.
				int staleTarget = native ? data.SoldierTarget + 300 : SoldierNPC.DummySlot;
				data.SoldierTarget = -1;
				if (npc.target == staleTarget) EnemyTargeting.Retarget(npc);
				body = EnemyTargeting.LiveBody(data.SoldierTarget);
				if (body == null) return true;
			}
			else if (++data.RetargetTimer >= LevyConstants.Targeting.RetargetIntervalTicks)
			{
				// Styles that only re-search when their target dies (worms, piranhas, jellyfish) would otherwise follow a soldier forever.
				data.RetargetTimer = 0;
				EnemyTargeting.Retarget(npc);
				body = EnemyTargeting.LiveBody(data.SoldierTarget);
				if (body == null) return true;
			}

			if (native) return true; // GetTargetData() resolves the 300+ index itself
			if (npc.target != SoldierNPC.DummySlot) npc.target = SoldierNPC.DummySlot;
			EnemyTargeting.Swap(body);
			return true;
		}

		public override void PostAI(NPC npc) => EnemyTargeting.Unswap();
	}
}
