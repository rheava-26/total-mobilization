using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// Search &amp; Destroy keeps Follow's catch-up: a Regular more than <see cref="LevyConstants.TeleportDistance"/> tiles from its commander, out of every player's sight and past the teleport cooldown,
	/// hops to the commander's side (<see cref="CatchUpTeleport"/>, the same hop Follow uses) and starts its sweep around the landing spot. Free Rein never calls this: it roams where it likes and never teleports.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>True when the levy hopped to the commander this think (the caller ends the think).</summary>
		private bool SearchCatchUp(int now)
		{
			if (!IsRegular || Record == null || !CatchUpOrder(Record.Order)) return false; // Free Rein, Hold and militia never teleport
			Player cmd = Commander;
			if (cmd == null || cmd.dead) return false;
			if (!SearchCatchUpDue(LevyUtil.TileDistance(NPC.Center, cmd.Center), now - _lastTeleportTick, VisibleToAnyPlayer())) return false;
			_lastTeleportTick = now; // the cooldown also spaces out a hop that found no ground
			Vector2 before = NPC.Center;
			CatchUpTeleport(cmd);
			if (NPC.Center == before) return false; // no ground near the commander: stay and keep hunting
			_searchAnchor = NPC.Center; _searchAnchorSet = true; // sweep around the landing spot, not the old anchor the hop just left
			_searchGoal = default; _searchGoalTick = 0; _searchPauseUntil = 0;
			return true;
		}

		/// <summary>Which standing order teleports to catch up: Search &amp; Destroy only (Follow has its own hop; Free Rein never hops).</summary>
		internal static bool CatchUpOrder(SoldierOrder order) => order == SoldierOrder.SearchDestroy;

		/// <summary>The catch-up rule as a pure function: far from the commander, past the cooldown, and unseen.</summary>
		internal static bool SearchCatchUpDue(float tilesFromCommander, int ticksSinceLastHop, bool seen)
			=> tilesFromCommander > LevyConstants.TeleportDistance && ticksSinceLastHop >= LevyConstants.TeleportCooldownTicks && !seen;
	}
}
