using System.Collections.Generic;
using Terraria.ID;

namespace Levy.Common
{
	/// <summary>Held-projectile weapons (Phantasm, Vortex Beater, Last Prism, Phantom Phoenix, Starlight...): the real vanilla held AI runs anchored to the soldier. Additive partial; see HeldWeapons.cs.</summary>
	public static partial class LevyConstants
	{
		/// <summary>Ticks after the last combat "feed" (a TryAttack that passed range / line of sight) during which the dummy keeps channeling. Past it the held AI sees the use button released and ends itself.</summary>
		public const int HeldFeedGraceTicks = 8;
		/// <summary>Hard cap: a holder nobody has fed for this long is killed outright (a held AI that never checks channel).</summary>
		public const int HeldIdleKillTicks = 120;
		/// <summary>Minimum ticks between two holder spawns for one soldier, so a weapon that cannot fire does not spawn and die every tick.</summary>
		public const int HeldRespawnMinTicks = 20;
		/// <summary>How often (ticks) the grade-based aim wobble is re-rolled while a holder is aiming.</summary>
		public const int HeldAimJitterRefreshTicks = 24;
		/// <summary>The soldier's raised-weapon pose is kept at least this many ticks while it feeds a holder.</summary>
		public const int HeldPoseHoldTicks = 4;
		/// <summary>Reach (tiles) of a melee-class held weapon (Starlight, Solar Eruption): the soldier closes to this and holds there.</summary>
		public const int HeldMeleeReachTiles = 7;
		/// <summary>Lead estimate for ammo shots: projectile speed = shootSpeed x this + ammo speed, never under the minimum.</summary>
		public const float HeldLeadSpeedFactor = 0.8f, HeldLeadSpeedMin = 10f;
		/// <summary>The fake cursor is placed at least this far (px) from the soldier along the aim direction.</summary>
		public const float HeldAimMinDistPx = 96f;
		/// <summary>Held weapons' real cadence lives in the held AI, not useTime (Last Prism's useTime of 5 would inflate its DPS), so their rank is scaled down.</summary>
		public const float HeldScoreFactor = 0.4f;
		/// <summary>Held weapons whose real shot interval (ticks) is known from their held AI: ranking uses it instead of useTime and skips <see cref="HeldScoreFactor"/>, which only exists to damp unknown cadences. Vortex Beater: AI_075 fires a bullet every 5 ticks (useTime 20 would rate it at a quarter of its output).</summary>
		public static readonly Dictionary<int, int> HeldRealCadence = new() { { ItemID.VortexBeater, 5 } };
	}
}
