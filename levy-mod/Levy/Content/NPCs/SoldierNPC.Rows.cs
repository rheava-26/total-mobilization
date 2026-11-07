using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// ROW SPREAD. A player in a built arena does not stand on the floor: the gunners and mages go up on the platform rows, spread over them so one sweep or one charge does not catch the whole squad,
	/// pick rows with a line to the boss, hold them while they shoot, and drop when the boss works the row. A ranged levy in a boss fight looks at the standable tiles around it (any height it can
	/// reach with its jump, second jump, wings or hook, plus the rows below it when it stands on a platform), scores each one the way a player would (how crowded that band is, a firing line and reach to the boss,
	/// whether the boss or a worm is working that lane, the walk and the climb) and goes to the best one when it beats the footing it has by a clear margin. Rows are a goal, never a cage: the threat dodge, the
	/// standoff retreat and the worm kite all run first and override it. Nothing here is keyed to a boss name or to the arena: it reads the tiles.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>0 = off (the old lane rule), 1 = row spread (harness switch LEVY_ROWS=0 turns it off).</summary>
		internal static int RowsMode = Environment.GetEnvironmentVariable("LEVY_ROWS") == "0" ? 0 : 1;
		/// <summary>Harness telemetry (reset at fight start): looks taken, climbs started, arrivals, looks with no better spot, drops (a lower band chosen).</summary>
		internal static int RowEvalsS, RowClimbsS, RowArriveS, RowStayS, RowDropsS, RowThreatEvalsS;
		internal static bool RowDbg = Environment.GetEnvironmentVariable("LEVY_ROWDBG") == "1";
		private int _rowNext, _rowHoldUntil, _rowGoalBand = int.MinValue, _rowStartedTick;
		private bool _rowGoingUp;
		/// <summary>The band (tiles above the ground under it) of the spot this body is heading for or holding; int.MinValue when none. Friends read it to avoid the same band.</summary>
		internal int RowBand => _platActive ? _rowGoalBand : int.MinValue;

		private const int RowColumns = 18, RowDropColumns = 6, RowEvalTicks = 70, RowHoldTicks = 150, RowThreatEvalTicks = 25;
		private const float RowCrowdColumns = 22f, RowCrowdWeight = 3.2f, RowMoveMargin = 2.4f, RowBandTiles = 3f, RowStickyBonus = 1.6f, RowLaneTiles = 3.5f, RowLaneColumns = 26f;

		/// <summary>Tiles above the first solid ground under (x, y) (platforms do not count as ground); 40 when there is none within the scan.</summary>
		private static int BandAlt(int x, int y)
		{
			for (int r = y + 1; r <= y + 48; r++) if (TileNav.IsSolid(x, r)) return r - y - 1;
			return 48;
		}

		/// <summary>True while this body has just picked (or is standing on) a row and should not be pulled off it by the high-ground search.</summary>
		private bool RowHolding(int now) => RowsMode >= 1 && now < _rowHoldUntil;

		/// <summary>The nearest segmented-boss head that is locked on a levy, and that levy.</summary>
		private bool ChainTargetOf(out SoldierNPC tgt)
		{
			tgt = null;
			var heads = ThreatField.Snapshot() == null ? null : ThreatField.ChainHeads;
			if (heads == null || heads.Count == 0) return false;
			float best = float.MaxValue;
			for (int i = 0; i < heads.Count; i++)
			{
				NPC h = Main.npc[heads[i]];
				if (!h.active) continue;
				SoldierNPC t = EnemyTargeting.SoldierTargetOf(h);
				if (t == null) continue;
				float d = Vector2.DistanceSquared(h.Center, NPC.Center);
				if (d < best) { best = d; tgt = t; }
			}
			return tgt != null;
		}

		/// <summary>
		/// One look at the footing around this body: returns true when a climb or a drop to a better row was started (the platform goal is set and <see cref="TickPlatformPerch"/> drives it).
		/// </summary>
		private bool TickRowSpread(int now)
		{
			if (now < _rowNext || NPC.velocity.Y != 0f) return false;
			if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) return false;
			NPC bo = Main.npc[TargetIndex];
			if (!bo.active) return false;
			RowEvalsS++;
			_rowNext = now + RowEvalTicks + (int)(SlotFrac(SlotSaltPhase) * 50f);
			NPC seg = Aiming.NearestSegment(bo, NPC.Center) ?? bo;
			float tile = LevyConstants.TilePx;
			float range = PlanningRange(bo);
			float standoff = IsFlamethrower ? LevyConstants.FlameStandoffTiles : StandoffTiles(range);
			bool ignoresTiles = ShotIgnoresTiles();
			Point feet = TileNav.FeetTile(NPC.Bottom);
			bool onPlatform = TileNav.IsPlatform(feet.X, feet.Y + 1);
			int maxUp = Abilities.JumpTiles + (Abilities.ExtraJumps > 0 ? ExtraJumpReachTiles * Math.Min(2, Abilities.ExtraJumps) : 0);
			if (Abilities.HasWings) maxUp = Math.Max(maxUp, 16);
			if (HookUsable) maxUp = Math.Max(maxUp, 12);
			int curAlt = BandAlt(feet.X, feet.Y);

			// is the boss working the floor (a hopper, a charger, a tunneller)? smoothed like the lane rule: a hopper is on the ground half the time
			int bossAlt = BandAlt((int)(seg.Center.X / 16f), (int)(seg.Bottom.Y / 16f) - 1);
			_platLow = _platLow * 0.5f + (bossAlt <= PlatformLaneLowTiles ? 0.5f : 0f);
			bool floorBoss = _platLow > 0.4f;
			bool worm = ChainTargetOf(out SoldierNPC wormTgt);
			if (worm) floorBoss = true;
			int wormBand = worm && wormTgt != null && wormTgt != this && wormTgt.NPC.velocity.Y == 0f ? BandAlt((int)(wormTgt.NPC.Center.X / 16f), (int)((wormTgt.NPC.Bottom.Y - 1f) / 16f)) : int.MinValue;

			// friends that stand (or are bound) in a band near here: ranged bodies only, melee bodies hold the floor on purpose
			int fn = 0;
			for (int i = 0; i < EnemyTargeting.Bodies.Count && fn < _rowFriendX.Length; i++)
			{
				var b = EnemyTargeting.Bodies[i];
				if (!b.IsAlive || b.NPC.whoAmI == NPC.whoAmI || b.NPC.ModNPC is not SoldierNPC f || f.Record == null || f.PositionsAsMelee) continue;
				float fdx = Math.Abs(b.NPC.Center.X - NPC.Center.X) / tile;
				if (fdx > RowCrowdColumns + RowColumns) continue;
				int band;
				if (f._platActive && f._rowGoalBand != int.MinValue) { _rowFriendX[fn] = f._platGoal.X / tile; band = f._rowGoalBand; }
				else if (b.NPC.velocity.Y == 0f) { _rowFriendX[fn] = b.NPC.Center.X / tile; band = BandAlt((int)(b.NPC.Center.X / 16f), (int)((b.NPC.Bottom.Y - 1f) / 16f)); }
				else continue;
				_rowFriendBand[fn++] = band;
			}

			float bossX = seg.Center.X / tile, bossY = seg.Center.Y / tile;
			Vector2 bc = seg.Center;
			// score of standing in tile (x, y): higher is better. LOS and reach are checked separately on the best few.
			float Score(int x, int y, int alt, out Vector2 bottom)
			{
				bottom = TileNav.BottomCentre(x, y);
				float sc = 0f;
				float cx = x + 0.5f;
				float crowd = 0f;
				for (int i = 0; i < fn; i++)
				{
					if (Math.Abs(_rowFriendBand[i] - alt) >= RowBandTiles) continue;
					float d = Math.Abs(_rowFriendX[i] - cx);
					if (d >= RowCrowdColumns) continue;
					crowd += 1f - d / RowCrowdColumns;
				}
				sc -= RowCrowdWeight * crowd;
				// the lane the boss works: level with it and close is where its body, charge or sweep comes through
				float bodyY = y - 1f;
				float dyBoss = Math.Abs(bodyY - bossY), dxBoss = Math.Abs(cx - bossX);
				if (dyBoss < RowLaneTiles && dxBoss < RowLaneColumns) sc -= 4f * (1f - dyBoss / RowLaneTiles) * (1f - dxBoss / RowLaneColumns * 0.5f);
				if (floorBoss && alt < PlatformLaneRowTiles) sc -= worm ? 6f : 4f;
				if (wormBand != int.MinValue && Math.Abs(wormBand - alt) < RowBandTiles && Math.Abs(cx - wormTgt.NPC.Center.X / tile) < 30f) sc -= 5f;
				sc -= 0.12f * Math.Abs(cx - NPC.Center.X / tile);
				int rise = feet.Y - y;
				sc -= 0.15f * Math.Abs(rise);
				if (rise > Abilities.JumpTiles) sc -= 0.8f; // needs the second jump, the wings or the hook
				return sc;
			}

			Point bestTile = default; float bestScore = float.MinValue; int bestAlt = 0;
			_rowCandN = 0;
			for (int dx = -RowColumns; dx <= RowColumns; dx++)
			{
				int x = feet.X + dx;
				if (Math.Abs(seg.Center.X / 16f - x) > 60f) continue;
				for (int y = feet.Y - maxUp; y <= feet.Y + PlatformLaneDrop; y++)
				{
					int dy = y - feet.Y;
					if (dy == 0 && dx == 0) continue;
					if (dy > 0 && !(onPlatform && Math.Abs(dx) <= RowDropColumns)) continue;   // a lower lane is reached by dropping through where it stands
					if (Math.Abs(dy) < 2 && Math.Abs(dx) < 3) continue;
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					if (!InFightZone(now, new Vector2(x * 16f + 8f, y * 16f))) continue;
					if (dy > 0 && TileNav.IsStandable(x, y - 1)) continue;
					if (Math.Abs(dy) >= 2 && TileNav.IsStandable(x, y - 1)) continue; // the tile just above a floor is the same footing
					int alt = BandAlt(x, y);
					float sc = Score(x, y, alt, out Vector2 bottom);
					if (_rowCandN < _rowCandPos.Length) { _rowCandPos[_rowCandN] = new Point(x, y); _rowCandScore[_rowCandN] = sc; _rowCandAlt[_rowCandN] = alt; _rowCandN++; }
				}
			}
			// current footing, scored the same way (sticky)
			float cur = Score(feet.X, feet.Y, curAlt, out Vector2 curBottom) + RowStickyBonus;
			Vector2 curBody = curBottom - new Vector2(0f, BodyHeight * 0.5f);
			bool curFires = SpotFires(NPC.Bottom, bc, range, standoff, LevyConstants.ShootableRangeFrac, 0f, ignoresTiles);
			if (!curFires) cur -= 5f;
			// the best few by score, checked for a firing line and reach (a levy goes where it can shoot from)
			for (int pass = 0; pass < 5 && _rowCandN > 0; pass++)
			{
				int bi = -1; float bs = float.MinValue;
				for (int i = 0; i < _rowCandN; i++) if (_rowCandScore[i] > bs) { bs = _rowCandScore[i]; bi = i; }
				if (bi < 0 || bs < cur + RowMoveMargin) break;
				Point p = _rowCandPos[bi]; int alt = _rowCandAlt[bi]; _rowCandScore[bi] = float.MinValue;
				Vector2 bottom = TileNav.BottomCentre(p.X, p.Y);
				if (!SpotFires(bottom, bc, range, standoff, LevyConstants.PerchRangeFrac, LevyConstants.PerchStandoffMarginTiles, ignoresTiles)) continue;
				if (IsBadPerch(p.X, p.Y, now)) continue;
				bestTile = p; bestScore = bs; bestAlt = alt; break;
			}
			if (RowDbg && (RowEvalsS % 7) == 0) Levy.Instance?.Logger.Info($"ROWDBG {SoldierName} t={now} feet=({feet.X},{feet.Y}) alt={curAlt} plat={onPlatform} maxUp={maxUp} jt={Abilities.JumpTiles} xj={Abilities.ExtraJumps} hook={HookUsable} cand={_rowCandN} cur={cur:0.0} fires={curFires} best={(bestScore == float.MinValue ? "none" : bestTile.X + "," + bestTile.Y + " " + bestScore.ToString("0.0"))} fn={fn} floorBoss={floorBoss} worm={worm}");
			if (bestScore == float.MinValue)
			{
				RowStayS++;
				if (curFires && (onPlatform || curAlt >= PlatformLaneRowTiles)) _rowHoldUntil = now + RowHoldTicks;
				return false;
			}
			_platGoal = TileNav.BottomCentre(bestTile.X, bestTile.Y); _platActive = true; _platStart = now; PlatformClimbs++;
			_rowGoalBand = bestAlt; _rowGoingUp = bestTile.Y < feet.Y; _rowStartedTick = now;
			if (_rowGoingUp) RowClimbsS++; else RowDropsS++;
			_rowHoldUntil = now + RowHoldTicks * 2;
			if (_perchActive) AbandonPerch(now);
			SetGoal(_platGoal);
			return true;
		}

		/// <summary>The boss (or a worm head) is level with this body on its row, close, and moving: look at the footing again now instead of at the next scheduled look.</summary>
		private void RowThreatPoke(int now)
		{
			if (RowsMode < 1 || _rowNext <= now + 20) return;
			if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs || now < _rowThreatNext) return;
			_rowThreatNext = now + RowThreatEvalTicks;
			NPC bo = Main.npc[TargetIndex];
			if (!bo.active) return;
			NPC seg = Aiming.NearestSegment(bo, NPC.Center) ?? bo;
			float dy = Math.Abs(seg.Center.Y - NPC.Center.Y) / LevyConstants.TilePx, dx = Math.Abs(seg.Center.X - NPC.Center.X) / LevyConstants.TilePx;
			if (dy < RowLaneTiles && dx < 18f && seg.velocity.LengthSquared() > 9f) { _rowNext = now; RowThreatEvalsS++; }
		}

		private int _rowThreatNext, _rowCandN;
		private static readonly float[] _rowFriendX = new float[64];
		private static readonly int[] _rowFriendBand = new int[64];
		private static readonly Point[] _rowCandPos = new Point[1400];
		private static readonly float[] _rowCandScore = new float[1400];
		private static readonly int[] _rowCandAlt = new int[1400];
	}
}
