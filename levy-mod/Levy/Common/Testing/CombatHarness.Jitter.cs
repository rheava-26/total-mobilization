using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Common.Movement;
using Levy.Common.Systems;
using Levy.Common.Flavor;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// JITTER (LEVY_HARNESS_SCENARIO=jitter). Eve: "levies will get stuck on 1x1, 2x2 or 2x1 platforms and then keep jittering around in it".
	/// An arena floor with small footholds standing 4 and 8 tiles above it (1x1 / 2x1 / 2x2 platform tiles, 1x1 / 2x2 solid blocks) and closed or half-open pockets.
	/// Phases (LEVY_JT_PHASES, default "follow,fight,pocket"): follow (half the army starts ON the footholds, the commander paces the floor), fight (a stand-in boss 25 tiles away: do the gunners climb onto the footholds and stay there),
	/// pocket (levies start inside small enclosed pockets / shafts, the commander is outside). LEVY_JT_N = army size (24), LEVY_JT_GEAR = bare | mixed.
	/// Every WINDOW (300 ticks) each levy is judged: direction reversals of its x motion, how far it went, how many times it left the ground. A levy that reverses >= 10 times in a window and never leaves a 6-tile
	/// span is JITTERING; it is logged with its state, goal, reason it is still, and the size of the standing cluster under it.
	/// </summary>
	public partial class CombatHarness
	{
		private static readonly int JtN = int.TryParse(Environment.GetEnvironmentVariable("LEVY_JT_N"), out int jtn) ? jtn : 24;
		private static readonly string JtGear = Environment.GetEnvironmentVariable("LEVY_JT_GEAR") ?? "mixed";
		private static readonly string[] JtPhases = (Environment.GetEnvironmentVariable("LEVY_JT_PHASES") ?? "follow,fight,pocket").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		private static readonly string JtTrace = Environment.GetEnvironmentVariable("LEVY_JT_TRACE");
		private static readonly int JtTraceFrom = int.TryParse(Environment.GetEnvironmentVariable("LEVY_JT_TRACEFROM"), out int jtf) ? jtf : 1200;
		private static readonly bool JtNoFoot = Environment.GetEnvironmentVariable("LEVY_JT_NOFOOT") == "1";
		private const int JtWindow = 300, JtWindows = 8;
		private int _jtPhase, _jtTick, _jtPass, _jtFail, _jtBoss = -1; private bool _jtStarted;
		private readonly List<SoldierRecord> _jtRecs = new List<SoldierRecord>();
		private sealed class JtTrack { public float LastX, MinX, MaxX, StartX, LastDx; public int Reversals, Hops, Tele; public bool WasAir; public Vector2 LastC; public int Start; public string Spot = ""; }
		private readonly Dictionary<SoldierRecord, JtTrack> _jtTrack = new Dictionary<SoldierRecord, JtTrack>();
		private readonly HashSet<string> _jtJitterers = new HashSet<string>();
		private int _jtFloorStrafe, _jtJitterWindows, _jtWindowsJudged, _jtTele, _jtOnTiny, _jtOnTinySamples;
		private readonly List<(int x, int y, int w, int h, bool plat, string name)> _jtShapes = new List<(int, int, int, int, bool, string)>();
		private void JtLog(string s) => Mod.Logger.Info("HARNESS-JITTER: " + s);
		private bool BeginJitter() { _jtTick = 0; _jtPhase = 0; _jtPass = _jtFail = 0; _jtStarted = false; _jtRecs.Clear(); return true; }
		private void JtCheck(bool ok, string what) { if (ok) _jtPass++; else _jtFail++; JtLog((ok ? "PASS " : "FAIL ") + what); }

		private void JtPlace(int x, int y, bool plat)
		{
			Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = plat ? TileID.Platforms : TileID.GrayBrick; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
		}

		private void JtBuildArena(string phase)
		{
			for (int x = _cx - 95; x <= _cx + 95; x++)
				for (int y = ClearTop; y < FloorY; y++) { Tile t = Main.tile[x, y]; t.HasTile = false; t.TileType = 0; t.WallType = 0; t.LiquidAmount = 0; }
			_jtShapes.Clear();
			if (phase == "pocket")
			{
				// pockets: 0 closed 2x3, 1 closed 1x2, 2 shaft open at the top 2x5, 3 shaft 1x4 open at the top, 4 closed 3x3, 5 half-open (a 1-tile window in the side wall at the bottom)
				int[] ix = { 2, 1, 2, 1, 3, 2 }, ih = { 3, 2, 5, 4, 3, 3 }; bool[] open = { false, false, true, true, false, false };
				for (int p = 0; p < 6; p++)
				{
					int x0 = _cx - 60 + p * 20, w = ix[p] + 2, top = FloorY - ih[p] - 1;
					for (int x = x0; x < x0 + w; x++) for (int y = top; y < FloorY; y++) { bool edge = x == x0 || x == x0 + w - 1 || y == top; if (!edge) continue; JtPlace(x, y, false); }
					// floor of the pocket is the arena floor; open the top for shafts, a window for the last one
					if (open[p]) for (int x = x0 + 1; x < x0 + w - 1; x++) { Tile t = Main.tile[x, top]; t.HasTile = false; }
					if (p == 5) { Tile t = Main.tile[x0 + w - 1, FloorY - 1]; t.HasTile = false; }
					_jtShapes.Add((x0 + 1, FloorY - 1, ix[p], ih[p], false, $"pocket{p}({ix[p]}x{ih[p]}{(open[p] ? " open top" : p == 5 ? " side window" : " closed")})"));
				}
				return;
			}
			// footholds: kind cycles; heights alternate 4 / 8 tiles above the floor; both sides of the commander
			var kinds = new (int w, int h, bool plat, string n)[] { (1, 1, true, "plat1x1"), (2, 1, true, "plat2x1"), (2, 2, false, "block2x2"), (1, 1, false, "block1x1"), (2, 2, true, "plat2x2") };
			int[] dxs = JtNoFoot ? new int[0] : new[] { -42, -34, -26, -18, -10, 10, 18, 26, 34, 42 };
			for (int i = 0; i < dxs.Length; i++)
			{
				var k = kinds[i % kinds.Length]; int up = i % 2 == 0 ? 4 : 8; int x0 = _cx + dxs[i], yTop = FloorY - up; // yTop = row of the foothold's top tile
				for (int ax = 0; ax < k.w; ax++) for (int ay = 0; ay < k.h; ay++) JtPlace(x0 + ax, yTop + ay, k.plat);
				_jtShapes.Add((x0, yTop - 1, k.w, k.h, k.plat, k.n));
			}
		}

		private void JtPin(string phase)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = 5000;
			float ox = 0f; int dir = 1;
			if (phase == "follow" && _jtTick >= 120) { double ph = (_jtTick - 120) * Math.PI / 450.0; ox = (float)(30.0 * Math.Sin(ph)) * 16f; dir = Math.Cos(ph) >= 0 ? 1 : -1; }
			if (phase == "pocket") ox = 12f * 16f * (float)Math.Sin(_jtTick * Math.PI / 900.0);
			p.position = new Vector2(_cx * 16f + ox - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero; p.direction = dir;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (i != _jtBoss && n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
			if (_jtBoss >= 0) { NPC b = Main.npc[_jtBoss]; if (b.active) { b.life = b.lifeMax = 2000000; b.timeLeft = 99999; } }
		}

		private void JtSetup(string phase)
		{
			ClearAll(); _jtBoss = -1; JtBuildArena(phase);
			GuideSystem.Instance.Autonomy = true; _jtRecs.Clear(); _jtTrack.Clear();
			FollowRows.Reset(); _jtTick = 0;
			JtPin(phase);
			for (int k = 0; k < JtN; k++)
			{
				var rec = MakeRecord("Pvt. J" + k);
				var w = new Item(); w.SetDefaults(ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
				string g = JtGear == "bare" ? "bare" : new[] { "bare", "boots", "wings", "hook" }[k % 4];
				if (g == "boots") { var it = new Item(); it.SetDefaults(ItemID.HermesBoots); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "wings") { var it = new Item(); it.SetDefaults(ItemID.AngelWings); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "hook") { var it = new Item(); it.SetDefaults(ItemID.GrapplingHook); rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; Formation.Apply(rec, FormationMode.Normal); rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at;
				if (phase == "follow" && k % 2 == 0 && _jtShapes.Count > 0) { var s = _jtShapes[(k / 2) % _jtShapes.Count]; at = JtTopOf(s); }
				else if (phase == "pocket") { var s = _jtShapes[k % _jtShapes.Count]; at = new Vector2((s.x + s.w * 0.5f) * 16f, FloorY * 16f); }
				else at = new Vector2((_cx - 4 + (k % 9) * 1.0f) * 16f, FloorY * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
				_jtRecs.Add(rec);
			}
			if (phase == "fight") { _jtBoss = BtSpawnDummy((_cx + 25) * 16f, (FloorY - 7) * 16f); JtLog("boss spawned idx=" + _jtBoss); }
			JtLog($"PHASE {phase}: n={_jtRecs.Count} shapes={string.Join(",", _jtShapes.Select(s => s.name + "@" + (s.x - _cx) + "," + (FloorY - s.y - 1)))}");
		}

		/// <summary>Bottom centre of the top of a shape (its top tile row), so a body placed there stands on it.</summary>
		private Vector2 JtTopOf((int x, int y, int w, int h, bool plat, string name) s) => new Vector2((s.x + s.w * 0.5f) * 16f, (s.y + 1) * 16f);

		private string JtCluster(NPC b) => JtCluster(b, out _, out _);
		private string JtCluster(NPC b, out int width, out int up)
		{
			Point f = TileNav.FeetTile(b.Bottom); int l = 0, r = 0;
			while (l < 12 && TileNav.IsStandable(f.X - l - 1, f.Y)) l++;
			while (r < 12 && TileNav.IsStandable(f.X + r + 1, f.Y)) r++;
			width = l + r + 1; up = FloorY - f.Y - 1;
			return $"standing run {l + r + 1} wide at feet=({f.X - _cx},{FloorY - f.Y - 1} above floor)";
		}

		private void StepJitter()
		{
			if (_jtPhase >= JtPhases.Length) { Finish($"jitter scenario complete pass={_jtPass} fail={_jtFail}"); return; }
			string phase = JtPhases[_jtPhase];
			_jtTick++;
			if (!_jtStarted) { JtSetup(phase); _jtStarted = true; _jtTick = 0; _jtFloorStrafe = _jtJitterWindows = _jtWindowsJudged = _jtTele = _jtOnTiny = _jtOnTinySamples = 0; _jtJitterers.Clear(); return; }
			JtPin(phase);
			foreach (var rec in _jtRecs)
			{
				if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; if (!b.active) continue;
				if (!_jtTrack.TryGetValue(rec, out var tr)) { tr = new JtTrack { LastX = b.Center.X, MinX = b.Center.X, MaxX = b.Center.X, StartX = b.Center.X, LastC = b.Center, Start = _jtTick }; _jtTrack[rec] = tr; }
				float dx = b.Center.X - tr.LastX;
				if (Math.Abs(dx) > 0.05f) { if (tr.LastDx != 0f && Math.Sign(dx) != Math.Sign(tr.LastDx)) tr.Reversals++; tr.LastDx = dx; }
				tr.LastX = b.Center.X; tr.MinX = Math.Min(tr.MinX, tr.LastX); tr.MaxX = Math.Max(tr.MaxX, tr.LastX);
				bool air = b.velocity.Y != 0f; if (air && !tr.WasAir) tr.Hops++; tr.WasAir = air;
				if (Math.Abs(b.Center.X - tr.LastC.X) > 6 * 16f || Math.Abs(b.Center.Y - tr.LastC.Y) > 6 * 16f) { tr.Tele++; _jtTele++; JtLog($"TELEPORT {phase} t={_jtTick} {rec.Name} -> ({(b.Center.X / 16f - _cx):0},{(FloorY - b.Center.Y / 16f):0})"); }
				tr.LastC = b.Center;
			}
			if (_jtTick % JtWindow == 0 && _jtTick >= JtWindow)
			{
				foreach (var rec in _jtRecs)
				{
					if (!rec.HasBody || !_jtTrack.TryGetValue(rec, out var tr)) continue; NPC b = Main.npc[rec.BodyIndex]; var sn = b.ModNPC as SoldierNPC;
					float span = (tr.MaxX - tr.MinX) / 16f;
					_jtWindowsJudged++;
					string cl = JtCluster(b, out int wd, out int upTiles); bool tiny = b.velocity.Y == 0f && wd <= 4 && upTiles >= 2;
					_jtOnTinySamples++; if (tiny) _jtOnTiny++;
					if (tr.Reversals >= 10 && span < 6f)
					{
						if (tiny) { _jtJitterWindows++; _jtJitterers.Add(rec.Name); } else _jtFloorStrafe++;
						JtLog($"JITTER {phase} t={_jtTick} {rec.Name}: reversals={tr.Reversals} span={span:0.0}t hops={tr.Hops} state={sn?.State} goal={(sn != null && sn.HasMoveGoal ? ((sn.MoveGoal.X / 16f) - _cx).ToString("0") + "," + (FloorY - sn.MoveGoal.Y / 16f).ToString("0") : "-")} why={sn?.StillReason()} {cl} frRow={(sn == null || sn.FollowRowY == int.MinValue ? "none" : sn.FollowRowY.ToString())}");
					}
					tr.Reversals = 0; tr.Hops = 0; tr.MinX = tr.MaxX = b.Center.X; tr.StartX = b.Center.X;
				}
			}
			if (JtTrace != null && _jtTick >= JtTraceFrom && _jtTick < JtTraceFrom + 150)
			{
				var rec = _jtRecs.FirstOrDefault(r => r.Name == JtTrace);
				if (rec != null && rec.HasBody) { NPC b = Main.npc[rec.BodyIndex]; var sn = b.ModNPC as SoldierNPC; JtLog($"TRACE t={_jtTick} x={(b.Center.X / 16f - _cx):0.00} y={(FloorY - b.Bottom.Y / 16f):0.00} vx={b.velocity.X:0.00} vy={b.velocity.Y:0.00} goal={(sn != null && sn.HasMoveGoal ? ((sn.MoveGoal.X / 16f) - _cx).ToString("0.00") + "," + (FloorY - sn.MoveGoal.Y / 16f).ToString("0.0") : "-")} st={sn?.State} why={sn?.StillReason()}"); }
			}
			int total = JtWindow * JtWindows;
			if (_jtTick >= total) { JtEnd(phase); _jtPhase++; _jtStarted = false; }
		}

		private void JtEnd(string phase)
		{
			int stuckTiny = 0;
			foreach (var rec in _jtRecs)
			{
				if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; var sn = b.ModNPC as SoldierNPC;
				bool enclosed = phase == "pocket" && Math.Abs(b.Center.X / 16f - _cx) < 200 && (FloorY - b.Bottom.Y / 16f) < 3 && JtInsidePocket(b);
				if (enclosed) stuckTiny++;
			}
			JtLog($"RESULT {phase}: n={_jtRecs.Count} windowsJudged={_jtWindowsJudged} jitterWindowsOnTinyFooting={_jtJitterWindows} floorStrafeWindows(not counted)={_jtFloorStrafe} jitterLevies={_jtJitterers.Count} ({string.Join(",", _jtJitterers.Take(8))}) windowsOnTinyFooting={_jtOnTiny}/{_jtOnTinySamples} teleports={_jtTele} stillInsidePocket={stuckTiny}");
			JtCheck(_jtJitterWindows == 0, $"{phase}: no levy jitters in place on a foothold of 4 tiles or fewer (jitter windows {_jtJitterWindows}, levies {_jtJitterers.Count})");
			if (phase == "pocket") JtCheck(stuckTiny == 0, $"{phase}: nobody left sealed inside a pocket at the end ({stuckTiny})");
		}

		private bool JtInsidePocket(NPC b)
		{
			foreach (var s in _jtShapes) { float x0 = s.x * 16f, x1 = (s.x + s.w) * 16f; if (b.Center.X > x0 && b.Center.X < x1) return true; }
			return false;
		}
	}
}
