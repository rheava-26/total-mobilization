using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;
using Levy.Common.Weapons;
using Levy.Content.TileEntities;
using Mechanization.Common;
using D = Mechanization.Common.MechConstants.Drone;

namespace Mechanization.Content.NPCs
{
	/// <summary>
	/// Boot-time self-check of the combat drone, in the style of <see cref="TurretWiringCheck"/>. A headless boot spawns no drone and loads no world, so this checks what can be checked without
	/// one and logs a single summary line (or the failures): the NPC and keybind are registered, every string exists, the battery numbers are sane and behave (drain, charge, clamp, the
	/// return threshold), the loiter box and the pads are laid out inside their bounds without overlap, the Armory link tolerates a missing Barracks, and the recall path (lend, hand back, lend
	/// again) works on a detached Armory's ledger. Inspect-and-log only: it touches no world state and never throws (a gap logs a warning and the mod still loads).
	/// </summary>
	public class DroneWiringCheck : ModSystem
	{
		private static readonly string[] Keys =
		{
			"Mods.Mechanization.NPCs.DroneNPC.DisplayName", "Mods.Mechanization.Keybinds.RecallDrones.DisplayName",
			"Mods.Mechanization.UI.DroneRecall", "Mods.Mechanization.UI.DroneLaunch", "Mods.Mechanization.UI.DroneRecallAll", "Mods.Mechanization.UI.DroneLaunchAll",
			"Mods.Mechanization.UI.DroneChatLoiter", "Mods.Mechanization.UI.DroneChatRefuel", "Mods.Mechanization.UI.DroneChatCharging", "Mods.Mechanization.UI.DroneChatRecall",
			"Mods.Mechanization.UI.DroneChatParked", "Mods.Mechanization.UI.DroneRecalling", "Mods.Mechanization.UI.DroneLaunching", "Mods.Mechanization.UI.DroneNoSpare",
			"Mods.Mechanization.UI.DroneKeyNoBarracks", "Mods.Mechanization.UI.DroneNone",
		};

		private static Item Make(int type, int stack = 1)
		{
			var it = new Item();
			it.SetDefaults(type);
			it.stack = stack;
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
				Check(ModContent.NPCType<DroneNPC>() > 0, "DroneNPC is registered");
				Check(DroneControls.RecallKey != null, "the recall / launch keybind is registered");
				Check(D.PerBarracksCap >= 1 && D.PerBarracksCap <= 32, "the garrison cap fits the slot bitmask");
				Check(D.SweepIntervalTicks > 0 && D.ValidateIntervalTicks > 0 && D.ScanIntervalTicks > 0 && D.NetRefreshTicks > 0 && D.AimSyncTicks > 0, "drone cadences are positive");
				Check(Enum.GetValues(typeof(DronePhase)).Length == 5 && (int)DronePhase.Parked == 4, "the five drone phases keep their sync values");
			});

			Guard("text", () =>
			{
				foreach (string key in Keys) Check(Language.Exists(key), "missing text " + key);
			});

			Guard("battery bounds", () =>
			{
				float cap = DroneBattery.Capacity;
				Check(cap > 0f && D.DrainPerSecond > 0f, "the battery has a capacity and drains");
				Check(D.ChargePerSecond > D.DrainPerSecond * 10f, "a refill is much faster than a drain (generous by design)");
				Check(cap / D.DrainPerSecond >= 300f, "a full battery flies at least five minutes");
				Check(cap / D.ChargePerSecond <= 30f, "a flat battery is full again within half a minute");
				Check(D.ReturnFraction > 0f && D.ReturnFraction < D.ScrambleFraction && D.ScrambleFraction < D.ResumeFraction && D.ResumeFraction <= 1f, "return < scramble < resume <= full");
				Check(D.PessimisticSpeedFactor > 0f && D.PessimisticSpeedFactor <= 1f, "the return trip is timed at a fraction of cruise speed");
				Check(D.ShowBarFraction > D.ReturnFraction && D.ShowBarFraction < 1f, "the bar shows before the drone turns back");

				Check(Math.Abs(DroneBattery.Step(cap, true, 10f) - (cap - 10f * D.DrainPerSecond)) < 0.001f, "ten seconds airborne drain ten seconds of charge");
				Check(DroneBattery.Step(1f, true, 100f) == 0f, "the battery never goes below empty");
				Check(DroneBattery.Step(cap - 1f, false, 100f) == cap, "the battery never overfills");
				Check(DroneBattery.Step(0f, false, 1f) > DroneBattery.Step(0f, true, 1f), "landing charges, flying drains");
				Check(DroneBattery.ReturnThreshold(0f) >= cap * D.ReturnFraction && DroneBattery.ReturnThreshold(10000f) > DroneBattery.ReturnThreshold(100f), "a farther drone turns back sooner");
				Check(DroneBattery.ReturnThreshold(1e9f) <= cap, "the return threshold never exceeds capacity");
				Check(!DroneBattery.ShouldReturn(cap, 0f) && DroneBattery.ShouldReturn(0f, 0f), "a full battery stays out, a flat one comes home");
				Check(DroneBattery.Recharged(cap) && !DroneBattery.Recharged(0f), "a full battery is recharged, a flat one is not");
				Check(DroneBattery.Fraction(cap * 2f) == 1f && DroneBattery.Fraction(-5f) == 0f, "the battery fraction is clamped to 0..1");
			});

