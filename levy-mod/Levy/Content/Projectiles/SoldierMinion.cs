using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// One minion of a summoner soldier: ai[0] = soldier whoAmI, ai[1] = the summon weapon's minion projectile type (drawn as that sprite),
	/// ai[2] = chased NPC index + 1 (0 = none). Hovers around its soldier, dives at the nearest hostile (tagged ones first), backs off after
	/// each hit, and goes home when it has nothing to chase. Owned by Main.myPlayer like every soldier projectile; it vanishes with its soldier.
	/// </summary>
	public class SoldierMinion : ModProjectile
	{
		/// <summary>SoldierMinion.png is a placeholder; PreDraw draws the real minion's texture when it resolves.</summary>
		public override void SetStaticDefaults()
		{
			Main.projFrames[Type] = 1;
		}

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.MinionHitboxPx;
			Projectile.height = LevyConstants.MinionHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.MinionLifeTicks;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.MinionLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.netImportant = true;
			Projectile.DamageType = DamageClass.Summon;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int VisualType => (int)Projectile.ai[1];

		public SoldierNPC Soldier
		{
			get
			{
				int i = SoldierIndex;
				if (i < 0 || i >= Main.maxNPCs) return null;
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null) return null;
				return s;
			}
		}

		public override bool? CanCutTiles() => false;
		public override bool MinionContactDamage() => false;

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.IsAlive)
			{
				// A client may see the minion before the body's record sync: let the server decide when it is really gone.
				if (Main.netMode == NetmodeID.MultiplayerClient) return;
				Projectile.Kill(); return;
			}

			Projectile.timeLeft = LevyConstants.MinionLifeTicks; // refreshed while the soldier lives
			float tick = ++Projectile.localAI[0];
			bool authoritative = Main.netMode != NetmodeID.MultiplayerClient;

			// ---- target ----
			if (authoritative && ((int)tick + Projectile.whoAmI) % LevyConstants.MinionRetargetTicks == 0)
			{
				int enc = (s.Record.HoldFire ? -1 : FindTarget(s)) + 1;
				if (enc != (int)Projectile.ai[2]) { Projectile.ai[2] = enc; Projectile.netUpdate = true; }
			}
			NPC target = CurrentTarget(s);

			// ---- movement ----
			if (Projectile.localAI[1] > 0f)
			{
				// recoil after a hit: keep moving away from the victim, slowing, before diving again
				Projectile.localAI[1] -= 1f;
				Projectile.velocity *= 0.94f;
			}
			else if (target != null)
			{
				Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * LevyConstants.MinionDashSpeed;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, LevyConstants.MinionTurnLerp);
			}
			else
			{
				Vector2 home = OrbitPoint(s, tick);
				Vector2 to = home - Projectile.Center;
				float dist = to.Length();
				if (dist > LevyConstants.MinionTeleportDistPx)
				{
					Projectile.Center = s.NPC.Center + new Vector2(0f, -LevyConstants.MinionOrbitLiftPx);
					Projectile.velocity = Vector2.Zero;
					if (authoritative) Projectile.netUpdate = true;
				}
				else
				{
					Vector2 want = dist < 1f ? Vector2.Zero : to / dist * Math.Min(LevyConstants.MinionIdleSpeed, dist * 0.18f + 0.5f);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.18f);
				}
			}

			// keep the shared state of the flock fresh on remote screens
			if (Main.netMode == NetmodeID.Server && ((int)tick + Projectile.whoAmI) % 10 == 0) Projectile.netUpdate = true;

			// ---- look ----
			if (Math.Abs(Projectile.velocity.X) > 0.4f) Projectile.spriteDirection = Projectile.direction = Projectile.velocity.X < 0f ? -1 : 1;
			Projectile.rotation = Projectile.velocity.X * 0.04f;
		}

		/// <summary>Where this minion hovers while idle: a slow circle over the soldier's head, each minion at its own phase and radius.</summary>
		private Vector2 OrbitPoint(SoldierNPC s, float tick)
		{
			float phase = Projectile.whoAmI * 1.9f + tick * LevyConstants.MinionOrbitSpeed;
			float radius = LevyConstants.MinionOrbitRadiusPx + (Projectile.whoAmI % 3) * (LevyConstants.MinionOrbitSpreadPx * 0.5f);
			return s.NPC.Center + new Vector2((float)Math.Cos(phase) * radius, -LevyConstants.MinionOrbitLiftPx + (float)Math.Sin(phase) * radius * 0.45f);
		}

		/// <summary>The chased NPC if still valid (alive, hostile, near the soldier), else null.</summary>
		private NPC CurrentTarget(SoldierNPC s)
		{
			int idx = (int)Projectile.ai[2] - 1;
			if (idx < 0 || idx >= Main.maxNPCs) return null;
			NPC n = Main.npc[idx];
			if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) return null;
			if (Vector2.DistanceSquared(n.Center, s.NPC.Center) > LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx) return null;
			return n;
		}

		/// <summary>Nearest hostile to this minion inside the seek range and the soldier's leash; tagged enemies and the soldier's own target count as closer.</summary>
		private int FindTarget(SoldierNPC s)
		{
			float seekSq = LevyConstants.MinionSeekRangePx * LevyConstants.MinionSeekRangePx;
			float leashSq = LevyConstants.MinionLeashPx * LevyConstants.MinionLeashPx;
			float tagFactor = LevyConstants.MinionTaggedPreference * LevyConstants.MinionTaggedPreference;
			int best = -1; float bestScore = float.MaxValue;
			Vector2 me = Projectile.Center, home = s.NPC.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.life <= 0 || !LevyUtil.IsHostileTarget(n) || SoldierTargeting.IsNeverTarget(n)) continue;
				Vector2 c = n.Center;
				float dSq = Vector2.DistanceSquared(me, c);
				if (dSq > seekSq || Vector2.DistanceSquared(home, c) > leashSq) continue;
				float score = dSq;
				if (LevyNpcData.IsNpcTagged(n)) score *= tagFactor;
				if (i == s.TargetIndex) score *= 0.5f;
				if (score < bestScore) { bestScore = score; best = i; }
			}
			return best;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (LevyNpcData.IsNpcTagged(target)) modifiers.SourceDamage += LevyConstants.TagMinionDamageBonus;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark are applied by LootCreditGlobalProjectile for every tagged soldier projectile.
			Vector2 away = (Projectile.Center - target.Center).SafeNormalize(-Vector2.UnitY);
			Projectile.velocity = away * (LevyConstants.MinionDashSpeed * 0.6f);
			Projectile.localAI[1] = LevyConstants.MinionRecoilTicks;
			Projectile.netUpdate = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				int t = VisualType;
				if (t <= 0 || t >= TextureAssets.Projectile.Length || t == Type) return true;
				Main.instance.LoadProjectile(t);
				var asset = TextureAssets.Projectile[t];
				if (asset == null || !asset.IsLoaded) return true;
				Texture2D tex = asset.Value;
				int frames = Math.Max(1, Main.projFrames[t]);
				int frame = (int)(Projectile.localAI[0] / Math.Max(1, LevyConstants.MinionFrameTicks)) % frames;
				Rectangle src = tex.Frame(1, frames, 0, frame);
				Vector2 origin = src.Size() * 0.5f;
				SpriteEffects fx = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
				// Vanilla minion sprites face left by default; flipped when travelling right.
				Color c = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
				Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, c, Projectile.rotation, origin, 1f, fx);
				return false;
			}
			catch (Exception) { return true; }
		}
	}
}
