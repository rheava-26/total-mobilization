using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// ORDER AUDIT. A few seconds after every order the commander gives (radial, radio, hotkey or barracks panel; one code path, <see cref="RosterSystem.IssueOrder"/>), every levy that was reached is looked at again: does its record hold the order, and does
	/// its body stand in a posture the order allows (<see cref="SoldierNPC.ObeysOrder"/>)? The count goes to the log as "[OrderObey] audit ...", and every levy that did not take it is written out with everything that can hold a body back
	/// (state, fight, fear, weapon, commander, home, goal, why it stands still), so a report of "they ignore my orders" arrives with the reason attached. Server / single player; costs one pass over the reached levies once per order.
	/// </summary>
	public class OrderAudit : ModSystem
	{
		private struct Pending { public SoldierOrder Order; public uint Due; public List<SoldierRecord> Picked; public string Who; }
		private static readonly List<Pending> _pending = new();
		/// <summary>Orders audited, levies checked, levies that did not take the order (harness and boot self-check read these).</summary>
		public static int Audits, Checked, Missed;
		/// <summary>Ticks between an order and its audit: long enough for a think and a walk (about 3 s), shorter than a levy needs to sit down again.</summary>
		public const int AuditDelayTicks = 180;

		internal static void Schedule(string who, SoldierOrder order, List<SoldierRecord> picked)
		{
			if (picked == null || picked.Count == 0) return;
			_pending.RemoveAll(x => x.Who == who && x.Order == order); // the same order repeated: audit the latest
			_pending.Add(new Pending { Order = order, Due = Main.GameUpdateCount + (uint)AuditDelayTicks, Picked = picked, Who = who });
		}

		public override void OnWorldUnload() => _pending.Clear();

		public override void PostUpdateEverything()
		{
			if (_pending.Count == 0 || !LevyUtil.IsServerOrSingle) return;
			for (int i = _pending.Count - 1; i >= 0; i--)
			{
				var p = _pending[i];
				if (Main.GameUpdateCount < p.Due) continue;
				_pending.RemoveAt(i);
				try { Evaluate(p); } catch (System.Exception e) { Mod.Logger.Warn("[OrderObey] audit failed: " + e.Message); }
			}
		}

		private void Evaluate(Pending p)
		{
			int bodies = 0, obeyed = 0, missed = 0, shown = 0;
			foreach (var rec in p.Picked)
			{
				if (rec == null || rec.Order != p.Order || !rec.HasBody) continue; // a newer order replaced this one (it has its own audit), or there is no body to look at
				if (Main.npc[rec.BodyIndex].ModNPC is not SoldierNPC b) continue;
				bodies++;
				if (b.ObeysOrder(p.Order, out string why)) { obeyed++; continue; }
				missed++;
				if (shown++ < 6) Mod.Logger.Warn($"[OrderObey] MISS {b.SoldierName}: {why} | {b.OrderDiagnosis()}");
			}
			Audits++; Checked += bodies; Missed += missed;
			Mod.Logger.Info($"[OrderObey] audit {p.Order}: reached={p.Picked.Count} bodies={bodies} obeyed={obeyed} missed={missed}");
		}
	}
}
