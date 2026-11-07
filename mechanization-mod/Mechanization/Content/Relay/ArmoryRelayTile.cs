using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Content.TileEntities;
using R = Mechanization.Common.MechConstants.Relay;

namespace Mechanization.Content.Relay
{
	/// <summary>
	/// The Armory Relay: a 2x2 floor placeable. Its lamp pulses green while it is linked to an Armory and sits a dull red when it is not. Hovering shows which Armory and how far.
	/// </summary>
	public class ArmoryRelayTile : ModTile
	{
		/// <summary>Lamp position inside the 32x32 tile, in pixels (a 4x4 patch, two art pixels square).</summary>
		private const int LampX = 22, LampY = 24, LampSize = 4;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileLavaDeath[Type] = true;
			Main.tileNoAttach[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.newTile.CoordinatePadding = 2;
			TileObjectData.newTile.UsesCustomCanPlace = true;
			TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<ArmoryRelayTE>().Hook_AfterPlacement, -1, 0, true);
			TileObjectData.addTile(Type);

			DustType = DustID.Iron;
			AddMapEntry(new Color(150, 156, 174), CreateMapEntryName());
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			ModContent.GetInstance<ArmoryRelayTE>().Kill(i, j);
		}

		/// <summary>0..1 pulse, one beat per <see cref="R.PulseTicks"/>.</summary>
		private static float Pulse()
		{
			float ph = (Main.GameUpdateCount % R.PulseTicks) / (float)R.PulseTicks;
			return 0.5f + 0.5f * (float)Math.Sin(ph * MathHelper.TwoPi);
		}

		private static ArmoryRelayTE RelayAt(int i, int j)
		{
			Tile t = Main.tile[i, j];
			int x = i - t.TileFrameX / 18 % 2, y = j - t.TileFrameY / 18 % 2;
			return TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity te) ? te as ArmoryRelayTE : null;
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			Tile t = Main.tile[i, j];
			if (t.TileFrameX != 18 || t.TileFrameY != 18) return; // the lamp's own tile
			ArmoryRelayTE relay = RelayAt(i, j);
			if (relay?.Linked == null) return;
			float p = 0.35f + 0.65f * Pulse();
			r = 0.10f * p; g = 0.55f * p; b = 0.25f * p;
		}

		public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile t = Main.tile[i, j];
			if (t.TileFrameX != 0 || t.TileFrameY != 0) return; // draw once, from the top-left tile
			ArmoryRelayTE relay = RelayAt(i, j);
			bool linked = relay?.Linked != null;
			Color c = linked ? Color.Lerp(new Color(30, 110, 50), new Color(120, 255, 140), Pulse()) : new Color(110, 40, 40);
			Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			var pos = new Vector2(i * 16 + LampX, j * 16 + LampY) - Main.screenPosition + zero;
			spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value, new Rectangle((int)pos.X, (int)pos.Y, LampSize, LampSize), c);
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<ArmoryRelayItem>();
			ArmoryRelayTE relay = RelayAt(i, j);
			ArmoryTE a = relay?.Linked;
			if (a == null) { player.cursorItemIconText = Language.GetTextValue("Mods.Mechanization.UI.RelayUnlinked", (int)R.LinkReachTiles); return; }
			int dist = (int)Math.Round(Vector2.Distance(relay.Center, a.Center) / 16f);
			player.cursorItemIconText = Language.GetTextValue("Mods.Mechanization.UI.RelayLinked", a.Position.X, a.Position.Y, dist, (int)R.ServeRadiusTiles);
		}
	}
}
