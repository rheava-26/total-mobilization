using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Movement;
using Levy.Common.Flavor;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TRAVEL LAB (LEVY_HARNESS_SCENARIO=travel). Measurement only. Carves a long surface course (hills, a ledge, a chasm with a stair, a forest, a pond, a house with doors, a mountain with a tunnel,
	/// a lake, a second chasm, rough hills) and sends N levies of mixed gear (none / boots / wings / hook) from one offset to another, either by a map-point order or by Follow behind a commander who
	/// walks the same route. The commander of a map order stays at the start (the player is far from the army, as in the report).
	/// Env: LEVY_TRAVEL_MODE=map|follow  LEVY_TRAVEL_FROM / LEVY_TRAVEL_TO (course offsets, tiles)  LEVY_TRAVEL_N (15)  LEVY_TRAVEL_CAP (ticks, 12000)  LEVY_TRAVEL_GEAR (mixed|none|boots|wings|hook)
	/// LEVY_TRAVEL_OUT (directory; levies.csv, trace.csv, events.txt are written there).
	/// </summary>
	public partial class CombatHarness
	{
		private static int TvG = 150;                // ground row at elevation 0 (top solid row); the wall course raises it so a 150-tile face fits
		private static int TvX0;                     // world tile x of course offset 0
		private static readonly int[] TvElev = new int[1100];   // elevation (tiles above TvG) of the top solid row at each course offset
		private static readonly bool[] TvWater = new bool[1100];
		private const int TvLen = 960;
		private static int TvDepthA = 12; private static bool TvBridgeB = true, TvZoo, TvWorld, TvCorrupt, TvWall, TvLavaC, TvRavine, TvLips; private static int TvWallH = 20;
		private static readonly int[] TvLavaLvl = new int[1100];   // lava course: elevation of the lava surface (int.MinValue = none)
		private static int[] TvSurf;                 // world mode: feet row of the standing surface for every world column (-1 = none)
		private string _tvMode = "map", _tvOut = "", _tvGear = "mixed";
		private int _tvFrom, _tvTo, _tvN = 15, _tvCap = 12000, _tvTick; private bool _tvStarted;
		private sealed class TvLevy
		{
			public string Gear; public SoldierRecord Rec; public int Idx = -1;
			public Vector2 Start, Last; public float Straight, Path; public int ArriveTick = -1, GaveUp = -1, Teleports, Falls, WetTicks, DeepTicks, Gone, StuckTicks, StuckEps, StallTicks, StallEps, Frames;
			public int LavaTicks, LavaEntries, LavaDeaths; public bool InLava; public int LavaLastTick; public int HkCasts, HkLatches, HkChains, HkAborts, HkReleases, HkLandings, RtCasts, RtBans, StepBacks; public float EndElev; public float FallTiles, BestProg; public int BestProgTick; public int VisTele, Hops, ClimbDone;
			public Vector2 Anchor; public int AnchorTick; public bool InStuck; public int FlyTicks, AirTicks, TravTicks; public float MaxDepth, DepthSum, RefRow = float.NaN;
			public string StuckWhere = "";
			public bool StallOpen; public int StallFrom;
			public float FallStartY = float.NaN; public bool Falling; public string StallWhere = ""; public int LastHasBody = 1;
		}
		private int _tvDbgFrom = -1, _tvDbgTo = -1, _tvDbgStep = 10, _tvDbgWho = -1;   // LEVY_TRAVEL_DBG=from,to[,step[,levyIndex]]: a DBG line per sampled tick in events.txt
		private bool _tvGun, _tvFoes; private int _tvAscii; private readonly Dictionary<string, int> _tvKnockTick = new Dictionary<string, int>();
		private readonly List<TvLevy> _tv = new List<TvLevy>();
		private StringBuilder _tvTrace = new StringBuilder(), _tvEvents = new StringBuilder();
		private int _tvGx, _tvGy; private float _tvCmdX;
		private System.Diagnostics.Stopwatch _tvWatch = new System.Diagnostics.Stopwatch();

		private static string TvFeature(float off)
		{
			if (TvWorld) return "w" + ((int)(off + TvX0) / 50 * 50);
			if (TvRavine || TvLips) { foreach (var f in TvFeats) if (off >= f.A - 3 && off <= f.B + 3) return f.Name; return "flat"; }
			if (TvLavaC) { int lg = (int)Math.Floor((off - 20) / 150f); string[] ln = { "lavalake", "lavagap8", "lavapit", "lavapool3", "lavashaft", "lavaledge" }; return lg >= 0 && lg < ln.Length ? "v" + lg + "-" + ln[lg] : "camp"; }
			if (TvWall) return off < 100 ? "foot" : off < 300 ? (TvWallH > 0 ? "plateau" : "far") : "end";
			if (TvCorrupt) { int leg2 = (int)Math.Floor((off - 20) / 150f); string[] nm2 = { "holes3-5-8", "cavemouth", "chasm60", "hole4", "pitstairs", "lakesteep" }; return leg2 >= 0 && leg2 < nm2.Length ? "c" + leg2 + "-" + nm2[leg2] : "camp"; }
			if (TvZoo) { int leg = (int)Math.Floor((off - 20) / 150f); string[] nm = { "cliff10", "crack6", "chasm14", "chasm20", "lake12", "cliff20" }; return leg >= 0 && leg < nm.Length ? "z" + leg + "-" + nm[leg] : "camp"; }
			if (off < 40) return "camp0"; if (off < 100) return "hills1"; if (off < 140) return "ledge"; if (off < 165) return "camp1";
			if (off < 200) return "pre-chasmA"; if (off < 216) return "chasmA"; if (off < 256) return "stairsA"; if (off < 315) return "forest"; if (off < 335) return "camp2";
			if (off < 372) return "pond"; if (off < 420) return "house"; if (off < 480) return "flat3"; if (off < 500) return "camp3";
			if (off < 530) return "mountain-up"; if (off < 580) return "tunnel/peak"; if (off < 620) return "mountain-down"; if (off < 645) return "camp4";
			if (off < 668) return "lake-in"; if (off < 702) return "lake"; if (off < 712) return "lake-out"; if (off < 735) return "chasmB"; if (off < 768) return "stairsB"; if (off < 790) return "camp5";
			if (off < 910) return "hills2"; return "camp6";
		}

		private bool BeginTravel()
		{
			_tvMode = (Environment.GetEnvironmentVariable("LEVY_TRAVEL_MODE") ?? "map").ToLowerInvariant();
			int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_FROM"), out _tvFrom);
			_tvTo = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_TO"), out int to) ? to : 920;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_N"), out int n) && n > 0) _tvN = n;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_CAP"), out int cap) && cap > 600) _tvCap = cap;
			_tvGear = (Environment.GetEnvironmentVariable("LEVY_TRAVEL_GEAR") ?? "mixed").ToLowerInvariant();
			{ _tvDbgFrom = -1; string dbg = Environment.GetEnvironmentVariable("LEVY_TRAVEL_DBG"); if (!string.IsNullOrEmpty(dbg)) { var q = dbg.Split(','); int.TryParse(q[0], out _tvDbgFrom); _tvDbgTo = q.Length > 1 && int.TryParse(q[1], out int t1) ? t1 : _tvDbgFrom + 300; _tvDbgStep = q.Length > 2 && int.TryParse(q[2], out int t2) && t2 > 0 ? t2 : 10; _tvDbgWho = q.Length > 3 && int.TryParse(q[3], out int t3) ? t3 : -1; } }
			_tvOut = Environment.GetEnvironmentVariable("LEVY_TRAVEL_OUT") ?? Path.Combine(Path.GetTempPath(), "levy_travel");
			Directory.CreateDirectory(_tvOut);
			_tvGun = Environment.GetEnvironmentVariable("LEVY_TRAVEL_GUN") == "1" || Environment.GetEnvironmentVariable("LEVY_TRAVEL_FOES") == "1"; _tvFoes = Environment.GetEnvironmentVariable("LEVY_TRAVEL_FOES") == "1";
			string course = Environment.GetEnvironmentVariable("LEVY_TRAVEL_COURSE");
			TravelLegacy.On = Environment.GetEnvironmentVariable("LEVY_TRAVEL_LEGACY") == "1";
			TvZoo = string.Equals(course, "zoo", StringComparison.OrdinalIgnoreCase);
			TvWorld = string.Equals(course, "world", StringComparison.OrdinalIgnoreCase);
			TvCorrupt = string.Equals(course, "corrupt", StringComparison.OrdinalIgnoreCase);
			TvWall = string.Equals(course, "wall", StringComparison.OrdinalIgnoreCase);
			TvLavaC = string.Equals(course, "lava", StringComparison.OrdinalIgnoreCase);
			TvRavine = string.Equals(course, "ravine", StringComparison.OrdinalIgnoreCase); TvLips = string.Equals(course, "lips", StringComparison.OrdinalIgnoreCase);
			TvG = 150; if (TvWall) { TvG = 300; int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_WALLH"), out TvWallH); if (TvWallH == 0) TvWallH = 20; }
			SoldierHooks.LogCasts = Environment.GetEnvironmentVariable("LEVY_TRAVEL_HOOKLOG") != "0"; SoldierHooks.CastLog.Clear();
			OffscreenHop.LabNoHop = Environment.GetEnvironmentVariable("LEVY_TRAVEL_NOHOP") == "1";
			TvDepthA = 12; TvBridgeB = true;
			_tvTick = 0; _tvStarted = false; _tv.Clear(); _tvTrace.Clear(); _tvEvents.Clear();
			TvSpecInit(); if (_tvSpec) TvSpecOutDir();
			return true;
		}

		// ---------------- course ----------------
		/// <summary>The obstacle zoo: camps (flat, 20 wide) every 150 tiles from 20, and exactly one obstacle between each pair, most of them impassable on foot.</summary>
		private static void TvDefineZoo()
		{
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length);
			// Z0 (20..170): a 10-high sheer cliff face at 90, a mesa 40 wide, then a sheer drop back down
			for (int i = 90; i < 130; i++) TvElev[i] = 10;
			// Z1 (170..320): a narrow crack, 6 wide, 30 deep
			for (int i = 230; i < 236; i++) TvElev[i] = -30;
			// Z2 (320..470): 14 wide, 40 deep
			for (int i = 380; i < 394; i++) TvElev[i] = -40;
			// Z3 (470..620): 20 wide, 30 deep
			for (int i = 530; i < 550; i++) TvElev[i] = -30;
			// Z4 (620..770): a deep lake, 12 deep, 1:1 shores, filled with water
			for (int i = 660; i < 724; i++) { int d = Math.Min(12, Math.Min(i - 659, 724 - i)); TvElev[i] = -d; TvWater[i] = true; }
			// Z5 (770..920): a 20-high cliff, a mesa, a drop
			for (int i = 840; i < 880; i++) TvElev[i] = 20;
		}

		/// <summary>The corruption route: sheer surface holes (4 wide, 3 / 5 / 8 deep), a cave mouth (dip under a rock lid), then a corruption chasm (8 wide, 60 deep, ledged walls) across the way. Flat camps every 150 from 20.</summary>
		private static void TvDefineCorrupt()
		{
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length);
			int[] hx = { 60, 90, 120 }, hd = { 3, 5, 8 };
			for (int k = 0; k < 3; k++) for (int i = hx[k]; i < hx[k] + 4; i++) TvElev[i] = -hd[k];
			for (int i = 200; i < 212; i++) TvElev[i] = -(i - 199);            // cave mouth: 12 down ...
			for (int i = 212; i < 232; i++) TvElev[i] = -12;                   // ... a flat floor under the lid ...
			for (int i = 232; i < 244; i++) TvElev[i] = -(244 - i);            // ... 12 up
			for (int i = 400; i < 408; i++) TvElev[i] = -60;                   // the chasm (ledges added in TvBuild)
			for (int i = 560; i < 566; i++) TvElev[i] = -4;                    // a shallow hole on the far side
			// leg 4 (620..770): a 6-wide pit 14 deep with a sheer near wall and 3-high, 2-wide steps up the far wall (down and up again is the only way across on foot)
			for (int i = 690; i < 696; i++) TvElev[i] = -14;
			for (int i = 696; i < 716; i++) TvElev[i] = Math.Min(0, -14 + ((i - 696) / 2 + 1) * 3);
		}

		/// <summary>The wall course: flat ground to 100, then (WALLH &gt; 0) a sheer face WALLH tall with a plateau on top to 300 and a ramp down, or (WALLH &lt; 0) a chasm |WALLH| wide and 60 deep with ground on the far side. Start 40, goal 250 (the harness sets From/To).</summary>
		private static void TvDefineWall()
		{
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length);
			if (TvWallH > 0) { for (int i = 100; i < 320; i++) TvElev[i] = TvWallH; for (int i = 320; i < 320 + TvWallH && i < TvElev.Length; i++) TvElev[i] = TvWallH - (i - 319); }
			else for (int i = 100; i < 100 - TvWallH; i++) TvElev[i] = -60;
		}

		/// <summary>The lava course: camps every 150 from 20 and one lava hazard per leg. V0 a lava lake 40 wide (shores 1:1, 14 deep, full), V1 a sheer gap 8 wide and 25 deep with 6 rows of lava at the bottom, V2 a 12-wide 20-deep pit with 4 rows of lava (sheer), V3 a 3-wide 3-deep lava pool in the road, V4 a 10-wide 40-deep shaft with 8 rows of lava (levies are dropped into it in the air), V5 an island ledge over a 30-wide lava sea.</summary>
		private static void TvDefineLava()
		{
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length); Array.Fill(TvLavaLvl, int.MinValue);
			if (Environment.GetEnvironmentVariable("LEVY_LAVA_CLIFF") == "1") for (int i = 80; i < 114; i++) { TvElev[i] = -36; TvLavaLvl[i] = -16; }   // a sheer-walled basin 34 wide, 20 rows of lava under a 16-tile drop
			else for (int i = 80; i < 120; i++) { int d = Math.Min(14, Math.Min(i - 79, 120 - i)); TvElev[i] = -d; TvLavaLvl[i] = 0; }
			for (int i = 230; i < 238; i++) { TvElev[i] = -25; TvLavaLvl[i] = -19; }
			for (int i = 380; i < 392; i++) { TvElev[i] = -20; TvLavaLvl[i] = -16; }
			for (int i = 530; i < 533; i++) { TvElev[i] = -3; TvLavaLvl[i] = 0; }
			for (int i = 680; i < 690; i++) { TvElev[i] = -40; TvLavaLvl[i] = -32; }
			for (int i = 830; i < 860; i++) { TvElev[i] = -30; TvLavaLvl[i] = -10; }
			for (int i = 843; i < 847; i++) TvElev[i] = -8;   // an island 4 wide, 8 rows below the road
		}

		private static void TvDefineCourse()
		{
			if (TvRavine || TvLips) { TvDefineRavine(); return; }
			if (TvLavaC) { TvDefineLava(); return; }
			if (TvWall) { TvDefineWall(); return; }
			if (TvCorrupt) { TvDefineCorrupt(); return; }
			if (TvZoo) { TvDefineZoo(); return; }
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length);
			// hills1 40..100: smooth, up to 9 (1-tile steps only)
			for (int i = 40; i < 100; i++) TvElev[i] = Math.Max(0, (int)Math.Round(6 * Math.Sin((i - 40) / 60.0 * Math.PI * 2))) + (i > 60 && i < 85 ? 3 : 0);
			// ledge 110..135: a 3-tile step up at 110 and down at 135
			for (int i = 110; i < 135; i++) TvElev[i] = 3;
			// chasmA 200..214: floor TvDepthA below (12 = walkable cave-mouth dip, 40 = "gap" course); stairs up from 214 (3 high x 3 wide)
			for (int i = 200; i < 214; i++) TvElev[i] = -TvDepthA;
			for (int i = 214; i < 256; i++) TvElev[i] = Math.Min(0, -TvDepthA + ((i - 214) / 3 + 1) * 3);
			// forest 256..315: gentle bumps
			for (int i = 256; i < 315; i++) TvElev[i] = (int)Math.Round(2 * Math.Sin((i - 256) / 12.0));
			// pond 340..368: bowl depth 3 with 1:1 sides, filled with water
			for (int i = 340; i < 368; i++) { int d = Math.Min(3, Math.Min(i - 339, 368 - i)); TvElev[i] = -d; TvWater[i] = true; }
			// mountain 500..620: slopes 1:1 up to 22, flat peak, with a tunnel at ground level built by TvBuild
			for (int i = 500; i < 530; i++) TvElev[i] = Math.Min(22, i - 499);
			for (int i = 530; i < 590; i++) TvElev[i] = 22;
			for (int i = 590; i < 620; i++) TvElev[i] = Math.Max(0, 22 - (i - 589));
			// lake 650..712: slope in 7, flat at -7, slope out 7; water fills to elevation 0
			for (int i = 650; i < 712; i++) { int d = Math.Min(7, Math.Min(i - 649, 712 - i)); TvElev[i] = -d; TvWater[i] = true; }
			// chasmB 714..734: floor 30 below, then stairs 734..770 (3 high x 3 wide)
			for (int i = 714; i < 734; i++) { TvElev[i] = -30; TvWater[i] = false; }
			if (!TvBridgeB) for (int i = 734; i < 770; i++) TvElev[i] = Math.Min(0, -30 + ((i - 734) / 3 + 1) * 3);
			// hills2 790..910: rough, amplitude up to ~13, 1-tile steps only
			int e = 0;
			for (int i = 790; i < 910; i++)
			{
				int target = (int)Math.Round(10 * Math.Sin((i - 790) / 18.0) + 3 * Math.Sin((i - 790) / 5.3));
				if (target > e) e++; else if (target < e) e--;
				TvElev[i] = Math.Max(0, e);
			}
			for (int i = 905; i < 912; i++) TvElev[i] = Math.Max(0, TvElev[i - 1] - 1);
		}


		// ---- seeded ravine / lip courses (LEVY_TRAVEL_COURSE=ravine|lips, LEVY_TRAVEL_SEED, LEVY_TRAVEL_RAVMODE=walk|mixed) ----
		private struct TvFeat { public int A, B; public string Name; public int Rim; }   // Rim: 0 square, 1 slope, 2 half block
		private static readonly List<TvFeat> TvFeats = new List<TvFeat>();
		private static readonly int[] TvRimL = new int[1100], TvRimR = new int[1100];      // per column: rim type of a left/right edge (0 none, 1 slope, 2 half)
		private static string TvRimTag(int rim) => rim == 1 ? "s" : rim == 2 ? "h" : "q";
		/// <summary>Rolling ground with a feature every 20 to 50 tiles. ravine: pits and V-valleys 8 to 40 wide and 12 to 45 deep (walkable ones have stairs both ways), hills, mesas, cracks. lips: small steps, rises and pits (3 to 12 deep) whose rim tiles are sloped or half blocks. Camps (flat) every 150 from 20.</summary>
		private static void TvDefineRavine()
		{
			Array.Clear(TvElev, 0, TvElev.Length); Array.Clear(TvWater, 0, TvWater.Length); Array.Clear(TvRimL, 0, TvRimL.Length); Array.Clear(TvRimR, 0, TvRimR.Length); TvFeats.Clear();
			int seed = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_SEED"), out int sd) ? sd : 1;
			bool walk = (Environment.GetEnvironmentVariable("LEVY_TRAVEL_RAVMODE") ?? "mixed") == "walk";
			var r = new Random(seed * 7919 + (TvLips ? 17 : 3));
			int x = 40;
			while (x < 860)
			{
				int k = (x - 20) / 150; int campEnd = 20 + 150 * k + 20; int nextCamp = Math.Min(880, 20 + 150 * (k + 1));
				if (x < campEnd) { x = campEnd; continue; }
				x += 18 + r.Next(30);
				int rim = TvLips ? (r.Next(3) == 0 ? 2 : 1) : (r.Next(2) == 0 ? 1 : (r.Next(3) == 0 ? 2 : 0));
				int type = r.Next(5); if (walk && type >= 3) type = r.Next(3);   // walk mode: only valleys with stairs and hills, the sheer mesas and cracks are for the winged
				if (TvLips)
				{
					int h = 2 + r.Next(4), w = 4 + r.Next(14), d = 3 + r.Next(8);
					if (type == 0) { if (x + w + 6 >= nextCamp) continue; for (int i = x; i < x + w; i++) TvElev[i] = h; TvRimL[x] = rim; TvRimR[x + w - 1] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "rise" + h + "x" + w + TvRimTag(rim), Rim = rim }); x += w; }
					else if (type == 1) { w = 2 + r.Next(2); d = 6 + r.Next(7); if (x + w + 6 >= nextCamp) continue; for (int i = x; i < x + w; i++) TvElev[i] = -d; TvRimL[x - 1] = rim; TvRimR[x + w] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "pit" + d + "x" + w + TvRimTag(rim), Rim = rim }); x += w; }
					else if (type == 4) { w = 5 + r.Next(8); d = 2 + r.Next(4); if (x + w + 6 >= nextCamp) continue; for (int i = x; i < x + w; i++) TvElev[i] = -d; TvRimL[x - 1] = rim; TvRimR[x + w] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "dip" + d + "x" + w + TvRimTag(rim), Rim = rim }); x += w; }
					else if (type == 2) { h = 2 + r.Next(4); if (x + w + 6 >= nextCamp) continue; for (int i = x; i < x + w; i++) TvElev[i] = h; TvRimL[x] = rim; TvRimR[x + w - 1] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "mesa" + h + "x" + w + TvRimTag(rim), Rim = rim }); x += w; }
					else { w = 8 + r.Next(8); if (x + w + 6 >= nextCamp) continue; for (int i = x; i < x + w; i++) TvElev[i] = Math.Max(1, Math.Min(h, Math.Min(i - x + 1, x + w - i))); TvRimL[x] = rim; TvRimR[x + w - 1] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "hump" + h + "x" + w, Rim = rim }); x += w; }
					continue;
				}
				if (type <= 1)
				{
					int w = 8 + r.Next(33), d = 12 + r.Next(34); if (x + w + 6 >= nextCamp) { w = nextCamp - x - 8; if (w < 6) continue; }
					bool stairs = walk || r.Next(5) < 3; int sw = 2 + r.Next(2), sh = 3;
					for (int i = x; i < x + w; i++) { int e = stairs ? -Math.Min(d, (Math.Min(i - x, x + w - 1 - i) / sw + 1) * sh) : -d; TvElev[i] = e; }
					TvRimL[x - 1] = rim; TvRimR[x + w] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = (stairs ? "vale" : "pit") + d + "x" + w + TvRimTag(rim), Rim = rim }); x += w;
				}
				else if (type == 2)
				{
					int w = 8 + r.Next(30), h = 8 + r.Next(23); if (x + w + 2 * h + 6 >= nextCamp) continue;
					for (int i = 0; i < w + 2 * h; i++) TvElev[x + i] = i < h ? i + 1 : i >= w + h ? Math.Max(1, h - (i - (w + h))) : h;
					TvRimL[x + h] = rim; TvRimR[x + w + h - 1] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w + 2 * h, Name = "hill" + h + "x" + w, Rim = rim }); x += w + 2 * h;
				}
				else if (type == 3)
				{
					int w = 6 + r.Next(14), h = 5 + r.Next(10); if (x + w + 6 >= nextCamp) continue;
					for (int i = x; i < x + w; i++) TvElev[i] = h; TvRimL[x] = rim; TvRimR[x + w - 1] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "mesa" + h + "x" + w + TvRimTag(rim), Rim = rim }); x += w;
				}
				else
				{
					int w = 4 + r.Next(6), d = 20 + r.Next(25); if (x + w + 6 >= nextCamp) continue;
					for (int i = x; i < x + w; i++) TvElev[i] = -d; TvRimL[x - 1] = rim; TvRimR[x + w] = rim; TvFeats.Add(new TvFeat { A = x, B = x + w, Name = "crack" + d + "x" + w + TvRimTag(rim), Rim = rim }); x += w;
				}
			}
		}

		/// <summary>After the columns exist: the rim tile of every edge that changes the ground by 2 or more becomes a slope or a half block, as chosen by the course (TvRimL / TvRimR mark the column at the lip).</summary>
		private static void TvApplyRims()
		{
			for (int off = 1; off < TvLen - 1; off++)
			{
				int x = TvX0 + off, top = TvG - TvElev[off];
				for (int side = -1; side <= 1; side += 2)
				{
					if (TvElev[off] - TvElev[off + side] < 2) continue;                       // not an edge that drops away to this side
					int kind = Math.Max(TvRimL[off], TvRimR[off]);   // the lip column itself was marked by the feature
					if (kind == 0) continue;
					Tile t = Main.tile[x, top];
					if (kind == 2) t.IsHalfBlock = true;
					else t.Slope = side < 0 ? SlopeType.SlopeDownLeft : SlopeType.SlopeDownRight;
				}
			}
		}

		private static void TvSet(int x, int y, ushort type)
		{
			Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = type; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid;
		}
		private static void TvAir(int x, int y) { Tile t = Main.tile[x, y]; t.HasTile = false; t.TileType = 0; t.TileFrameX = 0; t.TileFrameY = 0; t.LiquidAmount = 0; t.WallType = 0; }

		/// <summary>World mode: the real terrain. Surface row of each column (first place from the sky down with a floor and room for a body, below the floating islands).</summary>
		private void TvScanSurface()
		{
			TvSurf = new int[Main.maxTilesX];
			int y0 = (int)(Main.worldSurface * 0.60);
			for (int x = 0; x < Main.maxTilesX; x++)
			{
				TvSurf[x] = -1;
				if (x < 8 || x > Main.maxTilesX - 8) continue;
				for (int y = y0; y < (int)Main.worldSurface + 40; y++)
					if (TileNav.IsFloor(x, y + 1) && TileNav.IsBodyClear(x, y) && TileNav.IsSolid(x, y + 1)) { TvSurf[x] = y + 1; break; }
			}
		}

		private static string TvAscii(int cx, int cy, int dir)
		{
			var sb = new StringBuilder();
			for (int y = cy - 14; y <= cy + 10; y++)
			{
				for (int x = cx - 24; x <= cx + 40; x++)
				{
					if (!TileNav.InBounds(x, y)) { sb.Append(' '); continue; }
					Tile t = Main.tile[x, y];
					char ch = '.';
					if (x == cx && y == cy) ch = '@';
					else if (TileNav.IsClosedDoor(t)) ch = 'D';
					else if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) ch = t.Slope != SlopeType.Solid || t.IsHalfBlock ? '/' : '#';
					else if (t.HasTile && Main.tileSolidTop[t.TileType]) ch = '=';
					else if (t.LiquidAmount > 0) ch = t.LiquidType == LiquidID.Lava ? 'L' : '~';
					else if (t.HasTile) ch = 't';
					sb.Append(ch);
				}
				sb.AppendLine();
			}
			return sb.ToString();
		}

		private bool TvDry(int x) { int top = TvSurf[x]; return top > 0 && !TileNav.HasLiquid(x, top - 1) && !TileNav.HasLiquid(x, top - 2) && !TileNav.HasLiquid(x, top - 3); }

		private void TvBuild()
		{
			if (TvWorld)
			{
				int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_X0"), out int wx);
				TvScanSurface();
				if (wx <= 0) wx = 200;
				TvX0 = wx;
				{ var sf = new StringBuilder("x,surfaceRow,dry\n"); for (int x = 0; x < Main.maxTilesX; x++) sf.AppendLine($"{x},{TvSurf[x]},{(TvSurf[x] > 0 && TvDry(x) ? 1 : 0)}"); File.WriteAllText(Path.Combine(_tvOut, "surface.csv"), sf.ToString()); }
				_tvEvents.AppendLine($"world mode: X0={TvX0} world={Main.maxTilesX}x{Main.maxTilesY} surface={Main.worldSurface:0}");
				return;
			}
			TvDefineCourse();
			int world = Main.maxTilesX;
			TvX0 = Math.Max(60, Math.Min(world - TvLen - 80, 140));
			int x0 = TvX0 - 40, x1 = TvX0 + TvLen + 60;
			for (int x = x0; x <= x1; x++)
			{
				int off = x - TvX0;
				int e = off >= 0 && off < TvLen ? TvElev[off] : 0;
				int top = TvG - e;
				for (int y = 30; y < TvG + 100; y++)
				{
					if (y < top) TvAir(x, y);
					else { TvSet(x, y, y == top ? TileID.Grass : (y < top + 4 ? TileID.Dirt : TileID.Stone)); Main.tile[x, y].WallType = 0; Main.tile[x, y].LiquidAmount = 0; }
				}
				if (TvLavaC && off >= 0 && off < TvLen && TvLavaLvl[off] != int.MinValue)
					for (int y = TvG - TvLavaLvl[off]; y < top; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = LiquidID.Lava; }
				if (off >= 0 && off < TvLen && TvWater[off])
					for (int y = TvG; y < top; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = LiquidID.Water; }
			}
			foreach (int x in new[] { x0 - 1, x1 + 1 }) for (int y = 30; y < TvG + 100; y++) TvSet(x, y, TileID.GrayBrick);
			// house 382..406: 1-thick wood walls, doors at ground both ends, flat roof, a one-tile crate inside
			if (!TvRavine && !TvLips && Environment.GetEnvironmentVariable("LEVY_TRAVEL_NOHOUSE") != "1")   // (lab: LEVY_TRAVEL_NOHOUSE=1 leaves the house out; on the corruption course it stands across the chasm)
			{
				int hx0 = TvX0 + 382, hx1 = TvX0 + 406, top = TvG;
				for (int x = hx0; x <= hx1; x++)
					for (int y = top - 9; y < top; y++)
					{
						bool wall = x == hx0 || x == hx1 || y == top - 9;
						if (wall) TvSet(x, y, TileID.WoodBlock); else Main.tile[x, y].WallType = WallID.Wood;
					}
				foreach (int dx in new[] { hx0, hx1 }) for (int k = 0; k < 3; k++) { Tile t = Main.tile[dx, top - 3 + k]; t.HasTile = true; t.TileType = TileID.ClosedDoor; t.TileFrameX = 0; t.TileFrameY = (short)(18 * k); t.BlockType = BlockType.Solid; }
				TvSet((hx0 + hx1) / 2, top - 1, TileID.WoodBlock);
			}
			if (TvCorrupt)
			{
				for (int off = 212; off < 232; off++) for (int y = TvG; y <= TvG + 7; y++) TvSet(TvX0 + off, y, TileID.Stone); // rock lid over the cave floor (floor row TvG+12, four clear rows under the lid)
				// chasm walls: ledges every 10 rows, 2 wide, alternating sides
				for (int k = 1; k <= 5; k++) { int side = (k & 1) == 0 ? 400 : 406; for (int w = 0; w < 2; w++) for (int y = TvG + 10 * k; y < TvG + 10 * k + 2; y++) TvSet(TvX0 + side + w, y, TileID.Stone); }
			}
			else if (!TvZoo && !TvRavine && !TvLips) TvBuildFixtures();
			// natural slopes: smooth every surface step the way world generation does
			for (int x = x0; x <= x1; x++) { int off = x - TvX0; if (off < 0 || off >= TvLen) continue; int top = TvG - TvElev[off]; try { Tile.SmoothSlope(x, top, false, false); } catch { } }
			if (TvRavine || TvLips) { TvApplyRims(); _tvEvents.AppendLine("features: " + string.Join(" ", TvFeats.ConvertAll(f => f.Name + "@" + f.A))); }
			_tvEvents.AppendLine($"course built: X0={TvX0} world={world}x{Main.maxTilesY} zoo={TvZoo}");
		}

		private void TvBuildFixtures()
		{
			int world = Main.maxTilesX;
			// platform bridge over chasmB on the walkable course
			if (TvBridgeB) for (int off = 714; off <= 733; off++) { Tile t = Main.tile[TvX0 + off, TvG]; t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid; }
			// tunnel through the mountain: 5 tall at ground level, open at both slopes
			for (int off = 498; off <= 622; off++) for (int k = 1; k <= 5; k++) TvAir(TvX0 + off, TvG - k);
			// forest
			int trees = 0;
			try
			{
				var r = new Random(7);
				for (int off = 258; off < 312; off += 3 + r.Next(3))
					if (WorldGen.GrowTree(TvX0 + off, TvG - TvElev[off])) trees++;
			}
			catch (Exception ex) { _tvEvents.AppendLine("GrowTree failed: " + ex.Message); }
			_tvEvents.AppendLine($"fixtures: trees={trees}");
		}

		private int TvRow(int off)
		{
			if (TvWorld)
			{
				int x = Math.Clamp(TvX0 + off, 10, Main.maxTilesX - 10);
				for (int k = 0; k <= 40; k++) for (int sgn = -1; sgn <= 1; sgn += 2) { int xx = x + sgn * k; if (xx > 8 && xx < Main.maxTilesX - 8 && TvDry(xx)) return TvSurf[xx]; }
				return TvSurf[x];
			}
			return TvG - TvElev[Math.Clamp(off, 0, TvElev.Length - 1)]; // top solid row
		}

		// ---------------- run ----------------
		private string TvGearOf(int i)
		{
			if (_tvGear == "climb") { int c3 = i % 3; return c3 == 0 ? "spikes" : c3 == 1 ? "claws" : "tiger"; } // wall-climb gear arm: Shoe Spikes, Climbing Claws, Tiger Climbing Gear
			if (_tvGear == "wmix") { string[] wn = { "w:FledglingWings", "w:AngelWings", "w:FlameWings", "w:SteampunkWings" }; return wn[i % wn.Length]; }
			if (_tvGear == "wbare") return i % 2 == 0 ? "none" : "w:AngelWings";
			if (_tvGear == "hooks") return "hook:" + TvHookNames[i % TvHookNames.Length];
			if (_tvGear == "lavamix") { int c4 = i % 4; return c4 == 0 ? "none" : c4 == 1 ? "wings" : c4 == 2 ? "hook" : "wh"; }   // the lava lab: bare, wings, hook, wings + hook
			if (_tvGear != "mixed") return _tvGear;
			string mixEnv = Environment.GetEnvironmentVariable("LEVY_TRAVEL_MIX");   // lab: LEVY_TRAVEL_MIX=none,boots,wings,hook,... the gear of levy i is entry i % count (instead of the 15-levy mix)
			if (!string.IsNullOrEmpty(mixEnv)) { var mx = mixEnv.Split(','); return mx[i % mx.Length]; }
			int k = i % 15; // 15: 5 none, 4 boots, 3 wings, 3 hook
			if (k < 5) return "none"; if (k < 9) return "boots"; if (k < 12) return "wings"; return "hook";
		}

		private static readonly string[] TvHookNames = { "GrapplingHook", "AmethystHook", "SapphireHook", "EmeraldHook", "DiamondHook", "IvyWhip", "WebSlinger", "SkeletronHand", "SlimeHook", "FishHook", "DualHook", "LunarHook", "StaticHook" };
		private static int TvItemId(string name) { var f = typeof(ItemID).GetField(name); return f == null ? ItemID.AngelWings : (int)(short)f.GetValue(null); }
		private static int TvHookId(string name) { var f = typeof(ItemID).GetField(name); return f == null ? ItemID.GrapplingHook : (int)(short)f.GetValue(null); }

		private void TvSpawn()
		{
			GuideSystem.Instance.Autonomy = true;
			for (int i = 0; i < _tvN; i++)
			{
				var rec = MakeRecord("Pvt. Tv" + i);
				string g = TvGearOf(i);
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.HoldFire = !_tvGun; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				if (_tvGun)
				{
					var w = new Item(); w.SetDefaults(ItemID.Handgun);
					if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
					Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
				}
				TvKit(rec, g);
				if (g == "boots") { var it = new Item(); it.SetDefaults(ItemID.HermesBoots); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g.StartsWith("w:")) { var it = new Item(); it.SetDefaults(TvItemId(g.Substring(2))); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "wings" || g == "wh") { var it = new Item(); it.SetDefaults(ItemID.AngelWings); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "spikes" || g == "claws" || g == "tiger" || g == "ninja") { var it = new Item(); it.SetDefaults(g == "spikes" ? ItemID.ShoeSpikes : g == "claws" ? ItemID.ClimbingClaws : g == "tiger" ? ItemID.TigerClimbingGear : ItemID.MasterNinjaGear); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g.StartsWith("hook:")) { var it = new Item(); it.SetDefaults(TvHookId(g.Substring(5))); rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				if (g == "hook" || g == "wh") { var it = new Item(); it.SetDefaults(ItemID.GrapplingHook); rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				RosterSystem.Instance.Regulars.Add(rec);
				int off = _tvFrom + (i - _tvN / 2);
				Vector2 at = new Vector2((TvX0 + off) * 16f + 8f, TvSpawnRow(off) * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (_tvMode == "map" && Environment.GetEnvironmentVariable("LEVY_TRAVEL_HOLD") == "1") { rec.Order = SoldierOrder.Hold; rec.HoldPos = at; if (rec.HasBody && Main.npc[rec.BodyIndex].ModNPC is SoldierNPC hb) hb.HoldPosition = true; } // far commander: the levies wait where they are (a Follow levy 400+ tiles from the commander is pulled back to them before the order lands)
				var lv = new TvLevy { Gear = g, Rec = rec, Idx = idx };
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; lv.Start = lv.Last = lv.Anchor = b.NPC.Center; }
				_tv.Add(lv);
			}
		}

		private void TvPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			float fromX = (TvX0 + _tvFrom) * 16f;
			if (_tvMode == "follow")
			{
				// walks the route at an un-booted player's pace, flying over gaps (hovering at the level of the highest ground nearby)
				float dirSign = _tvTo >= _tvFrom ? 1f : -1f;
				if (_tvTick < 120) _tvCmdX = fromX - 40f * dirSign; else if ((_tvCmdX - (TvX0 + _tvTo) * 16f) * dirSign < 0f) _tvCmdX += 3.0f * dirSign;
				int row;
				if (TvWorld) { int cx = (int)(_tvCmdX / 16f); row = int.MaxValue; for (int k = -3; k <= 3; k++) if (TvSurf[Math.Clamp(cx + k, 10, Main.maxTilesX - 10)] > 0) row = Math.Min(row, TvSurf[Math.Clamp(cx + k, 10, Main.maxTilesX - 10)]); if (row == int.MaxValue) row = 200; }
				else
				{
					int off = Math.Clamp((int)(_tvCmdX / 16f) - TvX0, 0, TvLen);
					int ahead = -1000; for (int k = -2; k <= 3; k++) ahead = Math.Max(ahead, TvElev[Math.Clamp(off + k, 0, TvElev.Length - 1)]);
					row = TvG - ahead;
				}
				p.position = new Vector2(_tvCmdX - p.width / 2f, row * 16f - p.height); p.velocity = Vector2.Zero;
			}
			else
			{
				// the player is far from the army (LEVY_TRAVEL_CMDFAR tiles behind the start, default 400): off screen, out of the usual NPC range
				int far = int.TryParse(Environment.GetEnvironmentVariable("LEVY_TRAVEL_CMDFAR"), out int cf) ? cf : 400;
				float px = Math.Clamp(fromX - far * 16f, 40 * 16f, (Main.maxTilesX - 40) * 16f);
				int prow = TvSpawnRow(_tvFrom);
				if (Environment.GetEnvironmentVariable("LEVY_TRAVEL_CMDTRACK") == "1" && _tvTick >= 120)
				{
					// the commander travels with the army: stands at the middle levy (by x) that has a body, so the army is on screen around them
					var xs = new List<TvLevy>(); foreach (var l in _tv) if (l.Rec.HasBody && l.ArriveTick < 0) xs.Add(l);
					if (xs.Count > 0) { xs.Sort((a, b) => Main.npc[a.Rec.BodyIndex].Center.X.CompareTo(Main.npc[b.Rec.BodyIndex].Center.X)); NPC m = Main.npc[xs[xs.Count / 2].Rec.BodyIndex]; float cur = Main.player[0].Center.X; px = cur + Math.Clamp(m.Center.X - cur, -14f, 14f); prow = (int)((m.Bottom.Y - 1f) / 16f) + 1; }   // walks after the army at up to 14 px/tick, never jumps
				}
				p.position = new Vector2(px - p.width / 2f, prow * 16f - p.height); p.velocity = Vector2.Zero;
			}
			if (!_tvFoes) for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && (!n.friendly || Environment.GetEnvironmentVariable("LEVY_TRAVEL_NOCRITTERS") == "1")) { if (n.friendly && _tvTick % 600 == 0) _tvEvents.AppendLine($"t={_tvTick} removed critter {n.TypeName} at {n.Center.X / 16f - TvX0:0},{TvG - n.Center.Y / 16f:0}"); n.active = false; } }
		}

		/// <summary>FOES: every 6 s a day-time monster (slime / zombie / demon eye) appears 28 tiles ahead of the lead levy, like the night traffic of a real surface walk.</summary>
		private void TvSpawnFoe()
		{
			float lead = float.MinValue; float dirSign = _tvTo >= _tvFrom ? 1f : -1f; Vector2 at = default; bool any = false;
			foreach (var l in _tv) if (l.Rec.HasBody) { float c = Main.npc[l.Rec.BodyIndex].Center.X * dirSign; if (c > lead) { lead = c; at = Main.npc[l.Rec.BodyIndex].Center; any = true; } }
			if (!any) return;
			int hostile = 0; for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.damage > 0) hostile++; }
			if (hostile >= 8) return;
			int tx = (int)(at.X / 16f) + (int)(28 * dirSign); tx = Math.Clamp(tx, 20, Main.maxTilesX - 20);
			int ty = TvWorld ? (TvSurf[tx] > 0 ? TvSurf[tx] : (int)(at.Y / 16f)) : TvG - TvElev[Math.Clamp(tx - TvX0, 0, TvElev.Length - 1)];
			int[] kinds = { NPCID.GreenSlime, NPCID.Zombie, NPCID.DemonEye, NPCID.BlueSlime, NPCID.Zombie };
			NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("LevyTravel"), tx * 16 + 8, (ty - 2) * 16, kinds[_tvTick / 360 % kinds.Length]);
		}

		private void TvReset(TvLevy l) { l.Start = l.Last = l.Anchor = Main.npc[l.Rec.BodyIndex].Center; l.AnchorTick = _tvTick; l.BestProgTick = _tvTick; }

		private void StepTravel()
		{
			if (!_tvStarted)
			{
				ClearAll();
				if (_tvSpec) TvSpecBuild(); else TvBuild();
				_tvCmdX = (TvX0 + _tvFrom) * 16f;
				TvSpawn();
				_tvGx = TvX0 + _tvTo; _tvGy = _tvGoalRowAbs != int.MinValue ? _tvGoalRowAbs - 3 : TvRow(_tvTo) - 3;
				_tvStarted = true; _tvTick = 0; _tvWatch.Restart();
				TvPinCommander();
				SoldierNPC.StatArcStarts = SoldierNPC.StatArcFuelOut = SoldierNPC.FlyUnjams = 0; SoldierNPC.FlightShouts = SoldierNPC.FlightLines = 0; Pathfinder.StatReset(); RoutePlanner.StatReset(); SoldierHooks.ResetCounters(); PathSearch.StatHookScans = PathSearch.StatHookEdges = 0;
				SoldierNPC.CatchUpHops = 0; SoldierNPC.CatchUpHopsSeen = 0; SoldierNPC.CatchUpTicks = 0; //HOP
				if (Environment.GetEnvironmentVariable("LEVY_TRAVEL_PLAN") == "1")
				{
					// what the planner finds for each ability class between the two ends, searched to the end in one go (cost and end point)
					var ps = new PathSearch(LevyConstants.Movement.RouteWindowW, LevyConstants.Movement.RouteWindowH, LevyConstants.Movement.RouteWindowCells); var pts = new List<Vector2>();
					foreach (bool wg in new[] { false, true })
					{
						var ab = new MovementAbilities { JumpTiles = 6, HasWings = wg, WingTimeMax = wg ? 100 : 0 };
						Vector2 a = new Vector2((TvX0 + _tvFrom) * 16f + 8f, TvRow(_tvFrom) * 16f), b = new Vector2((TvX0 + _tvTo) * 16f + 8f, TvRow(_tvTo) * 16f);
						ps.Begin(a, b, ab, false, LevyConstants.Movement.RouteHWeight); var sw = System.Diagnostics.Stopwatch.StartNew(); while (ps.Status == PathSearch.State.Running && ps.Expanded < LevyConstants.Movement.RouteMaxNodes) ps.Step(5000); ps.Abandon(); double ms = sw.Elapsed.TotalMilliseconds;
						ps.BuildPath(pts); Point e = pts.Count > 0 ? TileNav.FeetTile(pts[pts.Count - 1]) : default;
						_tvEvents.AppendLine($"PLAN wings={wg}: status={ps.Status} expanded={ps.Expanded} ms={ms:0.0} pts={pts.Count} end=({e.X - TvX0},{TvG - e.Y})");
					}
				}
				Mod.Logger.Info($"HARNESS-TRAVEL: start mode={_tvMode} from={_tvFrom} to={_tvTo} n={_tv.Count} goal=({_tvGx},{_tvGy})");
				return;
			}
			_tvTick++;
			_tvPrevPl = Main.player[0].Center;
			OffscreenHop.LabViewers = new List<Vector2> { Main.player[0].Center }; //HOP
			TvPinCommander();
			if (_tvTick == 120 && _tvMode == "map" && _tvBannerMode) TvBannerOrder();
			else if (_tvTick == 120 && _tvMode == "map")
			{
				int took = MapOrder.Issue(Main.player[0], _tvGx, _tvGy);
				Mod.Logger.Info($"HARNESS-TRAVEL: order issued; took={took}");
			}
			if (_tvTick == 120) foreach (var l in _tv) if (l.Rec.HasBody) TvReset(l);
			if (_tvTick < 120) return;
			if (_tvFoes && _tvTick % 360 == 0) TvSpawnFoe();
			if (Environment.GetEnvironmentVariable("LEVY_LAVA_DROP") == "1" && _tvTick == 200) foreach (var l in _tv) if (l.Rec.HasBody) { NPC dn = Main.npc[l.Rec.BodyIndex]; dn.position = new Vector2((TvX0 + 685) * 16f + l.Idx % 5 * 8f, (TvG + 40 - 16 - 32) * 16f); dn.velocity = Vector2.Zero; dn.netUpdate = true; _tvEvents.AppendLine($"t={_tvTick} dropped {l.Rec.Name}({l.Gear}) into the shaft"); }   // 16 tiles above the top of a 40-deep shaft (row TvG+40 is its floor)
			if (Environment.GetEnvironmentVariable("LEVY_LAVA_GLIDE") == "1" && _tvTick == 200)
			{
				// over the basin, off the cliff: half of them with their wings already spent (a glide), the rest with flight left; walking off the edge toward the middle
				int gi = 0;
				foreach (var l in _tv) if (l.Rec.HasBody)
				{
					NPC gn = Main.npc[l.Rec.BodyIndex]; int off = 83 + (gi * 5) % 28; gi++;
					gn.position = new Vector2((TvX0 + off) * 16f, (TvG - 6) * 16f); gn.velocity = new Vector2(off < 97 ? 1.2f : -1.2f, 0.5f); gn.netUpdate = true;
					if (gn.ModNPC is SoldierNPC gs) { gs.NotifyTeleported(); gn.position = new Vector2((TvX0 + off) * 16f, (TvG - 6) * 16f); gn.velocity = new Vector2(off < 97 ? 1.2f : -1.2f, 0.5f); if (((gi - 1) / 4) % 2 == 0) gs.LabDrainWings(); }
					_tvEvents.AppendLine($"t={_tvTick} placed {l.Rec.Name}({l.Gear}) over the lava basin at {off}");
				}
			}
			float goalPx = _tvGx * 16f + 8f; float goalY = _tvGy * 16f;
			bool allDone = true;
			foreach (var l in _tv)
			{
				SoldierRecord rec = l.Rec;
				if (!rec.HasBody) { if (l.LastHasBody == 1) { l.Gone++; _tvEvents.AppendLine($"t={_tvTick} {rec.Name} body gone (last x={l.Last.X / 16f - TvX0:0} {TvFeature(l.Last.X / 16f - TvX0)})"); } l.LastHasBody = 0; if (l.ArriveTick < 0) allDone = false; continue; }
				l.LastHasBody = 1;
				NPC n = Main.npc[rec.BodyIndex]; var sn = (SoldierNPC)n.ModNPC;
				Vector2 c = n.Center; l.Idx = rec.BodyIndex;
				l.Frames++;
				if (l.ArriveTick < 0) { l.TravTicks++; if (n.velocity.Y != 0f && !n.wet) l.AirTicks++; if (sn.IsFlying) l.FlyTicks++; if (float.IsNaN(l.RefRow)) l.RefRow = Math.Max(l.Start.Y / 16f, _tvGy + 3f); float dep = n.Bottom.Y / 16f - l.RefRow; l.DepthSum += Math.Max(0f, dep); if (dep > l.MaxDepth) l.MaxDepth = dep; }
				l.ClimbDone = sn.ClimbEdges; //HOP
				l.HkCasts = sn.HkCasts; l.HkLatches = sn.HkLatches; l.HkChains = sn.HkChains; l.HkAborts = sn.HkAborts; l.HkReleases = sn.HkReleases; l.HkLandings = sn.HkLandings; l.RtCasts = sn.RouteHookCasts; l.RtBans = sn.RouteHookBans; l.StepBacks = sn.HkStepBacks; l.EndElev = TvG - n.Bottom.Y / 16f;
				float step = Vector2.Distance(c, l.Last);
				float offNow = c.X / 16f - TvX0;
				if (step > 6 * 16f) { l.Teleports++; l.Hops += _tvMode == "map" ? 1 : 0; if (TvSeen(l.Last) || TvSeen(c)) { l.VisTele++; _tvEvents.AppendLine($"t={_tvTick} VISIBLE hop {rec.Name} from {l.Last.X / 16f:0},{l.Last.Y / 16f:0} to {c.X / 16f:0},{c.Y / 16f:0} player now {Main.player[0].Center.X / 16f:0},{Main.player[0].Center.Y / 16f:0} prev {_tvPrevPl.X / 16f:0},{_tvPrevPl.Y / 16f:0}"); } _tvEvents.AppendLine($"t={_tvTick} {rec.Name}({l.Gear}) TELEPORT {l.Last.X / 16f - TvX0:0} -> {offNow:0} ({TvFeature(l.Last.X / 16f - TvX0)})"); }
				else l.Path += step;
				l.Last = c;
				bool grounded = n.velocity.Y == 0f;
				if (!grounded && n.velocity.Y > 2f && !l.Falling && !sn.IsFlying) { l.Falling = true; l.FallStartY = c.Y; }
				if (l.Falling && sn.IsFlying) l.Falling = false;   // a powered descent is not a fall
				if (l.Falling && grounded) { float drop = (c.Y - l.FallStartY) / 16f; if (drop >= 8f) { l.Falls++; l.FallTiles += drop; _tvEvents.AppendLine($"t={_tvTick} {rec.Name}({l.Gear}) FALL {drop:0} tiles at {offNow:0} ({TvFeature(offNow)})"); } l.Falling = false; }
				if (n.wet) { l.WetTicks++; int tx = (int)(c.X / 16f), ty = (int)(c.Y / 16f) - 1; if (Main.tile[tx, ty].LiquidAmount > 0) l.DeepTicks++; }
				// stuck: no 4-tile exit from the anchor for >180 ticks (only while still travelling: not once arrived, and in Follow only while more than 15 tiles from the commander)
				if (n.lavaWet)
				{
					l.LavaTicks++; l.LavaLastTick = _tvTick;
					if (!l.InLava)
					{
						l.InLava = true; l.LavaEntries++;
						_tvEvents.AppendLine($"t={_tvTick} {rec.Name}({l.Gear}) ENTERED LAVA at {offNow:0} ({TvFeature(offNow)}) v={n.velocity.X:0.0},{n.velocity.Y:0.0} state={sn.State} blockWhy={sn.MoveBlocked} hook={sn.HookState} why={sn.StillReason()} knock={_tvKnockTick.GetValueOrDefault(rec.Name, -999)} {sn.DebugRoute()}");
					}
				}
				else l.InLava = false;
				if (Environment.GetEnvironmentVariable("LEVY_LAVA_KNOCK") == "1" && _tvTick > 150 && _tvTick % 90 == 0 && l.ArriveTick < 0 && n.velocity.Y == 0f)
				{
					// a hit: a shove of 7 px/tick toward the nearest lava column within 10 tiles, and a small hop
					int cx = (int)(c.X / 16f), best = 0, bd = 99;
					for (int dx = -10; dx <= 10; dx++) { int o = cx - TvX0 + dx; if (o < 0 || o >= TvLen || TvLavaLvl[o] == int.MinValue) continue; if (Math.Abs(dx) < bd) { bd = Math.Abs(dx); best = Math.Sign(dx); } }
					if (bd < 99) { n.velocity.X = best * 7f; n.velocity.Y = -3f; _tvKnockTick[rec.Name] = _tvTick; n.netUpdate = true; }
				}
				bool tracking = l.ArriveTick < 0 && (_tvMode != "follow" || Math.Abs(c.X - Main.player[0].Center.X) > 15 * 16f);
				if (!tracking) { if (l.InStuck) { l.StuckTicks += _tvTick - l.AnchorTick; l.InStuck = false; } l.Anchor = c; l.AnchorTick = _tvTick; l.BestProgTick = _tvTick; if (l.StallOpen) { l.StallTicks += _tvTick - l.StallFrom; l.StallOpen = false; } }
				else if (Vector2.Distance(c, l.Anchor) >= 64f)
				{
					if (l.InStuck) { l.StuckTicks += _tvTick - l.AnchorTick; l.InStuck = false; }
					l.Anchor = c; l.AnchorTick = _tvTick;
				}
				else if (!l.InStuck && _tvTick - l.AnchorTick > 180)
				{
					l.InStuck = true; l.StuckEps++;
					if (l.StuckWhere.Length < 200) l.StuckWhere += TvFeature(offNow) + "@" + (int)offNow + " ";
					if (_tvAscii < 6) { _tvAscii++; _tvEvents.AppendLine("MAP around " + rec.Name + " (@ levy feet tile; # solid, / slope, = platform, ~ water, t other tile; x from -24 to +40, y from -14 to +10):\n" + TvAscii((int)(c.X / 16f), (int)((n.Bottom.Y - 1f) / 16f), 1)); }
					_tvEvents.AppendLine($"t={_tvTick} {rec.Name}({l.Gear}) STUCK at {offNow:0} ({TvFeature(offNow)}) state={sn.State} order={rec.Order} blocked={sn.MoveBlocked} why={sn.StillReason()} dropWhy={sn.DropWhy}  hasPath={sn.HasPath} {sn.DebugRoute()} v={n.velocity.X:0.0},{n.velocity.Y:0.0} noGrav={n.noGravity} wet={n.wet} hookSt={sn.HookState} fly={sn.IsFlying} {TvTiles(n)} elev={(TvG - c.Y / 16f):0}");
				}
				// global stall: best progress toward the goal has not improved for 600 ticks
				float prog = _tvSpec ? (Vector2.Distance(l.Start, new Vector2(goalPx, goalY)) - Vector2.Distance(c, new Vector2(goalPx, goalY))) / 16f : (_tvTo >= _tvFrom ? offNow - _tvFrom : _tvFrom - offNow);
				if (prog > l.BestProg + 1f) { if (l.StallOpen) { l.StallTicks += _tvTick - l.StallFrom; l.StallOpen = false; } l.BestProg = prog; l.BestProgTick = _tvTick; }
				else if (!l.StallOpen && _tvTick - l.BestProgTick > 600) { l.StallOpen = true; l.StallFrom = l.BestProgTick; l.StallEps++; if (l.StallWhere.Length < 200) l.StallWhere += TvFeature(offNow) + "@" + (int)offNow + " "; }
				// arrival
				if (l.ArriveTick < 0)
				{
					float dxT = Math.Abs(c.X - goalPx) / 16f, dyT = Math.Abs(n.Bottom.Y - goalY) / 16f;
					if (_tvBannerMode ? TvBannerArrived(sn, n) : (dxT <= 12f && dyT <= 10f)) l.ArriveTick = _tvTick - 120;
					else if (_tvMode == "map" && rec.Order != (_tvBannerMode ? SoldierOrder.HoldBanners : SoldierOrder.GoTo) && l.GaveUp < 0) { l.GaveUp = _tvTick - 120; _tvEvents.AppendLine($"t={_tvTick} {rec.Name}({l.Gear}) ORDER ENDED away from goal at {offNow:0} ({TvFeature(offNow)}) now={rec.Order}"); }
					if (l.ArriveTick < 0 && l.GaveUp < 0) allDone = false;
				}
				if (_tvDbgFrom >= 0 && _tvTick >= _tvDbgFrom && _tvTick <= _tvDbgTo && _tvTick % _tvDbgStep == 0 && (_tvDbgWho < 0 || l.Idx == _tvDbgWho || _tv.IndexOf(l) == _tvDbgWho)) _tvEvents.AppendLine($"DBG t={_tvTick} {rec.Name} pos=({n.Center.X / 16f - TvX0:0.00},{TvG - n.Bottom.Y / 16f:0.00}) v=({n.velocity.X:0.00},{n.velocity.Y:0.00}) fly={sn.IsFlying} wing={sn.WingTime} blocked={sn.MoveBlocked} why={sn.StillReason()} {sn.DebugRoute()} {TvTiles(n)} solidL={Collision.SolidCollision(n.position + new Vector2(-2f, 0f), n.width, n.height)} solidR={Collision.SolidCollision(n.position + new Vector2(2f, 0f), n.width, n.height)} solidUp={Collision.SolidCollision(n.position + new Vector2(0f, -2f), n.width, n.height)} solidNow={Collision.SolidCollision(n.position, n.width, n.height)} tcolL={Collision.TileCollision(n.position, new Vector2(-3f, 0f), n.width, n.height, false, false, 1).X:0.0} tcolR={Collision.TileCollision(n.position, new Vector2(3f, 0f), n.width, n.height, false, false, 1).X:0.0} col={n.collideX}/{n.collideY} pos=({n.position.X:0.0},{n.position.Y:0.0})");
				if (_tvTick % 30 == 0) _tvTrace.AppendLine($"{_tvTick},{rec.Name},{l.Gear},{offNow:0.0},{TvG - c.Y / 16f:0.0},{(int)sn.State}");
			}
			if (_tvTick % 600 == 0) Mod.Logger.Info($"HARNESS-TRAVEL: t={_tvTick} arrived={_tv.FindAll(l => l.ArriveTick >= 0).Count}/{_tv.Count} medOffset={Median():0}");
			if ((allDone && _tvTick > 300 && !TvHoldPhase()) || _tvTick >= _tvCap + 120) TvFinish();
		}

		/// <summary>Is this point inside the 2400x1400 px box around any active player (the harness's own test, not the mod's).</summary>
		private static Vector2 _tvPrevPl;
		private static bool TvSeen(Vector2 pt)
		{
			if (Math.Abs(pt.X - _tvPrevPl.X) <= 1200f && Math.Abs(pt.Y - _tvPrevPl.Y) <= 700f) return true;   // the player where he stood when the levies moved this tick
			for (int i = 0; i < Main.maxPlayers; i++) { Player p = Main.player[i]; if (p.active && Math.Abs(pt.X - p.Center.X) <= 1200f && Math.Abs(pt.Y - p.Center.Y) <= 700f) return true; }
			return false;
		}

		private string TvTiles(NPC n)
		{
			var f = Terraria.Utils.ToTileCoordinates(n.Bottom - new Vector2(0, 1)); var sb = new StringBuilder("tiles[feet=" + f.X + "," + (f.Y - TvG) + ":");
			for (int dx = -3; dx <= 3; dx++) { var sbb = new StringBuilder(); for (int dy = -2; dy <= 1; dy++) { Tile t = Main.tile[f.X + dx, f.Y + dy]; sbb.Append(t.HasTile ? t.TileType.ToString() + (t.Slope != SlopeType.Solid ? "s" + (int)t.Slope : "") + (t.IsHalfBlock ? "h" : "") : "."); sbb.Append('/'); } sb.Append(" " + (dx >= 0 ? "+" : "") + dx + "=" + sbb); }   // columns -3..+3; rows two over the head .. the floor (the feet row is the third)
			return sb + "]";
		}

		private float Median()
		{
			var xs = new List<float>(); foreach (var l in _tv) xs.Add(l.Last.X / 16f - TvX0); xs.Sort(); return xs.Count == 0 ? 0 : xs[xs.Count / 2];
		}

		private void TvFinish()
		{
			OffscreenHop.LabViewers = null; OffscreenHop.LabNoHop = false; //HOP
			double ms = _tvWatch.Elapsed.TotalMilliseconds;
			if (_tvBannerMode) TvBannerReport();
			var sb = new StringBuilder();
			sb.AppendLine("name,gear,arrived,arriveSec,gaveUpSec,straightTiles,pathTiles,ratio,stuckSec,stuckEps,stallSec,stallEps,teleports,falls,fallTiles,wetSec,deepSec,bodyGone,endOffset,endFeature,endState,endOrder,stuckWhere,stallWhere,visTele,climbEdges,hkCasts,hkLatches,hkChains,hkAborts,hkReleases,hkLandings,rtCasts,rtBans,stepBacks,endElev,lavaSec,lavaEntries,maxDepth,meanDepth,flyShare,airShare,travSec");
			foreach (var l in _tv)
			{
				if (l.InStuck) l.StuckTicks += _tvTick - l.AnchorTick;
				if (l.StallOpen) l.StallTicks += _tvTick - l.StallFrom;
				float straight = Vector2.Distance(l.Start, new Vector2(_tvGx * 16f + 8f, _tvGy * 16f)) / 16f;
				float pathT = l.Path / 16f; float endOff = l.Last.X / 16f - TvX0;
				string st = ""; if (l.Rec.HasBody) st = ((SoldierNPC)Main.npc[l.Rec.BodyIndex].ModNPC).State.ToString();
				sb.AppendLine(string.Join(",", l.Rec.Name, l.Gear, l.ArriveTick >= 0 ? 1 : 0, l.ArriveTick >= 0 ? (l.ArriveTick / 60f).ToString("0.0") : "", l.GaveUp >= 0 ? (l.GaveUp / 60f).ToString("0.0") : "", straight.ToString("0"), pathT.ToString("0"), (pathT / Math.Max(1f, straight)).ToString("0.00"), (l.StuckTicks / 60f).ToString("0.0"), l.StuckEps, (l.StallTicks / 60f).ToString("0.0"), l.StallEps, l.Teleports, l.Falls, l.FallTiles.ToString("0"), (l.WetTicks / 60f).ToString("0.0"), (l.DeepTicks / 60f).ToString("0.0"), l.Gone, endOff.ToString("0"), TvFeature(endOff), st, l.Rec.Order.ToString(), "\"" + l.StuckWhere.Trim() + "\"", "\"" + l.StallWhere.Trim() + "\"", l.VisTele, l.ClimbDone, l.HkCasts, l.HkLatches, l.HkChains, l.HkAborts, l.HkReleases, l.HkLandings, l.RtCasts, l.RtBans, l.StepBacks, l.EndElev.ToString("0"), (l.LavaTicks / 60f).ToString("0.0"), l.LavaEntries, l.MaxDepth.ToString("0.0"), (l.DepthSum / Math.Max(1, l.TravTicks)).ToString("0.0"), (l.FlyTicks / (float)Math.Max(1, l.TravTicks)).ToString("0.000"), (l.AirTicks / (float)Math.Max(1, l.TravTicks)).ToString("0.000"), (l.TravTicks / 60f).ToString("0.0")));
			}
			File.WriteAllText(Path.Combine(_tvOut, "levies.csv"), sb.ToString());
			File.WriteAllText(Path.Combine(_tvOut, "trace.csv"), "tick,name,gear,x,elevation,state\n" + _tvTrace);
			_tvEvents.AppendLine($"pathfinder: {Pathfinder.StatSummary()} ; planner: {RoutePlanner.StatSummary()} ; wall ms for run {ms:0}");
			int dumped = 0; var dumpedGear = new HashSet<string>();
			foreach (var l in _tv)
			{
				if (l.ArriveTick >= 0 || !l.Rec.HasBody) continue;
				if (!dumpedGear.Add(l.Gear)) continue;
				dumped++; NPC n = Main.npc[l.Rec.BodyIndex]; var sn = (SoldierNPC)n.ModNPC;
				_tvEvents.AppendLine($"END MAP {l.Rec.Name}({l.Gear}) at {n.Center.X / 16f - TvX0:0},{n.velocity} state={sn.State} order={l.Rec.Order} why={sn.StillReason()} {sn.DebugRoute()}\n" + TvAscii((int)(n.Center.X / 16f), (int)((n.Bottom.Y - 1f) / 16f), 1));
			}
			_tvEvents.AppendLine($"hops: total={SoldierNPC.CatchUpHops} seenByMod={SoldierNPC.CatchUpHopsSeen} ms={SoldierNPC.CatchUpTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.00} tries={SoldierNPC.CatchUpTries} last={SoldierNPC.CatchUpLast}"); //HOP
			foreach (string rl in RoutePlanner.Log) _tvEvents.AppendLine(rl);
			_tvEvents.AppendLine($"arcs: starts={SoldierNPC.StatArcStarts} fuelOut={SoldierNPC.StatArcFuelOut} shouts={SoldierNPC.FlightShouts} flightLines={SoldierNPC.FlightLines} unjams={SoldierNPC.FlyUnjams}");
			_tvEvents.AppendLine($"lavaguard: ticks={SoldierNPC.LavaGuardTicks} kicks={SoldierNPC.LavaKicks} escapeTicks={SoldierNPC.LavaEscapeTicks}");
			_tvEvents.AppendLine($"airctl: dangerTicks={SoldierNPC.StatLavaDangerTicks} lavaCatches={SoldierNPC.StatLavaCatches} hookedTicks={SoldierNPC.StatHookedTicks} hangTicks={SoldierNPC.StatHangTicks} wingHold={SoldierNPC.StatWingHoldTicks} wingRel={SoldierNPC.StatWingRelTicks} escapeFlight={SoldierNPC.StatEscapeTicks}");
			_tvEvents.AppendLine($"hookstats: plans={SoldierHooks.Plans} found={SoldierHooks.PlansFound} planMs={SoldierHooks.PlanTs * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0} tickCalls={SoldierHooks.TickCalls} tickMs={SoldierHooks.TickTs * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0} projMs={SoldierHooks.ProjTs * 1000.0 / System.Diagnostics.Stopwatch.Frequency:0.0} casts={SoldierHooks.Casts} ok={SoldierHooks.CastsOk} latches={SoldierHooks.Latches} releases={SoldierHooks.Releases} landings={SoldierHooks.Landings} aborts={SoldierHooks.Aborts} scans={PathSearch.StatHookScans} edges={PathSearch.StatHookEdges}");
			foreach (string hl in SoldierHooks.CastLog) _tvEvents.AppendLine(hl);
			File.WriteAllText(Path.Combine(_tvOut, "events.txt"), _tvEvents.ToString());
			int arrived = _tv.FindAll(l => l.ArriveTick >= 0).Count;
			Mod.Logger.Info($"HARNESS-TRAVEL: RESULT mode={_tvMode} from={_tvFrom} to={_tvTo} arrived={arrived}/{_tv.Count} ticks={_tvTick} pathfinder[{Pathfinder.StatSummary()}] planner[{RoutePlanner.StatSummary()}] out={_tvOut}");
			if (TvNextRun()) return;
			Finish($"travel scenario complete arrived={arrived}/{_tv.Count}");
		}
	}
}
