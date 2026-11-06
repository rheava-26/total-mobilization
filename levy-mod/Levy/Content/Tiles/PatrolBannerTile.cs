using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common;
using Levy.Content.Items.Placeables;
using Levy.Content.TileEntities;

namespace Levy.Content.Tiles
{
	/// <summary>1x3 hanging banner anchored to the ceiling like vanilla banners (tile 91). Two mark a patrol line. OWNED BY THE TILES AGENT.</summary>
	public class PatrolBannerTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileID.Sets.MultiTileSway[Type] = true; // swings in the wind like vanilla banners

			// Same anchoring as TileID.Banners: hang from a solid tile, a solid side, a solid bottom or a planter box…
			TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2Top);
			TileObjectData.newTile.Width = LevyConstants.PatrolBannerWidth;
			TileObjectData.newTile.Height = LevyConstants.PatrolBannerHeight;
			TileObjectData.newTile.Origin = new Point16(0, 0);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.newTile.StyleHorizontal = true;
			TileObjectData.newTile.AnchorTop = new AnchorData(AnchorType.SolidTile | AnchorType.SolidSide | AnchorType.SolidBottom | AnchorType.PlanterBox, LevyConstants.PatrolBannerWidth, 0);
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.DrawYOffset = -2;
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<PatrolBannerTE>().Hook_AfterPlacement, -1, 0, true);
			// …or from the underside of an un-hammered platform.
			TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
			TileObjectData.newAlternate.AnchorTop = new AnchorData(AnchorType.Platform, LevyConstants.PatrolBannerWidth, 0);
			TileObjectData.newAlternate.DrawYOffset = -10;
			TileObjectData.addAlternate(0);
			TileObjectData.addTile(Type);

			DustType = DustID.Silk;
			AddMapEntry(new Color(170, 40, 40), CreateMapEntryName());
		}

		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			// Hand the whole banner to the vanilla wind renderer once, from its top-left tile; the default draw is skipped.
			if (TileObjectData.IsTopLeft(i, j))
				Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.MultiTileVine);
			return false;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			ModContent.GetInstance<PatrolBannerTE>().Kill(i, j);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<PatrolBannerItem>();
		}
	}
}
