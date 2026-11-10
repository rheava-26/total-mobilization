using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Levy.Common.Soldiers;
using Levy.Common.Systems;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ORDER PROBE inside a calrev fight (LEVY_CR_ORDERPROBE=1): the real army in a real boss fight is sent every radio order, one every 15 s starting at tick 1200, through the packet handler,
	/// and each living levy is measured for 2 s exactly as in the orderobey scenario (the same OoIssue / OoEvaluate). LEVY_OO_MODE picks the radio mode.
	/// </summary>
	public partial class CombatHarness
	{
		private readonly System.Collections.Generic.HashSet<SoldierRecord> _opDied = new System.Collections.Generic.HashSet<SoldierRecord>();
		private int _opIssued = -1; private bool _opInit, _opDone;
		private void CrOrderProbe(CrState st)
		{
			if (_opDone || Environment.GetEnvironmentVariable("LEVY_CR_ORDERPROBE") != "1") return;
			if (!_opInit)
			{
				_opInit = true; _ooMode = Environment.GetEnvironmentVariable("LEVY_OO_MODE") ?? "radio"; _ooRound = "calrev";
				_ooRadio = _ooMode == "radio" || _ooMode == "radio_near"; _ooAtBarracks = _ooMode == "radio" || _ooMode == "barracks";
				_ooPass = _ooFail = 0; _ooStep = 0; _ooTe = null; _ooBoss = -1; _opDied.Clear();
				if (Environment.GetEnvironmentVariable("LEVY_OP_BARRACKS") != "0")
				{
					foreach (var te0 in Terraria.DataStructures.TileEntity.ByID.Values.OfType<global::Levy.Content.TileEntities.BarracksTE>().ToList()) { Terraria.DataStructures.TileEntity.ByPosition.Remove(te0.Position); Terraria.DataStructures.TileEntity.ByID.Remove(te0.ID); }
					int bx = _crCx - 45, fy = CrF;
					try { Terraria.WorldGen.PlaceObject(bx, fy - 1, Terraria.ModLoader.ModContent.TileType<global::Levy.Content.Tiles.BarracksTile>(), true, 0); } catch (Exception) { }
					_ooTe = Terraria.DataStructures.TileEntity.ByID.Values.OfType<global::Levy.Content.TileEntities.BarracksTE>().FirstOrDefault();
					if (_ooTe == null) { try { Terraria.ModLoader.ModContent.GetInstance<global::Levy.Content.TileEntities.BarracksTE>().Place(bx - 1, fy - 3); _ooTe = Terraria.DataStructures.TileEntity.ByID.Values.OfType<global::Levy.Content.TileEntities.BarracksTE>().FirstOrDefault(); } catch (Exception) { } }
					OoLog("calrev barracks " + (_ooTe == null ? "NOT placed" : "placed at " + _ooTe.Position));
				}
			}
			if (_ooTe != null)
			{
				_ooTe.Manpower = 99;
				foreach (var r in RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName)) { if (r.HomeBarracksId != _ooTe.ID) { r.HomeBarracksId = _ooTe.ID; r.HomePos = _ooTe.Position; } if (r.Dead) _opDied.Add(r); }
			}
			Main.player[0].active = true;
			if (_ooRadio && !global::Levy.Content.Items.CommandRadio.Has(Main.player[0])) Main.player[0].inventory[3].SetDefaults(Terraria.ModLoader.ModContent.ItemType<global::Levy.Content.Items.CommandRadio>());
			const int first = 1200, period = 600;
			int t = st.Tick - first;
			if (t < 0 || _ooStep >= OoOrders.Length) { if (_ooStep >= OoOrders.Length && !_opDone) { _opDone = true; OoLog($"RESULT pass={_ooPass} fail={_ooFail} mode={_ooMode} round=calrev liveAudit[audits={Systems.OrderAudit.Audits} levies={Systems.OrderAudit.Checked} missed={Systems.OrderAudit.Missed}]"); } return; }
			int phase = t % period;
			var (name, kind, order) = OoOrders[_ooStep];
			if (phase == 0)
			{
				var mine = RosterSystem.Instance.Regulars.Where(r => r.CommanderName == CommanderName).ToList();
				OoIssue(name, kind, order, mine, Main.player[0].Center);
				_opIssued = st.Tick;
			}
			else if (phase == OoMeasureTicks && _opIssued >= 0)
			{
				foreach (var l in _oo) if (l.FirstOk < 0 && OoRecOk(l.Rec, kind, order, name)) l.FirstOk = 0;
				OoEvaluate(name, kind, order);
				_ooStep++;
			}
			else if (phase > 0 && phase < OoMeasureTicks)
				foreach (var l in _oo) if (l.FirstOk < 0 && OoRecOk(l.Rec, kind, order, name)) l.FirstOk = phase;
		}
	}
}
