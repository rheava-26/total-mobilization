using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Logistical Centers: each one adds <see cref="LevyConstants.LogisticsSlotsPerCenter"/> storage slots to the NEAREST Armory within
	/// <see cref="LevyConstants.LogisticsLinkRadiusTiles"/>. A center carries no data of its own (it is a bare tile entity), so the link is derived, never saved: the count per armory is
	/// recomputed from where the entities stand. The server (and single player) reconciles every armory's backing array to that count on a timer; the resized entity then syncs
	/// to clients through the normal tile-entity sharing, so placing, mining or moving a center, placing or mining an armory, and loading a world all converge on the same state.
	/// Clients only recount for display.
	/// </summary>
	public class LogisticsSystem : ModSystem
	{
		private static readonly Dictionary<int, int> _centers = new();
		private static uint _stamp = uint.MaxValue;

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		public override void ClearWorld() => Reset();
		private static void Reset() { _centers.Clear(); _stamp = uint.MaxValue; }

		/// <summary>Forces the next <see cref="CentersFor"/> to recount (a center or armory was just placed or removed).</summary>
		public static void Invalidate() => _stamp = uint.MaxValue;

		/// <summary>Logistical Centers linked to <paramref name="armory"/>.</summary>
		public static int CentersFor(ArmoryTE armory)
		{
			if (armory == null) return 0;
			Recount(false);
			return _centers.TryGetValue(armory.ID, out int n) ? n : 0;
		}

		/// <summary>The armory a center at <paramref name="center"/> serves: the nearest within reach, or null.</summary>
		public static ArmoryTE ArmoryFor(Vector2 center)
		{
			float limit = LevyConstants.LogisticsLinkRadiusTiles * LevyConstants.TilePx; limit *= limit;
			ArmoryTE best = null; float bestD = float.MaxValue;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				float d = Vector2.DistanceSquared(a.Center, center);
				if (d <= limit && d < bestD) { bestD = d; best = a; }
			}
			return best;
		}

		private static void Recount(bool force)
		{
			uint now = Main.GameUpdateCount;
			if (!force && _stamp != uint.MaxValue && now - _stamp < (uint)LevyConstants.LogisticsReconcileTicks) return;
			_stamp = now;
			_centers.Clear();
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not LogisticalCenterTE c) continue;
				ArmoryTE a = ArmoryFor(c.Center);
				if (a != null) _centers[a.ID] = (_centers.TryGetValue(a.ID, out int n) ? n : 0) + 1;
			}
		}

		public override void PostUpdateWorld()
		{
			if (!LevyUtil.IsServerOrSingle || Main.GameUpdateCount % (uint)LevyConstants.LogisticsReconcileTicks != 0) return;
			Recount(true);
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				if (a.FitStorage(_centers.TryGetValue(a.ID, out int n) ? n : 0)) a.Sync();
			}
		}
	}
}
