using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Flavor;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// MAP ORDER (LEVY_HARNESS_SCENARIO=maporder). Calls the function every map click ends in (<see cref="MapOrder.Issue"/>: Send in single player, the MapOrder packet handler on a server)
	/// for a squad on a flat arena with the stand-in commander parked 150 tiles from the point, then asserts: the right levies took the order, each marches there and arrives within
	/// MoTimeoutTicks, and stays (order Hold, no drifting back to the commander) for MoStayTicks. The click itself (a UI frame) is covered by <see cref="MapOrder.IsPickClick"/> in the boot self-check.
	/// </summary>
	public partial class CombatHarness
	{
		private const float MoNearTiles = 20f; // Hold spreads a squad out around the point (idle slots)
		private const int MoTimeoutTicks = 4200, MoStayTicks = 900, MoIssueTick = 60;
		private sealed class MoLevy { public string Label; public SoldierRecord Rec; public int Idx; public bool Expect; public int ArriveTick = -1; public float StartDist; }
		private readonly List<MoLevy> _mo = new List<MoLevy>();
		private int _moTick, _moPass, _moFail, _moGx, _moGy, _moIssued = -1; private bool _moStarted;
		private int? _moStayStart;

		private void MoCheck(bool ok, string what) { if (ok) _moPass++; else _moFail++; Mod.Logger.Info("HARNESS-MAPORDER: " + (ok ? "PASS " : "FAIL ") + what); }

		private bool BeginMapOrder() { _moTick = 0; _moStarted = false; _moIssued = -1; _moPass = _moFail = 0; _moStayStart = null; _mo.Clear(); return true; }

		private void MoPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = new Vector2((_cx - 70) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
		}

		private MoLevy MoSpawn(string label, int dx, bool armed, SoldierOrder order, bool reserve, bool expect)
		{
			var rec = MakeRecord("Pvt. " + label);
			if (armed)
			{
				var w = new Item(); w.SetDefaults(Terraria.ID.ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			}
			rec.LoadoutLocked = true; rec.Order = order; rec.Reserve = reserve; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2((_cx - 70 + dx) * 16f, FloorY * 16f);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
			return new MoLevy { Label = label, Rec = rec, Idx = idx, Expect = expect };
		}

		private void StepMapOrder()
		{
			if (_moTick > MoIssueTick + MoTimeoutTicks + MoStayTicks + 10) { MoCheck(false, "scenario overran"); Finish($"maporder scenario complete pass={_moPass} fail={_moFail}"); return; }
			MoPinCommander();
			_moTick++;
			if (!_moStarted)
			{
				ClearAll(); MoPinCommander();
				GuideSystem.Instance.Autonomy = true; // the Guide to Autonomy gate (the prompt only appears past it)
				_mo.Add(MoSpawn("Follow", 0, true, SoldierOrder.Follow, false, true));
				_mo.Add(MoSpawn("Hold", 3, true, SoldierOrder.Hold, false, true));
				_mo.Add(MoSpawn("Unarmed", 6, false, SoldierOrder.Follow, false, true));
				_mo.Add(MoSpawn("Patrol", 9, true, SoldierOrder.Patrol, false, true));
				_mo.Add(MoSpawn("Free", 12, true, SoldierOrder.FreeRein, false, true));
				_mo.Add(MoSpawn("Reserve", 15, true, SoldierOrder.Hold, true, false));
				foreach (var l in _mo) if (!l.Rec.Reserve && (l.Idx < 0 || !l.Rec.HasBody)) { MoCheck(false, "SpawnBody failed for " + l.Label); Finish($"maporder scenario complete pass={_moPass} fail={_moFail}"); return; }
				_moGx = _cx + 80; _moGy = FloorY - 3; _moStarted = true; _moTick = 0;
				return;
			}
			if (_moTick == MoIssueTick)
			{
				foreach (var l in _mo) if (l.Idx >= 0) l.StartDist = Math.Abs(Main.npc[l.Idx].Center.X / 16f - _moGx);
				_moIssued = MapOrder.Issue(Main.player[0], _moGx, _moGy); // the code path of the click (single player: MapOrder.Send; server: the MapOrder packet)
				int want = 0; foreach (var l in _mo) if (l.Expect) want++;
				MoCheck(_moIssued == want, $"Issue took {want} levies (Follow/Hold/Unarmed/Patrol/FreeRein, not the Reserve one); got {_moIssued}");
				foreach (var l in _mo) MoCheck(l.Expect == (l.Rec.Order == SoldierOrder.GoTo), $"{l.Label}: record order {(l.Expect ? "is" : "is not")} GoTo (now {l.Rec.Order})");
			}
			if (_moTick <= MoIssueTick) return;
			foreach (var l in _mo)
			{
				if (l.Idx < 0) continue; NPC n = Main.npc[l.Idx]; if (!n.active || !(n.ModNPC is SoldierNPC)) continue;
				if (l.Expect && l.ArriveTick < 0 && Math.Abs(n.Center.X / 16f - _moGx) <= MoNearTiles && Math.Abs(n.Bottom.Y / 16f - FloorY) <= 3f) l.ArriveTick = _moTick - MoIssueTick;
			}
			bool allArrived = true; foreach (var l in _mo) if (l.Expect && l.ArriveTick < 0) allArrived = false;
			if (_moTick % 600 == 0) foreach (var l in _mo) { if (l.Idx < 0) continue; NPC n = Main.npc[l.Idx]; var sn = n.ModNPC as SoldierNPC; Mod.Logger.Info($"HARNESS-MAPORDER: t={_moTick - MoIssueTick} {l.Label} dx={(n.Center.X / 16f - _moGx):0.0}t order={l.Rec.Order} state={sn?.State} arrive={l.ArriveTick} goal={(MapOrder.TryGoal(l.Rec, out int gx, out int gy) ? gx + "," + gy : "none")} vx={n.velocity.X:0.0} hasGoal={sn?.HasMoveGoal}"); }
			if (!_moStayStart.HasValue && (allArrived || _moTick >= MoIssueTick + MoTimeoutTicks)) _moStayStart = _moTick;
			if (!_moStayStart.HasValue || _moTick - _moStayStart.Value < MoStayTicks) return;
			foreach (var l in _mo)
			{
				if (l.Idx < 0) { MoCheck(l.Rec.Order != SoldierOrder.GoTo, l.Label + ": the reserve levy (no body) did not take the order"); continue; }
				NPC n = Main.npc[l.Idx]; var sn = n.ModNPC as SoldierNPC;
				float dx = Math.Abs(n.Center.X / 16f - _moGx);
				if (l.Expect)
				{
					MoCheck(l.ArriveTick >= 0, $"{l.Label}: arrived within {MoTimeoutTicks / 60}s (tick {l.ArriveTick}, started {l.StartDist:0}t away)");
					MoCheck(dx <= MoNearTiles && l.Rec.Order == SoldierOrder.Hold, $"{l.Label}: stayed at the point for {MoStayTicks / 60}s under Hold (dx {dx:0.0}t, order {l.Rec.Order})");
				}
				else MoCheck(l.Rec.Order != SoldierOrder.GoTo, l.Label + ": the reserve levy stayed put");
			}
			Finish($"maporder scenario complete pass={_moPass} fail={_moFail}");
		}
	}
}
