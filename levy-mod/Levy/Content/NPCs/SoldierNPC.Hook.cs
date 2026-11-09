using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Owner;
using Levy.Common.Weapons;
using H = Levy.Common.LevyConstants.Hooks;

namespace Levy.Content.NPCs
{
	/// <summary>Why a soldier is thinking about a hook. The reason sets how long the blockage must hold first.</summary>
	public enum HookReason : byte { None, GoalAbove, Wall, Gap, Flee, Altitude, Rescue, Follow, Route }

	/// <summary>
	/// GRAPPLING HOOKS. A levy carries a real hook item (SoldierGear.Hook, the dummy's miscEquips[4]) and casts it with the vanilla <c>Player.QuickGrapple</c> inside an owner frame
	/// (<see cref="SoldierOwner"/>): the hook projectile is the real one, its own AI runs in the same frame every tick, and the reel is the real <c>Player.GrappleMovement</c> whose velocity the body adopts.
	/// <para/>State machine: Idle, then (a trigger held long enough, no walking path, a plan that beats walking and jumping) InFlight (the hook flies, further hooks of a multi-hook plan are cast),
	/// Reel (the body is pulled; this file owns the tick), Ballistic (after the release: the walking AI is skipped while airborne, with 0.1 px/tick air drag on x, else its Decelerate eats the momentum), Cooldown.
	/// The release is vanilla's own (controlJump + releaseJump inside GrappleMovement). Idle soldiers cost one compare per tick.
	/// <para/>Triggers (all feed <see cref="HookTrigger"/>, and <see cref="HookEvaluate"/> turns a sustained one into a plan): a goal above that jumping cannot reach, a wall or overhang ahead, a gap wider than a jump
	/// (ScanAhead), a retreating soldier that is blocked, and a target far overhead for a soldier with no flight kit (UpdateAltitudeIntent).
	/// </summary>
	public partial class SoldierNPC
	{
		internal enum HookPhase : byte { Idle, Cast, InFlight, Reel, Release, Ballistic, Cooldown }

		private HookPhase _hkPhase;
		private HookPlan _hkPlan;
		private readonly List<int> _hkProj = new(4);
		private int _hkTicks, _hkCasted, _hkStall, _hkLost, _hkFirstLatch, _hkHang, _hkFailStreak, _hkNetTick;
		private float _hkBestDist;
		private uint _hkNextAllowed;
		private HookReason _hkReason, _hkTrigReason; private int _hkTrigTicks, _hkTrigNeed = int.MaxValue; private uint _hkTrigLast; private Vector2 _hkTrigGoal;
		private bool _hkTeleportApplying; private Vector2 _hkTrigGoalUsed;
		/// <summary>Set by the brain each think (UpdateAltitudeIntent): the soldier has no flight kit and a target hangs far above it. The goal is a standing height at the target's x.</summary>
		private uint _hkAltSeen; private float _hkAltY, _hkAltX;
		// net mirror (clients draw the chain from these)
		private bool _hkNetOn; private Vector2 _hkNetAnchor;

		internal HookPhase HookState => _hkPhase;
		/// <summary>Per-soldier counters (harness and diagnostics).</summary>
		internal int HkCasts, HkLatches, HkReleases, HkLandings, HkAborts;
		/// <summary>HARNESS: start a plan now toward <paramref name="goal"/> (feet), skipping the trigger's hold time. True when a hook went out.</summary>
		internal bool HookLabStart(HookReason r, Vector2 goal) { if (!HookUsable || _hkPhase != HookPhase.Idle) return false; HookStart(r, goal); return _hkPhase != HookPhase.Idle; }
		/// <summary>HARNESS: stands in for the brain's altitude intent (a standing height at an x).</summary>
		internal void HookLabAltitude(float x, float y) { _hkAltSeen = Main.GameUpdateCount; _hkAltX = x; _hkAltY = y; }
		internal void HookLabReset() { _hkPhase = HookPhase.Idle; _hkProj.Clear(); _hkNextAllowed = 0; _hkTrigTicks = 0; _hkFailStreak = 0; _hkAltSeen = 0; HkCasts = HkLatches = HkReleases = HkLandings = HkAborts = 0; }
		public bool IsHooking => _hkPhase != HookPhase.Idle && _hkPhase != HookPhase.Cooldown;
		/// <summary>Harness / diagnostics.</summary>
		internal HookPlan HookCurrentPlan => _hkPlan;
		internal HookReason HookLastReason => _hkReason;
		internal int HookCountAlive { get { HookGather(out int a, out _); return a; } }

		/// <summary>The soldier may use hooks now: config on, a usable hook in the slot, a live body.</summary>
		private void HookDbg(string m) { if (SoldierHooks.Debug) Levy.Instance?.Logger.Info($"HOOKDBG t={Main.GameUpdateCount} {SoldierName} {_hkPhase}/{_hkTicks}: " + m); }
		private string HookProjInfo() { var sb = new System.Text.StringBuilder(); foreach (int idx in _hkProj) { Projectile p = Main.projectile[idx]; sb.Append($"[#{idx} act={p.active} ai0={p.ai[0]} c={p.Center.X / 16f:0.0},{p.Center.Y / 16f:0.0}] "); } return sb.ToString(); }

		private void HookLog(string m) { if (SoldierHooks.LogCasts) SoldierHooks.Log($"HK t={Main.GameUpdateCount} {SoldierName} @{NPC.Bottom.X / 16f:0.0},{NPC.Bottom.Y / 16f:0.0} v={NPC.velocity.X:0.0},{NPC.velocity.Y:0.0} {m}"); }

		/// <summary>The speed of a full jump off the hook (the jump this body makes from the ground, not the bare first frame of vanilla's: the held jump button adds the rest).</summary>
		private float HookJumpSpeed => JumpSpeedFor(Math.Max(2, Abilities.JumpTiles));

		private bool HookUsable => SoldierHooks.Enabled && Abilities != null && Abilities.HasHook && Record != null;

		// ---------------------------------------------------------------------------------------------
		// bookkeeping the projectile side calls
		// ---------------------------------------------------------------------------------------------
		internal void HookNoteSpawn(int projIndex) { if (!_hkProj.Contains(projIndex)) _hkProj.Add(projIndex); }
		internal bool HookOwns(int projIndex) => _hkPhase != HookPhase.Idle && _hkProj.Contains(projIndex);

