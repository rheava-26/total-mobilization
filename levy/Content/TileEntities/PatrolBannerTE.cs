using Terraria.ModLoader;

namespace Levy.Content.TileEntities
{
	/// <summary>Marks a patrol banner so routes can be enumerated. Two banners define a patrol line.</summary>
	public class PatrolBannerTE : ModTileEntity
	{
		public override bool IsTileValidForEntity(int x, int y) => false; // Tiles agent replaces
		public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate) => -1; // Tiles agent replaces
	}
}
