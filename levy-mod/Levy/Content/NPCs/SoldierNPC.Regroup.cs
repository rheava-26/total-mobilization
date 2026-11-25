using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Recording;
using Levy.Common.Movement;
using Levy.Common.Systems;
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

		internal static void InvalidateBossFight() => _bfStamp = uint.MaxValue;

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

		/// <summary>Lab switch (LEVY_REGROUPLEASH=0): the earlier Regroup, whose Free Rein base is the barracks or where the order was given and which never hops back.</summary>
		internal static bool RegroupLeashOn = System.Environment.GetEnvironmentVariable("LEVY_REGROUPLEASH") != "0";

		/// <summary>True while this levy is on Regroup &amp; Engage and inside a boss fight (the order reads as Free Rein): its base is the commander, it keeps within the fight zone around them, and it hops back when it is not on the boss.</summary>
		internal bool RegroupLeashed => RegroupLeashOn && _rgFree && IsRegular && Record != null && Record.Order == SoldierOrder.Regroup;

		/// <summary>Harness telemetry: hops back to the commander.</summary>
		internal static int RegroupSnaps;
		private int _rgOffBossSince = -1;

		/// <summary>Free Rein's base while leashed: the commander's feet, followed as they move (the anchor is re-set after a few tiles). False when the commander is gone.</summary>
		private bool RegroupBase(out Vector2 a)
		{
			a = default;
			Player cmd = Commander;
			if (cmd == null || cmd.dead) return false;
			a = cmd.Bottom + new Vector2(0f, -8f);
			if (!_searchAnchorSet || Vector2.DistanceSquared(_searchAnchor, a) > 96f * 96f) { _searchAnchor = a; _searchAnchorSet = true; FreeReinAnchorSet(a); }
			a = _searchAnchor;
			return true;
		}

		/// <summary>True when the NPC is the boss, a part of it, or one of the fight's bosses (the brothers of a boss that fights beside it count).</summary>
		internal static bool IsBossTarget(NPC n) => n != null && n.active && (FightRecorder.IsFightBoss(n) || FightRecorder.IsBossOrPart(n) || n.boss);

		/// <summary>
		/// A Regroup &amp; Engage levy in a boss fight that is not on the boss (no target, or an add) for <see cref="LevyConstants.RegroupIdleTicks"/> and stands out of reach of the commander hops back beside them.
		/// True when it hopped (the caller ends the think). Outside a boss fight, and on every other order, this does nothing.
		/// </summary>
		private bool RegroupSnapBack(int now)
		{
			if (!RegroupLeashed || Hostile || State == SoldierState.Dead || State == SoldierState.Desert || State == SoldierState.Routed) { _rgOffBossSince = -1; return false; }
			Player cmd = Commander;
			if (cmd == null || cmd.dead) return false;
			if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && IsBossTarget(Main.npc[TargetIndex])) { _rgOffBossSince = -1; return false; }
			if (_rgOffBossSince < 0) _rgOffBossSince = now;
			if (now - _rgOffBossSince < LevyConstants.RegroupIdleTicks) return false;
			if (LevyUtil.TileDistance(NPC.Center, cmd.Center) <= LevyConstants.RegroupNearTiles) return false;
			if (now - _lastTeleportTick < LevyConstants.RegroupSnapCooldownTicks) return false;
			_lastTeleportTick = now;
			Vector2 before = NPC.Center;
			int side = ((NPC.whoAmI & 1) == 0 ? 1 : -1);
			Vector2 want = cmd.Bottom + new Vector2(side * LevyConstants.RegroupBesideTiles * LevyConstants.TilePx, 0f);
			if (!FollowerTravel.FindStandingSpot(want, LevyConstants.RegroupSpotSearchTiles, out Vector2 spot)
				&& !FollowerTravel.FindSafeGround(cmd.Center, LevyConstants.RegroupSpotSearchTiles * 2, out spot)) return false; // no ground near them: stay
			spot = RosterSystem.SafeSpawnPosition(spot);
			NPC.Bottom = spot; NPC.velocity = Vector2.Zero; NPC.netUpdate = true;
			ClearGoal(); TargetIndex = -1; _haulTargetItem = -1; Movement_OnTeleported();
			_rgOffBossSince = now; RegroupSnaps++;
			return NPC.Center != before;
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

namespace Levy.Content.NPCs
{
	/// <summary>Drops the once-a-tick boss-fight reading before each NPC update, so a boss that spawned or died is seen by the very next think whatever the tick counter does.</summary>
	public class RegroupFightSystem : Terraria.ModLoader.ModSystem
	{
		public override void PreUpdateNPCs() => SoldierNPC.InvalidateBossFight();
	}
}
