using Terraria.ID;

namespace Mechanization.Common
{
	/// <summary>
	/// Every tunable number for Mechanization, spec v0.1 placeholders. Distances are in pixels unless the name says tiles.
	/// Agents may ADD members here; prefer not to rename or remove. Nothing here reaches into Levy — the cross-mod
	/// dependency is confined to a handful of explicit call sites (see <see cref="Mechanization.Load"/> and the turret's
	/// target scan) so the two mods stay loosely coupled.
	/// </summary>
	public static class MechConstants
	{
		public const int TilePx = 16;

		public static class Turret
		{
			// ---- body ----
			/// <summary>Hitbox, in pixels. A squat emplacement.</summary>
			public const int BodyWidth = 34, BodyHeight = 30;
			/// <summary>Toughness. Destructible, but it should cost the enemy something to knock out.</summary>
			public const int HpBase = 400;
			public const int DefenseBase = 10;
			/// <summary>Immovable: knockback never shoves it off its spot.</summary>
			public const float KnockbackResist = 1f;

			// ---- the shot ----
			/// <summary>Reach, in tiles, and the same reach in pixels (nested classes see the enclosing <see cref="TilePx"/>).</summary>
			public const float RangeTiles = 32f;
			public const float RangePx = RangeTiles * TilePx;
			/// <summary>Ticks between shots (60 = 1s). 30 => two rounds a second.</summary>
			public const int FireIntervalTicks = 30;
			/// <summary>The fixed vanilla round the turret fires (v0: no Levy Armory integration yet).</summary>
			public const int ProjectileType = ProjectileID.Bullet;
			public const int ShotDamage = 24;
			public const float ShotKnockback = 2f;
			public const float ShotSpeed = 11f;
			/// <summary>How often the turret re-picks a target, in ticks (a throttled scan, not every tick).</summary>
			public const int ScanIntervalTicks = 12;

			// ---- gravity: it falls until it rests on terrain, then sits still ----
			public const float GravityPerTick = 0.35f;
			public const float MaxFallSpeed = 12f;

			// ---- taking hits ----
			/// <summary>Broad-phase padding (px) for the self-applied contact-damage sweep.</summary>
			public const float ContactBroadPhasePx = 8f;
			/// <summary>Invulnerability window after a contact hit, in ticks (one contact strike per window).</summary>
			public const int ImmuneTicksOnHit = 20;

			// ---- aim / draw ----
			/// <summary>Barrel turn easing toward the wanted angle per tick (0..1).</summary>
			public const float AimTurnLerp = 0.18f;
			/// <summary>Muzzle stand-off from the body center along the aim (px): where the round is born.</summary>
			public const float MuzzleOffsetPx = 20f;
			/// <summary>The vanilla item sprite borrowed as the turret's body/barrel (no new art). Drawn rotated to the aim.</summary>
			public const int BodyDrawItemType = ItemID.Cannon;
			/// <summary>Longest edge the borrowed sprite is scaled to, in px.</summary>
			public const float BodyDrawMaxPx = 40f;

			// ---- healthbar (2-rectangle bar, borrowed from Levy's DroneNPC pattern) ----
			public const int HpBarWidthPx = 40, HpBarHeightPx = 5, HpBarOffsetY = 12;
		}
	}
}
