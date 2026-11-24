using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Budget;
using Levy.Common.Config;
using Levy.Common.Perf;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// World check of the active army size (RosterSystem.ActiveArmy.cs), on the ship world with real bodies: 30 soldiers of one commander (three Champions enlisted last), then a size of 20.
	/// The size holds through deployment; the Champions stay in the field; benching keeps every item and the live HP; a soldier who falls is replaced within one tick at his place (beside the commander under
	/// Regroup, near the commander after a hazard death); the fallen one waits out his own respawn timer and comes back with no body while the field is full; five falling in one tick are all replaced;
	/// raising and clearing the size fields the rest; a save and load round trip (records written and read back, bodies gone) neither loses nor doubles a soldier, an item or a body;
	/// beside the army making room for a boss (BossSlots) the size never refills the boss's room, and the field fills back to the size, best first, once the boss is gone.
	/// Plus the live notice (once a session, the words, Don't ask again) and the "?" text. Log prefix [ActiveArmy]. Runs in the world-check chain.
	/// </summary>
	public class ActiveArmyWorldCheck : ModSystem
	{
		internal static bool Running;
		private static bool _armed, _done; private static int _pass, _fail; private static Mod _mod;
		private const string Commander = "ActiveArmyTester";
		private const int FloorY = 300, Cx = 2600, Half = 150, N = 30, Cap = 20;
		private static Vector2 _cmdAt;
		private static readonly List<SoldierRecord> Recs = new();

		public override void OnWorldLoad()
		{
			if (!Main.dedServ) return;
			if (Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_ACTIVEARMYWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		public override void PostUpdateProjectiles() { if (Running && Main.player[0] != null) Main.player[0].active = true; }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; Running = true;
			bool sp = LevyUtil.SpEmulation; bool day = Main.dayTime; double time = Main.time; int renderCap = LevyConfig.Get?.RenderCap ?? 40;
			try { LevyUtil.SpEmulation = true; Main.player[0] = Main.player[0] ?? new Player(); Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + string.Join(" | ", (e.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim())), false); }
			finally
			{
				Running = false;
				try { Cleanup(); } catch (Exception) { }
				LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; Main.dayTime = day; Main.time = time;
				try { if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = renderCap; } catch (Exception) { }
			}
			_mod.Logger.Info($"[ActiveArmy] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[ActiveArmy] {(ok ? "PASS" : "FAIL")} {name}"); }

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = Commander;
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		private static void Step(int n) { for (int i = 0; i < n; i++) { KeepCommander(); WorldStep.Update(); } KeepCommander(); }

		private static void Arena()
		{
			for (int i = Cx - Half; i <= Cx + Half; i++)
			{
				for (int j = FloorY - 60; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				for (int j = FloorY; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && n.ModNPC is not SoldierNPC && Math.Abs(n.Center.X / 16f - Cx) < Half + 60) n.active = false; }
		}

		private static RosterSystem R => RosterSystem.Instance;
		private static int Fielded() => R.FieldedOf(Commander);
		private static IEnumerable<SoldierRecord> Mine() => R.Regulars.Where(r => r.CommanderName == Commander);
		private static int Waiting() => Mine().Count(r => !r.Dead && !r.Reserve && !r.HasBody);
		private static int Items(SoldierRecord r) => r.Gear.AllItems().Count(it => it != null && !it.IsAir);
		/// <summary>Soldier bodies in the world bound to one of this commander's records, and whether any record has two.</summary>
		private static int Bodies(out bool doubled)
		{
			var seen = new HashSet<int>(); doubled = false; int n = 0;
			for (int i = 0; i < Main.maxNPCs; i++) { NPC npc = Main.npc[i]; if (npc.active && npc.life > 0 && npc.ModNPC is SoldierNPC s && s.Record != null && s.Record.CommanderName == Commander) { n++; if (!seen.Add(s.Record.Id)) doubled = true; } }
			return n;
		}

		private static SoldierRecord Make(int k)
		{
			var rec = new SoldierRecord
			{
				Id = R.NextId++, Kind = SoldierKind.Regular, Grade = k >= N - 3 ? Grade.Champion : Grade.Levy, Temperament = Temperament.Sellsword, Male = k % 2 == 0, Name = "Pvt. Army" + k,
				CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = -1, StoodDown = false, Order = SoldierOrder.Hold,
			};
			rec.Gear.Weapon = new Item(); rec.Gear.Weapon.SetDefaults(ItemID.IronBroadsword);
			rec.Gear.Head = new Item(); rec.Gear.Head.SetDefaults(ItemID.IronHelmet); rec.Gear.Body = new Item(); rec.Gear.Body.SetDefaults(ItemID.IronChainmail); rec.Gear.Legs = new Item(); rec.Gear.Legs.SetDefaults(ItemID.IronGreaves);
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			R.Regulars.Add(rec); Recs.Add(rec);
			return rec;
		}

		private static SoldierNPC BodyOf(SoldierRecord r) => r != null && r.HasBody ? Main.npc[r.BodyIndex].ModNPC as SoldierNPC : null;

		private static void Run()
		{
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			Main.dayTime = true; Main.time = 20000;
			if (LevyConfig.Get != null) LevyConfig.Get.RenderCap = Math.Max(LevyConfig.Get.RenderCap, 100);
			KeepCommander();
			R.SetArmyCap(Commander, 0);

			// ---- 30 soldiers, all in the field with no size set
			for (int k = 0; k < N; k++) { var r = Make(k); R.SpawnBody(r, new Vector2((Cx - 60 + k * 4) * 16f, FloorY * 16f)); }
			Step(5);
			Check($"no active army size: all {N} in the field ({Fielded()})", Fielded() == N);
			int itemsBefore = Recs.Sum(Items);
			var hurt = Recs[0]; var hb = BodyOf(hurt); if (hb != null) hb.NPC.life = Math.Max(1, hb.NPC.lifeMax / 3);
			int hurtLife = hb?.NPC.life ?? -1;

			// ---- a size of 20: the 10 lowest-priority come off at once, the Champions (enlisted last) stay
			R.SetArmyCap(Commander, Cap);
			Check($"size {Cap}: {Cap} in the field at once ({Fielded()})", Fielded() == Cap);
			Check($"the other {N - Cap} wait alive, on duty, not in the reserve ({Waiting()})", Waiting() == N - Cap && Mine().All(r => !r.Reserve && !r.Dead));
			Check("the Champions stay in the field (best first)", Recs.Where(r => r.Grade == Grade.Champion).All(r => r.HasBody));
			Check("the benched are the last enlisted Levies", Recs.Where(r => r.Grade == Grade.Levy).Skip(Cap - 3).All(r => !r.HasBody) && Recs.Where(r => r.Grade == Grade.Levy).Take(Cap - 3).All(r => r.HasBody));
			Check("benching keeps every item on the records (" + itemsBefore + " -> " + Recs.Sum(Items) + ")", Recs.Sum(Items) == itemsBefore);
			Check("every soldier still holds his command points", Recs.All(r => r.CommandPointsHeld == r.CommandPointCost));
			Step(65); // two deployment passes
			Check($"deployment does not go over the size ({Fielded()} after 65 ticks)", Fielded() == Cap);
			int bodies = Bodies(out bool doubled);
			Check($"one body per soldier in the field ({bodies} bodies, doubled {doubled})", bodies == Cap && !doubled);
			Check("militia are never limited by the size", R.ArmyHasRoom(new SoldierRecord { Kind = SoldierKind.Militia, CommanderName = Commander }));

			// ---- one falls: the next waiting soldier takes his place within one tick, where he fell
			var victim = Recs.First(r => r.HasBody && r.Grade == Grade.Levy && r != hurt);
			var vb = BodyOf(victim); Vector2 spot = vb.NPC.Bottom;
			var waitingBefore = Recs.Where(r => !r.HasBody && !r.Dead).Select(r => r.Id).ToHashSet();
			int reps = RosterSystem.Replacements; uint t0 = Main.GameUpdateCount;
			vb.NPC.StrikeInstantKill();
			Check("the fallen soldier is dead with his own respawn timer", victim.Dead && !victim.HasBody && victim.RespawnTimer == LevyConstants.RegularRespawnTicks);
			Step(1);
			var rep = Recs.FirstOrDefault(r => waitingBefore.Contains(r.Id) && r.HasBody);
			Check($"a replacement within one tick ({RosterSystem.Replacements - reps} made, tick {RosterSystem.LastReplacementTick - t0} after the death)", RosterSystem.Replacements - reps == 1 && RosterSystem.LastReplacementTick - t0 <= 1 && Fielded() == Cap);
			Check("the replacement was a living soldier who was waiting, not the fallen one brought back", rep != null && rep != victim && victim.Dead);
			Check("the next in line came (the lowest id waiting)", rep != null && rep.Id == waitingBefore.Min());
			float d = rep != null && BodyOf(rep) != null ? Vector2.Distance(BodyOf(rep).NPC.Bottom, spot) / 16f : 999f;
			Check($"he appears where the fallen one stood ({d:0.0} tiles; fell at {spot.X / 16f:0},{spot.Y / 16f:0}, now at {(rep != null && BodyOf(rep) != null ? BodyOf(rep).NPC.Bottom.X / 16f : -1):0},{(rep != null && BodyOf(rep) != null ? BodyOf(rep).NPC.Bottom.Y / 16f : -1):0}, commander {_cmdAt.X / 16f:0})", d <= 2f);

			// ---- five fall in one tick: all five places filled
			var five = Recs.Where(r => r.HasBody && r.Grade == Grade.Levy && r != hurt).Take(5).ToList();
			reps = RosterSystem.Replacements;
			foreach (var r in five) BodyOf(r)?.NPC.StrikeInstantKill();
			Step(1);
			Check($"five fall at once: five replaced in one tick ({RosterSystem.Replacements - reps}), {Fielded()} in the field", RosterSystem.Replacements - reps == 5 && Fielded() == Cap);
			Check($"waiting now {Waiting()} (30 - 20 - 6 dead)", Waiting() == N - Cap - 6);

			// ---- a hazard death: not on the same spot, beside the commander instead
			var hz = Recs.First(r => r.HasBody && r.Grade == Grade.Levy && r != hurt);
			var hzb = BodyOf(hz); hzb.NPC.Bottom = new Vector2((Cx + 100) * 16f, FloorY * 16f); Vector2 hzSpot = hzb.NPC.Bottom;
			hzb.LastAttackerType = LevyConstants.EnvKillerLava; hzb.LastAttackerName = "lava";
			var waitingH = Recs.Where(r => !r.HasBody && !r.Dead).Select(r => r.Id).ToHashSet();
			hzb.NPC.StrikeInstantKill(); Step(1);
			var repH = Recs.FirstOrDefault(r => waitingH.Contains(r.Id) && r.HasBody);
			float dh = repH != null ? Vector2.Distance(BodyOf(repH).NPC.Bottom, hzSpot) / 16f : -1f, dc = repH != null ? Vector2.Distance(BodyOf(repH).NPC.Center, _cmdAt) / 16f : -1f;
			Check($"after a lava death the replacement comes in near the commander ({dc:0} tiles), not in the lava ({dh:0} tiles from it)", repH != null && dh > 20f && dc < LevyConstants.SpawnNearCommanderRadius + 4);

			// ---- Regroup: the replacement appears beside the commander
			foreach (var r in Mine()) r.Order = SoldierOrder.Regroup;
			var rg = Recs.First(r => r.HasBody && r.Grade == Grade.Levy && r != hurt);
			var rgb = BodyOf(rg); rgb.NPC.Bottom = new Vector2((Cx - 100) * 16f, FloorY * 16f);
			var waitingR = Recs.Where(r => !r.HasBody && !r.Dead).Select(r => r.Id).ToHashSet();
			rgb.NPC.StrikeInstantKill(); Step(1);
			var repR = Recs.FirstOrDefault(r => waitingR.Contains(r.Id) && r.HasBody);
			float dr = repR != null ? Vector2.Distance(BodyOf(repR).NPC.Center, _cmdAt) / 16f : -1f;
			Check($"under Regroup the replacement appears beside the commander ({dr:0.0} tiles)", repR != null && dr >= 0 && dr <= Movement_StandingRadius + 3);
			foreach (var r in Mine()) r.Order = SoldierOrder.Hold;

			// ---- the fallen come back alive but wait while the field is full; the rest of the field is untouched
			// (the respawn path: TickRespawns marks him alive and asks SpawnBody for a body at the muster point; the size refuses it while the field is full)
			int deadNow = Mine().Count(r => r.Dead), refused = 0;
			foreach (var r in Mine().ToList()) if (r.Dead) { r.Dead = false; r.RespawnTimer = 0; r.CurrentHp = r.MaxHp; if (R.SpawnBody(r, _cmdAt) < 0) refused++; }
			Step(LevyConstants.DeployIntervalTicks + 2);
			Check($"the respawned ({deadNow}) come back with no body while the field is full ({refused} refused, {Fielded()} in the field, {Waiting()} waiting)", refused == deadNow && Mine().All(r => !r.Dead) && Fielded() == Cap && Waiting() == N - Cap);

			// ---- the hurt soldier: benched and fielded again with the same HP (no free heal)
			hurtLife = BodyOf(hurt)?.NPC.life ?? -1;
			R.SetArmyCap(Commander, 1);
			bool hurtBenched = !hurt.HasBody;
			int hpOnRecord = hurt.CurrentHp;
			R.SetArmyCap(Commander, Cap);
			Check($"a soldier benched mid-fight keeps his HP on the record ({hurtLife} -> {hpOnRecord})", (hurtBenched && hpOnRecord == hurtLife) || (!hurtBenched && BodyOf(hurt)?.NPC.life == hurtLife));
			Check($"size 1 then {Cap}: back to {Cap} at once ({Fielded()})", Fielded() == Cap);

			// ---- raising and clearing the size
			R.SetArmyCap(Commander, 25);
			Check($"size 25: five more fielded at once ({Fielded()})", Fielded() == 25);
			R.SetArmyCap(Commander, 0);
			Step(LevyConstants.DeployIntervalTicks + 2);
			Check($"no limit: every soldier is fielded by the next deployment ({Fielded()})", Fielded() == N);
			Check("still every item (" + Recs.Sum(Items) + ")", Recs.Sum(Items) == itemsBefore);

			// ---- save and load: nothing lost, nothing doubled
			R.SetArmyCap(Commander, Cap);
			var dead1 = Recs.First(r => r.HasBody && r.Grade == Grade.Levy && r != hurt); BodyOf(dead1).NPC.StrikeInstantKill(); Step(1);
			Check("SoldierNPC bodies are never written to the world file", ModContent.GetInstance<SoldierNPC>() is SoldierNPC sn0 && !sn0.NeedSaving());
			var saved = Mine().Select(r => (Id: r.Id, Tag: r.Save(), r.Dead, r.RespawnTimer)).ToList();
			int savedItems = Mine().Sum(Items), savedCount = saved.Count;
			// the world closes: every body goes with it, the records are read back from what was written
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.Record != null && s.Record.CommanderName == Commander) n.active = false; }
			R.Regulars.RemoveAll(r => r.CommanderName == Commander); Recs.Clear();
			foreach (var s in saved) { var r = SoldierRecord.Load(s.Tag); r.Dead = s.Dead; r.RespawnTimer = s.RespawnTimer; if (r.Dead) r.CurrentHp = 0; r.BodyIndex = -1; R.Regulars.Add(r); Recs.Add(r); }
			R.SetArmyCap(Commander, Cap); // the player's client sends its size on entering the world
			Step(LevyConstants.DeployIntervalTicks + 2);
			int b2 = Bodies(out bool dbl2);
			Check($"after a reload: {savedCount} records back, ids unique, {Fielded()} in the field, one dead", Mine().Count() == savedCount && Mine().Select(r => r.Id).Distinct().Count() == savedCount && Fielded() == Cap && Mine().Count(r => r.Dead) == 1);
			Check($"after a reload: one body per fielded soldier ({b2}, doubled {dbl2}) and every item ({savedItems} -> {Mine().Sum(Items)})", b2 == Cap && !dbl2 && Mine().Sum(Items) == savedItems);
			// a second save of the same state reads back the same
			var again = Mine().Select(r => SoldierRecord.Load(r.Save())).ToList();
			Check("a second save reads back the same soldiers and items", again.Count == savedCount && again.Sum(Items) == savedItems && again.Select(r => r.Id).OrderBy(i => i).SequenceEqual(saved.Select(s => s.Id).OrderBy(i => i)));
			R.SetArmyCap(Commander, 0);

			// ---- the army makes room for a boss while a size is set
			BossRoomChecks();

			// ---- the live notice and the "?" text
			NoticeChecks();
		}

		/// <summary>
		/// The active army size beside the army making room for a boss (Budget/BossSlots.cs): the NPC array is filled to the last slot, a boss and its adds spawn into it, and BossSlots takes levy bodies off the field for them.
		/// While the boss lives: the size never brings a soldier back into the room the boss holds (not by the deployment, not by a replacement for a fallen soldier, not by raising the size), and nothing is over the size.
		/// When the boss is gone: the field fills back to the size exactly, best first (the size's own order), one body per soldier, every item still on the records.
		/// </summary>
		private static void BossRoomChecks()
		{
			if (!BossSlots.On) { _mod.Logger.Info("[ActiveArmy] OBSERVED boss room checks skipped (LEVY_BOSSSLOTS=0)"); return; }
			const int K = 5;
			var made = new List<int>();
			NPC Inert(int i, Vector2 at)
			{
				NPC n = Main.npc[i];
				n.aiStyle = -1; n.dontTakeDamage = true; n.noGravity = true; n.noTileCollide = true; n.damage = 0; n.friendly = false; n.timeLeft = 1_000_000;
				n.Center = at; n.velocity = Vector2.Zero; made.Add(i);
				return n;
			}
			int before = BossSlots.FreedLevies;
			try
			{
				R.SetArmyCap(Commander, Cap);
				Step(2);
				Check($"boss room: the field starts at the size ({Fielded()})", Fielded() == Cap);
				int itemsBefore = Mine().Sum(Items);
				// nothing in the world that the room-making takes before a levy: vanilla critters and hostiles go, then the array is filled with inert filler (out of reach, it cannot be hurt, so the room-making leaves it)
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.townNPC && n.ModNPC == null) n.active = false; }
				Vector2 far = new Vector2((Cx + 150) * 16f, (FloorY - 50) * 16f); // inside the active range of the commander (farther off, the game despawns it)
				for (int g = 0; g < Main.maxNPCs; g++)
				{
					int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_WorldEvent("ActiveArmyFiller"), (int)far.X, (int)far.Y, NPCID.BlueSlime);
					if (i < 0 || i >= Main.maxNPCs) break;
					Inert(i, far + new Vector2(g % 20 * 16f, g / 20 * 16f));
				}
				int fieldedFull = Fielded();
				// the boss spawns into the full array, then its adds: each one that finds no slot takes the farthest levy body off the field
				Vector2 bossAt = new Vector2((Cx + 140) * 16f, (FloorY - 30) * 16f);
				int bi = NPC.NewNPC(new Terraria.DataStructures.EntitySource_BossSpawn(Main.player[0]), (int)bossAt.X, (int)bossAt.Y, NPCID.BlueSlime);
				NPC boss = bi >= 0 && bi < Main.maxNPCs ? Inert(bi, bossAt) : null;
				if (boss != null) { boss.boss = true; boss.lifeMax = boss.life = 50000; }
				for (int g = 0; g < 60 && boss != null && fieldedFull - Fielded() < K; g++)
				{
					int i = NPC.NewNPC(new Terraria.DataStructures.EntitySource_Parent(boss), (int)bossAt.X, (int)bossAt.Y, NPCID.BlueSlime);
					if (i < 0 || i >= Main.maxNPCs) break;
					Inert(i, bossAt + new Vector2(0, -16f * (g + 1)));
				}
				int squeezed = fieldedFull - Fielded(), free = CrowdBudget.LiveFreeNpcs();
				_mod.Logger.Info($"[ActiveArmy] OBSERVED boss room: field {fieldedFull} before, {Fielded()} after; levies taken off by the room-making {BossSlots.FreedLevies - before}; squeeze {BossSlots.Squeeze}, extra reserve {BossSlots.ExtraReserve}, free NPC slots {free}; boss {(boss != null ? "spawned" : "FAILED")}");
				Check($"boss room: the boss spawns into a full array and the room-making takes {K} of the field ({squeezed})", boss != null && squeezed == K && BossSlots.Squeeze >= K);
				Check($"boss room: the soldiers taken off wait alive on duty, with their kit ({Waiting()} waiting, items {itemsBefore} -> {Mine().Sum(Items)})", Mine().All(r => !r.Reserve) && Mine().Sum(Items) == itemsBefore && Waiting() == N - 1 - (Cap - K));
				for (int q = 0; q < 4; q++) { Step(16); _mod.Logger.Info($"[ActiveArmy] OBSERVED boss room t{(q + 1) * 16}: fielded {Fielded()}, made alive {made.Count(i => Main.npc[i].active)}/{made.Count}, boss {(boss != null && boss.active && boss.boss)}, free {CrowdBudget.LiveFreeNpcs()}, squeeze {BossSlots.Squeeze}, extra {BossSlots.ExtraReserve}, bossAlive {BossSlots.BossAlive}"); }
				Check($"boss room: the deployment leaves the boss's room alone ({Fielded()} in the field after two passes, size {Cap})", Fielded() == Cap - K);
				// a soldier falls while the boss holds the room: no replacement takes the slot he leaves
				int reps = RosterSystem.Replacements;
				var v = Mine().First(r => r.HasBody && r.Grade == Grade.Levy);
				BodyOf(v).NPC.StrikeInstantKill(); Step(2);
				Check($"boss room: a soldier who falls is not replaced into the boss's room ({RosterSystem.Replacements - reps} replaced, {Fielded()} in the field)", RosterSystem.Replacements == reps && Fielded() == Cap - K - 1);
				// raising the size mid-boss does not either
				R.SetArmyCap(Commander, Cap + 5); Step(1);
				Check($"boss room: raising the size mid-boss fields nobody into the boss's room ({Fielded()})", Fielded() == Cap - K - 1);
				R.SetArmyCap(Commander, Cap); Step(1);
				int bodies = Bodies(out bool dbl);
				Check($"boss room: never over the size, one body per soldier ({bodies} bodies, doubled {dbl})", bodies == Fielded() && bodies <= Cap && !dbl);
				// the boss is gone: the field fills back to the size, in the size's order
				var expectBack = Mine().Where(r => !r.Dead && !r.Reserve && !r.HasBody).ToList();
				expectBack.Sort(RosterSystem.CompareDeploy);
				expectBack = expectBack.Take(Cap - Fielded()).ToList();
				foreach (int i in made) if (Main.npc[i].active) Main.npc[i].active = false;
				made.Clear();
				Step(30 + LevyConstants.DeployIntervalTicks + 2);
				int b2 = Bodies(out bool dbl2);
				Check($"boss room: after the boss the room-making stands down (squeeze {BossSlots.Squeeze}, extra reserve {BossSlots.ExtraReserve})", BossSlots.Squeeze == 0 && BossSlots.ExtraReserve == 0);
				Check($"boss room: after the boss the field fills back to the size exactly ({Fielded()}, {b2} bodies, doubled {dbl2})", Fielded() == Cap && b2 == Cap && !dbl2);
				Check($"boss room: the soldiers who come back are the next in the size's order ({expectBack.Count(r => r.HasBody)}/{expectBack.Count})", expectBack.Count > 0 && expectBack.All(r => r.HasBody));
				Check($"boss room: every item still on the records ({itemsBefore} -> {Mine().Sum(Items)})", Mine().Sum(Items) == itemsBefore);
			}
			finally
			{
				foreach (int i in made) if (Main.npc[i].active) Main.npc[i].active = false;
				BossSlots.Reset();
				R.SetArmyCap(Commander, 0);
			}
		}

		private static float Movement_StandingRadius => LevyConstants.Movement.StandingSpotRadiusTiles;

		private static void NoticeChecks()
		{
			bool tm = PerfPopup.TestMode; bool? dont = PerfPopup.TestArmyDontAsk; var popupState = PerfPopup.TestState; // the popup's clock is put back: the analyzer check after this one rate-limits on it
			try
			{
				PerfPopup.TestMode = true; PerfPopup.ArmyNoticeShown = false; PerfPopup.TestArmyDontAsk = false;
				var w = PerfAnalysis.Synth(8, 4, 58, 0.45, "CalamityMod", 0.1); w.UpdMs = 15.5; w.Levies = 45; w.Mine = 45; w.FrameSkip = 1; w.ArmyRefMs = new[] { 6.6, 3.6, 0, 0 };
				var v = PerfPopup.CheckLive(w, 45);
				int expect = PerfAnalysis.SuggestArmyCap(15.5, 10.2, 45, 45);
				Check($"live notice: a window at 15.5 ms with the army 10.2 of it shows the notice at once (suggests {v?.SuggestedArmy}, expected {expect})", v != null && PerfPopup.Current == v && v.SuggestedArmy == expect && PerfPopup.ArmyStep == expect);
				Check("live notice: the words", v != null && v.Detail[0] == "Your army is more than this computer can run smoothly (about " + expect + " soldiers at once). Set an active army size?");
				Check("live notice: once a session", PerfPopup.CheckLive(w, 45) == null);
				PerfPopup.ArmyNoticeShown = false; PerfPopup.Hide();
				var vf = PerfPopup.CheckLive(w, 45);
				Check("live notice: the note offers a fourth button, It's fine", vf != null && vf.Options.Contains(PopupOption.ItsFine) && vf.Options.Contains(PopupOption.ActiveArmy) && vf.Options.Contains(PopupOption.OpenSettings) && vf.Options.Contains(PopupOption.DontAskAgain));
				PerfPopup.Act(PopupOption.ItsFine); PerfPopup.ArmyNoticeShown = false;
				Check("live notice: It's fine keeps the note away for the rest of the session, without saving anything", PerfPopup.SessionQuiet && PerfPopup.CheckLive(w, 45) == null && PerfPopup.TestArmyDontAsk == false);
				int shown0 = PerfPopup.Shown;
				PerfPopup.Offer(new LagVerdict { Kind = LagKind.OtherMod, Laggy = true, Options = new System.Collections.Generic.List<PopupOption> { PopupOption.ItsFine } });
				Check("live notice: and the after-fight note stays away too", PerfPopup.Shown == shown0);
				PerfPopup.SessionQuiet = false; PerfPopup.ArmyNoticeShown = false; PerfPopup.Hide();
				Check("live notice: a new session starts without it", PerfPopup.CheckLive(w, 45) != null);
				PerfPopup.Act(PopupOption.DontAskAgain);
				Check("live notice: Don't ask again silences the army note, not the lag note", PerfPopup.TestArmyDontAsk == true);
				PerfPopup.ArmyNoticeShown = false;
				Check("live notice: and it stays silent after that", PerfPopup.CheckLive(w, 45) == null);
				var before = PerfPopup.TestState; PerfPopup.TestState = popupState; var after = PerfPopup.TestState;
				Check("the popup's clock and counters are given back for the next check", after.last == popupState.last && after.snooze == popupState.snooze && after.shown == popupState.shown && after.notice == popupState.notice);
				PerfPopup.TestState = before;
				PerfPopup.Hide();
			}
			finally { PerfPopup.Hide(); PerfPopup.TestMode = tm; PerfPopup.TestArmyDontAsk = dont; PerfPopup.TestState = popupState; }
			string help = LevyUtil.T("UI2.ActiveArmyHelp");
			Check("the \"?\" says what it does (" + help + ")", help == "Set how many soldiers fight at once. When one falls, the next in reserve takes their place right away. Fewer soldiers means less lag and less firepower. Below about 30, the army can be overwhelmed.");
			Check("the settings have a label and the waiting state has words", LevyUtil.T("UI2.WaitingInReserve") == "Waiting in reserve" && LevyUtil.T("Roster.ActiveArmySet", 20).Contains("20") && !LevyUtil.T("UI2.ActiveArmyAll").StartsWith("Mods."));
		}

		private static void Cleanup()
		{
			R?.SetArmyCap(Commander, 0);
			for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && n.ModNPC is SoldierNPC s && s.Record != null && s.Record.CommanderName == Commander) n.active = false; }
			R?.Regulars.RemoveAll(r => r.CommanderName == Commander);
			Recs.Clear();
			for (int i = 0; i < Main.maxProjectiles; i++) { var p = Main.projectile[i]; if (p.active && Math.Abs(p.Center.X / 16f - Cx) < Half + 60) p.active = false; }
		}
	}
}
