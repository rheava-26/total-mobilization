using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using Levy.Common.Boss;
using Levy.Common.Config;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// REAL-ENEMY DODGE MATRIX (LEVY_FIELD_SPEC=real). Real vanilla / Calamity bosses and ordinary enemies with their own AI attack a squad of mortal levies in four arenas
	/// (flat, platform rows, natural hills, mixed with lava and an island). A stand-in commander stands apart from the squad and the boss targets HIM, as in real play, so a dash passes through the levies.
	/// Dead levies are replaced after a pause. Every cell prints one "REALM|..." line: survival time, deaths/min, damage to the boss, contact vs projectile wounds, lava time.
	/// Env: LEVY_REAL=boss keys  LEVY_REAL_ARENA=flat,platform,natural,mixed  LEVY_REAL_GEAR=none,hook,bottle,hermes,dash,all  LEVY_REAL_MODE=0|2 (0 = no dodging)  LEVY_REAL_BEFORE=1 (the old mobility code)
	/// LEVY_REAL_TICKS=1800  LEVY_REAL_N=4  LEVY_REAL_SEEDS=1  LEVY_REAL_TARGET=cmd|army
	/// </summary>
	public partial class CombatHarness
	{
		private bool _faReal; private int _hookDrawOk;
		internal static bool RealOn;

		private sealed class RealBoss { public string Key, Label; public Func<int> Type; public float Frac = 1f; public bool Mob; public int MobCount; public int SpawnUpTiles; public int SpawnAcross = 30; }

		private static int ModNpc(string mod, string name) { try { return ModLoader.TryGetMod(mod, out Mod m) && m.TryFind(name, out ModNPC n) ? n.Type : -1; } catch { return -1; } }

		private static List<RealBoss> RealBosses() => new()
		{
			new RealBoss { Key = "kingslime", Label = "King Slime", Type = () => NPCID.KingSlime, SpawnUpTiles = 4 },
			new RealBoss { Key = "eoc", Label = "Eye of Cthulhu p1", Type = () => NPCID.EyeofCthulhu, SpawnUpTiles = 9 },
			new RealBoss { Key = "eoc2", Label = "Eye of Cthulhu p2", Type = () => NPCID.EyeofCthulhu, Frac = 0.4f, SpawnUpTiles = 9 },
			new RealBoss { Key = "brain", Label = "Brain of Cthulhu", Type = () => NPCID.BrainofCthulhu, SpawnUpTiles = 14 },
			new RealBoss { Key = "brain2", Label = "Brain of Cthulhu p2", Type = () => NPCID.BrainofCthulhu, Frac = 0.4f, SpawnUpTiles = 14 },
			new RealBoss { Key = "eow", Label = "Eater of Worlds", Type = () => NPCID.EaterofWorldsHead, SpawnUpTiles = 10 },
			new RealBoss { Key = "queenbee", Label = "Queen Bee", Type = () => NPCID.QueenBee, SpawnUpTiles = 14 },
			new RealBoss { Key = "skeletron", Label = "Skeletron", Type = () => NPCID.SkeletronHead, SpawnUpTiles = 14 },
			new RealBoss { Key = "skeletron2", Label = "Skeletron spinning", Type = () => NPCID.SkeletronHead, Frac = 0.3f, SpawnUpTiles = 14 },
			new RealBoss { Key = "deerclops", Label = "Deerclops", Type = () => NPCID.Deerclops, SpawnUpTiles = 4 },
			new RealBoss { Key = "desertscourge", Label = "Desert Scourge", Type = () => ModNpc("CalamityMod", "DesertScourgeHead"), SpawnUpTiles = 3, SpawnAcross = 40 },
			new RealBoss { Key = "crabulon", Label = "Crabulon", Type = () => ModNpc("CalamityMod", "Crabulon"), SpawnUpTiles = 4 },
			new RealBoss { Key = "hivemind", Label = "Hive Mind", Type = () => ModNpc("CalamityMod", "HiveMind"), SpawnUpTiles = 16 },
			new RealBoss { Key = "perforator", Label = "Perforators", Type = () => ModNpc("CalamityMod", "PerforatorHive"), SpawnUpTiles = 16 },
			new RealBoss { Key = "slimegod", Label = "Slime God", Type = () => ModNpc("CalamityMod", "SlimeGodCore"), SpawnUpTiles = 10 },
			new RealBoss { Key = "mob-zombie", Label = "Zombies", Type = () => NPCID.Zombie, Mob = true, MobCount = 6 },
			new RealBoss { Key = "mob-eye", Label = "Demon Eyes", Type = () => NPCID.DemonEye, Mob = true, MobCount = 5, SpawnUpTiles = 10 },
			new RealBoss { Key = "mob-archer", Label = "Goblin Archers", Type = () => NPCID.GoblinArcher, Mob = true, MobCount = 4 },
			new RealBoss { Key = "mob-imp", Label = "Fire Imps", Type = () => NPCID.FireImp, Mob = true, MobCount = 3, SpawnUpTiles = 6 },
			new RealBoss { Key = "mob-slime", Label = "Blue Slimes", Type = () => NPCID.BlueSlime, Mob = true, MobCount = 6 },
		};

		private static void RealUnlockBosses()
		{
			try
			{
				var f = typeof(BossDownedSystem).GetField("_downed", BindingFlags.NonPublic | BindingFlags.Static);
				var set = (HashSet<string>)f.GetValue(null);
				foreach (var rb in RealBosses()) { int t = rb.Type(); if (t > 0 && !rb.Mob) set.Add(BossDownedSystem.KeyOfType(t)); }
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("FIELDAI-REAL: could not unlock bosses: " + e.Message); }
		}

		private void FaAddReal()
		{
			string[] Env(string k, string d) => (Environment.GetEnvironmentVariable(k) ?? d).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var want = new HashSet<string>(Env("LEVY_REAL", string.Join(",", RealBosses().Where(b => !b.Mob).Select(b => b.Key))));
			var arenas = Env("LEVY_REAL_ARENA", "flat,platform,natural,mixed"); var gears = Env("LEVY_REAL_GEAR", "none,hook,bottle,hermes,dash,all");
			int mode = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_MODE"), out int md) ? md : 2;
			string target = Environment.GetEnvironmentVariable("LEVY_REAL_TARGET") ?? "cmd";
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_TICKS"), out int tk) ? tk : 1800;
			int seeds = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_SEEDS"), out int sd) ? sd : 1;
			foreach (var rb in RealBosses())
			{
				if (!want.Contains(rb.Key)) continue;
				if (rb.Type() <= 0) { Mod.Logger.Info($"FIELDAI-REAL: {rb.Key} skipped (its mod is not loaded)"); continue; }
				foreach (string ar in arenas) foreach (string gear in gears) for (int seed = 1; seed <= seeds; seed++)
					_fa.Add(FaReal(rb, mode, target, ar, gear, seed, ticks));
			}
		}

		// ---- arenas ----
		private int[] _faGround;   // top solid row per tile column, before decorations
		private int FaGroundRow(float xPx) { int x = (int)(xPx / 16f); return _faGround != null && x >= _cx - 108 && x <= _cx + 108 ? _faGround[x - (_cx - 108)] : _faFy; }

		private void FaBuildArena(string kind)
		{
			int cx = _cx, fy = _faFy;
			FaArena();
			_faGround = new int[217];
			bool hills = kind == "natural" || kind == "mixed";
			for (int x = cx - 108; x <= cx + 108; x++)
			{
				int h = 0;
				if (hills) { double v = 4.5 * Math.Sin(x * 0.07) + 2.5 * Math.Sin(x * 0.16 + 1.3) + 1.0 * Math.Sin(x * 0.35); h = Math.Max(0, (int)Math.Round(v + 7)); }
				if (h > 0) FaFill(x, fy - h, x, fy - 1, true, TileID.Dirt);
				_faGround[x - (cx - 108)] = fy - h;
			}
			void Plat(int x0, int x1, int y)
			{
				for (int x = x0; x <= x1; x++) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Platforms; t.WallType = 0; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.Slope = Terraria.ID.SlopeType.Solid; }
			}
			if (kind == "platform" || kind.StartsWith("plat"))
			{
				// "platform" = 6-tile rows; "platN" = N-tile rows; a trailing "s" adds half-height steps at the row ends (the step or ramp a player builds)
				int sp = kind == "platform" ? 6 : int.Parse(kind.Substring(4, 1)); bool steps = kind.EndsWith("s");
				for (int r = 1; sp * r <= 30; r++)
				{
					int y = fy - sp * r; int off = (r % 2) * 14;
					Plat(cx - 60 + off, cx - 8 + off / 2, y); Plat(cx + 4 + off / 2, cx + 66 - off, y);
					if (steps)
					{
						int sy = y + sp / 2;
						Plat(cx - 64 + off, cx - 62 + off, sy); Plat(cx - 6 + off / 2, cx - 4 + off / 2, sy); Plat(cx + 2 + off / 2, cx + 3 + off / 2, sy); Plat(cx + 67 - off, cx + 69 - off, sy);
					}
				}
			}
			if (kind == "mixed")
			{
				for (int r = 1; r <= 3; r++) { int y = fy - 16 - 7 * r + 7; Plat(cx - 50 + r * 6, cx + 20 + r * 6, y); }
				FaFill(cx + 55, fy - 42, cx + 70, fy - 40, true, TileID.Stone);       // a floating island
				FaFill(cx - 20, fy, cx - 12, fy + 3, false);                           // a lava pit in the floor
				for (int x = cx - 20; x <= cx - 12; x++) for (int y = fy; y <= fy + 3; y++) { Tile t = Main.tile[x, y]; t.LiquidAmount = 255; t.LiquidType = Terraria.ID.LiquidID.Lava; }
				for (int x = cx - 20; x <= cx - 12; x++) _faGround[x - (cx - 108)] = fy + 4;
			}
		}

		private static Item RItem(int type, int stack = 1) { var it = new Item(); it.SetDefaults(type); it.stack = stack; return it; }

		private static void RealGear(SoldierRecord r, string gear)
		{
			r.LifeCrystals = 8;
			r.Order = (Environment.GetEnvironmentVariable("LEVY_REAL_ORDER") ?? "freerein").ToLowerInvariant() switch { "follow" => SoldierOrder.Follow, "hold" => SoldierOrder.Hold, "search" => SoldierOrder.SearchDestroy, _ => SoldierOrder.FreeRein }; r.HoldFire = false;
			r.Gear.Weapon = RItem(ItemID.Minishark); r.Gear.Ammo = RItem(ItemID.MusketBall, 999);
			r.Gear.Head = RItem(ItemID.GoldHelmet); r.Gear.Body = RItem(ItemID.GoldChainmail); r.Gear.Legs = RItem(ItemID.GoldGreaves);
			int a = 0;
			if (gear == "all") r.Grade = Grade.Champion;
			if (gear == "hook" || gear == "all") { r.Gear.Hook = RItem(ItemID.GrapplingHook); r.Gear.SetLocked(SoldierGear.SlotHook, true); }
			if (gear == "bottle" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.CloudinaBottle);
			if (gear == "hermes" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.HermesBoots);
			if (gear == "dash" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.EoCShield);
			if (gear == "wings" || gear == "allw") r.Gear.Accessories[a++] = RItem(ItemID.AngelWings);
			if (gear == "rocket") r.Gear.Accessories[a++] = RItem(ItemID.RocketBoots);
			if (gear == "allw") { r.Grade = Grade.Champion; r.Gear.Hook = RItem(ItemID.GrapplingHook); r.Gear.SetLocked(SoldierGear.SlotHook, true); r.Gear.Accessories[a++] = RItem(ItemID.CloudinaBottle); r.Gear.Accessories[a++] = RItem(ItemID.HermesBoots); r.Gear.Accessories[a++] = RItem(ItemID.EoCShield); }
		}

		private int RealN => int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_N"), out int en) ? en : 4;
		private float RealSpawnX(int k) => _cx * 16f + (k - (RealN - 1) / 2f) * 7f * 16f + 8f;

		private FaTrial FaReal(RealBoss rb, int mode, string target, string arena, string gear, int seed, int ticks)
		{
			int N = RealN;
			bool before = Environment.GetEnvironmentVariable("LEVY_REAL_BEFORE") == "1";
			var lastHits = new int[N]; var born = new int[N]; var deadAt = new int[N]; var lastPos = new Vector2[N];
			int deaths = 0; long lifeSum = 0; int lives = 0, contactHits = 0, projHits = 0, lavaTicks = 0, hitsTotal = 0, exposure = 0, teleports = 0, hookCasts = 0, maneuvers = 0;
			long bossDmg = 0; int fieldSeesBoss = 0, bossFastTicks = 0; float bossPeakV = 0f; int prevBossLife = -1; int bossIdx = -1; bool bossGone = false;
			var mobs = new List<int>(); var byKind = new Dictionary<string, int>();
			bool cmdMove = Environment.GetEnvironmentVariable("LEVY_REAL_CMDMOVE") != "0"; bool auto = Environment.GetEnvironmentVariable("LEVY_REAL_AUTO") == "1" || target == "spec"; bool lesser = rb.Key.StartsWith("eoc") || rb.Key == "kingslime" || rb.Mob; int specBuff = ModContent.BuffType<Content.Buffs.SpectatorBuff>(); int autoBuff = ModContent.BuffType<Content.Buffs.AutonomousBuff>(); float cmdBaseX = (_cx - 17) * 16f + 8f;
			var lastGrY = new float[N]; var peakVy = new float[N]; var inAir = new bool[N]; var lastHitTick = new int[N]; int stillTicks = 0, aliveTicks = 0, edgeFalls = 0, aboveTicks = 0, climbs = 0, rowTicks = 0, airTicks = 0, airDodge = 0, airScan = 0, airKick = 0, airFall = 0, pEv = 0, pNo = 0, pOk = 0;
			int dCorner = 0, dOver = 0, dLegs = 0, dHops = 0, dFlights = 0, dFlips = 0; var why = new Dictionary<string, int>(); int still5 = 0, still5Air = 0, aliveAirKit = 0;
			long tgL = 0, tgP = 0, tgN = 0, bgL = 0, bgP = 0, bgN = 0, tgSamples = 0, bSamples = 0; double dLevySum = 0, dCmdSum = 0; long dSamples = 0; int aiTrans = 0, newProj = 0, fastStarts = 0; float prevSp = 0f; int prevA0 = -999, prevA1 = -999; var aiHist = new Dictionary<string, int>(); var projSeen = new HashSet<int>(); var projSeenId = new Dictionary<int, int>(); bool logT = arena == "flat" && gear == "none" && seed == 1;
			int dNear = 0, dBase = 0, dNoBetter = 0, dBusy = 0, dDJ = 0, dDash = 0, dHook = 0, dJump = 0, dStep = 0;
			void Acc(SoldierNPC b) { dCorner += b.DanceCorners; dOver += b.DanceOvers; dLegs += b.DanceLegs; dHops += b.DanceHops; dFlights += b.DanceFlights; dFlips += b.DanceFlips; climbs += b.PlatformClimbs; airDodge += b.DodgeAirManeuvers; airScan += b.DodgeAirScans; airKick += b.DodgeAirKicks; pEv += b.PlatEvals; pNo += b.PlatNoCand; pOk += b.PlatSepOk; airFall += b.DodgeAirFalls; hookCasts += b.HkCasts; maneuvers += b.DodgeManeuvers; dNear += b.DodgeNearScans; dBase += b.DodgeBaseHitScans; dNoBetter += b.DodgeNoBetter; dBusy += b.DodgeSkippedBusy; dDJ += b.DodgeDoubleJumps; dDash += b.DodgeDashes; dHook += b.DodgeHooks; dJump += b.DodgeJumps; dStep += b.DodgeSteps; }
			return new FaTrial
			{
				Name = "REAL", Ticks = ticks,
				Setup = () =>
				{
					_faReal = true; RealOn = true;
					Main.rand = new UnifiedRandom(1000 + seed * 7919);
					SoldierNPC.DodgeMode = mode; SoldierNPC.FallDamageOverride = 0; SoldierNPC.MobilityMode = before ? 0 : 1;
					SoldierNPC.DanceOff = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_DOFF"), out int dof) ? dof : 0; SoldierNPC.DanceMode = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_DANCE"), out int dm) ? dm : 1; SoldierNPC.AirMode = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_AIRMODE"), out int am) ? am : 1; SoldierNPC.PlatMode = int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_PLATMODE"), out int pm) ? pm : 1;
					var cfg = LevyConfig.Get; cfg.EnemiesTargetSoldiers = true; cfg.BossesTargetSoldiers = target == "army" || target == "spec";
					try { global::Levy.Common.Targeting.EnemyTargeting.RefreshConfig(); } catch { }
					RealUnlockBosses();
					FaBuildArena(arena);
					int cx = _cx;
					// a campfire and a heart lantern in the middle of the squad (the real station tiles the station system scans for)
					{ int gx = cx; int gy = FaGroundRow(gx * 16f); Tile c = Main.tile[gx, gy - 1]; c.HasTile = true; c.TileType = TileID.Campfire; c.TileFrameX = 0; c.TileFrameY = 0; Tile l = Main.tile[gx + 3, gy - 7]; l.HasTile = true; l.TileType = TileID.HangingLanterns; l.TileFrameX = 0; l.TileFrameY = 324; }
					float cmdX = (cx - 17) * 16f + 8f; if (target == "army") cmdX = (cx - 104) * 16f; if (target == "spec") cmdX = (cx - 70) * 16f;
					_faCmdPos = new Vector2(cmdX, FaGroundRow(cmdX) * 16f - Main.player[0].height);
					for (int k = 0; k < N; k++) { FaSpawnSlot(k, RealSpawnX(k), FaGroundRow(RealSpawnX(k)) * 16f, r => RealGear(r, gear)).RefreshStats(); lastPos[k] = _faSol[k].NPC.Center; }
					if (!rb.Mob)
					{
						float bx = (cx + rb.SpawnAcross) * 16f;
						int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyField"), (int)bx, FaGroundRow(bx) * 16 - rb.SpawnUpTiles * 16, rb.Type());
						if (i < 0 || i >= Main.maxNPCs) throw new Exception("boss spawn failed " + rb.Key);
						bossIdx = i; _faStandin = i;
						NPC n = Main.npc[i]; n.dontTakeDamage = false; n.defense = 0; n.value = 0f;
						n.lifeMax = 2_000_000; n.life = (int)(n.lifeMax * rb.Frac); prevBossLife = n.life;
					}
					else for (int m = 0; m < rb.MobCount; m++) FaSpawnMob(rb, mobs, m);
				},
				Step = t =>
				{
					{ Player zp = Main.player[0]; zp.ZoneDesert = rb.Key == "desertscourge"; zp.ZoneJungle = rb.Key == "queenbee"; zp.ZoneSnow = rb.Key == "deerclops"; zp.ZoneGlowshroom = rb.Key == "crabulon"; zp.ZoneCorrupt = rb.Key == "hivemind"; zp.ZoneCrimson = rb.Key == "brain" || rb.Key == "perforator"; }   // biome-tied bosses enrage or flee from a target outside their home
					if (target == "spec") { Player sp0 = Main.player[0]; if (!sp0.HasBuff(specBuff)) sp0.AddBuff(specBuff, 600); }
					foreach (var hb in _faSol) if (hb.NPC.active && hb.PotionSickness == 0 && hb.NPC.life * 100 < hb.NPC.lifeMax * LevyConstants.PotionDrinkBelowPercent) { hb.NPC.life = Math.Min(hb.NPC.lifeMax, hb.NPC.life + (lesser ? 50 : 100)); hb.PotionSickness = LevyConstants.PotionSicknessTicks; }
					if (cmdMove && target != "spec") { float mx = cmdBaseX + (float)Math.Sin(t / 110.0) * 9f * 16f; _faCmdPos = new Vector2(mx, FaGroundRow(mx) * 16f - Main.player[0].height); }
					if (auto) foreach (var sb0 in _faSol) if (sb0.NPC.active && !sb0.NPC.HasBuff(autoBuff)) sb0.NPC.AddBuff(autoBuff, 600);
					if (bossIdx >= 0)
					{
						NPC bn = Main.npc[bossIdx];
						if (!bn.active && !bossGone) { bossGone = true; Mod.Logger.Info($"FIELDAI-REAL: boss {rb.Key} inactive at tick {t}"); }
						if (bn.active)
						{
							bn.timeLeft = 3000; bn.dontTakeDamage = false;
							if (prevBossLife > 0 && bn.life < prevBossLife) bossDmg += prevBossLife - bn.life;
							if (bn.life < bn.lifeMax * (rb.Frac - 0.08f)) bn.life = (int)(bn.lifeMax * rb.Frac);
							prevBossLife = bn.life;
							float sp = bn.velocity.Length(); bossPeakV = Math.Max(bossPeakV, sp); if (sp > 4.5f) bossFastTicks++;
							foreach (var th in ThreatField.Snapshot()) if (th.Body && Vector2.Distance(th.P, bn.Center) < 30f) { fieldSeesBoss++; break; }
						}
					}
					else if (t % 90 == 0) { for (int m = 0; m < mobs.Count; m++) if (!Main.npc[mobs[m]].active || Main.npc[mobs[m]].type != rb.Type()) FaSpawnMob(rb, mobs, m); }
					if (t % 30 == 0) for (int pi = 0; pi < Main.maxProjectiles; pi++) { Projectile hp0 = Main.projectile[pi]; if (hp0.active && hp0.TryGetGlobalProjectile(out HookTag ht) && ht.Soldier >= 0) { float err = ht.SwapError(hp0); if (err > 6f) Mod.Logger.Error($"HOOKDRAW-CHECK FAILED: chain origin {err:0}px from the soldier"); else _hookDrawOk++; break; } }
					{
						// TARGETING: every hostile body (the boss, its parts and minions) each tick: who does it target (levy = the dummy slot or >= 300, player, none)
						Player cmdP = Main.player[0]; float nearLv = 1e9f; foreach (var lv in _faSol) if (lv.NPC.active && lv.NPC.life > 0 && lv.IsAlive) nearLv = Math.Min(nearLv, 0f);
						for (int i = 0; i < Main.maxNPCs; i++)
						{
							NPC hn = Main.npc[i]; if (!hn.active || hn.friendly || hn.townNPC || hn.damage <= 0 || hn.ModNPC is SoldierNPC) continue;
							tgSamples++; bool isB = i == bossIdx;
							if (hn.target == SoldierNPC.DummySlot || hn.target >= 300) { tgL++; if (isB) bgL++; } else if (hn.target >= 0 && hn.target < Main.maxPlayers) { tgP++; if (isB) bgP++; } else { tgN++; if (isB) bgN++; }
							if (isB)
							{
								bSamples++; float bl = 1e9f; foreach (var lv in _faSol) if (lv.NPC.active && lv.NPC.life > 0 && lv.IsAlive) bl = Math.Min(bl, Vector2.Distance(lv.NPC.Center, hn.Center));
								if (bl < 1e8f) { dLevySum += bl / 16f; dSamples++; } dCmdSum += Vector2.Distance(cmdP.Center, hn.Center) / 16f;
								int a0 = (int)hn.ai[0], a1 = (int)hn.ai[1]; if (a0 != prevA0 || a1 != prevA1) { aiTrans++; prevA0 = a0; prevA1 = a1; }
								string ak = a0 + "/" + a1; aiHist[ak] = aiHist.GetValueOrDefault(ak) + 1;
								float sp2 = hn.velocity.Length(); if (sp2 > 8f && prevSp <= 4f) fastStarts++; prevSp = sp2;
								if (logT && t < 300 && t % 10 == 0) Mod.Logger.Info($"REALT|boss={rb.Key}|t={t}|tgt={hn.target}|bx={hn.Center.X / 16f:0}|by={hn.Center.Y / 16f:0}|nearLevy={bl / 16f:0.0}|cmdDist={Vector2.Distance(cmdP.Center, hn.Center) / 16f:0.0}|ai={a0}/{a1}|v={hn.velocity.Length():0.0}|cmdActive={(cmdP.active ? 1 : 0)}");
							}
						}
						for (int pi = 0; pi < Main.maxProjectiles; pi++) { Projectile hp1 = Main.projectile[pi]; if (hp1.active && hp1.hostile && hp1.damage > 0) { if (!projSeenId.TryGetValue(pi, out int idv) || idv != hp1.identity) { projSeenId[pi] = hp1.identity; newProj++; } } else projSeenId.Remove(pi); }
					}
					bool exposed = false;
					for (int i = 0; i < Main.maxNPCs && !exposed; i++) { NPC n = Main.npc[i]; if (!n.active || n.friendly || n.townNPC || n.damage <= 0 || n.ModNPC is SoldierNPC) continue; foreach (var b in _faSol) if (b.NPC.active && Vector2.Distance(n.Center, b.NPC.Center) < 300f) { exposed = true; break; } }
					if (exposed) exposure++;
					for (int k = 0; k < N; k++)
					{
						var b = _faSol[k]; NPC nn = b.NPC;
						bool alive = nn.active && nn.life > 0 && b.IsAlive;
						if (!alive)
						{
							if (deadAt[k] == 0) { deadAt[k] = t + 1; deaths++; lifeSum += t - born[k]; lives++; Acc(b); }
							else if (t + 1 - deadAt[k] >= 45) { FaSpawnSlot(k, RealSpawnX(k), FaGroundRow(RealSpawnX(k)) * 16f, r => RealGear(r, gear)).RefreshStats(); lastHits[k] = 0; born[k] = t; deadAt[k] = 0; lastPos[k] = _faSol[k].NPC.Center; }
							continue;
						}
						aliveTicks++; if (nn.Bottom.Y < FaGroundRow(nn.Center.X) * 16f - 3 * 16f) aboveTicks++; if (Math.Abs(nn.velocity.X) < 0.3f && Math.Abs(nn.velocity.Y) < 0.3f) stillTicks++;
						if (Math.Abs(nn.velocity.X) < 0.5f && Math.Abs(nn.velocity.Y) < 0.5f) { still5++; string wk = b.StillReason(); if (b.NPC.velocity.Y != 0f) { still5Air++; wk += "+air"; } if (b.ItemAnimation > 0) wk += "+swinging"; why[wk] = why.GetValueOrDefault(wk) + 1; }
						{ Tile ft = Framing.GetTileSafely((int)(nn.Center.X / 16f), (int)((nn.Bottom.Y + 2f) / 16f)); if (ft.HasTile && ft.TileType == TileID.Platforms && nn.Bottom.Y < FaGroundRow(nn.Center.X) * 16f - 16f) rowTicks++; if (nn.velocity.Y != 0f) airTicks++; }
						if (nn.velocity.Y == 0f) { if (inAir[k] && nn.Bottom.Y - lastGrY[k] > 7 * 16f && peakVy[k] >= 8f && t - lastHitTick[k] > 40 && !nn.lavaWet) edgeFalls++; inAir[k] = false; lastGrY[k] = nn.Bottom.Y; peakVy[k] = 0f; }
						else { inAir[k] = true; if (nn.velocity.Y > peakVy[k]) peakVy[k] = nn.velocity.Y; }
						if (b.HitsTaken != lastHits[k]) lastHitTick[k] = t;
						if (Vector2.Distance(nn.Center, lastPos[k]) > 20 * 16f) teleports++;
						lastPos[k] = nn.Center;
						if (nn.lavaWet) lavaTicks++;
						if (b.HitsTaken != lastHits[k])
						{
							int nh = b.HitsTaken - lastHits[k]; lastHits[k] = b.HitsTaken; hitsTotal += nh;
							Rectangle me = nn.Hitbox; me.Inflate(6, 6);
							NPC contact = null;
							for (int i = 0; i < Main.maxNPCs && contact == null; i++) { NPC a = Main.npc[i]; if (a.active && !a.friendly && !a.townNPC && a.damage > 0 && !(a.ModNPC is SoldierNPC) && a.Hitbox.Intersects(me)) contact = a; }
							if (contact != null) { contactHits += nh; string key = "contact:" + contact.GivenOrTypeName + "#" + (contact.ModNPC != null ? contact.ModNPC.Name : contact.type.ToString()); byKind[key] = byKind.GetValueOrDefault(key) + nh; }
							else if (nn.lavaWet) { byKind["lava"] = byKind.GetValueOrDefault("lava") + nh; }
							else
							{
								projHits += nh; Projectile hp = null; Rectangle me2 = nn.Hitbox; me2.Inflate(48, 48);
								for (int pi = 0; pi < Main.maxProjectiles && hp == null; pi++) { Projectile pj = Main.projectile[pi]; if (pj.active && pj.hostile && pj.damage > 0 && pj.Hitbox.Intersects(me2)) hp = pj; }
								if (hp != null && Environment.GetEnvironmentVariable("LEVY_REAL_HITDBG") == "1") Mod.Logger.Info($"HITDBG t={t} {hp.Name} w={hp.width} h={hp.height} dmg={hp.damage} tl={hp.timeLeft} v={hp.velocity} ai0={hp.ai[0]:0.0} ai1={hp.ai[1]:0.0} rot={hp.rotation:0.00} dist={Vector2.Distance(hp.Center, nn.Center):0}  inField={ThreatField.Snapshot().Count} lvl={nn.Center - hp.Center}");
								string key = "proj:" + (b.LastAttackerName ?? "?") + (hp != null ? "/" + hp.Name + "@" + hp.velocity.Length().ToString("0.0", CultureInfo.InvariantCulture) : "/?"); byKind[key] = byKind.GetValueOrDefault(key) + nh;
							}
						}
					}
				},
				Report = () =>
				{
					for (int k = 0; k < N; k++) { var b = _faSol[k]; bool alive = b.NPC.active && b.NPC.life > 0 && b.IsAlive; if (alive) { lifeSum += ticks - born[k]; lives++; Acc(b); } }
					bool bossAlive = bossIdx < 0 || Main.npc[bossIdx].active;
					double surv = lives > 0 ? lifeSum / (double)lives / 60.0 : 0, dpm = deaths * 3600.0 / ticks;
					var sb = new StringBuilder();
					sb.Append($"REALM|boss={rb.Key}|arena={arena}|gear={gear}|order={Environment.GetEnvironmentVariable("LEVY_REAL_ORDER") ?? "freerein"}|auto={(auto ? 1 : 0)}|target={target}|mode={(before ? "before" : "after")}{mode}|seed={seed}|survSec={surv:0.0}|deaths={deaths}|deathsPerMin={dpm:0.00}|bossDmg={bossDmg}|hits={hitsTotal}|contact={contactHits}|proj={projHits}|lavaTicks={lavaTicks}|exposed={exposure}|tele={teleports}|hookCasts={hookCasts}|maneuvers={maneuvers}|bossAlive={bossAlive}|abovePct={(aliveTicks > 0 ? 100.0 * aboveTicks / aliveTicks : 0):0}|climbs={climbs}|rowPct={(aliveTicks > 0 ? 100.0 * rowTicks / aliveTicks : 0):0}|airPct={(aliveTicks > 0 ? 100.0 * airTicks / aliveTicks : 0):0}|airDodge={airDodge}|airScan={airScan}|platEv={pEv}|platNoCand={pNo}|platSepOk={pOk}|airKick={airKick}|airFall={airFall}|airmode={SoldierNPC.AirMode}|platmode={SoldierNPC.PlatMode}|stillPct={(aliveTicks > 0 ? 100.0 * stillTicks / aliveTicks : 0):0}|still5Pct={(aliveTicks > 0 ? 100.0 * still5 / aliveTicks : 0):0.0}|aliveTicks={aliveTicks}|danceLegs={dLegs}|danceHops={dHops}|danceFlights={dFlights}|danceFlips={dFlips}|corners={dCorner}|overs={dOver}|dance={SoldierNPC.DanceMode}|edgeFalls={edgeFalls}|dNear={dNear}|dBase={dBase}|dNoBetter={dNoBetter}|dBusy={dBusy}|dStep={dStep}|dJump={dJump}|dDJ={dDJ}|dDash={dDash}|dHook={dHook}|fieldSeesBoss={fieldSeesBoss}|bossFast={bossFastTicks}|bossPeakV={bossPeakV:0.0}|bossDamageStat={(bossIdx >= 0 ? Main.npc[bossIdx].damage : 0)}|");
					sb.Append($"tgtLevyPct={(tgSamples > 0 ? 100.0 * tgL / tgSamples : 0):0.0}|tgtPlayerPct={(tgSamples > 0 ? 100.0 * tgP / tgSamples : 0):0.0}|tgtNonePct={(tgSamples > 0 ? 100.0 * tgN / tgSamples : 0):0.0}|bTgtLevyPct={(bSamples > 0 ? 100.0 * bgL / bSamples : 0):0.0}|bTgtPlayerPct={(bSamples > 0 ? 100.0 * bgP / bSamples : 0):0.0}|bTgtNonePct={(bSamples > 0 ? 100.0 * bgN / bSamples : 0):0.0}|bossToLevyTiles={(dSamples > 0 ? dLevySum / dSamples : 0):0.0}|bossToCmdTiles={(bSamples > 0 ? dCmdSum / bSamples : 0):0.0}|aiTrans={aiTrans}|newProj={newProj}|fastStarts={fastStarts}|aiHist={string.Join(",", aiHist.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key + ":" + kv.Value))}|src=");
					sb.Append(string.Join(";", byKind.OrderByDescending(kv => kv.Value).Take(10).Select(kv => $"{kv.Key}={kv.Value}")));
					sb.Append("|hookDrawOk=" + _hookDrawOk); Mod.Logger.Info(sb.ToString());
					Mod.Logger.Info($"REALW|boss={rb.Key}|arena={arena}|gear={gear}|mode={(before ? "before" : "after")}{mode}|seed={seed}|aliveTicks={aliveTicks}|still5={still5}|still5Air={still5Air}|why=" + string.Join(";", why.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key}={kv.Value}")));
					return $"{rb.Key}/{arena}/{gear}: surv={surv:0.0}s deaths={deaths} bossDmg={bossDmg}";
				},
			};
		}

		/// <summary>Spawns (or replaces) the soldier in slot k of the squad.</summary>
		private SoldierNPC FaSpawnSlot(int k, float x, float bottomY, Action<SoldierRecord> gear)
		{
			var rec = MakeRecord("Pvt. Field" + k + "_" + Main.GameUpdateCount);
			rec.LoadoutLocked = true;
			gear?.Invoke(rec);
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, new Vector2(x, bottomY));
			if (idx < 0 || !(Main.npc[idx].ModNPC is SoldierNPC b)) throw new Exception("spawn failed");
			b.NPC.Bottom = new Vector2(x, bottomY); b.NPC.velocity = Vector2.Zero;
			if (k < _faSol.Count) _faSol[k] = b; else _faSol.Add(b);
			return b;
		}

		private void FaSpawnMob(RealBoss rb, List<int> mobs, int m)
		{
			int cx = _cx;
			float x = (cx + 28 + m * 9) * 16f; if (m % 2 == 1) x = (cx - 28 - m * 9) * 16f;
			float y = (FaGroundRow(x) - 2 - rb.SpawnUpTiles) * 16f;
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyField"), (int)x, (int)y, rb.Type());
			if (i < 0 || i >= Main.maxNPCs) return;
			NPC n = Main.npc[i]; n.lifeMax = n.life = 1_000_000; n.defense = 0; n.value = 0f; n.timeLeft = 3000;
			if (m < mobs.Count) mobs[m] = i; else mobs.Add(i);
		}
	}

	/// <summary>Real-fight trials: keeps the stand-in commander an active player while NPC AI runs (a dedicated server clears unconnected slots), and stops fight NPCs from despawning.</summary>
	public class RealFightSystem : ModSystem
	{
		public override void PreUpdateNPCs()
		{
			if (!CombatHarness.RealOn) return;
			Player p = Main.player[0]; p.active = true; p.dead = false; p.ghost = false;
		}
	}

	public class RealFightWatch : GlobalNPC
	{
		public override bool CheckActive(NPC npc) => !(CombatHarness.RealOn && !npc.friendly && !npc.townNPC && !(npc.ModNPC is SoldierNPC));
	}
}