		/// <summary>A hook moved the dummy by itself (Hook of Dissonance's teleport): the body goes with it.</summary>
		internal void HookAdoptTeleport(Vector2 pos, Vector2 vel)
		{
			_hkTeleportApplying = true;
			try { NPC.position = pos; NPC.velocity = vel; NPC.netUpdate = true; }
			finally { _hkTeleportApplying = false; }
		}

		/// <summary>The hooks this soldier has alive: prunes the list and counts the latched ones.</summary>
		private void HookGather(out int alive, out int latched)
		{
			alive = 0; latched = 0;
			for (int k = _hkProj.Count - 1; k >= 0; k--)
			{
				int idx = _hkProj[k];
				Projectile p = idx >= 0 && idx < Main.maxProjectiles ? Main.projectile[idx] : null;
				if (p == null || !p.active || !Main.projHook[p.type] || !p.TryGetGlobalProjectile(out HookTag t) || t.Soldier != NPC.whoAmI) { _hkProj.RemoveAt(k); continue; }
				alive++; if (p.ai[0] == 2f) latched++;
			}
		}

		private bool HookCentroid(out Vector2 centroid)
		{
			centroid = Vector2.Zero; int n = 0;
			for (int k = 0; k < _hkProj.Count; k++)
			{
				Projectile p = Main.projectile[_hkProj[k]];
				if (p.active && p.ai[0] == 2f) { centroid += p.Center; n++; }
			}
			if (n == 0) return false;
			centroid /= n; return true;
		}

		/// <summary>Takes every hook of this soldier out of the world (no packets: clients never knew of them).</summary>
		private void HookKillAll()
		{
			for (int k = 0; k < _hkProj.Count; k++)
			{
				int idx = _hkProj[k];
				if (idx < 0 || idx >= Main.maxProjectiles) continue;
				Projectile p = Main.projectile[idx];
				if (p.active && p.TryGetGlobalProjectile(out HookTag t) && t.Soldier == NPC.whoAmI) { p.active = false; p.timeLeft = 0; }
			}
			_hkProj.Clear();
			if (Dummy != null) { Dummy.grappling[0] = -1; Dummy.grapCount = 0; }
		}

		/// <summary>Back to Idle with everything released: death, teleport, config off, a lost hook item.</summary>
		private void HookReset(bool cooldown)
		{
			_hkChain = false; _hkChainFlight = false;
			if (_hkPhase == HookPhase.Idle && _hkProj.Count == 0 && !_hkNetOn) return;
			HookKillAll();
			NPC.noGravity = false;
			_hkNetOn = false;
			_hkPhase = cooldown ? HookPhase.Cooldown : HookPhase.Idle;
			if (cooldown) _hkNextAllowed = Math.Max(_hkNextAllowed, Main.GameUpdateCount + (uint)H.CooldownTicks);
		}

		/// <summary>The soldier died: hooks die with it and its owner slot is freed.</summary>
		private void HookOnDeath()
		{
			try { HookReset(false); } catch (Exception) { }
			OwnerSlots.Release(this);
		}

		// ---------------------------------------------------------------------------------------------
		// triggers
		// ---------------------------------------------------------------------------------------------
		private Vector2 _hkGoalNow;
		/// <summary>Harness/debug one-liner of what the hook layer and the walking layer are doing.</summary>
		internal string HookDbgState => $"state={State} usable={HookUsable} wings={Abilities?.HasWings} hasPath={HasPath} hasGoal={HasMoveGoal} goal=({MoveGoal.X / 16f:0},{MoveGoal.Y / 16f:0}) pos=({NPC.Bottom.X / 16f:0},{NPC.Bottom.Y / 16f:0}) trig={_hkTrigTicks}/{_hkTrigNeed}:{_hkTrigReason} phase={_hkPhase} failStreak={_hkFailStreak} nextIn={(int)_hkNextAllowed - (int)Main.GameUpdateCount}";

		/// <summary>A blocked branch of the movement asks for a hook. Counts consecutive ticks; <see cref="HookEvaluate"/> acts when it has held long enough.</summary>
		private void HookTrigger(HookReason r)
		{
			if (_hkPhase != HookPhase.Idle || !HookUsable || Abilities.HasWings) return;
			if (Retreating && r != HookReason.Altitude) r = HookReason.Flee;
			uint now = Main.GameUpdateCount;
			int need = TriggerTicksFor(r);
			if (now == _hkTrigLast) { if (need < _hkTrigNeed) { _hkTrigNeed = need; _hkTrigReason = r; } }          // two branches asked in one tick: one tick of blockage, the shorter wait counts
			else if (now - _hkTrigLast <= (uint)H.TriggerGraceTicks) { _hkTrigTicks += (int)(now - _hkTrigLast); if (need < _hkTrigNeed) { _hkTrigNeed = need; _hkTrigReason = r; } }
			else { _hkTrigTicks = 1; _hkTrigNeed = need; _hkTrigReason = r; }
			_hkTrigLast = now; _hkTrigGoal = _hkGoalNow;
		}

		private static int TriggerTicksFor(HookReason r) => r switch
		{
			HookReason.GoalAbove => H.TriggerGoalAboveTicks, HookReason.Wall => H.TriggerWallTicks, HookReason.Gap => H.TriggerGapTicks,
			HookReason.Flee => H.TriggerFleeTicks, HookReason.Follow => H.TriggerFollowTicks, HookReason.Altitude => H.TriggerAltitudeTicks, _ => int.MaxValue,
		};

		/// <summary>The top of Movement_Update: a trigger that has held long enough, with no walking path, becomes a plan and a cast.</summary>
		private void HookEvaluate()
		{
			uint now = Main.GameUpdateCount;
			if (now - _hkTrigLast > (uint)H.TriggerGraceTicks) { _hkTrigTicks = 0; return; }
			if (_hkTrigTicks < _hkTrigNeed || now < _hkNextAllowed) return;
			if (_hkTrigReason != HookReason.Flee && _hkTrigReason != HookReason.Altitude && _hkTrigReason != HookReason.Follow && HasPath) return; // the walking AI has a route: it wins
			if (NPC.velocity.Y != 0f || NPC.wet || _flying) return;                                          // cast from the ground
			if (!SoldierHooks.TakePlanBudget()) return;
			try { HookStart(_hkTrigReason, _hkTrigGoal); }
			catch (Exception e)
			{
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: hook planning failed on soldier '" + SoldierName + "': " + e);
				try { HookReset(true); } catch (Exception) { }
				HookBackoff();
			}
			_hkTrigTicks = 0;
		}

