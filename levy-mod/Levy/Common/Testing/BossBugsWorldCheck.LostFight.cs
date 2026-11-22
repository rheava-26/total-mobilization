using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LOST FIGHT world check, logged as "[LostFight]" lines (Eve, 2026-11-21: "If you die during a boss fight, but levies are around and you've beaten the boss before, they should target the levies").
	/// Runs in the world-check chain right after BossBugs (LEVY_LOSTFIGHTCHECK=0 skips it, LEVY_LOSTFIGHTCHECK_ONLY=1 runs it alone). For a spread of vanilla and Calamity bosses, each beaten before, an armed army of 8 levies
	/// stands with the commander (Follow, 10 to 40 tiles off) or holds 120 tiles away; the boss fights for 4 s, the commander dies and stays dead, and the boss is watched: does it keep fighting the levies, for how long, does it
	/// leave, and what made it leave (EncourageDespawn and its caller, timeLeft run out in CheckActive, deactivated by its own AI, its target state at that tick). Further arms: a first fight (the boss must leave), the army
	/// wiped mid lost fight (the boss must leave) and the commander respawning (the boss fights on). Providence and the Devourer of Gods fight in an enormous open arena: a long asphalt floor, three platform rows above, open sky.
	/// LEVY_LF_BOSS (comma list of name substrings), LEVY_LF_ARMS (follow,hold,unbeaten,wipe,respawn), LEVY_LF_WATCH (ticks watched after the death) pick a lab run; LEVY_LF_TRACE=1 logs a per-second trace.
	/// </summary>
	public partial class BossBugsWorldCheck
	{
		private static bool _lfDone; private static int _lfPass, _lfFail;
		private static bool _lfDay; private static Action<Player> _lfZones;
		private const int LfBarracksId = 900131;
		private static int _lfHomeTiles = 260;

		private static void LfCheck(string name, bool ok, Func<string> why = null) { if (ok) _lfPass++; else _lfFail++; _mod.Logger.Info($"[LostFight] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }
		private static void LfNote(string s) => _mod.Logger.Info("[LostFight] OBSERVED " + s);

		/// <summary>Runs the lost-fight check now when this boot armed the world checks.</summary>
		public static void LostFightRunIfArmed()
		{
			if (!_armed || _lfDone || !Main.dedServ || Environment.GetEnvironmentVariable("LEVY_LOSTFIGHTCHECK") == "0") return;
			_lfDone = true; _running = true; _lfPass = _lfFail = 0;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Main.player[0] = Main.player[0] ?? new Player(); LostFightRun(); }
			catch (Exception e) { LfCheck("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				_local = false; _running = false; _lfDay = false; _lfZones = null; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server;
				if (TileEntity.ByID.TryGetValue(LfBarracksId, out var bar)) TestTileEntities.Remove(bar);
				Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time;
			}
			_mod.Logger.Info($"[LostFight] self-check pass={_lfPass} fail={_lfFail}");
		}

		private sealed class LfBoss
		{
			public string Name, Cal; public int Vanilla = -1; public string Arena = "surface"; public bool Day, Worm, Hard = true; public Action<Player> Zones;
			public int Type => Cal != null ? CalType(Cal) : Vanilla;
		}

		private sealed class LfResult
		{
			public int Gone = -1, Locked, Watched, MinTl = int.MaxValue, EncourageCalls, TlSlashAt = -1, LockLostAt = -1, NearAtDeath = -1; public bool ByCheckActive, ByAi, DeadAtDeathTarget;
			public float DriftTiles; public int HomeAtEnd;
			public string FirstEncourage = "", AtGone = "", AtSlash = "", AtLockLost = "", TargetAtDeath = "", Trace = "", Rise = "";
		}

		private static List<LfBoss> LfBosses()
		{
			bool cal = ModLoader.HasMod("CalamityMod");
			var l = new List<LfBoss>
			{
				new() { Name = "Eye of Cthulhu", Vanilla = NPCID.EyeofCthulhu, Hard = false },
				new() { Name = "Skeletron Prime", Vanilla = NPCID.SkeletronPrime },
				new() { Name = "The Destroyer", Vanilla = NPCID.TheDestroyer, Worm = true },
				new() { Name = "Plantera", Vanilla = NPCID.Plantera, Arena = "jungle", Zones = p => p.ZoneJungle = true },
				new() { Name = "Golem", Vanilla = NPCID.Golem, Zones = p => { p.ZoneJungle = true; p.ZoneLihzhardTemple = true; } },
				new() { Name = "Duke Fishron", Vanilla = NPCID.DukeFishron, Zones = p => p.ZoneBeach = true },
				new() { Name = "Empress of Light", Vanilla = NPCID.HallowBoss, Zones = p => p.ZoneHallow = true },
				new() { Name = "Moon Lord", Vanilla = NPCID.MoonLordCore, Arena = "big" },
			};
			if (cal) l.AddRange(new LfBoss[]
			{
				new() { Name = "Desert Scourge", Cal = "DesertScourgeHead", Worm = true, Hard = false, Zones = p => p.ZoneDesert = true },
				new() { Name = "Hive Mind", Cal = "HiveMind", Hard = false, Zones = p => p.ZoneCorrupt = true },
				new() { Name = "Calamitas Clone", Cal = "CalamitasClone" },
				new() { Name = "Plaguebringer Goliath", Cal = "PlaguebringerGoliath", Zones = p => p.ZoneJungle = true },
				new() { Name = "Ravager", Cal = "RavagerBody" },
				new() { Name = "Providence (Underworld)", Cal = "Providence", Arena = "bigunderworld", Day = true, Zones = p => { p.ZoneUnderworldHeight = true; p.ZoneOverworldHeight = false; } },
				new() { Name = "Providence (Hallow)", Cal = "Providence", Arena = "big", Day = true, Zones = p => p.ZoneHallow = true },
				new() { Name = "Storm Weaver", Cal = "StormWeaverHead", Worm = true, Arena = "big" },
				new() { Name = "Polterghast", Cal = "Polterghast", Zones = p => p.ZoneDungeon = true },
				new() { Name = "The Devourer of Gods", Cal = "DevourerofGodsHead", Worm = true, Arena = "big" },
				new() { Name = "Yharon", Cal = "Yharon", Arena = "big" },
			});
			return l;
		}

		private static string _lfArenaKind;
		/// <summary>The arena a boss is fought in. "surface": the standard flat arena. "big": an enormous open arena (a 1200-tile asphalt floor, three platform rows above it, 200 tiles of open sky). "bigunderworld": the same in the Underworld.</summary>
		private static void LfArena(string kind)
		{
			if (_lfArenaKind == kind) return;
			_lfArenaKind = kind;
			if (kind == "surface") { Arena(FloorY, Half); return; }
			if (kind == "jungle")
			{
				// Plantera's hooks grab tiles and walls near her target: a jungle cave, the standard arena with mud walls behind it and a mud ceiling 60 tiles up
				Arena(FloorY, Half);
				for (int i = Cx - Half; i <= Cx + Half; i++)
				{
					for (int j = FloorY - 120; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.WallType = WallID.MudUnsafe; }
					for (int j = FloorY - 64; j < FloorY - 60; j++) { Tile c = Main.tile[i, j]; c.HasTile = true; c.TileType = TileID.Mud; c.BlockType = BlockType.Solid; }
				}
				return;
			}
			int fy = kind == "bigunderworld" ? Math.Max(Main.UnderworldLayer + 60, Main.maxTilesY - 125) : FloorY;
			int half = Math.Min(600, Cx - 60), top = Math.Max(10, fy - 200);
			for (int i = Cx - half; i <= Cx + half; i++)
			{
				for (int j = top; j < fy; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = fy; j < fy + 4; j++) { Tile f = Main.tile[i, j]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Asphalt; f.BlockType = BlockType.Solid; }
				foreach (int row in new[] { 22, 44, 66 })
				{
					if ((i - Cx + half) % 40 >= 30) continue; // gaps every 30 tiles so nothing walks a whole row
					Tile pf = Main.tile[i, fy - row]; pf.HasTile = true; pf.TileType = TileID.Platforms; pf.TileFrameY = 0; pf.BlockType = BlockType.Solid;
				}
			}
			_floorY = fy;
		}

		private static SoldierNPC LfLevy(Vector2 at, SoldierOrder order)
		{
			var s = Levy(at, order, 4000);
			if (s?.Record == null) return s;
			var gun = new Item(); gun.SetDefaults(ItemID.Minishark); var ball = new Item(); ball.SetDefaults(ItemID.MusketBall); ball.stack = 999; s.Record.Gear.Weapon = gun; s.Record.Gear.Ammo = ball;
			return s;
		}

		private static void LostFightRun()
		{
			var bosses = LfBosses();
			string[] only = Environment.GetEnvironmentVariable("LEVY_LF_BOSS")?.Split(',');
			string armsEnv = Environment.GetEnvironmentVariable("LEVY_LF_ARMS");
			// the lab sweep (any LEVY_LF_* pick, or LEVY_LF_FULL=1): every chosen boss on every chosen arm. The ship run: the plan below, a few minutes a half.
			bool sweep = only != null || armsEnv != null || Environment.GetEnvironmentVariable("LEVY_LF_FULL") == "1";
			string[] arms = (armsEnv ?? "real,hold,unbeaten,wipe,respawn,wormtail").Split(',');
			bool cal = ModLoader.HasMod("CalamityMod");
			int watch = int.TryParse(Environment.GetEnvironmentVariable("LEVY_LF_WATCH"), out int w) && w > 0 ? w : sweep ? 1200 : 900;
			var plan = new List<(string boss, string arm)>();
			if (sweep)
			{
				foreach (LfBoss b in bosses)
				{
					if (only != null && Array.FindIndex(only, o => b.Name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0) < 0) continue;
					foreach (string arm in arms)
					{
						// the first-fight, wipe and respawn arms ask about the rule's exits; with no boss named a few bosses stand for the rest
						if (arm == "wormtail" && !b.Worm) continue; // the head-or-tail leash is a worm's
						if ((arm == "wipe" || arm == "respawn" || arm == "unbeaten") && only == null && !(b.Name.StartsWith("Eye") || b.Name.StartsWith("Duke") || b.Name.StartsWith("Plaguebringer") || b.Name.StartsWith("The Devourer"))) continue;
						plan.Add((b.Name, arm));
					}
				}
			}
			else if (cal) plan.AddRange(new[] { ("The Devourer of Gods", "real"), ("Providence (Hallow)", "real"), ("Yharon", "real"), ("Plaguebringer Goliath", "hold"), ("Plantera", "real"), ("The Devourer of Gods", "unbeaten"), ("Eye of Cthulhu", "wipe"), ("Plaguebringer Goliath", "respawn"), ("Desert Scourge", "wormtail"), ("The Destroyer", "wormtail") });
			else plan.AddRange(new[] { ("Eye of Cthulhu", "real"), ("The Destroyer", "real"), ("Moon Lord", "real"), ("Plantera", "real"), ("Duke Fishron", "hold"), ("Skeletron Prime", "day"), ("Eye of Cthulhu", "unbeaten"), ("Duke Fishron", "wipe"), ("Eye of Cthulhu", "respawn"), ("The Destroyer", "wormtail") });
			LfNote($"lost fight on={EnemyTargeting.LostFightOn}, rally on={EnemyTargeting.LostFightRallyOn}, leash {LevyConstants.Targeting.LostFightLeashTiles} tiles, watch {watch} ticks after the death, Calamity {(cal ? "Revengeance+Death, Master" : "off, Expert")}, {plan.Count} trials{(sweep ? " (lab sweep)" : "")}");
			_lfArenaKind = null;
			int homeTiles = int.TryParse(Environment.GetEnvironmentVariable("LEVY_LF_HOME"), out int ht) && ht > 0 ? ht : 260;
			_lfHomeTiles = homeTiles;
			var bar = TestTileEntities.Add<global::Levy.Content.TileEntities.BarracksTE>(LfBarracksId, new Point16(Cx - homeTiles, FloorY - 3)); bar.TownName = "LostFightTown"; bar.AutoMuster = false;
			foreach (var (bossName, arm) in plan)
			{
				LfBoss b = bosses.Find(x => x.Name == bossName);
				if (b == null) continue;
				int type = b.Type;
				if (type <= 0) { LfCheck($"{b.Name}: the type resolves", false); continue; }
				if (arm == "wormtail") { LfWormTrial(b, type); continue; }
				{
					LfResult r = LfTrial(b, type, arm, watch);
					string label = arm switch
					{
						"follow" => "beaten, 8 armed levies following the commander", "hold" => "beaten, 8 armed levies holding 120 tiles off", "unbeaten" => "first fight, 8 levies following",
						"day" => "beaten, 8 levies following, by day", "wipe" => "beaten, the army wiped 8 s into the lost fight", "respawn" => "beaten, the commander respawns 8 s into the lost fight", "home" => $"beaten, 8 levies following, their barracks {_lfHomeTiles} tiles off", "real" => $"beaten, 8 levies following, their barracks {_lfHomeTiles} tiles off, the real death path",
						"realmind" => "beaten, as real, the commander driving a levy", _ => arm,
					};
					string verdict = r.Gone == -2 ? "did not spawn" : r.Gone == -3 ? "left before the commander died" : r.Gone >= 0 ? $"GONE {r.Gone / 60f:0.0} s after the death ({(r.ByCheckActive ? "timeLeft ran out in CheckActive" : r.ByAi ? "deactivated in its own update" : "deactivated elsewhere")})" : $"still fighting {watch / 60} s after the death";
					LfNote($"{b.Name} [{label}]: {verdict}; locked on a levy {r.Locked}/{r.Watched} ticks; target at the death {r.TargetAtDeath} (nearest levy {r.NearAtDeath} tiles); min timeLeft {r.MinTl}; EncourageDespawn {r.EncourageCalls}{(r.FirstEncourage != "" ? " first from " + r.FirstEncourage : "")}; timeLeft slashed at {(r.TlSlashAt >= 0 ? "t" + r.TlSlashAt + " [" + r.AtSlash + "]" : "never")}; lock lost at {(r.LockLostAt >= 0 ? "t" + r.LockLostAt + " [" + r.AtLockLost + "]" : "never")}; {r.Rise}{(r.AtGone != "" ? "; at the end [" + r.AtGone + "]" : "")}");
					if (Environment.GetEnvironmentVariable("LEVY_LF_TRACE") == "1") LfNote("  trace " + r.Trace);
					if (r.Gone <= -2) { LfCheck($"{b.Name} [{label}]: the trial runs ({verdict})", false); continue; }
					switch (arm)
					{
						case "follow": case "hold": case "home": case "real": case "realmind":
							LfCheck($"{b.Name} [{label}]: keeps fighting the levies {watch / 60} s after the commander's death", r.Gone == -1 && r.Locked * 10 >= r.Watched * 7, () => verdict + $", locked {r.Locked}/{r.Watched}");
							if ((arm == "real" || arm == "home") && EnemyTargeting.LostFightRallyOn)
								LfCheck($"{b.Name} [{label}]: the army rallies where the commander fell (no levy walks home, the fight drifts under 120 tiles)", r.HomeAtEnd == 0 && Math.Abs(r.DriftTiles) < 120, () => $"walking home at the end {r.HomeAtEnd}, drift {r.DriftTiles:0} tiles");
							break;
						case "unbeaten":
							LfCheck($"{b.Name} [{label}]: leaves and despawns within {watch / 60} s (the first kill is solo)", r.Gone >= 0, () => verdict);
							break;
						case "wipe":
							LfCheck($"{b.Name} [{label}]: fights the levies until they fall, then leaves", r.Gone >= 0 && r.LockLostAt >= 0, () => verdict + $", lock lost at {r.LockLostAt}");
							break;
						case "day":
							LfCheck($"{b.Name} [{label}]: leaves by day and never fights the levies after the commander's death (the daytime rule)", r.Gone >= 0 && r.Locked == 0, () => verdict + $", locked {r.Locked}/{r.Watched}");
							break;
						case "respawn":
							LfCheck($"{b.Name} [{label}]: stays in the fight after the commander respawns", r.Gone == -1, () => verdict);
							break;
					}
				}
			}
		}

		private static LfResult LfTrial(LfBoss b, int type, string arm, int watch)
		{
			var r = new LfResult();
			ClearHostiles();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC) n.active = false; }
			RosterSystem.Instance.Regulars.RemoveAll(x => x.CommanderName == Commander);
			_cmdDead = false;
			int who = -1; bool inCheckActive = false, deactivatedInCheckActive = false, deactivatedInUpdate = false;
			On_NPC.hook_EncourageDespawn encDet = (orig, self, time) => { if (self.whoAmI == who) { r.EncourageCalls++; if (r.FirstEncourage == "") r.FirstEncourage = Short(Environment.StackTrace); } orig(self, time); };
			On_NPC.hook_CheckActive actDet = (orig, self) => { bool was = self.active && self.whoAmI == who; inCheckActive = true; try { orig(self); } finally { inCheckActive = false; } if (was && !self.active) deactivatedInCheckActive = true; };
			On_NPC.hook_UpdateNPC updDet = (orig, self, i) => { bool was = i == who && self.active; orig(self, i); if (was && !self.active && !deactivatedInCheckActive) deactivatedInUpdate = true; };
			On_NPC.EncourageDespawn += encDet; On_NPC.CheckActive += actDet; On_NPC.UpdateNPC += updDet;
			try
			{
				LfArena(b.Arena);
				bool calm = ModLoader.HasMod("CalamityMod");
				SetRevenge(calm, calm); Main.GameMode = calm ? GameModeID.Master : GameModeID.Expert; Main.hardMode = b.Hard;
				_lfDay = b.Day || arm == "day"; _lfZones = b.Zones;
				PlaceCommander(0);
				bool beaten = arm != "unbeaten";
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, beaten);
				SetBossesTarget(true);
				EnemyTargeting.RefreshConfig();
				var army = new List<SoldierNPC>();
				for (int k = 0; k < 8; k++)
				{
					Vector2 at = arm == "hold" ? new Vector2((Cx + 120 + (k - 4) * 2) * 16f, _floorY * 16f) : new Vector2((Cx + (k % 2 == 0 ? -1 : 1) * (10 + k * 4)) * 16f, _floorY * 16f);
					var s = LfLevy(at, arm == "hold" ? SoldierOrder.Hold : SoldierOrder.Follow);
					if (s == null) continue;
					if (arm == "home" || arm == "real" || arm == "realmind") s.Record.HomeBarracksId = LfBarracksId;
					s.NPC.immortal = arm != "wipe";
					army.Add(s);
				}
				Step(2);
				NPC boss;
				if (b.Worm)
				{
					int idx = NPC.NewNPC(new EntitySource_BossSpawn(Main.player[0]), (int)_cmdAt.X, (int)_cmdAt.Y - 1600, type, 1);
					boss = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
					if (boss != null) boss.timeLeft *= 20;
				}
				else boss = SpawnNpc(type, _cmdAt + new Vector2(0, -30 * 16f));
				if (boss == null || boss.type != type) { r.Gone = -2; return r; }
				who = boss.whoAmI;
				bool Alive() => Main.npc[who].active && Main.npc[who].type == type;
				void TopUp() { if (!Alive()) return; NPC n = Main.npc[who]; n.life = n.lifeMax; for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && i != who && (x.realLife == who) && x.lifeMax > 0) x.life = x.lifeMax; } }
				void Heal() { if (arm == "wipe") return; foreach (var l in army) if (l.NPC.active) { l.NPC.life = l.NPC.lifeMax; if (l.Record != null) l.Record.CurrentHp = l.NPC.lifeMax; } }
				_perTick = () => { TopUp(); Heal(); };
				var pre = new StringBuilder();
				for (int q = 0; q < 4; q++)
				{
					Step(60);
					if (!Alive()) break;
					NPC f = Main.npc[who];
					pre.Append($" | fight t{(q + 1) * 60} target {f.target} lock {LevyNpcData.Of(f).SoldierTarget} pos ({f.Center.X / 16f:0},{f.Center.Y / 16f:0}) nearest {NearestLevyTiles(army, f):0}").Append(LfParts(f));
				}
				if (!Alive()) { r.Gone = -3; return r; }
				NPC b0 = Main.npc[who];
				bool lockedAtDeath = Locked(b0);
				r.TargetAtDeath = lockedAtDeath ? "a levy" : b0.target >= 0 && b0.target < Main.maxPlayers ? "the commander (player " + b0.target + ")" : "target " + b0.target;
				float near = float.MaxValue; foreach (var l in army) if (l.NPC.active) near = Math.Min(near, Vector2.Distance(l.NPC.Center, LevyUtilRoot(b0).Center) / 16f);
				r.NearAtDeath = near == float.MaxValue ? -1 : (int)near;
				if (arm == "real" || arm == "realmind")
				{
					// the real death path: vanilla's KillMe and every mod's Kill hook (Levy's fear rout on the commander's death among them)
					Player cp = Main.player[0]; cp.active = true;
					try { cp.KillMe(PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("LostFight")), 9999, 0); } catch (Exception e) { r.TargetAtDeath += " (KillMe threw " + e.GetType().Name + ")"; }
					int routed = 0; foreach (var l in army) if (l.NPC.active && l.State == SoldierState.Routed) routed++;
					r.TargetAtDeath += $", {routed} of {army.Count} levies routed by the death";
				}
				if (arm == "realmind" && army.Count > 0)
				{
					bool begun = global::Levy.Common.Observe.MindControl.Begin(Main.player[0], army[0].Record.Id, true);
					r.TargetAtDeath += begun ? ", the commander drives a levy" : ", mind control refused";
				}
				_cmdDead = true;
				EnemyTargeting.WhyTrack = new string[Main.maxNPCs + 1]; EnemyTargeting.WhyCount = new int[Main.maxNPCs + 1];
				float startY = b0.Center.Y, minY = startY, startX = b0.Center.X, lastX = startX; var trace = new StringBuilder();
				int prevTl = b0.timeLeft; bool wasLocked = lockedAtDeath; bool everLockedAfter = false;
				for (int t = 0; t < watch; t++)
				{
					if (t == 480 && arm == "wipe") { foreach (var l in army) if (l.NPC.active) { l.NPC.immortal = false; l.NPC.life = 1; l.NPC.SimpleStrikeNPC(99999, 0, false, 0f, null, false, 0f, false); } }
					if (t == 480 && arm == "respawn") _cmdDead = false;
					Step(1);
					if (!Alive())
					{
						r.Gone = t; r.ByCheckActive = deactivatedInCheckActive; r.ByAi = deactivatedInUpdate;
						break;
					}
					NPC n = Main.npc[who]; r.Watched++;
					bool lk = Locked(n);
					if (lk) { r.Locked++; everLockedAfter = true; }
					string State() => $"t{t} target {n.target} lock {LevyNpcData.Of(n).SoldierTarget} why \"{EnemyTargeting.WhyTrack[who]}\" ai {n.ai[0]:0.#},{n.ai[1]:0.#},{n.ai[2]:0.#},{n.ai[3]:0.#} tl {n.timeLeft} enc {n.despawnEncouraged} pos ({n.Center.X / 16f:0},{n.Center.Y / 16f:0}) levies {LevyNpcCount()} nearest {NearestLevyTiles(army, n):0} routed {CountState(army, SoldierState.Routed)} home {CountState(army, SoldierState.ReturnHome)}";
					if (wasLocked && !lk && r.LockLostAt < 0) { r.LockLostAt = t; r.AtLockLost = State(); }
					if (t == 0 && !lk && r.LockLostAt < 0) { r.LockLostAt = 0; r.AtLockLost = State(); }
					wasLocked = lk;
					if (n.timeLeft < prevTl - 5 && r.TlSlashAt < 0) { r.TlSlashAt = t; r.AtSlash = State(); }
					prevTl = n.timeLeft;
					r.MinTl = Math.Min(r.MinTl, n.timeLeft); minY = Math.Min(minY, n.Center.Y); lastX = n.Center.X;
					r.AtGone = State();
					if (t % 60 == 0 || t < 3) trace.Append(" | ").Append(State()).Append(LfParts(n));
				}
				if (r.Gone >= 0 && r.AtGone == "") r.AtGone = "gone on the first tick";
				r.DriftTiles = (lastX - startX) / 16f; r.HomeAtEnd = CountState(army, SoldierState.ReturnHome);
				r.Rise = $"rose {(startY - minY) / 16f:0} tiles, drifted {(lastX - startX) / 16f:+0;-0} tiles in x (the barracks lies {-_lfHomeTiles} x){(everLockedAfter ? "" : ", never locked on a levy after the death")}";
				r.Trace = pre + " || after the death:" + trace;
				return r;
			}
			finally
			{
				global::Levy.Common.Observe.MindControl.End(0);
				On_NPC.EncourageDespawn -= encDet; On_NPC.CheckActive -= actDet; On_NPC.UpdateNPC -= updDet;
				_cmdDead = false; _perTick = null; EnemyTargeting.WhyTrack = null; EnemyTargeting.WhyCount = null;
				SetBossesTarget(null);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles(); ClearMachines();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && x.ModNPC is SoldierNPC) x.active = false; }
				RosterSystem.Instance.Regulars.RemoveAll(x => x.CommanderName == Commander);
				_lfDay = false; _lfZones = null;
			}
		}

		/// <summary>
		/// The worm leash (Eve, 2026-11-22: a beaten worm measured from its head alone dug its head 201 tiles off while its body was on the army, and left). After the commander's death the worm's head is held 260 tiles from the army
		/// and its tail 15 tiles over it: a levy is within the leash of the tail, so the worm keeps fighting (the head-only rule would have let it go). Then the tail is held out too: both ends out, the worm drops the army and leaves.
		/// The two ends are put back after their own update every tick, so every reader that tick sees them where the trial put them.
		/// </summary>
		private static void LfWormTrial(LfBoss b, int type)
		{
			string label = $"{b.Name} [worm leash]";
			ClearHostiles();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC) n.active = false; }
			RosterSystem.Instance.Regulars.RemoveAll(x => x.CommanderName == Commander);
			_cmdDead = false;
			int who = -1, tailWho = -1; Vector2 headAt = default, tailAt = default; bool pin = false;
			On_NPC.hook_UpdateNPC pinDet = (orig, self, i) =>
			{
				orig(self, i);
				if (!pin) return;
				if (i == who && self.active) { self.Center = headAt; self.velocity = Vector2.Zero; }
				if (i == tailWho && self.active) { self.Center = tailAt; self.velocity = Vector2.Zero; }
			};
			On_NPC.UpdateNPC += pinDet;
			bool tailOn = EnemyTargeting.WormTailOn;
			try
			{
				LfArena("big");
				bool calm = ModLoader.HasMod("CalamityMod");
				SetRevenge(calm, calm); Main.GameMode = calm ? GameModeID.Master : GameModeID.Expert; Main.hardMode = b.Hard;
				_lfDay = false; _lfZones = b.Zones;
				PlaceCommander(0);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, true);
				SetBossesTarget(true);
				EnemyTargeting.RefreshConfig();
				var army = new List<SoldierNPC>();
				for (int k = 0; k < 8; k++)
				{
					var s = LfLevy(new Vector2((Cx + (k % 2 == 0 ? -1 : 1) * (6 + k * 2)) * 16f, _floorY * 16f), SoldierOrder.Follow);
					if (s == null) continue;
					s.NPC.immortal = true; army.Add(s);
				}
				Step(2);
				int idx = NPC.NewNPC(new EntitySource_BossSpawn(Main.player[0]), (int)_cmdAt.X, (int)_cmdAt.Y - 1600, type, 1);
				NPC boss = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
				if (boss == null || boss.type != type) { LfCheck(label + ": the worm spawns", false); return; }
				boss.timeLeft *= 20; who = boss.whoAmI;
				bool Alive() => Main.npc[who].active && Main.npc[who].type == type;
				void TopUp() { if (!Alive()) return; NPC n = Main.npc[who]; n.life = n.lifeMax; for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && i != who && x.realLife == who && x.lifeMax > 0) x.life = x.lifeMax; } }
				void Heal() { foreach (var l in army) if (l.NPC.active) { l.NPC.life = l.NPC.lifeMax; if (l.Record != null) l.Record.CurrentHp = l.NPC.lifeMax; } }
				void Pin() { if (!pin) return; if (Alive()) { Main.npc[who].Center = headAt; Main.npc[who].velocity = Vector2.Zero; } if (tailWho >= 0 && Main.npc[tailWho].active) { Main.npc[tailWho].Center = tailAt; Main.npc[tailWho].velocity = Vector2.Zero; } }
				_perTick = () => { TopUp(); Heal(); Pin(); };
				Step(120);
				if (!Alive()) { LfCheck(label + ": the worm is still in the fight before the commander falls", false); return; }
				_cmdDead = true;
				Step(2);
				NPC head = Main.npc[who];
				int segs = 0; for (int i = 0; i < Main.maxNPCs; i++) if (i != who && Main.npc[i].active && Main.npc[i].realLife == who) segs++;
				EnemyTargeting.WormTailOn = true; NPC tail = EnemyTargeting.WormTail(head); EnemyTargeting.WormTailOn = tailOn; // found even with LEVY_WORMTAIL=0 (the A/B run: the same trial with the head-only rule)
				int expect = b.Cal != null ? CalType(b.Cal.Replace("Head", "Tail")) : NPCID.TheDestroyerTail;
				LfCheck($"{label}: the tail is found ({(tail == null ? "none" : "type " + tail.type)}, expected type {expect}, {segs} segments in the pool)", tail != null && tail.type == expect);
				if (tail == null) return;
				tailWho = tail.whoAmI;
				// the army stands around the spot where the commander fell; the head goes 260 tiles off, the tail 15 tiles over the army
				Vector2 army0 = _cmdAt;
				headAt = army0 + new Vector2(260 * 16f, -30 * 16f); tailAt = army0 + new Vector2(0f, -15 * 16f); pin = true; Pin();
				float leash = LevyConstants.Targeting.LostFightLeashPx;
				int ticks = 300, alive = 0, locked = 0, within = 0, headOut = 0, tailIn = 0, holds = 0, minTl = int.MaxValue, headOnly = 0;
				for (int t = 0; t < ticks; t++)
				{
					Step(1);
					if (!Alive()) break;
					alive++;
					NPC h = Main.npc[who];
					if (t >= 60 && Locked(h)) locked++;
					if (EnemyTargeting.LevyWithin(h, leash)) within++;
					float dh = NearestTo(army, h.Center), dt = NearestTo(army, Main.npc[tailWho].Center);
					if (dh > LevyConstants.Targeting.LostFightLeashTiles) headOut++;
					if (dt <= LevyConstants.Targeting.LostFightLeashTiles) tailIn++;
					if (army.Count > 0 && EnemyTargeting.LostFightHolds(army[0].NPC.Center)) holds++;
					if (t >= 60) minTl = Math.Min(minTl, h.timeLeft);
					if (t % 60 == 30) { EnemyTargeting.WormTailOn = false; if (!EnemyTargeting.LevyWithin(h, leash)) headOnly++; EnemyTargeting.WormTailOn = tailOn; }
				}
				LfNote($"{label}: head held 260 tiles off, tail over the army: alive {alive}/{ticks}, head out of the leash {headOut}, tail in it {tailIn}, a levy within the leash {within}, the army holds the rally {holds}, locked on a levy {locked}/{ticks - 60} after the first second, min timeLeft {minTl}, head alone out of reach {headOnly}/5 samples");
				LfCheck($"{label}: the geometry holds (head out of the leash, tail in it, every tick)", headOut == alive && tailIn == alive && alive > 0, () => $"head out {headOut}, tail in {tailIn}, of {alive}");
				LfCheck($"{label}: measured from the head alone no levy is in the leash (the head-only rule lets it go)", headOnly == 5, () => headOnly + "/5");
				LfCheck($"{label}: head out, tail in: the worm keeps fighting the levies", alive == ticks && within == ticks && locked * 10 >= (ticks - 60) * 7 && minTl > 60, () => $"alive {alive}, within {within}, locked {locked}/{ticks - 60}, min timeLeft {minTl}");
				LfCheck($"{label}: head out, tail in: the army holds where the commander fell", holds == alive, () => $"{holds}/{alive}");
				if (!Alive()) return;
				// both ends out: head and tail held 400 tiles off (past the leash and past the range the game keeps an NPC alive in around a player)
				headAt = army0 + new Vector2(400 * 16f, -30 * 16f); tailAt = headAt + new Vector2(0f, 12 * 16f); Pin();
				int lockedB = 0, withinB = 0, holdsB = 0;
				for (int t = 0; t < 150; t++)
				{
					Step(1);
					if (!Alive()) break;
					NPC h = Main.npc[who];
					if (t >= 90 && Locked(h)) lockedB++;
					if (EnemyTargeting.LevyWithin(h, leash)) withinB++;
					if (army.Count > 0 && EnemyTargeting.LostFightHolds(army[0].NPC.Center)) holdsB++;
				}
				LfCheck($"{label}: both ends out: no levy in the leash, the lock drops within 1.5 s and the army stops holding", withinB == 0 && lockedB == 0 && holdsB == 0, () => $"within {withinB}, locked after 1.5 s {lockedB}/60, holds {holdsB}");
				// let it go: nothing holds it now, so it leaves on its own AI's terms. The army is held 450 tiles the other way, so the worm's flight cannot bring it back into the leash by chance.
				// The spawn-time timeLeft boost of this trial is taken back first: the worm starts from the timeLeft the keep-alive gave it (NPC.activeTime), as in a real fight.
				pin = false; Main.npc[who].timeLeft = Math.Min(Main.npc[who].timeLeft, NPC.activeTime);
				_perTick = () => { TopUp(); Heal(); for (int k = 0; k < army.Count; k++) if (army[k].NPC.active) { army[k].NPC.Bottom = new Vector2((Cx - 450 + k * 2) * 16f, _floorY * 16f); army[k].NPC.velocity = Vector2.Zero; } };
				int gone = -1; var leaveTrace = new StringBuilder();
				for (int t = 0; t < 1500 && gone < 0; t++)
				{
					Step(1);
					if (!Alive()) { gone = t; break; }
					NPC h = Main.npc[who];
					if (t % 300 == 0) leaveTrace.Append($" | t{t} ({h.Center.X / 16f:0},{h.Center.Y / 16f:0}) target {h.target} lock {LevyNpcData.Of(h).SoldierTarget} tl {h.timeLeft} nearest levy {NearestTo(army, h.Center):0} (tail {NearestTo(army, Main.npc[tailWho].Center):0})");
				}
				LfNote($"{label}: both ends out: {(gone >= 0 ? "gone " + (gone / 60f).ToString("0.0") + " s later" : "still here after 25 s")}{leaveTrace}");
				LfCheck($"{label}: both ends out: the worm leaves", gone >= 0, () => "still here after 25 s at (" + (Main.npc[who].Center.X / 16f).ToString("0") + "," + (Main.npc[who].Center.Y / 16f).ToString("0") + ")");
			}
			finally
			{
				pin = false; EnemyTargeting.WormTailOn = tailOn;
				On_NPC.UpdateNPC -= pinDet;
				_cmdDead = false; _perTick = null;
				SetBossesTarget(null);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles(); ClearMachines();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && x.ModNPC is SoldierNPC) x.active = false; }
				RosterSystem.Instance.Regulars.RemoveAll(x => x.CommanderName == Commander);
				_lfDay = false; _lfZones = null;
			}
		}

		private static float NearestTo(List<SoldierNPC> army, Vector2 at)
		{
			float near = float.MaxValue;
			foreach (var l in army) if (l.NPC.active && l.IsAlive) near = Math.Min(near, Vector2.Distance(l.NPC.Center, at) / 16f);
			return near;
		}

		/// <summary>Trace: up to three parts of this boss (hooks, hands, segments), where they are, what they are locked on and what they target.</summary>
		private static string LfParts(NPC boss)
		{
			if (Environment.GetEnvironmentVariable("LEVY_LF_TRACE") != "1") return "";
			var sb = new StringBuilder(); int k = 0;
			int bl = LevyNpcData.Of(boss).SoldierTarget;
			if (bl >= 0 && bl < Main.maxNPCs && Main.npc[bl].active) sb.Append($" locked levy at ({Main.npc[bl].Center.X / 16f:0},{Main.npc[bl].Center.Y / 16f:0}){(Main.npc[bl].ModNPC is SoldierNPC sl ? " " + sl.State + (sl.BodyguardRunning ? " decoy" : "") : "")}");
			for (int i = 0; i < Main.maxNPCs && k < 3; i++)
			{
				NPC p = Main.npc[i];
				if (!p.active || i == boss.whoAmI || p.realLife == boss.whoAmI) continue;
				if (!ReferenceEquals(LevyUtilRoot(p), boss) && !(global::Levy.Common.Boss.BossRule.TryGetRoot(p, out NPC root, out _) && ReferenceEquals(root, boss))) continue;
				sb.Append($" part {p.type} ({p.Center.X / 16f:0},{p.Center.Y / 16f:0}) lock {LevyNpcData.Of(p).SoldierTarget} target {p.target} ai {p.ai[0]:0.#},{p.ai[1]:0.#}"); k++;
			}
			return sb.ToString();
		}

		private static int CountState(List<SoldierNPC> army, SoldierState st) { int c = 0; foreach (var l in army) if (l.NPC.active && l.State == st) c++; return c; }

		private static NPC LevyUtilRoot(NPC n) => global::Levy.Common.Util.LevyUtil.RootNpc(n);

		private static float NearestLevyTiles(List<SoldierNPC> army, NPC boss)
		{
			float near = float.MaxValue; Vector2 at = LevyUtilRoot(boss).Center;
			foreach (var l in army) if (l.NPC.active && l.IsAlive) near = Math.Min(near, Vector2.Distance(l.NPC.Center, at) / 16f);
			return near == float.MaxValue ? -1 : near;
		}
	}
}
