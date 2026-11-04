using System.Collections.Generic;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// The "[Levy SETBONUS]" breadcrumb trail for armor set bonuses on soldiers: each distinct first-time event (a set recognised on a soldier, the first booster / bolt / Guardian of a world,
	/// a soldier's hit that a set bonus turned down and why) is written to the log ONCE per world, so a designer who sees "nothing happens" can open the log and read which step ran.
	/// Capped at <see cref="LevyConstants.SetBonusLogMaxKeys"/> distinct lines per world. Server / single player only call sites; the keys reset on every world load and unload.
	/// </summary>
	public sealed class SetBonusLog : ModSystem
	{
		private static readonly HashSet<string> Seen = new();
		private static readonly HashSet<(string, int)> SeenById = new();

		/// <summary>True the first time <paramref name="key"/> is asked for in this world (and the cap has not been reached): the caller then builds and writes its line.</summary>
		public static bool First(string key)
		{
			if (Seen.Count + SeenById.Count >= LevyConstants.SetBonusLogMaxKeys) return false;
			return Seen.Add(key);
		}

		/// <summary>The same, keyed by a category and an id (a projectile or NPC type), for the hot paths that ask on every hit: no string is built to find out that the answer is no.</summary>
		public static bool First(string category, int id)
		{
			if (Seen.Count + SeenById.Count >= LevyConstants.SetBonusLogMaxKeys) return false;
			return SeenById.Add((category, id));
		}

		public static void Write(string message) => Levy.Instance?.Logger.Info("[Levy SETBONUS] " + message);

		private static void Clear() { Seen.Clear(); SeenById.Clear(); }

		public override void OnWorldLoad() => Clear();
		public override void OnWorldUnload() => Clear();
		public override void Unload() => Clear();
	}
}
