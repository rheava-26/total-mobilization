using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Movement;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of an army inside a closed boss arena, logged as "[ScalArena]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the summon world check's chain runs it;
	/// LEVY_SCALARENA_ONLY=1 runs it alone). Needs Calamity: the real Supreme Calamitas is summoned over a built arena (a floor and platform rows), builds her own arena box (ArenaWallSystem), and fifteen levies of mixed gear
	/// (five with wings, six on foot, four with a grappling hook) follow a stand-in commander who weaves through the whole box, ground to ceiling and wall to wall, the way a player dodges a bullet hell.
	/// The SAME scene is run twice from the same random stream: once boxed in, and once with the box lifted (open sky). Per run: the share of levy-time pressed against a wall of the box (and the share of it that is NOT
	/// the commander being there), the spread of heights (standard deviation of the levies' Y, and how many bands of the box's height are used), the share of time airborne by gear class, and the damage taken.
	/// The checks assert that the box does not pin the army: wall time, height spread and flight stay near the open-sky run. Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed partial class ScalArenaWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static Vector2 _cmdAt, _cmdPrev; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 270;
		private const string Commander = "ScalArenaTester";
		private static readonly bool Trace = Environment.GetEnvironmentVariable("LEVY_SCALARENA_TRACE") == "1", Trace2 = Environment.GetEnvironmentVariable("LEVY_SCALARENA_TRACE") == "2";
		private static readonly int TraceFrom = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SCALARENA_TRACEFROM"), out int tf) ? tf : 1800;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool only = Environment.GetEnvironmentVariable("LEVY_SCALARENA_ONLY") == "1" || Environment.GetEnvironmentVariable("LEVY_COMBATFIX_ONLY") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_SCALARENA") != "0";
			if (!only && !ship) return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateNPCs() { if (_running) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; }
		public override void PostUpdateProjectiles() { if (_running) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the summon world check's chain calls this).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			if (!ModLoader.TryGetMod("CalamityMod", out _)) { _mod.Logger.Info("[ScalArena] OBSERVED Calamity is not loaded: skipped"); _mod.Logger.Info("[ScalArena] self-check pass=0 fail=0"); return; }
			_running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); if (Environment.GetEnvironmentVariable("LEVY_COMBATFIX_ONLY") != "1") Run(); { string cfp = Environment.GetEnvironmentVariable("LEVY_COMBATFIX_PARTS"); if (Environment.GetEnvironmentVariable("LEVY_COMBATFIX") != "0" && (string.IsNullOrEmpty(cfp) || cfp.Split(',').Contains("scal"))) CombatFixScal(); } }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally { _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			_mod.Logger.Info($"[ScalArena] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ScalArena] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[ScalArena] OBSERVED " + s);
		private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

		// ---------------------------------------------------------------------------------------------- Calamity seams
		private static FieldInfo _revField, _deathField, _boxesField;
		private static Type _boxT; private static PropertyInfo _tl, _br;
		private static bool SetRevenge(bool rev, bool death)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return false;
			if (_revField == null)
			{
				Type w = cal.Code.GetType("CalamityMod.World.CalamityWorld");
				_revField = w.GetField("revenge", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_deathField = w.GetField("death", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			_revField?.SetValue(null, rev); _deathField?.SetValue(null, death);
			return _revField != null;
		}
		private static System.Collections.IList Boxes()
		{
			if (_boxesField == null)
			{
				ModLoader.TryGetMod("CalamityMod", out Mod cal);
				Type sys = cal.Code.GetType("CalamityMod.Systems.Mechanic.ArenaWallSystem"); _boxT = sys.GetNestedType("Box");
				_boxesField = sys.GetField("ActiveBoxes", BindingFlags.Public | BindingFlags.Static);
				_tl = _boxT.GetProperty("TopLeft"); _br = _boxT.GetProperty("BottomRight");
			}
			return _boxesField.GetValue(null) as System.Collections.IList;
		}
		private static void BoxBounds(object box, out Vector2 tl, out Vector2 br) { tl = (Vector2)_tl.GetValue(box); br = (Vector2)_br.GetValue(box); }

		/// <summary>The box shapes Supreme Calamitas gives her arena over a fight (GetArenaSize): her own, the narrow tall shaft of the Soul Seeker phase, the wide flat one of the Cataclysm pair, and the small last-stand box. Dimensions are up, right, down, left in px.</summary>
		internal enum Shape { Natural, Sky, Shaft, Wide, Small }
		private static Shape _shape = Shape.Natural;
		private static void ShapeBox(Shape sh, out Vector4 dims, out Vector2 pos)
		{
			float floorPx = FloorY * 16f + 8f;   // the box's bottom edge sits just inside the floor, so the floor is the arena's floor
			switch (sh)
			{
				case Shape.Shaft: dims = new Vector4(2940f, 850f, 60f, 850f); break;
				case Shape.Wide: dims = new Vector4(1660f, 1330f, 60f, 1330f); break;
				default: dims = new Vector4(400f, 500f, 73f, 500f); break;
			}
			pos = new Vector2(Cx * 16f, floorPx - dims.Z);
		}
		private static void ImposeShape(object box)
		{
			ShapeBox(_shape, out Vector4 dims, out Vector2 pos);
			foreach (var (name, val) in new (string, object)[] { ("boxDimensions", dims), ("NewDimensions", dims), ("position", pos) }) _boxT.GetField(name).SetValue(box, val);
		}

		// ---------------------------------------------------------------------------------------------- scene
		private static Vector2 _lo, _hi; private static bool _haveBox;   // the box bounds of this tick (the last known ones once it is gone)
		private static object _box; private static bool _lifted;

		private static void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); WorldStep.Update(); Housekeeping(); } }

		/// <summary>Natural vanilla spawns go (the army fights whatever hostile it sees); Calamity's own NPCs are the fight. Lifts the box in the open-sky run, and reads its bounds.</summary>
		private static void Housekeeping()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.ModNPC == null) n.active = false; }
			var list = Boxes();
			if (_box == null && list != null && list.Count > 0) _box = list[0];
			if (_box != null)
			{
				if (_shape >= Shape.Shaft) ImposeShape(_box);
				BoxBounds(_box, out _lo, out _hi); _haveBox = true;
				if (_lifted && list != null && list.Contains(_box)) list.Remove(_box);
			}
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 5000) p.statLifeMax = p.statLifeMax2 = 5000;
			p.statLife = p.statLifeMax2;
			Vector2 at = _cmdAt;
			// held in the box the way a player is
			if (_haveBox) { float hw = p.width / 2f, hh = p.height / 2f; at.X = MathHelper.Clamp(at.X, _lo.X + hw, _hi.X - hw); at.Y = MathHelper.Clamp(at.Y, _lo.Y + hh, _hi.Y - hh); }
			p.position = at - new Vector2(p.width / 2f, p.height / 2f);
			p.velocity = at - _cmdPrev; if (p.velocity.Length() > 14f) p.velocity = Vector2.Normalize(p.velocity) * 14f;
			if (Math.Abs(p.velocity.X) > 0.3f) p.direction = Math.Sign(p.velocity.X);
			_cmdPrev = at;
		}

		/// <summary>The commander's weave: wall to wall and ground to ceiling of the box over a few seconds, the way a player slides through a bullet hell.</summary>
		private static Vector2 Weave(int t)
		{
			float w = _haveBox ? (_hi.X - _lo.X) * 0.5f : 60f * 16f, h = _haveBox ? (_hi.Y - _lo.Y) * 0.5f : 60f * 16f;
			float cx = _haveBox ? (_lo.X + _hi.X) * 0.5f : Cx * 16f, cy = _haveBox ? (_lo.Y + _hi.Y) * 0.5f : (FloorY - 40) * 16f;
			float ax = w - 5f * 16f, ay = h - 5f * 16f;           // within five tiles of every wall
			float x = cx + ax * (float)Math.Sin(t * 2f * Math.PI / 1500f);
			float yPhase = 0.5f + 0.5f * (float)Math.Sin(t * 2f * Math.PI / 1100f + 1.3f);   // 0 = ground, 1 = ceiling
			float floorPx = (FloorY - 3) * 16f, topPx = cy - ay;
			float y = MathHelper.Lerp(floorPx, topPx, yPhase);
			return new Vector2(x, y);
		}

		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 215; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 40; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
			// three platform rows, each in two runs with a gap in the middle (a built arena)
			foreach (int rise in new[] { 12, 24, 36 })
				foreach (var (a, b) in new[] { (-62, -14), (14, 62) })
					for (int x = Cx + a; x <= Cx + b; x++) WorldGen.PlaceTile(x, FloorY - rise, TileID.Platforms, true, true, -1, 0);
		}

		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
					else if (!n.friendly && !n.townNPC) n.active = false;
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				var list = Boxes(); if (list != null) list.Clear();
				_box = null; _haveBox = false; _lifted = false; _shape = Shape.Natural;
				SetRevenge(false, false);
				var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null && _savedBossTarget.HasValue) { cfg.BossesTargetSoldiers = _savedBossTarget.Value; _savedBossTarget = null; }
				if (ModContent.TryFind("CalamityMod/SupremeCalamitas", out ModNPC sc)) BossDownedSystem.SetDownedForTest(sc.Type, false);
			}
			catch (Exception) { }
		}
		private static bool? _savedBossTarget;
		private static int _lastAlive = -1, _deathNotes; private static readonly Dictionary<int, (Vector2 pos, Vector2 vel, int life)> _lastSeen = new(); private static readonly HashSet<int> _deathLogged = new(), _deathDone = new(); private static readonly HashSet<long> _hitSeen = new();

		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }

		private enum Cls { Flier, Foot, Hooker }

		/// <summary>An armed Follow levy of one gear class with hit points a boss cannot use up in the window; its body, or null.</summary>
		private static SoldierNPC Levy(int k, Cls cls, Vector2 at)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Box" + k,
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			rec.Gear.TrySet(SoldierGear.SlotWeapon, Make(ItemID.Handgun), rec.AccessorySlots, out _);
			var ammo = Make(ItemID.MusketBall); ammo.stack = 999; rec.Gear.TrySet(SoldierGear.SlotAmmo, ammo, rec.AccessorySlots, out _);
			if (cls == Cls.Flier) rec.Gear.TrySet(SoldierGear.SlotWings, Make(ItemID.AngelWings), rec.AccessorySlots, out _);
			if (cls == Cls.Hooker) rec.Gear.TrySet(SoldierGear.SlotHook, Make(ItemID.GrapplingHook), rec.AccessorySlots, out _);
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			Main.npc[body].lifeMax = Main.npc[body].life = 60000; rec.CurrentHp = 60000;
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		// ---------------------------------------------------------------------------------------------- one run
		private sealed class Acc
		{
			public long Ticks; public long WallTicks, HugTicks, NearTicks, AirTicks, BodyTicks; public readonly long[] HistX = new long[10], HistY = new long[10]; public double SdSum; public long SdN; public readonly HashSet<int> Bands = new();
			public double AltSum;
		}
		private sealed class Result
		{
			public Acc All = new(), Flier = new(), Foot = new(), Hook = new(); public long Damage; public int Hits, Ticks, Lost; public bool BossAlive; public double CmdWall; public long CmdTicks;
			public int Attacks; public long HostileProj;
		}

		private static Result RunScene(bool boxed, int warm, int ticks, Shape shape = Shape.Natural)
		{
			var r = new Result();
			Cleanup(); _shape = shape;
			Terraria.Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			Arena();
			Main.GameMode = GameModeID.Master; Main.hardMode = true;
			SetRevenge(true, true);
			var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null) { _savedBossTarget ??= cfg.BossesTargetSoldiers; cfg.BossesTargetSoldiers = true; }
			ModContent.TryFind("CalamityMod/SupremeCalamitas", out ModNPC sc);
			BossDownedSystem.SetDownedForTest(sc.Type, true);
			_lifted = !boxed; _box = null; _haveBox = false;
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f); _cmdPrev = _cmdAt;
			KeepCommander(); Step(2);
			var levies = new List<(SoldierNPC s, Cls c)>();
			var plan = new List<Cls>(); for (int i = 0; i < 5; i++) plan.Add(Cls.Flier); for (int i = 0; i < 6; i++) plan.Add(Cls.Foot); for (int i = 0; i < 4; i++) plan.Add(Cls.Hooker);
			for (int k = 0; k < plan.Count; k++)
			{
				float off = (k - plan.Count / 2) * 4f;
				var s = Levy(k, plan[k], new Vector2((Cx + off) * 16f, FloorY * 16f)); if (s != null) levies.Add((s, plan[k]));
			}
			Step(90);
			_lastAlive = -1;
			Note($"after settling: levies active {levies.Count(l => l.s.NPC.active)} of {levies.Count}");
			Vector2 spawnAt = new Vector2(Cx * 16f, (FloorY - 10) * 16f);
			if (shape == Shape.Sky) spawnAt = new Vector2(Cx * 16f, (FloorY - 50) * 16f);   // summoned over a sky platform arena: her own box, the real ground 50 tiles under its middle
			else if (shape != Shape.Natural) { ShapeBox(shape, out Vector4 sd, out Vector2 sp0); spawnAt = sp0 + new Vector2((sd.Y - sd.W) * 0.5f, (sd.Z - sd.X) * 0.5f); }
			int bi = NPC.NewNPC(new EntitySource_WorldEvent("ScalArenaCheck"), (int)spawnAt.X, (int)spawnAt.Y, sc.Type, 1);
			if (bi < 0 || bi >= Main.maxNPCs) { Check("Supreme Calamitas spawns", false); return r; }
			NPC boss = Main.npc[bi]; boss.timeLeft = 99999; boss.netOffset = Vector2.Zero;
			_hitSeen.Clear(); _lastSeen.Clear(); _deathLogged.Clear(); _deathDone.Clear();
			int t0 = 0;
			for (int t = 0; t < warm + ticks; t++)
			{
				_cmdAt = Weave(t);
				foreach (var (s0, _) in levies) { NPC n0 = s0.NPC; if (n0.active) { n0.dontTakeDamage = true; n0.life = n0.lifeMax; s0.Record.CurrentHp = n0.lifeMax; } }
				Step(1);
				if (!boss.active || boss.type != sc.Type) { r.BossAlive = false; break; }
				boss.timeLeft = 99999;
				r.BossAlive = true;
				foreach (var (sd, cd) in levies)
				{
					NPC nd = sd.NPC; int w = nd.whoAmI;
					if (nd.active) { _lastSeen[w] = (nd.Center, nd.velocity, nd.life); continue; }
					if (!_deathLogged.Add(w * 100000 + t)) continue;
					if (_lastSeen.TryGetValue(w, out var ls) && _deathNotes++ < 12 && !_deathDone.Contains(w))
					{
						_deathDone.Add(w);
						int nh = 0; Projectile near = null; float bd = 1e9f; for (int pj = 0; pj < Main.maxProjectiles; pj++) { var pr = Main.projectile[pj]; if (pr.active && pr.hostile && pr.damage > 0) { float d = Vector2.Distance(pr.Center, ls.pos); if (d < bd) { bd = d; near = pr; } nh++; } }
						Note($"DEATH t={t} {cd} at ({(ls.pos.X - _lo.X) / 16f:0},{(ls.pos.Y - _lo.Y) / 16f:0}) of box {(_hi.X - _lo.X) / 16f:0}x{(_hi.Y - _lo.Y) / 16f:0} v=({ls.vel.X:0.0},{ls.vel.Y:0.0}) life {ls.life}; nearest hostile projectile {(near == null ? "-" : near.ModProjectile?.Name ?? near.type.ToString())} at {bd / 16f:0.0} tiles dmg {near?.damage}; state {sd.State}");
					}
				}
				if (t % 60 == 0 || t < 5)
				{
					int alive = levies.Count(l => l.s.NPC.active);
					if (alive != _lastAlive) { _lastAlive = alive; _mod.Logger.Info($"[ScalArena] OBSERVED t={t} levies active {alive} of {levies.Count}; boss life {boss.life}/{boss.lifeMax} ai0={boss.ai[0]:0} ai1={boss.ai[1]:0} box {(_haveBox ? "up" : "not yet")}; gone: " + string.Join(",", levies.Where(l => !l.s.NPC.active).Take(4).Select(l => l.s.State + "/hp" + l.s.Record.CurrentHp))); }
				}
				if (!_haveBox) continue;
				if (t < warm) continue;
				if (t0 == 0) t0 = t;
				r.Ticks++;
				// commander against a wall
				{
					Player p = Main.player[0]; r.CmdTicks++;
					if (p.Left.X - _lo.X < 8f || _hi.X - p.Right.X < 8f || p.TopLeft.Y - _lo.Y < 8f || _hi.Y - p.BottomRight.Y < 8f) r.CmdWall += 1;
				}
				var ys = new List<double>();
				foreach (var (s, c) in levies)
				{
					NPC n = s.NPC;
					if (!n.active) { continue; }
					// hits taken: a hostile projectile's hitbox on the body (counted once per projectile and levy); the harness keeps the army alive (untouchable) for the whole window, so what is measured is where it stood
					Rectangle body = n.Hitbox;
					for (int pj = 0; pj < Main.maxProjectiles; pj++)
					{
						Projectile pr = Main.projectile[pj];
						if (!pr.active || !pr.hostile || pr.damage <= 0 || !pr.Hitbox.Intersects(body)) continue;
						if (!_hitSeen.Add(((long)pr.identity << 20) ^ ((long)pr.type << 8) ^ (long)n.whoAmI)) continue;
						r.Hits++; r.Damage += pr.damage;
					}
					Acc a = c == Cls.Flier ? r.Flier : c == Cls.Foot ? r.Foot : r.Hook;
					foreach (Acc acc in new[] { r.All, a })
					{
						acc.BodyTicks++;
						// a wall of the box within 6 px; a hug is the same while the commander is more than ten tiles from that wall
						Player cm = Main.player[0];
						bool wallL = n.Left.X - _lo.X < 6f, wallR = _hi.X - n.Right.X < 6f, wallT = n.TopLeft.Y - _lo.Y < 6f, wallB = _hi.Y - n.BottomRight.Y < 6f;
						if (wallL || wallR || wallT) acc.WallTicks++;   // the bottom wall is the arena's floor: standing on it is not pinned to it
						{
							bool nL = n.Left.X - _lo.X < 48f, nR = _hi.X - n.Right.X < 48f, nT = n.TopLeft.Y - _lo.Y < 48f, nB = _hi.Y - n.BottomRight.Y < 48f;
							if ((nL && cm.Left.X - _lo.X > 192f) || (nR && _hi.X - cm.Right.X > 192f) || (nT && cm.TopLeft.Y - _lo.Y > 192f) || (nB && _hi.Y - cm.BottomRight.Y > 192f)) acc.NearTicks++;
							acc.HistX[Math.Clamp((int)((n.Center.X - _lo.X) / Math.Max(1f, _hi.X - _lo.X) * 10f), 0, 9)]++;
							acc.HistY[Math.Clamp((int)((n.Center.Y - _lo.Y) / Math.Max(1f, _hi.Y - _lo.Y) * 10f), 0, 9)]++;
						}
						if ((wallL && cm.Left.X - _lo.X > 160f) || (wallR && _hi.X - cm.Right.X > 160f) || (wallT && cm.TopLeft.Y - _lo.Y > 160f)) acc.HugTicks++;
						bool stand = Collision.SolidCollision(n.position + new Vector2(0f, n.height), n.width, 4, true);
						if (!stand) acc.AirTicks++;
						acc.AltSum += (FloorY * 16f - n.Bottom.Y) / 16f;
					}
					ys.Add(n.Center.Y / 16.0);
					if (_haveBox) { float rel = (n.Center.Y - _lo.Y) / Math.Max(1f, _hi.Y - _lo.Y); a.Bands.Add(Math.Clamp((int)(rel * 8f), 0, 7)); r.All.Bands.Add(Math.Clamp((int)(rel * 8f), 0, 7) + 8 * (int)c); }
				}
				if (ys.Count > 1) { double m = ys.Average(); double sd = Math.Sqrt(ys.Sum(y => (y - m) * (y - m)) / ys.Count); r.All.SdSum += sd; r.All.SdN++; }
				if (Trace2 && t >= TraceFrom && t < TraceFrom + 24)
					foreach (var (s, c) in levies.Where((l, idx) => l.s.NPC.active && (idx == 3 || idx == 8 || idx == 13)))
					{
						NPC n = s.NPC; _mod.Logger.Info($"[ScalArena] TICK t={t} {c} bottom={(n.Bottom.Y - _lo.Y) / 16f:0.00} of {(_hi.Y - _lo.Y) / 16f:0.0} gap={(_hi.Y - n.Bottom.Y):0.0}px v=({n.velocity.X:0.00},{n.velocity.Y:0.00}) fly={s.IsFlying} wing={s.WingTime} goal={(s.HasMoveGoal ? ((s.MoveGoal.Y - _lo.Y) / 16f).ToString("0.0") : "-")} why={s.StillReason()} state={s.State} stand={Collision.SolidCollision(n.position + new Vector2(0f, n.height), n.width, 4, true)} onBoxFloor={s.OnBoxFloor} held={HeldN} checked={CheckedN} noGrav={n.noGravity} collide={!n.noTileCollide}");
					}
				if (Trace && (t - warm) % 120 == 0)
					foreach (var (s, c) in levies.Where((l, idx) => l.s.NPC.active && (idx == 0 || idx == 5 || idx == 11 || idx == 14)))
					{
						NPC n = s.NPC; Player cm = Main.player[0];
						_mod.Logger.Info($"[ScalArena] TRACE {(boxed ? "box" : "open")} t={t} {c} pos=({(n.Center.X - _lo.X) / 16f:0},{(n.Center.Y - _lo.Y) / 16f:0}) of box {(_hi.X - _lo.X) / 16f:0}x{(_hi.Y - _lo.Y) / 16f:0} cmd=({(cm.Center.X - _lo.X) / 16f:0},{(cm.Center.Y - _lo.Y) / 16f:0}) boss=({(boss.Center.X - _lo.X) / 16f:0},{(boss.Center.Y - _lo.Y) / 16f:0}) fly={s.IsFlying} goal={(s.HasMoveGoal ? ((s.MoveGoal.X - _lo.X) / 16f).ToString("0") + "," + ((s.MoveGoal.Y - _lo.Y) / 16f).ToString("0") : "-")} state={s.State} why={s.StillReason()} v=({n.velocity.X:0.0},{n.velocity.Y:0.0})");
					}
			}
			int hostile = 0; for (int i = 0; i < Main.maxProjectiles; i++) { var pr = Main.projectile[i]; if (pr.active && pr.hostile) hostile++; }
			r.HostileProj = hostile;
			r.Lost = levies.Count(l => !l.s.NPC.active);
			return r;
		}

		private static long HeldN => global::Levy.Common.Compat.Calamity.ArenaBoxes.Held;
		private static long CheckedN => global::Levy.Common.Compat.Calamity.ArenaBoxes.Checked;
		private static string Hist(long[] h) { long n = Math.Max(1, h.Sum()); return string.Join("/", h.Select(v => (100 * v / n).ToString())); }

		private static string Line(string name, Result r)
		{
			double wall = 100.0 * r.All.WallTicks / Math.Max(1, r.All.BodyTicks), hug = 100.0 * r.All.HugTicks / Math.Max(1, r.All.BodyTicks), air = 100.0 * r.All.AirTicks / Math.Max(1, r.All.BodyTicks);
			double sd = r.All.SdSum / Math.Max(1, r.All.SdN);
			double fAir = 100.0 * r.Flier.AirTicks / Math.Max(1, r.Flier.BodyTicks), gAir = 100.0 * r.Foot.AirTicks / Math.Max(1, r.Foot.BodyTicks), hAir = 100.0 * r.Hook.AirTicks / Math.Max(1, r.Hook.BodyTicks);
			double fWall = 100.0 * r.Flier.WallTicks / Math.Max(1, r.Flier.BodyTicks), gWall = 100.0 * r.Foot.WallTicks / Math.Max(1, r.Foot.BodyTicks), hWall = 100.0 * r.Hook.WallTicks / Math.Max(1, r.Hook.BodyTicks);
			double fAlt = r.Flier.AltSum / Math.Max(1, r.Flier.BodyTicks), gAlt = r.Foot.AltSum / Math.Max(1, r.Foot.BodyTicks), hAlt = r.Hook.AltSum / Math.Max(1, r.Hook.BodyTicks);
			return $"{name}: {r.Ticks} ticks, boss alive {r.BossAlive}; wall {F(wall)}% (hug {F(hug)}%; fliers {F(fWall)}%, foot {F(gWall)}%, hookers {F(hWall)}%), commander at a wall {F(100.0 * r.CmdWall / Math.Max(1, r.CmdTicks))}%; " +
				$"height spread sd {F(sd)} tiles; airborne {F(air)}% (fliers {F(fAir)}%, foot {F(gAir)}%, hookers {F(hAir)}%); mean altitude fliers {F(fAlt)}, foot {F(gAlt)}, hookers {F(hAlt)} tiles; bands used {r.Flier.Bands.Count} / {r.Foot.Bands.Count} / {r.Hook.Bands.Count} of 8; near-wall (3 tiles, commander 12+ tiles off that wall) {F(100.0 * r.All.NearTicks / Math.Max(1, r.All.BodyTicks))}%; share of levy time by tenth of width {Hist(r.All.HistX)} and of height (top to bottom) {Hist(r.All.HistY)}; hostile projectile contacts {r.Hits} (damage on paper {r.Damage}), lost {r.Lost}; hostile projectiles at the end {r.HostileProj}";
		}

		private static void Run()
		{
			int warm = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SCALARENA_WARM"), out int w) ? w : 900;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SCALARENA_TICKS"), out int tk) ? tk : 3600;
			string want = Environment.GetEnvironmentVariable("LEVY_SCALARENA_SHAPES") ?? "Natural,Sky,Small,Shaft,Wide";
			foreach (string name in want.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				if (!Enum.TryParse(name.Trim(), out Shape shape)) continue;
				var boxed = RunScene(true, warm, ticks, shape);
				Note(Line(shape + " boxed", boxed));
				Cleanup();
				var open = RunScene(false, warm, ticks, shape);
				Note(Line(shape + " open sky", open));
				Cleanup();
				if (boxed.Ticks < ticks / 2 || open.Ticks < ticks / 2) { Check($"{shape}: both runs measured (boxed {boxed.Ticks} ticks, open {open.Ticks})", false); continue; }
				ReportChecks(shape.ToString(), boxed, open);
			}
		}

		private static void ReportChecks(string tag, Result boxed, Result open)
		{
			double wall = 100.0 * boxed.All.WallTicks / Math.Max(1, boxed.All.BodyTicks);
			double hug = 100.0 * boxed.All.HugTicks / Math.Max(1, boxed.All.BodyTicks);
			double sdB = boxed.All.SdSum / Math.Max(1, boxed.All.SdN), sdO = open.All.SdSum / Math.Max(1, open.All.SdN);
			double fAirB = 100.0 * boxed.Flier.AirTicks / Math.Max(1, boxed.Flier.BodyTicks), fAirO = 100.0 * open.Flier.AirTicks / Math.Max(1, open.Flier.BodyTicks);
			Check($"{tag}: the arena box does not pin the army: wall time {F(wall)}% and hugging with the commander far off {F(hug)}%", wall <= 25.0 && hug <= 8.0);
			// The open-sky control has the world's ground under it. In the Sky shape (the box floats 33 tiles over empty air) that ground is far below the box floor, so the control's foot soldiers sit 33 tiles lower than any body in the box
			// can stand, and in the Small shape the control is not held by a ceiling a few tiles up: its spread is not a spread the box can match. There the box is judged by the height bands the army reaches (the control's count is the bar) instead of by spread.
			if (tag == "Sky" || tag == "Small") { int bB = boxed.All.Bands.Count, bO = open.All.Bands.Count; Check($"{tag}: the army reaches the height bands in the box that it reaches in the open: {bB} against {bO} (height spread {F(sdB)} against {F(sdO)} tiles; the control is not held by the box's height)", bB >= 0.8 * bO); }
			else Check($"{tag}: the army uses the box's height as it uses open sky: height spread {F(sdB)} tiles against {F(sdO)} in the open", sdB >= 0.6 * sdO);
			Check($"{tag}: levies with wings fly in the box: airborne {F(fAirB)}% against {F(fAirO)}% in the open", fAirB >= 0.6 * fAirO);
		}
	}
}
