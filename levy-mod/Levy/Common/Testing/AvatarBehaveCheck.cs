using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Compat.Wotg;
using Levy.Common.Dialogue;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of an army in the Avatar of Emptiness fight (Wrath of the Gods), logged as "[AvatarBehave]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the summon world check's chain
	/// runs it; LEVY_AVBEHAVE_ONLY=1 runs it alone). Needs NoxusBoss: the REAL Avatar of Emptiness is spawned over a built arena, put into one attack (its own state machine is pushed the state, and for the dimension attacks the
	/// SendPlayerToMyUniverse state that really moves the player to the middle of the world with tiles switched off), and fifteen levies of mixed gear (five with wings, six on foot, four with a grappling hook) follow a stand-in
	/// commander who hovers and weaves where the fight leaves him, being moved by the Avatar exactly as a player is (the stand-in's position is adopted after every Main.Update).
	/// Each attack is run twice from the same random seed: with the army's Avatar behaviour off (what the army did before) and on. Per run it reports the distance to the commander, the share of levy-time stranded (more than 40 tiles
	/// from the commander) and lost (more than 100), the share falling (not held up, speed 8+ down), the share of the fliers aloft and of everybody aloft, hits taken (a hostile projectile's hitbox on the body, counted once
	/// per projectile and levy; the harness keeps every levy alive so it measures where it stood), the share of the army that is within 40 tiles of the commander two and ten seconds after he was moved, and the lines spoken a minute.
	/// Throwaway soldiers only; nothing is saved.
	/// </summary>
	public sealed class AvatarBehaveCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod; private static Vector2 _cmdAt, _anchor; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 270;
		private const string Commander = "AvatarBehaveTester";
		private static readonly bool Trace = Environment.GetEnvironmentVariable("LEVY_AVBEHAVE_TRACE") == "1";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			bool only = Environment.GetEnvironmentVariable("LEVY_AVBEHAVE_ONLY") == "1";
			bool ship = Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") == "1" && Environment.GetEnvironmentVariable("LEVY_AVBEHAVE_CHECK") != "0";
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
			if (!ModLoader.TryGetMod("NoxusBoss", out _) || !WotgScenes.Installed || WotgScenes.AvatarType < 0) { _mod.Logger.Info("[AvatarBehave] OBSERVED NoxusBoss is not loaded: skipped"); _mod.Logger.Info("[AvatarBehave] self-check pass=0 fail=0"); return; }
			_running = true;
			On_Player.hook_KillMe killDet = (On_Player.orig_KillMe orig, Player self, Terraria.DataStructures.PlayerDeathReason reason, double dmg, int dir, bool pvp) => { if (self.whoAmI != 0) { orig(self, reason, dmg, dir, pvp); return; } _cmdKills++; };
			On_Player.KillMe += killDet;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time; bool? on0 = AvatarFight.TestOn;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally { On_Player.KillMe -= killDet; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); AvatarFight.TestOn = on0; AvatarFight.TestZones = null; WotgScenes.TestAvatarPercent = null; WotgScenes.TestVoice = null; Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			_mod.Logger.Info($"[AvatarBehave] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[AvatarBehave] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[AvatarBehave] OBSERVED " + s);
		private static string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
		private static string P(double v) => v.ToString("0", CultureInfo.InvariantCulture) + "%";

		// ---------------------------------------------------------------------------------------------- the mod's seams
		private static Type _avT; private static PropertyInfo _smP, _phaseP, _tilesP, _dimP, _stateP; private static Type _aiEnum;
		private static void Seams(Mod nox)
		{
			if (_avT != null) return;
			_avT = nox.Code.GetType("NoxusBoss.Content.NPCs.Bosses.Avatar.SecondPhaseForm.AvatarOfEmptiness");
			_smP = _avT.GetProperty("StateMachine", BindingFlags.Public | BindingFlags.Instance); _phaseP = _avT.GetProperty("CurrentPhase", BindingFlags.Public | BindingFlags.Instance);
			_stateP = _avT.GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance);
			_aiEnum = _avT.GetNestedType("AvatarAIType");
			_tilesP = nox.Code.GetType("NoxusBoss.Core.World.TileDisabling.TileDisablingSystem").GetProperty("TilesAreUninteractable", BindingFlags.Public | BindingFlags.Static);
			_dimP = nox.Code.GetType("NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.AvatarOfEmptinessSky").GetProperty("Dimension", BindingFlags.Public | BindingFlags.Static);
		}
		private static int SuckMax() => Convert.ToInt32(_avT.GetProperty("SendPlayerToMyUniverse_MaximumSuckTime", BindingFlags.Public | BindingFlags.Static).GetValue(null));

		/// <summary>Replaces the Avatar's state stack with these states (first = current), the one named <paramref name="timed"/> already <paramref name="time"/> ticks into its run.</summary>
		private static void ForceStates(NPC boss, string[] states, int phase, string timed, int time)
		{
			object sm = _smP.GetValue(boss.ModNPC);
			object stack = Member(sm, "StateStack");
			object reg = Member(sm, "StateRegistry");
			MethodInfo clear = stack.GetType().GetMethod("Clear"), push = stack.GetType().GetMethod("Push"), get = reg.GetType().GetMethod("get_Item");
			clear.Invoke(stack, null);
			for (int i = states.Length - 1; i >= 0; i--)
			{
				object st = get.Invoke(reg, new[] { Enum.Parse(_aiEnum, states[i]) });
				SetMember(st, "Time", states[i] == timed ? time : 0);
				push.Invoke(stack, new[] { st });
			}
			_phaseP.SetValue(boss.ModNPC, phase);
			// the awakening has not run: what it would have raised
			foreach (var (n, v) in new (string, object)[] { ("NeckAppearInterpolant", 1f), ("HeadOpacity", 1f), ("LeftFrontArmScale", 1f), ("RightFrontArmScale", 1f), ("LeftFrontArmOpacity", 1f), ("RightFrontArmOpacity", 1f), ("LilyScale", 1f) })
				try { _avT.GetProperty(n, BindingFlags.Public | BindingFlags.Instance)?.SetValue(boss.ModNPC, v); } catch (Exception) { }
			try { _avT.GetProperty("HasSentPlayersThroughVortex", BindingFlags.Public | BindingFlags.Instance)?.SetValue(boss.ModNPC, true); } catch (Exception) { }
		}
		private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		private static object Member(object o, string name)
		{
			Type t = o.GetType();
			PropertyInfo p = t.GetProperty(name, Any); if (p != null) return p.GetValue(o);
			FieldInfo f = t.GetField(name, Any); if (f != null) return f.GetValue(o);
			throw new MissingMemberException(t.Name, name);
		}
		private static void SetMember(object o, string name, object v)
		{
			Type t = o.GetType();
			PropertyInfo p = t.GetProperty(name, Any); if (p != null) { p.SetValue(o, v); return; }
			FieldInfo f = t.GetField(name, Any); if (f != null) { f.SetValue(o, v); return; }
			throw new MissingMemberException(t.Name, name);
		}
		private static string CurrentState(NPC boss) { try { return _stateP.GetValue(boss.ModNPC)?.ToString(); } catch (Exception) { return null; } }

		private static void Clear()
		{
			try { _tilesP?.SetValue(null, false); } catch (Exception) { }
			try { _dimP?.SetValue(null, null); } catch (Exception) { }
		}

		// ---------------------------------------------------------------------------------------------- scene
		private static void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); WorldStep.Update(); AfterUpdate(); Housekeeping(); } }
		private static int _shifts, _lastShiftTick = -100000; private static int _tick;

		private static void Housekeeping()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.ModNPC == null) n.active = false; }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 5000) p.statLifeMax = p.statLifeMax2 = 5000;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f);
			p.velocity = Vector2.Zero; p.wingTime = p.wingTimeMax;
		}

		/// <summary>After the world has updated: the Avatar may have moved the commander (his Center is set to the middle of the world, a portal teleports him): the stand-in is where the fight put him, and weaves from there.</summary>
		private static void AfterUpdate()
		{
			Player p = Main.player[0];
			if (Vector2.Distance(p.Center, _cmdAt) > 400f) { _shifts++; _lastShiftTick = _tick; _cmdAt = p.Center; _anchor = p.Center; if (Trace) _mod.Logger.Info($"[AvatarBehave] TRACE t={_tick} the commander was moved to tile ({p.Center.X / 16f:0},{p.Center.Y / 16f:0}) state {(_boss != null && _boss.active ? CurrentState(_boss) : "-")}"); }
		}
		private static NPC _boss; private static int _cmdKills, _goneNotes; private static readonly HashSet<int> _goneLogged = new(); private static readonly Dictionary<int, (Vector2 pos, Vector2 vel, int life)> _seen = new();

		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 215; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 40; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void Cleanup()
		{
			try
			{
				Clear(); AvatarFight.TestVoid = null;
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					if (n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
					else if (!n.friendly && !n.townNPC) n.active = false;
					else if (n.ModNPC?.Mod?.Name == "NoxusBoss") n.active = false;
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				_boss = null;
				if (ModContent.TryFind("NoxusBoss/AvatarOfEmptiness", out ModNPC av)) BossDownedSystem.SetDownedForTest(av.Type, false);
				var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null && _savedBossTarget.HasValue) { cfg.BossesTargetSoldiers = _savedBossTarget.Value; _savedBossTarget = null; }
			}
			catch (Exception) { }
		}
		private static bool? _savedBossTarget;

		private static Item Make(int type) { var i = new Item(); i.SetDefaults(type); return i; }
		private enum Cls { Flier, Foot, Hooker }

		private static SoldierNPC Levy(int k, Cls cls, Vector2 at)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Avatar" + k,
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
		private sealed class Acc { public long Ticks, Far, Lost, Fall, Aloft, Inside; public double Dist; }
		private sealed class Result
		{
			public Acc All = new(), Flier = new(), Foot = new(), Hook = new(); public int Hits, Ticks, Lost, Shifts, AliveAtEnd; public long Damage; public double Near2s, Near10s; public int Near2sN, Near10sN;
			public int Lines, Floating, Reforced, CmdKills, Moments, Dropped, Voice, FightLines; public string Telemetry = ""; public long InSphere; public bool BossAlive; public readonly HashSet<string> States = new();
			public readonly Dictionary<string, long> StateTicks = new(); public readonly Dictionary<string, int> HitBy = new(), ZoneTicks = new(), HitState = new(); public long Urgent;
		}

		private sealed record Scn(string Name, int Phase, string[] States, int Ticks, bool Send, bool Zone = false, int Up = 0, bool Portal = false);

		private static Result RunScene(Scn sc, int variant)
		{
			var r = new Result();
			bool behave = variant > 0; WotgScenes.TestVoice = false; Cleanup(); AvatarFight.TestVoid = sc.Portal ? true : null; AvatarFight.TestOn = behave; AvatarFight.TestZones = variant == 1 ? false : null; WotgScenes.TestAvatarPercent = behave ? null : 100;   // off = the chatter the army had before (every line of the fight pools at the old rate)
			Terraria.Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			Arena();
			Main.GameMode = GameModeID.Master; Main.hardMode = true;
			var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg != null) { _savedBossTarget ??= cfg.BossesTargetSoldiers; cfg.BossesTargetSoldiers = true; }
			ModContent.TryFind("NoxusBoss/AvatarOfEmptiness", out ModNPC av);
			BossDownedSystem.SetDownedForTest(av.Type, true);
			_shifts = 0; _lastShiftTick = -100000; _tick = 0; _cmdKills = 0; SoldierNPC.AvFlyTicks = SoldierNPC.AvCarryTicks = SoldierNPC.AvHoldTicks = SoldierNPC.AvRejoins = SoldierNPC.AvYields = 0; Array.Clear(SoldierNPC.AvWhy); _goneNotes = 0; _goneLogged.Clear(); _seen.Clear();
			_anchor = new Vector2(Cx * 16f, (FloorY - 12 - sc.Up) * 16f); _cmdAt = _anchor;
			KeepCommander(); Step(2);
			var levies = new List<(SoldierNPC s, Cls c)>();
			var plan = new List<Cls>(); for (int i = 0; i < 5; i++) plan.Add(Cls.Flier); for (int i = 0; i < 6; i++) plan.Add(Cls.Foot); for (int i = 0; i < 4; i++) plan.Add(Cls.Hooker);
			for (int k = 0; k < plan.Count; k++)
			{
				float off = (k - plan.Count / 2) * 3f;
				var s = Levy(k, plan[k], new Vector2((Cx + off) * 16f, FloorY * 16f)); if (s != null) levies.Add((s, plan[k]));
			}
			Step(60);
			// the army walks up to the commander on the ground, then the fight starts
			Vector2 spawnAt = new Vector2(Cx * 16f, _anchor.Y - 43f * 16f);
			int bi = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("AvatarBehaveCheck"), (int)spawnAt.X, (int)spawnAt.Y, av.Type, 1);
			if (bi < 0 || bi >= Main.maxNPCs) { Check("the Avatar of Emptiness spawns", false); return r; }
			NPC boss = Main.npc[bi]; _boss = boss; boss.timeLeft = 99999; boss.netOffset = Vector2.Zero;
			int timedAt = sc.Send ? SuckMax() - 100 : 0;
			ForceStates(boss, sc.States, sc.Phase, sc.Send ? "SendPlayerToMyUniverse" : null, timedAt);
			int lines0 = WotgScenes.Log.Count, fl0 = DialogueSystem.FloatingDelivered, mom0 = WotgScenes.MomentsStarted, drop0 = WotgScenes.CuesDropped, voice0 = WotgScenes.VoiceTicks, fight0 = WotgScenes.FightLines;
			var hitSeen = new HashSet<long>();
			var nearCheck = new List<(int at, bool two)>();
			int shiftsSeen = 0, lastForce = -1000;
			string[] roots = sc.States.Where(x => x != "SendPlayerToMyUniverse").Select(x => x.Contains('_') ? x.Substring(0, x.IndexOf('_')) : x).Distinct().ToArray();
			for (int t = 0; t < sc.Ticks; t++)
			{
				_tick = t;
				if (sc.Portal && t == 100)
				{
					// a dark portal moves the player with Player.Teleport (DarkPortal.cs): 150 tiles along, in the dimension
					_anchor += new Vector2(150f * 16f, 0f); _cmdAt = _anchor + WeaveOffset(t);
					Main.player[0].Teleport(_cmdAt - new Vector2(Main.player[0].width / 2f, Main.player[0].height / 2f), TeleportationStyleID.Portal);
					_shifts++; _lastShiftTick = t;
				}
				_cmdAt = _anchor + WeaveOffset(t);
				foreach (var (s0, _) in levies) { NPC n0 = s0.NPC; if (n0.active) { n0.dontTakeDamage = true; n0.life = n0.lifeMax; s0.Record.CurrentHp = n0.lifeMax; } }
				Step(1);
				if (!boss.active || boss.type != av.Type) { r.BossAlive = false; break; }
				boss.timeLeft = 99999; boss.life = boss.lifeMax; r.BossAlive = true;
				string st = CurrentState(boss) ?? "?";
				if (t - lastForce > 30 && sc.States[0] != "ResetCycle" && st != "SendPlayerToMyUniverse" && !st.StartsWith("Teleport") && !roots.Any(rt => st.StartsWith(rt))) { ForceStates(boss, sc.States.Where(x => x != "SendPlayerToMyUniverse").ToArray(), sc.Phase, null, 0); lastForce = t; r.Reforced++; st = CurrentState(boss) ?? "?"; }
				r.States.Add(st); r.StateTicks[st] = r.StateTicks.GetValueOrDefault(st) + 1;
				r.Ticks++;
				Player cm = Main.player[0];
				foreach (var z in AvatarFight.Zones) r.ZoneTicks[z.Why] = r.ZoneTicks.GetValueOrDefault(z.Why) + 1;
				if (_shifts > shiftsSeen) { shiftsSeen = _shifts; nearCheck.Add((_lastShiftTick + 120, true)); nearCheck.Add((_lastShiftTick + 600, false)); r.Shifts++; }
				for (int q = nearCheck.Count - 1; q >= 0; q--)
				{
					if (nearCheck[q].at != t) continue;
					int al = levies.Count(l => l.s.NPC.active), nr = levies.Count(l => l.s.NPC.active && Vector2.Distance(l.s.NPC.Center, cm.Center) < 40f * 16f);
					if (nearCheck[q].two) { r.Near2s += 100.0 * nr / Math.Max(1, al); r.Near2sN++; } else { r.Near10s += 100.0 * nr / Math.Max(1, al); r.Near10sN++; }
					nearCheck.RemoveAt(q);
				}
				foreach (var (s, c) in levies)
				{
					NPC n = s.NPC;
					if (!n.active)
					{
						if (_goneLogged.Add(n.whoAmI * 100000 + (int)(sc.Name.GetHashCode() & 0xff) * 0) && _goneNotes++ < 8 && _seen.TryGetValue(n.whoAmI, out var ls))
							Note($"GONE t={t} {c} state {st} void={AvatarFight.Void} last at ({(ls.pos.X - cm.Center.X) / 16f:0},{(ls.pos.Y - cm.Center.Y) / 16f:0}) of the commander, v=({ls.vel.X:0.0},{ls.vel.Y:0.0}) life {ls.life} dead={s.Record?.Dead} avatar at ({(boss.Center.X - cm.Center.X) / 16f:0},{(boss.Center.Y - cm.Center.Y) / 16f:0}) vy {boss.velocity.Y:0}");
						continue;
					}
					_seen[n.whoAmI] = (n.Center, n.velocity, n.life);
					Rectangle body = n.Hitbox;
					for (int pj = 0; pj < Main.maxProjectiles; pj++)
					{
						Projectile pr = Main.projectile[pj];
						if (!pr.active || !pr.hostile) continue;
						if (pr.ModProjectile?.Name == "AnnihilationSphere") { if (Vector2.Distance(pr.Center, n.Center) < pr.width * 0.38f) r.InSphere++; continue; }
						if (pr.damage <= 0 || !pr.Hitbox.Intersects(body)) continue;
						if (!hitSeen.Add(((long)pr.identity << 20) ^ ((long)pr.type << 8) ^ (long)n.whoAmI)) continue;
						r.Hits++; r.Damage += pr.damage; string hn = pr.ModProjectile?.Name ?? pr.type.ToString(); r.HitBy[hn] = r.HitBy.GetValueOrDefault(hn) + 1; r.HitState[st] = r.HitState.GetValueOrDefault(st) + 1;
					}
					Acc a = c == Cls.Flier ? r.Flier : c == Cls.Foot ? r.Foot : r.Hook;
					float d = Vector2.Distance(n.Center, cm.Center) / 16f;
					bool stand = !AvatarFight.Void && Collision.SolidCollision(n.position + new Vector2(0f, n.height), n.width, 4, true);
					foreach (Acc acc in new[] { r.All, a })
					{
						acc.Ticks++; acc.Dist += d;
						if (d > 40f) acc.Far++; if (d > 100f) acc.Lost++;
						if (n.velocity.Y >= 8f) acc.Fall++;
						if (!stand) acc.Aloft++;
					}
				}
				if (Trace && t % 120 == 0) foreach (var (fs, fc) in levies.Where(l => l.s.NPC.active && Vector2.Distance(l.s.NPC.Center, cm.Center) > 40f * 16f).Take(3)) _mod.Logger.Info($"[AvatarBehave] TRACE far t={t} {fc} d={Vector2.Distance(fs.NPC.Center, cm.Center) / 16f:0} state={fs.State} goal={(fs.HasMoveGoal ? ((fs.MoveGoal.X - fs.NPC.Center.X) / 16f).ToString("0") + "," + ((fs.MoveGoal.Y - fs.NPC.Center.Y) / 16f).ToString("0") : "-")} blocked={fs.MoveBlocked} flying={fs.IsFlying} v=({fs.NPC.velocity.X:0.0},{fs.NPC.velocity.Y:0.0}) at ({(fs.NPC.Center.X - cm.Center.X) / 16f:0},{(fs.NPC.Center.Y - cm.Center.Y) / 16f:0}) cmdY {(cm.Center.Y / 16f):0}");
				if (Trace && (t % 120 == 0 || (t >= 100 && t < 240 && t % 10 == 0 && st.StartsWith("RealityShatter")))) _mod.Logger.Info($"[AvatarBehave] TRACE t={t} state={st} void={AvatarFight.Void} zones={AvatarFight.Zones.Count} avatarDx={(boss.Center.X - cm.Center.X) / 16f:0} zone[{string.Join(";", AvatarFight.Zones.Select(z => z.Why + " x " + ((z.Rect.Left - cm.Center.X) / 16f).ToString("0") + ".." + ((z.Rect.Right - cm.Center.X) / 16f).ToString("0") + " y " + ((z.Rect.Top - cm.Center.Y) / 16f).ToString("0") + ".." + ((z.Rect.Bottom - cm.Center.Y) / 16f).ToString("0")))}] cmd=({cm.Center.X / 16f:0},{cm.Center.Y / 16f:0}) levies: " + string.Join(" ", levies.Where((l, i) => i % 3 == 0 && l.s.NPC.active).Select(l => $"{l.c.ToString()[0]}({(l.s.NPC.Center.X - cm.Center.X) / 16f:0},{(l.s.NPC.Center.Y - cm.Center.Y) / 16f:0})v{l.s.NPC.velocity.X:0},{l.s.NPC.velocity.Y:0}")));
			}
			r.Lost = levies.Count(l => !l.s.NPC.active); r.CmdKills = _cmdKills; r.Telemetry = $"fly {SoldierNPC.AvFlyTicks} carry {SoldierNPC.AvCarryTicks} hold {SoldierNPC.AvHoldTicks} rejoins {SoldierNPC.AvRejoins} yields {SoldierNPC.AvYields} not-owned why [{string.Join("/", SoldierNPC.AvWhy)}]";
			r.Lines = WotgScenes.Log.Count - lines0; r.Floating = DialogueSystem.FloatingDelivered - fl0; r.Moments = WotgScenes.MomentsStarted - mom0; r.Dropped = WotgScenes.CuesDropped - drop0; r.Voice = WotgScenes.VoiceTicks - voice0; r.FightLines = WotgScenes.FightLines - fight0;
			return r;
		}
		private static Vector2 WeaveOffset(int t) => new Vector2((float)Math.Sin(t * 2f * Math.PI / 900f) * 16f * 16f, (float)Math.Sin(t * 2f * Math.PI / 600f + 1f) * 5f * 16f);

		private static string Line(string name, Result r)
		{
			Acc A = r.All; double n = Math.Max(1, A.Ticks);
			string cls(Acc a) => P(100.0 * a.Far / Math.Max(1, a.Ticks));
			return $"{name}: {r.Ticks} ticks, moved {r.Shifts}x; mean distance {F(A.Dist / n)} tiles, stranded(>40) {P(100.0 * A.Far / n)} (fliers {cls(r.Flier)}, foot {cls(r.Foot)}, hookers {cls(r.Hook)}), lost(>100) {P(100.0 * A.Lost / n)}, falling {P(100.0 * A.Fall / n)}, aloft {P(100.0 * A.Aloft / n)} (fliers {P(100.0 * r.Flier.Aloft / Math.Max(1, r.Flier.Ticks))}, foot {P(100.0 * r.Foot.Aloft / Math.Max(1, r.Foot.Ticks))}); " +
				$"near after move 2s {(r.Near2sN > 0 ? P(r.Near2s / r.Near2sN) : "-")}, 10s {(r.Near10sN > 0 ? P(r.Near10s / r.Near10sN) : "-")}; hits {r.Hits} (dmg {r.Damage}) [{string.Join(",", r.HitBy.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + ":" + kv.Value))}], by state [{string.Join(",", r.HitState.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + ":" + kv.Value))}], in annihilation sphere {r.InSphere} body-ticks; lines {r.Lines} scripted ({r.FightLines} worried, {r.Moments} moments started, {r.Dropped} cues dropped, the boss held the floor {r.Voice} ticks) + {r.Floating - r.Lines} other floating in {F(r.Ticks / 3600.0)} min; levies lost {r.Lost}; the commander would have died {r.CmdKills}x; attack put back {r.Reforced}x; body ticks [{r.Telemetry}]; attack rules active [{string.Join(",", r.ZoneTicks.Select(kv => kv.Key + ":" + kv.Value))}]; states [{string.Join(",", r.StateTicks.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + ":" + kv.Value))}]";
		}

		private static readonly Scn[] All =
		{
			new("Erasure (phase 2)", 0, new[] { "Erasure" }, 1100, false),
			new("RubbleGravitySlam (phase 2)", 0, new[] { "RubbleGravitySlam_ApplyExtremeGravity" }, 1100, false),
			new("WorldSmash (phase 2)", 0, new[] { "WorldSmash" }, 1500, false),
			new("DyingStarsWind + LilyStars (phase 2)", 0, new[] { "DyingStarsWind", "LilyStars_ChargePower" }, 1500, false),
			new("Commander 70 tiles above the ground, Erasure (phase 2)", 0, new[] { "Erasure" }, 1100, false, false, 70),
			new("Send to the dimension, WhirlingIceStorm (frost)", 1, new[] { "SendPlayerToMyUniverse", "WhirlingIceStorm" }, 1500, true, true),
			new("Send, AbsoluteZeroOutburst (frost)", 1, new[] { "SendPlayerToMyUniverse", "AbsoluteZeroOutburst" }, 1800, true),
			new("Send, BloodiedFountainBlasts (blood)", 1, new[] { "SendPlayerToMyUniverse", "BloodiedFountainBlasts" }, 1300, true, true),
			new("Send, Unclog (blood)", 1, new[] { "SendPlayerToMyUniverse", "Unclog" }, 1500, true),
			new("Send, ArmPortalStrikes (dark)", 1, new[] { "SendPlayerToMyUniverse", "ArmPortalStrikes" }, 1200, true, true),
			new("Send, RealityShatter (dark)", 1, new[] { "SendPlayerToMyUniverse", "RealityShatter_CreateAndWaitForTelegraph" }, 1800, true, true),
			new("Send, EnterBloodWhirlpool + AntishadowOnslaught (phase 4)", 2, new[] { "SendPlayerToMyUniverse", "EnterBloodWhirlpool", "AntishadowOnslaught" }, 1800, true),
			new("A dark portal moves the commander 150 tiles (Player.Teleport) in the dimension", 1, new[] { "ArmPortalStrikes" }, 700, false, false, 0, true),
			new("Send, UniversalAnnihilation (phase 4)", 2, new[] { "SendPlayerToMyUniverse", "UniversalAnnihilation" }, 1800, true),
			new("The whole cycle as the game picks it, phase 4 (what the army says)", 2, new[] { "ResetCycle" }, 10800, false),
		};

		private static void Run()
		{
			ModLoader.TryGetMod("NoxusBoss", out Mod nox); Seams(nox);
			string only = Environment.GetEnvironmentVariable("LEVY_AVBEHAVE_SCN");   // lab: a comma list of scenario number prefixes (0-based), e.g. "4,11"
			int scale = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AVBEHAVE_TICKS"), out int sc) ? sc : 0;   // lab: override every scenario's length
			var results = new List<(Scn s, Result off, Result on)>();
			for (int i = 0; i < All.Length; i++)
			{
				if (only != null && !only.Split(',').Contains(i.ToString())) continue;
				Scn s = scale > 0 ? All[i] with { Ticks = scale } : All[i];
				var off = RunScene(s, 0); Note($"#{i} before - " + Line(s.Name, off)); Cleanup();
				Result mid = null;
				if (s.Zone) { mid = RunScene(s, 1); Note($"#{i} flight only (the attack's own rule off) - " + Line(s.Name, mid)); Cleanup(); }
				var on = RunScene(s, 2); Note($"#{i} after  - " + Line(s.Name, on)); Cleanup();
				results.Add((s, off, on)); if (mid != null) _mids[s.Name] = mid;
			}
			Report(results);
		}

		private static readonly Dictionary<string, Result> _mids = new();

		private static void Report(List<(Scn s, Result off, Result on)> results)
		{
			foreach (var (s, off, on) in results)
			{
				if (on.Ticks < s.Ticks / 2 || off.Ticks < s.Ticks / 2) { Check($"{s.Name}: both runs measured (before {off.Ticks} ticks, after {on.Ticks})", false); continue; }
				double offFar = 100.0 * off.All.Far / Math.Max(1, off.All.Ticks), onFar = 100.0 * on.All.Far / Math.Max(1, on.All.Ticks);
				double onFall = 100.0 * on.All.Fall / Math.Max(1, on.All.Ticks);
				Check($"{s.Name}: the army stays with the commander (stranded {P(onFar)} against {P(offFar)} before)", onFar <= 15.0 && onFar <= offFar + 1.0);
				bool natural = s.States[0] == "ResetCycle";
				if (s.Portal)
				{
					double p2 = on.Near2sN > 0 ? on.Near2s / on.Near2sN : 0, p10 = on.Near10sN > 0 ? on.Near10s / on.Near10sN : 0;
					Check($"{s.Name}: the army comes through the portal (within 40 tiles: {P(p2)} after 2 s, {P(p10)} after 10 s)", on.Shifts > 0 && p2 >= 80.0 && p10 >= 90.0);
				}
				if (s.Up > 0)
				{
					double fo = 100.0 * on.Foot.Far / Math.Max(1, on.Foot.Ticks), ho = 100.0 * on.Hook.Far / Math.Max(1, on.Hook.Ticks), fo0 = 100.0 * off.Foot.Far / Math.Max(1, off.Foot.Ticks);
					Check($"{s.Name}: levies on foot are not left far below the commander (foot stranded {P(fo)}, hookers {P(ho)}, against {P(fo0)} of the foot before)", fo <= 15.0 && ho <= 15.0);
				}
				if (!s.Send && !natural && s.Up == 0 && !s.Portal)
				{
					double fa = 100.0 * on.Flier.Aloft / Math.Max(1, on.Flier.Ticks), fb = 100.0 * off.Flier.Aloft / Math.Max(1, off.Flier.Ticks);
					Check($"{s.Name}: levies with wings fly in the open-air arena ({P(fa)} airborne, against {P(fb)} before)", fa >= 85.0);
				}
				_mids.TryGetValue(s.Name, out Result mid);
				if (mid != null)
				{
					int H(Result r, string n) => r.HitBy.GetValueOrDefault(n);
					int HS(Result r, string n) => r.HitState.GetValueOrDefault(n);
					if (s.States.Contains("RealityShatter_CreateAndWaitForTelegraph"))
						Check($"{s.Name}: the army leaves the slam's lane (levies lost: {on.Lost}, against {off.Lost} before and {mid.Lost} without the rule)", on.Lost <= 3 && on.Lost < Math.Max(1, mid.Lost));
					else if (s.States.Contains("WhirlingIceStorm"))
						Check($"{s.Name}: the army keeps out of the frost columns (column hits {H(on, "FrostColumn")}, against {H(mid, "FrostColumn")} without the rule)", H(mid, "FrostColumn") < 12 || H(on, "FrostColumn") <= 0.7 * H(mid, "FrostColumn"));
					else if (s.States.Contains("BloodiedFountainBlasts"))
						Check($"{s.Name}: the army shelters under Solyn's shield (blood-rain hits in the burst {HS(on, "BloodiedFountainBlasts_DenseBurst")}, against {HS(mid, "BloodiedFountainBlasts_DenseBurst")} without the rule)", on.ZoneTicks.GetValueOrDefault("solyn shield") > 0 && (HS(mid, "BloodiedFountainBlasts_DenseBurst") < 12 || HS(on, "BloodiedFountainBlasts_DenseBurst") <= 0.7 * HS(mid, "BloodiedFountainBlasts_DenseBurst")));
					else if (s.States.Contains("ArmPortalStrikes"))
						Check($"{s.Name}: the army spreads out of the arm strikes (hits {on.Hits}, against {mid.Hits} in the tight ring)", mid.Hits < 100 || on.Hits <= 0.6 * mid.Hits);
				}
				if (natural)
				{
					double lpmOn = on.Lines / Math.Max(0.1, on.Ticks / 3600.0), lpmOff = off.Lines / Math.Max(0.1, off.Ticks / 3600.0);
					Check($"{s.Name}: the Avatar fight is quiet ({F(lpmOn)} scripted lines a minute, against {F(lpmOff)} before)", lpmOn <= 3.5 && (lpmOn < lpmOff || lpmOff < 3.0));
				}
				if (s.Send)
				{
					double n2 = on.Near2sN > 0 ? on.Near2s / on.Near2sN : 0, n10 = on.Near10sN > 0 ? on.Near10s / on.Near10sN : 0;
					Check($"{s.Name}: the army is through when the commander is moved (within 40 tiles: {P(n2)} after 2 s, {P(n10)} after 10 s)", on.Shifts > 0 && n2 >= (s.States.Contains("RealityShatter_CreateAndWaitForTelegraph") ? 40.0 : 80.0) && n10 >= 90.0);   // (the slam's lane is cleared first: by design the army is not all beside him two seconds on)
					Check($"{s.Name}: nobody falls through the dimension (falling {P(onFall)})", onFall <= 3.0);
					double fAloft = 100.0 * on.Flier.Aloft / Math.Max(1, on.Flier.Ticks);
					Check($"{s.Name}: levies with wings are airborne in the fight ({P(fAloft)})", fAloft >= 85.0);
				}
			}
		}
	}
}
