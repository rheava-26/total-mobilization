using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// What the army says about the things the commander does in front of it: using a life crystal or fruit (LIFE_CRYSTAL), opening a chest (CHEST_OPENED), and the end of a Solar Eclipse it lived through beside him (ECLIPSE_END).
	/// Server and single player only. The commander's own state is read where the game logic runs (the server sees a player's max life and open chest through the normal player sync),
	/// so nothing is sent from the client and no item hook can miss a crystal used by another mod's item. A levy of that commander within reach speaks a floating line; a few at a time,
	/// one after another, under a cooldown per trigger so looting a whole house is not a running commentary.
	/// The eclipse line fires on the tick <c>Main.eclipse</c> goes from true to false (vanilla ends an eclipse at nightfall; a mod or a command can end it by day), once per ending: the first tick of a world only records
	/// what is going on, so loading into a world is never an ending.
	/// </summary>
	public class CommanderReactions : ModSystem
	{
		private struct Seen { public bool Init; public int LifeMax, Chest, Ticks; }
		private struct Cue { public int Npc, RecordId, At; public DialogueTrigger Trigger; public string Commander; }

		private static readonly Seen[] Last = new Seen[Main.maxPlayers];
		private static readonly List<Cue> Queue = new();
		private static readonly List<SoldierNPC> Scratch = new();
		private const int Never = int.MinValue / 2, StepTicks = 28, SettleTicks = 180;
		private static int _crystalAt = Never, _chestAt = Never;
		private static bool _eclipsePrimed, _eclipseWas;

		/// <summary>Self-check and harness telemetry: reactions started since boot.</summary>
		internal static int CrystalReactions, ChestReactions;
		/// <summary>Self-check telemetry: eclipse endings seen, endings that had an army to react, and eclipse-end lines actually spoken.</summary>
		internal static int EclipseEndings, EclipseEndReactions, EclipseEndLines;

		public override void OnWorldLoad() { Reset(); }
		public override void OnWorldUnload() { Reset(); }

		internal static void Reset()
		{
			Array.Clear(Last);
			Queue.Clear();
			_crystalAt = _chestAt = Never;
			_eclipsePrimed = _eclipseWas = false;
		}

		public override void PostUpdateEverything()
		{
			if (LevyUtil.IsClient || RosterSystem.Instance == null) return;
			try { Tick((int)Main.GameUpdateCount); }
			catch (Exception) { /* a reaction line must never throw into the game loop */ }
		}

		private static void Tick(int now)
		{
			bool eclipse = Main.eclipse;
			if (_eclipsePrimed && _eclipseWas && !eclipse) EclipseEnded(now);
			_eclipseWas = eclipse; _eclipsePrimed = true;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active || p.dead) { Last[i] = default; continue; }
				ref Seen s = ref Last[i];
				if (!s.Init) { s = new Seen { Init = true, LifeMax = p.statLifeMax, Chest = p.chest }; continue; } // a player who has just arrived is not "using a crystal"
				s.Ticks++;
				if (s.Ticks > SettleTicks && p.statLifeMax > s.LifeMax && p.statLifeMax - s.LifeMax <= 20) Crystal(p, now);
				if (s.Ticks > SettleTicks && p.chest >= 0 && p.chest != s.Chest) Chest(p, now);
				s.LifeMax = p.statLifeMax;
				s.Chest = p.chest;
			}
			for (int i = Queue.Count - 1; i >= 0; i--)
			{
				Cue c = Queue[i];
				if (now - c.At < 0) continue;
				Queue.RemoveAt(i);
				if ((uint)c.Npc >= Main.maxNPCs) continue;
				NPC n = Main.npc[c.Npc];
				if (!n.active || n.ModNPC is not SoldierNPC s2 || s2.Record == null || s2.Record.Id != c.RecordId || !s2.IsAlive) continue;
				if (DialogueSystem.React(s2, c.Trigger, new DialogueContext { Player = c.Commander }))
				{
					if (c.Trigger == DialogueTrigger.ECLIPSE_END) EclipseEndLines++;
					else if (c.Trigger == DialogueTrigger.RIFT_PANIC) RiftMood.PanicLines++;
				}
			}
		}

		/// <summary>The eclipse just ended: a few levies near each commander say so. One ending, one round of lines per army.</summary>
		private static void EclipseEnded(int now)
		{
			EclipseEndings++;
			bool any = false;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (!p.active || p.dead) continue;
				if (Enqueue(p, DialogueTrigger.ECLIPSE_END, LevyConstants.EclipseEndSpeakers, now)) any = true;
			}
			if (any) EclipseEndReactions++;
		}

		private static void Crystal(Player p, int now)
		{
			if ((uint)(now - _crystalAt) < LevyConstants.CrystalReactCooldownTicks) return;
			if (Enqueue(p, DialogueTrigger.LIFE_CRYSTAL, LevyConstants.CrystalReactSpeakers, now)) { _crystalAt = now; CrystalReactions++; }
		}

		private static void Chest(Player p, int now)
		{
			if ((uint)(now - _chestAt) < LevyConstants.ChestReactCooldownTicks) return;
			if (Main.rand.NextFloat() >= LevyConstants.ChestReactChance) return;
			int speakers = Main.rand.NextFloat() < 0.35f ? 2 : 1;
			if (Enqueue(p, DialogueTrigger.CHEST_OPENED, speakers, now)) { _chestAt = now; ChestReactions++; }
		}

		/// <summary>Picks up to <paramref name="speakers"/> levies of this commander who are near him and standing, and queues their lines one after another (<paramref name="step"/> ticks apart; the rift mood uses it for the burst after the sun is covered).</summary>
		internal static bool Enqueue(Player p, DialogueTrigger trigger, int speakers, int now, int step = StepTicks)
		{
			Scratch.Clear();
			float reach = LevyConstants.ReactReachTiles * LevyConstants.TilePx;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || !s.IsRegular || s.Record.Reserve || s.Record.CommanderName != p.name) continue;
				if (Vector2.Distance(s.NPC.Center, p.Center) > reach) continue;
				Scratch.Add(s);
			}
			if (Scratch.Count == 0) return false;
			for (int i = Scratch.Count - 1; i > 0; i--) { int j = Main.rand.Next(i + 1); (Scratch[i], Scratch[j]) = (Scratch[j], Scratch[i]); }
			int n = Math.Min(speakers, Scratch.Count);
			for (int k = 0; k < n; k++)
				Queue.Add(new Cue { Npc = Scratch[k].NPC.whoAmI, RecordId = Scratch[k].Record.Id, At = now + 20 + k * step + Main.rand.Next(12), Trigger = trigger, Commander = p.name });
			Scratch.Clear();
			return true;
		}
	}
}
