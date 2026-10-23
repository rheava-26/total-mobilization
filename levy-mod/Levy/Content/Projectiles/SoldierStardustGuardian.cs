using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// The Stardust Guardian of a soldier in the full Stardust set (Stardust Helmet + Breastplate + Leggings): ai[0] = the soldier's whoAmI, ai[1] = the enemy it is punching + 1 (0 = resting beside him).
	/// Vanilla's Guardian (ProjectileID.StardustGuardian, aiStyle 120) is cast by Player.UpdateArmorSets for a REAL player (it is gated on whoAmI == Main.myPlayer, which a soldier's hidden dummy is
	/// deliberately never) and then reads everything off Main.player[owner]: his position and facing (where it rests), his stardustGuardian flag (what keeps it alive), the buff handler that owns its
	/// lifetime, and, for a soldier's shot, the owner is the commander. So this is a port of that AI with the soldier in the wearer's place, drawn with vanilla's sprite and punching with vanilla's fist.
	/// <para/>It is the vanilla behaviour and numbers, tied to the soldier instead of a player: it hangs behind the wearer, picks an enemy within 500 px of him (his own target first), flies up beside it and
	/// throws punches through an 80 x 40 fist box for 30 summon-scaled damage and 10 knockback, one hit per enemy every 5 ticks, returns to him when the enemy is gone or has strayed past 500 px. The
	/// soldier's hold-fire order stands it down. It is not a minion (a minion's damage would be re-derived from the COMMANDER's summon stats every tick); the damage is the soldier's own
	/// (<see cref="SoldierNPC.StardustGuardianDamage"/>), refreshed by <see cref="SoldierNPC"/>'s upkeep. SoldierNPC.MaintainStardustGuardian keeps exactly one up while the set is worn.
	/// <para/>MULTIPLAYER: the server (or single player) alone decides every state change (target, release), spawns it and ends it; remote screens run the same movement between syncs. Damage, kill
	/// credit and the punch spark are server-side like every soldier shot.
	/// </summary>
	public class SoldierStardustGuardian : ModProjectile
	{
		/// <summary>No asset of its own: vanilla's Guardian sprite sheet (19 frames: 0-7 idle, 12-18 punching).</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StardustGuardian;

		public override void SetStaticDefaults() => Main.projFrames[Type] = LevyConstants.StardustGuardianFrames;

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.StardustGuardianWidthPx;
			Projectile.height = LevyConstants.StardustGuardianHeightPx;
			Projectile.aiStyle = -1;
			Projectile.friendly = true; // it only ever damages while it punches (CanDamage / Colliding below)
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.alpha = 255; // materialises, in a flash of dust, on its first tick
			Projectile.timeLeft = LevyConstants.StardustGuardianTimeLeftTicks;
			Projectile.netImportant = true;
			Projectile.manualDirectionChange = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.StardustGuardianHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.knockBack = LevyConstants.StardustGuardianKnockback;
		}

		/// <summary>The enemy an idle scan settled on (not synced; the punch target rides ai[1]) and the tick of the last punch spark.</summary>
		private int _idleTarget = -1;
		private uint _lastFxTick;

		private bool Attacking => Projectile.ai[1] > 0f;
		private int PunchTarget => (int)Projectile.ai[1] - 1;

		private SoldierNPC Soldier
		{
			get
			{
				int i = (int)Projectile.ai[0];
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				return n.active && n.ModNPC is SoldierNPC s && s.Record != null ? s : null;
			}
		}

		public override bool? CanCutTiles() => false;
		public override bool MinionContactDamage() => false;

		/// <summary>Punches only: idle (or told to hold fire) it deals nothing.</summary>
		public override bool? CanDamage()
		{
			if (!Attacking) return false;
			SoldierNPC s = Soldier;
			return s != null && s.Record.HoldFire ? false : null;
		}

		/// <summary>Vanilla's fist (Projectile.Colliding, type 623): an 80 x 40 box centred 40 px in front of the Guardian while it punches.</summary>
		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!Attacking) return false;
			Rectangle fist = Utils.CenteredRectangle(Projectile.Center + new Vector2(Projectile.direction * LevyConstants.StardustGuardianFistReachPx, 0f),
				new Vector2(LevyConstants.StardustGuardianFistWidthPx, LevyConstants.StardustGuardianFistHeightPx));
			return fist.Intersects(targetHitbox);
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		/// <summary>It is a summon: an enemy a whip tagged takes the same bonus the soldiers' other minions get.</summary>
		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (LevyNpcData.IsNpcTagged(target)) modifiers.SourceDamage += LevyConstants.TagMinionDamageBonus;
		}

		/// <summary>Vanilla's punch spark (ParticleOrchestraType.StardustPunch) at the knuckles, broadcast to every screen, at most a few a second per Guardian so a squad does not flood the wire.</summary>
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient || Main.GameUpdateCount - _lastFxTick < (uint)LevyConstants.StardustGuardianPunchFxTicks) return;
			_lastFxTick = Main.GameUpdateCount;
			try
			{
				var settings = new ParticleOrchestraSettings
				{
					PositionInWorld = Vector2.Lerp(Projectile.Center, target.Hitbox.ClosestPointInRect(Projectile.Center), 0.5f) + new Vector2(0f, Main.rand.NextFloatDirection() * 10f),
					MovementVector = new Vector2(Projectile.direction, Main.rand.NextFloatDirection() * 0.5f) * (3f + 3f * Main.rand.NextFloat()),
				};
				if (Main.netMode == NetmodeID.SinglePlayer) ParticleOrchestrator.SpawnParticlesDirect(ParticleOrchestraType.StardustPunch, settings);
				else ParticleOrchestrator.BroadcastParticleSpawn(ParticleOrchestraType.StardustPunch, settings);
			}
			catch (Exception) { /* a missing spark must never cost a punch */ }
		}

		// ------------------------------------------------------------------------------------------------------------
		// AI: Projectile.AI_120_StardustGuardian with the soldier as the wearer (states 0 resting and 2 punching; vanilla's 1 and 3 belong to the player's minion-rest-point command, which a soldier does not give)
		// ------------------------------------------------------------------------------------------------------------
		public override void AI()
		{
			bool authority = Main.netMode != NetmodeID.MultiplayerClient;
			Projectile.timeLeft = LevyConstants.StardustGuardianTimeLeftTicks; // the authority ends it; a remote screen only follows
			SoldierNPC s = Soldier;
			if (s == null || (authority && (!s.IsAlive || !s.WearsStardustSet)))
			{
				// A client may see the Guardian before its soldier's body arrives: let the server decide when it is really gone.
				if (authority) Projectile.Kill();
				return;
			}

			NPC body = s.NPC;
			int dir = body.direction == 0 ? 1 : body.direction;
			// Vanilla: the wearer's centre, (5 + width / 2) behind him against his facing, 25 up.
			Vector2 rest = body.Center + new Vector2(-(LevyConstants.StardustGuardianRestBackPx + body.width / 2) * dir, -LevyConstants.StardustGuardianRestLiftPx);
			bool hold = s.Record.HoldFire;

			if (Projectile.alpha == 255) { Projectile.alpha = 0; Poof(); }
			Lighting.AddLight(Projectile.Center, 0.9f, 0.9f, 0.7f);

			// A recall or a teleport leaves the wearer far away: join him in a flash instead of drifting across the map.
			if (Vector2.DistanceSquared(Projectile.Center, rest) > LevyConstants.StardustGuardianTeleportDistPx * LevyConstants.StardustGuardianTeleportDistPx)
			{
				Poof();
				Projectile.Center = rest;
				Projectile.velocity = Vector2.Zero;
				Projectile.ai[1] = 0f;
				_idleTarget = -1;
				Poof();
				if (authority) Projectile.netUpdate = true;
			}

			if (Attacking) PunchStep(s, body, authority, hold);
			else RestStep(s, body, rest, dir, authority, hold);

			if (Main.netMode == NetmodeID.Server && ((uint)Projectile.whoAmI + Main.GameUpdateCount) % (uint)LevyConstants.StardustGuardianNetRefreshTicks == 0) Projectile.netUpdate = true;
		}

		/// <summary>State 0: glide to the resting place, face the way the wearer faces, and look for an enemy; one found within reach starts the punching.</summary>
		private void RestStep(SoldierNPC s, NPC body, Vector2 rest, int dir, bool authority, bool hold)
		{
			Projectile.Center = Vector2.Lerp(Projectile.Center, rest, LevyConstants.StardustGuardianFollowLerp);
			Projectile.velocity *= 0.5f;
			Projectile.direction = Projectile.spriteDirection = dir;
			if (++Projectile.frameCounter >= LevyConstants.StardustGuardianIdleFrameTicks)
			{
				Projectile.frameCounter = 0;
				if (++Projectile.frame >= LevyConstants.StardustGuardianIdleFrames) Projectile.frame = 0;
			}

			// Vanilla only looks while it is within 170 px of its resting place; a scan every few ticks (staggered) is plenty, and the pick is dropped the moment it stops being valid.
			int target = -1;
			if (!hold && Math.Abs(Projectile.Center.X - rest.X) <= LevyConstants.StardustGuardianLeashPx + LevyConstants.StardustGuardianScanSlackPx)
			{
				if (((int)Main.GameUpdateCount + Projectile.whoAmI) % LevyConstants.StardustGuardianScanTicks == 0) _idleTarget = FindTarget(s, body);
				else if (ValidTarget(_idleTarget, body.Center, LevyConstants.StardustGuardianLookupPx) == null) _idleTarget = -1;
				target = _idleTarget;
			}
			else _idleTarget = -1;
			if (target < 0) return;

			NPC t = Main.npc[target];
			Projectile.direction = Projectile.spriteDirection = t.Center.X > Projectile.Center.X ? 1 : -1;
			float leash = LevyConstants.StardustGuardianLeashPx, band = LevyConstants.StardustGuardianVerticalPx;
			float awayX = Math.Abs(rest.X - Projectile.Center.X), toX = Math.Abs(t.Center.X - Projectile.Center.X);
			float awayY = Math.Abs(rest.Y - Projectile.Center.Y), toY = Math.Abs(t.Center.Y - Projectile.Bottom.Y);
			float vert = t.Center.Y > Projectile.Bottom.Y ? 1f : -1f;
			if ((awayX < leash || (rest.X - Projectile.Center.X) * Projectile.direction < 0f) && toX > 20f && toX < leash - awayX + 100f) Projectile.velocity.X += 0.1f * Projectile.direction;
			else Projectile.velocity.X *= 0.7f;
			if ((awayY < band || (rest.Y - Projectile.Bottom.Y) * vert < 0f) && toY > 10f && toY < band - awayY + 10f) Projectile.velocity.Y += 0.1f * vert;
			else Projectile.velocity.Y *= 0.7f;
			if (authority && toX < LevyConstants.StardustGuardianAttackPx)
			{
				Projectile.ai[1] = target + 1;
				Projectile.netUpdate = true;
			}
		}

		/// <summary>State 2: close on the enemy (hanging off its near side), punching; let go when it is dead, out of reach or the soldier is told to hold fire.</summary>
		private void PunchStep(SoldierNPC s, NPC body, bool authority, bool hold)
		{
			int step = LevyConstants.StardustGuardianPunchFrameTicks;
			if (Projectile.frame < LevyConstants.StardustGuardianPunchFirstFrame) Projectile.frame = LevyConstants.StardustGuardianPunchFirstFrame;
			if (Projectile.frame == LevyConstants.StardustGuardianPunchFirstFrame || Projectile.frame == LevyConstants.StardustGuardianPunchFirstFrame + 1) step = LevyConstants.StardustGuardianWindUpFrameTicks;
			if (++Projectile.frameCounter >= step)
			{
				Projectile.frameCounter = 0;
				if (++Projectile.frame >= LevyConstants.StardustGuardianPunchEndFrame) Projectile.frame = LevyConstants.StardustGuardianPunchLoopFrame;
			}

			bool near = Vector2.Distance(Projectile.Center, body.Center) < LevyConstants.StardustGuardianLookupPx;
			NPC t = hold ? null : ValidTarget(PunchTarget, Projectile.Center, LevyConstants.StardustGuardianAttackPx);
			if (!near || t == null)
			{
				Projectile.ai[1] = 0f;
				_idleTarget = -1;
				if (authority) Projectile.netUpdate = true;
				if (Projectile.frame < LevyConstants.StardustGuardianReleaseFrame) Projectile.frame = LevyConstants.StardustGuardianReleaseFrame;
				return;
			}

			int side = t.Center.X - body.Center.X > 1f ? 1 : -1;
			Vector2 to = t.Center + new Vector2(-side * (t.width * 0.5f + LevyConstants.StardustGuardianStandoffPx), LevyConstants.StardustGuardianStandoffLiftPx) - Projectile.Center;
			float reach = LevyConstants.StardustGuardianChaseSpeed * Utils.Remap(to.Length(), LevyConstants.StardustGuardianChaseNearPx, LevyConstants.StardustGuardianChaseFarPx, 1f, LevyConstants.StardustGuardianChaseMaxMult);
			Projectile.velocity = Projectile.velocity.MoveTowards(Vector2.Zero.MoveTowards(to, reach), LevyConstants.StardustGuardianChaseAccel);
			Projectile.direction = Projectile.spriteDirection = side;

			if (Projectile.localAI[0]++ >= LevyConstants.StardustGuardianRetargetTicks)
			{
				Projectile.localAI[0] = 0f;
				if (!authority) return;
				int better = FindTarget(s, body);
				if (better >= 0 && better + 1 != (int)Projectile.ai[1])
				{
					Projectile.ai[1] = better + 1;
					Projectile.netUpdate = true;
				}
			}
		}

		/// <summary>
		/// The enemy to punch: the soldier's own target when it is within reach of him (the commander's focus designation is already folded into it), else the nearest fair enemy within
		/// the lookup range of the soldier (vanilla's metric: distance from the wearer). -1 when there is none.
		/// </summary>
		private int FindTarget(SoldierNPC s, NPC body)
		{
			float range = LevyConstants.StardustGuardianLookupPx;
			Vector2 home = body.Center;
			int pref = s.TargetIndex;
			if (pref >= 0 && pref < Main.maxNPCs && Eligible(Main.npc[pref]) && Vector2.Distance(home, Main.npc[pref].Center) < range) return pref;
			int best = -1;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !Eligible(n)) continue;
				float d = Vector2.Distance(home, n.Center);
				if (d < range) { best = i; range = d; }
			}
			return best;
		}

		/// <summary>A live hostile a soldier may fight (vanilla's CanBeChasedBy, plus the army's critter and never-target rules).</summary>
		private bool Eligible(NPC n) => n.active && n.life > 0 && n.CanBeChasedBy(Projectile) && LevyUtil.IsHostileTarget(n) && !SoldierTargeting.IsNeverTarget(n);

		private NPC ValidTarget(int index, Vector2 from, float reach)
		{
			if (index < 0 || index >= Main.maxNPCs) return null;
			NPC n = Main.npc[index];
			return Eligible(n) && Vector2.Distance(from, n.Center) <= reach ? n : null;
		}

		/// <summary>Vanilla's materialise flash (thirty pairs of dust, dust 135).</summary>
		private void Poof()
		{
			if (Main.dedServ || !LevyUtil.IsOnScreen(Projectile.Hitbox)) return; // an army that arrives off screen (a world load) spends nothing on the picture
			for (int i = 0; i < LevyConstants.StardustGuardianPoofDust; i++)
			{
				int a = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 200, default, 1.7f);
				Main.dust[a].noGravity = true;
				Main.dust[a].velocity *= 3f;
				int b = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100);
				Main.dust[b].velocity *= 2f;
				Main.dust[b].noGravity = true;
				Main.dust[b].fadeIn = 2.5f;
			}
		}

		/// <summary>Dismissed (its soldier died or took the set off): it leaves in the same flash it arrived in. A Guardian that merely timed out on a screen that lost its soldier leaves quietly.</summary>
		public override void OnKill(int timeLeft)
		{
			if (timeLeft > 0) Poof();
		}

		// ------------------------------------------------------------------------------------------------------------
		// Draw: Main.DrawProj for type 623 (full-bright, half-additive, shaking while it punches) plus the fist streaks of DrawProj_StardustGuardianPunching
		// ------------------------------------------------------------------------------------------------------------
		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Rectangle frame = tex.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
			Vector2 origin = frame.Size() / 2f;
			Vector2 pos = Projectile.Center + new Vector2(0f, Projectile.gfxOffY) - Main.screenPosition;
			bool punching = Attacking;
			if (!Main.gamePaused && punching) pos += Main.rand.NextVector2Circular(2f, 2f);
			int bright = 255 - Projectile.alpha;
			Color color = new Color(bright, bright, bright, bright);
			color.A /= 2;
			SpriteEffects fx = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Main.EntitySpriteDraw(tex, pos, frame, color, Projectile.rotation, origin, Projectile.scale, fx);
			if (punching && Projectile.frame >= LevyConstants.StardustGuardianPunchLoopFrame) DrawFistStreaks();
			return false;
		}

		/// <summary>The streaks trailing off the fist (Main.DrawProj_StardustGuardianPunching): six faint bolts thrown ahead of the Guardian, redrawn at random every frame.</summary>
		private void DrawFistStreaks()
		{
			if (Main.gamePaused) return;
			Texture2D streak = TextureAssets.Extra[46].Value;
			Vector2 origin = streak.Size() / 2f;
			Vector2 from = Projectile.Center - Projectile.rotation.ToRotationVector2() * 20f * Projectile.spriteDirection;
			for (int i = 0; i < 6; i++)
			{
				float t = Main.rand.NextFloat();
				float fade = Utils.GetLerpValue(0f, 0.3f, t, true) * Utils.GetLerpValue(1f, 0.5f, t, true);
				float grow = MathHelper.Lerp(0.6f, 1f, fade);
				Color bright = new Color(255, 255, 255, 127) * (fade * 0.5f);
				Color soft = Color.White * fade;
				soft.A /= 2;
				soft *= 0.5f;
				float length = 1f + Main.rand.NextFloat() * 0.5f;
				float wobble = Main.rand.NextFloatDirection();
				Vector2 scale = new Vector2(0.8f) * length * grow;
				float distance = 20f + MathHelper.Lerp(0f, 20f, t) + length * 16f;
				float angle = Projectile.rotation + (Projectile.direction == 1 ? 0f : MathHelper.Pi) + wobble * MathHelper.TwoPi * 0.04f;
				Vector2 at = from + angle.ToRotationVector2() * distance + Main.rand.NextVector2Circular(20f, 20f) - Main.screenPosition;
				Main.spriteBatch.Draw(streak, at, null, bright, angle + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0f);
				Main.spriteBatch.Draw(streak, at, null, soft, angle + MathHelper.PiOver2, origin, scale * 0.8f, SpriteEffects.None, 0f);
			}
		}
	}
}
