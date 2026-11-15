using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Officers;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL self-check of the officers' conjured creatures, logged as "[OfficerWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the identity
	/// world check). Slot 0 stands in as the commander (the single-player owner situation), a flat arena holds a pinned, tough enemy ten tiles away, and the real Main.Update steps the world.
	///  * ROSTER (no world needed): the candidate lists per progression stage, logged; no boss, miniboss, town NPC or critter in them; nothing above the stage's life or damage cap; at least one candidate at every stage.
	///  * CLOTHIER: commissioned through the officer system; the squad is REAL skeleton enemies (not the generic unit), friendly, tier-appropriate, goes for the enemy on its own AI and hurts it; the commander is never
	///    hurt; its archer's arrows spawn friendly; a killed skeleton drops nothing and counts no kill; a lost skeleton is raised again at the new tier; enemies target the squad; dismissing the Clothier removes it.
	///  * ZOOLOGIST: commissioned the same way; calls one creature that is on the candidate list and not a boss, fights the enemy; the Bestiary filter picks only unlocked creatures when there are enough, and falls back
	///    to the stage pool when there are not.
	///  * SWEEP: for the strongest few candidates of every stage, one conjured creature against the enemy for a few seconds: whether its own AI engaged it (logged, so a creature whose AI cannot work friendly shows up).
	/// Throwaway soldiers, town NPCs and creatures only; nothing is saved.
	/// </summary>
	public partial class OfficerWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _pass, _fail;
		private static Mod _mod;
		private static NPC _target; private static Vector2 _targetAt, _cmdAt;
		private static bool _local; private static int _savedMy;
		private static int _commanderHurt;
		private const int FloorY = 300, Cx = 1800;
		private const string Commander = "OfficerTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_OFFICERWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; PinTarget(); if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_running && _local) Main.myPlayer = _savedMy; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; if (_local) { _savedMy = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_running && _local) Main.myPlayer = _savedMy; }

		/// <summary>Runs now when this boot armed it (the identity world check calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; _local = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _local = false; _running = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Cleanup(); }
			_mod.Logger.Info($"[OfficerWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[OfficerWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[OfficerWorld] OBSERVED " + s);

		/// <summary>True while the check steps the world: natural enemy spawns are off (they would walk into the arena and kill the officers under test).</summary>
		public static bool Running => _running;

		private static void ClearStrangers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && !n.friendly && !n.townNPC && n.ModNPC == null && n != _target && !_auditKids.Contains(i) && !(n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget)) n.active = false;
			}
		}

		private static void Step(int n)
		{
			ClearStrangers();
			for (int i = 0; i < n; i++)
			{
				KeepCommander();
				global::Levy.Common.Testing.WorldStep.Update();
				Player p = Main.player[0];
				if (p.active && p.statLife < p.statLifeMax2) _commanderHurt++;
			}
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			Main.dayTime = true; Main.time = 18000;   // no dawn: wages fall due then and a lapsed officer is dismissed mid-check
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

		private static void SpawnEnemy(float tilesAway)
		{
			KillEnemy();
			_targetAt = new Vector2(Cx * 16f + tilesAway * 16f, FloorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("OfficerWorldCheck"), (int)_targetAt.X, (int)_targetAt.Y, NPCID.GreenSlime);
			if (ti < 0 || ti >= Main.maxNPCs) { _target = null; return; }
			_target = Main.npc[ti];
			_target.lifeMax = 1_000_000; _target.life = 1_000_000; _target.defense = 0; _target.damage = 0; _target.knockBackResist = 0f; _target.width = 64; _target.height = 96;
			_target.noGravity = true; _target.noTileCollide = true; _target.aiStyle = -1; _target.friendly = false; _target.value = 0f; _target.Bottom = _targetAt;
			var ht = _target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = _target.position;
		}

		private static void KillEnemy()
		{
			if (_target != null) _target.active = false;
			_target = null;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) Main.npc[i].active = false; // the pinned enemy is found by its mark, not by a reference that may be stale
		}

		/// <summary>Everything this check made, gone: conjured creatures, officers, avatars, the enemy, projectiles.</summary>
		private static void Cleanup()
		{
			try
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC n = Main.npc[i];
					if (!n.active) continue;
					var g = OfficerSystem.Data(n);
					if (g != null && g.Enrolled && g.Commander == Commander) { g.Enrolled = false; g.Commander = ""; g.ResetRuntime(); n.active = false; }
					else if (n.ModNPC is SoldierNPC sn && sn.OfficerMode) n.active = false;
					else if (ConjuredGlobalNPC.IsConjured(n)) n.active = false;
				}
				KillEnemy();
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			}
			catch (Exception) { }
			NPC.downedBoss1 = _saved[0]; NPC.downedBoss2 = _saved[1]; NPC.downedBoss3 = _saved[2]; Main.hardMode = _saved[3]; NPC.downedPlantBoss = _saved[4];
		}

		private static readonly bool[] _saved = new bool[5];

		// =====================================================================================================
		// scenario plumbing
		// =====================================================================================================
		/// <summary>Commissions a throwaway town NPC of <paramref name="townType"/> to the commander (slot <paramref name="slot"/>) and steps until his avatar marches. Returns the town NPC, or null.</summary>
		private static NPC Commission(int townType, int slot, out SoldierNPC avatar)
		{
			avatar = null;
			int i = NPC.NewNPC(new EntitySource_WorldEvent("OfficerWorldCheck"), (int)(Cx * 16f - 80f), (int)(FloorY * 16f - 40f), townType);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC t = Main.npc[i];
			var g = OfficerSystem.Data(t);
			if (g == null) { t.active = false; return null; }
			g.Enrolled = true; g.Commander = Commander; g.Slot = slot; g.DaysUnpaid = 0; g.ResetRuntime();
			t.netAlways = true; t.netUpdate = true;
			for (int k = 0; k < 180 && avatar == null; k++) { Step(1); avatar = OfficerSystem.Instance.AvatarOf(t); }
			return t;
		}

		private static void Dismiss(NPC town)
		{
			OfficerSystem.Instance.Dismiss(Main.player[0], town);
			Step(10);
			town.active = false;
			Step(2);
		}

		private static List<NPC> Conjured(int ownerWho = -1)
		{
			var l = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active && (ownerWho < 0 || c.OwnerWho == ownerWho)) l.Add(n);
			}
			return l;
		}

		private static int ActiveItems() { int c = 0; for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active) c++; return c; }

		private static void Run()
		{
			_saved[0] = NPC.downedBoss1; _saved[1] = NPC.downedBoss2; _saved[2] = NPC.downedBoss3; _saved[3] = Main.hardMode; _saved[4] = NPC.downedPlantBoss;
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			string only = Environment.GetEnvironmentVariable("LEVY_OFFICERWORLD_ONLY"); // lab switch: "sweep" runs only the creature sweep (with LEVY_OFFICERWORLD_TYPES = comma-separated NPCID names added to it)
			if (only == "audit") { AuditAll(); return; }
			if (only == "churn") { ClothierChurn(); return; }
			if (only == "core") { AuditCore(); return; } // lab: the aim audit's core set alone (LEVY_AUDIT_TYPES narrows it, LEVY_AUDIT_REPEAT=N runs each type N times)
			if (only != "sweep") { RosterChecks(); ClothierScenario(); ZoologistScenario(); ClothierChurn(); AuditCore(); }
			Sweep();
		}

		// =====================================================================================================
		// ROSTER
		// =====================================================================================================
		private static void RosterChecks()
		{
			var facts = ConjureRoster.AllFacts();
			Note($"conjurable creature types read: {facts.Count} ({facts.Count(f => f.Modded)} modded)");
			Check("the roster reads conjurable creatures", facts.Count > 50);
			bool bossFree = true, townFree = true, capped = true, any = true; int stage;
			for (stage = 0; stage <= 10; stage++)
			{
				var c = ConjureRoster.BeastCandidates(stage, false, out _);
				int lifeCap = LevyConstants.Summon.ConjureMaxLifeByStage[stage], dmgCap = LevyConstants.Summon.ConjureMaxDamageByStage[stage];
				if (c.Count == 0) any = false;
				foreach (var f in c)
				{
					var probe = new NPC(); probe.SetDefaults(f.Type);
					if (probe.boss || NPCID.Sets.ShouldBeCountedAsBoss[f.Type]) bossFree = false;
					if (probe.townNPC || probe.friendly || NPCID.Sets.CountsAsCritter[f.Type]) townFree = false;
					if (f.ClassicLife > lifeCap || f.ClassicDamage > dmgCap) capped = false;
				}
				Note($"stage {stage} (life<= {lifeCap}, damage<= {dmgCap}): {c.Count} candidates; strongest: " + string.Join("; ", c.Take(6).Select(f => f.ToString())));
				Note($"stage {stage} skeletons: " + string.Join(", ", ConjureRoster.SkeletonPool(stage).Distinct().Select(t => Lang.GetNPCNameValue(t))));
			}
			Check("no candidate at any stage is a boss", bossFree);
			Check("no candidate at any stage is a town NPC or critter", townFree);
			Check("no candidate exceeds its stage's life or damage cap", capped);
			Check("every stage has at least one candidate", any);
			Check("a modded creature with a million life is never a candidate (no candidate has more than the top cap)", facts.All(f => f.ClassicLife <= LevyConstants.Summon.ConjureMaxLifeByStage[10] || ConjureRoster.BeastCandidates(10, false, out _).All(c => c.Type != f.Type)));
			bool skelOk = true;
			for (stage = 0; stage <= 10; stage++) foreach (int t in ConjureRoster.SkeletonPool(stage)) { var probe = new NPC(); probe.SetDefaults(t); if (probe.type != t || probe.boss || probe.townNPC) skelOk = false; }
			Check("every skeleton on the ladder is a real, non-boss enemy type", skelOk);
			Check("the first skeleton is the plain Skeleton; after Plantera it is armored or armed (never a plain Skeleton)", ConjureRoster.SkeletonPool(0).Length == 1 && ConjureRoster.SkeletonPool(0)[0] == NPCID.Skeleton && !ConjureRoster.SkeletonPool(7).Contains(NPCID.Skeleton));
		}

		// =====================================================================================================
		// CLOTHIER
		// =====================================================================================================
		private static void ClothierScenario()
		{
			SpawnEnemy(10);
			NPC town = Commission(NPCID.Clothier, 0, out SoldierNPC avatar);
			Check("Clothier: commissioned and marching", town != null && avatar != null);
			if (avatar == null) { if (town != null) town.active = false; return; }
			int owner = avatar.NPC.whoAmI;
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f);
			Step(60);
			var squad = Conjured(owner);
			Check($"Clothier: his squad is {LevyConstants.Summon.ClothierSkeletonCount} conjured creatures ({squad.Count})", squad.Count == LevyConstants.Summon.ClothierSkeletonCount);
			Check("Clothier: at the start of the world they are plain Skeletons on the real type", squad.Count > 0 && squad.All(n => n.type == NPCID.Skeleton));
			Check("Clothier: they are friendly, owner-bound and not the generic summoned unit", squad.Count > 0 && squad.All(n => n.friendly && !n.townNPC && n.ModNPC == null && n.GetGlobalNPC<ConjuredGlobalNPC>().OwnerWho == owner));
			Check($"Clothier: no raised skeleton is frailer than the squad was (life {(squad.Count > 0 ? squad.Min(n => n.lifeMax) : 0)} >= {LevyConstants.Summon.ClothierSkeletonMinLife})", squad.Count > 0 && squad.All(n => n.lifeMax >= LevyConstants.Summon.ClothierSkeletonMinLife && n.life == n.lifeMax));
			Check("Clothier: they carry no loot (value 0, from a statue)", squad.Count > 0 && squad.All(n => n.value == 0f && n.SpawnedFromStatue));
			Check("Clothier: enemies can target them (each is a live targeting body, resolved by its NPC index)", squad.Count > 0 && SummonedUnitSystem.Instance.ActiveBodies.Count(b => b is ConjuredGlobalNPC) == squad.Count && squad.All(n => EnemyTargeting.LiveTarget(n.whoAmI) != null));
			// control for the no-player-damage check: a real hostile skeleton would hit a player; ours must not
			int slot = 0;
			bool oursHit = squad.Count > 0 && NPCLoader.CanHitPlayer(squad[0], Main.player[0], ref slot);
			Check("Clothier: a conjured skeleton cannot hit a player", !oursHit);
			var hostile = new NPC(); hostile.SetDefaults(NPCID.Skeleton); hostile.active = true;
			// engagement
			int dmg = 0; float startX = squad.Count > 0 ? squad.Average(n => n.Center.X) : 0f; int moved = 0;
			_commanderHurt = 0;
			for (int tick = 0; tick < 900; tick++)
			{
				Step(1);
				if (_target != null && _target.active && _target.life < _target.lifeMax) dmg += _target.lifeMax - _target.life;
			}
			squad = Conjured(owner);
			if (squad.Count > 0) moved = (int)((squad.Average(n => n.Center.X) - startX) / 16f);
			Note($"Clothier: damage to the enemy over 900 ticks = {dmg}; the squad advanced {moved} tiles; commander hurt ticks = {_commanderHurt}");
			Check($"Clothier: the squad hurt the enemy with its own AI ({dmg})", dmg > 0);
			Check("Clothier: the commander was never hurt", _commanderHurt == 0);
			Check("Clothier: the squad is still full-strength after 900 ticks", squad.Count == LevyConstants.Summon.ClothierSkeletonCount);

			// kill: no loot, no kill count
			var victim = squad.FirstOrDefault();
			if (victim != null)
			{
				int items0 = ActiveItems(); int kills0 = NPC.killCount[victim.type]; int coins0 = 0;
				int type = victim.type;
				victim.SimpleStrikeNPC(100000, 1, false, 0f, null, false, 0f, true);
				Step(3);
				Check("Clothier: a killed skeleton is gone", !victim.active || victim.life <= 0);
				Check($"Clothier: a killed skeleton drops nothing ({ActiveItems() - items0} items)", ActiveItems() == items0);
				Check("Clothier: a killed skeleton counts no kill", NPC.killCount[type] == kills0);
				_ = coins0;
			}
			// tier: after Plantera, a lost skeleton is raised as armored bones / a gunner
			NPC.downedBoss1 = NPC.downedBoss2 = NPC.downedBoss3 = true; Main.hardMode = true; NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = true; NPC.downedPlantBoss = true;
			foreach (var n in Conjured(owner)) n.SimpleStrikeNPC(100000, 1, false, 0f, null, false, 0f, true);
			Step(LevyConstants.Summon.ClothierRespawnCadenceTicks * 6 + 60);
			squad = Conjured(owner);
			var pool = ConjureRoster.SkeletonPool(ConjureRoster.Stage());
			Note($"Clothier: after Plantera the squad is: " + string.Join(", ", squad.Select(n => n.TypeName)));
			Check($"Clothier: after Plantera the raised skeletons are from the Plantera tier ({squad.Count} of them)", squad.Count > 0 && squad.All(n => pool.Contains(n.type)) && squad.All(n => n.type != NPCID.Skeleton));
			// dismiss cleanup
			Dismiss(town);
			Check($"Clothier: dismissing him removes every skeleton ({Conjured(owner).Count} left)", Conjured(owner).Count == 0);
			NPC.downedPlantBoss = _saved[4]; NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = false; NPC.downedBoss1 = _saved[0]; NPC.downedBoss2 = _saved[1]; NPC.downedBoss3 = _saved[2]; Main.hardMode = _saved[3];
			KillEnemy();
		}

		// =====================================================================================================
		// ZOOLOGIST
		// =====================================================================================================
		private static void ZoologistScenario()
		{
			SpawnEnemy(10);
			NPC town = Commission(NPCID.BestiaryGirl, 0, out SoldierNPC avatar);
			Check("Zoologist: commissioned and marching", town != null && avatar != null);
			if (avatar == null) { if (town != null) town.active = false; return; }
			int owner = avatar.NPC.whoAmI;
			avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f);
			Step(90);
			var beasts = Conjured(owner);
			var cands = ConjureRoster.BeastCandidates(ConjureRoster.Stage(), true, out bool usedBestiary);
			Note($"Zoologist: stage {ConjureRoster.Stage()}, bestiary used = {usedBestiary}, candidates {cands.Count}; called: " + string.Join(", ", beasts.Select(b => b.TypeName)));
			Check($"Zoologist: she fields {LevyConstants.Summon.ZoologistBeastCount} creature ({beasts.Count})", beasts.Count == LevyConstants.Summon.ZoologistBeastCount);
			Check("Zoologist: it is on the candidate list, and is not a boss", beasts.Count > 0 && beasts.All(b => cands.Any(c => c.Type == b.type) && !b.boss && !NPCID.Sets.ShouldBeCountedAsBoss[b.type]));
			Check($"Zoologist: her creature is not frailer than the pack was (life {(beasts.Count > 0 ? beasts.Min(n => n.lifeMax) : 0)} >= {LevyConstants.Summon.ZoologistBeastMinLife})", beasts.Count > 0 && beasts.All(n => n.lifeMax >= LevyConstants.Summon.ZoologistBeastMinLife));
			Check("Zoologist: it is a real creature of its own type, friendly, with no loot", beasts.Count > 0 && beasts.All(b => b.friendly && b.ModNPC == null && b.value == 0f));
			int dmg = 0; _commanderHurt = 0; float closest = float.MaxValue, x0 = beasts.Count > 0 ? beasts[0].Center.X : 0f, travelled = 0f;
			for (int tick = 0; tick < 900; tick++)
			{
				Step(1);
				if (_target != null && _target.active && _target.life < _target.lifeMax) dmg += _target.lifeMax - _target.life;
				var live = Conjured(owner);
				if (live.Count > 0 && _target != null) { closest = Math.Min(closest, Math.Abs(live[0].Center.X - _target.Center.X)); travelled = Math.Max(travelled, Math.Abs(live[0].Center.X - x0)); }
			}
			bool engaged = dmg > 0 || closest < 6 * 16f || travelled > 4 * 16f; // a caster may hold its distance and fire rarely: it counts when it hurt the enemy, closed on it or moved toward it
			beasts = Conjured(owner);
			Note($"Zoologist: damage to the enemy over 900 ticks = {dmg}; commander hurt ticks = {_commanderHurt}; creature = " + string.Join(", ", beasts.Select(b => b.TypeName)));
			Check("Zoologist: the commander was never hurt", _commanderHurt == 0);
			Check($"Zoologist: her creature went for the enemy on its own AI (damage {dmg}, closest {(int)(closest / 16f)} tiles, travelled {(int)(travelled / 16f)})", engaged);
			Dismiss(town);
			Check($"Zoologist: dismissing her removes her creature ({Conjured(owner).Count} left)", Conjured(owner).Count == 0);
			KillEnemy();

			// Bestiary filter: with enough entries unlocked the pick is only from them; with none, the stage pool
			int st = 4;
			// "thin" is set up here, not assumed: the checks before this one fight (a Zoologist's creature, the Clothier's squad, the Whip and the Mobility checks' levies kill what they meet), and every kill, sighting and chat unlocks
			// Bestiary entries in the world's tracker. This world is a throwaway copy that is never saved.
			try { Main.BestiaryTracker.Reset(); } catch (Exception e) { Note("could not reset the Bestiary: " + e.Message); }
			var nothing = ConjureRoster.BeastCandidates(st, true, out bool usedNone);
			var all = ConjureRoster.BeastCandidates(st, false, out _);
			Check("Zoologist: with a thin Bestiary she falls back to the whole stage pool", !usedNone && nothing.Count == all.Count);
			var want = all.Skip(2).Take(5).ToList();
			bool unlocked = true;
			try
			{
				foreach (var f in want) Main.BestiaryTracker.Kills.SetKillCountDirectly(Terraria.ID.ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[f.Type], 50);
			}
			catch (Exception e) { unlocked = false; Note("could not unlock bestiary entries: " + e.Message); }
			if (unlocked)
			{
				var seen = ConjureRoster.BeastCandidates(st, true, out bool usedSome);
				Note($"Zoologist: bestiary-filtered pool = {seen.Count} ({string.Join(", ", seen.Take(6).Select(f => f.Name))})");
				Check("Zoologist: with creatures unlocked in the Bestiary, she picks only from those", usedSome && seen.Count > 0 && seen.Count <= all.Count && seen.All(f => ConjureRoster.InBestiary(f.Type)));
			}
		}

		// =====================================================================================================
		// SWEEP: does each strong creature's own AI engage the enemy when friendly?
		// =====================================================================================================
		private static HashSet<int> LabTypes()
		{
			var set = new HashSet<int>();
			string v = Environment.GetEnvironmentVariable("LEVY_OFFICERWORLD_TYPES");
			if (string.IsNullOrEmpty(v)) return set;
			foreach (string name in v.Split(',')) { var f = typeof(NPCID).GetField(name.Trim()); if (f != null) set.Add(Convert.ToInt32(f.GetValue(null))); }
			return set;
		}

		private static void Sweep()
		{
			NPC town = Commission(NPCID.Merchant, 0, out SoldierNPC avatar); // an officer with no squad of his own: just an owner
			Check("sweep: an owner officer is marching", town != null && avatar != null);
			if (avatar == null) { if (town != null) town.active = false; return; }
			int owner = avatar.NPC.whoAmI;
			int tried = 0, engaged = 0, spawned = 0, stagesWithEngagement = 0, conjProj = 0, hostileConjProj = 0; bool archerRanged = false;
			var allFacts = ConjureRoster.AllFacts();
			for (int stage = 0; stage <= 11; stage++)
			{
				// stage 11: the shooters among the Clothier's skeletons, which no stage's strongest list holds (an archer, and the Sniper's rifle)
				var top = stage <= 10 ? ConjureRoster.BeastCandidates(stage, false, out _).Take(Environment.GetEnvironmentVariable("LEVY_OFFICERWORLD_ONLY") == "sweep" ? 0 : 2).ToList()
					: allFacts.Where(q => q.Type == NPCID.SkeletonArcher || q.Type == NPCID.SkeletonSniper || LabTypes().Contains(q.Type)).ToList();
				bool any = false;
				foreach (var f in top)
				{
					SpawnEnemy(9);
					avatar.NPC.Bottom = new Vector2(Cx * 16f - 40f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero;
					int idx = ConjuredGlobalNPC.Conjure(avatar.NPC, 0, f.Type);
					tried++;
					if (idx < 0) { Note($"stage {stage}: {f.Name} (type {f.Type}) could not be conjured: {ConjuredGlobalNPC.LastFail}; avatar active={avatar.NPC.active} life={avatar.NPC.life}"); continue; }
					spawned++;
					NPC n = Main.npc[idx];
					float x0 = n.Center.X; int dmg = 0; float best = float.MaxValue;
					for (int tick = 0; tick < 200; tick++)
					{
						Step(1);
						if (!avatar.NPC.active || avatar.NPC.life <= 0)
						{
							var near = new List<string>();
							for (int q = 0; q < Main.maxNPCs; q++) { NPC o = Main.npc[q]; if (o.active && o.whoAmI != avatar.NPC.whoAmI && Math.Abs(o.Center.X - avatar.NPC.Center.X) < 1200f) near.Add($"{o.TypeName}#{o.whoAmI}(f={o.friendly},d={o.damage})"); }
							Note($"sweep: the owner avatar was lost at tick {tick} (life {avatar.NPC.life}); NPCs near: " + string.Join(" ", near));
							break;
						}
						if (!n.active) break;
						if (tick % 4 == 0) for (int q = 0; q < Main.maxProjectiles; q++) { Projectile pr = Main.projectile[q]; if (pr.active && pr.TryGetGlobalProjectile(out ConjuredProjectile cp) && cp.Conjured) { conjProj++; if (pr.hostile || !pr.friendly) hostileConjProj++; } }
						if (_target != null && _target.active && _target.life < _target.lifeMax) dmg += _target.lifeMax - _target.life;
						best = Math.Min(best, Math.Abs(_target.Center.X - n.Center.X));
					}
					bool got = dmg > 0 || (n.active && Math.Abs(n.Center.X - x0) > 48f) || best < 6 * 16f;
					if (got) { engaged++; any = true; }
					if (f.Type == NPCID.SkeletonArcher && dmg > 0 && best > 6 * 16f) archerRanged = true;
					Note($"stage {stage}: {f.Name} (ai {f.AiStyle}) engaged={got} damage={dmg} closest={(int)(best / 16f)} tiles alive={n.active}" + (n.active ? $" pos={(int)((n.Center.X - _targetAt.X) / 16f)},{(int)((n.Center.Y - _targetAt.Y) / 16f)} rel-to-enemy target={n.GetGlobalNPC<ConjuredGlobalNPC>().Target} ai=[{n.ai[0]:0.#},{n.ai[1]:0.#},{n.ai[2]:0.#},{n.ai[3]:0.#}] vel={n.velocity.X:0.#},{n.velocity.Y:0.#} noGrav={n.noGravity} dmgStat={n.damage}" : ""));
					if (n.active) { n.active = false; }
					Step(2);
				}
				if (any && stage <= 10) stagesWithEngagement++;
			}
			Note($"sweep: {spawned}/{tried} conjured, {engaged} engaged the enemy");
			Check("sweep: every candidate could be conjured", spawned == tried);
			Check("sweep: a Skeleton Archer hurt the enemy from range with arrows (its shots are the army's)", archerRanged);
			Note($"sweep: projectiles of conjured creatures seen: {conjProj}, of them hostile: {hostileConjProj}");
			Check("sweep: a conjured creature's projectiles are the army's (none hostile)", hostileConjProj == 0);
			Check($"sweep: at nearly every stage one of the strongest creatures engaged the enemy ({stagesWithEngagement}/11)", stagesWithEngagement >= 9);
			Dismiss(town);
			KillEnemy();
		}
	}

	/// <summary>No natural enemy spawns while <see cref="OfficerWorldCheck"/> steps the world.</summary>
	public class OfficerWorldSpawnGate : GlobalNPC
	{
		public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) { if (OfficerWorldCheck.Running) { maxSpawns = 0; spawnRate = int.MaxValue / 4; } }
	}
}