		/// <summary>The commander is swinging on a hook and has left the soldier behind: a following soldier with a hook of its own goes after them the same way.</summary>
		/// <summary>0 = hooks only for blocked walks, falls and a commander on a hook; 1 = also for a goal up a cliff or a long way round. The harness flips it for before/after.</summary>
		internal static int HookShortcutMode = 1;
		private bool _hkChain, _hkChainFlight, _hkChainUsed, _hkClimb, _hkJumpKick; private int _hkChainCount, _hkJumpTries; private uint _hkChainStart, _hkAirTry;
		internal int HkChains;

		/// <summary>
		/// Chain climbing, as a player spams the hook up a cliff: on arrival at a wall anchor that is not yet the top, the hook comes off and the next one goes out higher while the body hangs, as many
		/// times as it takes (the wall's height does not matter). Each link is the cast that gets the body nearest the goal (<see cref="HookPlanner.PlanClimb"/>); within reach of the goal the link is a landing plan.
		/// When no anchor in reach gets the body closer, the body jumps off the hook (a full jump, extra jumps refreshed) and casts again in the air. False when nothing works (the usual release).
		/// </summary>
		private bool TryChain(Vector2 centroid)
		{
			uint now = Main.GameUpdateCount;
			if (_hkChainCount >= H.ChainMaxLinks || now - _hkChainStart > (uint)H.ChainMaxTicks) { HookLog("chain cap"); return false; }
			if (!PlanChainLink(out HookPlan np, out bool final)) { HookLog("chain: no anchor gains ground " + HookPlanner.Describe()); return HookJumpFromHang(); }
			StartChainLink(np, final);
			return true;
		}

		/// <summary>The next cast of a climb from where the body is (hanging or in the air). False when there is none.</summary>
		private bool PlanChainLink(out HookPlan np, out bool final)
		{
			np = null; final = false;
			Vector2 goal = _hkTrigGoalUsed, bottom = NPC.Bottom;
			HookInfo info = HookInfo.Of(Record.Gear.Hook);
			HookParams hp = HookParams.For(info, NPC.width, NPC.height, Math.Max(5.01f, Player.jumpSpeed), Gravity()); hp.AirSteer = _hkReason == HookReason.Route; hp.Climb = true; hp.KickSpeed = HookJumpSpeed;
			float straight = Vector2.Distance(bottom, goal), climbNow = HookPlanner.ClimbDist(bottom, goal);
			if (straight <= info.ReachPx * 0.95f)
			{
				float nd = straight + Math.Abs(bottom.Y - goal.Y) * 0.5f;
				np = HookPlanner.Plan(TileHookWorld.Instance, bottom, hp, goal, HookGoalKind.Ground, nd);
				final = np != null && np.Beats(H.ArriveTiles * 16f, H.MinGainTiles * 16f);
			}
			if (!final) np = HookPlanner.PlanClimb(TileHookWorld.Instance, bottom, hp, goal, climbNow, H.ChainMinGainPx);
			return np != null && np.Anchors.Count > 0;
		}

		private void StartChainLink(HookPlan np, bool final)
		{
			HookKillAll();
			_hkPlan = np; _hkPhase = HookPhase.InFlight; _hkTicks = 0; _hkCasted = 0; _hkStall = 0; _hkLost = 0; _hkFirstLatch = -1; _hkHang = 0; _hkBestDist = float.MaxValue;
			_hkChainCount++; HkChains++; _hkChainUsed = true; _hkClimb = !final; if (final) _hkChain = false; _hkChainFlight = true;
			NPC.velocity = Vector2.Zero; NPC.noGravity = true;
			HookLog($"chain {_hkChainCount} " + (final ? "final" : $"anchor={np.Anchors[0].X / 16f:0},{np.Anchors[0].Y / 16f:0}"));
			if (!HookCast(np.Aims[0])) { _hkChain = false; _hkChainFlight = false; HookLetGo(false, true); return; }
			_hkCasted = 1;
		}

		/// <summary>
		/// A gap wider than a cast reaches: the body runs off the lip with a full jump toward the far side, and casts from the air (the climb machinery's air recast) once the far wall is in reach.
		/// False when the body is not standing at an open edge facing the goal.
		/// </summary>
		private bool TryJumpCast(HookReason reason, Vector2 goalBottom)
		{
			if (NPC.velocity.Y != 0f || NPC.wet) return false;
			int side = Math.Sign(goalBottom.X - NPC.Center.X); if (side == 0) return false;
			Point f = TileNav.FeetTile(NPC.Bottom);
			if (TileNav.IsFloor(f.X + side, f.Y + 1) || TileNav.IsSolid(f.X + side, f.Y - 1) || TileNav.IsSolid(f.X + side, f.Y - 2)) return false;   // not at an open lip
			float gap = Math.Abs(goalBottom.X - NPC.Center.X);
			if (gap < 8f * 16f) return false;
			_hkReason = reason; _hkTrigGoalUsed = goalBottom; _hkChain = true; _hkClimb = true; _hkJumpKick = true; _hkChainCount = 0; _hkJumpTries = 1; _hkChainStart = Main.GameUpdateCount; _hkChainUsed = true; _hkChainFlight = false;
			_hkPlan = null; _hkProj.Clear(); _hkAirTry = Main.GameUpdateCount;
			NPC.velocity.X = side * Math.Min(Abilities.MaxRunSpeed, 3.6f); NPC.velocity.Y = -HookJumpSpeed; NPC.direction = NPC.spriteDirection = side;
			_jumpsUsed = 0; _hkPhase = HookPhase.Ballistic; _hkTicks = 0; NPC.netUpdate = true;
			HookLog($"jump-cast over a gap of {gap / 16f:0} tiles");
			return true;
		}

		/// <summary>HOOK-JUMP: no anchor gets the hanging body closer, so it lets go with a jump (a full jump from the hook, the extra jumps refreshed, as vanilla's release does) and casts again on the way.</summary>
		private bool HookJumpFromHang()
		{
			if (_hkJumpTries >= H.HookJumpMax) return false;
			_hkJumpTries++;
			HookKillAll(); NPC.noGravity = false; _hkNetOn = false; _hkChainFlight = false;
			float gx = _hkTrigGoalUsed.X - NPC.Center.X;
			NPC.velocity.Y = -HookJumpSpeed;
			NPC.velocity.X = Math.Abs(gx) > 20f ? Math.Sign(gx) * 1.5f : 0f;
			_jumpsUsed = 0; _hkAirTry = Main.GameUpdateCount;
			SoldierHooks.Releases++; HkReleases++;
			_hkPhase = HookPhase.Ballistic; _hkTicks = 0; _hkChainUsed = true;
			HookLog($"hook-jump {_hkJumpTries}");
			NPC.netUpdate = true;
			return true;
		}

