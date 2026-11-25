using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// A BOSS ATTACK'S OWN PLAYER IMMUNITY WINDOW, for a levy. A player hit by most things is immune for 40 ticks (Player.Hurt). Some attacks shorten that on purpose so the player can be hit again at once: Wrath of the Gods' Nameless Deity
	/// super cosmic beam (<c>SuperCosmicBeam.OnHitPlayer</c> stores a 6-tick window in the mod's PlayerDataManager ModPlayer, whose PostUpdate writes it into <c>Player.immuneTime</c>, "similar to Sans' fight"). A levy took the beam's hits on the
	/// ordinary 40-tick window, so it took about a sixth of what a player standing in the same beam takes.
	/// <para/>What a player's window comes to after a hit is read the way the game reads it: the projectile's own <c>OnHitPlayer</c> runs on a scratch player that stands in the hit's window, then the PostUpdate of its mod's PlayerDataManager
	/// (the one place that mod writes such windows) runs on it, and what is left in its <c>immuneTime</c> is the window. A window shorter than the ordinary one replaces the levy's general window for that hit.
	/// <para/>Bounded on purpose: only a projectile whose mod has a ModPlayer named PlayerDataManager and that overrides <c>OnHitPlayer</c> is probed (cached per type), so no other projectile pays for it. A hit with its own immunity slot (a boss
	/// slot) is left to the slot logic. Checked across Calamity's decompile: its bosses write no player window of their own on a hit (they use the Bosses slot, which the levy already keeps); Calamity's own shortenings are revengeance caps
	/// and longer windows (extra hit frames), neither of which is a boss attack's; Wrath of the Gods' only such window is this beam's. <c>LEVY_SPECIALIMMUNE=0</c> turns it off (lab A/B).
	/// </summary>
	public static class SpecialImmunity
	{
		public static readonly bool On = Environment.GetEnvironmentVariable("LEVY_SPECIALIMMUNE") != "0";
		/// <summary>Harness telemetry: hits probed, hits whose window came out shorter than the ordinary one.</summary>
		public static long Probes, Shortened;
		/// <summary>The shortest window (ticks) seen, for the world check.</summary>
		public static int LastWindow;
		/// <summary>Lab: when set, replaces the probe (the world check hands a stand-in projectile a window to prove the levy's side of it).</summary>
		public static Func<Projectile, int> WindowOverride;

		/// <summary>Per mod: its ModPlayer that holds per-player effect state and applies it in PostUpdate (Wrath of the Gods' PlayerDataManager), or null.</summary>
		private static readonly Dictionary<string, ModPlayer> _dataManager = new();
		private static readonly MethodInfo _post = typeof(ModPlayer).GetMethod("PostUpdate", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
		private static readonly Dictionary<int, bool> _candidate = new();
		private static Player _scratch;

		private static ModPlayer ManagerOf(Mod mod)
		{
			if (mod == null) return null;
			if (_dataManager.TryGetValue(mod.Name, out ModPlayer m)) return m;
			try { mod.TryFind("PlayerDataManager", out m); } catch (Exception) { m = null; }
			_dataManager[mod.Name] = m; return m;
		}

		/// <summary>A modded projectile whose mod keeps per-player effect state in a PlayerDataManager ModPlayer and that overrides OnHitPlayer.</summary>
		private static bool Candidate(Projectile p)
		{
			if (p.ModProjectile == null) return false;
			if (_candidate.TryGetValue(p.type, out bool c)) return c;
			bool r = false;
			try
			{
				ModProjectile mp = p.ModProjectile;
				MethodInfo m = mp.GetType().GetMethod("OnHitPlayer", BindingFlags.Public | BindingFlags.Instance);
				r = m != null && m.DeclaringType != typeof(ModProjectile) && ManagerOf(mp.Mod) != null;
			}
			catch (Exception) { }
			_candidate[p.type] = r; return r;
		}

		/// <summary>Lab: why a projectile is or is not probed.</summary>
		public static string Diag(Projectile p)
		{
			MethodInfo m = p.ModProjectile?.GetType().GetMethod("OnHitPlayer", BindingFlags.Public | BindingFlags.Instance);
			return $"candidate {Candidate(p)}; data manager of {p.ModProjectile?.Mod.Name} {(ManagerOf(p.ModProjectile?.Mod) != null ? "found" : "NOT found")}; PostUpdate {(_post != null ? "found" : "NOT found")}; OnHitPlayer declared in {m?.DeclaringType?.Name}";
		}

		/// <summary>
		/// The window (ticks, unscaled) a PLAYER would be left with after taking <paramref name="proj"/>, given the ordinary window <paramref name="ordinary"/>: <paramref name="ordinary"/> itself unless the projectile's mod shortens it.
		/// </summary>
		public static int Window(Projectile proj, NPC body, int damageDone, int ordinary)
		{
			if (WindowOverride != null && proj != null) { int o = WindowOverride(proj); return o > 0 && o < ordinary ? o : ordinary; }
			if (!On || proj == null || !Candidate(proj)) return ordinary;
			ModPlayer pdm = ManagerOf(proj.ModProjectile.Mod);
			if (pdm == null || _post == null) return ordinary;
			int slot = Main.maxPlayers;
			Player prev = Main.player[slot];
			try
			{
				Player s = _scratch ??= new Player { whoAmI = slot, active = true, width = 20, height = 42 };
				Array.Clear(s.buffType); Array.Clear(s.buffTime);
				s.statLife = s.statLifeMax = s.statLifeMax2 = 100000; s.dead = false; s.creativeGodMode = false;
				s.position = body.position; s.width = body.width; s.height = body.height;
				s.immune = true; s.immuneTime = ordinary;
				Main.player[slot] = s;
				var info = new Player.HurtInfo { DamageSource = PlayerDeathReason.ByProjectile(-1, proj.whoAmI), Damage = Math.Max(1, damageDone), CooldownCounter = -1, Dodgeable = false, PvP = false, SoundDisabled = true, DustDisabled = true };
				ProjectileLoader.OnHitPlayer(proj, s, in info);
				_post.Invoke(s.GetModPlayer(pdm), null);
				int after = s.immuneTime;
				s.immune = false; s.immuneTime = 0;
				Probes++;
				if (after > 0 && after < ordinary) { Shortened++; LastWindow = after; return after; }
				return ordinary;
			}
			catch (Exception) { return ordinary; }
			finally { Main.player[slot] = prev; }
		}
	}
}
