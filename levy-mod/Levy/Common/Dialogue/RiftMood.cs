using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>The army's mood under the Wrath of the Gods sky. None without the mod, before the sun is covered, and after the sky is freed.</summary>
	public enum RiftPhase : byte { None = 0, Silent, Tense, Settled }

	/// <summary>Where a trigger's line comes from under the rift sky.</summary>
	public enum RiftRoute : byte { Normal = 0, Silent, Chat, Camp, Hush }

	/// <summary>
	/// THE RIFT MOOD. With Wrath of the Gods, once Moon Lord falls and the sun is covered, the levies change how they talk (see <see cref="RiftSky"/> for what is read).
	///   Silent: while the sun is being covered nobody says anything (no floating line, no emote, no bark; right-click chat is "..."). Chat-log notices (deaths, desertions) still post.
	///   The moment it ends: a burst of worried lines from the levies near each commander (RIFT_PANIC), then <see cref="LevyConstants.RiftPanicTicks"/> of worried talk.
	///   Tense, for <see cref="LevyConstants.RiftTenseDays"/> dawns, then Settled for as long as the sky stays covered. In both, every calm or joking pool is routed to the rift pools
	///   (right-click and ambient chat, situations, biomes, name-drops, armory talk, the Blood Moon chat), camp and drink talk becomes RIFT_CAMP, the commander reactions, gear thanks and flight
	///   chatter go quiet, and the floating lines that remain are thinned (<see cref="AllowFloating"/>). Combat barks, boss lines, pay, mutiny and officer voices keep their own pools.
	///   Within the rift chat: a levy who was dead or in reserve when the sun went asks what happened (RIFT_ABSENT), the cold when snow falls or lies on the surface (RIFT_COLD),
	///   unsettled respect for the other classes (RIFT_RESPECT), the first shock in the tense days (RIFT_SHOCK), "what now" from day <see cref="LevyConstants.RiftLaterFromDay"/> (RIFT_LATER), and quiet talk (RIFT_IDLE).
	/// The day count, whether the sky has been seen and who was away are saved with the world and sent to clients.
	/// </summary>
	public class RiftMood : ModSystem
	{
		private const int Never = int.MinValue / 2;

		/// <summary>Dawns since the sun was covered (or since Levy first saw it covered).</summary>
		public static int DaysSince;
		/// <summary>Levy has seen this world's sun covered.</summary>
		public static bool Seen;
		/// <summary>Record ids of the levies who were dead or in reserve when the sun went, until each has asked about it <see cref="LevyConstants.RiftAwayLines"/> times.</summary>
		public static readonly HashSet<int> Away = new();
		private static readonly Dictionary<int, int> AwayAsked = new();
		private static int _panicUntil = Never;
		private static bool _primed, _wasTransition, _lastDay;

		// ---- self-check overrides and telemetry ----
		internal static RiftPhase? TestPhase;
		internal static bool? TestPanic, TestSurface;
		internal static int? TestDays;
		/// <summary>Coverings seen ending, panic rounds started (an army was near), panic lines spoken, floating lines held back by the mood.</summary>
		internal static int Changes, PanicRounds, PanicLines, FloatingHeld;

		private static int Now => (int)Main.GameUpdateCount;
		private static int Days => TestDays ?? DaysSince;

		/// <summary>The mood right now.</summary>
		public static RiftPhase Phase
		{
			get
			{
				if (TestPhase.HasValue) return TestPhase.Value;
				if (!RiftSky.Installed) return RiftPhase.None;
				if (RiftSky.Transition) return RiftPhase.Silent;
				if (!RiftSky.Covered || RiftSky.Defeated) return RiftPhase.None;
				return Days < LevyConstants.RiftTenseDays ? RiftPhase.Tense : RiftPhase.Settled;
			}
		}

		/// <summary>The worried talk right after the covering.</summary>
		public static bool PanicNow => TestPanic ?? (_panicUntil != Never && (uint)(_panicUntil - Now) <= (uint)LevyConstants.RiftPanicTicks);

		internal static bool IsRift(DialogueTrigger t) => t >= DialogueTrigger.RIFT_PANIC && t <= DialogueTrigger.RIFT_CAMP;

		/// <summary>Chat-log notices: not a levy speaking, never touched.</summary>
		internal static bool IsNotice(DialogueTrigger t) => t == DialogueTrigger.DEATH_MESSAGE || t == DialogueTrigger.DESERTED || t == DialogueTrigger.NO_ONE_LEFT;

		/// <summary>Where a trigger's line comes from in the current mood.</summary>
		public static RiftRoute Route(DialogueTrigger t)
		{
			RiftPhase ph = Phase;
			if (ph == RiftPhase.None) return RiftRoute.Normal;
			if (ph == RiftPhase.Silent) return IsNotice(t) ? RiftRoute.Normal : RiftRoute.Silent;
			if (IsRift(t)) return RiftRoute.Normal;
			return Covered(t);
		}

		/// <summary>The fixed routing of the covered pools (the self-check reads it too).</summary>
		internal static RiftRoute Covered(DialogueTrigger t)
		{
			switch (t)
			{
				case DialogueTrigger.IDLE: case DialogueTrigger.IDLE_NIGHT: case DialogueTrigger.NAMEDROP: case DialogueTrigger.BLOOD_MOON: case DialogueTrigger.PARTY:
				case DialogueTrigger.GRAVEYARD: case DialogueTrigger.RAIN:
				case DialogueTrigger.BIOME_JUNGLE: case DialogueTrigger.BIOME_DUNGEON: case DialogueTrigger.BIOME_UNDERWORLD: case DialogueTrigger.BIOME_SNOW: case DialogueTrigger.BIOME_CORRUPTION:
				case DialogueTrigger.BIOME_CRIMSON: case DialogueTrigger.BIOME_HALLOW: case DialogueTrigger.BIOME_OCEAN: case DialogueTrigger.BIOME_UNDERGROUND:
				case DialogueTrigger.ARMORY_WANT: case DialogueTrigger.ARMORY_BEST: case DialogueTrigger.ARMORY_WORST:
					return RiftRoute.Chat;
				case DialogueTrigger.FIRESIDE: case DialogueTrigger.PASTIME: case DialogueTrigger.PASTIME_NIGHT:
				case DialogueTrigger.DRINK: case DialogueTrigger.DRINK_ALE: case DialogueTrigger.DRINK_STOUT: case DialogueTrigger.DRINK_RUM: case DialogueTrigger.DRINK_FIZZ: case DialogueTrigger.DRINK_ICEBERG:
					return RiftRoute.Camp;
				case DialogueTrigger.LIFE_CRYSTAL: case DialogueTrigger.CHEST_OPENED: case DialogueTrigger.ARMORY_GOT: case DialogueTrigger.ECLIPSE_END:
				case DialogueTrigger.TAKEOFF: case DialogueTrigger.LANDING: case DialogueTrigger.FLIGHT_FEAR: case DialogueTrigger.FLIGHT_SHOUT:
					return RiftRoute.Hush;
				default: return RiftRoute.Normal;
			}
		}

		// =====================================================================================================
		// The rift chat
		// =====================================================================================================

		private static readonly DialogueTrigger[] Order = new DialogueTrigger[8];

		/// <summary>The rift pools to try, in order, for this speaker right now. RIFT_IDLE is always last.</summary>
		internal static int ChatOrder(SoldierRecord rec, SoldierNPC body, bool camp)
		{
			int n = 0;
			if (PanicNow) Order[n++] = DialogueTrigger.RIFT_PANIC;
			if (camp) { Order[n++] = DialogueTrigger.RIFT_CAMP; Order[n++] = DialogueTrigger.RIFT_IDLE; return n; }
			if (rec != null && Away.Contains(rec.Id) && Main.rand.NextFloat() < LevyConstants.RiftAwayChance) Order[n++] = DialogueTrigger.RIFT_ABSENT;
			if (Cold(body) && Main.rand.NextFloat() < LevyConstants.RiftColdChance) Order[n++] = DialogueTrigger.RIFT_COLD;
			if (Main.rand.NextFloat() < LevyConstants.RiftRespectChance) Order[n++] = DialogueTrigger.RIFT_RESPECT;
			RiftPhase ph = Phase;
			if (ph == RiftPhase.Tense && Main.rand.NextFloat() < LevyConstants.RiftShockChance) Order[n++] = DialogueTrigger.RIFT_SHOCK;
			if (Days >= LevyConstants.RiftLaterFromDay && Main.rand.NextFloat() < LevyConstants.RiftLaterChance) Order[n++] = DialogueTrigger.RIFT_LATER;
			Order[n++] = DialogueTrigger.RIFT_IDLE;
			return n;
		}

		internal static DialogueTrigger OrderAt(int i) => Order[i];

		/// <summary>Snow falling or lying, and the speaker (else the local player) on the surface.</summary>
		internal static bool Cold(SoldierNPC body)
		{
			if (!(RiftSky.Snowing || RiftSky.SnowHeight >= LevyConstants.RiftSnowLyingHeight)) return false;
			if (TestSurface.HasValue) return TestSurface.Value;
			try
			{
				float surfacePx = (float)(Main.worldSurface * 16.0);
				if (body?.NPC != null && body.NPC.active) return body.NPC.Center.Y <= surfacePx;
				if (!Main.dedServ && Main.LocalPlayer != null && Main.LocalPlayer.active) return Main.LocalPlayer.Center.Y <= surfacePx;
			}
			catch (Exception) { }
			return true;
		}

		/// <summary>A levy said a RIFT_ABSENT line: after enough of them he has caught up.</summary>
		internal static void NoteAway(SoldierRecord rec)
		{
			if (rec == null || !Away.Contains(rec.Id)) return;
			AwayAsked.TryGetValue(rec.Id, out int n);
			if (++n >= LevyConstants.RiftAwayLines) { Away.Remove(rec.Id); AwayAsked.Remove(rec.Id); }
			else AwayAsked[rec.Id] = n;
		}

		// =====================================================================================================
		// How much is still said out loud
		// =====================================================================================================

		/// <summary>The share of a trigger's floating lines still spoken in a phase (1 = all).</summary>
		internal static float Share(DialogueTrigger t, RiftPhase ph)
		{
			if (ph == RiftPhase.None) return 1f;
			if (ph == RiftPhase.Silent) return 0f;
			bool tense = ph == RiftPhase.Tense;
			switch (t)
			{
				case DialogueTrigger.RIFT_PANIC: case DialogueTrigger.MUTINY: return 1f;
				case DialogueTrigger.FLIGHT_SHOUT: return tense ? 0f : 0.25f;
				case DialogueTrigger.IDLE: case DialogueTrigger.IDLE_NIGHT: case DialogueTrigger.NAMEDROP: case DialogueTrigger.FIRESIDE: case DialogueTrigger.PASTIME: case DialogueTrigger.PASTIME_NIGHT:
				case DialogueTrigger.DRINK: case DialogueTrigger.DRINK_ALE: case DialogueTrigger.DRINK_STOUT: case DialogueTrigger.DRINK_RUM: case DialogueTrigger.DRINK_FIZZ: case DialogueTrigger.DRINK_ICEBERG:
				case DialogueTrigger.LIFE_CRYSTAL: case DialogueTrigger.CHEST_OPENED: case DialogueTrigger.ARMORY_GOT: case DialogueTrigger.ECLIPSE_END:
				case DialogueTrigger.TAKEOFF: case DialogueTrigger.LANDING: case DialogueTrigger.FLIGHT_FEAR:
				case DialogueTrigger.RIFT_SHOCK: case DialogueTrigger.RIFT_ABSENT: case DialogueTrigger.RIFT_COLD: case DialogueTrigger.RIFT_LATER: case DialogueTrigger.RIFT_RESPECT: case DialogueTrigger.RIFT_IDLE: case DialogueTrigger.RIFT_CAMP:
					return tense ? LevyConstants.RiftTenseChatterShare : LevyConstants.RiftSettledChatterShare;
				case DialogueTrigger.BOSS_SIGHTED: case DialogueTrigger.BOSS: case DialogueTrigger.COMRADE_DOWN: case DialogueTrigger.ROUTED: case DialogueTrigger.RALLIED: case DialogueTrigger.VICTORY:
				case DialogueTrigger.EVENT_GOBLINS: case DialogueTrigger.EVENT_PIRATES: case DialogueTrigger.EVENT_ECLIPSE: case DialogueTrigger.EVENT_PUMPKIN:
				case DialogueTrigger.EVENT_FROST: case DialogueTrigger.EVENT_MARTIANS: case DialogueTrigger.EVENT_OLDONES: case DialogueTrigger.EVENT_FROSTLEGION:
					return tense ? LevyConstants.RiftTenseBigShare : 1f;
				default: return tense ? LevyConstants.RiftTenseBarkShare : LevyConstants.RiftSettledBarkShare;
			}
		}

		/// <summary>
		/// Whether a floating line goes out (byte.MaxValue = a line with no trigger, e.g. a Rage shout: always). During the worried talk after the covering the ambient lines are not thinned.
		/// </summary>
		public static bool AllowFloating(byte triggerByte)
		{
			RiftPhase ph = Phase;
			if (ph == RiftPhase.None || triggerByte == byte.MaxValue) return ph != RiftPhase.Silent;
			var t = (DialogueTrigger)triggerByte;
			float share = Share(t, ph);
			if (share > 0f && PanicNow && ph != RiftPhase.Silent) share = 1f;
			if (share >= 1f) return true;
			if (share > 0f && Main.rand.NextFloat() < share) return true;
			FloatingHeld++;
			return false;
		}

		/// <summary>No sound at all from a levy: the sun is being covered.</summary>
		public static bool Hushed => Phase == RiftPhase.Silent;

		// =====================================================================================================
		// The world: watching for the covering, counting the days
		// =====================================================================================================

		public override void ClearWorld() => Reset();
		public override void OnWorldUnload() => Reset();

		internal static void Reset()
		{
			DaysSince = 0; Seen = false; Away.Clear(); AwayAsked.Clear();
			_panicUntil = Never; _primed = false; _wasTransition = false; _lastDay = false;
		}

		public override void PostUpdateEverything()
		{
			try { Tick(Now); }
			catch (Exception) { /* the mood must never throw into the game loop */ }
		}

		/// <summary>One tick of watching (runs everywhere: a client keeps its own panic clock and day count for right-click chat). Exposed for the world check.</summary>
		internal static void Tick(int now)
		{
			if (!RiftSky.Installed) return;
			bool trans = RiftSky.Transition, covered = RiftSky.Covered;
			if (!_primed) { _primed = true; _wasTransition = trans; _lastDay = Main.dayTime; }
			else
			{
				if (_wasTransition && !trans && covered) OnCovered(now);
				_wasTransition = trans;
				if (Main.dayTime && !_lastDay && Seen && covered) DaysSince++;
				_lastDay = Main.dayTime;
			}
			if (covered && !trans && !Seen) { Seen = true; DaysSince = 0; } // first sight of a sky already covered (a save from before Levy, or a world joined late)
		}

		/// <summary>The sun has just been covered: the clock starts, the absent are noted, and the levies near each commander react.</summary>
		private static void OnCovered(int now)
		{
			Changes++;
			Seen = true; DaysSince = 0;
			_panicUntil = now + LevyConstants.RiftPanicTicks;
			if (LevyUtil.IsClient) return; // a client hears who was away from the server (NetReceive)
			Away.Clear(); AwayAsked.Clear();
			try
			{
				var roster = RosterSystem.Instance?.Regulars;
				if (roster != null) foreach (SoldierRecord r in roster) if (r.Dead || r.Reserve) Away.Add(r.Id);
			}
			catch (Exception) { }
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.dead) continue;
				if (CommanderReactions.Enqueue(p, DialogueTrigger.RIFT_PANIC, LevyConstants.RiftPanicSpeakers, now, LevyConstants.RiftPanicStepTicks)) PanicRounds++;
			}
			if (Main.netMode == NetmodeID.Server && AnyClient()) NetMessage.SendData(MessageID.WorldData); // the clients learn who was away
		}

		private static bool AnyClient()
		{
			for (int i = 0; i < Main.maxPlayers; i++) if (Netplay.Clients[i] != null && Netplay.Clients[i].IsConnected()) return true;
			return false;
		}

		/// <summary>World check hook: the panic clock as if the covering had just ended (or cleared).</summary>
		internal static void SetPanic(bool on) => _panicUntil = on ? Now + LevyConstants.RiftPanicTicks : Never;

		public override void SaveWorldData(TagCompound tag)
		{
			if (!Seen) return;
			tag["riftSeen"] = true;
			tag["riftDays"] = DaysSince;
			if (Away.Count > 0) { var ids = new int[Away.Count]; Away.CopyTo(ids); tag["riftAway"] = ids; }
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Seen = tag.ContainsKey("riftSeen");
			DaysSince = tag.GetInt("riftDays");
			Away.Clear();
			if (tag.TryGet("riftAway", out int[] ids)) foreach (int id in ids) Away.Add(id);
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Seen);
			writer.Write((ushort)Math.Clamp(DaysSince, 0, 65535));
			int n = Math.Min(Away.Count, 255);
			writer.Write((byte)n);
			foreach (int id in Away) { if (n-- <= 0) break; writer.Write(id); }
		}

		public override void NetReceive(BinaryReader reader)
		{
			Seen = reader.ReadBoolean();
			DaysSince = reader.ReadUInt16();
			int n = reader.ReadByte();
			Away.Clear();
			for (int i = 0; i < n; i++) Away.Add(reader.ReadInt32());
		}
	}
}
