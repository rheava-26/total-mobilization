using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;
using Levy.Common.Util;

namespace LevyThorium.Content
{
	/// <summary>
	/// A soldier's healing bolt. ai[0] = the wounded soldier's NPC index, ai[1] = hit points restored. It homes onto that soldier and restores
	/// its life on contact (server / single player), then vanishes. Deals no damage and ignores terrain, so a heal never harms an enemy
	/// and never gets lost behind a ledge.
	/// </summary>
	public class SoldierHeal : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.AmethystBolt;

		public override void SetDefaults()
		{
			Projectile.width = 12; Projectile.height = 12;
			Projectile.aiStyle = -1;
			Projectile.friendly = false; Projectile.hostile = false;
			Projectile.tileCollide = false; Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 240;
			Projectile.alpha = 255;
		}

		public override bool? CanHitNPC(NPC target) => false;
		public override bool CanHitPlayer(Player target) => false;
		public override bool? CanDamage() => false;
		public override bool PreDraw(ref Color lightColor) => false;

		public override void AI()
		{
			int idx = (int)Projectile.ai[0];
			NPC t = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
			if (t == null || !t.active || t.life <= 0 || t.ModNPC is not SoldierNPC) { Projectile.Kill(); return; }

			Vector2 to = t.Center - Projectile.Center;
			float dist = to.Length();
			if (dist < 14f || Projectile.Hitbox.Intersects(t.Hitbox)) { Arrive(t); return; }

			float speed = System.Math.Max(7f, Projectile.velocity.Length());
			Vector2 want = to / dist * speed;
			Vector2 v = Vector2.Lerp(Projectile.velocity, want, 0.25f);
			Projectile.velocity = v.LengthSquared() > 0.01f ? Vector2.Normalize(v) * speed : want;
			Projectile.rotation = Projectile.velocity.ToRotation();

			if (Main.netMode != NetmodeID.Server)
			{
				Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 100, default, 1.1f);
				d.noGravity = true;
			}
		}

		private void Arrive(NPC t)
		{
			if (LevyUtil.IsServerOrSingle)
			{
				int heal = System.Math.Max(0, (int)Projectile.ai[1]);
				int before = t.life;
				t.life = System.Math.Min(t.lifeMax, t.life + heal);
				int gained = t.life - before;
				if (gained > 0)
				{
					if (t.ModNPC is SoldierNPC { Record: not null } s) s.Record.CurrentHp = t.life;
					t.HealEffect(gained, true);
					t.netUpdate = true;
				}
			}
			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 8; i++)
				{
					Dust d = Dust.NewDustPerfect(t.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.2f);
					d.noGravity = true;
				}
			}
			Projectile.Kill();
		}
	}
}
