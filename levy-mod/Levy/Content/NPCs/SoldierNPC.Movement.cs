using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Targeting;
using Levy.Common.Util;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Movement reflexes, run every tick after the brain (server/single player): walk and run toward MoveGoal, step up,
	/// jump obstacles, drop through platforms, open and close doors, refuse lava, deep water and unrecoverable drops,
	/// recover when stuck, follow a coarse path when a direct walk is blocked, and fly or glide with wings.
	/// Clients only mirror the velocity into the dummy for leg animation. OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- movement state ----
		private int _wingTime; private bool _flying; private int _jumpsUsed; private bool _stepInit;
		private int _flyRampTicks; private float _flyPeakMult = 1f; // hold-to-accelerate: ticks of continuous flight, and the wings' peak speed multiple
		private int _fallThroughTicks;
		private int _doorBusy; // ticks left of the short no-dash / no-hop spell after this body opened a door (the door custodian shuts it, DoorCustodian.cs)
		private Vector2 _stuckPos; private int _stuckSampleTimer, _stuckTicks, _stuckTotal, _reverseTicks, _sinceReverse = 100000; private bool _stuckJumped;
		private readonly List<Vector2> _path = new();
		private int _pathTimer, _pathFailTimer; private Vector2 _pathGoal;
		private bool _blocked, _blockedHard;
		private float _rhBest = float.MaxValue; private int _rhNoProgress;
		private int _hopTimer;
		// goal-jump debounce and wedge detector (see TickGoalJumpDebounce / TickWedge)
		private int _goalJumpStreak, _goalJumpHold, _goalJumpIdle; private float _goalJumpBaseY;
		private Vector2 _wedgeAnchor; private int _wedgeTimer, _wedgeWindows; private bool _wedgeShuffled, _wedgeForcePath;
		private int _dodgeTicks, _dodgeCooldown; private float _dodgeVx;
		private int _dashTicks, _dashCooldown, _dashDir;
		// ---- aerial state (wings / extra jumps / rocket boots; see UpdateAerial) ----
		private MovementAbilities _kitRef; private int _kitRocketMax; private bool _kitAny;
		private int _rocketTime; private float _drainAcc;
		private bool _aerial, _aerialClimbed, _aerialSpent, _wasAirborne, _usedRocket;
		private int _aerialCooldown, _airJumpCooldown, _aerialVisualTicks, _gapAssist;

		/// <summary>Remaining flight ticks (refilled on the ground).</summary>
		public int WingTime => _wingTime;
		public bool IsFlying => _flying;
		/// <summary>The gear gives this soldier some way to gain altitude (wings, extra jumps or rocket boots). Re-read whenever the abilities object is rebuilt.</summary>
		public bool HasFlightKit { get { RefreshKit(); return _kitAny; } }
		/// <summary>Tiles above the ground a full budget of wings, extra jumps and rocket boots can lift the body (0 without gear).</summary>
		public float AerialMaxReachTiles { get; private set; }
		/// <summary>World Y of the feet the last time the body stood on something (the reference for the altitude cap).</summary>
		public float AerialGroundY { get; private set; } = float.MinValue;
		/// <summary>The body is in aerial mode this tick: climbing to or holding the brain's firing altitude.</summary>
		public bool IsAerial => _aerial;
		/// <summary>The direct walk toward the current goal was refused or obstructed this tick.</summary>
		public bool MoveBlocked => _blocked;
		public bool HasPath => _path.Count > 0;

		/// <summary>Travel helpers call this after moving the body.</summary>
		public void NotifyTeleported() => Movement_OnTeleported();

		partial void Movement_Update()
		{
			Movement_UpdateCore();
			FlightTalkTick();
			AirPost();   // SoldierNPC.AirCtl.cs: lava catch, wing hold / release, telemetry (last word on the air velocity)
		}

		private void Movement_UpdateCore()
		{
			NPC npc = NPC; WhyM = Why.MNone;
			if (!_stepInit) { npc.stepSpeed = StepSpeed; _stepInit = true; }
			TickReturnHomeProgress();
			// on a slope the body keeps a small downward velocity as it walks: with solid ground within a few pixels under the feet and no real fall under way it is standing, not airborne (jumping, dodging and strafing all need that)
			bool onSlope = MobilityMode >= 1 && npc.velocity.Y > 0f && npc.velocity.Y < 2.2f && !npc.wet && Collision.SolidCollision(new Vector2(npc.position.X, npc.Bottom.Y), npc.width, 4);
			bool grounded = (npc.velocity.Y == 0f || onSlope) && !(_wasAirborne && HeadOnCeiling());
			RefreshKit();
			if (npc.lavaWet && !npc.lavaImmune) LavaEscape(); // a Magma Shot body swims it instead (SwimAssist)
			if (grounded)
			{
				_jumpsUsed = 0; _wingTime = Abilities?.WingTimeMax ?? 0; _flying = false; _flyRampTicks = 0;
				_rocketTime = _kitRocketMax; _drainAcc = 0f;
				AerialGroundY = npc.Bottom.Y;
				if (_wasAirborne && _aerialSpent) _aerialCooldown = Math.Max(_aerialCooldown, AerialRelaunchCooldownTicks);
				_aerialSpent = false; _aerialClimbed = false;
			}
			else if (AerialGroundY == float.MinValue) AerialGroundY = npc.Bottom.Y;
			_wasAirborne = !grounded;
			TickGoalJumpDebounce(grounded);
			bool air = UpdateAerial(grounded);
			if (_gapAssist > 0 && !air) TickGapAssist(grounded);

			if (_doorBusy > 0) _doorBusy--;
			NudgeOffPlayers();
			// The brain's drop request (SetGoal: goal well below) is read here, then the flag is rewritten from the armed ticks alone: the request does not drop him by itself,
			// it only stands in for the goal-below test of the platform-drop block below, which still checks the floor and lava under him before arming.
			bool brainDropRequest = WantsToFallThrough && _fallThroughTicks <= 0;
			if (_fallThroughTicks > 0) { _fallThroughTicks--; WantsToFallThrough = _fallThroughTicks > 0; }
			else WantsToFallThrough = false;
			if (_dashCooldown > 0) _dashCooldown--;
			if (_hkPhase == HookPhase.Idle && _rtHookWant) RouteHookTick(grounded);
			if (_hkPhase == HookPhase.Idle && _hkTrigTicks > 0) HookEvaluate(); // a trigger that held long enough becomes a plan and a cast (SoldierNPC.Hook.cs)
			if (_hkPhase == HookPhase.Idle) { FallCatchCheck(); HookFollowCommander(); }
			if (_hkStepUntil != 0 && HookStepBackTick(grounded)) { WhyM = Why.MHook; _dashTicks = 0; MirrorDummyControls(); return; }
			if (_clmOn && TickClimb(grounded)) { WhyM = Why.MClimb; _dashTicks = 0; MirrorDummyControls(); return; } // up a wall face on a planned route: the climb owns the tick
			if (TickDodge(grounded, air)) { _dashTicks = 0; if (air) ApplyAltitude(grounded); MirrorDummyControls(); return; }
			if (_hkPhase != HookPhase.Idle && TickHook(grounded)) { WhyM = Why.MHook; _dashTicks = 0; MirrorDummyControls(); return; } // in flight, reeling or ballistic: the hook owns the tick
			if (_hkAltSeen != 0 && _hkPhase == HookPhase.Idle && Main.GameUpdateCount - _hkAltSeen <= 12u) { _hkGoalNow = new Vector2(_hkAltX, _hkAltY); HookTrigger(HookReason.Altitude); }

			if (!HasMoveGoal)
			{
				WhyM = air || !grounded ? Why.MBusyAir : Why.MNoGoal;
				Decelerate();
				_blocked = false; _blockedHard = false; _flying = false; _flyRampTicks = 0;
				if (_path.Count > 0) _path.Clear();
				ResetStuck();
				if (air) ApplyAltitude(grounded); else Glide(grounded);
				MirrorDummyControls();
				return;
			}

			Vector2 goal = ResolveLocalGoal();
			_hkGoalNow = goal;
			float dx = goal.X - npc.Center.X;
			float dy = goal.Y - npc.Bottom.Y; // > 0: goal below the feet
			if (!TravelLegacy.On) SwimAssist(dy, air);
			if (Math.Abs(dx) < ArriveXPx && (air || Math.Abs(dy) < ArriveYPx)) // aloft the goal's Y is the ground spot below: only X counts
			{
				WhyM = Why.MArrived;
				Decelerate();
				_blocked = false; _blockedHard = false; _flying = false; _flyRampTicks = 0;
				ResetStuck();
				if (air) ApplyAltitude(grounded); else Glide(grounded);
				MirrorDummyControls();
				return;
			}

			if (TickDash(goal, dx, dy, grounded, air)) { MirrorDummyControls(); return; }

			_blocked = false; _blockedHard = false;
			int jumpTiles = 0; bool wantFly = false; bool flyUp = false; bool goalJump = false; FlyWhy = 0;
			bool horizontal = Math.Abs(dx) >= ArriveXPx;
			int dir = dx >= 0f ? 1 : -1;
			bool reversing = _reverseTicks > 0;
			if (reversing) { _reverseTicks--; dir = -dir; }
			float maxSpeed = 0f, accel = 0f;
			if (horizontal)
			{
				int face = FacingWhileMoving(dir);
				npc.direction = face; npc.spriteDirection = face;
				bool close = Math.Abs(_pathFromRoute && !TravelLegacy.On && HasMoveGoal ? MoveGoal.X - npc.Center.X : dx) < WalkWithinTiles * 16 && !Retreating; // a retreat always runs, however short the step; on a planned route the waypoints are close together, the leg end is what counts
				bool walk = reversing || (close && !(PathSearch.Travel6On && _pathFromRoute && RouteFlying && !grounded)) || State == SoldierState.Idle || State == SoldierState.Patrol;   // (a glide to a landing does not slow to a stroll for its last six tiles)
				maxSpeed = walk ? WalkSpeedPx * Abilities.WalkSpeed : Abilities.MaxRunSpeed; // MaxRunSpeed already carries moveSpeed the way Player.Update applies it (not on the sprint)
				if (State == SoldierState.Routed && !walk) maxSpeed *= LevyConstants.RoutedSpeedMult;
				accel = walk ? WalkAccel : RunAccel;
				Point feet = TileNav.FeetTile(npc.Bottom);
				int ax = (int)((npc.Center.X + dir * (npc.width / 2f + LookAheadPx)) / 16f);
				ScanAhead(feet.X, ax, feet.Y, dir, grounded, dy, ref jumpTiles, ref wantFly, ref flyUp);
			}

			// goal above and close: jump to it, fly to it, or admit it is out of reach
			// A planned route asks for the climb itself: the look-ahead refusal of an overhang or a wall (ScanAhead is conservative about a tight pocket under a ledge) does not veto a jump the route's next waypoint calls for.
			bool routeClimb = !TravelLegacy.On && _blocked && _pathFromRoute && _path.Count > 0 && (BlockWhy == 5 || BlockWhy == 6) && !Abilities.HasWings;
			if (!air && (!_blocked || routeClimb) && dy < -JumpUpAbovePx && Math.Abs(dx) <= GoalJumpReachPx())
			{
				if (routeClimb) { _blocked = false; _blockedHard = false; }
				int tiles = (int)Math.Ceiling(-dy / 16f);
				int reachBonus = State == SoldierState.Engage ? EngageJumpReachBonusTiles : 0; // a weapon covers the last stretch
				if (PlatMode >= 1) reachBonus += Abilities.ExtraJumps > 0 ? ExtraJumpReachTiles * Math.Min(2, Abilities.ExtraJumps) : 0; // a second jump at the apex carries the rest
				if (tiles <= Abilities.JumpTiles + reachBonus)
				{
					int jt = Math.Min(tiles, Abilities.JumpTiles);
					if (jt > jumpTiles && GoalJumpAllowed()) { jumpTiles = jt; goalJump = true; }
				}
				else if (Abilities.HasWings) { wantFly = true; FlyWhy = 1; }
				else { _blocked = true; _blockedHard = true; BlockWhy = 1; HookTrigger(HookReason.GoalAbove); }
			}
			// goal well below while standing on a platform: drop through it when the floor below is safe
			bool dropNudged = false;
			if (!air && grounded && (dy > PlatformDropBelowPx || brainDropRequest))
			{
				if (PlatformDropIsSafe()) { _fallThroughTicks = PlatformDropTicks; WantsToFallThrough = true; }
				else if (_dropNudge != 0 && !Collision.SolidCollision(new Vector2(npc.position.X + _dropNudge * 1.5f, npc.position.Y), npc.width, npc.height - 2)) { npc.position.X += _dropNudge * 1.5f; dropNudged = true; }   // off the solid edge, then the next tick drops
			}
			if (Abilities.HasWings && dy < -(Abilities.JumpTiles * 16f)) { wantFly = true; FlyWhy = 2; }
			if (!TravelLegacy.On && _pathFromRoute && Abilities.HasWings && _wingTime > 0 && dy < -(Math.Abs(dx) < 24f && PathSearch.Travel6On ? ArriveYPx : RouteFlyRisePx)) { wantFly = true; FlyWhy = 3; }   // (a waypoint straight overhead, between 'arrived' and the flight threshold, used to leave the body standing under it until the wedge jump) // a planned route climbs by wing (a flight edge), not by hopping at the foot of the climb
			if (!TravelLegacy.On && _pathFromRoute && Abilities.HasWings && RouteFlying) { if (_wingTime > 0) { wantFly = true; flyUp = false; FlyWhy = 4; } else if (!grounded) StatArcFuelOutNote(); } // a planned arc: thrust toward the next air point while the tank lasts, a glide after
			if (air) { jumpTiles = 0; wantFly = false; flyUp = false; } // aerial mode owns the vertical axis

			if (!air && Abilities.HasWings && _wingTime > 0 && (wantFly || (_flying && dy < -FlyArriveYPx)))
			{
				Fly(goal, dir, flyUp);
			}
			else
			{
				_flying = false; _flyRampTicks = 0;
				if (dropNudged) npc.velocity.X = _dropNudge * 0.6f;   // stepping clear of the shaft wall: the walk toward the waypoint's tile centre (which overlaps the wall by a pixel or two) waits one tick
				else if (horizontal)
				{
					if (_blocked) npc.velocity.X *= BlockedDecel;
					else if (MobilityMode >= 1 && LavaAhead(dir)) npc.velocity.X *= 0.5f;   // never run or drift toward lava, on the ground or in the air
					else Accelerate(dir, maxSpeed, accel);
				}
				else Decelerate();
				DbgJump = jumpTiles * 10 + (horizontal ? 1 : 0) + (grounded ? 2 : 0) + (_blocked ? 4 : 0);
				if (jumpTiles > 0)
				{
					bool fromGround = npc.velocity.Y == 0f;
					if (TryJump(jumpTiles) && goalJump && fromGround) NoteGoalJump();
				}
				else if (grounded && horizontal && !_blocked && !air) TryRunningHop(dx, dir);
				StepUp();
				if (air) ApplyAltitude(grounded); else Glide(grounded);
			}

			if (_blocked) { WhyM = Why.MBlocked; }
			TrackStuck();
			UpdatePathRequest();
			MirrorDummyControls();
		}

		/// <summary>
		/// Swimming, as a player does it: in deep water a body with no input only sinks (an NPC has no buoyancy: gravity 0.2, half-speed movement), so a levy that waded into a lake could stand on its floor and never climb out.
		/// Holding jump in water swims up; this does it while the goal is above the feet. A goal level with or below the feet is left to sink to (the lake floor is walkable, and the route may cross it).
		/// </summary>
		private void SwimAssist(float dy, bool air)
		{
			NPC npc = NPC;
			bool lavaSwim = npc.lavaWet && npc.lavaImmune; // lava is water to a body with a Magma Shot in it
			if ((!npc.wet && !lavaSwim) || (npc.lavaWet && !lavaSwim) || air || _flying || _hkPhase != HookPhase.Idle) return;
			if (dy >= -SwimUpDyPx) return;
			float cap = npc.honeyWet ? SwimMaxUp * 0.5f : SwimMaxUp;
			if (npc.velocity.Y > -cap) npc.velocity.Y = Math.Max(-cap, npc.velocity.Y - SwimAccel);
			SwimTicks++;
		}
		internal int SwimTicks;

		/// <summary>
		/// In lava: out, at once. A player in lava holds jump and a direction: this swims up hard (spending a wing flap, a rocket pulse or an extra jump when the body has one) and leans toward the nearer bank
		/// (the side with less lava under the next columns). No planned route is followed in lava; the hook and the walking AI take over again on the bank.
		/// </summary>
		private void LavaEscape()
		{
			NPC npc = NPC;
			LavaEscapeTicks++;
			if (_hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown) return;     // a reel is already pulling it out
			if (AirLavaOn && HookUsable && Main.GameUpdateCount >= _lavaCatchAt)
			{
				// a player in lava casts at the nearest wall and is pulled out
				_lavaCatchAt = Main.GameUpdateCount + 10;
				if (SoldierHooks.TakePlanBudget())
				{
					try { _hkPhase = HookPhase.Idle; if (TryFallHang(new Vector2(npc.Bottom.X, npc.Bottom.Y - 14f * 16f))) { LavaCatches++; StatLavaCatches++; HookLog("lava catch (in lava)"); return; } }
					catch (Exception) { try { HookReset(true); } catch (Exception) { } }
				}
			}
			npc.velocity.Y = Math.Max(-LavaSwimMaxUp, npc.velocity.Y - LavaSwimAccel);
			int dir = LavaFreeSide(TileNav.FeetTile(npc.Bottom));
			if (dir != 0) { npc.velocity.X = MathHelper.Clamp(npc.velocity.X + dir * 0.5f, -3f, 3f); npc.direction = npc.spriteDirection = dir; }
			if (npc.velocity.Y > -1f && !npc.collideY) AirKick(JumpSpeedFor(Math.Min(Abilities.JumpTiles, BaseJumpTiles)) * ExtraJumpSpeedMult);
			npc.netUpdate = true;
		}
		internal static int LavaEscapeTicks;

		partial void Movement_ClientAnimate() => MirrorDummyControls();

		partial void Movement_OnTeleported()
		{
			if (!_hkTeleportApplying) HookReset(false); // a teleport takes the soldier off its hook
			_path.Clear(); _pathTimer = 0; _pathFailTimer = 0;
			ResetStuck(); _stuckTotal = 0;
			_goalJumpStreak = 0; _goalJumpHold = 0; _goalJumpIdle = 0;
			_wingTime = Abilities?.WingTimeMax ?? 0; _flying = false; _flyRampTicks = 0; _jumpsUsed = 0;
			_rocketTime = _kitRocketMax; _aerial = false; _aerialSpent = false; _aerialClimbed = false; _gapAssist = 0; _wasAirborne = false; _drainAcc = 0f;
			_fallThroughTicks = 0; WantsToFallThrough = false; ClimbCancel();
			_doorBusy = 0; _reverseTicks = 0; _blocked = false; _blockedHard = false; _dodgeTicks = 0; _dodgeHold = 0; _dashTicks = 0; _fallStartRow = int.MinValue;
			NPC.velocity = Vector2.Zero;
			_stuckPos = NPC.position;
			_fallImmuneTicks = TeleportFallImmunityTicks; _fallStartRow = int.MinValue; // a teleport never starts a fall that counts
			NPC.netUpdate = true;
		}

		// =====================================================================================================
		// look-ahead
		// =====================================================================================================

		/// <summary>Looks at the column one step ahead: doors, lava, deep water, walls to jump, ledges to refuse.</summary>
		private void ScanAhead(int feetX, int ax, int fy, int dir, bool grounded, float dyPx, ref int jumpTiles, ref bool wantFly, ref bool flyUp)
		{
			// closed door in the way: open it (town-NPC pattern) or stand blocked
			for (int ty = fy; ty >= fy - 2; ty--)
			{
				Tile t = Framing.GetTileSafely(ax, ty);
				if (!TileNav.IsClosedDoor(t)) continue;
				if (!TryOpenDoor(ax, ty, dir)) { _blocked = true; BlockWhy = 2; }
				return;
			}
			// lava in the next two columns, from head height to the floor under the next step
			for (int i = 0; i < 2 && !NPC.lavaImmune; i++)
			{
				int tx = ax + dir * i;
				for (int ty = fy - 2; ty <= fy + 1; ty++)
					if (TileNav.HasLava(tx, ty)) { _blocked = true; _blockedHard = true; BlockWhy = 3; return; }
			}
			// deep water at head height ahead, unless already swimming
			if (!NPC.wet && TileNav.IsDeepWater(ax, fy - 2)) { _blocked = true; _blockedHard = true; BlockWhy = 4; return; }

			// wall ahead: count solid tiles upward from feet level
			int h = 0;
			while (h < MaxJumpTiles + 3 && TileNav.IsSolid(ax, fy - h)) h++;
			if (h == 0 && (TileNav.IsSolid(ax, fy - 1) || TileNav.IsSolid(ax, fy - 2)))
			{
				// an overhang at chest or head height: the body cannot duck under it and cannot jump onto it
				_blocked = true; _blockedHard = true; BlockWhy = 5;
				if (Abilities.HasWings) { wantFly = true; flyUp = true; FlyWhy = 5; } else HookTrigger(HookReason.Wall);
				return;
			}
			if (h >= 2)
			{
				bool clearAbove = !TileNav.IsSolid(ax, fy - h) && !TileNav.IsSolid(ax, fy - h - 1) && !TileNav.IsSolid(ax, fy - h - 2);
				bool headroom = true;
				for (int k = 1; k <= h && headroom; k++) if (TileNav.IsSolid(feetX, fy - 2 - k)) headroom = false;
				if (h <= Abilities.JumpTiles && clearAbove && headroom) { if (h > jumpTiles) jumpTiles = h; }
				else { _blocked = true; _blockedHard = true; BlockWhy = 6; if (Abilities.HasWings) { wantFly = true; flyUp = true; FlyWhy = 6; } else HookTrigger(HookReason.Wall); }
				return;
			}
			if (h == 1) return; // a single step: Collision.StepUp carries the body over it

			// ledge ahead: refuse bottomless drops, lava below, and drops too deep to climb back (wings may fly down)
			if (grounded && !TileNav.IsFloor(ax, fy + 1))
			{
				int depth = TileNav.DropDepth(ax, fy);
				if (depth < 0 && !TravelLegacy.On && _pathFromRoute && !Abilities.HasWings && TileNav.IsFloor(ax, fy + FallScanTiles + 1) && !TileNav.HasLava(ax, fy + FallScanTiles + 1)) depth = FallScanTiles;   // the route search takes a fall of exactly FallScanTiles; the scan above stops one row short
				if (depth < 0 && PathSearch.Travel8On && _pathFromRoute && !Abilities.HasWings && _path.Count > 0 && _path[0].Y - NPC.Bottom.Y > 3 * 16f && Math.Abs((int)(_path[0].X / 16f) - ax) <= 2) depth = FallScanTiles;   // the route's next waypoint is down the column ahead, further than the scan above reaches: a long fall (a shaft) whose far floor the route search checked
				// a narrow gap with floor on the far side: jump it rather than drop into a pit that cannot be climbed back out of
				if ((depth < 0 || depth >= GapJumpMinDepthTiles) && TryGapJump(ax, fy, dir, dyPx, ref jumpTiles)) return;
				if (depth < 0)
				{
					if (!TravelLegacy.On && Abilities.HasWings && _wingTime > 0) { wantFly = true; FlyWhy = 7; return; } // a gap wider than a jump: wings fly it
					_blocked = true; _blockedHard = true; BlockWhy = 7; if (dyPx <= 2f * 16f) HookTrigger(HookReason.Gap); return; // a hook may span it
				}
				// a hole too deep to climb back out of (deeper than a jump) is not stepped into when the goal is level or above and no planned route asks for it: that is how a body ends up trapped in a pit
				if (!TravelLegacy.On && depth > Abilities.JumpTiles && !Abilities.HasWings && dyPx <= 2f * 16f && !_pathFromRoute) { _blocked = true; _blockedHard = true; BlockWhy = 8; HookTrigger(HookReason.Gap); return; }
				if (depth > Abilities.JumpTiles + DropMarginTiles && !(!TravelLegacy.On && _pathFromRoute && !Abilities.HasWings && depth <= FallScanTiles)) // a planned route may go down a safe fall (the far side was checked for a way out)
				{
					if (Abilities.HasWings) { wantFly = true; FlyWhy = 8; }
					else { _blocked = true; _blockedHard = true; BlockWhy = 8; if (dyPx <= 2f * 16f) HookTrigger(HookReason.Gap); }
				}
			}
		}

		/// <summary>
		/// The column ahead has no floor. If solid floor resumes within <see cref="GapJumpMaxTiles"/> columns on the same level (clear
		/// headroom over the gap and the landing), ask for a jump. Not when the goal is well below: then dropping is the point.
		/// </summary>
		private bool TryGapJump(int ax, int fy, int dir, float dyPx, ref int jumpTiles)
		{
			if (dyPx > 2f * 16f) return false;
			int maxW = TravelLegacy.On ? GapJumpMaxTiles + (Abilities.ExtraJumps > 0 ? AirGapBonusTiles : 0) : Abilities.GapTiles; // run speed and an extra jump mid-air carry a wider gap
			for (int w = 0; w <= maxW; w++)
			{
				int cx = ax + dir * w;
				for (int r = 0; r <= GapJumpClearRows; r++) if (TileNav.IsSolid(cx, fy - r)) return false;
				if (TileNav.HasLava(cx, fy) || TileNav.HasLava(cx, fy - 1)) return false;
				if (!TileNav.IsFloor(cx, fy + 1)) continue;
				if (w == 0) return false;
				if (TileNav.HasLava(cx, fy + 1)) return false;
				int tiles = Math.Min(Abilities.JumpTiles, GapJumpTiles);
				if (tiles < 2) return false;
				if (tiles > jumpTiles) jumpTiles = tiles;
				if (Abilities.ExtraJumps > 0 || Abilities.HasWings || _kitRocketMax > 0) _gapAssist = GapAssistTicks;
				return true;
			}
			return false;
		}

		// =====================================================================================================
		// facing / retreat / dodging
		// =====================================================================================================

		/// <summary>A body backing away (fear rout, or a ranged soldier stepping out of its standoff).</summary>
		private bool Retreating => State == SoldierState.Routed
			|| (_engMode == EngageMode.Retreat && (State == SoldierState.Engage || State == SoldierState.Shaken));

		/// <summary>Fighting bodies face the thing they are shooting while they walk, so a backpedal reads as a backpedal.</summary>
		private int FacingWhileMoving(int walkDir)
		{
			if (SwingFacingLocked) return NPC.direction != 0 ? NPC.direction : walkDir; // a swing in flight keeps the side it was thrown on
			if ((State == SoldierState.Engage || State == SoldierState.Shaken || State == SoldierState.Routed) && TargetIndex >= 0 && TargetIndex < Main.maxNPCs)
			{
				NPC t = Main.npc[TargetIndex];
				float tx = t.Center.X - NPC.Center.X;
				if (t.active && Math.Abs(tx) > 2f) return tx < 0f ? -1 : 1;
			}
			return walkDir;
		}

		/// <summary>
		/// Flinches out of the nearest incoming threat: a hostile projectile, or the body of a fast, hard-hitting hostile NPC (worm segment,
		/// wyvern, charger) whose padded hitbox will overlap ours. A short sidestep out of the lane (or a hop when the lane is flat or the
		/// sidestep is blocked), then a cooldown and straight back to the fight. Rate-limited. Returns true while a sidestep is in progress
		/// (the walk logic is skipped for those few ticks).
		/// </summary>
		private bool TickDodgeLegacy(bool grounded, bool air)
		{
			NPC npc = NPC;
			if (_dodgeTicks > 0)
			{
				_dodgeTicks--;
				npc.velocity.X = _dodgeVx;
				return true;
			}
			if (_dodgeCooldown > 0) { _dodgeCooldown--; return false; }
			if ((!grounded && !air) || _flying || npc.wet) return false; // aloft in aerial mode a dodge is allowed too
			if ((int)((Main.GameUpdateCount + (uint)npc.whoAmI) % (uint)DodgeScanIntervalTicks) != 0) return false;

			Projectile worst = null; NPC worstNpc = null; float bestT = DodgeLookaheadTicks; Vector2 worstClosest = default;
			float rSq = DodgeRadiusTiles * 16f * DodgeRadiusTiles * 16f;
			float hit = DodgeHitRadiusPx + npc.width * 0.5f;
			Vector2 c = npc.Center;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !IsThreatShot(p) || p.damage < DodgeMinDamage) continue; // a levy steps out of hostile shots, a hostile body out of the player's side's (HOSTILE SEAM)
				Vector2 rel = c - p.Center;
				if (rel.LengthSquared() > rSq) continue;
				Vector2 v = p.velocity; float v2 = v.LengthSquared();
				if (v2 < DodgeMinProjectileSpeed * DodgeMinProjectileSpeed) continue;
				float t = Vector2.Dot(rel, v) / v2; // ticks until the closest approach
				if (t < 2f || t > bestT) continue;
				Vector2 closest = p.Center + v * t;
				if (Vector2.Distance(closest, c) > hit) continue;
				bestT = t; worst = p; worstClosest = closest;
			}

			// Enemy BODIES: only the fast, hard-hitting ones on a collision course (a slow melee enemy being traded blows with never qualifies).
			// A worm is many segment NPCs, so this naturally catches the segment about to hit.
			float npcRSq = DodgeNpcRadiusTiles * 16f * DodgeNpcRadiusTiles * 16f;
			float minSpeedSq = DodgeNpcMinSpeed * DodgeNpcMinSpeed;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.whoAmI == npc.whoAmI || n.damage < DodgeNpcMinDamage) continue;
				Vector2 rel = c - n.Center;
				if (rel.LengthSquared() > npcRSq) continue;
				Vector2 v = n.velocity; float v2 = v.LengthSquared();
				if (v2 < minSpeedSq) continue;
				if (!LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				float t = Vector2.Dot(rel, v) / v2; // ticks until the closest approach
				if (t < 2f || t >= bestT || t > DodgeNpcLookaheadTicks) continue;
				Vector2 closest = n.Center + v * t; // where the NPC's centre will be when it is nearest to us
				// padded hitbox overlap at that moment (the NPC's real width/height: worm segments and wyverns are big)
				if (Math.Abs(closest.X - c.X) > n.width * 0.5f + npc.width * 0.5f + DodgeNpcMarginPx) continue;
				if (Math.Abs(closest.Y - c.Y) > n.height * 0.5f + npc.height * 0.5f + DodgeNpcMarginPx) continue;
				bestT = t; worstNpc = n; worst = null; worstClosest = closest;
			}
			if (worst == null && worstNpc == null) return false;

			_dodgeCooldown = DodgeCooldownTicks;
			if (worstNpc != null)
			{
				// Step out of the body's lane: the side of its travel line that we are already on (a flat charge has no sideways lane, so hop).
				Vector2 nv = worstNpc.velocity;
				Vector2 nrm = new Vector2(-nv.Y, nv.X) / nv.Length();
				float lateral = Vector2.Dot(c - worstNpc.Center, nrm);
				if (Math.Abs(nrm.X) >= DodgeNpcMinLaneX)
				{
					float offX = nrm.X * lateral; // which way our offset from the travel line points, horizontally
					int side = offX >= 0f ? 1 : -1;
					if (Math.Abs(lateral) < 4f) side = HasMoveGoal ? (MoveGoal.X >= c.X ? 1 : -1) : (NPC.direction == 0 ? 1 : -NPC.direction);
					for (int attempt = 0; attempt < 2; attempt++, side = -side)
					{
						if (!(grounded ? SidestepSafe(side) : SidestepClearAir(side))) continue;
						_dodgeVx = side * DodgeSidestepSpeedPx; _dodgeTicks = DodgeSidestepTicks;
						npc.velocity.X = _dodgeVx; npc.netUpdate = true;
						return true;
					}
				}
			}
			else
			{
				Vector2 pv = worst.velocity;
				bool flat = Math.Abs(pv.X) > Math.Abs(pv.Y) * 1.5f;
				if (!flat)
				{
					// steep shot: step out of its column, preferably the way we are already heading
					float off = c.X - worstClosest.X;
					int side = off >= 0f ? 1 : -1;
					if (Math.Abs(off) < 4f) side = HasMoveGoal ? (MoveGoal.X >= c.X ? 1 : -1) : (NPC.direction == 0 ? 1 : -NPC.direction);
					for (int attempt = 0; attempt < 2; attempt++, side = -side)
					{
						if (!(grounded ? SidestepSafe(side) : SidestepClearAir(side))) continue;
						_dodgeVx = side * DodgeSidestepSpeedPx; _dodgeTicks = DodgeSidestepTicks;
						npc.velocity.X = _dodgeVx; npc.netUpdate = true;
						return true;
					}
				}
			}
			if (!grounded) { if (AirBlip()) npc.netUpdate = true; return false; } // aloft: no room to sidestep, so flap or kick off the air
			// a flighted soldier's hop is bigger (the extra jump and boots are there to be used)
			if (TryJump(DodgeHopTiles + (_kitAny ? FlightDodgeHopBonusTiles : 0))) npc.netUpdate = true;
			return false;
		}

		// =====================================================================================================
		// dashing
		// =====================================================================================================

		/// <summary>
		/// A dash accessory (Player.dashType) lends a short burst of speed to a walk the soldier is ALREADY making: toward a goal (or waypoint)
		/// that is far across and level, down a flat, open, floored lane, while it is advancing (never retreating, routed or shaken). It
		/// never chooses a destination, so it cannot pull a soldier off its line. Rate-limited by a cooldown with jitter. Returns true while a
		/// burst is in progress (the walk logic is skipped for those few ticks).
		/// </summary>
		private bool TickDash(Vector2 goal, float dx, float dy, bool grounded, bool air)
		{
			NPC npc = NPC;
			if (_dashTicks > 0)
			{
				// a burst ends early if the body left the ground, got wet, or hit something (the speed collapsed)
				bool hit = DashBurstTicks - _dashTicks >= 2 && Math.Abs(npc.velocity.X) < DashSpeedPx * 0.4f;
				if (!grounded || air || npc.wet || hit) { _dashTicks = 0; npc.netUpdate = true; return false; }
				_dashTicks--;
				npc.velocity.X = _dashDir * DashSpeedPx;
				if (_dashTicks == 0) npc.netUpdate = true;
				return true;
			}
			if (_dashCooldown > 0 || Abilities == null || !Abilities.HasDash || !grounded || air || npc.wet || _doorBusy > 0) return false;
			if (!(State == SoldierState.Follow || State == SoldierState.SearchDestroy || State == SoldierState.FreeRein
				|| (State == SoldierState.Engage && _engMode == EngageMode.Approach))) return false;
			if (Retreating) return false;
			float adx = Math.Abs(dx);
			if (adx < DashMinGoalTiles * 16f || adx > DashMaxGoalTiles * 16f || Math.Abs(dy) > DashMaxRiseTiles * 16f) return false;
			int dir = dx >= 0f ? 1 : -1;
			if (Math.Abs(npc.velocity.X) > 0.5f && Math.Sign(npc.velocity.X) == -dir) return false; // mid-turn: wait for the body to face the goal
			if (!DashLaneClear(dir)) return false;
			_dashDir = dir; _dashTicks = DashBurstTicks;
			_dashCooldown = DashCooldownTicks + Main.rand.Next(DashCooldownJitterTicks + 1);
			npc.direction = dir; npc.spriteDirection = dir;
			npc.velocity.X = dir * DashSpeedPx;
			npc.netUpdate = true;
			return true;
		}

		/// <summary>The next <see cref="DashLaneTiles"/> columns are level with the feet, open to head height, floored and free of lava and deep water.</summary>
		private bool DashLaneClear(int dir)
		{
			if (MobilityMode >= 1) return SurfaceLane(dir, DashLaneTiles, true);
			Point feet = TileNav.FeetTile(NPC.Bottom);
			for (int i = 1; i <= DashLaneTiles; i++)
			{
				int x = feet.X + dir * i;
				if (TileNav.IsSolid(x, feet.Y) || TileNav.IsSolid(x, feet.Y - 1) || TileNav.IsSolid(x, feet.Y - 2)) return false;
				if (!TileNav.IsFloor(x, feet.Y + 1) || TileNav.HasLava(x, feet.Y) || TileNav.HasLava(x, feet.Y + 1) || TileNav.IsDeepWater(x, feet.Y - 1)) return false;
				if (TileNav.IsClosedDoor(Framing.GetTileSafely(x, feet.Y))) return false;
			}
			return true;
		}

		/// <summary>Room to sidestep a couple of tiles: no wall, no lava, floor under every column.</summary>
		private bool SidestepSafe(int side)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int ax = (int)((NPC.Center.X + side * (NPC.width / 2f + LookAheadPx)) / 16f);
			for (int i = 0; i < 3; i++)
			{
				int x = ax + side * i;
				if (TileNav.IsSolid(x, feet.Y) || TileNav.IsSolid(x, feet.Y - 1) || TileNav.IsSolid(x, feet.Y - 2)) return false;
				if (!TileNav.IsFloor(x, feet.Y + 1) || TileNav.HasLava(x, feet.Y) || TileNav.HasLava(x, feet.Y + 1)) return false;
			}
			return true;
		}

		// =====================================================================================================
		// locomotion
		// =====================================================================================================

		private float Gravity()
		{
			float g = NPC.gravity;
			return g > 0f ? g : DefaultGravity;
		}

		private void Accelerate(int dir, float maxSpeed, float accel)
		{
			float v = NPC.velocity.X;
			if (Math.Sign(v) == -dir) v *= 0.9f; // turning around: shed the wrong-way speed quickly
			float target = dir * maxSpeed;
			if (Math.Abs(v - target) <= accel) v = target;
			else v += v < target ? accel : -accel;
			NPC.velocity.X = v;
		}

		private void Decelerate()
		{
			NPC.velocity.X *= StopDecel;
			if (Math.Abs(NPC.velocity.X) < StopBelowPx) NPC.velocity.X = 0f;
		}

		/// <summary>v = sqrt(2 g h); the small offset keeps the arc from passing through exactly 0 (the grounded test).</summary>
		private float JumpSpeedFor(int tiles)
			=> MathHelper.Clamp((float)Math.Sqrt(2f * Gravity() * (tiles * 16 + JumpClearancePx)) + JumpSpeedOffset, MinJumpSpeed, MaxJumpSpeed);

		/// <summary>Jumps from the ground, or spends an extra jump near the top of the arc. Returns true if a jump started.</summary>
		private bool TryJump(int tiles)
		{
			tiles = Math.Clamp(tiles, 1, MaxJumpTiles);
			if (NPC.velocity.Y == 0f)
			{
				NPC.velocity.Y = -JumpSpeedFor(tiles);
				NPC.netUpdate = true;
				if (DodgeDebug) Levy.Instance?.Logger.Info($"JUMPDBG t={Main.GameUpdateCount} {SoldierName} tiles={tiles} from {string.Join('<', new System.Diagnostics.StackTrace(false).GetFrames().Skip(1).Take(3).Select(fr => fr.GetMethod()?.Name))} state={State}");
				return true;
			}
			if (_jumpsUsed < Abilities.ExtraJumps && NPC.velocity.Y > -1f)
			{
				_jumpsUsed++;
				NPC.velocity.Y = -JumpSpeedFor(Math.Min(tiles, BaseJumpTiles)) * ExtraJumpSpeedMult;
				NPC.netUpdate = true;
				return true;
			}
			return false;
		}

		/// <summary>
		/// A soldier on the move toward a distant goal hops now and then over open, floored ground so a march does not look flat-footed.
		/// Rate-limited (random interval), never in water, never while backing away, and only with a clear landing ahead.
		/// </summary>
		private void TryRunningHop(float dx, int dir)
		{
			if (_hopTimer > 0) { _hopTimer--; return; }
			if (!(State == SoldierState.Follow || State == SoldierState.ReturnHome || State == SoldierState.SearchDestroy
				|| State == SoldierState.FreeRein || State == SoldierState.Engage || State == SoldierState.Resupply)) return;
			NPC npc = NPC;
			if (npc.wet || Retreating || _doorBusy > 0 || Math.Abs(dx) < HopMinGoalDistTiles * 16f) return;
			if (Math.Abs(npc.velocity.X) < HopMinSpeedPx || Math.Sign(npc.velocity.X) != dir) return;
			if (!HopLandingClear(dir)) return;
			_hopTimer = Main.rand.Next(HopIntervalMinTicks, HopIntervalMaxTicks + 1);
			TryJump(HopTiles);
		}

		/// <summary>Headroom overhead and the next few columns open, floored and free of lava: a hop here lands on solid ground.</summary>
		private bool HopLandingClear(int dir)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			for (int r = 1; r <= HopClearRows; r++) if (TileNav.IsSolid(feet.X, feet.Y - r)) return false;
			// a hop covers speed x airtime; every column it can land on (plus a tile of slide) must be floored, or the hop carries a runner over an edge
			float airTicks = 2f * JumpSpeedFor(HopTiles) / Math.Max(0.05f, Gravity());
			int reach = Math.Max(HopLandingTiles, (int)Math.Ceiling(Math.Abs(NPC.velocity.X) * airTicks / 16f) + 2);
			for (int i = 1; i <= reach; i++)
			{
				int x = feet.X + dir * i;
				for (int r = 0; r <= HopClearRows; r++) if (TileNav.IsSolid(x, feet.Y - r)) return false;
				if (!TileNav.IsFloor(x, feet.Y + 1) || TileNav.HasLava(x, feet.Y) || TileNav.HasLava(x, feet.Y + 1)) return false;
			}
			return true;
		}

		/// <summary>One-tile steps, exactly as NPC.AI_107_ImprovedWalkers does it.</summary>
		private void StepUp()
		{
			NPC npc = NPC;
			if (npc.noGravity || npc.velocity.Y < 0f) return;
			Point c = npc.Center.ToTileCoordinates();
			if (!WorldGen.InWorld(c.X, c.Y, 5)) return;
			npc.GetTileCollisionParameters(out Vector2 cPos, out int cW, out int cH);
			Vector2 offset = npc.position - cPos;
			Collision.StepUp(ref cPos, ref npc.velocity, cW, cH, ref npc.stepSpeed, ref npc.gfxOffY);
			npc.position = cPos + offset;
		}

		/// <summary>Powered flight toward the goal; gravity is added by UpdateNPC after AI, so it is cancelled here.</summary>
		private void Fly(Vector2 goal, int dir, bool flyUp)
		{
			NPC npc = NPC;
			// thrusting but not moving: a corner of the ceiling or the wall holds the body (it is 20 px wide, a tile is 16); slide it off a few pixels
			if (PathSearch.Travel6On && _flying && npc.velocity.LengthSquared() < 0.06f && (goal - npc.Bottom).LengthSquared() > 24f * 24f) { if (++_flyJam >= 3) { FlyUnjam(goal - npc.Bottom); _flyJam = 0; } } else _flyJam = 0;
			_flying = true;
			// hold-to-accelerate: bonus is the share of the wings' peak speed earned so far (0 = flat FlySpeed, today's behaviour)
			float bonus = (_flyPeakMult - 1f) * Math.Min(1f, _flyRampTicks / (float)FlyRampTicks);
			if (_flyRampTicks < FlyRampTicks) _flyRampTicks++;
			Vector2 toGoal = goal - npc.Bottom;
			// the bonus fades out over the last FlyRampArriveDampPx to the goal so the body arrives at plain FlySpeed and stops as it always did
			Vector2 desired = flyUp
				? new Vector2(dir * FlySpeed * 0.3f, -FlySpeed * (1f + bonus * FlyUpRampShare))
				: toGoal.SafeNormalize(Vector2.Zero) * FlySpeed * (1f + bonus * MathHelper.Clamp(toGoal.Length() / FlyRampArriveDampPx, 0f, 1f));
			float acc = PathSearch.Travel6On && _pathFromRoute && RouteFlying ? RouteFlyAccel : FlyAccel;   // a planned arc turns and takes off crisply (the planner's fuel sum assumes it)
			npc.velocity.X += MathHelper.Clamp(desired.X - npc.velocity.X, -acc, acc);
			npc.velocity.Y += MathHelper.Clamp(desired.Y - npc.velocity.Y, -acc, acc);
			npc.velocity.Y -= Gravity();
			_wingTime--;
			_blocked = false;
			if (desired.X != 0f) { npc.direction = desired.X > 0f ? 1 : -1; npc.spriteDirection = npc.direction; }
		}

		private int _flyJam;
		/// <summary>Moves the body 2 to 6 px sideways of its heading to the first spot from which a few pixels of travel toward the goal are free.</summary>
		private void FlyUnjam(Vector2 toGoal)
		{
			NPC npc = NPC; Vector2 dir = toGoal.SafeNormalize(Vector2.Zero), perp = new Vector2(-dir.Y, dir.X);
			for (int k = 1; k <= 3; k++)
				for (int sgn = 1; sgn >= -1; sgn -= 2)
				{
					Vector2 cand = npc.position + perp * (sgn * 2f * k);
					if (Collision.SolidCollision(cand, npc.width, npc.height) || Collision.SolidCollision(cand + dir * 4f, npc.width, npc.height)) continue;
					npc.position = cand; FlyUnjams++; return;
				}
		}
		internal static long FlyUnjams;

		/// <summary>With wings but no flight time left, fall slowly.</summary>
		private void Glide(bool grounded)
		{
			if (grounded || _flying || !Abilities.CanGlide) return;
			if (PathSearch.Travel8On && _pathFromRoute && !RouteFlying && _path.Count > 0 && _path[0].Y - NPC.Bottom.Y > 6 * 16f) return;   // a planned long drop (a shaft) falls at full speed: wings take no fall damage, and the slow glide made the trip down 1200 tiles last minutes
			if (_wingTime > 0)
			{
				// budget in hand after an aerial climb: come down under control rather than plunge
				if (_aerialClimbed && NPC.velocity.Y > AerialDescendSpeedPx) NPC.velocity.Y = AerialDescendSpeedPx;
				return;
			}
			if (NPC.velocity.Y > GlideFallSpeed) NPC.velocity.Y = GlideFallSpeed;
		}

		/// <summary>Soldiers never body-block a player: slide out of any overlapping player hitbox.</summary>
		private void NudgeOffPlayers()
		{
			Rectangle hb = NPC.Hitbox;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active || p.dead || p.ghost) continue;
				if (!hb.Intersects(p.Hitbox)) continue;
				Vector2 nudged = NPC.position + new Vector2(NPC.Center.X >= p.Center.X ? PlayerNudgePx : -PlayerNudgePx, 0f);
				if (!Collision.SolidCollision(nudged, NPC.width, NPC.height)) NPC.position = nudged;
			}
		}

		// =====================================================================================================
		// aerial engagement: climbing to the brain's firing altitude with extra jumps, then wings, then rocket boots
		// =====================================================================================================

		/// <summary>
		/// Re-reads the flight kit when the abilities object is rebuilt (every RefreshStats, i.e. on any gear change and once a second).
		/// Wings and extra jumps come from <see cref="Abilities"/>; rocket boots (Player.rocketBoots / rocketTimeMax) from the dummy,
		/// which still carries the flags UpdateEquips just set because this runs in the same tick as RefreshStats.
		/// </summary>
		private void RefreshKit()
		{
			if (ReferenceEquals(_kitRef, Abilities)) return;
			_kitRef = Abilities;
			var a = Abilities;
			if (a == null) { _kitRocketMax = 0; _kitAny = false; AerialMaxReachTiles = 0f; AerialSustained = false; _flyPeakMult = 1f; return; }
			// peak flight speed from the wing's real horizontal speed (WingStats.AccRunSpeedOverride; unset/modded wings count as the reference tier)
			_flyPeakMult = 1f;
			if (a.HasWings && Dummy != null)
			{
				float wingRun = Dummy.GetWingStats(Dummy.wingsLogic).AccRunSpeedOverride;
				if (wingRun <= 0f) wingRun = FlyWingRefRunSpeed;
				_flyPeakMult = MathHelper.Clamp(FlyRampPeakMult + (wingRun - FlyWingRefRunSpeed) * FlyRampPeakPerRunSpeed, FlyRampPeakMinMult, FlyRampPeakMaxMult);
			}
			_kitRocketMax = Dummy != null && Dummy.rocketBoots > 0 ? Math.Max(1, Dummy.rocketTimeMax) * RocketBurstTicks : 0;
			_rocketTime = Math.Min(_rocketTime, _kitRocketMax);
			_kitAny = a.ExtraJumps > 0 || a.HasWings || _kitRocketMax > 0;
			AerialSustained = a.HasWings || _kitRocketMax > 0;
			float g = Gravity();
			float risePx = _kitAny ? a.JumpTiles * 16f : 0f;
			risePx += a.ExtraJumps * (AirJumpImpulsePx * AirJumpImpulsePx / (2f * g));
			if (a.HasWings) risePx += Math.Max(0, a.WingTimeMax - WingBudgetReserveTicks) * AerialClimbSpeedPx * AerialReachEfficiency;
			risePx += _kitRocketMax * AerialClimbSpeedPx * AerialReachEfficiency;
			AerialMaxReachTiles = risePx / 16f;
		}

		/// <summary>The flight kit has wings or rocket boots (a hover is possible), not only extra jumps.</summary>
		public bool AerialSustained { get; private set; }

		/// <summary>Standing at 0 vertical speed with the head against a ceiling and no floor underfoot: a bump, not a landing.</summary>
		private bool HeadOnCeiling()
		{
			NPC npc = NPC;
			if (!Collision.SolidCollision(new Vector2(npc.position.X, npc.position.Y - CeilingProbePx), npc.width, CeilingProbePx)) return false;
			return !Collision.SolidCollision(new Vector2(npc.position.X, npc.position.Y + npc.height), npc.width, CeilingProbePx);
		}

		private bool CeilingAbove()
			=> Collision.SolidCollision(new Vector2(NPC.position.X, NPC.position.Y - CeilingProbePx), NPC.width, CeilingProbePx);

		/// <summary>
		/// Decides whether aerial mode owns the vertical axis this tick: the brain wants a firing altitude (WantsAltitude / AltitudeBottomY),
		/// the gear can fly, this airtime has not run dry, and (from the ground) the relaunch cooldown is over and the climb is worth it.
		/// A body that is no longer wanted aloft simply drops out of the mode and falls back to the normal glide/descend rules.
		/// </summary>
		private bool UpdateAerial(bool grounded)
		{
			if (_aerialCooldown > 0) _aerialCooldown--;
			if (_airJumpCooldown > 0) _airJumpCooldown--;
			if (_aerialVisualTicks > 0) _aerialVisualTicks--;
			_aerial = false;
			if (!WantsAltitude || !_kitAny || _aerialSpent || State == SoldierState.Routed || State == SoldierState.Dead) return false;
			if (grounded && (_aerialCooldown > 0 || AltitudeBottomY > NPC.Bottom.Y - AerialClimbBenefitTiles * 16f)) return false;
			_aerial = true;
			return true;
		}

		/// <summary>
		/// One tick of vertical control toward AltitudeBottomY. From the ground: an ordinary jump. In the air: an extra jump near the apex
		/// while a big climb remains; otherwise wing thrust (budget above the reserve), then rocket boots; thrust is a proportional
		/// controller (desired speed = error x gain, capped), so it eases into a hover that feathers at a fraction of the climb's cost.
		/// Out of budget with the altitude still unreached (or a ceiling overhead) the airtime is spent: aerial mode stays off until the
		/// body has landed and the relaunch cooldown has passed. Gravity is added by UpdateNPC after AI, so thrust ticks subtract it first.
		/// </summary>
		private void ApplyAltitude(bool grounded)
		{
			NPC npc = NPC;
			float g = Gravity();
			float dy = AltitudeBottomY - npc.Bottom.Y; // < 0: the firing altitude is above the feet
			bool needUp = dy < -AltitudeToleranceTiles * 16f;
			if (grounded)
			{
				if (needUp && TryJump(Abilities.JumpTiles)) _aerialClimbed = true;
				return;
			}
			_aerialClimbed = true;
			float vy = npc.velocity.Y;

			// extra jumps first: spent near the apex of the previous impulse, and only while a real climb remains
			if (needUp && Abilities.ExtraJumps - _jumpsUsed > 0 && dy < -AirJumpMinNeedTiles * 16f)
			{
				if (vy > AirJumpApexVy && _airJumpCooldown == 0) AirJump(AirJumpImpulsePx);
				return; // otherwise coast up to the apex
			}

			float desired = MathHelper.Clamp(dy * AerialClimbGain, -AerialClimbSpeedPx, AerialDescendSpeedPx);
			if (vy <= desired + AerialThrustDeadbandPx) { Glide(false); return; } // already rising as fast as wanted (or falling gently): coast
			bool wing = Abilities.HasWings && _wingTime > WingBudgetReserveTicks;
			bool rocket = !wing && _rocketTime > 0; // boots only once the wings are out, as in vanilla
			if (!wing && !rocket)
			{
				if (needUp) _aerialSpent = true; // dry before the altitude: land, recharge, wait out the cooldown
				Glide(false);
				return;
			}
			if (desired < 0f && CeilingAbove()) { _aerialSpent = true; Glide(false); return; } // a roof overhead: stop burning budget on it

			float post = vy + Math.Max(desired - vy, -AerialClimbAccel); // vertical speed after this tick's gravity
			if (Math.Abs(post) < AerialMinVyPx) post = -AerialMinVyPx;   // never exactly 0: that reads as standing on the ground
			npc.velocity.Y = post - g;
			SpendFlight(wing, desired < AerialClimbDrainBelowVy ? AerialClimbDrain : AerialHoverDrain);
			_aerialVisualTicks = AerialVisualHoldTicks; _usedRocket = rocket;
			if (((uint)Main.GameUpdateCount + (uint)npc.whoAmI) % (uint)AerialNetSyncTicks == 0) npc.netUpdate = true;
		}

		/// <summary>Burns flight budget in fractions (hover feathering costs less than a tick of climb).</summary>
		private void SpendFlight(bool wing, float amount)
		{
			_drainAcc += amount;
			while (_drainAcc >= 1f)
			{
				_drainAcc -= 1f;
				if (wing) _wingTime--; else _rocketTime--;
			}
		}

		private void AirJump(float impulse)
		{
			_jumpsUsed++;
			NPC.velocity.Y = -(impulse + JumpSpeedOffset); // the offset keeps the arc from passing through exactly 0 (the grounded test)
			_airJumpCooldown = AirJumpCooldownTicks;
			NPC.netUpdate = true;
		}

		/// <summary>An upward kick in mid-air: an extra jump if one is left, else a wing flap, else a rocket pulse. Returns true if one was spent.</summary>
		private bool AirKick(float upPx)
		{
			if (_airJumpCooldown > 0) return false;
			if (_jumpsUsed < Abilities.ExtraJumps) { AirJump(upPx); return true; }
			if (Abilities.HasWings && _wingTime > WingBudgetReserveTicks + FlapAssistCostTicks) { _wingTime -= FlapAssistCostTicks; _usedRocket = false; }
			else if (_rocketTime > FlapAssistCostTicks) { _rocketTime -= FlapAssistCostTicks; _usedRocket = true; }
			else return false;
			NPC.velocity.Y = Math.Min(NPC.velocity.Y, -(upPx + JumpSpeedOffset));
			_airJumpCooldown = AirJumpCooldownTicks;
			_aerialVisualTicks = AerialVisualHoldTicks;
			NPC.netUpdate = true;
			return true;
		}

		/// <summary>Aloft in aerial mode a flat shot is dodged by a kick upward (extra jump, flap or pulse).</summary>
		private bool AirBlip() => AirKick(AirDodgeBlipPx);

		/// <summary>Aloft there is no floor to check: just no wall, three columns out.</summary>
		private bool SidestepClearAir(int side)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int ax = (int)((NPC.Center.X + side * (NPC.width / 2f + LookAheadPx)) / 16f);
			for (int i = 0; i < 3; i++)
			{
				int x = ax + side * i;
				if (TileNav.IsSolid(x, feet.Y) || TileNav.IsSolid(x, feet.Y - 1) || TileNav.IsSolid(x, feet.Y - 2)) return false;
			}
			return true;
		}

		/// <summary>
		/// Armed by a gap jump. Once the body is falling over the gap with the far edge a few columns ahead, one extra jump (or flap)
		/// carries it across instead of into the pit. Fires at most once per jump.
		/// </summary>
		private void TickGapAssist(bool grounded)
		{
			_gapAssist--;
			if (grounded || NPC.velocity.Y < GapAssistFallVy || _airJumpCooldown > 0) return;
			int dir = NPC.velocity.X != 0f ? (NPC.velocity.X > 0f ? 1 : -1) : (NPC.direction >= 0 ? 1 : -1);
			Point feet = TileNav.FeetTile(NPC.Bottom);
			if (TileNav.IsFloor(feet.X, feet.Y + 1) || TileNav.IsFloor(feet.X, feet.Y + 2)) return; // over footing already
			for (int i = 1; i <= GapAssistLandingTiles; i++)
			{
				int x = feet.X + dir * i;
				for (int r = -1; r <= 1; r++)
				{
					if (!TileNav.IsFloor(x, feet.Y + r) || TileNav.IsSolid(x, feet.Y + r - 1)) continue;
					if (AirKick(AirJumpImpulsePx)) _gapAssist = 0;
					return;
				}
			}
		}

		// =====================================================================================================
		// doors
		// =====================================================================================================

		/// <summary>Opens the door and hands it to the door custodian, which shuts it once the doorway is clear (whoever opened it, wherever they are by then).</summary>
		private bool TryOpenDoor(int x, int y, int dir)
		{
			if (!DoorCustodian.TryOpen(x, y, dir)) return false;
			if (SoldierHooks.Debug) Levy.Instance?.Logger.Info($"DOORDBG t={Main.GameUpdateCount} {SoldierName} opened door ({x},{y}) dir={dir} at {NPC.Bottom.X / 16f:0.0},{NPC.Bottom.Y / 16f:0.0} v={NPC.velocity.X:0.0},{NPC.velocity.Y:0.0} flying={_flying}");
			_doorBusy = DoorBusyTicks;
			return true;
		}

		// =====================================================================================================
		// stuck handling
		// =====================================================================================================

		private void ResetStuck()
		{
			_stuckTicks = 0; _stuckTotal = 0; _stuckSampleTimer = 0; _stuckJumped = false; _reverseTicks = 0;
			_stuckPos = NPC.position;
			ResetWedge();
		}

		private void ResetWedge()
		{
			_wedgeAnchor = NPC.position; _wedgeTimer = 0; _wedgeWindows = 0; _wedgeShuffled = false;
		}

		// ---- goal-jump debounce ----
		// A "goal is above me" jump that keeps landing without getting any higher (a hovering target, a ledge out of reach, a rock
		// overhead) is pointless bunny-hopping. After a short futile streak the NEXT such jump waits for the goal to be close, and after
		// GoalJumpStreakMax of them goal-jumps rest for a few seconds. Wall, gap, dodge and running-hop jumps never go through here, and
		// a jump that does gain height clears the streak, so a normal ledge climb is never touched.

		/// <summary>How far across (px) a goal above is still jumped for: the full reach, or only close in after a jump that gained nothing.</summary>
		private float GoalJumpReachPx()
			=> (_goalJumpStreak > 0 ? GoalJumpRetryReachTiles : JumpTowardGoalReachTiles) * 16 + 8;

		private bool GoalJumpAllowed()
			=> _goalJumpHold <= 0 && !Collision.SolidCollision(new Vector2(NPC.position.X, NPC.position.Y - GoalJumpHeadroomPx), NPC.width, GoalJumpHeadroomPx);

		private void NoteGoalJump()
		{
			if (_goalJumpStreak == 0) _goalJumpBaseY = NPC.Bottom.Y;
			if (_goalJumpStreak < 1000) _goalJumpStreak++;
			_goalJumpIdle = 0;
			if (_goalJumpStreak >= GoalJumpStreakMax) _goalJumpHold = GoalJumpHoldTicks;
		}

		private void TickGoalJumpDebounce(bool grounded)
		{
			if (_goalJumpHold > 0) _goalJumpHold--;
			if (_goalJumpStreak == 0) return;
			if (grounded && NPC.Bottom.Y < _goalJumpBaseY - GoalJumpGainPx) { _goalJumpStreak = 0; return; } // it got up: a real climb
			if (++_goalJumpIdle > GoalJumpStreakResetTicks) _goalJumpStreak = 0;
		}

		// ---- wedge detector ----
		// The 10-tick sampler in TrackStuck reads any vertical bounce as movement, so a body that hops against a wall or ceiling never
		// looks stuck to it. This judges NET displacement over a long window while the body is trying to walk. Two wedged windows in a
		// row force a fresh path request; four (about 6 s, the same as the existing leash) send a FOLLOWING soldier to its commander.

		private void TickWedge()
		{
			if (_flying || _aerial) { ResetWedge(); return; }
			if (_reverseTicks > 0) _wedgeShuffled = true;
			if (++_wedgeTimer < WedgeWindowTicks) return;
			_wedgeTimer = 0;
			float net = Vector2.Distance(NPC.position, _wedgeAnchor);
			_wedgeAnchor = NPC.position;
			bool shuffled = _wedgeShuffled; _wedgeShuffled = false;
			if (net >= WedgeNetPx && !shuffled) { _wedgeWindows = 0; return; }
			_wedgeWindows++;
			if (_wedgeWindows == WedgeRepathWindows) { _wedgeForcePath = UsesPath; _path.Clear(); _pathFailTimer = 0; }
			else if (_wedgeWindows >= WedgeTeleportWindows)
			{
				_wedgeWindows = 0;
				if (State == SoldierState.Follow && !OnMapMarch && FollowerTravel.TeleportNearCommander(this)) return;
			}
		}

		/// <summary>Snaps the body onto its barracks muster point (server/single player). The brain then sees it arrived and stands it down.</summary>
		/// <summary>A map-point march runs in State Follow; the catch-up teleports toward the commander must not fire on it (it is meant to leave the commander far behind).</summary>
		private static readonly bool GoToTeleportsOld = Environment.GetEnvironmentVariable("LEVY_GOTO_TELE") == "1"; // harness: restore the old behaviour for before/after
		private bool OnMapMarch => !GoToTeleportsOld && Record != null && Record.Order == SoldierOrder.GoTo;

		private bool TeleportHome()
		{
			var home = Home;
			if (home == null) return false;
			_rhBest = float.MaxValue; _rhNoProgress = 0;
			return FollowerTravel.TeleportToPoint(this, home.MusterPoint);
		}

		/// <summary>While ordered home: if the muster point has not gotten ReturnHomeProgressPx closer for ReturnHomeNoProgressTeleportTicks, teleport there. Normal travel always keeps beating its best distance.</summary>
		private void TickReturnHomeProgress()
		{
			if (State != SoldierState.ReturnHome) { _rhBest = float.MaxValue; _rhNoProgress = 0; return; }
			var home = Home;
			if (home == null) return;
			float d = Vector2.Distance(NPC.Bottom, home.MusterPoint);
			if (d < _rhBest - ReturnHomeProgressPx || _rhBest == float.MaxValue) { _rhBest = d; _rhNoProgress = 0; return; }
			if (d <= LevyConstants.HomeArriveTiles * 16f) return; // arrived: the brain dismisses / stands down
			if (++_rhNoProgress >= ReturnHomeNoProgressTeleportTicks) TeleportHome();
		}

		/// <summary>Samples position every few ticks: jump, then walk back a tile, then (following) teleport to the commander.</summary>
		private void TrackStuck()
		{
			TickWedge();
			if (++_stuckSampleTimer < StuckSampleTicks) return;
			_stuckSampleTimer = 0;
			float moved = Vector2.Distance(NPC.position, _stuckPos);
			_stuckPos = NPC.position;
			if (_sinceReverse < ReverseSettleTicks) _sinceReverse += StuckSampleTicks;
			if (moved < StuckMoveThresholdPx)
			{
				_stuckTicks += StuckSampleTicks;
				_stuckTotal += StuckSampleTicks;
			}
			else
			{
				_stuckTicks = 0; _stuckJumped = false;
				if (moved >= StuckProgressPx && _reverseTicks == 0 && _sinceReverse >= ReverseSettleTicks) _stuckTotal = 0; // real walking, not the reverse shuffle
			}
			if (State == SoldierState.Follow && !OnMapMarch && _stuckTotal >= LevyConstants.StuckTeleportTicks)
			{
				_stuckTotal = 0;
				if (FollowerTravel.TeleportNearCommander(this)) return;
			}
			if ((State == SoldierState.ReturnHome || (State == SoldierState.Engage && MarchingOrder && Record.Order == SoldierOrder.ReturnToBarracks)) && _stuckTotal >= ReturnHomeStuckTeleportTicks)
			{
				_stuckTotal = 0;
				if (TeleportHome()) return;
			}
			if (_stuckTicks < LevyConstants.StuckTicks || _blockedHard) return; // a deliberate refusal (lava, drop, wall) is not a wedge
			if (!_stuckJumped) { _stuckJumped = true; if (!(MobilityMode >= 1 && LavaAhead(NPC.direction >= 0 ? 1 : -1))) TryJump(Abilities.JumpTiles); return; }
			if (_stuckTicks >= LevyConstants.StuckTicks + StuckReverseAfterExtraTicks && _reverseTicks == 0) { _reverseTicks = ReverseTicks; _sinceReverse = 0; }
		}

		// =====================================================================================================
		// pathfinding (Follow and Haul only)
		// =====================================================================================================

		private bool UsesPath => State == SoldierState.Follow || State == SoldierState.Haul || State == SoldierState.Resupply || State == SoldierState.ReturnHome || State == SoldierState.SearchDestroy || State == SoldierState.FreeRein || State == SoldierState.Mutiny
			|| (_engPathOk && (State == SoldierState.Engage || State == SoldierState.Shaken));

		/// <summary>
		/// The body stands on a platform it can drop through, and the fall is safe. Looks at every tile column under the body's full width (not just the centre one): at least one must be a
		/// platform and none may be a solid block (it would hold him up). Refused when any column has lava below (<see cref="TileNav.DropDepth"/> -2), when no column has a floor
		/// within the scan (-1), or when the shallowest drop is deeper than a jump plus <see cref="DropMarginTiles"/> and he has no wings.
		/// </summary>
		/// <summary>Why the last platform-drop test said no (lab: the travel lab's STUCK lines print it).</summary>
		internal string DropWhy = ""; private int _dropNudge;
		/// <summary>Lab: which rule asked for wing thrust this tick (1 goal above, 2 rise, 3 route rise, 4 planned arc, 5 overhang, 6 wall, 7 gap, 8 deep drop; 0 none).</summary>
		internal int FlyWhy, DbgJump;
		private bool PlatformDropIsSafe()
		{
			var npc = NPC;
			int fy = TileNav.FeetTile(npc.Bottom).Y;
			int x0 = (int)(npc.position.X / 16f), x1 = (int)((npc.position.X + npc.width - 1f) / 16f);
			bool platform = false;
			int depth = int.MaxValue; _dropNudge = 0;
			for (int x = x0; x <= x1; x++)
			{
				if (TileNav.IsSolid(x, fy + 1))
				{
					DropWhy = "solid-under-edge";
					// the body's box overlaps a solid tile at one edge (a shaft is 2 to 4 tiles wide, the body 1.25 and a waypoint is a tile centre): step away from that edge so the platform alone is under it
					if (PathSearch.Travel8On && x0 < x1) { bool leftSolid = TileNav.IsSolid(x0, fy + 1), rightSolid = TileNav.IsSolid(x1, fy + 1); if (leftSolid != rightSolid) _dropNudge = leftSolid ? 1 : -1; }
					return false;
				}
				if (TileNav.IsPlatform(x, fy + 1)) platform = true;
				int d = TileNav.DropDepth(x, fy + 1);
				if (d == -2) { DropWhy = "lava"; return false; } // lava below: stays refused
				if (d >= 0 && d < depth) depth = d;
			}
			if (!platform || depth == int.MaxValue) { DropWhy = !platform ? "no-platform" : "no-floor-in-scan"; return false; }
			if (!(Abilities.HasWings || depth <= Abilities.JumpTiles + DropMarginTiles || (PathSearch.Travel8On && _pathFromRoute && depth <= FallScanTiles))) { DropWhy = "too-deep:" + depth; return false; }   // (a planned route takes falls up to FallScanTiles: the platform drop is such a fall)
			DropWhy = "ok"; return true;
		}

		/// <summary>The next waypoint while a path exists, else the brain's goal. Pops waypoints as they are reached.</summary>
		private Vector2 ResolveLocalGoal()
		{
			if (!UsesPath || _aerial) // aloft the ground path means nothing: fly straight at the goal's X
			{
				if (_path.Count > 0) _path.Clear();
				return MoveGoal;
			}
			float moved = PathGoalMovedTiles * 16f;
			if (_path.Count > 0 && Vector2.DistanceSquared(_pathGoal, MoveGoal) > moved * moved) { _path.Clear(); _pathTimer = PathRecalcTicks; }
			// A waypoint counts as reached inside the circle of WaypointReachPx, and also inside the arrival box the walk logic itself stops in (|dx| < ArriveXPx, |dy| < ArriveYPx): on a slope or a half block the feet sit up to
			// 16 px off the waypoint's flat height, which is outside the circle but inside the box, and the body used to stand "arrived" at a waypoint it never dropped.
			while (_path.Count > 0)
			{
				Vector2 wp = _path[0]; float wdx = wp.X - NPC.Center.X, wdy = wp.Y - NPC.Bottom.Y;
				if (wdx * wdx + wdy * wdy <= WaypointReachPx * WaypointReachPx || (!TravelLegacy.On && Math.Abs(wdx) < ArriveXPx && Math.Abs(wdy) < ArriveYPx)) _path.RemoveAt(0); else break;
			}
			if (_path.Count == 0) return MoveGoal;
			Vector2 next = _path[0];
			// A drop waypoint sits in the landing column, so a body whose centre is already over it (balanced on the lip, still held up by the edge of its box) has no sideways error left to walk off with.
			// Aim a little past it, in the direction the route goes on, until the body is in the air.
			// (a shallow drop counts too, and so does a body that stands on a slope or a half block, whose vertical speed is a small positive number: the lip of a two-tile dip held bodies for seconds)
			bool shallowOk = PathSearch.NudgeOn && PathSearch.Travel6On && !_rtHookWant && _hkPhase == HookPhase.Idle && !_clmOn;   // (not at a hook's cast point or the foot of a climb: the body stands there on purpose)
			if (!TravelLegacy.On && next.Y - NPC.Bottom.Y > (shallowOk ? 12f : 32f) && Math.Abs(next.X - NPC.Center.X) < 14f && (shallowOk ? NPC.velocity.Y >= 0f && NPC.velocity.Y < 2.5f : NPC.velocity.Y == 0f))
			{
				float onward = _path.Count > 1 ? _path[1].X : MoveGoal.X;
				float side = onward >= NPC.Center.X ? 1f : -1f;
				if (PathSearch.Travel8On)
				{
					// in a shaft the way on is straight down, so 'onward' is a coin toss: never aim into the shaft wall (the look-ahead takes it for a wall and a winged body flies up against it); if both sides are walls, drop straight
					int fy = TileNav.FeetTile(NPC.Bottom).Y;
					bool wallAt(float sd) { int ax = (int)((NPC.Center.X + sd * (NPC.width / 2f + LookAheadPx)) / 16f); return TileNav.IsSolid(ax, fy) || TileNav.IsSolid(ax, fy - 1) || TileNav.IsSolid(ax, fy - 2); }
					if (wallAt(side)) side = wallAt(-side) ? 0f : -side;
				}
				next.X += side * 24f;
			}
			return next;
		}

		/// <summary>Lava (or a drop onto lava) in the columns the body is about to cover: the next few tiles at body height and the floor under them, further out the faster it moves.</summary>
		private bool LavaAhead(int dir)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int reach = 2 + (int)(Math.Abs(NPC.velocity.X) * 10f / 16f) + 1;
			for (int i = 0; i <= reach; i++)
			{
				int x = feet.X + dir * i;
				for (int y = feet.Y - 1; y <= feet.Y + 2; y++) if (TileNav.HasLava(x, y)) return true;
				if (TileNav.DropDepth(x, feet.Y) == -2) return true;
			}
			return false;
		}

		/// <summary>Searches when the direct walk is blocked, and refreshes a live path every 30 ticks.</summary>
		private void UpdatePathRequest()
		{
			if (!UsesPath) return;
			_pathTimer++;
			if (_pathFailTimer > 0) { _pathFailTimer--; return; }
			bool want = (_blockedHard || _wedgeForcePath) && _path.Count == 0;
			_wedgeForcePath = false; // one request per wedge, whatever it finds
			if (_path.Count == 0) _pathFromRoute = false;
			bool refresh = _path.Count > 0 && _pathTimer >= PathRecalcTicks && !_pathFromRoute; // a leg handed over by the route planner is not searched again every half second
			if (!want && !refresh) return;
			_pathTimer = 0;
			_pathGoal = MoveGoal;
			if (!Pathfinder.FindPath(NPC.Bottom, MoveGoal, Abilities, _path)) { _path.Clear(); _pathFailTimer = PathFailRetryTicks; }
		}

		// =====================================================================================================
		// dummy animation hints (the Draw agent calls PlayerFrame)
		// =====================================================================================================

		private void MirrorDummyControls()
		{
			if (Dummy == null) return;
			Dummy.velocity = NPC.velocity;
			Dummy.direction = NPC.direction == 0 ? 1 : NPC.direction;
			Dummy.controlLeft = NPC.velocity.X < -StopBelowPx;
			Dummy.controlRight = NPC.velocity.X > StopBelowPx;
			bool thrusting = _aerialVisualTicks > 0;
			Dummy.controlJump = NPC.velocity.Y < 0f || _flying || thrusting;
			if (Dummy.wingsLogic > 0) Dummy.wingTime = (NPC.velocity.Y < 0f || _flying || thrusting) ? Math.Max(1, Dummy.wingTimeMax) : 0f;
			if (Dummy.rocketBoots > 0) Dummy.rocketFrame = thrusting && _usedRocket;
		}
	}
}
