using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;                      // LevyConstants.ArrowGravity / ArrowGravityDelayTicks
using Levy.Common.Officers;             // OfficerBrain.TownDamageScale
using Levy.Common.Globals;              // SoldierProjectileTag
using Levy.Common.Weapons;              // DroneShot / Aiming

namespace Mechanization.Common
{
	/// <summary>
	/// The shot plumbing every Mechanization gun shares (the turret's barrel and the drone's mount): how hard a round hits, where it is aimed, who gets the kill, and how it is marked.
	/// All of it rides Levy's public weapon pipeline (<see cref="DroneShot"/>, <see cref="Aiming"/>, <see cref="SoldierProjectileTag"/>); nothing here holds state.
	/// </summary>
	public static class ShotKit
	{
		/// <summary>The damage a round is fired for: <paramref name="baseDamage"/> scaled with world progress exactly as a town unit's rounds are, and by the game mode's town-NPC multiplier.</summary>
		public static int ScaledDamage(float baseDamage)
		{
			float dmg = baseDamage * OfficerBrain.TownDamageScale();
			if (Main.expertMode) dmg *= Main.GameModeInfo.TownNPCDamageMultiplier;
			return Math.Max(1, (int)Math.Round(dmg));
		}

		/// <summary>
		/// Lead, and the arc of a falling arrow: the same intercept solve a soldier's shot uses. Homing ammo flies straight at the target and needs no lead.
		/// A <paramref name="jitterDeg"/> above 0 adds the small long-range wobble a soldier has.
		/// </summary>
		public static Vector2 AimVelocity(DroneShot shot, Vector2 from, NPC target, float jitterDeg = 0f)
		{
			Vector2 at = target.Center;
			Vector2 vel;
			if (shot.Homing)
			{
				Vector2 d = at - from;
				if (d == Vector2.Zero) d = Vector2.UnitX;
				vel = Vector2.Normalize(d) * shot.Speed;
			}
			else
			{
				float steps = shot.StepMult <= 0f ? 1f : shot.StepMult; // the shot advances this many times a tick, and gravity runs per advance
				float leadSpeed = Math.Max(0.01f, shot.Speed) * steps;
				vel = Aiming.SolveIntercept(from, leadSpeed, at, target.velocity, shot.Gravity,
					shot.Gravity ? LevyConstants.ArrowGravity * steps * steps : 0f,
					shot.Gravity ? (int)Math.Round(LevyConstants.ArrowGravityDelayTicks / steps) : 0) / steps;
			}
			if (jitterDeg > 0f)
			{
				float jitter = jitterDeg * Aiming.LongRangeErrorScale(Vector2.Distance(from, at));
				vel = Aiming.ApplyError(vel, jitter, 0f, Vector2.Zero, from, at);
			}
			return vel;
		}

		/// <summary>The player a machine's kills are credited to: the nearest active player, a living one before a dead one. -1 when nobody is on.</summary>
		public static int PickCommander(Vector2 near)
		{
			int best = -1; float bestD = float.MaxValue;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active) continue;
				float d = Vector2.DistanceSquared(p.Center, near);
				if (p.dead) d += 1e12f;
				if (d < bestD) { bestD = d; best = i; }
			}
			return best;
		}

		/// <summary>
		/// Makes a spawned round a friendly, NPC-style shot owned by the real player and credited to <paramref name="commander"/> through Levy's
		/// <see cref="SoldierProjectileTag"/>. A piercing round gets its own hit-immunity so it does not share the owner's cooldown with every other shooter.
		/// </summary>
		public static void TagShot(Projectile p, int commander, DamageClass cls, int crit)
		{
			if (!p.active) return;
			p.friendly = true; p.hostile = false;
			if (cls != null) p.DamageType = cls;
			if (crit > 0) p.CritChance = crit;
			if (p.penetrate != 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity)
			{ p.usesLocalNPCImmunity = true; if (p.localNPCHitCooldown <= 0) p.localNPCHitCooldown = 10; }
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag))
			{
				tag.CommanderIndex = commander; // ModifyHitNPC credits this player before the kill drops loot (banners, bestiary, "defeated by")
				tag.DroneShot = true;           // spares the player's own base from the machine's blasts
			}
			p.npcProj = true; p.noDropItem = true; // the commander's own on-hit item effects and drops never run for a machine's shot
			p.netUpdate = true;
		}
	}
}
