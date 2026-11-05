using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Each levy has his own lifesteal budget, under vanilla's rules. Vanilla's budget is <c>Player.lifeSteal</c>: a hit's lifesteal spends from it (Projectile.vampireHeal, ghostHeal, Calamity's
	/// SpawnLifeStealProjectile) and it refills every tick, +0.6 up to 80 in Normal and +0.5 up to 70 in Expert / Master. A levy's shots run owned by the commander, so without this every levy would spend the
	/// commander's one pool and a large army would throttle itself. <see cref="OwnerPuppet"/> swaps the owner's field for the dealing levy's value for the length of a puppeted pass and writes it back after.
	/// A new levy starts with a full pool (a freshly spawned player does too). Regeneration is applied when the pool is next used, for the ticks that have gone by, so an idle levy costs nothing.
	/// Calamity lowers the refill to rate minus a reduction (0.15 Normal, 0.1 Expert) while the pool is below its cap; with Calamity loaded the same numbers are read from it.
	/// Thorium's lifesteal (HealLife on a hit) has no budget of its own; it reads the owner's life, which the routed pass already holds as the levy's.
	/// </summary>
	internal static class LifeStealPool
	{
		/// <summary>Test seam (harness only, env LEVY_LIFESTEAL_SHARED): off = every levy spends the commander's single pool, as before the per-levy pool.</summary>
		internal static readonly bool SharedForTest = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_LIFESTEAL_SHARED"));

		/// <summary>Vanilla's cap and per-tick refill for the current difficulty.</summary>
		internal static void Rules(out float cap, out float rate)
		{
			bool expert = Main.expertMode;
			cap = expert ? 70f : 80f; rate = expert ? 0.5f : 0.6f;
		}

		private static bool _tried; private static FieldInfo _redClassic, _redExpert, _capClassic, _capExpert;
		private static void EnsureCalamity()
		{
			if (_tried) return;
			_tried = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				Type t = cal.Code.GetType("CalamityMod.Balancing.BalancingConstants");
				if (t == null) return;
				const BindingFlags f = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
				_redClassic = t.GetField("LifeStealRecoveryRateReduction_Classic", f); _redExpert = t.GetField("LifeStealRecoveryRateReduction_Expert", f);
				_capClassic = t.GetField("LifeStealCap_Classic", f); _capExpert = t.GetField("LifeStealCap_Expert", f);
			}
			catch (Exception) { _redClassic = _redExpert = _capClassic = _capExpert = null; }
		}

		internal static void Unload() { _tried = false; _redClassic = _redExpert = _capClassic = _capExpert = null; }

		/// <summary>True when Calamity is loaded and its refill reduction was found (<paramref name="reduction"/> per tick while below <paramref name="cap"/>).</summary>
		internal static bool CalamityRules(out float cap, out float reduction)
		{
			EnsureCalamity();
			bool expert = Main.expertMode;
			FieldInfo r = expert ? _redExpert : _redClassic, c = expert ? _capExpert : _capClassic;
			cap = 0f; reduction = 0f;
			if (r == null || c == null) return false;
			try { reduction = Convert.ToSingle(r.GetValue(null)); cap = Convert.ToSingle(c.GetValue(null)); return true; }
			catch (Exception) { return false; }
		}

		/// <summary>The pool after <paramref name="ticks"/> ticks of refill from <paramref name="pool"/>. Pure arithmetic (the boot check uses it too).</summary>
		internal static float Refill(float pool, int ticks, float cap, float rate, float reduction)
		{
			if (pool >= cap) return cap; // vanilla trims a pool above the cap back to it
			if (ticks <= 0) return pool;
			if (reduction <= 0f) return Math.Min(cap, pool + ticks * rate);
			float net = rate - reduction;
			if (net <= 0f) return pool;
			float x = pool + ticks * net;
			return x >= cap - rate ? cap : x; // the tick that lifts the pool within one refill of the cap clamps it there, and the reduction no longer applies
		}

		/// <summary>The levy's pool as it stands now, after the ticks since it was last touched. A levy that has not used lifesteal yet has a full pool.</summary>
		internal static float Current(SoldierNPC body)
		{
			Rules(out float cap, out float rate);
			float red = 0f;
			if (CalamityRules(out float calCap, out float calRed)) { red = calRed; cap = calCap; }
			uint now = Main.GameUpdateCount;
			if (float.IsNaN(body.LifeStealPool)) { body.LifeStealPool = cap; body.LifeStealStamp = now; return cap; }
			int ticks = (int)Math.Min(now - body.LifeStealStamp, 100000u);
			float p = Refill(body.LifeStealPool, ticks, cap, rate, red);
			body.LifeStealPool = p; body.LifeStealStamp = now;
			return p;
		}

		/// <summary>Writes the pool back after a pass (what the pass spent).</summary>
		internal static void Store(SoldierNPC body, float pool) { body.LifeStealPool = pool; body.LifeStealStamp = Main.GameUpdateCount; }
	}
}
