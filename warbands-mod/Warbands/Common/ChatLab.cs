using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// CHAT LAB (env WARBANDS_LIVE=chat; WARBANDS_SEEDS fights, default 8; WARBANDS_CHAT_TYPE the band type id, default ForestDeserters; WARBANDS_CHAT_SIZE members, default 5; WARBANDS_CHAT_LEGACY=1 runs the old chatter; WARBANDS_OUT the TSV).
	/// A band of five fights the stand-in for 90 s (the stand-in lands a share of its hits, so members get hurt and fall and the band has things to say). Every line shown is recorded (tick, speaker, position, text) through <see cref="BandChatter.Probe"/>.
	/// The screen is modelled every 6 ticks: the old chatter as vanilla combat text (a rectangle of 8.5 px a character where the line was said, floating up, 210 ticks), the new chatter as speech bubbles through <see cref="BandBubbles.Layout"/> (headless, so text
	/// width is estimated at 6.3 px a character). Measured: lines shown, the most bubbles on screen at once, the share of frames with two or more bubbles in which two overlap, the share of frames in which two members of one band speak at once, and the mean overlapped
	/// area of those frames as a share of the smaller bubble. Static: the lowest contrast ratio of the line colour against sample backgrounds (WCAG, text against what is behind it: old text against the world, new text against its plate).
	/// </summary>
	public partial class WarbandsLab
	{
		private class Said { public uint Tick; public int Npc, Band; public float X, Y; public string Text; }

		private static double Luma(Color c) { double F(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); } return 0.2126 * F(c.R) + 0.7152 * F(c.G) + 0.0722 * F(c.B); }
		private static double Contrast(Color a, Color b) { double la = Luma(a), lb = Luma(b); return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05); }

		private void AddChatTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 8;
			int size = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_CHAT_SIZE"), out int zv) ? zv : 5;
			var type = BandTypes.ById(Environment.GetEnvironmentVariable("WARBANDS_CHAT_TYPE") ?? "ForestDeserters");
			bool legacy = BandChatter.Legacy;
			_tsv.AppendLine("mode\tseed\tlines\tmax_bubbles\tframes2\toverlap_frames_pct\ttalk_over_pct\toverlap_area_pct\tlines_per_min");
			// contrast, once
			{
				var bgs = new (string, Color)[] { ("snow", new Color(235, 242, 255)), ("sand", new Color(222, 196, 130)), ("dark cave", new Color(22, 20, 30)), ("crimson", new Color(160, 42, 52)), ("hellstone", new Color(190, 66, 40)), ("hallow", new Color(255, 230, 240)), ("jungle", new Color(70, 120, 50)), ("dungeon", new Color(55, 60, 110)), ("sky", new Color(120, 180, 250)) };
				var old = new Color(240, 130, 110); var txt = new Color(255, 244, 222); var plate = new Color(14, 11, 18);
				string rows = string.Join(", ", bgs.Select(b => $"{b.Item1} {Contrast(old, b.Item2):0.0}"));
				Mod.Logger.Info($"[Warbands] chat contrast (WCAG, old text colour against the world): {rows}; min {bgs.Min(b => Contrast(old, b.Item2)):0.00}. New text against its plate: {Contrast(txt, plate):0.0}, plate against the same worlds: min {bgs.Min(b => Contrast(plate, b.Item2)):0.0}");
			}
			for (int seed = 0; seed < seeds; seed++)
			{
				int sd = seed; var said = new List<Said>(); int t0 = 0;
				_trials.Add(new Trial
				{
					Name = $"chat {(legacy ? "legacy" : "bubbles")} / {type.Id} / seed {sd}", Max = 5400,
					Setup = () =>
					{
						Main.rand = new Terraria.Utilities.UnifiedRandom(81000 + sd); said.Clear(); BandChatter.ResetTalk(); BandChatter.Log.Lines.Clear(); t0 = (int)Main.GameUpdateCount;
						ArmPlayer(Gold, 700, 0, "DemonBow", "FlamingArrow"); _accuracy = 0.25f; DamageTaken = 0;
						BandChatter.Probe = (npc, text) => said.Add(new Said { Tick = Main.GameUpdateCount, Npc = npc.whoAmI, Band = (npc.ModNPC as BandLevyNPC)?.BandId ?? 0, X = npc.Center.X, Y = npc.Top.Y, Text = text });
						BandSpawner.SpawnBand(Main.player[0], type, size, 24, 30, false, false);
						if (BandSpawner.LastBrain != null) { BandSpawner.LastBrain.Aggro = true; BandSpawner.LastBrain.Warns = false; BandSpawner.LastBrain.Neutral = false; }
					},
					Tick = t => { if (Levies().Count == 0 && t > 60) return true; return false; },
					Result = () =>
					{
						BandChatter.Probe = null;
						int frames2 = 0, framesOv = 0, framesTalk = 0, maxB = 0; double areaSum = 0; int areaN = 0;
						uint end = (uint)(t0 + 5400);
						for (uint tk = (uint)t0; tk < end; tk += 6)
						{
							var vis = said.Where(s => s.Tick <= tk && tk - s.Tick < (legacy ? 210u : (uint)BandBubbles.LifeFor(s.Text))).ToList();
							maxB = Math.Max(maxB, vis.Count);
							if (vis.Count < 2) continue;
							frames2++;
							var rects = new List<Rectangle>(); var bandOf = new List<int>();
							if (legacy)
							{
								foreach (var s in vis) { float age = tk - s.Tick; float w = s.Text.Length * 8.5f, h = 28f; float rise = Math.Min(age, 60f) * 0.6f; rects.Add(new Rectangle((int)(s.X - w * 0.5f), (int)(s.Y - 20f - rise - h * 0.5f), (int)w, (int)h)); bandOf.Add(s.Band); }
							}
							else
							{
								var bubbles = vis.Select(s => new BandBubbles.Bubble { Npc = s.Npc, Band = s.Band, Text = s.Text, Start = s.Tick, Life = BandBubbles.LifeFor(s.Text) }).ToList();
								var posOf = vis.ToDictionary(s => s.Npc, s => new Vector2(s.X, s.Y - 18f));
								BandBubbles.Layout(bubbles, b => { if ((uint)b.Npc < Main.maxNPCs && Main.npc[b.Npc].active) return new Vector2(Main.npc[b.Npc].Center.X, Main.npc[b.Npc].Top.Y - 18f); return posOf[b.Npc]; }, BandBubbles.EstimateMeasure);
								foreach (var b in bubbles) { rects.Add(b.Rect); bandOf.Add(b.Band); }
							}
							bool ov = false;
							for (int i = 0; i < rects.Count; i++)
								for (int j = i + 1; j < rects.Count; j++)
								{
									var r = Rectangle.Intersect(rects[i], rects[j]); if (r.IsEmpty) continue;
									ov = true; double small = Math.Min(rects[i].Width * rects[i].Height, rects[j].Width * rects[j].Height); areaSum += 100.0 * r.Width * r.Height / Math.Max(1.0, small); areaN++;
								}
							if (ov) framesOv++;
							if (vis.Select(s => s.Band).GroupBy(x => x).Any(g => g.Count() >= 2)) framesTalk++;
						}
						double perMin = said.Count / 1.5;
						_tsv.AppendLine($"{(legacy ? "legacy" : "bubbles")}\t{sd}\t{said.Count}\t{maxB}\t{frames2}\t{(frames2 == 0 ? 0 : 100.0 * framesOv / frames2):0.0}\t{(frames2 == 0 ? 0 : 100.0 * framesTalk / frames2):0.0}\t{(areaN == 0 ? 0 : areaSum / areaN):0.0}\t{perMin:0.0}");
						return (said.Count > 0, $"{said.Count} lines, up to {maxB} on screen, {frames2} frames with two or more, overlapping in {(frames2 == 0 ? 0 : 100.0 * framesOv / frames2):0.0}% of them, two of one band at once in {(frames2 == 0 ? 0 : 100.0 * framesTalk / frames2):0.0}%");
					},
					Cleanup = () => { BandChatter.Probe = null; },
				});
			}
		}
	}
}
