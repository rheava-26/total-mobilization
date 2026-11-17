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
	/// alone; LEVY_BOSSBUGS_PART=calamity|shots|ml|wof|mlkill|killedge|spectarget|lostbet picks one part). A flat arena, slot 0 standing in as the commander, the real Main.Update stepping the world, throwaway soldiers and bosses only; everything is removed
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

		private static void Check(string name, bool ok, Func<string> why = null) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[BossBugs] {(ok ? "PASS" : "FAIL")} {name}{(!ok && why != null ? " :: " + why() : "")}"); }
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
			if (order == SoldierOrder.Regroup) { var gun = new Item(); gun.SetDefaults(ItemID.Minishark); var ball = new Item(); ball.SetDefaults(ItemID.MusketBall); ball.stack = 999; rec.Gear.Weapon = gun; rec.Gear.Ammo = ball; } // a Regroup levy is armed: an unarmed body never reads its order
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			if (order != SoldierOrder.Regroup) Main.npc[body].Bottom = at; // a Regroup levy keeps the spot the spawn gave it (beside the commander)
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

		/// <summary>One part, or all (LEVY_BOSSBUGS_PART takes "all" or a comma list).</summary>
		private static bool Part(string only, string name) => only == "all" || Array.IndexOf(only.Split(','), name) >= 0;

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			string only = Environment.GetEnvironmentVariable("LEVY_BOSSBUGS_PART") ?? "all";
			if (Part(only, "calamity")) { try { Calamity(); } finally { Cleanup(); } }
			if (Part(only, "shots")) { try { Shots(); } finally { Cleanup(); } }
			if (Part(only, "ml")) { try { MoonLord(); } finally { Cleanup(); } }
			if (Part(only, "wof")) { try { WallOfFlesh(); } finally { Cleanup(); } }
			if (Part(only, "mlkill")) { try { MoonLordDeath(); } finally { Cleanup(); } }
			if (Part(only, "killedge")) { try { IceBlockAtTheEdge(); } finally { Cleanup(); } }
			if (Part(only, "spectarget")) { try { SpectatorSpawnedBosses(); } finally { Cleanup(); } }
			if (Part(only, "lostbet")) { try { LostBet(); } finally { Cleanup(); } }
			if (Part(only, "forfeit")) { try { Forfeit(); } finally { Cleanup(); } }
			if (Part(only, "drones")) { try { DronesDoNotHoldTheBet(); } finally { Cleanup(); } }
			if (Part(only, "regroup")) { try { RegroupOrder(); } finally { Cleanup(); } }
			if (Part(only, "redemption") && ModLoader.HasMod("Redemption")) { try { RedemptionPart(); } finally { Cleanup(); } } // only when the mod is loaded
			if (Array.IndexOf(only.Split(','), "noxus") >= 0 && ModLoader.HasMod("NoxusBoss")) { try { NoxusPart(); } finally { Cleanup(); } } // Wrath of the Gods reconnaissance: only when asked for by name (never part of "all") and the mod is loaded
		}

		// =====================================================================================================
		// CALAMITY: native-targeting types whose AI Calamity replaced
		// =====================================================================================================
		/// <summary>A type's name for the logs: vanilla's display name, a modded type's code identifier (Mod/Name), never another mod's display text.</summary>
		private static string AuditName(int type) => type < NPCID.Count ? Lang.GetNPCNameValue(type) : (NPCLoader.GetNPC(type) is ModNPC mn ? mn.Mod.Name + "/" + mn.Name : "#" + type);

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
				if (n == null || n.type != type) { rows.Add(AuditName(type) + "(" + type + "): did not spawn"); if (n != null) n.active = false; continue; }
				bool b = CalamityTargetingCompat.OverrideBound(n);
				bool native = EnemyTargeting.UsesNativeTargeting(n), flyer = EnemyTargeting.UsesFlyerSlot(n);
				bool unproven = EnemyTargeting.ModdedNativeUnproven(type); // a modded native type nobody proved safe also takes the flyer slot, whatever its AI is
				if (b) bound++;
				if (b || unproven ? (native || !flyer) : (!native || flyer)) wrong++;
				rows.Add($"{AuditName(type)}({type}): calamityOverride={b}{(unproven ? " moddedUnproven" : "")} -> {(native ? "native NPC target (300+)" : flyer ? "flyer slot 254" : "other")}");
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
		// ==============================================================================================		}

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
				Note("what the three kinds of Moon Lord NPC threw during the death: " + (thrown.Count == 0 ? "none" : string.Join(", ", thrown.Select(k => k.Key + " x" + k.Value))) + (firstEx != null ? "; first: " + firstEx : ""));
				Check("Moon Lord's death animation ends without a throw (Calamity's override AI clears itself on the server inside its own PreAI)", thrown.Count == 0);
				Check("the core and its parts are gone after the kill", !core.active && !Main.npc[part[0]].active && !Main.npc[part[1]].active && !Main.npc[part[2]].active);
				Check($"the SendExtraAI hook is installed and kept the dead core's override through its own pass (kept {CalamityTargetingCompat.OverrideKept} times), so the fallback never had a null read to answer ({CalamityTargetingCompat.DeathNreAnswered})",
					CalamityTargetingCompat.SendHookInstalled && CalamityTargetingCompat.OverrideKept >= 1 && CalamityTargetingCompat.DeathNreAnswered == 0);
			}
			finally { On_NPC.UpdateNPC -= det; }
		}

		// =====================================================================================================
		// AN ICE BLOCK THAT DIES PAST THE EDGE OF THE WORLD
		// =====================================================================================================
		/// <summary>
		/// A levy with an Ice Rod shoots up at a boss that flies high. Vanilla Update drops a projectile that has left the world without calling Kill, but an Ice Block that crosses the top edge in the same step its
		/// time runs out reaches Kill with its position above the world (seen at y = -19.8 px), and Kill's read of the tile under it (<c>Main.tile[(int)position.X / 16, (int)position.Y / 16]</c>, row -1) throws
		/// IndexOutOfRange; the exception passed through UseGuard.KillDetour in a Queen Slime fight. An army round of that type must die cleanly wherever it crosses, and the block's own Kill must still run.
		/// </summary>
		private static void IceBlockAtTheEdge()
		{
			Arena(FloorY, Half);
			PlaceCommander(0);
			var levies = Levies(2, 3f);
			Check("two levies spawn", levies.Count == 2);
			Step(2);
			int soldier = levies.Count > 0 ? levies[0].NPC.whoAmI : 0;
			float mx = Main.maxTilesX * 16f, my = Main.maxTilesY * 16f;
			var cases = new (string name, Vector2 at, bool army)[]
			{
				("above the top edge (the case seen: y = -19.8 px)", new Vector2(Cx * 16f, -19.76f), true),
				("far above the top edge", new Vector2(Cx * 16f, -400f), true),
				("past the left edge", new Vector2(-30f, 300f * 16f), true),
				("past the right edge", new Vector2(mx + 30f, 300f * 16f), true),
				("past the bottom edge", new Vector2(Cx * 16f, my + 30f), true),
				("inside the world (nothing to clamp)", new Vector2(Cx * 16f, (FloorY - 20) * 16f), true),
			};
			int clamped0 = global::Levy.Common.Owner.UseGuard.Clamped;
			foreach (var (name, at, army) in cases)
			{
				int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("BossBugsCheck"), new Vector2(Cx * 16f, (FloorY - 20) * 16f), new Vector2(0f, -19.8f), ProjectileID.IceBlock, 40, 0f, 0);
				if (pi < 0 || pi >= Main.maxProjectiles) { Check($"Ice Block {name}: the projectile spawns", false); continue; }
				Projectile pr = Main.projectile[pi];
				if (army && pr.TryGetGlobalProjectile(out SoldierProjectileTag tag)) tag.SoldierIndex = soldier;
				pr.position = at; pr.timeLeft = 0;
				int before = global::Levy.Common.Owner.UseGuard.Clamped;
				bool outside = at.X < 0 || at.Y < 0 || at.X >= mx || at.Y >= my;
				Exception thrown = null;
				try { pr.Kill(); } catch (Exception e) { thrown = e; }
				Check($"an army Ice Block dies cleanly {name}" + (thrown != null ? " (threw " + thrown.GetType().Name + ")" : ""), thrown == null && !pr.active);
				int moves = global::Levy.Common.Owner.UseGuard.Clamped - before;
				Check($"{name}: the dying block's position is moved back into the world only when it was outside ({moves} moves)", moves == (outside ? 1 : 0));
				pr.active = false;
			}
			Check("the guard moved a position exactly for the five cases outside the world", global::Levy.Common.Owner.UseGuard.Clamped - clamped0 == 5);
		}

		// =====================================================================================================
		// SPECTATOR vs a boss's second half: the Leviathan (summoned by Anahita through a projectile) and the second Twin
		// =====================================================================================================
		private static void SpectatorSpawnedBosses()
		{
			Arena(FloorY, Half);
			bool cal = ModLoader.HasMod("CalamityMod");
			SetRevenge(cal, cal); Main.GameMode = cal ? GameModeID.Master : GameModeID.Expert;
			Main.hardMode = true;
			var cases = new List<(string name, int first, int second)> { ("Retinazer then Spazmatism", NPCID.Retinazer, NPCID.Spazmatism) };
			if (cal && ModContent.TryFind<ModNPC>("CalamityMod/Anahita", out var ana) && ModContent.TryFind<ModNPC>("CalamityMod/Leviathan", out var levi)) cases.Add(("Anahita then Leviathan", ana.Type, levi.Type));
			else Note("Calamity is not loaded: only the Twins case runs");
			int reps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BOSSBUGS_REPEAT"), out int rp) && rp > 1 ? rp : 1; // lab: sample the random boss AI many times in one boot
			foreach (var c in Enumerable.Range(0, reps).SelectMany(_ => cases))
			{
				// downed+spectator: the first half is beaten in a record that never named the second (the harness's rematch state); a Spectator commander must not hold either half.
				// undowned+visible: nothing beaten, a visible commander is targeted as usual (the first kill is solo).
				foreach (string mode in new[] { "downed+spectator", "undowned+visible" })
				{
					bool down = mode.StartsWith("downed"), spec = down;
					ClearHostiles(); RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
					for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
					global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
					if (down) global::Levy.Common.Boss.BossDownedSystem.SetOnlyKeyDownedForTest(c.first, true);
					SetBossesTarget(true); _spectator = spec;
					if (!spec) { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); }
					PlaceCommander(-20);
					var levies = Levies(8, 3f, 10f);
					Step(4);
					// the second half joins mid-fight with no owner NPC (Anahita's spawner is a projectile) and Terraria's default target 0: the commander
					NPC a = SpawnNpc(c.first, new Vector2((Cx + 40) * 16f, (_floorY - 30) * 16f));
					NPC b2 = SpawnNpc(c.second, new Vector2((Cx + 46) * 16f, (_floorY - 30) * 16f));
					string who = c.name + " / " + mode;
					Check($"({who}) both halves spawn", a != null && b2 != null);
					if (a == null || b2 == null) continue;
					b2.target = 0;
					if (down) { EnemyTargeting.WhyTrack = new string[Main.maxNPCs]; EnemyTargeting.WhyCount = new int[Main.maxNPCs]; }
					int ticks = 0, lockedA = 0, lockedB = 0, onCmdB = 0; var onCmdTicks = new List<string>(); int earlyCmd = 0, firstEarly = -1;
					_perTick = () =>
					{
						if (!a.active || !b2.active) return;
						a.timeLeft = b2.timeLeft = 99999; a.life = a.lifeMax; b2.life = b2.lifeMax;
						foreach (var l in levies) if (l.NPC.active && l.NPC.life > 0) { l.NPC.life = l.NPC.lifeMax; if (l.Record != null) l.Record.CurrentHp = l.NPC.life; } // the army must stay on the field for the whole window: this measures who the boss picks, not what Calamity's lasers do to eight levies
						if (++ticks <= 60) { if (down && ticks > 1 && !Locked(b2) && b2.target == 0) { earlyCmd++; if (firstEarly < 0) firstEarly = ticks; } return; } // a second to settle; the settling ticks are counted apart (tick 1 is the spawn state, before any AI has run)
						if (Locked(a)) lockedA++;
						if (Locked(b2)) lockedB++;
						if (!Locked(b2) && b2.target == 0)
						{
							onCmdB++;
							if (down && onCmdTicks.Count < 40) { int alive = 0; foreach (var l in levies) if (l.NPC.active && l.NPC.life > 0 && l.IsAlive) alive++; onCmdTicks.Add($"t{ticks}:alive={alive}/{levies.Count},bodies={EnemyTargeting.Bodies.Count},tgt={b2.target},why=[{EnemyTargeting.WhyTrack?[b2.whoAmI]}],ai1={b2.ai[1]:0}"); }
						}
					};
					Step(420);
					_perTick = null; EnemyTargeting.WhyTrack = null;
					int n = Math.Max(1, ticks - 60);
					if (down) Note($"({who}) settling ticks 2-60 with the boss's second half on the commander: {earlyCmd}" + (firstEarly >= 0 ? $" (first at tick {firstEarly})" : ""));
					if (onCmdTicks.Count > 0) Note($"({who}) ticks on the commander: " + string.Join(" ", onCmdTicks));
					Note($"({who}) first half locked on a levy {lockedA}/{n} ticks, second half {lockedB}/{n} ticks (on the commander {onCmdB}); undowned(second)={EnemyTargeting.IsUndownedBoss(b2)} hides spectator={EnemyTargeting.HidesSpectatorFrom(b2)}");
					if (down)
					{
						Check($"({who}) the second half counts as beaten with the first and the Spectator is hidden from it", !EnemyTargeting.IsUndownedBoss(b2) && EnemyTargeting.HidesSpectatorFrom(b2));
						Check($"({who}) the second half locks onto levies, never the Spectator commander, not on any tick ({lockedB}/{n} ticks, {onCmdB} on the commander)", lockedB * 10 >= n * 8 && onCmdB == 0);
						Check($"({who}) the first half still locks onto levies", lockedA * 10 >= n * 8);
						Check($"({who}) a downed boss's second half never has the Spectator commander as its target, from its first AI tick ({earlyCmd} settling ticks)", earlyCmd == 0);
					}
					else
					{
						Check($"({who}) an unbeaten second half is flagged and a visible commander stays its target ({lockedB}/{n} ticks on levies)", EnemyTargeting.IsUndownedBoss(b2) && lockedB * 10 <= n && onCmdB * 10 >= n * 8);
					}
					a.active = false; b2.active = false;
				}
			}
			global::Levy.Common.Boss.BossDownedSystem.ClearForTest();
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
			int wofReps = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BOSSBUGS_REPEAT"), out int wr) && wr > 1 ? wr : 1; // lab: sample the part many times in one boot
			for (int rep = 0; rep < wofReps; rep++)
			foreach (bool spectator in new[] { false, true })
			{
				ClearHostiles(); RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) Main.npc[i].active = false;
				Main.wofNPCIndex = -1; _spectator = spectator;
				if (!spectator) { var b = Main.player[0]; int bt = ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(); if (b.HasBuff(bt)) b.ClearBuff(bt); }
				string who = spectator ? "Spectator commander" : "visible commander";
				PlaceCommander(-6);
				var levies = Levies(10, 3f, -6f);
				// A levy with no weapon keeps away from what it cannot fight and the wall walks no faster than a levy runs, so unarmed levies simply stay ahead of it for the whole window (observed:
				// they end 50+ tiles ahead and nothing of the wall ever touches them). A tall wall of brick behind the army (lab: LEVY_BOSSBUGS_WOFBARRIER=0 removes it) leaves them nowhere to go, so the wall arrives.
				bool barrier = Environment.GetEnvironmentVariable("LEVY_BOSSBUGS_WOFBARRIER") != "0";
				if (barrier) for (int bx = Cx - 27; bx <= Cx - 26; bx++) for (int by = fy - 119; by < fy; by++) { Tile bt = Main.tile[bx, by]; bt.ClearEverything(); bt.HasTile = true; bt.TileType = TileID.GrayBrick; bt.BlockType = BlockType.Solid; }
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
				// a levy the wall nearly kills is topped up again (the damage is counted as it lands): the wall's pass over the army takes ~1200 ticks and a levy dead in the first 100 leaves no reach window to judge
				var prevLife = new Dictionary<SoldierNPC, int>(); int dmgTaken = 0;
				float minDist = float.MaxValue; int minDistTick = -1, nearTicks = 0; long swStart = System.Diagnostics.Stopwatch.GetTimestamp();
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
					float wd = float.MaxValue; foreach (var l in levies) if (l.NPC.active) wd = Math.Min(wd, Math.Abs(wall.Center.X - l.NPC.Center.X));
					if (wd < minDist) { minDist = wd; minDistTick = wofTick; }
					if (wd < 35f * 16f) nearTicks++;
					foreach (var l in levies)
					{
						if (!l.NPC.active) continue;
						if (prevLife.TryGetValue(l, out int pl) && l.NPC.life < pl) dmgTaken += pl - l.NPC.life;
						if (l.NPC.life < l.NPC.lifeMax / 3) { l.NPC.life = l.NPC.lifeMax; if (l.Record != null) l.Record.CurrentHp = l.NPC.lifeMax; }
						prevLife[l] = l.NPC.life;
					}
					wall.life = wall.lifeMax; // the army here has no weapons: the wall stays up for the whole window
					if (++wofTick % 300 == 0)
					{
						float hd = float.MaxValue; int nh = 0; foreach (var q in Main.npc) if (q.active && (q.type == NPCID.TheHungry || q.type == NPCID.TheHungryII)) { nh++; foreach (var l in levies) if (l.NPC.active) hd = Math.Min(hd, Vector2.Distance(q.Center, l.NPC.Center)); }
						Note($"({who}) tick {wofTick}: wall at x={(wall.Center.X / 16f - Cx):0} y={(wall.Center.Y / 16f - fy):0} v=({wall.velocity.X:0.0},{wall.velocity.Y:0.0}) target={wall.target} ai0={wall.ai[0]:0} ai1={wall.ai[1]:0} ai2={wall.ai[2]:0}; {nh} Hungry, nearest {(hd / 16f):0} tiles from a levy; levies at x={(levies[0].NPC.Center.X / 16f - Cx):0}..{(levies[levies.Count - 1].NPC.Center.X / 16f - Cx):0}");
					}
				};
				// the window ends on conditions, not a tick count: at least 1500 ticks, then until the wall has been within reach of the army (eyes and mouth in 35 tiles for 100+ ticks) and has hurt it, capped at 6000
				Step(1500);
				while (wofTick < 6000 && !(act.GetValueOrDefault(NPCID.WallofFleshEye) > 100 && act.GetValueOrDefault(NPCID.WallofFlesh) > 100 && dmgTaken > 0)) Step(100);
				_perTick = null;
				if (barrier) for (int bx = Cx - 27; bx <= Cx - 26; bx++) for (int by = fy - 119; by < fy; by++) { Tile bt = Main.tile[bx, by]; bt.ClearEverything(); }
				int lev1 = lev0 - dmgTaken;
				string Row(int t) => $"{Lang.GetNPCNameValue(t)} {lck.GetValueOrDefault(t)}/{act.GetValueOrDefault(t)} ticks locked on a levy while one is within 35 tiles sideways";
				Note($"({who}) {string.Join("; ", kinds.Select(Row))}");
				Note($"({who}) hostile projectiles seen (ticks, closest approach to a levy in tiles): " + (shots.Count == 0 ? "none" : string.Join(", ", shots.Select(k => Lang.GetProjectileName(k.Key).Value + "(" + k.Key + ")=" + k.Value + " / " + (shotMin[k.Key] / 16f).ToString("0.0")))));
				Note($"({who}) damage the levies took by source: " + (SoldierNPC.HitDbg.Count == 0 ? "none" : string.Join(", ", SoldierNPC.HitDbg.OrderByDescending(k => k.Value.dmg).Select(k => k.Key + "=" + k.Value.dmg + " (" + k.Value.n + " hits)"))) + $"; levy life {lev0} -> {lev1}");
				Check($"({who}) the lasers the eyes fire are aimed at the levy each eye is locked on ({aimErrs.Count} sampled, worst {(aimErrs.Count > 0 ? aimErrs.Max() : 0):0} deg off)", aimErrs.Count == 0 || aimErrs.Max() <= 12f);
				double wallMs = (System.Diagnostics.Stopwatch.GetTimestamp() - swStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				string Diag() => $"rep {rep}; {wallMs / Math.Max(1, wofTick):0.00} ms/tick, {wofTick} ticks; wall now at ({(wall.Center.X / 16f - Cx):0},{(wall.Center.Y / 16f - fy):0}) target={wall.target}; closest wall-to-levy sideways {(minDist / 16f):0.0} tiles at tick {minDistTick}, {nearTicks} ticks within 35 tiles; levies active {levies.Count(l => l.NPC.active)}/{levies.Count} at x={(levies.Where(l => l.NPC.active).Select(l => l.NPC.Center.X / 16f - Cx).DefaultIfEmpty(0).Min()):0}..{(levies.Where(l => l.NPC.active).Select(l => l.NPC.Center.X / 16f - Cx).DefaultIfEmpty(0).Max()):0} y={(levies.Where(l => l.NPC.active).Select(l => l.NPC.Center.Y / 16f - fy).DefaultIfEmpty(0).Average()):0}; eyes {lck.GetValueOrDefault(NPCID.WallofFleshEye)}/{act.GetValueOrDefault(NPCID.WallofFleshEye)} locked/active, Hungry {lck.GetValueOrDefault(NPCID.TheHungry)}/{act.GetValueOrDefault(NPCID.TheHungry)}, mouth {lck.GetValueOrDefault(NPCID.WallofFlesh)}/{act.GetValueOrDefault(NPCID.WallofFlesh)}; shots {string.Join(",", shots.Select(k => k.Key + "=" + k.Value))}; hits {(SoldierNPC.HitDbg == null || SoldierNPC.HitDbg.Count == 0 ? "none" : string.Join(",", SoldierNPC.HitDbg.Select(k => k.Key + "=" + k.Value.dmg + "/" + k.Value.n)))}";
				Note($"({who}) rep {rep}: {wallMs / Math.Max(1, wofTick):0.00} ms/tick, {wofTick} ticks, closest wall-to-levy sideways {(minDist / 16f):0.0} tiles, {nearTicks} ticks within 35 tiles, levy life {lev0} -> {lev1}");
				Check($"({who}) the Wall of Flesh's eyes lock onto levies", act.GetValueOrDefault(NPCID.WallofFleshEye) > 100 && lck.GetValueOrDefault(NPCID.WallofFleshEye) * 10 >= act.GetValueOrDefault(NPCID.WallofFleshEye) * 6, Diag);
				int hungry = act.GetValueOrDefault(NPCID.TheHungry) + act.GetValueOrDefault(NPCID.TheHungryII), hungryL = lck.GetValueOrDefault(NPCID.TheHungry) + lck.GetValueOrDefault(NPCID.TheHungryII);
				Check($"({who}) the Hungry (when any are out) lock onto levies", hungry == 0 || hungryL * 10 >= hungry * 6);
				if (spectator) Check($"({who}) the mouth walks at the levies when there is no visible player", act.GetValueOrDefault(NPCID.WallofFlesh) > 100 && lck.GetValueOrDefault(NPCID.WallofFlesh) * 10 >= act.GetValueOrDefault(NPCID.WallofFlesh) * 6);
				else Check($"({who}) the mouth keeps walking at the visible player", act.GetValueOrDefault(NPCID.WallofFlesh) > 100 && lck.GetValueOrDefault(NPCID.WallofFlesh) * 10 < act.GetValueOrDefault(NPCID.WallofFlesh) * 4);
				Check($"({who}) the wall hurts the levies (life {lev0} -> {lev1})", lev1 < lev0, Diag);
				SoldierNPC.HitDbg = null;
			}
		}
	}
}
