using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Events;

namespace Levy.Content.Projectiles
{
	/// <summary>
	/// The planted levy sentry: ai[0] = tier (1 Rod, 2 Cane, 3 Staff). A stationary marker that is not a vanilla "sentry" (so the player's turret cap never culls it);
	/// it ends with the Old One's Army, and while it stands <see cref="OldOnesArmySystem"/> keeps its levies called up.
	/// </summary>
	public class LevySentryBanner : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 24; Projectile.height = 40;
			Projectile.friendly = false; Projectile.hostile = false;
			Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.aiStyle = -1;
			Projectile.timeLeft = 600; Projectile.ignoreWater = true; Projectile.netImportant = true;
			Projectile.sentry = false;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			// The server (or single player) ends it with the event, which also covers the option being switched off mid-event.
			if (Main.netMode != NetmodeID.MultiplayerClient && !OldOnesArmySystem.Active) { Projectile.Kill(); return; }
			Projectile.timeLeft = 600;
			if (Main.dedServ) return;
			int tier = (int)Projectile.ai[0];
			if (Main.rand.NextBool(18))
			{
				int d = Dust.NewDust(Projectile.position + new Vector2(0, -8), Projectile.width, 8, tier >= 3 ? DustID.GoldFlame : tier == 2 ? DustID.PurpleTorch : DustID.BlueTorch, 0f, -1f);
				Main.dust[d].noGravity = true; Main.dust[d].velocity *= 0.3f;
			}
			Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.4f);
		}
	}
}
