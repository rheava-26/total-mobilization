using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Budget;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class BossBugsWorldCheck
	{
		// =====================================================================================================
		// SUMMON (Calamity half, Master + Death): a segmented boss summoned the way its item does (Calamity's SpawnBossOnPosUsingItem: NPC.NewNPC with an EntitySource_BossSpawn 100 tiles over the commander,
		// Start 1, timeLeft x20) with a standing army beside the commander. Eve, 2026-11-20: "Can't summon DoG with 40 levies" (DoG woke and was gone at once).
		// OBSERVED cause: a worm spawns every segment in one pass of its own AI (DoG 102 NPCs, Storm Weaver 61). With fewer free NPC slots than that NPC.NewNPC returns 200, the dummy slot, and the boss's AI
		// throws on it (DevourerofGodsHead.AI line 1515, NullReferenceException) so the boss is gone on its first tick. The army's reserve keeps only 40 slots free and its garrison drones fill the rest.
		// The fix is BossSlots (Levy/Common/Budget): a failed boss spawn frees slots from what matters least (critters, stray hostiles, garrison drones, summoned units, last the levies the boss's room needs).
		// Arms: none (nobody), army (40 levies, the boss beaten before), armed (the same, armed), unbeaten (first kill), crowd (40 levies, 8 townspeople, 2 field drones and 2 turrets, zombies fill the world
		// until only 40 NPC slots are free: the army's own reserve), lean (96 levies, 4 townspeople: 100 slots used, the levies themselves must make room), full (the world full to the last slot).
		// Every tick for the first 5 s: target, lock, ai[], what the target slot holds, how it left. Then it must stay in the fight for 11 s.
		// LEVY_SUMMON_BOSS (substring of the worm names), LEVY_SUMMON_ARMS (arm list), LEVY_SUMMON_N (levies), LEVY_SUMMON_TRACE=1 (the per-tick log), LEVY_BOSSSLOTS=0 (the fix off: A/B) pick a lab run.
		// =====================================================================================================
		private const int SummonWatch = 780, SummonMustLast = 660;

		private sealed class SummonTrace
		{
			public int Gone = -1, LockedTicks, PlayerTicks, DespawnState, SegMin = int.MaxValue, SegMax, TargetOutOfRange, DeadTargetSeen, EncourageCalls, CheckActiveCalls;
			public string Why = "", LastAi = "", DespawnStack = "";
			public readonly StringBuilder Log = new();
			public int FreeBefore, FreeAfter, TownBefore, TownAfter, MachinesBefore, MachinesAfter, LeviesBefore, LeviesAfter, SqueezeAtEnd, SqueezeSoon, FreedTotal, FreedHostile; public string Comp = "";
		}

		private static void SummonPart()
		{
			if (!ModLoader.HasMod("CalamityMod")) { Note("(Summon) Calamity is not loaded: the summon part is skipped"); return; }
			var bosses = new List<(string name, string type)> { ("Devourer of Gods", "DevourerofGodsHead"), ("Storm Weaver", "StormWeaverHead"), ("Desert Scourge", "DesertScourgeHead") };
			string only = Environment.GetEnvironmentVariable("LEVY_SUMMON_BOSS");
			string[] arms = (Environment.GetEnvironmentVariable("LEVY_SUMMON_ARMS") ?? "none,army,armed,unbeaten,crowd,lean,full").Split(',');
			int n = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMON_N"), out int nn) ? nn : 40;
			foreach (var (name, tn) in bosses)
			{
				if (only != null && !name.Contains(only)) continue;
				int type = CalType(tn);
				if (type <= 0) { Check($"(Summon) {name}: the type resolves", false); continue; }
				int baseSegs = -1;
				foreach (string arm in arms)
				{
					SummonTrace t = SummonTrial(type, arm, n);
					string label = arm switch
					{
						"none" => "no levies", "army" => $"{n} levies around the commander, boss beaten before", "armed" => $"{n} armed levies around the commander, boss beaten before",
						"unbeaten" => $"{n} levies around the commander, first kill", "off" => $"{n} levies, enemies-target-soldiers off",
						"crowd" => $"{n} levies, townspeople, machines and zombies: only 40 NPC slots free", "lean" => "96 levies and 4 townspeople: the levies must make room",
						"full" => $"{n} levies, the world full to the last NPC slot", _ => arm
					};
					if (arm == "none") baseSegs = t.SegMin == int.MaxValue ? 0 : t.SegMin;
					Note($"{name} [{label}]: {(t.Gone >= 0 ? $"GONE {t.Gone} ticks ({t.Gone / 60f:0.0} s) after the summon ({t.Why})" : $"still there after {SummonWatch / 60} s")}; locked on a levy {t.LockedTicks}, on a player {t.PlayerTicks}, ai[3]==3 (leaving) {t.DespawnState} ticks, segments {t.SegMin}..{t.SegMax}, target index out of 0..254 {t.TargetOutOfRange}, target dead/inactive at the AI call {t.DeadTargetSeen}; EncourageDespawn {t.EncourageCalls}, CheckActive {t.CheckActiveCalls}; the world before the summon: {t.Comp}; free NPC slots {t.FreeBefore} before the summon, {t.FreeAfter} after; townspeople {t.TownBefore}->{t.TownAfter}, machines {t.MachinesBefore}->{t.MachinesAfter}, levies {t.LeviesBefore}->{t.LeviesAfter}, squeeze at the end {t.SqueezeAtEnd}; BossSlots freed {BossSlots.Freed} (critters {BossSlots.FreedCritters}, hostiles {BossSlots.FreedHostiles}, garrison {BossSlots.FreedReleased}, summoned {BossSlots.FreedSummons}, levies {BossSlots.FreedLevies})");
					if (Environment.GetEnvironmentVariable("LEVY_SUMMON_TRACE") == "1") foreach (string l in t.Log.ToString().Split('\n')) if (l.Length > 0) Note("  " + l);
					Check($"(Summon) {name} [{label}]: stays in the fight for {SummonMustLast / 60} s after the summon", t.Gone < 0 || t.Gone >= SummonMustLast, () => $"gone at tick {t.Gone}; {t.Why}; free slots before {t.FreeBefore}; " + t.Log.ToString().Replace('\n', '|'));
					Check($"(Summon) {name} [{label}]: its AI never reads a dead or missing target", t.DeadTargetSeen == 0 && t.DespawnState == 0, () => $"dead target at {t.DeadTargetSeen} AI calls, leaving state {t.DespawnState} ticks");
					if (arm != "none" && baseSegs > 0) Check($"(Summon) {name} [{label}]: every segment spawned ({t.SegMin} of {baseSegs})", t.SegMin >= baseSegs, () => $"segments {t.SegMin}..{t.SegMax}");
					if (arm == "crowd" || arm == "full")
					{
						if (BossSlots.On && t.FreeBefore < baseSegs + 1) Check($"(Summon) {name} [{label}]: the world had {t.FreeBefore} slots free for {baseSegs + 1}: the room came from the zombies and critters ({t.FreedTotal} freed, {t.FreedHostile} hostiles)", t.FreedTotal >= baseSegs + 1 - t.FreeBefore - 2, () => t.Comp);
						Check($"(Summon) {name} [{label}]: the townspeople are never taken ({t.TownBefore} -> {t.TownAfter})", t.TownAfter == t.TownBefore);
						Check($"(Summon) {name} [{label}]: the player's drones and turrets are never taken ({t.MachinesBefore} -> {t.MachinesAfter})", t.MachinesAfter == t.MachinesBefore);
						Check($"(Summon) {name} [{label}]: not one levy left the field ({t.LeviesBefore} -> {t.LeviesAfter})", t.LeviesAfter == t.LeviesBefore, () => $"squeeze {t.SqueezeAtEnd}");
					}
					if (arm == "lean")
					{
						bool needs = t.FreeBefore < baseSegs + 1; // the boss needs more room than the world has left over the levies
						Check($"(Summon) {name} [{label}]: the levies stand down only as many as the room took ({t.LeviesBefore} -> {t.LeviesAfter}, {(needs ? "it needs " + (baseSegs + 1 - t.FreeBefore) + " more" : "it fits")})", needs ? (t.LeviesAfter < t.LeviesBefore && t.LeviesAfter >= t.LeviesBefore - (baseSegs + 1 - t.FreeBefore) - 3) : t.LeviesAfter == t.LeviesBefore, () => $"squeeze {t.SqueezeSoon}");
						Check($"(Summon) {name} [{label}]: the townspeople are never taken ({t.TownBefore} -> {t.TownAfter})", t.TownAfter == t.TownBefore);
					}
				}
			}
			// the room is given back: with no boss left the squeeze and the drones' extra reserve are gone
			Check("(Summon) with the boss gone the squeeze and the extra reserve are given back", BossSlotsClearedAfterBoss());
		}

		private static bool BossSlotsClearedAfterBoss()
		{
			ClearHostiles();
			for (int tick = 0; tick < 90; tick++) Step(1);
			return BossSlots.Squeeze == 0 && BossSlots.ExtraReserve == 0;
		}

		private static readonly List<int> _fillers = new();
		private static int FreeNpcSlots() { int f = 0; for (int i = 0; i < Main.maxNPCs; i++) if (!Main.npc[i].active) f++; return f; }
		private static int CountNpcs(Func<NPC, bool> p) { int c = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && p(Main.npc[i])) c++; return c; }

		private static int Filler(int type, Vector2 at, bool immortal)
		{
			int bi = NPC.NewNPC(new EntitySource_WorldEvent("SummonFill"), (int)at.X, (int)at.Y, type);
			if (bi < 0 || bi >= Main.maxNPCs) return -1;
			NPC f = Main.npc[bi]; f.timeLeft = 99999; f.netOffset = Vector2.Zero; f.immortal = immortal;
			_fillers.Add(bi);
			return bi;
		}

		private static SummonTrace SummonTrial(int type, string arm, int n)
		{
			ClearHostiles();
			for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && x.ModNPC is SoldierNPC) x.active = false; }
			global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			_cmdDead = false;
			var cfg = global::Levy.Common.Config.LevyConfig.Get;
			bool savedEnemies = cfg?.EnemiesTargetSoldiers ?? true; int savedCap = cfg?.RenderCap ?? 40;
			var t = new SummonTrace();
			BossSlots.Failed = BossSlots.Freed = BossSlots.FreedCritters = BossSlots.FreedHostiles = BossSlots.FreedReleased = BossSlots.FreedSummons = BossSlots.FreedLevies = BossSlots.Unanswered = 0; BossSlots.Reset();
			On_NPC.hook_EncourageDespawn encDet = (On_NPC.orig_EncourageDespawn orig, NPC self, int time) => { if (self.type == type) { t.EncourageCalls++; if (t.DespawnStack == "") t.DespawnStack = "EncourageDespawn from " + Short(Environment.StackTrace); } orig(self, time); };
			On_NPC.hook_CheckActive actDet = (On_NPC.orig_CheckActive orig, NPC self) => { if (self.type == type) t.CheckActiveCalls++; orig(self); };
			On_NPC.EncourageDespawn += encDet; On_NPC.CheckActive += actDet;
			try
			{
				Arena(FloorY, Half);
				SetRevenge(true, true); Main.GameMode = GameModeID.Master; Main.hardMode = true;
				PlaceCommander(0);
				bool beaten = arm != "unbeaten";
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, beaten);
				SetBossesTarget(beaten);
				if (cfg != null && arm == "off") cfg.EnemiesTargetSoldiers = false;
				EnemyTargeting.RefreshConfig();
				int levies = arm == "none" ? 0 : arm == "lean" ? 96 : n;
				if (cfg != null && levies > cfg.RenderCap) cfg.RenderCap = levies;
				var army = new List<SoldierNPC>();
				for (int k = 0; k < levies; k++)
				{
					var s = Levy(new Vector2((Cx + (k - levies / 2) * 0.7f) * 16f, _floorY * 16f));
					if (s == null) continue;
					s.NPC.immortal = true;
					if (arm == "armed" && s.Record != null) { var gun = new Item(); gun.SetDefaults(ItemID.Minishark); var ball = new Item(); ball.SetDefaults(ItemID.MusketBall); ball.stack = 999; s.Record.Gear.Weapon = gun; s.Record.Gear.Ammo = ball; }
					army.Add(s);
				}
				Step(2);
				// the world around the army: what a real world holds
				Vector2 at = _cmdAt + new Vector2(40 * 16f, -3 * 16f);
				int towns = arm == "crowd" || arm == "full" ? 8 : arm == "lean" ? 4 : 0;
				for (int k = 0; k < towns; k++) Filler(NPCID.Guide, at, true);
				if (arm == "crowd" || arm == "full") Machines(_cmdAt, 2, 2);
				int want = arm == "crowd" ? 40 : arm == "lean" ? -1 : arm == "full" ? 0 : -1;
				if (want >= 0)
				{
					// crowd: the world's hostiles fill it (zombies hold their slots; a critter can leave on its own); full: critters fill it to the last slot
					for (int k = 0; k < 30 && arm == "full"; k++) Filler(NPCID.Zombie, at, false);
					for (int guard = 0; guard < 400 && FreeNpcSlots() > want; guard++) Filler(arm == "crowd" ? NPCID.Zombie : NPCID.Bunny, at, false);
				}
				ClearAggroOfFillers();
				t.Comp = $"levies {CountNpcs(x => x.ModNPC is SoldierNPC)}, town {CountNpcs(x => x.townNPC)}, machines {CountNpcs(x => EnemyTargeting.IsMachineBody(x))}, bunnies {CountNpcs(x => x.type == NPCID.Bunny)}, zombies {CountNpcs(x => x.type == NPCID.Zombie)}";
				t.FreeBefore = FreeNpcSlots(); t.TownBefore = CountNpcs(x => x.townNPC); t.MachinesBefore = CountNpcs(x => EnemyTargeting.IsMachineBody(x)); t.LeviesBefore = CountNpcs(x => x.ModNPC is SoldierNPC);
				// the item's own spawn: NPC.NewNPC(EntitySource_BossSpawn(player), x, y - 1600, type, Start: 1), then timeLeft *= 20
				int idx = NPC.NewNPC(new EntitySource_BossSpawn(Main.player[0]), (int)_cmdAt.X, (int)_cmdAt.Y - 1600, type, 1);
				if (idx < 0 || idx >= Main.maxNPCs || Main.npc[idx].type != type) { t.Why = "did not spawn (no free NPC slot for its head)"; t.Gone = 0; return t; }
				NPC head = Main.npc[idx]; head.timeLeft *= 20;
				int who = idx;
				bool Alive() => Main.npc[who].active && Main.npc[who].type == type;
				EnemyTargeting.AiProbe = b =>
				{
					if (b.whoAmI != who) return;
					int tg = b.target; Player pl = tg >= 0 && tg <= Main.maxPlayers ? Main.player[tg] : null;
					if (tg < 0 || tg >= Main.maxPlayers) t.TargetOutOfRange++;
					if (pl == null || !pl.active || pl.dead) t.DeadTargetSeen++;
				};
				for (int tick = 0; tick < SummonWatch; tick++)
				{
					foreach (int fi in _fillers) { if (Main.npc[fi].active && (Main.npc[fi].type == NPCID.Bunny || Main.npc[fi].type == NPCID.Zombie || Main.npc[fi].type == NPCID.Guide)) Main.npc[fi].timeLeft = 99999; }
					Step(1);
					if (!Alive())
					{
						t.Gone = tick; NPC d = Main.npc[who];
						t.Why = $"slot now {(d.active ? "type " + d.type : "inactive")}; last ai [{t.LastAi}]";
						break;
					}
					NPC b = Main.npc[who];
					if (tick == 3) { t.TownAfter = CountNpcs(x => x.townNPC); t.MachinesAfter = CountNpcs(x => EnemyTargeting.IsMachineBody(x)); t.LeviesAfter = CountNpcs(x => x.ModNPC is SoldierNPC); t.SqueezeSoon = BossSlots.Squeeze; }
					int segs = 0; for (int i = 0; i < Main.maxNPCs; i++) { NPC s = Main.npc[i]; if (s.active && s.realLife == who && i != who) segs++; }
					t.SegMin = Math.Min(t.SegMin, segs); t.SegMax = Math.Max(t.SegMax, segs);
					if (Locked(b)) t.LockedTicks++; else if (b.target >= 0 && b.target < Main.maxPlayers && b.target != SoldierNPC.DummySlot) t.PlayerTicks++;
					if (b.ai[3] == 3f) t.DespawnState++;
					t.LastAi = $"{b.ai[0]:0.#},{b.ai[1]:0.#},{b.ai[2]:0.#},{b.ai[3]:0.#}";
					if (tick < 12 || tick % 60 == 0 || b.ai[3] == 3f && t.DespawnState <= 3)
						t.Log.AppendLine($"t{tick}: target {b.target} lock {LevyNpcData.Of(b).SoldierTarget} why \"{EnemyTargeting.WhyLast}\" ai[{t.LastAi}] life {b.life}/{b.lifeMax} timeLeft {b.timeLeft} dep {b.despawnEncouraged} segs {segs} free {FreeNpcSlots()} pos ({b.Center.X / 16f:0},{b.Center.Y / 16f:0}) vs cmd ({_cmdAt.X / 16f:0},{_cmdAt.Y / 16f:0}) op {b.Opacity:0.##}");
				}
				if (t.SegMin == int.MaxValue) t.SegMin = 0;
				t.FreeAfter = FreeNpcSlots();
				t.SqueezeAtEnd = BossSlots.Squeeze; t.FreedTotal = BossSlots.Freed; t.FreedHostile = BossSlots.FreedHostiles;
				return t;
			}
			finally
			{
				On_NPC.EncourageDespawn -= encDet; On_NPC.CheckActive -= actDet;
				EnemyTargeting.AiProbe = null;
				if (cfg != null) { cfg.EnemiesTargetSoldiers = savedEnemies; cfg.RenderCap = savedCap; }
				EnemyTargeting.RefreshConfig();
				SetBossesTarget(null);
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(type, false);
				ClearHostiles(); ClearMachines();
				foreach (int fi in _fillers) if (Main.npc[fi].active && (Main.npc[fi].type == NPCID.Bunny || Main.npc[fi].type == NPCID.Zombie || Main.npc[fi].type == NPCID.Guide)) Main.npc[fi].active = false;
				_fillers.Clear();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC x = Main.npc[i]; if (x.active && x.ModNPC is SoldierNPC) x.active = false; }
				global::Levy.Common.Systems.RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
			}
		}

		/// <summary>The zombies fill slots, they are not an enemy of this check: nothing to do but keep them alive and put (the levies are immortal).</summary>
		private static void ClearAggroOfFillers() { foreach (int fi in _fillers) if (Main.npc[fi].active && Main.npc[fi].type == NPCID.Zombie) Main.npc[fi].lifeMax = Main.npc[fi].life = 400; }

		private static string Short(string stack)
		{
			var sb = new StringBuilder(); int k = 0;
			foreach (string line in stack.Split('\n'))
			{
				string l = line.Trim(); if (l.StartsWith("at System.Environment") || l.Contains("MonoMod") || l.Contains("DMD<") && k > 6) continue;
				sb.Append(l.Replace("at ", "")).Append(" < "); if (++k >= 6) break;
			}
			return sb.ToString();
		}
	}
}
