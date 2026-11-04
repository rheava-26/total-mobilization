using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using ThoriumMod.Items;
using Levy.Common.Util;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// A PLAYER's Thorium instrument empowering the player's own soldiers and officer avatars. Thorium empowers only Players, once per note, from
	/// <c>BardItem.ApplyEmpowerments(Player bard, Player target)</c> on the casting player's own machine (target = that machine's local player). This installs a detour on that one method:
	/// Thorium runs unchanged, then - for the local player's own instrument only - the same empowerments, at the same level, duration and range, are worked out
	/// (<see cref="EmpowerCompat.ResolveForPlayer"/>, which is Thorium's own list, virtual rules and duration bonuses run for the bard) and laid on every soldier and officer avatar of that
	/// commander in reach through the very grant a bard soldier's note uses (<see cref="EmpowerCompat.ApplyResolved"/>): the same Thorium <c>Empowerment.Update</c> stats on the soldier's stat
	/// dummy, the same Empowered buff, the same expiry. In multiplayer the client sends the resolved list to the server (<see cref="CompanionRequest"/>); the server applies it.
	/// A detour on the single method that every instrument note passes through is robust to the 100+ instruments (and any mod that adds more) with no per-item code.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class PlayerBardCompat
	{
		private delegate void Orig_ApplyEmpowerments(BardItem self, Player bard, Player target);
		private delegate void Hook_ApplyEmpowerments(Orig_ApplyEmpowerments orig, BardItem self, Player bard, Player target);

		/// <summary>Minimum ticks between two requests to the server (an instrument sounds many times a second; an empowerment lasts seconds).</summary>
		private const uint SendCadence = 12;
		private static uint _nextSend;

		/// <summary>True once the detour is in place (the compat reports it in the log; false = the player's own tunes do not reach the soldiers).</summary>
		public static bool Installed { get; private set; }

		/// <summary>Install the detour. Called once from <see cref="ThoriumDriver.Register"/>; tModLoader removes it when the mod unloads.</summary>
		public static void Install()
		{
			Installed = false;
			MethodInfo target = typeof(BardItem).GetMethod("ApplyEmpowerments", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(Player), typeof(Player) }, null);
			if (target == null) throw new MissingMethodException("BardItem.ApplyEmpowerments(Player, Player)");
			MonoModHooks.Add(target, new Hook_ApplyEmpowerments(Detour));
			Installed = true;
		}

		private static void Detour(Orig_ApplyEmpowerments orig, BardItem self, Player bard, Player target)
		{
			orig(self, bard, target); // Thorium's own empowerment of the players, untouched
			if (!ThoriumCompatSystem.Active || Main.dedServ) return;
			// This method also runs on every client for OTHER players' notes (target = the local player): only the local player's own note, once, reaches his soldiers.
			if (bard != Main.LocalPlayer || target != bard) return;
			try { Reach(self, bard); }
			catch (Exception e) { Levy.Instance.Logger.Warn("Levy/Thorium compat: the player's empowerments failed to reach the soldiers: " + e.Message); }
		}

		private static void Reach(BardItem bi, Player bard)
		{
			if (!bard.active || bard.dead) return;
			var list = EmpowerCompat.ResolveForPlayer(bi, bard, out float range);
			if (list.Count == 0 || range <= 0f) return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				if (Main.GameUpdateCount < _nextSend) return;
				_nextSend = Main.GameUpdateCount + SendCadence;
				EmpowerCompat.SendRequest(range, list);
			}
			else if (LevyUtil.IsServerOrSingle)
			{
				EmpowerCompat.ApplyResolved(bard.Center, bard.name, range, list);
			}
		}
	}
}
