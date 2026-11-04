using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Common;                  // LevyConstants.PylonShareRadius / TilePx
using Levy.Common.Systems;          // SettlementSystem.NearestArmory / NetworkedArmories
using Levy.Content.TileEntities;    // ArmoryTE, BarracksTE
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Common
{
	/// <summary>
	/// Which Levy Armories a spot (a turret, or the cursor while placing one) can draw surplus weapons from. Two ways in, pooled together:
	/// the nearest Armory within <see cref="T.ArmoryReachTiles"/>, and, when the spot stands at a vanilla pylon, the nearest Armory that is itself on the
	/// pylon network. Each seed is then widened through Levy's <see cref="SettlementSystem.NetworkedArmories"/>, so one turret reaches every Armory the
	/// base's pylon network pools. Read-only; safe on clients (tile entities and pylons are synced).
	/// </summary>
	public static class ArmoryLink
	{
		/// <summary>Fills <paramref name="pool"/> (cleared first) with every Armory reachable from <paramref name="pos"/>, seed armories first. Empty when none.</summary>
		public static void Find(Vector2 pos, List<ArmoryTE> pool)
		{
			pool.Clear();
			SettlementSystem settle = SettlementSystem.Instance;
			if (settle == null) return;

			ArmoryTE local = settle.NearestArmory(pos, T.ArmoryReachTiles);
			if (local != null) AddNetwork(settle, local, pool);

			var pylons = Main.PylonSystem?.Pylons;
			if (pylons != null && pylons.Count > 0 && NearPylon(pos, pylons))
			{
				ArmoryTE seed = null; float bestSq = float.MaxValue;
				foreach (TileEntity te in TileEntity.ByID.Values)
				{
					if (te is not ArmoryTE a || !NearPylon(a.Center, pylons)) continue;
					float d = Vector2.DistanceSquared(pos, a.Center);
					if (d < bestSq) { bestSq = d; seed = a; }
				}
				if (seed != null) AddNetwork(settle, seed, pool);
			}
		}

		/// <summary>
		/// The one Armory a Barracks' drones borrow from: the Armory Levy links to that Barracks (the nearest within its link radius). A drone's weapon lives on THAT Armory's lend ledger and goes back to
		/// it, so unlike the turret's read-only pool this is a single, stable Armory. Null when the Barracks has none (or is null).
		/// </summary>
		public static ArmoryTE OfBarracks(BarracksTE barracks) => barracks == null ? null : SettlementSystem.Instance?.LinkedArmory(barracks);

		private static void AddNetwork(SettlementSystem settle, ArmoryTE seed, List<ArmoryTE> pool)
		{
			foreach (ArmoryTE a in settle.NetworkedArmories(seed))
				if (a != null && !pool.Contains(a)) pool.Add(a);
		}

		/// <summary>True when a vanilla pylon stands within Levy's pylon-share radius of <paramref name="c"/> (the same test Levy uses to put an Armory on the network).</summary>
		private static bool NearPylon(Vector2 c, IReadOnlyList<Terraria.GameContent.TeleportPylonInfo> pylons)
		{
			float limit = LevyConstants.PylonShareRadius * LevyConstants.TilePx; limit *= limit;
			for (int i = 0; i < pylons.Count; i++)
			{
				var p = pylons[i].PositionInTiles; // top-left of the 3x4 pylon; measure from its middle
				var pc = new Vector2((p.X + 1.5f) * LevyConstants.TilePx, (p.Y + 2f) * LevyConstants.TilePx);
				if (Vector2.DistanceSquared(c, pc) <= limit) return true;
			}
			return false;
		}
	}
}
