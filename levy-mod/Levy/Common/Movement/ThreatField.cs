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
		/// <summary>A boss body: its hits count several times over, its margin is wider, and a hopper in the air keeps falling (<see cref="Gy"/>) until the floor (<see cref="Floor"/>, px, 0 = none).</summary>
		public bool Boss; public float Gy, Floor;
	}

	/// <summary>A dodge candidate: horizontal velocity for Dur ticks, an optional jump (Delay ticks from now: a hop timed to land after a charge has passed under it), an optional platform drop.</summary>
	public struct Maneuver
	{
		public float Vx; public int Dur, Delay; public float JumpVy; public bool Drop; public byte Kind; // 0 stay, 1 step, 2 jump, 3 step+jump, 4 dash, 5 drop, 6 full jump, 7 double jump, 8 dash+jump, 9 hook to a ledge, 10 dash+drop
		public float Penalty;
		/// <summary>A second jump in the air: impulse (negative = up) taken <see cref="J2At"/> ticks after the first leaves the ground.</summary>
		public float J2Vy; public int J2At;
		/// <summary>A hook to a ledge: after a cast delay the body moves in a line to <see cref="HookTarget"/> over <see cref="HookDur"/> ticks.</summary>
		public bool Hook; public Vector2 HookTarget; public int HookDur;
		/// <summary>An airborne start: the body already moves at (<see cref="V0x"/>, <see cref="V0y"/>) and falls until <see cref="FloorY"/> (centre Y of the landing). <see cref="J2Vy"/> (up negative, down positive) is a kick taken <see cref="J2At"/> ticks from now; Kind 11 = air jump / wing flap, 12 = wing or rocket thrust, 13 = fast fall.</summary>
		public bool Air; public float V0x, V0y, FloorY, AirAcc;
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

		public static float BodyMarginPx = 10f, BossBodyMarginPx = 22f;
		public const int BossHitWeight = 3;
		/// <summary>Number of boss bodies the field saw this tick (a fight is on).</summary>
		public static int BossBodies;
		public const float ChargeMinSpeed = 4.5f, ChargeAccel = 0.25f, ChargeTopSpeed = 20f, MaxBuildTicks = 60f;

		public static List<Threat> Snapshot()
		{
			uint now = Main.GameUpdateCount;
			if (_tick == now) return Snap;
			_tick = now;
			long t0 = Stopwatch.GetTimestamp();
			Snap.Clear(); BossBodies = 0;
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
			float minSq = LevyConstants.Movement.DodgeNpcMinSpeed * LevyConstants.Movement.DodgeNpcMinSpeed, heavyMinSq = LevyConstants.Movement.DodgeHeavyMinSpeed * LevyConstants.Movement.DodgeHeavyMinSpeed;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.damage < (Content.NPCs.SoldierNPC.MobilityMode >= 1 ? LevyConstants.Movement.DodgeNpcMinDamageMobile : LevyConstants.Movement.DodgeNpcMinDamage) || n.ModNPC is Content.NPCs.SoldierNPC) continue;
				Vector2 v = n.velocity; float v2 = v.LengthSquared();
				// the acceleration it is showing: how its velocity changed since the last time the field looked (NPC.oldVelocity is not kept for every NPC)
				Vector2 a = Vector2.Zero;
				if (PrevVelTick[i] != 0 && now > PrevVelTick[i] && now - PrevVelTick[i] <= 8u) a = (n.velocity - PrevVel[i]) / (now - PrevVelTick[i]);
				PrevVel[i] = n.velocity; PrevVelTick[i] = now;
				// a charge shows itself as speed, or as speed that is still building along the direction of travel
				bool building = v2 > 2.5f * 2.5f && Vector2.Dot(a, v) > ChargeAccel * Math.Sqrt(v2);
				// a boss or a hard hitter that merely walks at a levy is still a body to step out of (a player does not stand and trade with a Deerclops); ordinary walkers stay below the filter
				bool heavy = Content.NPCs.SoldierNPC.MobilityMode >= 1 && (n.damage >= LevyConstants.Movement.DodgeHeavyDamage || LevyUtil.IsBossNpc(n));
				if (v2 < (heavy ? heavyMinSq : minSq) && !building) continue;
				bool isBoss = Content.NPCs.SoldierNPC.DanceMode >= 1 && Content.NPCs.SoldierNPC.DOn(1) && LevyUtil.IsBossNpc(n);
				float gy = 0f, floor = 0f;
				if (isBoss) BossBodies++;
				// a boss that hops (a slime) is in a ballistic arc: it keeps falling until the floor, so the landing spot is where it will be
				if (isBoss && !n.noGravity && !n.noTileCollide && n.velocity.Y != 0f && !building) { gy = 0.3f; floor = FloorBelow(n); a = Vector2.Zero; }
				Snap.Add(new Threat { P = n.Center, V = v, A = building ? a : Vector2.Zero, Half = new Vector2(n.width, n.height) * 0.5f, Weight = n.damage, Body = true, Boss = isBoss, Gy = gy, Floor = floor });
			}
			BuildTs += Stopwatch.GetTimestamp() - t0; Builds++;
			return Snap;
		}

		/// <summary>Y (px) of the first solid tile under the body's centre column, 0 when there is none within 40 tiles.</summary>
		private static float FloorBelow(NPC n)
		{
			int x = (int)(n.Center.X / 16f), y0 = (int)(n.Bottom.Y / 16f);
			for (int r = y0; r <= y0 + 40; r++) if (TileNav.IsSolid(x, r)) return r * 16f;
			return 0f;
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

		public static Vector2 PathAt(Vector2 c0, in Maneuver m, float g, int t) => BodyAt(c0, m, g, t);

		private static Vector2 BodyAt(Vector2 c0, in Maneuver m, float g, int t)
		{
			if (m.Hook) return Vector2.Lerp(c0, m.HookTarget, MathHelper.Clamp((t - 8) / (float)Math.Max(1, m.HookDur), 0f, 1f));
			if (m.Air) return AirBodyAt(c0, m, g, t);
			float x = c0.X + m.Vx * Math.Min(t, m.Dur), y = c0.Y;
			if (m.JumpVy < 0f)
			{
				int tj = t - m.Delay;
				if (tj > 0)
				{
					float d;
					if (m.J2Vy < 0f && tj > m.J2At) { float y1 = m.JumpVy * m.J2At + 0.5f * g * m.J2At * m.J2At; int t2 = tj - m.J2At; d = y1 + m.J2Vy * t2 + 0.5f * g * t2 * t2; }
					else d = m.JumpVy * tj + 0.5f * g * tj * tj;
					if (d < 0f) y += d;
				}
			}
			else if (m.Drop && t > 3) y += 0.5f * g * (t - 3) * (t - 3);
			return new Vector2(x, y);
		}

		/// <summary>Distance fallen in t ticks from vertical speed v0 under gravity g, with the terminal speed a falling body has.</summary>
		private static float FallDist(float v0, float g, float t)
		{
			if (t <= 0f) return 0f;
			const float term = 10f;
			if (g <= 0f) return v0 * t;
			float tc = v0 >= term ? 0f : (term - v0) / g;
			if (t <= tc) return v0 * t + 0.5f * g * t * t;
			float yc = v0 * tc + 0.5f * g * tc * tc;
			return yc + Math.Max(v0, term) * (t - tc);
		}

		/// <summary>Distance covered in t ticks while the horizontal speed moves from v0 toward v1 at a (px/tick per tick); a = 0 is an instant change.</summary>
		private static float RampX(float v0, float v1, float a, float t)
		{
			if (a <= 0f) return v1 * t;
			float d = Math.Abs(v1 - v0), tr = d / a, sg = v1 >= v0 ? 1f : -1f;
			if (t <= tr) return v0 * t + sg * 0.5f * a * t * t;
			return v0 * tr + sg * 0.5f * a * tr * tr + v1 * (t - tr);
		}

		/// <summary>The path of a body that is already in the air: its own velocity, a horizontal air move (a step, a dash), one kick (second jump, flap, thrust, fast fall), and the floor it lands on.</summary>
		private static Vector2 AirBodyAt(Vector2 c0, in Maneuver m, float g, int t)
		{
			float x = m.Dur > 0 ? c0.X + RampX(m.V0x, m.Vx, m.AirAcc, Math.Min(t, m.Dur)) : c0.X + m.V0x * t;
			float y;
			if (m.J2Vy != 0f && t > m.J2At)
			{
				float y1 = FallDist(m.V0y, g, m.J2At);
				float v1 = Math.Min(10f, m.V0y + g * m.J2At);
				if (m.Kind == 12)
				{
					int th = Math.Min(t - m.J2At, Math.Max(1, m.HookDur)); float yT = m.J2Vy * th;
					y = y1 + yT + FallDist(m.J2Vy * 0.3f, g, t - m.J2At - th);
				}
				else y = y1 + FallDist(m.J2Vy, g, t - m.J2At);
			}
			else y = FallDist(m.V0y, g, t);
			y += c0.Y;
			if (y > m.FloorY) y = m.FloorY;
			return new Vector2(x, y);
		}

		private static Vector2 ThreatAt(in Threat th, int t)
		{
			Vector2 p = th.P + th.V * t;
			if (th.Gy > 0f)
			{
				p.Y += 0.5f * th.Gy * t * t;
				if (th.Floor > 0f && p.Y > th.Floor - th.Half.Y) p.Y = th.Floor - th.Half.Y;
				return p;
			}
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
		public static int Hits(List<Threat> near, Vector2 c0, float bw, float bh, in Maneuver m, float g, int horizon, out int firstHit, out int lastHit, float extraMargin = 0f, int immune = 0)
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
					for (int t = immune; t <= horizon; t++)
					{
						Vector2 b = BodyAt(c0, m, g, t); float ang = th.Ang + th.Omega * t;
						Vector2 end = th.P + ang.ToRotationVector2() * th.Len; float f = 0f;
						EvalSamples++;
						if (Collision.CheckAABBvLineCollision(b - bHalf, new Vector2(bw, bh), th.P, end, th.Width, ref f)) { if (hit < 0) hit = t; lineTicks++; lineLast = t; }
					}
				}
				else
				{
					float marg = th.Boss ? BossBodyMarginPx : BodyMarginPx;
					Vector2 half = th.Half + bHalf + (th.Body ? new Vector2(marg, marg) : new Vector2(2f)) + new Vector2(extraMargin, extraMargin);   // a prediction of a charging body is never exact: keep a margin around it
					int t0i = immune - (immune & 1);
					Vector2 prev = ThreatAt(th, t0i) - BodyAt(c0, m, g, t0i);
					if (immune == 0 && Math.Abs(prev.X) <= half.X && Math.Abs(prev.Y) <= half.Y) hit = 0;
					for (int t = t0i + 2; t <= horizon && hit < 0; t += 2)
					{
						Vector2 cur = ThreatAt(th, t) - BodyAt(c0, m, g, t);
						EvalSamples++;
						if (SegHitsBox(prev, cur, half)) hit = t;
						prev = cur;
					}
				}
				if (hit >= 0) { hits += (th.Boss ? BossHitWeight : 1) + lineTicks / 12; if (hit < firstHit) firstHit = hit; if (hit > lastHit) lastHit = hit; if (lineLast > lastHit) lastHit = lineLast; }
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
