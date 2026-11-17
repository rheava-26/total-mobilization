using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The floor and dash trials of the mobility world check: a levy against a real <see cref="Player"/> with the same boots (or the same dash accessory), on asphalt, honey, ice, conveyor belts, and with a dash accessory in the horizontal slot.
	/// The real player runs its own Player.Update on the same floor tiles (no world step: only the player), with its controls held; the levy runs the real world step toward a commander kept out ahead. Each trial reports the top speed (px/tick),
	/// the ticks from the start of the move to 90% of that top, and the ticks to cover 100 tiles; the "before" arm switches the new rule off (<see cref="TileRun.On"/>, <see cref="SoldierNPC.DashFixOn"/>) in the same boot.
	/// </summary>
	public sealed partial class MobilityWorldCheck
	{
		private struct RunStat { public float top; public int t90, t100; public float x1; }

		/// <summary>The speeds of a run along the floor: its top (px/tick), ticks from the start of the move to 90% of that top, ticks to cover 100 tiles (0 if it never did).</summary>
		private static RunStat Stat(List<float> vx)
		{
			var r = new RunStat(); if (vx.Count == 0) return r;
			int start = vx.FindIndex(v => v > 0.3f); if (start < 0) return r;
			r.top = vx.Skip(start).Max();
			int k90 = vx.FindIndex(start, v => v >= 0.9f * r.top); r.t90 = k90 < 0 ? 0 : k90 - start;
			float x = 0f; r.t100 = 0; for (int i = start; i < vx.Count; i++) { x += vx[i]; if (r.t100 == 0 && x >= 1600f) { r.t100 = i - start + 1; break; } }
			r.x1 = vx.Skip(start).Sum();
			return r;
		}

		private static RunStat LevyRun(Item horizontal, int floorTile, int ticks, float aheadTiles = 45f)
		{
			ClearSoldiers(); Arena(floorTile);
			var at = new Vector2((Cx - Half + 12) * 16f, FloorY * 16f);
			_cmdAt = at + new Vector2(aheadTiles * 16f, -3 * 16f);
			KeepCommander(); Step(2);
			int body = SpawnLevy(null, horizontal, at, out var rec);
			if (body < 0 || !rec.HasBody) { Check("a floor trial levy spawns", false); return default; }
			var vx = new List<float>(); float prev = Main.npc[body].position.X;
			for (int t = 1; t <= ticks; t++)
			{
				var n = Main.npc[body];
				_cmdAt = new Vector2(Math.Min(n.Center.X + aheadTiles * 16f, (Cx + Half - 4) * 16f), (FloorY - 3) * 16f);
				Step(1);
				n = Main.npc[body]; if (!n.active) break;
				vx.Add(n.position.X - prev); prev = n.position.X;
			}
			var st = Stat(vx);
			ClearSoldiers();
			return st;
		}

		/// <summary>A real Player (its own Player.Update, in an unused player slot; a Calamity dash only runs for the local player, which a test server cannot make, so those are measured on the levy alone) held on the floor with its controls: running right, standing still, or double-tapping right to dash. Its per-tick horizontal movement.</summary>
		private static List<float> PlayerTrace(Item horizontal, int floorTile, int ticks, bool dashTaps = false, bool idle = false)
		{
			Arena(floorTile);
			const int slot = 1;
			var saved = Main.player[slot]; var p = new Player(); var vx = new List<float>();
			try
			{
				Main.player[slot] = p; p.whoAmI = slot; p.active = true; p.dead = false; p.name = "MobPlayer"; p.width = 20; p.height = 42; p.direction = 1;
				p.statLifeMax = p.statLifeMax2 = p.statLife = 500;
				if (horizontal != null) p.armor[3] = horizontal;
				p.position = new Vector2((Cx - Half + 12) * 16f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
				float prev = p.position.X;
				for (int t = 1; t <= ticks; t++)
				{
					p.active = true;
					if (idle) { p.controlRight = false; p.controlLeft = false; }
					else if (dashTaps) { p.controlRight = t == 1 || t >= 3; if (t == 1 || t == 3) p.releaseRight = true; }
					else p.controlRight = true;
					p.Update(slot); // not the local player (a local one queues a character autosave, which a test server cannot do and quits on)
					vx.Add(p.position.X - prev); prev = p.position.X;
					if (p.position.X > (Cx + Half - 30) * 16f) break;
				}
			}
			finally { Main.player[slot] = saved; }
			return vx;
		}

		private static string RS(RunStat r) => $"top {F(r.top)} px/tick, 90% of it after {r.t90} ticks, 100 tiles in {(r.t100 > 0 ? r.t100.ToString() : "never")} ticks";

		private static void FloorTrials()
		{
			var floors = new List<(string name, int tile)> { ("plain", TileID.GrayBrick), ("asphalt", TileID.Asphalt), ("honey block", TileID.HoneyBlock), ("ice block", TileID.IceBlock) };
			var boots = new List<(string name, int type)> { ("no boots", 0), ("Hermes Boots", ItemID.HermesBoots) };
			var levyAfter = new Dictionary<string, RunStat>(); var levyBefore = new Dictionary<string, RunStat>(); var player = new Dictionary<string, RunStat>();
			bool tr0 = TileRun.On;
			try
			{
				foreach (var (fn, ft) in floors)
					foreach (var (bn, bt) in boots)
					{
						string key = fn + "/" + bn;
						Item it() => bt == 0 ? null : Make(bt);
						player[key] = Stat(PlayerTrace(it(), ft, 700));
						TileRun.On = true; levyAfter[key] = LevyRun(it(), ft, 330);
						if (ft != TileID.GrayBrick) { TileRun.On = false; levyBefore[key] = LevyRun(it(), ft, 330); }
						Note($"floor {fn}, {bn}: real player {RS(player[key])}; levy with the floor rule {RS(levyAfter[key])}" + (levyBefore.ContainsKey(key) ? $"; levy before (floor rule off) {RS(levyBefore[key])}" : ""));
					}
			}
			finally { TileRun.On = tr0; }

			foreach (var bn in boots.Select(b => b.name))
			{
				var pa = player["asphalt/" + bn]; var la = levyAfter["asphalt/" + bn]; var lb = levyBefore["asphalt/" + bn]; var pp = player["plain/" + bn]; var lp = levyAfter["plain/" + bn];
				Check($"asphalt, {bn}: a levy runs as fast as a player does there: {F(la.top)} vs {F(pa.top)} px/tick (before {F(lb.top)})", pa.top > 8f && la.top >= pa.top * 0.85f && la.top <= pa.top * 1.15f);
				Check($"asphalt, {bn}: the floor makes a levy faster than he runs on plain ground, as it does a player: levy {F(lp.top)} -> {F(la.top)}, player {F(pp.top)} -> {F(pa.top)}", la.top >= lp.top * 1.4f && pa.top >= pp.top * 1.4f);
				Check($"asphalt, {bn}: with the floor rule off a levy ran no faster there (the old behaviour): {F(lb.top)} vs plain {F(lp.top)}", lb.top <= lp.top * 1.15f);
			}
			{
				var ph = player["honey block/no boots"]; var lh = levyAfter["honey block/no boots"]; var lp = levyAfter["plain/no boots"]; var pp = player["plain/no boots"];
				Check($"honey blocks slow a levy to a quarter of his run as they do a player: levy {F(lp.top)} -> {F(lh.top)}, player {F(pp.top)} -> {F(ph.top)} px/tick", lh.top <= lp.top * 0.4f && ph.top <= pp.top * 0.4f && lh.top > 0.3f);
				var pi = player["ice block/no boots"]; var li = levyAfter["ice block/no boots"];
				Check($"ice slows the climb to his top speed as it does a player's: ticks to 90% of the top: levy {lp.t90} -> {li.t90}, player {pp.t90} -> {pi.t90}", li.t90 > lp.t90 && pi.t90 >= pp.t90 * 1.2f);
			}

			// conveyor belts: a body standing still on one is carried (a player is; a levy was not)
			foreach (var (bn, bt) in new[] { ("right belt", TileID.ConveyorBeltRight), ("left belt", TileID.ConveyorBeltLeft) })
			{
				var pv = PlayerTrace(null, bt, 150, idle: true); float pd = pv.Sum();
				bool tr1 = TileRun.On; float after, before;
				try { TileRun.On = true; after = LevyStill(bt, 150); TileRun.On = false; before = LevyStill(bt, 150); } finally { TileRun.On = tr1; }
				Note($"{bn}: standing still for 150 ticks a real player drifts {F(pd)} px, a levy {F(after)} px (before {F(before)})");
				Check($"a {bn} carries a levy as it carries a player: {F(after)} px against {F(pd)}", Math.Abs(pd) > 20f && Math.Sign(after) == Math.Sign(pd) && Math.Abs(after) >= Math.Abs(pd) * 0.25f && Math.Abs(after) <= Math.Abs(pd) * 1.6f && Math.Abs(before) < Math.Abs(after) * 0.25f);
			}
			Arena();
		}

		private static float LevyStill(int floorTile, int ticks)
		{
			ClearSoldiers(); Arena(floorTile);
			var at = new Vector2(Cx * 16f, FloorY * 16f);
			_cmdAt = new Vector2(at.X - 30 * 16f, FloorY * 16f - 21f); // well away from him (a commander on top of him shoves him aside), and he is on Hold
			KeepCommander(); Step(2);
			int body = SpawnLevy(null, null, at, out var rec);
			if (body < 0 || !rec.HasBody) { Check("a belt trial levy spawns", false); return 0f; }
			rec.Order = SoldierOrder.Hold;
			Step(60);
			float x0 = Main.npc[body].position.X;
			for (int t = 0; t < ticks; t++) Step(1);
			float d = Main.npc[body].position.X - x0;
			ClearSoldiers();
			return d;
		}

		// =====================================================================================================
		// dash accessories in the horizontal slot (a levy against a real player with the same item)
		// =====================================================================================================
		private static void DashTrials()
		{
			var items = new List<(string name, int type)> { ("Tabi", ItemID.Tabi), ("Master Ninja Gear", ItemID.MasterNinjaGear), ("Shield of Cthulhu", ItemID.EoCShield) };
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				foreach (var n in new[] { "AsgardianAegis", "ElysianAegis", "StatisNinjaBelt", "EvasionScarf", "OrnateShield" })
					if (cal.TryFind<ModItem>(n, out var mi)) items.Add((n, mi.Type));
			Arena();
			Check("a bare dummy has no dash", !DashOf(null).has);
			var bare = LevyRun(null, TileID.GrayBrick, 330, 38f);
			Note($"levy, nothing in the horizontal slot: {RS(bare)}");
			bool df = SoldierNPC.DashFixOn;
			try
			{
				foreach (var (name, type) in items)
				{
					var (has, speed) = DashOf(Make(type));
					Check($"{name} gives a dummy a dash ({F(speed)} px/tick)", has && speed > 8f);
					Check($"the horizontal slot accepts {name}", new SoldierGear().CanHold(SoldierGear.SlotBoots, Make(type)) && MobilitySlots.IsHorizontal(Make(type)));
					// the real player's dash: double tap, then follow the speed until the dash is spent
					var tr = PlayerTrace(Make(type), TileID.GrayBrick, 130, dashTaps: true);
					float pPeak = tr.Count > 0 ? tr.Max() : 0f; float pDist60 = DashDistance(tr, 60), pDist100 = DashDistance(tr, 100);
					// the levy: the gear's own dash on, and the old fixed burst
					Array.Clear(SoldierNPC.DashRef, 0, SoldierNPC.DashRef.Length);
					SoldierNPC.DashFixOn = true; var on = LevyDash(Make(type), out int dOn, out float peakOn, out float dist60On);
					string refOn = string.Join(",", SoldierNPC.DashRef.Take(10));
					SoldierNPC.DashFixOn = false; var off = LevyDash(Make(type), out int dOff, out float peakOff, out float dist60Off);
					SoldierNPC.DashFixOn = df;
					Note($"{name}: real player dash peak {F(pPeak)} px/tick, {F(pDist60)} px in the first 60 ticks of it ({F(pDist100)} in 100); levy with the gear's dash: peak {F(peakOn)}, {dOn} dashes, {F(dist60On)} px in the 60 ticks after the first; levy with the old fixed burst: peak {F(peakOff)}, {dOff} dashes, {F(dist60Off)} px; dash gate (asked,cooling,ground,state,retreat,goal,turn,lane short,lane blocked,started) with the gear's dash {refOn}");
					Check($"{name} in a levy's horizontal slot makes him dash: peak {F(peakOn)} px/tick over a bare run's {F(bare.top)}, {dOn} dashes", dOn >= 1 && peakOn >= bare.top + 5f);
					if ((type == ItemID.Tabi || type == ItemID.MasterNinjaGear || type == ItemID.EoCShield) && !ModLoader.HasMod("CalamityMod")) // Calamity rewrites a player's vanilla dash speed (its own dash code): observed above, compared only without it
						Check($"{name}: a levy's dash starts at the speed a player's does ({F(peakOn)} vs {F(pPeak)} px/tick) and covers ground to the goal, up to a free dash's ({F(dist60On)} vs {F(pDist60)} px in 60 ticks)", pPeak > 8f && peakOn >= pPeak * 0.8f && dist60On >= pDist60 * 0.3f && dist60On <= pDist60 * 1.5f);
				}
			}
			finally { SoldierNPC.DashFixOn = df; }
			Arena();
		}

		private static (bool has, float speed) DashOf(Item it)
		{
			var d = new Player(); d.ResetEffects();
			for (int i = 0; i < d.armor.Length; i++) d.armor[i] = new Item();
			if (it != null) d.armor[3] = it;
			global::Levy.Common.Util.DummyEquip.SafeUpdateEquips(d, 250);
			var ab = MovementAbilities.FromDummy(d);
			return (ab.HasDash, ab.DashSpeed);
		}

		/// <summary>Ground covered in the <paramref name="window"/> ticks that start where the dash does (the first tick the speed reaches 10 px/tick).</summary>
		private static float DashDistance(List<float> vx, int window)
		{
			int s = vx.FindIndex(v => v >= 10f); if (s < 0) return 0f;
			return vx.Skip(s).Take(window).Sum();
		}

		private static RunStat LevyDash(Item item, out int dashes, out float peak, out float dist60)
		{
			ClearSoldiers(); Arena();
			var at = new Vector2((Cx - Half + 12) * 16f, FloorY * 16f);
			float ahead = 38f; _cmdAt = new Vector2(at.X + ahead * 16f, FloorY * 16f - 21f); // her feet on the floor: a goal a tile above or below the levy's feet is not a goal to dash along
			KeepCommander(); Step(2);
			int body = SpawnLevy(null, item, at, out var rec);
			dashes = 0; peak = 0f; dist60 = 0f;
			if (body < 0 || !rec.HasBody) { Check("a dash trial levy spawns with the item in the horizontal slot", false); return default; }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			var vx = new List<float>(); float prev = Main.npc[body].position.X; int firstDash = -1;
			for (int t = 1; t <= 420; t++)
			{
				var n = Main.npc[body];
				_cmdAt = new Vector2(Math.Min(n.Center.X + ahead * 16f, (Cx + Half - 4) * 16f), FloorY * 16f - 21f);
				Step(1);
				n = Main.npc[body]; if (!n.active) break;
				vx.Add(n.position.X - prev); prev = n.position.X;
				if (firstDash < 0 && sn.TravelDashes + sn.OldDashes > 0) firstDash = vx.Count - 1;
			}
			dashes = sn.TravelDashes + sn.OldDashes; peak = vx.Count > 0 ? vx.Max() : 0f;
			if (firstDash >= 0) dist60 = vx.Skip(firstDash).Take(60).Sum();
			var st = Stat(vx);
			ClearSoldiers();
			return st;
		}
	}
}
