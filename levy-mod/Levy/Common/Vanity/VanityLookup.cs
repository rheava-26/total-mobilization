using System.Collections.Generic;
using Terraria;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.TileEntities;

namespace Levy.Common.Vanity
{
	/// <summary>
	/// Finds the vanity a levy wears: his home barracks' linked armory, then that armory's rows resolved for his roster id and weapon class. The armory is cached per barracks for a short while,
	/// so a draw pass over a hundred levies does not scan the tile entities a hundred times. Works the same on a client, which holds the armory's synced vanity list and the levy's visual gear.
	/// </summary>
	public static class VanityLookup
	{
		private const int CacheTicks = 90;
		private static readonly Dictionary<int, (uint tick, ArmoryTE armory)> _byBarracks = new();

		public static void Reset() => _byBarracks.Clear();

		/// <summary>The armory whose vanity dresses this levy, or null (no home barracks, or none linked).</summary>
		public static ArmoryTE ArmoryFor(SoldierRecord rec)
		{
			if (rec == null || rec.HomeBarracksId < 0) return null;
			uint now = Main.GameUpdateCount;
			if (_byBarracks.TryGetValue(rec.HomeBarracksId, out var hit) && now - hit.tick < CacheTicks && (hit.armory == null || ArmoryStillThere(hit.armory))) return hit.armory;
			var sys = SettlementSystem.Instance;
			var barracks = sys?.GetBarracks(rec.HomeBarracksId);
			var armory = barracks == null ? null : sys.LinkedArmory(barracks);
			_byBarracks[rec.HomeBarracksId] = (now, armory);
			return armory;
		}

		private static bool ArmoryStillThere(ArmoryTE a) => Terraria.DataStructures.TileEntity.ByID.TryGetValue(a.ID, out var cur) && ReferenceEquals(cur, a);

		/// <summary>The class id the vanity rows see for this levy: his weapon's class under the armory's rules (null when he carries none).</summary>
		public static string ClassKeyOf(ArmoryTE armory, SoldierRecord rec)
		{
			var weapon = rec?.Gear?.Weapon;
			return weapon == null || weapon.IsAir ? null : armory.Rules.ClassKeyOf(weapon);
		}

		/// <summary>What this levy should wear from the vanity screen, or null when nothing applies to him.</summary>
		public static VanityLook LookFor(SoldierRecord rec)
		{
			var armory = ArmoryFor(rec);
			if (armory == null || armory.Vanity.IsNeutral) return null;
			var look = armory.Vanity.Resolve(rec.Id, ClassKeyOf(armory, rec));
			return look.Any ? look : null;
		}
	}
}
