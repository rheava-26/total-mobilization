using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// Dialogue table, triggers, placeholders, bark budget and delivery channels.
	/// Barks run where the game logic runs (server / single player). Right-click chat runs on the client and works from the
	/// display copy of the record the client holds.
	/// </summary>
	public static partial class DialogueSystem
	{
		// =====================================================================================================
		// Public contract
		// =====================================================================================================

		/// <summary>Deliver a line for a trigger through its default channel, subject to the squad budget.</summary>
		public static void Bark(SoldierNPC s, DialogueTrigger trigger, DialogueContext ctx = null)
		{
			if (s == null || s.Record == null || s.NPC == null || !s.NPC.active) return;
			if (LevyUtil.IsClient) return; // the server decides and tells the clients
			SoldierRecord rec = s.Record;
			if (rec.Kind == SoldierKind.Militia && !MilitiaMayBark(trigger)) return;
			int idx = s.NPC.whoAmI;
			if ((uint)idx >= MaxBodies) return;
			int now = Now;

			switch (ChannelOf(trigger, out int emote))
			{
				case DialogueChannel.ChatLog:
					ChatLog(trigger, rec, ctx);
					break;

				case DialogueChannel.ChatWindow:
					// Militia cannot be spoken to, so their event lines are shouted instead of remembered.
					if (rec.Kind == SoldierKind.Militia) BarkFloating(s, rec, trigger, ctx, now);
					else RememberForChat(s, rec, trigger, ctx, idx, now);
					break;

				case DialogueChannel.Emote:
					BarkEmote(s, rec, trigger, ctx, emote, idx, now);
					break;

				case DialogueChannel.FloatingText:
					if (trigger == DialogueTrigger.ROUTED) TryEmote(s, EmoteID.EmoteFear, idx, now);
					else if (trigger == DialogueTrigger.RALLIED) TryEmote(s, EmoteID.EmoteHappiness, idx, now);
					BarkFloating(s, rec, trigger, ctx, now);
					break;
			}
		}

		/// <summary>A filled line for a trigger (or null if none). Used for chat windows and death messages.</summary>
		public static string PickLine(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx = null)
		{
			return Pick(trigger, rec, ctx, null, false, -1, out _);
		}

		/// <summary>Right-click chat text for a soldier. Never null or empty.</summary>
		public static string ChatLine(SoldierNPC s)
		{
			SoldierRecord rec = s?.Record;
			if (rec == null || s.NPC == null) return Localized("Silence", "...");
			int idx = s.NPC.whoAmI;
			bool tracked = (uint)idx < MaxBodies;
			int now = Now;
			// Do not hand the same soldier the same line twice in a row, even when the whole pool is cooling down.
			int avoid = tracked && Within(LastChatTick[idx], now, LevyConstants.IdleChatCooldownTicks) ? LastChatLine[idx] : -1;

			string text = null;

			// 1. the event in progress
			Span<DialogueTrigger> events = stackalloc DialogueTrigger[8];
			int eventCount = ActiveEvents(events);
			for (int i = 0; i < eventCount && text == null; i++) text = ChatPick(events[i], s, rec, idx, tracked, avoid, now, false);

			// 2. a payday / unpaid / hold-fire line from the last two minutes
			if (text == null && tracked) text = TakeRecent(idx, rec, now);

			// 3. a name-drop
			if (text == null && Main.rand.NextFloat() < LevyConstants.DialogueNamedropChance)
				text = ChatPick(DialogueTrigger.NAMEDROP, s, rec, idx, tracked, avoid, now, false);

			// 4. the biome or situation of the local player
			if (text == null)
			{
				Span<DialogueTrigger> situations = stackalloc DialogueTrigger[16];
				int count = LocalSituations(situations);
				for (int i = 0; i < count && text == null; i++)
					if (Main.rand.NextFloat() < LevyConstants.DialogueSituationChance)
						text = ChatPick(situations[i], s, rec, idx, tracked, avoid, now, false);
			}

			// 5. plain idle talk; 6. the player asked, so repeat rather than go quiet
			if (text == null) text = ChatPick(DialogueTrigger.IDLE, s, rec, idx, tracked, avoid, now, false);
			if (text == null) text = ChatPick(Main.dayTime ? DialogueTrigger.IDLE : DialogueTrigger.IDLE_NIGHT, s, rec, idx, tracked, avoid, now, true);
			if (text == null) text = ChatPick(DialogueTrigger.IDLE, s, rec, idx, tracked, avoid, now, true);
			if (text == null) text = ChatPick(DialogueTrigger.IDLE, s, rec, idx, tracked, -1, now, true);

			return string.IsNullOrEmpty(text) ? Localized("Silence", "...") : text;
		}

		/// <summary>
		/// Chat-log notice (muster, deaths, desertions...). No budget. Server and single player only: the server broadcasts.
		/// A Regular's death message obeys LevyConfig.RegularDeathMessages; a militia death message is only posted in Individual mode.
		/// </summary>
		public static void ChatLog(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx = null)
		{
			if (LevyUtil.IsClient) return;
			if (trigger == DialogueTrigger.DEATH_MESSAGE && rec != null)
			{
				LevyConfig cfg = LevyConfig.Get;
				if (cfg != null)
				{
					if (rec.Kind == SoldierKind.Militia ? cfg.MilitiaDeathMessages != MilitiaDeathMessageMode.Individual : !cfg.RegularDeathMessages) return;
				}
			}
			string line = PickLine(trigger, rec, ctx);
			if (string.IsNullOrEmpty(line)) return;
			// Deaths use the vanilla death-message red; everything else the mod's gold (LevyUtil's default).
			LevyUtil.Chat(line, trigger == DialogueTrigger.DEATH_MESSAGE ? new Color(225, 25, 25) : null);
		}

		/// <summary>Show an emote bubble over a soldier (server / single player; NewBubble synchronises itself to clients). No budget.</summary>
		public static void Emote(SoldierNPC s, int emoteId)
		{
			if (s == null || s.NPC == null || !s.NPC.active) return;
			if (Main.netMode == NetmodeID.MultiplayerClient) return; // EmoteBubble.NewBubble does nothing on a client
			EmoteBubble.NewBubble(emoteId, new WorldUIAnchor(s.NPC), LevyConstants.DialogueEmoteBubbleTicks);
		}

		/// <summary>Show floating text over a soldier: in single player directly, on a server through the Bark packet. No budget.</summary>
		public static void FloatingText(SoldierNPC s, string text) => DeliverFloating(s, text, byte.MaxValue);

		/// <summary>As <see cref="FloatingText(SoldierNPC, string)"/>, coloured for the trigger that produced the line.</summary>
		public static void FloatingText(SoldierNPC s, string text, DialogueTrigger trigger) => DeliverFloating(s, text, (byte)trigger);

		/// <summary>Random soldier name from vanilla town-NPC name lists.</summary>
		public static string RandomSoldierName(bool male) => DialogueNames.RandomSoldierName(male);

		/// <summary>Random settlement name.</summary>
		public static string RandomTownName() => DialogueNames.RandomTownName();

		// =====================================================================================================
		// Channels
		// =====================================================================================================

		/// <summary>The channel a trigger is delivered through, and the emote it shows (-1 when it has none).</summary>
		public static DialogueChannel ChannelOf(DialogueTrigger trigger, out int emoteId)
		{
			emoteId = -1;
			switch (trigger)
			{
				case DialogueTrigger.ENGAGE: emoteId = EmoteID.EmoteFight; return DialogueChannel.Emote;
				case DialogueTrigger.KILL: emoteId = EmoteID.EmoteHappiness; return DialogueChannel.Emote;
				case DialogueTrigger.HURT: emoteId = EmoteID.EmotionAnger; return DialogueChannel.Emote;
				case DialogueTrigger.LOW_HP: emoteId = EmoteID.EmotionCry; return DialogueChannel.Emote;
				case DialogueTrigger.OUT_OF_AMMO: emoteId = EmoteID.EmoteConfused; return DialogueChannel.Emote;
				case DialogueTrigger.SHAKEN: emoteId = EmoteID.EmoteFear; return DialogueChannel.Emote;
				case DialogueTrigger.COMRADE_HIT: emoteId = EmoteID.EmotionAlert; return DialogueChannel.Emote;
				case DialogueTrigger.MUSTER: emoteId = EmoteID.EmoteFight; return DialogueChannel.Emote;
				case DialogueTrigger.STAND_DOWN: emoteId = EmoteID.EmoteSleep; return DialogueChannel.Emote;
				case DialogueTrigger.PROMOTED:
				case DialogueTrigger.VETERAN: emoteId = EmoteID.EmoteHappiness; return DialogueChannel.Emote;

				case DialogueTrigger.BOSS_SIGHTED:
				case DialogueTrigger.BOSS:
				case DialogueTrigger.COMRADE_DOWN:
				case DialogueTrigger.ROUTED:
				case DialogueTrigger.RALLIED:
				case DialogueTrigger.VICTORY: return DialogueChannel.FloatingText;

				case DialogueTrigger.DEATH_MESSAGE:
				case DialogueTrigger.DESERTED:
				case DialogueTrigger.NO_ONE_LEFT: return DialogueChannel.ChatLog;

				default: return DialogueChannel.ChatWindow; // IDLE, IDLE_NIGHT, NAMEDROP, BLOOD_MOON, PARTY, GRAVEYARD, RAIN, BIOME_*, PAYDAY, UNPAID, HOLD_FIRE, EVENT_*
			}
		}

		/// <summary>The triggers a militia soldier may use.</summary>
		public static bool MilitiaMayBark(DialogueTrigger trigger)
		{
			switch (trigger)
			{
				case DialogueTrigger.ENGAGE: case DialogueTrigger.KILL: case DialogueTrigger.HURT: case DialogueTrigger.LOW_HP:
				case DialogueTrigger.OUT_OF_AMMO: case DialogueTrigger.COMRADE_HIT: case DialogueTrigger.COMRADE_DOWN: case DialogueTrigger.SHAKEN:
				case DialogueTrigger.ROUTED: case DialogueTrigger.RALLIED: case DialogueTrigger.MUSTER: case DialogueTrigger.STAND_DOWN:
				case DialogueTrigger.EVENT_GOBLINS: case DialogueTrigger.EVENT_PIRATES: case DialogueTrigger.EVENT_ECLIPSE: case DialogueTrigger.EVENT_PUMPKIN:
				case DialogueTrigger.EVENT_FROST: case DialogueTrigger.EVENT_MARTIANS: case DialogueTrigger.EVENT_OLDONES: case DialogueTrigger.EVENT_FROSTLEGION:
				case DialogueTrigger.BOSS_SIGHTED: case DialogueTrigger.BOSS: case DialogueTrigger.DEATH_MESSAGE:
					return true;
				default:
					return false;
			}
		}

		/// <summary>Emote budget: one per soldier per EmotePerSoldierTicks.</summary>
		private static bool TryEmote(SoldierNPC s, int emoteId, int idx, int now)
		{
			if (Within(LastEmote[idx], now, LevyConstants.EmotePerSoldierTicks)) return false;
			LastEmote[idx] = now;
			Emote(s, emoteId);
			return true;
		}

		/// <summary>Combat chatter: an emote bubble, plus the line as a small floating text when the squad budget allows.</summary>
		private static void BarkEmote(SoldierNPC s, SoldierRecord rec, DialogueTrigger trigger, DialogueContext ctx, int emote, int idx, int now)
		{
			if (!TryEmote(s, emote, idx, now)) return; // the whole bark waits for the soldier's emote budget
			if (!LevyConstants.DialogueChatterText) return;
			SquadClock clock = Squad(rec);
			if (Within(clock.Any, now, LevyConstants.FloatingLineIntervalTicks)) return;
			string line = Pick(trigger, rec, ctx, s, false, -1, out _);
			if (line == null) return;
			clock.Any = now;
			DeliverFloating(s, line, (byte)trigger);
		}

		/// <summary>
		/// Big moments: floating text, one per squad per FloatingLineIntervalTicks. They only compete with each other.
		/// A boss-specific BOSS line outranks the generic BOSS_SIGHTED fired just before it: BOSS passes through a clock that only
		/// BOSS_SIGHTED set, so the pair (sighting, then the boss's own line) both show. In the other order the sighting is dropped.
		/// </summary>
		private static void BarkFloating(SoldierNPC s, SoldierRecord rec, DialogueTrigger trigger, DialogueContext ctx, int now)
		{
			SquadClock clock = Squad(rec);
			bool blocked = Within(clock.Big, now, LevyConstants.FloatingLineIntervalTicks);
			if (blocked && trigger == DialogueTrigger.BOSS && clock.BigWasSighted) blocked = false;
			if (blocked) return;
			string line = Pick(trigger, rec, ctx, s, false, -1, out _);
			if (line == null) return;
			clock.Big = clock.Any = now;
			clock.BigWasSighted = trigger == DialogueTrigger.BOSS_SIGHTED;
			DeliverFloating(s, line, (byte)trigger);
		}

		/// <summary>Chat-window triggers: keep the line so the next right-click can use it, and tell the clients who will do the chatting.</summary>
		private static void RememberForChat(SoldierNPC s, SoldierRecord rec, DialogueTrigger trigger, DialogueContext ctx, int idx, int now)
		{
			// Prefer a line nobody said lately, but fall back to a repeat: the cooldown only starts when the line is actually shown (TakeRecent).
			string line = Pick(trigger, rec, ctx, s, false, -1, out DialogueLine picked, mark: false)
				?? Pick(trigger, rec, ctx, s, true, -1, out picked, mark: false);
			if (line == null) return;
			SetRecent(idx, trigger, line, rec.Id, now, picked?.Id ?? -1);
			if (Main.netMode != NetmodeID.Server) return;
			ModPacket p = LevyPackets.New(LevyPacket.Bark);
			p.Write((short)idx);
			p.Write((byte)DialogueChannel.ChatWindow);
			p.Write((byte)trigger);
			p.Write(line);
			p.Send();
		}

		private static void SetRecent(int idx, DialogueTrigger trigger, string line, int recordId, int now, int lineId = -1)
		{
			Recent[idx] = new RecentLine { Valid = true, Trigger = trigger, Line = line, Tick = now, RecordId = recordId, LineId = lineId };
		}

		private static string TakeRecent(int idx, SoldierRecord rec, int now)
		{
			RecentLine r = Recent[idx];
			if (!r.Valid) return null;
			if (r.Trigger != DialogueTrigger.PAYDAY && r.Trigger != DialogueTrigger.UNPAID && r.Trigger != DialogueTrigger.HOLD_FIRE) return null;
			Recent[idx].Valid = false; // said once
			if (r.RecordId != rec.Id || !Within(r.Tick, now, LevyConstants.DialogueRecentLineTicks)) return null;
			if (r.LineId >= 0) LineUsed[r.LineId] = now; // shown now, so the two-minute repeat cooldown starts now
			return r.Line;
		}

		private static string ChatPick(DialogueTrigger trigger, SoldierNPC s, SoldierRecord rec, int idx, bool tracked, int avoid, int now, bool ignoreRepeat)
		{
			string text = Pick(trigger, rec, null, s, ignoreRepeat, avoid, out DialogueLine line);
			if (text != null && tracked && line != null) { LastChatLine[idx] = line.Id; LastChatTick[idx] = now; }
			return text;
		}

		/// <summary>The events in progress, in priority order. Returns how many were written.</summary>
		private static int ActiveEvents(Span<DialogueTrigger> into)
		{
			int n = 0;
			if (Main.invasionType > 0 && Main.invasionSize > 0)
			{
				switch (Main.invasionType)
				{
					case InvasionID.GoblinArmy: into[n++] = DialogueTrigger.EVENT_GOBLINS; break;
					case InvasionID.SnowLegion: into[n++] = DialogueTrigger.EVENT_FROSTLEGION; break;
					case InvasionID.PirateInvasion: into[n++] = DialogueTrigger.EVENT_PIRATES; break;
					case InvasionID.MartianMadness: into[n++] = DialogueTrigger.EVENT_MARTIANS; break;
				}
			}
			if (Main.eclipse) into[n++] = DialogueTrigger.EVENT_ECLIPSE;
			if (Main.pumpkinMoon) into[n++] = DialogueTrigger.EVENT_PUMPKIN;
			if (Main.snowMoon) into[n++] = DialogueTrigger.EVENT_FROST;
			if (DD2Event.Ongoing) into[n++] = DialogueTrigger.EVENT_OLDONES;
			if (Main.bloodMoon) into[n++] = DialogueTrigger.BLOOD_MOON;
			return n;
		}

		/// <summary>The biomes and situations of the local player, in priority order. Returns how many were written.</summary>
		private static int LocalSituations(Span<DialogueTrigger> into)
		{
			int n = 0;
			Player p = Main.LocalPlayer;
			if (p != null && p.active)
			{
				if (p.ZoneDungeon) into[n++] = DialogueTrigger.BIOME_DUNGEON;
				if (p.ZoneUnderworldHeight) into[n++] = DialogueTrigger.BIOME_UNDERWORLD;
				if (p.ZoneJungle) into[n++] = DialogueTrigger.BIOME_JUNGLE;
				if (p.ZoneSnow) into[n++] = DialogueTrigger.BIOME_SNOW;
				if (p.ZoneCorrupt) into[n++] = DialogueTrigger.BIOME_CORRUPTION;
				if (p.ZoneCrimson) into[n++] = DialogueTrigger.BIOME_CRIMSON;
				if (p.ZoneHallow) into[n++] = DialogueTrigger.BIOME_HALLOW;
				if (p.ZoneBeach) into[n++] = DialogueTrigger.BIOME_OCEAN;
				if (p.ZoneRockLayerHeight || p.ZoneDirtLayerHeight) into[n++] = DialogueTrigger.BIOME_UNDERGROUND;
				if (BirthdayParty.PartyIsUp) into[n++] = DialogueTrigger.PARTY;
				if (p.ZoneGraveyard) into[n++] = DialogueTrigger.GRAVEYARD;
			}
			if (Main.raining) into[n++] = DialogueTrigger.RAIN;
			if (!Main.dayTime) into[n++] = DialogueTrigger.IDLE_NIGHT;
			return n;
		}

		// =====================================================================================================
		// Floating text and the Bark packet
		// =====================================================================================================

		private static void DeliverFloating(SoldierNPC s, string text, byte triggerByte)
		{
			if (s == null || s.NPC == null || !s.NPC.active || string.IsNullOrEmpty(text)) return;
			if (Main.netMode == NetmodeID.Server)
			{
				int idx = s.NPC.whoAmI;
				if ((uint)idx >= MaxBodies) return;
				ModPacket packet = null;
				float rangeSq = LevyConstants.DialogueFloatingRangePx * LevyConstants.DialogueFloatingRangePx;
				for (int i = 0; i < Main.maxPlayers; i++)
				{
					Player pl = Main.player[i];
					if (!pl.active || Vector2.DistanceSquared(pl.Center, s.NPC.Center) > rangeSq) continue;
					if (!Netplay.Clients[i].IsConnected()) continue; // Send(toClient) dereferences the socket
					if (packet == null)
					{
						packet = LevyPackets.New(LevyPacket.Bark);
						packet.Write((short)idx);
						packet.Write((byte)DialogueChannel.FloatingText);
						packet.Write(triggerByte);
						packet.Write(text);
					}
					packet.Send(toClient: i);
				}
			}
			else
			{
				SpawnFloating(s.NPC.whoAmI, s.NPC.Hitbox, text, triggerByte);
			}
		}

		/// <summary>Client side of the Bark packet: short npcIndex, byte channel, byte emoteId (carries the trigger), string text.</summary>
		internal static void HandleBarkPacket(BinaryReader reader, int fromWhoAmI)
		{
			short npcIndex = reader.ReadInt16();
			DialogueChannel channel = (DialogueChannel)reader.ReadByte();
			byte extra = reader.ReadByte();
			string text = reader.ReadString();
			if (Main.netMode != NetmodeID.MultiplayerClient) return;
			if (npcIndex < 0 || npcIndex >= Main.maxNPCs || npcIndex >= MaxBodies) return;
			NPC npc = Main.npc[npcIndex];
			if (!npc.active) return;

			switch (channel)
			{
				case DialogueChannel.FloatingText:
					SpawnFloating(npcIndex, npc.Hitbox, text, extra);
					break;

				case DialogueChannel.ChatWindow:
					if (extra < DialogueTable.TriggerNames.Length)
					{
						int recordId = (npc.ModNPC as SoldierNPC)?.Record?.Id ?? -1;
						SetRecent(npcIndex, (DialogueTrigger)extra, text, recordId, Now);
					}
					break;

				// Emote: EmoteBubble.NewBubble synchronises itself from the server (MessageID 91), so no packet is sent for it.
				// ChatLog: the server broadcasts chat itself.
			}
		}

		/// <summary>
		/// Draws a line as combat text above a body. Client and single player only; skipped when the speaker is off screen.
		/// A second line from the same speaker stacks above the first until the first has risen out of the way.
		/// </summary>
		private static void SpawnFloating(int npcIndex, Rectangle hitbox, string text, byte triggerByte)
		{
			if (Main.netMode == NetmodeID.Server || Main.dedServ) return;
			if (string.IsNullOrEmpty(text) || !LevyUtil.IsOnScreen(hitbox)) return;
			Color color = FloatColor(triggerByte);
			string[] rows = Wrap(text, LevyConstants.DialogueFloatingRowChars);
			var font = FontAssets.CombatText[0].Value;
			float rowHeight = font.MeasureString("Ag").Y * 0.9f;

			bool tracked = (uint)npcIndex < MaxBodies;
			int now = Now;
			float lift = 0f;
			if (tracked && Within(FloatStackTick[npcIndex], now, LevyConstants.DialogueFloatingLifeTicks))
			{
				// How far the previous line has already risen: vanilla combat text moves 7 px a tick, decaying by 0.92.
				float risen = LevyConstants.DialogueFloatingRisePx * (1f - MathF.Pow(LevyConstants.DialogueFloatingRiseDecay, now - FloatStackTick[npcIndex]));
				lift = Math.Min(Math.Max(0f, FloatStackPx[npcIndex] - risen), LevyConstants.DialogueFloatingMaxStackRows * rowHeight);
			}

			for (int k = 0; k < rows.Length; k++)
			{
				int i = CombatText.NewText(hitbox, color, rows[k]);
				if (i < 0 || i >= Main.combatText.Length) continue; // all 100 slots busy
				CombatText ct = Main.combatText[i];
				if (!ct.active) continue;
				Vector2 size = font.MeasureString(rows[k]);
				// Rows stack above the head, centred; keep the text up long enough to read a sentence.
				ct.position = new Vector2(hitbox.Center.X - size.X * 0.5f, hitbox.Top - 10f - lift - (rows.Length - k) * rowHeight);
				ct.lifeTime = LevyConstants.DialogueFloatingLifeTicks;
			}
			if (tracked)
			{
				FloatStackTick[npcIndex] = now;
				FloatStackPx[npcIndex] = lift + rows.Length * rowHeight;
			}
		}

		private static Color FloatColor(byte triggerByte)
		{
			if (triggerByte == byte.MaxValue) return new Color(255, 255, 255);
			switch ((DialogueTrigger)triggerByte)
			{
				case DialogueTrigger.BOSS_SIGHTED:
				case DialogueTrigger.BOSS: return new Color(255, 170, 70);
				case DialogueTrigger.COMRADE_DOWN: return new Color(255, 110, 110);
				case DialogueTrigger.MUTINY: return new Color(255, 60, 50);
				case DialogueTrigger.ROUTED: return new Color(255, 225, 90);
				case DialogueTrigger.RALLIED: return new Color(150, 255, 150);
				case DialogueTrigger.VICTORY: return new Color(255, 235, 120);
				case DialogueTrigger.EVENT_GOBLINS: case DialogueTrigger.EVENT_PIRATES: case DialogueTrigger.EVENT_ECLIPSE: case DialogueTrigger.EVENT_PUMPKIN:
				case DialogueTrigger.EVENT_FROST: case DialogueTrigger.EVENT_MARTIANS: case DialogueTrigger.EVENT_OLDONES: case DialogueTrigger.EVENT_FROSTLEGION:
					return new Color(255, 240, 200);
				default: return new Color(235, 235, 235);
			}
		}

		/// <summary>Word-wraps to at most three rows; the last row takes whatever is left.</summary>
		private static string[] Wrap(string text, int maxChars)
		{
			if (text.Length <= maxChars) return new[] { text };
			var rows = new System.Collections.Generic.List<string>(3);
			int start = 0;
			while (start < text.Length)
			{
				int remain = text.Length - start;
				if (remain <= maxChars || rows.Count == 2) { rows.Add(text.Substring(start).Trim()); break; }
				int cut = text.LastIndexOf(' ', start + maxChars, maxChars);
				if (cut <= start) cut = text.IndexOf(' ', start + maxChars);
				if (cut < 0) { rows.Add(text.Substring(start).Trim()); break; }
				rows.Add(text.Substring(start, cut - start).Trim());
				start = cut + 1;
			}
			return rows.ToArray();
		}
	}
}
