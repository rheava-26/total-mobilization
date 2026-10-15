using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Dialogue;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// The brain: a state machine that runs every <see cref="LevyConstants.ThinkIntervalTicks"/> ticks on the server and only
	/// DECIDES. It writes State, StateTimer, TargetIndex, HasMoveGoal/MoveGoal, WantsToFallThrough, HoldPosition and (for
	/// resupply only) UsingSidearm. Movement walks to MoveGoal; Combat fires at TargetIndex.
	/// Priority per think: Dead/Desert → Routed → Flee → Engage → Resupply → order state (ReturnHome / Follow / Haul / Patrol / Idle).
	/// Shaken and Routed are owned by the Fear agent: the brain never writes State while either is set. Routed is a FIGHTING
	/// retreat: it backs away from the nearest threat (toward home / the commander when that lies on the safe side), keeps a
	/// target and keeps firing at it (Combat applies the accuracy penalty). Shaken engages like Engage and never hauls.
	/// Ranged soldiers keep a standoff from every hostile (see RangedGoal): they hold in range, step away from anything that
	/// closes inside it, and never walk toward a target they can already shoot.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- brain scratch (private; allocation-free after construction) ----
		private readonly List<Item> _carried = new();
		private readonly int[] _haulRefusedType = new int[LevyConstants.HaulRefusedMemory];
		private readonly int[] _haulRefusedTick = new int[LevyConstants.HaulRefusedMemory];
		private SoldierState _lastSeenState = SoldierState.Idle;
		private int _thinkCount;
		private int _lastEngageBarkTick = -100000;
		private int _lastTeleportTick = -100000;
		private bool _outOfAmmoBarked;
		private bool _lastHoldFire;
		private int _idleNextDecisionTick;
		private Vector2 _idleAnchor;
		private bool _idleAnchorSet;
		private int _patrolLeg;
		private int _patrolPauseUntil;
		private ArmoryTE _resupplyArmory;
		private int _resupplyFailTick = -100000;
		private int _resupplySearchTick = -100000;
		private int _resupplyWaitUntil;
		private int _manaLowSinceTick = -1;
		private int _topUpTick = -100000;
		private int _haulTargetItem = -1;
		private int _haulCooldownUntil;
		private int _haulStartedTick;
		private int _commanderLookupTick = -100000;
		// ---- engage scratch (standoff / reachability) ----
		private enum EngageMode : byte { None, Hold, Retreat, Approach }
		private EngageMode _engMode;
		/// <summary>The goal is an approach toward a target (not a kite step): Movement may path to it around gaps and walls.</summary>
		private bool _engPathOk;
		private int _threatIdx = -1;
		private float _threatTiles = float.MaxValue;
		private int _retreatDir;
		private int _unreachIdx = -1, _unreachUntil;
		private int _spotTarget = -1, _spotTick = -100000;
		private bool _spotValid;
		private Vector2 _spot;
		private readonly List<Vector2> _probePath = new();
		// ---- tactical positioning (high ground + dispersion) ----
		/// <summary>Heading for a better firing spot (a ledge / platform / higher ground) picked by <see cref="TryPerch"/>.</summary>
		private bool _perchActive;
		private Vector2 _perchGoal;
		private int _perchTarget = -1, _perchStartTick;
		/// <summary>Earliest tick of the next perch evaluation; -1 until the first one is staggered per soldier.</summary>
		private int _perchNextTick = -1;
		private int _nudgeNextTick;
		/// <summary>Crowding read from the last NPC scan: which way to step (+ right / - left, weighted), how many friends, nearest gap in tiles.</summary>
		private float _crowdAway, _crowdNearestTiles = float.MaxValue;
		private int _crowdCount;
		// scratch shared by every soldier (the brain runs on one thread): friends near the search and the best-scoring candidate spots
		private static readonly Vector2[] _friendPos = new Vector2[LevyConstants.PerchFriendCap];
		private static int _friendN;
		private static readonly Vector2[] _candPos = new Vector2[Math.Max(LevyConstants.PerchCandidates, LevyConstants.EngageSpotCandidates)];
		private static readonly float[] _candScore = new float[Math.Max(LevyConstants.PerchCandidates, LevyConstants.EngageSpotCandidates)];
		private static int _candN;
		private const int HashMulX = 73856093, HashMulY = 19349663, HashMulId = 83492791;
		private const uint HashMix = 1274126177u;
		private const float SlotSaltAltitude = 0.37f, SlotSaltPhase = 0.71f, SlotSaltMelee = 0.13f;
		// ---- search & destroy ----
		private bool _searchAnchorSet;
		private Vector2 _searchAnchor;
		private int _searchDir;
		private Vector2 _searchGoal;
		private int _searchGoalTick, _searchPauseUntil;
		private const float SlotSaltSearch = 0.53f;
		// ---- aerial engagement (brain -> Movement) ----
		/// <summary>The brain wants the body held at <see cref="AltitudeBottomY"/> (world Y of the feet) while it fights. Cleared at the top of every think.</summary>
		public bool WantsAltitude;
		public float AltitudeBottomY;
		private bool _altHeld;
		private WeaponProfile _brainMainProfile;
		private int _brainMainProfileType = -1;
		private int _brainMainProfilePrefix = -1;

		// =====================================================================================================
		// Contract entry points
		// =====================================================================================================

		partial void Brain_OnBound()
		{
			ResetBrainScratch();
			_lastHoldFire = Record.HoldFire;
			if (IsMilitia)
			{
				State = SoldierState.Patrol;
				DialogueSystem.Bark(this, DialogueTrigger.MUSTER, MakeContext());
			}
			else
			{
				switch (Record.Order)
				{
					case SoldierOrder.Follow: State = SoldierState.Follow; break;
					case SoldierOrder.Hold: HoldPosition = true; State = SoldierState.Idle; break;
					case SoldierOrder.Patrol: State = SoldierState.Patrol; break;
					case SoldierOrder.ReturnToBarracks: State = SoldierState.ReturnHome; break;
					case SoldierOrder.SearchDestroy: State = SoldierState.SearchDestroy; break;
					default: State = SoldierState.Idle; break;
				}
			}
			_lastSeenState = State;
			StateTimer = 0;
			TargetIndex = -1;
			HasMoveGoal = false; WantsToFallThrough = false;
		}

		partial void Brain_OnOrder(SoldierOrder order)
		{
			TargetIndex = -1;
			HasMoveGoal = false; WantsToFallThrough = false;
			_haulTargetItem = -1;
			_idleAnchorSet = false;
			_searchAnchorSet = false; _searchDir = 0; _searchPauseUntil = 0; // any new order (or a re-entry from fear) restarts the sweep
			// Fear owns Shaken/Routed and the roster owns Desert/Dead: the order is recorded (core) and takes effect when they let go.
			if (State == SoldierState.Shaken || State == SoldierState.Routed || State == SoldierState.Desert || State == SoldierState.Dead) return;
			if (IsMilitia)
			{
				// Militia only ever stand down; every other order keeps them on their settlement patrol.
				SetState(order == SoldierOrder.ReturnToBarracks ? SoldierState.ReturnHome : SoldierState.Patrol);
				_lastSeenState = State;
				return;
			}
			switch (order)
			{
				case SoldierOrder.Follow: HoldPosition = false; SetState(SoldierState.Follow); break;
				case SoldierOrder.Hold: HoldPosition = true; SetState(SoldierState.Idle); break;
				case SoldierOrder.Patrol: HoldPosition = false; _patrolLeg = 0; _patrolPauseUntil = 0; SetState(SoldierState.Patrol); break;
				case SoldierOrder.ReturnToBarracks: HoldPosition = false; SetState(SoldierState.ReturnHome); break;
				case SoldierOrder.SearchDestroy: HoldPosition = false; SetState(SoldierState.SearchDestroy); break;
			}
			_lastSeenState = State;
		}

		partial void Brain_Think()
		{
			if (Record == null || !NPC.active) return;
			_altHeld &= WantsAltitude; // the exit latch only carries over a think that was actually aloft
			WantsAltitude = false; // re-asserted below by TryEngage when there is still an airborne target worth climbing to
			int now = (int)Main.GameUpdateCount;
			SoldierState prevState = State; int prevTarget = TargetIndex;
			_thinkCount++;
			RefreshCommanderIndex(now);

			// Another module (Fear, Roster, Settlement) changed the state since the last think: restart the state clock.
			if (State != _lastSeenState) { StateTimer = 0; _lastSeenState = State; }
			else StateTimer += LevyConstants.ThinkIntervalTicks;

			// Hold-fire edge: drop the target and say so.
			if (Record.HoldFire && !_lastHoldFire) { TargetIndex = -1; DialogueSystem.Bark(this, DialogueTrigger.HOLD_FIRE, MakeContext()); }
			_lastHoldFire = Record.HoldFire;

			ScanNpcs(out int fleeIdx, out float nearestHostileTiles);

			switch (State)
			{
				case SoldierState.Dead:
					break;
				case SoldierState.Desert:
					ThinkDesert();
					break;
				case SoldierState.Routed:
					ThinkRouted(nearestHostileTiles, now);
					break;
				default:
					// Shaken is driven here too: it engages like Engage (Combat applies the penalties) and never hauls.
					if (fleeIdx >= 0) ThinkFlee(fleeIdx);
					else if (!TryEngage(now))
					{
						if (!TryResupply(now)) ThinkOrder(now, nearestHostileTiles);
					}
					break;
			}

			if (!NPC.active) return; // despawned this think
			if (State != prevState) StateTimer = 0;
			_lastSeenState = State;
			if (State != prevState || TargetIndex != prevTarget) NPC.netUpdate = true;
		}

		/// <summary>Drops everything the soldier was hauling at its feet (death, dismissal, desertion). Server only.</summary>
		public void DropCarriedItems()
		{
			if (_carried.Count == 0) return;
			DropCarried(NPC.Center);
		}

		// =====================================================================================================
		// Shared helpers
		// =====================================================================================================

		private void ResetBrainScratch()
		{
			_carried.Clear();
			for (int i = 0; i < _haulRefusedType.Length; i++) { _haulRefusedType[i] = -1; _haulRefusedTick[i] = -100000; }
			_thinkCount = 0; _outOfAmmoBarked = false; _idleAnchorSet = false; _idleNextDecisionTick = 0;
			_patrolLeg = 0; _patrolPauseUntil = 0;
			_resupplyArmory = null; _resupplyFailTick = -100000; _resupplySearchTick = -100000; _resupplyWaitUntil = 0;
			_manaLowSinceTick = -1; _topUpTick = -100000;
			_haulTargetItem = -1; _haulCooldownUntil = 0; _haulStartedTick = 0;
			_lastTeleportTick = -100000; _lastEngageBarkTick = -100000; _commanderLookupTick = -100000;
			_engMode = EngageMode.None; _engPathOk = false; _threatIdx = -1; _threatTiles = float.MaxValue; _retreatDir = 0;
			_unreachIdx = -1; _unreachUntil = 0; _spotTarget = -1; _spotTick = -100000; _spotValid = false;
			WantsAltitude = false; _altHeld = false;
			_perchActive = false; _perchTarget = -1; _perchNextTick = -1; _nudgeNextTick = 0;
			_crowdAway = 0f; _crowdCount = 0; _crowdNearestTiles = float.MaxValue;
			_searchAnchorSet = false; _searchDir = 0; _searchGoalTick = 0; _searchPauseUntil = 0;
		}

		/// <summary>Spread Out multiplier on every dispersion spacing (1 when off).</summary>
		private float SpreadMult => Record != null && Record.SpreadOut ? LevyConstants.SpreadOutMult : 1f;

		/// <summary>A commander who relogged gets a new player index; re-resolve the name once a second while the index is stale.</summary>
		private void RefreshCommanderIndex(int now)
		{
			if (!IsRegular || string.IsNullOrEmpty(Record.CommanderName) || Commander != null) return;
			if (now - _commanderLookupTick < 60) return;
			_commanderLookupTick = now;
			Record.CommanderIndex = LevyUtil.FindPlayerByName(Record.CommanderName);
		}

		/// <summary>Writes State unless the Fear agent owns the current label (Shaken, Routed): only Fear moves out of those.</summary>
		private void SetState(SoldierState s)
		{
			if (State == s) return;
			if (State == SoldierState.Routed || State == SoldierState.Shaken) return;
			State = s;
			StateTimer = 0;
			if (s != SoldierState.Idle) _idleAnchorSet = false;
			if (s != SoldierState.Haul) _haulTargetItem = -1;
			if (s != SoldierState.Resupply) { _resupplyArmory = null; _resupplyWaitUntil = 0; }
		}

		private void SetGoal(Vector2 goal)
		{
			HasMoveGoal = true; MoveGoal = goal;
			WantsToFallThrough = goal.Y > NPC.Bottom.Y + LevyConstants.FallThroughBelowTiles * LevyConstants.TilePx;
		}

		private void ClearGoal() { HasMoveGoal = false; WantsToFallThrough = false; }

		private DialogueContext MakeContext()
		{
			var home = Home;
			return new DialogueContext { Player = Commander?.name ?? "", Town = home != null ? SettlementSystem.Instance.TownName(home) : "" };
		}

		private static bool ValidNpc(int idx) => idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active;

		/// <summary>One pass over the NPC array: the nearest flee-list NPC inside the flee scan and the nearest hostile distance.</summary>
		private void ScanNpcs(out int fleeIdx, out float nearestHostileTiles)
		{
			fleeIdx = -1; float fleeBest = float.MaxValue; float hostileBest = float.MaxValue;
			Vector2 c = NPC.Center;
			float tileSq = LevyConstants.TilePx * LevyConstants.TilePx;
			_crowdAway = 0f; _crowdCount = 0; _crowdNearestTiles = float.MaxValue;
			float crowdReach = LevyConstants.DispersionMinGapTiles * SpreadMult + LevyConstants.DispersionVerticalTiles;
			float crowdSq = crowdReach * crowdReach * tileSq;
			float fleeScanSq = LevyConstants.FleeScanTiles * LevyConstants.FleeScanTiles * tileSq;
			// threat = nearest enemy that matters (hitbox distance, so a wide boss counts from its edge); prefilter on the centre distance
			float threatBest = LevyConstants.ThreatScanTiles * LevyConstants.ThreatScanTiles * tileSq; int threatIdx = -1;
			float threatPrefilterSq = (LevyConstants.ThreatScanTiles + 30f) * (LevyConstants.ThreatScanTiles + 30f) * tileSq;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || i == NPC.whoAmI) continue;
				float dsq = Vector2.DistanceSquared(c, n.Center);
				if (dsq < fleeScanSq && dsq < fleeBest && SoldierTargeting.IsFleeTarget(n)) { fleeBest = dsq; fleeIdx = i; }
				if (dsq < hostileBest && LevyUtil.IsHostileTarget(n)) hostileBest = dsq;
				if (dsq < crowdSq && n.type == NPC.type && n.ModNPC is SoldierNPC friend && friend.State != SoldierState.Dead) AccumulateCrowd(n);
				if (dsq < threatPrefilterSq)
				{
					float hsq = Vector2.DistanceSquared(c, n.Hitbox.ClosestPointInRect(c));
					if (hsq < threatBest && !SoldierTargeting.IsNeverTarget(n)) { threatBest = hsq; threatIdx = i; }
				}
			}
			_threatIdx = threatIdx;
			_threatTiles = threatIdx < 0 ? float.MaxValue : MathF.Sqrt(threatBest) / LevyConstants.TilePx;
			nearestHostileTiles = hostileBest == float.MaxValue ? float.MaxValue : MathF.Sqrt(hostileBest) / LevyConstants.TilePx;
		}

		/// <summary>The state the standing order asks for right now (no fear, no target, nothing to haul).</summary>
		private SoldierState OrderState()
		{
			if (IsMilitia)
			{
				// Stand-down is signalled by the settlement writing State = ReturnHome directly (and Mobilized = false); honour
				// both so a body on its way home is never turned back onto patrol. Re-mobilizing writes Idle, which lands on Patrol.
				var home = Home;
				bool standingDown = State == SoldierState.ReturnHome || Record.Order == SoldierOrder.ReturnToBarracks || home == null || !home.Mobilized;
				return standingDown ? SoldierState.ReturnHome : SoldierState.Patrol;
			}
			switch (Record.Order)
			{
				case SoldierOrder.Follow:
				{
					var cmd = Commander;
					if (cmd != null && !cmd.dead) return SoldierState.Follow;
					return Home == null || NearHome() ? SoldierState.Idle : SoldierState.ReturnHome;
				}
				case SoldierOrder.Hold: return SoldierState.Idle;
				case SoldierOrder.Patrol: return SoldierState.Patrol;
				case SoldierOrder.SearchDestroy: return SoldierState.SearchDestroy;
				case SoldierOrder.ReturnToBarracks:
				{
					if (Home == null)
					{
						// Homeless (barracks destroyed): there is nowhere to return to, so fall in behind a living commander.
						var cmd = Commander;
						return cmd != null && !cmd.dead ? SoldierState.Follow : SoldierState.Idle;
					}
					return NearHome() ? SoldierState.Idle : SoldierState.ReturnHome;
				}
			}
			return SoldierState.Idle;
		}

		private bool NearHome()
		{
			var home = Home;
			return home != null && LevyUtil.WithinTiles(NPC.Center, home.MusterPoint, LevyConstants.NearHomeTiles);
		}

		private bool AtMusterPoint()
		{
			var home = Home;
			return home != null && LevyUtil.WithinTiles(NPC.Center, home.MusterPoint, LevyConstants.HomeArriveTiles);
		}

		/// <summary>Profile of the issued main weapon (not the sidearm), cached by type + prefix.</summary>
		private WeaponProfile MainProfile()
		{
			var w = Record.Gear.Weapon;
			if (w == null || w.IsAir) { _brainMainProfile = null; _brainMainProfileType = -1; return null; }
			if (_brainMainProfile == null || _brainMainProfileType != w.type || _brainMainProfilePrefix != w.prefix)
			{
				_brainMainProfile = WeaponClassifier.GetProfile(w); _brainMainProfileType = w.type; _brainMainProfilePrefix = w.prefix;
			}
			return _brainMainProfile;
		}

		private static bool ArmoryAlive(ArmoryTE a) => a != null && TileEntity.ByID.TryGetValue(a.ID, out var te) && ReferenceEquals(te, a);

		// =====================================================================================================
		// 1. Desert / 2. Routed / 3. Flee
		// =====================================================================================================

		private void ThinkDesert()
		{
			TargetIndex = -1;
			float leftX = LevyConstants.DesertEdgeMarginPx;
			float rightX = Main.maxTilesX * (float)LevyConstants.TilePx - LevyConstants.DesertEdgeMarginPx;
			float edgeX = NPC.Center.X - leftX < rightX - NPC.Center.X ? leftX : rightX;
			// Reaching the edge despawns the body here; the roster's TickDeserters force-despawns it after DesertDespawnTicks
			// regardless (it checks the body is still live, so a slot reused after this despawn is never touched).
			if (Math.Abs(NPC.Center.X - edgeX) < LevyConstants.DesertArriveTiles * LevyConstants.TilePx)
			{
				DropCarriedItems();
				ClearGoal();
				// The roster strikes the record when it rolls the desertion; only report the walk-off if it still owns the record.
				if (RosterSystem.Instance.Regulars.Contains(Record)) RosterSystem.Instance.OnBodyDeserted(this);
				NPC.active = false; NPC.netUpdate = true;
				return;
			}
			SetGoal(new Vector2(edgeX, NPC.Center.Y));
		}

		/// <summary>
		/// Fighting retreat. The body keeps (or picks) a target and Combat keeps firing at it with the routed accuracy penalty;
		/// this only decides where to run. Away from the nearest threat; to the anchor (home muster point, or the commander
		/// of a following regular) when the anchor lies on the safe side; with nothing near, walk to the anchor and stand.
		/// </summary>
		private void ThinkRouted(float nearestHostileTiles, int now)
		{
			_haulTargetItem = -1; _engPathOk = false; _engMode = EngageMode.None; _perchActive = false;
			KeepRoutedTarget(now);

			bool hasAnchor = RoutedAnchor(out Vector2 anchor);
			bool anchorArrived = hasAnchor && LevyUtil.WithinTiles(NPC.Center, anchor, LevyConstants.RoutedAnchorArriveTiles);
			if (_threatIdx >= 0 && _threatTiles < LevyConstants.ThreatScanTiles)
			{
				NPC threat = Main.npc[_threatIdx];
				float away = AwayDir(threat.Center);
				if (hasAnchor && !anchorArrived && Math.Sign(anchor.X - NPC.Center.X) == (int)away) SetGoal(anchor);
				else SetGoal(new Vector2(NPC.Center.X + away * LevyConstants.RoutedRetreatTiles * LevyConstants.TilePx, NPC.Center.Y));
				return;
			}
			if (hasAnchor && !anchorArrived) SetGoal(anchor);
			else ClearGoal();
		}

		/// <summary>Direction (+1 / -1) that increases the distance from a point; sticks to the previous answer when directly above or below it.</summary>
		private float AwayDir(Vector2 from)
		{
			float dx = NPC.Center.X - from.X;
			if (Math.Abs(dx) >= LevyConstants.TilePx) _retreatDir = dx > 0f ? 1 : -1;
			else if (_retreatDir == 0) _retreatDir = NPC.direction == 0 ? 1 : -NPC.direction;
			return _retreatDir;
		}

		/// <summary>Where a routed body runs for safety: the commander of a following regular, else the home muster point.</summary>
		private bool RoutedAnchor(out Vector2 anchor)
		{
			if (IsRegular && Record.Order == SoldierOrder.Follow)
			{
				var cmd = Commander;
				if (cmd != null && !cmd.dead) { anchor = cmd.Bottom; return true; }
			}
			var home = Home;
			if (home != null) { anchor = home.MusterPoint; return true; }
			anchor = default; return false;
		}

		/// <summary>A routed body keeps shooting: hold the current target while it stays in reach, else take the best one in sight.</summary>
		private void KeepRoutedTarget(int now)
		{
			if (!CanFight(now)) { TargetIndex = -1; return; }
			float range = AcquireRangeTiles();
			if (ValidNpc(TargetIndex))
			{
				var t = Main.npc[TargetIndex];
				if (t.life > 0 && !SoldierTargeting.IsNeverTarget(t) && LevyUtil.WithinTiles(NPC.Center, t.Center, range * LevyConstants.TargetKeepRangeMult)) return;
			}
			TargetIndex = -1;
			int pick = SoldierTargeting.PickTarget(this, range);
			if (ValidNpc(pick) && Main.npc[pick].life > 0 && !SoldierTargeting.IsNeverTarget(Main.npc[pick])) TargetIndex = pick;
		}

		private int NearestHostileIndex()
		{
			int best = -1; float bestSq = float.MaxValue; Vector2 c = NPC.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || !LevyUtil.IsHostileTarget(n)) continue;
				float dsq = Vector2.DistanceSquared(c, n.Center);
				if (dsq < bestSq) { bestSq = dsq; best = i; }
			}
			return best;
		}

		private void RunAwayFrom(Vector2 threat)
		{
			float dir = NPC.Center.X >= threat.X ? 1f : -1f;
			SetGoal(new Vector2(NPC.Center.X + dir * LevyConstants.FleeRunTiles * LevyConstants.TilePx, NPC.Center.Y));
		}

		private void ThinkFlee(int fleeIdx)
		{
			TargetIndex = -1;
			_haulTargetItem = -1; _engMode = EngageMode.None; _engPathOk = false; _perchActive = false;
			if (State == SoldierState.Engage || State == SoldierState.Resupply || State == SoldierState.Haul) SetState(OrderState());
			RunAwayFrom(Main.npc[fleeIdx].Center);
		}

		// =====================================================================================================
		// 4. Engage (also drives Shaken)
		// =====================================================================================================

		/// <summary>True when the weapon in hand can actually be used right now.</summary>
		private bool CanFight(int now)
		{
			if (Record.HoldFire || Weapon == null || !Weapon.Supported) return false;
			if (Weapon.IsRanged && Weapon.UseAmmo > 0 && !Record.Gear.HasAmmo) return false;
			if (Weapon.Family == WeaponFamily.Magic && Weapon.Mana > 0 && ManaStarved(now)) return false;
			return true;
		}

		private float AcquireRangeTiles() => Weapon.IsMelee ? LevyConstants.MeleeAcquireRangeTiles : Weapon.RangeTiles;

		private bool TargetStillValid(int idx, float rangeTiles)
		{
			if (!ValidNpc(idx)) return false;
			var n = Main.npc[idx];
			if (n.life <= 0 || SoldierTargeting.IsNeverTarget(n) || !LevyUtil.IsHostileTarget(n)) return false;
			if (!LevyUtil.WithinTiles(NPC.Center, n.Center, rangeTiles * LevyConstants.TargetKeepRangeMult)) return false;
			return WithinChaseBounds(n, rangeTiles);
		}

		/// <summary>Posts, holds, settlements and commanders bound how far a soldier may be dragged by a target.</summary>
		private bool WithinChaseBounds(NPC target, float rangeTiles)
		{
			if (HoldPosition && !LevyUtil.WithinTiles(Record.HoldPos, target.Center, LevyConstants.HoldChaseLimit + rangeTiles)) return false;
			if ((Record.Order == SoldierOrder.Patrol || IsMilitia) && TryNearestPatrolPoint(out var post)
				&& !LevyUtil.WithinTiles(post, target.Center, LevyConstants.PostChaseLimit + rangeTiles)) return false;
			if (IsMilitia)
			{
				var home = Home;
				if (home != null && Math.Abs(target.Center.X - home.Center.X) > (LevyConstants.SettlementRadiusX + rangeTiles) * LevyConstants.TilePx) return false;
			}
			if (IsRegular && Record.Order == SoldierOrder.Follow && !HoldPosition)
			{
				var cmd = Commander;
				if (cmd != null && !LevyUtil.WithinTiles(cmd.Center, target.Center, LevyConstants.FollowChaseLimit + rangeTiles)) return false;
			}
			if (IsRegular && Record.Order == SoldierOrder.SearchDestroy && !LevyUtil.WithinTiles(SearchAnchor(), target.Center, LevyConstants.SearchLeashTiles + rangeTiles)) return false;
			return true;
		}

		/// <summary>
		/// The commander's focus target when this soldier may act on it: a regular, in reach (x FocusRangeMult), inside the order's chase
		/// bounds and not recently found unreachable. Otherwise -1 and normal target selection applies.
		/// </summary>
		private int FocusTargetIndex(float rangeTiles, int now)
		{
			if (!IsRegular) return -1;
			int idx = SoldierTargeting.FocusTargetFor(this, rangeTiles);
			if (idx < 0) return -1;
			if (idx == _unreachIdx && now < _unreachUntil) return -1;
			return WithinChaseBounds(Main.npc[idx], rangeTiles) ? idx : -1;
		}

		private bool TryEngage(int now)
		{
			_engPathOk = false;
			if (!CanFight(now)) { TargetIndex = -1; _engMode = EngageMode.None; _perchActive = false; return false; }
			float range = AcquireRangeTiles();
			if (IsRegular && Record.Order == SoldierOrder.SearchDestroy) range *= LevyConstants.SearchAcquireRangeMult; // hunters run enemies down
			NPC target = null;
			int focus = FocusTargetIndex(range, now);
			if (focus >= 0) { TargetIndex = focus; target = Main.npc[focus]; } // the commander's designated target outranks everything
			else if (TargetStillValid(TargetIndex, range)) target = Main.npc[TargetIndex];
			else
			{
				TargetIndex = -1;
				int pick = SoldierTargeting.PickTarget(this, range, useFocus: false);
				if (ValidNpc(pick) && Main.npc[pick].life > 0 && !SoldierTargeting.IsNeverTarget(Main.npc[pick]) && WithinChaseBounds(Main.npc[pick], range))
				{
					TargetIndex = pick; target = Main.npc[pick];
				}
			}
			// a melee target the body could not path to is left alone for a while
			if (target != null && TargetIndex == _unreachIdx && now < _unreachUntil) { TargetIndex = -1; target = null; }
			if (target == null) { _engMode = EngageMode.None; _perchActive = false; return false; }

			if (State != SoldierState.Engage && State != SoldierState.Shaken)
			{
				SetState(SoldierState.Engage);
				if (now - _lastEngageBarkTick >= LevyConstants.EngageBarkCooldownTicks) { _lastEngageBarkTick = now; DialogueSystem.Bark(this, DialogueTrigger.ENGAGE, MakeContext()); }
			}

			if (Weapon.IsMelee) { MeleeGoal(target, now); return true; }
			if (RangedGoal(target, now)) return true;
			// nothing to shoot from here and nowhere to shoot from: drop it so the soldier carries on with its order
			// (ignored until the cached "no route" answer expires, so the state does not flap every think)
			_unreachIdx = TargetIndex; _unreachUntil = _spotTick + LevyConstants.EngageSpotRecheckTicks;
			TargetIndex = -1; _engMode = EngageMode.None; _perchActive = false;
			WantsAltitude = false; _altHeld = false;
			return false;
		}

		/// <summary>
		/// Aerial engagement. A soldier with a flight kit whose target is well above it (and in reach) asks Movement for a firing
		/// altitude instead of shooting up from the ground. Written every think; Movement owns the climb, hover and budget.
		/// The altitude is the target's level less FiringAltitudeBelowTargetTiles, pushed further below it while the target is closer
		/// than the standoff (so the standoff holds in 2D), capped by the kit's reach, MaxAltitudeTiles over the last solid ground and the
		/// top of the world. A latch (VerticalEngageExitTiles) stops the intent flickering on the threshold.
		/// </summary>
		private void UpdateAltitudeIntent(NPC seg, float rangeTiles, float standoffTiles, bool melee)
		{
			WantsAltitude = false;
			if (!HasFlightKit || AerialMaxReachTiles < LevyConstants.MinClimbBenefitTiles) { _altHeld = false; return; }
			float tilePx = LevyConstants.TilePx;
			float sepTiles = (NPC.Center.Y - seg.Center.Y) / tilePx; // > 0: the target is above the body centre
			float need = _altHeld ? LevyConstants.VerticalEngageMinTiles - LevyConstants.VerticalEngageExitTiles : LevyConstants.VerticalEngageMinTiles;
			if (sepTiles < need) { _altHeld = false; return; }
			if (!melee)
			{
				float reach = rangeTiles * LevyConstants.ShootableRangeFrac * (_altHeld ? LevyConstants.AerialRangeKeepMult : 1f);
				if (Aiming.TilesToHitbox(NPC.Center, seg) > reach) { _altHeld = false; return; } // not close enough yet: the ground approach handles it
			}

			float belowTiles = LevyConstants.FiringAltitudeBelowTargetTiles;
			if (melee) belowTiles = 0f;
			else
			{
				float dxTiles = Math.Abs(seg.Center.X - NPC.Center.X) / tilePx;
				if (dxTiles < standoffTiles) belowTiles = Math.Max(belowTiles, MathF.Sqrt(standoffTiles * standoffTiles - dxTiles * dxTiles));
				belowTiles += SlotFrac(SlotSaltAltitude) * LevyConstants.DispersionAltitudeSpreadTiles * SpreadMult; // hovering squads fan out in altitude, only ever further from the target
			}
			float bottom = seg.Center.Y + belowTiles * tilePx + BodyHeight * 0.5f;
			float groundY = AerialGroundY == float.MinValue ? NPC.Bottom.Y : AerialGroundY;
			float maxPx = Math.Min(LevyConstants.MaxAltitudeTiles, AerialMaxReachTiles * LevyConstants.MaxReachUseFrac) * tilePx;
			float capped = Math.Max(bottom, groundY - maxPx);
			capped = Math.Max(capped, (LevyConstants.WorldTopMarginTiles + 3) * tilePx);
			bool truncated = capped - bottom > LevyConstants.MeleeAerialSlackTiles * tilePx;
			// melee must actually reach its target; a jump-only kit has no hover, so it only goes when it can fully get there
			if (truncated && (melee || !AerialSustained)) { _altHeld = false; return; }
			if (groundY - capped < LevyConstants.MinClimbBenefitTiles * tilePx) { _altHeld = false; return; }
			WantsAltitude = true; AltitudeBottomY = capped; _altHeld = true;
		}

		/// <summary>Distance (tiles) inside which a ranged soldier backs away from any hostile.</summary>
		private static float StandoffTiles(float range)
			=> Math.Min(Math.Max(LevyConstants.KiteMin, range * LevyConstants.StandoffRangeFrac), range * LevyConstants.StandoffMaxRangeFrac);

		/// <summary>Distance (tiles) a ranged soldier walks out to when it has to close on a target.</summary>
		private static float HoldMaxTiles(float range) => Math.Min(LevyConstants.KiteMax, range * LevyConstants.KiteMaxRangeFrac);

		/// <summary>
		/// Gun / bow / magic. Priority: (1) anything hostile inside the standoff -> step away (still facing and firing);
		/// (2) a target in reach with a clear shot -> hold and shoot, never walk toward it (so a gap or pit between us is irrelevant);
		/// (3) otherwise find a spot that is both reachable and a firing line, or stand and wait if there is none.
		/// </summary>
		private bool RangedGoal(NPC target, int now)
		{
			float range = Weapon.RangeTiles;
			float standoff = StandoffTiles(range);
			NPC seg = Aiming.NearestSegment(target, NPC.Center) ?? target;
			UpdateAltitudeIntent(seg, range, standoff, false); // independent of the horizontal plan below: climb, retreat and shoot compose

			// leaving a retreat takes a little extra room (hysteresis), so the body does not stutter on the standoff line
			float standoffNow = standoff + (_engMode == EngageMode.Retreat ? LevyConstants.StandoffHysteresisTiles : 0f);
			if (_threatIdx >= 0 && _threatTiles < standoffNow)
			{
				float away = AwayDir(Main.npc[_threatIdx].Center);
				Vector2 step = new Vector2(NPC.Center.X + away * LevyConstants.KiteStepAwayTiles * LevyConstants.TilePx, NPC.Center.Y);
				ApplyChaseClamp(ref step);
				_engMode = EngageMode.Retreat;
				if (_perchActive) AbandonPerch(now); // the standoff outranks the climb to a better spot
				SetGoal(step);
				return true;
			}
			if (_engMode == EngageMode.Retreat) ClearGoal();

			float dT = Aiming.TilesToHitbox(NPC.Center, seg);
			bool los = HasFiringLine(seg);

			if (dT <= range * LevyConstants.ShootableRangeFrac && los)
			{
				// Aloft the aerial system owns position; on the ground, first look for better footing, then for elbow room.
				bool aloft = WantsAltitude || _aerial;
				if (!aloft && TryPerch(target, seg, range, standoff, now)) return true;
				if (_engMode == EngageMode.Approach) ClearGoal();
				_engMode = EngageMode.Hold;
				if (!aloft && _crowdCount > 0 && now >= _nudgeNextTick && TryDisperseStep(seg, range, standoff, now)) return true;
				// In the pocket: a small lateral shuffle every other think, only with room to spare past the standoff.
				if ((_thinkCount & 1) != 0 || _threatTiles < standoff + LevyConstants.StrafeMarginTiles) return true;
				float side = ((_thinkCount >> 1) & 1) == 0 ? 1f : -1f;
				Vector2 shuffle;
				if (aloft) shuffle = new Vector2(NPC.Center.X + side * LevyConstants.StrafeTiles * LevyConstants.TilePx, NPC.Center.Y);
				else if (!FindSafeStep((int)side, LevyConstants.StrafeTiles, seg.Center, range, standoff, false, out shuffle)
					&& !FindSafeStep(-(int)side, LevyConstants.StrafeTiles, seg.Center, range, standoff, false, out shuffle)) return true; // a ledge edge: stay put
				ApplyChaseClamp(ref shuffle);
				SetGoal(shuffle);
				return true;
			}

			if (_perchActive) AbandonPerch(now); // out of reach or no line from here: the approach search takes over

			if (FindEngageSpot(target, seg, range, standoff, now, out Vector2 spot))
			{
				ApplyChaseClamp(ref spot);
				_engMode = EngageMode.Approach; _engPathOk = true;
				SetGoal(spot);
				return true;
			}
			// No route to a firing line (or the target is across a gap out of reach): stand here rather than walk into the gap.
			if (_engMode == EngageMode.Approach) ClearGoal();
			_engMode = EngageMode.Hold;
			return false;
		}

		/// <summary>A shot from the muzzle would reach the segment (projectiles that ignore tiles always can).</summary>
		private bool HasFiringLine(NPC seg) => ShotIgnoresTiles() || Aiming.HasLineOfSight(WeaponUse.Muzzle(this), seg.Center);

		/// <summary>
		/// Somewhere to stand that is inside reach, outside the standoff, has a firing line to the target and can be walked to.
		/// A handful of candidates on the line from the target toward us (shifted sideways by this soldier's own slot so a squad
		/// does not converge on one tile), each SCORED (<see cref="SpotMerit"/>: height, ledge, platform, travel, crowding); the best
		/// get one path search each, in score order. The answer is cached per target for EngageSpotRecheckTicks.
		/// </summary>
		private bool FindEngageSpot(NPC target, NPC seg, float range, float standoff, int now, out Vector2 spot)
		{
			if (_spotTarget == target.whoAmI && now - _spotTick < LevyConstants.EngageSpotRecheckTicks) { spot = _spot; return _spotValid; }
			_spotTarget = target.whoAmI; _spotTick = now; _spotValid = false; _spot = default;
			spot = default;

			Vector2 tc = seg.Center;
			Vector2 toUs = NPC.Center - tc;
			if (toUs.LengthSquared() < 1f) toUs = new Vector2(NPC.direction == 0 ? 1f : -NPC.direction, 0f);
			toUs.Normalize();
			float holdMax = HoldMaxTiles(range);
			float nearest = Math.Min(standoff + 2f, holdMax);
			int n = Math.Max(2, LevyConstants.EngageSpotCandidates);
			bool ignoresTiles = ShotIgnoresTiles();
			GatherFriends();
			float slotPx = SlotOffsetTiles(0f, LevyConstants.DispersionSpreadTiles * SpreadMult) * LevyConstants.TilePx;
			for (int pass = 0; pass < 2; pass++)
			{
				// pass 0 fans out by slot; pass 1 (only if the shifted points found nothing at all) is the plain line
				if (pass == 1 && (slotPx == 0f || _candN > 0)) break;
				float shift = pass == 0 ? slotPx : 0f;
				_candN = 0;
				for (int k = 0; k < n; k++)
				{
					float distTiles = MathHelper.Lerp(holdMax, nearest, k / (float)(n - 1));
					Vector2 pt = tc + toUs * distTiles * LevyConstants.TilePx;
					pt.X += shift;
					if (!TileNav.NearestStandable((int)(pt.X / 16f), (int)(pt.Y / 16f), LevyConstants.EngageSpotSearchRadius, out Point tile)) continue;
					Vector2 bottom = TileNav.BottomCentre(tile.X, tile.Y);
					if (!SpotFires(bottom, tc, range, standoff, LevyConstants.ShootableRangeFrac, 0f, ignoresTiles)) continue;
					InsertCandidate(bottom, SpotMerit(bottom, tc), n);
				}
				for (int i = 0; i < _candN; i++)
				{
					Vector2 bottom = _candPos[i];
					bool here = Vector2.Distance(NPC.Bottom, bottom) < 2f * LevyConstants.TilePx;
					if (!here && !Pathfinder.FindPath(NPC.Bottom, bottom, Abilities, _probePath)) continue;
					_spot = bottom; _spotValid = true; spot = bottom;
					return true;
				}
			}
			return false;
		}

		// =====================================================================================================
		// 4b. Tactical positioning: high ground (perch) and dispersion
		// =====================================================================================================

		/// <summary>This soldier's stable fraction in [0,1): a golden-ratio sequence over the body index, so neighbouring indices land far apart.</summary>
		private float SlotFrac(float salt)
		{
			float f = (NPC.whoAmI + 1) * LevyConstants.SlotGoldenStep + salt;
			return f - MathF.Floor(f);
		}

		/// <summary>This soldier's stable offset in [-spread, +spread) tiles.</summary>
		private float SlotOffsetTiles(float salt, float spreadTiles) => (SlotFrac(salt) - 0.5f) * 2f * spreadTiles;

		/// <summary>Deterministic per-soldier, per-tile noise in [-PerchJitter/2, +PerchJitter/2].</summary>
		private float TileJitter(int x, int y)
		{
			uint h = unchecked((uint)(NPC.whoAmI * HashMulId) ^ (uint)(x * HashMulX) ^ (uint)(y * HashMulY));
			h ^= h >> 13; h = unchecked(h * HashMix); h ^= h >> 16;
			return ((h & 0xFFFFu) / 65535f - 0.5f) * LevyConstants.PerchJitter;
		}

		/// <summary>Adds one friendly soldier in the crowding box to the push read by <see cref="TryDisperseStep"/>.</summary>
		private void AccumulateCrowd(NPC other)
		{
			float tilePx = LevyConstants.TilePx;
			float dx = other.Center.X - NPC.Center.X;
			float adx = Math.Abs(dx);
			float gapPx = LevyConstants.DispersionMinGapTiles * SpreadMult * tilePx;
			if (adx >= gapPx || Math.Abs(other.Bottom.Y - NPC.Bottom.Y) >= LevyConstants.DispersionVerticalTiles * tilePx) return;
			float weight = Math.Max(0.1f, 1f - adx / gapPx);
			float away = adx < 1f ? (NPC.whoAmI < other.whoAmI ? -1f : 1f) : (dx > 0f ? -1f : 1f); // exactly stacked: the lower index yields left
			_crowdAway += away * weight; _crowdCount++;
			_crowdNearestTiles = Math.Min(_crowdNearestTiles, adx / tilePx);
		}

		/// <summary>Friendly soldiers near enough to matter for crowding a perch; a soldier already travelling counts at its destination.</summary>
		private void GatherFriends()
		{
			_friendN = 0;
			float reachPx = (LevyConstants.PerchSearchRadiusTiles + LevyConstants.DispersionSpacingTiles * SpreadMult) * LevyConstants.TilePx;
			for (int i = 0; i < Main.maxNPCs && _friendN < _friendPos.Length; i++)
			{
				var o = Main.npc[i];
				if (!o.active || i == NPC.whoAmI || o.type != NPC.type || Math.Abs(o.Center.X - NPC.Center.X) > reachPx) continue;
				if (o.ModNPC is not SoldierNPC f || f.Record == null || f.State == SoldierState.Dead) continue;
				_friendPos[_friendN++] = f._perchActive ? f._perchGoal : o.Bottom;
			}
		}

		/// <summary>Merit lost to friends standing on or near a spot (same height band only).</summary>
		private float CrowdPenalty(Vector2 bottom)
		{
			float tilePx = LevyConstants.TilePx, pen = 0f;
			float spacingPx = LevyConstants.DispersionSpacingTiles * SpreadMult * tilePx, bandPx = LevyConstants.DispersionVerticalTiles * tilePx;
			for (int i = 0; i < _friendN; i++)
			{
				float dx = Math.Abs(_friendPos[i].X - bottom.X);
				if (dx >= spacingPx || Math.Abs(_friendPos[i].Y - bottom.Y) >= bandPx) continue;
				pen += LevyConstants.DispersionCrowdPenalty * (1f - dx / spacingPx);
			}
			return pen;
		}

		/// <summary>
		/// How good a spot to shoot from is (higher is better), ignoring range and line of sight: height over the target, a ledge over
		/// open ground below, a platform underfoot, minus the walk there and any friends already standing around it, plus per-soldier
		/// noise that splits near-equal spots between soldiers. <see cref="GatherFriends"/> must have run for this search.
		/// </summary>
		private float SpotMerit(Vector2 bottom, Vector2 tc)
		{
			float tilePx = LevyConstants.TilePx;
			float bodyY = bottom.Y - BodyHeight * 0.5f;
			float cap = LevyConstants.PerchElevationCapTiles;
			float merit = LevyConstants.PerchElevationWeight * MathHelper.Clamp((tc.Y - bodyY) / tilePx, -cap, cap);
			Point feet = TileNav.FeetTile(bottom);
			int below = TileNav.DropDepth(feet.X, feet.Y + 1); // empty tiles under this floor before the next one; -1 none within the scan, -2 lava
			float gap = below == -1 ? LevyConstants.PerchLedgeCapTiles : below < 0 ? 0f : Math.Min(below, LevyConstants.PerchLedgeCapTiles);
			merit += LevyConstants.PerchLedgeWeight * gap;
			if (TileNav.IsPlatform(feet.X, feet.Y + 1)) merit += LevyConstants.PerchPlatformBonus;
			merit -= LevyConstants.PerchTravelCostPerTile * Vector2.Distance(NPC.Bottom, bottom) / tilePx;
			merit -= CrowdPenalty(bottom);
			merit += TileJitter(feet.X, feet.Y);
			return merit;
		}

		/// <summary>The spot is inside reach (x rangeFrac), past the standoff (+ margin) and, unless the shot ignores tiles, has a firing line.</summary>
		private bool SpotFires(Vector2 bottom, Vector2 tc, float range, float standoff, float rangeFrac, float standoffMargin, bool ignoresTiles, bool checkLos = true)
		{
			Vector2 body = bottom - new Vector2(0f, BodyHeight * 0.5f);
			float d = Vector2.Distance(body, tc) / LevyConstants.TilePx;
			if (d > range * rangeFrac || d < standoff + standoffMargin) return false;
			return !checkLos || ignoresTiles || Aiming.HasLineOfSight(body, tc);
		}

		/// <summary>Keeps the best <paramref name="cap"/> candidates sorted by score (descending); duplicates are dropped.</summary>
		private static void InsertCandidate(Vector2 pos, float score, int cap)
		{
			for (int i = 0; i < _candN; i++) if (Vector2.DistanceSquared(_candPos[i], pos) < 1f) return;
			int at = _candN;
			while (at > 0 && _candScore[at - 1] < score) at--;
			if (at >= cap) return;
			for (int j = Math.Min(_candN, cap - 1); j > at; j--) { _candPos[j] = _candPos[j - 1]; _candScore[j] = _candScore[j - 1]; }
			_candPos[at] = pos; _candScore[at] = score;
			_candN = Math.Min(_candN + 1, cap);
		}

		/// <summary>A chosen spot must be where the order lets this soldier stand (hold radius, post, settlement, commander leash).</summary>
		private bool WithinChaseClamp(Vector2 bottom)
		{
			Vector2 g = bottom;
			ApplyChaseClamp(ref g);
			return Vector2.DistanceSquared(g, bottom) < 4f;
		}

		private void AbandonPerch(int now)
		{
			_perchActive = false;
			_perchNextTick = Math.Max(_perchNextTick, now + LevyConstants.PerchFailCooldownTicks);
			if (_engMode == EngageMode.Approach) ClearGoal();
		}

		/// <summary>
		/// High ground. Called while the soldier can already shoot from where it stands. Scores its own footing against standable
		/// tiles in a box around it (every PerchColumnStepTiles-th column, PerchMaxRiseTiles up to PerchMaxDropTiles down), keeps the
		/// best few that are inside reach, past the standoff and within the order's leash, and relocates to the first of them (by score)
		/// that has a firing line and a walkable path, but only when it beats the current footing by PerchMinImprovement. Evaluated at
		/// most once per EngageSpotRecheckTicks, then PerchRelocateCooldownTicks after a decision and PerchSettleTicks after arriving.
		/// Returns true while a relocation is under way (the goal is set). Never called aloft or while backing off.
		/// </summary>
		private bool TryPerch(NPC target, NPC seg, float range, float standoff, int now)
		{
			float tilePx = LevyConstants.TilePx;
			Vector2 tc = seg.Center;
			if (_perchActive)
			{
				bool arrived = Math.Abs(NPC.Bottom.X - _perchGoal.X) < LevyConstants.PerchArriveXTiles * tilePx
					&& Math.Abs(NPC.Bottom.Y - _perchGoal.Y) < LevyConstants.PerchArriveYTiles * tilePx;
				if (arrived)
				{
					_perchActive = false;
					_perchNextTick = Math.Max(_perchNextTick, now + LevyConstants.PerchSettleTicks);
					return false;
				}
				bool stalled = now - _perchStartTick > LevyConstants.PerchStallGraceTicks && _blockedHard && _path.Count == 0 && _pathFailTimer > 0;
				if (target.whoAmI != _perchTarget || stalled || now - _perchStartTick > LevyConstants.PerchTravelTimeoutTicks
					|| !SpotFires(_perchGoal, tc, range, standoff, LevyConstants.ShootableRangeFrac, 0f, ShotIgnoresTiles()))
				{
					AbandonPerch(now);
					return false;
				}
				_engMode = EngageMode.Approach; _engPathOk = true;
				SetGoal(_perchGoal);
				return true;
			}

			if (_perchNextTick < 0) { _perchNextTick = now + (int)(SlotFrac(SlotSaltPhase) * LevyConstants.EngageSpotRecheckTicks); return false; } // stagger the first look
			if (now < _perchNextTick) return false;
			_perchNextTick = now + LevyConstants.EngageSpotRecheckTicks;
			if (NPC.velocity.Y != 0f) return false; // only judge footing while standing on it

			GatherFriends();
			bool ignoresTiles = ShotIgnoresTiles();
			float current = SpotMerit(NPC.Bottom, tc);
			float need = current + LevyConstants.PerchMinImprovement;
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int r = LevyConstants.PerchSearchRadiusTiles, step = Math.Max(1, LevyConstants.PerchColumnStepTiles);
			int phase = (int)(SlotFrac(SlotSaltPhase) * step);
			_candN = 0;
			for (int cx = feet.X - r + phase; cx <= feet.X + r; cx += step)
			{
				for (int cy = feet.Y - LevyConstants.PerchMaxRiseTiles; cy <= feet.Y + LevyConstants.PerchMaxDropTiles; cy++)
				{
					if (Math.Abs(cx - feet.X) <= 1 && Math.Abs(cy - feet.Y) <= 1) continue;
					if (!TileNav.InBounds(cx, cy) || !TileNav.IsStandable(cx, cy)) continue;
					Vector2 bottom = TileNav.BottomCentre(cx, cy);
					if (!SpotFires(bottom, tc, range, standoff, LevyConstants.PerchRangeFrac, LevyConstants.PerchStandoffMarginTiles, true, false)) continue;
					if (!WithinChaseClamp(bottom)) continue;
					float merit = SpotMerit(bottom, tc);
					if (merit >= need) InsertCandidate(bottom, merit, LevyConstants.PerchCandidates);
				}
			}

			int pathChecks = 0;
			for (int i = 0; i < _candN; i++)
			{
				Vector2 bottom = _candPos[i];
				if (!SpotFires(bottom, tc, range, standoff, LevyConstants.PerchRangeFrac, LevyConstants.PerchStandoffMarginTiles, ignoresTiles)) continue;
				if (pathChecks++ >= LevyConstants.PerchPathChecks) break;
				if (!Pathfinder.FindPath(NPC.Bottom, bottom, Abilities, _probePath)) continue;
				_perchActive = true; _perchGoal = bottom; _perchTarget = target.whoAmI; _perchStartTick = now;
				_perchNextTick = now + LevyConstants.PerchRelocateCooldownTicks;
				_engMode = EngageMode.Approach; _engPathOk = true;
				SetGoal(bottom);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Elbow room. Steps sideways, away from the friends crowding this soldier, to roughly DispersionSpacingTiles of space, but only
		/// onto firm footing at the same level that is still a legal firing spot (standoff and reach hold). No spot: stays (the perch
		/// search can still move it). Rate-limited by DispersionNudgeCooldownTicks.
		/// </summary>
		private bool TryDisperseStep(NPC seg, float range, float standoff, int now)
		{
			if (Math.Abs(_crowdAway) < 0.01f) return false; // pressed from both sides equally
			int dir = _crowdAway > 0f ? 1 : -1;
			int steps = DisperseSteps();
			if (!FindSafeStep(dir, steps, seg.Center, range, standoff, true, out Vector2 dest)) return false;
			ApplyChaseClamp(ref dest);
			_nudgeNextTick = now + LevyConstants.DispersionNudgeCooldownTicks;
			SetGoal(dest);
			return true;
		}

		/// <summary>Columns one nudge covers: enough to reach the (Spread Out scaled) spacing from the nearest neighbour, capped by the (scaled) nudge length.</summary>
		private int DisperseSteps()
		{
			float m = SpreadMult;
			return Math.Clamp((int)Math.Ceiling(LevyConstants.DispersionSpacingTiles * m - _crowdNearestTiles), 1, (int)Math.Ceiling(LevyConstants.DispersionNudgeMaxTiles * m));
		}

		/// <summary>A standable tile up to <paramref name="maxSteps"/> columns away in <paramref name="dir"/> at the same level (+/- one row); no range or line tests. False if none, so a ledge edge is never stepped off.</summary>
		private bool FindFlatStep(int dir, int maxSteps, out Vector2 dest)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			for (int k = maxSteps; k >= 1; k--)
			{
				int x = feet.X + dir * k;
				for (int row = 0; row < 3; row++)
				{
					int y = feet.Y + (row == 0 ? 0 : row == 1 ? -1 : 1);
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					dest = TileNav.BottomCentre(x, y);
					return true;
				}
			}
			dest = default;
			return false;
		}

		/// <summary>
		/// A standable tile up to <paramref name="maxSteps"/> columns away in <paramref name="dir"/> (the farthest that works) at the same
		/// level (+/- one row) that still satisfies the standoff and reach. False if none, so a ledge edge is never stepped off.
		/// </summary>
		private bool FindSafeStep(int dir, int maxSteps, Vector2 tc, float range, float standoff, bool checkLos, out Vector2 dest)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			bool ignoresTiles = checkLos && ShotIgnoresTiles();
			for (int k = maxSteps; k >= 1; k--)
			{
				int x = feet.X + dir * k;
				for (int row = 0; row < 3; row++)
				{
					int y = feet.Y + (row == 0 ? 0 : row == 1 ? -1 : 1);
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					Vector2 bottom = TileNav.BottomCentre(x, y);
					if (!SpotFires(bottom, tc, range, standoff, LevyConstants.ShootableRangeFrac, 0f, ignoresTiles, checkLos)) continue;
					dest = bottom;
					return true;
				}
			}
			dest = default;
			return false;
		}

		/// <summary>
		/// Melee closes on its target. If the direct walk has been refused (gap, wall, lava) and Movement's own path search found no
		/// route either, the target is unreachable: stop pushing at it and leave it alone for a few seconds.
		/// </summary>
		private void MeleeGoal(NPC target, int now)
		{
			Vector2 goal;
			UpdateAltitudeIntent(target, 0f, 0f, true);
			if (now - LastMeleeHitTick < LevyConstants.MeleeStepBackWindowTicks)
			{
				float sx = NPC.Center.X >= target.Center.X ? 1f : -1f;
				goal = new Vector2(NPC.Center.X + sx * LevyConstants.MeleeStepBackPixels * 2, NPC.Center.Y);
				_engMode = EngageMode.Retreat;
			}
			else if (!LevyUtil.WithinTiles(NPC.Center, target.Center, LevyConstants.RangeMelee))
			{
				if (_blockedHard && _path.Count == 0 && _pathFailTimer > 0)
				{
					_unreachIdx = TargetIndex; _unreachUntil = now + LevyConstants.UnreachableIgnoreTicks;
					TargetIndex = -1; _engMode = EngageMode.Hold;
					ClearGoal();
					return;
				}
				// each swordsman aims for its own spot beside the target, within the melee reach, so a squad does not stack on one tile
				goal = target.Center + new Vector2(SlotOffsetTiles(SlotSaltMelee, LevyConstants.DispersionMeleeSpreadTiles * (Record.SpreadOut ? LevyConstants.SpreadOutMult : 1f)) * LevyConstants.TilePx, 0f);
				_engMode = EngageMode.Approach; _engPathOk = true;
			}
			else { _engMode = EngageMode.Hold; ClearGoal(); return; }
			ApplyChaseClamp(ref goal);
			SetGoal(goal);
		}

		private void ApplyChaseClamp(ref Vector2 goal)
		{
			if (HoldPosition) ClampToCircle(ref goal, Record.HoldPos, LevyConstants.HoldChaseLimit);
			if ((Record.Order == SoldierOrder.Patrol || IsMilitia) && TryNearestPatrolPoint(out var post)) ClampToCircle(ref goal, post, LevyConstants.PostChaseLimit);
			if (IsMilitia)
			{
				var home = Home;
				if (home != null)
				{
					float r = LevyConstants.SettlementRadiusX * LevyConstants.TilePx;
					goal.X = MathHelper.Clamp(goal.X, home.Center.X - r, home.Center.X + r);
				}
			}
			if (IsRegular && Record.Order == SoldierOrder.Follow && !HoldPosition)
			{
				var cmd = Commander;
				if (cmd != null) ClampToCircle(ref goal, cmd.Center, LevyConstants.FollowChaseLimit);
			}
			if (IsRegular && Record.Order == SoldierOrder.SearchDestroy) ClampToCircle(ref goal, SearchAnchor(), LevyConstants.SearchLeashTiles);
		}

		private static void ClampToCircle(ref Vector2 goal, Vector2 center, float radiusTiles)
		{
			float r = radiusTiles * LevyConstants.TilePx;
			Vector2 d = goal - center;
			float len = d.Length();
			if (len > r && len > 0.001f) goal = center + d * (r / len);
		}

		private bool TryNearestPatrolPoint(out Vector2 post)
		{
			var a = Record.PatrolA; var b = Record.PatrolB;
			bool hasA = a != Point16.NegativeOne, hasB = b != Point16.NegativeOne;
			if (!hasA && !hasB) { post = default; return false; }
			if (!hasA) { post = PatrolWorld(b); return true; }
			if (!hasB) { post = PatrolWorld(a); return true; }
			Vector2 wa = PatrolWorld(a), wb = PatrolWorld(b);
			post = Vector2.DistanceSquared(NPC.Center, wa) <= Vector2.DistanceSquared(NPC.Center, wb) ? wa : wb;
			return true;
		}

		/// <summary>Banner tile → world point: centred on the column, feet on the banner's bottom row.</summary>
		private static Vector2 PatrolWorld(Point16 p) => new Vector2(p.X * LevyConstants.TilePx + LevyConstants.TilePx * 0.5f, (p.Y + 3) * LevyConstants.TilePx);

		// =====================================================================================================
		// 5. Resupply
		// =====================================================================================================

		/// <summary>Magic: the pool has sat below one cast's cost for ManaStarvedTicks without recovering.</summary>
		private bool ManaStarved(int now)
		{
			var main = MainProfile();
			if (main == null || main.Family != WeaponFamily.Magic || main.Mana <= 0 || Record.Mana >= main.Mana) { _manaLowSinceTick = -1; return false; }
			if (_manaLowSinceTick < 0) { _manaLowSinceTick = now; return false; }
			return now - _manaLowSinceTick >= LevyConstants.ManaStarvedTicks;
		}

		private bool TryResupply(int now)
		{
			var main = MainProfile();
			bool dry = main != null && main.IsRanged && main.UseAmmo > 0 && !Record.Gear.HasAmmo;
			bool starved = !dry && main != null && main.Family == WeaponFamily.Magic && ManaStarved(now);
			if (!dry && !starved)
			{
				_outOfAmmoBarked = false;
				if (State == SoldierState.Resupply) { SetState(OrderState()); ClearGoal(); }
				TopUp(now, main);
				return false;
			}

			if (State == SoldierState.Resupply)
			{
				var armory = _resupplyArmory;
				if (!ArmoryAlive(armory))
				{
					SetState(OrderState()); // the armory is gone; search again below
				}
				else if (LevyUtil.WithinTiles(NPC.Center, armory.Center, LevyConstants.ArmoryArriveTiles))
				{
					ClearGoal();
					if (dry)
					{
						int taken = armory.TakeAmmo(main.UseAmmo, Record.Gear.Ammo, LevyConstants.AmmoPouchMax);
						if (taken > 0) { Rearmed(); return false; }
						_resupplyFailTick = now; // empty shelves: sidearm, try again later
						SetState(OrderState());
					}
					else
					{
						if (_resupplyWaitUntil == 0) _resupplyWaitUntil = now + LevyConstants.MagicRegenWaitTicks;
						if (now < _resupplyWaitUntil) return true;
						_manaLowSinceTick = -1;
						Rearmed(); return false;
					}
				}
				else
				{
					SetGoal(armory.Center);
					return true;
				}
			}

			if (State != SoldierState.Resupply && now - _resupplyFailTick >= LevyConstants.ResupplyRetryTicks && now - _resupplySearchTick >= LevyConstants.ResupplySearchIntervalTicks)
			{
				_resupplySearchTick = now;
				var armory = SettlementSystem.Instance.NearestArmory(NPC.Center, LevyConstants.ResupplyArmoryRadius);
				if (ArmoryAlive(armory))
				{
					SetState(SoldierState.Resupply);
					if (State == SoldierState.Resupply)
					{
						_resupplyArmory = armory; _resupplyWaitUntil = 0;
						SetGoal(armory.Center);
						return true;
					}
				}
			}

			// No armory in reach (or it had nothing): sidearm if there is one, else carry on without engaging.
			if (Record.Gear.HasSidearm && !UsingSidearm)
			{
				UsingSidearm = true; StatsDirty = true;
				Combat_OnGearChanged();
				NPC.netUpdate = true;
			}
			if (!_outOfAmmoBarked) { _outOfAmmoBarked = true; DialogueSystem.Bark(this, DialogueTrigger.OUT_OF_AMMO, MakeContext()); }
			return false;
		}

		/// <summary>Back on the main weapon after a successful resupply.</summary>
		private void Rearmed()
		{
			_outOfAmmoBarked = false;
			_resupplyFailTick = -100000;
			bool changed = UsingSidearm;
			UsingSidearm = false; StatsDirty = true;
			Combat_OnGearChanged();
			if (changed) NPC.netUpdate = true;
			SetState(OrderState());
		}

		/// <summary>Walking past an armory with a nearly empty pouch: top it up.</summary>
		private void TopUp(int now, WeaponProfile main)
		{
			if (main == null || !main.IsRanged || main.UseAmmo <= 0) return;
			if (now - _topUpTick < LevyConstants.TopUpIntervalTicks) return;
			var pouch = Record.Gear.Ammo;
			int stack = pouch == null || pouch.IsAir ? 0 : pouch.stack;
			if (stack >= LevyConstants.AmmoPouchMax * LevyConstants.TopUpBelowFraction) return;
			_topUpTick = now;
			var armory = SettlementSystem.Instance.NearestArmory(NPC.Center, LevyConstants.TopUpArmoryTiles);
			if (!ArmoryAlive(armory)) return;
			int taken = armory.TakeAmmo(main.UseAmmo, Record.Gear.Ammo, LevyConstants.AmmoPouchMax);
			if (taken > 0) { StatsDirty = true; Combat_OnGearChanged(); }
		}

		// =====================================================================================================
		// 6–10. Order states: ReturnHome, Follow, Haul, Patrol, Idle
		// =====================================================================================================

		private void ThinkOrder(int now, float nearestHostileTiles)
		{
			if (State == SoldierState.Haul) { ThinkHaul(now, nearestHostileTiles); return; }

			// Re-derive the state from the standing order every think so a returned commander, a finished walk home,
			// a dead commander or the end of a fight all flip it. A walk home finishes at the muster point, not at the
			// wider "near home" radius that keeps idlers from re-triggering it.
			SoldierState os = OrderState();
			if (State == SoldierState.ReturnHome && os == SoldierState.Idle && !AtMusterPoint()) os = SoldierState.ReturnHome;
			SoldierState behave;
			if (State == SoldierState.Shaken) behave = os; // the Fear agent's label stays; behave by the order underneath it
			else { if (State != os) SetState(os); behave = State; }

			bool mayHaul = State != SoldierState.Shaken && !Shaken && !Record.HoldFire && !HoldPosition && Record.Order != SoldierOrder.SearchDestroy;
			switch (behave)
			{
				case SoldierState.SearchDestroy: ThinkSearch(now); break;
				case SoldierState.ReturnHome: ThinkReturnHome(now); break;
				case SoldierState.Follow:
					if (mayHaul && TryStartHaul(now, nearestHostileTiles)) return;
					ThinkFollow(now);
					break;
				case SoldierState.Patrol:
					if (mayHaul && IsMilitia && (Home?.Mobilized ?? false) && TryStartHaul(now, nearestHostileTiles)) return;
					ThinkPatrol(now);
					break;
				default:
					if (mayHaul && IsRegular && TryStartHaul(now, nearestHostileTiles)) return;
					ThinkIdle(now);
					break;
			}
		}

		// ---- 6. ReturnHome ----
		private void ThinkReturnHome(int now)
		{
			var home = Home;
			if (home == null)
			{
				if (IsMilitia) { Dismiss(); return; }
				var cmd = Commander;
				SetState(cmd != null && !cmd.dead ? SoldierState.Follow : SoldierState.Idle);
				ClearGoal();
				return;
			}
			if (LevyUtil.WithinTiles(NPC.Center, home.MusterPoint, LevyConstants.HomeArriveTiles))
			{
				ClearGoal();
				if (IsMilitia) { Dismiss(); return; }
				SetState(SoldierState.Idle);
				return;
			}
			SetGoal(home.MusterPoint);
		}

		private void Dismiss()
		{
			// Anything still carried goes into the books/armory first when possible, else on the floor.
			if (_carried.Count > 0)
			{
				if (HaulDestination(out _, out var armory)) Deliver(armory, NPC.Center, (int)Main.GameUpdateCount);
				else DropCarriedItems();
			}
			ClearGoal(); TargetIndex = -1;
			SettlementSystem.Instance.DismissMilitiaBody(this);
		}

		// ---- 7. Follow ----
		private int FollowSlotIndex()
		{
			int idx = 0;
			foreach (var r in RosterSystem.Instance.Regulars)
			{
				if (ReferenceEquals(r, Record) || r.Order != SoldierOrder.Follow || r.Id >= Record.Id) continue;
				if (r.CommanderName == Record.CommanderName) idx++;
			}
			return idx;
		}

		private void ThinkFollow(int now)
		{
			var cmd = Commander;
			if (cmd == null || cmd.dead) { SetState(Home != null && !NearHome() ? SoldierState.ReturnHome : SoldierState.Idle); ClearGoal(); return; }

			float distTiles = LevyUtil.TileDistance(NPC.Center, cmd.Center);
			if (distTiles > LevyConstants.TeleportDistance && now - _lastTeleportTick >= LevyConstants.TeleportCooldownTicks && !VisibleToAnyPlayer())
			{
				CatchUpTeleport(cmd);
				_lastTeleportTick = now;
				return;
			}

			int facing = cmd.direction == 0 ? 1 : cmd.direction;
			int slotIdx = FollowSlotIndex();
			float slotTiles = (slotIdx + 1) * LevyConstants.FollowSlotSpacingTiles, slotSide = -facing;
			if (Record.SpreadOut)
			{
				// wide stance: alternate behind / ahead of the commander, a long stride apart per rank
				slotTiles = Math.Min((slotIdx / 2 + 1) * LevyConstants.SpreadOutFollowSpacingTiles, LevyConstants.SpreadOutFollowMaxTiles);
				slotSide = (slotIdx & 1) == 0 ? -facing : facing;
			}
			Vector2 slot = cmd.Bottom + new Vector2(slotSide * slotTiles * LevyConstants.TilePx, 0f);
			float dSlot = LevyUtil.TileDistance(NPC.Bottom, slot);
			if (HasMoveGoal)
			{
				if (dSlot < LevyConstants.FollowDistance / 3f) ClearGoal();
				else SetGoal(slot);
			}
			else if (dSlot > LevyConstants.FollowDistance / 2f) SetGoal(slot);
		}

		private static void ScreenHalfExtents(out float hx, out float hy)
		{
			int w = Main.screenWidth, h = Main.screenHeight;
			if (LevyUtil.IsServer || w <= 0 || h <= 0) { w = Math.Max(w, LevyConstants.ServerAssumedScreenW); h = Math.Max(h, LevyConstants.ServerAssumedScreenH); }
			hx = w * 0.5f + LevyConstants.TeleportOffscreenMarginPx;
			hy = h * 0.5f + LevyConstants.TeleportOffscreenMarginPx;
		}

		private bool VisibleToAnyPlayer()
		{
			ScreenHalfExtents(out float hx, out float hy);
			Vector2 c = NPC.Center;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var p = Main.player[i];
				if (!p.active) continue;
				if (Math.Abs(c.X - p.Center.X) <= hx && Math.Abs(c.Y - p.Center.Y) <= hy) return true;
			}
			return false;
		}

		private void CatchUpTeleport(Player cmd)
		{
			ScreenHalfExtents(out float hx, out _);
			int facing = cmd.direction == 0 ? 1 : cmd.direction;
			Vector2 behind = cmd.Center + new Vector2(-facing * (hx - LevyConstants.TeleportOffscreenMarginPx + LevyConstants.TeleportBehindExtraPx), 0f);
			Vector2 spot;
			if (!FollowerTravel.FindStandingSpot(behind, LevyConstants.TeleportSpotSearchTiles, out spot)
				&& !FollowerTravel.FindStandingSpot(cmd.Center, LevyConstants.TeleportSpotSearchTiles, out spot))
				spot = cmd.Bottom;
			NPC.Bottom = spot;
			NPC.velocity = Vector2.Zero;
			NPC.netUpdate = true;
			ClearGoal();
			_haulTargetItem = -1;
			Movement_OnTeleported();
		}

		// ---- 8. Haul ----
		private bool HaulDestination(out Vector2 dest, out ArmoryTE armory)
		{
			var home = Home;
			armory = home != null ? SettlementSystem.Instance.LinkedArmory(home) : null;
			if (!ArmoryAlive(armory)) armory = null;
			if (armory != null) { dest = armory.Center; return true; }
			if (home != null) { dest = home.Center; return true; }
			dest = default; return false;
		}

		private bool IsRefusedType(int type, int now)
		{
			for (int i = 0; i < _haulRefusedType.Length; i++) if (_haulRefusedType[i] == type && now - _haulRefusedTick[i] < LevyConstants.HaulRefusedCooldownTicks) return true;
			return false;
		}

		private void NoteRefused(int type, int now)
		{
			int slot = 0, oldest = int.MaxValue;
			for (int i = 0; i < _haulRefusedType.Length; i++)
			{
				if (_haulRefusedType[i] == type) { _haulRefusedTick[i] = now; return; }
				if (_haulRefusedTick[i] < oldest) { oldest = _haulRefusedTick[i]; slot = i; }
			}
			_haulRefusedType[slot] = type; _haulRefusedTick[slot] = now;
		}

		private static bool IsPickupItemType(int type)
		{
			if (type <= 0 || type >= ItemID.Sets.IsAPickup.Length) return true;
			if (ItemID.Sets.IsAPickup[type] || ItemID.Sets.NebulaPickup[type] || ItemID.Sets.BossBag[type]) return true;
			switch (type)
			{
				case ItemID.Heart: case ItemID.CandyApple: case ItemID.CandyCane:
				case ItemID.Star: case ItemID.SoulCake: case ItemID.SugarPlum:
					return true;
			}
			return false;
		}

		private bool IsHaulable(Item it, bool armoryExists, int now)
		{
			if (it == null || !it.active || it.IsAir || it.stack <= 0) return false;
			if (it.noGrabDelay > 0 || it.beingGrabbed || it.instanced || it.shimmered) return false;
			if (IsPickupItemType(it.type)) return false;
			if (!armoryExists && EconomySystem.CoinValue(it) <= 0) return false; // no armory: only coins have somewhere to go
			if (IsRefusedType(it.type, now)) return false;
			Vector2 c = it.Center;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var p = Main.player[i];
				if (p.active && LevyUtil.WithinTiles(c, p.Center, LevyConstants.HaulPlayerExclusion)) return false;
			}
			return true;
		}

		private int FindHaulItem(bool armoryExists, int now)
		{
			int best = -1; float bestSq = float.MaxValue; Vector2 c = NPC.Center;
			float rSq = LevyConstants.HaulPickupRadius * LevyConstants.HaulPickupRadius * (float)(LevyConstants.TilePx * LevyConstants.TilePx);
			for (int i = 0; i < Main.maxItems; i++)
			{
				var it = Main.item[i];
				if (!it.active || it.IsAir) continue;
				float dsq = Vector2.DistanceSquared(c, it.Center);
				if (dsq > rSq || dsq >= bestSq) continue;
				if (!IsHaulable(it, armoryExists, now)) continue;
				bestSq = dsq; best = i;
			}
			return best;
		}

		private bool TryStartHaul(int now, float nearestHostileTiles)
		{
			if (nearestHostileTiles < LevyConstants.HaulNoEnemyRadius) return false;
			if (_carried.Count > 0) { SetState(SoldierState.Haul); _haulStartedTick = now; return State == SoldierState.Haul; }
			if (now < _haulCooldownUntil) return false;
			if (_thinkCount % LevyConstants.HaulScanEveryThinks != 0) return false;
			if (!HaulDestination(out var dest, out var armory)) return false;
			if (!LevyUtil.WithinTiles(NPC.Center, dest, LevyConstants.HaulMaxTripTiles)) return false;
			int idx = FindHaulItem(armory != null, now);
			if (idx < 0) return false;
			SetState(SoldierState.Haul);
			if (State != SoldierState.Haul) return false;
			_haulTargetItem = idx; _haulStartedTick = now;
			SetGoal(Main.item[idx].Center);
			return true;
		}

		private void ThinkHaul(int now, float nearestHostileTiles)
		{
			bool haveDest = HaulDestination(out var dest, out var armory);
			bool calm = nearestHostileTiles >= LevyConstants.HaulNoEnemyRadius;
			if (now - _haulStartedTick > LevyConstants.HaulGiveUpTicks)
			{
				// Could not reach the item or the drop-off in time: leave everything here and go back to the order.
				DropCarriedItems(); _haulCooldownUntil = now + LevyConstants.HaulRefusedCooldownTicks / 2;
				SetState(OrderState()); ClearGoal(); return;
			}

			// Pick-up phase.
			if (_carried.Count < LevyConstants.HaulCarryMax && _haulTargetItem >= 0)
			{
				var it = _haulTargetItem < Main.maxItems ? Main.item[_haulTargetItem] : null;
				if (it == null || !IsHaulable(it, armory != null, now) || !LevyUtil.WithinTiles(NPC.Center, it.Center, LevyConstants.HaulPickupRadius * 2f)) _haulTargetItem = -1;
				else if (NPC.Hitbox.Intersects(it.Hitbox)) { PickUp(_haulTargetItem); _haulTargetItem = -1; _haulStartedTick = now; }
				else { SetGoal(it.Center); return; }
			}
			if (_carried.Count < LevyConstants.HaulCarryMax && calm && haveDest)
			{
				int next = FindHaulItem(armory != null, now);
				if (next >= 0) { _haulTargetItem = next; SetGoal(Main.item[next].Center); return; }
			}
			if (_carried.Count == 0) { SetState(OrderState()); ClearGoal(); return; }

			// Delivery phase.
			if (!haveDest) { DropCarriedItems(); SetState(OrderState()); ClearGoal(); return; }
			if (LevyUtil.WithinTiles(NPC.Center, dest, LevyConstants.HaulArriveTiles))
			{
				Deliver(armory, dest, now);
				SetState(OrderState()); ClearGoal();
				return;
			}
			SetGoal(dest);
		}

		private void PickUp(int idx)
		{
			var it = Main.item[idx];
			if (!it.active || it.IsAir) return;
			_carried.Add(it.Clone());
			it.active = false;
			it.TurnToAir();
			if (LevyUtil.IsServer) NetMessage.SendData(MessageID.SyncItem, -1, -1, null, idx);
		}

		private void Deliver(ArmoryTE armory, Vector2 dest, int now)
		{
			var home = Home;
			float plunder = SettlementSystem.Instance.BookPlunder ? LevyConstants.PlunderCoinMult : 1f;
			for (int i = 0; i < _carried.Count; i++)
			{
				var it = _carried[i];
				if (it == null || it.IsAir) continue;
				long coins = EconomySystem.CoinValue(it);
				if (coins > 0)
				{
					if (home != null) { EconomySystem.Deposit(home, (long)(coins * plunder)); continue; }
					DropItemAt(it, dest); continue;
				}
				if (armory != null)
				{
					var rem = armory.Deposit(it);
					if (rem == null || rem.IsAir) continue;
					NoteRefused(rem.type, now);
					DropItemAt(rem, dest);
				}
				else DropItemAt(it, dest);
			}
			_carried.Clear();
		}

		private void DropCarried(Vector2 at)
		{
			for (int i = 0; i < _carried.Count; i++) { var it = _carried[i]; if (it != null && !it.IsAir) DropItemAt(it, at); }
			_carried.Clear();
		}

		private void DropItemAt(Item it, Vector2 at)
		{
			if (!LevyUtil.IsServerOrSingle) return;
			Item.NewItem(NPC.GetSource_FromAI(), at, it); // NewItem clones the instance and syncs on a server
		}

		// ---- 9. Patrol ----
		private void ThinkPatrol(int now)
		{
			var a = Record.PatrolA; var b = Record.PatrolB;
			bool hasA = a != Point16.NegativeOne, hasB = b != Point16.NegativeOne;
			if (!hasA && !hasB) { ThinkIdle(now); return; }
			if (!hasA) a = b;
			if (!hasB) b = a;
			if (now < _patrolPauseUntil) { ClearGoal(); return; }
			Vector2 target = PatrolWorld(_patrolLeg == 0 ? a : b);
			bool arrived = Math.Abs(NPC.Center.X - target.X) < LevyConstants.PatrolArriveXTiles * LevyConstants.TilePx
				&& Math.Abs(NPC.Bottom.Y - target.Y) < LevyConstants.PatrolArriveYTiles * LevyConstants.TilePx;
			if (arrived)
			{
				ClearGoal();
				_patrolPauseUntil = now + LevyConstants.PatrolPauseTicks;
				_patrolLeg ^= 1;
				return;
			}
			SetGoal(target);
		}

		// ---- 11. Search & Destroy ----
		/// <summary>
		/// Where the sweep is centred: the home muster point when the soldier stood within the leash of it when the order arrived, otherwise
		/// where the soldier stood (e.g. out with the commander). Fixed for the life of the order.
		/// </summary>
		private Vector2 SearchAnchor()
		{
			if (_searchAnchorSet) return _searchAnchor;
			var home = Home;
			_searchAnchor = home != null && LevyUtil.WithinTiles(NPC.Center, home.MusterPoint, LevyConstants.SearchLeashTiles) ? home.MusterPoint : NPC.Center;
			_searchAnchorSet = true;
			return _searchAnchor;
		}

		/// <summary>
		/// Search &amp; destroy with nothing to shoot: sweep outward from the anchor in legs of SearchStepTiles, pausing at each waypoint, turning
		/// round at the leash edge (SearchLeashTiles), the world margin or a dead end. Targets are found by TryEngage first (longer acquire
		/// range), so every standoff / retreat / perch / aerial rule applies as for any fight; this only decides where to look next, and never
		/// advances a ranged soldier toward a hostile it could not engage while that hostile is inside the standoff plus SearchHaltMarginTiles.
		/// Holding fire or a dry weapon makes the soldier stand its ground instead of roaming.
		/// </summary>
		private void ThinkSearch(int now)
		{
			if (!CanFight(now)) { ClearGoal(); return; } // holding fire / nothing to shoot with: stand here rather than roam
			Vector2 anchor = SearchAnchor();
			float tilePx = LevyConstants.TilePx;
			bool melee = Weapon.IsMelee;
			float standoff = melee ? 0f : StandoffTiles(Weapon.RangeTiles);

			// a hostile we are not engaging but that is close: never walk at it; back off when it is inside the standoff
			bool haveThreat = _threatIdx >= 0 && !melee;
			float threatSide = haveThreat ? Math.Sign(Main.npc[_threatIdx].Center.X - NPC.Center.X) : 0f;
			if (haveThreat && _threatTiles < standoff)
			{
				var step = new Vector2(NPC.Center.X + AwayDir(Main.npc[_threatIdx].Center) * LevyConstants.KiteStepAwayTiles * tilePx, NPC.Center.Y);
				ApplyChaseClamp(ref step);
				SetGoal(step);
				_searchGoal = default; _searchGoalTick = 0;
				return;
			}

			bool onWaypoint = HasMoveGoal && MoveGoal == _searchGoal && _searchGoalTick != 0;
			if (onWaypoint)
			{
				bool arrived = Math.Abs(NPC.Center.X - _searchGoal.X) < LevyConstants.SearchArriveXTiles * tilePx;
				bool blocked = _blockedHard && _path.Count == 0 && _pathFailTimer > 0;
				bool headingIntoThreat = haveThreat && _threatTiles < standoff + LevyConstants.SearchHaltMarginTiles && Math.Sign(_searchGoal.X - NPC.Center.X) == threatSide;
				if (headingIntoThreat) { ClearGoal(); _searchGoal = default; _searchGoalTick = 0; _searchPauseUntil = now + LevyConstants.SearchPauseTicks; return; }
				if (arrived) { ClearGoal(); _searchGoal = default; _searchGoalTick = 0; _searchPauseUntil = now + LevyConstants.SearchPauseTicks; return; }
				if (blocked || now - _searchGoalTick > LevyConstants.SearchGoalTimeoutTicks) { SearchTurnAround(now); return; }
				return; // still walking to the waypoint
			}
			if (now < _searchPauseUntil) { if (HasMoveGoal && MoveGoal != _searchGoal) ClearGoal(); return; }

			// pick the next waypoint
			if (_searchDir == 0)
			{
				float off = NPC.Center.X - anchor.X;
				_searchDir = Math.Abs(off) > tilePx * 2f ? Math.Sign(off) : (SlotFrac(SlotSaltSearch) < 0.5f ? -1 : 1);
			}
			if (haveThreat && _threatTiles < standoff + LevyConstants.SearchHaltMarginTiles && _searchDir == (int)threatSide) { SearchTurnAround(now); return; }

			float leftEdge = Math.Max(anchor.X - LevyConstants.SearchLeashTiles * tilePx, LevyConstants.SearchWorldMarginTiles * tilePx);
			float rightEdge = Math.Min(anchor.X + LevyConstants.SearchLeashTiles * tilePx, (Main.maxTilesX - LevyConstants.SearchWorldMarginTiles) * tilePx);
			float gx = NPC.Center.X + _searchDir * LevyConstants.SearchStepTiles * tilePx
				+ SlotOffsetTiles(SlotSaltSearch, LevyConstants.SearchSlotSpreadTiles * SpreadMult) * tilePx;
			gx = MathHelper.Clamp(gx, leftEdge, rightEdge);
			if (Math.Abs(gx - NPC.Center.X) < LevyConstants.SearchMinStepTiles * tilePx) { SearchTurnAround(now); return; } // leash edge or world margin
			if (!TileNav.NearestStandable((int)(gx / tilePx), (int)(NPC.Bottom.Y / tilePx) - 1, LevyConstants.SearchSpotRadiusTiles, out Point tile)) { SearchTurnAround(now); return; }
			Vector2 bottom = TileNav.BottomCentre(tile.X, tile.Y);
			if (Vector2.DistanceSquared(bottom, anchor) > LevyConstants.SearchLeashTiles * LevyConstants.SearchLeashTiles * tilePx * tilePx) { SearchTurnAround(now); return; }
			if (!Pathfinder.FindPath(NPC.Bottom, bottom, Abilities, _probePath)) { SearchTurnAround(now); return; }
			_searchGoal = bottom; _searchGoalTick = now;
			SetGoal(bottom);
		}

		/// <summary>Reverses the sweep heading and rests a moment (leash edge, blocked route or an enemy ahead).</summary>
		private void SearchTurnAround(int now)
		{
			_searchDir = _searchDir == 0 ? 1 : -_searchDir;
			_searchGoal = default; _searchGoalTick = 0;
			_searchPauseUntil = now + LevyConstants.SearchTurnPauseTicks;
			ClearGoal();
		}

		// ---- 10. Idle ----
		/// <summary>
		/// Spread Out while idle or holding: when friends crowd this soldier, step away from them (up to the scaled nudge length) so the
		/// line is wide even before a fight. Stays within the wander radius plus SpreadOutIdleExtraTiles of its anchor. Returns true if it moved.
		/// </summary>
		private bool TrySpreadIdle(int now, Vector2 anchor, float radiusTiles)
		{
			if (_crowdCount == 0 || Math.Abs(_crowdAway) < 0.01f || now < _nudgeNextTick) return false;
			int dir = _crowdAway > 0f ? 1 : -1;
			if (!FindFlatStep(dir, DisperseSteps(), out Vector2 dest)) return false;
			float limit = (radiusTiles + LevyConstants.SpreadOutIdleExtraTiles) * LevyConstants.TilePx;
			dest.X = MathHelper.Clamp(dest.X, anchor.X - limit, anchor.X + limit);
			if (Math.Abs(dest.X - NPC.Center.X) < LevyConstants.TilePx) return false;
			_nudgeNextTick = now + LevyConstants.DispersionNudgeCooldownTicks;
			_idleNextDecisionTick = Math.Max(_idleNextDecisionTick, now + LevyConstants.DispersionNudgeCooldownTicks);
			SetGoal(dest);
			return true;
		}

		private void ThinkIdle(int now)
		{
			Vector2 anchor; float radius;
			if (HoldPosition) { anchor = Record.HoldPos; radius = LevyConstants.HoldIdleWanderTiles; }
			else
			{
				var home = Home;
				if (home != null) { anchor = home.MusterPoint; radius = LevyConstants.IdleWanderTiles; }
				else
				{
					if (!_idleAnchorSet) { _idleAnchor = NPC.Center; _idleAnchorSet = true; }
					anchor = _idleAnchor; radius = LevyConstants.IdleWanderTiles;
				}
			}
			if (Record.SpreadOut && TrySpreadIdle(now, anchor, radius)) return;
			if (now < _idleNextDecisionTick)
			{
				if (HasMoveGoal && Math.Abs(NPC.Center.X - MoveGoal.X) < LevyConstants.TilePx) ClearGoal();
				return;
			}
			_idleNextDecisionTick = now + Main.rand.Next(LevyConstants.IdleDecisionMinTicks, LevyConstants.IdleDecisionMaxTicks + 1);
			// Like town NPCs: half the time stand (and let Movement face whichever way), half the time stroll a few tiles.
			if (Main.rand.NextBool()) { ClearGoal(); return; }
			float dx = Main.rand.NextFloat(-radius, radius) * LevyConstants.TilePx;
			SetGoal(new Vector2(anchor.X + dx, anchor.Y));
		}
	}

	/// <summary>
	/// Death hook for the brain: a soldier that dies mid-haul drops what it carried. Lives on a GlobalNPC so the core's
	/// PreKill=false (which skips OnKill) and other agents' overrides are untouched. HitEffect runs after life is reduced and
	/// before checkDead, on server and clients; the drop itself is server-only.
	/// </summary>
	public class SoldierBrainDeathDrop : GlobalNPC
	{
		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<SoldierNPC>();

		public override void HitEffect(NPC npc, NPC.HitInfo hit)
		{
			if (npc.life > 0 || !LevyUtil.IsServerOrSingle) return;
			if (npc.ModNPC is SoldierNPC s) s.DropCarriedItems();
		}
	}
}
