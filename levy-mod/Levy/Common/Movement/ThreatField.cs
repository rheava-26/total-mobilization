using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Common.Movement
{
	/// <summary>A hostile projectile that is a long thin line (a deathray) rather than a point. A ModProjectile implements it; Moon Lord's ray is read directly.</summary>
	public interface IThreatBeam
	{
		/// <summary>Length (px) of the line from the projectile centre along its velocity direction.</summary>
		float BeamLength(Projectile p);
		/// <summary>The line width argument the projectile's own Colliding passes to Collision.CheckAABBvLineCollision.</summary>
		float BeamWidth(Projectile p);
	}

	/// <summary>One thing that can hurt a soldier, as a motion: a point or body moving on a predicted path, or a line that sweeps.</summary>
	public struct Threat
	{
		public Vector2 P, V, A, Half;
		public float Weight;
		public bool Line; public float Ang, Omega, Len, Width;
		public bool Body;
	}

	/// <summary>A dodge candidate: horizontal velocity for Dur ticks, an optional jump (Delay ticks from now: a hop timed to land after a charge has passed under it), an optional platform drop.</summary>
	public struct Maneuver
	{
		public float Vx; public int Dur, Delay; public float JumpVy; public bool Drop; public byte Kind; // 0 stay, 1 step, 2 jump, 3 step+jump, 4 dash, 5 drop
		public float Penalty;
	}

	/// <summary>
	/// THREAT FIELD. One shared snapshot per tick of everything hostile that moves (projectiles, fast bodies, beams), and the prediction a soldier uses to choose where to be.
	/// The method is general: every threat is a path through the next few dozen ticks (a point or body on its velocity plus the acceleration it is showing, or a line that sweeps at the
	/// angular rate it has been turning), a candidate manoeuvre is a path for the soldier, and the manoeuvre with the fewest predicted overlaps wins. Nothing is keyed to an enemy name.
	/// </summary>
	public static class ThreatField
	{
		private static uint _tick = uint.MaxValue;
		private static readonly List<Threat> Snap = new(256);
		private static readonly float[] PrevAng = new float[Main.maxProjectiles];
		private static readonly uint[] PrevAngTick = new uint[Main.maxProjectiles];
		private static readonly Vector2[] PrevVel = new Vector2[Main.maxNPCs + 1];
		private static readonly uint[] PrevVelTick = new uint[Main.maxNPCs + 1];
		public static long BuildTs, EvalTs; public static int Builds, Evals, EvalSamples;

		public static float BodyMarginPx = 10f;
		public const float ChargeMinSpeed = 4.5f, ChargeAccel = 0.25f, ChargeTopSpeed = 20f, MaxBuildTicks = 60f;

		public static List<Threat> Snapshot()
		{
			uint now = Main.GameUpdateCount;
			if (_tick == now) return Snap;
			_tick = now;
			long t0 = Stopwatch.GetTimestamp();
			Snap.Clear();
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.hostile || p.damage < LevyConstants.Movement.DodgeMinDamage) continue;
				if (BeamOf(p, out float len, out float width))
				{
					Vector2 d = p.velocity; if (d == Vector2.Zero || d.HasNaNs()) continue;
					float ang = d.ToRotation(), omega = 0f;
					if (PrevAngTick[i] != 0 && now - PrevAngTick[i] <= 8u && now > PrevAngTick[i]) omega = MathHelper.WrapAngle(ang - PrevAng[i]) / (now - PrevAngTick[i]);
					PrevAng[i] = ang; PrevAngTick[i] = now;
					Snap.Add(new Threat { P = p.Center, Line = true, Ang = ang, Omega = omega, Len = len, Width = width, Weight = p.damage });
					continue;
				}
				Vector2 v = p.velocity;
				if (v.LengthSquared() < LevyConstants.Movement.DodgeMinProjectileSpeed * LevyConstants.Movement.DodgeMinProjectileSpeed) continue;
				Snap.Add(new Threat { P = p.Center, V = v * (1f + p.extraUpdates), Half = new Vector2(p.width, p.height) * 0.5f, Weight = p.damage });
			}
			float minSq = LevyConstants.Movement.DodgeNpcMinSpeed * LevyConstants.Movement.DodgeNpcMinSpeed;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.damage < LevyConstants.Movement.DodgeNpcMinDamage || n.ModNPC is Content.NPCs.SoldierNPC) continue;
				Vector2 v = n.velocity; float v2 = v.LengthSquared();
				// the acceleration it is showing: how its velocity changed since the last time the field looked (NPC.oldVelocity is not kept for every NPC)
				Vector2 a = Vector2.Zero;
				if (PrevVelTick[i] != 0 && now > PrevVelTick[i] && now - PrevVelTick[i] <= 8u) a = (n.velocity - PrevVel[i]) / (now - PrevVelTick[i]);
				PrevVel[i] = n.velocity; PrevVelTick[i] = now;
				// a charge shows itself as speed, or as speed that is still building along the direction of travel
				bool building = v2 > 2.5f * 2.5f && Vector2.Dot(a, v) > ChargeAccel * Math.Sqrt(v2);
				if (v2 < minSq && !building) continue;
				Snap.Add(new Threat { P = n.Center, V = v, A = building ? a : Vector2.Zero, Half = new Vector2(n.width, n.height) * 0.5f, Weight = n.damage, Body = true });
			}
			BuildTs += Stopwatch.GetTimestamp() - t0; Builds++;
			return Snap;
		}

		private static bool BeamOf(Projectile p, out float len, out float width)
		{
			len = width = 0f;
			if (p.ModProjectile is IThreatBeam b) { len = b.BeamLength(p); width = b.BeamWidth(p); return len > 0f; }
			if (p.type == ProjectileID.PhantasmalDeathray) { len = p.localAI[1]; width = 36f * p.scale; return len > 0f; }
			return false;
		}

		/// <summary>Keeps the threats that can come within <paramref name="reach"/> px of <paramref name="c"/> inside <paramref name="horizon"/> ticks.</summary>
		public static void Near(List<Threat> all, Vector2 c, float reach, int horizon, List<Threat> into)
		{
			into.Clear();
			for (int i = 0; i < all.Count; i++)
			{
				Threat t = all[i];
				if (t.Line) { if (Vector2.DistanceSquared(t.P, c) <= (t.Len + reach) * (t.Len + reach)) into.Add(t); continue; }
				Vector2 end = ThreatAt(t, horizon);
				float r = reach + Math.Max(t.Half.X, t.Half.Y);
				if (SegDistSq(t.P, end, c) <= r * r) into.Add(t);
			}
		}

		private static float SegDistSq(Vector2 a, Vector2 b, Vector2 p)
		{
			Vector2 ab = b - a; float l2 = ab.LengthSquared();
			float u = l2 < 1e-4f ? 0f : MathHelper.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
			return Vector2.DistanceSquared(a + ab * u, p);
		}

		private static Vector2 BodyAt(Vector2 c0, in Maneuver m, float g, int t)
		{
			float x = c0.X + m.Vx * Math.Min(t, m.Dur), y = c0.Y;
			if (m.JumpVy < 0f) { int tj = t - m.Delay; if (tj > 0) { float d = m.JumpVy * tj + 0.5f * g * tj * tj; if (d < 0f) y += d; } }
			else if (m.Drop && t > 3) y += 0.5f * g * (t - 3) * (t - 3);
			return new Vector2(x, y);
		}

		private static Vector2 ThreatAt(in Threat th, int t)
		{
			Vector2 p = th.P + th.V * t;
			if (th.A != Vector2.Zero)
			{
				// a charge that is still building speed keeps building it until it reaches a charging body's top speed (it is not known; ChargeTopSpeed is a pessimistic one)
				float aLong = th.V.LengthSquared() > 1e-4f ? Vector2.Dot(th.A, Vector2.Normalize(th.V)) : th.A.Length();
				float tc = aLong > 0.01f ? Math.Min(MaxBuildTicks, Math.Max(0f, (ChargeTopSpeed - th.V.Length()) / aLong)) : 12f;
				float ta = Math.Min(t, tc);
				p += th.A * (0.5f * ta * ta + ta * Math.Max(0f, t - ta));
			}
			return p;
		}

		/// <summary>Counts the threats that overlap the body on this manoeuvre inside the horizon. <paramref name="lastHit"/> is the tick of the latest overlap.</summary>
		public static int Hits(List<Threat> near, Vector2 c0, float bw, float bh, in Maneuver m, float g, int horizon, out int firstHit, out int lastHit)
		{
			long t0 = Stopwatch.GetTimestamp();
			int hits = 0; firstHit = int.MaxValue; lastHit = -1;
			Vector2 bHalf = new Vector2(bw, bh) * 0.5f;
			for (int k = 0; k < near.Count; k++)
			{
				Threat th = near[k];
				int hit = -1, lineTicks = 0, lineLast = -1;
				if (th.Line)
				{
					for (int t = 0; t <= horizon; t++)
					{
						Vector2 b = BodyAt(c0, m, g, t); float ang = th.Ang + th.Omega * t;
						Vector2 end = th.P + ang.ToRotationVector2() * th.Len; float f = 0f;
						EvalSamples++;
						if (Collision.CheckAABBvLineCollision(b - bHalf, new Vector2(bw, bh), th.P, end, th.Width, ref f)) { if (hit < 0) hit = t; lineTicks++; lineLast = t; }
					}
				}
				else
				{
					Vector2 half = th.Half + bHalf + (th.Body ? new Vector2(BodyMarginPx, BodyMarginPx) : new Vector2(2f));   // a prediction of a charging body is never exact: keep a margin around it
					Vector2 prev = ThreatAt(th, 0) - BodyAt(c0, m, g, 0);
					if (Math.Abs(prev.X) <= half.X && Math.Abs(prev.Y) <= half.Y) hit = 0;
					for (int t = 2; t <= horizon && hit < 0; t += 2)
					{
						Vector2 cur = ThreatAt(th, t) - BodyAt(c0, m, g, t);
						EvalSamples++;
						if (SegHitsBox(prev, cur, half)) hit = t;
						prev = cur;
					}
				}
				if (hit >= 0) { hits += 1 + lineTicks / 12; if (hit < firstHit) firstHit = hit; if (hit > lastHit) lastHit = hit; if (lineLast > lastHit) lastHit = lineLast; }
			}
			EvalTs += Stopwatch.GetTimestamp() - t0; Evals++;
			return hits;
		}

		/// <summary>Does the segment a-b cross the box centred on the origin with these half extents (slab test).</summary>
		private static bool SegHitsBox(Vector2 a, Vector2 b, Vector2 h)
		{
			float t0 = 0f, t1 = 1f;
			Vector2 d = b - a;
			for (int ax = 0; ax < 2; ax++)
			{
				float s = ax == 0 ? a.X : a.Y, dd = ax == 0 ? d.X : d.Y, hh = ax == 0 ? h.X : h.Y;
				if (Math.Abs(dd) < 1e-5f) { if (Math.Abs(s) > hh) return false; continue; }
				float u0 = (-hh - s) / dd, u1 = (hh - s) / dd;
				if (u0 > u1) (u0, u1) = (u1, u0);
				t0 = Math.Max(t0, u0); t1 = Math.Min(t1, u1);
				if (t0 > t1) return false;
			}
			return true;
		}
	}
}
