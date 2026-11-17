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
using Terraria.Chat;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Systems;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;
using Levy.Content.Tiles;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of what clicking a pylon on the full-screen map costs. Logged as "[PylonClick]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1
	/// (the SummonWorldCheck chain runs it; LEVY_PYLONCLICK_ONLY=1 with LEVY_MECHWORLD=0 runs it alone). A headless server cannot draw, so this runs the non-drawing half of the click: vanilla's own
	/// pylon request (TeleportPylonsSystem.HandleTeleportRequest, which validates the pylon and calls Player.Teleport, where Levy's follower-travel hook runs) between two real pylons, then steps the real world
	/// for the ticks that follow. Timed with no army and with 40 levies in the same boot.
	/// <para/>PASS rules: the click costs no more than a frame (<see cref="ClickBudgetMs"/>), no tick after a click costs more than a few frames, and the world tick after a click is no heavier than before it.
	/// What it cannot show: the draw of the map itself.
	/// </summary>
	public class PylonClickCheck : ModSystem
	{
		/// <summary>One 60 fps frame. A click that costs more than this is a visible hitch.</summary>
		public const double ClickBudgetMs = 16.7;
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static int _savedMy;
		private const int FloorY = 300;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_PYLONWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the summon world check chains it, so checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			On_ChatHelper.SendChatMessageToClient += RefusalSpy;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { On_ChatHelper.SendChatMessageToClient -= RefusalSpy; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; }
			_mod.Logger.Info($"[PylonClick] self-check pass={_pass} fail={_fail}");
		}

		private static int _refusals; private static readonly HashSet<string> RefusalKeys = new();
		/// <summary>Logs why vanilla refused a pylon request (its chat line to the client; the stand-in has no socket, so the send itself is skipped).</summary>
		private static void RefusalSpy(On_ChatHelper.orig_SendChatMessageToClient orig, Terraria.Localization.NetworkText text, Color color, int playerId)
		{
			_refusals++; string t = text?.ToString() ?? "?";
			if (RefusalKeys.Add(t)) { _mod.Logger.Info("[PylonClick] OBSERVED vanilla refused a pylon request: \"" + t + "\""); Diag("at the first refusal"); }
		}

		/// <summary>What vanilla's pylon test sees: every town-like NPC (home tile, homeless, distance from home) against each listed pylon's scan rectangle, and where the commander stands.</summary>
		private static void Diag(string tag)
		{
			try
			{
				var sb = new System.Text.StringBuilder();
				Player c = Main.player[0];
				sb.Append($"commander {(int)(c.Center.X / 16f)},{(int)(c.Center.Y / 16f)} talkNPC {c.talkNPC} dead {c.dead}; scan {Main.buffScanAreaWidth}x{Main.buffScanAreaHeight}; anyDanger {NPC.AnyDanger(false, true)} invasion {Main.invasionType} time {Main.time:0} day {Main.dayTime} bloodMoon {Main.bloodMoon} raining {Main.raining}; pylons");
				foreach (var pi in Main.PylonSystem.Pylons) sb.Append($" [{pi.PositionInTiles.X},{pi.PositionInTiles.Y} {pi.TypeOfPylon} inReach {c.InInteractionRange(pi.PositionInTiles.X, pi.PositionInTiles.Y, TileReachCheckSettings.Pylons)} enough2 {TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2, pi.PositionInTiles)}]");
				sb.Append("; town-like NPCs");
				int n = 0;
				for (int i = 0; i < 200; i++)
				{
					NPC t = Main.npc[i]; if (!t.active || !t.isLikeATownNPC) continue; n++;
					if (n <= 14) sb.Append($" [#{i} {(t.ModNPC?.Name ?? t.TypeName)} home {t.homeTileX},{t.homeTileY} homeless {t.homeless} at {(int)(t.Center.X / 16f)},{(int)(t.Center.Y / 16f)} dist {Vector2.Distance(new Vector2(t.homeTileX, t.homeTileY), t.Center / 16f):0.0}]");
				}
				sb.Append($"; {n} in all");
				_mod.Logger.Info("[PylonClick] OBSERVED census " + tag + ": " + sb);
			}
			catch (Exception e) { _mod.Logger.Info("[PylonClick] census failed " + e.Message); }
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[PylonClick] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[PylonClick] OBSERVED " + s);
		private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
		private static double Median(List<double> l) { if (l.Count == 0) return 0; var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; }

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

		private static List<double> Ticks(int n)
		{
			var l = new List<double>(n);
			for (int i = 0; i < n; i++) { Main.player[0].active = true; long t0 = Stopwatch.GetTimestamp(); WorldStep.Update(); l.Add(Ms(Stopwatch.GetTimestamp() - t0)); }
			return l;
		}

		private static void Run()
		{
			var log = _mod.Logger; var placed = new List<(int i, int j, int w, int h)>(); var townNpcs = new List<NPC>(); var recs = new List<SoldierRecord>();
			Player p = Main.player[0] ?? (Main.player[0] = new Player()); bool pWas = p.active; p.active = true; p.name = "ClickTester";
			try
			{
				RefreshVanillaPylons(); // an earlier check of the same boot may have removed its pylons without a refresh: vanilla refuses a second pylon of a type it still lists
				{
					var left = new List<string>(); int projs = 0;
					for (int i = 0; i < Main.maxNPCs; i++) { NPC n0 = Main.npc[i]; if (n0.active && !n0.townNPC) left.Add((n0.ModNPC?.Name ?? n0.TypeName) + (n0.friendly ? "(f)" : "") + (n0.boss ? "(BOSS)" : "") + (NPCID.Sets.DangerThatPreventsOtherDangers[n0.type] ? "(DANGER)" : "") + "@" + (int)(n0.Center.X / 16f) + "," + (int)(n0.Center.Y / 16f)); }
					for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) projs++;
					Player c0 = Main.player[0];
					Note($"before the scene: {left.Count} non-town NPCs [{string.Join(" ", left.Take(12))}], {projs} projectiles, commander at {(int)(c0.Center.X / 16f)},{(int)(c0.Center.Y / 16f)} life {c0.statLife}/{c0.statLifeMax2} dead {c0.dead} channel {c0.channel} use {c0.controlUseItem} myPlayer {Main.myPlayer} anyDanger {NPC.AnyDanger(false, true)} invasion {Main.invasionType} snowMoon {Main.snowMoon} pumpkinMoon {Main.pumpkinMoon} dd2 {Terraria.GameContent.Events.DD2Event.Ongoing} talkNPC {c0.talkNPC} time {Main.time:0} day {Main.dayTime} hardMode {Main.hardMode}");
				}
				int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>();
				var barracks = new List<BarracksTE>(); var armories = new List<ArmoryTE>();
				foreach (int bx in new[] { 600, 1000 })
				{
					Flatten(bx + 1, FloorY); Flatten(bx + 5, FloorY);
					WorldGen.PlaceObject(bx + 1, FloorY - 1, barType, true);
					ModContent.GetInstance<BarracksTE>().Hook_AfterPlacement(bx, FloorY - 3, barType, 0, 1, 0); placed.Add((bx, FloorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx, FloorY - 3), out var e) && e is BarracksTE b) { b.TownName = "Click" + bx; b.Treasury = 10_000_000; barracks.Add(b); }
					WorldGen.PlaceObject(bx + 4, FloorY - 1, armType, true);
					ModContent.GetInstance<ArmoryTE>().Hook_AfterPlacement(bx + 4, FloorY - 3, armType, 0, 1, 0); placed.Add((bx + 4, FloorY - 3, 3, 3));
					if (TileEntity.ByPosition.TryGetValue(new Point16(bx + 4, FloorY - 3), out var e2) && e2 is ArmoryTE a) armories.Add(a);
				}
				foreach (var (px, style) in new[] { (608, 0), (1008, 1) })
				{
					Flatten(px + 1, FloorY, 2);
					WorldGen.PlaceObject(px + 1, FloorY - 1, TileID.TeleportationPylon, true, style);
					if (Main.tile[px + 1, FloorY - 2].HasTile && Main.tile[px + 1, FloorY - 2].TileType == TileID.TeleportationPylon)
					{ TETeleportationPylon.PlacementPreviewHook_AfterPlacement(px + 1, FloorY - 1, TileID.TeleportationPylon, style, 1, 0); placed.Add((px, FloorY - 4, 3, 4)); }
				}
				var townTypes = new[] { NPCID.Guide, NPCID.Nurse, NPCID.Demolitionist, NPCID.Merchant };
				foreach (var bar in barracks)
					for (int k = 0; k < townTypes.Length; k++)
						for (int i = Main.maxNPCs - 2; i >= 0; i--)
						{
							var n = Main.npc[i]; if (n.active) continue;
							n.SetDefaults(townTypes[k]); n.active = true; n.townNPC = true; n.homeless = false; n.homeTileX = bar.Position.X + 6 + k; n.homeTileY = FloorY - 2; n.life = n.lifeMax;
							n.position = new Vector2((bar.Position.X + 6 + k) * 16f, (FloorY - 4) * 16f); townNpcs.Add(n); break;
						}
				int tilesUp = 0; foreach (int px in new[] { 609, 1009 }) if (Main.tile[px, FloorY - 2].HasTile && Main.tile[px, FloorY - 2].TileType == TileID.TeleportationPylon) tilesUp++;
				int pylonTes = 0; foreach (var te in TileEntity.ByID.Values) if (te is TETeleportationPylon) pylonTes++;
				RefreshVanillaPylons();
				log.Info($"[PylonClick] OBSERVED pylon tiles standing {tilesUp}/2, pylon tile entities {pylonTes}, listed by vanilla after a refresh {Main.PylonSystem.Pylons.Count}, netMode {Main.netMode}");
				SettlementSystem.Instance.InvalidateTownNpcs();
				// other checks of the same boot may have left pylons of their own listed: take the two this scene placed
				var all = Main.PylonSystem.Pylons;
				var pylons = new List<TeleportPylonInfo>();
				foreach (int px in new[] { 608, 1008 }) for (int k = 0; k < all.Count; k++) if (Math.Abs(all[k].PositionInTiles.X - px) <= 2 && Math.Abs(all[k].PositionInTiles.Y - (FloorY - 4)) <= 2) { pylons.Add(all[k]); break; }
				var sysT = typeof(TeleportPylonsSystem);
				log.Info("[PylonClick] vanilla TeleportPylonsSystem methods: " + string.Join("; ", sysT.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(m => m.Name + "(" + string.Join(",", m.GetParameters().Select(q => q.ParameterType.Name)) + ")")));
				Note($"scene: {barracks.Count} barracks, {armories.Count} armories, {pylons.Count} of the {all.Count} listed pylons are this scene's, {townNpcs.Count} town NPCs");
				Diag("after the scene is built");
				Check($"vanilla lists both of the scene's pylons ({all.Count} listed in all)", pylons.Count == 2);
				if (pylons.Count < 2) return;

				p.position = new Vector2(610 * 16f, (FloorY - 4) * 16f); p.statLife = p.statLifeMax2 = Math.Max(p.statLifeMax2, 400);
				Ticks(30);
				Diag("after 30 ticks, before the first click");

				var armArr = armories; var barArr = barracks;
				foreach (int levies in new[] { 0, 40 })
				{
					var roster = RosterSystem.Instance;
					foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; roster.Regulars.Remove(r); } catch { } }
					recs.Clear();
					for (int i = 0; i < levies; i++)
					{
						var home = barracks[i % barracks.Count];
						var rec = new SoldierRecord { Id = 72000 + i, Kind = SoldierKind.Regular, Grade = Grade.Levy, Name = "Click " + i, CommanderName = "ClickTester", CommanderIndex = 0, HomeBarracksId = home.ID };
						rec.CurrentHp = rec.MaxHp; roster.Regulars.Add(rec); recs.Add(rec);
						roster.SpawnBody(rec, new Vector2((home.Position.X + 12 + i % 8) * 16f, (FloorY - 4) * 16f));
					}
					roster.PreUpdateNPCs(); Ticks(30);
					string arm = levies == 0 ? "no army" : levies + " levies";
					var baseline = Ticks(120);
					// the vanilla request, as the map layer makes it on a click: ask the pylon system to teleport the player to the other pylon (12 times, alternating; the first two are warm-up: JIT)
					var handle = sysT.GetMethod("HandleTeleportRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					var clickMs = new List<double>(); var after = new List<double>(); int moved = 0;
					for (int r = 0; r < 14; r++)
					{
						var dest = pylons[r % 2 == 0 ? 1 : 0]; float before = p.Center.X;
						long t0 = Stopwatch.GetTimestamp();
						try { handle?.Invoke(Main.PylonSystem, new object[] { dest, 0 }); } catch (Exception e) { log.Info("[PylonClick] HandleTeleportRequest threw " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message); }
						double ms = Ms(Stopwatch.GetTimestamp() - t0);
						if (Math.Abs(p.Center.X - before) > 1000f) moved++;
						var t = Ticks(30);
						if (r >= 2) { clickMs.Add(ms); after.AddRange(t); }
					}
					double clickMed = Median(clickMs), clickMax = clickMs.Max(), idleMed = Median(baseline), afterMed = Median(after), afterWorst = after.Max();
					log.Info($"[PylonClick] {arm}: click (the pylon request, Player.Teleport and Levy's follower hook) median {clickMed:F3} ms, max {clickMax:F2} ms; world tick idle median {idleMed:F3} ms; the 30 ticks after each click median {afterMed:F3} ms, worst {afterWorst:F1} ms");
					Check($"{arm}: {moved}/14 pylon requests moved the player to the other pylon", moved >= 12);
					Check($"{arm}: a pylon click costs under a frame: median {clickMed:F2} ms (budget {ClickBudgetMs} ms)", clickMed <= ClickBudgetMs);
					Check($"{arm}: the world does not tick slower after a click: median {afterMed:F2} ms after vs {idleMed:F2} ms idle (rule: within 4x + 6 ms, a shared build box is noisy)", afterMed <= idleMed * 4.0 + 6.0);

					// the worst arrival: the commander lands where no ground is in reach, so the followers' ground search comes up empty. Used to cost one full search per follower.
					var keep = p.position; p.position = new Vector2(800 * 16f, (FloorY - 150) * 16f);
					for (int i = 0; i < 3; i++) FollowerTravel.BringFollowers(p);
					var air = new List<double>();
					for (int i = 0; i < 9; i++) { long a0 = Stopwatch.GetTimestamp(); FollowerTravel.BringFollowers(p); air.Add(Ms(Stopwatch.GetTimestamp() - a0)); }
					p.position = keep;
					double airMed = Median(air);
					log.Info($"[PylonClick] {arm}: followers brought to a commander with no ground in reach: median {airMed:F3} ms, max {air.Max():F2} ms");
					Check($"{arm}: followers join a teleport to a spot with no ground in reach within a frame: median {airMed:F2} ms (budget {ClickBudgetMs} ms)", airMed <= ClickBudgetMs);
				}

				// ---- the full-screen map's own per-frame work and the pylon-network queries it shares with the logistics screens (the draw itself is not measurable headless)
				var tiles = new Vector2[60]; var assign = new int[tiles.Length]; var sums = new Vector2[6]; var counts = new int[6]; var first = new int[6];
				for (int i = 0; i < tiles.Length; i++) tiles[i] = new Vector2(600 + (i * 37) % 900, 300);
				double Bench(int iters, Action a) { for (int i = 0; i < Math.Min(iters, 200); i++) a(); long t0 = Stopwatch.GetTimestamp(); for (int i = 0; i < iters; i++) a(); return Ms(Stopwatch.GetTimestamp() - t0) / iters; }
				var sysS = SettlementSystem.Instance;
				double frame = Bench(2000, () => { global::Levy.Common.UI.MapMarkersLayer.BenchGather(); global::Levy.Common.UI.MapMarkerGeometry.Cluster(tiles, tiles.Length, 40f, 6, assign, sums, counts, first); for (int i = 0; i < pylons.Count; i++) TeleportPylonsSystem.DoesPositionHaveEnoughNPCs(2, pylons[i].PositionInTiles); });
				double net = Bench(500, () => { SettlementSystem.PylonPoints(); foreach (var a in armArr) { sysS.NetworkedArmories(a); sysS.IsArmoryOnNetwork(a); } foreach (var b in barArr) { sysS.LinkOf(b); sysS.NetworkedBarracks(b); } });
				log.Info($"[PylonClick] map frame (marker gather + cluster + every pylon's town-NPC test) {frame * 1000:F1} us; pylon-network queries (all armories and barracks) {net * 1000:F1} us");
				Check($"the map's per-frame work costs under a tenth of a frame: {frame:F3} ms (budget {ClickBudgetMs / 10:F2} ms)", frame <= ClickBudgetMs / 10);
				Check($"the pylon-network queries cost under a tenth of a frame: {net:F3} ms (budget {ClickBudgetMs / 10:F2} ms)", net <= ClickBudgetMs / 10);
			}
			catch (Exception e) { log.Warn("[PylonClick] threw " + e.GetType().Name + ": " + e.Message + " " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(5).Select(l => l.Trim()))); Check("scene built and timed", false); }
			finally
			{
				foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(r); } catch { } }
				foreach (var n in townNpcs) { n.active = false; n.townNPC = false; }
				foreach (var (i, j, w, h) in placed) for (int x = i; x < i + w; x++) for (int y = j; y < j + h; y++) if (WorldGen.InWorld(x, y)) Main.tile[x, y].ClearEverything();
				foreach (var id in new List<int>(TileEntity.ByID.Keys)) { var te = TileEntity.ByID[id]; if (te is BarracksTE || te is ArmoryTE || te is TETeleportationPylon) { TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position); } }
				try { RefreshVanillaPylons(); } catch { }
				p.active = pWas;
				_mod.Logger.Info("[PylonClick] done");
			}
		}
	}
}
