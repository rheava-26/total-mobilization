using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Flavor;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// FREE REIN LAB (LEVY_HARNESS_SCENARIO=freerein). Measurement only. On the real generated terrain it builds a base (barracks, campfires, beds, a keg, chairs), an elevator shaft down to a cavern, a sheer hole and a pit near it,
	/// puts N armed levies of mixed gear on Free Rein and records, every 20 ticks, how far each is from the base, how deep it is below the base floor, and what it is doing. One boot runs LEVY_FR_RUNS runs at base sites spread over the world.
	/// Env: LEVY_FR_OUT (dir) LEVY_FR_RUNS (4) LEVY_FR_N (15) LEVY_FR_TICKS (game ticks per run, 36000) LEVY_FR_FOES (0|1: day/night monsters near the base, a few in the cavern) LEVY_FR_SPACING (tiles between run sites, 260)
	/// LEVY_FR_TIME (dawn|noon|dusk|midnight: the clock at the start of each run, noon) LEVY_FR_SHAFT (platform spacing in the elevator, 5; 0 = none, no elevator) LEVY_FR_CMD (commander: base|away|none)
	/// LEVY_FR_LIFE=0 turns the off-duty activities off (a lab switch: SoldierNPC.LeisureOverride). LEVY_FREEREIN_LEGACY=1 runs the old Free Rein in the same build.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class FrLevy
		{
			public SoldierRecord Rec; public int Idx = -1; public string Gear = "";
			public float MaxDx, MaxDepth, DistSum; public int Samples, OutTicks, PitTicks, DeepTicks, UgTicks, FirstOut = -1, FirstPit = -1, FirstDeep = -1, BodyLost, Pits, Seated, Moves;
			public bool InPit; public int[] Act = new int[12]; public float FinalDx, FinalDepth; public string FirstOutWhy = "", FirstPitWhy = "", FirstDeepWhy = ""; public Vector2 Last;
			public readonly List<string> Ring = new List<string>(); public int Out200Ticks, Teleports, InHoleTicks, InShaftTicks, InPitTicks; public bool WasInHole, WasInShaft, WasInPit; public int HoleFalls, ShaftFalls, PitFalls;
		}
		private readonly List<FrLevy> _fr = new List<FrLevy>();
		private int _frRuns = 4, _frRun, _frRun0, _frTotal = 4, _frN = 15, _frTicks = 36000, _frTick, _frSpacing = 260, _frShaft = 5;
		private bool _frStarted, _frFoes; private string _frOut = "", _frTime = "noon", _frCmd = "base";
		private int _frBx, _frFy, _frShaftX = -1, _frHoleX = -1, _frPitX = -1; private Vector2 _frBase;
		private readonly StringBuilder _frEvents = new StringBuilder(), _frTrace = new StringBuilder();
		private double _frWallMs; private long _frWallTicks; private System.Diagnostics.Stopwatch _frWatch = new System.Diagnostics.Stopwatch();
		private int _frActMax; private string _frAsciiText = "";
		private static readonly string[] FrActNames = { "none", "kite", "marshmallow", "sit", "keg", "sleep", "stargaze", "fish", "cheer", "?", "?", "?" };

		private bool BeginFreeRein()
		{
			_frOut = Environment.GetEnvironmentVariable("LEVY_FR_OUT") ?? Path.Combine(Path.GetTempPath(), "levy_freerein");
			Directory.CreateDirectory(_frOut);
			int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_RUNS"), out _frRuns); if (_frRuns <= 0) _frRuns = 4;
			int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_RUN0"), out _frRun0); _frTotal = int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_TOTAL"), out int tot) && tot > 0 ? tot : _frRuns;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_N"), out int n) && n > 0) _frN = n;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_TICKS"), out int t) && t > 600) _frTicks = t;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_SPACING"), out int sp) && sp > 0) _frSpacing = sp;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_FR_SHAFT"), out int sh)) _frShaft = sh;
			_frFoes = Environment.GetEnvironmentVariable("LEVY_FR_FOES") == "1";
			_frTime = Environment.GetEnvironmentVariable("LEVY_FR_TIME") ?? "noon"; _frCmd = Environment.GetEnvironmentVariable("LEVY_FR_CMD") ?? "base";
			string life = Environment.GetEnvironmentVariable("LEVY_FR_LIFE"); if (life == "0") SoldierNPC.LeisureOverride = 0; else if (life == "1") SoldierNPC.LeisureOverride = 1;
			OffscreenHop.LabNoHop = true;
			_frRun = 0; _frStarted = false; _frTick = 0;
			return true;
		}

		// ---------------- world ----------------
		private static int FrSurfaceRow(int x, int fromY)
		{
			for (int y = Math.Max(10, fromY); y < Main.maxTilesY - 10; y++) { Tile t = Main.tile[x, y]; if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) return y; }
			return -1;
		}

		private void FrBuildBase(int run)
		{
			int site = Main.spawnTileX + (int)Math.Round((run - (_frTotal - 1) / 2f) * _frSpacing);
			site = Math.Clamp(site, 220, Main.maxTilesX - 220);
			int srf = FrSurfaceRow(site, (int)(Main.worldSurface * 0.55));
			if (!TvFindRoom(site, srf > 0 ? srf - 1 : (int)Main.worldSurface - 20, 150, 50, out int fx, out int fy)) { fx = site; fy = srf > 0 ? srf - 1 : (int)Main.worldSurface; _frEvents.AppendLine("BASE: no room found near " + site); }
			// the pad: a flat ledge 56 wide (floor row fy+1 is solid), 14 clear rows above, so the props stand on level ground; the natural terrain runs on either side of it
			int floor = fy + 1;
			for (int x = fx - 28; x <= fx + 28; x++)
			{
				for (int y = floor - 16; y < floor; y++) { TvAir(x, y); Main.tile[x, y].WallType = 0; }
				for (int y = floor; y < floor + 5; y++) { if (!Main.tile[x, y].HasTile || !Main.tileSolid[Main.tile[x, y].TileType]) TvSet(x, y, y == floor ? TileID.Grass : TileID.Dirt); Main.tile[x, y].LiquidAmount = 0; }
			}
			_frBx = fx; _frFy = floor;
						foreach (var te in TileEntity.ByID.Values.OfType<BarracksTE>().ToList()) { TileEntity.ByPosition.Remove(te.Position); TileEntity.ByID.Remove(te.ID); }
			try { WorldGen.PlaceObject(fx - 12, floor - 1, ModContent.TileType<BarracksTile>(), true, 0); } catch { }
			BarracksTE barracks = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault();
			if (barracks == null) { try { ModContent.GetInstance<BarracksTE>().Place(fx - 13, floor - 4); barracks = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(); } catch { } }
			if (barracks != null) barracks.Manpower = 99;
			_frBarracks = barracks;
			PlaceObj(fx + 5, floor - 1, TileID.Campfire, 0, 1); PlaceObj(fx + 20, floor - 1, TileID.Campfire, 0, 1);
			PlaceObj(fx - 7, floor - 1, TileID.Beds, 0, 1); PlaceObj(fx - 2, floor - 1, TileID.Beds, 0, -1); PlaceObj(fx - 18, floor - 1, TileID.Beds, 0, 1);
			PlaceObj(fx + 12, floor - 1, TileID.Kegs, 0, 1);
			PlaceObj(fx + 2, floor - 1, TileID.Chairs, 0, 1); PlaceObj(fx + 9, floor - 1, TileID.Chairs, 0, -1); PlaceObj(fx + 16, floor - 1, TileID.Chairs, 0, 1); PlaceObj(fx + 24, floor - 1, TileID.Chairs, 0, -1);
			PlaceObj(fx + 27, floor - 1, TileID.Benches, 0, 1);
			// the elevator (a 4-wide shaft with a platform every _frShaft rows, down to a cavern 130 rows below the pad), a sheer hole 3 wide and 30 deep, and a 9-deep pit with a stair out
			_frShaftX = _frHoleX = _frPitX = -1;
			if (_frShaft >= 0) FrBuildShaft(fx + 40, floor, 4, 130, _frShaft);
			FrBuildHole(fx - 44, floor, 3, 30);
			FrBuildPit(fx + 62, floor, 9);
			_frBase = new Vector2(barracks != null ? barracks.MusterPoint.X : (fx - 12) * 16f, barracks != null ? barracks.MusterPoint.Y : floor * 16f);
			_frAsciiText = FrAscii(fx, floor);
			{
				int ar = (int)(_frBase.Y / 16f) - 1; var sbp = new StringBuilder("PITCHECK anchorRow=" + ar + ":");
				for (int x = _frPitX - 2; x <= _frPitX + 18; x++)
					for (int y = floor - 3; y < floor + 14; y++)
						if (TileNav.IsStandable(x, y)) { int ov = SoldierNPC.Overburden(x, y); sbp.Append($" [{x - _frPitX}:{y - floor} ov{ov} pit{(SoldierNPC.PitFloor(x, y, ov, ar) ? 1 : 0)}]"); break; }
				_frEvents.AppendLine(sbp.ToString());
			}
			_frEvents.AppendLine($"BASE run={run} site={site} pad=({fx},{floor}) surfaceHere={srf} barracks={(barracks == null ? "NONE" : barracks.Position.ToString())} muster=({(int)(_frBase.X / 16f)},{(int)(_frBase.Y / 16f)}) shaftX={_frShaftX} holeX={_frHoleX} pitX={_frPitX} world={Main.maxTilesX}x{Main.maxTilesY}");
		}

		private BarracksTE _frBarracks;

		/// <summary>An ASCII picture of the base: # solid, = platform, ~ water, t other tile, . air; a column ruler every 10 tiles from the pad centre.</summary>
		private string FrAscii(int cx, int floor)
		{
			var sb = new StringBuilder();
			for (int y = floor - 14; y <= floor + 36; y++)
			{
				sb.Append((y - floor).ToString().PadLeft(4)).Append(' ');
				for (int x = cx - 70; x <= cx + 90; x++)
				{
					if (!TileNav.InBounds(x, y)) { sb.Append(' '); continue; }
					Tile t = Main.tile[x, y]; char ch = '.';
					if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) ch = '#';
					else if (t.HasTile && Main.tileSolidTop[t.TileType]) ch = '=';
					else if (t.LiquidAmount > 0) ch = t.LiquidType == LiquidID.Lava ? 'L' : '~';
					else if (t.HasTile) ch = 't';
					sb.Append(ch);
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}

		private void PlaceObj(int x, int y, int type, int style, int dir)
		{
			try
			{
				bool ok = WorldGen.PlaceObject(x, y, type, true, style, 0, -1, dir);
				if (!ok)
				{
					var td = Terraria.ObjectData.TileObjectData.GetTileData(type, style);
					bool can = TileObject.CanPlace(x, y, type, style, dir, out TileObject to, true);
					_frEvents.AppendLine($"PLACE failed type={type} at {x},{y} dir={dir}: size={td?.Width}x{td?.Height} origin={td?.Origin} canPlace={can} solidBelow={Main.tile[x, y + 1].HasTile && Main.tileSolid[Main.tile[x, y + 1].TileType]} airHere={!Main.tile[x, y].HasTile}");
				}
			}
			catch (Exception e) { _frEvents.AppendLine($"PLACE threw type={type}: {e.Message}"); }
		}

		private void FrBuildShaft(int x, int floor, int w, int depth, int plat)
		{
			_frShaftX = x; int bottom = floor + depth;
			// the cavern: 70 wide, 14 clear rows over a platform floor, stone around
			for (int xx = x - 34; xx <= x + w + 34; xx++)
				for (int y = bottom - 14; y <= bottom + 6; y++)
				{
					bool wall = xx == x - 34 || xx == x + w + 34 || y == bottom - 14 || y >= bottom + 1;
					if (wall) TvSet(xx, y, TileID.Stone); else TvAir(xx, y);
					Main.tile[xx, y].LiquidAmount = 0;
				}
			for (int xx = x - 33; xx <= x + w + 33; xx++) TvSet(xx, bottom, TileID.Stone);
			// the lining and the shaft
			for (int y = floor; y <= bottom - 14; y++)
			{
				foreach (int lx in new[] { x - 1, x + w }) { Tile t = Main.tile[lx, y]; if (!t.HasTile || !Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]) TvSet(lx, y, TileID.GrayBrick); Main.tile[lx, y].LiquidAmount = 0; }
				for (int xx = x; xx < x + w; xx++) TvAir(xx, y);
			}
			if (plat > 0) for (int y = floor + plat; y < bottom - 14; y += plat) for (int xx = x; xx < x + w; xx++) TvPlat(xx, y);
			for (int y = floor - 3; y < floor; y++) for (int xx = x; xx < x + w; xx++) TvAir(xx, y);
		}

		private void FrBuildHole(int x, int floor, int w, int depth)
		{
			_frHoleX = x;
			for (int xx = x - 1; xx <= x + w; xx++) for (int y = floor; y <= floor + depth + 2; y++) TvSet(xx, y, TileID.Stone);
			for (int xx = x; xx < x + w; xx++) for (int y = floor - 3; y < floor + depth; y++) TvAir(xx, y);
		}

		private void FrBuildPit(int x, int floor, int depth)
		{
			_frPitX = x;
			for (int xx = x; xx < x + 16; xx++)
			{
				int d = Math.Max(0, depth - (xx - x - 6) / 1); d = Math.Clamp(d, 0, depth);
				int dd = xx - x < 6 ? depth : Math.Max(0, depth - (xx - x - 5));
				for (int y = floor; y < floor + depth + 2; y++) { if (y < floor + dd) TvAir(xx, y); else if (!(Main.tile[xx, y].HasTile && Main.tileSolid[Main.tile[xx, y].TileType])) TvSet(xx, y, TileID.Dirt); }
			}
		}

		// ---------------- run ----------------
		private static string FrGearOf(int i)
		{
			string mix = Environment.GetEnvironmentVariable("LEVY_TRAVEL_MIX");
			if (!string.IsNullOrEmpty(mix)) { var m = mix.Split(','); return m[i % m.Length]; }
			int k = i % 15; if (k < 5) return "none"; if (k < 9) return "boots"; if (k < 12) return "wings"; return "hook";
		}

		private void FrSpawn()
		{
			GuideSystem.Instance.Autonomy = true;
			for (int i = 0; i < _frN; i++)
			{
				var rec = MakeRecord("Pvt. Fr" + i);
				string g = FrGearOf(i);
				var w = new Item(); w.SetDefaults(ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
				TvKit(rec, g);
				if (g == "boots") { var it = new Item(); it.SetDefaults(ItemID.HermesBoots); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "wings" || g == "wh") { var it = new Item(); it.SetDefaults(ItemID.AngelWings); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "hook" || g == "wh") { var it = new Item(); it.SetDefaults(ItemID.GrapplingHook); rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				if (_frBarracks != null) { rec.HomeBarracksId = _frBarracks.ID; rec.HomePos = _frBarracks.Position; }
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = new Vector2(_frBase.X + (i - _frN / 2) * 24f, _frBase.Y);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				var l = new FrLevy { Rec = rec, Idx = idx, Gear = g };
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; l.Last = b.NPC.Center; }
				_fr.Add(l);
			}
		}

		private void FrPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.velocity = Vector2.Zero;
			float px = _frCmd == "away" ? Math.Clamp(_frBase.X + 900 * 16f, 100 * 16f, (Main.maxTilesX - 100) * 16f) : _frBase.X + 10 * 16f;
			p.position = new Vector2(px - p.width / 2f, _frBase.Y - p.height);
			if (!_frFoes) { for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; } }
			else for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && n.friendly && !n.townNPC) n.active = false; }
		}

		private void FrSpawnFoe()
		{
			int hostile = 0; for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.damage > 0) hostile++; }
			if (hostile >= 7) return;
			int[] kinds = Main.dayTime ? new int[] { NPCID.GreenSlime, NPCID.BlueSlime, NPCID.DemonEye, NPCID.GreenSlime } : new int[] { NPCID.Zombie, NPCID.DemonEye, NPCID.Zombie, NPCID.Raven };
			int side = Main.rand.NextBool() ? -1 : 1, tx = _frBx + side * Main.rand.Next(45, 110); tx = Math.Clamp(tx, 20, Main.maxTilesX - 20);
			int ty = FrSurfaceRow(tx, _frFy - 60) - 3; if (ty < 0) return;
			NPC.NewNPC(new EntitySource_WorldEvent("LevyFr"), tx * 16 + 8, ty * 16, kinds[Main.rand.Next(kinds.Length)]);
			if (_frShaftX > 0 && _frTick % 1800 == 0)   // a skeleton in the cavern at the foot of the elevator (the scout pull reaches through rock)
				NPC.NewNPC(new EntitySource_WorldEvent("LevyFrCave"), (_frShaftX + 12) * 16, (_frFy + 130 - 4) * 16, NPCID.Skeleton);
		}

		private void FrSetClock()
		{
			double t = _frTime switch { "dawn" => 600, "dusk" => 50000, "midnight" => 16200, _ => 27000 };
			bool day = _frTime != "midnight";
			// time runs (the day is 54000 ticks, the night 32400); the run starts at the chosen clock
			double tt = t + _frTick; bool dd = day;
			if (dd && tt >= 54000) { tt -= 54000; dd = false; }
			if (!dd && tt >= 32400) { tt -= 32400; dd = true; }
			Main.dayTime = dd; Main.time = tt; Main.raining = false;
		}

		private void StepFreeRein()
		{
			if (!_frStarted)
			{
				ClearAll(); RoutePlanner.LabReset(); _fr.Clear(); _frEvents.Clear(); _frTrace.Clear();
				FrBuildBase(_frRun0 + _frRun);
				FrSpawn();
				_frStarted = true; _frTick = 0; _frWallMs = 0; _frWallTicks = 0; SoldierNPC.LeisureStatReset(); _frWatch.Restart();
				Main.player[0].Center = _frBase; FrPinCommander();
				Mod.Logger.Info($"HARNESS-FR: run {_frRun} base=({_frBx},{_frFy}) levies={_fr.Count} legacy={SoldierNPC.FreeReinLegacy} life={SoldierNPC.LeisureEnabledForLab}");
				return;
			}
			_frTick++;
			FrSetClock(); FrPinCommander();
			OffscreenHop.LabViewers = _frCmd == "none" ? new List<Vector2>() : new List<Vector2> { Main.player[0].Center };
			if (_frTick == 100 && _frHoleX > 0)
			{
				// the route check across the hole: east rim to west rim, for a walker and a winger
				foreach (int gi in new[] { 0, 5, 9, 12 })
				{
					var lv = _fr[Math.Min(gi, _fr.Count - 1)]; if (!lv.Rec.HasBody) continue;
					var sn = (SoldierNPC)Main.npc[lv.Rec.BodyIndex].ModNPC; NPC bn = Main.npc[lv.Rec.BodyIndex];
					Vector2 save = bn.Bottom;
					int row = FrSurfaceRow(_frHoleX + 8, _frFy - 10);
					bn.Bottom = new Vector2((_frHoleX + 8) * 16f + 8f, row * 16f);
					Vector2 west = new Vector2((_frHoleX - 2) * 16f + 8f, row * 16f);
					_frEvents.AppendLine($"ROUTEPROBE gear={lv.Gear} " + sn.LabRouteProbe(west));
					bn.Bottom = save;
				}
			}
			if (_frTick == 120)
			{
				RosterSystem.Instance.IssueOrder(Main.player[0], SoldierOrder.FreeRein, true);
				int took = _fr.Count(l => l.Rec.Order == SoldierOrder.FreeRein);
				_frEvents.AppendLine($"t=120 Free Rein issued: {took}/{_fr.Count} took it");
			}
			if (_frFoes && _frTick > 300 && _frTick % 420 == 0) FrSpawnFoe();
			if (_frTick > 125) foreach (var l in _fr) { if (!l.Rec.HasBody) { l.Last = default; continue; } NPC bn = Main.npc[l.Rec.BodyIndex]; if (l.Last != default && Vector2.Distance(bn.Center, l.Last) > 16f * 6f) l.Teleports++; l.Last = bn.Center; }
			long now = System.Diagnostics.Stopwatch.GetTimestamp();
			if (_frTick > 120 && _frTick % 20 == 0) FrSample();
			if (_frTick >= _frTicks) FrEndRun();
		}

		private void FrSample()
		{
			foreach (var l in _fr)
			{
				if (!l.Rec.HasBody) { l.BodyLost++; continue; }
				NPC n = Main.npc[l.Rec.BodyIndex]; if (!n.active || !(n.ModNPC is SoldierNPC sn)) { l.BodyLost++; continue; }
				float dx = Math.Abs(n.Center.X - _frBase.X) / 16f; float depth = n.Bottom.Y / 16f - _frFy;
				l.Samples++; l.DistSum += dx; if (dx > l.MaxDx) l.MaxDx = dx; if (depth > l.MaxDepth) l.MaxDepth = depth; l.FinalDx = dx; l.FinalDepth = depth;
				bool out150 = dx > 150f || depth > 14f, pit = depth >= 8f, deep = depth >= 20f, ug = depth >= 40f;
				if (dx > 205f || depth > 18f) l.Out200Ticks += 20;
				if (out150) { l.OutTicks += 20; if (l.FirstOut < 0) { l.FirstOut = _frTick; l.FirstOutWhy = sn.FreeReinProbe(); } }
				if (pit) { l.PitTicks += 20; if (!l.InPit) { l.Pits++; if (l.FirstPit < 0) { l.FirstPit = _frTick; l.FirstPitWhy = sn.FreeReinProbe(); } } l.InPit = true; } else if (depth < 4f) l.InPit = false;
				int tx = (int)(n.Center.X / 16f);
				bool inHole = _frHoleX > 0 && tx >= _frHoleX - 1 && tx <= _frHoleX + 3 && depth >= 6f, inShaft = _frShaftX > 0 && tx >= _frShaftX - 1 && tx <= _frShaftX + 4 && depth >= 6f, inPit = _frPitX > 0 && tx >= _frPitX && tx <= _frPitX + 15 && depth >= 6f;
				if (_frHoleX > 0 && Math.Abs(tx - _frHoleX) <= 12 && depth < 6f) { l.Ring.Add($"t={_frTick / 60.0:0.0}s dx={tx - _frHoleX} y={depth:0} [{sn.FreeReinProbe()}] pastime={sn.LeisureKindForLab}"); if (l.Ring.Count > 7) l.Ring.RemoveAt(0); }
				if (inHole) { l.InHoleTicks += 20; if (!l.WasInHole) { l.HoleFalls++; _frEvents.AppendLine($"HOLEFALL {l.Rec.Name}({l.Gear}) at t={_frTick / 60.0:0.0}s; last samples before:\n    " + string.Join("\n    ", l.Ring)); } } l.WasInHole = inHole;
				if (inShaft) { l.InShaftTicks += 20; if (!l.WasInShaft) l.ShaftFalls++; } l.WasInShaft = inShaft;
				if (inPit) { l.InPitTicks += 20; if (!l.WasInPit) l.PitFalls++; } l.WasInPit = inPit;
				if (deep) { l.DeepTicks += 20; if (l.FirstDeep < 0) { l.FirstDeep = _frTick; l.FirstDeepWhy = sn.FreeReinProbe(); } }
				if (ug) l.UgTicks += 20;
				int act = sn.LeisureKindForLab; if (act >= 0 && act < l.Act.Length) l.Act[act]++;
				if (sn.Seated) l.Seated++;
			}
			if (_frTick % 1200 == 0)
			{
				var sb = new StringBuilder($"t={_frTick / 60}s");
				foreach (var l in _fr.Take(15)) { if (!l.Rec.HasBody) { sb.Append(" x"); continue; } NPC n = Main.npc[l.Rec.BodyIndex]; sb.Append($" {(int)((n.Center.X - _frBase.X) / 16f)}/{(int)(n.Bottom.Y / 16f - _frFy)}"); }
				_frTrace.AppendLine(sb.ToString());
			}
		}

		private void FrEndRun()
		{
			var csv = new StringBuilder("run,name,gear,samples,bodyLost,maxDx,meanDx,finalDx,maxDepth,finalDepth,outTicks,pitTicks,deepTicks,ugTicks,pits,firstOut,firstPit,firstDeep,teleports,seated,out200Ticks,holeFalls,shaftFalls,pitFalls," + string.Join(",", FrActNames.Take(9).Select(a => "act_" + a)) + "\n");
			foreach (var l in _fr)
				csv.AppendLine($"{_frRun},{l.Rec.Name},{l.Gear},{l.Samples},{l.BodyLost},{l.MaxDx:0},{(l.Samples > 0 ? l.DistSum / l.Samples : 0):0},{l.FinalDx:0},{l.MaxDepth:0},{l.FinalDepth:0},{l.OutTicks},{l.PitTicks},{l.DeepTicks},{l.UgTicks},{l.Pits},{l.FirstOut},{l.FirstPit},{l.FirstDeep},{l.Teleports},{l.Seated},{l.Out200Ticks},{l.HoleFalls},{l.ShaftFalls},{l.PitFalls},{string.Join(",", l.Act.Take(9))}");
			string dir = Path.Combine(_frOut, "r" + (_frRun0 + _frRun)); Directory.CreateDirectory(dir);
			File.WriteAllText(Path.Combine(dir, "levies.csv"), csv.ToString());
			foreach (var l in _fr)
			{
				if (l.FirstOut >= 0) _frEvents.AppendLine($"OUT   {l.Rec.Name}({l.Gear}) t={l.FirstOut / 60}s {l.FirstOutWhy}");
				if (l.FirstPit >= 0) _frEvents.AppendLine($"PIT   {l.Rec.Name}({l.Gear}) t={l.FirstPit / 60}s {l.FirstPitWhy}");
				if (l.FirstDeep >= 0) _frEvents.AppendLine($"DEEP  {l.Rec.Name}({l.Gear}) t={l.FirstDeep / 60}s {l.FirstDeepWhy}");
			}
			_frEvents.AppendLine("LEISURE " + SoldierNPC.LeisureStatLine());
			_frEvents.AppendLine("PATH " + Pathfinder.StatSummary());
			_frEvents.AppendLine($"WALL ticks={_frTick} ms_per_tick={_frWatch.Elapsed.TotalMilliseconds / Math.Max(1, _frTick):0.000} (whole server tick, all runs so far)");
			File.WriteAllText(Path.Combine(dir, "events.txt"), _frEvents.ToString());
			File.WriteAllText(Path.Combine(dir, "trace.txt"), _frTrace.ToString());
			File.WriteAllText(Path.Combine(dir, "base.txt"), $"column 0 = the pad centre x={_frBx}; rows are relative to the base floor (row 0 = the top solid row)\n" + _frAsciiText);
			Mod.Logger.Info($"HARNESS-FR: run {_frRun} done; out150={_fr.Count(l => l.OutTicks > 0)}/{_fr.Count} pit={_fr.Count(l => l.Pits > 0)} deep={_fr.Count(l => l.DeepTicks > 0)} ug40={_fr.Count(l => l.UgTicks > 0)}");
			_frRun++; _frStarted = false;
			if (_frRun >= _frRuns) Finish($"freerein complete runs={_frRuns}");
		}
	}
}
