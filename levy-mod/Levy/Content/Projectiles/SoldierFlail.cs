using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// A soldier's flail: ai[0] = soldier whoAmI, ai[1] = flail item type (its own projectile sprite is the ball), ai[2] = life in ticks. velocity is the unit aim direction.
	/// Levy-owned and anchored to the soldier every tick (like <see cref="SoldierThrust"/>): the ball is thrown out along the aim over the first part of its life,
	/// swings in a small circle at the end of its chain, then is hauled back. It hurts whatever the ball crosses, each enemy at most once per local cooldown.
	/// It never reads the owning player and never touches the commander's held-projectile state. One out per soldier.
	/// </summary>
	public class SoldierFlail : ModProjectile
	{
		/// <summary>No asset of its own: PreDraw draws the flail item's ball sprite. A vanilla flail texture only satisfies the loader.</summary>
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Mace;

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.FlailHitboxPx;
			Projectile.height = LevyConstants.FlailHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.FlailLifeMaxTicks + 2;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.FlailLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.ownerHitCheck = false;
			Projectile.DamageType = DamageClass.Melee;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int ItemType => (int)Projectile.ai[1];
		private int Life { get { int l = (int)Projectile.ai[2]; return l >= 1 ? Math.Min(l, LevyConstants.FlailLifeMaxTicks) : LevyConstants.FlailLifeMinTicks; } }

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

		private float ShootSpeedOfItem()
		{
			int t = ItemType;
			return t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out var it) && it != null ? it.shootSpeed : 0f;
		}

		private int ShotType()
		{
			int t = ItemType;
			return t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out var it) && it != null ? it.shoot : 0;
		}

		/// <summary>0 at the hand, 1 at the end of the chain: out over the first fraction, held while swinging, then hauled back.</summary>
		private float Progress()
		{
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, Life), 0f, 1f);
			float ext = LevyConstants.FlailExtendFraction, swing = LevyConstants.FlailSwingFraction;
			float p = t < ext ? t / ext : t < ext + swing ? 1f : (1f - t) / Math.Max(0.01f, 1f - ext - swing);
			return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(p, 0f, 1f));
		}

		private Vector2 Hand(SoldierNPC s) => s.NPC.Center + new Vector2(0f, -6f);

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.IsAlive)
			{
				if (Main.netMode == NetmodeID.MultiplayerClient) return;
				Projectile.Kill(); return;
			}
			int life = Life;
			if (Projectile.localAI[0] == 0f && Projectile.timeLeft < life + 2) Projectile.timeLeft = life + 2;
			Projectile.localAI[0] += 1f;
			if (Projectile.localAI[0] > life) { Projectile.Kill(); return; }

			Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction));
			Projectile.velocity = dir;
			Projectile.direction = Projectile.spriteDirection = dir.X < 0f ? -1 : 1;
			float reach = WeaponClassifier.FlailReach(ShootSpeedOfItem());
			float prog = Progress();
			// at the end of the chain the ball swings round in a small circle
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, life), 0f, 1f);
			float circle = LevyConstants.FlailSwingRadiusPx * prog * (float)Math.Sin(MathHelper.Clamp((t - LevyConstants.FlailExtendFraction * 0.5f) / 0.6f, 0f, 1f) * MathHelper.Pi);
			Vector2 perp = new Vector2(-dir.Y, dir.X);
			float ang = Projectile.localAI[0] * LevyConstants.FlailSpinPerTick * 1.5f;
			Projectile.Center = Hand(s) + dir * reach * prog + perp * (float)Math.Sin(ang) * circle + dir * (float)Math.Cos(ang) * circle * 0.4f;
			Projectile.rotation += LevyConstants.FlailSpinPerTick * Projectile.spriteDirection;
		}

		public override bool ShouldUpdatePosition() => false; // anchored to the soldier in AI; velocity is only the aim direction

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark come from LootCreditGlobalProjectile.
			SoldierNPC s = Soldier;
			if (s != null) s.LastMeleeHitTick = (int)Main.GameUpdateCount;
			MeleeEffects.ApplyFlailStatus(ShotType(), target);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				SoldierNPC s = Soldier;
				if (s != null) DrawChain(Hand(s), Projectile.Center);
				int t = ShotType();
				if (t <= 0 || t >= TextureAssets.Projectile.Length || t == Type) return true;
				Main.instance.LoadProjectile(t);
				var asset = TextureAssets.Projectile[t];
				if (asset == null || !asset.IsLoaded) return true;
				Texture2D tex = asset.Value;
				int frames = Math.Max(1, Main.projFrames[t]);
				Rectangle src = tex.Frame(1, frames, 0, 0);
				Color c = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
				Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, c, Projectile.rotation, src.Size() * 0.5f, 1f, SpriteEffects.None);
				return false;
			}
			catch (Exception) { return true; }
		}

		/// <summary>A dashed grey chain from the hand to the ball.</summary>
		private static void DrawChain(Vector2 a, Vector2 b)
		{
			Vector2 d = b - a;
			float len = d.Length();
			if (len < 3f) return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			float rot = d.ToRotation();
			Vector2 step = d / len * 6f;
			int links = (int)(len / 6f);
			Vector2 at = a;
			for (int i = 0; i < links; i++)
			{
				Color lit = Lighting.GetColor((int)(at.X / 16f), (int)(at.Y / 16f));
				Color c = new Color(150, 150, 160).MultiplyRGBA(lit);
				Main.EntitySpriteDraw(px, at - Main.screenPosition, new Rectangle(0, 0, 1, 1), c, rot, new Vector2(0f, 0.5f), new Vector2(4f, 2.2f), SpriteEffects.None);
				at += step;
			}
		}
	}
}
