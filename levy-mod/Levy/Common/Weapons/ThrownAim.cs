using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Ballistic lob for thrown consumables. These projectiles fly straight for a few ticks and then fall (Projectile.AI aiStyle 1/2/14/16/113), so
	/// the launch angle is found by simulating the flight for a sweep of angles and speeds and keeping the throw that passes closest to the
	/// (moving) target. Tiles are ignored: the combat layer already requires a line of sight before any ranged soldier fires.
	/// </summary>
	public static class ThrownAim
	{
		/// <summary>The flight model of a thrown projectile type, read from its AI.</summary>
		public static LevyConstants.ThrownFlight FlightFor(int projType)
		{
			var sample = WeaponClassifier.ProjectileSample(projType);
			switch (sample?.aiStyle ?? ProjAIStyleID.ThrownProjectile)
			{
				case ProjAIStyleID.Arrow: return LevyConstants.ThrownFlightArrow;
				case ProjAIStyleID.GroundProjectile: return LevyConstants.ThrownFlightGround;
				case ProjAIStyleID.Explosive:
					return projType == ProjectileID.Grenade || projType == ProjectileID.StickyGrenade || projType == ProjectileID.BouncyGrenade || projType == ProjectileID.PartyGirlGrenade
						? LevyConstants.ThrownFlightGrenade : LevyConstants.ThrownFlightBomb;
				case LevyConstants.ThrownStickAiStyle: return LevyConstants.ThrownFlightStick;
				default: return projType == 162 ? LevyConstants.ThrownFlightKnife162 : LevyConstants.ThrownFlightKnife;
			}
		}

		/// <summary>
		/// Launch velocity that carries a <paramref name="flight"/>-model projectile from <paramref name="muzzle"/> onto <paramref name="target"/> (which moves at
		/// <paramref name="targetVel"/>). The slowest speed that lands wins, so close targets get a gentle toss and far ones a hard lob; when nothing lands the
		/// closest pass is used. Low arcs are preferred over high ones at equal accuracy.
		/// </summary>
		public static Vector2 Solve(Vector2 muzzle, Vector2 target, Vector2 targetVel, LevyConstants.ThrownFlight flight, float shootSpeed)
		{
			if (target.HasNaNs()) target = muzzle + Vector2.UnitX;
			if (targetVel.HasNaNs()) targetVel = Vector2.Zero;
			Vector2 to = target - muzzle;
			if (to == Vector2.Zero) to = Vector2.UnitX;
			float baseAngle = to.ToRotation();

			float bestErr = float.MaxValue; Vector2 best = Vector2.Normalize(to) * Math.Max(shootSpeed, LevyConstants.ThrownSpeedSteps[0]);
			float first = Math.Max(4f, shootSpeed);
			for (int si = -1; si < LevyConstants.ThrownSpeedSteps.Length; si++)
			{
				float speed = si < 0 ? first : LevyConstants.ThrownSpeedSteps[si];
				if (si >= 0 && speed <= first + 0.25f) continue;
				float speedBestErr = float.MaxValue; float speedBestAngle = baseAngle;
				// angle offsets 0, +d, -d, +2d, -2d, ... so the flattest arc is found first and a later angle must be clearly better to replace it
				for (int k = 0; k < LevyConstants.ThrownAngleSteps; k++)
				{
					int ring = (k + 1) / 2; float sign = (k & 1) == 1 ? -1f : 1f;
					float off = MathHelper.ToRadians(LevyConstants.ThrownAngleSweepDeg) * ring / (LevyConstants.ThrownAngleSteps / 2f) * sign;
					float angle = baseAngle + off;
					float err = SimulateMiss(muzzle, angle.ToRotationVector2() * speed, target, targetVel, flight);
					if (err + 1f < speedBestErr) { speedBestErr = err; speedBestAngle = angle; }
				}
				if (speedBestErr < bestErr) { bestErr = speedBestErr; best = speedBestAngle.ToRotationVector2() * speed; }
				if (speedBestErr <= LevyConstants.ThrownHitRadiusPx) return best;
			}
			return best;
		}

		/// <summary>Closest approach (px) of a projectile launched with <paramref name="vel"/> to the moving target over its flight.</summary>
		private static float SimulateMiss(Vector2 muzzle, Vector2 vel, Vector2 target, Vector2 targetVel, LevyConstants.ThrownFlight f)
		{
			Vector2 pos = muzzle; float min = float.MaxValue;
			float fallLimit = Math.Max(muzzle.Y, target.Y) + 800f;
			for (int t = 1; t <= LevyConstants.ThrownSimMaxTicks; t++)
			{
				if (t > f.Delay) { vel.Y += f.Gravity; vel.X *= f.Drag; }
				// aiStyle-2 AI clamps the fall speed (Projectile.cs ~19940: velocity.Y > 16 => 16); a harmless no-op for the slower models.
				if (vel.Y > LevyConstants.ThrownKnifeTerminalVelY) vel.Y = LevyConstants.ThrownKnifeTerminalVelY;
				pos += vel;
				Vector2 tp = target + targetVel * Math.Min(t, LevyConstants.AimMaxLeadTicks);
				float d = Vector2.Distance(pos, tp);
				if (d < min) min = d;
				if (pos.Y > fallLimit && vel.Y > 0f) break;
			}
			return min;
		}
	}
}
