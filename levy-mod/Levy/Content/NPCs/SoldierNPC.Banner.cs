using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Levy.Common;
using Levy.Common.Banners;
using Levy.Common.Movement;
using Levy.Common.Observe;
using Levy.Common.Util;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE HOLD THE BANNERS ORDER, soldier side. A levy on <see cref="SoldierOrder.HoldBanners"/> walks to the stand <see cref="BannerSystem"/> gave it at the nearest Holding Banner (the same planned route a map order
	/// follows, so hooks, wings, climbs and platforms all count), stops there and stays. Its post is also its Hold post: the order keeps <c>HoldPosition</c> on and <c>Record.HoldPos</c> at the stand, so a levy
	/// fights from its stand (ranged fire, melee inside the Hold leash) and walks back to it when the fight is over. With no banner it can use, the order is plain Hold where it stands.
	/// </summary>
	public partial class SoldierNPC
	{
		private bool _bnHasPost; private Point16 _bnBanner; private Point _bnStand;
		private Vector2 _bnProgPos; private int _bnProgTick, _bnNoProgress;
		private const int BannerProgressTicks = 180, BannerGiveUpWindows = 20, BannerGiveUpWindowsAfterRoute = 8;

		/// <summary>On the banner order (a Regular).</summary>
		internal bool OnBannerOrder => IsRegular && Record != null && Record.Order == SoldierOrder.HoldBanners;

		/// <summary>On the banner order, with a stand, and still far from it: the march outranks a fight (it keeps walking and shoots on the way); within <see cref="LevyConstants.Banner.MarchFarTiles"/> it fights from where it is.</summary>
		internal bool BannerMarching => OnBannerOrder && _bnHasPost && Vector2.Distance(NPC.Bottom, TileNav.BottomCentre(_bnStand.X, _bnStand.Y)) > LevyConstants.Banner.MarchFarTiles * LevyConstants.TilePx;

		/// <summary>The stand this levy holds (feet tile), when it has one. For the lab and the self-check.</summary>
		internal bool BannerStand(out Point stand) { stand = _bnStand; return _bnHasPost; }

		/// <summary>Brain hook (top of ThinkOrder): true when this think was spent on the banner order (walking to the stand, or standing at it).</summary>
		internal bool ThinkBannerOrder(int now)
		{
			if (!OnBannerOrder) return false;
			HoldPosition = true; // the leash of a Hold post, centred on the stand (set below)
			BannerPost post = LevyUtil.IsClient ? null : BannerSystem.PostOf(Record);
			if (post == null)
			{
				if (_bnHasPost) { _bnHasPost = false; Record.HoldPos = NPC.Center; ClearGoal(); RouteReset(); } // the banner went, or none is reachable: hold where it stands
				return false; // the idle code holds the spot
			}
			if (!_bnHasPost || _bnBanner != post.Banner || _bnStand != post.Stand)
			{
				_bnHasPost = true; _bnBanner = post.Banner; _bnStand = post.Stand;
				_bnProgPos = NPC.Center; _bnProgTick = now; _bnNoProgress = 0; RouteReset(); ClearGoal();
			}
			Vector2 goal = post.StandBottom;
			Record.HoldPos = goal;
			float dx = goal.X - NPC.Center.X, dy = goal.Y - NPC.Bottom.Y;
			bool sameLevel = Math.Abs(dy) <= 2f * LevyConstants.TilePx;
			// at the stand: stop and stay (a levy that stopped starts walking again only when it has been shoved well off the spot)
			if (sameLevel && !ClimbActive && (Math.Abs(dx) <= LevyConstants.Banner.StandArriveTiles * LevyConstants.TilePx || (!HasMoveGoal && Math.Abs(dx) <= LevyConstants.Banner.StandResumeTiles * LevyConstants.TilePx)))
			{
				ClearGoal(); SetState(SoldierState.Idle); _bnProgPos = NPC.Center; _bnProgTick = now; _bnNoProgress = 0;
				return true;
			}
			SetState(SoldierState.Follow); // runs rather than strolls; combat still takes over through TryEngage before this is reached
			if (CatchUpTick(now, goal)) { _bnProgPos = NPC.Center; _bnProgTick = now; _bnNoProgress = 0; return true; }
			if (now - _bnProgTick >= BannerProgressTicks)
			{
				_bnNoProgress = Vector2.DistanceSquared(NPC.Center, _bnProgPos) < 24f * 24f ? _bnNoProgress + 1 : 0;
				_bnProgPos = NPC.Center; _bnProgTick = now;
				if (_bnNoProgress >= (RouteExhausted ? BannerGiveUpWindowsAfterRoute : BannerGiveUpWindows))
				{
					// no way to this banner: set it aside for this levy; the next nearest is tried, and with none left it holds where it stands
					if (RouteExhausted) MapOrder.NoteUnreachable(Record, NPC.Center);
					BannerSystem.NoteUnreachable(Record, post.Banner);
					_bnHasPost = false; _bnNoProgress = 0; ClearGoal(); RouteReset();
					return true;
				}
			}
			if (RouteLeg(now, goal, out Vector2 leg)) { SetGoal(leg); return true; }
			SetGoal(MapOrder.LegGoal(NPC.Center, NPC.Bottom, goal));
			return true;
		}
	}
}
