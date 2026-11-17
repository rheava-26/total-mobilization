using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Boss;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Movement
{
	/// <summary>
	/// BODYGUARD: who a boss is going for right now, once for the whole army. A boss that has a levy in its sights (the targeting lock <c>LevyNpcData.SoldierTarget</c>, the boss's own NPC target, or the boss rule's
	/// spite grudge: the recorder's target kinds 2 and 3) is a pair (boss, levy). The levy it targets draws it away from the commander and the rest of the army; the others keep off the line between the two.
	/// The positioning itself is <see cref="SoldierNPC"/>'s (SoldierNPC.Bodyguard.cs); this is the shared snapshot and the numbers.
	/// A segmented boss (a worm) is not read here: <c>TrySpreadFromWormTarget</c> already owns that fight. Rebuilt every <see cref="RebuildTicks"/> ticks, one pass over the NPC table.
	/// <c>LEVY_BODYGUARD=0</c> switches the whole rule off (A/B for the lab and the world check); the snapshot is still built, so the check can read who is targeted in the off arm too.
	/// </summary>
	public static class Bodyguard
	{
		/// <summary>0 off, 1 on. The world check flips it between its two arms.</summary>
		public static int Mode = Environment.GetEnvironmentVariable("LEVY_BODYGUARD") == "0" ? 0 : 1;

		public const int RebuildTicks = 4;
		/// <summary>The decoy's leg (tiles), how long a chosen direction is held (ticks), how long it keeps running after the boss looked away, the clump it flees (tiles) and the distance from the commander it stops at when the fight zone does not set one.</summary>
		public const int LegTiles = 12, HoldTicks = 90, GraceTicks = 45;
		public const float ClumpRadiusTiles = 22f, ClumpMinOthers = 2f, StopTilesNoZone = 60f;
		/// <summary>The lane between a boss and the levy it targets: half width (tiles), the extra a levy must clear before it stops sidestepping, the shortest lane worth leaving, and the flattest line a walk can leave.</summary>
		public const float LaneHalfTiles = 3.5f, LaneExtraTiles = 1.5f, LaneMinLenTiles = 8f, LaneMinNormalX = 0.35f;

		public struct Pair { public int Boss, Body; public bool Spite; }
		private static readonly List<Pair> Cur = new(8);
		private static readonly int[] BossOf = new int[Main.maxNPCs];
		private static uint _tick = uint.MaxValue;
		/// <summary>Telemetry: rebuilds, pairs seen, decoy steps and lane steps taken.</summary>
		public static int Builds, PairsSeen; public static long DecoySteps, LaneSteps, Holds;

		/// <summary>The (boss, levy) pairs of the last rebuild (empty almost always).</summary>
		public static List<Pair> Get()
		{
			uint now = Main.GameUpdateCount;
			if (_tick != uint.MaxValue && now - _tick < RebuildTicks && now >= _tick) return Cur;
			_tick = now;
			Rebuild();
			return Cur;
		}

		private static bool IsWormHead(int i) { var h = ThreatField.ChainHeads; for (int k = 0; k < h.Count; k++) if (h[k] == i) return true; return false; }

		private static void Rebuild()
		{
			Builds++;
			Cur.Clear();
			Array.Fill(BossOf, -1);
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.life <= 0 || n.ModNPC is SoldierNPC) continue;
				if (!(n.boss || (n.type > 0 && n.type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[n.type]))) continue;
				if (n.realLife >= 0 && n.realLife != i) continue;      // a part: its root is the boss
				if (IsWormHead(i)) continue;
				int st = LevyNpcData.Of(n).SoldierTarget; bool spite = false;
				if (!(st >= 0 && st < Main.maxNPCs && Main.npc[st].active && Main.npc[st].ModNPC is SoldierNPC))
				{
					st = -1;
					if (n.target >= 300 && n.target - 300 < Main.maxNPCs && Main.npc[n.target - 300].active && Main.npc[n.target - 300].ModNPC is SoldierNPC) st = n.target - 300;
					else { var sp = BossRule.SpiteTargetOf(n); if (sp != null && sp.NPC.active) { st = sp.NPC.whoAmI; spite = true; } }
				}
				if (st < 0 || BossOf[st] >= 0) continue;
				SoldierNPC body = (SoldierNPC)Main.npc[st].ModNPC;
				if (!body.IsAlive || body.Hostile) continue;
				Cur.Add(new Pair { Boss = i, Body = st, Spite = spite }); BossOf[st] = i; PairsSeen++;
			}
		}

		/// <summary>The boss NPC index that has this levy (NPC index) in its sights, or -1.</summary>
		public static int BossTargeting(int bodyIndex) { Get(); return bodyIndex >= 0 && bodyIndex < BossOf.Length ? BossOf[bodyIndex] : -1; }

		/// <summary>
		/// The nearest clump of other levies around <paramref name="me"/>: the centre of every other levy within <see cref="ClumpRadiusTiles"/> when at least <see cref="ClumpMinOthers"/> stand there, else the nearest single one within twice the radius.
		/// False when nobody else is near.
		/// </summary>
		public static bool ClumpCentre(SoldierNPC me, out Vector2 centre)
		{
			centre = default;
			var bodies = BossRule.LiveBodies;
			float r = ClumpRadiusTiles * LevyConstants.TilePx, r2 = r * r, far2 = 4f * r2;
			Vector2 sum = Vector2.Zero; int n = 0; float nearD = float.MaxValue; Vector2 nearP = default;
			Vector2 p0 = me.NPC.Center;
			for (int i = 0; i < bodies.Count; i++)
			{
				var b = bodies[i];
				if (b == me || b.NPC == null || !b.NPC.active) continue;
				float d = Vector2.DistanceSquared(b.NPC.Center, p0);
				if (d <= r2) { sum += b.NPC.Center; n++; }
				if (d < nearD) { nearD = d; nearP = b.NPC.Center; }
			}
			if (n >= ClumpMinOthers) { centre = sum / n; return true; }
			if (nearD <= far2) { centre = nearP; return true; }
			return false;
		}

		/// <summary>Distance (tiles) from a point to the segment a-b, and how far along it the closest point is (0 at a, 1 at b).</summary>
		public static float DistToSegmentTiles(Vector2 p, Vector2 a, Vector2 b, out float along, out Vector2 closest)
		{
			Vector2 ab = b - a; float len2 = ab.LengthSquared();
			along = len2 < 1f ? 0f : MathHelper.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
			closest = a + ab * along;
			return Vector2.Distance(p, closest) / LevyConstants.TilePx;
		}
	}
}
