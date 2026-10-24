using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A soldier's explosive blasts enemies but must not eat the player's base. Vanilla's explosive Kill() calls Projectile.ExplodeTiles, which asks
	/// <see cref="GlobalTile.CanExplode"/> for every tile; this guard answers "no" for the length of a Kill() of any soldier-fired explosive (thrown bombs,
	/// and gun ordnance such as Rocket III/IV, Mini Nukes, Cluster Rockets and their fragments). Every shot a combat drone fires is guarded the same way,
	/// explosive or not (the flag only matters when something explodes tiles). Other explosions are never touched.
	/// </summary>
	public class ThrownBlastGuardProjectile : GlobalProjectile
	{
		/// <summary>True only between PreKill and OnKill of a guarded projectile (Projectile.Kill runs both in one call, so nothing else can explode meanwhile).</summary>
		internal static bool Guarding;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => true;

		/// <summary>The projectile explodes the way vanilla's bombs and rockets do (Projectile.Kill gates its explosion handling on these same two tests).</summary>
		private static bool IsExplosive(Projectile p) => ProjectileID.Sets.Explosive[p.type] || p.aiStyle == ProjAIStyleID.Explosive;

		public override bool PreKill(Projectile projectile, int timeLeft)
		{
			// Always reset first: if an earlier guarded Kill was vetoed by another mod's PreKill, OnKill never ran and the flag would still be up
			// for this (possibly legitimate, non-soldier) explosion.
			Guarding = false;
			if (!LevyConstants.ThrownBlastsHarmTiles && projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)
				&& (tag.DroneShot || (IsExplosive(projectile) && tag.IsSoldierShot)))
				Guarding = true; // a drone's shot is guarded whatever it is: some rockets (the Celebration Mk2's) explode tiles through Projectile.Kill's own type list, which IsExplosive does not see
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
