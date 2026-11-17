using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the wings and boots slots, logged as "[MobWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the officer world check;
	/// LEVY_MOBONLY=1 runs it alone). Slot 0 stands in as the commander and the real Main.Update steps the world over a wide flat arena.
	///  * TOP SPEED: a Follow levy chases a commander kept 45 tiles ahead on level ground; his position is read every tick. Bare, then in Hermes Boots, Lightning Boots and Terraspark Boots (and Angel Treads with Calamity),
	///    each worn only in the BOOTS slot. The boots must change the measured top speed (and match the speed the stat pass reports for the dummy).
	///  * FLIGHT TIME: the commander hovers 60 tiles above the levy; a levy with wings flies up after him and a bare one can only jump. The ticks of his first flight (from leaving the floor to landing) and the height he reaches
	///    are read every tick, bare and in Fledgling Wings, Angel Wings and Solar Wings, each worn only in the WINGS slot. The wings must change the measured flight time and the dummy's wing time.
	/// A flight is the LONGEST unbroken stretch in the air over a fixed 900-tick window with the commander held out of reach the whole time (the brain may land and take off again; one early landing is not the wings' fault), and the
	/// wings' ranking is also asserted on the stat the wings give the dummy (their wing time), which does not depend on what the brain chooses to do.
	/// Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed partial class MobilityWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static Vector2 _cmdAt; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 270;
		private const string Commander = "MobTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_MOBWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		// the server drops a slot with no connection every tick: slot 0 is kept alive and made local only while NPCs and projectiles update
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the officer world check calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; ClearSoldiers(); }
			_mod.Logger.Info($"[MobWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[MobWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[MobWorld] OBSERVED " + s);
		private static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++) { KeepCommander(); WorldStep.Update(); }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void Arena(int floorTile = TileID.GrayBrick)
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 90; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = (ushort)floorTile; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void ClearSoldiers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
			}
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		/// <summary>A Follow levy with the wings and boots in their slots only (nothing in an accessory slot), locked so the armory leaves the kit alone; its body, or -1.</summary>
		private static int SpawnLevy(Item wings, Item boots, Vector2 at, out SoldierRecord rec)
		{
			rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Mobility",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.TrySet(SoldierGear.SlotWeapon, Make(ItemID.CopperShortsword), rec.AccessorySlots, out _);
			if (wings != null && !rec.Gear.TrySet(SoldierGear.SlotWings, wings, rec.AccessorySlots, out _)) return -1;
			if (boots != null && !rec.Gear.TrySet(SoldierGear.SlotBoots, boots, rec.AccessorySlots, out _)) return -1;
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body >= 0 && rec.HasBody) Main.npc[body].Bottom = at;
			return body;
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			ClearSoldiers();
			var boots = new List<(string name, int type)> { ("none", 0), ("Hermes Boots", ItemID.HermesBoots), ("Lightning Boots", ItemID.LightningBoots), ("Terraspark Boots", ItemID.TerrasparkBoots) };
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind<ModItem>("AngelTreads", out var treads)) boots.Add(("Angel Treads", treads.Type));
			var wings = new List<(string name, int type)> { ("none", 0), ("Fledgling Wings", ItemID.CreativeWings), ("Angel Wings", ItemID.AngelWings), ("Solar Wings", ItemID.WingsSolar) };

			var speeds = new Dictionary<string, float>();
			foreach (var (name, type) in boots)
			{
				var r = SpeedTrial(type == 0 ? null : Make(type), name);
				speeds[name] = r.peak; // the top speed: the most a levy covers in one tick (the average also holds the time spent accelerating and the follow brain's pauses)
			}
			float none = speeds["none"];
			foreach (var kv in speeds) if (kv.Key != "none") Check($"boots in the boots slot change a levy's measured top speed: {kv.Key} {F(kv.Value)} px/tick vs bare {F(none)}", kv.Value >= none + 1.5f - 0.01f);
			if (speeds.ContainsKey("Hermes Boots") && speeds.ContainsKey("Lightning Boots")) Check($"better boots run faster: Lightning {F(speeds["Lightning Boots"])} > Hermes {F(speeds["Hermes Boots"])}", speeds["Lightning Boots"] > speeds["Hermes Boots"] + 0.2f);
			if (speeds.ContainsKey("Angel Treads") && speeds.ContainsKey("Hermes Boots")) Check($"Angel Treads outrun Hermes Boots: {F(speeds["Angel Treads"])} vs {F(speeds["Hermes Boots"])}", speeds["Angel Treads"] > speeds["Hermes Boots"] + 0.5f);

			var flights = new Dictionary<string, int>(); var wingTimes = new Dictionary<string, int>();
			foreach (var (name, type) in wings)
			{
				var r = FlightTrial(type == 0 ? null : Make(type), name);
				flights[name] = r.ticks; wingTimes[name] = r.wingTime;
			}
			int bare = flights["none"];
			foreach (var kv in flights) if (kv.Key != "none") Check($"wings in the wings slot change a levy's flight time: {kv.Key} {kv.Value} ticks airborne vs bare {bare}", kv.Value > bare + 30);
			// the ranking on what the wings give the levy (the stat pass's wing time: the same number a player's wings carry), then as measured over the longest flight
			if (wingTimes.ContainsKey("Fledgling Wings") && wingTimes.ContainsKey("Angel Wings") && wingTimes.ContainsKey("Solar Wings"))
				Check($"longer-flying wings give a longer wing time: Fledgling {wingTimes["Fledgling Wings"]} < Angel {wingTimes["Angel Wings"]} < Solar {wingTimes["Solar Wings"]}", wingTimes["Fledgling Wings"] < wingTimes["Angel Wings"] && wingTimes["Angel Wings"] < wingTimes["Solar Wings"]);
			if (flights.ContainsKey("Fledgling Wings") && flights.ContainsKey("Angel Wings")) Check($"longer-flying wings fly longer: Angel {flights["Angel Wings"]} >= Fledgling {flights["Fledgling Wings"]}", flights["Angel Wings"] >= flights["Fledgling Wings"]);
			ClearSoldiers();
			Arena();
			try { FloorTrials(); } catch (Exception e) { Check("floor trials threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			try { DashTrials(); } catch (Exception e) { Check("dash trials threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())), false); }
			ClearSoldiers(); Arena();
		}

		private static (float avg, float peak) SpeedTrial(Item boots, string label)
		{
			ClearSoldiers();
			var at = new Vector2((Cx - Half + 12) * 16f, FloorY * 16f);
			_cmdAt = at + new Vector2(45 * 16f, -3 * 16f);
			KeepCommander(); Step(2);
			int body = SpawnLevy(null, boots, at, out var rec);
			if (body < 0 || !rec.HasBody) { Check($"speed trial {label}: the levy spawns with the boots in the boots slot", false); return (0, 0); }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			const int Warm = 150, Total = 330;
			float x0 = 0f, prev = Main.npc[body].position.X, peak = 0f; int startTick = 0;
			for (int t = 1; t <= Total; t++)
			{
				var n = Main.npc[body];
				_cmdAt = new Vector2(Math.Min(n.Center.X + 45 * 16f, (Cx + Half - 4) * 16f), (FloorY - 3) * 16f);
				Step(1);
				n = Main.npc[body]; if (!n.active) break;
				float x = n.position.X;
				if (t > Warm) { peak = Math.Max(peak, Math.Abs(x - prev)); if (startTick == 0) { startTick = t; x0 = x; } }
				prev = x;
			}
			float avg = startTick == 0 ? 0f : (Main.npc[body].position.X - x0) / (Total - startTick + 1);
			var ab = sn.Abilities;
			Note($"top speed, boots slot = {label}: measured avg {F(avg)} px/tick, peak {F(peak)}; stat pass MaxRunSpeed {F(ab?.MaxRunSpeed ?? 0)}, dummy accRunSpeed {F(sn.Dummy.accRunSpeed)}, rocketBoots {sn.Dummy.rocketBoots}, wings slot empty {!rec.Gear.HasWings}, accessory slots empty {rec.Gear.Accessories.All(a => a.IsAir)}");
			if (boots != null) Check($"speed trial {label}: the levy's dummy runs at the boots' speed ({F(ab?.MaxRunSpeed ?? 0)} px/tick from the stat pass)", ab != null && ab.MaxRunSpeed > 3.5f);
			ClearSoldiers();
			return (avg, peak);
		}

		private static (int ticks, float heightTiles, int wingTime) FlightTrial(Item wings, string label)
		{
			ClearSoldiers();
			global::Terraria.Main.rand = new Terraria.Utilities.UnifiedRandom(9100 + (wings?.type ?? 0)); // the same random stream for the same wings, whatever else the box is doing
			var at = new Vector2(Cx * 16f, FloorY * 16f);
			_cmdAt = new Vector2(Cx * 16f + 4 * 16f, (FloorY - 60) * 16f);
			KeepCommander(); Step(2);
			int body = SpawnLevy(wings, null, at, out var rec);
			if (body < 0 || !rec.HasBody) { Check($"flight trial {label}: the levy spawns with the wings in the wings slot", false); return (0, 0, 0); }
			var sn = (SoldierNPC)Main.npc[body].ModNPC;
			// stand still on the floor until his spawn grace is over, then the commander rises out of reach
			_cmdAt = new Vector2(Cx * 16f + 4 * 16f, (FloorY - 3) * 16f);
			Step(70);
			_cmdAt = new Vector2(Cx * 16f + 4 * 16f, (FloorY - 60) * 16f);
			float floorB = FloorY * 16f;
			int flightTicks = 0, streak = 0, flights = 0; float minB = floorB;
			for (int t = 1; t <= 900; t++) // the whole window: the commander stays out of reach, and the levy may take off more than once
			{
				Step(1);
				var n = Main.npc[body]; if (!n.active) break;
				bool air = n.Bottom.Y < floorB - 6f;
				if (air) { if (streak == 0) flights++; streak++; flightTicks = Math.Max(flightTicks, streak); minB = Math.Min(minB, n.Bottom.Y); }
				else streak = 0;
			}
			var ab = sn.Abilities;
			float h = (floorB - minB) / 16f;
			Note($"flight time, wings slot = {label}: longest flight {flightTicks} ticks (of {flights} take-offs in 900 ticks), rose {F(h)} tiles; stat pass HasWings {ab?.HasWings} WingTimeMax {ab?.WingTimeMax}, dummy wingsLogic {sn.Dummy.wingsLogic} wingTimeMax {sn.Dummy.wingTimeMax}, accessory slots empty {rec.Gear.Accessories.All(a => a.IsAir)}");
			if (wings != null) Check($"flight trial {label}: the dummy wears the wings from the wings slot (wingsLogic {sn.Dummy.wingsLogic}, wing time {sn.Dummy.wingTimeMax})", ab != null && ab.HasWings && ab.WingTimeMax > 0 && sn.Dummy.wingsLogic == wings.wingSlot);
			int wingTime = ab?.WingTimeMax ?? 0;
			ClearSoldiers();
			return (flightTicks, h, wingTime);
		}
	}
}
