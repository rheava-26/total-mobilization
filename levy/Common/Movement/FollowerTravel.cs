using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Movement
{
	/// <summary>Followers come along through pylons, mirrors, recall, teleporters; off-screen catch-up teleport. OWNED BY THE MOVEMENT AGENT.</summary>
	public static class FollowerTravel
	{
		/// <summary>Finds a free standing spot near <paramref name="near"/> (no lava/water/solid), within radius tiles. Returns false if none.</summary>
		public static bool FindStandingSpot(Vector2 near, int radiusTiles, out Vector2 spot) { spot = near; return false; }
		/// <summary>Moves all following regulars of the commander next to them (server).</summary>
		public static void BringFollowers(Player commander) { }
	}
}
