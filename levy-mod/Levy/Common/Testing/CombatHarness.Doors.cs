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
	/// DOORS (LEVY_HARNESS_SCENARIO=doors, LEVY_DOORS_CASES = comma list of single,stack2,series3; default all; LEVY_DOORS_N = levies per case, default 15). Measurement only.
	/// Each case builds a wall (or walls) with closed doors in it, spawns N Follow levies on the left, parks the stand-in commander on the right (leg 1) and then back on the left (leg 2).
	/// After every leg, once the last levy is clear of the structure, waits DrSettleTicks (10 s) and counts the doors left open. One "HARNESS-DOORS: RESULT" line per leg, then a summary.
	/// The count reads tiles only, so the same scenario runs against any version of the door code.
	/// </summary>
	public partial class CombatHarness
	{
		private const int DrSettleTicks = 600, DrLegMaxTicks = 3000, DrWallTop = 12;
		private enum DrKind { Single, Stack2, Series3 }
		private sealed class DrCase { public string Name; public DrKind Kind; public int Doors; }
		private readonly List<DrCase> _drCases = new List<DrCase>();
		private int _drCase = -1, _drLeg, _drTick, _drSettle = -1, _drN = 15;
		private bool _drStarted; private string _drSummary = "";
		private readonly List<SoldierRecord> _drRecs = new List<SoldierRecord>();

		private bool BeginDoors()
		{
			_drCases.Clear();
			string want = Environment.GetEnvironmentVariable("LEVY_DOORS_CASES") ?? "single,stack2,series3";
			int.TryParse(Environment.GetEnvironmentVariable("LEVY_DOORS_N"), out _drN); if (_drN <= 0) _drN = 15;
			foreach (string s in want.Split(','))
				switch (s.Trim().ToLowerInvariant())
				{
					case "single": _drCases.Add(new DrCase { Name = "single", Kind = DrKind.Single, Doors = 1 }); break;
					case "stack2": _drCases.Add(new DrCase { Name = "stack2", Kind = DrKind.Stack2, Doors = 2 }); break;
					case "series3": _drCases.Add(new DrCase { Name = "series3", Kind = DrKind.Series3, Doors = 3 }); break;
				}
			_drCase = -1; _drStarted = false; _drSummary = "";
			Mod.Logger.Info($"HARNESS-DOORS: scenario; cases={_drCases.Count} levies={_drN}");
			return _drCases.Count > 0;
		}

		private static void DrAir(int x, int y) { Tile t = Main.tile[x, y]; t.HasTile = false; t.TileType = 0; t.TileFrameX = 0; t.TileFrameY = 0; t.LiquidAmount = 0; }
		private static void DrBlock(int x, int y) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.GrayBrick; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = BlockType.Solid; }
		private static void DrDoor(int x, int topY) { for (int k = 0; k < 3; k++) { Tile t = Main.tile[x, topY + k]; t.HasTile = true; t.TileType = TileID.ClosedDoor; t.TileFrameX = 0; t.TileFrameY = (short)(18 * k); t.BlockType = BlockType.Solid; } }
		/// <summary>A wall column x, DrWallTop tall from the floor, with a 3-tall door at each of doorTops (the door's top row).</summary>
		private static void DrWall(int x, params int[] doorTops)
		{
			for (int y = FloorY - DrWallTop; y < FloorY; y++) DrBlock(x, y);
			foreach (int top in doorTops) DrDoor(x, top);
		}

		private void DrBuild(DrCase c)
		{
			for (int x = _cx - 30; x <= _cx + 30; x++) for (int y = FloorY - 40; y < FloorY; y++) DrAir(x, y);
			switch (c.Kind)
			{
				case DrKind.Single: DrWall(_cx, FloorY - 3); break;
				case DrKind.Stack2: DrWall(_cx, FloorY - 3, FloorY - 6); break; // a 6-tall doorway made of two stacked doors
				case DrKind.Series3: DrWall(_cx - 3, FloorY - 3); DrWall(_cx, FloorY - 3); DrWall(_cx + 3, FloorY - 3); break; // a corridor of doors three tiles apart
			}
		}

		private int DrCountOpen()
		{
			int n = 0;
			for (int x = _cx - 30; x <= _cx + 30; x++)
				for (int y = FloorY - 40; y < FloorY; y++)
				{
					Tile t = Main.tile[x, y];
					if (t.HasTile && t.TileType == TileID.OpenDoor && t.TileFrameY % 54 == 0 && t.TileFrameX % 36 == 0) n++;
				}
			return n;
		}

		private void DrPin(bool right)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = new Vector2((_cx + (right ? 30 : -30)) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
		}

		private void DrSpawn()
		{
			_drRecs.Clear();
			for (int i = 0; i < _drN; i++)
			{
				var rec = MakeRecord("Pvt. Door" + i);
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 at = new Vector2((_cx - 29 + i) * 16f, FloorY * 16f);
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				if (idx >= 0 && rec.HasBody) { var b = (SoldierNPC)Main.npc[idx].ModNPC; b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero; }
				_drRecs.Add(rec);
			}
		}

		private bool DrAllPast(bool right)
		{
			int have = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !(n.ModNPC is SoldierNPC)) continue;
				have++;
				float x = n.Center.X / 16f - _cx;
				if (right ? x < 9f : x > -9f) return false;
			}
			return have > 0;
		}

		private int DrCountPast(bool right)
		{
			int c = 0;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) { float x = Main.npc[i].Center.X / 16f - _cx; if (right ? x >= 9f : x <= -9f) c++; }
			return c;
		}

		private void StepDoors()
		{
			if (!_drStarted)
			{
				_drCase++;
				if (_drCase >= _drCases.Count) { Finish("doors scenario complete: " + _drSummary); return; }
				DrCase c = _drCases[_drCase];
				ClearAll(); DrBuild(c); DrPin(false); DrSpawn();
				_drLeg = 1; _drTick = 0; _drSettle = -1; _drStarted = true;
				Mod.Logger.Info($"HARNESS-DOORS: case {c.Name} built; doors={c.Doors}; levies spawned={_drRecs.Count}");
				return;
			}
			DrCase cs = _drCases[_drCase];
			bool right = _drLeg == 1;
			DrPin(right);
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false; }
			_drTick++;
			if (_drSettle < 0)
			{
				if (DrAllPast(right) || _drTick >= DrLegMaxTicks) { _drSettle = 0; Mod.Logger.Info($"HARNESS-DOORS: case {cs.Name} leg {_drLeg}: levies past={DrCountPast(right)}/{_drRecs.Count} after {_drTick} ticks; open now={DrCountOpen()}"); }
				return;
			}
			if (++_drSettle < DrSettleTicks) return;
			int open = DrCountOpen();
			Mod.Logger.Info($"HARNESS-DOORS: RESULT case={cs.Name} leg={_drLeg} doors={cs.Doors} openAfter10s={open} leviesPast={DrCountPast(right)}/{_drRecs.Count}");
			_drSummary += $"{cs.Name}/leg{_drLeg}={open}/{cs.Doors} ";
			if (_drLeg == 1) { _drLeg = 2; _drTick = 0; _drSettle = -1; }
			else _drStarted = false;
		}
	}
}
