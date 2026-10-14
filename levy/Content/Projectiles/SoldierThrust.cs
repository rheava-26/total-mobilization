using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Boss;
using Levy.Common.Dialogue;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// One shared thrust projectile for spears and shortswords: ai[0] = soldier whoAmI, ai[1] = item type, ai[2] = life in ticks.
	/// Anchored to the body every tick; extends over the first half of its life and retracts over the second. Draws the item texture.
	/// OWNED BY THE WEAPONS AGENT.
	/// </summary>
	public class SoldierThrust : ModProjectile
	{
		public override string Texture => "Levy/Content/Projectiles/SoldierThrust";

		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.ThrustHitboxPx;
			Projectile.height = LevyConstants.ThrustHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.ThrustDefaultLifeTicks;
			Projectile.hide = false;
			Projectile.ownerHitCheck = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.ThrustLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.DamageType = DamageClass.Melee;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int ItemType => (int)Projectile.ai[1];
		private int Life => Projectile.ai[2] >= 1f ? (int)Projectile.ai[2] : LevyConstants.ThrustDefaultLifeTicks;

		/// <summary>The live soldier this thrust belongs to, or null.</summary>
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

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.NPC.active || s.NPC.life <= 0) { Projectile.Kill(); return; }

			int life = Life;
			if (Projectile.localAI[0] == 0f && Projectile.timeLeft < life) Projectile.timeLeft = life; // both sides: ai[2] is synced
			Projectile.localAI[0] += 1f;
			float elapsed = Projectile.localAI[0];
			if (elapsed > life) { Projectile.Kill(); return; }

			Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction));
			Projectile.velocity = dir;
			float half = Math.Max(1f, life * 0.5f);
			float progress = elapsed <= half ? elapsed / half : (life - elapsed) / half;
			progress = MathHelper.Clamp(progress, 0f, 1f);
			float reach = WeaponClassifier.ThrustReach(ShootSpeedOfItem());
			Projectile.Center = s.NPC.Center + dir * MathHelper.SmoothStep(0f, reach, progress);
			Projectile.direction = Projectile.spriteDirection = dir.X < 0f ? -1 : 1;
			Projectile.rotation = dir.ToRotation() + (Projectile.spriteDirection == -1 ? MathHelper.PiOver4 * 3f : MathHelper.PiOver4);
		}

		private float ShootSpeedOfItem()
		{
			int t = ItemType;
			if (t > 0 && ContentSamples.ItemsByType.TryGetValue(t, out var it) && it != null) return it.shootSpeed;
			return 0f;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			SoldierNPC s = Soldier;
			if (s == null) return;
			LootCredit.Credit(target, s);
			BossRule.OnSoldierDamagedNpc(target, s, damageDone);
			s.LastMeleeHitTick = (int)Main.GameUpdateCount;
			if (target.life <= 0 && s.Record != null)
			{
				s.Record.Kills++;
				DialogueSystem.Bark(s, DialogueTrigger.KILL);
			}
		}

		public override bool ShouldUpdatePosition() => false; // anchored to the body in AI; velocity is only the thrust direction

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			int t = ItemType;
			if (t <= 0 || t >= TextureAssets.Item.Length) return false;
			Main.instance.LoadItem(t);
			var asset = TextureAssets.Item[t];
			if (asset == null || !asset.IsLoaded) return false;
			Texture2D tex = asset.Value;
			Rectangle frame = Main.itemAnimations[t] != null ? Main.itemAnimations[t].GetFrame(tex) : tex.Frame();
			float itemScale = ContentSamples.ItemsByType.TryGetValue(t, out var sample) && sample != null ? sample.scale : 1f;
			float scale = Projectile.scale * itemScale;
			Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			// Item sprites point to the top-right with the grip at the bottom-left; flipped they point top-left with the grip bottom-right.
			bool flip = Projectile.spriteDirection == -1;
			Vector2 origin = flip ? new Vector2(frame.Width, frame.Height) : new Vector2(0f, frame.Height);
			float rotation = dir.ToRotation() + (flip ? MathHelper.PiOver4 * 3f : MathHelper.PiOver4);
			float blade = new Vector2(frame.Width, frame.Height).Length() * scale;
			Vector2 grip = Projectile.Center - dir * blade;
			Color color = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
			Main.EntitySpriteDraw(tex, grip - Main.screenPosition, frame, color, rotation, origin, scale, flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			return false;
		}
	}
}
