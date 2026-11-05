using System.Threading;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Owner
{
	/// <summary>
	/// Keeps the dedicated server's network thread out of a soldier owner frame.
	/// <para/>While a soldier's dummy stands in a player slot below 255, <c>Netplay.UpdateConnectedClients</c> (run by the server's own thread, continuously) sees an active player with no client behind it and
	/// deactivates it. <c>Projectile.Update</c> then kills every projectile whose owner is inactive, and a hook dies the tick after it is cast. The frame therefore holds <see cref="Gate"/> for its whole length,
	/// and this detour takes the same lock around the network thread's pass. Dedicated server only: single player has no network thread.
	/// <para/>Both sides use <c>Monitor.TryEnter</c> with a timeout. A frame that cannot get the lock does not run (the soldier tries again next tick); a network pass that cannot get it is skipped (the loop comes round again).
	/// </summary>
	public sealed class NetThreadGuard : ModSystem
	{
		public static readonly object Gate = new object();
		public const int TimeoutMs = 250;
		/// <summary>Network passes skipped because a frame held the lock past the timeout, and frames that could not start for the same reason (diagnostics).</summary>
		public static long SkippedPasses, RefusedFrames;
		private static bool _hooked;

		/// <summary>True when a network thread exists (a dedicated server). Read from the real net mode, which a frame never changes.</summary>
		public static bool Needed => Main.netMode == Terraria.ID.NetmodeID.Server;

		public override void Load()
		{
			if (!Main.dedServ) return;
			On_Netplay.UpdateConnectedClients += Detour;
			_hooked = true;
		}

		public override void Unload()
		{
			if (!_hooked) return;
			try { On_Netplay.UpdateConnectedClients -= Detour; } catch { }
			_hooked = false;
		}

		private static void Detour(On_Netplay.orig_UpdateConnectedClients orig)
		{
			if (!Monitor.TryEnter(Gate, TimeoutMs)) { SkippedPasses++; return; }
			try { orig(); }
			finally { Monitor.Exit(Gate); }
		}

		/// <summary>Main thread: take the lock for a frame. False when it could not be had within the timeout.</summary>
		internal static bool TryEnter()
		{
			if (Monitor.TryEnter(Gate, TimeoutMs)) return true;
			RefusedFrames++;
			return false;
		}

		internal static void Exit() => Monitor.Exit(Gate);
	}
}
