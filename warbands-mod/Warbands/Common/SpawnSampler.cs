using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// Headless spawn sampler (env WARBANDS_SAMPLE=1, dedicated server, no players needed; nothing happens without it): draws standing spots across the loaded world, reads their biome the way the game does (a scene scan
	/// and the vanilla zone rules on a stand-in player), and reports for every band type how often a band forms and how many of its members stand free of the terrain (a body inside a tile is a soldier that never
	/// appears), plus the natural roll end to end for every biome and stage. Writes "[Warbands] sample ..." lines to the log.
	/// </summary>
	public class SpawnSampler : ModSystem
	{
		private bool _ran; private int _phase;
		private static bool Wanted => Environment.GetEnvironmentVariable("WARBANDS_SAMPLE") == "1";

		// A dedicated server with nobody online does not advance the world; step Main.Update from the server loop (same driver as WarbandsLab).
		private static bool _inDrive, _driving; private static System.Reflection.MethodInfo _upd;
		private delegate void OrigUpdateConnectedClients();
		private delegate void HookUpdateConnectedClients(OrigUpdateConnectedClients orig);
		private static void KeepGoing(OrigUpdateConnectedClients orig) { if (_driving) return; orig(); }
		public override void Load()
		{
			if (!Wanted || !Main.dedServ) return;
			Main.OnTickForThirdPartySoftwareOnly += Drive;
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepGoing)); } catch (Exception e) { Mod.Logger.Warn("[Warbands] sample: could not hook UpdateConnectedClients: " + e.Message); }
		}
		public override void Unload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } }
		public override void OnWorldLoad() { if (Wanted && Main.dedServ) { _driving = true; CalamityZones.ScannedProbe = true; } }
		private void Drive()
		{
			if (!_driving || _inDrive || _phase >= 9 || Main.netMode != NetmodeID.Server) return;
			_inDrive = true;
			try
			{
				_upd ??= typeof(Main).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
				for (int i = 0; i < 20 && _phase < 9; i++) _upd.Invoke(Main.instance, new object[] { new GameTime() });
			}
			catch (Exception e) { Mod.Logger.Warn("[Warbands] sample: world update threw: " + (e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e)); }
			finally { _inDrive = false; }
		}

		public override void PostUpdateWorld()
		{
			if (!Wanted || Main.netMode == NetmodeID.MultiplayerClient) return;
			if (!_ran && Main.GameUpdateCount > 300) { _ran = true; try { Phase1(); _phase = 1; } catch (Exception e) { Mod.Logger.Error("[Warbands] sample threw: " + e); _phase = 9; } }
			else if (_phase == 1) { try { LiveStep(); } catch (Exception e) { Mod.Logger.Error("[Warbands] sample live threw: " + e); _phase = 2; } }
			else if (_phase == 2) { _phase = 9; Mod.Logger.Info($"[Warbands] sample TOTAL bands tried {_triedAll} formed {_formedAll} healthy {_healthyAll}"); Mod.Logger.Info("[Warbands] sample done"); }
		}

		private static Player Stand(int tx, int ty)
		{
			var p = new Player(); p.width = 20; p.height = 42;
			p.position = new Vector2(tx * 16f - 10f, ty * 16f - 42f); // feet on the top of floor tile (tx, ty)
			return p;
		}

		/// <summary>Biome of a stand-in player at a spot: the scene scan the real player would have had, then the zone lines of Player.UpdateBiomes (repeated here because that method touches client-only visuals, null on a server).</summary>
		public static bool LastSunken;
		public static BandBiome BiomeAt(Player p, out bool surface)
		{
			Main.SceneMetrics.ScanAndExportToMain(new SceneMetricsScanSettings { VisualScanArea = null, BiomeScanCenterPositionInWorld = p.Center, ScanOreFinderData = false });
			var m = Main.SceneMetrics; var c = p.Center.ToTileCoordinates();
			p.ZoneDungeon = m.DungeonTileCount >= 250 && p.Center.Y > Main.worldSurface * 16.0 && Main.tile[c.X, c.Y] != null && Main.wallDungeon[Main.tile[c.X, c.Y].WallType];
			p.ZoneCorrupt = m.EnoughTilesForCorruption; p.ZoneCrimson = m.EnoughTilesForCrimson; p.ZoneHallow = m.EnoughTilesForHallow;
			p.ZoneJungle = m.EnoughTilesForJungle && p.position.Y / 16f < (float)Main.UnderworldLayer; p.ZoneSnow = m.EnoughTilesForSnow; p.ZoneDesert = m.EnoughTilesForDesert; p.ZoneGlowshroom = m.EnoughTilesForGlowingMushroom;
			p.ZoneUnderworldHeight = c.Y > Main.UnderworldLayer; p.ZoneRockLayerHeight = c.Y <= Main.UnderworldLayer && c.Y > Main.rockLayer; p.ZoneDirtLayerHeight = c.Y <= Main.rockLayer && c.Y > Main.worldSurface;
			surface = p.Center.Y / 16f <= (float)Main.worldSurface;
			LastSunken = CalamityZones.InSunkenSea(p, true);
			return BandData.BiomeOf(p);
		}

		/// <summary>The body overlaps a solid tile, shrunk 3 px each side so a soldier standing on a slope or a half block is not counted.</summary>
		private static bool Buried(NPC n) => Collision.SolidCollision(n.position + new Vector2(3f, 3f), n.width - 6, n.height - 6);

		private static bool Passable(int x, int y) { if (x < 1 || y < 1 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1) return false; var t = Main.tile[x, y]; return !(t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]); }

		private Dictionary<string, List<(int x, int y)>> _spots; private Random _rng; private int _ti; private uint _liveAt;
		private readonly List<(int id, int embedded0, int n0)> _cur = new();
		private int _formedAll, _triedAll, _healthyAll, _curTried; private string _curNote = "";

		private void Phase1()
		{
			var rng = new Random(20261111);
			var spots = new Dictionary<string, List<(int x, int y)>>();
			var seen = new Dictionary<string, int>();
			int tries = 0;
			// candidate standing spots: random columns, on the surface or under it; the first floor below the pick with clear air over it
			while (tries++ < 9000)
			{
				int x = rng.Next(120, Main.maxTilesX - 120), y;
				if (rng.Next(3) == 0) y = rng.Next(80, (int)Main.worldSurface); else y = rng.Next((int)Main.worldSurface, Main.maxTilesY - 120);
				int fy = -1;
				for (int k = 0; k < 60 && y + k < Main.maxTilesY - 100; k++)
				{
					int yy = y + k; var f = Main.tile[x, yy];
					if (f.HasUnactuatedTile && Main.tileSolid[f.TileType] && !Main.tileSolidTop[f.TileType] && Passable(x, yy - 1) && Passable(x, yy - 2) && Passable(x, yy - 3) && Main.tile[x, yy - 1].LiquidAmount == 0) { fy = yy; break; }
				}
				if (fy < 0) continue;
				var biome = BiomeAt(Stand(x, fy), out bool surf);
				string key = biome + (surf ? "/surface" : "/cave") + (LastSunken ? "+SunkenSea" : "");
				seen[key] = seen.GetValueOrDefault(key) + 1;
				if (!spots.TryGetValue(key, out var l)) spots[key] = l = new List<(int, int)>();
				if (l.Count < 60) l.Add((x, fy));
			}
			Mod.Logger.Info("[Warbands] sample world " + Main.maxTilesX + "x" + Main.maxTilesY + " surface " + (int)Main.worldSurface + " spots: " + string.Join(", ", seen.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));

			// ground truth vs detection: places that are plainly jungle (mud and jungle grass all around) against the biome the zone flags give
			{
				var conf = new Dictionary<string, int>(); int n = 0, tries2 = 0;
				while (n < 400 && tries2++ < 20000)
				{
					int x = rng.Next(120, Main.maxTilesX - 120), y = rng.Next(100, Main.maxTilesY - 150);
					if (!Passable(x, y) || !Passable(x, y - 1) || Main.tile[x, y].LiquidAmount > 0) continue;
					int jungle = 0, total = 0;
					for (int dx = -40; dx <= 40; dx += 2) for (int dy = -20; dy <= 20; dy += 2)
					{
						var t = Main.tile[x + dx, y + dy]; if (!t.HasUnactuatedTile) continue; total++;
						if (t.TileType == TileID.JungleGrass || t.TileType == TileID.Mud || t.TileType == TileID.JunglePlants || t.TileType == TileID.JunglePlants2 || t.TileType == TileID.LivingMahogany || t.TileType == TileID.LivingMahoganyLeaves || t.TileType == TileID.Hive) jungle++;
					}
					if (total < 40) continue;
					bool truth = jungle >= total * 0.45f; if (!truth && jungle > total * 0.05f) continue;
					var biome = BiomeAt(Stand(x, y + 1), out bool surf);
					string k = (truth ? "JUNGLE" : "other ") + " -> " + biome + (surf ? "/surface" : "/cave"); conf[k] = conf.GetValueOrDefault(k) + 1; n++;
				}
				Mod.Logger.Info("[Warbands] sample jungle detection (truth -> biome read): " + string.Join(", ", conf.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
			}

			// the natural roll end to end: for each biome and world stage, form a band the way the roll does, note which type it picks, how many members stand inside terrain, then remove it
			foreach (var kv in spots.OrderBy(k => k.Key))
				for (int st = 1; st < Stage.Count; st++)
				{
					int ok = 0, tot = 0, embed = 0, mem = 0, neut = 0; var types = new Dictionary<string, int>();
					foreach (var (x, y) in kv.Value.Take(8))
					{
						var p = Stand(x, y); BiomeAt(p, out _); tot++;
						int firstId = BandBrain.PeekNextId();
						if (BandSpawner.SpawnBand(p, st, 4)) { ok++; var b = BandBrain.Get(firstId); if (b != null) { if (b.Neutral) neut++; string id = b.Type.Id ?? b.Type.Name; types[id] = types.GetValueOrDefault(id) + 1; } }
						for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC l && l.BandId >= firstId) { mem++; if (Buried(n)) embed++; n.active = false; } }
						BandBrain.All.Remove(firstId);
					}
					Mod.Logger.Info($"[Warbands] sample natural {kv.Key,-20} stage {st} formed {ok}/{tot} neutral {neut} membersInsideTerrain {embed}/{mem} types {string.Join(" ", types.Select(t => t.Key + "x" + t.Value))}");
				}

			// the (biome, stage) pairs with no band type at all
			var gaps = new List<string>();
			foreach (BandBiome b in Enum.GetValues(typeof(BandBiome)))
				for (int s = 1; s < Stage.Count; s++)
					if ((BandTypes.Pick(b, s, Main.rand) ?? BandData.For(b, s)) == null) gaps.Add($"{b}@{s}");
			Mod.Logger.Info("[Warbands] sample type gaps (biome@stage with no band): " + (gaps.Count == 0 ? "none" : string.Join(",", gaps)));
			var sea = spots.Where(k => k.Key.EndsWith("+SunkenSea")).Sum(k => k.Value.Count);
			Mod.Logger.Info("[Warbands] sample Sunken Sea: zone " + (CalamityZones.SunkenSea == null ? "not available (Calamity not loaded)" : CalamityZones.SunkenSea.FullName) + ", spots found " + sea);
			_spots = spots; _rng = rng; _ti = 0; _liveAt = 0;
		}

		/// <summary>One type at a time (the NPC array is only 200 long): six bands of four, formed 24 to 34 tiles from spots of the type's own biome, then watched for 400 ticks.</summary>
		private void LiveStep()
		{
			if (_ti >= BandTypes.All.Length) { _phase = 2; return; }
			var t = BandTypes.All[_ti];
			if (_liveAt == 0)
			{
				var pool = _spots.Where(k => k.Key.StartsWith(t.Biome + "/") && !k.Key.EndsWith("+SunkenSea")).SelectMany(k => k.Value).ToList(); string note = "";
				if (pool.Count == 0) { pool = _spots.Where(k => k.Key == "Forest/surface").SelectMany(k => k.Value).ToList(); note = $" (no {t.Biome} in this world: Forest surface terrain)"; }
				_cur.Clear(); int tried = 0;
				for (int k = 0; k < 6 && pool.Count > 0; k++)
				{
					var (x, y) = pool[_rng.Next(pool.Count)]; var p = Stand(x, y); BiomeAt(p, out _);
					int id = BandBrain.PeekNextId(); tried++;
					if (!BandSpawner.SpawnBand(p, t, 4, 24, 34, false, false, false)) continue;
					int mem = 0, emb = 0;
					for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC l && l.BandId == id) { mem++; if (Buried(n)) emb++; } }
					_cur.Add((id, emb, mem));
				}
				_curTried = tried; _curNote = note; _liveAt = Main.GameUpdateCount; return;
			}
			if (Main.GameUpdateCount - _liveAt < 400) return;
			int started = 0, buried0 = 0, alive = 0, buried1 = 0, healthy = 0, formed = _cur.Count;
			foreach (var (id, e0, n0) in _cur)
			{
				started += n0; buried0 += e0; int a = 0, e1 = 0;
				for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && n.ModNPC is BandLevyNPC l && l.BandId == id) { a++; if (Buried(n)) e1++; } }
				alive += a; buried1 += e1; if (n0 > 0 && a - e1 >= Math.Max(1, (int)Math.Ceiling(n0 * 0.75))) healthy++;
			}
			_triedAll += _curTried; _formedAll += formed; _healthyAll += healthy;
			Mod.Logger.Info($"[Warbands] sample live {t.Id,-20} bands tried {_curTried} formed {formed} healthy {healthy} (>=75% of members free) | members {started}: inside terrain at spawn {buried0}, alive after 400 ticks {alive}, inside terrain then {buried1}{_curNote}");
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is BandLevyNPC) Main.npc[i].active = false;
			BandBrain.All.Clear(); _ti++; _liveAt = 0;
		}
	}
}
