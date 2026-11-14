using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check and lab of the army's whips, logged as "[WhipReal]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the officer check).
	/// For EVERY whip item the loaded mods define (vanilla, Calamity, Thorium: whatever <see cref="WeaponClassifier"/> files under Whip) the same flat arena holds a pinned, tough enemy a little way off and the
	/// real Main.Update steps the world twice:
	///  * PLAYER: the stand-in commander (slot 0, the single-player owner) holds the whip and uses it through vanilla's own Player.ItemCheck (the reference);
	///  * LEVY: a levy carries the same whip and fights the same enemy.
	/// Both are measured the same way: the projectile type the use makes, the lash's control points (segments) and reach, swings, hits and their damage, the buffs the enemy carries afterwards (the whip's tag debuff
	/// and whatever else it applies), and the tag's damage bonus read off a real minion shot (a Hornet stinger, a vanilla MinionShot) hitting the enemy untagged, tagged, from the commander and from a levy.
	/// The LEVY run also checks the real commander is not written to (the use-state snapshot of <see cref="UseGuard"/> plus mana, life, buffs and the minion focus) against a control run with a plain sword.
	/// LEVY_WHIPLAB=1 runs long windows (LEVY_WHIPONLY=1 runs this check alone, LEVY_WHIPFILTER=name narrows the whips); LEVY_WHIPREAL=0 runs the levy half on the old hand-made lash (the A/B).
	/// Throwaway soldiers only; nothing is saved.
	/// </summary>
	public class WhipRealCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static NPC _target; private static Vector2 _targetAt, _cmdAt;
		private static bool _local; private static int _savedMy, _my;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "WhipTester";
		private static bool _globalOff;
		private static bool Lab => Environment.GetEnvironmentVariable("LEVY_WHIPLAB") == "1";
		private static readonly bool Debug = Environment.GetEnvironmentVariable("LEVY_WHIPDEBUG") == "1";
		private static int _dbgLines, _dbgGeo;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_WHIPWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; PinTarget(); if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = _my; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = _my; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the officer world check calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			On_Projectile.Update += KeepOwnerActive;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { On_Projectile.Update -= KeepOwnerActive; _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Tracking = false; Cleanup(); }
			_mod.Logger.Info($"[WhipReal] took {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / 1000.0:0.0}s");
			_mod.Logger.Info($"[WhipReal] self-check pass={_pass} fail={_fail}");
		}

		/// <summary>The server loop drops a client-less slot's active flag from its own thread, and a whip (like any projectile) dies the moment its owner is not active: slot 0 is held active right before each projectile update.</summary>
		private static void KeepOwnerActive(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (_running && self.owner == 0 && Main.player[0] != null) { Main.player[0].active = true; Main.player[0].dead = false; }
			orig(self, i);
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[WhipReal] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[WhipReal] " + s);

		// ------------------------------------------------------------------------------------------------------------ the probe
		/// <summary>What the check records about every projectile spawned while it is tracking.</summary>
		private sealed class Info
		{
			public int Seq, Type, Owner, Kind, SpawnTick, SoldierIdx = -1, MaxSegs, Hits, Crits, LastSeen; public long Dmg; public float MaxReach, DrawReach = float.NaN; public bool Whip, Dead; public List<int> HitDmg = new();
		}
		public static bool Tracking;
		private static int _seq, _tick;
		private static readonly Dictionary<int, Info> Live = new();
		private static readonly List<Info> All = new();
		/// <summary>Set by the check just before it spawns a probe projectile (0 = an ordinary projectile, 1 = untagged-enemy stinger, 2 = commander's stinger, 3 = levy's stinger).</summary>
		internal static int PendingKind;

		internal static void OnSpawn(Projectile p)
		{
			if (!Tracking) return;
			var inf = new Info { Seq = ++_seq, Type = p.type, Owner = p.owner, Kind = PendingKind, SpawnTick = _tick, LastSeen = _tick, Whip = IsWhipType(p) };
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) inf.SoldierIdx = tag.SoldierIndex;
			if (Live.TryGetValue(p.whoAmI, out Info old)) old.Dead = true; // the slot was reused within the tick: the old one ended at its last sample
			Live[p.whoAmI] = inf; All.Add(inf);
		}

		private static int _killLines;
		internal static void OnKill(Projectile p, int timeLeft)
		{
			if (!Debug || !IsWhipType(p) || _killLines++ >= 6) return;
			var st = new System.Diagnostics.StackTrace(2, false);
			string frames = string.Join(" < ", st.GetFrames().Take(22).Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name));
			Note($"  dbg KILL whip owner={p.owner} ai0={p.ai[0]:0.0} ai1={p.ai[1]:0.0} timeLeft={timeLeft} itemAnim(dummy)={(Main.player[p.owner]?.itemAnimation)}/{(Main.player[p.owner]?.itemAnimationMax)} :: {frames}");
		}

		/// <summary>True for a probe stinger: its hit has no damage variation, so the tag's bonus reads as an exact number instead of hiding in the +-15% spread.</summary>
		internal static bool IsProbe(Projectile p) => Tracking && Live.TryGetValue(p.whoAmI, out Info inf) && inf.Type == p.type && inf.Kind != 0;

		internal static void OnHit(Projectile p, NPC target, NPC.HitInfo hit, int dmg)
		{
			if (!Tracking || !Live.TryGetValue(p.whoAmI, out Info inf) || inf.Type != p.type) return;
			inf.Hits++; inf.Dmg += dmg; if (hit.Crit) inf.Crits++; inf.HitDmg.Add(dmg);
		}

		/// <summary>Draw-state geometry: once a swing, a levy's lash is measured the way its draw sees it (BeginDraw's swap) and compared with the AI-state reach the sampler read.</summary>
		private static void DrawProbe()
		{
			if (!_drawProbeOn) return;
			foreach (var kv in Live)
			{
				Info inf = kv.Value; Projectile p = Main.projectile[kv.Key];
				if (!inf.Whip || !p.active || p.owner != SoldierNPC.DummySlot || p.ModProjectile is SoldierWhip || _tick - inf.SpawnTick < _probeAnim * 0.45f || !float.IsNaN(inf.DrawReach)) continue;
				float draw = HeldWeaponProjectile.ProbeWhipDrawReach(p), ai = AiReach(p, inf);
				// the draw state must give the lash the same geometry the AI state does, at the same instant
				inf.DrawReach = draw < 0f || ai <= 0f ? -3f : Math.Abs(draw - ai);
			}
		}
		/// <summary>The lash's reach at this instant in the state its AI sees (the dummy in the owner slot with the swing's animation).</summary>
		private static float AiReach(Projectile p, Info inf)
		{
			var pts = new List<Vector2>(48); bool swapped = false;
			try
			{
				if (inf.SoldierIdx < 0 || Main.npc[inf.SoldierIdx].ModNPC is not SoldierNPC sb || sb.Dummy == null) return -1f;
				EnemyTargeting.Swap(sb); swapped = true;
				using (new RealWhips.AiScope(sb, p, WeaponUse.HeldItem(sb), true)) Projectile.FillWhipControlPoints(p, pts);
			}
			catch (Exception) { return -1f; }
			finally { if (swapped) EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); }
			float r = 0f; for (int i = 1; i < pts.Count; i++) { float d = Vector2.Distance(pts[0], pts[i]); if (!float.IsNaN(d)) r = Math.Max(r, d); }
			return r;
		}
		private static bool _drawProbeOn; private static int _probeAnim = 30;

		private static bool IsWhipType(Projectile p) => (p.type > 0 && p.type < ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[p.type]) || p.aiStyle == ProjAIStyleID.Whip || p.ModProjectile is SoldierWhip;

		private static string ProjName(int type) => type <= 0 ? "none" : (type < ProjectileID.Count ? ProjectileID.Search.GetName(type) : ProjectileLoader.GetProjectile(type)?.FullName ?? type.ToString()) + "#" + type;
		private static string BuffName(int type) => type <= 0 ? "none" : (type < BuffID.Count ? BuffID.Search.GetName(type) : BuffLoader.GetBuff(type)?.FullName ?? type.ToString());

		/// <summary>Per tick: the lash's geometry. A whip owned by the dummy slot is measured with its soldier's dummy standing in the slot (what the AI sees).</summary>
		private static void Sample()
		{
			_tick++;
			List<int> dead = null;
			var pts = new List<Vector2>(32);
			DrawProbe();
			foreach (var kv in Live)
			{
				Info inf = kv.Value; Projectile p = Main.projectile[kv.Key];
				if (!p.active || p.type != inf.Type || p.owner != inf.Owner)
				{
					if (Debug && inf.Whip && _dbgLines++ < 60) Note($"  dbg t={_tick} whip#{kv.Key} gone: active={p.active} type={p.type}/{inf.Type} owner={p.owner}/{inf.Owner} spawned t={inf.SpawnTick}");
					inf.Dead = true; (dead ??= new List<int>()).Add(kv.Key); continue;
				}
				if (Debug && inf.Whip && _tick - inf.SpawnTick < 4 && _dbgLines++ < 60) Note($"  dbg t={_tick} whip#{kv.Key} owner={p.owner} ai0={p.ai[0]:0.0} timeLeft={p.timeLeft} vel={p.velocity.Length():0.0} pos={p.Center - Main.player[0].Center}");
				inf.LastSeen = _tick;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tg0) && tg0.SoldierIndex >= 0) inf.SoldierIdx = tg0.SoldierIndex; // the soldier tag is put on after the spawn
				if (!inf.Whip) continue;
				if (p.ModProjectile is SoldierWhip)
				{
					NPC sn = inf.SoldierIdx >= 0 ? Main.npc[inf.SoldierIdx] : null;
					if (sn != null && sn.active) inf.MaxReach = Math.Max(inf.MaxReach, Vector2.Distance(sn.Center, p.Center));
					inf.MaxSegs = LevyConstants.WhipSegments;
					continue;
				}
				bool swapped = false;
				try
				{
					if (p.owner == SoldierNPC.DummySlot && inf.SoldierIdx >= 0 && Main.npc[inf.SoldierIdx].ModNPC is SoldierNPC sb && sb.Dummy != null) { EnemyTargeting.Swap(sb); swapped = true; }
					pts.Clear();
					if (swapped) { Item held = WeaponUse.HeldItem((SoldierNPC)Main.npc[inf.SoldierIdx].ModNPC); using (new RealWhips.AiScope((SoldierNPC)Main.npc[inf.SoldierIdx].ModNPC, p, held, true)) Projectile.FillWhipControlPoints(p, pts); }
					else Projectile.FillWhipControlPoints(p, pts);
				}
				catch (Exception) { pts.Clear(); }
				finally { if (swapped) EnemyTargeting.Unswap(); EnemyTargeting.EnsureSlotRestored(); }
				if (pts.Count > 0)
				{
					if (Debug && p.owner == SoldierNPC.DummySlot && _dbgGeo++ < 40) Note($"  dbg geo t={_tick} ai0={p.ai[0]:0.0} n={pts.Count} first={pts[0]} p5={pts[Math.Min(5, pts.Count - 1)]} last={pts[pts.Count - 1]} vel={p.velocity} soldier={Main.npc[inf.SoldierIdx].Center}");
					inf.MaxSegs = Math.Max(inf.MaxSegs, pts.Count);
					float r = 0f; bool nan = false; for (int i = 1; i < pts.Count; i++) { float dd = Vector2.Distance(pts[0], pts[i]); if (float.IsNaN(dd)) nan = true; else r = Math.Max(r, dd); }
					if (nan && Debug && _dbgLines++ < 60) Note($"  dbg NaN points t={_tick} owner={p.owner} ai0={p.ai[0]:0.0} n={pts.Count} first={pts[0]} last={pts[pts.Count - 1]} vel={p.velocity}");
					if (!nan) inf.MaxReach = Math.Max(inf.MaxReach, r);
				}
			}
			if (dead != null) foreach (int k in dead) Live.Remove(k);
		}

		// ------------------------------------------------------------------------------------------------------------ the arena
		private static void Cleanup()
		{
			try { Clear(); } catch (Exception) { }
			Player p = Main.player[0]; if (p != null) { p.inventory[0] = new Item(); }
		}

		private static void Step(int n, Action perTick = null)
		{
			for (int i = 0; i < n; i++)
			{
				KeepCommander(); ClearStrangers();
				global::Levy.Common.Testing.WorldStep.Update();
				perTick?.Invoke();
				if (Tracking) Sample();
			}
		}

		private static void ClearStrangers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && !n.friendly && !n.townNPC && n.ModNPC == null && n != _target && !(n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) n.active = false;
			}
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void PinTarget()
		{
			if (_target == null || !_target.active) return;
			_target.active = true; _target.timeLeft = 9999; _target.Bottom = _targetAt; _target.velocity = Vector2.Zero; _target.life = _target.lifeMax; _target.dontTakeDamage = false;
		}

		private static void Arena()
		{
			for (int i = Cx - 70; i <= Cx + 70; i++)
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
			_target = null;
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			Live.Clear();
			EnemyTargeting.EnsureSlotRestored();
		}

		private static void SpawnEnemy(float pxAway)
		{
			_targetAt = new Vector2(Cx * 16f + pxAway, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("WhipRealCheck"), (int)_targetAt.X, (int)_targetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) { _target = null; return; }
			_target = Main.npc[ti];
			_target.lifeMax = 1_000_000; _target.life = 1_000_000; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 64; _target.height = 96;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.Bottom = _targetAt;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position;
		}

		private static int ItemOf(string mod, string name)
		{
			if (mod == null) return ItemID.Search.TryGetId(name, out int id) ? id : 0;
			return ModContent.TryFind(mod, name, out ModItem mi) ? mi.Type : 0;
		}

		// ------------------------------------------------------------------------------------------------------------ the measurement of one scene
		private sealed class Scene
		{
			public string Item, Path; public int Ticks, Swings, Hits, Crits, WhipType = -1, Segs; public float Reach; public long Dmg;
			public double LifeTicks; public readonly Dictionary<int, int> Types = new(); public readonly List<string> Buffs = new();
			public readonly List<int> BuffIds = new(); public bool VanillaTag, LevyTag; public double StingerBase, StingerCmd, StingerLevy, StingerCmdFirst, StingerLevyFirst; public string LeftoverNames = ""; public int StingerBaseN, StingerCmdN, StingerLevyN;
			public float DrawReach = float.NaN; public int Leftover = -1; public int Writes; public List<string> WriteNotes = new(); public double StartUs, AiUs, DmgUs, ScopeUs; public bool Ok = true;
			public double DmgPerHit => Hits > 0 ? (double)Dmg / Hits : 0; public double DmgPerSec => Ticks > 0 ? Dmg * 60.0 / Ticks : 0;
		}

		private static bool IsTagBuff(int t) => t > 0 && t < BuffID.Sets.IsATagBuff.Length && BuffID.Sets.IsATagBuff[t];

		/// <summary>Reads the lash numbers out of what the probe recorded.</summary>
		private static void Collect(Scene sc, int fromTick)
		{
			var whips = All.Where(i => i.Whip && i.SpawnTick >= fromTick).ToList();
			sc.Swings = whips.Count;
			if (whips.Count > 0)
			{
				sc.WhipType = whips[0].Type; sc.Segs = whips.Max(i => i.MaxSegs); sc.Reach = whips.Max(i => i.MaxReach);
				var dr = whips.Where(i => !float.IsNaN(i.DrawReach)).Select(i => i.DrawReach).ToList(); if (dr.Count > 0) sc.DrawReach = dr.Max(); // the largest gap between the draw state and the AI state
				sc.Hits = whips.Sum(i => i.Hits); sc.Dmg = whips.Sum(i => i.Dmg); sc.Crits = whips.Sum(i => i.Crits);
				var done = whips.Where(i => i.Dead).Select(i => i.LastSeen - i.SpawnTick + 1).ToList();
				sc.LifeTicks = done.Count > 0 ? done.Average() : 0;
			}
			foreach (Info i in All.Where(i => i.SpawnTick >= fromTick && i.Kind == 0)) sc.Types[i.Type] = sc.Types.TryGetValue(i.Type, out int n) ? n + 1 : 1;
		}

		private static void ReadEnemy(Scene sc)
		{
			sc.Buffs.Clear(); sc.BuffIds.Clear();
			if (_target == null || !_target.active) return;
			for (int b = 0; b < _target.buffType.Length; b++) if (_target.buffType[b] > 0 && _target.buffTime[b] > 0) { sc.BuffIds.Add(_target.buffType[b]); sc.Buffs.Add(BuffName(_target.buffType[b])); if (IsTagBuff(_target.buffType[b])) sc.VanillaTag = true; }
			sc.Buffs.Sort();
			sc.LevyTag = LevyNpcData.IsNpcTagged(_target);
		}

		/// <summary>Fires <paramref name="n"/> Hornet stingers (a vanilla MinionShot) into the enemy, one a tick, and returns the mean damage of the hits that did not crit.</summary>
		private static double LastFirst;
		private static int Reps => int.TryParse(Environment.GetEnvironmentVariable("LEVY_WHIPREPS"), out int r) && r > 0 ? r : (Lab ? 1 : 2); // the old lash's damage rate varies by a third from run to run: the quick check takes two runs of each

		/// <summary>Adds another run of the same whip to <paramref name="a"/> (the damage numbers only: swings, hits, crits, damage, ticks).</summary>
		private static void Merge(Scene a, Scene b) { if (b == null || !b.Ok) return; a.Swings += b.Swings; a.Hits += b.Hits; a.Crits += b.Crits; a.Dmg += b.Dmg; a.Ticks += b.Ticks; a.Reach = Math.Max(a.Reach, b.Reach); a.Segs = Math.Max(a.Segs, b.Segs); }
		private static (double mean, int n) Stingers(int kind, SoldierNPC body, int n)
		{
			int before = All.Count;
			for (int k = 0; k < n; k++)
			{
				for (int i = 0; i < _target.immune.Length; i++) _target.immune[i] = 0;
				PendingKind = kind;
				try
				{
					if (kind == 3 && body != null) { int sv = Main.myPlayer; Main.myPlayer = 0; try { WeaponUse.Spawn(body, _target.Center, Vector2.Zero, ProjectileID.HornetStinger, 40, 0f, DamageClass.Summon); } finally { Main.myPlayer = sv; } } // a levy's shot is owned by the single-player commander
					else Projectile.NewProjectile(new EntitySource_WorldEvent("WhipRealCheck"), _target.Center, Vector2.Zero, ProjectileID.HornetStinger, 40, 0f, _my);
				}
				finally { PendingKind = 0; }
				Step(2);
			}
			Step(3);
			var hits = All.Skip(before).Where(i => i.Kind == kind && i.Hits > 0).SelectMany(i => i.HitDmg.Select(d => (d, i.Crits))).ToList();
			var noncrit = All.Skip(before).Where(i => i.Kind == kind && i.Hits > 0 && i.Crits == 0).SelectMany(i => i.HitDmg).ToList();
			var firstHit = All.Skip(before).Where(i => i.Kind == kind && i.Hits > 0).Select(i => i.HitDmg[0]).FirstOrDefault();
			LastFirst = firstHit;
			return (noncrit.Count > 0 ? noncrit.Average() : 0, noncrit.Count);
		}

		// ------------------------------------------------------------------------------------------------------------ PLAYER
		private static MethodInfo _itemCheck; private static bool _itemCheckTakesInt;
		/// <summary>
		/// The reference player: a scratch Player standing in slot 255, which is where a dedicated server's own player lives (Main.myPlayer = 255 there) and the one slot the server loop never marks inactive from its
		/// thread (it does that to slot 0 between hooks, and a whip is killed the moment its owner is not active). It uses the whip through vanilla's own Player.ItemCheck, one call a tick.
		/// </summary>
		private static Player _scratch, _saved255; private static Terraria.Graphics.SpriteViewMatrix _savedView;

		private static void PlayerUse(Item whip, Vector2 aim, int t)
		{
			Player p = _scratch;
			p.whoAmI = 255; p.active = true; p.dead = false; p.ghost = false; p.name = "WhipScratch";
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
			if (p.statLifeMax2 < 500) { p.statLifeMax = p.statLifeMax2 = 500; } p.statLife = p.statLifeMax2;
			if (p.inventory[0] == null || p.inventory[0].IsAir || p.inventory[0].type != whip.type) p.inventory[0] = whip.Clone();
			p.selectedItem = 0; p.noItems = false;
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
			bool press = whip.autoReuse || (t % 2 == 0);
			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && whip.channel; if (!press) p.releaseUseItem = true;
			try { PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
			int saved = Main.myPlayer;
			try
			{
				Main.myPlayer = 255;
				if (_itemCheck == null)
				{
					foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
				}
				_itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 255 } : null);
			}
			catch (Exception e) { var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; if (!(ie.StackTrace ?? "").Contains("ItemCheck_PlayInstruments")) Note("  player ItemCheck threw " + ie.GetType().Name + ": " + ie.Message); }
			finally { Main.myPlayer = saved; }
		}

		private static void BeginScratch()
		{
			_saved255 = Main.player[255];
			_scratch = new Player { whoAmI = 255, name = "WhipScratch" };
			_scratch.statLifeMax = _scratch.statLifeMax2 = 500; _scratch.statLife = 500;
			_scratch.ResetEffects();
			_scratch.inventory[0] = new Item(); _scratch.selectedItem = 0;
			Main.player[255] = _scratch; _my = 255;
			// vanilla's use code reads Main.GameViewMatrix.Zoom for every item (ItemCheck_PlayInstruments), and a dedicated server has none: it threw a NullReferenceException a tick (tML logs each)
			_savedView = Main.GameViewMatrix;
			if (_savedView == null) Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
		}

		private static void EndScratch()
		{
			_my = 0;
			if (_scratch != null && ReferenceEquals(Main.player[255], _scratch)) Main.player[255] = _saved255;
			if (_savedView == null) Main.GameViewMatrix = null;
			_scratch = null; _saved255 = null; _savedView = null;
			EnemyTargeting.ResetState(); // the swap bookkeeping adopted the scratch as slot 255's null player: it is the real one again
		}

		private static Scene PlayerScene(Item whip, int ticks)
		{
			Clear(); Arena();
			var sc = new Scene { Item = whip.Name, Path = "PLAYER", Ticks = ticks };
			_cmdAt = new Vector2(Cx * 16f + 8f, FloorY * 16f - 28f);
			SpawnEnemy(100f);
			if (_target == null) { sc.Ok = false; return sc; }
			BeginScratch();
			try
			{
				Tracking = true; _tick = 0; _seq = 0; All.Clear(); Live.Clear();
				Step(3);
				(sc.StingerBase, sc.StingerBaseN) = Stingers(1, null, 10);
				int from = _tick;
				_scratch.inventory[0] = whip.Clone();
				_scratch.itemAnimation = 0; _scratch.itemTime = 0;
				for (int t = 0; t < ticks; t++)
				{
					int tt = t;
					Step(1, () => PlayerUse(whip, _target.Center, tt));
					if (Debug && t < 12 && _dbgLines++ < 80) { Player pl = _scratch; Note($"  dbg player t={t} anim={pl.itemAnimation}/{pl.itemAnimationMax} itemTime={pl.itemTime} use={pl.controlUseItem} rel={pl.releaseUseItem} held={pl.heldProj} whips={All.Count(i => i.Whip)} projs={All.Count}"); }
				}
				_scratch.controlUseItem = false;
				Collect(sc, from); ReadEnemy(sc);
				if (sc.VanillaTag) { (sc.StingerCmd, sc.StingerCmdN) = Stingers(2, null, 10); sc.StingerCmdFirst = LastFirst; }
				Tracking = false;
			}
			finally { Tracking = false; Clear(); EndScratch(); }
			return sc;
		}

		// ------------------------------------------------------------------------------------------------------------ LEVY
		private struct CmdState
		{
			public UseGuard.Snap S; public int Life, Mana, Focus, Buffs; public long BuffHash;
			public static CmdState Take()
			{
				Player p = Main.player[0]; var c = new CmdState { S = UseGuard.Take(0), Life = p.statLife, Mana = p.statMana, Focus = p.MinionAttackTargetNPC };
				for (int i = 0; i < p.buffType.Length; i++) if (p.buffType[i] > 0) { c.Buffs++; c.BuffHash = c.BuffHash * 31 + p.buffType[i]; }
				return c;
			}
			public List<string> Diff(CmdState o)
			{
				var d = new List<string>(); UseGuard.Snap a = S, b = o.S;
				if (a.HeldProj != b.HeldProj) d.Add("heldProj"); if (a.ItemAnimation != b.ItemAnimation) d.Add("itemAnimation"); if (a.ItemAnimationMax != b.ItemAnimationMax) d.Add("itemAnimationMax");
				if (a.ItemTime != b.ItemTime) d.Add("itemTime"); if (a.ItemTimeMax != b.ItemTimeMax) d.Add("itemTimeMax"); if (a.ReuseDelay != b.ReuseDelay) d.Add("reuseDelay");
				if (a.Direction != b.Direction) d.Add("direction"); if (a.SelectedItem != b.SelectedItem) d.Add("selectedItem"); if (a.Channel != b.Channel) d.Add("channel");
				if (a.UseItem != b.UseItem) d.Add("controlUseItem"); if (a.UseTile != b.UseTile) d.Add("controlUseTile"); if (a.ReleaseUse != b.ReleaseUse) d.Add("releaseUseItem"); if (a.NoItems != b.NoItems) d.Add("noItems");
				if (a.ItemRotation != b.ItemRotation) d.Add("itemRotation"); if (a.Velocity.X != b.Velocity.X) d.Add("velocity.X"); if (a.NoThrow != b.NoThrow) d.Add("noThrow");
				if (a.Frozen != b.Frozen || a.Stoned != b.Stoned || a.Webbed != b.Webbed) d.Add("frozen/stoned/webbed"); if (a.TalkNpc != b.TalkNpc) d.Add("talkNPC");
				if (Focus != o.Focus) d.Add("MinionAttackTargetNPC"); if (Buffs != o.Buffs || BuffHash != o.BuffHash) d.Add("buffs");
				return d;
			}
		}

		private static SoldierNPC SpawnLevy(Item weapon, out SoldierRecord rec)
		{
			rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Whip",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (!rec.Gear.CanHold(SoldierGear.SlotWeapon, weapon)) return null;
			rec.Gear.Weapon = weapon; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private static Scene LevyScene(Item whip, int ticks, int mode) // 0 control (a plain sword), 1 the real lash, 2 the old hand-made lash
		{
			bool control = mode == 0, savedOff = RealWhips.Disabled;
			if (mode == 1 || mode == 0) RealWhips.Disabled = _globalOff; else RealWhips.Disabled = true;
			try { return LevyScene1(whip, ticks, control, mode); }
			finally { RealWhips.Disabled = savedOff; }
		}

		private static Scene LevyScene1(Item whip, int ticks, bool control, int mode)
		{
			Clear(); Arena();
			var sc = new Scene { Item = whip.Name, Path = control ? "CONTROL" : (mode == 2 ? "LEVY-OLD" : "LEVY"), Ticks = ticks };
			_cmdAt = new Vector2(Cx * 16f + 8f, FloorY * 16f - 28f);
			Main.player[0].inventory[0] = new Item(); Main.player[0].itemAnimation = 0; Main.player[0].itemTime = 0; Main.player[0].MinionAttackTargetNPC = -1;
			SpawnEnemy(100f);
			if (_target == null) { sc.Ok = false; return sc; }
			Item w = new Item(); w.SetDefaults(whip.type);
			SoldierNPC body = SpawnLevy(w, out _);
			if (body == null) { sc.Ok = false; return sc; }
			Tracking = true; _tick = 0; _seq = 0; All.Clear(); Live.Clear();
			Step(3);
			_drawProbeOn = mode == 1; _probeAnim = Math.Max(1, whip.useAnimation);
			RealWhips.Measure = true; RealWhips.ResetCost();
			int from = _tick; var writes = new Dictionary<string, int>(); int writeTicks = 0;
			for (int t = 0; t < ticks; t++)
			{
				KeepCommander(); CmdState before = CmdState.Take();
				ClearStrangers(); global::Levy.Common.Testing.WorldStep.Update(); Sample();
				CmdState after = CmdState.Take();
				var d = before.Diff(after); if (d.Count > 0) { writeTicks++; foreach (string f in d) writes[f] = writes.TryGetValue(f, out int n) ? n + 1 : 1; }
			}
			RealWhips.Measure = false; _drawProbeOn = false;
			sc.Writes = writeTicks; foreach (var kv in writes.OrderBy(k => k.Key)) sc.WriteNotes.Add(kv.Key + "x" + kv.Value);
			Collect(sc, from); ReadEnemy(sc);
			if (RealWhips.Starts > 0 || RealWhips.AiCalls > 0)
			{
				double us = 1e6 / System.Diagnostics.Stopwatch.Frequency; int sw = Math.Max(1, sc.Swings);
				sc.StartUs = RealWhips.StartTicks * us / sw; sc.AiUs = RealWhips.AiTicks * us / sw; sc.DmgUs = RealWhips.DamageTicks * us / sw; sc.ScopeUs = RealWhips.ScopeTicks * us / sw;
			}
			if (!control && (sc.VanillaTag || sc.LevyTag))
			{
				bool flame = sc.Buffs.Contains("FlameWhipEnemyDebuff"); // the Firecracker's tag is spent by the first minion hit: put it back for each batch
				if (flame) _target.AddBuff(BuffID.FlameWhipEnemyDebuff, 240);
				(sc.StingerLevy, sc.StingerLevyN) = Stingers(3, body, 10); sc.StingerLevyFirst = LastFirst;
				if (flame) _target.AddBuff(BuffID.FlameWhipEnemyDebuff, 240);
				(sc.StingerCmd, sc.StingerCmdN) = Stingers(2, null, 10); sc.StingerCmdFirst = LastFirst;
			}
			if (!control)
			{
				// the levy is gone: the lash it threw goes with it
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander); // (a levy that is still on the roster is respawned at once, and its lash with it)
				body.NPC.active = false;
				Step(4);
				int left = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && IsWhipType(q)) { left++; q.TryGetGlobalProjectile(out SoldierProjectileTag lt); q.TryGetGlobalProjectile(out HeldWeaponProjectile lh); sc.LeftoverNames += $"{ProjName(q.type)}@{q.owner} ai0={q.ai[0]:0} timeLeft={q.timeLeft} chain={lh?.Chain} whip={lh?.Whip} soldier={lt?.SoldierIndex} "; } }
				sc.Leftover = left;
			}
			Tracking = false;
			return sc;
		}

		/// <summary>The stinger's untagged damage, measured on its own on a fresh enemy.</summary>
		private static double StingerBaseline()
		{
			Clear(); Arena(); _cmdAt = new Vector2(Cx * 16f + 8f, FloorY * 16f - 28f); SpawnEnemy(100f);
			Tracking = true; _tick = 0; _seq = 0; All.Clear(); Live.Clear();
			Step(3);
			var (m, n) = Stingers(1, null, 12);
			Tracking = false; return m;
		}

		// ------------------------------------------------------------------------------------------------------------ the sweep
		private static List<int> WhipItems()
		{
			var list = new List<int>();
			string filter = Environment.GetEnvironmentVariable("LEVY_WHIPFILTER");
			for (int t = 1; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item it) || it == null || it.IsAir || it.shoot <= 0) continue;
				WeaponProfile prof; try { prof = WeaponClassifier.GetProfile(it); } catch (Exception) { continue; }
				if (prof == null || prof.Family != WeaponFamily.Whip) continue;
				if (!string.IsNullOrEmpty(filter) && !(it.Name + "|" + it.ModItem?.FullName).Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
				list.Add(t);
			}
			return list;
		}

		private static string Fmt(Scene s) =>
			$"{s.Path,-8} proj={ProjName(s.WhipType)} segs={s.Segs} reach={s.Reach:0}px swings={s.Swings} life={s.LifeTicks:0.0}t hits={s.Hits} crit={s.Crits} dmg/hit={s.DmgPerHit:0.0} dmg/s={s.DmgPerSec:0.0} others=[{string.Join(",", s.Types.Where(k => !(k.Key == s.WhipType)).Select(k => ProjName(k.Key) + "x" + k.Value))}] buffs=[{string.Join(",", s.Buffs)}] tag(vanilla={s.VanillaTag},levy={s.LevyTag}) stinger(base={s.StingerBase:0.0},cmd={s.StingerCmd:0.0},levy={s.StingerLevy:0.0}) writes={s.Writes}[{string.Join(" ", s.WriteNotes)}] cost/swing(start={s.StartUs:0.0},ai={s.AiUs:0.0},dmg={s.DmgUs:0.0},of which damage-scope={s.ScopeUs:0.0})us";

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			bool lab = Lab; int pT = lab ? 360 : 150, lT = lab ? 600 : 330;
			_globalOff = RealWhips.Disabled;
			List<int> whips = WhipItems();
			{
				var other = new List<string>();
				for (int ty = 1; ty < ItemLoader.ItemCount; ty++)
					if (ContentSamples.ItemsByType.TryGetValue(ty, out Item it2) && it2 != null && !it2.IsAir && it2.shoot > 0 && it2.shoot < ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[it2.shoot] && !whips.Contains(ty)) other.Add(it2.Name);
				Note($"items whose shot is a whip but are not filed under Whip: [{string.Join(",", other)}]");
			}
			Note($"{whips.Count} whips; levy path = {(RealWhips.Disabled ? "OLD hand-made lash (LEVY_WHIPREAL=0)" : "real whip projectile")}; windows player={pT} levy={lT} ticks");
			Check($"the sweep found whips ({whips.Count})", whips.Count > 0);
			Scene ctl = null;
			try { Item sword = new Item(); sword.SetDefaults(ItemID.CopperShortsword); ctl = LevyScene(sword, lT, 0); }
			catch (Exception e) { Check("control run threw " + e.GetType().Name + ": " + e.Message, false); }
			finally { Clear(); }
			int ctlWrites = ctl?.Writes ?? 0;
			Note($"control (levy with a plain sword, {lT} ticks): real-commander writes on {ctlWrites} ticks [{string.Join(" ", ctl?.WriteNotes ?? new List<string>())}]");
			double stingerBase = 0;
			try { stingerBase = StingerBaseline(); } finally { Clear(); }
			Note($"stinger on an untagged enemy: {stingerBase:0.0} damage per hit");
			Check("the Hornet stinger probe hits an untagged enemy", stingerBase > 0);
			var rows = new List<(Scene p, Scene l)>(); var olds = new List<Scene>();
			foreach (int type in whips)
			{
				Item w = new Item(); w.SetDefaults(type);
				string label = w.Name + (w.ModItem != null ? " (" + w.ModItem.Mod.Name + ")" : "");
				Scene ps = null, ls = null, lo = null;
				try { ps = PlayerScene(w, pT); } catch (Exception e) { Check($"{label}: the player scene ran ({e.GetType().Name}: {e.Message})", false); }
				finally { Clear(); }
				if (!_globalOff)
				{
					try { lo = LevyScene(w, lT, 2); for (int r = 1; r < Reps; r++) { Clear(); Merge(lo, LevyScene(w, lT, 2)); } } catch (Exception e) { Check($"{label}: the old-lash scene ran ({e.GetType().Name}: {e.Message})", false); }
					finally { Clear(); }
				}
				try { ls = LevyScene(w, lT, 1); for (int r = 1; r < Reps; r++) { Clear(); Merge(ls, LevyScene(w, lT, 1)); } } catch (Exception e) { Check($"{label}: the levy scene ran ({e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()})", false); }
				finally { Clear(); }
				if (ps == null || ls == null) continue;
				Note($"== {label} (shoot {ProjName(w.shoot)}, useTime {w.useTime}, anim {w.useAnimation}, speed {w.shootSpeed}, dmg {w.damage})");
				Note("   " + Fmt(ps)); if (lo != null) Note("   " + Fmt(lo)); Note("   " + Fmt(ls));
				rows.Add((ps, ls)); olds.Add(lo);
				bool realTag = ps.VanillaTag;
				Check($"{label}: the player's use made the whip projectile", ps.Ok && ps.WhipType > 0);
				Check($"{label}: the levy's lash is the real whip projectile ({ProjName(ls.WhipType)})", ls.Ok && ls.WhipType == ps.WhipType && !RealWhips.Disabled);
				Check($"{label}: its segments match the player's ({ls.Segs} vs {ps.Segs})", ls.Segs == ps.Segs && ps.Segs > 0);
				Check($"{label}: its reach is within 15% of the player's ({ls.Reach:0} vs {ps.Reach:0}px)", ps.Reach > 0 && Math.Abs(ls.Reach - ps.Reach) <= ps.Reach * 0.15f);
				Check($"{label}: it lashes and hits ({ls.Swings} swings, {ls.Hits} hits)", ls.Swings > 0 && ls.Hits > 0);
				Check($"{label}: its draw sees the same lash geometry as its AI (largest gap {ls.DrawReach:0.0}px)", !float.IsNaN(ls.DrawReach) && ls.DrawReach >= 0f && ls.DrawReach <= 2f);
				// the tag debuffs are the whip's identity and must match; a debuff that rides on a crit or a tip hit (Thorium's Gouge) depends on where the lash met the enemy: reported, not judged
				var tagsOf = (Scene sc0) => sc0.BuffIds.Where(IsTagBuff).Select(BuffName).ToList();
				bool buffsOk = tagsOf(ps).All(b => ls.Buffs.Contains(b));
				Check($"{label}: the enemy carries the player's tag debuff(s) [{string.Join(",", tagsOf(ps))}] (levy: [{string.Join(",", ls.Buffs)}])", buffsOk);
				if (!ps.Buffs.All(b => ls.Buffs.Contains(b))) Note($"   OBSERVED {label}: debuffs of the player's lash the levy's did not carry: [{string.Join(",", ps.Buffs.Where(b => !ls.Buffs.Contains(b)))}] (levy hits: {ls.Hits}, crits {ls.Crits}; player crits {ps.Crits})");
				if (realTag)
				{
					bool flame = ps.Buffs.Contains("FlameWhipEnemyDebuff"); // the Firecracker's tag is a one-shot: its first hit is the big one
					bool Up(double mean, double first, double b) => b > 0 && (flame ? first > b * 1.4 : mean > b * 1.03);
					// the reference is what the game gives a commander minion off the player's lash (Calamity re-works the vanilla tags into its own system, some of them crit chances, so the amount is read, not assumed)
					bool playerGives = Up(ps.StingerCmd, ps.StingerCmdFirst, ps.StingerBase);
					Note($"   OBSERVED {label}: commander minion off the player's lash {ps.StingerBase:0.0} -> {ps.StingerCmd:0.0} (first {ps.StingerCmdFirst:0.0}); off the levy's lash {ls.StingerCmd:0.0} (first {ls.StingerCmdFirst:0.0}); levy minion {ls.StingerLevy:0.0} (first {ls.StingerLevyFirst:0.0}); untagged {stingerBase:0.0}");
					double refCmd = flame ? ps.StingerCmdFirst : ps.StingerCmd, gotCmd = flame ? ls.StingerCmdFirst : ls.StingerCmd;
					Check($"{label}: the levy's lash gives the commander's minion what the player's lash does ({gotCmd:0.0} vs {refCmd:0.0})", Math.Abs(gotCmd - refCmd) <= Math.Max(3.0, refCmd * 0.1));
					if (playerGives) Check($"{label}: a levy minion's shot hits the tagged enemy harder, by the game's numbers ({ls.StingerLevy:0.0}, first {ls.StingerLevyFirst:0.0}, vs untagged {stingerBase:0.0})", Up(ls.StingerLevy, ls.StingerLevyFirst, stingerBase));
				}
				Check($"{label}: the real commander is not written to ({ls.Writes} ticks vs control {ctlWrites})", ls.Writes <= ctlWrites);
				Check($"{label}: no whip projectile is left after the levy is gone ({ls.Leftover} {ls.LeftoverNames})", ls.Leftover == 0);
				if (lo != null) Check($"{label}: the levy's whip damage per second is not below the old lash's, within the run-to-run spread ({ls.DmgPerSec:0.0} vs {lo.DmgPerSec:0.0})", ls.DmgPerSec >= lo.DmgPerSec * 0.8);
			}
			// the table the report is made of
			Note("-- SUMMARY (per whip: levy / player)");
			for (int ri = 0; ri < rows.Count; ri++)
			{
				var (p, l) = rows[ri]; Scene o = olds[ri];
				Note($"   {p.Item,-24} old-lash dmg/s {(o != null ? o.DmgPerSec.ToString("0.0") : "-")} (x{(o != null && o.DmgPerSec > 0 ? l.DmgPerSec / o.DmgPerSec : 0):0.00} now)  cost us/swing old {(o != null ? (o.StartUs + o.AiUs + o.DmgUs).ToString("0") : "-")}");
			}
			foreach (var (p, l) in rows)
				Note($"   {p.Item,-24} swings {l.Swings}/{p.Swings}  hits {l.Hits}/{p.Hits}  dmg/hit {l.DmgPerHit:0.0}/{p.DmgPerHit:0.0}  dmg/s {l.DmgPerSec:0.0}/{p.DmgPerSec:0.0}  reach {l.Reach:0}/{p.Reach:0}  segs {l.Segs}/{p.Segs}  tag cmd {l.StingerCmd:0.0}/{p.StingerCmd:0.0} levy {l.StingerLevy:0.0}  cost us/swing {l.StartUs + l.AiUs + l.DmgUs:0.0}");
		}

	}

	/// <summary>Feeds <see cref="WhipRealCheck"/> (inert unless its check is tracking).</summary>
	public class WhipProbeGlobal : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source) { if (WhipRealCheck.Tracking) WhipRealCheck.OnSpawn(projectile); }
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) { if (WhipRealCheck.Tracking) WhipRealCheck.OnHit(projectile, target, hit, damageDone); }
		public override void OnKill(Projectile projectile, int timeLeft) { if (WhipRealCheck.Tracking) WhipRealCheck.OnKill(projectile, timeLeft); }
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) { if (WhipRealCheck.Tracking && WhipRealCheck.IsProbe(projectile)) modifiers.DamageVariationScale *= 0f; }
	}
}
