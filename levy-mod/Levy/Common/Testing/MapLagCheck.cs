using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Levy.Common.Logistics;
using Levy.Common.Systems;
using Levy.Common.Soldiers;
using Levy.Common.UI;
using Levy.Content.NPCs;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// What Levy runs while the full-screen map (the pylon teleport map) is open, timed call by call in a populated world. A headless server cannot draw, so this calls the non-drawing halves directly:
	/// the map layer's gathering of officers, army and bosses, the cluster maths, the marker bake, and the settlement / pylon network queries (NetworkedArmories, IsArmoryOnNetwork, LinkOf, PylonPoints,
	/// Shopping reach), beside vanilla's own pylon checks. Env LEVY_MAPLAG=1 on a dedicated server with a world (LEVY_MAPLAG_LEVIES default 40). Logs "[MapLag]" lines: microseconds and bytes allocated per call.
	/// What it cannot show: the on-screen frame time of the draw itself (sprite batches, the baked textures' upload) and vanilla's own layer.
	/// </summary>
	public class MapLagCheck : ModSystem
	{
		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_MAPLAG") != "1") return;
			global::Levy.Common.Util.LoadSafe.Run(Run);
		}

		private static void Flatten(int cx, int floorY, int half = 14)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = floorY - 14; j < floorY; j++) { var t = Main.tile[i, j]; t.ClearEverything(); }
				var f = Main.tile[i, floorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
			}
		}

		private static void RefreshVanillaPylons()
		{
			var sys = Main.PylonSystem; var t = sys.GetType();
			foreach (var name in new[] { "RequestImmediateUpdate", "Update" })
				t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(sys, null);
		}

		private void Run()
		{
			int levies = int.TryParse(Environment.GetEnvironmentVariable("LEVY_MAPLAG_LEVIES"), out int lv) && lv > 0 ? lv : 40;
			var log = Mod.Logger; var placed = new List<(int i, int j, int w, int h)>(); var npcs = new List<NPC>(); var recs = new List<SoldierRecord>();
			try
			{
				int floorY = 300; int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
				var barracks = new List<BarracksTE>(); var armories = new List<ArmoryTE>(); int pylonsPlaced = 0;
				foreach (int bx in new[] { 600, 700, 1000, 1100 })
				{
					Flatten(bx + 1, floorY); Flatten(bx + 5, floorY);
					WorldGen.PlaceObject(bx + 1, floorY - 1, barType, true);
					ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(bx, floorY - 3, barType, 0, 1, 0); placed.Add((bx, floorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx, floorY - 3), out var e) && e is BarracksTE b) { b.TownName = "Lag" + bx; b.Treasury = 10_000_000; barracks.Add(b); }
					WorldGen.PlaceObject(bx + 4, floorY - 1, armType, true);
					ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(bx + 4, floorY - 3, armType, 0, 1, 0); placed.Add((bx + 4, floorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx + 4, floorY - 3), out var e2) && e2 is ArmoryTE a) armories.Add(a);
				}
				foreach (var (px, style) in new[] { (608, 0), (1008, 1), (1500, 2) })
				{
					Flatten(px + 1, floorY, 2);
					WorldGen.PlaceObject(px + 1, floorY - 1, TileID.TeleportationPylon, true, style);
					if (Main.tile[px + 1, floorY - 2].HasTile && Main.tile[px + 1, floorY - 2].TileType == TileID.TeleportationPylon)
					{
						TETeleportationPylon.PlacementPreviewHook_AfterPlacement(px + 1, floorY - 1, TileID.TeleportationPylon, style, 1, 0); placed.Add((px, floorY - 4, 3, 4)); pylonsPlaced++;
					}
				}
				var townTypes = new[] { NPCID.Guide, NPCID.Nurse, NPCID.Demolitionist, NPCID.Merchant, NPCID.Dryad, NPCID.ArmsDealer };
				foreach (var bar in barracks)
					for (int k = 0; k < townTypes.Length; k++)
						for (int i = Main.maxNPCs - 2; i >= 0; i--)
						{
							var n = Main.npc[i]; if (n.active) continue;
							n.SetDefaults(townTypes[k]); n.active = true; n.townNPC = true; n.homeless = false; n.homeTileX = bar.Position.X + 6 + k; n.homeTileY = floorY - 2; n.life = n.lifeMax;
							n.position = new Vector2((bar.Position.X + 6 + k) * 16f, (floorY - 4) * 16f); npcs.Add(n); break;
						}
				RefreshVanillaPylons();
				var sys = SettlementSystem.Instance; sys.InvalidateTownNpcs();
				var roster = RosterSystem.Instance;
				for (int i = 0; i < levies; i++)
				{
					var home = barracks[i % barracks.Count];
					var rec = new SoldierRecord { Id = 70000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Lag " + i, CommanderName = "MapLag", CommanderIndex = 0, HomeBarracksId = home.ID };
					rec.CurrentHp = rec.MaxHp; roster.Regulars.Add(rec); recs.Add(rec);
					roster.SpawnBody(rec, new Vector2((home.Position.X + 12 + i % 8) * 16f, (floorY - 4) * 16f));
				}
				var player = Main.player[0]; bool pWas = player.active; player.active = true; player.position = barracks[0].Center;
				roster.PreUpdateNPCs(); // rebuilds the cache of live bodies (an idle test server does not tick)
				log.Info($"[MapLag] world: {barracks.Count} barracks, {armories.Count} armories, {pylonsPlaced} pylons placed ({Main.PylonSystem.Pylons.Count} listed by vanilla), {recs.Count(r => r.HasBody)} levy bodies, {npcs.Count} town NPCs");

				// vanilla's own pylon check and the list walk the map layer's pylons go through
				var pyl = Main.PylonSystem.Pylons;
				var methods = typeof(TeleportPylonsSystem).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).Distinct().ToList();
				log.Info("[MapLag] vanilla TeleportPylonsSystem methods: " + string.Join(", ", methods));
				Type layer = typeof(Main).Assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("TeleportPylonsMapLayer"));
				log.Info("[MapLag] vanilla pylon map layer type: " + (layer?.FullName ?? "none found") + (layer != null ? " methods: " + string.Join(", ", layer.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name).Distinct()) : ""));

				var rows = new List<(string, double, double)>();
				void Bench(string name, int iters, Action a)
				{
					for (int i = 0; i < Math.Min(iters, 200); i++) a(); // warm
					GC.Collect(); GC.WaitForPendingFinalizers();
					long b0 = GC.GetAllocatedBytesForCurrentThread(); long t0 = Stopwatch.GetTimestamp();
					for (int i = 0; i < iters; i++) a();
					double us = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency / iters; double by = (GC.GetAllocatedBytesForCurrentThread() - b0) / (double)iters;
					rows.Add((name, us, by));
				}
				// ---- vanilla
				Bench("vanilla: walk Main.PylonSystem.Pylons", 20000, () => { long s = 0; for (int i = 0; i < pyl.Count; i++) s += pyl[i].PositionInTiles.X; });
				Bench("vanilla: DoesPositionHaveEnoughNPCs(2) for every pylon", 2000, () => { for (int i = 0; i < pyl.Count; i++) TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2, pyl[i].PositionInTiles); });
				// ---- Levy map layer, the non-drawing half
				Bench("Levy map layer: gather officers + army + bosses", 20000, MapMarkersLayer.BenchGather);
				var tiles = new Vector2[Math.Max(1, levies)]; var assign = new int[tiles.Length]; var sums = new Vector2[6]; var counts = new int[6]; var first = new int[6];
				for (int i = 0; i < tiles.Length; i++) tiles[i] = new Vector2(600 + (i * 37) % 900, 300);
				Bench("Levy map layer: cluster the army into 6 markers", 20000, () => MapMarkerGeometry.Cluster(tiles, tiles.Length, 40f, 6, assign, sums, counts, first));
				Bench("Levy map layer: bake one 100 px ring (first sight of a zoom level)", 40, () => MapMarkerGeometry.BakePixels(MarkerShape.Ring, 100, 3));
				Bench("Levy map layer: bake one 200 px ring (largest)", 20, () => MapMarkerGeometry.BakePixels(MarkerShape.Ring, 200, 3));
				Bench("Levy map layer: bake one 12 px disc", 2000, () => MapMarkerGeometry.BakePixels(MarkerShape.Disc, 12, 0));
				// ---- Levy settlement / pylon network
				Bench("SettlementSystem.NetworkedArmories (one armory)", 20000, () => sys.NetworkedArmories(armories[0]));
				Bench("SettlementSystem.NetworkedArmories (all armories)", 5000, () => { foreach (var a in armories) sys.NetworkedArmories(a); });
				Bench("SettlementSystem.IsArmoryOnNetwork (all armories)", 20000, () => { foreach (var a in armories) sys.IsArmoryOnNetwork(a); });
				Bench("SettlementSystem.LinkOf (all barracks)", 20000, () => { foreach (var b in barracks) sys.LinkOf(b); });
				Bench("SettlementSystem.NetworkedBarracks (all barracks)", 5000, () => { foreach (var b in barracks) sys.NetworkedBarracks(b); });
				Bench("SettlementSystem.PylonPoints (every pylon, vanilla valid check each)", 2000, () => SettlementSystem.PylonPoints());
				Bench("SettlementSystem.OnPylonNet (a point)", 20000, () => SettlementSystem.OnPylonNet(armories[0].Center));
				Bench("Shopping.KeepersReach (one armory)", 2000, () => Shopping.KeepersReach(armories[0]));
				Bench("ArmoryTE.Update (one armory tick)", 5000, () => { foreach (var a in armories) a.Update(); });
				var sbd = new System.Text.StringBuilder("[MapLag] per call (microseconds, bytes allocated):\n");
				foreach (var (n, us, by) in rows) sbd.Append($"[MapLag]   {n,-72} {us,10:F2} us {by,10:F0} B\n");
				log.Info(sbd.ToString().TrimEnd());
				player.active = pWas;
			}
			catch (Exception e) { log.Warn("[MapLag] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))); }
			finally
			{
				foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(r); } catch { } }
				foreach (var n in npcs) { n.active = false; n.townNPC = false; }
				foreach (var (i, j, w, h) in placed) for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
				foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE || te is TETeleportationPylon) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
				try { RefreshVanillaPylons(); } catch { }
				log.Info("[MapLag] done");
			}
		}
	}
}
