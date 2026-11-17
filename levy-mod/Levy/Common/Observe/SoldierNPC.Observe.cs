using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Observe;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Owner;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Command and Observe, soldier side. Two small plug-ins that live outside the movement/brain files:
	/// the "go to map point" march (<see cref="ThinkMapOrder"/>, called from the top of ThinkOrder) and mind control
	/// (<see cref="MindControl_Drive"/> before Movement_Update, <see cref="MindControl_Act"/> after Combat_Update; the brain is skipped while controlled).
	/// Movement goes through the soldier's own goal API (SetGoal / TryJump / platform drop), so every reflex of the movement code (step up, lava refusal, doors, wings) still applies.
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- map march ----
		private Point _goStand; private int _goStandFor = int.MinValue; private Vector2 _goProgPos; private int _goProgTick, _goNoProgress;
		private const int GoLegTiles = 30, GoArriveXTiles = 3, GoArriveYTiles = 6, GoProgressTicks = 180, GoGiveUpWindows = 20, GoGiveUpWindowsAfterRoute = 8;

		/// <summary>Brain hook: marches toward the order's map point. True when it set the goal this think (the brain stops here).</summary>
		internal bool ThinkMapOrder(int now)
		{
			if (Record == null) return false;
			if (!MapOrder.TryGoal(Record, out int tx, out int ty)) { MapArrive(); return true; }
			int key = tx * 100003 + ty;
			if (_goStandFor != key) { _goStand = MapOrder.ResolveStand(tx, ty); _goStandFor = key; _goProgPos = NPC.Center; _goProgTick = now; _goNoProgress = 0; RouteReset(); }
			SetState(SoldierState.Follow); // runs rather than strolls; combat still takes over through TryEngage before this is reached
			Vector2 goal = TileNav.BottomCentre(_goStand.X, _goStand.Y);
			float dx = goal.X - NPC.Center.X, dy = goal.Y - NPC.Bottom.Y;
			if (Math.Abs(dx) <= GoArriveXTiles * 16f && Math.Abs(dy) <= GoArriveYTiles * 16f && !ClimbActive) { MapArrive(); return true; }
			// out of every player's sight and stuck for six seconds: hop ahead (SoldierNPC.CatchUp.cs); it then goes on from the new spot
			if (CatchUpTick(now, goal)) { _goProgPos = NPC.Center; _goProgTick = now; _goNoProgress = 0; return true; }
			if (now - _goProgTick >= GoProgressTicks)
			{
				_goNoProgress = Vector2.DistanceSquared(NPC.Center, _goProgPos) < 24f * 24f ? _goNoProgress + 1 : 0;
				_goProgPos = NPC.Center; _goProgTick = now;
				if (_goNoProgress >= (RouteExhausted ? GoGiveUpWindowsAfterRoute : GoGiveUpWindows))
				{
					if (RouteExhausted) MapOrder.NoteUnreachable(Record, NPC.Center); // no way found, and the closest point of the best route is where it stopped
					MapArrive(); return true; // a minute of no progress (30 s once the planned route has run out): stop where he stands
				}
			}
			// a long or awkward trip follows a planned route (RoutePlanner); while the plan is still being searched, or when there is none, aim by the old rule: a point one leg on toward the goal
			if (RouteLeg(now, goal, out Vector2 leg)) { SetGoal(leg); return true; }
			SetGoal(MapOrder.LegGoal(NPC.Center, NPC.Bottom, goal));
			return true;
		}

		/// <summary>Arrival (or giving up): the order becomes Hold at this spot.</summary>
		private void MapArrive()
		{
			MapOrder.ClearGoal(Record);
			Record.Order = SoldierOrder.Hold; HoldPosition = true; Record.HoldPos = NPC.Center;
			ClearGoal(); SetState(SoldierState.Idle); _goStandFor = int.MinValue; RouteReset();
			NPC.netUpdate = true; MapOrder.SnapshotDue = true;
		}

		// ---- mind control ----
		private bool _mindWas;
		private bool _mindPJump, _mindPL, _mindPR, _mindPHook, _mindPSwap;
		/// <summary>The hand the player chose with the hotbar keys: -1 the brain's own pick, 0 the main weapon, 1 the sidearm.</summary>
		internal int MindSide = -1;

		/// <summary>True while a player's input drives this soldier.</summary>
		public bool MindControlled => Record != null && MindControl.LinkOf(Record.Id) != null;
		/// <summary>Driven by the direct controller (movement and attack straight from the keys), not the old goal-steered drive kept for the audit's "before" numbers.</summary>
		internal bool MindDirect => !MindControl.Legacy && MindControlled;

		partial void MindControl_Drive();
		partial void MindControl_Act();

		partial void MindControl_Drive()
		{
			var link = Record == null ? null : MindControl.LinkOf(Record.Id);
			if (link == null)
			{
				if (_mindWas) { _mindWas = false; MindRelease(); } // released: the brain takes the next think
				return;
			}
			if (!MindControl.Legacy) { MindBegin(); return; }
			_mindWas = true;
			var inp = link.In;
			HoldPosition = false;
			MindControl.Map(in inp, NPC.Center, NPC.Bottom.Y, NPC.velocity.Y == 0f, Abilities.HasWings && _wingTime > 0, out bool hasGoal, out Vector2 goal, out bool jump, out bool drop);
			if (hasGoal) SetGoal(goal); else ClearGoal();
			if (jump) TryJump(Math.Max(1, Abilities.JumpTiles));
			if (drop) { _fallThroughTicks = 12; WantsToFallThrough = true; }

			// attack input: lock the hostile nearest the cursor (or, for a swing, nearest the soldier) and let the normal combat code fire
			TargetIndex = -1;
			State = inp.Use ? SoldierState.Engage : SoldierState.Follow;
			if (inp.Use && Weapon != null && Weapon.Supported)
			{
				int idx = MindControl.HostileNear(inp.Aim, 6f);
				if (idx < 0 && !Weapon.IsRanged) idx = MindControl.HostileNear(NPC.Center, 10f);
				if (idx >= 0) TargetIndex = idx;
			}
			StateTimer = 0;
		}

		partial void MindControl_Act()
		{
			if (!MindControl.Legacy) return;
			var link = Record == null ? null : MindControl.LinkOf(Record.Id);
			if (link == null) return;
			var inp = link.In;
			if (inp.Use || inp.Alt) { if (!SwingFacingLocked && Math.Abs(inp.Aim.X - NPC.Center.X) > 2f) NPC.direction = inp.Aim.X < NPC.Center.X ? -1 : 1; }
			// open ground under the cursor: a ranged weapon fires at that point
			if (inp.Use && TargetIndex < 0 && Weapon != null && Weapon.Supported && Weapon.IsRanged && !Weapon.IsDeployer && !Record.HoldFire)
			{
				try { WeaponUse.TryAttack(this, SoldierTargeting.FocusPhantom(inp.Aim, NPC.Center)); }
				catch (Exception e) { MindControl.Warn(e); }
			}
		}

		/// <summary>The first tick of a link: everything the brain was doing is dropped, so no goal, route, hook plan or dash of its own is left to steer the body.</summary>
		private void MindBegin()
		{
			if (_mindWas) return;
			_mindWas = true;
			HoldPosition = false; ClearGoal(); TargetIndex = -1; _focusBare = false;
			_path.Clear(); _dashTicks = 0; _dodgeTicks = 0; _fallThroughTicks = 0; _flying = false; _blocked = false; _blockedHard = false;
			if (_hkPhase != HookPhase.Idle) { try { HookReset(false); } catch (Exception) { } }
			Sit_Stop();
			_mindPJump = _mindPL = _mindPR = _mindPHook = false; MindSide = -1;
			NPC.netUpdate = true;
		}

		/// <summary>The link ended (released, respawn, the levy was handed on): the player's keys are let go and the body goes back to the brain.</summary>
		private void MindRelease()
		{
			ClearGoal(); TargetIndex = -1; MindSide = -1; HoldPosition = false;
			if (Dummy != null) { Dummy.controlLeft = Dummy.controlRight = Dummy.controlJump = Dummy.controlDown = Dummy.controlUp = false; Dummy.releaseLeft = Dummy.releaseRight = Dummy.releaseJump = true; Dummy.dashTime = 0; Dummy.jump = 0; }
			if (Weapon != null) UpdateSidearm();
			NPC.netUpdate = true;
		}
	}
}