			Guard("loiter and pads", () =>
			{
				float half = D.LoiterHalfWidthTiles;
				Check(D.LoiterMinAltTiles > 0 && D.LoiterMaxAltTiles > D.LoiterMinAltTiles && half > 0f, "the loiter box has a size");
				Check(D.LeashTiles > 0f && D.EngageStandoffFraction > 0f && D.EngageStandoffFraction < 1f, "the engage leash and standoff are set");
				var rand = new UnifiedRandom(7);
				bool inside = true;
				for (int slot = 0; slot < D.PerBarracksCap; slot++)
				{
					DroneFlight.AltRangeTiles(slot, out float lo, out float hi);
					inside &= lo >= D.LoiterMinAltTiles && hi <= D.LoiterMaxAltTiles && hi >= lo;
					inside &= Math.Abs(DroneFlight.SectorCenterTiles(slot)) <= half;
					for (int k = 0; k < 200; k++)
					{
						DroneFlight.PickWaypointTiles(slot, rand, out float dx, out float alt);
						inside &= Math.Abs(dx) <= half + 0.001f && alt >= D.LoiterMinAltTiles - 0.001f && alt <= D.LoiterMaxAltTiles + 0.001f;
					}
				}
				Check(inside, "every loiter waypoint lies inside the loiter box and its altitude band");
				DroneFlight.AltRangeTiles(0, out _, out float lowRowTop);
				DroneFlight.AltRangeTiles(D.LoiterSectors, out float highRowBottom, out _);
				Check(D.PerBarracksCap <= D.LoiterSectors || highRowBottom >= lowRowTop, "the second row of sectors flies above the first");

				// a drone wanders more than one point: the sector is wider than a waypoint
				Check(D.SectorSpreadTiles > 0f, "a waypoint varies inside its sector");

				// pads: distinct, both sides of the Barracks, and far enough apart that landed drones do not overlap
				float need = D.BodyWidth / (float)MechConstants.TilePx;
				bool clear = true; bool left = false, right = false;
				for (int a = 0; a < D.PerBarracksCap; a++)
				{
					float pa = DroneFlight.PadOffsetTiles(a);
					left |= pa < 0f; right |= pa > 0f;
					for (int b = a + 1; b < D.PerBarracksCap; b++) clear &= Math.Abs(pa - DroneFlight.PadOffsetTiles(b)) >= need;
				}
				Check(clear && left && right, "every pad is on its own patch of ground, left and right of the Barracks, clear of its neighbours");
				Check(D.PadStartTiles > 1.5f + need * 0.5f, "the first pad clears the Barracks wall");
			});

			Guard("armory link and recall", () =>
			{
				Check(ArmoryLink.OfBarracks(null) == null, "a missing Barracks has no linked Armory");

				// The recall path on a detached Armory's ledger: lend a weapon and a round, hand both back, lend again.
				var armory = new ArmoryTE();
				armory.Storage[0] = Make(ItemID.Musket);
				armory.Storage[1] = Make(ItemID.MusketBall, 50);
				Item weapon = armory.LendDroneWeapon(0);
				Check(weapon != null && DroneArsenal.IsMountable(weapon) && armory.IsLent(weapon), "a drone borrows the spare weapon and it sits on the ledger");
				Item round = DroneArsenal.TakesAmmo(weapon) ? armory.LendDroneAmmo(weapon.useAmmo, D.AmmoKeepRounds) : null;
				Check(round != null && round.stack == 1 && armory.IsLent(round), "it borrows one round from a stack that can spare it");
				DroneShot shot = DroneArsenal.BuildShot(weapon, round);
				Check(shot != null && shot.Valid, "the borrowed weapon builds a valid shot");
				Check(armory.ReturnLent(round) && armory.ReturnLent(weapon), "a recalled drone hands both back");
				Check(armory.Lent.Count == 0, "the ledger is empty after the recall");
				Check(!armory.ReturnLent(weapon), "a loan can be handed back once, never twice");
				int found = 0; foreach (Item it in armory.Storage) if (it != null && !it.IsAir && it.type == ItemID.Musket) found++;
				Check(found == 1, "the weapon is back on the racks exactly once (found " + found + ")");
				Item again = armory.LendDroneWeapon(0);
				Check(again != null && armory.IsLent(again) && !ReferenceEquals(again, weapon), "a relaunch lends the weapon afresh (a new instance, so a stale reference is never live)");
				Check(DroneArsenal.MaxHpFor(0) > 0 && DroneArsenal.MaxHpFor(ItemID.LastPrism) >= DroneArsenal.MaxHpFor(0), "an unarmed drone still has a life");

				// the order packets keep their wire values
				Check((byte)MechPacket.DroneCommand == 2 && (byte)MechPacket.DroneKey == 3 && (byte)DroneOrder.Recall == 0 && (byte)DroneOrder.LaunchAll == 3, "the drone packet and order values are stable");
			});

			try
			{
				if (failures.Count == 0) Mod.Logger.Info($"Mechanization: drone wiring check OK ({ok} checks): registration, text, battery, loiter and pads, armory link, recall path.");
				else foreach (string f in failures) Mod.Logger.Warn("Mechanization: drone wiring check FAILED: " + f);
			}
			catch (Exception) { }
		}
	}
}
