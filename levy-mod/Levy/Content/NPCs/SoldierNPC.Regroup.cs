using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Recording;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE REGROUP ORDER. A regular on <see cref="SoldierOrder.Regroup"/> behaves as Search &amp; Destroy until a boss fight is on, then as Free Rein while it is in that fight (it fights the boss or the boss is within
	/// the free-rein scout reach), and goes back to Search &amp; Destroy when the fight ends. There is no second AI: <see cref="EffOrder"/> names the order the existing Search / Free Rein code reads.
	/// A levy that respawns or is raised under the order appears beside its commander (RosterSystem.SpawnBody).
	/// </summary>
	public partial class SoldierNPC
	{
		// ---- the boss fight: any living main boss, the same test the fight recorder uses (FightRecorder.IsFightBoss), read once a tick
		private static uint _bfStamp = uint.MaxValue; private static bool _bfOn;
		private static readonly List<Vector2> _bfPos = new();
		/// <summary>Test hook: when set, overrides whether a boss fight is on.</summary>
		internal static bool? BossFightForTest;

		internal static bool BossFightNow
		{
			get
			{
				if (BossFightForTest.HasValue) return BossFightForTest.Value;
				if (_bfStamp == Main.GameUpdateCount) return _bfOn;
				_bfStamp = Main.GameUpdateCount; _bfOn = false; _bfPos.Clear();
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || !FightRecorder.IsFightBoss(n)) continue;
					_bfOn = true; _bfPos.Add(n.Center);
				}
				return _bfOn;
			}
		}

		private bool _rgFree;

		/// <summary>The order the roaming brain reads: the record's order, except Regroup, which is Free Rein while this levy is in a boss fight and Search &amp; Destroy otherwise.</summary>
		internal SoldierOrder EffOrder
		{
			get
			{
				SoldierOrder o = Record != null ? Record.Order : SoldierOrder.Follow;
				if (o != SoldierOrder.Regroup) return o;
				bool free = RegroupInFight();
				if (free != _rgFree)
				{
					_rgFree = free;
					_searchAnchorSet = false; HasMoveGoal = false; // the other behaviour sets its own anchor and its own first goal
				}
				return free ? SoldierOrder.FreeRein : SoldierOrder.SearchDestroy;
			}
		}

		/// <summary>A boss fight is on and this levy is in it: engaged, or a boss within the free-rein scout reach. Once in, it stays in until the fight ends.</summary>
		private bool RegroupInFight()
		{
			if (!BossFightNow) return false;
			if (_rgFree) return true;
			if (State == SoldierState.Engage || TargetIndex >= 0) return true;
			float reach = LevyConstants.FreeReinScoutTiles * LevyConstants.TilePx; reach *= reach;
			for (int i = 0; i < _bfPos.Count; i++) if (Vector2.DistanceSquared(_bfPos[i], NPC.Center) <= reach) return true;
			return false;
		}
	}
}
