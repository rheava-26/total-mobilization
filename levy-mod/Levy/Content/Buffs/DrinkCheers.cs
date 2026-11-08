using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Levy.Common;
using Terraria.ModLoader;
using Levy.Common.Dialogue;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// The celebration when a round is served (server and single player; clients only see the synced emote bubbles and chat bars).
	/// Every levy that receives the round shows a cheer emote within a second; a handful of them also speak a floating DRINK line, one after another so the screen
	/// is never a wall of text. For the rest of the buff an occasional hiccup bubble goes up over one drunk levy of the army. The hop, the raised arm and the sway are draw-only
	/// (SoldierNPC.Drunk.cs), driven by the buff itself.
	/// </summary>
	public class DrinkCheers : ModSystem
	{
		private struct Cue { public int Npc, RecordId, At; public bool Speak; public string Text; }

		private const int MaxSpeakers = 5, SpeakFirstTicks = 30, SpeakStepTicks = 18, HiccupEveryTicks = 45;
		private static readonly List<Cue> Queue = new();
		private static readonly List<SoldierNPC> Scratch = new();

		/// <summary>Ticks after the round at which a levy's cheer starts (0..60). Shared with the draw so the hop lines up with the bubble.</summary>
		public static int CheerDelay(int npcIndex) => npcIndex * 29 % 61;

		public override void OnWorldLoad() => Queue.Clear();
		public override void OnWorldUnload() => Queue.Clear();

		/// <summary>Queues the cheers and lines for the levies a round just reached.</summary>
		public static void Begin(List<SoldierNPC> served, string commander)
		{
			if (LevyUtil.IsClient || served == null || served.Count == 0) return;
			int now = (int)Main.GameUpdateCount;
			int speakers = Math.Min(MaxSpeakers, 1 + served.Count / 3);
			// a random set of speakers, each in its own slot in the order drawn
			var order = new List<int>(served.Count);
			for (int i = 0; i < served.Count; i++) order.Add(i);
			for (int i = order.Count - 1; i > 0; i--) { int j = Main.rand.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
			var slot = new Dictionary<int, int>(speakers);
			for (int k = 0; k < speakers; k++) slot[order[k]] = k;

			for (int i = 0; i < served.Count; i++)
			{
				SoldierNPC s = served[i];
				int id = s.Record.Id;
				Queue.Add(new Cue { Npc = s.NPC.whoAmI, RecordId = id, At = now + CheerDelay(s.NPC.whoAmI) });
				if (!slot.TryGetValue(i, out int k)) continue;
				string mate = null;
				if (served.Count > 1) { int o = Main.rand.Next(served.Count - 1); if (o >= i) o++; mate = served[o].Record.Name; }
				var ctx = new DialogueContext { Player = commander, Comrade = mate };
				string text = DialogueSystem.PickLine(DialogueTrigger.DRINK, s.Record, ctx) ?? DialogueSystem.PickLine("DRINK", s.Record, ctx, true);
				if (string.IsNullOrEmpty(text)) continue;
				Queue.Add(new Cue { Npc = s.NPC.whoAmI, RecordId = id, At = now + SpeakFirstTicks + k * SpeakStepTicks + Main.rand.Next(10), Speak = true, Text = text });
			}
		}

		public override void PostUpdateEverything()
		{
			if (LevyUtil.IsClient) return;
			int now = (int)Main.GameUpdateCount;
			for (int i = Queue.Count - 1; i >= 0; i--)
			{
				Cue c = Queue[i];
				if (now - c.At < 0) continue;
				Queue.RemoveAt(i);
				SoldierNPC s = Body(c.Npc, c.RecordId);
				if (s == null) continue;
				if (c.Speak) DialogueSystem.FloatingText(s, c.Text, DialogueTrigger.DRINK);
				else DialogueSystem.Emote(s, CheerEmote());
			}
			if (now % HiccupEveryTicks == 0 && Main.rand.NextBool() && RosterSystem.Instance != null) Hiccup();
		}

		/// <summary>One drunk levy of the army, chosen evenly, shows a stumble or hiccup bubble.</summary>
		private static void Hiccup()
		{
			Scratch.Clear();
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies()) if (s.IsAlive && DrinkBuffs.IsDrunk(s.NPC)) Scratch.Add(s);
			if (Scratch.Count == 0) return;
			SoldierNPC pick = Scratch[Main.rand.Next(Scratch.Count)];
			Scratch.Clear();
			int[] bubbles = { EmoteID.EmoteConfused, EmoteID.EmoteSilly, EmoteID.EmoteLaugh, EmoteID.EmoteSleep };
			DialogueSystem.Emote(pick, bubbles[Main.rand.Next(bubbles.Length)]);
		}

		private static int CheerEmote()
		{
			int r = Main.rand.Next(4);
			return r < 2 ? EmoteID.ItemBeer : r == 2 ? EmoteID.EmoteHappiness : EmoteID.EmoteLaugh;
		}

		private static SoldierNPC Body(int npcIndex, int recordId)
		{
			if ((uint)npcIndex >= Main.maxNPCs) return null;
			NPC n = Main.npc[npcIndex];
			return n.active && n.ModNPC is SoldierNPC s && s.Record != null && s.Record.Id == recordId && s.IsAlive ? s : null;
		}
	}
}
