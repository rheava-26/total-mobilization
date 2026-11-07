using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Soldiers
{
	/// <summary>
	/// Which ammo of a class is best for a given weapon. The armory hands a soldier with an empty (or lesser) pouch the best-scoring ammo it has, instead of the first stack in slot order.
	/// The score is the real effect of a round fired from THIS weapon: (weapon damage + ammo damage), times the projectile the weapon really fires with that ammo
	/// (<see cref="WeaponUse.ResolveProjectile"/>: launcher pairings, rocket offsets, the per-weapon swaps of Player.PickAmmo) for piercing and bouncing, times a small bonus for what the round carries
	/// (homing, splitting, debuffs). A bonus from the ammo item only counts when the weapon fires the ammo's own projectile: a bow that turns every arrow into one fixed projectile gets nothing from the arrow's trick.
	/// Pure of game state apart from content samples, so the boot self-check can exercise it.
	/// </summary>
	public static class AmmoRank
	{
		/// <summary>LEVY_AMMOBEST=0 restores the old "first stack in slot order" pick (A/B switch for measuring).</summary>
		public static bool Off = Environment.GetEnvironmentVariable("LEVY_AMMOBEST") == "0";

		/// <summary>A pouch is only swapped for ammo that scores at least this much better (stops churn between near-equal types).</summary>
		public const float UpgradeMargin = 1.15f;

		private const float PierceStep = 0.15f, PierceMaxSteps = 5f, PierceUnlimited = 1.9f, BounceBonus = 0.20f;

		private static readonly Dictionary<long, float> _cache = new();

		/// <summary>Ammo-item bonus multipliers for effects the round carries that neither its damage nor its penetration shows.</summary>
		private static Dictionary<int, float> _special;
		private static Dictionary<int, float> Special => _special ??= new Dictionary<int, float>
		{
			[ItemID.ChlorophyteBullet] = 1.30f, [ItemID.ChlorophyteArrow] = 1.30f,   // homing
			[ItemID.CrystalBullet] = 1.25f, [ItemID.HolyArrow] = 1.25f,               // splits into shards / stars
			[ItemID.VenomBullet] = 1.25f, [ItemID.VenomArrow] = 1.25f,                // venom
			[ItemID.IchorBullet] = 1.20f, [ItemID.IchorArrow] = 1.20f,                // defense shred
			[ItemID.CursedBullet] = 1.15f, [ItemID.CursedArrow] = 1.15f,              // cursed flames
			[ItemID.ExplodingBullet] = 1.15f, [ItemID.HellfireArrow] = 1.15f,         // blast
			[ItemID.NanoBullet] = 1.10f, [ItemID.FrostburnArrow] = 1.08f, [ItemID.FlamingArrow] = 1.08f,
			[ItemID.GoldenBullet] = 1.05f, [ItemID.HighVelocityBullet] = 1.05f,
			[ItemID.PartyBullet] = 0.95f,                                             // confetti: weak
		};

		/// <summary>The projectile's penetrate value and whether it is a known bouncer. Reads the content sample; a type with no sample reads as a plain single-hit shot.</summary>
		private static void Traits(int proj, out int penetrate, out bool bounces)
		{
			penetrate = 1; bounces = false;
			if (proj <= 0 || proj >= ProjectileLoader.ProjectileCount) return;
			bounces = proj == ProjectileID.MeteorShot;
			try
			{
				Projectile p = ContentSamples.ProjectilesByType != null && ContentSamples.ProjectilesByType.TryGetValue(proj, out var s) ? s : null;
				if (p == null) { p = new Projectile(); p.SetDefaults(proj); }
				penetrate = p.penetrate;
			}
			catch (Exception) { }
		}

		/// <summary>The score of firing <paramref name="ammo"/> from <paramref name="weapon"/> (null weapon: the ammo's own projectile and damage only). Higher is better; 0 = unusable.</summary>
		public static float Score(Item weapon, Item ammo)
		{
			if (ammo == null || ammo.IsAir) return 0f;
			long key = ((long)(weapon?.type ?? 0) << 32) | (uint)ammo.type;
			if (_cache.TryGetValue(key, out float cached)) return cached;
			float score = Compute(weapon, ammo);
			_cache[key] = score;
			return score;
		}

		private static float Compute(Item weapon, Item ammo)
		{
			int proj;
			try { proj = weapon != null ? WeaponUse.ResolveProjectile(null, weapon, ammo) : ammo.shoot; }
			catch (Exception) { proj = ammo.shoot; }
			if (proj <= 0) return 0f; // fires nothing: a dud, ranked last (still usable if it is all there is)
			float baseDmg = Math.Max(1f, (weapon != null ? ContentSamples.ItemsByType[weapon.type].damage : 0) + Math.Max(0, ammo.damage));
			Traits(proj, out int pen, out bool bounce);
			float mult = 1f;
			if (pen < 0) mult *= PierceUnlimited;
			else if (pen > 1) mult *= 1f + PierceStep * Math.Min(pen, PierceMaxSteps);
			if (bounce) mult *= 1f + BounceBonus;
			bool carried = weapon == null || proj == ammo.shoot; // the weapon fires the round's own projectile, so its trick comes along
			if (carried && Special.TryGetValue(ammo.type, out float sp)) mult *= sp;
			return baseDmg * mult;
		}

		internal static void ResetCache() => _cache.Clear();
	}

	/// <summary>Boot self-check of <see cref="AmmoRank.Score"/> (WeaponWiringCheck style). Logs only; changes no state.</summary>
	public class AmmoRankCheck : ModSystem
	{
		public override void PostAddRecipes()
		{
			try
			{
				AmmoRank.ResetCache();
				Item W(int t) { var i = new Item(); i.SetDefaults(t); return i; }
				float S(int weapon, int ammo) => AmmoRank.Score(W(weapon), W(ammo));
				var cases = new (string name, bool ok)[]
				{
					("Minishark: Meteor Shot > Silver Bullet", S(ItemID.Minishark, ItemID.MeteorShot) > S(ItemID.Minishark, ItemID.SilverBullet)),
					("Minishark: Silver Bullet > Musket Ball", S(ItemID.Minishark, ItemID.SilverBullet) > S(ItemID.Minishark, ItemID.MusketBall)),
					("Wooden Bow: Jester's Arrow > Flaming Arrow", S(ItemID.WoodenBow, ItemID.JestersArrow) > S(ItemID.WoodenBow, ItemID.FlamingArrow)),
					("Wooden Bow: Flaming Arrow > Wooden Arrow", S(ItemID.WoodenBow, ItemID.FlamingArrow) > S(ItemID.WoodenBow, ItemID.WoodenArrow)),
					("Wooden Bow: Hellfire Arrow > Jester's Arrow", S(ItemID.WoodenBow, ItemID.HellfireArrow) > S(ItemID.WoodenBow, ItemID.JestersArrow)),
					// the Shadowflame Bow turns every arrow into one fixed projectile: the arrow's pierce is gone, only its damage counts (Unholy 12 > Jester's 10)
					("Shadowflame Bow: swap makes Unholy Arrow > Jester's Arrow", S(ItemID.ShadowFlameBow, ItemID.UnholyArrow) > S(ItemID.ShadowFlameBow, ItemID.JestersArrow)),
					("no ammo scores 0", AmmoRank.Score(W(ItemID.Minishark), new Item()) == 0f),
					("null weapon still ranks by the round", AmmoRank.Score(null, W(ItemID.MeteorShot)) > AmmoRank.Score(null, W(ItemID.MusketBall))),
				};
				int bad = 0;
				foreach (var c in cases) if (!c.ok) { bad++; Mod.Logger.Warn($"Levy ammo-rank self-check FAILED: {c.name}"); }
				if (bad == 0) Mod.Logger.Info($"Levy ammo-rank self-check: {cases.Length} cases OK (best-ammo pick {(AmmoRank.Off ? "OFF by LEVY_AMMOBEST=0" : "on")})");
				AmmoRank.ResetCache();
			}
			catch (Exception e) { Mod.Logger.Warn("Levy ammo-rank self-check threw: " + e.Message); }
		}
	}
}
