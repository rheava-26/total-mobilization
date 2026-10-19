using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
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

			// The crack: once, as the tip reaches full extension.
			if (!Main.dedServ && (int)Projectile.localAI[0] == Math.Max(2, (int)Math.Round(life * LevyConstants.WhipExtendFraction)))
			{
				SoundEngine.PlaySound(SoundID.Item153, Projectile.Center);
				for (int i = 0; i < 4; i++)
				{
					Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin, dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 3.5f), 0, default, 0.9f);
					d.noGravity = true;
				}
			}
		}

		public override bool ShouldUpdatePosition() => false;

		/// <summary>True while the lash is out far enough to hurt (not during the first wind-up ticks).</summary>
		private bool Live() => Projectile.localAI[0] >= 3f && Progress() >= 0.3f;

		/// <summary>
		/// The lash as a polyline from the hand to the tip. Wind-up: the line arcs overhead and unrolls (tip trailing); at full extension it snaps straight
		/// (the crack); on the retract it sags a little below the aim line as it recoils.
		/// </summary>
		private int FillPoints(SoldierNPC s, Vector2[] pts)
		{
			Vector2 hand = Hand(s);
			Vector2 tip = Projectile.Center;
			Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 perp = new Vector2(-dir.Y, dir.X);
			float len = Vector2.Distance(hand, tip);
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, Life), 0f, 1f);
			float ext = LevyConstants.WhipExtendFraction;
			float bend;
			if (t < ext) { float e = t / ext; bend = 0.34f * (float)Math.Pow(1f - e, 1.6f); }
			else { float r = (t - ext) / Math.Max(0.01f, 1f - ext); bend = -0.14f * (float)Math.Sin(r * MathHelper.Pi); }
			float up = -Projectile.spriteDirection; // perp * up points to screen-up for either facing
			int n = pts.Length;
			for (int i = 0; i < n; i++)
			{
				float f = i / (float)(n - 1);
				float arc = (float)Math.Sin(f * MathHelper.Pi) * (0.55f + 0.45f * f); // fixed at the hand, free at the tip
				float tipFlick = f > 0.8f ? (f - 0.8f) * 5f * bend * len * 0.25f : 0f; // a small curl at the cracker
				pts[i] = Vector2.Lerp(hand, tip, f) + perp * (up * (arc * bend * len + tipFlick));
			}
			return n;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			SoldierNPC s = Soldier;
			if (s == null || !Live()) return false;
			var pts = new Vector2[LevyConstants.WhipSegments];
			int n = FillPoints(s, pts);
			int pad = LevyConstants.WhipHitPadPx;
			Vector2 pos = targetHitbox.Location.ToVector2() - new Vector2(pad), size = targetHitbox.Size() + new Vector2(pad * 2);
			for (int i = 1; i < n; i++)
				if (Collision.CheckAABBvLineCollision(pos, size, pts[i - 1], pts[i])) return true;
			return false;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		/// <summary>True when the outer fraction of the lash (the cracker end) is what meets the target.</summary>
		private bool TipHit(NPC target)
		{
			SoldierNPC s = Soldier;
			if (s == null) return false;
			var pts = new Vector2[LevyConstants.WhipSegments];
			int n = FillPoints(s, pts);
			int pad = LevyConstants.WhipHitPadPx;
			Vector2 pos = target.Hitbox.Location.ToVector2() - new Vector2(pad), size = target.Hitbox.Size() + new Vector2(pad * 2);
			int first = Math.Max(1, (int)Math.Floor(n * (1f - LevyConstants.WhipTipFraction)));
			for (int i = first; i < n; i++)
				if (Collision.CheckAABBvLineCollision(pos, size, pts[i - 1], pts[i])) return true;
			return false;
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// The cracker end hits like a vanilla whip tip: a guaranteed critical.
			if (TipHit(target)) modifiers.SetCrit();
		}

		/// <summary>The NPC tag debuff vanilla gives an enemy lashed by this whip projectile (0 when it has none), so the commander's own minions benefit too.</summary>
		private static int VanillaTagBuff(int projType) => projType switch
		{
			ProjectileID.BlandWhip => BuffID.BlandWhipEnemyDebuff,
			ProjectileID.ThornWhip => BuffID.ThornWhipNPCDebuff,
			ProjectileID.BoneWhip => BuffID.BoneWhipNPCDebuff,
			ProjectileID.MaceWhip => BuffID.MaceWhipNPCDebuff,
			ProjectileID.SwordWhip => BuffID.SwordWhipNPCDebuff,
			ProjectileID.ScytheWhip => BuffID.ScytheWhipEnemyDebuff,
			ProjectileID.CoolWhip => BuffID.CoolWhipNPCDebuff,
			ProjectileID.FireWhip => BuffID.FlameWhipEnemyDebuff,
			ProjectileID.RainbowWhip => BuffID.RainbowWhipNPCDebuff,
			_ => 0
		};

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark come from LootCreditGlobalProjectile.
			// The tag is Levy's own (soldier minions deal bonus damage to it and chase it first); the item's vanilla tag buff rides along with it.
			LevyNpcData.ApplyTag(target, SoldierIndex);
			int buff = VanillaTagBuff(WhipProjType());
			if (buff > 0) { try { target.AddBuff(buff, LevyConstants.TagDurationTicks); } catch (Exception) { } }
			// (no step-back: a whip soldier holds its standoff and keeps lashing)
		}

		/// <summary>The whip projectile type the source item fires (its texture is the real 5-frame strip: handle, three segments, tip), or -1.</summary>
		private int WhipProjType()
		{
			int t = ItemType;
			if (t <= 0 || !ContentSamples.ItemsByType.TryGetValue(t, out var it) || it == null) return -1;
			int sp = it.shoot;
			return sp > 0 && sp < ProjectileLoader.ProjectileCount ? sp : -1;
		}

		private static Color VanillaLineColor(int projType) => projType switch
		{
			ProjectileID.SwordWhip => Color.OrangeRed,
			ProjectileID.MaceWhip => Color.DarkBlue,
			ProjectileID.ScytheWhip => Color.Black,
			ProjectileID.CoolWhip => Color.LightBlue,
			ProjectileID.FireWhip => Color.Firebrick,
			ProjectileID.ThornWhip => Color.ForestGreen,
			ProjectileID.RainbowWhip => Color.White,
			ProjectileID.BoneWhip => Color.Tan,
			_ => Color.White
		};

		/// <summary>Vanilla whip draw: a thin cord along the control points, then the whip's 5-frame projectile sheet laid on it (handle, repeating segments, enlarged tip). Returns false when the texture can't resolve.</summary>
		private bool DrawRealWhip(SoldierNPC s)
		{
			int wp = WhipProjType();
			if (wp < 0) return false;
			Main.instance.LoadProjectile(wp);
			var asset = TextureAssets.Projectile[wp];
			if (asset == null || !asset.IsLoaded) return false;
			Texture2D tex = asset.Value;
			if (tex == null || tex.IsDisposed || tex.Height < 5 * 6 || tex.Width < 4) return false;

			Rectangle frame = tex.Frame(1, 5);
			int frameH = frame.Height;
			frame.Height -= 2;
			int segLen = Math.Max(6, frameH - 2);
			float curLen = Vector2.Distance(Hand(s), Projectile.Center);
			int segs = Math.Clamp((int)Math.Ceiling(curLen / segLen), LevyConstants.WhipDrawMinSegs, LevyConstants.WhipDrawMaxSegs); // spacing stays near one sprite per joint however far the lash is out
			var pts = new Vector2[segs + 1];
			FillPoints(s, pts);

			// cord
			Texture2D cord = TextureAssets.FishingLine.Value;
			Rectangle cordFrame = cord.Frame();
			Vector2 cordOrigin = new Vector2(cordFrame.Width / 2, 2f);
			Color cordColor = VanillaLineColor(wp);
			Vector2 at = pts[0];
			for (int i = 0; i < segs; i++)
			{
				Vector2 d = pts[i + 1] - pts[i];
				if (d.LengthSquared() < 0.01f) continue;
				Color c = Lighting.GetColor(pts[i].ToTileCoordinates(), cordColor);
				Main.EntitySpriteDraw(cord, at - Main.screenPosition, cordFrame, c, d.ToRotation() - MathHelper.PiOver2, cordOrigin, new Vector2(1f, (d.Length() + 2f) / cordFrame.Height), SpriteEffects.None);
				at += d;
			}

			// sprite strip
			SpriteEffects fx = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 baseOrigin = frame.Size() / 2f;
			at = pts[0];
			float t = MathHelper.Clamp(Projectile.localAI[0] / Math.Max(1, Life), 0f, 1f);
			float tipScale = MathHelper.Lerp(0.5f, 1.5f, Utils.GetLerpValue(0.1f, 0.7f, t, true) * Utils.GetLerpValue(0.95f, 0.7f, t, true));
			for (int i = 0; i < segs; i++)
			{
				Vector2 origin = baseOrigin;
				float scale = 1f;
				int fr;
				if (i == 0) { fr = 0; origin.Y -= 4f; }
				else if (i == segs - 1) { fr = 4; scale = tipScale; }
				else fr = 1 + Math.Min(2, (i - 1) * 3 / Math.Max(1, segs - 2));
				frame.Y = frameH * fr;
				Vector2 d = pts[i + 1] - pts[i];
				if (d.LengthSquared() > 0.01f)
				{
					Color c = Lighting.GetColor(pts[i].ToTileCoordinates());
					Main.EntitySpriteDraw(tex, at - Main.screenPosition, frame, c, d.ToRotation() - MathHelper.PiOver2, origin, scale, fx);
				}
				at += d;
			}
			return true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			SoldierNPC s = Soldier;
			if (s == null) return false;
			try
			{
				if (DrawRealWhip(s)) return false;
			}
			catch (Exception) { }
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
