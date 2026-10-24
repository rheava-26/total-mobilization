using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>
	/// Engineering combat drones: hovering robots that a Barracks garrisons once A Guide to Engineering is studied. A drone mounts one spare ranged or magic weapon lent
	/// by its Armory and fires it at nearby enemies; the weapon is the only thing it costs. Additive partial; every number of the drone feature lives here.
	/// </summary>
	public static partial class LevyConstants
	{
		public static class Drones
		{
			// ---------- Garrison (DroneSystem) ----------
			/// <summary>Most drones one Barracks keeps aloft. A density cap for performance and for the picture, not an economy limit: the spare weapons in the Armory are the real one.</summary>
			public const int PerBarracksCap = 10;
			/// <summary>The manager visits ONE Barracks every this many ticks (round robin), so its cost never grows with the number of Barracks.</summary>
			public const int SweepIntervalTicks = 30;
			/// <summary>After a Barracks launches a drone it waits this long before launching the next (a refill, or a replacement for a lost one).</summary>
			public const int RespawnCadenceTicks = 600;
			/// <summary>When an Armory has nothing spare to lend, its Barracks does not look again for this long.</summary>
			public const int NoSurplusBackoffTicks = 900;
			/// <summary>How often the list of Barracks the sweep walks is rebuilt.</summary>
			public const int BarracksListRefreshTicks = 600;
			/// <summary>Drones never launch while fewer than this many NPC slots are free, so a garrison cannot crowd soldiers or an invasion out of the NPC array.</summary>
			public const int NpcSlotReserve = 60;
			/// <summary>A drone only borrows its round of ammo from a stack that holds more than this many, so the army keeps its last rounds.</summary>
			public const int AmmoKeepRounds = 10;

			// ---------- Body ----------
			public const int BodyWidth = 36, BodyHeight = 28;
			/// <summary>The vanilla NPC whose sprite a drone borrows (the Martian Drone: a small hovering machine). No new art asset.</summary>
			public const int DrawNpcType = NPCID.MartianDrone;
			/// <summary>Taunt weight for enemy targeting (0 = weighed like a plain soldier body).</summary>
			public const int Aggro = 0;
			public const float KnockbackResist = 0.3f;

			// ---------- Health: the weapon sets the drone's toughness, like its damage ----------
			/// <summary>Max life = base + per-tier x the weapon's rarity tier, up to the cap. Vanilla's own rarity ladder, so a drone keeps pace with the weapon it carries.</summary>
			public const int HpBase = 90, HpPerRarityTier = 30, HpMax = 420;
			/// <summary>Defense = base + per-tier x the weapon's rarity tier.</summary>
			public const int DefenseBase = 2, DefensePerRarityTier = 2;
			/// <summary>A drone that has not been hurt for the delay mends this fraction of its max life every interval.</summary>
			public const int RegenDelayTicks = 300, RegenIntervalTicks = 30;
			public const float RegenFraction = 0.02f;

			// ---------- Hover ----------
			/// <summary>The drone hovers this many tiles above the top of its Barracks, less where a ceiling is in the way (never below <see cref="HoverMinTiles"/>).</summary>
			public const int HoverHeightTiles = 7, HoverMinTiles = 2;
			/// <summary>Slots are laid out in rows of this many, this far apart (px) across and between rows, centred over the Barracks.</summary>
			public const int FormationColumns = 5;
			public const float FormationSpacingPx = 56f, FormationRowSpacingPx = 38f;
			/// <summary>Cruise speed cap (px/tick), how quickly velocity eases toward the wanted one (0..1 per tick), and the arrival gain: wanted speed is the goal distance (px) times this, capped at the cruise speed, so the drone slows smoothly as it closes in.</summary>
			public const float MaxSpeedPx = 3.4f, SteerLerp = 0.08f, ArriveGain = 0.09f;
			/// <summary>Idle bob: amplitude (px) and angular speed (radians per tick).</summary>
			public const float BobAmplitudePx = 5f, BobSpeed = 0.05f;
			/// <summary>Body tilt with sideways speed (radians per px/tick) and its cap (radians).</summary>
			public const float TiltPerSpeed = 0.05f, MaxTilt = 0.22f;
			/// <summary>A drone this far from its hover spot (tiles) for this many ticks is set back on it (a wall trap, a knock into a pit).</summary>
			public const float LeashTiles = 45f;
			public const int TeleportAfterTicks = 240;

			// ---------- Combat ----------
			/// <summary>A drone looks for an enemy this often (ticks).</summary>
			public const int ScanIntervalTicks = 10;
			/// <summary>A drone re-checks its Barracks, its Armory and its loan this often (ticks), and the server refreshes its sync this often.</summary>
			public const int ValidateIntervalTicks = 30, NetRefreshTicks = 20;
			/// <summary>Engagement reach is the weapon's own reach in tiles, held within these bounds.</summary>
			public const int RangeMinTiles = 16, RangeMaxTiles = 64;
			/// <summary>Shot cadence is the weapon's own, held within these bounds (ticks between shots).</summary>
			public const int MinShotIntervalTicks = 4, MaxShotIntervalTicks = 120;
			/// <summary>Shots leave this far (px) from the centre of the body, along the aim, and drop this far (px) below it.</summary>
			public const float MuzzleOffsetPx = 14f, MuzzleDropPx = 6f;
			/// <summary>Aim wobble in degrees, eased at long range like a soldier's.</summary>
			public const float AimJitterDeg = 2f;
			/// <summary>Scales a drone's damage on top of the world-progress scale every town unit uses. 1 = the weapon's own damage x that scale.</summary>
			public const float DamageMult = 1f;
			/// <summary>How fast the drawn weapon turns toward its aim (0..1 per tick).</summary>
			public const float AimTurnLerp = 0.25f;

			// ---------- Held weapons (Last Prism, Laser Machinegun, Charged Blaster...) ----------
			// A held weapon's real shot is an owner-anchored AI that would grab the commander, so a drone never spawns it. A held weapon that fires ammo (Phantasm, Vortex Beater,
			// Phantom Phoenix) fires that ammo's own shot; a held caster fires this bolt instead, at the weapon's real cadence and damage.
			public const int HeldStandInProjectile = ProjectileID.LaserMachinegunLaser;
			public const float HeldStandInSpeed = 9f;

			// ---------- Draw ----------
			/// <summary>Healthbar over each drone (same look as a summoned unit's).</summary>
			public const int HpBarWidthPx = 36, HpBarHeightPx = 5, HpBarOffsetY = 12;
			/// <summary>The mounted weapon is drawn under the body, scaled so its longer side is this many px, this far below the centre.</summary>
			public const float WeaponDrawMaxPx = 30f, WeaponDrawOffsetY = 9f;

			// ---------- A Guide to Engineering ----------
			/// <summary>Recipe at an Anvil: iron or lead bars and lenses. Early on purpose: a drone's strength is the weapon it carries, so the whole game is worth having them.</summary>
			public const int EngineeringRecipeIron = 10, EngineeringRecipeLens = 2;

			/// <summary>
			/// Weapons a drone never mounts, because firing their plain shot would misbehave (read from vanilla's Player.ItemCheck_Shoot and the soldier tables): the guided missiles and the
			/// Flying Knife steer to their owner's cursor (the commander's); the rain clouds, the Ice Rod and the Clinger Staff take a target position in ai[]; the Medusa Head's ray is anchored to
			/// its owner. Deploy-and-persist weapons (<see cref="LevyConstants.DeployPersistItems"/>) are refused too. They stay in the Armory for soldiers, who have drivers for all of them.
			/// </summary>
			public static readonly HashSet<int> UnmountableItems = new()
			{
				ItemID.MagicMissile, ItemID.Flamelash, ItemID.RainbowRod, ItemID.FlyingKnife,
				ItemID.NimbusRod, ItemID.CrimsonRod, ItemID.IceRod, ItemID.ClingerStaff, ItemID.MedusaHead,
			};
		}
	}
}
