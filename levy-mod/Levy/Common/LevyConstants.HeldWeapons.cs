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
		/// <summary>Held weapons' real cadence lives in the held AI, not useTime (useTime says nothing about a channeled weapon's output), so a held weapon with no entry in <see cref="HeldRealCadence"/> has its rank scaled down.</summary>
		public const float HeldScoreFactor = 0.4f;
		/// <summary>Held weapons whose real shot interval (ticks) is known from their held AI: ranking uses it instead of useTime and skips <see cref="HeldScoreFactor"/>, which only exists to damp unknown cadences. Vortex Beater: AI_075 fires a bullet every 5 ticks (useTime 20 would rate it at a quarter of its output).</summary>
		/// <summary>
		/// Channeled weapons added to it (the held AI is in Projectile.AI_075; the cadence below is ticks per full-damage hit on one target, after the soldier's own respawn gap):
		///  * Solar Eruption (lash 611): lives 30 ticks, then dies; soldiers respawn it after <see cref="HeldRespawnMinTicks"/>/its death, so about one lash per 31 ticks. A lash hits a target every 6 ticks
		///    (local immunity) and each hit spawns a 612 flare of equal damage; about 3 of those land per lash as it sweeps = one 105-damage hit per 10 ticks (useTime 20 would rate it at a third of that).
		///  * Phantasm (630): a volley of 4 arrows every 24 ticks, every 18 once spun up (about 20 averaged over a 6 s channel). The Multishot table already counts the 4 arrows, so this is ticks per VOLLEY:
		///    4 arrows per 20 ticks = 12 arrows a second. Soldiers get no phantom arrows (those spawn only for a player-owned arrow).
		///  * Last Prism (633): the six beams share the target's 5-tick owner immunity, so one target takes at most one hit per 5 ticks (12 a second) however many beams cross it; the cadence is that hit rate.
		///    The listed damage is the 1x figure: <see cref="ScoreShotDamageMult"/> credits the 3x the beams hit for after 180 ticks of channeling. Its mana (12 a cast, up to 144 a second) is not credited, like every other mana weapon's.
		///  * Laser Machinegun (439): two lasers per volley; a volley every 24 ticks, every 18 / 12 / 6 after 40 / 80 / 120 ticks of channeling = 5 rising to 20 lasers a second, about 15.7 averaged over 6 s.
		///    Counted at 12 a second (one 60-damage hit per 5 ticks): its 5 px bolts scatter up to 5.6 degrees either side and miss small targets at range.
		///  * Charged Blaster Cannon (460): an 8 s cycle of a 3 s charge (6 orbs at 1x and 3 at 2.5x damage) then a 5 s beam at 1.5x damage that hits every 10 ticks = about 730 damage a second = one 100-damage hit per ~8 ticks;
		///    9 allows for a charge that a dead target cuts short.
		/// </summary>
		public static readonly Dictionary<int, int> HeldRealCadence = new()
		{
			{ ItemID.VortexBeater, 5 },
			{ ItemID.SolarEruption, 10 },
			{ ItemID.Phantasm, 20 },
			{ ItemID.LastPrism, 5 },
			{ ItemID.LaserMachinegun, 5 },
			{ ItemID.ChargedBlasterCannon, 9 },
		};
	}
}
