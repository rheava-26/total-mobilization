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
		private bool _faReal;
		internal static bool RealOn;

		private sealed class RealBoss { public string Key, Label; public Func<int> Type; public float Frac = 1f; public bool Mob; public int MobCount; public int SpawnUpTiles; public int SpawnAcross = 40; }

		private static int ModNpc(string mod, string name) { try { return ModLoader.TryGetMod(mod, out Mod m) && m.TryFind(name, out ModNPC n) ? n.Type : -1; } catch { return -1; } }

		private static List<RealBoss> RealBosses() => new()
		{
			new RealBoss { Key = "kingslime", Label = "King Slime", Type = () => NPCID.KingSlime, SpawnUpTiles = 4 },
			new RealBoss { Key = "eoc", Label = "Eye of Cthulhu p1", Type = () => NPCID.EyeofCthulhu, SpawnUpTiles = 18 },
			new RealBoss { Key = "eoc2", Label = "Eye of Cthulhu p2", Type = () => NPCID.EyeofCthulhu, Frac = 0.4f, SpawnUpTiles = 18 },
			new RealBoss { Key = "brain", Label = "Brain of Cthulhu", Type = () => NPCID.BrainofCthulhu, SpawnUpTiles = 14 },
			new RealBoss { Key = "eow", Label = "Eater of Worlds", Type = () => NPCID.EaterofWorldsHead, SpawnUpTiles = 10 },
			new RealBoss { Key = "queenbee", Label = "Queen Bee", Type = () => NPCID.QueenBee, SpawnUpTiles = 14 },
			new RealBoss { Key = "skeletron", Label = "Skeletron", Type = () => NPCID.SkeletronHead, SpawnUpTiles = 14 },
			new RealBoss { Key = "skeletron2", Label = "Skeletron spinning", Type = () => NPCID.SkeletronHead, Frac = 0.3f, SpawnUpTiles = 14 },
			new RealBoss { Key = "deerclops", Label = "Deerclops", Type = () => NPCID.Deerclops, SpawnUpTiles = 4 },
			new RealBoss { Key = "desertscourge", Label = "Desert Scourge", Type = () => ModNpc("CalamityMod", "DesertScourgeHead"), SpawnUpTiles = 3, SpawnAcross = 50 },
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
				if (hills) { double v = 5.5 * Math.Sin(x * 0.11) + 3.5 * Math.Sin(x * 0.27 + 1.3) + 2 * Math.Sin(x * 0.5); h = Math.Max(0, (int)Math.Round(v + 6)); }
				if (h > 0) FaFill(x, fy - h, x, fy - 1, true, TileID.Dirt);
				_faGround[x - (cx - 108)] = fy - h;
			}
			void Plat(int x0, int x1, int y)
			{
				for (int x = x0; x <= x1; x++) { Tile t = Main.tile[x, y]; t.HasTile = true; t.TileType = TileID.Platforms; t.WallType = 0; t.TileFrameX = 0; t.TileFrameY = 0; t.BlockType = Terraria.ID.BlockType.Solid; t.Slope = Terraria.ID.SlopeType.Solid; }
			}
			if (kind == "platform")
			{
				for (int r = 1; r <= 5; r++) { int y = fy - 6 * r; int off = (r % 2) * 14; Plat(cx - 60 + off, cx - 8 + off / 2, y); Plat(cx + 4 + off / 2, cx + 66 - off, y); }
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
			r.Order = SoldierOrder.Hold; r.HoldFire = false;
			r.Gear.Weapon = RItem(ItemID.Minishark); r.Gear.Ammo = RItem(ItemID.MusketBall, 999);
			r.Gear.Head = RItem(ItemID.GoldHelmet); r.Gear.Body = RItem(ItemID.GoldChainmail); r.Gear.Legs = RItem(ItemID.GoldGreaves);
			int a = 0;
			if (gear == "all") r.Grade = Grade.Champion;
			if (gear == "hook" || gear == "all") { r.Gear.Hook = RItem(ItemID.GrapplingHook); r.Gear.SetLocked(SoldierGear.SlotHook, true); }
			if (gear == "bottle" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.CloudinaBottle);
			if (gear == "hermes" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.HermesBoots);
			if (gear == "dash" || gear == "all") r.Gear.Accessories[a++] = RItem(ItemID.EoCShield);
		}

		private int RealN => int.TryParse(Environment.GetEnvironmentVariable("LEVY_REAL_N"), out int en) ? en : 4;
		private float RealSpawnX(int k) => _cx * 16f + (k - (RealN - 1) / 2f) * 7f * 16f + 8f;

		private FaTrial FaReal(RealBoss rb, int mode, string target, string arena, string gear, int seed, int ticks)
		{
			int N = RealN;
			bool before = Environment.GetEnvironmentVariable("LEVY_REAL_BEFORE") == "1";
			var lastHits = new int[N]; var born = new int[N]; var deadAt = new int[N]; var lastPos = new Vector2[N];
			int deaths = 0; long lifeSum = 0; int lives = 0, contactHits = 0, projHits = 0, lavaTicks = 0, hitsTotal = 0, exposure = 0, teleports = 0, hookCasts = 0, maneuvers = 0;
			long bossDmg = 0; int prevBossLife = -1; int bossIdx = -1; bool bossGone = false;
			var mobs = new List<int>(); var byKind = new Dictionary<string, int>();
			return new FaTrial
			{
				Name = "REAL", Ticks = ticks,
				Setup = () =>
				{
					_faReal = true; RealOn = true;
					Main.rand = new UnifiedRandom(1000 + seed * 7919);
					SoldierNPC.DodgeMode = mode; SoldierNPC.FallDamageOverride = 0; SoldierNPC.MobilityMode = before ? 0 : 1;
					var cfg = LevyConfig.Get; cfg.EnemiesTargetSoldiers = true; cfg.BossesTargetSoldiers = target == "army";
					try { global::Levy.Common.Targeting.EnemyTargeting.RefreshConfig(); } catch { }
					RealUnlockBosses();
					FaBuildArena(arena);
					int cx = _cx;
					float cmdX = (cx - 30) * 16f + 8f; if (target == "army") cmdX = (cx - 104) * 16f;
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
						}
					}
					else if (t % 90 == 0) { for (int m = 0; m < mobs.Count; m++) if (!Main.npc[mobs[m]].active || Main.npc[mobs[m]].type != rb.Type()) FaSpawnMob(rb, mobs, m); }
					bool exposed = false;
					for (int i = 0; i < Main.maxNPCs && !exposed; i++) { NPC n = Main.npc[i]; if (!n.active || n.friendly || n.townNPC || n.damage <= 0 || n.ModNPC is SoldierNPC) continue; foreach (var b in _faSol) if (b.NPC.active && Vector2.Distance(n.Center, b.NPC.Center) < 300f) { exposed = true; break; } }
					if (exposed) exposure++;
					for (int k = 0; k < N; k++)
					{
						var b = _faSol[k]; NPC nn = b.NPC;
						bool alive = nn.active && nn.life > 0 && b.IsAlive;
						if (!alive)
						{
							if (deadAt[k] == 0) { deadAt[k] = t + 1; deaths++; lifeSum += t - born[k]; lives++; hookCasts += b.HkCasts; maneuvers += b.DodgeManeuvers; }
							else if (t + 1 - deadAt[k] >= 45) { FaSpawnSlot(k, RealSpawnX(k), FaGroundRow(RealSpawnX(k)) * 16f, r => RealGear(r, gear)).RefreshStats(); lastHits[k] = 0; born[k] = t; deadAt[k] = 0; lastPos[k] = _faSol[k].NPC.Center; }
							continue;
						}
						if (Vector2.Distance(nn.Center, lastPos[k]) > 20 * 16f) teleports++;
						lastPos[k] = nn.Center;
						if (nn.lavaWet) lavaTicks++;
						if (b.HitsTaken != lastHits[k])
						{
							int nh = b.HitsTaken - lastHits[k]; lastHits[k] = b.HitsTaken; hitsTotal += nh;
							Rectangle me = nn.Hitbox; me.Inflate(6, 6);
							NPC contact = null;
							for (int i = 0; i < Main.maxNPCs && contact == null; i++) { NPC a = Main.npc[i]; if (a.active && !a.friendly && !a.townNPC && a.damage > 0 && !(a.ModNPC is SoldierNPC) && a.Hitbox.Intersects(me)) contact = a; }
							if (contact != null) { contactHits += nh; string key = "contact:" + contact.GivenOrTypeName; byKind[key] = byKind.GetValueOrDefault(key) + nh; }
							else if (nn.lavaWet) { byKind["lava"] = byKind.GetValueOrDefault("lava") + nh; }
							else { projHits += nh; string key = "proj:" + (b.LastAttackerName ?? "?"); byKind[key] = byKind.GetValueOrDefault(key) + nh; }
						}
					}
				},
				Report = () =>
				{
					for (int k = 0; k < N; k++) { var b = _faSol[k]; bool alive = b.NPC.active && b.NPC.life > 0 && b.IsAlive; if (alive) { lifeSum += ticks - born[k]; lives++; hookCasts += b.HkCasts; maneuvers += b.DodgeManeuvers; } }
					bool bossAlive = bossIdx < 0 || Main.npc[bossIdx].active;
					double surv = lives > 0 ? lifeSum / (double)lives / 60.0 : 0, dpm = deaths * 3600.0 / ticks;
					var sb = new StringBuilder();
					sb.Append($"REALM|boss={rb.Key}|arena={arena}|gear={gear}|mode={(before ? "before" : "after")}{mode}|seed={seed}|survSec={surv:0.0}|deaths={deaths}|deathsPerMin={dpm:0.00}|bossDmg={bossDmg}|hits={hitsTotal}|contact={contactHits}|proj={projHits}|lavaTicks={lavaTicks}|exposed={exposure}|tele={teleports}|hookCasts={hookCasts}|maneuvers={maneuvers}|bossAlive={bossAlive}|src=");
					sb.Append(string.Join(";", byKind.OrderByDescending(kv => kv.Value).Take(5).Select(kv => $"{kv.Key}={kv.Value}")));
					Mod.Logger.Info(sb.ToString());
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
