using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Dialogue
{
	/// <summary>The Wrath of the Gods scene the army is in.</summary>
	public enum WotgFight : byte { None = 0, Genesis, Rift, Avatar, Nameless }

	/// <summary>
	/// THE ARMY IN THE WRATH OF THE GODS FIGHTS. While the Genesis is fired, the Avatar's rift is open, the Avatar of Emptiness or Nameless Deity is up, the levies' ordinary chatter stays hushed
	/// (<see cref="Death.NoxusFights.Hush"/>: no pay talk, no camp grumbles, no generic boss barks) and this system speaks for them instead, in lines written for the fight (SCENE_ pools in the dialogue file):
	///   moments read from the bosses' own state machines (NoxusBoss's CurrentState / CurrentAttack enum, by reflection: the rift opening, the Avatar climbing out, its screams, the trips to its universe,
	///   the frost and blood dimensions, the Nameless Deity arriving), the boss nearly beaten, the kill (and the fallen, by name), a commander falling, and the army greeting its commander home after the
	///   Nameless Deity is beaten in its garden (a subworld the army does not follow); plus now and then a worried line from a levy near the commander while the fight goes on.
	/// Timing: nothing is said while the boss "speaks" (its screams and rants, Solyn's lines, the Nameless Deity's subtitles) or for <see cref="LevyConstants.SceneVoiceGapTicks"/> after; scripted lines are at least
	/// <see cref="LevyConstants.SceneLineGapTicks"/> apart army-wide; a moment speaks once per fight (attack moments: once per cooldown); a reaction that cannot be said in time is dropped, never queued up.
	/// Lines go out through the normal floating-line path (<see cref="DialogueSystem.SayScene"/>: a Bark packet to clients in reach). Server and single player. Off with the "Levies react in Wrath of the Gods fights" setting,
	/// which puts back the old full silence. NoxusBoss is optional: everything is read by name and a missing member is named in <see cref="Problem"/>.
	/// </summary>
	public partial class WotgScenes : ModSystem
	{
		// =====================================================================================================
		// Pools
		// =====================================================================================================

		/// <summary>Every SCENE_ pool the code asks for. The dialogue file may only use these (a typo is a load warning).</summary>
		internal static readonly string[] Pools =
		{
			"SCENE_GENESIS", "SCENE_RIFT_OPEN", "SCENE_RIFT_SUCK", "SCENE_RIFT_DOWN", "SCENE_RIFT_FIGHT",
			"SCENE_AVATAR_EMERGE", "SCENE_AVATAR_HEAD", "SCENE_AVATAR_SCREAM", "SCENE_AVATAR_FIGHT", "SCENE_AVATAR_COLD", "SCENE_AVATAR_BLOOD", "SCENE_AVATAR_ATTACK",
			"SCENE_AVATAR_HEADPATS", "SCENE_AVATAR_UNIVERSE", "SCENE_AVATAR_RETURN", "SCENE_AVATAR_PHASE3", "SCENE_AVATAR_HOPE", "SCENE_AVATAR_STATIC", "SCENE_AVATAR_NAMELESS",
			"SCENE_AVATAR_HOME", "SCENE_AVATAR_KILL",
			"SCENE_NAMELESS_AWAKEN", "SCENE_NAMELESS_SCREAM", "SCENE_NAMELESS_FIGHT", "SCENE_NAMELESS_ATTACK", "SCENE_NAMELESS_CLOCK", "SCENE_NAMELESS_PHASE2", "SCENE_NAMELESS_PHASE3",
			"SCENE_NAMELESS_HOPE", "SCENE_NAMELESS_RESCUE", "SCENE_NAMELESS_KILL", "SCENE_NAMELESS_HOMECOMING",
			"SCENE_WOTG_COMMANDER_DOWN", "SCENE_WOTG_MOURN",
		};

		internal static string CanonicalPool(string s)
		{
			foreach (string p in Pools) if (string.Equals(p, s, StringComparison.OrdinalIgnoreCase)) return p;
			return null;
		}

		/// <summary>A boss state that opens a moment. Exit = speak when the state ends (a scream), not when it starts. Cooldown 0 = once a fight.</summary>
		private readonly struct Moment
		{
			public readonly string Pool; public readonly int Speakers; public readonly bool Exit; public readonly int Cooldown;
			public Moment(string pool, int speakers, bool exit = false, int cooldown = 0) { Pool = pool; Speakers = speakers; Exit = exit; Cooldown = cooldown; }
		}

		private const int Attack = 60 * 45;
		private static readonly Dictionary<string, Moment> RiftMoments = new()
		{
			["Awaken"] = new("SCENE_RIFT_OPEN", 3), ["SuckPlayerIn"] = new("SCENE_RIFT_SUCK", 1, cooldown: Attack), ["DeathAnimation"] = new("SCENE_RIFT_DOWN", 2),
		};
		private static readonly Dictionary<string, Moment> AvatarMoments = new()
		{
			["Awaken_LegEmergence"] = new("SCENE_AVATAR_EMERGE", 2), ["Awaken_HeadEmergence"] = new("SCENE_AVATAR_HEAD", 1), ["Awaken_Scream"] = new("SCENE_AVATAR_SCREAM", 3, exit: true),
			["Phase3TransitionScream"] = new("SCENE_AVATAR_PHASE3", 2, exit: true),
			["SendPlayerToMyUniverse"] = new("SCENE_AVATAR_UNIVERSE", 2, cooldown: Attack), ["ReturnPlayersFromMyUniverse"] = new("SCENE_AVATAR_RETURN", 1, cooldown: Attack),
			["FrostScreenSmash_CreateFrost"] = new("SCENE_AVATAR_COLD", 1, cooldown: Attack * 2), ["WhirlingIceStorm"] = new("SCENE_AVATAR_COLD", 1, cooldown: Attack * 2), ["AbsoluteZeroOutburst"] = new("SCENE_AVATAR_COLD", 1, cooldown: Attack * 2),
			["BloodiedFountainBlasts"] = new("SCENE_AVATAR_BLOOD", 1, cooldown: Attack * 2), ["EnterBloodWhirlpool"] = new("SCENE_AVATAR_BLOOD", 1, cooldown: Attack * 2), ["BloodiedWeep"] = new("SCENE_AVATAR_BLOOD", 1, cooldown: Attack * 2),
			["UniversalAnnihilation"] = new("SCENE_AVATAR_ATTACK", 1, cooldown: Attack), ["RealityShatter_CreateAndWaitForTelegraph"] = new("SCENE_AVATAR_ATTACK", 1, cooldown: Attack), ["WorldSmash"] = new("SCENE_AVATAR_ATTACK", 1, cooldown: Attack),
			["GivePlayerHeadpats"] = new("SCENE_AVATAR_HEADPATS", 2),
			["ParadiseReclaimed_StaticChase"] = new("SCENE_AVATAR_STATIC", 2), ["ParadiseReclaimed_NamelessDispelsStatic"] = new("SCENE_AVATAR_NAMELESS", 2),
			["ParadiseReclaimed_NamelessReturnsEveryoneToOverworld"] = new("SCENE_AVATAR_HOME", 1),
		};
		private static readonly Dictionary<string, Moment> NamelessMoments = new()
		{
			["Awaken"] = new("SCENE_NAMELESS_AWAKEN", 2), ["IntroScreamAnimation"] = new("SCENE_NAMELESS_SCREAM", 2, exit: true),
			["EnterPhase2"] = new("SCENE_NAMELESS_PHASE2", 2), ["EnterPhase3"] = new("SCENE_NAMELESS_PHASE3", 2), ["ClockConstellation"] = new("SCENE_NAMELESS_CLOCK", 1),
			["SuperCosmicLaserbeam"] = new("SCENE_NAMELESS_ATTACK", 1, cooldown: Attack), ["MomentOfCreation"] = new("SCENE_NAMELESS_ATTACK", 1, cooldown: Attack), ["CrushStarIntoQuasar"] = new("SCENE_NAMELESS_ATTACK", 1, cooldown: Attack),
			["SavePlayerFromAvatar"] = new("SCENE_NAMELESS_RESCUE", 2),
		};

		/// <summary>States in which the boss itself has the floor (a scream, a rant, Solyn talking, a death scene): nothing is said.</summary>
		private static readonly HashSet<string> VoiceStates = new()
		{
			"Awaken_Scream", "Phase3TransitionScream", "ParadiseReclaimed_SolynDialogue", "IntroScreamAnimation", "RodOfHarmonyRant", "PlayerHurtAvatarRant", "DeathAnimation", "EndCreditsScene",
		};

		private static Dictionary<string, Moment> MomentsOf(WotgFight f) => f switch { WotgFight.Rift => RiftMoments, WotgFight.Avatar => AvatarMoments, WotgFight.Nameless => NamelessMoments, _ => null };
		private static string FightPool(WotgFight f) => f switch { WotgFight.Rift => "SCENE_RIFT_FIGHT", WotgFight.Avatar => "SCENE_AVATAR_FIGHT", WotgFight.Nameless => "SCENE_NAMELESS_FIGHT", _ => null };

		// =====================================================================================================
		// Self-check overrides and telemetry
		// =====================================================================================================

		internal static WotgFight? TestFight; internal static string TestState; internal static float? TestLife; internal static bool? TestVoice, TestEnabled, TestAvatarDefeated, TestNamelessDefeated, TestGenesis;
		internal struct Said { public int Tick; public string Pool; public string Text; public int Npc; }
		/// <summary>Every scripted line said (newest last, capped), the moments started, cues dropped for lack of time or speakers, ticks the boss held the floor.</summary>
		internal static readonly List<Said> Log = new();
		internal static int MomentsStarted, CuesDropped, VoiceTicks, FightLines, Homecomings;
		/// <summary>The last tick the boss had the floor.</summary>
		internal static int LastVoiceTick = Never;

		// =====================================================================================================
		// Runtime state
		// =====================================================================================================

		private const int Never = int.MinValue / 2;
		private struct Cue { public string Pool; public int Due, Expire; public string Comrade; public Vector2 At; public bool UseSpot; }
		private static readonly List<Cue> Queue = new();
		private static readonly Dictionary<string, int> MomentAt = new();
		private static readonly List<SoldierNPC> Scratch = new();
		private static readonly int[] RecentSpeakers = { -1, -1, -1 };
		private static WotgFight _fight; private static string _state; private static int _holdUntil = Never, _lastSaid = Never, _nextChatter = Never;
		private static bool _hope; private static readonly HashSet<int> FightAlive = new();
		private static readonly bool[] WasDead = new bool[Main.maxPlayers];
		private static bool? _ndKnown; private static int _flagReadAt = Never; private static bool _ndNow, _avNow;

		private static int Now => (int)Main.GameUpdateCount;
		/// <summary>The scene the watch saw last tick.</summary>
		internal static WotgFight CurrentFight => _fight;

		/// <summary>The setting (on by default).</summary>
		public static bool Enabled { get { if (TestEnabled.HasValue) return TestEnabled.Value; try { return LevyConfig.Get?.WotgFightChatter ?? true; } catch (Exception) { return true; } } }

		/// <summary>Self-check: the Avatar fight's chatter share (percent), or null for the setting.</summary>
		internal static int? TestAvatarPercent;

		/// <summary>
		/// How many times rarer the repeating chatter is in this fight: 1 outside the Avatar of Emptiness fight and at 100 percent, 100 / percent below that, infinity at 0 (nothing between the boss's own moments).
		/// The setting is AvatarFightChatterPercent (default 25): the army speaks the story moments (the rift, the scream, the kill) in full and a quarter of the worried lines and attack reactions.
		/// </summary>
		internal static float Quiet(WotgFight fight)
		{
			if (fight != WotgFight.Avatar) return 1f;
			int pct = TestAvatarPercent ?? 25;
			if (!TestAvatarPercent.HasValue) { try { pct = LevyConfig.Get?.AvatarFightChatterPercent ?? 25; } catch (Exception) { pct = 25; } }
			if (pct >= 100) return 1f;
			if (pct <= 0) return float.PositiveInfinity;
			return 100f / pct;
		}

		/// <summary>The Genesis scene (the summoning of the Avatar's rift) is playing.</summary>
		public static bool GenesisOn { get { if (TestGenesis.HasValue) return TestGenesis.Value; Resolve(); return Read(_genesisActive, null, false); } }

		public override void ClearWorld() => Reset();
		public override void OnWorldUnload() => Reset();

		internal static void Reset()
		{
			Queue.Clear(); MomentAt.Clear(); FightAlive.Clear(); Array.Fill(RecentSpeakers, -1); Array.Clear(WasDead);
			_fight = WotgFight.None; _state = null; _holdUntil = _lastSaid = _nextChatter = LastVoiceTick = Never; _hope = false; _flagReadAt = Never;
		}

		public override void SaveWorldData(TagCompound tag) { if (_ndKnown.HasValue) tag["wotgNamelessSeen"] = _ndKnown.Value; }
		public override void LoadWorldData(TagCompound tag) { _ndKnown = tag.ContainsKey("wotgNamelessSeen") ? tag.GetBool("wotgNamelessSeen") : null; }

		public override void PostUpdateEverything()
		{
			if (LevyUtil.IsClient || RosterSystem.Instance == null) return;
			try { Tick(Now); } catch (Exception) { /* a fight line must never throw into the game loop */ }
		}

		// =====================================================================================================
		// The watch
		// =====================================================================================================

		/// <summary>One tick: read the fight, start moments, say what is due. Exposed for the world check.</summary>
		internal static void Tick(int now)
		{
			if (!TestFight.HasValue && !Installed) return;
			Flags(now);
			WotgFight fight = Observe(out NPC boss, out string state, out float life);
			bool enabled = Enabled;

			if (fight != _fight)
			{
				if (_fight != WotgFight.None && fight == WotgFight.None) Ended(_fight, now);
				if (fight != WotgFight.None) Started(fight, now);
				_fight = fight; _state = null; _hope = false;
			}
			if (fight == WotgFight.None) { Deliver(now, enabled); return; }

			bool voice = Voice(state);
			if (voice) { VoiceTicks++; LastVoiceTick = now; _holdUntil = now + LevyConstants.SceneVoiceGapTicks; }

			// state changes: a moment on entry, or (a scream) on exit
			if (state != _state)
			{
				var moments = MomentsOf(fight);
				if (_state != null && moments != null && moments.TryGetValue(_state, out Moment was) && was.Exit) Begin(was, fight, now);
				if (state != null && moments != null && moments.TryGetValue(state, out Moment m) && !m.Exit) Begin(m, fight, now);
				if (fight == WotgFight.Avatar && state == "LeaveAfterBeingHit") Kill(fight, now);
				_state = state;
			}
			if (!_hope && life > 0f && life <= LevyConstants.SceneHopeLife && (fight == WotgFight.Avatar || fight == WotgFight.Nameless))
			{
				_hope = true;
				Begin(new Moment(fight == WotgFight.Avatar ? "SCENE_AVATAR_HOPE" : "SCENE_NAMELESS_HOPE", 2), fight, now);
			}
			CommanderDeaths(now);

			// a worried line now and then while the fight goes on
			string pool = FightPool(fight);
			if (pool != null && Queue.Count == 0 && enabled)
			{
				float quiet = Quiet(fight);   // the Avatar fight keeps a share of it (the AvatarFightChatterPercent setting): 4 means one worried line where there were four
				if (float.IsPositiveInfinity(quiet)) { }
				else if (_nextChatter == Never) _nextChatter = now + (int)(Main.rand.Next(LevyConstants.SceneChatterMinTicks / 2, LevyConstants.SceneChatterMaxTicks / 2) * quiet);
				else if (now - _nextChatter >= 0)
				{
					_nextChatter = now + (int)(Main.rand.Next(LevyConstants.SceneChatterMinTicks, LevyConstants.SceneChatterMaxTicks) * quiet);
					Enqueue(pool, 1, now, 0, LevyConstants.SceneChatterExpireTicks);
				}
			}
			Deliver(now, enabled);
		}

		private static void Started(WotgFight fight, int now)
		{
			_nextChatter = Never;
			if (_fight != WotgFight.None) return; // the Genesis opens the rift, the rift gives way to the Avatar: one fight, and what was about to be said still is
			MomentAt.Clear(); Queue.Clear();
			FightAlive.Clear();
			foreach (SoldierRecord r in RosterSystem.Instance.Regulars) if (!r.Dead && !r.Reserve) FightAlive.Add(r.Id);
			Array.Clear(WasDead);
			for (int i = 0; i < Main.maxPlayers; i++) WasDead[i] = Main.player[i] != null && Main.player[i].active && Main.player[i].dead;
			if (fight == WotgFight.Genesis) Begin(new Moment("SCENE_GENESIS", 3), fight, now);
		}

		/// <summary>The scene is over: the rift and the Avatar hand on to each other (no kill there); a Nameless Deity that leaves beaten is a kill.</summary>
		private static void Ended(WotgFight was, int now)
		{
			if (was == WotgFight.Nameless && NamelessDefeated) { _ndKnown = true; Kill(was, now); }
			else if (was == WotgFight.Avatar && _avNow) Kill(was, now);
		}

		private static void Kill(WotgFight fight, int now)
		{
			if (MomentAt.ContainsKey("kill")) return;
			MomentAt["kill"] = now;
			Begin(new Moment(fight == WotgFight.Nameless ? "SCENE_NAMELESS_KILL" : "SCENE_AVATAR_KILL", 3), fight, now, force: true);
			// the fallen: a levy who stood at the start of the fight and is dead now
			var fallen = new List<string>();
			foreach (SoldierRecord r in RosterSystem.Instance.Regulars) if (r.Dead && FightAlive.Contains(r.Id)) fallen.Add(r.Name);
			if (fallen.Count > 0) Enqueue("SCENE_WOTG_MOURN", 1, now + LevyConstants.SceneStepTicks * 3, 0, LevyConstants.SceneKillExpireTicks, fallen[Main.rand.Next(fallen.Count)]);
		}

		/// <summary>A commander who dies in the fight: the levies near where he fell.</summary>
		private static void CommanderDeaths(int now)
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				bool dead = p != null && p.active && p.dead;
				if (dead && !WasDead[i])
				{
					string key = "down" + i;
					if (!MomentAt.TryGetValue(key, out int at) || now - at > LevyConstants.SceneCommanderDownCooldownTicks)
					{
						MomentAt[key] = now;
						CuesDropped += Queue.RemoveAll(q => !q.Pool.EndsWith("_KILL")); // whatever else was about to be said is stale now
						for (int k = 0; k < 2; k++) Queue.Add(new Cue { Pool = "SCENE_WOTG_COMMANDER_DOWN", Due = now + 30 + k * LevyConstants.SceneStepTicks, Expire = now + 30 + k * LevyConstants.SceneStepTicks + LevyConstants.SceneMomentExpireTicks, At = p.Center, UseSpot = true });
						MomentsStarted++;
					}
				}
				WasDead[i] = dead;
			}
		}

		/// <summary>Starts a moment: its speakers queued one after another. A moment speaks once a fight, or once per its cooldown.</summary>
		private static void Begin(Moment m, WotgFight fight, int now, bool force = false)
		{
			float quiet = m.Cooldown > 0 ? Quiet(fight) : 1f;   // a moment that comes round again (the Avatar's attacks and trips) is quieter in the Avatar fight; the story moments and the kill are not touched
			if (float.IsPositiveInfinity(quiet) && !force) return;
			int cooldown = float.IsPositiveInfinity(quiet) ? m.Cooldown : (int)(m.Cooldown * quiet);
			if (!force && MomentAt.TryGetValue(m.Pool, out int at) && (m.Cooldown <= 0 || now - at < cooldown)) return;
			MomentAt[m.Pool] = now;
			MomentsStarted++;
			Enqueue(m.Pool, quiet > 1f ? 1 : m.Speakers, now + 20, LevyConstants.SceneStepTicks, m.Pool.EndsWith("_KILL") ? LevyConstants.SceneKillExpireTicks : LevyConstants.SceneMomentExpireTicks);
		}

		private static void Enqueue(string pool, int speakers, int due, int step, int expire, string comrade = null)
		{
			for (int k = 0; k < speakers; k++) Queue.Add(new Cue { Pool = pool, Due = due + k * step + Main.rand.Next(12), Expire = due + k * step + expire, Comrade = comrade });
			Queue.Sort((a, b) => a.Due.CompareTo(b.Due));
		}

		/// <summary>Says the first cue that is due, if the boss does not have the floor and the last line is far enough back. Stale cues are dropped.</summary>
		private static void Deliver(int now, bool enabled)
		{
			for (int i = Queue.Count - 1; i >= 0; i--) if (now - Queue[i].Expire > 0 || !enabled) { Queue.RemoveAt(i); CuesDropped++; }
			if (Queue.Count == 0 || now - _holdUntil < 0 || now - _lastSaid < LevyConstants.SceneLineGapTicks) return;
			Cue c = Queue[0];
			if (now - c.Due < 0) return;
			Queue.RemoveAt(0);
			SoldierNPC s = PickSpeaker(c);
			if (s == null) { CuesDropped++; return; }
			var ctx = new DialogueContext { Player = s.Record.CommanderName, Comrade = c.Comrade };
			string text = DialogueSystem.SayScene(s, c.Pool, ctx);
			if (text == null) { CuesDropped++; return; }
			_lastSaid = now;
			if (c.Pool.EndsWith("_FIGHT")) FightLines++;
			RecentSpeakers[2] = RecentSpeakers[1]; RecentSpeakers[1] = RecentSpeakers[0]; RecentSpeakers[0] = s.Record.Id;
			Log.Add(new Said { Tick = now, Pool = c.Pool, Text = text, Npc = s.NPC.whoAmI });
			if (Log.Count > 400) Log.RemoveRange(0, 100);
		}

		/// <summary>A standing regular near a living commander (or near the spot a commander fell), not one of the last three to speak when anyone else is there.</summary>
		private static SoldierNPC PickSpeaker(Cue c)
		{
			Scratch.Clear();
			float reach = LevyConstants.SceneReachTiles * LevyConstants.TilePx;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (!s.IsAlive || !s.IsRegular || s.Record.Reserve || s.Hostile) continue;
				bool near = false;
				if (c.UseSpot) near = Vector2.Distance(s.NPC.Center, c.At) <= reach;
				else
					for (int i = 0; i < Main.maxPlayers && !near; i++)
					{
						Player p = Main.player[i];
						if (p != null && p.active && !p.dead && p.name == s.Record.CommanderName && Vector2.Distance(s.NPC.Center, p.Center) <= reach) near = true;
					}
				if (near) Scratch.Add(s);
			}
			if (Scratch.Count == 0) return null;
			int fresh = 0;
			foreach (SoldierNPC s in Scratch) if (Array.IndexOf(RecentSpeakers, s.Record.Id) < 0) fresh++;
			int pick = Main.rand.Next(fresh > 0 ? fresh : Scratch.Count);
			foreach (SoldierNPC s in Scratch)
			{
				if (fresh > 0 && Array.IndexOf(RecentSpeakers, s.Record.Id) >= 0) continue;
				if (pick-- == 0) { Scratch.Clear(); return s; }
			}
			SoldierNPC last = Scratch[Scratch.Count - 1]; Scratch.Clear(); return last;
		}

		/// <summary>The defeat flags, read through the mod's own call twice a second. The Nameless Deity's turning on while the army is home (it is fought in its garden) is the army's commander coming back victorious.</summary>
		private static void Flags(int now)
		{
			if (_flagReadAt != Never && now - _flagReadAt < 30) return;
			_flagReadAt = now;
			_ndNow = NamelessDefeated; _avNow = TestAvatarDefeated ?? CallDefeated("avatarofemptiness");
			if (!_ndKnown.HasValue) { _ndKnown = _ndNow; return; }
			if (_ndNow && !_ndKnown.Value && _fight != WotgFight.Nameless && !AnyNpc(_namelessType)) // beaten in front of the army is the kill, not a homecoming
			{
				Homecomings++;
				if (Enabled) Enqueue("SCENE_NAMELESS_HOMECOMING", 3, now + LevyConstants.SceneHomecomingDelayTicks, LevyConstants.SceneStepTicks, LevyConstants.SceneKillExpireTicks * 2);
			}
			_ndKnown = _ndNow;
		}

		internal static bool NamelessDefeated => TestNamelessDefeated ?? CallDefeated("namelessdeity");

		/// <summary>The boss has the floor: a voice state, Solyn talking, or the Nameless Deity's subtitles on screen.</summary>
		private static bool Voice(string state)
		{
			if (TestVoice.HasValue) return TestVoice.Value || (state != null && VoiceStates.Contains(state));
			if (state != null && VoiceStates.Contains(state)) return true;
			return SolynTalking() || SubtitlesShowing();
		}

		/// <summary>The scene now: the boss NPC, its state's name and its life (0..1).</summary>
		private static WotgFight Observe(out NPC boss, out string state, out float life)
		{
			boss = null; state = null; life = 0f;
			if (TestFight.HasValue) { state = TestState; life = TestLife ?? 1f; return TestFight.Value; }
			NPC rift = null, avatar = null, nameless = null;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n == null || !n.active) continue;
				if (n.type == _namelessType) nameless = n; else if (n.type == _avatarType) avatar = n; else if (n.type == _riftType) rift = n;
			}
			WotgFight f;
			if (avatar != null) { boss = avatar; f = WotgFight.Avatar; }
			else if (nameless != null) { boss = nameless; f = WotgFight.Nameless; }
			else if (rift != null) { boss = rift; f = WotgFight.Rift; if (StateOf(rift) == "KillOldDuke") return WotgFight.None; } // the sky taking the Old Duke is RiftMood's scene (RIFT_DUKE), not a fight
			else return GenesisOn ? WotgFight.Genesis : WotgFight.None;
			state = StateOf(boss);
			life = boss.lifeMax > 0 ? (float)boss.life / boss.lifeMax : 0f;
			// the Nameless Deity saving the commander from the Avatar is a moment of the Avatar's fight
			if (f == WotgFight.Avatar && nameless != null && StateOf(nameless) == "SavePlayerFromAvatar") Begin(NamelessMoments["SavePlayerFromAvatar"], f, Now);
			return f;
		}

		/// <summary>Right-click chat during one of these fights (any side: the fight is read from the NPCs here): a worried line from the fight's pool, or null (then "...") with the setting off or no fight.</summary>
		internal static string ChatLine(SoldierNPC s)
		{
			if (!Enabled || s?.Record == null) return null;
			WotgFight f = TestFight ?? (AnyNpc(AvatarType) ? WotgFight.Avatar : AnyNpc(NamelessType) ? WotgFight.Nameless : AnyNpc(RiftType) ? WotgFight.Rift : GenesisOn ? WotgFight.Genesis : WotgFight.None);
			string pool = f == WotgFight.Genesis ? "SCENE_GENESIS" : FightPool(f);
			if (pool == null) return null;
			var ctx = new DialogueContext { Player = s.Record.CommanderName };
			return DialogueSystem.PickLine(pool, s.Record, ctx) ?? DialogueSystem.PickLine(pool, s.Record, ctx, true);
		}

		/// <summary>The only Wrath of the Gods boss up is the rift taking Calamity's Old Duke (a scene the army watches and reacts to through RiftMood, not a fight to hush).</summary>
		public static bool DukeSceneOnly
		{
			get
			{
				if (TestFight.HasValue || !Installed) return false;
				uint tick = Main.GameUpdateCount;
				if (tick == _dukeTick) return _dukeOnly;
				_dukeTick = tick; _dukeOnly = ScanDuke();
				return _dukeOnly;
			}
		}
		private static uint _dukeTick = uint.MaxValue; private static bool _dukeOnly;

		private static bool ScanDuke()
		{
			{
				bool rift = false;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (n == null || !n.active) continue;
					if (n.type == _avatarType || n.type == _namelessType) return false;
					if (n.type == _riftType) { if (StateOf(n) != "KillOldDuke") return false; rift = true; }
				}
				return rift;
			}
		}

		/// <summary>Self-check: the text is one of the fight pools' lines (placeholders matched loosely: the literal parts in order).</summary>
		internal static bool IsFightChat(string text)
		{
			if (string.IsNullOrEmpty(text)) return false;
			foreach (string pool in new[] { "SCENE_GENESIS", "SCENE_RIFT_FIGHT", "SCENE_AVATAR_FIGHT", "SCENE_NAMELESS_FIGHT" })
			{
				var lines = DialogueTable.Get(pool);
				if (lines == null) continue;
				foreach (DialogueLine l in lines)
				{
					int at = 0; bool ok = true;
					foreach (LinePart p in l.Parts)
					{
						if (p.Kind != PlaceholderKind.Literal || string.IsNullOrEmpty(p.Literal)) continue;
						int i = text.IndexOf(p.Literal, at, StringComparison.Ordinal);
						if (i < 0) { ok = false; break; }
						at = i + p.Literal.Length;
					}
					if (ok && l.Parts.Length > 0) return true;
				}
			}
			return false;
		}

		private static bool AnyNpc(int type)
		{
			if (type < 0) return false;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i] != null && Main.npc[i].active && Main.npc[i].type == type) return true;
			return false;
		}
	}
}
