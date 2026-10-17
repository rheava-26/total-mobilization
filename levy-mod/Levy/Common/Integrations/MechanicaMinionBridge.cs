using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Projectiles;

namespace Levy.Common.Integrations
{
	/// <summary>
	/// Weak, name-only bridge to Mechanica's summon minions (no compile-time reference). Soldier stand-ins ram by default; every Mechanica minion
	/// that actually fires a projectile is registered into <see cref="SoldierMinion.RegisterRangedMinion(int, SoldierMinion.RangedShot)"/> so it holds a standoff range
	/// and shoots instead. Every lookup is by name through the Mod instance; a name that does not resolve is skipped. Mechanica absent = nothing runs.
	/// Cadence / speed / lifetime come from the minions' own AI (decompiled): cooldown = the attack timer threshold, speed = the NewProjectile velocity
	/// magnitude, leadSpeed = speed x (1 + extraUpdates of the bullet). Instant beams (speed 1, no lead, own short lifetime) are direction-only.
	/// Left to the default ram on purpose: GlacialSnakeMinion (body-segment lasers only), PiranhaMinion, TombJewelBeetleMinion, JungleSnakeMinion,
	/// CoolSharkMinion, FloatingPlantMinion (no attack projectile), HallowedSkullMinion and SkullDroneMinion (damage-0 orbs aimed at the owner).
	/// </summary>
	public static class MechanicaMinionBridge
	{
		private static bool _warned;

		// minion name, bullet (Mechanica ModProjectile name, or null to use a vanilla ProjectileID), cooldown, speed, standoff, lead, dmgMult, timeLeft (0 = bullet default)
		private readonly struct Entry
		{
			public readonly string Minion, Bullet; public readonly int VanillaBullet, Cooldown; public readonly float Speed, Standoff, Lead, DamageMult; public readonly int TimeLeft;
			public Entry(string minion, string bullet, int vanilla, int cooldown, float speed, float standoff, float lead, float dmg = 1f, int timeLeft = 0)
			{ Minion = minion; Bullet = bullet; VanillaBullet = vanilla; Cooldown = cooldown; Speed = speed; Standoff = standoff; Lead = lead; DamageMult = dmg; TimeLeft = timeLeft; }
		}

		private static readonly Entry[] Table =
		{
			// vanilla-bullet syringes (flame / ichor every 80 ticks at speed 10)
			new Entry("CurseFireSyringeMinion",          null, ProjectileID.CursedFlameFriendly, 80, 10f, 300f, 10f),
			new Entry("ConvertedCurseFireSyringeMinion", null, ProjectileID.CursedFlameFriendly, 80, 10f, 300f, 10f),
			new Entry("IchorSyringeMinion",              null, ProjectileID.IchorSplash,         80, 10f, 300f, 10f),
			new Entry("ConvertedIchorSyringeMinion",     null, ProjectileID.IchorSplash,         80, 10f, 300f, 10f),

			// drones (LabDroneBullet clones CursedBullet: extraUpdates 1 -> 24 px/tick effective)
			new Entry("LaserDroneMinion",                "LabDroneBullet",              0, 60, 12f, 320f, 24f),
			new Entry("ConvertedLaserDroneMinion",       "ConvertedLabDroneBullet",     0, 60, 12f, 320f, 24f),
			new Entry("MissileDroneMinion",              "MissileDroneRocket",          0, 90, 10f, 320f, 10f),
			new Entry("ConvertedMissileDroneMinion",     "ConvertedMissileDroneRocket", 0, 90, 10f, 320f, 10f),

			// mini helicopter: 10-round bursts every ~170 ticks -> ~1 bullet / 17 ticks (rockets skipped); HelicopterBullet has extraUpdates 1
			new Entry("MiniHelicopterMinion",            "HelicopterBullet",          0, 17, 13f, 300f, 26f),
			new Entry("ConvertedMiniHelicopterMinion",   "ConvertedHelicopterBullet", 0, 17, 13f, 300f, 26f),

			// instant hit-scan beams: velocity is a direction only (speed 1), the beam keeps its own short lifetime, no lead
			new Entry("TempleHelicopterMinion",          "TempleHelicopterBeam",          0, 60,  1f, 450f, 0f),
			new Entry("ConvertedTempleHelicopterMinion", "ConvertedTempleHelicopterBeam", 0, 60,  1f, 450f, 0f),
			new Entry("RoboDogMinion",                   "RoboDogMartianLaser",           0, 40,  1f, 400f, 0f),
			new Entry("ConvertedRoboDogMinion",          "ConvertedRoboDogMartianLaser",  0, 40,  1f, 400f, 0f),
			new Entry("WarHeliMinion",                   "WarHeliInstantLaser",           0, 125, 1f, 450f, 0f),
			new Entry("ConvertedWarHeliMinion",          "ConvertedWarHeliInstantLaser",  0, 125, 1f, 450f, 0f),

			// Smol Betsy's flame stream (every 5 ticks, 46 px/tick, 26-tick life) and the mounted machine gun (every 18 ticks, 24 px/tick, 90-tick life)
			new Entry("SmolBetsyMinion",                 "SmolBetsyFlame",          0,  5, 46f, 320f, 46f),
			new Entry("MountedMachinegunMinion",         "MountedMachinegunBullet", 0, 18, 24f, 320f, 24f, 1f, 90),
		};

		/// <summary>Registers every resolvable Mechanica shooter minion. Safe to call with Mechanica absent (no-op) and never throws.</summary>
		public static void Register()
		{
			try
			{
				if (!ModLoader.TryGetMod("Mechanica", out Mod mech) || mech == null) return; // not installed: silent and normal
				int added = 0;
				foreach (Entry e in Table)
				{
					try
					{
						if (!mech.TryFind(e.Minion, out ModProjectile minion) || minion == null) continue;
						int bullet = e.VanillaBullet;
						if (e.Bullet != null)
						{
							if (!mech.TryFind(e.Bullet, out ModProjectile b) || b == null) continue;
							bullet = b.Type;
						}
						if (bullet <= 0) continue;
						SoldierMinion.RegisterRangedMinion(minion.Type, new SoldierMinion.RangedShot(bullet, e.Cooldown, e.Speed, e.Standoff, e.Lead, e.DamageMult, e.TimeLeft));
						added++;
					}
					catch (Exception ex) { Warn("entry " + e.Minion + " skipped: " + ex.Message); }
				}
				try { Levy.Instance?.Logger.Info("Levy: Mechanica minion bridge registered " + added + " shooter minions."); } catch (Exception) { }
			}
			catch (Exception ex) { Warn(ex.Message); }
		}

		private static void Warn(string why)
		{
			if (_warned) return;
			_warned = true;
			try { Levy.Instance?.Logger.Warn("Levy: Mechanica minion bridge problem (" + why + "); remaining shooters may keep the default ram."); } catch (Exception) { }
		}
	}

	/// <summary>Runs the Mechanica minion registration once content is set up, and drops the runtime entries on unload.</summary>
	public class MechanicaMinionBridgeSystem : ModSystem
	{
		public override void PostSetupContent()
		{
			try { SoldierMinion.ClearRegisteredRangedMinions(); MechanicaMinionBridge.Register(); } catch (Exception) { }
		}

		public override void Unload()
		{
			try { SoldierMinion.ClearRegisteredRangedMinions(); } catch (Exception) { }
		}
	}
}
