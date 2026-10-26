using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Content.TileEntities;
using Mechanization.Common;
using Mechanization.Content.Items;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// Two lightweight hints, both a line of text by the cursor: while holding the turret item, whether the spot under the cursor is in reach of a Levy
	/// Armory (so the player knows if it will mount a real weapon or fire the plain bullet); and while hovering a placed turret, the weapon it has mounted
	/// and its Armory reach. Client-only drawing; reads only synced state.
	/// </summary>
	public class TurretHud : ModSystem
	{
		private readonly List<ArmoryTE> _placePool = new();
		private uint _placeStamp;
		private Vector2 _placeAt;

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int i = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (i < 0) return;
			layers.Insert(i, new LegacyGameInterfaceLayer("Mechanization: Turret Hint", () => { DrawHint(); return true; }, InterfaceScaleType.UI));
			layers.Insert(i, new LegacyGameInterfaceLayer("Mechanization: Turret Reach", () => { DrawReach(); return true; }, InterfaceScaleType.Game)); // world-scaled: the ring sits on the map
		}

		/// <summary>
		/// A dotted ring at each turret's reach: the turret under the cursor or being talked to, or every turret while the turret item is held (so a new one can be spaced to cover
		/// the line). Reads only the synced reach.
		/// </summary>
		private static void DrawReach()
		{
			Player me = Main.LocalPlayer;
			if (me == null || !me.active || me.dead || Main.mapFullscreen) return;
			bool holding = me.HeldItem != null && me.HeldItem.type == ModContent.ItemType<TurretItem>() && !Main.playerInventory;
			Vector2 m = Main.MouseWorld;
			Texture2D px = TextureAssets.MagicPixel.Value;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.ModNPC is not TurretNPC t || !t.IsAlive) continue;
				Rectangle box = npc.Hitbox; box.Inflate(6, 6);
				bool talking = me.talkNPC == i;
				if (!holding && !talking && !(!me.mouseInterface && box.Contains(m.ToPoint()))) continue;
				float r = t.ReachPx;
				Color col = new Color(140, 255, 150, 0) * 0.55f;
				for (int k = 0; k < T.RingDots; k++)
				{
					float a = MathHelper.TwoPi * k / T.RingDots;
					Vector2 p = npc.Center + new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a)) * r - Main.screenPosition;
					if (p.X < -8f || p.Y < -8f || p.X > Main.screenWidth + 8f || p.Y > Main.screenHeight + 8f) continue;
					Main.spriteBatch.Draw(px, p, null, col, 0f, new Vector2(0.5f, 0.5f), new Vector2(3f, 3f), SpriteEffects.None, 0f);
				}
			}
		}

		private void DrawHint()
		{
			Player me = Main.LocalPlayer;
			if (me == null || !me.active || me.dead || Main.mapFullscreen || me.mouseInterface || me.talkNPC >= 0) return;
			string text = null; Color color = Color.White;

			if (me.HeldItem != null && me.HeldItem.type == ModContent.ItemType<TurretItem>() && !Main.playerInventory)
			{
				Vector2 at = Main.MouseWorld;
				uint now = Main.GameUpdateCount;
				if (_placeStamp == 0 || now - _placeStamp >= (uint)T.HudRefreshTicks || now < _placeStamp || Vector2.DistanceSquared(at, _placeAt) > 16f * 16f)
				{
					_placeStamp = now == 0 ? 1u : now; _placeAt = at;
					ArmoryLink.Find(at, _placePool);
				}
				int n = _placePool.Count;
				text = Language.GetTextValue(n > 0 ? "Mods.Mechanization.UI.HintPlaceLinked" : "Mods.Mechanization.UI.HintPlaceUnlinked", n);
				color = n > 0 ? new Color(140, 255, 150) : new Color(255, 190, 110);
			}
			else
			{
				Vector2 m = Main.MouseWorld;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (!npc.active || npc.ModNPC is not TurretNPC t || !t.IsAlive) continue;
					Rectangle box = npc.Hitbox; box.Inflate(6, 6);
					if (!box.Contains(m.ToPoint())) continue;
					int n = t.ArmoriesInReach().Count;
					int w = t.MountedWeaponType;
					if (w > 0 && w < ItemLoader.ItemCount) text = Language.GetTextValue("Mods.Mechanization.UI.HintTurretMounted", Lang.GetItemNameValue(w), n, t.ReachTiles);
					else text = Language.GetTextValue(n > 0 ? "Mods.Mechanization.UI.HintTurretNoWeapon" : "Mods.Mechanization.UI.HintTurretNoArmory", t.ReachTiles);
					color = w > 0 ? new Color(140, 255, 150) : new Color(255, 190, 110);
					break;
				}
			}

			if (text == null) return;
			Vector2 pos = new Vector2(Main.mouseX + T.HudOffsetX, Main.mouseY + T.HudOffsetY);
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
			if (pos.X + size.X > Main.screenWidth - 8) pos.X = Main.screenWidth - 8 - size.X; // keep it on screen near the right edge
			Utils.DrawBorderString(Main.spriteBatch, text, pos, color);
		}
	}
}
