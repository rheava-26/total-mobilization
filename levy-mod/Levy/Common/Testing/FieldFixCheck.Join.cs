using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Content.NPCs;
using Levy.Content.TileEntities;

namespace Levy.Common.Testing
{
	public partial class FieldFixCheck
	{
		// =====================================================================================================
		// BOSS JOIN
		// =====================================================================================================
		private sealed class JoinResult
		{
			public long Dmg; public float MinTiles = float.MaxValue; public int Ticks, EngagedTicks; public readonly Dictionary<SoldierState, int> States = new();
			public string StateText() => string.Join(" ", States.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + ":" + (100 * kv.Value / Math.Max(1, Ticks)) + "%"));
		}

		/// <summary>
		/// The random stream a trial draws from, fixed per trial: a shot's spread and a think's jitter come from Main.rand, so the same trial replays the same way whatever else the box is doing.
		/// </summary>
		private static void Seed(int seed) { Main.rand = new Terraria.Utilities.UnifiedRandom(seed); }

		/// <summary>A claim that a body hurts a boss is a claim about a hit landing, not about it landing inside a fixed 12 seconds: the window is the minimum, and a trial that has not seen a hit by then runs on to this many windows before it gives up.</summary>
		private const int HitBudgetFactor = 5;

		/// <summary>One body against one pinned boss: how close it gets, whether it hurts the boss, and which states it spends the time in. With <paramref name="untilHit"/> the trial runs on past the window until the first hit.</summary>
		private static JoinResult Join(string label, float bossTilesFromCmd, Func<Vector2, NPC> spawn, int ticks = 720, bool untilHit = false)
		{
			Cleanup(); Arena();
			Seed(7000 + label.Sum(c => (int)c)); // a seed per trial, the same every run
			PlaceCommander(-100);
			SpawnBoss(-100 + bossTilesFromCmd);
			NPC body = spawn(new Vector2((Cx - 100 + 3) * 16f, FloorY * 16f));
			var r = new JoinResult();
			if (body == null || _target == null) { Note(label + ": could not set up"); return r; }
			for (int t = 0; t < (untilHit ? ticks * HitBudgetFactor : ticks) && (t < ticks || r.Dmg == 0); t++) // with untilHit: the window, then on (to a budget) until the first hit lands. The claim is "it hurts the boss", not "it does within 12 seconds"
			{
				int lifeBefore = body.life;
				Step(1);
				if (t < 3 && body.ModNPC is SoldierNPC dbg && dbg.OfficerMode) Note($"  tick {t}: life {lifeBefore} -> {body.life}/{body.lifeMax}, active {body.active}, killer '{dbg.LastAttackerName}' {dbg.LastAttackerType}, bottom {body.Bottom / 16f}");
				if (_target.active && _target.life > 0 && _target.life < _target.lifeMax) r.Dmg += _target.lifeMax - _target.life;
				if (!body.active || body.life <= 0) { Note($"{label}: the body is gone after {t} ticks (life {body.life}, active {body.active}, last hit by {(body.ModNPC as SoldierNPC)?.LastAttackerName} type {(body.ModNPC as SoldierNPC)?.LastAttackerType})"); break; }
				r.Ticks++;
				float d = Vector2.Distance(body.Center, _target.Center) / 16f; if (d < r.MinTiles) r.MinTiles = d;
				var st = body.ModNPC is SoldierNPC sn ? (sn.OfficerEngaged ? SoldierState.Engage : sn.State) : SoldierState.Idle; // the state the recorder writes
				if (st == SoldierState.Engage) r.EngagedTicks++;
				r.States[st] = r.States.TryGetValue(st, out int c) ? c + 1 : 1;
			}
			Note($"{label}: boss {bossTilesFromCmd:0} tiles from the commander: damage {r.Dmg}, closest {r.MinTiles:0.0} tiles, states {r.StateText()}");
			return r;
		}

		private static Func<Vector2, NPC> RegularBody(SoldierOrder order) => at => Levy(at, order, ItemID.Minishark, ItemID.MusketBall)?.NPC;

