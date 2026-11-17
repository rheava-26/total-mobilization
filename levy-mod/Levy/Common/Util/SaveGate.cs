using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// THE SAVE GATE. For a few microseconds at a time the army borrows "the local player": the damage-over-time step (SoldierNPC.DebuffDot) and the player-hit / gear frames (SoldierNPC.EnterFrameSlot)
	/// set <c>Main.myPlayer</c> to a spare slot (and the frame also <c>Main.dedServ</c> and the sound volume) so every "whoAmI == Main.myPlayer" branch the game and other mods run for a Player lands on the
	/// soldier's dummy. That is exactly how a real player's rules run, and it cannot be done with the scratch player passed explicitly: the branches read the global, not a parameter.
	/// <para/>
	/// tModLoader saves the character on a thread-pool thread (WorldGen.saveToonWhilePlaying, saveAndPlay on the ten-minute autosave; Save and Quit), through <c>Player.SavePlayer</c>. That path reads
	/// <c>Main.myPlayer</c> / <c>Main.player[Main.myPlayer]</c> (the map header reads the local player's biome flags) and runs every mod's ModPlayer.PreSavePlayer / SaveData / PostSavePlayer, which read
	/// <c>Main.LocalPlayer</c>. tML takes no lock around the swap's window, so the save could run with the swapped values.
	/// <para/>
	/// The fix makes the swap and the save mutually exclusive. A swap site calls <see cref="Enter"/> before it changes the globals and <see cref="Exit"/> after it has restored them; the hook on
	/// <c>Player.SavePlayer</c> takes the same monitor for the whole save. The save therefore starts only between swaps and finishes before the next swap begins, so it can never see a swapped value. Nothing
	/// the swap does changes (the same player rules run in the same order), so no damage number moves. The monitor is re-entrant, a swap that nests inside another costs nothing, and the main thread
	/// waits for a save only when a save is in flight (a save is tens of milliseconds, once every ten minutes).
	/// <para/>
	/// Safety valves: a swap waits at most <see cref="WaitMs"/> for a save (then runs ungated and logs once: a stuck save must never freeze the game), and a waiting save is let in ahead of the next swap
	/// (without that, a monitor that is released and re-taken hundreds of times a tick could starve it). <c>LEVY_SAVEGATE=0</c> turns the gate off (lab A/B only).
	/// </summary>
	public static class SaveGate
	{
		private static readonly object Lock = new();
		private static int _savers;
		/// <summary>Lab A/B switch (LEVY_SAVEGATE=0 or the SwapRace check): false puts the unguarded swap back.</summary>
		public static volatile bool Enabled = Environment.GetEnvironmentVariable("LEVY_SAVEGATE") != "0";
		/// <summary>How long a swap waits for an in-flight save before it runs without the gate.</summary>
		public const int WaitMs = 5000;
		public static long Swaps, SwapWaits, Timeouts, Saves;
		/// <summary>Longest time (ms) a swap spent waiting for a save, and the longest a save held the gate.</summary>
		public static double MaxSwapWaitMs, MaxSaveMs;
		private static int _warned;

		/// <summary>Called before a swap changes Main.myPlayer / Main.dedServ. Returns the token to hand to <see cref="Exit"/>.</summary>
		public static bool Enter()
		{
			if (!Enabled) return false;
			Swaps++;
			if (Monitor.IsEntered(Lock)) { Monitor.Enter(Lock); return true; } // nested inside a swap this thread already holds
			if (Volatile.Read(ref _savers) > 0)
			{
				// a save is queued behind the monitor: give it the next turn instead of re-taking the monitor ahead of it
				long t0 = Stopwatch.GetTimestamp(); var sw = new SpinWait();
				while (Volatile.Read(ref _savers) > 0 && Stopwatch.GetElapsedTime(t0).TotalMilliseconds < 2) sw.SpinOnce(-1);
			}
			long w0 = Stopwatch.GetTimestamp();
			if (Monitor.TryEnter(Lock))
				return true;
			SwapWaits++;
			bool got = Monitor.TryEnter(Lock, WaitMs);
			double ms = Stopwatch.GetElapsedTime(w0).TotalMilliseconds;
			if (ms > MaxSwapWaitMs) MaxSwapWaitMs = ms;
			if (got) return true;
			Timeouts++;
			if (Interlocked.Exchange(ref _warned, 1) == 0) Levy.Instance?.Logger.Warn($"Levy: a player save held the swap gate for over {WaitMs} ms; the swap ran without it.");
			return false;
		}

		/// <summary>Called after the swapped globals are restored.</summary>
		public static void Exit(bool token) { if (token) Monitor.Exit(Lock); }

		/// <summary>Runs <paramref name="save"/> with the gate held: no swap is live for its whole length.</summary>
		public static void RunSave(Action save)
		{
			if (!Enabled) { save(); return; }
			Interlocked.Increment(ref _savers);
			try { Monitor.Enter(Lock); }
			finally { Interlocked.Decrement(ref _savers); }
			long t0 = Stopwatch.GetTimestamp();
			try { save(); }
			finally
			{
				double ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
				if (ms > MaxSaveMs) MaxSaveMs = ms;
				Interlocked.Increment(ref Saves);
				Monitor.Exit(Lock);
			}
		}

		public static void ResetCounters() { Swaps = SwapWaits = Timeouts = Saves = 0; MaxSwapWaitMs = MaxSaveMs = 0; }
	}

	/// <summary>Puts <see cref="SaveGate"/> around tML's character save (<c>Player.SavePlayer</c>), the one entry every autosave, Save and Quit and map save goes through.</summary>
	public sealed class SaveGateSystem : ModSystem
	{
		private delegate void OrigSave(PlayerFileData file, bool skipMapSave);
		public static bool Hooked { get; private set; }

		public override void Load()
		{
			try
			{
				MethodInfo m = null;
				foreach (var c in typeof(Player).GetMethods(BindingFlags.Public | BindingFlags.Static))
				{
					if (c.Name != "SavePlayer") continue;
					var ps = c.GetParameters();
					if (ps.Length >= 1 && ps[0].ParameterType == typeof(PlayerFileData)) { m = c; break; }
				}
				if (m == null) { Mod.Logger.Warn("Levy: Player.SavePlayer not found; the swap gate has nothing to guard"); return; }
				if (m.GetParameters().Length == 2) MonoModHooks.Add(m, (Action<OrigSave, PlayerFileData, bool>)Detour2);
				else MonoModHooks.Add(m, (Action<Action<PlayerFileData>, PlayerFileData>)Detour1);
				Hooked = true;
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: save gate hook failed: " + e.Message); }
		}

		private static int _hl_Detour2_1, _hl_Detour1_1;
		private static void Detour2(OrigSave orig, PlayerFileData file, bool skipMapSave) { _hl_Detour2_1++; SaveGate.RunSave(() => orig(file, skipMapSave)); }
		private static void Detour1(Action<PlayerFileData> orig, PlayerFileData file) { _hl_Detour1_1++; SaveGate.RunSave(() => orig(file)); }
	}
}
