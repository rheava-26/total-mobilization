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
		/// and hover in open air. Replace its AI: seek the soldier's target, dart around it, then return to the soldier. A player's own knife keeps vanilla AI.
		/// </summary>
		public override bool PreAI(Projectile projectile)
		{
			if (SoldierIndex < 0 || projectile.type != ProjectileID.FlyingKnife) return true;
			DriveSoldierFlyingKnife(projectile);
			return false;
		}

		private int _knifeAge;

		private void DriveSoldierFlyingKnife(Projectile p)
		{
			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			SoldierNPC body = s != null && s.active ? s.ModNPC as SoldierNPC : null;

			p.tileCollide = false;
			p.rotation += LevyConstants.KnifeSpinPerTick;
			if (p.timeLeft < 5) p.timeLeft = 5;
			if (++_knifeAge > LevyConstants.KnifeMaxLifeTicks) { p.Kill(); return; }

			if (p.ai[0] == 0f)
			{
				NPC target = PickKnifeTarget(p, body);
				p.ai[1]++;
				if (target == null || p.ai[1] >= LevyConstants.KnifeSeekTicks)
				{
					p.ai[0] = 1f; p.ai[1] = 0f; p.netUpdate = true;
					return;
				}
				SteerKnife(p, target.Center, LevyConstants.KnifeSeekAccel, LevyConstants.KnifeSeekMaxSpeed);
				return;
			}

			p.ai[1]++;
			if (body == null)
			{
				// Soldier gone: coast on the current heading, slowing, and despawn after a short grace.
				p.velocity *= 0.96f;
				if (p.ai[1] > LevyConstants.KnifeOrphanGraceTicks) p.Kill();
				return;
			}
			if (Vector2.Distance(p.Center, s.Center) <= LevyConstants.KnifeReturnCatchPx) { p.Kill(); return; }
			SteerKnife(p, s.Center, LevyConstants.KnifeReturnAccel, LevyConstants.KnifeReturnMaxSpeed);
		}

		/// <summary>The soldier's engaged enemy if it is still a valid hostile; else the nearest valid hostile within the fallback radius of the knife; else null.</summary>
		private static NPC PickKnifeTarget(Projectile p, SoldierNPC body)
		{
			if (body != null)
			{
				int ti = body.TargetIndex;
				if (ti >= 0 && ti < Main.maxNPCs)
				{
					NPC t = Main.npc[ti];
					if (t.active && t.life > 0 && !SoldierTargeting.IsNeverTarget(t)) return t;
				}
			}
			float best = LevyConstants.KnifeFallbackSearchTiles * LevyConstants.TilePx;
			NPC found = null;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || SoldierTargeting.IsNeverTarget(n)) continue;
				float d = Vector2.Distance(p.Center, n.Center);
				if (d < best) { best = d; found = n; }
			}
			return found;
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
