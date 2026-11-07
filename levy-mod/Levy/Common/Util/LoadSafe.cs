using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Terraria;

namespace Levy.Common.Util
{
	/// <summary>
	/// Load-time self-checks run on the load worker thread while the main thread keeps drawing the loading screen. A check that swaps Main.gameMenu, Main.player[], Main.myPlayer, Main.dedServ or Main.netMode
	/// lets a draw or update land in the window with a half-state world (stale Player, wrong mod-player array) and the engine crashes. Every such check goes through <see cref="Run"/>, which executes it on the
	/// main thread, synchronously, so no draw can interleave. On the main thread (a dedicated server loads there) it runs inline, since queueing and waiting would deadlock; on a worker it is queued with
	/// Main.RunOnMainThread and waited for (the main thread's update loop services the queue while the loading screen is up).
	/// </summary>
	public static class LoadSafe
	{
		private static Func<bool> _isMain;

		private static bool OnMainThread()
		{
			if (Main.dedServ) return true; // a dedicated server loads mods on its main thread and has no draw loop
			if (_isMain == null)
			{
				Func<bool> f = null;
				try
				{
					var p = typeof(Main).Assembly.GetTypes().Select(t => t.GetProperty("IsMainThread", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)).FirstOrDefault(x => x != null && x.PropertyType == typeof(bool));
					if (p != null) f = () => (bool)p.GetValue(null);
				}
				catch (Exception) { }
				_isMain = f ?? (() => false);
			}
			return _isMain();
		}

		public static void Run(Action a)
		{
			if (OnMainThread()) { a(); return; }
			Exception err = null;
			Task t = Main.RunOnMainThread(() => { try { a(); } catch (Exception e) { err = e; } });
			t.Wait();
			if (err != null) Levy.Instance?.Logger.Warn("Levy: a load-time self-check threw " + err.GetType().Name + ": " + err.Message);
		}
	}
}
