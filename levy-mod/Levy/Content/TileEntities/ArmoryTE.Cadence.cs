using Terraria;
using Levy.Common;
using Levy.Common.Soldiers;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// When a levy looks at his gear again. Every levy re-checks his own weapon, armor, accessories and sidearms on his own timer (<see cref="UpgradeWeapons"/>), a fresh random 10 to 15 seconds apart, and takes the best free
	/// item for each slot that beats what he has by the upgrade margin. His first look lands at a random point inside the same window, so a freshly mustered army never checks on one tick.
	/// </summary>
	public partial class ArmoryTE
	{
		internal static int GearCheckDelayMin() => LevyConstants.AutoUpgradeCheckMinTicks;
		internal static int GearCheckDelayMax() => LevyConstants.AutoUpgradeCheckMaxTicks;

		/// <summary>Ticks until a levy's next look at his gear: random, between <see cref="LevyConstants.AutoUpgradeCheckMinTicks"/> and <see cref="LevyConstants.AutoUpgradeCheckMaxTicks"/>.</summary>
		internal static uint GearCheckDelay() => (uint)Main.rand.Next(LevyConstants.AutoUpgradeCheckMinTicks, LevyConstants.AutoUpgradeCheckMaxTicks + 1);

		/// <summary>Ticks until a levy's first look: a random point inside the window (he was issued his kit when he was mustered).</summary>
		internal static uint GearCheckFirstDelay(SoldierRecord rec) => (uint)Main.rand.Next(1, LevyConstants.AutoUpgradeCheckMaxTicks + 1);
	}
}
