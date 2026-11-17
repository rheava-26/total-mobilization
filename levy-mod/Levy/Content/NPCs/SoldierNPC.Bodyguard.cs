using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Compat.Calamity;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// BODYGUARD. A boss that has this levy in its sights is going to follow it, so the levy goes where the boss is least harmful to follow it: away from the commander first, then away from the nearest clump of the other levies,
	/// in a leg of <see cref="Bodyguard.LegTiles"/> tiles that is held for <see cref="Bodyguard.HoldTicks"/> ticks (and for <see cref="Bodyguard.GraceTicks"/> after the boss looks away, so a boss that swaps targets does not make a levy stutter).
	/// It is positioning only: the target, the weapon and the fire gate are not touched, so the levy keeps shooting while it runs, exactly as a standoff retreat does. A levy that is not the target stays out of the lane between the boss and the target:
	/// a step across it, held until the lane has moved on. A melee levy is left alone (it has to be at the boss to fight it); a levy on a hook, a wall climb or a marching order too.
	/// It runs at the end of the think, after the brain chose its goal and before the fight zone, so the zone still bounds it, and the arena box of a Calamity fight bounds it too. Nothing here is keyed to a boss name.
	/// </summary>
	public partial class SoldierNPC
	{
		private int _bgDir, _bgHoldUntil, _bgActiveUntil, _bgLaneDir, _bgLaneUntil; private bool _bgLaneOn;
		/// <summary>Harness telemetry: decoy legs started, thinks spent holding at the far end, lane sidesteps.</summary>
		internal int BgLegs, BgHolds, BgLaneStepsN;
		/// <summary>True while this levy is running the decoy rule (a boss has it in its sights, or it is still in the grace after).</summary>
		internal bool BodyguardRunning => _bgActiveUntil != 0 && (int)Main.GameUpdateCount < _bgActiveUntil;

		private void ApplyBodyguard(int now) { long pt = global::Levy.Common.Util.LevyProf.Begin(); try { ApplyBodyguardBody(now); } finally { global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.Zone, pt); } }

		private void ApplyBodyguardBody(int now)
		{
			if (Bodyguard.Mode == 0 || Record == null || Hostile || !IsAlive) return;
			var pairs = Bodyguard.Get();
			bool targeted = false; int bossIdx = -1;
			if (pairs.Count > 0) { bossIdx = Bodyguard.BossTargeting(NPC.whoAmI); targeted = bossIdx >= 0; }
			if (!targeted && !BodyguardRunning && !_bgLaneOn && pairs.Count == 0) return; // the usual think: no boss has anyone in its sights
			if (State == SoldierState.Routed || State == SoldierState.Desert || State == SoldierState.Dead || PositionsAsMelee || MarchingOrder) { _bgLaneOn = false; return; }
			if (_hkPhase != HookPhase.Idle || _clmOn || _perchAir) return;
			if (targeted) { RunDecoy(now, Main.npc[bossIdx]); return; }
			if (BodyguardRunning) { if (_bgDir != 0 && !FarFromCommander(out _)) { TrySpreadLeg(now, _bgDir); } return; }
			KeepOffTheLane(now, pairs);
		}

		/// <summary>True (and the distance in tiles) when the commander is further than this levy may be drawn: the fight zone's edge for a Follow levy, <see cref="Bodyguard.StopTilesNoZone"/> otherwise.</summary>
		private bool FarFromCommander(out float tiles)
		{
			tiles = 0f;
			Player cmd = Commander; if (cmd == null) return false;
			tiles = Vector2.Distance(cmd.Center, NPC.Center) / LevyConstants.TilePx;
			bool zone = IsRegular && Record.Order == SoldierOrder.Follow && !HoldPosition && ZoneMode >= 1;
			float stop = zone ? FightZoneTiles * FightZoneEdgeFrac - 1f : Bodyguard.StopTilesNoZone;
			return tiles >= stop;
		}

		private void RunDecoy(int now, NPC boss)
		{
			float tile = LevyConstants.TilePx;
			Vector2 here = NPC.Center; Player cmd = Commander;
			bool fresh = !BodyguardRunning;
			_bgActiveUntil = now + Bodyguard.GraceTicks;
			// which way: the commander decides when she is more than a few tiles to one side, the nearest clump adds its half, and a levy right under her walks away from the boss
			float cmdSide = 0f;
			if (cmd != null) { float dx = here.X - cmd.Center.X; if (Math.Abs(dx) > 3f * tile) cmdSide = Math.Sign(dx); }
			bool recross = _bgDir != 0 && cmdSide != 0f && Math.Abs(here.X - cmd.Center.X) > 6f * tile && cmdSide != _bgDir; // went past her: turn
			if (fresh || _bgDir == 0 || now >= _bgHoldUntil || recross)
			{
				float s = cmdSide;
				if (Bodyguard.ClumpCentre(this, out Vector2 k)) { float dxk = here.X - k.X; if (Math.Abs(dxk) > 2f * tile) s += 0.5f * Math.Sign(dxk); }
				if (s == 0f) { s = Math.Sign(here.X - boss.Center.X); if (s == 0f) s = (NPC.whoAmI & 1) == 0 ? 1f : -1f; }
				int nd = s > 0f ? 1 : -1;
				if (nd != _bgDir || fresh) BgLegs++;
				_bgDir = nd; _bgHoldUntil = now + Bodyguard.HoldTicks;
			}
			if (FarFromCommander(out _)) { ClearGoal(); _platActive = false; WhyB = Why.BBodyguard; BgHolds++; Bodyguard.Holds++; return; } // as far out as the fight lets it: stand and keep shooting
			TrySpreadLeg(now, _bgDir);
		}

		/// <summary>The decoy's leg: the farthest standable tile within the leg length in the chosen direction, bounded by the order's own limits and the arena box.</summary>
		private bool TrySpreadLeg(int now, int dir)
		{
			if (!TryFlatStep(dir, Bodyguard.LegTiles, out Vector2 dest) && !TryFlatStep(dir, Bodyguard.LegTiles / 2, out dest)) { BgHolds++; Bodyguard.Holds++; ClearGoal(); _platActive = false; WhyB = Why.BBodyguard; return false; }
			ClampToArenaBox(ref dest);
			ApplyChaseClamp(ref dest, skipFollow: true); // the order's own leash (Hold, Patrol, Roam, Free Rein); the Follow leash of 30 tiles would pull a levy further out than that back in, and the fight zone bounds it
			SetGoal(dest); _platActive = false; WhyB = Why.BBodyguard; Bodyguard.DecoySteps++;
			return true;
		}

		/// <summary>A Calamity arena box holds a levy like a player: a goal outside it would only press the body against the wall, so it is pulled in to a couple of tiles inside.</summary>
		private void ClampToArenaBox(ref Vector2 dest)
		{
			if (!ArenaBoxes.TryBounds(NPC, out Vector2 tl, out Vector2 br)) return;
			float m = 3f * LevyConstants.TilePx;
			if (br.X - tl.X > 2f * m) dest.X = MathHelper.Clamp(dest.X, tl.X + m, br.X - m);
		}

		/// <summary>A levy that is not the target steps across the line from each boss to the levy it has in its sights, and stays across it while that line is still near.</summary>
		private void KeepOffTheLane(int now, System.Collections.Generic.List<Bodyguard.Pair> pairs)
		{
			float tile = LevyConstants.TilePx; Vector2 here = NPC.Center;
			int side = 0; float worst = float.MaxValue; bool near = false;
			for (int i = 0; i < pairs.Count; i++)
			{
				var p = pairs[i];
				if (p.Body == NPC.whoAmI) continue;
				NPC boss = Main.npc[p.Boss], tgt = Main.npc[p.Body];
				if (!boss.active || !tgt.active) continue;
				Vector2 a = boss.Center, b = tgt.Center;
				if (Vector2.Distance(a, b) < Bodyguard.LaneMinLenTiles * tile) continue;
				float d = Bodyguard.DistToSegmentTiles(here, a, b, out float along, out Vector2 closest);
				float need = Bodyguard.LaneHalfTiles + (_bgLaneOn ? Bodyguard.LaneExtraTiles : 0f);
				if (d >= need || along <= 0.04f || along >= 0.96f) continue;
				Vector2 nrm = here - closest; float nl = nrm.Length();
				if (nl < 1f) { Vector2 ab = b - a; nrm = new Vector2(-ab.Y, ab.X); nl = nrm.Length(); } // standing on the line itself: either side
				if (nl < 1f) continue;
				nrm /= nl;
				if (Math.Abs(nrm.X) < Bodyguard.LaneMinNormalX) continue; // a line along the ground cannot be left by walking
				if (d < worst) { worst = d; side = nrm.X >= 0f ? 1 : -1; near = true; }
			}
			if (!near) { _bgLaneOn = false; return; }
			if (_bgLaneOn && now < _bgLaneUntil && _bgLaneDir != 0) side = _bgLaneDir; // keep the side it chose: no flipping while the lane drifts
			int clear = (int)Math.Ceiling(Bodyguard.LaneHalfTiles + Bodyguard.LaneExtraTiles + 2f - Math.Min(worst, Bodyguard.LaneHalfTiles));
			clear = Math.Clamp(clear, 3, 9);
			if (!TryFlatStep(side, clear, out Vector2 dest)) { _bgLaneOn = false; return; }
			ClampToArenaBox(ref dest);
			ApplyChaseClamp(ref dest);
			if (!_bgLaneOn) { _bgLaneOn = true; _bgLaneDir = side; _bgLaneUntil = now + 45; }
			SetGoal(dest); _platActive = false; WhyB = Why.BLane; BgLaneStepsN++; Bodyguard.LaneSteps++;
		}
	}
}
