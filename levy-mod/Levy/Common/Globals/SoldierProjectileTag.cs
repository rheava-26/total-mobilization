using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
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
			// Anything a soldier's projectile spawns (a sentry's own shots: ballista bolts, fireballs, lightning, hydra breath; Beenade bees, cluster and rocket fragments,
			// Molotov fire, Stynger shards...) carries the soldier's tag so kills and loot credit the soldier, and is an NPC-style projectile so the commander's
			// on-hit effects and item drops never run for it.
			else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.TryGetGlobalProjectile(out SoldierProjectileTag parentTag) && parentTag.SoldierIndex >= 0)
			{
				SoldierIndex = parentTag.SoldierIndex;
				projectile.npcProj = true;
				projectile.noDropItem = true;
			}
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
			if (SoldierIndex >= 0 && IsSporeTrap(projectile.type)) AgeSporeTrapFromSoldier(projectile);
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
			if (IsSporeTrap(projectile.type)) { _sporeAgeBefore = projectile.ai[0]; return true; } // vanilla AI runs; PostAI then re-ages it from the soldier
			if (projectile.type == ProjectileID.FlyingKnife) { DriveSoldierFlyingKnife(projectile); return false; }
			if (IsGuidedMissile(projectile.type)) { DriveSoldierGuidedMissile(projectile); return false; }
			if (projectile.type == ProjectileID.SoulDrain) { DriveSoldierSoulDrain(projectile); return false; }
			if (projectile.type == ProjectileID.MedusaHead) { DriveSoldierMedusaHead(projectile); return false; }
			return true;
		}

		// ============================================================================================================
		// Spore Sac traps (aiStyle 105): vanilla ages a trap by 1 a tick plus a term that grows with its distance from Main.player[owner] (and is +100 a tick when that
		// player is not wearing the sac), so traps far from their wearer wither fast. A soldier's trap is owned by the commander, who is usually far away and wears no
		// sac, so it would be dead within a second without ever hurting anything.
		// ============================================================================================================
		private float _sporeAgeBefore;

		private static bool IsSporeTrap(int type) => type == ProjectileID.SporeTrap || type == ProjectileID.SporeTrap2;

		/// <summary>
		/// After the vanilla step: rewrite the age so the distance term is measured from the soldier that dropped the trap (the same formula, as a wearer of the sac).
		/// Peers without the soldier's stat pipeline (clients) get the same result, since only the body's presence and position are read. A trap whose soldier is gone
		/// withers at the rate vanilla gives an owner without the sac.
		/// </summary>
		private void AgeSporeTrapFromSoldier(Projectile p)
		{
			float aged = _sporeAgeBefore + 1f; // the vanilla +1; past the cap vanilla is already fading the trap and adds nothing more
			if (aged > LevyConstants.SporeTrapMaxAge) return;
			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			float extra = LevyConstants.SporeOrphanAgePerTick;
			if (s != null && s.active && s.ModNPC is SoldierNPC)
			{
				extra = Vector2.Distance(p.Center, s.Center) / LevyConstants.SporeAgeDistDivisor;
				foreach (var (over, mult) in LevyConstants.SporeAgeFalloff) if (extra > over) extra *= mult;
			}
			p.ai[0] = aged + extra;
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
					// Fly at an intercept point with turn authority that grows as it closes and a speed that ramps up while lined up, so it connects instead of circling.
					HomeIn(p, LeadPoint(p, target), LevyConstants.GuidedAccel, _guidedSpeed * LevyConstants.GuidedMaxSpeedMul, _guidedSpeed * LevyConstants.GuidedMinSpeedMul,
						LevyConstants.HomeTurnFar, LevyConstants.HomeTurnNear, LevyConstants.GuidedNearRangePx);
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
				// Dive straight at the enemy (intercept point, turn authority rising as it closes); after passing through it snaps back round for the next strike.
				HomeIn(p, LeadPoint(p, target), LevyConstants.KnifeSeekAccel, LevyConstants.KnifeSeekMaxSpeed, LevyConstants.KnifeMinSpeed,
					LevyConstants.HomeTurnFar, LevyConstants.HomeTurnNear, LevyConstants.HomeNearRangePx);
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
			HomeIn(p, s.Center, LevyConstants.KnifeReturnAccel, LevyConstants.KnifeReturnMaxSpeed, LevyConstants.KnifeMinSpeed,
				LevyConstants.HomeTurnFar, LevyConstants.HomeTurnNear, LevyConstants.HomeNearRangePx);
		}

		/// <summary>The soldier's own engaged enemy if it is still a valid hostile; else null. The knife mirrors the soldier's aim and never picks a target itself.</summary>
		private static NPC CurrentKnifeTarget(SoldierNPC body)
		{
			int ti = body.TargetIndex;
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			return t.active && t.life > 0 && !SoldierTargeting.IsNeverTarget(t) ? t : null;
		}

		/// <summary>
		/// Converging homing shared by the knife and the guided missiles. The heading rotates toward the destination by at most a turn rate that blends from
		/// <paramref name="turnFar"/> to <paramref name="turnNear"/> as the distance falls inside <paramref name="nearRange"/> (so it can always curve into the target),
		/// and the speed ramps toward <paramref name="maxSpeed"/> while the projectile is lined up, bleeding toward <paramref name="minSpeed"/> when it must swing round
		/// after an overshoot (a slower projectile has a tighter turning circle). Deterministic and smooth: no per-tick jitter.
		/// </summary>
		private static void HomeIn(Projectile p, Vector2 dest, float accel, float maxSpeed, float minSpeed, float turnFar, float turnNear, float nearRange)
		{
			Vector2 to = dest - p.Center;
			float dist = to.Length();
			if (dist < 0.001f) return;
			float sp = p.velocity.Length();
			if (sp < 0.01f) { p.velocity = to / dist * minSpeed; sp = minSpeed; }
			float cur = p.velocity.ToRotation();
			float diff = MathHelper.WrapAngle(to.ToRotation() - cur);
			float near = 1f - MathHelper.Clamp(dist / System.Math.Max(1f, nearRange), 0f, 1f);
			float maxTurn = MathHelper.Lerp(turnFar, turnNear, near);
			float turn = MathHelper.Clamp(diff, -maxTurn, maxTurn);
			float align = System.MathF.Cos(diff); // 1 = dead ahead, -1 = target behind
			float want = MathHelper.Lerp(minSpeed, maxSpeed, MathHelper.Clamp(align * 2f - 0.5f, 0f, 1f));
			sp = sp < want ? System.Math.Min(want, sp + accel) : System.Math.Max(want, sp - accel * 2f);
			p.velocity = new Vector2(System.MathF.Cos(cur + turn), System.MathF.Sin(cur + turn)) * sp;
		}

		/// <summary>The point to fly at to meet a moving target: its position advanced by its velocity over the (capped) time to reach it.</summary>
		private static Vector2 LeadPoint(Projectile p, NPC target)
		{
			float sp = System.Math.Max(8f, p.velocity.Length());
			float t = System.Math.Min(Vector2.Distance(p.Center, target.Center) / sp, 20f);
			return target.Center + target.velocity * t * 0.7f;
		}

		// ============================================================================================================
		// Soul Drain (ProjectileID.SoulDrain, aiStyle 185): vanilla pays the commander's mana on hit; the soldier already paid its cast mana in WeaponUse.FireMagic.
		// ============================================================================================================
		private int _auxAge;
		private bool _drainSpent;

		/// <summary>
		/// The drain bolt is a stationary 200x200 blast that vanilla drops on the cursor. A soldier's lands on its engaged target (as far along the line as the ground allows),
		/// never reads the commander (no mana, no steering), and gives the soldier a little HP back when it connects. No live target: it stays where it was cast.
		/// </summary>
		private void DriveSoldierSoulDrain(Projectile p)
		{
			p.velocity = Vector2.Zero;
			p.tileCollide = false;
			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			SoldierNPC body = s != null && s.active ? s.ModNPC as SoldierNPC : null;
			bool first = _auxAge++ == 0;
			if (first && body != null)
			{
				bool engaging = (body.State == SoldierState.Engage || body.State == SoldierState.Shaken || body.State == SoldierState.Routed) && !body.Record.HoldFire;
				NPC target = engaging ? CurrentKnifeTarget(body) : null;
				if (target != null)
				{
					Vector2 at = FarthestOnLine(s.Center, target.Center, LevyConstants.SoulDrainReachPx);
					if (at != s.Center) { p.Center = at; p.netUpdate = true; }
				}
			}
			if (!_drainSpent && body != null && body.IsAlive && LevyUtil.IsServerOrSingle)
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.townNPC || n.dontTakeDamage || n.friendly || SoldierTargeting.IsNeverTarget(n) || !p.Hitbox.Intersects(n.Hitbox)) continue;
					_drainSpent = true;
					int heal = System.Math.Min(LevyConstants.SoulDrainHealPerHit, s.lifeMax - s.life);
					if (heal > 0) { s.life += heal; body.Record.CurrentHp = s.life; }
					break;
				}
			}
			if (first && Main.netMode != NetmodeID.Server)
			{
				for (int k = 0; k < 6; k++)
				{
					Vector2 at = p.Center + new Vector2(Main.rand.Next(-60, 61), Main.rand.Next(-60, 61));
					Vector2 toward = body != null ? s.Center - at : Vector2.Zero;
					if (toward != Vector2.Zero) toward = Vector2.Normalize(toward) * 3f;
					Dust d = Dust.NewDustPerfect(at, 235, toward, 0, default, Main.rand.Next(70, 85) * 0.01f);
					d.noGravity = true;
				}
			}
			Lighting.AddLight(p.Center, 0.35f, 0.1f, 0.45f);
		}

		/// <summary>Walks from <paramref name="from"/> toward <paramref name="to"/> in tile steps (up to <paramref name="maxDist"/>) and returns the last open point.</summary>
		private static Vector2 FarthestOnLine(Vector2 from, Vector2 to, float maxDist)
		{
			Vector2 d = to - from;
			float len = d.Length();
			if (len < 1f) return from;
			Vector2 dir = d / len;
			float reach = System.Math.Min(len, maxDist);
			Vector2 last = from;
			for (float t = 16f; ; t += 16f)
			{
				float step = System.Math.Min(t, reach);
				Vector2 pt = from + dir * step;
				if (Collision.SolidCollision(pt - new Vector2(4f, 4f), 8, 8)) break;
				last = pt;
				if (step >= reach) break;
			}
			return last;
		}

		// ============================================================================================================
		// Medusa Head (ProjectileID.MedusaHead, aiStyle 100): vanilla anchors to Main.player[owner], dies unless the commander channels, aims at the mouse and pays the commander.
		// ============================================================================================================
		/// <summary>Ticks the head has left (refreshed by each cast), the vanilla pulse countdown, and whether the last cast's mana has not yet bought a pulse.</summary>
		private int _headLife, _headPulse;
		private bool _headUnspent;

		/// <summary>
		/// A soldier's head floats at the soldier, pulses (up to 3 nearest enemies within range and line of sight, engaged target first) exactly like the vanilla head,
		/// and never touches the commander. Each cast (paid by FireMagic from the soldier's own mana) keeps one head alive; further casts refresh it instead of stacking.
		/// A cast that bought no pulse (nothing in reach) is refunded to the SOLDIER's mana. The head fades when the soldier dies, swaps weapons, or stops casting.
		/// </summary>
		private void DriveSoldierMedusaHead(Projectile p)
		{
			bool authority = LevyUtil.IsServerOrSingle;
			NPC s = SoldierIndex < Main.maxNPCs ? Main.npc[SoldierIndex] : null;
			SoldierNPC body = s != null && s.active ? s.ModNPC as SoldierNPC : null;
			if (body == null || !body.IsAlive || body.Record == null)
			{
				if (authority) p.Kill(); // clients let timeLeft lapse; the authority's kill syncs
				return;
			}
			Item held = WeaponUse.HeldItem(body);
			if (authority && (held == null || held.IsAir || held.shoot != p.type)) { p.Kill(); return; }
			int cost = held != null && !held.IsAir ? System.Math.Max(0, held.mana) : 0;

			if (_auxAge++ == 0 && authority)
			{
				Projectile older = FindOlderHead(p);
				if (older != null && older.TryGetGlobalProjectile(out SoldierProjectileTag ot))
				{
					ot.RefundUnspentHead(body, cost); // the previous cast bought nothing: give it back, the new one takes over
					ot._headLife = LevyConstants.MedusaHeadLingerTicks; ot._headUnspent = true;
					p.Kill();
					return;
				}
				_headLife = LevyConstants.MedusaHeadLingerTicks; _headUnspent = true;
			}
			if (authority && --_headLife <= 0) { RefundUnspentHead(body, cost); p.Kill(); return; }

			p.timeLeft = 2;
			int dir = s.direction == 0 ? 1 : s.direction;
			p.direction = p.spriteDirection = dir;
			p.rotation = 0f;
			Vector2 oldVel = p.velocity;
			p.velocity = new Vector2(dir, 0f);
			if (oldVel != p.velocity) p.netUpdate = true;
			p.Center = (s.Center + new Vector2(dir * 8f, -16f) - p.velocity).Floor();

			bool charged = _headPulse > 0;
			if (_headPulse > 0) _headPulse--;
			AnimateHead(p, charged);

			if (authority && !body.Record.HoldFire && (!charged || _headPulse % 15 == 0)) PulseHead(p, body);
			Lighting.AddLight(p.Center, 0.9f, 0.75f, 0.1f);
		}

		private static Projectile FindOlderHead(Projectile self)
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || q.whoAmI == self.whoAmI || q.type != self.type) continue;
				if (q.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == self.GetGlobalProjectile<SoldierProjectileTag>().SoldierIndex && t._auxAge > 0) return q;
			}
			return null;
		}

		/// <summary>Credits the soldier (never the commander) with a cast that produced no pulse.</summary>
		private void RefundUnspentHead(SoldierNPC body, int cost)
		{
			if (!_headUnspent) return;
			_headUnspent = false;
			if (cost > 0 && body.Record != null) body.Record.Mana = System.Math.Min(body.Record.MaxMana, body.Record.Mana + cost);
		}

		/// <summary>The head's frame cycle: 0-7 idle, 8-11 while charged (the vanilla ranges).</summary>
		private static void AnimateHead(Projectile p, bool charged)
		{
			if (Main.projFrames[p.type] < 12) return;
			if (charged)
			{
				if (p.frame < 8 || p.frame >= 12) p.frame = 8;
				if (++p.frameCounter >= 5) { p.frameCounter = 0; if (++p.frame >= 12) p.frame = 8; }
			}
			else if (++p.frameCounter >= 5) { p.frameCounter = 0; if (++p.frame >= 8) p.frame = 0; }
		}

		/// <summary>One pulse: the nearest enemies in range with a clear line each take an instant ray hit (credited to the soldier); nothing in reach, no pulse.</summary>
		private void PulseHead(Projectile p, SoldierNPC body)
		{
			Vector2 pos = p.Center;
			var list = new System.Collections.Generic.List<(int idx, float key)>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active) continue;
				float d = p.Distance(n.Center);
				if (d >= LevyConstants.MedusaRangePx || !n.CanBeChasedBy(p) || SoldierTargeting.IsNeverTarget(n) || !Collision.CanHitLine(n.position, n.width, n.height, pos, 0, 0)) continue;
				list.Add((i, i == body.TargetIndex ? -1f : d)); // the soldier's engaged target is always struck first
			}
			if (list.Count == 0) return;
			list.Sort((a, b) => a.key.CompareTo(b.key));

			float angleSum = 0f;
			int hit = 0;
			for (int k = 0; k < list.Count && k < LevyConstants.MedusaMaxRays; k++)
			{
				NPC n = Main.npc[list[k].idx];
				Vector2 v = n.Center - pos;
				angleSum += v.ToRotation();
				hit++;
				int ri = Projectile.NewProjectile(p.GetSource_FromThis(), pos.X, pos.Y, v.X, v.Y, ProjectileID.MedusaHeadRay, 0, 0f, p.owner, p.whoAmI);
				if (ri < 0 || ri >= Main.maxProjectiles) continue;
				Projectile ray = Main.projectile[ri];
				WeaponUse.TagSoldierProjectile(body, ray, DamageClass.Magic);
				ray.Center = n.Center;
				ray.damage = p.damage;
				ray.Damage();
				ray.damage = 0;
				ray.Center = pos;
			}
			float aim = hit > 0 ? angleSum / hit : (p.direction == 1 ? 0f : MathHelper.Pi);
			for (int l = 0; l < 4; l++)
			{
				Vector2 spread = Main.rand.NextBool(4)
					? Vector2.UnitX.RotatedByRandom(MathHelper.TwoPi) * new Vector2(200f, 50f) * (Main.rand.NextFloat() * 0.7f + 0.3f)
					: Vector2.UnitX.RotatedByRandom(MathHelper.Pi).RotatedBy(aim) * new Vector2(200f, 50f) * (Main.rand.NextFloat() * 0.7f + 0.3f);
				Projectile.NewProjectile(p.GetSource_FromThis(), pos.X, pos.Y, spread.X, spread.Y, ProjectileID.MedusaHeadRay, 0, 0f, p.owner, p.whoAmI);
			}
			_headUnspent = false;
			_headPulse = LevyConstants.MedusaPulseTicks;
			p.netUpdate = true;
			if (Main.netMode != NetmodeID.Server) SoundEngine.PlaySound(SoundID.Item17, pos);
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
