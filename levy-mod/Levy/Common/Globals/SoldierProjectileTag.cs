using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Globals
{
	/// <summary>
	/// Per-projectile tag shared by Weapons (sets SoldierIndex on soldier shots), Targeting/LootCredit (reads it),
	/// Boss rule (reads SourceNpcIndex on hostile boss shots) and Damage (killer attribution).
	/// </summary>
	public class SoldierProjectileTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		/// <summary>NPC whoAmI of the soldier that fired this projectile, or -1.</summary>
		public int SoldierIndex = -1;
		/// <summary>NPC type/index that spawned this (hostile) projectile, from the entity source, or -1.</summary>
		public int SourceNpcType = -1, SourceNpcIndex = -1;
		/// <summary>Set by the boss rule when this projectile has been redirected at a spite target.</summary>
		public bool Redirected;

		public bool IsSoldierShot => SoldierIndex >= 0;
		/// <summary>Set once a reflected soldier shot has been re-aimed at its firing soldier (so the bounce-back hits the unit, not the commander).</summary>
		private bool _reflectRedirected;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (source is EntitySource_Parent { Entity: NPC npc })
			{
				SourceNpcType = npc.type; SourceNpcIndex = npc.whoAmI;
			}
			// A soldier's sentry fires its own shots (ballista bolts, fireballs, lightning, hydra breath): they carry the soldier's tag so kills and loot credit the soldier.
			else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.sentry && parent.TryGetGlobalProjectile(out SoldierProjectileTag parentTag) && parentTag.SoldierIndex >= 0)
				SoldierIndex = parentTag.SoldierIndex;
		}

		/// <summary>Set while this soldier sentry is parked (Projectile.sentry cleared) for the commander's Player.Update.</summary>
		private bool _sentryParked;

		/// <summary>
		/// Vanilla culls sentries only inside Player.UpdateMaxTurrets (the commander placing one, or maxTurrets changing), over EVERY sentry the commander owns, oldest first.
		/// Soldier shots are owned by the commander, so soldier towers would count against the commander's cap and cull its towers (and each other). Clearing
		/// Projectile.sentry on them for the length of the commander's own Update (Projectile.WipableTurret requires it) hides them from that count; they are restored right after.
		/// </summary>
		public static void ParkSoldierSentries()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || !p.sentry) continue;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0) { p.sentry = false; tag._sentryParked = true; }
			}
		}

		/// <summary>Undoes <see cref="ParkSoldierSentries"/>. Per-projectile flag, so a slot that was recycled while parked is never touched.</summary>
		public static void RestoreSoldierSentries()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag._sentryParked) { p.sentry = true; tag._sentryParked = false; }
			}
		}

		/// <summary>
		/// Terra Blade's beam (AI_191) switches ownerHitCheck on for its first 6 ticks, which gates every hit on a line of sight from Main.player[owner] (the commander)
		/// to the enemy. A soldier's beam starts at the soldier, so that check is turned back off.
		/// </summary>
		public override void PostAI(Projectile projectile)
		{
			if (SoldierIndex >= 0 && projectile.type == ProjectileID.TerraBlade2Shot) projectile.ownerHitCheck = false;
			// A mimic (etc.) reflecting a soldier's shot aims the bounce-back at Main.player[owner] — the commander, since soldier shots are
			// owned by the commander. Re-aim it at the soldier that fired it instead, so the reflection comes back at the unit, not at you. Once.
			if (SoldierIndex >= 0 && projectile.reflected && !_reflectRedirected)
			{
				_reflectRedirected = true;
				if (SoldierIndex < Main.maxNPCs)
				{
					NPC s = Main.npc[SoldierIndex];
					if (s.active && s.ModNPC is Content.NPCs.SoldierNPC)
					{
						Vector2 dir = s.Center - projectile.Center;
						if (dir != Vector2.Zero) { projectile.velocity = Vector2.Normalize(dir) * projectile.velocity.Length(); projectile.netUpdate = true; }
					}
				}
			}
		}

		/// <summary>
		/// A soldier-fired Flying Knife would otherwise steer to Main.player[owner]'s cursor (the commander's, since soldier shots are owned by the commander)
		/// and hover in open air. Replace its AI to mirror a held vanilla knife: it hovers on the soldier's current target for as long as the soldier keeps fighting,
		/// and returns to the soldier only once it stops. A player's own knife keeps vanilla AI.
		/// </summary>
		public override bool PreAI(Projectile projectile)
		{
			if (_sentryParked) { projectile.sentry = true; _sentryParked = false; } // backstop: never run a sentry's AI parked
			// A soldier sentry whose soldier is gone (dismissed, replaced slot, killed outside the death hook) has no one to keep it up.
			if (SoldierIndex >= 0 && projectile.sentry && Main.netMode != NetmodeID.MultiplayerClient
				&& !(SoldierIndex < Main.maxNPCs && Main.npc[SoldierIndex].active && Main.npc[SoldierIndex].ModNPC is SoldierNPC))
			{
				projectile.Kill();
				return false;
			}
			if (SoldierIndex < 0) return true;
			if (projectile.type == ProjectileID.FlyingKnife) { DriveSoldierFlyingKnife(projectile); return false; }
			if (IsGuidedMissile(projectile.type)) { DriveSoldierGuidedMissile(projectile); return false; }
			return true;
		}

		/// <summary>Channel-guided aiStyle-9 missiles (Magic Missile, Flamelash, Rainbow Rod): vanilla steers them to Main.player[owner]'s cursor, which is the commander's for soldier shots.</summary>
		private static bool IsGuidedMissile(int type) => type == ProjectileID.MagicMissile || type == ProjectileID.Flamelash || type == ProjectileID.RainbowRodBullet;

		/// <summary>Cruise speed captured on the first driven tick; the missile keeps its own launch speed while steering.</summary>
		private float _guidedSpeed;

		/// <summary>
		/// A soldier's guided missile homes on the soldier's current engaged target (never the commander's cursor/position) with a capped turn rate,
		/// and keeps its launch speed. With no live target it flies straight on its last heading, and expires naturally via timeLeft / tile / hit.
		/// </summary>
		private void DriveSoldierGuidedMissile(Projectile p)
		{
			if (_guidedSpeed <= 0f) _guidedSpeed = System.Math.Max(p.velocity.Length(), 6f);

			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			SoldierNPC body = s != null && s.active ? s.ModNPC as SoldierNPC : null;
			if (body != null)
			{
				bool engaging = (body.State == SoldierState.Engage || body.State == SoldierState.Shaken || body.State == SoldierState.Routed) && !body.Record.HoldFire;
				NPC target = engaging ? CurrentKnifeTarget(body) : null;
				if (target != null)
				{
					Vector2 to = target.Center - p.Center;
					if (to != Vector2.Zero)
					{
						// Rotate the heading toward the target by a bounded amount, then restore cruise speed (a lerp-and-renormalise turn).
						Vector2 heading = p.velocity == Vector2.Zero ? Vector2.Normalize(to) : Vector2.Normalize(p.velocity);
						heading = Vector2.Normalize(Vector2.Lerp(heading, Vector2.Normalize(to), 0.14f));
						p.velocity = heading * _guidedSpeed;
					}
				}
			}
			if (p.velocity != Vector2.Zero)
			{
				p.rotation = p.velocity.ToRotation() + MathHelper.PiOver2;
				p.netUpdate = false;
			}
			// Light trail in the missile's own colour family (vanilla AI is skipped, so supply a minimal one).
			int dust = p.type == ProjectileID.Flamelash ? DustID.Torch : p.type == ProjectileID.RainbowRodBullet ? DustID.RainbowMk2 : DustID.MagicMirror;
			if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustPerfect(p.Center, dust, -p.velocity * 0.1f, 100, p.type == ProjectileID.RainbowRodBullet ? Main.hslToRgb((Main.GlobalTimeWrappedHourly * 2f) % 1f, 1f, 0.6f) : default, 1.1f);
				d.noGravity = true;
			}
			Lighting.AddLight(p.Center, 0.35f, 0.3f, 0.5f);
		}

		private int _knifeAge;
		/// <summary>Ticks with no valid target while the soldier still fights (hover grace); doubles as the orphan counter once the soldier is gone.</summary>
		private int _knifeLost;

		/// <summary>True if this soldier already has a Flying Knife in the air (the spawn gate: one knife at a time per soldier).</summary>
		public static bool SoldierHasLiveKnife(int soldierIndex)
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (q.active && q.type == ProjectileID.FlyingKnife && q.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == soldierIndex) return true;
			}
			return false;
		}

		private void DriveSoldierFlyingKnife(Projectile p)
		{
			p.tileCollide = false;
			p.rotation += LevyConstants.KnifeSpinPerTick;
			if (p.timeLeft < 5) p.timeLeft = 5;
			// Defensive backstop only: cleans up a truly stuck knife; the soldier simply throws a fresh one.
			if (++_knifeAge > LevyConstants.KnifeSafetyMaxTicks) { p.Kill(); return; }

			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			SoldierNPC body = s != null && s.active ? s.ModNPC as SoldierNPC : null;
			if (body == null)
			{
				// Soldier gone: coast, slowing, and despawn after a short grace.
				p.velocity *= 0.96f;
				if (++_knifeLost > LevyConstants.KnifeOrphanGraceTicks) p.Kill();
				return;
			}

			// Same "is actively shooting" test as SoldierNPC.Combat.
			bool engaging = (body.State == SoldierState.Engage || body.State == SoldierState.Shaken || body.State == SoldierState.Routed) && !body.Record.HoldFire;
			NPC target = engaging ? CurrentKnifeTarget(body) : null;
			if (target != null)
			{
				_knifeLost = 0;
				SteerKnife(p, target.Center, LevyConstants.KnifeSeekAccel, LevyConstants.KnifeSeekMaxSpeed); // overshoot/orbit = the jabbing hover
				return;
			}
			_knifeLost++;
			if (engaging && _knifeLost < LevyConstants.KnifeLoseTargetGraceTicks)
			{
				p.velocity *= 0.9f; // between targets: hold position and wait for the soldier to reacquire
				return;
			}
			// Disengaged (or grace spent): fly home. If the soldier re-engages en route, the next tick's target != null flips it back to attacking.
			if (Vector2.Distance(p.Center, s.Center) <= LevyConstants.KnifeReturnCatchPx) { p.Kill(); return; }
			SteerKnife(p, s.Center, LevyConstants.KnifeReturnAccel, LevyConstants.KnifeReturnMaxSpeed);
		}

		/// <summary>The soldier's own engaged enemy if it is still a valid hostile; else null. The knife mirrors the soldier's aim and never picks a target itself.</summary>
		private static NPC CurrentKnifeTarget(SoldierNPC body)
		{
			int ti = body.TargetIndex;
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			return t.active && t.life > 0 && !SoldierTargeting.IsNeverTarget(t) ? t : null;
		}

		/// <summary>Standard homing: pull the velocity toward the destination, capped at max speed. Overshoots and loops, which reads as darting around the point.</summary>
		private static void SteerKnife(Projectile p, Vector2 dest, float accel, float maxSpeed)
		{
			Vector2 to = dest - p.Center;
			if (to != Vector2.Zero) p.velocity += Vector2.Normalize(to) * accel;
			float sp = p.velocity.Length();
			if (sp > maxSpeed) p.velocity *= maxSpeed / sp;
		}

		public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
		{
			binaryWriter.Write((short)SoldierIndex); binaryWriter.Write((short)SourceNpcIndex);
		}
		public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
		{
			SoldierIndex = binaryReader.ReadInt16(); SourceNpcIndex = binaryReader.ReadInt16();
			if (SourceNpcIndex >= 0 && SourceNpcIndex < Main.maxNPCs) SourceNpcType = Main.npc[SourceNpcIndex].type;
		}
	}
}
