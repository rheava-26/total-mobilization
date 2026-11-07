using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Flavor;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ORDER REPRO (LEVY_HARNESS_SCENARIO=orderrepro). Eve: levies keep following after Return / map orders, and Spread Out looks like normal ranks. Each phase is a fresh squad of 6 on Follow beside the commander:
	/// 0 Return, 1 Return with a boss on the commander, 2 map order with a boss, 3 map order, 4 Return with no barracks (control: falls in behind the commander by design),
	/// 5 Follow in normal ranks, 6 Follow Spread Out, 7 Follow Close Up (spacing is measured at the end: min gap and span between levies).
	/// Logs order/state/goal/distances every 2 s. Self-check: a squad ordered away ends nearer its destination than the commander; Spread Out is wider than normal ranks.
	/// </summary>
	public partial class CombatHarness
	{
		private const int OrPhaseTicks = 1500, OrIssueTick = 90; private static readonly int OrSquad = int.TryParse(Environment.GetEnvironmentVariable("LEVY_OR_N"), out int orn) ? orn : 6;
		private int _orTick, _orPhase, _orPass, _orFail; private bool _orStarted; private BarracksTE _orTe; private int _orBoss = -1; private int _orGx;
		private readonly float[] _orSpan = new float[12], _orMinGap = new float[12];
		private readonly List<SoldierRecord> _orRecs = new List<SoldierRecord>();
		private static readonly string[] OrNames = { "Return", "Return+boss", "GoTo+boss", "GoTo", "Return-nobarracks(control)", "Normal", "SpreadOut", "CloseUp", "SpreadOut+boss", "Normal+boss", "GoTo-wall(teleport check)" };
		private void OrLog(string s) => Mod.Logger.Info("HARNESS-ORDERREPRO: " + s);
		private bool BeginOrderRepro() { _orTick = 0; _orPhase = 0; _orPass = _orFail = 0; _orStarted = false; _orRecs.Clear(); return true; }

		private void OrPin()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = 5000;
			p.position = new Vector2((_cx - 70) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (i != _orBoss && n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
			if (_orBoss >= 0) { NPC b = Main.npc[_orBoss]; if (b.active) { b.life = b.lifeMax = 2000000; b.timeLeft = 99999; if (b.type != NPCID.EyeofCthulhu) OrLog("boss type changed to " + b.type); } else if (_orTick < 400) OrLog("boss inactive at tick " + _orTick); }
		}

		private void OrSetup(int phase)
		{
			ClearAll(); _orBoss = -1; OrPin();
			GuideSystem.Instance.Autonomy = true;
			foreach (var te in TileEntity.ByID.Values.OfType<BarracksTE>().ToList()) { TileEntity.ByPosition.Remove(te.Position); TileEntity.ByID.Remove(te.ID); }
			_orTe = null;
			if (phase < 4)
			{
				int x = _cx - 170, fy = FloorY;
				try { WorldGen.PlaceObject(x, fy - 1, ModContent.TileType<BarracksTile>(), true, 0); } catch { }
				_orTe = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault();
				if (_orTe == null) { try { ModContent.GetInstance<BarracksTE>().Place(x - 1, fy - 3); _orTe = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(); } catch { } }
				if (_orTe == null) OrLog("NO BARRACKS could be placed");
			}
			_orRecs.Clear();
			for (int k = 0; k < OrSquad; k++)
			{
				var rec = MakeRecord("Pvt. R" + k);
				var w = new Item(); w.SetDefaults(ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				rec.SpreadOut = phase == 6 || phase == 8; rec.CloseUp = phase == 7;
				if (_orTe != null) { rec.HomeBarracksId = _orTe.ID; rec.HomePos = _orTe.Position; }
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = new Vector2((_cx - 70 - 2 - k) * 16f, FloorY * 16f); // all stacked near the commander, like the screenshot
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
				_orRecs.Add(rec);
			}
			if (phase == 1 || phase == 2 || phase == 8 || phase == 9)
			{
				_orBoss = BtSpawnDummy((_cx - 70 + (phase >= 8 ? 40 : 10)) * 16f, (FloorY - 6) * 16f); // stationary stand-in main boss (Eye of Cthulhu, no AI)
				OrLog("boss spawned idx=" + _orBoss);
			}
			_orGx = phase == 10 ? _cx - 160 : _cx + 80; _orLastX.Clear(); _orJumps = 0;
		}

		private SoldierState _orPrevState; private int _orStacked, _orJumps; private readonly Dictionary<SoldierRecord, float> _orLastX = new Dictionary<SoldierRecord, float>();
		private void OrSpacing(out float span, out float minGap)
		{
			var xs = _orRecs.Where(r => r.HasBody).Select(r => Main.npc[r.BodyIndex].Center.X / 16f).OrderBy(x => x).ToList();
			span = xs.Count > 1 ? xs[xs.Count - 1] - xs[0] : 0; minGap = float.MaxValue;
			for (int i = 1; i < xs.Count; i++) minGap = Math.Min(minGap, xs[i] - xs[i - 1]);
			if (minGap == float.MaxValue) minGap = 0;
			_orStacked = 0; for (int i = 1; i < xs.Count; i++) if (xs[i] - xs[i - 1] < 1.0f) _orStacked++;
		}

		private void StepOrderRepro()
		{
			if (_orPhase >= OrNames.Length)
			{
				bool ok = _orSpan[6] > _orSpan[5] * 1.3f && _orSpan[8] > _orSpan[9] * 1.3f; OrLog($"SPACING+boss span normal={_orSpan[9]:0.0} spread={_orSpan[8]:0.0}"); if (ok) _orPass++; else _orFail++;
				OrLog($"SPACING span normal={_orSpan[5]:0.0} spread={_orSpan[6]:0.0} close={_orSpan[7]:0.0}; minGap normal={_orMinGap[5]:0.0} spread={_orMinGap[6]:0.0} close={_orMinGap[7]:0.0} -> {(ok ? "PASS" : "FAIL")} spread is wider than normal ranks");
				Finish($"orderrepro scenario complete pass={_orPass} fail={_orFail}"); return;
			}
			_orTick++;
			if (!_orStarted) { OrSetup(_orPhase); _orStarted = true; _orTick = 0; return; }
			OrPin();
			var cmdP = Main.player[0];
			foreach (var r in _orRecs) { if (!r.HasBody) continue; float x = Main.npc[r.BodyIndex].Center.X / 16f; if (_orLastX.TryGetValue(r, out float lx) && Math.Abs(x - lx) > 12f) { _orJumps++; OrLog($"TELEPORT {OrNames[_orPhase]} t={_orTick} {r.Name} order={r.Order} {lx:0}->{x:0} (commander at {cmdP.Center.X / 16f:0})"); } _orLastX[r] = x; }
			bool formation = _orPhase >= 5 && _orPhase < 10;
			if (formation && _orPhase == 6 && _orRecs.Count > 5 && _orRecs[5].HasBody && _orTick > 700)
			{
				var q = (SoldierNPC)Main.npc[_orRecs[5].BodyIndex].ModNPC;
				if (q.State != _orPrevState) { OrLog($"TRANSITION t={_orTick} {_orPrevState}->{q.State} goal={(q.HasMoveGoal ? ((q.MoveGoal.X - cmdP.Center.X) / 16f).ToString("0") : "-")} x={((q.NPC.Center.X - cmdP.Center.X) / 16f):0} dir={cmdP.direction} cmdNull={q.Commander == null} cmdIdx={q.CommanderIndex} active={Main.player[0].active} dead={Main.player[0].dead} order={_orRecs[5].Order} {q.StillReason()}"); _orPrevState = q.State; }
			} if (formation && _orTick < 600) Main.player[0].position.X += 30f * 16f * (float)Math.Sin(_orTick * Math.PI / 300.0);
			if (_orTick == OrIssueTick && !formation)
			{
				bool ret = _orPhase == 0 || _orPhase == 1 || _orPhase == 4;
				if (ret) RosterSystem.Instance.IssueOrder(cmdP, SoldierOrder.ReturnToBarracks, false);
				else OrLog("issued=" + MapOrder.Issue(cmdP, _orGx, FloorY - 3));
				OrLog($"PHASE {OrNames[_orPhase]}: order issued");
			}
			if (_orTick % 120 == 0 && _orTick >= 30)
			{
				foreach (var r in _orRecs)
				{
					if (!r.HasBody) { OrLog($"{OrNames[_orPhase]} t={_orTick} {r.Name} NOBODY order={r.Order}"); continue; }
					NPC n = Main.npc[r.BodyIndex]; var sn = n.ModNPC as SoldierNPC;
					float dC = (n.Center.X - cmdP.Center.X) / 16f, dB = _orTe != null ? (n.Center.X - _orTe.MusterPoint.X) / 16f : float.NaN, dG = (n.Center.X / 16f - _orGx);
					string tgt = sn.TargetIndex >= 0 ? Main.npc[sn.TargetIndex].TypeName : "-";
					string boss = _orBoss >= 0 ? (Main.npc[_orBoss].active ? ((Main.npc[_orBoss].Center.X - cmdP.Center.X) / 16f).ToString("0") + "/" + Main.npc[_orBoss].TypeName : "inactive") : "-";
					OrLog($"{OrNames[_orPhase]} t={_orTick} {r.Name} order={r.Order} state={sn.State} hold={sn.HoldPosition} hasGoal={sn.HasMoveGoal} goalDxCmd={(sn.HasMoveGoal ? ((sn.MoveGoal.X - cmdP.Center.X) / 16f).ToString("0") : "-")} dCmd={dC:0} dBarracks={dB:0} dGoTo={dG:0} tgt={tgt} bossDx={boss} why={sn.StillReason()}");
				}
			}
			if (_orTick >= (formation ? 1500 : OrPhaseTicks))
			{
				if (formation)
				{
					OrSpacing(out _orSpan[_orPhase], out _orMinGap[_orPhase]);
					OrLog("STATES " + OrNames[_orPhase] + ": " + string.Join(" ", _orRecs.Where(r => r.HasBody).Select(r => { var q = (SoldierNPC)Main.npc[r.BodyIndex].ModNPC; return ((Main.npc[r.BodyIndex].Center.X - cmdP.Center.X) / 16f).ToString("0") + ":" + q.State + (q.HasMoveGoal ? ">" + ((q.MoveGoal.X - cmdP.Center.X) / 16f).ToString("0") : "") + ":" + q.StillReason(); })));
					OrLog("OFFSETS " + OrNames[_orPhase] + ": " + string.Join(" ", _orRecs.Where(r => r.HasBody).Select(r => ((Main.npc[r.BodyIndex].Center.X - cmdP.Center.X) / 16f).ToString("0")).OrderBy(x => float.Parse(x))));
					OrLog($"END {OrNames[_orPhase]} n={_orRecs.Count} span={_orSpan[_orPhase]:0.0}t minGap={_orMinGap[_orPhase]:0.0}t levies-within-1-tile-of-a-neighbour={_orStacked}");
				}
				else
				{
					int away = 0;
					foreach (var r in _orRecs)
					{
						if (!r.HasBody) continue; NPC n = Main.npc[r.BodyIndex];
						float dC = Math.Abs(n.Center.X - cmdP.Center.X) / 16f;
						bool ret = _orPhase == 0 || _orPhase == 1;
						float dTarget = ret ? Math.Abs(n.Center.X - _orTe.MusterPoint.X) / 16f : Math.Abs(n.Center.X / 16f - _orGx);
						bool good = _orPhase == 10 ? _orJumps == 0 : _orPhase == 4 ? dC < 40f : dTarget < dC && dTarget < 30f;
						if (good) away++;
						OrLog($"END {OrNames[_orPhase]} {r.Name} order={r.Order} dCmd={dC:0} dTarget={dTarget:0} -> {(good ? "ok" : "BAD")}");
					}
					if (away == _orRecs.Count) _orPass++; else _orFail++;
					OrLog($"jumps={_orJumps}"); OrLog($"PHASE RESULT {OrNames[_orPhase]}: {(away == _orRecs.Count ? "PASS" : "FAIL")} ({away}/{_orRecs.Count})");
				}
				_orPhase++; _orStarted = false;
			}
		}
	}
}