		private float PathLengthPx()
		{
			float len = 0f; Vector2 p = NPC.Bottom;
			for (int i = 0; i < _path.Count; i++) { len += Vector2.Distance(p, _path[i]); p = _path[i]; }
			return len;
		}

		/// <summary>
		/// A player goes UP a cliff with the hook and does not take the long way round. A goal well above that jumping cannot reach, or a goal whose walking path is much longer than the straight line, becomes a trigger.
		/// Gentle slopes never trigger (the rise has to be steep against the distance), and the planner still has to find a line that lands closer.
		/// </summary>
		private void HookShortcut()
		{
			if (HookShortcutMode < 1 || !HasMoveGoal || NPC.velocity.Y != 0f || NPC.wet) return;
			if (State != SoldierState.Follow && State != SoldierState.SearchDestroy && State != SoldierState.FreeRein && State != SoldierState.Engage) return;
			Vector2 goal = MoveGoal;
			float dxPx = Math.Abs(goal.X - NPC.Center.X), upPx = NPC.Bottom.Y - goal.Y, direct = Vector2.Distance(NPC.Bottom, goal);
			if (direct > 46f * 16f || direct < 6f * 16f) return;
			bool steepUp = upPx >= 6f * 16f && upPx >= 0.45f * dxPx && upPx > Abilities.JumpTiles * 16f + 16f;
			bool longWay = _path.Count > 0 && upPx > 2f * 16f && PathLengthPx() > Math.Max(30f * 16f, 2.5f * direct);
			if (!steepUp && !longWay) return;
			if (steepUp && _path.Count > 0 && !longWay) return;   // a path up exists and is not long: walk it
			_hkGoalNow = goal; HookTrigger(HookReason.Follow);
		}

		private void HookFollowCommander()
		{
			if (_hkPhase == HookPhase.Idle && HookUsable && !Abilities.HasWings) HookShortcut();
			if (State != SoldierState.Follow || !HookUsable || Abilities.HasWings || _hkPhase != HookPhase.Idle) return;
			Player c = Commander;
			if (c == null || c.dead || c.grapCount <= 0) return;
			Vector2 cb = c.Bottom;
			float dx = Math.Abs(cb.X - NPC.Center.X), dyUp = NPC.Bottom.Y - cb.Y;
			if (dx < H.FollowMinTiles * 16f && dyUp < H.FollowMinTiles * 16f * 0.5f) return;
			_hkGoalNow = cb; HookTrigger(HookReason.Follow);
		}

		/// <summary>The brain's altitude intent for a target far overhead: a soldier with no flight kit climbs by hook (the aerial pursuit needs wings or boots).</summary>
		private void HookAltitudeIntent(NPC seg, float rangeTiles, bool melee)
		{
			if (SoldierHooks.Debug && (Main.GameUpdateCount & 63) == 1) HookDbg($"altitude intent called: usable={HookUsable} flightKit={HasFlightKit} sep={(NPC.Center.Y - seg.Center.Y) / 16f:0.0}T range={rangeTiles:0.0}");
			if (!HookUsable || HasFlightKit) { _hkAltSeen = 0; return; }
			float tilePx = LevyConstants.TilePx;
			float sepTiles = (NPC.Center.Y - seg.Center.Y) / tilePx;
			if (sepTiles < H.AltitudeMinTiles) { _hkAltSeen = 0; return; }
			if (!melee && Aiming.TilesToHitbox(NPC.Center, seg) > rangeTiles * LevyConstants.ShootableRangeFrac * LevyConstants.Movement.AerialHoldRangeMult) { if (SoldierHooks.Debug && (Main.GameUpdateCount & 63) == 0) HookDbg($"altitude intent: target out of reach ({Aiming.TilesToHitbox(NPC.Center, seg):0.0}T > {rangeTiles * LevyConstants.ShootableRangeFrac * LevyConstants.Movement.AerialHoldRangeMult:0.0}T)"); _hkAltSeen = 0; return; }
			if (!IsHooking && RoofOverhead()) { _hkAltSeen = 0; return; }   // on the hook the ceiling above IS the anchor
			_hkAltSeen = Main.GameUpdateCount;
			_hkAltY = seg.Center.Y + (melee ? 0f : LevyConstants.FiringAltitudeBelowTargetTiles * tilePx) + BodyHeight * 0.5f;
			_hkAltX = seg.Center.X;
		}

