using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A soldier's thrown bomb blasts enemies but must not eat the player's base. Vanilla's bomb Kill() calls Projectile.ExplodeTiles, which asks
	/// <see cref="GlobalTile.CanExplode"/> for every tile; this guard answers "no" for the length of a Kill() of a soldier-thrown explosive. It only
	/// affects projectile types a soldier has thrown from a Thrown-family item (see <see cref="Register"/>), never other soldier shots.
	/// </summary>
	public class ThrownBlastGuardProjectile : GlobalProjectile
	{
		/// <summary>True only between PreKill and OnKill of a guarded projectile (Projectile.Kill runs both in one call, so nothing else can explode meanwhile).</summary>
		internal static bool Guarding;
		private static readonly HashSet<int> _types = new();

		/// <summary>Called by the classifier for every Thrown-family item's projectile type.</summary>
		public static void Register(int projType) { if (projType > 0) _types.Add(projType); }

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => true;

		public override bool PreKill(Projectile projectile, int timeLeft)
		{
			if (!LevyConstants.ThrownBlastsHarmTiles && _types.Contains(projectile.type)
				&& projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.IsSoldierShot)
				Guarding = true;
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

	/// <summary>Safety net: a Kill() that threw before OnKill must not leave the guard on for the next explosion.</summary>
	public class ThrownBlastGuardSystem : ModSystem
	{
		public override void PostUpdateEverything() { ThrownBlastGuardProjectile.Guarding = false; }
	}
}
