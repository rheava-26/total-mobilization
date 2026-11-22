using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// "[RecMulti]" world checks of THE ENCOUNTER RULE (FightRecorder._fightSerials) on the headless (harness) path: the real automatic start (FightRecAuto.HeadlessUpdate), sampling and stop, the game clock stepped by hand, bosses spawned with the
	/// entity sources the games use (a summon item's NPC.SpawnOnPlayer, a boss's own AI, a projectile a boss makes), so the spawn serials come from the real spawn hooks.
	/// One fight = from the first boss until no boss of the fight stands (a boss that appears while one stands joins it), then the short grace; a boss that appears once the fight's bosses are gone is the next fight.
	/// Run from <see cref="FightRecAutoCheck"/> (LEVY_RECMULTI=0 skips it). Restores every global it touches.
	/// </summary>
	internal static class FightRecMultiCheck
	{
		private static int _pass, _fail;
		private static Mod _mod;
		private static void Chk(bool ok, string name) { if (ok) { _pass++; _mod.Logger.Info("[RecMulti] PASS " + name); } else { _fail++; _mod.Logger.Warn("[RecMulti] FAIL " + name); } }
		private const int Floor = FightRecAutoCheck.Floor;
		private static Player _p;
		private static int _cx;

		/// <summary>Boss: the header's fight name as the file name spells it (spaces are underscores).</summary>
		private sealed class Rec { public string Path, Boss, Reason; public int Ticks, Trial; public readonly List<(string e, string name, int fin)> Ends = new(); public readonly HashSet<string> Bosses = new(); public string Err; }

		private static Rec Read(string path)
		{
			var r = new Rec { Path = path };
			var lines = FightRecAutoCheck.ReadLines(path, path.EndsWith(".levyrec"), out r.Err);
			foreach (string l in lines)
			{
				try
				{
					using var d = JsonDocument.Parse(l); var e = d.RootElement; string k = e.GetProperty("k").GetString();
					if (k == "hdr") { r.Boss = e.GetProperty("boss").GetString(); if (e.TryGetProperty("meta", out var m) && m.TryGetProperty("trial", out var tr)) int.TryParse(tr.GetString(), out r.Trial); }
					else if (k == "end") { r.Reason = e.GetProperty("reason").GetString(); r.Ticks = e.GetProperty("ticks").GetInt32(); }
					else if (k == "e")
					{
						string ev = e.GetProperty("e").GetString();
						if (ev == "boss") r.Bosses.Add(e.GetProperty("name").GetString());
						if (ev == "bdeath" || ev == "bdespawn") r.Ends.Add((ev, e.GetProperty("name").GetString(), e.TryGetProperty("final", out var f) ? f.GetInt32() : 0));
					}
				}
				catch { }
			}
			return r;
		}

		private static List<Rec> Files(string dir) => Directory.Exists(dir) ? Directory.GetFiles(dir).Where(f => f.EndsWith(".jsonl") || f.EndsWith(".levyrec")).Select(Read).OrderBy(r => r.Trial).ToList() : new List<Rec>();

		private static int Boss(IEntitySource src, int dx, int type)
		{
			int i = NPC.NewNPC(src, (_cx + dx) * 16, (Floor - 25) * 16, type);
			if (i >= 0 && i < Main.maxNPCs) { NPC b = Main.npc[i]; b.aiStyle = -1; b.noGravity = true; b.noTileCollide = true; b.life = b.lifeMax = 2800; b.damage = 0; b.ai[3] = 4242f; b.timeLeft = 99999; return i; }
			return -1;
		}
		private static void Kill(int i) { if (i < 0) return; NPC n = Main.npc[i]; FightRecorder.NoteBossKill(n); n.life = 0; n.active = false; }
		private static void Step(int ticks) => FightRecAutoCheck.Step(ticks, _p, null);
		private static int Until(int max) => FightRecAutoCheck.StepUntilStopped(_p, -1, max);
		private static void Clear()
		{
			for (int q = 0; q < Main.maxNPCs; q++) { NPC n = Main.npc[q]; if (n.active && !(n.ModNPC is SoldierNPC) && !n.friendly && !n.townNPC) n.active = false; }
			for (int q = 0; q < Main.maxProjectiles; q++) if (Main.projectile[q].active && !Main.projectile[q].friendly) Main.projectile[q].active = false;
		}
		private static IEntitySource Summon() => new EntitySource_BossSpawn(_p); // what NPC.SpawnOnPlayer gives every boss a summon item or another boss calls in

		/// <summary>Runs one scenario in its own folder and returns the recordings it left, oldest first.</summary>
		private static List<Rec> Scenario(string root, string name, Action body)
		{
			string dir = Path.Combine(root, name); Directory.CreateDirectory(dir);
			if (FightRecorder.On) FightRecorder.Stop("self-check next");
			Clear(); FightRecAuto.ClearCooldown(); FightRecAuto.TestHeadlessDir = dir; Step(3);
			try { body(); }
			catch (Exception e) { Chk(false, name + " threw " + e.GetType().Name + ": " + e.Message); }
			if (FightRecorder.On) Until(2000);
			if (FightRecorder.On) FightRecorder.Stop("self-check over");
			Clear(); Step(30); // the short pause after a headless stop runs out, so the next scenario starts clean
			var recs = Files(dir);
			_mod.Logger.Info("[RecMulti] " + name + ": " + recs.Count + " recording(s): " + string.Join(" | ", recs.Select(r => Path.GetFileName(r.Path) + " reason=" + r.Reason + " ticks=" + r.Ticks + " ends=" + string.Join(",", r.Ends.Select(x => x.e + ":" + x.name + (x.fin == 1 ? "*" : ""))))));
			return recs;
		}

		internal static void Run(Mod mod, Player p, int cx, string tmp)
		{
			_mod = mod; _pass = _fail = 0; _p = p; _cx = cx;
			bool wasHeadless = FightRecorder.Headless, wasIgnore = FightRecorder.IgnoreInactive, wasClient = FightRecAuto.TestClient; string wasOut = FightRecorder.OutDirOverride; var wasMode = FightRecAuto.TestMode; int wasSeq = FightRecAuto.Seq;
			string root = Path.Combine(tmp, "recmulti");
			try
			{
				FightRecAuto.TestClient = false; FightRecAuto.TestMode = FightRecMode.Off; // the client path stays out: only the harness path starts recordings here
				int levies = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC) levies++;
				Chk(levies > 0, "a levy body stands (the headless recorder starts only beside the army): " + levies);
				bool cal = ModLoader.HasMod("CalamityMod");

				// ---- vanilla: the Twins. One eye falls first, the other fights on: one recording, ending when the second eye dies
				var twins = Scenario(root, "twins", () =>
				{
					int ret = Boss(Summon(), 25, NPCID.Retinazer), spa = Boss(Summon(), 30, NPCID.Spazmatism);
					Step(240); Chk(FightRecorder.On, "the Twins start a headless recording");
					Kill(ret); Step(300);
					Chk(FightRecorder.On, "Retinazer dies and Spazmatism fights on 5 s later: the recording runs on");
					Kill(spa);
				});
				Chk(twins.Count == 1 && twins[0].Reason == "boss gone" && twins[0].Ends.Count(e => e.e == "bdeath") == 2 && twins[0].Ends.Exists(e => e.fin == 1 && e.name == "Spazmatism"),
					"the Twins are one recording that ends on Spazmatism's death (" + Desc(twins) + ")");

				// ---- vanilla: a second boss summoned while the first stands (King Slime, then the Eye 3 s later); King Slime dies first. Before: "next fight started" at King Slime's death
				var overlap = Scenario(root, "overlap", () =>
				{
					int ks = Boss(Summon(), 25, NPCID.KingSlime);
					Step(180);
					int eye = Boss(Summon(), 35, NPCID.EyeofCthulhu);
					Step(240); Kill(ks); Step(300);
					Chk(FightRecorder.On, "King Slime dies, the Eye summoned while she stood fights on: the recording runs on");
					Kill(eye);
				});
				Chk(overlap.Count == 1 && overlap[0].Reason == "boss gone" && overlap[0].Ends.Exists(e => e.fin == 1 && e.name == "Eye of Cthulhu") && overlap[0].Bosses.Contains("Eye of Cthulhu"),
					"a boss summoned while another stands joins its fight: one recording that ends on the Eye's death (" + Desc(overlap) + ")");

				// ---- two separate bosses summoned 30 s apart (the first is dead by then): two recordings
				var apart = Scenario(root, "apart", () =>
				{
					int t0 = (int)Main.GameUpdateCount;
					int eye = Boss(Summon(), 25, NPCID.EyeofCthulhu);
					Step(600); Kill(eye); Until(400);
					Chk(!FightRecorder.On, "the first boss's recording ends once it is dead");
					Step(Math.Max(1, 1800 - ((int)Main.GameUpdateCount - t0)));
					int ks = Boss(Summon(), 25, NPCID.KingSlime);
					Step(400); Chk(FightRecorder.On, "the boss summoned 30 s after the first starts a recording of its own");
					Kill(ks);
				});
				Chk(apart.Count == 2 && apart[0].Boss == "Eye_of_Cthulhu" && apart[1].Boss == "King_Slime" && apart.All(r => r.Reason == "boss gone"), "two bosses summoned 30 s apart are two recordings (" + Desc(apart) + ")");

				// ---- a harness that clears the arena and spawns the next trial's boss in the same step: two recordings, the first ends as "next fight started" at once
				var swap = Scenario(root, "swap", () =>
				{
					int eye = Boss(Summon(), 25, NPCID.EyeofCthulhu);
					Step(400);
					Clear(); int ks = Boss(Summon(), 25, NPCID.KingSlime);
					int n = Until(60);
					Chk(!FightRecorder.On && n <= 3, "the cleared trial stops within a tick of the next trial's boss (" + n + " ticks)");
					Step(400); Chk(FightRecorder.On, "the next trial gets a recording of its own");
					Kill(ks);
				});
				Chk(swap.Count == 2 && swap[0].Reason == "next fight started" && swap[0].Boss == "Eye_of_Cthulhu" && swap[1].Boss == "King_Slime", "a cleared arena and a new boss in the same step are two recordings (" + Desc(swap) + ")");

				if (!cal) { _mod.Logger.Info("[RecMulti] Calamity is not loaded: the Leviathan and Exo Mechs cases run in the Calamity half"); return; }

				// ---- Calamity: Leviathan and Anahita, the real chain. Anahita's AI makes a LeviathanSpawner (NPC.GetSource_FromAI), the projectile summons the Leviathan (Projectile.GetSource_FromThis). Anahita dies first.
				bool haveLevi = ModContent.TryFind<ModNPC>("CalamityMod/Anahita", out var ana) & ModContent.TryFind<ModNPC>("CalamityMod/Leviathan", out var levi) & ModContent.TryFind<ModProjectile>("CalamityMod/LeviathanSpawner", out var spawner);
				Chk(haveLevi, "Calamity's Anahita, Leviathan and LeviathanSpawner are found");
				if (haveLevi)
				{
					int serialA = -1, serialL = -2;
					var leviRecs = Scenario(root, "levi", () =>
					{
						int a = Boss(Summon(), 25, ana.Type);
						Step(200);
						int pj = Projectile.NewProjectile(Main.npc[a].GetSource_FromAI(), Main.npc[a].Center, Vector2.Zero, spawner.Type, 0, 0f);
						Step(120);
						int l = Boss(Main.projectile[pj].GetSource_FromThis(), 32, levi.Type);
						Main.projectile[pj].active = false;
						serialA = FightRecSpawnTag.RawSerial(Main.npc[a]); serialL = FightRecSpawnTag.RawSerial(Main.npc[l]);
						Step(300); Kill(a); Step(600);
						Chk(FightRecorder.On, "Anahita dies and the Leviathan fights on 10 s later: the recording runs on");
						Kill(l);
					});
					Chk(serialA > 0 && serialA == serialL, "the Leviathan a LeviathanSpawner summons carries Anahita's spawn serial (" + serialA + " / " + serialL + ")");
					Chk(leviRecs.Count == 1 && leviRecs[0].Reason == "boss gone" && leviRecs[0].Ends.Exists(e => e.fin == 1 && e.name == Lang.GetNPCNameValue(levi.Type) && e.e == "bdeath") && leviRecs[0].Ends.Exists(e => e.name == Lang.GetNPCNameValue(ana.Type) && e.e == "bdeath" && e.fin == 0),
						"Leviathan and Anahita are one recording that ends on the Leviathan's death (" + Desc(leviRecs) + ")");

					// Anahita dies while her LeviathanSpawner is still out (the Leviathan comes 405 ticks after it is made): the end waits for the summoner, the Leviathan joins the same recording
					var leviLate = Scenario(root, "levilate", () =>
					{
						int a = Boss(Summon(), 25, ana.Type);
						Step(200);
						int pj = Projectile.NewProjectile(Main.npc[a].GetSource_FromAI(), Main.npc[a].Center, Vector2.Zero, spawner.Type, 0, 0f);
						Step(30); Kill(a); Step(300);
						Chk(FightRecorder.On, "Anahita is dead but her LeviathanSpawner is out: the recording waits (5 s on)");
						int l = Boss(Main.projectile[pj].GetSource_FromThis(), 32, levi.Type);
						Main.projectile[pj].active = false;
						Step(300); Kill(l);
					});
					Chk(leviLate.Count == 1 && leviLate[0].Reason == "boss gone" && leviLate[0].Ends.Exists(e => e.fin == 1 && e.name == Lang.GetNPCNameValue(levi.Type)) && leviLate[0].Ends.Count(e => e.fin == 1) == 1,
						"the Leviathan that arrives after Anahita's death is in her recording, and only its death is the fight's final end (" + Desc(leviLate) + " ends " + string.Join(",", leviLate.SelectMany(r => r.Ends).Select(e => e.e + ":" + e.name + (e.fin == 1 ? "*" : ""))) + ")");

					// a summoner that never delivers does not hold the fight open forever
					var stuck = Scenario(root, "spawnerstuck", () =>
					{
						int a = Boss(Summon(), 25, ana.Type);
						Step(200);
						Projectile.NewProjectile(Main.npc[a].GetSource_FromAI(), Main.npc[a].Center, Vector2.Zero, spawner.Type, 0, 0f);
						Step(30); Kill(a);
						int n = Until(2000);
						Chk(!FightRecorder.On && n <= FightRecorder.SpawnerHoldTicks + 20, "a LeviathanSpawner that never summons holds the end at most " + FightRecorder.SpawnerHoldTicks + " ticks (" + n + ")");
					});
					Chk(stuck.Count == 1, "it is still one recording (" + Desc(stuck) + ")");
				}

				// ---- Calamity: the Exo Mechs. Draedon calls the first mech from his AI; the mechs call each other in with NPC.SpawnOnPlayer (Apollo.cs, ThanatosHead.cs, AresBody.cs). One mech dies mid-attempt, the others fight on: one recording
				bool haveExo = ModContent.TryFind<ModNPC>("CalamityMod/Draedon", out var dra) & ModContent.TryFind<ModNPC>("CalamityMod/Apollo", out var apo) & ModContent.TryFind<ModNPC>("CalamityMod/Artemis", out var art) & ModContent.TryFind<ModNPC>("CalamityMod/ThanatosHead", out var tha) & ModContent.TryFind<ModNPC>("CalamityMod/AresBody", out var are);
				Chk(haveExo, "Calamity's Draedon and Exo Mechs are found");
				if (haveExo)
				{
					var exo = Scenario(root, "exo", () =>
					{
						int d = Boss(Summon(), 20, dra.Type);
						Step(120);
						int th = Boss(Main.npc[d].GetSource_FromAI(), 30, tha.Type);
						Step(240);
						int ar = Boss(Summon(), 36, are.Type), ap = Boss(Summon(), 40, apo.Type), at = Boss(Summon(), 42, art.Type);
						Step(240); Kill(th); Step(400);
						Chk(FightRecorder.On, "Thanatos (the first mech) dies, the mechs it called in fight on: the recording runs on");
						Kill(ar); Step(200); Kill(ap); Kill(at); Step(200);
						Chk(FightRecorder.On, "every mech is dead and Draedon still stands: the recording runs on");
						Main.npc[d].active = false; // Draedon leaves after his last line
					});
					Chk(exo.Count == 1 && exo[0].Bosses.Contains("XF-09 Ares") && exo[0].Ends.Count(e => e.e == "bdeath") >= 4, "an Exo Mechs attempt where mechs die one by one is one recording (" + Desc(exo) + ")");
				}
			}
			catch (Exception e) { Chk(false, "threw " + e); }
			finally
			{
				try { if (FightRecorder.On) FightRecorder.Stop("self-check over"); } catch { }
				Clear();
				FightRecAuto.TestHeadlessDir = null; FightRecorder.Headless = wasHeadless; FightRecorder.IgnoreInactive = wasIgnore; FightRecorder.OutDirOverride = wasOut;
				FightRecAuto.TestClient = wasClient; FightRecAuto.TestMode = wasMode; FightRecAuto.Seq = wasSeq; FightRecAuto.ClearCooldown();
				if (_fail == 0) mod.Logger.Info($"[RecMulti] self-check pass={_pass} fail=0");
				else mod.Logger.Warn($"[RecMulti] self-check pass={_pass} fail={_fail}");
			}
		}

		private static string Desc(List<Rec> recs) => recs.Count + " file(s): " + string.Join("; ", recs.Select(r => (r.Boss ?? "?") + " reason=" + (r.Reason ?? "none") + " " + r.Ticks + "t"));
	}
}
