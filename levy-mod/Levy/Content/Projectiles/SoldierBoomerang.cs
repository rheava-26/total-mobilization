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
using Levy.Content.NPCs;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// A thrown boomerang: ai[0] = soldier whoAmI, ai[1] = boomerang item type (its own projectile sprite is drawn), ai[2] = phase (0 outbound, 1 returning).
	/// Flies out along the throw until it has travelled far enough, then homes back on the SOLDIER (not the commander) and is caught on arrival.
	/// Damages whatever it crosses in both directions, each enemy at most once per local cooldown.
	/// </summary>
	public class SoldierBoomerang : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = LevyConstants.BoomerangHitboxPx;
			Projectile.height = LevyConstants.BoomerangHitboxPx;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.aiStyle = -1;
			Projectile.timeLeft = LevyConstants.BoomerangMaxFlightTicks + 60;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = LevyConstants.BoomerangLocalHitCooldownTicks;
			Projectile.npcProj = true;
			Projectile.noDropItem = true;
			Projectile.DamageType = DamageClass.Melee;
		}

		private int SoldierIndex => (int)Projectile.ai[0];
		private int ItemType => (int)Projectile.ai[1];
		private bool Returning => Projectile.ai[2] >= 1f;

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

		/// <summary>Cruise speed: the item's shoot speed, kept in a sane band. Stored in localAI[1] on first tick.</summary>
		private float CruiseSpeed()
		{
			if (Projectile.localAI[1] > 0f) return Projectile.localAI[1];
			float sp = Projectile.velocity.Length();
			Projectile.localAI[1] = MathHelper.Clamp(sp, LevyConstants.BoomerangMinSpeed, LevyConstants.BoomerangMaxSpeed);
			return Projectile.localAI[1];
		}

		public override void AI()
		{
			SoldierNPC s = Soldier;
			if (s == null || !s.IsAlive)
			{
				if (Main.netMode == NetmodeID.MultiplayerClient) return;
				Projectile.Kill(); return;
			}
			bool authoritative = Main.netMode != NetmodeID.MultiplayerClient;
			float cruise = CruiseSpeed();
			float tick = ++Projectile.localAI[0];
			if (Projectile.localAI[0] == 1f) Projectile.localAI[2] = 0f;
			Projectile.rotation += LevyConstants.BoomerangSpinPerTick * (Projectile.velocity.X >= 0f ? 1f : -1f);

			if (!Returning)
			{
				float dist = Vector2.Distance(Projectile.Center, s.NPC.Center);
				// hold the throw direction; turn around at full range or when the flight has run long
				if (dist >= LevyConstants.BoomerangMaxOutPx || tick > LevyConstants.BoomerangMaxFlightTicks * 0.5f)
				{
					if (authoritative) { Projectile.ai[2] = 1f; Projectile.netUpdate = true; }
					else Projectile.ai[2] = 1f; // clients mirror it at once; the server's value follows on the next sync
				}
				else
				{
					Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * cruise;
				}
			}
			if (Returning)
			{
				Vector2 to = s.NPC.Center - Projectile.Center;
				float d = to.Length();
				if (d <= LevyConstants.BoomerangCatchDistPx || tick > LevyConstants.BoomerangMaxFlightTicks)
				{
					if (authoritative) Projectile.Kill();
					return;
				}
				Vector2 want = to / Math.Max(1f, d) * (cruise * 1.25f);
				Projectile.velocity += (want - Projectile.velocity) * 0.18f + to / Math.Max(1f, d) * LevyConstants.BoomerangReturnAccel * 0.25f;
				float max = cruise * 1.6f;
				if (Projectile.velocity.LengthSquared() > max * max) Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * max;
			}
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (!LevyUtil.IsHostileTarget(target) || SoldierTargeting.IsNeverTarget(target)) return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Credit, boss-rule accounting, kill count and the KILL bark come from LootCreditGlobalProjectile.
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ) return false;
			try
			{
				int item = ItemType;
				int t = 0;
				if (item > 0 && ContentSamples.ItemsByType.TryGetValue(item, out var it) && it != null) t = it.shoot;
				if (t <= 0 || t >= TextureAssets.Projectile.Length || t == Type) return true;
				Main.instance.LoadProjectile(t);
				var asset = TextureAssets.Projectile[t];
				if (asset == null || !asset.IsLoaded) return true;
				Texture2D tex = asset.Value;
				int frames = Math.Max(1, Main.projFrames[t]);
				int frame = (int)(Projectile.localAI[0] / 4f) % frames;
				Rectangle src = tex.Frame(1, frames, 0, frame);
				Color c = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
				Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, c, Projectile.rotation, src.Size() * 0.5f, 1f, SpriteEffects.None);
				return false;
			}
			catch (Exception) { return true; }
		}
	}
}
