using System;
using Terraria;                 // Utils.NextFloat (an extension on UnifiedRandom)
using Terraria.Utilities;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Common
{
	/// <summary>
	/// The drone's battery rules, as pure functions so the NPC, the HUD and the boot self-check share one set of numbers. The unit is seconds of flight (see <see cref="D.BatterySeconds"/>).
	/// A battery is a COST, never a cap: nothing here weakens a drone, and a flat battery only means "go home".
	/// </summary>
	public static class DroneBattery
	{
		public const float Capacity = D.BatterySeconds;

		/// <summary>The charge after <paramref name="seconds"/> more: drained while airborne, topped up while on the pad, always held within 0..capacity.</summary>
		public static float Step(float battery, bool airborne, float seconds)
		{
			float next = airborne ? battery - D.DrainPerSecond * seconds : battery + D.ChargePerSecond * seconds;
			return Math.Clamp(next, 0f, Capacity);
		}

		/// <summary>
		/// The charge at or under which a drone heads home: the reserve fraction of capacity plus the charge the trip itself burns, timed at a pessimistic speed
		/// (the way Levy's aircraft estimate their run to the runway), so a drone is never caught dry on the way.
		/// </summary>
		public static float ReturnThreshold(float distanceToPadPx)
		{
			float tripSeconds = Math.Max(0f, distanceToPadPx) / (D.ReturnSpeedPx * D.PessimisticSpeedFactor * 60f);
			return Math.Min(Capacity, Capacity * D.ReturnFraction + tripSeconds * D.DrainPerSecond);
		}

		public static bool ShouldReturn(float battery, float distanceToPadPx) => battery <= ReturnThreshold(distanceToPadPx);
		public static bool Recharged(float battery) => battery >= Capacity * D.ResumeFraction;
		public static bool CanScramble(float battery) => battery >= Capacity * D.ScrambleFraction;
		public static float Fraction(float battery) => Capacity <= 0f ? 0f : Math.Clamp(battery / Capacity, 0f, 1f);
	}

	/// <summary>Where a drone loiters and where it lands, as pure geometry in tiles relative to its Barracks, so the NPC and the boot self-check agree on it.</summary>
	public static class DroneFlight
	{
		/// <summary>The pad for formation slot <paramref name="slot"/>, in tiles left (negative) or right of the Barracks centre. Slots alternate sides and fan outward.</summary>
		public static float PadOffsetTiles(int slot)
		{
			int side = (slot & 1) == 0 ? 1 : -1;
			return side * (D.PadStartTiles + (slot >> 1) * D.PadSpacingTiles);
		}

		/// <summary>The centre, in tiles from the Barracks centre, of the loiter sector slot <paramref name="slot"/> wanders in.</summary>
		public static float SectorCenterTiles(int slot)
		{
			int cols = Math.Max(1, D.LoiterSectors);
			int col = ((slot % cols) + cols) % cols;
			return (col - (cols - 1) * 0.5f) * (2f * D.LoiterHalfWidthTiles / cols);
		}

		/// <summary>The altitude band, in tiles above the roof, for <paramref name="slot"/>: the lower half of the loiter band for the first row of sectors, the upper half for the next.</summary>
		public static void AltRangeTiles(int slot, out float lo, out float hi)
		{
			int cols = Math.Max(1, D.LoiterSectors);
			int row = ((slot / cols) % 2 + 2) % 2;
			float mid = (D.LoiterMinAltTiles + D.LoiterMaxAltTiles) * 0.5f;
			lo = row == 0 ? D.LoiterMinAltTiles : mid;
			hi = row == 0 ? mid : D.LoiterMaxAltTiles;
		}

		/// <summary>A random loiter waypoint for <paramref name="slot"/>, as tiles right of the Barracks centre and tiles above its roof. Always inside the loiter box.</summary>
		public static void PickWaypointTiles(int slot, UnifiedRandom rand, out float dxTiles, out float altTiles)
		{
			float half = D.LoiterHalfWidthTiles;
			dxTiles = Math.Clamp(SectorCenterTiles(slot) + (rand.NextFloat() * 2f - 1f) * D.SectorSpreadTiles, -half, half);
			AltRangeTiles(slot, out float lo, out float hi);
			altTiles = lo + rand.NextFloat() * (hi - lo);
		}
	}
}
