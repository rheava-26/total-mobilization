using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.DataStructures;
using Terraria.IO;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEST FIXTURE TILE ENTITIES. The world self-checks plant barracks, armories and relays straight into <see cref="TileEntity.ByID"/> without a tile under them. One built with <c>new</c> is not a modded
	/// entity to the world save: its <c>type</c> byte is 0 and its Mod is null, so <c>WorldFile.SaveTileEntities</c> takes it for a vanilla entity and calls <c>ModTileEntity.WriteExtraData(networkSend: false)</c>,
	/// which throws, and the autosave runs on a thread-pool thread, so the throw ends the server (11-17 ship run: the ten-minute autosave landed in the potion section of [LevyRules]).
	/// <see cref="Make{T}"/> builds the entity the way <c>ModTileEntity.Place</c> does (Mod, Type and the type byte set), so a save writes it like a placed one and the load drops it again (no tile under it).
	/// Every check that registers an entity by hand goes through <see cref="Add{T}"/> / <see cref="Remove"/>.
	/// </summary>
	public static class TestTileEntities
	{
		/// <summary>A detached entity of type <typeparamref name="T"/> at <paramref name="pos"/>, built as tML builds a placed one.</summary>
		public static T Make<T>(Point16 pos) where T : ModTileEntity => (T)Make(ModContent.GetInstance<T>(), pos);

		/// <summary>A detached entity of <paramref name="template"/>'s type at <paramref name="pos"/>: <c>ModTileEntity.Place</c> without the id and the registration.</summary>
		public static ModTileEntity Make(ModTileEntity template, Point16 pos)
		{
			var te = ModTileEntity.ConstructFromBase(template);
			te.type = (byte)te.Type;
			te.Position = pos;
			return te;
		}

		/// <summary>Registers <paramref name="te"/> under <paramref name="id"/> (and at its position when <paramref name="byPosition"/>).</summary>
		public static T Register<T>(T te, int id, bool byPosition = false) where T : TileEntity
		{
			te.ID = id;
			lock (TileEntity.EntityCreationLock)
			{
				TileEntity.ByID[id] = te;
				if (byPosition) TileEntity.ByPosition[te.Position] = te;
			}
			return te;
		}

		/// <summary><see cref="Make{T}"/> then <see cref="Register{T}"/>.</summary>
		public static T Add<T>(int id, Point16 pos, bool byPosition = false) where T : ModTileEntity => Register(Make<T>(pos), id, byPosition);

		/// <summary>Unregisters a fixture (by id, and at its position when it is the entity there).</summary>
		public static void Remove(TileEntity te)
		{
			if (te == null) return;
			lock (TileEntity.EntityCreationLock)
			{
				if (TileEntity.ByID.TryGetValue(te.ID, out var cur) && ReferenceEquals(cur, te)) TileEntity.ByID.Remove(te.ID);
				if (TileEntity.ByPosition.TryGetValue(te.Position, out var at) && ReferenceEquals(at, te)) TileEntity.ByPosition.Remove(te.Position);
			}
		}

		/// <summary>True when the world save can write <paramref name="te"/>: a vanilla entity, or a modded one with its Mod and a type byte that matches its Type.</summary>
		public static bool IsSaveSafe(TileEntity te)
		{
			if (te is not ModTileEntity m) return true;
			return m.Mod != null && m.Type >= ModTileEntity.NumVanilla && te.type == (byte)m.Type;
		}

		/// <summary>Every registered entity the world save cannot write.</summary>
		public static List<TileEntity> FindUnsafe()
		{
			var bad = new List<TileEntity>();
			lock (TileEntity.EntityCreationLock) foreach (var kv in TileEntity.ByID) if (!IsSaveSafe(kv.Value)) bad.Add(kv.Value);
			return bad;
		}

		public static string Describe(TileEntity te) => $"{te.GetType().Name} id {te.ID} at {te.Position} (type byte {te.type}, mod {(te as ModTileEntity)?.Mod?.Name ?? "none"})";

		private static readonly HashSet<(Type, int)> Flagged = new();

		/// <summary>
		/// Called after every world step of a check: logs a FAIL the first time an entity the save cannot write is seen registered. Any check that plants one by hand again is caught on the next step,
		/// whether or not an autosave lands while it is in.
		/// </summary>
		internal static void ReportUnsafe()
		{
			List<TileEntity> bad;
			try { bad = FindUnsafe(); } catch (InvalidOperationException) { return; } // the table changed under the scan: the next step looks again
			foreach (var te in bad)
				if (Flagged.Add((te.GetType(), te.ID)))
					global::Levy.Levy.Instance?.Logger.Info($"[SaveHold] FAIL a check registered a tile entity the world save cannot write: {Describe(te)}");
		}
	}

	/// <summary>
	/// THE SAVE HOLD: no world save runs while a world self-check owns the world. The checks step the world with Main.Update, and the server's autosave (Main.DoUpdate_AutoSave) fires every ten minutes of wall
	/// time on a server whatever <see cref="Main.autoSave"/> says, queueing <c>WorldFile.SaveWorld</c> on a thread-pool thread. On a loaded box the check chain runs longer than ten minutes, so that save read the
	/// world while a check had fixtures in it. The hold takes <c>WorldFile.IOLock</c>, the lock every world save takes first (a save that finds it taken returns without saving), and sets
	/// <see cref="Main.autoSave"/> off; the outermost <see cref="Scope.Dispose"/> puts both back, also when the check throws. A save the check itself makes on its own thread still runs (the lock is re-entrant).
	/// Opening a hold waits for a save already in flight to finish. Only the world self-checks open one; no hook is installed, so a real game is never touched.
	/// </summary>
	public static class SaveHold
	{
		private static readonly object IOLock = typeof(WorldFile).GetField("IOLock", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
		/// <summary>Longest wait for an in-flight save before the hold opens without the lock (the fixtures are save-safe anyway).</summary>
		public const int WaitMs = 120_000;

		private static int _depth; private static bool _savedAutoSave, _locked; private static Thread _owner;
		public static int Depth => _depth;
		/// <summary>True when <c>WorldFile.IOLock</c> was found (the hold can block a save).</summary>
		public static bool LockFound => IOLock != null;
		/// <summary>True while a hold is open and holds the world-save lock.</summary>
		public static bool Locked => _locked;

		/// <summary>Opens a hold; dispose it (a <c>using</c>) to close it. Nested holds on the owner thread only count.</summary>
		public static Scope Begin(string who)
		{
			if (_depth > 0 && _owner == Thread.CurrentThread) { _depth++; return new Scope(true); }
			if (_depth > 0) return new Scope(false); // another thread holds it: nothing to add
			_savedAutoSave = Main.autoSave; Main.autoSave = false;
			_locked = false;
			if (IOLock != null)
			{
				long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
				_locked = Monitor.TryEnter(IOLock, WaitMs);
				double ms = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
				if (!_locked) global::Levy.Levy.Instance?.Logger.Info($"[SaveHold] OBSERVED {who}: a world save held the lock for over {WaitMs / 1000} s; the hold opened without it");
				else if (ms > 50) global::Levy.Levy.Instance?.Logger.Info($"[SaveHold] OBSERVED {who}: waited {ms:0} ms for a world save in flight before the hold opened");
			}
			_owner = Thread.CurrentThread; _depth = 1;
			return new Scope(true);
		}

		/// <summary>True when another thread could start a world save now (the lock is free to it). Blocks for the length of one pool round trip; checks only.</summary>
		internal static bool FreeForOtherThreads()
		{
			if (IOLock == null) return true;
			var t = System.Threading.Tasks.Task.Run(() => { if (!Monitor.TryEnter(IOLock)) return false; Monitor.Exit(IOLock); return true; });
			return t.Wait(30_000) && t.Result;
		}

		private static void End()
		{
			if (_depth <= 0 || _owner != Thread.CurrentThread) return;
			if (--_depth > 0) return;
			_owner = null;
			Main.autoSave = _savedAutoSave;
			if (_locked) { _locked = false; Monitor.Exit(IOLock); }
		}

		public readonly struct Scope : IDisposable
		{
			private readonly bool _counted;
			internal Scope(bool counted) { _counted = counted; }
			public void Dispose() { if (_counted) End(); }
		}
	}

	/// <summary>While a world check holds the save (<see cref="SaveHold.Depth"/> &gt; 0), looks for a tile entity the save cannot write after every tick (<see cref="TestTileEntities.ReportUnsafe"/>). One int test otherwise.</summary>
	public sealed class SaveHoldScan : ModSystem
	{
		public override void PostUpdateEverything() { if (SaveHold.Depth > 0) TestTileEntities.ReportUnsafe(); }
	}
}