		private static Func<Vector2, NPC> OfficerBody(int townType, SoldierOrder order) => at =>
		{
			NPC town = Commission(townType, 0, at, out SoldierNPC avatar);
			if (avatar == null) return null;
			Note($"officer avatar of {NPCID.Search.GetName(townType)}: life {avatar.NPC.life}/{avatar.NPC.lifeMax}, defense {avatar.NPC.defense}, at {avatar.NPC.Bottom / 16f}, boss at {(_target != null ? _target.Center / 16f : Vector2.Zero)}");
			avatar.NPC.Bottom = at; avatar.NPC.velocity = Vector2.Zero;
			if (order != SoldierOrder.Follow) { avatar.Officer_ReceiveOrder(order); avatar.NPC.netUpdate = true; }
			return avatar.NPC;
		};

		/// <summary>An officer is under the targeting rules a levy is under: what levies never shoot (the Dungeon Guardian, which they keep away from), an officer does not shoot either.</summary>
		private static void NeverTargets()
		{
			var r = Join("officer Arms Dealer vs a boss", 15f, OfficerBody(NPCID.ArmsDealer, SoldierOrder.Follow), 360, untilHit: true);
			Check($"control: an officer shoots a boss 15 tiles away ({r.Dmg})", r.Dmg > 0);
			Cleanup(); Arena(); PlaceCommander(-100);
			SpawnBoss(-100 + 15f, NPCID.DungeonGuardian);
			NPC body = OfficerBody(NPCID.ArmsDealer, SoldierOrder.Follow)(new Vector2((Cx - 97) * 16f, FloorY * 16f));
			long dmg = 0; bool engaged = false;
			for (int t = 0; t < 360 && body != null && body.active; t++)
			{
				Step(1);
				if (_target.active && _target.life < _target.lifeMax) dmg += _target.lifeMax - _target.life;
				if (body.ModNPC is SoldierNPC sn && sn.OfficerEngaged) engaged = true;
			}
			Note($"officer vs the Dungeon Guardian 15 tiles away: damage {dmg}, engaged {engaged}");
			Check("an officer does not shoot the Dungeon Guardian, as a levy does not", body != null && dmg == 0 && !engaged);
		}

		/// <summary>
		/// Militia are the settlement's own: they hunt only inside the settlement radius of their barracks (SettlementRadiusX plus the weapon's reach). A boss inside it is fought; a boss far from home is not, however long it lives.
		/// Records the rule rather than changing it.
		/// </summary>
		private static void MilitiaLeash()
		{
			BarracksTE te = null;
			try { ModContent.GetInstance<BarracksTE>().Place(Cx - 150, FloorY - 3); te = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(b => b.Position.X == Cx - 150); } catch (Exception) { }
			Check("a barracks is placed for the militia check", te != null);
			if (te == null) return;
			float[] dists = { 60f, 0f }; // the far boss's distance is set below, from the weapon's reach: the rule is SettlementRadiusX plus that reach
			var dmgBy = new long[2]; var closest = new float[2]; var states = new string[2];
			for (int k = 0; k < 2; k++)
			{
				Cleanup(); Arena(); PlaceCommander(-100);
				Seed(7100 + k);
				te.Mobilized = true;
				SpawnBoss(-150 + 60f);
				var m = Levy(new Vector2((te.Position.X + 1.5f) * 16f, (te.Position.Y + 3) * 16f), SoldierOrder.SearchDestroy, ItemID.Minishark, ItemID.MusketBall, SoldierKind.Militia, te.ID);
				if (m == null) { Check("a militiaman spawns", false); break; }
				if (k == 1)
				{
					// far = past the settlement radius and past everything this gun's reach (at a boss) adds to it, so the answer does not hang on how far a shot flies; the arena's east end caps it
					float reach = m.Weapon != null && _target != null ? m.Weapon.RangeTiles * m.BossRangeMult(_target) : 0f;
					dists[1] = Math.Min(LevyConstants.SettlementRadiusX + reach + 25f, Half - 5f + 150f);
					SpawnBoss(-150 + dists[1]);
					Note($"militiaman far case: the gun reaches {reach:0} tiles at a boss, the boss stands {dists[1]:0} tiles from the barracks (radius {LevyConstants.SettlementRadiusX})");
				}
				var r = new JoinResult(); closest[k] = float.MaxValue;
				// inside the radius: wait for the first hit (up to HitBudgetFactor windows); outside it: the whole window, and nothing may land
				int window = 900, limit = k == 0 ? window * HitBudgetFactor : window;
				for (int t = 0; t < limit && m.NPC.active && m.NPC.life > 0 && (t < window || dmgBy[k] == 0); t++)
				{
					// the settlement stands its garrison down on the falling edge of an earlier check's boss: this barracks stays mobilized for the measurement
					te.Mobilized = true; if (m.State == SoldierState.ReturnHome) m.State = SoldierState.Idle;
					Step(1);
					if (_target.active && _target.life < _target.lifeMax) dmgBy[k] += _target.lifeMax - _target.life;
					float d = Vector2.Distance(m.NPC.Center, _target.Center) / 16f; if (d < closest[k]) closest[k] = d;
					r.Ticks++; r.States[m.State] = r.States.TryGetValue(m.State, out int c) ? c + 1 : 1;
				}
				states[k] = r.StateText();
				Note($"militiaman, boss {dists[k]:0} tiles from the barracks: damage {dmgBy[k]}, closest {closest[k]:0.0} tiles, states {states[k]}");
			}
			Check($"a militiaman fights a boss inside the settlement radius ({dmgBy[0]})", dmgBy[0] > 0);
			Check($"and stays at home for a boss far outside it (damage {dmgBy[1]}, closest {closest[1]:0} tiles)", dmgBy[1] == 0 && closest[1] > 100f);
			try { ModContent.GetInstance<BarracksTE>().Kill(Cx - 150, FloorY - 3); } catch (Exception) { }
		}

