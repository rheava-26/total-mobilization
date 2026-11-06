using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// SPEED SCENARIO (LEVY_HARNESS_SCENARIO=speed). Measurement only. For each boots loadout: (1) a stand-in PLAYER wearing the same boots, simulated with vanilla's own
	/// Player.HorizontalMovement on flat ground (no collision), holding right; (2) a Follow levy chasing a commander carrot 45 tiles ahead, unarmed and with nothing to shoot
	/// ("run"), and armed with a target pinned mid-path ("runshoot"); (3) a WALL-OF-FLESH-LIKE hazard (a tall hostile moving left at a fixed px/tick) closing on a levy that flees
	/// left after a commander carrot, unarmed and armed. One "HARNESS-SPEED" log line per trial. LEVY_SPEED_FILTER (comma list of substrings of the trial label) narrows the run.
	/// </summary>
	public partial class CombatHarness
	{
		private static readonly (string name, int[] item)[] SpeedLoadouts = { ("none", new int[0]), ("Hermes", new int[] { ItemID.HermesBoots }), ("Lightning", new int[] { ItemID.LightningBoots }), ("Terraspark", new int[] { ItemID.TerrasparkBoots }), ("TerraMagi", new int[] { ItemID.TerrasparkBoots, ItemID.Magiluminescence }), ("NoneSwift", new int[] { -1 }), ("HermesSwift", new int[] { ItemID.HermesBoots, -1 }), ("TerraSwift", new int[] { ItemID.TerrasparkBoots, -1 }) }; // -1 = a Swiftness potion (+0.25 moveSpeed)
		private sealed class SpeedTrial { public string Label; public int[] Boots; public string Kind; public float HazV; public bool Armed; }
		private readonly List<SpeedTrial> _spTrials = new();
		private int _spI = -1, _spTick;
		private bool _spStarted;
		private float _spX0, _spPeak, _spSumV; private float _spCloseSum; private int _spCloseN, _spCloseStill; private int _spT100, _spStill, _spContact, _spEnd;
		private float _spMinGap; private int _spHazIdx = -1, _spTargetIdx = -1; private string _spSamples;
		private SoldierRecord _spRec; private int _spBody;
		private const float SpCarrotTiles = 45f;
		private const int SpHazStartTick = 90; // the hazard holds still for the first 90 ticks (a levy's spawn grace is about 45), then advances
		private static readonly float[] SpHazSpeeds = ParseHaz();
		private static float[] ParseHaz()
		{
			string s = Environment.GetEnvironmentVariable("LEVY_SPEED_HAZ");
			if (string.IsNullOrWhiteSpace(s)) return new[] { 1.5f, 2.5f, 3.4f };
			var l = new List<float>();
			foreach (var p in s.Split(',', StringSplitOptions.RemoveEmptyEntries)) l.Add(float.Parse(p.Trim(), System.Globalization.CultureInfo.InvariantCulture));
			return l.ToArray();
		}
		private static readonly bool _spTrace = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_SPEED_TRACE"));

		/// <summary>The server drops a client-less player slot during the player pass, so every soldier think saw no commander (Idle) in the speed trials. Keep the stand-in up for the NPC pass. Measurement artifact only.</summary>
		private static int _dbgPreOff, _dbgPostOff, _dbgPre;
		public override void PreUpdateNPCs()
		{
			if (Enabled && _ph == Ph.Scenario && (string.Equals(Scenario, "speed", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "banner", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "gate", StringComparison.OrdinalIgnoreCase))) { Player sp = Main.player[0]; if (sp.name == CommanderName) { _dbgPre++; if (!sp.active) _dbgPreOff++; sp.active = true; sp.dead = false; } }
		}
		public override void PostUpdateNPCs()
		{
			if (Enabled && _ph == Ph.Scenario && (string.Equals(Scenario, "speed", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "banner", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "gate", StringComparison.OrdinalIgnoreCase))) { Player sp = Main.player[0]; if (sp.name == CommanderName && !sp.active) _dbgPostOff++; }
		}

		private delegate void OrigUpdateConnectedClients();
		private delegate void HookUpdateConnectedClients(OrigUpdateConnectedClients orig);
		private static void KeepStandinDetour(OrigUpdateConnectedClients orig)
		{
			// the server loop thread (Netplay.UpdateConnectedClients) drops every client-less player slot many times a second, racing the world update the harness drives on the
			// main thread; a soldier that thinks inside that window sees no commander and goes Idle. Keep the stand-in commander's slot as it was.
			if (_spHooked && Enabled && (string.Equals(Scenario, "speed", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "banner", StringComparison.OrdinalIgnoreCase) || string.Equals(Scenario, "gate", StringComparison.OrdinalIgnoreCase))) return; // no real clients exist in a harness run: skip the pass that drops the stand-in
			orig();
		}
		private static bool _spHooked;

		private bool BeginSpeed()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; }
			catch (Exception e) { Mod.Logger.Warn("HARNESS-SPEED: could not hook UpdateConnectedClients: " + e.Message); }
			string flt = Environment.GetEnvironmentVariable("LEVY_SPEED_FILTER");
			string[] f = string.IsNullOrWhiteSpace(flt) ? null : flt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			foreach (var (name, item) in SpeedLoadouts)
			{
				var kinds = new List<(string k, float v, bool armed)> { ("run", 0, false), ("bridge", 0, false), ("bumps", 0, false), ("gaps", 0, false), ("runshoot", 0, true) };
				foreach (float v in SpHazSpeeds) { kinds.Add(("haz", v, false)); kinds.Add(("haz", v, true)); }
				foreach (var (k, v, armed) in kinds)
				{
					string label = name + "/" + k + (v > 0 ? v.ToString("0.0") : "") + (k == "haz" ? (armed ? "/armed" : "/unarmed") : "");
					if (f != null && !Array.Exists(f, s => label.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
					_spTrials.Add(new SpeedTrial { Label = label, Boots = item, Kind = k, HazV = v, Armed = armed });
				}
			}
			// the stand-in player reference, simulated once per loadout (instant)
			foreach (var (name, item) in SpeedLoadouts)
			{
				try { RefPlayer(name, item); }
				catch (Exception e) { Mod.Logger.Warn("HARNESS-SPEED: player ref failed for " + name + ": " + e); }
			}
			_spI = 0; _spStarted = false;
			Mod.Logger.Info($"HARNESS-SPEED: {_spTrials.Count} levy trials");
			return true;
		}

		private void RefPlayer(string name, int[] boots)
		{
			var p = new Player();
			p.whoAmI = SoldierNPC.DummySlot; p.active = true; p.width = 20; p.height = 42; p.direction = 1;
			bool swift = Array.IndexOf(boots, -1) >= 0; int ai = 0;
			for (int bi = 0; bi < boots.Length; bi++) { if (boots[bi] < 0) continue; var it = new Item(); it.SetDefaults(boots[bi]); p.armor[3 + ai++] = it; }
			p.ResetEffects();
			p.UpdateEquips(SoldierNPC.DummySlot);
			if (swift) p.moveSpeed += 0.25f; // vanilla Swiftness buff
			float accBeforeScale = p.accRunSpeed, maxBefore = p.maxRunSpeed, ms = p.moveSpeed, ra = p.runAcceleration;
			// the run-speed part of Player.Update that follows UpdateEquips (Player.cs ~23009 and ~23440-23574): moveSpeed scaling, then the flag multipliers, then the mod hook
			p.runAcceleration *= p.moveSpeed; p.maxRunSpeed *= p.moveSpeed; // not accRunSpeed
			if (p.hasMagiluminescence) { p.runAcceleration *= 1.75f; p.maxRunSpeed *= 1.15f; p.accRunSpeed *= 1.15f; p.runSlowdown *= 1.75f; }
			if (p.shadowArmor) { p.runAcceleration *= 1.75f; p.maxRunSpeed *= 1.15f; p.accRunSpeed *= 1.15f; p.runSlowdown *= 1.75f; }
			PlayerLoader.PostUpdateRunSpeeds(p);
			p.controlRight = true; p.velocity = Vector2.Zero;
			float x = 0f, peak = 0f; int t100 = -1; var sb = new StringBuilder();
			for (int t = 1; t <= 1800; t++)
			{
				p.HorizontalMovement();
				x += p.velocity.X; peak = Math.Max(peak, p.velocity.X);
				if (t100 < 0 && x >= 1600f) t100 = t;
				if (t == 30 || t == 60 || t == 120 || t == 240 || t == 480) sb.Append($" v@{t}={p.velocity.X:0.00}");
			}
			// flee from the hazard: the player starts at rest 30 tiles in front of it and runs left; the hazard holds 90 ticks then advances (same geometry as the levy trials)
			var fl = new StringBuilder();
			foreach (float hv in SpHazSpeeds)
			{
				p.velocity = Vector2.Zero; p.controlRight = false; p.controlLeft = true; p.direction = -1;
				float px = 0f, hx = 480f, minGap = float.MaxValue; string outcome = "TIMEOUT";
				for (int t = 1; t <= 1400; t++)
				{
					p.HorizontalMovement(); px += p.velocity.X; if (t > SpHazStartTick) hx -= hv;
					minGap = Math.Min(minGap, (hx - px - 70f) / 16f);
					if (hx - px <= 70f) { outcome = "CAUGHT@" + t; break; }
					if (px <= -2464f) { outcome = "ESCAPED@" + t; break; }
				}
				fl.Append($" flee{hv:0.0}={outcome}(minGap {minGap:0.0}t)");
			}
			p.controlLeft = false; p.controlRight = true; p.velocity = Vector2.Zero;
			Mod.Logger.Info($"HARNESS-SPEED: PLAYERFLEE {name}:{fl}");
			Mod.Logger.Info($"HARNESS-SPEED: PLAYER {name}: maxRunSpeed(raw {maxBefore:0.00} x moveSpeed {ms:0.00})={p.maxRunSpeed:0.00} accRunSpeed={accBeforeScale:0.00} runAccel={ra:0.000}->{p.runAcceleration:0.000} peak={peak:0.00}px/tick t100tiles={t100} ticks ({(t100 > 0 ? 1600f / t100 : 0):0.00} px/tick avg){sb}");
		}

		private void SpPinStandins(float cmdX, int dir)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(cmdX - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero; p.direction = dir;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				n.active = false;
			}
		}

		/// <summary>Solid floor, or a hellbridge: a one-tile platform row at the floor line with lava in the two rows under it.</summary>
		private void SpBuildFloor(string kind)
		{
			bool bridge = kind == "bridge" || kind == "gaps";
			for (int x = _cx - HalfWidth - 2; x <= _cx + HalfWidth + 2; x++)
			{
				int rel = x - _cx;
				// "bumps": a one-tile step up for five columns out of every ten (a step the body must climb and descend); "gaps": a three-tile hole in the bridge every 25 columns (must be jumped)
				Tile above = Main.tile[x, FloorY - 1];
				bool bump = kind == "bumps" && ((rel % 10) + 10) % 10 >= 5;
				above.HasTile = bump; above.TileType = bump ? TileID.GrayBrick : (ushort)0; above.BlockType = Terraria.ID.BlockType.Solid; above.TileFrameX = 0; above.TileFrameY = 0;
				Tile top = Main.tile[x, FloorY];
				bool hole = kind == "gaps" && ((rel % 25) + 25) % 25 < 3 && Math.Abs(rel) < 98;
				top.HasTile = !hole; top.TileType = bridge ? TileID.Platforms : TileID.GrayBrick; top.BlockType = Terraria.ID.BlockType.Solid; top.TileFrameX = 0; top.TileFrameY = 0;
				for (int y = FloorY + 1; y <= FloorY + 2; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = !bridge; t.TileType = bridge ? (ushort)0 : TileID.GrayBrick; t.BlockType = Terraria.ID.BlockType.Solid;
					t.LiquidAmount = (byte)(bridge ? 255 : 0); t.LiquidType = LiquidID.Lava;
				}
			}
		}

		private int SpSpawnTarget(float centerX, int w, int h, int damage, int npcType = NPCID.GreenSlime)
		{
			Vector2 pos = new Vector2(centerX, FloorY * 16f);
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyHarness"), (int)pos.X, (int)pos.Y, npcType);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("target NewNPC failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>();
			ht.IsTarget = true;
			n.lifeMax = 1_000_000; n.life = 1_000_000; n.defense = 0; n.damage = damage; n.knockBackResist = 0f;
			n.width = w; n.height = h; n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f;
			n.velocity = Vector2.Zero; n.Bottom = pos; ht.Pin = n.position; n.netUpdate = true;
			return i;
		}

		private void StepSpeed()
		{
			if (_spI >= _spTrials.Count) { Finish("speed scenario complete"); return; }
			SpeedTrial tr = _spTrials[_spI];
			bool haz = tr.Kind == "haz";
			if (!_spStarted)
			{
				ClearAll(); SpBuildFloor(tr.Kind);
				_spStarted = true; _spTick = 0; _spPeak = 0; _spSumV = 0; _spCloseSum = 0; _spCloseN = 0; _spCloseStill = 0; _spT100 = -1; _spStill = 0; _spContact = -1; _spEnd = -1; _spMinGap = float.MaxValue; _spSamples = "";
				var rec = MakeRecord("Pvt. Speed");
				var gear = rec.Gear;
				if (tr.Boots.Length > 1) rec.Grade = Grade.Champion; // a Levy-grade soldier has one accessory slot; the stack loadouts need the Champion's five
				int ai = 0;
				for (int bi = 0; bi < tr.Boots.Length; bi++) { if (tr.Boots[bi] < 0) continue; var b = new Item(); b.SetDefaults(tr.Boots[bi]); if (gear.CanHold(SoldierGear.SlotAcc0 + ai, b)) { gear.Set(SoldierGear.SlotAcc0 + ai, b); gear.SetLocked(SoldierGear.SlotAcc0 + ai, true); } else Mod.Logger.Warn("HARNESS-SPEED: accessory CanHold false"); ai++; }
				if (tr.Armed)
				{
					var w = new Item(); w.SetDefaults(ItemID.Handgun);
					if (gear.CanHold(SoldierGear.SlotWeapon, w)) { gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true); }
					Item ammo = FindAmmo(w, ""); if (ammo != null && gear.CanHold(SoldierGear.SlotAmmo, ammo)) { gear.Ammo = ammo; gear.SetLocked(SoldierGear.SlotAmmo, true); }
				}
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec); _spRec = rec;
				float startTile = haz ? _cx + 60 : _cx - 96;
				Vector2 at = new Vector2(startTile * 16f, FloorY * 16f);
				SpPinStandins(at.X + (haz ? -SpCarrotTiles : SpCarrotTiles) * 16f, haz ? -1 : 1);
				_spBody = RosterSystem.Instance.SpawnBody(rec, at);
				if (_spBody < 0 || !rec.HasBody) { Mod.Logger.Warn("HARNESS-SPEED: SpawnBody failed for " + tr.Label); _spI++; _spStarted = false; return; }
				var sn = (SoldierNPC)Main.npc[_spBody].ModNPC; sn.NPC.Bottom = at; sn.NPC.velocity = Vector2.Zero;
				_spX0 = sn.NPC.Center.X;
				if (Array.IndexOf(tr.Boots, -1) >= 0) { sn.NPC.AddBuff(BuffID.Swiftness, 60 * 600); sn.StatsDirty = true; }
				_spHazIdx = -1; _spTargetIdx = -1;
				if (tr.Kind == "runshoot") _spTargetIdx = SpSpawnTarget((_cx - 50) * 16f, 64, 96, 0);
				if (haz) _spHazIdx = SpSpawnTarget((_cx + 90) * 16f, 120, 480, 120);
				return;
			}
			NPC n = Main.npc[_spBody];
			bool alive = n.active && n.ModNPC is SoldierNPC;
			_spTick++;
			if (alive)
			{
				var sn = (SoldierNPC)n.ModNPC;
				float cx = n.Center.X;
				if (!haz) SpPinStandins(Math.Min(cx + SpCarrotTiles * 16f, (_cx + 98) * 16f), 1);
				else SpPinStandins(Math.Max(cx - SpCarrotTiles * 16f, (_cx - 98) * 16f), -1);
				float vx = Math.Abs(n.velocity.X);
				if (_spTrace && _spTick % 15 == 0) Mod.Logger.Info($"HARNESS-SPEED-DBG pre={_dbgPre} preOff={_dbgPreOff} postOff={_dbgPostOff}");
				if (_spTrace && _spTick % 15 == 0) Mod.Logger.Info($"HARNESS-SPEED-TRACE {tr.Label} t={_spTick} x={(cx - _spX0) / 16f:0.0}t vx={n.velocity.X:0.00} vy={n.velocity.Y:0.00} state={sn.State} goal={(sn.HasMoveGoal ? ((sn.MoveGoal.X - cx) / 16f).ToString("0.0") + "t" : "none")} tgt={sn.TargetIndex} cmd={(sn.Commander == null ? "NULL" : "ok")} cmdIdx={sn.CommanderIndex} p0active={Main.player[0].active} hp={n.life}/{n.lifeMax} fear={sn.Record.Fear:0.0}");
				_spPeak = Math.Max(_spPeak, vx);
				if (vx < 0.5f) _spStill++;
				if (!haz)
				{
					if (_spT100 < 0 && Math.Abs(cx - _spX0) >= 1600f) _spT100 = _spTick;
					if (_spTick == 30 || _spTick == 60 || _spTick == 120 || _spTick == 240 || _spTick == 480) _spSamples += $" v@{_spTick}={n.velocity.X:0.00}";
				}
				else
				{
					NPC hz = Main.npc[_spHazIdx];
					if (hz.active && hz.TryGetGlobalNPC(out HarnessTarget ht)) { if (_spTick > SpHazStartTick) ht.Pin.X -= tr.HazV; hz.position = ht.Pin; }
					float gap = (hz.Center.X - cx) / 16f; // tiles between centres, > 0 while the hazard is still to the right
					_spMinGap = Math.Min(_spMinGap, gap - (hz.width / 2f + n.width / 2f) / 16f);
					if (_spContact < 0 && hz.Center.X - cx <= (hz.width / 2f + n.width / 2f)) _spContact = _spTick;
					if (_spContact < 0) { _spSumV += vx; if (_spTick > SpHazStartTick && gap - (hz.width / 2f + n.width / 2f) / 16f < 40f) { _spCloseN++; _spCloseSum += vx; if (vx < 0.5f) _spCloseStill++; } }
					if (_spEnd < 0 && cx <= (_cx - 94) * 16f) _spEnd = _spTick;
				}
			}
			int maxTicks = haz ? 1400 : 1100;
			bool done = !alive || _spTick >= maxTicks || (!haz && _spT100 >= 0) || (haz && (_spContact >= 0 || _spEnd >= 0));
			if (!done) return;
			string state = alive ? ((SoldierNPC)n.ModNPC).State.ToString() : "dead";
			string ab = alive ? $"abil.MaxRun={((SoldierNPC)n.ModNPC).Abilities.MaxRunSpeed:0.00} abil.Walk={((SoldierNPC)n.ModNPC).Abilities.WalkSpeed:0.00}" : "";
			if (!haz)
				Mod.Logger.Info($"HARNESS-SPEED: LEVY {tr.Label}: {ab} peak={_spPeak:0.00}px/tick t100tiles={(_spT100 >= 0 ? _spT100.ToString() : "NOT REACHED in " + _spTick)} ticks ({(_spT100 > 0 ? 1600f / _spT100 : Math.Abs(n.Center.X - _spX0) / _spTick):0.00} px/tick avg) distMoved={Math.Abs(n.Center.X - _spX0) / 16f:0.0}t stillTicks={_spStill} state={state}{_spSamples}");
			else
				Mod.Logger.Info($"HARNESS-SPEED: LEVY {tr.Label}: {ab} peak={_spPeak:0.00}px/tick outcome={(_spContact >= 0 ? "CAUGHT@" + _spContact : _spEnd >= 0 ? "ESCAPED@" + _spEnd : "TIMEOUT@" + _spTick)} minGap={_spMinGap:0.0}t avgSpeedBeforeEnd={(_spSumV / Math.Max(1, _spContact >= 0 ? _spContact : _spTick)):0.00} closeTicks={_spCloseN} closeAvgV={(_spCloseSum / Math.Max(1, _spCloseN)):0.00} closeStill={(100f * _spCloseStill / Math.Max(1, _spCloseN)):0}% state={state}");
			_spI++; _spStarted = false;
		}
	}
}
