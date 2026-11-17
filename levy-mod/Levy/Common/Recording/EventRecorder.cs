using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Event recorder (format 12, header kind "event"): one wide recording of a whole event, every levy, militia outpost and enemy anywhere in the world, from the event's start until it ends, the army is gone, or the hour cap.
	/// It runs beside the boss recorder (a boss that appears during the event still gets its own recording) and shares only the file writer, the folder and the format helpers with it.
	/// The rules (start, end, result, cap, miniboss list) are documented at the top of <see cref="FightRecFormat"/> and mirrored in tools/recview/recrules.py.
	/// Off while the setting is off; the per-tick cost while no event runs is a few static reads every <see cref="PollEvery"/> ticks.
	/// </summary>
	internal static class EventRecorder
	{
		// ------------------------------------------------------------------------------------------ rules (keep identical to recrules.py)
		/// <summary>One sample every 12 ticks: 5 Hz (the viewer interpolates; an hour of a big event stays near 35 MB).</summary>
		public const int SampleEvery = 12;
		/// <summary>The event state (which events run, results, waves, the army count) is read every this many ticks.</summary>
		public const int StateEvery = 10;
		/// <summary>Hard cap: 60 minutes of event (about 30 MB at 60 levies and a full enemy cap; see FightRecFormat).</summary>
		public const int MaxTicks = 60 * 60 * 60;
		/// <summary>The army is gone: no levy or militia body anywhere for 90 s while the event still runs. A fallen regular respawns after 60 s (LevyConstants.RegularRespawnTicks), so 90 s means nobody came back.</summary>
		public const int ArmyGoneTicks = 90 * 60;
		/// <summary>A recording that ends this soon (an event flag that flickered) is not kept.</summary>
		public const int MinKeepTicks = 300;
		public const int PollEvery = 30, CooldownTicks = 120, MaxProj = 300, ProjCellPx = 100 * 16, FinalMoonWave = 15;
		/// <summary>Terrain is captured in a grid of cells of this many tiles around every levy, militia outpost, enemy and the commander, wherever they go: a window of TerrX x TerrY tiles each side.</summary>
		public const int CellW = 50, CellH = 40, TerrX = 100, TerrY = 50, CellsPerTick = 6;

		// ------------------------------------------------------------------------------------------ self-check overrides
		public static bool? TestEnabled;
		public static int TestMaxTicks, TestArmyGoneTicks;
		public static string LastResult { get; private set; }
		public static string LastPath { get; private set; }
		public static string LastHeader { get; private set; }
		/// <summary>Self-check: the measured cost of the last recording (mean microseconds per tick while recording, worst tick ms, mean microseconds per sample).</summary>
		public static double LastMeanUs, LastMaxMs, LastSampleUs;

		public static bool On;
		private static RecWriter _w;
		private static Player _cmd;
		private static string _path, _dir, _mainId, _startId;
		private static DateTime _startWall;
		private static int _startTick, _frames, _events, _chunks;
		private static bool _gzip;

		// event bookkeeping
		private static readonly List<string> _active = new(), _prevActive = new(), _seen = new();
		private static readonly Dictionary<string, string> _results = new();
		private static readonly HashSet<string> _blocked = new();
		private static int _cooldownUntil, _waveMax, _lastWave = -1, _lastInvSize, _lastInvType, _lastAcidPts = -1, _acidStartPts = -1, _armyGoneSince = -1, _armyPeak, _armyNow;
		private static bool _moonFinal, _dd2Won, _dd2Lost, _pillarsUp;
		private static int _kills, _miniKills, _bossKills, _levyHits, _levyDeaths, _cmdDeaths; private static bool _pDead;
		private static long _dmgAll;

		// names and importance
		private static readonly Dictionary<int, string> _items = new(), _npcs = new(), _projs = new(), _soldiers = new(), _prefixes = new();
		private static readonly Dictionary<int, int> _imp = new(), _typeLife = new();
		private static int _impStamp;

		// levy tracking
		private sealed class LTrack { public int Life; public bool Seen; }
		private static readonly Dictionary<int, LTrack> _lt = new();
		private static readonly HashSet<int> _deadNoted = new();
		private static readonly List<int> _gone = new();
		private static readonly Dictionary<int, (uint tick, int proj)> _projHit = new();
		private static readonly Dictionary<int, int> _lgear = new();
		private static readonly Dictionary<int, long> _hbDef = new();
		private sealed class DmgAcc { public long Dmg; public int Hits, Crits; }
		private static readonly Dictionary<string, DmgAcc> _dmg = new();
		private static int _lastDmgFlush, _lastProg, _outpostHash;

		// terrain grid
		private static readonly HashSet<long> _cells = new(), _centres = new();
		private static readonly Queue<long> _cellQ = new();
		private static readonly HashSet<int> _projCells = new();

		// cost
		private static long _costTicks, _costMax, _costSamples, _sampleTicks; private static int _costN;
		/// <summary>Per-tick cost histogram, microseconds: under 25, 50, 100, 250, 500, 1000, 2000, 5000, 10000, and above.</summary>
		internal static readonly int[] CostEdgesUs = { 25, 50, 100, 250, 500, 1000, 2000, 5000, 10000 };
		private static readonly int[] _hist = new int[10];
		/// <summary>Self-check: time the parts of a sample (levies, enemies, projectiles, the write) in Stopwatch ticks.</summary>
		public static bool ProfileParts; public static long PartL, PartA, PartJ, PartW, PartPoll, PartLevyPoll;
		public static int[] LastHist = new int[10];

		public static string Path => _path;
		public static int Elapsed => (int)(Main.GameUpdateCount - (uint)_startTick);
		public static string MainEvent => _mainId;

		// ------------------------------------------------------------------------------------------ events: which are running
		/// <summary>Naming priority: the first id in this list that a recording saw names it.</summary>
		internal static readonly string[] Priority = { "pillars", "martianmadness", "pirates", "goblinarmy", "frostlegion", "oldonesarmy", "pumpkinmoon", "frostmoon", "acidrain", "solareclipse", "bloodmoon" };

		internal static string DisplayName(string id) => id switch
		{
			"goblinarmy" => "Goblin Army", "frostlegion" => "Frost Legion", "pirates" => "Pirate Invasion", "martianmadness" => "Martian Madness", "pumpkinmoon" => "Pumpkin Moon", "frostmoon" => "Frost Moon",
			"solareclipse" => "Solar Eclipse", "oldonesarmy" => "Old One's Army", "bloodmoon" => "Blood Moon", "pillars" => "Celestial Pillars", "acidrain" => "Acid Rain", _ => id,
		};

		private static bool _acidLooked; private static System.Reflection.FieldInfo _acidOn, _acidPts;
		/// <summary>Calamity's Acid Rain, read by reflection (no hard dependency): ongoing, and the kill points left (they count down to 0 when the event is beaten). -1 without Calamity.</summary>
		internal static bool AcidRain(out int pointsLeft)
		{
			pointsLeft = -1;
			if (!_acidLooked)
			{
				_acidLooked = true;
				try
				{
					if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
					{
						var t = cal.Code?.GetType("CalamityMod.Events.AcidRainEvent");
						_acidOn = t?.GetField("AcidRainEventIsOngoing", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
						_acidPts = t?.GetField("AccumulatedKillPoints", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
					}
				}
				catch (Exception e) { Levy.Instance?.Logger.Warn("[EventRec] Acid Rain lookup failed: " + e.Message); _acidOn = null; }
			}
			if (_acidOn == null) return false;
			try { if (_acidPts?.GetValue(null) is int p) pointsLeft = p; return _acidOn.GetValue(null) is true; }
			catch { return false; }
		}

		internal static bool IsPillar(int type) => type == NPCID.LunarTowerSolar || type == NPCID.LunarTowerVortex || type == NPCID.LunarTowerNebula || type == NPCID.LunarTowerStardust;

		/// <summary>The events running right now, in <see cref="Priority"/> order. The Celestial Pillars count while the lunar apocalypse is up or any pillar stands.</summary>
		internal static void ActiveEvents(List<string> into)
		{
			into.Clear();
			bool pillar = NPC.LunarApocalypseIsUp;
			if (!pillar) for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && IsPillar(n.type)) { pillar = true; break; } }
			if (pillar) into.Add("pillars");
			if (Main.invasionType > 0 && Main.invasionSize > 0)
			{
				string inv = Main.invasionType switch { InvasionID.MartianMadness => "martianmadness", InvasionID.PirateInvasion => "pirates", InvasionID.GoblinArmy => "goblinarmy", InvasionID.SnowLegion => "frostlegion", _ => null };
				if (inv != null) into.Add(inv);
			}
			if (DD2Event.Ongoing) into.Add("oldonesarmy");
			if (Main.pumpkinMoon) into.Add("pumpkinmoon");
			if (Main.snowMoon) into.Add("frostmoon");
			if (AcidRain(out _)) into.Add("acidrain");
			if (Main.eclipse) into.Add("solareclipse");
			if (Main.bloodMoon) into.Add("bloodmoon");
			into.Sort((a, b) => Array.IndexOf(Priority, a).CompareTo(Array.IndexOf(Priority, b)));
		}

		// ------------------------------------------------------------------------------------------ importance
		/// <summary>Hand list of minibosses (importance 1), any event: Goblin Summoner, Flying Dutchman and its cannons, Martian Saucer and its parts, Mourning Wood, Pumpking, Everscream, Santa-NK1, Ice Queen, Mothron,
		/// the Dark Mage and Ogre tiers, the Dreadnautilus; with Calamity also the Cragmaw Mire, Nuclear Terror and Mauler. Bosses (NPC.boss, counted as a boss, the Celestial Pillars, Betsy) are importance 2.</summary>
		internal static readonly HashSet<int> MinibossTypes = new()
		{
			NPCID.GoblinSummoner, NPCID.PirateShip, NPCID.PirateShipCannon, NPCID.MartianSaucer, NPCID.MartianSaucerTurret, NPCID.MartianSaucerCannon, NPCID.MartianSaucerCore,
			NPCID.MourningWood, NPCID.Pumpking, NPCID.PumpkingBlade, NPCID.Everscream, NPCID.SantaNK1, NPCID.IceQueen, NPCID.Mothron,
			NPCID.DD2DarkMageT1, NPCID.DD2DarkMageT3, NPCID.DD2OgreT2, NPCID.DD2OgreT3, NPCID.BloodNautilus,
		};
		private static bool _modMinis;
		private static void AddModMinis()
		{
			if (_modMinis) return; _modMinis = true;
			foreach (string nm in new[] { "CragmawMire", "NuclearTerror", "Mauler" })
				try { if (ModContent.TryFind("CalamityMod", nm, out ModNPC m)) MinibossTypes.Add(m.Type); } catch { }
		}

		/// <summary>The event's own minibosses and bosses by id (for the header's name table).</summary>
		internal static int[] EventRoster(string id) => Array.ConvertAll(RosterOf(id), x => (int)x);
		private static short[] RosterOf(string id) => id switch
		{
			"goblinarmy" => new[] { NPCID.GoblinSummoner },
			"pirates" => new[] { NPCID.PirateShip, NPCID.PirateShipCannon },
			"martianmadness" => new[] { NPCID.MartianSaucerCore, NPCID.MartianSaucer, NPCID.MartianSaucerTurret, NPCID.MartianSaucerCannon },
			"pumpkinmoon" => new[] { NPCID.MourningWood, NPCID.Pumpking, NPCID.PumpkingBlade },
			"frostmoon" => new[] { NPCID.Everscream, NPCID.SantaNK1, NPCID.IceQueen },
			"solareclipse" => new[] { NPCID.Mothron },
			"oldonesarmy" => new[] { NPCID.DD2DarkMageT1, NPCID.DD2DarkMageT3, NPCID.DD2OgreT2, NPCID.DD2OgreT3, NPCID.DD2Betsy },
			"bloodmoon" => new[] { NPCID.BloodNautilus },
			"pillars" => new[] { NPCID.LunarTowerSolar, NPCID.LunarTowerVortex, NPCID.LunarTowerNebula, NPCID.LunarTowerStardust },
			_ => Array.Empty<short>(),
		};

		/// <summary>Fallback miniboss rule: once three or more ordinary enemy types have been seen in the recording, a type whose largest life is at least 6 times their median (and at least 1000) is a miniboss.</summary>
		public const int FallbackRatio = 6, FallbackMinLife = 1000, FallbackMinTypes = 3;

		/// <summary>Importance of a type by the hand list and the boss flags alone: 2 boss, 1 miniboss, 0 otherwise (the fallback may raise a 0 later).</summary>
		internal static int BaseImportance(NPC n)
		{
			if (IsPillar(n.type) || n.type == NPCID.DD2Betsy) return 2;
			AddModMinis();
			if (MinibossTypes.Contains(n.type)) return 1;
			if (n.boss || NPCID.Sets.ShouldBeCountedAsBoss[n.type] || FightRecorder.IsFightBoss(n)) return 2;
			return 0;
		}

		/// <summary>The median of the largest life of every ordinary type seen so far, 0 with fewer than <see cref="FallbackMinTypes"/> types.</summary>
		internal static int NormalMedian(IDictionary<int, int> imp, IDictionary<int, int> life)
		{
			var l = new List<int>(); foreach (var kv in life) if (imp.TryGetValue(kv.Key, out int v) && v == 0) l.Add(kv.Value);
			if (l.Count < FallbackMinTypes) return 0;
			l.Sort(); return l[l.Count / 2];
		}

		internal static bool FallbackMini(int lifeMax, int median) => median > 0 && lifeMax >= FallbackMinLife && lifeMax >= FallbackRatio * median;

		private static int Imp(NPC n)
		{
			if (_imp.TryGetValue(n.type, out int v)) { if (n.lifeMax > _typeLife[n.type]) _typeLife[n.type] = n.lifeMax; return v; }
			v = BaseImportance(n);
			_imp[n.type] = v; _typeLife[n.type] = n.lifeMax;
			if (!_npcs.ContainsKey(n.type)) _npcs[n.type] = n.FullName;
			if (v == 0 && FallbackMini(n.lifeMax, NormalMedian(_imp, _typeLife))) v = _imp[n.type] = 1;
			WriteNt(n.type, v);
			return v;
		}

		private static void WriteNt(int type, int imp)
		{
			Line("{\"k\":\"nt\",\"type\":" + type + ",\"name\":" + FightRecFormat.Esc(_npcs.TryGetValue(type, out string nm) ? nm : Lang.GetNPCNameValue(type)) + ",\"imp\":" + imp + "}");
		}

		/// <summary>Every 10 s: an ordinary type that the fallback now names a miniboss gets a new nt line (the viewer takes the last one).</summary>
		private static void RecheckImportance()
		{
			int med = NormalMedian(_imp, _typeLife); if (med <= 0) return;
			List<int> up = null;
			foreach (var kv in _imp) if (kv.Value == 0 && FallbackMini(_typeLife[kv.Key], med)) (up ??= new List<int>()).Add(kv.Key);
			if (up == null) return;
			foreach (int t in up) { _imp[t] = 1; WriteNt(t, 1); }
		}

		// ------------------------------------------------------------------------------------------ setting and start
		public static bool Enabled
		{
			get
			{
				if (TestEnabled.HasValue) return TestEnabled.Value;
				try { return FightRecConfig.Get?.RecordEvents ?? true; } catch { return true; }
			}
		}

		private static bool ClientActive => (!Main.dedServ || FightRecAuto.TestClient) && Main.netMode != NetmodeID.MultiplayerClient;

		/// <summary>Once per game tick (FightRecSystem.Step): starts a recording when an event runs, samples, stops.</summary>
		public static void Step()
		{
			if (On) { Tick(); return; }
			if (Main.GameUpdateCount % PollEvery != 0 || !ClientActive) return;
			if (!Enabled) { _blocked.Clear(); return; }
			if ((int)Main.GameUpdateCount < _cooldownUntil) return;
			ActiveEvents(_active);
			_blocked.RemoveWhere(id => !_active.Contains(id)); // an event that ended may be recorded again next time
			string first = null; foreach (string id in _active) if (!_blocked.Contains(id)) { first = id; break; }
			if (first == null) return;
			Player cmd = Main.dedServ ? Main.player[0] : Main.LocalPlayer;
			if (cmd == null || !cmd.active) return;
			string p = Start(cmd, first, out string err);
			if (p == null) { FightRecAuto.Post("Event recording could not start: " + err, true); _cooldownUntil = (int)Main.GameUpdateCount + 3600; }
		}

		public static string Start(Player cmd, string eventId, out string error)
		{
			error = null;
			if (On) { error = "already recording"; return null; }
			try
			{
				_dir = FightRecorder.Dir(out string warn);
				if (_dir == null) { error = warn ?? "no recording folder"; return null; }
				_gzip = FightRecorder.GzipOverride ?? true;
				_startWall = DateTime.Now; _startTick = (int)Main.GameUpdateCount; _cmd = cmd;
				_frames = _events = _chunks = 0; _kills = _miniKills = _bossKills = _levyHits = _levyDeaths = _cmdDeaths = 0; _pDead = false; _dmgAll = 0;
				_items.Clear(); _npcs.Clear(); _projs.Clear(); _soldiers.Clear(); _prefixes.Clear(); _imp.Clear(); _typeLife.Clear(); _lt.Clear(); _deadNoted.Clear(); _projHit.Clear(); _lgear.Clear(); _hbDef.Clear(); _dmg.Clear();
				_cells.Clear(); _centres.Clear(); _cellQ.Clear(); _levySlots.Clear(); _seen.Clear(); _results.Clear(); _prevActive.Clear();
				_waveMax = 0; _lastWave = -1; _lastInvSize = Main.invasionSize; _lastInvType = Main.invasionType; _lastAcidPts = -1; _acidStartPts = -1; _armyGoneSince = -1; _armyPeak = 0; _moonFinal = false; _dd2Won = _dd2Lost = false; _pillarsUp = false;
				_lastDmgFlush = 0; _lastProg = -100000; _outpostHash = 0; _impStamp = 0;
				_costTicks = _costMax = _costSamples = _sampleTicks = 0; _costN = 0; Array.Clear(_hist); PartL = PartA = PartJ = PartW = PartPoll = PartLevyPoll = 0;
				_startId = _mainId = eventId;
				string stamp = _startWall.ToString("yyyy-MM-dd_HH-mm");
				_path = System.IO.Path.Combine(_dir, "event_" + eventId + "_" + stamp + (_gzip ? FightRecFolder.Ext : ".jsonl"));
				{ int dup = 2; string b = _path; while (File.Exists(_path) || File.Exists(_path + ".part")) _path = b.Replace(_gzip ? FightRecFolder.Ext : ".jsonl", "_" + dup++ + (_gzip ? FightRecFolder.Ext : ".jsonl")); }
				long cap = FightRecorder.MaxFileBytes > 0 ? FightRecorder.MaxFileBytes : FightRecorder.ConfigFileBytes();
				if (!RecWriter.TryOpen(_path, _gzip, cap, out _w, out string why)) { error = "could not create " + _path + ": " + why; _w = null; return null; }
				ActiveEvents(_active);
				foreach (string id in _active) NoteSeen(id);
				string mods = FightRecorder.ModList(out string mode);
				string ver = Levy.Instance?.Version?.ToString() ?? "?";
				var roster = new StringBuilder("{"); bool first = true;
				foreach (int t in EventRoster(eventId))
				{
					string nm = Lang.GetNPCNameValue(t); _npcs[t] = nm;
					if (!first) roster.Append(','); first = false;
					roster.Append('"').Append(t).Append("\":{\"name\":").Append(FightRecFormat.Esc(nm)).Append(",\"imp\":").Append(IsPillar(t) || t == NPCID.DD2Betsy ? 2 : 1).Append('}');
				}
				roster.Append('}');
				var meta = new Dictionary<string, string> { ["source"] = "auto", ["trigger"] = "event", ["setting"] = "RecordEvents", ["event"] = eventId, ["eventName"] = DisplayName(eventId) };
				string hdr = "{\"k\":\"hdr\",\"format\":" + FightRecFormat.Version + ",\"kind\":\"event\",\"event\":" + FightRecFormat.Esc(eventId) + ",\"eventName\":" + FightRecFormat.Esc(DisplayName(eventId))
					+ ",\"events\":[" + string.Join(",", _active.ConvertAll(FightRecFormat.Esc)) + "],\"meta\":" + FightRecFormat.MetaJson(meta) + ",\"headless\":0,\"levy\":" + FightRecFormat.Esc(ver) + ",\"mods\":" + mods + ",\"mode\":" + FightRecFormat.Esc(mode)
					+ ",\"boss\":" + FightRecFormat.Esc(FightRecFolder.SafeName(DisplayName(eventId), "Event")) + ",\"player\":" + FightRecFormat.Esc(cmd.name) + ",\"startTick\":" + _startTick + ",\"hz\":" + (60 / SampleEvery)
					+ ",\"world\":" + FightRecFormat.Esc(Main.worldName) + ",\"worldW\":" + Main.maxTilesX + ",\"worldH\":" + Main.maxTilesY + ",\"date\":" + FightRecFormat.Esc(DateTime.Now.ToString("s"))
					+ ",\"stateNames\":" + FightRecFormat.Esc(string.Join(",", Enum.GetNames(typeof(SoldierState))))
					+ ",\"schemaP\":" + FightRecFormat.Esc(FightRecFormat.SchemaP) + ",\"flagsP\":" + FightRecFormat.Esc(FightRecFormat.SchemaPFlags) + ",\"schemaJ\":" + FightRecFormat.Esc(FightRecFormat.SchemaJ)
					+ ",\"tkinds\":" + FightRecFormat.Esc(FightRecFormat.TKinds) + ",\"schemaA\":" + FightRecFormat.Esc(FightRecFormat.SchemaA) + ",\"schemaS\":" + FightRecFormat.Esc(FightRecFormat.SchemaS) + ",\"schemaL\":" + FightRecFormat.Esc(FightRecFormat.SchemaL) + ",\"flagsL\":" + FightRecFormat.Esc(FightRecFormat.SchemaLFlags)
					+ ",\"imp\":" + FightRecFormat.Esc(FightRecFormat.ImpNames) + ",\"roster\":" + roster + ",\"wave\":" + CurrentWave(eventId)
					+ ",\"auto\":3,\"gzip\":" + (_gzip ? "1" : "0") + ",\"army\":" + FightRecorder.ArmySummary(_items) + "}";
				LastHeader = hdr;
				_w.WriteLine(hdr, true);
				On = true;
				Outposts(true);
				Levy.Instance?.Logger.Info("[EventRec] recording " + DisplayName(eventId) + ": " + _path);
				return _path;
			}
			catch (Exception e) { error = e.Message; try { _w?.Close(null, null, true, 3000); } catch { } _w = null; On = false; return null; }
		}

		private static void NoteSeen(string id)
		{
			if (_seen.Contains(id)) return;
			_seen.Add(id);
			if (Array.IndexOf(Priority, id) < Array.IndexOf(Priority, _mainId)) _mainId = id;
		}

		// ------------------------------------------------------------------------------------------ per tick
		private static void Tick()
		{
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			try
			{
				if (_cmd == null) { Stop("player gone"); return; }
				if (!Enabled) { Stop("turned off"); return; }
				int el = Elapsed;
				long q0 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
				if (el % StateEvery == 0) { PollState(el); if (!On) return; }
				long q1 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
				PollLevies();
				if (ProfileParts) { long q2 = System.Diagnostics.Stopwatch.GetTimestamp(); PartPoll += q1 - q0; PartLevyPoll += q2 - q1; }
				if (_w.Error != null) { Stop("write error"); return; }
				if (_w.Full) { Stop("size limit"); return; }
				if (el >= (TestMaxTicks > 0 ? TestMaxTicks : MaxTicks)) { Stop("time limit"); return; }
				FlushDmg(el, false);
				PumpTerrain();
				if (el % SampleEvery == 0)
				{
					long s0 = System.Diagnostics.Stopwatch.GetTimestamp();
					Sample(el);
					_sampleTicks += System.Diagnostics.Stopwatch.GetTimestamp() - s0; _costSamples++;
				}
				if (el % 600 == 0) RecheckImportance();
				if (el % 300 == 0) Outposts(false);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[EventRec] tick failed, stopping: " + e); Stop("error: " + e.Message); }
			finally
			{
				// the recorder's own cost per tick; the tick that closes the file is not counted (the writer thread finishes the file)
				if (On)
				{
					long dt = System.Diagnostics.Stopwatch.GetTimestamp() - t0; _costTicks += dt; _costN++; if (dt > _costMax) _costMax = dt;
					double us = dt * 1e6 / System.Diagnostics.Stopwatch.Frequency; int b = 0; while (b < CostEdgesUs.Length && us >= CostEdgesUs[b]) b++; _hist[b]++;
				}
			}
		}

		internal static int CurrentWave(string id) => id switch
		{
			"pumpkinmoon" or "frostmoon" or "oldonesarmy" => NPC.waveNumber,
			_ => 0,
		};

		/// <summary>Each tick: which events run now, each event's end and its result, wave changes, invasion progress, the army-gone timer.</summary>
		private static void PollState(int el)
		{
			_prevActive.Clear(); _prevActive.AddRange(_active);
			ActiveEvents(_active);
			// state of the running events, read every tick so the value just before an event ends is known
			if (Main.invasionType > 0) { _lastInvType = Main.invasionType; _lastInvSize = Main.invasionSize; }
			if (_active.Contains("oldonesarmy") || DD2Event.WonThisRun || DD2Event.LostThisRun) { _dd2Won = DD2Event.WonThisRun; _dd2Lost = DD2Event.LostThisRun; }
			if (AcidRain(out int pts)) { _lastAcidPts = pts; if (_acidStartPts < 0) _acidStartPts = pts; }
			foreach (string id in _active)
			{
				NoteSeen(id);
				if (!_prevActive.Contains(id) && el > 0) Ev("evstart", ("ev", FightRecFormat.Esc(id)), ("name", FightRecFormat.Esc(DisplayName(id))));
			}
			int wave = 0; foreach (string id in _active) wave = Math.Max(wave, CurrentWave(id));
			if (wave != _lastWave && (_active.Contains("pumpkinmoon") || _active.Contains("frostmoon") || _active.Contains("oldonesarmy")))
			{
				_lastWave = wave; _waveMax = Math.Max(_waveMax, wave);
				if ((_active.Contains("pumpkinmoon") || _active.Contains("frostmoon")) && wave >= FinalMoonWave) _moonFinal = true;
				Ev("wave", ("n", wave.ToString()), ("ev", FightRecFormat.Esc(_active.Contains("oldonesarmy") ? "oldonesarmy" : _active.Contains("pumpkinmoon") ? "pumpkinmoon" : "frostmoon")));
			}
			if (el - _lastProg >= 600)
			{
				_lastProg = el;
				if (Main.invasionType > 0 && Main.invasionSizeStart > 0) Ev("prog", ("left", Math.Max(0, Main.invasionSize).ToString()), ("of", Main.invasionSizeStart.ToString()));
				else if (_lastAcidPts >= 0 && _active.Contains("acidrain")) Ev("prog", ("left", Math.Max(0, _lastAcidPts).ToString()), ("of", Math.Max(_acidStartPts, _lastAcidPts).ToString()));
				Ev("kc", ("n", _kills.ToString()), ("mini", _miniKills.ToString()), ("army", _armyNow.ToString()));
			}
			bool pillarNow = _active.Contains("pillars"); if (pillarNow) _pillarsUp = true;
			foreach (string id in _prevActive)
			{
				if (_active.Contains(id)) continue;
				string r = EventResult(id);
				_results[id] = r;
				Ev("evend", ("ev", FightRecFormat.Esc(id)), ("result", FightRecFormat.Esc(r)));
			}
			// the army: every levy or militia body anywhere
			int army = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) army++;
			_armyNow = army; if (army > _armyPeak) _armyPeak = army;
			if (army > 0 || _armyPeak == 0) _armyGoneSince = -1;
			else if (_armyGoneSince < 0) _armyGoneSince = el;
			int grace = TestArmyGoneTicks > 0 ? TestArmyGoneTicks : ArmyGoneTicks;
			if (_active.Count == 0) { Stop("event over"); return; }
			if (_armyGoneSince >= 0 && el - _armyGoneSince >= grace) { Stop("army gone"); return; }
		}

		/// <summary>The result of one event that just ended (see FightRecFormat, EVENT RESULT RULE).</summary>
		private static string EventResult(string id)
		{
			switch (id)
			{
				case "goblinarmy": case "frostlegion": case "pirates": case "martianmadness":
					return DecideInvasion(Main.invasionSize, Main.invasionType);
				case "pumpkinmoon": case "frostmoon": return _moonFinal ? "won" : "survived";
				case "oldonesarmy": return _dd2Won || DD2Event.WonThisRun ? "won" : _dd2Lost || DD2Event.LostThisRun ? "lost" : "ended";
				case "pillars": return "won";
				case "acidrain": return DecideAcid(_acidStartPts, _lastAcidPts);
				default: return "survived"; // Blood Moon at dawn, the Solar Eclipse at dusk
			}
		}

		/// <summary>An invasion that stopped: won when its size had run out (the game clears it at 0), ended when something else took it away (a command, another mod).</summary>
		internal static string DecideInvasion(int sizeAfter, int typeAfter) => sizeAfter <= 0 ? "won" : "ended";

		/// <summary>Acid Rain counts its kill points down to 0; Calamity also zeroes them when the rain stops or no enemy dies for too long. Won when the last points seen were at most 10 % of the start (and at most 2 left), else survived.</summary>
		internal static string DecideAcid(int startPts, int lastPts) => lastPts >= 0 && lastPts <= Math.Max(2, startPts / 10) ? "won" : "survived";

		/// <summary>
		/// The recording's result word: lost when the army was gone, interrupted when the world closed, ended when the setting went off or a write failed; else the result of the main event (the first in <see cref="Priority"/> that the recording saw),
		/// survived when the hour cap or the size cap stopped it.
		/// </summary>
		internal static string DecideRecording(string reason, string mainResult)
		{
			if (reason == "army gone") return "lost";
			if (reason == "world unloaded" || reason == "unloaded") return "interrupted";
			if (reason == "time limit" || reason == "size limit") return "survived";
			if (reason == "event over") return mainResult ?? "ended";
			return "ended";
		}

		// ------------------------------------------------------------------------------------------ stop
		public static string Stop(string reason)
		{
			if (!On) return null;
			On = false;
			var w = _w; _w = null; string final = null;
			bool sync = FightRecorder.ForceSync || reason == "world unloaded" || reason == "unloaded";
			try
			{
				FlushDmgTo(w, Elapsed);
				// events still running when the recording stops get their state as the result
				foreach (string id in _active) if (!_results.ContainsKey(id)) _results[id] = reason == "army gone" ? "lost" : reason == "event over" ? EventResult(id) : "running";
				_results.TryGetValue(_mainId, out string mainRes);
				string result = DecideRecording(reason, mainRes);
				LastResult = result;
				bool discard = Elapsed < MinKeepTicks || _frames < 3;
				w.WriteLine("{\"k\":\"names\",\"items\":" + Names(_items) + ",\"npcs\":" + Names(_npcs) + ",\"projectiles\":" + Names(_projs) + ",\"buffs\":{},\"soldiers\":" + Names(_soldiers) + ",\"prefixes\":" + Names(_prefixes) + "}", true);
				int total = 0; if (RosterSystem.Instance != null) foreach (var r in RosterSystem.Instance.Regulars) if (!r.Reserve) total++;
				var res = new StringBuilder("{"); bool first = true; foreach (var kv in _results) { if (!first) res.Append(','); first = false; res.Append(FightRecFormat.Esc(kv.Key)).Append(':').Append(FightRecFormat.Esc(kv.Value)); } res.Append('}');
				double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				LastMeanUs = _costN > 0 ? _costTicks * f * 1000.0 / _costN : 0; LastMaxMs = _costMax * f; LastSampleUs = _costSamples > 0 ? _sampleTicks * f * 1000.0 / _costSamples : 0;
				LastHist = (int[])_hist.Clone();
				w.WriteLine("{\"k\":\"end\",\"kind\":\"event\",\"reason\":" + FightRecFormat.Esc(reason) + ",\"ticks\":" + Elapsed + ",\"frames\":" + _frames + ",\"events\":" + _events + ",\"result\":" + FightRecFormat.Esc(result)
					+ ",\"durationSec\":" + FightRecFormat.N(Elapsed / 60.0) + ",\"event\":" + FightRecFormat.Esc(_mainId) + ",\"eventName\":" + FightRecFormat.Esc(DisplayName(_mainId)) + ",\"bossName\":" + FightRecFormat.Esc(DisplayName(_mainId))
					+ ",\"eventsSeen\":[" + string.Join(",", _seen.ConvertAll(FightRecFormat.Esc)) + "],\"results\":" + res + ",\"waveMax\":" + _waveMax + ",\"kills\":" + _kills + ",\"minibossKills\":" + _miniKills + ",\"bossKills\":" + _bossKills
					+ ",\"levyHits\":" + _levyHits + ",\"levyDeaths\":" + _levyDeaths + ",\"cmdDeaths\":" + _cmdDeaths + ",\"armyPeak\":" + _armyPeak + ",\"armyAlive\":" + _armyNow + ",\"armyTotal\":" + total + ",\"arenaChunks\":" + _chunks + ",\"dmgAll\":" + _dmgAll
					+ ",\"cpu\":{\"meanUs\":" + FightRecFormat.N(LastMeanUs) + ",\"maxMs\":" + FightRecFormat.N(LastMaxMs) + ",\"sampleUs\":" + FightRecFormat.N(LastSampleUs) + ",\"histUs\":[" + string.Join(",", CostEdgesUs) + "],\"hist\":[" + string.Join(",", _hist) + "]},\"dropped\":" + w.Dropped + ",\"capped\":" + (w.Full ? "1" : "0") + ",\"writeError\":" + FightRecFormat.Esc(w.Error) + "}", true);
				if (!_gzip) { w.Close(null, null, true); final = _path; if (discard) { try { File.Delete(_path); } catch { } final = null; } }
				else
				{
					string to = discard ? null : EventFileName(_dir, _startWall, _mainId, result);
					long folderCap = FightRecorder.MaxFolderBytes > 0 ? FightRecorder.MaxFolderBytes : FightRecorder.ConfigFolderBytes();
					string dir = _dir; int frames = _frames;
					w.Close(to, done => Finished(w, done, discard, dir, folderCap, reason, frames), sync);
					final = to;
				}
				if (!discard) _cooldownUntil = (int)Main.GameUpdateCount + CooldownTicks;
				// an event that ends the recording by itself is not recorded again until it ends; the setting going off, or the world closing, does not block it
				if (reason == "army gone" || reason == "time limit" || reason == "size limit" || reason == "write error") foreach (string id in _active) _blocked.Add(id);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[EventRec] stop failed: " + e.Message); }
			LastPath = final; _path = final; _cmd = null;
			if (final != null && !Main.dedServ && reason != "world unloaded" && reason != "unloaded") Main.NewText("Event recording saved: " + System.IO.Path.GetFileName(final), 160, 255, 160);
			Levy.Instance?.Logger.Info("[EventRec] stopped (" + reason + "), result " + LastResult + ", " + _frames + " samples, cost mean " + LastMeanUs.ToString("0.0") + " us/tick, worst " + LastMaxMs.ToString("0.00") + " ms, sample " + LastSampleUs.ToString("0") + " us: " + (final ?? "discarded"));
			return final;
		}

		/// <summary>event_ID_YYYY-MM-DD_HH-MM_result.levyrec, with _2, _3 ... when that name is taken.</summary>
		internal static string EventFileName(string dir, DateTime when, string id, string result)
		{
			string stem = "event_" + FightRecFolder.SafeName(id, "event") + "_" + when.ToString("yyyy-MM-dd_HH-mm") + "_" + FightRecFolder.SafeName(result, "ended");
			string p = System.IO.Path.Combine(dir, stem + FightRecFolder.Ext); int n = 2;
			while (File.Exists(p) || File.Exists(p + ".part")) p = System.IO.Path.Combine(dir, stem + "_" + n++ + FightRecFolder.Ext);
			return p;
		}

		private static void Finished(RecWriter w, string path, bool discard, string dir, long folderCap, string reason, int frames)
		{
			try
			{
				if (path == null) { if (!discard) FightRecAuto.Post("Event recording could not be saved: " + (w.Error ?? "unknown error") + ".", true); return; }
				if (discard) { try { File.Delete(path); } catch { } return; }
				int gone = FightRecFolder.Enforce(dir, folderCap, path, out long total);
				Levy.Instance?.Logger.Info("[EventRec] saved " + path + " (" + reason + ", " + frames + " samples, " + (w.FileBytes / 1024) + " KB on disk from " + (w.RawBytes / 1024) + " KB, folder " + (total / 1048576) + " MB, " + gone + " old removed)");
				if (w.Error != null) FightRecAuto.Post("Event recording hit a write error (" + w.Error + "). The file may be cut short.", true);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[EventRec] finishing failed: " + e.Message); }
		}

		// ------------------------------------------------------------------------------------------ hooks
		/// <summary>An enemy died (GlobalNPC.OnKill): counted, and a miniboss or boss gets an ekill event.</summary>
		public static void NoteKill(NPC n)
		{
			if (!On || n == null || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || n.lifeMax <= 5 || NPCID.Sets.CountsAsCritter[n.type]) return;
			if (n.realLife >= 0 && n.realLife != n.whoAmI) return;
			try
			{
				int imp = Imp(n); _kills++;
				if (imp >= 1)
				{
					if (imp == 1) _miniKills++; else _bossKills++;
					Ev("ekill", ("npc", n.type.ToString()), ("name", FightRecFormat.Esc(n.FullName)), ("id", n.whoAmI.ToString()), ("imp", imp.ToString()), ("x", ((int)n.Center.X).ToString()), ("y", ((int)n.Center.Y).ToString()), ("max", n.lifeMax.ToString()));
				}
			}
			catch { }
		}

		public static void NoteLevyProjectile(NPC levy, Projectile pr)
		{
			if (!On) return;
			_projHit[levy.whoAmI] = (Main.GameUpdateCount, pr.type);
			if (!_projs.ContainsKey(pr.type)) _projs[pr.type] = Lang.GetProjectileName(pr.type).Value;
		}

		public static void NoteLevyDeath(NPC n)
		{
			if (!On || !(n.ModNPC is SoldierNPC s)) return;
			try
			{
				int id = FightRecorder.LevyId(s, n.whoAmI);
				if (!_deadNoted.Add(id)) return;
				_lt.TryGetValue(id, out var t);
				LevyHit(s, n, id, t != null ? Math.Max(1, t.Life) : 1, 0, true);
				Ev("ldeath", ("id", id.ToString()), ("x", ((int)n.Center.X).ToString()), ("y", ((int)n.Center.Y).ToString()), ("npc", s.LastAttackerType.ToString()), ("by", FightRecFormat.Esc(AttackerKind(s))));
				_levyDeaths++;
				_lt.Remove(id);
			}
			catch { }
		}

		/// <summary>Who struck a levy: boss, miniboss, enemy (an ordinary one) or world.</summary>
		private static string AttackerKind(SoldierNPC s)
		{
			if (s.LastAttackerType <= 0 && s.LastAttackerIndex < 0) return "world";
			int idx = s.LastAttackerIndex;
			if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && Main.npc[idx].type == s.LastAttackerType)
			{
				NPC a = Main.npc[idx]; if (a.realLife >= 0 && a.realLife < Main.maxNPCs && Main.npc[a.realLife].active) a = Main.npc[a.realLife];
				int imp = Imp(a); return imp == 2 ? "boss" : imp == 1 ? "miniboss" : "enemy";
			}
			if (s.LastAttackerType > 0 && _imp.TryGetValue(s.LastAttackerType, out int v)) return v == 2 ? "boss" : v == 1 ? "miniboss" : "enemy";
			return "enemy";
		}

		private static void LevyHit(SoldierNPC s, NPC n, int id, int dmg, int lifeAfter, bool kill)
		{
			bool proj = _projHit.TryGetValue(n.whoAmI, out var ph) && Main.GameUpdateCount - ph.tick <= 1;
			int at = s.LastAttackerType;
			if (at > 0 && !_npcs.ContainsKey(at)) _npcs[at] = Lang.GetNPCNameValue(at);
			string by = AttackerKind(s);
			Ev("lhit", ("id", id.ToString()), ("dmg", dmg.ToString()), ("src", FightRecFormat.Esc(proj ? "proj" : by == "world" ? "other" : "body")), ("by", FightRecFormat.Esc(by)), ("npc", at.ToString()), ("proj", (proj ? ph.proj : -1).ToString()),
				("life", Math.Max(0, lifeAfter).ToString()), ("max", n.lifeMax.ToString()), ("x", ((int)n.Center.X).ToString()), ("y", ((int)n.Center.Y).ToString()), ("kill", kill ? "1" : "0"));
			_levyHits++;
		}

		/// <summary>The NPC slots of the levy bodies at the last sample: the hit poll walks only these (a body that appeared since is picked up at the next sample).</summary>
		private static readonly List<int> _levySlots = new();

		private static void PollLevies()
		{
			foreach (var t in _lt.Values) t.Seen = false;
			foreach (int i in _levySlots)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC s) || n.life <= 0) continue;
				int id = FightRecorder.LevyId(s, n.whoAmI);
				if (_deadNoted.Contains(id)) { if (n.life >= n.lifeMax / 2) _deadNoted.Remove(id); else continue; } // a levy that respawned under the same id is tracked again
				if (!_lt.TryGetValue(id, out var t)) { t = new LTrack { Life = n.life }; _lt[id] = t; }
				if (n.life < t.Life) LevyHit(s, n, id, t.Life - n.life, n.life, false);
				t.Life = n.life; t.Seen = true;
			}
			_gone.Clear();
			foreach (var kv in _lt) if (!kv.Value.Seen) _gone.Add(kv.Key);
			foreach (int id in _gone) _lt.Remove(id);
		}

		/// <summary>One hit on an enemy by anyone (the GlobalNPC hooks). Added up and written once a second as dmg rows; field 4 is the target's importance (0 normal, 1 miniboss, 2 boss).</summary>
		internal static void NoteDamage(NPC npc, Player byPlayer, Projectile proj, Item item, int dmg, bool crit)
		{
			if (!On || dmg <= 0 || npc == null) return;
			try
			{
				NPC root = npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active ? Main.npc[npc.realLife] : npc;
				int imp = Imp(root);
				string owner, cls, kind; int type; int srcItem = 0;
				if (proj != null)
				{
					proj.TryGetGlobalProjectile(out SoldierProjectileTag tag);
					if (tag != null && tag.SoldierIndex >= 0) owner = "levy";
					else if (tag != null && tag.DroneShot) owner = "drone";
					else if (tag != null && tag.CommanderIndex >= 0) owner = "levy";
					else if (_cmd != null && proj.owner == _cmd.whoAmI) owner = "cmd";
					else owner = "other";
					cls = FightRecorder.ClassName(proj.DamageType); kind = "proj"; type = proj.type;
					if (!_projs.ContainsKey(type)) _projs[type] = Lang.GetProjectileName(type).Value;
					if (proj.TryGetGlobalProjectile(out FightRecProjTag pt) && pt.SrcItem > 0) { srcItem = pt.SrcItem; if (!_items.ContainsKey(srcItem)) _items[srcItem] = Lang.GetItemNameValue(srcItem); }
				}
				else
				{
					owner = byPlayer != null && global::Levy.Common.Owner.SoldierOwner.IsDummy(byPlayer) ? "levy" : byPlayer != null && byPlayer == _cmd ? "cmd" : "other";
					cls = FightRecorder.ClassName(item?.DamageType); kind = "item"; type = item != null ? item.type : 0; srcItem = type;
					if (item != null && !_items.ContainsKey(type)) _items[type] = item.Name;
				}
				string key = owner + "|" + cls + "|" + kind + "|" + type + "|" + imp + "|" + srcItem;
				if (!_dmg.TryGetValue(key, out var a)) _dmg[key] = a = new DmgAcc();
				a.Dmg += dmg; a.Hits++; if (crit) a.Crits++; _dmgAll += dmg;
			}
			catch { }
		}

		private static void FlushDmg(int el, bool force)
		{
			if (_dmg.Count == 0) { _lastDmgFlush = el; return; }
			if (!force && el - _lastDmgFlush < 60) return;
			_lastDmgFlush = el;
			FlushDmgTo(_w, el);
		}

		private static void FlushDmgTo(RecWriter w, int el)
		{
			if (_dmg.Count == 0 || w == null) return;
			var sb = new StringBuilder("["); bool first = true;
			foreach (var kv in _dmg)
			{
				string[] k = kv.Key.Split('|');
				if (!first) sb.Append(','); first = false;
				sb.Append('[').Append(FightRecFormat.Esc(k[0])).Append(',').Append(FightRecFormat.Esc(k[1])).Append(',').Append(FightRecFormat.Esc(k[2])).Append(',').Append(k[3]).Append(',').Append(k[4]).Append(',').Append(kv.Value.Dmg).Append(',').Append(kv.Value.Hits).Append(',').Append(k[5]).Append(",0,").Append(kv.Value.Crits).Append(']');
			}
			_dmg.Clear();
			w.WriteLine(FightRecFormat.Event(el, "dmg", ("d", sb.Append(']').ToString()))); _events++;
		}

		// ------------------------------------------------------------------------------------------ sampling
		private static void Line(string s) { _w?.WriteLine(s); }
		private static void Ev(string kind, params (string, string)[] extra) { Line(FightRecFormat.Event(Elapsed, kind, extra)); _events++; }

		private static string Names(Dictionary<int, string> d)
		{
			var sb = new StringBuilder("{"); bool first = true;
			foreach (var kv in d) { if (!first) sb.Append(','); first = false; sb.Append('"').Append(kv.Key).Append("\":").Append(FightRecFormat.Esc(kv.Value)); }
			return sb.Append('}').ToString();
		}

		private static long CellKey(int cx, int cy) => ((long)cx << 32) | (uint)cy;

		/// <summary>Queues the terrain cells around a world position that are not captured yet.</summary>
		private static void WantTerrain(float px, float py)
		{
			int tx = (int)(px / 16f), ty = (int)(py / 16f);
			if (!_centres.Add(CellKey(tx / CellW, ty / CellH))) return; // the window around this cell was queued already
			int cx0 = Math.Max(0, (tx - TerrX) / CellW), cx1 = Math.Min((Main.maxTilesX - 1) / CellW, (tx + TerrX) / CellW);
			int cy0 = Math.Max(0, (ty - TerrY) / CellH), cy1 = Math.Min((Main.maxTilesY - 1) / CellH, (ty + TerrY) / CellH);
			for (int cx = cx0; cx <= cx1; cx++)
				for (int cy = cy0; cy <= cy1; cy++) { long k = CellKey(cx, cy); if (_cells.Add(k)) _cellQ.Enqueue(k); }
		}

		/// <summary>Writes up to <see cref="CellsPerTick"/> queued terrain cells (arena records, the format of the boss recorder's terrain).</summary>
		private static void PumpTerrain()
		{
			for (int n = 0; n < CellsPerTick && _cellQ.Count > 0; n++)
			{
				long k = _cellQ.Dequeue(); int cx = (int)(k >> 32), cy = (int)(k & 0xffffffff);
				int x0 = cx * CellW, y0 = cy * CellH, x1 = Math.Min(Main.maxTilesX, x0 + CellW), y1 = Math.Min(Main.maxTilesY, y0 + CellH);
				Line(TerrainChunk(x0, y0, x1, y1)); _chunks++;
			}
		}

		/// <summary>One arena record of the tiles [x0,x1) x [y0,y1): rows of run-length pairs (0 air, 1 solid, 2 platform, 3 liquid).</summary>
		internal static string TerrainChunk(int x0, int y0, int x1, int y1)
		{
			int w = x1 - x0, h = y1 - y0;
			var rows = new List<int[]>(h); var codes = new int[Math.Max(0, w)];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					Tile t = Framing.GetTileSafely(x0 + x, y0 + y);
					int c = 0;
					if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) c = 1;
					else if (t.HasUnactuatedTile && Main.tileSolidTop[t.TileType]) c = 2;
					else if (t.LiquidAmount > 100) c = 3;
					codes[x] = c;
				}
				rows.Add(FightRecFormat.RleEncode(codes).ToArray());
			}
			return FightRecFormat.Arena(x0, y0, w, h, rows);
		}

		/// <summary>The militia outposts (every Barracks): written at the start and again when one appears, goes, mobilizes or stands down. [id, x, y, mobilized, militia, name].</summary>
		private static void Outposts(bool force)
		{
			var sb = new StringBuilder("["); int h = 17; bool first = true; int n = 0;
			try
			{
				foreach (var te in TileEntity.ByID.Values)
				{
					if (!(te is BarracksTE b)) continue;
					h = h * 31 + b.ID; h = h * 31 + (b.Mobilized ? 1 : 0); h = h * 31 + b.Militia.Count;
					if (!first) sb.Append(','); first = false; n++;
					sb.Append('[').Append(b.ID).Append(',').Append((int)b.Center.X).Append(',').Append((int)b.Center.Y).Append(',').Append(b.Mobilized ? 1 : 0).Append(',').Append(b.Militia.Count).Append(',').Append(FightRecFormat.Esc(b.TownName ?? "")).Append(']');
					WantTerrain(b.Center.X, b.Center.Y);
				}
			}
			catch { }
			if (!force && h == _outpostHash) return;
			_outpostHash = h;
			Ev("outposts", ("o", sb.Append(']').ToString()));
		}

		private static readonly StringBuilder _sb = new(65536);
		private static readonly char[] _dig = new char[12];
		/// <summary>An integer in invariant digits (no culture minus sign, no allocation).</summary>
		private static void I(StringBuilder sb, int v)
		{
			if (v == 0) { sb.Append('0'); return; }
			long x = v; if (x < 0) { sb.Append('-'); x = -x; }
			int n = 0; while (x > 0) { _dig[n++] = (char)('0' + x % 10); x /= 10; }
			while (n > 0) sb.Append(_dig[--n]);
		}
		/// <summary>A value to one decimal ("3", "-1.5").</summary>
		private static void D1(StringBuilder sb, float v)
		{
			if (float.IsNaN(v) || float.IsInfinity(v)) { sb.Append('0'); return; }
			int t = (int)Math.Round(v * 10f);
			if (t % 10 == 0) { I(sb, t / 10); return; }
			if (t < 0) { sb.Append('-'); t = -t; }
			I(sb, t / 10); sb.Append('.').Append((char)('0' + t % 10));
		}
		private static void C(StringBuilder sb) => sb.Append(',');

		private static void Sample(int el)
		{
			Player p = _cmd;
			var sb = _sb; sb.Clear();
			Item held = p.HeldItem;
			bool grounded = Math.Abs(p.velocity.Y) < 0.01f;
			int flags = (grounded ? 1 : 0) | (p.controlUseItem ? 1024 : 0) | (p.mount.Active ? 4096 : 0) | (p.dead ? 8192 : 0);
			int heldType = held != null && !held.IsAir ? held.type : 0;
			if (heldType > 0 && !_items.ContainsKey(heldType)) _items[heldType] = held.Name;
			// p: SchemaP, the commander (x,y,vx,vy,dir,flags,life,mana,immune 0,wingTime 0,rocketTime 0,held,mount,hookX 0,hookY 0,dashDelay 0)
			sb.Append("{\"k\":\"f\",\"t\":"); I(sb, el); sb.Append(",\"p\":[");
			I(sb, (int)p.Center.X); C(sb); I(sb, (int)p.Center.Y); C(sb); D1(sb, p.velocity.X); C(sb); D1(sb, p.velocity.Y); C(sb); I(sb, p.direction); C(sb); I(sb, flags); C(sb); I(sb, p.statLife); C(sb); I(sb, p.statMana);
			sb.Append(",0,0,0,"); I(sb, heldType); C(sb); I(sb, p.mount.Active ? p.mount.Type : -1); sb.Append(",0,0,0]");
			if (p.dead && !_pDead) { _cmdDeaths++; Ev("cdeath", ("x", ((int)p.Center.X).ToString()), ("y", ((int)p.Center.Y).ToString()), ("n", _cmdDeaths.ToString())); }
			_pDead = p.dead;
			_projCells.Clear();
			void Anchor(float x, float y) { WantTerrain(x, y); int k = ((int)(x / ProjCellPx) << 16) ^ (int)(y / ProjCellPx); _projCells.Add(k); }
			if (!p.dead) Anchor(p.Center.X, p.Center.Y);
			bool gearTick = el % 60 < SampleEvery;
			long z0 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			// l: every levy and militia body, wherever it is (SchemaL)
			bool first = true; _levySlots.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC s)) continue;
				_levySlots.Add(i);
				int id = FightRecorder.LevyId(s, n.whoAmI);
				if (s.Record != null && !_soldiers.ContainsKey(id)) _soldiers[id] = s.Record.Name;
				Anchor(n.Center.X, n.Center.Y);
				if (gearTick) LevyGear(s, id);
				Item lh = s.Dummy?.HeldItem;
				int lt = lh != null && !lh.IsAir ? lh.type : 0;
				if (lt > 0 && !_items.ContainsKey(lt)) _items[lt] = lh.Name;
				int lflags = (Math.Abs(n.velocity.Y) < 0.01f ? 1 : 0) | (s.IsFlying ? 2 : 0) | (s.IsAerial ? 4 : 0) | (s.IsHooking ? 8 : 0) | (s.MoveBlocked ? 16 : 0);
				sb.Append(first ? ",\"l\":[[" : ",["); first = false;
				I(sb, id); C(sb); I(sb, (int)n.Center.X); C(sb); I(sb, (int)n.Center.Y); C(sb); D1(sb, n.velocity.X); C(sb); D1(sb, n.velocity.Y); C(sb); I(sb, lflags); C(sb); I(sb, n.life); C(sb);
				I(sb, s.OfficerEngaged ? (int)SoldierState.Engage : (int)s.State); C(sb); I(sb, lt); C(sb); I(sb, s.WingTime); C(sb); I(sb, n.lifeMax); C(sb); I(sb, n.width); C(sb); I(sb, n.height); sb.Append(']');
			}
			if (!first) sb.Append(']');
			long z1 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			// a: every enemy that is not a worm body segment, wherever it is (SchemaA); s: worm body segments (SchemaS: full rows when new or changed, else [id,x,y])
			first = true; var segs = _segRows; segs.Clear();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC || n.type <= 0 || n.lifeMax <= 5 || NPCID.Sets.CountsAsCritter[n.type]) continue;
				if (n.dontCountMe && n.damage <= 0 && n.realLife < 0) continue;
				if (n.realLife >= 0 && n.realLife != n.whoAmI) { segs.Add(i); continue; }
				int imp = Imp(n);
				WantTerrain(n.Center.X, n.Center.Y); // terrain wherever the fight goes: around every enemy too (each 50 x 40 cell's window is queued once)
				Target(n, out int tk, out int tl);
				sb.Append(first ? ",\"a\":[[" : ",["); first = false;
				I(sb, n.type); C(sb); I(sb, i); C(sb); I(sb, (int)n.Center.X); C(sb); I(sb, (int)n.Center.Y); C(sb); D1(sb, n.velocity.X); C(sb); D1(sb, n.velocity.Y); C(sb); I(sb, n.life); C(sb); I(sb, n.lifeMax); C(sb);
				I(sb, tk); C(sb); I(sb, tl); C(sb); I(sb, n.width); C(sb); I(sb, n.height); sb.Append(']');
			}
			if (!first) sb.Append(']');
			if (segs.Count > 0)
			{
				sb.Append(",\"s\":[");
				for (int k = 0; k < segs.Count; k++)
				{
					NPC n = Main.npc[segs[k]]; int i = segs[k];
					if (k > 0) C(sb);
					int fl = (n.damage > 0 ? 1 : 0) | (n.dontTakeDamage ? 2 : 0) | 4;
					long def = ((((long)n.type * 4099 + n.width) * 4099 + n.height) * 4099 + (n.realLife + 1)) * 8 + fl;
					if (!_npcs.ContainsKey(n.type)) _npcs[n.type] = n.FullName;
					if (_hbDef.TryGetValue(i, out long old) && old == def) { sb.Append('['); I(sb, i); C(sb); I(sb, (int)n.Center.X); C(sb); I(sb, (int)n.Center.Y); sb.Append(']'); }
					else { _hbDef[i] = def; sb.Append('['); I(sb, n.type); C(sb); I(sb, i); C(sb); I(sb, (int)n.Center.X); C(sb); I(sb, (int)n.Center.Y); C(sb); I(sb, n.width); C(sb); I(sb, n.height); C(sb); I(sb, n.realLife); C(sb); I(sb, fl); sb.Append(']'); }
				}
				sb.Append(']');
			}
			long z2 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			// j: hostile projectiles near the army or the commander (within ~100 tiles), at most MaxProj (SchemaJ)
			int pc = 0; first = true;
			for (int i = 0; i < Main.maxProjectiles && pc < MaxProj; i++)
			{
				Projectile pr = Main.projectile[i];
				if (!pr.active || !pr.hostile) continue;
				int cx = (int)(pr.Center.X / ProjCellPx), cy = (int)(pr.Center.Y / ProjCellPx); bool near = false;
				for (int dx = -1; dx <= 1 && !near; dx++) for (int dy = -1; dy <= 1 && !near; dy++) if (_projCells.Contains(((cx + dx) << 16) ^ (cy + dy))) near = true;
				if (!near) continue;
				if (!_projs.ContainsKey(pr.type)) _projs[pr.type] = Lang.GetProjectileName(pr.type).Value;
				sb.Append(first ? ",\"j\":[[" : ",["); first = false;
				I(sb, pr.type); C(sb); I(sb, (int)pr.Center.X); C(sb); I(sb, (int)pr.Center.Y); C(sb); D1(sb, pr.velocity.X); C(sb); D1(sb, pr.velocity.Y); C(sb); I(sb, pr.damage); C(sb); I(sb, pr.width); C(sb); I(sb, pr.height); sb.Append(']');
				pc++;
			}
			if (!first) sb.Append(']');
			sb.Append('}');
			long z3 = ProfileParts ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
			Line(sb.ToString());
			_frames++;
			if (ProfileParts) { long z4 = System.Diagnostics.Stopwatch.GetTimestamp(); PartL += z1 - z0; PartA += z2 - z1; PartJ += z3 - z2; PartW += z4 - z3; }
		}
		private static readonly List<int> _segRows = new();

		/// <summary>What an enemy attacks (SchemaA tkind / tlevy): 2 a levy (the targeting lock, or an NPC target of 300 and up), 1 a player, 0 none. The boss rule's spite (kind 3) is a boss matter and is left to the boss recorder.</summary>
		private static void Target(NPC n, out int kind, out int levyId)
		{
			kind = 0; levyId = -1;
			int st = -1;
			try { st = LevyNpcData.Of(n).SoldierTarget; } catch { }
			if (!(st >= 0 && st < Main.maxNPCs && Main.npc[st].active && Main.npc[st].ModNPC is SoldierNPC))
				st = n.target >= 300 && n.target - 300 < Main.maxNPCs && Main.npc[n.target - 300].active && Main.npc[n.target - 300].ModNPC is SoldierNPC ? n.target - 300 : -1;
			if (st >= 0) { kind = 2; levyId = FightRecorder.LevyId((SoldierNPC)Main.npc[st].ModNPC, st); return; }
			if (n.target >= 0 && n.target < Main.maxPlayers) kind = 1;
		}

		private static string GearItem(Item it, int slot)
		{
			if (it == null || it.IsAir) return null;
			if (!_items.ContainsKey(it.type)) _items[it.type] = it.Name;
			if (it.prefix > 0 && !_prefixes.ContainsKey(it.prefix)) { try { _prefixes[it.prefix] = Lang.prefix[it.prefix].Value; } catch { } }
			return "[" + slot + "," + it.type + "," + it.prefix + "," + it.stack + "]";
		}

		private static int GearHash(Item it, int h) => it == null || it.IsAir ? h * 31 + 7 : (h * 31 + it.type) * 31 + it.prefix * 7;

		/// <summary>One levy's gear (the lgear event of the boss recorder): at first sight and when it changes, checked once a second.</summary>
		private static void LevyGear(SoldierNPC s, int id)
		{
			var g = s.Record?.Gear; if (g == null) return;
			int h = 17;
			h = GearHash(g.Weapon, h); h = GearHash(g.Sidearm, h); h = GearHash(g.Head, h); h = GearHash(g.Body, h); h = GearHash(g.Legs, h);
			for (int i = 0; i < g.Accessories.Length; i++) h = GearHash(g.Accessories[i], h);
			h = GearHash(g.Ammo, h); h = GearHash(g.Hook, h); h = GearHash(g.Wings, h); h = GearHash(g.Boots, h);
			if (_lgear.TryGetValue(id, out int was) && was == h) return;
			_lgear[id] = h;
			var l = new List<string>();
			void Add(Item it, int slot) { string x = GearItem(it, slot); if (x != null) l.Add(x); }
			Add(g.Weapon, 0); Add(g.Sidearm, 1); Add(g.Head, 2); Add(g.Body, 3); Add(g.Legs, 4);
			for (int i = 0; i < g.Accessories.Length; i++) Add(g.Accessories[i], 5 + i);
			Add(g.Ammo, 11); Add(g.Hook, 12); Add(g.Wings, 13); Add(g.Boots, 14);
			Ev("lgear", ("id", id.ToString()), ("g", "[" + string.Join(",", l) + "]"));
		}

		/// <summary>Self-check: forget the pause after a recording and the events a finished recording blocked.</summary>
		public static void ClearCooldown() { _cooldownUntil = 0; _blocked.Clear(); }
	}
}
