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

		/// <summary>Floating lines that went out (after the rift mood's thinning). Self-check and world-check telemetry.</summary>
		internal static int FloatingDelivered;

		/// <summary>Deliver a line for a trigger through its default channel, subject to the squad budget.</summary>
		public static void Bark(SoldierNPC s, DialogueTrigger trigger, DialogueContext ctx = null)
		{
			if (s == null || s.Record == null || s.NPC == null || !s.NPC.active || s.Hostile) return; // a hostile body speaks through its band, not the levy table
			if (LevyUtil.IsClient) return; // the server decides and tells the clients
			if (global::Levy.Common.Death.NoxusFights.Hush && ChannelOf(trigger, out _) != DialogueChannel.ChatLog && !global::Levy.Common.Compat.Wotg.AoeVoice.Allows(trigger)) { global::Levy.Common.Death.NoxusFights.HushDropped++; return; } // silent through the fight, but for the plain combat callouts while the Avatar of Emptiness is up (Compat/Wotg/AoeVoice.cs)
			SoldierRecord rec = s.Record;
			if (rec.Kind == SoldierKind.Militia && !MilitiaMayBark(trigger)) return;
			int idx = s.NPC.whoAmI;
			if ((uint)idx >= MaxBodies) return;
			int now = Now;
			DialogueChannel channel = ChannelOf(trigger, out int emote);
			if (channel != DialogueChannel.ChatLog && RiftMood.Hushed) return; // the sun is being covered: not a sound

			switch (channel)
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

		/// <summary>A filled line from an explicit string-keyed pool (e.g. "OFFICER_Nurse_FIGHT"), or null when the pool is missing or nothing in it fits. Same filter, placeholder and repeat rules as the trigger overload.</summary>
		public static string PickLine(string poolKey, SoldierRecord rec, DialogueContext ctx = null, bool ignoreRepeat = false)
		{
			// A trigger's own pool asked for by name (the drink fallback, the mutineers) goes through the rift routing like the trigger itself.
			if (RiftMood.Phase != RiftPhase.None && poolKey != null && Enum.TryParse(poolKey, true, out DialogueTrigger named) && Enum.IsDefined(typeof(DialogueTrigger), named)
				&& RiftMood.Route(named) != RiftRoute.Normal)
				return Pick(named, rec, ctx, null, ignoreRepeat, -1, out _);
			return PickFromPool(DialogueTable.Get(poolKey), rec, ctx, null, ignoreRepeat, -1, out _, true, poolKey != null && poolKey.StartsWith("MUTINY", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>A line from a trigger's pool in the dialogue file (repeats allowed), or the localization key's text when the file has none. Used by the mutineers' shouts.</summary>
		public static string LineOrKey(DialogueTrigger trigger, SoldierRecord rec, string fallbackKey)
		{
			return PickLine(trigger.ToString(), rec, null, true) ?? LevyUtil.T(fallbackKey);
		}

		/// <summary>Right-click chat text for a soldier. Never null or empty.</summary>
		public static string ChatLine(SoldierNPC s)
		{
			SoldierRecord rec = s?.Record;
			if (rec == null || s.NPC == null) return Localized("Silence", "...");
			if (global::Levy.Common.Death.NoxusFights.Hush) return WotgScenes.ChatLine(s) ?? Localized("Silence", "..."); // in a Wrath of the Gods fight: a line about the fight, never camp talk
			int idx = s.NPC.whoAmI;
			bool tracked = (uint)idx < MaxBodies;
			int now = Now;
			// Do not hand the same soldier the same line twice in a row, even when the whole pool is cooling down.
			int avoid = tracked && Within(LastChatTick[idx], now, LevyConstants.IdleChatCooldownTicks) ? LastChatLine[idx] : -1;

			string text = null;

			// 1. the event in progress
			Span<DialogueTrigger> events = stackalloc DialogueTrigger[8];
			int eventCount = ActiveEvents(events);
			bool grumpy = RosterFacts.Grumpy(rec);
			for (int i = 0; i < eventCount && text == null; i++)
			{
				if (grumpy && events[i] == DialogueTrigger.BLOOD_MOON && Main.rand.NextBool()) continue; // the rest of her annoyed chat should be heard too
				text = ChatPick(events[i], s, rec, idx, tracked, avoid, now, false);
			}

			// 2. a payday / unpaid / hold-fire line from the last two minutes
			if (text == null && tracked) text = TakeRecent(idx, rec, now);

			// 2b. where his kit ranks in the army (the best-equipped, the worst, or anyone well below the average)
			if (text == null && Main.rand.NextFloat() < LevyConstants.DialogueArmoryChance)
			{
				DialogueTrigger armory = ArmoryTriggerFor(rec);
				if (armory != DialogueTrigger.IDLE) text = ChatPick(armory, s, rec, idx, tracked, avoid, now, false);
			}

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

		/// <summary>The armory chat trigger for where a levy's kit ranks in his army, or IDLE (meaning none) when it is nothing special.</summary>
		internal static DialogueTrigger ArmoryTriggerFor(SoldierRecord rec)
		{
			switch (RosterFacts.KitStanding(rec))
			{
				case RosterFacts.KitRank.Best: return DialogueTrigger.ARMORY_BEST;
				case RosterFacts.KitRank.Worst: return DialogueTrigger.ARMORY_WORST;
				case RosterFacts.KitRank.Below: return DialogueTrigger.ARMORY_WANT;
				default: return DialogueTrigger.IDLE;
			}
		}

		/// <summary>
		/// A reaction line over a levy, outside the squad budget: the caller (commander reactions, a gear hand-out) owns the throttle. Server and single player; returns true when a line was shown.
		/// </summary>
		public static bool React(SoldierNPC s, DialogueTrigger trigger, DialogueContext ctx = null)
		{
			if (s == null || s.Record == null || s.NPC == null || !s.NPC.active || s.Hostile || LevyUtil.IsClient) return false;
			if (global::Levy.Common.Death.NoxusFights.Hush) { global::Levy.Common.Death.NoxusFights.HushDropped++; return false; }
			string line = Pick(trigger, s.Record, ctx, s, false, -1, out _);
			if (string.IsNullOrEmpty(line)) return false;
			DeliverFloating(s, line, (byte)trigger);
			return true;
		}

		/// <summary>
		/// Chat-log notice (muster, deaths, desertions...). No budget. Server and single player only: the server broadcasts.
		/// A Regular's death message obeys LevyConfig.RegularDeathMessages; a militia death message is only posted in Individual mode.
		/// </summary>
		public static void ChatLog(DialogueTrigger trigger, SoldierRecord rec, DialogueContext ctx = null, bool force = false)
		{
			if (LevyUtil.IsClient) return;
			if (trigger == DialogueTrigger.DEATH_MESSAGE && rec != null && !force)
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
			if (trigger == DialogueTrigger.DEATH_MESSAGE) global::Levy.Common.Death.LevyDeathPost.Note(line);
			LevyUtil.Chat(line, trigger == DialogueTrigger.DEATH_MESSAGE ? new Color(225, 25, 25) : null);
		}

		/// <summary>Show an emote bubble over a soldier (server / single player; NewBubble synchronises itself to clients). No budget.</summary>
		public static void Emote(SoldierNPC s, int emoteId)
		{
			if (s == null || s.NPC == null || !s.NPC.active) return;
			if (global::Levy.Common.Death.NoxusFights.Hush) { global::Levy.Common.Death.NoxusFights.HushDropped++; return; }
			if (Main.netMode == NetmodeID.MultiplayerClient) return; // EmoteBubble.NewBubble does nothing on a client
			if (RiftMood.Hushed) return;
			EmoteBubble.NewBubble(emoteId, new WorldUIAnchor(s.NPC), LevyConstants.DialogueEmoteBubbleTicks);
		}

		/// <summary>Show floating text over a soldier: in single player directly, on a server through the Bark packet. No budget.</summary>
		public static void FloatingText(SoldierNPC s, string text) { if (HushHolds()) return; DeliverFloating(s, text, byte.MaxValue); }
		private static bool HushHolds() { if (!global::Levy.Common.Death.NoxusFights.Hush) return false; global::Levy.Common.Death.NoxusFights.HushDropped++; return true; }

		/// <summary>As <see cref="FloatingText(SoldierNPC, string)"/>, coloured for the trigger that produced the line.</summary>
		public static void FloatingText(SoldierNPC s, string text, DialogueTrigger trigger) { if (HushHolds()) return; DeliverFloating(s, text, (byte)trigger); }

		/// <summary>
		/// A scripted reaction in a Wrath of the Gods fight (<see cref="WotgScenes"/>): a line from a SCENE_ pool over the levy, delivered like any floating line (a Bark packet to the clients in reach).
		/// It is the one voice the fight hush lets through; the caller owns the timing and the rate limit. Server and single player. Returns the line said, or null.
		/// </summary>
		internal static string SayScene(SoldierNPC s, string pool, DialogueContext ctx)
		{
			if (s == null || s.Record == null || s.NPC == null || !s.NPC.active || s.Hostile || LevyUtil.IsClient) return null;
			var lines = DialogueTable.Get(pool);
			string text = PickFromPool(lines, s.Record, ctx, s, false, -1, out _, true, true) ?? PickFromPool(lines, s.Record, ctx, s, true, -1, out _, true, true);
			if (string.IsNullOrEmpty(text)) return null;
			DeliverFloating(s, text, (byte)DialogueTrigger.BOSS_SCENE);
			return text;
		}

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
				case DialogueTrigger.OFFICER_MARCH:
				case DialogueTrigger.NURSE_MARCH:
				case DialogueTrigger.FLIGHT_SHOUT: case DialogueTrigger.FLIGHT_FEAR: case DialogueTrigger.TAKEOFF: case DialogueTrigger.LANDING: case DialogueTrigger.FIRESIDE: case DialogueTrigger.PASTIME: case DialogueTrigger.PASTIME_NIGHT:
				case DialogueTrigger.LIFE_CRYSTAL: case DialogueTrigger.CHEST_OPENED: case DialogueTrigger.ARMORY_GOT: case DialogueTrigger.ECLIPSE_END:
				case DialogueTrigger.DRINK_ALE: case DialogueTrigger.DRINK_STOUT: case DialogueTrigger.DRINK_RUM: case DialogueTrigger.DRINK_FIZZ: case DialogueTrigger.DRINK_ICEBERG:
				case DialogueTrigger.RIFT_PANIC: case DialogueTrigger.RIFT_CAMP: case DialogueTrigger.RIFT_DUKE: case DialogueTrigger.RIFT_BLOOD: case DialogueTrigger.RIFT_SCARE:
				case DialogueTrigger.RIFT_GROW1: case DialogueTrigger.RIFT_GROW2: case DialogueTrigger.RIFT_GROW3: case DialogueTrigger.RIFT_ICE: case DialogueTrigger.RIFT_FROZEN:
				case DialogueTrigger.VICTORY: return DialogueChannel.FloatingText;

				case DialogueTrigger.DEATH_MESSAGE:
				case DialogueTrigger.DESERTED:
				case DialogueTrigger.NO_ONE_LEFT: return DialogueChannel.ChatLog;

				default: return DialogueChannel.ChatWindow; // IDLE, IDLE_NIGHT, NAMEDROP, BLOOD_MOON, PARTY, GRAVEYARD, RAIN, BIOME_*, PAYDAY, UNPAID, HOLD_FIRE, EVENT_*, ARMORY_WANT / BEST / WORST
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
			if (!RiftMood.AllowFloating(triggerByte)) return; // under the rift sky the army says less (and nothing while the sun is covered)
			FloatingDelivered++;
			global::Levy.Common.Compat.Wotg.AoeVoice.FloatLog?.Add((triggerByte, text));
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
		/// Shows a line on the speaker's chat bar (blue bar over the head, text types in). Client and single player only; the bar queue limits how many show at once
		/// and prefers nearby speakers and important lines. A speaker that is off screen gets no bar.
		/// </summary>
		private static void SpawnFloating(int npcIndex, Rectangle hitbox, string text, byte triggerByte)
		{
			if (Main.netMode == NetmodeID.Server || Main.dedServ) return;
			Common.UI.ChatBars.Show(npcIndex, hitbox, text, triggerByte);
		}

		private static Color FloatColor(byte triggerByte)
		{
			if (triggerByte == byte.MaxValue) return new Color(255, 255, 255);
			switch ((DialogueTrigger)triggerByte)
			{
				case DialogueTrigger.BOSS_SIGHTED:
				case DialogueTrigger.BOSS: return new Color(255, 170, 70);
				case DialogueTrigger.BOSS_SCENE: return new Color(225, 205, 255);
				case DialogueTrigger.COMRADE_DOWN: return new Color(255, 110, 110);
				case DialogueTrigger.MUTINY: return new Color(255, 60, 50);
				case DialogueTrigger.ROUTED: return new Color(255, 225, 90);
				case DialogueTrigger.RALLIED: return new Color(150, 255, 150);
				case DialogueTrigger.VICTORY: return new Color(255, 235, 120);
				case DialogueTrigger.FLIGHT_SHOUT: return new Color(255, 245, 170);
				case DialogueTrigger.EVENT_GOBLINS: case DialogueTrigger.EVENT_PIRATES: case DialogueTrigger.EVENT_ECLIPSE: case DialogueTrigger.EVENT_PUMPKIN:
				case DialogueTrigger.EVENT_FROST: case DialogueTrigger.EVENT_MARTIANS: case DialogueTrigger.EVENT_OLDONES: case DialogueTrigger.EVENT_FROSTLEGION:
				case DialogueTrigger.ECLIPSE_END:
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
