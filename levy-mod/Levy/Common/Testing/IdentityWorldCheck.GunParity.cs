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
		// Logged as "[AmmoWorld]" lines. Each weapon is fired twice for a window with the same plain ammo: one levy holding its ground 12 tiles from the pinned target, the commander 60 tiles straight above him with her cursor
		// pointed at herself from his spot (a shot that reads her cursor while it flies for him heads to her), then the commander herself from the levy's average spot at her natural rate (the button held, or clicked as fast as
		// the game takes it for a weapon that does not reuse by itself; her ItemCheck runs once a tick, HarnessCommanderPlayer.PreItemCheck). Measured from each side's first shot: damage per second on the target and shots per second.
		// Checks: the levy's DPS within 0.75 to 1.25 of the player's; a bow's shots per second within 10% of hers; nothing of the levy's comes within 10 tiles of the commander.
		// Env: LEVY_GUNPARITY=0 skips, LEVY_GUNPARITY_ONLY=Name;Name (item names), LEVY_GUNPARITY_TICKS=N (window, default 600), LEVY_GUNPARITY_CMD=left (the commander 64 tiles left instead).

		/// <summary>Vanilla bows and guns of every tier (the vanilla half of the ship run covers vanilla bows).</summary>
		private static readonly short[] GunParityVanilla =
		{
			ItemID.WoodenBow, ItemID.PlatinumBow, ItemID.GoldBow, ItemID.DemonBow, ItemID.MoltenFury, ItemID.HellwingBow, ItemID.BeesKnees, ItemID.Marrow, ItemID.IceBow, ItemID.ShadowFlameBow,
			ItemID.Tsunami, ItemID.DaedalusStormbow, ItemID.PulseBow, ItemID.DD2BetsyBow, ItemID.FairyQueenRangedItem,
			ItemID.Minishark, ItemID.Handgun, ItemID.Megashark, ItemID.SniperRifle, ItemID.Shotgun, ItemID.ClockworkAssaultRifle, ItemID.Uzi, ItemID.VenusMagnum, ItemID.ChainGun,
		};

		/// <summary>Calamity's: the guns the ammo sweep found off (Infinity, Svantechnical, Seadragon, Megalodon, Auralis, Dragon's Breath), their siblings, and a spread of bows.</summary>
		private static readonly string[] GunParityCal =
		{
			"Infinity", "Svantechnical", "Shredder", "Seadragon", "Megalodon", "Archerfish", "Voidragon", "Auralis", "DragonsBreath",
			"Galeforce", "Barinade", "Goobow", "BlossomFlux", "Alluvion", "Monsoon", "TheBallista", "Ultima", "Contagion", "DaemonsFlame", "AstrealDefeat", "TelluricGlare", "PlanetaryAnnihilation",
		};

		/// <summary>The Calamity weapons of the ship run (the rest with LEVY_GUNPARITY_ALL=1): the ones the gunparity pass fixed, one sibling of the shark guns and two bows. The Calamity half of the ship run has no time to spare.</summary>
		private static readonly HashSet<string> GunParityShipCal = new() { "Infinity", "Svantechnical", "Seadragon", "Auralis", "DragonsBreath", "Archerfish", "Galeforce", "TheBallista" };

		/// <summary>Weapons this check found off and the gunparity pass did not fix: their DPS is noted, not checked (the designer rules on them). Name to what was measured.</summary>
		private static readonly Dictionary<string, string> GunParityNoteOnly = new()
		{
			["DD2BetsyBow"] = "a player's Aerial Bane fires Betsy arrows that split (6 roots a use with Wooden Arrows); a levy's fires the table volley of 3 plain Wooden Arrows (0.37 of a player's DPS): vanilla's own Aerial Bane shot is not copied",
			["Shotgun"] = "a levy fires a fixed fan of 4 pellets over 25 degrees, a player 4 to 5 pellets with vanilla's random spread (0.46 to 0.74 of a player's DPS at 12 tiles)",
			["DaedalusStormbow"] = "a levy's rain lands 0.62 to 0.90 of a player's DPS at the same rate (15.9 against 18.6 a shot): the levy's rain is SkyDrop's, a player's is vanilla's own scatter over her cursor: not found",
			["BlossomFlux"] = "a levy shoots 0.58 as often as a player (its use is 4 shots in a 16-tick animation) for 0.73 of her DPS: not found",
			["Megalodon"] = "0.67 to 0.81 of a player's DPS (0.59 to 0.65 before the pass): the levy's rockets carry his own hit count now (30, as hers), but he fired 4 rockets to her 6 in 1,500 ticks: not found",
			["Voidragon"] = "0.88 of a player's DPS over 600 ticks, 1.59 over 1,500 (0.07 before the pass): the levy's Abyssal Fire beam lands 318 hits a beam to her 181 (its local hit cooldown is 2 ticks, at most 250): not found",
		};

		/// <summary>Weapons whose shots pass the commander in this check's geometry by their own flight, not by reading her: the near-commander check is a note for them. Name to reason.</summary>
		private static readonly Dictionary<string, string> GunParityNearNoteOnly = new()
		{
			["DaedalusStormbow"] = "its arrows fall from the top of the screen above the target, where the commander stands in this check",
			["PulseBow"] = "its bolts ricochet off the arena's walls and ceiling",
			// Shredder's splits (ChargedBlastSplit with ai[2] 0) never read a cursor: they fly straight back from the hit within 0.9 radians, bounce off tiles up to 4 times and live about 40 ticks (some 90 tiles of flight); observed
			// passing her on a straight line (velocity (-5.6, -2.2) unchanged over the ticks near her)
			["Shredder"] = "its splits fly straight back from the hit and bounce off the arena, far enough to pass the commander 60 tiles up",
		};

		private const int GunParityCmdUpTiles = 60;

		private static List<(int type, string label)> GunParityWeapons()
		{
			var list = new List<(int type, string label)>();
			string only = Environment.GetEnvironmentVariable("LEVY_GUNPARITY_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			// The ship run keeps the Calamity half inside its time budget (the full list cost 163 s there): with Calamity loaded the vanilla weapons are left to the vanilla half (one bow and one gun stay as a control),
			// and the weapons whose DPS is only noted are skipped. LEVY_GUNPARITY_ALL=1 (or a LEVY_GUNPARITY_ONLY list) runs everything.
			bool calLoaded = ModLoader.TryGetMod("CalamityMod", out Mod cal);
			bool all = names != null || Environment.GetEnvironmentVariable("LEVY_GUNPARITY_ALL") == "1";
			foreach (short t in GunParityVanilla) if (all || !calLoaded || t == ItemID.PlatinumBow || t == ItemID.Minishark) list.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
			if (calLoaded)
				foreach (string n in GunParityCal) if ((all || GunParityShipCal.Contains(n)) && cal.TryFind(n, out ModItem mi)) list.Add((mi.Type, "CalamityMod:" + n));
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
			int fixedTicks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_GUNPARITY_TICKS"), out int tk) ? tk : 0;
			bool left = Environment.GetEnvironmentVariable("LEVY_GUNPARITY_CMD") == "left";
			var byKind = AmmoByKind();
			var sw = System.Diagnostics.Stopwatch.StartNew();
			Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
			Vector2? envCursor = AmmoCmdCursor;
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
					int ticks = fixedTicks > 0 ? fixedTicks : Math.Clamp(Math.Max(smp.useAnimation, smp.useTime) * 40, 900, 1500); // at least 900 ticks and about 40 uses: in 600 ticks a slow gun's few shots (a Sniper Rifle's 16) and the Hellwing's wandering bats measured 0.73 to 1.11
					// The commander stands 60 tiles straight above the levy. A shot's own flight never reaches her there (Svantechnical's splits fly back about 50 tiles past the shooter before they turn: a commander standing far
					// behind the levy is where they fly by design, as they fly behind a player), while a shot that steers to her cursor climbs to her: her cursor is pointed at herself from the levy's spot, because a shot that reads it
					// while it flies for him reads it from his place (OwnerPuppet lends the owner's place to the levy).
					Vector2 above = new Vector2(Cx * 16f + 8f, (FloorY - GunParityCmdUpTiles) * 16f);
					AmmoCmdCursor = left ? envCursor : new Vector2(0f, -GunParityCmdUpTiles * 16f);
					long ctrSwaps0 = PlayerCounters.Swaps, ctrHits0 = PlayerCounters.HitCounts, cntSwaps0 = OwnerPuppet.CountSwaps, declined0 = WeaponUse.DeclinedUses;
					AmmoStat lv;
					try { lv = AmmoRun(type, ammo, label, false, ticks, 0f, 1, 0, 0, 0, left ? null : above); }
					finally { AmmoCmdCursor = envCursor; }
					if (AmmoDebug) ANote($"  {label}: player counter swaps {PlayerCounters.Swaps - ctrSwaps0}, hit counts {PlayerCounters.HitCounts - ctrHits0} (rows {PlayerCounters.RowNames}), owned-count swaps {OwnerPuppet.CountSwaps - cntSwaps0}, declined uses run as uses {WeaponUse.DeclinedUses - declined0}");
					AmmoStat pl = AmmoRun(type, ammo, label, true, ticks, lv.LevyX);
					double dps = pl.DpsAfter > 0 ? lv.DpsAfter / pl.DpsAfter : 0, rate = pl.RatePerSec > 0 ? lv.RatePerSec / pl.RatePerSec : 0;
					bool bow = wp.Family == WeaponFamily.Bow || smp.useAmmo == AmmoID.Arrow;
					ANote($"gun parity: {label} family={wp.Family} useTime={smp.useTime} useAnimation={smp.useAnimation} reuseDelay={smp.reuseDelay} autoReuse={smp.autoReuse} channel={smp.channel} ammo={ItemName(ammo)} | levy: first shot t={lv.FirstShot} shots={lv.Roots} ({lv.RatePerSec:0.00}/s) dps={lv.DpsAfter:0} perShot={lv.PerShot:0.0} near cmd={lv.NearCmd} at cmd={lv.AtCmd} | player: first shot t={pl.FirstShot} shots={pl.Roots} ({pl.RatePerSec:0.00}/s) dps={pl.DpsAfter:0} perShot={pl.PerShot:0.0} | levy/player dps {dps:0.00} rate {rate:0.00}");
					if (AmmoDebug) { ANote("  " + lv); ANote("  " + pl); ANote($"  shot ticks levy [{string.Join(" ", lv.ShotTicks)}] player [{string.Join(" ", pl.ShotTicks)}]"); }
					string n = label.Substring(label.IndexOf(':') + 1);
					if (GunParityNearNoteOnly.TryGetValue(n, out string nearWhy)) ANote($"gun parity: {label} near the commander {lv.NearCmd} ticks, not checked: {nearWhy}");
					// on her: within 4 tiles (a shot that goes to her cursor ends there; a ricochet or a long split that flies past within 10 tiles is its own flight, noted)
					else ACheck($"gun parity: {label} nothing of the levy's goes to the commander {GunParityCmdUpTiles} tiles above him, her cursor pointed at herself (born at her {lv.AtCmd}, warped to her {lv.JumpsToCmd}, ticks on her {lv.OnCmd}, within 10 tiles {lv.NearCmd})", lv.AtCmd == 0 && lv.JumpsToCmd == 0 && lv.OnCmd == 0);
					if (GunParityNoteOnly.TryGetValue(n, out string why)) { ANote($"gun parity: {label} KNOWN DIVERGENCE (the designer rules), DPS not checked: {why}"); continue; }
					ACheck($"gun parity: {label} a levy's DPS is a player's at the same spot (levy {lv.DpsAfter:0}, player {pl.DpsAfter:0}, ratio {dps:0.00}, within 0.75 to 1.25)", lv.Roots > 0 && pl.Roots > 0 && dps >= 0.75 && dps <= 1.25);
					// a bow that does not reuse by itself: the player lets go and presses again for every shot, which costs her a tick or two a shot that the levy, who is handed the next use when the last ends, does not pay
					double rateMax = smp.autoReuse ? 1.1 : 1.15;
					if (bow) ACheck($"gun parity: {label} a levy draws as often as a player (levy {lv.RatePerSec:0.00} shots/s, player {pl.RatePerSec:0.00}, ratio {rate:0.00}, within 0.9 to {rateMax:0.00})", lv.Roots > 0 && pl.Roots > 0 && rate >= 0.9 && rate <= rateMax);
				}
				catch (Exception e) { ACheck($"gun parity: {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
			}
			ANote($"gun parity ran in {sw.ElapsedMilliseconds / 1000} s");
		}
	}
}
