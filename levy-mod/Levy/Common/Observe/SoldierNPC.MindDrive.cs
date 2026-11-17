using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Observe;
using Levy.Common.Owner;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Mind control, direct drive. While a player drives a levy none of the levy's own movement or targeting runs: the keys go to the levy's dummy Player exactly as they go to a real one
	/// (vanilla's own UpdateJumpHeight / DashMovement / HorizontalMovement / JumpMovement / WingMovement, with the levy's boots, wings, jumps and dash), the resulting velocity goes to the body, and the
	/// body's collision does the rest. The attack button fires the weapon in hand at the cursor every tick the weapon allows, through the same real-item path a fighting levy uses.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>Counters for the audit: ticks the direct controller moved a body / fired a weapon.</summary>
		internal static long MindMoves, MindShots, MindDbgCount, MindDbgJump; internal static readonly bool MindDebug = Environment.GetEnvironmentVariable("LEVY_MIND_DEBUG") == "1";

		/// <summary>One tick of the keys: run, jump, wings, dash, platform drop and hook, the way a player's own movement does it.</summary>
		private void MindMove()
		{
			var link = MindControl.LinkOf(Record.Id);
			if (link == null || Dummy == null) return;
			MindInputState s = link.In;
			NPC npc = NPC; Player d = Dummy;
			MindMoves++;
			HasMoveGoal = false; HoldPosition = false;
			npc.noGravity = true;

			// the grapple key: a hook plan aimed at the cursor; while the hook owns the body its own controller runs and the keys wait
			bool hookEdge = s.Hook && !_mindPHook; _mindPHook = s.Hook;
			// standing: the body's engine leaves a little downward speed (its own gravity, added after the AI) on the floor, so "on the ground" is the AI's own test, not vy == 0
			bool onSlope = npc.velocity.Y > 0f && npc.velocity.Y < 2.2f && !npc.wet && Collision.SolidCollision(new Vector2(npc.position.X, npc.Bottom.Y), npc.width, 4);
			bool grounded0 = npc.velocity.Y == 0f || onSlope;
			if (hookEdge && HookUsable && _hkPhase == HookPhase.Idle) { try { HookLabStart(HookReason.GoalAbove, s.Aim); } catch (Exception e) { MindControl.Warn(e); } }
			if (_hkPhase != HookPhase.Idle)
			{
				if (s.Jump && !_mindPJump) { try { HookReset(true); } catch (Exception) { } } // a jump lets the hook go, as it does for a player
				else if (TickHook(grounded0)) { _mindPJump = s.Jump; MirrorDummyControls(); MindNet(); return; }
			}

			using (var f = SoldierOwner.Enter(this))
			{
				if (!f.Ok) { MindFallbackMove(in s); return; }
				d.position = npc.position; d.velocity = npc.velocity; d.oldVelocity = npc.oldVelocity; d.gravDir = 1f;
				if (grounded0) d.velocity.Y = 0f; // a player on the floor has no vertical speed: vanilla's jump test reads exactly that
				d.wet = npc.wet; d.lavaWet = npc.lavaWet; d.honeyWet = npc.honeyWet;
				d.controlLeft = s.Left; d.controlRight = s.Right; d.controlJump = s.Jump; d.controlDown = s.Down; d.controlUp = false;
				d.releaseLeft = !_mindPL; d.releaseRight = !_mindPR; d.releaseJump = !_mindPJump;
				d.noItems = false; d.frozen = false; d.webbed = false; d.stoned = false; d.dead = false;
				d.direction = npc.direction == 0 ? 1 : npc.direction;
				if (grounded0)
				{
					d.wingTime = d.wingTimeMax; d.rocketTime = d.rocketTimeMax;
					try { d.RefreshExtraJumps(); } catch (Exception) { }
				}

				// Player.Update scales the boots' numbers after UpdateEquips and before the movement runs (the dummy never runs Update): the same scaling, undone after the call
				float maxRun0 = d.maxRunSpeed, accRun0 = d.accRunSpeed, accel0 = d.runAcceleration, slow0 = d.runSlowdown;
				try
				{
					global::Levy.Common.Movement.MovementAbilities.ApplyCalamityRunBonus(d);
					d.runAcceleration *= d.moveSpeed; d.maxRunSpeed *= d.moveSpeed;
					if (d.hasMagiluminescence) { d.runAcceleration *= 1.75f; d.maxRunSpeed *= 1.15f; d.accRunSpeed *= 1.15f; d.runSlowdown *= 1.75f; }
					if (d.shadowArmor) { d.runAcceleration *= 1.75f; d.maxRunSpeed *= 1.15f; d.accRunSpeed *= 1.15f; d.runSlowdown *= 1.75f; }
					try { PlayerLoader.PostUpdateRunSpeeds(d); } catch (Exception e) { MindControl.Warn(e); }
					if (MindDebug && MindDbgCount++ % 200 == 0) Levy.Instance.Logger.Info($"[MindDbg] after scaling: maxRun={d.maxRunSpeed} accRun={d.accRunSpeed} accel={d.runAcceleration} slow={d.runSlowdown} (raw {maxRun0}/{accRun0}/{accel0}/{slow0}) moveSpeed={d.moveSpeed} calMsb={CalMoveBonus(d)}");
					// wings in the air: their own horizontal speed (what Player.Update applies for the length of the flight)
					if (d.wingsLogic > 0 && !grounded0 && s.Jump && d.wingTime > 0f)
					{
						float over = d.GetWingStats(d.wingsLogic).AccRunSpeedOverride;
						if (over > 0f) d.accRunSpeed = Math.Max(d.accRunSpeed, over * (1f + 0.16f * global::Levy.Common.Movement.MovementAbilities.CalMoveBonus(d)));
					}
					d.UpdateJumpHeight();
					if (!_mindDashBroken)
					{
						// vanilla's dash (double tap, or the key Calamity swaps in for it). A server has no keybind profile for Calamity's lookup: that throws, and the double tap below stands in
						// Calamity swaps the double tap for a keybind for whoever is Main.myPlayer: the dummy stands in as a remote player for this one call, so the double tap works as it does for anyone else
						int myp = Main.myPlayer;
						try { Main.myPlayer = (d.whoAmI + 1) % 250; d.DashMovement(); } catch (Exception) { _mindDashBroken = true; } finally { Main.myPlayer = myp; }
					}
					d.HorizontalMovement();
					if (_mindDashBroken) MindDoubleTapDash(in s, grounded0, d);
					float vy0 = d.velocity.Y;
					d.JumpMovement();
					float vy1 = d.velocity.Y;
					if (d.wingsLogic > 0 || d.rocketBoots > 0) d.WingMovement(); // with neither it still pulls a rising body up 0.1 a tick, which Player.Update never lets it do
					if (MindDebug && MindDbgJump < 14 && (s.Jump || vy1 < -0.5f)) Levy.Instance.Logger.Info($"[MindDbg] vy before jump code {vy0:0.000}, after JumpMovement {vy1:0.000}, after WingMovement {d.velocity.Y:0.000} gravity={d.gravity} slowFall={d.slowFall} gravDir={d.gravDir} jumpSpeedBoost={d.jumpSpeedBoost} dead={d.dead} wet={d.wet}");
				}
				finally { d.maxRunSpeed = maxRun0; d.accRunSpeed = accRun0; d.runAcceleration = accel0; d.runSlowdown = slow0; }

				// gravity: Player.Update adds a player's after the jump and wing code (a held jump is -jumpSpeed + gravity each tick, so a jump of 5.71 reads -5.31). The body's engine gravity is off while she drives (noGravity), so this is the only one
				float pg = npc.wet ? 0.2f : d.gravity, fall = npc.wet ? 5f : d.maxFallSpeed;
				float vyBefore = d.velocity.Y;
				d.velocity.Y += pg;
				if (d.velocity.Y > fall) d.velocity.Y = fall;
				if (MindDebug && s.Jump && MindDbgJump++ < 14) Levy.Instance.Logger.Info($"[MindDbg] jump tick: grounded0={grounded0} vy in {npc.velocity.Y:0.000} after movement {vyBefore:0.000} +g {pg} -> {d.velocity.Y:0.000} noGravity={npc.noGravity} jump={d.jump} jumpS={Player.jumpSpeed}");
				npc.velocity = d.velocity;
				d.velocity = npc.velocity;
				if (d.direction != 0) { npc.direction = d.direction; npc.spriteDirection = d.direction; }
			}
			_mindPL = s.Left; _mindPR = s.Right; _mindPJump = s.Jump;
			WantsToFallThrough = s.Down; if (s.Down) _fallThroughTicks = 2;
			StepUp();
			MirrorDummyControlsMind(in s);
			MindNet();
		}

		private static System.Reflection.FieldInfo _calMsb; private static bool _calMsbTried;
		/// <summary>Calamity's own run-speed bonus on this player (CalamityPlayer.moveSpeedBonus), 0 without Calamity.</summary>
		internal static float CalMoveBonus(Player p)
		{
			try
			{
				object cp = AltPolicies.CalamityPlayerOf(p); if (cp == null) return 0f;
				if (!_calMsbTried) { _calMsbTried = true; _calMsb = cp.GetType().GetField("moveSpeedBonus"); }
				return _calMsb == null ? 0f : Convert.ToSingle(_calMsb.GetValue(cp));
			}
			catch (Exception) { return 0f; }
		}

		private bool _mindDashBroken; private int _mindTapDir, _mindDashLeft; private uint _mindTapAt;

		/// <summary>Double tap a direction with a dash accessory on: the body bursts at the dash speed for the dash's length, as the player's dash does.</summary>
		private void MindDoubleTapDash(in MindInputState s, bool grounded, Player d)
		{
			if (_dashCooldown > 0) _dashCooldown--;
			int tap = s.Right && !_mindPR ? 1 : s.Left && !_mindPL ? -1 : 0;
			uint now = Main.GameUpdateCount;
			if (tap != 0)
			{
				if (_mindTapDir == tap && now - _mindTapAt <= 15u && Abilities != null && Abilities.HasDash && grounded && _dashCooldown <= 0)
				{
					_mindDashLeft = DashBurstTicks; _dashDir = tap; _dashCooldown = DashCooldownTicks; _mindTapDir = 0;
				}
				else { _mindTapDir = tap; _mindTapAt = now; }
			}
			if (_mindDashLeft > 0)
			{
				bool hit = DashBurstTicks - _mindDashLeft >= 2 && Math.Abs(d.velocity.X) < DashSpeedPx * 0.4f;
				if (hit || NPC.wet) _mindDashLeft = 0;
				else { _mindDashLeft--; d.velocity.X = _dashDir * DashSpeedPx; }
			}
		}

		/// <summary>The dummy's frame could not open (no free owner slot): run, jump and stop with the plain numbers rather than stand frozen.</summary>
		private void MindFallbackMove(in MindInputState s)
		{
			NPC.velocity.Y += NPC.wet ? 0.2f : 0.4f;
			int dir = (s.Right ? 1 : 0) - (s.Left ? 1 : 0);
			float top = Abilities?.MaxRunSpeed ?? 3f;
			if (dir != 0) { NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X + dir * 0.08f, -top, top); NPC.direction = NPC.spriteDirection = dir; }
			else NPC.velocity.X *= 0.8f;
			if (s.Jump && NPC.velocity.Y == 0f) NPC.velocity.Y = -JumpSpeedFor(Math.Max(1, Abilities?.JumpTiles ?? 5));
			_mindPL = s.Left; _mindPR = s.Right; _mindPJump = s.Jump;
		}

		private void MirrorDummyControlsMind(in MindInputState s)
		{
			Dummy.velocity = NPC.velocity;
			Dummy.controlLeft = s.Left; Dummy.controlRight = s.Right; Dummy.controlJump = s.Jump || NPC.velocity.Y < 0f;
		}

		/// <summary>The body streams to the player's client every tick while it is driven (a levy normally sends at the vanilla cadence).</summary>
		private void MindNet()
		{
			if (Main.netMode == NetmodeID.Server) { NPC.netUpdate = true; NPC.netSpam = 0; }
		}

		// ------------------------------------------------------------------------------------------------------------
		// attack
		// ------------------------------------------------------------------------------------------------------------

		/// <summary>The enemy under the cursor (a hit-box the cursor is inside of, with a little give), else null.</summary>
		private int MindEnemyAt(Vector2 aim)
		{
			int best = -1; float bd = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				Rectangle hb = n.Hitbox; hb.Inflate(24, 24);
				if (!hb.Contains((int)aim.X, (int)aim.Y)) continue;
				float d = Vector2.DistanceSquared(n.Center, aim);
				if (d < bd) { bd = d; best = i; }
			}
			return best;
		}

		/// <summary>One tick of the attack buttons: the hotbar keys pick the hand, the attack button fires at the cursor whenever the weapon is ready, with no range, line-of-sight or target gate (a player has none).</summary>
		private void MindFire()
		{
			var link = MindControl.LinkOf(Record.Id);
			if (link == null) return;
			MindInputState s = link.In;
			// hotbar: 1 the main weapon, 2 the sidearm
			if (s.Slot != 0 && s.Slot - 1 != MindSide && !(s.Slot == 2 && !Record.Gear.HasSidearm)) { MindSide = s.Slot - 1; UpdateSidearm(); }
			bool engaged = s.Use || s.Alt;
			State = engaged ? SoldierState.Engage : SoldierState.Follow; StateTimer = 0;
			int idx = MindEnemyAt(s.Aim);
			TargetIndex = idx;
			if (engaged || ItemAnimation > 0)
			{
				if (!SwingFacingLocked && Math.Abs(s.Aim.X - NPC.Center.X) > 2f) NPC.direction = s.Aim.X < NPC.Center.X ? -1 : 1;
			}
			if (Weapon == null || !Weapon.Supported) return;
			NPC target = idx >= 0 ? Main.npc[idx] : SoldierTargeting.FocusPhantom(s.Aim, NPC.Center);
			if (Weapon.Family == WeaponFamily.Broadsword && ItemAnimation > 0) ProcessSwing(target);
			if (!s.Use || Weapon.IsDeployer) return;
			float prev = Aiming.ExtraErrorScale;
			Aiming.ExtraErrorScale = 0f; // the cursor is where the shot goes: no grade wobble, no lead error
			try { if (WeaponUse.TryAttack(this, target)) MindShots++; }
			catch (Exception e) { MindControl.Warn(e); }
			finally { Aiming.ExtraErrorScale = prev; }
		}
	}
}
