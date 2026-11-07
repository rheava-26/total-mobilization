using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Content.TileEntities;    // ArmoryTE
using Mechanization.Common;         // ArmoryLink.NearPylon
using R = Mechanization.Common.MechConstants.Relay;

namespace Mechanization.Content.Relay
{
	/// <summary>
	/// Armory Relay logic. A relay links to ONE Armory: the nearest within <see cref="R.LinkReachTiles"/>, else (when the relay stands at a vanilla pylon) the nearest Armory on the pylon network.
	/// Anything within <see cref="R.ServeRadiusTiles"/> of a relay treats that Armory (and its pylon network) as in reach. The core is pure over passed lists so it can be checked without a world;
	/// the live wrappers read the tile entities. Several relays each add their Armory to the pool, which de-duplicates, so nothing stacks.
	/// </summary>
	public static class ArmoryRelays
	{
		public const float PxPerTile = 16f;

		/// <summary>The Armory a relay at <paramref name="relayCenter"/> links to, from the given candidates. Null when none.</summary>
		public static ArmoryTE Link(Vector2 relayCenter, IEnumerable<ArmoryTE> armories, bool nearPylon, Func<ArmoryTE, bool> onNetwork)
		{
			float reach = R.LinkReachTiles * PxPerTile; reach *= reach;
			ArmoryTE best = null, bestNet = null; float bestD = float.MaxValue, bestNetD = float.MaxValue;
			foreach (ArmoryTE a in armories)
			{
				if (a == null) continue;
				float d = Vector2.DistanceSquared(relayCenter, a.Center);
				if (d <= reach && d < bestD) { bestD = d; best = a; }
				if (nearPylon && onNetwork != null && d < bestNetD && onNetwork(a)) { bestNetD = d; bestNet = a; }
			}
			return best ?? bestNet;
		}

		/// <summary>True when <paramref name="spot"/> is within the serve radius of <paramref name="relayCenter"/>.</summary>
		public static bool Serves(Vector2 relayCenter, Vector2 spot)
		{
			float r = R.ServeRadiusTiles * PxPerTile;
			return Vector2.DistanceSquared(relayCenter, spot) <= r * r;
		}

		/// <summary>Adds to <paramref name="into"/> the linked Armory of every relay that serves <paramref name="spot"/> (no duplicates).</summary>
		public static void LinkedFor(Vector2 spot, IEnumerable<ArmoryRelayTE> relays, IEnumerable<ArmoryTE> armories, Func<Vector2, bool> pylonNear, Func<ArmoryTE, bool> onNetwork, List<ArmoryTE> into)
		{
			foreach (ArmoryRelayTE r in relays)
			{
				if (!Serves(r.Center, spot)) continue;
				ArmoryTE a = Link(r.Center, armories, pylonNear != null && pylonNear(r.Center), onNetwork);
				if (a != null && !into.Contains(a)) into.Add(a);
			}
		}

		// ---------------- live world ----------------
		private static readonly List<ArmoryTE> _scratch = new();
		private static readonly List<ArmoryRelayTE> _relays = new();
		private static readonly List<ArmoryTE> _armories = new();

		/// <summary>The Armories lent to <paramref name="spot"/> by nearby relays in the live world. The returned list is reused; read it at once.</summary>
		public static List<ArmoryTE> LinkedFor(Vector2 spot)
		{
			_scratch.Clear();
			Gather();
			if (_relays.Count > 0) LinkedFor(spot, _relays, _armories, LivePylonNear, LiveOnNetwork, _scratch);
			return _scratch;
		}

		public static ArmoryTE FirstLinkedFor(Vector2 spot) { var l = LinkedFor(spot); return l.Count > 0 ? l[0] : null; }

		/// <summary>The Armory this one relay is linked to right now (for the hover readout). Null when none.</summary>
		public static ArmoryTE LinkOf(ArmoryRelayTE relay)
		{
			Gather();
			return Link(relay.Center, _armories, LivePylonNear(relay.Center), LiveOnNetwork);
		}

		private static void Gather()
		{
			_relays.Clear(); _armories.Clear();
			foreach (TileEntity te in TileEntity.ByID.Values)
			{
				if (te is ArmoryRelayTE r) _relays.Add(r);
				else if (te is ArmoryTE a) _armories.Add(a);
			}
		}

		private static bool LivePylonNear(Vector2 c)
		{
			var pylons = Main.PylonSystem?.Pylons;
			return pylons != null && pylons.Count > 0 && ArmoryLink.NearPylon(c, pylons);
		}

		private static bool LiveOnNetwork(ArmoryTE a) => LivePylonNear(a.Center);
	}
}