		// ---------------------------------------------------------------------------------------------
		// start: plan and cast
		// ---------------------------------------------------------------------------------------------
		private void HookStart(HookReason reason, Vector2 goal)
		{
			Item hookItem = Record.Gear.Hook;
			HookInfo info = HookInfo.Of(hookItem);
			if (!info.Usable) return;
			long t0 = Stopwatch.GetTimestamp();
			HookPlanner.Trace = SoldierHooks.Debug;
			bool altitude = reason == HookReason.Altitude;
			Vector2 goalBottom = altitude ? new Vector2(_hkAltX, _hkAltY) : goal;
			Vector2 bottom = NPC.Bottom;
			float nowDist = Vector2.Distance(bottom, goalBottom) + Math.Abs(bottom.Y - goalBottom.Y) * 0.5f;
			float js = Math.Max(5.01f, Player.jumpSpeed);
			bool climbReason = !altitude && (reason == HookReason.Route || reason == HookReason.Wall || reason == HookReason.GoalAbove || reason == HookReason.Follow || reason == HookReason.Gap);
			HookParams hp = HookParams.For(info, NPC.width, NPC.height, js, Gravity()); hp.AirSteer = reason == HookReason.Route; hp.Climb = climbReason; hp.KickSpeed = HookJumpSpeed;
			HookPlan plan = HookPlanner.Plan(TileHookWorld.Instance, bottom, hp, goalBottom, altitude ? HookGoalKind.Altitude : HookGoalKind.Ground, nowDist);
			SoldierHooks.PlanTs += Stopwatch.GetTimestamp() - t0; SoldierHooks.Plans++;
			if (SoldierHooks.Debug && plan == null || (SoldierHooks.Debug && Environment.GetEnvironmentVariable("LEVY_HOOKDBG2") == "1")) foreach (var l in HookPlanner.DiagLog.Take(60)) Levy.Instance?.Logger.Info("HOOKDBG2 " + l);
			if (SoldierHooks.Debug) Levy.Instance?.Logger.Info($"HOOKDBG {SoldierName} {reason} from {bottom.X / 16f:0.0},{bottom.Y / 16f:0.0} goal {goalBottom.X / 16f:0.0},{goalBottom.Y / 16f:0.0} nowDist {nowDist / 16f:0.0}T: " + (plan == null ? "no plan" : $"anchors={plan.Anchors.Count} first={plan.Anchors[0].X / 16f:0.0},{plan.Anchors[0].Y / 16f:0.0} relTick={plan.ReleaseTick} relDist={plan.ReleaseDist:0} land={plan.Landing.X / 16f:0.0},{plan.Landing.Y / 16f:0.0} goalDist={plan.GoalDist / 16f:0.0}T gain={plan.Gain / 16f:0.0}T score={plan.Score:0}") + " " + HookPlanner.Describe() + (plan?.Path != null ? " path: " + string.Join(" ", plan.Path.Where((v, i) => i % 4 == 0).Select(v => $"{v.X / 16f:0.0},{v.Y / 16f:0.0}")) : ""));
			HookLog($"plan {reason} goal={goalBottom.X / 16f:0},{goalBottom.Y / 16f:0} " + (plan == null ? "none " + HookPlanner.Describe() : $"anchor={plan.Anchors[0].X / 16f:0},{plan.Anchors[0].Y / 16f:0} n={plan.Anchors.Count} {(plan.Hang ? "hang" : "land=" + (plan.Landing.X / 16f).ToString("0") + "," + (plan.Landing.Y / 16f).ToString("0"))} gain={plan.Gain / 16f:0.0}T beats={plan.Beats(H.ArriveTiles * 16f, H.MinGainTiles * 16f)}"));
			bool planOk = plan != null && (reason == HookReason.Rescue ? plan.Landed : plan.Beats(H.ArriveTiles * 16f, H.MinGainTiles * 16f));
			bool climbStart = false;
			if (!planOk && climbReason)
			{
				// no single cast gets there: climb (a chain of casts up the face, or across to the far wall of a chasm and up it)
				HookPlan cp = HookPlanner.PlanClimb(TileHookWorld.Instance, bottom, hp, goalBottom, HookPlanner.ClimbDist(bottom, goalBottom), H.ChainMinGainPx);
				HookLog("climb plan " + (cp == null ? "none " + HookPlanner.Describe() : $"anchor={cp.Anchors[0].X / 16f:0},{cp.Anchors[0].Y / 16f:0} gain={cp.Gain / 16f:0.0}T"));
				if (cp != null) { plan = cp; planOk = true; climbStart = true; }
			}
			if (!planOk && (reason == HookReason.Route || reason == HookReason.Gap) && TryJumpCast(reason, goalBottom)) return;
			if (!planOk)
			{
				if (reason == HookReason.Wall || reason == HookReason.GoalAbove || reason == HookReason.Follow || reason == HookReason.Route)
				{
					if (!_hkStepSkip && TryStepBack(reason, goalBottom, hp)) return;   // a player backs off from the wall to get a diagonal line over the lip
				}
				if (reason != HookReason.Rescue) HookBackoff();
				_hkStepSkip = false; return;
			}
			_hkStepSkip = false;
			SoldierHooks.PlansFound++;
			_hkPlan = plan; _hkReason = reason; _hkTrigGoalUsed = goalBottom;
			_hkChain = climbStart || (HookShortcutMode >= 1 && !altitude && (reason == HookReason.Follow || reason == HookReason.GoalAbove || reason == HookReason.Wall || reason == HookReason.Route) && bottom.Y - goalBottom.Y > info.ReachPx * 0.6f); _hkChainCount = 0; _hkChainFlight = false;
			_hkClimb = climbStart; _hkJumpKick = climbReason; _hkJumpTries = 0; _hkChainStart = Main.GameUpdateCount;
			_hkPhase = HookPhase.InFlight; _hkTicks = 0; _hkCasted = 0; _hkStall = 0; _hkLost = 0; _hkFirstLatch = -1; _hkHang = 0; _hkBestDist = float.MaxValue; _hkNetTick = 0;
			_hkProj.Clear();
			if (!HookCast(plan.Aims[0])) { HookReset(false); HookBackoff(); SoldierHooks.Aborts++; HkAborts++; return; }
			_hkCasted = 1;
			NPC.netUpdate = true;
		}

		// ---------------------------------------------------------------------------------------------
		// step back: against a tall wall the only line over the lip starts further out, as a player would back off and aim
		// ---------------------------------------------------------------------------------------------
		private float _hkStepX; private uint _hkStepUntil; private HookReason _hkStepReason; private Vector2 _hkStepGoal; private bool _hkStepSkip;
		internal int HkStepBacks;

		private bool TryStepBack(HookReason reason, Vector2 goalBottom, HookParams hp)
		{
			if (!NPC.collideY && NPC.velocity.Y != 0f) return false;
			int side = goalBottom.X >= NPC.Center.X ? -1 : 1;          // away from the obstacle, the goal side being where the wall is
			foreach (int tiles in H.StepBackTiles)
			{
				if (!StepLaneSafe(side, tiles * 16f)) break;
				Vector2 start = NPC.Bottom + new Vector2(side * tiles * 16f, 0f);
				float nd = Vector2.Distance(start, goalBottom) + Math.Abs(start.Y - goalBottom.Y) * 0.5f;
				HookPlan p = HookPlanner.Plan(TileHookWorld.Instance, start, hp, goalBottom, HookGoalKind.Ground, nd);
				if (p == null || !p.Beats(H.ArriveTiles * 16f, H.MinGainTiles * 16f)) continue;
				_hkStepX = start.X; _hkStepUntil = Main.GameUpdateCount + (uint)H.StepBackMaxTicks; _hkStepReason = reason; _hkStepGoal = goalBottom;
				HkStepBacks++;
				HookDbg($"step back {tiles} tiles to {start.X / 16f:0.0} for a plan (gain {p.Gain / 16f:0.0}T)");
				return true;
			}
			return false;
		}

