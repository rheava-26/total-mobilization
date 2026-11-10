using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Systems
{
	/// <summary>
	/// Boot self-check "[OrderObey] self-check pass=N fail=M" (dedicated server boots; LEVY_ORDEROBEYCHECK=0 skips, =1 forces it on a client).
	/// Eve (2.0.43): "soldiers refuse orders unless they die first". Every order the radio offers is given through the same method the radial, the hotkeys and the packet handler end in (<see cref="RosterSystem.IssueOrder"/>,
	/// <see cref="RosterSystem.ToggleHoldFire"/>, <see cref="RosterSystem.ToggleSpreadOut"/>) to real bodies standing in every posture a living levy can be in, and each must end up holding the order exactly as a freshly respawned one does.
	/// </summary>
	public class OrderObeySelfCheck : ModSystem
	{
		private int pass, fail;
		private void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[OrderObey] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void PostSetupRecipes() => LoadSafe.Run(Run);

		private static readonly SoldierOrder[] Orders = { SoldierOrder.Follow, SoldierOrder.Hold, SoldierOrder.ReturnToBarracks, SoldierOrder.SearchDestroy, SoldierOrder.FreeRein };
		// postures an idle, fighting, walking, hauling, resupplying or sitting body can be in; Shaken / Routed / Desert belong to Fear and the roster
		private static readonly SoldierState[] Calm = { SoldierState.Idle, SoldierState.Follow, SoldierState.Patrol, SoldierState.Engage, SoldierState.Haul, SoldierState.Resupply, SoldierState.ReturnHome, SoldierState.SearchDestroy, SoldierState.FreeRein };
		private static readonly SoldierState[] Owned = { SoldierState.Shaken, SoldierState.Routed, SoldierState.Desert };

		private void Run()
		{
			string env = Environment.GetEnvironmentVariable("LEVY_ORDEROBEYCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			pass = fail = 0;
			var roster = RosterSystem.Instance;
			int slot = Main.maxNPCs - 2;
			NPC npc = Main.npc[slot];
			bool wasActive = npc.active; int wasWho = npc.whoAmI;
			Player cmd = Main.player[0];
			bool cmdActive = cmd.active; string cmdName = cmd.name; Vector2 cmdPos = cmd.position;
			int nextId = roster.NextId;
			var added = new List<SoldierRecord>();
			try
			{
				cmd.active = true; cmd.name = "OrderObeyCheck"; cmd.position = new Vector2(800f * 16f, 400f * 16f);
				var rec = new SoldierRecord { Id = roster.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. OrderCheck", CommanderName = cmd.name, CommanderIndex = 0, LifeCrystals = 0, ManaCrystals = 0, HomeBarracksId = -1 };
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
				roster.Regulars.Add(rec); added.Add(rec);

				npc.SetDefaults(ModContent.NPCType<SoldierNPC>());
				npc.whoAmI = slot; npc.active = true; npc.position = cmd.position + new Vector2(10f * 16f, 0f);
				var body = npc.ModNPC as SoldierNPC;
				if (body == null) { Check("a levy body can be made", false, "ModNPC is " + npc.ModNPC); return; }
				body.Bind(rec);
				npc.position = cmd.position + new Vector2(10f * 16f, 0f);
				Check("1 the levy body is bound to its record", rec.HasBody && ReferenceEquals(body.Record, rec));

				// 1. every order x every calm posture: the same end state a respawned levy has
				int bad = 0; string firstBad = "";
				foreach (var pre in Calm)
					foreach (var order in Orders)
					{
						Reset(rec, body, pre, seated: false);
						roster.IssueOrder(cmd, order, true);
						if (!Took(rec, body, order, out string why)) { bad++; if (firstBad.Length == 0) firstBad = $"{pre} -> {order}: {why}"; }
					}
				Check($"1 every order puts a body in any calm posture ({Calm.Length} x {Orders.Length}) in the order's state, holding or not", bad == 0, firstBad);

				// 2. a sitting body stands up for every order
				bad = 0; firstBad = "";
				foreach (var order in Orders)
				{
					Reset(rec, body, SoldierState.Idle, seated: true);
					bool wasSeated = body.Seated;
					roster.IssueOrder(cmd, order, true);
					string why = null;
					if (!wasSeated || body.Seated || !Took(rec, body, order, out why)) { bad++; if (firstBad.Length == 0) firstBad = $"seated={wasSeated} -> {order}: seatedNow={body.Seated} {why}"; }
				}
				Check("2 a seated levy is on its feet and holding the order after each one", bad == 0, firstBad);

				// 3. Shaken / Routed / Desert keep their label (fear owns it) but the record always holds the order, so the body acts on it the moment fear lets go
				bad = 0; firstBad = "";
				foreach (var pre in Owned)
					foreach (var order in Orders)
					{
						Reset(rec, body, pre, seated: false);
						roster.IssueOrder(cmd, order, true);
						if (rec.Order != order || body.State != pre || !body.ObeysOrder(order, out string why)) { bad++; if (firstBad.Length == 0) firstBad = $"{pre} -> {order}: record {rec.Order}, state {body.State}"; }
					}
				Check("3 a Shaken, Routed or deserting levy still records every order and keeps its label", bad == 0, firstBad);

				// 4. an order given twice, in any sequence, ends in the last one
				bad = 0; firstBad = "";
				foreach (var a in Orders)
					foreach (var b in Orders)
					{
						Reset(rec, body, SoldierState.Engage, seated: false);
						roster.IssueOrder(cmd, a, true); roster.IssueOrder(cmd, b, true);
						if (!Took(rec, body, b, out string why)) { bad++; if (firstBad.Length == 0) firstBad = $"{a} then {b}: {why}"; }
					}
				Check("4 every order after every other order ends in the last one given", bad == 0, firstBad);

				// 5. reach: the barracks panel and the radio reach every levy, the hotkey only the levies within OrderRadius of the commander
				Reset(rec, body, SoldierState.Follow, seated: false);
				npc.position = cmd.position + new Vector2((LevyConstants.OrderRadius + 50f) * 16f, 0f);
				roster.IssueOrder(cmd, SoldierOrder.Hold, false);
				bool farUntouched = rec.Order == SoldierOrder.Follow;
				roster.IssueOrder(cmd, SoldierOrder.Hold, true);
				bool farPanel = rec.Order == SoldierOrder.Hold && body.HoldPosition;
				Check("5 beyond the hotkey radius only the radio / panel reach a levy", farUntouched && farPanel, $"hotkey left it {(farUntouched ? "alone" : "ordered")}, panel {(farPanel ? "reached it" : "missed it")}");
				npc.position = cmd.position + new Vector2(10f * 16f, 0f);
				Reset(rec, body, SoldierState.Follow, seated: false);
				roster.IssueOrder(cmd, SoldierOrder.Hold, false);
				Check("5 within the radius the hotkey reaches it", rec.Order == SoldierOrder.Hold && body.HoldPosition);

				// 6. someone else's levy, a reserve levy and a levy with no body: left alone / recorded
				Reset(rec, body, SoldierState.Follow, seated: false);
				string owner = rec.CommanderName; rec.CommanderName = "SomeoneElse";
				roster.IssueOrder(cmd, SoldierOrder.Hold, true);
				bool otherUntouched = rec.Order == SoldierOrder.Follow;
				rec.CommanderName = owner; rec.Reserve = true;
				roster.IssueOrder(cmd, SoldierOrder.Hold, true);
				bool reserveUntouched = rec.Order == SoldierOrder.Follow;
				rec.Reserve = false;
				Check("6 another commander's levy and a reserve levy take no order", otherUntouched && reserveUntouched);

				// 7. Hold Fire and Spread Out toggle every reached levy
				rec.HoldFire = false; rec.SpreadOut = rec.CloseUp = false;
				roster.ToggleHoldFire(cmd, true); bool on = rec.HoldFire;
				roster.ToggleHoldFire(cmd, true); bool off = !rec.HoldFire;
				Check("7 Hold Fire holds, then releases", on && off);
				roster.ToggleSpreadOut(cmd, true); bool spread = rec.SpreadOut;
				roster.ToggleSpreadOut(cmd, true); bool close = rec.CloseUp && !rec.SpreadOut;
				roster.ToggleSpreadOut(cmd, true); bool normal = !rec.SpreadOut && !rec.CloseUp;
				Check("7 Spread Out cycles normal -> spread -> close up -> normal", spread && close && normal);

				// 8. the audit rule itself: a body left in the wrong posture is reported, a right one is not
				Reset(rec, body, SoldierState.Follow, seated: false);
				roster.IssueOrder(cmd, SoldierOrder.ReturnToBarracks, true);
				body.State = SoldierState.Follow; // the order never reached the body (the failure the audit exists to catch)
				bool caught = !body.ObeysOrder(SoldierOrder.ReturnToBarracks, out _);
				Reset(rec, body, SoldierState.Follow, seated: false);
				roster.IssueOrder(cmd, SoldierOrder.Hold, true);
				Check("8 the audit flags a body that kept its old posture and passes one that took the order", caught && body.ObeysOrder(SoldierOrder.Hold, out _));

				// 10. world load: tile-entity ids are renumbered on load (or not in yet), so the saved barracks id is stale; the levy must still be matched to its barracks by position and obey Return
				{
					var te = new Content.TileEntities.BarracksTE { Position = new Terraria.DataStructures.Point16(700, 300) };
					te.ID = 9001; Terraria.DataStructures.TileEntity.ByID[te.ID] = te; Terraria.DataStructures.TileEntity.ByPosition[te.Position] = te;
					try
					{
						Reset(rec, body, SoldierState.Follow, seated: false);
						rec.HomePos = te.Position; rec.HomeBarracksId = -1; // as a load leaves it
						roster.IssueOrder(cmd, SoldierOrder.ReturnToBarracks, true);
						bool a = rec.HomeBarracksId == te.ID && body.Home == te && Took(rec, body, SoldierOrder.ReturnToBarracks, out _);
						rec.HomeBarracksId = 4242; // a stale id naming nothing
						roster.IssueOrder(cmd, SoldierOrder.ReturnToBarracks, true);
						bool b2 = rec.HomeBarracksId == te.ID && body.Home == te;
						Check("10 after a world load the levy is matched to its barracks by position and Return reaches it", a && b2, $"homeId={rec.HomeBarracksId} expected {te.ID}");
					}
					finally { Terraria.DataStructures.TileEntity.ByID.Remove(te.ID); Terraria.DataStructures.TileEntity.ByPosition.Remove(te.Position); rec.HomePos = Terraria.DataStructures.Point16.NegativeOne; rec.HomeBarracksId = -1; }
				}

				// 9. the packet handlers are registered
				Check("9 Order, HoldFire and SpreadOut packets have handlers", Net.LevyPackets.IsRegistered(Net.LevyPacket.Order) && Net.LevyPackets.IsRegistered(Net.LevyPacket.HoldFire) && Net.LevyPackets.IsRegistered(Net.LevyPacket.SpreadOut));
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			finally
			{
				foreach (var r in added) roster.Regulars.Remove(r);
				roster.NextId = nextId;
				npc.active = false; npc.whoAmI = wasWho; if (wasActive) npc.active = true;
				cmd.active = cmdActive; cmd.name = cmdName; cmd.position = cmdPos;
				OrderAudit.Audits = OrderAudit.Checked = OrderAudit.Missed = 0;
			}
			Mod.Logger.Info($"[OrderObey] self-check pass={pass} fail={fail}");
		}

		private static void Reset(SoldierRecord rec, SoldierNPC body, SoldierState pre, bool seated)
		{
			rec.Order = SoldierOrder.Follow; rec.HoldFire = false; rec.StoodDown = false;
			body.HoldPosition = false; body.State = pre; body.TargetIndex = pre == SoldierState.Engage ? 0 : -1;
			SetSeat(body, seated);
		}

		/// <summary>Seats the body the way the Sit module does (private state), or stands it up.</summary>
		private static void SetSeat(SoldierNPC body, bool seated)
		{
			var t = typeof(SoldierNPC); var f = BindingFlags.Instance | BindingFlags.NonPublic;
			t.GetField("_sitMode", f)?.SetValue(body, seated ? 2 : 0);
			t.GetField("_sitNet", f)?.SetValue(body, false);
		}

		private static bool Took(SoldierRecord rec, SoldierNPC body, SoldierOrder order, out string why)
		{
			why = null;
			if (rec.Order != order) { why = "record " + rec.Order; return false; }
			if (rec.StoodDown != (order == SoldierOrder.ReturnToBarracks)) { why = "StoodDown " + rec.StoodDown; return false; }
			if (body.HoldPosition != (order == SoldierOrder.Hold)) { why = "HoldPosition " + body.HoldPosition; return false; }
			if (body.Seated) { why = "still seated"; return false; }
			if (body.State != SoldierNPC.StateAfterOrder(order)) { why = "state " + body.State; return false; }
			if (body.TargetIndex != -1) { why = "kept target " + body.TargetIndex; return false; }
			return true;
		}
	}
}
