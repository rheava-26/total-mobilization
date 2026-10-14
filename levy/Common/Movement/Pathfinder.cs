using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Levy.Common.Movement
{
	/// <summary>Coarse tile-grid search with step/jump/fall/flight edges. OWNED BY THE MOVEMENT AGENT.</summary>
	public static class Pathfinder
	{
		/// <summary>Fills <paramref name="path"/> with world-space waypoints (first = next step). Returns false if unreachable within budget.</summary>
		public static bool FindPath(Vector2 fromWorld, Vector2 toWorld, MovementAbilities abilities, List<Vector2> path, int maxNodes = 2500) { path.Clear(); return false; }
	}
}
