using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// DARK GATE SCENARIO (LEVY_HARNESS_SCENARIO=gate; needs ThoriumMod). Measurement only. Each case builds a flat range, pins the stand-in commander, places gateways where the case says, and checks
	/// after 240 ticks whether the soldier stepped through (USED) and/or placed a pair (PLACED) as the PORTAL_USE rules require. One HARNESS-GATE line per case with PASS or FAIL.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class GateCase
		{
			public string Name; public bool ExpectUse, ExpectPlace;
			public float GateA = -50, GateB = 0, Cmd = 2, Soldier = -50; // tile offsets from the range centre
			public float SoldierHp = 1f, CmdHp = 1f;
			public bool Chaos, CmdHoldsGate, Tough, Holds, PlayerPair, NoGates;
		}

		private static readonly GateCase[] GateCases =
		{
			new() { Name = "retreat (30% hp, partner by the commander)", SoldierHp = 0.3f, ExpectUse = true },
			new() { Name = "regroup (full hp, 53 tiles out, partner by the commander)", Cmd = 3, ExpectUse = true },
			new() { Name = "no need (full hp, 10 tiles from the commander)", Cmd = -40, GateB = -40, ExpectUse = false },
			new() { Name = "commander within 6 tiles of the gate (YIELD)", SoldierHp = 0.3f, Cmd = -46, GateB = -45, ExpectUse = false },
			new() { Name = "control: commander 20 tiles from the gate", SoldierHp = 0.3f, Cmd = -30, GateB = -30, ExpectUse = true },
			new() { Name = "commander has ChaosState (YIELD)", SoldierHp = 0.3f, Chaos = true, ExpectUse = false },
			new() { Name = "commander holds a Dark Gate (YIELD)", SoldierHp = 0.3f, CmdHoldsGate = true, ExpectUse = false },
			new() { Name = "tough enemy within 12 tiles (no leaving a fight)", SoldierHp = 0.3f, Tough = true, ExpectUse = false },
			new() { Name = "place (wounded commander 25 tiles away, no gates out)", Holds = true, NoGates = true, Soldier = -30, Cmd = -5, CmdHp = 0.4f, ExpectPlace = true },
			new() { Name = "place blocked: the player already owns a pair", Holds = true, PlayerPair = true, Soldier = -30, Cmd = -5, CmdHp = 0.4f, GateA = 60, GateB = 70, ExpectPlace = false },
			new() { Name = "place blocked: commander holds a Dark Gate", Holds = true, NoGates = true, CmdHoldsGate = true, Soldier = -30, Cmd = -5, CmdHp = 0.4f, ExpectPlace = false },
			new() { Name = "place blocked: nobody wounded", Holds = true, NoGates = true, Soldier = -30, Cmd = -5, CmdHp = 1f, ExpectPlace = false },
		};

		private int _gI = -1, _gTick, _gBody, _gTarget, _gUse0, _gPlace0; private bool _gStarted; private SoldierRecord _gRec;
		private const int GateTicks = 240;

		private void GateLog(string s)
		{
			Mod.Logger.Info(s);
			try { System.IO.File.AppendAllText(OutPath + ".gate.txt", s + "\n"); } catch (Exception) { }
		}

		private bool BeginGate()
		{
			if (!ThoriumCompatSystem.Active) { Mod.Logger.Warn("HARNESS-GATE: needs ThoriumMod"); return false; }
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; } catch (Exception e) { Mod.Logger.Warn("HARNESS-GATE: could not hook UpdateConnectedClients: " + e.Message); }
			_gI = 0; _gStarted = false;
			return true;
		}

		[MethodImpl(MethodImplOptions.NoInlining)] private static Item GateItem() => DarkGateCompat.TestGateItem();
		[MethodImpl(MethodImplOptions.NoInlining)] private static void GateChaos(Player p, bool on) => DarkGateCompat.TestSetChaos(p, on);
		[MethodImpl(MethodImplOptions.NoInlining)] private static void GateClear() => DarkGateCompat.TestClearGates();
		[MethodImpl(MethodImplOptions.NoInlining)] private static int GateCount(int by = int.MinValue) => DarkGateCompat.TestGateCount(by);
		[MethodImpl(MethodImplOptions.NoInlining)] private static void GatePair(Player o, Vector2 a, Vector2 b) => DarkGateCompat.TestSpawnPair(o, a, b);
		[MethodImpl(MethodImplOptions.NoInlining)] private static int GateUsed() => DarkGateCompat.UsedCount;
		[MethodImpl(MethodImplOptions.NoInlining)] private static int GatePlaced() => DarkGateCompat.PlacedCount;

		private void GatePinCommander(GateCase c)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = (int)(500 * c.CmdHp); p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2((_cx + c.Cmd) * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			if (c.CmdHoldsGate) { p.inventory[0] = GateItem(); p.selectedItem = 0; } else if (p.inventory[0] != null && !p.inventory[0].IsAir && p.inventory[0].ModItem != null) p.inventory[0] = new Item();
			GateChaos(p, c.Chaos);
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				n.active = false;
			}
		}

		private void StepGate()
		{
			if (_gI < 0 || _gI >= GateCases.Length) { Finish("gate scenario complete"); return; }
			GateCase c = GateCases[_gI];
			if (!_gStarted)
			{
				ClearAll(); SpBuildFloor("run"); GateClear();
				GatePinCommander(c);
				if (!c.NoGates)
				{
					Vector2 a = new Vector2((_cx + c.GateA) * 16f, FloorY * 16f - 40f), b = new Vector2((_cx + c.GateB) * 16f, FloorY * 16f - 40f);
					GatePair(Main.player[0], a, b);
				}
				_gRec = MakeRecord("Pvt. Gate");
				var gear = _gRec.Gear;
				if (c.Holds)
				{
					Item gi = GateItem();
					if (gear.CanHold(SoldierGear.SlotWeapon, gi)) { gear.Weapon = gi; gear.SetLocked(SoldierGear.SlotWeapon, true); }
					else GateLog("HARNESS-GATE: DarkGate CanHold false in the weapon slot");
				}
				_gRec.LoadoutLocked = true; _gRec.Order = SoldierOrder.Hold; _gRec.HoldFire = true; _gRec.Mana = _gRec.MaxMana;
				_gRec.CurrentHp = Math.Max(1, (int)(_gRec.MaxHp * c.SoldierHp));
				RosterSystem.Instance.Regulars.Add(_gRec);
				Vector2 at = new Vector2((_cx + c.Soldier) * 16f, FloorY * 16f);
				_gBody = RosterSystem.Instance.SpawnBody(_gRec, at);
				if (_gBody < 0 || !_gRec.HasBody) { GateLog("HARNESS-GATE: SpawnBody failed"); _gI++; return; }
				Main.npc[_gBody].Bottom = at; Main.npc[_gBody].velocity = Vector2.Zero; Main.npc[_gBody].life = _gRec.CurrentHp;
				_gTarget = -1;
				if (c.Tough) { _gTarget = SpSpawnTarget((_cx + c.Soldier + 8) * 16f, 48, 48, 0); }
				_gUse0 = GateUsed(); _gPlace0 = GatePlaced();
				_gStarted = true; _gTick = 0;
				return;
			}
			GatePinCommander(c);
			NPC s = Main.npc[_gBody];
			if (s.active && !c.Holds) { s.Bottom = new Vector2(s.Center.X, s.Bottom.Y); }
			if (++_gTick < GateTicks) return;
			int used = GateUsed() - _gUse0, placed = GatePlaced() - _gPlace0;
			float finalX = s.active ? (s.Center.X / 16f) - _cx : float.NaN;
			bool ok = c.ExpectPlace ? (placed >= 1) : placed == 0;
			if (!c.ExpectPlace && c.ExpectUse != (used >= 1)) ok = false;
			if (c.ExpectPlace && c.Name.Contains("place (")) ok &= GateCount(_gBody) == 2;
			if (c.PlayerPair) ok &= GateCount(-1) == 2; // the player's pair is untouched
			GateLog($"HARNESS-GATE: {(ok ? "PASS" : "FAIL")} | {c.Name} | used={used} placed={placed} soldierX={finalX:0.0} (started {c.Soldier:0}) hp={s.life}/{s.lifeMax} gatesTotal={GateCount()} byThisSoldier={GateCount(_gBody)} playerOwned={GateCount(-1)}");
			_gI++; _gStarted = false;
		}
	}
}
