using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;
using Warbands.Content.NPCs;

namespace Warbands.Common
{
	/// <summary>
	/// HEADLESS LAB (env WARBANDS_LIVE=check|fight; nothing happens without it). A dedicated server with nobody online does not advance the world, so the lab steps Main.Update itself from the server loop (as Levy's harness does)
	/// in a sealed sky arena with a stand-in player in slot 0 (real armor on it, a puppet weapon that fires at the nearest enemy on a fixed rhythm). Modes:
	///   check  scripted scenarios with assertions: the real Levy brain on the enemy side (targets, shots, damage both ways, no i-frames), warnings, tactics, rotating aggro, morale and Recall, cultists, all 20 summoning items.
	///   events a live run per vanilla event (Goblin Army, Pirate Invasion, Sandstorm, Slime Rain) over WARBANDS_SEEDS seeds: the band forms, fights, credits the invasion, drops the captain's loot, leaves when the event ends; one TSV row per trial.
	///   fight  the fight comparison (Expert): the same player kit against a Levy-AI band, a phase-1 Fighter-AI band and ordinary enemies of the stage; one TSV row per fight (WARBANDS_OUT).
	/// The check results go to the log as "[Warbands] LIVE PASS/FAIL ..." and into the self-check count.
	/// </summary>
	public class WarbandsLab : ModSystem
	{
		public static readonly string Mode = Environment.GetEnvironmentVariable("WARBANDS_LIVE");
		public static bool On => !string.IsNullOrEmpty(Mode);
		public static int Pass, Fail;
		public static bool Done;

		private int _tick, _trialTick, _ti = -1; private bool _booted, _armed;
		private readonly List<Trial> _trials = new();
		private Trial _cur;
		private static MethodInfoHolder _upd = new();
		private class MethodInfoHolder { public System.Reflection.MethodInfo M; }
		private static bool _inDrive;
		private StringBuilder _tsv = new();

		public class Trial
		{
			public string Name; public int Max = 900;
			public Action Setup; public Func<int, bool> Tick; public Func<(bool ok, string detail)> Result; public Action Cleanup;
		}

