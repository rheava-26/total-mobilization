using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// MOVEMENT SCENARIOS (LEVY_HARNESS_SCENARIO=platform). Replaces the weapon trials. A Follow soldier stands on a one-tile-thick platform row (very wide, so walking off an
	/// edge is not a way down) PlatformHeightTiles above the arena floor; the stand-in commander stands on the floor at several horizontal offsets. Records whether and how
	/// fast the soldier reaches the floor, one "HARNESS-MOVE" log line per trial. Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		internal static string Scenario;
		private const int PlatformHeightTiles = 8, PlatformHalfWidth = 60, ScenarioMaxTicks = 1500;
		// (commander offset in tiles from the arena centre, ledge): ledge = the platform ends just left of the soldier, so only the edge of his body is on it and the tile under his body-centre column is bare.
		private static readonly (int off, bool ledge, bool control, bool pin, bool lava)[] Trials = { (0, false, false, false, false), (3, false, false, false, false), (12, false, false, false, false), (0, true, false, false, false), (3, false, true, false, false), (0, false, false, true, false), (0, true, false, true, false), (0, false, false, true, true) };
		// pin = the soldier's X is held fixed every tick (no walking sideways), so the platform drop is the ONLY way down; with ledge the tile under his body-centre column is bare.
		// control = the commander stands ON the platform row: the soldier must stay up there (proves the platform is solid for him and he is not simply falling through).
		private const int LedgeCentrePx = 5; // soldier centre x = arena centre + this (hitbox overlaps the platform's last column; width/2 is 10)
		private int _scTrial, _scTick;
		private SoldierRecord _scRec; private int _scIdx = -1;
		private float _scStartX; private int _scLandTick = -1, _scFirstDrop = -1, _scWantTicks;

		private bool BeginScenario()
		{
			if (!string.Equals(Scenario, "platform", StringComparison.OrdinalIgnoreCase)) return false;
			_scTrial = 0; _scTick = -1;
			Mod.Logger.Info($"HARNESS-MOVE: platform scenario; platform row y={FloorY - PlatformHeightTiles} ({PlatformHeightTiles} tiles above the floor); trials={Trials.Length}; max {ScenarioMaxTicks} ticks");
			return true;
		}

		private void BuildPlatform(bool ledge, bool lava)
		{
			for (int x = _cx - 8; x <= _cx + 8; x++) { Tile f = Main.tile[x, FloorY - 1]; bool on = lava && Math.Abs(x - _cx) <= 3; f.LiquidAmount = (byte)(on ? 255 : 0); f.LiquidType = LiquidID.Lava; }
			int py = FloorY - PlatformHeightTiles;
			for (int x = _cx - PlatformHalfWidth - 2; x <= _cx + PlatformHalfWidth + 2; x++)
			{
				Tile t = Main.tile[x, py];
				bool on = ledge ? x <= _cx - 1 && x >= _cx - PlatformHalfWidth : x >= _cx - PlatformHalfWidth && x <= _cx + PlatformHalfWidth;
				t.HasTile = on; t.TileType = on ? TileID.Platforms : (ushort)0; t.TileFrameX = 0; t.TileFrameY = 0;
				if (on) t.BlockType = Terraria.ID.BlockType.Solid;
			}
		}

		private void ScenarioPinCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "LevyTester";
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(_cx * 16f + (Trials[_scTrial].ledge ? LedgeCentrePx : 8f) + Trials[_scTrial].off * 16f - p.width / 2f, (Trials[_scTrial].control ? FloorY - PlatformHeightTiles : FloorY) * 16f - p.height);
			p.velocity = Vector2.Zero;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && !(n.ModNPC is SoldierNPC) && !n.townNPC && !n.friendly) n.active = false;
			}
		}

		private void StepScenario()
		{
			if (_scTrial >= Trials.Length) { Finish("platform scenario complete"); return; }
			if (_scTick < 0)
			{
				ClearAll();
				BuildPlatform(Trials[_scTrial].ledge, Trials[_scTrial].lava);
				var rec = MakeRecord("Pvt. Drop");
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
				RosterSystem.Instance.Regulars.Add(rec);
				_scRec = rec;
				Vector2 at = new Vector2(_cx * 16f + (Trials[_scTrial].ledge ? LedgeCentrePx : 8f), (FloorY - PlatformHeightTiles) * 16f);
				_scIdx = RosterSystem.Instance.SpawnBody(rec, at);
				if (_scIdx < 0 || !rec.HasBody) { Mod.Logger.Warn("HARNESS-MOVE: SpawnBody failed"); _scTrial++; return; }
				var b = (SoldierNPC)Main.npc[_scIdx].ModNPC;
				b.NPC.Bottom = at; b.NPC.velocity = Vector2.Zero;
				_scStartX = b.NPC.Center.X; _scLandTick = -1; _scFirstDrop = -1; _scWantTicks = 0; _scTick = 0;
				ScenarioPinCommander();
				return;
			}
			ScenarioPinCommander();
			_scTick++;
			if (Trials[_scTrial].pin && Main.npc[_scIdx].active) { var pn = Main.npc[_scIdx]; pn.position.X = _scStartX - pn.width / 2f; pn.velocity.X = 0f; }
			NPC n = Main.npc[_scIdx];
			bool alive = n.active && n.ModNPC is SoldierNPC;
			if (alive && ((SoldierNPC)n.ModNPC).WantsToFallThrough) _scWantTicks++;
			if (alive && _scFirstDrop < 0 && n.Bottom.Y > (FloorY - PlatformHeightTiles) * 16f + 4f) _scFirstDrop = _scTick;
			if (alive && _scLandTick < 0 && n.Bottom.Y >= FloorY * 16f - 8f) _scLandTick = _scTick;
			// a short margin on the floor, then report
			if (!alive || _scTick >= (Trials[_scTrial].control ? 400 : ScenarioMaxTicks) || (_scLandTick >= 0 && _scTick >= _scLandTick + 30))
			{
				float dxTiles = alive ? Math.Abs(n.Center.X - _scStartX) / 16f : -1f;
				string st = alive ? ((SoldierNPC)n.ModNPC).State.ToString() : "dead";
				string land = _scLandTick >= 0 ? (_scLandTick / 60f).ToString("0.0") + "s" : "-";
				string bot = alive ? (n.Bottom.Y / 16f).ToString("0.0") : "-";
				Mod.Logger.Info($"HARNESS-MOVE: offset={Trials[_scTrial].off}t ledge={Trials[_scTrial].ledge} control={Trials[_scTrial].control} pinX={Trials[_scTrial].pin} lavaBelow={Trials[_scTrial].lava} firstDropTick={_scFirstDrop} wantFallThroughTicks={_scWantTicks} result={(_scLandTick >= 0 ? "REACHED_FLOOR" : "STUCK_ON_PLATFORM")} landTick={_scLandTick} ({land}) sidewaysTravel={dxTiles:0.0}t finalBottomY={bot} state={st}");
				_scTrial++; _scTick = -1;
			}
		}
	}
}
