using System;
using Levy.Common.Owner;

namespace Mechanization.Common
{
	/// <summary>
	/// A round a drone or a turret fires is owned by the real player (kill credit, loot, banners), so any code that reads or writes "the owner" during that round's life lands on the player: a held-weapon
	/// shot that keeps writing <c>owner.heldProj / itemAnimation / itemTime</c>, a modded hook that kicks <c>owner.velocity</c> or flips Calamity's <c>mouseWorldListener</c>. The player's own weapon stops working
	/// for the moment that happens.
	/// <para/>The rule is Levy's, for the whole army, not Mechanization's: <see cref="UseGuard"/> puts the player's use state, heading, speed and arms back exactly as they were after every call into any army round
	/// (its spawn, each tick of its life, its hit pass, its death), and a machine's round counts as the army's because Levy tagged it (SoldierProjectileTag.DroneShot, inherited by its fragments). This class is the machine
	/// side of that one guard: <see cref="Hold"/> wraps a machine spawning its rounds, and the counters the world check reads are the guard's.
	/// </summary>
	public static class OwnerGuard
	{
		/// <summary>Runs <paramref name="body"/> (a machine spawning its rounds) and puts the owner's use state back afterwards.</summary>
		public static void Hold(int owner, Action body) => UseGuard.Hold(owner, body);

		// Measurement (the world check turns it on): time spent in Take + Put, in Stopwatch ticks, and how many snapshots that was.
		internal static bool Measure { get => UseGuard.Measure; set => UseGuard.Measure = value; }
		internal static long GuardTicks { get => UseGuard.GuardTicks; set => UseGuard.GuardTicks = value; }
		internal static int GuardSnaps { get => UseGuard.GuardSnaps; set => UseGuard.GuardSnaps = value; }

		/// <summary>How many round updates the guard has wrapped, and how many times it had to put the player's state back (the check reads them).</summary>
		internal static int GuardedUpdates => UseGuard.GuardedUpdates;
		internal static int Undone => UseGuard.Undone;

		/// <summary>Cost of one snapshot + restore of <paramref name="owner"/>'s use state, in microseconds, over <paramref name="n"/> round trips (the check reports it).</summary>
		internal static double BenchMicros(int owner, int n) => UseGuard.BenchMicros(owner, n);
	}
}