		/// <summary>Walks to the step-back spot, then casts from there. True while it owns the tick.</summary>
		private bool HookStepBackTick(bool grounded)
		{
			if (_hkStepUntil == 0) return false;
			uint now = Main.GameUpdateCount;
			if (_hkPhase != HookPhase.Idle || !grounded || now > _hkStepUntil) { _hkStepUntil = 0; return false; }
			float dx = _hkStepX - NPC.Bottom.X;
			if (Math.Abs(dx) > 10f)
			{
				int dir = dx > 0f ? 1 : -1;
				NPC.direction = dir; NPC.spriteDirection = dir;
				NPC.velocity.X = dir * Math.Min(Abilities.MaxRunSpeed, 3.2f);
				return true;
			}
			NPC.velocity.X = 0f; _hkStepUntil = 0;
			if (SoldierHooks.TakePlanBudget())
			{
				_hkStepSkip = true;
				try { HookStart(_hkStepReason, _hkStepGoal); } catch (Exception) { try { HookReset(true); } catch (Exception) { } HookBackoff(); }
				_hkStepSkip = false;
			}
			return true;
		}

		private void HookBackoff()
		{
			_hkFailStreak = Math.Min(_hkFailStreak + 1, 6);
			uint wait = (uint)Math.Min(H.FailRetryTicks << (_hkFailStreak - 1), H.FailRetryMaxTicks);
			_hkNextAllowed = Main.GameUpdateCount + wait;
		}

		/// <summary>One real cast: QuickGrapple inside the frame, the cursor on the anchor. True when a hook projectile came out.</summary>
		private bool HookCast(Vector2 aim)
		{
			int before = _hkProj.Count;
			using (var f = SoldierOwner.Enter(this, new OwnerFrameOptions { Aim = aim, FullSync = true }))
			{
				if (!f.Ok) return false;
				Player d = Dummy;
				d.miscEquips[4] = Record.Gear.Hook;
				d.noItems = false; d.frozen = false; d.webbed = false; d.stoned = false; d.dead = false;
				d.direction = aim.X >= NPC.Center.X ? 1 : -1;
				d.controlHook = true; d.releaseHook = true;
				SoldierHooks.Casting = this;
				SoldierHooks.Casts++; HkCasts++;
				try { d.QuickGrapple(); }
				finally { SoldierHooks.Casting = null; d.controlHook = false; }
			}
			bool ok = _hkProj.Count > before;
			HookLog($"cast {(ok ? "ok" : "FAILED")} aim={aim.X / 16f:0.0},{aim.Y / 16f:0.0} reason={_hkReason}");
			if (ok) { SoldierHooks.CastsOk++; NPC.direction = NPC.spriteDirection = aim.X >= NPC.Center.X ? 1 : -1; }
			return ok;
		}

		// ---------------------------------------------------------------------------------------------
		// the tick
		// ---------------------------------------------------------------------------------------------
		/// <summary>Runs the hook state machine. True when it owns the tick (the walking AI is skipped).</summary>
		private bool TickHook(bool grounded)
		{
			if (_hkPhase == HookPhase.Idle) return false;
			long t0 = Stopwatch.GetTimestamp();
			bool owns;
			try { owns = TickHookCore(grounded); }
			catch (Exception e)
			{
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: hook tick failed on soldier '" + SoldierName + "', hooks released: " + e);
				try { HookReset(true); } catch (Exception) { }
				owns = false;
			}
			SoldierHooks.TickTs += Stopwatch.GetTimestamp() - t0; SoldierHooks.TickCalls++;
			return owns;
		}

		private bool TickHookCore(bool grounded)
		{
			NPC npc = NPC; uint now = Main.GameUpdateCount;
			if (_hkPhase == HookPhase.Cooldown) { if (now >= _hkNextAllowed) _hkPhase = HookPhase.Idle; return false; }
			if (!HookUsable || !IsAlive) { HookReset(true); return false; }
			// frozen, webbed or stoned: let go at once (the hook AI also kills itself, the real body is not pulled)
			if (npc.HasBuff(BuffID.Frozen) || npc.HasBuff(BuffID.Webbed) || npc.HasBuff(BuffID.Stoned))
			{
				if (_hkPhase == HookPhase.Ballistic) return false;                       // already let go: the body just falls
				HookLetGo(grounded, true); return false;
			}
			_hkTicks++;
			if ((++_hkNetTick & 3) == 0) npc.netUpdate = true; // clients see the body at the rate it is moving
			switch (_hkPhase)
			{
				case HookPhase.InFlight: return TickInFlight();
				case HookPhase.Reel: return TickReel();
				case HookPhase.Ballistic: return TickBallistic(grounded);
				default: _hkPhase = HookPhase.Idle; return false;
			}
		}

		private bool TickInFlight()
		{
			NPC npc = NPC;
			if (_hkChainFlight) { npc.noGravity = true; npc.velocity = Vector2.Zero; }
			HookGather(out int alive, out int latched);
			if (latched > 0 && _hkFirstLatch < 0) { HookLog($"latch after {_hkTicks} ticks"); _hkFirstLatch = _hkTicks; SoldierHooks.Latches++; HkLatches++; }
			if (_hkCasted < _hkPlan.Anchors.Count && (_hkTicks % H.CastSpacingTicks) == 0)
			{
				if (HookCast(_hkPlan.Aims[_hkCasted])) _hkCasted++;
				else if (_hkTicks > H.LatchTimeoutTicks / 2) _hkCasted = _hkPlan.Anchors.Count; // a second hook that will not go: reel on the first
			}
			bool allCast = _hkCasted >= _hkPlan.Anchors.Count;
			if (latched > 0 && ((allCast && latched >= alive) || (_hkFirstLatch >= 0 && _hkTicks - _hkFirstLatch >= 14) || _hkTicks >= H.LatchTimeoutTicks))
			{
				HookDbg($"latched={latched}/{alive} casts={_hkCasted}/{_hkPlan.Anchors.Count} {HookProjInfo()}");
				if (!HookReplanFromLatch()) return false;
				_hkPhase = HookPhase.Reel; _hkTicks = 0; _hkStall = 0; _hkLost = 0; _hkBestDist = float.MaxValue; _hkChainFlight = false;
				return TickReel();
			}
			if (_hkTicks >= H.LatchTimeoutTicks || (alive == 0 && allCast)) { HookLog($"abort in flight alive={alive} latched={latched}"); SoldierHooks.Aborts++; HkAborts++; HookKillAll(); HookBackoff(); npc.noGravity = false; _hkChain = false; _hkChainFlight = false; _hkPhase = HookPhase.Cooldown; _hkNextAllowed = Math.Max(_hkNextAllowed, Main.GameUpdateCount + (uint)H.CooldownTicks / 3); return false; }
			npc.velocity.X *= LevyConstants.Movement.StopDecel;           // stand while the hook flies
			if (Math.Abs(npc.velocity.X) < LevyConstants.Movement.StopBelowPx) npc.velocity.X = 0f;
			return true;
		}

