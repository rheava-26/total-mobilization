using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Potions
{
	/// <summary>
	/// An enemy's shot carries the local player's index as its owner (every NPC fires with <c>Main.myPlayer</c>: the one player of a single-player world, the host of a hosted one). Vanilla asks that owner whether the
	/// projectile may hurt an NPC (<c>PlayerLoader.CanHitNPCWithProj</c> runs <c>ModPlayer.CanHitNPC</c> first and refuses on false), and a Spectator "deals no damage by any means" (<see cref="SpectatorPlayer.CanHitNPC"/>:
	/// dashes, auras, thorns, melee), so while the commander was a Spectator every hostile projectile the army stood in passed through it harmlessly: Moon Lord's bolts and spheres, the Wall of Flesh's lasers and
	/// every other boss shot, with the levies' own contact damage the only thing that still landed.
	/// The owner's veto is for the Spectator's OWN damage. A hostile shot is the enemy's, so for it the Spectator owner has no say: the hook below returns "no opinion" and the rest of the hit rules (the levy's own
	/// player-style immunity window, Calamity's, ...) decide exactly as they do for a levy whose commander is not a Spectator. <c>LEVY_SPECSHOTS=0</c> restores the old refusal (A/B switch for the lab and the world check).
	/// </summary>
	public class SpectatorHostileShots : ModSystem
	{
		private delegate bool? OrigCanHit(Player owner, Projectile proj, NPC target);
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_SPECSHOTS") != "0";
		public static bool Installed { get; private set; }

		public override void Load()
		{
			if (!On) return;
			try
			{
				MethodInfo m = typeof(PlayerLoader).GetMethod("CanHitNPCWithProj", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Player), typeof(Projectile), typeof(NPC) }, null);
				if (m == null) { Mod.Logger.Warn("Levy: PlayerLoader.CanHitNPCWithProj not found; a Spectator's hostile shots stay harmless to the army"); return; }
				MonoModHooks.Add(m, (Func<OrigCanHit, Player, Projectile, NPC, bool?>)Detour);
				Installed = true;
			}
			catch (Exception e) { Mod.Logger.Warn("Levy: Spectator hostile-shot hook failed: " + e.Message); }
		}

		private static bool? Detour(OrigCanHit orig, Player owner, Projectile proj, NPC target)
		{
			if (proj != null && proj.hostile && !proj.friendly && SpectatorPlayer.IsSpectator(owner)) return null;
			return orig(owner, proj, target);
		}
	}
}
