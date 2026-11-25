using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of a levy's facing, logged as "[FacingWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the whip check).
	/// A levy's shots are owned by the commander's player slot, so any code that reads the OWNER's direction (or place) for a lash, a swing, a beam or a held weapon's shot must find the LEVY's, never the commander's.
	/// The check puts one levy in a flat arena with a pinned, tough enemy a few tiles in front of it (the levy faces the way the enemy is) and the commander well away from both, then measures the same weapon with the
	/// commander facing right and facing left, for a levy facing right and for the mirror image (a levy facing left). Per weapon and per levy facing the two runs share one random seed. Measured: the damage, the hits,
	/// the share of hits whose knockback pushes the enemy away from the levy, where the shot was when it hit (forward and vertical offset from the levy), and the share of shots that left the levy toward the enemy.
	/// The commander's facing must make no difference beyond the noise: the commander is not the one swinging.
	/// LEVY_FACING_ONLY=1 runs this check alone, LEVY_FACING_FILTER=name narrows the weapons, LEVY_FACING_TICKS=n sets the window.
	/// Throwaway soldiers only; nothing is saved.
	/// </summary>
	public class FacingWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static NPC _target; private static Vector2 _targetAt, _cmdAt, _levyAt;
		private static int _cmdDir = 1, _levyDir = 1;
		private static SoldierNPC _levy;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "FacingTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_FACINGWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		// the stand-in commander (slot 0, the single-player owner) is kept active, local while NPCs and projectiles update, and faces the way the scene says at every point the game could read it
		public override void PostUpdatePlayers() { if (_running) PinCommander(); }
		public override void PreUpdateNPCs() { if (!_running) return; PinCommander(); PinTarget(); PinLevy(); Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; PinCommander(); Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }
		private static int _savedMy;

		private static void PinCommander()
		{
			Player p = Main.player[0]; p.active = true; p.dead = false; p.name = Commander; p.direction = _cmdDir;
		}

		/// <summary>Runs now when this boot armed it (the whip world check calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			_savedMy = Main.myPlayer;
			On_Projectile.Update += KeepOwnerActive;
			On_NPC.StrikeNPC_HitInfo_bool_bool += StrikeProbe;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				On_Projectile.Update -= KeepOwnerActive; On_NPC.StrikeNPC_HitInfo_bool_bool -= StrikeProbe;
				_running = false; Tracking = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.myPlayer = _savedMy; Cleanup();
			}
			_mod.Logger.Info($"[FacingWorld] took {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1.0 / System.Diagnostics.Stopwatch.Frequency:0.0}s");
			_mod.Logger.Info($"[FacingWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void KeepOwnerActive(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (_running && self.owner == 0 && Main.player[0] != null) { Main.player[0].active = true; Main.player[0].dead = false; }
			orig(self, i);
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[FacingWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[FacingWorld] " + s);

		// ------------------------------------------------------------------------------------------------------------ the probe
		/// <summary>One scene's measurements.</summary>
		private sealed class Scene
		{
			public int Hits, Strikes, AwayHits, ProjHits, Shots, ForwardShots, SeenLevyDir; public long Dmg;
			public double FwdSum, UpSum; public int Pos;
			public int CmdDirEnd, BornFacing, BornFacingBad, BornStale;
			/// <summary>Per hook kind (Kinds): how often a levy's shot or strike was handled, how often the owner it reads stood facing the other way than the levy, and how often it stood away from the levy.</summary>
			public readonly int[] Seen = new int[Kinds.Length], DirBad = new int[Kinds.Length], PosBad = new int[Kinds.Length];
			public readonly Dictionary<string, int> Types = new();
			/// <summary>The births behind BornFacingBad, by projectile: the shots that were born with a facing neither the way they fly nor the way the levy faces.</summary>
			public readonly Dictionary<string, int> BornBadTypes = new(), BornTypes = new(), BornStaleTypes = new();
			public double Fwd => Pos > 0 ? FwdSum / Pos : 0; public double Up => Pos > 0 ? UpSum / Pos : 0;
			public double AwayShare => Strikes > 0 ? (double)AwayHits / Strikes : 0; public double ForwardShare => Shots > 0 ? (double)ForwardShots / Shots : 0;
		}
		private static Scene _sc; public static bool Tracking;
		/// <summary>The points the game hands a levy's shot (or strike) to code that reads "the owner": the spawn, the AI before and after, the hit test, the hit modifiers, the hit, the death, and the strike on the enemy.</summary>
		public static readonly string[] Kinds = { "spawn", "preAI", "postAI", "canHit", "modifyHit", "onHit", "kill", "strike" };
		public const int KSpawn = 0, KPreAI = 1, KPostAI = 2, KCanHit = 3, KModifyHit = 4, KOnHit = 5, KKill = 6, KStrike = 7;

		/// <summary>Called from a hook with the player slot the game would read as the owner: counts it, and counts it again when that owner is not facing, and standing, as the levy is.</summary>
		internal static void SeeOwner(int kind, int ownerSlot)
		{
			if (!Tracking || _sc == null || _levy == null || !_levy.NPC.active || ownerSlot < 0 || ownerSlot > 255) return;
			Player o = Main.player[ownerSlot]; if (o == null) return;
			int want = _levy.NPC.direction == 0 ? 1 : _levy.NPC.direction;
			_sc.Seen[kind]++;
			if (ReferenceEquals(o, _levy.Dummy)) return; // the levy's own stand-in is the levy by construction (it faces where the levy faced when it was last synced: a tick behind at worst); what is judged is any OTHER player the code could find in the owner's slot: the commander, or the empty player of slot 255
			if (o.direction != want)
			{
				_sc.DirBad[kind]++;
				if (Debug && (kind == KPreAI || kind == KSpawn) && _dbgOwner++ < 6) Note($"  dbg owner wrong way: kind={Kinds[kind]} slot={ownerSlot} ownerDir={o.direction} levyDir={want} isDummy={(global::Levy.Common.Owner.SoldierOwner.IsDummy(o))} ownerIsLevyDummy={ReferenceEquals(o, _levy.Dummy)} swapped={EnemyTargeting.IsSwapped} dummyDir={_levy.Dummy?.direction} cmdDir={Main.player[0].direction} myPlayer={Main.myPlayer} :: " + (kind == KPreAI || kind == KSpawn ? Frames() : ""));
			}
			if (Vector2.Distance(o.Center, _levy.NPC.Center) > 40f) _sc.PosBad[kind]++;
		}

		/// <summary>True for a projectile that is this levy's: tagged to it, or (at its spawn, when no tag is on yet) made by the levy's body, its stand-in player, or one of its own shots.</summary>
		internal static bool IsLevys(Projectile p, bool spawning, IEntitySource source = null)
		{
			if (!Tracking || _sc == null || _levy == null || !_levy.NPC.active || p.hostile) return false;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex == _levy.NPC.whoAmI) return true;
			if (!spawning || source is not EntitySource_Parent par) return false;
			return par.Entity switch
			{
				NPC n => n.whoAmI == _levy.NPC.whoAmI,
				Player pl => ReferenceEquals(pl, _levy.Dummy),
				Projectile pp => pp.TryGetGlobalProjectile(out SoldierProjectileTag pt) && pt.SoldierIndex == _levy.NPC.whoAmI,
				_ => false,
			};
		}
		private static readonly bool Debug = Environment.GetEnvironmentVariable("LEVY_FACING_DEBUG") == "1"; private static int _dbgStacks, _dbgOwner, _dbgBorn; private static readonly int[] _slotDir = new int[Main.maxProjectiles + 1], _slotType = new int[Main.maxProjectiles + 1];

		private static int StrikeProbe(On_NPC.orig_StrikeNPC_HitInfo_bool_bool orig, NPC self, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
		{
			int done = orig(self, hit, fromNet, noPlayerInteraction);
			if (Tracking && _sc != null && self == _target && done > 0)
			{
				_sc.Strikes++; _sc.Dmg += done;
				if (hit.HitDirection == _levyDir) _sc.AwayHits++;
				SeeOwner(KStrike, 0);
				if (Main.player[0].direction == _levyDir) _sc.SeenLevyDir++;
				else if (Debug && _dbgStacks++ < 2)
				{
					Note($"  dbg strike outside any lend: cmdDir={Main.player[0].direction} levyDir={_levyDir} dmg={done} hitDir={hit.HitDirection} :: " + Frames());
				}
			}
			return done;
		}

		private static readonly HashSet<long> _seen = new();
		/// <summary>Every shot this levy has made, seen once, after the tick that made it (the soldier tag is put on after the spawn): its type, and whether it left toward the enemy.</summary>
		private static void SeeShots()
		{
			if (!Tracking || _sc == null || _levy == null || !_levy.NPC.active) return;
			int idx = _levy.NPC.whoAmI;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				int prevDir = _slotDir[i], prevType = _slotType[i]; _slotDir[i] = p.direction; _slotType[i] = p.type; // what the slot held at the last look (a freed slot keeps its direction: vanilla does not reset it for the next projectile)
				if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex != idx) continue;
				if (!_seen.Add(((long)tag.SpawnTick << 12) | (uint)i)) continue;
				string key = p.ModProjectile != null ? p.ModProjectile.Name : ProjectileID.Search.GetName(p.type);
				_sc.Types[key] = _sc.Types.TryGetValue(key, out int n) ? n + 1 : 1;
				if (Math.Abs(p.velocity.X) > 0.5f) { _sc.Shots++; if (p.velocity.X * _levyDir > 0f) _sc.ForwardShots++; }
				// the facing a shot is born with (Projectile.direction, which the game and mods copy from the owner when it is made): it must be the way the levy faces, and the way it flies.
				// Vanilla never resets Projectile.direction for a new projectile (NewProjectile and SetDefaults leave it): a shot whose mod does not set it carries what the slot's last projectile had, a thrown blade that came
				// back, a spark. A birth whose direction is just what its slot held a tick ago says nothing about the owner it was made by, so it is counted apart (BornStale) and not judged.
				if (p.direction != 0 && Math.Abs(p.velocity.X) > 0.5f)
				{
					_sc.BornFacing++; _sc.BornTypes[key] = _sc.BornTypes.GetValueOrDefault(key) + 1;
					if (p.direction != (p.velocity.X > 0f ? 1 : -1) && p.direction != _levyDir)
					{
						if (p.direction == prevDir) { _sc.BornStale++; _sc.BornStaleTypes[key] = _sc.BornStaleTypes.GetValueOrDefault(key) + 1; }
						else
						{
							_sc.BornFacingBad++; _sc.BornBadTypes[key] = _sc.BornBadTypes.GetValueOrDefault(key) + 1;
							if (Debug && _dbgBorn++ < 40) Note($"  dbg bad birth {key}: direction={p.direction} vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) levyDir={_levyDir} cmdDir={Main.player[0].direction} slot={i} slotHeldBefore={(prevType >= ProjectileID.Count ? ProjectileLoader.GetProjectile(prevType)?.Name : ProjectileID.Search.GetName(prevType))} dir {prevDir}");
						}
					}
				}
			}
		}

		internal static void OnHit(Projectile p, NPC target)
		{
			if (!Tracking || _sc == null || target != _target || _levy == null || !_levy.NPC.active) return;
			Vector2 rel = p.Center - _levy.NPC.Center;
			_sc.ProjHits++; _sc.FwdSum += rel.X * _levyDir; _sc.UpSum += rel.Y; _sc.Pos++;
		}

		// ------------------------------------------------------------------------------------------------------------ the arena
		private static void Cleanup()
		{
			try { Clear(); } catch (Exception) { }
		}

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++)
			{
				PinCommander(); PinTarget(); PinLevy(); ClearStrangers();
				WorldStep.Update();
				KeepSceneAlive(); SeeShots();
			}
		}

		private static void KeepSceneAlive()
		{
			if (_target != null && _target.active) { _target.life = _target.lifeMax; }
		}

		private static void ClearStrangers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (WorldStep.IsNaturalHostile(n) && n != _target && !(n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) n.active = false;
			}
		}

		private static void PinTarget()
		{
			if (_target == null || !_target.active) return;
			_target.active = true; _target.timeLeft = 9999; _target.Bottom = _targetAt; _target.velocity = Vector2.Zero; _target.life = _target.lifeMax; _target.dontTakeDamage = false;
			for (int i = 0; i < _target.immune.Length; i++) _target.immune[i] = 0;
		}

		private static void PinLevy()
		{
			if (_levy == null || !_levy.NPC.active) return;
			NPC n = _levy.NPC; n.Bottom = _levyAt; n.velocity = Vector2.Zero; n.direction = _levyDir; n.spriteDirection = _levyDir;
			Player c = Main.player[0];
			c.position = _cmdAt - new Vector2(c.width / 2f, c.height / 2f); c.velocity = Vector2.Zero; c.statLife = c.statLifeMax2 = Math.Max(c.statLifeMax2, 500);
		}

		private static void Arena()
		{
			for (int i = Cx - 90; i <= Cx + 90; i++)
			{
				for (int j = FloorY - 80; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Clear()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
			}
			if (_target != null) _target.active = false;
			_target = null; _levy = null;
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			EnemyTargeting.EnsureSlotRestored();
		}

		private static void SpawnEnemy(float pxAway)
		{
			_targetAt = new Vector2(_levyAt.X + _levyDir * pxAway, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("FacingWorldCheck"), (int)_targetAt.X, (int)_targetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) { _target = null; return; }
			_target = Main.npc[ti];
			_target.lifeMax = 1_000_000; _target.life = 1_000_000; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 64; _target.height = 96;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.Bottom = _targetAt;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position;
		}

		private static SoldierNPC SpawnLevy(Item weapon, Item ammo)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Facing",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (!rec.Gear.CanHold(SoldierGear.SlotWeapon, weapon)) return null;
			rec.Gear.Weapon = weapon; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (ammo != null) rec.Gear.Ammo = ammo;
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, _levyAt);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = _levyAt; rec.HoldPos = Main.npc[body].Center; // a levy on Hold fights around its post: without one the post is the world's corner
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		// ------------------------------------------------------------------------------------------------------------ one scene
		private sealed class Weapon
		{
			public string Label; public Func<Item> Make; public Func<Item> Ammo; public float Dist;
			/// <summary>A weapon of another mod that this arena may not suit (the levy cannot hold it, or it needs a boss, a stealth bar, a world event): when it makes no hits at all in either run it is reported as skipped, never failed.</summary>
			public bool Optional;
			public Weapon(string label, Func<Item> make, float dist, Func<Item> ammo = null) { Label = label; Make = make; Dist = dist; Ammo = ammo; }
		}

		private static Item Vanilla(int id) { Item it = new Item(); it.SetDefaults(id); return it; }
		private static Func<Item> Mod_(string mod, string name) => () => ModContent.TryFind(mod, name, out ModItem mi) ? Vanilla(mi.Type) : null;

		private static int Ticks => int.TryParse(Environment.GetEnvironmentVariable("LEVY_FACING_TICKS"), out int t) && t > 0 ? t : 600;

		private static Scene Measure(Weapon w, int levyDir, int cmdDir, int ticks)
		{
			Clear(); Arena();
			_levyDir = levyDir; _cmdDir = cmdDir;
			_levyAt = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			_cmdAt = new Vector2(_levyAt.X - levyDir * 32f * 16f, FloorY * 16f - 28f); // 32 tiles behind the levy: nothing a levy does reaches her
			var sc = new Scene(); _seen.Clear(); _dbgStacks = 0; _dbgOwner = 0;
			SpawnEnemy(w.Dist);
			if (_target == null) return null;
			Item item = w.Make(); if (item == null) return null;
			_levy = SpawnLevy(item, w.Ammo?.Invoke());
			if (_levy == null) return null;
			PinCommander(); PinLevy();
			Main.rand = new Terraria.Utilities.UnifiedRandom(12345);
			Step(4);
			_sc = sc; Tracking = true;
			for (int t = 0; t < ticks; t++)
			{
				Step(1);
				if (Debug && t % 70 == 0 && _levy != null) Note($"  dbg t={t} {w.Label} levyDir={levyDir} cmdDir={cmdDir}: state={_levy.State} target={_levy.TargetIndex}/{(_target?.whoAmI ?? -1)} family={_levy.Weapon?.Family} cooldown={_levy.AttackCooldown} anim={_levy.ItemAnimation} life={_levy.NPC.life} at={_levy.NPC.Center - _levyAt} enemyAt={(_target != null ? (_target.Center.X - _levy.NPC.Center.X) : 0)} hits={sc.Strikes}");
			}
			Tracking = false; sc.CmdDirEnd = Main.player[0].direction;
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			if (_levy != null) _levy.NPC.active = false;
			Step(3);
			_sc = null;
			return sc;
		}

		internal static bool IsTarget(NPC n) => _target != null && n == _target;

		/// <summary>The method names on the stack right now, innermost first (a debugging aid: LEVY_FACING_DEBUG=1).</summary>
		private static string Frames()
		{
			var names = Environment.StackTrace.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("at ")).Select(l => { int p = l.IndexOf('('); string m = p > 0 ? l.Substring(3, p - 3) : l.Substring(3); int d = m.LastIndexOf('.'); int d2 = d > 0 ? m.LastIndexOf('.', d - 1) : -1; return d2 >= 0 ? m.Substring(d2 + 1) : m; }).Where(n => !n.Contains("Frames") && !n.Contains("SeeOwner") && !n.Contains("SyncProxy") && !n.Contains("StrikeProbe") && !n.Contains("FacingProbeGlobal")).Take(26);
			return string.Join(" < ", names);
		}

		private static string Fmt(Scene s) => s == null ? "none" :
			$"bornFacing {s.BornFacingBad}/{s.BornFacing} (stale slot direction {s.BornStale}) [{string.Join(",", s.BornTypes.OrderBy(k => k.Key).Select(k => k.Key + " " + s.BornBadTypes.GetValueOrDefault(k.Key) + "+" + s.BornStaleTypes.GetValueOrDefault(k.Key) + "/" + k.Value))}] dmg={s.Dmg} strikes={s.Strikes} projHits={s.ProjHits} away={s.AwayShare:0.00} shots={s.Shots} forward={s.ForwardShare:0.00} hitAt(fwd={s.Fwd:0.0},up={s.Up:0.0})px cmdSawLevyDir={s.SeenLevyDir}/{s.Strikes} proj=[{string.Join(",", s.Types.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value))}] ownerWrongWay/seen=[{string.Join(" ", Kinds.Select((k, i) => s.Seen[i] > 0 ? $"{k}:{s.DirBad[i]}/{s.Seen[i]}" : null).Where(x => x != null))}] ownerAway/seen=[{string.Join(" ", Kinds.Select((k, i) => s.Seen[i] > 0 ? $"{k}:{s.PosBad[i]}/{s.Seen[i]}" : null).Where(x => x != null))}]";

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Main.player[0].active = true; Main.player[0].statLifeMax = Main.player[0].statLifeMax2 = 500; Main.player[0].statLife = 500;
			Arena();
			string filter = Environment.GetEnvironmentVariable("LEVY_FACING_FILTER");
			int ticks = Ticks;
			string sweep = Environment.GetEnvironmentVariable("LEVY_FACING_SWEEP");
			if (!string.IsNullOrEmpty(sweep)) { RunSweep(sweep, filter, int.TryParse(Environment.GetEnvironmentVariable("LEVY_FACING_SWEEPTICKS"), out int swt) && swt > 0 ? swt : Math.Min(ticks, 360)); return; }
			var weapons = new List<Weapon>
			{
				new Weapon("Leather Whip (whip)", () => Vanilla(ItemID.BlandWhip), 110f),
				new Weapon("Thorn Whip (whip, long)", () => Vanilla(ItemID.ThornWhip), 150f),
				new Weapon("Iron Broadsword (swing)", () => Vanilla(ItemID.IronBroadsword), 64f),
				new Weapon("Enchanted Sword (swing and beam)", () => Vanilla(ItemID.EnchantedSword), 110f),
				new Weapon("Spear (thrust)", () => Vanilla(ItemID.Spear), 100f),
				new Weapon("Blue Moon (flail)", () => Vanilla(ItemID.BlueMoon), 100f),
				new Weapon("Cascade (yoyo)", () => Vanilla(ItemID.Cascade), 100f),
				new Weapon("Enchanted Boomerang (boomerang)", () => Vanilla(ItemID.EnchantedBoomerang), 130f),
				new Weapon("Last Prism (held weapon)", () => Vanilla(ItemID.LastPrism), 140f),
				new Weapon("Laser Machinegun (held weapon)", () => Vanilla(ItemID.LaserMachinegun), 140f),
				new Weapon("Phantasm (held bow)", () => Vanilla(ItemID.Phantasm), 140f, () => { Item a = Vanilla(ItemID.WoodenArrow); a.stack = 999; return a; }),
				new Weapon("Musket (gun)", () => Vanilla(ItemID.Musket), 140f, () => { Item a = Vanilla(ItemID.MusketBall); a.stack = 999; return a; }),
				new Weapon("Amethyst Staff (magic)", () => Vanilla(ItemID.AmethystStaff), 140f),
			};
			// Calamity and Thorium weapons, when the mods are loaded: one thrown rogue weapon, one held blade, one spinning thrown melee
			foreach (var (mod, name, label, dist) in new[] { ("CalamityMod", "Brimblade", "Brimblade (Calamity rogue)", 120f), ("CalamityMod", "Celestus", "Celestus (Calamity rogue)", 120f), ("CalamityMod", "MoltenAmputator", "Molten Amputator (Calamity rogue)", 120f), ("CalamityMod", "StormfrontRazor", "Stormfront Razor (Calamity rogue)", 120f), ("CalamityMod", "Terratomere", "Terratomere (Calamity melee)", 90f), ("CalamityMod", "BrimstoneSword", "Brimstone Sword (Calamity melee)", 90f), ("CalamityMod", "AquamarineStaff", "Aquamarine Staff (Calamity magic)", 140f), ("CalamityMod", "Murasama", "Murasama (Calamity melee)", 90f), ("CalamityMod", "ArkoftheElements", "Ark of the Elements (Calamity melee)", 90f), ("CalamityMod", "ArkoftheCosmos", "Ark of the Cosmos (Calamity melee)", 90f), ("CalamityMod", "TrueBiomeBlade", "True Biome Blade (Calamity melee)", 90f), ("CalamityMod", "BrokenBiomeBlade", "Broken Biome Blade (Calamity melee)", 90f), ("CalamityMod", "SkytideDragoon", "Skytide Dragoon (Calamity melee)", 90f), ("CalamityMod", "Basher", "Basher (Calamity melee)", 90f), ("CalamityMod", "GrandDad", "Grand Dad (Calamity melee)", 90f), ("CalamityMod", "Respiteblock", "Respiteblock (Calamity melee)", 90f), ("CalamityMod", "PhotonRipper", "Photon Ripper (Calamity melee)", 90f), ("CalamityMod", "DevilsDevastation", "Devil's Devastation (Calamity melee)", 90f), ("CalamityMod", "TaintedBlade", "Tainted Blade (Calamity melee)", 90f) })
				if (ModContent.TryFind(mod, name, out ModItem _)) weapons.Add(new Weapon(label, Mod_(mod, name), dist) { Optional = true });
			Note($"{weapons.Count} weapons, {ticks} ticks a scene; commander {Commander} faces right then left, the levy right then left");
			foreach (Weapon w in weapons)
			{
				if (!Matches(filter, w.Label)) continue;
				foreach (int levyDir in new[] { 1, -1 })
				{
					Scene a = null, b = null;
					try { a = Measure(w, levyDir, 1, ticks); } catch (Exception e) { Check($"{w.Label}: the scene ran ({e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()})", false); }
					finally { Clear(); }
					try { b = Measure(w, levyDir, -1, ticks); } catch (Exception e) { Check($"{w.Label}: the mirrored scene ran ({e.GetType().Name}: {e.Message})", false); }
					finally { Clear(); }
					string side = levyDir > 0 ? "levy faces right" : "levy faces left";
					if (a == null || b == null) { if (w.Optional) Note($"   SKIPPED {w.Label}, {side}: the levy cannot be given it here"); else Check($"{w.Label}, {side}: both scenes could be set up", false); continue; }
					Note($"== {w.Label}, {side}");
					Note("   commander right: " + Fmt(a)); Note("   commander left : " + Fmt(b));
					if (w.Optional && (a.Strikes == 0 || b.Strikes == 0)) { Note($"   SKIPPED {w.Label}, {side}: it made {a.Strikes} and {b.Strikes} hits on the pinned enemy (not a weapon this arena can show)"); continue; }
					Judge(w.Label + ", " + side, a, b);
				}
			}
		}

		/// <summary>
		/// LAB (LEVY_FACING_SWEEP=ModName): every damaging weapon of one mod the army can use, in the same four scenes, one line each: the damage and hits with the commander facing right and left, and how often the owner a hook read was
		/// the wrong way. It finds the weapons whose OUTCOME depends on the commander's facing (the ratio of the two damages well under 1). Not part of the ship chain.
		/// </summary>
		private static void RunSweep(string modName, string filter, int ticks)
		{
			var items = new List<int>();
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item it) || it == null || it.IsAir || it.damage <= 0 || it.consumable || it.useStyle == 0 || it.accessory || it.pick > 0 || it.axe > 0 || it.hammer > 0) continue;
				string owner = it.ModItem?.Mod.Name ?? "Terraria";
				if (!string.Equals(owner, modName, StringComparison.OrdinalIgnoreCase)) continue;
				if (!Matches(filter, it.Name + "|" + it.ModItem?.Name)) continue;
				WeaponProfile prof; try { prof = WeaponClassifier.GetProfile(it); } catch (Exception) { continue; }
				if (prof == null || prof.Family == WeaponFamily.None || prof.Family == WeaponFamily.Unsupported || prof.Family == WeaponFamily.Summon || prof.Family == WeaponFamily.Sentry) continue;
				items.Add(t);
			}
			Note($"SWEEP {modName}: {items.Count} weapons, {ticks} ticks a scene");
			int flagged = 0, judged = 0, ownerBadWeapons = 0;
			foreach (int t in items)
			{
				Item sample = Vanilla(t); WeaponProfile prof = WeaponClassifier.GetProfile(sample);
				bool melee = prof.Family == WeaponFamily.Broadsword || prof.Family == WeaponFamily.Shortsword || prof.Family == WeaponFamily.Spear || prof.Family == WeaponFamily.Flail || prof.Family == WeaponFamily.Yoyo;
				int useAmmo = prof.UseAmmo; int tt = t;
				var w = new Weapon(sample.Name + " (" + prof.Family + ")", () => Vanilla(tt), melee ? 90f : 130f, useAmmo > 0 ? () => AmmoFor(useAmmo) : null) { Optional = true };
				var res = new Scene[2, 2]; bool ok = true;
				foreach (int levyDir in new[] { 1, -1 })
					foreach (int cmd in CmdOrder)
					{
						try { res[levyDir > 0 ? 0 : 1, cmd > 0 ? 0 : 1] = Measure(w, levyDir, cmd, ticks); } catch (Exception) { ok = false; }
						finally { Clear(); }
					}
				if (!ok) { Note($"SWEEP {w.Label}: threw"); continue; }
				var line = new System.Text.StringBuilder(); bool bad = false, ownerBad = false, any = false;
				for (int l = 0; l < 2; l++)
				{
					Scene a = res[l, 0], b = res[l, 1];
					if (a == null || b == null) { line.Append($" [{(l == 0 ? "R" : "L")} setup failed]"); continue; }
					if (a.Strikes + b.Strikes == 0) { line.Append($" [{(l == 0 ? "R" : "L")} no hits]"); continue; }
					any = true;
					double lo = Math.Min(a.Dmg, b.Dmg), hi = Math.Max(1, Math.Max(a.Dmg, b.Dmg));
					int wrong = 0; for (int k = 0; k < Kinds.Length; k++) wrong += a.DirBad[k] + b.DirBad[k];
					int born = Math.Abs(a.BornFacingBad - b.BornFacingBad); wrong += born > 4 ? born : 0;
					if (wrong > 0) ownerBad = true;
					double ratio = lo / hi; int nh = Math.Min(a.Strikes, b.Strikes);
					if (nh >= 8 && ratio < 0.6) bad = true;
					line.Append($" [{(l == 0 ? "R" : "L")} dmg {a.Dmg}/{b.Dmg} ratio {ratio:0.00} hits {a.Strikes}/{b.Strikes} fwd {a.Fwd:0}/{b.Fwd:0} ownerWrong {wrong} (born {born})]");
				}
				if (any) judged++; if (bad) flagged++; if (ownerBad) ownerBadWeapons++;
				Note($"SWEEP {w.Label}{(bad ? " ** OUTCOME DIFFERS **" : "")}{(ownerBad ? " (owner read wrong)" : "")}:{line}");
				if (Debug && bad) for (int l = 0; l < 2; l++) for (int c = 0; c < 2; c++) Note($"   dbg {w.Label} levy {(l == 0 ? "R" : "L")} cmd {(c == 0 ? "R" : "L")}: {Fmt(res[l, c])}");
			}
			Note($"SWEEP {modName} done: {judged} weapons hit the enemy, {flagged} with an outcome that depends on the commander's facing (damage ratio under 0.6), {ownerBadWeapons} read an owner facing the wrong way");
		}

		/// <summary>LEVY_FACING_FILTER: empty matches everything; otherwise any of the "/"-separated names occurring in the text.</summary>
		private static bool Matches(string filter, string text) => string.IsNullOrEmpty(filter) || filter.Split('/').Any(f => f.Length > 0 && text.Contains(f, StringComparison.OrdinalIgnoreCase));

		/// <summary>The order the two commander facings are run in (LEVY_FACING_REVERSE=1: left first). A weapon whose result changes with the order, not with the facing, carries state from one scene to the next.</summary>
		private static int[] CmdOrder => Environment.GetEnvironmentVariable("LEVY_FACING_REVERSE") == "1" ? new[] { -1, 1 } : new[] { 1, -1 };

		private static Item AmmoFor(int useAmmo)
		{
			for (int t = 1; t < ItemID.Count; t++)
				if (ContentSamples.ItemsByType.TryGetValue(t, out Item it) && it != null && it.ammo == useAmmo && it.damage >= 0 && !it.IsAir) { Item a = Vanilla(t); a.stack = 999; return a; }
			return null;
		}

		/// <summary>The commander's facing makes no difference, beyond the noise of two runs on one seed.</summary>
		private static void Judge(string label, Scene a, Scene b)
		{
			Check($"{label}: the weapon hits the enemy with the commander facing right and left ({a.Strikes}, {b.Strikes} hits)", a.Strikes > 0 && b.Strikes > 0);
			if (a.Strikes == 0 || b.Strikes == 0) return;
			// two runs on one seed still differ by the weapon's own chaos (a yoyo's orbit, a boomerang's return) and by how few hits a slow weapon lands in the window: the tolerances grow as the hit count shrinks, and stay under what the bias measured
			// in the PvP lab looked like (2 to 3 times the damage). The owner checks below are exact.
			int n = Math.Min(a.Strikes, b.Strikes); double noise = 2.0 / Math.Sqrt(Math.Max(1, n));
			double lo = Math.Min(a.Dmg, b.Dmg), hi = Math.Max(a.Dmg, b.Dmg);
			Check($"{label}: the damage does not depend on the commander's facing ({a.Dmg} vs {b.Dmg})", lo >= hi * 0.6);
			Check($"{label}: the hit count does not depend on the commander's facing ({a.Strikes} vs {b.Strikes})", Math.Abs(a.Strikes - b.Strikes) <= Math.Max(3, 0.35 * Math.Max(a.Strikes, b.Strikes)));
			Check($"{label}: the knockback pushes the enemy away from the levy alike ({a.AwayShare:0.00} vs {b.AwayShare:0.00})", Math.Abs(a.AwayShare - b.AwayShare) <= Math.Max(0.15, noise));
			if (a.Shots >= 12 && b.Shots >= 12) Check($"{label}: the shots leave the levy toward the enemy alike ({a.ForwardShare:0.00} vs {b.ForwardShare:0.00})", Math.Abs(a.ForwardShare - b.ForwardShare) <= Math.Max(0.15, 2.0 / Math.Sqrt(Math.Min(a.Shots, b.Shots))));
			if (a.Pos > 0 && b.Pos > 0)
			{
				Check($"{label}: where the shot is when it hits does not depend on the commander's facing (forward {a.Fwd:0.0} vs {b.Fwd:0.0}px, vertical {a.Up:0.0} vs {b.Up:0.0}px)", Math.Abs(a.Fwd - b.Fwd) <= Math.Max(12, 60 * noise) && Math.Abs(a.Up - b.Up) <= Math.Max(12, 60 * noise));
			}
			// what the game's owner reads said: at every point a levy's shot is handed to code that reads its owner, the owner faces the way the levy faces (the lash, the swing and the shot are the levy's, not the commander's)
			for (int k = 0; k < Kinds.Length; k++)
			{
				int bad = a.DirBad[k] + b.DirBad[k], seen = a.Seen[k] + b.Seen[k];
				if (seen > 0) Check($"{label}: the owner a levy's {Kinds[k]} reads faces the way the levy faces, with the commander facing either way ({bad} of {seen} the other way)", bad == 0);
			}
			// the facing a shot is born with: a weapon may give some of its own shots a facing of their own (sparks, scatter), so the two runs are compared, not the shots against the levy
			if (a.BornFacing + b.BornFacing >= 20) Check($"{label}: the facing a shot is born with does not depend on the commander's facing ({a.BornFacingBad} of {a.BornFacing} against the way it flies and the levy faces, vs {b.BornFacingBad} of {b.BornFacing}; {a.BornStale} and {b.BornStale} more only carry their slot's old direction)", Math.Abs(a.BornFacingBad - b.BornFacingBad) <= Math.Max(4, 0.25 * Math.Max(a.BornFacingBad, b.BornFacingBad)));
			Check($"{label}: the commander was left facing as the scene set her", a.CmdDirEnd == 1 && b.CmdDirEnd == -1);
		}
	}

	/// <summary>Feeds <see cref="FacingWorldCheck"/> (inert unless its check is tracking): what the owner of a levy's shot looks like at each point the game reads it.</summary>
	public class FacingProbeGlobal : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsLevys(projectile, true, source)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KSpawn, projectile.owner); }
		public override bool PreAI(Projectile projectile) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KPreAI, projectile.owner); return true; }
		public override void PostAI(Projectile projectile) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KPostAI, projectile.owner); }
		public override bool? CanHitNPC(Projectile projectile, NPC target) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsTarget(target) && FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KCanHit, projectile.owner); return null; }
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KModifyHit, projectile.owner); }
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) { if (FacingWorldCheck.Tracking) { FacingWorldCheck.OnHit(projectile, target); if (FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KOnHit, projectile.owner); } }
		public override void OnKill(Projectile projectile, int timeLeft) { if (FacingWorldCheck.Tracking && FacingWorldCheck.IsLevys(projectile, false)) FacingWorldCheck.SeeOwner(FacingWorldCheck.KKill, projectile.owner); }
	}
}