		/// <summary>The hooks stuck where they stuck: when that differs from the plan (a ray that grazed an edge), the release is re-planned from the real anchors. False when they cannot do the job: let go.</summary>
		private bool HookReplanFromLatch()
		{
			var real = new List<Vector2>(2);
			for (int k = 0; k < _hkProj.Count; k++) { Projectile p = Main.projectile[_hkProj[k]]; if (p.active && p.ai[0] == 2f) real.Add(p.Center); }
			bool same = real.Count == _hkPlan.Anchors.Count;
			if (same) foreach (var a in real) { bool m = false; foreach (var b in _hkPlan.Anchors) if (Vector2.DistanceSquared(a, b) < 20f * 20f) m = true; if (!m) same = false; }
			if (same) return true;
			bool altitude = _hkPlan.Kind == HookGoalKind.Altitude;
			Vector2 goalBottom = altitude && !_hkClimb ? new Vector2(_hkAltX, _hkAltY) : _hkTrigGoalUsed;
			float nowDist = _hkClimb ? HookPlanner.ClimbDist(NPC.Bottom, goalBottom) : Vector2.Distance(NPC.Bottom, goalBottom) + Math.Abs(NPC.Bottom.Y - goalBottom.Y) * 0.5f;
			HookInfo info = HookInfo.Of(Record.Gear.Hook);
			HookParams hp = HookParams.For(info, NPC.width, NPC.height, Math.Max(5.01f, Player.jumpSpeed), Gravity()); hp.AirSteer = _hkReason == HookReason.Route; hp.Climb = _hkJumpKick; hp.KickSpeed = HookJumpSpeed;
			HookPlan np = HookPlanner.Replan(TileHookWorld.Instance, NPC.Bottom, hp, real, goalBottom, _hkPlan.Kind, nowDist);
			HookDbg($"replan from real anchors ({real.Count}): " + (np == null ? "none" : $"relDist={np.ReleaseDist:0} goalDist={np.GoalDist / 16f:0.0}T gain={np.Gain / 16f:0.0}T"));
			if (np == null || !(_hkReason == HookReason.Rescue ? np.Landed : (_hkClimb ? np.Gain >= 0f : np.Beats(H.ArriveTiles * 16f, H.MinGainTiles * 16f)))) { SoldierHooks.Aborts++; HkAborts++; HookKillAll(); HookBackoff(); NPC.noGravity = false; _hkChain = false; _hkChainFlight = false; _hkPhase = HookPhase.Cooldown; _hkNextAllowed = Math.Max(_hkNextAllowed, Main.GameUpdateCount + (uint)H.CooldownTicks / 3); return false; }
			_hkPlan = np;
			return true;
		}

		private bool TickReel()
		{
			NPC npc = NPC;
			HookGather(out int alive, out int latched);
			if (!HookCentroid(out Vector2 centroid)) // nothing latched this tick
			{
				if (++_hkLost >= 3 || alive == 0) { HookLog($"reel lost alive={alive}"); HookDbg($"reel lost the hook: alive={alive} latched={latched} {HookProjInfo()} body={npc.Center.X / 16f:0.0},{npc.Center.Y / 16f:0.0}"); HookLetGo(false, false); return npc.velocity.Y != 0f; }
				return true;
			}
			_hkLost = 0;
			_hkNetOn = true; _hkNetAnchor = centroid;
			float dist = Vector2.Distance(npc.Center, centroid);
			if (dist > _hkBestDist - H.ReelProgressPx) _hkStall++; else { _hkStall = 0; _hkBestDist = dist; }
			bool arrived = dist < 28f || _hkStall >= 6;
			if (_hkChain && (arrived || dist <= _hkPlan.ReleaseDist + H.ReleaseDistSlackPx) && TryChain(centroid)) return true;
			bool release;
			if (_hkPlan.Hang)
			{
				if (arrived) _hkHang++;
				bool keep = Main.GameUpdateCount - _hkAltSeen <= 20u;
				release = (_hkHang > 0 && !keep) || _hkHang > H.HangMaxTicks || _hkTicks > H.ReelMaxTicks + H.HangMaxTicks;
			}
			else release = dist <= _hkPlan.ReleaseDist + H.ReleaseDistSlackPx || _hkTicks >= _hkPlan.ReleaseTick + 14 || _hkStall >= H.ReelStallTicks || _hkTicks > H.ReelMaxTicks;
			if (_hkTicks > H.ReelMaxTicks + 40 + (_hkPlan.Hang ? H.HangMaxTicks : 0)) release = true;

			if (!HookReelFrame(release, centroid)) { HookLetGo(false, true); return true; }
			if (release && _hkJumpKick && dist < 24f) npc.velocity.Y = Math.Min(npc.velocity.Y, -HookJumpSpeed);   // arrived: the hook-jump, a full jump off the hook
			if (release)
			{
				_jumpsUsed = 0;                                // vanilla's release refreshes the extra jumps
				HookLog($"release dist={dist:0} planDist={_hkPlan.ReleaseDist:0} hang={_hkPlan.Hang} stall={_hkStall} ticks={_hkTicks}");
				HookDbg($"release dist={dist:0} planDist={_hkPlan.ReleaseDist:0} body={npc.Center.X / 16f:0.0},{npc.Center.Y / 16f:0.0} v={npc.velocity.X:0.0},{npc.velocity.Y:0.0}");
				SoldierHooks.Releases++; HkReleases++;
				HookKillAll();                                 // vanilla's release removed them in the frame; anything left goes now
				npc.noGravity = false; _hkNetOn = false;
				_hkPhase = HookPhase.Ballistic; _hkTicks = 0;
				npc.netUpdate = true;
			}
			else npc.noGravity = true;
			if (Math.Abs(npc.velocity.X) > 0.3f) { npc.direction = npc.spriteDirection = npc.velocity.X > 0f ? 1 : -1; }
			return true;
		}

