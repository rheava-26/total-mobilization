using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// SPREAD FROM THE TARGET. A segmented boss (a worm: any chain the threat field models) homes on ONE body and its trailing body sweeps the whole space around that body, so everyone standing
	/// near the target eats the trail. In such a fight a levy that is not the worm's target keeps a wide berth from the TARGETED levy (not just from the head) and goes on shooting from there; the
	/// targeted levy runs a long straight leg away from the rest of the army, using the worm's turn radius instead of trying to sidestep a body it cannot clear. Nothing is keyed to a boss name:
	/// the trigger is "a modelled chain whose head is locked on a levy".
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>0 = off, 1 = spread from the worm's target (harness switch).</summary>
		internal static int SpreadMode = Environment.GetEnvironmentVariable("LEVY_SPREAD") == "0" ? 0 : 1;
		/// <summary>Tiles a bystander keeps from the targeted levy, and the extra it must regain before it stops spreading.</summary>
		private const float SpreadStandoffTiles = 26f, SpreadHysteresisTiles = 4f, KiteLegTiles = 16f;
		private int _kiteDir, _kiteUntil; private bool _spreading;
		internal int SpreadSteps, KiteLegs;

		/// <summary>True when this think's horizontal goal was set by the spread / kite rule.</summary>
		private bool TrySpreadFromWormTarget(int now)
		{
			if (SpreadMode == 0 || !IsAlive) return false;
			var heads = ThreatField.Snapshot() == null ? null : ThreatField.ChainHeads;
			if (heads == null || heads.Count == 0) { _spreading = false; return false; }
			NPC npc = NPC;
			// the nearest chain head that is locked on a levy
			NPC head = null; SoldierNPC tgt = null; float best = float.MaxValue;
			for (int i = 0; i < heads.Count; i++)
			{
				NPC h = Main.npc[heads[i]];
				if (!h.active) continue;
				SoldierNPC t = EnemyTargeting.SoldierTargetOf(h);
				if (t == null) continue;
				float d = Vector2.DistanceSquared(h.Center, npc.Center);
				if (d < best) { best = d; head = h; tgt = t; }
			}
			if (head == null) { _spreading = false; return false; }
			if (Vector2.Distance(head.Center, npc.Center) > 90f * LevyConstants.TilePx && !_spreading) return false;
			// a bystander first tries a different row from the hunted levy (up or down out of the trail's lane), before it spreads along the floor
			if (RowsMode >= 1 && tgt != this && !PositionsAsMelee && !(WantsAltitude || _aerial) && TickPlatformPerch(now)) { WhyB = Why.BRow; return true; }
			float tile = LevyConstants.TilePx;
			int dir; float legTiles;
			if (tgt == this)
			{
				// the target: a long leg away from the rest of the army (or from the head when it is alone), held for a while
				if (now >= _kiteUntil || _kiteDir == 0)
				{
					float cx = 0f; int n = 0;
					for (int i = 0; i < EnemyTargeting.Bodies.Count; i++) { var b = EnemyTargeting.Bodies[i]; if (b.IsAlive && b.NPC.whoAmI != npc.whoAmI) { cx += b.NPC.Center.X; n++; } }
					float from = n > 0 ? cx / n : head.Center.X;
					_kiteDir = npc.Center.X >= from ? 1 : -1; _kiteUntil = now + 120; KiteLegs++;
				}
				dir = _kiteDir; legTiles = KiteLegTiles;
			}
			else
			{
				float dT = Math.Abs(tgt.NPC.Center.X - npc.Center.X) / tile + Math.Abs(tgt.NPC.Center.Y - npc.Center.Y) / tile * 0.5f;
				float need = SpreadStandoffTiles + (_spreading ? SpreadHysteresisTiles : 0f);
				if (dT >= need) { _spreading = false; return false; }
				dir = npc.Center.X >= tgt.NPC.Center.X ? 1 : -1; legTiles = Math.Min(10f, need - dT + 2f);
			}
			_spreading = true;
			if (!TryFlatStep(dir, (int)legTiles, out Vector2 dest) && !TryFlatStep(-dir, (int)legTiles, out dest)) return false;
			if (Math.Sign(dest.X - npc.Center.X) != dir && tgt == this) _kiteDir = -dir;
			ApplyChaseClamp(ref dest);
			SetGoal(dest); SpreadSteps++;
			return true;
		}

		/// <summary>Farthest standable tile within <paramref name="maxTiles"/> in a direction (no firing-line rule: getting clear of the lane outranks the shot).</summary>
		private bool TryFlatStep(int dir, int maxTiles, out Vector2 dest)
		{
			Point feet = TileNav.FeetTile(NPC.Bottom);
			bool aloft = WantsAltitude || _aerial || NPC.velocity.Y != 0f;
			if (aloft) { dest = new Vector2(NPC.Center.X + dir * maxTiles * LevyConstants.TilePx, NPC.Center.Y); return AirLaneSafe(dir, Math.Max(3, maxTiles)); }
			for (int k = maxTiles; k >= 1; k--)
			{
				int x = feet.X + dir * k;
				for (int row = 0; row < 3; row++)
				{
					int y = feet.Y + (row == 0 ? 0 : row == 1 ? -1 : 1);
					if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) continue;
					dest = TileNav.BottomCentre(x, y);
					return true;
				}
			}
			dest = default;
			return false;
		}
	}
}
