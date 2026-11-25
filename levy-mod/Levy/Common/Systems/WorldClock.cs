using System;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Systems
{
	/// <summary>
	/// A running count of in-game ticks (a day and a night are 86400), the same on the server, in single player and on every client: it follows the world's clock, so sleeping and time commands move it too.
	/// Things that wait "one in-game day" (the Recruitment Office) hold the moment they are ready again as a point on this count; a save writes the time left, a load puts it back on the count.
	/// </summary>
	public class WorldClock : ModSystem
	{
		/// <summary>In-game ticks counted since this game session started. Only differences between two readings mean anything.</summary>
		public static double Now;
		private static double _last; private static bool _day, _init;

		/// <summary>Ticks in one in-game day: the day and the night together.</summary>
		public static double DayTicks => Main.dayLength + Main.nightLength;

		public override void OnWorldLoad() { _init = false; }
		public override void OnWorldUnload() { _init = false; }

		public override void PostUpdateTime()
		{
			double t = Main.time; bool d = Main.dayTime;
			if (!_init) { _init = true; _last = t; _day = d; return; }
			double delta = d == _day ? t - _last : (_day ? Main.dayLength : Main.nightLength) - _last + t;
			_last = t; _day = d;
			if (delta > 0 && delta <= DayTicks) Now += delta; // a jump back (a time command) or past a whole day adds nothing
		}

		/// <summary>Self-checks: feeds the clock one reading as <see cref="PostUpdateTime"/> would.</summary>
		internal static void TestFeed(double time, bool dayTime)
		{
			double was = Main.time; bool wasDay = Main.dayTime;
			Main.time = time; Main.dayTime = dayTime;
			try { ModContent.GetInstance<WorldClock>().PostUpdateTime(); } finally { Main.time = was; Main.dayTime = wasDay; }
		}
		internal static void TestRestart() { _init = false; }

		/// <summary>In-game time as the panels show it: "13h 20m", "40m" (never less than 1m while any time is left).</summary>
		public static string Format(double ticks)
		{
			if (ticks <= 0) return "0m";
			int minutes = (int)Math.Ceiling(ticks / 60.0); // 3600 ticks are one in-game hour
			int h = minutes / 60, m = minutes % 60;
			return h > 0 ? h + "h " + m + "m" : m + "m";
		}
	}
}