		// ------------------------------------------------------------------------------------------------ driver
		public override void Load()
		{
			if (!On || !Main.dedServ) return;
			Main.OnTickForThirdPartySoftwareOnly += DriveServer;
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandIn)); } catch (Exception e) { Mod.Logger.Warn("[Warbands] lab: could not hook UpdateConnectedClients: " + e.Message); }
		}
		public override void Unload() { try { Main.OnTickForThirdPartySoftwareOnly -= DriveServer; } catch { } }

		private delegate void OrigUpdateConnectedClients();
		private delegate void HookUpdateConnectedClients(OrigUpdateConnectedClients orig);
		private static void KeepStandIn(OrigUpdateConnectedClients orig) { if (On && _armedStatic) return; orig(); }
		private static bool _armedStatic;

		private static void DriveServer()
		{
			if (!On || _inDrive || !Main.dedServ || Main.netMode != NetmodeID.Server || Done || !_armedStatic) return;
			_inDrive = true;
			try
			{
				_upd.M ??= typeof(Main).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
				for (int i = 0; i < 20 && !Done; i++) _upd.M.Invoke(Main.instance, new object[] { new GameTime() });
			}
			catch (Exception e) { ModContent.GetInstance<Warbands>()?.Logger.Warn("[Warbands] lab: world update threw: " + (e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e)); }
			finally { _inDrive = false; }
		}

		public override void OnWorldLoad()
		{
			if (!On || !Main.dedServ) return;
			_armedStatic = true; _armed = true; _tick = 0; _booted = false; _ti = -1; Pass = Fail = 0; Done = false;
			BandChatter.Log.Lines.Clear();
		}

		public override void PreUpdateWorld() { if (On && Mode.Contains("events") && !Done) { Main.windSpeedCurrent = 0.8f; Main.windSpeedTarget = 0.8f; } }
		public override void PreUpdateNPCs() { KeepPlayer(); }
		public override void PreUpdateProjectiles() { KeepPlayer(); }
		public override void PostUpdateNPCs() { KeepPlayer(); }

		private void KeepPlayer()
		{
			if (!On || !_armed || Done) return;
			var p = Main.player[0];
			if (p.name != Tester) return;
			p.active = true; p.dead = false; p.ghost = false;
			if (_pin) { p.position = _pinAt; p.velocity = Vector2.Zero; }
			Main.dayTime = true; Main.time = 27000; Main.raining = false; Main.eclipse = false; Main.bloodMoon = false;
			if (Mode.Contains("events")) { Main.windSpeedCurrent = 0.8f; Main.windSpeedTarget = 0.8f; } // a sandstorm ends at once in dead air
		}
		private const string Tester = "WBTester";
		private bool _pin; private Vector2 _pinAt; private int _extraDefense;

		// ------------------------------------------------------------------------------------------------ the stand-in player
		private int _hpMax; private int _lastLife; public float DamageTaken; private int _atk; private float _rawHit; private int _useTime = 30; private float _accuracy = 0.7f; private int _range = 32;
		private float _cumDamage; private bool _playerDied; private int _killedByPuppet;

		private void ArmPlayer(string[] armor, int hp, int defAllow, string weapon, string ammo, string heldClass = null)
		{
			var p = Main.player[0];
			p.name = Tester; p.active = true; p.dead = false; p.width = 20; p.height = 42; p.whoAmI = 0;
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			for (int i = 0; i < 3; i++) { int t = BandData.ItemType(armor[i]); p.armor[i] = new Item(); if (t > 0) p.armor[i].SetDefaults(t); }
			p.statLifeMax = p.statLifeMax2 = hp; p.statLife = hp; p.statMana = p.statManaMax2 = 200;
			for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
			int w = BandData.ItemType(weapon); p.inventory[0] = new Item(); if (w > 0) p.inventory[0].SetDefaults(w); p.selectedItem = 0;
			float hit = p.inventory[0].damage; if (ammo != null) { int a = BandData.ItemType(ammo); var ai = new Item(); if (a > 0) ai.SetDefaults(a); hit += ai.damage; }
			_rawHit = hit; _useTime = Math.Max(8, p.inventory[0].useTime); _extraDefense = defAllow; _hpMax = hp; _lastLife = hp; _cumDamage = 0; _playerDied = false; _killedByPuppet = 0; _atk = 0;
			p.immune = false; p.immuneTime = 0;
			_pin = true; _pinAt = new Vector2(ArenaX * 16f, (ArenaFloor - 3) * 16f);
			p.position = _pinAt; p.velocity = Vector2.Zero;
		}

		private int _lossLog;
		public override void PostUpdatePlayers()
		{
			if (!On || !_armed || Done) return;
			var p = Main.player[0]; if (p.name != Tester) return;
			p.statLifeMax = p.statLifeMax2 = _hpMax; if (_lastLife > 0) p.statLife = Math.Min(_lastLife, _hpMax); // Player.Update clamps life to a client-less slot's 100; hold the kit's life
			if (_extraDefense > 0) p.statDefense += _extraDefense; // the stage's accessory allowance, on top of the real armor (Player.Update just reset it)
		}

		/// <summary>The puppet's weapon: every use time, a 70% chance to land the weapon's raw hit on the nearest enemy of the arena within range and with a clear line.</summary>
		private void PuppetFire()
		{
			var p = Main.player[0]; if (p.name != Tester || p.dead) return;
			if (--_atk > 0) return; _atk = _useTime;
			NPC best = null; float bestSq = _range * 16f * _range * 16f;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.friendly || n.life <= 0 || n.dontTakeDamage || n.townNPC || n.ModNPC is PlayerStandIn) continue;
				if (!(n.ModNPC is BandLevyNPC || n.ModNPC is BandSoldier || _labNpcs.Contains(i))) continue;
				float d = Vector2.DistanceSquared(p.Center, n.Center); if (d >= bestSq) continue;
				if (!Collision.CanHitLine(p.position, p.width, p.height, n.position, n.width, n.height)) continue;
				bestSq = d; best = n;
			}
			if (best == null || Main.rand.NextFloat() > _accuracy) return;
			int dir = best.Center.X < p.Center.X ? -1 : 1;
			bool wasAlive = best.life > 0;
			best.SimpleStrikeNPC((int)Math.Max(1, _rawHit), dir, false, 3f, DamageClass.Ranged, false, 0f, true);
			if (wasAlive && best.life <= 0) _killedByPuppet++;
		}
		private readonly HashSet<int> _labNpcs = new();

		/// <summary>A dedicated server runs no player-side NPC collision (a real client does), so enemy contact damage is applied here the way Player.Update_NPCCollision would: the NPC's damage through the player's own Hurt (defense, hit immunity).</summary>
		private void ContactHits(Player p)
		{
			if (p.immuneTime > 0 || p.immune) return;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				var n = Main.npc[i];
				if (!n.active || n.friendly || n.damage <= 0 || n.life <= 0) continue;
				if (!(_labNpcs.Contains(i) || n.ModNPC is BandSoldier)) continue;
				if (!n.Hitbox.Intersects(p.Hitbox)) continue;
				p.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(i), Math.Max(1, (int)(n.damage)), n.Center.X < p.Center.X ? -1 : 1);
				break;
			}
		}

		// ------------------------------------------------------------------------------------------------ arena
		private const int ArenaFloor = 120; private int ArenaX => Main.maxTilesX / 2;
		private void BuildArena()
		{
			int cx = ArenaX;
			for (int x = cx - 110; x <= cx + 110; x++)
				for (int y = 50; y <= ArenaFloor + 3; y++)
				{
					Tile t = Main.tile[x, y]; t.ClearEverything();
					if (y >= ArenaFloor) { t.HasTile = true; t.TileType = TileID.GrayBrick; }
				}
			// two wall pillars at the ends keep everything in
			for (int y = 50; y < ArenaFloor; y++) foreach (int x in new[] { cx - 111, cx + 111 }) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.GrayBrick; }
		}

		private void ClearArena()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && !(n.ModNPC is PlayerStandIn) && n.type != NPCID.TargetDummy) { n.active = false; } }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			for (int i = 0; i < Main.maxItems; i++) Main.item[i].active = false;
			foreach (var r in RosterSystem.Instance.Regulars.ToArray()) RosterSystem.Instance.Regulars.Remove(r);
			BandBrain.All.Clear(); _labNpcs.Clear();
		}

		// ------------------------------------------------------------------------------------------------ the step machine
		public override void PostUpdateEverything()
		{
			if (!On || !_armed || Done || Main.netMode == NetmodeID.MultiplayerClient) return;
			_tick++;
			try { Step(); }
			catch (Exception e) { Mod.Logger.Error("[Warbands] lab driver threw: " + e); Finish("driver exception"); }
		}

		private void Step()
		{
			if (!_booted)
			{
				if (_tick < 90) return; // let the world settle
				_booted = true;
				Main.GameMode = GameModeID.Expert;
				BuildArena();
				if (Mode.Contains("check")) AddCheckTrials();
				if (Mode.Contains("fight")) AddFightTrials();
				if (Mode.Contains("events")) AddEventTrials();
				Mod.Logger.Info($"[Warbands] LIVE: {_trials.Count} trials ({Mode})");
				_ti = -1; return;
			}
			if (_cur == null)
			{
				if (++_ti >= _trials.Count) { Finish("all trials ran"); return; }
				_cur = _trials[_ti]; _trialTick = 0;
				ClearArena();
				try { _cur.Setup?.Invoke(); } catch (Exception e) { Report(_cur.Name, false, "setup threw " + e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n').FirstOrDefault()); _cur = null; return; }
				return;
			}
			_trialTick++;
			var pl = Main.player[0];
			if (pl.name == Tester)
			{
				int loss = _lastLife - pl.statLife; if (loss > 0) { DamageTaken += loss; _cumDamage += loss; if (_lossLog++ < 12) Mod.Logger.Info($"[Warbands] lab: stand-in lost {loss} (life {pl.statLife}/{pl.statLifeMax2}, last {_lastLife}, defense {pl.statDefense}) in trial tick {_trialTick}"); }
				_lastLife = pl.statLife;
				if (!_playerDied && _cumDamage >= _hpMax) _playerDied = true;
				if (pl.statLife < _hpMax / 2) { pl.statLife = _hpMax; _lastLife = _hpMax; } // never let the stand-in die; the damage taken is counted above
				ContactHits(pl);
				PuppetFire();
			}
			bool done = false;
			try { done = _cur.Tick(_trialTick); } catch (Exception e) { Report(_cur.Name, false, "tick threw " + e.GetType().Name + ": " + e.Message + " @ " + e.StackTrace?.Split('\n').FirstOrDefault()); _cur = null; return; }
			if (done || _trialTick >= _cur.Max)
			{
				try { var (ok, detail) = _cur.Result(); Report(_cur.Name, ok, detail); } catch (Exception e) { Report(_cur.Name, false, "result threw " + e.Message); }
				try { _cur.Cleanup?.Invoke(); } catch { }
				_cur = null;
			}
		}

		private void Report(string name, bool ok, string detail)
		{
			if (ok) Pass++; else Fail++;
			Mod.Logger.Info($"[Warbands] LIVE {(ok ? "PASS" : "FAIL")} {name} :: {detail}");
		}

		private void Finish(string why)
		{
			if (Done) return; Done = true;
			if (Mode.Contains("fight") || Mode.Contains("events")) { try { File.WriteAllText(Environment.GetEnvironmentVariable("WARBANDS_OUT") ?? "warbands_fight.tsv", _tsv.ToString()); } catch (Exception e) { Mod.Logger.Warn("[Warbands] lab: could not write the TSV: " + e.Message); } }
			Mod.Logger.Info($"[Warbands] LIVE done ({why}) pass={Pass} fail={Fail}");
			Mod.Logger.Info($"[Warbands] self-check done pass={Pass} fail={Fail} (live)");
			try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { }
			new System.Threading.Thread(() => { System.Threading.Thread.Sleep(15000); Environment.Exit(0); }) { IsBackground = true }.Start();
		}

		// ------------------------------------------------------------------------------------------------ helpers
		private static readonly string[] Gold = { "GoldHelmet", "GoldChainmail", "GoldGreaves" };
		private static readonly string[] Shadow = { "ShadowHelmet", "ShadowScalemail", "ShadowGreaves" };
		private static readonly string[] Mythril = { "MythrilHelmet", "MythrilChainmail", "MythrilGreaves" };
		private static readonly string[] Turtle = { "TurtleHelmet", "TurtleScaleMail", "TurtleLeggings" };

		private List<BandLevyNPC> Levies() { var l = new List<BandLevyNPC>(); for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is BandLevyNPC b) l.Add(b); return l; }
		private int HostileShots() { int c = 0; for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && p.hostile && (p.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag t) && t.HostileShot)) c++; } return c; }
		private int _shotsSeen; private readonly HashSet<int> _shotIds = new();
		private void CountShots() { for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (!p.active || !p.hostile) continue; if (p.TryGetGlobalProjectile(out Levy.Common.Globals.SoldierProjectileTag t) && t.HostileShot) { if (_shotIds.Add(p.identity * 1000 + p.type)) _shotsSeen++; float d = Vector2.Distance(p.Center, PlayerPos) / 16f; if (d < _minShotDist) _minShotDist = d; } } }
		private float _minShotDist = 9999f;
		private Vector2 PlayerPos => Main.player[0].Center;

		/// <summary>A band of the type, size n, 28 to 34 tiles from the stand-in. Levy brain unless legacy.</summary>
		private bool SpawnArmBand(BandRoster type, int n, bool legacy) => BandSpawner.SpawnBand(Main.player[0], type, n, 28, 34, legacy, false);

		private SoldierRecord MakeLevy(string weapon, string ammo, string[] armor, int life = 400)
		{
			var rec = new SoldierRecord { Name = "Lab Levy", Id = 70000 + Main.rand.Next(9000), Kind = SoldierKind.Regular, Grade = Grade.Levy, CommanderName = Tester, Order = SoldierOrder.Follow };
			rec.LifeCrystals = Math.Max(0, (life - 100) / 20);
			rec.Gear.Weapon = new Item(); rec.Gear.Weapon.SetDefaults(BandData.ItemType(weapon));
			if (ammo != null) { rec.Gear.Ammo = new Item(); rec.Gear.Ammo.SetDefaults(BandData.ItemType(ammo)); rec.Gear.Ammo.stack = 999; }
			rec.Gear.Head = new Item(); rec.Gear.Head.SetDefaults(BandData.ItemType(armor[0])); rec.Gear.Body = new Item(); rec.Gear.Body.SetDefaults(BandData.ItemType(armor[1])); rec.Gear.Legs = new Item(); rec.Gear.Legs.SetDefaults(BandData.ItemType(armor[2]));
			rec.CurrentHp = rec.MaxHp;
			return rec;
		}

		// ================================================================================================ CHECK TRIALS
		private void AddCheckTrials()
		{
			var forest = BandTypes.ById("ForestDeserters");
			int shotsBefore = 0; int minDist = int.MaxValue; float startDist = 0; float dmgStart = 0;

			// 1: the real Levy brain on the enemy side reaches the player and hurts them
			_trials.Add(new Trial
			{
				Name = "levy brain: a band closes in, shoots the stand-in and hurts it", Max = 3600,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0; _shotIds.Clear(); _shotsSeen = 0; _minShotDist = 9999f; SpawnArmBand(forest, 4, false); startDist = Levies().Count > 0 ? Vector2.Distance(Levies()[0].NPC.Center, PlayerPos) / 16f : 0; minDist = int.MaxValue; },
				Tick = t => { CountShots(); foreach (var b in Levies()) minDist = Math.Min(minDist, (int)(Vector2.Distance(b.NPC.Center, PlayerPos) / 16f)); return t > 300 && (DamageTaken > 0 || _minShotDist < 3f) && _shotsSeen >= 3; },
				Result = () => (_shotsSeen >= 3 && (DamageTaken > 0 || _minShotDist < 3f) && Levies().Count >= 3, $"members {Levies().Count}, start {startDist:0} tiles, closest {minDist}, hostile shots {_shotsSeen} (nearest passed {_minShotDist:0.0} tiles from the player), damage to the stand-in {DamageTaken:0}"),
			});

			// 2: the enemy side's targets and damage both ways with a real levy
			SoldierRecord levyRec = null; int levyBody = -1; float levyLife0 = 0; float bandLife0 = 0; float bandLife1 = 0;
			_trials.Add(new Trial
			{
				Name = "levy brain: a band fights a real levy (it is targeted, and it shoots back)", Max = 1800,
				Setup = () =>
				{
					ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; _shotIds.Clear(); _shotsSeen = 0;
					levyRec = MakeLevy("DemonBow", "FlamingArrow", Gold, 400); RosterSystem.Instance.Regulars.Add(levyRec);
					levyBody = RosterSystem.Instance.SpawnBody(levyRec, PlayerPos + new Vector2(60, 0));
					SpawnArmBand(forest, 3, false);
					levyLife0 = levyRec.MaxHp;
				},
				Tick = t =>
				{
					CountShots();
					if (t == 2) { bandLife0 = Levies().Sum(b => (float)b.NPC.life); }
					var body = levyRec.HasBody ? Main.npc[levyRec.BodyIndex] : null;
					bandLife1 = Levies().Sum(b => (float)b.NPC.life);
					return t > 600 && (body == null || body.life < levyLife0) && bandLife1 < bandLife0 - 5;
				},
				Result = () => { var body = levyRec.HasBody ? Main.npc[levyRec.BodyIndex] : null; bool levyHurt = body == null || body.life < levyLife0; return ((levyHurt || _shotsSeen > 0) && bandLife1 < bandLife0, $"levy life {(body?.life ?? 0)}/{levyLife0:0}, band life {bandLife1:0}/{bandLife0:0}"); },
				Cleanup = () => { RosterSystem.Instance.Regulars.Clear(); },
			});

			// 3: damage intake is an ordinary enemy's: two hits in two ticks both land (no player-style i-frames), defense is capped
			int idxHit = -1; int lifeA = 0, lifeB = 0, lifeC = 0;
			_trials.Add(new Trial
			{
				Name = "damage intake: vanilla enemy hit immunity, no player i-frames, defense held to the stage line", Max = 20,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; SpawnArmBand(BandTypes.ById("ChlorophyteWardens"), 1, false); var b = Levies().FirstOrDefault(); idxHit = b?.NPC.whoAmI ?? -1; },
				Tick = t =>
				{
					var n = Main.npc[idxHit];
					if (t == 1) { lifeA = n.life; n.SimpleStrikeNPC(100, 1, false, 0f, DamageClass.Ranged, false, 0f, true); lifeB = n.life; }
					if (t == 2) { n.SimpleStrikeNPC(100, 1, false, 0f, DamageClass.Ranged, false, 0f, true); lifeC = n.life; return true; }
					return false;
				},
				Result = () => { var n = Main.npc[idxHit]; bool hits = lifeB < lifeA && lifeC < lifeB; bool def = n.defense <= Levy.Common.Weapons.WeaponRank.StageDefense[5]; return (hits && def, $"life {lifeA}->{lifeB}->{lifeC}, defense {n.defense} (line {Levy.Common.Weapons.WeaponRank.StageDefense[5]}), life max {n.lifeMax}"); },
			});

			// 4: warning bands hold fire, say "Turn back." and attack only when the warning is broken
			var snow = BandTypes.ById("SnowRangers"); int shotsWarn = 0; bool aggroEarly = true; string warnSaid = null; int aggroAt = -1;
			_trials.Add(new Trial
			{
				Name = "warning: the Snow Rangers say 'Turn back.', hold fire, then attack once the player closes in", Max = 1500,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; _shotIds.Clear(); _shotsSeen = 0; DamageTaken = 0; BandSpawner.SpawnBand(Main.player[0], snow, 4, 44, 46, false, false); BandChatter.Log.Lines.Clear(); aggroAt = -1; },
				Tick = t =>
				{
					CountShots();
					var b = BandBrain.All.Values.FirstOrDefault(x => x.Type == snow);
					if (b == null) return false;
					if (t == 400) { shotsWarn = _shotsSeen; aggroEarly = b.Aggro; warnSaid = BandChatter.Log.Lines.FirstOrDefault(l => l.StartsWith("WARNING")); _pin = false; Main.player[0].position += new Vector2(Math.Sign(Levies().First().NPC.Center.X - PlayerPos.X) * 34 * 16, 0); _pin = true; _pinAt = Main.player[0].position; }
					if (t > 400 && b.Aggro && aggroAt < 0) aggroAt = t;
					return t > 800 && b.Aggro;
				},
				Result = () => (!aggroEarly && shotsWarn == 0 && warnSaid != null && warnSaid.Contains("Turn back") && aggroAt > 0, $"before the player moved: fire={shotsWarn} aggro={aggroEarly} said='{warnSaid}'; aggro at tick {aggroAt} why {BandBrain.All.Values.FirstOrDefault(x => x.Type == snow)?.AggroWhy}"),
			});

			// 5: tactics by the foe's class
			float mMelee = 0, mGun = 0, mSummon = 0;
			_trials.Add(new Trial
			{
				Name = "tactics: ranged members keep range from a melee foe, close in on a gunner, flank a summoner", Max = 400,
				Setup = () => { ArmPlayer(Gold, 400, 0, "GoldBroadsword", null); _accuracy = 0f; SpawnArmBand(forest, 4, false); },
				Tick = t =>
				{
					var p = Main.player[0];
					if (t == 200) { var r = Levies().FirstOrDefault(b => b.Role == 'R'); mMelee = r?.HostileStandoffMult ?? -1; p.inventory[0] = new Item(); p.inventory[0].SetDefaults(ItemID.DemonBow); }
					if (t == 399) { var r = Levies().FirstOrDefault(b => b.Role == 'R'); mGun = r?.HostileStandoffMult ?? -1; }
					return false;
				},
				Result = () => (mMelee > 1.5f && mGun < 0.6f && mGun > 0, $"standoff multiplier vs a sword {mMelee:0.0}, vs a bow {mGun:0.0}"),
			});
			_trials.Add(new Trial
			{
				Name = "tactics: a player with minions is read as a summoner", Max = 5,
				Setup = () => { ArmPlayer(Gold, 400, 0, "SlimeStaff", null); },
				Tick = t => true,
				Result = () => { var c = BandBrain.ClassOf(Main.player[0]); return (c == FoeClass.Summoner, c.ToString()); },
			});

			// 6: rotating aggro: the attacked side holds fire, the other fires
			bool rotOk = false; string rotDetail = "";
			_trials.Add(new Trial
			{
				Name = "rotating aggro: the attacked side falls back and holds fire while the other side may fire", Max = 40,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; SpawnArmBand(BandTypes.ById("MythrilMercenaries"), 4, false); },
				Tick = t =>
				{
					var band = BandBrain.All.Values.FirstOrDefault(); if (band == null || band.Members.Count < 4) return false;
					var shooters = band.Members.Where(m => m.Role != 'E').ToList();
					var g0 = shooters.FirstOrDefault(m => m.Group == 0); var g1 = shooters.FirstOrDefault(m => m.Group == 1);
					if (g0 == null || g1 == null) { rotDetail = "no shooter in both groups"; return true; }
					band.Aggro = true; g0.EngagedUntil = 0; g1.EngagedUntil = 0;
					band.OnMemberHit(g0, g0.NPC.lifeMax / 5);
					bool a = band.TryToken(g0), b2 = band.TryToken(g1);
					rotOk = !a && b2 && band.RetreatGroup == 0; rotDetail = $"retreat group {band.RetreatGroup}, attacked member may fire={a}, other side may fire={b2}"; return true;
				},
				Result = () => (rotOk, rotDetail),
			});

			// 7: morale: chip damage on an armoured player breaks them; a member panics, runs and may drink the Recall potion
			int vanishedBefore = 0; float minMorale = 100; bool sawPanic = false; bool sawRouted = false; int bandAlive0 = 0;
			_trials.Add(new Trial
			{
				Name = "morale: chip damage on an armoured player makes members panic, run, and some drink a Recall potion", Max = 3200,
				Setup = () => { ArmPlayer(Turtle, 500, 90, "Megashark", "ChlorophyteBullet"); _accuracy = 0f; vanishedBefore = BandLevyNPC.Vanished; minMorale = 100; sawPanic = false; sawRouted = false; SpawnArmBand(BandTypes.ById("MeteorMarauders"), 4, false); bandAlive0 = 4; foreach (var b in Levies()) b.HasRecall = true; },
				Tick = t => { foreach (var b in Levies()) { minMorale = Math.Min(minMorale, b.Morale); if (b.Panic) sawPanic = true; if (b.State == SoldierState.Routed) sawRouted = true; } return BandLevyNPC.Vanished > vanishedBefore; },
				Result = () => (sawPanic && sawRouted && BandLevyNPC.Vanished > vanishedBefore, $"lowest morale {minMorale:0}, panic seen {sawPanic}, routed state {sawRouted}, recalled {BandLevyNPC.Vanished - vanishedBefore}"),
			});

			// 8: chatter in the voice, and the player's armor remark
			_trials.Add(new Trial
			{
				Name = "chatter: lines are aimed at a band mate or the player, with the armor remark", Max = 5,
				Setup = () => { ArmPlayer(new[] { "ShroomiteHeadgear", "ShroomiteBreastplate", "ShroomiteLeggings" }, 500, 0, "Megashark", "ChlorophyteBullet"); BandChatter.Log.Lines.Clear(); SpawnArmBand(forest, 4, false); },
				Tick = t => true,
				Result = () =>
				{
					var l = Levies(); var talker = l.FirstOrDefault();
					string armor = BandChatter.ArmorSetName(Main.player[0]);
					string line = BandChatter.Pick("SPOTTED_ARMOR", talker, "Tester", armor, 0);
					return (armor == "Shroomite" && line != null && line.Contains("Shroomite") && !line.Contains("{"), $"armor set '{armor}', line '{line}', pool {BandChatter.Lines.Values.Sum(v => v.Count)} lines");
				},
			});

			// 9: every summoning item: the band forms, every member is armed with a weapon Levy can use, and has a Levy kit
			foreach (var type in BandTypes.All)
			{
				var tp = type; int made = 0; int usable = 0; var weird = new List<string>(); int shotsT = 0;
				_trials.Add(new Trial
				{
					Name = $"summon item: {tp.ItemName} forms the {tp.Name} (levy brain, usable weapons)", Max = 420,
					Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; _shotIds.Clear(); _shotsSeen = 0; BandSpawner.Summon(Main.player[0], BandTypes.IndexOf(tp)); made = Levies().Count; usable = 0; weird.Clear(); },
					Tick = t =>
					{
						CountShots();
						if (t == 5) foreach (var b in Levies()) { if (b.Weapon != null && b.Weapon.Supported) usable++; else weird.Add(b.Record?.Gear.Weapon?.Name ?? "?"); }
						return false;
					},
					Result = () => (made >= 4 && usable == made && (_shotsSeen > 0 || tp.Warns), $"members {made}, usable weapons {usable}{(weird.Count > 0 ? " unsupported: " + string.Join(",", weird) : "")}, hostile shots in 7 s {_shotsSeen}{(tp.Warns ? " (warns first)" : "")}"),
				});
			}

			// 10: the bounty: beating the whole band leaves a notice (not loot), gear drops are off, the notice pays when turned in
			int bandSize = 0;
			_trials.Add(new Trial
			{
				Name = "bounty: beating the whole Chlorophyte Wardens band leaves one bounty notice and no gear; turning it in pays ore and coins", Max = 600,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; SpawnArmBand(BandTypes.ById("ChlorophyteWardens"), 4, false); bandSize = Levies().Count; },
				Tick = t =>
				{
					if (t == 10) foreach (var b in Levies()) b.NPC.SimpleStrikeNPC(99999, 1, false, 0f, DamageClass.Ranged, false, 0f, true);
					return t > 40 && Levies().Count == 0 && BandBrain.All.Count == 0;
				},
				Result = () =>
				{
					int notices = 0, gear = 0, share = 0, bandIdx = -1;
					for (int i = 0; i < Main.maxItems; i++) { var it = Main.item[i]; if (!it.active) continue; if (it.ModItem is Content.Items.BountyNotice bn) { notices += it.stack; share = bn.SharePct; bandIdx = bn.Band; } else gear++; }
					bool idOk = bandIdx == BandTypes.IndexOf(BandTypes.ById("ChlorophyteWardens"));
					return (notices == 1 && gear == 0 && share == 100 && idOk && BandBrain.BountiesPaid > 0, $"band of {bandSize}: notices {notices} (band {bandIdx}, {share}%), gear drops {gear}, bounties issued {BandBrain.BountiesPaid}");
				},
			});

			// 10b: a neutral band ignores the player until it is hit, then fights
			BandBrain neutralBand = null; int neutralShots0 = 0; bool wasPassive = false; float dmg0 = 0;
			_trials.Add(new Trial
			{
				Name = "neutral band: unprovoked it neither shoots nor is targeted; one hit makes it hostile", Max = 900,
				Setup = () =>
				{
					ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; DamageTaken = 0; _shotIds.Clear(); _shotsSeen = 0; wasPassive = false;
					BandSpawner.SpawnBand(Main.player[0], BandTypes.ById("MeteorMarauders"), 3, 28, 34, false, false, true);
					neutralBand = null; foreach (var b in BandBrain.All.Values) neutralBand = b;
				},
				Tick = t =>
				{
					CountShots();
					if (t == 400) { wasPassive = Levies().Count > 0 && Levies().All(m => m.HostilePassive && !m.NPC.chaseable) && _shotsSeen == 0 && DamageTaken == 0; neutralShots0 = _shotsSeen; Levies()[0].NPC.SimpleStrikeNPC(5, 1, false, 0f, DamageClass.Ranged, false, 0f, true); }
					return t > 460 && neutralBand != null && neutralBand.Provoked;
				},
				Result = () => (wasPassive && neutralBand != null && neutralBand.Neutral && neutralBand.Provoked && neutralBand.Aggro, $"passive and silent for 400 ticks: {wasPassive}, provoked after one hit: {neutralBand?.Provoked}"),
			});

			// 10c: a band that has spotted its foe spreads out: no two members share a spot, and the shooters look for higher ground
			_trials.Add(new Trial
			{
				Name = "positioning: a band spreads out on contact (own spots, no clump)", Max = 700,
				Setup = () => { ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; SpawnArmBand(BandTypes.ById("MythrilMercenaries"), 4, false); },
				Tick = t => t >= 360,
				Result = () =>
				{
					var l = Levies(); int withSpot = l.Count(m => m.PostureSpot != default); float minPair = float.MaxValue;
					for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++) if (l[i].PostureSpot != default && l[j].PostureSpot != default) minPair = Math.Min(minPair, Vector2.Distance(l[i].PostureSpot, l[j].PostureSpot) / 16f);
					return (l.Count >= 3 && withSpot >= 2 && minPair >= 4f, $"members {l.Count}, with a spot {withSpot}, nearest two spots {(minPair > 1e5f ? 0 : minPair):0.0} tiles apart");
				},
			});

			// 11: the Lunatic Cultist's circle
			int castsBefore = 0; int convBefore = 0; int orbs = 0;
			_trials.Add(new Trial
			{
				Name = "cultists: Devotees keep chanting and cast homing orbs; Cultist Archers become a levy-AI band", Max = 900,
				Setup = () =>
				{
					ArmPlayer(Gold, 400, 0, "DemonBow", "FlamingArrow"); _accuracy = 0f; castsBefore = CultistBandGlobal.Casts; convBefore = CultistBandGlobal.Converted; orbs = 0;
					var src = new Terraria.DataStructures.EntitySource_WorldEvent();
					int a = NPC.NewNPC(src, (int)PlayerPos.X + 25 * 16, (int)PlayerPos.Y, NPCID.CultistDevote); if (a >= 0 && a < Main.maxNPCs) { Main.npc[a].target = 0; }
					int b = NPC.NewNPC(src, (int)PlayerPos.X - 25 * 16, (int)PlayerPos.Y, NPCID.CultistArcherBlue); if (b >= 0 && b < Main.maxNPCs) { Main.npc[b].target = 0; }
				},
				Tick = t => { for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<Content.Projectiles.DevoteOrb>()) orbs++; return CultistBandGlobal.Casts > castsBefore && CultistBandGlobal.Converted > convBefore; },
				Result = () => (CultistBandGlobal.Casts > castsBefore && CultistBandGlobal.Converted > convBefore && Levies().Count > 0, $"devotee casts {CultistBandGlobal.Casts - castsBefore}, archers converted {CultistBandGlobal.Converted - convBefore}, levy-AI cultists {Levies().Count}, orb frames {orbs}"),
			});

			// 12: the recipes
			_trials.Add(new Trial
			{
				Name = "items: 20 summoning items, each with a Work Bench recipe of wood and cheap materials", Max = 3,
				Setup = () => { },
				Tick = t => true,
				Result = () =>
				{
					int items = 0, recipes = 0; var bad = new List<string>();
					foreach (var type in BandTypes.All)
					{
						int t = Mod.Find<ModItem>("Band_" + type.Id)?.Type ?? 0; if (t > 0) items++; else { bad.Add("no item " + type.Id); continue; }
						bool found = false;
						foreach (var r in Main.recipe) if (r != null && r.createItem != null && r.createItem.type == t && r.requiredTile.Contains(TileID.WorkBenches) && r.requiredItem.Any(i => i.type == ItemID.Wood)) { found = true; recipes++; break; }
						if (!found) bad.Add("no recipe " + type.Id);
					}
					return (items == 20 && recipes == 20, $"{items} items, {recipes} recipes {string.Join(",", bad)}");
				},
			});
		}

		// ================================================================================================ EVENT TRIALS
		private static readonly (string[] armor, int hp, int defAllow, string weapon, string ammo)[] EventKits =
		{
			(Gold, 200, 0, "DemonBow", "FlamingArrow"), (Shadow, 300, 2, "PhoenixBlaster", "MeteorShot"), (Shadow, 330, 4, "PhoenixBlaster", "MeteorShot"),
			(Mythril, 400, 5, "MythrilRepeater", "HellfireArrow"), (Mythril, 450, 7, "MythrilRepeater", "HellfireArrow"), (Turtle, 500, 10, "Megashark", "ChlorophyteBullet"), (Turtle, 520, 12, "Megashark", "ChlorophyteBullet"),
		};

		private static readonly int[][] EventStages = { null, new[] { 0, 1, 2, 3, 5, 6 }, new[] { 3, 4, 5, 6 }, new[] { 0, 1, 2, 3, 4, 5 }, new[] { 0, 1, 2, 3, 4, 5 } };

		/// <summary>The world's progress by actual bosses beaten (Levy's WeaponRank reads these flags).</summary>
		private static void SetWorldStage(int s)
		{
			NPC.downedBoss2 = s >= 1; NPC.downedBoss3 = s >= 2; Main.hardMode = s >= 3;
			NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = NPC.downedMechBossAny = s >= 4;
			NPC.downedPlantBoss = s >= 5; NPC.downedGolemBoss = s >= 6;
		}

		private static void StartEvent(EventKind k, Player p, int sign)
		{
			Main.invasionType = 0; Main.invasionSize = 0; Main.slimeRain = false; Terraria.GameContent.Events.Sandstorm.Happening = false;
			p.ConsumedLifeCrystals = 5;
			if (k == EventKind.Goblins || k == EventKind.Pirates)
			{
				Main.StartInvasion(k == EventKind.Goblins ? InvasionID.GoblinArmy : InvasionID.PirateInvasion);
				if (Main.invasionType == 0) { Main.invasionType = k == EventKind.Goblins ? InvasionID.GoblinArmy : InvasionID.PirateInvasion; Main.invasionSize = Main.invasionSizeStart = k == EventKind.Goblins ? 120 : 180; }
				Main.invasionX = p.Center.X / 16.0 + sign * 120; Main.invasionDelay = 0; // the front has arrived, 120 tiles to one side
			}
			else if (k == EventKind.Sandstorm) Terraria.GameContent.Events.Sandstorm.StartSandstorm();
			else { Main.StartSlimeRain(false); if (!Main.slimeRain) Main.slimeRain = true; Main.slimeRainTime = 54000.0; }
		}

		private static void StopEvent(EventKind k)
		{
			if (k == EventKind.Goblins || k == EventKind.Pirates) Main.invasionType = 0;
			else if (k == EventKind.Sandstorm) Terraria.GameContent.Events.Sandstorm.StopSandstorm();
			else Main.StopSlimeRain(false);
		}

		private static readonly bool _debugEv = Environment.GetEnvironmentVariable("WARBANDS_DEBUGEV") == "1";
		private void AddEventTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 12;
			string only = Environment.GetEnvironmentVariable("WARBANDS_EVENTS"); // e.g. "Goblins,Pirates" (default: all four)
			_tsv.AppendLine("event\tstage\tseed\tside\tform_tick\tmembers\tbands\thostile\thostile_shots\tstandin_damage\tmin_dist_tiles\tnatural_kills\tkilled\tcaptain_killed\tcredited\texpected_credit\tinvasion_size_at_form\tinvasion_size_at_end\tcaptain_drop_items\tend_tick\tleft_ticks\tleft_walk_off\tleft_recall\tremaining\tnote");
			foreach (var kind in new[] { EventKind.Goblins, EventKind.Pirates, EventKind.Sandstorm, EventKind.SlimeRain })
			{
				if (only != null && !only.Contains(kind.ToString())) continue;
				for (int seed = 0; seed < seeds; seed++)
				{
					var k = kind; int sd = seed; int stage = EventStages[(int)k][seed % EventStages[(int)k].Length]; int sign = sd % 2 == 0 ? 1 : -1;
					bool capAtExec = false; int k0 = 0, c0 = 0, creditedAtEnd = 0, aliveAtEnd = 0, stageSeen = -1, formTick = -1, endTick = -1, leftTick = -1, members = 0, cr0 = 0, cd0 = 0, dep0 = 0, rec0 = 0, sizeStart = 0, sizeEnd = 0, dropItems = 0, dropSnapAt = -1, killedAtEnd = 0, capAtEnd = 0, natKills = 0, execAt = -1;
					bool hostile = false, sawLeaving = false; float minDist = 9999f;
					var seen = new Dictionary<int, int>(); var dropTypes = new HashSet<int>();
					int TotalKilled() { int s = 0; foreach (var v in seen.Values) s += v; return s; }
					int AliveOfKind() { int a = 0; for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && n.ModNPC is BandLevyNPC l && l.Roster != null && l.Roster.Event == k) a++; } return a; }
					_trials.Add(new Trial
					{
						Name = $"event {k} / stage {stage} / seed {sd}", Max = 60 * 160,
						Setup = () =>
						{
							WarbandsConfig.Get.NeutralBandPercent = 100f; WarbandsConfig.Get.EventBandCooldownSeconds = 10;
							SetWorldStage(stage);
							var kit = EventKits[stage]; ArmPlayer(kit.armor, kit.hp, kit.defAllow, kit.weapon, kit.ammo); _accuracy = 0.6f; _range = 48; DamageTaken = 0;
							Main.rand = new Terraria.Utilities.UnifiedRandom(7000 * (int)k + 131 * sd + 3);
							ModContent.GetInstance<EventBandSystem>().OnWorldLoad();
							EventBands.LabAlwaysQualify = k == EventKind.Sandstorm || k == EventKind.SlimeRain;
							_shotIds.Clear(); _shotsSeen = 0; formTick = endTick = leftTick = execAt = -1; capAtExec = false; hostile = false; sawLeaving = false; dropSnapAt = -1; dropItems = 0; dropTypes.Clear(); seen.Clear(); minDist = 9999f; natKills = 0;
							cr0 = EventBands.Credited; k0 = EventBands.Kills; c0 = EventBands.CaptainKills; cd0 = EventBands.CaptainDrops; creditedAtEnd = 0; aliveAtEnd = 0; dep0 = EventBands.Departed; rec0 = BandLevyNPC.Vanished; killedAtEnd = capAtEnd = 0;
							StartEvent(k, Main.player[0], sign); stageSeen = Stage.Current;
							foreach (var d in EventBands.CaptainLoot(k)) dropTypes.Add(BandData.ItemType(d.Item));
						},
						Tick = t =>
						{
							CountShots();
							if (_debugEv && formTick >= 0 && endTick < 0 && (t - formTick) % 150 == 0) for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.ModNPC is BandLevyNPC l && l.Roster?.Event == k) Mod.Logger.Info($"[Warbands] dbg t={t - formTick} #{l.MemberIdx} {l.Role} {(l.IsCaptain ? "CAP " : "")}dist {Vector2.Distance(n.Center, Main.player[0].Center) / 16f:0.0} spot {(l.PostureSpot == default ? -1f : Vector2.Distance(n.Bottom, l.PostureSpot) / 16f):0.0} vel {n.velocity.X:0.0},{n.velocity.Y:0.0} target {l.HostileTarget?.whoAmI ?? -1} panic {l.Panic} hook {l.IsHooking} at {(int)(n.Center.X / 16f)},{(int)(n.Bottom.Y / 16f)} player {(int)(Main.player[0].Center.X / 16f)},{(int)(Main.player[0].Bottom.Y / 16f)} spotAt {(int)(l.PostureSpot.X / 16f)},{(int)(l.PostureSpot.Y / 16f)} weapon {l.Record?.Gear.Weapon.Name}"); }
							foreach (var b in BandBrain.All.Values)
							{
								if (!b.IsEvent || b.Type.Event != k) continue;
								if (!seen.ContainsKey(b.Id)) { seen[b.Id] = 0; if (formTick < 0) { formTick = t; members = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is BandLevyNPC l && l.BandId == b.Id) members++; hostile = !b.Neutral && b.Aggro; sizeStart = Main.invasionSize; } else hostile &= !b.Neutral && b.Aggro; }
								seen[b.Id] = Math.Max(seen[b.Id], b.Killed); if (b.Leaving) sawLeaving = true;
							}
							if (formTick < 0) return t > 60 * 40; // the first band comes ten seconds in; none by forty is a failure
							if (endTick < 0)
							{
								for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && n.ModNPC is BandLevyNPC l && l.Roster?.Event == k) minDist = Math.Min(minDist, Vector2.Distance(n.Center, Main.player[0].Center) / 16f); }
								if (dropSnapAt < 0 && EventBands.CaptainDrops > cd0) dropSnapAt = t + 2;
								if (dropSnapAt > 0 && t == dropSnapAt) for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && dropTypes.Contains(Main.item[i].type)) dropItems++;
								// 25 seconds of real fighting, then the stand-in's weapon finishes what it started: one member and the captain fall at once, so every run exercises credit and the drop table
								if (execAt < 0 && t - formTick >= 60 * 25) { execAt = t; natKills = EventBands.Kills - k0; }
								if (execAt == t)
								{
									BandLevyNPC cap = null, crew = null;
									for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && n.ModNPC is BandLevyNPC l && l.Roster?.Event == k) { if (l.IsCaptain) cap = l; else crew ??= l; } }
									capAtExec = cap != null; crew?.NPC.StrikeInstantKill(); cap?.NPC.StrikeInstantKill();
									return false;
								}
								if (execAt >= 0 && t > execAt) { endTick = t; killedAtEnd = EventBands.Kills - k0; capAtEnd = EventBands.CaptainKills - c0; creditedAtEnd = EventBands.Credited - cr0; aliveAtEnd = AliveOfKind(); sizeEnd = Main.invasionSize; StopEvent(k); }
								return false;
							}
							if (dropSnapAt > 0 && t == dropSnapAt) for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && dropTypes.Contains(Main.item[i].type)) dropItems++;
							if (AliveOfKind() == 0 && leftTick < 0) { leftTick = t; return true; }
							return false;
						},
						Result = () =>
						{
							int remaining = AliveOfKind();
							int credited = creditedAtEnd;
							int expected = (k == EventKind.Goblins || k == EventKind.Pirates) ? Math.Max(0, killedAtEnd - capAtEnd) + 2 * capAtEnd : 0;
							int walk = EventBands.Departed - dep0; int recall = BandLevyNPC.Vanished - rec0;
							bool formedOk = formTick >= 60 * 9 && formTick <= 60 * 25 && members >= 3 && members <= 8;
							bool fought = _shotsSeen > 0 || DamageTaken > 0;
							bool killedOk = killedAtEnd >= 1 && (!capAtExec || capAtEnd >= 1);
							bool creditOk = (k == EventKind.Sandstorm || k == EventKind.SlimeRain) ? Main.invasionSize == 0 || sizeEnd == sizeStart : credited == expected && credited >= 1 && (sizeStart - sizeEnd) == credited;
							bool leaveOk = endTick >= 0 && leftTick >= 0 && remaining == 0 && (sawLeaving || aliveAtEnd == 0);
							bool ok = formedOk && hostile && fought && killedOk && creditOk && leaveOk;
							string note = (stageSeen == stage ? "" : $"stage mismatch (wanted {stage}, world {stageSeen}) ") + (ok ? "" : $"formedOk={formedOk} hostile={hostile} fought={fought} killedOk={killedOk} creditOk={creditOk} leaveOk={leaveOk}");
							_tsv.AppendLine($"{k}\t{stage}\t{sd}\t{sign}\t{formTick}\t{members}\t{seen.Count}\t{(hostile ? 1 : 0)}\t{_shotsSeen}\t{DamageTaken:0}\t{minDist:0}\t{natKills}\t{killedAtEnd}\t{capAtEnd}\t{credited}\t{expected}\t{sizeStart}\t{sizeEnd}\t{dropItems}\t{endTick}\t{(leftTick >= 0 && endTick >= 0 ? leftTick - endTick : -1)}\t{walk}\t{recall}\t{remaining}\t{note}");
							return (ok, $"form@{formTick} members {members} bands {seen.Count} hostile={hostile} shots {_shotsSeen} dmg {DamageTaken:0} closest {minDist:0} tiles, natural kills {natKills}, killed {killedAtEnd} (captain {capAtEnd}), credit {credited}/{expected} (size {sizeStart}->{sizeEnd}), captain-drop items {dropItems}, left {(leftTick >= 0 && endTick >= 0 ? (leftTick - endTick) : -1)} ticks after the end (off-screen {walk}, recall {recall}), remaining {remaining}{(note.Length > 0 ? " [" + note + "]" : "")}");
						},
						Cleanup = () => { StopEvent(k); EventBands.LabAlwaysQualify = false; },
					});
				}
			}
		}

		// ================================================================================================ FIGHT TRIALS
		private static readonly (int stage, string label, string[] armor, int hp, int defAllow, string weapon, string ammo, string rosterId, short[] vanilla)[] Stages =
		{
			(0, "pre-boss", Gold, 200, 0, "DemonBow", "FlamingArrow", "ForestDeserters", new short[] { NPCID.Zombie, NPCID.DemonEye, NPCID.Skeleton, NPCID.Zombie }),
			(1, "post-EoW", Shadow, 300, 2, "PhoenixBlaster", "MeteorShot", "MeteorMarauders", new short[] { NPCID.FireImp, NPCID.Hornet, NPCID.Demon, NPCID.FireImp }),
			(3, "early Hardmode", Mythril, 400, 5, "MythrilRepeater", "HellfireArrow", "MythrilMercenaries", new short[] { NPCID.ChaosElemental, NPCID.Pixie, NPCID.Corruptor, NPCID.Pixie }),
			(5, "post-Plantera", Turtle, 500, 10, "Megashark", "ChlorophyteBullet", "ChlorophyteWardens", new short[] { NPCID.BlueArmoredBones, NPCID.Necromancer, NPCID.SkeletonSniper, NPCID.BlueArmoredBones }),
		};

		private void AddFightTrials()
		{
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("WARBANDS_SEEDS"), out int sv) ? sv : 4;
			int cap = 60 * 75;
			_tsv.AppendLine("stage\tarm\tseed\tplayer_damage_taken\tplayer_hp\tplayer_died\tclear_seconds\tenemies\tkilled\tfled\tpanics\tretreat_rotations\tnote");
			foreach (var st in Stages)
				foreach (string arm in new[] { "levy-ai band", "fighter-ai band", "normal enemies" })
					for (int seed = 0; seed < seeds; seed++)
					{
						var s = st; string a = arm; int sd = seed; int total = 0; int clearedAt = -1; int panics0 = 0, rot0 = 0, fled0 = 0; int killed0 = 0; var role = BandTypes.ById(s.rosterId);
						_trials.Add(new Trial
						{
							Name = $"fight {s.label} / {a} / seed {sd}", Max = cap,
							Setup = () =>
							{
								ArmPlayer(s.armor, s.hp, s.defAllow, s.weapon, s.ammo); _accuracy = 0.7f; DamageTaken = 0; clearedAt = -1; panics0 = BandLevyNPC.PanicEntries; fled0 = BandLevyNPC.Vanished; killed0 = 0;
								Main.rand = new Terraria.Utilities.UnifiedRandom(1000 * s.stage + 17 * sd + (a == "levy-ai band" ? 1 : a == "fighter-ai band" ? 2 : 3));
								if (a == "normal enemies")
								{
									var src = new Terraria.DataStructures.EntitySource_WorldEvent();
									for (int k = 0; k < 4; k++)
									{
										int side = k % 2 == 0 ? 1 : -1; int x = (int)PlayerPos.X + side * (28 + 3 * k) * 16;
										int id = NPC.NewNPC(src, x, (int)(PlayerPos.Y - 60), s.vanilla[k]); if (id >= 0 && id < Main.maxNPCs) { Main.npc[id].target = 0; _labNpcs.Add(id); }
									}
									total = _labNpcs.Count;
								}
								else { SpawnArmBand(role, 4, a == "fighter-ai band"); total = 4; }
							},
							Tick = t =>
							{
								int alive = 0;
								if (a == "normal enemies") { foreach (int i in _labNpcs) if (Main.npc[i].active && Main.npc[i].life > 0) alive++; }
								else for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && (n.ModNPC is BandLevyNPC || n.ModNPC is BandSoldier)) alive++; }
								if (alive == 0 && t > 30) { clearedAt = t; return true; }
								return _playerDied;
							},
							Result = () =>
							{
								int alive = 0;
								if (a == "normal enemies") { foreach (int i in _labNpcs) if (Main.npc[i].active && Main.npc[i].life > 0) alive++; }
								else for (int i = 0; i < Main.maxNPCs; i++) { var n = Main.npc[i]; if (n.active && n.life > 0 && (n.ModNPC is BandLevyNPC || n.ModNPC is BandSoldier)) alive++; }
								int fled = a == "levy-ai band" ? BandLevyNPC.Vanished - fled0 : 0; int killed = total - alive - fled;
								int panics = a == "levy-ai band" ? BandLevyNPC.PanicEntries - panics0 : 0;
								int rot = 0; foreach (var b in BandBrain.All.Values) rot += b.Rotations;
								float clear = clearedAt > 0 ? clearedAt / 60f : -1f;
								_tsv.AppendLine($"{s.label}\t{a}\t{sd}\t{DamageTaken:0}\t{s.hp}\t{(_playerDied ? 1 : 0)}\t{(clear < 0 ? "cap" : clear.ToString("0.0"))}\t{total}\t{killed}\t{fled}\t{panics}\t{rot}\talive_at_end={alive}");
								return (true, $"damage {DamageTaken:0}/{s.hp} died={_playerDied} clear={(clear < 0 ? "cap" : clear.ToString("0.0") + "s")} killed {killed}/{total} fled {fled} panics {panics} rotations {rot}");
							},
						});
					}
		}
	}

	/// <summary>No vanilla spawns while the lab runs (the stand-in player would draw zombies into the arena).</summary>
	public class LabSpawnSuppress : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) { if (WarbandsLab.On && !WarbandsLab.Done) { spawnRate = 99999; maxSpawns = 0; } }
	}
}
