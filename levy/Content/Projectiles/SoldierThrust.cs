using Terraria.ModLoader;

namespace Levy.Content.Projectiles
{
	/// <summary>One shared thrust projectile for spears and shortswords: ai[0] = soldier whoAmI, ai[1] = item type. OWNED BY THE WEAPONS AGENT.</summary>
	public class SoldierThrust : ModProjectile
	{
		public override void SetDefaults() { Projectile.width = 18; Projectile.height = 18; Projectile.friendly = true; Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.aiStyle = -1; Projectile.timeLeft = 60; Projectile.hide = true; Projectile.ownerHitCheck = false; }
	}
}
