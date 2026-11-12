using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Banners
{
	/// <summary>Where one levy on the Hold the Banners order stands: the banner (its tile entity's top-left) and the standing tile (feet tile) inside the banner's stands.</summary>
	public sealed class BannerPost
	{
		public Point16 Banner; public Point Stand; public int Slot;
		public Vector2 StandBottom => TileNav.BottomCentre(Stand.X, Stand.Y);
	}

	/// <summary>
	/// HOLDING BANNERS. The registry of placed Holding Banners, the rule that gives each levy on the <see cref="SoldierOrder.HoldBanners"/> order its own standing spot at the nearest banner, and the aura that
	/// keeps the <see cref="HoldingBannerBuff"/> on every soldier standing near one.
	/// <list type="bullet">
	/// <item>A banner's STANDS are the ground tiles within <see cref="LevyConstants.Banner.StandReachTiles"/> columns of its centre that lie at the banner's own level (a slope's one row either way): the nearest
	/// <see cref="LevyConstants.Banner.MaxStands"/> of them, so a banner on a platform or a tower top gets only the stands that are really there.</item>
	/// <item>Levies are assigned in id order to the NEAREST banner that still has a free stand (a banner holds as many levies as it has stands; when every banner is full the nearest takes the extra ones and they
	/// share stands). Placing or breaking a banner reassigns the whole army; an assignment otherwise stays until its banner goes, the levy gives up reaching it, or the order changes.</item>
	/// <item>A levy that followed its route to the end and could not stand at its banner sets that banner aside for <see cref="LevyConstants.Banner.UnreachableBanTicks"/> and goes to the next nearest.</item>
	/// <item>The aura gives the buff to ANY soldier (Regular or militia, whatever its order) within <see cref="LevyConstants.Banner.AuraTiles"/> tiles of a banner and no more than <see cref="LevyConstants.Banner.AuraRows"/> rows above its foot.</item>
	/// </list>
	/// Server / single player only; clients draw the tile and see the synced buff.
	/// </summary>
	public class BannerSystem : ModSystem
	{
		private sealed class BannerInfo
		{
			public Point16 Key; public Vector2 Foot; public readonly List<Point> Stands = new(); public bool[] Used; public int Extra;
			public int FreeSlot() { for (int i = 0; i < Stands.Count; i++) if (!Used[i]) return i; return -1; }
		}

		private static readonly List<BannerInfo> _banners = new();
		private static readonly Dictionary<int, BannerPost> _post = new();
		private static readonly Dictionary<long, int> _bans = new();
		private static int _version, _built = -1, _refreshAt;
		private static bool _full = true;
		private int _pulse;
		/// <summary>Harness / self-check telemetry: rebuilds done.</summary>
		public static int Rebuilds;

		/// <summary>A banner was placed or broken (or a stand's ground changed): the next lookup rebuilds the banner list and reassigns the whole army.</summary>
		public static void Changed() { _version++; _full = true; }

		public override void ClearWorld() { _banners.Clear(); _post.Clear(); _bans.Clear(); _built = -1; _version = 0; _full = true; }
		public override void OnWorldLoad() => ClearWorld();

		// ------------------------------------------------------------------------------------------------ registry
		/// <summary>Every standing Holding Banner (tile entity, top-left position).</summary>
		public static List<Point16> BannerPositions()
		{
			var list = new List<Point16>();
			foreach (var kv in TileEntity.ByID)
				if (kv.Value is HoldingBannerTE te && te.IsTileValidForEntity(te.Position.X, te.Position.Y)) list.Add(te.Position);
			return list;
		}

		public static bool AnyBanner() => BannerPositions().Count > 0;

		/// <summary>The standing tiles (feet tiles) of a banner whose tile entity is at <paramref name="top"/>: columns by distance from the banner's centre, at its own level.</summary>
		public static List<Point> StandsOf(Point16 top)
		{
			var res = new List<Point>();
			int footRow = top.Y + LevyConstants.Banner.Height - 1;
			int w = LevyConstants.Banner.Width;
			float centre = top.X + w * 0.5f;
			var cols = new List<int>();
			for (int c = top.X - (LevyConstants.Banner.StandReachTiles - 1); c <= top.X + w - 1 + (LevyConstants.Banner.StandReachTiles - 1); c++) cols.Add(c);
			cols.Sort((a, b) =>
			{
				float da = Math.Abs(a + 0.5f - centre), db = Math.Abs(b + 0.5f - centre);
				int cmp = da.CompareTo(db);
				return cmp != 0 ? cmp : b.CompareTo(a); // the right side first on a tie
			});
			foreach (int c in cols)
			{
				if (res.Count >= LevyConstants.Banner.MaxStands) break;
				int pick = int.MinValue;
				for (int k = 0; k <= LevyConstants.Banner.StandRowSlack * 2 && pick == int.MinValue; k++)
				{
					int d = (k + 1) / 2 * (k % 2 == 1 ? -1 : 1); // 0, -1, +1: the banner's own row first, then a row up, then a row down
					int r = footRow + d;
					if (TileNav.InBounds(c, r) && TileNav.IsStandable(c, r) && !TileNav.HasLiquid(c, r) && !TileNav.HasLiquid(c, r - 1)) pick = r;
				}
				if (pick != int.MinValue) res.Add(new Point(c, pick));
			}
			if (res.Count == 0 && TileNav.NearestStandable(top.X, footRow, 6, out Point np)) res.Add(np);
			return res;
		}

		/// <summary>Test seam (self-check): replaces the tile-entity scan with these banners and stands.</summary>
		internal static Func<List<(Point16 Top, List<Point> Stands)>> TestSource;

		private static List<(Point16 Top, List<Point> Stands)> Scan()
		{
			var list = new List<(Point16, List<Point>)>();
			foreach (Point16 top in BannerPositions()) list.Add((top, StandsOf(top)));
			return list;
		}

		/// <summary>Self-check / lab: forget every assignment and ban.</summary>
		internal static void ResetForTest() { _post.Clear(); _bans.Clear(); _banners.Clear(); _built = -1; _full = true; }

		// ------------------------------------------------------------------------------------------------ assignment
		/// <summary>The post of a levy on the banner order, or null when there is no banner it can use (it then holds where it stands). Rebuilds the registry and the assignments when they are stale. Server / single player.</summary>
		public static BannerPost PostOf(SoldierRecord rec)
		{
			if (rec == null || rec.Order != SoldierOrder.HoldBanners) return null;
			int now = (int)Main.GameUpdateCount;
			if (_built != _version || now >= _refreshAt || !_post.ContainsKey(rec.Id) && !_bannedAll(rec)) Rebuild(now);
			return _post.TryGetValue(rec.Id, out var p) ? p : null;
		}

		private static bool _bannedAll(SoldierRecord rec)
		{
			// a levy with no post and no banner left that it may use has nothing to wait for; do not rebuild every think for it
			if (_banners.Count == 0) return true;
			foreach (var b in _banners) if (!Banned(rec.Id, b.Key)) return false;
			return true;
		}

		private static long BanKey(int recId, Point16 b) => ((long)recId << 32) | (uint)(b.X * 4099 + b.Y);
		private static bool Banned(int recId, Point16 b) => _bans.TryGetValue(BanKey(recId, b), out int until) && (int)Main.GameUpdateCount < until;

		/// <summary>The levy could not get to its banner: set that banner aside for it and let the next nearest take it.</summary>
		public static void NoteUnreachable(SoldierRecord rec, Point16 banner)
		{
			if (rec == null) return;
			_bans[BanKey(rec.Id, banner)] = (int)Main.GameUpdateCount + LevyConstants.Banner.UnreachableBanTicks;
			_post.Remove(rec.Id);
			_built = -1; // the next lookup rebuilds (the others keep their posts)
		}

		private static Vector2 Where(SoldierRecord rec)
		{
			if (rec.HasBody && rec.BodyIndex >= 0 && rec.BodyIndex < Main.maxNPCs && Main.npc[rec.BodyIndex].active) return Main.npc[rec.BodyIndex].Bottom;
			return rec.HoldPos;
		}

		private static void Rebuild(int now)
		{
			Rebuilds++;
			bool full = _full;
			_banners.Clear();
			foreach (var src in TestSource != null ? TestSource() : Scan())
			{
				Point16 top = src.Top;
				var b = new BannerInfo { Key = top, Foot = new Vector2((top.X + LevyConstants.Banner.Width * 0.5f) * 16f, (top.Y + LevyConstants.Banner.Height) * 16f) };
				b.Stands.AddRange(src.Stands);
				if (b.Stands.Count > 0) { b.Used = new bool[b.Stands.Count]; _banners.Add(b); }
			}
			_built = _version; _full = false; _refreshAt = now + 600;
			if (full) _post.Clear();
			if (_banners.Count == 0) { _post.Clear(); return; }

			var army = new List<SoldierRecord>();
			var onOrder = new HashSet<int>();
			foreach (var r in RosterSystem.Instance.Regulars) if (r.Order == SoldierOrder.HoldBanners && !r.Reserve) { army.Add(r); onOrder.Add(r.Id); }
			army.Sort((a, b) => a.Id.CompareTo(b.Id));
			// a levy that left the order, or whose banner is gone or set aside, loses its post; the rest keep theirs (and their stand, re-read from the banner's stands) and count toward their banner's load
			foreach (int id in new List<int>(_post.Keys))
			{
				var p = _post[id];
				var bi = _banners.Find(b => b.Key == p.Banner);
				if (!onOrder.Contains(id) || bi == null || Banned(id, p.Banner)) { _post.Remove(id); continue; }
				if (p.Slot < bi.Stands.Count) bi.Used[p.Slot] = true; else bi.Extra++;
				p.Stand = bi.Stands[p.Slot % bi.Stands.Count];
			}

			foreach (var r in army)
			{
				if (_post.ContainsKey(r.Id)) continue;
				Vector2 at = Where(r);
				BannerInfo best = null, bestAny = null; float bd = float.MaxValue, bdAny = float.MaxValue;
				foreach (var b in _banners)
				{
					if (Banned(r.Id, b.Key)) continue;
					float d = Vector2.DistanceSquared(at, b.Foot);
					if (d < bdAny) { bdAny = d; bestAny = b; }
					if (b.FreeSlot() >= 0 && d < bd) { bd = d; best = b; }
				}
				var pick = best ?? bestAny;
				if (pick == null) continue; // every banner is set aside for this levy
				int slot = pick.FreeSlot();
				if (slot >= 0) pick.Used[slot] = true; else slot = pick.Stands.Count + pick.Extra++; // every banner is full: the nearest takes the extra levy, who shares a stand
				_post[r.Id] = new BannerPost { Banner = pick.Key, Stand = pick.Stands[slot % pick.Stands.Count], Slot = slot };
			}
		}

		// ------------------------------------------------------------------------------------------------ aura
		public override void PostUpdateNPCs()
		{
			if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
			if (++_pulse < LevyConstants.Banner.PulseTicks) return;
			_pulse = 0;
			Pulse();
		}

		/// <summary>One aura pulse (also called by the self-check and the lab): refreshes the buff on every soldier near a banner. Returns how many soldiers hold it.</summary>
		public static int Pulse()
		{
			var tops = BannerPositions();
			if (tops.Count == 0) return 0;
			int type = ModContent.BuffType<HoldingBannerBuff>(), n = 0;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || s.Hostile) continue;
				bool near = false;
				foreach (Point16 top in tops) if (InAura(top, s.NPC.Center, s.NPC.Bottom.Y)) { near = true; break; }
				if (!near) continue;
				bool fresh = !s.NPC.HasBuff(type);
				s.NPC.AddBuff(type, LevyConstants.Banner.BuffTicks);
				if (fresh) s.StatsDirty = true;
				n++;
			}
			return n;
		}

		/// <summary>Whether a body at <paramref name="centre"/> (feet at <paramref name="bottomY"/>) is inside the aura of the banner at <paramref name="top"/>.</summary>
		public static bool InAura(Point16 top, Vector2 centre, float bottomY)
		{
			float cx = (top.X + LevyConstants.Banner.Width * 0.5f) * 16f, footY = (top.Y + LevyConstants.Banner.Height) * 16f;
			return Math.Abs(centre.X - cx) <= LevyConstants.Banner.AuraTiles * 16f && bottomY <= footY + 2f * 16f && bottomY >= footY - LevyConstants.Banner.AuraRows * 16f;
		}
	}
}
