using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common;
using Levy.Common.Util;
using Levy.Content.Buffs;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FIZZY BODY (Fizz Can, Case of Fizz). The buff gives the body a quarter of its gravity (ProvisionsBody), a double jump and six times the knockback; this file is the rest:
	/// <list type="bullet">
	/// <item>A hit on a Fizzy body adds <see cref="LevyConstants.FizzKnockbackMult"/> times the knockback, and knockback immunity worn on the dummy (Cobalt Shield and the like) does not stop it: it is paid back as
	/// <see cref="LevyConstants.FizzImmunityDr"/> damage reduction (the hit "diverts all its energy into momentum").</item>
	/// <item>A knocked body FLIES: the launch is capped (vanilla's own 16 px/tick would be a rise of 60 tiles in a quarter gravity), the movement code stays off while it is in the air so the AI cannot
	/// cancel the momentum, walls and ceilings and floors give a bounce back, a floor slides it to a stop, and the flight ends once it has settled (grounded and slow), touched water or lava, or run 7 seconds.
	/// Combat keeps running; only the movement is suspended.</item>
	/// <item>No fall damage while Fizzy (the fall counter is held at the current row), whatever the setting.</item>
	/// <item>Each double jump puffs a small cloud (the Cloud in a Bottle's dust and smoke gore).</item>
	/// </list>
	/// </summary>
	public partial class SoldierNPC
	{
		private int _fzFlight, _fzAge; private Vector2 _fzPrevV;
		/// <summary>Harness telemetry: flights started, flights that ended by settling / by the time limit / by water.</summary>
		internal static int FizzFlights, FizzSettled, FizzTimedOut, FizzWetEnd;
		/// <summary>True while a knocked Fizzy body is in the air or sliding (the movement code is off).</summary>
		internal bool FizzFlying => _fzFlight > 0;

		private ProvisionsBody _fzBody;
		/// <summary>The body carries Fizzy (read from the buff list; the Provisions state caches it each tick).</summary>
		internal bool FizzyNow
		{
			get
			{
				if (NPC == null || !NPC.active) return false;
				_fzBody ??= NPC.TryGetGlobalNPC(out ProvisionsBody b) ? b : null;
				return _fzBody != null && (_fzBody.Fizzy || NPC.HasBuff(FizzyType()));
			}
		}
		private static int FizzyType() => Terraria.ModLoader.ModContent.BuffType<FizzyBuff>();

		/// <summary>ModifyIncomingHit: six times the knockback, and the dummy's knockback immunity becomes damage reduction.</summary>
		private void Fizz_ModifyHit(ref NPC.HitModifiers modifiers)
		{
			if (!FizzyNow) return;
			modifiers.Knockback *= LevyConstants.FizzKnockbackMult;
			if (Dummy != null && Dummy.noKnockback) modifiers.FinalDamage *= 1f - LevyConstants.FizzImmunityDr;
		}

		/// <summary>HitEffect (every machine, after vanilla set the knockback velocity): cap the launch; on the server start the flight.</summary>
		private void Fizz_OnHit(NPC.HitInfo hit)
		{
			if (OfficerMode || hit.Knockback <= 0f || !FizzyNow) return;
			NPC npc = NPC;
			npc.velocity.X = MathHelper.Clamp(npc.velocity.X, -LevyConstants.FizzMaxLaunchX, LevyConstants.FizzMaxLaunchX);
			if (npc.velocity.Y < -LevyConstants.FizzMaxLaunchUp) npc.velocity.Y = -LevyConstants.FizzMaxLaunchUp;
			if (!LevyUtil.IsServerOrSingle || !IsAlive) return;
			if (_fzFlight == 0)
			{
				FizzFlights++;
				try { if (_hkPhase != HookPhase.Idle) HookReset(true); } catch (Exception) { }
				_clmOn = false; WantsToFallThrough = false;
			}
			_fzFlight = LevyConstants.FizzFlightMaxTicks; _fzAge = 0; _fzPrevV = npc.velocity;
			npc.netUpdate = true;
		}

		/// <summary>
		/// One tick of a knocked body's flight, in place of the movement code. True: the flight owns this tick (the caller skips Movement_Update). The momentum is left alone except for the bounce off whatever
		/// the last move ran into, the slide on a floor and a light air drag.
		/// </summary>
		private bool Fizz_Flight()
		{
			if (_fzFlight <= 0) return false;
			NPC npc = NPC;
			if (!IsAlive || !FizzyNow) { _fzFlight = 0; return false; }
			if (npc.wet || npc.lavaWet || npc.honeyWet) { FizzWetEnd++; _fzFlight = 0; return false; }
			_fzFlight--; _fzAge++;
			// what the last move ran into (vanilla set collideX/collideY after it and zeroed that component); _fzPrevV is the velocity it moved with, gravity not yet added
			bool floor = false;
			if (npc.collideX && Math.Abs(_fzPrevV.X) > 1.5f) npc.velocity.X = -_fzPrevV.X * LevyConstants.FizzBounceWall;
			if (npc.collideY)
			{
				if (_fzPrevV.Y > 1.5f) { npc.velocity.Y = -_fzPrevV.Y * LevyConstants.FizzBounceFloor; floor = true; }
				else if (_fzPrevV.Y < -1.5f) npc.velocity.Y = -_fzPrevV.Y * LevyConstants.FizzBounceWall;
				else if (_fzPrevV.Y >= -0.5f) floor = true;
			}
			if (floor) npc.velocity.X *= LevyConstants.FizzGroundSlide; else npc.velocity.X *= LevyConstants.FizzAirDrag;
			if (npc.velocity.X != 0f) { int face = npc.velocity.X > 0f ? 1 : -1; npc.direction = face; npc.spriteDirection = face; }
			if (floor && _fzAge > 6 && Math.Abs(npc.velocity.X) < LevyConstants.FizzSettleSpeed && Math.Abs(npc.velocity.Y) < LevyConstants.FizzSettleSpeed) { FizzSettled++; _fzFlight = 0; npc.velocity.X = 0f; return false; }
			if (_fzFlight == 0) FizzTimedOut++;
			AirLavaGuard(); // a flight toward lava still steers away (the same saving throw as any fall)
			_fzPrevV = npc.velocity;
			MirrorDummyControls();
			return true;
		}

		/// <summary>A little cloud at the feet for a double jump: the Cloud in a Bottle's smoke dust and the three smoke gores.</summary>
		private void FizzPuff()
		{
			NPC npc = NPC;
			for (int i = 0; i < 8; i++)
			{
				int d = Dust.NewDust(new Vector2(npc.position.X - 4f, npc.position.Y + npc.height - 2f), npc.width + 8, 4, DustID.Smoke, -npc.velocity.X * 0.5f, npc.velocity.Y * 0.5f, 100, default, 1.5f);
				Main.dust[d].velocity.X = Main.dust[d].velocity.X * 0.5f - npc.velocity.X * 0.1f;
				Main.dust[d].velocity.Y = Main.dust[d].velocity.Y * 0.5f - npc.velocity.Y * 0.3f;
			}
			for (int i = -1; i <= 1; i += 2)
			{
				int g = Gore.NewGore(npc.GetSource_FromAI(), new Vector2(npc.Center.X - 8f + i * 8f, npc.Bottom.Y - 4f), new Vector2(-npc.velocity.X * 0.3f + i * 0.8f, 0.4f), Main.rand.Next(11, 14));
				if (g >= 0 && g < Main.maxGore) { Main.gore[g].velocity *= 0.5f; Main.gore[g].velocity.X += i * 0.4f; }
			}
		}
	}
}
