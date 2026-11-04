using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Globals;
using Levy.Common.Util;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// The army's BARD RENOWN: damage dealt by bard levies' instruments adds up, world-wide and saved with the world; each threshold reached is one point, and every point raises the
	/// Inspiration maximum of EVERY bard levy (present and future) by one, on top of his own crystals, never above Thorium's player cap. Touches no ThoriumMod type (the Thorium-class test
	/// lives in <see cref="BardCompat.IsBardShot"/>), so the curve is plain arithmetic and the save data survives Thorium being switched off. Server / single player is authoritative; clients
	/// only ever see the resulting maximum, which rides each bard's own net sync.
	/// </summary>
	public static class BardRenown
	{
		/// <summary>Total damage the army's bards have dealt (the world's renown). Server / single player.</summary>
		public static long TotalDamage { get; private set; }
		/// <summary>Renown points earned so far (0..BardRenownMaxPoints).</summary>
		public static int Bonus { get; private set; }

		// ---------------------------------------------------------------- the curve (pure)

		/// <summary>Cumulative damage needed for renown point <paramref name="n"/> (1-based): base * n^exponent.</summary>
		public static long Threshold(int n) => n <= 0 ? 0L : (long)Math.Ceiling(LevyConstants.BardRenownBase * Math.Pow(n, LevyConstants.BardRenownExponent));

		/// <summary>Renown points a world total of <paramref name="damage"/> has earned (0..max).</summary>
		public static int BonusFor(long damage)
		{
			if (damage <= 0) return 0;
			int n = (int)Math.Floor(Math.Pow(damage / LevyConstants.BardRenownBase, 1.0 / LevyConstants.BardRenownExponent));
			n = Math.Clamp(n, 0, LevyConstants.BardRenownMaxPoints);
			while (n < LevyConstants.BardRenownMaxPoints && Threshold(n + 1) <= damage) n++; // floating-point guard, both directions
			while (n > 0 && Threshold(n) > damage) n--;
			return n;
		}

		/// <summary>A bard's Inspiration maximum: his own crystals' maximum plus the army's renown, clamped to Thorium's player range (10..40).</summary>
		public static int EffectiveMax(int crystalMax, int renown) =>
			Math.Clamp(Math.Clamp(crystalMax, LevyConstants.BardInspirationBase, LevyConstants.BardInspirationCap) + Math.Max(0, renown), LevyConstants.BardInspirationBase, LevyConstants.BardInspirationCap);

		// ---------------------------------------------------------------- accrual and persistence

		/// <summary>A bard levy's instrument dealt <paramref name="damage"/>. Server / single player. Announces each new point.</summary>
		public static void Add(int damage)
		{
			if (damage <= 0 || !LevyUtil.IsServerOrSingle) return;
			TotalDamage = Math.Min(TotalDamage + damage, long.MaxValue / 4);
			int now = BonusFor(TotalDamage);
			if (now <= Bonus) return;
			Bonus = now;
			LevyUtil.Chat($"Bard renown rises to {Bonus}: every bard levy's Inspiration maximum is {Bonus} higher.", new Microsoft.Xna.Framework.Color(190, 130, 255));
		}

		internal static void Clear() { TotalDamage = 0; Bonus = 0; }

		internal static void Load(long damage) { TotalDamage = Math.Max(0, damage); Bonus = BonusFor(TotalDamage); }
	}

	/// <summary>Saves the army's bard renown with the world, and runs the boot-time self-check of the curve.</summary>
	public class BardRenownSystem : ModSystem
	{
		public override void ClearWorld() => BardRenown.Clear();
		public override void OnWorldUnload() => BardRenown.Clear();

		public override void SaveWorldData(TagCompound tag)
		{
			if (BardRenown.TotalDamage > 0) tag["bardRenownDamage"] = BardRenown.TotalDamage;
		}

		public override void LoadWorldData(TagCompound tag) => BardRenown.Load(tag.ContainsKey("bardRenownDamage") ? tag.GetLong("bardRenownDamage") : 0L);

		/// <summary>Boot-time self-check of the pure curve and the cap.</summary>
		public override void PostAddRecipes()
		{
			var fail = new List<string>();
			int checks = 0;
			void Expect(bool ok, string what) { checks++; if (!ok) fail.Add(what); }
			try
			{
				Expect(BardRenown.BonusFor(0) == 0 && BardRenown.BonusFor(-5) == 0, "no damage, no points");
				long prev = 0;
				for (int n = 1; n <= LevyConstants.BardRenownMaxPoints; n++)
				{
					long t = BardRenown.Threshold(n);
					Expect(t > prev, $"threshold {n} escalates");
					Expect(BardRenown.BonusFor(t) == n, $"exactly threshold {n} earns point {n}");
					Expect(BardRenown.BonusFor(t - 1) == n - 1, $"one short of threshold {n} earns {n - 1}");
					prev = t;
				}
				Expect(BardRenown.BonusFor(long.MaxValue / 4) == LevyConstants.BardRenownMaxPoints, "points stop at the maximum");
				Expect(BardRenown.EffectiveMax(10, 0) == 10 && BardRenown.EffectiveMax(10, 5) == 15 && BardRenown.EffectiveMax(22, 5) == 27, "crystals and renown add");
				Expect(BardRenown.EffectiveMax(38, 30) == LevyConstants.BardInspirationCap && BardRenown.EffectiveMax(40, 30) == LevyConstants.BardInspirationCap, "never above Thorium's cap of 40");
				Expect(BardRenown.EffectiveMax(10, LevyConstants.BardRenownMaxPoints) == LevyConstants.BardInspirationCap, "full renown alone reaches the cap");
				Expect(BardRenown.EffectiveMax(3, 0) == 10 && BardRenown.EffectiveMax(10, -4) == 10, "never below the base 10");
			}
			catch (Exception e) { fail.Add("threw " + e.GetType().Name + ": " + e.Message); }
			if (fail.Count == 0)
				Mod.Logger.Info($"Levy bard-renown self-check: {checks} checks OK (points at {BardRenown.Threshold(1)}, {BardRenown.Threshold(2)}, {BardRenown.Threshold(3)}, {BardRenown.Threshold(5)}, {BardRenown.Threshold(10)}, {BardRenown.Threshold(20)}, {BardRenown.Threshold(30)} damage; cap {LevyConstants.BardInspirationCap})");
			else foreach (string f in fail) Mod.Logger.Warn("Levy bard-renown self-check FAILED: " + f);
		}
	}

	/// <summary>
	/// Shell that credits the army's renown with the damage a bard levy's instrument deals. Holds no state and touches no ThoriumMod type: acts on soldier shots only while the compat is
	/// active, and asks the JIT-gated <see cref="BardCompat.IsBardShot"/> whether the shot is Bard-class. Melee hits and any other class a bard soldier carries never count.
	/// </summary>
	public class BardRenownGlobalProjectile : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (damageDone <= 0 || !ThoriumCompatSystem.Active || !LevyUtil.IsServerOrSingle || target == null || target.friendly) return;
			try
			{
				if (!projectile.GetGlobalProjectile<SoldierProjectileTag>().IsSoldierShot) return;
				if (BardCompat.IsBardShot(projectile)) BardRenown.Add(damageDone);
			}
			catch (Exception) { /* renown is a bonus: never let it break a hit */ }
		}
	}
}
