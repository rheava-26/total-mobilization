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

		/// <summary>
		/// The combat drone: a flying machine a Barracks launches, one per spare ranged or magic weapon in its Armory. It loiters over its base, chases what comes in reach
		/// (leashed so the line holds), can be recalled to land and hand its weapon back, and flies on a battery that costs a refuel trip, never a penalty.
		/// </summary>
		public static class Drone
		{
			// ---------- garrison (DroneSystem) ----------
			/// <summary>Most drones one Barracks keeps. A density cap for performance and the picture; the spare weapons in the Armory are the real limit. Must stay at 32 or under (slots are a bitmask).</summary>
			public const int PerBarracksCap = 10;
			/// <summary>The sweep visits ONE Barracks every this many ticks (round robin), so its cost never grows with the number of Barracks.</summary>
			public const int SweepIntervalTicks = 30;
			/// <summary>After a Barracks launches (or re-arms) a drone it waits this long before the next.</summary>
			public const int RespawnCadenceTicks = 600;
			/// <summary>When an Armory has nothing spare to lend, its Barracks does not look again for this long.</summary>
			public const int NoSurplusBackoffTicks = 900;
			/// <summary>How often the list of Barracks the sweep walks is rebuilt.</summary>
			public const int BarracksListRefreshTicks = 600;
			/// <summary>Drones never launch while fewer than this many NPC slots are free, so a garrison cannot crowd soldiers or an invasion out of the NPC array.</summary>
			public const int NpcSlotReserve = 60;
			/// <summary>A drone only borrows its round of ammo from a stack that holds more than this many, so the army keeps its last rounds.</summary>
			public const int AmmoKeepRounds = 10;

			// ---------- body ----------
			public const int BodyWidth = 36, BodyHeight = 28;
			/// <summary>Taunt weight for enemy targeting (0 = weighed like a plain soldier body).</summary>
			public const int Aggro = 0;
			public const float KnockbackResist = 0.3f;
			/// <summary>A drone that has not been hurt for the delay mends this fraction of its max life every interval (this many times faster while it sits on its pad).</summary>
			public const int RegenDelayTicks = 300, RegenIntervalTicks = 30;
			public const float RegenFraction = 0.02f;
			public const int PadRegenMult = 2;

			// ---------- loiter: a lazy patrol inside a box above the base ----------
			/// <summary>Half the width (tiles) of the box the garrison loiters in, centred over the Barracks.</summary>
			public const float LoiterHalfWidthTiles = 26f;
			/// <summary>The loiter band, in tiles above the Barracks roof. Slots 0..4 fly the lower half of the band, 5..9 the upper half, so a full garrison is stacked, not tangled.</summary>
			public const int LoiterMinAltTiles = 6, LoiterMaxAltTiles = 16;
			/// <summary>The box is split into this many sectors across; a slot wanders inside its own sector (widened by <see cref="SectorSpreadTiles"/> each way) so the garrison covers the whole base.</summary>
			public const int LoiterSectors = 5;
			public const float SectorSpreadTiles = 9f;
			/// <summary>A drone within this many tiles of its waypoint has arrived; it hangs there a random dwell (ticks) and then picks the next.</summary>
			public const float WaypointArriveTiles = 2.5f;
			public const int DwellMinTicks = 20, DwellMaxTicks = 110;
			/// <summary>Cruise speeds (px/tick): a lazy loiter, a fast pursuit, and the run home to land.</summary>
			public const float LoiterSpeedPx = 1.9f, EngageSpeedPx = 4.0f, ReturnSpeedPx = 3.6f;
			/// <summary>How quickly velocity eases toward the wanted one (0..1 per tick), and the arrival gain (wanted speed = distance x gain, capped at cruise), so a drone slows smoothly as it closes in.</summary>
			public const float SteerLerp = 0.07f, ArriveGain = 0.08f;
			/// <summary>Idle bob: amplitude (px) and angular speed (radians per tick).</summary>
			public const float BobAmplitudePx = 4f, BobSpeed = 0.05f;
			/// <summary>Body tilt with sideways speed (radians per px/tick) and its cap (radians).</summary>
			public const float TiltPerSpeed = 0.05f, MaxTilt = 0.22f;
			/// <summary>A drone looks this many px ahead along its heading and rises when that point is inside terrain.</summary>
			public const float LookAheadPx = 64f, LookAheadClimbPx = 1.4f;
			/// <summary>A drone that makes no headway (speed under <see cref="StuckSpeedPx"/>) toward a far goal for <see cref="StuckClimbTicks"/> climbs at <see cref="StuckClimbPx"/>; after <see cref="StuckGiveUpTicks"/> it drops the waypoint and picks another.</summary>
			public const float StuckSpeedPx = 0.25f, StuckClimbPx = 1.6f;
			public const int StuckClimbTicks = 60, StuckGiveUpTicks = 300;

			// ---------- engaging (leashed to the loiter box) ----------
			/// <summary>While chasing, a drone strays at most this many tiles outside the loiter box (out the sides and up), and <see cref="LeashDownTiles"/> below the roof line. It returns to loitering once the enemy is dead or out of reach.</summary>
			public const float LeashTiles = 22f, LeashDownTiles = 8f;
			/// <summary>A drone presses toward an enemy until it is within this fraction of its weapon's reach, then holds.</summary>
			public const float EngageStandoffFraction = 0.6f;
			/// <summary>A drone this far (tiles) outside the leash box for <see cref="SetBackAfterTicks"/> is trapped (a wall, a pit) and is put back over its Barracks.</summary>
			public const float SetBackFarTiles = 30f;
			public const int SetBackAfterTicks = 240;

			// ---------- the pad: where a drone lands, on the ground beside its Barracks ----------
			/// <summary>Pads fan out left and right of the Barracks: the first this many tiles from its centre, each next one this far further out (alternating sides). Spacing must clear the body width.</summary>
			public const float PadStartTiles = 3f, PadSpacingTiles = 2.5f;
			/// <summary>Searching for the ground at a pad: up through this many tiles of solid (a pad dug into a slope), then down through this many open tiles.</summary>
			public const int PadScanUpTiles = 14, PadScanDownTiles = 40;
			/// <summary>A returning drone first flies to this many tiles above its pad, then settles straight down. Within <see cref="TouchdownPx"/> of the pad it has landed.</summary>
			public const float ApproachAltTiles = 5f, TouchdownPx = 5f;
			/// <summary>Descent speed cap (px/tick) on the last stretch, how fast (px/tick) a drone climbs straight up after leaving its pad, and for how long (ticks).</summary>
			public const float LandSpeedPx = 1.7f, TakeoffRisePx = 2.4f;
			public const int TakeoffTicks = 45;

			// ---------- battery: a refuel trip, never a penalty ----------
			// Think aircraft fuel: the number is seconds of flight. A low battery sends the drone home to recharge; it is never weaker, slower or capped for being low,
			// and it is never destroyed by running dry. Generous by design: a long sortie and a quick top-up.
			/// <summary>Capacity, in seconds of flight (600 = ten minutes).</summary>
			public const float BatterySeconds = 600f;
			/// <summary>Seconds of charge lost per second airborne, and gained per second on the pad (40 = a flat battery is full again in fifteen seconds).</summary>
			public const float DrainPerSecond = 1f, ChargePerSecond = 40f;
			/// <summary>The drone heads home when its charge falls to this fraction of capacity (plus the flight time home, worked out with a pessimistic speed so it never lands dry), and takes off again at <see cref="ResumeFraction"/>.</summary>
			public const float ReturnFraction = 0.2f, ResumeFraction = 0.95f;
			/// <summary>The return trip is timed at this fraction of the return speed.</summary>
			public const float PessimisticSpeedFactor = 0.5f;
			/// <summary>A charging drone with an enemy in reach scrambles early once it is above this fraction of capacity.</summary>
			public const float ScrambleFraction = 0.4f;
			/// <summary>The battery bar shows while hovered, charging, or at or under this fraction of capacity.</summary>
			public const float ShowBarFraction = 0.35f;

			// ---------- orders (recall / launch) ----------
			/// <summary>How close (px) a player must be for the server to accept a drone order from the chat panel (a flying drone loiters well above the ground).</summary>
			public const float OrderReachPx = 30f * TilePx;
			/// <summary>The keybind acts on the Barracks nearest the player within this many tiles.</summary>
			public const float KeyReachTiles = 120f;
			/// <summary>Minimum ticks between two accepted orders from one player.</summary>
			public const int OrderMinGapTicks = 15;

			// ---------- combat ----------
			/// <summary>A drone looks for an enemy this often (ticks).</summary>
			public const int ScanIntervalTicks = 10;
			/// <summary>A drone re-checks its Barracks, its Armory and its loan this often (ticks), and the server refreshes its sync this often.</summary>
			public const int ValidateIntervalTicks = 30, NetRefreshTicks = 20;
			/// <summary>While engaging, how often (ticks) the aim and fire counter ride a network sync.</summary>
			public const int AimSyncTicks = 8;
			/// <summary>Shots leave this far (px) from the centre of the body, along the aim, and drop this far (px) below it.</summary>
			public const float MuzzleOffsetPx = 14f, MuzzleDropPx = 6f;
			/// <summary>Aim wobble in degrees, eased at long range like a soldier's.</summary>
			public const float AimJitterDeg = 2f;
			/// <summary>Scales a drone's damage on top of the world-progress scale every town unit uses. 1 = the weapon's own damage x that scale.</summary>
			public const float DamageMult = 1f;
			/// <summary>How fast the drawn weapon turns toward its aim (0..1 per tick).</summary>
			public const float AimTurnLerp = 0.25f;
			/// <summary>Weapon recoil kick (px) and return (px per tick). Purely visual.</summary>
			public const float RecoilPx = 3f, RecoilReturnPx = 0.5f;

			// ---------- draw (a hand-drawn side view from rectangles; no art asset) ----------
			/// <summary>A valid vanilla asset the NPC registers as its texture. The body itself is drawn from rectangles in PreDraw.</summary>
			public const int BodyDrawItemType = ItemID.Cog;
			public const int HpBarWidthPx = 36, HpBarHeightPx = 5, HpBarOffsetY = 12;
			public const int BatteryBarWidthPx = 30, BatteryBarHeightPx = 3, BatteryBarGapPx = 3;
			/// <summary>The mounted weapon is drawn under the body, scaled so its longer side is this many px, this far below the centre.</summary>
			public const float WeaponDrawMaxPx = 28f, WeaponDrawOffsetY = 9f;
		}
	}
}
