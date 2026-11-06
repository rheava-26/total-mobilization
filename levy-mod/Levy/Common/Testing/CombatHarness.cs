using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Common.Compat.Thorium;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HEADLESS COMBAT HARNESS (measurement only; changes no game behaviour). Does NOTHING unless the environment variable LEVY_HARNESS is set (read once, at Load),
	/// and only on a server / single-player world (never a multiplayer client). Run it through tools/run_harness.sh.
	///
	/// WHAT IT DOES
	///  1. FAKE COMMANDER: marks Main.player[0] active + alive and named "LevyTester" (hp 500/500, mana 200) and pins its position at the test site, so
	///     LevyUtil.FindPlayerByName / SoldierNPC.Commander / RosterSystem.ResolveCommanders / CompatAllies.CommanderOf / healer + bard ally searches behave as if a
	///     player were present. It has no network client, no inventory use, no input. Its life is only touched at the start of a healer/bard trial (set to 50%).
	///  2. TEST RANGE: a sealed-off box high in the sky (rows 60..119 cleared, walls/liquid removed, floor rows 120..123 of gray brick, 201 tiles wide, centred on the
	///     world's middle column), so terrain never confounds a trial. Vanilla spawning is suppressed (HarnessSpawnSuppress) and stray hostiles are removed each tick.
	///  3. TRIALS: one soldier per (weapon, range) via the mod's real path (a SoldierRecord added to RosterSystem.Regulars + RosterSystem.SpawnBody, so the genuine
	///     SoldierNPC AI runs). Target = a pinned, huge-HP Green Slime (NOT the Target Dummy: SoldierTargeting.IsNeverTarget lists NPCID.TargetDummy), at 10 / 25 / 40 tiles.
	///     Each trial: SETUP ticks, then RUN ticks (default 360 = 6 s of game time), then cleanup. Healer/bard trials also get a wounded ally soldier and a 50% commander.
	///  4. RECORDS one TSV row per (weapon, range) to LEVY_HARNESS_OUT and one summary line per weapon to server.log, then exits the server WITHOUT saving the world.
	///
	/// ENV VARS: LEVY_HARNESS (enable), LEVY_HARNESS_OUT (TSV path), LEVY_HARNESS_MOD (mod whose items are tested, default ThoriumMod), LEVY_HARNESS_FILTER (comma list of
	/// case-insensitive substrings of item name / internal name), LEVY_HARNESS_TICKS (run ticks per range, default 360), LEVY_HARNESS_SHARD ("k/n": only weapons whose index % n == k),
	/// LEVY_HARNESS_SKIPCONTROLS (any value: skip the vanilla controls), LEVY_HARNESS_RANGES (comma list of tile distances, default 10,25,40),
	/// LEVY_HARNESS_SCENARIO ("platform": run the platform-drop movement scenario instead of the weapon trials; see CombatHarness.Movement.cs).
	/// </summary>
	public partial class CombatHarness : ModSystem
	{
		internal static bool Enabled;
		internal static string OutPath, TestMod;
		private static string[] _filter;
		private static int _runTicks = 360, _shardK = 0, _shardN = 1;
		private static bool _skipControls;
		// ---- ally-heal scenario switches (all default off = the original behaviour) ----
		// LEVY_HARNESS_AIM=enemy        a heal-class weapon in the PLAYER baseline is aimed at the enemy (not at the wounded ally), so on-hit "heals allies" effects fire
		// LEVY_HARNESS_ALLYHP=<percent> start health of the wounded ally soldier (default 30; above 75 a soldier healer's own AI abstains, so only Thorium's own effect can heal him)
		// LEVY_HARNESS_ONLYLEVIES=1     the stand-in commander is at full health and there is no second stand-in player: the only wounded ally is the soldier
		// LEVY_HARNESS_SPSWAP=1         the SOLDIER window updates Thorium projectiles owned by the commander with Main.myPlayer = the commander, as in single player (a dedicated server has no local player)
		// LEVY_HARNESS_VANILLA=<names|ids>  extra VANILLA items to test (e.g. "Vampire Knives"); LEVY_HARNESS_COMPANION=1 gives every trial a wounded ally + wounded commander (non-healer weapons too)
		// LEVY_HARNESS_CMDHP=<percent>  start health of the commander in a companion trial (default 50; 100 = full)   LEVY_HARNESS_SELFHP=<percent>  start health of the SHOOTING soldier (default 100)
		// LEVY_HARNESS_ACC=<ModItem names in the tested mod>  accessories worn by the shooting soldier AND the baseline player (e.g. MedicalBag: +1 heal bonus, so a heal-bonus orb heals at all)
		// LEVY_HARNESS_VANPROBE=1  the shooting soldier's hit also spawns vanilla's own lifesteal bolt (VampireHeal, type 305, 8 life) at the target every 25 ticks, so vanilla's lifesteal arrival (statLife += on the owner) is exercised directly (Levy's soldiers throw their own SoldierVampireKnife, which never spawns it)
		// LEVY_HARNESS_ORBDIRECT=1  every 40 ticks drop a Thorium healing orb 3-7 tiles from the wounded ally, cast by the soldier (soldier window) or by the stand-in commander (baseline window); the orb count is in the medbag column
		// LEVY_HARNESS_ORBPROBE=1  follow every Thorium healing orb: how close to the wounded ally it came and how it ended (in the medbag column)
		// LEVY_HARNESS_MEDBAG=1  record the Medical Bag arrow/pick state of the commander each tick of the window (needs LEVY_HARNESS_ACC=MedicalBag)
		// LEVY_HARNESS_NOWALK=1 / LEVY_HARNESS_NOBASE=1  drop the walk-in trial / the player baseline window.   LEVY_HARNESS_HEALAIM=ally  a soldier-window heal-class weapon targets the wounded ally (not the enemy)
		private static bool _aimEnemy, _onlyLevies, _spSwap, _forceCompanion, _noWalk, _noBase;
		private static int _allyHpPct = 30, _cmdHpPct = -1, _selfHpPct = 100;
		private static string[] _vanilla, _accNames;
		private static bool _medBag, _vanProbe, _orbProbe, _orbDirect;
		private static int _orbDirectN;
		private static string[] _classFilter, _armorNames;
		private static int[] _ranges = { 4, 7, 15, 25, 40 };
		private const int WalkTicks = 1300;          // walk-in window: target walks 40 tiles at ~2 tiles/s (0.5333 px/tick)
		private const float WalkPxPerTick = 0.5333f;
		private int _stage; // 0 = soldier window, 1 = player baseline window
		private Cur _solCur, _plCur;

		// ---- range geometry (tiles) ----
		internal const int FloorY = 120;           // top row of the floor
		internal const int ClearTop = 60;          // rows ClearTop..FloorY-1 are air
		internal const int HalfWidth = 100;        // range spans cx-HalfWidth .. cx+HalfWidth
		private static int SetupTicks = 30; // LEVY_HARNESS_SETUP overrides (idle ticks before the target appears; lets Calamity stealth fill)
		private const int CleanupTicks = 6;
		private const string CommanderName = "LevyTester";

		private enum Ph { Boot, Setup, Run, Cleanup, Done, Scenario, Lab }
		private Ph _ph = Ph.Boot;
		private int _phTick, _bootTick;
		private int _cx;
		private readonly List<Trial> _trials = new();
		private int _ti;
		private readonly List<string> _rows = new();
		private readonly Dictionary<string, WeaponAgg> _agg = new();
		private readonly List<string> _weaponOrder = new();
		private HarnessLogCapture _capture;

		private sealed class Trial
		{
			public int ItemType;            // 0 = no weapon (negative control)
			public string Control = "";     // "pos" / "neg" / ""
			public string AmmoName = "";
			public int Range;
			public bool Walk;               // walk-in trial: target starts 40 tiles away and approaches
			public bool NoSoldier;          // not issuable: skip the soldier window, run only the player baseline
			public bool Baseline = true;    // run a player baseline window after the soldier window
			public int[] Gear;              // ON-HIT PROBE: commander gear for this trial (item types)
			public string GearLabel = "";
			public string Key;
		}

		private sealed class WeaponAgg
		{
			public string Name; public int Fired, Hit, Rows; public int Issued; public bool HealerCast, Healed, Exc;
		}

		public override void Load()
		{
			try
			{
				string v = Environment.GetEnvironmentVariable("LEVY_HARNESS");
				Enabled = !string.IsNullOrEmpty(v) && v != "0";
				if (!Enabled) return;
				OutPath = Environment.GetEnvironmentVariable("LEVY_HARNESS_OUT") ?? "levy_harness.tsv";
				TestMod = Environment.GetEnvironmentVariable("LEVY_HARNESS_MOD") ?? "ThoriumMod";
				Scenario = Environment.GetEnvironmentVariable("LEVY_HARNESS_SCENARIO");
				string f = Environment.GetEnvironmentVariable("LEVY_HARNESS_FILTER");
				if (!string.IsNullOrWhiteSpace(f)) _filter = f.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_TICKS"), out int t) && t >= 60) _runTicks = t;
				string sh = Environment.GetEnvironmentVariable("LEVY_HARNESS_SHARD");
				if (!string.IsNullOrWhiteSpace(sh) && sh.Contains('/')) { var p = sh.Split('/'); if (int.TryParse(p[0], out int k) && int.TryParse(p[1], out int n) && n > 0) { _shardK = k; _shardN = n; } }
				_skipControls = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_SKIPCONTROLS"));
				_aimEnemy = string.Equals(Environment.GetEnvironmentVariable("LEVY_HARNESS_AIM"), "enemy", StringComparison.OrdinalIgnoreCase);
				_onlyLevies = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_ONLYLEVIES"));
				_spSwap = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_SPSWAP"));
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_ALLYHP"), out int ahp) && ahp >= 1 && ahp <= 100) _allyHpPct = ahp;
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_CMDHP"), out int chp) && chp >= 1 && chp <= 100) _cmdHpPct = chp;
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_SELFHP"), out int shp) && shp >= 1 && shp <= 100) _selfHpPct = shp;
				_forceCompanion = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_COMPANION"));
				_noWalk = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_NOWALK"));
				_noBase = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_NOBASE"));
				string accs = Environment.GetEnvironmentVariable("LEVY_HARNESS_ACC");
				if (!string.IsNullOrWhiteSpace(accs)) _accNames = accs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				_medBag = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_MEDBAG"));
				_vanProbe = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_VANPROBE"));
				_orbProbe = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_ORBPROBE"));
				_orbDirect = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_HARNESS_ORBDIRECT"));
				string vn = Environment.GetEnvironmentVariable("LEVY_HARNESS_VANILLA");
				if (!string.IsNullOrWhiteSpace(vn)) _vanilla = vn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_HARNESS_SETUP"), out int su) && su >= 10 && su <= 2000) SetupTicks = su;
				string cf = Environment.GetEnvironmentVariable("LEVY_HARNESS_CLASS");
				if (!string.IsNullOrWhiteSpace(cf)) _classFilter = cf.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				string ar = Environment.GetEnvironmentVariable("LEVY_HARNESS_ARMOR");
				if (!string.IsNullOrWhiteSpace(ar)) _armorNames = ar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				string rg = Environment.GetEnvironmentVariable("LEVY_HARNESS_RANGES");
				if (!string.IsNullOrWhiteSpace(rg))
				{
					var list = new List<int>();
					foreach (var s in rg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) if (int.TryParse(s, out int r) && r >= 3 && r <= 90) list.Add(r);
					if (list.Count > 0) _ranges = list.ToArray();
				}
				OwnerLab.ReadEnv();
				if (Main.dedServ) Main.OnTickForThirdPartySoftwareOnly += DriveServer;
				On_Projectile.Update += OnProjUpdate;
				if (_vanProbe) On_Projectile.vampireHeal += VampireHealProbe;
				if (_orbProbe) On_Projectile.Kill += OrbKillProbe;
				if (_spSwap) On_NPC.UpdateNPC += OnNpcUpdate;
				On_NetMessage.SendStrikeNPC += OnSendStrike;
				On_NetMessage.SendData += OnSendData;
				On_Player.KillMe += OnPlayerKillMe;
				Mod.Logger.Info($"HARNESS: ENABLED (LEVY_HARNESS set). out={OutPath} mod={TestMod} filter={(f ?? "-")} ticks={_runTicks} shard={_shardK}/{_shardN} skipControls={_skipControls} ranges={string.Join(",", _ranges)}");
			}
			catch (Exception e) { Enabled = false; Mod.Logger.Warn("HARNESS: load failed, disabled: " + e.Message); }
		}

		private static readonly Dictionary<int, (int id, float dist, int timeLeft)> _vbSeen = new(); private static int _vbGone; private static float _vbLastDist, _vbLastTime;
		private static readonly Dictionary<int, (int id, float min, int timeLeft, int age)> _orbSeen = new(); private static int _orbTraceId = -1; private static string _orbTrace = ""; private static int _orbN, _orbEnded, _orbNear, _orbKilledEarly; private static float _orbMinSum;
		private static int _orbKillLogged;
		private static void OrbKillProbe(On_Projectile.orig_Kill orig, Projectile self)
		{
			if (self.ModProjectile != null && self.ModProjectile.Name.StartsWith("HealingOrb") && _orbKillLogged < 6)
			{
				_orbKillLogged++;
				var st = new System.Diagnostics.StackTrace(1, false).GetFrames();
				string where = string.Join(" < ", st.Take(9).Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name));
				Instance?.Mod.Logger.Info($"ORBKILL timeLeft={self.timeLeft} penetrate={self.penetrate} pos={self.Center} :: {where}");
			}
			orig(self);
		}
		private static int _vhCalls; private static string _vhLast = "";
		private static void VampireHealProbe(On_Projectile.orig_vampireHeal orig, Projectile self, int dmg, Vector2 pos, Entity victim)
		{
			_vhCalls++; _vhLast = $"dmg{dmg} myPlayer{Main.myPlayer} owner{self.owner} lifeSteal{Main.player[Main.myPlayer].lifeSteal} commanderLife{Main.player[0].statLife}/{Main.player[0].statLifeMax2} pos({Main.player[0].Center.X:0},{Main.player[0].Center.Y:0})";
			orig(self, dmg, pos, victim);
		}
		private static string StaticInt(string name)
		{
			var f = typeof(OwnerPuppet).GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			return f == null ? "n/a" : f.GetValue(null)?.ToString() ?? "?";
		}
		internal static string VampireProbeState => $"vampireHeal calls={_vhCalls} last[{_vhLast}] boltsGone={_vbGone} avgLastDistPx={(_vbGone > 0 ? _vbLastDist / _vbGone : 0):0} avgLastTimeLeft={(_vbGone > 0 ? _vbLastTime / _vbGone : 0):0}";

		public override void Unload() { try { Main.OnTickForThirdPartySoftwareOnly -= DriveServer; } catch { } try { On_Projectile.Update -= OnProjUpdate; On_Projectile.vampireHeal -= VampireHealProbe; On_Projectile.Kill -= OrbKillProbe; On_NPC.UpdateNPC -= OnNpcUpdate; On_NetMessage.SendStrikeNPC -= OnSendStrike; On_NetMessage.SendData -= OnSendData; On_Player.KillMe -= OnPlayerKillMe; } catch { } Enabled = false; _capture?.Detach(); _capture = null; _filter = null; }

		public override void OnWorldLoad()
		{
			if (!Enabled) return;
			_ph = Ph.Boot; _bootTick = 0; _ti = 0; _trials.Clear(); _rows.Clear(); _agg.Clear(); _weaponOrder.Clear();
		}

		// =========================================================================================================
		// Tick driver
		// =========================================================================================================
		// A dedicated server with no client connected does not advance the world on its own, so (as the earlier drone harness did) the harness steps
		// Main.Update itself from the server loop's per-turn callback: the REAL game update runs (NPC AI, projectiles, every ModSystem/GlobalNPC hook), just
		// driven faster than 60/s. StepsPerTurn real updates per server-loop turn.
		private static System.Reflection.MethodInfo _update;
		private static bool _inDrive;
		private static int StepsPerTurn = 20;

		private static void DriveServer()
		{
			if (!Enabled || _inDrive || !Main.dedServ || Main.netMode != NetmodeID.Server) return;
			var h = Instance;
			if (h == null || h._ph == Ph.Done) return;
			_inDrive = true;
			try
			{
				_update ??= typeof(Main).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
				for (int i = 0; i < StepsPerTurn && h._ph != Ph.Done; i++)
				{
					bool emu = HarnessOnHit.Enabled && HarnessOnHit.Emulate && h._stage == 0 && h._ph != Ph.Boot;
					int savedMy = Main.myPlayer;
					if (emu) Main.myPlayer = 0; // single-player emulation: soldier shots are owned by the commander, as in a real single-player game
					try { _update.Invoke(Main.instance, new object[] { new GameTime() }); }
					finally { if (emu) Main.myPlayer = savedMy; }
				}
			}
			catch (Exception e) { Instance?.Mod.Logger.Warn("HARNESS: world update threw: " + (e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e)); }
			finally { _inDrive = false; }
		}

		/// <summary>Medical Bag probe: the commander's accessory pass runs after the players update and before the projectiles do, so drive it here (a real client's order).</summary>
		public override void PreUpdateProjectiles()
		{
			// The server drops a player slot that has no connected client during the player pass, and vanilla kills every projectile whose owner is not active (Projectile.Update: owner < 255 && !Main.player[owner].active).
			// The stand-in commander is only re-asserted at the end of the tick, so without this most of its (and its soldiers') projectiles died within a few ticks of spawning. Measurement artifact; keep the stand-in up for the projectile pass.
			if (Enabled && _ph != Ph.Done && _ph != Ph.Boot) { Player sp = Main.player[0]; if (sp.name == CommanderName) { sp.active = true; sp.dead = false; } }
			if (Enabled && _accNames != null && _ph == Ph.Run && ModLoader.HasMod("ThoriumMod") && ThoriumCompatSystem.Active) HarnessThoriumProbe.SetHealBonus(Main.player[0], 1);
			if (Enabled && _ph == Ph.Run && Main.netMode == NetmodeID.Server && ModLoader.HasMod("ThoriumMod") && ThoriumCompatSystem.Active) { int smy = Main.myPlayer; Main.myPlayer = 0; try { OwnerPuppet.EmulateClientCache(Main.player[0]); } finally { Main.myPlayer = smy; } } // a real client's player update points Thorium's local-player cache at its player; this client-less stand-in never does // the player pass zeroes it; a Medical Bag gives +1
			if (!Enabled || !_medBag || _ph != Ph.Run || Main.netMode == NetmodeID.MultiplayerClient || !ModLoader.HasMod("ThoriumMod") || !ThoriumCompatSystem.Active) return;
			string me = HarnessThoriumProbe.MedBagDrive(Main.player[0]);
			if (me != "" && _cur != null && !_cur.Notes.Contains(me)) _cur.Notes += "medbag drive " + me;
		}

		public override void PostUpdateEverything()
		{
			if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient) return;
			try { Step(); }
			catch (Exception e)
			{
				Mod.Logger.Warn("HARNESS: driver exception: " + e);
				_cur?.Exceptions.Add("driver:" + e.GetType().Name + ":" + e.Message);
				if (_ph == Ph.Boot) { Finish("driver exception during boot"); }
				else { try { EndTrial(); } catch { } _ph = Ph.Cleanup; _phTick = 0; }
			}
		}

		private void Step()
		{
			if (_ph == Ph.Done) return;
			// frozen sunny noon: no night spawns, no events
			Main.dayTime = true; Main.time = 27000; Main.raining = false; Main.bloodMoon = false; Main.eclipse = false;
			if (Main.invasionType != 0) Main.invasionType = 0;

			switch (_ph)
			{
				case Ph.Boot:
					if (++_bootTick < 180) return;
					BootInit();
					return;
				case Ph.Scenario:
					StepScenario();
					return;
				case Ph.Lab:
					OwnerLab.Tick(this);
					return;
				case Ph.Setup:
					if (_phTick == 0)
					{
						BeginTrial();
						if (_cur != null && _cur.SkipRun) { EndTrial(); _ph = Ph.Cleanup; _phTick = 0; return; }
					}
					KeepSite();
					if (++_phTick >= SetupTicks) { _ph = Ph.Run; _phTick = 0; StartRun(); }
					return;
				case Ph.Run:
					KeepSite();
					if (_stage == 1) { PlayerItemCheck(); SamplePlayerTick(); } else SampleTick();
					if (++_phTick >= RunTicksFor(_trials[_ti])) { EndTrial(); _ph = Ph.Cleanup; _phTick = 0; }
					return;
				case Ph.Cleanup:
					if (_phTick == 0) Teardown();
					if (++_phTick >= CleanupTicks)
					{
						Trial tr = _trials[_ti];
						if (_stage == 0 && NeedsBaseline(tr)) { _stage = 1; _ph = Ph.Setup; _phTick = 0; return; }
						FinishRow();
						_stage = 0; _solCur = null; _plCur = null;
						_ti++;
						_ph = Ph.Setup; _phTick = 0;
						if (_ti >= _trials.Count) Finish("all trials complete");
					}
					return;
			}
		}

		// =========================================================================================================
		// Boot: range, commander, trial list
		// =========================================================================================================
		private void BootInit()
		{
			_cx = Main.maxTilesX / 2;
			try { Main.autoSave = false; } catch { } // the autosave worker saves Main.player[Main.myPlayer]; with the baseline's brief myPlayer swap it once hit the client-less slot and crashed a shard
			TryAttachCapture();
			BuildRange();
			PlaceCommander(false);
			if (!string.IsNullOrEmpty(Scenario)) { if (BeginScenario()) { _ph = Ph.Scenario; _phTick = 0; return; } Finish("unknown LEVY_HARNESS_SCENARIO '" + Scenario + "'"); return; }
			if (OwnerLab.Active) { _ph = Ph.Lab; OwnerLab.Boot(this); return; } // owner-lab feasibility study (OwnerLab.cs): its own scenario driver replaces the trial list
			BuildTrials();
			Mod.Logger.Info($"HARNESS: range built at x={_cx - HalfWidth}..{_cx + HalfWidth}, floor y={FloorY}; world {Main.maxTilesX}x{Main.maxTilesY}; {_trials.Count} trials queued; thoriumActive={ThoriumCompatSystem.Active}; netMode={Main.netMode}");
			if (_trials.Count == 0) { Finish("no trials"); return; }
			_ph = Ph.Setup; _phTick = 0; _ti = 0;
		}

		private void TryAttachCapture()
		{
			try { _capture = new HarnessLogCapture(); _capture.Attach(Mod.Logger); }
			catch (Exception e) { Mod.Logger.Warn("HARNESS: could not attach the log capture (in-game exception capture disabled): " + e.Message); _capture = null; }
		}

		private void BuildRange()
		{
			int x0 = Math.Max(10, _cx - HalfWidth), x1 = Math.Min(Main.maxTilesX - 10, _cx + HalfWidth);
			for (int x = x0 - 2; x <= x1 + 2; x++)
			{
				for (int y = ClearTop; y < FloorY; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = false; t.TileType = 0; t.WallType = 0; t.LiquidAmount = 0; t.TileFrameX = 0; t.TileFrameY = 0;
				}
				for (int y = FloorY; y < FloorY + 4; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = true; t.TileType = TileID.GrayBrick; t.WallType = 0; t.LiquidAmount = 0;
					t.BlockType = Terraria.ID.BlockType.Solid;
					t.TileFrameX = 0; t.TileFrameY = 0;
				}
			}
			// side walls so nothing wanders off the range
			foreach (int x in new[] { x0 - 3, x1 + 3 })
				for (int y = ClearTop; y < FloorY + 4; y++)
				{
					Tile t = Main.tile[x, y];
					t.HasTile = true; t.TileType = TileID.GrayBrick; t.BlockType = Terraria.ID.BlockType.Solid;
				}
		}

		private Vector2 SoldierX0Bottom => new Vector2(_cx * 16f + 8f, FloorY * 16f);

		/// <summary>Stand-in commander. Sets: active, name, alive, hp/mana, team, position (4 tiles left of the soldier's spawn), zero velocity.</summary>
		private void PlaceCommander(bool wounded)
		{
			Player p = Main.player[0];
			if (!p.active || p.name != CommanderName)
			{
				p.active = true;
				p.name = CommanderName;
				p.statLifeMax = p.statLifeMax2 = 500;
				p.statManaMax = p.statManaMax2 = 200;
				p.statLife = 500; p.statMana = 200;
				p.dead = false; p.team = 0;
				p.whoAmI = 0;
			}
			p.dead = false;
			p.statLifeMax = 500; p.statLifeMax2 = 500;
			p.statLife = wounded ? 250 : 500;
			p.statMana = 200;
			p.position = new Vector2(_cx * 16f - 4 * 16f, FloorY * 16f - p.height);
			if (HarnessOnHit.Enabled && _stage == 0 && HarnessOnHit.FarTiles > 0) p.position = new Vector2(_cx * 16f - HarnessOnHit.FarTiles * 16f, HarnessOnHit.FarY * 16f);
			p.velocity = Vector2.Zero;
			for (int b = 0; b < p.buffType.Length; b++) { p.buffType[b] = 0; p.buffTime[b] = 0; }
		}

		private void KeepSite()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			// the server resets a player slot that has no network client (max life fell back to 100 in the first run), so re-assert the maxima every tick
			p.statLifeMax = 500; p.statLifeMax2 = 500; p.statManaMax = 200; p.statManaMax2 = 200;
			if (_vanProbe) p.lifeSteal = 80f; // vanilla's lifesteal pool (a spawned player starts with it full); the stand-in was never spawned
			if (_accNames != null && ModLoader.HasMod("ThoriumMod") && ThoriumCompatSystem.Active) HarnessThoriumProbe.SetHealBonus(p, 1);
 // what a Medical Bag gives; the stand-in's gear is never updated
			p.lifeRegenTime = 0; p.lifeRegenCount = 0; // natural regeneration speeds up the longer a player goes unhurt; pin it so every window sees the same slow regen and a heal stands out
			p.position = new Vector2(_cx * 16f - (_stage == 1 ? 10f : 4 * 16f) , FloorY * 16f - p.height);
			if (HarnessOnHit.Enabled && _stage == 0 && HarnessOnHit.FarTiles > 0) p.position = new Vector2(_cx * 16f - HarnessOnHit.FarTiles * 16f, HarnessOnHit.FarY * 16f); // far and high above the soldier
			p.velocity = Vector2.Zero;
			p.statMana = p.statManaMax2;
			if (p.statLife < 1) p.statLife = 1;
			if (_cur != null && _cur.Player2 && _stage == 1)
			{
				Player q = Main.player[1];
				q.active = true; q.dead = false; q.name = "LevyTester2";
				q.statLifeMax = 500; q.statLifeMax2 = 500; q.statManaMax = 200; q.statManaMax2 = 200;
				q.position = new Vector2(_cx * 16f - 9 * 16f - 10f, FloorY * 16f - q.height);
				q.velocity = Vector2.Zero;
				if (q.statLife < 1) q.statLife = 1;
			}
			// walk-in trial: drive the pinned target toward the soldier
			if (_ph == Ph.Run && _stage == 0 && _cur != null && _cur.T.Walk && _cur.TargetIdx >= 0 && _cur.BodyIdx >= 0)
			{
				NPC tn = Main.npc[_cur.TargetIdx], bn = Main.npc[_cur.BodyIdx];
				if (tn.active && bn.active && tn.TryGetGlobalNPC(out HarnessTarget wt))
				{
					float gap = tn.Center.X - bn.Center.X;
					if (Math.Abs(gap) > 2 * 16f) wt.Pin.X -= Math.Sign(gap) * WalkPxPerTick;
				}
			}
			// sweep: nothing hostile except our target may exist on/near the range
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				if (!n.friendly || n.type == NPCID.TargetDummy) { n.active = false; }
			}
		}

		// =========================================================================================================
		// Trial list
		// =========================================================================================================
		/// <summary>rogue / summon / magic / ranged / melee / other, from the item's damage class (Calamity's rogue class is matched by name).</summary>
		internal static string ClassGroup(Item it)
		{
			DamageClass d = it.DamageType;
			if (d == null) return "other";
			string n = d.GetType().Name;
			if (n.Contains("Rogue") || n.Contains("Stealth")) return "rogue";
			if (d.CountsAsClass(DamageClass.Summon)) return "summon";
			if (d.CountsAsClass(DamageClass.Magic)) return "magic";
			if (d.CountsAsClass(DamageClass.Ranged)) return "ranged";
			if (d.CountsAsClass(DamageClass.Melee)) return "melee";
			if (d.CountsAsClass(DamageClass.Throwing)) return "throwing";
			return "other";
		}

		private static bool ClassMatches(Item it)
		{
			if (_classFilter == null) return true;
			string g = ClassGroup(it);
			foreach (var f in _classFilter) if (string.Equals(f, g, StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		private static bool NameMatches(Item it)
		{
			if (_filter == null) return true;
			string n = it.Name ?? ""; string inn = it.ModItem?.Name ?? "";
			foreach (var f in _filter) if (n.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0 || inn.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		/// <summary>The weapon predicate: from the tested mod, and anything Levy's classifier or the companion compat treats as a weapon, plus any ordinary damage-dealing usable item.</summary>
		internal static bool IsWeaponCandidate(Item it, out string why)
		{
			why = "";
			if (it == null || it.IsAir || it.ModItem == null) return false;
			if (it.ModItem.Mod.Name != TestMod) return false;
			if (WeaponClassifier.IsIssuableWeapon(it)) { why = "issuable"; return true; }
			string kind = CompanionKind(it);
			if (kind != "") { why = kind; return true; }
			if (it.damage > 0 && it.useStyle > 0 && it.ammo == 0 && it.pick == 0 && it.axe == 0 && it.hammer == 0 && !it.accessory && !it.vanity && it.mountType < 0 && it.shoot >= 0
				&& !(it.consumable && it.shoot == 0)) { why = "damage-item"; return true; }
			return false;
		}

		/// <summary>"bard" / "healer:Role" / "" via the Thorium probe (only when Thorium is loaded).</summary>
		internal static string CompanionKind(Item it)
		{
			try { if (ModLoader.HasMod("ThoriumMod") && ThoriumCompatSystem.Active) return HarnessThoriumProbe.KindOf(it); }
			catch (Exception) { }
			return "";
		}

		private void BuildTrials()
		{
			if (HarnessOnHit.Enabled) { BuildOnHitTrials(); return; }
			if (!_skipControls)
			{
				AddWeaponTrials(ItemID.Musket, "pos", "Musket Ball", ItemID.MusketBall);
				AddWeaponTrials(ItemID.WoodenBow, "pos", "Wooden Arrow", ItemID.WoodenArrow);
				AddWeaponTrials(0, "neg", "", 0);
				AddWeaponTrials(0, "negc", "", 0); // no weapon, but WITH the wounded ally + 50% commander: the natural-regen baseline for the heal columns
			}
			int idx = 0;
			int count = ItemLoader.ItemCount;
			var seen = new HashSet<int>();
			if (_vanilla != null)
				foreach (string vname in _vanilla)
				{
					int vt = int.TryParse(vname, out int vid) ? vid : -1;
					if (vt < 0) { for (int k = 1; k < ItemID.Count; k++) if (Lang.GetItemNameValue(k).Equals(vname, StringComparison.OrdinalIgnoreCase)) { vt = k; break; } }
					if (vt > 0 && seen.Add(vt)) AddWeaponTrials(vt, "", null, 0);
				}
			for (int t = _vanilla != null ? count : ItemID.Count; t < count; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || !seen.Add(t)) continue;
				if (!IsWeaponCandidate(it, out _)) continue;
				if (!NameMatches(it) || !ClassMatches(it)) continue;
				if ((idx++ % _shardN) != _shardK) continue;
				AddWeaponTrials(t, "", null, 0);
			}
		}

		/// <summary>ON-HIT PROBE trial list: one soldier window per trial (no walk-in, no player baseline), commander far away. Gear scan: per gear one idle control + one trial per reference weapon; otherwise one trial per issuable weapon.</summary>
		private void BuildOnHitTrials()
		{
			int range = _ranges.Length > 0 ? _ranges[Math.Min(_ranges.Length - 1, _ranges.Length / 2)] : 15;
			var rangeList = _ranges;
			void Add(int itemType, int[] gear, string gearLabel, int r)
			{
				string name = itemType == 0 ? "(no weapon)" : ContentSamples.ItemsByType[itemType].Name;
				string key = (itemType == 0 ? "NEG" : itemType.ToString()) + "/" + (gearLabel == "" ? "-" : gearLabel) + "/r" + r;
				_weaponOrder.Add(key);
				_agg[key] = new WeaponAgg { Name = name + (gearLabel == "" ? "" : " + " + gearLabel), Issued = itemType == 0 ? 0 : 1 };
				_trials.Add(new Trial { ItemType = itemType, Control = itemType == 0 ? "neg" : "", Range = r, Key = key, Baseline = HarnessOnHit.WithBaseline && itemType > 0 && gear == null, Gear = gear, GearLabel = gearLabel });
			}
			if (HarnessOnHit.GearScanMods != null)
			{
				var gears = HarnessOnHit.EnumerateGear(); var refs = HarnessOnHit.ResolveRefs();
				Mod.Logger.Info($"ONHIT: gear scan {gears.Count} gear entries x ({refs.Count} refs + idle) at {range} tiles");
				Add(0, null, "", range); // idle control with only the fixed loadout
				foreach (var g in gears)
				{
					Add(0, g.Types, g.Label, range);
					foreach (int rw in refs) Add(rw, g.Types, g.Label, range);
				}
				return;
			}
			Add(0, null, "", range);
			_agg["NEGC"] = new WeaponAgg { Name = "(no weapon, companion)" }; _weaponOrder.Add("NEGC");
			_trials.Add(new Trial { ItemType = 0, Control = "negc", Range = range, Key = "NEGC", Baseline = false }); // idle control with a wounded ally + 50% commander (natural-regen noise for healer / bard weapons)
			int idx = 0; var seen = new HashSet<int>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				Item it = new Item();
				try { it.SetDefaults(t); } catch { continue; }
				if (it.IsAir || !seen.Add(t)) continue;
				if (!IsWeaponCandidate(it, out _) || !WeaponClassifier.IsIssuableWeapon(it)) continue;
				if (!NameMatches(it) || !ClassMatches(it)) continue;
				if ((idx++ % _shardN) != _shardK) continue;
				foreach (int r in rangeList) Add(t, null, "", r);
			}
		}

		private void AddWeaponTrials(int itemType, string control, string ammoName, int ammoType)
		{
			string name = itemType == 0 ? (control == "negc" ? "(no weapon, with wounded ally + 50% commander)" : "(no weapon)") : ContentSamples.ItemsByType[itemType].Name;
			var item = itemType == 0 ? null : ContentSamples.ItemsByType[itemType];
			bool issuable = item != null && WeaponClassifier.IsIssuableWeapon(item);
			string key = itemType == 0 ? "NEG-" + control : itemType.ToString();
			_weaponOrder.Add(key);
			_agg[key] = new WeaponAgg { Name = name, Issued = issuable ? 1 : 0 };
			if (item != null && !issuable && control == "")
			{
				// can't be held by a soldier: no soldier window. If a player could plausibly use it, one player-baseline window at 25 tiles; else a single empty row.
				bool usable = PlayerCouldUse(item);
				_trials.Add(new Trial { ItemType = itemType, Control = control, Range = usable ? 25 : 0, Key = key, NoSoldier = true, Baseline = usable });
				return;
			}
			foreach (int r in _ranges) _trials.Add(new Trial { ItemType = itemType, Control = control, Range = r, Key = key, AmmoName = ammoName ?? "", Baseline = itemType != 0 && !_noBase });
			if (!_noWalk) _trials.Add(new Trial { ItemType = itemType, Control = control, Range = 40, Walk = true, Key = key, AmmoName = ammoName ?? "", Baseline = false });
		}

		/// <summary>Plain 'tool/weapon' items a player can hold and swing/shoot: not armor, accessories, potions or consumable buffs.</summary>
		private static bool PlayerCouldUse(Item it)
		{
			if (it.headSlot >= 0 || it.bodySlot >= 0 || it.legSlot >= 0 || it.accessory || it.vanity || it.potion || it.buffType > 0) return false;
			return it.useStyle > 0;
		}

		private bool NeedsBaseline(Trial t) => t.Baseline && t.ItemType > 0 && t.Range > 0;
		private int RunTicksFor(Trial t) => t.Walk ? WalkTicks : _runTicks;

		// =========================================================================================================
		// Per-trial state
		// =========================================================================================================
		private sealed class Cur
		{
			public Trial T; public Item Weapon; public string Family = "-", Kind = "", Where = "-"; public bool Issuable;
			public SoldierRecord Rec, AllyRec; public int BodyIdx = -1, AllyIdx = -1, TargetIdx = -1;
			public string Ammo = "-";
			public bool SetupOk;
			public List<string> Exceptions = new();
			public Dictionary<string, int> Proj = new(); public int ProjOther, ProjTotal;
			public long Damage; public int Hits; public List<int> Extra = new(); // altfire-survey: extra crowd targets (LEVY_HARNESS_CROWD)
			public int AnimTicks, FirstFire = -1, EngageTicks, AcquiredTicks, AliveTicks, DeadTicks; public float MinDist = 9999f, StartDist;
			public int CmdBefore, CmdAfter, CmdMax, AllyBefore, AllyAfter, AllyMax, AllyMaxLife;
			public string Med = ""; public int MedAimed, MedTicks;
			public int SelfBefore, SelfAfter, SelfMax, SelfPrev = -1, SelfGainSum, SelfBig, SelfLifeMax; // the SHOOTING soldier's own life (lifesteal / self-heal)
			public int CmdPrev = -1, AllyPrev = -1, CmdBig, AllyBig, CmdGainSum, AllyGainSum; // per-tick life rises; 'Big' = a single-tick rise of 3+ (natural regen rises 1 at a time)
			public int InspStart = -1, InspEnd = -1, InspSpent, InspPrev = -1, InspMax = -1;
			public uint EmpUnion, AllyEmpUnion;
			public HashSet<string> CmdBuffNames = new();
			public int ManaStart, ManaEnd, ManaMin;
			public float MovedX;
			public float StartX;
			public string Notes = "";
			public int ActiveTick;
			public HashSet<string> States = new();
			public int HealProjSeen;
			public bool IsBaseline, SkipRun;
			public bool CalOn; public float StealthStart = -1f, StealthEnd = -1f, StealthMax, StealthPeak; public bool StealthFilled; public int StealthStrikes, StealthSubs, StealthAvailTicks;
			public float[] RA0, RA1;
			public Dictionary<string, int> Items = new();       // items that appeared in the world during the window (name -> count)
			public HashSet<string> AllyBuffNames = new();
			public uint PlayerEmpUnion;
			public float EngageDist = -1f, FirstShotDist = -1f;  // center-to-center tiles at first Engage state / first shot or item animation
			public bool AimAtAlly; public string BaselineNote = ""; public int PlayInstrNre, StrikePackets, KillsSuppressed;
			public Dictionary<int, int> Packets = new(); public int PacketHeals; public bool Player2; public int P2Before, P2After, P2Max, P2Prev = -1, P2Big, P2GainSum;
		}
		private Cur _cur;

		/// <summary>Called by the projectile watcher at spawn.</summary>
		internal static CombatHarness Instance => ModContent.GetInstance<CombatHarness>();
		internal bool Collecting => Enabled && _ph == Ph.Run && _cur != null;
		private readonly List<Projectile> _pendingProj = new();
		internal void NoteSpawn(Projectile p) { if (Collecting) _pendingProj.Add(p); }
		internal void NoteItem(Item it, IEntitySource src)
		{
			if (!Collecting || it == null) return;
			string n = it.ModItem != null ? it.ModItem.Mod.Name + "/" + it.ModItem.Name : (ItemID.Search.TryGetName(it.type, out string vn) ? vn : "item" + it.type);
			string tag = src == null ? "null" : src.GetType().Name.Replace("EntitySource_", "") + (src is EntitySource_Parent ep ? ":" + (ep.Entity is Projectile ? "Projectile:" + ((Projectile)ep.Entity).type : ep.Entity?.GetType().Name) : "") + (src is EntitySource_ItemUse ? "" : "");
			n += "@" + tag + "#t" + it.type;
			_cur.Items[n] = _cur.Items.TryGetValue(n, out int c) ? c + it.stack : it.stack;
		}

		private void BeginTrial()
		{
			Trial t = _trials[_ti];
			_cur = new Cur { T = t, IsBaseline = _stage == 1 };
			_pendingProj.Clear();
			_capture?.Begin();
			ClearAll();
			try
			{
				Item w = null;
				if (t.ItemType > 0)
				{
					w = new Item(); w.SetDefaults(t.ItemType);
					if (w.IsAir) throw new Exception("SetDefaults left air for type " + t.ItemType);
				}
				_cur.Weapon = w;
				if (w != null)
				{
					_cur.Issuable = WeaponClassifier.IsIssuableWeapon(w);
					var prof = WeaponClassifier.GetProfile(w);
					_cur.Family = prof.Family.ToString() + "/rng" + prof.RangeTiles;
					_cur.Kind = CompanionKind(w);
				}
				if (_stage == 0 && (t.Range == 0 || t.NoSoldier))
				{
					_cur.SetupOk = false; _cur.SkipRun = true;
					_cur.Notes = t.Range == 0 ? "not issuable and not player-usable: no trial run" : "not issuable: soldier window skipped (player baseline only)";
					return;
				}

				bool companion = _cur.Kind != "" || t.Control == "negc" || _forceCompanion;
				PlaceCommander(companion && !_onlyLevies);
				if (HarnessOnHit.Enabled && _stage == 0) HarnessOnHit.ApplyLoadout(Main.player[0], t.Gear);

				if (_stage == 1) { SetupPlayer(w, t, companion); return; }

				// ---- the soldier ----
				var rec = MakeRecord("Pvt. Subject");
				var gear = rec.Gear;
				if (w != null)
				{
					Item held = w.Clone();
					if (WeaponClassifier.Classify(held) == WeaponFamily.Thrown) held.stack = Math.Min(held.maxStack > 1 ? held.maxStack : 999, 999);
					if (gear.CanHold(SoldierGear.SlotWeapon, held)) { gear.Weapon = held; _cur.Where = "weapon"; }
					else if (gear.CanHold(SoldierGear.SlotSidearm, held)) { gear.Sidearm = held; _cur.Where = "sidearm"; }
					else { _cur.Where = "none(CanHold false)"; }
					if (_cur.Where == "weapon") gear.SetLocked(SoldierGear.SlotWeapon, true);
					if (_cur.Where == "sidearm") gear.SetLocked(SoldierGear.SlotSidearm, true);
					if (w.useAmmo > 0 || t.AmmoName != "")
					{
						Item ammo = FindAmmo(w, t.AmmoName);
						if (ammo != null && gear.CanHold(SoldierGear.SlotAmmo, ammo)) { gear.Ammo = ammo; gear.SetLocked(SoldierGear.SlotAmmo, true); _cur.Ammo = ammo.Name + " x" + ammo.stack; }
						else _cur.Ammo = ammo == null ? "NONE FOUND for useAmmo " + w.useAmmo : ammo.Name + " (CanHold false)";
					}
				}
				if (_armorNames != null)
					foreach (Item a in ArmorItems())
					{
						int slot = a.headSlot >= 0 ? SoldierGear.SlotHead : a.bodySlot >= 0 ? SoldierGear.SlotBody : SoldierGear.SlotLegs;
						if (gear.CanHold(slot, a)) { gear.Set(slot, a); gear.SetLocked(slot, true); _cur.Notes += "armor:" + a.Name + ";"; }
						else _cur.Notes += "armor " + a.Name + " CanHold false;";
					}
				if (_accNames != null)
				{
					int ak = 0;
					foreach (Item a in AccItems())
					{
						int slot = SoldierGear.SlotAcc0 + ak;
						if (gear.CanHold(slot, a)) { gear.Set(slot, a); gear.SetLocked(slot, true); _cur.Notes += "acc:" + a.Name + ";"; ak++; }
						else _cur.Notes += "acc " + a.Name + " CanHold false;";
					}
				}
				rec.LoadoutLocked = true;
				rec.Order = HarnessOnHit.Enabled && HarnessOnHit.FarTiles > 0 ? HarnessOnHit.FarOrder : SoldierOrder.Follow;
				rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec);
				_cur.Rec = rec;
				Vector2 at = SoldierX0Bottom;
				int idx = RosterSystem.Instance.SpawnBody(rec, at);
				_cur.BodyIdx = idx;
				if (idx < 0 || !rec.HasBody) throw new Exception("SpawnBody failed (idx=" + idx + ")");
				var body = (SoldierNPC)Main.npc[idx].ModNPC;
				body.NPC.Bottom = at;
				if (companion) SpawnAlly(at);
				_cur.SetupOk = true; // the target is placed in StartRun, after the soldier has settled, at the real labelled distance
			}
			catch (Exception e) { _cur.Exceptions.Add("setup:" + e.GetType().Name + ":" + e.Message); }
		}

		/// <summary>A wounded (30%) squadmate 7 tiles left of the item user, on Hold + HoldFire.</summary>
		private void SpawnAlly(Vector2 at)
		{
			var ar = MakeRecord("Pvt. Wounded");
			ar.Order = SoldierOrder.Hold; ar.HoldFire = true; ar.LoadoutLocked = true;
			ar.CurrentHp = Math.Max(1, (int)(ar.MaxHp * (_allyHpPct / 100f))); ar.Mana = ar.MaxMana;
			RosterSystem.Instance.Regulars.Add(ar);
			_cur.AllyRec = ar;
			Vector2 aat = at + new Vector2(-7 * 16f, 0f);
			int ai = RosterSystem.Instance.SpawnBody(ar, aat);
			_cur.AllyIdx = ai;
			if (ai >= 0 && ar.HasBody) { var ab = (SoldierNPC)Main.npc[ai].ModNPC; ab.NPC.Bottom = aat; ab.ReceiveOrder(SoldierOrder.Hold); ab.NPC.life = ar.CurrentHp; _cur.AllyMaxLife = ab.NPC.lifeMax; }
			else _cur.Notes += "ally spawn failed;";
		}

		/// <summary>PLAYER BASELINE setup: the stand-in commander holds the item in inventory slot 0 (ammo in slot 54), same wounded ally, same geometry; the soldier is absent.</summary>
		private void SetupPlayer(Item w, Trial t, bool companion)
		{
			Player p = Main.player[0];
			for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			Item held = w.Clone();
			if (held.consumable || WeaponClassifier.Classify(held) == WeaponFamily.Thrown) held.stack = Math.Min(held.maxStack > 1 ? held.maxStack : 999, 999);
			p.inventory[0] = held; p.selectedItem = 0;
			if (_armorNames != null)
				foreach (Item a in ArmorItems())
				{
					int ai = a.headSlot >= 0 ? 0 : a.bodySlot >= 0 ? 1 : 2;
					p.armor[ai] = a; _cur.BaselineNote += "armor:" + a.Name + ";";
				}
			if (_accNames != null) { int ak = 0; foreach (Item a in AccItems()) { if (3 + ak < 8) p.armor[3 + ak++] = a; _cur.BaselineNote += "acc:" + a.Name + ";"; } }
			_cur.Where = "player slot0";
			if (w.useAmmo > 0)
			{
				Item ammo = FindAmmo(w, t.AmmoName);
				if (ammo != null) { p.inventory[54] = ammo; _cur.Ammo = ammo.Name + " x" + ammo.stack; }
				else _cur.Ammo = "NONE FOUND for useAmmo " + w.useAmmo;
			}
			p.noItems = false; p.itemAnimation = 0; p.itemTime = 0; p.controlUseItem = false; p.channel = false;
			_cur.AimAtAlly = _cur.Kind == "healer:AllyHeal" || (_cur.Kind == "healer:Hybrid" && !_aimEnemy);
			Vector2 at = SoldierX0Bottom;
			if (companion) { SpawnAlly(at); if (!_onlyLevies) SetupPlayer2(); }
			if (_cur.Kind == "bard") { try { HarnessThoriumProbe.PrimePlayerBard(p); } catch (Exception e) { _cur.Exceptions.Add("prime:" + e.Message); } }
			_cur.SetupOk = true;
		}

		/// <summary>A second stand-in PLAYER (slot 1, wounded to 30%) 9 tiles left of the user: Thorium's heals and empowerments are written for players, so the baseline needs one to land on.</summary>
		private void SetupPlayer2()
		{
			Player q = Main.player[1];
			q.active = true; q.name = "LevyTester2"; q.dead = false; q.team = 0;
			q.statLifeMax = q.statLifeMax2 = 500; q.statManaMax = q.statManaMax2 = 200; q.statMana = 200;
			q.statLife = 150;
			for (int b = 0; b < q.buffType.Length; b++) { q.buffType[b] = 0; q.buffTime[b] = 0; }
			q.position = new Vector2(_cx * 16f - 9 * 16f - 10f, FloorY * 16f - q.height);
			q.velocity = Vector2.Zero;
			_cur.Player2 = true;
		}

		/// <summary>Called right before every world update of a baseline Run window (Main.myPlayer is already 0): aim the cursor and hold / pulse the use button, as a player would.</summary>
		/// <summary>
		/// Baseline window only: a PLAYER's projectile damages NPCs only when Main.myPlayer == its owner (damage is owner-authoritative in vanilla), so the baseline player's own projectiles
		/// are updated with Main.myPlayer = 0. Every other projectile (and every other window) updates untouched.
		/// </summary>
		/// <summary>SPSWAP only: a soldier's body updates as it would in single player, where the machine's local player (Main.myPlayer) is the commander - so his shots are owned by the commander, as in play.</summary>
		private static void OnNpcUpdate(On_NPC.orig_UpdateNPC orig, NPC self, int i)
		{
			var h = Enabled ? Instance : null;
			if (h != null && _spSwap && h._ph == Ph.Run && h._stage == 0 && self.ModNPC is SoldierNPC)
			{
				int saved = Main.myPlayer;
				Main.myPlayer = 0;
				try { orig(self, i); } finally { Main.myPlayer = saved; }
			}
			else orig(self, i);
		}

		private static void OnProjUpdate(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			var h = Enabled ? Instance : null;
			if (h != null && h._ph == Ph.Scenario && global::Levy.Common.Team.TeamItems.HarnessLocal) { Main.player[0].active = true; Main.player[0].dead = false; } // team scenario: the stand-in blinks inactive during the server's player pass; a projectile that homes on a player must not see that
			// the stand-in player's own projectiles AND Thorium's heal projectiles (which are not "friendly" but only act when Main.myPlayer == owner); with SPSWAP the soldier window does the same, as single player would
			bool thor = self.owner == 0 && ((self.ModProjectile != null && (self.ModProjectile.Mod.Name == "ThoriumMod" || self.ModProjectile.Mod.Name == "CalamityMod")) || (_vanProbe && self.type == ProjectileID.VampireHeal));
			if (h != null && h._ph == Ph.Run && self.owner == 0 && ((h._stage == 1 && (self.friendly || thor)) || (_spSwap && h._stage == 0 && thor)))
			{
				int saved = Main.myPlayer;
				Main.myPlayer = 0;
				Main.player[0].active = true; // the server clears the flag of a client-less slot during the update; a projectile whose owner is inactive is killed by vanilla
				try { orig(self, i); } finally { Main.myPlayer = saved; }
			}
			else orig(self, i);
		}

		/// <summary>
		/// A real client's hit on an NPC is sent to the server as a strike packet, which the server applies. This server has no client, so in the baseline window a strike the stand-in
		/// player's item or projectile reports is applied locally in exactly the way the server applies a received one (StrikeNPC with fromNet: true).
		/// </summary>
		private static void OnSendStrike(On_NetMessage.orig_SendStrikeNPC orig, NPC npc, ref NPC.HitInfo hit, int ignoreClient)
		{
			var h = Enabled ? Instance : null;
			if (h != null && h._stage == 1 && h._ph == Ph.Run && Main.myPlayer == 0 && npc != null && npc.active)
			{
				h._cur.StrikePackets++;
				npc.StrikeNPC(hit, true);
				return;
			}
			orig(npc, ref hit, ignoreClient);
		}

		// The server's network thread keeps clearing Player.active for a slot with no client, asynchronously, and vanilla kills a player's projectile whose owner is inactive. For the length of a
		// baseline window a tiny watcher thread re-asserts the flag so the stand-in player stays "connected".
		private static volatile bool _keepAlive, _p2Alive;
		private static System.Threading.Thread _keepAliveThread;
		private static void SetKeepAlive(bool on)
		{
			_keepAlive = on;
			if (on && (_keepAliveThread == null || !_keepAliveThread.IsAlive))
			{
				_keepAliveThread = new System.Threading.Thread(() => { while (Enabled) { if (_keepAlive) { try { Main.player[0].active = true; if (_p2Alive) Main.player[1].active = true; } catch { } } System.Threading.Thread.Sleep(0); } }) { IsBackground = true, Name = "Levy harness keepalive" };
				_keepAliveThread.Start();
			}
		}

		/// <summary>
		/// A real player's heal / buff on ANOTHER player reaches that player as a packet and is applied by the target's client. Baseline window: count the vanilla packets the item's code emits
		/// (so the heal mechanism is visible) and apply the one that carries a heal (SpiritHeal, 66) to the stand-in player it names, as that player's client would.
		/// </summary>
		private static void OnSendData(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{
			var h = Enabled ? Instance : null;
			if (h != null && h._stage == 1 && h._ph == Ph.Run && h._cur != null && msgType != 23 && msgType != 27 && msgType != 21 && msgType != 5 && msgType != 13 && msgType != 14 && msgType != 16 && msgType != 17 && msgType != 20 && msgType != 8 && msgType != 22 && msgType != 29)
			{
				h._cur.Packets[msgType] = h._cur.Packets.TryGetValue(msgType, out int n) ? n + 1 : 1;
				if (msgType == 66 && number >= 0 && number < Main.maxPlayers && Main.player[number].active)
				{
					Player t = Main.player[number];
					t.statLife = Math.Min(t.statLifeMax2, t.statLife + (int)number2);
					h._cur.PacketHeals++;
				}
			}
			orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
		}

		/// <summary>
		/// The stand-in players must survive their own item (a self-sacrifice item such as a life-for-power chalice would otherwise run the local-player death path, which saves and quits the game).
		/// The death is suppressed and counted; the player is left alive.
		/// </summary>
		private static void OnPlayerKillMe(On_Player.orig_KillMe orig, Player self, Terraria.DataStructures.PlayerDeathReason reason, double dmg, int hitDirection, bool pvp)
		{
			var h = Enabled ? Instance : null;
			if (h != null && self.whoAmI < 2 && (self.name == "LevyTester" || self.name == "LevyTester2"))
			{
				if (h._cur != null) h._cur.KillsSuppressed++;
				self.statLife = Math.Max(1, self.statLife); self.dead = false;
				return;
			}
			orig(self, reason, dmg, hitDirection, pvp);
		}

		private static System.Reflection.MethodInfo _itemCheck;
		private static bool _itemCheckTakesInt;
		/// <summary>
		/// Player baseline: vanilla runs Player.ItemCheck only for the local player, and making slot 0 the local player for the whole update trips a client-only Thorium hook
		/// (TrackerUIPlayer.PreUpdate throws and aborts the player's update). So the baseline runs vanilla's own ItemCheck for slot 0 once per tick, from the end of the tick,
		/// with Main.myPlayer = 0 for exactly that call. Everything else about the player's update (life, buffs, mana, modplayers) is the normal path.
		/// </summary>
		private void PlayerItemCheck()
		{
			var c = _cur;
			if (c == null || !c.SetupOk || !c.IsBaseline || c.Weapon == null) return;
			Player p = Main.player[0];
			int saved = Main.myPlayer;
			try
			{
				Main.myPlayer = 0;
				ApplyPlayerInput();
				if (!p.controlUseItem) p.releaseUseItem = true;
				if (_itemCheck == null)
				{
					foreach (var m in typeof(Player).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
					if (_itemCheck == null) throw new Exception("Player.ItemCheck not found");
				}
				_itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 0 } : null);
			}
			catch (Exception e) { var ie = (e is System.Reflection.TargetInvocationException && e.InnerException != null) ? e.InnerException : e; if ((ie.StackTrace ?? "").Contains("ItemCheck_PlayInstruments")) { c.PlayInstrNre++; } else c.Exceptions.Add("itemcheck:" + ie.GetType().Name + ":" + ie.Message + " @ " + string.Join(" < ", (ie.StackTrace ?? "").Split((char)10).Take(3).Select(x => x.Trim()))); }
			finally { Main.myPlayer = saved; }
		}

		internal void ApplyPlayerInputInUpdate() { if (_stage == 1 && _ph == Ph.Run && Main.myPlayer == 0) ApplyPlayerInput(); }

		internal void ApplyPlayerInput()
		{
			var c = _cur;
			if (c == null || !c.SetupOk || !c.IsBaseline || c.Weapon == null) return;
			Player p = Main.player[0];
			Item w = c.Weapon;
			if (p.inventory[0] == null || p.inventory[0].IsAir || p.inventory[0].type != w.type)
			{
				Item h2 = w.Clone(); if (h2.consumable) h2.stack = Math.Min(h2.maxStack > 1 ? h2.maxStack : 999, 999);
				p.inventory[0] = h2;
			}
			p.selectedItem = 0; p.noItems = false; p.dead = false;
			Vector2 aim = p.Center;
			if (c.AimAtAlly && c.Player2) aim = Main.player[1].Center;
			else if (c.AimAtAlly && c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active) aim = Main.npc[c.AllyIdx].Center;
			else if (c.TargetIdx >= 0 && Main.npc[c.TargetIdx].active) aim = Main.npc[c.TargetIdx].Center;
			Main.screenPosition = Vector2.Zero;
			Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
			bool hold = w.autoReuse || w.channel;
			bool press = hold || (_phTick % 2 == 0);
			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && w.channel;
			try { Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
		}

		// ---- owner-lab hooks (harness-only; see OwnerLab.cs) ----
		internal int Cx => _cx;
		internal SoldierRecord LabMakeRecord(string name) => MakeRecord(name);
		internal void LabClearAll() => ClearAll();
		internal void LabFinish(string why) => Finish(why);

		private SoldierRecord MakeRecord(string name)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy,
				Temperament = Temperament.Sellsword, Male = true, Name = name,
				CommanderName = CommanderName, CommanderIndex = 0,
				LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, // 500 hp / 200 mana (a fully grown soldier)
				HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			return rec;
		}

		/// <summary>Spawns the pinned target with its center at (centerX, whatever puts its feet on the floor).</summary>
		private void SpawnTarget(float centerX)
		{
			Vector2 pos = new Vector2(centerX, FloorY * 16f);
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyHarness"), (int)pos.X, (int)pos.Y, NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("target NewNPC failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>();
			ht.IsTarget = true;
			n.lifeMax = 1_000_000; n.life = 1_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f;
			n.width = 64; n.height = 96; // a generous hitbox (humanoid-sized) so Levy's +-6 degree aim error does not hide a working weapon
			n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f;
			n.velocity = Vector2.Zero;
			n.Bottom = pos;
			ht.Pin = n.position;
			n.netUpdate = true;
			_cur.TargetIdx = i;
			_cur.Extra.Clear();
			for (int k = 1; k < AltPolicyLab.CrowdN; k++) // altfire-survey: a crowd of pinned slimes 2.5 tiles apart around the target
			{
				int j = NPC.NewNPC(new EntitySource_WorldEvent("LevyHarness"), (int)(centerX + ((k + 1) / 2) * 40f * (k % 2 == 1 ? 1 : -1)), (int)pos.Y, NPCID.GreenSlime);
				if (j < 0 || j >= Main.maxNPCs) continue;
				NPC e = Main.npc[j]; var eh = e.GetGlobalNPC<HarnessTarget>(); eh.IsTarget = true;
				e.lifeMax = 1_000_000; e.life = 1_000_000; e.defense = 0; e.damage = 0; e.knockBackResist = 0f; e.width = 64; e.height = 96;
				e.noGravity = true; e.noTileCollide = true; e.aiStyle = -1; e.friendly = false; e.dontTakeDamage = false; e.value = 0f; e.velocity = Vector2.Zero;
				e.Bottom = new Vector2(centerX + ((k + 1) / 2) * 40f * (k % 2 == 1 ? 1 : -1), pos.Y); eh.Pin = e.position; e.netUpdate = true;
				_cur.Extra.Add(j);
			}
		}

		private static Item FindAmmo(Item weapon, string preferredName)
		{
			int want = weapon.useAmmo;
			int type = 0;
			if (preferredName != "") { type = preferredName == "Musket Ball" ? ItemID.MusketBall : preferredName == "Wooden Arrow" ? ItemID.WoodenArrow : 0; }
			if (type == 0)
			{
				if (want == AmmoID.Bullet) type = ItemID.MusketBall;
				else if (want == AmmoID.Arrow) type = ItemID.WoodenArrow;
				else if (want == AmmoID.Rocket) type = ItemID.RocketI;
				else if (want == AmmoID.Dart) type = ItemID.PoisonDart;
				else if (want == AmmoID.Gel) type = ItemID.Gel;
				else if (want == AmmoID.Flare) type = ItemID.Flare;
				else if (want == AmmoID.Snowball) type = ItemID.Snowball;
				else if (want == AmmoID.Stake) type = ItemID.Stake;
				else if (want == AmmoID.Sand) type = ItemID.SandBlock;
				else
				{
					for (int t = 1; t < ItemLoader.ItemCount && type == 0; t++)
					{
						Item s = ContentSamples.ItemsByType.TryGetValue(t, out var ss) ? ss : null;
						if (s != null && !s.IsAir && s.ammo == want && !s.notAmmo) type = t;
					}
				}
			}
			if (type == 0) return null;
			Item a = new Item(); a.SetDefaults(type);
			a.stack = Math.Min(a.maxStack, LevyConstants.AmmoPouchMax);
			return a;
		}

		/// <summary>Horizontal offset (px) that puts the target's CENTER exactly <paramref name="rangeTiles"/> tiles (Euclidean) from a user whose center is <paramref name="userCenterY"/>, target feet on the floor.</summary>
		private static float TargetDx(float rangeTiles, float userCenterY)
		{
			float targetCenterY = FloorY * 16f - 48f; // 96 px tall, feet on the floor
			float dy = userCenterY - targetCenterY;
			float r = rangeTiles * 16f;
			return (float)Math.Sqrt(Math.Max(0f, r * r - dy * dy));
		}

		/// <summary>LEVY_HARNESS_ARMOR: ModItem internal names (in the tested mod) of head/body/legs pieces both the soldier and the player baseline wear.</summary>
		private static List<Item> ArmorItems()
		{
			var list = new List<Item>();
			foreach (string n in _armorNames)
			{
				if (!ModContent.TryFind<ModItem>(TestMod + "/" + n, out ModItem mi)) continue;
				Item it = new Item(); it.SetDefaults(mi.Type); list.Add(it);
			}
			return list;
		}

		private static List<Item> AccItems()
		{
			var list = new List<Item>();
			foreach (string n in _accNames)
			{
				if (!ModContent.TryFind<ModItem>(TestMod + "/" + n, out ModItem mi)) continue;
				Item it = new Item(); it.SetDefaults(mi.Type); list.Add(it);
			}
			return list;
		}

		private static bool CalAvail()
		{
			try { return ModLoader.HasMod("CalamityMod") && HarnessCalamityProbe.Available; } catch (Exception) { return false; }
		}

		private static void CalStart(Cur c, Player p)
		{
			try
			{
				if (!CalAvail()) return;
				c.CalOn = true;
				float[] st = HarnessCalamityProbe.Stealth(p);
				if (st != null) { c.StealthStart = st[0]; c.StealthEnd = st[0]; c.StealthMax = st[1]; c.StealthPeak = st[0]; }
				if (c.IsBaseline) c.RA0 = HarnessCalamityProbe.RageAdrenaline(p);
			}
			catch (Exception e) { c.Exceptions.Add("calstart:" + e.Message); }
		}

		private static void CalSample(Cur c, Player p)
		{
			if (!c.CalOn) return;
			try
			{
				float[] st = HarnessCalamityProbe.Stealth(p);
				if (st == null) return;
				c.StealthEnd = st[0]; if (st[1] > c.StealthMax) c.StealthMax = st[1]; if (st[0] > c.StealthPeak) c.StealthPeak = st[0];
				if (st[1] > 0f && st[0] >= st[1] * 0.999f) c.StealthFilled = true;
				if (HarnessCalamityProbe.StealthStrikeAvailable(p)) c.StealthAvailTicks++;
				if (c.IsBaseline) c.RA1 = HarnessCalamityProbe.RageAdrenaline(p);
			}
			catch (Exception e) { if (c.Exceptions.Count < 3) c.Exceptions.Add("calsample:" + e.Message); }
		}

		private void StartRun()
		{
			var c = _cur;
			if (c == null || !c.SetupOk) return;
			try
			{
				Player p = Main.player[0];
				bool companion = c.Kind != "" || c.T.Control == "negc" || _forceCompanion;
				p.statLifeMax = 500; p.statLifeMax2 = 500;
				p.statLife = companion && !_onlyLevies ? (_cmdHpPct > 0 ? 500 * _cmdHpPct / 100 : 250) : 500;
				c.CmdBefore = c.CmdMax = c.CmdAfter = p.statLife; c.CmdPrev = p.statLife;
				if (c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active) { c.AllyBefore = c.AllyMax = c.AllyAfter = Main.npc[c.AllyIdx].life; }
				if (c.IsBaseline)
				{
					if (c.Player2) { c.P2Before = c.P2After = c.P2Max = c.P2Prev = Main.player[1].statLife; }
					_p2Alive = c.Player2;
					SetKeepAlive(true);
					// melee-class items cannot reach a target 15+ tiles away from a player who does not move: put the target in swing reach instead (recorded in startDist)
					float rng = c.T.Range;
					if (c.Weapon != null && c.Weapon.DamageType.CountsAsClass(DamageClass.Melee) && !c.Weapon.noMelee && c.Weapon.shoot <= 0) { rng = 2.5f; c.BaselineNote += "melee: target placed at 2.5 tiles;"; }
					float dx = TargetDx(rng, p.Center.Y);
					SpawnTarget(p.Center.X + dx);
					Main.npc[c.TargetIdx].GetGlobalNPC<HarnessTarget>().Pin = Main.npc[c.TargetIdx].position;
					if (c.Kind == "bard") { c.InspStart = c.InspPrev = HarnessThoriumProbe.PlayerBardResource(p); c.InspMax = HarnessThoriumProbe.PlayerBardMax(p); }
					c.StartDist = Vector2.Distance(p.Center, Main.npc[c.TargetIdx].Center) / 16f;
					CalStart(c, p);
					_pendingProj.Clear();
					if (HarnessOnHit.Enabled) HarnessOnHit.BeginWindow(c.Weapon?.Name ?? (c.T.Control == "negc" ? "(no weapon) COMPANION" : "(no weapon)"), c.T.GearLabel, c.T.Range.ToString(), 1);
					return;
				}
				var body = Main.npc[c.BodyIdx].ModNPC as SoldierNPC;
				if (_selfHpPct < 100) { int nl = Math.Max(1, body.NPC.lifeMax * _selfHpPct / 100); body.NPC.life = nl; c.Rec.CurrentHp = nl; }
				c.SelfLifeMax = body.NPC.lifeMax; c.SelfBefore = c.SelfAfter = c.SelfMax = c.SelfPrev = body.NPC.life;
				float rangeT = c.T.Walk ? 40f : c.T.Range;
				float dx2 = TargetDx(rangeT, body.NPC.Center.Y);
				SpawnTarget(body.NPC.Center.X + dx2);
				c.StartX = body.NPC.Center.X;
				c.ManaStart = c.ManaMin = c.Rec.Mana;
				if (c.Kind == "bard") { c.InspStart = c.InspPrev = HarnessThoriumProbe.BardResource(body); c.InspMax = HarnessThoriumProbe.BardMax(body); }
				c.StartDist = Vector2.Distance(Main.npc[c.BodyIdx].Center, Main.npc[c.TargetIdx].Center) / 16f;
				CalStart(c, body.Dummy);
				_pendingProj.Clear();
				if (HarnessOnHit.Enabled) { SetKeepAlive(true); HarnessOnHit.BeginWindow(c.Weapon?.Name ?? (c.T.Control == "negc" ? "(no weapon) COMPANION" : "(no weapon)"), c.T.GearLabel, c.T.Range.ToString(), 0); }
			}
			catch (Exception e) { c.Exceptions.Add("startrun:" + e.GetType().Name + ":" + e.Message); }
		}

		private void SampleTick()
		{
			var c = _cur;
			if (c == null || !c.SetupOk) return;
			try
			{
				c.ActiveTick++;
				NPC bn = Main.npc[c.BodyIdx];
				bool alive = bn.active && bn.ModNPC is SoldierNPC sb && ReferenceEquals(sb.Record, c.Rec) && sb.IsAlive;
				if (!alive) { c.DeadTicks++; }
				else
				{
					c.AliveTicks++;
					var s = (SoldierNPC)bn.ModNPC;
					if (c.SelfPrev >= 0 && bn.life > c.SelfPrev) { int sg = bn.life - c.SelfPrev; c.SelfGainSum += sg; if (sg >= 3) c.SelfBig++; }
					c.SelfPrev = bn.life; c.SelfAfter = bn.life; if (bn.life > c.SelfMax) c.SelfMax = bn.life;
					NPC tn = Main.npc[c.TargetIdx];
					float dNow = tn.active ? Vector2.Distance(bn.Center, tn.Center) / 16f : -1f;
					if (s.ItemAnimation > 0) { c.AnimTicks++; if (c.FirstFire < 0) c.FirstFire = c.ActiveTick; if (c.FirstShotDist < 0) c.FirstShotDist = dNow; }
					if (s.State == SoldierState.Engage) { c.EngageTicks++; if (c.EngageDist < 0) c.EngageDist = dNow; }
					c.States.Add(s.State.ToString());
					if (s.TargetIndex == c.TargetIdx) c.AcquiredTicks++;
					if (tn.active && dNow < c.MinDist) c.MinDist = dNow;
					c.MovedX = bn.Center.X - c.StartX;
					c.ManaEnd = c.Rec.Mana; if (c.Rec.Mana < c.ManaMin) c.ManaMin = c.Rec.Mana;
					if (_orbProbe) TrackOrbs(c, bn.Center);
					if (_orbDirect && ModLoader.HasMod("ThoriumMod") && c.ActiveTick % 40 == 0 && c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active)
					{
						int sm2 = Main.myPlayer; Main.myPlayer = 0;
						try { HarnessThoriumProbe.SpawnOrb(Main.npc[c.AllyIdx].Center + new Vector2((c.ActiveTick / 40 % 2 == 0 ? 1 : -1) * (50 + 20 * (c.ActiveTick / 40 % 4)), -30f), s); _orbDirectN++; } finally { Main.myPlayer = sm2; }
					}
					if (_vanProbe)
					{
						// follow every vanilla heal bolt: how close to the soldier it was when it vanished (it vanishes on arrival, or times out)
						for (int pi = 0; pi < Main.maxProjectiles; pi++)
						{
							Projectile q = Main.projectile[pi];
							if (q.active && q.type == ProjectileID.VampireHeal) { _vbSeen[pi] = (q.identity, Vector2.Distance(q.Center, bn.Center), q.timeLeft); }
							else if (_vbSeen.TryGetValue(pi, out var last)) { _vbGone++; _vbLastDist += last.dist; _vbLastTime += last.timeLeft; _vbSeen.Remove(pi); }
						}
					}
					if (_vanProbe && c.ActiveTick % 25 == 0 && tn.active)
					{
						// vanilla's own lifesteal bolt (what Projectile.vampireHeal spawns on a vanilla knife's hit), born at the enemy, owner = the commander, 8 life: it homes on Main.player[owner] and does statLife += 8 on arrival
						int sm = Main.myPlayer; Main.myPlayer = 0; // as in single player: the soldier's shot is owned by the commander
						Projectile vb; try { vb = WeaponUse.Spawn(s, tn.Center, Vector2.Zero, ProjectileID.VampireHeal, 1, 0f, null, 0f, 8f); } finally { Main.myPlayer = sm; }
						if (vb != null) { vb.friendly = false; vb.damage = 0; } // a heal bolt, not a shot: it must not hit the enemy it is born on
					}
					if (bn.TryGetGlobalNPC(out SoldierThoriumGlobalNPC g)) c.EmpUnion |= g.EmpMask;
					CalSample(c, s.Dummy);
					if (c.Kind == "bard")
					{
						int r = HarnessThoriumProbe.BardResource(s);
						if (c.InspPrev >= 0 && r < c.InspPrev) c.InspSpent += c.InspPrev - r;
						c.InspPrev = r; c.InspEnd = r;
					}
				}
				SampleCommon(c);
			}
			catch (Exception e) { c.Exceptions.Add("sample:" + e.GetType().Name + ":" + e.Message); }
		}

		private void TrackOrbs(Cur c, Vector2 fallback)
		{
						Vector2 ally = c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active ? Main.npc[c.AllyIdx].Center : fallback;
			for (int pi = 0; pi < Main.maxProjectiles; pi++)
			{
				Projectile q = Main.projectile[pi];
				bool isOrb = q.active && q.ModProjectile != null && q.ModProjectile.Name.StartsWith("HealingOrb");
				if (isOrb)
				{
					float dd = Vector2.Distance(q.Center, ally);
					if (!_orbSeen.TryGetValue(pi, out var o) || o.id != q.identity) { _orbSeen[pi] = (q.identity, dd, q.timeLeft, 0); _orbN++; if (_orbTraceId < 0) { _orbTraceId = q.identity; _orbTrace = ""; } }
					else { _orbSeen[pi] = (o.id, Math.Min(o.min, dd), q.timeLeft, o.age + 1); if (q.identity == _orbTraceId && o.age % 15 == 0 && _orbTrace.Length < 700) _orbTrace += $" [{o.age}t d{dd:0} v{q.velocity.Length():0.0}]"; }
				}
				else if (_orbSeen.TryGetValue(pi, out var last))
				{
					if (last.id == _orbTraceId) _orbTraceId = -1;
					_orbEnded++; if (last.min <= 40f) _orbNear++; if (last.timeLeft > 20) _orbKilledEarly++; _orbMinSum += last.min; _orbSeen.Remove(pi);
				}
			}
					
		}

		private void SamplePlayerTick()
		{
			var c = _cur;
			if (c == null || !c.SetupOk) return;
			try
			{
				c.ActiveTick++; c.AliveTicks++;
				Player p = Main.player[0];
				CalSample(c, p);
				if (p.itemAnimation > 0) { c.AnimTicks++; if (c.FirstFire < 0) c.FirstFire = c.ActiveTick; }
				if (_orbProbe) TrackOrbs(c, p.Center);
				if (_orbDirect && ModLoader.HasMod("ThoriumMod") && c.ActiveTick % 40 == 0 && c.AllyIdx >= 0 && Main.npc[c.AllyIdx].active)
				{
					int sm2 = Main.myPlayer; Main.myPlayer = 0;
					try { HarnessThoriumProbe.SpawnOrb(Main.npc[c.AllyIdx].Center + new Vector2((c.ActiveTick / 40 % 2 == 0 ? 1 : -1) * (50 + 20 * (c.ActiveTick / 40 % 4)), -30f), null); _orbDirectN++; } finally { Main.myPlayer = sm2; }
				}
				if (c.Kind == "bard")
				{
					int r = HarnessThoriumProbe.PlayerBardResource(p);
					if (c.InspPrev >= 0 && r < c.InspPrev) c.InspSpent += c.InspPrev - r;
					c.InspPrev = r; c.InspEnd = r;
					c.PlayerEmpUnion |= HarnessThoriumProbe.PlayerEmpMask(p);
				}
				else if (c.Kind != "") c.PlayerEmpUnion |= HarnessThoriumProbe.PlayerEmpMask(p);
				SampleCommon(c);
			}
			catch (Exception e) { c.Exceptions.Add("sample:" + e.GetType().Name + ":" + e.Message); }
		}

		/// <summary>Target damage, commander / ally life, buffs, projectiles: identical for the soldier window and the player window.</summary>
		private void SampleCommon(Cur c)
		{
			NPC t = Main.npc[c.TargetIdx];
			if (HarnessOnHit.Enabled) HarnessOnHit.Tick(c.BodyIdx >= 0 && Main.npc[c.BodyIdx].active ? Main.npc[c.BodyIdx].Center : Main.player[0].Center, t.Center);
			if (t.active && t.life < t.lifeMax) { c.Damage += t.lifeMax - t.life; c.Hits++; t.life = t.lifeMax; }
			if (!t.active) c.Exceptions.Add("target vanished @tick" + c.ActiveTick);
			foreach (int xi in c.Extra) { NPC xe = Main.npc[xi]; if (xe.active && xe.life < xe.lifeMax) { c.Damage += xe.lifeMax - xe.life; c.Hits++; xe.life = xe.lifeMax; } }

			Player p = Main.player[0];
			if (c.CmdPrev >= 0 && p.statLife > c.CmdPrev) { int g = p.statLife - c.CmdPrev; c.CmdGainSum += g; if (g >= 3) c.CmdBig++; }
			c.CmdPrev = p.statLife;
			c.CmdAfter = p.statLife; if (p.statLife > c.CmdMax) c.CmdMax = p.statLife;
			foreach (int b in p.buffType) if (b > 0) c.CmdBuffNames.Add(BuffLoader.GetBuff(b)?.Name ?? ("buff" + b));
			if (c.AllyIdx >= 0)
			{
				NPC an = Main.npc[c.AllyIdx];
				if (an.active && an.ModNPC is SoldierNPC)
				{
					if (c.AllyPrev >= 0 && an.life > c.AllyPrev) { int g = an.life - c.AllyPrev; c.AllyGainSum += g; if (g >= 3) c.AllyBig++; }
					c.AllyPrev = an.life;
					c.AllyAfter = an.life; if (an.life > c.AllyMax) c.AllyMax = an.life;
					if (an.TryGetGlobalNPC(out SoldierThoriumGlobalNPC ag)) c.AllyEmpUnion |= ag.EmpMask;
					for (int bi = 0; bi < an.buffType.Length; bi++) if (an.buffType[bi] > 0 && an.buffTime[bi] > 0) c.AllyBuffNames.Add(BuffLoader.GetBuff(an.buffType[bi])?.Name ?? ("buff" + an.buffType[bi]));
				}
			}
			if (c.Player2)
			{
				Player q = Main.player[1];
				if (c.P2Prev >= 0 && q.statLife > c.P2Prev) { int g = q.statLife - c.P2Prev; c.P2GainSum += g; if (g >= 3) c.P2Big++; }
				c.P2Prev = q.statLife; c.P2After = q.statLife; if (q.statLife > c.P2Max) c.P2Max = q.statLife;
				foreach (int b in q.buffType) if (b > 0) c.CmdBuffNames.Add("P2:" + (BuffLoader.GetBuff(b)?.Name ?? ("buff" + b)));
			}
			if (_medBag) { try { HarnessThoriumProbe.MedBag(c, out c.Med, out bool aimed); c.MedTicks++; if (aimed) c.MedAimed++; } catch (Exception e) { c.Med = "err:" + e.Message; } }
			ResolvePending();
		}

		private void ResolvePending()
		{
			var c = _cur;
			for (int i = 0; i < _pendingProj.Count; i++)
			{
				Projectile p = _pendingProj[i];
				string name = "vanilla" + p.type;
				if (p.ModProjectile != null) name = p.ModProjectile.Mod.Name + "/" + p.ModProjectile.Name;
				else if (ProjectileID.Search.TryGetName(p.type, out string vname)) name = vname;
				string key = name + "#" + p.type;
				bool ours;
				if (c.IsBaseline) ours = p.owner == 0;
				else { ours = false; if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) ours = tag.SoldierIndex == c.BodyIdx; }
				if (ours)
				{
					c.Proj[key] = c.Proj.TryGetValue(key, out int n) ? n + 1 : 1; c.ProjTotal++;
					if (c.FirstShotDist < 0 && !c.IsBaseline && c.BodyIdx >= 0 && c.TargetIdx >= 0 && Main.npc[c.BodyIdx].active && Main.npc[c.TargetIdx].active)
						c.FirstShotDist = Vector2.Distance(Main.npc[c.BodyIdx].Center, Main.npc[c.TargetIdx].Center) / 16f;
					if (name.EndsWith("SoldierHeal")) c.HealProjSeen++;
					if (c.CalOn && HarnessCalamityProbe.IsStealthStrike(p, out bool subStrike)) { if (subStrike) c.StealthSubs++; else c.StealthStrikes++; }
				}
				else c.ProjOther++;
			}
			_pendingProj.Clear();
		}

		private void EndTrial()
		{
			var c = _cur;
			if (c == null) return;
			if (c.SetupOk) ResolvePending();
			if (HarnessOnHit.Enabled && HarnessOnHit.Active) HarnessOnHit.EndWindow(c.Damage, c.Hits, c.ProjTotal, Main.player[0].statDefense);
			SetKeepAlive(false); _p2Alive = false;
			var cap = _capture?.End();
			if (cap != null) foreach (var m in cap) c.Exceptions.Add("log:" + m);
			if (_stage == 0) c.Notes += AltPolicyLab.TakeNotes();
			if (_stage == 0) _solCur = c; else _plCur = c;
		}

		/// <summary>Builds the TSV row from the soldier window (and the player baseline window, when one ran), aggregates, logs and writes.</summary>
		private void FinishRow()
		{
			var c = _solCur;
			if (c == null) return;
			string row = BuildRow(c, _plCur);
			_rows.Add(row);
			var a = _agg[c.T.Key]; a.Rows++;
			if (c.ProjTotal > 0 || c.AnimTicks > 0) a.Fired++;
			if (c.Damage > 0) a.Hit++;
			if (c.HealProjSeen > 0) a.HealerCast = true;
			if (c.CmdBig > 0 || c.AllyBig > 0) a.Healed = true;
			if (c.Exceptions.Count > 0 || (_plCur != null && _plCur.Exceptions.Count > 0)) a.Exc = true;
			bool last = _ti + 1 >= _trials.Count || _trials[_ti + 1].Key != c.T.Key;
			if (last)
				Mod.Logger.Info($"HARNESS SUMMARY | {a.Name} | issued={(a.Issued == 1 ? "Y" : "N")} | fired-in-windows={a.Fired}/{a.Rows} | hit-in-windows={a.Hit}/{a.Rows} | healer-cast={a.HealerCast} healed={a.Healed} | exceptions={a.Exc} | family={c.Family} | last-verdict={Verdict(c, _plCur)}");
			WriteTsv();
		}

		private static string Fmt(Dictionary<string, int> d) => d.Count == 0 ? "-" : string.Join(";", d.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value));

		// ---- verdict machinery ----
		/// <summary>Items that appeared during the window, minus world-side noise (tile-break drops such as stray arrows breaking a tile).</summary>
		private static int ItemsN(Cur c) => c == null ? 0 : c.Items.Where(k => !k.Key.Contains("@TileBreak")).Sum(k => k.Value);
		private static bool Big(Cur c) => c != null && (c.CmdBig > 0 || c.AllyBig > 0 || c.P2Big > 0);
		private static bool Emp(Cur c) => c != null && (c.EmpUnion != 0 || c.AllyEmpUnion != 0 || c.PlayerEmpUnion != 0 || c.CmdBuffNames.Count > 0 || c.AllyBuffNames.Count > 0);
		private static bool Something(Cur c) => c != null && (c.ProjTotal > 0 || c.Damage > 0 || Big(c) || ItemsN(c) > 0 || Emp(c) || c.InspSpent > 0);

		private static string Mech(Cur c)
		{
			if (c == null || !Big(c)) return "none";
			if (c.ProjTotal > 0) return ItemsN(c) > 0 ? "projectile+item" : "projectile";
			if (ItemsN(c) > 0) return "dropped-item";
			return "direct";
		}

		private static string Verdict(Cur s, Cur pl)
		{
			if (pl == null) return "-";
			if (!pl.SetupOk) return "BASELINE_FAILED(setup)";
			if (pl.Exceptions.Any(x => x.Contains("AchievementsHelper"))) return "BASELINE_FAILED(server has no achievements: ItemCheck_Shoot aborted)";
			if (pl.AnimTicks == 0 && pl.ProjTotal == 0) return "BASELINE_FAILED(player never began item use)";
			bool S = s != null && s.SetupOk && Something(s), P = Something(pl);
			if (!S && !P) return "BOTH_NOTHING";
			if (P && !S) return "PLAYER_ONLY";
			if (S && !P) return "SOLDIER_ONLY";
			bool sameProj = new HashSet<string>(s.Proj.Keys).SetEquals(pl.Proj.Keys);
			bool same = sameProj && (s.Damage > 0) == (pl.Damage > 0) && Mech(s) == Mech(pl) && (ItemsN(s) > 0) == (ItemsN(pl) > 0)
				&& Emp(s) == Emp(pl) && (s.InspSpent > 0) == (pl.InspSpent > 0);
			return same ? "SAME" : "DIFFERENT";
		}

		private static string StealthCell(Cur c) => !c.CalOn || c.StealthStart < 0f ? "-" : $"{c.StealthStart:0.00}>{c.StealthEnd:0.00}/max{c.StealthMax:0.00},peak{c.StealthPeak:0.00}";
		private static string Mask(uint m) => m == 0 ? "-" : "0x" + m.ToString("x");
		private static string Names(IEnumerable<string> n) { var l = n.OrderBy(x => x).ToList(); return l.Count == 0 ? "-" : string.Join(",", l); }

		private string BuildRow(Cur c, Cur pl)
		{
			var t = c.T;
			Item w = c.Weapon;
			string name = w == null ? (t.Control == "negc" ? "(no weapon, with wounded ally + 50% commander)" : "(no weapon)") : w.Name;
			string inner = w?.ModItem?.Name ?? (w != null ? ItemID.Search.GetName(w.type) : "-");
			string mod = w == null ? "-" : (w.ModItem?.Mod.Name ?? "Terraria");
			bool comp = c.AllyIdx >= 0 || c.Kind.StartsWith("healer") || c.Kind == "bard";
			var f = new List<string>
			{
				t.Control == "pos" ? "POSCONTROL" : t.Control == "neg" ? "NEGCONTROL" : t.Control == "negc" ? "NEGCONTROL-COMPANION" : "weapon",
				name, (w?.type ?? 0).ToString(), inner, mod, c.Family, c.Kind == "" ? "-" : c.Kind,
				c.Issuable ? "Y" : "N", c.Where, c.Ammo, t.Range == 0 ? "-" : (t.Walk ? "walk" : t.Range.ToString()), c.SetupOk ? "Y" : "N",
				c.AliveTicks + "/" + c.ActiveTick, string.Join(",", c.States.OrderBy(x => x)), c.EngageTicks.ToString(), c.AcquiredTicks.ToString(),
				c.MinDist > 9000 ? "-" : c.MinDist.ToString("0.0"), c.StartDist.ToString("0.0"), c.MovedX.ToString("0"),
				c.EngageDist < 0 ? "-" : c.EngageDist.ToString("0.0"), c.FirstShotDist < 0 ? "-" : c.FirstShotDist.ToString("0.0"),
				(c.ProjTotal > 0 ? "Y" : "N"), c.ProjTotal.ToString(), Fmt(c.Proj), c.ProjOther.ToString(),
				c.AnimTicks.ToString(), c.FirstFire.ToString(),
				c.Damage.ToString(), c.Hits.ToString(),
				comp ? $"{c.CmdBefore}>{c.CmdAfter}(max{c.CmdMax})" : "-",
				c.AllyIdx >= 0 ? $"{c.AllyBefore}>{c.AllyAfter}(max{c.AllyMax}/{c.AllyMaxLife})" : "-",
				c.AllyIdx >= 0 ? $"cmd+{c.CmdGainSum}/big{c.CmdBig} ally+{c.AllyGainSum}/big{c.AllyBig}" : "-",
				c.InspStart < 0 ? "-" : $"{c.InspStart}>{c.InspEnd}/{c.InspMax}", c.Kind == "bard" ? c.InspSpent.ToString() : "-",
				Mask(c.EmpUnion), Mask(c.AllyEmpUnion), Names(c.CmdBuffNames), Names(c.AllyBuffNames),
				c.Items.Count == 0 ? "-" : Fmt(c.Items),
				c.SetupOk ? $"{c.ManaStart}>{c.ManaEnd}(min{c.ManaMin})" : "-",
				c.Exceptions.Count == 0 ? "-" : string.Join(" || ", c.Exceptions.Take(6).Select(Clean)) + (c.Exceptions.Count > 6 ? " ||(+" + (c.Exceptions.Count - 6) + " more)" : ""),
				string.IsNullOrEmpty(c.Notes) ? "-" : Clean(c.Notes),
			};
			// ---- player baseline columns ----
			if (pl == null) f.AddRange(Enumerable.Repeat("-", 24));
			else
			{
				bool plComp = pl.AllyIdx >= 0 || pl.Kind.StartsWith("healer") || pl.Kind == "bard";
				f.Add(!pl.SetupOk ? "SETUP_FAILED" : (pl.AnimTicks == 0 && pl.ProjTotal == 0 ? "NO_USE" : "OK"));
				f.Add(pl.StartDist.ToString("0.0"));
				f.Add(pl.AnimTicks.ToString());
				f.Add(pl.ProjTotal.ToString()); f.Add(Fmt(pl.Proj)); f.Add(pl.ProjOther.ToString());
				f.Add(pl.Damage.ToString()); f.Add(pl.Hits.ToString());
				f.Add(plComp ? $"{pl.CmdBefore}>{pl.CmdAfter}(max{pl.CmdMax})" : "-");
				f.Add(pl.AllyIdx >= 0 ? $"{pl.AllyBefore}>{pl.AllyAfter}(max{pl.AllyMax}/{pl.AllyMaxLife})" : "-");
				f.Add(pl.AllyIdx >= 0 ? $"cmd+{pl.CmdGainSum}/big{pl.CmdBig} ally+{pl.AllyGainSum}/big{pl.AllyBig}" : "-");
				f.Add(pl.InspStart < 0 ? "-" : $"{pl.InspStart}>{pl.InspEnd}/{pl.InspMax}"); f.Add(pl.Kind == "bard" ? pl.InspSpent.ToString() : "-");
				f.Add(Mask(pl.PlayerEmpUnion)); f.Add(Mask(pl.AllyEmpUnion));
				f.Add(Names(pl.CmdBuffNames)); f.Add(Names(pl.AllyBuffNames));
				f.Add(pl.Items.Count == 0 ? "-" : Fmt(pl.Items));
				f.Add(pl.Exceptions.Count == 0 ? "-" : string.Join(" || ", pl.Exceptions.Take(6).Select(Clean)) + (pl.Exceptions.Count > 6 ? " ||(+" + (pl.Exceptions.Count - 6) + " more)" : ""));
				f.Add((string.IsNullOrEmpty(pl.BaselineNote) ? "" : pl.BaselineNote) + (pl.Kind.StartsWith("healer") && !Big(pl) ? "NO player life rise observed: a heal on another player is applied by the target player's client / mod packet, which a client-less server baseline cannot deliver;" : "") + (pl.KillsSuppressed > 0 ? "PLAYER DEATH SUPPRESSED x" + pl.KillsSuppressed + ";" : "") + (pl.StrikePackets > 0 ? "strike packets applied locally x" + pl.StrikePackets + ";" : "") + (pl.PlayInstrNre > 0 ? "ItemCheck_PlayInstruments NRE x" + pl.PlayInstrNre + " (server has no audio; ignored);" : "") is string nn && nn != "" ? nn : "-");
				f.Add(pl.AimAtAlly ? "ally" : "target");
				f.Add(Names(pl.Proj.Keys.Where(k => !c.Proj.ContainsKey(k))));
				f.Add(pl.Packets.Count == 0 ? "-" : string.Join(";", pl.Packets.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value)) + (pl.PacketHeals > 0 ? " (heal packets applied: " + pl.PacketHeals + ")" : ""));
				f.Add(pl.Player2 ? $"{pl.P2Before}>{pl.P2After}(max{pl.P2Max}) +{pl.P2GainSum}/big{pl.P2Big}" : "-");
			}
			// ---- Calamity / class columns (appended after the verdict block) ----
			// ---- diff columns ----
			if (pl == null) f.AddRange(Enumerable.Repeat("-", 4));
			else
			{
				f.Add(Names(c.Proj.Keys.Where(k => !pl.Proj.ContainsKey(k))));
				f.Add(Mech(pl)); f.Add(Mech(c));
				f.Add(Verdict(c, pl));
			}
			f.Add(w == null ? "-" : ClassGroup(w) + "/" + (w.DamageType?.GetType().Name ?? "?"));
			f.Add(StealthCell(c)); f.Add(c.CalOn ? (c.StealthFilled ? "Y" : "N") : "-"); f.Add(c.CalOn ? c.StealthStrikes.ToString() : "-"); f.Add(c.CalOn ? c.StealthSubs.ToString() : "-"); f.Add(c.CalOn ? c.StealthAvailTicks.ToString() : "-");
			if (pl == null) f.AddRange(Enumerable.Repeat("-", 6));
			else
			{
				f.Add(StealthCell(pl)); f.Add(pl.CalOn ? (pl.StealthFilled ? "Y" : "N") : "-"); f.Add(pl.CalOn ? pl.StealthStrikes.ToString() : "-"); f.Add(pl.CalOn ? pl.StealthSubs.ToString() : "-"); f.Add(pl.CalOn ? pl.StealthAvailTicks.ToString() : "-");
				f.Add(pl.RA0 == null || pl.RA1 == null ? "-" : $"rage {pl.RA0[0]:0}>{pl.RA1[0]:0}/{pl.RA0[1]:0} adr {pl.RA0[2]:0}>{pl.RA1[2]:0}/{pl.RA0[3]:0}");
			}
			f.Add(_armorNames == null ? "-" : string.Join("+", _armorNames));
			f.Add(c.SelfLifeMax > 0 ? $"{c.SelfBefore}>{c.SelfAfter}(max{c.SelfMax}/{c.SelfLifeMax})" : "-");
			f.Add(c.SelfLifeMax > 0 ? $"self+{c.SelfGainSum}/big{c.SelfBig}" : "-");
			f.Add((c.MedTicks == 0 ? "-" : $"arrowAtPick {c.MedAimed}/{c.MedTicks} | {c.Med}") + (_orbDirect ? $" || orbsDropped={_orbDirectN}" : "") + (_orbProbe ? $" || orbs spawned={_orbN} ended={_orbEnded} endedWithin40px={_orbNear} endedBeforeTimeout={_orbKilledEarly} avgClosestPx={(_orbEnded > 0 ? _orbMinSum / _orbEnded : 0):0} trace:{_orbTrace}" : "") + (_vanProbe ? " || " + VampireProbeState + " routed=" + StaticInt("RoutedHeals") + "/" + StaticInt("RoutedLife") + " healTextOpenUnrouted=" + StaticInt("HealTextOpenUnrouted") + " healTextOutside=" + StaticInt("HealTextOutside") : ""));
			return string.Join("\t", f.Select(Clean));
		}

		private static string Clean(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

		private const string Header = "kind\tweapon\titemType\tinternal\tmod\tclassifier\tcompanionKind\tissued\theldIn\tammoGiven\trangeTiles\tsetupOk\taliveTicks\tstates\tengageTicks\ttargetAcquiredTicks\tminDistTiles\tstartDistTiles\tsoldierMovedPx\tengageDistTiles\tfirstShotDistTiles\tfired(proj)\tprojectiles\tprojTypes\tprojUntagged\titemAnimTicks\tfirstAnimTick\tdamageToTarget\ttargetHitTicks\tcommanderHp\tallyHp\tlifeRises\tinspiration(start>end/max)\tinspSpent\tempowermentsSoldier\tempowermentsAlly\tcommanderBuffs\tallyBuffs\titemsSpawned\tmana(start>end)\texceptions\tnotes"
			+ "\tpl_status\tpl_startDistTiles\tpl_itemAnimTicks\tpl_projectiles\tpl_projTypes\tpl_projNotOwner\tpl_damage\tpl_targetHitTicks\tpl_commanderHp\tpl_allyHp\tpl_lifeRises\tpl_inspiration\tpl_inspSpent\tpl_empowermentsPlayer\tpl_empowermentsAlly\tpl_playerBuffs\tpl_allyBuffs\tpl_itemsSpawned\tpl_exceptions\tpl_notes\tpl_aimedAt\tprojOnlyPlayer\tpl_vanillaPackets\tpl_player2Hp"
			+ "\tprojOnlySoldier\thealMechPlayer\thealMechSoldier\tverdict\tdmgClass\tstealth(start>end/max,peak)\tstealthFilled\tstealthStrikes\tstealthSubProj\tstealthAvailTicks\tpl_stealth\tpl_stealthFilled\tpl_stealthStrikes\tpl_stealthSubProj\tpl_stealthAvailTicks\tpl_rageAdrenaline\tarmor\tshooterHp\tshooterRises\tmedbag";

		private void WriteTsv()
		{
			try
			{
				var sb = new StringBuilder(); sb.AppendLine(Header);
				foreach (var r in _rows) sb.AppendLine(r);
				string tmp = OutPath + ".tmp";
				File.WriteAllText(tmp, sb.ToString());
				File.Move(tmp, OutPath, true);
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS: could not write TSV: " + e.Message); }
		}

		private void Teardown()
		{
			ClearAll();
			try { Main.player[1].active = false; } catch { }
		}

		/// <summary>Removes every soldier of the test commander (record + body), the target and all projectiles.</summary>
		private void ClearAll()
		{
			try
			{
				var ros = RosterSystem.Instance;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) { sn.Record.Gear = new SoldierGear(); } n.active = false; }
					else if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) n.active = false;
				}
				ros.Regulars.RemoveAll(r => r.CommanderName == CommanderName);
				for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active) Main.projectile[i].Kill();
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active) Main.item[i].active = false;
				_pendingProj.Clear();
			}
			catch (Exception e) { Mod.Logger.Warn("HARNESS: clear failed: " + e.Message); }
		}

		private void Finish(string why)
		{
			if (_ph == Ph.Done) return;
			_ph = Ph.Done;
			try { ClearAll(); } catch { }
			if (!OwnerLab.Active) WriteTsv(); // the owner lab writes its own TSV
			Mod.Logger.Info($"HARNESS: DONE ({why}); {_rows.Count} rows -> {OutPath}");
			Mod.Logger.Info("HARNESS: shutting the server down WITHOUT saving the world.");
			try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { }
			var th = new System.Threading.Thread(() => { System.Threading.Thread.Sleep(15000); Environment.Exit(0); }) { IsBackground = true };
			th.Start();
		}
	}

	/// <summary>Marks the harness target and pins it in place (no AI, no gravity, no knockback).</summary>
	public class HarnessTarget : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		public bool IsTarget;
		public Vector2 Pin;
		public override bool PreAI(NPC npc)
		{
			if (!IsTarget) return true;
			npc.velocity = Vector2.Zero; npc.position = Pin; npc.timeLeft = 3600; npc.noGravity = true;
			return false;
		}
		public override bool CheckActive(NPC npc) => !IsTarget;
	}

	/// <summary>While the harness is on: no vanilla spawns anywhere.</summary>
	public class HarnessSpawnSuppress : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
		{
			if (!CombatHarness.Enabled) return;
			spawnRate = 100000000; maxSpawns = 0;
		}
		public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
		{
			if (!CombatHarness.Enabled) return;
			pool.Clear();
		}
	}

	/// <summary>Reports every projectile that spawns during a trial's measured window to the harness.</summary>
	public class HarnessProjectileWatch : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!CombatHarness.Enabled) return;
			CombatHarness.Instance?.NoteSpawn(projectile);
		}
	}

	/// <summary>Reports every item that appears in the world during a trial's measured window (hearts, drops).</summary>
	public class HarnessItemWatch : GlobalItem
	{
		public override void OnSpawn(Item item, IEntitySource source)
		{
			if (!CombatHarness.Enabled) return;
			CombatHarness.Instance?.NoteItem(item, source);
			HarnessOnHit.OnItemSpawn(item, source);
		}
	}

	/// <summary>Captures WARN/ERROR log lines (and anything with an exception) logged during a trial, so a swallowed in-game exception is attributed to its trial.</summary>
	internal sealed class HarnessLogCapture : log4net.Appender.AppenderSkeleton
	{
		private readonly object _lock = new();
		private Dictionary<string, int> _msgs = new();
		private bool _on;
		private log4net.Repository.Hierarchy.Logger _root;

		public void Attach(log4net.ILog log)
		{
			var repo = (log4net.Repository.Hierarchy.Hierarchy)log.Logger.Repository;
			_root = repo.Root;
			Name = "LevyHarnessCapture";
			ActivateOptions();
			_root.AddAppender(this);
		}
		public void Detach() { try { _root?.RemoveAppender(this); } catch { } }
		public void Begin() { lock (_lock) { _msgs = new Dictionary<string, int>(); _on = true; } }
		public List<string> End()
		{
			lock (_lock)
			{
				_on = false;
				return _msgs.Select(k => (k.Value > 1 ? "x" + k.Value + " " : "") + k.Key).ToList();
			}
		}
		protected override void Append(log4net.Core.LoggingEvent e)
		{
			lock (_lock)
			{
				if (!_on) return;
				string msg = e.RenderedMessage ?? "";
				if (msg.StartsWith("HARNESS")) return;
				bool bad = e.Level >= log4net.Core.Level.Warn || e.ExceptionObject != null;
				if (!bad) return;
				string key = "[" + e.Level + " " + e.LoggerName + "] " + (msg.Length > 260 ? msg.Substring(0, 260) : msg);
				if (e.ExceptionObject != null) key += " {" + e.ExceptionObject.GetType().Name + ": " + e.ExceptionObject.Message + "}";
				key = key.Replace("\r", " ").Replace("\n", " ");
				_msgs[key] = _msgs.TryGetValue(key, out int n) ? n + 1 : 1;
			}
		}
	}
}
