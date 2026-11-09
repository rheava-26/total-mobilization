using System.Collections.Generic;

namespace Levy.Common
{
	/// <summary>Calamity's Biome Blade family (Broken, Biome Blade, Omega, Four Seasons Galaxia): how a soldier attunes and holds each attunement. Additive partial; see BiomeBlades.cs.</summary>
	public static partial class LevyConstants
	{
		/// <summary>How a soldier drives one attunement's projectile: Self = a short swing that ends on its own (the button is never held), Loop = held for as long as the target is in reach, Charge = held for a charge, then let go (the throw / lunge).</summary>
		public enum BiomeMode : byte { Self = 0, Loop = 1, Charge = 2 }

		/// <summary>One attunement's drive. Reach is in tiles (body centre to the target's hitbox) a use may start at; Blocks = the item's own CanUseItem refuses a second use while its projectile lives.</summary>
		public sealed class BiomePlan
		{
			public readonly BiomeMode Mode; public readonly float ReachTiles; public readonly bool Blocks;
			/// <summary>Charge only: which projectile slot carries the charge (0 = ai[0], 1 = ai[1]) and the charge it is full at; the fraction needed for the let-go to do anything, and the fraction a soldier waits for against a big target.</summary>
			public readonly int ChargeSlot; public readonly float ChargeMax, MinFrac, IdealFrac;
			public BiomePlan(BiomeMode mode, float reach, bool blocks, int slot = 0, float max = 1f, float min = 0f, float ideal = 1f) { Mode = mode; ReachTiles = reach; Blocks = blocks; ChargeSlot = slot; ChargeMax = max; MinFrac = min; IdealFrac = ideal; }
		}

		/// <summary>The drive of every attunement, by the attunement's own name (the AttunementID member). Reach numbers are a share of the blade's peak reach read from each projectile's Colliding line.</summary>
		public static readonly Dictionary<string, BiomePlan> BiomePlans = new()
		{
			// Broken Biome Blade
			["Default"] = new BiomePlan(BiomeMode.Loop, 6f, true),
			["Hot"] = new BiomePlan(BiomeMode.Loop, 8f, true),
			["Cold"] = new BiomePlan(BiomeMode.Self, 7f, true),
			["Evil"] = new BiomePlan(BiomeMode.Self, 7f, false),
			// Biome Blade
			["TrueDefault"] = new BiomePlan(BiomeMode.Loop, 7f, true),
			["TrueHot"] = new BiomePlan(BiomeMode.Loop, 9f, true),
			["TrueCold"] = new BiomePlan(BiomeMode.Self, 8f, true),
			["TrueTropical"] = new BiomePlan(BiomeMode.Self, 11f, true),
			["TrueEvil"] = new BiomePlan(BiomeMode.Self, 8f, false),
			["Holy"] = new BiomePlan(BiomeMode.Charge, 14f, true, slot: 1, max: 600f, min: 0.52f),
			// Omega Biome Blade
			["Whirlwind"] = new BiomePlan(BiomeMode.Charge, 14f, true, slot: 1, max: 600f, min: 0.52f),
			["FlailBlade"] = new BiomePlan(BiomeMode.Loop, 10f, true),
			["SuperPogo"] = new BiomePlan(BiomeMode.Loop, 9f, true),
			["Shockwave"] = new BiomePlan(BiomeMode.Charge, 10f, true, slot: 0, max: 420f, min: 0.3f),
			// Four Seasons Galaxia
			["Phoenix"] = new BiomePlan(BiomeMode.Charge, 14f, true, slot: 1, max: 600f, min: 0.52f),
			["Aries"] = new BiomePlan(BiomeMode.Loop, 24f, true),
			["Polaris"] = new BiomePlan(BiomeMode.Loop, 10f, true),
			["Andromeda"] = new BiomePlan(BiomeMode.Charge, 10f, true, slot: 0, max: 360f, min: 0.3f),
		};

		/// <summary>Fallback drive for an attunement from another build that is not in <see cref="BiomePlans"/>: a swing that ends on its own.</summary>
		public static readonly BiomePlan BiomePlanDefault = new BiomePlan(BiomeMode.Self, 7f, true);

		/// <summary>Item-level engagement reach in tiles (what the brain walks up to), by the item's attunement family. A soldier closes to this and then uses whatever the attunement can do from there.</summary>
		public const int BiomeReachBroken = 6, BiomeReachBiome = 7, BiomeReachOmega = 8, BiomeReachGalaxia = 8;

		/// <summary>A charge is held to its full value against a boss or a target with at least this much life; against anything smaller it is let go as soon as it does anything.</summary>
		public const int BiomeFullChargeLife = 1500;
		/// <summary>A held (Loop) attunement is let go when the target is farther than its reach by this factor; a charge is held only while the target stays within this factor of its reach.</summary>
		public const float BiomeHoldReachMult = 1.5f;
		/// <summary>A soldier is busy for this many ticks after he attunes (the player's channel is 120 ticks; a soldier needs no stillness, only a moment).</summary>
		public const int BiomeAttuneTicks = 30;
		/// <summary>The fastest a blade's lunge or dash moves a soldier, in pixels per tick per axis (a player's Ark lunge is 30).</summary>
		public const float BiomeDashMaxSpeed = 20f;

		/// <summary>
		/// Sustained single-target damage of an attunement relative to its nominal damage x use rate, for ranking (BiomeBlades.SustainedDps): the share of time its blade is on the target, the hits it lands per swing and the
		/// shots it adds. Calibrated against the lab (docs/boss_lab/biomeblades): measured damage per second per levy over the nominal rate, read against the same ratio for the plain swords of the same tiers (about 0.43).
		/// </summary>
		public static readonly Dictionary<string, float> BiomeSustainedMult = new()
		{
			["Default"] = 0.84f,
			["Hot"] = 0.72f,
			["Cold"] = 1.09f,
			["Evil"] = 0.92f,
			["TrueDefault"] = 0.97f,
			["TrueHot"] = 1.34f,
			["TrueCold"] = 1.42f,
			["TrueTropical"] = 0.87f,
			["TrueEvil"] = 1.03f,
			["Holy"] = 0.76f,
			["Whirlwind"] = 0.53f,
			["FlailBlade"] = 0.20f,
			["SuperPogo"] = 0.87f,
			["Shockwave"] = 0.22f,
			["Phoenix"] = 0.90f,
			["Aries"] = 1.05f,
			["Polaris"] = 1.12f,
			["Andromeda"] = 0.45f,
		};
	}
}
