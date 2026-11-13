using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Testing
{
	/// <summary>
	/// One real Main.Update for the world self-checks (test harness only). A dedicated test server has no connected player, so the checks keep a stand-in in slot 0 active; the server loop thread
	/// clears that slot's active flag from the side, and vanilla's ambience pass (AmbienceServer.FindPlayerThatCanSeeBackgroundAmbience counts the active players, then indexes a second pass over them)
	/// throws ArgumentOutOfRange when the flag drops between the two passes. tML catches and logs it as a "Silently Caught Exception". The ambience pass is sky decoration with no bearing on what the
	/// checks measure, so it is left out of the tick (and the meteor-fall ambience call that shares it is held back with <see cref="WorldGen.spawnMeteor"/>).
	/// </summary>
	public static class WorldStep
	{
		private static MethodInfo _update;

		/// <summary>Runs one Main.Update with the ambience pass suppressed.</summary>
		public static void Update()
		{
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			var ambience = Main.AmbienceServer; bool meteor = WorldGen.spawnMeteor;
			Main.AmbienceServer = null; WorldGen.spawnMeteor = false;
			try { _update.Invoke(Main.instance, new object[] { new GameTime() }); }
			finally { Main.AmbienceServer = ambience; WorldGen.spawnMeteor = meteor || WorldGen.spawnMeteor; }
		}
	}
}
