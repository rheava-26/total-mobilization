using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Mechanization.Content.Items;
using T = Mechanization.Common.MechConstants.Turret;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// Boot-time self-check of the turret, in the style of Levy's <c>DroneWiringCheck</c>. A headless boot spawns no turret and loads no world, so this checks what can be checked without
	/// one and logs a single summary line (or the failures): the NPC and item are registered, every string the turret shows exists, the weapons a turret must mount build a valid shot,
	/// and the reach it claims for each is honest (a long gun reaches at least the plain bullet's distance; a flamethrower, whose flames die young, claims no more than they fly).
	/// Inspect-and-log only: it touches no world state and never throws (a gap logs a warning and the mod still loads).
	/// </summary>
	public class TurretWiringCheck : ModSystem
	{
		private static readonly string[] Keys =
		{
			"Mods.Mechanization.Items.TurretItem.DisplayName", "Mods.Mechanization.Items.TurretItem.Tooltip", "Mods.Mechanization.NPCs.TurretNPC.DisplayName",
			"Mods.Mechanization.UI.TurretDismantle", "Mods.Mechanization.UI.TurretChatMounted", "Mods.Mechanization.UI.TurretChatNoWeapon", "Mods.Mechanization.UI.TurretChatNoArmory",
			"Mods.Mechanization.UI.TurretNoRoom", "Mods.Mechanization.UI.HintPlaceLinked", "Mods.Mechanization.UI.HintPlaceUnlinked",
			"Mods.Mechanization.UI.HintTurretMounted", "Mods.Mechanization.UI.HintTurretNoWeapon", "Mods.Mechanization.UI.HintTurretNoArmory",
		};

		private static Item Make(int type)
		{
			var it = new Item();
			it.SetDefaults(type);
			return it;
		}

		public override void PostAddRecipes()
		{
			var failures = new List<string>();
			int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			void Guard(string name, Action body)
			{
				try { body(); }
				catch (Exception e) { failures.Add($"{name} threw {e.GetType().Name}: {e.Message}"); }
			}

			Guard("registration", () =>
			{
				Check(ModContent.NPCType<TurretNPC>() > 0, "TurretNPC is registered");
				Check(ModContent.ItemType<TurretItem>() > 0, "TurretItem is registered");
				Check(T.RangePx > 0f && T.FireIntervalTicks > 0 && T.ScanIntervalTicks > 0 && T.AimSyncTicks > 0, "turret tunables are positive");
				Check(T.RetargetCloserFraction > 0f && T.RetargetCloserFraction < 1f, "retarget fraction is a fraction");
			});

			Guard("text", () =>
			{
				foreach (string key in Keys) Check(Language.Exists(key), "missing text " + key);
			});

			Guard("weapons and reach", () =>
			{
				// weapons the turret must mount, each building a valid shot with a sane reach
				int[] mount = { ItemID.Musket, ItemID.Minishark, ItemID.WoodenBow, ItemID.AmethystStaff, ItemID.RocketLauncher, ItemID.Flamethrower, ItemID.Megashark };
				foreach (int id in mount)
				{
					Item w = Make(id);
					Check(DroneArsenal.IsMountable(w), w.Name + " should be mountable");
					DroneShot shot = DroneArsenal.BuildShot(w, null);
					Check(shot != null && shot.Valid, w.Name + " should build a valid shot");
					if (shot == null || !shot.Valid) continue;
					float reach = TurretNPC.ReachFor(w, shot);
					Check(reach >= T.MinReachTiles * Levy.Common.LevyConstants.TilePx, w.Name + " reach is below the floor");
				}
				// a gun reaches at least as far as the plain bullet; the flamethrower claims no more than its flames fly
				Item gun = Make(ItemID.Megashark); DroneShot gs = DroneArsenal.BuildShot(gun, null);
				Check(gs != null && gs.Valid && TurretNPC.ReachFor(gun, gs) >= T.RangePx, "a long gun reaches at least the plain bullet's distance");
				Item fl = Make(ItemID.Flamethrower); DroneShot fs = DroneArsenal.BuildShot(fl, null);
				Projectile fsp = fs != null ? WeaponClassifier.ProjectileSample(fs.Projectile) : null;
				Check(fs != null && fs.Valid && TurretNPC.ReachFor(fl, fs) < T.RangePx, $"a flamethrower's reach is cut to its flames (reach {TurretNPC.ReachFor(fl, fs) / 16f:0.#} tiles, shot range {fs?.RangePx / 16f:0.#}, profile {WeaponClassifier.GetProfile(fl).RangeTiles}, proj {fs?.Projectile} life {fsp?.timeLeft} speed {fs?.Speed} steps {fs?.StepMult})");
				// melee is refused (the turret never mounts a blade)
				Check(!DroneArsenal.IsMountable(Make(ItemID.CopperBroadsword)), "a broadsword must not mount");
			});

			Guard("placement", () =>
			{
				// off the edge of the world there is no room (the in-world path needs a loaded world, which a headless boot has not)
				Check(!TurretSystem.FindRestSpot(new Microsoft.Xna.Framework.Vector2(-100f, -100f), out _), "a spot off the world must be refused");
			});

			if (failures.Count == 0) Mod.Logger.Info($"Mechanization: turret wiring check OK ({ok} checks): registration, text, weapon mounts, honest reach, placement guard.");
			else foreach (string f in failures) Mod.Logger.Warn("Mechanization: turret wiring check FAILED: " + f);
		}
	}
}
