using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
	/// WORLD-LEVEL self-check of the boss-battery fixes, logged as "[BossBugs]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the summon world check chains it; LEVY_BOSSBUGS_ONLY=1 runs it
	/// alone; LEVY_BOSSBUGS_PART=calamity|shots|ml|wof picks one part). A flat arena, slot 0 standing in as the commander, the real Main.Update stepping the world, throwaway soldiers and bosses only; everything is removed
	/// again and nothing is saved.
	///  * CALAMITY (Calamity half only): Revengeance replaces Deerclops' and the Queen Bee's bees' AI with ones that index Main.player[NPC.target]; a 300+ NPC target (the native route) failed on an out-of-range index there every
	///    tick and Deerclops despawned. Audits every NPCID.Sets.UsesNewTargetting type, then fights a real Deerclops and real bees against a downed-boss army and counts exceptions and the target index their AI saw.
	///  * SHOTS: a Spectator commander owns every shot an enemy fires in single player (Main.myPlayer), and the Spectator's "deals no damage" veto stopped those shots hurting levies. Real hostile projectiles of Moon Lord,
	///    the Wall of Flesh, Plantera and the Twins' kind are put on a levy under a Spectator commander and must hurt it.
	///  * ML: a real Moon Lord against levies: he locks onto them and each of his real projectile kinds (Phantasmal Eye, Bolt, Sphere) hurts a levy it reaches.
	///  * WOF: a real Wall of Flesh in the Underworld, once with a visible commander and once with a Spectator: its parts lock onto levies and the wall hurts them.
	/// </summary>
	public partial class BossBugsWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static Vector2 _cmdAt;
		private static bool _local; private static int _savedMy;
		private const int FloorY = 300, Cx = 1800, Half = 200;
		private const string Commander = "BossBugsTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_BOSSBUGS") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the world checks chain it, one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation; int gm = Main.GameMode; bool hm = Main.hardMode; bool day = Main.dayTime; double time = Main.time;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); Main.GameMode = gm; Main.hardMode = hm; Main.dayTime = day; Main.time = time; }
			_mod.Logger.Info($"[BossBugs] self-check pass={_pass} fail={_fail}");
		}

		/// <summary>True while the check steps the world (the stand-in commander counts as online, natural spawns are cleared).</summary>
		public static bool Running => _running;

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[BossBugs] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[BossBugs] OBSERVED " + s);

		// =====================================================================================================
		// scaffolding
		// =====================================================================================================
		private static Action _perTick;
		private static bool _snow, _spectator, _underworld;
		private static int _floorY = FloorY;
		private static readonly HashSet<int> _watched = new();

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++) { KeepCommander(); _perTick?.Invoke(); WorldStep.Update(); }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = false; Main.time = 18000;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.ZoneSnow = _snow;
			if (_underworld) { p.ZoneUnderworldHeight = true; p.ZoneOverworldHeight = false; }
			if (_spectator)
			{
				int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>();
				if (!p.HasBuff(bt)) { p.AddBuff(bt, 600000); if (p.TryGetModPlayer(out global::Levy.Common.Potions.SpectatorPlayer sp)) sp.Anchor = p.Center; }
			}
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static FieldInfo _revField, _deathField;
		/// <summary>Calamity's Revengeance / Death flags (the harness's CrSetRevenge); false when Calamity is not loaded.</summary>
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

		/// <summary>A flat arena: air over a floor at <paramref name="fy"/>, half-width tiles each way from the centre.</summary>
		private static void Arena(int fy, int half)
		{
			for (int i = Cx - half; i <= Cx + half; i++)
			{
				for (int j = fy - 120; j < fy; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, fy]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = fy + 1; j < fy + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
			_floorY = fy;
		}

		private static void PlaceCommander(float tilesFromCentre) { _cmdAt = new Vector2((Cx + tilesFromCentre) * 16f, (_floorY - 3) * 16f); Main.player[0].position = _cmdAt; }

		private static SoldierNPC Levy(Vector2 at, SoldierOrder order = SoldierOrder.Hold, int hp = 4000)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. BossBugs",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = order;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			if (hp > 0) { Main.npc[body].lifeMax = Main.npc[body].life = hp; rec.CurrentHp = hp; }
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		private static List<SoldierNPC> Levies(int n, float spacingTiles, float centreTiles = 0f)
		{
			var l = new List<SoldierNPC>();
			for (int k = 0; k < n; k++) { var s = Levy(new Vector2((Cx + centreTiles + (k - n / 2) * spacingTiles) * 16f, _floorY * 16f)); if (s != null) l.Add(s); }
			return l;
		}

		private static NPC SpawnNpc(int type, Vector2 at)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("BossBugsCheck"), (int)at.X, (int)at.Y, type);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.timeLeft = 99999; n.netOffset = Vector2.Zero;
			return n;
		}

		private static bool Locked(NPC n) => n != null && n.active && LevyNpcData.Of(n).SoldierTarget >= 0;

		private static void ClearHostiles()
		{
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.active = false; }
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		private static bool? _savedBossTarget;
		/// <summary>The config's BossesTargetSoldiers for a check (null restores it).</summary>
		private static void SetBossesTarget(bool? on)
		{
			var cfg = global::Levy.Common.Config.LevyConfig.Get; if (cfg == null) return;
			if (on.HasValue) { _savedBossTarget ??= cfg.BossesTargetSoldiers; cfg.BossesTargetSoldiers = on.Value; }
			else if (_savedBossTarget.HasValue) { cfg.BossesTargetSoldiers = _savedBossTarget.Value; _savedBossTarget = null; }
		}

		/// <summary>Everything this check made, gone.</summary>
		private static void Cleanup()
		{
			try
			{
				ClearHostiles();
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
				}
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				_perTick = null; _snow = false; _spectator = false; _underworld = false; _watched.Clear();
				SetRevenge(false, false); SetBossesTarget(null);
				EnemyTargeting.AiProbe = null;
				SoldierNPC.HitDbg = null;
				Main.wofNPCIndex = -1;
				Main.hardMode = false;
				try { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); } catch (Exception) { }
			}
			catch (Exception) { }
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			string only = Environment.GetEnvironmentVariable("LEVY_BOSSBUGS_PART") ?? "all";
			if (only == "all" || only == "calamity") { try { Calamity(); } finally { Cleanup(); } }
			if (only == "all" || only == "shots") { try { Shots(); } finally { Cleanup(); } }
			if (only == "all" || only == "ml") { try { MoonLord(); } finally { Cleanup(); } }
			if (only == "all" || only == "wof") { try { WallOfFlesh(); } finally { Cleanup(); } }
			if (only == "all" || only == "mlkill") { try { MoonLordDeath(); } finally { Cleanup(); } }
		}

		// =====================================================================================================
		// CALAMITY: native-targeting types whose AI Calamity replaced
		// =====================================================================================================
		private static void Calamity()
		{
			if (!ModLoader.HasMod("CalamityMod")) { Note("Calamity is not loaded: the Calamity part is skipped"); return; }
			SetRevenge(true, true);
			Arena(FloorY, Half);

			// ---- the audit: every type vanilla marks UsesNewTargetting, with Calamity's Revengeance AI override bound or not
			var rows = new List<string>(); int bound = 0, wrong = 0;
			for (int type = 1; type < NPCLoader.NPCCount; type++)
			{
				if (type >= NPCID.Sets.UsesNewTargetting.Length || !NPCID.Sets.UsesNewTargetting[type]) continue;
				NPC n = SpawnNpc(type, new Vector2((Cx + 150) * 16f, (FloorY - 30) * 16f));
				if (n == null || n.type != type) { rows.Add(Lang.GetNPCNameValue(type) + "(" + type + "): did not spawn"); if (n != null) n.active = false; continue; }
				bool b = CalamityTargetingCompat.OverrideBound(n);
				bool native = EnemyTargeting.UsesNativeTargeting(n), flyer = EnemyTargeting.UsesFlyerSlot(n);
				if (b) bound++;
				if (b ? (native || !flyer) : (!native || flyer)) wrong++;
				rows.Add($"{Lang.GetNPCNameValue(type)}({type}): calamityOverride={b} -> {(native ? "native NPC target (300+)" : flyer ? "flyer slot 254" : "other")}");
				n.active = false;
			}
			foreach (string r in rows) Note("audit " + r);
			Check($"every native-targeting type Calamity's AI drives is kept off the 300+ route and on the flyer slot ({bound} of {rows.Count} bound, {wrong} wrong)", wrong == 0 && bound >= 3);
			Check("Deerclops and the Queen Bee's bees are among the bound types", rows.Exists(r => r.StartsWith("Deerclops") && r.Contains("calamityOverride=True")) && rows.Exists(r => r.StartsWith("Bee(") && r.Contains("calamityOverride=True")));

			// ---- the fight: a downed Deerclops and bees against an army, counting what their AI throws
			PlaceCommander(-150); _snow = true;
			Main.GameMode = GameModeID.Master;
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.Deerclops, true);
			SetBossesTarget(true);
			var levies = Levies(6, 3f);
			Check("six levies spawn", levies.Count == 6);
			Step(2);
			var thrown = new Dictionary<int, int>(); var seenTargets = new Dictionary<int, int>(); var aiCalls = new Dictionary<int, int>(); var lockedAi = new Dictionary<int, int>(); string firstEx = null;
			On_NPC.hook_UpdateNPC det = (On_NPC.orig_UpdateNPC orig, NPC self, int i) =>
			{
				try { orig(self, i); }
				catch (Exception e) { if (_watched.Contains(self.type)) { thrown[self.type] = thrown.GetValueOrDefault(self.type) + 1; firstEx ??= self.TypeName + ": " + e.GetType().Name + " " + (e.StackTrace ?? "").Split('\n')[0].Trim(); } throw; }
			};
			On_NPC.UpdateNPC += det;
			try
			{
				_watched.Add(NPCID.Deerclops); _watched.Add(NPCID.Bee); _watched.Add(NPCID.BeeSmall);
				EnemyTargeting.AiProbe = n =>
				{
					if (!_watched.Contains(n.type)) return;
					aiCalls[n.type] = aiCalls.GetValueOrDefault(n.type) + 1;
					if (Locked(n)) { lockedAi[n.type] = lockedAi.GetValueOrDefault(n.type) + 1; if (n.target < 0 || n.target >= Main.maxPlayers) seenTargets[n.type] = seenTargets.GetValueOrDefault(n.type) + 1; }
				};
				NPC deer = SpawnNpc(NPCID.Deerclops, new Vector2((Cx + 28) * 16f, (_floorY - 4) * 16f));
				Check("Deerclops spawns", deer != null);
				if (deer == null) return;
				Check("Calamity's Revengeance AI is bound to this Deerclops", CalamityTargetingCompat.OverrideBound(deer));
				int lockedTicks = 0, alive = 0;
				_perTick = () => { if (deer.active && deer.type == NPCID.Deerclops) { alive++; if (Locked(deer)) lockedTicks++; deer.timeLeft = 99999; } };
				Step(600);
				_perTick = null;
				Note($"Deerclops: alive {alive} of 600 ticks, locked on a levy {lockedTicks} ticks, AI calls {aiCalls.GetValueOrDefault(NPCID.Deerclops)}, calls locked {lockedAi.GetValueOrDefault(NPCID.Deerclops)}, out-of-range targets seen {seenTargets.GetValueOrDefault(NPCID.Deerclops)}, AI errors thrown {thrown.GetValueOrDefault(NPCID.Deerclops)}{(firstEx != null ? " first: " + firstEx.Replace("Exception", "Error") : "")}");
				Check("Deerclops' AI throws nothing against a levy-locked target", thrown.GetValueOrDefault(NPCID.Deerclops) == 0);
				Check("Deerclops stays in the fight (it did not despawn)", deer.active && deer.type == NPCID.Deerclops && alive >= 590);
				Check("Deerclops locks onto a levy", lockedTicks >= 300);
				Check("its AI reads a real player index (0 to 254) every call while locked on a levy", lockedAi.GetValueOrDefault(NPCID.Deerclops) > 100 && seenTargets.GetValueOrDefault(NPCID.Deerclops) == 0);
				deer.active = false;

				// the bees: Revengeance's flying AI steers by Main.player[NPC.target]
				for (int k = 0; k < 6; k++) SpawnNpc(k % 2 == 0 ? NPCID.Bee : NPCID.BeeSmall, new Vector2((Cx + 10 + k * 3) * 16f, (_floorY - 20) * 16f));
				int beeBound = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && (Main.npc[i].type == NPCID.Bee || Main.npc[i].type == NPCID.BeeSmall) && CalamityTargetingCompat.OverrideBound(Main.npc[i])) beeBound++;
				int beeLock = 0; _perTick = () => { for (int i = 0; i < Main.maxNPCs; i++) { NPC b = Main.npc[i]; if (b.active && (b.type == NPCID.Bee || b.type == NPCID.BeeSmall)) { b.timeLeft = 99999; if (Locked(b)) beeLock++; } } };
				Step(300);
				_perTick = null;
				int exB = thrown.GetValueOrDefault(NPCID.Bee) + thrown.GetValueOrDefault(NPCID.BeeSmall);
				Note($"bees: bound {beeBound} of 6, locked-on-a-levy bee ticks {beeLock}, AI calls {aiCalls.GetValueOrDefault(NPCID.Bee) + aiCalls.GetValueOrDefault(NPCID.BeeSmall)}, out-of-range targets seen {seenTargets.GetValueOrDefault(NPCID.Bee) + seenTargets.GetValueOrDefault(NPCID.BeeSmall)}, AI errors thrown {exB}");
				Check("the bees' Revengeance AI throws nothing against a levy-locked target", beeBound == 6 && exB == 0);
				Check("the bees go after levies and read a real player index doing it", beeLock > 0 && seenTargets.GetValueOrDefault(NPCID.Bee) + seenTargets.GetValueOrDefault(NPCID.BeeSmall) == 0);
			}
			finally { On_NPC.UpdateNPC -= det; }
		}

		// =====================================================================================================
		// SHOTS: a Spectator commander and the enemy's projectiles
		// =====================================================================================================
		/// <summary>Whether slot 0 is a Spectator right now (the dedicated server drops the slot's active flag between ticks, so the check holds it up for the question, as it does during every NPC and projectile update).</summary>
		private static string Spec() { Main.player[0].active = true; return global::Levy.Common.Potions.SpectatorPlayer.IsSpectator(Main.player[0]) ? "spectator" : "plain"; }

		/// <summary>One run of vanilla's own hit loop for this projectile (Projectile.Damage), with slot 0 held up as it is during every projectile update (the dedicated server's loop drops the flag from the side, which would make a world step flaky).</summary>
		private static void DamageNow(Projectile pr)
		{
			Main.player[0].active = true; int keep = Main.myPlayer; Main.myPlayer = 0;
			try { pr.Damage(); } finally { Main.myPlayer = keep; }
		}

		private static void Shots()
		{
			Arena(FloorY, Half);
			PlaceCommander(-100); _spectator = true;
			var types = new (string name, int id)[]
			{
				("Phantasmal Eye", ProjectileID.PhantasmalEye), ("Eye Laser (Wall of Flesh)", ProjectileID.EyeLaser), ("Death Laser (the Twins, Destroyer)", ProjectileID.DeathLaser),
				("Poison Seed (Plantera)", ProjectileID.PoisonSeedPlantera), ("Thorn Ball (Plantera)", ProjectileID.ThornBall), ("Eye Fire (Spazmatism)", ProjectileID.EyeFire),
			};
			Step(4);
			Check("the commander is a Spectator for this part", Spec() == "spectator");
			foreach (var (name, id) in types)
			{
				var s = Levy(new Vector2(Cx * 16f, _floorY * 16f), SoldierOrder.Hold, 4000);
				if (s == null) { Check(name + ": the levy spawns", false); continue; }
				Step(2);
				int life0 = s.NPC.life; Vector2 at = s.NPC.Center;
				int myKeep = Main.myPlayer; Main.myPlayer = 0; // an enemy's shot carries the local player's index, as it does in the lab and in a single-player world
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("BossBugsCheck"), at, Vector2.Zero, id, 60, 0f, 0);
				Main.myPlayer = myKeep;
				if (pi < 0 || pi >= Main.maxProjectiles) { Check(name + ": the projectile spawns", false); s.NPC.active = false; continue; }
				Projectile pr = Main.projectile[pi];
				int hitTick = -1; var asked = new SortedSet<string>();
				for (int k = 0; k < 4 && hitTick < 0; k++)
				{
					pr.Center = at; pr.velocity = Vector2.Zero; { string sp = Spec(); asked.Add((CombinedHooks.CanHitNPCWithProj(pr, s.NPC) is bool b ? b.ToString() : "null") + "/" + sp); }
					DamageNow(pr);
					if (s.NPC.life < life0) hitTick = k;
				}
				Check($"{name}: a hostile shot hurts a levy under a Spectator commander (life {life0} -> {s.NPC.life}; the hit rules answered {string.Join(",", asked)})", hitTick >= 0);
				pr.active = false; s.NPC.active = false; RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				Step(2);
			}
		}

		// =====================================================================================================
		// MOON LORD
		// =====================================================================================================
		private static void MoonLord()
		{
			Arena(FloorY, Half);
			_spectator = true;
			PlaceCommander(0);
			Main.hardMode = true;
			bool rev = SetRevenge(true, true); if (rev) Main.GameMode = GameModeID.Master;
			Note("Moon Lord scenario: Calamity Revengeance+Death " + (rev ? "on" : "off") + ", commander is a Spectator standing among the army");
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.MoonLordCore, true);
			SetBossesTarget(true);
			var levies = Levies(30, 3f);
			Check("30 levies spawn", levies.Count == 30);
			Step(4);
			NPC core = SpawnNpc(NPCID.MoonLordCore, new Vector2(Cx * 16f, (FloorY - 40) * 16f));
			if (core == null) { Check("Moon Lord spawns", false); return; }
			SoldierNPC.HitDbg = new();
			int ticks = 0, lockedTicks = 0;
			var tested = new HashSet<int>(); var kinds = new int[] { ProjectileID.PhantasmalBolt, ProjectileID.PhantasmalEye, ProjectileID.PhantasmalSphere };
			var seen = new Dictionary<int, int>();
			for (int t = 0; t < 3000 && tested.Count < kinds.Length; t++)
			{
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.timeLeft = 99999; }
				Step(1); ticks++;
				if (Locked(core)) lockedTicks++;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile pr = Main.projectile[i];
					if (!pr.active || !pr.hostile) continue;
					seen[pr.type] = seen.GetValueOrDefault(pr.type) + 1;
					if (Array.IndexOf(kinds, pr.type) < 0 || tested.Contains(pr.type)) continue;
					var lv = levies.FirstOrDefault(l => l.NPC.active && l.ImmuneTime == 0 && l.NPC.life == l.NPC.lifeMax);
					if (lv == null) break;
					// one of Moon Lord's own shots, as he fired it, brought onto a levy: it must hurt
					int life0 = lv.NPC.life; pr.Center = lv.NPC.Center; string sp0 = Spec(); string asked = (CombinedHooks.CanHitNPCWithProj(pr, lv.NPC) is bool b ? b.ToString() : "null") + "/" + sp0;
					DamageNow(pr);
					tested.Add(pr.type);
					Check($"Moon Lord's {Lang.GetProjectileName(pr.type).Value} hurts a levy it reaches (life {life0} -> {lv.NPC.life}; the hit rules answered {asked})", lv.NPC.life < life0);
					break;
				}
			}
			Note("hostile projectile kinds Moon Lord fired (ticks seen): " + string.Join(", ", seen.OrderByDescending(k => k.Value).Select(k => Lang.GetProjectileName(k.Key).Value + "=" + k.Value)));
			Note("damage the levies took by source: " + (SoldierNPC.HitDbg.Count == 0 ? "none" : string.Join(", ", SoldierNPC.HitDbg.OrderByDescending(k => k.Value.dmg).Select(k => k.Key + "=" + k.Value.dmg + " (" + k.Value.n + " hits)"))));
			Check($"Moon Lord locks onto levies ({lockedTicks} of {ticks} ticks, the first second to find them not counted)", ticks > 100 && lockedTicks * 10 >= (ticks - 60) * 8);
			Check("every kind of shot he fired in the check was tried on a levy (" + tested.Count + " of " + kinds.Length + ")", tested.Count == kinds.Length);
		}

		// =====================================================================================================
		// MOON LORD'S DEATH: Calamity's override AI ends the fight inside its own PreAI
		// =====================================================================================================
		/// <summary>
		/// Calamity's Moon Lord override finishes the core's death animation INSIDE its AI: it deactivates the parts and the core and calls NetMessage.SendData(SyncNPC) for each. On a server that serializes the NPC,
		/// and Calamity's SendExtraAI clears <c>AIOverride</c> of a dead NPC, so when the AI returns, CalamityVanillaAIOverrideNPC.PreAI reads the field again (<c>AIOverride.DisableMultiplayerSmoothing</c>) and throws a
		/// NullReferenceException, once per Moon Lord kill (seen in every Moon Lord fight of the hardmode battery). Levy's PreAI detour wraps that call: it must let the death finish without an exception.
		/// </summary>
		private static void MoonLordDeath()
		{
			if (!ModLoader.HasMod("CalamityMod")) { Note("Calamity is not loaded: the Moon Lord death part is skipped"); return; }
			Arena(FloorY, Half);
			_spectator = true;
			PlaceCommander(0);
			Main.hardMode = true;
			SetRevenge(true, true); Main.GameMode = GameModeID.Master;
			Note($"Moon Lord death scenario: netMode {Main.netMode} (2 = dedicated server, the mode Calamity's death animation throws in), Calamity Revengeance+Death on");
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.MoonLordCore, true);
			SetBossesTarget(true);
			var levies = Levies(6, 3f);
			Check("six levies spawn", levies.Count == 6);
			Step(4);
			NPC core = SpawnNpc(NPCID.MoonLordCore, new Vector2(Cx * 16f, (FloorY - 40) * 16f));
			if (core == null) { Check("Moon Lord spawns", false); return; }
			Check("Calamity's Revengeance AI is bound to Moon Lord's core", CalamityTargetingCompat.OverrideBound(core));
			var thrown = new Dictionary<string, int>(); string firstEx = null;
			On_NPC.hook_UpdateNPC det = (On_NPC.orig_UpdateNPC orig, NPC self, int i) =>
			{
				try { orig(self, i); }
				catch (Exception e)
				{
					if (self.type == NPCID.MoonLordCore || self.type == NPCID.MoonLordHead || self.type == NPCID.MoonLordHand) { string k = self.TypeName + ": " + e.GetType().Name; thrown[k] = thrown.GetValueOrDefault(k) + 1; firstEx ??= k + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(); }
					throw;
				}
			};
			On_NPC.UpdateNPC += det;
			try
			{
				// the parts: the core spawns its two hands and head one second in
				int[] part = null;
				for (int t = 0; t < 600 && part == null; t++)
				{
					for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is not SoldierNPC && !n.friendly && !n.townNPC) n.timeLeft = 99999; }
					Step(1);
					if (core.active && core.localAI[0] > 0 && core.localAI[1] > 0 && core.localAI[2] > 0) part = new[] { (int)core.localAI[0], (int)core.localAI[1], (int)core.localAI[2] };
				}
				Check("Moon Lord's two hands and head spawn", part != null && Main.npc[part[0]].type == NPCID.MoonLordHand && Main.npc[part[1]].type == NPCID.MoonLordHand && Main.npc[part[2]].type == NPCID.MoonLordHead);
				if (part == null) return;
				Check("nothing threw while the fight ran up to the kill", thrown.Count == 0);
				// the end of the death animation: ai[0] == 2 is "death effects", ai[1] counts its ticks and the kill comes at 600
				core.ai[0] = 2f; core.ai[1] = 595f; core.netUpdate = true;
				Step(12);
				Note("exceptions the three kinds of Moon Lord NPC threw during the death: " + (thrown.Count == 0 ? "none" : string.Join(", ", thrown.Select(k => k.Key + " x" + k.Value))) + (firstEx != null ? "; first: " + firstEx : ""));
				Check("Moon Lord's death animation ends without an exception (Calamity's override AI clears itself on the server inside its own PreAI)", thrown.Count == 0);
				Check("the core and its parts are gone after the kill", !core.active && !Main.npc[part[0]].active && !Main.npc[part[1]].active && !Main.npc[part[2]].active);
			}
			finally { On_NPC.UpdateNPC -= det; }
		}

		// =====================================================================================================
		// WALL OF FLESH
		// =====================================================================================================
		private static void WallOfFlesh()
		{
			int fy = Math.Max(Main.UnderworldLayer + 60, Main.maxTilesY - 125); // the floor stands near the height the wall's mouth rides at, so its Hungry and eyes reach the army
			Arena(fy, 300);
			_underworld = true;
			SetRevenge(true, true); Main.GameMode = ModLoader.HasMod("CalamityMod") ? GameModeID.Master : GameModeID.Expert;
			global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.WallofFlesh, true);
			SetBossesTarget(true);
			foreach (bool spectator in new[] { false, true })
			{
				ClearHostiles(); RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
				Main.wofNPCIndex = -1; _spectator = spectator;
				if (!spectator) { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); }
				string who = spectator ? "Spectator commander" : "visible commander";
				PlaceCommander(-6);
				var levies = Levies(10, 3f, -6f);
				Step(4);
				NPC.SpawnWOF(Main.player[0].position);
				int wi = Main.wofNPCIndex;
				if (wi < 0) { for (int q = 0; q < Main.maxNPCs; q++) if (Main.npc[q].active && Main.npc[q].type == NPCID.WallofFlesh) { wi = q; break; } Note($"SpawnWOF: wofNPCIndex={Main.wofNPCIndex} found wall at slot {wi}; player tile y={Main.player[0].position.Y / 16f:0} (needs >= {Main.maxTilesY - 205}), maxTilesY={Main.maxTilesY}, netMode={Main.netMode}"); }
				Check($"({who}) the Wall of Flesh spawns", wi >= 0 && Main.npc[wi].active);
				if (wi < 0 || !Main.npc[wi].active) continue;
				NPC wall = Main.npc[wi]; wall.position = new Vector2((Cx + 60) * 16f, (fy - 12) * 16f) - wall.Size / 2f; wall.velocity = Vector2.Zero;
				SoldierNPC.HitDbg = new();
				var act = new Dictionary<int, int>(); var lck = new Dictionary<int, int>();
				int[] kinds = { NPCID.WallofFlesh, NPCID.WallofFleshEye, NPCID.TheHungry, NPCID.TheHungryII, NPCID.LeechHead };
				int lev0 = levies.Sum(l => l.NPC.life); int wofTick = 0;
				var shots = new Dictionary<int, int>(); var shotMin = new Dictionary<int, float>(); var seenLaser = new HashSet<int>(); var aimErrs = new List<float>();
				_perTick = () =>
				{
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile pr = Main.projectile[i]; if (!pr.active || !pr.hostile) continue;
						shots[pr.type] = shots.GetValueOrDefault(pr.type) + 1;
						if (pr.type == ProjectileID.EyeLaser && seenLaser.Add(pr.identity) && seenLaser.Count <= 8)
						{
							int own = (int)pr.GetGlobalProjectile<SoldierProjectileTag>().SourceNpcIndex; NPC src = own >= 0 && own < Main.maxNPCs ? Main.npc[own] : null;
							SoldierNPC lk = src != null ? EnemyTargeting.SoldierTargetOf(src) as SoldierNPC : null;
							string aim = "no locked levy";
							if (lk != null && pr.velocity != Vector2.Zero)
							{
								float err = Math.Abs(MathHelper.WrapAngle(pr.velocity.ToRotation() - (lk.NPC.Center - pr.Center).ToRotation())) * 57.29578f;
								aimErrs.Add(err); aim = err.ToString("0") + " deg off its locked levy";
							}
							Vector2 toCmd = Main.player[0].Center - pr.Center;
							string aimCmd = pr.velocity != Vector2.Zero ? ((float)Math.Abs(MathHelper.WrapAngle(pr.velocity.ToRotation() - toCmd.ToRotation())) * 57.29578f).ToString("0") + " deg off the commander" : "";
							Note($"({who}) laser from {(src != null ? src.TypeName : "?")}#{own} at ({(pr.Center.X / 16f - Cx):0},{(pr.Center.Y / 16f - fy):0}) v=({pr.velocity.X:0.0},{pr.velocity.Y:0.0}): {aim}; {aimCmd}; ai0={pr.ai[0]} ai1={pr.ai[1]} damage={pr.damage}");
						}
						float dd = float.MaxValue; foreach (var l in levies) if (l.NPC.active) dd = Math.Min(dd, Vector2.Distance(pr.Center, l.NPC.Center));
						shotMin[pr.type] = Math.Min(shotMin.GetValueOrDefault(pr.type, float.MaxValue), dd);
					}
					for (int i = 0; i < Main.maxNPCs; i++)
					{
						NPC n = Main.npc[i]; if (!n.active || n.friendly || n.townNPC || n.ModNPC is SoldierNPC) continue;
						n.timeLeft = 99999;
						if (Array.IndexOf(kinds, n.type) < 0) continue;
						// only while a levy is within reach (the wall walks past the army and on; once it is far away nothing is left for it to lock onto)
						float near = float.MaxValue; foreach (var l in levies) if (l.NPC.active) near = Math.Min(near, Math.Abs(n.Center.X - l.NPC.Center.X));
						if (near > 35f * 16f) continue;
						act[n.type] = act.GetValueOrDefault(n.type) + 1; if (Locked(n)) lck[n.type] = lck.GetValueOrDefault(n.type) + 1;
					}
					wall.life = wall.lifeMax; // the army here has no weapons: the wall stays up for the whole window
					if (++wofTick % 300 == 0)
					{
						float hd = float.MaxValue; int nh = 0; foreach (var q in Main.npc) if (q.active && (q.type == NPCID.TheHungry || q.type == NPCID.TheHungryII)) { nh++; foreach (var l in levies) if (l.NPC.active) hd = Math.Min(hd, Vector2.Distance(q.Center, l.NPC.Center)); }
						Note($"({who}) tick {wofTick}: wall at x={(wall.Center.X / 16f - Cx):0} y={(wall.Center.Y / 16f - fy):0} v=({wall.velocity.X:0.0},{wall.velocity.Y:0.0}) target={wall.target} ai0={wall.ai[0]:0} ai1={wall.ai[1]:0} ai2={wall.ai[2]:0}; {nh} Hungry, nearest {(hd / 16f):0} tiles from a levy; levies at x={(levies[0].NPC.Center.X / 16f - Cx):0}..{(levies[levies.Count - 1].NPC.Center.X / 16f - Cx):0}");
					}
				};
				Step(1500);
				_perTick = null;
				int lev1 = levies.Sum(l => l.NPC.active ? l.NPC.life : 0);
				string Row(int t) => $"{Lang.GetNPCNameValue(t)} {lck.GetValueOrDefault(t)}/{act.GetValueOrDefault(t)} ticks locked on a levy while one is within 35 tiles sideways";
				Note($"({who}) {string.Join("; ", kinds.Select(Row))}");
				Note($"({who}) hostile projectiles seen (ticks, closest approach to a levy in tiles): " + (shots.Count == 0 ? "none" : string.Join(", ", shots.Select(k => Lang.GetProjectileName(k.Key).Value + "(" + k.Key + ")=" + k.Value + " / " + (shotMin[k.Key] / 16f).ToString("0.0")))));
				Note($"({who}) damage the levies took by source: " + (SoldierNPC.HitDbg.Count == 0 ? "none" : string.Join(", ", SoldierNPC.HitDbg.OrderByDescending(k => k.Value.dmg).Select(k => k.Key + "=" + k.Value.dmg + " (" + k.Value.n + " hits)"))) + $"; levy life {lev0} -> {lev1}");
				Check($"({who}) the lasers the eyes fire are aimed at the levy each eye is locked on ({aimErrs.Count} sampled, worst {(aimErrs.Count > 0 ? aimErrs.Max() : 0):0} deg off)", aimErrs.Count == 0 || aimErrs.Max() <= 12f);
				Check($"({who}) the Wall of Flesh's eyes lock onto levies", act.GetValueOrDefault(NPCID.WallofFleshEye) > 100 && lck.GetValueOrDefault(NPCID.WallofFleshEye) * 10 >= act.GetValueOrDefault(NPCID.WallofFleshEye) * 6);
				int hungry = act.GetValueOrDefault(NPCID.TheHungry) + act.GetValueOrDefault(NPCID.TheHungryII), hungryL = lck.GetValueOrDefault(NPCID.TheHungry) + lck.GetValueOrDefault(NPCID.TheHungryII);
				Check($"({who}) the Hungry (when any are out) lock onto levies", hungry == 0 || hungryL * 10 >= hungry * 6);
				if (spectator) Check($"({who}) the mouth walks at the levies when there is no visible player", act.GetValueOrDefault(NPCID.WallofFlesh) > 100 && lck.GetValueOrDefault(NPCID.WallofFlesh) * 10 >= act.GetValueOrDefault(NPCID.WallofFlesh) * 6);
				else Check($"({who}) the mouth keeps walking at the visible player", act.GetValueOrDefault(NPCID.WallofFlesh) > 100 && lck.GetValueOrDefault(NPCID.WallofFlesh) * 10 < act.GetValueOrDefault(NPCID.WallofFlesh) * 4);
				Check($"({who}) the wall hurts the levies (life {lev0} -> {lev1})", lev1 < lev0);
				SoldierNPC.HitDbg = null;
			}
		}
	}
}
