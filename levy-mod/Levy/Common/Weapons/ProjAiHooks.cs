using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Where a wrapper of a projectile's AI call can stand. <c>On_Projectile.AI</c> is a one-line stub the JIT inlines into <c>Projectile.Update</c>, so a detour on it is never reached (observed: 0 calls over thousands of updates, while
	/// <c>On_Projectile.VanillaAI</c> and <c>ProjectileLoader.ProjectileAI</c> saw every one). A wrapper therefore stands on all three points behind a re-entrancy guard of its own: whichever fires first opens the wrapper, the others pass straight through.
	/// <c>ProjectileLoader.ProjectileAI</c> is the one that also reaches a modded projectile's PreAI (a Calamity GlobalProjectile.PreAI that runs the projectile's whole AI and returns false never reaches VanillaAI).
	/// </summary>
	internal static class ProjAiHooks
	{
		/// <summary>Lab A/B switch (env LEVY_DEADHOOKS_OFF=1): the revived wrappers (Zenith anchor, flyer slot, held-item view) pass straight through, which is how the behaviour before the revival is measured.</summary>
		internal static readonly bool Off = Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_OFF") == "1";

		/// <summary>Detours <c>ProjectileLoader.ProjectileAI(Projectile)</c> (what <c>Projectile.AI</c> calls: PreAI of every global and the projectile, then the vanilla or modded AI).</summary>
		internal static void AddLoader(Action<Action<Projectile>, Projectile> detour, string who)
		{
			try
			{
				MethodInfo mi = typeof(ProjectileLoader).GetMethod("ProjectileAI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Projectile) }, null);
				if (mi != null) MonoModHooks.Add(mi, detour);
				else ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: ProjectileLoader.ProjectileAI not found: " + who + " is not wrapped for modded projectiles");
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: could not hook ProjectileLoader.ProjectileAI for " + who + ": " + e.Message); }
		}
	}
}
