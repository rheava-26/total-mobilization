using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A soldier's or drone's blast kills enemies but must not eat the player's base. Vanilla's explosive Kill() calls Projectile.ExplodeTiles, which asks
	/// <see cref="GlobalTile.CanExplode"/> for every tile; this guard answers "no" for the length of a Kill() of any shot a soldier or a combat drone fires
	/// (thrown bombs, gun ordnance such as Rocket III/IV, Mini Nukes, Cluster Rockets and their fragments — and the Celebration Mk2's rockets, which explode
	/// tiles through Projectile.Kill's own type list without being flagged explosive at all). The flag only matters when something actually explodes tiles, so
	/// guarding a non-explosive shot is a harmless no-op. Other explosions are never touched.
	/// </summary>
	public class ThrownBlastGuardProjectile : GlobalProjectile
	{
		/// <summary>True only between PreKill and OnKill of a guarded projectile (Projectile.Kill runs both in one call, so nothing else can explode meanwhile).</summary>
		internal static bool Guarding;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => true;

		public override bool PreKill(Projectile projectile, int timeLeft)
		{
			// Always reset first: if an earlier guarded Kill was vetoed by another mod's PreKill, OnKill never ran and the flag would still be up
			// for this (possibly legitimate, non-soldier) explosion.
			Guarding = false;
			if (!LevyConstants.ThrownBlastsHarmTiles && projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)
				&& (tag.DroneShot || tag.IsSoldierShot))
				Guarding = true; // every soldier and drone shot is guarded whatever it is: some rockets (the Celebration Mk2's, 716/718) explode tiles through Projectile.Kill's own type list, which the old Sets.Explosive/aiStyle test missed
			return true;
		}

		public override void OnKill(Projectile projectile, int timeLeft) { Guarding = false; }
	}

	public class ThrownBlastGuardTile : GlobalTile
	{
		public override bool CanExplode(int i, int j, int type) => !ThrownBlastGuardProjectile.Guarding;
	}

	public class ThrownBlastGuardWall : GlobalWall
	{
		public override bool CanExplode(int i, int j, int type) => !ThrownBlastGuardProjectile.Guarding;
	}

	/// <summary>Safety net: a Kill() that threw or was vetoed before OnKill must not leave the guard on past this tick.</summary>
	public class ThrownBlastGuardSystem : ModSystem
	{
		public override void PostUpdateEverything() { ThrownBlastGuardProjectile.Guarding = false; }
	}
}
