using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Warbands.Content.Items;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// What a band says, drawn. Every line is a speech bubble over its speaker: a dark plate with a tinted edge and light text, wrapped to a fixed width, so it reads on snow, sand and crimson alike. Bubbles never sit on one another:
	/// the newest is placed above its speaker and every older one that would overlap it is moved up, and a band says one line at a time (<see cref="BandChatter"/> queues the rest). The layout is a pure function over rectangles
	/// (<see cref="Layout"/>) so the lab can run it without a screen. Server and single player decide what is said; clients draw it (a packet carries the line).
	/// </summary>
	public class BandBubbles : ModSystem
	{
		public class Bubble
		{
			public int Npc, Band; public string Text, Name; public uint Start; public int Life; public Color Tint;
			public Rectangle Rect; public Vector2 Anchor; public string[] Lines; public Vector2 Size;
		}

		public static readonly List<Bubble> Active = new();
		public const int MaxWidthPx = 210, MaxLines = 4, PadPx = 5, GapPx = 3, NameLinePx = 13;
		public const float TextScale = 0.9f;
		/// <summary>Ticks a line stays: time to read it (about 20 characters a second plus a beat), at least 2.5 s, at most 6 s.</summary>
		public static int LifeFor(string text) => Math.Max(150, Math.Min(360, 105 + 4 * (text?.Length ?? 0)));

		public static void Add(int npc, int band, string name, string text, Color tint, uint now)
		{
			if (string.IsNullOrEmpty(text)) return;
			Active.RemoveAll(b => b.Npc == npc); // a speaker has one bubble at a time
			Active.Add(new Bubble { Npc = npc, Band = band, Name = name, Text = text, Start = now, Life = LifeFor(text), Tint = tint });
			if (Active.Count > 24) Active.RemoveAt(0);
		}

		// ---------------------------------------------------------------- the layout
		/// <summary>Measures a line wrapped to <see cref="MaxWidthPx"/>: its lines and its size in pixels, name line included.</summary>
		public delegate Vector2 Measure(string text, out string[] lines);

		/// <summary>Estimate for a machine with no fonts (the headless lab): 6.3 px a character at scale 1, wrapped on spaces.</summary>
		public static Vector2 EstimateMeasure(string text, out string[] lines)
		{
			float cw = 6.3f * TextScale; int perLine = Math.Max(8, (int)(MaxWidthPx / cw));
			var l = new List<string>(); string cur = "";
			foreach (var w in text.Split(' '))
			{
				if (cur.Length > 0 && cur.Length + 1 + w.Length > perLine) { l.Add(cur); cur = w; } else cur = cur.Length == 0 ? w : cur + " " + w;
				if (l.Count >= MaxLines) break;
			}
			if (cur.Length > 0 && l.Count < MaxLines) l.Add(cur);
			lines = l.ToArray(); float wmax = 0f; foreach (var s in lines) wmax = Math.Max(wmax, s.Length * cw);
			return new Vector2(wmax, lines.Length * 17f * TextScale);
		}

		/// <summary>Places every bubble: centred over its anchor (the top of its speaker's head), newest nearest the speaker, each older one moved up until it clears every bubble already placed. Fills <see cref="Bubble.Rect"/>. Returns the number of bubbles that had to move.</summary>
		public static int Layout(List<Bubble> bubbles, Func<Bubble, Vector2?> anchorOf, Measure measure)
		{
			var order = new List<Bubble>(bubbles); order.Sort((a, b) => b.Start.CompareTo(a.Start)); // newest first
			var placed = new List<Rectangle>(); int moved = 0;
			foreach (var b in order)
			{
				var a = anchorOf(b); if (a == null) { b.Rect = Rectangle.Empty; continue; }
				b.Anchor = a.Value;
				Vector2 sz = measure(b.Text, out b.Lines);
				float w = sz.X + PadPx * 2, h = sz.Y + PadPx * 2 + NameLinePx;
				b.Size = new Vector2(w, h);
				var r = new Rectangle((int)(a.Value.X - w * 0.5f), (int)(a.Value.Y - 6f - h), (int)Math.Ceiling(w), (int)Math.Ceiling(h));
				bool again = true; int guard = 0;
				while (again && guard++ < 12)
				{
					again = false;
					foreach (var o in placed)
					{
						var o2 = o; o2.Inflate(GapPx, GapPx);
						if (!o2.Intersects(r)) continue;
						r.Y = o.Y - GapPx - r.Height; again = true; moved++; break;
					}
				}
				b.Rect = r; placed.Add(r);
			}
			return moved;
		}

		// ---------------------------------------------------------------- drawing (client and single player)
		private static Vector2? AnchorOfNpc(Bubble b)
		{
			if ((uint)b.Npc >= Main.maxNPCs) return null;
			var n = Main.npc[b.Npc]; if (!n.active || !(n.ModNPC is BandLevyNPC)) return null;
			return new Vector2(n.Center.X, n.Top.Y - 18f);
		}

		private static Vector2 FontMeasure(string text, out string[] lines)
		{
			var font = FontAssets.MouseText.Value;
			string[] wrapped = Utils.WordwrapString(text, font, (int)(MaxWidthPx / TextScale), MaxLines, out int count);
			var l = new List<string>(); for (int i = 0; i < count && i < wrapped.Length; i++) if (wrapped[i] != null) l.Add(wrapped[i]);
			lines = l.ToArray(); float wmax = 0f; foreach (var s in lines) wmax = Math.Max(wmax, font.MeasureString(s).X * TextScale);
			return new Vector2(wmax, lines.Length * 17f * TextScale);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (Main.dedServ) return;
			int i = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text"); if (i < 0) i = layers.Count;
			layers.Insert(i, new LegacyGameInterfaceLayer("Warbands: Chatter", () => { DrawAll(Main.spriteBatch); return true; }, InterfaceScaleType.Game));
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ) return;
			uint now = Main.GameUpdateCount; Active.RemoveAll(b => now - b.Start > (uint)b.Life);
		}

		public override void OnWorldLoad() { Active.Clear(); }

		private static void DrawAll(SpriteBatch sb)
		{
			if (Active.Count == 0) return;
			uint now = Main.GameUpdateCount; var font = FontAssets.MouseText.Value; var px = TextureAssets.MagicPixel.Value;
			foreach (var b in Active) { if ((uint)b.Npc < Main.maxNPCs && Main.npc[b.Npc].ModNPC is BandLevyNPC m && m.Roster != null) b.Tint = new Color(m.Roster.TintR, m.Roster.TintG, m.Roster.TintB); }
			Layout(Active, AnchorOfNpc, FontMeasure);
			var view = new Rectangle((int)Main.screenPosition.X - 40, (int)Main.screenPosition.Y - 40, Main.screenWidth + 80, Main.screenHeight + 80);
			foreach (var b in Active)
			{
				if (b.Rect.IsEmpty || !b.Rect.Intersects(view)) continue;
				float age = now - b.Start, a = Math.Min(1f, Math.Min(age / 8f, (b.Life - age) / 24f)); if (a <= 0f) continue;
				var r = b.Rect; var sr = new Rectangle(r.X - (int)Main.screenPosition.X, r.Y - (int)Main.screenPosition.Y, r.Width, r.Height);
				Color edge = Color.Lerp(b.Tint, Color.White, 0.45f);
				// plate: near-black, so the light text has the same contrast on a bright or a dark background
				sb.Draw(px, new Rectangle(sr.X - 1, sr.Y - 1, sr.Width + 2, sr.Height + 2), edge * (0.85f * a));
				sb.Draw(px, sr, new Color(14, 11, 18) * (0.88f * a));
				var tail = new Rectangle(sr.Center.X - 1, sr.Bottom, 2, 5); sb.Draw(px, tail, edge * (0.85f * a));
				if (!string.IsNullOrEmpty(b.Name)) Utils.DrawBorderString(sb, b.Name, new Vector2(sr.X + PadPx, sr.Y + 2), edge * a, 0.72f, 0f, 0f);
				float y = sr.Y + PadPx + NameLinePx;
				foreach (var line in b.Lines ?? Array.Empty<string>())
				{
					Utils.DrawBorderString(sb, line, new Vector2(sr.X + PadPx, y), new Color(255, 244, 222) * a, TextScale, 0f, 0f);
					y += 17f * TextScale;
				}
			}
		}

		// ---------------------------------------------------------------- the packet
		public static void Send(NPC npc, int band, string name, string text)
		{
			if (Main.netMode != NetmodeID.Server) return;
			var p = Warbands.Instance.GetPacket();
			p.Write((byte)WarbandsPacket.Chatter); p.Write((short)npc.whoAmI); p.Write(band); p.Write(name ?? ""); p.Write(text);
			p.Send();
		}

		public static void Receive(BinaryReader r)
		{
			int npc = r.ReadInt16(), band = r.ReadInt32(); string name = r.ReadString(), text = r.ReadString();
			if (Main.dedServ) return;
			Add(npc, band, name, text, new Color(240, 130, 110), Main.GameUpdateCount);
		}
	}
}
