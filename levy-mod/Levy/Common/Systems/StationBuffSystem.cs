using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.Systems
{
	/// <summary>Placed buff stations a soldier can benefit from while near an armory.</summary>
	[Flags]
	public enum StationFlags
	{
		None = 0,
		Campfire = 1 << 0,        // TileID.Campfire (lit) / Fireplace (lit): +1 lifeRegen
		HeartLantern = 1 << 1,    // TileID.HangingLanterns heart styles: +2 lifeRegen
		StarInBottle = 1 << 2,    // TileID.HangingLanterns star styles: mana regen
		Sunflower = 1 << 3,       // TileID.Sunflower: Happy! move speed
		CatBast = 1 << 4,         // TileID.CatBast: +5 defense
		AmmoBox = 1 << 5,         // TileID.AmmoBox: 20% free ammo
		Sharpening = 1 << 6,      // TileID.SharpeningStation: Sharpened
		CrystalBall = 1 << 7,     // TileID.CrystalBall: Clairvoyance
		Bewitching = 1 << 8,      // TileID.BewitchingTable: Bewitched (+1 minion)
		SliceOfCake = 1 << 9,     // TileID.SliceOfCake: Sugar Rush
		WarTable = 1 << 10,       // TileID.WarTable: War Table (+1 sentry slot)
		Honey = 1 << 11,          // standing in honey: Honey (+2 life regen); never from a tile scan
		All = (1 << 12) - 1,
		/// <summary>The stations vanilla reads from the scene scan (a 169x124 tile box centred on the player): a placed tile within that box is the buff.</summary>
		Scene = Campfire | HeartLantern | StarInBottle | Sunflower | CatBast,
	}

	/// <summary>
	/// Per-armory scan for vanilla buff stations (tile conditions copied from SceneMetrics.ScanAndExportToMain and the station buff
	/// rules in Player.UpdateBuffs), cached and refreshed every <see cref="LevyConstants.StationScanIntervalTicks"/>. A soldier within
	/// <see cref="LevyConstants.StationBenefitRadiusTiles"/> of an armory gets that armory's stations (union over all such armories).
	/// Server / single player only; the soldier's stat pipeline and regen consume the result, and HP/stats already sync.
	/// </summary>
	public class StationBuffSystem : ModSystem
	{
		private struct Entry { public Vector2 Center; public StationFlags Flags; }

		/// <summary>Armory-side stations (every kind), cached per armory; a soldier within StationBenefitRadiusTiles of the armory gets them.</summary>
		private static readonly List<Entry> _cache = new();
		/// <summary>Scene stations (campfire, heart lantern, star in a bottle, sunflower, bast statue) found around every armory and every player, as tile-centre positions per flag bit.</summary>
		private static readonly List<Vector2>[] _scene = MakeScene();
		private static List<Vector2>[] MakeScene() { var a = new List<Vector2>[12]; for (int i = 0; i < a.Length; i++) a[i] = new List<Vector2>(); return a; }
		private static readonly HashSet<long> _sceneSeen = new();
		private static uint _stamp = uint.MaxValue;

		public override void OnWorldLoad() => Reset();
		public override void OnWorldUnload() => Reset();
		public override void ClearWorld() => Reset();
		private static void Reset() { _cache.Clear(); foreach (var l in _scene) l.Clear(); _sceneSeen.Clear(); _stamp = uint.MaxValue; }

		/// <summary>Probe seam (the combat harness): when set, every soldier counts as standing beside exactly these stations.</summary>
		internal static StationFlags? ForceFlags;

		/// <summary>
		/// Every station a soldier at <paramref name="pos"/> benefits from (cached; cheap per call): the stations beside an armory within benefit range,
		/// the scene stations within vanilla's 169x124 tile scan box of the soldier (what a player standing there would get), and the station buffs
		/// the commander has activated himself (Sharpened, Ammo Box, Clairvoyance, Bewitched, War Table, Sugar Rush).
		/// </summary>
		public static StationFlags For(Vector2 pos, Player commander = null)
		{
			if (ForceFlags.HasValue) return ForceFlags.Value;
			if (!LevyUtil.IsServerOrSingle) return StationFlags.None;
			Refresh();
			StationFlags f = StationFlags.None;
			float r = LevyConstants.StationBenefitRadiusTiles * LevyConstants.TilePx; r *= r;
			for (int i = 0; i < _cache.Count; i++)
				if (_cache[i].Flags != StationFlags.None && Vector2.DistanceSquared(_cache[i].Center, pos) <= r) f |= _cache[i].Flags;
			f |= SceneFlags(pos);
			if (InHoney(pos)) f |= StationFlags.Honey;
			if (commander != null) f |= CommanderFlags(commander);
			return f;
		}

		/// <summary>Scene stations within vanilla's scan box of a point: |dx| <= 84 and |dy| <= 62 tiles (Main.buffScanAreaWidth/Height halved).</summary>
		public static StationFlags SceneFlags(Vector2 pos)
		{
			float hx = Main.buffScanAreaWidth / 2 * LevyConstants.TilePx, hy = Main.buffScanAreaHeight / 2 * LevyConstants.TilePx;
			StationFlags f = StationFlags.None;
			for (int bit = 0; bit < _scene.Length; bit++)
			{
				List<Vector2> l = _scene[bit];
				for (int i = 0; i < l.Count; i++)
				{
					Vector2 t = l[i];
					if (Math.Abs(t.X - pos.X) <= hx && Math.Abs(t.Y - pos.Y) <= hy) { f |= (StationFlags)(1 << bit); break; }
				}
			}
			return f;
		}

		/// <summary>The station buffs a player has switched on by using a station (30 minutes in vanilla). They reach all of his levies wherever they are.</summary>
		public static StationFlags CommanderFlags(Player p)
		{
			StationFlags f = StationFlags.None;
			if (p == null || !p.active) return f;
			if (p.HasBuff(BuffID.Sharpened)) f |= StationFlags.Sharpening;
			if (p.HasBuff(BuffID.AmmoBox)) f |= StationFlags.AmmoBox;
			if (p.HasBuff(BuffID.Clairvoyance)) f |= StationFlags.CrystalBall;
			if (p.HasBuff(BuffID.Bewitched)) f |= StationFlags.Bewitching;
			if (p.HasBuff(BuffID.WarTable)) f |= StationFlags.WarTable;
			if (p.HasBuff(BuffID.SugarRush)) f |= StationFlags.SliceOfCake;
			return f;
		}

		/// <summary>True if a soldier standing at <paramref name="pos"/> is in liquid honey (vanilla gives a player the Honey buff there).</summary>
		public static bool InHoney(Vector2 pos)
		{
			Point c = pos.ToTileCoordinates();
			if (!WorldGen.InWorld(c.X, c.Y, 2)) return false;
			Tile t = Main.tile[c.X, c.Y];
			return t.LiquidAmount > 0 && t.LiquidType == LiquidID.Honey;
		}

		private static void Refresh()
		{
			uint now = Main.GameUpdateCount;
			if (_stamp != uint.MaxValue && now - _stamp < (uint)LevyConstants.StationScanIntervalTicks) return;
			_stamp = now;
			_cache.Clear();
			foreach (var l in _scene) l.Clear();
			_sceneSeen.Clear();
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not ArmoryTE a) continue;
				try { _cache.Add(new Entry { Center = a.Center, Flags = Scan(a.Center) }); ScanScene(a.Center); } catch (Exception) { }
			}
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p == null || !p.active || p.dead) continue;
				try { ScanScene(p.Center); } catch (Exception) { }
			}
		}

		/// <summary>
		/// Records the scene stations in the box around a source (the scan box plus a margin, so soldiers up to that far from the source are exact).
		/// Tile tests are the ones in SceneMetrics.ScanAndExportToMain (the Sunflower is any tile of type Sunflower).
		/// </summary>
		private static void ScanScene(Vector2 center)
		{
			Point c = center.ToTileCoordinates();
			int rx = Main.buffScanAreaWidth / 2 + LevyConstants.StationSceneMarginTiles, ry = Main.buffScanAreaHeight / 2 + LevyConstants.StationSceneMarginTiles;
			int x0 = Math.Max(1, c.X - rx), x1 = Math.Min(Main.maxTilesX - 2, c.X + rx);
			int y0 = Math.Max(1, c.Y - ry), y1 = Math.Min(Main.maxTilesY - 2, c.Y + ry);
			for (int x = x0; x <= x1; x++)
				for (int y = y0; y <= y1; y++)
				{
					Tile t = Main.tile[x, y];
					if (!t.HasTile) continue;
					StationFlags f = StationFlags.None;
					switch (t.TileType)
					{
						case TileID.Campfire: if (t.TileFrameY < 36) f = StationFlags.Campfire; break;
						case TileID.Fireplace: if (t.TileFrameX < 54) f = StationFlags.Campfire; break;
						case TileID.CatBast: if (t.TileFrameX < 72) f = StationFlags.CatBast; break;
						case TileID.HangingLanterns:
							if (t.TileFrameY >= 324 && t.TileFrameY <= 358) f = StationFlags.HeartLantern;
							else if (t.TileFrameY >= 252 && t.TileFrameY <= 286) f = StationFlags.StarInBottle;
							break;
						case TileID.Sunflower: f = StationFlags.Sunflower; break;
					}
					if (f == StationFlags.None) continue;
					if (!_sceneSeen.Add(((long)x << 32) | (uint)y)) continue;
					int bit = 0; while (((int)f >> bit) != 1) bit++;
					_scene[bit].Add(new Vector2(x * LevyConstants.TilePx + 8f, y * LevyConstants.TilePx + 8f));
				}
		}

		/// <summary>Scans the box around an armory for station tiles. Stops early once every station has been seen.</summary>
		private static StationFlags Scan(Vector2 center)
		{
			Point c = center.ToTileCoordinates();
			int x0 = Math.Max(1, c.X - LevyConstants.StationScanRadiusX), x1 = Math.Min(Main.maxTilesX - 2, c.X + LevyConstants.StationScanRadiusX);
			int y0 = Math.Max(1, c.Y - LevyConstants.StationScanRadiusY), y1 = Math.Min(Main.maxTilesY - 2, c.Y + LevyConstants.StationScanRadiusY);
			StationFlags f = StationFlags.None;
			for (int x = x0; x <= x1; x++)
			{
				for (int y = y0; y <= y1; y++)
				{
					Tile t = Main.tile[x, y];
					if (!t.HasTile) continue;
					switch (t.TileType)
					{
						case TileID.Campfire: if (t.TileFrameY < 36) f |= StationFlags.Campfire; break;
						case TileID.Fireplace: if (t.TileFrameX < 54) f |= StationFlags.Campfire; break;
						case TileID.CatBast: if (t.TileFrameX < 72) f |= StationFlags.CatBast; break;
						case TileID.HangingLanterns:
							if (t.TileFrameY >= 324 && t.TileFrameY <= 358) f |= StationFlags.HeartLantern;
							else if (t.TileFrameY >= 252 && t.TileFrameY <= 286) f |= StationFlags.StarInBottle;
							break;
						case TileID.Sunflower: f |= StationFlags.Sunflower; break;
						case TileID.AmmoBox: f |= StationFlags.AmmoBox; break;
						case TileID.SharpeningStation: f |= StationFlags.Sharpening; break;
						case TileID.CrystalBall: f |= StationFlags.CrystalBall; break;
						case TileID.BewitchingTable: f |= StationFlags.Bewitching; break;
						case TileID.SliceOfCake: f |= StationFlags.SliceOfCake; break;
						case TileID.WarTable: f |= StationFlags.WarTable; break;
					}
					if (f == StationFlags.All) return f;
				}
			}
			return f;
		}

		/// <summary>Life regen in Player.lifeRegen units contributed by the stations (vanilla heals 1 HP per 120 units).</summary>
		public static int LifeRegen(StationFlags f)
		{
			int r = 0;
			if ((f & StationFlags.Campfire) != 0) r += LevyConstants.StationCampfireLifeRegen;
			if ((f & StationFlags.Honey) != 0) r += LevyConstants.StationHoneyLifeRegen;
			if ((f & StationFlags.HeartLantern) != 0) r += LevyConstants.StationHeartLanternLifeRegen;
			return r;
		}

		/// <summary>
		/// Applies the stats vanilla's station buffs grant to a soldier's dummy Player. Call after ResetEffects/UpdateEquips/UpdateArmorSets
		/// and before the stats are read. The dummy does not run UpdateBuffs, so each buff's stat effect is applied directly.
		/// </summary>
		public static void Apply(Player d, StationFlags f)
		{
			if (f == StationFlags.None) return;
			if ((f & StationFlags.CatBast) != 0) d.statDefense += LevyConstants.CatBastDefense;                 // Cat Bast buff 215
			if ((f & StationFlags.Sunflower) != 0) { d.moveSpeed += 0.1f; d.moveSpeed *= 1.1f; d.sunflower = true; } // Happy! 146
			if ((f & StationFlags.SliceOfCake) != 0) d.moveSpeed += 0.2f;                                         // Sugar Rush 192 (pick speed is irrelevant)
			if ((f & StationFlags.StarInBottle) != 0) { d.manaRegenBonus += 10; d.manaRegenDelayBonus += 0.5f; }  // Star in a Bottle 158 (the mana tick itself runs in Damage)
			if ((f & StationFlags.Sharpening) != 0) d.GetArmorPenetration(DamageClass.Melee) += LevyConstants.SharpenedArmorPen; // Sharpened 159
			if ((f & StationFlags.CrystalBall) != 0)                                                              // Clairvoyance 29
			{
				d.GetCritChance(DamageClass.Magic) += 2f;
				d.GetDamage(DamageClass.Magic) += 0.05f;
				d.manaCost -= 0.02f;
			}
			if ((f & StationFlags.Bewitching) != 0) d.maxMinions++;                                               // Bewitched 150
			if ((f & StationFlags.WarTable) != 0) d.maxTurrets++;                                                 // War Table 348
			if ((f & StationFlags.AmmoBox) != 0)                                                                  // Ammo Box 93
			{
				d.ammoBox = true;
				if (d.TryGetModPlayer(out StationAmmoPlayer p)) p.AmmoBox = true;
			}
		}
	}

	/// <summary>
	/// Carries the Ammo Box flag on a soldier's dummy Player. The soldier ammo path (WeaponUse.AmmoFreeThisShot) asks
	/// CombinedHooks.CanConsumeAmmo, which reaches this hook; returning false means the shot is free. Real players never set the flag.
	/// </summary>
	public class StationAmmoPlayer : ModPlayer
	{
		public bool AmmoBox;
		public override void ResetEffects() => AmmoBox = false;
		public override bool CanConsumeAmmo(Item weapon, Item ammo) => !(AmmoBox && Main.rand.NextBool(LevyConstants.StationAmmoBoxDenominator));
	}
}
