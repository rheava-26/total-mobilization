using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>
	/// The combat chat bar: a blue bar over the speaker's head whose text types itself in. It replaces the old floating combat-text lines.
	/// <see cref="ChatBarQueue"/> decides which few lines get a bar (nearest and most important first); this class feeds it, ticks it and draws it.
	/// Client and single player only. Drawn in one world-space layer so bars that would overlap (a close-up squad) are pushed apart.
	/// </summary>
	public class ChatBars : ModSystem
	{
		public static readonly ChatBarQueue Queue = new();

		private static readonly Color Back = new(24, 52, 128), Edge = new(120, 170, 255), Ink = new(245, 248, 255);

		/// <summary>Priority a trigger's line carries for bar slots.</summary>
		public static int PriorityOf(byte triggerByte)
		{
			if (triggerByte == byte.MaxValue) return LevyConstants.ChatBarPriorityChatter;
			switch ((DialogueTrigger)triggerByte)
			{
				case DialogueTrigger.BOSS_SIGHTED: case DialogueTrigger.BOSS: case DialogueTrigger.COMRADE_DOWN:
				case DialogueTrigger.ROUTED: case DialogueTrigger.MUTINY:
					return LevyConstants.ChatBarPriorityBig;
				case DialogueTrigger.RALLIED: case DialogueTrigger.VICTORY:
				case DialogueTrigger.EVENT_GOBLINS: case DialogueTrigger.EVENT_PIRATES: case DialogueTrigger.EVENT_ECLIPSE: case DialogueTrigger.EVENT_PUMPKIN:
				case DialogueTrigger.EVENT_FROST: case DialogueTrigger.EVENT_MARTIANS: case DialogueTrigger.EVENT_OLDONES: case DialogueTrigger.EVENT_FROSTLEGION:
					return LevyConstants.ChatBarPriorityNotice;
				default: return LevyConstants.ChatBarPriorityChatter;
			}
		}

		/// <summary>Show a line over a speaker (client / single player). Off-screen or out-of-range speakers are ignored.</summary>
		public static void Show(int npcIndex, Rectangle hitbox, string text, byte triggerByte)
		{
			if (Main.dedServ || Main.netMode == Terraria.ID.NetmodeID.Server || string.IsNullOrEmpty(text)) return;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs || !LevyUtil.IsOnScreen(hitbox)) return;
			Queue.Enqueue(npcIndex, text, PriorityOf(triggerByte), (int)Main.GameUpdateCount, DistanceTiles(npcIndex));
		}

		/// <summary>Distance from the local player to a speaker in tiles; negative when the speaker is gone.</summary>
		private static float DistanceTiles(int npcIndex)
		{
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return -1f;
			NPC n = Main.npc[npcIndex];
			if (!n.active) return -1f;
			return Vector2.Distance(n.Center, Main.LocalPlayer.Center) / LevyConstants.TilePx;
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gamePaused) return;
			Queue.Tick((int)Main.GameUpdateCount, DistanceTiles);
		}

		public override void OnWorldLoad() => Queue.Clear();
		public override void OnWorldUnload() => Queue.Clear();

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int i = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (i < 0) i = layers.Count;
			layers.Insert(i, new LegacyGameInterfaceLayer("Levy: Chat Bars", () => { DrawAll(Main.spriteBatch); return true; }, InterfaceScaleType.Game));
		}

		private sealed class BarLayout { public string[] Rows; public int W, H; public int Rev; }

		private static void DrawAll(SpriteBatch sb)
		{
			if (Queue.Active.Count == 0) return;
			int now = (int)Main.GameUpdateCount;
			var font = FontAssets.MouseText.Value;
			float sc = LevyConstants.ChatBarTextScale;
			int pad = LevyConstants.ChatBarPad;
			var px = TextureAssets.MagicPixel.Value;

			// place each bar over its speaker, left to right, lifting a bar that would overlap one already placed
			var placed = new List<Rectangle>(Queue.Active.Count);
			var order = new List<ChatBarEntry>(Queue.Active);
			order.Sort((a, b) => Main.npc[a.Speaker].Center.X.CompareTo(Main.npc[b.Speaker].Center.X));
			foreach (var e in order)
			{
				if (e.Speaker < 0 || e.Speaker >= Main.maxNPCs) continue;
				NPC n = Main.npc[e.Speaker];
				if (!n.active) continue;
				float alpha = e.Alpha(now);
				if (alpha <= 0f) continue;

				var lay = e.Layout as BarLayout;
				if (lay == null)
				{
					string[] rows = Utils.WordwrapString(e.Text, font, (int)(LevyConstants.ChatBarMaxTextWidth / sc), LevyConstants.ChatBarMaxRows, out int count);
					var list = new List<string>(count);
					for (int k = 0; k < count && k < rows.Length; k++) if (rows[k] != null) list.Add(rows[k].TrimEnd());
					float w = 0f;
					foreach (string r in list) w = Math.Max(w, font.MeasureString(r).X * sc);
					lay = new BarLayout { Rows = list.ToArray(), W = (int)Math.Ceiling(w) + pad * 2, H = (int)Math.Ceiling(list.Count * font.LineSpacing * sc * 0.92f) + pad * 2 };
					e.Layout = lay;
				}

				Vector2 head = new Vector2(n.Center.X, n.Top.Y) - Main.screenPosition;
				var rect = new Rectangle((int)(head.X - lay.W * 0.5f), (int)(head.Y - LevyConstants.ChatBarHeadGap - lay.H), lay.W, lay.H);
				for (int guard = 0; guard < 8; guard++)
				{
					bool hit = false;
					foreach (var p in placed)
						if (p.Intersects(rect)) { rect.Y = p.Y - rect.Height - LevyConstants.ChatBarStackGap; hit = true; break; }
					if (!hit) break;
				}
				placed.Add(rect);

				DrawBar(sb, px, font, sc, rect, head, lay, e.RevealedChars(now), alpha);
			}
		}

		private static void DrawBar(SpriteBatch sb, Texture2D px, DynamicSpriteFont font, float sc, Rectangle rect, Vector2 head, BarLayout lay, int revealed, float alpha)
		{
			// body, 1 px edge, a short tail pointing at the speaker
			sb.Draw(px, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), Edge * (alpha * 0.9f));
			sb.Draw(px, rect, Back * (alpha * 0.92f));
			int tailX = (int)MathHelper.Clamp(head.X, rect.X + 6, rect.Right - 6);
			sb.Draw(px, new Rectangle(tailX - 3, rect.Bottom + 1, 7, 2), Edge * (alpha * 0.9f));
			sb.Draw(px, new Rectangle(tailX - 1, rect.Bottom + 3, 3, 2), Edge * (alpha * 0.9f));

			int left = revealed;
			float lineH = font.LineSpacing * sc * 0.92f;
			for (int r = 0; r < lay.Rows.Length && left > 0; r++)
			{
				string row = lay.Rows[r];
				int take = Math.Min(left, row.Length);
				left -= row.Length + 1; // the space the wrap swallowed
				string shown = row.Substring(0, take);
				Vector2 pos = new Vector2(rect.X + LevyConstants.ChatBarPad, rect.Y + LevyConstants.ChatBarPad + r * lineH);
				Utils.DrawBorderStringFourWay(sb, font, shown, pos.X, pos.Y, Ink * alpha, new Color(8, 18, 50) * alpha, Vector2.Zero, sc);
			}
		}
	}
}
