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
			/// <summary>The fallback round the turret fires when no Levy Armory with a mountable weapon is in reach, so a turret is never inert.</summary>
			public const int ProjectileType = ProjectileID.Bullet;
			public const int ShotDamage = 24;
			public const float ShotKnockback = 2f;
			public const float ShotSpeed = 11f;
			/// <summary>How often the turret re-picks a target, in ticks (a throttled scan, not every tick).</summary>
			public const int ScanIntervalTicks = 12;

			// ---- surplus weapon (Levy Armory integration) ----
			/// <summary>
			/// How far the turret reaches for a Levy Armory to draw a weapon pattern from, in tiles, and the same reach in pixels. An Armory found in this
			/// reach also brings in its whole pylon network (Levy's networked armories), and a turret standing at a pylon reaches that network directly,
			/// so a frontier turret far from town still refills from the base.
			/// </summary>
			public const float ArmoryReachTiles = 240f;
			public const float ArmoryReachPx = ArmoryReachTiles * TilePx;
			/// <summary>Ticks between re-picking the mounted weapon from the nearest Armory (a throttled scan; the weapon rarely changes).</summary>
			public const int WeaponRefreshTicks = 90;
			/// <summary>How often a turret re-reads which armories it reaches for display (chat / hover), in ticks. The server re-reads on <see cref="WeaponRefreshTicks"/>.</summary>
			public const int LinkRefreshTicks = 30;
			/// <summary>The mounted weapon's listed damage is multiplied by this before the turret fires it. 1 = the weapon's own damage.</summary>
			public const float SurplusDamageMult = 1f;

			// ---- dismantle / hud ----
			/// <summary>How close (px) the player must be for the server to accept a dismantle request (a little more than the chat window's own reach).</summary>
			public const float DismantleReachPx = 20f * TilePx;
			/// <summary>Pixels the hover / placement hint is drawn from the cursor.</summary>
			public const float HudOffsetX = 18f, HudOffsetY = 22f;
			/// <summary>Ticks between re-reading armory reach for the placement hint while the turret item is held.</summary>
			public const int HudRefreshTicks = 10;

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
			/// <summary>Longest edge the MOUNTED weapon's item sprite is scaled to, in px (drawn over the cannon so you can see which gun it fires).</summary>
			public const float WeaponDrawMaxPx = 26f;
			/// <summary>How far along the aim the mounted weapon's sprite is pushed out from the body center, in px.</summary>
			public const float WeaponDrawOffsetPx = 8f;

			// ---- enemy aggro (Levy EnemyTargeting integration) ----
			/// <summary>Taunt weight (Player.aggro equivalent) enemies feel from a turret, so hostiles path to and attack it like a soldier or drone.</summary>
			public const int Aggro = 0;

			// ---- healthbar (2-rectangle bar, borrowed from Levy's DroneNPC pattern) ----
			public const int HpBarWidthPx = 40, HpBarHeightPx = 5, HpBarOffsetY = 12;
		}
	}
}
