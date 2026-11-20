using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// THE SAVE-RACE CHECK ("[SwapRace]" lines, LEVY_SELFCHECK_WORLD=1 on a dedicated server; LEVY_SWAPRACE_ONLY=1 runs it alone; LEVY_SWAPRACE_REPRO=1 adds the unguarded phase).
	/// The army lends "the local player" for a few microseconds at a time (SoldierNPC.DebuffDot, the player-hit and gear frames set Main.myPlayer, and the frame Main.dedServ). tML saves the character on a
	/// thread-pool thread. This check runs the damage-over-time step and the hit-sequence frame on many levies for hundreds of ticks (Main.myPlayer = 0, the single-player situation) while a second thread
	/// repeatedly calls tML's own autosave entry (<c>Player.SavePlayer(Main.ActivePlayerFileData)</c>, what WorldGen.saveToonWhilePlayingCallBack runs) and a third thread polls <c>Main.myPlayer</c>.
	/// <list type="bullet">
	/// <item>SAMPLER: how often and for how long the swap is live (independent of any guard: it measures the exposure, and proves the run really swaps);</item>
	/// <item>SAVE PATH: a ModPlayer (<see cref="SwapProbePlayer"/>) reads the globals inside the save (PreSavePlayer, SaveData, PostSavePlayer), as every other mod's hook does;</item>
	/// <item>pass: with the gate on, the save path never saw a swapped Main.myPlayer / Main.LocalPlayer / slot 255, no save threw, and the damage over time lost exactly what it loses with no save running.</item>
	/// </list>
	/// With LEVY_SWAPRACE_REPRO=1 the same run is repeated with <see cref="SaveGate.Enabled"/> off (the swap exactly as it was): lab only, a crash there is logged by tML and the exception count of that run is not zero.
	/// </summary>
	public class SwapRaceCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "SwapRaceTester";
		private static volatile bool _guard;       // WorldGen.SaveAndQuit is swallowed while a phase runs (a failed save calls it; on a test server it would end the run)
		private static int _quitCalls;
		public static volatile bool Probing;
		private static Player _orig255; private static bool _mapOrig; private static FieldInfo _achField; private static object _achOrig;

		public override void Load()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1") return;
			try { MonoModHooks.Add(typeof(WorldGen).GetMethod("SaveAndQuit", BindingFlags.Public | BindingFlags.Static), (Action<Action<Action>, Action>)QuitDetour); }
			catch (Exception e) { Mod.Logger.Warn("[SwapRace] could not hook WorldGen.SaveAndQuit: " + e.Message); }
			// tML's own autosave is queued on the thread pool by Player.KillMe for the local player (and by the ten-minute timer on a client). A test server has no achievement manager, so that save throws on a pool
			// thread with no catch and ends the process. While the check's server stubs are in place the queued save is counted and waited for; at any time on a test server a throw from it is logged, not fatal.
			try { MonoModHooks.Add(typeof(WorldGen).GetMethod("saveToonWhilePlaying", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes), (Action<Action>)QueueDetour); }
			catch (Exception e) { Mod.Logger.Warn("[SwapRace] could not hook WorldGen.saveToonWhilePlaying: " + e.Message); }
			try { MonoModHooks.Add(typeof(WorldGen).GetMethod("saveToonWhilePlayingCallBack", BindingFlags.Public | BindingFlags.Static), (Action<Action<object>, object>)CallbackDetour); }
			catch (Exception e) { Mod.Logger.Warn("[SwapRace] could not hook WorldGen.saveToonWhilePlayingCallBack: " + e.Message); }
		}
		private static volatile bool _stubs;      // the check's server stubs (achievement manager, mapEnabled, ActivePlayerFileData) are in place
		private static int _pending, _poolThrew, _queuedLate; private static int _loggedQueue, _loggedThrow;
		private static void QueueDetour(Action orig)
		{
			if (_stubs) Interlocked.Increment(ref _pending); else Interlocked.Increment(ref _queuedLate);
			if (Interlocked.Exchange(ref _loggedQueue, 1) == 0) Mod_Log("tML queued a character autosave on the thread pool (first one; Player.KillMe of the local player does this) from: " + (Environment.StackTrace ?? "").Replace("\n", " | ").Replace("\r", ""));
			orig();
		}
		private static void CallbackDetour(Action<object> orig, object ctx)
		{
			try { orig(ctx); }
			catch (Exception e)
			{
				Interlocked.Increment(ref _poolThrew);
				if (Interlocked.Exchange(ref _loggedThrow, 1) == 0) Mod_Log("an autosave tML queued threw on the pool and was caught (a test server has no achievement manager; first one): " + e.GetType().Name + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim());
			}
			finally { int n; do { n = Volatile.Read(ref _pending); } while (n > 0 && Interlocked.CompareExchange(ref _pending, n - 1, n) != n); }
		}
		private static void Mod_Log(string s) => ModContent.GetInstance<SwapRaceCheck>()?.Mod.Logger.Info("[SwapRace] OBSERVED " + s);
		private static void QuitDetour(Action<Action> orig, Action cb) { if (_guard) { Interlocked.Increment(ref _quitCalls); return; } orig(cb); }

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_SWAPRACEWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }

		/// <summary>Standalone entry (LEVY_SWAPRACE_ONLY=1) and the tail of SummonWorldCheck's chain.</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = LevyUtil.SpEmulation; int my0 = Main.myPlayer; var afd = Main.ActivePlayerFileData; _orig255 = Main.player[Main.maxPlayers];
			try { LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; Probing = false; _guard = false; DrainSaves(); try { _achField?.SetValue(Main.instance, _achOrig); Main.mapEnabled = _mapOrig; } catch (Exception) { } _stubs = false; LevyUtil.SpEmulation = sp; Main.myPlayer = my0; Main.ActivePlayerFileData = afd; Main.netMode = NetmodeID.Server; Cleanup(); SaveGate.Enabled = Environment.GetEnvironmentVariable("LEVY_SAVEGATE") != "0"; }
			_mod.Logger.Info($"[SwapRace] self-check pass={_pass} fail={_fail}");
		}
		/// <summary>No save this check started or provoked may still be running or queued when the stubs come out: waits for the pool's saves (up to 15 s, noted when it gives up).</summary>
		private static void DrainSaves()
		{
			long t0 = Stopwatch.GetTimestamp();
			while (Volatile.Read(ref _pending) > 0 && Stopwatch.GetElapsedTime(t0).TotalSeconds < 15) Thread.Sleep(5);
			int left = Volatile.Read(ref _pending);
			Note($"saves queued on the pool and finished before the stubs were removed: waited {Stopwatch.GetElapsedTime(t0).TotalMilliseconds:0} ms, {left} still pending, {_poolThrew} threw (caught), {_queuedLate} queued with no stubs");
		}
		private static void Check(string n, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[SwapRace] {(ok ? "PASS" : "FAIL")} {n}"); }
		private static void Note(string s) => _mod.Logger.Info("[SwapRace] OBSERVED " + s);

		// ---- what the save path saw (written by SwapProbePlayer on the saving thread) ----
		private static long _points, _myWrong, _localWrong, _slot255Wrong, _onWorkerThread;
		private static int _mainThreadId;
		internal static void Sample()
		{
			if (!Probing) return;
			Interlocked.Increment(ref _points);
			if (Environment.CurrentManagedThreadId != _mainThreadId) Interlocked.Increment(ref _onWorkerThread);
			int my = Volatile.Read(ref Main.myPlayer);
			if (my != 0) Interlocked.Increment(ref _myWrong);
			Player lp = Main.player[Volatile.Read(ref Main.myPlayer)];
			if (!ReferenceEquals(lp, Main.player[0])) Interlocked.Increment(ref _localWrong);
			if (!ReferenceEquals(Main.player[Main.maxPlayers], _orig255)) Interlocked.Increment(ref _slot255Wrong);
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; } else if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.active = false; }
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
			catch (Exception) { }
		}

		private static void Arena()
		{
			for (int i = Cx - 200; i <= Cx + 200; i++)
			{
				for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
			}
		}
		private static SoldierNPC MakeLevy(Vector2 at, int n)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. SwapRace" + n, CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Hold;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at; Main.npc[body].lifeMax = Main.npc[body].life = 4000000; rec.CurrentHp = 4000000;
			return Main.npc[body].ModNPC as SoldierNPC;
		}
		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander; if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500; p.statLife = p.statLifeMax2;
				p.position = new Vector2(Cx * 16f - 600f, (FloorY - 3) * 16f); p.velocity = Vector2.Zero; Main.dayTime = false; Main.time = 18000;
				WorldStep.Update();
			}
		}

		private sealed class Phase
		{
			public string Name; public bool Gate;
			public long Samples, SwappedSamples, Runs; public double WindowSumUs, WindowMaxUs, ElapsedMs;
			public long Saves, SaveThrows; public string FirstThrow;
			public long Points, MyWrong, LocalWrong, Slot255Wrong, OnWorker; public int Quits;
			public long DotDamage, DotTicks; public double TickAvgMs, TickMaxMs; public long GateSwaps, GateWaits; public double GateMaxWaitMs, GateMaxSaveMs;
		}

		/// <summary>Saves the gated phase waits for before it ends (the verdict needs 3).</summary>
		private const int MinSaves = 4;

		private static Phase RunPhase(string name, bool gate, List<SoldierNPC> levies, NPC attacker, bool withSaver, int dotTicks)
		{
			var ph = new Phase { Name = name, Gate = gate };
			SaveGate.Enabled = gate; SaveGate.ResetCounters();
			foreach (var l in levies)
			{
				NPC b = l.NPC; b.life = b.lifeMax; b.netUpdate = false; Array.Clear(b.buffType); Array.Clear(b.buffTime); b.buffImmune[BuffID.OnFire] = false; b.buffImmune[BuffID.Poisoned] = false; l.DebuffDot(); /* no debuff: clears the step's carried regeneration remainder, so every phase starts equal */
				b.AddBuff(BuffID.OnFire, 60000); b.AddBuff(BuffID.Poisoned, 60000);
				b.defense = 0; l.Defense = 0;
			}
			long dot0 = SoldierNPC.DotDamage, dt0 = SoldierNPC.DotTicks, dotEnd = dot0, dtEnd = dt0;
			Interlocked.Exchange(ref _points, 0); Interlocked.Exchange(ref _myWrong, 0); Interlocked.Exchange(ref _localWrong, 0); Interlocked.Exchange(ref _slot255Wrong, 0); Interlocked.Exchange(ref _onWorkerThread, 0); Interlocked.Exchange(ref _quitCalls, 0);
			_mainThreadId = Environment.CurrentManagedThreadId;
			volatile_stop = false; Probing = withSaver; _guard = true;

			// the sampler: polls Main.myPlayer, never takes the gate (it measures the exposure itself)
			long total = 0, swSamples = 0, runs = 0; double winSum = 0, winMax = 0; var t0all = Stopwatch.GetTimestamp(); double elapsedMs = 0;
			var sampler = new Thread(() =>
			{
				bool inSw = false; long runStart = 0; long n = 0, sw = 0, r = 0; double sum = 0, mx = 0;
				while (!volatile_stop)
				{
					bool swapped = Volatile.Read(ref Main.myPlayer) != 0; n++;
					if (swapped) { sw++; if (!inSw) { inSw = true; runStart = Stopwatch.GetTimestamp(); r++; } }
					else if (inSw) { inSw = false; double us = Stopwatch.GetElapsedTime(runStart).TotalMilliseconds * 1000.0; sum += us; if (us > mx) mx = us; }
					Thread.SpinWait(8);
				}
				total = n; swSamples = sw; runs = r; winSum = sum; winMax = mx;
			}) { IsBackground = true, Name = "SwapRace sampler" };

			long saves = 0, throws = 0; string firstThrow = null;
			var saver = new Thread(() =>
			{
				while (!volatile_stop)
				{
					try { Player.SavePlayer(Main.ActivePlayerFileData); saves++; } // what tML's autosave callback runs (WorldGen.saveToonWhilePlayingCallBack), called on this thread and caught
					catch (Exception e) { throws++; firstThrow ??= e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(); }
					Thread.Sleep(12);
				}
			}) { IsBackground = true, Name = "SwapRace saver" };

			var tickMs = new List<double>();
			sampler.Start(); if (withSaver) saver.Start();
			try
			{
				for (int tick = 0; tick < dotTicks; tick++)
				{
					long t0 = Stopwatch.GetTimestamp();
					foreach (var l in levies)
					{
						l.DebuffDot();
						if ((tick & 3) == 0) { var res = l.PlayerSequence(attacker, null, 40, 1, -1); if (res.Ran && !res.Dodged) l.PlayerOnHit(attacker, null, in res, 1); }
					}
					tickMs.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
				}
				dotEnd = SoldierNPC.DotDamage; dtEnd = SoldierNPC.DotTicks;
				// On an idle, fast box the fixed ticks end in ~50 ms: room for only two saves at the saver's ~25 ms cadence, too few for the verdict (3 saves, 9 probe points). Keep swapping
				// (damage-over-time steps only, past the counted ticks, so the DoT comparison is unchanged) until the saver has finished MinSaves saves or 5 s pass.
				while (withSaver && Interlocked.Read(ref saves) < MinSaves && Stopwatch.GetElapsedTime(t0all).TotalSeconds < 5)
					foreach (var l in levies) l.DebuffDot();
			}
			finally
			{
				elapsedMs = Stopwatch.GetElapsedTime(t0all).TotalMilliseconds;
				volatile_stop = true; Probing = false;
				sampler.Join(); if (withSaver) saver.Join(15000);
				_guard = false;
			}
			ph.Samples = total; ph.SwappedSamples = swSamples; ph.Runs = runs; ph.WindowSumUs = winSum; ph.WindowMaxUs = winMax; ph.ElapsedMs = elapsedMs; 
			ph.Saves = saves; ph.SaveThrows = throws; ph.FirstThrow = firstThrow;
			ph.Points = Interlocked.Read(ref _points); ph.MyWrong = Interlocked.Read(ref _myWrong); ph.LocalWrong = Interlocked.Read(ref _localWrong); ph.Slot255Wrong = Interlocked.Read(ref _slot255Wrong); ph.OnWorker = Interlocked.Read(ref _onWorkerThread); ph.Quits = _quitCalls;
			ph.DotDamage = dotEnd - dot0; ph.DotTicks = dtEnd - dt0; // the counted ticks only (an extension for the saver is not counted)
			double sumT = 0, maxT = 0; foreach (double d in tickMs) { sumT += d; if (d > maxT) maxT = d; }
			ph.TickAvgMs = tickMs.Count > 0 ? sumT / tickMs.Count : 0; ph.TickMaxMs = maxT;
			ph.GateSwaps = SaveGate.Swaps; ph.GateWaits = SaveGate.SwapWaits; ph.GateMaxWaitMs = SaveGate.MaxSwapWaitMs; ph.GateMaxSaveMs = SaveGate.MaxSaveMs;
			return ph;
		}
		private static volatile bool volatile_stop;

		private static void Report(Phase p)
		{
			double meanUs = p.Runs > 0 ? p.WindowSumUs / p.Runs : 0;
			Note($"{p.Name}: SAMPLER {p.Samples} polls of Main.myPlayer, swapped in {p.SwappedSamples} ({(p.Samples > 0 ? 100.0 * p.SwappedSamples / p.Samples : 0):0.00}% of the {p.ElapsedMs:0} ms), {p.Runs} windows, mean {meanUs:0.0} us, longest {p.WindowMaxUs:0.0} us");
			Note($"{p.Name}: SAVE PATH {p.Saves} saves finished, {p.SaveThrows} threw{(p.FirstThrow != null ? " (first: " + p.FirstThrow + ")" : "")}; {p.Points} probe points ({p.OnWorker} off the main thread): Main.myPlayer wrong {p.MyWrong}, Main.LocalPlayer wrong {p.LocalWrong}, slot 255 lent {p.Slot255Wrong}; WorldGen.SaveAndQuit calls swallowed {p.Quits}");
			Note($"{p.Name}: DoT {p.DotTicks} steps lost {p.DotDamage} life; tick avg {p.TickAvgMs:0.00} ms max {p.TickMaxMs:0.0} ms; gate swaps {p.GateSwaps}, waited for a save {p.GateWaits}x (longest {p.GateMaxWaitMs:0.0} ms), longest save {p.GateMaxSaveMs:0.0} ms");
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.active = false; }
			int count = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SWAPRACE_LEVIES"), out int c) && c > 0 ? c : 40;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SWAPRACE_TICKS"), out int tk) && tk > 0 ? tk : 300;
			var levies = new List<SoldierNPC>();
			for (int i = 0; i < count; i++) { var l = MakeLevy(new Vector2((Cx - 150 + i * 6) * 16f, FloorY * 16f), i); if (l != null) levies.Add(l); }
			if (levies.Count < count / 2) { Check($"levies spawn ({levies.Count} of {count})", false); return; }
			Step(3);
			int ai = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("SwapRace"), (Cx + 150) * 16, (FloorY - 3) * 16, NPCID.BlueSlime);
			NPC attacker = ai >= 0 && ai < Main.maxNPCs ? Main.npc[ai] : null;
			if (attacker == null) { Check("an attacker spawns", false); return; }
			attacker.timeLeft = 99999; attacker.dontTakeDamage = true; attacker.friendly = false; attacker.damage = 40; attacker.active = true;

			// SP emulation: Main.myPlayer is the commander's slot for the whole check, as it is for a real single player
			Main.myPlayer = 0; Main.player[0].active = true; Main.netMode = NetmodeID.Server;
			string dir = Path.Combine(Main.SavePath, "SwapRace"); Directory.CreateDirectory(dir);
			var file = new PlayerFileData(Path.Combine(dir, "SwapRaceProbe.plr"), false) { Metadata = FileMetadata.FromCurrentSettings(FileType.Player) };
			var probe = new Player(); probe.name = "SwapRaceProbe"; file.Player = probe;
			Main.ActivePlayerFileData = file;
			// a dedicated server never builds the achievement manager (Main.Initialize does, for a client), and Player.SavePlayer's first line is Main.Achievements.Save(): lend it one for the check
			var achField = typeof(Main).GetField("_achievements", BindingFlags.Instance | BindingFlags.NonPublic); _achOrig = achField?.GetValue(Main.instance); _achField = achField;
			if (achField != null && _achOrig == null) achField.SetValue(Main.instance, new Terraria.Achievements.AchievementManager());
			_stubs = true;
			_mapOrig = Main.mapEnabled; Main.mapEnabled = false; // Main.Map.Save() throws on a server (no map textures; tML swallows it and logs a Silently Caught Exception): out of the check, so the exception count stays a real count
			Note($"{levies.Count} levies, {ticks} DoT ticks each (every 4th also a hit sequence); Main.myPlayer = {Main.myPlayer}, dedServ = {Main.dedServ}, gate hooked = {SaveGateSystem.Hooked}, Calamity = {ModLoader.HasMod("CalamityMod")}, upkeep active on the first levy = {levies[0].UpkeepActive}");

			// no save running: what the damage over time loses, and how much of the time the swap is live
			var baseline = RunPhase("BASELINE (gate on, no save running)", true, levies, attacker, false, ticks); Report(baseline);
			var guarded = RunPhase("GATED (the fix; a save every ~12 ms on its own thread)", true, levies, attacker, true, ticks); Report(guarded);
			Phase raw = null;
			if (Environment.GetEnvironmentVariable("LEVY_SWAPRACE_REPRO") == "1")
			{
				raw = RunPhase("UNGUARDED (the swap as it was; lab only)", false, levies, attacker, true, ticks); Report(raw);
				Note($"REPRO: with the gate off the save path sampled a swapped Main.myPlayer {raw.MyWrong} times in {raw.Points}, a wrong Main.LocalPlayer {raw.LocalWrong}, a lent slot 255 {raw.Slot255Wrong}, {raw.SaveThrows} saves threw, SaveAndQuit would have fired {raw.Quits}x");
			}
			Check($"the swap really happens in this run (sampler saw Main.myPlayer swapped {baseline.SwappedSamples} times in {baseline.Runs} windows, {baseline.DotTicks} DoT steps)", baseline.SwappedSamples > 0 && baseline.DotTicks > 0);
			Check($"gate on: {guarded.Saves} saves ran beside the swaps and none threw ({guarded.SaveThrows} threw, SaveAndQuit {guarded.Quits}x)", guarded.Saves >= 3 && guarded.SaveThrows == 0 && guarded.Quits == 0);
			Check($"gate on: the save path never saw a swapped Main.myPlayer ({guarded.MyWrong}), Main.LocalPlayer ({guarded.LocalWrong}) or lent slot 255 ({guarded.Slot255Wrong}) in {guarded.Points} probe points", guarded.Points >= 9 && guarded.MyWrong == 0 && guarded.LocalWrong == 0 && guarded.Slot255Wrong == 0);
			Check($"gate on: the damage over time loses exactly what it loses with no save running ({guarded.DotDamage} vs {baseline.DotDamage} life over {guarded.DotTicks} steps)", guarded.DotTicks == baseline.DotTicks && guarded.DotDamage == baseline.DotDamage && baseline.DotDamage > 0);
			Check($"gate on: Main.myPlayer is back to {Main.myPlayer} and slot 255 is the one it was", Main.myPlayer == 0 && ReferenceEquals(Main.player[Main.maxPlayers], _orig255));
			if (Environment.GetEnvironmentVariable("LEVY_SWAPRACE_QUEUE") == "1") { Note("REPRO knob: queueing tML's own autosave on the thread pool as Player.KillMe does for the local player"); WorldGen.saveToonWhilePlaying(); }
			if (raw != null) Check($"repro phase: same damage over time as the baseline ({raw.DotDamage} vs {baseline.DotDamage}): the gate moves no number", raw.DotTicks == baseline.DotTicks && raw.DotDamage == baseline.DotDamage);
		}
	}

	/// <summary>The save path's own view of the globals, sampled where every mod's ModPlayer save hook runs (inside Player.SavePlayer, after the gate). Inert unless <see cref="SwapRaceCheck.Probing"/>.</summary>
	public sealed class SwapProbePlayer : ModPlayer
	{
		public override void PreSavePlayer() => SwapRaceCheck.Sample();
		public override void SaveData(TagCompound tag) => SwapRaceCheck.Sample();
		public override void LoadData(TagCompound tag) { } // tML requires SaveData and LoadData together
		public override void PostSavePlayer() => SwapRaceCheck.Sample();
	}
}
