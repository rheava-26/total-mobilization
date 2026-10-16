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
	/// A soldier's lash: ai[0] = soldier whoAmI, ai[1] = whip item type, ai[2] = life ticks * 1000 + reach multiplier * 100. velocity is the unit aim direction.
	/// Anchored to the soldier's hand every tick; the tip runs out over the first part of its life, holds, then retracts. Every enemy the lash
	/// crosses takes damage once and is TAGGED (see <see cref="LevyNpcData.ApplyTag"/>): soldier minions hit tagged enemies harder and chase them first.
	/// </summary>
	public class SoldierWhip : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = 60;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.WhipLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.ownerHitCheck = false;
			Projectile.DamageType = DamageClass.SummonMeleeSpeed;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int ItemType => (int)Projectile.ai[1];
		private int Life { get { int l = (int)Projectile.ai[2] / 1000; return l >= 1 ? l : 30; } }
		private float ReachMult { get { float m = ((int)Projectile.ai[2] % 1000) / 100f; return m >= 0.5f ? m : 1f; } }

		/// <summary>Packs the lash life and the reach multiplier into ai[2].</summary>
		public static float PackAi2(int lifeTicks, float reachMult) => Math.Clamp(lifeTicks, 1, 200) * 1000 + Math.Clamp((int)Math.Round(reachMult * 100f), 50, 400);

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

		private float ReachPx()
		{
			float shootSpeed = 4f;
			int t = ItemType;
			if (t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out var it) && it != null) shootSpeed = it.shootSpeed;
			return WeaponClassifier.WhipReach(shootSpeed) * ReachMult;
		}

		private float Progress()
		{
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, Life), 0f, 1f);
			float ext = LevyConstants.WhipExtendFraction, hold = LevyConstants.WhipHoldFractionOfLife;
			float p = t < ext ? t / ext : t < ext + hold ? 1f : (1f - t) / Math.Max(0.01f, 1f - ext - hold);
			return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(p, 0f, 1f));
		}

		private Vector2 Hand(SoldierNPC s) => s.NPC.Center + new Vector2(Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X) * 6f, -8f);

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
			Projectile.Center = Hand(s) + dir * ReachPx() * Progress(); // the hit-box sits at the tip
			Projectile.rotation = dir.ToRotation();
		}

		public override bool ShouldUpdatePosition() => false;

		/// <summary>The lash as a polyline from the hand to the tip: a straight line bowed by a travelling wave that settles as the whip extends.</summary>
		private int FillPoints(SoldierNPC s, Vector2[] pts)
		{
			Vector2 hand = Hand(s);
			Vector2 tip = Projectile.Center;
			Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 perp = new Vector2(-dir.Y, dir.X);
			float len = Vector2.Distance(hand, tip);
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, Life), 0f, 1f);
			float amp = len * 0.22f * (float)Math.Sin(MathHelper.Clamp(t / LevyConstants.WhipExtendFraction, 0f, 1f) * MathHelper.Pi) + len * 0.05f;
			float side = Projectile.spriteDirection;
			int n = pts.Length;
			for (int i = 0; i < n; i++)
			{
				float f = i / (float)(n - 1);
				float wave = (float)Math.Sin(f * MathHelper.Pi) * amp * (1f - f * 0.5f) * side;
				pts[i] = Vector2.Lerp(hand, tip, f) + perp * wave;
			}
			return n;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			SoldierNPC s = Soldier;
			if (s == null) return false;
			if (Projectile.localAI[0] < 3f) return false;
			var pts = new Vector2[LevyConstants.WhipSegments];
			int n = FillPoints(s, pts);
			Vector2 pos = targetHitbox.Location.ToVector2(), size = targetHitbox.Size();
			for (int i = 1; i < n; i++)
				if (Collision.CheckAABBvLineCollision(pos, size, pts[i - 1], pts[i])) return true;
			return false;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark come from LootCreditGlobalProjectile.
			LevyNpcData.ApplyTag(target, SoldierIndex);
			SoldierNPC s = Soldier;
			if (s != null) s.LastMeleeHitTick = (int)Main.GameUpdateCount;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			SoldierNPC s = Soldier;
			if (s == null) return false;
			try
			{
				var pts = new Vector2[LevyConstants.WhipSegments];
				int n = FillPoints(s, pts);
				Texture2D px = TextureAssets.MagicPixel.Value;
				Color baseColor = new Color(168, 120, 70);
				for (int i = 1; i < n; i++)
				{
					Vector2 a = pts[i - 1], b = pts[i];
					Vector2 d = b - a;
					float len = d.Length();
					if (len < 0.1f) continue;
					float f = i / (float)(n - 1);
					float thick = MathHelper.Lerp(3.2f, 1.4f, f);
					Color lit = Lighting.GetColor((int)(b.X / 16f), (int)(b.Y / 16f));
					Color c = Color.Lerp(baseColor, new Color(235, 215, 170), f * f).MultiplyRGBA(lit);
					Main.EntitySpriteDraw(px, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), c, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len + 0.5f, thick), SpriteEffects.None);
				}
				// the cracker at the tip
				Vector2 tip = pts[n - 1];
				Main.EntitySpriteDraw(px, tip - Main.screenPosition, new Rectangle(0, 0, 1, 1), Color.White * 0.9f, Projectile.rotation, new Vector2(0.5f, 0.5f), new Vector2(4f, 4f), SpriteEffects.None);
			}
			catch (Exception) { }
			return false;
		}
	}
}
