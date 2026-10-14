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
	/// Shaken and Routed are owned by the Fear agent: the brain never writes State while either is set. Routed walks home;
	/// Shaken engages like Engage (Combat applies the penalties) and never hauls.
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
		private WeaponProfile _mainProfile;
		private int _mainProfileType = -1;
		private int _mainProfilePrefix = -1;

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
			}
			_lastSeenState = State;
		}

		partial void Brain_Think()
		{
			if (Record == null || !NPC.active) return;
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
					ThinkRouted(nearestHostileTiles);
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
		}

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
			float fleeScanSq = LevyConstants.FleeScanTiles * LevyConstants.FleeScanTiles * (float)(LevyConstants.TilePx * LevyConstants.TilePx);
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || i == NPC.whoAmI) continue;
				float dsq = Vector2.DistanceSquared(c, n.Center);
				if (dsq < fleeScanSq && dsq < fleeBest && SoldierTargeting.IsFleeTarget(n)) { fleeBest = dsq; fleeIdx = i; }
				if (dsq < hostileBest && LevyUtil.IsHostileTarget(n)) hostileBest = dsq;
			}
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
			if (w == null || w.IsAir) { _mainProfile = null; _mainProfileType = -1; return null; }
			if (_mainProfile == null || _mainProfileType != w.type || _mainProfilePrefix != w.prefix)
			{
				_mainProfile = WeaponClassifier.GetProfile(w); _mainProfileType = w.type; _mainProfilePrefix = w.prefix;
			}
			return _mainProfile;
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

		private void ThinkRouted(float nearestHostileTiles)
		{
			TargetIndex = -1;
			_haulTargetItem = -1;
			var home = Home;
			if (home != null)
			{
				if (LevyUtil.WithinTiles(NPC.Center, home.MusterPoint, LevyConstants.HomeArriveTiles)) ClearGoal();
				else SetGoal(home.MusterPoint);
				return;
			}
			// Homeless: put distance between us and the nearest hostile, else stand.
			if (nearestHostileTiles < LevyConstants.FleeScanTiles)
			{
				int idx = NearestHostileIndex();
				if (idx >= 0) { RunAwayFrom(Main.npc[idx].Center); return; }
			}
			ClearGoal();
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
			_haulTargetItem = -1;
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
			return true;
		}

		private bool TryEngage(int now)
		{
			if (!CanFight(now)) { TargetIndex = -1; return false; }
			float range = AcquireRangeTiles();
			NPC target = null;
			if (TargetStillValid(TargetIndex, range)) target = Main.npc[TargetIndex];
			else
			{
				TargetIndex = -1;
				int pick = SoldierTargeting.PickTarget(this, range);
				if (ValidNpc(pick) && Main.npc[pick].life > 0 && !SoldierTargeting.IsNeverTarget(Main.npc[pick]) && WithinChaseBounds(Main.npc[pick], range))
				{
					TargetIndex = pick; target = Main.npc[pick];
				}
			}
			if (target == null) return false;

			if (State != SoldierState.Engage && State != SoldierState.Shaken)
			{
				SetState(SoldierState.Engage);
				if (now - _lastEngageBarkTick >= LevyConstants.EngageBarkCooldownTicks) { _lastEngageBarkTick = now; DialogueSystem.Bark(this, DialogueTrigger.ENGAGE, MakeContext()); }
			}

			if (Weapon.IsMelee) MeleeGoal(target, now); else RangedGoal(target);
			return true;
		}

		private void RangedGoal(NPC target)
		{
			float d = LevyUtil.TileDistance(NPC.Center, target.Center);
			Vector2 away = NPC.Center - target.Center;
			if (away.LengthSquared() < 1f) away = new Vector2(-(NPC.direction == 0 ? 1 : NPC.direction), 0f);
			away.Normalize();
			Vector2 goal;
			if (d < LevyConstants.KiteMin)
			{
				float sx = away.X != 0f ? Math.Sign(away.X) : -(NPC.direction == 0 ? 1 : NPC.direction);
				goal = new Vector2(NPC.Center.X + sx * LevyConstants.KiteStepAwayTiles * LevyConstants.TilePx, NPC.Center.Y);
			}
			else if (d > LevyConstants.KiteMax || !Aiming.HasLineOfSight(NPC.Center, target.Center))
			{
				goal = target.Center + away * LevyConstants.KiteMax * LevyConstants.TilePx;
			}
			else
			{
				// In the pocket: a small lateral shuffle every other think, otherwise keep what we had.
				if ((_thinkCount & 1) != 0) return;
				float side = ((_thinkCount >> 1) & 1) == 0 ? 1f : -1f;
				goal = new Vector2(NPC.Center.X + side * LevyConstants.StrafeTiles * LevyConstants.TilePx, NPC.Center.Y);
			}
			ApplyChaseClamp(ref goal);
			SetGoal(goal);
		}

		private void MeleeGoal(NPC target, int now)
		{
			Vector2 goal;
			if (now - LastMeleeHitTick < LevyConstants.MeleeStepBackWindowTicks)
			{
				float sx = NPC.Center.X >= target.Center.X ? 1f : -1f;
				goal = new Vector2(NPC.Center.X + sx * LevyConstants.MeleeStepBackPixels * 2, NPC.Center.Y);
			}
			else if (!LevyUtil.WithinTiles(NPC.Center, target.Center, LevyConstants.RangeMelee))
			{
				goal = target.Center;
			}
			else { ClearGoal(); return; }
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

			bool mayHaul = State != SoldierState.Shaken && !Shaken && !Record.HoldFire && !HoldPosition;
			switch (behave)
			{
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
			Vector2 slot = cmd.Bottom + new Vector2(-facing * (FollowSlotIndex() + 1) * LevyConstants.FollowSlotSpacingTiles * LevyConstants.TilePx, 0f);
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

		// ---- 10. Idle ----
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
