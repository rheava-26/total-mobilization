using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Content.TileEntities
{
	/// <summary>
	/// Raise the floor: new or better armor and accessories go to the worst-equipped levies first (see the armor / accessory upgrade paths in ArmoryTE.cs).
	/// </summary>
	public partial class ArmoryTE
	{
		/// <summary>Worst-equipped-first gear upgrades (default on). Test seam: the gear audit turns it off to measure the old first-come behaviour.</summary>
		internal static bool FloorFirst = true;

		internal static float ArmorScoreOf(Item it) => ArmorScore(it);
		internal static float AccessoryScoreOf(Item it, DamageClass cls) => AccessoryScore(it, cls);
		internal static string StemOf(Item it) => ArmorStem(it);
	}
}
