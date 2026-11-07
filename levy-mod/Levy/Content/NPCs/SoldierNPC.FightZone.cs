using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common;
using Levy.Common.Boss;
using Levy.Common.Soldiers;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// FIGHT ZONE. While the commander is in a boss fight (a main boss alive within FightZoneBossTiles of them) a levy with no explicit order (Follow, no Hold) stays inside FightZoneTiles of the commander: a goal outside the
	/// zone (a flee, a standoff retreat, a chase of an add, a row climb) is pulled onto its edge, and a body that has drifted out is walked back in. The dodge, the row spread and the worm kite still run inside it. Without this a boss that
	/// chases a retreating squad drags the whole fight away from the player. Harness switch LEVY_ZONE=0 turns it off.
	/// </summary>
	public partial class SoldierNPC
	{
		internal static int ZoneMode = Environment.GetEnvironmentVariable("LEVY_ZONE") == "0" ? 0 : 1;
		/// <summary>Harness telemetry: goals pulled in, walk-backs started.</summary>
		internal static int ZoneClampsS, ZoneReturnsS;
		private int _zoneCheckTick = -100000; private bool _zoneActive; private Vector2 _zoneCentre;

		/// <summary>True (and the zone centre) while this body is bound by the fight zone. Refreshed twice a second.</summary>
		private bool FightZone(int now, out Vector2 centre)
		{
			if (now - _zoneCheckTick >= 30)
			{
				_zoneCheckTick = now; _zoneActive = false;
				if (ZoneMode >= 1 && IsRegular && Record != null && Record.Order == SoldierOrder.Follow && !HoldPosition && State != SoldierState.Desert && State != SoldierState.Dead)
				{
					Player cmd = Commander;
					if (cmd != null && !cmd.dead)
					{
						float r2 = FightZoneBossTiles * LevyConstants.TilePx; r2 *= r2;
						for (int i = 0; i < Main.maxNPCs; i++)
						{
							NPC n = Main.npc[i];
							if (!n.active || n.life <= 0 || n.friendly || (n.realLife >= 0 && n.realLife != i)) continue;
							if (Vector2.DistanceSquared(n.Center, cmd.Center) > r2 || !BossRule.IsMainBoss(n)) continue;
							_zoneActive = true; _zoneCentre = cmd.Center; break;
						}
					}
				}
			}
			centre = _zoneCentre; return _zoneActive;
		}

		/// <summary>True when a point is inside the zone (or no zone binds this body).</summary>
		private bool InFightZone(int now, Vector2 p) => !FightZone(now, out Vector2 c) || Vector2.DistanceSquared(p, c) <= FightZoneTiles * FightZoneTiles * LevyConstants.TilePx * LevyConstants.TilePx;

		/// <summary>End of a think: pull the goal into the zone, or send a body that has left it back to the edge.</summary>
		private void ApplyFightZone(int now) { long pt = global::Levy.Common.Util.LevyProf.Begin(); try { ApplyFightZoneBody(now); } finally { global::Levy.Common.Util.LevyProf.End(global::Levy.Common.Util.LevyProf.Zone, pt); } }
		private void ApplyFightZoneBody(int now)
		{
			if (!FightZone(now, out Vector2 c)) return;
			float R = FightZoneTiles * LevyConstants.TilePx;
			Vector2 here = NPC.Center;
			if (HasMoveGoal)
			{
				Vector2 d = MoveGoal - c; float len = d.Length();
				if (len > R && len > 0.001f)
				{
					float gx = c.X + Math.Clamp(MoveGoal.X - c.X, -R * FightZoneEdgeFrac, R * FightZoneEdgeFrac);
					SetGoal(new Vector2(gx, MoveGoal.Y)); ZoneClampsS++; _platActive = false;
				}
				return;
			}
			if (Vector2.DistanceSquared(here, c) > R * R)
			{
				float gx = c.X + Math.Clamp(here.X - c.X, -R * FightZoneEdgeFrac, R * FightZoneEdgeFrac);
				SetGoal(new Vector2(gx, c.Y)); ZoneReturnsS++;
			}
		}
	}
}
