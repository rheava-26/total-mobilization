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
using Levy.Common.Officers;
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
	/// Bosses, ShouldBeCountedAsBoss types, realLife parts of bosses and known boss segments are never allowlisted (the boss rule owns them),
	/// except once BossesTargetSoldiers is on AND that boss type has been defeated in this world (BossDownedSystem): a first kill is always solo.
	/// An unlocked boss then takes the swap for any AI style that is not swap-unsafe (BossSwapIncompatibleAiStyles), or the native route for UsesNewTargetting types.
	/// Boss parts that carry neither a boss flag nor realLife (Moon Lord's True Eyes, Skeletron's hands…) are recognised through LevyConstants.BossParts and take the same gate.
	/// SPREAD: Compete first decides army or commander, then which soldier; see <see cref="AggroSpread"/> for how a horde is fanned out over the army instead of stacking on the nearest man.
	/// </summary>
	public static class EnemyTargeting
	{
		/// <summary>Live soldier bodies and active officers this tick (rebuilt in EnemyTargetingSystem.PreUpdateNPCs on every netmode; NPC AI runs on clients too).</summary>
		internal static readonly List<ITargetBody> Bodies = new(64);
		private static readonly HashSet<int> _swapStyles = new(LevyConstants.Targeting.SwapAiStyles);
		private static readonly HashSet<int> _nativeStyles = new(LevyConstants.Targeting.NativeAiStyles);
		private static readonly HashSet<int> _swapIncompatible = new(LevyConstants.Targeting.SwapIncompatibleAiStyles);
		private static readonly HashSet<int> _bossParts = new(LevyConstants.Targeting.BossPartTypes);
		private static readonly HashSet<int> _swapExcludedTypes = new(LevyConstants.Targeting.SwapExcludedTypes);
		private static readonly HashSet<int> _bossSwapIncompatible = new(LevyConstants.Targeting.BossSwapIncompatibleAiStyles);
		private static readonly HashSet<int> _eventTowers = new(LevyConstants.Targeting.EventTowerTypes);

		// Scratch for one Compete call: the soldiers in reach, as (index into Bodies, NPC index, weighted distance). Compete is never re-entered, so one set is enough.
		private static float[] _candD = new float[64];
		private static int[] _candNpc = new int[64];
		private static int[] _candSlot = new int[64];

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

		/// <summary>
		/// Live target body at an NPC index: a soldier, a summoned unit, or an active officer (a conscripted town NPC); null when the slot is
		/// empty or reused. Soldier-only callers (loot credit, minions, projectile tags) keep using <see cref="LiveBody"/>.
		/// </summary>
		public static ITargetBody LiveTarget(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return null;
			NPC npc = Main.npc[npcIndex];
			if (!npc.active) return null;
			if (npc.ModNPC is SoldierNPC s) return s.Record != null && s.IsAlive ? s : null;
			if (npc.ModNPC is Content.NPCs.SummonedUnitNPC u) return u.IsAlive ? u : null; // so an enemy sticks to a summoned unit without re-acquiring every tick
			if (npc.ModNPC is Content.NPCs.DroneNPC drone) return drone.IsAlive ? drone : null; // ... and to a combat drone
			return OfficerSystem.Instance.BodyOf(npc);
		}

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

		/// <summary>Config on, not friendly, not a boss or boss part (unless its boss type is unlocked), and an AI style we verified against the chosen mechanism.</summary>
		public static bool IsAllowlisted(NPC npc)
		{
			if (npc == null || !npc.active || !Enabled) return false;
			if (npc.friendly || npc.townNPC || npc.ModNPC is SoldierNPC) return false;
			int type = npc.type;
			// Lunar Pillars: boss-class, but they only spawn event attacks (AI 94 reads Main.player[target] directly, swap-safe), so they take the swap without the boss unlock.
			if (_eventTowers.Contains(type)) return true;
			if (npc.boss) return BossAllowlisted(npc);
			if (type < 0 || type >= NPCID.Sets.ShouldBeCountedAsBoss.Length) return false;
			if (NPCID.Sets.ShouldBeCountedAsBoss[type] || _bossParts.Contains(type)) return BossAllowlisted(npc);
			if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].boss) return BossAllowlisted(npc);
			// Parts with neither boss nor realLife (Skeletron hands, Prime limbs, Golem fists, Moon Lord's True Eyes…) resolve to their boss through LevyConstants.BossParts.
			if (BossRule.IsMainBoss(LevyUtil.RootNpc(npc))) return BossAllowlisted(npc);
			int style = npc.aiStyle;
			if (UsesNativeTargeting(npc)) return _nativeStyles.Contains(style) || ConfigAllows(style);
			if (_swapIncompatible.Contains(style) || _swapExcludedTypes.Contains(type)) return false;
			return _swapStyles.Contains(style) || ConfigAllows(style);
		}

		/// <summary>Opt-in: BossesTargetSoldiers is on and this boss (or the boss it belongs to) has been defeated in this world.</summary>
		public static bool BossAggroUnlocked(NPC npc) => (_cfg ??= LevyConfig.Get) != null && _cfg.BossesTargetSoldiers && BossDownedSystem.IsBossDowned(npc);

		/// <summary>
		/// A boss, boss part or boss segment (the four exclusion guards of <see cref="IsAllowlisted"/>) that the army may draw. Needs the unlock; then any AI style
		/// works through the swap except the swap-unsafe ones. Native types (Deerclops, Betsy) keep the native route, whatever their style.
		/// </summary>
		private static bool BossAllowlisted(NPC npc)
		{
			if (!BossAggroUnlocked(npc)) return false;
			// Skeletron / Skeletron Prime must not be drawn onto the army once it is daytime: they enrage lethally at dawn and would
			// endlessly massacre the deathless, respawning soldiers. At day they fight the commander as vanilla and despawn on vanilla's terms.
			if (BossPersistenceGlobalNPC.IsDaytimeExcluded(npc)) return false;
			if (UsesNativeTargeting(npc)) return true;
			int style = npc.aiStyle;
			return !_swapIncompatible.Contains(style) && !_bossSwapIncompatible.Contains(style) && !_swapExcludedTypes.Contains(npc.type);
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

		/// <summary>
		/// After a vanilla search: decide army or commander, then which soldier, and point the enemy at the chosen soldier. Returns true when a soldier was chosen.
		/// Step 1 compares the best soldier's weighted distance with vanilla's pick; step 2 (<see cref="AggroSpread.Pick"/>) takes the least crowded of the soldiers close to that best one.
		/// </summary>
		private static bool Compete(NPC npc, bool faceTarget, Vector2 center, bool manhattan, float maxSoldierDist)
		{
			if (!npc.active) return false;
			if (BossRuleGlobalNPC.IsDashSwapping(npc)) return false; // the boss rule is aiming this boss's dash at its spite target through the same slot
			LevyNpcData data = LevyNpcData.Of(npc);
			if (Bodies.Count == 0 || !IsAllowlisted(npc)) { SetLock(npc, data, -1); return false; }

			float vanillaDist = float.PositiveInfinity;
			bool vanillaIsPlayer = false;
			if (npc.target >= 0 && npc.target < Main.maxPlayers)
			{
				Player p = Main.player[npc.target];
				if (p.active && !p.dead && !p.ghost)
				{
					// targetRect is the player's hitbox, or the tank pet's when vanilla chose that
					Vector2 point = npc.targetRect.Width > 0 ? npc.targetRect.Center.ToVector2() : p.Center;
					vanillaDist = Metric(npc, center, point, manhattan) - p.aggro;
					if (p.npcTypeNoAggro[npc.type] && npc.direction != 0) vanillaDist += 1000f;
					vanillaIsPlayer = true; // the collective army pull below only ever weighs against a player
				}
			}
			else if (npc.HasNPCTarget)
			{
				int idx = npc.target - 300;
				if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) vanillaDist = Metric(npc, center, Main.npc[idx].Center, manhattan);
			}

			// A sleeping mimic only reacts to something inside its wake radius, so soldiers compete for it only that close.
			if (IsDisguisedMimic(npc)) maxSoldierDist = Math.Min(maxSoldierDist, LevyConstants.Targeting.MimicWakeSoldierRangePx);
			// A pillar heals itself when its target is beyond 2000 px, so a soldier never takes it from a commander who is in range by pulling it past that.
			if (_eventTowers.Contains(npc.type)) maxSoldierDist = Math.Min(maxSoldierDist, LevyConstants.Targeting.EventTowerSoldierRangePx);

			// STEP 1 — army or commander. Same weighting vanilla applies to players (NPC.TryTrackingTarget / TargetClosestUpgraded: distance -= player.aggro), tilted toward the army so it
			// soaks the aggro and the commander is the exception (see LevyConstants.Targeting):
			//  * every soldier carries a flat SoldierBaseAggroPx discount, and its gear aggro (Flesh Knuckles) counts SoldierTauntMultiplier times;
			//  * a fixed per-(enemy, soldier) jitter keeps enemies from all ranking the soldiers in the same order;
			//  * each soldier near the enemy (ArmyPullRadiusPx) adds ArmyPullPerSoldierPx to the commander's weighted distance, so a cluster outweighs one player.
			// Soft weighting only: a soldier that is too far away still loses to a close commander, and with no soldier in reach the vanilla pick stands. The range limit uses the real distance.
			// The army's price here is the best soldier's distance with NO crowding in it, so how many enemies are already on the army never makes the commander the better target.
			int ownLock = data.SoldierTarget; // the soldier this enemy is on right now (already counted in the tally; Pick leaves its own tally out)
			int n = 0, nearSoldiers = 0, nearest = -1; float dMin = float.PositiveInfinity;
			for (int i = 0; i < Bodies.Count; i++)
			{
				ITargetBody body = Bodies[i];
				if (!body.IsAlive) continue;
				float real = Metric(npc, center, body.NPC.Center, manhattan);
				if (real > maxSoldierDist) continue;
				if (real <= LevyConstants.Targeting.ArmyPullRadiusPx) nearSoldiers++;
				float d = real - SoldierPull(body.TargetAggro) + Spread(npc.whoAmI, body.NPC.whoAmI);
				if (n == _candD.Length) GrowScratch();
				_candSlot[n] = i; _candNpc[n] = body.NPC.whoAmI; _candD[n] = d;
				if (d < dMin) { dMin = d; nearest = n; }
				n++;
			}
			if (n == 0) { SetLock(npc, data, -1); return false; }
			if (vanillaIsPlayer) vanillaDist += Math.Min(nearSoldiers, LevyConstants.Targeting.ArmyPullMaxSoldiers) * LevyConstants.Targeting.ArmyPullPerSoldierPx;
			if (dMin >= vanillaDist) { SetLock(npc, data, -1); return false; }

			// STEP 2 — which soldier: among the ones close to the best, the least crowded (see AggroSpread). A worm's trailing segment is not an attacker of its own (it follows
			// the segment ahead), so it takes the plain nearest, as before, and stays out of the tally.
			int pick = CountsAsEnemy(npc) ? AggroSpread.Pick(_candD, _candNpc, n, dMin, ownLock) : nearest;
			if (pick < 0) { SetLock(npc, data, -1); return false; }
			ITargetBody best = Bodies[_candSlot[pick]];

			npc.target = UsesNativeTargeting(npc) ? best.NPC.WhoAmIToTargettingIndex : SoldierNPC.DummySlot;
			npc.targetRect = best.NPC.Hitbox;
			if (faceTarget)
			{
				// what SetTargetTrackingValues does for a player it decided to face
				npc.direction = best.NPC.Center.X < npc.Center.X ? -1 : 1;
				npc.directionY = best.NPC.Center.Y < npc.Center.Y ? -1 : 1;
				if (npc.confused) npc.direction *= -1;
			}
			SetLock(npc, data, best.NPC.whoAmI);
			data.RetargetTimer = 0;
			// This redirect may have happened inside the enemy's OWN AI call (e.g. a boss that re-runs TargetClosest every tick in its
			// active state), not via PreAI — which is the only place that otherwise puts the dummy in slot 255. Without the dummy there,
			// the rest of this AI call reads Main.player[255] as the inactive vanilla null player: a boss then sees "no valid target /
			// target far away" and bolts into its flee/despawn state the instant it is redirected to a soldier. Swap the dummy in now so
			// the live soldier is visible for the remainder of this AI call; PostAI / EnsureSlotRestored clears it as usual.
			if (!UsesNativeTargeting(npc) && !_swapped) Swap(best);
			return true;
		}

		/// <summary>A soldier's effective aggro in px: the flat standing advantage plus its gear aggro (positive aggro multiplied so taunt gear dominates, negative used as-is).</summary>
		private static float SoldierPull(int gearAggro) =>
			LevyConstants.Targeting.SoldierBaseAggroPx + (gearAggro > 0 ? gearAggro * LevyConstants.Targeting.SoldierTauntMultiplier : gearAggro);

		/// <summary>Fixed pseudo-random offset in [-SpreadJitterPx, +SpreadJitterPx] for an (enemy, soldier) pair; stable, so retargets never flap.</summary>
		private static float Spread(int npcWho, int bodyWho)
		{
			uint h = (uint)(npcWho * 73856093) ^ (uint)(bodyWho * 19349663);
			h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
			return ((h & 0xFFFF) / 65535f * 2f - 1f) * LevyConstants.Targeting.SpreadJitterPx;
		}

		private static void GrowScratch()
		{
			int len = _candD.Length * 2;
			Array.Resize(ref _candD, len); Array.Resize(ref _candNpc, len); Array.Resize(ref _candSlot, len);
		}

		/// <summary>
		/// True when this enemy's lock counts as one attacker on its soldier. A worm's trailing segment does not: it follows the segment ahead (every segment re-runs the target
		/// search, and a long worm would otherwise put dozens of phantom enemies on the soldiers). Worms mark followers with realLife (their head's index); the Eater of Worlds
		/// clears realLife on every segment, so its body and tail are named.
		/// </summary>
		private static bool CountsAsEnemy(NPC npc) =>
			!(npc.realLife >= 0 && npc.realLife != npc.whoAmI) && npc.type != NPCID.EaterofWorldsBody && npc.type != NPCID.EaterofWorldsTail;

		/// <summary>
		/// Points an enemy's soldier lock at a body (its NPC index) or at nobody (-1) and keeps the per-body tally of locked enemies in step.
		/// Every write to <see cref="LevyNpcData.SoldierTarget"/> goes through here, so the tally the next enemy weighs is current within the same tick.
		/// </summary>
		internal static void SetLock(NPC npc, LevyNpcData data, int bodyIndex)
		{
			int prev = data.SoldierTarget;
			if (prev == bodyIndex) return;
			if (CountsAsEnemy(npc)) AggroSpread.Move(prev, bodyIndex);
			data.SoldierTarget = bodyIndex;
		}

		/// <summary>
		/// Recounts, from scratch, how many enemies are locked on each body. Once a tick (EnemyTargetingSystem.PreUpdateNPCs), so a lock that ended without passing through
		/// SetLock — the enemy died or despawned, its slot was reused — can never leave a stale count behind.
		/// </summary>
		internal static void RebuildLoad()
		{
			AggroSpread.Clear();
			if (Bodies.Count == 0) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || !CountsAsEnemy(n)) continue;
				AggroSpread.Count(LevyNpcData.Of(n).SoldierTarget);
			}
		}

		/// <summary>Nearest live soldier body to a point within maxDist (real distance), or null. For event projectiles that choose a target themselves.</summary>
		internal static ITargetBody NearestBody(Vector2 from, float maxDist, out float dist)
		{
			ITargetBody best = null; dist = float.PositiveInfinity;
			for (int i = 0; i < Bodies.Count; i++)
			{
				ITargetBody b = Bodies[i];
				if (!b.IsAlive) continue;
				float d = Vector2.Distance(from, b.NPC.Center);
				if (d > maxDist || d >= dist) continue;
				dist = d; best = b;
			}
			return best;
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
		/// <summary>True while a soldier's dummy stands in Main.player[255] for an enemy's AI call.</summary>
		internal static bool IsSwapped => _swapped;

		internal static void Swap(ITargetBody body)
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
			_swapped = false; _backup = null; Bodies.Clear(); AggroSpread.Clear();
			_nullPlayer = Main.player[SoldierNPC.DummySlot];
		}
	}

	/// <summary>
	/// Fans a horde out over the army. Keeps a tally of how many enemies are locked on each body (key: the body's NPC index) and picks, among the soldiers close to an enemy's best choice,
	/// the one with the lowest <c>weighted distance + LoadPenaltyPx x enemies already on him</c>. Plain arithmetic over arrays, no game state, so it can be exercised outside the game.
	///  * The enemy's own lock is left out of its soldier's count, and that soldier gets <c>LockStickinessPx</c> off, so an enemy standing on a soldier neither flees him for being "crowded" by itself
	///    nor swaps for a marginal gain when the search re-runs every tick (Fighter AIs do).
	///  * Only soldiers within <c>LoadWindowPx</c> of the best soldier compete, so crowding hands an enemy to a neighbour but never sends it past a much nearer man to a far one.
	///  * No cap and no diminishing returns: the more enemies on a soldier, the more the next one prefers someone else (inside the window). With nobody locked on anyone the pick is the plain nearest.
	/// </summary>
	internal static class AggroSpread
	{
		private static readonly int[] _load = new int[LevyConstants.Targeting.LoadSlots];

		/// <summary>Forget every count (world change, or the start of a tally rebuild).</summary>
		internal static void Clear() => Array.Clear(_load, 0, _load.Length);

		/// <summary>One more enemy is locked on this body (tally rebuild). Out-of-range indices, including -1 for "nobody", are ignored.</summary>
		internal static void Count(int bodyIndex)
		{
			if ((uint)bodyIndex < (uint)_load.Length) _load[bodyIndex]++;
		}

		/// <summary>An enemy's lock moved from one body to another (-1 = none).</summary>
		internal static void Move(int from, int to)
		{
			if (from == to) return;
			if ((uint)from < (uint)_load.Length && _load[from] > 0) _load[from]--;
			if ((uint)to < (uint)_load.Length) _load[to]++;
		}

		/// <summary>Enemies locked on this body, not counting the asking enemy's own lock (<paramref name="ownLock"/> is the body it is on now, or -1).</summary>
		internal static int LoadOf(int bodyIndex, int ownLock)
		{
			if ((uint)bodyIndex >= (uint)_load.Length) return 0;
			int load = _load[bodyIndex] - (bodyIndex == ownLock ? 1 : 0);
			return load > 0 ? load : 0;
		}

		/// <summary>
		/// The candidate (index into the arrays) the enemy should take, or -1 when none qualifies. <paramref name="d"/> are the candidates' weighted distances without any crowding,
		/// <paramref name="bodyIndex"/> their NPC indices, <paramref name="dMin"/> the smallest of the first <paramref name="n"/> distances.
		/// </summary>
		internal static int Pick(float[] d, int[] bodyIndex, int n, float dMin, int ownLock)
		{
			float reach = dMin + LevyConstants.Targeting.LoadWindowPx;
			int best = -1; float bestScore = float.PositiveInfinity;
			for (int k = 0; k < n; k++)
			{
				float dk = d[k];
				if (dk > reach) continue;
				int who = bodyIndex[k];
				float score = dk + LoadOf(who, ownLock) * LevyConstants.Targeting.LoadPenaltyPx;
				if (who == ownLock) score -= LevyConstants.Targeting.LockStickinessPx;
				if (score < bestScore) { bestScore = score; best = k; }
			}
			return best;
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
			if (!EnemyTargeting.Enabled) { EnemyTargeting.RebuildLoad(); return; } // no bodies: just empties the tally
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
				if (s != null && s.IsAlive && s.Dummy != null) bodies.Add(s);
			// Officers (conscripted town NPCs) are targeted like soldiers; the list is the officer module's own per-tick cache.
			var officers = OfficerSystem.Instance.ActiveBodies;
			for (int i = 0; i < officers.Count; i++) bodies.Add(officers[i]);
			// Summoned units (an officer kit's squad) are targeted the same way, from their own per-tick cache.
			var summons = Content.NPCs.SummonedUnitSystem.Instance.ActiveBodies;
			for (int i = 0; i < summons.Count; i++) bodies.Add(summons[i]);
			// Combat drones (a Barracks' garrison) are targeted the same way, from their own per-tick cache.
			var drones = Content.NPCs.DroneSystem.Instance.ActiveBodies;
			for (int i = 0; i < drones.Count; i++) bodies.Add(drones[i]);
			// How many enemies are on each body right now, so the enemies choosing this tick can spread over the army.
			EnemyTargeting.RebuildLoad();
		}

		public override void PostUpdateNPCs() => EnemyTargeting.EnsureSlotRestored();

		// Event projectiles (EventProjectileAim) swap a soldier's dummy in for one AI call too; nothing may survive past the projectile pass.
		public override void PreUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();
		public override void PostUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();
	}

	/// <summary>Puts the targeted soldier's dummy into Main.player[255] for exactly one AI call of the enemy that targets it.</summary>
	public class EnemyTargetingGlobalNPC : GlobalNPC
	{
		public override bool PreAI(NPC npc)
		{
			if (!EnemyTargeting.Enabled) return true;
			// Both this class and the boss rule's dash use Main.player[255] for one AI call; a boss mid-dash (or about to dash) keeps the boss rule's dummy.
			if (BossRuleGlobalNPC.IsDashSwapping(npc) || BossRuleGlobalNPC.DashSwapDue(npc)) return true;
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
			ITargetBody body = EnemyTargeting.LiveTarget(data.SoldierTarget);
			if (body == null || !EnemyTargeting.IsAllowlisted(npc))
			{
				// The soldier died, despawned or the slot now holds another NPC: pick again right now so the enemy never
				// chases the null player at (0,0) or a stranger that inherited the index.
				int staleTarget = native ? data.SoldierTarget + 300 : SoldierNPC.DummySlot;
				EnemyTargeting.SetLock(npc, data, -1);
				if (npc.target == staleTarget) EnemyTargeting.Retarget(npc);
				body = EnemyTargeting.LiveTarget(data.SoldierTarget);
				if (body == null) return true;
			}
			else if (++data.RetargetTimer >= LevyConstants.Targeting.RetargetIntervalTicks)
			{
				// Styles that only re-search when their target dies (worms, piranhas, jellyfish) would otherwise follow a soldier forever.
				data.RetargetTimer = 0;
				EnemyTargeting.Retarget(npc);
				body = EnemyTargeting.LiveTarget(data.SoldierTarget);
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
