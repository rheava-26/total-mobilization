using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- GUN PARITY (part 3 of the ammo world check): a gun or bow in a levy's hands against the same gun in a player's
		// Logged as "[AmmoWorld]" lines. Each weapon is fired twice for a window with the same plain ammo: one levy holding its ground 12 tiles from the pinned target (the commander 64 tiles away), then the commander
		// herself from the levy's average spot at her natural rate (the button held, or clicked as fast as the game takes it for a weapon that does not reuse by itself). Measured from each side's first shot:
		// damage per second on the target and shots per second. Checks: the levy's DPS within 0.75 to 1.25 of the player's; a bow's shots per second within 10% of hers; none of the levy's shots at the commander.
		// Env: LEVY_GUNPARITY=0 skips, LEVY_GUNPARITY_ONLY=Name;Name (item names), LEVY_GUNPARITY_TICKS=N (window, default 600).

		/// <summary>Vanilla bows and guns of every tier (the vanilla half of the ship run covers vanilla bows).</summary>
		private static readonly short[] GunParityVanilla =
		{
			ItemID.WoodenBow, ItemID.PlatinumBow, ItemID.GoldBow, ItemID.DemonBow, ItemID.MoltenFury, ItemID.HellwingBow, ItemID.BeesKnees, ItemID.Marrow, ItemID.IceBow, ItemID.ShadowFlameBow,
			ItemID.Tsunami, ItemID.DaedalusStormbow, ItemID.PulseBow, ItemID.DD2BetsyBow, ItemID.FairyQueenRangedItem,
			ItemID.Minishark, ItemID.Handgun, ItemID.Megashark, ItemID.SniperRifle, ItemID.Shotgun, ItemID.ClockworkAssaultRifle, ItemID.Uzi, ItemID.VenusMagnum, ItemID.ChainGun,
		};

		/// <summary>Calamity's: the guns the ammo sweep found off (Infinity, Svantechnical, Seadragon, Megalodon, Auralis), their siblings, and a spread of bows.</summary>
		private static readonly string[] GunParityCal =
		{
			"Infinity", "Svantechnical", "Shredder", "Seadragon", "Megalodon", "Archerfish", "Voidragon", "Auralis", "DragonsBreath",
			"Galeforce", "Barinade", "Goobow", "BlossomFlux", "Alluvion", "Monsoon", "TheBallista", "Ultima", "Contagion", "DaemonsFlame", "AstrealDefeat", "TelluricGlare", "PlanetaryAnnihilation",
		};

		/// <summary>Weapons the check only notes (a known divergence the designer rules on, or a weapon whose player-side measurement the stand-in cannot make): name to reason.</summary>
		private static readonly Dictionary<string, string> GunParityNoteOnly = new();

		private const int GunParityCmdUpTiles = 60;

		private static List<(int type, string label)> GunParityWeapons()
		{
			var list = new List<(int type, string label)>();
			string only = Environment.GetEnvironmentVariable("LEVY_GUNPARITY_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			foreach (short t in GunParityVanilla) list.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				foreach (string n in GunParityCal) if (cal.TryFind(n, out ModItem mi)) list.Add((mi.Type, "CalamityMod:" + n));
			if (names != null)
			{
				list = list.Where(w => names.Any(n => w.label.EndsWith(":" + n))).ToList();
				foreach (string n in names)
				{
					if (list.Any(w => w.label.EndsWith(":" + n))) continue;
					foreach (Mod m in ModLoader.Mods) if (m.TryFind(n, out ModItem mi)) { list.Add((mi.Type, m.Name + ":" + n)); break; }
					if (!list.Any(w => w.label.EndsWith(":" + n)) && ItemID.Search.TryGetId(n, out int vid)) list.Add((vid, "Terraria:" + n));
				}
			}
			return list;
		}

		/// <summary>The plain ammo a parity run loads: Musket Balls, Wooden Arrows, Gel, else the first ammo of the kind.</summary>
		private static int GunParityAmmo(Item w, Dictionary<int, int> byKind)
		{
			if (w.useAmmo <= 0) return 0;
			if (w.useAmmo == AmmoID.Bullet) return ItemID.MusketBall;
			if (w.useAmmo == AmmoID.Arrow) return ItemID.WoodenArrow;
			if (w.useAmmo == AmmoID.Gel) return ItemID.Gel;
			return byKind.TryGetValue(w.useAmmo, out int a) ? a : 0;
		}

		private static void GunParityCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_GUNPARITY") == "0") return;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GUNPARITY_TICKS"), out int tk) ? tk : 600;
			var byKind = AmmoByKind();
			var sw = System.Diagnostics.Stopwatch.StartNew();
			Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
			foreach (var (type, label) in GunParityWeapons())
			{
				try
				{
					Main.dayTime = true; Main.time = 27000;
					Item smp = ContentSamples.ItemsByType[type];
					int ammo = GunParityAmmo(smp, byKind);
					if (smp.useAmmo > 0 && ammo <= 0) { ANote($"gun parity: {label} has no ammo of its kind loaded, skipped"); continue; }
					Item gi = new Item(); gi.SetDefaults(type);
					bool alters = false; try { alters = WorldAlter.AltersWith(gi, ammo > 0 ? ContentSamples.ItemsByType[ammo] : null); } catch (Exception) { }
					if (alters) { ANote($"gun parity: {label} breaks blocks, not fired by the world check"); continue; }
					WeaponProfile wp = WeaponClassifier.GetProfile(gi);
					// the commander stands 60 tiles straight above the levy, her cursor on herself: a shot that homes on her cursor climbs to her, while a shot's own flight (Svantechnical's splits fly back 50 tiles past the
					// shooter before they turn) never reaches her (a commander far behind the levy is where those splits fly by design, as they fly behind a player). LEVY_GUNPARITY_CMD=left puts her 64 tiles left instead.
					Vector2 above = new Vector2(Cx * 16f + 8f, (FloorY - GunParityCmdUpTiles) * 16f);
					AmmoStat lv = AmmoRun(type, ammo, label, false, ticks, 0f, 1, 0, 0, 0, Environment.GetEnvironmentVariable("LEVY_GUNPARITY_CMD") == "left" ? null : above);
					AmmoStat pl = AmmoRun(type, ammo, label, true, ticks, lv.LevyX);
					double dps = pl.DpsAfter > 0 ? lv.DpsAfter / pl.DpsAfter : 0, rate = pl.RatePerSec > 0 ? lv.RatePerSec / pl.RatePerSec : 0;
					bool bow = wp.Family == WeaponFamily.Bow || smp.useAmmo == AmmoID.Arrow;
					ANote($"gun parity: {label} family={wp.Family} useTime={smp.useTime} useAnimation={smp.useAnimation} reuseDelay={smp.reuseDelay} autoReuse={smp.autoReuse} channel={smp.channel} ammo={ItemName(ammo)} | levy: first shot t={lv.FirstShot} shots={lv.Roots} ({lv.RatePerSec:0.00}/s) dps={lv.DpsAfter:0} perShot={lv.PerShot:0.0} near cmd={lv.NearCmd} at cmd={lv.AtCmd} | player: first shot t={pl.FirstShot} shots={pl.Roots} ({pl.RatePerSec:0.00}/s) dps={pl.DpsAfter:0} perShot={pl.PerShot:0.0} | levy/player dps {dps:0.00} rate {rate:0.00}");
					if (AmmoDebug) { ANote("  " + lv); ANote("  " + pl); ANote($"  shot ticks levy [{string.Join(" ", lv.ShotTicks)}] player [{string.Join(" ", pl.ShotTicks)}]"); }
					string n = label.Substring(label.IndexOf(':') + 1);
					if (GunParityNoteOnly.TryGetValue(n, out string why)) { ANote($"gun parity: {label} not checked: {why}"); continue; }
					ACheck($"gun parity: {label} a levy's DPS is a player's at the same spot (levy {lv.DpsAfter:0}, player {pl.DpsAfter:0}, ratio {dps:0.00}, within 0.75 to 1.25)", lv.Roots > 0 && pl.Roots > 0 && dps >= 0.75 && dps <= 1.25);
					if (bow) ACheck($"gun parity: {label} a levy draws as often as a player (levy {lv.RatePerSec:0.00} shots/s, player {pl.RatePerSec:0.00}, ratio {rate:0.00}, within 0.9 to 1.1)", lv.Roots > 0 && pl.Roots > 0 && rate >= 0.9 && rate <= 1.1);
					ACheck($"gun parity: {label} nothing of the levy's goes to the commander {GunParityCmdUpTiles} tiles above him, her cursor on herself (born at her {lv.AtCmd}, warped to her {lv.JumpsToCmd}, ticks near her {lv.NearCmd})", lv.AtCmd == 0 && lv.JumpsToCmd == 0 && lv.NearCmd == 0);
				}
				catch (Exception e) { ACheck($"gun parity: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
			}
			ANote($"gun parity ran in {sw.ElapsedMilliseconds / 1000} s");
		}
	}
}
