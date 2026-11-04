using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>
	/// The weapon-mount pipeline (<see cref="Weapons.DroneArsenal"/>): which weapons a machine can mount and how it fires them. Levy keeps the pipeline because soldiers (an officer's real weapon)
	/// and the Mechanization companion mod (its combat drones and turrets) both mount weapons through it; the drones and turrets themselves live in Mechanization. Additive partial.
	/// </summary>
	public static partial class LevyConstants
	{
		public static class Arsenal
		{
			// ---------- Toughness: the weapon sets a machine's life, like its damage ----------
			/// <summary>Max life = base + per-tier x the weapon's rarity tier, up to the cap. Vanilla's own rarity ladder, so a machine keeps pace with the weapon it carries.</summary>
			public const int HpBase = 90, HpPerRarityTier = 30, HpMax = 420;
			/// <summary>Defense = base + per-tier x the weapon's rarity tier.</summary>
			public const int DefenseBase = 2, DefensePerRarityTier = 2;

			// ---------- Shot ----------
			/// <summary>Engagement reach is the weapon's own reach in tiles, held within these bounds.</summary>
			public const int RangeMinTiles = 16, RangeMaxTiles = 64;
			/// <summary>Shot cadence is the weapon's own, held within these bounds (ticks between shots).</summary>
			public const int MinShotIntervalTicks = 4, MaxShotIntervalTicks = 120;

			// ---------- Held weapons (Last Prism, Laser Machinegun, Charged Blaster...) ----------
			// A held weapon's real shot is an owner-anchored AI that would grab the commander, so a machine never spawns it. A held weapon that fires ammo (Phantasm, Vortex Beater,
			// Phantom Phoenix) fires that ammo's own shot; a held caster fires this bolt instead, at the weapon's real cadence and damage.
			public const int HeldStandInProjectile = ProjectileID.LaserMachinegunLaser;
			public const float HeldStandInSpeed = 9f;

			// ---------- A Guide to Engineering ----------
			/// <summary>Recipe at an Anvil: iron or lead bars and lenses. Early on purpose: a drone's strength is the weapon it carries, so the whole game is worth having them.</summary>
			public const int EngineeringRecipeIron = 10, EngineeringRecipeLens = 2;

			/// <summary>
			/// Weapons a machine never mounts, because firing their plain shot would misbehave (read from vanilla's Player.ItemCheck_Shoot and the soldier tables): the guided missiles and the
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
