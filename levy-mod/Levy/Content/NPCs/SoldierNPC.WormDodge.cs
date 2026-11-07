using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Movement;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// WORM DODGE. A worm head homes on its target at about twice a levy's running speed, and a path predicted more than ~15 ticks ahead is wrong by a body width. So against a head the levy does what a player does:
	/// it holds still and lets the head commit to the line (and to its turn), then makes ONE late, decisive move across the head's path, at the last moment that still clears (WormCanWait), never along the head's
	/// heading and never in the head's horizontal direction of travel (WormAlongPenalty: against the turn, the body ends behind the head and the head needs a long turn to come back).
	/// Generic: the trigger is "a chain head the threat field tracks is in the near list", never a name. LEVY_WORMDODGE=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static bool WormDodgeOn = Environment.GetEnvironmentVariable("LEVY_WORMDODGE") != "0";
		private bool _wormScan; private float _wormMargin;
		internal int WormHolds, WormCommits;
		private static readonly System.Collections.Generic.List<Threat> _thrAged = new(8);

		/// <summary>Stand still (decelerating) until the next look: the head has not reached the point where a move is still just enough.</summary>
		private bool WormHoldStill()
		{
			WormHolds++; WhyM = Why.MDodgeHold;
			_dodgeHold = LevyConstants.WormDodge.HoldTicks; _dodgeCooldown = 0;
			NPC.velocity.X *= StopDecel;
			if (Math.Abs(NPC.velocity.X) < StopBelowPx) NPC.velocity.X = 0f;
			return true;
		}

		/// <summary>Would this manoeuvre still clear the head if it started SlackTicks later (the body holds still meanwhile)? Then wait.</summary>
		private bool WormCanWait(in Maneuver best, Vector2 c0, float g, int horizon, bool airScan)
		{
			int slack = LevyConstants.WormDodge.SlackTicks;
			ThreatField.Aged(_thrNear, slack, _thrAged);
			int imm = DanceMode >= 1 && DOn(4) && DashIFrameTicks > 0 && (best.Kind == 4 || best.Kind == 8 || best.Kind == 10) ? DashIFrameTicks : 0;
			int h = ThreatField.Hits(_thrAged, c0, NPC.width, NPC.height, best, g, Math.Max(8, horizon - slack), out _, out _, _wormMargin, imm);
			if (h == 0) return true;
			WormCommits++;
			return false;
		}

		/// <summary>Soft preference (a hit is 100): a candidate that moves along the head's heading, toward the head, or the way the head is travelling horizontally scores worse than one that goes across / against.</summary>
		private float WormAlongPenalty(in Maneuver m, Vector2 c0, float g)
		{
			Vector2 d = ThreatField.PathAt(c0, m, g, 12) - c0;
			float len = d.Length(); if (len < 20f) return 0f;
			Vector2 u = d / len;
			float pen = 0f; int best = -1; float bd = float.MaxValue;
			for (int i = 0; i < _thrNear.Count; i++)
			{
				if (!_thrNear[i].Head) continue;
				float dd = Vector2.DistanceSquared(_thrNear[i].P, c0);
				if (dd < bd) { bd = dd; best = i; }
			}
			if (best < 0) return 0f;
			Threat h = _thrNear[best];
			float hs = h.V.Length(); if (hs < 1f) return 0f;
			Vector2 hv = h.V / hs;
			pen += Math.Max(0f, Vector2.Dot(u, hv));                                                      // along its heading
			Vector2 toHead = h.P - c0; float tl = toHead.Length(); if (tl > 1f) pen += Math.Max(0f, Vector2.Dot(u, toHead / tl));  // toward the head
			float hx = Math.Abs(h.V.X) > 1f ? Math.Sign(h.V.X) : 0f;
			pen += Math.Max(0f, u.X * hx);                                                                // with its horizontal direction of travel
			return pen * LevyConstants.WormDodge.AlongPenalty;
		}
	}
}
