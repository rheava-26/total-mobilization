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
		/// <summary>A worm segment (or a chain head): its path is a table of <see cref="TrajN"/> points, 2 ticks apart, starting at <see cref="TrajOff"/> in the field's shared trajectory store (see ThreatField.BuildChain).</summary>
		public int TrajOff, TrajN;
		/// <summary>Axis-aligned box around every sample of the path table (set with it): a quick reject for 'can this threat come near here at all'.</summary>
		public Vector2 BbMin, BbMax;
		/// <summary>A worm chain's head (set only while the worm dodge is on). <see cref="Turn"/> is its turn rate (rad per tick) and <see cref="TurnT"/> the ticks that turn is assumed to last (a body threat bends on an arc, then flies straight).</summary>
		public bool Head; public float Turn, TurnT;
	}

	/// <summary>A worm head the field tracks this tick: where it is, how fast it flies, and how fast its heading is turning (rad per tick).</summary>
	public struct HeadInfo { public int Idx; public Vector2 P, V; public float Omega; }

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
		/// <summary>A winged body's held fall: its downward speed never passes this (0 = no cap). Kind 15 is the wing hold.</summary>
		public float VyCap;
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
		internal static bool FastOn = Environment.GetEnvironmentVariable("LEVY_PERFOPT") != "0";
		public static long BuildTs, EvalTs; public static int Builds, Evals, EvalSamples;

		public static float BodyMarginPx = 10f, BossBodyMarginPx = 22f;
		public const int BossHitWeight = 3;
		/// <summary>Number of boss bodies the field saw this tick (a fight is on).</summary>
		public static int BossBodies;
		public const float ChargeMinSpeed = 4.5f, ChargeAccel = 0.25f, ChargeTopSpeed = 20f, MaxBuildTicks = 60f;

		// ---- worm chains: a segmented body is one moving curve, not a row of stationary blocks ----
		/// <summary>Samples in a chain segment's path table (2 ticks apart: covers a 58-tick look-ahead).</summary>
		public const int ChainSamples = 30;
		/// <summary>Largest per-tick turn rate (rad) a chain head is assumed to keep, and how fast that turn is assumed to ease off.</summary>
		private const float ChainMaxOmega = 0.09f, ChainOmegaDecay = 0.985f;
		private static readonly int[] _childOf = new int[Main.maxNPCs + 1];
		private static readonly bool[] _follower = new bool[Main.maxNPCs + 1];
		private static readonly int[] _snapOf = new int[Main.maxNPCs + 1];
		private static readonly int[] _chainOf = new int[Main.maxNPCs + 1];
		/// <summary>The head of the chain this NPC is a segment of this tick (the head itself included), -1 when it is in none.</summary>
		public static int ChainHeadOf(int npc) => (uint)npc < (uint)_chainOf.Length ? _chainOf[npc] : -1;
		private static readonly float[] _omega = new float[Main.maxNPCs + 1];
		private static Vector2[] _traj = new Vector2[4096];
		private static int _trajN;
		private static readonly int[] _chainSegs = new int[512];
		private static readonly float[] _chainD = new float[512];
		private static readonly Vector2[] _bp = new Vector2[96];
		private static readonly Vector2[] _hPath = new Vector2[ChainSamples];
		private static readonly float[] _hLen = new float[ChainSamples];
		/// <summary>Chains and chain segments the field modelled this tick (harness / diagnostics).</summary>
		public static int ChainsThisTick, ChainSegsThisTick;
		/// <summary>NPC indices of the heads of the chains modelled this tick.</summary>
		public static readonly List<int> ChainHeads = new();
		/// <summary>The heads of the chains modelled this tick, with their motion (read by the worm momentum rule).</summary>
		public static readonly List<HeadInfo> Heads = new();
		/// <summary>Harness switch (LEVY_CHAIN=1): model worm chains (OFF by default: the A/B runs showed no gain, and Eater of Worlds fared worse) or fall back to treating each segment as its own body.</summary>
		internal static bool ChainBossBody = Environment.GetEnvironmentVariable("LEVY_CHAINBOSS") == "1";
		internal static bool ChainOn = Environment.GetEnvironmentVariable("LEVY_CHAIN") == "1" || Environment.GetEnvironmentVariable("LEVY_WORMBODY") != "0";

		/// <summary>
		/// True when this NPC is a follower in a segmented body: it takes the segment ahead of it (ai[1]) as its parent and is dragged along by it (its own velocity is zeroed every tick).
		/// Recognised by the shape every worm shares, never by name: vanilla worms (Worm AI: Eater of Worlds, Destroyer, Wyvern, giant worms) link through ai[1] with the Worm style on both ends,
		/// and a modded worm shares the head's realLife and links to its parent (ai[1]) whose ai[0] points back at it (Calamity's Desert Scourge, Storm Weaver, Aquatic Scourge...).
		/// </summary>
		public static bool IsChainFollower(NPC n, out int parent)
		{
			parent = (int)n.ai[1];
			if (parent < 0 || parent >= Main.maxNPCs || parent == n.whoAmI) return false;
			NPC p = Main.npc[parent];
			if (!p.active || p.friendly) return false;
			bool back = (int)p.ai[0] == n.whoAmI;
			if (n.aiStyle == NPCAIStyleID.Worm && p.aiStyle == NPCAIStyleID.Worm) return parent > 0 || back;
			return back && n.realLife >= 0 && n.realLife != n.whoAmI && (parent == n.realLife || p.realLife == n.realLife);
		}

		public static List<Threat> Snapshot()
		{
			uint now = Main.GameUpdateCount;
			if (_tick == now) return Snap;
			_tick = now;
			long t0 = Stopwatch.GetTimestamp();
			Snap.Clear(); BossBodies = 0; _trajN = 0; ChainsThisTick = 0; ChainSegsThisTick = 0; ChainHeads.Clear(); Heads.Clear();
			for (int i = 0; i < _childOf.Length; i++) { _childOf[i] = -1; _follower[i] = false; _snapOf[i] = -1; _chainOf[i] = -1; }
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
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is Content.NPCs.SoldierNPC) continue;
				// a follower in a worm's body is dragged along by the segment ahead (its velocity reads zero): its path comes from the chain, built below
				if (IsChainFollower(n, out int par)) { _follower[i] = true; if (_childOf[par] < 0) _childOf[par] = i; if (ChainOn) continue; }
				if (n.damage < (Content.NPCs.SoldierNPC.MobilityMode >= 1 ? LevyConstants.Movement.DodgeNpcMinDamageMobile : LevyConstants.Movement.DodgeNpcMinDamage)) continue;
				Vector2 v = n.velocity; float v2 = v.LengthSquared();
				// the acceleration it is showing: how its velocity changed since the last time the field looked (NPC.oldVelocity is not kept for every NPC)
				Vector2 a = Vector2.Zero;
				if (PrevVelTick[i] != 0 && now > PrevVelTick[i] && now - PrevVelTick[i] <= 8u)
				{
					float dt = now - PrevVelTick[i];
					a = (n.velocity - PrevVel[i]) / dt;
					// how fast its heading is turning (a worm loops on a smooth arc): smoothed, since one tick of AI steering is noisy
					if (v2 > 1f && PrevVel[i].LengthSquared() > 1f) _omega[i] = _omega[i] * 0.6f + 0.4f * MathHelper.WrapAngle((float)(Math.Atan2(v.Y, v.X) - Math.Atan2(PrevVel[i].Y, PrevVel[i].X))) / dt;
				}
				else _omega[i] = 0f;
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
				_snapOf[i] = Snap.Count;
				Snap.Add(new Threat { P = n.Center, V = v, A = building ? a : Vector2.Zero, Half = new Vector2(n.width, n.height) * 0.5f, Weight = n.damage, Body = true, Boss = isBoss, Gy = gy, Floor = floor });
			}
			// every worm whose head the field is tracking: the whole chain is one moving curve (the body follows the head's path with a lag)
			for (int i = 0; i < Main.maxNPCs; i++) if (_childOf[i] >= 0 && !_follower[i] && _snapOf[i] >= 0)
			{
				if (ChainOn) BuildChain(i); else ChainHeads.Add(i);
				if (Content.NPCs.SoldierNPC.WormDodgeOn)
				{
					Threat h = Snap[_snapOf[i]]; h.Head = true;
					if (h.TrajN == 0)
					{
						float w = MathHelper.Clamp(_omega[i], -LevyConstants.WormDodge.MaxTurnPerTick, LevyConstants.WormDodge.MaxTurnPerTick);
						if (Math.Abs(w) > 1e-4f) { h.Turn = w; h.TurnT = LevyConstants.WormDodge.TurnCapRad / Math.Abs(w); }
					}
					Snap[_snapOf[i]] = h;
				}
			}
			for (int hi = 0; hi < ChainHeads.Count; hi++) { int ix = ChainHeads[hi]; NPC hn = Main.npc[ix]; Heads.Add(new HeadInfo { Idx = ix, P = hn.Center, V = hn.velocity, Omega = _omega[ix] }); }
			long tb = Stopwatch.GetTimestamp() - t0; BuildTs += tb; Builds++; if (Util.LevyProf.On) Util.LevyProf.Add(Util.LevyProf.Snap, tb);
			return Snap;
		}

		/// <summary>
		/// A worm as ONE moving curve. The head's path is predicted as an arc (its velocity, the acceleration it is showing, and the turn it has been making, easing off); every follower
		/// is where the head's path puts it once the lag is counted: a segment that is s px behind the head's start has, after the head has covered L px, s - L px of the OLD trail left
		/// ahead of it (the polyline through the current segment centres) and, once that runs out, it is on the head's own future path. So the body sweeps through exactly where the head went,
		/// instead of standing still where it was last seen (a follower's velocity is zero). Works for any chain: nothing is keyed to a name.
		/// </summary>
		private static void BuildChain(int head)
		{
			int n = 0, cur = head; _chainSegs[n++] = head;
			while (n < _chainSegs.Length)
			{
				int c = _childOf[cur];
				if (c < 0 || c == head) break;
				_chainSegs[n++] = c; cur = c;
			}
			if (n < 2) return;
			NPC h = Main.npc[head];
			int idx = _snapOf[head];
			Threat th0 = Snap[idx];
			_chainD[0] = 0f;
			for (int k = 1; k < n; k++) _chainD[k] = _chainD[k - 1] + Vector2.Distance(Main.npc[_chainSegs[k]].Center, Main.npc[_chainSegs[k - 1]].Center);
			// the head's future path at 2-tick samples: straight-line speed profile (with any building acceleration) bent by the turn rate
			float omega = MathHelper.Clamp(_omega[head], -ChainMaxOmega, ChainMaxOmega);
			float ang0 = (float)Math.Atan2(th0.V.Y, th0.V.X), arcPrev = 0f, theta = 0f, omT = omega;
			Vector2 pos = th0.P; _hPath[0] = pos; _hLen[0] = 0f; float len = 0f;
			for (int t = 1; t < 2 * ChainSamples - 1; t++)
			{
				float arc = Vector2.Distance(ThreatAt(th0, t), th0.P);
				float step = Math.Max(0f, arc - arcPrev); arcPrev = arc;
				theta += omT; omT *= ChainOmegaDecay;
				pos += new Vector2((float)Math.Cos(ang0 + theta), (float)Math.Sin(ang0 + theta)) * step; len += step;
				if ((t & 1) == 0) { _hPath[t >> 1] = pos; _hLen[t >> 1] = len; }
			}
			ChainsThisTick++; ChainHeads.Add(head);
			for (int k = 0; k < n; k++) _chainOf[_chainSegs[k]] = head;
			bool dm = Content.NPCs.SoldierNPC.DanceMode >= 1 && Content.NPCs.SoldierNPC.DOn(1);
			for (int i = 0; i < ChainSamples; i++) _tp[i] = 1;
			for (int k = 0; k < n; k++)
			{
				NPC seg = Main.npc[_chainSegs[k]];
				if (_trajN + ChainSamples > _traj.Length) Array.Resize(ref _traj, _traj.Length * 2);
				int off = _trajN; _trajN += ChainSamples;
				float dk = _chainD[k];
				Vector2 bmin = new Vector2(float.MaxValue), bmax = new Vector2(float.MinValue);
				for (int i = 0; i < ChainSamples; i++)
				{
					float s = dk - _hLen[i];
					Vector2 q = s >= 0f ? (FastOn ? OnTrailFrom(n, s, ref _tp[i]) : OnTrail(n, s)) : OnHeadPath(-s);
					_traj[off + i] = q;
					if (q.X < bmin.X) bmin.X = q.X; if (q.X > bmax.X) bmax.X = q.X;
					if (q.Y < bmin.Y) bmin.Y = q.Y; if (q.Y > bmax.Y) bmax.Y = q.Y;
				}
				bool boss = dm && (k == 0 || ChainBossBody) && LevyUtil.IsBossNpc(seg);
				if (boss) BossBodies++;
				ChainSegsThisTick++;
				var th = new Threat { P = seg.Center, Half = new Vector2(seg.width, seg.height) * 0.5f, Weight = k == 0 ? th0.Weight : Math.Max(seg.damage, 1), Body = true, Boss = boss, TrajOff = off, TrajN = ChainSamples, BbMin = bmin, BbMax = bmax };
				if (k == 0) Snap[idx] = th; else Snap.Add(th);
			}
		}

		private static readonly int[] _tp = new int[ChainSamples];
		/// <summary>OnTrail with the search resumed at <paramref name="k"/>: along one sample column the distance only grows from one segment to the next, so the search never has to restart.</summary>
		private static Vector2 OnTrailFrom(int n, float s, ref int k)
		{
			while (k < n - 1 && _chainD[k] < s) k++;
			float d0 = _chainD[k - 1], d1 = _chainD[k];
			float u = d1 - d0 > 1e-3f ? MathHelper.Clamp((s - d0) / (d1 - d0), 0f, 1f) : 0f;
			return Vector2.Lerp(Main.npc[_chainSegs[k - 1]].Center, Main.npc[_chainSegs[k]].Center, u);
		}

		/// <summary>The point <paramref name="s"/> px behind the head's start along the polyline through the segment centres.</summary>
		private static Vector2 OnTrail(int n, float s)
		{
			int k = 1;
			while (k < n - 1 && _chainD[k] < s) k++;
			float d0 = _chainD[k - 1], d1 = _chainD[k];
			float u = d1 - d0 > 1e-3f ? MathHelper.Clamp((s - d0) / (d1 - d0), 0f, 1f) : 0f;
			return Vector2.Lerp(Main.npc[_chainSegs[k - 1]].Center, Main.npc[_chainSegs[k]].Center, u);
		}

		/// <summary>The point on the head's future path that is <paramref name="len"/> px of path past where the head is now.</summary>
		private static Vector2 OnHeadPath(float len)
		{
			int i = 1;
			while (i < ChainSamples - 1 && _hLen[i] < len) i++;
			float l0 = _hLen[i - 1], l1 = _hLen[i];
			float u = l1 - l0 > 1e-3f ? MathHelper.Clamp((len - l0) / (l1 - l0), 0f, 1f) : 0f;
			return Vector2.Lerp(_hPath[i - 1], _hPath[i], u);
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
			NearCore(all, c, reach, horizon, into, FastOn);
			if (!PerfChk) return;
			_chkNear.Clear(); NearCore(all, c, reach, horizon, _chkNear, !FastOn);
			ChkN++; bool bad = _chkNear.Count != into.Count;
			for (int i = 0; !bad && i < into.Count; i++) if (into[i].P != _chkNear[i].P || into[i].TrajOff != _chkNear[i].TrajOff) bad = true;
			if (bad) ChkBad++;
		}
		private static readonly List<Threat> _chkNear = new(64);
		public static bool PerfChk = Environment.GetEnvironmentVariable("LEVY_PERFCHK") == "1"; public static int ChkN, ChkBad;

		private static void NearCore(List<Threat> all, Vector2 c, float reach, int horizon, List<Threat> into, bool fast)
		{
			into.Clear();
			for (int i = 0; i < all.Count; i++)
			{
				Threat t = all[i];
				if (t.Line) { if (Vector2.DistanceSquared(t.P, c) <= (t.Len + reach) * (t.Len + reach)) into.Add(t); continue; }
				if (t.TrajN > 0)
				{
					float rr = reach + Math.Max(t.Half.X, t.Half.Y);
					if (fast && (c.X < t.BbMin.X - rr || c.X > t.BbMax.X + rr || c.Y < t.BbMin.Y - rr || c.Y > t.BbMax.Y + rr)) continue;   // outside the box around the whole path: no segment can be within reach
					float rt = rr * rr;
					int last = Math.Min(t.TrajN - 1, (horizon + 1) >> 1);
					for (int k = 0; k < last; k++) if (SegDistSq(_traj[t.TrajOff + k], _traj[t.TrajOff + k + 1], c) <= rt) { into.Add(t); break; }
					continue;
				}
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
			if (m.VyCap > 0f && y > m.VyCap * t) y = m.VyCap * t;
			y += c0.Y;
			if (y > m.FloorY) y = m.FloorY;
			return new Vector2(x, y);
		}

		private static Vector2 ThreatAt(in Threat th, int t)
		{
			if (th.TrajN > 0)
			{
				float f = t * 0.5f; int i0 = (int)f;
				if (i0 >= th.TrajN - 1) return _traj[th.TrajOff + th.TrajN - 1];
				return Vector2.Lerp(_traj[th.TrajOff + i0], _traj[th.TrajOff + i0 + 1], f - i0);
			}
			if (th.Turn != 0f && th.Gy <= 0f) return TurnedAt(th, t);
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

		/// <summary>A head on an arc: it keeps its speed and bends at its turn rate for <see cref="Threat.TurnT"/> ticks, then flies straight along the heading it has reached.</summary>
		private static Vector2 TurnedAt(in Threat th, int t)
		{
			float s = th.V.Length(); if (s < 1e-3f) return th.P;
			float w = th.Turn, th0 = (float)Math.Atan2(th.V.Y, th.V.X), tau = Math.Min(t, th.TurnT);
			float a1 = th0 + w * tau;
			float dx = s / w * ((float)Math.Sin(a1) - (float)Math.Sin(th0)), dy = -s / w * ((float)Math.Cos(a1) - (float)Math.Cos(th0));
			float rest = t - tau;
			return th.P + new Vector2(dx + rest * s * (float)Math.Cos(a1), dy + rest * s * (float)Math.Sin(a1));
		}

		/// <summary>The threats as they will be <paramref name="ticks"/> from now (what a dodge started that much later would face). A path-table threat (the chain model) cannot be aged and is copied as it is.</summary>
		public static void Aged(List<Threat> src, int ticks, List<Threat> into)
		{
			into.Clear();
			for (int i = 0; i < src.Count; i++)
			{
				Threat th = src[i];
				if (th.Line) { th.Ang += th.Omega * ticks; into.Add(th); continue; }
				if (th.TrajN > 0) { into.Add(th); continue; }
				Vector2 p = ThreatAt(th, ticks);
				if (th.Turn != 0f && th.Gy <= 0f)
				{
					float tau = Math.Min(ticks, th.TurnT), rot = th.Turn * tau, c = (float)Math.Cos(rot), sn = (float)Math.Sin(rot);
					th.V = new Vector2(th.V.X * c - th.V.Y * sn, th.V.X * sn + th.V.Y * c); th.TurnT = Math.Max(0f, th.TurnT - ticks);
				}
				th.P = p; into.Add(th);
			}
		}

		/// <summary>Where this threat is <paramref name="t"/> ticks from now.</summary>
		public static Vector2 PosAt(in Threat th, int t) => ThreatAt(th, t);

		/// <summary>
		/// Ticks until this head can fly through <paramref name="c"/> again. A head keeps its speed and bends at a bounded turn rate, so a point is reached along the shortest of the two turn-circle routes: an arc
		/// around the circle on that side, then a straight leg along the tangent. A point inside a turn circle cannot be reached by that side at all (the head must leave and loop back: a half turn plus the distance).
		/// A head already turning one way pays <paramref name="reverseTicks"/> to start turning the other. Pure geometry: no name, no tile.
		/// </summary>
		public static float RecallTicks(in HeadInfo h, Vector2 c, float turnRate, float minSpeed, float reverseTicks)
		{
			float s = Math.Max(h.V.Length(), minSpeed);
			float w = Math.Max(turnRate, Math.Abs(h.Omega) * 1.2f);
			Vector2 d = c - h.P;
			Vector2 u = h.V.LengthSquared() > 1e-4f ? Vector2.Normalize(h.V) : new Vector2(1f, 0f);
			float x = d.X * u.X + d.Y * u.Y, y = d.X * -u.Y + d.Y * u.X;     // x along the heading, y toward the side positive omega turns to
			float r = s / w, best = float.MaxValue;
			bool committed = Math.Abs(h.Omega) > 0.015f;
			for (int sg = -1; sg <= 1; sg += 2)
			{
				float cy = sg * r, dx = x, dy = y - cy, D = (float)Math.Sqrt(dx * dx + dy * dy), t;
				if (D <= r) t = MathHelper.Pi / w + Math.Max(0f, (float)Math.Sqrt(x * x + y * y)) / s;
				else
				{
					float a0 = -sg * MathHelper.PiOver2, aT = (float)Math.Atan2(dy, dx), beta = (float)Math.Acos(r / D);
					float arc = sg > 0 ? aT - beta - a0 : a0 - (aT + beta);
					arc = arc % MathHelper.TwoPi; if (arc < 0f) arc += MathHelper.TwoPi;
					if (arc > MathHelper.TwoPi - 1e-3f) arc = 0f;
					t = arc / w + (float)Math.Sqrt(D * D - r * r) / s;
				}
				if (committed && Math.Sign(h.Omega) != sg) t += reverseTicks;
				if (t < best) best = t;
			}
			return best;
		}

		/// <summary>True when one of these threats is a worm head.</summary>
		public static bool HasHead(List<Threat> l) { for (int i = 0; i < l.Count; i++) if (l[i].Head) return true; return false; }

		/// <summary>Counts the threats that overlap the body on this manoeuvre inside the horizon. <paramref name="lastHit"/> is the tick of the latest overlap.</summary>
		public static int Hits(List<Threat> near, Vector2 c0, float bw, float bh, in Maneuver m, float g, int horizon, out int firstHit, out int lastHit, float extraMargin = 0f, int immune = 0)
		{
			if (!PerfChk) return HitsCore(near, c0, bw, bh, m, g, horizon, out firstHit, out lastHit, extraMargin, immune, FastOn);
			int a = HitsCore(near, c0, bw, bh, m, g, horizon, out firstHit, out lastHit, extraMargin, immune, true);
			int b = HitsCore(near, c0, bw, bh, m, g, horizon, out int f2, out int l2, extraMargin, immune, false);
			ChkN++; if (a != b || firstHit != f2 || lastHit != l2) ChkBad++;
			return a;
		}

		private static int HitsCore(List<Threat> near, Vector2 c0, float bw, float bh, in Maneuver m, float g, int horizon, out int firstHit, out int lastHit, float extraMargin, int immune, bool fast)
		{
			long t0 = Stopwatch.GetTimestamp();
			int hits = 0; firstHit = int.MaxValue; lastHit = -1;
			Vector2 bHalf = new Vector2(bw, bh) * 0.5f;
			// the body's path is the same for every threat: sample it once (2-tick steps from the first even tick), with the box around it
			int bt0 = immune - (immune & 1), bn = horizon >= bt0 ? ((horizon - bt0) >> 1) + 1 : 0;
			bool haveBp = fast && bn > 0 && bn <= _bp.Length;
			Vector2 bbMin = new Vector2(float.MaxValue), bbMax = new Vector2(float.MinValue);
			if (haveBp)
				for (int i = 0; i < bn; i++)
				{
					Vector2 q = BodyAt(c0, m, g, bt0 + 2 * i); _bp[i] = q;
					if (q.X < bbMin.X) bbMin.X = q.X; if (q.X > bbMax.X) bbMax.X = q.X;
					if (q.Y < bbMin.Y) bbMin.Y = q.Y; if (q.Y > bbMax.Y) bbMax.Y = q.Y;
				}
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
					if (haveBp && th.TrajN > 0 && (th.BbMin.X - half.X > bbMax.X || th.BbMax.X + half.X < bbMin.X || th.BbMin.Y - half.Y > bbMax.Y || th.BbMax.Y + half.Y < bbMin.Y)) continue;   // the two paths' boxes never meet: no hit
					Vector2 prev = ThreatAt(th, t0i) - (haveBp ? _bp[0] : BodyAt(c0, m, g, t0i));
					if (immune == 0 && Math.Abs(prev.X) <= half.X && Math.Abs(prev.Y) <= half.Y) hit = 0;
					for (int t = t0i + 2; t <= horizon && hit < 0; t += 2)
					{
						Vector2 cur = ThreatAt(th, t) - (haveBp ? _bp[(t - bt0) >> 1] : BodyAt(c0, m, g, t));
						EvalSamples++;
						if (SegHitsBox(prev, cur, half)) hit = t;
						prev = cur;
					}
				}
				if (hit >= 0) { hits += (th.Boss ? BossHitWeight : 1) + lineTicks / 12; if (hit < firstHit) firstHit = hit; if (hit > lastHit) lastHit = hit; if (lineLast > lastHit) lastHit = lineLast; }
			}
			long te = Stopwatch.GetTimestamp() - t0; EvalTs += te; Evals++; if (Util.LevyProf.On) Util.LevyProf.Add(Util.LevyProf.Hits, te);
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