		/// <summary>One reel tick: the real Player.GrappleMovement on the dummy standing on the soldier. On a release tick it presses jump so vanilla lets go itself.</summary>
		private bool HookReelFrame(bool release, Vector2 centroid)
		{
			using var f = SoldierOwner.Enter(this, default); // position, velocity and facing are all GrappleMovement reads
			if (!f.Ok) return false;
			Player d = Dummy;
			d.controlJump = release; d.releaseJump = release;
			d.controlLeft = d.controlRight = d.controlUp = d.controlDown = false;
			if (HookInfo.Of(Record.Gear.Hook).Kind == HookKind.Steered)
			{
				Vector2 dir = centroid - NPC.Center;
				if (dir != Vector2.Zero) dir.Normalize();
				d.controlUp = dir.Y < -0.38f; d.controlDown = dir.Y > 0.38f; d.controlLeft = dir.X < -0.38f; d.controlRight = dir.X > 0.38f;
			}
			d.GrappleMovement();
			d.grappling[0] = -1; d.grapCount = 0;                // what the end of Player.Update does every tick; without it multi-hooks pile up
			d.controlJump = false; d.controlUp = d.controlDown = d.controlLeft = d.controlRight = false;
			NPC.velocity = d.velocity;
			return true;
		}

		private bool TickBallistic(bool grounded)
		{
			NPC npc = NPC;
			if (Dummy != null) { Dummy.grappling[0] = -1; Dummy.grapCount = 0; }
			npc.noGravity = false;
			bool floor = _hkTicks >= 3 && npc.velocity.Y == 0f && FloorUnderfoot();
			if (floor || npc.wet || _hkTicks > H.BallisticMaxTicks)
			{
				HookLog(floor ? "landed" : npc.wet ? "landed wet" : "ballistic timeout"); _hkChainUsed = false; SoldierHooks.Landings++; HkLandings++; HookDbg($"landed at {NPC.Bottom.X / 16f:0.0},{NPC.Bottom.Y / 16f:0.0}");
				_hkFailStreak = 0;
				_hkPhase = HookPhase.Cooldown; _hkNextAllowed = Main.GameUpdateCount + (uint)H.CooldownTicks;
				ResetStuck(); npc.netUpdate = true;
				return false;
			}
			if (_hkChain && _hkChainUsed && _hkTicks >= 3 && Main.GameUpdateCount - _hkAirTry >= (uint)H.AirRecastSpacing && SoldierHooks.TakePlanBudget())
			{
				_hkAirTry = Main.GameUpdateCount;
				if (PlanChainLink(out HookPlan ap, out bool af)) { HookLog("air recast"); StartChainLink(ap, af); return true; }
			}
			if ((_hkChainUsed || _hkReason == HookReason.Route) && _hkTrigGoalUsed != Vector2.Zero && _jumpsUsed < Abilities.ExtraJumps && npc.velocity.Y > 0.5f && npc.Bottom.Y - _hkTrigGoalUsed.Y > 24f && Math.Abs(_hkTrigGoalUsed.X - npc.Center.X) < 10f * 16f)
			{
				// at the top of the arc and still short of the ledge: an extra jump (cloud in a bottle and the like) carries the rest
				_jumpsUsed++; npc.velocity.Y = -JumpSpeedFor(Math.Min(Abilities.JumpTiles, LevyConstants.Movement.BaseJumpTiles)) * LevyConstants.Movement.ExtraJumpSpeedMult; npc.netUpdate = true;
				HookLog("extra jump");
			}
			if ((_hkChainUsed || _hkReason == HookReason.Route) && _hkTrigGoalUsed != Vector2.Zero)
			{
				// a climber who lets go at the lip holds the direction key toward the ledge: without it the wall zeroes the sideways speed and the body drops back down
				float gx = _hkTrigGoalUsed.X - npc.Center.X;
				if (Math.Abs(gx) > 20f && npc.Bottom.Y - _hkTrigGoalUsed.Y < 12f * 16f) { npc.velocity.X = Math.Sign(gx) * Math.Min(3.4f, Math.Max(1.5f, Math.Abs(gx) / 8f)); npc.direction = npc.spriteDirection = Math.Sign(gx); return true; }
			}
			float v = npc.velocity.X;                                      // a player in the air with no input loses runSlowdown * 0.5 of x each tick
			npc.velocity.X = Math.Abs(v) > H.AirDragPx ? v - Math.Sign(v) * H.AirDragPx : 0f;
			if (Math.Abs(npc.velocity.X) > 0.3f) { npc.direction = npc.spriteDirection = npc.velocity.X > 0f ? 1 : -1; }
			return true;
		}

		private bool FloorUnderfoot()
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			int x0 = (int)(NPC.position.X / 16f), x1 = (int)((NPC.position.X + NPC.width - 1f) / 16f);
			for (int x = x0; x <= x1; x++) if (TileNav.IsFloor(x, feet.Y + 1)) return true;
			return false;
		}

		/// <summary>An early end: the hooks come off, the soldier falls (Ballistic) or stands (Cooldown).</summary>
		private void HookLetGo(bool grounded, bool forced)
		{
			HookKillAll();
			NPC.noGravity = false; _hkNetOn = false;
			if (forced) SoldierHooks.Aborts++; HkAborts++;
			if (NPC.velocity.Y != 0f && !grounded) { _hkPhase = HookPhase.Ballistic; _hkTicks = 0; }
			else { _hkPhase = HookPhase.Cooldown; _hkNextAllowed = Main.GameUpdateCount + (uint)H.CooldownTicks; }
		}

		// ---------------------------------------------------------------------------------------------
		// multiplayer draw: clients have no hook projectile, so they draw a chain from the net-synced anchor
		// ---------------------------------------------------------------------------------------------
		private void HookWriteNet(System.IO.BinaryWriter w)
		{
			bool on = _hkNetOn && _hkPhase == HookPhase.Reel;
			w.Write(on); if (on) { w.Write(_hkNetAnchor.X); w.Write(_hkNetAnchor.Y); }
		}

		private void HookReadNet(System.IO.BinaryReader r)
		{
			_hkNetOn = r.ReadBoolean();
			if (_hkNetOn) _hkNetAnchor = new Vector2(r.ReadSingle(), r.ReadSingle());
		}

		private void HookDrawChain(SpriteBatch sb, Vector2 screenPos)
		{
			if (!_hkNetOn || Main.netMode != NetmodeID.MultiplayerClient) return;
			Vector2 a = NPC.Center - screenPos, b = _hkNetAnchor - screenPos;
			Utils.DrawLine(sb, a, b, new Color(150, 150, 150), new Color(90, 90, 90), 2f);
			sb.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, new Rectangle((int)b.X - 3, (int)b.Y - 3, 6, 6), new Color(200, 200, 200));
		}
	}
}
