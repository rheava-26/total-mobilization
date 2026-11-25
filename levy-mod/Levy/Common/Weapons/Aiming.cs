using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Weapons
{
	/// <summary>Intercept + ballistic solves, line of sight, aim error. OWNED BY THE WEAPONS AGENT.</summary>
	public static class Aiming
	{
		/// <summary>
		/// Extra multiplier on jitter and lead error for the shots fired while it is set. The combat layer raises it around a single
		/// TryAttack call (routed soldiers) and always puts it back to 1 afterwards; the game update is single threaded.
		/// </summary>
		public static float ExtraErrorScale = 1f;

		/// <summary>
		/// Where the target will be when a projectile of <paramref name="speed"/> px/tick arrives (two iterations). Returns a direction * speed.
		/// With <paramref name="gravity"/> the aim point is raised by the drop a projectile accumulates after <paramref name="gravityDelay"/> ticks
		/// at <paramref name="gravityPerTick"/> px/tick² (vanilla arrows: 0.1 after 15 ticks), refined once.
		/// </summary>
		public static Vector2 SolveIntercept(Vector2 muzzle, float speed, Vector2 targetPos, Vector2 targetVel, bool gravity, float gravityPerTick = 0f, int gravityDelay = 0)
		{
			if (speed < 0.01f) speed = 0.01f;
			if (targetPos.HasNaNs()) targetPos = muzzle + Vector2.UnitX;
			if (targetVel.HasNaNs()) targetVel = Vector2.Zero;
			Vector2 predicted = targetPos;
			float t = 0f;
			for (int i = 0; i < LevyConstants.AimRefineIterations; i++)
			{
				t = MathHelper.Clamp(Vector2.Distance(muzzle, predicted) / speed, 0f, LevyConstants.AimMaxLeadTicks);
				predicted = targetPos + targetVel * t;
			}
			Vector2 aim = predicted;
			if (gravity && gravityPerTick > 0f)
			{
				aim.Y = predicted.Y - Drop(t, gravityPerTick, gravityDelay);
				float t2 = MathHelper.Clamp(Vector2.Distance(muzzle, aim) / speed, 0f, LevyConstants.AimMaxLeadTicks);
				aim.Y = predicted.Y - Drop(t2, gravityPerTick, gravityDelay);
			}
			Vector2 d = aim - muzzle;
			if (d == Vector2.Zero || d.HasNaNs()) d = Vector2.UnitX;
			return Vector2.Normalize(d) * speed;
		}

		/// <summary>Pixels a projectile falls over <paramref name="flightTicks"/> when gravity starts after <paramref name="delay"/> ticks.</summary>
		public static float Drop(float flightTicks, float gravityPerTick, int delay)
		{
			float n = Math.Max(0f, flightTicks - delay);
			return 0.5f * gravityPerTick * n * (n + 1f);
		}

		/// <summary>
		/// Multiplier on aim error for a shot of <paramref name="distPx"/> pixels: 1 up to <see cref="LevyConstants.AccuracyFalloffStartTiles"/>, then easing
		/// linearly down to <see cref="LevyConstants.AccuracyLongRangeErrorMult"/> at <see cref="LevyConstants.AccuracyFalloffEndTiles"/>.
		/// </summary>
		public static float LongRangeErrorScale(float distPx)
		{
			float tiles = distPx / LevyConstants.TilePx;
			float start = LevyConstants.AccuracyFalloffStartTiles, end = LevyConstants.AccuracyFalloffEndTiles;
			if (tiles <= start || end <= start) return 1f;
			float t = MathHelper.Clamp((tiles - start) / (end - start), 0f, 1f);
			return MathHelper.Lerp(1f, LevyConstants.AccuracyLongRangeErrorMult, t);
		}

		public static bool HasLineOfSight(Vector2 from, Vector2 to) => Collision.CanHitLine(from, 1, 1, to, 1, 1);

		/// <summary>
		/// Applies angle jitter (degrees) and lead error (fraction of the lead offset) to a velocity. Without a target position the lead
		/// error is turned into an angle at <see cref="LevyConstants.AimErrorReferenceDistancePx"/>.
		/// </summary>
		public static Vector2 ApplyError(Vector2 velocity, float jitterDeg, float leadError, Vector2 lead)
		{
			float speed = velocity.Length();
			if (speed <= 0.0001f) return velocity;
			Vector2 muzzle = Vector2.Zero;
			Vector2 target = velocity / speed * LevyConstants.AimErrorReferenceDistancePx;
			return ApplyError(velocity, jitterDeg, leadError, lead, muzzle, target);
		}

		/// <summary>Jitter plus a lead error that shifts the aim point by lead × U(−leadError, leadError) and re-aims from the muzzle.</summary>
		public static Vector2 ApplyError(Vector2 velocity, float jitterDeg, float leadError, Vector2 lead, Vector2 muzzle, Vector2 targetPos)
		{
			float speed = velocity.Length();
			if (speed <= 0.0001f) return velocity;
			if (ExtraErrorScale != 1f) { jitterDeg *= ExtraErrorScale; leadError *= ExtraErrorScale; }
			Vector2 dir = velocity / speed;
			if (leadError > 0f && lead != Vector2.Zero && !lead.HasNaNs())
			{
				float dist = Vector2.Distance(muzzle, targetPos);
				if (dist < 1f) dist = 1f;
				Vector2 aimPoint = muzzle + dir * dist + lead * Main.rand.NextFloat(-leadError, leadError);
				Vector2 d = aimPoint - muzzle;
				if (d != Vector2.Zero && !d.HasNaNs()) dir = Vector2.Normalize(d);
			}
			if (jitterDeg > 0f)
			{
				float rad = MathHelper.ToRadians(Main.rand.NextFloat(-jitterDeg, jitterDeg));
				dir = dir.RotatedBy(rad);
			}
			return dir * speed;
		}

		/// <summary>Nearest damageable segment of a multi-part enemy (same realLife), else the NPC itself.</summary>
		/// <summary>Set around a soldier's firing code by a rule that wants a different segment than the nearest (worm momentum); null = the nearest.</summary>
		public static System.Func<NPC, Vector2, NPC> Prefer;

		/// <summary>
		/// With <paramref name="preferOpen"/> (the aim of a shot or a swing): when the nearest segment is shut behind armour (<see cref="Targeting.SoldierTargeting.IsArmouredPart"/>: Thanatos's plates at 99.99%)
		/// or out of sight (a worm's body under the floor), the nearest segment of the same body that is open and in sight, no more than <see cref="OpenExtraTiles"/> further, is aimed at instead; failing that,
		/// for a part out of sight, the nearest one in sight. A player shoots the vent he can see, not the plate in front of it or the coil under the ground.
		/// </summary>
		public static NPC NearestSegment(NPC npc, Vector2 from, bool preferOpen = false)
		{
			NPC seg = NearestSegmentCore(npc, from, preferOpen);
			// a target whose centre is inside rock (a boss that flies through tiles and is pressed into a wall) is aimed at where it shows: a stand-in at the nearest open point of its hitbox that a shot can reach
			if (ExposeOn && seg != null && seg.whoAmI >= 0 && CentreWalled(seg) && TryExposedPoint(from, seg, out Vector2 pt)) { Exposed++; return ExposedStandIn(seg, pt); }
			return seg;
		}

		/// <summary>Harness switch (LEVY_EXPOSE=0): a target inside rock is left as it was (no line, no shot, no move).</summary>
		public static bool ExposeOn = Environment.GetEnvironmentVariable("LEVY_EXPOSE") != "0";
		/// <summary>Harness telemetry: aims turned from a target's walled-in centre to an open point of its hitbox.</summary>
		public static long Exposed;

		/// <summary>The target's centre lies inside solid tiles.</summary>
		public static bool CentreWalled(NPC n) => Collision.SolidCollision(n.Center - new Vector2(2f), 4, 4);

		/// <summary>
		/// The open point of <paramref name="n"/>'s hitbox nearest its centre that a shot from <paramref name="from"/> can reach (not inside a solid tile, a clear line to it): the hitbox is sampled on a grid a tile apart (at most 9 by 9).
		/// </summary>
		public static bool TryExposedPoint(Vector2 from, NPC n, out Vector2 pt)
		{
			pt = n.Center;
			Rectangle r = n.Hitbox;
			int cols = Math.Clamp(r.Width / 16 + 1, 3, 9), rows = Math.Clamp(r.Height / 16 + 1, 3, 9);
			float best = float.MaxValue; bool found = false;
			for (int iy = 0; iy < rows; iy++)
				for (int ix = 0; ix < cols; ix++)
				{
					Vector2 p = new Vector2(r.X + (ix + 0.5f) * r.Width / cols, r.Y + (iy + 0.5f) * r.Height / rows);
					float d = Vector2.DistanceSquared(p, n.Center);
					if (d >= best || Collision.SolidCollision(p - new Vector2(2f), 4, 4) || !Collision.CanHitLine(from, 1, 1, p, 1, 1)) continue;
					best = d; pt = p; found = true;
				}
			return found;
		}

		/// <summary>True when a shot from <paramref name="from"/> can reach the target: a clear line to its centre, or (the centre being inside rock) to an open point of its hitbox.</summary>
		public static bool Seen(Vector2 from, NPC n) => ExposeOn && CentreWalled(n) ? TryExposedPoint(from, n, out _) : Collision.CanHitLine(from, 1, 1, n.Center, 1, 1);

		private static NPC _exposed;
		private static NPC ExposedStandIn(NPC real, Vector2 pt)
		{
			_exposed ??= new NPC();
			_exposed.active = true; _exposed.width = 12; _exposed.height = 12; _exposed.velocity = real.velocity; _exposed.realLife = -1; _exposed.boss = false; _exposed.life = Math.Max(1, real.life); _exposed.lifeMax = Math.Max(1, real.lifeMax);
			_exposed.Center = pt;
			_exposed.whoAmI = -2 - (int)(unchecked((uint)real.whoAmI * 73856093u ^ 0x5bd1e995u) & 0x3FFFFFFF);
			return _exposed;
		}

		private static NPC NearestSegmentCore(NPC npc, Vector2 from, bool preferOpen = false)
		{
			if (npc == null) return null;
			if (Prefer != null) { NPC pick = Prefer(npc, from); if (pick != null) return pick; }
			if (npc.realLife < 0 && !npc.boss) return npc; // single-part ordinary enemy: no segment scan needed
			int root = npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? npc.realLife : npc.whoAmI;
			NPC best = npc;
			float bestDist = float.MaxValue; int parts = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage) continue;
				bool related = i == npc.whoAmI || i == root || n.realLife == root || n.realLife == npc.whoAmI;
				if (!related) continue;
				parts++;
				float d = Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(from), from);
				if (d < bestDist) { bestDist = d; best = n; }
			}
			if (preferOpen && parts > 1)
			{
				bool shut = Targeting.SoldierTargeting.IsArmouredPart(best);
				bool hidden = SegmentAimOn && !Collision.CanHitLine(from, 1, 1, best.Center, 1, 1);
				if (shut || hidden)
				{
					float lim = (float)Math.Sqrt(bestDist) + OpenExtraTiles * LevyConstants.TilePx;
					NPC open = NearestOpenSegment(npc, root, from, lim * lim, false);
					if (open != null) { if (shut) OpenAims++; else SeenAims++; return open; }
					if (hidden) { NPC seen = NearestOpenSegment(npc, root, from, lim * lim, true); if (seen != null) { SeenAims++; return seen; } } // nothing open in sight: at least a part the shot can reach
				}
			}
			return best;
		}

		/// <summary>Harness switch (LEVY_SEGAIM=0): a part out of sight is aimed at as before (the shot is then held).</summary>
		public static bool SegmentAimOn = Environment.GetEnvironmentVariable("LEVY_SEGAIM") != "0";
		/// <summary>Harness telemetry: aims turned from a part out of sight (a worm's segment under the floor) to one in sight.</summary>
		public static long SeenAims;

		/// <summary>How much further (tiles) than the shut plate in front an open segment may be and still be aimed at instead.</summary>
		public const float OpenExtraTiles = 25f;
		/// <summary>Harness telemetry: aims turned from a shut plate to an open segment of the same body.</summary>
		public static long OpenAims;

		/// <summary>The nearest part of the same body within sqrt(<paramref name="maxSq"/>) px that is in sight and (unless <paramref name="allowShut"/>) not behind closed armour, or null.</summary>
		private static NPC NearestOpenSegment(NPC npc, int root, Vector2 from, float maxSq, bool allowShut)
		{
			NPC best = null; float bestDist = maxSq;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage) continue;
				if (!(i == npc.whoAmI || i == root || n.realLife == root || n.realLife == npc.whoAmI)) continue;
				float d = Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(from), from);
				if (d >= bestDist || (!allowShut && Targeting.SoldierTargeting.IsArmouredPart(n))) continue;
				if (!Collision.CanHitLine(from, 1, 1, n.Center, 1, 1)) continue; // a part behind a wall (or under the floor) cannot be shot
				bestDist = d; best = n;
			}
			return best;
		}

		/// <summary>Distance in tiles from a point to the nearest edge of an NPC's hitbox.</summary>
		public static float TilesToHitbox(Vector2 from, NPC npc) => Vector2.Distance(from, npc.Hitbox.ClosestPointInRect(from)) / LevyConstants.TilePx;
	}
}