		private static void BossJoin()
		{
			NeverTargets();
			MilitiaLeash();
			var fighters = new List<(string label, Func<Vector2, NPC> make, bool officer)>
			{
				("regular, Follow", RegularBody(SoldierOrder.Follow), false),
				("regular, Search and Destroy", RegularBody(SoldierOrder.SearchDestroy), false),
				("officer Arms Dealer, Follow", OfficerBody(NPCID.ArmsDealer, SoldierOrder.Follow), true),
				("officer Arms Dealer, Search and Destroy", OfficerBody(NPCID.ArmsDealer, SoldierOrder.SearchDestroy), true),
				("officer Guide, Follow", OfficerBody(NPCID.Guide, SoldierOrder.Follow), true),
			};
			foreach (float dist in new[] { 25f, 60f })
			{
				foreach (var f in fighters)
				{
					var r = Join(f.label, dist, f.make, untilHit: dist <= 25f); // the claims are made at 25 tiles: wait there for the first hit
					// the contract: an officer with a weapon joins a boss fight the way a regular does at the same order and the same distance
					if (f.officer && dist <= 25f) Check($"{f.label}, boss {dist:0} tiles away: the officer hurts the boss ({r.Dmg}) and the recording shows him engaged ({r.EngagedTicks} ticks)", r.Dmg > 0 && r.EngagedTicks > 0);
					if (!f.officer && dist <= 25f) Check($"{f.label}, boss {dist:0} tiles away: the regular hurts the boss, the control ({r.Dmg})", r.Dmg > 0);
				}
			}
		}

