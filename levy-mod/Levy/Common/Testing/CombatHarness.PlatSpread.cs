using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Common.Flavor;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// PLATFORM SPREAD (LEVY_HARNESS_SCENARIO=platspread). Eve: "if you have levies, they should try to stand on multiple platforms when they're following you; right now they clump up into a big line".
	/// An arena with four platform rows LEVY_PS_GAP tiles apart over the floor, the stand-in commander on the second row, LEVY_PS_N (30) Follow levies spawned packed on that row (like her screenshot).
	/// Phases (LEVY_PS_PHASES, default "idle,walk,fight,fightend,climb,stagger,short"): idle (commander still), walk (commander paces +-35 tiles, then stops), fight (a stand-in boss in front of the commander),
	/// fightend (a fight for 20 s, then the boss is gone: do they spread out again), climb (the commander hops up a row at 10 s and another at 20 s: do they follow), stagger (the rows are shorter, offset), short (rows of 12 tiles: less room than levies). LEVY_PS_STANCE = normal | spread | close, LEVY_PS_GEAR = bare | mixed (boots, wings, hook round robin).
	/// Measures from sampled positions: rows used, per-row counts, the largest share on one row, levies packed within 1.6 tiles of a same-row neighbour, the closest pair, row changes per levy,
	/// falls of 8+ tiles, teleports, levies left in the air or far away. Logs "pass=N fail=M".
	/// </summary>
	public partial class CombatHarness
	{
		private static readonly int PsN = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PS_N"), out int psn) ? psn : 30;
		private static readonly int PsGap = int.TryParse(Environment.GetEnvironmentVariable("LEVY_PS_GAP"), out int psg) ? psg : 5;
		private static readonly string PsStance = Environment.GetEnvironmentVariable("LEVY_PS_STANCE") ?? "normal";
		private static readonly string PsGearMode = Environment.GetEnvironmentVariable("LEVY_PS_GEAR") ?? "mixed";
		private static readonly string[] PsPhaseNames = (Environment.GetEnvironmentVariable("LEVY_PS_PHASES") ?? "idle,walk,fight,fightend,climb,stagger,short").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		private int _psTick, _psPhase, _psPass, _psFail; private bool _psStarted;
		private readonly List<SoldierRecord> _psRecs = new List<SoldierRecord>();
		private readonly List<int> _psRowY = new List<int>();        // feet-tile y of every standing row, floor first
		private readonly Dictionary<SoldierRecord, int> _psLastRow = new Dictionary<SoldierRecord, int>();
		private readonly Dictionary<SoldierRecord, Vector2> _psLastPos = new Dictionary<SoldierRecord, Vector2>();
		private readonly Dictionary<SoldierRecord, float> _psAirY = new Dictionary<SoldierRecord, float>();
		private int _psBoss = -1, _psCmdRow = 2, _psCmdX; private bool _psWalk;
		private int _psFarLast, _psDrops, _psTrans, _psFalls, _psTele, _psSamples, _psAirSum, _psFarSum, _psPackedSum, _psRowsSum, _psSamplesMeasured; private float _psShareSum, _psMinGap = 99f;
		private int _psRowsMin = 99, _psAirMax, _psWorstTrans;
		private string _psLastCounts = "";
		private void PsLog(string s) => Mod.Logger.Info("HARNESS-PLATSPREAD: " + s);
		private bool BeginPlatSpread() { _psTick = 0; _psPhase = 0; _psPass = _psFail = 0; _psStarted = false; _psRecs.Clear(); return true; }

		private void PsCheck(bool ok, string what) { if (ok) _psPass++; else _psFail++; PsLog((ok ? "PASS " : "FAIL ") + what); }

		/// <summary>Rows: x range of each platform row for a layout. k = 1..4 above the floor.</summary>
		private void PsRowSpan(string layout, int k, out int x0, out int x1)
		{
			x0 = _cx - 90; x1 = _cx + 90;
			if (layout == "stagger") { if (k % 2 == 1) { x0 = _cx - 85; x1 = _cx + 25; } else { x0 = _cx - 25; x1 = _cx + 85; } }
			else if (layout == "short") { x0 = _cx + (k - 2) * 14 - 6; x1 = x0 + 12; }
		}

		private void PsBuildArena(string layout)
		{
			for (int x = _cx - 95; x <= _cx + 95; x++)
				for (int y = ClearTop; y < FloorY; y++) { Tile t = Main.tile[x, y]; t.HasTile = false; t.TileType = 0; t.WallType = 0; t.LiquidAmount = 0; }
			_psRowY.Clear(); _psRowY.Add(FloorY - 1);
			for (int k = 1; k <= 4; k++)
			{
				int py = FloorY - PsGap * k; PsRowSpan(layout, k, out int x0, out int x1);
				for (int x = x0; x <= x1; x++)
				{
					Tile t = Main.tile[x, py];
					t.HasTile = true; t.TileType = TileID.Platforms; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.WallType = 0;
				}
				_psRowY.Add(py - 1);
			}
		}

		private void PsPin(string phase)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = 5000;
			float ox = 0f; int dir = 1;
			if (_psWalk && _psTick >= 300 && _psTick < 1500) { double ph = (_psTick - 300) * Math.PI / 600.0; ox = (float)(35.0 * Math.Sin(ph)) * 16f; dir = Math.Cos(ph) >= 0 ? 1 : -1; }
			if (phase == "climb") _psCmdRow = _psTick < 600 ? 2 : _psTick < 1200 ? 3 : 4;
			if (phase == "fightend" && _psTick == 1200 && _psBoss >= 0) { Main.npc[_psBoss].active = false; _psBoss = -1; PsLog("boss removed"); }
			p.position = new Vector2(_psCmdX * 16f + ox - p.width / 2f, (_psRowY[_psCmdRow] + 1) * 16f - p.height); p.velocity = Vector2.Zero; p.direction = dir;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (i != _psBoss && n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
			if (_psBoss >= 0) { NPC b = Main.npc[_psBoss]; if (b.active) { b.life = b.lifeMax = 2000000; b.timeLeft = 99999; } }
		}

		private string PsGearOf(int i)
		{
			if (PsGearMode == "bare") return "bare";
			string[] g = { "bare", "boots", "wings", "hook" }; return g[i % g.Length];
		}

		private void PsSetup(string phase)
		{
			ClearAll(); _psBoss = -1;
			string layout = phase == "stagger" ? "stagger" : phase == "short" ? "short" : "full";
			PsBuildArena(layout);
			GuideSystem.Instance.Autonomy = true;
			_psWalk = phase == "walk"; _psCmdRow = 2; _psCmdX = layout == "stagger" ? _cx + 30 : layout == "short" ? _cx : _cx;
			PsPin(phase);
			_psRecs.Clear(); _psLastRow.Clear(); _psLastPos.Clear(); _psAirY.Clear();
			FollowRows.Reset(); SoldierNPC.FrPicksS = SoldierNPC.FrBreaksS = SoldierNPC.FrFailsS = 0;
			_psFarLast = 0; _psDrops = _psTrans = _psFalls = _psTele = _psSamples = _psAirSum = _psFarSum = _psPackedSum = _psRowsSum = _psSamplesMeasured = 0; _psShareSum = 0f; _psMinGap = 99f; _psRowsMin = 99; _psAirMax = 0; _psWorstTrans = 0;
			for (int k = 0; k < PsN; k++)
			{
				var rec = MakeRecord("Pvt. P" + k);
				var w = new Item(); w.SetDefaults(ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
				string g = PsGearOf(k);
				if (g == "boots") { var it = new Item(); it.SetDefaults(ItemID.HermesBoots); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "wings") { var it = new Item(); it.SetDefaults(ItemID.AngelWings); rec.Gear.Accessories[0] = it; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				if (g == "hook") { var it = new Item(); it.SetDefaults(ItemID.GrapplingHook); rec.Gear.Hook = it; rec.Gear.SetLocked(SoldierGear.SlotHook, true); }
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; Formation.Apply(rec, PsStance == "spread" ? FormationMode.Spread : PsStance == "close" ? FormationMode.CloseUp : FormationMode.Normal); rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				float sx = _psCmdX - 3 - k * 0.5f; if (layout == "short") sx = _psCmdX - 4 + (k % 8);
				Vector2 at = new Vector2(sx * 16f, (_psRowY[_psCmdRow] + 1) * 16f); // all packed on the commander's row, like the screenshot
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
				_psRecs.Add(rec);
			}
			if (phase == "fight" || phase == "fightend") { _psBoss = BtSpawnDummy((_psCmdX + 22) * 16f, (_psRowY[_psCmdRow] - 6) * 16f); PsLog("boss spawned idx=" + _psBoss); }
			PsLog($"PHASE {phase}: layout={layout} gap={PsGap} rows(feetY)={string.Join(",", _psRowY)} commander row {_psCmdRow} n={_psRecs.Count} gear={PsGearMode}");
		}

		private int PsRowIndexOfY(int fy) { for (int r = 0; r < _psRowY.Count; r++) if (Math.Abs(fy - _psRowY[r]) <= 1) return r; return -1; }
		private int PsRowOf(NPC n)
		{
			int fy = (int)((n.Bottom.Y - 1f) / 16f);
			for (int r = 0; r < _psRowY.Count; r++) if (Math.Abs(fy - _psRowY[r]) <= 1) return r;
			return -1;
		}

		private void PsSample(bool measure)
		{
			Player cmd = Main.player[0];
			var counts = new int[_psRowY.Count]; int air = 0, far = 0, n = 0;
			var perRowX = new List<float>[_psRowY.Count]; for (int r = 0; r < perRowX.Length; r++) perRowX[r] = new List<float>();
			foreach (var rec in _psRecs)
			{
				if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; if (!b.active) continue; n++;
				bool still = b.velocity.Y == 0f;
				int row = still ? PsRowOf(b) : -2;
				if (row >= 0) { counts[row]++; perRowX[row].Add(b.Center.X / 16f); } else air++;
				if (Math.Abs(b.Center.X - cmd.Center.X) / 16f > 45f || Math.Abs(b.Center.Y - cmd.Center.Y) / 16f > _psRowY.Count * (PsGap + 2)) far++;
				if (measure && row >= 0)
				{
					if (_psLastRow.TryGetValue(rec, out int lr) && lr != row) _psTrans++;
					_psLastRow[rec] = row;
				}
			}
			if (!measure) return;
			int used = counts.Count(c => c > 0), maxc = counts.Length > 0 ? counts.Max() : 0, packed = 0; float minGap = 99f;
			for (int r = 0; r < perRowX.Length; r++)
			{
				var xs = perRowX[r]; xs.Sort();
				for (int i = 0; i < xs.Count; i++)
				{
					float gl = i > 0 ? xs[i] - xs[i - 1] : 99f, gr = i + 1 < xs.Count ? xs[i + 1] - xs[i] : 99f;
					if (Math.Min(gl, gr) < 1.6f) packed++;
					if (gr < minGap) minGap = gr;
				}
			}
			_psSamplesMeasured++; _psRowsSum += used; _psRowsMin = Math.Min(_psRowsMin, used); _psShareSum += n > 0 ? maxc / (float)n : 0f; _psPackedSum += packed; _psAirSum += air; _psAirMax = Math.Max(_psAirMax, air); _psFarSum += far; _psFarLast = far; _psMinGap = Math.Min(_psMinGap, minGap);
			_psLastCounts = string.Join("/", counts);
		}

		private void StepPlatSpread()
		{
			if (_psPhase >= PsPhaseNames.Length) { Finish($"platspread scenario complete pass={_psPass} fail={_psFail}"); return; }
			string phase = PsPhaseNames[_psPhase];
			_psTick++;
			if (!_psStarted) { PsSetup(phase); _psStarted = true; _psTick = 0; return; }
			PsPin(phase);
			int measureFrom = phase == "walk" ? 1800 : phase == "fightend" ? 1900 : phase == "climb" ? 1500 : 900, total = phase == "walk" ? 2700 : phase == "fight" ? 2400 : phase == "fightend" ? 2700 : phase == "climb" ? 2400 : 1800;
			// per-tick watchers: teleports and falls
			foreach (var rec in _psRecs)
			{
				if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; Vector2 c = b.Center;
				if (_psLastPos.TryGetValue(rec, out Vector2 lp) && (Math.Abs(c.X - lp.X) > 6 * 16f || Math.Abs(c.Y - lp.Y) > 6 * 16f)) { _psTele++; { var tsn = b.ModNPC as SoldierNPC; PsLog($"TELEPORT {phase} t={_psTick} {rec.Name} {(lp.X / 16f):0},{(lp.Y / 16f):0} -> {(c.X / 16f):0},{(c.Y / 16f):0} assignedY={(tsn == null || tsn.FollowRowY == int.MinValue ? "none" : tsn.FollowRowY.ToString())} state={tsn?.State} goal={(tsn != null && tsn.HasMoveGoal ? ((tsn.MoveGoal.X / 16f) - _cx).ToString("0") + "," + (tsn.MoveGoal.Y / 16f).ToString("0") : "-")} why={tsn?.StillReason()}"); } }
				_psLastPos[rec] = c;
				bool airborne = b.velocity.Y != 0f;
				if (airborne) { if (!_psAirY.ContainsKey(rec)) _psAirY[rec] = b.Bottom.Y; }
				else if (_psAirY.TryGetValue(rec, out float ay))
				{
					if ((b.Bottom.Y - ay) / 16f >= 8f)
					{
						int land = (int)((b.Bottom.Y - 1f) / 16f), mine = (b.ModNPC as SoldierNPC)?.FollowRowY ?? int.MinValue;
						bool deliberate = PsRowIndexOfY(land) >= 0; // dropped onto a row of the arena (the next row down, or the floor); only a landing off every row is a fall
						if (deliberate) _psDrops++; else { _psFalls++; PsLog($"FALL {phase} t={_psTick} {rec.Name} {(b.Bottom.Y - ay) / 16f:0} tiles, landed row y={land}, assigned row y={(mine == int.MinValue ? "none" : mine.ToString())}"); }
					}
					_psAirY.Remove(rec);
				}
			}
			if (_psTick % 30 == 0) PsSample(_psTick >= measureFrom || (phase == "walk" && _psTick >= 600 && _psTick < 1500));
			if (_psTick % 300 == 0) PsLog($"{phase} t={_psTick} rowCounts(floor..top)={_psLastCounts} rows={PsRowCountNow()}");
			if (_psTick >= total) { PsEndPhase(phase); _psPhase++; _psStarted = false; }
		}

		private int PsRowCountNow() { int used = 0; var seen = new HashSet<int>(); foreach (var rec in _psRecs) { if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; if (b.velocity.Y == 0f) { int r = PsRowOf(b); if (r >= 0 && seen.Add(r)) used++; } } return used; }

		private void PsEndPhase(string phase)
		{
			int m = Math.Max(1, _psSamplesMeasured); float rowsMean = _psRowsSum / (float)m, share = _psShareSum / m, packedFrac = _psPackedSum / (float)(m * Math.Max(1, _psRecs.Count)), airMean = _psAirSum / (float)m;
			float transPerLevy = _psTrans / (float)Math.Max(1, _psRecs.Count);
			bool fightPhase = phase == "fight";
			float airFrac = airMean / Math.Max(1, _psRecs.Count);
			int rowsAvail = phase == "short" ? 4 : 5;
			foreach (var rec in _psRecs)
			{
				if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; var sn = b.ModNPC as SoldierNPC; int fy = (int)((b.Bottom.Y - 1f) / 16f);
				if (phase != "fight" && sn != null && (sn.FollowRowY == int.MinValue || Math.Abs(fy - sn.FollowRowY) > 1)) PsLog($"OFFROW {phase} {rec.Name} feetY={fy} x={(b.Center.X / 16f - _cx):0} assignedY={(sn.FollowRowY == int.MinValue ? "none" : sn.FollowRowY.ToString())} state={sn.State} goal={(sn.HasMoveGoal ? ((sn.MoveGoal.X / 16f) - _cx).ToString("0") + "," + (sn.MoveGoal.Y / 16f).ToString("0") : "-")} vy={b.velocity.Y:0.0} why={sn.StillReason()}");
			}
			{
				var sb = new System.Text.StringBuilder(); float cxx = Main.player[0].Center.X / 16f;
				for (int r = 0; r < _psRowY.Count; r++)
				{
					var xs = new List<float>(); foreach (var rec in _psRecs) { if (!rec.HasBody) continue; NPC b = Main.npc[rec.BodyIndex]; if (b.velocity.Y == 0f && PsRowOf(b) == r) xs.Add(b.Center.X / 16f - cxx); }
					xs.Sort(); sb.Append($" r{r}[").Append(string.Join(" ", xs.Select(x => x.ToString("0.0")))).Append("]");
				}
				PsLog($"LANES {phase} (x offset from the commander, floor first):{sb}");
			}
			PsLog($"COST {phase}: row scans={FollowRows.Rebuilds} ({FollowRows.RebuildMs / Math.Max(1, FollowRows.Rebuilds):0.000} ms each, {FollowRows.TilesRead / Math.Max(1, FollowRows.Rebuilds)} columns-rows read each, {_psTick} ticks, {_psRecs.Count} levies); claims made={SoldierNPC.FrPicksS} broken={SoldierNPC.FrBreaksS} rows struck off as unreachable={SoldierNPC.FrFailsS}");
			PsLog($"RESULT {phase}: n={_psRecs.Count} samples={_psSamplesMeasured} rowsUsed mean={rowsMean:0.00} min={_psRowsMin} of {rowsAvail}; maxRowShare={share:0.00}; packed(<1.6t)={packedFrac:0.00}; closestPair={_psMinGap:0.0}t; rowChanges/levy={transPerLevy:0.00}; airborne mean={airMean:0.0} max={_psAirMax}; farOrStranded mean={_psFarSum / (float)m:0.0}; falls8+(landed off every row)={_psFalls}; drops8+(onto a row)={_psDrops}; teleports={_psTele}; lastCounts(floor..top)={_psLastCounts}");
			PsCheck(rowsMean >= 2.0f, $"{phase}: levies stand on >= 2 rows (mean {rowsMean:0.00})");
			if (!fightPhase && phase != "short" && phase != "fightend") PsCheck(share <= 0.55f, $"{phase}: no row holds more than 55% (max share {share:0.00})");
			if (phase == "fightend") { PsCheck(rowsMean >= 3.0f && share <= 0.6f, $"{phase}: after the boss is gone the army spreads out again (rows {rowsMean:0.00} >= 3, max share {share:0.00} <= 0.60)"); PsCheck(packedFrac <= 0.15f, $"{phase}: <= 15% packed after the fight (got {packedFrac:0.00})"); }
			if (phase == "climb") PsCheck(share <= 0.6f, $"{phase}: no row holds more than 60% after the commander climbs (max share {share:0.00})");
			if (phase == "short") { PsCheck(share <= 0.75f, $"{phase}: short rows: no row holds more than 75% (max share {share:0.00})"); PsCheck(packedFrac <= 0.20f, $"{phase}: short rows hold fewer levies than there are: <= 20% packed (got {packedFrac:0.00})"); }
			if (!fightPhase && phase != "short") PsCheck(packedFrac <= 0.10f, $"{phase}: <= 10% of levies packed within 1.6 tiles of a neighbour (got {packedFrac:0.00})");
			if (!fightPhase && phase != "climb" && phase != "fightend") PsCheck(transPerLevy <= 0.5f, $"{phase}: stable, row changes per levy {transPerLevy:0.00} <= 0.5");
			PsCheck(_psFarLast == 0 && airFrac <= (fightPhase ? 0.35f : 0.15f), $"{phase}: nobody stranded or far at the end (far={_psFarLast}), few in the air (mean {airFrac:0.00})");
			PsCheck(_psFalls == 0 && _psTele == 0, $"{phase}: no falls of 8+ tiles off every row ({_psFalls}) and no teleports ({_psTele})");
			if (phase == "climb") PsCheck(transPerLevy <= 1.2f, $"{phase}: the army follows the commander up without churning (row changes per levy {transPerLevy:0.00} <= 1.2)");
		}
	}
}
