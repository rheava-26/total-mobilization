using System;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;
using static Levy.Common.LevyConstants.Movement;

namespace Levy.Common.Movement
{
	/// <summary>
	/// Followers come along through pylons, mirrors, recall, teleporters and respawns; off-screen catch-up teleport.
	/// Spots are never in liquid and always have line of sight to the point they were asked for, so nobody arrives
	/// inside a wall or on the far side of one. Militia never travel. OWNED BY THE MOVEMENT AGENT.
	/// </summary>
	public static class FollowerTravel
	{
		/// <summary>Finds a free standing spot near <paramref name="near"/> (no lava/water/solid), within radius tiles. Returns the bottom-centre; false if none.</summary>
		public static bool FindStandingSpot(Vector2 near, int radiusTiles, out Vector2 spot)
		{
			spot = near;
			int cx = (int)(near.X / 16f), cy = (int)(near.Y / 16f);
			if (!TileNav.InBounds(cx, cy)) return false;
			for (int r = 0; r <= radiusTiles; r++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					for (int dx = -r; dx <= r; dx++)
					{
						if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
						int x = cx + dx, y = cy + dy;
						if (!IsSpot(x, y)) continue;
						Vector2 bottom = TileNav.BottomCentre(x, y);
						Vector2 centre = bottom - new Vector2(0f, SoldierNPC.BodyHeight / 2f);
						if (!Collision.CanHitLine(centre, 1, 1, near, 1, 1)) continue;
						spot = bottom;
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// A place a teleported body can stand, near <paramref name="near"/> and never in the air: the ordinary standing-spot search first, then a wider ground search (the nearest standable tile in the
		/// columns either side, scanning down and up), so a commander hanging from a ceiling, flying over a chasm or sitting in a sky house gets their followers on the ground close by. False when
		/// there is no ground at all within reach; the caller then leaves the body where it is.
		/// </summary>
		public static bool FindSafeGround(Vector2 near, int radiusTiles, out Vector2 spot)
		{
			if (FindStandingSpot(near, radiusTiles, out spot)) return true;
			int cx = (int)(near.X / 16f), cy = (int)(near.Y / 16f);
			// pass 0: in sight of the point (so never the far side of a ceiling slab); pass 1 (nothing in sight): the nearest ground below it
			for (int pass = 0; pass < 2; pass++)
			{
				float best = float.MaxValue; Point bp = default; bool found = false;
				for (int dx = 0; dx <= SafeGroundColumns; dx++)
				{
					for (int sgn = -1; sgn <= 1; sgn += 2)
					{
						if (dx == 0 && sgn == 1) continue;
						int x = cx + sgn * dx;
						if (!TileNav.InBounds(x, cy) || dx * dx >= best) continue;
						for (int k = 0; k <= SafeGroundRowsDown + SafeGroundRowsUp; k++)
						{
							int dy = k <= SafeGroundRowsDown ? k : -(k - SafeGroundRowsDown);
							if (pass == 1 && dy < 0) break;
							int y = cy + dy;
							if (!TileNav.InBounds(x, y)) continue;
							float d = dx * dx + dy * dy;
							if (d >= best || !IsSpot(x, y)) continue;
							if (pass == 0 && !Collision.CanHitLine(TileNav.BottomCentre(x, y) - new Vector2(0f, SoldierNPC.BodyHeight / 2f), 1, 1, near, 1, 1)) continue;
							best = d; bp = new Point(x, y); found = true;
						}
					}
				}
				if (found) { spot = TileNav.BottomCentre(bp.X, bp.Y); return true; }
			}
			return false;
		}

		/// <summary>The standing spot under a point (first ground below it, then the nearest in the neighbouring columns): where followers wait for a commander who is not on the ground.</summary>
		public static bool FindGroundBelow(Vector2 from, out Vector2 spot)
		{
			spot = from;
			int cx = (int)(from.X / 16f), cy = (int)(from.Y / 16f);
			for (int dx = 0; dx <= 8; dx++)
				for (int sgn = -1; sgn <= 1; sgn += 2)
				{
					if (dx == 0 && sgn == 1) continue;
					int x = cx + sgn * dx;
					for (int y = cy; y <= cy + SafeGroundRowsDown; y++)
					{
						if (!TileNav.InBounds(x, y)) break;
						if (!IsSpot(x, y)) continue;
						spot = TileNav.BottomCentre(x, y); return true;
					}
				}
			return false;
		}

		private static bool IsSpot(int x, int y)
		{
			if (!TileNav.InBounds(x, y) || !TileNav.IsStandable(x, y)) return false;
			for (int i = 0; i < SpotColumnClearTiles; i++)
				if (TileNav.IsSolid(x, y - i) || TileNav.HasLiquid(x, y - i)) return false;
			return true;
		}

		/// <summary>Moves all following regulars of the commander next to them (server/single player).</summary>
		public static void BringFollowers(Player commander)
		{
			if (commander == null || !commander.active || !LevyUtil.IsServerOrSingle) return;
			var roster = RosterSystem.Instance;
			if (roster == null) return;
			var regulars = roster.Regulars;
			for (int i = 0; i < regulars.Count; i++)
			{
				var rec = regulars[i];
				if (rec == null || rec.Kind != SoldierKind.Regular || rec.Order != SoldierOrder.Follow || rec.StoodDown) continue;
				if (!string.Equals(rec.CommanderName, commander.name, StringComparison.Ordinal)) continue;
				if (!rec.HasBody) continue;
				if (Main.npc[rec.BodyIndex].ModNPC is not SoldierNPC body || !body.IsAlive) continue;
				if (body.State == SoldierState.Routed || body.State == SoldierState.ReturnHome || body.State == SoldierState.Desert || body.State == SoldierState.Dead) continue;
				if (!FindSafeGround(commander.Center, StandingSpotRadiusTiles, out var spot)) continue;
				Place(body, spot);
			}
			// his officers on a Follow order come too (one that was told to hold, patrol or stand down stays where its order has it)
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is not SoldierNPC avatar || !avatar.OfficerMode || !avatar.IsAlive || avatar.Record == null) continue;
				if (avatar.Record.Order != SoldierOrder.Follow || !string.Equals(avatar.Record.CommanderName, commander.name, StringComparison.Ordinal)) continue;
				if (!FindSafeGround(commander.Center, StandingSpotRadiusTiles, out var spot)) continue;
				Place(avatar, spot);
			}
		}

		/// <summary>Catch-up teleport for one body: arrives at the screen edge on its own side of the commander, or next to them. Returns false without a commander.</summary>
		public static bool TeleportNearCommander(SoldierNPC body)
		{
			if (body == null || body.NPC == null || !LevyUtil.IsServerOrSingle) return false;
			Player commander = body.Commander;
			if (commander == null || commander.dead) return false;
			float offset = body.NPC.Center.X - commander.Center.X;
			int side = Math.Abs(offset) < 1f ? -Math.Sign(commander.direction == 0 ? 1 : commander.direction) : Math.Sign(offset);
			float sw = (LevyUtil.IsServer || Main.screenWidth <= 0) ? LevyConstants.ServerAssumedScreenW : Main.screenWidth; // Main.screenWidth stays 1152 on a dedicated server
			Vector2 edge = commander.Center + new Vector2(side * (sw / 2f + ScreenEdgeExtraPx), 0f);
			if (!FindStandingSpot(edge, StandingSpotRadiusTiles, out var spot) && !FindSafeGround(commander.Center, StandingSpotRadiusTiles, out spot)) return false;
			Place(body, spot);
			return true;
		}

		/// <summary>Snaps a body that cannot reach its barracks onto the muster point (or the nearest free standing spot). Server/single player only.</summary>
		public static bool TeleportToPoint(SoldierNPC body, Vector2 musterBottom)
		{
			if (body == null || body.NPC == null || !LevyUtil.IsServerOrSingle) return false;
			if (!FindStandingSpot(musterBottom, StandingSpotRadiusTiles, out var spot)) spot = musterBottom;
			Place(body, spot);
			return true;
		}

		private static void Place(SoldierNPC body, Vector2 bottom)
		{
			NPC npc = body.NPC;
			bottom = RosterSystem.SafeSpawnPosition(bottom); // never arrive inside a boss's reach
			npc.position = new Vector2(bottom.X - npc.width / 2f, bottom.Y - npc.height);
			npc.oldPosition = npc.position;
			npc.velocity = Vector2.Zero;
			body.NotifyTeleported();
			npc.netUpdate = true;
		}
	}
}
