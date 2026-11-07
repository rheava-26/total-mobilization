using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Weapons;
using W = Levy.Common.LevyConstants.WormMomentum;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// WORM MOMENTUM. A worm head keeps its speed and bends at a bounded turn rate, so right after a pass it is committed to its heading and cannot come back quickly. A ranged levy that the head
	/// has just passed estimates "ticks until the head can threaten me again" (ThreatField.RecallTicks: the turn-circle route to where it stands) and, while that is long, it holds its ground and
	/// keeps shooting instead of chasing the head or kiting; a levy with room shifts a few tiles toward the side the head would have to turn the long way round to reach. A piercing weapon aims
	/// at the body segment behind the head whose line runs most along the shot, so the bolt passes through several segments. Nothing is keyed to a name: the trigger is a modelled chain head that is
	/// already past the levy. LEVY_WORMMOMENTUM=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static bool WormMomentumOn = Environment.GetEnvironmentVariable("LEVY_WORMMOMENTUM") != "0";
		internal static int WmThinks, WmHolds, WmShifts, WmAims, WmAimCalls, WmPierce;
		private int _wmUntil, _wmShiftUntil; private HeadInfo _wmHead;
		private Func<NPC, Vector2, NPC> _wmPrefer;

		/// <summary>The nearest head within range that is already past <paramref name="c"/> and cannot come back for the window's minimum.</summary>
		private bool WormWindow(Vector2 c, out HeadInfo head, out float ticks)
		{
			head = default; ticks = 0f;
			if (ThreatField.Snapshot() == null) return false;
			var hs = ThreatField.Heads; if (hs.Count == 0) return false;
			float rng = W.RangeTiles * LevyConstants.TilePx, bd = rng * rng; bool ok = false;
			for (int i = 0; i < hs.Count; i++)
			{
				HeadInfo h = hs[i];
				Vector2 d = c - h.P; float dd = d.LengthSquared();
				if (dd >= bd) continue;
				float sp = h.V.Length(); if (sp < 2f) continue;
				if (Vector2.Dot(d, h.V / sp) > -W.BehindMarginPx) continue;                 // the head has not passed yet
				float t = ThreatField.RecallTicks(h, c, W.TurnRate, W.MinSpeed, W.ReverseTicks);
				if (t < W.WindowMinTicks) continue;
				bd = dd; head = h; ticks = t; ok = true;
			}
			return ok;
		}

		/// <summary>True when this think's goal was set (or cleared) by the momentum rule: hold the ground and shoot, or shift a few tiles to the side the head must turn the long way round to reach.</summary>
		private bool TryWormWindow(NPC seg, float range, float standoff, int now)
		{
			if (!WormMomentumOn || !IsAlive || WantsAltitude || _aerial) return false;
			if (now < _wmShiftUntil && HasMoveGoal) { _wmUntil = now + 8; WhyB = Why.BWormWindow; return true; }
			if (!WormWindow(NPC.Center, out HeadInfo head, out float ticks)) return false;
			if (_threatIdx >= 0 && _threatTiles < standoff * 0.5f) return false;              // something is right on top of us: the ordinary rules
			float dT = Aiming.TilesToHitbox(NPC.Center, seg);
			if (dT > range * FireBuffer * LevyConstants.ShootableRangeFrac || !HasFiringLine(seg)) return false;
			_wmUntil = now + 8; _wmHead = head; WmThinks++;
			float here = Vector2.Distance(NPC.Center, head.P), bestT = ticks + W.ShiftGainTicks; Vector2 bestDest = default; bool shift = false;
			if (NPC.velocity.Y == 0f)
				for (int dir = -1; dir <= 1; dir += 2)
				{
					if (!TryFlatStep(dir, W.ShiftTiles, out Vector2 dest)) continue;
					if (Vector2.Distance(dest, head.P) < here - 16f) continue;                // never toward the head
					if (Aiming.TilesToHitbox(dest, seg) > range * 0.9f) continue;
					float t = ThreatField.RecallTicks(head, dest, W.TurnRate, W.MinSpeed, W.ReverseTicks);
					if (t > bestT) { bestT = t; bestDest = dest; shift = true; }
				}
			if (shift)
			{
				ApplyChaseClamp(ref bestDest); SetGoal(bestDest); _wmShiftUntil = now + W.ShiftHoldTicks; WmShifts++;
			}
			else
			{
				if (HasMoveGoal || _engMode == EngageMode.Retreat) ClearGoal();
				_engMode = EngageMode.Hold; WmHolds++;
			}
			WhyB = Why.BWormWindow;
			return true;
		}

		/// <summary>Is the held weapon's shot a piercing one (its projectile, or the ammo's, goes through more than one body)?</summary>
		private bool PiercingNow()
		{
			Item it = WeaponUse.HeldItem(this); if (it == null || it.IsAir) return false;
			int shoot = it.shoot;
			if (it.useAmmo > 0 && Record?.Gear?.Ammo is Item am && !am.IsAir && am.shoot > 0) shoot = am.shoot;
			if (shoot <= 0 || !Terraria.ID.ContentSamples.ProjectilesByType.TryGetValue(shoot, out Projectile p)) return false;
			return p.penetrate < 0 || p.penetrate > 1;
		}

		/// <summary>The segment a piercing shot should aim at while the window is open: behind the head, close to the nearest, and with the body's line running most along the line of fire.</summary>
		private NPC WormAimSegment(NPC target, Vector2 from)
		{
			WmAimCalls++;
			if (target == null || !PiercingNow()) return null;
			WmPierce++;
			NPC hn = Main.npc[_wmHead.Idx];
			if (!hn.active) return null;
			int hid = hn.whoAmI;
			if (ThreatField.ChainHeadOf(target.whoAmI) != hid) return null;
			Vector2 hdir = hn.velocity.LengthSquared() > 1e-4f ? Vector2.Normalize(hn.velocity) : Vector2.Zero;
			float nearest = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage || ThreatField.ChainHeadOf(i) != hid) continue;
				float d = Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(from), from);
				if (d < nearest) nearest = d;
			}
			float lim = (float)Math.Sqrt(nearest) + W.AimSlackTiles * LevyConstants.TilePx;
			NPC best = null; float bs = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage || ThreatField.ChainHeadOf(i) != hid || i == hn.whoAmI) continue;
				if (hdir != Vector2.Zero && Vector2.Dot(n.Center - hn.Center, hdir) >= 0f) continue;      // only the tail side of the head
				float d = Vector2.Distance(n.Hitbox.ClosestPointInRect(from), from);
				if (d > lim) continue;
				float align = 0f;
				if (ThreatField.IsChainFollower(n, out int par))
				{
					Vector2 tg = Main.npc[par].Center - n.Center, ln = n.Center - from;
					if (tg.LengthSquared() > 1f && ln.LengthSquared() > 1f) align = Math.Abs(Vector2.Dot(Vector2.Normalize(tg), Vector2.Normalize(ln)));
				}
				float score = d * (1f + W.AlignWeight * (1f - align));
				if (score < bs) { bs = score; best = n; }
			}
			if (best != null) WmAims++;
			return best;
		}

		/// <summary>Called around the firing code: while the window is open a piercing shot's segment choice goes through <see cref="WormAimSegment"/>.</summary>
		private void WormAimBegin(int now) { Aiming.Prefer = WormMomentumOn && now <= _wmUntil ? (_wmPrefer ??= WormAimSegment) : null; }
	}
}
