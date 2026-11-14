using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FOLLOW ROWS. A following army does not line up on the commander's one row: each levy claims a lane on one of the standing rows near the commander (the commander's row, the platform rows above and below within a
	/// band, the ground), spreads across them in proportion to their room, keeps a minimum gap to the levies beside it on its row, and keeps the claim until the row or lane stops being valid. Rows are chosen by what the
	/// levy can reach (jump, second jump, wings, hook, a chain of rows) and a row it cannot get onto is struck off for a while. The scan is shared (<see cref="FollowRows"/>), a claim is a few array reads, and a levy
	/// only looks again when its claim breaks. Close up keeps its tight stack, a single row (flat ground) keeps the old slots. Harness switch LEVY_FOLLOWROWS=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static int FollowRowsMode = Environment.GetEnvironmentVariable("LEVY_FOLLOWROWS") == "0" ? 0 : 1;
		/// <summary>Harness telemetry: claims made, claims broken, rows struck off as unreachable.</summary>
		internal static int FrPicksS, FrBreaksS, FrFailsS;
		private int _frY = int.MinValue, _frLane = -1, _frNextPick, _frReachCheck, _frOffSince, _frBadY = int.MinValue, _frBadUntil;
		/// <summary>The feet-tile y of the row this levy follows on (int.MinValue when it has none); the harness and friends read it.</summary>
		internal int FollowRowY => _frY;

		private const float FrNormalStart = 2.8f, FrNormalStep = 3.0f, FrNormalMax = 30f, FrSpreadStart = 6f, FrSpreadStep = 6f, FrSpreadMax = 24f;
		private const float FollowRowStopTiles = 0.7f, FollowRowStartTiles = 1.6f;
		private const int FrPickEveryTicks = 40, FrOffTicks = 900, FrBadTicks = 1800;

		internal static float FrLaneOffset(int lane, bool spread)
		{
			int i = lane >> 1; float off = spread ? FrSpreadStart + i * FrSpreadStep : FrNormalStart + i * FrNormalStep;
			return (lane & 1) == 0 ? -off : off;
		}
		private static bool FrLaneInRange(int lane, bool spread) => Math.Abs(FrLaneOffset(lane, spread)) <= (spread ? FrSpreadMax : FrNormalMax);

		/// <summary>The whole body standing on lane k (a body is about a tile and a quarter wide) is over one run of the row, not hanging off its end.</summary>
		private static bool FrLaneStands(FollowRowSet.Row row, float cmdX, int lane, bool spread)
		{
			float x = cmdX + FrLaneOffset(lane, spread);
			int a = (int)(x - 0.9f), b = (int)(x + 0.9f);
			for (int i = 0; i < row.Runs; i++) if (a >= row.RunA[i] && b <= row.RunB[i]) return true;
			return false;
		}

		private bool FrLaneOwnedByLiving(FollowRowSet.Row row, int k)
		{
			int n = row.OwnerNpc[k] - 1; if (n < 0 || n >= Main.maxNPCs) return false;
			NPC o = Main.npc[n];
			return o.active && o.ModNPC is SoldierNPC s && s.Record != null && s.Record.Id == row.OwnerId[k] && s._frY == row.Y && s._frLane == k && s.Record.Order == SoldierOrder.Follow && s.State != SoldierState.Dead;
		}

		private void FrRelease(FollowRowSet set)
		{
			if (_frY != int.MinValue && set != null)
			{
				int ri = set.IndexOfY(_frY);
				if (ri >= 0 && _frLane >= 0 && set.Rows[ri].OwnerNpc[_frLane] == NPC.whoAmI + 1) { set.Rows[ri].OwnerNpc[_frLane] = 0; set.Rows[ri].OwnerId[_frLane] = 0; }
			}
			_frY = int.MinValue; _frLane = -1; _frOffSince = 0;
		}

		/// <summary>True when the row at index <paramref name="to"/> can be reached from the standing spot by this body's gear, over a chain of rows (each step a jump up or a drop down onto the next row).</summary>
		private void FrReach(FollowRowSet set, bool[] reach)
		{
			int n = set.Count;
			int maxUp = Abilities.JumpTiles + (Abilities.ExtraJumps > 0 ? ExtraJumpReachTiles * Math.Min(2, Abilities.ExtraJumps) : 0);
			if (Abilities.HasWings) maxUp = Math.Max(maxUp, 16);
			if (HookUsable) maxUp = Math.Max(maxUp, 12);
			int maxDown = Abilities.HasWings ? 30 : Abilities.JumpTiles + DropMarginTiles;
			Point feet = TileNav.FeetTile(NPC.Bottom);
			var queue = new int[FollowRowSet.MaxRows]; int qh = 0, qt = 0;
			for (int i = 0; i < n; i++) reach[i] = false;
			// where this body stands now: the row it is on, else any row it could jump or drop to straight from where it is
			for (int i = 0; i < n; i++)
			{
				var r = set.Rows[i]; int dy = feet.Y - r.Y; // > 0: the row is above
				bool hx = feet.X >= r.X0 - 3 && feet.X <= r.X1 + 3;
				if (Math.Abs(dy) <= 1 && hx) { reach[i] = true; queue[qt++] = i; }
			}
			if (qt == 0)
				for (int i = 0; i < n; i++)
				{
					var r = set.Rows[i]; int dy = feet.Y - r.Y;
					bool hx = feet.X >= r.X0 - 6 && feet.X <= r.X1 + 6;
					if (hx && (dy > 0 ? dy <= maxUp : -dy <= maxDown)) { reach[i] = true; queue[qt++] = i; }
				}
			if (qt == 0) { for (int i = 0; i < n; i++) reach[i] = true; return; } // nowhere to chain from: let the failure feedback judge it
			while (qh < qt)
			{
				var a = set.Rows[queue[qh++]];
				for (int j = 0; j < n; j++)
				{
					if (reach[j]) continue; var b = set.Rows[j];
					int dy = a.Y - b.Y; // > 0: b is above a
					if (!(dy > 0 ? dy <= maxUp : -dy <= maxDown)) continue;
					if (!FrRowsOverlap(a, b)) continue; // a jump straight up through the row above, or a walk off the edge onto the row below, needs room under / over it
					reach[j] = true; queue[qt++] = j;
				}
			}
		}

		/// <summary>Two rows share at least two columns (some run of one lies over some run of the other).</summary>
		private static bool FrRowsOverlap(FollowRowSet.Row a, FollowRowSet.Row b)
		{
			for (int i = 0; i < a.Runs; i++) for (int j = 0; j < b.Runs; j++) if (Math.Min(a.RunB[i], b.RunB[j]) - Math.Max(a.RunA[i], b.RunA[j]) >= 1) return true;
			return false;
		}

		private static readonly bool[] _frReach = new bool[FollowRowSet.MaxRows];

		/// <summary>
		/// The slot a following levy stands at on its claimed lane. False when this levy takes the plain slot instead (rows off, Close up, fewer than two rows, no free lane).
		/// </summary>
		private bool TryFollowRowSlot(Player cmd, int now, bool spread, out Vector2 slot)
		{
			slot = default;
			if (FollowRowsMode < 1 || Record == null || Record.CloseUp || cmd == null) { if (_frY != int.MinValue) FrRelease(null); return false; }
			var set = FollowRows.Get(cmd, now);
			if (set == null || set.Count < 2) { if (_frY != int.MinValue) { FrRelease(set); } return false; }
			Point feet = TileNav.FeetTile(NPC.Bottom);
			float cmdX = cmd.Center.X / LevyConstants.TilePx;

			// 1. is the claim still good?
			if (_frY != int.MinValue)
			{
				int ri = set.IndexOfY(_frY);
				bool ok = ri >= 0 && _frLane >= 0 && set.Rows[ri].OwnerNpc[_frLane] == NPC.whoAmI + 1 && FrLaneInRange(_frLane, spread) && FrLaneStands(set.Rows[ri], cmdX, _frLane, spread);
				if (ok && (set.Rows[ri].InBand == false) && Math.Abs(_frY - set.CmdY) > FollowRows.BandUp + FollowRows.BandSlack) ok = false;
				if (ok && _frBadY == _frY && now < _frBadUntil) ok = false;
				if (ok && NPC.velocity.Y == 0f && Math.Abs(feet.Y - _frY) > 2 && now >= _frReachCheck)
				{
					// away from its row (a fight, a fall, the commander moved): a row this body cannot get back onto is no claim at all
					_frReachCheck = now + 60; FrReach(set, _frReach);
					if (!_frReach[ri]) { ok = false; _frBadY = _frY; _frBadUntil = now + FrBadTicks; }
				}
				if (ok)
				{
					// a body that has stayed off its row for a long while (grounded, more than two tiles from its height) cannot get there: strike the row off for this levy
					bool offRow = NPC.velocity.Y == 0f && Math.Abs(feet.Y - _frY) > 2;
					if (!offRow) _frOffSince = 0;
					else if (_frOffSince == 0) _frOffSince = now;
					else if (now - _frOffSince > FrOffTicks) { _frBadY = _frY; _frBadUntil = now + FrBadTicks; FrFailsS++; ok = false; }
				}
				if (!ok) { FrBreaksS++; FrRelease(set); _frNextPick = Math.Min(_frNextPick, now); }
			}

			// 2. claim a lane
			if (_frY == int.MinValue)
			{
				if (now < _frNextPick) return false;
				_frNextPick = now + FrPickEveryTicks + (int)(SlotFrac(SlotSaltPhase) * 40f);
				FrReach(set, _frReach);
				int bestRow = -1, bestLane = -1; float bestScore = float.MaxValue;
				for (int pass = 0; pass < 2 && bestRow < 0; pass++) // pass 0: the rows in the band; pass 1: any row this body can reach (a levy that cannot get into the band stands where it can)
				for (int r = 0; r < set.Count; r++)
				{
					var row = set.Rows[r];
					if (!_frReach[r] || (pass == 0 && !row.InBand) || (row.Y == _frBadY && now < _frBadUntil)) continue;
					int occupied = 0, valid = 0, free = -1;
					for (int k = 0; k < FollowRowSet.MaxLanes; k++)
					{
						if (!FrLaneInRange(k, spread)) break;
						if (!FrLaneStands(row, cmdX, k, spread)) continue;
						valid++;
						if (row.OwnerNpc[k] != 0 && row.OwnerNpc[k] - 1 != NPC.whoAmI && FrLaneOwnedByLiving(row, k)) { occupied++; continue; }
						if (free < 0) free = k;
					}
					if (free < 0 || valid == 0) continue;
					float weight = Math.Min(valid, 24) * (r == set.CmdRow ? 1.4f : 1f) / (1f + 0.05f * Math.Abs(row.Y - set.CmdY));
					float score = (occupied + 1) / weight + 0.01f * Math.Abs(row.Y - feet.Y);
					if (score < bestScore) { bestScore = score; bestRow = r; bestLane = free; }
				}
				if (bestRow < 0) return false;
				var pr = set.Rows[bestRow];
				pr.OwnerNpc[bestLane] = NPC.whoAmI + 1; pr.OwnerId[bestLane] = Record.Id;
				_frY = pr.Y; _frLane = bestLane; _frOffSince = 0; FrPicksS++;
			}

			float tx = cmd.Center.X + FrLaneOffset(_frLane, spread) * LevyConstants.TilePx;
			slot = new Vector2(tx, (_frY + 1) * 16f);
			return true;
		}
	}
}
