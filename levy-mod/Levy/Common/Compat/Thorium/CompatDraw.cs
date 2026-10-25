using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using ThoriumMod.Empowerments;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// What the player sees of the music, drawn over a soldier's head from <see cref="SoldierThoriumGlobalNPC"/>'s synced values:
	/// <list type="bullet">
	/// <item>a bard: a small Inspiration bar (current / maximum, a tick every 10 points) with the count beside it;</item>
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

			// Inspiration bar, above the nameplate.
			if (st.InspMax > 0)
			{
				const int w = 30, h = 4;
				int x = (int)(top.X - w * 0.5f), y = (int)(top.Y - 54f);
				float frac = MathHelper.Clamp(st.InspCur / (float)st.InspMax, 0f, 1f);
				sb.Draw(px, new Rectangle(x - 1, y - 1, w + 2, h + 2), Dark);
				sb.Draw(px, new Rectangle(x, y, (int)(w * frac), h), InspFill);
				for (int k = 10; k < st.InspMax; k += 10) sb.Draw(px, new Rectangle(x + (int)(w * (k / (float)st.InspMax)), y, 1, h), Dark);
				Utils.DrawBorderString(sb, st.InspCur + "/" + st.InspMax, new Vector2(x + w + 4f, y + h * 0.5f), InspText, 0.55f, 0f, 0.5f);
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
