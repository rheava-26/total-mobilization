using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// IDLE SITTING (LEVY_HARNESS_SCENARIO=sit). A flat arena with chairs standing around a Hold post and around a commander who stands still, and armed levies in each group (Hold, Follow).
	/// After SitRunTicks it asserts how many levies sat down, and logs why the others did not. Night variant: LEVY_HARNESS_SITNIGHT=1.
	/// </summary>
	public partial class CombatHarness
	{
		private const int SitRunTicks = 60 * 100;
		private sealed class SitLevy { public string Label; public SoldierRecord Rec; public int Idx; public int SatAt = -1; }
		private readonly List<SitLevy> _sit = new List<SitLevy>();
		private int _sitTick, _sitPass, _sitFail; private bool _sitStarted;

		private void SitCheck(bool ok, string what) { if (ok) _sitPass++; else _sitFail++; Mod.Logger.Info("HARNESS-SIT: " + (ok ? "PASS " : "FAIL ") + what); }

		private bool BeginSit() { _sitTick = 0; _sitStarted = false; _sitPass = _sitFail = 0; _sit.Clear(); return true; }

		private void SitPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = new Vector2((_cx + 40) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			if (Environment.GetEnvironmentVariable("LEVY_HARNESS_SITREAL") == "1")
			{
				// a commander who is busy: still for 25 s, then a short walk 6 tiles out and back (the levies must wait, then settle again)
				int ph = _sitTick % 1500; float off = ph < 90 ? ph * 1.1f : ph < 180 ? (180 - ph) * 1.1f : 0f;
				p.position.X += off; p.velocity.X = ph < 90 ? 1.1f * 60f / 60f * 3f : ph < 180 ? -3f : 0f;
			}
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
		}

		private void SitChair(int x, int dir) { WorldGen.PlaceObject(x, FloorY - 1, TileID.Chairs, true, 0, 0, -1, dir); }

		private SitLevy SitSpawn(string label, int dx, SoldierOrder order)
		{
			var rec = MakeRecord("Pvt. " + label);
			var w = new Item(); w.SetDefaults(ItemID.Handgun);
			if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			rec.LoadoutLocked = true; rec.Order = order; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			Vector2 at = new Vector2((_cx + dx) * 16f, FloorY * 16f);
			if (order == SoldierOrder.Hold) rec.HoldPos = at;
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
			return new SitLevy { Label = label, Rec = rec, Idx = idx };
		}

		private void StepSit()
		{
			if (_sitTick > SitRunTicks + 10) { Finish($"sit scenario complete pass={_sitPass} fail={_sitFail}"); return; }
			SitPinCommander();
			_sitTick++;
			if (!_sitStarted)
			{
				ClearAll(); SitPinCommander();
				if (Environment.GetEnvironmentVariable("LEVY_HARNESS_SITNIGHT") == "1") { Main.dayTime = false; Main.time = 10000; } else { Main.dayTime = true; Main.time = 20000; }
				// chairs around the Hold post (x = cx) and around the commander (x = cx + 40)
				SitChair(_cx - 4, 1); SitChair(_cx + 5, -1); SitChair(_cx + 7, -1); SitChair(_cx + 36, 1); SitChair(_cx + 44, -1); SitChair(_cx + 46, -1);
				for (int i = 0; i < 4; i++) _sit.Add(SitSpawn("Hold" + i, i * 2, SoldierOrder.Hold));
				for (int i = 0; i < 4; i++) _sit.Add(SitSpawn("Follow" + i, 32 + i * 2, SoldierOrder.Follow));
				foreach (var l in _sit) if (l.Idx < 0 || !l.Rec.HasBody) { SitCheck(false, "SpawnBody failed for " + l.Label); Finish($"sit scenario complete pass={_sitPass} fail={_sitFail}"); return; }
				_sitStarted = true; _sitTick = 0;
				return;
			}
			foreach (var l in _sit)
			{
				NPC n = Main.npc[l.Idx]; if (!n.active || !(n.ModNPC is SoldierNPC sn)) continue;
				if (sn.Seated && l.SatAt < 0) { l.SatAt = _sitTick; Mod.Logger.Info($"HARNESS-SIT: {l.Label} sat down at {_sitTick / 60}s"); }
			}
			if (_sitTick % 600 == 0)
				foreach (var l in _sit) { NPC n = Main.npc[l.Idx]; var sn = n.ModNPC as SoldierNPC; Mod.Logger.Info($"HARNESS-SIT: t={_sitTick / 60}s {l.Label} x={(n.Center.X / 16f - _cx):0.0} mode={sn?.SitModeForProbe} why='{sn?.SitWhy}' goal={sn?.HasMoveGoal} state={sn?.State}"); }
			if (_sitTick < SitRunTicks) return;
			Mod.Logger.Info($"HARNESS-SIT: ambient chatter lines said in {SitRunTicks / 60}s by 8 levies: {SoldierNPC.ChatterLines}");
			int hold = 0, follow = 0;
			foreach (var l in _sit) if (l.SatAt >= 0) { if (l.Label.StartsWith("Hold")) hold++; else follow++; }
			SitCheck(hold >= 1, $"at least one Hold levy sat within {SitRunTicks / 60}s ({hold} of 4 did)");
			SitCheck(follow >= 1, $"at least one Follow levy sat beside the still commander within {SitRunTicks / 60}s ({follow} of 4 did)");
			Finish($"sit scenario complete pass={_sitPass} fail={_sitFail}");
		}
	}
}
