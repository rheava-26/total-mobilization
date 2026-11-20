using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// One real Main.Update for the world self-checks (test harness only). A dedicated test server has no connected player, so the checks keep a stand-in in slot 0 active; the server loop thread
	/// clears that slot's active flag from the side, and vanilla's ambience pass (AmbienceServer.FindPlayerThatCanSeeBackgroundAmbience counts the active players, then indexes a second pass over them)
	/// throws ArgumentOutOfRange when the flag drops between the two passes. tML catches and logs it as a "Silently Caught Exception". The ambience pass is sky decoration with no bearing on what the
	/// checks measure, so it is left out of the tick (and the meteor-fall ambience call that shares it is held back with <see cref="WorldGen.spawnMeteor"/>).
	/// <para/>
	/// The same race has a second, much larger effect: vanilla's Projectile.Update kills any projectile whose owner's slot is not active, and the stand-in owns every round the army fires in a check. While the server
	/// loop thread is clearing the flag a few percent of updates, a bullet that lives 30 updates survives only about one time in four, so "the levy hurt the boss" or "the minion hurt the enemy" came down to timing
	/// (and to how loaded the box was). <see cref="WorldStepStandIn"/> stops the server loop from clearing slots while a check is stepping the world; nothing is connected on a test server, so it has nothing to drop.
	/// </summary>
	public static class WorldStep
	{
		private static MethodInfo _update;

		/// <summary>
		/// A hostile NPC the world spawned on its own (a zombie, a slime, and with Calamity loaded its modded spawns): not friendly, not a town NPC, not one of this army's own NPC types (Levy, Mechanization, Warbands).
		/// With an active player in it the world spawns these near her at random, and the army fights whatever hostile it sees, so a check that needs a quiet arena removes them each tick (the checks' own pinned targets are
		/// told apart by the caller).
		/// </summary>
		public static bool IsNaturalHostile(NPC n)
		{
			if (!n.active || n.friendly || n.townNPC) return false;
			string mod = n.ModNPC?.Mod.Name;
			return mod != "Levy" && mod != "Mechanization" && mod != "Warbands";
		}

		/// <summary>True once a check has begun stepping the world on this test server: the server loop leaves the player slots alone from then on (cleared again when the world unloads).</summary>
		public static volatile bool Holding;

		/// <summary>
		/// While a check holds this, the world spawns no natural enemies for the stand-in commander (maxSpawns 0, see <see cref="WorldStepSpawnGate"/>). With an active player in it the world spawns zombies and slimes at random, and a check that
		/// measures the army on a quiet field (dormancy needs 360 calm ticks, a Mind Link body must start from rest, a thorn must be walked on) used to remove them after the fact: a spawn inside the very tick could still knock a body or reset
		/// a calm counter, and which checks it hit depended on the box's luck. Opt-in, because the event checks need the world's own invasion spawns: <c>using (WorldStep.Quiet()) { ... }</c>.
		/// </summary>
		public static volatile int QuietSpawns;
		/// <summary>Lab only: LEVY_WORLDSTEP_NOQUIET=1 lets the world spawn as before (to measure what the spawns did to a check).</summary>
		private static readonly bool NoQuiet = Environment.GetEnvironmentVariable("LEVY_WORLDSTEP_NOQUIET") == "1";
		public static IDisposable Quiet() { if (!NoQuiet) QuietSpawns++; return new QuietToken(NoQuiet); }
		private sealed class QuietToken : IDisposable { private bool _d; public QuietToken(bool noop) { _d = noop; } public void Dispose() { if (_d) return; _d = true; if (QuietSpawns > 0) QuietSpawns--; } }

		/// <summary>Runs one Main.Update with the ambience pass suppressed.</summary>
		public static void Update()
		{
			Holding = true;
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			var ambience = Main.AmbienceServer; bool meteor = WorldGen.spawnMeteor;
			Main.AmbienceServer = null; WorldGen.spawnMeteor = false;
			try { _update.Invoke(Main.instance, new object[] { new GameTime() }); }
			finally { Main.AmbienceServer = ambience; WorldGen.spawnMeteor = meteor || WorldGen.spawnMeteor; }
		}
	}

	/// <summary>No natural enemy spawns while a check asked for quiet (<see cref="WorldStep.Quiet"/>). Inert in play: nothing but the world checks raises the counter.</summary>
	public sealed class WorldStepSpawnGate : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) { if (WorldStep.QuietSpawns > 0) { maxSpawns = 0; spawnRate = int.MaxValue / 4; } }
	}

	/// <summary>Keeps the server loop thread (Netplay.UpdateConnectedClients) from dropping the check's stand-in player while <see cref="WorldStep.Holding"/>. Inert in play: nothing but the world checks sets it.</summary>
	public sealed class WorldStepStandIn : ModSystem
	{
		private delegate void OrigUpdateConnectedClients();
		private delegate void HookUpdateConnectedClients(OrigUpdateConnectedClients orig);

		public override void Load()
		{
			if (!Main.dedServ) return;
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", BindingFlags.Static | BindingFlags.NonPublic), new HookUpdateConnectedClients(Detour)); }
			catch (Exception e) { Mod.Logger.Warn("WorldStep: could not hook UpdateConnectedClients: " + e.Message); }
		}

		/// <summary>Lab only: LEVY_WORLDSTEP_NOHOLD=1 puts the race back (to measure what it does to a check).</summary>
		private static readonly bool NoHold = Environment.GetEnvironmentVariable("LEVY_WORLDSTEP_NOHOLD") == "1";

		public override void OnWorldUnload() { WorldStep.Holding = false; }

		private static void Detour(OrigUpdateConnectedClients orig)
		{
			if (WorldStep.Holding && !NoHold) return; // a check is stepping the world with a stand-in in slot 0: no client is connected, so there is nothing to drop and the stand-in must stay
			orig();
		}
	}
}
