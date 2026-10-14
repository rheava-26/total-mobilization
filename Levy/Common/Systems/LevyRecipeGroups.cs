using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Levy.Common.Systems
{
	/// <summary>
	/// "Any Banner" (every item that places <see cref="TileID.Banners"/> or a <see cref="ModBannerTile"/>) and "Any Chest"
	/// (every item that places <see cref="TileID.Containers"/> / <see cref="TileID.Containers2"/>). Built from
	/// <see cref="ContentSamples.ItemsByType"/>, which is populated before <see cref="ModSystem.AddRecipeGroups"/> runs.
	/// </summary>
	public class LevyRecipeGroups : ModSystem
	{
		public override void AddRecipeGroups()
		{
			var banners = new List<int> { ItemID.RedBanner };
			var chests = new List<int> { ItemID.Chest };

			foreach (var kv in ContentSamples.ItemsByType) {
				Item item = kv.Value;
				if (item == null || item.type <= 0 || item.createTile < 0) continue;
				int tile = item.createTile;
				if (tile == TileID.Banners || (tile >= TileID.Count && TileLoader.GetTile(tile) is ModBannerTile)) {
					if (!banners.Contains(item.type)) banners.Add(item.type);
				}
				else if (tile == TileID.Containers || tile == TileID.Containers2) {
					if (!chests.Contains(item.type)) chests.Add(item.type);
				}
			}

			// LegacyMisc.37 is the vanilla "Any" prefix used by every recipe group name.
			RecipeGroup.RegisterGroup(LevyConstants.RecipeGroupAnyBanner,
				new RecipeGroup(() => Language.GetTextValue("LegacyMisc.37") + " " + Language.GetTextValue("MapObject.Banner"), banners.ToArray()));
			RecipeGroup.RegisterGroup(LevyConstants.RecipeGroupAnyChest,
				new RecipeGroup(() => Language.GetTextValue("LegacyMisc.37") + " " + Lang.GetItemNameValue(ItemID.Chest), chests.ToArray()));
		}
	}
}
