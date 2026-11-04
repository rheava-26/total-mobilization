using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using ReLogic.Content;
using Terraria.GameContent;
using Terraria.ModLoader;
using ThoriumMod.Empowerments;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// What the player sees of the music, drawn over a soldier's head from <see cref="SoldierThoriumGlobalNPC"/>'s synced values:
	/// <list type="bullet">
	/// <item>a bard: Thorium's own Inspiration wheel (its UI sheet, drawn as Thorium draws the player's, scaled down) with the count beside it; if the sheet cannot be loaded, a small bar instead;</item>
	/// <item>any soldier under a bard's empowerments: a row of small squares, one per empowerment, each in that empowerment's own Thorium colour.</item>
	/// </list>
	/// Light on purpose: a handful of rectangles, drawn only for soldiers that have something to show, client side only.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class CompatDraw
	{
		private static readonly Color Dark = new(20, 20, 20, 200), InspFill = new(190, 130, 255), InspText = new(225, 200, 255);
		private static readonly Color[] _colors = new Color[SoldierThoriumGlobalNPC.EmpSlots];
		private static readonly bool[] _have = new bool[SoldierThoriumGlobalNPC.EmpSlots];

		public static void Draw(NPC npc, SoldierThoriumGlobalNPC st, SpriteBatch sb, Vector2 screenPos)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			Vector2 top = npc.Top - screenPos;
			top.Y += npc.gfxOffY;

			// Inspiration wheel (Thorium's own sheet), above the nameplate; the old bar is the fallback should the sheet fail to load.
			if (st.InspMax > 0)
			{
				Texture2D sheet = WheelSheet();
				if (sheet != null && TryDrawWheel(sb, sheet, new Vector2((int)top.X, (int)(top.Y - WheelLift)), st.InspCur, st.InspBase > 0 ? st.InspBase : st.InspMax, st.InspMax))
				{
					Utils.DrawBorderString(sb, st.InspCur + "/" + st.InspMax, new Vector2((int)top.X + WheelScale * 31f + 3f, (int)(top.Y - WheelLift)), InspText, 0.5f, 0f, 0.5f);
				}
				else
				{
					const int w = 30, h = 4;
					int x = (int)(top.X - w * 0.5f), y = (int)(top.Y - 54f);
					float frac = MathHelper.Clamp(st.InspCur / (float)st.InspMax, 0f, 1f);
					sb.Draw(px, new Rectangle(x - 1, y - 1, w + 2, h + 2), Dark);
					sb.Draw(px, new Rectangle(x, y, (int)(w * frac), h), InspFill);
					for (int k = 10; k < st.InspMax; k += 10) sb.Draw(px, new Rectangle(x + (int)(w * (k / (float)st.InspMax)), y, 1, h), Dark);
					Utils.DrawBorderString(sb, st.InspCur + "/" + st.InspMax, new Vector2(x + w + 4f, y + h * 0.5f), InspText, 0.55f, 0f, 0.5f);
				}
			}

			// Empowerment squares, one per empowerment the soldier is under.
			if (st.EmpMask != 0)
			{
				int n = 0;
				for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++) if ((st.EmpMask & (1u << t)) != 0) n++;
				const int size = 5, gap = 1;
				int total = n * size + (n - 1) * gap;
				int x = (int)(top.X - total * 0.5f), y = (int)(top.Y - 44f);
				for (int t = 0; t < SoldierThoriumGlobalNPC.EmpSlots; t++)
				{
					if ((st.EmpMask & (1u << t)) == 0) continue;
					sb.Draw(px, new Rectangle(x - 1, y - 1, size + 2, size + 2), Dark);
					sb.Draw(px, new Rectangle(x, y, size, size), ColorOf((byte)t));
					x += size + gap;
				}
			}
		}

		/// <summary>Height over the soldier's head of the wheel's centre, and the wheel's size relative to the player's full-size one.</summary>
		private const float WheelLift = 68f, WheelScale = 0.5f;
		private static Asset<Texture2D> _sheet;
		private static bool _sheetFailed;

		/// <summary>Thorium's Inspiration wheel sheet (<c>UI/ResourceBars/BardWheel_Sheet</c>, 7 x 10 frames), or null when it cannot be had.</summary>
		private static Texture2D WheelSheet()
		{
			if (_sheetFailed) return null;
			try
			{
				if (_sheet == null)
				{
					if (!ModLoader.TryGetMod("ThoriumMod", out Mod thorium)) { _sheetFailed = true; return null; }
					_sheet = thorium.Assets.Request<Texture2D>("UI/ResourceBars/BardWheel_Sheet", AssetRequestMode.ImmediateLoad);
				}
				return _sheet.IsLoaded ? _sheet.Value : null;
			}
			catch (Exception) { _sheetFailed = true; return null; }
		}

		/// <summary>
		/// Thorium's <c>BardWheel.DrawSelf</c>, run for one bard from its synced numbers instead of the local player's: the same frames of the same sheet in the same order
		/// (base ring, full glow, one extra ring per two points of maximum past 10, the filled segments, then the over-maximum band). <paramref name="max"/> is <c>bardResourceMax</c>, <paramref name="max2"/> <c>bardResourceMax2</c>.
		/// Returns false if the draw throws, so the caller can fall back to the bar.
		/// </summary>
		private static bool TryDrawWheel(SpriteBatch sb, Texture2D sheet, Vector2 pos, int res, int max, int max2)
		{
			try
			{
				Color white = Color.White;
				Rectangle fr = Utils.Frame(sheet, 7, 10, 0, 0, 0, 0);
				Vector2 origin = Utils.Size(fr) / 2f;
				float scale = WheelScale;
				sb.Draw(sheet, pos, fr, white, 0f, origin, scale, SpriteEffects.None, 0f);
				int rings = (Math.Min(20, max) - 10) / 2;
				fr.Y = sheet.Height / 10 * 2;
				sb.Draw(sheet, pos, fr, white, 0f, origin, scale, SpriteEffects.None, 0f);
				if (res == max2)
				{
					fr.Y = sheet.Height / 10;
					sb.Draw(sheet, pos, fr, white, 0f, origin, scale, SpriteEffects.None, 0f);
				}
				if (rings > 0)
				{
					fr.Y = sheet.Height / 10 * 2;
					for (int i = 0; i < rings; i++)
					{
						fr.Y += sheet.Height / 10;
						sb.Draw(sheet, pos, fr, white, 0f, origin, scale, SpriteEffects.None, 0f);
					}
				}
				rings += 5;
				float filled = Math.Min(Math.Min(res, 40) / (float)max * rings, rings);
				for (int j = 0; j < filled; j++)
				{
					float a = Math.Min(filled - j, 1f);
					int fy = j, fx;
					if (rings <= 5 || rings > 8)
					{
						int tens = (max - 1) / 10;
						fx = 2 + tens - (j + 1 <= max - 10 * tens ? 1 : 2);
					}
					else
					{
						fx = 2;
						if (j > max - 10) { fy -= (max - 9) / 2; fx--; }
					}
					fr = Utils.Frame(sheet, 7, 10, fx, fy, 0, 0);
					sb.Draw(sheet, pos, fr, white * a, 0f, origin, scale, SpriteEffects.None, 0f);
				}
				float over = res - max;
				if (over > 0f)
				{
					filled = rings * Math.Min(over, 10f) / 10f;
					for (int k = 0; k < filled; k++)
					{
						bool second = over > 10f;
						float a = Math.Min(second ? (over - 10f) / ((max2 - max - 10f) / rings) : filled - k, 1f);
						int fx = second ? 6 : 5, fy = k;
						if (rings > 5 && rings <= 8) { if (k > max - 10) fy = (fy - (max - 9) / 2) * 2; }
						else if (rings <= 5) fy *= 2;
						over -= 10f / rings;
						fr = Utils.Frame(sheet, 7, 10, fx, fy, 0, 0);
						sb.Draw(sheet, pos, fr, white * a, 0f, origin, scale, SpriteEffects.None, 0f);
					}
				}
				return true;
			}
			catch (Exception) { _sheetFailed = true; return false; }
		}

		/// <summary>The empowerment's own Thorium colour (the one its combat text uses), cached per type.</summary>
		private static Color ColorOf(byte type)
		{
			if (type >= _colors.Length) return Color.White;
			if (_have[type]) return _colors[type];
			Color c = Color.White;
			Empowerment emp = EmpowermentLoader.GetEmpowerment(type);
			switch (emp?.Name)
			{
				case "Damage": c = new Color(255, 0, 0); break;
				case "FlatDamage": c = new Color(175, 0, 0); break;
				case "AttackSpeed": c = new Color(255, 135, 40); break;
				case "CriticalStrikeChance": c = new Color(250, 215, 0); break;
				case "MovementSpeed": c = new Color(110, 255, 0); break;
				case "Defense": c = new Color(50, 150, 255); break;
				case "DamageReduction": c = new Color(50, 75, 255); break;
				case "LifeRegeneration": c = new Color(175, 175, 175); break;
				case "ResourceRegen": c = new Color(125, 75, 255); break;
				case "ResourceMaximum": c = new Color(200, 25, 255); break;
				case "ResourceConsumptionChance": c = new Color(255, 125, 255); break;
				case "InvincibilityFrames": c = new Color(0, 255, 255); break;
				case "EmpowermentProlongation": c = new Color(200, 255, 255); break;
			}
			_colors[type] = c; _have[type] = emp != null;
			return c;
		}
	}
}
