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
		public static NPC NearestSegment(NPC npc, Vector2 from)
		{
			if (npc == null) return null;
			if (npc.realLife < 0 && !npc.boss) return npc; // single-part ordinary enemy: no segment scan needed
			int root = npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? npc.realLife : npc.whoAmI;
			NPC best = npc;
			float bestDist = float.MaxValue;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.dontTakeDamage) continue;
				bool related = i == npc.whoAmI || i == root || n.realLife == root || n.realLife == npc.whoAmI;
				if (!related) continue;
				float d = Vector2.DistanceSquared(n.Hitbox.ClosestPointInRect(from), from);
				if (d < bestDist) { bestDist = d; best = n; }
			}
			return best;
		}

		/// <summary>Distance in tiles from a point to the nearest edge of an NPC's hitbox.</summary>
		public static float TilesToHitbox(Vector2 from, NPC npc) => Vector2.Distance(from, npc.Hitbox.ClosestPointInRect(from)) / LevyConstants.TilePx;
	}
}
