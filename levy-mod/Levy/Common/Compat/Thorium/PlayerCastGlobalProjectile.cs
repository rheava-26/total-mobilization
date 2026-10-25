using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Shell that lets a PLAYER's Thorium heal projectile reach the player's own soldiers and officer avatars. Holds only plain state and touches no ThoriumMod type: it attaches only to
	/// projectiles of ThoriumMod while the compat is active, and hands each tick to the JIT-gated <see cref="PlayerHealCompat"/>. Only the owning player's own client acts on a projectile.
	/// </summary>
	public class PlayerCastGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
			lateInstantiation && ThoriumCompatSystem.Active && entity.ModProjectile != null && entity.ModProjectile.Mod != null && entity.ModProjectile.Mod.Name == "ThoriumMod";

		/// <summary>Base heal of the ally-heal weapon this projectile came from: 0 = not resolved yet, -1 = not an ally heal (nothing for soldiers), above 0 = the weapon's heal amount.</summary>
		public int BaseHeal;

		private bool _warned;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (Main.dedServ || projectile.owner != Main.myPlayer) { BaseHeal = -1; return; }
			try { BaseHeal = PlayerHealCompat.AmountFromSource(projectile, source); }
			catch (Exception) { BaseHeal = -1; }
		}

		public override void PostAI(Projectile projectile)
		{
			if (BaseHeal <= 0 || Main.dedServ || !ThoriumCompatSystem.Active || !projectile.active || projectile.owner != Main.myPlayer) return;
			try { PlayerHealCompat.Tick(projectile, this); }
			catch (Exception e)
			{
				// A Thorium surprise must never take down the projectile's tick: report it once and stop trying for this projectile.
				if (!_warned) { _warned = true; BaseHeal = -1; Mod.Logger.Warn("Levy/Thorium compat: a player heal projectile threw: " + e.Message); }
			}
		}
	}
}
