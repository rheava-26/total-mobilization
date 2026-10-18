using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// The flying wave some broadswords launch on each swing, fired FROM THE SOLDIER at its target on top of the swing hitbox (which stays the melee damage).
	/// Only beams that fly on their own velocity are listed. Night's Edge, Excalibur, True Excalibur and The Horseman's Blade launch nothing that flies
	/// (every projectile they spawn is a slash redrawn around Main.player[owner] each tick), and True Night's Edge's lunging slash (973) re-centres on the
	/// owner every tick too, so none of those can be flown from a soldier. The Horseman's pumpkins are an on-hit effect (MeleeEffects.ApplyVanillaMeleeHit).
	/// </summary>
	public static class SwordBeams
	{
		/// <param name="Count">Projectiles per swing, fanned across <paramref name="SpreadDeg"/>.</param>
		/// <param name="Ai1">The beam's ai[1] (Terra Blade: swing length it fades out over).</param>
		private readonly record struct BeamSpec(int ProjType, float Speed, int Count, float SpreadDeg, float DamageMult, float Ai1);

		private static readonly Dictionary<int, BeamSpec> Table = new()
		{
			// Player.ItemCheck_Shoot item 757: NewProjectile(MountedCenter, aim * 5, 985, totalDamage, knockBack, i, direction, 18, scale). AI_191 does not re-anchor type 985.
			[ItemID.TerraBlade] = new BeamSpec(ProjectileID.TerraBlade2Shot, LevyConstants.TerraBladeBeamSpeed, 1, 0f, 1f, LevyConstants.TerraBladeBeamLifeAi),
		};

		public static bool HasBeam(int itemType) => Table.ContainsKey(itemType);

		/// <summary>The projectile type the item's table beam launches, or 0 when the item has none.</summary>
		public static int BeamProjectile(int itemType) => Table.TryGetValue(itemType, out BeamSpec spec) ? spec.ProjType : 0;

		/// <summary>Launches the item's beam from the soldier toward <paramref name="target"/>. Returns false when the item has none or nothing spawned.</summary>
		public static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			if (s == null || item == null || item.IsAir || target == null || !target.active || !Table.TryGetValue(item.type, out BeamSpec spec)) return false;
			Vector2 origin = s.NPC.Center;
			Vector2 vel = WeaponUse.AimShot(s, origin, target, spec.ProjType, spec.Speed, -1);
			if (vel == Vector2.Zero || vel.HasNaNs()) return false;
			int dir = vel.X < 0f ? -1 : 1;
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier * spec.DamageMult));
			float scale = item.scale * (s.Dummy != null && s.Dummy.meleeScaleGlove ? LevyConstants.MeleeGloveScale : 1f);
			DamageClass cls = item.DamageType ?? DamageClass.Melee;
			int spawned = 0;
			for (int k = 0; k < spec.Count; k++)
			{
				float deg = spec.Count > 1 ? -spec.SpreadDeg * 0.5f + spec.SpreadDeg * k / (spec.Count - 1) : 0f;
				Vector2 v = deg == 0f ? vel : vel.RotatedBy(MathHelper.ToRadians(deg));
				if (WeaponUse.Spawn(s, origin, v, spec.ProjType, damage, item.knockBack, cls, dir, spec.Ai1, scale) != null) spawned++;
			}
			return spawned > 0;
		}
	}
}
