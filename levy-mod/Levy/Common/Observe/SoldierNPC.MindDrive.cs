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
		internal static long MindMoves, MindShots;

		/// <summary>One tick of the keys: run, jump, wings, dash, platform drop and hook, the way a player's own movement does it.</summary>
		private void MindMove()
		{
			var link = MindControl.LinkOf(Record.Id);
			if (link == null || Dummy == null) return;
			MindInputState s = link.In;
			NPC npc = NPC; Player d = Dummy;
			MindMoves++;
			HasMoveGoal = false; HoldPosition = false;

			// the grapple key: a hook plan aimed at the cursor; while the hook owns the body its own controller runs and the keys wait
			bool hookEdge = s.Hook && !_mindPHook; _mindPHook = s.Hook;
			bool grounded0 = npc.velocity.Y == 0f;
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
					d.runAcceleration *= d.moveSpeed; d.maxRunSpeed *= d.moveSpeed;
					if (d.hasMagiluminescence) { d.runAcceleration *= 1.75f; d.maxRunSpeed *= 1.15f; d.accRunSpeed *= 1.15f; d.runSlowdown *= 1.75f; }
					if (d.shadowArmor) { d.runAcceleration *= 1.75f; d.maxRunSpeed *= 1.15f; d.accRunSpeed *= 1.15f; d.runSlowdown *= 1.75f; }
					try { PlayerLoader.PostUpdateRunSpeeds(d); } catch { }
					d.UpdateJumpHeight();
					d.DashMovement();
					d.HorizontalMovement();
					d.JumpMovement();
					d.WingMovement();
				}
				finally { d.maxRunSpeed = maxRun0; d.accRunSpeed = accRun0; d.runAcceleration = accel0; d.runSlowdown = slow0; }

				// gravity: the body's engine adds its own after this tick's AI, so the difference to a player's is made up here (a held jump has none)
				float ng = Gravity(), pg = npc.wet ? 0.2f : d.gravity;
				if (d.jump > 0) pg = 0f;
				d.velocity.Y += pg - ng;
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

		/// <summary>The dummy's frame could not open (no free owner slot): run, jump and stop with the plain numbers rather than stand frozen.</summary>
		private void MindFallbackMove(in MindInputState s)
		{
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
