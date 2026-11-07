using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Flavor;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Levy.Common.Config;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ORDER MIX (LEVY_HARNESS_SCENARIO=ordermix). Eve: "some levies ignore every order, some listen". A mixed army follows the commander; a Hold order is issued the way the radio / hotkey does it (atBarracks false), then
	/// the way the barracks panel does (atBarracks true). Each levy reports whether its record and body took the order. Cases: near, mid, far (past OrderRadius), unpaid mutineer, reserve, no body (render cap / waiting to respawn),
	/// unarmed, a levy of another commander, and one homed to a different barracks id.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class OmL { public float DistAtIssue = -1; public bool BodyAtIssue; public string Label; public SoldierRecord Rec; public int Idx = -1; public bool ExpectRadio, ExpectPanel; }
		private readonly List<OmL> _om = new List<OmL>();
		private int _omTick, _omPass, _omFail; private bool _omStarted; private int _omPhase;
		private void OmLog(string s) => Mod.Logger.Info("HARNESS-ORDERMIX: " + s);
		private bool BeginOrderMix() { _omTick = 0; _omStarted = false; _omPhase = 0; _omPass = _omFail = 0; _om.Clear(); return true; }

		private void OmPin()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 5000; p.statLife = 5000;
			p.position = new Vector2((_cx - 70) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
		}

		private OmL OmSpawn(string label, int dx, bool armed, Action<SoldierRecord> tweak, bool body, bool expectRadio, bool expectPanel, string commander = null, int barracksId = -1)
		{
			var rec = MakeRecord("Pvt. " + label);
			if (armed)
			{
				var w = new Item(); w.SetDefaults(ItemID.Handgun);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			}
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (commander != null) rec.CommanderName = commander;
			rec.HomeBarracksId = barracksId;
			tweak?.Invoke(rec);
			RosterSystem.Instance.Regulars.Add(rec);
			var l = new OmL { Label = label, Rec = rec, ExpectRadio = expectRadio, ExpectPanel = expectPanel };
			if (body)
			{
				Vector2 at = new Vector2((_cx - 70 + dx) * 16f, FloorY * 16f);
				l.Idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (l.Idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[l.Idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
			}
			_om.Add(l);
			return l;
		}

		private void OmReport(string phase)
		{
			foreach (var l in _om)
			{
				SoldierNPC b = l.Rec.HasBody ? Main.npc[l.Rec.BodyIndex].ModNPC as SoldierNPC : null;
				float d = b != null ? Math.Abs(b.NPC.Center.X - Main.player[0].Center.X) / 16f : -1f;
				bool took = l.Rec.Order == SoldierOrder.Hold && (b == null || b.HoldPosition);
				bool want = phase == "radio" ? l.ExpectRadio : l.ExpectPanel;
				OmLog($"{phase} {l.Label,-14} distAtIssue={l.DistAtIssue,4:0} body={l.BodyAtIssue} order={l.Rec.Order} bodyHold={(b == null ? "nobody" : b.HoldPosition.ToString())} state={(b == null ? "-" : b.State.ToString())} took={took} expected={want} {(took == want ? "ok" : "MISMATCH")}");
				if (took == want) _omPass++; else _omFail++;
			}
		}

		private void StepOrderMix()
		{
			_omTick++;
			if (!_omStarted)
			{
				ClearAll(); OmPin(); GuideSystem.Instance.Autonomy = true; LevyConfig.Get.WageMutiny = true;
				OmSpawn("near", 10, true, null, true, true, true);
				OmSpawn("mid", 30, true, null, true, true, true);
				OmSpawn("far", 60, true, null, true, false, true);          // past OrderRadius (40 tiles): radio / hotkey skip it
				OmSpawn("unpaid", 14, true, r => r.Mutinous = true, true, false, false);
				OmSpawn("reserve", 16, true, r => r.Reserve = true, false, false, false);
				OmSpawn("nobody", 0, true, r => r.Dead = true, false, true, true);        // on the books, no live body (render cap / waiting to respawn)
				OmSpawn("unarmed", 18, false, null, true, true, true);
				OmSpawn("other-barracks", 20, true, null, true, true, true, null, 9999);
				OmSpawn("other-cmdr", 22, true, null, true, false, false, "SomeoneElse");
				_omStarted = true; _omTick = 0; return;
			}
			OmPin();
			if (_omTick == 119 || _omTick == 479) foreach (var l in _om) { if (l.Label == "unpaid") l.Rec.Mutinous = true; l.BodyAtIssue = l.Rec.HasBody; l.DistAtIssue = l.Rec.HasBody ? Math.Abs(Main.npc[l.Rec.BodyIndex].Center.X - Main.player[0].Center.X) / 16f : -1f; }
			if (_omTick == 120) { RosterSystem.Instance.IssueOrder(Main.player[0], SoldierOrder.Hold, false); OmLog("issued Hold (radio/hotkey, atBarracks=false)"); }
			if (_omTick == 420) { OmReport("radio"); foreach (var l in _om) { l.Rec.Order = SoldierOrder.Follow; if (l.Rec.HasBody) ((SoldierNPC)Main.npc[l.Rec.BodyIndex].ModNPC).ReceiveOrder(SoldierOrder.Follow); } }
			if (_omTick == 480) { RosterSystem.Instance.IssueOrder(Main.player[0], SoldierOrder.Hold, true); OmLog("issued Hold (barracks panel, atBarracks=true)"); }
			if (_omTick == 780) { OmReport("panel"); Finish($"ordermix scenario complete pass={_omPass} fail={_omFail}"); }
		}
	}
}
