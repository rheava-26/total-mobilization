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
			public const int ScanIntervalTicks = 8;
			/// <summary>
			/// A turret holds its current target until it dies or leaves reach, and only swaps to a new nearest hostile that is closer than this fraction of the current
			/// one's distance (so two enemies at about the same range do not make the barrel flip between them every scan).
			/// </summary>
			public const float RetargetCloserFraction = 0.7f;
			/// <summary>
			/// Shortest reach, in tiles, a turret will claim for a weapon whose round dies young (a flamethrower's flames, a short bolt): it engages only out to where the round
			/// really flies (speed x lifetime x Levy's short-shot fraction), never past it, but never below this.
			/// </summary>
			public const float MinReachTiles = 8f;
			/// <summary>How many updates a flamethrower's flames (the Flamethrower projectile AI) fly before they burn out when the turret fires them (vanilla: 50). Their projectile lists a far longer lifetime that the AI cuts short.</summary>
			public const int FlameLifetimeUpdates = 50;

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

			// ---- placement ----
			/// <summary>How far (px) from the placing player the server accepts a turret being set down (a generous screen-and-a-half; this only rejects a spoofed packet).</summary>
			public const float PlaceReachPx = 90f * TilePx;
			/// <summary>A spot that is buried in the ground is lifted up to this many tiles to the first open space; if there is none the item is not used up.</summary>
			public const int PlaceLiftTiles = 8;
			/// <summary>A turret set down in the air is settled onto the ground below it, searching up to this many tiles, so it never spends its first seconds falling.</summary>
			public const int PlaceDropTiles = 120;
			/// <summary>Minimum ticks between two accepted placement requests from one player (a held item cannot spawn a stack of turrets from a burst of packets).</summary>
			public const int PlaceMinGapTicks = 10;

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
			public const float AimTurnLerp = 0.3f;
			/// <summary>While engaging, how often (ticks) the aim and fire counter ride a network sync, so a remote client sees the barrel track and hears the shots.</summary>
			public const int AimSyncTicks = 8;
			/// <summary>Barrel kick-back on each shot (px) and how fast it returns (px per tick). Purely visual.</summary>
			public const float RecoilPx = 4f, RecoilReturnPx = 0.7f;
			/// <summary>Muzzle stand-off from the body center along the aim (px): where the round is born.</summary>
			public const float MuzzleOffsetPx = 20f;
			/// <summary>A valid vanilla asset the NPC registers as its texture. The body itself is drawn from plain rectangles (a fixed plinth and a turning barrel) in <c>PreDraw</c>.</summary>
			public const int BodyDrawItemType = ItemID.Cannon;
			/// <summary>The fixed plinth (px): its slab width/height, the narrower pedestal width/height above it, and how far above the body centre the barrel pivots.</summary>
			public const int PlinthWidthPx = 32, PlinthHeightPx = 7, PedestalWidthPx = 14, PedestalHeightPx = 7;
			public const float PivotUpPx = 2f;
			/// <summary>The bare barrel (px) shown under the mounted weapon, or alone when the turret fires the plain bullet.</summary>
			public const int BarrelLengthPx = 22, BarrelThicknessPx = 6;
			/// <summary>Longest edge the MOUNTED weapon's item sprite is scaled to, in px (drawn over the barrel so you can see which gun it fires).</summary>
			public const float WeaponDrawMaxPx = 34f;
			/// <summary>How far along the aim the mounted weapon's sprite is pushed out from the pivot, in px.</summary>
			public const float WeaponDrawOffsetPx = 12f;

			// ---- reach ring (drawn round a hovered or chatted turret) ----
			public const int RingDots = 96;

			// ---- enemy aggro (Levy EnemyTargeting integration) ----
			/// <summary>Taunt weight (Player.aggro equivalent) enemies feel from a turret, so hostiles path to and attack it like a soldier or drone.</summary>
			public const int Aggro = 0;

			// ---- healthbar (2-rectangle bar, borrowed from Levy's DroneNPC pattern) ----
			public const int HpBarWidthPx = 40, HpBarHeightPx = 5, HpBarOffsetY = 12;
		}
	}
}
