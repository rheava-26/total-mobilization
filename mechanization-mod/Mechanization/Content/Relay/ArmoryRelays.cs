using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Levy.Content.TileEntities;    // ArmoryTE
using Mechanization.Common;         // ArmoryLink
using R = Mechanization.Common.MechConstants.Relay;

namespace Mechanization.Content.Relay
{
	/// <summary>
	/// Armory Relay logic. A relay links to ONE Armory: the nearest one anywhere in the world (the whole pylon network of that Armory is then lent along with it, see <see cref="ArmoryLink"/>).
	/// Anything within <see cref="R.ServeRadiusTiles"/> of a relay treats that Armory as in reach. The core is pure over passed lists so it can be checked without a world;
	/// the live wrappers read the tile entities through a cache refreshed on a cadence. Several relays each add their Armory to the pool, which de-duplicates, so nothing stacks.
	/// </summary>
	public static class ArmoryRelays
	{
		public const float PxPerTile = 16f;

		/// <summary>The Armory a relay at <paramref name="relayCenter"/> links to: the nearest of the given candidates, at any distance. Null when there are none.</summary>
		public static ArmoryTE Link(Vector2 relayCenter, IEnumerable<ArmoryTE> armories)
		{
			ArmoryTE best = null; float bestD = float.MaxValue;
			foreach (ArmoryTE a in armories)
			{
				if (a == null) continue;
				float d = Vector2.DistanceSquared(relayCenter, a.Center);
				if (d < bestD) { bestD = d; best = a; }
			}
			return best;
		}

		/// <summary>True when <paramref name="spot"/> is within the serve radius of <paramref name="relayCenter"/>.</summary>
		public static bool Serves(Vector2 relayCenter, Vector2 spot)
		{
			float r = R.ServeRadiusTiles * PxPerTile;
			return Vector2.DistanceSquared(relayCenter, spot) <= r * r;
		}

		/// <summary>Adds to <paramref name="into"/> the linked Armory of every relay that serves <paramref name="spot"/> (no duplicates).</summary>
		public static void LinkedFor(Vector2 spot, IEnumerable<ArmoryRelayTE> relays, IEnumerable<ArmoryTE> armories, List<ArmoryTE> into)
		{
			foreach (ArmoryRelayTE r in relays)
			{
				if (!Serves(r.Center, spot)) continue;
				ArmoryTE a = Link(r.Center, armories);
				if (a != null && !into.Contains(a)) into.Add(a);
			}
		}

		// ---------------- live world ----------------
		private static readonly List<ArmoryTE> _scratch = new();
		private static readonly List<ArmoryRelayTE> _relays = new();
		private static readonly List<ArmoryTE> _armories = new();
		private static uint _gatherStamp = uint.MaxValue; private static int _gatherCount = -1;

		/// <summary>The Armories lent to <paramref name="spot"/> by nearby relays in the live world. The returned list is reused; read it at once.</summary>
		public static List<ArmoryTE> LinkedFor(Vector2 spot)
		{
			_scratch.Clear();
			Gather();
			for (int i = 0; i < _relays.Count; i++)
			{
				ArmoryRelayTE r = _relays[i];
				if (!Serves(r.Center, spot)) continue;
				ArmoryTE a = r.Linked;
				if (a != null && !_scratch.Contains(a)) _scratch.Add(a);
			}
			return _scratch;
		}

		public static ArmoryTE FirstLinkedFor(Vector2 spot) { var l = LinkedFor(spot); return l.Count > 0 ? l[0] : null; }

		/// <summary>The Armory this one relay is linked to right now (the relay caches it; see <see cref="ArmoryRelayTE.Linked"/>). Null when there is no Armory in the world.</summary>
		public static ArmoryTE LinkOf(ArmoryRelayTE relay)
		{
			Gather();
			return Link(relay.Center, _armories);
		}

		/// <summary>How many Armories stand in the world right now. Read-only; for the hover text of an unlinked relay.</summary>
		public static int ArmoriesInWorld() { Gather(); return _armories.Count; }

		/// <summary>The relay and Armory lists, rebuilt at most every <see cref="R.RefreshTicks"/> ticks or when the tile-entity count changes, so many relays and many turrets do not each scan every entity every tick.</summary>
		private static void Gather()
		{
			uint now = Main.GameUpdateCount;
			if (_gatherStamp != uint.MaxValue && now >= _gatherStamp && now - _gatherStamp < R.RefreshTicks && _gatherCount == TileEntity.ByID.Count) return;
			_gatherStamp = now; _gatherCount = TileEntity.ByID.Count;
			_relays.Clear(); _armories.Clear();
			foreach (TileEntity te in TileEntity.ByID.Values)
			{
				if (te is ArmoryRelayTE r) _relays.Add(r);
				else if (te is ArmoryTE a) _armories.Add(a);
			}
		}

		/// <summary>Drops the cached lists so the next read rebuilds them (the boot checks swap entities in and out within one tick).</summary>
		internal static void Invalidate() { _gatherStamp = uint.MaxValue; }
	}
}
