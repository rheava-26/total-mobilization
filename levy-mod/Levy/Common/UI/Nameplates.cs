using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Levy.Common.Config;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>Name, grade chevrons, HP bar, fear icon over a soldier. OWNED BY THE UI AGENT.</summary>
	public static class Nameplates
	{
		private static int _drawnThisFrame, _drawnLastFrame, _frameStamp = -1;
		private static readonly Color Gold = new(255, 215, 80), Dark = new(20, 20, 20, 200), FearOrange = new(255, 140, 30), RoutRed = new(255, 60, 40), WeaponTan = new(200, 185, 150);

		/// <summary>Soldiers whose plates were requested last frame (per draw frame, not per tick, via Main.drawsCountedForFPS).</summary>
		public static int DrawnLastFrame => _drawnLastFrame;

		/// <summary>Draw from the body's PostDraw. <paramref name="screenPos"/> is Main.screenPosition (world minus screen = pixels).</summary>
		public static void Draw(SoldierNPC s, SpriteBatch spriteBatch, Vector2 screenPos)
		{
			if (Main.drawsCountedForFPS != _frameStamp) { _frameStamp = Main.drawsCountedForFPS; _drawnLastFrame = _drawnThisFrame; _drawnThisFrame = 0; }
			_drawnThisFrame++;
			if (s?.Record == null || spriteBatch == null || Main.dedServ) return;
			if (!LevyConfig.Get.Nameplates) return;
			if (Math.Max(UISystem.SoldiersOnScreenLastFrame, _drawnLastFrame) > LevyConstants.NameplateHideAbove) return;

			var npc = s.NPC;
			var top = npc.Top - screenPos;
			float y = top.Y - LevyConstants.UI.NameplateOffsetY;
			float scale = LevyConstants.UI.NameplateTextScale;
			var font = FontAssets.MouseText.Value;
			string name = s.Record.Name ?? "";
			bool militia = s.IsMilitia;

			// name (+ chevrons for regulars), centred over the head
			float nameW = font.MeasureString(name).X * scale;
			string chev = militia ? "" : UIActions.Chevrons(s.Grade);
			float chevW = militia ? 0f : font.MeasureString(chev).X * scale + 4f;
			float startX = top.X - (nameW + chevW) * 0.5f;
			Utils.DrawBorderString(spriteBatch, name, new Vector2(startX, y), s.Record.Veteran ? new Color(255, 240, 200) : Color.White, scale, 0f, 0.5f);
			if (!militia) Utils.DrawBorderString(spriteBatch, chev, new Vector2(startX + nameW + 4f, y), Gold, scale, 0f, 0.5f);

			// HP bar
			int w = LevyConstants.UI.NameplateHpBarWidth, h = LevyConstants.UI.NameplateHpBarHeight;
			float frac = MathHelper.Clamp(npc.life / (float)Math.Max(1, npc.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(y + 8f);
			spriteBatch.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), Dark);
			spriteBatch.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), Color.Lerp(Color.Red, Color.LimeGreen, frac));

			// held weapon, small and muted, centred under the HP bar (militia and regulars alike)
			var weapon = s.Record.Gear?.Weapon;
			string weaponName = weapon == null || weapon.IsAir ? "Unarmed" : weapon.Name;
			Utils.DrawBorderString(spriteBatch, weaponName, new Vector2(top.X, by + h + 6f), WeaponTan, scale * LevyConstants.UI.NameplateWeaponScale, 0.5f, 0f);
			if (militia) return;

			// fear
			if (s.IsRouted)
			{
				Utils.DrawBorderString(spriteBatch, "!!", new Vector2(bx + w + 6f, by + h * 0.5f), RoutRed, 1.1f, 0f, 0.5f);
			}
			else if (s.Fear > 0f)
			{
				float f = MathHelper.Clamp(s.Fear / 100f, 0f, 1f);
				float fs = MathHelper.Lerp(LevyConstants.UI.FearIconMinScale, LevyConstants.UI.FearIconMaxScale, f);
				Utils.DrawBorderString(spriteBatch, "!", new Vector2(bx + w + 6f, by + h * 0.5f), Color.Lerp(Color.Yellow, FearOrange, f), fs, 0f, 0.5f);
			}
		}
	}
}
