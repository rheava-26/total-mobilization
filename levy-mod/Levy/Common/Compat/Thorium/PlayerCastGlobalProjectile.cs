using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Shell that lets a PLAYER's (or a soldier's) Thorium ally-heal projectile reach the soldiers and officer avatars. Holds only plain state and touches no ThoriumMod type: it attaches only to
	/// projectiles of ThoriumMod while the compat is active. It records the heal amount of the weapon the projectile came from, and opens the "on-hit" window the hooked Thorium routines
	/// (<see cref="AllyHealHook"/>) read when this projectile damages an enemy.
	/// </summary>
	public class PlayerCastGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) =>
			lateInstantiation && ThoriumCompatSystem.Active && entity.ModProjectile != null && entity.ModProjectile.Mod != null && entity.ModProjectile.Mod.Name == "ThoriumMod";

		/// <summary>Base heal of the ally-heal weapon this projectile came from: 0 = not resolved yet, -1 = not an ally heal (nothing for soldiers), above 0 = the weapon's heal amount.</summary>
		public int BaseHeal;

		/// <summary>Game tick of the last contact heal this projectile gave soldiers (a Thorium heal bolt tests its radius every tick; the soldiers get one heal per tick at most).</summary>
		public uint ContactTick = uint.MaxValue;

		/// <summary>Seek-and-heal projectiles (a homing orb, a bolt that steers to the nearest ally): the tick this projectile last picked among Players, the nearest eligible Player it saw (px, float.MaxValue = none) and whether the
		/// pick excluded its healer. <see cref="PreVelocity"/> is the velocity before its own AI steered it. AllyHealHook reads them in <see cref="PostAI"/> to steer to a nearer wounded soldier.</summary>
		public uint SeekTick = uint.MaxValue;
		public float SeekBest = float.MaxValue;
		public bool SeekIgnoreHealer = true;
		public Microsoft.Xna.Framework.Vector2 PreVelocity;

		public override bool PreAI(Projectile projectile) { PreVelocity = projectile.velocity; return true; }

		public override void PostAI(Projectile projectile)
		{
			if (SeekTick != Main.GameUpdateCount) return;
			try { AllyHealHook.SeekPost(projectile, this); }
			catch (Exception e) { Mod.Logger.Warn("Levy/Thorium compat: steering a seek-heal projectile threw: " + e.Message); SeekTick = uint.MaxValue; }
		}

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			try { BaseHeal = PlayerHealCompat.AmountFromSource(projectile, source); }
			catch (Exception) { BaseHeal = -1; }
		}

		/// <summary>This projectile is about to damage an enemy: any Thorium "heal allies on hit" sweep that runs in the same tick belongs to it.</summary>
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (BaseHeal <= 0 || !ThoriumCompatSystem.Active) return;
			try { AllyHealHook.OpenHitWindow(projectile, null, projectile.owner, BaseHeal); }
			catch (Exception e) { Mod.Logger.Warn("Levy/Thorium compat: opening the on-hit ally-heal window threw: " + e.Message); BaseHeal = -1; }
		}
	}

	/// <summary>The item half of the on-hit window: a Thorium weapon's own swing (an item's OnHitNPC) that heals allies.</summary>
	public class PlayerCastGlobalItem : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
			lateInstantiation && ThoriumCompatSystem.Active && entity.ModItem != null && entity.ModItem.Mod != null && entity.ModItem.Mod.Name == "ThoriumMod";

		public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (!ThoriumCompatSystem.Active) return;
			try
			{
				int a = PlayerHealCompat.AllyHealOf(item);
				if (a > 0) AllyHealHook.OpenHitWindow(null, item, player.whoAmI, a);
			}
			catch (Exception e) { Mod.Logger.Warn("Levy/Thorium compat: opening the on-hit ally-heal window threw: " + e.Message); }
		}
	}
}