		// =====================================================================================================
		// BANNER
		// =====================================================================================================
		private static void Banner()
		{
			Cleanup(); Arena();
			PlaceCommander(-100);
			Check("a body is away from home beyond the near-home radius, and not inside it", OffDutyBanner.IsAway(Vector2.Zero, new Vector2(LevyConstants.NearHomeTiles * 16f + 40f, 0f)) && !OffDutyBanner.IsAway(Vector2.Zero, new Vector2(40f, 0f)));

			// an officer: the banner stands at the town NPC's home while his avatar marches, with the town NPC's name
			int homeX = Cx - 150;
			NPC town = Commission(NPCID.Guide, 0, new Vector2((Cx - 97) * 16f, FloorY * 16f), out SoldierNPC avatar);
			Check("an officer is commissioned for the banner check", town != null && avatar != null);
			if (town != null && avatar != null)
			{
				town.homeTileX = homeX; town.homeTileY = FloorY; town.homeless = false;
				Step(20);
				OffDutyBanner.Refresh();
				Check("an officer away from home leaves one banner at the door of his house", OffDutyBanner.SpotCount == 1 && OffDutyBanner.SpotAt(0).Officer && OffDutyBanner.SpotAt(0).Base == new Vector2(homeX * 16f + 8f, FloorY * 16f));
				string text = OffDutyBanner.SpotCount > 0 ? OffDutyBanner.TextOf(0) : "";
				Note("officer banner hover: " + text.Replace("\n", " | "));
				Check("the hover reads Off on duty. and his name", text.StartsWith("Off on duty.") && text.Contains(avatar.Record.Name));
				Check("the banner places no tile (the world is unchanged under it)", !Main.tile[homeX, FloorY - 1].HasTile && !Main.tile[homeX, FloorY - 2].HasTile);
				avatar.NPC.Bottom = new Vector2(homeX * 16f + 8f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero;
				OffDutyBanner.Refresh();
				if (OffDutyBanner.SpotCount > 0) Note($"banner still up: spots {OffDutyBanner.SpotCount}, count {OffDutyBanner.SpotAt(0).Count}, base {OffDutyBanner.SpotAt(0).Base}, avatar centre {avatar.NPC.Center}, home door {new Vector2(homeX * 16f + 8f, FloorY * 16f)}");
				Check("the banner is gone when he is back at his door", OffDutyBanner.SpotCount == 0);
				avatar.NPC.Bottom = new Vector2((Cx - 97) * 16f, FloorY * 16f);
				OffDutyBanner.Refresh();
				Check("and returns when he marches off again", OffDutyBanner.SpotCount == 1);
				OfficerSystem.Instance.Dismiss(Main.player[0], town); Step(20);
				OffDutyBanner.Refresh();
				Check("a dismissed officer leaves no banner", OffDutyBanner.SpotCount == 0);
				town.active = false;
			}

			// levies: one banner over the barracks for all of them, the names listed, a count past the names that fit
			BarracksTE te = null;
			try { ModContent.GetInstance<BarracksTE>().Place(Cx - 150, FloorY - 3); te = TileEntity.ByID.Values.OfType<BarracksTE>().FirstOrDefault(b => b.Position.X == Cx - 150); } catch (Exception) { }
			Check("a barracks is placed for the banner check", te != null);
			if (te != null)
			{
				var made = new List<SoldierNPC>();
				for (int i = 0; i < 7; i++) { var s = Levy(new Vector2((Cx - 95 + i) * 16f, FloorY * 16f), SoldierOrder.Hold, 0, 0, SoldierKind.Regular, te.ID); if (s != null) { s.Record.Name = "Pvt. Banner" + i; made.Add(s); } }
				Step(10);
				OffDutyBanner.Refresh();
				Check("seven levies away from their barracks leave ONE banner over it", made.Count == 7 && OffDutyBanner.SpotCount == 1 && !OffDutyBanner.SpotAt(0).Officer && OffDutyBanner.SpotAt(0).Count == 7);
				string t = OffDutyBanner.SpotCount > 0 ? OffDutyBanner.TextOf(0) : "";
				Note("levy banner hover: " + t.Replace("\n", " | "));
				Check("the hover lists the first names and then how many more", t.StartsWith("Off on duty.") && t.Contains("Pvt. Banner0") && t.Contains("and 2 more"));
				foreach (var s in made) { s.NPC.Bottom = new Vector2((te.Position.X + 1.5f) * 16f + 16f * made.IndexOf(s) * 0.2f, (te.Position.Y + 3) * 16f); }
				OffDutyBanner.Refresh();
				Check("the banner is gone when they are home", OffDutyBanner.SpotCount == 0);
				made[0].NPC.Bottom = new Vector2((Cx - 95) * 16f, FloorY * 16f); OffDutyBanner.Refresh();
				Check("and one levy away brings it back for that one", OffDutyBanner.SpotCount == 1 && OffDutyBanner.SpotAt(0).Count == 1 && OffDutyBanner.TextOf(0).Contains("Pvt. Banner0"));
				// cheap: a refresh allocates nothing
				OffDutyBanner.Refresh(); long b0 = GC.GetAllocatedBytesForCurrentThread();
				for (int i = 0; i < 200; i++) OffDutyBanner.Refresh();
				long per = (GC.GetAllocatedBytesForCurrentThread() - b0) / 200;
				Note("a banner refresh allocates " + per + " bytes");
				Check("a banner refresh allocates nothing", per == 0);
				try { ModContent.GetInstance<BarracksTE>().Kill(Cx - 150, FloorY - 3); } catch (Exception) { }
			}
			OffDutyBanner.Clear();
		}
	}
}
