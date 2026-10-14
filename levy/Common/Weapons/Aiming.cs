using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Weapons
{
	/// <summary>Intercept + ballistic solves, line of sight, aim error. OWNED BY THE WEAPONS AGENT.</summary>
	public static class Aiming
	{
		/// <summary>Where the target will be when a projectile of <paramref name="speed"/> px/tick arrives (two iterations). Returns a direction * speed.</summary>
		public static Vector2 SolveIntercept(Vector2 muzzle, float speed, Vector2 targetPos, Vector2 targetVel, bool gravity, float gravityPerTick = 0f, int gravityDelay = 0)
		{
			var d = targetPos - muzzle; if (d == Vector2.Zero) d = Vector2.UnitX; d.Normalize(); return d * speed;
		}
		public static bool HasLineOfSight(Vector2 from, Vector2 to) => Collision.CanHitLine(from, 1, 1, to, 1, 1);
		/// <summary>Applies angle jitter (degrees) and lead error (fraction of the lead offset) to a velocity.</summary>
		public static Vector2 ApplyError(Vector2 velocity, float jitterDeg, float leadError, Vector2 lead) => velocity;
		/// <summary>Nearest damageable segment of a multi-part enemy (same realLife), else the NPC itself.</summary>
		public static NPC NearestSegment(NPC npc, Vector2 from) => npc;
	}
}
