using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;

namespace Warbands.Content.Projectiles
{
	/// <summary>
	/// The Lunatic Devotee's chanting magic: a slow stardust orb that homes on the nearest player or levy within 40 tiles (it turns a little each tick and never gets faster than 3.4 px per tick, so it can be walked around).
	/// Hostile: it hurts the player and the player's levies. Damage is set by the caster from BandStats at the world's stage. Drawn as a ball of stardust dust.
	/// </summary>
	public class DevoteOrb : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistBossFireBall;
		public override void SetDefaults()
		{
			Projectile.width = 16; Projectile.height = 16; Projectile.hostile = true; Projectile.friendly = false;
			Projectile.penetrate = 1; Projectile.timeLeft = 330; Projectile.tileCollide = true; Projectile.ignoreWater = true; Projectile.light = 0.5f;
		}
		public override bool PreDraw(ref Color lightColor) => false;
		public override void AI()
		{
			Vector2 target = Vector2.Zero; float best = 40f * 16f; best *= best; bool found = false;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				var p = Main.player[i]; if (!p.active || p.dead) continue;
				float d = Vector2.DistanceSquared(p.Center, Projectile.Center); if (d < best) { best = d; target = p.Center; found = true; }
			}
			var bodies = EnemyTargeting.Bodies;
			for (int i = 0; i < bodies.Count; i++)
			{
				var b = bodies[i]; if (b == null || !b.IsAlive || b.NPC == null || !b.NPC.active) continue;
				float d = Vector2.DistanceSquared(b.NPC.Center, Projectile.Center); if (d < best) { best = d; target = b.NPC.Center; found = true; }
			}
			if (found)
			{
				Vector2 want = Vector2.Normalize(target - Projectile.Center) * 3.4f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.045f);
			}
			if (Projectile.velocity.Length() > 3.4f) Projectile.velocity *= 0.96f;
			Projectile.rotation += 0.2f;
			if (!Main.dedServ)
			{
				for (int k = 0; k < 2; k++)
				{
					int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Vortex, 0f, 0f, 100, default, 1.3f);
					Main.dust[d].noGravity = true; Main.dust[d].velocity = Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.8f, 0.8f);
				}
				Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.7f);
			}
		}
		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ) return;
			for (int i = 0; i < 12; i++) { int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Vortex, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f)); Main.dust[d].noGravity = true; }
		}
	}
}
